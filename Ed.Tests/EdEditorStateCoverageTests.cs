using Ed;

namespace Ed.Tests;

public class EdEditorStateCoverageTests
{
    [Test]
    public async Task Append_MarksBufferAsModified()
    {
        // Verifies mutating the buffer through append marks the session as modified.
        var editor = EdEditorTestSupport.CreateEditor();

        editor.Append(afterLine: null, EdEditorTestSupport.LineSetAt(0));

        await Assert.That(editor.IsModified).IsTrue();
    }

    [Test]
    public async Task Read_MarksBufferAsModified()
    {
        // Verifies reading file content into the buffer marks the session as modified.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);

        editor.Read(fileCase.Path);

        await Assert.That(editor.IsModified).IsTrue();
    }

    [Test]
    public async Task Write_ClearsModifiedFlag_AfterPersistingBuffer()
    {
        // Verifies writing the buffer clears the modified state once the current contents are persisted.
        var fileCase = EdEditorTestSupport.FileCaseAt(0);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        fileSystem.SetFullPath(fileCase.Path, fileCase.FullPath);
        editor.Append(afterLine: null, EdEditorTestSupport.LineSetAt(1));

        editor.Write(fileCase.Path);

        await Assert.That(editor.IsModified).IsFalse();
    }

    [Test]
    public async Task Edit_SetsCurrentLineToLastLoadedLine()
    {
        // Verifies editing a file moves the current line to the final line loaded into the buffer.
        var fileCase = EdEditorTestSupport.FileCaseAt(1);
        var editor = EdEditorTestSupport.CreateEditor(out var fileSystem, out _);
        EdEditorTestSupport.SeedFile(fileSystem, fileCase);

        editor.Edit(fileCase.Path);

        await Assert.That(editor.CurrentLineNumber).IsEqualTo(fileCase.Lines.Count);
    }
}
