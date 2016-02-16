using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.ComponentModel;
using System.Net;
using HtmlAgilityPack;
using log4net;
using Newtonsoft.Json;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace Spidr.Runtime
{
    public class Spider
    {
        // instance fields
        public string Frontier { get; private set; }
        public int MaxAllowedPages { get; private set; }
        public int MaxAllowedTasks { get; private set; }
        public string UserAgent { get; private set; }
        public bool OnDomainPagesOnly { get; private set; }

        // spider drivers
        public List<Page> Visited { get; private set; }
        public List<UrlObject> Unvisited { get; private set; }

        // spider tasks
        public Stack<Task<Page>> SpiderTasks { get; private set; }

        public static List<string> ValidFileExtensions = new List<string>()
        {
            "pdf", "xls", "xlsx", "tar.gz", "mp3", "wav", "wma", "svg"
        };

        public static List<string> ValidPageExtensions = new List<string>()
        {
            "htm", "html", "asp", "aspx", "php", "xhtml", "asmx", "ashx"
        };

        private static readonly ILog log = LogManager.GetLogger(typeof(Spider));

        public Spider(string Frontier, int MaxAllowedPages = 1000, bool OnDomainPagesOnly = true, int MaxAllowedTasks = 3)
        {
            this.Frontier = Frontier;
            this.MaxAllowedPages = MaxAllowedPages;
            this.MaxAllowedTasks = MaxAllowedTasks;
            this.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";
            this.OnDomainPagesOnly = OnDomainPagesOnly;
            this.Unvisited = new List<UrlObject>();
            this.Visited = new List<Page>();
            this.SpiderTasks = new Stack<Task<Page>>();
        }

        public void Start()
        {
            // ignore ssl errors
            ServicePointManager.ServerCertificateValidationCallback = delegate (
            Object obj, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors errors)
            {
                return (true);
            };

            // start
            UrlObject starter = UrlObject.FromString(Frontier);
            if (Unvisited.Count() == 0)
            {
                Unvisited.Add(starter);
            }

            while (Unvisited.Count() > 0
                && Visited.Count() < MaxAllowedPages)
            {
                for (var i = 0; i < MaxAllowedTasks; i++)
                {
                    if (i < Unvisited.Count())
                    {
                        var ctr = i;
                        SpiderTasks.Push(Task.Factory.StartNew(() => PageFromUrl(Unvisited[ctr])));
                    }
                }

                for (var i = 0; i < SpiderTasks.Count(); i++)
                {
                    Task<Page> t = SpiderTasks.Peek();
                    Page p = t.Result;
                    if (p != null)
                    {
                        Console.WriteLine("Visited: " + p.Link.GetFullPath(false));
                        Visited.Add(p);
                        Unvisited.RemoveAll(x => x.AssociatedPage == p.PageId);
                        foreach (LinkTag l in p.LinkTags)
                        {
                            bool toBeVisited = false;
                            bool visited = false;
                            foreach (UrlObject url in Unvisited)
                            {
                                if (url == l.Url)
                                {
                                    toBeVisited = true;
                                }
                            }

                            foreach (Page page in Visited)
                            {
                                if (page.Link == l.Url)
                                {
                                    visited = true;
                                }
                            }

                            if (toBeVisited != true && visited != true)
                            {
                                if (l.Url.GetDomain() == starter.GetDomain())
                                    Unvisited.Add(l.Url);
                            }
                        }
                    }
                    SpiderTasks.Pop();
                }

                MySqlPersistence g = new MySqlPersistence();
                foreach (Page page in Visited)
                {
                    if (page.Processed == false)
                    {
                        g.PersistData(page);
                        page.Processed = true;
                    }
                }
            }
        }

        public string StringFromAddress(string address)
        {
            string content = null;
            var request = (HttpWebRequest) WebRequest.Create(address);
            request.Timeout = 10000;
            request.AllowAutoRedirect = true;
            var response = (HttpWebResponse)request.GetResponse();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                content = sr.ReadToEnd();
            }
            return content;
        }

        public Page PageFromUrl(UrlObject address)
        {
            return GetPage(address);
        }

        private Page GetPage(UrlObject address)
        {
            // web client for downloading the file
            /*
            WebClient Client = new WebClient();
            */
            try
            {
                string fullPath = address.GetFullPath(false);
                /*
                Client.UseDefaultCredentials = true;
                Client.DownloadProgressChanged += Client_DownloadProgressChanged;
                Client.Headers.Add("user-agent", this.UserAgent);
                Client.Encoding = System.Text.Encoding.UTF8;
                */
                string pageContent = StringFromAddress(fullPath); // Client.DownloadString yada yada yada...

                string title = GetTitle(pageContent);
                List<LinkTag> links = GetLinks(address.AssociatedPage, fullPath, pageContent);
                List<BinaryFile> images = GetImages(address.AssociatedPage, fullPath, pageContent);
                List<BinaryFile> files = GetFiles(address.AssociatedPage, fullPath, pageContent);

                return new Page()
                {
                    Content = pageContent,
                    Name = title,
                    FileTags = files,
                    ImageTags = images,
                    LinkTags = links,
                    Link = address,
                    PageId = address.AssociatedPage
                };
            }
            catch (Exception wex)
            {
                log.Warn(wex);
                return null;
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine(" - " + e.ProgressPercentage + "%");
        }

        private string GetTitle(string content)
        {
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(content);
            try
            {
                return document.DocumentNode.SelectSingleNode("//title").InnerText.Trim();
            }
            catch (Exception e)
            {
                log.Warn("No title in document", e);
                return "No Title";
            }
        }

        private List<BinaryFile> GetImages(Guid pageId, string address, string content)
        {
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(content);
            List<BinaryFile> images = new List<BinaryFile>();
            WebClient downloadClient = new WebClient();
            try
            {
                foreach (HtmlNode image in document.DocumentNode.SelectNodes("//img[@src]"))
                {
                    HtmlAttribute imgSrcAttribute = image.Attributes["src"];
                    UrlObject urlObject = UrlObject.FromRelativeString(address, imgSrcAttribute.Value.ToString());
                    urlObject.AssociatedPage = pageId;
                    try
                    {
                        byte[] fileBytes = downloadClient.DownloadData(urlObject.GetFullPath(false));
                        images.Add(new BinaryFile(pageId)
                        {
                            Url = urlObject,
                            Tag = image.OuterHtml,
                            Name = urlObject.Path.LastOrDefault(),
                            Contents = new MemoryStream(fileBytes)
                        });
                    }
                    catch (WebException wex)
                    {
                        log.Warn(wex);
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("No image tags present in document", e);
            }
            return images;
        }

        private List<BinaryFile> GetFiles(Guid pageId, string address, string content)
        {
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(content);
            List<BinaryFile> files = new List<BinaryFile>();
            WebClient downloadClient = new WebClient();

            try
            {
                //get all of the hrefs on the page
                foreach (HtmlNode link in document.DocumentNode.SelectNodes("//a[@href]"))
                {
                    HtmlAttribute hrefAttribute = link.Attributes["href"];
                    UrlObject urlObject = UrlObject.FromRelativeString(address, hrefAttribute.Value.ToString());
                    urlObject.AssociatedPage = pageId;
                    if (urlObject.Path.LastOrDefault() != null)
                    {
                        bool hasValidExtension = false;
                        foreach (var extension in ValidFileExtensions)
                        {
                            if (urlObject.Path.LastOrDefault().Contains("." + extension))
                            {
                                hasValidExtension = true;
                            }
                        }

                        if (hasValidExtension)
                        {
                            try
                            {
                                byte[] fileBytes = downloadClient.DownloadData(urlObject.GetFullPath(false));
                                files.Add(new BinaryFile(pageId)
                                {
                                    Url = urlObject,
                                    Tag = link.OuterHtml,
                                    Name = urlObject.Path.LastOrDefault(),
                                    Contents = new MemoryStream(fileBytes)
                                });
                            }
                            catch (WebException wex)
                            {
                                log.Warn(wex);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("No files in the document", e);
            }
            return files;
        }

        private List<LinkTag> GetLinks(Guid pageId, string address, string content)
        {
            //attempt to parse the document
            HtmlDocument Document = new HtmlDocument();
            Document.LoadHtml(content);
            List<LinkTag> tags = new List<LinkTag>();
            try
            {
                //get all of the hrefs on the page
                foreach (HtmlNode Link in Document.DocumentNode.SelectNodes("//a[@href]"))
                {
                    HtmlAttribute hrefAttribute = Link.Attributes["href"];
                    UrlObject urlObject = UrlObject.FromRelativeString(address, hrefAttribute.Value.ToString());
                    urlObject.AssociatedPage = pageId;
                    if (urlObject.Path.LastOrDefault() != null)
                    {
                        bool hasValidExtension = false;
                        foreach (var extension in ValidFileExtensions)
                        {
                            if (urlObject.Path.LastOrDefault().Contains("." + extension))
                            {
                                hasValidExtension = true;
                            }
                        }

                        if (!hasValidExtension)
                        {
                            tags.Add(new LinkTag(pageId)
                            {
                                Tag = Link.OuterHtml,
                                Url = urlObject
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("No links available in the document", e);
            }
            return tags;
        }
    }
}
