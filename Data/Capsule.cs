using System;
using System.Collections.Generic;
using System.IO;

namespace atlas.Data
{
    public class Capsule
    {
        public string AbsoluteTlsCertPath { get; set; } = string.Empty;
        public string AbsoluteRootPath { get; set; } = string.Empty;
        public string FQDN { get; set; } = string.Empty;
        public string Index { get; set; } = "index.gmi";
        public int MaxUploadSize { get; set; }
        public List<Location> Locations { get; set; } = new();

        public Location GetLocation(Uri uri)
        {
            foreach (var loc in Locations)
            {
                var absolutePath = Path.GetDirectoryName(Path.Combine(AbsoluteRootPath, uri.AbsolutePath[1..])) + "/";
                if (loc.AbsoluteRootPath == absolutePath)
                    return loc;
                if (uri.AbsolutePath.StartsWith("/cgi/"))
                    return new Location() { AbsoluteRootPath = AbsoluteRootPath + "/cgi/", CGI = true };
            }
            return null;
        }
    }
}