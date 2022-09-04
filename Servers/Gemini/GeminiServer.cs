using System;
using System.Collections.Generic;
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
                    return Program.Config.Capsules.TryGetValue(host, out var capsule)
                        ? X509Certificate.CreateFromCertFile(capsule.AbsoluteTlsCertPath)
                        : null;
                }
            };

            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Config.GeminiPort));
            Socket.Listen();
        }

        public async void Start()
        {
            var tasks = new List<Task>();
            while (true)
            {
                Console.WriteLine("[GEMINI] Waiting for connection...");
                var socket = await Socket.AcceptAsync().ConfigureAwait(false);

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
            Response response;

            try
            {
                var success = await HandShake(ctx).ConfigureAwait(false);
                if (!success)
                    return;

                await ReceiveRequest(ctx).ConfigureAwait(false);

                if (!Uri.IsWellFormedUriString(ctx.Request, UriKind.Absolute))
                {
                    await ctx.Stream.WriteAsync(Response.BadRequest("invalid request"));
                    return;
                }

                ctx.Uri = new Uri(ctx.Request);

                response = ctx.Uri.Scheme == "titan"
                    ? await ProcessUploadRequest(ctx).ConfigureAwait(false)
                    : await ProcessGetRequest(ctx).ConfigureAwait(false);

                if (!Program.Config.SlowMode || response.MimeType != "text/gemini")
                    await ctx.Stream.WriteAsync(response);
                else
                {
                    var lines = Encoding.UTF8.GetString(response.Bytes).Split('\n'); ;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i] + '\n';
                        if (i == 0)
                        {
                            ctx.Stream.Write(Encoding.UTF8.GetBytes(line));
                            ctx.Stream.Flush();
                            continue;
                        }
                        else if (line.StartsWith("#"))
                        {
                            var bytes = Encoding.UTF8.GetBytes(line);
                            foreach (var b in bytes)
                            {
                                ctx.Stream.WriteByte(b);
                                ctx.Stream.Flush();
                                await Task.Delay(8);
                            }
                            ctx.Stream.Write(Encoding.UTF8.GetBytes("\n"));
                            ctx.Stream.Flush();
                        }
                        else if (line.Length > 200 && !line.StartsWith("=>"))
                        {
                            var words = line.Split(' ');
                            foreach (var word in words)
                            {
                                ctx.Stream.Write(Encoding.UTF8.GetBytes(word + ' '));
                                ctx.Stream.Flush();
                                await Task.Delay(8);
                            }
                        }
                        else
                        {
                            await Task.Delay(16);
                            ctx.Stream.Write(Encoding.UTF8.GetBytes(line));
                            ctx.Stream.Flush();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally { CloseConnection(ctx); }
        }

        public async ValueTask<bool> HandShake(GeminiCtx ctx)
        {
            try
            {
                var tlsStream = (SslStream)ctx.Stream;
                ctx.Cert = new ClientCert();

                TlsOptions.RemoteCertificateValidationCallback = (_, shittyCert, chain, error) =>
                {
                    if (shittyCert == null)
                    {
                        Console.WriteLine("No certificate");
                        return true;
                    }
                    Console.WriteLine("Chain: " + string.Join(' ', chain.ChainStatus.Select(x => x.Status)));
                    ctx.Cert.SelfSignedCert = chain.ChainStatus.Any(x => x.Status == X509ChainStatusFlags.UntrustedRoot);

                    var cert = new X509Certificate2(shittyCert);
                    if (DateTime.Now < cert.NotBefore)
                        return false;
                    return DateTime.Now <= cert.NotAfter;
                };

                await tlsStream.AuthenticateAsServerAsync(TlsOptions).ConfigureAwait(false);
                Program.Config.Capsules.TryGetValue(tlsStream.TargetHostName, out ctx.Capsule);

                if (tlsStream.RemoteCertificate != null)
                {
                    ctx.Cert.SetCert(tlsStream.RemoteCertificate);
                    Console.WriteLine($"Client Cert: {ctx.Cert.Subject}, Hash: {ctx.Cert.Thumbprint} ");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{ctx.Socket.RemoteEndPoint} -> TLS HandShake aborted.");
                Console.WriteLine(e);
            }
            return ctx.Capsule != null;
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
            var size = int.Parse(strSizeBytes);

            return await UploadFile(ctx, path, pathUri, mimeType, size).ConfigureAwait(false);
        }
    }
}