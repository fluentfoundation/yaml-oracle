using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using YamlOracle;
using YamlOracle.Worker;

// YamlOracle.Worker: parses each input it reads with libfyaml and writes back the events (see WorkerProtocol).
// YAML_ORACLE_LIBFYAML, if set, is the path of the libfyaml shared library to load instead of the one beside it.
//
//   YamlOracle.Worker --events FILE   prints FILE's events, or "ERROR <message>" and the events before it

NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, assembly, path) =>
    name == "fyaml" && Environment.GetEnvironmentVariable("YAML_ORACLE_LIBFYAML") is { Length: > 0 } file
        ? NativeLibrary.Load(file)
        : 0);

if (args is ["--events", var file])
{
    var (parsed, events, message) = Parse(File.ReadAllBytes(file));
    Console.Out.Write(parsed ? events : $"ERROR {message}\n{events}");
    return parsed ? 0 : 1;
}

using var stdin = Console.OpenStandardInput();
using var stdout = Console.OpenStandardOutput();
var reader = new BinaryReader(stdin);
var writer = new BinaryWriter(stdout);
while (true)
{
    int count;
    try { count = reader.ReadInt32(); }
    catch (EndOfStreamException) { return 0; }
    if (count == WorkerProtocol.Quit) return 0;
    var (ok, text, error) = Parse(reader.ReadBytes(count));
    writer.Write(ok ? WorkerProtocol.Parsed : WorkerProtocol.Rejected);
    foreach (var s in new[] { text, error })
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
    writer.Flush();
}

static unsafe (bool Parsed, string Events, string Message) Parse(byte[] input)
{
    var cfg = new Native.ParseCfg { Flags = Native.Quiet };
    var parser = Native.ParserCreate(&cfg);
    if (parser == 0) throw new InvalidOperationException("fy_parser_create failed");
    Native.CollectErrors(parser);
    // libfyaml reads the string in place while parsing, so it stays pinned until the parser is gone
    var buffer = (byte*)NativeMemory.Alloc((nuint)input.Length + 1);
    try
    {
        input.CopyTo(new Span<byte>(buffer, input.Length));
        buffer[input.Length] = 0;
        if (Native.ParserSetString(parser, buffer, (nuint)input.Length) != 0) throw new InvalidOperationException("fy_parser_set_string failed");
        var sb = new StringBuilder();
        for (var ev = Native.ParserParse(parser); ev != 0; ev = Native.ParserParse(parser))
        {
            try { Write(sb, ev); }
            finally { Native.ParserEventFree(parser, ev); }
        }
        return Native.ParserGetStreamError(parser) ? (false, sb.ToString(), Native.FirstError(parser) ?? "") : (true, sb.ToString(), "");
    }
    finally
    {
        Native.ParserDestroy(parser);
        NativeMemory.Free(buffer);
    }
}

static void Write(StringBuilder sb, nint ev)
{
    string? Anchor() => Native.Text(Native.EventGetAnchorToken(ev));
    string? Tag() => Native.Text(Native.EventGetTagToken(ev));
    bool Flow() => Native.EventGetNodeStyle(ev) == Native.Flow;
    switch (Native.EventType(ev))
    {
        case Native.StreamStart: TestSuiteEvents.StreamStart(sb); break;
        case Native.StreamEnd: TestSuiteEvents.StreamEnd(sb); break;
        case Native.DocumentStart: TestSuiteEvents.DocumentStart(sb, !Native.DocumentEventIsImplicit(ev)); break;
        case Native.DocumentEnd: TestSuiteEvents.DocumentEnd(sb, !Native.DocumentEventIsImplicit(ev)); break;
        case Native.MappingStart: TestSuiteEvents.MappingStart(sb, Flow(), Anchor(), Tag()); break;
        case Native.MappingEnd: TestSuiteEvents.MappingEnd(sb); break;
        case Native.SequenceStart: TestSuiteEvents.SequenceStart(sb, Flow(), Anchor(), Tag()); break;
        case Native.SequenceEnd: TestSuiteEvents.SequenceEnd(sb); break;
        case Native.Alias: TestSuiteEvents.Alias(sb, Native.Text(Native.EventGetToken(ev)) ?? ""); break;
        case Native.Scalar:
            var value = Native.EventGetToken(ev);
            var style = Native.TokenScalarStyle(value) switch
            {
                Native.SingleQuoted => '\'',
                Native.DoubleQuoted => '"',
                Native.Literal => '|',
                Native.Folded => '>',
                _ => ':',
            };
            TestSuiteEvents.Scalar(sb, Anchor(), Tag(), style, Native.Text(value) ?? "");
            break;
        default: throw new InvalidOperationException($"unknown libfyaml event type {Native.EventType(ev)}");
    }
}
