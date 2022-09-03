namespace atlas
{
    public static class Util
    {
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