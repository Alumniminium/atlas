namespace atlas
{
    public class Configuration
    {
        public ushort SpartanPort { get; set; } = 3000;
        public ushort GeminiPort { get; set; } = 1965;
        public Dictionary<string, Capsule> Capsules { get; set; }
    }
    public class Capsule
    {
        public string AbsoluteTlsCertPath { get; set; }
        public string AbsoluteRootPath { get; set; }
        public string FQDN { get; set; }
        public int MaxUploadSize { get; set; }
        public string Index { get; set; } = "index.gmi";
        public List<Location> Locations { get; set; }

        public Location GetLocation(Uri uri)
        {
            foreach (var loc in Locations)
            {
                var absolutePath = Path.GetDirectoryName(Path.Combine(AbsoluteRootPath, uri.AbsolutePath[1..])) + "/";
                if (loc.AbsoluteRootPath == absolutePath)
                    return loc;
            }
            return null;
        }
    }

    public class Location
    {
        public string Index { get; set; } = "index.gmi";
        public bool CGI { get; set; } = false;
        public bool DirectoryListing { get; set; }
        public string AbsoluteRootPath { get; set; }
        public bool AllowFileUploads { get; set; }
        public bool RequireClientCert { get; set; }
        public Dictionary<string, MimeConfig> AllowedMimeTypes { get; set; }
    }
    public class MimeConfig
    {
        public int MaxSizeBytes { get; set; }
    }
}