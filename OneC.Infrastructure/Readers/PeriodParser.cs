namespace OneC.Infrastructure.Readers;

/// <summary>
///     Parses a "changed since" period string into a date range.
///     Supports relative periods ("45d", "2w", "6h", "30m", "45s")
///     and absolute ranges ("2026-07-01:2026-07-31").
/// </summary>
public static class PeriodParser
{
    /// <summary>
    ///     Parses a period string into a (from, to) date range.
    ///     Returns (null, null) for null/empty input or invalid format.
    /// </summary>
    public static (DateTime? From, DateTime? To) Parse(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return (null, null);
        }

        // Absolute range: "2026-07-01:2026-07-31"
        var parts = period.Split(':');
        if (parts.Length == 2 &&
            DateOnly.TryParse(parts[0], out var fromDate) &&
            DateOnly.TryParse(parts[1], out var toDate))
        {
            return (
                fromDate.ToDateTime(TimeOnly.MinValue),
                toDate.ToDateTime(TimeOnly.MaxValue));
        }

        // Relative: "45d", "2w", "6h", "30m", "45s"
        if (period.Length > 1 &&
            int.TryParse(period[..^1], out var amount) &&
            "smhdw".Contains(period[^1]))
        {
            var now = DateTime.Now;
            var from = period[^1] switch
            {
                's' => now.AddSeconds(-amount),
                'm' => now.AddMinutes(-amount),
                'h' => now.AddHours(-amount),
                'd' => now.AddDays(-amount),
                'w' => now.AddDays(-amount * 7),
                _ => now,
            };

            return (from, now);
        }

        return (null, null);
    }
}