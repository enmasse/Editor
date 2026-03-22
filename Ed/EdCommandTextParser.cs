namespace Ed;

internal static class EdCommandTextParser
{
    public static bool TryParseDelimitedArguments(string input, out string first, out string second, out string remainder)
    {
        first = string.Empty;
        second = string.Empty;
        remainder = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var delimiter = input[0];
        var secondDelimiterIndex = FindClosingDelimiter(input, 1, delimiter);

        if (secondDelimiterIndex < 0)
        {
            return false;
        }

        var thirdDelimiterIndex = FindClosingDelimiter(input, secondDelimiterIndex + 1, delimiter);

        if (thirdDelimiterIndex < 0)
        {
            return false;
        }

        first = UnescapeDelimiter(input[1..secondDelimiterIndex], delimiter);
        second = UnescapeDelimiter(input[(secondDelimiterIndex + 1)..thirdDelimiterIndex], delimiter);
        remainder = input[(thirdDelimiterIndex + 1)..];
        return true;
    }

    public static bool TryParseDelimitedValue(string input, out char delimiter, out string value, out string remainder)
    {
        delimiter = default;
        value = string.Empty;
        remainder = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        delimiter = input[0];
        var closingDelimiterIndex = FindClosingDelimiter(input, 1, delimiter);

        if (closingDelimiterIndex < 0)
        {
            return false;
        }

        value = UnescapeDelimiter(input[1..closingDelimiterIndex], delimiter);
        remainder = input[(closingDelimiterIndex + 1)..];
        return true;
    }

    private static int FindClosingDelimiter(string input, int startIndex, char delimiter)
    {
        var escaped = false;

        for (var index = startIndex; index < input.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (input[index] == '\\')
            {
                escaped = true;
                continue;
            }

            if (input[index] == delimiter)
            {
                return index;
            }
        }

        return -1;
    }

    private static string UnescapeDelimiter(string value, char delimiter)
    {
        return value.Replace($"\\{delimiter}", delimiter.ToString(), StringComparison.Ordinal);
    }
}
