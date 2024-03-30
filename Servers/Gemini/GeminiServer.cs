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
    public class GeminiServer
    {
        public Socket Socket { get; set; }
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

            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Cfg.GeminiPort));
            Socket.Listen();
        }

        public async void Start()
        {
            while (true)
            {
                Console.WriteLine("[Gemini] Waiting for connection...");
                var socket = await Socket.AcceptAsync().ConfigureAwait(false);
                socket.NoDelay = true;
                var task = Task.Run(async () =>
                {
                    var ctx = new GeminiCtx(socket);
                    if (await HandShake(ctx).ConfigureAwait(false))
                    {
                        ctx.Reader = new StreamReader(ctx.SslStream);
                        ctx.Writer = new BinaryWriter(ctx.SslStream);
                        await ProcessSocket(ctx).ConfigureAwait(false);
                    }
                    CloseConnection(ctx);
                });
            }
        }

        public async ValueTask<bool> HandShake(GeminiCtx ctx)
        {
            try
            {
                var tlsStream = ctx.SslStream;
                TlsOptions.RemoteCertificateValidationCallback = (_, shittyCert, chain, error) =>
                {
                    if (shittyCert == null)
                        return true;

                    ctx.IsSelfSignedCert = chain.ChainStatus.Any(x => x.Status == X509ChainStatusFlags.UntrustedRoot);

                    var cert = new X509Certificate2(shittyCert);
                    ctx.Certificate = cert;
                    return true;
                };

                await tlsStream.AuthenticateAsServerAsync(TlsOptions).ConfigureAwait(false);

                if (!Program.Cfg.Capsules.TryGetValue(tlsStream.TargetHostName, out ctx.Capsule))
                {
                    ctx.Capsule = new Data.Capsule() { FQDN = tlsStream.TargetHostName };
                    Program.Log(ctx, $"'{tlsStream.TargetHostName}' not configured.");
                    await ctx.SslStream.WriteAsync(Response.ProxyDenied());
                    return false;
                }

                if (ctx.Certificate != null)
                {
                    Program.Log(ctx, $"Client Cert: {ctx.CertSubject}, Hash: {ctx.CertThumbprint}");

                    if (!ctx.IsValidCert)
                    {
                        await ctx.SslStream.WriteAsync(Response.CertExpired().Data);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Program.Log(ctx, "TLS HandShake aborted. Reason: " + e.Message);
                return false;
            }
            return true;
        }

        private static async ValueTask ProcessSocket(GeminiCtx ctx)
        {
            try
            {
                Program.Log(ctx, "Receiving Request...");
                var header = await ctx.Reader.ReadLineAsync();

                ctx.Request = header;
                ctx.Request = ctx.Request.Replace($":{Program.Cfg.GeminiPort}", "");
                Program.Log(ctx, "Received!");

                if (!Uri.IsWellFormedUriString(ctx.Request, UriKind.Absolute))
                {
                    Program.Log(ctx, $"Uri Invalid ({ctx.Request})");
                    await ctx.SslStream.WriteAsync(Response.BadRequest("invalid request"));
                    return;
                }

                ctx.Uri = new Uri(ctx.Request);
                var response = default(Response);

                switch (ctx.Uri.Scheme)
                {
                    case "gemini":
                        response = await GenericServer.ProcessRequest(ctx).ConfigureAwait(false);
                        break;
                    case "titan":
                        response = await UploadProcessor.Process(ctx).ConfigureAwait(false);
                        break;
                }

                Statistics.AddResponse(response);
                if (Program.Cfg.SlowMode && response.MimeType == "text/gemini")
                    await AnimatedResponse(ctx, response);
                else
                    await ctx.SslStream.WriteAsync(response);
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { CloseConnection(ctx); }
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
                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes(line));
                    continue;
                }
                else if (line.StartsWith("#") && timeCount < maxTime)
                {
                    var bytes = Encoding.UTF8.GetBytes(line);
                    foreach (var b in bytes)
                    {
                        await ctx.SslStream.WriteAsync(new []{b});
                        timeCount += 16;
                        await Task.Delay(16);
                    }
                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes("\n"));
                    continue;
                }

                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes(line));
                timeCount += 16;

                if (timeCount < maxTime)
                    await Task.Delay(16);
            }
        }

        public static void CloseConnection(Context ctx)
        {
            Program.Log(ctx, "complete");
            ctx?.Socket?.Close();
            ctx?.Reader?.Dispose();
            ctx?.Socket?.Dispose();
        }
    }
}