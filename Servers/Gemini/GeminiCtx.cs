using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace atlas.Servers.Gemini
{
    public class GeminiCtx : Context, IDisposable
    {
        public X509Certificate2 Certificate;
        public string CertSubject => Certificate.Subject.Replace("CN=", "", true, System.Globalization.CultureInfo.InvariantCulture);
        public string CertThumbprint => Certificate.Thumbprint;
        public bool IsSelfSignedCert;
        public bool IsValidCert => DateTime.Now < Certificate.NotAfter && DateTime.Now > Certificate.NotBefore;
        public bool IsTrustedCert => Certificate.Verify();
        public SslStream SslStream;
        
        public GeminiCtx(Socket socket) : base(socket, 1024) 
        { 
            SslStream = new SslStream(new NetworkStream(socket));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Certificate.Dispose();
        }
    }
}