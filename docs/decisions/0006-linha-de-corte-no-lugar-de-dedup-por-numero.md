---
name: adr-0006-linha-de-corte-no-lugar-de-dedup-por-numero
description: Duplicata de importação (RF-07) é resolvida por uma linha de corte temporal por lote, não por unicidade do número do protocolo — Numero nunca é único
metadata:
  type: decision
  status: accepted
---

# ADR-0006: Linha de corte substitui dedup por número; `Numero` nunca é único

> Cada importação leva um `linhaDeCorte` (instante); toda linha com `dataHoraAndamento` ≤ corte é
> ignorada, sem olhar o número. `Protocolo.Numero` não tem índice único e nunca deve ganhar um.

## Status

Accepted — 2026-08-27 (commit `6d7b953`). Complementado por ADR-0022 (continuidade de conferência,
que correlaciona linhas do mesmo número).

## Contexto

RF-07 pede "detectar e marcar duplicatas (protocolo já existente)". Descoberta operacional: um
protocolo **reprovado volta a aparecer** em relatórios seguintes com um novo andamento — o mesmo
número, legitimamente, vira uma nova conferência.

## Decisão

Um mecanismo só resolve duplicata acidental (reimportar o mesmo relatório) e reprocessamento
legítimo (reprovado voltando): a linha de corte. `Numero` fica sem unicidade; recebeu depois um
**índice não único** para as buscas por número (ver `docs/patterns/ef-core.md`). A regra de
"número já em uso" existe só no cadastro manual, como regra de aplicação (hoje via
`ResolvedorDeContinuidade.PodeRecriar`, ADR-0022).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Dedup por número (índice único em `numero`) | Literal ao RF-07 | Bloqueia o reprocessamento legítimo de protocolo reprovado | Contradiz a operação real |
| Dedup por (número, andamento) | Distingue reprocessamento | Continua exigindo comparar linha a linha com o histórico | A linha de corte resolve os dois casos sem consulta por linha |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Histórico preservado (2 linhas para 1 número, de propósito) |
| Usabilidade | ⚠️ Atenção | Quem importa precisa escolher o corte certo (RF-05b) |

## Consequências

**Negativas** — "o mesmo protocolo" deixa de ser uma linha; qualquer visão por número precisa
agrupar (painel de histórico, continuidade).

## Referências

- ADR-0022 (continuidade).
- `docs/historico.md`, "Importação de lote (RF-05 a RF-12)".
