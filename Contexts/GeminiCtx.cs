using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace atlas.Contexts
{
    public class GeminiCtx : AtlasCtx
    {
        public override int MaxHeaderSize {get;set;}= 1024;
        public override Uri Uri => new(RequestPath);
        public X509Certificate ServerCert => (Stream as SslStream).LocalCertificate;
        public X509Certificate ClientCert => (Stream as SslStream).RemoteCertificate;
        public string ClientIdentity => ClientCert.Subject.Replace("CN=", "");
        public string ClientIdentityHash => ClientCert.GetCertHashString();

        public CipherAlgorithmType CertAlgo { get; internal set; }
        public ExchangeAlgorithmType CertKx { get; internal set; }

        public override async ValueTask NotFound() => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.FailurePerm} {Uri.AbsolutePath} not allowed.\r\n"));
        public override async ValueTask BadRequest(string reason) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.BadRequest} {reason}\r\n"));
        public override async ValueTask Redirect(string target) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.RedirectTemp} gemini://{Capsule.FQDN}/{target}\r\n"));
        public override async ValueTask ServerError(Exception e) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.BadRequest} {e.Message}\n{e.StackTrace}\r\n"));
        public async ValueTask CertRequired() => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.ClientCertRequired}\r\n"));
        public override async ValueTask Success(byte[] data, string mimeType = "text/gemini")
        {
            await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.Success} {mimeType}; charset=utf-8\r\n"));
            await Stream.WriteAsync(data);
        }

    }
}