---
name: adr-0007-vencimento-a-partir-do-andamento
description: O vencimento de um protocolo é calculado a partir de Protocolo.AndamentoEm (quando o ato entrou), nunca do instante em que o sistema processou ou editou o protocolo
metadata:
  type: decision
  status: accepted
---

# ADR-0007: Vencimento calculado a partir do `AndamentoEm`, não de "agora"

> `Prazo.CalcularVencimento` usa `Protocolo.AndamentoEm` como momento de referência. Importação,
> recálculo por mudança de prazo da equipe (RF-38) e edição manual (RF-18g) recalculam sempre a
> partir desse instante original — nunca de `IRelogio.Agora`.

## Status

Accepted — 2026-08-27 (commit `6d7b953`). Exceção deliberada registrada em ADR-0034 (reabertura
recalcula a partir de agora).

## Contexto

O primeiro corte usava `IRelogio.Agora` como referência. Para lote isso é errado: um protocolo
criado às 9h e importado às 14h não pode ter o prazo contado a partir das 14h. O documento também
diz (RF-18g) que o novo vencimento é recalculado "a partir da origem do ato, nunca do instante da
edição".

## Decisão

- `Protocolo.AndamentoEm` (obrigatório, `{ get; }` imutável) guarda o `dataHoraAndamento` do
  relatório e é o `momentoDeReferencia` do prazo.
- Recálculos (`RecalculoDeVencimentos`/RF-38, `EditarProtocoloManual`/RF-18g, `AplicarSugestao`)
  chamam `protocolo.DefinirPrazo(prazoNovo, protocolo.AndamentoEm)`.
- Cadastro manual aceita `andamentoEm` opcional ("hora de entrada", 2026-09-14); sem valor, usa
  `IRelogio.Agora`. O `agora` de `AtribuirA` continua sendo o instante real da chamada (RNF-16).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| `IRelogio.Agora` como referência (comportamento original) | Não depende de dado de entrada | Prazo contado do processamento, não da chegada do ato; errado para lote | Registrado como "comportamento antigo e errado pra lote" |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Semáforo reflete a chegada real do ato |
| Rastreabilidade | ✅ Melhora | `AndamentoEm` nunca muda; é o histórico de entrada |

## Consequências

**Negativas** — protocolo reaberto dias depois aparecia "vencido há Xd" na hora; motivou a exceção
do ADR-0034.

## Referências

- ADR-0034 (exceção na reabertura), ADR-0013 (regras de prazo), ADR-0037 (corte de horário usa o
  mesmo `AndamentoEm` para decidir o corte).
- `docs/patterns/motor-e-prazos.md`.
- `docs/historico.md`, "Importação de lote (RF-05 a RF-12)", "Protocolo.EscreventeId — fecha RF-14 e RF-38".
