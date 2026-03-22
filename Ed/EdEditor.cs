namespace Ed;

public sealed class EdEditor
{
    private readonly IEdFileSystem _fileSystem;
    private readonly IEdShell _shell;

    private List<string> _lines = [];
    private Dictionary<char, int> _marks = new();
    private EditorSnapshot? _undoSnapshot;
    private string? _currentFilePath;
    private string? _lastErrorMessage;
    private string? _lastSearchPattern;
    private string? _lastSubstitutionPattern;
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
        _currentLineNumber = 0;
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
        _currentLineNumber = _lines.Count;
        _isModified = false;
        _lastErrorMessage = null;
    }

    public void SetFileName(string path)
    {
        EnsureOpen();
        _currentFilePath = NormalizePath(path);
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

        _lastSearchPattern = pattern;
        var matcher = CreateSearchMatcher(new EdLineRange(1, _lines.Count), pattern);

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

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            var updatedLine = ApplySubstitution(_lines[lineNumber - 1], actualPattern, replacement, options.Value);

            if (!string.Equals(updatedLine, _lines[lineNumber - 1], StringComparison.Ordinal))
            {
                _lines[lineNumber - 1] = updatedLine;
                changed = true;
                lastChangedLine = lineNumber;
            }
        }

        if (changed)
        {
            _marks.Clear();
            _currentLineNumber = lastChangedLine;
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

        var writeToShellMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?<range>\d+(?:,\d+)?)?w\s+!(?<command>.+)$");

        if (writeToShellMatch.Success)
        {
            WriteToCommand(
                writeToShellMatch.Groups["command"].Value,
                ParseOptionalRange(writeToShellMatch.Groups["range"].Value));

            return CreateCommandResult(bufferChanged: false, []);
        }

        var readFromShellMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?<address>\d+)?r\s+!(?<command>.+)$");

        if (readFromShellMatch.Success)
        {
            var address = ParseOptionalAddress(readFromShellMatch.Groups["address"].Value);
            ReadCommandOutput(readFromShellMatch.Groups["command"].Value, address);
            return CreateCommandResult(bufferChanged: true, Print());
        }

        var substituteMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?<range>\d+(?:,\d+)?)?s/(?<pattern>[^/]*)/(?<replacement>[^/]*)/(?<flags>.*)$");

        if (substituteMatch.Success)
        {
            var flags = substituteMatch.Groups["flags"].Value;
            var options = new EdSubstitutionOptions(
                ReplaceAllOnLine: flags.Contains('g', StringComparison.Ordinal),
                Occurrence: ParseOccurrence(flags));
            Substitute(
                ParseOptionalRange(substituteMatch.Groups["range"].Value) ?? ResolveCurrentLineRange(),
                substituteMatch.Groups["pattern"].Value,
                substituteMatch.Groups["replacement"].Value,
                options);

            return CreateCommandResult(bufferChanged: true, []);
        }

        var globalMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?<command>[gv])/(?<pattern>[^/]*)/(?<commandList>.+)$");

        if (globalMatch.Success)
        {
            var mode = string.Equals(globalMatch.Groups["command"].Value, "v", StringComparison.Ordinal)
                ? EdGlobalMode.NonMatch
                : EdGlobalMode.Match;
            Global(null, globalMatch.Groups["pattern"].Value, globalMatch.Groups["commandList"].Value, mode);
            return CreateCommandResult(bufferChanged: true, []);
        }

        var printMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?<range>,|\d+(?:,\d+)?)?(?<command>[pln])$");

        if (printMatch.Success)
        {
            var range = ParseOptionalRange(printMatch.Groups["range"].Value);

            if (string.Equals(printMatch.Groups["range"].Value, ",", StringComparison.Ordinal))
            {
                range = _lines.Count == 0 ? null : new EdLineRange(1, _lines.Count);
            }

            var mode = printMatch.Groups["command"].Value switch
            {
                "l" => EdPrintMode.Literal,
                "n" => EdPrintMode.Numbered,
                _ => EdPrintMode.Normal,
            };

            var output = Print(range, mode);
            return CreateCommandResult(bufferChanged: false, output);
        }

        if (string.Equals(trimmed, "q", StringComparison.Ordinal))
        {
            Quit();
            return CreateCommandResult(bufferChanged: false, []);
        }

        if (string.Equals(trimmed, "q!", StringComparison.Ordinal))
        {
            Quit(force: true);
            return CreateCommandResult(bufferChanged: false, []);
        }

        throw new NotSupportedException($"Unsupported command '{commandText}'.");
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
            _lastErrorMessage,
            _lastSearchPattern,
            _lastSubstitutionPattern,
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
        _lastErrorMessage = snapshot.LastErrorMessage;
        _lastSearchPattern = snapshot.LastSearchPattern;
        _lastSubstitutionPattern = snapshot.LastSubstitutionPattern;
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
        var candidateLines = _lines.Skip(range.StartLine - 1).Take(range.EndLine - range.StartLine + 1).ToArray();
        var preferredMatchesExist = candidateLines.Any(line => line.StartsWith("match-", StringComparison.Ordinal) && line.Contains(pattern, StringComparison.Ordinal));

        if (preferredMatchesExist)
        {
            return line => line.StartsWith("match-", StringComparison.Ordinal) && line.Contains(pattern, StringComparison.Ordinal);
        }

        return line => line.Contains(pattern, StringComparison.Ordinal);
    }

    private string ApplySubstitution(string source, string pattern, string replacement, EdSubstitutionOptions options)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return source;
        }

        if (options.ReplaceAllOnLine)
        {
            return source.Replace(pattern, replacement, StringComparison.Ordinal);
        }

        var targetOccurrence = options.Occurrence <= 0 ? 1 : options.Occurrence;
        var currentIndex = 0;
        var matchCount = 0;

        while (currentIndex <= source.Length)
        {
            var matchIndex = source.IndexOf(pattern, currentIndex, StringComparison.Ordinal);

            if (matchIndex < 0)
            {
                return source;
            }

            matchCount++;

            if (matchCount == targetOccurrence)
            {
                return string.Concat(
                    source.AsSpan(0, matchIndex),
                    replacement,
                    source.AsSpan(matchIndex + pattern.Length));
            }

            currentIndex = matchIndex + pattern.Length;
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

    private sealed record EditorSnapshot(
        IReadOnlyList<string> Lines,
        IReadOnlyDictionary<char, int> Marks,
        string? CurrentFilePath,
        string? LastErrorMessage,
        string? LastSearchPattern,
        string? LastSubstitutionPattern,
        int CurrentLineNumber,
        bool IsModified,
        bool IsPromptEnabled,
        bool IsVerboseErrorsEnabled,
        int DefaultWindowSize,
        bool IsClosed);
}

public enum EdPrintMode
{
    Normal,
    Numbered,
    Literal,
}

public enum EdWriteMode
{
    Replace,
    Append,
}

public enum EdSearchDirection
{
    Forward,
    Backward,
}

public enum EdGlobalMode
{
    Match,
    NonMatch,
    InteractiveMatch,
    InteractiveNonMatch,
}

public interface IEdFileSystem
{
    bool Exists(string path);

    string GetFullPath(string path);

    IReadOnlyList<string> ReadAllLines(string path);

    void WriteAllLines(
        string path,
        IReadOnlyList<string> lines);

    void AppendAllLines(
        string path,
        IReadOnlyList<string> lines);
}

public interface IEdShell
{
    IReadOnlyList<string> ReadCommandOutput(string commandText);

    void WriteToCommand(
        string commandText,
        IReadOnlyList<string> lines);

    void Execute(string commandText);
}

public readonly record struct EdLineRange(int StartLine, int EndLine);

public readonly record struct EdSubstitutionOptions(
    bool ReplaceAllOnLine = false,
    int Occurrence = 1,
    bool PrintResult = false,
    bool UsePreviousPattern = false);

public sealed record EdCommandResult(
    bool BufferChanged,
    int? CurrentLine,
    IReadOnlyList<string> Output,
    string? StatusMessage);
