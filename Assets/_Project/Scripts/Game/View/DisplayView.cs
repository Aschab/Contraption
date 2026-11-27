using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    public sealed class DisplayView : MonoBehaviour
    {
        private DisplayDef _def;
        private MachineKit _kit;
        private int[] _glyphs;
        private Renderer _bulb;
        private Renderer[] _cells;
        private Transform _needle;
        private Quaternion _needleRest;
        private Vector3 _needleAxis;
        private float _needleAngle, _needleTarget;

        public static DisplayView Create(MachineKit kit, Transform machine, Vector3 pos, DisplayDef def, int[] glyphs)
        {
            var go = new GameObject($"{def.Kind} display");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            var v = go.AddComponent<DisplayView>();
            v._def = def;
            v._kit = kit;
            v._glyphs = glyphs;

            var model = def.Kind switch
            {
                DisplayKind.Light => kit.Light,
                DisplayKind.Bar => kit.Bar,
                DisplayKind.Gauge => kit.Gauge,
                _ => kit.Glyph,
            };
            var body = kit.Spawn(model, go.transform, Vector3.zero).transform;
            switch (def.Kind)
            {
                case DisplayKind.Light: v._bulb = MachineKit.Find(body, "Bulb").GetComponent<Renderer>(); break;
                case DisplayKind.Bar: v._cells = Children(body, "Seg", 5); break;
                case DisplayKind.Glyph: v._cells = Children(body, "Cell", 9); break;
                case DisplayKind.Gauge:
                    v._needle = MachineKit.Find(body, "Needle");
                    v._needleRest = v._needle.localRotation;
                    v._needleAxis = Art.AxisIn(v._needle, machine, Vector3.forward);
                    break;
            }
            return v;
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
            Color on = _kit.Glow, off = _kit.Dark * 1.6f;

            switch (_def.Kind)
            {
                case DisplayKind.Light:
                    bool lit = MachineRules.LightOn(_def, state);
                    Art.Paint(_bulb, lit ? on : off, lit ? on * 2.5f : Color.black, 0.8f);
                    break;
                case DisplayKind.Bar:
                    int n = MachineRules.Readout(_def, state);
                    for (int i = 0; i < _cells.Length; i++) Art.Paint(_cells[i], i < n ? on : off, i < n ? on * 1.5f : Color.black);
                    break;
                case DisplayKind.Gauge:
                    _needleTarget = Mathf.Lerp(70f, -70f, norm);
                    if (instant) _needleAngle = _needleTarget;
                    break;
                case DisplayKind.Glyph:
                    Glyphs.Paint(_cells, _glyphs[MachineRules.Readout(_def, state)], on, off, 1.5f);
                    break;
            }
        }

        private void Update()
        {
            if (_needle == null) return;
            // Continuous needle with a little analog jitter.
            _needleAngle = Mathf.Lerp(_needleAngle, _needleTarget, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float jitter = (Mathf.PerlinNoise(Time.time * 3f, GetInstanceID()) - 0.5f) * 2f;
            _needle.localRotation = Quaternion.AngleAxis(_needleAngle + jitter, _needleAxis) * _needleRest;
        }
    }
}
