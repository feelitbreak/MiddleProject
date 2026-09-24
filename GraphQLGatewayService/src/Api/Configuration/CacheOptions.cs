namespace GraphQLGatewayService.Api.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>How long cached query results stay usable. Bound from the "Cache" section.</summary>
[ExcludeFromCodeCoverage(Justification = "Configuration binding target.")]
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Gets or sets the aggregate series lifetime, in seconds. Below the injector's polling
    /// interval keeps a series at most one poll behind.
    /// </summary>
    [Range(1, 3_600)]
    public int AggregateSeconds { get; set; } = 20;

    /// <summary>Gets or sets the sensor and location catalogue lifetime, in seconds.</summary>
    [Range(1, 3_600)]
    public int CatalogueSeconds { get; set; } = 60;
}
