using System;
using System.IO;
using System.Net.Sockets;
using atlas.Data;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public abstract class Context
    {
        public bool IsGemini => this is GeminiCtx;
        public bool IsSpartan => this is not GeminiCtx;
        public int MaxHeaderSize;
        public Socket Socket;
        public Stream Stream;
        public Capsule Capsule;
        public Uri Uri;
        public string Request = string.Empty;
        public string IP => Socket.RemoteEndPoint.ToString().Split(':')[0];
    }
}