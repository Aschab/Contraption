using System.Collections.Generic;
using System.Text;

namespace Contraption.Core
{
    // Human-readable dump of a machine's internal rules, for the lab and debugging. Never shown to players.
    public static class MachineDescriber
    {
        public static string Describe(MachineDefinition def)
        {
            var sb = new StringBuilder();
            sb.Append("Registers: ");
            for (int r = 0; r < def.Registers.Count; r++) sb.Append($"R{r}({def.Registers[r].Overflow}) ");
            sb.AppendLine();

            for (int c = 0; c < def.Controls.Count; c++)
            {
                var ctl = def.Controls[c];
                sb.AppendLine($"{ControlName(def, c)} [{ctl.Positions} pos{(ctl.Wraps ? ", wraps" : "")}]");
                foreach (var a in ctl.Actions)
                {
                    sb.Append($"   {a.Kind}:");
                    foreach (var e in a.Effects)
                    {
                        if (e.When.Count > 0) sb.Append($" if {Clauses(def, e.When)} →");
                        foreach (var d in e.Deltas) sb.Append($" R{d.Register}{d.Amount:+0;-0}");
                        sb.Append(" ;");
                    }
                    sb.AppendLine();
                }
            }

            for (int i = 0; i < def.Displays.Count; i++)
            {
                var d = def.Displays[i];
                sb.Append($"Display {i} {d.Kind}: {d.Offset}");
                foreach (var t in d.Terms) sb.Append($" {t.Weight:+0;-0}·R{t.Register}");
                if (d.Kind == DisplayKind.Light) sb.Append($"  on if {Op(d.Op)} {d.Threshold}");
                sb.AppendLine();
            }

            sb.AppendLine("Pipeline:");
            foreach (var op in def.Pipeline)
            {
                string src = op.Kind is OpKind.Siphon or OpKind.Swap or OpKind.Exchange ? $" ⇄ {op.Source}" : "";
                string th = op.Kind switch
                {
                    OpKind.Exchange => " (always)",
                    OpKind.Purge or OpKind.Swap => $" (R{op.Register}≥{op.Threshold})",
                    _ => $" by R{op.Register}",
                };
                sb.AppendLine($"   {op.Kind} {op.Target}{src}{th}");
            }

            sb.AppendLine($"Reference solution: [{string.Join(",", def.ReferenceSolution)}]");
            return sb.ToString();
        }

        public static string ControlName(MachineDefinition def, int c) => $"{def.Controls[c].Kind} {c}";

        private static string Clauses(MachineDefinition def, List<Clause> clauses)
        {
            var parts = new List<string>();
            foreach (var c in clauses)
            {
                string slot = c.Slot < def.Registers.Count ? $"R{c.Slot}" : $"pos({ControlName(def, c.Slot - def.Registers.Count)})";
                parts.Add($"{slot} {Op(c.Op)} {c.Value}");
            }
            return string.Join(" & ", parts);
        }

        private static string Op(Comparison op) => op switch
        {
            Comparison.Equal => "=",
            Comparison.NotEqual => "≠",
            Comparison.Less => "<",
            _ => "≥",
        };
    }
}
