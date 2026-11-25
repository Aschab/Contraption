namespace Contraption.Core
{
    // Mixed-radix encoding of a full state into a long. Registers are the low digits,
    // so (code % RegisterSpace) identifies the register-only part used by goal tables.
    public sealed class StateLayout
    {
        private readonly int[] _radix;
        private readonly long[] _mult;

        public long Size { get; }
        public int RegisterSpace { get; }
        public int SlotCount => _radix.Length;

        public StateLayout(MachineDefinition def)
        {
            int n = def.SlotCount();
            _radix = new int[n];
            _mult = new long[n];
            for (int i = 0; i < def.Registers.Count; i++) _radix[i] = Values.Levels;
            for (int c = 0; c < def.Controls.Count; c++) _radix[def.ControlSlot(c)] = def.Controls[c].Positions;

            long size = 1;
            for (int i = 0; i < n; i++)
            {
                _mult[i] = size;
                size = size > long.MaxValue / _radix[i] ? long.MaxValue : size * _radix[i];
                if (i == def.Registers.Count - 1) RegisterSpace = (int)System.Math.Min(size, int.MaxValue);
            }
            if (def.Registers.Count == 0) RegisterSpace = 1;
            Size = size;
        }

        public long Encode(int[] state)
        {
            long code = 0;
            for (int i = 0; i < _radix.Length; i++) code += state[i] * _mult[i];
            return code;
        }

        public void Decode(long code, int[] into)
        {
            for (int i = 0; i < _radix.Length; i++)
            {
                into[i] = (int)(code % _radix[i]);
                code /= _radix[i];
            }
        }

        public int RegisterCode(long code) => (int)(code % RegisterSpace);
    }
}
