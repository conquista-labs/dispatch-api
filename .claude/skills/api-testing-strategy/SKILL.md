---
name: api-testing-strategy
description: Decide que teste uma mudança do dispatch-api precisa — unidade no Domain, caso de uso com fakes na Application, ou integração HTTP com Postgres real (Testcontainers) — e onde o arquivo vive. Use ao terminar uma mudança em src/, ao revisar código sem teste, ou quando o usuário perguntar "isso precisa de teste?", "que teste eu escrevo pra isso?".
---

# api-testing-strategy

Par da `testing-strategy` do dispatch-web. Aqui é onde mora a regra de negócio do sistema inteiro
(motor, alçada, prazo, score), então é aqui que a maior parte do valor de teste está.

Leia antes: `docs/patterns/` sobre testes e o ADR de estratégia de testes em `docs/decisions/`, se
existirem. Releia agora — não confie num resumo de antes nesta conversa.

## Entrada

O arquivo/feature/diff em questão. Vazio: `git diff` + `git diff --cached` e trabalhe sobre o que
mudou.

## As três camadas de teste

| Projeto | O que prova | Custo |
|---|---|---|
| `tests/Dispatch.Domain.Tests` | Regra pura: motor, `ResolvedorAlcada`, prazo, continuidade, transições de `Protocolo` | Milissegundos, sem nada |
| `tests/Dispatch.Application.Tests` | Orquestração do caso de uso, com fakes em memória (`Fakes.cs`) | Rápido, sem banco |
| `tests/Dispatch.Api.Tests` | O que fake não tem: pipeline HTTP, `RequireRole`, EF Core (change tracker, `ValueConverter`, `CHECK`, FK, owned types), migrations, Npgsql | Lento — sobe Postgres via Testcontainers, exige Docker |

## Passo 1 — onde a mudança mora

1. **Regra de domínio nova ou alterada** (Domain) → teste no Domain **primeiro** (skill
   `add-domain-rule`), incluindo os casos de precedência e borda. Toda regra com histórico de erro
   (alçada foi reescrita 3×) ganha teste do caso que quebrou.
2. **Caso de uso novo** (Application) → um teste por desfecho do `ResultadoX` (`Sucesso`,
   `NaoEncontrado`, `JaExiste`...), com fakes. Se o fake precisou de método novo, implemente-o em
   `Fakes.cs` com a mesma semântica do repositório real (filtro, ordenação) — fake que devolve
   tudo esconde bug de filtro.
3. **Endpoint novo ou regra de autorização** → integração em `Dispatch.Api.Tests`: o papel que não
   pode leva 403, o par que pode passa. Modelo: `AutorizacaoIntegracaoTests.cs`.
4. **Persistência nova** (entidade, coluna, owned collection, conversor, repositório que muta) →
   integração que **grava, relê numa query nova e confere** — é o único jeito de pegar o change
   tracker desconectado (aconteceu com `RegraAlcada`, `Sugestao`, `Configuracao`) e o round-trip
   de conversor (o `Prazo` com corte de horário quase perdeu o horário ao recarregar).
   Modelo: `CorteDeHorarioIntegracaoTests.cs`.
5. **Bug que já aconteceu** → teste de regressão no mesmo commit, na camada mais baixa que o
   reproduz. Se o bug só existe com Postgres de verdade (offset não-UTC no Npgsql, FK, `CHECK`),
   a camada é a de integração — não force um fake a simular.

## Passo 2 — o que não precisa de teste

- DTO/record sem lógica, mapeamento `Protocolo → ProtocoloResumo` campo a campo (passagem direta).
- Migration gerada — ela é exercitada de graça pelo `Database.Migrate()` do fixture (`SchemaTests`).
- Endpoint que só repassa pra um caso de uso já testado, **exceto** pela autorização (passo 1.3).

## Passo 3 — convenções

- Nome do teste: `Cenario_ResultadoEsperado` em português (`Pool_OrdenaPorVencimentoAscendente`).
- Relógio: `FakeRelogio` com instante fixo; prazo em teste de borda usa `TipoPrazo.UmaHora` (o
  único que não empurra pra dia útil — D0/D1/D2 mudam de dia perto do fim de semana).
- Integração: isolada por Respawn entre testes; login pelo `POST /dev/seed-e2e` (conta seed), sem
  bypass de token.
- Depois de mudar teste/código: `dotnet test --filter <Classe>` durante o trabalho; a suíte inteira
  fica pro `api-gate` no fim.

## Reporte

Que teste escreveu (ou por que nenhum), em qual camada e por quê, e o que ficou sem cobertura
consciente.
