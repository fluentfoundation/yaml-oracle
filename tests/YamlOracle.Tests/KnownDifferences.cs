using System.Text.RegularExpressions;

namespace YamlOracle.Tests;

/// <summary>Where YamlDotNet, or libfyaml, is known to differ from the YAML spec. Each list has to stay true: an entry that
/// starts passing fails its test, so the list is updated when YamlDotNet is.</summary>
public static partial class KnownDifferences
{
    const string RejectsValid = "rejects valid YAML";
    const string MisreadsValid = "reads valid YAML to the wrong events";
    const string AcceptsInvalid = "accepts invalid YAML";
    const string Throws = "throws InvalidOperationException, not a YamlException, on invalid YAML";

    /// <summary>
    /// yaml-test-suite cases YamlDotNet 18.1.0 fails, by id. All but 9C9N are in the suite's subdirectories
    /// (4MUZ/02, ...), which YamlDotNet's own spec tests don't run: YamlDotNet.Test/Spec/SpecTestData.cs enumerates
    /// only top-level directories, which skips 69 of the 402 cases. 9C9N runs there, and passes because any exception
    /// counts as the expected error.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SuiteFailures { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["4MUZ/02"] = RejectsValid,
        ["9C9N"] = Throws,
        ["DK95/01"] = AcceptsInvalid,
        ["DK95/04"] = RejectsValid,
        ["JEF9/01"] = RejectsValid,
        ["JEF9/02"] = MisreadsValid,
        ["L24T/01"] = MisreadsValid,
        ["M2N8/00"] = MisreadsValid,
        ["MUS6/00"] = AcceptsInvalid,
        ["UKK6/00"] = RejectsValid,
        ["VJP3/00"] = Throws,
        ["VJP3/01"] = RejectsValid,
        ["Y79Y/003"] = AcceptsInvalid,
        ["Y79Y/004"] = AcceptsInvalid,
        ["Y79Y/005"] = AcceptsInvalid,
        ["Y79Y/006"] = AcceptsInvalid,
        ["Y79Y/008"] = AcceptsInvalid,
        ["Y79Y/009"] = AcceptsInvalid,
    };

    const string StarWord = "a plain scalar's word starting with * hides the ': ' after it (fluentfoundation/OASDelta#54)";

    /// <summary>Cases of tests/YamlOracle.Tests/cases YamlDotNet 18.1.0 fails, by id.</summary>
    public static IReadOnlyDictionary<string, string> YamlDotNetCaseFailures { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["star-word-in-plain-scalar/01-root-first-key"] = StarWord,
        ["star-word-in-plain-scalar/02-nested-first-key"] = StarWord,
        ["star-word-in-plain-scalar/04-go-yaml-emitted-key"] = StarWord,
        ["star-word-in-plain-scalar/05-seq-entry-first-key"] = StarWord,
        ["star-word-in-plain-scalar/06-flow-map-key"] = StarWord,
        ["star-word-in-plain-scalar/07-flow-seq-pair"] = StarWord,
        ["star-word-in-plain-scalar/08-value-with-colon"] = StarWord,
        ["star-word-in-plain-scalar/11-key-then-nested-pair"] = StarWord,
        ["empty-key/02-indented"] = EmptyKey,
        ["empty-key/03-indented-with-value"] = EmptyKey,
        ["empty-key/04-nested"] = EmptyKey,
        ["empty-key/05-nested-with-value"] = EmptyKey,
        ["empty-key/06-nested-then-key"] = EmptyKey,
        ["empty-key/07-nested-last"] = EmptyKey,
        ["empty-key/08-sequence-entry"] = EmptyKey,
        ["empty-key/09-sequence-entry-with-value"] = EmptyKey,
        ["empty-key/10-explicit-empty-key"] = EmptyKey,
        ["empty-key/12-indented-then-nested-on-the-same-line"] = EmptyKey,
        ["empty-key/13-explicit-empty-key-in-flow-sequence"] = EmptyKey,
        ["empty-key/14-question-mark-alone-in-flow-sequence"] = QuestionMarkAlone,
        ["mapping-on-its-keys-line/03-in-a-sequence"] = EmptyKey,
        ["mapping-on-its-keys-line/04-on-the-next-line"] = EmptyKey,
        ["unicode-line-breaks/01-paragraph-separator-alone"] = UnicodeBreaks,
        ["unicode-line-breaks/02-line-separator-in-plain"] = UnicodeBreaks,
        ["unicode-line-breaks/03-next-line-in-plain"] = UnicodeBreaks,
        ["unicode-line-breaks/04-paragraph-separator-in-plain"] = UnicodeBreaks,
        ["unicode-line-breaks/05-line-separator-in-double-quoted"] = UnicodeBreaks,
        ["unicode-line-breaks/06-next-line-in-literal"] = UnicodeBreaks,
        ["document-end-marker/01-first-then-scalar"] = LeadingDocumentEnd,
        ["document-end-marker/02-first-then-tag"] = LeadingDocumentEnd,
        ["document-end-marker/03-first-then-explicit-document"] = LeadingDocumentEnd,
        ["document-end-marker/05-tagged-empty-document-after-end"] = PropertiesOnlyAfterEnd,
        ["document-end-marker/08-anchored-empty-document-after-end"] = PropertiesOnlyAfterEnd,
        ["block-scalar-spaces-only-lines/01-keep-last-line-no-break"] = SpacesOnlyLines,
        ["block-scalar-spaces-only-lines/02-keep-after-content-no-break"] = SpacesOnlyLines,
        ["block-scalar-spaces-only-lines/04-leading-lines-longer-than-content"] = SpacesOnlyLines,
        ["reserved-indicator-starts-plain-scalar/01-backtick-in-flow-sequence"] = ReservedIndicator,
        ["reserved-indicator-starts-plain-scalar/02-at-in-flow-sequence"] = ReservedIndicator,
        ["reserved-indicator-starts-plain-scalar/03-at-key-in-flow-mapping"] = ReservedIndicator,
        ["plus-in-tag/01-local-tag-alone"] = PlusInTag,
        ["plus-in-tag/02-local-tag"] = PlusInTag,
        ["plus-in-tag/03-secondary-tag"] = PlusInTag,
        ["plus-in-tag/04-verbatim-tag"] = PlusInTag,
        ["plus-in-tag/05-named-handle"] = PlusInTag,
        ["comment-without-blank/01-after-flow-sequence-start"] = CommentAfterFlowStart,
        ["comment-without-blank/02-after-flow-mapping-start"] = CommentAfterFlowStart,
    };

    const string CommentAfterFlowStart = "a '#' right after '[' or '{': YamlDotNet takes it for a comment, which needs a blank " +
        "before it";

    const string PlusInTag = "a '+' in a tag: YamlDotNet decodes it as a space in a local tag ('!a+b' is '!a b'), and throws " +
        "ArgumentException (\"Global tags must be valid URIs\"), not a YamlException, for a global one";

    const string ReservedIndicator = "a plain scalar starting with a reserved indicator ('@' or '`') in a flow collection: " +
        "YamlDotNet accepts it (it rejects it in block context)";

    const string LeadingDocumentEnd = "a stream that starts with '...': YamlDotNet throws InvalidOperationException (\"The scanner " +
        "should contain no more tokens\")";

    const string PropertiesOnlyAfterEnd = "a document that is only a tag or an anchor, after '...': YamlDotNet rejects it";

    const string SpacesOnlyLines = "a block scalar's lines of only spaces: YamlDotNet drops the last one at the end of a stream " +
        "without a final line break, and rejects leading ones longer than the content's indentation (yaml-test-suite JEF9, L24T)";

    const string ByteOrderMark = "a byte order mark (U+FEFF): the reference parser rejects every one, though YAML allows one at " +
        "the start of a document (section 5.2), so it can't settle these; and YamlDotNet reads one at the start of a string as " +
        "content, where reading a file would have removed it";

    const string Tabs = "a tab where YAML forbids one, as indentation (yaml-test-suite Y79Y, DK95): YamlDotNet accepts it";

    const string UnicodeBreaks = "NEL (U+0085), LS (U+2028) and PS (U+2029) are line breaks, as in YAML 1.1; YAML 1.2 made them " +
        "ordinary characters (section 5.4)";

    const string EmptyKey = "an entry with an empty key (': x') anywhere but the start of a top-level line: YamlDotNet rejects it, " +
        "drops its value, or reads extra entries";

    const string QuestionMarkAlone = "accepts '?' alone in a flow collection ('[?]') as a plain scalar; YAML needs a blank after an " +
        "explicit key's '?', and a plain scalar can't start with '?' before ']'";

    const string MappingOnKeysLine = "accepts a block mapping that starts on the line of its key's ':'";

    /// <summary>Cases of tests/YamlOracle.Tests/cases libfyaml 1.0.0-rc2 fails, by id: where the oracle is wrong. The
    /// property doesn't need them listed (the reference parser settles each difference), but they show what libfyaml
    /// gets wrong, for a report to it.</summary>
    public static IReadOnlyDictionary<string, string> LibFyamlCaseFailures { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mapping-on-its-keys-line/01-empty-keys"] = MappingOnKeysLine,
        ["mapping-on-its-keys-line/02-key-then-empty-key"] = MappingOnKeysLine,
        ["mapping-on-its-keys-line/03-in-a-sequence"] = MappingOnKeysLine,
        ["empty-key/12-indented-then-nested-on-the-same-line"] = MappingOnKeysLine,
        ["empty-key/14-question-mark-alone-in-flow-sequence"] = QuestionMarkAlone,
    };

    /// <summary>
    /// A difference found by a generated input, put down to a known cause: rewriting the input so the cause can't
    /// apply makes the two parsers agree. Each rewrite only changes text where the cause could be at work.
    /// </summary>
    public sealed record Cause(string Name, Func<string, string> Avoid);

    public static IReadOnlyList<Cause> Causes { get; } =
    [
        // a '*' after a blank that follows a character of a plain scalar (not an indicator that can come before an
        // alias): YamlDotNet takes the word for an alias and reads past the ': ' after it. The rewrite makes it 'x'.
        new(StarWord, input => StarInPlainScalar().Replace(input, "x")),
        // a ':' that starts an entry, after the indentation and any '- ' or '? ' on its line, or alone on a '?' line's
        // next line: the rewrite gives the entry a key, 'k'
        new(EmptyKey, input => FlowEmptyExplicitKey().Replace(EmptyKeyEntry().Replace(input, "k:"), "? k")),
        // NEL, LS and PS: the rewrite makes each an 'x'
        new(UnicodeBreaks, input => input.Replace((char)0x85, 'x').Replace((char)0x2028, 'x').Replace((char)0x2029, 'x')),
        // a document end marker with something after it: the rewrite makes each a comment
        new(LeadingDocumentEnd + "; " + PropertiesOnlyAfterEnd, input => DocumentEndMarker().Replace(input, "#..")),
        // tabs: the rewrite makes each a space
        new(Tabs, input => input.Replace((char)0x09, ' ')),
        // lines of only spaces: the rewrite empties them
        new(SpacesOnlyLines, input => SpacesOnlyLine().Replace(input, "")),
        // reserved indicators: the rewrite makes each an 'x'
        new(ReservedIndicator, input => input.Replace('@', 'x').Replace('`', 'x')),
        // a '+' after a '!' with no blank or flow indicator between (in a tag): the rewrite makes it 'p'
        new(PlusInTag, input => PlusInTagText().Replace(input, "p")),
        // a '#' right after '[' or '{': the rewrite puts a blank before it
        new(CommentAfterFlowStart, input => input.Replace("[#", "[ #").Replace("{#", "{ #")),
        // byte order marks: the rewrite removes them
        new(ByteOrderMark, input => input.Replace(((char)0xFEFF).ToString(), "")),
    ];

    [GeneratedRegex(@"(?m)^ +(?=\r?$)")]
    private static partial Regex SpacesOnlyLine();

    [GeneratedRegex(@"\?(?=[ \t]+[,\]}])")]
    private static partial Regex FlowEmptyExplicitKey();

    [GeneratedRegex(@"(?<=!(?:<[^>\s]*|[^\s,\[\]{}]*))\+")]
    private static partial Regex PlusInTagText();

    [GeneratedRegex(@"(?m)^\.\.\.(?=[ \t]|\r?$)")]
    private static partial Regex DocumentEndMarker();

    [GeneratedRegex(@"(?<=[^\s:\-?,\[{][ \t]+)\*")]
    private static partial Regex StarInPlainScalar();

    [GeneratedRegex(@"(?m)(?<=^[ \t]*(?:[-?][ \t]+)*):(?=[ \t]|\r?$)")]
    private static partial Regex EmptyKeyEntry();

    /// <summary>The known cause of the two outcomes differing on <paramref name="input"/>, if there is one: a single
    /// cause, or else every cause that applies to the input together (an input can have more than one).</summary>
    public static Cause? Explain(string input, Func<string, bool> agree)
    {
        var applying = Causes.Where(c => c.Avoid(input) != input).ToList();
        foreach (var c in applying)
            if (agree(c.Avoid(input))) return c;
        if (applying.Count < 2) return null;
        var all = applying.Aggregate(input, (text, c) => c.Avoid(text));
        return agree(all) ? new Cause(string.Join(" + ", applying.Select(c => c.Name)), text => applying.Aggregate(text, (t, c) => c.Avoid(t))) : null;
    }
}
