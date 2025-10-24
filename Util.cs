using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using atlas.Data;
using atlas.Servers;
using atlas.Servers.Gemini;

namespace atlas
{
    public static class Util
    {
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

        public static string CreateDirectoryListing(Context ctx, Location loc)
        {
            var sb = new StringBuilder();

            foreach (var file in Directory.GetFiles(loc.AbsoluteRootPath))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> {ctx.Uri.Scheme}://{ctx.Capsule.FQDN}/{loc.AbsoluteRootPath.Replace(ctx.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)}  {CenterString(fi.CreationTimeUtc.ToString("yyyy-MM-dd"), 12)} | {CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        internal static string ReplaceTokens(string input, Context ctx)
        {
            input = ReplaceToken(input, "%%{sub}%%", GetSubject(ctx));
            input = ReplaceToken(input, "%%{host}%%", ctx.Uri?.Host);
            input = ReplaceToken(input, "%%{path}%%", ctx.Uri?.AbsolutePath);
            input = ReplaceToken(input, "%%{scheme}%%", ctx.Uri?.Scheme);
            input = ReplaceToken(input, "%%{date}%%", DateTime.Now.ToString("yyyy-MM-dd"));
            input = ReplaceToken(input, "%%{time}%%", DateTime.Now.ToString("HH:mm:ss"));
            input = ReplaceToken(input, "%%{datetime}%%", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            input = ReplaceToken(input, "%%{rendertime}%%", (DateTime.UtcNow - ctx.RequestStart).TotalMilliseconds.ToString("0.00"));
            input = ReplaceToken(input, "%%{ls}%%", CreateDirectoryListing(ctx, ctx.Capsule.GetLocation(ctx.Uri)));

            return input.Trim();
        }

        private static string ReplaceToken(string input, string token, string value)
            => input.Contains(token) ? input.Replace(token, value ?? string.Empty) : input;

        private static string GetSubject(Context ctx)
        {
            if (ctx is GeminiCtx gctx)
            {
                var name = gctx.Certificate?.Subject.Replace("CN=", "");
                return string.IsNullOrEmpty(name) ? "Anon" : name;
            }
            return "Spartan";
        }
    }
}
