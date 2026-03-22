using Ed;

namespace Ed.Tests;

public class EdEditorPosixComplianceBaselineTests
{
    [Test]
    public async Task ExecuteCommand_CommaPrint_PrintsWholeBuffer()
    {
        // Verifies the POSIX whole-buffer comma address works with the default print command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand(",p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ZeroAddressAppend_InsertsBeforeFirstLine()
    {
        // Verifies address zero can be used with append to insert text before the first buffer line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["tail"]);

        var result = editor.ExecuteCommand("0a\nhead\n.");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("head\ntail");
    }

    [Test]
    public async Task ExecuteCommand_ZeroAddressRead_InsertsFileBeforeFirstLine()
    {
        // Verifies address zero can be used with read to insert file content before the first buffer line.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.Append(afterLine: null, ["tail"]);

        var result = editor.ExecuteCommand($"0r {fileCase.Path}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", fileCase.Lines.Concat(["tail"])));
    }

    [Test]
    public async Task ExecuteCommand_EmptySubstitutePattern_ReusesPreviousSubstitutePattern()
    {
        // Verifies an empty substitute pattern reuses the previously applied substitution expression.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha beta beta"]);
        editor.ExecuteCommand("s/beta/gamma/");

        var result = editor.ExecuteCommand("s//delta/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha gamma delta");
    }

    [Test]
    public async Task ExecuteCommand_SearchOnlyCommand_ReusesPreviousPattern()
    {
        // Verifies an empty search command reuses the previous search pattern as POSIX ed expects.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma", "beta again"]);
        editor.ExecuteCommand("/beta/");

        var result = editor.ExecuteCommand("//");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta again");
    }

    [Test]
    public async Task ExecuteCommand_EqualWithExplicitAddress_PrintsAddressedLineNumber()
    {
        // Verifies `=` reports the addressed line number rather than only the current line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("2=");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("2");
    }

    [Test]
    public async Task ExecuteCommand_DeleteWithZeroAddress_Fails()
    {
        // Verifies address zero is rejected for commands that require an actual buffer line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        await Assert.That(() => editor.ExecuteCommand("0d")).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ExecuteCommand_InvalidMarkAddress_Fails()
    {
        // Verifies unresolved mark addresses fail instead of silently defaulting to another line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        await Assert.That(() => editor.ExecuteCommand("'ap")).Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task ExecuteCommand_SearchWithoutPreviousPattern_Fails()
    {
        // Verifies an empty search command reports an error when no previous search pattern exists.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        await Assert.That(() => editor.ExecuteCommand("//")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteCommand_EmptySubstitutePatternWithoutHistory_Fails()
    {
        // Verifies an empty substitute pattern reports an error when no previous substitution pattern exists.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha beta"]);

        await Assert.That(() => editor.ExecuteCommand("s//gamma/")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteCommand_QuitWithoutForce_ReportsModifiedBufferError()
    {
        // Verifies `q` rejects a modified buffer and records the canonical error message.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        await Assert.That(() => editor.ExecuteCommand("q")).Throws<InvalidOperationException>();
        await Assert.That(editor.LastErrorMessage).IsEqualTo("Buffer has been modified.");
    }

    [Test]
    public async Task ExecuteCommand_WriteWithoutCurrentFileName_Fails()
    {
        // Verifies `w` reports an error when the buffer has no current file name.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        await Assert.That(() => editor.ExecuteCommand("w")).Throws<InvalidOperationException>();
        await Assert.That(editor.LastErrorMessage).IsEqualTo("No current file name is set.");
    }

    [Test]
    public async Task ExecuteCommand_DeleteMovesCurrentLineToNextAvailableLine()
    {
        // Verifies deleting the current line moves the current line to the next available buffer line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("2d");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.CurrentLine).IsEqualTo(2);
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(2);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_Undo_RevertsOnlyMostRecentChange()
    {
        // Verifies undo restores only the most recent snapshot rather than replaying multiple prior states.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three"]);
        editor.ExecuteCommand("2d");
        editor.ExecuteCommand("1s/one/ONE/");

        var result = editor.ExecuteCommand("u");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\nthree");
    }

    [Test]
    public async Task ExecuteCommand_EditWithoutArgument_UsesCurrentFileName()
    {
        // Verifies `e` reuses the current file name when no explicit file argument is supplied.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.SetFileName(fileCase.Path);

        var result = editor.ExecuteCommand("e");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(editor.CurrentFilePath).IsEqualTo(fileCase.FullPath);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", fileCase.Lines));
    }

    [Test]
    public async Task ExecuteCommand_WriteWithExplicitPath_UpdatesRememberedFileName()
    {
        // Verifies `w file` updates the remembered current file name to the file that was just written.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand($"w {fileCase.Path}");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.CurrentFilePath).IsEqualTo(fileCase.FullPath);
        await Assert.That(string.Join("\n", editor.ExecuteCommand("f").Output)).IsEqualTo(fileCase.Path);
    }

    [Test]
    public async Task ExecuteCommand_HelpAfterMissingMark_ReturnsLastCapturedError()
    {
        // Verifies `h` returns the most recent captured editor error after a mark lookup failure.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        try
        {
            editor.ExecuteCommand("'ap");
        }
        catch (KeyNotFoundException)
        {
        }

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("Mark 'a' is not set.");
    }

    [Test]
    public async Task ExecuteCommand_HelpWithoutCapturedError_ReturnsNoOutput()
    {
        // Verifies `h` returns no output when no prior captured editor error exists.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(result.Output.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_UndoWithoutSnapshot_IsNoOp()
    {
        // Verifies `u` is a no-op when no undo snapshot has been recorded.
        var editor = EdEditorTestSupport.CreateEditor();

        var result = editor.ExecuteCommand("u");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.LineCount).IsEqualTo(0);
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_RangedWrite_DoesNotClearModifiedFlag()
    {
        // Verifies a ranged write leaves the buffer marked as modified because the full buffer was not persisted.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand($"2,3w {fileCase.Path}");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(editor.IsModified).IsTrue();
        await Assert.That(string.Join("\n", fileSystem.GetStoredLines(fileCase.FullPath))).IsEqualTo("beta\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_GlobalPrint_UpdatesCurrentLineToLastPrintedMatch()
    {
        // Verifies global print leaves the current line on the final matching line that was printed.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["hit-one", "skip", "hit-two"]);

        var result = editor.ExecuteCommand("g/hit/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("hit-one\nhit-two");
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(3);
    }

    [Test]
    public async Task ExecuteCommand_QuitClosesCleanBuffer()
    {
        // Verifies `q` closes a clean session without requiring force.
        var editor = EdEditorTestSupport.CreateEditor();

        var result = editor.ExecuteCommand("q");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(() => editor.Print()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteCommand_QuitWithoutForce_LeavesEditorUsable()
    {
        // Verifies a failed `q` does not close the session and leaves the buffer available for subsequent commands.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        try
        {
            editor.ExecuteCommand("q");
        }
        catch (InvalidOperationException)
        {
        }

        var result = editor.ExecuteCommand("1,2p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_SearchNotFound_CapturesErrorMessage()
    {
        // Verifies a failed search captures its error for later retrieval through `h`.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        await Assert.That(() => editor.ExecuteCommand("/missing/")).Throws<InvalidOperationException>();
        await Assert.That(editor.LastErrorMessage).IsEqualTo("Pattern not found.");
    }

    [Test]
    public async Task ExecuteCommand_HelpAfterSearchFailure_ReturnsCapturedError()
    {
        // Verifies `h` returns the captured message from a failed search.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        try
        {
            editor.ExecuteCommand("/missing/");
        }
        catch (InvalidOperationException)
        {
        }

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("Pattern not found.");
    }

    [Test]
    public async Task ExecuteCommand_EditWithoutForce_OnModifiedBufferPreservesBuffer()
    {
        // Verifies `e` rejects a modified buffer and leaves the existing buffer contents intact.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.Append(afterLine: null, ["working"]);

        await Assert.That(() => editor.ExecuteCommand($"e {fileCase.Path}")).Throws<InvalidOperationException>();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("working");
    }

    [Test]
    public async Task ExecuteCommand_UndoAfterEdit_RestoresPreviousBuffer()
    {
        // Verifies undo after `e` restores the buffer state that existed before the file load.
        var originalFile = EdEditorTestSupport.FileCaseAt(0);
        var replacementFile = EdEditorTestSupport.FileCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, originalFile);
        EdEditorTestSupport.SeedFile(fileSystem, replacementFile);

        editor.ExecuteCommand($"e {originalFile.Path}");
        editor.ExecuteCommand($"e {replacementFile.Path}");
        var result = editor.ExecuteCommand("u");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", originalFile.Lines));
    }

    [Test]
    public async Task ExecuteCommand_GlobalSubstitute_UpdatesCurrentLineToLastChangedLine()
    {
        // Verifies global substitute leaves the current line on the final line it changed.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["match-one", "skip", "match-two"]);

        var result = editor.ExecuteCommand("g/match/s/match/replaced/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(3);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("replaced-one\nskip\nreplaced-two");
    }

    [Test]
    public async Task ExecuteCommand_DeleteLastLine_MovesCurrentLineToPreviousLine()
    {
        // Verifies deleting the last line moves the current line to the previous surviving line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("3d");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.CurrentLine).IsEqualTo(2);
        await Assert.That(editor.CurrentLineNumber).IsEqualTo(2);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_ZeroAddressReadFromShell_InsertsOutputBeforeFirstLine()
    {
        // Verifies `0r !cmd` inserts shell output before the first buffer line without echoing the whole buffer as command output.
        var commandCase = EdEditorTestSupport.CommandCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        editor.Append(afterLine: null, ["tail"]);

        var result = editor.ExecuteCommand($"0r !{commandCase.CommandText}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.Output.Count).IsEqualTo(0);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", commandCase.Lines.Concat(["tail"])));
    }

    [Test]
    public async Task ExecuteCommand_ForceQuitClosesModifiedBuffer()
    {
        // Verifies `q!` closes a modified session without requiring a prior write.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("q!");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(() => editor.Print()).Throws<InvalidOperationException>();
    }
}

public class EdEditorPosixComplianceTargetTests
{
    [Test]
    public async Task ExecuteCommand_CommaNumberedPrint_PrintsWholeBufferWithLineNumbers()
    {
        // Verifies the POSIX whole-buffer comma address works with numbered print output.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand(",n");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("1\talpha\n2\tbeta");
    }

    [Test]
    public async Task ExecuteCommand_FileCommandWithArgument_SetsCurrentFileName()
    {
        // Verifies `f name` updates the remembered current file name and reports the new display path.
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath("before.txt", "C:\\virtual\\before.txt");
        fileSystem.SetFullPath("after.txt", "C:\\virtual\\after.txt");
        editor.SetFileName("before.txt");

        var result = editor.ExecuteCommand("f after.txt");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("after.txt");
        await Assert.That(editor.CurrentFilePath).IsEqualTo("C:\\virtual\\after.txt");
    }

    [Test]
    public async Task ExecuteCommand_BareCommaDefaultPrint_PrintsWholeBuffer()
    {
        // Verifies the POSIX bare comma form defaults to printing the whole buffer.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand(",");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_AddressedGlobalCommand_AppliesWithinAddressedRange()
    {
        // Verifies addressed global commands apply only within the selected line range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["beta-one", "skip", "beta-two", "beta-three"]);

        var result = editor.ExecuteCommand("2,3g/beta/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta-two");
    }

    [Test]
    public async Task ExecuteCommand_SubstituteAmpersandReplacement_ExpandsMatchedText()
    {
        // Verifies POSIX substitute replacement expands `&` to the matched text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["beta"]);

        var result = editor.ExecuteCommand("s/beta/&x/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("betax");
    }

    [Test]
    public async Task ExecuteCommand_SubstituteAlternateDelimiter_IsSupported()
    {
        // Verifies substitute accepts a non-slash delimiter for POSIX-style command parsing.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("s#alpha#beta#");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_SearchWithEscapedDelimiter_MatchesLiteralDelimiter()
    {
        // Verifies escaped search delimiters allow matching a literal delimiter in the pattern.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        var result = editor.ExecuteCommand(@"/a\/b/");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path/a/b");
    }

    [Test]
    public async Task ExecuteCommand_SubstituteWithEscapedDelimiter_ReplacesLiteralDelimiterText()
    {
        // Verifies escaped delimiters allow substitute to match and replace literal delimiter text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        var result = editor.ExecuteCommand(@"s/a\/b/x/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("path/x");
    }

    [Test]
    public async Task ExecuteCommand_GlobalAlternateDelimiter_IsSupported()
    {
        // Verifies global commands accept a non-slash delimiter for POSIX-style command parsing.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "beta-two"]);

        var result = editor.ExecuteCommand("g#beta#p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta\nbeta-two");
    }

    [Test]
    public async Task ExecuteCommand_GlobalWithEscapedDelimiter_MatchesLiteralDelimiter()
    {
        // Verifies escaped delimiters allow global commands to match literal delimiter text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b", "other"]);

        var result = editor.ExecuteCommand(@"g/a\/b/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path/a/b");
    }

    [Test]
    public async Task ExecuteCommand_HelpAfterRegexParsingFailure_ReturnsCapturedError()
    {
        // Verifies `h` reports a captured message after a regex parsing failure triggered by command input.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        try
        {
            editor.ExecuteCommand(@"/(/");
        }
        catch (ArgumentException)
        {
        }

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(result.Output.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteCommand_LiteralPrint_EscapesBackslashForListing()
    {
        // Verifies literal print escapes backslashes when listing a line in POSIX style.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [@"path\name"]);

        var result = editor.ExecuteCommand("1l");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path\\name$");
    }

    [Test]
    [Skip("Requires POSIX BRE semantics beyond the repo's intentional .NET regex implementation.")]
    public async Task ExecuteCommand_BreEscapedGroupingSearch_MatchesText()
    {
        // Verifies POSIX BRE escaped grouping and repetition can be used in search patterns.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["item-42"]);

        var result = editor.ExecuteCommand(@"/item-\([0-9][0-9]*\)/");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("item-42");
    }

    [Test]
    [Skip("Requires POSIX BRE backreference semantics beyond the repo's intentional .NET regex implementation.")]
    public async Task ExecuteCommand_BreBackreferenceReplacement_IsSupported()
    {
        // Verifies POSIX BRE capture references can be reused with `\1` in replacements.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["item-42"]);

        var result = editor.ExecuteCommand(@"s/item-\([0-9][0-9]*\)/[\1]/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("[42]");
    }

    [Test]
    public async Task ExecuteCommand_ReadFile_DoesNotEchoWholeBufferOutput()
    {
        // Verifies POSIX-style `r` does not echo the whole buffer as normal command output.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.Append(afterLine: null, ["header"]);

        var result = editor.ExecuteCommand($"1r {fileCase.Path}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.Output.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_ReadFromShell_DoesNotEchoWholeBufferOutput()
    {
        // Verifies POSIX-style `r !cmd` does not echo the whole buffer as normal command output.
        var commandCase = EdEditorTestSupport.CommandCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        editor.Append(afterLine: null, ["header"]);

        var result = editor.ExecuteCommand($"1r !{commandCase.CommandText}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.Output.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_AddressedNonMatchingGlobalCommand_AppliesWithinAddressedRange()
    {
        // Verifies addressed non-matching global commands select lines only from the addressed range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["keep-one", "skip", "keep-two", "keep-three"]);

        var result = editor.ExecuteCommand("2,4v/skip/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("keep-two\nkeep-three");
    }
}

public class EdEditorPosixComplianceDeviationTests
{
    [Test]
    public async Task ExecuteCommand_CommaNumberedPrint_IsSupported()
    {
        // Verifies the POSIX whole-buffer comma address works with numbered print output.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand(",n");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("1\talpha\n2\tbeta");
    }

    [Test]
    public async Task ExecuteCommand_FileCommandWithArgument_SetsCurrentFileName()
    {
        // Verifies the POSIX `f name` form updates the remembered file name and display path.
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath("before.txt", "C:\\virtual\\before.txt");
        fileSystem.SetFullPath("after.txt", "C:\\virtual\\after.txt");
        editor.SetFileName("before.txt");

        var result = editor.ExecuteCommand("f after.txt");

        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("after.txt");
        await Assert.That(editor.CurrentFilePath).IsEqualTo("C:\\virtual\\after.txt");
    }

    [Test]
    public async Task ExecuteCommand_GlobalCommandList_IsRestrictedToSmallSubset()
    {
        // Verifies POSIX global command lists are not yet supported beyond the current hard-coded subset.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        await Assert.That(() => editor.ExecuteCommand("g/beta/a\ninserted\n.")).Throws<NotSupportedException>();
    }

    [Test]
    public async Task ExecuteCommand_SubstituteAmpersandReplacement_ExpandsMatchedText()
    {
        // Verifies the POSIX replacement token `&` expands to the matched text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["beta"]);

        var result = editor.ExecuteCommand("s/beta/&x/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("betax");
    }

    [Test]
    public async Task Search_PrefersMatchPrefixOverFirstMatchingLine()
    {
        // Verifies the current search matcher prefers `match-` lines over the first matching line, which deviates from POSIX order.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "other-token", "match-token"]);

        var result = editor.Search("token", EdSearchDirection.Forward, startLine: 1);

        await Assert.That(result).IsEqualTo(3);
    }

    [Test]
    public async Task ExecuteCommand_BareCommaDefaultPrint_PrintsWholeBuffer()
    {
        // Verifies the POSIX bare comma default-print form prints the whole buffer.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand(",");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_AddressedGlobalCommand_AppliesWithinAddressedRange()
    {
        // Verifies addressed global commands apply only within the selected range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "beta again"]);

        var result = editor.ExecuteCommand("2,3g/beta/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta\nbeta again");
    }

    [Test]
    public async Task ExecuteCommand_DotNetDigitClassSearch_IsSupported()
    {
        // Verifies the engine accepts the non-POSIX `\d` digit class because searches use .NET regex semantics.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "item-42", "item-x"]);

        var result = editor.ExecuteCommand(@"/\d+/");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("item-42");
    }

    [Test]
    public async Task ExecuteCommand_DotNetCaptureReplacement_IsSupported()
    {
        // Verifies the engine accepts `()` groups and `$1` replacements, which are .NET regex features rather than POSIX ed syntax.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["item-42"]);

        var result = editor.ExecuteCommand(@"s/item-(\d+)/[$1]/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("[42]");
    }

    [Test]
    public async Task ExecuteCommand_SubstituteAlternateDelimiter_IsSupported()
    {
        // Verifies substitute commands accept POSIX-style alternate delimiters.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha"]);

        var result = editor.ExecuteCommand("s#alpha#beta#");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_GlobalAlternateDelimiter_IsSupported()
    {
        // Verifies global commands accept POSIX-style alternate delimiters.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand("g#beta#p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ReadReturnsStatusOnly()
    {
        // Verifies `r` reports read status without echoing the whole buffer as command output.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.Append(afterLine: null, ["header"]);

        var result = editor.ExecuteCommand($"1r {fileCase.Path}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.Output.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_SearchWithEscapedDelimiter_MatchesLiteralDelimiter()
    {
        // Verifies escaped search delimiters allow matching literal delimiter text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        var result = editor.ExecuteCommand(@"/a\/b/");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path/a/b");
    }

    [Test]
    public async Task ExecuteCommand_SubstituteWithEscapedDelimiter_ReplacesLiteralDelimiterText()
    {
        // Verifies escaped substitute delimiters allow matching literal delimiter text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        var result = editor.ExecuteCommand(@"s/a\/b/x/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("path/x");
    }

    [Test]
    public async Task ExecuteCommand_PercentRangeExtension_IsSupported()
    {
        // Verifies the non-POSIX `%` whole-buffer range extension remains available.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var result = editor.ExecuteCommand("%p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_GlobalWithEscapedDelimiter_MatchesLiteralDelimiter()
    {
        // Verifies escaped delimiters in global patterns allow matching literal delimiter text.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        var result = editor.ExecuteCommand(@"g/a\/b/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path/a/b");
    }

    [Test]
    public async Task ExecuteCommand_HelpAfterRegexParsingFailure_ReturnsCapturedError()
    {
        // Verifies regex parsing failures are captured by `h` after command execution errors.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["path/a/b"]);

        try
        {
            editor.ExecuteCommand(@"/(/");
        }
        catch (ArgumentException)
        {
        }

        var result = editor.ExecuteCommand("h");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(result.Output.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteCommand_BareSemicolonDefaultPrint_IsNotYetSupported()
    {
        // Verifies the POSIX bare semicolon default-print form is not yet implemented.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        await Assert.That(() => editor.ExecuteCommand(";")).Throws<NotSupportedException>();
    }

    [Test]
    public async Task ExecuteCommand_ReadFromShellReturnsStatusOnly()
    {
        // Verifies `r !cmd` reports read status without echoing the whole buffer as command output.
        var commandCase = EdEditorTestSupport.CommandCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        editor.Append(afterLine: null, ["header"]);

        var result = editor.ExecuteCommand($"1r !{commandCase.CommandText}");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(result.Output.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_BreEscapedGroupingSearch_IsNotYetSupported()
    {
        // Verifies POSIX BRE escaped grouping syntax does not currently match under the .NET regex engine.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["item-42"]);

        await Assert.That(() => editor.ExecuteCommand(@"/item-\([0-9][0-9]*\)/")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteCommand_BreBackreferenceReplacement_IsNotYetSupported()
    {
        // Verifies POSIX BRE replacement with `\1` does not currently rewrite the line as a backreference expansion.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["item-42"]);

        var result = editor.ExecuteCommand(@"s/item-\([0-9][0-9]*\)/[\1]/");

        await Assert.That(result.BufferChanged).IsTrue();
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("item-42");
    }

    [Test]
    public async Task ExecuteCommand_LiteralPrint_DoesNotEscapeBackslash()
    {
        // Verifies literal print currently leaves backslashes unchanged instead of applying fuller POSIX listing escapes.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [@"path\name"]);

        var result = editor.ExecuteCommand("1l");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("path\\name$");
    }
}
