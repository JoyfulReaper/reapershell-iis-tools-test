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
        IisErrorSearchOptions options,
        Action<string>? warningWriter,
        CancellationToken cancellationToken)
    {
        var matches = new List<IisMatch>();
        var applyStatusFilter = ShouldApplyStatusFilter(options);

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

                    if (!row.Fields.TryGetValue("sc-status", out var statusRaw) ||
                        !int.TryParse(statusRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
                    {
                        continue;
                    }

                    var url = BuildUrl(row.Fields);
                    var userAgent = FormatUserAgent(GetRowValue(row.Fields, "cs(User-Agent)"));
                    var combinedText = BuildCombinedText(row.Fields, url, userAgent, status);

                    if (!MatchesFilters(options, url, userAgent, combinedText))
                    {
                        continue;
                    }

                    if (applyStatusFilter && !options.IisStatusCodes.Contains(status))
                    {
                        continue;
                    }

                    var effectiveTimestampUtc = row.TimestampUtc ?? new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
                    if (options.SinceUtc.HasValue && effectiveTimestampUtc < options.SinceUtc.Value)
                    {
                        continue;
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
                        userAgent));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
            {
                Warn(options.Verbose, warningWriter, $"Skipping IIS log '{file.FullName}': {ex.Message}");
            }
        }

        var newestWindow = matches
            .OrderByDescending(GetEffectiveSortTime)
            .ThenByDescending(match => match.File, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(match => match.LineNumber)
            .Take(options.Last)
            .ToList();

        return options.DisplayOrder == MatchDisplayOrder.NewestFirst
            ? newestWindow
            : newestWindow
                .OrderBy(GetEffectiveSortTime)
                .ThenBy(match => match.File, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.LineNumber)
                .ToList();
    }

    private static DateTimeOffset GetEffectiveSortTime(IisMatch match)
    {
        return match.SortTimeUtc ?? new DateTimeOffset(match.LastWriteTimeUtc, TimeSpan.Zero);
    }

    private static bool ShouldApplyStatusFilter(IisErrorSearchOptions options)
    {
        if (options.AllStatuses)
        {
            return false;
        }

        if (options.HasExplicitStatusFilter)
        {
            return true;
        }

        return !options.HasIisTextFilters;
    }

    private static bool MatchesFilters(
        IisErrorSearchOptions options,
        string url,
        string userAgent,
        string combinedText)
    {
        return MatchesAny(combinedText, options.IisContainsPatterns) &&
               MatchesAny(userAgent, options.UserAgentPatterns) &&
               MatchesAny(url, options.UrlPatterns);
    }

    private static string BuildUrl(IReadOnlyDictionary<string, string> row)
    {
        var uriStem = GetRowValue(row, "cs-uri-stem");
        var uriQuery = GetRowValue(row, "cs-uri-query");
        if (!string.IsNullOrWhiteSpace(uriQuery) && uriQuery != "-")
        {
            return $"{uriStem}?{uriQuery}";
        }

        return uriStem;
    }

    private static string BuildCombinedText(
        IReadOnlyDictionary<string, string> row,
        string url,
        string userAgent,
        int status)
    {
        return string.Join(
            " ",
            [
                GetRowValue(row, "cs-method"),
                url,
                GetRowValue(row, "cs(Referer)"),
                userAgent,
                status.ToString(CultureInfo.InvariantCulture),
                GetRowValue(row, "sc-substatus"),
                GetRowValue(row, "sc-win32-status")
            ]);
    }

    private static bool MatchesAny(string value, IReadOnlyCollection<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return true;
        }

        return patterns.Any(pattern =>
            !string.IsNullOrWhiteSpace(pattern) &&
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
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
