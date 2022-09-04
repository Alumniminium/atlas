using System.Collections.Generic;
using System.IO;

namespace atlas.Data
{
    public static class MimeMap
    {
        private static readonly Dictionary<string, string> ExtensionToMimeType = new();
        private static readonly Dictionary<string, string> MimeTypeToExtension = new();

        public static void LoadMimeMap()
        {
            var lines = File.ReadLines("mimetypes.tsv");
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                ExtensionToMimeType.Add(parts[0], parts[1]);
            }
        }
        public static string GetMimeType(string ext, string defaultMimeType = "text/gemini")
        {
            if (!ExtensionToMimeType.TryGetValue(ext, out var mimeType))
                mimeType = defaultMimeType;
            return mimeType;
        }
        public static string GetExtFromMimeType(string mimeType, string defaultExt = ".txt")
        {
            if (!MimeTypeToExtension.TryGetValue(mimeType, out var ext))
                ext = defaultExt;
            return ext;
        }
    }
    public class MimeConfig
    {
        public int MaxSizeBytes { get; set; }
    }
}