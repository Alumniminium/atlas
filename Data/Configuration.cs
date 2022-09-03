using System.Diagnostics;
using System.Text.Json;

namespace atlas.Data
{
    public class Configuration
    {
        public ushort SpartanPort { get; set; } = 300;
        public ushort GeminiPort { get; set; } = 1965;
        public Dictionary<string, Capsule> Capsules { get; set; } = new ();
        public bool SlowMode { get; set; }

        public static object CreateSampleConfig()
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
}