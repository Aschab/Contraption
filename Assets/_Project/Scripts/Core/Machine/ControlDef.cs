using System.Collections.Generic;

namespace Contraption.Core
{
    public enum ControlKind { Button, Lever, Dial, Knob }
    public enum ActionKind { Press, Increment, Decrement }
    public enum Comparison { Equal, NotEqual, Less, GreaterOrEqual }

    public sealed class ControlDef
    {
        public ControlKind Kind;
        public int Positions = 1;
        public bool Wraps;
        public List<ControlAction> Actions = new();

        public ControlAction Find(ActionKind kind)
        {
            foreach (var a in Actions)
                if (a.Kind == kind) return a;
            return null;
        }
    }

    public sealed class ControlAction
    {
        public ActionKind Kind;
        public List<Effect> Effects = new();
    }

    // Applies Deltas when every clause holds on the state *before* the action.
    public sealed class Effect
    {
        public List<Clause> When = new();
        public List<Delta> Deltas = new();
    }

    public sealed class Clause
    {
        public int Slot;
        public Comparison Op;
        public int Value;
    }

    public struct Delta
    {
        public int Register;
        public int Amount;
    }

    public struct Move
    {
        public int Control;
        public ActionKind Action;

        public Move(int control, ActionKind action)
        {
            Control = control;
            Action = action;
        }

        public override string ToString() => $"{Action} #{Control}";
    }
}
