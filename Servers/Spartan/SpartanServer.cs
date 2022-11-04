using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using atlas.Data;

namespace atlas.Servers.Spartan
{
    public class SpartanServer
    {
        public Socket Socket { get; set; }

        public SpartanServer()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, Program.Cfg.SpartanPort));
            Socket.Listen();
        }

        public async void Start()
        {
            while (true)
            {
                Console.WriteLine("[Spartan] Waiting for connection...");
                var socket = await Socket.AcceptAsync().ConfigureAwait(false);
                var task = Task.Run(async () =>
                {
                    var ctx = new SpartanCtx(socket);
                    await ProcessSocket(ctx).ConfigureAwait(false);
                });
            }
        }

        private static async ValueTask ProcessSocket(SpartanCtx ctx)
        {
            try
            {
                await ReadRequest(ctx);

                if (!Uri.IsWellFormedUriString(ctx.Request, UriKind.Absolute))
                {
                    Program.Log(ctx, $"Uri Invalid ({ctx.Request})");
                    ctx.Writer.Write(Response.BadRequest("invalid request").Data.Span);
                    return;
                }

                ctx.Uri = new Uri(ctx.Request);

                var response = ctx.PayloadSize > 0 ? await ProcessUploadRequest(ctx).ConfigureAwait(false) : await DownloadProcessor.Process(ctx).ConfigureAwait(false);
                Statistics.AddResponse(response);
                ctx.Writer.Write(response.Data.Span);
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { CloseConnection(ctx); }
        }

        private static async ValueTask ReadRequest(SpartanCtx ctx)
        {
            var req = await ctx.Reader.ReadLineAsync().ConfigureAwait(false);

            var parts = req.Trim().Split(' ');
            var host = parts[0];
            var path = parts[1];
            ctx.PayloadSize = int.Parse(parts[2]);

            if (Program.Cfg.Capsules.TryGetValue(host, out var capsule))
                ctx.Capsule = capsule;
            ctx.Request = $"spartan://{host}{path}";
        }

        public static async ValueTask<Response> ProcessUploadRequest(SpartanCtx ctx)
        {
            var absoluteDestinationPath = Path.Combine(ctx.Capsule.AbsoluteRootPath, ctx.Uri.AbsolutePath[1..]);
            var mimeType = MimeMap.GetMimeType(Path.GetExtension(ctx.Uri.AbsolutePath));

            return await DownloadProcessor.UploadFile(ctx, absoluteDestinationPath, ctx.Uri, mimeType, ctx.PayloadSize).ConfigureAwait(false);
        }

        public static void CloseConnection(Context ctx)
        {
            Program.Log(ctx, "complete");
            ctx?.Reader?.Close();
            ctx?.Reader?.Dispose();
            ctx?.Socket?.Close();
            ctx?.Socket?.Dispose();
        }
    }
}