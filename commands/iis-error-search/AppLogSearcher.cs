using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class AppLogSearcher
{
    private readonly ShellContext _context;

    public AppLogSearcher(ShellContext context)
    {
        _context = context;
    }

    public List<AppMatch> Search(
        IReadOnlyCollection<LogFile> appFiles,
        IReadOnlyCollection<string> patterns,
        int last,
        DateTimeOffset? sinceUtc,
        bool verbose,
        Action<string>? warningWriter,
        CancellationToken cancellationToken)
    {
        if (patterns.Count == 0)
        {
            return [];
        }

        var matches = new List<AppMatch>();

        foreach (var file in appFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file.FullName))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;

                    if (!ContainsAnyPattern(line, patterns))
                    {
                        continue;
                    }

                    var hasParsedTimestamp = TryExtractTimestampUtc(line, out var lineTimestampUtc);
                    var sortTimeUtc = hasParsedTimestamp
                        ? lineTimestampUtc
                        : new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);

                    if (sinceUtc.HasValue && sortTimeUtc < sinceUtc.Value)
                    {
                        continue;
                    }

                    matches.Add(new AppMatch(
                        sortTimeUtc,
                        file.LastWriteTimeUtc,
                        hasParsedTimestamp,
                        file.FullName,
                        lineNumber,
                        line.Trim()));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
            {
                Warn(verbose, warningWriter, $"Skipping app log '{file.FullName}': {ex.Message}");
            }
        }

        return matches
            .OrderByDescending(match => match.SortTimeUtc)
            .ThenByDescending(match => match.File, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(match => match.LineNumber)
            .Take(last)
            .ToList();
    }

    private static bool ContainsAnyPattern(string line, IReadOnlyCollection<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractTimestampUtc(string line, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        var candidate = line.TrimStart();
        if (candidate.Length == 0)
        {
            return false;
        }

        if (candidate[0] is '[' or '(')
        {
            var closingIndex = candidate.IndexOf(candidate[0] == '[' ? ']' : ')');
            if (closingIndex > 1)
            {
                candidate = candidate[1..closingIndex].Trim();
            }
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss.fffzzz"
        };

        foreach (var format in formats)
        {
            if (candidate.Length < format.Length)
            {
                continue;
            }

            var slice = candidate[..format.Length];
            if (DateTimeOffset.TryParseExact(
                    slice,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsed))
            {
                timestampUtc = parsed.ToUniversalTime();
                return true;
            }
        }

        if (DateTimeOffset.TryParse(
                candidate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var parsedFlexible))
        {
            timestampUtc = parsedFlexible.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static void Warn(bool verbose, Action<string>? warningWriter, string message)
    {
        if (verbose)
        {
            warningWriter?.Invoke(message);
        }
    }
}
