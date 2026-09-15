namespace GraphQLGatewayService.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// PostgreSQL connection settings, bound from the "Database" section. No migration switch:
/// DataProcessorService owns this schema.
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Configuration binding target, exercised through the options validation tests."
)]
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Gets or sets the Npgsql connection string. Must pin <c>Options=-c timezone=UTC</c>, or
    /// <c>date_trunc</c> buckets aggregations in the server's local time zone.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 3_600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(0, 20)]
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Gets or sets the ceiling on the retry delay, in seconds.</summary>
    [Range(1, 300)]
    public int MaxRetryDelaySeconds { get; set; } = 10;
}
