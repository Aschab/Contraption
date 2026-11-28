using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class DisplayView : MonoBehaviour
    {
        // Every display model has a recessed label slot along its bottom edge ("Label" empty) and its instrument
        // shrunk into the panel above. Keep in sync with LABEL_H / LABEL_GAP / label_scale in build_kit.py.
        public const float LabelSlot = 0.24f, LabelGap = 0.03f;

        public static float ContentScale(int h)
        {
            float panel = h * MachineView.Cell - 0.14f;
            return (panel - 2f * LabelGap - LabelSlot) / panel;
        }

        private Placement _placement;
        private Transform _label;
        private GameObject _focusFrame;
        private readonly List<Renderer> _labelIcons = new();
        private bool _focused;
        private float _glow;
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
        private int _shown = int.MinValue;
        private int _variant;

        public static DisplayView Create(MachineKit kit, Look look, Transform machine, Vector3 pos, DisplayDef def, Placement p, GlyphSet glyphs, int variant)
        {
            var go = new GameObject($"{def.Kind} display");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            // Displays take focus too (hover / arrows), so they can be magnified.
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(p.W * MachineView.Cell - 0.1f, p.H * MachineView.Cell - 0.1f, 0.4f);
            box.center = new Vector3(0f, 0f, -0.2f);
            var v = go.AddComponent<DisplayView>();
            v._def = def;
            v._look = look;
            v._glyphs = glyphs;
            v._placement = p;
            v._variant = variant;

            var body = kit.Spawn(p.Style, go.transform, Vector3.zero, look).transform;
            v._label = MachineKit.FindOptional(body, "Label");
            v._focusFrame = FocusFrame(go.transform, p.W * MachineView.Cell - 0.06f, p.H * MachineView.Cell - 0.06f, look.Glow);
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
            float k = ContentScale(p.H);
            if (!_script)
            {
                float size = 0.42f * p.W * k;
                _strokes = new[] { Glyphs.BuildStrokes(root, c, size, 0.05f * p.W * k, _look.Glow) };
                return;
            }
            float width = (p.W * MachineView.Cell - 0.6f) * k;
            float pitch = Mathf.Min(0.5f * k, width / (GlyphSet.WordLength + 1));
            float letter = Mathf.Min(0.36f * k, pitch * 0.7f);
            _strokes = new Renderer[GlyphSet.WordLength][];
            for (int i = 0; i < GlyphSet.WordLength; i++)
                _strokes[i] = Glyphs.BuildStrokes(root, c + new Vector3((i - GlyphSet.WordLength / 2f) * pitch, 0f, 0f), letter, 0.04f * k, _look.Glow);
            _cursor = Art.Prim(PrimitiveType.Cube, root, c + new Vector3(GlyphSet.WordLength / 2f * pitch, -letter / 2f, 0f),
                new Vector3(letter * 0.6f, 0.05f, 0.015f), _look.Glow).GetComponent<Renderer>();
            Art.Paint(_cursor, _look.Glow, _look.Glow * 1.5f);
        }

        // Each display always makes the same sound for the same change, so it can be recognised by ear.
        private float Sound(bool up)
        {
            string name = _def.Kind switch
            {
                DisplayKind.Light => up ? "disp_light_on" : "disp_light_off",
                DisplayKind.Bar => up ? "disp_bar_up" : "disp_bar_down",
                DisplayKind.Gauge => "disp_gauge_move",
                _ => _script ? "disp_script_type" : "disp_glyph_change",
            };
            return Sfx.PlayFixed(name, _variant, transform.position, 0.8f);
        }

        public void SetFocused(bool focused)
        {
            _focused = focused;
            _focusFrame.SetActive(focused);
        }

        // While selected, the label's symbols light up in the glow colour and breathe gently.
        private void GlowLabel()
        {
            float target = _focused ? 1f : 0f;
            if (_labelIcons.Count == 0 || (_glow == 0f && target == 0f)) return;
            _glow = Mathf.MoveTowards(_glow, target, Time.deltaTime * 4f);
            float pulse = _glow * (0.6f + 0.4f * Mathf.Sin(Time.time * 4f));
            var color = Color.Lerp(_look.Panel, _look.Glow, _glow * 0.7f);
            foreach (var r in _labelIcons) Art.Paint(r, color, _look.Glow * (1.4f * pulse));
        }

        private static GameObject FocusFrame(Transform parent, float w, float h, Color glow)
        {
            var frame = new GameObject("Focus");
            frame.transform.SetParent(parent, false);
            const float t = 0.05f, z = -0.17f;
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, h / 2f, z), new Vector3(w, t, t)), (new Vector3(0f, -h / 2f, z), new Vector3(w, t, t)),
                (new Vector3(w / 2f, 0f, z), new Vector3(t, h, t)), (new Vector3(-w / 2f, 0f, z), new Vector3(t, h, t)),
            })
                Art.Paint(Art.Prim(PrimitiveType.Cube, frame.transform, pos, size, glow).GetComponent<Renderer>(), glow, glow * 1.5f);
            frame.SetActive(false);
            return frame;
        }

        // Writes step labels into the slot (one sentence per step hosted here).
        public void SetLabel(List<List<Plates.Mark>> rows)
        {
            if (_label == null || rows == null || rows.Count == 0) return;
            var c = transform.InverseTransformPoint(_label.position) + new Vector3(0f, 0f, -0.003f);
            float width = _placement.W * MachineView.Cell - 0.14f - 0.2f;
            Plates.Fill(transform, c, width, LabelSlot, rows, _look.Panel, _look, _labelIcons);
        }

        private static Renderer[] Children(Transform body, string prefix, int count)
        {
            var r = new Renderer[count];
            for (int i = 0; i < count; i++) r[i] = MachineKit.Find(body, prefix + i).GetComponent<Renderer>();
            return r;
        }

        private int Reading(int[] state) =>
            _def.Kind == DisplayKind.Light ? (MachineRules.LightOn(_def, state) ? 1 : 0)
            : _def.Kind == DisplayKind.Bar ? MachineRules.Readout(_def, state) : MachineRules.DisplayValue(_def, state);

        public bool Differs(int[] state) => Reading(state) != _shown;

        // Returns the length of the change sound (0 when silent: instant, or nothing changed).
        public float Show(int[] state, bool instant)
        {
            int value = MachineRules.DisplayValue(_def, state);
            int reading = Reading(state);
            float sound = !instant && _shown != int.MinValue && reading != _shown ? Sound(reading > _shown) : 0f;
            _shown = reading;
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
            return sound;
        }

        private void Update()
        {
            if (_cursor != null) _cursor.enabled = Time.time % 1f < 0.55f;
            GlowLabel();
            if (_needle == null) return;
            // Continuous needle with a little analog jitter.
            _needleAngle = Mathf.Lerp(_needleAngle, _needleTarget, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float jitter = (Mathf.PerlinNoise(Time.time * 3f, GetInstanceID()) - 0.5f) * 2f;
            if (_linear) _needle.localPosition = Vector3.LerpUnclamped(_needleMin, _needleMax, _needleAngle + jitter * 0.01f);
            else _needle.localRotation = Quaternion.AngleAxis(_needleAngle + jitter, _needleAxis) * _needleRest;
        }
    }
}
