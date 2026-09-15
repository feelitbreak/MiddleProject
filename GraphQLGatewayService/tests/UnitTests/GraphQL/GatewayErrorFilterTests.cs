namespace GraphQLGatewayService.UnitTests.GraphQL;

using GraphQLGatewayService.Api.GraphQL.Errors;
using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// The filter is the only thing keeping internal detail out of a response, so these tests assert
/// what a client must never see as much as what it must.
/// </summary>
public sealed class GatewayErrorFilterTests
{
    private const string SecretText = "Host=db;Password=hunter2";

    [Fact]
    public void OnError_ErrorCarryingAnException_ReplacesTheMessage()
    {
        var error = FailedError();

        var filtered = CreateFilter().OnError(error);

        Assert.DoesNotContain(SecretText, filtered.Message, StringComparison.Ordinal);
        Assert.Equal("INTERNAL_SERVER_ERROR", filtered.Code);
    }

    [Fact]
    public void OnError_ErrorCarryingAnException_LeaksNothingAnywhereInTheError()
    {
        var filtered = CreateFilter().OnError(FailedError());

        var rendered = filtered.Message + string.Join(
            ";",
            (filtered.Extensions ?? new Dictionary<string, object?>()).Select(pair =>
                $"{pair.Key}={pair.Value}"
            )
        );

        Assert.DoesNotContain(SecretText, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Null(filtered.Exception);
    }

    [Fact]
    public void OnError_ErrorCarryingAnException_AddsACorrelationId()
    {
        var filtered = CreateFilter().OnError(FailedError());

        Assert.NotNull(filtered.Extensions);
        Assert.True(filtered.Extensions.TryGetValue("correlationId", out var correlationId));
        Assert.False(string.IsNullOrWhiteSpace(correlationId as string));
    }

    [Fact]
    public void OnError_ErrorCarryingAnException_KeepsThePath()
    {
        var filtered = CreateFilter().OnError(FailedError());

        Assert.Equal("sensors", filtered.Path?.ToString());
    }

    [Fact]
    public void OnError_SuccessiveFailures_GetDistinctCorrelationIds()
    {
        var filter = CreateFilter();

        var first = filter.OnError(FailedError());
        var second = filter.OnError(FailedError());

        Assert.NotEqual(
            first.Extensions!["correlationId"],
            second.Extensions!["correlationId"]
        );
    }

    [Fact]
    public void OnError_DeliberateError_PassesThroughUntouched()
    {
        // A validation failure already carries a message the caller can act on.
        var error = ErrorBuilder
            .New()
            .SetMessage("from must be earlier than to.")
            .SetCode("BAD_USER_INPUT")
            .Build();

        var filtered = CreateFilter().OnError(error);

        Assert.Same(error, filtered);
    }

    [Fact]
    public void OnError_ErrorCarryingAnException_IsLogged()
    {
        var logger = CreateLogger();

        CreateFilter(logger).OnError(FailedError());

        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void OnError_ErrorCarryingAnException_CountsAnInternalError()
    {
        using var metrics = new GraphQLGatewayMetrics();
        using var collector = new MetricCollector<long>(metrics.Errors);
        var filter = new GatewayErrorFilter(CreateLogger().Object, metrics);

        filter.OnError(FailedError());

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(
            "INTERNAL_SERVER_ERROR",
            measurement.Tags[GraphQLGatewayMetrics.ErrorCodeTag]
        );
    }

    [Fact]
    public void OnError_DeliberateError_CountsUnderItsOwnCode()
    {
        using var metrics = new GraphQLGatewayMetrics();
        using var collector = new MetricCollector<long>(metrics.Errors);
        var filter = new GatewayErrorFilter(CreateLogger().Object, metrics);

        filter.OnError(ErrorBuilder.New().SetMessage("bad").SetCode("BAD_USER_INPUT").Build());

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("BAD_USER_INPUT", measurement.Tags[GraphQLGatewayMetrics.ErrorCodeTag]);
    }

    [Fact]
    public void OnError_NullError_Throws() =>
        Assert.Throws<ArgumentNullException>(() => CreateFilter().OnError(null!));

    private static IError FailedError() =>
        ErrorBuilder
            .New()
            .SetMessage($"Failed to connect: {SecretText}")
            .SetException(new InvalidOperationException(SecretText))
            .SetPath(Path.Root.Append("sensors"))
            .SetExtension("stackTrace", "at Npgsql.Whatever()")
            .Build();

    private static Mock<ILogger<GatewayErrorFilter>> CreateLogger()
    {
        var logger = new Mock<ILogger<GatewayErrorFilter>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static GatewayErrorFilter CreateFilter(Mock<ILogger<GatewayErrorFilter>>? logger = null) =>
        new((logger ?? CreateLogger()).Object, new GraphQLGatewayMetrics());
}
