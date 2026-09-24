namespace NotificationService.Extensions;

using Microsoft.AspNetCore.Authentication;
using NotificationService.Authentication;
using NotificationService.Configuration;
using NotificationService.Contracts;
using NotificationService.HealthChecks;
using NotificationService.Messaging;
using NotificationService.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods that keep <c>Program.cs</c> declarative and
/// free of registration boilerplate. Single composition root, matching the sibling services.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Dependency injection wiring, exercised indirectly by every integration test.")]
public static class Extensions
{
    /// <summary>
    /// Registers the API-key scheme. The reverse proxy supplies the key on the negotiate and on
    /// every transport request alike, so a browser never holds it.
    /// </summary>
    public static void AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                configureOptions: null
            );

        services.AddAuthorization();
    }

    /// <summary>
    /// Registers a CORS policy that allows localhost (in development) and any explicitly configured
    /// origins, with credentials.
    /// </summary>
    /// <remarks>
    /// <see cref="CorsPolicyBuilder.AllowCredentials"/> is what a SignalR handshake needs, and the
    /// browser refuses it alongside a wildcard origin. Hence <c>SetIsOriginAllowed</c> rather than
    /// the <c>AllowAnyOrigin</c> this would otherwise be: the policy has to name the origins it
    /// admits, one at a time.
    /// </remarks>
    public static void AddCorsConfiguration(
        this IServiceCollection services,
        IConfigurationManager configuration
    )
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowOrigins",
                policy =>
                {
                    var allowLocalhost = configuration.GetValue("Cors:AllowLocalhost", true);
                    var allowedOrigins =
                        configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                    var normalizedAllowedOrigins = allowedOrigins.Select(origin =>
                        origin.Trim().TrimEnd('/')
                    );

                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                            {
                                return false;
                            }

                            if (
                                allowLocalhost
                                && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                return true;
                            }

                            return normalizedAllowedOrigins.Contains(origin.Trim().TrimEnd('/'));
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            );
        });
    }

    /// <summary>
    /// Registers Swagger/OpenAPI generation for the health probes.
    /// </summary>
    /// <remarks>
    /// The probes are all it can describe. A hub is a long-lived bidirectional connection with no
    /// per-call schema, so SignalR's endpoints carry no API explorer metadata and never appear here
    /// — the event contract lives in the README and in the Development test page instead.
    /// </remarks>
    public static void AddSwaggerGenConfiguration(this IServiceCollection services)
    {
        // Minimal APIs do not register the API explorer implicitly, and Swashbuckle's generator
        // cannot be constructed without it.
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "NotificationService", Version = "v1" });
        });
    }

    /// <summary>Registers the hub and the protocol its payloads are written with.</summary>
    public static void AddRealtime(this IServiceCollection services)
    {
        services
            .AddSignalR()
            // The same options the topic is decoded with, so the event reaches the browser spelled
            // as it arrived. The client contract is camelCase; stating it beats inheriting it.
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions = ReadingsPersistedMessage.SerializerOptions
            );
    }

    /// <summary>Registers the Kafka consumer and its liveness signal.</summary>
    public static void AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ConsumerHeartbeat>();
        services.AddHostedService<KafkaConsumerService>();
    }

    /// <summary>Registers liveness and readiness health checks.</summary>
    public static void AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<ConsumerLivenessHealthCheck>("consumer-loop", tags: ["live"])
            .AddCheck<ConsumerAssignmentHealthCheck>("consumer-assignment", tags: ["ready"]);
    }

    /// <summary>Registers the meter plus standard instrumentation, exported for Prometheus.</summary>
    public static void AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<NotificationMetrics>();

        var serviceVersion = typeof(Extensions).Assembly.GetName().Version?.ToString();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "notification-service",
                    serviceVersion: serviceVersion
                )
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(NotificationMetrics.MeterName)
                    .AddPrometheusExporter()
            )
            // No exporter: this exists to mint the ids the logs print and the headers carry.
            .WithTracing(tracing =>
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddSource(KafkaConsumerService.ActivitySourceName)
            );
    }
}
