using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace atlas.Servers.Gemini
{
    public class GeminiServer : GenericServer
    {
        public SslServerAuthenticationOptions TlsOptions { get; set; }

        public GeminiServer()
        {
            TlsOptions = new SslServerAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls13,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                ClientCertificateRequired = true,
                CertificateChainPolicy = new X509ChainPolicy
                {
                    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority,
                },
                ServerCertificateSelectionCallback = (_, host) =>
                {
                    return Program.Cfg.Capsules.TryGetValue(host, out var capsule)
                        ? X509Certificate.CreateFromCertFile(capsule.AbsoluteTlsCertPath)
                        : null;
                }
            };

            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Cfg.GeminiPort));
            Socket.Listen();
        }

        public async void Start()
        {
            while (true)
            {
                Console.WriteLine("[GEMINI] Waiting for connection...");
                var socket = await Socket.AcceptAsync().ConfigureAwait(false);
                socket.NoDelay=true;
                var task = Task.Run(async () => await ProcessSocket(socket).ConfigureAwait(false));
            }
        }

        private async ValueTask ProcessSocket(Socket clientSocket)
        {
            var ctx = new GeminiCtx()
            {
                Socket = clientSocket,
                Stream = new SslStream(new NetworkStream(clientSocket), false)
            };
            Response response = default;

            try
            {
                var success = await HandShake(ctx).ConfigureAwait(false);
                if (!success)
                    return;

                await ReceiveRequest(ctx).ConfigureAwait(false);

                if (!Uri.IsWellFormedUriString(ctx.Request, UriKind.Absolute))
                {
                    await ctx.Stream.WriteAsync(Response.BadRequest("invalid request"));
                    await ctx.Stream.FlushAsync();
                    return;
                }

                ctx.Uri = new Uri(ctx.Request);

                switch(ctx.Uri.Scheme)
                {
                    case "gemini":
                        response = await ProcessGetRequest(ctx).ConfigureAwait(false);
                        break;
                    case "titan":
                        response = await ProcessUploadRequest(ctx).ConfigureAwait(false);
                        break;
                }

                if (Program.Cfg.SlowMode && response.MimeType == "text/gemini")
                    await AnimatedResponse(ctx, response);
                else
                    await ctx.Stream.WriteAsync(response);
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { CloseConnection(ctx); }
        }

        public async ValueTask<bool> HandShake(GeminiCtx ctx)
        {
            try
            {
                var tlsStream = (SslStream)ctx.Stream;

                TlsOptions.RemoteCertificateValidationCallback = (_, shittyCert, chain, error) =>
                {
                    if (shittyCert == null)
                    {
                        Console.WriteLine("No certificate");
                        return true;
                    }
                    Console.WriteLine("Chain: " + string.Join(' ', chain.ChainStatus.Select(x => x.Status)));
                    ctx.IsSelfSignedCert = chain.ChainStatus.Any(x => x.Status == X509ChainStatusFlags.UntrustedRoot);

                    var cert = new X509Certificate2(shittyCert);
                    ctx.Certificate = cert;
                    return true;
                };

                await tlsStream.AuthenticateAsServerAsync(TlsOptions).ConfigureAwait(false);

                if (!Program.Cfg.Capsules.TryGetValue(tlsStream.TargetHostName, out ctx.Capsule))
                {
                    Console.WriteLine($"[FAIL] vhost '{tlsStream.TargetHostName}' not configured.");
                    await ctx.Stream.WriteAsync(Response.ProxyDenied());
                    await ctx.Stream.FlushAsync();
                    return false;
                }

                if (ctx.Certificate != null)
                {
                    Console.WriteLine($"Client Cert: {ctx.CertSubject}, Hash: {ctx.CertThumbprint} ");

                    if (!ctx.IsValidCert)
                    {
                        await ctx.Stream.WriteAsync(Response.CertExpired().Data);
                        await ctx.Stream.FlushAsync();
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{ctx.Socket.RemoteEndPoint} -> TLS HandShake aborted.");
                Console.WriteLine(e);
            }
            return ctx.Capsule != null;
        }
        public override async ValueTask ReceiveRequest(Context ctx)
        {
            Console.WriteLine($"[Gemini] {ctx.IP} -> Receiving Request...");
            await base.ReceiveRequest(ctx);
            ctx.Request = ctx.Request.Replace($":{Program.Cfg.GeminiPort}", "");
            Console.WriteLine($"[Gemini] {ctx.IP} -> {ctx.Request}");
        }

        public static async ValueTask<Response> ProcessUploadRequest(GeminiCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var path = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = "text/gemini";
            var strSizeBytes = "0";

            for (int i = 0; i < titanArgs.Length; i++)
            {
                var arg = titanArgs[i];
                var kvp = arg.Split('=');

                if (kvp[0] == "mime")
                    mimeType = kvp[1];
                if (kvp[0] == "size")
                    strSizeBytes = kvp[1];
                if (kvp[0] == "charset")
                    continue;
            }

            return int.TryParse(strSizeBytes, out var size)
                ? await UploadFile(ctx, path, pathUri, mimeType, size).ConfigureAwait(false)
                : Response.BadRequest("Invalid Size: " + strSizeBytes);
        }
        private static async ValueTask AnimatedResponse(GeminiCtx ctx, Response response)
        {
            var lines = Encoding.UTF8.GetString(response.Data.ToArray()).Split('\n'); ;
            var timeCount = 0;
            var maxTime = Program.Cfg.SlowModeMaxMilliSeconds;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i] + '\n';
                if (i == 0)
                {
                    await ctx.Stream.WriteAsync(Encoding.UTF8.GetBytes(line));
                    await ctx.Stream.FlushAsync();
                    continue;
                }
                else if (line.StartsWith("#") && timeCount < maxTime)
                {
                    var bytes = Encoding.UTF8.GetBytes(line);
                    foreach (var b in bytes)
                    {
                        ctx.Stream.WriteByte(b);
                        await ctx.Stream.FlushAsync();
                        timeCount += 16;
                        await Task.Delay(16);
                    }
                    await ctx.Stream.WriteAsync(Encoding.UTF8.GetBytes("\n"));
                    await ctx.Stream.FlushAsync();
                    continue;
                }
                else if (line.Length > 200 && !line.StartsWith("=>") && timeCount < maxTime)
                {
                    foreach (var word in line.Split(' ').Select(word => Encoding.UTF8.GetBytes(word + ' ')))
                    {
                        await ctx.Stream.WriteAsync(word);
                        await ctx.Stream.FlushAsync();
                        timeCount += 16;
                        await Task.Delay(16);
                    }
                    continue;
                }

                await ctx.Stream.WriteAsync(Encoding.UTF8.GetBytes(line));
                await ctx.Stream.FlushAsync();
                timeCount += 16;

                if (timeCount < maxTime)
                    await Task.Delay(16);
            }
        }
    }
}