---
name: adr-0032-tempo-de-conferencia-por-ciclo
description: Cada ciclo de conferência encerrado vira um CicloConferencia (conferente, início, fim) numa coleção owned do protocolo, e o Dashboard atribui tempo por ciclo a quem o fez — substitui o acumulador cego TempoAcumuladoAnterior
metadata:
  type: decision
  status: accepted
---

# ADR-0032: Tempo de conferência registrado por ciclo (`CicloConferencia`)

> `Protocolo.CiclosAnteriores : IReadOnlyList<CicloConferencia>` (tabela `ciclos_conferencia`,
> `OwnsMany`). Reabrir fecha o ciclo corrente com o dono de agora. `Duracao` = soma de todos os
> ciclos; o Dashboard atribui o tempo de cada ciclo a quem o fez. Reabertura com dono fora da escala
> vai para o pool.

## Status

Accepted — 2026-09-17 (commit `ea4069a`). Substitui o `TempoAcumuladoAnterior : TimeSpan` introduzido
em 2026-09-16 (commit `e883c42`) — que por sua vez corrigiu `Duracao` perdendo o ciclo anterior.

## Contexto

`Duracao` era `ConcluidoEm − IniciadoEm`; reabrir sobrescrevia os dois e o dado do primeiro ciclo
deixava de existir (protocolo real nº 264137). O primeiro fix somava os ciclos num `TimeSpan` — certo
para `Duracao`, mas cego: se o protocolo reabria e ia para outra pessoa, o Dashboard jogava tudo na
conta do dono atual. O tempo alimenta uma conta de bonificação **fora do sistema** (RF-46 não tem
parcela de tempo); o dono: "não podemos perder nada desses dados".

## Decisão

- `CicloConferencia` (Domain): `ConferenteId`/`IniciadoEm`/`ConcluidoEm` (+`Duracao`). Sem identidade
  fora do protocolo (shadow `Id`), índice em `conferente_id`.
- Migration `AdicionaCiclosDeConferencia` com **backfill antes de dropar** `tempo_acumulado_anterior`:
  sintetiza um ciclo por protocolo atribuído ao dono atual (`COALESCE(iniciado_em, reaberto_em) −
  tempo_acumulado_anterior` até `COALESCE(iniciado_em, reaberto_em)`); conferido em produção antes
  (1 protocolo afetado). `Down()` simétrico.
- `ObterDashboard.ConstruirTemposPorConferente`: `(conferenteId, duração)` por ciclo + ciclo final.
  `TempoMedio` por pessoa vem dos próprios ciclos; KPIs agregados e "por tipo de ato" continuam
  somando o protocolo inteiro ("quanto o ato leva", não "quanto a pessoa trabalhou"). Quem fez um
  ciclo mas não é dono de nada aparece com `Volume: 0`.
- RF-27 na reabertura: `ReabrirConferencia`/`DecidirPedidoReabertura` mandam para o pool se o dono
  não está mais `NaEscala` (ou não existe).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| `TempoAcumuladoAnterior : TimeSpan` (fix de 2026-09-16) | Uma coluna; `Duracao` certa | Não sabe de quem é cada ciclo; pessoa nova herda tempo da antiga | Peso financeiro do dado |
| Não fazer backfill (dropar a coluna) | Migration simples | Perde o tempo já acumulado | "Não podemos perder nada" |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Fidelidade do dado | ✅ Melhora | Tempo atribuído a quem trabalhou |
| Complexidade | ⚠️ Piora | Primeira coleção-filha do projeto; o Dashboard achata ciclos |

## Consequências

**Negativas** — protocolos reabertos antes de 2026-09-16 perderam o primeiro ciclo para sempre;
o backfill é aproximação (horários reais de ciclos passados não existem).

## Referências

- ADR-0031, ADR-0033 (pausas reaproveitam o mesmo fechamento de ciclo), ADR-0035 (ajuste manual
  sobrepõe o cálculo por ciclo).
- `docs/patterns/ef-core.md` (`OwnsMany` e backing field).
- `docs/historico.md`, "Bug real: Duracao de um protocolo reaberto perdia o tempo do ciclo anterior",
  "`TempoAcumuladoAnterior` vira `CiclosAnteriores`".
