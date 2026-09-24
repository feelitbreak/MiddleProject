namespace GraphQLGatewayService.Api.GraphQL;

using Microsoft.Extensions.Caching.Hybrid;

/// <summary>The read-through cache the resolvers use, carrying the configured lifetimes.</summary>
public interface IQueryCache
{
    HybridCacheEntryOptions Aggregates { get; }

    HybridCacheEntryOptions Catalogue { get; }

    ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions entryOptions,
        CancellationToken cancellationToken
    );
}
