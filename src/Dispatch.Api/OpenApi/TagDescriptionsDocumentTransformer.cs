using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Dispatch.Api.OpenApi;

// Dá nome + descrição pra cada categoria que aparece no Swagger UI — sem isso, o agrupamento
// funciona (cada .WithTags(...) já separa visualmente), mas fica sem a legenda ao lado do
// título, tipo "App/Auth · Authentication endpoints for..." no exemplo.
internal sealed class TagDescriptionsDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Tags = new HashSet<OpenApiTag>
        {
            new() { Name = OpenApiTags.Autenticacao, Description = "Login por e-mail e senha (RF-01/RF-02) — devolve o token JWT usado nos demais endpoints." },
            new() { Name = OpenApiTags.Conferentes, Description = "Cadastro, edição, presença na escala e remoção de conferentes (RF-25 a RF-27). Só Distribuidora." },
            new() { Name = OpenApiTags.Protocolos, Description = "Motor de distribuição — resolve prazo e decide o destino de um protocolo. Só Distribuidora." },
            new() { Name = OpenApiTags.Importacao, Description = "Importação de lote de protocolos (RF-05 a RF-12) — prévia e confirmação. Só Distribuidora." },
            new() { Name = OpenApiTags.CentralDeRegras, Description = "Alçada e prazos por equipe (RF-31 a RF-38). Só Distribuidora." },
            new() { Name = OpenApiTags.Sistema, Description = "Endpoints operacionais, sem regra de negócio." }
        };

        return Task.CompletedTask;
    }
}
