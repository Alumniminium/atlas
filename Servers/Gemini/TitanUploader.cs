using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using atlas.Data;

namespace atlas.Servers.Gemini
{
    public class UploadProcessor
    {
        public static async ValueTask<Response> Process(GeminiCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var path = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);

            var parameters = ParseTitanParameters(titanArgs);
            var mimeType = parameters.GetValueOrDefault("mime", Defaults.DefaultMimeType);
            var sizeStr = parameters.GetValueOrDefault("size", "0");

            return int.TryParse(sizeStr, out var size)
                ? await GenericServer.ProcessFileUpload(ctx, path, pathUri, mimeType, size).ConfigureAwait(false)
                : Response.BadRequest("Invalid Size: " + sizeStr);
        }

        private static Dictionary<string, string> ParseTitanParameters(string[] args)
        {
            return args
                .Skip(1)
                .Select(arg => arg.Split('=', 2))
                .Where(kvp => kvp.Length == 2)
                .ToDictionary(kvp => kvp[0].Trim(), kvp => kvp[1].Trim());
        }
    }
}