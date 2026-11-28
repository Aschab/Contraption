using Contraption.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contraption.Game
{
    // Makes liquid properties (0-4) readable in bands: temperature (ice 0, mist 1, steam 3, boiling 4),
    // flammability (sparks 2, flames 3, big flames 4), refraction (shader, light 3, glints 4), viscosity (surface wobble).
    public sealed class LiquidEffects : MonoBehaviour
    {
        private static Texture2D _soft;
        private static Material _alpha, _additive;

        private Renderer _liquid;
        private ParticleSystem _steam, _bubbles, _mist, _sparks, _flames, _glints;
        private Transform[] _ice;
        private Light _light;
        private Liquid? _value;
        private float _width, _scale = 1f;

        // World Y of the liquid surface, set by the container every frame.
        public float LightScale { get; set; } = 1f;
        public bool Audible { get; set; } = true;
        private AudioSource _heatLoop, _flameLoop;
        private float _nextCrackle;
        public float Level { get; set; }
        public Bounds Bounds { get; set; }

        public static LiquidEffects Attach(MachineKit kit, Renderer liquid, Bounds bounds)
        {
            var fx = liquid.transform.parent.gameObject.AddComponent<LiquidEffects>();
            fx._liquid = liquid;
            fx.Bounds = bounds;
            fx.Build(kit);
            return fx;
        }

        private void Build(MachineKit kit)
        {
            var b = Bounds;
            _width = Mathf.Max(0.15f, Mathf.Min(b.size.x, b.size.z));
            float w = _width;
            // Effects were tuned for ~0.45 m wide containers; scale speeds and sizes with the container.
            float k = _scale = Mathf.Clamp(w / 0.45f, 0.1f, 2f);
            var root = new GameObject("Effects").transform;
            root.SetParent(transform, false);

            _steam = Particles(root, "Steam", Alpha(kit), new Color(1f, 1f, 1f, 0.35f), 6f, 2f, 0.35f * k, w * 0.5f, new Vector3(w * 0.7f, 0.01f, w * 0.5f), 0f);
            Grow(_steam, 0.5f, 1.8f);
            _bubbles = Particles(root, "Bubbles", Alpha(kit), new Color(1f, 1f, 1f, 0.7f), 30f, 1f, 0.5f * k, w * 0.08f, new Vector3(w * 0.7f, 0.01f, 0.01f), 0f);
            _mist = Particles(root, "Mist", Alpha(kit), new Color(0.85f, 0.95f, 1f, 0.25f), 5f, 2.5f, -0.08f * k, w * 0.6f, new Vector3(w * 1.2f, 0.3f, w), 0f);
            Grow(_mist, 0.6f, 1.4f);
            _sparks = Particles(root, "Sparks", Additive(kit), new Color(1f, 0.6f, 0.2f, 1f), 8f, 0.6f, 1.2f * k, 0.04f * k, new Vector3(w * 0.7f, 0.01f, w * 0.5f), 0.6f * k);
            var sm = _sparks.main;
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * k, 1.6f * k);
            var ss = _sparks.shape;
            ss.randomDirectionAmount = 0.4f;
            _flames = Particles(root, "Flames", Additive(kit), new Color(1f, 0.55f, 0.15f, 0.8f), 25f, 0.6f, 0.5f * k, w * 0.55f, new Vector3(w * 0.7f, 0.01f, w * 0.5f), -0.1f);
            Grow(_flames, 1f, 0.2f);
            var fc = _flames.colorOverLifetime;
            fc.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.6f), new GradientColorKey(Color.red, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fc.color = g;
            _glints = Particles(root, "Glints", Additive(kit), Color.white, 10f, 0.3f, 0f, 0.07f * k, new Vector3(w * 0.8f, 0.4f, 0.01f), 0f);

            _ice = new Transform[3];
            for (int i = 0; i < _ice.Length; i++)
            {
                var cube = Art.Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * w * 0.22f, new Color(0.85f, 0.95f, 1f));
                Art.Paint(cube.GetComponent<Renderer>(), new Color(0.85f, 0.95f, 1f), new Color(0.2f, 0.3f, 0.4f), 0.95f);
                _ice[i] = cube.transform;
            }

            _light = new GameObject("Glow").AddComponent<Light>();
            _light.transform.SetParent(root, false);
            _light.type = LightType.Point;
            _light.range = 2.5f * k;
            _light.shadows = LightShadows.None;
            Show(null, 0f);
        }

        // fill 0..1; effects hide when nearly empty.
        public void Show(Liquid? liquid, float fill)
        {
            _value = fill > 0.05f ? liquid : null;
            var l = _value ?? default;
            bool on = _value.HasValue;
            Toggle(_steam, on && l.Temperature >= 3);
            Toggle(_bubbles, on && l.Temperature >= 4);
            Toggle(_mist, on && l.Temperature <= 1);
            Toggle(_sparks, on && l.Flammability >= 2);
            Toggle(_flames, on && l.Flammability >= 3);
            Toggle(_glints, on && l.Refraction >= 4);
            foreach (var c in _ice) c.gameObject.SetActive(on && l.Temperature == 0);
            string heat = !on || !Audible ? null : l.Temperature <= 1 ? "liquid_cold" : l.Temperature == 3 ? "liquid_steam" : l.Temperature >= 4 ? "liquid_boil" : null;
            SetLoop(ref _heatLoop, heat, 0.35f);
            SetLoop(ref _flameLoop, on && Audible && l.Flammability >= 3 ? "liquid_flame" : null, l.Flammability >= 4 ? 0.55f : 0.35f);
            _light.enabled = on && l.Refraction >= 3;
            if (on)
            {
                var em = _flames.emission;
                em.rateOverTime = l.Flammability >= 4 ? 45f : 15f;
                var fm = _flames.main;
                fm.startSize = _width * (l.Flammability >= 4 ? 0.75f : 0.4f);
                _light.color = Art.LiquidColor(l.Color);
            }
        }

        // Swaps a trait loop's clip only when the trait band changes (Show runs every frame while filling).
        private void SetLoop(ref AudioSource src, string name, float volume)
        {
            if (name == null) { if (src != null && src.isPlaying) src.Stop(); return; }
            if (src != null && src.isPlaying && src.clip != null && src.clip.name == name) return;
            if (src != null) Destroy(src.gameObject);
            src = Sfx.Loop(name, transform, Vector3.zero, volume);
            if (src == null) return;
            src.time = Random.Range(0f, src.clip.length);
            src.Play();
        }

        private void Update()
        {
            if (!_value.HasValue) return;
            var l = _value.Value;
            if (Audible && (l.Flammability == 2 || l.Refraction >= 4) && Time.time > _nextCrackle)
            {
                _nextCrackle = Time.time + Random.Range(0.6f, 1.8f);
                Sfx.Play(l.Flammability == 2 ? "liquid_sparks" : "liquid_glint", new Vector3(Bounds.center.x, Level, Bounds.center.z), 0.5f);
            }
            var b = Bounds;
            float level = Level;
            var top = new Vector3(b.center.x, level, b.center.z);
            var front = new Vector3(b.center.x, b.min.y, b.min.z + 0.02f);
            _steam.transform.position = top + Vector3.up * 0.05f;
            _flames.transform.position = top;
            _sparks.transform.position = top;
            _mist.transform.position = new Vector3(b.center.x, (b.min.y + level) * 0.5f, b.center.z);
            // Bubbles rise along the front glass and pop at the surface.
            _bubbles.transform.position = front;
            var bm = _bubbles.main;
            bm.startLifetime = Mathf.Max(0.1f, (level - b.min.y) / (0.5f * _scale));
            _glints.transform.position = new Vector3(b.center.x, Mathf.Lerp(b.min.y, level, 0.5f), b.min.z + 0.02f);
            float t = Time.time;
            for (int i = 0; i < _ice.Length; i++)
            {
                float x = (i - 1) * _width * 0.28f;
                _ice[i].position = new Vector3(b.center.x + x + Mathf.Sin(t * 0.7f + i * 2f) * 0.03f, level - _width * 0.05f + Mathf.Sin(t * 1.3f + i) * 0.015f, b.min.z + _width * 0.3f);
                _ice[i].rotation = Quaternion.Euler(10f * Mathf.Sin(t + i), i * 37f + t * 6f, 8f * Mathf.Cos(t * 0.8f + i));
            }
            _light.transform.position = top + new Vector3(0f, 0.1f, -0.5f) * _scale;
            _light.intensity = LightScale * (l.Refraction >= 4 ? 2.5f : 1.2f) * (0.75f + 0.25f * Mathf.PerlinNoise(t * 3f, GetInstanceID()));
        }

        private static void Toggle(ParticleSystem ps, bool on)
        {
            if (on && !ps.isPlaying) ps.Play();
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static ParticleSystem Particles(Transform parent, string name, Material mat, Color color, float rate, float life,
            float speed, float size, Vector3 box, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            // Box emits along its local Z; rotated so particles leave upwards. Scale is in the rotated frame.
            shape.scale = new Vector3(box.x, box.z, box.y);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private static Material Alpha(MachineKit kit) => _alpha != null ? _alpha : _alpha = ParticleMaterial(kit, false);
        private static Material Additive(MachineKit kit) => _additive != null ? _additive : _additive = ParticleMaterial(kit, true);

        private static Material ParticleMaterial(MachineKit kit, bool additive)
        {
            var shader = kit.ParticleShader != null ? kit.ParticleShader : Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { name = additive ? "FxAdditive" : "FxAlpha" };
            m.SetTexture("_BaseMap", Soft());
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent + (additive ? 1 : 0);
            return m;
        }

        private static Texture2D Soft()
        {
            if (_soft != null) return _soft;
            const int n = 32;
            _soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    _soft.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            _soft.Apply();
            return _soft;
        }
    }
}
