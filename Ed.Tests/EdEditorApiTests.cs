using Ed;
using FsCheck;

namespace Ed.Tests;

public class EdEditorApiTests
{
    [Test]
    public async Task StateProperties_ReturnDefaults_WhenEditorIsNew()
    {
        // Verifies a new editor starts with an empty default state.
        var editor = CreateEditor();

        await Assert.That(editor.CurrentFilePath).IsNull();
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(0);
        await Assert.That(editor.LineCount).IsEqualTo(0);
        await Assert.That(editor.IsModified).IsFalse();
        await Assert.That(editor.LastErrorMessage).IsNull();
    }

    [Test]
    [MethodDataSource(nameof(BooleanCases))]
    public async Task PromptMode_RoundTripsConfiguredValue(bool enabled)
    {
        // Verifies prompt mode can be configured through the editor API.
        var editor = CreateEditor();

        editor.IsPromptEnabled = enabled;

        await Assert.That(editor.IsPromptEnabled).IsEqualTo(enabled);
    }

    [Test]
    [MethodDataSource(nameof(BooleanCases))]
    public async Task VerboseErrors_RoundTripsConfiguredValue(bool enabled)
    {
        // Verifies verbose error mode can be configured through the editor API.
        var editor = CreateEditor();

        editor.IsVerboseErrorsEnabled = enabled;

        await Assert.That(editor.IsVerboseErrorsEnabled).IsEqualTo(enabled);
    }

    [Test]
    [MethodDataSource(nameof(WindowSizeCases))]
    public async Task DefaultWindowSize_RoundTripsConfiguredValue(int windowSize)
    {
        // Verifies the editor exposes a configurable default scroll window size.
        var editor = CreateEditor();

        editor.DefaultWindowSize = windowSize;

        await Assert.That(editor.DefaultWindowSize).IsEqualTo(windowSize);
    }

    [Test]
    public async Task CreateBuffer_ClearsBufferState()
    {
        // Verifies creating a fresh buffer clears existing content and resets cursor state.
        var lines = LineSetCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, lines);

        editor.CreateBuffer();

        await Assert.That(editor.LineCount).IsEqualTo(0);
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(0);
        await Assert.That(editor.IsModified).IsFalse();
    }

    [Test]
    public async Task SetFileName_UsesNormalizedPath()
    {
        // Verifies setting a file name stores the normalized path from the injected file system.
        var fileCase = FileCases().First();
        var fileSystem = new FakeEdFileSystem();
        var shell = new FakeEdShell();
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        SeedFile(fileSystem, fileCase.Path, fileCase.FullPath, fileCase.Lines);
        var editor = new EdEditor(fileSystem, shell);

        editor.SetFileName(fileCase.Path);

        await Assert.That(editor.CurrentFilePath).IsEqualTo(fileCase.FullPath);
    }

    [Test]
    public async Task Edit_LoadsFileIntoBuffer()
    {
        // Verifies editing a file loads its content into the active buffer.
        var fileCase = FileCases().First();
        var fileSystem = new FakeEdFileSystem();
        var shell = new FakeEdShell();
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        SeedFile(fileSystem, fileCase.Path, fileCase.FullPath, fileCase.Lines);
        var editor = new EdEditor(fileSystem, shell);

        editor.Edit(fileCase.Path);

        await Assert.That(editor.CurrentFilePath).IsEqualTo(fileCase.FullPath);
        await Assert.That(editor.LineCount).IsEqualTo(fileCase.Lines.Length);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", fileCase.Lines));
    }

    [Test]
    public async Task Read_AppendsFileContentAfterAddress()
    {
        // Verifies reading a file inserts its content into the existing buffer after the requested address.
        var fileCase = FileCases().First();
        var fileSystem = new FakeEdFileSystem();
        var shell = new FakeEdShell();
        var initialLines = new[] { "header" };
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        SeedFile(fileSystem, fileCase.Path, fileCase.FullPath, fileCase.Lines);
        var editor = new EdEditor(fileSystem, shell);
        editor.Append(afterLine: null, initialLines);

        editor.Read(fileCase.Path, afterLine: 1);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", initialLines.Concat(fileCase.Lines)));
    }

    [Test]
    public async Task ReadCommandOutput_AppendsShellOutputAfterAddress()
    {
        // Verifies shell command output can be inserted into the buffer.
        var commandCase = CommandCases().First();
        var fileSystem = new FakeEdFileSystem();
        var shell = new FakeEdShell();
        var initialLines = new[] { "before" };
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        var editor = new EdEditor(fileSystem, shell);
        editor.Append(afterLine: null, initialLines);

        editor.ReadCommandOutput(commandCase.CommandText, afterLine: 1);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", initialLines.Concat(commandCase.Lines)));
    }

    [Test]
    public async Task Write_PersistsBufferContentToFile()
    {
        // Verifies writing persists the current buffer content to the selected file.
        var fileCase = FileCases().First();
        var fileSystem = new FakeEdFileSystem();
        var shell = new FakeEdShell();
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        var editor = new EdEditor(fileSystem, shell);
        editor.Append(afterLine: null, fileCase.Lines);

        editor.Write(fileCase.Path);

        await Assert.That(fileSystem.LastWritePath).IsEqualTo(fileCase.FullPath);
        await Assert.That(string.Join("\n", fileSystem.GetStoredLines(fileCase.FullPath))).IsEqualTo(string.Join("\n", fileCase.Lines));
    }

    [Test]
    public async Task WriteToCommand_SendsBufferRangeToShell()
    {
        // Verifies writing to a shell command forwards the selected buffer content.
        var commandCase = CommandCases().First();
        var editor = CreateEditor(out _, out var shell);
        editor.Append(afterLine: null, commandCase.Lines);

        editor.WriteToCommand(commandCase.CommandText);

        await Assert.That(shell.Writes.Count).IsEqualTo(1);
        await Assert.That(shell.Writes[0].CommandText).IsEqualTo(commandCase.CommandText);
        await Assert.That(string.Join("\n", shell.Writes[0].Lines)).IsEqualTo(string.Join("\n", commandCase.Lines));
    }

    [Test]
    public async Task Append_AddsLinesAfterAddress()
    {
        // Verifies append adds new lines to the buffer and moves the current line to the final inserted line.
        var lines = LineSetCases().First();
        var editor = CreateEditor();

        editor.Append(afterLine: null, lines);

        await Assert.That(editor.LineCount).IsEqualTo(lines.Length);
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(lines.Length);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", lines));
    }

    [Test]
    public async Task Insert_AddsLinesBeforeAddress()
    {
        // Verifies insert places lines before the addressed line.
        var lines = LineSetCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "tail" });

        editor.Insert(beforeLine: 1, lines);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", lines.Concat(new[] { "tail" })));
    }

    [Test]
    public async Task Change_ReplacesSelectedRange()
    {
        // Verifies change replaces the addressed lines with the provided content.
        var replacementLines = LineSetCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "first", "second", "third" });

        editor.Change(new EdLineRange(2, 2), replacementLines);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", new[] { "first" }.Concat(replacementLines).Concat(new[] { "third" })));
    }

    [Test]
    public async Task Delete_RemovesSelectedRange()
    {
        // Verifies delete removes the addressed lines from the buffer.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "first", "second", "third" });

        editor.Delete(new EdLineRange(2, 2));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("first\nthird");
    }

    [Test]
    public async Task Print_ReturnsRequestedLines()
    {
        // Verifies print returns the requested range from the active buffer.
        var lines = LineSetCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, lines);
        var range = new EdLineRange(1, lines.Length);

        var printedLines = editor.Print(range);

        await Assert.That(string.Join("\n", printedLines)).IsEqualTo(string.Join("\n", lines));
    }

    [Test]
    public async Task Scroll_ReturnsWindowUsingRequestedCount()
    {
        // Verifies scroll returns the requested number of lines starting at the requested address.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "one", "two", "three", "four" });

        var scrolledLines = editor.Scroll(startLine: 2, lineCount: 2);

        await Assert.That(string.Join("\n", scrolledLines)).IsEqualTo("two\nthree");
    }

    [Test]
    [MethodDataSource(nameof(AddressCases))]
    public async Task GetAddress_ReturnsExplicitLine(int line)
    {
        // Verifies explicit addresses resolve directly to the requested line number.
        var editor = CreateEditor();

        var address = editor.GetAddress(line);

        await Assert.That(address).IsEqualTo(line);
    }

    [Test]
    public async Task Join_ConcatenatesSelectedLines()
    {
        // Verifies join merges the addressed lines into a single line.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "alpha", "beta", "gamma" });

        editor.Join(new EdLineRange(1, 2));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alphabeta\ngamma");
    }

    [Test]
    public async Task Move_RelocatesSelectedRange()
    {
        // Verifies move removes a range from its original position and reinserts it after the destination line.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "one", "two", "three" });

        editor.Move(new EdLineRange(1, 1), destinationLine: 3);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("two\nthree\none");
    }

    [Test]
    public async Task Copy_DuplicatesSelectedRange()
    {
        // Verifies copy duplicates a range after the destination line without removing the original.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "one", "two", "three" });

        editor.Copy(new EdLineRange(1, 1), destinationLine: 2);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\ntwo\none\nthree");
    }

    [Test]
    [MethodDataSource(nameof(MarkCases))]
    public async Task SetMark_ThenResolveMark_ReturnsLine(char markName, int line)
    {
        // Verifies marks can be set and later resolved back to a line address.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "one", "two", "three", "four" });

        editor.SetMark(markName, line);
        var resolvedLine = editor.ResolveMark(markName);

        await Assert.That(resolvedLine).IsEqualTo(line);
    }

    [Test]
    public async Task Search_ReturnsMatchingLineNumber()
    {
        // Verifies forward search returns the first matching line number.
        var searchCase = SearchCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var lineNumber = editor.Search(searchCase.Pattern, EdSearchDirection.Forward, startLine: 1);

        await Assert.That(lineNumber).IsEqualTo(2);
    }

    [Test]
    public async Task Substitute_ReplacesPatternWithinRange()
    {
        // Verifies substitute replaces matching text within the addressed range.
        var substitutionCase = SubstitutionCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { substitutionCase.SourceLine });

        editor.Substitute(new EdLineRange(1, 1), substitutionCase.Pattern, substitutionCase.Replacement);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(substitutionCase.SourceLine.Replace(substitutionCase.Pattern, substitutionCase.Replacement, StringComparison.Ordinal));
    }

    [Test]
    public async Task Global_ExecutesCommandListForMatches()
    {
        // Verifies global applies a command list to every matching line in the addressed range.
        var searchCase = SearchCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        editor.Global(new EdLineRange(1, searchCase.Lines.Length), searchCase.Pattern, "d");

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(searchCase.Lines[0]);
    }

    [Test]
    public async Task ExecuteShellCommand_DelegatesToShell()
    {
        // Verifies direct shell commands are forwarded to the injected shell abstraction.
        var commandCase = CommandCases().First();
        var editor = CreateEditor(out _, out var shell);

        editor.ExecuteShellCommand(commandCase.CommandText);

        await Assert.That(shell.ExecutedCommands.Count).IsEqualTo(1);
        await Assert.That(shell.ExecutedCommands[0]).IsEqualTo(commandCase.CommandText);
    }

    [Test]
    public async Task ExecuteCommand_ParsesAndRunsEditorCommand()
    {
        // Verifies command execution returns the same printed output as the equivalent direct API call.
        var lines = LineSetCases().First();
        var editor = CreateEditor();
        editor.Append(afterLine: null, lines);

        var result = editor.ExecuteCommand(",p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(result.CurrentLine).IsEqualTo(lines.Length);
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(string.Join("\n", lines));
    }

    [Test]
    public async Task Undo_RevertsPreviousChange()
    {
        // Verifies undo restores the buffer to its previous state after a mutating command.
        var editor = CreateEditor();
        editor.Append(afterLine: null, new[] { "one", "two", "three" });
        editor.Delete(new EdLineRange(2, 2));

        editor.Undo();

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\ntwo\nthree");
    }

    [Test]
    public void Quit_AllowsClosingCleanBuffer()
    {
        // Verifies a clean editor session can be closed without forcing.
        var editor = CreateEditor();

        editor.Quit(force: false);
    }

    public static IEnumerable<bool> BooleanCases()
    {
        yield return true;
        yield return false;
    }

    public static IEnumerable<int> WindowSizeCases()
    {
        foreach (var value in CreatePositiveIntegers(4))
        {
            yield return value;
        }
    }

    public static IEnumerable<int> AddressCases()
    {
        foreach (var value in CreatePositiveIntegers(4))
        {
            yield return value;
        }
    }

    public static IEnumerable<(string Path, string FullPath, string[] Lines)> FileCases()
    {
        foreach (var token in CreateTokens(4))
        {
            yield return ($".\\{token}.txt", $"C:\\virtual\\{token}.txt", CreateLines(token));
        }
    }

    public static IEnumerable<string[]> LineSetCases()
    {
        foreach (var token in CreateTokens(4))
        {
            yield return CreateLines(token);
        }
    }

    public static IEnumerable<(string CommandText, string[] Lines)> CommandCases()
    {
        foreach (var token in CreateTokens(4))
        {
            yield return ($"echo {token}", CreateLines(token));
        }
    }

    public static IEnumerable<(char MarkName, int Line)> MarkCases()
    {
        foreach (var token in CreateTokens(4))
        {
            var line = token.Length;

            if (line > 4)
            {
                line = 4;
            }

            yield return (char.ToLowerInvariant(token[0]), line);
        }
    }

    public static IEnumerable<(string Pattern, string[] Lines)> SearchCases()
    {
        foreach (var token in CreateTokens(4))
        {
            yield return (token, new[] { $"before-{token}", $"match-{token}", $"after-{token}" });
        }
    }

    public static IEnumerable<(string Pattern, string Replacement, string SourceLine)> SubstitutionCases()
    {
        var tokens = CreateTokens(8).ToArray();

        for (var index = 0; index < tokens.Length - 1; index += 2)
        {
            var pattern = tokens[index];
            var replacement = tokens[index + 1];
            var sourceLine = $"prefix-{pattern}-suffix";

            yield return (pattern, replacement, sourceLine);
        }
    }

    private static EdEditor CreateEditor()
    {
        return CreateEditor(out _, out _);
    }

    private static EdEditor CreateEditor(out FakeEdFileSystem fileSystem, out FakeEdShell shell)
    {
        fileSystem = new FakeEdFileSystem();
        shell = new FakeEdShell();
        return new EdEditor(fileSystem, shell);
    }

    private static void SeedFile(FakeEdFileSystem fileSystem, string path, string fullPath, string[] lines)
    {
        fileSystem.SeedFile(path, lines);
        fileSystem.SeedFile(fullPath, lines);
    }

    private static string[] CreateLines(string token)
    {
        return [
            $"{token}-one",
            $"{token}-two",
            $"{token}-three",
        ];
    }

    private static IEnumerable<int> CreatePositiveIntegers(int count)
    {
        var generator = FsCheck.Fluent.Gen.Choose(1, 20);
        var samples = FsCheck.Fluent.Gen.Sample(generator, 0, count * 8)
            .Distinct()
            .Take(count);

        foreach (var sample in samples)
        {
            yield return sample;
        }
    }

    private static IEnumerable<string> CreateTokens(int count)
    {
        var generator = FsCheck.Fluent.Gen.Choose(1000, 999999);
        var samples = FsCheck.Fluent.Gen.Sample(generator, 0, count * 12)
            .Select(value => $"token{value}")
            .Distinct(StringComparer.Ordinal)
            .Take(count);

        foreach (var sample in samples)
        {
            yield return sample;
        }
    }
}
