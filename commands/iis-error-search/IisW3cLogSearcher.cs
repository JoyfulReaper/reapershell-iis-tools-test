using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace IisErrorSearchCommand;

public sealed class IisW3cLogSearcher
{
    private readonly IisW3cParser _parser;

    public IisW3cLogSearcher(IisW3cParser parser)
    {
        _parser = parser;
    }

    public List<IisMatch> Search(
        IReadOnlyCollection<LogFile> iisFiles,
        IReadOnlyCollection<int> statusCodes,
        int last,
        DateTimeOffset? sinceUtc,
        bool verbose,
        Action<string>? warningWriter,
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

                    if (_parser.TryReadFieldsHeader(line, out var parsedFields))
                    {
                        fields = parsedFields;
                        continue;
                    }

                    if (fields is null)
                    {
                        continue;
                    }

                    if (!_parser.TryParseRow(line, fields, out var row))
                    {
                        continue;
                    }

                    if (sinceUtc.HasValue && !row.TimestampUtc.HasValue)
                    {
                        continue;
                    }

                    if (!row.Fields.TryGetValue("sc-status", out var statusRaw) ||
                        !int.TryParse(statusRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
                    {
                        continue;
                    }

                    if (!statusCodes.Contains(status))
                    {
                        continue;
                    }

                    if (sinceUtc.HasValue && row.TimestampUtc.HasValue && row.TimestampUtc.Value < sinceUtc.Value)
                    {
                        continue;
                    }

                    var uriStem = GetRowValue(row.Fields, "cs-uri-stem");
                    var uriQuery = GetRowValue(row.Fields, "cs-uri-query");
                    var url = uriStem;
                    if (!string.IsNullOrWhiteSpace(uriQuery) && uriQuery != "-")
                    {
                        url = $"{uriStem}?{uriQuery}";
                    }

                    matches.Add(new IisMatch(
                        row.TimestampUtc,
                        file.FullName,
                        file.LastWriteTimeUtc,
                        lineNumber,
                        GetRowValue(row.Fields, "date"),
                        GetRowValue(row.Fields, "time"),
                        GetRowValue(row.Fields, "cs-method"),
                        url,
                        status,
                        GetRowValue(row.Fields, "sc-substatus"),
                        GetRowValue(row.Fields, "sc-win32-status"),
                        GetRowValue(row.Fields, "time-taken"),
                        GetRowValue(row.Fields, "cs(Referer)"),
                        FormatUserAgent(GetRowValue(row.Fields, "cs(User-Agent)"))));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
            {
                Warn(verbose, warningWriter, $"Skipping IIS log '{file.FullName}': {ex.Message}");
            }
        }

        return matches
            .OrderByDescending(match => match.SortTimeUtc ?? new DateTimeOffset(match.LastWriteTimeUtc, TimeSpan.Zero))
            .ThenByDescending(match => match.File, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(match => match.LineNumber)
            .Take(last)
            .ToList();
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

    private static void Warn(bool verbose, Action<string>? warningWriter, string message)
    {
        if (verbose)
        {
            warningWriter?.Invoke(message);
        }
    }
}
