namespace Contraption.Core
{
    public static class Values
    {
        public const int Min = 0;
        public const int Max = 4;
        public const int Levels = Max + 1;

        public static int Clamp(int v) => v < Min ? Min : v > Max ? Max : v;

        public static int Wrap(int v, int levels)
        {
            int m = v % levels;
            return m < 0 ? m + levels : m;
        }
    }
}
