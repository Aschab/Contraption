using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // A glass container whose liquid fills and drains; the liquid's properties drive the shader and LiquidEffects.
    public sealed class LiquidContainerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FillLevelId = Shader.PropertyToID("_FillLevel");
        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int WobbleSpeedId = Shader.PropertyToID("_WobbleSpeed");
        private static readonly int RefractionId = Shader.PropertyToID("_Refraction");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");

        private Renderer _liquid;
        private LiquidEffects _fx;
        private MaterialPropertyBlock _block;
        private Liquid? _value;
        private float _fill, _target, _speed;

        public Transform Inlet { get; private set; }
        public Transform Outlet { get; private set; }
        public float Fill => _fill;
        public bool Moving => !Mathf.Approximately(_fill, _target);
        // From the mesh, so it stays valid while the renderer is hidden.
        public Bounds LiquidBounds
        {
            get
            {
                var mb = _liquid.GetComponent<MeshFilter>().sharedMesh.bounds;
                var m = _liquid.transform.localToWorldMatrix;
                var b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2) * 2 - 1))));
                return b;
            }
        }

        public float SurfaceY
        {
            get
            {
                var b = LiquidBounds;
                return Mathf.Lerp(b.min.y, b.max.y, _fill);
            }
        }

        public static LiquidContainerView Create(MachineKit kit, Transform parent, string style, Vector3 pos, float scale = 1f)
        {
            var go = new GameObject(style);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var v = go.AddComponent<LiquidContainerView>();
            var body = kit.Spawn(style, go.transform, Vector3.zero).transform;
            v._liquid = MachineKit.Find(body, "Liquid").GetComponent<Renderer>();
            v._liquid.sharedMaterial = kit.LiquidMaterial();
            v._liquid.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v.Inlet = MachineKit.FindOptional(body, "Inlet");
            v.Outlet = MachineKit.FindOptional(body, "Outlet");
            v._block = new MaterialPropertyBlock();
            v._fx = LiquidEffects.Attach(kit, v._liquid, v.LiquidBounds);
            v.Apply();
            return v;
        }

        public void Show(Liquid? liquid)
        {
            _value = liquid;
            Apply();
        }

        public void SetFill(float target, float seconds)
        {
            _target = Mathf.Clamp01(target);
            _speed = seconds <= 0f ? float.MaxValue : Mathf.Max(0.01f, Mathf.Abs(_target - _fill) / seconds);
            if (seconds <= 0f) _fill = _target;
            Apply();
        }

        private void Update()
        {
            if (Moving)
            {
                _fill = Mathf.MoveTowards(_fill, _target, _speed * Time.deltaTime);
                Apply();
            }
            _fx.Level = SurfaceY;
            _fx.Bounds = LiquidBounds;
        }

        private void Apply()
        {
            bool visible = _value.HasValue && _fill > 0.001f;
            _liquid.enabled = visible;
            _fx.Show(_value, _fill);
            if (!visible) return;
            var l = _value.Value;
            const float k = 1f / Values.Max;
            _liquid.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Art.LiquidColor(l.Color));
            _block.SetFloat(FillLevelId, SurfaceY);
            float size = Mathf.Min(LiquidBounds.size.x, 1f);
            _block.SetFloat(WobbleId, Mathf.Lerp(0.035f, 0.004f, l.Viscosity * k) * size * (Moving ? 1.6f : 1f));
            _block.SetFloat(WobbleSpeedId, Mathf.Lerp(7f, 0.8f, l.Viscosity * k));
            _block.SetFloat(RefractionId, l.Refraction * k);
            _block.SetFloat(EmissionId, 0.15f + l.Temperature * k * 0.25f);
            _liquid.SetPropertyBlock(_block);
        }
    }
}
