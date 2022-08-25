using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using atlas.Contexts;

namespace atlas.Protocols
{
    internal class Gemini
    {
        public const int MAX_URI_LENGTH_GEMINI = 1024;

        public static async ValueTask<bool> HandShake(GeminiCtx ctx)
        {
            try
            {
                var tlsStream = (SslStream)ctx.Stream;
                await tlsStream.AuthenticateAsServerAsync(Server.TlsOptions);
                Server.Config.Capsules.TryGetValue(tlsStream.TargetHostName, out ctx.Capsule);

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
    }
}