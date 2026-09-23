namespace DataProcessorService.UnitTests.Authentication;

using DataProcessorService.Api.Authentication;
using DataProcessorService.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

/// <summary>
/// Covers the inbound key check. The distinction between a missing header and a wrong one only
/// shows up in logs, but both must end as a challenge rather than as a pass.
/// </summary>
public sealed class ApiKeyAuthenticationHandlerTests
{
    private const string ConfiguredKey = "configured-api-key-0123456789abcdef";

    [Fact]
    public async Task HandleAuthenticate_NoHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(presented: null);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticate_CorrectKey_Succeeds()
    {
        var result = await AuthenticateAsync(presented: ConfiguredKey);

        Assert.True(result.Succeeded);
        Assert.Equal(ApiKeyAuthenticationHandler.SchemeName, result.Ticket.AuthenticationScheme);
    }

    [Fact]
    public async Task HandleAuthenticate_WrongKeyOfSameLength_Fails()
    {
        var wrong = new string('x', ConfiguredKey.Length);

        var result = await AuthenticateAsync(presented: wrong);

        Assert.False(result.Succeeded);
        Assert.False(result.None);
    }

    /// <summary>
    /// Pins the fixed-time comparison against being "fixed" into a length check plus ==, which
    /// would pass a prefix of the real key.
    /// </summary>
    [Fact]
    public async Task HandleAuthenticate_PrefixOfTheKey_Fails()
    {
        var result = await AuthenticateAsync(presented: ConfiguredKey[..^1]);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticate_EmptyHeader_Fails()
    {
        var result = await AuthenticateAsync(presented: string.Empty);

        Assert.False(result.Succeeded);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(string? presented)
    {
        var handler = new ApiKeyAuthenticationHandler(
            new SchemeOptionsMonitor(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new ApiKeyOptions { Key = ConfiguredKey })
        );

        var context = new DefaultHttpContext();

        if (presented is not null)
        {
            context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = presented;
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationHandler.SchemeName,
                displayName: null,
                typeof(ApiKeyAuthenticationHandler)
            ),
            context
        );

        return await handler.AuthenticateAsync();
    }

    private sealed class SchemeOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => this.CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
