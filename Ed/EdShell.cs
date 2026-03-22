using System.Diagnostics;

namespace Ed;

public sealed class EdShell : IEdShell
{
    public IReadOnlyList<string> ReadCommandOutput(string commandText)
    {
        using var process = StartShellProcess(commandText, redirectStandardInput: false, redirectStandardOutput: true);
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        EnsureSuccess(process.ExitCode, standardError);
        return ParseOutputLines(standardOutput);
    }

    public void WriteToCommand(
        string commandText,
        IReadOnlyList<string> lines)
    {
        using var process = StartShellProcess(commandText, redirectStandardInput: true, redirectStandardOutput: false);
        process.StandardInput.Write(string.Join(Environment.NewLine, lines));
        process.StandardInput.Close();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        EnsureSuccess(process.ExitCode, standardError);
    }

    public void Execute(string commandText)
    {
        using var process = StartShellProcess(commandText, redirectStandardInput: false, redirectStandardOutput: false);
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        EnsureSuccess(process.ExitCode, standardError);
    }

    private static Process StartShellProcess(string commandText, bool redirectStandardInput, bool redirectStandardOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{commandText.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start the shell process.");
        }

        return process;
    }

    private static void EnsureSuccess(int exitCode, string standardError)
    {
        if (exitCode == 0)
        {
            return;
        }
        else if (string.IsNullOrWhiteSpace(standardError))
        {
            throw new InvalidOperationException($"Shell command failed with exit code {exitCode}.");
        }
        else
        {
            throw new InvalidOperationException(standardError.Trim());
        }
    }

    private static IReadOnlyList<string> ParseOutputLines(string standardOutput)
    {
        if (string.IsNullOrEmpty(standardOutput))
        {
            return [];
        }

        var normalizedOutput = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalizedOutput.Split('\n');

        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            return lines[..^1];
        }
        else
        {
            return lines;
        }
    }
}
