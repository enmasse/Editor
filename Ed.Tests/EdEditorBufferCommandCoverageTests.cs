using Ed;

namespace Ed.Tests;

public class EdEditorBufferCommandCoverageTests
{
    [Test]
    public async Task Append_AfterExplicitAddress_InsertsBetweenExistingLines()
    {
        // Verifies append inserts new lines after the addressed line rather than only at the buffer end.
        var editor = EdEditorTestSupport.CreateEditor();
        var insertedLines = EdEditorTestSupport.LineSetAt(0);
        editor.Append(afterLine: null, ["first", "last"]);

        editor.Append(afterLine: 1, insertedLines);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", new[] { "first" }.Concat(insertedLines).Concat(["last"])));
    }

    [Test]
    public async Task Insert_BeforeFirstLine_ShiftsBufferDown()
    {
        // Verifies insert can place new lines before line one and retain the existing trailing content.
        var editor = EdEditorTestSupport.CreateEditor();
        var insertedLines = EdEditorTestSupport.LineSetAt(1);
        editor.Append(afterLine: null, ["tail"]);

        editor.Insert(beforeLine: 1, insertedLines);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", insertedLines.Concat(["tail"])));
    }

    [Test]
    public async Task Change_WithEmptyReplacement_RemovesSelectedRange()
    {
        // Verifies change with no replacement lines behaves like deleting the addressed range.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["first", "second", "third"]);

        editor.Change(new EdLineRange(2, 2), []);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("first\nthird");
    }

    [Test]
    public async Task Delete_RemovesMultiLineSpan_AndKeepsSurroundingLines()
    {
        // Verifies delete removes an entire addressed span while preserving lines outside the span.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["zero", "one", "two", "three", "four"]);

        editor.Delete(new EdLineRange(2, 4));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("zero\nfour");
    }

    [Test]
    public async Task Join_ThreeLines_ProducesSingleMergedLine()
    {
        // Verifies join can collapse more than two consecutive lines into one merged line.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["alpha", "beta", "gamma", "delta"]);

        editor.Join(new EdLineRange(1, 3));

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("alphabetagamma\ndelta");
    }

    [Test]
    public async Task Move_BlockToBeginning_PreservesMovedOrder()
    {
        // Verifies move keeps the block order intact when relocating a span to the buffer front.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        editor.Move(new EdLineRange(3, 4), destinationLine: 0);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("three\nfour\none\ntwo");
    }

    [Test]
    public async Task Copy_BlockToEnd_PreservesOriginalAndCopyOrder()
    {
        // Verifies copy leaves the original span in place and appends an ordered duplicate after the destination.
        var editor = EdEditorTestSupport.CreateEditor();
        editor.Append(afterLine: null, ["one", "two", "three", "four"]);

        editor.Copy(new EdLineRange(1, 2), destinationLine: 4);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\ntwo\nthree\nfour\none\ntwo");
    }

    [Test]
    public async Task Undo_AfterChange_RestoresPriorBuffer()
    {
        // Verifies undo restores the exact buffer contents that existed before a change command.
        var editor = EdEditorTestSupport.CreateEditor();
        var replacementLines = EdEditorTestSupport.LineSetAt(2);
        editor.Append(afterLine: null, ["one", "two", "three"]);
        editor.Change(new EdLineRange(2, 2), replacementLines);

        editor.Undo();

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("one\ntwo\nthree");
    }
}
