using System;
using System.IO;
using System.Threading.Tasks;

namespace atlas.Servers.Gemini
{
    public class UploadProcessor
    {
public static async ValueTask<Response> Process(GeminiCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var path = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = "text/gemini";
            var strSizeBytes = "0";

            for (int i = 0; i < titanArgs.Length; i++)
            {
                var arg = titanArgs[i];
                var kvp = arg.Split('=');

                if (kvp[0] == "mime")
                    mimeType = kvp[1];
                if (kvp[0] == "size")
                    strSizeBytes = kvp[1];
                if (kvp[0] == "charset")
                    continue;
            }

            return int.TryParse(strSizeBytes, out var size)
                ? await DownloadProcessor.UploadFile(ctx, path, pathUri, mimeType, size).ConfigureAwait(false)
                : Response.BadRequest("Invalid Size: " + strSizeBytes);
        }
    }
}