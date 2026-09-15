namespace GraphQLGatewayService.Infrastructure.Persistence;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// PostgreSQL functions mapped into LINQ. Npgsql does not surface <c>date_trunc</c> as a
/// <c>DbFunction</c>, so it is declared here and mapped in
/// <see cref="MeterReadingsDbContext.OnModelCreating"/>, which also makes its arguments bound
/// parameters rather than SQL literals.
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "A marker for EF Core translation, never executed in process."
)]
public static class PostgresFunctions
{
    /// <summary>
    /// Truncates a timestamp to the given unit. The time zone is not optional: on a
    /// <c>timestamptz</c> this otherwise truncates in the session time zone.
    /// </summary>
    public static DateTimeOffset DateTrunc(string unit, DateTimeOffset source, string timeZone) =>
        throw new NotSupportedException("Only callable inside a LINQ query.");
}
