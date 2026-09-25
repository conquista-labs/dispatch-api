---
name: adr-NNNN-titulo-curto
description: Uma frase objetiva descrevendo a decisão registrada
metadata:
  type: decision
  status: proposed
---

<!-- Todo o docs/ deste repositório é em português. Identificadores de código ficam como estão
no código (`ResolvedorAlcada`, `IProtocoloRepository`...). Use a skill /api-adr pra criar um ADR
novo — ela cuida da numeração e das regras abaixo. -->

# ADR-NNNN: Título em frase nominal curta

> Resuma a decisão em 1-2 frases. Quem ler só o índice deve entender o que foi decidido sem
> abrir o arquivo.

## Status

`Proposed` | `Accepted — AAAA-MM-DD` | `Superseded by ADR-00XX` | `Deprecated` | `Rejected`

Um ADR aceito não é editado para mudar de ideia depois. Se a decisão for revertida ou
substituída, crie um novo ADR e volte aqui **só** para atualizar este campo (e, se quiser, uma
linha dizendo o que mudou e onde).

## Contexto

Que força técnica, de negócio ou do próprio projeto pressiona essa decisão? Que restrições existem
(free tier, dado já gravado em produção, requisito ambíguo, protótipo divergente do documento)?
Descreva o problema antes da solução — quem ler daqui a um ano precisa entender por que isso virou
uma decisão, e não apenas "a forma que sempre foi feito". Cite o RF/RNF do documento de requisitos
(`../dispatch-prototype/Dispatch - Requisitos.dc.html`) quando houver.

## Decisão

Frase declarativa, em voz ativa: "Vamos usar X para resolver Y porque Z." Depois, os detalhes
concretos que fazem parte da decisão (arquivos, tipos, endpoints, migration).

## Alternativas consideradas

Toda decisão de arquitetura é um trade-off. Liste **só as alternativas que foram de fato
consideradas** (na conversa com o dono, no código anterior, no protótipo) e por que foram
descartadas — é essa parte que impede o mesmo debate de se repetir. Se não houve alternativa real,
provavelmente isso não é um ADR: é um pattern (`docs/patterns/`) ou uma entrada do histórico
(`docs/historico.md`).

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| A           | ...  | ...     | ...                    |
| B           | ...  | ...     | ...                    |

## Characteristics impactadas (-ilities)

| Characteristic       | Impacto                           | Justificativa |
| -------------------- | --------------------------------- | ------------- |
| ex: Testabilidade    | ✅ Melhora / ⚠️ Piora / ➖ Neutro | ...           |
| ex: Auditabilidade   | ...                               | ...           |

## Consequências

**Positivas** — o que fica mais fácil a partir de agora.

**Negativas** — o que fica mais difícil, ou débito assumido conscientemente.

**Riscos** — o que pode dar errado e como isso seria percebido (teste, uso em produção, revisão).

## Referências

Commit(s), seção de `docs/historico.md`, patterns relacionados (`docs/patterns/...`), ADRs que este
substitui ou complementa, itens de `docs/gaps-requisitos.md`.
