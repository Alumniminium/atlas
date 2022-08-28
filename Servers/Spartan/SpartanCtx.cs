using System.Text;

namespace atlas.Servers.Spartan
{
    public class SpartanCtx : AtlasCtx
    {
        public override int MaxHeaderSize {get;set;}= 1024;
    }
}