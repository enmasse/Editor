using Ed;

namespace Ed.Tests;

public class EdEditorAddressingCoverageTests
{
    [Test]
    public async Task GetAddress_UsesCurrentLine_WhenArgumentIsOmitted()
    {
        // Verifies omitted explicit addresses resolve to the current line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three"]);

        var address = editor.GetAddress();

        await Assert.That(address).IsEqualTo(3);
    }

    [Test]
    public async Task Scroll_UsesConfiguredDefaultWindowSize_WhenCountIsOmitted()
    {
        // Verifies scroll falls back to the configured default window size when no explicit count is supplied.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.DefaultWindowSize = 2;
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        var scrolledLines = editor.Scroll(startLine: 2);

        await Assert.That(string.Join("\n", scrolledLines)).IsEqualTo("two\nthree");
    }

    [Test]
    public async Task Print_NumberedMode_PrefixesLineNumbers()
    {
        // Verifies numbered print mode prefixes each emitted line with its one-based line number.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta"]);

        var printedLines = editor.Print(mode: EdPrintMode.Numbered);

        await Assert.That(string.Join("\n", printedLines)).IsEqualTo("1\talpha\n2\tbeta");
    }

    [Test]
    public async Task Print_LiteralMode_EscapesTabs_AndMarksLineEnd()
    {
        // Verifies literal print mode emits escaped special characters and an explicit end-of-line marker.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha\tbeta"]);

        var printedLines = editor.Print(mode: EdPrintMode.Literal);

        await Assert.That(string.Join("\n", printedLines)).IsEqualTo("alpha\\tbeta$");
    }

    [Test]
    public async Task Search_Backward_FindsPreviousMatch()
    {
        // Verifies backward search locates the nearest preceding matching line.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        var matchLine = editor.Search(searchCase.Pattern, EdSearchDirection.Backward, startLine: 4);

        await Assert.That(matchLine).IsEqualTo(2);
    }

    [Test]
    public async Task Search_Forward_WrapsAroundBuffer()
    {
        // Verifies forward search wraps to the beginning of the buffer when no later match exists.
        var searchCase = EdEditorTestSupport.SearchCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [
            $"match-{searchCase.Pattern}",
            "filler",
            $"other-{searchCase.Pattern}"
        ]);

        var matchLine = editor.Search(searchCase.Pattern, EdSearchDirection.Forward, startLine: 3);

        await Assert.That(matchLine).IsEqualTo(1);
    }

    [Test]
    public async Task Substitute_ReplaceAllOnLine_ReplacesEveryOccurrence()
    {
        // Verifies substitute can replace every match on a line when the global option is requested.
        var substitutionCase = EdEditorTestSupport.SubstitutionCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [substitutionCase.SourceLine]);

        editor.Substitute(
            new EdLineRange(1, 1),
            substitutionCase.Pattern,
            substitutionCase.Replacement,
            new EdSubstitutionOptions(ReplaceAllOnLine: true));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo($"prefix-{substitutionCase.Replacement}-{substitutionCase.Replacement}-suffix");
    }

    [Test]
    public async Task Substitute_UsesRequestedOccurrence_WhenProvided()
    {
        // Verifies substitute can target a specific match occurrence instead of replacing every match.
        var substitutionCase = EdEditorTestSupport.SubstitutionCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, [substitutionCase.SourceLine]);

        editor.Substitute(
            new EdLineRange(1, 1),
            substitutionCase.Pattern,
            substitutionCase.Replacement,
            new EdSubstitutionOptions(Occurrence: 2));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo($"prefix-{substitutionCase.Pattern}-{substitutionCase.Replacement}-suffix");
    }

    [Test]
    public async Task Global_NonMatch_DeletesOnlyNonMatchingLines()
    {
        // Verifies non-matching global mode applies its command list only to lines that do not satisfy the pattern.
        var searchCase = EdEditorTestSupport.SearchCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, searchCase.Lines);

        editor.Global(new EdLineRange(1, searchCase.Lines.Count), searchCase.Pattern, "d", EdGlobalMode.NonMatch);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(searchCase.Lines[1]);
    }

    [Test]
    public async Task ExecuteCommand_ParsesCurrentLineAddressSymbol()
    {
        // Verifies command parsing handles `.` as the current line address.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand(".p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("gamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesLastLineAddressSymbol()
    {
        // Verifies command parsing handles `$` as the last line address.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("$p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("gamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesRelativeBackwardAddress()
    {
        // Verifies command parsing handles relative backward addresses from the current line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("-1p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesExplicitBasePlusOffsetAddress()
    {
        // Verifies command parsing handles offsets applied to an explicit base address such as `1+1`.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("1+1p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesMarkAddress()
    {
        // Verifies command parsing handles mark-based addresses such as `'a`.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);
        editor.SetMark('a', 2);

        var result = editor.ExecuteCommand("'ap");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesForwardSearchAddress()
    {
        // Verifies command parsing handles forward-search addresses used before another editor command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("/beta/p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesBackwardSearchAddress()
    {
        // Verifies command parsing handles backward-search addresses used before another editor command.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("?beta?p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesWholeBufferShorthandRange()
    {
        // Verifies command parsing handles the classic `%` shorthand as the whole-buffer range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("%p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta\ngamma");
    }

    [Test]
    public async Task ExecuteCommand_ParsesSemicolonSeparatedRelativeRange()
    {
        // Verifies command parsing handles `;` ranges that reset the current line before resolving the next address.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);

        var result = editor.ExecuteCommand("1;+1p");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesRepeatedForwardSearchPattern()
    {
        // Verifies command parsing treats an empty forward search as reuse of the previous search pattern.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);
        editor.ExecuteCommand("/beta/");

        var result = editor.ExecuteCommand("//");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }

    [Test]
    public async Task ExecuteCommand_ParsesRepeatedBackwardSearchPattern()
    {
        // Verifies command parsing treats an empty backward search as reuse of the previous search pattern.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma"]);
        editor.ExecuteCommand("?beta?");

        var result = editor.ExecuteCommand("??");

        await Assert.That(result.BufferChanged).IsFalse();
        await Assert.That(string.Join("\n", result.Output)).IsEqualTo("beta");
    }
}
