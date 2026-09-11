namespace DataProcessorService.Infrastructure.Persistence;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Collections.Concurrent;

/// <summary>
/// Resolves sensor natural keys to surrogate keys, caching the whole catalogue in memory.
/// <para>
/// The upstream API reports from a small fixed set of locations, so after warm-up the ingestion
/// hot path never touches the database for this. Registered as a singleton to make the cache
/// process-wide, which means it creates its own scope whenever it does need the database.
/// </para>
/// <para>
/// Resolution is deliberately expressed in LINQ rather than a hand-written upsert. The unique
/// index on (name, sensor_type) is what makes it safe under concurrency: a losing racer sees a
/// unique violation and re-reads the row the winner committed.
/// </para>
/// </summary>
public sealed class SensorRegistry(IServiceScopeFactory scopeFactory) : ISensorRegistry
{
    private const string UniqueViolation = "23505";

    private readonly ConcurrentDictionary<(string Name, SensorType Type), int> cache = new();

    /// <inheritdoc/>
    public async Task<int> GetOrCreateIdAsync(
        string name,
        SensorType type,
        CancellationToken cancellationToken
    )
    {
        var key = (name, type);

        if (this.cache.TryGetValue(key, out var cachedId))
        {
            return cachedId;
        }

        var id = await this.ResolveAsync(name, type, cancellationToken);
        this.cache[key] = id;
        return id;
    }

    private async Task<int> ResolveAsync(
        string name,
        SensorType type,
        CancellationToken cancellationToken
    )
    {
        // A dedicated scope, and therefore a dedicated context and connection: sensor creation
        // must survive independently of the caller's batch transaction. If it enlisted in that
        // transaction, a rolled-back batch would also roll back the catalogue row while this
        // cache kept serving the id it had already handed out.
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeterReadingsDbContext>();

        var existingId = await FindIdAsync(context, name, type, cancellationToken);

        if (existingId is not null)
        {
            return existingId.Value;
        }

        var sensor = new Sensor { Name = name, Type = type };
        context.Sensors.Add(sensor);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return sensor.Id;
        }
        catch (DbUpdateException ex)
            when ((ex.InnerException as PostgresException)?.SqlState == UniqueViolation)
        {
            // Another writer created the same sensor between the read and the insert. The unique
            // index rejected the loser, so re-read the winner's row.
            context.Entry(sensor).State = EntityState.Detached;

            var raced = await FindIdAsync(context, name, type, cancellationToken);

            return raced
                ?? throw new InvalidOperationException(
                    $"Sensor '{name}' of type '{type}' reported a unique violation but could not "
                        + "be found afterwards."
                );
        }
    }

    private static async Task<int?> FindIdAsync(
        MeterReadingsDbContext context,
        string name,
        SensorType type,
        CancellationToken cancellationToken
    )
    {
        // Ordered because the unique index makes at most one row match, but EF cannot know that
        // and warns about a row-limiting operator without a deterministic order.
        var ids = await context
            .Sensors.AsNoTracking()
            .Where(sensor => sensor.Name == name && sensor.Type == type)
            .OrderBy(sensor => sensor.Id)
            .Select(sensor => sensor.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        return ids.Count == 0 ? null : ids[0];
    }
}
