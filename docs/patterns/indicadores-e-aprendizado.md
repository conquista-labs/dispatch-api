---
name: indicadores-e-aprendizado
description: Fórmulas e interpretações do Dashboard (score, faixa, visão restrita, tempo por ciclo) e do módulo de aprendizado sem IA (quatro propostas, limiares, dedup, descarte com memória, índice de confiança)
metadata:
  type: pattern
  domains: [dominio, dashboard, aprendizado, metricas]
  status: stable
---

# Indicadores (Dashboard) e aprendizado sem IA

> Onde o documento de requisitos é vago, o que está aqui é a leitura adotada — e está marcado. O
> tempo de conferência alimenta uma conta de bonificação **fora do sistema**: trate qualquer mudança
> em tempo/volume como mudança de dado financeiro.

## Quando ler

- Antes de mexer em `ObterDashboard`, `GeradorDeSugestoes`, `GerarSugestoes`, `AplicarSugestao`.
- Ao responder "de onde vem esse número?".

## Dashboard (`GET /dashboard?periodo=Semana|Mes|Trimestre`, RF-42 a RF-46)

- **Período**: janela móvel a partir de `IRelogio.Agora` (7/30/90 dias), não mês calendário.
- **Base**: `IProtocoloRepository.ObterConcluidosNoPeriodoAsync(desde, ate)` — todos os donos
  (índice `(status, concluido_em)`).
- **Score** (fórmula do **protótipo**; o requisito só nomeia fatores e pesos):
  `40·(volume/volumeMáxDoGrupo) + 30·%noPrazo + 20·%aprovado + 10·(complexidadeMédia/complexidadeMáxDoGrupo)`.
  Volume e complexidade normalizados pelo melhor do grupo; complexidade = peso médio do `TipoAto`
  (`PesoComplexidade`, RF-34f). As 4 parcelas vão **já ponderadas** ("32.4 / 40").
- **Faixa**: `≥85` Integral, `≥70` Parcial, abaixo Fora (limiares do protótipo).
- **Simplificação consciente**: "% aprovado" usa o resultado **atual** (`Status == Aprovado`), não
  "aprovado na 1ª" — não há histórico do resultado original antes de uma correção; inferir por
  `CorrigidoEm == null` não é confiável o bastante para bonificação (ver gaps).
- **"No prazo"**: `EstaNoPrazo` único, reaproveitado por KPIs, desempenho e cumprimento por equipe.
- **Cumprimento de prazo por equipe** (RF-43): agrupado por `(EquipeId do escrevente, Etapa)` —
  prazos diferem por etapa; "sem equipe" é grupo próprio (`EquipeNome: "sem equipe"`, prazo real
  D+1); ordenado pelo pior percentual primeiro (igual ao protótipo). `Prazo` do grupo é só informativo.
- **Desempenho por tipo de ato**: volume, tempo médio, % reprovação.
- **Tempo** ([ADR-0032](../decisions/0032-tempo-de-conferencia-por-ciclo.md)): `TempoMedio` por
  conferente vem dos **ciclos que a pessoa fez** (`ConstruirTemposPorConferente`); KPIs agregados e
  "por tipo" somam o protocolo inteiro. Quem fez ciclo mas não é dono de nada aparece com `Volume: 0`.
  Protocolo com ajuste manual atribui a duração inteira ao dono atual
  ([ADR-0035](../decisions/0035-ajuste-manual-de-duracao.md)). Volume/score/prazo/aprovação/complexidade
  continuam do dono atual.
- **Visão restrita (RF-45)**, quando o token é só Conferente: linha do próprio conferente (com nome e
  parcelas) + linha "média da casa" (`Nome`/`Nivel`/`Parcelas` nulos); **`Faixa` nula nas duas**
  (leitura conservadora de "sem faixa de bônus"); `PorTipoAto` e `CumprimentoPrazoEquipe` vazios;
  **`Kpis` calculados só sobre os protocolos do próprio conferente** (vazava o total da operação até
  2026-09-15).
- KPI "custo por ato" não existe (sem dado de custo — e o documento v2 já o removeu, RF-42b).

## Aprendizado sem IA (RF-39 a RF-41)

Sem tabela `evento_decisao` ([ADR-0009](../decisions/0009-aprendizado-sem-tabela-evento-decisao.md)).
`GeradorDeSugestoes` tem quatro funções puras; limiares vêm de `Configuracao`:

| Proposta | Gatilho (padrão) | Sugestão | Aplicar (RF-40) |
| -------- | ---------------- | -------- | --------------- |
| `TipoDesconhecido` | ≥5 ocorrências de `TipoAtoNomeOriginal` resolvidas na mão | moda do nível de quem resolveu | adiciona ao catálogo |
| `PrazoIrreal` | ≥8 casos e >60% de estouro em equipe+etapa | faixa mais próxima do percentil 80 do tempo real (durações típicas de referência 1h/12h/36h/60h — aproximação consciente) | `Equipe.DefinirPrazos` + `RecalculoDeVencimentos` (preserva corte de horário) |
| `EscreventeOrfao` | ≥3 protocolos sem equipe | equipe dominante nos mesmos lotes | `Escrevente.MoverParaEquipe` |
| `RiscoQualidade` | ≥6 casos e >50% de reprovação em tipo+nível | restringir o tipo àquele nível | cria `RegraAlcada` `Nega` com `Origem.Aprendida` |

- `POST /sugestoes/gerar` (sob demanda — não há scheduler) decide por `Chave`: nova (não achou, ou
  descartada com janela vencida) / atualiza ocorrências e evidência (pendente) / ignora (descartada
  dentro da janela, ou aplicada).
- Descartar silencia por `DiasDeMemoriaDescarte` (padrão 30).
- **Índice de confiança** (nem requisito nem protótipo definem — protótipo usa número mockado): a
  proporção que cada função já calculava para comparar com o próprio limiar, na escala [0,1] —
  força da moda (`ModaComForca<T>`), `percentualEstouro`, dominância da equipe, `percentualReprovacao`.
  Recalculado junto com `Ocorrencias`/`Evidencia`. Sugestões antigas ficam 0 até a próxima rodada.
- Com o cadastro automático de tipo na importação ([ADR-0012](../decisions/0012-importacao-cadastra-tipo-de-ato-novo.md)),
  `TipoDesconhecido` quase não dispara para nomes legíveis.

## Referências

- ADR-0009, ADR-0012, ADR-0032, ADR-0035.
- `docs/gaps-requisitos.md` (ritmo, metas, pesos configuráveis, exportar CSV, "aprovado na 1ª").
