using System.Collections.Generic;

namespace Contraption.Core
{
    // State layout: [registers..., control positions...]. Everything stored is an int; nothing else is state.
    public sealed class MachineDefinition
    {
        public string Name = "Machine";
        // Visual kit name (Unity side). Empty = lab's current kit.
        public string Kit = "";
        public ulong Seed;
        public int GeneratorVersion;
        public int InputPorts = 1;
        public List<RegisterDef> Registers = new();
        public List<ControlDef> Controls = new();
        public List<DisplayDef> Displays = new();
        public List<TransferOp> Pipeline = new();
        public List<Liquid> InputPool = new();
        public List<ManualEntry> Manual = new();
        public int[] StartState = new int[0];
        // Register values used to author the manual. Debug/lab only; other register sets may also solve it.
        public int[] ReferenceSolution = new int[0];
        // Visual arrangement; null for levels saved before layouts existed (the lab generates one).
        public MachineLayout Layout;

        public int SlotCount() => Registers.Count + Controls.Count;
        public int ControlSlot(int controlIndex) => Registers.Count + controlIndex;
    }

    public enum Overflow { Clamp, Wrap }

    public sealed class RegisterDef
    {
        public Overflow Overflow;
    }

    public sealed class ManualEntry
    {
        public List<Liquid> Inputs = new();
        public Liquid Output;
    }
}
