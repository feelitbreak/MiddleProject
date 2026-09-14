namespace DataProcessorService.UnitTests.Readings;

using DataProcessorService.Application.Abstractions.Persistence;
using DataProcessorService.Application.Readings.IngestReadingBatch;
using DataProcessorService.Domain.Enums;
using Moq;

/// <summary>
/// Covers the ingestion handler's two responsibilities: resolving sensors to surrogate keys, and
/// preparing rows so that the database's idempotency guarantee actually holds.
/// </summary>
public sealed class IngestReadingBatchCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_EmptyBatch_SucceedsWithoutTouchingTheDatabase()
    {
        var repository = new Mock<IMeterReadingRepository>(MockBehavior.Strict);
        var registry = new Mock<ISensorRegistry>(MockBehavior.Strict);
        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);

        var result = await handler.HandleAsync(
            new IngestReadingBatchCommand([]),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Inserted);
        Assert.Equal(0, result.Value.DuplicatesSkipped);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_NonUtcTimestamp_NormalizesBeforeWriting()
    {
        var captured = CaptureRows(out var repository, insertedCount: 1);
        var registry = StubRegistry(sensorId: 5);
        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);

        var berlinNoon = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2));

        await handler.HandleAsync(
            new IngestReadingBatchCommand(
                [Reading("Kitchen", SensorType.Energy, berlinNoon, 1.0)]
            ),
            TestContext.Current.CancellationToken
        );

        var row = Assert.Single(captured);
        Assert.Equal(TimeSpan.Zero, row.CollectedAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero), row.CollectedAtUtc);
        Assert.Equal(5, row.SensorId);
    }

    [Fact]
    public async Task HandleAsync_DuplicateWithinBatch_CollapsesBeforeWriting()
    {
        // ON CONFLICT DO NOTHING defines behaviour against already-committed rows, not against two
        // rows arriving in the same statement, so the handler must collapse them itself.
        var captured = CaptureRows(out var repository, insertedCount: 1);
        var registry = StubRegistry(sensorId: 1);
        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);

        var collectedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        await handler.HandleAsync(
            new IngestReadingBatchCommand(
                [
                    Reading("Kitchen", SensorType.Energy, collectedAt, 1.0),
                    Reading("Kitchen", SensorType.Energy, collectedAt, 2.0),
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Single(captured);
    }

    [Fact]
    public async Task HandleAsync_SameInstantDifferentOffsets_CollapsesBeforeWriting()
    {
        // The deduplication key is computed after normalisation, so two spellings of one instant
        // must collapse rather than collide later in the database.
        var captured = CaptureRows(out var repository, insertedCount: 1);
        var registry = StubRegistry(sensorId: 1);
        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);

        await handler.HandleAsync(
            new IngestReadingBatchCommand(
                [
                    Reading(
                        "Kitchen",
                        SensorType.Energy,
                        new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2)),
                        1.0
                    ),
                    Reading(
                        "Kitchen",
                        SensorType.Energy,
                        new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
                        2.0
                    ),
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Single(captured);
    }

    [Fact]
    public async Task HandleAsync_DistinctSensors_KeepsEveryRow()
    {
        var captured = CaptureRows(out var repository, insertedCount: 3);
        var registry = DistinctIdRegistry();

        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);
        var collectedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var result = await handler.HandleAsync(
            new IngestReadingBatchCommand(
                [
                    Reading("Kitchen", SensorType.Energy, collectedAt, 1.0),
                    Reading("Office", SensorType.Energy, collectedAt, 2.0),
                    Reading("Bedroom", SensorType.Energy, collectedAt, 3.0),
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(3, captured.Count);
        Assert.Equal(3, result.Value.Inserted);
        Assert.Equal(0, result.Value.DuplicatesSkipped);
    }

    [Fact]
    public async Task HandleAsync_RepositorySkipsRows_ReportsThemAsDuplicates()
    {
        // The repository returns rows actually inserted; the difference against the batch size is
        // what the duplicates-skipped metric reports.
        CaptureRows(out var repository, insertedCount: 1);
        var registry = DistinctIdRegistry();
        var handler = new IngestReadingBatchCommandHandler(registry.Object, repository.Object);
        var collectedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var result = await handler.HandleAsync(
            new IngestReadingBatchCommand(
                [
                    Reading("Kitchen", SensorType.Energy, collectedAt, 1.0),
                    Reading("Office", SensorType.Energy, collectedAt, 2.0),
                ]
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result.Value.Inserted);
        Assert.Equal(1, result.Value.DuplicatesSkipped);
    }

    private static List<MeterReadingRow> CaptureRows(
        out Mock<IMeterReadingRepository> repository,
        int insertedCount
    )
    {
        var captured = new List<MeterReadingRow>();
        repository = new Mock<IMeterReadingRepository>();
        repository
            .Setup(r =>
                r.InsertIgnoringDuplicatesAsync(
                    It.IsAny<IReadOnlyList<MeterReadingRow>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyList<MeterReadingRow> rows, CancellationToken _) =>
                    captured.AddRange(rows)
            )
            .ReturnsAsync(insertedCount);

        return captured;
    }

    /// <summary>
    /// Assigns each distinct (name, type) pair its own surrogate key, so that a collision in the
    /// stub cannot be mistaken for the handler deduplicating.
    /// </summary>
    private static Mock<ISensorRegistry> DistinctIdRegistry()
    {
        var ids = new Dictionary<(string, SensorType), int>();
        var registry = new Mock<ISensorRegistry>();
        registry
            .Setup(r =>
                r.GetOrCreateIdAsync(
                    It.IsAny<string>(),
                    It.IsAny<SensorType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (string name, SensorType type, CancellationToken _) =>
                {
                    if (!ids.TryGetValue((name, type), out var id))
                    {
                        id = ids.Count + 1;
                        ids[(name, type)] = id;
                    }

                    return id;
                }
            );

        return registry;
    }

    private static Mock<ISensorRegistry> StubRegistry(int sensorId)
    {
        var registry = new Mock<ISensorRegistry>();
        registry
            .Setup(r =>
                r.GetOrCreateIdAsync(
                    It.IsAny<string>(),
                    It.IsAny<SensorType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(sensorId);

        return registry;
    }

    private static ReadingToIngest Reading(
        string name,
        SensorType type,
        DateTimeOffset collectedAt,
        double energyKwh
    ) => new(name, type, collectedAt, ReadingValues.ForEnergy(energyKwh));
}
