using System.Collections.Generic;

namespace Contraption.Core
{
    public enum DisplayKind { Light, Bar, Gauge, Glyph }

    // Value = Offset + Σ weight·register. Light is on when Value <Op> Threshold.
    public sealed class DisplayDef
    {
        public DisplayKind Kind;
        public List<Term> Terms = new();
        public int Offset;
        public Comparison Op = Comparison.GreaterOrEqual;
        public int Threshold;
    }

    public struct Term
    {
        public int Register;
        public int Weight;
    }
}
