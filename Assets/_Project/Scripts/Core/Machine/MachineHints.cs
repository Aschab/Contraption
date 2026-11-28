using System.Collections.Generic;

namespace Contraption.Core
{
    // How much a machine tells the player about its pipeline (the learning curve). Saved with the level.
    // Every pipeline step lives at a "home" display (the one that best shows its register); Exchange has none.
    public sealed class MachineHints
    {
        public const int LabelNone = 0, LabelTrait = 1, LabelFull = 2;

        // The machine can be turned around: its back shows every display's rear with cables in step order.
        public bool Back;
        // Per pipeline step: plate under its home display (and on its rear node).
        public List<int> Labels = new();
        // Links[i] runs into step i; the last one runs from the final step to the end. Count = steps + 1.
        public List<bool> Links = new();

        public static MachineHints All(int steps, int label = LabelFull)
        {
            var h = new MachineHints { Back = true };
            h.Fit(steps, label, true);
            return h;
        }

        // Pads or trims the lists to the pipeline (levels edited after saving, old files).
        public void Fit(int steps, int label = LabelNone, bool link = false)
        {
            while (Labels.Count < steps) Labels.Add(label);
            if (Labels.Count > steps) Labels.RemoveRange(steps, Labels.Count - steps);
            while (Links.Count < steps + 1) Links.Add(link);
            if (Links.Count > steps + 1) Links.RemoveRange(steps + 1, Links.Count - steps - 1);
        }

        public int Label(int step) => step < Labels.Count ? Labels[step] : LabelNone;
        public bool Link(int index) => index < Links.Count && Links[index];

        // Display that shows the step's register most plainly: fewest terms, not a light, not inverted. -1 for Exchange.
        public static int Home(MachineDefinition def, int step)
        {
            var op = def.Pipeline[step];
            if (op.Kind == OpKind.Exchange) return -1;
            int best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < def.Displays.Count; i++)
            {
                var d = def.Displays[i];
                int weight = Weight(d, op.Register);
                if (weight == 0) continue;
                int score = d.Terms.Count * 4 + (d.Kind == DisplayKind.Light ? 2 : 0) + (weight < 0 ? 1 : 0);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        public static int Weight(DisplayDef d, int register)
        {
            int w = 0;
            foreach (var t in d.Terms) if (t.Register == register) w += t.Weight;
            return w;
        }
    }
}
