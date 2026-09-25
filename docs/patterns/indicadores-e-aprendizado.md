---
name: indicadores-e-aprendizado
description: Fórmulas e interpretações do Dashboard (período de calendário, variação, série, aprovado na 1ª, score, faixa, visão restrita, tempo por ciclo) e do módulo de aprendizado sem IA (quatro propostas, limiares, dedup, descarte com memória, índice de confiança)
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

- **Período** ([ADR-0041](../decisions/0041-periodo-do-dashboard-por-calendario.md)): **calendário no
  dia de Brasília** (`CalendarioDoPeriodo`, Domain) — Semana = segunda 00:00 local até agora; Mes = dia
  1; Trimestre = 1º de jan/abr/jul/out. Vale para tudo que o Dashboard calcula. `periodoInicio` e
  `periodoFim` (UTC; fim = agora) vão na resposta. Até 2026-09-25 era janela móvel de 7/30/90 dias.
- **Base**: `IProtocoloRepository.ObterConcluidosNoPeriodoAsync(desde, ate)` — todos os donos
  (índice `(status, concluido_em)`), `ConcluidoEm` em `[inicio, fim)`.
- **Variação (RF-42b)**: `kpisAnterior`, mesmo formato de `kpis`, sobre o **mesmo trecho** do período
  anterior — início anterior = primeiro dia − 1 período no calendário local; fim anterior = início
  anterior + (agora − início), **limitado ao início atual** (31/03 compara com fevereiro inteiro).
  Segunda chamada ao mesmo `ObterConcluidosNoPeriodoAsync`, só com o trecho — não uma busca única que
  traria o vão entre os dois. Visão restrita: também vem, com os números do próprio conferente (dono
  atual; tempo pelos ciclos dele). O percentual da variação é conta do front.
- **Série (RF-42c)** — `SerieDoPeriodo` (Domain): `granularidade` `Dia` (Semana/Mes) ou `Semana`
  (Trimestre). Dia: um ponto por dia útil (seg–sex, dia local, **sem feriado**) do período **inteiro**;
  sábado/domingo só entram se tiverem conferência. Semana: um ponto por segunda-feira, da semana que
  contém o dia 1 do trimestre até a que contém o último dia (13–14 pontos; a 1ª pode começar no
  trimestre anterior, mas só conta o que foi concluído dentro do período). Ponto =
  `{ inicio: "yyyy-MM-dd", conferidos, estourados, futuro }`; `futuro` = o dia/segunda é depois de hoje
  (hoje não é futuro), com zeros. `estourados` = concluídos depois do vencimento — o complemento do
  `EstaNoPrazo`, a mesma definição do "no prazo". Visão restrita: só os protocolos dele (dono atual).
- **"Aprovados na 1ª" (RF-43)**: `percentualAprovadoNaPrimeira` em `kpis`, `kpisAnterior`, cada linha
  de `desempenho` e `mediaDaCasa` — das linhas concluídas no recorte com `NumeroDaConferencia == 1`
  (RF-24k, [ADR-0038](../decisions/0038-numero-da-conferencia-calculado-na-leitura.md)), a fração com
  `Status == Aprovado` **agora**: correção reprovado→aprovado dentro da janela conta (decisão 3 do dono
  — vale o resultado atual da linha de 1ª rodada, sem gravar o original). `null` sem nenhuma 1ª
  conferência (não é 0%). Na média da casa, média simples entre quem tem valor. Números em lote:
  `NumeroDaConferenciaEmLote` sobre os dois trechos juntos, uma query.
- **Score** (fórmula do **protótipo**; o requisito só nomeia fatores e pesos):
  `pV·(volume/volumeMáxDoGrupo) + pP·%noPrazo + pQ·%aprovado + pC·(complexidadeMédia/complexidadeMáxDoGrupo)`,
  com os **pesos da Configuração** (RF-46, [ADR-0042](../decisions/0042-metas-e-pesos-do-score-na-configuracao.md);
  padrão 40/30/20/10, inteiros ≥ 0 que **somam 100** — `PesosDoScore`, Domain). Volume e complexidade
  normalizados pelo melhor do grupo; complexidade = peso médio do `TipoAto` (`PesoComplexidade`, RF-34f).
  As 4 parcelas vão **já ponderadas** (cada uma de 0 ao seu peso, "32.4 / 40"). Os pesos valem **na
  leitura**: trocar um peso muda o score de qualquer período consultado depois, inclusive meses fechados
  (sem histórico de pesos — risco registrado no ADR-0042).
- **`pesos`** (`{ volume, prazo, qualidade, complexidade }`, o máximo de cada parcela): só pra quem vê
  score — Administrador e o próprio conferente na visão restrita; `null` pra distribuidora (ADR-0039).
- **`metas`** (RF-42b, `{ noPrazo, aprovadoNaPrimeira }`, frações 0,50–1,00, padrão 0,95/0,90 —
  `MetasDoDashboard`, Domain): **só na visão de gestão** (decisão 4 do dono: o conferente não vê meta);
  `null` na restrita. O back só informa a meta; a barra e a cor são do front.
- **Configuração**: `GET /config` (Distribuidora) devolve os 6; `PUT /config` (só Administrador) os
  aceita **opcionais** — ausente/`null` mantém o atual, campo a campo; o conjunto resultante é validado
  (400 `{ motivo }` via `MetasOuPesosInvalidos`). `ObterDashboard` lê pela `ObterAsync` cacheada.
- **Faixa**: `≥85` Integral, `≥70` Parcial, abaixo Fora (limiares do protótipo) — **fixos**, não
  configuráveis; é por isso que os pesos precisam somar 100.
- **Qualidade no score**: `percentualAprovado` (todas as linhas, resultado atual) continua sendo a
  parcela de qualidade (`pesoQualidade`, padrão 20) — o "aprovado na 1ª" é só KPI/coluna por enquanto. Trocar a parcela é decisão do dono
  (mexe em bonificação).
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
  parcelas, sem o próprio `Nivel` — ADR-0039) + linha "média da casa" (`Nome`/`Nivel`/`Parcelas`
  nulos); **`Faixa` nula nas duas**
  (leitura conservadora de "sem faixa de bônus"); `PorTipoAto` e `CumprimentoPrazoEquipe` vazios;
  **`Kpis` calculados só sobre os protocolos do próprio conferente** (vazava o total da operação até
  2026-09-15).
- **Visão de gestão sem Administrador (RF-43a)**: `ObterDashboard(incluirAvaliacaoDePessoal: false)`
  (o default) tira `Nivel`/`Score`/`Faixa`/`Parcelas` de toda linha e ordena **por nome** — a ordem por
  score, sozinha, entregaria o ranking. O endpoint passa `usuario.EhAdministrador()`.
- KPI "custo por ato" não existe (sem dado de custo — e o documento v2 já o removeu, RF-42b).

## Painel de hoje (`GET /dashboard/hoje`, RF-42a)

A faixa "Hoje, agora" (gestão) / "Seu dia" (conferente). Caso de uso próprio (`ObterPainelDeHoje`),
separado de `ObterDashboard` porque olha o **agora** (trabalho aberto + concluídos do dia), não um
período de concluídos — e o front a recarrega mais vezes que o resto.

- **Visão**: mesma regra do `GET /dashboard` (`ResolverVisaoRestritaAsync` no `DashboardEndpoints`,
  compartilhado pelas duas rotas): só Conferente → `visao: "Conferente"`; quem tem Distribuidora
  (inclui Administrador e conta combo) → `"Gestao"`. Um tipo só; os campos da outra visão vão `null`
  (gestão: `naMao`; conferente: `naFila`, `excecoes`, `gargalo`).
- **Hoje** = desde `FusoHorario.InicioDoDiaLocal(agora)` (meia-noite de Brasília). `conferidosHoje` =
  Aprovado + Reprovado com `ConcluidoEm` hoje (gestão: todos; conferente: `DonoId` dele).
- **Abertos** = Pool, Atribuído, Conferindo, Exceção. Conferente: só a mão dele (Atribuído + Conferindo;
  pausado continua Conferindo). `naFila.comConferente` = Atribuído + Conferindo de todos.
- **Em risco**: `Semaforo.Calcular` com a faixa de urgência fixa em 1h (`ObterPainelDeHoje.JanelaDeRisco`)
  — Vermelho = estourado (`vencimento < agora`), Laranja = vence em 1h (`agora ≤ vencimento < agora+1h`).
  Reaproveitar o semáforo garante que "estourado" aqui é o mesmo vermelho do card. Sem vencimento
  gravado: fora das duas contagens.
- **Gargalo**: entre os abertos em risco, agrupa pela equipe **do escrevente** (escrevente sem equipe,
  ou fora do cadastro, = grupo `equipeId: null`); devolve o maior **só se > 1**. Empate: menor `EquipeId`
  (`Guid.CompareTo`) entre equipes de verdade; "sem equipe" só vence se estiver sozinho no topo (leitura
  adotada — o contrato só dizia "menor equipeId"). O front resolve o nome da equipe.
- **Consultas** (sem N+1): gestão = `ObterParaVisaoDistribuicaoAsync(null, inicioDoDia)` (uma query:
  abertos sem corte + concluídos de hoje) + `escreventes.ObterTodosAsync` só quando há >1 em risco;
  conferente = `ObterAtribuidosAAsync` + `ObterEmConferenciaPorConferenteAsync` +
  `ObterConcluidosPorConferenteAsync`.
- `atualizadoEm` = o `agora` do `IRelogio` usado no cálculo.

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

- ADR-0009, ADR-0012, ADR-0032, ADR-0035, ADR-0038, ADR-0041.
- `docs/gaps-requisitos.md` (ritmo, metas, pesos configuráveis, exportar CSV).
