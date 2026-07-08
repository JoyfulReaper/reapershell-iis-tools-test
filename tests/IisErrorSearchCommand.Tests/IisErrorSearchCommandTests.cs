namespace IisErrorSearchCommand.Tests;

public sealed class IisErrorSearchCommandTests
{
    [Fact]
    public async Task HelpReturnsZeroAndPrintsUsage()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
        Assert.Contains("iis-error-search [options]", result.StdOut);
    }

    [Fact]
    public async Task VersionReturnsZeroAndPrintsBuildMetadata()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("IIS Tools Command Pack", result.StdOut);
        Assert.Contains("Git Commit:", result.StdOut);
    }

    [Fact]
    public async Task UnknownOptionReturnsOne()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--not-real");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown option: --not-real", result.StdErr);
    }

    [Fact]
    public async Task BasicAppSearchPrintsMatchingLine()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "info", "System.Exception: boom");

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            "--app-log",
            appLog.FullName,
            "--pattern",
            "Exception",
            "--iis-log",
            temp.GetPath("empty-iis.log"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("APP / STDOUT LOG MATCHES", result.StdOut);
        Assert.Contains("System.Exception: boom", result.StdOut);
    }

    [Fact]
    public async Task BasicIisSearchPrintsMatchAndSummary()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/api/stats", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            "--iis-log",
            iisLog.FullName,
            "--status",
            "500",
            "--app-log",
            temp.GetPath("empty-app.log"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("IIS LOG MATCHES", result.StdOut);
        Assert.Contains("HTTP 500", result.StdOut);
        Assert.Contains("HTTP 500: 1", result.StdOut);
    }

    [Fact]
    public async Task BotUserAgentSearchReturnsMatch()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/robots", "-", 200, "Googlebot"));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--user-agent", "bot");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Googlebot", result.StdOut);
    }

    [Fact]
    public async Task SuspiciousIisLogGlobPrintsBotSearchHint()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", "*bot*");
        var warningOutput = result.StdOut + result.StdErr;

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("`--iis-log` selects log files", warningOutput);
        Assert.Contains("--user-agent bot", warningOutput);
        Assert.Contains("--iis-contains bot", warningOutput);
    }

    [Fact]
    public async Task FailOnMatchReturnsTwoAndStillPrintsMatches()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "Exception happened");

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            "--app-log",
            appLog.FullName,
            "--iis-log",
            temp.GetPath("empty-iis.log"),
            "--fail-on-match");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Exception happened", result.StdOut);
    }

    [Fact]
    public async Task NoMatchesReturnsZeroAndPrintsCleanMessages()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "all quiet");
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/health", "-", 200));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            "--app-log",
            appLog.FullName,
            "--iis-log",
            iisLog.FullName,
            "--pattern",
            "Exception");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No app/stdout matches found.", result.StdOut);
        Assert.Contains("No IIS log matches found.", result.StdOut);
    }

    [Fact]
    public async Task CursedModeDoesNotChangeExitCodeAndAddsAmbientEvents()
    {
        using var temp = new TempDirectory();
        var appLog = temp.WriteAppLog("logs/app.log", "Exception happened");
        var curse = new FakeCursedShell(isEnabled: true);
        var services = new FakeServiceProvider(curse);

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            services,
            "--app-log",
            appLog.FullName,
            "--iis-log",
            temp.GetPath("empty-iis.log"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(curse.Events, entry => entry.Contains("iis-error-search", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CursedBotSearchWithNoMatchesShiftsMoodToSuspicious()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/health", "-", 200, "Mozilla/5.0"));
        var curse = new FakeCursedShell(isEnabled: true);

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            new FakeServiceProvider(curse),
            "--iis-log",
            iisLog.FullName,
            "--user-agent",
            "bot");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("suspicious", curse.Mood);
        Assert.Contains("Bot traffic search requested. The curse narrows its eyes.", curse.Events);
    }

    [Fact]
    public async Task CursedSuspiciousIisLogGlobRecordsAmbientWarning()
    {
        using var temp = new TempDirectory();
        var curse = new FakeCursedShell(isEnabled: true);

        var result = await CommandTestHost.ExecuteSearchCommandAsync(
            temp.Directory,
            new FakeServiceProvider(curse),
            "--iis-log",
            "*bot*");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(curse.Events, entry => entry.Contains("path filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CompactPrintsTableHeadersAndMatchValues()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLogWithClientIp(
            "iis/u_ex.log",
            TestLogData.W3cRowWithClientIp("2026-07-07", "12:00:01", "203.0.113.10", "GET", "/api/stats", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Time                 Status  IP", result.StdOut);
        Assert.Contains("500", result.StdOut);
        Assert.Contains("GET", result.StdOut);
        Assert.Contains("/api/stats", result.StdOut);
        Assert.Contains("203.0.113.10", result.StdOut);
    }

    [Fact]
    public async Task TableAliasEnablesCompactOutput()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/api/stats", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--table");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Time                 Status", result.StdOut);
    }

    [Fact]
    public async Task DetailedOutputRemainsDefault()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/api/stats", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("HTTP 500  GET /api/stats", result.StdOut);
        Assert.DoesNotContain("Time                 Status  IP", result.StdOut);
    }

    [Fact]
    public async Task TopDefaultsToTenUrlSummaryRows()
    {
        using var temp = new TempDirectory();
        var rows = Enumerable.Range(1, 11)
            .Select(index => TestLogData.W3cRow("2026-07-07", $"12:00:{index:00}", "GET", $"/url-{index}", "-", 500))
            .ToArray();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", rows);

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(10, CountSummaryUrlRows(result.StdOut));
    }

    [Fact]
    public async Task TopLimitsSummaryRows()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog(
            "iis/u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/first", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/second", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--top", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, CountSummaryUrlRows(result.StdOut));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public async Task InvalidTopReturnsOne(string value)
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--top", value);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--top must be a number between 1 and 100.", result.StdErr);
    }

    [Theory]
    [InlineData("status", "GROUPED BY STATUS", "500")]
    [InlineData("url", "GROUPED BY URL", "/api/stats")]
    [InlineData("user-agent", "GROUPED BY USER-AGENT", "Googlebot")]
    [InlineData("ip", "GROUPED BY IP", "203.0.113.10")]
    public async Task GroupByPrintsRequestedGrouping(string groupBy, string heading, string expectedValue)
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLogWithClientIp(
            "iis/u_ex.log",
            TestLogData.W3cRowWithClientIp("2026-07-07", "12:00:01", "203.0.113.10", "GET", "/api/stats", "-", 500, "Googlebot"));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--group-by", groupBy);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(heading, result.StdOut);
        Assert.Contains(expectedValue, result.StdOut);
    }

    [Fact]
    public async Task InvalidGroupByReturnsOne()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--group-by", "cookie");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Valid values: status, url, user-agent, ua, agent, ip, referer.", result.StdErr);
    }

    [Fact]
    public async Task TopLimitsGroupRows()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog(
            "iis/u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/first", "-", 500),
            TestLogData.W3cRow("2026-07-07", "12:00:02", "GET", "/second", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--group-by", "url", "--top", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, CountGroupedRows(result.StdOut));
    }

    [Fact]
    public async Task IpFilterAndDetailedOutputIncludeIp()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLogWithClientIp(
            "iis/u_ex.log",
            TestLogData.W3cRowWithClientIp("2026-07-07", "12:00:01", "203.0.113.10", "GET", "/api/stats", "-", 200),
            TestLogData.W3cRowWithClientIp("2026-07-07", "12:00:02", "198.51.100.20", "GET", "/api/other", "-", 200));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--ip", "203.0.113");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("IP:         203.0.113.10", result.StdOut);
        Assert.DoesNotContain("/api/other", result.StdOut);
    }

    [Fact]
    public async Task HintsShowStatusSubstatusAndWin32Hints()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog(
            "iis/u_ex.log",
            TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/broken", "-", 500, subStatus: "19", win32Status: "5"));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500", "--hints");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("500.19: IIS configuration error.", result.StdOut);
        Assert.Contains("Win32=5 means access denied.", result.StdOut);
    }

    [Fact]
    public async Task HintsAreNotPrintedByDefault()
    {
        using var temp = new TempDirectory();
        var iisLog = temp.WriteIisLog("iis/u_ex.log", TestLogData.W3cRow("2026-07-07", "12:00:01", "GET", "/broken", "-", 500));

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--iis-log", iisLog.FullName, "--status", "500");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Hint:", result.StdOut);
    }

    [Fact]
    public async Task StatusBotReturnsSuggestion()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteSearchCommandAsync(temp.Directory, "--status", "bot");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status code must be numeric: bot", result.StdErr);
        Assert.Contains("--user-agent bot", result.StdErr);
    }

    private static int CountSummaryUrlRows(string output)
    {
        return output.Split('\n').Count(line => line.Contains("x  /", StringComparison.Ordinal));
    }

    private static int CountGroupedRows(string output)
    {
        return output.Split('\n').Count(line => line.StartsWith("1x  /", StringComparison.Ordinal));
    }

    private sealed class FakeServiceProvider(ICursedShell curse) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(ICursedShell) ? curse : null;
        }
    }

    private sealed class FakeCursedShell(bool isEnabled) : ICursedShell
    {
        public List<string> Events { get; } = [];

        public bool IsEnabled { get; } = isEnabled;

        public int BlessingCharges => 0;

        public int FailureChancePercent => 0;

        public string Mood { get; private set; } = "quiet";

        public string? LastOmen => null;

        public void AddAmbientEvent(string message)
        {
            Events.Add(message);
        }

        public void ShiftMood(string mood)
        {
            Mood = mood;
        }

        public void AddBlessing(int charges, string? reason = null)
        {
        }
    }
}
