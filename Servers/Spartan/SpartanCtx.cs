using System.Text;

namespace atlas.Servers.Spartan
{
    public class SpartanCtx : Context
    {
        public override int MaxHeaderSize {get;set;}= 1024;
    }
}