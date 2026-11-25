using NUnit.Framework;

namespace Contraption.Core.Tests
{
    public sealed class CoreTests
    {
        // The original design example: lever up +2+2+0 / down -2-2+1; button depends on the lever.
        [Test]
        public void LeverAndConditionalButton()
        {
            var def = new MachineDefinition();
            for (int i = 0; i < 3; i++) def.Registers.Add(new RegisterDef());
            def.Controls.Add(new ControlDef
            {
                Kind = ControlKind.Lever, Positions = 2,
                Actions =
                {
                    new ControlAction { Kind = ActionKind.Increment, Effects = { Fx(null, 2, 2, 0) } },
                    new ControlAction { Kind = ActionKind.Decrement, Effects = { Fx(null, -2, -2, 1) } },
                },
            });
            int lever = def.ControlSlot(0);
            def.Controls.Add(new ControlDef
            {
                Kind = ControlKind.Button,
                Actions =
                {
                    new ControlAction
                    {
                        Kind = ActionKind.Press,
                        Effects =
                        {
                            Fx(new Clause { Slot = lever, Op = Comparison.Equal, Value = 1 }, 0, 2, 0),
                            Fx(new Clause { Slot = lever, Op = Comparison.Equal, Value = 0 }, 2, 0, -1),
                        },
                    },
                },
            });

            int[] s = { 0, 0, 0, 0, 0 };
            s = Step(def, s, new Move(0, ActionKind.Increment));
            Assert.AreEqual(new[] { 2, 2, 0, 1, 0 }, s);
            s = Step(def, s, new Move(1, ActionKind.Press));
            Assert.AreEqual(new[] { 2, 4, 0, 1, 0 }, s);
            s = Step(def, s, new Move(0, ActionKind.Decrement));
            Assert.AreEqual(new[] { 0, 2, 1, 0, 0 }, s);
            s = Step(def, s, new Move(1, ActionKind.Press));
            Assert.AreEqual(new[] { 2, 2, 0, 0, 0 }, s);
            Assert.IsFalse(MachineRules.TryApply(def, s, new Move(0, ActionKind.Decrement), new int[5]), "lever already down");
        }

        [Test]
        public void GeneratorIsDeterministic()
        {
            var a = MachineGenerator.Generate(42, new GeneratorSettings());
            var b = MachineGenerator.Generate(42, new GeneratorSettings());
            Assert.IsTrue(a.Success, "generation failed");
            Assert.AreEqual(MachineJson.ToJson(a.Definition), MachineJson.ToJson(b.Definition));
        }

        [Test]
        public void SolutionSolvesGeneratedMachines()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var gen = MachineGenerator.Generate(seed, new GeneratorSettings());
                if (!gen.Success) continue;
                var def = gen.Definition;
                Assert.IsFalse(MachineRules.MatchesManual(def, def.StartState), $"seed {seed} starts solved");
                var s = (int[])def.StartState.Clone();
                foreach (var m in gen.Solve.Solution) s = Step(def, s, m);
                Assert.IsTrue(MachineRules.MatchesManual(def, s), $"seed {seed} solution fails");
            }
        }

        [Test]
        public void JsonRoundTrip()
        {
            var def = MachineGenerator.Generate(7, new GeneratorSettings()).Definition;
            string json = MachineJson.ToJson(def);
            Assert.AreEqual(json, MachineJson.ToJson(MachineJson.FromJson(json)));
        }

        private static Effect Fx(Clause when, params int[] deltas)
        {
            var e = new Effect();
            if (when != null) e.When.Add(when);
            for (int r = 0; r < deltas.Length; r++)
                if (deltas[r] != 0) e.Deltas.Add(new Delta { Register = r, Amount = deltas[r] });
            return e;
        }

        private static int[] Step(MachineDefinition def, int[] s, Move m)
        {
            var next = new int[s.Length];
            Assert.IsTrue(MachineRules.TryApply(def, s, m, next), $"invalid move {m}");
            return next;
        }
    }
}
