using System;
using System.Security.Cryptography.X509Certificates;

namespace atlas.Servers.Gemini
{
    public class GeminiCtx : Context, IDisposable
    {
        public X509Certificate2 Certificate;
        public string CertSubject => Certificate.Subject.Replace("CN=", "", true, System.Globalization.CultureInfo.InvariantCulture);
        public string CertThumbprint => Certificate.Thumbprint;
        public bool SelfSignedCert;
        public bool ValidCert => DateTime.Now < Certificate.NotAfter && DateTime.Now > Certificate.NotBefore;
        public bool TrustedCert => Certificate.Verify();

        public GeminiCtx() => MaxHeaderSize = 1024;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Certificate.Dispose();
        }
    }
}