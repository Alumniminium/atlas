using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace atlas.Data
{
    public class Configuration
    {
        public bool SlowMode { get; set; }
        public int SlowModeMaxMilliSeconds { get; set; }
        public ushort SpartanPort { get; set; } = 300;
        public ushort GeminiPort { get; set; } = 1965;
        public Dictionary<string, Capsule> Capsules { get; set; } = new();

        public static Configuration Load()
        {
            var conf = new Configuration();
            var configPath = "/etc/atlas/config.json";

            if (File.Exists(configPath))
            {
                Console.WriteLine($"Loading /etc/atlas/config.json ...");
                conf = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("/etc/atlas/config.json"));
            }
            else if (File.Exists("config.json"))
            {
                configPath = $"{Environment.CurrentDirectory}/config.json";
                Console.WriteLine($"Loading {configPath} ...");
                conf = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"));
            }

            if (conf == null)
            {
                Console.WriteLine("Failed to load configuration. Does config.json exist?");
                Console.WriteLine($"Looking @ '/etc/atlas/config.json'");
                Console.WriteLine($"Looking @ '{Environment.CurrentDirectory}/config.json'");
                Console.WriteLine($"");
                Console.WriteLine($"--- Creating Default Configuration ---");
                Console.WriteLine($"");
                Console.WriteLine(CreateSampleConfig());
                Console.WriteLine($"");
                Console.WriteLine($"--- ^ Ready for Copy/Paste/Edit ^ ---");
                Environment.Exit(0);
            }
            else
            {
                foreach (var vhost in conf.Capsules)
                {
                    if (!string.IsNullOrWhiteSpace(vhost.Value.AbsoluteTlsCertPath) && File.Exists(vhost.Value.AbsoluteTlsCertPath))
                        continue;

                    var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    var req = new CertificateRequest("cn=" + vhost.Value.FQDN, ecdsa, HashAlgorithmName.SHA256);
                    req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: false));
                    req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, false));

                    var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1000));

                    vhost.Value.AbsoluteTlsCertPath = Path.Combine(vhost.Value.AbsoluteRootPath, vhost.Value.FQDN + ".pfx");

                    Console.WriteLine($"Certificate for {vhost.Value.FQDN}not found. Creating new one at {vhost.Value.AbsoluteTlsCertPath}");

                    File.WriteAllBytes(vhost.Value.AbsoluteTlsCertPath, cert.Export(X509ContentType.Pfx));

                    Console.WriteLine($"Updating {configPath} with certificate...");

                    var json = JsonSerializer.Serialize(conf, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault });
                    File.WriteAllText(configPath, json);
                    Console.WriteLine($"Updated {configPath}");
                }
            }
            return conf;
        }

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
                                        ["text/*"] = new MimeConfig(1024 * 1024 * 1),
                                        ["image/*"] = new MimeConfig(default),
                                        ["audio/mpeg"] = new MimeConfig(default),
                                        ["audio/ogg"] = new MimeConfig(default),
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