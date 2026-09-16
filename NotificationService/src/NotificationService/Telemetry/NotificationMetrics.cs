namespace NotificationService.Telemetry;

using System.Diagnostics.Metrics;

/// <summary>
/// Custom meter and instruments for NotificationService business metrics. Registered as a singleton
/// so the consumer and the hub share the same instrument instances.
/// <para>
/// Instrument names follow the repository-wide
/// <c>&lt;service&gt;.&lt;subsystem&gt;.&lt;noun&gt;</c> convention, which Prometheus renders as
/// <c>notification_kafka_events_consumed_total</c> and friends.
/// </para>
/// <para>
/// Connected clients are deliberately absent: ASP.NET Core's own
/// <c>Microsoft.AspNetCore.Http.Connections</c> meter already exports
/// <c>signalr_server_active_connections</c>, tagged by transport.
/// </para>
/// </summary>
public sealed class NotificationMetrics : IDisposable
{
    public const string MeterName = "NotificationService";

    private readonly Meter meter;

    /// <summary>Initializes a new instance of the <see cref="NotificationMetrics"/> class.</summary>
    public NotificationMetrics()
    {
        this.meter = new Meter(MeterName, "1.0.0");

        this.EventsConsumed = this.meter.CreateCounter<long>(
            "notification.kafka.events_consumed",
            unit: "{event}",
            description: "Number of messages consumed from the readings-persisted topic."
        );

        this.EventsPushed = this.meter.CreateCounter<long>(
            "notification.signalr.events_pushed",
            unit: "{event}",
            description: "Number of events broadcast to connected clients."
        );

        this.ConsumerLag = this.meter.CreateHistogram<double>(
            "notification.kafka.consumer_lag",
            unit: "s",
            description: "Age of an event when it is broadcast, measured from its publication time."
        );
    }

    public Counter<long> EventsConsumed { get; }

    /// <summary>Trails <see cref="EventsConsumed"/> by the messages that could not be decoded.</summary>
    public Counter<long> EventsPushed { get; }

    /// <summary>
    /// How far behind the pipeline the dashboard is, in seconds: the interval between
    /// DataProcessorService committing a batch and this service pushing the signal for it.
    /// </summary>
    public Histogram<double> ConsumerLag { get; }

    /// <inheritdoc/>
    public void Dispose() => this.meter.Dispose();
}
