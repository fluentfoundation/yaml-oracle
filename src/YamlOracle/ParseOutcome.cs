namespace YamlOracle;

public enum OutcomeKind
{
    /// <summary>The whole input parsed; <see cref="ParseOutcome.Events"/> is the event stream.</summary>
    Events,
    /// <summary>The parser rejected the input; <see cref="ParseOutcome.Events"/> holds the events before the error.</summary>
    Error,
    /// <summary>The parser failed some other way: a native crash of the worker, or an exception that isn't a parse
    /// error (a bug of the parser itself).</summary>
    Crashed,
    /// <summary>No answer in time; the worker was stopped.</summary>
    TimedOut,
}

/// <summary>What one parser made of one input.</summary>
public sealed record ParseOutcome(OutcomeKind Kind, string Events, string? Message = null)
{
    public static ParseOutcome Parsed(string events) => new(OutcomeKind.Events, events);
    public static ParseOutcome Rejected(string events, string? message) => new(OutcomeKind.Error, events, message);
    public static ParseOutcome Crash(string events, string message) => new(OutcomeKind.Crashed, events, message);

    /// <summary>Both parsed it to the same events, or both rejected it. Where a parser notices an error, and so the
    /// events before it, differ between correct parsers, so those are not compared.</summary>
    public bool AgreesWith(ParseOutcome other) => (Kind, other.Kind) switch
    {
        (OutcomeKind.Events, OutcomeKind.Events) => Events == other.Events,
        (OutcomeKind.Error, OutcomeKind.Error) => true,
        _ => false,
    };

    public override string ToString() => Kind switch
    {
        OutcomeKind.Events => Events,
        _ => $"{Kind.ToString().ToUpperInvariant()}{(Message is null ? "" : ": " + Message)}\n{Events}",
    };
}
