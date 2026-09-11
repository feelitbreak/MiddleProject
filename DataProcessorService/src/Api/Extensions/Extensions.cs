namespace DataProcessorService.Api.Extensions;

using DataProcessorService.Api.HealthChecks;
using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Behaviors;
using DataProcessorService.Application.Contracts;
using DataProcessorService.Application.Dispatch;
using DataProcessorService.Application.Readings.GetLatestReadings;
using DataProcessorService.Application.Readings.GetReadingAggregates;
using DataProcessorService.Application.Readings.GetReadings;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Application.Sensors.GetSensors;
using DataProcessorService.Infrastructure.Configuration;
using DataProcessorService.Infrastructure.Messaging;
using DataProcessorService.Infrastructure.Persistence;
using DataProcessorService.Infrastructure.Persistence.Queries;
using DataProcessorService.Infrastructure.Persistence.Repositories;
using DataProcessorService.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods that keep <c>Program.cs</c> declarative and
/// free of registration boilerplate. Single composition root for the whole service, matching the
/// layout of the sibling DataInjectorService.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Extensions
{
    /// <summary>
    /// Configures JSON for the read API: enums are written as their names, matching what query
    /// parameter binding accepts, and columns that do not apply to a reading's type are omitted
    /// rather than serialised as nulls.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddJsonConfiguration(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            // Default member names, not snake_case: minimal API parameter binding uses
            // Enum.TryParse and does not consult this serializer, so writing a different spelling
            // here would make the API accept one vocabulary and emit another.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull;
        });
    }

    /// <summary>Registers Swagger/OpenAPI generation for the service.</summary>
    /// <param name="services">The service collection.</param>
    public static void AddSwaggerGenConfiguration(this IServiceCollection services)
    {
        // Minimal APIs do not register the API explorer implicitly, and Swashbuckle's generator
        // cannot be constructed without it.
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "DataProcessorService", Version = "v1" });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });
    }

    /// <summary>
    /// Registers a CORS policy that allows localhost (in development) and any explicitly
    /// configured origins.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
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
    /// Registers the database context and the persistence abstractions the application layer
    /// depends on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    public static void AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var databaseOptions =
            configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new();

        services.AddDbContext<MeterReadingsDbContext>(options =>
            options.UseNpgsql(
                databaseOptions.ConnectionString,
                npgsql =>
                {
                    npgsql.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    npgsql.EnableRetryOnFailure(
                        databaseOptions.MaxRetryCount,
                        TimeSpan.FromSeconds(databaseOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null
                    );
                }
            )
        );

        services.AddScoped<IMeterReadingRepository, MeterReadingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReadingQueries, ReadingQueries>();
        services.AddScoped<ISensorQueries, SensorQueries>();

        // Singleton so the sensor catalogue is cached process-wide; it opens its own scope on the
        // rare occasions it needs the database.
        services.AddSingleton<ISensorRegistry, SensorRegistry>();
    }

    /// <summary>
    /// Registers the Kafka consumer, the dead-letter producer and the consumer's liveness signal.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
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
        services.AddSingleton<IDeadLetterProducer, DeadLetterProducer>();
        services.AddHostedService<KafkaConsumerService>();
    }

    /// <summary>
    /// Registers the CQRS dispatcher, its pipeline behaviours and every handler.
    /// <para>
    /// Handlers are listed explicitly rather than discovered by assembly scanning; a unit test
    /// performs the scan and asserts this list is complete.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddCqrsHandlers(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddCqrs(cqrs =>
            cqrs
                // Outermost first. Logging wraps everything so that a rollback is still reported;
                // the unit of work is constrained to commands and never closes over a query.
                .AddBehavior(typeof(LoggingBehavior<,>))
                .AddBehavior(typeof(UnitOfWorkBehavior<,>))
                .AddCommandHandler<
                    IngestReadingBatchCommand,
                    IngestReadingBatchSummary,
                    IngestReadingBatchCommandHandler
                >()
                .AddQueryHandler<
                    GetReadingsQuery,
                    PagedResult<ReadingDto>,
                    GetReadingsQueryHandler
                >()
                .AddQueryHandler<
                    GetLatestReadingsQuery,
                    IReadOnlyList<ReadingDto>,
                    GetLatestReadingsQueryHandler
                >()
                .AddQueryHandler<
                    GetReadingAggregatesQuery,
                    IReadOnlyList<AggregatePeriodDto>,
                    GetReadingAggregatesQueryHandler
                >()
                .AddQueryHandler<GetSensorsQuery, IReadOnlyList<SensorDto>, GetSensorsQueryHandler>()
        );
    }

    /// <summary>
    /// Registers liveness and readiness health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<ConsumerLivenessHealthCheck>("consumer-loop", tags: ["live"])
            .AddDbContextCheck<MeterReadingsDbContext>("database", tags: ["ready"])
            .AddCheck<ConsumerAssignmentHealthCheck>("consumer-assignment", tags: ["ready"]);
    }

    /// <summary>
    /// Registers the custom <see cref="DataProcessorMetrics"/> meter along with ASP.NET Core and
    /// runtime instrumentation, exported for Prometheus to scrape.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<DataProcessorMetrics>();

        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "data-processor-service",
                    serviceVersion: serviceVersion
                )
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(DataProcessorMetrics.MeterName)
                    .AddPrometheusExporter()
            );
    }

    /// <summary>
    /// Applies pending migrations when configured to do so.
    /// <para>
    /// Called from the composition root before the host starts serving, never from a hosted
    /// service: migrating inside a hosted service races the readiness probe, and an instance can
    /// be killed part-way through start-up as a result.
    /// </para>
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>A task that completes once migrations have been applied.</returns>
    public static async Task ApplyMigrationsIfConfiguredAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<
            IOptions<DatabaseOptions>
        >();

        if (!options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeterReadingsDbContext>();
        await context.Database.MigrateAsync();
    }
}
