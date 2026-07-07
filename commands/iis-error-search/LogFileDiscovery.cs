using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IisErrorSearchCommand;

public sealed class LogFileDiscovery
{
    private readonly Options _options;
    private readonly string _workingDirectory;

    public LogFileDiscovery(Options options, string workingDirectory)
    {
        _options = options;
        _workingDirectory = workingDirectory;
    }

    public List<LogFile> DiscoverAppLogFiles()
    {
        return GetLogFiles(_options.AppLogPaths);
    }

    public List<LogFile> DiscoverIisLogFiles()
    {
        return GetLogFiles(_options.IisLogPaths);
    }

    private List<LogFile> GetLogFiles(IEnumerable<string> paths)
    {
        var filesByPath = new Dictionary<string, LogFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            foreach (var filePath in ExpandGlobPath(path))
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
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
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

    private IEnumerable<string> ExpandGlobPath(string path)
    {
        string fullPattern;
        try
        {
            fullPattern = Path.GetFullPath(path, _workingDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            yield break;
        }

        var root = Path.GetPathRoot(fullPattern);
        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        var relativePattern = fullPattern[root.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relativePattern))
        {
            yield break;
        }

        var parts = relativePattern.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var match in ExpandGlobParts(root, parts, 0))
        {
            yield return match;
        }
    }

    private static IEnumerable<string> ExpandGlobParts(string currentDirectory, IReadOnlyList<string> parts, int index)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    private static bool HasWildcard(string value)
    {
        return value.Contains('*', StringComparison.Ordinal) ||
               value.Contains('?', StringComparison.Ordinal);
    }
}
