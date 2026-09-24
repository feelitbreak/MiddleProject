namespace GraphQLGatewayService.Api.GraphQL;

using GraphQLGatewayService.Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

/// <summary>
/// The read-through cache the resolvers use, carrying the lifetimes configuration sets so a
/// resolver needs one service rather than a cache and its options.
/// </summary>
public sealed class QueryCache(HybridCache cache, IOptions<CacheOptions> options)
{
    /// <summary>Gets the lifetime for aggregate series.</summary>
    public HybridCacheEntryOptions Aggregates { get; } =
        new() { Expiration = TimeSpan.FromSeconds(options.Value.AggregateSeconds) };

    /// <summary>Gets the lifetime for the sensor and location catalogue.</summary>
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
