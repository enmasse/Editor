namespace Ed;

internal static class EdBufferUtilities
{
    public static EdLineRange ResolveNextLineRange(int lineCount, int currentLine)
    {
        if (lineCount == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        if (currentLine == 0)
        {
            return new EdLineRange(1, 1);
        }

        if (currentLine >= lineCount)
        {
            return new EdLineRange(lineCount, lineCount);
        }

        return new EdLineRange(currentLine + 1, currentLine + 1);
    }

    public static EdLineRange ResolveJoinRange(int lineCount, int currentLine)
    {
        if (lineCount == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        var startLine = currentLine;

        if (startLine == 0)
        {
            startLine = 1;
        }

        var endLine = Math.Min(startLine + 1, lineCount);
        return new EdLineRange(startLine, endLine);
    }

    public static string ResolvePath(string? path, string? currentFilePath, Func<string, string> normalizePath)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return normalizePath(path);
        }

        return currentFilePath ?? throw new InvalidOperationException("No current file name is set.");
    }

    public static IReadOnlyList<string> GetLinesInRange(IReadOnlyList<string> lines, EdLineRange? range)
    {
        var resolvedRange = ResolveRange(lines.Count, range);

        if (resolvedRange is null)
        {
            return [];
        }

        return lines
            .Skip(resolvedRange.Value.StartLine - 1)
            .Take(resolvedRange.Value.EndLine - resolvedRange.Value.StartLine + 1)
            .ToArray();
    }

    public static EdLineRange? ResolveRange(int lineCount, EdLineRange? range)
    {
        if (lineCount == 0)
        {
            return null;
        }

        EdLineRange resolvedRange;

        if (range is null)
        {
            resolvedRange = new EdLineRange(1, lineCount);
        }
        else
        {
            resolvedRange = range.Value;
        }

        ValidateRange(resolvedRange, lineCount);
        return resolvedRange;
    }

    public static EdLineRange ResolveCurrentLineRange(int currentLine)
    {
        var line = currentLine;

        if (line == 0)
        {
            line = 1;
        }

        return new EdLineRange(line, line);
    }

    public static EdLineRange? ResolveRangeForMutation(int lineCount, EdLineRange? range, int currentLine)
    {
        if (lineCount == 0)
        {
            return null;
        }

        EdLineRange resolvedRange;

        if (range is null)
        {
            resolvedRange = ResolveCurrentLineRange(currentLine);
        }
        else
        {
            resolvedRange = range.Value;
        }

        ValidateRange(resolvedRange, lineCount);
        return resolvedRange;
    }

    public static bool IsWholeBuffer(int lineCount, EdLineRange? range)
    {
        if (range is null)
        {
            return true;
        }

        return lineCount > 0 && range.Value.StartLine == 1 && range.Value.EndLine == lineCount;
    }

    public static int ResolveAfterLine(int? afterLine, int currentLine)
    {
        if (afterLine.HasValue)
        {
            return afterLine.Value;
        }

        return currentLine;
    }

    public static int DetermineCurrentLineAfterDeletion(int lineCount, int preferredLine)
    {
        if (lineCount == 0)
        {
            return 0;
        }

        return Math.Min(preferredLine, lineCount);
    }

    public static void ValidateInsertionPoint(int afterLine, int lineCount)
    {
        if (afterLine < 0 || afterLine > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(afterLine));
        }
    }

    public static void ValidateDestination(int destinationLine, int lineCount)
    {
        if (destinationLine < 0 || destinationLine > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationLine));
        }
    }

    public static void ValidateRange(EdLineRange range, int lineCount)
    {
        if (range.StartLine < 1 || range.EndLine < range.StartLine || range.EndLine > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(range));
        }
    }
}
