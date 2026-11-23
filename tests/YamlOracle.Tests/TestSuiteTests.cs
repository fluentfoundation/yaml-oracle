using Xunit;

namespace YamlOracle.Tests;

/// <summary>
/// The yaml-test-suite (external/yaml-test-suite) through both parsers. libfyaml has to pass every case: that is
/// what makes it an oracle, and it checks the worker's P/Invoke declarations too. YamlDotNet has to pass every case
/// except those in <see cref="KnownDifferences.SuiteFailures"/>, which have to keep failing until the list is updated.
/// </summary>
public class TestSuiteTests(LibFyamlFixture fyaml) : IClassFixture<LibFyamlFixture>
{
    public static TheoryData<string> Suite => Support.Ids(Support.SuiteCases);

    static SuiteCase Case(string id) => Support.SuiteCases.Single(c => c.Id == id);

    [Fact]
    public void TheSuiteIsThere() =>
        Assert.True(Support.SuiteCases.Count > 300, $"no yaml-test-suite in {Support.SuiteDir}: run 'git submodule update --init'");

    [Theory, MemberData(nameof(Suite))]
    public void LibFyamlPasses(string id)
    {
        var c = Case(id);
        var got = fyaml.Parser.Parse(c.Input);
        Assert.True(c.Matches(got), Explain(c, got));
    }

    [Theory, MemberData(nameof(Suite))]
    public void YamlDotNetPasses(string id)
    {
        var c = Case(id);
        var got = YamlDotNetEvents.Parse(c.Input);
        if (KnownDifferences.SuiteFailures.TryGetValue(id, out var why))
            Assert.False(c.Matches(got), $"{id} passes now; remove it from KnownDifferences.SuiteFailures ({why})");
        else
            Assert.True(c.Matches(got), Explain(c, got));
    }

    internal static string Explain(SuiteCase c, ParseOutcome got) =>
        $"{c.Id}: {c.Title}\n--- in.yaml\n{c.Input}\n--- expected\n{(c.Error ? "an error" : c.Events)}\n--- got\n{got}";
}
