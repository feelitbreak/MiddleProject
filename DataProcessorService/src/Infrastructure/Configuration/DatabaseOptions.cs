namespace DataProcessorService.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Strongly-typed configuration for the PostgreSQL connection.
/// Bound from the "Database" section in appsettings / environment variables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DatabaseOptions
{
    /// <summary>Configuration section this type binds from.</summary>
    public const string SectionName = "Database";

    /// <summary>Gets or sets the Npgsql connection string.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the per-command timeout, in seconds.</summary>
    [Range(1, 3_600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether pending migrations are applied during start-up.
    /// <para>
    /// Convenient for local docker-compose and CI, where the service owns its database outright.
    /// Applied from the composition root before the host starts serving, never from a hosted
    /// service: migrating inside a hosted service races the readiness probe.
    /// </para>
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Gets or sets how many times a transient connection failure is retried.</summary>
    [Range(0, 20)]
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Gets or sets the ceiling on the connection retry delay, in seconds.</summary>
    [Range(1, 300)]
    public int MaxRetryDelaySeconds { get; set; } = 10;
}
