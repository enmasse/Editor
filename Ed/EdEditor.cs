namespace Ed;

public sealed class EdEditor
{
    private readonly IEdFileSystem _fileSystem;
    private readonly IEdShell _shell;

    public EdEditor(
        IEdFileSystem fileSystem,
        IEdShell shell)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public string? CurrentFilePath
    {
        get => throw new NotImplementedException();
    }

    public int CurrentLineNumber
    {
        get => throw new NotImplementedException();
    }

    public int LineCount
    {
        get => throw new NotImplementedException();
    }

    public bool IsModified
    {
        get => throw new NotImplementedException();
    }

    public bool IsPromptEnabled
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public bool IsVerboseErrorsEnabled
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int DefaultWindowSize
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string? LastErrorMessage
    {
        get => throw new NotImplementedException();
    }

    public void CreateBuffer() => throw new NotImplementedException();

    public void Edit(string? path, bool force = false) => throw new NotImplementedException();

    public void SetFileName(string path) => throw new NotImplementedException();

    public void Read(string path, int? afterLine = null) => throw new NotImplementedException();

    public void ReadCommandOutput(string commandText, int? afterLine = null) => throw new NotImplementedException();

    public void Write(string? path = null, EdLineRange? range = null, EdWriteMode mode = EdWriteMode.Replace) => throw new NotImplementedException();

    public void WriteToCommand(string commandText, EdLineRange? range = null) => throw new NotImplementedException();

    public void Append(int? afterLine, IReadOnlyList<string> lines) => throw new NotImplementedException();

    public void Insert(int? beforeLine, IReadOnlyList<string> lines) => throw new NotImplementedException();

    public void Change(EdLineRange range, IReadOnlyList<string> lines) => throw new NotImplementedException();

    public void Delete(EdLineRange range) => throw new NotImplementedException();

    public IReadOnlyList<string> Print(EdLineRange? range = null, EdPrintMode mode = EdPrintMode.Normal) => throw new NotImplementedException();

    public IReadOnlyList<string> Scroll(int? startLine = null, int? lineCount = null, EdPrintMode mode = EdPrintMode.Normal) => throw new NotImplementedException();

    public int GetAddress(int? line = null) => throw new NotImplementedException();

    public void Join(EdLineRange range) => throw new NotImplementedException();

    public void Move(EdLineRange range, int destinationLine) => throw new NotImplementedException();

    public void Copy(EdLineRange range, int destinationLine) => throw new NotImplementedException();

    public void SetMark(char markName, int line) => throw new NotImplementedException();

    public int ResolveMark(char markName) => throw new NotImplementedException();

    public int Search(string pattern, EdSearchDirection direction, int? startLine = null) => throw new NotImplementedException();

    public void Substitute(EdLineRange? range, string pattern, string replacement, EdSubstitutionOptions? options = null) => throw new NotImplementedException();

    public void Global(EdLineRange? range, string pattern, string commandList, EdGlobalMode mode = EdGlobalMode.Match) => throw new NotImplementedException();

    public void ExecuteShellCommand(string commandText) => throw new NotImplementedException();

    public EdCommandResult ExecuteCommand(string commandText) => throw new NotImplementedException();

    public void Undo() => throw new NotImplementedException();

    public void Quit(bool force = false) => throw new NotImplementedException();
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
