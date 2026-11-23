using System.Diagnostics;
using System.Text;

namespace YamlOracle;

/// <summary>
/// A parser in a process of its own that stays up between inputs and speaks <see cref="WorkerProtocol"/>:
/// <list type="bullet">
/// <item><see cref="LibFyaml"/>: YamlOracle.Worker, which calls libfyaml by P/Invoke. A native crash ends only the
/// worker and comes back as <see cref="OutcomeKind.Crashed"/>; the next input starts a new one.</item>
/// <item><see cref="Reference"/>: the YAML 1.2 reference parser, generated from the spec's grammar
/// (tools/reference/reference.js serve). Slow, so it settles differences rather than parsing every input.</item>
/// </list>
/// Not thread-safe: use one instance per thread.
/// </summary>
public sealed class ParserProcess(string name, Func<ProcessStartInfo> startInfo, TimeSpan? timeout = null) : IDisposable
{
    readonly TimeSpan timeout = timeout ?? TimeSpan.FromSeconds(10);
    static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);
    Process? process;
    BinaryWriter? input;
    BinaryReader? output;
    readonly Queue<string> stderr = new();

    public string Name { get; } = name;

    /// <summary>The libfyaml release eng/libfyaml.cmake built, from libfyaml.version beside the library.</summary>
    public static string LibFyamlVersion { get; } = ReadVersion();

    /// <summary>libfyaml, through YamlOracle.Worker beside this assembly.</summary>
    public static ParserProcess LibFyaml(TimeSpan? timeout = null) => new($"libfyaml {LibFyamlVersion}", () =>
    {
        var dir = AppContext.BaseDirectory;
        var apphost = Path.Combine(dir, OperatingSystem.IsWindows() ? "YamlOracle.Worker.exe" : "YamlOracle.Worker");
        return File.Exists(apphost)
            ? new ProcessStartInfo(apphost)
            : new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { ArgumentList = { Path.Combine(dir, "YamlOracle.Worker.dll") } };
    }, timeout);

    /// <summary>The reference parser: <paramref name="script"/> is tools/reference/reference.js, run with
    /// <paramref name="node"/>. Its dependencies have to be installed (npm ci in tools/reference).</summary>
    public static ParserProcess Reference(string script, string node = "node", TimeSpan? timeout = null) =>
        new("the YAML 1.2 reference parser", () => new ProcessStartInfo(node) { ArgumentList = { script, "serve" } }, timeout ?? TimeSpan.FromSeconds(60));

    public ParseOutcome Parse(string yaml)
    {
        if (!Start()) return ParseOutcome.Crash("", $"{Name} did not start in {StartTimeout.TotalMinutes:0} min");
        return Send(yaml, timeout);
    }

    /// <summary>Sends one input and reads the answer on the calling thread; a timer kills the process if the answer
    /// takes longer than <paramref name="wait"/>. (Waiting on a task instead starves the thread pool when many
    /// threads parse at once, as CsCheck's do.)</summary>
    ParseOutcome Send(string yaml, TimeSpan wait)
    {
        var bytes = Encoding.UTF8.GetBytes(yaml);
        var running = process!;
        var timedOut = 0;
        using var timer = new Timer(_ =>
        {
            Interlocked.Exchange(ref timedOut, 1);
            try { running.Kill(entireProcessTree: true); }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }, null, wait, Timeout.InfiniteTimeSpan);
        try
        {
            input!.Write(bytes.Length);
            input.Write(bytes);
            input.Flush();
            var kind = output!.ReadByte();
            var events = Encoding.UTF8.GetString(output.ReadBytes(output.ReadInt32()));
            var message = Encoding.UTF8.GetString(output.ReadBytes(output.ReadInt32()));
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            if (Volatile.Read(ref timedOut) == 0)
                return kind == WorkerProtocol.Parsed ? ParseOutcome.Parsed(events) : ParseOutcome.Rejected(events, message);
        }
        catch (Exception e) when (e is IOException or EndOfStreamException or ObjectDisposedException) { }
        timer.Change(Timeout.Infinite, Timeout.Infinite);
        if (Volatile.Read(ref timedOut) == 1)
        {
            Stop();
            return new ParseOutcome(OutcomeKind.TimedOut, "", $"{Name}: no answer in {wait.TotalSeconds:0.#} s");
        }
        var exit = running.WaitForExit(2000) ? $"exit code {running.ExitCode}" : "no exit code";
        string tail;
        lock (stderr) tail = string.Join(" | ", stderr);
        Stop();
        return ParseOutcome.Crash("", $"{Name} died ({exit}){(tail.Length > 0 ? ": " + tail : "")}");
    }

    /// <summary>Starts the process if it isn't running, and waits for its first answer (to an empty input) with a long
    /// timeout: starting can be slow, above all with many at once, and that isn't an input taking long.</summary>
    bool Start()
    {
        if (process is { HasExited: false }) return true;
        Stop();
        var info = startInfo();
        info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
        info.UseShellExecute = false;
        process = Process.Start(info) ?? throw new InvalidOperationException($"could not start {Name}");
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderr)
            {
                stderr.Enqueue(e.Data);
                while (stderr.Count > 5) stderr.Dequeue();
            }
        };
        process.BeginErrorReadLine();
        input = new BinaryWriter(process.StandardInput.BaseStream);
        output = new BinaryReader(process.StandardOutput.BaseStream);
        return Send("", StartTimeout).Kind is OutcomeKind.Events or OutcomeKind.Error;
    }

    void Stop()
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { } // exited meanwhile
        process.Dispose();
        process = null;
        input = null;
        output = null;
        lock (stderr) stderr.Clear();
    }

    public void Dispose()
    {
        if (process is { HasExited: false })
        {
            try
            {
                input!.Write(WorkerProtocol.Quit);
                input.Flush();
                process.WaitForExit(2000);
            }
            catch (IOException) { }
        }
        Stop();
    }

    static string ReadVersion()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "libfyaml.version");
        return File.Exists(file) ? File.ReadAllText(file).Trim() : "?";
    }
}
