---
name: adr-0033-pausar-conferencia
description: Pausar mantém o protocolo em Conferindo (conta no limite de simultâneos), fecha o ciclo corrente para o tempo pausado não contar, e registra cada pausa — visibilidade, não bloqueio
metadata:
  type: decision
  status: accepted
---

# ADR-0033: Pausar conferência — continua em Conferindo; pausa visível, não limitada

> `Protocolo.Pausar(agora)` fecha o ciclo corrente em `CiclosAnteriores` e zera `IniciadoEm` **sem
> mudar `Status`**; `Retomar(agora)` abre ciclo novo e registra a pausa encerrada
> (`PausaConferencia`). Concluir exige retomar antes. Sem limite de número/duração de pausas.

## Status

Accepted — 2026-09-17 (commits `9314641` e `063dbfd`). Pedido do dono; **não é RF numerado** nem está
no protótipo/requisito (busca por "pausa" no documento não acha nada).

## Contexto

"A pessoa sai pra almoçar, por exemplo." O tempo pausado não pode contar (mesma disciplina da
reabertura). Antes de aplicar em produção o dono perguntou: "como garantir que ninguém abusa da pausa
pra melhorar o tempo dela?" — o tempo alimenta bonificação.

## Decisão

- Pausado continua `Conferindo` — ocupa o limite de simultâneos (RF-21), confirmado com o dono.
  `ObterEmConferenciaPorConferenteAsync` filtra só por `Status`, então conta sozinho.
- `ConcluirConferencia` rejeita com `EstaPausado` (409): concluir com `IniciadoEm` nulo gravaria
  `ConcluidoEm` sem ciclo aberto e `Duracao` voltaria nula.
- `PausaConferencia` (`PausadoEm`/`RetomadoEm`, sem `ConferenteId` — pausa é sempre do dono atual),
  tabela `pausas_conferencia`. `GET /protocolos/{id}/detalhe` expõe `PausadoEm` e `Pausas`.
- `POST /minha-fila/{id}/pausar` e `/retomar`; `ProtocoloResumo.PausadoEm`.
- **Visibilidade, não bloqueio** — decisão do dono: time pequeno, confiança resolve, mas o dado não
  pode ficar invisível.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Pausar devolve o ato à fila (Atribuído) | Libera o limite | Mistura pausa com reabertura; outro ato pode ser iniciado no lugar | Confirmado: pausado continua ocupando o limite |
| Limitar pausas (quantidade/duração) | Impede abuso | Atrito para um time pequeno de confiança | Decisão do dono: visibilidade |
| Pausa sem registro | Mais simples | Intervalo some sem rastro — abuso invisível | Pergunta do dono antes de ir para produção |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Fidelidade do tempo | ✅ Melhora | Pausa nunca entra em `Duracao` |
| Auditabilidade | ✅ Melhora | Cada pausa com início/fim |

## Referências

- ADR-0032 (ciclos).
- `docs/historico.md`, "Pausar conferência — \"a pessoa sai pra almoçar, por exemplo\"",
  "Visibilidade das pausas".
