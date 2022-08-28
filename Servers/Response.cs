using System.Text;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas.Servers
{
    public class Response
    {
        public byte[] Bytes;
        public Response(byte[] bytes) => Bytes = bytes;
        public Response(string data) => Bytes = Encoding.UTF8.GetBytes(data);

        public Response(bool spartan, string mimeType, byte[] buffer)
        {
            var header = Encoding.UTF8.GetBytes($"{(spartan ? (int)SpartanStatusCode.Success : (int)GeminiStatusCode.Success)} {mimeType}; charset=utf-8\r\n");
            Bytes = new byte[header.Length + buffer.Length];
            Buffer.BlockCopy(header, 0, Bytes, 0, header.Length);
            Buffer.BlockCopy(buffer, 0, Bytes, header.Length, buffer.Length);
        }
        public static Response NotFound(string message, bool spartan = false)
        {
            if (spartan)
                return new($"{(int)SpartanStatusCode.ServerError} {message}.\r\n");

            return new($"{(int)GeminiStatusCode.FailurePerm} {message}.\r\n");
        }

        public static Response BadRequest(string reason, bool spartan = false)
        {
            if (spartan)
                return new($"{(int)SpartanStatusCode.ServerError} {reason}\r\n");
            return new($"{(int)GeminiStatusCode.BadRequest} {reason}\r\n");
        }

        public static Response Redirect(string target, bool spartan = false)
        {
            if (spartan)
                return new($"{(int)SpartanStatusCode.Redirect} {target}\r\n");
            return new($"{(int)GeminiStatusCode.RedirectTemp} {target}\r\n");
        }

        public static Response Ok(byte[] data, string mimeType = "text/gemini", bool spartan = false) => new(spartan, mimeType, data);
        public static Response CertRequired() => new($"{(int)GeminiStatusCode.ClientCertRequired}\r\n");
        public static implicit operator ReadOnlyMemory<byte>(Response r) => r.Bytes.AsMemory();

    }
}