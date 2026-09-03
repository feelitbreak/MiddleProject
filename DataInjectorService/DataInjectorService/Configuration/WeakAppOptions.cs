namespace DataInjectorService.Configuration;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Strongly-typed configuration for the WeakApp external API client.
/// Bound from the "WeakApp" section in appsettings / environment variables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WeakAppOptions
{
    public const string SectionName = "WeakApp";

    /// <summary>Gets or sets the base URL of the WeakApp API (e.g. http://weak_app:8080).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the API key sent in the X-Api-Key request header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the polling interval in seconds between successive /meters calls.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>Gets or sets the HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of retry attempts on transient failures.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Gets or sets the base delay in seconds for the first retry (doubles each attempt).</summary>
    public int RetryBaseDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay in seconds to pause when the API returns HTTP 429 Too Many Requests.
    /// </summary>
    public int RateLimitDelaySeconds { get; set; } = 60;
}
