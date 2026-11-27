using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Draws flat ink/paint shapes on a book page. Page space: x right, y toward the page top, metres; origin = page centre.
    public sealed class PageDrawer
    {
        public static readonly Color Ink = new(0.2f, 0.14f, 0.1f);
        public static readonly Color Fill = new(0.22f, 0.72f, 0.66f);
        public static readonly Color Mismatch = new(0.96f, 0.64f, 0.18f);
        public static readonly Color Seal = new(0.62f, 0.1f, 0.08f);

        // Alien symbol for each liquid trait (Color, Viscosity, Temperature, Flammability, Refraction); the same in every machine.
        public static readonly int[] TraitSymbols = Glyphs.MakeRunes(0x7A175EEDUL, Liquid.PropertyCount, 3, 5);

        private readonly Transform _root;
        public Color Paper { get; }

        public PageDrawer(Transform root, Color paper)
        {
            _root = root;
            Paper = paper;
        }

        public void Clear()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Object.Destroy(_root.GetChild(i).gameObject);
        }

        public GameObject Rect(Vector2 center, Vector2 size, Color color, int layer = 1)
        {
            var go = Art.Prim(PrimitiveType.Cube, _root, new Vector3(center.x, center.y, -0.0008f * layer), new Vector3(size.x, size.y, 0.001f), color);
            Art.Paint(go.GetComponent<Renderer>(), color, Color.black, 0.1f);
            return go;
        }

        public void Line(Vector2 a, Vector2 b, float width, Color color, int layer = 1)
        {
            var go = Rect((a + b) / 2f, new Vector2(Vector2.Distance(a, b) + width, width), color, layer);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        public void Ring(Vector2 c, float r, float width, Color color, int segments = 14, int layer = 1)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Line(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, width, color, layer);
            }
        }

        public void Disc(Vector2 c, float r, Color color, int layer = 1)
        {
            var go = Art.Prim(PrimitiveType.Cylinder, _root, new Vector3(c.x, c.y, -0.0008f * layer), new Vector3(r * 2f, 0.0005f, r * 2f), color);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Art.Paint(go.GetComponent<Renderer>(), color, Color.black, 0.1f);
        }

        // A value 0-4 as a 2x2 grid: boxes fill top-left, top-right, bottom-left, bottom-right.
        public void Grid(Vector2 c, float size, int value, Color fill)
        {
            Rect(c, new Vector2(size, size), Ink, 1);
            float box = size * 0.42f, off = size * 0.235f;
            for (int i = 0; i < 4; i++)
            {
                var p = c + new Vector2(i % 2 == 0 ? -off : off, i < 2 ? off : -off);
                Rect(p, new Vector2(box, box), i < value ? fill : Paper, 2);
            }
        }

        // Line-drawn symbol on the 3x3 lattice used by Glyphs.
        public void Symbol(Vector2 c, float size, int mask, Color color, float width = 0.004f)
        {
            for (int i = 0; i < Glyphs.Segments.Length; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var (a, b) = Glyphs.Segments[i];
                Line(c + Lattice(a, size), c + Lattice(b, size), width, color);
            }
        }

        private static Vector2 Lattice(int p, float size) => new((p % 3 - 1) * size / 2f, (1 - p / 3) * size / 2f);

        // Sample tag symbols: simple and unmistakable, one per tube in the input pool (also drawn on the tube tags).
        public const int SampleSymbolCount = 8;

        public void SampleSymbol(Vector2 c, float s, int index, Color color, float w = 0.0035f, int layer = 1)
        {
            float h = s / 2f;
            switch (index % SampleSymbolCount)
            {
                case 0: // triangle
                    Line(c + new Vector2(-h, -h * 0.8f), c + new Vector2(h, -h * 0.8f), w, color, layer);
                    Line(c + new Vector2(h, -h * 0.8f), c + new Vector2(0f, h), w, color, layer);
                    Line(c + new Vector2(0f, h), c + new Vector2(-h, -h * 0.8f), w, color, layer);
                    break;
                case 1: Ring(c, h, w, color, 12, layer); break;
                case 2: // square
                    Line(c + new Vector2(-h, -h), c + new Vector2(h, -h), w, color, layer);
                    Line(c + new Vector2(h, -h), c + new Vector2(h, h), w, color, layer);
                    Line(c + new Vector2(h, h), c + new Vector2(-h, h), w, color, layer);
                    Line(c + new Vector2(-h, h), c + new Vector2(-h, -h), w, color, layer);
                    break;
                case 3: // X
                    Line(c + new Vector2(-h, -h), c + new Vector2(h, h), w, color, layer);
                    Line(c + new Vector2(-h, h), c + new Vector2(h, -h), w, color, layer);
                    break;
                case 4: // +
                    Line(c + new Vector2(-h, 0f), c + new Vector2(h, 0f), w, color, layer);
                    Line(c + new Vector2(0f, -h), c + new Vector2(0f, h), w, color, layer);
                    break;
                case 5: // diamond with a dot
                    Line(c + new Vector2(0f, -h), c + new Vector2(h, 0f), w, color, layer);
                    Line(c + new Vector2(h, 0f), c + new Vector2(0f, h), w, color, layer);
                    Line(c + new Vector2(0f, h), c + new Vector2(-h, 0f), w, color, layer);
                    Line(c + new Vector2(-h, 0f), c + new Vector2(0f, -h), w, color, layer);
                    Rect(c, new Vector2(w * 2f, w * 2f), color, layer + 1);
                    break;
                case 6: // two bars
                    Line(c + new Vector2(-h, h * 0.45f), c + new Vector2(h, h * 0.45f), w * 1.3f, color, layer);
                    Line(c + new Vector2(-h, -h * 0.45f), c + new Vector2(h, -h * 0.45f), w * 1.3f, color, layer);
                    break;
                default: // three dots
                    foreach (var p in new[] { new Vector2(0f, h * 0.7f), new Vector2(-h * 0.7f, -h * 0.5f), new Vector2(h * 0.7f, -h * 0.5f) })
                        Disc(c + p, w * 1.4f, color, layer);
                    break;
            }
        }

        // An input liquid: a cog in the liquid's colour carrying its sample tag symbol.
        public void LiquidIcon(Vector2 c, float r, Liquid l, int poolIndex)
        {
            const int teeth = 8;
            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth;
                var t = Rect(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, new Vector2(r * 0.45f, r * 0.45f), Ink);
                t.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            Disc(c, r, Ink, 1);
            Disc(c, r * 0.85f, Art.LiquidColor(l.Color), 2);
            Disc(c, r * 0.62f, Paper, 3);
            SampleSymbol(c, r * 0.8f, poolIndex, Ink, r * 0.12f, 4);
        }

        // "→◯" (goes in) and "◯→" (comes out).
        public void FlowMark(Vector2 c, float s, bool into)
        {
            var ring = c + new Vector2(into ? s * 0.35f : -s * 0.35f, 0f);
            Ring(ring, s * 0.3f, 0.003f, Ink, 10);
            float ax = into ? -s * 0.55f : s * 0.05f;
            Arrow(new Vector2(c.x + ax, c.y), new Vector2(c.x + ax + s * 0.5f, c.y), 0.003f);
        }

        public void Arrow(Vector2 from, Vector2 to, float width)
        {
            Line(from, to, width, Ink);
            var dir = (to - from).normalized;
            var side = new Vector2(-dir.y, dir.x);
            float head = Mathf.Max(0.01f, width * 4f);
            Line(to, to - dir * head + side * head * 0.7f, width, Ink);
            Line(to, to - dir * head - side * head * 0.7f, width, Ink);
        }
    }
}
