using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace atlas
{
    public class GeminiCtx
    {
        public Socket Socket;
        public SslStream SslStream;
        public Capsule Capsule;
        public Uri Uri => new(RequestPath);
        public string Request { get; set; }
        public string RequestPath;
        public bool DirectoryListing { get; set; }
        public bool RequestFileExists { get; set; }
        public X509Certificate ServerCert => SslStream.LocalCertificate;
        public X509Certificate ClientCert => SslStream.RemoteCertificate;
        public string ClientIdentity => ClientCert.Subject.Replace("CN=", "");
        public string ClientIdentityHash => ClientCert.GetCertHashString();

        public bool IsUpload { get; internal set; }
    }
    public class SpartanCtx
    {
        public Socket Socket;
        public NetworkStream SslStream;
        public Capsule Capsule;
        public Uri Uri => new(RequestPath);
        public string Request { get; set; }
        public string RequestPath;
        public bool DirectoryListing { get; set; }
        public bool RequestFileExists { get; set; }

        public bool IsUpload { get; internal set; }
    }
}