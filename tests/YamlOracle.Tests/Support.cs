using Xunit;
using System.Text;

namespace YamlOracle.Tests;

/// <summary>A yaml-test-suite style case: a directory with in.yaml, and test.event or error.</summary>
/// <param name="Lf">The input's line breaks are LF: true for the yaml-test-suite, whose committed in.yaml files have no
/// carriage returns, so a CRLF checkout (core.autocrlf) is read back as committed.</param>
public sealed record SuiteCase(string Id, string Dir, bool Lf = false)
{
    public string Input
    {
        get
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(Dir, "in.yaml")));
            return Lf ? text.Replace("\r\n", "\n") : text;
        }
    }
    public bool Error => File.Exists(Path.Combine(Dir, "error"));
    public string? Events => File.Exists(Path.Combine(Dir, "test.event")) ? File.ReadAllText(Path.Combine(Dir, "test.event")).ReplaceLineEndings("\n") : null;
    public string Title => File.Exists(Path.Combine(Dir, "===")) ? File.ReadAllText(Path.Combine(Dir, "===")).Trim() : Id;

    /// <summary>The outcome matches the case: the same events, or an error when the case is invalid.</summary>
    public bool Matches(ParseOutcome outcome) => Error ? outcome.Kind == OutcomeKind.Error : outcome.Kind == OutcomeKind.Events && outcome.Events == Events;

    public override string ToString() => Id;
}

public static partial class Support
{
    public static string Root { get; } = FindRoot();

    static string FindRoot()
    {
        for (var dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
            if (File.Exists(Path.Combine(dir, "YamlOracle.slnx"))) return dir;
        throw new InvalidOperationException("YamlOracle.slnx not found above " + AppContext.BaseDirectory);
    }

    /// <summary>external/yaml-test-suite, the data-2022-01-17 release (git submodule).</summary>
    public static string SuiteDir => Path.Combine(Root, "external", "yaml-test-suite");

    /// <summary>tests/YamlOracle.Tests/cases: cases of our own, in the suite's layout.</summary>
    public static string CasesDir => Path.Combine(Root, "tests", "YamlOracle.Tests", "cases");

    /// <summary>Where a found difference is written, as a case directory (TestResults/differences).</summary>
    public static string DifferencesDir => Path.Combine(Root, "TestResults", "differences");

    public static IReadOnlyList<SuiteCase> Cases(string root, bool lf = false, Func<string, bool>? id = null)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "in.yaml", SearchOption.AllDirectories)
            .Select(f => Path.GetDirectoryName(f)!)
            .Select(d => new SuiteCase(Path.GetRelativePath(root, d).Replace('\\', '/'), d, lf))
            .Where(c => id is null || id(c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The suite's cases: XXXX or XXXX/NN. Its tags/ and name/ directories hold symlinks to the same cases
    /// (followed where git checks symlinks out, as on CI's runners), which would count each case again.</summary>
    public static IReadOnlyList<SuiteCase> SuiteCases { get; } = Cases(SuiteDir, lf: true, id => SuiteId().IsMatch(id));

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9A-Z]{4}(/[0-9]+)?$")]
    private static partial System.Text.RegularExpressions.Regex SuiteId();
    public static IReadOnlyList<SuiteCase> OwnCases { get; } = Cases(CasesDir);

    /// <summary>The reference parser (tools/reference), or null when it can't run here: its dependencies aren't
    /// installed (npm ci in tools/reference) or there is no node (on the PATH, or YAML_ORACLE_NODE).</summary>
    public static ParserProcess? Reference()
    {
        var script = Path.Combine(Root, "tools", "reference", "reference.js");
        if (!Directory.Exists(Path.Combine(Root, "tools", "reference", "node_modules"))) return null;
        if (!File.Exists(Path.Combine(Root, "external", "yaml-reference-parser", "parser-1.2", "javascript", "lib", "grammar.js"))) return null;
        var node = Environment.GetEnvironmentVariable("YAML_ORACLE_NODE") is { Length: > 0 } n ? n : FindOnPath(OperatingSystem.IsWindows() ? "node.exe" : "node");
        return node is null ? null : ParserProcess.Reference(script, node);
    }

    static string? FindOnPath(string file) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, file)).FirstOrDefault(File.Exists);

    public static TheoryData<string> Ids(IEnumerable<SuiteCase> cases)
    {
        var data = new TheoryData<string>();
        foreach (var c in cases) data.Add(c.Id);
        return data;
    }
}

/// <summary>One libfyaml worker for a test class (xunit runs a class's tests one at a time).</summary>
public sealed class LibFyamlFixture : IDisposable
{
    public ParserProcess Parser { get; } = ParserProcess.LibFyaml();
    public void Dispose() => Parser.Dispose();
}
