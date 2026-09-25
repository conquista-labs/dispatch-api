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
            new() { Name = OpenApiTags.Conferentes, Description = "Conferentes (RF-25 a RF-30). Leitura e presença na escala: Distribuidora. Cadastro, vínculo, edição de perfil/nível/jornada e remoção: só Administrador (ADR-0039). O nível só aparece pra Administrador." },
            new() { Name = OpenApiTags.Protocolos, Description = "Motor de distribuição — resolve prazo e decide o destino de um protocolo. Só Distribuidora." },
            new() { Name = OpenApiTags.Importacao, Description = "Importação de lote de protocolos (RF-05 a RF-12) — conversão do relatório do cartório, prévia e confirmação. Só Distribuidora." },
            new() { Name = OpenApiTags.CentralDeRegras, Description = "Alçada e prazos por equipe (RF-31 a RF-38). Leitura: Distribuidora (nível das regras oculto). Escrita, simulador e sugestões: só Administrador (ADR-0039)." },
            new() { Name = OpenApiTags.MinhaFila, Description = "Fila e ações do próprio conferente — pegar, iniciar, concluir e ver concluídos do dia (RF-19 a RF-24). Só Conferente." },
            new() { Name = OpenApiTags.Dashboard, Description = "KPIs, score e desempenho por período (RF-42 a RF-46) e a faixa de hoje (RF-42a). Administrador vê tudo; Distribuidora sem nível, score nem faixa (RF-43a); Conferente só os próprios números." },
            new() { Name = OpenApiTags.Contas, Description = "Contas de gestão — administradores e distribuidoras (RF-44 a RF-47). Só Administrador." },
            new() { Name = OpenApiTags.Sistema, Description = "Endpoints operacionais, sem regra de negócio." }
        };

        return Task.CompletedTask;
    }
}
