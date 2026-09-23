namespace DataProcessorService.Api.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Attaches the API-key requirement to the operations that actually carry authorization metadata,
/// so the probes and the metrics endpoint are not documented as needing a key they do not.
/// </summary>
public sealed class ApiKeySecurityOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresKey = context
            .ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>()
            .Any();

        if (!requiresKey)
        {
            return;
        }

        // The document has to come along: without it the reference cannot resolve the scheme it
        // names, and the requirement serialises as an empty object rather than as the scheme.
        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    ApiKeyAuthenticationHandler.SchemeName,
                    context.Document
                )] = [],
            },
        ];
    }
}
