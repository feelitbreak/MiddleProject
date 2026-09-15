namespace GraphQLGatewayService.Infrastructure.Persistence.Configurations;

using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Maps <see cref="SensorRow"/> to <c>sensors</c>. DataProcessorService owns this schema and its
/// migrations; this read-only mapping must not diverge from it.
/// </summary>
public sealed class SensorConfiguration : IEntityTypeConfiguration<SensorRow>
{
    /// <summary>Name of the unique index over the sensor's natural key.</summary>
    public const string UniqueIndexName = "ux_sensors_name_type";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SensorRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sensors");

        builder.HasKey(sensor => sensor.Id);

        builder.Property(sensor => sensor.Id).HasColumnName("id");

        builder
            .Property(sensor => sensor.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder
            .Property(sensor => sensor.Type)
            .HasColumnName(MeterReadingColumns.SensorType)
            .HasMaxLength(32)
            .HasConversion(
                type => SensorTypeNames.ToName(type),
                name => SensorTypeNames.FromName(name)
            )
            .IsRequired();

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
