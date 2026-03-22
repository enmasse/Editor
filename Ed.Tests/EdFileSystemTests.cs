using Ed;

namespace Ed.Tests;

public class EdFileSystemTests
{
    [Test]
    public async Task GetFullPath_ReturnsAbsolutePath()
    {
        // Verifies the concrete filesystem resolves relative paths to absolute paths.
        using var sandbox = TemporaryDirectory.Create();
        var fileSystem = new EdFileSystem();
        var relativePath = Path.Combine(sandbox.DirectoryPath, ".", "sample.txt");

        var fullPath = fileSystem.GetFullPath(relativePath);

        await Assert.That(Path.IsPathFullyQualified(fullPath)).IsTrue();
        await Assert.That(fullPath).IsEqualTo(Path.GetFullPath(relativePath));
    }

    [Test]
    public async Task Exists_ReturnsTrue_WhenFileExists()
    {
        // Verifies the concrete filesystem reports when a file is present on disk.
        using var sandbox = TemporaryDirectory.Create();
        var fileSystem = new EdFileSystem();
        var path = Path.Combine(sandbox.DirectoryPath, "existing.txt");
        File.WriteAllLines(path, ["alpha"]);

        var exists = fileSystem.Exists(path);

        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ReadAllLines_ReturnsStoredContent()
    {
        // Verifies the concrete filesystem reads file content from disk in order.
        using var sandbox = TemporaryDirectory.Create();
        var fileSystem = new EdFileSystem();
        var path = Path.Combine(sandbox.DirectoryPath, "input.txt");
        File.WriteAllLines(path, ["alpha", "beta", "gamma"]);

        var lines = fileSystem.ReadAllLines(path);

        await Assert.That(string.Join("\n", lines)).IsEqualTo("alpha\nbeta\ngamma");
    }

    [Test]
    public async Task WriteAllLines_PersistsContentToDisk()
    {
        // Verifies the concrete filesystem overwrites a file with the provided lines.
        using var sandbox = TemporaryDirectory.Create();
        var fileSystem = new EdFileSystem();
        var path = Path.Combine(sandbox.DirectoryPath, "output.txt");

        fileSystem.WriteAllLines(path, ["first", "second"]);

        await Assert.That(string.Join("\n", File.ReadAllLines(path))).IsEqualTo("first\nsecond");
    }

    [Test]
    public async Task AppendAllLines_AppendsContentToExistingFile()
    {
        // Verifies the concrete filesystem appends new lines after existing file content.
        using var sandbox = TemporaryDirectory.Create();
        var fileSystem = new EdFileSystem();
        var path = Path.Combine(sandbox.DirectoryPath, "append.txt");
        File.WriteAllLines(path, ["first"]);

        fileSystem.AppendAllLines(path, ["second", "third"]);

        await Assert.That(string.Join("\n", File.ReadAllLines(path))).IsEqualTo("first\nsecond\nthird");
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
            var directoryPath = Path.Combine(Path.GetTempPath(), "EdFileSystemTests", Guid.NewGuid().ToString("N"));
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
