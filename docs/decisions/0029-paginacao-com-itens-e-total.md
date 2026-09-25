---
name: adr-0029-paginacao-com-itens-e-total
description: Primeira paginação do sistema (GET /tipos-ato/com-uso) — offset por página, busca antes de paginar, parâmetros clampados, resposta { Itens, Total } via Paginado<T> como formato padrão
metadata:
  type: decision
  status: accepted
---

# ADR-0029: Paginação por página com `{ Itens, Total }`

> `Paginado<T>(Itens, Total)` é o formato de toda paginação futura. Parâmetros `busca`, `pagina`
> (padrão 1), `tamanhoPagina` (padrão 20, máx. 100) — clampados, não rejeitados. A busca filtra
> **antes** de paginar.

## Status

Accepted — 2026-09-15 (commit `6bce51d`).

## Contexto

O dono pediu paginação de verdade na lista de Tipos de ato em produção. Até aqui nenhum endpoint
paginava; as outras listas usam "busca + rolagem contida" no front.

## Decisão

- `Dispatch.Application/CasosDeUso/Paginado.cs` — record mínimo, sem cursor nem metadata que ninguém
  usa.
- `ListarTiposAtoComUso.ExecutarAsync(string? busca, int pagina = 1, int tamanhoPagina = 20)`: filtro
  por nome (case-insensitive, `Contains`) antes de paginar; `Math.Max(1, ...)` e `Math.Clamp(1, 100)`.
- `GET /tipos-ato/com-uso` passa de array solto para `PaginaDeTipoAtoComUsoResponse { Itens, Total }`
  — primeira mudança de shape array → objeto do sistema.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Cursor/keyset | Estável sob inserção; escala | Mais complexo; "ir para página N" difícil | Catálogo pequeno; o componente do front é por página |
| Rejeitar parâmetro fora do intervalo com 400 | Explícito | Leitura fora do range é mais barata de tolerar | Diferente de `PUT /config`, onde valor fora do range é erro de digitação que vale avisar |
| Continuar só client-side | Nenhuma mudança de contrato | Pedido explícito do dono | — |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Consistência de contrato | ✅ Melhora | Um formato para qualquer paginação futura |
| Compatibilidade | ⚠️ Quebra | Consumidores do array antigo precisaram mudar (front, na mesma rodada) |

## Referências

- `docs/patterns/endpoints.md` (convenções de resposta).
- `docs/historico.md`, "Primeira paginação de verdade do sistema — GET /tipos-ato/com-uso".
