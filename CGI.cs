using System;
using System.Diagnostics;
using System.Security;
using System.Text;
using atlas.Contexts;

namespace atlas
{
    public static class CGI
    {
        public static async ValueTask<string> ExecuteScript(GeminiCtx ctx, string scriptName, string path)
        {
            var info = new ProcessStartInfo();

            info.EnvironmentVariables.Clear();
            info.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
            info.EnvironmentVariables.Add("SERVER_PROTOCOL", "gemini");
            info.EnvironmentVariables.Add("SERVER_SOFTWARE", "atlas/0.1");
            info.EnvironmentVariables.Add("GEMINI_URL", ctx.Request.Replace("\r\n",""));
            info.EnvironmentVariables.Add("SCRIPT_NAME", scriptName);
            info.EnvironmentVariables.Add("PATH_INFO", ctx.Uri.AbsolutePath);
            info.EnvironmentVariables.Add("QUERY_STRING", ctx.Uri.Query);
            info.EnvironmentVariables.Add("SERVER_NAME", ctx.Capsule.FQDN);
            info.EnvironmentVariables.Add("SERVER_PORT", $"{Server.Config.Port}");
            info.EnvironmentVariables.Add("REMOTE_HOST", ctx.Socket.RemoteEndPoint.ToString());
            info.EnvironmentVariables.Add("REMOTE_ADDR", ctx.Socket.RemoteEndPoint.ToString().Split(':')[0]);
            info.EnvironmentVariables.Add("TLS_VERSION", "1.3");
            info.EnvironmentVariables.Add("TLS_CIPHER", ctx.CertAlgo.ToString());
            info.EnvironmentVariables.Add("TLS_KEY_EXCHANGE", ctx.CertKx.ToString());
            info.EnvironmentVariables.Add("AUTH_TYPE", $"{(ctx is GeminiCtx ? "CERTIFICATE" : "NONE")}");
            info.EnvironmentVariables.Add("REMOTE_USER", $"{ctx.ClientIdentity}");
            info.EnvironmentVariables.Add("TLS_CLIENT_HASH", ctx.ClientIdentityHash);
            info.EnvironmentVariables.Add("TLS_CLIENT_NOT_BEFORE", ctx.ClientCert.GetEffectiveDateString());
            info.EnvironmentVariables.Add("TLS_CLIENT_NOT_AFTER", ctx.ClientCert.GetExpirationDateString());
            info.EnvironmentVariables.Add("TLS_CLIENT_SERIAL_NUMBER", ctx.ClientCert.GetSerialNumberString());

            info.WorkingDirectory = path;
            info.UseShellExecute=false;
            info.FileName = "sh";
            info.Arguments = $"-c {path}script.sh";
            info.RedirectStandardOutput = true;
            var process = new Process { StartInfo = info };

            process.Start();
            await process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            Console.WriteLine(output);
            return output;
        }
    }
}