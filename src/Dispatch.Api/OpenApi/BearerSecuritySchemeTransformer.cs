using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dispatch.Api.OpenApi;

// Sem isso, a spec OpenAPI não declara o esquema Bearer, e o Swagger UI não mostra o botão
// "Authorize" — dá pra chamar endpoint protegido só via curl com header manual. O esquema é
// registrado uma vez no documento (IOpenApiDocumentTransformer); o cadeado por operação só
// aparece em quem exige autenticação, não em /health ou /auth/login (IOpenApiOperationTransformer).
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var permiteAnonimo = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (!permiteAnonimo)
        {
            var referencia = new OpenApiSecuritySchemeReference("Bearer", context.Document);
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement { [referencia] = [] });
        }

        return Task.CompletedTask;
    }
}
