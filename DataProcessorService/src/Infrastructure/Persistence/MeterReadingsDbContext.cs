namespace DataProcessorService.Infrastructure.Persistence;

using DataProcessorService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

/// <summary>
/// EF Core context for the meter readings schema. Code-first: the schema is owned by the entity
/// configurations in this assembly and applied through migrations.
/// </summary>
/// <param name="options">Context options supplied by dependency injection.</param>
public sealed class MeterReadingsDbContext(DbContextOptions<MeterReadingsDbContext> options)
    : DbContext(options)
{
    /// <summary>Gets the sensor catalogue.</summary>
    public DbSet<Sensor> Sensors => this.Set<Sensor>();

    /// <summary>
    /// Gets every reading, across the whole table-per-hierarchy hierarchy. Use
    /// <c>Set&lt;EnergyReading&gt;()</c> or <c>OfType&lt;EnergyReading&gt;()</c> to query one kind.
    /// </summary>
    public DbSet<MeterReading> MeterReadings => this.Set<MeterReading>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

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
