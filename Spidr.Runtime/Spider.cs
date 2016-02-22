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
    public enum SpiderJobType
    {
        FULL,
        PAGE_ONLY,
        PING_ONLY
    }

    public class Spider
    {
        // instance fields
        public string Frontier { get; private set; }
        public int MaxAllowedPages { get; private set; }
        public int MaxAllowedTasks { get; private set; }
        public string UserAgent { get; private set; }
        public bool OnDomainPagesOnly { get; private set; }

        // spider drivers
        public Dictionary<string, Page> Visited { get; private set; }
        public Dictionary<string, UrlObject> Unvisited { get; private set; }

        // spider tasks
        public List<Task<Page>> SpiderTasks { get; private set; }
        public Task PersistenceTask { get; private set; }
        public CancellationTokenSource PersistenceCancellationSource { get; private set; }
        public SpiderJobType JobType { get; private set; }
        public static object VisitedLock = new object();

        public static List<string> ValidFileExtensions = new List<string>()
        {
            "pdf", "xls", "xlsx", "tar.gz", "mp3", "wav", "wma", "svg"
        };

        public static List<string> ValidImgExtensions = new List<string>()
        {
            "bmp", "jpg", "gif", "svg", "png", "tif"
        };

        public static List<string> ValidPageExtensions = new List<string>()
        {
            "htm", "html", "asp", "aspx", "php", "xhtml", "asmx", "ashx"
        };

        private static readonly ILog log = LogManager.GetLogger(typeof(Spider));

        public static string DefaultUserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";

        public Spider(string Frontier, SpiderJobType JobType, int MaxAllowedPages = 1000, bool OnDomainPagesOnly = true, int MaxAllowedTasks = 3)
        {
            this.Frontier = Frontier;
            this.MaxAllowedPages = MaxAllowedPages;
            this.MaxAllowedTasks = MaxAllowedTasks;
            this.UserAgent = DefaultUserAgent;
            this.JobType = JobType;
            this.OnDomainPagesOnly = OnDomainPagesOnly;
            this.Unvisited = new Dictionary<string, UrlObject>();
            this.Visited = new Dictionary<string, Page>();
            this.SpiderTasks = new List<Task<Page>>();
            this.PersistenceCancellationSource = new CancellationTokenSource();
            var ct = PersistenceCancellationSource.Token;
            if (!(this.JobType == SpiderJobType.PING_ONLY))
            {
                this.PersistenceTask = new Task(new Action(() =>
                {
                    MySqlPersistence g = new MySqlPersistence();
                    while (!ct.IsCancellationRequested)
                    {
                        lock (VisitedLock)
                        {
                            var unprocessed = Visited.Where(x => x.Value.Processed == false);
                            foreach (KeyValuePair<string, Page> page in unprocessed)
                            {
                                if (this.JobType == SpiderJobType.PAGE_ONLY)
                                {
                                    page.Value.LinkTags = new List<LinkTag>();
                                }
                                g.PersistData(page.Value);
                                page.Value.Processed = true;
                            }
                        }
                        Thread.Sleep(1000);
                    }
                }), ct);
                this.PersistenceTask.Start();
            }
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
                Unvisited.Add(starter.GetFullPath(false), starter);
            }

            // while still pages unprocessed
            while (Unvisited.Count() > 0
                && Visited.Count() < MaxAllowedPages)
            {
                // hard limit on number of tasks
                var numberOfAddedTasks = MaxAllowedTasks - SpiderTasks.Count();
                for (var i = 0; i < numberOfAddedTasks; i++)
                {
                    if (i < Unvisited.Count())
                    {
                        var ctr = i;
                        SpiderTasks.Add(Task.Factory.StartNew(() => {
                            try
                            {
                                return PageFromUrl(Unvisited.ElementAt(ctr).Value);
                            }
                            catch (ArgumentOutOfRangeException) { return null; };
                        }));
                    }
                }

                // chack for tasks that ran to completion and process them
                for (var i = 0; i < SpiderTasks.Count(); i++)
                {
                    if (SpiderTasks[i].Status == TaskStatus.RanToCompletion)
                    {
                        Task<Page> t = SpiderTasks[i];
                        Page p = t.Result;
                        ProcessNewPaths(p, starter);
                    }
                }

                // remove finished, cancelled, or faulted tasks
                SpiderTasks.RemoveAll(x => x.Status == TaskStatus.RanToCompletion
                || x.Status == TaskStatus.Canceled
                || x.Status == TaskStatus.Faulted);
            }
        }

        public void ProcessNewPaths(Page p, UrlObject domainObject)
        {
            if (p != null
                && domainObject != null)
            {
                Console.WriteLine("Visited: " + p.Link.GetFullPath(false));
                Unvisited.Remove(p.Link.GetFullPath(false));
                lock (VisitedLock)
                {
                    Visited.Add(p.Link.GetFullPath(false), p);
                }

                foreach (LinkTag l in p.LinkTags)
                {
                    bool toBeVisited = false;
                    bool visited = false;
                    try
                    {
                        var key = Unvisited[l.Url.GetFullPath(false)];
                        toBeVisited = true;
                    }
                    catch (KeyNotFoundException /* knfe */) { }

                    try
                    {
                        lock (VisitedLock)
                        {
                            var key = Visited[l.Url.GetFullPath(false)];
                        }
                        visited = true;
                    }
                    catch (KeyNotFoundException /* knfe */) { }

                    if (toBeVisited != true 
                        & visited != true)
                    {
                        if (l.Url.GetDomain() == domainObject.GetDomain())
                            Unvisited.Add(l.Url.GetFullPath(false), l.Url);
                    }
                }
            }
        }

        public string StringFromAddress(string address)
        {
            string content = null;
            var request = (HttpWebRequest)WebRequest.Create(address);
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
            try
            {
                string fullPath = address.GetFullPath(false);
                string pageContent = StringFromAddress(fullPath); // Client.DownloadString yada yada yada...

                string title = GetTitle(pageContent);
                var pageId = Guid.NewGuid();

                List<LinkTag> links = null;
                links = GetLinks(pageId, fullPath, pageContent);

                List<BinaryFile> images = null;
                if (this.JobType == SpiderJobType.PAGE_ONLY 
                    || this.JobType == SpiderJobType.PING_ONLY)
                {
                    images = new List<BinaryFile>();
                }
                else
                {
                    images = GetImages(pageId, fullPath, pageContent);
                }

                List<BinaryFile> files = null;
                if (this.JobType == SpiderJobType.PAGE_ONLY 
                    || this.JobType == SpiderJobType.PING_ONLY)
                {
                    files = new List<BinaryFile>();
                }
                else
                {
                    files = GetFiles(pageId, fullPath, pageContent);
                }

                return new Page()
                {
                    Content = pageContent,
                    Name = title,
                    FileTags = files,
                    ImageTags = images,
                    LinkTags = links,
                    Link = address,
                    PageId = pageId
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
                return "NOTITLE";
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
                    if (urlObject.Path.LastOrDefault() != null)
                    {
                        bool hasValidExtension = false;
                        foreach (var extension in ValidImgExtensions)
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
                                images.Add(new BinaryFile(pageId)
                                {
                                    Url = urlObject,
                                    Tag = image.OuterHtml,
                                    Name = urlObject.Path.LastOrDefault(),
                                    Contents = new MemoryStream(fileBytes),
                                });
                                Console.WriteLine("Found image: " + urlObject.GetFullPath(false));
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
                                Console.WriteLine("Found files: " + urlObject.GetFullPath(false));
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
                            Console.WriteLine("Found link: " + urlObject.GetFullPath(false));
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
