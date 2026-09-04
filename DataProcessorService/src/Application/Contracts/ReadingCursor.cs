namespace DataProcessorService.Application.Contracts;

using System.Buffers.Text;
using System.Globalization;
using System.Text;

/// <summary>
/// A position in the descending <c>(collected_at, id)</c> ordering.
/// <para>
/// The identifier is part of the key, not decoration: collection timestamps are stamped per poll,
/// so a whole batch of readings shares one instant. Ordering by timestamp alone would leave those
/// rows in an arbitrary order between queries, and a page boundary landing inside such a group
/// would skip or repeat rows.
/// </para>
/// </summary>
/// <param name="collectedAt">The collection instant of the last item on the previous page.</param>
/// <param name="id">The identifier of the last item on the previous page.</param>
public sealed class ReadingCursor(DateTimeOffset collectedAt, long id)
{
    /// <summary>Gets the collection instant of the last item on the previous page.</summary>
    public DateTimeOffset CollectedAt { get; } = collectedAt;

    /// <summary>Gets the identifier of the last item on the previous page.</summary>
    public long Id { get; } = id;

    /// <summary>Encodes the cursor into a URL-safe opaque token.</summary>
    /// <returns>The encoded cursor.</returns>
    public string Encode() =>
        Base64Url.EncodeToString(
            Encoding.UTF8.GetBytes(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{this.CollectedAt.UtcTicks}:{this.Id}"
                )
            )
        );

    /// <summary>
    /// Decodes a cursor produced by <see cref="Encode"/>.
    /// </summary>
    /// <param name="value">The encoded cursor.</param>
    /// <param name="cursor">The decoded cursor, when the token is well-formed.</param>
    /// <returns><see langword="true"/> when the token could be decoded.</returns>
    public static bool TryDecode(string? value, out ReadingCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        byte[] decoded;

        try
        {
            decoded = Base64Url.DecodeFromChars(value);
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = Encoding.UTF8.GetString(decoded).Split(':');

        if (
            parts.Length != 2
            || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks)
            || !long.TryParse(parts[1], CultureInfo.InvariantCulture, out var id)
            || ticks < 0
            || ticks > DateTimeOffset.MaxValue.UtcTicks
        )
        {
            return false;
        }

        cursor = new ReadingCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
