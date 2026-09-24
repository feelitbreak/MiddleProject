namespace DataProcessorService.Api;

using DataProcessorService.Api.Endpoints;
using DataProcessorService.Api.Extensions;
using Microsoft.Extensions.Hosting;
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
[ExcludeFromCodeCoverage(Justification = "Host composition and start-up wiring.")]
public static class Program
{
    /// <summary>Builds, migrates and runs the host.</summary>
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
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
                        )
            );

            builder.Services.AddCorsConfiguration(builder.Configuration);
            builder.Services.AddApiKeyAuthentication(builder.Configuration);
            builder.Services.AddJsonConfiguration();
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

            // CORS first, so a preflight is answered before authorization: it carries no key.
            app.UseCors("AllowOrigins");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapReadingEndpoints();

            // Anonymous on purpose: Prometheus and the orchestrator probes carry no key.
            app.MapPrometheusScrapingEndpoint();

            app.MapHealthEndpoints();

            await app.RunAsync();
        }
        catch (HostAbortedException)
        {
            // Expected: the EF Core design-time tooling intercepts host construction and aborts
            // once it has the service provider. Letting it through keeps `dotnet ef` commands from
            // reporting a fatal error for what is ordinary control flow.
            throw;
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
