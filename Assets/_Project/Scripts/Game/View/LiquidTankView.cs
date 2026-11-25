using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Shows a liquid; properties are visible through placeholder effects:
    // color = hue, temperature = glow, flammability = flicker, viscosity = slosh speed, refraction = gloss.
    public sealed class LiquidTankView : MonoBehaviour
    {
        private Renderer _liquid;
        private Vector3 _restScale;
        private Liquid? _value;

        public static LiquidTankView Create(MachineKit kit, Transform machine, Vector3 pos, float scale)
        {
            var go = new GameObject("Tank");
            go.transform.SetParent(machine, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var v = go.AddComponent<LiquidTankView>();
            var body = kit.Spawn(kit.Tank, go.transform, Vector3.zero).transform;
            v._liquid = MachineKit.Find(body, "Liquid").GetComponent<Renderer>();
            v._restScale = v._liquid.transform.localScale;
            v.Show(null);
            return v;
        }

        public void Show(Liquid? liquid)
        {
            _value = liquid;
            _liquid.gameObject.SetActive(liquid.HasValue);
        }

        private void Update()
        {
            if (!_value.HasValue) return;
            var l = _value.Value;
            const float k = 1f / Values.Max;

            float slosh = Mathf.Lerp(7f, 0.6f, l.Viscosity * k);
            float fill = 0.85f + Mathf.Sin(Time.time * slosh) * 0.04f;
            _liquid.transform.localScale = new Vector3(_restScale.x, _restScale.y * fill, _restScale.z);

            var color = Art.LiquidColor(l.Color);
            float glow = l.Temperature * k * 1.2f;
            if (l.Flammability >= 3) glow += Mathf.PerlinNoise(Time.time * 6f, 0f) * l.Flammability * k * 1.5f;
            Art.Paint(_liquid, color, color * glow, Mathf.Lerp(0.1f, 0.98f, l.Refraction * k));
        }
    }
}
