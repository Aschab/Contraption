using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class DisplayView : MonoBehaviour
    {
        private const int Segments = 5;

        private DisplayDef _def;
        private int[] _glyphs;
        private Renderer _bulb;
        private Renderer[] _cells;
        private Transform _needle;
        private float _needleAngle, _needleTarget;

        public static DisplayView Create(Transform parent, Vector3 pos, DisplayDef def, int[] glyphs)
        {
            var go = new GameObject($"{def.Kind} display");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var v = go.AddComponent<DisplayView>();
            v._def = def;
            v._glyphs = glyphs;
            v.Build();
            return v;
        }

        private void Build()
        {
            var t = transform;
            switch (_def.Kind)
            {
                case DisplayKind.Light:
                    Art.Disc(t, new Vector3(0f, 0f, -0.04f), 0.5f, 0.08f, Art.Brass);
                    _bulb = Art.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, -0.1f), Vector3.one * 0.36f, Art.Unlit).GetComponent<Renderer>();
                    break;
                case DisplayKind.Bar:
                    Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.03f), new Vector3(0.36f, 0.9f, 0.04f), Art.Charcoal);
                    _cells = new Renderer[Segments];
                    for (int i = 0; i < Segments; i++)
                        _cells[i] = Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.32f + i * 0.16f, -0.06f), new Vector3(0.24f, 0.12f, 0.03f), Art.Unlit).GetComponent<Renderer>();
                    break;
                case DisplayKind.Gauge:
                    Art.Disc(t, new Vector3(0f, 0f, -0.03f), 0.82f, 0.05f, Art.Brass);
                    Art.Disc(t, new Vector3(0f, 0f, -0.06f), 0.72f, 0.03f, Art.Cream);
                    _needle = new GameObject("Needle").transform;
                    _needle.SetParent(t, false);
                    _needle.localPosition = new Vector3(0f, -0.1f, -0.09f);
                    Art.Prim(PrimitiveType.Cube, _needle, new Vector3(0f, 0.18f, 0f), new Vector3(0.03f, 0.36f, 0.02f), new Color(0.6f, 0.1f, 0.08f));
                    break;
                case DisplayKind.Glyph:
                    Art.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.03f), new Vector3(0.75f, 0.75f, 0.04f), Art.Charcoal);
                    _cells = Glyphs.Build(t, new Vector3(0f, 0f, -0.06f), 0.18f);
                    break;
            }
        }

        public void Show(int[] state, bool instant)
        {
            int value = MachineRules.DisplayValue(_def, state);
            MachineRules.DisplayRange(_def, out int min, out int max);
            float norm = max > min ? (value - min) / (float)(max - min) : 0f;

            switch (_def.Kind)
            {
                case DisplayKind.Light:
                    bool on = MachineRules.LightOn(_def, state);
                    Art.Paint(_bulb, on ? Art.Glow : Art.Unlit, on ? Art.Glow * 2.5f : Color.black, 0.8f);
                    break;
                case DisplayKind.Bar:
                    int lit = Mathf.RoundToInt(norm * Segments);
                    for (int i = 0; i < Segments; i++)
                        Art.Paint(_cells[i], i < lit ? Art.Glow : Art.Unlit, i < lit ? Art.Glow * 1.5f : Color.black);
                    break;
                case DisplayKind.Gauge:
                    _needleTarget = Mathf.Lerp(70f, -70f, norm);
                    if (instant) _needleAngle = _needleTarget;
                    break;
                case DisplayKind.Glyph:
                    Glyphs.Paint(_cells, _glyphs[Values.Wrap(value, _glyphs.Length)], Art.Glow, Art.Unlit, 1.5f);
                    break;
            }
        }

        private void Update()
        {
            if (_needle == null) return;
            // Continuous needle with a little alien jitter so it reads as analog.
            _needleAngle = Mathf.Lerp(_needleAngle, _needleTarget, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float jitter = (Mathf.PerlinNoise(Time.time * 3f, GetInstanceID()) - 0.5f) * 2f;
            _needle.localRotation = Quaternion.Euler(0f, 0f, _needleAngle + jitter);
        }
    }
}
