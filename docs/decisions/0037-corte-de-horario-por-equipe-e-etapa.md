---
name: adr-0037-corte-de-horario-por-equipe-e-etapa
description: Prazo condicional por horário de entrada, configurável por Equipe+Etapa (corte + horário de vencimento no dia útil seguinte), como acréscimo ao TipoPrazo e em horário de Brasília fixo
metadata:
  type: decision
  status: accepted
---

# ADR-0037: Corte de horário configurável por Equipe + Etapa

> Cada equipe pode configurar, por etapa, um par (horário de corte, horário de vencimento). Ato que
> entra depois do corte vence no horário configurado do dia útil seguinte; antes do corte, vale o
> `TipoPrazo` normal. Horários em Brasília (UTC−3 fixo).

## Status

Accepted — 2026-09-22 (commit `ecd8304`).

## Contexto

"Os protocolos da equipe Quinto Andar que entram para a pós-conferência após as 16h têm prazo até as
10h do dia seguinte — como fazer isso sem ser algo chumbado?" Esclarecido: genérico por Equipe+Etapa;
**acréscimo**, não substituição; vencimento segue o ajuste de dia útil; horários de Brasília.

## Decisão

- `TipoPrazo.CorteDeHorario` — só transitório, construído por `Equipe.PrazoPara(etapa, referencia)`
  quando a entrada já foi decidida como pós-corte; nunca é o prazo base persistido da equipe.
- `record Prazo(TipoPrazo Tipo, TimeOnly? HorarioDeVencimento = null)`; reaproveita `ProximoDiaUtil`.
- `FusoHorario` (Domain): `America/Sao_Paulo` fixo UTC−3, sem `TimeZoneInfo`/horário de verão (extinto
  em 2019), mesma filosofia de "sem feriado". Todo instante do sistema é UTC — comparar "16h" direto
  dispararia a regra ~3h errada.
- `Equipe`: 4 `TimeOnly?` opcionais (par corte/vencimento por etapa), parâmetros opcionais no fim do
  construtor (19 call sites intactos); `DefinirPrazos` sem default (força o único call site real).
- `ResolvedorDePrazo.Resolver(..., DateTimeOffset referencia)` — sempre `protocolo.AndamentoEm`.
- `PrazoConversoes`: formato composto retrocompatível `"Tipo"` ou `"Tipo|HH:mm"`; colunas de prazo
  sobem de `varchar(20)` para `varchar(30)` (`"CorteDeHorario|10:00"` tem exatamente 20).
- Api: 4 campos `TimeOnly?` planos; cada par precisa ser os dois nulos ou os dois preenchidos (400).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Regra fixa para Quinto Andar | Entrega imediata | "Sem ser algo chumbado" — pedido explícito | Genérico por Equipe+Etapa |
| Substituir o `TipoPrazo` da etapa | Um prazo só por etapa | Antes do corte o prazo normal precisa continuar valendo | Confirmado como acréscimo |
| `TimeZoneInfo` com fuso real | Correto se o Brasil voltar a ter horário de verão | Dependência de tzdata do container; complexidade | UTC−3 fixo, como "sem feriado" |
| Serializar só `Tipo` (como antes) | Nenhuma mudança no converter | `HorarioDeVencimento` sumiria no round-trip e a reabertura quebraria com `NullReferenceException` | Risco real de perda de dado, achado antes da migration |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Configurabilidade | ✅ Melhora | Qualquer equipe configura seu corte |
| Compatibilidade de dados | ✅ Neutro | Linhas antigas não têm `|`, o parse continua |

## Referências

- ADR-0013 (dia útil), ADR-0007 (referência), ADR-0034 (reabertura reusa o `Prazo` recarregado).
- `docs/patterns/motor-e-prazos.md`; `docs/patterns/ef-core.md` (ValueConverter composto).
- `docs/historico.md`, "Corte de horário — prazo condicional por horário de entrada (Equipe + Etapa)".
