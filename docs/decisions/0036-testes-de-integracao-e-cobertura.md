---
name: adr-0036-testes-de-integracao-e-cobertura
description: Infrastructure/Api ganham um projeto de teste de integração (WebApplicationFactory + Testcontainers + Respawn, login real) e a cobertura passa a ser medida com reportgenerator excluindo migrations, sem threshold
metadata:
  type: decision
  status: accepted
---

# ADR-0036: Testes de integração com Testcontainers e cobertura honesta, sem gate

> `tests/Dispatch.Api.Tests` sobe a API em memória (`WebApplicationFactory<Program>`) contra um
> Postgres efêmero (Testcontainers), com Respawn entre testes e login real via `POST /dev/seed-e2e`.
> Cobertura via `dotnet-reportgenerator-globaltool`, **sempre** excluindo
> `Dispatch.Infrastructure.Migrations.*`; sem threshold.

## Status

Accepted — 2026-09-22 (commit `09110cd`). Convenções adaptadas de `swap-benefits-web` (skills
`testing-strategy`/`gate`, ADR-0005 de lá).

## Contexto

"Sinto falta de um coverage no backend." `coverlet.collector` estava nos `.csproj` de teste desde o
template, nunca usado. Infrastructure e Api nunca tiveram teste automatizado — a verificação era
manual (`dotnet run` + curl/psql), e é justamente onde moram os bugs que fake nenhum pega (FK, CHECK,
change tracker).

## Decisão

- **Um projeto só**: bater no endpoint já exercita Infrastructure por baixo.
- **Testcontainers**, não o Postgres do `docker-compose.yml`; container por execução +
  `Database.Migrate()` do zero no fixture (é, de graça, o teste de fumaça do schema).
- **Um container para a suíte + Respawn** entre testes; `TablesToIgnore`: `configuracao` (linha
  semeada pela migration) e `__EFMigrationsHistory`.
- `public partial class Program;` no fim do `Program.cs` (única mudança de produção por causa de
  teste).
- Primeira leva mira bug de histórico: migrations + `configuracao` semeada; ativar/desativar de
  regra persistindo; XOR de alvo na Api **e** no `CHECK`; guarda de papel RNF-04; importação (linha
  de corte, FKs de tipo e escrevente).
- Cobertura sem gate: `coverlet.collector` não enforça limite (exigiria `coverlet.msbuild`) e não há CI.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Dois projetos (repositórios + Api) | Separação por camada | Duplica a estratégia de banco sem cobrir nada a mais | HTTP já exercita a Infrastructure |
| Postgres do `docker-compose.yml` | Nada a instalar | Acumula dado de sessão (e clone de produção) — flaky por construção | Container efêmero |
| Container por teste | Isolamento total | Subir container é a parte cara | Respawn trunca respeitando FK |
| Bypass de autenticação no teste | Mais rápido | Não exercita hash + JWT | Login real via seed |
| Threshold de cobertura (`coverlet.msbuild`) | Ratchet como no front | Sem CI para avaliar | Número para olhar, não para travar |
| Cobertura incluindo migrations | Nenhum filtro | Infrastructure 97,2% em vez de 65,4%; total 92,1% em vez de 71,5% | Cobertura inflada é pior que nenhuma |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Confiabilidade | ✅ Melhora | Primeira execução achou um 500 real (DateTimeOffset não-UTC) |
| Velocidade da suíte | ⚠️ Piora | Exige Docker e sobe container |

## Consequências

**Riscos** — re-semear (`/dev/seed-e2e`) bumpa `SessoesValidasApartirDe` e invalida tokens emitidos
antes (flake corrigido em `c928ceb`). Ver `docs/patterns/testes.md`.

## Referências

- `docs/patterns/testes.md`; skill `api-gate`.
- `docs/historico.md`, "Cobertura de testes + testes de integração (Infrastructure/Api)".
