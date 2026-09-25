---
name: adr-0013-prazos-em-horas-corridas-com-dia-util
description: D+1 e D+2 são 24h e 48h corridas a partir da referência, D+0 vence no fim do dia; os três empurram para o próximo dia útil (sem feriados), e "1 hora" nunca é empurrado
metadata:
  type: decision
  status: accepted
---

# ADR-0013: Prazos D+1/D+2 em horas corridas, com ajuste de dia útil

> Regra confirmada com a operação: D+0 vence no fim do dia atual; **D+1 = 24h corridas, D+2 = 48h**
> a partir da referência. Se o vencimento cai em sábado/domingo, empurra para o próximo dia útil no
> mesmo horário (sem calendário de feriado). "1 hora" fica fora desse ajuste.

## Status

Accepted — 2026-08-28 (commit `20b28be`). Substitui a primeira modelagem (2026-08-26, commit
`a638188`), que tratava D+1/D+2 como "fim do dia seguinte"/"fim de dois dias depois". Estendido por
ADR-0037 (corte de horário), que reaproveita o mesmo `ProximoDiaUtil`.

## Contexto

A seção 11 do documento listava "os nomes e prazos reais de cada equipe" como ponto a confirmar com
a operação. A primeira versão modelou D+1/D+2 como fim de dia.

## Decisão

- `Prazo`/`TipoPrazo` (`UmaHora`, `D0`, `D1`, `D2`): D+0 segue modelado como "início do dia
  seguinte" (mesmo instante do fim do dia, cálculo mais simples); D+1/D+2 somam 24h/48h.
- `ProximoDiaUtil` empurra D0/D1/D2 que caem no fim de semana para segunda, mesmo horário.
- `UmaHora` nunca é empurrado: é o prazo mais urgente (RF-13); jogá-lo para depois do fim de
  semana contradiz o motivo de existir.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| D+1/D+2 = fim do dia seguinte / de dois dias depois (primeira versão) | Leitura intuitiva de "D+N" | Não é como a operação conta | Confirmado com a operação que são horas corridas |
| Calendário de feriados | Vencimento exato | Dado a manter por cartório/cidade | Fora do escopo por ora — só fim de semana |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Bate com a prática da operação |
| Testabilidade | ⚠️ Atenção | Testes com D1/D2 perto de fim de semana mudam de resultado — helpers de teste usam `UmaHora` para vencimento exato (ver `docs/patterns/testes.md`) |

## Consequências

**Negativas** — feriados não são considerados (ver `docs/gaps-requisitos.md`).

## Referências

- `docs/patterns/motor-e-prazos.md`.
- `docs/historico.md`, "Estado atual (scaffold, motor, prazos, primeira persistência)".
