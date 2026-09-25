---
name: adr-0008-tipo-desconhecido-sinalizado-nao-cadastrado
description: Tipo de ato desconhecido na importação é sinalizado (TipoAtoId nulo, exceção) e nunca cadastrado automaticamente no catálogo
metadata:
  type: decision
  status: superseded
---

# ADR-0008: Tipo de ato desconhecido é sinalizado, nunca cadastrado sozinho

> Na importação, um tipo de ato fora do catálogo deixa `Protocolo.TipoAtoId` nulo e vira exceção
> "tipo desconhecido"; o catálogo só cresce por decisão humana (sugestão de aprendizado revisada).

## Status

Superseded by [ADR-0012](0012-importacao-cadastra-tipo-de-ato-novo.md) — um sistema novo travava
na primeira importação.

Aceito em 2026-08-27 (commit `6d7b953`).

## Contexto

RF-09 pede "sinalizar tipos desconhecidos". A seção 7 (aprendizado sem IA) trata evolução de
catálogo como proposta revisada por humano ("Tipo desconhecido: ≥5 ocorrências → moda do nível").

## Decisão

`Protocolo.TipoAtoId` vira `Guid?`; tipo desconhecido é sinalizado no resumo e o motor já o tratava
como exceção. Nada é criado no catálogo pela importação. (Escrevente desconhecido, ao contrário, é
criado sem equipe — confirmado com o dono.)

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Cadastrar o tipo automaticamente na importação | Nada trava por falta de catálogo | Catálogo cresce sem revisão humana | Na época, lia-se a seção 7 como "evolução de catálogo só por proposta revisada" |

## Consequências

**Negativas** — com catálogo vazio, **nada** fluía pro pool até 5 ocorrências gerarem sugestão
(RF-40). Foi o que levou à reversão.

## Referências

- Substituído por [ADR-0012](0012-importacao-cadastra-tipo-de-ato-novo.md).
- `docs/historico.md`, "Importação de lote (RF-05 a RF-12)".
