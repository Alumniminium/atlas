namespace atlas.Servers.Gemini
{
    public enum GeminiCode
    {
        Input = 10,
        SensitiveInput = 11,
        Success = 20,
        RedirectTemp = 30,
        RedirectPerm = 31,
        FailureTemp = 40,
        ServerUnavailable = 41,
        CGIError = 42,
        ProxyError = 43,
        SlowDown = 44,
        FailurePerm = 50,
        NotFound = 51,
        Gone = 52,
        ProxyRequestRefused = 53,
        BadRequest = 59,
        ClientCertRequired = 60,
        CertNotAuthorised = 61,
        CertNotValid = 62
    }
}