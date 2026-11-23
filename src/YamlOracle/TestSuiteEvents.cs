using System.Text;

namespace YamlOracle;

/// <summary>
/// Writes parse events in the yaml-test-suite's test.event format (one event per line), the same way for every
/// parser, so two parsers agree exactly when their text is equal. The escapes are those of the YAML reference
/// parser's test receiver (yaml/yaml-reference-parser, parser-1.2/javascript/lib/test-receiver.js).
/// </summary>
public static class TestSuiteEvents
{
    public static void StreamStart(StringBuilder sb) => sb.Append("+STR\n");
    public static void StreamEnd(StringBuilder sb) => sb.Append("-STR\n");
    public static void DocumentStart(StringBuilder sb, bool @explicit) => sb.Append(@explicit ? "+DOC ---\n" : "+DOC\n");
    public static void DocumentEnd(StringBuilder sb, bool @explicit) => sb.Append(@explicit ? "-DOC ...\n" : "-DOC\n");
    public static void MappingStart(StringBuilder sb, bool flow, string? anchor, string? tag) => Node(sb, "+MAP", flow ? "{}" : null, anchor, tag);
    public static void MappingEnd(StringBuilder sb) => sb.Append("-MAP\n");
    public static void SequenceStart(StringBuilder sb, bool flow, string? anchor, string? tag) => Node(sb, "+SEQ", flow ? "[]" : null, anchor, tag);
    public static void SequenceEnd(StringBuilder sb) => sb.Append("-SEQ\n");
    public static void Alias(StringBuilder sb, string name) => sb.Append("=ALI *").Append(name).Append('\n');

    /// <param name="style">':' plain, '\'' single-quoted, '"' double-quoted, '|' literal, '>' folded</param>
    public static void Scalar(StringBuilder sb, string? anchor, string? tag, char style, string value)
    {
        sb.Append("=VAL");
        Properties(sb, anchor, tag);
        sb.Append(' ').Append(style);
        Escape(sb, value);
        sb.Append('\n');
    }

    static void Node(StringBuilder sb, string head, string? marker, string? anchor, string? tag)
    {
        sb.Append(head);
        if (marker is not null) sb.Append(' ').Append(marker);
        Properties(sb, anchor, tag);
        sb.Append('\n');
    }

    static void Properties(StringBuilder sb, string? anchor, string? tag)
    {
        if (!string.IsNullOrEmpty(anchor)) sb.Append(" &").Append(anchor);
        if (!string.IsNullOrEmpty(tag)) sb.Append(" <").Append(tag).Append('>');
    }

    public static void Escape(StringBuilder sb, string value)
    {
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '\\' => @"\\",
                '\0' => @"\0",
                '\a' => @"\a",
                '\b' => @"\b",
                '\t' => @"\t",
                '\n' => @"\n",
                '\v' => @"\v",
                '\f' => @"\f",
                '\r' => @"\r",
                '\u001b' => @"\e",
                '\u0085' => @"\N",
                (char)0xA0 => @"\_",
                (char)0x2028 => @"\L",
                (char)0x2029 => @"\P",
                _ => null,
            } ?? c.ToString());
        }
    }
}
