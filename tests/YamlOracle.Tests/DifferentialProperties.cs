using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace YamlOracle.Tests;

/// <summary>
/// The differential property: on every generated input (<see cref="Generators.Input"/>), YamlDotNet and libfyaml
/// both parse it to the same events, or both reject it. When they differ:
/// <list type="bullet">
/// <item>a known cause that explains it (<see cref="KnownDifferences.Causes"/>) counts it;</item>
/// <item>otherwise the YAML reference parser settles it. If it sides with YamlDotNet, libfyaml is wrong: the input is
/// kept under TestResults/libfyaml-bugs and the search goes on. Otherwise YamlDotNet is wrong: the test fails, after
/// <see cref="Reducer"/> has made the input small, and keeps it under TestResults/differences.</item>
/// </list>
/// Each kept input is a case directory: in.yaml, libfyaml.event, yamldotnet.event, reference.event (and error if
/// the reference parser rejects it). Without the reference parser (npm ci in tools/reference, and node on the
/// PATH or in YAML_ORACLE_NODE), every difference fails the test.
///
/// CsCheck_Time (seconds, default 10) sets how long it searches; CsCheck_Seed replays a run. It runs alone, after the
/// other tests, as it keeps every core busy.
///
/// With YAML_ORACLE_KEEP_GOING=1 (CI) a YamlDotNet difference doesn't stop the search or fail the test: every one is
/// kept, and the smallest few are also reduced, so a night's search finds as many as it can.
/// </summary>
[Collection(nameof(DifferentialProperties))]
public class DifferentialProperties(ITestOutputHelper output)
{
    /// <summary>YamlDotNet runs in the test process: if it hangs on an input, the watchdog keeps the input under
    /// TestResults/yamldotnet-hangs and ends the process, as the hung thread can't be stopped.</summary>
    static readonly TimeSpan YamlDotNetTimeout = TimeSpan.FromSeconds(30);

    enum Verdict { Agree, Known, LibFyamlWrong, YamlDotNetWrong, Unsettled, LibFyamlFailed }

    sealed class Parsers : IDisposable
    {
        public ParserProcess LibFyaml { get; } = ParserProcess.LibFyaml();
        public ParserProcess? Reference { get; } = Support.Reference();

        public void Dispose()
        {
            LibFyaml.Dispose();
            Reference?.Dispose();
        }
    }

    [Fact]
    public void YamlDotNetAgreesWithLibFyaml()
    {
        int seconds = int.TryParse(Environment.GetEnvironmentVariable("CsCheck_Time"), out var s) ? s : 10;
        bool keepGoing = Environment.GetEnvironmentVariable("YAML_ORACLE_KEEP_GOING") == "1";
        var parsers = new ThreadLocal<Parsers>(() => new Parsers(), trackAllValues: true);
        var known = new ConcurrentDictionary<string, int>();
        var kept = new ConcurrentDictionary<string, ConcurrentBag<string>>();
        var found = new ConcurrentDictionary<string, byte>();
        long cases = 0, rejected = 0;
        string? smallest = null;
        var gate = new Lock();

        void Keep(string kind, string input, Parsers p, string title) =>
            kept.GetOrAdd(kind, _ => []).Add(KeepCase(kind, input, p, title));

        void Check(string input)
        {
            Interlocked.Increment(ref cases);
            var p = parsers.Value!;
            if (p.LibFyaml.Parse(input).Kind == OutcomeKind.Error) Interlocked.Increment(ref rejected);
            switch (Judge(input, p, out var cause))
            {
                case Verdict.LibFyamlFailed:
                    Keep("libfyaml-failures", input, p, "libfyaml crashed or gave no answer"); // not about YamlDotNet; for a libfyaml report
                    return;
                case Verdict.Agree:
                    return;
                case Verdict.Known:
                    known.AddOrUpdate(cause!, 1, (_, n) => n + 1);
                    return;
                case Verdict.LibFyamlWrong:
                    Keep("libfyaml-bugs", input, p, "the reference parser sides with YamlDotNet");
                    return;
                case Verdict.Unsettled:
                    Keep("unsettled", input, p, "the reference parser gave no answer");
                    return;
                default:
                    if (keepGoing)
                    {
                        if (found.TryAdd(input, 0)) Keep("differences-unreduced", input, p, "YamlDotNet is wrong (not reduced)");
                        return;
                    }
                    lock (gate)
                        if (smallest is null || input.Length < smallest.Length) smallest = input;
                    throw new Exception("YamlDotNet differs from libfyaml and the reference parser");
            }
        }

        try
        {
            Generators.Input.Sample(Check, time: seconds);
        }
        catch (CsCheckException) { } // YamlDotNet is wrong somewhere: the smallest input CsCheck tried is in smallest
        finally
        {
            output.WriteLine($"{cases} inputs in {seconds} s, {rejected} of them invalid (libfyaml {ParserProcess.LibFyamlVersion}, YamlDotNet {YamlDotNetEvents.Version})");
            if (parsers.Values.All(p => p.Reference is null))
                output.WriteLine("the reference parser isn't available: every difference fails (see tools/reference)");
            foreach (var (name, count) in known.OrderBy(k => k.Key)) output.WriteLine($"known difference ({count}): {name}");
            foreach (var (kind, dirs) in kept.OrderBy(k => k.Key)) output.WriteLine($"{kind} ({dirs.Count}): {string.Join(", ", dirs.Take(3))}");
        }

        try
        {
            var p = parsers.Value!;
            string Reduce(string input)
            {
                var first = Kinds(input, p);
                return Reducer.Reduce(input, candidate =>
                    Kinds(candidate, p) == first && Judge(candidate, p, out _) == Verdict.YamlDotNetWrong);
            }
            string KeepReduced(string reduced) =>
                KeepCase("differences", reduced, p, $"YamlDotNet {Describe(YamlDotNet(reduced))}, libfyaml {Describe(p.LibFyaml.Parse(reduced))}");

            if (keepGoing)
            {
                var dirs = found.Keys.OrderBy(i => i.Length).Take(10).Select(i => KeepReduced(Reduce(i))).Distinct().ToList();
                output.WriteLine($"YamlDotNet is wrong on {found.Count} inputs (TestResults/differences-unreduced); the smallest, reduced: {string.Join(", ", dirs)}");
                return;
            }
            if (smallest is null) return;
            var reduced = Reduce(smallest);
            var dir = KeepReduced(reduced);
            Assert.Fail($"YamlDotNet {YamlDotNetEvents.Version} is wrong on this input (kept in {dir}):\n{Show(reduced)}\n" +
                $"--- libfyaml\n{p.LibFyaml.Parse(reduced)}\n--- YamlDotNet\n{YamlDotNet(reduced)}" +
                (p.Reference is null ? "\n(not confirmed: the reference parser isn't available)" : $"\n--- reference parser\n{p.Reference.Parse(reduced)}"));
        }
        finally
        {
            foreach (var p in parsers.Values) p.Dispose();
            parsers.Dispose();
        }
    }

    static Verdict Judge(string input, Parsers p, out string? cause)
    {
        cause = null;
        var fy = p.LibFyaml.Parse(input);
        if (fy.Kind is OutcomeKind.Crashed or OutcomeKind.TimedOut) return Verdict.LibFyamlFailed;
        var yd = YamlDotNet(input);
        if (yd.AgreesWith(fy)) return Verdict.Agree;
        cause = KnownDifferences.Explain(input, avoided => YamlDotNet(avoided).AgreesWith(p.LibFyaml.Parse(avoided)))?.Name;
        if (cause is not null) return Verdict.Known;
        if (p.Reference is null) return Verdict.YamlDotNetWrong;
        var reference = p.Reference.Parse(input);
        if (reference.Kind is OutcomeKind.Crashed or OutcomeKind.TimedOut) return Verdict.Unsettled;
        if (reference.AgreesWith(yd)) return Verdict.LibFyamlWrong;
        // YamlDotNet is wrong. Where libfyaml is wrong too, a known cause can only show against the reference parser.
        if (!reference.AgreesWith(fy))
        {
            var reference2 = p.Reference;
            cause = KnownDifferences.Explain(input, avoided => YamlDotNet(avoided).AgreesWith(reference2.Parse(avoided)))?.Name;
            if (cause is not null) return Verdict.Known;
        }
        return Verdict.YamlDotNetWrong;
    }

    static ParseOutcome YamlDotNet(string input)
    {
        using var watchdog = new Timer(_ =>
        {
            var dir = Path.Combine(Support.Root, "TestResults", "yamldotnet-hangs", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..8]);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "in.yaml"), Encoding.UTF8.GetBytes(input));
            Environment.FailFast($"YamlDotNet gave no answer in {YamlDotNetTimeout.TotalSeconds} s; the input is in {dir}");
        }, null, YamlDotNetTimeout, Timeout.InfiniteTimeSpan);
        return YamlDotNetEvents.Parse(input);
    }

    static (OutcomeKind YamlDotNet, OutcomeKind LibFyaml) Kinds(string input, Parsers p) => (YamlDotNet(input).Kind, p.LibFyaml.Parse(input).Kind);

    static string Describe(ParseOutcome o) => o.Kind switch
    {
        OutcomeKind.Events => "parses it",
        OutcomeKind.Error => "rejects it",
        OutcomeKind.Crashed => "crashes",
        _ => "hangs",
    };

    /// <summary>The input with its tabs and carriage returns visible.</summary>
    static string Show(string input) => "--- in.yaml\n" + input.Replace("\t", "→").Replace("\r", "␍");

    static readonly Lock KeepGate = new();

    /// <summary>Writes a case directory under TestResults/&lt;kind&gt; (once per input) and returns its path.</summary>
    static string KeepCase(string kind, string input, Parsers p, string title)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8];
        var root = Path.Combine(Support.Root, "TestResults", kind);
        string dir;
        lock (KeepGate)
        {
            if (Directory.Exists(root) && Directory.EnumerateDirectories(root, "*-" + hash).FirstOrDefault() is { } existing) return existing;
            dir = Path.Combine(root, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{hash}");
            Directory.CreateDirectory(dir);
        }
        var fy = p.LibFyaml.Parse(input); // again: a crash or timeout says so here too
        File.WriteAllBytes(Path.Combine(dir, "in.yaml"), bytes);
        File.WriteAllText(Path.Combine(dir, "==="), title + "\n");
        File.WriteAllText(Path.Combine(dir, "libfyaml.event"), Event(fy));
        File.WriteAllText(Path.Combine(dir, "yamldotnet.event"), Event(YamlDotNet(input)));
        if (p.Reference?.Parse(input) is { } reference)
        {
            File.WriteAllText(Path.Combine(dir, "reference.event"), Event(reference));
            if (reference.Kind == OutcomeKind.Error) File.WriteAllText(Path.Combine(dir, "error"), "");
        }
        return dir;
    }

    static string Event(ParseOutcome o) => o.Kind == OutcomeKind.Events ? o.Events : $"ERROR {o.Kind}: {o.Message}\n{o.Events}";
}

/// <summary>Runs <see cref="DifferentialProperties"/> by itself, after the tests that run in parallel.</summary>
[CollectionDefinition(nameof(DifferentialProperties), DisableParallelization = true)]
public sealed class DifferentialPropertiesCollection;
