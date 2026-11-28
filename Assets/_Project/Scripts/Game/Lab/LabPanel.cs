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
        private bool _showSettings = true, _showRules, _showLevels, _showLayout = true, _showHints = true, _showSounds;
        private string _solutionText = "";

        public void Draw(Rect area, MachineLab lab)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>MACHINE LAB</b>  (F1 hides)", Rich());
            if (GUILayout.Toggle(lab.PanelsHidden, "Hide side panels")) lab.PanelsHidden = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(40));
            lab.SeedText = GUILayout.TextField(lab.SeedText);
            if (GUILayout.Button("◀", GUILayout.Width(26))) lab.StepSeed(-1);
            if (GUILayout.Button("▶", GUILayout.Width(26))) lab.StepSeed(1);
            if (GUILayout.Button("Random", GUILayout.Width(64))) lab.RandomSeed();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<b>GENERATE</b>", Rich(GUI.skin.button), GUILayout.Height(28))) lab.Generate();
            if (GUILayout.Button("Intro", GUILayout.Width(60), GUILayout.Height(28))) lab.PlayIntro();
            GUILayout.EndHorizontal();
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
            if (lab.Session != null) DrawHints(lab);

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

            _showSounds = GUILayout.Toggle(_showSounds, "<b>SOUND REVIEW</b>  (marks → ArtSource/Audio/packs/review.txt)", Rich(GUI.skin.toggle));
            if (_showSounds) DrawSoundReview(lab);

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
            bool ledges = GUILayout.Toggle(l.Ledges, "Ledges");
            GUILayout.EndHorizontal();
            if (steps != l.ShowSteps) { l.ShowSteps = steps; lab.RebuildView(); }
            if (ledges != l.Ledges) { l.Ledges = ledges; lab.RebuildView(); }

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
            if (Cycle("Depth", DepthNames[p.Depth], DepthNames, out var depth)) { p.Depth = Array.IndexOf(DepthNames, depth); lab.RebuildView(); }
        }

        // Replay recent sounds and mark single files keep / purge (purged ones stop playing at once).
        private static void DrawSoundReview(MachineLab lab)
        {
            GUILayout.BeginHorizontal();
            var track = Music.Current;
            GUILayout.Label("Music: " + (track != null ? track.name : "—"), GUILayout.Width(220));
            if (GUILayout.Button("Next", GUILayout.Width(44))) Music.Next(lab);
            if (track != null) Marks(track.name);
            GUILayout.EndHorizontal();
            GUILayout.Label("Loops playing:");
            foreach (var clip in Sfx.PlayingLoops()) SoundRow(clip, false);
            GUILayout.Label("Recent (newest first):");
            foreach (var clip in Sfx.Recent.ToArray()) SoundRow(clip, true);
        }

        private static void SoundRow(AudioClip clip, bool replay)
        {
            GUILayout.BeginHorizontal();
            if (replay && GUILayout.Button("▶", GUILayout.Width(24))) Sfx.Replay(clip);
            string source = SoundReview.Source(clip.name);
            GUILayout.Label(new GUIContent(clip.name, source), GUILayout.Width(replay ? 200 : 228));
            Marks(clip.name);
            GUILayout.EndHorizontal();
            if (source.Length > 0) GUILayout.Label("<size=10>   " + source + "</size>", Rich());
        }

        private static void Marks(string clip)
        {
            string mark = SoundReview.MarkOf(clip);
            var old = GUI.color;
            GUI.color = mark == "keep" ? Color.green : old;
            if (GUILayout.Button("Keep", GUILayout.Width(44))) SoundReview.Toggle(clip, "keep");
            GUI.color = mark == "purge" ? new Color(1f, 0.4f, 0.4f) : old;
            if (GUILayout.Button("Purge", GUILayout.Width(48))) SoundReview.Toggle(clip, "purge");
            GUI.color = old;
        }

        private static readonly string[] LabelNames = { "None", "Trait", "Full" };

        // Per step: the cable into it, its label level, and where it lives (display or the pipe for Exchange).
        private void DrawHints(MachineLab lab)
        {
            var def = lab.Session.Definition;
            int n = def.Pipeline.Count;
            GUILayout.Space(6);
            _showHints = GUILayout.Toggle(_showHints, "<b>HINTS</b>  (labels and rear cables, saved with the level)", Rich(GUI.skin.toggle));
            if (!_showHints) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All")) { def.Hints = MachineHints.All(n); lab.RebuildView(); }
            if (GUILayout.Button("Traits + cables")) { def.Hints = MachineHints.All(n, MachineHints.LabelTrait); lab.RebuildView(); }
            if (GUILayout.Button("None")) { def.Hints = null; lab.RebuildView(); }
            GUILayout.EndHorizontal();
            var h = def.Hints;
            if (h == null) return;
            h.Fit(n);

            bool changed = false;
            GUILayout.BeginHorizontal();
            bool back = GUILayout.Toggle(h.Back, "Rear view");
            if (back != h.Back) { h.Back = back; changed = true; }
            if (lab.HasBack && GUILayout.Button("Turn around (F)", GUILayout.Width(130))) lab.TurnMachine();
            GUILayout.EndHorizontal();
            GUILayout.Label("cable in · step · home · label");
            for (int s = 0; s <= n; s++)
            {
                GUILayout.BeginHorizontal();
                bool link = GUILayout.Toggle(h.Links[s], "", GUILayout.Width(18));
                if (link != h.Links[s]) { h.Links[s] = link; changed = true; }
                if (s == n)
                {
                    GUILayout.Label("→ end");
                    GUILayout.EndHorizontal();
                    break;
                }
                int home = MachineHints.Home(def, s);
                GUILayout.Label($"{s + 1}. {MachineDescriber.Step(def.Pipeline[s])} · {(home < 0 ? "pipe" : "D" + (home + 1))}", GUILayout.Width(250));
                if (GUILayout.Button(LabelNames[h.Labels[s]], GUILayout.Width(50))) { h.Labels[s] = (h.Labels[s] + 1) % LabelNames.Length; changed = true; }
                GUILayout.EndHorizontal();
            }
            if (changed) lab.RebuildView();
        }

        private static readonly string[] DepthNames = { "Flush", "Raised", "Framed" };

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
