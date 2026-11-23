namespace YamlOracle;

/// <summary>
/// How <see cref="ParserProcess"/> talks to a parser process (YamlOracle.Worker, or the reference parser) over its standard input and output, little-endian:
/// <list type="bullet">
/// <item>request: int32 byte count, then the input as UTF-8 (a count of -1 asks the worker to exit);</item>
/// <item>response: one byte (<see cref="Parsed"/> or <see cref="Rejected"/>); the events (on a rejection, those before
/// the error); the parser's first error message, empty if it parsed. Each string is an int32 byte count, then UTF-8.</item>
/// </list>
/// </summary>
public static class WorkerProtocol
{
    public const byte Parsed = 0;
    public const byte Rejected = 1;
    public const int Quit = -1;
}
