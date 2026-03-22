namespace Ed;

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
