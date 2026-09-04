namespace DataProcessorService.Domain.Common;

using System.Collections.Frozen;
using System.Text.Json;

/// <summary>
/// The canonical snake_case spelling of an enum, derived from its members.
/// <para>
/// One vocabulary serves the whole system: the Kafka wire contract, the stored discriminator, JSON
/// responses, and query-string parameters. That last one is why this exists as a shared helper
/// rather than serializer configuration --- minimal API parameter binding does not consult the JSON
/// serializer, so without an explicit conversion the API would accept <c>AirQuality</c> in a query
/// string while emitting <c>air_quality</c> in the response body.
/// </para>
/// </summary>
/// <typeparam name="TEnum">The enum being named.</typeparam>
public static class EnumVocabulary<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> NamesByValue = Enum.GetValues<TEnum>()
        .ToFrozenDictionary(
            value => value,
            value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString())
        );

    private static readonly FrozenDictionary<string, TEnum> ValuesByName =
        NamesByValue.ToFrozenDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase
        );

    /// <summary>
    /// Gets every accepted spelling, comma separated. Intended for error messages, so that a
    /// rejected request says what it should have said.
    /// </summary>
    public static string AcceptedValues { get; } = string.Join(", ", NamesByValue.Values.Order());

    /// <summary>Converts a value to its canonical spelling.</summary>
    /// <param name="value">The value to name.</param>
    /// <returns>The canonical spelling.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined member.</exception>
    public static string ToName(TEnum value) =>
        NamesByValue.TryGetValue(value, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Not a defined {typeof(TEnum).Name}."
            );

    /// <summary>Parses a canonical spelling, case-insensitively.</summary>
    /// <param name="name">The candidate spelling.</param>
    /// <param name="value">The parsed value, when recognised.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> is recognised.</returns>
    public static bool TryParse(string? name, out TEnum value)
    {
        if (name is null)
        {
            value = default;
            return false;
        }

        return ValuesByName.TryGetValue(name, out value);
    }
}
