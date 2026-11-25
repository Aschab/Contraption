using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Shows a liquid; properties are visible through simple placeholder effects:
    // color = hue, temperature = glow, flammability = flicker, viscosity = slosh speed, refraction = gloss.
    public sealed class LiquidTankView : MonoBehaviour
    {
        private Renderer _liquid;
        private float _height;
        private Liquid? _value;

        public static LiquidTankView Create(Transform parent, Vector3 pos, float height, float diameter)
        {
            var go = new GameObject("Tank");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var v = go.AddComponent<LiquidTankView>();
            v._height = height;

            var t = go.transform;
            Art.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, height / 2f + 0.05f, 0f), new Vector3(diameter + 0.12f, 0.05f, diameter + 0.12f), Art.Brass);
            Art.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, -height / 2f - 0.05f, 0f), new Vector3(diameter + 0.12f, 0.05f, diameter + 0.12f), Art.Brass);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.5f;
                Art.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (diameter / 2f + 0.04f), new Vector3(0.04f, height, 0.04f), Art.Brass);
            }
            v._liquid = Art.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(diameter, height / 2f, diameter), Art.Unlit).GetComponent<Renderer>();
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
            float k = 1f / Values.Max;

            float slosh = Mathf.Lerp(7f, 0.6f, l.Viscosity * k);
            float fill = 0.85f + Mathf.Sin(Time.time * slosh) * 0.04f;
            var tr = _liquid.transform;
            tr.localScale = new Vector3(tr.localScale.x, _height / 2f * fill, tr.localScale.z);
            tr.localPosition = new Vector3(0f, -_height / 2f * (1f - fill), 0f);

            var color = Art.LiquidColor(l.Color);
            float glow = l.Temperature * k * 1.2f;
            if (l.Flammability >= 3) glow += Mathf.PerlinNoise(Time.time * 6f, 0f) * l.Flammability * k * 1.5f;
            Art.Paint(_liquid, color, color * glow, Mathf.Lerp(0.1f, 0.98f, l.Refraction * k));
        }
    }
}
