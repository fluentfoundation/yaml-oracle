using Xunit;

namespace YamlOracle.Tests;

/// <summary>
/// Our own cases (tests/YamlOracle.Tests/cases), in the yaml-test-suite's layout. Their expected events come from the
/// YAML reference parser (CI checks them with tools/reference). Each parser has to pass all of them but its known
/// failures (<see cref="KnownDifferences.YamlDotNetCaseFailures"/>, <see cref="KnownDifferences.LibFyamlCaseFailures"/>),
/// which have to keep failing until the lists are updated.
/// </summary>
public class CaseTests(LibFyamlFixture fyaml) : IClassFixture<LibFyamlFixture>
{
    public static TheoryData<string> Own => Support.Ids(Support.OwnCases);

    static SuiteCase Case(string id) => Support.OwnCases.Single(c => c.Id == id);

    [Theory, MemberData(nameof(Own))]
    public void LibFyamlPasses(string id) => Passes(id, fyaml.Parser.Parse, KnownDifferences.LibFyamlCaseFailures, nameof(KnownDifferences.LibFyamlCaseFailures));

    [Theory, MemberData(nameof(Own))]
    public void YamlDotNetPasses(string id) => Passes(id, YamlDotNetEvents.Parse, KnownDifferences.YamlDotNetCaseFailures, nameof(KnownDifferences.YamlDotNetCaseFailures));

    static void Passes(string id, Func<string, ParseOutcome> parse, IReadOnlyDictionary<string, string> failures, string list)
    {
        var c = Case(id);
        var got = parse(c.Input);
        if (failures.TryGetValue(id, out var why))
            Assert.False(c.Matches(got), $"{id} passes now; remove it from KnownDifferences.{list} ({why})");
        else
            Assert.True(c.Matches(got), TestSuiteTests.Explain(c, got));
    }

    /// <summary>Each case YamlDotNet is known to fail has a known cause, whose rewrite the property relies on (unless
    /// libfyaml gets the case wrong too: then the reference parser settles it).</summary>
    [Theory, MemberData(nameof(Own))]
    public void YamlDotNetFailuresHaveAKnownCause(string id)
    {
        if (!KnownDifferences.YamlDotNetCaseFailures.ContainsKey(id) || KnownDifferences.LibFyamlCaseFailures.ContainsKey(id)) return;
        var c = Case(id);
        var cause = KnownDifferences.Explain(c.Input, avoided => YamlDotNetEvents.Parse(avoided).AgreesWith(fyaml.Parser.Parse(avoided)));
        Assert.NotNull(cause);
    }

    [Fact]
    public void TheCasesAreThere() => Assert.NotEmpty(Support.OwnCases);
}
