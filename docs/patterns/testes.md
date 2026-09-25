---
name: testes
description: Tiers de teste do dispatch-api (Domain, Application com fakes, integração HTTP com Testcontainers), o que cada um não pega, verificação manual obrigatória e relatório de cobertura
metadata:
  type: pattern
  domains: [testes, qualidade, integracao, cobertura]
  status: stable
---

# Estratégia de testes e verificação

> Decisão de ferramental: [ADR-0036](../decisions/0036-testes-de-integracao-e-cobertura.md). Cadeia de
> verificação no fim de uma tarefa: skill `api-gate`. Smoke test manual: skill `verify-integration`.

## Quando ler

- Decidindo que teste uma mudança precisa e em qual projeto.
- Antes de declarar pronta uma feature que mexe em persistência, DI, autenticação ou endpoint.
- Quando um teste de integração falha de forma intermitente.

## Tiers

| Projeto | O que cobre | Roda com | Não pega |
| ------- | ----------- | -------- | -------- |
| `Dispatch.Domain.Tests` | Motor, precedência de alçada, prazos, semáforo, transições de `Protocolo`, gerador de sugestões | `dotnet test` (rápido) | Nada de persistência |
| `Dispatch.Application.Tests` | Casos de uso com fakes em memória (`Fakes.cs`) | `dotnet test` (rápido) | FK, `CHECK`, change tracker, restrições do Npgsql, DI |
| `Dispatch.Api.Tests` | HTTP real via `WebApplicationFactory<Program>` contra Postgres efêmero | `dotnet test` — **exige Docker** | Composition root de verdade só parcialmente; UI |
| Manual (`dotnet run` + curl/psql) | A API sobe, endpoints tocados respondem, valor gravado confere | skill `verify-integration` | — |
| E2E do `dispatch-web` (Playwright) | Fluxos reais com login | no repo do front | — |

Regra de domínio nasce com teste antes ou junto da implementação (skill `add-domain-rule`),
incluindo bordas de precedência.

## Por que fakes não bastam — bugs reais que só apareceram contra o Postgres

- **FK**: `/protocolos/distribuir` confiava no `TipoAtoId` do request; Guid inexistente quebrava a FK
  ao gravar. Fakes não têm FK para violar.
- **Change tracker**: `Sugestao` aplicada continuava `Pendente` (ver `ef-core.md`). Fakes não têm
  change tracker para perder a referência.
- **Npgsql**: `DateTimeOffset` com offset ≠ 0 dava 500 — primeiro achado do primeiro `dotnet test` da
  suíte de integração.
- **DI**: caso de uso não registrado só explode no `dotnet run`.
- **JWT**: cast para o tipo errado de token e claim `iat` ausente passaram em build/test; só login
  real + chamada autenticada provou (ver `autorizacao.md`).
- **Autorização por papel**: `GET /equipes` 403 para Conferente fazia o filtro do front "funcionar"
  com tudo em "sem equipe" — só uma asserção de comportamento no Playwright (contagem antes/depois)
  pegou; screenshot e typecheck não.
- **Leitura de resposta**: `GET /regras-alcada` devolvia `alvoEtapa: null` com a linha certa no banco;
  o smoke com curl conferiu que o campo *existia*, não que o *valor* batia.

Lição: **sempre validar contra o banco real antes de considerar pronto**, e no smoke test comparar o
valor com o que foi gravado (`psql`), não só a presença do campo.

## Suíte de integração (`tests/Dispatch.Api.Tests`)

- `DispatchApiFactory`, `IntegracaoFixture`, `IntegracaoTestBase`: um container
  `Testcontainers.PostgreSql` para a suíte inteira, `Database.Migrate()` do zero (teste de fumaça do
  schema — pega "CHECK novo sem backfill"), **Respawn** entre testes com `TablesToIgnore`
  `configuracao` (linha semeada pela migration; truncar quebraria o `SingleAsync`) e
  `__EFMigrationsHistory`.
- Login real via `POST /dev/seed-e2e` (mesmo endpoint do `globalSetup` do Playwright do front).
- **Re-semear invalida tokens antigos**: o seed reseta a senha, o que bumpa
  `SessoesValidasApartirDe` (RF-01k). Pegar token de Distribuidora, logar como Conferente (que re-semeia)
  e voltar a usar o token antigo dava 401 intermitente (sempre que um segundo virava entre os logins).
  Desde 2026-09-25 `IntegracaoTestBase.AutenticarComoAsync` **semeia uma vez por teste** e só loga nas
  chamadas seguintes — clientes de papéis diferentes convivem no mesmo teste. Chamar `/dev/seed-e2e` à
  mão no meio de um teste traz o problema de volta.
- Quando um teste falha com status HTTP inesperado, a mensagem do `Assert` traz o corpo da resposta;
  em Development o `DeveloperExceptionPage` devolve a stack trace real.
- `Program.cs` termina com `public partial class Program;` (top-level statements geram classe
  `internal`; a factory precisa dela pública).
- `Microsoft.EntityFrameworkCore` pinado em 10.0.11 no `.csproj` de teste — sem isso o MSBuild
  resolvia 10.0.4 transitivo e dava MSB3277.
- Arquivos atuais: `SchemaTests`, `RegraAlcadaIntegracaoTests`, `AutorizacaoIntegracaoTests`,
  `ImportacaoIntegracaoTests`, `CorteDeHorarioIntegracaoTests` (round-trip do `Prazo` com corte +
  reabertura reusando o `Prazo` recarregado).
- Escreva teste de integração mirando **bug de histórico documentado**, não cobertura de enfeite.

## Lições de escrita de teste

- **Asserções completas**: o teste da visão restrita do Dashboard checava nome, faixa, média da casa
  e tipo de ato, mas nunca `Kpis` — o KPI do topo vazava o total da operação. Pergunte "e o campo X,
  é meu ou de todo mundo?".
- **Teste que para no meio do fluxo esconde bug**: o teste de reabertura só olhava o estado logo
  após reabrir; nunca concluía de novo para ver se `Duracao` somava. Feche o ciclo.
- **Prazo em helper de teste**: D1/D2 passam pelo ajuste de dia útil — perto de fim de semana o
  vencimento muda. Para vencimento exato, use `TipoPrazo.UmaHora` (nunca é ajustado).
- Mudança de comportamento **renomeia** o teste antigo e inverte a asserção (não deixe nome
  descrevendo o comportamento velho) — ex.: `ReabrirConferencia_VoltaPraAtribuidoSemLigarOCronometro`,
  `EquipeEEtapa_NegacaoEhAbsolutaMesmoComExcecaoPessoalDoMesmoAlvo`.
- Premissa desatualizada: ao escrever teste, confira o comportamento atual no código, não em texto
  antigo de documentação (o teste de importação assumiu "tipo desconhecido só é sinalizado", que
  deixou de valer em [ADR-0012](../decisions/0012-importacao-cadastra-tipo-de-ato-novo.md)).
- Casos parametrizados para tabelas de estado (`PodeRecriar`, 10 casos).
- Fakes (`Fakes.cs`) acompanham toda mudança de assinatura de porta.

## Verificação manual — obrigatória em certos casos

`dotnet run --project src/Dispatch.Api` + curl/psql depois de: caso de uso novo, endpoint novo,
dependência nova de construtor, mudança em `Program.cs` (auth, CORS, health, DI). Para esperar a API
subir, use laço `until curl .../health` (não `sleep` longo — bloqueado no harness). Filtre saída com
`grep -viE "password|senha|token"` para não vazar credencial no transcript. Mudança que toca
autenticação: rode também a suíte e2e do `dispatch-web` (foi ela que pegou o bug do `iat`).

Deriva de dado: o Postgres local acumula dado de sessões; falhas e2e em áreas não tocadas costumam
ser deriva, não regressão — mas confirme.

## Cobertura

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport \
  -reporttypes:"Html;TextSummary" -classfilters:"-Dispatch.Infrastructure.Migrations.*"
```

O `-classfilters` **não é opcional**: migrations são ~18,6k das 24,8k linhas cobríveis e todas contam
como cobertas (o fixture roda `Migrate()`).

| | Com migrations | Sem (número honesto) |
| --- | --- | --- |
| `Dispatch.Infrastructure` | 97,2% | **65,4%** |
| Total (linha) | 92,1% | **71,5%** |

Baseline em 2026-09-22: Domain 96%, Application 96,9%, Infrastructure 65,4%, **Api 43%**, branch
63,4%. Leia `coveragereport/Summary.txt`. Sem threshold/gate (sem CI; `coverlet.collector` não
enforça).

## Contagem de testes (marcos)

10 (primeiro motor) → 103 (Minha fila) → 236 (motor v2) → 305 (TOTP) → 384 (v4 absoluto) → 429
(integração: 127 Domain + 293 Application + 9 Api) → **434** em 2026-09-22 (130 + 294 + 10).

## Referências

- ADR-0036; skills `api-gate`, `verify-integration`, `add-domain-rule`.
- `ef-core.md`, `autorizacao.md`.
