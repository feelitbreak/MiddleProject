namespace GraphQLGatewayService.UnitTests.RateLimiting;

using GraphQLGatewayService.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using ApiExtensions = GraphQLGatewayService.Api.Extensions.Extensions;

/// <summary>Drives the limiter registration through a real pipeline, holding a permit open.</summary>
public sealed class RequestLimitingTests : IAsyncDisposable
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? app;

    [Fact]
    public async Task Limiter_PermitAndQueueExhausted_RejectsWith429()
    {
        using var client = await this.StartAsync(permitLimit: 1, queueLimit: 0);
        var held = client.GetAsync("/limited", TestContext.Current.CancellationToken);
        await this.started.Task;

        using var rejected = await client.GetAsync("/limited", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        this.release.SetResult();
        using var completed = await held;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task Limiter_Rejection_IsShapedLikeAGraphQLError()
    {
        using var client = await this.StartAsync(permitLimit: 1, queueLimit: 0);
        var held = client.GetAsync("/limited", TestContext.Current.CancellationToken);
        await this.started.Task;

        using var rejected = await client.GetAsync("/limited", TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(
            await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );

        var code = body.RootElement.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code");
        Assert.Equal("TOO_MANY_REQUESTS", code.GetString());
        this.release.SetResult();
        (await held).Dispose();
    }

    [Fact]
    public async Task Limiter_EndpointWithoutThePolicy_IsNotLimited()
    {
        using var client = await this.StartAsync(permitLimit: 1, queueLimit: 0);
        var held = client.GetAsync("/limited", TestContext.Current.CancellationToken);
        await this.started.Task;

        using var probe = await client.GetAsync("/probe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        this.release.SetResult();
        (await held).Dispose();
    }

    [Fact]
    public async Task AddRequestLimiting_PermitLimitOutOfRange_FailsAtStartup() =>
        await Assert.ThrowsAsync<OptionsValidationException>(() =>
            this.StartAsync(permitLimit: 0, queueLimit: 0)
        );

    public async ValueTask DisposeAsync()
    {
        this.release.TrySetResult();

        if (this.app is not null)
        {
            await this.app.DisposeAsync();
        }
    }

    private async Task<HttpClient> StartAsync(int permitLimit, int queueLimit)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RateLimiting:QueueLimit"] = queueLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }
        );
        builder.Services.AddRequestLimiting(builder.Configuration);

        this.app = builder.Build();
        this.app.UseRateLimiter();
        this.app.MapGet(
                "/limited",
                async () =>
                {
                    this.started.TrySetResult();
                    await this.release.Task;
                    return "done";
                }
            )
            .RequireRateLimiting(ApiExtensions.GraphQLRateLimitPolicy);
        this.app.MapGet("/probe", () => "ok");

        await this.app.StartAsync(TestContext.Current.CancellationToken);
        return this.app.GetTestClient();
    }
}
