using System.Text;
using atlas.Contexts;

namespace atlas.Protocols
{
    public static class Spartan
    {
        public const int MAX_URI_LENGTH_SPARTAN = 1024;

        public static async ValueTask ReceiveHeader(SpartanCtx ctx)
        {
            var reqBuffer = new byte[MAX_URI_LENGTH_SPARTAN + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.Stream.ReadAsync(reqBuffer.AsMemory(length, 1)) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if (!ctx.Request.EndsWith("\r\n"))
                    continue;

                ctx.RequestPath = string.Join(' ', ctx.Request[..^2]);
                break;
            }
            var parts = ctx.Request.Split(' ');
            var host = parts[0];
            var path = parts[1];
            var size = int.Parse(parts[2]);
            ctx.IsUpload = size > 0;

            if (Server.Config.Capsules.TryGetValue(host, out var capsule))
                ctx.Capsule = capsule;
            ctx.RequestPath = path;
        }

        public static async ValueTask POST(SpartanCtx ctx)
        {
            var parts = ctx.Request.Split(' ');
            var host = parts[0];
            var pathUri = new Uri(parts[1]);
            var size = int.Parse(parts[2]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);

            var location = ctx.Capsule.GetLocation(pathUri);
            var isAllowedType = false;
            var mimeType = Util.GetMimeType(Path.GetExtension(pathUri.AbsolutePath));
            var filename = Path.GetFileName(parts[1]);

            if (string.IsNullOrEmpty(filename) || location == null)
            {
                await ctx.BadRequest("missing filaneme or forbidden path");
                return;
            }

            isAllowedType = location.AllowedMimeTypes.Any(x => x.MimeType.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.MimeType.Split('/')[1] == "*" && mimeType.Split('/')[0] == x.MimeType.Split('/')[0]));

            if (!isAllowedType)
            {
                await ctx.BadRequest("mimetype not allowed here");
                return;
            }

            if (ctx.Capsule.MaxUploadSize <= size)
            {
                await ctx.BadRequest($"{size} exceeds max upload size of {ctx.Capsule.MaxUploadSize}");
                return;
            }

            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
                fileLen += await ctx.Stream.ReadAsync(data.AsMemory(fileLen, size - fileLen));

            Console.WriteLine("Finished");
            File.WriteAllBytes(absoluteDestinationPath, data);
            await ctx.Redirect($"{Path.GetDirectoryName(pathUri.AbsolutePath)}/");
        }
        public static async ValueTask GET(SpartanCtx ctx)
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