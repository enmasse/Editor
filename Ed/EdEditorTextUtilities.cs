namespace Ed;

internal static class EdEditorTextUtilities
{
    public static bool ShouldCaptureError(Exception exception)
    {
        return exception is InvalidOperationException
            || exception is NotSupportedException
            || exception is ArgumentException
            || exception is ArgumentOutOfRangeException
            || exception is KeyNotFoundException;
    }

    public static IReadOnlyList<string> ParseTextInputLines(string command)
    {
        var normalized = command
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var newlineIndex = normalized.IndexOf('\n');

        if (newlineIndex < 0)
        {
            throw new NotSupportedException($"Unsupported command '{command}'.");
        }

        var lines = normalized[(newlineIndex + 1)..]
            .Split('\n')
            .ToList();

        if (lines.Count == 0 || !string.Equals(lines[^1], ".", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported command '{command}'.");
        }

        lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    public static string FormatLine(string line, int lineNumber, EdPrintMode mode)
    {
        if (mode == EdPrintMode.Numbered)
        {
            return $"{lineNumber}\t{line}";
        }

        if (mode == EdPrintMode.Literal)
        {
            return line.Replace("\t", "\\t", StringComparison.Ordinal) + "$";
        }

        return line;
    }

    public static int ParseOccurrence(string flags)
    {
        var digits = new string(flags.Where(char.IsDigit).ToArray());

        if (int.TryParse(digits, out var occurrence) && occurrence > 0)
        {
            return occurrence;
        }

        return 1;
    }

    public static string ApplySubstitution(
        IEdRegexEngine regexEngine,
        string source,
        string pattern,
        string replacement,
        EdSubstitutionOptions options,
        int startIndex,
        out bool replaced,
        out int nextSearchIndex)
    {
        ArgumentNullException.ThrowIfNull(regexEngine);
        replaced = false;
        nextSearchIndex = 0;
        var normalizedReplacement = NormalizeReplacementText(replacement);

        if (string.IsNullOrEmpty(pattern))
        {
            return source;
        }

        if (options.ReplaceAllOnLine)
        {
            replaced = regexEngine.IsMatch(pattern, source);
            nextSearchIndex = source.Length;
            return regexEngine.Replace(pattern, source, normalizedReplacement);
        }

        var targetOccurrence = options.Occurrence;

        if (targetOccurrence <= 0)
        {
            targetOccurrence = 1;
        }

        var currentIndex = Math.Max(0, startIndex);

        if (currentIndex > source.Length)
        {
            currentIndex = source.Length;
        }

        var matchCount = 0;
        var match = regexEngine.Match(pattern, source, currentIndex);

        while (match.Success)
        {
            matchCount++;

            if (matchCount == targetOccurrence)
            {
                var replacementText = match.ExpandReplacement(normalizedReplacement);
                replaced = true;
                nextSearchIndex = match.Index + replacementText.Length;
                return regexEngine.Replace(pattern, source, normalizedReplacement, 1, match.Index);
            }

            currentIndex = match.Index + match.Length;

            if (match.Length == 0)
            {
                currentIndex++;
            }

            if (currentIndex > source.Length)
            {
                break;
            }

            match = regexEngine.Match(pattern, source, currentIndex);
        }

        return source;
    }

    private static string NormalizeReplacementText(string replacement)
    {
        var builder = new System.Text.StringBuilder();

        for (var index = 0; index < replacement.Length; index++)
        {
            if (replacement[index] == '\\' && index + 1 < replacement.Length && replacement[index + 1] == '&')
            {
                builder.Append('&');
                index++;
            }
            else if (replacement[index] == '&')
            {
                builder.Append("$0");
            }
            else
            {
                builder.Append(replacement[index]);
            }
        }

        return builder.ToString();
    }
}
