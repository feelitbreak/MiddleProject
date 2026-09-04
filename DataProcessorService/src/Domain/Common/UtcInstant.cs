namespace DataProcessorService.Domain.Common;

/// <summary>
/// Normalises collection timestamps to the precision and offset the database actually stores.
/// </summary>
public static class UtcInstant
{
    /// <summary>Ticks per microsecond. One tick is 100 nanoseconds.</summary>
    private const long TicksPerMicrosecond = 10;

    /// <summary>
    /// Converts a timestamp to UTC and truncates it to microsecond precision.
    /// <para>
    /// Both halves matter. Npgsql refuses outright to write a <see cref="DateTimeOffset"/> with a
    /// non-zero offset to a <c>timestamptz</c> column, and <c>timestamptz</c> stores microseconds
    /// while <see cref="DateTimeOffset"/> carries 100-nanosecond ticks. Without truncation an
    /// in-memory deduplication key computed from the raw value would disagree with the unique
    /// index that the database enforces on the stored, truncated value.
    /// </para>
    /// </summary>
    /// <param name="value">The timestamp to normalise.</param>
    /// <returns>The equivalent instant in UTC, truncated to microseconds.</returns>
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var truncatedTicks = utc.Ticks - (utc.Ticks % TicksPerMicrosecond);
        return new DateTimeOffset(truncatedTicks, TimeSpan.Zero);
    }
}
