namespace GraphQLGatewayService.IntegrationTests.Schema;

using GraphQLGatewayService.Api.Extensions;
using GraphQLGatewayService.Domain.Common;
using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

/// <summary>
/// Executes real operations through the configured executor and asserts on the JSON a client would
/// receive, so the resolvers, paging middleware, guard rails and error filter are all covered.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GraphQLExecutionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = UtcInstant.Normalize(
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
    );

    // Fixed bounds, so the cache key cannot move between two executions.
    private const string AggregateInSeedWindow = """
        { readingAggregates(metric: ENERGY_KWH, interval: HOUR,
            where: { from: "2026-04-30T00:00:00Z", to: "2026-05-02T00:00:00Z" }) { location } }
        """;

    [Fact]
    public async Task Execute_Sensors_ReturnsTheCatalogue()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ sensors { name type } }");

        AssertNoErrors(response);
        Assert.Equal(2, Data(response).GetProperty("sensors").GetArrayLength());
    }

    [Fact]
    public async Task Execute_ReadingsWithoutArguments_ReturnsTheDefaultPage()
    {
        // RequirePagingBoundaries is off so the dashboard's first render needs no arguments.
        await this.SeedAsync(readingsPerSensor: 30);

        var response = await this.ExecuteAsync("{ readings { nodes { id } } }");

        AssertNoErrors(response);
        Assert.Equal(25, Nodes(response).GetArrayLength());
    }

    [Fact]
    public async Task Execute_ReadingsAskingBeyondMaxPageSize_IsRejected()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ readings(first: 500) { nodes { id } } }");

        Assert.NotEmpty(Errors(response).EnumerateArray());
    }

    [Fact]
    public async Task Execute_ReadingsWithTotalCount_CountsEveryMatchingRow()
    {
        await this.SeedAsync(readingsPerSensor: 4);

        var response = await this.ExecuteAsync("{ readings { totalCount } }");

        AssertNoErrors(response);
        Assert.Equal(8, Data(response).GetProperty("readings").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Execute_ReadingsFilteredByLocation_NarrowsTotalCount()
    {
        await this.SeedAsync(readingsPerSensor: 4);

        var response = await this.ExecuteAsync(
            """{ readings(where: { location: "Garage" }) { totalCount } }"""
        );

        AssertNoErrors(response);
        Assert.Equal(4, Data(response).GetProperty("readings").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Execute_ReadingsPagedByCursor_DoesNotRepeatRows()
    {
        await this.SeedAsync(readingsPerSensor: 6);

        var first = await this.ExecuteAsync(
            "{ readings(first: 4) { pageInfo { endCursor } nodes { id } } }"
        );
        var cursor = Data(first)
            .GetProperty("readings")
            .GetProperty("pageInfo")
            .GetProperty("endCursor")
            .GetString();

        var second = await this.ExecuteAsync(
            $$"""{ readings(first: 4, after: "{{cursor}}") { nodes { id } } }"""
        );

        AssertNoErrors(second);
        Assert.Empty(NodeIds(first).Intersect(NodeIds(second)));
    }

    [Fact]
    public async Task Execute_LatestReadings_ReturnsOneRowPerSensor()
    {
        await this.SeedAsync(readingsPerSensor: 5);

        var response = await this.ExecuteAsync("{ latestReadings { sensor { name } energyKwh } }");

        AssertNoErrors(response);
        Assert.Equal(2, Data(response).GetProperty("latestReadings").GetArrayLength());
    }

    [Fact]
    public async Task Execute_ReadingOfAnEnergySensor_LeavesAirQualityValuesNull()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ latestReadings { energyKwh co2 motionDetected } }");

        AssertNoErrors(response);
        var reading = Data(response).GetProperty("latestReadings")[0];
        Assert.Equal(JsonValueKind.Null, reading.GetProperty("co2").ValueKind);
        Assert.Equal(JsonValueKind.Null, reading.GetProperty("motionDetected").ValueKind);
        Assert.Equal(JsonValueKind.Number, reading.GetProperty("energyKwh").ValueKind);
    }

    [Fact]
    public async Task Execute_AggregateWithTooWideARange_ReportsBadUserInput()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync(
            """
            { readingAggregates(metric: ENERGY_KWH,
                                where: { from: "2020-01-01T00:00:00Z", to: "2024-01-01T00:00:00Z" })
              { location } }
            """
        );

        var error = Errors(response)[0];
        Assert.Equal("BAD_USER_INPUT", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Contains("90", error.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_AggregateWithoutARange_CoversRecentReadings()
    {
        // The default window ends at now, which is what lets a dashboard render before the user
        // touches a control -- so this seeds against the real clock rather than a fixed date.
        await fixture.ResetAsync();
        var recent = UtcInstant.Normalize(DateTimeOffset.UtcNow.AddMinutes(-5));
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Kitchen", SensorType.Energy), recent, 7)
        );

        var response = await this.ExecuteAsync(
            "{ readingAggregates(metric: ENERGY_KWH, interval: DAY) { location unit } }"
        );

        AssertNoErrors(response);
        var series = Data(response).GetProperty("readingAggregates");
        Assert.Equal(1, series.GetArrayLength());
        Assert.Equal("kWh", series[0].GetProperty("unit").GetString());
    }

    [Fact]
    public async Task Execute_AggregateWithoutARange_ExcludesReadingsOlderThanTheWindow()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync(
            "{ readingAggregates(metric: ENERGY_KWH, interval: HOUR) { location } }"
        );

        AssertNoErrors(response);
        Assert.Equal(0, Data(response).GetProperty("readingAggregates").GetArrayLength());
    }

    [Fact]
    public async Task Execute_SameAggregationAliasedThreeTimes_ExceedsTheCostLimit()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync(
            """
            { a: readingAggregates(metric: CO2) { location }
              b: readingAggregates(metric: PM25) { location }
              c: readingAggregates(metric: HUMIDITY) { location } }
            """
        );

        Assert.Contains(
            "cost",
            Errors(response)[0].GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Execute_SingleFullAggregation_IsWithinTheCostLimit()
    {
        // The counterpart to the test above: the limit must not reject the service's own heaviest
        // legitimate query.
        await this.SeedAsync();

        var response = await this.ExecuteAsync(
            """
            { readingAggregates(metric: ENERGY_KWH)
              { location unit points { periodStart count average minimum maximum } } }
            """
        );

        AssertNoErrors(response);
    }

    [Fact]
    public async Task Execute_SameAggregateWithinItsLifetime_ServesTheCachedSeries()
    {
        await this.SeedAsync();
        await using var provider = this.BuildProvider();

        var first = await RunAsync(provider, AggregateInSeedWindow);
        await this.SeedOfficeAsync();
        var second = await RunAsync(provider, AggregateInSeedWindow);

        Assert.Equal(2, Data(first).GetProperty("readingAggregates").GetArrayLength());
        Assert.Equal(2, Data(second).GetProperty("readingAggregates").GetArrayLength());
    }

    [Fact]
    public async Task Execute_SameAggregateAfterItsLifetime_QueriesAgain()
    {
        await this.SeedAsync();
        await using var provider = this.BuildProvider(
            new Dictionary<string, string?> { ["Cache:AggregateSeconds"] = "1" }
        );

        await RunAsync(provider, AggregateInSeedWindow);
        await this.SeedOfficeAsync();
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        var refreshed = await RunAsync(provider, AggregateInSeedWindow);

        Assert.Equal(3, Data(refreshed).GetProperty("readingAggregates").GetArrayLength());
    }

    [Fact]
    public async Task Execute_LocationsWithinTheirLifetime_ServeTheCachedCatalogue()
    {
        await this.SeedAsync();
        await using var provider = this.BuildProvider();

        await RunAsync(provider, "{ locations }");
        await this.SeedOfficeAsync();
        var second = await RunAsync(provider, "{ locations }");

        Assert.Equal(2, Data(second).GetProperty("locations").GetArrayLength());
    }

    [Fact]
    public async Task Execute_Introspection_IsRejectedOutsideDevelopment()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync(
            "{ __schema { types { name } } }",
            environmentName: "Production"
        );

        Assert.NotEmpty(Errors(response).EnumerateArray());
    }

    [Fact]
    public async Task Execute_Introspection_IsAllowedInDevelopment()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ __schema { types { name } } }");

        AssertNoErrors(response);
    }

    [Fact]
    public async Task Execute_Health_ReportsTheDatabaseReachable()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ health { status checks { name status } } }");

        AssertNoErrors(response);
        Assert.Equal("HEALTHY", Data(response).GetProperty("health").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_UnknownField_IsRejected()
    {
        await this.SeedAsync();

        var response = await this.ExecuteAsync("{ nosuchfield }");

        Assert.NotEmpty(Errors(response).EnumerateArray());
    }

    private static JsonElement Data(JsonElement response) => response.GetProperty("data");

    private static JsonElement Errors(JsonElement response)
    {
        Assert.True(response.TryGetProperty("errors", out var errors), "Expected the response to carry errors.");
        return errors;
    }

    private static void AssertNoErrors(JsonElement response) =>
        Assert.False(
            response.TryGetProperty("errors", out var errors),
            $"Expected no errors, got: {errors}"
        );

    private static JsonElement Nodes(JsonElement response) =>
        Data(response).GetProperty("readings").GetProperty("nodes");

    private static List<long> NodeIds(JsonElement response) =>
        [.. Nodes(response).EnumerateArray().Select(node => node.GetProperty("id").GetInt64())];

    private async Task<JsonElement> ExecuteAsync(
        string query,
        string environmentName = "Development"
    )
    {
        await using var provider = this.BuildProvider(environmentName: environmentName);
        return await RunAsync(provider, query);
    }

    private static async Task<JsonElement> RunAsync(IServiceProvider provider, string query)
    {
        var executor = await provider
            .GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);

        var result = await executor.ExecuteAsync(
            OperationRequestBuilder.New().SetDocument(query).Build(),
            TestContext.Current.CancellationToken
        );

        using var document = JsonDocument.Parse(result.ToJson());

        // Cloned so the element outlives the document this method disposes.
        return document.RootElement.Clone();
    }

    private ServiceProvider BuildProvider(
        IDictionary<string, string?>? settings = null,
        string environmentName = "Development"
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GraphQLGatewayMetrics>();
        services.AddDbContext<MeterReadingsDbContext>(options =>
            options
                .UseNpgsql(fixture.ConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );
        services.AddHealthChecks().AddDbContextCheck<MeterReadingsDbContext>("database");
        services.AddGraphQLApi(
            new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build(),
            new TestEnvironment(environmentName)
        );

        return services.BuildServiceProvider();
    }

    private async Task SeedOfficeAsync() =>
        await fixture.SeedAsync(
            Energy(PostgresFixture.Sensor("Office", SensorType.Energy), Start.AddMinutes(1), 5)
        );

    private static EnergyReadingRow Energy(SensorRow sensor, DateTimeOffset at, double kwh) =>
        new()
        {
            Sensor = sensor,
            CollectedAt = UtcInstant.Normalize(at),
            EnergyKwh = kwh,
        };

    private async Task SeedAsync(int readingsPerSensor = 1)
    {
        await fixture.ResetAsync();
        var kitchen = PostgresFixture.Sensor("Kitchen", SensorType.Energy);
        var garage = PostgresFixture.Sensor("Garage", SensorType.Energy);

        await fixture.SeedAsync(
            [
                .. Enumerable
                    .Range(0, readingsPerSensor)
                    .SelectMany(index =>
                        new MeterReadingRow[]
                        {
                            Energy(kitchen, Start.AddMinutes(index), index),
                            Energy(garage, Start.AddMinutes(index), index * 2),
                        }
                    ),
            ]
        );
    }

    /// <summary>Lets a test choose the environment the guard rails are configured for.</summary>
    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "GraphQLGatewayService.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
