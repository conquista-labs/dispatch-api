---
name: motor-e-prazos
description: Estado atual do motor de distribuição, do resolvedor de alçada (cascata v3 + reserva + equipe-e-etapa absoluto) e do cálculo de prazo/vencimento/semáforo — o que o código faz hoje, sem precisar ler a cadeia de ADRs
metadata:
  type: pattern
  domains: [dominio, motor, alcada, prazos, semaforo]
  status: stable
---

# Motor de distribuição, alçada e prazos — como funciona hoje

> O "porquê" de cada versão está na cadeia de ADRs: v1 [0002](../decisions/0002-motor-de-alcada-v1-precedencia-por-escopo.md)
> → v2 [0017](../decisions/0017-motor-de-alcada-v2-lista-fechada-por-dimensao.md) → v3
> [0018](../decisions/0018-motor-de-alcada-v3-cascata-de-camadas.md) (+ v4 [0024](../decisions/0024-motor-de-alcada-v4-equipe-nao-faz-etapa.md)
> e [0030](../decisions/0030-equipe-e-etapa-absoluto-fora-da-cascata.md)). Este documento descreve
> **o comportamento atual**. O documento de requisitos (seção 4) ainda descreve o v2 — divergência
> consciente, o documento é gerado por ferramenta externa do dono e não é editado aqui.

## Quando ler

- Antes de mexer em `Dispatch.Domain/Alcada/`, `Distribuicao/` ou `Prazos/` (junto com a skill
  `add-domain-rule`).
- Para explicar por que um protocolo foi para pool/exceção/alguém.
- Ao mexer em prazo de equipe, corte de horário ou semáforo.

## Motor de distribuição (`MotorDistribuicao.Distribuir`)

1. Tipo de ato desconhecido (`TipoAtoId` nulo) → `Excecao("tipo desconhecido")`; tipo desativado →
   `Excecao("tipo desativado")` (motivos distintos — resolução diferente).
2. Candidatos = conferentes **na escala**.
3. Para cada candidato, `ResolvedorAlcada.Resolver(conferente, CasoAlcada(etapa, tipoAto, equipeDoEscreventeId), regras)`.
4. **Continuidade** ([ADR-0022](../decisions/0022-continuidade-de-conferencia.md)): se veio
   `donoDaPrimeiraConferenciaId` e ele é elegível → `Atribuido` direto, `RegraAplicada` nula; se não é
   → `Excecao("conferente da primeira conferência não está mais disponível")`.
5. Nenhum elegível → `Excecao("ninguém com alçada")` com o motivo por pessoa.
6. `Protocolo.Urgente` (prazo `UmaHora`/`D0` **ou** prioridade `Alta`) → atribui ao elegível de menor
   `CargaAtual` (`EscolherMenosCarregado<T>`, também usado por `AtribuirAoMenosCarregado`). Senão →
   `Pool`. A decisão é por urgência, não pela contagem de elegíveis (1 elegível não urgente vai pro
   pool; vários elegíveis urgentes vão para um só).

- `ResultadoDistribuicao` (Atribuido/EnviadoParaPool/Excecao) carrega os `Elegiveis` e a avaliação
  por candidato (RNF-02). `AplicadorDeDistribuicao` grava `Protocolo.RegraAplicadaId =
  avaliacao.Decisao.RegraAplicada?.Id` — só em atribuição automática; decisão humana e continuidade
  deixam nulo.
- Carga acumulada **dentro da rodada**: cada atribuição chama `Conferente.IncrementarCargaAtual()` em
  memória ([ADR-0011](../decisions/0011-carga-atual-calculada-na-leitura.md)).
- `RedistribuirPool` (RF-16) reaplica o motor a protocolos sem dono (`Pool`/`Excecao`, não
  `Descartado`), sem recalcular prazo.
- Modo de operação global (Híbrido/Tudo atribuído/Tudo no pool, seção 4 do documento) **não existe**:
  o comportamento fixo é o Híbrido (ver gaps).

## Resolvedor de alçada (`ResolvedorAlcada`)

Regras: sujeito `Nivel` ou `Pessoa`; permissão `Permite`/`Nega`/`Reserva`; alvo `PorEtapa`,
`PorTipoAto`, `PorGrupoTipoAto`, `PorEquipeDeEscrevente(Guid?)` (nulo = "sem equipe"),
`PorTodosOsAtos` (alçada plena), `PorEquipeEEtapa(Guid?, Etapa)` (só `Nega`).

Ordem de avaliação para um conferente e um caso:

1. **Reserva** (`ReservaQueBloqueia`): reserva ativa batendo no caso bloqueia quem não é o sujeito
   dela (`MotivoAlcada.Reservado`). Não concede acesso ao próprio sujeito.
2. **Equipe+etapa** (`NegaEquipeEEtapaQueBloqueia`): `Nega`/`PorEquipeEEtapa` valendo para o
   conferente (nível ou pessoa) e batendo no caso → `Negado`/`EquipeEEtapa`, absoluto.
3. **Cascata** (`CamadasComOpiniao`), cada camada contra o **caso inteiro**:
   - `Base por nível` — regras cujo sujeito é o nível do conferente, qualquer alvo;
   - `Ajuste por equipe` — regras da pessoa com alvo equipe;
   - `Exceção por pessoa` — regras da pessoa com outros alvos.
   Dentro da camada (`DecideCamada`): negação que bate vence; senão alçada plena satisfaz; senão cada
   dimensão (equipe → etapa → grupo → tipo, `OrdemDasDimensoes`) com alguma permissão na camada vira
   **lista fechada** (fora dela = "fora da alçada"). Camada sem regra aplicável não opina. **A de baixo
   sobrescreve a de cima** quando opina.
4. Nenhuma camada opinou → permitido (padrão aberto).

- `Resolver` devolve só `DecisaoAlcada` (caminho quente: motor, importação). `Explicar` devolve a
  trilha `PassoTrilha` por camada (painel de detalhe, `POST /regras-alcada/testar`). A reserva e o
  equipe+etapa ficam fora da unificação `CamadasComOpiniao` de propósito — saídas diferentes, e essa
  é a parte historicamente mais propensa a erro (reescrita 3×).
- `MotivoAlcada`: `Etapa`/`Tipo`/`Grupo`/`Equipe`/`Geral`/`Reservado`/`EquipeEEtapa` — sem nome
  próprio; o texto é do front.
- Exemplo resolvido da seção 4 continua valendo: regra pessoal permitindo o mesmo alvo que a de nível
  nega → permitido (`RegraPessoalPermiteMesmoComRegraDeNivelNegandoOMesmoAlvo_Permite`).
- Tipo desconhecido/removido é "sem alçada" antes de chamar o resolvedor (`PegarProtocolo`,
  `ObterMinhaFila`, `AtribuirAoMenosCarregado`, `ObterDetalheProtocolo` precisam de `ITipoAtoRepository`
  porque o caso leva o `TipoAto` inteiro, pelo `.Grupo`).
- **Leituras agregadas são aproximação**: `ObterAlcancePorConferente` (RF-34) fixa um caso
  representativo por eixo (etapa `PosConferencia` + sem equipe para "tipos permitidos"; um tipo
  representativo — o primeiro que a pessoa já alcança, senão o primeiro do catálogo — para etapas e
  equipes). `ObterCoberturaDeAlcada` (RF-30) reaproveita isso e cruza só com quem está `NaEscala`;
  "tipo em circulação" = `TipoAtoId` distinto presente nos protocolos. `ListarTiposAtoComUso` também.
- `SimularAlcada` roda o motor de verdade sobre um `Protocolo`/`Escrevente` transitórios (com a
  prioridade informada) para devolver o destino real, não uma inferência por contagem.
- Persistência das regras: ver `ef-core.md` (classe-registro + discriminador + `CHECK`).

## Prazo e vencimento

- Prazo **derivado, nunca digitado**: escrevente → equipe → `Equipe.PrazoPara(etapa, referencia)`.
  Escrevente sem equipe → D+1 padrão e sinalizado (`ResolvedorDePrazo`, RF-09).
- `TipoPrazo`: `UmaHora`, `D0` (fim do dia **de Brasília** — modelado como a meia-noite local seguinte, 03h UTC), `D1` (24h
  corridas), `D2` (48h), e o transitório `CorteDeHorario` ([ADR-0013](../decisions/0013-prazos-em-horas-corridas-com-dia-util.md)).
- **Dia útil**: D0/D1/D2 e o corte caindo em sábado/domingo (dia da semana **de Brasília**) vão para
  segunda, mesmo horário (`ProximoDiaUtil`). Sem feriados. `UmaHora` nunca é empurrado.
- **Corte de horário** ([ADR-0037](../decisions/0037-corte-de-horario-por-equipe-e-etapa.md)): se a
  equipe tem (corte, vencimento) para a etapa e a entrada foi depois do corte **em horário de
  Brasília**, o prazo é `Prazo(CorteDeHorario, horarioVencimento)` — vence no horário configurado do
  dia útil seguinte. Senão, o `TipoPrazo` base.
- **Fuso**: todo instante do sistema é UTC (inclusive `IRelogio.Agora` e o que volta do Postgres).
  Comparar horário de parede só via `FusoHorario` (UTC−3 fixo, público — a Application também usa).
  Comparar "16h" direto contra UTC erra por ~3h.
- **Dia local** ("hoje", "fim do dia", dia da semana): `FusoHorario.InicioDoDiaLocal(instante)` devolve
  a meia-noite de Brasília daquele instante, já em UTC (pronta pra query). **Nunca** `instante.Date` /
  `new DateTimeOffset(x.Date, x.Offset)` num instante UTC: entre 21h e 24h de Brasília o dia UTC já
  virou, e o "hoje" zerava às 21h (`ObterConcluidosHoje`, `ObterVisaoDistribuicao`) e o D+0 vencia às
  21h — ou no dia seguinte, se a entrada fosse depois das 21h. Corrigido em 2026-09-25 (itens 0.6 do
  `PLANO-dashboard-v2.md`); vencimentos já gravados não foram recalculados.
- **Referência** = `Protocolo.AndamentoEm` ([ADR-0007](../decisions/0007-vencimento-a-partir-do-andamento.md)):
  importação, recálculo por mudança de prazo da equipe (RF-38, `RecalculoDeVencimentos` — só
  protocolos **abertos**, incluindo Exceção, excluindo Aprovado/Reprovado/Descartado/Excluido) e
  edição que muda tipo/escrevente/etapa (RF-18g). **Exceção**: reabrir recalcula a partir de agora
  ([ADR-0034](../decisions/0034-reabertura-recalcula-vencimento.md)).
- Edição manual (RF-18h): se a identidade mudou e o dono perdeu alçada (`VerificadorDeAlcada`), volta
  pro pool.

## Semáforo

`Semaforo.Calcular` (sempre computado): verde / amarelo (menos que `FaixaAtencao`) / laranja (menos
que `FaixaUrgente`) / vermelho (vencido). As faixas vêm de `Configuracao` (padrão 4h/60min) e
`faixaUrgente` precisa ser menor que `faixaAtencao` (senão o laranja nunca aparece — validado no
`PUT /config`). Pool, atribuídos de Minha fila e colunas ordenam por `VencimentoEm`, nulos por último.

## Ciclo de vida do protocolo (resumo)

`Pool` → (`PegarProtocolo`/motor/manual) `Atribuido` → `IniciarConferencia` (limite de simultâneos da
config, padrão 1; pausado conta) `Conferindo` ⇄ `Pausar`/`Retomar` → `Aprovado`/`Reprovado`
(`CorrigirResultado` dentro da janela; `ReabrirConferencia` → `Atribuido` do mesmo dono, ou `Pool` se
fora da escala). Laterais: `Excecao`, `Descartado`, `Excluido` (guarda `StatusAntesDeExcluir`).
`Duracao` = override de ajuste manual, senão soma de `CiclosAnteriores` + ciclo atual. Marcar ausente
ou remover conferente devolve os atribuídos ao pool (RF-27); trocar senha devolve os em conferência.

## Número da conferência (RF-24k)

`ResolvedorDeContinuidade.NumeroDaConferencia` — `1 +` as outras linhas do mesmo `Numero`, na mesma
etapa, com `AndamentoEm` estritamente anterior e status `Reprovado`. Aprovado (inclusive corrigido),
aberto, Descartado e Excluído não contam; a própria linha e as posteriores também não (a linha antiga
continua sendo a 1ª). Calculado na leitura, nunca gravado (ADR-0038): listagens usam
`NumeroDaConferenciaEmLote` (uma query projetada pelos números distintos), o detalhe reaproveita o
histórico que já carrega. Chega ao front em `ProtocoloResumo.NumeroDaConferencia`,
`DetalheProtocoloResponse.NumeroDaConferencia` e em cada `HistoricoConferenciaResponse`, que também
traz a `Observacao` da linha — o "motivo da não aprovação".

## Referências

- ADRs 0002, 0007, 0011, 0013, 0017, 0018, 0022, 0024, 0030, 0031–0035, 0037, 0038.
- Skill `add-domain-rule`.
- `indicadores-e-aprendizado.md` (Dashboard e sugestões).
