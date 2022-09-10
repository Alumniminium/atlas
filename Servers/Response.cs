using System;
using System.Text;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas.Servers
{
    public class Response
    {
        public bool IsGemini { get; set; }
        public Memory<byte> Data;
        public string MimeType { get; set; } = "text/gemini";

        public Response(Memory<byte> bytes)
        {
            Data = bytes;
        }

        public Response(string data, bool spartan)
        {
            IsGemini = !spartan;
            Data = Encoding.UTF8.GetBytes(data);
        }

        public Response(bool spartan, string mimeType, Memory<byte> buffer)
        {
            IsGemini = !spartan;
            MimeType = mimeType;
            var header = Ok(spartan, mimeType);
            Data = new byte[header.Data.Length + buffer.Length].AsMemory();
            header.Data.CopyTo(Data);
            buffer.CopyTo(Data[header.Data.Length..]);
        }

        public static Response Ok(Memory<byte> data, string mimeType = "text/gemini", bool spartan = false) => new(spartan, mimeType, data);

        public static Response Ok(bool spartan = false, string mimeType = "text/gemini") => spartan
                        ? new($"{(int)SpartanCode.Success} {mimeType}\r\n", spartan)
                        : new($"{(int)GeminiCode.Success} {mimeType}\r\n", spartan);

        public static Response NotFound(string message, bool spartan = false) => spartan
                        ? (new($"{(int)SpartanCode.ServerError} {message}.\r\n",spartan))
                        : (new($"{(int)GeminiCode.NotFound} {message}.\r\n",spartan));

        public static Response BadRequest(string reason, bool spartan = false) => spartan
                ? (new($"{(int)SpartanCode.ServerError} {reason}\r\n",spartan))
                : (new($"{(int)GeminiCode.BadRequest} {reason}\r\n",spartan));

        public static Response Redirect(string target, bool spartan = false) => spartan
                ? (new($"{(int)SpartanCode.Redirect} {target}\r\n",spartan))
                : (new($"{(int)GeminiCode.RedirectPerm} gemini://{target}\r\n",spartan));

        public static Response ProxyDenied() => new($"{(int)GeminiCode.ProxyRequestRefused} \r\n",false);

        public static Response ProxyError() => new($"{(int)GeminiCode.ProxyError} \r\n",false);

        public static Response CertRequired() => new($"{(int)GeminiCode.ClientCertRequired} \r\n",false);

        internal static Response CertExpired() => new($"{(int)GeminiCode.CertNotValid} \r\n",false);

        public static implicit operator ReadOnlyMemory<byte>(Response r) => r.Data;
    }
}