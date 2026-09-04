namespace DataProcessorService.IntegrationTests.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Enums;
using DataProcessorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Covers get-or-create of the sensor catalogue.
/// <para>
/// The interesting case is the race. Two writers can both observe a sensor as absent and both try
/// to insert it; the unique index on (name, sensor_type) rejects the loser, which must then re-read
/// the winner's row rather than fail the batch. That is why resolution can be plain LINQ instead of
/// a hand-written upsert, so it is worth proving against a real database.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class SensorRegistryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task GetOrCreateIdAsync_UnknownSensor_CreatesIt()
    {
        await fixture.ResetAsync();
        await using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ISensorRegistry>();

        var id = await registry.GetOrCreateIdAsync(
            "Kitchen",
            SensorType.Energy,
            TestContext.Current.CancellationToken
        );

        Assert.True(id > 0);

        await using var context = fixture.CreateContext();
        var sensor = await context.Sensors.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Kitchen", sensor.Name);
        Assert.Equal(SensorType.Energy, sensor.Type);
    }

    [Fact]
    public async Task GetOrCreateIdAsync_SameSensorTwice_ReturnsTheSameId()
    {
        await fixture.ResetAsync();
        await using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ISensorRegistry>();

        var first = await registry.GetOrCreateIdAsync(
            "Kitchen",
            SensorType.Energy,
            TestContext.Current.CancellationToken
        );
        var second = await registry.GetOrCreateIdAsync(
            "Kitchen",
            SensorType.Energy,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(first, second);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Sensors.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrCreateIdAsync_SameNameDifferentType_AreDistinctSensors()
    {
        // Location alone is not the key: one room reports several kinds of reading.
        await fixture.ResetAsync();
        await using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ISensorRegistry>();

        var energy = await registry.GetOrCreateIdAsync(
            "Kitchen",
            SensorType.Energy,
            TestContext.Current.CancellationToken
        );
        var motion = await registry.GetOrCreateIdAsync(
            "Kitchen",
            SensorType.Motion,
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(energy, motion);
    }

    [Fact]
    public async Task GetOrCreateIdAsync_ConcurrentRegistries_AgreeOnOneRow()
    {
        // Separate registry instances mean separate caches, so both genuinely race to the database
        // the way two service replicas would.
        await fixture.ResetAsync();

        await using var first = BuildProvider();
        await using var second = BuildProvider();

        var registries = new[]
        {
            first.GetRequiredService<ISensorRegistry>(),
            second.GetRequiredService<ISensorRegistry>(),
        };

        var ids = await Task.WhenAll(
            registries.Select(registry =>
                registry.GetOrCreateIdAsync("Kitchen", SensorType.Energy, CancellationToken.None)
            )
        );

        Assert.Equal(ids[0], ids[1]);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Sensors.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrCreateIdAsync_ManyConcurrentCallers_CreateOneRowPerSensor()
    {
        await fixture.ResetAsync();

        var providers = Enumerable.Range(0, 8).Select(_ => BuildProvider()).ToList();

        try
        {
            var calls = providers.SelectMany(provider =>
                new[] { SensorType.Energy, SensorType.Motion }.Select(type =>
                    provider
                        .GetRequiredService<ISensorRegistry>()
                        .GetOrCreateIdAsync("Kitchen", type, CancellationToken.None)
                )
            );

            var ids = await Task.WhenAll(calls);

            Assert.Equal(2, ids.Distinct().Count());

            await using var context = fixture.CreateContext();
            Assert.Equal(
                2,
                await context.Sensors.CountAsync(TestContext.Current.CancellationToken)
            );
        }
        finally
        {
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MeterReadingsDbContext>(options =>
            options.UseNpgsql(fixture.ConnectionString)
        );
        services.AddSingleton<ISensorRegistry, SensorRegistry>();

        return services.BuildServiceProvider();
    }
}
