using Ed;
using FsCheck;

namespace Ed.Tests;

internal static class EdEditorTestSupport
{
    public static EdEditor CreateEditor()
    {
        return CreateEditor(out _, out _);
    }

    public static EdEditor CreateEditor(IEdRegexEngine regexEngine)
    {
        return CreateEditor(regexEngine, out _, out _);
    }

    public static EdEditor CreateEditor(out FakeEdFileSystem fileSystem, out FakeEdShell shell)
    {
        fileSystem = new FakeEdFileSystem();
        shell = new FakeEdShell();
        return new EdEditor(fileSystem, shell);
    }

    public static EdEditor CreateEditor(IEdRegexEngine regexEngine, out FakeEdFileSystem fileSystem, out FakeEdShell shell)
    {
        fileSystem = new FakeEdFileSystem();
        shell = new FakeEdShell();
        return new EdEditor(fileSystem, shell, regexEngine);
    }

    public static FileCase FileCaseAt(int index)
    {
        return CreateFileCases(index + 1).Last();
    }

    public static CommandCase CommandCaseAt(int index)
    {
        return CreateCommandCases(index + 1).Last();
    }

    public static SearchCase SearchCaseAt(int index)
    {
        return CreateSearchCases(index + 1).Last();
    }

    public static SubstitutionCase SubstitutionCaseAt(int index)
    {
        return CreateSubstitutionCases(index + 1).Last();
    }

    public static IReadOnlyList<string> LineSetAt(int index)
    {
        return CreateLineSets(index + 1).Last();
    }

    public static void SeedFile(FakeEdFileSystem fileSystem, FileCase fileCase)
    {
        fileSystem.SeedFile(fileCase.Path, fileCase.Lines);
        fileSystem.SeedFile(fileCase.FullPath, fileCase.Lines);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
    }

    public static IEnumerable<int> PositiveIntegers(int count)
    {
        var generator = FsCheck.Fluent.Gen.Choose(1, 20);
        var samples = FsCheck.Fluent.Gen.Sample(generator, 1, count * 16)
            .Distinct()
            .ToList();

        for (var value = 1; samples.Count < count && value <= 20; value++)
        {
            if (!samples.Contains(value))
            {
                samples.Add(value);
            }
        }

        foreach (var sample in samples.Take(count))
        {
            yield return sample;
        }
    }

    private static IEnumerable<FileCase> CreateFileCases(int count)
    {
        foreach (var token in CreateTokens(count))
        {
            yield return new FileCase(
                $".\\{token}.txt",
                $"C:\\virtual\\{token}.txt",
                CreateLines(token));
        }
    }

    private static IEnumerable<IReadOnlyList<string>> CreateLineSets(int count)
    {
        foreach (var token in CreateTokens(count))
        {
            yield return CreateLines(token);
        }
    }

    private static IEnumerable<CommandCase> CreateCommandCases(int count)
    {
        foreach (var token in CreateTokens(count))
        {
            yield return new CommandCase(
                $"echo {token}",
                CreateLines(token));
        }
    }

    private static IEnumerable<SearchCase> CreateSearchCases(int count)
    {
        foreach (var token in CreateTokens(count))
        {
            yield return new SearchCase(
                token,
                [
                    $"before-{token}",
                    $"match-{token}",
                    $"after-{token}",
                    $"wrap-{token}"
                ]);
        }
    }

    private static IEnumerable<SubstitutionCase> CreateSubstitutionCases(int count)
    {
        var tokens = CreateTokens(count * 2).ToArray();

        for (var index = 0; index < tokens.Length - 1; index += 2)
        {
            yield return new SubstitutionCase(
                tokens[index],
                tokens[index + 1],
                $"prefix-{tokens[index]}-{tokens[index]}-suffix");
        }
    }

    private static IReadOnlyList<string> CreateLines(string token)
    {
        return
        [
            $"{token}-one",
            $"{token}-two",
            $"{token}-three",
        ];
    }

    private static IEnumerable<string> CreateTokens(int count)
    {
        var generator = FsCheck.Fluent.Gen.Choose(1000, 999999);
        var samples = FsCheck.Fluent.Gen.Sample(generator, 1, count * 24)
            .Select(value => $"token{value}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        for (var index = samples.Count; index < count; index++)
        {
            samples.Add($"tokenfallback{index}");
        }

        foreach (var sample in samples.Take(count))
        {
            yield return sample;
        }
    }
}

internal readonly record struct FileCase(
    string Path,
    string FullPath,
    IReadOnlyList<string> Lines);

internal readonly record struct CommandCase(
    string CommandText,
    IReadOnlyList<string> Lines);

internal readonly record struct SearchCase(
    string Pattern,
    IReadOnlyList<string> Lines);

internal readonly record struct SubstitutionCase(
    string Pattern,
    string Replacement,
    string SourceLine);
