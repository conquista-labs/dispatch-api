---
name: adr-0031-reabrir-devolve-para-atribuido
description: Reabrir conferência devolve o protocolo para Atribuido (mesmo dono, IniciadoEm nulo) em vez de Conferindo com cronômetro ligado — a espera entre aprovação e retomada não conta como tempo
metadata:
  type: decision
  status: accepted
---

# ADR-0031: Reabrir conferência devolve para Atribuído, sem ligar o cronômetro

> `Protocolo.ReabrirConferencia` passa de `Status = Conferindo` + `IniciadoEm = agora` para
> `Status = Atribuido` + `IniciadoEm = null`. O cronômetro só liga quando `IniciarConferencia` roda
> de novo.

## Status

Accepted — 2026-09-16 (commit `240d51a`). Revê a semântica de reabrir definida em ADR-0016
(2026-08-31, leitura literal de RF-24c: "mesmo dono, cronômetro reiniciado").

## Contexto

Uso real (protocolo 263605): o ato "reabriu como em conferência e não na fila da pessoa". E quando a
distribuidora aprova o pedido fora do horário do conferente, esse intervalo entrava no cronômetro
como se fosse tempo de conferência — tempo usado numa conta de bonificação fora do sistema.

## Decisão

Reabrir = voltar para "Atribuídas a você" do mesmo dono. Vale para os dois caminhos
(`ReabrirConferencia` direto e `DecidirPedidoReabertura`), sem mudança neles nem no contrato de API
(o front decide a coluna pelo `Status`).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| `Conferindo` + `IniciadoEm = agora` (ADR-0016) | Literal ao RF-24c | Cronômetro conta sem ninguém trabalhando; card na coluna errada | Relatado em produção |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Fidelidade do tempo medido | ✅ Melhora | Só conta de `Iniciar` até concluir |
| Aderência literal ao requisito | ⚠️ Diverge | RF-24c diz "cronômetro reiniciado" |

## Consequências

**Positivas** — resolveu de graça o caso "aprovado fora do horário". Precisou dos ciclos (ADR-0032)
para não perder o tempo do ciclo anterior.

## Referências

- ADR-0016, ADR-0032, ADR-0034.
- `docs/historico.md`, "Reabrir conferência devolve pra Atribuído, não liga o cronômetro na hora".
