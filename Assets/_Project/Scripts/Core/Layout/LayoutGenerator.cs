using System;
using System.Collections.Generic;

namespace Contraption.Core
{
    // Picks a silhouette on a grid, styles for every part, and places them anywhere on the body.
    // Uses its own RNG stream so layouts never change the puzzle a seed produces.
    public static class LayoutGenerator
    {
        public static MachineLayout Generate(MachineDefinition def, ulong seed, GeneratorSettings s)
        {
            var rng = new Rng(seed ^ 0x1A70D7UL);
            int minRows = Math.Clamp(s.MinRows, 2, 5), maxRows = Math.Clamp(s.MaxRows, minRows, 5);
            double empty = Math.Clamp(s.EmptyCells, 0.0, 0.7);

            for (int attempt = 0; attempt < 60; attempt++)
            {
                int rows = rng.RangeInclusive(minRows, maxRows);
                var controls = new List<PartStyle>();
                var displays = new List<PartStyle>();
                foreach (var c in def.Controls) controls.Add(PickStyle(rng, PartStyles.For(c.Kind), rows, s.LargePartChance));
                foreach (var d in def.Displays) displays.Add(PickStyle(rng, PartStyles.For(d.Kind), rows, s.LargePartChance));

                int area = 0, widest = 1;
                foreach (var p in controls) { area += p.W * p.H; widest = Math.Max(widest, p.W); }
                foreach (var p in displays) { area += p.W * p.H; widest = Math.Max(widest, p.W); }
                int target = (int)Math.Ceiling(area / (1.0 - empty));
                // Keep machines from getting much wider than tall.
                while (rows < maxRows && (target + rows - 1) / rows > rows * 2 + 1) rows++;
                int cols = Math.Max(Math.Max(3, def.InputPorts + 1), Math.Max(widest, (target + rows - 1) / rows));

                for (int grow = 0; grow < 6; grow++, cols++)
                {
                    var layout = Shape(rng, cols, rows);
                    if (layout == null || BodyCount(layout) < target) continue;
                    if (!Place(rng, layout, layout.Controls, controls) || !Place(rng, layout, layout.Displays, displays)) continue;
                    Decorate(rng, layout, def.InputPorts);
                    return layout;
                }
            }
            return Fallback(rng, def);
        }

        private static PartStyle PickStyle(Rng rng, IReadOnlyList<PartStyle> styles, int rows, double largeChance)
        {
            var large = new List<PartStyle>();
            var single = new List<PartStyle>();
            foreach (var s in styles)
                if (s.IsSingle) single.Add(s);
                else if (s.H <= rows) large.Add(s);
            return large.Count > 0 && (single.Count == 0 || rng.Chance(largeChance)) ? rng.Pick(large) : rng.Pick(single);
        }

        private static MachineLayout Shape(Rng rng, int cols, int rows)
        {
            var l = new MachineLayout { Columns = cols, Rows = rows, Body = new bool[cols * rows] };
            for (int i = 0; i < l.Body.Length; i++) l.Body[i] = true;

            switch (rng.Range(0, 5))
            {
                case 1: // step down on the left
                case 2: // step down on the right
                {
                    bool left = rng.Chance(0.5);
                    int k = rng.RangeInclusive(1, Math.Max(1, cols / 2));
                    int drop = rng.RangeInclusive(1, rows - 1);
                    for (int i = 0; i < k; i++) Lower(l, left ? i : cols - 1 - i, drop);
                    break;
                }
                case 3: // central tower
                {
                    if (cols < 3) break;
                    int w = rng.RangeInclusive(1, Math.Max(1, cols - 2));
                    int start = rng.Range(1, cols - w);
                    for (int x = 0; x < cols; x++) if (x < start || x >= start + w) Lower(l, x, 1);
                    break;
                }
                case 4: // both shoulders lowered
                    Lower(l, 0, 1);
                    Lower(l, cols - 1, 1);
                    break;
            }
            if (rows >= 3 && cols >= 5 && rng.Chance(0.25))
            {
                // Arch: the machine stands on two legs.
                int a = rng.RangeInclusive(1, cols / 2 - 1), b = rng.RangeInclusive(cols / 2, cols - 2);
                for (int x = a; x <= b; x++) l.Body[x] = false;
            }
            if (rows >= 3 && cols >= 4 && rng.Chance(0.25))
            {
                int x = rng.Range(1, cols - 1), top = l.TopOf(x);
                if (top >= 2) l.Body[top * cols + x] = false;
            }
            return Valid(l) ? l : null;
        }

        private static void Lower(MachineLayout l, int x, int drop)
        {
            for (int y = l.Rows - drop; y < l.Rows; y++) l.Body[y * l.Columns + x] = false;
        }

        // Rejects diagonal-only corners (no tile for them) and disconnected bodies.
        private static bool Valid(MachineLayout l)
        {
            for (int j = 0; j <= l.Rows; j++)
                for (int i = 0; i <= l.Columns; i++)
                {
                    bool bl = l.IsBody(i - 1, j - 1), br = l.IsBody(i, j - 1), tl = l.IsBody(i - 1, j), tr = l.IsBody(i, j);
                    if ((bl && tr && !br && !tl) || (br && tl && !bl && !tr)) return false;
                }
            for (int x = 0; x < l.Columns; x++) if (l.TopOf(x) < 0) return false;

            var seen = new bool[l.Body.Length];
            var stack = new Stack<int>();
            int start = Array.IndexOf(l.Body, true);
            stack.Push(start);
            seen[start] = true;
            int count = 0;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                count++;
                int x = c % l.Columns, y = c / l.Columns;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!l.IsBody(nx, ny) || seen[ny * l.Columns + nx]) continue;
                    seen[ny * l.Columns + nx] = true;
                    stack.Push(ny * l.Columns + nx);
                }
            }
            return count == BodyCount(l);
        }

        private static int BodyCount(MachineLayout l)
        {
            int n = 0;
            foreach (bool b in l.Body) if (b) n++;
            return n;
        }

        // Places the biggest parts first, each at a random free spot.
        private static bool Place(Rng rng, MachineLayout l, List<Placement> target, List<PartStyle> styles)
        {
            var order = new List<int>();
            for (int i = 0; i < styles.Count; i++) { order.Add(i); target.Add(null); }
            order.Sort((a, b) => styles[b].W * styles[b].H != styles[a].W * styles[a].H
                ? styles[b].W * styles[b].H - styles[a].W * styles[a].H : a - b);
            foreach (int i in order)
            {
                var s = styles[i];
                var spots = FreeSpots(l, s.W, s.H, null);
                if (spots.Count == 0) return false;
                var (x, y) = rng.Pick(spots);
                target[i] = new Placement { Style = s.Name, X = x, Y = y, W = s.W, H = s.H };
            }
            return true;
        }

        public static List<(int x, int y)> FreeSpots(MachineLayout l, int w, int h, Placement ignore)
        {
            var spots = new List<(int, int)>();
            for (int y = 0; y + h <= l.Rows; y++)
                for (int x = 0; x + w <= l.Columns; x++)
                    if (l.Fits(x, y, w, h, ignore)) spots.Add((x, y));
            return spots;
        }

        private static void Decorate(Rng rng, MachineLayout l, int ports)
        {
            var cols = new List<int>();
            for (int x = 0; x < l.Columns; x++) cols.Add(x);
            rng.Shuffle(cols);
            cols = cols.GetRange(0, ports);
            cols.Sort();
            bool matching = rng.Chance(0.7);
            string style = rng.Pick(PartStyles.InputContainers);
            foreach (int c in cols)
                l.Inputs.Add(new Container { Column = c, Style = matching ? style : rng.Pick(PartStyles.InputContainers) });
            l.Output = new Container { Column = l.Columns, Style = rng.Pick(PartStyles.OutputContainers) };
            l.TopStyle = rng.Pick(PartStyles.TopStyles);
            l.BaseStyle = rng.Pick(PartStyles.BaseStyles);
            l.DecorSeed = rng.NextULong();
            l.TestStyle = rng.Pick(PartStyles.TestTriggers);
            FillEmpty(l);
        }

        // Covers every free body cell with a decorative panel; existing fillers are kept.
        public static void FillEmpty(MachineLayout l)
        {
            var rng = new Rng(l.DecorSeed ^ (ulong)l.Fillers.Count * 0x9E37UL);
            for (int y = 0; y < l.Rows; y++)
                for (int x = 0; x < l.Columns; x++)
                {
                    if (!l.Fits(x, y, 1, 1)) continue;
                    bool big = l.Fits(x, y, 2, 2) && rng.Chance(0.35);
                    l.Fillers.Add(big
                        ? new Placement { Style = rng.Pick(PartStyles.BigFillers), X = x, Y = y, W = 2, H = 2 }
                        : new Placement { Style = rng.Pick(PartStyles.Fillers), X = x, Y = y });
                }
        }

        // Plain rectangle of 1x1 parts; always fits.
        private static MachineLayout Fallback(Rng rng, MachineDefinition def)
        {
            int parts = def.Controls.Count + def.Displays.Count, rows = 3;
            int cols = Math.Max(def.InputPorts + 1, (parts + rows - 1) / rows + 1);
            var l = new MachineLayout { Columns = cols, Rows = rows, Body = new bool[cols * rows] };
            for (int i = 0; i < l.Body.Length; i++) l.Body[i] = true;
            int n = 0;
            foreach (var c in def.Controls) l.Controls.Add(Single(PartStyles.For(c.Kind)[0], n++, cols));
            foreach (var d in def.Displays) l.Displays.Add(Single(PartStyles.For(d.Kind)[0], n++, cols));
            Decorate(rng, l, def.InputPorts);
            return l;
        }

        private static Placement Single(PartStyle s, int n, int cols) =>
            new() { Style = s.Name, X = n % cols, Y = n / cols };
    }
}
