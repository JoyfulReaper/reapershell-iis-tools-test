using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IisErrorSearchCommand;

public sealed class IisW3cParser
{
    private const string FieldsHeader = "#Fields:";

    public bool TryReadFieldsHeader(string line, out string[] fields)
    {
        fields = [];

        if (!line.StartsWith(FieldsHeader, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var header = line[FieldsHeader.Length..].Trim();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        fields = header.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fields.Length > 0;
    }

    public bool TryParseRow(string line, IReadOnlyList<string> fields, out IisW3cRow row)
    {
        row = new IisW3cRow(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            return false;
        }

        var values = TokenizeLine(line);
        if (values.Count < fields.Count)
        {
            return false;
        }

        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < fields.Count; index++)
        {
            fieldValues[fields[index]] = values[index];
        }

        fieldValues.TryGetValue("date", out var date);
        fieldValues.TryGetValue("time", out var time);

        row = new IisW3cRow(
            TryParseTimestampUtc(date, time, out var timestampUtc) ? timestampUtc : null,
            fieldValues);
        return true;
    }

    private static List<string> TokenizeLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            values.Add(current.ToString());
        }

        return values;
    }

    private static bool TryParseTimestampUtc(string? date, string? time, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time) || date == "-" || time == "-")
        {
            return false;
        }

        var combined = $"{date} {time}";
        if (DateTimeOffset.TryParseExact(
                combined,
                new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            timestampUtc = parsed.ToUniversalTime();
            return true;
        }

        if (DateTimeOffset.TryParse(
                combined,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var flexibleParsed))
        {
            timestampUtc = flexibleParsed.ToUniversalTime();
            return true;
        }

        return false;
    }
}
