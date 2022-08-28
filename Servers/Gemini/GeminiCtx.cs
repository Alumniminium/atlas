using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace atlas.Servers.Gemini
{
    public class GeminiCtx : AtlasCtx
    {
        public override int MaxHeaderSize {get;set;}= 1024;
        public X509Certificate ServerCert => (Stream as SslStream).LocalCertificate;
        public X509Certificate ClientCert => (Stream as SslStream).RemoteCertificate;
        public string ClientIdentity => ClientCert.Subject.Replace("CN=", "");
        public string ClientIdentityHash => ClientCert.GetCertHashString();

        public CipherAlgorithmType CertAlgo { get; internal set; }
        public ExchangeAlgorithmType CertKx { get; internal set; }

    }
}