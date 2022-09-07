using System;
using System.Net;

namespace atlas.Servers.Gemini
{
    public class GeminiRequest
    {
        private const int GEMINI_DEFAULT_PORT = 1965;
        public Uri _url;
        public GeminiRequest(Uri url) => _url = url;

        public static bool TryParse(string url, out GeminiRequest request)
        {
            var uri = new Uri(url);
            request = default;

            if (!uri.IsAbsoluteUri)
                return false;
            if (uri.Scheme != "gemini")
                return false;
            if (string.IsNullOrEmpty(uri.Host))
                return false;

            request = new GeminiRequest(uri);
            return true;
        }

        public int Port => (_url.Port > 0) ? _url.Port : GEMINI_DEFAULT_PORT;
        public string Authority => $"{Hostname}:{Port}";
        public string Hostname => (_url.HostNameType == UriHostNameType.IPv6) ? _url.Host : _url.DnsSafeHost;
        public string Path => _url.AbsolutePath;
        public string Filename => System.IO.Path.GetFileName(Path);
        public string FileExtension
        {
            get
            {
                var ext = System.IO.Path.GetExtension(Path);
                return (ext.Length > 1) ? ext[1..] : ext;
            }
        }
        public bool HasQuery => _url.Query.Length > 1;
        public string RawQuery => (_url.Query.Length > 1) ? _url.Query[1..] : "";
        public string RootUrl => Port == GEMINI_DEFAULT_PORT ? $"gemini://{Hostname}/" : $"gemini://{Hostname}{Path}:{Port}/";
        public string Query => WebUtility.UrlDecode(RawQuery);
        public string Fragment => (_url.Fragment.Length > 1) ? _url.Fragment[1..] : "";
        public string NormalizedUrl => Port == GEMINI_DEFAULT_PORT ? $"gemini://{Hostname}{Path}{_url.Query}" : $"gemini://{Hostname}:{Port}{Path}{_url.Query}";
        public static GeminiRequest Rewrite(GeminiRequest request, string relateiveTarget)
        {
            try
            {
                var newUrl = new Uri(request._url, relateiveTarget);
                return (newUrl.Scheme == "gemini") ? new GeminiRequest(newUrl) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }       
        public override string ToString() => NormalizedUrl;
    }
}