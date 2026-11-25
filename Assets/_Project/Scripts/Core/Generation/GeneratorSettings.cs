using System.Collections.Generic;

namespace Contraption.Core
{
    public sealed class GeneratorSettings
    {
        public int MinRegisters = 2, MaxRegisters = 3;
        public int MinControls = 2, MaxControls = 3;
        public int InputPorts = 1;
        public int InputPoolSize = 3;
        public int ManualEntries = 2;
        public int ExtraOps = 1;
        public int Displays = 3;
        public int MaxHiddenRegisters;
        public int MaxDeltasPerEffect = 2;
        public double ConditionalChance = 0.25;
        public double WrapChance = 0.3;
        public double CombinedDisplayChance = 0.3;
        public double InvertedDisplayChance = 0.2;
        public int MinSolutionLength = 3, MaxSolutionLength = 12;
        public double MaxGoalFraction = 0.1;
        // Share of reachable states from which the goal is unreachable (only Reset saves you).
        public double MaxTrapRatio = 0.2;
        public int MaxStates = 200_000;
        public int MaxAttempts = 200;
        public int StartTriesPerMachine = 5;
        public List<ControlKind> ControlKinds = new() { ControlKind.Button, ControlKind.Lever, ControlKind.Dial, ControlKind.Knob };
        public List<OpKind> OpKinds = new() { OpKind.Add, OpKind.Subtract, OpKind.Cap, OpKind.Raise, OpKind.Purge, OpKind.Amplify, OpKind.Siphon, OpKind.Swap };
        public List<DisplayKind> DisplayKinds = new() { DisplayKind.Light, DisplayKind.Bar, DisplayKind.Gauge, DisplayKind.Glyph };
    }
}
