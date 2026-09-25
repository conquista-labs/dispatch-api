---
name: new-endpoint
description: Expõe um caso de uso como rota HTTP no Dispatch.Api (Minimal APIs) seguindo as convenções do repositório — grupo, RequireRole certo, mapeamento do resultado pra status code, DTOs record, OpenAPI e o teste de autorização. Use ao criar ou alterar uma rota, ao mudar quem pode chamar uma rota, ou quando o front precisar de um endpoint que ainda não existe.
---

# new-endpoint

A rota é a camada mais fina: recebe HTTP, chama **um** caso de uso (skill `new-use-case`) e traduz
o resultado. Regra de negócio nunca mora aqui.

## Onde as coisas ficam

| O quê | Onde |
|---|---|
| Rotas de uma área | `src/Dispatch.Api/Endpoints/<Area>Endpoints.cs`, método `Map<Area>Endpoints` |
| Registro das rotas | `src/Dispatch.Api/Program.cs` (`app.Map<Area>Endpoints();`) |
| Registro do caso de uso no DI | `src/Dispatch.Infrastructure/ServiceCollectionExtensions.cs` (`services.AddScoped<X>()`) |
| Categoria do Swagger | `src/Dispatch.Api/OpenApi/OpenApiTags.cs` + descrição em `TagDescriptionsDocumentTransformer.cs` |
| Id do usuário logado | `usuario.ObterUsuarioId()` (`ClaimsPrincipalExtensions.cs`) — nunca `FindFirstValue` na mão |
| DTOs | `public sealed record XRequest(...)` / `XResponse(...)` no **fim** do mesmo arquivo de endpoints |

## Passos

1. **Autorização primeiro.** Decida o papel olhando o documento de requisitos (§3 "Papéis e
   permissões" e o RF da tela). A restrição é sempre no servidor (RNF-04).
   - No grupo: `app.MapGroup("/x").RequireAuthorization(p => p.RequireRole(nameof(Papel.Distribuidora)))`.
   - Papéis dentro de **um** `RequireRole(a, b)` são **OU**. Política de grupo + política de rota
     somam como **E**: `RequireRole(Administrador)` numa rota dentro de um grupo Distribuidora quer
     dizer "Distribuidora **e** Administrador".
   - Rota de conferente que age sobre "o meu": resolva o conferente pelo `ObterUsuarioId()` e deixe
     o caso de uso recusar o que não é dele — o id do conferente nunca vem do corpo do request.
   - Dado sensível (nível, score, faixa) só com a flag de administrador passada pelo endpoint pro
     caso de uso (invariante do perfil Administrador) — nunca "o front esconde".
2. **Traduza o resultado com `switch` exaustivo.** Casos de uso devolvem um `abstract record
   ResultadoX` com `sealed record` por desfecho (`Sucesso`, `NaoEncontrado`, `JaExiste`...). Mapeie
   cada um (`Results.Created/NoContent/NotFound/Conflict(new { motivo = "..." })`) e termine com
   `_ => throw new InvalidOperationException($"Resultado não mapeado: ...")` — desfecho novo sem
   mapeamento quebra alto, em vez de virar 200 silencioso. `motivo` em português, legível pro
   usuário final (o front mostra).
3. **DTO de fato cru.** Ids e enums crus, o front resolve nome (exceção documentada:
   `ajustadoPorNome`). Enum sai como string (há `EnumSchemaTransformer`). Campo novo em DTO
   existente vai **no fim** do record — o front e os fakes quebram menos.
4. **OpenAPI**: `.WithName("VerboDeNegocio")` (igual ao caso de uso), `.WithSummary("RF-xx — ...")`,
   `.WithTags(OpenApiTags.X)` e um `.Produces<T>(status)` por desfecho.
5. **Teste de autorização** em `tests/Dispatch.Api.Tests` (integração com Testcontainers — fake
   nenhum simula `RequireRole`): o papel que **não** pode leva 403, e o par que pode passa (prova
   que o 403 é sobre papel, não request malformado). Modelo: `AutorizacaoIntegracaoTests.cs`.
6. **Verifique de verdade** com a skill `verify-integration` (API rodando, `curl` com token real).
7. Mudou contrato que o front consome? Avise quem chamou o que mudou no JSON (camelCase) — o tipo
   espelhado mora em `dispatch-web/src/entities/<x>/model/types.ts`.
