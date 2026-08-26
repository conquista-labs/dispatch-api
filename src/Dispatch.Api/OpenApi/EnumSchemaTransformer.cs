using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dispatch.Api.OpenApi;

// O gerador de schema do .NET produz "enum": [...] pra enum, mas sem "type": "string" —
// tecnicamente válido em JSON Schema (os valores já são autoexplicativos), mas o Swagger UI
// não sabe rotular isso e mostra "any" em vez do enum. Só preenche o "type" que faltou.
internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var tipo = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (tipo.IsEnum && schema.Enum is { Count: > 0 })
        {
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }
}
