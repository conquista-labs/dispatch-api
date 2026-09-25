---
name: adr-0011-carga-atual-calculada-na-leitura
description: Conferente.CargaAtual é sempre recalculada na leitura por subquery (protocolos Atribuido/Conferindo do dono), nunca gravada a cada atribuição
metadata:
  type: decision
  status: accepted
---

# ADR-0011: Carga atual calculada na leitura, nunca persistida

> `ConferenteRepository.ObterTodosAsync`/`ObterNaEscalaAsync` projetam `Conferente` com uma
> subquery correlacionada que conta protocolos do dono em `Atribuido`/`Conferindo`. Nada é escrito
> em `CargaAtual` quando um protocolo muda de mão.

## Status

Accepted — 2026-08-28 (commit `3ab0618`). Complementado em 2026-08-31 (motor v2) com
`IncrementarCargaAtual()` só em memória, para a carga acumulada dentro da mesma rodada.

## Contexto

`CargaAtual` era coluna persistida desde o início, mas nunca atualizada (sempre 0). Não era só
exibição: `MotorDistribuicao` desempata urgentes por `OrderBy(CargaAtual)` — com todos em 0, o
desempate nunca funcionou (caía no primeiro da ordenação por acaso).

## Decisão

Recalcular na leitura, mesma lógica de `Semaforo.Calcular` (sempre computado, nunca persistido).
Só as leituras que alimentam listagem/motor projetam; `ObterPorIdAsync` (usado pelos fluxos que
**mutam** `Conferente`) continua query simples — entidade construída dentro de `.Select()` sai
desconectada do change tracker (ver `docs/patterns/ef-core.md`).

Dentro de uma rodada (`ImportarLote`/`RedistribuirPool`), `Conferente.IncrementarCargaAtual()`
soma em memória a cada atribuição (premissa da seção 11: "carga acumulada dentro da própria
rodada") — chamado por `AplicadorDeDistribuicao.Executar` e por `RedistribuirPool`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Gravar `CargaAtual` a cada atribuição/devolução | Leitura trivial | Estado duplicado que dessincroniza mais cedo ou mais tarde (vários caminhos mudam dono) | Mesmo raciocínio já aplicado ao semáforo |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Desempate do motor passou a funcionar |
| Performance | ⚠️ Leve custo | Subquery por conferente nas leituras de lista |

## Consequências

**Riscos** — mutar um `Conferente` vindo de uma leitura projetada não grava nada (armadilha do
change tracker).

## Referências

- `docs/patterns/ef-core.md` ("Objeto desconectado do change tracker").
- `docs/historico.md`, "RF-28 (carga real) e RF-30 (aviso de cobertura)", "Motor de alçada v2".
