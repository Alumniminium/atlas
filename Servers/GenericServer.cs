using System.Net.Sockets;
using System.Text;
using atlas.Contexts;

namespace atlas
{
    public class GenericServer
    {
        public Socket Socket { get; set; }

        public static async ValueTask ReceiveRequest(AtlasCtx ctx)
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
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Size: {length} bytes");
                break;
            }
        }
        public async ValueTask<Response> ProcessGetRequest(AtlasCtx ctx)
        {
            var location = ctx.Capsule.GetLocation(ctx.Uri);
            if (location == null)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Location not found");
                return BadRequest("Location not found");
            }

            if (location.RequireClientCert)
            {
                if (!ctx.IsGemini)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> requires certificate but spartan doesn't support that");
                    return BadRequest("Client Certificate required - Connect using Gemini");
                }

                var gctx = (GeminiCtx)ctx;

                if (gctx.ClientCert == null)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Location '{location.AbsoluteRootPath}' -> requires a certificate but none was sent");
                    return CertRequired();
                }
            }

            if (string.IsNullOrEmpty(Path.GetFileName(ctx.Request)))
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> No filename for request");
                if (location.DirectoryListing)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Create DirectoryListing");
                    var gmi = Util.CreateDirectoryListing(ctx, location);
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> DirectoryListing -> {gmi.Length} bytes");
                    return Ok(Encoding.UTF8.GetBytes(gmi));
                }
                else
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> Adding {location.Index} to request");
                    ctx.Request += location.Index;
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
            if (!File.Exists(ctx.Request))
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> '{ctx.Request}' -> Not Found");
                return NotFound(ctx.Request);
            }

            var ext = Path.GetExtension(ctx.Request);
            var mimeType = Util.GetMimeType(ext);
            var data = await File.ReadAllBytesAsync(ctx.Request).ConfigureAwait(false);

            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> '{ctx.Request}' -> {data.Length/1024f:0.00}kb of {mimeType}");
            return Ok(data, mimeType);
        }

        public async ValueTask<Response> UploadFile(AtlasCtx ctx, string path, Uri pathUri, string mimeType, int size)
        {
            var location = ctx.Capsule.GetLocation(pathUri);

            if (string.IsNullOrEmpty(path) || location == null)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> '{ctx.Request}' missing location or path");
                return BadRequest("missing filaneme or forbidden path");
            }
            if (ctx.Capsule.MaxUploadSize <= size)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> '{ctx.Request}' -> {size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}");
                return BadRequest($"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}");
            }

            var isAllowedType = location.AllowedMimeTypes.Any(x => x.Key.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.Key.ToLowerInvariant().Split('/')[1] == "*" && mimeType.Split('/')[0] == x.Key.ToLowerInvariant().Split('/')[0]));

            if (!isAllowedType)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> '{ctx.Request}' -> {mimeType} not allowed at {location.AbsoluteRootPath}");
                return BadRequest("mimetype not allowed here");
            }

            var data = await ReceivePayload(ctx, size).ConfigureAwait(false);
            File.WriteAllBytes(path, data);
            return Redirect($"{Path.GetDirectoryName(pathUri.AbsolutePath)}/");
        }

        private static async Task<byte[]> ReceivePayload(AtlasCtx ctx, int size)
        {
            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> receiving {size/1024f:0.00}kb payload");    
            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
            {
                fileLen += await ctx.Stream.ReadAsync(data.AsMemory(fileLen, size - fileLen)).ConfigureAwait(false);
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> received {fileLen}/{size}"); 
            }
            return data;
        }

        public static void CloseConnection(AtlasCtx ctx)
        {
            Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> {ctx.Request} -> complete"); 
            ctx.Stream.Flush();
            ctx.Stream.Dispose();
            ctx.Socket.Dispose();
        }

        public virtual Response BadRequest(string message) => throw new InvalidOperationException("Override this method");
        public virtual Response Ok(byte[] data, string mimeType = "text/gemini") => throw new InvalidOperationException("Override this method");
        public virtual Response NotFound(string message) => throw new InvalidOperationException("Override this method");
        public virtual Response Redirect(string to) => throw new InvalidOperationException("Override this method");
        public static Response CertRequired() => new($"{(int)GeminiStatusCode.ClientCertRequired}\r\n");
    }
}