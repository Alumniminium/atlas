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
        public static IEnumerable<string> ExecuteScript(Context ctx, string scriptName, string path, string pathInfo)
        {
            var info = new ProcessStartInfo
            {
                WorkingDirectory = path,
                UseShellExecute = false,
                FileName = "sh",
                Arguments = $"-c {Path.Combine(path, Path.GetFileName(scriptName)).Replace("//", "/")}",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            info.EnvironmentVariables.Clear();
            AddBaseEnvironmentVariables(info, ctx, scriptName, pathInfo);

            if (ctx is GeminiCtx gCtx)
                AddGeminiEnvironmentVariables(info, gCtx);
            else
                info.EnvironmentVariables.Add("AUTH_TYPE", "none");

            return RunScript(info, ctx);
        }

        private static IEnumerable<string> RunScript(ProcessStartInfo info, Context ctx)
        {
            var bc = new BlockingCollection<string>();
            using var process = new Process
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
                if (line == null)
                {
                    if (process.HasExited)
                        break;
                    continue;
                }

                Console.WriteLine(line);
                yield return line;
            }

            var errors = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
                yield return ctx.IsGemini ? $"{(int)GeminiCode.CGIError} {errors}\r\n" : $"{(int)SpartanCode.ServerError} {errors}\r\n";

            if (!string.IsNullOrWhiteSpace(errors))
                Console.WriteLine(errors);
        }

        private static void AddBaseEnvironmentVariables(ProcessStartInfo info, Context ctx, string scriptName, string pathInfo)
        {
            info.EnvironmentVariables.Add("DOTNET_CLI_HOME", Environment.GetEnvironmentVariable("DOTNET_CLI_HOME") ?? "/tmp/.dotnet");
            info.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
            info.EnvironmentVariables.Add("SERVER_PROTOCOL", ctx.IsGemini ? "GEMINI" : "SPARTAN");
            info.EnvironmentVariables.Add("SERVER_PORT", (ctx.IsGemini ? Program.Cfg.GeminiPort : Program.Cfg.SpartanPort).ToString());
            info.EnvironmentVariables.Add("SERVER_SOFTWARE", $"atlas/{Program.Version}");
            info.EnvironmentVariables.Add("URL", ctx.Request);
            info.EnvironmentVariables.Add("SCRIPT_NAME", scriptName);
            info.EnvironmentVariables.Add("PATH_INFO", pathInfo);
            info.EnvironmentVariables.Add("QUERY_STRING", ctx.Uri.Query);
            info.EnvironmentVariables.Add("SERVER_NAME", ctx.Capsule.FQDN);
            info.EnvironmentVariables.Add("REMOTE_HOST", ctx.ClientIP);
            info.EnvironmentVariables.Add("REMOTE_ADDR", ctx.ClientIP);
        }

        private static void AddGeminiEnvironmentVariables(ProcessStartInfo info, GeminiCtx ctx)
        {
            info.EnvironmentVariables.Add("TLS_VERSION", "1.3");

            if (ctx.Certificate != null)
            {
                info.EnvironmentVariables.Add("REMOTE_USER", ctx.CertSubject);
                info.EnvironmentVariables.Add("TLS_CLIENT_VALID", ctx.IsValidCert.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_TRUSTED", ctx.IsTrustedCert.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_SUBJECT", ctx.CertSubject);
                info.EnvironmentVariables.Add("TLS_CLIENT_HASH", ctx.CertThumbprint);
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_BEFORE", ctx.Certificate.NotBefore.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_NOT_AFTER", ctx.Certificate.NotAfter.ToString());
                info.EnvironmentVariables.Add("TLS_CLIENT_SERIAL_NUMBER", ctx.Certificate.GetSerialNumberString());
                info.EnvironmentVariables.Add("AUTH_TYPE", "certificate");
            }
            else
            {
                info.EnvironmentVariables.Add("AUTH_TYPE", "none");
            }
        }
    }
}