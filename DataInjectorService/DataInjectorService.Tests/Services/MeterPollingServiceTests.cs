namespace DataInjectorService.Tests.Services;

using DataInjectorService.Common;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Services;
using DataInjectorService.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

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

    [Fact]
    public async Task ExecuteAsync_Success_RecordsSuccessOutcome()
    {
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();
        using var metrics = new DataInjectorMetrics();
        var outcomes = CaptureWeakAppRequestOutcomes(metrics);

        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Success<IReadOnlyList<MeterReading>>([MakeReading()])
        );

        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(serviceMock.Object, producerMock.Object, metrics: metrics);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // SetupSequenceWithCancel always appends one more failing call to stop the loop, so a
        // trailing "failure" is expected here — the point is that the configured result recorded
        // "success" first.
        Assert.Equal("success", outcomes[0]);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_RecordsFailureOutcome()
    {
        var serviceMock = new Mock<IWeakAppService>();
        var producerMock = new Mock<IKafkaProducer>();
        using var cts = new CancellationTokenSource();
        using var metrics = new DataInjectorMetrics();
        var outcomes = CaptureWeakAppRequestOutcomes(metrics);

        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Failure<IReadOnlyList<MeterReading>>(Error.Failed)
        );

        var service = BuildService(serviceMock.Object, producerMock.Object, metrics: metrics);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains("failure", outcomes);
        Assert.DoesNotContain("success", outcomes);
    }

    /// <summary>
    /// Attaches a <see cref="MeterListener"/> to <paramref name="metrics"/>'s meter and returns
    /// the list of <c>outcome</c> tag values recorded against <see cref="DataInjectorMetrics.WeakAppRequests"/>.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnePoll_PublishesEveryReadingUnderOneTrace()
    {
        using var listener = ListenToPolls();
        var serviceMock = new Mock<IWeakAppService>();
        using var cts = new CancellationTokenSource();
        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Success<IReadOnlyList<MeterReading>>(
                [MakeReading("energy", "A"), MakeReading("energy", "B"), MakeReading("energy", "C")]
            )
        );
        var traces = new ConcurrentBag<ActivityTraceId>();
        var producer = ProducerSignallingAfter(
            3,
            _ => traces.Add(Activity.Current?.TraceId ?? default),
            out var done
        );

        await RunUntilAsync(BuildService(serviceMock.Object, producer), done);

        Assert.Equal(3, traces.Count);
        Assert.Single(traces.Distinct());
        Assert.NotEqual(default, traces.First());
    }

    [Fact]
    public async Task ExecuteAsync_SuccessivePolls_GetTheirOwnTraces()
    {
        using var listener = ListenToPolls();
        var serviceMock = new Mock<IWeakAppService>();
        using var cts = new CancellationTokenSource();
        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Success<IReadOnlyList<MeterReading>>([MakeReading("energy", "First")]),
            Result.Success<IReadOnlyList<MeterReading>>([MakeReading("energy", "Second")])
        );
        var traces = new ConcurrentDictionary<string, ActivityTraceId>();
        var producer = ProducerSignallingAfter(
            2,
            reading => traces[reading.Name] = Activity.Current?.TraceId ?? default,
            out var done
        );

        await RunUntilAsync(BuildService(serviceMock.Object, producer), done);

        Assert.NotEqual(traces["First"], traces["Second"]);
    }

    [Fact]
    public async Task ExecuteAsync_ManyReadings_PublishesAtMostTheConfiguredNumberAtOnce()
    {
        var serviceMock = new Mock<IWeakAppService>();
        using var cts = new CancellationTokenSource();
        SetupSequenceWithCancel(
            serviceMock,
            cts,
            Result.Success<IReadOnlyList<MeterReading>>(
                [.. Enumerable.Range(0, 10).Select(i => MakeReading("energy", $"S{i}"))]
            )
        );
        var inFlight = 0;
        var peak = 0;
        var published = 0;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                InterlockedMax(ref peak, Interlocked.Increment(ref inFlight));
                await Task.Delay(20);
                Interlocked.Decrement(ref inFlight);

                if (Interlocked.Increment(ref published) == 10)
                {
                    done.TrySetResult();
                }
            });

        var service = BuildService(
            serviceMock.Object,
            producer.Object,
            kafkaOptions: new KafkaOptions { MaxConcurrentPublishes = 2 }
        );
        await RunUntilAsync(service, done.Task);

        Assert.Equal(2, peak);
    }

    private static ActivityListener ListenToPolls()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeterPollingService.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static IKafkaProducer ProducerSignallingAfter(
        int count,
        Action<MeterReading> onPublish,
        out Task done
    )
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = 0;
        var producer = new Mock<IKafkaProducer>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<MeterReading>(), It.IsAny<CancellationToken>()))
            .Callback<MeterReading, CancellationToken>(
                (reading, _) =>
                {
                    onPublish(reading);

                    if (Interlocked.Increment(ref published) == count)
                    {
                        signal.TrySetResult();
                    }
                }
            )
            .Returns(Task.CompletedTask);
        done = signal.Task;
        return producer.Object;
    }

    private static async Task RunUntilAsync(MeterPollingService service, Task done)
    {
        await service.StartAsync(TestContext.Current.CancellationToken);
        await done.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);

        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref target, value, current);

            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private static List<string> CaptureWeakAppRequestOutcomes(DataInjectorMetrics metrics)
    {
        var outcomes = new List<string>();
        var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DataInjectorMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
            {
                if (instrument == metrics.WeakAppRequests)
                {
                    var outcome = tags.ToArray().First(t => t.Key == "outcome").Value?.ToString();
                    if (outcome is not null)
                    {
                        outcomes.Add(outcome);
                    }
                }
            }
        );

        listener.Start();
        return outcomes;
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
        WeakAppOptions? options = null,
        DataInjectorMetrics? metrics = null,
        KafkaOptions? kafkaOptions = null
    )
    {
        var opts = Options.Create(options ?? new WeakAppOptions { PollingIntervalSeconds = 0 });
        return new MeterPollingService(
            weakAppService,
            kafkaProducer,
            opts,
            Options.Create(kafkaOptions ?? new KafkaOptions()),
            NullLogger<MeterPollingService>.Instance,
            metrics ?? new DataInjectorMetrics()
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
