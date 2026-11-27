namespace Contraption.Core
{
    public enum OpKind
    {
        Add,      // t += r
        Subtract, // t -= r
        Cap,      // t = min(t, r)
        Raise,    // t = max(t, r)
        Purge,    // t = 0 if r >= threshold
        Amplify,  // t = t * r / 2   (r = 2 is neutral)
        Siphon,   // move min(r, s) from source to target
        Swap,     // swap(t, s) if r >= threshold
        Exchange, // swap(t, s) always; ignores its register
    }

    public sealed class TransferOp
    {
        public OpKind Kind;
        public LiquidProperty Target;
        public LiquidProperty Source;
        public int Register;
        public int Threshold = 1;
    }
}
