namespace GraphQLGatewayService.Api;

using GraphQLGatewayService.Api.Endpoints;
using GraphQLGatewayService.Api.Extensions;
using HotChocolate.AspNetCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Diagnostics.CodeAnalysis;

// The class and its namespace share a name, so the class needs one to be referred to.
using ApiExtensions = Extensions.Extensions;

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
            Log.Information("Starting GraphQLGatewayService");
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
            builder.Services.AddPersistence(builder.Configuration);
            builder.Services.AddGraphQLApi(builder.Configuration, builder.Environment);
            builder.Services.AddRequestLimiting();
            builder.Services.AddHealthCheckConfiguration();
            builder.Services.AddObservability();

            var app = builder.Build();

            // CORS first, so a preflight is answered before authorization: it carries no key.
            app.UseCors("AllowOrigins");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapGraphQL()
                .WithOptions(
                    (GraphQLServerOptions options) =>
                    {
                        // Development conveniences, gated like introspection in AddGraphQLApi.
                        options.Tool.Enable = app.Environment.IsDevelopment();
                        options.EnableSchemaRequests = app.Environment.IsDevelopment();

                        // A GET query is cacheable and triggerable by a plain cross-origin link.
                        options.EnableGetRequests = false;
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(ApiExtensions.GraphQLRateLimitPolicy);

            // Anonymous on purpose: Prometheus and the orchestrator probes carry no key.
            app.MapPrometheusScrapingEndpoint();
            app.MapHealthEndpoints();

            // Serves normally unless args name a schema command, which is how CI exports the SDL.
            await app.RunWithGraphQLCommandsAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "GraphQLGatewayService terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
