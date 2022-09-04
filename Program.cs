using System;
using System.Threading;
using atlas.Data;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas
{
    internal class Program
    {
        public static string Version = "0.2b";
        public static Configuration Config;
        public static GeminiServer GeminiServer;
        public static SpartanServer SpartanServer;

        private static void Main()
        {
            Console.WriteLine("Loading MimeMap...");
            MimeMap.LoadMimeMap();
            Console.WriteLine("Loading Config...");
            Config = Configuration.Load();
            Console.WriteLine("Starting Gemini...");
            GeminiServer = new();
            GeminiServer.Start();
            Console.WriteLine("Starting Spartan...");
            SpartanServer = new();
            SpartanServer.Start();
            Console.WriteLine($"Atlas/{Version} Ready!");

            while (true)
                Thread.Sleep(int.MaxValue);
        }
    }
}