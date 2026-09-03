using DataInjectorService.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Diagnostics.CodeAnalysis;

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

/// <summary>
/// Entry point marker for the top-level statements above. Excluded from coverage:
/// this file is composition-root wiring, exercised end-to-end rather than by unit tests.
/// </summary>
[ExcludeFromCodeCoverage]
public static partial class Program { }
