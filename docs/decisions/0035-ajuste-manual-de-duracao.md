---
name: adr-0035-ajuste-manual-de-duracao
description: A distribuidora corrige a duração final de um protocolo concluído com um valor direto em minutos; cada ajuste é auditado (quem, quando, antes, depois, motivo) e substitui o cálculo por ciclo no Dashboard
metadata:
  type: decision
  status: accepted
---

# ADR-0035: Ajuste manual de duração com trilha encadeada

> `POST /protocolos/{id}/ajustar-duracao { duracaoMinutos, motivo? }` (Distribuidora, só
> Aprovado/Reprovado). `Protocolo.Duracao = DuracaoAjustada ?? cálculo por ciclos`. Cada
> `AjusteDeDuracao` guarda a duração anterior (que pode ser outro ajuste). No Dashboard, protocolo
> ajustado atribui a duração inteira ao dono atual.

## Status

Accepted — 2026-09-22 (commit `de4ed14`).

## Contexto

"Eu como distribuidora e admin do sistema gostaria de uma opção de editar o tempo de conferência de
um protocolo." Esclarecido: valor final direto em minutos; rastreável (mesmo padrão "visibilidade,
não bloqueio" das pausas); precisa refletir no Dashboard (tempo médio por conferente, bonificação).

## Decisão

- `AjusteDeDuracao` (Domain): `AjustadoPorId`, `AjustadoEm`, `DuracaoAnterior?`, `DuracaoNova`,
  `Motivo`. Tabela `ajustes_de_duracao` (`OwnsMany`, shadow `Id`, FK cascade).
- `AjustarDuracaoProtocolo`: 204/404/409 (não concluído)/400 (negativa). Minutos, não `TimeSpan` cru.
- Dashboard: com ajuste, `Duracao` inteira vai para o `DonoId` atual e o loop por ciclo é pulado — a
  distribuidora está dizendo "o tempo certo é este", não "corrija só a última pessoa".
- **Exceção ao "back manda fato cru"**: `AjusteDeDuracaoResponse` traz `AjustadoPorNome` resolvido no
  back (`GET /protocolos/{id}/detalhe` injeta `IUsuarioRepository`), porque quem ajusta é uma
  Distribuidora que o front não tem em nenhuma lista, e não existe `GET /usuarios`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Editar os timestamps de cada ciclo | Preserva a distribuição por pessoa | Complexo de operar; não é o que foi pedido | Esclarecido: valor final direto |
| Ajuste corrige só o último ciclo | Mantém o tempo dos anteriores por pessoa | Não é a intenção de "o tempo certo é este" | Decisão consciente |
| Criar `GET /usuarios` para o front resolver o nome | Mantém o padrão fato-cru | Endpoint novo só para isso | Não vale a pena |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Auditabilidade | ✅ Melhora | Trilha encadeada com motivo |
| Fidelidade por pessoa | ⚠️ Piora | Ajuste concentra o tempo no dono atual |

## Referências

- ADR-0032, ADR-0033.
- `docs/patterns/endpoints.md` (a exceção ao fato cru).
- `docs/historico.md`, "Ajustar duração — a distribuidora corrige o tempo final de conferência".
