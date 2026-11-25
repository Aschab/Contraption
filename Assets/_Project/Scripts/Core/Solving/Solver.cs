using System.Collections.Generic;

namespace Contraption.Core
{
    public sealed class SolveResult
    {
        public bool Solvable;
        public bool Truncated;
        public List<Move> Solution = new();
        public int ReachableStates;
        public int GoalStates;
        // Reachable states from which no goal can be reached. -1 when not computed (truncated).
        public int TrapStates = -1;
    }

    public static class Solver
    {
        // goal[registerCode] == true when those register values reproduce every manual entry.
        public static bool[] BuildGoalTable(MachineDefinition def, StateLayout layout)
        {
            var goal = new bool[layout.RegisterSpace];
            var full = new int[layout.SlotCount];
            for (int code = 0; code < goal.Length; code++)
            {
                layout.Decode(code, full);
                goal[code] = MachineRules.MatchesManual(def, full);
            }
            return goal;
        }

        public static SolveResult Solve(MachineDefinition def, int maxStates = 500_000) =>
            Solve(def, new StateLayout(def), null, maxStates);

        public static SolveResult Solve(MachineDefinition def, StateLayout layout, bool[] goal, int maxStates, int[] start = null)
        {
            goal ??= BuildGoalTable(def, layout);
            var moves = MachineRules.AllMoves(def);
            var result = new SolveResult();

            var index = new Dictionary<long, int>();
            var codes = new List<long>();
            var parent = new List<int>();
            var parentMove = new List<int>();
            var edgeFrom = new List<int>();
            var edgeTo = new List<int>();

            long startCode = layout.Encode(start ?? def.StartState);
            index[startCode] = 0;
            codes.Add(startCode);
            parent.Add(-1);
            parentMove.Add(-1);

            var cur = new int[layout.SlotCount];
            var next = new int[layout.SlotCount];
            int firstGoal = -1;

            for (int head = 0; head < codes.Count; head++)
            {
                long code = codes[head];
                if (goal[layout.RegisterCode(code)])
                {
                    result.GoalStates++;
                    if (firstGoal < 0) firstGoal = head;
                }
                layout.Decode(code, cur);
                for (int m = 0; m < moves.Count; m++)
                {
                    if (!MachineRules.TryApply(def, cur, moves[m], next)) continue;
                    long nc = layout.Encode(next);
                    if (nc == code) continue;
                    if (!index.TryGetValue(nc, out int j))
                    {
                        if (codes.Count >= maxStates)
                        {
                            result.Truncated = true;
                            continue;
                        }
                        j = codes.Count;
                        index[nc] = j;
                        codes.Add(nc);
                        parent.Add(head);
                        parentMove.Add(m);
                    }
                    edgeFrom.Add(head);
                    edgeTo.Add(j);
                }
            }

            result.ReachableStates = codes.Count;
            result.Solvable = firstGoal >= 0;
            if (result.Solvable)
            {
                for (int s = firstGoal; parent[s] >= 0; s = parent[s]) result.Solution.Add(moves[parentMove[s]]);
                result.Solution.Reverse();
            }
            if (!result.Truncated) result.TrapStates = CountTraps(codes, layout, goal, edgeFrom, edgeTo);
            return result;
        }

        private static int CountTraps(List<long> codes, StateLayout layout, bool[] goal, List<int> from, List<int> to)
        {
            int n = codes.Count;
            // Reverse adjacency in CSR form.
            var offsets = new int[n + 1];
            foreach (int t in to) offsets[t + 1]++;
            for (int i = 0; i < n; i++) offsets[i + 1] += offsets[i];
            var fill = (int[])offsets.Clone();
            var preds = new int[from.Count];
            for (int e = 0; e < from.Count; e++) preds[fill[to[e]]++] = from[e];

            var canReach = new bool[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (!goal[layout.RegisterCode(codes[i])]) continue;
                canReach[i] = true;
                queue.Enqueue(i);
            }
            int reached = queue.Count;
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                for (int k = offsets[s]; k < offsets[s + 1]; k++)
                {
                    int p = preds[k];
                    if (canReach[p]) continue;
                    canReach[p] = true;
                    reached++;
                    queue.Enqueue(p);
                }
            }
            return n - reached;
        }
    }
}
