using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using atlas.Data;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public class DownloadProcessor
    {
        public static async ValueTask<Response> Process(Context ctx)
        {
            Statistics.AddRequest(ctx);

            if (ctx.Request.Contains(".."))
            {
                var msg = "invalid request (..)";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            if (ctx.Uri.Host != ctx.Capsule.FQDN)
                return Proxy(ctx);

            var location = ctx.Capsule.GetLocation(ctx.Uri);

            if (location.RequireClientCert)
            {
                if (ctx is not GeminiCtx gctx)
                {
                    var msg = "this location requires gemini";
                    Program.Log(ctx, msg);
                    return Response.BadRequest(msg, !ctx.IsGemini);
                }
                if (gctx.Certificate == null)
                {
                    var msg = "this location requires a client certificate";
                    Program.Log(ctx, msg);
                    return Response.CertRequired();
                }
            }

            var fileName = Path.GetFileName(ctx.Uri.AbsolutePath);
            if (string.IsNullOrEmpty(fileName))
            {
                Program.Log(ctx, $"No filename for request");
                if (location.DirectoryListing)
                {
                    Program.Log(ctx, $"Create DirectoryListing");
                    var gmi = CreateDirectoryListing(ctx, location);
                    Program.Log(ctx, $"DirectoryListing -> {gmi.Length} bytes");
                    return Response.Ok(Encoding.UTF8.GetBytes(gmi).AsMemory(), "text/gemini", !ctx.IsGemini);
                }
                else
                {
                    Program.Log(ctx, $"Adding {location.Index} to request");
                    ctx.Request = Path.Combine(ctx.Request, location.Index);
                    ctx.Uri = new Uri(ctx.Request);
                }
            }

            if (location.CGI)
            {
                Program.Log(ctx, "Invoking CGI");

                var cgiParts = ctx.Uri.AbsolutePath.Replace("/cgi/", "").Split('/');
                var file = cgiParts[0];
                var PATH_INFO = cgiParts.Length > 1 ? string.Join('/', cgiParts[1..]) : "/";

                var counter = 0;
                foreach (var line in CGI.ExecuteScript(ctx, file, location.AbsoluteRootPath, PATH_INFO))
                {
                    var l = line;

                    if (!l.EndsWith("\r\n"))
                    {
                        if (counter == 0)
                            l += "\r\n";
                        else
                            l += '\n';
                    }
                    ctx.Writer.Write(Encoding.UTF8.GetBytes(l));
                    counter++;
                }

                return new("", ctx.IsSpartan);
            }

            ctx.Request = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));
            if (ctx.Request == ctx.Capsule.AbsoluteTlsCertPath)
            {
                Program.Log(ctx, "Requested TLS Certificate");
                return Response.NotFound("nice try");
            }

            if (ctx.Request.EndsWith("/atlas.stats"))
                return Statistics.Get();

            if (!File.Exists(ctx.Request))
            {
                var msg = $"Not Found: {ctx.Request}";
                Program.Log(ctx, msg);
                return Response.NotFound(msg, !ctx.IsGemini);
            }

            var ext = Path.GetExtension(ctx.Request);
            var mimeType = MimeMap.GetMimeType(ext, location.DefaultMimeType);
            var data = await File.ReadAllBytesAsync(ctx.Request).ConfigureAwait(false);

            Program.Log(ctx, $"{data.Length / 1024f:0.00}kb of {mimeType}");
            return Response.Ok(data.AsMemory(), mimeType, !ctx.IsGemini);
        }

        private static Response Proxy(Context ctx)
        {
            return Response.ProxyDenied();
            // var client = new TcpClient(ctx.Uri.Host, 1965);
            // var stream = client.GetStream();
            // var tlsStream = new SslStream(stream);
            // var options = new SslClientAuthenticationOptions();
            // options.RemoteCertificateValidationCallback = (_,_,_,_) => true;
            // options.AllowRenegotiation=true;
            // options.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13 | System.Security.Authentication.SslProtocols.Tls12;
            // options.EncryptionPolicy = EncryptionPolicy.AllowNoEncryption;
            // 
            // tlsStream.AuthenticateAsClient(options);
            // tlsStream.Write(Encoding.UTF8.GetBytes(ctx.Uri.AbsoluteUri + "\r\n"));
            // tlsStream.Flush();
            // var buffer = new byte[10240];
            // var idx = 0;
            // while (tlsStream.CanRead)
            // {
            // var read = tlsStream.Read(buffer, idx, buffer.Length-idx);
            // idx += read;
            // if(read == 0) 
            // break;
            // }
            // var payload = string.Join('\n', Encoding.UTF8.GetString(buffer).Split("\r\n")[1..]);
            // payload = "### proxied by her.st atlas\n" + payload;
            // return Response.Ok(Encoding.UTF8.GetBytes(payload).AsMemory());
        }

        public static async ValueTask<Response> UploadFile(Context ctx, string path, Uri pathUri, string mimeType, int size)
        {
            var location = ctx.Capsule.GetLocation(pathUri);

            if (string.IsNullOrEmpty(path))
            {
                var msg = $"{ctx.Request} missing location or path";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }
            if (ctx.Capsule.MaxUploadSize <= size || location.MaxUploadSize <= size)
            {
                var msg = $"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            var isAllowedType = location.AllowedMimeTypes.Any(x => x.Key.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.Key.ToLowerInvariant().Split('/')[1] == "*" && mimeType.Split('/')[0] == x.Key.ToLowerInvariant().Split('/')[0]));

            if (!isAllowedType)
            {
                var msg = $"{mimeType} not allowed at {location.AbsoluteRootPath}";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            var data = await ReceivePayload(ctx, size).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, data.ToArray());
            return Response.Redirect($"{ctx.Capsule.FQDN}{Path.GetDirectoryName(pathUri.AbsolutePath)}/", !ctx.IsGemini);
        }

        private static async Task<Memory<byte>> ReceivePayload(Context ctx, int size)
        {
            Program.Log(ctx, $"receiving {size / 1024f:0.00}kb payload");
            var data = new Memory<byte>();
            var fileLen = 0;
            while (fileLen != size)
            {
                fileLen += await ctx.Socket.ReceiveAsync(data[fileLen..size]).ConfigureAwait(false);
                Program.Log(ctx, $"received {fileLen}/{size}");
            }
            return data;
        }

        public static string CreateDirectoryListing(Context ctx, Location loc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### LAST  MODIFIED   |  SIZE  | NAME");

            foreach (var file in Directory.GetFiles(loc.AbsoluteRootPath))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> {ctx.Uri.Scheme}://{ctx.Capsule.FQDN}/{loc.AbsoluteRootPath.Replace(ctx.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {Util.CenterString(fi.CreationTimeUtc.ToString("yyyy-MM-dd"), 12)} | {Util.CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }
    }
}