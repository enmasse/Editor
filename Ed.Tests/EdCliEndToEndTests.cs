using System.Diagnostics;
using System.Threading;

namespace Ed.Tests;

public class EdCliEndToEndTests
{
    private static readonly SemaphoreSlim BuildLock = new(1, 1);
    private static string? _cliPath;

    [Test]
    public async Task ExistingFileSession_PrintsCurrentLine_AndQuitsSuccessfully()
    {
        // Verifies the CLI can open an existing file, execute a print command, and exit successfully.
        using var sandbox = TemporaryDirectory.Create("EdCliEndToEndTests");
        var filePath = Path.Combine(sandbox.DirectoryPath, "input.txt");
        File.WriteAllLines(filePath, ["alpha", "beta"]);

        var result = await RunEdAsync("p\nq!\n", filePath);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo("beta\n");
        await Assert.That(result.StandardError).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NewFileSession_AppendsContent_AndWritesNamedFile()
    {
        // Verifies the CLI can create a named buffer, accept multiline append input, and persist it to disk.
        using var sandbox = TemporaryDirectory.Create("EdCliEndToEndTests");
        var filePath = Path.Combine(sandbox.DirectoryPath, "output.txt");

        var result = await RunEdAsync("a\nalpha\nbeta\n.\nw\nq\n", filePath);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo(string.Empty);
        await Assert.That(result.StandardError).IsEqualTo(string.Empty);
        await Assert.That(File.ReadAllLines(filePath)).IsEquivalentTo(["alpha", "beta"]);
    }

    [Test]
    public async Task VerboseErrors_PrintQuestionMark_AndDetailedMessage()
    {
        // Verifies the CLI reports command failures with canonical `?` output and the detailed message when verbose errors are enabled.
        var result = await RunEdAsync("a\nalpha\n.\nH\nq\nq!\n");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo(string.Empty);
        await Assert.That(result.StandardError).IsEqualTo("?\nBuffer has been modified.\n");
    }

    [Test]
    public async Task PromptMode_WritesPromptBeforeNextCommand()
    {
        // Verifies the CLI emits the interactive prompt marker after prompt mode is enabled.
        var result = await RunEdAsync("P\nq!\n");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput).IsEqualTo("*");
        await Assert.That(result.StandardError).IsEqualTo(string.Empty);
    }

    private static async Task<CliRunResult> RunEdAsync(string standardInput, params string[] args)
    {
        var cliPath = await EnsureCliBuiltAsync();
        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            WorkingDirectory = GetWorkspaceRoot(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start the ed CLI process.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(cancellationTokenSource.Token);

        var standardOutput = NormalizeLineEndings(await standardOutputTask);
        var standardError = NormalizeLineEndings(await standardErrorTask);
        return new CliRunResult(process.ExitCode, standardOutput, standardError);
    }

    private static async Task<string> EnsureCliBuiltAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cliPath) && File.Exists(_cliPath))
        {
            return _cliPath;
        }

        await BuildLock.WaitAsync();

        try
        {
            if (!string.IsNullOrWhiteSpace(_cliPath) && File.Exists(_cliPath))
            {
                return _cliPath;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = GetWorkspaceRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(Path.Combine("Ed.Cli", "Ed.Cli.csproj"));
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("Debug");

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                throw new InvalidOperationException("Failed to start the ed CLI build process.");
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var standardOutput = await standardOutputTask;
                var standardError = await standardErrorTask;
                throw new InvalidOperationException($"Building the ed CLI failed.{Environment.NewLine}{standardOutput}{standardError}");
            }

            _cliPath = GetCliPath();
            return _cliPath;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    private static string GetCliPath()
    {
        var binDirectory = Path.Combine(GetWorkspaceRoot(), "Ed.Cli", "bin");
        var candidates = Directory.GetFiles(binDirectory, "ed.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException("The built ed executable could not be found.");
        }

        return candidates[0];
    }

    private static string GetWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Editor.slnx");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The workspace root could not be determined.");
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private sealed record CliRunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

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
