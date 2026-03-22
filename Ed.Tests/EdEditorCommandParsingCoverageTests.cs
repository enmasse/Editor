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
    public async Task ExecuteCommand_RejectsLiteralPrintCommand()
    {
        // Verifies command parsing rejects the unsupported `l` command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha\tbeta"]);

        await Assert.That(() => editor.ExecuteCommand("1l")).Throws<NotSupportedException>();
    }

    [Test]
    public async Task ExecuteCommand_ParsesWholeBufferLiteralPrintShorthand()
    {
        // Verifies command parsing handles the classic `,l` shorthand by listing every line in literal form.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta\tgamma"]);

        var result = editor.ExecuteCommand(",l");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha$\nbeta\\tgamma$");
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
    public async Task ExecuteCommand_UsesSearchAddressPattern_WhenSubstitutePatternIsOmitted()
    {
        // Verifies commands like `/Hello/s//Hej/` reuse the search-address pattern for substitution.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["before", "Hello there", "after"]);

        var result = editor.ExecuteCommand("/Hello/s//Hej/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("before\nHej there\nafter");
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
    public async Task ExecuteCommand_ParsesAddressOnlyCommand_AsPrint()
    {
        // Verifies command parsing treats an addressed line with no explicit suffix as the classic default print command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("2");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesEmptyCommand_AsPrintNextLine()
    {
        // Verifies command parsing treats an empty command as the classic print-next-line behavior.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);
        editor.ExecuteCommand("1p");

        var result = editor.ExecuteCommand(string.Empty);

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
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
    public async Task ExecuteCommand_ParsesAppendCommand_WithDotTerminatedInput()
    {
        // Verifies command parsing handles the classic `a` command with dot-terminated text input.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("a\nbeta\ngamma\n.");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\nbeta\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesInsertCommand_WithDotTerminatedInput()
    {
        // Verifies command parsing handles the classic `i` command with dot-terminated text input.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand("1i\nzero\n.");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("zero\nalpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesChangeCommand_WithDotTerminatedInput()
    {
        // Verifies command parsing handles the classic `c` command with dot-terminated replacement text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("2c\nreplacement\n.");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\nreplacement\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesJoinCommand()
    {
        // Verifies command parsing handles the classic `j` command by merging the addressed lines.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("1,2j");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alphabeta\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesMoveCommand()
    {
        // Verifies command parsing handles the classic `m` command by relocating the addressed span.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        var result = editor.ExecuteCommand("2,3m0");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("two\nthree\none\nfour");
    }

    [Test]
    public async Task ExecuteCommand_ParsesCopyCommand()
    {
        // Verifies command parsing handles the classic `t` command by duplicating the addressed span after the destination.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        var result = editor.ExecuteCommand("1,2t4");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\ntwo\nthree\nfour\none\ntwo");
    }

    [Test]
    public async Task ExecuteCommand_ParsesMarkCommand()
    {
        // Verifies command parsing handles the classic `k` command by storing the addressed line under the requested mark.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three"]);

        var result = editor.ExecuteCommand("2ka");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.ResolveMark('a')).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteCommand_ParsesUndoCommand()
    {
        // Verifies command parsing handles the classic `u` command by restoring the prior buffer state.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);
        editor.ExecuteCommand("2d");

        var result = editor.ExecuteCommand("u");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\nbeta\ngamma");
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
    public async Task ExecuteCommand_ParsesVerboseErrorToggleCommand()
    {
        // Verifies command parsing handles the classic `H` command by toggling verbose error mode.
        var editor = EdEditorTestSupport.CreateEditor();

        var result = editor.ExecuteCommand("H");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.IsVerboseErrorsEnabled).IsTrue();
    }

    [Test]
    public async Task ExecuteCommand_ParsesHelpCommand()
    {
        // Verifies command parsing handles the classic `h` command by reporting the last editor error.
        var editor = EdEditorTestSupport.CreateEditor();

        try
        {
            editor.ExecuteCommand("bogus");
        }
        catch (NotSupportedException)
        {
        }

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("Unsupported command 'bogus'.");
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
    public async Task ExecuteCommand_ParsesGlobalNumberedPrintCommand()
    {
        // Verifies command parsing handles global commands whose command list uses numbered print output.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var result = editor.ExecuteCommand($"g/{searchCase.Pattern}/n");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo($"2\t{searchCase.Lines[1]}");
    }

    [Test]
    public async Task ExecuteCommand_ParsesGlobalSubstituteCommandList()
    {
        // Verifies command parsing handles classic global command lists that execute non-print, non-delete editor commands.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["match-one", "skip", "match-two"]);

        var result = editor.ExecuteCommand("g/match/s/match/replaced/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("replaced-one\nskip\nreplaced-two");
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
    public async Task ExecuteCommand_ParsesNonMatchingGlobalSubstituteCommand()
    {
        // Verifies command parsing handles non-matching global commands whose command list mutates each selected line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["keep-one", "skip", "keep-two"]);

        var result = editor.ExecuteCommand("v/skip/s/keep/replaced/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("replaced-one\nskip\nreplaced-two");
    }

    [Test]
    public async Task ExecuteCommand_ParsesScrollCommand()
    {
        // Verifies command parsing handles the classic `z` command by printing a window of lines.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        var result = editor.ExecuteCommand("2z2");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("two\nthree");
    }

    [Test]
    public async Task ExecuteCommand_RejectsGlobalLiteralPrintCommand()
    {
        // Verifies command parsing rejects global command lists that use the unsupported `l` command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "match\tbeta", "tail"]);

        await Assert.That(() => editor.ExecuteCommand("g/match/l")).Throws<NotSupportedException>();
    }

    [Test]
    public async Task ExecuteCommand_ParsesForceEditCommand()
    {
        // Verifies command parsing handles the classic `E` command by discarding modifications before loading a new file.
        var originalFile = EdEditorTestSupport.FileCaseAt(0);
        var replacementFile = EdEditorTestSupport.FileCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, originalFile);
        EdEditorTestSupport.SeedFile(fileSystem, replacementFile);
        editor.Edit(originalFile.Path);
        editor.Append(afterLine: null, ["dirty"]);

        var result = editor.ExecuteCommand($"E {replacementFile.Path}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(editor.CurrentFilePath).IsEqualTo(replacementFile.FullPath);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", replacementFile.Lines));
    }

    [Test]
    public async Task ExecuteCommand_ParsesForceQuitCommand()
    {
        // Verifies command parsing handles the classic `Q` command by closing a modified session without requiring a write.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("Q");

        await Assert.That(result.BufferChanged).IsFalse();

        var isClosed = false;

        try
        {
            editor.Print();
        }
        catch (InvalidOperationException)
        {
            isClosed = true;
        }

        await Assert.That(isClosed).IsTrue();
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
