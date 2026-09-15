namespace GraphQLGatewayService.UnitTests.GraphQL;

using GraphQLGatewayService.Api.GraphQL.Errors;
using GraphQLGatewayService.Infrastructure.Telemetry;
using HotChocolate;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Errors refused before execution never reach the error filter, so without this listener they
/// would be invisible: the HTTP response is still 200.
/// </summary>
public sealed class GraphQLDiagnosticsListenerTests
{
    [Fact]
    public void ValidationErrors_EachError_IsCounted()
    {
        using var metrics = new GraphQLGatewayMetrics();
        using var collector = new MetricCollector<long>(metrics.Errors);

        CreateListener(metrics).ValidationErrors(
            context: null!,
            [Coded("HC0011"), Coded("HC0011")]
        );

        Assert.Equal(2, collector.GetMeasurementSnapshot().Count);
    }

    [Fact]
    public void RequestError_UncodedError_IsCountedAsValidation()
    {
        using var metrics = new GraphQLGatewayMetrics();
        using var collector = new MetricCollector<long>(metrics.Errors);

        // An unknown field arrives with no code set, which is the common case for this event.
        CreateListener(metrics).RequestError(
            context: null!,
            ErrorBuilder.New().SetMessage("The field `nope` does not exist.").Build()
        );

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("VALIDATION", measurement.Tags[GraphQLGatewayMetrics.ErrorCodeTag]);
    }

    [Fact]
    public void RequestError_CodedError_IsCountedUnderThatCode()
    {
        using var metrics = new GraphQLGatewayMetrics();
        using var collector = new MetricCollector<long>(metrics.Errors);

        CreateListener(metrics).RequestError(context: null!, Coded("HC0047"));

        Assert.Equal(
            "HC0047",
            Assert.Single(collector.GetMeasurementSnapshot()).Tags[GraphQLGatewayMetrics.ErrorCodeTag]
        );
    }

    [Fact]
    public void ValidationErrors_NullList_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            CreateListener(new GraphQLGatewayMetrics()).ValidationErrors(context: null!, errors: null!)
        );

    private static IError Coded(string code) =>
        ErrorBuilder.New().SetMessage("rejected").SetCode(code).Build();

    private static GraphQLDiagnosticsListener CreateListener(GraphQLGatewayMetrics metrics)
    {
        var logger = new Mock<ILogger<GraphQLDiagnosticsListener>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return new GraphQLDiagnosticsListener(logger.Object, metrics);
    }
}
