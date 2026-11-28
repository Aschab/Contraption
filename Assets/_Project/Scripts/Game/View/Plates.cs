using System.Collections.Generic;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Label plates: brass plaques with symbol sentences describing pipeline steps ([trait] [step] [trait] [threshold]).
    // Symbols are textures in Resources/Labels (Trait_*, Step_*, Mod_*).
    public static class Plates
    {
        // A symbol name, or a 2x2 value grid when Grid >= 0.
        public readonly struct Mark
        {
            public readonly string Icon;
            public readonly int Grid;

            public Mark(string icon) { Icon = icon; Grid = -1; }
            public Mark(int grid) { Icon = null; Grid = grid; }
        }

        private static readonly Dictionary<string, Material> Materials = new();
        private static Shader _shader;

        public static string Trait(LiquidProperty p) => "Trait_" + p;

        // What a step's label says at the given level. `home` adds the display modifiers (mixes two registers, reads backwards).
        public static List<Mark> Sentence(MachineDefinition def, int step, int level, int home)
        {
            var op = def.Pipeline[step];
            var marks = new List<Mark>();
            if (level <= MachineHints.LabelNone) return marks;
            if (home >= 0)
            {
                var d = def.Displays[home];
                if (d.Terms.Count > 1) marks.Add(new Mark("Mod_Pair"));
                if (MachineHints.Weight(d, op.Register) < 0) marks.Add(new Mark("Mod_Inverted"));
            }
            bool full = level >= MachineHints.LabelFull;
            bool two = op.Kind is OpKind.Siphon or OpKind.Swap or OpKind.Exchange;
            // Siphon reads left to right: from its source into its target.
            marks.Add(new Mark(Trait(op.Kind == OpKind.Siphon ? op.Source : op.Target)));
            if (full) marks.Add(new Mark("Step_" + op.Kind));
            if (two) marks.Add(new Mark(Trait(op.Kind == OpKind.Siphon ? op.Target : op.Source)));
            if (full && op.Kind is OpKind.Purge or OpKind.Swap) marks.Add(new Mark(op.Threshold));
            return marks;
        }

        // Plate centred on `pos`, one row per sentence, shrunk to fit maxWidth. Faces -Z (the camera) unless facingBack.
        public static GameObject Build(Transform parent, Vector3 pos, List<List<Mark>> rows, float maxWidth, Look look, bool facingBack, float icon = 0.15f)
        {
            int longest = 0;
            foreach (var r in rows) longest = Mathf.Max(longest, r.Count);
            if (longest == 0) return null;
            const float pad = 0.05f;
            float gap = icon * 0.15f;
            float width = longest * icon + (longest - 1) * gap + pad * 2f;
            if (width > maxWidth)
            {
                icon *= (maxWidth - pad * 2f) / (width - pad * 2f);
                gap = icon * 0.15f;
                width = maxWidth;
            }
            float rowH = icon * 1.2f, height = rows.Count * rowH + pad;

            var root = new GameObject("Plate").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            if (facingBack) root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Art.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(width, height, 0.03f), look.Metal);
            Art.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.004f), new Vector3(width + 0.03f, height + 0.03f, 0.024f), look.Dark);
            foreach (float x in new[] { -width / 2f + pad * 0.5f, width / 2f - pad * 0.5f })
                Art.Prim(PrimitiveType.Cylinder, root, new Vector3(x, 0f, -0.016f), new Vector3(0.02f, 0.006f, 0.02f), look.Dark)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                float y = height / 2f - pad / 2f - rowH * (r + 0.5f);
                float x = -(row.Count * icon + (row.Count - 1) * gap) / 2f + icon / 2f;
                foreach (var m in row)
                {
                    var c = new Vector3(x, y, -0.017f);
                    if (m.Grid >= 0) ValueGrid(root, c, icon * 0.8f, m.Grid, look);
                    else Icon(root, c, icon, m.Icon, look.Dark);
                    x += icon + gap;
                }
            }
            return root.gameObject;
        }

        public static GameObject Icon(Transform parent, Vector3 pos, float size, string name, Color color, Color emission = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size, size, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Material(name);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Art.Paint(r, color, emission);
            return go;
        }

        // A value 0-4 as the 2x2 grid used everywhere: boxes fill top-left, top-right, bottom-left, bottom-right.
        public static void ValueGrid(Transform parent, Vector3 c, float size, int value, Look look)
        {
            Art.Prim(PrimitiveType.Cube, parent, c + new Vector3(0f, 0f, 0.001f), new Vector3(size, size, 0.006f), look.Dark);
            float box = size * 0.4f, off = size * 0.235f;
            for (int i = 0; i < 4; i++)
            {
                var p = c + new Vector3(i % 2 == 0 ? -off : off, i < 2 ? off : -off, -0.003f);
                var cell = Art.Prim(PrimitiveType.Cube, parent, p, new Vector3(box, box, 0.006f), look.Metal).GetComponent<Renderer>();
                if (i < value) Art.Paint(cell, look.Glow, look.Glow * 1.5f);
            }
        }

        private static Material Material(string name)
        {
            if (Materials.TryGetValue(name, out var m) && m != null) return m;
            _shader ??= Shader.Find("Contraption/Icon");
            var tex = Resources.Load<Texture2D>("Labels/" + name);
            if (tex != null) tex.wrapMode = TextureWrapMode.Clamp;
            else Debug.LogError("Missing label symbol Resources/Labels/" + name);
            m = new Material(_shader != null ? _shader : Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            Materials[name] = m;
            return m;
        }
    }
}
