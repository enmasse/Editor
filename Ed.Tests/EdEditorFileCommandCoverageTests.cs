using Ed;

namespace Ed.Tests;

public class EdEditorFileCommandCoverageTests
{
    [Test]
    public async Task Edit_Force_ReplacesModifiedBuffer_WithReplacementFile()
    {
        // Verifies forced edit discards modified buffer contents and loads the requested file.
        var originalFile = EdEditorTestSupport.FileCaseAt(0);
        var replacementFile = EdEditorTestSupport.FileCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, originalFile);
        EdEditorTestSupport.SeedFile(fileSystem, replacementFile);
        editor.Edit(originalFile.Path);
        editor.Append(afterLine: null, EdEditorTestSupport.LineSetAt(2));

        editor.Edit(replacementFile.Path, force: true);

        await Assert.That(editor.CurrentFilePath).IsEqualTo(replacementFile.FullPath);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", replacementFile.Lines));
    }

    [Test]
    public async Task Edit_NormalizesPath_BeforeReadingFile()
    {
        // Verifies edit consults the injected file system for path normalization before reading file content.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);

        editor.Edit(fileCase.Path);

        await Assert.That(fileSystem.FullPathRequests.Count).IsEqualTo(1);
        await Assert.That(fileSystem.FullPathRequests[0]).IsEqualTo(fileCase.Path);
        await Assert.That(fileSystem.ReadRequests[^1]).IsEqualTo(fileCase.FullPath);
    }

    [Test]
    public async Task Read_InsertsFileContentAtStart_WhenAddressIsZero()
    {
        // Verifies file reads can insert content before the first buffer line using address zero.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.Append(afterLine: null, ["tail"]);

        editor.Read(fileCase.Path, afterLine: 0);

        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo(string.Join("\n", fileCase.Lines.Concat(["tail"])));
    }

    [Test]
    public async Task ReadCommandOutput_InsertsShellOutputAtCurrentLine_WhenAddressIsOmitted()
    {
        // Verifies shell read commands insert output after the current line when no explicit address is provided.
        var commandCase = EdEditorTestSupport.CommandCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        shell.SeedOutput(commandCase.CommandText, commandCase.Lines);
        editor.Append(afterLine: null, ["header", "middle"]);

        editor.ReadCommandOutput(commandCase.CommandText);

        await Assert.That(shell.OutputRequests.Count).IsEqualTo(1);
        await Assert.That(shell.OutputRequests[0]).IsEqualTo(commandCase.CommandText);
        await Assert.That(string.Join("\n", editor.Print())).IsEqualTo("header\nmiddle\n" + string.Join("\n", commandCase.Lines));
    }

    [Test]
    public async Task Write_UsesCurrentFile_WhenPathIsOmitted()
    {
        // Verifies write falls back to the current file name when no explicit path is provided.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);
        editor.SetFileName(fileCase.Path);
        editor.Append(afterLine: null, EdEditorTestSupport.LineSetAt(1));

        editor.Write();

        await Assert.That(fileSystem.LastWritePath).IsEqualTo(fileCase.FullPath);
        await Assert.That(string.Join("\n", fileSystem.GetStoredLines(fileCase.FullPath))).IsEqualTo(string.Join("\n", EdEditorTestSupport.LineSetAt(1)));
    }

    [Test]
    public async Task Write_AppendsSelectedRange_WhenAppendModeRequested()
    {
        // Verifies append write mode preserves existing file content and adds only the selected range.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SeedFile(fileCase.FullPath, ["existing"]);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        editor.Append(afterLine: null, ["first", "second", "third"]);

        editor.Write(fileCase.Path, new EdLineRange(2, 3), EdWriteMode.Append);

        await Assert.That(fileSystem.LastAppendPath).IsEqualTo(fileCase.FullPath);
        await Assert.That(string.Join("\n", fileSystem.GetStoredLines(fileCase.FullPath))).IsEqualTo("existing\nsecond\nthird");
    }

    [Test]
    public async Task Write_PersistsOnlyRequestedRange()
    {
        // Verifies ranged writes persist only the addressed lines instead of the entire buffer.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        editor.Append(afterLine: null, ["first", "second", "third"]);

        editor.Write(fileCase.Path, new EdLineRange(2, 3));

        await Assert.That(string.Join("\n", fileSystem.GetStoredLines(fileCase.FullPath))).IsEqualTo("second\nthird");
    }

    [Test]
    public async Task WriteToCommand_WritesOnlyRequestedRange()
    {
        // Verifies shell writes can be limited to the addressed subset of buffer lines.
        var commandCase = EdEditorTestSupport.CommandCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out _, out var shell);
        editor.Append(afterLine: null, ["first", "second", "third"]);

        editor.WriteToCommand(commandCase.CommandText, new EdLineRange(2, 3));

        await Assert.That(shell.Writes.Count).IsEqualTo(1);
        await Assert.That(shell.Writes[0].CommandText).IsEqualTo(commandCase.CommandText);
        await Assert.That(string.Join("\n", shell.Writes[0].Lines)).IsEqualTo("second\nthird");
    }
}
