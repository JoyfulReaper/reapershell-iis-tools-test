using System.Globalization;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "iis-tools-tests-" + Guid.NewGuid().ToString("N")));
        Directory.Create();
    }

    public DirectoryInfo Directory { get; }

    public string GetPath(params string[] parts)
    {
        return Path.Combine([Directory.FullName, .. parts]);
    }

    public FileInfo WriteAppLog(string relativePath, params string[] lines)
    {
        return WriteFile(relativePath, lines);
    }

    public FileInfo WriteIisLog(string relativePath, params string[] rows)
    {
        var lines = new[]
        {
            "#Software: Microsoft Internet Information Services 10.0",
            "#Version: 1.0",
            "#Date: 2026-07-07 12:00:00",
            TestLogData.W3cHeader
        }.Concat(rows);

        return WriteFile(relativePath, lines);
    }

    public FileInfo WriteFile(string relativePath, IEnumerable<string> lines)
    {
        var path = GetPath(relativePath);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            System.IO.Directory.CreateDirectory(parent);
        }

        File.WriteAllLines(path, lines);
        return new FileInfo(path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Refresh();
            if (Directory.Exists)
            {
                Directory.Delete(recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class TestLogData
{
    public const string W3cHeader = "#Fields: date time cs-method cs-uri-stem cs-uri-query sc-status sc-substatus sc-win32-status time-taken cs(User-Agent) cs(Referer)";

    public static string W3cRow(
        string date,
        string time,
        string method,
        string path,
        string query,
        int status,
        string userAgent = "Mozilla/5.0",
        string referer = "-",
        string subStatus = "0",
        string win32Status = "64",
        string timeTaken = "123")
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{date} {time} {method} {path} {query} {status} {subStatus} {win32Status} {timeTaken} {userAgent} {referer}");
    }
}

internal static class CommandTestHost
{
    public static Task<(int ExitCode, string StdOut, string StdErr)> ExecuteSearchCommandAsync(
        DirectoryInfo workingDirectory,
        params string[] args)
    {
        return ExecuteSearchCommandAsync(workingDirectory, services: null, args);
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteSearchCommandAsync(
        DirectoryInfo workingDirectory,
        IServiceProvider? services,
        params string[] args)
    {
        var stdout = new StringWriter(CultureInfo.InvariantCulture);
        var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var context = new ShellContext(stdout, stderr, workingDirectory, services, CancellationToken.None, ShellColorMode.Never);
        var exitCode = await new IisErrorSearchCommand().ExecuteAsync(context, args);

        return (exitCode, NormalizeLineEndings(stdout.ToString()), NormalizeLineEndings(stderr.ToString()));
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteVersionCommandAsync(
        DirectoryInfo workingDirectory,
        params string[] args)
    {
        var stdout = new StringWriter(CultureInfo.InvariantCulture);
        var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var context = new ShellContext(stdout, stderr, workingDirectory, services: null, CancellationToken.None, ShellColorMode.Never);
        var exitCode = await new IisToolsVersionCommand().ExecuteAsync(context, args);

        return (exitCode, NormalizeLineEndings(stdout.ToString()), NormalizeLineEndings(stderr.ToString()));
    }

    public static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
