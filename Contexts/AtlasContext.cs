using System.Net.Sockets;

namespace atlas.Contexts
{
    public abstract class AtlasCtx
    {
        public Socket Socket;
        public Stream Stream;
        public Capsule Capsule;
        public virtual Uri Uri => new(RequestPath);
        public string Request { get; set; }
        public string RequestPath;
        public bool DirectoryListing { get; set; }
        public bool RequestFileExists { get; set; }
        public bool IsUpload { get; internal set; }
        public abstract ValueTask NotFound();
        public abstract ValueTask BadRequest(string reason);
        public abstract ValueTask Success(byte[] data, string mimeType = "text/gemini");
        public abstract ValueTask ServerError(Exception e);
        public abstract ValueTask Redirect(string target);
        public void CloseConnection()
        {
            Stream.Flush();
            Stream.Close();
            Socket.Close();
            Console.WriteLine("Closed Connection");
        }
    }
}