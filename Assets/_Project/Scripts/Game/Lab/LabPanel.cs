using System;
using System.IO;
using System.Linq;
using Contraption.Core;
using UnityEngine;

namespace Contraption.Game
{
    // Developer panel (IMGUI): generator settings, metrics, debugging and saving.
    public sealed class LabPanel
    {
        public const float Width = 400f;
        private Vector2 _scroll;
        private bool _showSettings = true, _showRules, _showLevels;
        private string _solutionText = "";

        public void Draw(Rect area, MachineLab lab)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("<b>MACHINE LAB</b>  (F1 hides)", Rich());

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(40));
            lab.SeedText = GUILayout.TextField(lab.SeedText);
            if (GUILayout.Button("◀", GUILayout.Width(26))) lab.StepSeed(-1);
            if (GUILayout.Button("▶", GUILayout.Width(26))) lab.StepSeed(1);
            if (GUILayout.Button("Random", GUILayout.Width(64))) lab.RandomSeed();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("<b>GENERATE</b>", Rich(GUI.skin.button), GUILayout.Height(28))) lab.Generate();
            if (lab.Kits != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kit", GUILayout.Width(40));
                for (int i = 0; i < lab.Kits.Length; i++)
                    if (GUILayout.Toggle(lab.KitIndex == i, lab.Kits[i].DisplayName, GUI.skin.button) && lab.KitIndex != i) lab.SetKit(i);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            _showSettings = GUILayout.Toggle(_showSettings, "Generator settings");
            if (GUILayout.Button("Reset to defaults", GUILayout.Width(130))) lab.ResetSettings();
            GUILayout.EndHorizontal();
            if (_showSettings) DrawSettings(lab.Settings);

            GUILayout.Space(6);
            GUILayout.Label("<b>RESULT</b>", Rich());
            GUILayout.Label(lab.Status);
            var d = lab.Report;
            if (d != null)
            {
                GUILayout.Label($"Score {d.Score:F1} · solution {d.SolutionLength} moves");
                GUILayout.Label($"States {d.ReachableStates} · goal {d.GoalStates} · traps {d.TrapStates}");
                GUILayout.Label($"Registers {d.Registers} · controls {d.Controls} · conditional {d.ConditionalEffects} · ops {d.PipelineOps}");
            }

            if (lab.Session != null)
            {
                var session = lab.Session;
                GUILayout.Space(6);
                GUILayout.Label("<b>DEBUG</b>", Rich());
                GUILayout.Label($"State [{string.Join(",", session.State)}] · moves {session.Moves}");
                GUILayout.Label(lab.GoalDistance < 0 ? "<color=red><b>TRAPPED</b></color> — goal unreachable, reset (R)" : $"Distance to goal: {lab.GoalDistance}", Rich());
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Hint step")) lab.HintStep();
                if (GUILayout.Button("Show solution")) _solutionText = lab.SolutionText();
                if (GUILayout.Button("Current → start")) lab.UseCurrentAsStart();
                GUILayout.EndHorizontal();
                if (_solutionText.Length > 0) GUILayout.Label(_solutionText);

                _showRules = GUILayout.Toggle(_showRules, "Reveal rules");
                if (_showRules) GUILayout.TextArea(MachineDescriber.Describe(session.Definition));

                GUILayout.Space(6);
                GUILayout.Label("<b>SAVE</b>", Rich());
                GUILayout.BeginHorizontal();
                session.Definition.Name = GUILayout.TextField(session.Definition.Name);
                if (GUILayout.Button("Save", GUILayout.Width(60))) lab.Save();
                GUILayout.EndHorizontal();
            }

            _showLevels = GUILayout.Toggle(_showLevels, "Saved levels");
            if (_showLevels)
                foreach (var path in LevelStore.List())
                    if (GUILayout.Button(Path.GetFileNameWithoutExtension(path))) lab.LoadFile(path);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawSettings(GeneratorSettings s)
        {
            Range("Registers", ref s.MinRegisters, ref s.MaxRegisters, 1, 6);
            Range("Controls", ref s.MinControls, ref s.MaxControls, 1, 8);
            Range("Solution length", ref s.MinSolutionLength, ref s.MaxSolutionLength, 1, 30);
            s.InputPorts = Int("Input ports", s.InputPorts, 1, 3);
            s.InputPoolSize = Int("Input pool", s.InputPoolSize, 1, 8);
            s.ManualEntries = Int("Manual entries", s.ManualEntries, 1, 6);
            s.ExtraOps = Int("Extra pipeline ops", s.ExtraOps, 0, 6);
            s.Displays = Int("Displays (min)", s.Displays, 1, 8);
            s.MaxDeltasPerEffect = Int("Max deltas per effect", s.MaxDeltasPerEffect, 1, 4);
            s.ConditionalChance = Pct("Conditional chance", s.ConditionalChance);
            s.WrapChance = Pct("Wrap chance", s.WrapChance);
            s.CombinedDisplayChance = Pct("Combined display", s.CombinedDisplayChance);
            s.InvertedDisplayChance = Pct("Inverted display", s.InvertedDisplayChance);
            s.MaxGoalFraction = Pct("Max goal fraction", s.MaxGoalFraction);
            s.MaxTrapRatio = Pct("Max trap ratio", s.MaxTrapRatio);
            Kinds("Controls", s.ControlKinds);
            Kinds("Displays", s.DisplayKinds);
            Kinds("Ops", s.OpKinds);
        }

        private static int Int(string label, int v, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {v}", GUILayout.Width(170));
            v = Mathf.RoundToInt(GUILayout.HorizontalSlider(v, min, max));
            GUILayout.EndHorizontal();
            return v;
        }

        private static void Range(string label, ref int min, ref int max, int lo, int hi)
        {
            min = Int(label + " min", min, lo, hi);
            max = Mathf.Max(min, Int(label + " max", max, lo, hi));
        }

        private static double Pct(string label, double v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {v:P0}", GUILayout.Width(170));
            v = Math.Round(GUILayout.HorizontalSlider((float)v, 0f, 1f), 2);
            GUILayout.EndHorizontal();
            return v;
        }

        private static void Kinds<T>(string label, System.Collections.Generic.List<T> list) where T : struct, Enum
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60));
            foreach (var k in Enum.GetValues(typeof(T)).Cast<T>())
            {
                bool on = list.Contains(k);
                bool now = GUILayout.Toggle(on, k.ToString());
                if (now && !on) { list.Add(k); list.Sort(); }
                else if (!now && on && list.Count > 1) list.Remove(k);
            }
            GUILayout.EndHorizontal();
        }

        private static GUIStyle Rich() => new(GUI.skin.label) { richText = true };
        private static GUIStyle Rich(GUIStyle b) => new(b) { richText = true };
    }
}
