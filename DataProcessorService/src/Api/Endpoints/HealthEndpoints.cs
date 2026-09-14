namespace DataProcessorService.Api.Endpoints;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.CodeAnalysis;

/// <summary>The result of one health check.</summary>
public sealed class HealthCheckEntryDto
{
    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Description { get; init; }

    public double DurationMs { get; init; }

    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>The overall health report.</summary>
public sealed class HealthReportDto
{
    public string Status { get; init; } = string.Empty;

    public double TotalDurationMs { get; init; }

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
[ExcludeFromCodeCoverage(Justification = "Endpoint wiring only; the behaviour lives in the handlers those endpoints dispatch to.")]
public static class HealthEndpoints
{
    /// <summary>Maps the liveness and readiness endpoints.</summary>
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
