using System.Collections.Generic;

namespace atlas
{
    public sealed class Stats
    {
        public long TotalHits => GeminiHits;
        public long TotalBytesSent => GeminiBytesSent;
        public long TotalBytesReceived => GeminiBytesReceived;

        public long GeminiHits { get; set; } = new();
        public long GeminiBytesSent { get; set; } = new();
        public long GeminiBytesReceived { get; set; } = new();
        public Dictionary<string, long> GeminiFiles { get; set; } = new();
    }
}