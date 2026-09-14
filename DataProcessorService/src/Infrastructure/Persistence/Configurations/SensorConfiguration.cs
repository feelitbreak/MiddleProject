namespace DataProcessorService.Infrastructure.Persistence.Configurations;

using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Maps <see cref="Sensor"/> to the <c>sensors</c> table.
/// </summary>
public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    /// <summary>Name of the unique index over the sensor's natural key.</summary>
    public const string UniqueIndexName = "ux_sensors_name_type";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("sensors");

        builder.HasKey(sensor => sensor.Id);

        builder.Property(sensor => sensor.Id).HasColumnName("id");

        builder.Property(sensor => sensor.Name).HasColumnName("name").HasMaxLength(128).IsRequired();

        builder
            .Property(sensor => sensor.Type)
            .HasColumnName(MeterReadingColumns.SensorType)
            .HasMaxLength(32)
            .HasConversion(type => SensorTypeNames.ToName(type), name => SensorTypeNames.FromName(name))
            .IsRequired();

        // The upstream API supplies no sensor identifier, so location plus kind is the natural
        // key. The unique index is what makes concurrent get-or-create safe.
        builder
            .HasIndex(sensor => new { sensor.Name, sensor.Type })
            .HasDatabaseName(UniqueIndexName)
            .IsUnique();

        builder
            .HasMany(sensor => sensor.Readings)
            .WithOne(reading => reading.Sensor)
            .HasForeignKey(reading => reading.SensorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
