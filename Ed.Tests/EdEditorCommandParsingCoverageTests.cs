using Ed;

namespace Ed.Tests;

public class EdEditorCommandParsingCoverageTests
{
    [Test]
    public async Task ExecuteCommand_ParsesNumberedPrintCommand()
    {
        // Verifies command parsing handles numbered print commands with explicit address ranges.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand("1,2n");

        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("1\talpha\n2\tbeta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesLiteralPrintCommand()
    {
        // Verifies command parsing handles literal print commands that expose non-printing characters.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha\tbeta"]);

        var result = editor.ExecuteCommand("1l");

        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\\tbeta$");
    }

    [Test]
    public async Task ExecuteCommand_ParsesWriteToShellCommand()
    {
        // Verifies command parsing handles shell write forms such as `w !cmd` for addressed ranges.
        var commandCase = EdEditorTestSupport.CommandCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        editor.Append(afterLine: null, ["one", "two", "three"]);

        var result = editor.ExecuteCommand($"2,3w !{commandCase.CommandText}");

        await Assert.That(shell.Writes.Count).IsEqualTo(1);
        await Assert.That(shell.Writes[0].CommandText).IsEqualTo(commandCase.CommandText);
        await Assert.That(string.Join("\n", shell.Writes[0].Lines)).IsEqualTo("two\nthree");
        await Assert.That(result.BufferChanged).IsFalse();
    }

    [Test]
    public async Task ExecuteCommand_ParsesReadFromShellCommand()
    {
        // Verifies command parsing handles shell read forms such as `r !cmd`.
        var commandCase = EdEditorTestSupport.CommandCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        editor.Append(afterLine: null, ["header"]);

        var result = editor.ExecuteCommand($"1r !{commandCase.CommandText}");

        await Assert.That(shell.OutputRequests.Count).IsEqualTo(1);
        await Assert.That(shell.OutputRequests[0]).IsEqualTo(commandCase.CommandText);
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(string.Join("\n", new[] { "header" }.Concat(commandCase.Lines)));
    }

    [Test]
    public async Task ExecuteCommand_ParsesShellEscapeCommand()
    {
        // Verifies command parsing handles direct shell escape commands beginning with `!`.
        var commandCase = EdEditorTestSupport.CommandCaseAt(2);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);

        var result = editor.ExecuteCommand($"!{commandCase.CommandText}");

        await Assert.That(shell.ExecutedCommands.Count).IsEqualTo(1);
        await Assert.That(shell.ExecutedCommands[0]).IsEqualTo(commandCase.CommandText);
        await Assert.That(result.BufferChanged).IsFalse();
    }

    [Test]
    public async Task ExecuteCommand_ParsesSubstituteGlobalFlagCommand()
    {
        // Verifies command parsing handles substitute commands with the global replacement flag.
        var substitutionCase = EdEditorTestSupport.SubstitutionCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [substitutionCase.SourceLine]);

        var result = editor.ExecuteCommand($"1s/{substitutionCase.Pattern}/{substitutionCase.Replacement}/g");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo($"prefix-{substitutionCase.Replacement}-{substitutionCase.Replacement}-suffix");
    }

    [Test]
    public async Task ExecuteCommand_ParsesCurrentLinePrintCommand_WhenRangeIsOmitted()
    {
        // Verifies command parsing defaults plain print commands to the current line when no address is supplied.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("gamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesCurrentLineSubstituteCommand_WhenRangeIsOmitted()
    {
        // Verifies command parsing defaults substitute commands to the current line when no address is supplied.
        var substitutionCase = EdEditorTestSupport.SubstitutionCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [substitutionCase.SourceLine]);

        var result = editor.ExecuteCommand($"s/{substitutionCase.Pattern}/{substitutionCase.Replacement}/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo($"prefix-{substitutionCase.Replacement}-{substitutionCase.Pattern}-suffix");
    }

    [Test]
    public async Task ExecuteCommand_ParsesSubstituteOccurrenceFlagCommand()
    {
        // Verifies command parsing handles numeric substitute flags that target a specific match occurrence.
        var substitutionCase = EdEditorTestSupport.SubstitutionCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [substitutionCase.SourceLine]);

        var result = editor.ExecuteCommand($"1s/{substitutionCase.Pattern}/{substitutionCase.Replacement}/2");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo($"prefix-{substitutionCase.Pattern}-{substitutionCase.Replacement}-suffix");
    }

    [Test]
    public async Task ExecuteCommand_ParsesGlobalDeleteCommand()
    {
        // Verifies command parsing handles matching global delete commands.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"g/{searchCase.Pattern}/d");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(searchCase.Lines[0] + "\n" + searchCase.Lines[2] + "\n" + searchCase.Lines[3]);
    }

    [Test]
    public async Task ExecuteCommand_ParsesDeleteCommand()
    {
        // Verifies command parsing handles delete commands with an explicit addressed range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("2d");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesCurrentLineNumberCommand()
    {
        // Verifies command parsing handles the `=` command and emits the current line number.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("=");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("3");
    }

    [Test]
    public async Task ExecuteCommand_ParsesFileNameDisplayCommand()
    {
        // Verifies command parsing handles `f` by reporting the current file name.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.SetFileName(fileCase.Path);

        var result = editor.ExecuteCommand("f");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(fileCase.Path);
    }

    [Test]
    public async Task ExecuteCommand_ParsesPromptToggleCommand()
    {
        // Verifies command parsing handles `P` by toggling prompt mode.
        var editor = EdEditorTestSupport.CreateEditor();

        var result = editor.ExecuteCommand("P");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.IsPromptEnabled).IsTrue();
    }

    [Test]
    public async Task ExecuteCommand_ParsesForwardSearchCommand()
    {
        // Verifies command parsing handles forward search commands and prints the matched line.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"/{searchCase.Pattern}/");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(searchCase.Lines[1]);
    }

    [Test]
    public async Task ExecuteCommand_ParsesRegexForwardSearchCommand()
    {
        // Verifies command parsing routes forward searches through the .NET Regex engine.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "item-42", "item-x"]);

        var result = editor.ExecuteCommand(@"/item-\d+/" );

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("item-42");
    }

    [Test]
    public async Task ExecuteCommand_ParsesBackwardSearchCommand()
    {
        // Verifies command parsing handles backward search commands and prints the matched line.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"?{searchCase.Pattern}?");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(searchCase.Lines[3]);
    }

    [Test]
    public async Task ExecuteCommand_ParsesRegexSubstituteCommand()
    {
        // Verifies command parsing routes substitute commands through the .NET Regex engine.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["prefix-item-42-item-84-suffix"]);

        var result = editor.ExecuteCommand(@"1s/item-(\d+)/[$1]/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("prefix-[42]-item-84-suffix");
    }

    [Test]
    public async Task ExecuteCommand_ParsesGlobalPrintCommand()
    {
        // Verifies command parsing handles global commands whose command list prints matching lines.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"g/{searchCase.Pattern}/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo(searchCase.Lines[1]);
    }

    [Test]
    public async Task ExecuteCommand_ParsesNonMatchingGlobalDeleteCommand()
    {
        // Verifies command parsing handles non-matching global delete commands.
        var searchCase = EdEditorTestSupport.SearchCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"v/{searchCase.Pattern}/d");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(searchCase.Lines[1]);
    }

    [Test]
    public void Quit_Force_AllowsModifiedBufferToClose()
    {
        // Verifies force quit can close a modified session without requiring a prior write.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, EdEditorTestSupport.LineSetAt(0));

        editor.Quit(force: true);
    }
}
