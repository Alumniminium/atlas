using System.Text;

namespace atlas.Contexts
{
    public class SpartanCtx : AtlasCtx
    {
        public override int MaxHeaderSize {get;set;}= 1024;
        public override Uri Uri => new($"spartan://{Request}");
    }
}