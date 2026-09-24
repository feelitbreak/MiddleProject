namespace DataInjectorService.Tests.Extensions;

using DataInjectorService.Common;
using DataInjectorService.Extensions;
using DataInjectorService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

/// <summary>Drives the registered WeakApp client, resilience pipeline included, against a stub.</summary>
public sealed class WeakAppResilienceTests
{
    [Fact]
    public async Task GetMetersAsync_RateLimited_IsNotRetried()
    {
        var weakApp = new StubWeakApp(HttpStatusCode.TooManyRequests);
        await using var provider = BuildProvider(weakApp, retryCount: 3);

        var result = await provider
            .GetRequiredService<IWeakAppService>()
            .GetMetersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCode.RateLimited, result.Error.Code);
        Assert.Equal(1, weakApp.Calls);
    }

    [Fact]
    public async Task GetMetersAsync_ServerError_IsStillRetried()
    {
        var weakApp = new StubWeakApp(HttpStatusCode.ServiceUnavailable);
        await using var provider = BuildProvider(weakApp, retryCount: 1);

        var result = await provider
            .GetRequiredService<IWeakAppService>()
            .GetMetersAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(2, weakApp.Calls);
    }

    private static ServiceProvider BuildProvider(StubWeakApp weakApp, int retryCount)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["WeakApp:BaseUrl"] = "http://weak-app",
                    ["WeakApp:RetryCount"] = retryCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    ["WeakApp:RetryBaseDelaySeconds"] = "1",
                    ["Kafka:BootstrapServers"] = "localhost:9092",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataInjectorServices(configuration);
        services.AddHttpClient("WeakApp").ConfigurePrimaryHttpMessageHandler(() => weakApp);

        return services.BuildServiceProvider();
    }

    private sealed class StubWeakApp(HttpStatusCode status) : HttpMessageHandler
    {
        private int calls;

        public int Calls => this.calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref this.calls);

            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        }
    }
}
