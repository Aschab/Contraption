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

        private static int Bits(int m)
        {
            int n = 0;
            for (; m != 0; m >>= 1) n += m & 1;
            return n;
        }
    }
}
