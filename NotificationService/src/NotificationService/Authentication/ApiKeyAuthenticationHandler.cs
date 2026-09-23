namespace NotificationService.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;

/// <summary>
/// Authenticates a request by the shared key in the <c>X-Api-Key</c> header. The reverse proxy in
/// front of this service sets that header, so a browser never holds the key.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyOptions> apiKey
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    public const string HeaderName = "X-Api-Key";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.TryGetValue(HeaderName, out var presented))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Compared as chars rather than UTF-8 bytes to avoid a per-request allocation; the key is
        // ASCII either way.
        var matches = CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(presented.ToString().AsSpan()),
            MemoryMarshal.AsBytes(apiKey.Value.Key.AsSpan())
        );

        if (!matches)
        {
            return Task.FromResult(AuthenticateResult.Fail("The API key is not recognised."));
        }

        var identity = new ClaimsIdentity(SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
