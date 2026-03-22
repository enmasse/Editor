using Ed;

namespace Ed.Tests;

public class EdShellTests
{
    [Test]
    public async Task ReadCommandOutput_ReturnsStandardOutputLines()
    {
        // Verifies the concrete shell captures standard output as ordered lines.
        var shell = new EdShell();

        var output = shell.ReadCommandOutput("Write-Output 'alpha'; Write-Output 'beta'");

        await Assert.That(string.Join("\n", output)).IsEqualTo("alpha\nbeta");
    }

    [Test]
    public async Task Execute_RunsCommand()
    {
        // Verifies the concrete shell executes a command that can change the external environment.
        using var sandbox = TemporaryDirectory.Create();
        var shell = new EdShell();
        var outputPath = Path.Combine(sandbox.DirectoryPath, "executed.txt");

        shell.Execute($"Set-Content -Path '{outputPath}' -Value 'done'");

        await Assert.That(File.Exists(outputPath)).IsTrue();
        await Assert.That(File.ReadAllText(outputPath).TrimEnd()).IsEqualTo("done");
    }

    [Test]
    public async Task WriteToCommand_PipesLinesToCommandStandardInput()
    {
        // Verifies the concrete shell forwards provided lines to the command through standard input.
        using var sandbox = TemporaryDirectory.Create();
        var shell = new EdShell();
        var outputPath = Path.Combine(sandbox.DirectoryPath, "written.txt");
        var lines = new[] { "first", "second", "third" };

        shell.WriteToCommand(
            $"[Console]::In.ReadToEnd() | Set-Content -Path '{outputPath}' -NoNewline",
            lines);

        await Assert.That(File.ReadAllText(outputPath)).IsEqualTo(string.Join(Environment.NewLine, lines));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string DirectoryPath { get; }

        private TemporaryDirectory(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public static TemporaryDirectory Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "EdShellTests", Guid.NewGuid().ToString("N"));
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
