using System.Net.Sockets;
using System.Text;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public class GenericServer
    {
        public Socket Socket { get; set; }

        public static async ValueTask ReceiveRequest(Context ctx)
        {
            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> Receiving Request...");
            var reqBuffer = new byte[ctx.MaxHeaderSize + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.Stream.ReadAsync(reqBuffer.AsMemory(length, 1)).ConfigureAwait(false) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                length++;

                if (!ctx.Request.EndsWith("\r\n"))
                    continue;

                ctx.Request = string.Join(' ', ctx.Request[..^2]);
                ctx.Request = ctx.Request.Replace($":{Program.Config.GeminiPort}", "");
                ctx.Request = ctx.Request.Replace($":{Program.Config.SpartanPort}", "");
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Size: {length} bytes");
                break;
            }
        }
        public static async ValueTask<Response> ProcessGetRequest(Context ctx)
        {
            if (ctx.Request.Contains(".."))
                return Response.BadRequest("invalid request", !ctx.IsGemini);
            if (ctx.Uri.Host != ctx.Capsule.FQDN)
                return Response.ProxyDenied();
            if (ctx.Uri.Port != -1)
            {
                if (ctx.IsGemini && ctx.Uri.Port != Program.Config.GeminiPort)
                    return Response.ProxyDenied();
                if (ctx.Uri.Port != Program.Config.SpartanPort && !ctx.IsGemini)
                    return Response.BadRequest("port invalid", !ctx.IsGemini);
            }

            var location = ctx.Capsule.GetLocation(ctx.Uri);
            if (location == null)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Location not found");
                return Response.NotFound("Location not found", !ctx.IsGemini);
            }

            if (location.RequireClientCert)
            {
                if (!ctx.IsGemini)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> requires certificate but spartan doesn't support that");
                    return Response.BadRequest("Client Certificate required - Connect using Gemini", !ctx.IsGemini);
                }

                var gctx = (GeminiCtx)ctx;

                if (gctx.Cert == null)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Location '{location.AbsoluteRootPath}' -> requires a certificate but none was sent");
                    return Response.CertRequired();
                }
            }

            if (string.IsNullOrEmpty(Path.GetFileName(ctx.Request)))
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> No filename for request");
                if (location.DirectoryListing)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Create DirectoryListing");
                    var gmi = CreateDirectoryListing(ctx, location);
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> DirectoryListing -> {gmi.Length} bytes");
                    return Response.Ok(Encoding.UTF8.GetBytes(gmi), "text/gemini", !ctx.IsGemini);
                }
                else
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Adding {location.Index} to request");
                    ctx.Request = Path.Combine(ctx.Request, location.Index);
                    ctx.Uri = new Uri(ctx.Request);
                }
            }

            if (location.CGI)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Invoking CGI");
                Console.WriteLine($"--- BEGIN CGI STREAM ---");
                var counter = 0;
                foreach (var line in CGI.ExecuteScript(ctx, location.Index, location.AbsoluteRootPath))
                {
                    var l = line;
                    if (counter == 0)
                        l += "\r\n";
                    else
                        l += '\n';
                    ctx.Stream.Write(Encoding.UTF8.GetBytes(l));
                    ctx.Stream.Flush();
                    counter++;
                }
                Console.WriteLine($"--- END CGI STREAM ---");
                return new("");
            }

            ctx.Request = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));
            if(ctx.Request == ctx.Capsule.AbsoluteTlsCertPath)
                return Response.NotFound("nice try");
            
            if (!File.Exists(ctx.Request))
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Not Found");
                return Response.NotFound(ctx.Request, !ctx.IsGemini);
            }

            var ext = Path.GetExtension(ctx.Request);
            var mimeType = Util.GetMimeType(ext);
            var data = await File.ReadAllBytesAsync(ctx.Request).ConfigureAwait(false);

            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> {data.Length / 1024f:0.00}kb of {mimeType}");
            return Response.Ok(data, mimeType, !ctx.IsGemini);
        }

        public static async ValueTask<Response> UploadFile(Context ctx, string path, Uri pathUri, string mimeType, int size)
        {
            var location = ctx.Capsule.GetLocation(pathUri);

            if (string.IsNullOrEmpty(path) || location == null)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} missing location or path");
                return Response.BadRequest("missing filaneme or forbidden path", !ctx.IsGemini);
            }
            if (ctx.Capsule.MaxUploadSize <= size)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> {size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}");
                return Response.BadRequest($"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}", !ctx.IsGemini);
            }

            var isAllowedType = location.AllowedMimeTypes.Any(x => x.Key.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.Key.ToLowerInvariant().Split('/')[1] == "*" && mimeType.Split('/')[0] == x.Key.ToLowerInvariant().Split('/')[0]));

            if (!isAllowedType)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> {mimeType} not allowed at {location.AbsoluteRootPath}");
                return Response.BadRequest("mimetype not allowed here", !ctx.IsGemini);
            }

            var data = await ReceivePayload(ctx, size).ConfigureAwait(false);
            File.WriteAllBytes(path, data);
            return Response.Redirect($"{ctx.Capsule.FQDN}{Path.GetDirectoryName(pathUri.AbsolutePath)}/", !ctx.IsGemini);
        }

        private static async Task<byte[]> ReceivePayload(Context ctx, int size)
        {
            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> receiving {size / 1024f:0.00}kb payload");
            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
            {
                fileLen += await ctx.Stream.ReadAsync(data.AsMemory(fileLen, size - fileLen)).ConfigureAwait(false);
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> received {fileLen}/{size}");
            }
            return data;
        }

        public static string CreateDirectoryListing(Context ctx, Location loc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### LAST  MODIFIED   |  SIZE  | NAME");

            foreach (var file in Directory.GetFiles(loc.AbsoluteRootPath).OrderBy(x => x))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> {ctx.Uri.Scheme}://{ctx.Capsule.FQDN}/{loc.AbsoluteRootPath.Replace(ctx.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {Util.CenterString(fi.CreationTimeUtc.ToString(), 26)} | {Util.CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        public static void CloseConnection(Context ctx)
        {
            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> complete");
            ctx.Stream.Flush();
            ctx.Socket.Close();
            ctx.Stream.Dispose();
            ctx.Socket.Dispose();
        }
    }
}