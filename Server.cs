

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace atlas
{
    public static class Server
    {
        private const int MAX_URI_LENGTH_GEMINI = 1024;
        public static Socket ServerSocket { get; set; }
        public static Configuration Config { get; set; }
        public static SslServerAuthenticationOptions TlsOptions { get; set; }

        static Server()
        {
            TlsOptions = new SslServerAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls13,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                ClientCertificateRequired = true,
            };
            TlsOptions.ServerCertificateSelectionCallback += (object _, string host) =>
            {
                if (Config.Capsules.TryGetValue(host, out var capsule))
                    return X509Certificate.CreateFromCertFile(capsule.AbsoluteTlsCertPath);
                return null;
            };

            TlsOptions.RemoteCertificateValidationCallback += (object _, X509Certificate _, X509Chain _, SslPolicyErrors _) => true; ;
        }

        public static async ValueTask Start()
        {
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, Config.Port));
            ServerSocket.Listen();

            while (true)
            {
                var clientSocket = await ServerSocket.AcceptAsync();
                var (success, ctx) = await HandShake(clientSocket);
                try
                {
                    if (!success)
                        continue;

                    var (reqFileExists, titan) = await ReceiveData(ctx);

                    if(!titan)
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

        private static async ValueTask<(bool, GeminiCtx)> HandShake(Socket client)
        {
            var context = new GeminiCtx()
            {
                Socket = client,
                SslStream = new SslStream(new NetworkStream(client), false)
            };

            try
            {
                await context.SslStream.AuthenticateAsServerAsync(TlsOptions);
                Config.Capsules.TryGetValue(context.SslStream.TargetHostName, out context.Capsule);

                if (context.ClientCert != null)
                    Console.WriteLine($"Client Cert: {context.ClientIdentity}, Hash: {context.ClientIdentityHash} ");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{client.RemoteEndPoint} -> TLS HandShake aborted.");
                Console.WriteLine(e);
            }
            return (context.Capsule != null, context);
        }

        private static async ValueTask<(bool found, bool titan)> ReceiveData(GeminiCtx context)
        {
            var reqBuffer = new byte[MAX_URI_LENGTH_GEMINI + 2]; // +2 for \r\n
            var length = 0;
            var request = string.Empty;
            while(await context.SslStream.ReadAsync(reqBuffer, length, 1) == 1)
            {
                request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if(request.EndsWith("\r\n"))
                    break;
            }

            // var length = await context.SslStream.ReadAsync(reqBuffer);

            context.Uri = new Uri(request);

            switch (context.Uri.Scheme)
            {
                case "gemini":
                    {
                        context.FilePath = Path.Combine(context.Capsule.AbsoluteRootPath, context.Uri.AbsolutePath[1..]); // remove leading slash

                        if (context.FilePath == context.Capsule.AbsoluteRootPath)
                            context.FilePath += "index.gmi";

                        var fileExists = File.Exists(context.FilePath);

                        Console.WriteLine($"{context.Socket.RemoteEndPoint} -> {context.FilePath} -> {(fileExists ? "found" : "not found")}");
                        return (fileExists, false);
                        break;
                    }
                case "titan":
                    {
                        var parts = request.Split(';');
                        var pathUri = new Uri(parts[0]);
                        var path = Path.Combine(context.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
                        var strSize  = parts[2].Split('=')[1].Replace("\r\n","");
                        var size = int.Parse(strSize);
                        
                        var data = new byte[size];
                        var fileLen = 0;
                        while (fileLen != size)
                            fileLen += await context.SslStream.ReadAsync(data,fileLen,size-fileLen);
                        
                        Console.WriteLine("Finished");

                        File.WriteAllBytes(path, data);
                        
                        await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.RedirectTemp} gemini://{Path.GetDirectoryName(pathUri.AbsolutePath)}/\r\n"));
                        return (true,true);
                    }
            }
            return (false,false);
        }
        public static async ValueTask Respond(GeminiCtx context, bool reqFileExists)
        {
            var isDirectory = context.FilePath.EndsWith("/");

            if (reqFileExists)
            {
                try
                {
                    var ext = Path.GetExtension(context.FilePath);
                    if (Program.ExtensionToMimeType.TryGetValue(ext, out var mimeType))
                        await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.Success} {mimeType}; charset=utf-8\r\n"));
                    else
                        await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.Success} text/gemini; charset=utf-8\r\n"));

                    var data = await File.ReadAllBytesAsync(context.FilePath);
                    // if (context.ClientCert != null)
                    //     await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"Hello, {context.ClientIdentity}\r\n"));
                    // else
                    //     await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"Hello, anon!\r\n"));
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
                await context.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)StatusCode.NotFound} Atlas: not found.\r\n"));
        }

        private static string CreateDirectoryListing(GeminiCtx context)
        {
            var sb = new StringBuilder();
            foreach (var file in Directory.GetFiles(context.FilePath))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> gemini://{context.Capsule.FQDN}/{context.FilePath.Replace(context.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {fi.CreationTimeUtc} | {fi.Length / 1024}kb | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        private static void CloseConnection(GeminiCtx context)
        {
            context.SslStream.Flush();
            context.SslStream.Close();
            context.Socket.Close();
        }
    }
}