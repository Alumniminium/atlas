namespace atlas
{
    public class Capsule
    {
        public string TlsCertPath { get; set; }
        public string Root { get; set; }
        public string Hostname { get; set; }
        public List<Location> Locations { get; set; }
    }
    public class Configuration
    {
        public ushort Port { get; set; }
        public Dictionary<string,Capsule> Capsules {get;set;}
    }
}