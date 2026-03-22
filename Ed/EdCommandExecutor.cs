namespace Ed;

internal static class EdCommandExecutor
{
    public static EdCommandResult Execute(
        EdEditor editor,
        string commandText)
    {
        var trimmed = commandText.Trim();

        if (string.Equals(trimmed, ",", StringComparison.Ordinal) || string.Equals(trimmed, ",p", StringComparison.Ordinal))
        {
            var output = editor.Print();
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (string.Equals(trimmed, ",n", StringComparison.Ordinal))
        {
            var output = editor.Print(mode: EdPrintMode.Numbered);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (string.Equals(trimmed, ",l", StringComparison.Ordinal))
        {
            var output = editor.Print(mode: EdPrintMode.Literal);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }

        if (trimmed.StartsWith('!'))
        {
            editor.ExecuteShellCommand(trimmed[1..].TrimStart());
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        if (EdAddressParser.TryParseSearchCommand(trimmed, out var searchDirection, out var searchPattern))
        {
            return HandleSearchCommand(
                editor,
                searchDirection,
                searchPattern);
        }

        var parsedCommand = EdAddressParser.ParseCommand(
            commandText,
            editor.LineCount,
            editor.CurrentLineNumber,
            editor.ResolveMark,
            editor.FindSearchLine);
        var range = parsedCommand.Range;
        var command = parsedCommand.CommandText;

        if (EdAddressParser.TryParseGlobalCommand(command, out var globalMode, out var globalPattern, out var globalCommandList))
        {
            EdLineRange? effectiveRange;

            if (parsedCommand.HasAddress)
            {
                effectiveRange = range;
            }
            else
            {
                effectiveRange = null;
            }

            return EdGlobalOperations.ExecuteGlobalCommand(
                editor,
                effectiveRange,
                globalMode,
                globalPattern,
                globalCommandList);
        }

        if (string.IsNullOrEmpty(command))
        {
            return HandleEmptyCommand(
                editor,
                parsedCommand,
                range);
        }

        if (command[0] == 'a' || command[0] == 'i' || command[0] == 'c')
        {
            return HandleTextInputCommand(
                editor,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 's')
        {
            return HandleSubstituteCommand(
                editor,
                commandText,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 'p' || command[0] == 'l' || command[0] == 'n')
        {
            return HandlePrintCommand(
                editor,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 'd')
        {
            var effectiveRange = ResolveDeleteRange(
                editor,
                parsedCommand,
                range);
            editor.Delete(effectiveRange);
            return editor.CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'j')
        {
            var joinRange = ResolveJoinRange(
                editor,
                parsedCommand,
                range);
            editor.Join(joinRange);
            return editor.CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'm')
        {
            return HandleMoveCommand(
                editor,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 't')
        {
            return HandleCopyCommand(
                editor,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 'k')
        {
            return HandleMarkCommand(
                editor,
                commandText,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 'u')
        {
            var hadUndoSnapshot = editor.HasUndoSnapshot;
            editor.Undo();
            return editor.CreateCommandResult(bufferChanged: hadUndoSnapshot, []);
        }

        if (command[0] == '=')
        {
            return HandleLineNumberCommand(
                editor,
                parsedCommand,
                range);
        }

        if (command[0] == 'f')
        {
            return HandleFileCommand(
                editor,
                command);
        }

        if (command[0] == 'P')
        {
            editor.IsPromptEnabled = !editor.IsPromptEnabled;
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'H')
        {
            editor.IsVerboseErrorsEnabled = !editor.IsVerboseErrorsEnabled;
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'h')
        {
            if (string.IsNullOrEmpty(editor.LastErrorMessageInternal))
            {
                return editor.CreateCommandResult(bufferChanged: false, []);
            }
            else
            {
                return editor.CreateCommandResult(bufferChanged: false, [editor.LastErrorMessageInternal]);
            }
        }

        if (command[0] == 'w' || command[0] == 'W')
        {
            return HandleWriteCommand(
                editor,
                range,
                command);
        }

        if (command[0] == 'z')
        {
            return HandleScrollCommand(
                editor,
                parsedCommand,
                range,
                command);
        }

        if (command[0] == 'r')
        {
            return HandleReadCommand(
                editor,
                range,
                command);
        }

        if (command[0] == 'e' || command[0] == 'E')
        {
            return HandleEditCommand(
                editor,
                command);
        }

        if (string.Equals(trimmed, "q", StringComparison.Ordinal))
        {
            editor.Quit();
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        if (string.Equals(trimmed, "q!", StringComparison.Ordinal) || string.Equals(trimmed, "Q", StringComparison.Ordinal))
        {
            editor.Quit(force: true);
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        throw new NotSupportedException($"Unsupported command '{commandText}'.");
    }

    private static EdCommandResult HandleSearchCommand(
        EdEditor editor,
        EdSearchDirection searchDirection,
        string searchPattern)
    {
        var pattern = EdSearchEngine.ResolveSearchPattern(
            searchPattern,
            editor.LastSearchPatternInternal);

        if (searchDirection == EdSearchDirection.Backward
            && editor.CurrentLineNumberInternal >= 1
            && editor.CurrentLineNumberInternal <= editor.BufferLines.Count
            && EdSearchEngine.IsPatternMatch(editor.RegexEngine, editor.BufferLines[editor.CurrentLineNumberInternal - 1], pattern))
        {
            editor.LastSearchPatternInternal = pattern;
            var currentLineOutput = editor.Print(new EdLineRange(editor.CurrentLineNumberInternal, editor.CurrentLineNumberInternal));
            return editor.CreateCommandResult(bufferChanged: false, currentLineOutput);
        }

        var lineNumber = editor.Search(pattern, searchDirection);
        var output = editor.Print(new EdLineRange(lineNumber, lineNumber));
        return editor.CreateCommandResult(bufferChanged: false, output);
    }

    private static EdCommandResult HandleEmptyCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range)
    {
        if (parsedCommand.HasAddress)
        {
            var output = editor.Print(range);
            return editor.CreateCommandResult(bufferChanged: false, output);
        }
        else
        {
            var output = editor.Print(EdBufferUtilities.ResolveNextLineRange(editor.LineCount, editor.CurrentLineNumberInternal));
            return editor.CreateCommandResult(bufferChanged: false, output);
        }
    }

    private static EdCommandResult HandleTextInputCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var lines = EdEditorTextUtilities.ParseTextInputLines(command);

        if (command[0] == 'a')
        {
            int? afterLine;

            if (parsedCommand.HasAddress && range.HasValue)
            {
                afterLine = range.Value.EndLine;
            }
            else
            {
                afterLine = editor.CurrentLineNumberInternal;
            }

            editor.Append(afterLine, lines);
        }
        else if (command[0] == 'i')
        {
            int beforeLine;

            if (parsedCommand.HasAddress && range.HasValue)
            {
                beforeLine = range.Value.StartLine;
            }
            else if (editor.CurrentLineNumberInternal == 0)
            {
                beforeLine = 1;
            }
            else
            {
                beforeLine = editor.CurrentLineNumberInternal;
            }

            editor.Insert(beforeLine, lines);
        }
        else
        {
            EdLineRange changeRange;

            if (parsedCommand.HasAddress && range.HasValue)
            {
                changeRange = range.Value;
            }
            else
            {
                changeRange = EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal);
            }

            editor.Change(changeRange, lines);
        }

        return editor.CreateCommandResult(bufferChanged: true, []);
    }

    private static EdCommandResult HandleSubstituteCommand(
        EdEditor editor,
        string commandText,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var substituteBody = command[1..];

        if (!EdCommandTextParser.TryParseDelimitedArguments(substituteBody, out var pattern, out var replacement, out var flags))
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        string effectivePattern;
        bool usePreviousPattern;

        if (string.IsNullOrEmpty(pattern) && parsedCommand.UsedSearchAddress && !string.IsNullOrEmpty(editor.LastSearchPatternInternal))
        {
            effectivePattern = editor.LastSearchPatternInternal;
            usePreviousPattern = false;
        }
        else
        {
            effectivePattern = pattern;
            usePreviousPattern = string.IsNullOrEmpty(pattern);
        }

        var options = new EdSubstitutionOptions(
            ReplaceAllOnLine: flags.Contains('g', StringComparison.Ordinal),
            Occurrence: EdEditorTextUtilities.ParseOccurrence(flags),
            UsePreviousPattern: usePreviousPattern);

        EdLineRange? effectiveRange;

        if (parsedCommand.HasAddress)
        {
            effectiveRange = range;
        }
        else
        {
            effectiveRange = EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal);
        }

        editor.Substitute(
            effectiveRange,
            effectivePattern,
            replacement,
            options);

        return editor.CreateCommandResult(bufferChanged: true, []);
    }

    private static EdCommandResult HandlePrintCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        EdPrintMode mode;

        if (command[0] == 'l')
        {
            mode = EdPrintMode.Literal;
        }
        else if (command[0] == 'n')
        {
            mode = EdPrintMode.Numbered;
        }
        else
        {
            mode = EdPrintMode.Normal;
        }

        EdLineRange? effectiveRange;

        if (parsedCommand.HasAddress)
        {
            effectiveRange = range;
        }
        else
        {
            effectiveRange = EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal);
        }

        var output = editor.Print(effectiveRange, mode);
        return editor.CreateCommandResult(bufferChanged: false, output);
    }

    private static EdLineRange ResolveDeleteRange(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range)
    {
        if (parsedCommand.HasAddress)
        {
            if (range.HasValue)
            {
                return range.Value;
            }
            else
            {
                return EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal);
            }
        }
        else
        {
            return EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal);
        }
    }

    private static EdLineRange ResolveJoinRange(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range)
    {
        if (parsedCommand.HasAddress && range.HasValue)
        {
            return range.Value;
        }
        else
        {
            return EdBufferUtilities.ResolveJoinRange(editor.LineCount, editor.CurrentLineNumberInternal);
        }
    }

    private static EdCommandResult HandleMoveCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var destinationLine = EdAddressParser.ParseDestinationAddress(
            command[1..],
            editor.CurrentLineNumberInternal,
            editor.LineCount,
            editor.ResolveMark,
            editor.FindSearchLine);
        var moveRange = ResolveDeleteRange(editor, parsedCommand, range);
        editor.Move(moveRange, destinationLine);
        return editor.CreateCommandResult(bufferChanged: true, []);
    }

    private static EdCommandResult HandleCopyCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var destinationLine = EdAddressParser.ParseDestinationAddress(
            command[1..],
            editor.CurrentLineNumberInternal,
            editor.LineCount,
            editor.ResolveMark,
            editor.FindSearchLine);
        var copyRange = ResolveDeleteRange(editor, parsedCommand, range);
        editor.Copy(copyRange, destinationLine);
        return editor.CreateCommandResult(bufferChanged: true, []);
    }

    private static EdCommandResult HandleMarkCommand(
        EdEditor editor,
        string commandText,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var argument = command[1..].TrimStart();

        if (argument.Length == 0)
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        int line;

        if (parsedCommand.HasAddress && range.HasValue)
        {
            line = range.Value.EndLine;
        }
        else
        {
            line = EdBufferUtilities.ResolveCurrentLineRange(editor.CurrentLineNumberInternal).EndLine;
        }

        editor.SetMark(argument[0], line);
        return editor.CreateCommandResult(bufferChanged: false, []);
    }

    private static EdCommandResult HandleLineNumberCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range)
    {
        int lineNumber;

        if (parsedCommand.HasAddress && range.HasValue)
        {
            lineNumber = range.Value.EndLine;
        }
        else
        {
            lineNumber = editor.CurrentLineNumberInternal;
        }

        return editor.CreateCommandResult(
            bufferChanged: false,
            [lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static EdCommandResult HandleFileCommand(
        EdEditor editor,
        string command)
    {
        var argument = string.Empty;

        if (command.Length > 1)
        {
            argument = command[1..].TrimStart();
        }

        if (!string.IsNullOrWhiteSpace(argument))
        {
            editor.SetFileName(argument);
        }

        return editor.CreateCommandResult(
            bufferChanged: false,
            [editor.CurrentFileDisplayPathOrCurrentPath]);
    }

    private static EdCommandResult HandleWriteCommand(
        EdEditor editor,
        EdLineRange? range,
        string command)
    {
        var argument = string.Empty;

        if (command.Length > 1)
        {
            argument = command[1..].TrimStart();
        }

        if (argument.StartsWith('!'))
        {
            editor.WriteToCommand(argument[1..].TrimStart(), range);
            return editor.CreateCommandResult(bufferChanged: false, []);
        }

        string? path;

        if (string.IsNullOrWhiteSpace(argument))
        {
            path = null;
        }
        else
        {
            path = argument;
        }

        EdWriteMode mode;

        if (command[0] == 'W')
        {
            mode = EdWriteMode.Append;
        }
        else
        {
            mode = EdWriteMode.Replace;
        }

        editor.Write(path, range, mode);
        return editor.CreateCommandResult(bufferChanged: false, []);
    }

    private static EdCommandResult HandleScrollCommand(
        EdEditor editor,
        ParsedCommand parsedCommand,
        EdLineRange? range,
        string command)
    {
        var argument = string.Empty;

        if (command.Length > 1)
        {
            argument = command[1..].Trim();
        }

        int? lineCount;

        if (string.IsNullOrEmpty(argument))
        {
            lineCount = null;
        }
        else
        {
            lineCount = int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture);
        }

        int? startLine;

        if (parsedCommand.HasAddress && range.HasValue)
        {
            startLine = range.Value.EndLine;
        }
        else
        {
            startLine = null;
        }

        var output = editor.Scroll(startLine, lineCount);
        return editor.CreateCommandResult(bufferChanged: false, output);
    }

    private static EdCommandResult HandleReadCommand(
        EdEditor editor,
        EdLineRange? range,
        string command)
    {
        var argument = string.Empty;

        if (command.Length > 1)
        {
            argument = command[1..].TrimStart();
        }

        int? address;

        if (range.HasValue)
        {
            address = range.Value.EndLine;
        }
        else
        {
            address = null;
        }

        if (argument.StartsWith('!'))
        {
            editor.ReadCommandOutput(argument[1..].TrimStart(), address);
        }
        else
        {
            editor.Read(argument, address);
        }

        return editor.CreateCommandResult(bufferChanged: true, []);
    }

    private static EdCommandResult HandleEditCommand(
        EdEditor editor,
        string command)
    {
        string? argument;

        if (command.Length > 1)
        {
            argument = command[1..].TrimStart();
        }
        else
        {
            argument = null;
        }

        string? path;

        if (string.IsNullOrWhiteSpace(argument))
        {
            path = null;
        }
        else
        {
            path = argument;
        }

        var force = false;

        if (command[0] == 'E')
        {
            force = true;
        }

        editor.Edit(path, force);
        return editor.CreateCommandResult(bufferChanged: true, []);
    }
}
