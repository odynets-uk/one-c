using OneC.Infrastructure.Readers;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="PeriodParser" />.
/// </summary>
public class PeriodParserTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsNullRange()
    {
        var (from, to) = PeriodParser.Parse(null);
        Assert.Null(from);
        Assert.Null(to);

        (from, to) = PeriodParser.Parse("");
        Assert.Null(from);
        Assert.Null(to);

        (from, to) = PeriodParser.Parse("   ");
        Assert.Null(from);
        Assert.Null(to);
    }

    [Fact]
    public void Parse_RelativeDays_ReturnsFromDate()
    {
        var before = DateTime.Now.AddDays(-45).AddSeconds(-5);
        var (from, to) = PeriodParser.Parse("45d");

        Assert.NotNull(from);
        Assert.NotNull(to);
        Assert.True(from >= before, $"Expected from >= {before}, got {from}");
        Assert.True(from <= DateTime.Now, $"Expected from <= now, got {from}");
        Assert.True(to >= DateTime.Now.AddSeconds(-1), $"Expected to >= now, got {to}");
    }

    [Fact]
    public void Parse_RelativeWeeks_ReturnsFromDate()
    {
        var before = DateTime.Now.AddDays(-14).AddSeconds(-5);
        var (from, _) = PeriodParser.Parse("2w");

        Assert.NotNull(from);
        Assert.True(from >= before, $"Expected from >= {before}, got {from}");
    }

    [Fact]
    public void Parse_RelativeHours_ReturnsFromDate()
    {
        var before = DateTime.Now.AddHours(-6).AddSeconds(-5);
        var (from, _) = PeriodParser.Parse("6h");

        Assert.NotNull(from);
        Assert.True(from >= before, $"Expected from >= {before}, got {from}");
    }

    [Fact]
    public void Parse_AbsoluteRange_ReturnsRange()
    {
        var (from, to) = PeriodParser.Parse("2026-07-01:2026-07-31");

        Assert.NotNull(from);
        Assert.NotNull(to);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0), from);
        Assert.Equal(new DateOnly(2026, 7, 31), DateOnly.FromDateTime(to.Value));
    }

    [Fact]
    public void Parse_Invalid_ReturnsNullRange()
    {
        var (from, to) = PeriodParser.Parse("invalid");
        Assert.Null(from);
        Assert.Null(to);

        (from, to) = PeriodParser.Parse("45x");
        Assert.Null(from);
        Assert.Null(to);

        (from, to) = PeriodParser.Parse("2026-13-99:2026-07-31");
        Assert.Null(from);
        Assert.Null(to);
    }
}