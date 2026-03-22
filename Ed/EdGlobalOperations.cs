namespace Ed;

internal static class EdGlobalOperations
{
    public static void Global(
        EdEditor editor,
        EdLineRange? range,
        string pattern,
        string commandList,
        EdGlobalMode mode = EdGlobalMode.Match)
    {
        var resolvedRange = EdBufferUtilities.ResolveRange(editor.BufferLines.Count, range);

        if (resolvedRange is null)
        {
            return;
        }

        var matcher = EdSearchEngine.CreateSearchMatcher(
            editor.RegexEngine,
            editor.BufferLines,
            resolvedRange.Value,
            pattern);
        var matchingLines = new List<int>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            if (matcher(editor.BufferLines[lineNumber - 1]))
            {
                matchingLines.Add(lineNumber);
            }
        }

        var command = commandList.Trim();
        int[] linesToProcess;

        if (mode == EdGlobalMode.NonMatch)
        {
            linesToProcess = Enumerable.Range(resolvedRange.Value.StartLine, resolvedRange.Value.EndLine - resolvedRange.Value.StartLine + 1)
                .Except(matchingLines)
                .ToArray();
        }
        else
        {
            linesToProcess = matchingLines.ToArray();
        }

        if (!string.Equals(command, "d", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Only delete is supported by the current global implementation.");
        }

        if (linesToProcess.Length == 0)
        {
            return;
        }

        editor.SaveUndoState();

        foreach (var lineNumber in linesToProcess.OrderByDescending(static value => value))
        {
            editor.BufferLines.RemoveAt(lineNumber - 1);
        }

        editor.ClearMarks();

        if (editor.BufferLines.Count == 0)
        {
            editor.CurrentLineNumberInternal = 0;
        }
        else
        {
            editor.CurrentLineNumberInternal = Math.Min(linesToProcess[0], editor.BufferLines.Count);
        }

        editor.IsModifiedInternal = true;
        editor.LastErrorMessageInternal = null;
    }

    public static EdCommandResult ExecuteGlobalCommand(
        EdEditor editor,
        EdLineRange? range,
        EdGlobalMode mode,
        string pattern,
        string commandList)
    {
        if (string.Equals(commandList, "d", StringComparison.Ordinal))
        {
            Global(editor, range, pattern, commandList, mode);
            return editor.CreateCommandResult(bufferChanged: true, []);
        }

        if (string.Equals(commandList, "p", StringComparison.Ordinal))
        {
            var output = PrintGlobalMatches(editor, range, pattern, mode, EdPrintMode.Normal);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (string.Equals(commandList, "n", StringComparison.Ordinal))
        {
            var output = PrintGlobalMatches(editor, range, pattern, mode, EdPrintMode.Numbered);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (string.Equals(commandList, "l", StringComparison.Ordinal))
        {
            var output = PrintGlobalMatches(editor, range, pattern, mode, EdPrintMode.Literal);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (commandList.StartsWith('s'))
        {
            ExecuteGlobalSubstitute(editor, range, pattern, commandList, mode);
            return editor.CreateCommandResult(bufferChanged: true, []);
        }

        throw new NotSupportedException("Only `d`, `p`, `n`, `l`, and `s` are supported by the current global implementation.");
    }

    public static void ExecuteGlobalSubstitute(
        EdEditor editor,
        EdLineRange? range,
        string pattern,
        string commandList,
        EdGlobalMode mode)
    {
        var resolvedRange = EdBufferUtilities.ResolveRange(editor.BufferLines.Count, range);

        if (resolvedRange is null)
        {
            return;
        }

        var matcher = EdSearchEngine.CreateSearchMatcher(
            editor.RegexEngine,
            editor.BufferLines,
            resolvedRange.Value,
            pattern);
        var matchingLines = new List<int>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            if (matcher(editor.BufferLines[lineNumber - 1]))
            {
                matchingLines.Add(lineNumber);
            }
        }

        int[] linesToProcess;

        if (mode == EdGlobalMode.NonMatch)
        {
            linesToProcess = Enumerable.Range(resolvedRange.Value.StartLine, resolvedRange.Value.EndLine - resolvedRange.Value.StartLine + 1)
                .Except(matchingLines)
                .ToArray();
        }
        else
        {
            linesToProcess = matchingLines.ToArray();
        }

        if (linesToProcess.Length == 0)
        {
            return;
        }

        var substituteBody = commandList[1..];

        if (!EdCommandTextParser.TryParseDelimitedArguments(substituteBody, out var substitutePattern, out var replacement, out var flags))
        {
            throw new NotSupportedException($"Unsupported command '{commandList}'.");
        }

        var options = new EdSubstitutionOptions(
            ReplaceAllOnLine: flags.Contains('g', StringComparison.Ordinal),
            Occurrence: EdEditorTextUtilities.ParseOccurrence(flags),
            UsePreviousPattern: string.IsNullOrEmpty(substitutePattern));
        string actualPattern;

        if (options.UsePreviousPattern)
        {
            actualPattern = editor.LastSubstitutionPatternInternal ?? throw new InvalidOperationException("No previous substitution pattern is available.");
        }
        else
        {
            actualPattern = substitutePattern;
        }

        editor.SaveUndoState();
        var changed = false;
        var lastChangedLine = editor.CurrentLineNumberInternal;
        var lastNextSearchIndex = 0;

        foreach (var lineNumber in linesToProcess)
        {
            var updatedLine = EdEditorTextUtilities.ApplySubstitution(
                editor.RegexEngine,
                editor.BufferLines[lineNumber - 1],
                actualPattern,
                replacement,
                options,
                0,
                out var replaced,
                out var nextSearchIndex);

            if (!string.Equals(updatedLine, editor.BufferLines[lineNumber - 1], StringComparison.Ordinal))
            {
                editor.BufferLines[lineNumber - 1] = updatedLine;
                changed = true;
                lastChangedLine = lineNumber;

                if (replaced)
                {
                    lastNextSearchIndex = nextSearchIndex;
                }
                else
                {
                    lastNextSearchIndex = 0;
                }
            }
        }

        if (changed)
        {
            editor.ClearMarks();
            editor.CurrentLineNumberInternal = lastChangedLine;
            editor.LastSubstitutionLineNumberInternal = lastChangedLine;
            editor.LastSubstitutionNextSearchIndexInternal = lastNextSearchIndex;
            editor.IsModifiedInternal = true;
        }

        editor.LastSubstitutionPatternInternal = actualPattern;
        editor.LastErrorMessageInternal = null;
    }

    public static IReadOnlyList<string> PrintGlobalMatches(
        EdEditor editor,
        EdLineRange? range,
        string pattern,
        EdGlobalMode mode,
        EdPrintMode printMode)
    {
        var resolvedRange = EdBufferUtilities.ResolveRange(editor.BufferLines.Count, range);

        if (resolvedRange is null)
        {
            return [];
        }

        var matcher = EdSearchEngine.CreateSearchMatcher(
            editor.RegexEngine,
            editor.BufferLines,
            resolvedRange.Value,
            pattern);
        var matchingLines = new List<string>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            var isMatch = matcher(editor.BufferLines[lineNumber - 1]);

            if ((mode == EdGlobalMode.Match && isMatch) || (mode == EdGlobalMode.NonMatch && !isMatch))
            {
                matchingLines.Add(EdEditorTextUtilities.FormatLine(editor.BufferLines[lineNumber - 1], lineNumber, printMode));
                editor.CurrentLineNumberInternal = lineNumber;
            }
        }

        return matchingLines;
    }
}
