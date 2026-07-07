using System;
using System.Collections.Generic;
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

                    matches.Add(new AppMatch(
                        file.LastWriteTimeUtc,
                        file.FullName,
                        lineNumber,
                        line.Trim()));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Match the PowerShell script's "silently continue" behavior.
            }
        }

        return matches
            .OrderByDescending(match => match.LastWriteTimeUtc)
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
}
