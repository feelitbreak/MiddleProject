namespace GraphQLGatewayService.UnitTests.GraphQL;

using GraphQLGatewayService.Api.Configuration;
using GraphQLGatewayService.Api.GraphQL;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Covers the configured lifetimes and the read-through behaviour resolvers rely on.</summary>
public sealed class QueryCacheTests : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection()
        .AddHybridCache()
        .Services.BuildServiceProvider();

    [Fact]
    public void Lifetimes_ComeFromConfiguration()
    {
        var cache = this.CreateCache(new CacheOptions { AggregateSeconds = 7, CatalogueSeconds = 90 });

        Assert.Equal(TimeSpan.FromSeconds(7), cache.Aggregates.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(90), cache.Catalogue.Expiration);
    }

    [Fact]
    public async Task GetOrCreateAsync_SameKeyTwice_RunsTheFactoryOnce()
    {
        var cache = this.CreateCache(new CacheOptions());
        var calls = 0;

        for (var i = 0; i < 2; i++)
        {
            await cache.GetOrCreateAsync(
                "key",
                0,
                (_, _) => ValueTask.FromResult(Interlocked.Increment(ref calls)),
                cache.Aggregates,
                TestContext.Current.CancellationToken
            );
        }

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentMisses_ShareOneFactoryCall()
    {
        var cache = this.CreateCache(new CacheOptions());
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        var requests = Enumerable
            .Range(0, 10)
            .Select(_ =>
                cache
                    .GetOrCreateAsync(
                        "key",
                        release,
                        async (pending, _) =>
                        {
                            Interlocked.Increment(ref calls);
                            return await pending.Task;
                        },
                        cache.Aggregates,
                        TestContext.Current.CancellationToken
                    )
                    .AsTask()
            )
            .ToList();

        release.SetResult(42);
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Equal(42, result));
    }

    public void Dispose() => this.services.Dispose();

    private QueryCache CreateCache(CacheOptions options) =>
        new(this.services.GetRequiredService<HybridCache>(), Options.Create(options));
}
