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
        if (args.Length > 1 && args[0] == "layout") return Layout(ulong.Parse(args[1]));
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

    // `dotnet run -- layout SEED`: ASCII view of the generated layout (top row first).
    private static int Layout(ulong seed)
    {
        var r = Contraption.Core.MachineGenerator.Generate(seed, new Contraption.Core.GeneratorSettings());
        var l = r.Definition.Layout;
        var grid = new char[l.Rows, l.Columns];
        for (int y = 0; y < l.Rows; y++) for (int x = 0; x < l.Columns; x++) grid[y, x] = l.IsBody(x, y) ? '.' : ' ';
        void Mark(Contraption.Core.Placement p, char c) { for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) grid[y, x] = c; }
        for (int i = 0; i < l.Controls.Count; i++) Mark(l.Controls[i], (char)('A' + i));
        for (int i = 0; i < l.Displays.Count; i++) Mark(l.Displays[i], (char)('1' + i));
        foreach (var f in l.Fillers) if (f.W > 1) Mark(f, '#');
        Console.WriteLine($"inputs {string.Join(",", l.Inputs.Select(c => c.Column + ":" + c.Style))} out {l.Output.Style} top {l.TopStyle} base {l.BaseStyle}");
        for (int y = l.Rows - 1; y >= 0; y--) { var line = ""; for (int x = 0; x < l.Columns; x++) line += grid[y, x]; Console.WriteLine("|" + line + "|"); }
        foreach (var p in l.Controls.Concat(l.Displays)) Console.Write(p.Style + " ");
        Console.WriteLine();
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
            Console.WriteLine($"seed {seed,3}: att {r.Attempts,3} len {d.SolutionLength,2} reach {d.ReachableStates,6} goal {d.GoalStates,4} traps {d.TrapStates,6} cond {d.ConditionalEffects} score {d.Score,5:F1} {sw.ElapsedMilliseconds}ms");
        }
        Console.WriteLine($"{ok}/{count} succeeded");
        return 0;
    }
}
