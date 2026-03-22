using Ed;

namespace Ed.Tests;

public class EdEditorAbstractionIntegrationTests
{
    [Test]
    public async Task Edit_Append_Write_RoundTripsThroughConcreteFileSystem()
    {
        // Verifies the editor can load, modify, and persist a file through the concrete filesystem abstraction.
        using var sandbox = TemporaryDirectory.Create("EdEditorAbstractionIntegrationTests");
        var editor = new EdEditor(new EdFileSystem(), new EdShell());
        var path = Path.Combine(sandbox.DirectoryPath, ".", "buffer.txt");
        File.WriteAllLines(path, ["alpha", "beta"]);

        editor.Edit(path);
        editor.Append(afterLine: null, ["gamma"]);
        editor.Write();

        await Assert.That(editor.CurrentFilePath).IsEqualTo(Path.GetFullPath(path));
        await Assert.That(File.ReadAllLines(Path.GetFullPath(path))).IsEquivalentTo(["alpha", "beta", "gamma"]);
    }

    [Test]
    public async Task ReadCommandOutput_And_WriteToCommand_RoundTripThroughConcreteShell()
    {
        // Verifies the editor can consume shell output and pipe selected buffer lines back through the concrete shell abstraction.
        using var sandbox = TemporaryDirectory.Create("EdEditorAbstractionIntegrationTests");
        var editor = new EdEditor(new EdFileSystem(), new EdShell());
        var outputPath = Path.Combine(sandbox.DirectoryPath, "captured.txt");

        editor.Append(afterLine: null, ["header"]);
        editor.ReadCommandOutput("Write-Output 'middle'; Write-Output 'tail'", afterLine: 1);
        editor.WriteToCommand(
            $"[Console]::In.ReadToEnd() | Set-Content -Path {ToPowerShellLiteral(outputPath)} -NoNewline",
            new EdLineRange(2, 3));

        await Assert.That(editor.Print()).IsEquivalentTo(["header", "middle", "tail"]);
        await Assert.That(File.ReadAllText(outputPath)).IsEqualTo(string.Join(Environment.NewLine, ["middle", "tail"]));
    }

    [Test]
    public async Task ExecuteCommand_UsesConcreteAbstractions_ForFileAndShellFlows()
    {
        // Verifies command execution can coordinate concrete file and shell abstractions in one end-to-end flow while shell reads remain status-only.
        using var sandbox = TemporaryDirectory.Create("EdEditorAbstractionIntegrationTests");
        var editor = new EdEditor(new EdFileSystem(), new EdShell());
        var inputPath = Path.Combine(sandbox.DirectoryPath, "input.txt");
        var outputPath = Path.Combine(sandbox.DirectoryPath, "output.txt");
        File.WriteAllLines(inputPath, ["one"]);

        var editResult = editor.ExecuteCommand($"e {inputPath}");
        var readResult = editor.ExecuteCommand("$r !Write-Output 'two'; Write-Output 'three'");
        var writeResult = editor.ExecuteCommand($"w {outputPath}");

        await Assert.That(editResult.BufferChanged).IsTrue();
        await Assert.That(readResult.BufferChanged).IsTrue();
        await Assert.That(readResult.Output.Count).IsEqualTo(0);
        await Assert.That(writeResult.BufferChanged).IsFalse();
        await Assert.That(File.ReadAllLines(outputPath)).IsEquivalentTo(["one", "two", "three"]);
    }

    private static string ToPowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string DirectoryPath { get; }

        private TemporaryDirectory(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public static TemporaryDirectory Create(string scope)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), scope, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new TemporaryDirectory(directoryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
