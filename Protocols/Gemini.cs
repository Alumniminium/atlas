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

        public static async ValueTask ReceiveHeader(GeminiCtx ctx)
        {
            var reqBuffer = new byte[MAX_URI_LENGTH_GEMINI + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.Stream.ReadAsync(reqBuffer.AsMemory(length, 1)) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if (!ctx.Request.EndsWith("\r\n"))
                    continue;
                ctx.RequestPath = string.Join(' ', ctx.Request[..^2]);
                break;
            }

            if (ctx.Uri.Scheme == "titan")
                ctx.IsUpload = true;
        }


        public static async ValueTask HandleRequest(GeminiCtx ctx)
        {
            var location = ctx.Capsule.GetLocation(ctx.Uri);
            if (location == null)
                return;

            var file = Path.GetFileName(ctx.RequestPath);
            
            if(string.IsNullOrEmpty(file))
            {
                ctx.DirectoryListing = location.DirectoryListing;
                if (ctx.DirectoryListing)
                {
                    var gmi = Util.CreateDirectoryListing(ctx, location);
                    await ctx.Success(Encoding.UTF8.GetBytes(gmi));
                    return;
                }
            }

            ctx.RequestPath = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));
            
            if (File.Exists(ctx.RequestPath))
            {
                try
                {
                    var ext = Path.GetExtension(ctx.RequestPath);
                    var mimeType = Util.GetMimeType(ext);
                    var data = await File.ReadAllBytesAsync(ctx.RequestPath);

                    await ctx.Success(data, mimeType);
                }
                catch (Exception e)
                {
                    await ctx.ServerError(e);
                    Console.WriteLine(e);
                }
            }
            else
                await ctx.NotFound();
        }
    }
}