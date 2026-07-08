namespace IisErrorSearchCommand.Tests;

public sealed class IisToolsVersionCommandTests
{
    [Fact]
    public async Task NoArgsReturnsZeroAndPrintsMetadata()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory);

        Assert.Equal(0, result.ExitCode);
        AssertVersionMetadata(result.StdOut);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public async Task HelpArgsReturnZero(string arg)
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory, arg);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("iis-tools-version [--help]", result.StdOut);
    }

    [Fact]
    public async Task CursedNoArgsAddsAmbientEvents()
    {
        using var temp = new TempDirectory();
        var curse = new FakeCursedShell(isEnabled: true);

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory, new FakeServiceProvider(curse));

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(curse.Events);
    }

    [Fact]
    public async Task CursedHelpDoesNotAddAmbientEvents()
    {
        using var temp = new TempDirectory();
        var curse = new FakeCursedShell(isEnabled: true);

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory, new FakeServiceProvider(curse), "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(curse.Events);
    }

    [Fact]
    public async Task CursedInvalidArgDoesNotAddAmbientEvents()
    {
        using var temp = new TempDirectory();
        var curse = new FakeCursedShell(isEnabled: true);

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory, new FakeServiceProvider(curse), "--bad");

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(curse.Events);
    }

    [Fact]
    public async Task UnknownArgReturnsOne()
    {
        using var temp = new TempDirectory();

        var result = await CommandTestHost.ExecuteVersionCommandAsync(temp.Directory, "--wat");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown option: --wat", result.StdErr);
    }

    private static void AssertVersionMetadata(string output)
    {
        Assert.Contains("IIS Tools Command Pack", output);
        Assert.Contains("Assembly: IisErrorSearchCommand", output);
        Assert.Contains("Version:", output);
        Assert.Contains("Git Branch:", output);
        Assert.Contains("Git Commit:", output);
        Assert.Contains("Build UTC:", output);
        Assert.Contains("Assembly Path:", output);
    }
}
