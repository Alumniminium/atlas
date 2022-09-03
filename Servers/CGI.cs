using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using atlas.Servers.Gemini;
using atlas.Servers.Spartan;

namespace atlas.Servers
{
    public static class CGI
    {
        public static IEnumerable<string> ExecuteScript(Context ctx, string scriptName, string path, string PATH_INFO)
        {
            var info = new ProcessStartInfo();

            var pathVar = info.Environment["PATH"];
            info.EnvironmentVariables.Clear();
            info.EnvironmentVariables.Add("DOTNET_CLI_HOME", "/home/trbl/.dotnet");
            info.EnvironmentVariables.Add("PATH", pathVar);
            info.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
            info.EnvironmentVariables.Add("SERVER_PROTOCOL", $"{(ctx.IsGemini ? "GEMINI" : "SPARTAN")}");
            info.EnvironmentVariables.Add("SERVER_PORT", $"{(ctx.IsGemini ? Program.Config.GeminiPort : Program.Config.SpartanPort)}");
            info.EnvironmentVariables.Add("SERVER_SOFTWARE", $"atlas/{Program.Version}");
            info.EnvironmentVariables.Add("SPARTAN_URL", ctx.Request.Replace("\r\n", ""));
            info.EnvironmentVariables.Add("SCRIPT_NAME", scriptName);
            info.EnvironmentVariables.Add("PATH_INFO", PATH_INFO);
            info.EnvironmentVariables.Add("QUERY_STRING", ctx.Uri.Query);
            info.EnvironmentVariables.Add("SERVER_NAME", ctx.Capsule.FQDN);
            info.EnvironmentVariables.Add("REMOTE_HOST", ctx.IP);
            info.EnvironmentVariables.Add("REMOTE_ADDR", ctx.IP);
            info.EnvironmentVariables.Add("AUTH_TYPE", $"{(ctx.IsGemini ? "CERTIFICATE" : "NONE")}");

            if (ctx is GeminiCtx gCtx)
            {
                info.EnvironmentVariables.Add("GEMINI_URL", ctx.Request.Replace("\r\n", ""));
                info.EnvironmentVariables.Add("TLS_VERSION", "1.3");
                info.EnvironmentVariables.Add("REMOTE_USER", $"{gCtx.Cert.Subject}");
                info.EnvironmentVariables.Add("TLS_CLIENT_VALID", $"{gCtx.Cert.Valid}");
                info.EnvironmentVariables.Add("TLS_CLIENT_TRUSTED", $"{gCtx.Cert.Trusted}");
                info.EnvironmentVariables.Add("TLS_CLIENT_SUBJECT", $"{gCtx.Cert.Subject}");
                info.EnvironmentVariables.Add("TLS_CLIENT_HASH", gCtx.Cert.Thumbprint);
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_BEFORE", gCtx.Cert.Certificate.NotBefore.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_AFTER", gCtx.Cert.Certificate.NotAfter.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_SERIAL_NUMBER", gCtx.Cert.Certificate.GetSerialNumberString());
            }

            info.WorkingDirectory = path;
            info.UseShellExecute = false;
            info.FileName = $"sh";
            info.Arguments = $"-c {Path.Combine(path, Path.GetFileName(scriptName))}";
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            return ExecuteScript(info, ctx);
        }

        public static IEnumerable<string> ExecuteScript(ProcessStartInfo info, Context ctx)
        {
            var bc = new BlockingCollection<string>();
            var process = new Process
            {
                StartInfo = info,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, e) => bc.Add(e.Data);
            process.Exited += (x, p) => bc.Add(null);

            process.Start();
            process.BeginOutputReadLine();

            foreach (var line in bc.GetConsumingEnumerable())
            {
                if (process.HasExited && line == null)
                    break;

                if (line == null)
                    continue;

                Console.WriteLine(line);

                yield return line;
            }

            var errors = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
            {
                if (ctx.IsGemini)
                    yield return $"{(int)GeminiStatusCode.CGIError} {errors}\r\n";
                else
                    yield return $"{(int)SpartanStatusCode.ServerError} {errors}\r\n";
            }
            Console.WriteLine(errors);

            process.Close();
            process.Dispose();
        }
    }
}