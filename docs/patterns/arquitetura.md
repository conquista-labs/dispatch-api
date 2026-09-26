---
name: arquitetura
description: Camadas, o que pode referenciar o quê, portas e adapters, convenções de caso de uso e o composition root do dispatch-api
metadata:
  type: pattern
  domains: [arquitetura, application, domain, di]
  status: stable
---

# Arquitetura — camadas, portas e casos de uso

> A decisão está no [ADR-0001](../decisions/0001-clean-architecture-em-quatro-projetos.md). Este
> documento é a referência de **onde o código novo vai** e das convenções que o projeto consolidou
> ao longo de ~70 entregas.

## Quando ler

- Antes de criar um caso de uso, porta, entidade ou endpoint novo (a skill `new-use-case` segue
  este documento).
- Quando algo em `Domain`/`Application` parecer precisar de EF Core ou ASP.NET.
- Quando a API subir com `Failure to infer one or more parameters`.

## Camadas e dependências

```
src/
  Dispatch.Domain          entidades, value objects, motor, prazos, alçada, aprendizado — C# puro
  Dispatch.Application     CasosDeUso/ + Portas/ (interfaces) — depende só de Domain
  Dispatch.Infrastructure  DispatchDbContext, Configuracoes/ (IEntityTypeConfiguration), Repositorios/,
                           Persistencia/ (classes-registro achatadas), Migrations/, adapters
                           (hash, JWT, TOTP, AES) — depende de Application + Domain
  Dispatch.Api             Program.cs, Endpoints/*.cs, DTOs de request/response — depende de
                           Application + Infrastructure
tests/
  Dispatch.Domain.Tests  Dispatch.Application.Tests (fakes)  Dispatch.Api.Tests (integração)
```

- Uma camada só referencia as de dentro. `Domain` nunca ganha pacote NuGet de framework.
- Se uma classe de `Domain`/`Application` "precisa" de EF Core, a abstração está no lugar errado.
- Organização do Domain por assunto: raiz (`Protocolo`, `Conferente`, `TipoAto`, enums), `Alcada/`,
  `Distribuicao/`, `Prazos/`, `Aprendizado/`, `Usuarios/`.

## Domain: transição sabe *fazer*, caso de uso decide *se pode*

- Entidades expõem comportamento, não setter público: `AtribuirA`, `EnviarParaPool`,
  `MarcarExcecao`, `IniciarConferencia`, `Aprovar`, `Reprovar`, `Pausar`, `Retomar`, `Excluir`,
  `Restaurar`, `DefinirPrazo`, `DefinirPrioridade`... Propriedades com `private set`.
- A **regra de permissão** ("só o dono", "só se Conferindo", "dentro da janela") fica no caso de
  uso. Exceção consciente: invariantes que precisam ser absolutos no Domain mesmo que a Api valide
  (ex.: negação equipe+etapa — [ADR-0030](../decisions/0030-equipe-e-etapa-absoluto-fora-da-cascata.md)).
- Hierarquias fechadas emulando sum type: record abstrato com construtor privado + tipos aninhados
  (`SujeitoAlcada`, `AlvoAlcada`, `PayloadSugestao`, `ResultadoDistribuicao`). Tradução para a
  resposta HTTP por `switch`, nunca por `as` espalhado (ver `endpoints.md`).
- Valores derivados são **calculados, nunca persistidos**: `Semaforo`, `CargaAtual`
  ([ADR-0011](../decisions/0011-carga-atual-calculada-na-leitura.md)), `Duracao`, contador de usos de
  regra, uso de tipo de ato.
- Todo "agora" vem de `IRelogio` injetado — inclusive "hoje" (`ObterConcluidosHoje`,
  `ObterVisaoDistribuicao`). Nunca `DateTimeOffset.Now` direto. "Hoje" é o dia de Brasília:
  `FusoHorario.InicioDoDiaLocal(relogio.Agora)`, nunca `Agora.Date` (ver `motor-e-prazos.md`, "Fuso").

## Application: casos de uso

- **Nome pelo verbo de negócio** (`ImportarLote`, `PegarProtocolo`, `DecidirPedidoReabertura`), nunca
  "Service"/"Manager". Um arquivo por caso de uso em `CasosDeUso/`, método `ExecutarAsync`.
- **Resultado com mais de dois desfechos** = `abstract record ResultadoX` fechado
  (`Sucesso`/`NaoEncontrado`/`NaoEhSeu`/`StatusInvalido`...). Dois desfechos podem ser `bool`.
- **A Api nunca injeta repositório direto**, nem para leitura trivial — sempre um caso de uso,
  mesmo que seja pass-through fino (`ObterUsuarioAtual`, `ListarEquipes`, `ListarRegrasAlcada`,
  `ObterConfiguracao`). Exceções que existem hoje: `GET /protocolos/{id}/detalhe` injeta
  `IUsuarioRepository` para resolver `AjustadoPorNome` ([ADR-0035](../decisions/0035-ajuste-manual-de-duracao.md))
  e `GET /regras-alcada` usa `IProtocoloRepository.ContarPorRegraAplicadaAsync` para o contador de usos.
  Casos de uso da Application podem injetar portas direto (ex.: `IConfiguracaoRepository`).
- **Helpers internos compartilhados** (não são casos de uso, `internal`): `AplicadorDeDistribuicao`
  (resolve prazo → roda motor → aplica resultado; usado por importação, cadastro manual e simulações),
  `VerificadorDeAlcada` ("esse conferente pode pegar esse protocolo"), `RecalculoDeVencimentos`
  (RF-38), `PapeisEfetivos`, `ResolvedorDeEscreventePorNome` (busca case-insensitive, cria sem equipe;
  `adicionarSeNovo: false` nas simulações).
- **Simular sem persistir**: monte `Protocolo`/`Escrevente` só em memória e chame
  `AplicadorDeDistribuicao.Executar` — ele nunca persiste sozinho, quem persiste é o chamador
  (`SimularProtocoloManual`, `SimularAlcada`).
- Leituras compostas devolvem **records de primitivos** (`ConferenteComUsuario`, `AlcanceDoConferente`,
  `ResumoImportacao`) — nunca entidade de Domain vazando para a Api.
- Joins entre agregados são feitos **em memória com busca em lote** (`ObterVariosPorIdsAsync`), nunca
  `ObterPorIdAsync` num laço (ver `ef-core.md`, N+1).

## Portas e unidade de trabalho

- Portas em `Application/Portas/`: `IProtocoloRepository`, `IConferenteRepository`, `IUsuarioRepository`,
  `IEquipeRepository`, `IEscreventeRepository`, `ITipoAtoRepository`, `IRegraAlcadaRepository`,
  `ISugestaoRepository`, `IConfiguracaoRepository`, `IRelogio`, `IHashDeSenha`, `IEmissorDeToken`,
  `IConversorDeRelatorio`, `IUnitOfWork`...
- **Porta com vários adaptadores**: `IConversorDeRelatorio` (conector de relatório do cartório,
  [ADR-0045](../decisions/0045-conector-de-relatorio-por-cartorio.md)) é registrada uma vez por formato, e o
  caso de uso recebe `IEnumerable<IConversorDeRelatorio>` — o container entrega todos os registros. Cada
  adaptador devolve `FormatoNaoReconhecido` pro que não é dele; o caso de uso usa o primeiro que reconhece.
  Adaptadores moram em `Infrastructure/Conectores/`.
- **`IUnitOfWork`**: métodos de escrita dos repositórios (`Adicionar`) só marcam estado; o caso de
  uso chama `unitOfWork.SalvarAsync()` **uma vez** no fim. Equivale ao `prisma.$transaction([...])`,
  só que explícito por injeção. O projeto não usa `BeginTransactionAsync` em lugar nenhum — premissa
  do `EnableRetryOnFailure` (ver `ef-core.md`).
- Quando uma query compartilhada precisa de um recorte para um consumidor, crie **método dedicado**
  em vez de alterar a compartilhada (ex.: `ObterParaVisaoDistribuicaoAsync` vs
  `ObterParaDistribuicaoAsync` — [ADR-0026](../decisions/0026-corte-de-30-dias-nos-concluidos.md);
  `ObterPorEquipeIdAsync` vs `ObterTodosAsync`).

## Composition root

- Registro de DI em `src/Dispatch.Infrastructure/ServiceCollectionExtensions.cs` (`AddInfrastructure`,
  chamado pelo `Program.cs`). Os ~65 `AddScoped<CasoDeUso>()` estão **agrupados por arquivo de
  endpoint consumidor**, com comentário por grupo — ao criar caso de uso, registre no grupo certo.
- **Armadilha**: esquecer o registro não quebra `dotnet build` nem `dotnet test` (nenhum teste
  instancia o composition root inteiro). Minimal API só falha **ao montar o endpoint em runtime**,
  com `InvalidOperationException: Failure to infer one or more parameters` — ela não sabe se o
  parâmetro é rota, corpo ou serviço quando o tipo não está no container. **Sempre `dotnet run`
  depois de adicionar caso de uso, endpoint ou dependência nova de construtor** (tier 3 da skill
  `api-gate`). Aconteceu com `DefinirGrupoDoTipoAto` (motor v2).
- Endpoints de desenvolvimento (`DevSeedEndpoints`) são mapeados só dentro de
  `if (app.Environment.IsDevelopment())` no `Program.cs` — o gate fica no Program, não no endpoint.

## Evoluindo assinaturas sem deixar call site para trás

- Parâmetro novo obrigatório **posicional antes do `CancellationToken`** força erro de compilação em
  todo call site antigo (usado para `origem` na auditoria de autenticação e `andamentoEm` no
  cadastro manual). Parâmetro com default (`= null`) só quando o comportamento antigo é o correto para
  quem não passa.
- Campo novo em record de resposta vai **no fim** (não quebra quem desestrutura posicionalmente).
- Construtor de entidade: parâmetros novos opcionais no fim preservam call sites de teste
  (`Equipe`, 19 call sites); método de mutação sem default força o call site real a decidir
  (`Equipe.DefinirPrazos`).

## Referências

- [ADR-0001](../decisions/0001-clean-architecture-em-quatro-projetos.md)
- `endpoints.md`, `ef-core.md`, `testes.md`, `conceitos-dotnet.md`
- Skills `new-use-case`, `add-domain-rule`, `api-gate`
