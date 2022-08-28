using System.Text.Json;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas
{
    class Program
    {
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
            geminiServer = new ();
            geminiServer.Start();
            Console.WriteLine("Starting Spartan...");
            spartanServer = new ();
            spartanServer.Start();
            Console.WriteLine("Atlas Ready!");

            while(true)
                Thread.Sleep(int.MaxValue);
        }

        private static void LoadConfig()
        {
            if (File.Exists("/etc/atlas/config.json"))
                Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("/etc/atlas/config.json"));
                
            if (Config == null)
            {
                Console.WriteLine("Failed to load configuration. Does config.json exist?");
                Console.WriteLine($"Looking @ '/etc/atlas/config.json'");
                Console.WriteLine($"");
                Console.WriteLine($"--- Creating Default Configuration ---");
                Console.WriteLine($"");
                Console.WriteLine(Configuration.CreateSampleConfig());
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