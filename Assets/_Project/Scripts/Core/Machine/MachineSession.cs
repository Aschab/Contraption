using System;
using System.Collections.Generic;

namespace Contraption.Core
{
    public sealed class TestRecord
    {
        public int[] Inputs;
        public Liquid Output;
    }

    // One play-through of a machine: current state, undo history, selected inputs, test log (oldest first), win tracking.
    // The machine counts as fixed once every manual entry has been tested successfully without touching a control since.
    public sealed class MachineSession
    {
        private readonly Stack<int[]> _history = new();
        private int[] _state;
        private StateLayout _layout;
        private bool[] _goal;

        public MachineDefinition Definition { get; }
        public IReadOnlyList<int> State => _state;
        public int[] SelectedInputs { get; }
        public bool[] Verified { get; }
        public List<TestRecord> Log { get; } = new();
        public Liquid? LastOutput { get; private set; }
        public int Moves { get; private set; }
        public const int MaxLog = 200;
        public event Action Changed;
        // Raised by Test before Changed, so views can animate the run.
        public event Action Tested;

        public bool Solved => Array.TrueForAll(Verified, v => v);

        public MachineSession(MachineDefinition def)
        {
            Definition = def;
            _state = (int[])def.StartState.Clone();
            SelectedInputs = new int[def.InputPorts];
            Verified = new bool[def.Manual.Count];
        }

        public int[] CopyState() => (int[])_state.Clone();

        public bool CanApply(Move m) => MachineRules.TryApply(Definition, _state, m, new int[_state.Length]);

        public bool Apply(Move m)
        {
            var next = new int[_state.Length];
            if (!MachineRules.TryApply(Definition, _state, m, next)) return false;
            _history.Push(_state);
            _state = next;
            Moves++;
            StateChanged();
            return true;
        }

        public bool Undo()
        {
            if (_history.Count == 0) return false;
            _state = _history.Pop();
            StateChanged();
            return true;
        }

        public void Reset()
        {
            _history.Clear();
            _state = (int[])Definition.StartState.Clone();
            StateChanged();
        }

        public void DeleteLog(int index)
        {
            if (index < 0 || index >= Log.Count) return;
            Log.RemoveAt(index);
            Changed?.Invoke();
        }

        // The manual entry whose inputs match a log record (any order), or -1.
        public int ManualEntryFor(TestRecord record)
        {
            var inputs = new List<Liquid>();
            foreach (int i in record.Inputs) inputs.Add(Definition.InputPool[i]);
            for (int e = 0; e < Definition.Manual.Count; e++)
                if (SameMultiset(Definition.Manual[e].Inputs, inputs)) return e;
            return -1;
        }

        public void SelectInput(int port, int poolIndex)
        {
            SelectedInputs[port] = Values.Wrap(poolIndex, Definition.InputPool.Count);
            Changed?.Invoke();
        }

        public Liquid Test()
        {
            var inputs = new List<Liquid>();
            foreach (int i in SelectedInputs) inputs.Add(Definition.InputPool[i]);
            var output = MachineRules.Process(Definition, _state, inputs);

            for (int e = 0; e < Definition.Manual.Count; e++)
                if (SameMultiset(Definition.Manual[e].Inputs, inputs))
                    Verified[e] = output.Equals(Definition.Manual[e].Output);

            LastOutput = output;
            Log.Add(new TestRecord { Inputs = (int[])SelectedInputs.Clone(), Output = output });
            if (Log.Count > MaxLog) Log.RemoveAt(0);
            Tested?.Invoke();
            Changed?.Invoke();
            return output;
        }

        // Shortest remaining solution from the current state (lab/hints). Null if unsolvable from here.
        public List<Move> SolveFromHere(int maxStates = 500_000)
        {
            _layout ??= new StateLayout(Definition);
            _goal ??= Solver.BuildGoalTable(Definition, _layout);
            var r = Solver.Solve(Definition, _layout, _goal, maxStates, _state);
            return r.Solvable ? r.Solution : null;
        }

        private void StateChanged()
        {
            Array.Clear(Verified, 0, Verified.Length);
            LastOutput = null;
            Changed?.Invoke();
        }

        // Mixing is order-independent, so a manual entry matches any permutation of its inputs.
        private static bool SameMultiset(List<Liquid> a, List<Liquid> b)
        {
            if (a.Count != b.Count) return false;
            var used = new bool[b.Count];
            foreach (var x in a)
            {
                int j = 0;
                while (j < b.Count && (used[j] || !b[j].Equals(x))) j++;
                if (j == b.Count) return false;
                used[j] = true;
            }
            return true;
        }
    }
}
