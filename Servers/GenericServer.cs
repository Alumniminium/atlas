using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using atlas.Data;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public class GenericServer
    {
        public static async ValueTask<Response> ProcessRequest(Context ctx)
        {
            Statistics.AddRequest(ctx);

            if (ctx.Request.EndsWith("/atlas.stats"))
                return Statistics.Get();

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
                if (location.DirectoryListing)
                    return ServeDirectoryListing(ctx, location);

                Program.Log(ctx, $"Adding {location.Index} to request");
                ctx.Request = Path.Combine(ctx.Request, location.Index);
                ctx.Uri = new Uri(ctx.Request);
            }

            if (location.CGI)
                return await ServeCGI(ctx, location).ConfigureAwait(false);

            ctx.Request = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));
            if (ctx.Request == ctx.Capsule.AbsoluteTlsCertPath)
            {
                Program.Log(ctx, "Requested TLS Certificate");
                return Response.NotFound("nice try");
            }

            if (!File.Exists(ctx.Request))
            {
                var msg = $"Not Found: {ctx.Request}";
                Program.Log(ctx, msg);
                return Response.NotFound(msg, !ctx.IsGemini);
            }

            return await ServeFile(ctx, location).ConfigureAwait(false);
        }

        private static Response Proxy(Context ctx) => Response.ProxyDenied();

        public static async ValueTask<Response> ProcessFileUpload(Context ctx, string path, Uri pathUri, string mimeType, int size)
        {
            var location = ctx.Capsule.GetLocation(pathUri);

            var validation = ValidateUpload(ctx, location, path, mimeType, size);
            if (validation != null)
                return validation;

            var data = await ReceivePayload(ctx, size).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, data.ToArray());
            return Response.Redirect($"{ctx.Capsule.FQDN}{Path.GetDirectoryName(pathUri.AbsolutePath)}/", !ctx.IsGemini);
        }

        private static async Task<Memory<byte>> ReceivePayload(Context ctx, int size)
        {
            Program.Log(ctx, $"receiving {size / 1024f:0.00}kb payload");
            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
            {
                fileLen += await ctx.Socket.ReceiveAsync(data.AsMemory(fileLen, size - fileLen)).ConfigureAwait(false);
                Program.Log(ctx, $"received {fileLen}/{size}");
            }
            return data;
        }

        private static Response ServeDirectoryListing(Context ctx, Location location)
        {
            Program.Log(ctx, "Creating directory listing");
            var gmi = Util.CreateDirectoryListing(ctx, location);
            Program.Log(ctx, $"DirectoryListing -> {gmi.Length} bytes");
            return Response.Ok(Encoding.UTF8.GetBytes(gmi).AsMemory(), "text/gemini", !ctx.IsGemini);
        }

        private static async ValueTask<Response> ServeCGI(Context ctx, Location location)
        {
            Program.Log(ctx, "Invoking CGI");

            var cgiParts = ctx.Uri.AbsolutePath.Replace("/cgi/", "").Split('/');
            var file = cgiParts[0];
            var pathInfo = cgiParts.Length > 1 ? string.Join('/', cgiParts[1..]) : "/";

            var isFirstLine = true;
            foreach (var line in CGI.ExecuteScript(ctx, file, location.AbsoluteRootPath, pathInfo))
            {
                var lineEnding = isFirstLine ? "\r\n" : "\n";
                var output = line.EndsWith("\r\n") ? line : line + lineEnding;
                ctx.Writer.Write(Encoding.UTF8.GetBytes(output));
                isFirstLine = false;
            }

            return new("", ctx.IsSpartan);
        }

        private static async ValueTask<Response> ServeFile(Context ctx, Location location)
        {
            var ext = Path.GetExtension(ctx.Request);
            var mimeType = MimeMap.GetMimeType(ext, location.DefaultMimeType);
            var data = await File.ReadAllBytesAsync(ctx.Request).ConfigureAwait(false);
            var time = DateTime.UtcNow - ctx.RequestStart;

            if (mimeType.StartsWith("text/"))
                data = Encoding.UTF8.GetBytes(Util.ReplaceTokens(Encoding.UTF8.GetString(data), ctx));

            Program.Log(ctx, $"{data.Length / 1024f:0.00}kb of {mimeType} - in {time.TotalMilliseconds:0.00}ms");
            return Response.Ok(data.AsMemory(), mimeType, !ctx.IsGemini);
        }

        private static Response ValidateUpload(Context ctx, Location location, string path, string mimeType, int size)
        {
            if (string.IsNullOrEmpty(path))
            {
                var msg = $"{ctx.Request} missing location or path";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            if (!location.AllowFileUploads)
            {
                var msg = "Uploads not allowed here";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            if (size > ctx.Capsule.MaxUploadSize)
            {
                var msg = $"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            if (!location.IsAllowedMimeType(mimeType))
            {
                var msg = $"{mimeType} not allowed at {location.AbsoluteRootPath}";
                Program.Log(ctx, msg);
                return Response.BadRequest(msg, !ctx.IsGemini);
            }

            return null;
        }
    }
}