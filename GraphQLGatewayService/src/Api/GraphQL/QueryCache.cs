namespace GraphQLGatewayService.Api.GraphQL;

using GraphQLGatewayService.Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

public sealed class QueryCache(HybridCache cache, IOptions<CacheOptions> options) : IQueryCache
{
    public HybridCacheEntryOptions Aggregates { get; } =
        new() { Expiration = TimeSpan.FromSeconds(options.Value.AggregateSeconds) };

    public HybridCacheEntryOptions Catalogue { get; } =
        new() { Expiration = TimeSpan.FromSeconds(options.Value.CatalogueSeconds) };

    public ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions entryOptions,
        CancellationToken cancellationToken
    ) =>
        cache.GetOrCreateAsync(
            key,
            state,
            factory,
            entryOptions,
            cancellationToken: cancellationToken
        );
}
