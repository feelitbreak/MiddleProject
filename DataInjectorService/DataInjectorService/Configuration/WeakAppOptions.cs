namespace DataInjectorService.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Strongly-typed configuration for the WeakApp external API client.
/// Bound from the "WeakApp" section in appsettings / environment variables.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Configuration binding target, exercised through the options validation tests.")]
public sealed class WeakAppOptions
{
    public const string SectionName = "WeakApp";

    /// <summary>Gets or sets the base URL of the WeakApp API (e.g. http://weak_app:8080).</summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key sent in the X-Api-Key request header.
    /// Deliberately not marked required: the value is supplied per-environment (docker-compose,
    /// user secrets or environment variables) and is empty in the committed settings files.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the polling interval in seconds between successive /meters calls.</summary>
    [Range(1, 86_400)]
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>Gets or sets the HTTP request timeout in seconds.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of retry attempts on transient failures.</summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    /// <summary>Gets or sets the base delay in seconds for the first retry (doubles each attempt).</summary>
    [Range(1, 300)]
    public int RetryBaseDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay in seconds to pause when the API returns HTTP 429 Too Many Requests.
    /// </summary>
    [Range(1, 3_600)]
    public int RateLimitDelaySeconds { get; set; } = 60;
}
