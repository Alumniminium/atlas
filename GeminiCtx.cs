using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace atlas
{
    public class GeminiCtx
    {
        public Socket Socket;
        public SslStream SslStream;
        public Capsule Capsule;
        public string FilePath;
        public Uri Uri;
        public X509Certificate ServerCert => SslStream.LocalCertificate;
        public X509Certificate ClientCert => SslStream.RemoteCertificate;
        public string ClientIdentity => ClientCert.Subject.Replace("CN=","");
        public string ClientIdentityHash => ClientCert.GetCertHashString();
    }
}