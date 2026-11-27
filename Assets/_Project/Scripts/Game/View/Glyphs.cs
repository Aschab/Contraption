using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Alien glyphs as 3x3 dot masks, random per machine (no consistent alphabet by design).
    public static class Glyphs
    {
        public static int[] Make(ulong seed, int count)
        {
            var rng = new Rng(seed ^ 0x5EEDF00DUL);
            var list = new List<int>();
            while (list.Count < count)
            {
                int mask = (int)(rng.NextULong() & 0x1FF);
                int bits = Bits(mask);
                if (bits < 3 || bits > 6 || list.Contains(mask)) continue;
                list.Add(mask);
            }
            return list.ToArray();
        }

        public static Renderer[] Build(Transform parent, Vector3 center, float spacing, Color color)
        {
            var cells = new Renderer[9];
            for (int i = 0; i < 9; i++)
            {
                var pos = center + new Vector3((i % 3 - 1) * spacing, (1 - i / 3) * spacing, 0f);
                var size = spacing * 0.75f;
                cells[i] = Art.Prim(PrimitiveType.Cube, parent, pos, new Vector3(size, size, 0.02f), color).GetComponent<Renderer>();
            }
            return cells;
        }

        public static void Paint(Renderer[] cells, int mask, Color on, Color off, float emission)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                bool lit = (mask & (1 << i)) != 0;
                Art.Paint(cells[i], lit ? on : off, lit ? on * emission : Color.black);
            }
        }

        // Line-drawn symbols: segments between points of a 3x3 lattice (orthogonal neighbours and square diagonals).
        public static readonly (int a, int b)[] Segments = BuildSegments();

        private static (int, int)[] BuildSegments()
        {
            var list = new List<(int, int)>();
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int p = y * 3 + x;
                    if (x < 2) list.Add((p, p + 1));
                    if (y < 2) list.Add((p, p + 3));
                    if (x < 2 && y < 2) { list.Add((p, p + 4)); list.Add((p + 1, p + 3)); }
                }
            return list.ToArray();
        }

        // Unique connected stroke masks (bit i = Segments[i]).
        public static int[] MakeRunes(ulong seed, int count, int minStrokes, int maxStrokes)
        {
            var rng = new Rng(seed);
            var list = new List<int>();
            for (int guard = 0; list.Count < count && guard < 100_000; guard++)
            {
                int target = rng.RangeInclusive(minStrokes, maxStrokes);
                int mask = 1 << rng.Range(0, Segments.Length);
                for (int tries = 0; Bits(mask) < target && tries < 50; tries++)
                {
                    int s = rng.Range(0, Segments.Length);
                    if ((mask & (1 << s)) == 0 && Touches(mask, s)) mask |= 1 << s;
                }
                if (!list.Contains(mask)) list.Add(mask);
            }
            return list.ToArray();
        }

        private static bool Touches(int mask, int s)
        {
            var (a, b) = Segments[s];
            for (int i = 0; i < Segments.Length; i++)
                if ((mask & (1 << i)) != 0 && (Segments[i].a == a || Segments[i].a == b || Segments[i].b == a || Segments[i].b == b)) return true;
            return false;
        }

        // One renderer per lattice segment, fitted in a size×size square centred on `center`.
        public static Renderer[] BuildStrokes(Transform parent, Vector3 center, float size, float thickness, Color color)
        {
            var r = new Renderer[Segments.Length];
            for (int i = 0; i < Segments.Length; i++)
            {
                var (a, b) = Segments[i];
                Vector3 pa = Lattice(a, center, size), pb = Lattice(b, center, size);
                var go = Art.Prim(PrimitiveType.Cube, parent, (pa + pb) / 2f, new Vector3(Vector3.Distance(pa, pb) + thickness, thickness, 0.015f), color);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
                r[i] = go.GetComponent<Renderer>();
            }
            return r;
        }

        private static Vector3 Lattice(int p, Vector3 center, float size) =>
            center + new Vector3((p % 3 - 1) * size / 2f, (1 - p / 3) * size / 2f, 0f);

        public static void PaintStrokes(Renderer[] strokes, int mask, Color on, float emission)
        {
            for (int i = 0; i < strokes.Length; i++)
            {
                bool lit = (mask & (1 << i)) != 0;
                strokes[i].enabled = lit;
                if (lit) Art.Paint(strokes[i], on, on * emission);
            }
        }

        public static int Bits(int m)
        {
            int n = 0;
            for (; m != 0; m >>= 1) n += m & 1;
            return n;
        }
    }
}
