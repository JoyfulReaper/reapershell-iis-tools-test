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
