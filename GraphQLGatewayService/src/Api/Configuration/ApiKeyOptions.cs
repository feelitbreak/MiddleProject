namespace GraphQLGatewayService.Api.Configuration;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The shared key callers must present in the <c>X-Api-Key</c> header. Bound from the "ApiKey"
/// section.
/// </summary>
/// <remarks>
/// Required, unlike the outbound credentials elsewhere in this system: a service that starts
/// without a key and then rejects every request looks broken rather than misconfigured.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Configuration binding target.")]
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    /// <summary>Gets or sets the expected key. Long enough not to be worth guessing at.</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string Key { get; set; } = string.Empty;
}
