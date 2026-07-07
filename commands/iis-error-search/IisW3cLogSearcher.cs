using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class IisW3cLogSearcher
{
    private const string FieldsHeader = "#Fields:";
    private readonly ShellContext _context;

    public IisW3cLogSearcher(ShellContext context)
    {
        _context = context;
    }

    public List<IisMatch> Search(
        IReadOnlyCollection<LogFile> iisFiles,
        IReadOnlyCollection<int> statusCodes,
        int last,
        CancellationToken cancellationToken)
    {
        var matches = new List<IisMatch>();

        foreach (var file in iisFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[]? fields = null;
            var lineNumber = 0;

            try
            {
                foreach (var line in File.ReadLines(file.FullName))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;

                    if (line.StartsWith(FieldsHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        fields = line[FieldsHeader.Length..]
                            .Trim()
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        continue;
                    }

                    if (fields is null)
                    {
                        continue;
                    }

                    var row = ParseIisW3cLine(line, fields);
                    if (row is null)
                    {
                        continue;
                    }

                    if (!row.TryGetValue("sc-status", out var statusRaw))
                    {
                        continue;
                    }

                    if (!int.TryParse(statusRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
                    {
                        continue;
                    }

                    if (!statusCodes.Contains(status))
                    {
                        continue;
                    }

                    var uriStem = GetRowValue(row, "cs-uri-stem");
                    var uriQuery = GetRowValue(row, "cs-uri-query");
                    var url = uriStem;

                    if (!string.IsNullOrWhiteSpace(uriQuery) && uriQuery != "-")
                    {
                        url = $"{uriStem}?{uriQuery}";
                    }

                    matches.Add(new IisMatch(
                        file.FullName,
                        file.LastWriteTimeUtc,
                        lineNumber,
                        GetRowValue(row, "date"),
                        GetRowValue(row, "time"),
                        GetRowValue(row, "cs-method"),
                        url,
                        status,
                        GetRowValue(row, "sc-substatus"),
                        GetRowValue(row, "sc-win32-status"),
                        GetRowValue(row, "time-taken"),
                        GetRowValue(row, "cs(Referer)"),
                        FormatUserAgent(GetRowValue(row, "cs(User-Agent)"))));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Match the PowerShell script's "silently continue" behavior.
            }
        }

        return matches
            .OrderByDescending(match => match.Date, StringComparer.Ordinal)
            .ThenByDescending(match => match.Time, StringComparer.Ordinal)
            .ThenByDescending(match => match.File, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(match => match.LineNumber)
            .Take(last)
            .ToList();
    }

    private static Dictionary<string, string>? ParseIisW3cLine(string line, IReadOnlyList<string> fields)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            return null;
        }

        var values = TokenizeLine(line);
        if (values.Count < fields.Count)
        {
            return null;
        }

        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < fields.Count; index++)
        {
            row[fields[index]] = values[index];
        }

        return row;
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

    private static string GetRowValue(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "-";
    }

    private static string FormatUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent) || userAgent == "-")
        {
            return "-";
        }

        try
        {
            return Uri.UnescapeDataString(userAgent.Replace("+", " "));
        }
        catch (UriFormatException)
        {
            return userAgent.Replace("+", " ");
        }
    }
}
