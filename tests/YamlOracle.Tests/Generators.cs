using System.Text;
using CsCheck;

namespace YamlOracle.Tests;

/// <summary>
/// YAML texts for the differential property, valid or not (both parsers have to reject the invalid ones):
/// <list type="bullet">
/// <item>seeds, from the yaml-test-suite and our cases, with a few small edits: an indicator, a blank, a line break
/// or a short sequence of them inserted, a few characters deleted, or one replaced;</item>
/// <item>documents built from YAML's constructs (block and flow collections, every scalar style, anchors, tags,
/// aliases, comments, document markers) with indicators inside scalars, sometimes edited the same way.</item>
/// </list>
/// </summary>
public static class Generators
{
    static readonly string[] Tokens =
    [
        "*", "&", "!", ":", " ", "#", "-", "?", ",", "[", "]", "{", "}", "'", "\"", "|", ">", "%", "@", "`", "\\",
        "\n", "\t", "\r\n", "a", "*a", "&a", "!!str", "!t", ": ", "- ", "? ", " #", "---", "...", "\n  ", "\n- ", "\n? ",
        "|-", ">+", "|2",
        // NEL, no-break space, line and paragraph separators, byte order mark
        ((char)0x85).ToString(), ((char)0xA0).ToString(), ((char)0x2028).ToString(), ((char)0x2029).ToString(), ((char)0xFEFF).ToString(),
    ];

    static readonly string[] Seeds = Support.SuiteCases.Concat(Support.OwnCases).Select(c => c.Input).ToArray();

    record struct Edit(double At, int Kind, int Length, int Token);

    static readonly Gen<Edit> EditGen = Gen.Select(Gen.Double[0, 1], Gen.Int[0, 2], Gen.Int[1, 3], Gen.Int[0, Tokens.Length - 1],
        (at, kind, length, token) => new Edit(at, kind, length, token));

    static string Apply(string text, Edit[] edits)
    {
        var sb = new StringBuilder(text);
        foreach (var e in edits)
        {
            var at = Math.Min(sb.Length, (int)(e.At * (sb.Length + 1)));
            switch (e.Kind)
            {
                case 0:
                    sb.Insert(at, Tokens[e.Token]);
                    break;
                case 1:
                    sb.Remove(at, Math.Min(e.Length, sb.Length - at));
                    break;
                default:
                    if (at < sb.Length) sb.Remove(at, 1);
                    sb.Insert(at, Tokens[e.Token]);
                    break;
            }
        }
        return sb.ToString();
    }

    public static readonly Gen<string> MutatedSeed =
        Gen.Select(Gen.Int[0, Seeds.Length - 1], EditGen.Array[1, 4], (seed, edits) => Apply(Seeds[seed], edits));

    // words of plain scalars, with indicators inside them and at their start
    const string WordChars = "abxyz019*&!:#-?,[]{}%@`'\".|>~";
    static readonly Gen<string> Word = Gen.Char[WordChars].Array[1, 4].Select(cs => new string(cs));
    static readonly Gen<string> Plain = Word.Array[1, 3].Select(ws => string.Join(" ", ws));

    static readonly Gen<string> Single = Gen.OneOf(Plain, Gen.Const("it''s"), Gen.Const("a\n  b")).Select(s => "'" + s + "'");
    static readonly Gen<string> Double = Gen.OneOf(Plain, Gen.Const("\\t\\n\\x41\\u00e9"), Gen.Const("a\n  b"), Gen.Const("a\\\n  b"), Gen.Const("a\nb"))
        .Select(s => "\"" + s + "\"");

    static Gen<string> Block(int indent) =>
        Gen.Select(Gen.OneOf("|", ">", "|-", ">+", "|2", "|1-"), Plain.Array[1, 3], Gen.Bool,
            (head, lines, blank) =>
            {
                var pad = new string(' ', indent + 2);
                return head + "\n" + string.Join(blank ? "\n\n" : "\n", lines.Select(l => pad + l));
            });

    static readonly Gen<string> Properties = Gen.Frequency(
        (6, Gen.Const("")),
        (1, Gen.OneOf("&a ", "&b ", "&a: ")),
        (1, Gen.OneOf("!t ", "!!str ", "!<tag:x> ", "! ")),
        (1, Gen.Const("&a !t ")));

    static readonly Gen<string> Alias = Gen.OneOf("*a", "*b", "*a:");

    static readonly Gen<string> Comment = Gen.Frequency((5, Gen.Const("")), (1, Gen.Const(" # c")), (1, Gen.Const(" #")));

    static Gen<string> FlowScalar => Gen.Frequency((5, Plain), (2, Single), (2, Double), (1, Alias));

    static Gen<string> Flow(int depth) => depth >= 2
        ? FlowScalar
        : Gen.Frequency(
            (4, FlowScalar),
            (1, Flow(depth + 1).Array[0, 3].Select(xs => "[" + string.Join(", ", xs) + "]")),
            (1, Gen.Select(Flow(depth + 1), Flow(depth + 1), (k, v) => k + ": " + v).Array[0, 3].Select(xs => "{" + string.Join(", ", xs) + "}")),
            (1, Gen.Select(Flow(depth + 1), Flow(depth + 1), (k, v) => "[" + k + ": " + v + "]")),
            (1, Flow(depth + 1).Array[1, 3].Select(xs => "[\n" + string.Join(",\n", xs) + "\n]")));

    /// <summary>A node at <paramref name="indent"/> spaces; the text starts on the current line (after "key:" or "- ").</summary>
    static Gen<string> Node(int depth, int indent)
    {
        var inline = Gen.Frequency((5, FlowScalar), (2, Flow(1)), (1, Block(indent)));
        if (depth >= 3) return Gen.Select(Properties, inline, Comment, (p, n, c) => p + n + c);
        var child = indent + 2;
        var pad = new string(' ', child);
        var key = Gen.Frequency((6, Plain), (1, Single), (1, Double), (1, Alias), (1, Gen.Const("? " + "k")));
        var map = Gen.Select(key, Node(depth + 1, child), Comment, (k, v, c) => (k, v, c)).Array[1, 3]
            .Select(es => string.Concat(es.Select(e => "\n" + pad + e.k + ":" + (e.v.StartsWith('\n') ? e.c + e.v : " " + e.v))));
        var seq = Gen.Select(Node(depth + 1, child), Comment, (v, c) => (v, c)).Array[1, 3]
            .Select(es => string.Concat(es.Select(e => "\n" + pad + "-" + (e.v.StartsWith('\n') ? e.c + e.v : " " + e.v))));
        return Gen.Frequency(
            (4, Gen.Select(Properties, inline, Comment, (p, n, c) => p + n + c)),
            (2, Gen.Select(Properties, map, (p, m) => p.TrimEnd() + m)),
            (2, Gen.Select(Properties, seq, (p, s) => p.TrimEnd() + s)));
    }

    public static readonly Gen<string> Document =
        Gen.Select(Gen.OneOf("", "---\n", "--- ", "%YAML 1.2\n---\n", "%TAG !t! tag:x,2000:\n---\n"), Node(0, -2), Gen.OneOf("\n", "\n...\n", "", "\n---\n"),
            (start, node, end) => start + (node.StartsWith('\n') ? node[1..] : node) + end);

    public static readonly Gen<string> EditedDocument = Gen.Select(Document, EditGen.Array[1, 2], Apply);

    public static readonly Gen<string> Input = Gen.Frequency((3, MutatedSeed), (2, Document), (1, EditedDocument));
}
