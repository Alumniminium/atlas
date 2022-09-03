using System;
using System.Security.Cryptography.X509Certificates;

namespace atlas.Servers.Gemini
{
    public class ClientCert
    {
        public X509Certificate2 Certificate;
        public string Subject => Certificate.Subject.Replace("CN=", "");
        public string Thumbprint => Certificate.Thumbprint;
        public bool SelfSignedCert;
        public bool Valid => DateTime.Now < Certificate.NotAfter && DateTime.Now > Certificate.NotBefore;
        public bool Trusted => Certificate.Verify();
        public void SetCert(X509Certificate shiityCert) => Certificate = new X509Certificate2(shiityCert);
    }
}