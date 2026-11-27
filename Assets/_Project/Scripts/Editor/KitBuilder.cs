using System.Linq;
using Contraption.Game;
using UnityEditor;
using UnityEngine;

namespace Contraption.Editor
{
    // Creates/updates the kit assets from the FBX pieces exported by ArtSource/Blender/build_kit.py.
    // Colors are only set when a kit is first created, so Inspector tweaks survive rebuilds.
    public static class KitBuilder
    {
        private static readonly string[] Prefixes = { "CO", "TH" };
        private const string Models = "Assets/_Project/Art/Models/Kit/";
        private const string Output = "Assets/_Project/Resources/Kits/";

        [MenuItem("Contraption/Build Kits")]
        public static void BuildAll()
        {
            Build("CharcoalOchre", "Charcoal & Ochre", "CO", k =>
            {
                k.Body = Hex("29272A"); k.Trim = Hex("C7862E"); k.Panel = Hex("E8D9B5"); k.Metal = Hex("9A9A9E");
                k.Accent = Hex("5A3420"); k.Glow = Hex("3FFFD9"); k.Dark = Hex("151517");
            });
            Build("TwinHeart", "Twin-Heart", "TH", k =>
            {
                k.Body = Hex("33615A"); k.Trim = Hex("A8452E"); k.Panel = Hex("E8D9B5"); k.Metal = Hex("C28F42");
                k.Accent = Hex("A8452E"); k.Glow = Hex("3FFFD9"); k.Dark = Hex("1A1F1F");
            });
            AssetDatabase.SaveAssets();
            Debug.Log("Contraption kits built.");
        }

        private static void Build(string file, string display, string prefix, System.Action<MachineKit> colors)
        {
            System.IO.Directory.CreateDirectory(Output);
            string path = Output + file + ".asset";
            var kit = AssetDatabase.LoadAssetAtPath<MachineKit>(path);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<MachineKit>();
                kit.Glass = new Color(0.75f, 0.92f, 0.95f, 0.25f);
                kit.Liquid = Hex("3FE0C8");
                colors(kit);
                AssetDatabase.CreateAsset(kit, path);
            }
            kit.DisplayName = display;
            kit.Prefix = prefix;
            kit.Pieces = new System.Collections.Generic.List<GameObject>();
            foreach (var model in ModelPaths())
            {
                string n = System.IO.Path.GetFileNameWithoutExtension(model);
                bool otherKit = System.Array.Exists(Prefixes, p => p != prefix && n.StartsWith(p + "_"));
                if (!otherKit) kit.Pieces.Add(AssetDatabase.LoadAssetAtPath<GameObject>(model));
            }
            kit.LiquidShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/Liquid.shader");
            kit.ParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            kit.PaintedShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/Painted.shader");
            var decor = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model " + prefix + "_", new[] { Models + "Decor" }))
                decor.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            kit.Decor = decor.ToArray();
            EditorUtility.SetDirty(kit);
        }

        // Logs whether imported pieces face the camera (-Z) with left/right preserved. Runs after every script reload.
        [InitializeOnLoadMethod]
        private static void ScheduleOrientationCheck() => EditorApplication.delayCall += () =>
        {
            // Kits created before a new piece existed get their references filled in automatically.
            foreach (var guid in AssetDatabase.FindAssets("t:MachineKit"))
            {
                var kit = AssetDatabase.LoadAssetAtPath<MachineKit>(AssetDatabase.GUIDToAssetPath(guid));
                if (kit != null && (kit.Pieces == null || kit.Pieces.Count == 0 || kit.Pieces.Exists(p => p == null)
                    || kit.LiquidShader == null || kit.PaintedShader == null || kit.Pieces.Count != CountFor(kit.Prefix))) { BuildAll(); break; }
            }
            CheckOrientation();
        };

        public static void CheckOrientation()
        {
            foreach (var prefix in Prefixes)
            {
                var tr = Center(prefix + "_Tile_8");
                var tl = Center(prefix + "_Tile_4");
                if (tr == null || tl == null) continue;
                bool ok = tr.Value.z > 0.2f && tl.Value.x < 0f && tr.Value.x > 0f && tr.Value.y > 0f;
                string msg = $"Kit orientation {prefix}: {(ok ? "OK" : "WRONG")} (top-right tile centre {tr.Value}, top-left {tl.Value})";
                if (ok) Debug.Log(msg); else Debug.LogError(msg);
            }
        }

        private static string[] ModelPaths() =>
            System.Array.ConvertAll(AssetDatabase.FindAssets("t:Model", new[] { Models.TrimEnd('/') }), AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') + "/" == Models).OrderBy(p => p).ToArray();

        private static int CountFor(string prefix) =>
            ModelPaths().Count(p => !System.Array.Exists(Prefixes, x => x != prefix && System.IO.Path.GetFileName(p).StartsWith(x + "_")));

        private static Vector3? Center(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
            if (go == null) return null;
            var mfs = go.GetComponentsInChildren<MeshFilter>();
            if (mfs.Length == 0) return null;
            var sum = Vector3.zero;
            foreach (var mf in mfs) sum += mf.transform.localToWorldMatrix.MultiplyPoint(mf.sharedMesh.bounds.center);
            return sum / mfs.Length;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
