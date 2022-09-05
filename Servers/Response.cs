using System;
using System.Text;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas.Servers
{
    public class Response
    {
        public Memory<byte> Data;
        public string MimeType { get; set; } = "text/gemini";

        public Response(Memory<byte> bytes) => Data = bytes;
        public Response(string data) => Data = Encoding.UTF8.GetBytes(data);
        public Response(bool spartan, string mimeType, Memory<byte> buffer)
        {
            MimeType = mimeType;
            var header = Ok(spartan, mimeType);
            Data = new byte[header.Data.Length + buffer.Length].AsMemory();
            header.Data.CopyTo(Data);
            buffer.CopyTo(Data[header.Data.Length..]);
        }

        public static Response Ok(Memory<byte> data, string mimeType = "text/gemini", bool spartan = false) => new(spartan, mimeType, data);
        public static Response Ok(bool spartan = false, string mimeType = "text/gemini") => spartan
                ? new($"{(int)SpartanCode.Success} \r\n")
                : new($"{(int)GeminiCode.Success} {mimeType}\r\n");
        public static Response NotFound(string message, bool spartan = false) => spartan
                ? (new($"{(int)SpartanCode.ServerError} {message}.\r\n"))
                : (new($"{(int)GeminiCode.NotFound} {message}.\r\n"));

        public static Response BadRequest(string reason, bool spartan = false) => spartan
                ? (new($"{(int)SpartanCode.ServerError} {reason}\r\n"))
                : (new($"{(int)GeminiCode.BadRequest} {reason}\r\n"));

        public static Response Redirect(string target, bool spartan = false) => spartan
                ? (new($"{(int)SpartanCode.Redirect} {target}\r\n"))
                : (new($"{(int)GeminiCode.RedirectPerm} gemini://{target}\r\n"));
        public static Response ProxyDenied() => new($"{(int)GeminiCode.ProxyRequestRefused} \r\n");
        public static Response CertRequired() => new($"{(int)GeminiCode.ClientCertRequired} \r\n");
        internal static Response CertExpired()=> new($"{(int)GeminiCode.CertNotValid} \r\n");
        public static implicit operator ReadOnlyMemory<byte>(Response r) => r.Data;
    }
}