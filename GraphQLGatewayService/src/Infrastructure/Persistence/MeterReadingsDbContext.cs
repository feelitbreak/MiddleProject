namespace GraphQLGatewayService.Infrastructure.Persistence;

using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Read-only EF Core context over the meter readings schema. DataProcessorService owns that schema
/// and migrates it; this context has no migrations and never calls <c>Migrate</c>.
/// </summary>
public sealed class MeterReadingsDbContext(DbContextOptions<MeterReadingsDbContext> options)
    : DbContext(options)
{
    /// <summary>Gets the sensor catalogue.</summary>
    public DbSet<SensorRow> Sensors => this.Set<SensorRow>();

    /// <summary>Gets every reading; use <c>Set&lt;EnergyReadingRow&gt;()</c> for one kind.</summary>
    public DbSet<MeterReadingRow> MeterReadings => this.Set<MeterReadingRow>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeterReadingsDbContext).Assembly);

        modelBuilder
            .HasDbFunction(
                typeof(PostgresFunctions).GetMethod(
                    nameof(PostgresFunctions.DateTrunc),
                    [typeof(string), typeof(DateTimeOffset), typeof(string)]
                )!
            )
            .HasName("date_trunc");

        base.OnModelCreating(modelBuilder);
    }
}
