using System.Text;

namespace atlas.Servers.Spartan
{
    public class SpartanCtx : Context
    {
        public SpartanCtx() => MaxHeaderSize = 1024;
    }
}