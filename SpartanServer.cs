using System.Net;
using System.Net.Sockets;
using System.Text;
using atlas.Contexts;

namespace atlas
{
    public class SpartanServer : GenericServer
    {
        public async ValueTask Start()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Config.SpartanPort));
            Socket.Listen();
            while (true)
            {
                Console.WriteLine("[SPARTAN] Waiting for connection...");
                var clientSocket = await Socket.AcceptAsync().ConfigureAwait(false);

                var ctx = new SpartanCtx()
                {
                    Socket = clientSocket,
                    Stream = new NetworkStream(clientSocket)
                };

                try
                {
                    await ReceiveRequest(ctx).ConfigureAwait(false);
                    var parts = ctx.Request.Split(' ');
                    var host = parts[0];
                    var path = parts[1];
                    var size = int.Parse(parts[2]);

                    if (Program.Config.Capsules.TryGetValue(host, out var capsule))
                        ctx.Capsule = capsule;
                    ctx.Request = path;

                    Response response;
        
                    if (size > 0)
                        response = await HandleUpload(ctx).ConfigureAwait(false);
                    else
                        response = await HandleRequest(ctx).ConfigureAwait(false);

                    await ctx.Stream.WriteAsync(response.Bytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    CloseConnection(ctx);
                }
            }
        }
        public async ValueTask<Response> HandleUpload(SpartanCtx ctx)
        {
            var parts = ctx.Request.Split(' ');
            var pathUri = new Uri(parts[1]);
            var size = int.Parse(parts[2]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = Util.GetMimeType(Path.GetExtension(pathUri.AbsolutePath));

            return await UploadFile(ctx, absoluteDestinationPath, pathUri, mimeType, size).ConfigureAwait(false);
        }

        public override Response NotFound(string message) => new(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} {message}.\r\n"));
        public override Response BadRequest(string reason) => new(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ServerError} {reason}\r\n"));
        public override Response Redirect(string target) => new(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Redirect} {target}\r\n"));
        public override Response Ok(byte[] data, string mimeType = "text/gemini")
        {
            var header = Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Success} {mimeType}; charset=utf-8\r\n");
            var buffer = new byte[header.Length + data.Length];
            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(data, 0, buffer, header.Length, data.Length);
            return new Response(data);
        }
    }
}