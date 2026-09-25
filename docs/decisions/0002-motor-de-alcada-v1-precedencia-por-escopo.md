---
name: adr-0002-motor-de-alcada-v1-precedencia-por-escopo
description: Primeira versão do resolvedor de alçada — regra por pessoa substitui a de nível no mesmo alvo, negação vence permissão dentro do escopo, ausência de regra é permitido
metadata:
  type: decision
  status: superseded
---

# ADR-0002: Motor de alçada v1 — precedência por escopo e padrão aberto

> `ResolvedorAlcada` implementa a precedência da seção 4 do documento de requisitos ao pé da
> letra: regra por pessoa, quando existe para aquele alvo, substitui a de nível; dentro do mesmo
> escopo, negação vence permissão; ausência de regra aplicável = permitido.

## Status

Superseded by [ADR-0017](0017-motor-de-alcada-v2-lista-fechada-por-dimensao.md) — a semântica de
"padrão aberto" fez regras `Permite` não terem efeito nenhum em produção.

Aceito em 2026-08-26 (commit `bd4d305`, primeiro corte do motor, 10 testes).

## Contexto

A seção 4 do documento de requisitos chama a precedência de regras de "a parte mais sujeita a
interpretação errada" e dá um exemplo resolvido: Júnior com regra de nível "não pode fazer
pré-conferência" e regra pessoal "pode fazer pré e pós" **pode** fazer pré-conferência. O mesmo
documento registra uma divergência conhecida: o protótipo avaliava pessoa e nível no mesmo
conjunto e aplicava a negação sempre.

## Decisão

Vamos implementar a precedência do **documento**, não a do protótipo:

- `SujeitoAlcada` (Nivel | Pessoa) e `AlvoAlcada` (Etapa | TipoAto) como hierarquias fechadas
  (record abstrato com construtor privado + tipos aninhados, emulando sum type).
- `ResolvedorAlcada`: pessoa > nível **por alvo**; negação > permissão dentro do escopo; sem regra
  aplicável = permitido.
- `MotorDistribuicao` (5 passos da seção 4) devolve `ResultadoDistribuicao` (Atribuido /
  EnviadoParaPool / Excecao) carregando a regra aplicada por candidato (RNF-02).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Comportamento do protótipo (pessoa e nível num conjunto só, negação sempre vence) | Igual ao que a distribuidora via no protótipo | Contradiz o exemplo resolvido do documento; regra pessoal nunca conseguiria abrir exceção | O documento declara explicitamente que a precedência por escopo é o comportamento a implementar |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Auditabilidade | ✅ Melhora | Resultado carrega a regra aplicada por candidato |
| Correção percebida | ⚠️ Piora (descoberto depois) | "Permite" só importava sobrepondo uma negação — a distribuidora esperava lista fechada |

## Consequências

**Positivas** — precedência testada com os casos de borda do documento; a skill `add-domain-rule`
nasceu daqui.

**Negativas** — em produção, regras `Permite` criadas pela distribuidora não tinham efeito (padrão
aberto de verdade). Motivou a v2.

## Referências

- Substituído por [ADR-0017](0017-motor-de-alcada-v2-lista-fechada-por-dimensao.md).
- `docs/patterns/motor-e-prazos.md` — algoritmo **atual**.
- `docs/historico.md`, "Estado atual (scaffold, motor, prazos, primeira persistência)".
