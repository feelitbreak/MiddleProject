namespace GraphQLGatewayService.Infrastructure.Persistence.Configurations;

using GraphQLGatewayService.Domain.Enums;
using GraphQLGatewayService.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Maps the <see cref="MeterReadingRow"/> hierarchy onto <c>meter_readings</c>. DataProcessorService
/// owns this schema and its migrations; this read-only mapping must not diverge from it. The
/// indexes are declared so EF's query planning matches the physical table, not to create them.
/// </summary>
public sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReadingRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MeterReadingRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(MeterReadingColumns.Table);

        builder.HasKey(reading => reading.Id);

        builder.Property(reading => reading.Id).HasColumnName(MeterReadingColumns.Id);

        builder.Property(reading => reading.SensorId).HasColumnName(MeterReadingColumns.SensorId);

        builder
            .Property(reading => reading.CollectedAt)
            .HasColumnName(MeterReadingColumns.CollectedAt)
            .IsRequired();

        builder
            .HasDiscriminator<string>(MeterReadingColumns.SensorType)
            .HasValue<AirQualityReadingRow>(SensorTypeNames.ToName(SensorType.AirQuality))
            .HasValue<MotionReadingRow>(SensorTypeNames.ToName(SensorType.Motion))
            .HasValue<EnergyReadingRow>(SensorTypeNames.ToName(SensorType.Energy));

        builder.Property<string>(MeterReadingColumns.SensorType).HasMaxLength(32);

        builder
            .HasIndex(reading => new { reading.SensorId, reading.CollectedAt })
            .HasDatabaseName(MeterReadingColumns.UniqueIndex)
            .IsUnique();

        // The index the readings feed pages on; ascending because PostgreSQL scans backwards.
        builder
            .HasIndex(reading => new { reading.CollectedAt, reading.Id })
            .HasDatabaseName("ix_meter_readings_collected_id");

        builder
            .HasIndex(MeterReadingColumns.SensorType, nameof(MeterReadingRow.CollectedAt))
            .HasDatabaseName("ix_meter_readings_type_collected_desc")
            .IsDescending(false, true);
    }
}

/// <summary>Maps the air quality subtype's value columns.</summary>
public sealed class AirQualityReadingConfiguration : IEntityTypeConfiguration<AirQualityReadingRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AirQualityReadingRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(reading => reading.Co2).HasColumnName(MeterReadingColumns.Co2);
        builder.Property(reading => reading.Pm25).HasColumnName(MeterReadingColumns.Pm25);
        builder.Property(reading => reading.Humidity).HasColumnName(MeterReadingColumns.Humidity);
    }
}

/// <summary>Maps the motion subtype's value column.</summary>
public sealed class MotionReadingConfiguration : IEntityTypeConfiguration<MotionReadingRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MotionReadingRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Property(reading => reading.MotionDetected)
            .HasColumnName(MeterReadingColumns.MotionDetected);
    }
}

/// <summary>Maps the energy subtype's value column.</summary>
public sealed class EnergyReadingConfiguration : IEntityTypeConfiguration<EnergyReadingRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EnergyReadingRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(reading => reading.EnergyKwh).HasColumnName(MeterReadingColumns.EnergyKwh);
    }
}
