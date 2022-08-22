using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace atlas
{
    public class GeminiCtx
    {
        public Socket Socket { get; set; }
        public SslStream SslStream { get; set; }
        public Capsule Capsule { get; set; }
        public string FilePath { get; internal set; }
        public Uri Uri { get; internal set; }
    }
    class Program
    {
        private const int MAX_URI_LENGTH_GEMINI = 1024;
        public static Configuration Config { get; set; }
        public static Dictionary<string, string> ExtensionToMimeType = new();
        public static X509Certificate Certificate { get; set; }

        static async Task Main(string[] args)
        {
            LoadMimeMap();
            LoadConfig();
            Console.WriteLine("Atlas ready!");
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, Config.Port));
            socket.Listen();

            while (true)
            {
                var client = await socket.AcceptAsync();
                var (success, ctx) = await HandShake(client);
                try
                {
                    if (!success)
                        continue;

                    var reqFileExists = await ReceiveData(ctx);
                    await Respond(ctx, reqFileExists);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    CloseConnection(ctx);
                }
            }
        }

        private static void LoadConfig()
        {
            if (File.Exists("config.json"))
                Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("config.json"));
            else if (File.Exists("/etc/atlas/config.json"))
                Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText("/etc/atlas/config.json"));
            if (Config == null)
            {
                Console.WriteLine("Failed to load configuration. Does config.json exist?");
                Console.WriteLine($"Looking @ '/etc/atlas/config.json'");
                Console.WriteLine($"Looking @ '{Environment.CurrentDirectory}/config.json'");
                Console.WriteLine($"");
                Console.WriteLine($"--- Creating Default Configuration ---");
                Console.WriteLine($"");
                Config = new Configuration()
                {
                    Port = 1965,
                    Capsules = new()
                    {
                        [Environment.MachineName] = new Capsule()
                        {
                            Hostname = Environment.MachineName,
                            TlsCertPath = $"{Environment.MachineName}.pfx",
                            Root = $"/srv/gemini/{Environment.MachineName}/",
                            Locations = new()
                            {
                                new Location()
                                {
                                    Root = $"/srv/gemini/{Environment.MachineName}/",
                                    DirectoryListing = false
                                },
                                new Location()
                                {
                                    Root = $"/srv/gemini/{Environment.MachineName}/files/",
                                    DirectoryListing = true
                                },
                            }
                        }
                    }

                };
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions() { WriteIndented = true });
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
        private static async ValueTask<(bool, GeminiCtx)> HandShake(Socket client)
        {
            var netStream = new NetworkStream(client);

            var context = new GeminiCtx()
            {
                Socket = client,
                SslStream = new SslStream(netStream, false)
            };

            try
            {
                Console.WriteLine(context.SslStream.TargetHostName);
                var options = new SslServerAuthenticationOptions();
                options.ServerCertificateSelectionCallback += SelectCertificate;
                options.EnabledSslProtocols = SslProtocols.Tls13;
                options.RemoteCertificateValidationCallback += ValidateCertificate;

                await context.SslStream.AuthenticateAsServerAsync(options);

                if (Config.Capsules.TryGetValue(context.SslStream.TargetHostName, out var capsule))
                    context.Capsule = capsule;
            }
            catch
            {
                Console.WriteLine($"{client.RemoteEndPoint} -> TLS HandShake aborted.");
            }
            return (context.Capsule!=null, context);
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

        private static X509Certificate SelectCertificate(object sender, string hostName)
        {
            if (Config.Capsules.TryGetValue(hostName, out var capsule))
                return X509Cemy gemini server now supports vhostsrtificate.CreateFromCertFile(Path.Combine(capsule.Root, capsule.TlsCertPath));
            return null;
        }
        private static async ValueTask<bool> ReceiveData(GeminiCtx context)
        {
            var buffer = new byte[MAX_URI_LENGTH_GEMINI + 2]; // +2 for \r\n
            var length = await context.SslStream.ReadAsync(buffer);
            var request = Encoding.UTF8.GetString(buffer, 0, length);

            context.Uri = new Uri(request);
            context.FilePath = Path.Combine(context.Capsule.Root, context.Uri.AbsolutePath[1..]); // remove leading slash

            if (context.FilePath == context.Capsule.Root)
                context.FilePath += "index.gmi";

            var fileExists = File.Exists(context.FilePath);

            Console.WriteLine($"{context.Socket.RemoteEndPoint} -> {context.FilePath} -> {(fileExists ? "found" : "not found")}");

            return fileExists;
        }
        public static async ValueTask Respond(GeminiCtx context, bool reqFileExists)
        {
            var isDirectory = context.FilePath.EndsWith("/");

            if (reqFileExists)
            {
                try
                {
                    var data = await File.ReadAllBytesAsync(context.FilePath);
                    var ext = Path.GetExtension(context.FilePath);
                    var mimeType = ExtensionToMimeType.Where(x => x.Key == ext).Select(x => x.Value).FirstOrDefault("text/gemini");

                    await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.Success} {mimeType}; charset=utf-8\r\n"));
                    await context.SslStream.WriteAsync(data);
                }
                catch (Exception e)
                {
                    await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.FailurePerm} {e.Message}\r\n"));
                    Console.WriteLine(e);
                }
            }
            else if (isDirectory)
            {
                var gmi = CreateDirectoryListing(context);
                await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.Success} text/gemini; charset=utf-8\r\n"));
                await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{gmi}\r\n"));
            }
            else
                await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.NotFound} Atlas: '{context.FilePath}' not found.\r\n"));
        }

        private static string CreateDirectoryListing(GeminiCtx context)
        {
            var sb = new StringBuilder();
            foreach (var file in Directory.GetFiles(context.FilePath))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> gemini://{context.Capsule.Hostname}/{context.FilePath.Replace(context.Capsule.Root, "")}/{Path.GetFileName(file)} {fi.CreationTimeUtc} | {fi.Length / 1024}kb | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        private static void CloseConnection(GeminiCtx context)
        {
            // context.SslStream.Write(Encoding.UTF8.GetBytes($"{Environment.NewLine}=> gemini://atlas.her.st/ {context.Capsule.Hostname} is powered by Atlas"));
            context.SslStream.Flush();
            context.SslStream.Close();
            context.Socket.Close();
        }
    }
}