using System;

namespace Contraption.Core
{
    public sealed class DifficultyReport
    {
        public int SolutionLength;
        public int ReachableStates;
        public int GoalStates;
        public int TrapStates;
        public int Registers;
        public int HiddenRegisters;
        public int Controls;
        public int ConditionalEffects;
        public int PipelineOps;
        public double Score;
    }

    public static class Difficulty
    {
        // Heuristic; weights to be tuned against playtests.
        public static DifficultyReport Evaluate(MachineDefinition def, SolveResult solve)
        {
            var r = new DifficultyReport
            {
                SolutionLength = solve.Solution.Count,
                ReachableStates = solve.ReachableStates,
                GoalStates = solve.GoalStates,
                TrapStates = Math.Max(0, solve.TrapStates),
                Registers = def.Registers.Count,
                Controls = def.Controls.Count,
                PipelineOps = def.Pipeline.Count,
            };

            var seen = new bool[def.Registers.Count];
            foreach (var d in def.Displays)
                foreach (var t in d.Terms) seen[t.Register] = true;
            foreach (bool s in seen) if (!s) r.HiddenRegisters++;

            foreach (var c in def.Controls)
                foreach (var a in c.Actions)
                    foreach (var e in a.Effects)
                        if (e.When.Count > 0) r.ConditionalEffects++;

            double trapRatio = r.ReachableStates > 0 ? (double)r.TrapStates / r.ReachableStates : 0;
            r.Score = r.SolutionLength * 1.0
                      + Math.Log(Math.Max(1, r.ReachableStates), 2) * 0.5
                      - Math.Log(Math.Max(1, r.GoalStates), 2) * 0.5
                      + r.ConditionalEffects * 0.75
                      + r.HiddenRegisters * 1.5
                      + r.PipelineOps * 0.25
                      + trapRatio * 4.0;
            return r;
        }
    }
}
