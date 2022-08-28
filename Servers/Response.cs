using System.Text;

namespace atlas
{
    public class Response
    {
        public byte[] Bytes;
        public Response(byte[] bytes) => Bytes = bytes;
        public Response(string data) => Bytes = Encoding.UTF8.GetBytes(data);

        public Response(bool spartan, string mimeType, byte[] buffer)
        {
            var header = Encoding.UTF8.GetBytes($"{(spartan ? (int)SpartanStatusCode.Success: (int)GeminiStatusCode.Success)} {mimeType}; charset=utf-8\r\n");
            Bytes = new byte[header.Length + buffer.Length];
            Buffer.BlockCopy(header, 0, Bytes, 0, header.Length);
            Buffer.BlockCopy(buffer, 0, Bytes, header.Length, buffer.Length);
        }

        public static implicit operator ReadOnlyMemory<byte>(Response r) => r.Bytes.AsMemory();
    }
}