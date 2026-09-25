---
name: adr-0044-ritmo-so-dos-atos-concluidos
description: O ritmo (RF-46a) de uma pessoa usa só os atos que ela concluiu (dono atual), com o tempo dos ciclos dela neles, como razão de somas; o da operação usa a duração inteira; ato sem tipo fica fora
metadata:
  type: decision
  status: accepted
---

# ADR-0044: Ritmo só dos atos que a pessoa concluiu

> Ritmo = Σ tempo real ÷ Σ tempo de referência (ADR-0043). Por pessoa, entram **só os atos que ela concluiu no
> período** (dono atual), com o tempo **dos ciclos dela** em cada um; na operação, a duração inteira de cada ato.
> Ato sem tipo ou sem tempo medido fica fora; sem ato elegível o ritmo é nulo.

## Status

`Accepted — 2026-09-25 (commits 92f2e45, b5680fa)`

## Contexto

RF-46a: "a comparação usa o ritmo = tempo real ÷ tempo esperado, onde o tempo esperado soma, ato a ato, o tempo
de referência do tipo conferido. 1,00× é a referência". Ele substitui o tempo médio na tabela da gestão, no KPI
do conferente e em "Você e a média da casa"; RF-46b pede "Seu tempo por tipo de ato" com uma frase ("16 min
bruto; a referência para o mesmo conjunto seria 21 min; ritmo 0,76×"). O protótipo usa uma fórmula fictícia.

O Dashboard já reparte o tempo por ciclo (ADR-0032): um ato reaberto e reatribuído tem pedaços de pessoas
diferentes, e o tempo médio por conferente soma os ciclos que cada um fez — inclusive de quem não concluiu nada
(aparece com volume 0). Para o ritmo, isso abre a pergunta: o ciclo intermediário de quem não concluiu entra no
ritmo dele? E contra que referência? O dono decidiu em 25/09/2026: **"só os atos que a pessoa concluiu (dono
atual), com o tempo dos ciclos dela neles"**.

## Decisão

- **Por pessoa** (`CalculoDeRitmo.DoConferente`): os atos Aprovado/Reprovado do período cujo `DonoId` é ela; tempo
  real de cada um = `Protocolo.TempoDe(pessoa)` (soma dos ciclos dela; com ajuste manual, a duração ajustada
  inteira — ADR-0035); referência = a efetiva do tipo. Quem só fez ciclo intermediário tem linha (tempo médio),
  mas ritmo nulo.
- **Razão de somas**, não média das razões por ato (`Ritmo.Calcular`, Domain) — é o "soma, ato a ato" do RF-46a e
  a frase do RF-46b. `tempoMedioReferencia` = Σ referência ÷ nº de atos elegíveis (o "21 min" da frase).
- **Operação** (`kpis.ritmo` da gestão): Σ `Duracao` inteira ÷ Σ referência de todos os atos com tipo — o mesmo
  ponto de vista do tempo médio agregado ("quanto os atos levaram"). Visão restrita: `kpis.ritmo` é o dela.
- **Média da casa**: média simples dos ritmos de quem tem valor (como os outros campos da linha).
- **Elegibilidade**: ato sem tipo (ou tipo fora do catálogo) e ato sem tempo medido ficam fora; nenhum elegível →
  `null` (não "0×").
- Ritmo aparece em **todas** as visões — a distribuidora também vê (não é avaliação de pessoal; o score segue só
  do Administrador, ADR-0039).
- `meuTempoPorTipo` (visão restrita) agrupa os mesmos atos elegíveis por tipo, mais volume primeiro.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Todos os ciclos da pessoa, contra a referência de cada ato tocado | Não "perde" o trabalho de quem não concluiu | Um pedaço de 20 min de um ato de referência 40 viraria 0,5× para quem saiu no meio — parece rápido sem ter terminado; o ato conta referência cheia para duas pessoas | Decisão do dono |
| Duração inteira do ato para o dono atual | Simples | Quem herda um ato reaberto carrega o tempo de quem saiu (o problema que motivou ADR-0032) | Contraria ADR-0032 |
| Média das razões por ato | Cada ato pesa igual | Atos curtos dominam; não é o "soma, ato a ato" do requisito nem bate com a frase do RF-46b | Divergente do requisito |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Justiça do indicador | ✅ Melhora | Compara pessoas pelo conjunto de atos que cada uma fechou, contra a referência desse conjunto |
| Consistência | ✅ Melhora | Reusa a repartição por ciclo do Domain (`Protocolo.TemposPorConferente`), a mesma do tempo médio |
| Cobertura | ⚠️ Piora | Tempo de quem não concluiu nenhum ato não entra em ritmo nenhum (continua no tempo médio) |

## Consequências

**Positivas** — o número é explicável com os dados da própria pessoa (a frase do RF-46b sai direto de
`tempoMedio`, `tempoMedioReferencia` e `ritmo`).

**Negativas** — Σ dos numeradores individuais ≠ numerador da operação quando há ato reaberto e reatribuído
(ciclos de quem não é dono entram só no da operação). É intencional e está documentado.

**Riscos** — o ritmo alimenta leitura de desempenho (a bonificação é externa): mudar referência ou peso muda o
ritmo de períodos passados (ADR-0043).

## Referências

- RF-46a, RF-46b; `PLANO-dashboard-v2.md` (fatia 6, "Decisões do dono para as fatias 5 e 6").
- ADR-0032, ADR-0035, ADR-0039, ADR-0043 — complementa.
- `docs/patterns/indicadores-e-aprendizado.md`; `docs/historico.md`; gaps §35.
