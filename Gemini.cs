using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace atlas
{
    internal class Gemini
    {
        public const int MAX_URI_LENGTH_GEMINI = 1024;

        public static async ValueTask<(bool, GeminiCtx)> HandShake(Socket client)
        {
            var context = new GeminiCtx()
            {
                Socket = client,
                SslStream = new SslStream(new NetworkStream(client), false)
            };

            try
            {
                await context.SslStream.AuthenticateAsServerAsync(Server.TlsOptions);
                Server.Config.Capsules.TryGetValue(context.SslStream.TargetHostName, out context.Capsule);

                if (context.ClientCert != null)
                    Console.WriteLine($"Client Cert: {context.ClientIdentity}, Hash: {context.ClientIdentityHash} ");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{client.RemoteEndPoint} -> TLS HandShake aborted.");
                Console.WriteLine(e);
            }
            return (context.Capsule != null, context);
        }

        public static async ValueTask ReceiveHeader(GeminiCtx ctx)
        {
            var reqBuffer = new byte[MAX_URI_LENGTH_GEMINI + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.SslStream.ReadAsync(reqBuffer.AsMemory(length, 1)) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if (!ctx.Request.EndsWith("\r\n"))
                    continue;
                // ctx.Request.Replace("\r\n", "");
                ctx.RequestPath = ctx.Request;
                break;
            }

            switch (ctx.Uri.Scheme)
            {
                case "gemini":
                    ctx.IsUpload = false;
                    break;
                case "titan":
                    ctx.IsUpload = true;
                    break;
            }
        }

        public static async ValueTask POST(GeminiCtx ctx)
        {
            var (pathUri, path, mimeType, size) = ParseTitanRequest(ctx);

            var location = ctx.Capsule.GetLocation(pathUri);
            var isAllowedType = false;

            if (location == null)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.FailurePerm} {pathUri.AbsolutePath} not allowed.\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            isAllowedType = location.AllowedMimeTypes.Any(x => x.MimeType.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.MimeType.Split('/')[1] == "*" && mimeType.Split('/')[0] == x.MimeType.Split('/')[0]));

            if (!isAllowedType)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.FailurePerm} {mimeType} not allowed.\nAllowed MimeTypes:\n{string.Join('\n', location.AllowedMimeTypes.Select(x => x.MimeType))}\n\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            if (ctx.Capsule.MaxUploadSize <= size)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.FailurePerm} Max Upload Size: {ctx.Capsule.MaxUploadSize}! Your File: {size}.\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
                fileLen += await ctx.SslStream.ReadAsync(data.AsMemory(fileLen, size - fileLen));

            Console.WriteLine("Finished");
            File.WriteAllBytes(path, data);
            await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.RedirectTemp} gemini://{Path.GetDirectoryName(pathUri.AbsolutePath)}/\r\n"));
        }

        public static async ValueTask GET(GeminiCtx ctx)
        {
            var filePath = Path.Combine(ctx.Capsule.AbsoluteRootPath, ctx.Uri.AbsolutePath[1..]); // remove leading slash

            if (ctx.Uri.AbsolutePath.EndsWith('/'))
            {
                var location = ctx.Capsule.GetLocation(ctx.Uri);
                if (location != null)
                {
                    if (location.DirectoryListing)
                        ctx.DirectoryListing = true;
                    else
                        filePath = Path.Combine(filePath, ctx.Capsule.Index);
                }
                else
                {
                    var withIndex = Path.Combine(ctx.Uri.AbsolutePath, location.Index);
                    if (File.Exists(withIndex))
                        ctx.RequestPath = withIndex;
                }
            }
            var exists = File.Exists(filePath);
            ctx.RequestPath = filePath;
            ctx.RequestFileExists = exists;

            Console.WriteLine($"{ctx.Socket.RemoteEndPoint} {ctx.RequestPath} {(exists ? GeminiStatusCode.Success : GeminiStatusCode.NotFound)}");

            if (ctx.DirectoryListing)
            {
                var gmi = CreateDirectoryListing(ctx);
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.Success} text/gemini; charset=utf-8\r\n"));
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{gmi}\r\n"));
            }
            else if (File.Exists(ctx.RequestPath))
            {
                try
                {
                    var ext = Path.GetExtension(ctx.RequestPath);
                    var mimeType = GetMimeType(ext);
                    var data = await File.ReadAllBytesAsync(ctx.RequestPath);

                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.Success} {mimeType}; charset=utf-8\r\n"));
                    await ctx.SslStream.WriteAsync(data);
                }
                catch (Exception e)
                {
                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.FailurePerm} {e.Message}\r\n"));
                    Console.WriteLine(e);
                }
            }
            else
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)GeminiStatusCode.NotFound} Atlas: not found.\r\n"));
        }

        private static (Uri, string, string, int) ParseTitanRequest(GeminiCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = titanArgs[1].Split('=')[1];
            var strSizeBytes = titanArgs[2].Split('=')[1].Replace("\r\n", "");
            var sizeBytes = int.Parse(strSizeBytes);

            return (pathUri, absoluteDestinationPath, mimeType, sizeBytes);
        }

        private static string CreateDirectoryListing(GeminiCtx context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### LAST MODIFIED   |  SIZE  | NAME");
            foreach (var file in Directory.GetFiles(context.RequestPath).OrderBy(x => x))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> gemini://{context.Capsule.FQDN}/{context.RequestPath.Replace(context.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {CenterString(fi.CreationTimeUtc.ToString(), 24)} | {CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        public static void CloseConnection(GeminiCtx context)
        {
            context.SslStream.Flush();
            context.SslStream.Close();
            context.Socket.Close();
            Console.WriteLine("Closed Connection");
        }

        public static string GetMimeType(string ext)
        {
            if (!Program.ExtensionToMimeType.TryGetValue(ext, out var mimeType))
                mimeType = "text/gemini";
            return mimeType;
        }

        public static string CenterString(string txt, int length)
        {
            var delta = Math.Abs(txt.Length - length);
            for (int i = 0; i < delta; i++)
            {
                if (i % 2 == 0)
                    txt = " " + txt;
                else
                    txt += " ";
            }
            return txt;
        }
    }
}