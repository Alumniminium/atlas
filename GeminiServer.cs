using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using atlas.Contexts;

namespace atlas
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
            while (true)
            {
                Console.WriteLine("[GEMINI] Waiting for connection...");
                var clientSocket = await Socket.AcceptAsync().ConfigureAwait(false); ;

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
                        continue;
                        
                    await ReceiveRequest(ctx).ConfigureAwait(false);

                    if (ctx.Uri.Scheme == "titan")
                        response = await ProcessUploadRequest(ctx).ConfigureAwait(false);
                    else
                        response = await ProcessGetRequest(ctx).ConfigureAwait(false);

                    await ctx.Stream.WriteAsync(response);
                }
                catch (Exception e) { Console.WriteLine(e); }
                finally { CloseConnection(ctx); }
            }
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
        public async ValueTask<Response> ProcessUploadRequest(GeminiCtx ctx)
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
        public override Response NotFound(string message) => new($"{(int)GeminiStatusCode.FailurePerm} {message}.\r\n");
        public override Response BadRequest(string reason) => new($"{(int)GeminiStatusCode.BadRequest} {reason}\r\n");
        public override Response Redirect(string target) => new($"{(int)GeminiStatusCode.RedirectTemp} {target}\r\n");
        public override Response Ok(byte[] data, string mimeType = "text/gemini") => new(mimeType, data);
    }
}