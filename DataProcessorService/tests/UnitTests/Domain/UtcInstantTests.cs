namespace DataProcessorService.UnitTests.Domain;

using DataProcessorService.Domain.Common;

/// <summary>
/// Covers timestamp normalisation, which is load-bearing for idempotency.
/// <para>
/// The unique index that makes ingestion idempotent is on <c>(sensor_id, collected_at)</c>, and
/// PostgreSQL stores <c>timestamptz</c> at microsecond resolution while
/// <see cref="DateTimeOffset"/> carries 100-nanosecond ticks. If the value used for the in-memory
/// deduplication key were not truncated the same way the database truncates it, two representations
/// of the same instant would compare unequal in memory and equal in the index.
/// </para>
/// </summary>
public sealed class UtcInstantTests
{
    [Fact]
    public void Normalize_NonZeroOffset_ConvertsToUtc()
    {
        var local = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2));

        var normalized = UtcInstant.Normalize(local);

        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero), normalized);
    }

    [Fact]
    public void Normalize_NegativeOffset_ConvertsToUtc()
    {
        var local = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(-5));

        var normalized = UtcInstant.Normalize(local);

        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 17, 0, 0, TimeSpan.Zero), normalized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    public void Normalize_SubMicrosecondTicks_TruncatesTowardsTheMicrosecond(int subMicrosecondTicks)
    {
        // One tick is 100 nanoseconds, so ten ticks make the microsecond PostgreSQL stores.
        // A whole second is a multiple of ten ticks, which makes the remainder exactly the
        // sub-microsecond part being added here.
        var value = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(
            subMicrosecondTicks
        );

        var normalized = UtcInstant.Normalize(value);

        Assert.Equal(0, normalized.Ticks % 10);
        Assert.Equal(value.Ticks - subMicrosecondTicks, normalized.Ticks);
        Assert.True(normalized <= value);
    }

    [Fact]
    public void Normalize_AlreadyNormalized_IsUnchanged()
    {
        var value = UtcInstant.Normalize(DateTimeOffset.UtcNow);

        Assert.Equal(value, UtcInstant.Normalize(value));
    }

    [Fact]
    public void Normalize_SameInstantDifferentOffsets_ProducesEqualKeys()
    {
        var berlin = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var utc = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(UtcInstant.Normalize(berlin), UtcInstant.Normalize(utc));
    }
}
