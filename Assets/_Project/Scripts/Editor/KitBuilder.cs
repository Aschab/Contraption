using Contraption.Game;
using UnityEditor;
using UnityEngine;

namespace Contraption.Editor
{
    // Creates/updates the kit assets from the FBX pieces exported by ArtSource/Blender/build_kit.py.
    // Colors are only set when a kit is first created, so Inspector tweaks survive rebuilds.
    public static class KitBuilder
    {
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
            kit.Segment = Model(prefix + "_Segment"); kit.EndL = Model(prefix + "_EndL"); kit.EndR = Model(prefix + "_EndR");
            kit.Foot = Model(prefix + "_Foot"); kit.Bay = Model(prefix + "_Bay"); kit.Top = Model(prefix + "_Top");
            kit.Button = Model("Ctl_Button"); kit.Lever = Model("Ctl_Lever"); kit.Dial = Model("Ctl_Dial"); kit.Knob = Model("Ctl_Knob");
            kit.Light = Model("Disp_Light"); kit.Bar = Model("Disp_Bar"); kit.Gauge = Model("Disp_Gauge"); kit.Glyph = Model("Disp_Glyph");
            kit.Tank = Model("Tank"); kit.Pipe = Model("Pipe"); kit.Flange = Model("Flange");
            var decor = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model " + prefix + "_", new[] { Models + "Decor" }))
                decor.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            kit.Decor = decor.ToArray();
            EditorUtility.SetDirty(kit);
        }

        private static GameObject Model(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
            if (go == null) Debug.LogError("Missing kit model " + name);
            return go;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
