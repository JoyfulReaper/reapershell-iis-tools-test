using System;
using System.Collections.Generic;
using System.Globalization;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class OptionParser
{
    private static readonly HashSet<string> KnownOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--help",
        "-h",
        "/?",
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
        "--newest-file-count"
    };

    public bool TryParse(ShellContext context, IReadOnlyList<string> args, out Options options)
    {
        options = new Options();

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

                    if (!statusCodesOverridden)
                    {
                        options.IisStatusCodes.Clear();
                        statusCodesOverridden = true;
                    }

                    var parsedAnyStatus = false;
                    foreach (var statusPart in statusValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(statusPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
                        {
                            context.WriteErrorLine($"Invalid status code: {statusPart}");
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

                default:
                    context.WriteErrorLine($"Unknown option: {arg}");
                    CommandUsage.Write(context);
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
}
