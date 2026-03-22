namespace Ed;

internal sealed record EditorSnapshot(
    IReadOnlyList<string> Lines,
    IReadOnlyDictionary<char, int> Marks,
    string? CurrentFilePath,
    string? CurrentFileDisplayPath,
    string? LastErrorMessage,
    string? LastSearchPattern,
    string? LastSubstitutionPattern,
    int LastSubstitutionLineNumber,
    int LastSubstitutionNextSearchIndex,
    int CurrentLineNumber,
    bool IsModified,
    bool IsPromptEnabled,
    bool IsVerboseErrorsEnabled,
    int DefaultWindowSize,
    bool IsClosed);

internal sealed record ParsedCommand(
    EdLineRange? Range,
    bool HasAddress,
    bool UsedSearchAddress,
    string CommandText);
