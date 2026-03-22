using System.Text;

namespace Ed.Cli;

internal sealed class EdCommandApplication
{
    private readonly EdEditor _editor;
    private readonly IEdFileSystem _fileSystem;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public EdCommandApplication(
        EdEditor editor,
        IEdFileSystem fileSystem,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public int Run(string[] args)
    {
        if (args.Length > 1)
        {
            _error.WriteLine("usage: ed [file]");
            return 1;
        }

        _editor.CreateBuffer();

        try
        {
            if (args.Length == 1)
            {
                OpenInitialBuffer(args[0]);
            }
        }
        catch (Exception ex) when (IsCommandError(ex))
        {
            WriteError(ex.Message);
            return 1;
        }

        while (true)
        {
            if (_editor.IsPromptEnabled)
            {
                _output.Write("*");
                _output.Flush();
            }

            var rawCommand = _input.ReadLine();

            if (rawCommand is null)
            {
                return 0;
            }

            var commandText = ReadFullCommand(rawCommand);

            try
            {
                var result = _editor.ExecuteCommand(commandText);
                WriteOutput(result.Output);

                if (IsQuitCommand(commandText))
                {
                    return 0;
                }
            }
            catch (Exception ex) when (IsCommandError(ex))
            {
                WriteError(ex.Message);
            }
        }
    }

    private void OpenInitialBuffer(string path)
    {
        if (_fileSystem.Exists(path))
        {
            _editor.Edit(path, force: true);
        }
        else
        {
            _editor.SetFileName(path);
        }
    }

    private string ReadFullCommand(string commandText)
    {
        if (!RequiresTextInput(commandText))
        {
            return commandText;
        }

        var builder = new StringBuilder(commandText);

        while (true)
        {
            var inputLine = _input.ReadLine();

            if (inputLine is null)
            {
                throw new InvalidOperationException("Unexpected end of input.");
            }

            builder.Append('\n');
            builder.Append(inputLine);

            if (string.Equals(inputLine, ".", StringComparison.Ordinal))
            {
                return builder.ToString();
            }
        }
    }

    private void WriteOutput(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }
    }

    private void WriteError(string message)
    {
        _error.WriteLine("?");

        if (_editor.IsVerboseErrorsEnabled)
        {
            var verboseMessage = !string.IsNullOrWhiteSpace(_editor.LastErrorMessage)
                ? _editor.LastErrorMessage
                : message;

            if (!string.IsNullOrWhiteSpace(verboseMessage))
            {
                _error.WriteLine(verboseMessage);
            }
        }
    }

    private static bool IsQuitCommand(string commandText)
    {
        var trimmed = commandText.Trim();
        return string.Equals(trimmed, "q", StringComparison.Ordinal)
            || string.Equals(trimmed, "q!", StringComparison.Ordinal)
            || string.Equals(trimmed, "Q", StringComparison.Ordinal);
    }

    private static bool RequiresTextInput(string commandText)
    {
        var command = GetCommandCharacter(commandText);
        return command == 'a' || command == 'i' || command == 'c';
    }

    private static char? GetCommandCharacter(string commandText)
    {
        var trimmed = commandText.TrimStart();

        if (trimmed.Length == 0)
        {
            return null;
        }

        var index = 0;

        if (trimmed[index] == '%')
        {
            index++;
        }

        while (index < trimmed.Length)
        {
            var value = trimmed[index];

            if (char.IsWhiteSpace(value) || value == ',' || value == ';')
            {
                index++;
                continue;
            }

            if (char.IsDigit(value) || value == '.' || value == '$')
            {
                index++;
                continue;
            }

            if (value == '+' || value == '-')
            {
                index++;

                while (index < trimmed.Length && char.IsDigit(trimmed[index]))
                {
                    index++;
                }

                continue;
            }

            if (value == '\'')
            {
                index = Math.Min(index + 2, trimmed.Length);
                continue;
            }

            if (value == '/' || value == '?')
            {
                var delimiter = value;
                index++;

                while (index < trimmed.Length && trimmed[index] != delimiter)
                {
                    index++;
                }

                if (index < trimmed.Length)
                {
                    index++;
                    continue;
                }

                return null;
            }

            return value;
        }

        return null;
    }

    private static bool IsCommandError(Exception exception)
    {
        return exception is InvalidOperationException
            || exception is NotSupportedException
            || exception is ArgumentOutOfRangeException
            || exception is KeyNotFoundException
            || exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
