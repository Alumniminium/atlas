namespace atlas.Servers.Gemini
{
    public class GeminiCtx : Context
    {
        public ClientCert Cert;
        public GeminiCtx()
        {
            MaxHeaderSize = 1024;
        }
    }
}