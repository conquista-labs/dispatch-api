---
name: adr-0009-aprendizado-sem-tabela-evento-decisao
description: O módulo de aprendizado (RF-39 a RF-41) deriva as propostas de dados que já existem, sem a tabela genérica evento_decisao sugerida pelo documento
metadata:
  type: decision
  status: accepted
---

# ADR-0009: Aprendizado sem tabela `evento_decisao`

> Das quatro propostas da seção 7, três são funções puras de dados já existentes (protocolo,
> escrevente, conferente, equipe). Só "Tipo desconhecido" precisava de um dado novo —
> `Protocolo.TipoAtoNomeOriginal`. Não existe log de eventos genérico.

## Status

Accepted — 2026-08-27 (commit `ee902fd`), decidido com o dono antes de codificar.

## Contexto

A seção 7 diz "o sistema que aprende é contagem, não modelo" e a seção 8 sugere uma tabela
`evento_decisao(id, protocolo_id, previsto, realizado, usuario_id, em)` gravada toda vez que um
humano contraria o motor, com um job diário agregando.

## Decisão

- `Dispatch.Domain/Aprendizado/`: `PayloadSugestao` (hierarquia fechada — `TipoDesconhecido`,
  `PrazoIrreal`, `EscreventeOrfao`, `RiscoQualidade`), `Sugestao` (`Pendente`/`Aplicada`/
  `Descartada`, `Chave` para dedup, `DescartarAte` para descarte com memória) e
  `GeradorDeSugestoes` (quatro funções puras, limiares como parâmetro).
- `Protocolo.TipoAtoNomeOriginal` guarda o texto bruto quando `TipoAtoId` é nulo.
- O "job diário" roda sob demanda (`POST /sugestoes/gerar`) — não há scheduler (ver gaps).
- Se aparecer uma proposta que dependa mesmo de "previsto vs. realizado" solto, a tabela nasce ali.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Tabela `evento_decisao` como no documento | Fiel ao modelo sugerido; base genérica para propostas futuras | Infraestrutura para um caso de uso que não existe; dado duplicado do que protocolo já guarda | Três das quatro propostas já saem dos dados atuais |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Simplicidade | ✅ Melhora | Nenhuma escrita extra em cada decisão |
| Auditabilidade | ⚠️ Piora | Sem trilha genérica de transições; auditoria nasce caso a caso (`PedidoReabertura`, ciclos, pausas, ajustes, `EventoAutenticacao`) |

## Consequências

**Negativas** — "toda transição gera registro de auditoria com autor e horário" (seção 8) não tem
infraestrutura genérica; `AtribuidoEm` guarda só a última atribuição. Ver `docs/gaps-requisitos.md`.

## Referências

- `docs/historico.md`, "Aprendizado sem IA (RF-39 a RF-41)", "Índice de confiança real da sugestão".
- ADR-0016 (primeiro registro de auditoria dedicado).
