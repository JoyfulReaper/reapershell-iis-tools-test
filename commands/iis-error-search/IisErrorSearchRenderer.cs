using System.Collections.Generic;
using System.Linq;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public static class IisErrorSearchRenderer
{
    public static void WriteRunHeader(
        ShellContext context,
        IisErrorSearchOptions options,
        int appFileCount,
        int iisFileCount)
    {
        context.WriteLine($"App log files: {appFileCount}");
        context.WriteLine($"IIS log files: {iisFileCount}");
        WriteIisFilterSummary(context, options);
        if (options.SinceUtc is not null)
        {
            context.WriteLine($"Since filter: >= {options.SinceUtc.Value:O} UTC");
        }

        context.WriteLine($"Showing newest {options.Last} app result(s) and newest {options.Last} IIS result(s), displayed {GetDisplayOrderLabel(options.DisplayOrder)}.");
    }

    public static void WriteWarning(ShellContext context, string message)
    {
        context.WriteErrorLine(message);
    }

    public static void WriteAppMatches(ShellContext context, IReadOnlyCollection<AppMatch> appMatches)
    {
        WriteSection(context, "APP / STDOUT LOG MATCHES");

        if (appMatches.Count == 0)
        {
            context.WriteLine("No app/stdout matches found.");
            return;
        }

        foreach (var match in appMatches)
        {
            context.WriteLine("");
            context.WriteLine($"APP MATCH  {match.File}:{match.LineNumber}");
            if (match.HasParsedTimestamp)
            {
                context.WriteLine($"  Time:    {match.SortTimeUtc:u}");
                context.WriteLine($"  Updated: {match.LastWriteTimeUtc:u}");
            }
            else
            {
                context.WriteLine($"  Updated: {match.LastWriteTimeUtc:u}");
                context.WriteLine("  Time:    not parsed; ordering/filtering used file updated time.");
            }

            context.WriteLine($"  {SanitizeDisplay(match.Line)}");
        }
    }

    public static void WriteIisMatches(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
    {
        WriteSection(context, "IIS LOG MATCHES");

        if (iisMatches.Count == 0)
        {
            context.WriteLine("No IIS log matches found.");
            return;
        }

        foreach (var match in iisMatches)
        {
            context.WriteLine("");
            context.WriteLine($"HTTP {match.Status}  {SanitizeDisplay(match.Method)} {SanitizeDisplay(match.Url)}");
            context.WriteLine($"  Time:       {SanitizeDisplay(match.Date)} {SanitizeDisplay(match.Time)} UTC");
            context.WriteLine($"  IIS:        {match.Status}.{SanitizeDisplay(match.SubStatus)}  Win32={SanitizeDisplay(match.Win32Status)}  Took={FormatTimeTaken(match.TimeTakenMs)}");
            context.WriteLine($"  File:       {match.File}:{match.LineNumber}");

            if (!string.IsNullOrWhiteSpace(match.Referer) && match.Referer != "-")
            {
                context.WriteLine($"  Referer:    {SanitizeDisplay(match.Referer)}");
            }

            if (!string.IsNullOrWhiteSpace(match.UserAgent) && match.UserAgent != "-")
            {
                context.WriteLine($"  Agent:      {SanitizeDisplay(match.UserAgent)}");
            }
        }
    }

    public static void WriteSummary(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
    {
        WriteSection(context, "SUMMARY");

        if (iisMatches.Count == 0)
        {
            context.WriteLine("No IIS log matches to summarize.");
            return;
        }

        foreach (var group in iisMatches
                     .GroupBy(match => match.Status)
                     .OrderBy(group => group.Key))
        {
            context.WriteLine($"HTTP {group.Key}: {group.Count()}");
        }

        context.WriteLine("");

        foreach (var group in iisMatches
                     .GroupBy(match => match.Url)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(10))
        {
            var statusBreakdown = FormatStatusBreakdown(group);
            context.WriteLine($"{group.Count()}x  {SanitizeDisplay(group.Key)} ({statusBreakdown})");
        }
    }

    public static void WriteUsage(ShellContext context)
    {
        context.WriteLine("Usage:");
        context.WriteLine("  iis-error-search [options]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  --app-log <glob>             App/stdout log glob. Can be repeated.");
        context.WriteLine("  --iis-log <glob>             IIS W3C log file glob. Can be repeated.");
        context.WriteLine("  --pattern <text>             App/stdout search pattern. Can be repeated.");
        context.WriteLine("  --status <code[,code]>       Explicit IIS status code filter. Can be repeated.");
        context.WriteLine("  --all-statuses               Search IIS rows across all status codes.");
        context.WriteLine("  --iis-contains <text>        Search IIS row content. Can be repeated.");
        context.WriteLine("  --user-agent <text>          Search decoded IIS user-agent text. Can be repeated.");
        context.WriteLine("  --ua <text>                  Alias for --user-agent.");
        context.WriteLine("  --url <text>                 Search the combined IIS URL. Can be repeated.");
        context.WriteLine("  --last <count>               Number of newest results to show per section. Default: 100.");
        context.WriteLine("  --oldest-first               Display selected matches oldest-to-newest. Default.");
        context.WriteLine("  --newest-first               Display selected matches newest-to-oldest.");
        context.WriteLine("  --newest-files-only          Search only newest files.");
        context.WriteLine("  --newest-file-count <n>      Number of newest files when newest-only is enabled. Default: 10.");
        context.WriteLine("  --since <value>              Only show results at or after this time.");
        context.WriteLine("                               Examples: 30m, 2h, 1d, 2026-07-05T14:30:00");
        context.WriteLine("  --verbose                    Print warnings for skipped paths/files.");
        context.WriteLine("  --fail-on-match              Return exit code 2 if any matches are found.");
        context.WriteLine("  --version                    Show loaded command pack version/build info.");
        context.WriteLine("  --help                       Show this help.");
        context.WriteLine("");
        context.WriteLine("Examples:");
        context.WriteLine("  iis-error-search");
        context.WriteLine("  iis-error-search --status 404,500");
        context.WriteLine("  iis-error-search --last 50");
        context.WriteLine("  iis-error-search --last 50 --newest-first");
        context.WriteLine("  iis-error-search --user-agent bot");
        context.WriteLine("  iis-error-search --iis-contains bot --all-statuses");
        context.WriteLine("  iis-error-search --iis-log \"C:\\inetpub\\logs\\LogFiles\\W3SVC*\\*.log\" --user-agent bot");
        context.WriteLine("");
        context.WriteLine("Notes:");
        context.WriteLine("  --status and --all-statuses are mutually exclusive.");
        context.WriteLine("  --last selects the newest N matches per section.");
        context.WriteLine("  Display order is controlled by --oldest-first / --newest-first.");
    }

    public static void WriteIisFileHintIfNeeded(ShellContext context, IReadOnlyCollection<LogFile> iisFiles, IReadOnlyCollection<string> requestedGlobs)
    {
        if (iisFiles.Count > 0)
        {
            return;
        }

        context.WriteLine("No IIS log files were found.");
        context.WriteLine("`--iis-log` selects log files. To search for bot traffic, use `--user-agent bot` or `--iis-contains bot`.");

        if (requestedGlobs.Any(looksSuspicious))
        {
            context.WriteLine("The supplied `--iis-log` value looks more like a search term than a file path.");
        }
    }

    private static void WriteIisFilterSummary(ShellContext context, IisErrorSearchOptions options)
    {
        if (options.IisContainsPatterns.Count == 0 &&
            options.UserAgentPatterns.Count == 0 &&
            options.UrlPatterns.Count == 0 &&
            !options.AllStatuses &&
            !options.HasExplicitStatusFilter)
        {
            context.WriteLine($"IIS status filter: default error statuses ({string.Join(", ", options.IisStatusCodes)})");
            return;
        }

        if (options.AllStatuses)
        {
            context.WriteLine("IIS status filter: all statuses");
        }
        else if (options.HasExplicitStatusFilter)
        {
            context.WriteLine($"IIS status filter: {string.Join(", ", options.IisStatusCodes)}");
        }
        else if (options.HasIisTextFilters)
        {
            context.WriteLine("IIS status filter: all statuses (text filters active)");
        }

        if (options.IisContainsPatterns.Count > 0)
        {
            context.WriteLine($"IIS contains: {string.Join(", ", options.IisContainsPatterns)}");
        }

        if (options.UserAgentPatterns.Count > 0)
        {
            context.WriteLine($"IIS user-agent: {string.Join(", ", options.UserAgentPatterns)}");
        }

        if (options.UrlPatterns.Count > 0)
        {
            context.WriteLine($"IIS URL: {string.Join(", ", options.UrlPatterns)}");
        }
    }

    private static bool looksSuspicious(string glob)
    {
        return !glob.Contains('\\') &&
               !glob.Contains('/') &&
               !glob.Contains(':') &&
               glob.IndexOfAny(['*', '?']) >= 0;
    }

    private static void WriteSection(ShellContext context, string title)
    {
        context.WriteLine("");
        context.WriteLine("================================================================================");
        context.WriteLine($" {title}");
        context.WriteLine("================================================================================");
    }

    private static string GetDisplayOrderLabel(MatchDisplayOrder displayOrder)
    {
        return displayOrder == MatchDisplayOrder.NewestFirst
            ? "newest-to-oldest"
            : "oldest-to-newest";
    }

    private static string FormatTimeTaken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return "-";
        }

        return $"{SanitizeDisplay(value)}ms";
    }

    private static string FormatStatusBreakdown(IEnumerable<IisMatch> matches)
    {
        return string.Join(
            ", ",
            matches
                .GroupBy(match => match.Status)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key} x{group.Count()}"));
    }

    private static string SanitizeDisplay(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == '\t' || !char.IsControl(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }
}
