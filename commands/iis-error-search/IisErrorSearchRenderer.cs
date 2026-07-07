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
        if (options.SinceUtc is not null)
        {
            context.WriteLine($"Since filter: >= {options.SinceUtc.Value:O} UTC");
        }

        context.WriteLine($"Showing newest {options.Last} result(s).");
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
            context.WriteLine($"  Updated: {match.LastWriteTimeUtc:u}");
            context.WriteLine($"  {match.Line}");
        }
    }

    public static void WriteIisMatches(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
    {
        WriteSection(context, "IIS ERROR MATCHES");

        if (iisMatches.Count == 0)
        {
            context.WriteLine("No IIS error status matches found.");
            return;
        }

        foreach (var match in iisMatches)
        {
            context.WriteLine("");
            context.WriteLine($"HTTP {match.Status}  {match.Method} {match.Url}");
            context.WriteLine($"  Time:       {match.Date} {match.Time} UTC");
            context.WriteLine($"  IIS:        {match.Status}.{match.SubStatus}  Win32={match.Win32Status}  Took={match.TimeTakenMs}ms");
            context.WriteLine($"  File:       {match.File}:{match.LineNumber}");

            if (!string.IsNullOrWhiteSpace(match.Referer) && match.Referer != "-")
            {
                context.WriteLine($"  Referer:    {match.Referer}");
            }

            if (!string.IsNullOrWhiteSpace(match.UserAgent) && match.UserAgent != "-")
            {
                context.WriteLine($"  Agent:      {match.UserAgent}");
            }
        }
    }

    public static void WriteSummary(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
    {
        WriteSection(context, "SUMMARY");

        if (iisMatches.Count == 0)
        {
            context.WriteLine("No IIS errors to summarize.");
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
            context.WriteLine($"{group.Count()}x  {group.Key}");
        }
    }

    public static void WriteUsage(ShellContext context)
    {
        context.WriteLine("Usage:");
        context.WriteLine("  iis-error-search [options]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  --app-log <glob>           App/stdout log glob. Can be repeated.");
        context.WriteLine("  --iis-log <glob>           IIS W3C log glob. Can be repeated.");
        context.WriteLine("  --pattern <text>           App/stdout search pattern. Can be repeated.");
        context.WriteLine("  --status <code[,code]>     IIS status code filter. Can be repeated.");
        context.WriteLine("  --last <count>             Number of newest results to show. Default: 100.");
        context.WriteLine("  --newest-files-only        Search only newest files.");
        context.WriteLine("  --newest-file-count <n>    Number of newest files when newest-only is enabled. Default: 10.");
        context.WriteLine("  --since <value>            Only show results at or after this time.");
        context.WriteLine("                             Examples: 30m, 2h, 1d, 2026-07-05T14:30:00");
        context.WriteLine("  --verbose                  Print warnings for skipped paths/files.");
        context.WriteLine("  --fail-on-match            Return exit code 2 if any matches are found.");
        context.WriteLine("  --help                     Show this help.");
    }

    private static void WriteSection(ShellContext context, string title)
    {
        context.WriteLine("");
        context.WriteLine("================================================================================");
        context.WriteLine($" {title}");
        context.WriteLine("================================================================================");
    }
}
