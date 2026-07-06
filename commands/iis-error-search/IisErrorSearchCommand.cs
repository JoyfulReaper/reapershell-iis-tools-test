using System.Globalization;
using ReaperShell.Abstractions;
using System.Text.RegularExpressions;

namespace IisErrorSearchCommand;

public sealed class IisErrorSearchCommand : IShellCommand
{
    public string Name => "iis-error-search";

    public string Description => "Search app/stdout logs and IIS W3C logs for error signals.";

    public Task<int> ExecuteAsync(
        ShellContext context,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseOptions(context, args, out var options))
        {
            return Task.FromResult(1);
        }

        if (options.ShowHelp)
        {
            WriteUsage(context);
            return Task.FromResult(0);
        }

        var appFiles = GetLogFiles(context, options.AppLogPaths);
        var iisFiles = GetLogFiles(context, options.IisLogPaths);

        if (options.NewestFilesOnly)
        {
            appFiles = appFiles.Take(options.NewestFileCount).ToList();
            iisFiles = iisFiles.Take(options.NewestFileCount).ToList();
        }
        context.WriteLine($"App log files: {appFiles.Count}");
        context.WriteLine($"IIS log files: {iisFiles.Count}");
        context.WriteLine($"Showing newest {options.Last} result(s).");

        var appMatches = FindAppMatches(
            appFiles,
            options.AppPatterns,
            options.Last,
            cancellationToken);

        WriteAppMatches(context, appMatches);

        var iisMatches = FindIisMatches(
            iisFiles,
            options.IisStatusCodes,
            options.Last,
            cancellationToken);

        WriteIisMatches(context, iisMatches);
        WriteSummary(context, iisMatches);

        return Task.FromResult(0);
    }

    private static bool TryParseOptions(
        ShellContext context,
        IReadOnlyList<string> args,
        out Options options)
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

                    foreach (var statusPart in statusValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(statusPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
                        {
                            context.WriteErrorLine($"Invalid status code: {statusPart}");
                            return false;
                        }

                        options.IisStatusCodes.Add(statusCode);
                    }

                    break;

                case "--last":
                    if (!TryReadPositiveInt(context, args, ref index, arg, out options.Last))
                    {
                        return false;
                    }

                    break;

                case "--newest-files-only":
                    options.NewestFilesOnly = true;
                    break;

                case "--newest-file-count":
                    if (!TryReadPositiveInt(context, args, ref index, arg, out options.NewestFileCount))
                    {
                        return false;
                    }

                    break;

                default:
                    context.WriteErrorLine($"Unknown option: {arg}");
                    WriteUsage(context);
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

        if (index + 1 >= args.Count)
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

    private static List<LogFile> GetLogFiles(
        ShellContext context,
        IEnumerable<string> paths)
    {
        var filesByPath = new Dictionary<string, LogFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            foreach (var filePath in ExpandGlobPath(context, path))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                    {
                        continue;
                    }

                    filesByPath[fileInfo.FullName] = new LogFile(
                        fileInfo.FullName,
                        fileInfo.LastWriteTimeUtc);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    // Match the PowerShell script's "silently continue" behavior for bad paths/files.
                }
            }
        }

        return filesByPath.Values
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ExpandGlobPath(ShellContext context, string path)
    {
        var fullPattern = Path.GetFullPath(path, context.WorkingDirectory.FullName);
        var root = Path.GetPathRoot(fullPattern);

        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        var relativePattern = fullPattern[root.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var parts = relativePattern.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var match in ExpandGlobParts(root, parts, 0))
        {
            yield return match;
        }
    }

    private static IEnumerable<string> ExpandGlobParts(
        string currentDirectory,
        IReadOnlyList<string> parts,
        int index)
    {
        if (index >= parts.Count)
        {
            yield break;
        }

        var part = parts[index];
        var isLastPart = index == parts.Count - 1;
        var hasWildcard = HasWildcard(part);

        if (isLastPart)
        {
            if (hasWildcard)
            {
                foreach (var file in SafeEnumerateFiles(currentDirectory, part))
                {
                    yield return file;
                }

                yield break;
            }

            var filePath = Path.Combine(currentDirectory, part);
            if (File.Exists(filePath))
            {
                yield return filePath;
            }

            yield break;
        }

        if (hasWildcard)
        {
            foreach (var directory in SafeEnumerateDirectories(currentDirectory, part))
            {
                foreach (var file in ExpandGlobParts(directory, parts, index + 1))
                {
                    yield return file;
                }
            }

            yield break;
        }

        var nextDirectory = Path.Combine(currentDirectory, part);
        if (!Directory.Exists(nextDirectory))
        {
            yield break;
        }

        foreach (var file in ExpandGlobParts(nextDirectory, parts, index + 1))
        {
            yield return file;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
        {
            return [];
        }
    }

    private static bool HasWildcard(string value)
    {
        return value.Contains('*', StringComparison.Ordinal) ||
               value.Contains('?', StringComparison.Ordinal);
    }

    private static void WriteUsage(ShellContext context)
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
        context.WriteLine("  --help                     Show this help.");
    }

    private static List<AppMatch> FindAppMatches(
    IEnumerable<LogFile> appFiles,
    IReadOnlyCollection<string> patterns,
    int last,
    CancellationToken cancellationToken)
    {
        if (patterns.Count == 0)
        {
            return [];
        }

        var appRegex = new Regex(
            string.Join("|", patterns.Select(Regex.Escape)),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

                    if (!appRegex.IsMatch(line))
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

    private static void WriteSection(ShellContext context, string title)
    {
        context.WriteLine("");
        context.WriteLine("================================================================================");
        context.WriteLine($" {title}");
        context.WriteLine("================================================================================");
    }

    private static void WriteAppMatches(ShellContext context, IReadOnlyCollection<AppMatch> appMatches)
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

    private static List<IisMatch> FindIisMatches(
    IEnumerable<LogFile> iisFiles,
    IReadOnlySet<int> statusCodes,
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

                    if (line.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase))
                    {
                        fields = line["#Fields:".Length..]
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
            .Take(last)
            .ToList();
    }

    private static Dictionary<string, string>? ParseIisW3cLine(string line, IReadOnlyList<string> fields)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            return null;
        }

        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length < fields.Count)
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

    private static void WriteIisMatches(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
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

    private static void WriteSummary(ShellContext context, IReadOnlyCollection<IisMatch> iisMatches)
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

    private sealed record IisMatch(
        string File,
        DateTime LastWriteTimeUtc,
        int LineNumber,
        string Date,
        string Time,
        string Method,
        string Url,
        int Status,
        string SubStatus,
        string Win32Status,
        string TimeTakenMs,
        string Referer,
        string UserAgent);

    private sealed record AppMatch(
        DateTime LastWriteTimeUtc,
        string File,
        int LineNumber,
        string Line);

    private sealed record LogFile(string FullName, DateTime LastWriteTimeUtc);

    private sealed class Options
    {
        public List<string> AppLogPaths { get; } =
        [
            @".\logs\*.log",
            @".\logs\stdout*.log"
        ];

        public List<string> IisLogPaths { get; } =
        [
            @"C:\inetpub\logs\LogFiles\W3SVC*\*.log"
        ];

        public List<string> AppPatterns { get; } =
        [
            "Exception",
            "Unhandled",
            "fail",
            "failed",
            "error",
            "critical",
            "StackTrace",
            "NullReferenceException",
            "InvalidOperationException",
            "ArgumentException"
        ];

        public HashSet<int> IisStatusCodes { get; } =
        [
            400,
            401,
            403,
            404,
            410,
            429,
            500,
            502,
            503
        ];

        public int Last = 100;
        public bool NewestFilesOnly;
        public int NewestFileCount = 10;
        public bool ShowHelp;
    }
}