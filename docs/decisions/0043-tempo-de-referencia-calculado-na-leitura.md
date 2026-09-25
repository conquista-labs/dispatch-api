---
name: adr-0043-tempo-de-referencia-calculado-na-leitura
description: O tempo de referência por tipo de ato (RF-46c) é informado → mediana de 12 meses (≥ 30, descarte > 4× a estimativa) → estimativa; só o informado é gravado, a efetiva é calculada na leitura, em lote, sem cache; o peso de complexidade vira decimal 0,50–2,50
metadata:
  type: decision
  status: accepted
---

# ADR-0043: Tempo de referência por tipo de ato calculado na leitura

> `tipos_ato` ganha só o valor **informado** (`tempo_referencia_minutos`, 2–240, nulo = não informado) e o
> peso vira `numeric(3,2)` 0,50–2,50. A referência **efetiva** — informado → mediana dos últimos 12 meses
> (≥ 30 conferências válidas, descartando as > 4× a estimativa) → estimativa `round(TempoMedioPorAtoMinutos
> × peso)` — é calculada em cada leitura que precisa dela, numa query projetada por listagem, sem cache e
> sem coluna materializada.

## Status

`Accepted — 2026-09-25 (commits 92f2e45, b5680fa)`

## Contexto

RF-46c define a origem do tempo de referência "por tipo de ato, nesta precedência: (1) valor informado pelo
administrador em Tipos de ato; (2) mediana histórica da casa, quando o tipo tem pelo menos 30 conferências;
(3) estimativa = 15 min × peso de complexidade" e manda descartar da mediana "conferências acima de 4× a
referência do tipo". RF-34a pede, em Tipos de ato, a referência com a origem, um stepper que grava o valor
informado e "usar histórico" que volta à mediana. RF-46a usa a referência como divisor do ritmo.

O requisito deixa quatro coisas abertas, que o dono fechou em 25/09/2026 (`PLANO-dashboard-v2.md`,
"Decisões do dono para as fatias 5 e 6"):

- **Estimativa**: `round(Configuracao.TempoMedioPorAtoMinutos × peso)`, não 15 fixo — o tempo médio já é
  configurável (RF-28) e 15 fixo ignoraria a configuração da casa.
- **Janela da mediana**: conferências concluídas nos **últimos 12 meses**, **≥ 30**.
- **Descarte**: duração > 4× a **estimativa** (não "a referência": a referência depende da própria mediana —
  circular — e um valor informado baixo descartaria o histórico real).
- **Peso decimal** 0,50–2,50 em passos de 0,05 (decisão 2 da rodada), com os inteiros atuais convertidos
  1→1,00 · 2→1,25 · 3→1,50 · 4→1,75 · 5→2,00 (outros: clamp).

Duas restrições técnicas pesam na forma de calcular: `Protocolo.Duracao` **não é coluna** (é ciclos +
ajuste manual, calculada em memória — ADR-0032/0035), e **não há job** no sistema (gaps §29) — qualquer
valor materializado precisa ser recalculado por quem escreve. A tela de Tipos de ato é paginada no servidor
(ADR-0029).

## Decisão

Vamos gravar só o que é **decisão humana** (o valor informado e o peso) e calcular a referência efetiva **na
leitura**, porque ela depende de dado que muda o tempo todo (cada conclusão, correção, reabertura e ajuste de
duração mexe na mediana; cada troca de peso ou de `TempoMedioPorAtoMinutos` mexe na estimativa).

- **Domain** (`Indicadores/TempoDeReferencia.cs`, puro): `Calcular(informado, peso, tempoMedio, duracoes)`
  aplica precedência, janela mínima, descarte e arredondamento escolar (`MidpointRounding.AwayFromZero`,
  22,5 → 23 — o "round" do dono, não o bancário do .NET). Minutos inteiros, nunca abaixo de 1 (é divisor do
  ritmo). `MedianaMinutos` sai mesmo com valor informado (a tela precisa saber se "usar histórico" tem pra
  onde voltar); `ConferenciasNoHistorico` conta as válidas, depois do descarte. `PesoDeComplexidade.Validar`
  e `TempoDeReferencia.ValidarInformado` são as regras de escrita; `TipoAto` revalida (lança) e o banco tem os
  dois `CHECK`.
- **Uma query por listagem**: `IProtocoloRepository.ObterDuracoesConcluidasPorTipoAsync(tipoIds, desde)`
  projeta só tipo + início/fim + ciclos + último ajuste das linhas Aprovado/Reprovado da janela (índice
  `(status, concluido_em)`), e a soma reusa `Protocolo.CalcularDuracao` — a mesma função da propriedade
  `Duracao`, então a conta não é reescrita em SQL. `ReferenciasDeTempoEmLote` (Application) chama isso uma
  vez e aplica a regra por tipo em memória.
- **Só o que a tela usa**: `GET /tipos-ato/com-uso` calcula para os tipos **da página** (depois de paginar);
  `GET /dashboard` para os tipos que aparecem nos dois trechos (atual e anterior). Referência é sempre a **de
  agora** — o trecho anterior do Dashboard usa a mediana atual, como os pesos do score (ADR-0042).
- **Sem cache**: invalidar exigiria ganchos em conclusão, correção, reabertura, ajuste, troca de peso e
  PUT /config; a consulta projetada e indexada custa menos que esse acoplamento no volume de um cartório
  (dezenas de milhares de linhas/ano no pior caso).
- **Migration** `ConverteTempoDeReferenciaEPesoDecimalEmTiposAto`: `ALTER COLUMN ... TYPE numeric(3,2) USING
  (CASE ...)` com o mapa do dono em SQL explícito (o `AlterColumn` gerado faria cast direto: 3 → 3,00, fora da
  faixa); `CHECK`s depois da conversão; `Down()` pelo mapa inverso (mais próximo).
- **Endpoints**: `PUT /tipos-ato/{id}/peso` aceita decimal (400 com motivo fora da faixa/passo — sai o clamp
  silencioso); `PUT /tipos-ato/{id}/tempo-referencia { minutos: int | null }` (só Administrador, grupo de gestão
  de tipos, ADR-0039); `GET /tipos-ato` ganha `pesoComplexidade`; `GET /tipos-ato/com-uso` ganha
  `tempoReferencia { minutos, origem, informadoMinutos, medianaMinutos, conferenciasNoHistorico }`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Coluna materializada `tempo_referencia_efetivo` (+ origem, N) em `tipos_ato`, recalculada por quem escreve | Leitura trivial; Dashboard não consulta histórico | Seis caminhos de escrita teriam de recalcular (concluir, corrigir, reabrir, ajustar duração, peso, config); esquecer um deixa o número errado em silêncio; não há job pra corrigir | Acoplamento e risco de dado velho num número que alimenta bonificação |
| Cache em memória por tipo (como a `Configuracao`), invalidado nas escritas | Uma consulta a cada N leituras | Mesma lista de ganchos de invalidação; instância única do Render ajuda, mas o ganho é pequeno frente a uma query indexada | Custo de manutenção maior que o de performance evitado |
| Mediana em SQL (`percentile_cont`) | Nada de duração trafega | Duração é ciclos + ajuste (não coluna): a conta teria de ser reescrita em SQL, divergindo de `Protocolo.Duracao`; o descarte depende de config × peso por tipo | Duas implementações da mesma regra de tempo — exatamente o que ADR-0032/0035 evitaram |
| Materializar `Protocolo` completo (`ObterConcluidosNoPeriodoAsync` na janela de 12 meses) | Zero código novo no repositório | Traz todas as colunas e as três coleções filhas (pausas inclusive) de milhares de linhas | A projeção custa uma query igual e muito menos dado |
| Estimativa `15 × peso` (texto literal do RF-46c) | Fiel ao texto | Ignora `TempoMedioPorAtoMinutos`, que a casa já configura | Decisão do dono |
| Descarte > 4× a **referência** (texto do RF-46c) | Fiel ao texto | Circular com a mediana; informado baixo descartaria histórico real | Decisão do dono: 4× a estimativa |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Corretude | ✅ Melhora | Um só lugar calcula a duração (`Protocolo.CalcularDuracao`) e um só a referência (`TempoDeReferencia`) |
| Performance | ⚠️ Piora (pouco) | +1 query projetada por `GET /tipos-ato/com-uso` e por `GET /dashboard` |
| Testabilidade | ✅ Melhora | Regra pura no Domain com bordas (29 × 30, descarte, arredondamento) testadas sem banco |
| Auditabilidade | ➖ Neutro | O informado fica gravado; a efetiva é reprodutível a partir do histórico, mas não tem histórico próprio |

## Consequências

**Positivas** — cartório novo começa em "Estimado" e migra para "Historico" sozinho conforme acumula
conferências (RF-46c), sem job. Mudar peso ou tempo médio reflete na hora.

**Negativas** — a referência de um período passado muda conforme o histórico anda (mesmo trade-off dos pesos
do score, ADR-0042). O indicador "quantos tipos ainda em estimativa" (RF-34a) não sai da página — o contrato
não o incluiu (gaps §26).

**Riscos** — o front anterior manda peso inteiro 1–5: 1 e 2 continuam válidos, 3–5 passam a receber 400 entre o
deploy da API e o do front. Se o volume crescer muito, a query dos 12 meses é o ponto a observar (pode
virar cache com invalidação ou coluna materializada — revisitar este ADR).

## Referências

- RF-34a, RF-34f, RF-46c, RF-18a; `PLANO-dashboard-v2.md` (fatia 5).
- ADR-0029 (paginação), ADR-0032/0035 (tempo por ciclo, ajuste manual), ADR-0039 (Administrador), ADR-0042
  (aplicado na leitura) — complementa, não substitui.
- `docs/historico.md` ("Dashboard v2 — fatias 5 e 6"); `docs/patterns/indicadores-e-aprendizado.md`; gaps §26.
