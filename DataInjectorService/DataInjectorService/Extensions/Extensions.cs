namespace DataInjectorService.Extensions;

using DataInjectorService.Configuration;
using DataInjectorService.Services;
using DataInjectorService.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods that keep <c>Program.cs</c>
/// declarative and free of registration boilerplate.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Dependency injection wiring, exercised indirectly by every integration test.")]
public static class Extensions
{
    /// <summary>Registers Swagger/OpenAPI generation for the service.</summary>
    public static void AddSwaggerGenConfiguration(this IServiceCollection services)
    {
        // Controllers bring their own API explorer but minimal APIs do not, so without this the
        // health probes would never reach Swagger.
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "DataInjectorService", Version = "v1" });
        });
    }

    /// <summary>
    /// Registers a CORS policy that allows localhost (in development) and any explicitly
    /// configured origins.
    /// </summary>
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
                        .AllowAnyMethod();
                }
            );
        });
    }

    /// <summary>
    /// Registers strongly-typed configuration options, a named resilient HTTP client for
    /// WeakApp (used via <see cref="IHttpClientFactory"/>), the <see cref="IWeakAppService"/>
    /// singleton, the Kafka producer singleton, and the background polling hosted service.
    /// </summary>
    public static void AddDataInjectorServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<WeakAppOptions>()
            .Bind(configuration.GetSection(WeakAppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var weakAppOptions =
            configuration.GetSection(WeakAppOptions.SectionName).Get<WeakAppOptions>() ?? new();

        // Named HTTP client consumed by WeakAppService via IHttpClientFactory.
        // The standard resilience pipeline provides:
        //   retry (exponential back-off + jitter) → circuit breaker → attempt timeout.
        // 429 responses are handled explicitly in WeakAppService and are not retried here.
        services
            .AddHttpClient(
                "WeakApp",
                client =>
                {
                    client.BaseAddress = new(weakAppOptions.BaseUrl.TrimEnd('/'));
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    client.DefaultRequestHeaders.Add("X-Api-Key", weakAppOptions.ApiKey);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                }
            )
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = weakAppOptions.RetryCount;
                options.Retry.Delay = TimeSpan.FromSeconds(weakAppOptions.RetryBaseDelaySeconds);
                options.Retry.UseJitter = true;

                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(
                    weakAppOptions.TimeoutSeconds
                );

                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.FailureRatio = 0.8;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });

        services.AddSingleton<IWeakAppService, WeakAppService>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddHostedService<MeterPollingService>();
    }

    /// <summary>
    /// Registers ASP.NET Core health checks: liveness (always healthy) and a readiness
    /// check that probes the WeakApp <c>/health</c> endpoint.
    /// </summary>
    public static void AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<WeakAppHealthCheck>("weakapp", tags: ["ready"]);
    }

    /// <summary>
    /// Registers the custom <see cref="DataInjectorMetrics"/> meter along with ASP.NET Core,
    /// HttpClient and runtime instrumentation, exported for Prometheus to scrape.
    /// </summary>
    public static void AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<DataInjectorMetrics>();

        var serviceVersion = typeof(Extensions).Assembly.GetName().Version?.ToString();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "data-injector-service",
                    serviceVersion: serviceVersion
                )
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(DataInjectorMetrics.MeterName)
                    .AddPrometheusExporter()
            );
    }
}
