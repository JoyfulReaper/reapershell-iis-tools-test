namespace IisErrorSearchCommand.Tests;

public sealed class AppLogSearcherTests
{
    [Fact]
    public async Task Command_DefaultPatternsMatchErrorLikeLines()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "info", "Unhandled Exception: boom");

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            "--app-log",
            appLog.FullName,
            "--iis-log",
            temp.GetPath("missing-iis.log"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Unhandled Exception: boom", result.StdOut);
    }

    [Fact]
    public void Search_ExplicitPatternMatches()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "info", "System.Exception: boom");

        var matches = Search(appLog, ["Exception"]);

        var match = Assert.Single(matches);
        Assert.Contains("System.Exception", match.Line);
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "critical failure");

        var matches = Search(appLog, ["CRITICAL"]);

        Assert.Single(matches);
    }

    [Fact]
    public void Search_ExtractsTimestampFromCommonFormats()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "2026-07-07T12:00:01Z Exception happened");

        var matches = Search(appLog, ["Exception"]);

        var match = Assert.Single(matches);
        Assert.True(match.HasParsedTimestamp);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 12, 0, 1, TimeSpan.Zero), match.SortTimeUtc.ToUniversalTime());
    }

    [Fact]
    public void Search_SinceFiltersOldMatches()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog(
            "logs/app.log",
            "2026-07-07T11:59:59Z Exception old",
            "2026-07-07T12:00:01Z Exception new");

        var matches = Search(
            appLog,
            ["Exception"],
            sinceUtc: new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));

        var match = Assert.Single(matches);
        Assert.Contains("new", match.Line);
    }

    [Fact]
    public void Search_LastLimitsResultCount()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog(
            "logs/app.log",
            "2026-07-07T12:00:01Z Exception first",
            "2026-07-07T12:00:02Z Exception second");

        var matches = Search(appLog, ["Exception"], last: 1);

        var match = Assert.Single(matches);
        Assert.Contains("second", match.Line);
    }

    [Fact]
    public void Search_NewestFirstOrdersNewestToOldest()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog(
            "logs/app.log",
            "2026-07-07T12:00:01Z Exception first",
            "2026-07-07T12:00:02Z Exception second");

        var matches = Search(appLog, ["Exception"], displayOrder: MatchDisplayOrder.NewestFirst);

        Assert.Collection(
            matches,
            match => Assert.Contains("second", match.Line),
            match => Assert.Contains("first", match.Line));
    }

    [Fact]
    public void Search_MissingPathDoesNotCrashWithVerboseWarnings()
    {
        var warnings = new List<string>();
        var missing = new LogFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.log"), DateTime.UtcNow);

        var matches = new AppLogSearcher().Search(
            [missing],
            ["Exception"],
            last: 10,
            MatchDisplayOrder.OldestFirst,
            sinceUtc: null,
            verbose: true,
            warnings.Add,
            CancellationToken.None);

        Assert.Empty(matches);
        Assert.Contains(warnings, warning => warning.Contains("Skipping app log", StringComparison.OrdinalIgnoreCase));
    }

    private static List<AppMatch> Search(
        FileInfo appLog,
        IReadOnlyCollection<string> patterns,
        int last = 100,
        MatchDisplayOrder displayOrder = MatchDisplayOrder.OldestFirst,
        DateTimeOffset? sinceUtc = null)
    {
        return new AppLogSearcher().Search(
            [new LogFile(appLog.FullName, appLog.LastWriteTimeUtc)],
            patterns,
            last,
            displayOrder,
            sinceUtc,
            verbose: false,
            warningWriter: null,
            CancellationToken.None);
    }
}
