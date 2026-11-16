using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Arena.Api;

public sealed class BearerOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "Arena Football Management API";
        document.Info.Description = "Week 1 foundation. All timestamps are UTC; monetary amounts are EGP. " +
            "Register or log in, then supply the access token as a Bearer token for protected operations.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
        {
            if (!description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any()) continue;
            var path = "/" + description.RelativePath;
            if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Operations is null) continue;
            var operationPair = pathItem.Operations.FirstOrDefault(x =>
                x.Key.Method.Equals(description.HttpMethod, StringComparison.OrdinalIgnoreCase));
            if (operationPair.Value is not { } operation) continue;
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
        return Task.CompletedTask;
    }
}
