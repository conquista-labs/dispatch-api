---
name: adr-0027-atribuicao-manual-sem-validacao-de-alcada
description: AtribuirManualmente passa a valer em Pool, Excecao e Atribuido (nunca Conferindo) e não valida alçada — é decisão humana deliberada, auditável pelo carimbo de atribuição
metadata:
  type: decision
  status: accepted
---

# ADR-0027: Atribuição manual sem validação de alçada, exceto em conferência

> A distribuidora pode mandar um ato para qualquer conferente cadastrado — protocolo em `Pool`,
> `Excecao` ou já `Atribuido` (redirecionar sem passar pelo pool). Nunca em `Conferindo`. Não passa
> pelo motor nem checa alçada.

## Status

Accepted — 2026-09-15 (commit `51cebe2`). Amplia o RF-17 original (2026-08-27), em que só exceção
podia ser atribuída manualmente.

## Contexto

Pedido do dono: "mandar um ato pra alguém", não só para resolver exceção. RNF-02 exige
auditabilidade de decisão automática; decisão humana deixa `RegraAplicadaId` nulo de propósito.

## Decisão

- Guarda de status: `Status is Pool or Excecao or Atribuido`. `ProtocoloNaoEstaEmExcecao` virou
  `ProtocoloNaoElegivel`.
- **Sem validação de alçada** — confirmado com o dono, igual ao que já acontecia para exceção.
- `Protocolo.AtribuirA` já era transição incondicional; sobrescreve `DonoId`/`AtribuidoEm`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Incluir `Conferindo` | Máxima flexibilidade | Interrompe trabalho em andamento | Fica de fora até (e se) virar pedido explícito |
| Validar alçada na atribuição manual | Impede atribuição "errada" | Tira da distribuidora a decisão humana que o sistema quer registrar, não impedir | Decisão consciente do dono |
| Manter só para exceção | Escopo original do RF-17 | Não atende "mandar um ato pra alguém" | Pedido do dono |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Flexibilidade operacional | ✅ Melhora | Redirecionar sem devolver ao pool |
| Garantia de alçada | ⚠️ Piora | Um ato pode chegar a quem não teria alçada — aceito |

## Consequências

**Riscos** — o documento (seção 11) ainda pergunta "quem pode sobrepor uma regra de alçada numa
urgência"; na prática, hoje é a distribuidora, sem restrição.

## Referências

- `docs/historico.md`, "`AtribuirManualmente` deixa de ser exclusivo de exceção".
