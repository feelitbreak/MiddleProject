namespace DataProcessorService.Infrastructure.Persistence;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// PostgreSQL functions mapped into LINQ so aggregation stays composable. The Npgsql provider does
/// not surface <c>date_trunc</c> as a <c>DbFunction</c>, so it is declared here and mapped in
/// <see cref="MeterReadingsDbContext.OnModelCreating"/>. Mapping it also means the unit and time
/// zone arrive as bound parameters rather than SQL literals.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "A marker for EF Core translation, never executed in process.")]
public static class PostgresFunctions
{
    /// <summary>
    /// Truncates a timestamp to the given unit, in the given time zone. The time zone is not
    /// optional: <c>date_trunc</c> on a <c>timestamptz</c> truncates in the session time zone, so
    /// omitting it would make every bucket depend on server configuration.
    /// </summary>
    public static DateTimeOffset DateTrunc(string unit, DateTimeOffset source, string timeZone) =>
        throw new NotSupportedException("Only callable inside a LINQ query.");
}
