namespace GraphQLGatewayService.Api.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>The GraphQL endpoint's concurrency limit. Bound from the "RateLimiting" section.</summary>
[ExcludeFromCodeCoverage(Justification = "Configuration binding target.")]
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets how many operations may execute at once.</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 32;

    /// <summary>Gets or sets how many may wait for a permit before the rest get a 429.</summary>
    [Range(0, 100_000)]
    public int QueueLimit { get; set; } = 64;
}
