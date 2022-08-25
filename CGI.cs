using System.Collections.Concurrent;
using System.Diagnostics;
using atlas.Contexts;

namespace atlas
{
    public static class CGI
    {
        public static IEnumerable<string> ExecuteScript(AtlasCtx ctx, string scriptName, string path)
        {
            var info = new ProcessStartInfo();

            var pathVar = info.Environment["PATH"];
            info.EnvironmentVariables.Clear();
            info.EnvironmentVariables.Add("DOTNET_CLI_HOME","/home/trbl/.dotnet");
            info.EnvironmentVariables.Add("PATH", pathVar);
            info.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
            info.EnvironmentVariables.Add("SERVER_PROTOCOL", $"{(ctx is GeminiCtx ? "gemini" : "spartan")}");
            info.EnvironmentVariables.Add("SERVER_SOFTWARE", "atlas/0.1");
            info.EnvironmentVariables.Add("SPARTAN_URL", ctx.Request.Replace("\r\n", ""));
            info.EnvironmentVariables.Add("SCRIPT_NAME", scriptName);
            info.EnvironmentVariables.Add("PATH_INFO", ctx.Uri.AbsolutePath);
            info.EnvironmentVariables.Add("QUERY_STRING", ctx.Uri.Query);
            info.EnvironmentVariables.Add("SERVER_NAME", ctx.Capsule.FQDN);
            info.EnvironmentVariables.Add("SERVER_PORT", $"{Server.Config.Port}");
            info.EnvironmentVariables.Add("REMOTE_HOST", ctx.Socket.RemoteEndPoint.ToString());
            info.EnvironmentVariables.Add("REMOTE_ADDR", ctx.Socket.RemoteEndPoint.ToString().Split(':')[0]);
            info.EnvironmentVariables.Add("AUTH_TYPE", $"{(ctx is GeminiCtx ? "CERTIFICATE" : "NONE")}");

            if(ctx is GeminiCtx gCtx)
            {
                info.EnvironmentVariables.Add("GEMINI_URL", ctx.Request.Replace("\r\n", ""));
                info.EnvironmentVariables.Add("TLS_VERSION", "1.3");
                info.EnvironmentVariables.Add("TLS_CIPHER", gCtx.CertAlgo.ToString());
                info.EnvironmentVariables.Add("TLS_KEY_EXCHANGE", gCtx.CertKx.ToString());
                info.EnvironmentVariables.Add("REMOTE_USER", $"{gCtx.ClientIdentity}");
                info.EnvironmentVariables.Add("TLS_CLIENT_HASH", gCtx.ClientIdentityHash);
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_BEFORE", gCtx.ClientCert.GetEffectiveDateString());
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_AFTER", gCtx.ClientCert.GetExpirationDateString());
                info.EnvironmentVariables.Add("TLS_CLIENT_SERIAL_NUMBER", gCtx.ClientCert.GetSerialNumberString());
            }

            info.WorkingDirectory = path;
            info.UseShellExecute = false;
            info.FileName = $"bash";
            info.Arguments = $"-c {path}{scriptName}";
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            return ExecuteScript(info);
        }

        public static IEnumerable<string> ExecuteScript(ProcessStartInfo info)
        {
            var tcs = new CancellationTokenSource();
            var bc = new BlockingCollection<string>();
            var process = new Process
            {
                StartInfo = info,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s,e)=> bc.Add(e.Data);
            process.Exited += (x,p) => tcs.Cancel(false);

            process.Start();
            process.BeginOutputReadLine();

            foreach(var line in bc.GetConsumingEnumerable(tcs.Token))
            {
                if(line == null)
                    continue;
                    
                yield return line;
            }
            process.Close();
            process.Dispose();
        }
    }
}