using System.Collections.Generic;

namespace Contraption.Core
{
    // Lab edits on a saved layout. Parts may cover fillers; freed cells get new fillers.
    public static class LayoutEditor
    {
        public static bool TryMove(MachineLayout l, Placement p, int dx, int dy) => TrySet(l, p, p.Style, p.X + dx, p.Y + dy, p.W, p.H);

        public static bool TryRestyle(MachineLayout l, Placement p, IReadOnlyList<PartStyle> styles, int dir)
        {
            int current = 0;
            for (int i = 0; i < styles.Count; i++) if (styles[i].Name == p.Style) current = i;
            for (int step = 1; step < styles.Count; step++)
            {
                var s = styles[Values.Wrap(current + dir * step, styles.Count)];
                // Keep the part's corner if possible, otherwise the nearest spot that fits.
                if (TrySet(l, p, s.Name, p.X, p.Y, s.W, s.H)) return true;
                foreach (var (x, y) in NearestFirst(l, p, s))
                    if (TrySet(l, p, s.Name, x, y, s.W, s.H)) return true;
            }
            return false;
        }

        private static IEnumerable<(int, int)> NearestFirst(MachineLayout l, Placement p, PartStyle s)
        {
            var spots = new List<(int x, int y)>();
            for (int y = 0; y + s.H <= l.Rows; y++)
                for (int x = 0; x + s.W <= l.Columns; x++)
                    if (l.Fits(x, y, s.W, s.H, p, true)) spots.Add((x, y));
            spots.Sort((a, b) => Dist(a, p).CompareTo(Dist(b, p)));
            foreach (var spot in spots) yield return spot;
        }

        private static int Dist((int x, int y) a, Placement p) => System.Math.Abs(a.x - p.X) + System.Math.Abs(a.y - p.Y);

        private static bool TrySet(MachineLayout l, Placement p, string style, int x, int y, int w, int h)
        {
            if (!l.Fits(x, y, w, h, p, true)) return false;
            l.Fillers.RemoveAll(f => f.Overlaps(x, y, w, h));
            p.Style = style;
            p.X = x; p.Y = y; p.W = w; p.H = h;
            LayoutGenerator.FillEmpty(l);
            return true;
        }

        public static void Reroll(MachineLayout l, ulong decorSeed)
        {
            l.DecorSeed = decorSeed;
            l.Fillers.Clear();
            LayoutGenerator.FillEmpty(l);
            LayoutGenerator.Sculpt(l);
        }
    }
}
