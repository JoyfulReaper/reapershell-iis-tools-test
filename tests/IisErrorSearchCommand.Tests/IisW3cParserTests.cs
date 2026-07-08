namespace IisErrorSearchCommand.Tests;

public sealed class IisW3cParserTests
{
    private readonly IisW3cParser _parser = new();

    [Fact]
    public void TryReadFieldsHeader_ReadsFields()
    {
        var parsed = _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        Assert.True(parsed);
        Assert.Equal(
            ["date", "time", "cs-method", "cs-uri-stem", "cs-uri-query", "sc-status", "sc-substatus", "sc-win32-status", "time-taken", "cs(User-Agent)", "cs(Referer)"],
            fields);
    }

    [Fact]
    public void TryParseRow_ParsesNormalRow()
    {
        _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        var parsed = _parser.TryParseRow(
            "2026-07-07 12:00:01 GET /api/stats - 500 0 64 123 Mozilla/5.0 -",
            fields,
            out var row);

        Assert.True(parsed);
        Assert.Equal("GET", row.Fields["cs-method"]);
        Assert.Equal("/api/stats", row.Fields["cs-uri-stem"]);
        Assert.Equal("500", row.Fields["sc-status"]);
    }

    [Fact]
    public void TryParseRow_ParsesTimestampFromDateAndTime()
    {
        _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        var parsed = _parser.TryParseRow(
            "2026-07-07 12:00:01 GET /api/stats - 500 0 64 123 Mozilla/5.0 -",
            fields,
            out var row);

        Assert.True(parsed);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 12, 0, 1, TimeSpan.Zero), row.TimestampUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#Comment: ignored")]
    public void TryParseRow_IgnoresCommentsAndBlankLines(string line)
    {
        _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        var parsed = _parser.TryParseRow(line, fields, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseRow_ReturnsFalseWhenValuesAreFewerThanFields()
    {
        _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        var parsed = _parser.TryParseRow("2026-07-07 12:00:01 GET /api/stats", fields, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseRow_SupportsQuotedValuesWithSpaces()
    {
        _parser.TryReadFieldsHeader(TestLogData.W3cHeader, out var fields);

        var parsed = _parser.TryParseRow(
            "2026-07-07 12:00:01 GET /api/stats - 500 0 64 123 \"Mozilla Bot/5.0\" \"https://example.test/search value\"",
            fields,
            out var row);

        Assert.True(parsed);
        Assert.Equal("Mozilla Bot/5.0", row.Fields["cs(User-Agent)"]);
        Assert.Equal("https://example.test/search value", row.Fields["cs(Referer)"]);
    }
}
