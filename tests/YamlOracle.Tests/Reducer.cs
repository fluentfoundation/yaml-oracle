namespace YamlOracle.Tests;

/// <summary>
/// Makes a failing input smaller while it keeps failing: removes runs of lines, then runs of characters, halving the
/// run length down to one (delta debugging, without the complements). Then it tries replacing each remaining
/// character that isn't YAML syntax with 'a', so what's left is the syntax that matters.
/// </summary>
public static class Reducer
{
    public static string Reduce(string input, Func<string, bool> fails, int budget = 4000)
    {
        var current = input;
        bool Try(string candidate)
        {
            if (budget-- <= 0 || candidate == current || !fails(candidate)) return false;
            current = candidate;
            return true;
        }

        // lines (keeping each line's break with it)
        for (var size = Math.Max(1, Lines(current).Count / 2); size >= 1; size /= 2)
        {
            for (var start = 0; start < Lines(current).Count;)
            {
                var lines = Lines(current);
                if (!Try(string.Concat(lines.Take(start).Concat(lines.Skip(start + size))))) start += size;
            }
        }
        // characters
        for (var size = Math.Max(1, current.Length / 2); size >= 1; size /= 2)
        {
            for (var start = 0; start < current.Length;)
            {
                if (!Try(current.Remove(start, Math.Min(size, current.Length - start)))) start += size;
            }
        }
        // letters and digits for the same letter: one word reads like another
        for (var i = 0; i < current.Length; i++)
        {
            if (char.IsLetterOrDigit(current[i]) && current[i] != 'a')
                Try(current[..i] + "a" + current[(i + 1)..]);
        }
        return current;
    }

    static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            lines.Add(text[start..(i + 1)]);
            start = i + 1;
        }
        if (start < text.Length) lines.Add(text[start..]);
        return lines;
    }
}
