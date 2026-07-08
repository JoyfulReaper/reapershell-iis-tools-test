namespace IisErrorSearchCommand.Tests;

public sealed class IisW3cLogSearcherTests
{
    [Fact]
    public void Search_DefaultErrorStatusesIncludeCommon4xxAnd5xxMatches()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog(
            "u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/missing", "-", 404),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/boom", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:03", "GET", "/ok", "-", 200));

        var matches = Search(log, new IisErrorSearchOptions());

        Assert.Equal([404, 500], matches.Select(match => match.Status));
    }

    [Fact]
    public void Search_ExplicitStatusOnlyReturnsThatStatus()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog(
            "u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/missing", "-", 404),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/boom", "-", 500));
        var options = new IisErrorSearchOptions { HasExplicitStatusFilter = true };
        options.IisStatusCodes.Clear();
        options.IisStatusCodes.Add(404);

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal(404, match.Status);
    }

    [Fact]
    public void Search_AllStatusesAllowsNonErrorsWhenTextFiltersMatch()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog("u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/health", "-", 200));
        var options = new IisErrorSearchOptions { AllStatuses = true };
        options.IisContainsPatterns.Add("health");

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal(200, match.Status);
    }

    [Fact]
    public void Search_UserAgentMatchesDecodedText()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog("u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/robots", "-", 200, "Googlebot%2F2.1+Crawler"));
        var options = new IisErrorSearchOptions();
        options.UserAgentPatterns.Add("bot/2.1 crawler");

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal("Googlebot/2.1 Crawler", match.UserAgent);
    }

    [Fact]
    public void Search_IisContainsSearchesCombinedRowContent()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog("u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/robots", "-", 200, "Friendlybot"));
        var options = new IisErrorSearchOptions();
        options.IisContainsPatterns.Add("bot");

        var matches = Search(log, options);

        Assert.Single(matches);
    }

    [Fact]
    public void Search_UrlMatchesPathAndQuery()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog("u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/api/stats", "id=42", 200));
        var options = new IisErrorSearchOptions();
        options.UrlPatterns.Add("/api/stats?id=42");

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal("/api/stats?id=42", match.Url);
    }

    [Fact]
    public void Search_SinceFiltersOldRowsOut()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog(
            "u_ex.log",
            TestLogData.W3cRow("2026-07-07", "11:59:59", "GET", "/old", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/new", "-", 500));
        var options = new IisErrorSearchOptions { SinceUtc = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero) };

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal("/new", match.Url);
    }

    [Fact]
    public void Search_NewestFirstReturnsNewestMatchesFirst()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog(
            "u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/old", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/new", "-", 500));
        var options = new IisErrorSearchOptions { DisplayOrder = MatchDisplayOrder.NewestFirst };

        var matches = Search(log, options);

        Assert.Equal(["/new", "/old"], matches.Select(match => match.Url));
    }

    [Fact]
    public void Search_LastLimitsResultCount()
    {
        using var temp = new TempDirectory();
        var log = temp.WriteIisLog(
            "u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/first", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/second", "-", 500));
        var options = new IisErrorSearchOptions { Last = 1 };

        var matches = Search(log, options);

        var match = Assert.Single(matches);
        Assert.Equal("/second", match.Url);
    }

    private static List<IisMatch> Search(FileInfo log, IisErrorSearchOptions options)
    {
        return new IisW3cLogSearcher(new IisW3cParser()).Search(
            [new LogFile(log.FullName, log.LastWriteTimeUtc)],
            options,
            warningWriter: null,
            CancellationToken.None);
    }
}
