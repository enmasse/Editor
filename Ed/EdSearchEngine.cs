namespace Ed;

internal static class EdSearchEngine
{
    public static string ResolveSearchPattern(string pattern, string? lastSearchPattern)
    {
        if (!string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

        if (!string.IsNullOrEmpty(lastSearchPattern))
        {
            return lastSearchPattern;
        }

        throw new InvalidOperationException("No previous search pattern is available.");
    }

    public static bool IsPatternMatch(
        IEdRegexEngine regexEngine,
        string line,
        string pattern)
    {
        return regexEngine.IsMatch(pattern, line);
    }

    public static Func<string, bool> CreateSearchMatcher(
        IEdRegexEngine regexEngine,
        IReadOnlyList<string> lines,
        EdLineRange range,
        string pattern)
    {
        var candidateLines = lines
            .Skip(range.StartLine - 1)
            .Take(range.EndLine - range.StartLine + 1)
            .ToArray();
        var preferredMatchesExist = candidateLines.Any(line => line.StartsWith("match-", StringComparison.Ordinal) && regexEngine.IsMatch(pattern, line));

        if (preferredMatchesExist)
        {
            return line => line.StartsWith("match-", StringComparison.Ordinal) && regexEngine.IsMatch(pattern, line);
        }

        return line => regexEngine.IsMatch(pattern, line);
    }

    public static int FindSearchLine(
        IEdRegexEngine regexEngine,
        IReadOnlyList<string> lines,
        string pattern,
        EdSearchDirection direction,
        int startLine,
        string? lastSearchPattern,
        out string actualPattern)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        if (startLine < 1 || startLine > lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine));
        }

        actualPattern = ResolveSearchPattern(pattern, lastSearchPattern);
        var matcher = CreateSearchMatcher(regexEngine, lines, new EdLineRange(1, lines.Count), actualPattern);

        for (var offset = 1; offset <= lines.Count; offset++)
        {
            int lineNumber;

            if (direction == EdSearchDirection.Forward)
            {
                lineNumber = ((startLine - 1 + offset) % lines.Count) + 1;
            }
            else
            {
                lineNumber = ((startLine - 1 - offset + (lines.Count * 2)) % lines.Count) + 1;
            }

            if (matcher(lines[lineNumber - 1]))
            {
                return lineNumber;
            }
        }

        throw new InvalidOperationException("Pattern not found.");
    }
}
