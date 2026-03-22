namespace Ed;

internal static class EdAddressParser
{
    public static bool TryParseSearchCommand(string commandText, out EdSearchDirection direction, out string pattern)
    {
        direction = EdSearchDirection.Forward;
        pattern = string.Empty;

        if (commandText.Length < 2 || (commandText[0] != '/' && commandText[0] != '?'))
        {
            return false;
        }

        if (!EdCommandTextParser.TryParseDelimitedValue(commandText, out var delimiter, out pattern, out var remainder))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(remainder))
        {
            return false;
        }

        if (delimiter == '/')
        {
            direction = EdSearchDirection.Forward;
        }
        else
        {
            direction = EdSearchDirection.Backward;
        }

        return true;
    }

    public static bool TryParseGlobalCommand(string commandText, out EdGlobalMode mode, out string pattern, out string commandList)
    {
        mode = EdGlobalMode.Match;
        pattern = string.Empty;
        commandList = string.Empty;

        if (commandText.Length < 4 || (commandText[0] != 'g' && commandText[0] != 'v'))
        {
            return false;
        }

        if (!EdCommandTextParser.TryParseDelimitedValue(commandText[1..], out _, out pattern, out var remainder))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        if (commandText[0] == 'v')
        {
            mode = EdGlobalMode.NonMatch;
        }
        else
        {
            mode = EdGlobalMode.Match;
        }

        commandList = remainder.Trim();
        return true;
    }

    public static ParsedCommand ParseCommand(
        string commandText,
        int lineCount,
        int currentLine,
        Func<char, int> resolveMark,
        Func<string, EdSearchDirection, int, int> findSearchLine)
    {
        var index = 0;

        while (index < commandText.Length && char.IsWhiteSpace(commandText[index]))
        {
            index++;
        }

        if (index >= commandText.Length)
        {
            return new ParsedCommand(null, false, false, string.Empty);
        }

        if (commandText[index] == '%')
        {
            index++;
            var lastLine = lineCount;

            if (lastLine == 0)
            {
                lastLine = 1;
            }

            return new ParsedCommand(new EdLineRange(1, lastLine), true, false, commandText[index..].TrimStart());
        }

        if (!IsAddressStart(commandText[index]))
        {
            return new ParsedCommand(null, false, false, commandText.TrimStart());
        }

        var originalCurrentLine = currentLine;
        var firstAddress = ParseAddress(commandText, ref index, originalCurrentLine, lineCount, resolveMark, findSearchLine, out var firstAddressUsedSearch);
        var range = new EdLineRange(firstAddress, firstAddress);
        var usedSearchAddress = firstAddressUsedSearch;

        if (index < commandText.Length && (commandText[index] == ',' || commandText[index] == ';'))
        {
            var separator = commandText[index];
            index++;
            var secondaryCurrentLine = originalCurrentLine;

            if (separator == ';')
            {
                secondaryCurrentLine = firstAddress;
            }

            var secondAddress = ParseAddress(commandText, ref index, secondaryCurrentLine, lineCount, resolveMark, findSearchLine, out var secondAddressUsedSearch);
            range = new EdLineRange(firstAddress, secondAddress);

            if (secondAddressUsedSearch)
            {
                usedSearchAddress = true;
            }
        }

        return new ParsedCommand(range, true, usedSearchAddress, commandText[index..].TrimStart());
    }

    public static int ParseDestinationAddress(
        string addressText,
        int currentLine,
        int lineCount,
        Func<char, int> resolveMark,
        Func<string, EdSearchDirection, int, int> findSearchLine)
    {
        var trimmed = addressText.Trim();

        if (string.Equals(trimmed, "0", StringComparison.Ordinal))
        {
            return 0;
        }

        var index = 0;
        var destination = ParseAddress(trimmed, ref index, currentLine, lineCount, resolveMark, findSearchLine, out _);

        if (index != trimmed.Length)
        {
            throw new NotSupportedException($"Unsupported command '{addressText}'.");
        }

        return destination;
    }

    private static bool IsAddressStart(char value)
    {
        return char.IsDigit(value)
            || value == '.'
            || value == '$'
            || value == '\''
            || value == '/'
            || value == '?'
            || value == '+'
            || value == '-';
    }

    private static int ParseAddress(
        string commandText,
        ref int index,
        int currentLine,
        int lineCount,
        Func<char, int> resolveMark,
        Func<string, EdSearchDirection, int, int> findSearchLine,
        out bool usedSearchAddress)
    {
        usedSearchAddress = false;

        if (index >= commandText.Length)
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        int address;

        if (char.IsDigit(commandText[index]))
        {
            var start = index;

            while (index < commandText.Length && char.IsDigit(commandText[index]))
            {
                index++;
            }

            address = int.Parse(commandText[start..index], System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (commandText[index] == '.')
        {
            index++;

            if (currentLine == 0)
            {
                address = 1;
            }
            else
            {
                address = currentLine;
            }
        }
        else if (commandText[index] == '$')
        {
            index++;
            address = lineCount;
        }
        else if (commandText[index] == '\'')
        {
            if (index + 1 >= commandText.Length)
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            var markName = commandText[index + 1];
            index += 2;
            address = resolveMark(markName);
        }
        else if (commandText[index] == '/' || commandText[index] == '?')
        {
            usedSearchAddress = true;
            var searchInput = commandText[index..];

            if (!EdCommandTextParser.TryParseDelimitedValue(searchInput, out var delimiter, out var pattern, out var remainder))
            {
                throw new NotSupportedException($"Unsupported command '{commandText}'.");
            }

            index = commandText.Length - remainder.Length;
            EdSearchDirection direction;

            if (delimiter == '/')
            {
                direction = EdSearchDirection.Forward;
            }
            else
            {
                direction = EdSearchDirection.Backward;
            }

            if (currentLine == 0)
            {
                address = findSearchLine(pattern, direction, 1);
            }
            else
            {
                address = findSearchLine(pattern, direction, currentLine);
            }
        }
        else if (commandText[index] == '+' || commandText[index] == '-')
        {
            if (currentLine == 0)
            {
                address = 1;
            }
            else
            {
                address = currentLine;
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported command '{commandText}'.");
        }

        while (index < commandText.Length && (commandText[index] == '+' || commandText[index] == '-'))
        {
            int sign;

            if (commandText[index] == '+')
            {
                sign = 1;
            }
            else
            {
                sign = -1;
            }

            index++;
            var start = index;

            while (index < commandText.Length && char.IsDigit(commandText[index]))
            {
                index++;
            }

            int offset;

            if (start == index)
            {
                offset = 1;
            }
            else
            {
                offset = int.Parse(commandText[start..index], System.Globalization.CultureInfo.InvariantCulture);
            }

            address += sign * offset;
        }

        return address;
    }
}
