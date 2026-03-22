using Ed;

namespace Ed.Tests;

internal sealed class FakeEdFileSystem : IEdFileSystem
{
    private readonly Dictionary<string, string> _fullPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _files = new(StringComparer.Ordinal);

    public List<string> ExistsChecks { get; } = [];

    public List<string> FullPathRequests { get; } = [];

    public List<string> ReadRequests { get; } = [];

    public string? LastWritePath { get; private set; }

    public string? LastAppendPath { get; private set; }

    public IReadOnlyList<string>? LastWrittenLines { get; private set; }

    public IReadOnlyList<string>? LastAppendedLines { get; private set; }

    public void SetFullPath(string path, string fullPath)
    {
        _fullPaths[path] = fullPath;
    }

    public void SeedFile(string path, IReadOnlyList<string> lines)
    {
        _files[path] = lines.ToArray();
    }

    public IReadOnlyList<string> GetStoredLines(string path)
    {
        return _files[path];
    }

    public bool Exists(string path)
    {
        ExistsChecks.Add(path);
        return _files.ContainsKey(path);
    }

    public string GetFullPath(string path)
    {
        FullPathRequests.Add(path);

        if (_fullPaths.TryGetValue(path, out var fullPath))
        {
            return fullPath;
        }

        return path;
    }

    public IReadOnlyList<string> ReadAllLines(string path)
    {
        ReadRequests.Add(path);
        return _files[path];
    }

    public void WriteAllLines(string path, IReadOnlyList<string> lines)
    {
        LastWritePath = path;
        LastWrittenLines = lines.ToArray();
        _files[path] = lines.ToArray();
    }

    public void AppendAllLines(string path, IReadOnlyList<string> lines)
    {
        LastAppendPath = path;
        LastAppendedLines = lines.ToArray();

        if (_files.TryGetValue(path, out var existingLines))
        {
            _files[path] = existingLines.Concat(lines).ToArray();
        }
        else
        {
            _files[path] = lines.ToArray();
        }
    }
}

internal sealed class FakeEdShell : IEdShell
{
    private readonly Dictionary<string, IReadOnlyList<string>> _outputs = new(StringComparer.Ordinal);

    public List<string> OutputRequests { get; } = [];

    public List<string> ExecutedCommands { get; } = [];

    public List<(string CommandText, IReadOnlyList<string> Lines)> Writes { get; } = [];

    public void SeedOutput(string commandText, IReadOnlyList<string> lines)
    {
        _outputs[commandText] = lines.ToArray();
    }

    public IReadOnlyList<string> ReadCommandOutput(string commandText)
    {
        OutputRequests.Add(commandText);

        if (_outputs.TryGetValue(commandText, out var lines))
        {
            return lines;
        }

        return [];
    }

    public void WriteToCommand(string commandText, IReadOnlyList<string> lines)
    {
        Writes.Add((commandText, lines.ToArray()));
    }

    public void Execute(string commandText)
    {
        ExecutedCommands.Add(commandText);
    }
}

internal sealed class FakeEdRegexEngine : IEdRegexEngine
{
    public Func<string, string, bool>? IsMatchHandler { get; set; }

    public Func<string, string, string, string>? ReplaceHandler { get; set; }

    public Func<string, string, string, int, int, string>? ReplaceWithCountHandler { get; set; }

    public Func<string, string, int, EdRegexMatch>? MatchHandler { get; set; }

    public bool IsMatch(string pattern, string input)
    {
        if (IsMatchHandler is not null)
        {
            return IsMatchHandler(pattern, input);
        }

        return input.Contains(pattern, StringComparison.Ordinal);
    }

    public string Replace(string pattern, string input, string replacement)
    {
        if (ReplaceHandler is not null)
        {
            return ReplaceHandler(pattern, input, replacement);
        }

        return input.Replace(pattern, replacement, StringComparison.Ordinal);
    }

    public string Replace(string pattern, string input, string replacement, int count, int startAt)
    {
        if (ReplaceWithCountHandler is not null)
        {
            return ReplaceWithCountHandler(pattern, input, replacement, count, startAt);
        }

        if (count <= 0)
        {
            return input;
        }

        var matchIndex = input.IndexOf(pattern, startAt, StringComparison.Ordinal);

        if (matchIndex < 0)
        {
            return input;
        }

        return input.Remove(matchIndex, pattern.Length)
            .Insert(matchIndex, replacement);
    }

    public EdRegexMatch Match(string pattern, string input, int startAt)
    {
        if (MatchHandler is not null)
        {
            return MatchHandler(pattern, input, startAt);
        }

        var matchIndex = input.IndexOf(pattern, startAt, StringComparison.Ordinal);

        if (matchIndex < 0)
        {
            return EdRegexMatch.None;
        }

        return new EdRegexMatch(
            success: true,
            index: matchIndex,
            length: pattern.Length,
            expandReplacement: replacement => replacement);
    }
}
