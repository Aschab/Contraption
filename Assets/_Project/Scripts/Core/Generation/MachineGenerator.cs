using System;
using System.Collections.Generic;

namespace Contraption.Core
{
    public sealed class GenerationResult
    {
        public MachineDefinition Definition;
        public SolveResult Solve;
        public DifficultyReport Difficulty;
        public int Attempts;
        // Rejection counts per reason, for tuning settings in the lab.
        public SortedDictionary<string, int> Rejections = new();
        public bool Success => Definition != null;
    }

    public static class MachineGenerator
    {
        public const int Version = 1;

        public static GenerationResult Generate(ulong seed, GeneratorSettings s)
        {
            var rng = new Rng(seed);
            var result = new GenerationResult();

            for (int attempt = 1; attempt <= s.MaxAttempts; attempt++)
            {
                result.Attempts = attempt;
                var def = BuildCandidate(rng, s);
                def.Seed = seed;
                def.Name = $"Machine {seed}";

                var layout = new StateLayout(def);
                if (layout.Size > s.MaxStates)
                {
                    Reject(result, "state space too large");
                    continue;
                }

                var goal = Solver.BuildGoalTable(def, layout);
                int goalCount = 0;
                foreach (bool g in goal) if (g) goalCount++;
                if (goalCount > s.MaxGoalFraction * goal.Length)
                {
                    Reject(result, "manual too permissive");
                    continue;
                }

                for (int t = 0; t < s.StartTriesPerMachine; t++)
                {
                    def.StartState = RandomStart(rng, def, layout, goal);
                    var solve = Solver.Solve(def, layout, goal, s.MaxStates);
                    if (!solve.Solvable) { Reject(result, "unsolvable from start"); continue; }
                    int len = solve.Solution.Count;
                    if (len < s.MinSolutionLength) { Reject(result, "solution too short"); continue; }
                    if (len > s.MaxSolutionLength) { Reject(result, "solution too long"); continue; }
                    if (solve.TrapStates > s.MaxTrapRatio * solve.ReachableStates) { Reject(result, "too many traps"); continue; }

                    result.Definition = def;
                    result.Solve = solve;
                    result.Difficulty = Difficulty.Evaluate(def, solve);
                    return result;
                }
            }
            return result;
        }

        private static void Reject(GenerationResult r, string reason)
        {
            r.Rejections.TryGetValue(reason, out int n);
            r.Rejections[reason] = n + 1;
        }

        private static MachineDefinition BuildCandidate(Rng rng, GeneratorSettings s)
        {
            var def = new MachineDefinition { GeneratorVersion = Version, InputPorts = s.InputPorts };

            int regCount = rng.RangeInclusive(s.MinRegisters, s.MaxRegisters);
            for (int i = 0; i < regCount; i++)
                def.Registers.Add(new RegisterDef { Overflow = rng.Chance(s.WrapChance) ? Overflow.Wrap : Overflow.Clamp });

            BuildControls(rng, s, def);
            BuildPipeline(rng, s, def);
            BuildManual(rng, s, def);
            BuildDisplays(rng, s, def);
            return def;
        }

        private static void BuildControls(Rng rng, GeneratorSettings s, MachineDefinition def)
        {
            int count = rng.RangeInclusive(s.MinControls, s.MaxControls);
            for (int i = 0; i < count; i++)
            {
                var kind = rng.Pick(s.ControlKinds);
                var c = new ControlDef { Kind = kind };
                switch (kind)
                {
                    case ControlKind.Button: c.Positions = 1; break;
                    case ControlKind.Lever: c.Positions = 2; break;
                    case ControlKind.Dial: c.Positions = rng.RangeInclusive(4, 6); c.Wraps = true; break;
                    case ControlKind.Knob: c.Positions = rng.RangeInclusive(3, 6); break;
                }
                def.Controls.Add(c);
            }

            var conditionSlots = new List<int>();
            for (int r = 0; r < def.Registers.Count; r++) conditionSlots.Add(r);
            for (int i = 0; i < def.Controls.Count; i++)
                if (def.Controls[i].Positions > 1) conditionSlots.Add(def.ControlSlot(i));

            foreach (var c in def.Controls)
            {
                if (c.Kind == ControlKind.Button)
                {
                    c.Actions.Add(RandomAction(rng, s, def, ActionKind.Press, conditionSlots));
                    continue;
                }
                var inc = RandomAction(rng, s, def, ActionKind.Increment, conditionSlots);
                c.Actions.Add(inc);
                // Dials/knobs often undo themselves exactly; levers rarely do.
                bool mirror = c.Kind != ControlKind.Lever && rng.Chance(0.5);
                c.Actions.Add(mirror ? Mirror(inc) : RandomAction(rng, s, def, ActionKind.Decrement, conditionSlots));
            }

            // Every register must be reachable by some control.
            var touched = new bool[def.Registers.Count];
            foreach (var c in def.Controls)
                foreach (var a in c.Actions)
                    foreach (var e in a.Effects)
                        foreach (var d in e.Deltas) touched[d.Register] = true;
            for (int r = 0; r < touched.Length; r++)
            {
                if (touched[r]) continue;
                var action = rng.Pick(rng.Pick(def.Controls).Actions);
                action.Effects[0].Deltas.Add(new Delta { Register = r, Amount = RandomAmount(rng) });
            }
        }

        private static ControlAction RandomAction(Rng rng, GeneratorSettings s, MachineDefinition def, ActionKind kind, List<int> conditionSlots)
        {
            var action = new ControlAction { Kind = kind };
            action.Effects.Add(new Effect { Deltas = RandomDeltas(rng, s, def) });
            if (rng.Chance(s.ConditionalChance))
            {
                int slot = rng.Pick(conditionSlots);
                action.Effects.Add(new Effect { When = { RandomClause(rng, def, slot) }, Deltas = RandomDeltas(rng, s, def) });
            }
            return action;
        }

        private static ControlAction Mirror(ControlAction a)
        {
            var m = new ControlAction { Kind = ActionKind.Decrement };
            foreach (var e in a.Effects)
            {
                var copy = new Effect { When = new List<Clause>(e.When) };
                foreach (var d in e.Deltas) copy.Deltas.Add(new Delta { Register = d.Register, Amount = -d.Amount });
                m.Effects.Add(copy);
            }
            return m;
        }

        private static List<Delta> RandomDeltas(Rng rng, GeneratorSettings s, MachineDefinition def)
        {
            var regs = new List<int>();
            for (int r = 0; r < def.Registers.Count; r++) regs.Add(r);
            rng.Shuffle(regs);
            int n = rng.RangeInclusive(1, Math.Min(s.MaxDeltasPerEffect, regs.Count));
            var deltas = new List<Delta>();
            for (int i = 0; i < n; i++) deltas.Add(new Delta { Register = regs[i], Amount = RandomAmount(rng) });
            return deltas;
        }

        private static int RandomAmount(Rng rng) => rng.Sign() * (rng.Chance(0.7) ? 1 : 2);

        private static Clause RandomClause(Rng rng, MachineDefinition def, int slot)
        {
            if (slot < def.Registers.Count)
                return new Clause
                {
                    Slot = slot,
                    Op = rng.Chance(0.5) ? Comparison.GreaterOrEqual : Comparison.Less,
                    Value = rng.RangeInclusive(1, Values.Max),
                };
            int positions = def.Controls[slot - def.Registers.Count].Positions;
            return new Clause
            {
                Slot = slot,
                Op = rng.Chance(0.7) ? Comparison.Equal : Comparison.NotEqual,
                Value = rng.Range(0, positions),
            };
        }

        private static void BuildPipeline(Rng rng, GeneratorSettings s, MachineDefinition def)
        {
            var regs = new List<int>();
            for (int r = 0; r < def.Registers.Count; r++) regs.Add(r);
            for (int i = 0; i < s.ExtraOps; i++) regs.Add(rng.Range(0, def.Registers.Count));
            rng.Shuffle(regs);

            foreach (int r in regs)
            {
                var target = (LiquidProperty)rng.Range(0, Liquid.PropertyCount);
                var source = (LiquidProperty)Values.Wrap((int)target + rng.RangeInclusive(1, Liquid.PropertyCount - 1), Liquid.PropertyCount);
                def.Pipeline.Add(new TransferOp
                {
                    Kind = rng.Pick(s.OpKinds),
                    Target = target,
                    Source = source,
                    Register = r,
                    Threshold = rng.RangeInclusive(1, 3),
                });
            }
        }

        private static void BuildManual(Rng rng, GeneratorSettings s, MachineDefinition def)
        {
            for (int i = 0; i < s.InputPoolSize; i++) def.InputPool.Add(Liquid.Random(rng));

            def.ReferenceSolution = new int[def.Registers.Count];
            for (int r = 0; r < def.ReferenceSolution.Length; r++) def.ReferenceSolution[r] = rng.RangeInclusive(Values.Min, Values.Max);

            var used = new HashSet<string>();
            for (int e = 0; e < s.ManualEntries; e++)
            {
                var entry = new ManualEntry();
                // Prefer distinct input combinations; give up after a few tries on tiny pools.
                for (int tries = 0; tries < 8; tries++)
                {
                    entry.Inputs.Clear();
                    for (int p = 0; p < def.InputPorts; p++) entry.Inputs.Add(rng.Pick(def.InputPool));
                    if (used.Add(string.Join("|", entry.Inputs))) break;
                }
                entry.Output = MachineRules.Process(def, def.ReferenceSolution, entry.Inputs);
                def.Manual.Add(entry);
            }
        }

        private static void BuildDisplays(Rng rng, GeneratorSettings s, MachineDefinition def)
        {
            int regCount = def.Registers.Count;
            int hidden = rng.RangeInclusive(0, Math.Min(s.MaxHiddenRegisters, regCount - 1));
            var visible = new List<int>();
            for (int r = 0; r < regCount; r++) visible.Add(r);
            rng.Shuffle(visible);
            visible.RemoveRange(0, hidden);

            int count = Math.Max(s.Displays, visible.Count);
            for (int i = 0; i < count; i++)
            {
                var d = new DisplayDef { Kind = rng.Pick(s.DisplayKinds) };
                int primary = i < visible.Count ? visible[i] : rng.Pick(visible);
                int weight = rng.Chance(s.InvertedDisplayChance) ? -1 : 1;
                d.Terms.Add(new Term { Register = primary, Weight = weight });
                if (visible.Count > 1 && rng.Chance(s.CombinedDisplayChance))
                {
                    int other = primary;
                    while (other == primary) other = rng.Pick(visible);
                    d.Terms.Add(new Term { Register = other, Weight = rng.Sign() });
                }
                if (weight < 0 && d.Terms.Count == 1) d.Offset = Values.Max; // inverted gauge still reads 0..5

                MachineRules.DisplayRange(d, out int min, out int max);
                d.Op = Comparison.GreaterOrEqual;
                d.Threshold = rng.RangeInclusive(min + 1, Math.Max(min + 1, max));
                def.Displays.Add(d);
            }
        }

        private static int[] RandomStart(Rng rng, MachineDefinition def, StateLayout layout, bool[] goal)
        {
            var state = new int[def.SlotCount()];
            for (int tries = 0; tries < 32; tries++)
            {
                for (int r = 0; r < def.Registers.Count; r++) state[r] = rng.RangeInclusive(Values.Min, Values.Max);
                for (int c = 0; c < def.Controls.Count; c++) state[def.ControlSlot(c)] = rng.Range(0, def.Controls[c].Positions);
                if (!goal[layout.RegisterCode(layout.Encode(state))]) break;
            }
            return state;
        }
    }
}
