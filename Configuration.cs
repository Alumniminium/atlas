namespace atlas
{
    public class Configuration
    {
        public ushort Port { get; set; }
        public Dictionary<string,Capsule> Capsules {get;set;}
    }
    public class Capsule
    {
        public string AbsoluteTlsCertPath { get; set; }
        public string AbsoluteRootPath { get; set; }
        public string FQDN { get; set; }
        public List<Location> Locations { get; set; }
    }
}