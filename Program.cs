using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas
{
    class Program
    {
        public static string Version = "0.2a";
        public static Configuration Config { get; set; }
        public static Dictionary<string, string> ExtensionToMimeType = new();
        public static Dictionary<string, string> MimeTypeToExtension = new();
        public static GeminiServer geminiServer;
        public static SpartanServer spartanServer;

        static void Main()
        {
            Console.WriteLine("Loading MimeMap...");
            LoadMimeMap();
            Console.WriteLine("Loading Config...");
            LoadConfig();
            Console.WriteLine("Starting Gemini...");
            geminiServer = new();
            geminiServer.Start();
            Console.WriteLine("Starting Spartan...");
            spartanServer = new();
            spartanServer.Start();
            Console.WriteLine("Atlas Ready!");

            while (true)
                Thread.Sleep(int.MaxValue);
        }

        private static async void LoadConfig()
        {
            var configPath = "/etc/atlas/config.json";
            if (File.Exists(configPath))
            {
                Console.WriteLine($"Loading /etc/atlas/config.json ...");
                Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("/etc/atlas/config.json"));
            }
            else if (File.Exists("config.json"))
            {
                configPath = "config.json";
                Console.WriteLine($"Loading {Environment.CurrentDirectory}/config.json ...");
                Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"));
            }

            if (Config == null)
            {
                Console.WriteLine("Failed to load configuration. Does config.json exist?");
                Console.WriteLine($"Looking @ '/etc/atlas/config.json'");
                Console.WriteLine($"Looking @ '{Environment.CurrentDirectory}/config.json'");
                Console.WriteLine($"");
                Console.WriteLine($"--- Creating Default Configuration ---");
                Console.WriteLine($"");
                Console.WriteLine(Configuration.CreateSampleConfig());
                Console.WriteLine($"");
                Console.WriteLine($"--- ^ Ready for Copy/Paste/Edit ^ ---");
                Environment.Exit(0);
            }
            else
            {
                foreach (var vhost in Config.Capsules)
                {
                    if (string.IsNullOrWhiteSpace(vhost.Value.AbsoluteTlsCertPath) || !File.Exists(vhost.Value.AbsoluteTlsCertPath))
                    {
                        var ecdsa = ECDsa.Create();
                        var req = new CertificateRequest("cn=" + vhost.Value.FQDN, ecdsa, HashAlgorithmName.SHA512);
                        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));

                        vhost.Value.AbsoluteTlsCertPath = Path.Combine(vhost.Value.AbsoluteRootPath, vhost.Value.FQDN + ".pfx");
                        Console.WriteLine($"Certificate for {vhost.Value.FQDN}not found. Creating new one at {vhost.Value.AbsoluteTlsCertPath}");
                        File.WriteAllBytes(vhost.Value.AbsoluteTlsCertPath, cert.Export(X509ContentType.Pfx));

                        Console.WriteLine($"Updating {configPath} with certificate...");
                        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault });
                        File.WriteAllText(configPath, json);
                        Console.WriteLine($"Updated {configPath}");
                    }
                }
            }
        }

        private static void LoadMimeMap()
        {
            var lines = File.ReadLines("mimetypes.tsv");
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                ExtensionToMimeType.Add(parts[0], parts[1]);
            }
        }
    }
}