using System;
using System.Collections.Generic;
using System.Globalization;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class IisErrorSearchOptionsParser
{
    private static readonly HashSet<string> KnownOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--help",
        "-h",
        "/?",
        "--version",
        "--app-log",
        "--app-log-path",
        "--iis-log",
        "--iis-log-path",
        "--pattern",
        "--app-pattern",
        "--status",
        "--iis-status",
        "--last",
        "--newest-files-only",
        "--newest-file-count",
        "--since",
        "--verbose",
        "--fail-on-match",
        "--iis-contains",
        "--user-agent",
        "--ua",
        "--url",
        "--all-statuses",
        "--oldest-first",
        "--newest-first"
    };

    public bool TryParse(ShellContext context, IReadOnlyList<string> args, out IisErrorSearchOptions options)
    {
        options = new IisErrorSearchOptions();

        var appLogPathsOverridden = false;
        var iisLogPathsOverridden = false;
        var appPatternsOverridden = false;
        var statusCodesOverridden = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];

            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    return true;

                case "--version":
                    options.ShowVersion = true;
                    return true;

                case "--app-log":
                case "--app-log-path":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var appLogPath))
                    {
                        return false;
                    }

                    if (!appLogPathsOverridden)
                    {
                        options.AppLogPaths.Clear();
                        appLogPathsOverridden = true;
                    }

                    options.AppLogPaths.Add(appLogPath);
                    break;

                case "--iis-log":
                case "--iis-log-path":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var iisLogPath))
                    {
                        return false;
                    }

                    if (!iisLogPathsOverridden)
                    {
                        options.IisLogPaths.Clear();
                        iisLogPathsOverridden = true;
                    }

                    options.IisLogPaths.Add(iisLogPath);
                    break;

                case "--pattern":
                case "--app-pattern":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var pattern))
                    {
                        return false;
                    }

                    if (!appPatternsOverridden)
                    {
                        options.AppPatterns.Clear();
                        appPatternsOverridden = true;
                    }

                    options.AppPatterns.Add(pattern);
                    break;

                case "--status":
                case "--iis-status":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var statusValue))
                    {
                        return false;
                    }

                    if (options.AllStatuses)
                    {
                        context.WriteErrorLine("Choose either --status or --all-statuses, not both.");
                        IisErrorSearchRenderer.WriteUsage(context);
                        return false;
                    }

                    if (!statusCodesOverridden)
                    {
                        options.IisStatusCodes.Clear();
                        statusCodesOverridden = true;
                    }

                    options.AllStatuses = false;
                    options.HasExplicitStatusFilter = true;

                    var parsedAnyStatus = false;
                    foreach (var statusPart in statusValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(statusPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
                        {
                            context.WriteErrorLine($"Invalid HTTP status code: {statusPart}. Status codes must be between 100 and 599.");
                            return false;
                        }

                        if (statusCode < 100 || statusCode > 599)
                        {
                            context.WriteErrorLine($"Invalid HTTP status code: {statusPart}. Status codes must be between 100 and 599.");
                            return false;
                        }

                        options.IisStatusCodes.Add(statusCode);
                        parsedAnyStatus = true;
                    }

                    if (!parsedAnyStatus)
                    {
                        context.WriteErrorLine("Missing value for --status.");
                        return false;
                    }

                    break;

                case "--last":
                    if (!TryReadPositiveInt(context, args, ref index, arg, out var last))
                    {
                        return false;
                    }

                    options.Last = last;
                    break;

                case "--newest-files-only":
                    options.NewestFilesOnly = true;
                    break;

                case "--newest-file-count":
                    if (!TryReadPositiveInt(context, args, ref index, arg, out var newestFileCount))
                    {
                        return false;
                    }

                    options.NewestFileCount = newestFileCount;
                    break;

                case "--since":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var sinceValue))
                    {
                        return false;
                    }

                    if (!TryParseSince(sinceValue, out var sinceUtc, out var sinceError))
                    {
                        context.WriteErrorLine(sinceError);
                        return false;
                    }

                    options.SinceUtc = sinceUtc;
                    options.SinceExpression = sinceValue;
                    break;

                case "--verbose":
                    options.Verbose = true;
                    break;

                case "--fail-on-match":
                    options.FailOnMatch = true;
                    break;

                case "--iis-contains":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var iisContains))
                    {
                        return false;
                    }

                    options.IisContainsPatterns.Add(iisContains);
                    break;

                case "--user-agent":
                case "--ua":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var userAgent))
                    {
                        return false;
                    }

                    options.UserAgentPatterns.Add(userAgent);
                    break;

                case "--url":
                    if (!TryReadOptionValue(context, args, ref index, arg, out var url))
                    {
                        return false;
                    }

                    options.UrlPatterns.Add(url);
                    break;

                case "--all-statuses":
                    if (options.HasExplicitStatusFilter)
                    {
                        context.WriteErrorLine("Choose either --status or --all-statuses, not both.");
                        IisErrorSearchRenderer.WriteUsage(context);
                        return false;
                    }

                    options.AllStatuses = true;
                    break;

                case "--oldest-first":
                    if (options.HasExplicitNewestFirst)
                    {
                        context.WriteErrorLine("Choose either --oldest-first or --newest-first, not both.");
                        IisErrorSearchRenderer.WriteUsage(context);
                        return false;
                    }

                    options.DisplayOrder = MatchDisplayOrder.OldestFirst;
                    options.HasExplicitOldestFirst = true;
                    break;

                case "--newest-first":
                    if (options.HasExplicitOldestFirst)
                    {
                        context.WriteErrorLine("Choose either --oldest-first or --newest-first, not both.");
                        IisErrorSearchRenderer.WriteUsage(context);
                        return false;
                    }

                    options.DisplayOrder = MatchDisplayOrder.NewestFirst;
                    options.HasExplicitNewestFirst = true;
                    break;

                default:
                    context.WriteErrorLine($"Unknown option: {arg}");
                    IisErrorSearchRenderer.WriteUsage(context);
                    return false;
            }
        }

        return true;
    }

    private static bool TryReadOptionValue(
        ShellContext context,
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value)
    {
        value = string.Empty;

        if (index + 1 >= args.Count || IsKnownOption(args[index + 1]))
        {
            context.WriteErrorLine($"Missing value for {optionName}.");
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryReadPositiveInt(
        ShellContext context,
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out int value)
    {
        value = 0;

        if (!TryReadOptionValue(context, args, ref index, optionName, out var rawValue))
        {
            return false;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
        {
            context.WriteErrorLine($"{optionName} must be a positive integer.");
            return false;
        }

        return true;
    }

    private static bool IsKnownOption(string arg)
    {
        return KnownOptions.Contains(arg);
    }

    private static bool TryParseSince(string value, out DateTimeOffset cutoffUtc, out string error)
    {
        error = string.Empty;
        cutoffUtc = default;

        if (TryParseRelativeSince(value, out var relativeCutoffUtc))
        {
            cutoffUtc = relativeCutoffUtc;
            return true;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var absoluteCutoff))
        {
            cutoffUtc = absoluteCutoff.ToUniversalTime();
            return true;
        }

        error = "Invalid value for --since. Use values like 30m, 2h, 1d, or an ISO/local datetime.";
        return false;
    }

    private static bool TryParseRelativeSince(string value, out DateTimeOffset cutoffUtc)
    {
        cutoffUtc = default;

        if (value.Length < 2)
        {
            return false;
        }

        var suffix = value[^1];
        var numberText = value[..^1];

        if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            return false;
        }

        var span = suffix switch
        {
            'm' or 'M' => TimeSpan.FromMinutes(amount),
            'h' or 'H' => TimeSpan.FromHours(amount),
            'd' or 'D' => TimeSpan.FromDays(amount),
            _ => TimeSpan.Zero
        };

        if (span == TimeSpan.Zero)
        {
            return false;
        }

        cutoffUtc = DateTimeOffset.UtcNow.Subtract(span);
        return true;
    }
}
