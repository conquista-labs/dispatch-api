# Dispatch API

Back-end do Dispatch — distribuição e conferência de protocolos (atos notariais) de cartório, com
um motor de distribuição determinístico e auditável no lugar de uma planilha manual.

> Panorama do projeto (os três repositórios, papéis do sistema): `../CLAUDE.md`.
> **Fonte da verdade do domínio**: `../dispatch-prototype/Dispatch - Requisitos.dc.html` — releia
> antes de modelar qualquer coisa nova. Protótipo interativo aprovado (lógica-fonte do simulador de
> alçada, telas): `../dispatch-prototype/Dispatch v2.dc.html`. Onde o back diverge dele de propósito, está registrado em
> ADR ou em `docs/gaps-requisitos.md` (o documento é gerado por ferramenta externa do dono e não é
> editado daqui).

## Contexto de aprendizado

Dois objetivos do dono: entregar o sistema do documento de requisitos e aprender .NET na prática
(stack nova para ele — vem de Prisma/JS) junto com engenharia assistida por IA. Ao introduzir um
conceito novo de .NET ou de Claude Code, **explique o porquê antes de aplicar**, e registre o conceito
em `docs/patterns/conceitos-dotnet.md`.

## Stack

- .NET 10 (SDK `10.0.400`), C#; ASP.NET Core **minimal APIs** (não controllers).
- EF Core + Npgsql; PostgreSQL no **Neon** (produção) e em Docker (local).
- Deploy: Docker no **Render** (free tier, auto-deploy no push em `main`).
- Testes: xUnit; integração com `WebApplicationFactory` + Testcontainers + Respawn.

## Arquitetura

Clean Architecture em 4 projetos ([ADR-0001](docs/decisions/0001-clean-architecture-em-quatro-projetos.md)):

```
src/Dispatch.Domain          entidades, motor, alçada, prazos — C# puro, zero pacote de framework
src/Dispatch.Application     CasosDeUso/ + Portas/ — depende só de Domain
src/Dispatch.Infrastructure  EF Core, repositórios, migrations, adapters — Application + Domain
src/Dispatch.Api             Program.cs, Endpoints/, DTOs — Application + Infrastructure
tests/Dispatch.{Domain,Application,Api}.Tests
```

Uma camada só referencia as de dentro. Se algo em Domain/Application "precisa" de EF Core, a
abstração está no lugar errado. Detalhes e convenções: `docs/patterns/arquitetura.md`.

## Onde está cada coisa

| Documento | Leia quando |
| --------- | ----------- |
| `docs/patterns/arquitetura.md` | For criar caso de uso, porta, entidade ou endpoint; a API subir com "Failure to infer one or more parameters"; tiver dúvida de em que camada algo mora |
| `docs/patterns/ef-core.md` | For mapear propriedade/entidade, escrever migration ou método de repositório; um `SaveChanges` não gravar nada; uma lista vier fora de ordem; algo ficar lento em produção |
| `docs/patterns/endpoints.md` | For criar/alterar request, response ou rota; decidir se um texto é montado no back ou no front; adicionar variante a um tipo que a resposta traduz |
| `docs/patterns/autorizacao.md` | For decidir papel/grupo de um endpoint; mexer em JWT, senha, TOTP, recuperação ou auditoria de login; alguém receber 403 "tendo o papel" |
| `docs/patterns/motor-e-prazos.md` | For mexer em `Alcada/`, `Distribuicao/`, `Prazos/`, semáforo ou ciclo de vida do protocolo; explicar por que um protocolo foi para pool/exceção/alguém |
| `docs/patterns/indicadores-e-aprendizado.md` | For mexer em Dashboard (score, faixa, tempo, visão restrita) ou nas sugestões de aprendizado |
| `docs/patterns/testes.md` | For decidir que teste escrever; antes de declarar pronta uma mudança em persistência/DI/auth; um teste de integração ficar intermitente; medir cobertura |
| `docs/patterns/deploy.md` | Antes de dar push com migration ou env var nova; aplicar migration no Neon; clonar produção; o Render dizer "live" e a API não responder |
| `docs/patterns/conceitos-dotnet.md` | For explicar um mecanismo de .NET/EF/ASP.NET ao dono, ou encontrar algo que "parece mágica" |
| `docs/decisions/` (ADR-0001 a 0038) | Antes de mudar um comportamento que parece estranho — pode ser decisão registrada. Índice abaixo |
| `docs/gaps-requisitos.md` | For planejar trabalho novo; um RF parecer não implementado; fechar ou abrir uma lacuna (numeração §N estável) |
| `docs/historico.md` | Precisar do contexto de uma entrega passada (arquivos, como foi verificado, contagem de testes), ou um comentário no código disser "ver CLAUDE.md, seção X" — a seção está lá com o mesmo título |

**Histórico de entregas: `docs/historico.md` — não acrescente seções de changelog aqui; decisão nova
vira ADR (skill `/api-adr`), lição nova vai pro pattern doc ou pra skill.**

### Índice de decisões (por tema)

- **Arquitetura e infraestrutura**: 0001 Clean Architecture · 0014 Fly.io+Neon (substituído) → 0015
  Render · 0025 `/health` sem banco · 0036 testes de integração e cobertura.
- **Autenticação**: 0003 JWT próprio sem Identity · 0010 login devolve usuário + `/auth/me` · 0020 TOTP
  só para recuperação · 0028 uma conta com dois papéis.
- **Motor de alçada** (cadeia de supersessão): 0002 v1 → 0017 v2 → 0018 v3 cascata (vigente) · 0024 v4
  equipe-não-faz-etapa → 0030 absoluto fora da cascata.
- **Importação e prazos**: 0005 CSV com PDF fora · 0006 linha de corte, `Numero` nunca único · 0007
  vencimento a partir do `AndamentoEm` · 0008 → 0012 tipo novo cadastrado na importação · 0013 D+1/D+2
  em horas corridas + dia útil · 0037 corte de horário por equipe+etapa.
- **Protocolo e conferência**: 0011 carga calculada na leitura · 0016 pedido de reabertura · 0019
  exclusão soft-delete · 0021 "Normal" = Média · 0022 continuidade de conferência · 0027 atribuição
  manual sem alçada · 0031 reabrir → Atribuído · 0032 tempo por ciclo · 0033 pausar · 0034 reabertura
  recalcula vencimento · 0035 ajuste manual de duração · 0038 nº da conferência na leitura.
- **Dados e leitura**: 0004 remover conferente é soft delete · 0009 aprendizado sem `evento_decisao` ·
  0023 tabela `config` de linha única · 0026 30 dias nos concluídos · 0029 paginação `{ Itens, Total }`.

## Skills do projeto (`.claude/skills/`)

- **`/api-gate`** — cadeia de verificação no fim da tarefa (build → testes → `dotnet run`), cobertura
  honesta, como ler a saída. Rode uma vez, quando a tarefa estiver pronta.
- **`/new-use-case`** — caso de uso novo na Application respeitando portas/adapters.
- **`/new-endpoint`** — expor caso de uso como rota (grupo, `RequireRole`, status, DTOs, OpenAPI,
  teste de autorização).
- **`/add-domain-rule`** — regra nova ou alterada no motor de distribuição/alçada (teste primeiro,
  bordas de precedência, Domain sem framework).
- **`/ef-migration`** — criar e aplicar migration (schema só muda por migration).
- **`/api-testing-strategy`** — que teste a mudança precisa e onde ele vive.
- **`/verify-integration`** — validar comportamento real pela HTTP (teste de integração + smoke manual).
- **`/api-commit`** — da árvore verificada até um PR aberto (branch + `gh pr create`; nada direto no
  `main`, o hook bloqueia). Merge só quando o dono pedir — merge no `main` é deploy.
- **`/prod-ops`** — qualquer coisa que toque produção (migration no Neon, SQL avulso, clone, conferir
  deploy), com as travas de confirmação e segredo.
- **`/api-adr`** — registrar decisão com alternativas reais em `docs/decisions/`, com numeração e
  supersessão.

## Comandos

```bash
docker compose up -d                                  # Postgres local (dev)
dotnet build                                          # compila a solution
dotnet test                                           # 3 suítes; Dispatch.Api.Tests exige Docker
dotnet test tests/Dispatch.Domain.Tests               # só um tier (rápido)
dotnet run --project src/Dispatch.Api                 # sobe a API (Swagger em /swagger)
dotnet tool restore                                   # dotnet-ef e reportgenerator (tools locais)
dotnet ef migrations add Nome --project src/Dispatch.Infrastructure --startup-project src/Dispatch.Api
dotnet ef database update --project src/Dispatch.Infrastructure --startup-project src/Dispatch.Api
```

Cobertura (o `-classfilters` é obrigatório — ver `docs/patterns/testes.md`):

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport \
  -reporttypes:"Html;TextSummary" -classfilters:"-Dispatch.Infrastructure.Migrations.*"
```

## Deploy

Produção em `https://lab-dispatch-api.onrender.com` (Render, `render.yaml`), banco no Neon, front em
`https://lab-dispatch-web.netlify.app`. **Merge em `main` já é deploy** (toda mudança chega por PR) —
migration que o código novo precisa tem de estar aplicada no Neon antes do merge. Secrets só no dashboard do Render. Procedimentos (env
vars, migration em produção, primeira conta, clone anonimizado): `docs/patterns/deploy.md`.

## Armadilhas que toda sessão precisa saber

1. **Build e testes verdes não provam que a API sobe.** Caso de uso não registrado no DI só falha em
   runtime ("Failure to infer one or more parameters"). Depois de caso de uso, endpoint ou dependência
   nova: `dotnet run` e uma chamada real. → `docs/patterns/arquitetura.md`
2. **Fakes não têm FK, `CHECK`, change tracker nem Npgsql.** Mudança de persistência só está pronta
   depois de validada contra o Postgres (teste de integração ou `psql`). → `docs/patterns/testes.md`
3. **Objeto desconectado do change tracker não grava nada, sem erro** — classes-registro traduzidas
   (`RegraAlcada`, `Sugestao`), entidades projetadas em `.Select()`, objetos do cache. →
   `docs/patterns/ef-core.md`
4. **Propriedade só com getter precisa de `builder.Property(...)` explícito** antes do
   `migrations add`, senão o constructor binding falha. → `docs/patterns/ef-core.md`
5. **Migration em banco com dado**: `CHECK` novo pede backfill antes; `NOT NULL` pede `DEFAULT`
   coerente com o invariante; dropar coluna com dado pede backfill para o destino. →
   `docs/patterns/ef-core.md`
6. **Todo instante é UTC** (o Npgsql recusa offset ≠ 0). Horário de parede ("16h") só via
   `FusoHorario`. → `docs/patterns/motor-e-prazos.md`
7. **`RequireAuthorization` numa rota combina com E com o do `MapGroup`** — não substitui. Visão
   restrita é `IsInRole(Conferente) && !IsInRole(Distribuidora)`; papéis no JWT só mudam no próximo
   login. → `docs/patterns/autorizacao.md`
8. **Sem `ORDER BY` o Postgres não garante ordem**; desempate sempre por `Id`. Coluna sem FK não ganha
   índice de graça. → `docs/patterns/ef-core.md`
9. **Back manda o fato cru** (ids, enums, instantes); rótulo e texto são do front. Resposta traduz
   hierarquia fechada por `switch`, nunca por `as` solto. → `docs/patterns/endpoints.md`
10. **Renomear membro de enum quebra dado gravado** (enums são string no banco) — por isso `Normal`
    nunca virou `Media`. → [ADR-0021](docs/decisions/0021-prioridade-normal-mantida-como-media.md)

## Premissas de qualidade

1. Lógica de domínio (motor, precedência de alçada) nasce com teste de unidade antes ou junto da
   implementação — é a parte mais fácil de acertar errado e mais cara de errar em produção.
2. Fronteiras de camada são levadas a sério.
3. Schema só muda por EF Core Migration — nunca editar o Neon na mão.
4. Decisão tomada não fica só na conversa: com alternativas reais → ADR (`/api-adr`); lição ou armadilha
   → pattern ou skill; entrega → `docs/historico.md`; lacuna do requisito → `docs/gaps-requisitos.md`.
