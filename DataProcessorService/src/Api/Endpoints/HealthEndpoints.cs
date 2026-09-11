namespace DataProcessorService.Api.Endpoints;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.CodeAnalysis;

/// <summary>The result of one health check.</summary>
public sealed class HealthCheckEntryDto
{
    /// <summary>Gets the check's registered name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the status: Healthy, Degraded or Unhealthy.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the check's own description of the result.</summary>
    public string? Description { get; init; }

    /// <summary>Gets how long the check took, in milliseconds.</summary>
    public double DurationMs { get; init; }

    /// <summary>Gets any diagnostic values the check reported.</summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>The overall health report.</summary>
public sealed class HealthReportDto
{
    /// <summary>Gets the worst status across every check.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets how long the whole report took, in milliseconds.</summary>
    public double TotalDurationMs { get; init; }

    /// <summary>Gets the individual check results.</summary>
    public IReadOnlyList<HealthCheckEntryDto> Checks { get; init; } = [];
}

/// <summary>
/// Liveness and readiness probes.
/// <para>
/// Mapped as ordinary handlers over <see cref="HealthCheckService"/> rather than with
/// <c>MapHealthChecks</c>. That extension writes a bare status string and, because it registers a
/// raw request delegate with no method to describe, its endpoints never reach the API explorer and
/// so never appear in Swagger. Going through the service directly fixes both: the probes are
/// documented alongside everything else, and they return which check failed instead of one word.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class HealthEndpoints
{
    /// <summary>Maps the liveness and readiness endpoints.</summary>
    /// <param name="app">The application to map onto.</param>
    /// <returns>The application, for chaining.</returns>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        var health = app.MapGroup("/health").WithTags("Health");

        health
            .MapGet(
                "/live",
                async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
                    Respond(
                        await healthChecks.CheckHealthAsync(
                            check => check.Tags.Contains("live"),
                            cancellationToken
                        )
                    )
            )
            .WithName("HealthLive")
            .WithSummary("Liveness probe.")
            .WithDescription(
                "Unhealthy when the Kafka consumer loop has not completed an iteration recently, "
                    + "which catches a wedged or evicted consumer that a plain process check would "
                    + "report as fine."
            )
            .Produces<HealthReportDto>()
            .Produces<HealthReportDto>(StatusCodes.Status503ServiceUnavailable);

        health
            .MapGet(
                "/ready",
                async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
                    Respond(
                        await healthChecks.CheckHealthAsync(
                            check => check.Tags.Contains("ready"),
                            cancellationToken
                        )
                    )
            )
            .WithName("HealthReady")
            .WithSummary("Readiness probe.")
            .WithDescription(
                "Checks the database connection and that the consumer owns partitions. Degraded, "
                    + "and still 200, while a rebalance leaves it briefly unassigned."
            )
            .Produces<HealthReportDto>()
            .Produces<HealthReportDto>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static IResult Respond(HealthReport report)
    {
        var body = new HealthReportDto
        {
            Status = report.Status.ToString(),
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Checks =
            [
                .. report.Entries.Select(entry => new HealthCheckEntryDto
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    DurationMs = entry.Value.Duration.TotalMilliseconds,
                    Data = entry.Value.Data.Count == 0 ? null : entry.Value.Data,
                }),
            ],
        };

        // Degraded stays a 200: it reports a condition worth seeing, not one worth taking the
        // instance out of rotation for.
        return report.Status == HealthStatus.Unhealthy
            ? Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(body);
    }
}
