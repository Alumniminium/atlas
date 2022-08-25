using System.Text;
using atlas.Contexts;

namespace atlas.Protocols
{
    public static class Spartan
    {
        public static async ValueTask HandleUpload(SpartanCtx ctx)
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
    }
}