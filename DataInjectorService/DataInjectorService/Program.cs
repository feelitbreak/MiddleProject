namespace DataInjectorService;

using DataInjectorService.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Composition root for the service. Excluded from coverage: this type is wiring only.
/// <para>
/// It uses an explicit entry point rather than top-level statements because the Sonar analyzer
/// resolves <see cref="ExcludeFromCodeCoverageAttribute"/> syntactically and cannot associate it
/// with top-level statements, which hang off the compilation unit rather than off the class
/// declaration carrying the attribute (sonar-dotnet#9562).
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>Builds, configures and runs the web application.</summary>
    /// <param name="args">Command-line arguments forwarded to the host builder.</param>
    /// <returns>A task that completes when the host shuts down.</returns>
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        try
        {
            Log.Information("Starting DataInjectorService");

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
            builder.Services.AddControllers();
            builder.Services.AddSwaggerGenConfiguration();
            builder.Services.AddDataInjectorServices(builder.Configuration);
            builder.Services.AddHealthCheckConfiguration();
            builder.Services.AddObservability();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowOrigins");
            app.UseAuthorization();
            app.MapControllers();

            // Local/dev only: not restricted to internal networks here.
            app.MapPrometheusScrapingEndpoint();

            // Liveness: the service process is up.
            app.MapHealthChecks(
                "/health/live",
                new()
                {
                    Predicate = _ => false,
                    ResultStatusCodes = { [HealthStatus.Healthy] = StatusCodes.Status200OK },
                }
            );

            // Readiness: liveness + WeakApp reachability.
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
            Log.Fatal(ex, "DataInjectorService terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
