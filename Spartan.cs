using System.Text;

namespace atlas
{
    internal class Spartan
    {
        public const int MAX_URI_LENGTH_SPARTAN = 1024;

        public static async ValueTask ReceiveHeader(SpartanCtx ctx)
        {
            var reqBuffer = new byte[MAX_URI_LENGTH_SPARTAN + 2]; // +2 for \r\n
            var length = 0;
            while (await ctx.SslStream.ReadAsync(reqBuffer.AsMemory(length, 1)) == 1)
            {
                ctx.Request += Encoding.UTF8.GetString(reqBuffer, length, 1);
                if (!ctx.Request.EndsWith("\r\n"))
                    continue;

                ctx.RequestPath = ctx.Request;
                break;
            }
            var parts = ctx.Request.Split(' ');
            var host = parts[0];
            var path = parts[1];
            var size = int.Parse(parts[2]);
            ctx.IsUpload = size > 0;

            if(Server.Config.Capsules.TryGetValue(host, out var capsule))
                ctx.Capsule = capsule;
            ctx.RequestPath = path;
        }

        public static async ValueTask POST(SpartanCtx ctx)
        {
            var parts = ctx.Request.Split(' ');
            var host = parts[0];
            var pathUri = new Uri(parts[1]);
            var size = int.Parse(parts[2]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);

            var location = ctx.Capsule.GetLocation(pathUri);
            var isAllowedType = false;
            var mimeType = GetMimeType(Path.GetExtension(pathUri.AbsolutePath));
            var filename = Path.GetFileName(parts[1]);

            if(string.IsNullOrEmpty(filename))
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} Missing Filename\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

             if (location == null)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} {pathUri.AbsolutePath} not allowed.\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            isAllowedType = location.AllowedMimeTypes.Any(x => x.MimeType.ToLowerInvariant() == mimeType.ToLowerInvariant() || (x.MimeType.Split('/')[1] == "*" && mimeType.Split('/')[0] == x.MimeType.Split('/')[0]));

            if (!isAllowedType)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} {mimeType} not allowed.\nAllowed MimeTypes:\n{string.Join('\n', location.AllowedMimeTypes.Select(x => x.MimeType))}\n\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            if (ctx.Capsule.MaxUploadSize <= size)
            {
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ServerError} Max Upload Size: {ctx.Capsule.MaxUploadSize}! Your File: {size}.\r\n"));
                await ctx.SslStream.FlushAsync();
                return;
            }

            var data = new byte[size];
            var fileLen = 0;
            while (fileLen != size)
                fileLen += await ctx.SslStream.ReadAsync(data.AsMemory(fileLen, size - fileLen));

            Console.WriteLine("Finished");
            File.WriteAllBytes(absoluteDestinationPath, data);
            await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Redirect} spartan://{Path.GetDirectoryName(pathUri.AbsolutePath)}/\r\n"));
        }
        public static async ValueTask GET(SpartanCtx ctx)
        {
            var filePath = Path.Combine(ctx.Capsule.AbsoluteRootPath, ctx.Uri.AbsolutePath[1..]); // remove leading slash
            if( ctx.Uri.AbsolutePath.EndsWith("/upload"))
            {

            }
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

            Console.WriteLine($"{ctx.Socket.RemoteEndPoint} {ctx.RequestPath} {(exists ? SpartanStatusCode.Success : SpartanStatusCode.ClientError)}");

            if (ctx.DirectoryListing)
            {
                var gmi = CreateDirectoryListing(ctx);
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Success} text/gemini; charset=utf-8\r\n"));
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{gmi}\r\n"));
            }
            else if (File.Exists(ctx.RequestPath))
            {
                try
                {
                    var ext = Path.GetExtension(ctx.RequestPath);
                    var mimeType = GetMimeType(ext);
                    var data = await File.ReadAllBytesAsync(ctx.RequestPath);

                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Success} {mimeType}; charset=utf-8\r\n"));
                    await ctx.SslStream.WriteAsync(data);
                }
                catch (Exception e)
                {
                    await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ServerError} {e.Message}\r\n"));
                    Console.WriteLine(e);
                }
            }
            else
                await ctx.SslStream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} Atlas: not found.\r\n"));
       
        }

        public static (Uri, string, string, int) ParseTitanRequest(SpartanCtx ctx)
        {
            var titanArgs = ctx.Request.Split(';');
            var pathUri = new Uri(titanArgs[0]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = titanArgs[1].Split('=')[1];
            var strSizeBytes = titanArgs[2].Split('=')[1].Replace("\r\n", "");
            var sizeBytes = int.Parse(strSizeBytes);

            return (pathUri, absoluteDestinationPath, mimeType, sizeBytes);
        }

        private static string CreateDirectoryListing(SpartanCtx context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### LAST MODIFIED   |  SIZE  | NAME");    
            sb.AppendLine($"=: spartan://{context.Capsule.FQDN}/files/create?image.png create new file");
            foreach (var file in Directory.GetFiles(context.RequestPath).OrderBy(x => x))
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"=> spartan://{context.Capsule.FQDN}/{context.RequestPath.Replace(context.Capsule.AbsoluteRootPath, "")}/{Path.GetFileName(file)} {CenterString(fi.CreationTimeUtc.ToString(), 24)} | {CenterString($"{fi.Length / 1024 / 1024f:0.00}mb", 10)} | {Path.GetFileName(file)}");
            }

            return sb.ToString();
        }

        public static void CloseConnection(SpartanCtx context)
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