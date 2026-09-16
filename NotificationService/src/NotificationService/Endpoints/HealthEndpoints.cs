namespace NotificationService.Endpoints;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.CodeAnalysis;

/// <summary>The result of one health check.</summary>
public sealed class HealthCheckEntryDto
{
    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Description { get; init; }

    public double DurationMs { get; init; }
}

/// <summary>The overall health report.</summary>
public sealed class HealthReportDto
{
    public string Status { get; init; } = string.Empty;

    public double TotalDurationMs { get; init; }

    public IReadOnlyList<HealthCheckEntryDto> Checks { get; init; } = [];
}

/// <summary>
/// Liveness and readiness probes. Handlers over <see cref="HealthCheckService"/> rather than
/// <c>MapHealthChecks</c>, which writes only a bare status string.
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Endpoint wiring only; the behaviour lives in the health checks those endpoints run."
)]
public static class HealthEndpoints
{
    /// <summary>
    /// Maps the probes. Liveness covers the consumer loop, without which the service accepts
    /// connections and then pushes nothing; readiness additionally covers partition assignment.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var health = app.MapGroup("/health");

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
            .WithName("HealthLive");

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
            .WithName("HealthReady");

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
