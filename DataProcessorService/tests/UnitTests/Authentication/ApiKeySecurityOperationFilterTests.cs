namespace DataProcessorService.UnitTests.Authentication;

using DataProcessorService.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Covers which operations Swagger marks as needing a key. The probes and the metrics endpoint are
/// anonymous, and documenting a padlock on them would describe a constraint that does not exist.
/// </summary>
public sealed class ApiKeySecurityOperationFilterTests
{
    [Fact]
    public void Apply_EndpointRequiringAuthorization_AddsTheApiKeyRequirement()
    {
        var operation = new OpenApiOperation();

        new ApiKeySecurityOperationFilter().Apply(operation, ContextWith(new AuthorizeAttribute()));

        Assert.NotNull(operation.Security);
        var requirement = Assert.Single(operation.Security);
        var scheme = Assert.Single(requirement.Keys);
        Assert.Equal(ApiKeyAuthenticationHandler.SchemeName, scheme.Reference.Id);
    }

    [Fact]
    public void Apply_AnonymousEndpoint_LeavesSecurityUnset()
    {
        var operation = new OpenApiOperation();

        new ApiKeySecurityOperationFilter().Apply(operation, ContextWith());

        Assert.Null(operation.Security);
    }

    /// <summary>
    /// The reference has to resolve against the document, or the requirement serialises as an empty
    /// object and Swagger shows a padlock that sends nothing.
    /// </summary>
    [Fact]
    public void Apply_EndpointRequiringAuthorization_BindsTheReferenceToTheDocument()
    {
        var document = new OpenApiDocument();
        var operation = new OpenApiOperation();

        new ApiKeySecurityOperationFilter()
            .Apply(operation, ContextWith(document, new AuthorizeAttribute()));

        Assert.NotNull(operation.Security);
        var scheme = Assert.Single(Assert.Single(operation.Security).Keys);
        Assert.Same(document, scheme.Reference.HostDocument);
    }

    private static OperationFilterContext ContextWith(params object[] endpointMetadata) =>
        ContextWith(new OpenApiDocument(), endpointMetadata);

    private static OperationFilterContext ContextWith(
        OpenApiDocument document,
        params object[] endpointMetadata
    ) =>
        new(
            new ApiDescription
            {
                ActionDescriptor = new ActionDescriptor
                {
                    EndpointMetadata = [.. endpointMetadata],
                },
            },
            null!,
            null!,
            document,
            null!
        );
}
