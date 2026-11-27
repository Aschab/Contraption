using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contraption.Game
{
    // A visual style: piece models + slot colors. Models use material names "<Slot>" or "<Slot>.<KIT>";
    // known slots are replaced at spawn with this kit's materials so one model can serve several kits.
    [CreateAssetMenu(menuName = "Contraption/Machine Kit")]
    public sealed class MachineKit : ScriptableObject
    {
        public enum Slot { Body, Trim, Panel, Metal, Accent, Glow, Dark, Glass, Liquid }

        public string DisplayName;
        public Color Body, Trim, Panel, Metal, Accent, Glow, Dark, Glass, Liquid;

        [Header("Chassis (kit specific)")]
        public GameObject Segment, EndL, EndR, Foot, Bay, Top;
        [Header("Mechanisms (shared models)")]
        public GameObject Button, Lever, Dial, Knob, Joystick, Light, Bar, Gauge, Glyph, Tank, Pipe, Flange;
        [Header("Decor (placed on top of segments)")]
        public GameObject[] Decor;

        [NonSerialized] private Dictionary<string, Material> _materials;

        public Color ColorOf(Slot s) => s switch
        {
            Slot.Body => Body, Slot.Trim => Trim, Slot.Panel => Panel, Slot.Metal => Metal, Slot.Accent => Accent,
            Slot.Glow => Glow, Slot.Dark => Dark, Slot.Glass => Glass, _ => Liquid,
        };

        public GameObject Spawn(GameObject model, Transform parent, Vector3 localPos, string name = null)
        {
            var go = Instantiate(model, parent, false);
            go.transform.localPosition = localPos;
            if (name != null) go.name = name;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && TryGetMaterial(mats[i].name, out var m)) mats[i] = m;
                r.sharedMaterials = mats;
            }
            return go;
        }

        private bool TryGetMaterial(string materialName, out Material m)
        {
            if (_materials == null) BuildMaterials();
            string slot = materialName.Split('.', ' ')[0];
            return _materials.TryGetValue(slot, out m);
        }

        private void BuildMaterials()
        {
            _materials = new Dictionary<string, Material>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (Slot s in Enum.GetValues(typeof(Slot)))
            {
                var m = new Material(lit) { name = $"{name}_{s}" };
                var c = ColorOf(s);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", s == Slot.Metal || s == Slot.Glass ? 0.7f : 0.3f);
                m.SetFloat("_Metallic", s == Slot.Metal ? 0.5f : 0f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", s == Slot.Glow ? c * 1.5f : Color.black);
                if (s == Slot.Glass) MakeTransparent(m, c.a);
                _materials[s.ToString()] = m;
            }
        }

        private static void MakeTransparent(Material m, float alpha)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            var c = m.GetColor("_BaseColor");
            m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
        }

        // Finds a named part, ignoring Blender's duplicate suffixes ("Moving.001"). Throws with context if missing.
        public static Transform Find(Transform root, string partName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (BaseName(t.name) == partName) return t;
            throw new InvalidOperationException($"Kit model '{root.name}' has no part named '{partName}'.");
        }

        private static string BaseName(string n)
        {
            int dot = n.LastIndexOf('.');
            if (dot <= 0) return n;
            for (int i = dot + 1; i < n.Length; i++)
                if (!char.IsDigit(n[i])) return n;
            return n.Substring(0, dot);
        }

        public static Transform FindSuffix(Transform root, string suffix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name.EndsWith(suffix, StringComparison.Ordinal)) return t;
            return null;
        }
    }
}
