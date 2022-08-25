using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using atlas.Contexts;
using atlas.Protocols;

namespace atlas
{
    public static class Server
    {
        public static Socket GeminiSocket { get; set; }
        public static Socket SpartanSocket { get; set; }
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

        public static void Start()
        {
            GeminiSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            GeminiSocket.Bind(new IPEndPoint(IPAddress.Any, Config.Port));
            GeminiSocket.Listen();

            SpartanSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SpartanSocket.Bind(new IPEndPoint(IPAddress.Any, 3000));//Config.SpartanPort
            SpartanSocket.Listen();

            Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine("[GEMINI] Waiting for connection...");
                    var clientSocket = await GeminiSocket.AcceptAsync();

                    var ctx = new GeminiCtx()
                    {
                        Socket = clientSocket,
                        Stream = new SslStream(new NetworkStream(clientSocket), false)
                    };

                    var success = await Gemini.HandShake(ctx);
                    try
                    {
                        if (!success)
                            continue;

                        await ReceiveHeader(ctx);


                        if (ctx.Uri.Scheme == "titan")
                            await Titan.HandleUpload(ctx);
                        else
                            await HandleRequest(ctx);
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
            });
            Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine("[SPARTAN] Waiting for connection...");
                    var clientSocket = await SpartanSocket.AcceptAsync();

                    var ctx = new SpartanCtx()
                    {
                        Socket = clientSocket,
                        Stream = new NetworkStream(clientSocket)
                    };

                    try
                    {
                        await ReceiveHeader(ctx);
                        var parts = ctx.Request.Split(' ');
                        var host = parts[0];
                        var path = parts[1];
                        var size = int.Parse(parts[2]);
                        
                        if (Config.Capsules.TryGetValue(host, out var capsule))
                            ctx.Capsule = capsule;
                        ctx.RequestPath = path;

                        if (size > 0)
                            await Spartan.HandleUpload(ctx);
                        else
                            await HandleRequest(ctx);
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
            });
        }
        public static async ValueTask ReceiveHeader(AtlasCtx ctx)
        {
            var reqBuffer = new byte[ctx.MaxHeaderSize + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.Stream.ReadAsync(reqBuffer.AsMemory(length, 1)) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if (!ctx.Request.EndsWith("\r\n"))
                    continue;

                ctx.RequestPath = string.Join(' ', ctx.Request[..^2]);
                break;
            }
        }
        public static async ValueTask HandleRequest(AtlasCtx ctx)
        {
            var location = ctx.Capsule.GetLocation(ctx.Uri);
            if (location == null)
                return;

            if (string.IsNullOrEmpty(Path.GetFileName(ctx.RequestPath)))
            {
                ctx.DirectoryListing = location.DirectoryListing;
                if (ctx.DirectoryListing)
                {
                    var gmi = Util.CreateDirectoryListing(ctx, location);
                    await ctx.Success(Encoding.UTF8.GetBytes(gmi));
                    return;
                }
                else
                    ctx.RequestPath += location.Index;
            }

            ctx.RequestPath = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));

            if (!File.Exists(ctx.RequestPath))
            {
                await ctx.NotFound();
                return;
            }

            var ext = Path.GetExtension(ctx.RequestPath);
            var mimeType = Util.GetMimeType(ext);
            var data = await File.ReadAllBytesAsync(ctx.RequestPath);
            await ctx.Success(data, mimeType);
        }
        public static void CloseConnection(AtlasCtx ctx)
        {
            ctx.Stream.Flush();
            ctx.Stream.Close();
            ctx.Socket.Close();
            Console.WriteLine("Closed Connection");
        }
    }
}