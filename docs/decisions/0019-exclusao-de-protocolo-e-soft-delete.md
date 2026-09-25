---
name: adr-0019-exclusao-de-protocolo-e-soft-delete
description: Excluir protocolo (RF-18i/j) é soft-delete permanente — status Excluido guardando o status anterior — e o desfazer é só restaurar esse status, sem snapshot nem job de limpeza
metadata:
  type: decision
  status: accepted
---

# ADR-0019: Exclusão de protocolo é soft-delete, e desfazer é restaurar o status

> `StatusProtocolo.Excluido` + `Protocolo.StatusAntesDeExcluir`. `Excluir()` guarda o status atual;
> `Restaurar()` devolve esse valor. Como nada além de `Status` é tocado, "restaura com o mesmo
> vencimento, dono e histórico" (RF-18j) é trivialmente verdadeiro.

## Status

Accepted — 2026-09-01 (commit `3136f93`).

## Contexto

RF-18i/j: excluir com confirmação e um aviso de "desfazer por alguns segundos" que devolve o ato
com o mesmo vencimento, dono e histórico. O projeto já preferia não apagar linhas (ADR-0004).

## Decisão

- Soft-delete permanente; restauração possível a qualquer momento (o "alguns segundos" é só o toast
  do front). Sem job de limpeza — o projeto não tem scheduler e não precisa: registro excluído só não
  aparece em tela nenhuma.
- Antes de introduzir `Excluido`, **auditar todo filtro de `Status` do repositório**:
  `ObterParaDistribuicaoAsync` não filtrava status (vazaria para Distribuição) e
  `ObterAbertosPorEscreventesAsync` excluía por lista (precisou de `!= Excluido`, senão RF-38
  recalcularia vencimento de excluído). Ambos cobertos por teste.
- Tipo de ato, ao contrário, tem **exclusão de verdade** — só quando sem nenhum uso (RF-34e,
  checado na aplicação porque `TipoAtoId` não tem FK).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Hard-delete + snapshot para o desfazer | Banco limpo | Lógica de reconstrução (dono, vencimento, histórico, filhos) para escrever e testar; janela de desfazer exigiria job | Soft-delete torna RF-18j trivial |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Simplicidade | ✅ Melhora | Restaurar é uma atribuição |
| Consistência de leitura | ⚠️ Atenção | Todo filtro por status precisa considerar `Excluido` (e `Descartado`) |

## Consequências

**Riscos** — outro caminho que mude status pode reviver um excluído: a auditoria de 2026-09-03
achou `DecidirPedidoReabertura` fazendo isso (ADR-0016). Ao adicionar status novo, repetir a
auditoria de filtros.

## Referências

- ADR-0004 (mesma filosofia para conferente).
- `docs/historico.md`, "Protocolo manual — criar, editar, excluir com desfazer (RF-18f a RF-18j)".
