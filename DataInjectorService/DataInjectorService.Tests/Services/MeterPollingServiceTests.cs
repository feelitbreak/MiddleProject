namespace DataInjectorService.Tests.Services;

using DataInjectorService.Common;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Services;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Tests the <see cref="MeterPollingService"/> polling loop behaviour across all
/// <see cref="Result{T}"/> outcomes, verifying producer interactions and that the service
/// keeps running after recoverable failures.
/// </summary>
public sealed class MeterPollingServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Success_PublishesAllReadings()
    {
        var readings = new IReadOnlyList<MeterReading>[]
        {
            [MakeReading("energy", "A"), MakeReading("motion", "B")],
        };
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();

        SetupSequenceWithCancel(serviceMock, cts, Result.Success(readings[0]));

        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(serviceMock.Object, producerMock.Object);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()),
            Times.Exactly(readings[0].Count)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Failure_NothingPublished()
    {
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();

        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed)
        );

        var service = BuildService(serviceMock.Object, producerMock.Object);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_RateLimited_DoesNotPublish()
    {
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();

        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Failure<IReadOnlyList<MeterReading>>(
                Error.CreateRateLimited(TimeSpan.FromMilliseconds(10))
            )
        );

        var service = BuildService(serviceMock.Object, producerMock.Object);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_FailureThenSuccess_ServiceKeepsRunning()
    {
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();

        var reading = MakeReading();
        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed),
            Result.Success<IReadOnlyList<MeterReading>>([reading])
        );

        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(serviceMock.Object, producerMock.Object);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_KafkaFails_ServiceKeepsRunning()
    {
        IReadOnlyList<MeterReading> readings =
        [
            MakeReading("energy", "A"),
            MakeReading("energy", "B"),
        ];
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();

        SetupSequenceWithCancel(serviceMock, cts, Result.Success(readings));

        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var service = BuildService(serviceMock.Object, producerMock.Object);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()),
            Times.Exactly(readings.Count)
        );
    }

    /// <summary>
    /// Configures the mock to return the given results in order, then cancel
    /// <paramref name="cts"/> and return a failure so the polling loop exits cleanly.
    /// </summary>
    private static void SetupSequenceWithCancel(
        Mock<IWeakAppService> serviceMock,
        CancellationTokenSource cts,
        params Result<IReadOnlyList<MeterReading>>[] results
    )
    {
        var sequence = serviceMock.SetupSequence(s =>
            s.GetMetersAsync(It.IsAny<CancellationToken>())
        );

        foreach (var result in results)
        {
            sequence = sequence.ReturnsAsync(result);
        }

        sequence.ReturnsAsync(() =>
        {
            cts.Cancel();
            return Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed);
        });
    }

    private static MeterPollingService BuildService(
        IWeakAppService weakAppService,
        IKafkaProducer kafkaProducer,
        WeakAppOptions? options = null
    )
    {
        var opts = Options.Create(options ?? new WeakAppOptions { PollingIntervalSeconds = 0 });
        return new MeterPollingService(
            weakAppService,
            kafkaProducer,
            opts,
            NullLogger<MeterPollingService>.Instance,
            new DataInjectorMetrics()
        );
    }

    private static MeterReading MakeReading(string type = "energy", string name = "Hall") =>
        new()
        {
            Type = type,
            Name = name,
            Payload = new EnergyPayload { Energy = 100 },
        };
}
