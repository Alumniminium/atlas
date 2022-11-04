using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using atlas.Data;
using atlas.Servers.Gemini;

namespace atlas.Servers
{
    public abstract class Context
    {
        public byte[] Buffer;
        public bool IsGemini => this is GeminiCtx;
        public bool IsSpartan => this is not GeminiCtx;
        public Socket Socket;
        public BinaryWriter Writer;
        public StreamReader Reader;
        public Capsule Capsule;
        public Uri Uri;
        public string Request = string.Empty;
        public string IP => Socket.RemoteEndPoint.ToString().Split(':')[0];

        public Context(Socket socket, int bufferSize)
        {
            Socket = socket;
            Reader = new (new NetworkStream(socket), Encoding.UTF8);
            Buffer = new byte[bufferSize];
        }
    }
}