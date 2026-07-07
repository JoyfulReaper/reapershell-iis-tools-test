using System.Collections.Generic;
using System.Linq;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public static class OutputWriter
{
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

    private static void WriteSection(ShellContext context, string title)
    {
        context.WriteLine("");
        context.WriteLine("================================================================================");
        context.WriteLine($" {title}");
        context.WriteLine("================================================================================");
    }
}
