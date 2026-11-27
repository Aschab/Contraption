using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contraption.Game
{
    // Slot colours + lit materials for one machine: the kit's defaults, overridden by the layout's colour scheme.
    // ForPart gives a variant with that part's panel tint and glow colour (other slots shared).
    public sealed class Look
    {
        public Color Body, Trim, Panel, Metal, Accent, Glow, Dark, Glass, Liquid;
        // The machine's other glow colour (same as Glow when the scheme has one).
        public Color Glow2;
        // Workshop surfaces (same for every machine).
        public Color Wood = new(0.42f, 0.28f, 0.2f), Stone = new(0.33f, 0.29f, 0.34f), Sky = new(0.75f, 0.85f, 1f);

        private readonly Dictionary<string, Material> _materials = new();
        private readonly Dictionary<(int, int), Look> _variants = new();
        private MachineColors _colors;
        private Look _parent;
        private string _name;
        private Shader _painted;

        public static Look From(MachineKit kit, MachineColors colors = null)
        {
            var l = new Look
            {
                _name = kit.name, _colors = colors, _painted = kit.PaintedShader,
                Body = kit.Body, Trim = kit.Trim, Panel = kit.Panel, Metal = kit.Metal, Accent = kit.Accent,
                Glow = kit.Glow, Dark = kit.Dark, Glass = kit.Glass, Liquid = kit.Liquid,
            };
            if (colors != null)
            {
                l.Body = Hex(colors.Frame, l.Body);
                l.Trim = Hex(colors.Trim, l.Trim);
                l.Panel = Hex(colors.Panel, l.Panel);
                l.Metal = Hex(colors.Metal, l.Metal);
                if (colors.Glows.Count > 0) l.Glow = Hex(colors.Glows[0], l.Glow);
            }
            l.Glow2 = colors != null && colors.Glows.Count > 1 ? Hex(colors.Glows[1], l.Glow) : l.Glow;
            return l;
        }

        public Look ForPart(Placement p)
        {
            if (_colors == null || (p.Tint == 0 && p.Glow == 0)) return this;
            var key = (p.Tint, p.Glow);
            if (_variants.TryGetValue(key, out var v)) return v;
            v = (Look)MemberwiseClone();
            v._materials.Clear();
            v._parent = this;
            v._variants.Clear();
            v.Panel = p.Tint == 1 ? Hex(_colors.PanelAlt, Panel) : p.Tint == 2 ? Hex(_colors.PanelAlt2, Panel) : Panel;
            int n = _colors.Glows.Count;
            v.Glow = n > 0 ? Hex(_colors.Glows[p.Glow % n], Glow) : Glow;
            v.Glow2 = n > 1 ? Hex(_colors.Glows[(p.Glow + 1) % n], Glow) : v.Glow;
            _variants[key] = v;
            return v;
        }

        // Material names are "<Slot>" or "<Slot>.<KIT>"; unknown names keep their imported material.
        public bool TryGet(string materialName, out Material m)
        {
            string slot = materialName.Split('.', ' ')[0];
            if (_materials.TryGetValue(slot, out m)) return true;
            if (!System.Enum.TryParse(slot, out MachineKit.Slot s)) return false;
            if (_parent != null && s != MachineKit.Slot.Panel && s != MachineKit.Slot.Glow) return _parent.TryGet(slot, out m);
            m = Create(s);
            _materials[slot] = m;
            return true;
        }

        public Color ColorOf(MachineKit.Slot s) => s switch
        {
            MachineKit.Slot.Body => Body, MachineKit.Slot.Trim => Trim, MachineKit.Slot.Panel => Panel, MachineKit.Slot.Metal => Metal,
            MachineKit.Slot.Accent => Accent, MachineKit.Slot.Glow => Glow, MachineKit.Slot.Dark => Dark, MachineKit.Slot.Glass => Glass,
            MachineKit.Slot.Wood => Wood, MachineKit.Slot.Stone => Stone, MachineKit.Slot.Sky => Sky,
            _ => Liquid,
        };

        private Material Create(MachineKit.Slot s)
        {
            bool glass = s == MachineKit.Slot.Glass;
            var shader = glass ? Shader.Find("Universal Render Pipeline/Lit") : Art.PaintedShader(_painted);
            var m = new Material(shader) { name = $"{_name}_{s}" };
            var c = ColorOf(s);
            if (!glass)
            {
                m.SetFloat("_Grime", s is MachineKit.Slot.Glow or MachineKit.Slot.Sky ? 0f : s == MachineKit.Slot.Metal ? 0.45f : s is MachineKit.Slot.Wood or MachineKit.Slot.Stone ? 0.9f : 0.6f);
                m.SetFloat("_Wear", s == MachineKit.Slot.Glow ? 0f : 0.6f);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", s == MachineKit.Slot.Metal || s == MachineKit.Slot.Glass ? 0.7f : 0.3f);
            m.SetFloat("_Metallic", s == MachineKit.Slot.Metal ? 0.5f : 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", s == MachineKit.Slot.Glow ? c * 1.5f : s == MachineKit.Slot.Sky ? c * 2.5f : Color.black);
            if (s == MachineKit.Slot.Glass) MakeTransparent(m, c.a);
            return m;
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

        private static Color Hex(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;
    }
}
