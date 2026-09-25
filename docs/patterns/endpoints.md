---
name: endpoints
description: Convenções da borda HTTP — DTOs próprios da Api, "back manda fato cru, front resolve nome", mapeamento de resultado para status, validação de requests achatados (XOR), paginação, OpenAPI e mapa dos arquivos de endpoint
metadata:
  type: pattern
  domains: [api, minimal-api, dto, openapi, contrato]
  status: stable
---

# Endpoints e contrato HTTP

> A borda HTTP é minimal API ([ADR-0001](../decisions/0001-clean-architecture-em-quatro-projetos.md)).
> Este documento é o contrato de convenções entre `dispatch-api` e `dispatch-web`.

## Quando ler

- Ao criar ou alterar endpoint, request ou response.
- Ao decidir se um texto/rótulo deve ser montado no back ou no front.
- Ao adicionar variante nova a um tipo que a resposta traduz (alvo de alçada, payload de sugestão).

## DTOs e tradução

- Request/Response são **records próprios da Api**, no arquivo do endpoint. Entidade de Domain nunca
  vai para o JSON; resultado de caso de uso é traduzido por `switch` sobre a hierarquia fechada, com
  `_ => throw new InvalidOperationException($"Resultado não mapeado: ...")` no fim.
- Enums serializados como string (`JsonStringEnumConverter`) — legível, e não quebra se a ordem do
  enum mudar. `TimeOnly` sai nativo como `"HH:mm:ss"`. `TimeSpan` sai como `"hh:mm:ss.fffffff"`;
  entrada de duração/faixa editável pelo usuário é em **minutos** inteiros (config, ajuste de duração).
- **Campo novo vai no fim do record** (não quebra quem desestrutura posicionalmente).
- Mapeamento compartilhado não se duplica: `MinhaFilaEndpoints.ParaResumo`/`ParaResumoConcluido`
  (`internal`) servem Minha fila, Distribuição e `/conferentes/{id}/fila`. `ProtocoloResumo` é o DTO
  magro dos cards (hoje com `Prioridade`, `IniciadoEm`, `ConcluidoEm`, `Duracao`, `PausadoEm`,
  `AndamentoEm`, `EscreventeId`...); `DetalheProtocoloResponse` é o do painel.

## Back manda o fato cru, front resolve o nome

- O back devolve ids, enums e instantes; o front cruza com as listas que já carrega (`GET /escreventes`
  → `equipeId`, `GET /tipos-ato`, `GET /conferentes`) e decide rótulo e texto. Exemplos: `Prazo` vai
  como `TipoPrazo?` (não "D+1"/"1 hora"); `FaixaSemaforo`; `MotivoAlcada` é enum **sem nome próprio**
  embutido ("Testamento fora da alçada", "reservado a Márcio Gomes" são montados no front, mesmo o
  protótipo interpolando nomes); `numeroDisponivel` é booleano cru; `IndiceConfianca` vai 0.0–1.0.
- Cálculo de tempo decorrido (cronômetro) é no front a partir de `IniciadoEm`.
- **Exceções conscientes**: prévia de importação manda `Equipe` por **nome** (o escrevente da linha
  pode nem existir ainda — não dá para cruzar o que não existe); `AjusteDeDuracaoResponse.AjustadoPorNome`
  é resolvido no back (quem ajusta é uma Distribuidora fora de qualquer lista do front e não há
  `GET /usuarios` — [ADR-0035](../decisions/0035-ajuste-manual-de-duracao.md)); pedidos de reabertura
  trazem o nome do solicitante (join em memória no caso de uso).
- Regra de "não inventar dado que o back não calcula": sem fonte real (ex.: custo por ato), o campo
  não existe — não se preenche com estimativa.

## Status e erros

- Corpo de erro sempre `{ motivo: "..." }` — inclusive 404 (`Results.NotFound()` vazio foi corrigido
  na auditoria de qualidade). Quando o front precisa reagir diferente a cada desfecho (não só mostrar o
  texto), o corpo leva também `codigo` em **snake_case** estável — `{ codigo: "senha_fraca", motivo }`
  (`AuthEndpoints`, `ContaEndpoints`, `/protocolos/importar/converter`). O front decide pelo `codigo`, nunca
  pelo texto do `motivo`.
- **Upload de arquivo** (`/protocolos/importar/converter`, [ADR-0045](../decisions/0045-conector-de-relatorio-por-cartorio.md)):
  `multipart/form-data` com `IFormFile?` (anulável, pra devolver o nosso 400 `arquivo_ausente` em vez do 400
  vazio do binding), `.DisableAntiforgery()` (sem ele a rota falha em runtime — a autenticação é Bearer, sem
  cookie, então CSRF não se aplica), limite de tamanho checado no handler (413 com `codigo`) mais
  `RequestSizeLimitAttribute` como teto do Kestrel (413 sem corpo).
- 201 com id no corpo para criação (`Results.Created($"/x/{id}", new XResponse(id))`); 204 para
  mutação sem retorno; 404 não encontrado; 409 estado inválido/conflito (duplicado, status errado,
  já pendente); 400 validação de formato; 423 conta bloqueada (recuperação de senha); 401/403 pelo
  pipeline de autenticação.
- **Validar vs. clampar**: edição deliberada (`PUT /config`, pares de corte de horário) rejeita com 400
  e motivo — clampar esconderia erro de digitação. Parâmetro de leitura (`pagina`, `tamanhoPagina`) e
  valor derivado (`DefinirPesoDeComplexidadeDoTipoAto`) clampam.
- Validar existência de referência antes de construir entidade (o primeiro endpoint quebrava a FK
  com `TipoAtoId` inexistente).

## Requests achatados de tipos ricos (XOR)

`POST /regras-alcada` recebe campos planos (`sujeitoNivel` XOR `sujeitoConferenteId`; `alvoEtapa` /
`alvoTipoAtoId` / `alvoEquipeId`+`alvoEhEquipe` / `alvoTodosOsAtos` / `alvoGrupo` /
`alvoEhEquipeEEtapa`) e monta `SujeitoAlcada`/`AlvoAlcada` em `TentarMontarSujeito`/`TentarMontarAlvo`
— um array único de `(bool condicao, Func<AlvoAlcada> construir)`; variante nova entra num lugar só.
400 se não bater exatamente um.

- Quando dois alvos reaproveitam o mesmo campo (`PorEtapa` e `PorEquipeEEtapa` usam `AlvoEtapa`), a
  condição do antigo precisa **excluir** o novo explicitamente, senão os dois batem e quebram o XOR.
- Ponha na condição tudo que o construtor exige (`AlvoEtapa is not null`), senão request incompleto
  vira `NullReferenceException` (500) em vez de 400.
- **Na resposta, nunca `as AlvoAntigo` solto**: `ParaResponse` montava `alvoEtapa` com
  `(regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa` — para `PorEquipeEEtapa` saía `null` com a linha certa
  no banco. Use `switch` cobrindo todas as variantes, e ao criar variante que reaproveita colunas,
  varra todos os downcasts do tipo antigo.

## Listas, paginação e ordenação

- Toda lista tem ordem determinística (ver `ef-core.md`).
- Paginação (hoje só `GET /tipos-ato/com-uso`): query `busca`, `pagina`, `tamanhoPagina`; resposta
  `{ itens, total }` via `Paginado<T>` — formato padrão para qualquer paginação futura
  ([ADR-0029](../decisions/0029-paginacao-com-itens-e-total.md)). O resto usa busca + rolagem contida
  no front.
- Contagens que o front trata como ausência = 0 podem omitir zeros (`ConcluidosHojePorConferente`).

## OpenAPI / Swagger

- `Microsoft.AspNetCore.OpenApi` gera `/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` só a UI
  em `/swagger` (sem o SwaggerGen — sem gerador duplicado). Ligado também em produção.
- Rotas categorizadas por tag (`OpenApiTags`), `.WithName`, `.WithSummary`, `.Produces<T>(status)`.
- **Gotcha 1**: `Microsoft.AspNetCore.OpenApi` v10 gera `"enum": [...]` sem `"type": "string"`; o
  Swagger UI mostra "any". `EnumSchemaTransformer` (`IOpenApiSchemaTransformer`) preenche o `type`.
- **Gotcha 2**: uma classe pode implementar `IOpenApiDocumentTransformer` e
  `IOpenApiOperationTransformer`, mas **cada papel precisa ser registrado** (`AddDocumentTransformer<T>()`
  e `AddOperationTransformer<T>()`); registrando só um, o outro método nunca é chamado, em silêncio.

## Mapa dos arquivos de endpoint (`src/Dispatch.Api/Endpoints/`)

| Arquivo | Rotas principais | Papel |
| ------- | ---------------- | ----- |
| `AuthEndpoints` | `POST /auth/login`, `GET /auth/me` | anônimo / autenticado |
| `TotpEndpoints`, `RecuperacaoSenhaEndpoints` | `/auth/totp/*`, `/auth/recuperar/*` | autenticado / anônimo |
| `ImportacaoEndpoints` | `POST /protocolos/importar/pre-visualizar`, `/confirmar`, `/converter` (upload do `.xls` do cartório) | Distribuidora |
| `DistribuicaoEndpoints` | `GET /protocolos/distribuicao?loteImportacaoId=` | Distribuidora |
| `ProtocoloEndpoints` | `/protocolos/{id}/detalhe`, `observacao`, `atribuir`, `descartar`, `devolver-ao-pool`, `atribuir-ao-menos-carregado`, `definir-prioridade`, `reabrir-conferencia`, `ajustar-duracao`, `restaurar`, `PUT/DELETE /protocolos/{id}`, `/protocolos/manual[/simular]`, `redistribuir-pool`, `pedidos-reabertura/*` | Distribuidora (observação: os dois) |
| `MinhaFilaEndpoints` | `GET /minha-fila`, `/{id}/pegar`, `iniciar`, `pausar`, `retomar`, `concluir`, `corrigir-resultado`, `pedir-reabertura`, `pedidos-reabertura/{id}/cancelar`, `/concluidos-hoje` | Conferente |
| `ConferenteEndpoints` | `/conferentes` (`GET`, `POST`, `DELETE /{id}`, `vincular`, `{id}/nivel-jornada`, `{id}/perfil`, `{id}/presenca`, `cobertura`, `{id}/fila`, `{id}/concluidos-hoje`) | Distribuidora |
| `RegraAlcadaEndpoints` | `/regras-alcada` (CRUD, `ativar`, `desativar`, `testar`), `GET /conferentes/alcance` | Distribuidora |
| `TipoAtoEndpoints` | `/tipos-ato` (`GET` dois papéis), `com-uso`, `{id}`, `peso`, `grupo`, `ativar`, `desativar` | Distribuidora |
| `EquipeEndpoints` | `/equipes`, `/escreventes` (`GET` dois papéis), `sem-equipe`, `{id}/mover`, `POST /escreventes` | Distribuidora |
| `SugestaoEndpoints` | `GET /sugestoes`, `POST /sugestoes/gerar`, `/{id}/aplicar`, `/{id}/descartar`, `GET /sugestoes/historico` | Distribuidora |
| `DashboardEndpoints` | `GET /dashboard?periodo=Semana\|Mes\|Trimestre`, `GET /dashboard/hoje` (RF-42a) | os dois |
| `ConfiguracaoEndpoints` | `GET/PUT /config` | Distribuidora |
| `DevSeedEndpoints` | `POST /dev/seed-e2e` | anônimo, só Development |
| `Program.cs` | `/health`, `/health/db` | anônimo |


## Referências

- `arquitetura.md` (a Api nunca injeta repositório direto), `autorizacao.md` (grupos e papéis).
- ADR-0010, ADR-0029, ADR-0035, ADR-0045.
