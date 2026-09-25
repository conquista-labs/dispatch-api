# Histórico de entregas do dispatch-api

> Registro cronológico do que foi construído, em que arquivos, e como foi verificado. Uma seção
> `## ` por entrega, na ordem dos commits. Decisões com alternativas reais viraram ADR
> (`docs/decisions/`); lições reutilizáveis viraram pattern (`docs/patterns/`); lacunas em relação
> ao documento de requisitos estão em `docs/gaps-requisitos.md`. Este arquivo é consulta, não
> leitura obrigatória de sessão.

## Como usar

- **Não acrescente entregas novas no `CLAUDE.md`.** Acrescente aqui, no fim, no mesmo formato
  (data do commit — título; o que mudou; arquivos; como foi verificado; contagem de testes), e crie
  ADR (skill `/api-adr`) ou atualize o pattern quando couber.
- **Comentários no código que dizem "ver CLAUDE.md, seção X"** apontam para seções que viviam no
  CLAUDE.md até 2026-09-25. Elas estão aqui com **o mesmo título** (depois da data) — procure pelo
  título. As exceções: "Decisões adiadas conscientemente" foi para `docs/gaps-requisitos.md` (§40,
  e §29 para o scheduler), comentários que falam em "gap consciente" apontam para
  `docs/gaps-requisitos.md`, e "Deploy — no ar" é a entrada de 2026-08-31 abaixo (procedimento atual
  em `docs/patterns/deploy.md`). O `../dispatch-web/CLAUDE.md` também cita "`../dispatch-api/CLAUDE.md`,
  mesma seção" — mesma regra: procure o título aqui.
- Entradas marcadas *(reconstituído do commit)* não tinham seção própria no CLAUDE.md; o texto vem
  da mensagem do commit.

---

## 2026-08-26 — Estado atual (scaffold, motor, prazos, primeira persistência)

Seção "Estado atual" do CLAUDE.md original, que cobria os primeiros commits:

- **Scaffold** (`2e2f6ed`): solution Clean Architecture (ADR-0001), `docker-compose.yml` com Postgres
  local, Dockerfile.
- **Motor de distribuição, seção 4** (`bd4d305`): `Dispatch.Domain` raiz (`TipoAto`, `Conferente`,
  `Protocolo`, enums `Nivel`/`Etapa`/`Prioridade`); `Alcada/` (`SujeitoAlcada`/`AlvoAlcada` como
  hierarquias fechadas, `RegraAlcada`, `ResolvedorAlcada` com precedência pessoa > nível, negação >
  permissão, ausência = permitido — ADR-0002); `Distribuicao/` (`MotorDistribuicao` com os 5 passos,
  `AvaliacaoCandidato`, `ResultadoDistribuicao` Atribuido/EnviadoParaPool/Excecao carregando a regra
  aplicada por candidato, RNF-02). 10 testes.
- **Prazo e semáforo, seção 5** (`a638188`): `Prazos/` — `Prazo`/`TipoPrazo` (1 hora, D+0, D+1, D+2),
  `Equipe`/`Escrevente`, `ResolvedorDePrazo` (escrevente sem equipe → D+1 sinalizado, RF-09),
  `Semaforo`/`FaixaSemaforo` (faixas como parâmetro, são configuração). `Protocolo` ganhou
  `Prazo`/`VencimentoEm` via `DefinirPrazo` (não no construtor); `Urgente` passou a considerar prazo
  curto (1h/D+0) além de prioridade alta. 28 testes.
- **Primeiro caso de uso** (`aa800ac`): `DistribuirProtocolo` (`CasosDeUso/`) orquestra
  `ResolvedorDePrazo` + `MotorDistribuicao`; portas em `Portas/` (`IConferenteRepository`,
  `IEquipeRepository`, `IRegraAlcadaRepository`, `ITipoAtoRepository`, `IRelogio`).
  `Dispatch.Application.Tests` com fakes em memória. 31 testes.
- **Persistência real** (`b9083e7`): `DispatchDbContext` (Npgsql), `IEntityTypeConfiguration<T>` em
  `Configuracoes/`, 5 repositórios em `Repositorios/`, `UseSnakeCaseNamingConvention`. Migration
  `InicializarSchema`. Decisões de mapeamento: `Prazo` via `ValueConverter` (`PrazoConversoes`), não
  `OwnsOne`; `RegraAlcada` persistida por `RegraAlcadaRegistro` (`Persistencia/`) com colunas
  achatadas e `CHECK` `num_nonnulls` (ver `docs/patterns/ef-core.md`). `Program.cs` chama
  `AddInfrastructure`; `/health` 200 contra o Postgres local; `dotnet-ef` como tool local.
- **Primeiro endpoint** (`650d6aa`): `POST /protocolos/distribuir` (`Endpoints/ProtocoloEndpoints.cs`)
  com DTOs próprios (`DistribuirProtocoloRequest`/`Response`), `ResultadoDistribuicao` traduzido por
  `switch`. Testado ponta a ponta com dado inserido via DBeaver/psql cobrindo os três destinos.
  Swagger UI: `Microsoft.AspNetCore.OpenApi` gera `/openapi/v1.json`, `Swashbuckle.AspNetCore.SwaggerUI`
  (só a UI) em `/swagger`, na época só em Development (liberado em produção em 2026-08-28). Enums como
  string (`JsonStringEnumConverter`).
- Pendências registradas na época (todas resolvidas depois): endpoint não persistia o `Protocolo`
  ("prévia"); sem endpoints de cadastro; sem seed.

## 2026-08-26 — Autenticação e autorização

(`3decc29`) Fechou o buraco de nenhum endpoint ter proteção (RNF-04), escopo mínimo — ADR-0003.
`Usuario`/`Papel` em `Dispatch.Domain/Usuarios/`; `Autenticar` com portas `IUsuarioRepository`,
`IHashDeSenha`, `IEmissorDeToken` (resultado não diferencia e-mail inexistente de senha errada);
`HashDeSenhaAspNetCore` com `PasswordHasher<object>` de `Microsoft.Extensions.Identity.Core`;
`EmissorDeTokenJwt` com papel em `ClaimTypes.Role`; config `Jwt:*`. `POST /auth/login`
(`AllowAnonymous`); `/protocolos/distribuir` passou a exigir `Distribuidora`.

Duas armadilhas registradas aqui pela primeira vez (hoje em `docs/patterns/ef-core.md` e
`docs/patterns/endpoints.md`): propriedade só com getter falhando o constructor binding no
`migrations add` (`Conferente.NaEscala`/`CargaAtual`, `Usuario.Ativo`) — resolver com
`builder.Property(...)` explícito; e dois gotchas de OpenAPI 2.x (`EnumSchemaTransformer` para o
`"type": "string"` que falta; transformer com duas interfaces precisa de dois registros).

## 2026-08-26 — Cadastro de conferentes (RF-25/RF-26/RF-27)

(`de20a8e`) `Conferente` ganhou `UsuarioId` (FK única, sem navigation no Domain) e `JornadaHoras`;
`Nivel`/`NaEscala` viraram `private set` (`AtualizarNivelEJornada`, `MarcarPresenca`);
`Usuario.Ativo` com `Desativar()`. Casos de uso em `/conferentes` (Distribuidora):
`CadastrarConferente` (cria `Usuario` com papel fixo `Conferente` + `Conferente`, atômico, rejeita
e-mail duplicado antes), `EditarNivelEJornada`, `MarcarPresenca`, `RemoverConferente` (soft delete —
ADR-0004). Nova porta `IUnitOfWork` (repositórios só marcam estado; o caso de uso chama `SalvarAsync`
uma vez — equivalente explícito ao `prisma.$transaction`).

## 2026-08-27 — Swagger por tags e static web assets *(reconstituído do commit)*

(`997989b`, `1c882bf`) Rotas do Swagger categorizadas por tag; static web assets desligados na Api
(eliminava warning do `dotnet watch`).

## 2026-08-27 — Persistência de Protocolo

(`b36fb9d`) `/protocolos/distribuir` passou a gravar. `Protocolo` ganhou `Status` (`StatusProtocolo`:
Pool/Atribuido/Conferindo/Aprovado/Reprovado/Excecao — seção 8), `DonoId`, `MotivoExcecao` e
comportamento (`AtribuirA`, `EnviarParaPool`, `MarcarExcecao`). `DistribuirProtocolo` grava via nova
porta `IProtocoloRepository` + `IUnitOfWork`. Destravou o RF-27: `MarcarPresenca` (ausente) e
`RemoverConferente` devolvem ao pool os atribuídos (`ObterAtribuidosAAsync`). Testado: atribuiu,
marcou ausente, `psql` confirmou `Pool` com `dono_id` nulo. Resposta ganhou `ProtocoloId`.

## 2026-08-27 — Importação de lote (RF-05 a RF-12)

(`6d7b953`) Decisões: PDF fora do sistema, CSV colado, etapa por lote (ADR-0005); linha de corte no
lugar de dedup por número, `Numero` nunca único (ADR-0006); `Protocolo.AndamentoEm` obrigatório como
referência do prazo, não `IRelogio.Agora` (ADR-0007) — o endpoint avulso continuava usando `Agora`;
`TipoAtoId` vira `Guid?`, tipo desconhecido só sinalizado (ADR-0008, revertido no dia seguinte pelo
ADR-0012); **escrevente desconhecido é criado sem equipe** (confirmado com o dono — RF-09), sinalizado
em `ResumoImportacao.EscreventesSemEquipe`, via nova porta `IEscreventeRepository`, reaproveitando o
padrão D+1 do `ResolvedorDePrazo`.

`ImportarLote` com `PreVisualizarAsync`/`ConfirmarAsync` — mesma lógica, só a confirmação persiste
(RF-11); nada fica guardado entre prévia e confirmação (confirmar reprocessa do zero). A sequência
"resolve prazo → roda motor → aplica" virou `AplicadorDeDistribuicao`, compartilhado com
`DistribuirProtocolo`. Endpoints `POST /protocolos/importar/pre-visualizar` e `/confirmar`
(Distribuidora). Testado com CSV real (10 linhas de Pós-Conferência): prévia não grava; confirmação
grava 10 e cria 8 escreventes sem equipe. Reimportar com corte posterior → 0 processadas; mesmo número
com andamento novo → registro novo (2 linhas para 1 número, de propósito).

**RF-08, prévia por linha** (`4822245`, mesmo dia): `ResumoImportacao.Linhas`
(`IReadOnlyList<LinhaPreviaImportacao>?`, só na prévia — um lote pode ter centenas de linhas). O
cálculo por linha já existia dentro de `ProcessarAsync`; só não sobrevivia ao contador. Equipe vai por
**nome** (o escrevente pode nem existir ainda); `Prazo` vai cru (`TipoPrazo?`); linha antes do corte
(`JaExiste: true`) não resolve nada (campos nulos). `ComAlcada` (RF-10) exigiu `Elegiveis` em
`ResultadoDistribuicao.Atribuido` (mesma forma de Pool/Exceção). Faixas do semáforo entravam como
parâmetro (4h/60min duplicados de `DistribuicaoEndpoints` até a tabela config); `ConfirmarAsync`
passava `TimeSpan.Zero`.

Pendência da época: sem `LoteImportacao` — resolvida no mesmo dia (abaixo).

## 2026-08-27 — Visão de distribuição (RF-13/RF-14)

(`7326e1a`) `GET /protocolos/distribuicao` — três visões da mesma massa numa resposta: `pool`,
`atribuidos`, `emConferencia`, `concluidos`, `excecoes`, `porConferente` (atribuídos + em conferência
por dono). Filtro opcional `?loteImportacaoId=`. **`LoteImportacao`** trazido para agora (Id, Etapa,
LinhaDeCorte, ImportadoEm, TotalLinhas); criado só na confirmação e carimbado em cada protocolo
(`LoteImportacaoId`, nulo fora de importação). `Semaforo.Calcular` ganhou o primeiro consumidor
(faixas hardcoded 4h/60min). Testado: lote com pool + exceção de tipo desconhecido (`tipoAtoId: null`),
filtro por lote, urgente avulso agrupado em `porConferente`. RF-15/16/17 ficaram para a entrega
seguinte.

## 2026-08-27 — RF-15/16/17 — fecha o módulo de Distribuição

(`51e4ce0`)
- **RF-15** `Protocolo.Observacao` livre, editável em qualquer estado; `PUT /protocolos/{id}/observacao`
  (na época só Distribuidora).
- **RF-16** `RedistribuirPool` reaplica o motor a protocolos sem dono (Pool/Exceção; não Descartado),
  sem recalcular prazo; `POST /protocolos/redistribuir-pool` devolve quantos mudaram.
- **RF-17** `POST /protocolos/{id}/atribuir` (manual, só `Excecao`, 409 senão — ampliado em 2026-09-15,
  ADR-0027) e `POST /protocolos/{id}/descartar` (status novo `Descartado`, mantém `MotivoExcecao`).
  "Definir alçada" ficou de fora (dependia de CRUD de regra).

**Bug real**: o endpoint avulso não validava o `TipoAtoId` contra o catálogo — Guid inexistente
quebrava a FK. Os testes com fake não têm FK. Corrigido validando antes. Reforçou "validar contra o
banco real antes de considerar pronto". Testado: observação sobrevivendo a redistribute, exceção
"ninguém com alçada" virando pool após cadastrar conferente, atribuição manual com 409 na segunda
tentativa, descarte preservando o motivo. 67 testes (39 Application).

## 2026-08-27 — Central de Regras — Alçada + Prazos por equipe (RF-31 a RF-38, exceto RF-32/38)

(`6cc3873`) `RegraAlcada` virou `class` com `Origem` (`OrigemRegra`: `Manual`/`Aprendida`) e `Ativar`/`Desativar`
(RF-33); `Equipe` ganhou `Renomear`/`DefinirPrazos`, `Escrevente` ganhou `MoverParaEquipe`.
- `POST/GET/DELETE /regras-alcada`, `/ativar`, `/desativar` (RF-31/33): request achatado
  (`sujeitoNivel` XOR `sujeitoConferenteId`, `alvoEtapa` XOR `alvoTipoAtoId`, 400 senão), valida
  existência das referências.
- `GET /conferentes/alcance` (RF-34): `ResolvedorAlcada` puro por conferente × etapas × catálogo.
- `/equipes`, `/escreventes/sem-equipe`, `/escreventes/{id}/mover` (RF-35 a 37). RF-38 ficou de fora
  (precisava de `Protocolo.EscreventeId`).

Testado: regra negando Júnior em pré refletindo no alcance, desativar devolvendo, remover esvaziando;
ciclo importação → escrevente órfão → mover para equipe nova → some dos órfãos. 83 testes.

## 2026-08-27 — Protocolo.EscreventeId — fecha RF-14 e RF-38

(`422f7ab`) `Protocolo.EscreventeId` obrigatório. **RF-14**: `ProtocoloResumo.EscreventeId`; equipe
cruzada pelo front via novo `GET /escreventes`. **RF-38**: `EditarEquipe` recalcula vencimentos dos
protocolos **abertos** dos escreventes da equipe (`ObterAbertosPorEscreventesAsync` — aberto = não
Aprovado/Reprovado/Descartado, **inclui Exceção**) com `DefinirPrazo(prazoNovo, AndamentoEm)`.

Efeito colateral: o endpoint avulso construía `Escrevente` só em memória — com FK obrigatória
quebraria. Alinhado a `ImportarLote` (busca por nome, cria sem equipe) e `DistribuirProtocoloRequest`
simplificado para `EscreventeNome`. Testado o cenário completo do RF-38 (vencimento recalculado a
partir do `andamento_em` original). 84 testes.

## 2026-08-27 — Minha fila (RF-19 a RF-24)

(`fc6d0d9`) Primeiro módulo do papel Conferente. `Protocolo` ganhou `IniciadoEm`/`ConcluidoEm`
(+`Duracao`) e `IniciarConferencia`/`Aprovar`/`Reprovar` — Domain só faz a transição, o caso de uso
decide se pode.
- `VerificadorDeAlcada` (helper): "esse conferente pode pegar esse protocolo", usado por
  `ObterMinhaFila` (filtra o pool) e `PegarProtocolo` (bloqueia a ação).
- `ObterMinhaFila` (RF-19, três colunas: pool já filtrado pela alçada, atribuídos e em conferência do
  próprio conferente), `PegarProtocolo` (RF-20, só sai do `Pool` se dentro da alçada),
  `IniciarConferencia` (RF-21, só se `Atribuido` e o dono bater; limite de simultâneos hardcoded 1),
  `ConcluirConferencia` (RF-22, aprova/reprova, só se `Conferindo` e o dono bater; grava
  `ConcluidoEm`), `ObterConcluidosHoje` (RF-24, "hoje" por `IRelogio`, nunca hardcoded),
  `DefinirObservacao` (RF-15/23) com `conferenteRestritoId` opcional — o mesmo caso de uso serve
  Distribuidora e dono — e enum `Sucesso`/`NaoEncontrado`/`NaoEhSeu` (três desfechos, não `bool`).
- `ClaimsPrincipal` como parâmetro de endpoint pela primeira vez; `IConferenteRepository.ObterPorUsuarioIdAsync`.
  `PUT /protocolos/{id}/observacao` aceita os dois papéis e restringe por dentro.
- `/minha-fila` (`RequireRole(Conferente)`, primeiro grupo exclusivo desse papel): `GET /`,
  `POST /{id}/pegar`, `/{id}/iniciar`, `/{id}/concluir` `{aprovado}`, `GET /concluidos-hoje`.

Testado: pegar do pool; Distribuidora e dono editando observação; segundo conferente com 403 (valor
intacto no `psql`); segundo `iniciar` com 409; `Duracao` em concluídos-hoje. 103 testes (28 Domain +
75 Application).

## 2026-08-27 — Aprendizado sem IA (RF-39 a RF-41)

(`ee902fd`) Sem tabela `evento_decisao` (ADR-0009); `Protocolo.TipoAtoNomeOriginal`.
`Dispatch.Domain/Aprendizado/`: `PayloadSugestao` (4 variantes), `Sugestao` (`Chave`, `DescartarAte`),
`GeradorDeSugestoes` (4 funções puras, limiares default do documento; `PrazoIrreal` mapeia o p80 para a
faixa mais próxima com durações típicas 1h/12h/36h/60h — aproximação consciente). Casos de uso:
`GerarSugestoes` (`POST /sugestoes/gerar`, sob demanda — sem scheduler), `AplicarSugestao` (tipo →
catálogo, primeira escrita em `ITipoAtoRepository`; prazo → `DefinirPrazos` + `RecalculoDeVencimentos`,
extraído de `EditarEquipe`; órfão → `MoverParaEquipe`; risco → `RegraAlcada` `Origem.Aprendida`),
`DescartarSugestao` (30 dias hardcoded), `ListarSugestoesPendentes`, `ListarHistoricoSugestoes`.

**Bug real (change tracker)**: `SugestaoRegistro` traduzido a cada `ObterPorIdAsync` — `Aplicar()`
mutava a cópia, status ficava `Pendente` para sempre (aplicar duas vezes → 204 duas vezes). Mesmo
padrão que `RegraAlcadaRepository` já evitava (`AtivarAsync`/`DesativarAsync`). Corrigido com
`AtualizarEvidenciaAsync`/`AplicarAsync`/`DescartarAsync` no repositório. Lição: fakes não têm change
tracker (ver `docs/patterns/ef-core.md`).

Testado os quatro caminhos (tipo desconhecido gerado → aplicado → 409 ao reaplicar; risco de qualidade
67% → regra aprendida refletindo no alcance; descarte com `descartarAte` +30 dias, não volta ao gerar,
descartar de novo 404). 128 testes (40 Domain + 88 Application).

## 2026-08-27 — GET /conferentes — fecha o gap encontrado planejando o front

(`6c038f2`) Nenhuma leitura juntava `Conferente` com `Usuario.Nome`/`Email`. `ListarConferentes`
junta em memória com `IUsuarioRepository.ObterVariosPorIdsAsync` (evita N+1), devolve
`ConferenteComUsuario` (record de primitivos). `GET /conferentes` (Distribuidora). 130 testes.

## 2026-08-27 — Login devolve o usuário + GET /auth/me

(`c4c9d53`) ADR-0010. `POST /auth/login` → `{ token, usuario: { id, nome, email, papel } }` (hoje
`papeis`); `GET /auth/me` via `ObterUsuarioAtual` (pass-through). Testado para os dois papéis e 401
sem token. 132 testes (40 Domain + 92 Application).

## 2026-08-27 — CORS pro dispatch-web

(`3c569f4`) Nenhum teste via `curl` esbarrou em CORS (curl não faz preflight). `AddCors`/`UseCors`
só em Development liberando `http://localhost:5173`. No dia seguinte virou policy sempre ativa com
origem em config (ver deploy).

## 2026-08-27 — ProtocoloResumo ganha IniciadoEm — fecha o gap do cronômetro (RF-21)

(`f5709c6`) `ProtocoloResumo` (compartilhado por distribuição e Minha fila) não expunha `IniciadoEm`;
sem ele o front não calculava o cronômetro sem inventar dado. Campo no fim do record; tempo decorrido
calculado no front.

## 2026-08-28 — RF-28 (carga real) e RF-30 (aviso de cobertura) — planejando a tela Conferentes do front

(`3ab0618`) **Bug**: `Conferente.CargaAtual` nunca era atualizada (sempre 0) — o desempate do motor
por carga (`elegiveis.OrderBy(a => a.Conferente.CargaAtual).First()`) nunca funcionou. **Correção**: carga recalculada na leitura por subquery correlacionada em
`ObterTodosAsync`/`ObterNaEscalaAsync` (ADR-0011); `ObterPorIdAsync` segue simples (projeção
desconecta do change tracker). Testado: atribuiu um protocolo, `cargaAtual: 1` sem escrita na tabela.
**RF-28**: `CapacidadeEstimada = jornadaHoras × 60 ÷ 18min` (premissa da seção 11), arredondado,
mínimo 1. **RF-30**: `ObterCoberturaDeAlcada` — tipos em circulação (presentes nos protocolos de
hoje) sem ninguém `NaEscala` habilitado ou dependentes de uma pessoa; reaproveita
`ObterAlcancePorConferente` (`TiposPermitidosIds`, só eixo tipo, mesma simplificação do protótipo);
tipo nulo não entra. Devolve `SemNinguemHabilitado`/`DependeDeUmaPessoa` em `GET /conferentes/cobertura`.
7 testes (`ObterCoberturaDeAlcadaTests` + 2 em `ListarConferentesTests`). 143 testes (103 Application).

## 2026-08-28 — Conferentes, ajustes achados testando a tela de verdade (RF-25)

(`b11ba33`, `abbfd92`) Três achados só no navegador: `GET /conferentes` sem `ORDER BY` (lista pulava a
cada refetch) → ordena por nome, depois `ThenBy(Id)` porque nomes empatam; removidos continuavam
aparecendo → filtro de `Ativo` em `ListarConferentes` (na fonte, não em cada tela); faltava editar
nome/e-mail → `EditarPerfilConferente` separado de `EditarNivelEJornada` (agregados diferentes),
`Usuario.AtualizarPerfil(nome, email)`, unicidade só quando o e-mail muda de verdade (`ExisteComEmailAsync`
sozinho rejeitaria a pessoa contra o próprio e-mail), `PUT /conferentes/{id}/perfil`
204/404/409. 4 testes (`EditarPerfilConferenteTests`). 149 testes.

## 2026-08-28 — GET /conferentes/{id}/fila e /concluidos-hoje — Distribuidora vendo a fila de alguém (RF-19)

(`0638add`) O protótipo mostra "Minha fila" para gestão também (o nav usa `soGestao || !d[3]`;
"Ver como outro conferente"). Nenhum caso de uso novo — `ObterMinhaFila`/`ObterConcluidosHoje` já
recebiam um `Conferente` (resolver "quem está logado" sempre foi do endpoint, `ResolverConferenteAsync`
em `MinhaFilaEndpoints.cs`). Dois endpoints em `ConferenteEndpoints.cs` resolvendo pelo id da URL;
`ParaResumo`/`ParaResumoConcluido` viraram `internal`. Testado 404/403/200.

## 2026-08-28 — Deploy em produção: Fly.io + Neon *(reconstituído do commit e da seção "Deploy — no ar")*

(`519f9ef`) ADR-0014. `fly.toml` (`min_machines_running = 0`), secrets via `fly secrets`, CORS vira
policy única sempre ativa com `Cors:AllowedOrigin`, primeira Distribuidora de produção criada direto
no banco.

## 2026-08-28 — Swagger liberado em produção *(reconstituído do commit)*

(`b6a63f5`) Swagger UI também em produção — projeto pessoal, útil para testar sem o front; usar os
endpoints continua exigindo token.

## 2026-08-28 — GET /tipos-ato *(reconstituído do commit)*

(`5a98e98`) `ListarTiposAto` + `GET /tipos-ato` — o front precisava resolver nome de tipo para alvo
de regra (RF-31) e construtor guiado (RF-32).

## 2026-08-28 — Cadastro automático de tipo de ato na importação + normalização de texto *(reconstituído do commit)*

(`e6c36da`) ADR-0012 (substitui ADR-0008). `ImportarLote` cadastra tipo novo com nome normalizado;
`NormalizadorDeTexto` (Domain) para escrevente e tipo (CAIXA ALTA → capitalização normal, conectivos
minúsculos exceto na primeira palavra); `POST /tipos-ato` (`CriarTipoAto`, 409 duplicado). Testes
`CriarTipoAtoTests`, `NormalizadorDeTextoTests`, `ImportarLoteTests` ajustado.

## 2026-08-28 — Correção da regra de prazo (D+1 = 24h, D+2 = 48h, dia útil)

(`20b28be`) ADR-0013 — "regra confirmada com a operação", fechando o ponto em aberto da seção 11.

## 2026-08-28 — Painel de detalhe do protocolo (RF-18a/b) — v2 do protótipo/requisitos

(`996d7f3`) Primeira frente da "v2" do protótipo/requisitos. Três gaps reais:
- `Protocolo.AtribuidoEm`: `AtribuirA(agora)` — todo call site ganhou `IRelogio` (`DistribuirProtocolo`,
  `RedistribuirPool`, `PegarProtocolo`, `AtribuirManualmente`). Não limpa ao voltar pro pool (fica
  como "última atribuição" — não há log de eventos).
- **RNF-02 até o fim**: `Protocolo.RegraAplicadaId` (nullable, **sem FK** — auditoria, não
  dependência). Quando etapa e tipo decidem por regras diferentes, guardava a de tipo. Só em atribuição
  automática (`AplicadorDeDistribuicao`); atribuição humana deixa nulo.
- "Quem pode conferir este protocolo": `ObterDetalheProtocolo` reaproveitando `ResolvedorAlcada` por
  conferente na escala.

Ações novas: `POST /protocolos/{id}/devolver-ao-pool` (só `Atribuido`) e
`POST /protocolos/{id}/atribuir-ao-menos-carregado` (Pool ou Exceção, `VerificadorDeAlcada`, menor
carga). Migration `AdicionaAtribuidoEmERegraAplicadaEmProtocolos` (sem backfill). Testado: grava
`atribuidoEm`, 409 sem alçada, devolver preserva `atribuidoEm`. 172 testes.

## 2026-08-28 — Tipos de ato — CRUD completo (RF-34a, b, d, e, f) — segunda frente do "v2"

(`f3f55b9`) `TipoAto` virou `class` com `PesoComplexidade` (mínimo 1) e `Renomear`/`Ativar`/`Desativar`/
`DefinirPesoDeComplexidade`. Mapeamento **direto** (rastreado; mutar e salvar basta).
- RF-34d: segundo motivo de exceção do motor, `"tipo desativado"`.
- RF-34e: **exclusão de verdade** só sem uso — `TipoAtoId` não tem FK em protocolo nem em regra, então
  `RemoverTipoAto` checa `ExisteComTipoAtoAsync` e as regras. RF-34c (mesclar) ficou de fora.
- RF-34a: `ListarTiposAtoComUso` (conferentes na escala com alçada × contagem de protocolos) — leitura
  derivada.

Endpoints (Distribuidora): `GET /tipos-ato/com-uso`, `PUT /tipos-ato/{id}` (409 nome existente),
`PUT /{id}/peso`, `POST /{id}/ativar|desativar`, `DELETE /{id}` (409 "tipo de ato em uso...").
Migration `AdicionaPesoDeComplexidadeEmTiposAto` (`DEFAULT 1`, não 0). 191 testes (50 Domain + 141
Application).

## 2026-08-31 — Correção de resultado + pedido de reabertura (RF-24a-d) — terceira frente do "v2"

(`d777941`) ADR-0016. `Protocolo.CorrigidoEm`/`ReabertoEm`; `CorrigirResultado(agora)` (inverte
Aprovado↔Reprovado, repetível dentro da janela) e `ReabrirConferencia(agora)` (na época: `Conferindo`,
`IniciadoEm = agora`, `ConcluidoEm` nulo, mesmo dono — revisto em 2026-09-16). Entidade
`PedidoReabertura` (primeiro registro de auditoria). Casos de uso com `abstract record ResultadoX`:
`CorrigirResultado` (só o dono, só Aprovado/Reprovado, `JanelaDeCorrecao = 15min` como constante
pública), `PedirReabertura` (só o dono, um `Pendente` por protocolo, não checa a janela de propósito),
`CancelarPedidoReabertura` (só o solicitante, só `Pendente`), `DecidirPedidoReabertura` (aprovar chama
`ReabrirConferencia` + marca `Aprovado`; negar só marca `Negado`), `ReabrirConferencia` (ação direta
para qualquer Aprovado/Reprovado, usada também pelo painel), `ListarPedidosReaberturaPendentes` (join
em memória com o nome do solicitante).

Endpoints: `POST /minha-fila/{id}/corrigir-resultado`, `/{id}/pedir-reabertura` (201 com `pedidoId`),
`/pedidos-reabertura/{id}/cancelar` (Conferente); `GET /protocolos/pedidos-reabertura`,
`POST /protocolos/pedidos-reabertura/{id}/aprovar|negar`, `POST /protocolos/{id}/reabrir-conferencia`
(Distribuidora). `ProtocoloConcluidoResumo` ganhou `CorrigidoEm`/`PedidoReaberturaPendenteId`
(em lote, `ObterPendentesPorProtocolosAsync`); `DetalheProtocoloResponse` ganhou `CorrigidoEm`/`ReabertoEm`.

**Bug real**: `Dictionary<Guid, Guid>.GetValueOrDefault` devolvia `Guid.Empty` em vez de `null` —
tipado como `Dictionary<Guid, Guid?>`. Migration `AdicionaCorrecaoEReaberturaDeProtocolos` (tabela
`pedidos_reabertura`, FK `Restrict` para `conferentes` via `solicitante_id`, `Cascade` para `protocolos`
via `protocolo_id`). Testado o ciclo completo (corrigir, pedir, 409 duplicado,
cancelar, pedir de novo, aprovar, reabrir direto, 409 reabrir não concluído, negar). 219 testes (54
Domain + 165 Application).

## 2026-08-31 — Dashboard (RF-42-46) — quarta frente do "v2"

(`c0abd67`) `ObterDashboard.ExecutarAsync(PeriodoDashboard, Guid? conferenteRestritoId)`; janela móvel
7/30/90 dias; `ObterConcluidosNoPeriodoAsync(desde, ate, ct)` (todos os donos — diferente de
`ObterConcluidosPorConferenteAsync`, do RF-24). Score pela fórmula do protótipo,
faixas 85/70; parcelas ponderadas; KPIs; desempenho por tipo. Simplificações conscientes: "aprovado" =
resultado atual; sem "custo por ato"; cumprimento por equipe/etapa ficou para 2026-09-01. RF-45: linha
própria + "média da casa" sem nome, `Faixa` nula nas duas, `PorTipoAto` vazio. Endpoint único
`GET /dashboard?periodo=`, os dois papéis. Detalhes em `docs/patterns/indicadores-e-aprendizado.md`.

Achado escrevendo teste: helper com `TipoPrazo.D2` caía no ajuste de dia útil perto do fim de semana;
trocado por `UmaHora`. Testado com dado acumulado local (2 conferentes, os 3 períodos). 225 testes (54
Domain + 171 Application).

## 2026-08-31 — Deploy — no ar (migração Fly.io → Render)

(`dacc899`, `c589aa6`) ADR-0015. `https://lab-dispatch-api.onrender.com`, Web Service Docker free;
nada de código mudou (mesmo Dockerfile, Neon, Netlify). `render.yaml` com secretas `sync: false`.
Gotcha de `PORT=8080` (`x-render-routing: no-server` com a app "live"); roteamento pode demorar
alguns minutos após o primeiro deploy. Hiberna por inatividade. CORS por `Cors__AllowedOrigin`. Sem
registro público — a primeira Distribuidora (feita antes da migração) sobreviveu porque o Neon não
mudou. `Jwt__ChaveDeAssinatura` nova (sessões antigas invalidadas). App do Fly parado, não excluído.
Procedimento e env vars atuais em `docs/patterns/deploy.md`. Auto-deploy no push em `main` confirmado
em 2026-09-17 (`957222f`).

## 2026-08-31 — Índice de confiança real da sugestão (RF-39-41) — fecha a simplificação consciente do módulo de Aprendizado

(`888a4a6`) Nem requisito nem protótipo definem a fórmula (protótipo usa número mockado). Decidido
(`CandidatoSugestao.cs`): a proporção que cada função já calculava para o limiar — força da moda
(`Moda`/`ModaGuid` viraram `ModaComForca`/`ModaGuidComForca`), `percentualEstouro`, dominância da
equipe, `percentualReprovacao`, todas em [0,1]. `Sugestao.IndiceConfianca` recalculado junto com
`Ocorrencias`/`Evidencia` (`AtualizarEvidencia`/`AtualizarEvidenciaAsync`). Migration
`AdicionaIndiceConfiancaEmSugestoes` (`double precision NOT NULL DEFAULT 0`). `SugestaoResponse.IndiceConfianca`
0.0–1.0 (front mostra %). Cobertura como asserções novas em testes existentes. 225 testes.

## 2026-08-31 — Motor de alçada v2 — lista fechada por dimensão, equipe, alçada plena, grupo de tipo

(`a3a3cca`) ADR-0017. Bug de produção (Permite sem efeito) confirmado ao vivo no simulador do
protótipo. Algoritmo por família de alvo em 5 passos; `AlvoAlcada.PorEquipeDeEscrevente(Guid?)` e
`PorTodosOsAtos`; `AvaliacaoCandidato.DecisaoEquipe`; `MotorDistribuicao.Distribuir(..., Guid?
equipeDoEscreventeId = null)`. Persistência com discriminador `AlvoTipoRegistro`; migration
`AdicionaEquipeETodosOsAtosEmRegrasAlcadaEGrupoEmTiposAto` com **backfill manual** antes de recriar o
`CHECK`, validada contra clone de produção (`pg_dump` via container `postgres:18` — cliente local 17.x,
Neon 18.x). `TipoAto.Grupo` (`GrupoTipoAto?`: Transmissões/Sucessões/Família/Garantias/Notariais) +
`DefinirGrupoDoTipoAto`/`PUT /tipos-ato/{id}/grupo` (inventado, sem tela de gestão no protótipo).
RF-33 "usos" via `ContarComRegraAplicadaAsync`. **Carga acumulada na rodada**:
`Conferente.IncrementarCargaAtual()` em memória, chamado por `AplicadorDeDistribuicao` e `RedistribuirPool`.

**Bug de composition root**: `DefinirGrupoDoTipoAto` não registrado — build/test verdes, `dotnet run`
com `Failure to infer one or more parameters`. Lição: sempre `dotnet run` após caso de uso novo.
Escopo: back + extensão mínima do construtor de regra do front (`AbaAlcada.tsx`); redesign
Camadas/Matriz/Testar à parte. Testado contra clone de produção (lista fechada por nível: Júnior 1 tipo,
Sênior 3; regras de equipe, sem-equipe e alçada plena persistindo). 236 testes (60 Domain + 176
Application).

## 2026-09-01 — Distribuição/Minha fila v2 (prioridade manual, RF-14/16/18c/18e/24f) — quinta frente do "v2"

(`05aad93`) Só prioridade manual mexeu em domínio (RF-14/16/18c já suportados; RF-18e/24f 100%
client-side — ver `../dispatch-web/CLAUDE.md`). Nenhum caminho real definia `Prioridade.Alta` (só o
endpoint avulso; o relatório não traz). Virou ação manual: `Protocolo.DefinirPrioridade`,
`DefinirPrioridadeDoProtocolo`, `POST /protocolos/{id}/definir-prioridade` (Distribuidora, 204/404);
`ProtocoloResumo.Prioridade`.

**Bug de autorização achado no Playwright**: `GET /equipes`, `/escreventes`, `/tipos-ato` eram só
Distribuidora; o filtro de Minha fila (Conferente) recebia 403 e todo protocolo caía em "sem equipe" —
filtrar por equipe real zerava a lista. Só uma asserção de comportamento (contagem antes/depois) pegou.
Correção: as três leituras aceitam os dois papéis; mutações e `sem-equipe` seguem Distribuidora.
**Gotcha de minimal API**: `RequireAuthorization` na rota combina com E com o do `MapGroup` — os grupos
de `EquipeEndpoints.cs` passaram a declarar policy por rota. Testado 200/403 corretos. 239 testes (60
Domain + 179 Application).

## 2026-09-01 — Motor de alçada v3 — cascata de camadas, reserva, grupo como alvo

(`14aa605`) ADR-0018. Divergência achada lendo a lógica-fonte da ferramenta interativa
(`bloqueioPuro`/`decideCamada`/`camadaDe`/`trilhaPura`) e ao vivo via Playwright; documento formal não
editado. Cascata de 3 camadas contra o caso inteiro; `PermissaoRegra.Reserva` antes de tudo;
`AlvoAlcada.PorGrupoTipoAto`. API do resolvedor: `CasoAlcada(Etapa, TipoAto, Guid? EquipeId)`,
`Resolver` → `DecisaoAlcada` (com `Motivo` enum sem nome próprio), `Explicar` → `PassoTrilha[]`.
`AvaliacaoCandidato(Conferente, DecisaoAlcada)`; `RegraAplicadaDe` colapsou para
`avaliacao.Decisao.RegraAplicada?.Id`. `ObterAlcancePorConferente` com caso representativo
(aproximação documentada). `SimularAlcada` + `POST /regras-alcada/testar` (`{etapa, tipoAtoId,
equipeId?}`, 404 tipo inexistente); `ObterDetalheProtocolo` passa a usar `Explicar`. Persistência:
`AlvoGrupoTipoAto`, discriminador `Grupo`, sem backfill; `Reserva` sem schema (enum string). Migration
`AdicionaGrupoEmRegrasAlcada`. Request/response com `AlvoGrupo`; XOR com 5 campos. Blast radius:
`PegarProtocolo`, `ObterMinhaFila`, `AtribuirAoMenosCarregado`, `ObterDetalheProtocolo` ganharam
`ITipoAtoRepository`. Testado: trilha por camada no `/testar`, regra de grupo e de reserva persistindo,
XOR com 400. 244 testes (64 Domain + 180 Application). Front (Camadas/Matriz/Testar) na frente seguinte.

## 2026-09-01 — Pool ordenado por vencimento (Distribuição e Minha fila)

(`a718851`) Pedido do dono. `ObterPoolAsync`/`ObterParaDistribuicaoAsync` não tinham `ORDER BY`.
`ObterVisaoDistribuicao` e `ObterMinhaFila` ordenam o pool por `VencimentoEm ?? DateTimeOffset.MaxValue`.
Escopo restrito ao pool. 246 testes.

## 2026-09-01 — Cumprimento de prazo por equipe — fecha metade do gap do RF-43

(`e831785`) `ObterDashboard` ganhou `IEscreventeRepository`/`IEquipeRepository`; `CalcularCumprimentoPrazoPorEquipe`
agrupa por `(EquipeId, Etapa)`, "sem equipe" como grupo próprio, reaproveita `EstaNoPrazo`, ordena pelo pior
percentual (igual ao `slaEquipes.sort` do `Dispatch.dc.html`). Record `CumprimentoPrazoEquipe(EquipeId,
EquipeNome, Etapa, Prazo, Total, PercentualNoPrazo)`; vazio na visão restrita. Verificado com
`dotnet run` (resposta vazia coerente no banco local). Teste
`CumprimentoPrazoEquipe_AgrupaPorEquipeEEtapa_PiorPercentualPrimeiro`. 247 testes (64 Domain + 183 Application).

## 2026-09-01 — Protocolo manual — criar, editar, excluir com desfazer (RF-18f a RF-18j)

(`3136f93`) `TipoAtoId`/`EscreventeId`/`Etapa` viraram `private set` + `EditarDadosBasicos`. Exclusão
soft-delete com `StatusAntesDeExcluir` (ADR-0019); auditoria prévia dos filtros de status
(`ObterParaDistribuicaoAsync` sem filtro; `ObterAbertosPorEscreventesAsync` precisou de `!= Excluido`),
ambos com teste (`ProtocoloExcluido_NaoAparecePraDistribuicao`,
`ProtocoloExcluido_NaoEntraNoRecalculoDeVencimentosAbertos`). `ResolvedorDeEscreventePorNome` extraído do endpoint avulso (na época `public`;
voltou a `internal` em 2026-09-03) — escrevente novo passou a ser normalizado. Casos de uso:
`SimularProtocoloManual` (`POST /protocolos/manual/simular`, nada persiste), `CriarProtocoloManual`
(`POST /protocolos/manual`, 201/409 por número duplicado via `ExisteComNumeroAsync` — substituído por
`PodeRecriar` em 2026-09-03), `EditarProtocoloManual` (`PUT /protocolos/{id}`, `identidadeMudou` compara
tipo/escrevente/etapa; só então `ResolvedorDePrazo` + `DefinirPrazo(prazo, AndamentoEm)`; prioridade
e observação sempre aplicadas, nunca disparam recálculo; RF-18h: identidade mudou e tem dono →
`VerificadorDeAlcada.TemAlcada` decide se volta pro pool; sem tipo conhecido, pula essa checagem), `ExcluirProtocolo`/`RestaurarProtocolo` (`DELETE /protocolos/{id}`,
`POST /protocolos/{id}/restaurar`). Migration `AdicionaProtocoloManualEExclusao`. Testado ponta a ponta.
268 testes. Segunda passada relendo o protótipo (`novoAberto`, linhas ~1739-1808): `CriarProtocoloManual`
passou a aceitar `observacao` (via `Protocolo.DefinirObservacao`; teste `ComObservacao_GravaJuntoNaCriacao`).
269 testes (67 Domain + 202 Application).

## 2026-09-01 — TOTP e recuperação de senha, caminho feliz (RF-01a a RF-01l)

(`226fb7d`) ADR-0020. `TotpComOtpNet` (Otp.NET, ±30s, anti-reuso), `UsuarioTotp` (segredo cifrado com
`CifradorAes`/`Totp:ChaveDeCifragem`, 5 tentativas → 15 min, hash do token), token de recuperação
opaco, anti-enumeração, `SessoesValidasApartirDe` + `OnTokenValidated` (primeira customização da
validação do JWT), atos em conferência voltam ao pool, `RegrasDeSenha`.

**Dois bugs só com `dotnet run` + curl**: cast para `JwtSecurityToken` dentro de `OnTokenValidated`
(ASP.NET Core 10 usa `JsonWebToken` — `InvalidCastException` em runtime); `EmissorDeTokenJwt` sem claim
`iat` — só apareceu quando a troca de senha fez `auth.spec.ts` e mais 7 specs do front falharem (todo
token novo rejeitado); corrigido com `JwtRegisteredClaimNames.Iat` explícito e carimbo truncado ao
segundo.

Endpoints: `POST /auth/totp/registrar`, `/auth/totp/confirmar`, `/auth/recuperar/iniciar`,
`/validar-codigo`, `/redefinir-senha`. Fora de escopo por decisão do dono: RF-01m/RF-01n. Testado com
TOTP calculado em Python a partir do segredo Base32 (fluxo completo, token de uso único, sessão antiga
401, senha antiga falha). 38 testes novos (`UsuarioTests`, `UsuarioTotpTests`, `RegrasDeSenhaTests` + 31 de aplicação). E2E do
front 100% nos specs de autenticação (8 falhas restantes em áreas não
tocadas = deriva de dado local). 305 testes. Front na frente seguinte.

## 2026-09-02 — Prioridade com 3 níveis (Baixa/Média/Alta)

(`41cecea`) ADR-0021. `Prioridade.Baixa` sem migration; `Normal` mantido (rótulo "Média" só no front).
Teste `ProtocoloTests.PrazoD1OuD2ComPrioridadeBaixa_NaoEhUrgente`. 307 testes.

## 2026-09-02 — Fix: simulador "Testar" da aba Alçada agora roda o motor de verdade

(`84acfff`) Achado numa auditoria do front: o destino no "Testar" (`AbaAlcadaTestar.tsx`) era inferido
pela contagem de elegíveis — errado quando urgência importa (`MotorDistribuicao.cs:34-46` decide
primeiro por `Protocolo.Urgente`). `SimularAlcada.ExecutarAsync` ganhou `Prioridade`, monta
`Protocolo`+`Escrevente` transitórios e chama `AplicadorDeDistribuicao.Executar`;
`ResultadoSimulacaoAlcada` ganhou `Destino`/`ConferenteId`/`Motivo`; `TestarAlcadaRequest`/`Response`
acompanharam. `SimularAlcadaTests.cs` novo (4 testes). 311 testes.

## 2026-09-03 — Auditoria de qualidade do back (endpoints + casos de uso)

(`81115b9`) 2 agentes em paralelo por cluster + verificação pessoal; 14 achados, corrigidos em fases
com `dotnet build && dotnet test` (e `dotnet run` nas fases de `Program.cs`/DI/rota).
- **Bug**: `DecidirPedidoReabertura` aprovava sem checar o status do protocolo — reabria protocolo
  excluído por baixo do `RestaurarProtocolo`. Guarda `StatusInvalido` (409) + teste.
- `MotorDistribuicao.EscolherMenosCarregado<T>` reaproveitado por `AtribuirAoMenosCarregado`.
- **Removido `POST /protocolos/distribuir`** (zero consumidor no front); `DistribuirProtocolo` e
  `DistribuirProtocoloResponse` ficam (usados por `CriarProtocoloManual`); `ResolvedorDeEscreventePorNome`
  voltou a `internal`.
- `RegraAlcadaEndpoints`: validação em `TentarMontarSujeito`/`TentarMontarAlvo` com array único
  `(bool, Func<AlvoAlcada>)`.
- 404 com `{ motivo }` em `DescartarExcecao`/`DefinirPrioridadeDoProtocolo`.
- `ClaimsPrincipalExtensions.ObterUsuarioId()` (8 lugares).
- `ParaResumo` sem cópia em `DistribuicaoEndpoints`.
- `AuthEndpoints.cs` dividido em `AuthEndpoints`/`TotpEndpoints`/`RecuperacaoSenhaEndpoints`.
- `ModaComForca<T>` genérico; `CamadasComOpiniao` unificando o laço de camadas de `Resolver`/`Explicar`
  (prelúdio de reserva deliberadamente fora); suíte inteira rodada antes de seguir.
- N+1 em `ListarPedidosReaberturaPendentes` → `IProtocoloRepository.ObterVariosPorIdsAsync`.
- `ImportarLote`: buscas O(n×m) → `Dictionary` por nome normalizado.
- `ServiceCollectionExtensions.cs`: ~65 registros agrupados por endpoint consumidor.

312 testes; smoke via curl em cada fase; e2e do front sem regressão nova.

## 2026-09-03 — Continuidade de conferência (pedido do dono, não é RF numerado)

(`2bb189c`) ADR-0022. `ResolvedorDeContinuidade` (Domain), `MotorDistribuicao.Distribuir(...,
donoDaPrimeiraConferenciaId)`, `IProtocoloRepository.ObterPorNumerosAsync`,
`ObterDetalheProtocolo.HistoricoConferencias` (em `GET /protocolos/{id}/detalhe`,
`HistoricoConferenciaResponse` cru). Front: seção "HISTÓRICO DE CONFERÊNCIAS" em
`PainelDetalheProtocolo.tsx`, no padrão visual de `ListaAlcada`/`LINHA DO TEMPO`. Testado: reimportação atribuída direto ao mesmo conferente,
`regraAplicadaId: null`, histórico visível; e2e com as mesmas 8 falhas pré-existentes. 322 testes.
**Estendido ao cadastro manual**: `ResolvedorDeContinuidade.PodeRecriar` (bloqueia só se algum registro
do número está em uso), `SimularProtocoloManual` acompanha, `ExisteComNumeroAsync` removido. Testado:
Atribuído → 409; reprovado → 201 atribuído ao mesmo dono; simulador confirma. 335 testes.

## 2026-09-03 — Tabela `config` (seção 8) — fecha o item do backlog

(`2bb189c`) ADR-0023. `Configuracao` (12 campos), `GET`/`PUT /config`, `ObterConfiguracao` usado pelos
endpoints que montam `ProtocoloResumo`/`DetalheProtocoloResponse` (`/protocolos/{id}/detalhe`,
`/protocolos/distribuicao`, `/minha-fila/`, `/conferentes/{id}/fila`,
`/protocolos/importar/pre-visualizar`) — os `static readonly` duplicados saíram. `IniciarConferencia`,
`CorrigirResultado`, `DescartarSugestao`, `ListarConferentes`, `GerarSugestoes` injetam
`IConfiguracaoRepository`. Migration `AdicionaConfiguracao` com `InsertData`
(4h/60min/1/15min/30dias/18min/5/8/0.6/3/6/0.5). Testado: `limiteDeAtosSimultaneos: 2` mudando o
comportamento sem reiniciar; 400 para valor inválido. 341 testes.

## 2026-09-04 — "N feitos hoje" + tempo de conferência — fecha o gap do card de conferente em Distribuição

(`ba9a249`) `ProtocoloResumo` ganhou `ConcluidoEm`/`Duracao` (no fim); `ObterVisaoDistribuicao` ganhou
`IRelogio` e `ConcluidosHojePorConferente` (`(ConferenteId, Total)`, quem não concluiu nada hoje não
aparece — front trata como 0). `VisaoDistribuicaoResponse` acompanha. 2 testes. 343 testes. Testado com
fluxo real (criar, pegar, iniciar, concluir). Front (`AbaPorConferente`, `DistribuicaoProtocoloCard`) em `../dispatch-web/CLAUDE.md`.

## 2026-09-11 — Motor de alçada v4 — equipe inteira não passa por uma etapa

(`53811b2`) ADR-0024. Pedido de um conferente em produção ("Quinto Andar não passa por
pré-conferência"). `AlvoAlcada.PorEquipeEEtapa(Guid? EquipeId, Etapa Etapa)`, só `Nega` (400 com
`Permite`/`Reserva`); `Dimensao.EquipeEEtapa` fora de `OrdemDasDimensoes`; `AlvoBate` compara os dois
campos; `CamadaDe` trata como `Camada.Equipe` para sujeito pessoa; `MotivoAlcada.EquipeEEtapa` (via `MotivoDaDimensao`). 5 testes
em `ResolvedorAlcadaTests.cs`. Persistência reaproveita `alvo_etapa`/`alvo_equipe_id` (branch novo no
`CHECK`: `alvo_tipo = 'EquipeEEtapa' AND alvo_etapa IS NOT NULL AND alvo_tipo_ato_id IS NULL AND
alvo_grupo_tipo_ato IS NULL`); migration `AdicionaAlvoEquipeEEtapaEmRegrasAlcada` só
`DropCheckConstraint`/`AddCheckConstraint`. Api: `AlvoEhEquipeEEtapa` reaproveitando `AlvoEtapa`/
`AlvoEquipeId`; dois cuidados achados em revisão (o candidato `PorEtapa` precisa excluir o novo; o
novo exige `AlvoEtapa is not null` na condição, senão 500).

**Bug achado só pelo front (Playwright)**: `RegraAlcadaEndpoints.ParaResponse` usava
`(regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa` — para o alvo novo saía `null` com a linha certa no banco;
a frase virava "fazer undefined da equipe X". O smoke com curl só conferiu presença do campo; o
`/testar` lê das colunas, não da resposta. Corrigido com `switch`. Lição em `docs/patterns/endpoints.md`.
Front: construtor guiado com alvo "equipe não faz etapa…", travando em Nega. Testado 400/201 e,
depois do fix, UI ↔ consulta ↔ "Testar" batendo. 348 testes (108 Domain + 240 Application).

## 2026-09-11 — Fix de performance: N+1 em GET /regras-alcada

(`63a6975`) Reportado pelo dono ("Central de Regras demorando"). `MapGet("/")` chamava
`ContarComRegraAplicadaAsync` por regra — ~96 round-trips sequenciais contra o Neon com ~95 regras; e
`RegraAplicadaId` sem FK não tinha índice (seq scan a cada `COUNT`). Fix: `ContarPorRegraAplicadaAsync`
(`GroupBy` + `Count`, uma query) → `Dictionary` + `GetValueOrDefault`; `HasIndex(p => p.RegraAplicadaId)`
(migration `AdicionaIndiceEmRegraAplicadaIdDeProtocolos`). Contrato JSON inalterado. 348 testes.

## 2026-09-11 — Sessão de 8 horas (Jwt:ExpiracaoMinutos)

(`63a6975`) "O token tá expirando muito rápido." O front não tem refresh (qualquer 401 limpa a sessão),
então expiração = tempo de sessão. Decidido: 480 min. `appsettings.Development.json` 60 → 480.
Pendente fora do código: `Jwt__ExpiracaoMinutos=480` no dashboard do Render (o `render.yaml` foi
corrigido em 2026-09-14).

## 2026-09-14 — Auditoria de performance/índices do banco — três correções

(`cb536bb`) Pedido explícito do dono, agente em background. Índice em `protocolos.status` (caminho mais
quente: `ObterPoolAsync` → `GET /minha-fila`); índice **não único** em `protocolos.numero`
(continuidade a cada importação, histórico a cada detalhe); `EnableRetryOnFailure()` no `UseNpgsql`
(Neon hiberna; confirmado antes que não há `BeginTransactionAsync`). Migration
`AdicionaIndicesEmStatusENumeroDeProtocolos`. Verificado com `dotnet run` (leitura e escrita com retry).
Registrado sem corrigir na época: `/protocolos/distribuicao` sem paginação (tratado em 2026-09-14,
ADR-0026).

## 2026-09-14 — Segunda rodada da auditoria — os 4 itens restantes, todos implementados

(`cb536bb`) Índice composto `(status, concluido_em)` (migration
`AdicionaIndiceCompostoStatusConcluidoEmDeProtocolos`, para `ObterConcluidosNoPeriodoAsync`); N+1 em
`GerarSugestoes` → `ObterMaisRecentesPorChavesAsync`; `ObterCoberturaDeAlcada` sem carregar a tabela →
`ObterTipoAtoIdsDistintosAsync`; `/health/db` separado de `/health` (ADR-0025); cache `IMemoryCache` de
5 min na configuração com `ObterParaEdicaoAsync` + `InvalidarCache()` (armadilha do change tracker —
ADR-0023); `render.yaml` `Jwt__ExpiracaoMinutos` 60 → 480 (aplicar em produção ainda exige o
dashboard). Testado: `/health` sem banco, `/health/db` ok, sugestões e cobertura normais, `PUT /config`
refletindo na hora.

## 2026-09-14 — Validação cruzada urgência < atenção — fecha o gap do protótipo reexportado

(`1a149a0`) O protótipo reexportado ganhou "Configuração do sistema" com validação não especificada no
requisito: se `faixaUrgente >= faixaAtencao`, o laranja nunca aparece. `AtualizarConfiguracao.Validar`
cruza (só depois de validar cada campo > 0). 2 casos em `ValorInvalido_RejeitaSemAlterarNada`. 350 testes (108 Domain + 242 Application).
Testado: `PUT /config` com faixas iguais → 400.

## 2026-09-14 — "Hora de entrada" no cadastro manual de protocolo (RF-18f)

(`8a0d4f2`) O cadastro manual sempre usava `IRelogio.Agora`. `CriarProtocoloManual`/`SimularProtocoloManual`
ganharam `DateTimeOffset? andamentoEm = null` (antes do `CancellationToken`); **só `AndamentoEm` muda** —
o `agora` de `AtribuirA` (RNF-16) continua o instante real. Requests ganharam `AndamentoEm`. 3 testes.
364 testes (111 Domain + 253 Application).

## 2026-09-14 — Corte de data no bucket "concluídos" de GET /protocolos/distribuicao

(`fce4546`) ADR-0026. Janela de 30 dias (`ObterVisaoDistribuicao.DiasHistoricoDeConcluidos`) só em
`concluidos`, ignorada com `loteImportacaoId`; método dedicado `ObterParaVisaoDistribuicaoAsync` para não
cortar `GerarSugestoes`/`ListarTiposAtoComUso`; `Excluido`/`Descartado` nunca buscados; índice composto
já existente. 5 testes. Testado backdatando um concluído 40 dias via `psql`: sumiu da distribuição,
continuou em `/tipos-ato/com-uso` e `/sugestoes/gerar` normal. 361 testes (111 Domain + 250
Application). Fora de escopo: override "ver tudo", `ORDER BY` nos outros buckets, promover para config.
Nenhuma mudança no front.

## 2026-09-14 — Bloqueio de tentativas de login por senha + origem no evento de auditoria

(`6343bce`) Gap-analysis contra a v2: login por senha sem limite de tentativa e `EventoAutenticacao` sem
origem (RNF-16). `Usuario.TentativasLoginFalhas`/`BloqueadoAte` + `EstaBloqueado`/
`RegistrarTentativaLoginFalha`/`RegistrarLoginComSucesso` (5 → 15 min). `Autenticar` reescrito
(bloqueado rejeita e audita `LoginBloqueado` sem checar senha; errada incrementa e audita `LoginFalhou`;
sucesso zera); anti-enumeração estendida ao bloqueio (mesmo `ResultadoAutenticacao.Rejeitado()`). `EventoAutenticacao.Origem` (`string?`) em todos
os casos de uso que auditam, posicional antes do `CancellationToken`; `HttpContextExtensions.ObterOrigem()`
(`X-Forwarded-For`, fallback `RemoteIpAddress`). De novo a armadilha de `builder.Property` explícito.
Migration `AdicionaBloqueioDeLoginEOrigemEmEventosAutenticacao` (`usuarios.tentativas_login_falhas`
`NOT NULL DEFAULT 0`, `usuarios.bloqueado_ate` e `eventos_autenticacao.origem` `varchar(64)` nullable). Testado: 6ª tentativa
401 mesmo com senha certa; `psql` confirma contador, bloqueio e origem. 356 testes.

## 2026-09-15 — `ProtocoloResumo` ganha `AndamentoEm` — "data de entrada" no card

(`0e03fce`) `DateTimeOffset AndamentoEm` (sempre preenchido) no fim de `ProtocoloResumo`, direto de
`Protocolo.AndamentoEm`. Sem migration, sem teste novo (passagem direta). 364 testes.

## 2026-09-15 — `AtribuirManualmente` deixa de ser exclusivo de exceção — "mandar um ato pra alguém"

(`51cebe2`) ADR-0027. Guarda `Pool or Excecao or Atribuido` (nunca `Conferindo`), sem validação de
alçada; `ProtocoloNaoEstaEmExcecao` → `ProtocoloNaoElegivel`. 3 testes. 366 testes (111 Domain + 255
Application). Testado: Pool → atribui (204) → reatribui direto (204) → forçado `Conferindo` via SQL → 409.
Front ("Atribuir a…/Reatribuir a…") na mesma rodada.

## 2026-09-15 — Uma conta com os dois papéis — distribuidora que também confere

(`45e6ff9`) ADR-0028. `PapeisEfetivos`, `VincularConferenteAUsuario` + `POST /conferentes/vincular`,
`EmitirToken` com lista de papéis (uma claim por papel), `Papeis` em `Autenticado`/`UsuarioAtual`/
`UsuarioResponse`, visões restritas com `&& !IsInRole(Distribuidora)`. Zero migration. Testado: vincular
→ novo login → `papeis: ["Distribuidora", "Conferente"]` → `GET /minha-fila` 200 → `GET /dashboard`
visão de gestão. 6 testes. 372 testes (111 Domain + 261 Application). Front: nav mesclada, sidebar com
os dois papéis, botão de vincular em Conferentes.

## 2026-09-15 — Primeira paginação de verdade do sistema — GET /tipos-ato/com-uso

(`6bce51d`) ADR-0029. `Paginado<T>`, `ListarTiposAtoComUso(busca, pagina, tamanhoPagina)`, resposta
`PaginaDeTipoAtoComUsoResponse { Itens, Total }`. Testado `?pagina=2&tamanhoPagina=5` (total 24) e
`?busca=venda` (2 itens: "Escritura de Compra e Venda", "Venda e Compra"). Testes
`Busca_FiltraPorNomeAntesDePaginar`, `Pagina2_DevolveOsItensSeguintesEOTotalReal`. 374 testes. Front: busca com debounce, paginação do shadcn, remoção
do seletor de grupo por linha; e2e achou specs dependentes do rótulo "Minha fila" para conta combo.

## 2026-09-15 — Clone de produção pra dev — achado real: a suíte e2e dependia de dado que só "por acaso" existia

(`c4cea2a`) Clone do Neon para o Postgres local (procedimento em `docs/patterns/deploy.md`), anonimizado,
para analisar a Central de Regras. Expôs que o e2e do front logava com contas seed criadas à mão numa
sessão antiga (`distribuidora@cartorio.com`, `conferente-rf27@cartorio.com`,
`conferente-visual@cartorio.com`). "Um bom teste não depende de dado local, a não ser que o dado seja
criado pelo teste e depois apagado." `SemearContasE2E` (idempotente: cria ou **reseta** senha
`Senha123!`, nome, e-mail, bloqueio; garante `Conferente` vinculado e presença na escala) +
`POST /dev/seed-e2e` (`DevSeedEndpoints.cs`, só em Development, anônimo). Front chama no `globalSetup`
do Playwright (`playwright.config.ts`). Achado corrigindo: o primeiro corte não resetava o nome (spec esperando "Distribuidora
Teste" quebrou contra o clone) → `Usuario.AtualizarPerfil` também no caminho "já existe". 2 testes.
375 testes (111 Domain + 264 Application).

## 2026-09-15 — Cadastro manual de escrevente

(`0b0a7ec`) Pergunta paralela sobre um papel futuro "Subscritor" ficou só no front (não existe no
requisito). `CriarEscrevente` — primeiro caminho deliberado (antes só por importação ou
`ResolvedorDeEscreventePorNome`): nome duplicado (normalizado, case-insensitive) é 409, equipe opcional
validada (404). `POST /escreventes` (`EquipeEndpoints.cs`, Distribuidora). 4 testes. 379 testes. Front:
"Novo escrevente" em Prazos por equipe.

## 2026-09-15 — Bug real: KPIs do Dashboard vazavam o total da operação pra visão restrita do conferente

(`999d3bf`) `kpis` era calculado antes de qualquer restrição, sobre todos os conferentes. Fix: na visão
restrita, `CalcularKpis` recebe `porDono.GetValueOrDefault(conferenteRestritoId.Value, [])`. O teste
existente (`VisaoRestrita_SoMostraOProprioDesempenhoESemFaixa`) nunca checava `Kpis` — ganhou a
asserção e um teste dedicado
(`VisaoRestrita_KpisRefletemSoOProprioConferente_NaoOTotalDaOperacao`). 381 testes. Testado contra o
clone: gestão `atosConferidos: 3`, conferente `2`. Front sem mudança (`VisaoConferente.tsx`/
`VisaoGestao.tsx` só exibem `dashboard.kpis`).

## 2026-09-16 — Bug real: Duracao de um protocolo reaberto perdia o tempo do ciclo anterior

(`e883c42`) Protocolo real nº 264137: só o segundo ciclo aparecia. `ReabrirConferencia` sobrescrevia
`IniciadoEm`/`ConcluidoEm` — o dado deixava de existir. Fix (substituído no dia seguinte pelo ADR-0032):
`Protocolo.TempoAcumuladoAnterior` (`TimeSpan`) somado antes de zerar; `Duracao` = acumulado + ciclo
atual. Migration `AdicionaTempoAcumuladoAnteriorEmProtocolos` (`interval NOT NULL DEFAULT '00:00:00'`,
sem backfill possível — reabertos antes perderam o primeiro ciclo). **Migration aplicada no Neon na
hora** (`dotnet ef database update --connection "..."`, confirmado com o dono), senão o deploy quebraria
toda leitura de `Protocolo`. Testes `ReabrirConferenciaEConcluirDeNovo_DuracaoSomaOsDoisCiclos`,
`ReabrirConferenciaDuasVezes_AcumulaOsTresCiclos`; o antigo
`ReabrirConferencia_VoltaPraConferindoComCronometroDoZero` nunca concluía de novo. 383 testes. Testado: dois ciclos de ~14,5s e ~14,2s somando
exatamente `00:00:28.7800620`.

## 2026-09-16 — Bug real em produção: exceção pessoal de outro alvo reabria "ninguém, independente de quem"

(`d3e3dae`) ADR-0030. Conta combo com alçada plena pessoal recebia atos que "Quinto Andar não faz
pré-conferência" deveria bloquear. `NegaEquipeEEtapaQueBloqueia` antes da cascata. Teste antigo
renomeado e invertido; novo `EquipeEEtapa_NegacaoEhAbsolutaMesmoComAlcadaPlenaPessoal`. 384 testes (114
Domain + 270 Application).

## 2026-09-16 — Reabrir conferência devolve pra Atribuído, não liga o cronômetro na hora

(`240d51a`) ADR-0031. Protocolo 263605. `ReabrirConferencia` → `Atribuido` + `IniciadoEm = null`.
Testes ajustados (renomeado `ReabrirConferencia_VoltaPraAtribuidoSemLigarOCronometro`; acumulação com
`IniciarConferencia` explícito e 1h de espera não contando; asserções de Application trocadas para
`Atribuido`/`null`). Verificado: `psql` Atribuído/`IniciadoEm` nulo, `GET /minha-fila` em `atribuidos`,
`Duracao` final soma os ciclos. 384 testes.

## 2026-09-17 — `TempoAcumuladoAnterior` vira `CiclosAnteriores` — Dashboard não pode mais herdar tempo de outra pessoa

(`ea4069a`) ADR-0032. `CicloConferencia`; `Protocolo.CiclosAnteriores` (backing field
`_ciclosAnteriores`); `OwnsMany` para `ciclos_conferencia` (primeira coleção-filha; backing field achado
por convenção, sem `UsePropertyAccessMode`); migration `AdicionaCiclosDeConferencia` com backfill antes
de dropar (1 protocolo em produção). `ObterDashboard`: `ConstruirTemposPorConferente`, `CalcularKpis`
com `temposProprios`, `CalcularDesempenho` com `TempoMedio` dos próprios ciclos, lista de desempenho
unindo `porDono.Keys` com `temposPorConferente.Keys`. RF-27 na reabertura (dono fora da escala → pool).
Testes: `ReabrirConferenciaTests.ProtocoloConcluido_VaiParaOPool_QuandoODonoSaiuDaEscala`,
`Aprovar_VaiParaOPool_QuandoOSolicitanteJaSaiuDaEscala`,
`ProtocoloReabertoEReatribuido_TempoMedioNaoHerdaDoConferenteAnterior` (Ana 20 min, Bruno 5 min); os
caminhos felizes `ProtocoloConcluido_Reabre`/`Aprovar_ReabreOProtocoloComMesmoDono` foram renomeados e
passaram a criar o `Conferente` com `naEscala: true`.
Verificado com API real (volume/tempo por pessoa batendo com cada ciclo; ausente → pool via `psql`).
389 testes (116 Domain + 273 Application).

## 2026-09-17 — "Atribuídas a você" também ordenada por vencimento

(`2ca6b57`) `ObterAtribuidosAAsync` sem `ORDER BY`; `ObterMinhaFila` ordena `atribuidos` por
`VencimentoEm ?? MaxValue` (corrige Minha fila e a fila vista pela distribuidora). `EmConferencia` fora
(limite de 1). Teste `ObterMinhaFilaTests.Atribuidos_OrdenaPorVencimentoAscendente` (par de
`PoolDisponivel_OrdenaPorVencimentoAscendente`); verificado com 2 protocolos criados fora de ordem. 390 testes.

## 2026-09-17 — Pausar conferência — "a pessoa sai pra almoçar, por exemplo"

(`9314641`) ADR-0033. `Protocolo.PausadoEm`, `Pausar`/`Retomar` (fecham/abrem ciclo, `Status` continua
`Conferindo`); `PausarConferencia`/`RetomarConferencia`; `ConcluirConferencia` com `EstaPausado`;
`POST /minha-fila/{id}/pausar|retomar`; `ProtocoloResumo.PausadoEm`; migration `AdicionaPausaEmProtocolos`.
Testes em `ProtocoloTests` (4), `PausarConferenciaTests`/`RetomarConferenciaTests` (5+4),
`ProtocoloPausadoAindaContaNoLimiteDeSimultaneos`, `ProtocoloPausado_RetornaEstaPausado`. Verificado:
concluir pausado 409; `duracao` = 1,06s + 2,05s = 3,11s excluindo ~13s de pausa. 405 testes (120 Domain
+ 285 Application).

## 2026-09-17 — Visibilidade das pausas — "como garantir que ninguém abusa da pausa pra melhorar o tempo dela?"

(`063dbfd`) Perguntado antes de aplicar em produção. Decisão do dono: visibilidade, não bloqueio.
`PausaConferencia` (`PausadoEm`/`RetomadoEm`), `Protocolo.Pausas` (`OwnsMany`, `pausas_conferencia`),
`Retomar` registra a pausa encerrada; `DetalheProtocoloResponse` com `PausadoEm` e `Pausas`. Migration
`AdicionaHistoricoDePausas`. Testes `Retomar_RegistraAPausaEncerrada`,
`PausarERetomarVariasVezes_AcumulaUmaPausaPorCiclo`. 407 testes (122 Domain + 285 Application).

## 2026-09-21 — Reabertura recalcula o vencimento a partir de agora

(`2725d5d`) ADR-0034. `DefinirPrazo(prazoAtual, agora)` no fim de `ReabrirConferencia`; `AndamentoEm`
intacto; guarda para `Prazo` nulo. Testes `ReabrirConferencia_RecalculaVencimentoAPartirDeAgora`,
`ReabrirConferencia_SemPrazoDefinidoAntes_NaoQuebraENaoInventaVencimento`. Verificado com entrada 5 dias
atrás. Sem migration. 409 testes (124 Domain + 285 Application).

## 2026-09-22 — Ajustar duração — a distribuidora corrige o tempo final de conferência

(`de4ed14`) ADR-0035. `AjusteDeDuracao`, `Protocolo.AjustarDuracao` com `DuracaoAjustada` sobrepondo o
cálculo, `AjustarDuracaoProtocolo`, `POST /protocolos/{id}/ajustar-duracao`, `ajustes_de_duracao`
(migration `AdicionaAjustesDeDuracao`), Dashboard atribuindo ao dono atual, `DetalheProtocoloResponse`
com `Duracao` e `AjustesDeDuracao` (nome resolvido no back). Verificado: ~2s real → ajuste 30 min →
detalhe e Dashboard com `00:30:00`, `ajustadoPorNome: "Distribuidora Teste"`; negativa 400, não
concluído 409. 420 testes (127 Domain + 293 Application).

## 2026-09-22 — Cobertura de testes + testes de integração (Infrastructure/Api) — fecha dois gaps de ferramental

(`09110cd`) ADR-0036. `coverlet.collector` estava nos `.csproj` desde o template `dotnet new xunit`, nunca usado.
`dotnet-reportgenerator-globaltool` como tool local; skill `gate` nova (`.claude/skills/gate/`, hoje `api-gate`);
`tests/Dispatch.Api.Tests` (Testcontainers, Respawn, seed-e2e, `public partial class Program;`, EF
pinado em 10.0.11). 9 testes mirando bug de histórico. **Bug real na primeira execução**: 500 com
`DateTimeOffset` não-UTC → `DateTimeOffsetParaUtcConverter` em `ConfigureConventions`, sem migration.
Premissa desatualizada corrigida no teste (tipo desconhecido é criado, não só sinalizado). Baseline de
cobertura em `docs/patterns/testes.md`. 429 testes (127 Domain + 293 Application + 9 Api.Tests), build
sem avisos.

## 2026-09-22 — Corte de horário — prazo condicional por horário de entrada (Equipe + Etapa)

(`ecd8304`) ADR-0037. `TipoPrazo.CorteDeHorario`, `record Prazo(Tipo, HorarioDeVencimento?)`,
`FusoHorario`, 4 `TimeOnly?` em `Equipe` (`CortePreConferenciaHorarioCorte`/`...HorarioVencimento`,
`CortePosConferenciaHorarioCorte`/`...HorarioVencimento`), `PrazoPara(etapa, referencia)`, `ResolvedorDePrazo.Resolver(...,
referencia)` (call sites: `AplicadorDeDistribuicao`, `EditarProtocoloManual`, `RecalculoDeVencimentos`;
`AplicarSugestao` preserva o corte). `PrazoConversoes` com `"Tipo|HH:mm"` e `protocolos.prazo_tipo`, `equipes.prazo_pre_tipo`/`prazo_pos_tipo`
para `varchar(30)`.
Api: 4 campos planos com validação de pares. Migration `AdicionaCorteDeHorarioEmEquipes`. Testado:
antes das 16h → D+1; sexta depois das 16h → segunda 10h. Teste de integração
`CorteDeHorarioIntegracaoTests.cs` (round-trip + reabertura). 434 testes (130 Domain + 294 Application
+ 10 Api.Tests). Flake do teste (re-seed invalidando token) corrigido em `c928ceb`.

## 2026-09-22 — Fix de performance: `RecalculoDeVencimentos` escaneava a tabela inteira de escreventes

(`07230cf`) Apontado pelo dono. `ObterTodosAsync` + filtro em memória → `IEscreventeRepository.ObterPorEquipeIdAsync`
(`WHERE equipe_id = @equipeId`, índice `ix_escreventes_equipe_id` da FK já existente, sem migration). A busca de protocolos
(`ObterAbertosPorEscreventesAsync`) já filtrava no SQL. `ObterTodosAsync` continua para os ~10
consumidores que precisam da lista inteira. Testado: `PUT /equipes/{id}` D1→D2 recalculou +1 dia. 434
testes, suíte rodada 3× seguidas.

## 2026-09-25 — Reestruturação da documentação

CLAUDE.md (~3.000 linhas de changelog) virou um índice curto; o conteúdo foi para `docs/decisions/`
(ADR-0001 a 0037), `docs/patterns/`, este histórico e `docs/gaps-requisitos.md`. Skill `/adr` criada (hoje `/api-adr`).

## 2026-09-25 — Número da conferência ("↻ 2ª conferência", RF-24k)

ADR-0038. Feature 1 do `PLANO-melhorias.md`, lado do back. `RegistroDoNumero` (recorte leve de
`Protocolo`) e `ResolvedorDeContinuidade.NumeroDaConferencia` no Domain (1 + linhas anteriores do mesmo
Número, mesma etapa, Reprovadas). `IProtocoloRepository.ObterRegistrosPorNumerosAsync` com projeção
(sem carregar coleções filhas) e o helper `NumeroDaConferenciaEmLote` (uma query por listagem), usados
por `ObterMinhaFila` e `ObterVisaoDistribuicao`; `ObterDetalheProtocolo` reaproveita o histórico que já
carrega. `ProtocoloResumo`/`DetalheProtocoloResponse` ganham `NumeroDaConferencia`;
`HistoricoConferenciaResponse` ganha `NumeroDaConferencia` e `Observacao` (o motivo da não aprovação,
decisão do dono). `MinhaFilaEndpoints.ParaResumo` passou a exigir o número (sem default), atualizando os
3 chamadores (Minha fila, `/conferentes/{id}/fila`, Distribuição). Sem migration.

Verificado: 16 testes de Domain (`NumeroDaConferenciaTests`, incluindo Theory dos status que não contam),
3 de Application, e `NumeroDaConferenciaIntegracaoTests` pelo fluxo real (importar → pegar → iniciar →
reprovar → reimportar; número 2 em `/minha-fila`, `/protocolos/distribuicao` e no detalhe, 1 na linha
anterior). 454 testes (146 Domain + 297 Application + 11 Api.Tests), build sem avisos.

## 2026-09-25 — Perfil Administrador, Contas e troca de senha no primeiro acesso

ADR-0039 e ADR-0040; fecha gaps §1. Feature 3 do `PLANO-melhorias.md`, lado do back.
- **Papel**: `Papel.Administrador`; `PapeisEfetivos` dá `[Administrador, Distribuidora]` (+ `Conferente`
  se vinculado); `ClaimsPrincipal.EhAdministrador()`.
- **Só do admin** (`RequireRole(Administrador)` somado ao do grupo): escritas de `/conferentes`,
  `/regras-alcada` (inclusive testar), `/tipos-ato` de gestão, `/equipes`/`/escreventes`, `PUT /config`,
  o grupo `/sugestoes` e o novo `/contas`.
- **Corte na Application**: `ListarConferentes(incluirNivel)`,
  `ObterDashboard(incluirAvaliacaoDePessoal = false)` (sem a flag: sem nível/score/faixa/parcelas e por
  nome; visão restrita perde só o nível), `ListarRegrasAlcada(incluirNivel)` → `RegraAlcadaVisivel` e
  `RegraBase` na resposta.
- **Contas**: `CriarConta`, `ListarContas` (`TambemConfere`, `EhVoce`), `DesativarConta` (travas da
  própria conta / último admin com código próprio, conta de conferente usa `RemoverConferente`);
  `ContaEndpoints.cs`.
- **Troca de senha**: `Usuario.TrocarSenhaNoProximoAcesso` (migration `AdicionaTrocarSenhaAUsuario`),
  `RegrasDeSenha.ServeComoSenhaInicial` (8+), claim `trocar_senha`, middleware no `Program.cs`,
  `TrocarSenhaInicial` + `POST /auth/trocar-senha`; `trocarSenha` no login e no `/auth/me`.
  `CadastrarConferente` passou a validar a senha inicial (antes aceitava qualquer uma) e exige a troca.
- `OnTokenValidated` recusa conta inativa.
- Seed e2e ganha `distribuidora@` e `administrador@cartorio.com`.

Verificado: `ContasTests` (Application), `AutenticarTests`, `ListarConferentesTests`,
`ObterDashboardTests`, `ListarRegrasAlcadaTests`, `CadastrarConferenteTests`; integração em
`AdministradorIntegracaoTests` (403 da distribuidora em todas as 32 rotas só do admin, leitura mantida, nível mascarado em
Conferentes e Regras, score só pro admin, conta nova → troca obrigatória → uso → desativada → 401,
trava da própria conta) contra o Postgres real; migration aplicada no banco local e smoke pela HTTP.
Testes de Domain para `ServeComoSenhaInicial` e `ExigirTrocaDeSenha`/`RedefinirSenha`. 524 testes
(150 Domain + 326 Application + 48 Api.Tests).

**Subida**: migration no Neon antes do merge; promover a primeira admin (Maria Vittoria) logo depois
do deploy do front — ver `docs/patterns/deploy.md`, "Ordem de subida".

## 2026-09-25 — "Hoje" no dia de Brasília e tipo de ato casando sem acento

Itens 0.6 e 0.7 do `PLANO-dashboard-v2.md`. Sem migration.
- **Dia local (0.6)**: `FusoHorario` virou público e ganhou `InicioDoDiaLocal` (meia-noite de Brasília
  em UTC). `ObterConcluidosHoje` e `ObterVisaoDistribuicao` ("feitos hoje") usavam `Agora.Date` em UTC —
  o dia virava às 21h de Brasília. O grep achou o mesmo bug em `Prazo`: `FimDoDia` (D+0 vencia às 21h
  locais, ou no fim do dia seguinte se a entrada fosse depois das 21h) e `ProximoDiaUtil` (dia da
  semana em UTC: sexta 22h + D+1 vencia no domingo). Os dois passam pelo `FusoHorario`. Vencimentos
  já gravados ficam como estão (sem recálculo).
- **Tipo com acento (0.7)**: `NormalizadorDeTexto.ComparadorDeNome` (`StringComparer` que ignora caixa,
  acento — FormD sem marcas combinantes — e espaço repetido). `ImportarLote` casa o tipo do relatório
  com ele (prévia e confirmação usam o mesmo dicionário); duplicata por acento já gravada não derruba o
  `ToDictionary` (vale a ativa, desempate por `Id`). `CriarTipoAto`/`RenomearTipoAto` usam a mesma
  comparação no "já existe".

Verificado: `FusoHorarioTests` (4), `PrazoTests` (+4, 2 ajustados ao dia local), `NormalizadorDeTextoTests`
(+9); Application: `ObterConcluidosHojeTests`/`ObterVisaoDistribuicaoTests` com relógio às 22h30–23h
de Brasília, `ImportarLoteTests` (+5: "INVENTARIO" × "Inventário" na prévia e na confirmação, caixa,
mesmo tipo novo com e sem acento no lote, catálogo com duplicata), `CriarTipoAtoTests` (+1),
`RenomearTipoAtoTests` (+2); `DistribuirProtocoloTests` ajustado ao D+0 local. 551 testes
(167 Domain + 336 Application + 48 Api.Tests), build sem avisos.

## 2026-09-25 — Painel de hoje (RF-42a, fatia 1 do Dashboard v2)

`GET /dashboard/hoje` — a faixa "Hoje, agora" da gestão e "Seu dia" do conferente, contrato da fatia 1
do `PLANO-dashboard-v2.md`. Sem migration.
- **Caso de uso** `ObterPainelDeHoje` (Application): relógio injetado, "hoje" via
  `FusoHorario.InicioDoDiaLocal`; risco por `Semaforo.Calcular` com janela fixa de 1h; gargalo pela
  equipe do escrevente, só quando > 1, "sem equipe" como grupo, empate pelo menor `EquipeId` entre
  equipes de verdade. Reaproveita `ObterParaVisaoDistribuicaoAsync` (gestão, uma query) e as consultas
  da Minha fila (conferente). Regras: `docs/patterns/indicadores-e-aprendizado.md`, "Painel de hoje".
- **Endpoint**: `DashboardEndpoints` virou um `MapGroup("/dashboard")` com a mesma
  `RequireRole(Distribuidora, Conferente)` para `GET /dashboard` (rota inalterada) e `GET /dashboard/hoje`;
  a escolha da visão restrita (`IsInRole(Conferente) && !IsInRole(Distribuidora)`, conferente pelo
  usuário logado, 404 se não houver vínculo) saiu do handler para `ResolverVisaoRestritaAsync`,
  compartilhado. DI em `ServiceCollectionExtensions`.

Verificado: `ObterPainelDeHojeTests` (12, relógio às 23h e às 10h de Brasília: conferidos só do dia
local, bordas de estourado/vence em 1h, gargalo >1, "sem equipe" e escrevente fora do cadastro, dois
empates, visão do conferente); `PainelDeHojeIntegracaoTests` (3: sem token 401, distribuidora → Gestao
e conferente → Conferente com números de um fluxo real importar → pegar → iniciar → concluir,
administrador → Gestao). Smoke com `dotnet run` numa porta alternativa e token real das três contas.
566 testes (167 Domain + 348 Application + 51 Api.Tests), build sem avisos.

## 2026-09-25 — Período de calendário, variação, série e aprovado na 1ª (fatias 3 e 4 do Dashboard v2)

`GET /dashboard?periodo=` ganha os acréscimos do contrato das fatias 3 + 4 do `PLANO-dashboard-v2.md`
(nada removido). Sem migration.
- **Período por calendário** no dia de Brasília (decisão 1 do dono, [ADR-0041](decisions/0041-periodo-do-dashboard-por-calendario.md)):
  `CalendarioDoPeriodo` (Domain) — Semana desde segunda, Mes desde o dia 1, Trimestre desde
  jan/abr/jul/out — no lugar da janela móvel 7/30/90. `FusoHorario` ganhou `DiaLocal` e `InicioDoDia`.
  Resposta com `periodoInicio`/`periodoFim`.
- **`kpisAnterior`** (RF-42b): mesmo trecho do período anterior, limitado ao início atual; segunda
  chamada ao mesmo `ObterConcluidosNoPeriodoAsync`. Também na visão restrita (números dela).
- **`serie`** (RF-42c): `SerieDoPeriodo` (Domain) — Dia (dias úteis do período inteiro, `futuro` nos que
  não chegaram, fim de semana só com conferência) ou Semana (trimestre, uma por segunda, 13–14 pontos);
  estourados = complemento do `EstaNoPrazo`. Visão restrita: só dela.
- **`percentualAprovadoNaPrimeira`** (RF-43, decisão 3 do dono) em `kpis`, `kpisAnterior`, `desempenho`
  e `mediaDaCasa`: linhas com `NumeroDaConferencia == 1` (ADR-0038, via `NumeroDaConferenciaEmLote` sobre
  os dois trechos, uma query) com status Aprovado agora; `null` sem 1ª conferência. Score não mudou.
- Regras: `docs/patterns/indicadores-e-aprendizado.md`, "Dashboard". Gaps §33 fechado, §36 só com as
  metas (fatia 2) em aberto.

Verificado: `CalendarioDoPeriodoTests` (17: segunda de Brasília, domingo à noite que já é segunda UTC,
virada de mês, trimestres, mesmo trecho com fevereiro limitado, virada de ano, duração zero, último dia
e bissexto), `SerieDoPeriodoTests` (7: dias úteis e futuros, dia de Brasília, fim de semana com
volume, mês inteiro, trimestre em 14 e em 13 semanas, conclusão fora do período), `FusoHorarioTests`
(+2); `ObterDashboardTests` (+11: semana e mês de calendário, trecho anterior, visão restrita da
variação, aprovado na 1ª com 2ª rodada fora e correção contando, nulo sem 1ª conferência, média da casa,
série gestão/restrita/trimestre); `DashboardIntegracaoTests` (2: JSON com os campos novos nas duas
visões, fluxo real importar → pegar → iniciar → concluir; trimestre por semana). Smoke com `dotnet run`
na porta 5299 e token real (administrador e conferente, Mes e Trimestre; 401 sem token, 400 com
período inválido): soma da série = `atosConferidos`. 608 testes (196 Domain + 359 Application + 53
Api.Tests), build sem avisos.

## 2026-09-25 — Metas e pesos do score configuráveis (fatia 2 do Dashboard v2)

Contrato da fatia 2 do `PLANO-dashboard-v2.md` (RF-42b metas, RF-46), decisão 4 do dono.
[ADR-0042](decisions/0042-metas-e-pesos-do-score-na-configuracao.md).
- **Domain**: `MetasDoDashboard` (frações 0,50–1,00, padrão 0,95/0,90) e `PesosDoScore` (inteiros ≥ 0
  que somam 100, padrão 40/30/20/10), com `Validar()` → motivo. `Configuracao` ganha `MetaNoPrazo`,
  `MetaAprovadoNaPrimeira`, `PesoVolume`, `PesoPrazo`, `PesoQualidade`, `PesoComplexidade`, as leituras
  `Metas`/`Pesos` e `DefinirMetasEPesos` (lança se inválido).
- **Migration** `AdicionaMetasEPesosEmConfiguracao`: 6 colunas `NOT NULL` com `DEFAULT` = os valores de
  antes (editado sobre o 0 gerado pelo EF; sem `HasDefaultValue` no modelo). **Precisa ir ao Neon antes
  do merge.**
- **`PUT /config`** (só Administrador): os 6 opcionais (ausente/`null` = mantém o atual); inválido → 400
  `{ motivo }` (`ResultadoAtualizarConfiguracao.MetasOuPesosInvalidos`), nada muda. **`GET /config`**
  devolve os 6.
- **`GET /dashboard`**: score com os pesos da Configuração (parcelas de 0 ao peso; faixas 85/70 fixas);
  `metas` só na gestão; `pesos` pra Administrador e visão restrita, `null` pra distribuidora.
  `ObterDashboard` recebe `IConfiguracaoRepository` (leitura cacheada).
- **Testes de integração**: `IntegracaoTestBase.AutenticarComoAsync` passa a semear **uma vez por
  teste** — re-semear redefinia a senha e encerrava o token de outro cliente do mesmo teste
  (`SessoesValidasApartirDe`), 401 intermitente que apareceu em `DashboardIntegracaoTests` e
  `PainelDeHojeIntegracaoTests` (`docs/patterns/testes.md`).
- Gaps §34 e §36 fechados; §28 atualizado.

Verificado: `MetasEPesosTests` (18: padrões, bordas 0,50/1,00, NaN, percentual inteiro por engano,
soma ≠ 100 com a soma no motivo, negativo somando 100, `DefinirMetasEPesos` válido e recusado sem
mudar nada); `AtualizarConfiguracaoTests` (+7: PUT sem os 6 mantém, PUT parcial troca só o que veio,
os 6 gravados, quatro inválidos sem mudar nenhum dos 18); `ObterDashboardTests` (+6: score/faixa/parcelas
com pesos padrão e com 10/20/30/40, peso zero, metas/pesos por visão); `ConfiguracaoIntegracaoTests`
(7: GET com os padrões da migration, PUT com e sem os campos novos relido no Postgres, quatro 400 com
motivo, distribuidora 403 com corpo válido); `DashboardIntegracaoTests` (+1: metas/pesos por visão e
score recalculado depois do PUT, cache invalidado). Migration aplicada no Postgres local. Smoke com
`dotnet run` na porta 5299 e token real das três contas (PUT antigo 204, 400 soma 110 e meta 95, 403
distribuidora, dashboard por papel, pesos 10/20/30/40 mudando score e faixa, configuração restaurada).
Suíte de integração rodada 4× seguidas sem falha. 647 testes (214 Domain + 372 Application + 61
Api.Tests), build sem avisos.
