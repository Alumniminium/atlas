namespace atlas
{
    public class Configuration
    {
        public ushort Port { get; set; }
        public Dictionary<string, Capsule> Capsules { get; set; }
    }
    public class Capsule
    {
        public string AbsoluteTlsCertPath { get; set; }
        public string AbsoluteRootPath { get; set; }
        public string FQDN { get; set; }
        public List<Location> Locations { get; set; }
        public int MaxUploadSize { get; set; }
        public string Index {get;set;} = "index.gmi";

        public Location GetLocation(Uri uri) => Locations.Where(x=>x.AbsoluteRootPath.EndsWith(uri.AbsolutePath)).FirstOrDefault();
    }

    public class Location
    {
        public string Index {get;set;} = "index.gmi";
        public bool DirectoryListing { get; set; }
        public string AbsoluteRootPath { get; set; }
        public bool AllowFileUploads { get; set; }
        public List<MimeConfig> AllowedMimeTypes { get; set; }
        public bool RequireClientCert { get; set; }
    }
    public class MimeConfig
    {
        public string MimeType { get; set; }
        public int MaxSizeBytes { get; set; }
    }
}