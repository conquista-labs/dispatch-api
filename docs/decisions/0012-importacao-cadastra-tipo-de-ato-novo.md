---
name: adr-0012-importacao-cadastra-tipo-de-ato-novo
description: ImportarLote cadastra no catálogo, com nome normalizado, o tipo de ato que ainda não existe — substitui ADR-0008
metadata:
  type: decision
  status: accepted
---

# ADR-0012: Importação cadastra tipo de ato novo automaticamente

> Tipo de ato desconhecido no relatório é criado no catálogo na hora da importação, com nome
> normalizado (`NormalizadorDeTexto`). Alçada continua exigindo regra explícita quando negada — só
> o cadastro do tipo deixou de travar.

## Status

Accepted — 2026-08-28 (commit `e6c36da`). Substitui [ADR-0008](0008-tipo-desconhecido-sinalizado-nao-cadastrado.md).

Reconstituído do commit (o CLAUDE.md original não tinha seção própria para isso; só era citado em
"Cobertura de testes + testes de integração": "desde 'Cadastro automático de tipo de ato na
importação', é **criar** o tipo normalizado").

## Contexto

Com o ADR-0008, um sistema novo travava logo na primeira importação: todo tipo virava exceção e só
entrava no catálogo via sugestão de aprendizado (RF-40), que exige ≥5 ocorrências — com catálogo
vazio, nada fluía pro pool.

## Decisão

- `ImportarLote` cadastra o tipo novo direto no catálogo, nome normalizado.
- `NormalizadorDeTexto` (Domain): nomes de escrevente e de tipo saem do relatório em CAIXA ALTA e
  são gravados com capitalização normal (conectivos "de"/"da"/"e" minúsculos, exceto na primeira
  palavra).
- `POST /tipos-ato` (`CriarTipoAto`) — cadastro manual, para registrar um tipo antes de aparecer
  num relatório (409 se o nome já existe).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter ADR-0008 (sinalizar, esperar sugestão) | Catálogo só cresce com revisão humana | Sistema novo não distribui nada até 5 ocorrências por tipo | Bloqueio operacional imediato |
| Só cadastro manual prévio | Controle total | Exige cadastrar ~24-39 tipos antes do primeiro uso | Atrito desnecessário; o manual continua existindo como complemento |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Operabilidade | ✅ Melhora | Importação nunca trava por catálogo |
| Qualidade do catálogo | ⚠️ Piora | O mesmo ato escrito de formas diferentes vira dois tipos — exige mesclar (RF-34c, ainda não feito) |

## Consequências

**Negativas** — a sugestão "Tipo desconhecido" perde a maior parte da utilidade para relatórios
com nome legível; RF-34c (mesclar) fica mais importante. Ver `docs/gaps-requisitos.md`.

## Referências

- Substitui [ADR-0008](0008-tipo-desconhecido-sinalizado-nao-cadastrado.md).
- `docs/historico.md`, "Cadastro automático de tipo de ato na importação + normalização de texto".
