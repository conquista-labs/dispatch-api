---
name: adr-0005-importacao-por-csv-com-pdf-fora-do-sistema
description: O relatório real do cartório é PDF; a conversão PDF→CSV fica fora do sistema (IA externa com prompt padronizado) e a etapa é parâmetro do lote, não coluna
metadata:
  type: decision
  status: accepted
---

# ADR-0005: Importação por CSV colado — PDF fica fora do sistema; etapa é do lote

> O Dispatch importa CSV (colagem de linhas, RF-05). O PDF que o cartório realmente produz é
> convertido fora do sistema por uma IA externa com prompt padronizado. A etapa (Pré/Pós) é um
> parâmetro do pedido de importação inteiro, nunca uma coluna.

## Status

Accepted — 2026-08-27 (commit `6d7b953`).

## Contexto

O requisito original supõe csv/xlsx. Um relatório de exemplo real do cliente mostrou que a fonte é
**PDF**. Além disso, cada relatório do cartório é 100% Pré ou 100% Pós — hoje também no documento
(RF-05a: "o relatório não traz essa coluna e o lote é inteiramente pré ou inteiramente pós").

## Decisão

- PDF fica fora do sistema: o dono passa o PDF por uma IA externa (prompt padronizado, guardado fora
  do código) e cola o CSV resultante. RF-05 já cobre colagem — nenhum modo de entrada novo.
- `Etapa` é campo do pedido de importação, aplicado a todas as linhas.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Parser de PDF dentro da API | Um passo a menos para a operação | Layout de PDF frágil, dependência pesada, alto custo para um formato de um cliente só | Custo desproporcional; a colagem de CSV já estava no requisito |
| Etapa por linha (coluna do CSV) | Flexível | O relatório não traz essa coluna; exigiria editar o CSV à mão | A realidade operacional é lote homogêneo |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Simplicidade | ✅ Melhora | Um formato de entrada só |
| Operabilidade | ⚠️ Piora | Passo manual fora do sistema (IA externa) antes de importar |

## Consequências

**Negativas** — `.xlsx` (RF-05) não é aceito pelo back; ver `docs/gaps-requisitos.md`.

## Referências

- `docs/historico.md`, "Importação de lote (RF-05 a RF-12)".
