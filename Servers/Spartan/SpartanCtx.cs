using System.IO;
using System.Net.Sockets;

namespace atlas.Servers.Spartan
{
    public class SpartanCtx : Context
    {
        public int PayloadSize;
        public SpartanCtx(Socket socket) : base(socket, 4096) 
        { 
            Writer = new BinaryWriter(new NetworkStream(socket));
        }
    }
}