using System.Text;

namespace atlas.Contexts
{
    public class SpartanCtx : AtlasCtx
    {
        public override int MaxHeaderSize {get;set;}= 1024;
        public override Uri Uri => new($"spartan://{RequestPath}");
        public override async ValueTask NotFound() => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ClientError} {Uri.AbsolutePath} not found.\r\n")).ConfigureAwait(false);
        public override async ValueTask BadRequest(string reason) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ServerError} {Uri.AbsolutePath} {reason}\r\n")).ConfigureAwait(false);
        public override async ValueTask Redirect(string target) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Redirect} spartan://{Capsule.FQDN}{target}\r\n")).ConfigureAwait(false);
        public override async ValueTask ServerError(Exception e) => await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.ServerError} {e.Message}\n{e.StackTrace}\r\n")).ConfigureAwait(false);
        public override async ValueTask Success(byte[] data, string mimeType = "text/gemini")
        {
            await Stream.WriteAsync(Encoding.UTF8.GetBytes($"{(int)SpartanStatusCode.Success} {mimeType}; charset=utf-8\r\n")).ConfigureAwait(false);
            await Stream.WriteAsync(data).ConfigureAwait(false);
        }
    }
}