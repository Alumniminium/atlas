using System.Net;
using System.Net.Sockets;
using atlas.Data;

namespace atlas.Servers.Spartan
{
    public class SpartanServer : GenericServer
    {
        public SpartanServer()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Config.SpartanPort));
            Socket.Listen();
        }
        
        public async void Start()
        {
            while (true)
            {
                Console.WriteLine("[SPARTAN] Waiting for connection...");
                var socket = await Socket.AcceptAsync().ConfigureAwait(false);

                var task = Task.Run(async () => await ProcessSocket(socket).ConfigureAwait(false));
            }
        }

        private static async ValueTask ProcessSocket(Socket clientSocket)
        {
            var ctx = new SpartanCtx()
            {
                Socket = clientSocket,
                Stream = new NetworkStream(clientSocket)
            };
            Response response;

            try
            {
                await ReceiveRequest(ctx).ConfigureAwait(false);
                int size = ParseRequest(ctx);

                if (!Uri.IsWellFormedUriString(ctx.Request, UriKind.Absolute))
                {
                    await ctx.Stream.WriteAsync(Response.BadRequest("invalid request"));
                    return;
                }

                ctx.Uri = new Uri(ctx.Request);

                if (size > 0)
                    response = await ProcessUploadRequest(ctx).ConfigureAwait(false);
                else
                    response = await ProcessGetRequest(ctx).ConfigureAwait(false);

                await ctx.Stream.WriteAsync(response);
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { CloseConnection(ctx); }
        }

        private static int ParseRequest(SpartanCtx ctx)
        {
            var parts = ctx.Request.Split(' ');
            var host = parts[0];
            var path = parts[1];
            var size = int.Parse(parts[2]);

            if (Program.Config.Capsules.TryGetValue(host, out var capsule))
                ctx.Capsule = capsule;
            ctx.Request = $"spartan://{host}{path}";

            return size;
        }

        public static async ValueTask<Response> ProcessUploadRequest(SpartanCtx ctx)
        {
            var parts = ctx.Request.Split(' ');
            var pathUri = new Uri(parts[1]);
            var size = int.Parse(parts[2]);
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, pathUri.AbsolutePath[1..]);
            var mimeType = MimeMap.GetMimeType(Path.GetExtension(pathUri.AbsolutePath));

            return await UploadFile(ctx, absoluteDestinationPath, pathUri, mimeType, size).ConfigureAwait(false);
        }
    }
}