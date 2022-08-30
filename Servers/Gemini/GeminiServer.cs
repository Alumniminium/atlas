using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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
            };
            TlsOptions.ServerCertificateSelectionCallback += (_, host) =>
            {
                if (Program.Config.Capsules.TryGetValue(host, out var capsule))
                    return X509Certificate.CreateFromCertFile(capsule.AbsoluteTlsCertPath);
                return null;
            };
            TlsOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;


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

                if (ctx.Uri.Scheme == "titan")
                    response = await ProcessUploadRequest(ctx).ConfigureAwait(false);
                else
                    response = await ProcessGetRequest(ctx).ConfigureAwait(false);

                if (!Program.Config.SlowMode || response.MimeType != "text/gemini")
                    await ctx.Stream.WriteAsync(response);
                else
                {
                    var lines = Encoding.UTF8.GetString(response.Bytes).Split('\n');;

                    for(int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i] + '\n';
                        if(i == 0) 
                        {
                            ctx.Stream.Write(Encoding.UTF8.GetBytes(line));
                            ctx.Stream.Flush();
                            continue;
                        }
                        else if (line.StartsWith("#") || line.StartsWith("=>"))
                        {
                            var bytes = Encoding.UTF8.GetBytes(line);
                            foreach(var b in bytes)
                            {
                                ctx.Stream.WriteByte(b);
                                ctx.Stream.Flush();
                                await Task.Delay(16);
                            }
                            ctx.Stream.Write(Encoding.UTF8.GetBytes("\n"));
                            ctx.Stream.Flush();
                        }
                        else if(line.Length > 100)
                        {
                            var words = line.Split(' ');
                            foreach(var word in words)
                            {
                                ctx.Stream.Write(Encoding.UTF8.GetBytes(word + ' '));
                                ctx.Stream.Flush();
                                await Task.Delay(8);
                            }
                        }
                        else
                        {
                            ctx.Stream.Write(Encoding.UTF8.GetBytes(line));
                            ctx.Stream.Flush();
                        }
                        await Task.Delay(16);
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
                await tlsStream.AuthenticateAsServerAsync(TlsOptions).ConfigureAwait(false);
                Program.Config.Capsules.TryGetValue(tlsStream.TargetHostName, out ctx.Capsule);

                ctx.CertAlgo = tlsStream.CipherAlgorithm;
                ctx.CertKx = tlsStream.KeyExchangeAlgorithm;

                if (ctx.ClientCert != null)
                    Console.WriteLine($"Client Cert: {ctx.ClientIdentity}, Hash: {ctx.ClientIdentityHash} ");
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