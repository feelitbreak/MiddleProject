namespace DataProcessorService.IntegrationTests;

using DataProcessorService.Domain.Entities;
using DataProcessorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Guards the one piece of hand-written SQL in the service.
/// <para>
/// Ingestion writes through an <c>INSERT ... ON CONFLICT DO NOTHING</c> statement built from the
/// constants in <see cref="MeterReadingColumns"/>, because EF Core has no upsert API. The failure
/// mode that guards against is silent: renaming a column in the entity configuration updates the
/// model and the migration quite happily, and the raw statement then breaks at runtime, in
/// production, on the first batch. This test fails the build instead.
/// </para>
/// <para>
/// Model-only, so it needs no database or container.
/// </para>
/// </summary>
public sealed class MeterReadingColumnContractTests
{
    [Fact]
    public void InsertColumns_AllExistInTheModel()
    {
        var mapped = MappedColumnNames();

        foreach (var column in MeterReadingColumns.InsertColumns)
        {
            Assert.Contains(column, mapped);
        }
    }

    [Fact]
    public void InsertColumns_CoverEveryWritableColumnExceptTheKey()
    {
        var expected = MappedColumnNames()
            .Where(column => column != MeterReadingColumns.Id)
            .OrderBy(column => column, StringComparer.Ordinal);

        var actual = MeterReadingColumns.InsertColumns.OrderBy(
            column => column,
            StringComparer.Ordinal
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TableName_MatchesTheModel()
    {
        using var context = BuildContext();
        var entityType = context.Model.FindEntityType(typeof(MeterReading));

        Assert.NotNull(entityType);
        Assert.Equal(MeterReadingColumns.Table, entityType.GetTableName());
    }

    [Fact]
    public void UniqueIndex_ExistsAndTargetsTheConflictColumns()
    {
        using var context = BuildContext();
        var entityType = context.Model.FindEntityType(typeof(MeterReading));

        Assert.NotNull(entityType);

        var index = entityType
            .GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.GetDatabaseName() == MeterReadingColumns.UniqueIndex
            );

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(
            [MeterReadingColumns.SensorId, MeterReadingColumns.CollectedAt],
            index.Properties.Select(property => ColumnNameOf(property, entityType))
        );
    }

    private static IReadOnlyList<string> MappedColumnNames()
    {
        using var context = BuildContext();
        var entityType = context.Model.FindEntityType(typeof(MeterReading));

        Assert.NotNull(entityType);

        // Derived types carry the subtype-specific columns, so the hierarchy has to be walked to
        // see every column the shared table actually has.
        return entityType
            .GetDerivedTypesInclusive()
            .SelectMany(type => type.GetProperties())
            .Select(property => ColumnNameOf(property, entityType))
            .Concat([MeterReadingColumns.SensorType])
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string ColumnNameOf(IProperty property, IEntityType entityType) =>
        property.GetColumnName(
            StoreObjectIdentifier.Table(
                entityType.GetTableName()!,
                entityType.GetSchema()
            )
        )!;

    private static MeterReadingsDbContext BuildContext() =>
        new(
            new DbContextOptionsBuilder<MeterReadingsDbContext>()
                .UseNpgsql("Host=localhost;Database=model-only;Username=none;Password=none")
                .Options
        );
}
