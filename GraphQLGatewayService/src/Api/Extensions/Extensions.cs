namespace GraphQLGatewayService.Api.Extensions;

using GraphQLGatewayService.Api.Authentication;
using GraphQLGatewayService.Api.Configuration;
using GraphQLGatewayService.Api.GraphQL.Errors;
using GraphQLGatewayService.Api.HealthChecks;
using GraphQLGatewayService.Infrastructure.Configuration;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.CodeAnalysis;

/// <summary>Single composition root, matching the layout of the sibling services.</summary>
[ExcludeFromCodeCoverage(
    Justification = "Dependency injection wiring, exercised indirectly by every integration test."
)]
public static class Extensions
{
    /// <summary>The largest page a client may request. A schema invariant, not a deployment knob.</summary>
    private const int MaxPageSize = 100;

    /// <summary>The page size a client gets when it asks for none.</summary>
    private const int DefaultPageSize = 25;

    /// <summary>How deeply a query may nest. The schema's own deepest path is four levels.</summary>
    private const int MaxExecutionDepth = 12;

    /// <summary>
    /// Ceiling on an operation's analysed cost. Measured, not picked round: a fully expanded
    /// <c>readingAggregates</c> analyses at 2010, so this admits one but not three aliased.
    /// </summary>
    private const int MaxOperationCost = 5_000;

    /// <summary>
    /// Registers the API-key scheme. The reverse proxy supplies the key, so a browser never
    /// holds it.
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

    /// <summary>Registers a CORS policy allowing localhost and any configured origins.</summary>
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
    /// Registers the database context. A plain scoped context rather than a factory works because
    /// HotChocolate resolves each query field in its own scope; see <see cref="AddGraphQLApi"/>.
    /// </summary>
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
            options
                .UseNpgsql(
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
                // Set here rather than AsNoTracking per query, so no resolver can forget.
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );
    }

    /// <summary>Registers the GraphQL schema, its guard rails and its error handling.</summary>
    public static void AddGraphQLApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.TryAddSingleton(TimeProvider.System);

        services
            .AddGraphQLServer()
            // Source-generated by HotChocolate.Types.Analyzers from the [QueryType] classes in
            // GraphQL/Queries; the name follows this project's root namespace.
            .AddApiTypes()
            .AddPagingArguments()
            // The schema has its own service provider, without the host's open-generic logger
            // registration or the meter; these bridge them across.
            .AddApplicationService<ILogger<GatewayErrorFilter>>()
            .AddApplicationService<GraphQLGatewayMetrics>()
            .AddApplicationService<ILogger<GraphQLDiagnosticsListener>>()
            .AddErrorFilter<GatewayErrorFilter>()
            // Errors refused during validation never reach the filter; this counts those.
            .AddDiagnosticEventListener<GraphQLDiagnosticsListener>()
            // Correctness, not tuning: EF Core forbids parallel operations on one context and
            // sibling query fields resolve concurrently. This is the default; stating it keeps a
            // future change to that default from silently breaking the service.
            .ModifyOptions(options =>
                options.DefaultQueryDependencyInjectionScope = DependencyInjectionScope.Resolver
            )
            .ModifyPagingOptions(options =>
            {
                options.MaxPageSize = MaxPageSize;
                options.DefaultPageSize = DefaultPageSize;
                options.IncludeTotalCount = true;
                // So readings with no arguments returns the newest page rather than an error.
                options.RequirePagingBoundaries = false;
            })
            .AddCostAnalyzer()
            .ModifyCostOptions(options =>
            {
                options.MaxFieldCost = MaxOperationCost;
                options.MaxTypeCost = MaxOperationCost;
                options.EnforceCostLimits = true;
            })
            .AddMaxExecutionDepthRule(MaxExecutionDepth, skipIntrospectionFields: true)
            .ModifyRequestOptions(options =>
            {
                options.ExecutionTimeout = TimeSpan.FromSeconds(15);
                // Never, in any environment: a dev instance is still reachable and an Npgsql
                // message can carry connection details. See GatewayErrorFilter.
                options.IncludeExceptionDetails = false;
            })
            .DisableIntrospection(!environment.IsDevelopment());
    }

    /// <summary>Registers liveness and readiness health checks.</summary>
    public static void AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            // Nothing runs in the background that could stall, so liveness is "the host is up".
            // Registered anyway, so the probe returns a report rather than an empty one.
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("The host is running."),
                tags: ["live"]
            )
            .AddDbContextCheck<MeterReadingsDbContext>("database", tags: ["ready"])
            .AddCheck<GraphQLSchemaHealthCheck>("graphql-schema", tags: ["ready"]);
    }

    /// <summary>Registers the meter plus standard instrumentation, exported for Prometheus.</summary>
    public static void AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<GraphQLGatewayMetrics>();

        var serviceVersion = typeof(Extensions).Assembly.GetName().Version?.ToString();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "graphql-gateway-service",
                    serviceVersion: serviceVersion
                )
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(GraphQLGatewayMetrics.MeterName)
                    .AddPrometheusExporter()
            );
    }
}
