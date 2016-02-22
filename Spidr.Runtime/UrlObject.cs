using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;

namespace Spidr.Runtime
{
    public class UrlObject
    {
        private static List<string> ValidProtocols = new List<string>
        {
            "http",
            "https",
            "ftp"
        };

        private static List<List<string>> ValidDomains = new List<List<string>>
        {
            new List<string> { "edu" },
            new List<string> { "org" },
            new List<string> { "com" },
            new List<string> { "gov" },
            new List<string> { "io" },
            new List<string> { "co", "uk" },
            new List<string> { "co", "nz" }
        };

        public Guid UrlId { get; set; }
        public bool Valid { get; set; }
        public bool Relative { get; set; }
        public string Protocol { get; set; }
        public bool IsWWWAddress { get; set; }
        public string[] DomainPrefix { get; set; }
        public string DomainName { get; set; }
        public string[] DomainSuffix { get; set; }
        public string[] Path { get; set; }
        public Dictionary<string, string> QueryParams { get; set; }
        public string OptContent { get; set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(UrlObject));

        public UrlObject()
        {
            DomainPrefix = new string[] { };
            DomainSuffix = new string[] { };
            Path = new string[] { };
            QueryParams = new Dictionary<string, string>();
        }

        public string GetDomain()
        {
            string result = (DomainName != null ? DomainName : string.Empty);
            if (DomainSuffix != null)
            {
                foreach (var suffix in DomainSuffix)
                {
                    result += "." + suffix;
                }
            }
            return result;
        }

        public string GetFullPath(bool queryParams)
        {
            try
            {
                // test: log.Info(JsonConvert.SerializeObject(this, Formatting.Indented));
                if (Valid)
                {
                    if (!Relative)
                    {
                        return (Protocol != null ? Protocol + "://" : "") + (IsWWWAddress ? "www." : string.Empty)
                            + (DomainPrefix != null ? (DomainPrefix.Aggregate("", (x, y) => x + (y != null ? y + "." : ""))) : "")
                            + (DomainName != null ? DomainName : "")
                            + (DomainSuffix != null ? (DomainSuffix.Aggregate("", (x, y) => x + (y != null ? "." + y : ""))) : "")
                            + (Path != null ? Path.Aggregate("", (x, y) => x + (y != null ? "/" + y : "")) : "");
                    }
                    else
                    {
                        return (Path != null ? Path.Aggregate("", (x, y) => x + "/" + y) : "");
                    }
                }
                else
                {
                    return OptContent;
                }
            }
            catch (Exception e)
            {
                log.Warn(e);
                log.Info(JsonConvert.SerializeObject(this, Formatting.Indented));
                return string.Empty;
            }
        }

        public static bool operator !=(UrlObject o1, UrlObject o2)
        {
            return o1.GetFullPath(false) != o2.GetFullPath(false);
        }

        public static bool operator ==(UrlObject o1, UrlObject o2)
        {
            return o1.GetFullPath(false) == o2.GetFullPath(false);
        }

        public override bool Equals(Object o1)
        {
            UrlObject o1obj = (UrlObject)o1;
            return o1obj.GetFullPath(false) == this.GetFullPath(false);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static UrlObject FromRelativeString(string baseUrl, string relativeUrl)
        {
            string fullUrl = null;
            if (!relativeUrl.StartsWith("http"))
            {
                if (relativeUrl.StartsWith("#")
                    || relativeUrl.StartsWith("javascript")
                    || relativeUrl.StartsWith("mailto"))
                {
                    fullUrl = relativeUrl;
                }
                else
                {
                    fullUrl = new Uri(new Uri(baseUrl), relativeUrl).ToString();
                }
            }
            else
            {
                fullUrl = relativeUrl;
            }
            return FromString(fullUrl);
        }

        public static bool IsRelativeUrl(string urlValue)
        {
            return urlValue.StartsWith("/");
        }

        public static UrlObject FromString(string urlValue)
        {
            urlValue = urlValue.ToLowerInvariant();
            if (urlValue.StartsWith("#") 
                || urlValue.StartsWith("javascript")
                || urlValue.StartsWith("mailto"))
            {
                var guid = Guid.NewGuid();
                return new UrlObject()
                {
                    Valid = false,
                    Relative = false,
                    Protocol = null,
                    Path = ParsePath(urlValue, true),
                    QueryParams = GetQueryParams(urlValue),
                    OptContent = urlValue,
                    UrlId = guid
                };
            }
            else if (urlValue.StartsWith("/"))
            {
                var guid = Guid.NewGuid();
                return new UrlObject()
                {
                    Valid = true,
                    Relative = true,
                    Protocol = null,
                    Path = ParsePath(urlValue, true),
                    QueryParams = GetQueryParams(urlValue),
                    OptContent = null,
                    UrlId = guid
                };
            }
            else
            {
                var guid = Guid.NewGuid();
                return new UrlObject()
                {
                    Valid = true,
                    Relative = false,
                    Protocol = ParseProtocol(urlValue),
                    IsWWWAddress = FindIsWWWAddress(urlValue),
                    DomainPrefix = ParseDomainPrefix(urlValue),
                    DomainName = FindDomainName(urlValue),
                    DomainSuffix = ParseDomainSuffix(urlValue),
                    Path = ParsePath(urlValue, false),
                    QueryParams = GetQueryParams(urlValue),
                    OptContent = null,
                    UrlId = guid
                };
            }
        }

        private static Dictionary<string, string> GetQueryParams(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://" }, StringSplitOptions.None)[1];
                string[] slashDelimitedPath = nonProtocolValue.Split(new string[] { "/" }, StringSplitOptions.None).Skip(1).ToArray();
                Dictionary<string, string> queryParams = new Dictionary<string, string>();
                if (slashDelimitedPath.Length > 0)
                {
                    if (slashDelimitedPath[slashDelimitedPath.Length - 1].Split('?').Count() > 1)
                    {
                        var queryString = slashDelimitedPath[slashDelimitedPath.Length - 1] = slashDelimitedPath[slashDelimitedPath.Length - 1].Split('?')[1];
                        var splitParams = queryString.Split('&');
                        foreach (var splitParam in splitParams)
                        {
                            var keyValuePair = splitParam.Split('=');
                            if (keyValuePair.Count() == 2)
                            {
                                queryParams.Add(keyValuePair[0], keyValuePair[1]);
                            }
                        }
                    }
                }
                return queryParams;
            }
            catch (Exception /*e*/)
            {
                // simply is not all that important
                // log.Warn(e);
                // throw;
                return new Dictionary<string, string>();
            }
        }

        public static string[] ParsePath(string urlValue, bool relative)
        {
            try
            {
                string nonProtocolValue = null;
                if (!relative)
                {
                    var values = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.None);
                    var splitValues = new List<string>();
                    for (var i = 2; i < values.Length; i++)
                    {
                        splitValues.Add(values[i]);
                    }
                    nonProtocolValue = splitValues.Aggregate("", (x, y) => x + "/" + y);
                }
                else
                {
                    nonProtocolValue = urlValue.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                string[] slashDelimitedPath = nonProtocolValue.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
                if (slashDelimitedPath.Count() > 0)
                {
                    if (slashDelimitedPath[slashDelimitedPath.Length - 1].Split('?').Count() > 1)
                    {
                        slashDelimitedPath[slashDelimitedPath.Length - 1] = slashDelimitedPath[slashDelimitedPath.Length - 1].Split('?')[0];
                    }
                }
                return slashDelimitedPath;
            }
            catch (Exception e)
            {
                log.Warn(e);
                return new string[] { };
                // throw;
            }
        }

        private static string ParseProtocol(string urlValue)
        {
            try
            {
                return urlValue.Split(new string[] { "://" }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }
        }

        private static bool FindIsWWWAddress(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] domainValues = nonProtocolValue.Split('.');
                if (domainValues.Contains("www"))
                {
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                log.Error(e);
                return false;
                // throw;
            }
        }

        private static string FindDomainName(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] domainValues = nonProtocolValue.Split('.');
                foreach (var validDomain in ValidDomains)
                {
                    var comparisonDomain = validDomain.Aggregate("", (x, y) => x + y + ".").TrimEnd('.');
                    if (nonProtocolValue.Contains(comparisonDomain))
                    {
                        return domainValues[domainValues.Length - validDomain.Count() - 1];
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }
        }

        private static int FindDomainNameIndex(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] domainValues = nonProtocolValue.Split('.');
                foreach (var validDomain in ValidDomains)
                {
                    var comparisonDomain = validDomain.Aggregate("", (x, y) => x + y + ".").TrimEnd('.');
                    if (nonProtocolValue.Contains(comparisonDomain))
                    {
                        return domainValues.Length - validDomain.Count() - 1;
                    }
                }
                return -1;
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }
        }

        private static string[] ParseDomainPrefix(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] domainValues = nonProtocolValue.Split('.');
                int domainNameIndex = FindDomainNameIndex(urlValue);
                if (domainNameIndex != -1)
                {
                    List<string> prefix = new List<string>();
                    for (var i = 0; i < domainNameIndex; i++)
                    {
                        if (domainValues[i] != "www")
                            prefix.Add(domainValues[i]);
                    }
                    return prefix.ToArray();
                }
                return null;
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }
        }

        private static string[] ParseDomainSuffix(string urlValue)
        {
            try
            {
                string nonProtocolValue = urlValue.Split(new string[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string[] domainValues = nonProtocolValue.Split('.');
                int domainNameIndex = FindDomainNameIndex(urlValue);
                if (domainNameIndex != -1)
                {
                    List<string> suffix = new List<string>();
                    for (var i = domainNameIndex + 1; i < domainValues.Length; i++)
                    {
                        suffix.Add(domainValues[i]);
                    }
                    return suffix.ToArray();
                }
                return null;
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }
        }
    }
}
