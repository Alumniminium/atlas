using System.Text;
using atlas.Contexts;

namespace atlas
{
    public static class Util
    {
        public static string CreateDirectoryListing(AtlasCtx ctx, Location loc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### LAST  MODIFIED   |  SIZE  | NAME");
            
            foreach (var file in Directory.GetFiles(loc.AbsoluteRootPath).OrderBy(x => x))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> {ctx.Uri.Scheme}://{ctx.Capsule.FQDN}/{loc.AbsoluteRootPath.Replace(ctx.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {CenterString(fi.CreationTimeUtc.ToString(), 26)} | {CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }


        public static string GetMimeType(string ext)
        {
            if (!Program.ExtensionToMimeType.TryGetValue(ext, out var mimeType))
                mimeType = "text/gemini";
            return mimeType;
        }

        public static string CenterString(string txt, int length)
        {
            var delta = Math.Abs(txt.Length - length);
            for (int i = 0; i < delta; i++)
            {
                if (i % 2 == 0)
                    txt = " " + txt;
                else
                    txt += " ";
            }
            return txt;
        }
    }
}