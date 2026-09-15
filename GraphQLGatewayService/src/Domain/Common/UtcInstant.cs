namespace GraphQLGatewayService.Domain.Common;

/// <summary>Normalises timestamps to the offset and precision the database actually stores.</summary>
public static class UtcInstant
{
    private const long TicksPerMicrosecond = 10;

    /// <summary>
    /// Converts to UTC and truncates to microseconds. Both matter: Npgsql refuses a non-zero offset
    /// on a <c>timestamptz</c>, and that type stores microseconds while
    /// <see cref="DateTimeOffset"/> carries 100-nanosecond ticks.
    /// </summary>
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TicksPerMicrosecond), TimeSpan.Zero);
    }
}
