namespace NotificationService;

using NotificationService.Endpoints;
using NotificationService.Extensions;
using NotificationService.Hubs;
using Serilog;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Application entry point. An explicit class rather than top-level statements so that
/// <see cref="ExcludeFromCodeCoverageAttribute"/> is visible to static analysis.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Host composition and start-up wiring.")]
public static class Program
{
    /// <summary>Builds and runs the host.</summary>
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

        try
        {
            Log.Information("Starting NotificationService");
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
            builder.Services.AddSwaggerGenConfiguration();
            builder.Services.AddRealtime();
            builder.Services.AddMessaging(builder.Configuration);
            builder.Services.AddHealthCheckConfiguration();
            builder.Services.AddObservability();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();

                // wwwroot/index.html: a page to watch the hub from, since SignalR has no equivalent
                // of Swagger UI. Development only, like the gateway's Nitro IDE.
                app.UseDefaultFiles();
                app.UseStaticFiles();
            }

            // Before the hub: the WebSocket handshake is a cross-origin request like any other.
            app.UseCors("AllowOrigins");
            app.UseAuthentication();
            app.UseAuthorization();

            // One instance only. Scaling out needs a Redis backplane -- a second instance consumes
            // its own share of the partitions and broadcasts to its own connections, so clients
            // attached elsewhere silently never hear about those events rather than failing loudly.
            //
            // The guard covers the negotiate and every transport request: SignalR re-authorises
            // each one, so a key accepted only at negotiate would fail the upgrade.
            app.MapHub<ReadingsHub>("/hubs/readings").RequireAuthorization();

            // Anonymous on purpose: Prometheus and the orchestrator probes carry no key.
            app.MapPrometheusScrapingEndpoint();
            app.MapHealthEndpoints();

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "NotificationService terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
