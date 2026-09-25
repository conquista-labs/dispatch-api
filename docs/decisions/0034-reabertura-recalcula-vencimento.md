---
name: adr-0034-reabertura-recalcula-vencimento
description: Ao reabrir uma conferência, o vencimento é recalculado com o mesmo TipoPrazo a partir de agora — exceção deliberada ao ADR-0007; AndamentoEm não muda
metadata:
  type: decision
  status: accepted
---

# ADR-0034: Reabertura recalcula o vencimento a partir de agora

> `Protocolo.ReabrirConferencia` termina com `if (Prazo is { } prazoAtual) DefinirPrazo(prazoAtual,
> agora)`. Mesmo `TipoPrazo`, nova referência. `AndamentoEm` continua o histórico de entrada.

## Status

Accepted — 2026-09-21 (commit `2725d5d`). Exceção deliberada ao [ADR-0007](0007-vencimento-a-partir-do-andamento.md).

## Contexto

Uso real: protocolo reaberto dias depois continuava com vencimento da entrada original e aparecia
"vencido há Xd" na hora, mesmo sendo uma conferência nova pedida agora. Exemplo confirmado com o dono:
entrada 10/09, D+1, reaberto 15/09 → vencimento deve virar 16/09, não continuar 11/09.

## Decisão

Recalcular na própria transição de domínio (os dois caminhos que reabrem ganham de graça). Guarda
defensiva: sem `Prazo` definido, não inventa vencimento. Não confundir com `Duracao` (tempo
trabalhado, ciclos — ADR-0032), que continua somando normalmente.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter vencimento da entrada original (ADR-0007) | Regra única para todo recálculo | Reaberto nasce estourado; semáforo mente sobre a urgência real | Relatado em produção e confirmado com exemplo |
| Mudar `AndamentoEm` para a data da reabertura | Reaproveita o cálculo sem exceção | Perde o histórico de entrada | `AndamentoEm` é imutável de propósito |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção percebida | ✅ Melhora | Semáforo reflete a conferência nova |
| Uniformidade | ⚠️ Piora | Uma exceção à regra do ADR-0007 |

## Consequências

**Riscos** — com corte de horário (ADR-0037), o `Prazo` recarregado precisa preservar
`HorarioDeVencimento` no round-trip; coberto por teste de integração.

## Referências

- ADR-0007, ADR-0037.
- `docs/historico.md`, "Reabertura recalcula o vencimento a partir de agora".
