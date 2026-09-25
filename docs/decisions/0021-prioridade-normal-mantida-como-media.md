---
name: adr-0021-prioridade-normal-mantida-como-media
description: Prioridade ganha Baixa (3 níveis), mas o valor armazenado "Normal" não é renomeado para "Media" — o rótulo "Média" existe só no front
metadata:
  type: decision
  status: accepted
---

# ADR-0021: Três níveis de prioridade, com "Normal" mantido como valor armazenado de "Média"

> `Prioridade` passa a `Baixa`/`Normal`/`Alta`. O membro `Normal` **não** vira `Media` no C# nem no
> banco; "Média" é só rótulo do `dispatch-web` (`PRIORIDADE_LABEL`).

## Status

Accepted — 2026-09-02 (commit `41cecea`).

## Contexto

O app só tinha `Normal`/`Alta`; o protótipo aprovado sempre teve 3 níveis. Relendo a seção 4 e
RF-18d: 3 níveis é detalhe de UI, a única regra formal é binária ("urgente = prioridade **alta** OU
prazo curto"). `Prioridade` é mapeada como string (`HasConversion<string>().HasMaxLength(20)`) sem
`CHECK`.

## Decisão

Adicionar `Baixa` ao enum — sem migration (é só mais uma string possível na mesma coluna) e sem
tocar `Protocolo.Urgente`. Manter `Normal` como nome do membro.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Renomear `Normal` → `Media` | Nome igual ao da tela | `HasConversion<string>` faz `Enum.Parse`; todo protocolo gravado com `"Normal"` deixaria de ler — exigiria migration de dado | Custo e risco sem ganho de comportamento; divergência de rótulo já era aceita (`'Alta (urgente)'`) |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Compatibilidade de dados | ✅ Melhora | Nenhum registro existente muda |
| Clareza | ⚠️ Piora | Nome no código ≠ rótulo na tela |

## Consequências

**Riscos** — alguém "arrumar" o nome do enum quebra a leitura de todo protocolo existente (ver
`docs/patterns/ef-core.md`, enums como string).

## Referências

- `docs/historico.md`, "Prioridade com 3 níveis (Baixa/Média/Alta)".
