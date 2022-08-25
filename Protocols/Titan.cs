using System.Text;
using atlas.Contexts;

namespace atlas.Protocols
{
    public static class Titan
    {
        public static async ValueTask HandleUpload(GeminiCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var path = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = "text/gemini";
            var strSizeBytes = "0";

            for(int i = 0; i<titanArgs.Length;i++)
            {
                var arg = titanArgs[i];
                var kvp = arg.Split('=');

                if(kvp[0] == "mime")
                    mimeType = kvp[1];
                if(kvp[0] == "size")
                    strSizeBytes = kvp[1];
                if(kvp[0] == "charset")
                    continue;
            }
            
            var size = int.Parse(strSizeBytes);

            var location = ctx.Capsule.GetLocation(pathUri);
            var isAllowedType = false;

            if (string.IsNullOrEmpty(path) || location == null)
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
            File.WriteAllBytes(path, data);
            await ctx.Redirect($"{Path.GetDirectoryName(pathUri.AbsolutePath)}/");            
        }
    }
}