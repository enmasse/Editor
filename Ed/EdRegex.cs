namespace Ed;

public interface IEdRegexEngine
{
    bool IsMatch(
        string pattern,
        string input);

    string Replace(
        string pattern,
        string input,
        string replacement);

    string Replace(
        string pattern,
        string input,
        string replacement,
        int count,
        int startAt);

    EdRegexMatch Match(
        string pattern,
        string input,
        int startAt);
}

public sealed class EdRegexMatch
{
    private readonly Func<string, string> _expandReplacement;

    public static EdRegexMatch None { get; } = new(
        success: false,
        index: 0,
        length: 0,
        expandReplacement: static replacement => replacement);

    public EdRegexMatch(
        bool success,
        int index,
        int length,
        Func<string, string> expandReplacement)
    {
        Success = success;
        Index = index;
        Length = length;
        _expandReplacement = expandReplacement ?? throw new ArgumentNullException(nameof(expandReplacement));
    }

    public bool Success { get; }

    public int Index { get; }

    public int Length { get; }

    public string ExpandReplacement(string replacement)
    {
        return _expandReplacement(replacement);
    }
}

public sealed class DotNetEdRegexEngine : IEdRegexEngine
{
    public bool IsMatch(
        string pattern,
        string input)
    {
        var regex = CreateRegex(pattern);
        return regex.IsMatch(input);
    }

    public string Replace(
        string pattern,
        string input,
        string replacement)
    {
        var regex = CreateRegex(pattern);
        return regex.Replace(input, replacement);
    }

    public string Replace(
        string pattern,
        string input,
        string replacement,
        int count,
        int startAt)
    {
        var regex = CreateRegex(pattern);
        return regex.Replace(input, replacement, count, startAt);
    }

    public EdRegexMatch Match(
        string pattern,
        string input,
        int startAt)
    {
        var regex = CreateRegex(pattern);
        var match = regex.Match(input, startAt);

        if (!match.Success)
        {
            return EdRegexMatch.None;
        }

        return new EdRegexMatch(
            success: true,
            index: match.Index,
            length: match.Length,
            expandReplacement: replacement => match.Result(replacement));
    }

    private static System.Text.RegularExpressions.Regex CreateRegex(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(pattern);
    }
}
