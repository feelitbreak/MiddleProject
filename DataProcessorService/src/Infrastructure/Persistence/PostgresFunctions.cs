namespace DataProcessorService.Infrastructure.Persistence;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// PostgreSQL functions mapped into LINQ, so that aggregation stays composable instead of falling
/// back to hand-written SQL.
/// <para>
/// The Npgsql provider does not surface <c>date_trunc</c> as a <c>DbFunction</c> --- it appears
/// only as an internal translation of <c>DateTime.Date</c> --- so it is declared here and mapped in
/// <see cref="MeterReadingsDbContext.OnModelCreating"/>.
/// </para>
/// <para>
/// Mapping it this way rather than interpolating it into SQL also means the unit and time zone
/// arrive as bound parameters, so no caller-supplied value is ever concatenated into a statement.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage(Justification = "A marker for EF Core translation, never executed in process.")]
public static class PostgresFunctions
{
    /// <summary>
    /// Truncates a timestamp to the given unit, in the given time zone.
    /// <para>
    /// The time zone argument is not optional by accident. <c>date_trunc</c> on a
    /// <c>timestamptz</c> truncates in the <em>session</em> time zone, so omitting it would make
    /// every daily bucket depend on the server's configured zone.
    /// </para>
    /// </summary>
    /// <param name="unit">The truncation unit, for example <c>hour</c> or <c>day</c>.</param>
    /// <param name="source">The timestamp to truncate.</param>
    /// <param name="timeZone">The time zone to truncate in.</param>
    /// <returns>The truncated timestamp.</returns>
    /// <exception cref="NotSupportedException">Always, when called outside a LINQ query.</exception>
    public static DateTimeOffset DateTrunc(string unit, DateTimeOffset source, string timeZone) =>
        throw new NotSupportedException(
            "This method is a marker for EF Core translation and cannot be called directly."
        );
}
