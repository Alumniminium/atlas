using System.Net.Sockets;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public abstract class AtlasCtx
    {
        public bool IsGemini => this is GeminiCtx;
        public abstract int MaxHeaderSize {get;set;}
        public Socket Socket;
        public Stream Stream;
        public Capsule Capsule;
        public virtual Uri Uri => new(Request);
        public string Request { get; set; }
        public string IP => Socket.RemoteEndPoint.ToString().Split(':')[0];
    }
}