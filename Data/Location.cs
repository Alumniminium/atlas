using System.Collections.Generic;

namespace atlas.Data
{
    public class Location
    {
        public string Index { get; set; } = "index.gmi";
        public bool CGI { get; set; }
        public bool DirectoryListing { get; set; }
        public string AbsoluteRootPath { get; set; } = string.Empty;
        public bool AllowFileUploads { get; set; }
        public bool RequireClientCert { get; set; }
        public Dictionary<string, MimeConfig> AllowedMimeTypes { get; set; } = new();
    }
}