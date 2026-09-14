namespace DataProcessorService.Infrastructure.Persistence.Repositories;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Writes readings with PostgreSQL's <c>INSERT ... ON CONFLICT DO NOTHING</c>.
/// <para>
/// This is the only hand-written SQL in the service. EF Core exposes no insert-from-select and no
/// upsert API --- <c>ExecuteUpdate</c> and <c>ExecuteDelete</c> are its only set-based operations
/// --- so conflict-skipping insertion cannot be expressed in LINQ. The alternatives were all
/// worse: catching the unique violation aborts the whole PostgreSQL transaction and degenerates
/// into one round trip per row, and pre-querying which keys already exist is a time-of-check race
/// that two replicas would lose.
/// </para>
/// <para>
/// Values are passed as arrays expanded by <c>unnest</c> rather than as a <c>VALUES</c> list, so
/// the statement takes a fixed number of parameters regardless of batch size. That keeps it clear
/// of PostgreSQL's 65535-parameter ceiling and lets Npgsql reuse one prepared plan across every
/// batch size. Table-per-hierarchy mapping is what allows a batch of mixed sensor types to go in
/// one statement, with NULLs in the columns that do not apply.
/// </para>
/// </summary>
public sealed class MeterReadingRepository(MeterReadingsDbContext context) : IMeterReadingRepository
{
    /// <summary>
    /// The ingestion statement.
    /// <para>
    /// Every interpolated value is a compile-time constant from
    /// <see cref="MeterReadingColumns"/> --- an identifier, never data. No caller input reaches
    /// this string: values arrive exclusively as typed <see cref="NpgsqlParameter"/> array
    /// bindings, and <c>ExecuteSqlRawAsync</c> is used deliberately in preference to
    /// <c>ExecuteSqlInterpolatedAsync</c> so that no value can be inlined by accident. Deriving
    /// the column list from the same constants the parameters are built from also keeps the
    /// statement and the model from drifting apart silently.
    /// </para>
    /// </summary>
    private static readonly string InsertSql = BuildInsertSql();

    // PostgreSQL type names for the array parameters, given to NpgsqlParameter.DataTypeName.
    //
    // The alternative spelling is `NpgsqlDbType.Array | NpgsqlDbType.Integer`, which is Npgsql's
    // documented idiom but combines members of an enum that carries no [Flags] attribute --- so it
    // trips S3265, correctly. Naming the PostgreSQL types outright says the same thing without the
    // enum arithmetic, and reads closer to the statement it parameterises.
    private const string IntegerArray = "integer[]";
    private const string TimestampTzArray = "timestamp with time zone[]";
    private const string TextArray = "text[]";
    private const string BooleanArray = "boolean[]";
    private const string DoubleArray = "double precision[]";

    /// <inheritdoc/>
    public async Task<int> InsertIgnoringDuplicatesAsync(
        IReadOnlyList<MeterReadingRow> rows,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return 0;
        }

        var count = rows.Count;
        var sensorIds = new int[count];
        var collectedAt = new DateTimeOffset[count];
        var sensorTypes = new string[count];
        var co2 = new int?[count];
        var pm25 = new int?[count];
        var humidity = new int?[count];
        var motionDetected = new bool?[count];
        var energyKwh = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var row = rows[i];
            sensorIds[i] = row.SensorId;
            collectedAt[i] = row.CollectedAtUtc;
            sensorTypes[i] = SensorTypeNames.ToName(row.SensorType);
            co2[i] = row.Values.Co2;
            pm25[i] = row.Values.Pm25;
            humidity[i] = row.Values.Humidity;
            motionDetected[i] = row.Values.MotionDetected;
            energyKwh[i] = row.Values.EnergyKwh;
        }

        var parameters = new List<object>
        {
            Array(MeterReadingColumns.SensorId, sensorIds, IntegerArray),
            Array(MeterReadingColumns.CollectedAt, collectedAt, TimestampTzArray),
            Array(MeterReadingColumns.SensorType, sensorTypes, TextArray),
            Array(MeterReadingColumns.Co2, co2, IntegerArray),
            Array(MeterReadingColumns.Pm25, pm25, IntegerArray),
            Array(MeterReadingColumns.Humidity, humidity, IntegerArray),
            Array(MeterReadingColumns.MotionDetected, motionDetected, BooleanArray),
            Array(MeterReadingColumns.EnergyKwh, energyKwh, DoubleArray),
        };

        // Rows affected excludes rows the conflict clause skipped, so the caller gets the
        // duplicates-skipped count for free.
        return await context.Database.ExecuteSqlRawAsync(
            InsertSql,
            parameters,
            cancellationToken
        );
    }

    private static NpgsqlParameter Array<T>(string name, T[] values, string postgresArrayType) =>
        new(name, values) { DataTypeName = postgresArrayType };

    private static string BuildInsertSql()
    {
        var columns = string.Join(", ", MeterReadingColumns.InsertColumns);
        var arrays = string.Join(
            ", ",
            MeterReadingColumns.InsertColumns.Select(column => "@" + column)
        );

        return $"""
            INSERT INTO {MeterReadingColumns.Table} ({columns})
            SELECT * FROM unnest({arrays})
            ON CONFLICT ({MeterReadingColumns.SensorId}, {MeterReadingColumns.CollectedAt})
            DO NOTHING;
            """;
    }
}
