---
name: adr-0041-periodo-do-dashboard-por-calendario
description: O período do Dashboard (semana, mês, trimestre) passa de janela móvel 7/30/90 dias para calendário no dia de Brasília, e a variação compara com o mesmo trecho do período anterior
metadata:
  type: decision
  status: accepted
---

# ADR-0041: Período do Dashboard por calendário

> "Esta semana" = segunda 00:00 de Brasília até agora; "Este mês" = dia 1; "Trimestre" = 1º dia de
> jan/abr/jul/out. Vale para tudo que o `GET /dashboard` calcula. A variação (RF-42b) compara com o
> **mesmo trecho** do período anterior: início − 1 período, pela mesma duração já decorrida, sem passar
> do início atual.

## Status

`Accepted — 2026-09-25`

## Contexto

RF-42 pede "seletor de período (semana, mês, trimestre)" sem dizer se é calendário ou janela; o
protótipo v2 rotula "Este mês", "Esta semana" e "Trimestre", e o RF-42b acrescenta "a variação contra
o período anterior equivalente" em cada KPI. O RF-42c pede a série "por dia útil (por semana no
trimestre)".

Desde o primeiro Dashboard o back usava **janela móvel** a partir de `IRelogio.Agora`: 7, 30 ou 90 dias
corridos. Com isso, "Este mês" no dia 3 mostrava quase todo o mês anterior; o "período anterior
equivalente" seria os 30 dias antes dos 30 dias, sem correspondência com o mês que a gestão fecha; e a
série "do mês" não teria começo nem fim reconhecíveis. A bonificação (que usa esses números, fora do
sistema) é fechada por mês de calendário.

O dono decidiu em 25/09/2026 (`PLANO-dashboard-v2.md`, "Decisões do dono", item 1): **calendário**, e a
variação compara com o mesmo trecho do período anterior ("dia 1 até o mesmo dia do mês passado").

## Decisão

Vamos resolver o período no calendário de Brasília, porque é o recorte que a operação lê e fecha, e o
único em que "período anterior equivalente" tem par natural.

- `CalendarioDoPeriodo` (Domain, puro, testado em `CalendarioDoPeriodoTests`): `Atual(periodo, agora)`
  → `IntervaloDoPeriodo(Inicio, Fim)` em UTC, com `Fim = agora`; `MesmoTrechoAnterior(periodo, agora)`;
  `PrimeiroDia`/`UltimoDia` em `DateOnly` local (o último serve à série, que mostra o período inteiro).
- **Mesmo trecho**: início anterior = primeiro dia atual − 7 dias / − 1 mês / − 3 meses (no calendário
  local); fim anterior = início anterior + (agora − início atual), **limitado ao início atual**. Em
  31/03 o trecho passaria de fevereiro inteiro, então compara com fevereiro todo. A formulação por
  duração decorrida é a do contrato; ela coincide com "até o mesmo dia do mês passado" sempre que esse
  dia existe.
- O dia local sai de `FusoHorario` (`DiaLocal`, `InicioDoDia` — novos), como todo horário de parede do
  sistema (Brasília fixo em UTC−3, sem horário de verão desde 2019).
- `ObterDashboard` usa o intervalo em KPIs, desempenho, por tipo de ato, cumprimento por equipe e série,
  e faz uma segunda busca com o **mesmo** `ObterConcluidosNoPeriodoAsync` para o trecho anterior
  (`kpisAnterior`, também na visão restrita, com os números do próprio conferente). A resposta ganha
  `periodoInicio`/`periodoFim`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter a janela móvel 7/30/90 (o que existia) | Nenhuma mudança; números sempre com o mesmo "tamanho" de amostra | "Este mês" no dia 3 é quase o mês passado; período anterior sem par natural; série sem começo/fim | Dono decidiu calendário |
| Calendário, comparando com o período anterior **inteiro** (mês passado todo) | Uma data a menos pra calcular | Mês pela metade contra mês fechado: volume sempre "cai" até o fim do mês, variação enganosa | Dono pediu o mesmo trecho |
| Mesmo trecho pelo **mesmo dia do mês** (dia 1 até o dia N do mês passado) | Leitura literal da frase do dono | Precisa de regra à parte para o dia que não existe (31/03 → 28/02?) e para semana/trimestre; hora do dia ignorada | A duração decorrida limitada ao início atual dá o mesmo resultado quando o dia existe, e trata o resto sem caso especial |
| Uma busca só de início anterior até agora, separando em memória | Um round-trip a menos | Traz o vão entre o fim do trecho anterior e o início atual (no dia 15, meio mês sem uso) | Duas buscas indexadas `(status, concluido_em)` custam menos que carregar o vão |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | O número bate com o mês/semana que a gestão fecha; a variação compara trechos equivalentes |
| Testabilidade | ✅ Melhora | Cálculo de calendário isolado no Domain, com bordas de fuso e de mês curto |
| Desempenho | ⚠️ Piora (pouco) | Uma busca a mais (o trecho anterior) e a de continuidade para o "aprovado na 1ª" |
| Estabilidade da amostra | ⚠️ Piora | No começo do período a amostra é pequena (dia 1 = um dia); a variação oscila mais |

## Consequências

**Positivas** — "Este mês" é o mês; `periodoInicio` diz ao front exatamente o recorte; a série (RF-42c)
tem o período inteiro para desenhar, com os dias futuros marcados.

**Negativas** — nos primeiros dias do período os KPIs refletem poucos dados; quem comparava o Dashboard
de hoje com o de antes desta mudança vê números diferentes para o mesmo rótulo.

**Riscos** — sem feriado (mesma simplificação de `Prazo.ProximoDiaUtil`): a série mostra feriado como
dia útil zerado. Se Brasília voltar a ter horário de verão, `FusoHorario` é o único lugar a mudar.

## Referências

- RF-42, RF-42b, RF-42c; `PLANO-dashboard-v2.md` (raiz do workspace), "Decisões do dono (25/09/2026)"
  item 1 e "Contrato das fatias 3 + 4".
- `docs/patterns/indicadores-e-aprendizado.md` → "Dashboard"; `docs/historico.md`, entrada de
  2026-09-25 "Período de calendário, variação, série e aprovado na 1ª".
- `docs/gaps-requisitos.md` §33, §36.
- Complementa ADR-0038 (o "aprovado na 1ª" usa `NumeroDaConferencia` calculado na leitura).
