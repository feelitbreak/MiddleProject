namespace DataInjectorService.Tests.Extensions;

using DataInjectorService.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Tests the origin-matching predicate registered by
/// <see cref="Extensions.AddCorsConfiguration"/>. The surrounding registration code is excluded
/// from coverage as boilerplate, but the predicate itself decides who may call the service, so it
/// is verified here by resolving the built "AllowOrigins" policy and probing it directly.
/// </summary>
public sealed class CorsConfigurationTests
{
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://localhost")]
    [InlineData("http://LOCALHOST:3000")]
    public void AllowOrigins_LocalhostAllowed_PermitsLocalhostOrigins(string origin)
    {
        var policy = BuildPolicy();

        Assert.True(policy.IsOriginAllowed(origin));
    }

    [Fact]
    public void AllowOrigins_LocalhostDisallowed_RejectsLocalhostOrigin()
    {
        var policy = BuildPolicy(allowLocalhost: false);

        Assert.False(policy.IsOriginAllowed("http://localhost:5173"));
    }

    [Fact]
    public void AllowOrigins_ConfiguredOrigin_IsAllowed()
    {
        var policy = BuildPolicy(allowedOrigins: ["https://app.example.com"]);

        Assert.True(policy.IsOriginAllowed("https://app.example.com"));
    }

    [Fact]
    public void AllowOrigins_ConfiguredWithTrailingSlash_MatchesOriginWithout()
    {
        var policy = BuildPolicy(allowedOrigins: ["  https://app.example.com/  "]);

        Assert.True(policy.IsOriginAllowed("https://app.example.com"));
    }

    [Fact]
    public void AllowOrigins_UnknownOrigin_IsRejected()
    {
        var policy = BuildPolicy(allowedOrigins: ["https://app.example.com"]);

        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
    }

    [Fact]
    public void AllowOrigins_MalformedOrigin_IsRejected()
    {
        var policy = BuildPolicy(allowedOrigins: ["https://app.example.com"]);

        Assert.False(policy.IsOriginAllowed("not-a-uri"));
    }

    [Fact]
    public void AllowOrigins_NoConfiguredOrigins_RejectsNonLocalhost()
    {
        var policy = BuildPolicy(allowLocalhost: false);

        Assert.False(policy.IsOriginAllowed("https://app.example.com"));
    }

    [Fact]
    public void AllowOrigins_AllowsAnyHeaderAndMethod()
    {
        var policy = BuildPolicy();

        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
    }

    /// <summary>
    /// Registers the CORS configuration against an in-memory configuration source and returns the
    /// resulting "AllowOrigins" policy.
    /// </summary>
    /// <param name="allowLocalhost">Value bound to <c>Cors:AllowLocalhost</c>.</param>
    /// <param name="allowedOrigins">Values bound to <c>Cors:AllowedOrigins</c>.</param>
    /// <returns>The built CORS policy.</returns>
    private static CorsPolicy BuildPolicy(
        bool allowLocalhost = true,
        string[]? allowedOrigins = null
    )
    {
        var configuration = new ConfigurationManager();
        var settings = new Dictionary<string, string?>
        {
            ["Cors:AllowLocalhost"] = allowLocalhost.ToString(),
        };

        for (var i = 0; i < (allowedOrigins?.Length ?? 0); i++)
        {
            settings[$"Cors:AllowedOrigins:{i}"] = allowedOrigins![i];
        }

        configuration.AddInMemoryCollection(settings);

        var services = new ServiceCollection();
        services.AddCorsConfiguration(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<CorsOptions>>();
        var policy = options.Value.GetPolicy("AllowOrigins");

        Assert.NotNull(policy);
        return policy;
    }
}
