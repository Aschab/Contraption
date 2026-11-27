using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class DisplayView : MonoBehaviour
    {
        private DisplayDef _def;
        private Look _look;
        private GlyphSet _glyphs;
        private Renderer[][] _strokes;   // rune: one set; script: one set per letter
        private Renderer _cursor;
        private bool _script;
        private Renderer _bulb;
        private Renderer[] _cells;
        private Transform _needle;
        private Quaternion _needleRest;
        private Vector3 _needleAxis, _needleMin, _needleMax;
        private bool _linear;
        private float _needleAngle, _needleTarget;

        public static DisplayView Create(MachineKit kit, Look look, Transform machine, Vector3 pos, DisplayDef def, Placement p, GlyphSet glyphs)
        {
            var go = new GameObject($"{def.Kind} display");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            var v = go.AddComponent<DisplayView>();
            v._def = def;
            v._look = look;
            v._glyphs = glyphs;

            var body = kit.Spawn(p.Style, go.transform, Vector3.zero, look).transform;
            switch (def.Kind)
            {
                case DisplayKind.Light: v._bulb = MachineKit.Find(body, "Bulb").GetComponent<Renderer>(); break;
                case DisplayKind.Bar: v._cells = Children(body, "Seg", 5); break;
                case DisplayKind.Glyph:
                    if (MachineKit.FindOptional(body, "Cell0") != null) v._cells = Children(body, "Cell", 9);
                    else v.BuildScreen(go.transform, MachineKit.Find(body, "Screen"), p);
                    break;
                case DisplayKind.Gauge:
                    v._needle = MachineKit.Find(body, "Needle");
                    v._needleRest = v._needle.localRotation;
                    v._needleAxis = Art.AxisIn(v._needle, machine, Vector3.forward);
                    var min = MachineKit.FindOptional(body, "Min");
                    var max = MachineKit.FindOptional(body, "Max");
                    v._linear = min != null && max != null;
                    if (v._linear)
                    {
                        v._needleMin = v._needle.parent.InverseTransformPoint(min.position);
                        v._needleMax = v._needle.parent.InverseTransformPoint(max.position);
                    }
                    break;
            }
            return v;
        }

        private void BuildScreen(Transform root, Transform screen, Placement p)
        {
            var c = root.InverseTransformPoint(screen.position);
            _script = p.Style.StartsWith("Disp_Script");
            if (!_script)
            {
                float size = 0.42f * p.W;
                _strokes = new[] { Glyphs.BuildStrokes(root, c, size, 0.05f * p.W, _look.Glow) };
                return;
            }
            float width = p.W * MachineView.Cell - 0.6f;
            float pitch = Mathf.Min(0.5f, width / (GlyphSet.WordLength + 1));
            float letter = Mathf.Min(0.36f, pitch * 0.7f);
            _strokes = new Renderer[GlyphSet.WordLength][];
            for (int i = 0; i < GlyphSet.WordLength; i++)
                _strokes[i] = Glyphs.BuildStrokes(root, c + new Vector3((i - GlyphSet.WordLength / 2f) * pitch, 0f, 0f), letter, 0.04f, _look.Glow);
            _cursor = Art.Prim(PrimitiveType.Cube, root, c + new Vector3(GlyphSet.WordLength / 2f * pitch, -letter / 2f, 0f),
                new Vector3(letter * 0.6f, 0.05f, 0.015f), _look.Glow).GetComponent<Renderer>();
            Art.Paint(_cursor, _look.Glow, _look.Glow * 1.5f);
        }

        private static Renderer[] Children(Transform body, string prefix, int count)
        {
            var r = new Renderer[count];
            for (int i = 0; i < count; i++) r[i] = MachineKit.Find(body, prefix + i).GetComponent<Renderer>();
            return r;
        }

        public void Show(int[] state, bool instant)
        {
            int value = MachineRules.DisplayValue(_def, state);
            MachineRules.DisplayRange(_def, out int min, out int max);
            float norm = max > min ? (value - min) / (float)(max - min) : 0f;
            Color on = _look.Glow, off = _look.Dark * 1.6f;

            switch (_def.Kind)
            {
                case DisplayKind.Light:
                    bool lit = MachineRules.LightOn(_def, state);
                    Art.Paint(_bulb, lit ? on : off, lit ? on * 2.5f : Color.black, 0.8f);
                    break;
                case DisplayKind.Bar:
                    int n = MachineRules.Readout(_def, state);
                    // Bottom segment in the scheme's second glow colour (concept t1).
                    for (int i = 0; i < _cells.Length; i++)
                    {
                        var c = i == 0 ? _look.Glow2 : on;
                        Art.Paint(_cells[i], i < n ? c : off, i < n ? c * 1.5f : Color.black);
                    }
                    break;
                case DisplayKind.Gauge:
                    _needleTarget = _linear ? norm : Mathf.Lerp(70f, -70f, norm);
                    if (instant) _needleAngle = _needleTarget;
                    break;
                case DisplayKind.Glyph:
                    int index = value - _glyphs.MinValue;
                    if (_cells != null) Glyphs.Paint(_cells, _glyphs.Dots[index], on, off, 1.5f);
                    else if (!_script) Glyphs.PaintStrokes(_strokes[0], _glyphs.Runes[index], on, 1.8f);
                    else
                        for (int i = 0; i < _strokes.Length; i++)
                            Glyphs.PaintStrokes(_strokes[i], _glyphs.Letters[_glyphs.Words[index][i]], on, 1.8f);
                    break;
            }
        }

        private void Update()
        {
            if (_cursor != null) _cursor.enabled = Time.time % 1f < 0.55f;
            if (_needle == null) return;
            // Continuous needle with a little analog jitter.
            _needleAngle = Mathf.Lerp(_needleAngle, _needleTarget, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float jitter = (Mathf.PerlinNoise(Time.time * 3f, GetInstanceID()) - 0.5f) * 2f;
            if (_linear) _needle.localPosition = Vector3.LerpUnclamped(_needleMin, _needleMax, _needleAngle + jitter * 0.01f);
            else _needle.localRotation = Quaternion.AngleAxis(_needleAngle + jitter, _needleAxis) * _needleRest;
        }
    }
}
