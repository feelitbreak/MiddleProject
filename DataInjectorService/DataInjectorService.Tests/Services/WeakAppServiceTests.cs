namespace DataInjectorService.Tests.Services;

using DataInjectorService.Common;
using DataInjectorService.Configuration;
using DataInjectorService.Models;
using DataInjectorService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

/// <summary>
/// Tests that <see cref="WeakAppService"/> correctly classifies all documented WeakApp
/// response variants into the appropriate <see cref="Result{T}"/> outcome.
/// Uses <see cref="MockHttpMessageHandler"/> to control HTTP responses without a real server,
/// and a stub <see cref="IHttpClientFactory"/> to satisfy the constructor dependency.
/// </summary>
public sealed class WeakAppServiceTests
{
    [Fact]
    public async Task GetMetersAsync_ValidArray_ReturnsSuccess()
    {
        const string body = """
            [
              {"type":"energy","name":"Kitchen","payload":{"energy":369.62}},
              {"type":"motion","name":"Office","payload":{"motionDetected":true}}
            ]
            """;

        var service = BuildService(Json(HttpStatusCode.OK, body));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task GetMetersAsync_ValidArray_ResolvesPayloadTypes()
    {
        const string body = """
            [{"type":"air_quality","name":"Office","payload":{"co2":794,"pm25":25,"humidity":70}}]
            """;

        var service = BuildService(Json(HttpStatusCode.OK, body));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<AirQualityPayload>(result.Value[0].Payload);
    }

    [Fact]
    public async Task GetMetersAsync_EmptyArray_ReturnsFailure()
    {
        var service = BuildService(Json(HttpStatusCode.OK, "[]"));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Failed, result.Error.Code);
    }

    [Fact]
    public async Task GetMetersAsync_CorruptedSentinel_ReturnsFailure()
    {
        var service = BuildService(Json(HttpStatusCode.OK, """{"error":"data corrupted"}"""));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Failed, result.Error.Code);
    }

    [Fact]
    public async Task GetMetersAsync_InvalidJson_ReturnsFailure()
    {
        var service = BuildService(Json(HttpStatusCode.OK, "not json at all"));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Failed, result.Error.Code);
    }

    [Fact]
    public async Task GetMetersAsync_429_ReturnsRateLimited()
    {
        var service = BuildService(Json(HttpStatusCode.TooManyRequests, string.Empty));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.RateLimited, result.Error.Code);
        Assert.True(result.Error.RetryAfter!.Value.TotalSeconds > 0);
    }

    [Fact]
    public async Task GetMetersAsync_429_HonoursRetryAfterHeader()
    {
        var response = Json(HttpStatusCode.TooManyRequests, string.Empty);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(120));

        var service = BuildService(response);

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.RateLimited, result.Error.Code);
        Assert.Equal(120, result.Error.RetryAfter!.Value.TotalSeconds, precision: 0);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetMetersAsync_ServerError_ReturnsFailure(HttpStatusCode status)
    {
        var service = BuildService(Json(status, string.Empty));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Failed, result.Error.Code);
    }

    [Fact]
    public async Task GetMetersAsync_NetworkException_ReturnsFailure()
    {
        var service = BuildServiceWithThrowingHandler(
            new HttpRequestException("connection refused"));

        var result = await service.GetMetersAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Failed, result.Error.Code);
    }

    [Fact]
    public async Task GetMetersAsync_HostCancellation_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = BuildService(Json(HttpStatusCode.OK, "[]"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetMetersAsync(cts.Token));
    }

    private static WeakAppService BuildService(HttpResponseMessage response)
    {
        var factory = new StubHttpClientFactory(new MockHttpMessageHandler(response));
        var opts = Options.Create(new WeakAppOptions
        {
            BaseUrl = "http://weak-app-test",
            ApiKey = "some-secret",
            RateLimitDelaySeconds = 60,
        });

        return new WeakAppService(factory, opts, NullLogger<WeakAppService>.Instance);
    }

    private static WeakAppService BuildServiceWithThrowingHandler(Exception exception)
    {
        var factory = new StubHttpClientFactory(new ThrowingHttpMessageHandler(exception));
        var opts = Options.Create(new WeakAppOptions
        {
            BaseUrl = "http://weak-app-test",
        });

        return new WeakAppService(factory, opts, NullLogger<WeakAppService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}

/// <summary>
/// Stub <see cref="IHttpClientFactory"/> that always returns an <see cref="HttpClient"/>
/// backed by the provided <see cref="HttpMessageHandler"/>.
/// </summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <inheritdoc/>
    public HttpClient CreateClient(string name) =>
        new HttpClient(handler) { BaseAddress = new Uri("http://weak-app-test") };
}

/// <summary>Returns a pre-configured <see cref="HttpResponseMessage"/> for every request.</summary>
internal sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(response);
    }
}

/// <summary>Always throws the supplied exception.</summary>
internal sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw exception;
}
