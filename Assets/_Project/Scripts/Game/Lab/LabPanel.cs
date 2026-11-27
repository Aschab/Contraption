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
        private bool _showSettings = true, _showRules, _showLevels, _showLayout = true;
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

            if (lab.Session != null) DrawLayout(lab);

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
            Range("Rows", ref s.MinRows, ref s.MaxRows, 2, 5);
            s.EmptyCells = Pct("Empty cells", s.EmptyCells);
            s.LargePartChance = Pct("Large parts", s.LargePartChance);
            Kinds("Controls", s.ControlKinds);
            Kinds("Displays", s.DisplayKinds);
            Kinds("Ops", s.OpKinds);
        }

        private void DrawLayout(MachineLab lab)
        {
            var l = lab.Session.Definition.Layout;
            GUILayout.Space(6);
            _showLayout = GUILayout.Toggle(_showLayout, "<b>LAYOUT</b>  (saved with the level)", Rich(GUI.skin.toggle));
            if (!_showLayout) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New layout")) lab.NewLayout();
            if (GUILayout.Button("New decor")) lab.NewDecor();
            bool steps = GUILayout.Toggle(l.ShowSteps, "Show steps");
            GUILayout.EndHorizontal();
            if (steps != l.ShowSteps) { l.ShowSteps = steps; lab.RebuildView(); }

            var names = Array.ConvertAll(Palettes.All, c => c.Name);
            GUILayout.BeginHorizontal();
            if (Cycle("Colors", l.Colors?.Name ?? names[0], names, out var scheme))
                Recolor(lab, l, Array.Find(Palettes.All, c => c.Name == scheme));
            if (GUILayout.Button("Shuffle tints", GUILayout.Width(100)))
                Recolor(lab, l, l.Colors ?? Palettes.All[0]);
            GUILayout.EndHorizontal();
            if (Cycle("Top", l.TopStyle, PartStyles.TopStyles, out var top)) { l.TopStyle = top; lab.RebuildView(); }
            if (Cycle("Base", l.BaseStyle, PartStyles.BaseStyles, out var bottom)) { l.BaseStyle = bottom; lab.RebuildView(); }
            if (Cycle("Inputs", l.Inputs[0].Style, PartStyles.InputContainers, out var input))
            {
                foreach (var c in l.Inputs) c.Style = input;
                lab.RebuildView();
            }
            if (Cycle("Test", l.TestStyle, PartStyles.TestTriggers, out var test)) { l.TestStyle = test; lab.RebuildView(); }
            if (Cycle("Output", l.Output.Style, PartStyles.OutputContainers, out var output)) { l.Output.Style = output; lab.RebuildView(); }

            var p = lab.SelectedPlacement(out string label, out var styles);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(26))) lab.SelectedPart--;
            GUILayout.Label($"{label} · {p.Style} at {p.X},{p.Y}");
            if (GUILayout.Button("▶", GUILayout.Width(26))) lab.SelectedPart++;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Style", GUILayout.Width(40));
            if (GUILayout.Button("◀", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryRestyle(l, p, styles, -1), "Restyled");
            if (GUILayout.Button("▶", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryRestyle(l, p, styles, 1), "Restyled");
            GUILayout.Label("Move", GUILayout.Width(40));
            if (GUILayout.Button("←", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryMove(l, p, -1, 0), "Moved");
            if (GUILayout.Button("↑", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryMove(l, p, 0, 1), "Moved");
            if (GUILayout.Button("↓", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryMove(l, p, 0, -1), "Moved");
            if (GUILayout.Button("→", GUILayout.Width(26))) Edit(lab, LayoutEditor.TryMove(l, p, 1, 0), "Moved");
            GUILayout.EndHorizontal();
        }

        private static void Recolor(MachineLab lab, MachineLayout l, MachineColors scheme)
        {
            LayoutGenerator.Recolor(new Rng((ulong)UnityEngine.Random.Range(1, int.MaxValue)), l, scheme);
            lab.RebuildView();
        }

        private static void Edit(MachineLab lab, bool ok, string what)
        {
            lab.EditStatus(ok, what);
            if (ok) lab.RebuildView();
        }

        private static bool Cycle(string label, string current, string[] options, out string next)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(50));
            int i = Mathf.Max(0, Array.IndexOf(options, current)), dir = 0;
            if (GUILayout.Button("◀", GUILayout.Width(26))) dir = -1;
            GUILayout.Label(current, GUILayout.Width(110));
            if (GUILayout.Button("▶", GUILayout.Width(26))) dir = 1;
            GUILayout.EndHorizontal();
            next = options[Values.Wrap(i + dir, options.Length)];
            return dir != 0;
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
