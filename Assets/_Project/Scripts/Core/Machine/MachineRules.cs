using System;
using System.Collections.Generic;

namespace Contraption.Core
{
    public static class MachineRules
    {
        public static bool TryApply(MachineDefinition def, int[] state, Move move, int[] result)
        {
            var control = def.Controls[move.Control];
            var action = control.Find(move.Action);
            if (action == null) return false;

            int slot = def.ControlSlot(move.Control);
            int pos = state[slot];
            if (ControlDef.IsJoystickAction(move.Action))
            {
                pos = ControlDef.JoystickTarget(move.Action);
                if (pos == state[slot]) return false;
            }
            else if (move.Action == ActionKind.Increment) pos++;
            else if (move.Action == ActionKind.Decrement) pos--;
            if (pos < 0 || pos >= control.Positions)
            {
                if (!control.Wraps) return false;
                pos = Values.Wrap(pos, control.Positions);
            }

            int regCount = def.Registers.Count;
            Span<int> acc = stackalloc int[regCount];
            foreach (var effect in action.Effects)
            {
                if (!Holds(effect.When, state)) continue;
                foreach (var d in effect.Deltas) acc[d.Register] += d.Amount;
            }

            Array.Copy(state, result, state.Length);
            for (int r = 0; r < regCount; r++)
            {
                if (acc[r] == 0) continue;
                int v = state[r] + acc[r];
                result[r] = def.Registers[r].Overflow == Overflow.Wrap ? Values.Wrap(v, Values.Levels) : Values.Clamp(v);
            }
            result[slot] = pos;
            return true;
        }

        public static bool Holds(List<Clause> clauses, int[] state)
        {
            foreach (var c in clauses)
                if (!Compare(state[c.Slot], c.Op, c.Value)) return false;
            return true;
        }

        public static bool Compare(int a, Comparison op, int b) => op switch
        {
            Comparison.Equal => a == b,
            Comparison.NotEqual => a != b,
            Comparison.Less => a < b,
            _ => a >= b,
        };

        public static int DisplayValue(DisplayDef d, int[] state)
        {
            int v = d.Offset;
            foreach (var t in d.Terms) v += t.Weight * state[t.Register];
            return v;
        }

        public static void DisplayRange(DisplayDef d, out int min, out int max)
        {
            min = max = d.Offset;
            foreach (var t in d.Terms)
            {
                int a = t.Weight * Values.Min, b = t.Weight * Values.Max;
                min += Math.Min(a, b);
                max += Math.Max(a, b);
            }
        }

        public static bool LightOn(DisplayDef d, int[] state) => Compare(DisplayValue(d, state), d.Op, d.Threshold);

        // The distinguishable reading of a display: light on/off, lit bar segments, gauge value, glyph index.
        public static int Readout(DisplayDef d, int[] state)
        {
            int v = DisplayValue(d, state);
            switch (d.Kind)
            {
                case DisplayKind.Light: return LightOn(d, state) ? 1 : 0;
                case DisplayKind.Glyph: return Values.Wrap(v, DisplayDef.GlyphCount);
                case DisplayKind.Bar:
                    DisplayRange(d, out int min, out int max);
                    // Integer round-half-up of (v-min)/(max-min)*segments.
                    return max > min ? ((v - min) * DisplayDef.BarSegments * 2 + (max - min)) / ((max - min) * 2) : 0;
                default: return v;
            }
        }

        // Only registers (state[0..R)) are read, so a register-only array is a valid argument.
        public static Liquid Process(MachineDefinition def, int[] state, IReadOnlyList<Liquid> inputs)
        {
            var l = Liquid.Mix(inputs);
            foreach (var op in def.Pipeline)
            {
                int r = state[op.Register];
                int t = l[op.Target];
                switch (op.Kind)
                {
                    case OpKind.Add: t += r; break;
                    case OpKind.Subtract: t -= r; break;
                    case OpKind.Cap: t = Math.Min(t, r); break;
                    case OpKind.Raise: t = Math.Max(t, r); break;
                    case OpKind.Purge: if (r >= op.Threshold) t = 0; break;
                    case OpKind.Amplify: t = t * r / 2; break;
                    case OpKind.Siphon:
                        int amount = Math.Min(r, l[op.Source]);
                        l[op.Source] -= amount;
                        t += amount;
                        break;
                    case OpKind.Swap:
                    case OpKind.Exchange:
                        if (op.Kind == OpKind.Exchange || r >= op.Threshold)
                        {
                            int s = l[op.Source];
                            l[op.Source] = t;
                            t = s;
                        }
                        break;
                }
                l[op.Target] = Values.Clamp(t);
            }
            return l;
        }

        public static bool MatchesManual(MachineDefinition def, int[] state)
        {
            foreach (var entry in def.Manual)
                if (!Process(def, state, entry.Inputs).Equals(entry.Output)) return false;
            return true;
        }

        public static List<Move> AllMoves(MachineDefinition def)
        {
            var moves = new List<Move>();
            for (int c = 0; c < def.Controls.Count; c++)
                foreach (var a in def.Controls[c].Actions)
                    moves.Add(new Move(c, a.Kind));
            return moves;
        }
    }
}
