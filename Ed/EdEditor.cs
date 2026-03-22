namespace Ed;

public sealed class EdEditor
{
    private readonly IEdFileSystem _fileSystem;
    private readonly IEdShell _shell;
    private readonly IEdRegexEngine _regexEngine;

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
        : this(
            fileSystem,
            shell,
            new DotNetEdRegexEngine())
    {
    }

    public EdEditor(
        IEdFileSystem fileSystem,
        IEdShell shell,
        IEdRegexEngine regexEngine)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _regexEngine = regexEngine ?? throw new ArgumentNullException(nameof(regexEngine));
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

        var resolvedPath = EdBufferUtilities.ResolvePath(path, _currentFilePath, NormalizePath);
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
        InsertLines(EdBufferUtilities.ResolveAfterLine(afterLine, _currentLineNumber), lines, clearMarks: false);
    }

    public void ReadCommandOutput(string commandText, int? afterLine = null)
    {
        EnsureOpen();
        var lines = _shell.ReadCommandOutput(commandText);
        InsertLines(EdBufferUtilities.ResolveAfterLine(afterLine, _currentLineNumber), lines, clearMarks: false);
    }

    public void Write(string? path = null, EdLineRange? range = null, EdWriteMode mode = EdWriteMode.Replace)
    {
        EnsureOpen();
        var resolvedPath = path is null ? EdBufferUtilities.ResolvePath(path, _currentFilePath, NormalizePath) : NormalizePath(path);
        var linesToWrite = EdBufferUtilities.GetLinesInRange(_lines, range);

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

        if (mode == EdWriteMode.Replace && EdBufferUtilities.IsWholeBuffer(_lines.Count, range))
        {
            _isModified = false;
        }

        _lastErrorMessage = null;
    }

    public void WriteToCommand(string commandText, EdLineRange? range = null)
    {
        EnsureOpen();
        _shell.WriteToCommand(commandText, EdBufferUtilities.GetLinesInRange(_lines, range));
    }

    public void Append(int? afterLine, IReadOnlyList<string> lines)
    {
        EnsureOpen();
        InsertLines(EdBufferUtilities.ResolveAfterLine(afterLine, _currentLineNumber), lines, clearMarks: true);
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
        EdBufferUtilities.ValidateRange(range, _lines.Count);
        SaveUndoState();
        _lines.RemoveRange(range.StartLine - 1, range.EndLine - range.StartLine + 1);
        _lines.InsertRange(range.StartLine - 1, lines);
        _marks.Clear();
        _currentLineNumber = lines.Count > 0 ? range.StartLine + lines.Count - 1 : EdBufferUtilities.DetermineCurrentLineAfterDeletion(_lines.Count, range.StartLine);
        _isModified = true;
        _lastErrorMessage = null;
    }

    public void Delete(EdLineRange range)
    {
        EnsureOpen();
        EdBufferUtilities.ValidateRange(range, _lines.Count);
        SaveUndoState();
        _lines.RemoveRange(range.StartLine - 1, range.EndLine - range.StartLine + 1);
        _marks.Clear();
        _currentLineNumber = EdBufferUtilities.DetermineCurrentLineAfterDeletion(_lines.Count, range.StartLine);
        _isModified = true;
        _lastErrorMessage = null;
    }

    public IReadOnlyList<string> Print(EdLineRange? range = null, EdPrintMode mode = EdPrintMode.Normal)
    {
        EnsureOpen();
        var resolvedRange = EdBufferUtilities.ResolveRange(_lines.Count, range);

        if (resolvedRange is null)
        {
            return [];
        }

        var output = new List<string>();

        for (var lineNumber = resolvedRange.Value.StartLine; lineNumber <= resolvedRange.Value.EndLine; lineNumber++)
        {
            output.Add(EdEditorTextUtilities.FormatLine(_lines[lineNumber - 1], lineNumber, mode));
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
        EdBufferUtilities.ValidateRange(range, _lines.Count);
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
        EdBufferUtilities.ValidateRange(range, _lines.Count);
        EdBufferUtilities.ValidateDestination(destinationLine, _lines.Count);

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
        EdBufferUtilities.ValidateRange(range, _lines.Count);
        EdBufferUtilities.ValidateDestination(destinationLine, _lines.Count);
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

        var lineNumber = FindSearchLine(pattern, direction, searchStart);
        _currentLineNumber = lineNumber;
        return lineNumber;
    }

    public void Substitute(EdLineRange? range, string pattern, string replacement, EdSubstitutionOptions? options = null)
    {
        EnsureOpen();
        options ??= new EdSubstitutionOptions();
        var actualPattern = options.Value.UsePreviousPattern && string.IsNullOrEmpty(pattern)
            ? _lastSubstitutionPattern ?? throw new InvalidOperationException("No previous substitution pattern is available.")
            : pattern;
        var resolvedRange = EdBufferUtilities.ResolveRangeForMutation(_lines.Count, range, _currentLineNumber);

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
            var updatedLine = EdEditorTextUtilities.ApplySubstitution(_regexEngine, _lines[lineNumber - 1], actualPattern, replacement, options.Value, startIndex, out var replaced, out var nextSearchIndex);

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
        EdGlobalOperations.Global(this, range, pattern, commandList, mode);
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
        catch (Exception ex) when (EdEditorTextUtilities.ShouldCaptureError(ex))
        {
            _lastErrorMessage = ex.Message;
            throw;
        }
    }

    private EdCommandResult ExecuteCommandCore(string commandText)
    {
        return EdCommandExecutor.Execute(this, commandText);
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

    internal int FindSearchLine(string pattern, EdSearchDirection direction, int startLine)
    {
        var lineNumber = EdSearchEngine.FindSearchLine(_regexEngine, _lines, pattern, direction, startLine, _lastSearchPattern, out var actualPattern);
        _lastSearchPattern = actualPattern;
        return lineNumber;
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
        EdBufferUtilities.ValidateInsertionPoint(afterLine, _lines.Count);
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

    internal void SaveUndoState()
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

    private string NormalizePath(string path)
    {
        return _fileSystem.GetFullPath(path);
    }

    internal EdCommandResult CreateCommandResult(bool bufferChanged, IReadOnlyList<string> output)
    {
        return new EdCommandResult(bufferChanged, _currentLineNumber, output, null);
    }

    internal List<string> BufferLines
    {
        get => _lines;
    }

    internal IEdRegexEngine RegexEngine
    {
        get => _regexEngine;
    }

    internal int CurrentLineNumberInternal
    {
        get => _currentLineNumber;
        set => _currentLineNumber = value;
    }

    internal string? LastSearchPatternInternal
    {
        get => _lastSearchPattern;
        set => _lastSearchPattern = value;
    }

    internal string? LastSubstitutionPatternInternal
    {
        get => _lastSubstitutionPattern;
        set => _lastSubstitutionPattern = value;
    }

    internal int LastSubstitutionLineNumberInternal
    {
        get => _lastSubstitutionLineNumber;
        set => _lastSubstitutionLineNumber = value;
    }

    internal int LastSubstitutionNextSearchIndexInternal
    {
        get => _lastSubstitutionNextSearchIndex;
        set => _lastSubstitutionNextSearchIndex = value;
    }

    internal bool IsModifiedInternal
    {
        get => _isModified;
        set => _isModified = value;
    }

    internal string? LastErrorMessageInternal
    {
        get => _lastErrorMessage;
        set => _lastErrorMessage = value;
    }

    internal bool HasUndoSnapshot
    {
        get => _undoSnapshot is not null;
    }

    internal string CurrentFileDisplayPathOrCurrentPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_currentFileDisplayPath))
            {
                return _currentFileDisplayPath;
            }

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                return _currentFilePath;
            }

            return string.Empty;
        }
    }

    internal void ClearMarks()
    {
        _marks.Clear();
    }

}



