using System;
using System.Threading;
using atlas.Data;
using atlas.Servers;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas
{
    internal class Program
    {
        public static string Version = "0.3";
        public static Configuration Cfg;
        public static GeminiServer GeminiServer;
        public static SpartanServer SpartanServer;

        private static void Main()
        {
            Console.WriteLine("Loading MimeMap...");
            MimeMap.LoadMimeMap();
            Console.WriteLine("Loading Config...");
            Cfg = Configuration.Load();
            Console.WriteLine("Starting Gemini...");
            GeminiServer = new();
            GeminiServer.Start();
            Console.WriteLine("Starting Spartan...");
            SpartanServer = new();
            SpartanServer.Start();
            Console.WriteLine($"Atlas/{Version} Ready!");
            Statistics.Load();
            while (true)
                Thread.Sleep(int.MaxValue);
        }

        public static void Log(Context ctx, string text)
        {
            if (string.IsNullOrWhiteSpace(ctx.Request))
                Console.WriteLine($"[{ctx.Capsule?.FQDN}] [{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.ClientIP} -> {text}");
            else
                Console.WriteLine($"[{ctx.Capsule?.FQDN}] [{(ctx.IsGemini ? "Gemini" : "Spartan")}] {ctx.ClientIP} -> {ctx.Request.Trim()} -> {text}");
        }
    }
}