using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Small runtime-built bits (ticks, glyph dots) and per-renderer tinting via property blocks.
    public static class Art
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static Material _base;
        private static Shader _painted;
        private static MaterialPropertyBlock _block;

        private static Material Base
        {
            get
            {
                if (_base != null) return _base;
                _base = new Material(PaintedShader(null));
                _base.SetFloat("_Grime", 0f);
                _base.SetFloat("_Wear", 0f);
                _base.EnableKeyword("_EMISSION");
                _base.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return _base;
            }
        }

        // Falls back to URP Lit if the custom shader is missing (e.g. not yet imported).
        public static Shader PaintedShader(Shader preferred)
        {
            if (preferred != null) return preferred;
            _painted ??= Shader.Find("Contraption/Painted");
            return _painted != null ? _painted : Shader.Find("Universal Render Pipeline/Lit");
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

        // Rotation of `moving` by `angle` around a machine-space axis, independent of how the model was imported.
        public static Vector3 AxisIn(Transform moving, Transform machine, Vector3 machineAxis) =>
            moving.parent.InverseTransformDirection(machine.TransformDirection(machineAxis));
    }
}
