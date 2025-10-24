using System;
using System.Collections.Generic;
using System.Linq;

namespace atlas.Data
{
    public class Location
    {
        public string Index { get; set; } = Defaults.IndexFile;
        public bool CGI { get; set; }
        public bool DirectoryListing { get; set; }
        public string AbsoluteRootPath { get; set; } = string.Empty;
        public bool AllowFileUploads { get; set; }
        public int MaxUploadSize { get; set; }
        public bool RequireClientCert { get; set; }
        public string DefaultMimeType { get; set; } = Defaults.DefaultMimeType;
        public Dictionary<string, MimeConfig> AllowedMimeTypes { get; set; } = new();

        public bool IsAllowedMimeType(string mimeType)
        {
            var mimeTypeLower = mimeType.ToLowerInvariant();
            return AllowedMimeTypes.Any(allowed =>
            {
                var allowedLower = allowed.Key.ToLowerInvariant();
                if (allowedLower == mimeTypeLower)
                    return true;

                var parts = allowedLower.Split('/');
                if (parts.Length == 2 && parts[1] == "*")
                {
                    var mimeTypeParts = mimeTypeLower.Split('/');
                    return mimeTypeParts.Length == 2 && mimeTypeParts[0] == parts[0];
                }
                return false;
            });
        }
    }
}