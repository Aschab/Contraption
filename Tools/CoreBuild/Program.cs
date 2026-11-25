using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Minimal NUnit runner: executes every [Test] method in the compiled test sources.
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "show") return Show(ulong.Parse(args[1]));
        if (args.Length > 0 && args[0] == "gen") return GenStats(args.Length > 1 ? int.Parse(args[1]) : 30);

        int failed = 0, passed = 0;
        var tests = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null).Select(m => (t, m)));
        foreach (var (type, method) in tests)
        {
            try
            {
                method.Invoke(Activator.CreateInstance(type), null);
                passed++;
            }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine($"FAIL {type.Name}.{method.Name}: {e.InnerException?.Message}");
            }
        }
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    // `dotnet run -- show SEED`: dump one generated machine (default settings) with its solution.
    private static int Show(ulong seed)
    {
        var r = Contraption.Core.MachineGenerator.Generate(seed, new Contraption.Core.GeneratorSettings());
        if (!r.Success) { Console.WriteLine("generation failed"); return 1; }
        var def = r.Definition;
        Console.WriteLine(Contraption.Core.MachineDescriber.Describe(def));
        Console.WriteLine($"Start [{string.Join(",", def.StartState)}]");
        Console.WriteLine("Pool: " + string.Join(" | ", def.InputPool));
        foreach (var e in def.Manual) Console.WriteLine($"Manual: {string.Join(" + ", e.Inputs)} -> {e.Output}");
        Console.WriteLine("Solution: " + string.Join(", ", r.Solve.Solution));
        return 0;
    }

    // `dotnet run -- gen N`: generate N machines with default settings and print metrics.
    private static int GenStats(int count)
    {
        var sw = new System.Diagnostics.Stopwatch();
        int ok = 0;
        for (ulong seed = 1; seed <= (ulong)count; seed++)
        {
            sw.Restart();
            var r = Contraption.Core.MachineGenerator.Generate(seed, new Contraption.Core.GeneratorSettings());
            sw.Stop();
            if (!r.Success)
            {
                Console.WriteLine($"seed {seed,3}: FAIL after {r.Attempts} ({string.Join(", ", r.Rejections.Select(kv => $"{kv.Key}={kv.Value}"))}) {sw.ElapsedMilliseconds}ms");
                continue;
            }
            ok++;
            var d = r.Difficulty;
            Console.WriteLine($"seed {seed,3}: att {r.Attempts,3} len {d.SolutionLength,2} reach {d.ReachableStates,6} goal {d.GoalStates,4} traps {d.TrapStates,6} cond {d.ConditionalEffects} hidden {d.HiddenRegisters} score {d.Score,5:F1} {sw.ElapsedMilliseconds}ms");
        }
        Console.WriteLine($"{ok}/{count} succeeded");
        return 0;
    }
}
