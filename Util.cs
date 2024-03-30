using System;
using System.Collections.Generic;
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

        internal static string ReplaceTokens(string input, Context ctx)
        {
            Dictionary<string, Func<string>> tokens = new()
            {
                { "%%{sub}%%", () => ctx is GeminiCtx ? (ctx as GeminiCtx).Certificate?.Subject.Replace("CN=","") : "Spartan"},
                { "%%{host}%%", () => ctx.Uri?.Host },
                { "%%{path}%%", () => ctx.Uri?.AbsolutePath },
                { "%%{scheme}%%", () => ctx.Uri?.Scheme },
                { "%%{date}%%", () => DateTime.Now.ToString("yyyy-MM-dd") },
                { "%%{time}%%", () => DateTime.Now.ToString("HH:mm:ss") },
                { "%%{datetime}%%", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "%%{rendertime}%%", () => (DateTime.UtcNow - ctx.RequestStart).TotalMilliseconds.ToString("0.00")}
            };

            foreach (var token in tokens)
                input = input.Replace(token.Key, token.Value());

            return input;
        }
    }
}