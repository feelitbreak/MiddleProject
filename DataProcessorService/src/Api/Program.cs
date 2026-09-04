namespace DataProcessorService.Api;

using DataProcessorService.Api.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Application entry point.
/// <para>
/// Written as an explicit class with a <c>Main</c> method rather than top-level statements so that
/// <see cref="ExcludeFromCodeCoverageAttribute"/> is visible to static analysis, matching the
/// sibling DataInjectorService.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>Builds, migrates and runs the host.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task that completes when the host shuts down.</returns>
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        try
        {
            Log.Information("Starting DataProcessorService");
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog(
                (context, services, configuration) =>
                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .WriteTo.Console(
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
                        )
            );

            builder.Services.AddCorsConfiguration(builder.Configuration);
            builder.Services.AddSwaggerGenConfiguration();
            builder.Services.AddPersistence(builder.Configuration);
            builder.Services.AddCqrsHandlers();
            builder.Services.AddMessaging(builder.Configuration);
            builder.Services.AddHealthCheckConfiguration();
            builder.Services.AddObservability();

            var app = builder.Build();

            await app.ApplyMigrationsIfConfiguredAsync();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowOrigins");
            app.MapPrometheusScrapingEndpoint();

            app.MapHealthChecks(
                "/health/live",
                new() { Predicate = check => check.Tags.Contains("live") }
            );

            app.MapHealthChecks(
                "/health/ready",
                new()
                {
                    Predicate = check => check.Tags.Contains("ready"),
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
                    },
                }
            );

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "DataProcessorService terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
