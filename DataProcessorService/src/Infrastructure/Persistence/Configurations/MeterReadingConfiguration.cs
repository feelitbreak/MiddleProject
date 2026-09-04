namespace DataProcessorService.Infrastructure.Persistence.Configurations;

using DataProcessorService.Domain.Entities;
using DataProcessorService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Maps the <see cref="MeterReading"/> hierarchy table-per-hierarchy onto
/// <c>meter_readings</c>, with a string discriminator using the same vocabulary as the wire
/// contract so that ad-hoc SQL reads naturally.
/// </summary>
public sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
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
            .HasValue<AirQualityReading>(SensorTypeNames.AirQuality)
            .HasValue<MotionReading>(SensorTypeNames.Motion)
            .HasValue<EnergyReading>(SensorTypeNames.Energy);

        builder.Property<string>(MeterReadingColumns.SensorType).HasMaxLength(32);

        // Idempotency: the ingestion statement targets this index with ON CONFLICT DO NOTHING, so
        // a redelivered Kafka batch is a no-op. Note this deduplicates *messages*, not source
        // data --- CollectedAt is stamped per poll, so two polls of an unchanged upstream value
        // legitimately produce two rows.
        builder
            .HasIndex(reading => new { reading.SensorId, reading.CollectedAt })
            .HasDatabaseName(MeterReadingColumns.UniqueIndex)
            .IsUnique();

        // Declared ascending deliberately: PostgreSQL scans btrees backwards for the descending
        // keyset feed, so a mirrored descending index would be pure write amplification.
        //
        // For the same reason there is no (sensor_id, collected_at DESC) index: the unique index
        // above already covers latest-per-sensor. That query is a lateral lookup per sensor, and
        // with a sensor catalogue this small each one is a backward scan over a tiny prefix --- a
        // second index on identical columns would only add write cost on the ingest hot path.
        builder
            .HasIndex(reading => new { reading.CollectedAt, reading.Id })
            .HasDatabaseName("ix_meter_readings_collected_id");

        builder
            .HasIndex(MeterReadingColumns.SensorType, nameof(MeterReading.CollectedAt))
            .HasDatabaseName("ix_meter_readings_type_collected_desc")
            .IsDescending(false, true);
    }
}

/// <summary>Maps the air quality subtype's value columns.</summary>
public sealed class AirQualityReadingConfiguration : IEntityTypeConfiguration<AirQualityReading>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AirQualityReading> builder)
    {
        builder.Property(reading => reading.Co2).HasColumnName(MeterReadingColumns.Co2);
        builder.Property(reading => reading.Pm25).HasColumnName(MeterReadingColumns.Pm25);
        builder.Property(reading => reading.Humidity).HasColumnName(MeterReadingColumns.Humidity);
    }
}

/// <summary>Maps the motion subtype's value column.</summary>
public sealed class MotionReadingConfiguration : IEntityTypeConfiguration<MotionReading>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MotionReading> builder)
    {
        builder
            .Property(reading => reading.MotionDetected)
            .HasColumnName(MeterReadingColumns.MotionDetected);
    }
}

/// <summary>Maps the energy subtype's value column.</summary>
public sealed class EnergyReadingConfiguration : IEntityTypeConfiguration<EnergyReading>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EnergyReading> builder)
    {
        builder.Property(reading => reading.EnergyKwh).HasColumnName(MeterReadingColumns.EnergyKwh);
    }
}
