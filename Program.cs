using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace atlas
{
    class Program
    {
        public static Dictionary<string, string> ExtensionToMimeType = new();
        public static Dictionary<string, string> MimeTypeToExtension = new();

        static async Task Main(string[] args)
        {
            LoadMimeMap();
            LoadConfig();
            Console.WriteLine("Atlas ready!");
            await Server.Start();
        }

        private static void LoadConfig()
        {
            if (File.Exists("config.json"))
                Server.Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"));
            else if (File.Exists("/etc/atlas/config.json"))
                Server.Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("/etc/atlas/config.json"));
            if (Server.Config == null)
            {
                Console.WriteLine("Failed to load configuration. Does config.json exist?");
                Console.WriteLine($"Looking @ '/etc/atlas/config.json'");
                Console.WriteLine($"Looking @ '{Environment.CurrentDirectory}/config.json'");
                Console.WriteLine($"");
                Console.WriteLine($"--- Creating Default Configuration ---");
                Console.WriteLine($"");
                Server.Config = new Configuration()
                {
                    Port = 1965,
                    Capsules = new()
                    {
                        [Environment.MachineName] = new Capsule()
                        {
                            FQDN = Environment.MachineName,
                            AbsoluteTlsCertPath = $"{Environment.MachineName}.pfx",
                            AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/",
                            Locations = new()
                            {
                                new Location()
                                {
                                    AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/",
                                    DirectoryListing = false
                                },
                                new Location()
                                {
                                    AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/files/",
                                    DirectoryListing = true
                                },
                            }
                        }
                    }

                };
                var json = JsonSerializer.Serialize(Server.Config, new JsonSerializerOptions() { WriteIndented = true });
                Console.WriteLine(json);
                Environment.Exit(0);
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