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
        public void JoystickAndExchange()
        {
            var def = new MachineDefinition();
            def.Registers.Add(new RegisterDef());
            def.Controls.Add(new ControlDef
            {
                Kind = ControlKind.Joystick, Positions = ControlDef.JoystickPositions,
                Actions =
                {
                    new ControlAction { Kind = ActionKind.PushLeft, Effects = { Fx(null, 2) } },
                    new ControlAction { Kind = ActionKind.Center, Effects = { Fx(null, -1) } },
                },
            });
            int[] s = { 0, 0 };
            s = Step(def, s, new Move(0, ActionKind.PushLeft));
            Assert.AreEqual(new[] { 2, 3 }, s);
            Assert.IsFalse(MachineRules.TryApply(def, s, new Move(0, ActionKind.PushLeft), new int[2]), "already left");
            s = Step(def, s, new Move(0, ActionKind.Center));
            Assert.AreEqual(new[] { 1, 0 }, s);

            def.Pipeline.Add(new TransferOp { Kind = OpKind.Exchange, Target = LiquidProperty.Viscosity, Source = LiquidProperty.Temperature });
            var output = MachineRules.Process(def, s, new[] { new Liquid { Viscosity = 3, Temperature = 5 } });
            Assert.AreEqual(5, output.Viscosity);
            Assert.AreEqual(3, output.Temperature);
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
                Assert.IsNull(MachineGenerator.FindAmbiguousRegister(def), $"seed {seed} displays are ambiguous");
            }
        }

        [Test]
        public void LayoutsPlaceEveryPartOnTheBody()
        {
            var settings = new GeneratorSettings { MaxControls = 4, LargePartChance = 0.8 };
            for (ulong seed = 1; seed <= 15; seed++)
            {
                var gen = MachineGenerator.Generate(seed, settings);
                if (!gen.Success) continue;
                var def = gen.Definition;
                var l = def.Layout;
                Assert.AreEqual(def.Controls.Count, l.Controls.Count);
                Assert.AreEqual(def.Displays.Count, l.Displays.Count);
                var used = new bool[l.Body.Length];
                foreach (var p in l.AllPlacements())
                    for (int y = p.Y; y < p.Y + p.H; y++)
                        for (int x = p.X; x < p.X + p.W; x++)
                        {
                            Assert.IsTrue(l.IsBody(x, y), $"seed {seed}: {p.Style} off the body");
                            Assert.IsFalse(used[y * l.Columns + x], $"seed {seed}: overlap at {x},{y}");
                            used[y * l.Columns + x] = true;
                        }
                for (int i = 0; i < used.Length; i++) Assert.AreEqual(l.Body[i], used[i], $"seed {seed}: cell {i} not covered");
                Assert.AreEqual(def.InputPorts, l.Inputs.Count);
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
