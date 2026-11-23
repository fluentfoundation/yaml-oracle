using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlOracle;

/// <summary>YamlDotNet's parser, in process, with its events written as <see cref="TestSuiteEvents"/>.</summary>
public static class YamlDotNetEvents
{
    public static string Version { get; } =
        typeof(Parser).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]
        ?? typeof(Parser).Assembly.GetName().Version?.ToString() ?? "?";

    public static ParseOutcome Parse(string yaml)
    {
        var sb = new StringBuilder();
        try
        {
            var parser = new Parser(new StringReader(yaml));
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case StreamStart: TestSuiteEvents.StreamStart(sb); break;
                    case StreamEnd: TestSuiteEvents.StreamEnd(sb); break;
                    case DocumentStart d: TestSuiteEvents.DocumentStart(sb, !d.IsImplicit); break;
                    case DocumentEnd d: TestSuiteEvents.DocumentEnd(sb, !d.IsImplicit); break;
                    case MappingStart m: TestSuiteEvents.MappingStart(sb, m.Style == MappingStyle.Flow, Anchor(m), Tag(m)); break;
                    case MappingEnd: TestSuiteEvents.MappingEnd(sb); break;
                    case SequenceStart s: TestSuiteEvents.SequenceStart(sb, s.Style == SequenceStyle.Flow, Anchor(s), Tag(s)); break;
                    case SequenceEnd: TestSuiteEvents.SequenceEnd(sb); break;
                    case AnchorAlias a: TestSuiteEvents.Alias(sb, a.Value.Value); break;
                    case Scalar s: TestSuiteEvents.Scalar(sb, Anchor(s), Tag(s), Style(s.Style), s.Value); break;
                    case Comment: break; // the parser skips comments by default; the suite's events have none
                    default: throw new InvalidOperationException("unexpected event " + parser.Current?.GetType().Name);
                }
            }
            return ParseOutcome.Parsed(sb.ToString());
        }
        catch (YamlException e) { return ParseOutcome.Rejected(sb.ToString(), e.Message); }
        catch (Exception e) when (e is not OutOfMemoryException) { return ParseOutcome.Crash(sb.ToString(), $"{e.GetType().Name}: {e.Message}"); }
    }

    static string? Anchor(NodeEvent e) => e.Anchor.IsEmpty ? null : e.Anchor.Value;
    static string? Tag(NodeEvent e) => e.Tag.IsEmpty ? null : e.Tag.Value;

    static char Style(ScalarStyle s) => s switch
    {
        ScalarStyle.SingleQuoted => '\'',
        ScalarStyle.DoubleQuoted => '"',
        ScalarStyle.Literal => '|',
        ScalarStyle.Folded => '>',
        _ => ':',
    };
}
