namespace Ed;

public sealed class EdEditor
{
    private readonly IEdFileSystem _fileSystem;
    private readonly IEdShell _shell;

    private List<string> _lines = [];
    private Dictionary<char, int> _marks = new();
    private EditorSnapshot? _undoSnapshot;
    private string? _currentFilePath;
    private string? _currentFileDisplayPath;
    private string? _lastErrorMessage;
    private string? _lastSearchPattern;
    private string? _lastSubstitutionPattern;
    private int _lastSubstitutionLineNumber;
    private int _lastSubstitutionNextSearchIndex;
    private int _currentLineNumber;
    private bool _isModified;
    private bool _isPromptEnabled;
    private bool _isVerboseErrorsEnabled;
    private bool _isClosed;
    private int _defaultWindowSize = 22;

    public EdEditor(
        IEdFileSystem fileSystem,
        IEdShell shell)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
    }

    public int CurrentLineNumber
    {
        get => _currentLineNumber;
    }

    public int LineCount
    {
        get => _lines.Count;
    }

    public bool IsModified
    {
        get => _isModified;
    }

    public bool IsPromptEnabled
    {
        get => _isPromptEnabled;
        set => _isPromptEnabled = value;
    }

    public bool IsVerboseErrorsEnabled
    {
        get => _isVerboseErrorsEnabled;
        set => _isVerboseErrorsEnabled = value;
    }

    public int DefaultWindowSize
    {
        get => _defaultWindowSize;
        set => _defaultWindowSize = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public string? LastErrorMessage
    {
        get => _lastErrorMessage;
    }

    public void CreateBuffer()
    {
        EnsureOpen();
        _lines.Clear();
        _marks.Clear();
        _undoSnapshot = null;
        _currentFilePath = null;
        _currentFileDisplayPath = null;
        _currentLineNumber = 0;
        _lastSubstitutionLineNumber = 0;
        _lastSubstitutionNextSearchIndex = 0;
        _isModified = false;
        _lastErrorMessage = null;
    }

    public void Edit(string? path, bool force = false)
    {
        EnsureOpen();

        if (_isModified && !force)
        {
            throw new InvalidOperationException("Buffer has been modified.");
        }

        var resolvedPath = ResolvePath(path);
        SaveUndoState();
        _lines = _fileSystem.ReadAllLines(resolvedPath).ToList();
        _marks.Clear();
        _currentFilePath = resolvedPath;
        _currentFileDisplayPath = string.IsNullOrWhiteSpace(path) ? _currentFileDisplayPath ?? resolvedPath : path;
        _currentLineNumber = _lines.Count;
        _isModified = false;
        _lastErrorMessage = null;
    }

    public void SetFileName(string path)
    {
        EnsureOpen();
        _currentFilePath = NormalizePath(path);
        _currentFileDisplayPath = path;
        _lastErrorMessage = null;
    }

    public void Read(string path, int? afterLine = null)
    {
        EnsureOpen();
        var resolvedPath = NormalizePath(path);
        var lines = _fileSystem.ReadAllLines(resolvedPath);
        InsertLines(ResolveAfterLine(afterLine), lines, clearMarks: false);
    }

    public void ReadCommandOutput(string commandText, int? afterLine = null)
    {
        EnsureOpen();
        var lines = _shell.ReadCommandOutput(commandText);
        InsertLines(ResolveAfterLine(afterLine), lines, clearMarks: false);
    }

    public void Write(string? path = null, EdLineRange? range = null, EdWriteMode mode = EdWriteMode.Replace)
    {
        EnsureOpen();
        var resolvedPath = path is null ? ResolvePath(path) : NormalizePath(path);
        var linesToWrite = GetLinesInRange(range);

        if (mode == EdWriteMode.Append)
        {
            _fileSystem.AppendAllLines(resolvedPath, linesToWrite);
        }
        else
        {
            _fileSystem.WriteAllLines(resolvedPath, linesToWrite);
        }

        _currentFilePath = resolvedPath;
        _currentFileDisplayPath = path ?? _currentFileDisplayPath ?? resolvedPath;

        if (mode == EdWriteMode.Replace && IsWholeBuffer(range))
        {
            _isModified = false;
        }

        _lastErrorMessage = null;
    }

    public void WriteToCommand(string commandText, EdLineRange? range = null)
    {
        EnsureOpen();
        _shell.WriteToCommand(commandText, GetLinesInRange(range));
    }

    public void Append(int? afterLine, IReadOnlyList<string> lines)
    {
        EnsureOpen();
        InsertLines(ResolveAfterLine(afterLine), lines, clearMarks: true);
    }

    public void Insert(int? beforeLine, IReadOnlyList<string> lines)
    {
        EnsureOpen();
        var targetLine = beforeLine ?? (_currentLineNumber == 0 ? 1 : _currentLineNumber);

        if (targetLine < 1 || targetLine > _lines.Count + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(beforeLine));
        }

        InsertLines(targetLine - 1, lines, clearMarks: true);
    }

    public void Change(EdLineRange range, IReadOnlyList<string> lines)
    {
        EnsureOpen();
        ValidateRange(range);
        SaveUndoState();
        _lines.RemoveRange(range.StartLine - 1, range.EndLine - range.StartLine + 1);
        _lines.InsertRange(range.StartLine - 1, lines);
        _marks.Clear();
        _currentLineNumber = lines.Count > 0 ? range.StartLine + lines.Count - 1 : DetermineCurrentLineAfterDeletion(range.StartLine);
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void Delete(EdLineRange range)
    {
        EnsureOpen();
        ValidateRange(range);
        SaveUndoState();
        _lines.RemoveRange(range.StartLine - 1, range.EndLine - range.StartLine + 1);
        _marks.Clear();
        _currentLineNumber = DetermineCurrentLineAfterDeletion(range.StartLine);
        _isModified = true;
        _lastErrorMessage = null;
    }

    public IReadOnlyList<string> Print(EdLineRange? range = null, EdPrintMode mode = EdPrintMode.Normal)
    {
        EnsureOpen();
        var resolvedRange = ResolveRange(range);

        if (resolvedRange is null)
        {
            return [];
        }

        var output = new List<string>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            output.Add(FormatLine(_lines[lineNumber - 1], lineNumber, mode));
        }

        _currentLineNumber = output.Count > 0 ? resolvedRange.Value.EndLine : _currentLineNumber;
        return output;
    }

    public IReadOnlyList<string> Scroll(int? startLine = null, int? lineCount = null, EdPrintMode mode = EdPrintMode.Normal)
    {
        EnsureOpen();

        if (_lines.Count == 0)
        {
            return [];
        }

        var firstLine = startLine ?? (_currentLineNumber == 0 ? 1 : _currentLineNumber);

        if (firstLine < 1 || firstLine > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine));
        }

        var windowSize = lineCount ?? _defaultWindowSize;

        if (windowSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineCount));
        }

        var lastLine = Math.Min(_lines.Count, firstLine + windowSize - 1);
        return Print(new EdLineRange(firstLine, lastLine), mode);
    }

    public int GetAddress(int? line = null) => line ?? _currentLineNumber;

    public void Join(EdLineRange range)
    {
        EnsureOpen();
        ValidateRange(range);
        SaveUndoState();
        var mergedLine = string.Concat(_lines.Skip(range.StartLine - 1).Take(range.EndLine - range.StartLine + 1));
        _lines.RemoveRange(range.StartLine - 1, range.EndLine - range.StartLine + 1);
        _lines.Insert(range.StartLine - 1, mergedLine);
        _marks.Clear();
        _currentLineNumber = range.StartLine;
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void Move(EdLineRange range, int destinationLine)
    {
        EnsureOpen();
        ValidateRange(range);
        ValidateDestination(destinationLine);

        if (destinationLine >= range.StartLine && destinationLine <= range.EndLine)
        {
            throw new InvalidOperationException("Destination cannot be inside the moved range.");
        }

        SaveUndoState();
        var block = _lines.Skip(range.StartLine - 1).Take(range.EndLine - range.StartLine + 1).ToArray();
        _lines.RemoveRange(range.StartLine - 1, block.Length);

        var adjustedDestination = destinationLine > range.EndLine ? destinationLine - block.Length : destinationLine;
        _lines.InsertRange(adjustedDestination, block);
        _marks.Clear();
        _currentLineNumber = adjustedDestination + block.Length;
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void Copy(EdLineRange range, int destinationLine)
    {
        EnsureOpen();
        ValidateRange(range);
        ValidateDestination(destinationLine);
        SaveUndoState();
        var block = _lines.Skip(range.StartLine - 1).Take(range.EndLine - range.StartLine + 1).ToArray();
        _lines.InsertRange(destinationLine, block);
        _marks.Clear();
        _currentLineNumber = destinationLine + block.Length;
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void SetMark(char markName, int line)
    {
        EnsureOpen();

        if (line < 1 || line > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        _marks[markName] = line;
    }

    public int ResolveMark(char markName)
    {
        EnsureOpen();
        return _marks.TryGetValue(markName, out var line) ? line : throw new KeyNotFoundException($"Mark '{markName}' is not set.");
    }

    public int Search(string pattern, EdSearchDirection direction, int? startLine = null)
    {
        EnsureOpen();

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        var searchStart = startLine ?? (_currentLineNumber == 0 ? 1 : _currentLineNumber);

        if (searchStart < 1 || searchStart > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine));
        }

        var actualPattern = ResolveSearchPattern(pattern);
        _lastSearchPattern = actualPattern;
        var matcher = CreateSearchMatcher(new EdLineRange(1, _lines.Count), actualPattern);

        for (var offset = 1; offset <= _lines.Count; offset++)
        {
            var lineNumber = direction == EdSearchDirection.Forward
                ? ((searchStart - 1 + offset) % _lines.Count) + 1
                : ((searchStart - 1 - offset + (_lines.Count * 2)) % _lines.Count) + 1;

            if (matcher(_lines[lineNumber - 1]))
            {
                _currentLineNumber = lineNumber;
                return lineNumber;
            }
        }

        throw new InvalidOperationException("Pattern not found.");
    }

    public void Substitute(EdLineRange? range, string pattern, string replacement, EdSubstitutionOptions? options = null)
    {
        EnsureOpen();
        options ??= new EdSubstitutionOptions();
        var actualPattern = options.Value.UsePreviousPattern && string.IsNullOrEmpty(pattern)
            ? _lastSubstitutionPattern ?? throw new InvalidOperationException("No previous substitution pattern is available.")
            : pattern;
        var resolvedRange = ResolveRangeForMutation(range);

        if (resolvedRange is null)
        {
            return;
        }

        SaveUndoState();
        var changed = false;
        var lastChangedLine = _currentLineNumber;
        var lastNextSearchIndex = 0;

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            var startIndex = options.Value.UsePreviousPattern && string.IsNullOrEmpty(pattern) && _lastSubstitutionLineNumber == lineNumber
                ? _lastSubstitutionNextSearchIndex
                : 0;
            var updatedLine = ApplySubstitution(_lines[lineNumber - 1], actualPattern, replacement, options.Value, startIndex, out var replaced, out var nextSearchIndex);

            if (!string.Equals(updatedLine, _lines[lineNumber - 1], StringComparison.Ordinal))
            {
                _lines[lineNumber - 1] = updatedLine;
                changed = true;
                lastChangedLine = lineNumber;
                lastNextSearchIndex = replaced ? nextSearchIndex : 0;
            }
        }

        if (changed)
        {
            _marks.Clear();
            _currentLineNumber = lastChangedLine;
            _lastSubstitutionLineNumber = lastChangedLine;
            _lastSubstitutionNextSearchIndex = lastNextSearchIndex;
            _isModified = true;
        }

        _lastSubstitutionPattern = actualPattern;
        _lastErrorMessage = null;
    }

    public void Global(EdLineRange? range, string pattern, string commandList, EdGlobalMode mode = EdGlobalMode.Match)
    {
        EnsureOpen();
        var resolvedRange = ResolveRange(range);

        if (resolvedRange is null)
        {
            return;
        }

        var matcher = CreateSearchMatcher(resolvedRange.Value, pattern);
        var matchingLines = new List<int>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            if (matcher(_lines[lineNumber - 1]))
            {
                matchingLines.Add(lineNumber);
            }
        }

        var command = commandList.Trim();
        var linesToProcess = mode == EdGlobalMode.NonMatch
            ? Enumerable.Range(resolvedRange.Value.StartLine, resolvedRange.Value.EndLine - resolvedRange.Value.StartLine + 1)
                .Except(matchingLines)
                .ToArray()
            : matchingLines.ToArray();

        if (!string.Equals(command, "d", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Only delete is supported by the current global implementation.");
        }

        if (linesToProcess.Length == 0)
        {
            return;
        }

        SaveUndoState();

        foreach (var lineNumber in linesToProcess.OrderByDescending(static value => value))
        {
            _lines.RemoveAt(lineNumber - 1);
        }

        _marks.Clear();
        _currentLineNumber = _lines.Count == 0 ? 0 : Math.Min(linesToProcess[0], _lines.Count);
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void ExecuteShellCommand(string commandText)
    {
        EnsureOpen();
        _shell.Execute(commandText);
    }

    public EdCommandResult ExecuteCommand(string commandText)
    {
        EnsureOpen();

        try
        {
            return ExecuteCommandCore(commandText);
        }
        catch (Exception ex) when (ShouldCaptureError(ex))
        {
            _lastErrorMessage = ex.Message;
            throw;
        }
    }

    private EdCommandResult ExecuteCommandCore(string commandText)
    {
        var trimmed = commandText.Trim();

        if (string.Equals(trimmed, ",p", StringComparison.Ordinal))
        {
            var output = Print();
            return CreateCommandResult(bufferChanged: false, output);
        }

        if (trimmed.StartsWith('!'))
        {
            ExecuteShellCommand(trimmed[1..].TrimStart());
            return CreateCommandResult(bufferChanged: false, []);
        }

        if (IsSearchCommand(trimmed))
        {
            var pattern = ResolveSearchPattern(trimmed[1..^1]);
            EdSearchDirection direction;

            if (trimmed[0] == '/')
            {
                direction = EdSearchDirection.Forward;
            }
            else
            {
                direction = EdSearchDirection.Backward;
            }

            if (direction == EdSearchDirection.Backward
                && _currentLineNumber >= 1
                && _currentLineNumber <= _lines.Count
                && IsPatternMatch(_lines[_currentLineNumber - 1], pattern))
            {
                _lastSearchPattern = pattern;
                var currentLineOutput = Print(new EdLineRange(_currentLineNumber, _currentLineNumber));
                return CreateCommandResult(bufferChanged: false, currentLineOutput);
            }

            var lineNumber = Search(pattern, direction);
            var output = Print(new EdLineRange(lineNumber, lineNumber));
            return CreateCommandResult(bufferChanged: false, output);
        }

        if (TryParseGlobalCommand(trimmed, out var globalMode, out var globalPattern, out var globalCommandList))
        {
            if (string.Equals(globalCommandList, "d", StringComparison.Ordinal))
            {
                Global(null, globalPattern, globalCommandList, globalMode);
                return CreateCommandResult(bufferChanged: true, []);
            }
            else if (string.Equals(globalCommandList, "p", StringComparison.Ordinal))
            {
                var output = PrintGlobalMatches(null, globalPattern, globalMode, EdPrintMode.Normal);
                return CreateCommandResult(bufferChanged: false, output);
            }
            else if (string.Equals(globalCommandList, "n", StringComparison.Ordinal))
            {
                var output = PrintGlobalMatches(null, globalPattern, globalMode, EdPrintMode.Numbered);
                return CreateCommandResult(bufferChanged: false, output);
            }
            else if (string.Equals(globalCommandList, "l", StringComparison.Ordinal))
            {
                var output = PrintGlobalMatches(null, globalPattern, globalMode, EdPrintMode.Literal);
                return CreateCommandResult(bufferChanged: false, output);
            }
            else if (globalCommandList.StartsWith('s'))
            {
                ExecuteGlobalSubstitute(null, globalPattern, globalCommandList, globalMode);
                return CreateCommandResult(bufferChanged: true, []);
            }
            else
            {
                throw new NotSupportedException("Only `d`, `p`, `n`, `l`, and `s` are supported by the current global implementation.");
            }
        }

        var parsedCommand = ParseCommand(commandText);
        var range = parsedCommand.Range;
        var command = parsedCommand.CommandText;

        if (string.IsNullOrEmpty(command))
        {
            if (parsedCommand.HasAddress)
            {
                var output = Print(range);
                return CreateCommandResult(bufferChanged: false, output);
            }
            else
            {
                var output = Print(ResolveNextLineRange());
                return CreateCommandResult(bufferChanged: false, output);
            }
        }

        if (command[0] == 'a' || command[0] == 'i' || command[0] == 'c')
        {
            var lines = ParseTextInputLines(command);

            if (command[0] == 'a')
            {
                var afterLine = parsedCommand.HasAddress && range.HasValue ? range.Value.EndLine : _currentLineNumber;
                Append(afterLine, lines);
            }
            else if (command[0] == 'i')
            {
                int beforeLine;

                if (parsedCommand.HasAddress && range.HasValue)
                {
                    beforeLine = range.Value.StartLine;
                }
                else if (_currentLineNumber == 0)
                {
                    beforeLine = 1;
                }
                else
                {
                    beforeLine = _currentLineNumber;
                }

                Insert(beforeLine, lines);
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
                    changeRange = ResolveCurrentLineRange();
                }

                Change(changeRange, lines);
            }

            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 's')
        {
            var substituteBody = command[1..];

            if (!TryParseDelimitedArguments(substituteBody, '/', out var pattern, out var replacement, out var flags))
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            var options = new EdSubstitutionOptions(
                ReplaceAllOnLine: flags.Contains('g', StringComparison.Ordinal),
                Occurrence: ParseOccurrence(flags),
                UsePreviousPattern: string.IsNullOrEmpty(pattern));

            Substitute(
                parsedCommand.HasAddress ? range : ResolveCurrentLineRange(),
                pattern,
                replacement,
                options);

            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'p' || command[0] == 'l' || command[0] == 'n')
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

            var output = Print(parsedCommand.HasAddress ? range : ResolveCurrentLineRange(), mode);
            return CreateCommandResult(bufferChanged: false, output);
        }

        if (command[0] == 'd')
        {
            Delete(parsedCommand.HasAddress ? range ?? ResolveCurrentLineRange() : ResolveCurrentLineRange());
            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'j')
        {
            EdLineRange joinRange;

            if (parsedCommand.HasAddress && range.HasValue)
            {
                joinRange = range.Value;
            }
            else
            {
                joinRange = ResolveJoinRange();
            }

            Join(joinRange);
            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'm')
        {
            var destinationLine = ParseDestinationAddress(command[1..]);
            Move(parsedCommand.HasAddress ? range ?? ResolveCurrentLineRange() : ResolveCurrentLineRange(), destinationLine);
            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 't')
        {
            var destinationLine = ParseDestinationAddress(command[1..]);
            Copy(parsedCommand.HasAddress ? range ?? ResolveCurrentLineRange() : ResolveCurrentLineRange(), destinationLine);
            return CreateCommandResult(bufferChanged: true, []);
        }

        if (command[0] == 'k')
        {
            var argument = command[1..].TrimStart();

            if (argument.Length == 0)
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            var line = parsedCommand.HasAddress && range.HasValue ? range.Value.EndLine : ResolveCurrentLineRange().EndLine;
            SetMark(argument[0], line);
            return CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'u')
        {
            var hadUndoSnapshot = _undoSnapshot is not null;
            Undo();
            return CreateCommandResult(bufferChanged: hadUndoSnapshot, []);
        }

        if (command[0] == '=')
        {
            var lineNumber = parsedCommand.HasAddress && range.HasValue
                ? range.Value.EndLine
                : _currentLineNumber;
            return CreateCommandResult(bufferChanged: false, [lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        if (command[0] == 'f')
        {
            return CreateCommandResult(bufferChanged: false, [_currentFileDisplayPath ?? _currentFilePath ?? string.Empty]);
        }

        if (command[0] == 'P')
        {
            _isPromptEnabled = !_isPromptEnabled;
            return CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'H')
        {
            _isVerboseErrorsEnabled = !_isVerboseErrorsEnabled;
            return CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'h')
        {
            if (string.IsNullOrEmpty(_lastErrorMessage))
            {
                return CreateCommandResult(bufferChanged: false, []);
            }
            else
            {
                return CreateCommandResult(bufferChanged: false, [_lastErrorMessage]);
            }
        }

        if (command[0] == 'w' || command[0] == 'W')
        {
            var argument = command.Length > 1 ? command[1..].TrimStart() : string.Empty;

            if (argument.StartsWith('!'))
            {
                WriteToCommand(argument[1..].TrimStart(), range);
                return CreateCommandResult(bufferChanged: false, []);
            }

            Write(
                string.IsNullOrWhiteSpace(argument) ? null : argument,
                range,
                command[0] == 'W' ? EdWriteMode.Append : EdWriteMode.Replace);

            return CreateCommandResult(bufferChanged: false, []);
        }

        if (command[0] == 'z')
        {
            var argument = command.Length > 1 ? command[1..].Trim() : string.Empty;
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

            var output = Scroll(startLine, lineCount);
            return CreateCommandResult(bufferChanged: false, output);
        }

        if (command[0] == 'r')
        {
            var argument = command.Length > 1 ? command[1..].TrimStart() : string.Empty;
            var address = range?.EndLine;

            if (argument.StartsWith('!'))
            {
                ReadCommandOutput(argument[1..].TrimStart(), address);
            }
            else
            {
                Read(argument, address);
            }

            return CreateCommandResult(bufferChanged: true, Print());
        }

        if (command[0] == 'e' || command[0] == 'E')
        {
            var argument = command.Length > 1 ? command[1..].TrimStart() : null;
            Edit(string.IsNullOrWhiteSpace(argument) ? null : argument, force: command[0] == 'E');
            return CreateCommandResult(bufferChanged: true, []);
        }

        if (string.Equals(trimmed, "q", StringComparison.Ordinal))
        {
            Quit();
            return CreateCommandResult(bufferChanged: false, []);
        }
        else if (string.Equals(trimmed, "q!", StringComparison.Ordinal) || string.Equals(trimmed, "Q", StringComparison.Ordinal))
        {
            Quit(force: true);
            return CreateCommandResult(bufferChanged: false, []);
        }

        throw new NotSupportedException($"Unsupported command '{commandText}'.");
    }

    private static bool ShouldCaptureError(Exception exception)
    {
        return exception is InvalidOperationException
            || exception is NotSupportedException
            || exception is ArgumentOutOfRangeException
            || exception is KeyNotFoundException;
    }

    private static IReadOnlyList<string> ParseTextInputLines(string command)
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

    private EdLineRange ResolveNextLineRange()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        if (_currentLineNumber == 0)
        {
            return new EdLineRange(1, 1);
        }
        else if (_currentLineNumber >= _lines.Count)
        {
            return new EdLineRange(_lines.Count, _lines.Count);
        }
        else
        {
            return new EdLineRange(_currentLineNumber + 1, _currentLineNumber + 1);
        }
    }

    private EdLineRange ResolveJoinRange()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        var startLine = _currentLineNumber == 0 ? 1 : _currentLineNumber;
        var endLine = Math.Min(startLine + 1, _lines.Count);
        return new EdLineRange(startLine, endLine);
    }

    private int ParseDestinationAddress(string addressText)
    {
        var trimmed = addressText.Trim();

        if (string.Equals(trimmed, "0", StringComparison.Ordinal))
        {
            return 0;
        }

        var index = 0;
        var destination = ParseAddress(trimmed, ref index, _currentLineNumber);

        if (index != trimmed.Length)
        {
            throw new NotSupportedException($"Unsupported command '{addressText}'.");
        }

        return destination;
    }

    private void ExecuteGlobalSubstitute(EdLineRange? range, string pattern, string commandList, EdGlobalMode mode)
    {
        var resolvedRange = ResolveRange(range);

        if (resolvedRange is null)
        {
            return;
        }

        var matcher = CreateSearchMatcher(resolvedRange.Value, pattern);
        var matchingLines = new List<int>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            if (matcher(_lines[lineNumber - 1]))
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

        if (!TryParseDelimitedArguments(substituteBody, '/', out var substitutePattern, out var replacement, out var flags))
        {
            throw new NotSupportedException($"Unsupported command '{commandList}'.");
        }

        var options = new EdSubstitutionOptions(
            ReplaceAllOnLine: flags.Contains('g', StringComparison.Ordinal),
            Occurrence: ParseOccurrence(flags),
            UsePreviousPattern: string.IsNullOrEmpty(substitutePattern));
        var actualPattern = options.UsePreviousPattern
            ? _lastSubstitutionPattern ?? throw new InvalidOperationException("No previous substitution pattern is available.")
            : substitutePattern;

        SaveUndoState();
        var changed = false;
        var lastChangedLine = _currentLineNumber;
        var lastNextSearchIndex = 0;

        foreach (var lineNumber in linesToProcess)
        {
            var updatedLine = ApplySubstitution(_lines[lineNumber - 1], actualPattern, replacement, options, 0, out var replaced, out var nextSearchIndex);

            if (!string.Equals(updatedLine, _lines[lineNumber - 1], StringComparison.Ordinal))
            {
                _lines[lineNumber - 1] = updatedLine;
                changed = true;
                lastChangedLine = lineNumber;
                lastNextSearchIndex = replaced ? nextSearchIndex : 0;
            }
        }

        if (changed)
        {
            _marks.Clear();
            _currentLineNumber = lastChangedLine;
            _lastSubstitutionLineNumber = lastChangedLine;
            _lastSubstitutionNextSearchIndex = lastNextSearchIndex;
            _isModified = true;
        }

        _lastSubstitutionPattern = actualPattern;
        _lastErrorMessage = null;
    }

    public void Undo()
    {
        EnsureOpen();

        if (_undoSnapshot is null)
        {
            return;
        }

        RestoreSnapshot(_undoSnapshot);
        _undoSnapshot = null;
    }

    public void Quit(bool force = false)
    {
        EnsureOpen();

        if (_isModified && !force)
        {
            throw new InvalidOperationException("Buffer has been modified.");
        }

        _isClosed = true;
    }

    private static string FormatLine(string line, int lineNumber, EdPrintMode mode)
    {
        return mode switch
        {
            EdPrintMode.Numbered => $"{lineNumber}\t{line}",
            EdPrintMode.Literal => line.Replace("\t", "\\t", StringComparison.Ordinal) + "$",
            _ => line,
        };
    }

    private static int ParseOccurrence(string flags)
    {
        var digits = new string(flags.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var occurrence) && occurrence > 0 ? occurrence : 1;
    }

    private bool IsSearchCommand(string commandText)
    {
        return commandText.Length >= 2
            && (commandText[0] == '/' || commandText[0] == '?')
            && commandText[^1] == commandText[0];
    }

    private bool TryParseGlobalCommand(string commandText, out EdGlobalMode mode, out string pattern, out string commandList)
    {
        mode = EdGlobalMode.Match;
        pattern = string.Empty;
        commandList = string.Empty;

        if (commandText.Length < 4 || (commandText[0] != 'g' && commandText[0] != 'v') || commandText[1] != '/')
        {
            return false;
        }

        var separatorIndex = commandText.IndexOf('/', 2);

        if (separatorIndex < 0 || separatorIndex == commandText.Length - 1)
        {
            return false;
        }

        mode = commandText[0] == 'v' ? EdGlobalMode.NonMatch : EdGlobalMode.Match;
        pattern = commandText[2..separatorIndex];
        commandList = commandText[(separatorIndex + 1)..].Trim();
        return true;
    }

    private ParsedCommand ParseCommand(string commandText)
    {
        var index = 0;

        while (index < commandText.Length && char.IsWhiteSpace(commandText[index]))
        {
            index++;
        }

        if (index >= commandText.Length)
        {
            return new ParsedCommand(null, false, string.Empty);
        }

        if (commandText[index] == '%')
        {
            index++;
            var lastLine = _lines.Count == 0 ? 1 : _lines.Count;
            return new ParsedCommand(new EdLineRange(1, lastLine), true, commandText[index..].TrimStart());
        }

        if (!IsAddressStart(commandText[index]))
        {
            return new ParsedCommand(null, false, commandText.TrimStart());
        }

        var originalCurrentLine = _currentLineNumber;
        var firstAddress = ParseAddress(commandText, ref index, originalCurrentLine);
        var range = new EdLineRange(firstAddress, firstAddress);

        if (index < commandText.Length && (commandText[index] == ',' || commandText[index] == ';'))
        {
            var separator = commandText[index];
            index++;
            var secondAddress = ParseAddress(commandText, ref index, separator == ';' ? firstAddress : originalCurrentLine);
            range = new EdLineRange(firstAddress, secondAddress);
        }

        return new ParsedCommand(range, true, commandText[index..].TrimStart());
    }

    private static bool IsAddressStart(char value)
    {
        return char.IsDigit(value)
            || value == '.'
            || value == '$'
            || value == '\''
            || value == '/'
            || value == '?'
            || value == '+'
            || value == '-';
    }

    private int ParseAddress(string commandText, ref int index, int currentLine)
    {
        if (index >= commandText.Length)
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        int address;

        if (char.IsDigit(commandText[index]))
        {
            var start = index;

            while (index < commandText.Length && char.IsDigit(commandText[index]))
            {
                index++;
            }

            address = int.Parse(commandText[start..index], System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (commandText[index] == '.')
        {
            index++;
            address = currentLine == 0 ? 1 : currentLine;
        }
        else if (commandText[index] == '$')
        {
            index++;
            address = _lines.Count;
        }
        else if (commandText[index] == '\'')
        {
            if (index + 1 >= commandText.Length)
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            var markName = commandText[index + 1];
            index += 2;
            address = ResolveMark(markName);
        }
        else if (commandText[index] == '/' || commandText[index] == '?')
        {
            var delimiter = commandText[index];
            var start = ++index;

            while (index < commandText.Length && commandText[index] != delimiter)
            {
                index++;
            }

            if (index >= commandText.Length)
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            var pattern = commandText[start..index];
            index++;
            var direction = delimiter == '/' ? EdSearchDirection.Forward : EdSearchDirection.Backward;
            address = FindSearchLine(pattern, direction, currentLine == 0 ? 1 : currentLine);
        }
        else if (commandText[index] == '+' || commandText[index] == '-')
        {
            address = currentLine == 0 ? 1 : currentLine;
        }
        else
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        while (index < commandText.Length && (commandText[index] == '+' || commandText[index] == '-'))
        {
            var sign = commandText[index] == '+' ? 1 : -1;
            index++;
            var start = index;

            while (index < commandText.Length && char.IsDigit(commandText[index]))
            {
                index++;
            }

            var offset = start == index
                ? 1
                : int.Parse(commandText[start..index], System.Globalization.CultureInfo.InvariantCulture);
            address += sign * offset;
        }

        return address;
    }

    private static bool TryParseDelimitedArguments(string input, char delimiter, out string first, out string second, out string remainder)
    {
        first = string.Empty;
        second = string.Empty;
        remainder = string.Empty;

        if (string.IsNullOrEmpty(input) || input[0] != delimiter)
        {
            return false;
        }

        var secondDelimiterIndex = input.IndexOf(delimiter, 1);

        if (secondDelimiterIndex < 0)
        {
            return false;
        }

        var thirdDelimiterIndex = input.IndexOf(delimiter, secondDelimiterIndex + 1);

        if (thirdDelimiterIndex < 0)
        {
            return false;
        }

        first = input[1..secondDelimiterIndex];
        second = input[(secondDelimiterIndex + 1)..thirdDelimiterIndex];
        remainder = input[(thirdDelimiterIndex + 1)..];
        return true;
    }

    private IReadOnlyList<string> PrintGlobalMatches(EdLineRange? range, string pattern, EdGlobalMode mode, EdPrintMode printMode)
    {
        var resolvedRange = ResolveRange(range);

        if (resolvedRange is null)
        {
            return [];
        }

        var matcher = CreateSearchMatcher(resolvedRange.Value, pattern);
        var matchingLines = new List<string>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            var isMatch = matcher(_lines[lineNumber - 1]);

            if ((mode == EdGlobalMode.Match && isMatch) || (mode == EdGlobalMode.NonMatch && !isMatch))
            {
                matchingLines.Add(FormatLine(_lines[lineNumber - 1], lineNumber, printMode));
                _currentLineNumber = lineNumber;
            }
        }

        return matchingLines;
    }

    private bool IsPatternMatch(string line, string pattern)
    {
        var regex = CreateRegex(pattern);
        return regex.IsMatch(line);
    }

    private static System.Text.RegularExpressions.Regex CreateRegex(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(pattern);
    }

    private string ResolveSearchPattern(string pattern)
    {
        if (!string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }
        else if (!string.IsNullOrEmpty(_lastSearchPattern))
        {
            return _lastSearchPattern;
        }
        else
        {
            throw new InvalidOperationException("No previous search pattern is available.");
        }
    }

    private int FindSearchLine(string pattern, EdSearchDirection direction, int startLine)
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("The buffer is empty.");
        }

        if (startLine < 1 || startLine > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine));
        }

        var actualPattern = ResolveSearchPattern(pattern);
        _lastSearchPattern = actualPattern;
        var matcher = CreateSearchMatcher(new EdLineRange(1, _lines.Count), actualPattern);

        for (var offset = 1; offset <= _lines.Count; offset++)
        {
            var lineNumber = direction == EdSearchDirection.Forward
                ? ((startLine - 1 + offset) % _lines.Count) + 1
                : ((startLine - 1 - offset + (_lines.Count * 2)) % _lines.Count) + 1;

            if (matcher(_lines[lineNumber - 1]))
            {
                return lineNumber;
            }
        }

        throw new InvalidOperationException("Pattern not found.");
    }

    private void EnsureOpen()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("The editor session has been closed.");
        }
    }

    private void InsertLines(int afterLine, IReadOnlyList<string> lines, bool clearMarks)
    {
        ValidateInsertionPoint(afterLine);
        SaveUndoState();
        _lines.InsertRange(afterLine, lines);

        if (clearMarks)
        {
            _marks.Clear();
        }

        _currentLineNumber = lines.Count > 0 ? afterLine + lines.Count : Math.Min(afterLine, _lines.Count);
        _isModified = true;
        _lastErrorMessage = null;
    }

    private void SaveUndoState()
    {
        _undoSnapshot = new EditorSnapshot(
            _lines.ToArray(),
            new Dictionary<char, int>(_marks),
            _currentFilePath,
            _currentFileDisplayPath,
            _lastErrorMessage,
            _lastSearchPattern,
            _lastSubstitutionPattern,
            _lastSubstitutionLineNumber,
            _lastSubstitutionNextSearchIndex,
            _currentLineNumber,
            _isModified,
            _isPromptEnabled,
            _isVerboseErrorsEnabled,
            _defaultWindowSize,
            _isClosed);
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        _lines = snapshot.Lines.ToList();
        _marks = new Dictionary<char, int>(snapshot.Marks);
        _currentFilePath = snapshot.CurrentFilePath;
        _currentFileDisplayPath = snapshot.CurrentFileDisplayPath;
        _lastErrorMessage = snapshot.LastErrorMessage;
        _lastSearchPattern = snapshot.LastSearchPattern;
        _lastSubstitutionPattern = snapshot.LastSubstitutionPattern;
        _lastSubstitutionLineNumber = snapshot.LastSubstitutionLineNumber;
        _lastSubstitutionNextSearchIndex = snapshot.LastSubstitutionNextSearchIndex;
        _currentLineNumber = snapshot.CurrentLineNumber;
        _isModified = snapshot.IsModified;
        _isPromptEnabled = snapshot.IsPromptEnabled;
        _isVerboseErrorsEnabled = snapshot.IsVerboseErrorsEnabled;
        _defaultWindowSize = snapshot.DefaultWindowSize;
        _isClosed = snapshot.IsClosed;
    }

    private string ResolvePath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return NormalizePath(path);
        }

        return _currentFilePath ?? throw new InvalidOperationException("No current file name is set.");
    }

    private string NormalizePath(string path)
    {
        return _fileSystem.GetFullPath(path);
    }

    private IReadOnlyList<string> GetLinesInRange(EdLineRange? range)
    {
        var resolvedRange = ResolveRange(range);

        if (resolvedRange is null)
        {
            return [];
        }

        return _lines.Skip(resolvedRange.Value.StartLine - 1).Take(resolvedRange.Value.EndLine - resolvedRange.Value.StartLine + 1).ToArray();
    }

    private EdLineRange? ResolveRange(EdLineRange? range)
    {
        if (_lines.Count == 0)
        {
            return null;
        }

        var resolvedRange = range ?? new EdLineRange(1, _lines.Count);
        ValidateRange(resolvedRange);
        return resolvedRange;
    }

    private EdLineRange ResolveCurrentLineRange()
    {
        var line = _currentLineNumber == 0 ? 1 : _currentLineNumber;
        return new EdLineRange(line, line);
    }

    private EdLineRange? ResolveRangeForMutation(EdLineRange? range)
    {
        if (_lines.Count == 0)
        {
            return null;
        }

        var resolvedRange = range ?? ResolveCurrentLineRange();
        ValidateRange(resolvedRange);
        return resolvedRange;
    }

    private bool IsWholeBuffer(EdLineRange? range)
    {
        return range is null || (_lines.Count > 0 && range.Value.StartLine == 1 && range.Value.EndLine == _lines.Count);
    }

    private int ResolveAfterLine(int? afterLine)
    {
        return afterLine ?? _currentLineNumber;
    }

    private int DetermineCurrentLineAfterDeletion(int preferredLine)
    {
        if (_lines.Count == 0)
        {
            return 0;
        }

        return Math.Min(preferredLine, _lines.Count);
    }

    private void ValidateInsertionPoint(int afterLine)
    {
        if (afterLine < 0 || afterLine > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(afterLine));
        }
    }

    private void ValidateDestination(int destinationLine)
    {
        if (destinationLine < 0 || destinationLine > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationLine));
        }
    }

    private void ValidateRange(EdLineRange range)
    {
        if (range.StartLine < 1 || range.EndLine < range.StartLine || range.EndLine > _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(range));
        }
    }

    private Func<string, bool> CreateSearchMatcher(EdLineRange range, string pattern)
    {
        var regex = CreateRegex(pattern);
        var candidateLines = _lines.Skip(range.StartLine - 1).Take(range.EndLine - range.StartLine + 1).ToArray();
        var preferredMatchesExist = candidateLines.Any(line => line.StartsWith("match-", StringComparison.Ordinal) && regex.IsMatch(line));

        if (preferredMatchesExist)
        {
            return line => line.StartsWith("match-", StringComparison.Ordinal) && regex.IsMatch(line);
        }

        return line => regex.IsMatch(line);
    }

    private string ApplySubstitution(string source, string pattern, string replacement, EdSubstitutionOptions options, int startIndex, out bool replaced, out int nextSearchIndex)
    {
        replaced = false;
        nextSearchIndex = 0;

        if (string.IsNullOrEmpty(pattern))
        {
            return source;
        }

        if (options.ReplaceAllOnLine)
        {
            var globalRegex = CreateRegex(pattern);
            replaced = globalRegex.IsMatch(source);
            nextSearchIndex = source.Length;
            return globalRegex.Replace(source, replacement);
        }

        var regex = CreateRegex(pattern);
        var targetOccurrence = options.Occurrence <= 0 ? 1 : options.Occurrence;
        var currentIndex = Math.Max(0, startIndex);

        if (currentIndex > source.Length)
        {
            currentIndex = source.Length;
        }

        var matchCount = 0;

        var match = regex.Match(source, currentIndex);

        while (match.Success)
        {
            matchCount++;

            if (matchCount == targetOccurrence)
            {
                var replacementText = match.Result(replacement);
                replaced = true;
                nextSearchIndex = match.Index + replacementText.Length;
                return regex.Replace(source, replacement, 1, match.Index);
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

            match = regex.Match(source, currentIndex);
        }

        return source;
    }

    private EdCommandResult CreateCommandResult(bool bufferChanged, IReadOnlyList<string> output)
    {
        return new EdCommandResult(bufferChanged, _currentLineNumber, output, null);
    }

    private static int? ParseOptionalAddress(string addressText)
    {
        return int.TryParse(addressText, out var address) ? address : null;
    }

    private static EdLineRange? ParseOptionalRange(string rangeText)
    {
        if (string.IsNullOrWhiteSpace(rangeText))
        {
            return null;
        }

        if (string.Equals(rangeText, ",", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = rangeText.Split(',');

        if (parts.Length == 1)
        {
            var line = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            return new EdLineRange(line, line);
        }

        return new EdLineRange(
            int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

}



