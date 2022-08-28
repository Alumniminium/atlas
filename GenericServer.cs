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
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> Request: '{ctx.Request}' Size: {length} bytes");
                break;
            }
        }
        public async ValueTask<Response> ProcessGetRequest(AtlasCtx ctx)
        {
            var location = ctx.Capsule.GetLocation(ctx.Uri);
            if (location == null)
            {
                Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> Location not found for Uri: '{ctx.Uri}'");
                return BadRequest("Location not found");
            }

            if (location.RequireClientCert)
            {
                if (!ctx.IsGemini)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> Location not found for Uri: '{ctx.Uri}'");
                    return BadRequest("Client Certificate required - Connect using Gemini");
                }

                var gctx = (GeminiCtx)ctx;

                if (gctx.ClientCert == null)
                {
                    Console.WriteLine($"[{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.IP} -> Location '{location.AbsoluteRootPath}' requires a certificate.");
                    return CertRequired();
                }
            }

            if (string.IsNullOrEmpty(Path.GetFileName(ctx.Request)))
            {
                if (location.DirectoryListing)
                {
                    var gmi = Util.CreateDirectoryListing(ctx, location);
                    return Ok(Encoding.UTF8.GetBytes(gmi));
                }
                else
                    ctx.Request += location.Index;
            }

            if (location.CGI)
            {
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
                return new ("");
            }

            ctx.Request = Path.Combine(location.AbsoluteRootPath, Path.GetFileName(ctx.Uri.AbsolutePath));
            if (!File.Exists(ctx.Request))
                return NotFound("");

            var ext = Path.GetExtension(ctx.Request);
            var mimeType = Util.GetMimeType(ext);
            var data = await File.ReadAllBytesAsync(ctx.Request).ConfigureAwait(false);
            return Ok(data, mimeType);
        }

        public async ValueTask<Response> UploadFile(AtlasCtx ctx, string path, Uri pathUri, string mimeType, int size)
        {
            var location = ctx.Capsule.GetLocation(pathUri);

            if (string.IsNullOrEmpty(path) || location == null)
                return BadRequest("missing filaneme or forbidden path");

            if (ctx.Capsule.MaxUploadSize <= size)
                return BadRequest($"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}");

            var isAllowedType = location.AllowedMimeTypes.Any(x => x.Key.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.Key.ToLowerInvariant().Split('/')[1] == "*" && mimeType.Split('/')[0] == x.Key.ToLowerInvariant().Split('/')[0]));

            if (!isAllowedType)
                return BadRequest("mimetype not allowed here");

            byte[] data = await ReceivePayload(ctx, size).ConfigureAwait(false);

            Console.WriteLine("Finished");
            File.WriteAllBytes(path, data);
            return Redirect($"{Path.GetDirectoryName(pathUri.AbsolutePath)}/");
        }

        private static async Task<byte[]> ReceivePayload(AtlasCtx ctx, int size)
        {
            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
                fileLen += await ctx.Stream.ReadAsync(data.AsMemory(fileLen, size - fileLen)).ConfigureAwait(false);
            return data;
        }

        public static void CloseConnection(AtlasCtx ctx)
        {
            ctx.Stream.Flush();
            ctx.Stream.Dispose();
            ctx.Socket.Dispose();
            Console.WriteLine("Closed Connection");
        }

        public virtual Response BadRequest(string message) => throw new InvalidOperationException("Override this method");
        public virtual Response Ok(byte[] data, string mimeType = "text/gemini") => throw new InvalidOperationException("Override this method");
        public virtual Response NotFound(string message) => throw new InvalidOperationException("Override this method");
        public virtual Response Redirect(string to) => throw new InvalidOperationException("Override this method");
        public static Response CertRequired() => new ($"{(int)GeminiStatusCode.ClientCertRequired}\r\n");
    }
}