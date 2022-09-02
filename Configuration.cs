using System.Diagnostics;
using System.Text.Json;

namespace atlas
{
    public class Configuration
    {
        public ushort SpartanPort { get; set; } = 300;
        public ushort GeminiPort { get; set; } = 1965;
        public Dictionary<string, Capsule> Capsules { get; set; }
        public bool SlowMode { get; set; }

        internal static object CreateSampleConfig()
        {
            var Config = new Configuration()
            {
                GeminiPort = 1965,
                SpartanPort = (ushort)(Debugger.IsAttached ? 3000 : 300),
                Capsules = new()
                {
                    [Environment.MachineName] = new Capsule()
                    {
                        FQDN = Environment.MachineName,
                        AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/",
                        MaxUploadSize = 1024 * 1024 * 4,
                        Index = "index.gmi",
                        Locations = new()
                            {
                                new Location()
                                {
                                    Index = "index.gmi",
                                    AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/",
                                },
                                new Location()
                                {
                                    AbsoluteRootPath = $"/srv/gemini/{Environment.MachineName}/files/",
                                    DirectoryListing = true,
                                    AllowFileUploads = true,

                                    AllowedMimeTypes = new()
                                    {
                                        ["text/*"] = new MimeConfig
                                        {
                                            MaxSizeBytes = 1024 * 1024 * 1,
                                        },
                                        ["image/*"] = new MimeConfig{},
                                        ["audio/mpeg"] = new MimeConfig{},
                                        ["audio/ogg"] = new MimeConfig{},
                                    }
                                },
                            }
                    }
                }

            };
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
                IncludeFields = true,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
            });
            return json;
        }
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
                if(uri.AbsolutePath.StartsWith("/cgi/"))
                    return Locations.Where(x => x.CGI).First();
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