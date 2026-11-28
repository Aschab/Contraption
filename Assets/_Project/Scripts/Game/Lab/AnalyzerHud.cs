using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Placeholder player HUD (IMGUI): input selection and the last output. The manual and log are books on the table.
    // Will become diegetic (in-world manual + analyzer device) once the look is settled.
    public sealed class AnalyzerHud
    {
        public const float Width = 340f;
        private static readonly string[] PropNames = { "Col", "Vis", "Tmp", "Flm", "Ref" };
        private Vector2 _scroll;

        public void Draw(Rect area, MachineSession s, MachineInput input)
        {
            var def = s.Definition;
            GUILayout.BeginArea(area, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (s.Solved) GUILayout.Box("<b><size=18>MACHINE FIXED</size></b>  in " + s.Moves + " moves", Rich());

            GUILayout.Label("<b>INPUTS</b>   ([ ] cycle, Tab port)", Rich(GUI.skin.label));
            for (int p = 0; p < def.InputPorts; p++)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(input.ActivePort == p, $"Port {p + 1}", GUILayout.Width(70))) input.ActivePort = p;
                if (GUILayout.Button("◀", GUILayout.Width(26))) { input.ActivePort = p; input.CycleInput(s, -1); }
                LiquidRow(def.InputPool[s.SelectedInputs[p]]);
                if (GUILayout.Button("▶", GUILayout.Width(26))) { input.ActivePort = p; input.CycleInput(s, 1); }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            if (GUILayout.Button("<b>TEST (T)</b>", Rich(GUI.skin.button), GUILayout.Height(30))) s.Test();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Output", GUILayout.Width(70));
            if (s.LastOutput.HasValue) LiquidRow(s.LastOutput.Value);
            else GUILayout.Label("—");
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            MachineView.AnimateTests = GUILayout.Toggle(MachineView.AnimateTests, "Animate tests (any key skips)");
            GUILayout.Label("Arrows select (past the left/right edge: manual/logbook) · W/S (A/D) turn or push · Space use/centre\nZ undo · R reset · T test · Q manual · E logbook (pad: LT/RT)\nBook up: ←→ pages · ↑↓ entry · Del remove · Esc/B close");
            GUILayout.EndArea();
        }

        private static void LiquidRow(Liquid l)
        {
            var old = GUI.color;
            GUI.color = Art.LiquidColor(l.Color);
            GUILayout.Box(Texture2D.whiteTexture, GUILayout.Width(18), GUILayout.Height(18));
            GUI.color = old;
            for (int p = 0; p < Liquid.PropertyCount; p++)
                GUILayout.Label($"{PropNames[p]} {l[(LiquidProperty)p]}", GUILayout.Width(44));
        }

        private static GUIStyle Rich(GUIStyle baseStyle = null) => new(baseStyle ?? GUI.skin.box) { richText = true };
    }
}
