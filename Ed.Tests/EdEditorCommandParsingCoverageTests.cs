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
