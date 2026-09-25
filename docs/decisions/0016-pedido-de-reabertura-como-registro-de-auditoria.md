---
name: adr-0016-pedido-de-reabertura-como-registro-de-auditoria
description: Correção de resultado e pedido de reabertura (RF-24a-d) modelados com uma entidade própria PedidoReabertura que é, ela mesma, a linha de auditoria da decisão — sem log genérico
metadata:
  type: decision
  status: accepted
---

# ADR-0016: `PedidoReabertura` como entidade própria de auditoria

> `PedidoReabertura` (`Id`, `ProtocoloId`, `SolicitanteId`, `CriadoEm`, `Status`
> Pendente/Aprovado/Negado/Cancelado, `DecididoPorId`, `DecididoEm`) é o primeiro registro de
> auditoria de verdade do sistema. Correção de resultado grava `CorrigidoEm` no protocolo, com
> janela configurável.

## Status

Accepted — 2026-08-31 (commit `d777941`). A semântica de *reabrir* definida aqui
(`Status = Conferindo`, `IniciadoEm = agora`) foi revista por ADR-0031 (volta para Atribuído) e
ADR-0034 (recalcula vencimento); a entidade de pedido segue igual.

## Contexto

RF-24a-d: janela de correção (padrão 15 min), pedido de reabertura cancelável, decisão da
distribuidora, correção fora da janela só por reabertura. A seção 8 pede "toda transição gera
registro de auditoria com autor e horário", mas o projeto decidiu não ter log genérico (ADR-0009).

## Decisão

- `Protocolo` ganha `CorrigidoEm`/`ReabertoEm` e os métodos `CorrigirResultado(agora)` (inverte
  Aprovado↔Reprovado; pode corrigir mais de uma vez dentro da janela — nada proíbe) e
  `ReabrirConferencia(agora)`.
- `PedidoReabertura` com mapeamento direto; FK `Restrict` para o solicitante e `Cascade` para o
  protocolo.
- Casos de uso com `abstract record ResultadoX` fechado: `CorrigirResultado` (só o dono, só
  concluído, só dentro de `JanelaDeCorrecao`), `PedirReabertura` (só o dono, um pendente por vez, não
  checa a janela de propósito), `CancelarPedidoReabertura`, `DecidirPedidoReabertura`,
  `ReabrirConferencia` (ação direta), `ListarPedidosReaberturaPendentes`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Tabela genérica de eventos/transições | Cobre toda transição de uma vez | Infraestrutura que o projeto já tinha decidido não ter (ADR-0009) | O próprio pedido já é a linha de auditoria da decisão |
| Só campos no protocolo (sem entidade de pedido) | Menos tabelas | Não guarda quem pediu, quem decidiu nem o ciclo pendente/cancelado | RF-24b/c exigem o ciclo de vida do pedido |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Auditabilidade | ✅ Melhora | Autor e horário do pedido e da decisão persistidos |
| Consistência | ⚠️ Atenção | Aprovar pedido precisa checar o status atual do protocolo (bug achado na auditoria de 2026-09-03) |

## Consequências

**Riscos** — um pedido velho aprovado reabria um protocolo excluído; corrigido com
`ResultadoDecidirPedidoReabertura.StatusInvalido` (409).

## Referências

- ADR-0031, ADR-0032, ADR-0034 (evolução da reabertura).
- `docs/historico.md`, "Correção de resultado + pedido de reabertura (RF-24a-d)".
