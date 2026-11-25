using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Placeholder primitive art. Replaced by the modular kit later; keep the palette in sync with Share/ concept art.
    public static class Art
    {
        public static readonly Color Charcoal = new(0.16f, 0.15f, 0.15f);
        public static readonly Color Ochre = new(0.78f, 0.52f, 0.18f);
        public static readonly Color Cream = new(0.90f, 0.84f, 0.70f);
        public static readonly Color Brass = new(0.70f, 0.52f, 0.25f);
        public static readonly Color Steel = new(0.60f, 0.60f, 0.62f);
        public static readonly Color Leather = new(0.35f, 0.20f, 0.12f);
        public static readonly Color Glow = new(0.25f, 1f, 0.85f);
        public static readonly Color Unlit = new(0.10f, 0.18f, 0.18f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static Material _base;
        private static MaterialPropertyBlock _block;

        private static Material Base
        {
            get
            {
                if (_base != null) return _base;
                _base = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _base.EnableKeyword("_EMISSION");
                _base.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return _base;
            }
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Base;
            Paint(r, color);
            return go;
        }

        // Cylinder whose axis faces the camera (-Z). Unity cylinders are 2 units tall at scale 1.
        public static GameObject Disc(Transform parent, Vector3 pos, float diameter, float depth, Color color)
        {
            var go = Prim(PrimitiveType.Cylinder, parent, pos, new Vector3(diameter, depth / 2f, diameter), color);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        public static void Paint(Renderer r, Color color, Color emission = default, float smoothness = 0.35f)
        {
            _block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionId, emission);
            _block.SetFloat(SmoothnessId, smoothness);
            r.SetPropertyBlock(_block);
        }

        public static Color LiquidColor(int hue) => Color.HSVToRGB(hue / (float)Values.Levels, 0.75f, 0.95f);
    }
}
