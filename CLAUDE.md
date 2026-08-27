# Dispatch API

> Panorama geral do projeto Dispatch (os três repositórios, papéis do sistema) está em `../CLAUDE.md`.

Back-end do Dispatch — sistema de distribuição e conferência de protocolos (atos notariais)
que substitui uma planilha manual por regras explícitas e um motor de distribuição auditável.

O documento de requisitos completo vive em `../dispatch-prototype/Dispatch - Requisitos.dc.html`
(e os wireframes de exploração em `Fila de Protocolos - Wireframes.dc.html`, na mesma pasta).
Ele é a fonte da verdade do domínio — releia-o antes de modelar qualquer coisa nova.

## Contexto do projeto

Este projeto tem dois objetivos declarados pelo dono: entregar o sistema descrito no
documento de requisitos, e servir de veículo de aprendizado prático de .NET (a stack é nova
para ele, que vem de outros ecossistemas) e de boas práticas de engenharia assistida por IA.
Isso significa: explicar o "porquê" das escolhas de .NET ao introduzi-las (não só aplicar),
e manter este arquivo atualizado conforme decisões forem tomadas — ele é o registro entre sessões.

## Stack

- **.NET 10** (SDK `10.0.400`), C#.
- **ASP.NET Core Web API** (minimal APIs, não controllers) para a camada de entrada.
- **Entity Framework Core** para acesso a dados.
- **PostgreSQL no Neon** (free tier) como banco.
- **Fly.io** (free tier) como alvo de deploy.
- **xUnit** para testes.

## Arquitetura

Clean Architecture em 4 projetos sob `src/`, com um projeto de teste por camada sob `tests/`
(hoje só `Dispatch.Domain.Tests` existe — outros nascem quando a camada correspondente
ganhar lógica que justifique teste).

```
Dispatch.Domain          entidades, value objects, motor de distribuição — C# puro, zero dependência externa
Dispatch.Application     casos de uso e interfaces (portas) que a Infrastructure implementa — depende só de Domain
Dispatch.Infrastructure  EF Core (DbContext, migrations), repositórios, adapters — depende de Application + Domain
Dispatch.Api             Program.cs, endpoints, composition root (DI) — depende de Application + Infrastructure
```

Regra de dependência: uma camada só referencia as que estão "mais para dentro" na lista acima.
`Domain` nunca deve ganhar um pacote NuGet de framework (nada de EF Core, ASP.NET, etc. lá dentro).

## Premissas de qualidade

1. Lógica de domínio (motor de distribuição, precedência de regras de alçada) nasce com teste
   de unidade antes ou junto da implementação — é a parte do sistema mais fácil de acertar
   errado e mais cara de errar em produção.
2. Fronteiras de camada são levadas a sério: se uma classe em `Domain` ou `Application`
   "precisa" de algo do EF Core, é sinal de que a abstração está no lugar errado.
3. Mudança de schema só acontece via EF Core Migrations — nunca editar o banco do Neon na mão.
4. Este arquivo é atualizado sempre que uma decisão de arquitetura, stack ou convenção for
   tomada — não deixar a decisão só na conversa.

## Banco de dados

Postgres em dois lugares diferentes por ambiente, não o mesmo banco:

- **Local**: container Postgres via `docker-compose.yml` na raiz, sem dados de produção nem
  conexão com o Neon. Credenciais de dev fixas (não são segredo — só valem dentro do container
  local). Connection string em `src/Dispatch.Api/appsettings.Development.json`.
- **Produção**: Neon. A connection string real nunca fica em arquivo do repo — entra como
  secret no Fly.io (`fly secrets set`) quando o deploy for configurado.

Isso usa o sistema de configuração em camadas do ASP.NET Core (`appsettings.json` →
`appsettings.{Environment}.json` → variáveis de ambiente/secrets), que troca a connection
string sozinho conforme `ASPNETCORE_ENVIRONMENT`, sem `if` de ambiente no código.

## Skills do projeto

Em `.claude/skills/`, pra fluxos recorrentes deste repositório:

- **`add-domain-rule`** — adicionar/alterar regra do motor de distribuição ou de alçada.
- **`ef-migration`** — criar e aplicar migrations do EF Core.
- **`new-use-case`** — adicionar um caso de uso na Application respeitando as fronteiras de camada.
- **`verify-integration`** — validar comportamento real (HTTP de verdade, não só teste de unidade); ganha uma seção de Playwright quando o `dispatch-web` existir.

## Comandos

```
dotnet build              # compila a solution inteira
dotnet test                # roda todos os projetos de teste
dotnet run --project src/Dispatch.Api   # sobe a API localmente
```

## Estado atual

Solution, scaffold Docker/Postgres local e commit inicial feitos (ver seções acima).

Primeiro corte do motor de distribuição (seção 4 do documento de requisitos) modelado em
`Dispatch.Domain`, com 10 testes cobrindo os casos de precedência de alçada e os 5 passos
do motor — `dotnet test` verde. Estrutura:

- `Dispatch.Domain/` (raiz): `TipoAto`, `Conferente`, `Protocolo`, enums `Nivel`/`Etapa`/`Prioridade`.
- `Dispatch.Domain/Alcada/`: `SujeitoAlcada` e `AlvoAlcada` (hierarquias fechadas — record
  abstrato com construtor privado + tipos aninhados, emulando um sum type), `RegraAlcada`,
  `ResolvedorAlcada` (implementa a precedência pessoa > nível, negação > permissão, ausência
  de regra = permitido).
- `Dispatch.Domain/Distribuicao/`: `MotorDistribuicao` (os 5 passos), `AvaliacaoCandidato` e
  `ResultadoDistribuicao` (Atribuido / EnviadoParaPool / Excecao — carrega a regra aplicada
  por candidato, para auditabilidade — RNF-02).

Prazo e vencimento (seção 5) modelados em `Dispatch.Domain/Prazos/`: `Prazo`/`TipoPrazo`
(os 4 valores fixos — 1 hora, D+0, D+1, D+2), `Equipe`/`Escrevente`, `ResolvedorDePrazo`
(escrevente sem equipe cai no padrão D+1 e sinaliza — RF-09) e `Semaforo`/`FaixaSemaforo`
(as duas faixas de atenção/urgência entram como parâmetro, são configuração do sistema, não
constante do domínio). `Protocolo` ganhou `Prazo`/`VencimentoEm` (definidos via
`DefinirPrazo`, não no construtor — só existem depois da resolução) e `Urgente` agora
considera prazo curto (1h/D+0) além de prioridade alta. 28 testes, `dotnet test` verde.

Suposição assumida (não confirmada com a operação — ver seção 11 do documento): "vence no
fim do dia D" foi modelado como "vence no início do dia seguinte" (mesmo instante, cálculo
mais simples). Ajustar se a operação confirmar outra fronteira (ex.: 23:59:59 exatas).

Primeiro caso de uso em `Dispatch.Application`: `DistribuirProtocolo` (`CasosDeUso/`), que
orquestra `ResolvedorDePrazo` + `MotorDistribuicao` sem reimplementar nenhuma regra. As
dependências externas (conferentes, equipes, regras, catálogo de tipos, relógio) entram como
portas em `Portas/` (`IConferenteRepository`, `IEquipeRepository`, `IRegraAlcadaRepository`,
`ITipoAtoRepository`, `IRelogio`) — implementação real fica pra `Dispatch.Infrastructure`
depois. `Dispatch.Application.Tests` criado, testado com fakes in-memory dessas portas
(sem banco). 31 testes no total, `dotnet test` verde.

Persistência real em `Dispatch.Infrastructure`: `DispatchDbContext` (Npgsql), mapeamento via
`IEntityTypeConfiguration<T>` em `Configuracoes/` (uma classe por entidade), implementação das
5 portas em `Repositorios/`, e `EFCore.NamingConventions` ligado (`UseSnakeCaseNamingConvention`)
pra manter o banco em snake_case por convenção, sem precisar nomear coluna a coluna na mão.
Primeira migration (`InicializarSchema`) aplicada no Postgres local.

Duas decisões de mapeamento que valem registrar (não são óbvias vindo de Prisma/outros ORMs):
- **`Prazo` (value object de um campo só) usa `ValueConverter`, não `OwnsOne`.** EF Core não
  permite ligar uma navegação "owned" via parâmetro de construtor — só via propriedade com
  setter, o que forçaria abrir mão da imutabilidade de `Equipe`/`Protocolo` só por causa do
  ORM. Um conversor (`PrazoConversoes`, em `Configuracoes/`) trata a coluna como texto simples
  e resolve isso sem exigir setter nenhum.
- **`SujeitoAlcada`/`AlvoAlcada` (hierarquias fechadas / sum types) não são mapeadas
  diretamente.** `RegraAlcada` tem uma classe de persistência paralela, só pra EF Core
  (`RegraAlcadaRegistro`, em `Persistencia/`), com colunas achatadas (par nulo/preenchido) e
  um `CHECK` no Postgres (`num_nonnulls`) garantindo o invariante também no banco. Quem
  traduz de volta pro tipo rico do Domain é `RegraAlcadaRepository`, não o EF Core.

`Program.cs` chama `AddInfrastructure` (composition root) — API sobe e resolve toda a
cadeia de DI sem exception, `/health` responde 200 contra o Postgres local. `dotnet-ef`
instalado como tool local (`.config/dotnet-tools.json`).

Primeiro endpoint em `Dispatch.Api`: `POST /protocolos/distribuir`
(`Endpoints/ProtocoloEndpoints.cs`), com `DistribuirProtocoloRequest`/`Response` como DTOs
próprios da Api — `ResultadoDistribuicao` do Domain não vaza pro cliente HTTP, é traduzido
por um `switch` na hierarquia fechada. Testado ponta a ponta de verdade (Api → Application →
Domain → Postgres local) com dado inserido manualmente via DBeaver/psql, cobrindo os três
destinos (atribuído, pool, exceção).

Swagger UI ligado: `Microsoft.AspNetCore.OpenApi` (já presente) gera o JSON da spec em
`/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` (só a UI, sem o SwaggerGen deles —
sem gerador de spec duplicado) renderiza em `/swagger`, ambos só em Development. Enums
serializados como string no JSON (`JsonStringEnumConverter`), mesma decisão já tomada pro
banco — legível no Swagger, não quebra se a ordem do enum mudar no C#.

Pendências conhecidas: o endpoint não persiste o `Protocolo` (não existe porta de escrita
pra protocolo ainda, só leitura das outras entidades) — é efetivamente uma "prévia" de
distribuição. Não há endpoint pra cadastrar tipo de ato/conferente/equipe/escrevente ainda;
testar hoje exige inserir linha manualmente (DBeaver ou psql). Nenhuma seed de dados existe.

## Autenticação e autorização

Fechado o buraco de segurança que existia até aqui (nenhum endpoint tinha proteção nenhuma).
Escopo deliberadamente mínimo — só o suficiente pra ter login e checagem de papel no servidor
(RNF-04); cadastro completo de usuário fica pra quando RF-25 (cadastro de conferentes) entrar.

- **`Usuario`/`Papel`** em `Dispatch.Domain/Usuarios/` — entidade simples (id, nome, email,
  senha_hash, papel, ativo). O algoritmo de hash em si não mora aqui (é infraestrutura).
- **`Autenticar`** em `Dispatch.Application` — porta `IUsuarioRepository` (leitura por e-mail),
  `IHashDeSenha` (verificação) e `IEmissorDeToken` (emissão). Resultado (`Autenticado`/
  `Rejeitado`) não diferencia e-mail inexistente de senha errada — evita dar pista de quais
  e-mails estão cadastrados.
- **Hash de senha**: `PasswordHasher<TUser>` do pacote `Microsoft.Extensions.Identity.Core` —
  só o hasher, sem trazer o ASP.NET Core Identity inteiro (não precisamos de reset de senha,
  confirmação de e-mail, external login etc.; o requisito pede só e-mail+senha).
  `HashDeSenhaAspNetCore` usa `PasswordHasher<object>` porque a implementação não usa a
  instância do usuário pra nada — é só um parâmetro de extensibilidade da API.
- **JWT**: `EmissorDeTokenJwt` (`System.IdentityModel.Tokens.Jwt`) inclui o papel como
  `ClaimTypes.Role`, não claim customizada — deixa `RequireRole`/`[Authorize(Roles=...)]`
  funcionarem prontos, sem policy customizada. Config em `Jwt:*` no `appsettings` — a chave
  de assinatura do ambiente de Development é só pra local, a de produção entra como secret
  do Fly.io (`fly secrets set Jwt__ChaveDeAssinatura=...`), igual a connection string do Neon.
- **`POST /auth/login`** (`AllowAnonymous`) e `/protocolos/distribuir` agora exige papel
  `Distribuidora` (`RequireAuthorization(policy => policy.RequireRole(...))`) — importação/
  distribuição é ação de gestão (seção 3 do requisito).

### Duas armadilhas do EF Core que já apareceram duas vezes

Ao adicionar uma entidade nova com propriedade `bool`/`int`/`Guid` só de `get` (sem setter),
o EF Core às vezes falha o constructor binding em tempo de design (`dotnet ef migrations add`)
achando que não consegue ligar o parâmetro — mesmo a propriedade existindo. Aconteceu com
`Conferente.NaEscala`/`CargaAtual` e de novo com `Usuario.Ativo`. Solução: declarar a
propriedade explicitamente na `IEntityTypeConfiguration<T>` (`builder.Property(x => x.Ativo)`)
antes de gerar a migration — parece bobo mas resolve.

### Duas armadilhas do Swagger/OpenAPI (Microsoft.OpenApi 2.x)

1. `Microsoft.AspNetCore.OpenApi` v10 gera `"enum": [...]` pro schema de um enum, mas sem
   `"type": "string"` — o Swagger UI não sabe rotular isso e mostra "any". Corrigido com um
   `IOpenApiSchemaTransformer` (`EnumSchemaTransformer`) que preenche o `type` que falta.
2. Uma classe pode implementar `IOpenApiDocumentTransformer` **e**
   `IOpenApiOperationTransformer` ao mesmo tempo, mas isso não basta — cada papel precisa ser
   registrado separadamente (`AddDocumentTransformer<T>()` e `AddOperationTransformer<T>()`);
   registrar só um deles faz o outro método nunca ser chamado, silenciosamente.

## Cadastro de conferentes (RF-25/RF-26/RF-27)

`Conferente` ganhou `UsuarioId` (FK única pra `usuarios`, sem navigation property no Domain —
mesmo padrão já usado em `Escrevente`/`Equipe`) e `JornadaHoras`; `Nivel` e `NaEscala` viraram
`private set` porque agora têm comportamento de domínio de verdade (`AtualizarNivelEJornada`,
`MarcarPresenca`). `Usuario.Ativo` também virou `private set` (+ `Desativar()`).

Quatro casos de uso novos em `Dispatch.Application`, todos atrás de `/conferentes` (`Api`),
exigindo papel Distribuidora:

- **`CadastrarConferente`** — cria `Usuario` (papel fixo `Conferente`, nunca escolhido) +
  `Conferente` juntos, atômico. Rejeita e-mail duplicado antes de criar qualquer coisa.
- **`EditarNivelEJornada`**, **`MarcarPresenca`** — simples, devolvem `bool` (achou/não achou).
- **`RemoverConferente`** — soft delete: `Usuario.Desativar()` + sai da escala. Não apaga a
  linha — decisão registrada aqui porque o requisito não deixa isso explícito (RF-25 só diz
  "remover"); manter histórico de quem conferiu o quê pesou mais que apagar de verdade.

**`IUnitOfWork`** (nova porta): `CadastrarConferente` precisa gravar `Usuario` + `Conferente`
como uma coisa só — daí os métodos de escrita dos repositórios (`Adicionar`) só marcam o
estado, e quem decide gravar de fato é o caso de uso, chamando `unitOfWork.SalvarAsync()`
uma vez no final. Equivalente ao `prisma.$transaction([...])`, só que explícito por injeção
em vez de um wrapper de array.

## Persistência de Protocolo

`/protocolos/distribuir` deixou de ser só prévia — agora grava de verdade. `Protocolo` ganhou
`Status` (`StatusProtocolo`: Pool/Atribuido/Conferindo/Aprovado/Reprovado/Excecao — seção 8;
só os 3 primeiros têm transição implementada, os outros nascem junto com "Minha fila", RF-19
a RF-24), `DonoId` e `MotivoExcecao`, com comportamento (`AtribuirA`, `EnviarParaPool`,
`MarcarExcecao`) em vez de setter público solto. `DistribuirProtocolo` aplica o resultado do
motor no protocolo e grava via nova porta `IProtocoloRepository` + `IUnitOfWork`.

Isso destrava o RF-27 que ficou pendente: `MarcarPresenca` (ao marcar ausente) e
`RemoverConferente` agora buscam os protocolos atribuídos à pessoa
(`IProtocoloRepository.ObterAtribuidosAAsync`) e devolvem pro pool
(`protocolo.EnviarParaPool()`) antes de gravar. Testado ponta a ponta contra o Postgres local:
atribuiu um protocolo, marcou o conferente ausente, confirmou via `psql` que o protocolo
voltou pra `Pool` com `dono_id` nulo.

Resposta de `POST /protocolos/distribuir` ganhou `ProtocoloId`, já que agora existe um
registro de verdade pra referenciar depois.

## Importação de lote (RF-05 a RF-12)

Fonte real do relatório do cartório é **PDF**, não csv/xlsx como o requisito original supõe —
descoberto com um relatório de exemplo real do cliente. Decisão: PDF fica **fora do sistema**.
O dono passa o PDF por uma IA externa antes (prompt padronizado, guardado fora do código) e
cola/envia o CSV resultante — RF-05 já cobre "colagem de linhas", então não precisou de modo
de entrada novo. `Etapa` nunca é coluna do CSV: cada relatório do cartório é 100% Pré ou 100%
Pós, então a etapa é um parâmetro do pedido de importação inteiro, não por linha.

**Linha de corte substitui dedup por número de protocolo (RF-07).** Descoberta operacional
importante: um protocolo reprovado volta a aparecer em relatórios seguintes com um novo
andamento — `Numero` não é único, não tem índice único nele, e nunca deve ganhar um. O
mecanismo real: cada pedido de importação leva um `linhaDeCorte` (instante); toda linha do
CSV com `dataHoraAndamento` igual ou anterior a esse instante é ignorada (já processada num
lote anterior), sem olhar pro número do protocolo. Isso resolve duplicata acidental (reimportar
o mesmo relatório) e reprocessamento legítimo (protocolo reprovado voltando) com um mecanismo
só. Testado ponta a ponta: reimportar o mesmo arquivo com corte posterior → 0 processadas;
mesmo número com andamento novo após o corte → processado como registro novo, histórico
preservado (2 linhas pra 1 número no banco, de propósito).

`Protocolo.AndamentoEm` (novo campo, obrigatório) guarda esse instante — é também o
`momentoDeReferencia` usado por `Prazo.CalcularVencimento`, **não** mais "agora"
(`IRelogio.Agora`, que era o comportamento antigo e errado pra lote: um protocolo criado às
9h e importado às 14h não pode ter o prazo contado a partir das 14h). O endpoint avulso
`/protocolos/distribuir` continua usando `IRelogio.Agora` como `AndamentoEm`, já que ali não
existe um andamento de relatório de verdade por trás — é só simulação manual.

`Protocolo.TipoAtoId` virou `Guid?` — tipo de ato desconhecido (RF-09) é **sinalizado**, nunca
criado sozinho no catálogo (a seção 7, "aprendizado sem ia", já deixa claro que evolução de
catálogo passa por proposta revisada por humano, não por criação automática na importação).
`MotorDistribuicao` já tratava tipo desconhecido como exceção — só precisou aceitar nulo.

**`Escrevente` é criado automaticamente quando desconhecido** (confirmado com o dono — RF-09):
nasce sem equipe, aparece sinalizado no resumo (`ResumoImportacao.EscreventesSemEquipe`), fica
pra alocar na Central de Regras depois. Isso já reaproveita o `ResolvedorDePrazo` existente
(que já sabia lidar com escrevente sem equipe, caindo no padrão D+1) — nenhuma lógica nova de
domínio, só a porta nova `IEscreventeRepository`.

**`ImportarLote`**: duas operações públicas (`PreVisualizarAsync`/`ConfirmarAsync`) com a
mesma lógica por dentro — a diferença é só se persiste no final ou não (RF-11: nada grava até
confirmar). Não existe "lote pendente" guardado em lugar nenhum entre prévia e confirmação;
confirmar reprocessa as mesmas linhas do zero. A sequência "resolve prazo → roda motor →
aplica resultado" foi extraída pra `AplicadorDeDistribuicao`, reaproveitada tanto por
`DistribuirProtocolo` (avulso) quanto por `ImportarLote` (lote) — nenhuma duplicação de regra.

Endpoints: `POST /protocolos/importar/pre-visualizar` e `POST /protocolos/importar/confirmar`,
mesmo formato de corpo, só Distribuidora. Testado ponta a ponta com um CSV real (10 linhas de
um relatório de Pós-Conferência de verdade): prévia não grava nada, confirmação grava os 10 +
cria os 8 escreventes distintos sem equipe, todos sinalizados corretamente.

Pendência conhecida: não existe `LoteImportacao` (entidade da seção 8) ainda — cada protocolo
importado não sabe de qual importação veio. Adiado pra quando a visão "por lote" (RF-13) for
construída; não é necessário pro que existe hoje.

## Visão de distribuição (RF-13/RF-14)

`GET /protocolos/distribuicao` — três visões do mesmo conjunto de protocolos numa resposta só
(RF-13 descreve como visões da mesma massa de dados, não telas independentes): `pool`,
`atribuidos`, `emConferencia`, `concluidos` (bucket "por status"), `excecoes` (visão própria)
e `porConferente` (atribuídos + em conferência quebrados por dono — pool não entra aqui,
já é ele mesmo uma "coluna"). Filtro opcional `?loteImportacaoId=` — sem ele, mostra todos os
protocolos que já existiram, não só de um lote.

**`LoteImportacao`** (estava adiado, decidi trazer pra agora): entidade simples (Id, Etapa,
LinhaDeCorte, ImportadoEm, TotalLinhas — seção 8). `ImportarLote.ConfirmarAsync` cria um
registro por confirmação e carimba o `Id` dele em cada `Protocolo` criado (`LoteImportacaoId`,
nulo quando o protocolo nasce fora de importação — ex.: o endpoint avulso). Só na confirmação,
nunca na prévia — senão a prévia estaria persistindo algo (RF-11).

`Semaforo.Calcular` (já existia no Domain, seção 5) finalmente tem um consumidor: cada card da
visão leva a faixa (RF-14). As duas faixas (atenção/urgência) continuam sem tabela de config —
hardcoded no endpoint com os mesmos valores de exemplo do requisito (4h/60min), até a tabela
`config` (seção 8) existir.

Testado ponta a ponta: lote confirmado gerando pool + exceção com tipo desconhecido
corretamente sinalizado (`tipoAtoId: null`), filtro por lote batendo, e um protocolo urgente
avulso aparecendo agrupado em `porConferente`.

Pendências conscientes: RF-15 (observação no card), RF-16 (redistribuir pool) e RF-17 (ação de
resolver exceção) ficaram de fora — são ações, não fazem parte da leitura em si.

## RF-15/16/17 — fecha o módulo de Distribuição

- **RF-15** (`Protocolo.Observacao`) — campo livre, editável "em qualquer estado" (sem guarda
  no Domain), exposto em cada card da visão de distribuição. `PUT /protocolos/{id}/observacao`,
  hoje só Distribuidora — RF-23 (o próprio conferente dono editando) fica pra quando "Minha
  fila" existir, é ali que faz sentido decidir a regra de "só o dono edita".
- **RF-16** (`RedistribuirPool`) — reaplica `MotorDistribuicao` a todo protocolo **sem dono**
  (Pool ou Exceção — os dois únicos status com `DonoId` nulo; `Descartado` também tem `DonoId`
  nulo mas não entra, não faz sentido redistribuir algo descartado). Não recalcula prazo — só
  reavalia elegibilidade contra o estado atual de conferentes/regras, que pode ter mudado desde
  a distribuição original. `POST /protocolos/redistribuir-pool`, devolve quantos mudaram de
  status.
- **RF-17** — duas ações na fila de exceções: `POST /protocolos/{id}/atribuir` (manual, sem
  passar pelo motor — só funciona se `Status == Excecao`, 409 caso contrário) e
  `POST /protocolos/{id}/descartar` (novo status `Descartado`, mantém `MotivoExcecao` pra
  auditoria de por que existiu). "Definir alçada" (a outra ação de resolução que RF-17 cita)
  fica de fora — depende de CRUD de `RegraAlcada`, que é Central de Regras, ainda não existe.

**Bug real encontrado testando ponta a ponta**: o endpoint avulso `/protocolos/distribuir`
nunca validava se o `TipoAtoId` do request existia no catálogo — confiava direto no Guid
recebido. Simular "tipo desconhecido" (Guid que não existe) quebrava a FK na hora de gravar,
porque só `ImportarLote` tinha esse cuidado (resolvendo por nome). Corrigido: o endpoint agora
verifica contra o catálogo antes de construir o `Protocolo`, igual `ImportarLote` já fazia.
Não pego por teste automatizado nenhum — só apareceu testando de verdade contra o Postgres
(os testes de `DistribuirProtocolo` usam fakes que não têm FK pra violar). Reforça o hábito de
sempre validar contra o banco real antes de considerar uma feature pronta.

Testado ponta a ponta: observação sobrevivendo a um redistribute, exceção "ninguém com alçada"
virando pool sozinha depois que um conferente foi cadastrado, atribuição manual com guarda de
estado (409 na segunda tentativa) e descarte preservando o motivo original. 39 testes na
Application (67 no total do projeto).

## Central de Regras — Alçada + Prazos por equipe (RF-31 a RF-38, exceto RF-32/38)

`RegraAlcada` deixou de ser `record` e virou `class` — ganhou `Origem` (`OrigemRegra`: Manual
ou Aprendida, sempre Manual por enquanto — Aprendida só nasce do módulo de aprendizado,
RF-39 a RF-41, que não existe) e comportamento (`Ativar`/`Desativar`, RF-33) em vez de ser só
um valor imutável. `Equipe` e `Escrevente` ganharam `Renomear`/`DefinirPrazos` e
`MoverParaEquipe` (RF-35/RF-36).

- **`POST/GET/DELETE /regras-alcada`, `/ativar`, `/desativar`** (RF-31, RF-33) — a Api monta
  `SujeitoAlcada`/`AlvoAlcada` a partir de um request achatado (`sujeitoNivel` XOR
  `sujeitoConferenteId`, `alvoEtapa` XOR `alvoTipoAtoId`, 400 se não bater exatamente um dos
  dois), valida que pessoa/tipo referenciados existem antes de criar.
- **`GET /conferentes/alcance`** (RF-34) — reaproveita `ResolvedorAlcada` puro: pra cada
  conferente, resolve alçada contra as duas etapas e todo o catálogo de tipos, sem tocar em
  protocolo nenhum. Zero lógica de domínio nova.
- **`/equipes`, `/escreventes/sem-equipe`, `/escreventes/{id}/mover`** (RF-35 a RF-37).
  **RF-38 (recalcular vencimentos abertos ao mudar prazo) continua de fora** — mesma pendência
  do RF-14, precisa de `Protocolo.EscreventeId`, que ainda não existe.

Testado ponta a ponta: regra negando Júnior em pré-conferência refletindo no alcance,
desativação devolvendo o alcance, remoção esvaziando a lista; e o ciclo completo de
`Equipe`/`Escrevente` — importação cria escrevente órfão → aparece em "sem equipe" → move
pra equipe recém-criada → some da lista de órfãos. 83 testes automatizados no total.

## Protocolo.EscreventeId — fecha RF-14 e RF-38

`Protocolo` ganhou `EscreventeId` (obrigatório) — o gap que tinha ficado registrado desde a
visão de distribuição (RF-14) e bloqueava o recálculo de vencimento (RF-38) foi fechado numa
tacada só.

- **RF-14**: `ProtocoloResumo` agora leva `EscreventeId`. Equipe não vai no card — dá pra
  cruzar via `GET /escreventes` (novo endpoint de listagem geral, além do `/sem-equipe` que já
  existia), que devolve o `EquipeId` de cada escrevente.
- **RF-38**: `EditarEquipe` agora recalcula de verdade. Acha os escreventes daquela equipe,
  busca os protocolos **abertos** deles (`ObterAbertosPorEscreventesAsync` — aberto é status
  != Aprovado/Reprovado/Descartado, **inclui Exceção** de propósito, porque o vencimento dela
  também fica desatualizado) e chama `protocolo.DefinirPrazo(prazoNovo, protocolo.AndamentoEm)`
  pra cada um — a referência continua sendo o `AndamentoEm` original, nunca "agora".

**Efeito colateral que precisou de conserto**: o endpoint avulso `/protocolos/distribuir`
construía um `Escrevente` só em memória, nunca persistido — com `EscreventeId` virando FK
obrigatória, isso quebraria a gravação. Alinhei esse endpoint com o mesmo padrão do
`ImportarLote` (busca por nome, cria sem equipe se for a primeira vez) — e simplifiquei
`DistribuirProtocoloRequest`, que tinha `EscreventeId`/`EquipeId` redundantes desde antes dessa
persistência existir; agora é só `EscreventeNome`, igual toda linha de importação.

Testado ponta a ponta: card carregando `escreventeId` de verdade, e o cenário completo do
RF-38 — criar equipe, mover escrevente pra ela, distribuir um protocolo, mudar o prazo da
equipe, confirmar que o `vencimento_em` recalculou a partir do `andamento_em` original (não de
"agora"). 84 testes automatizados no total.

## Minha fila (RF-19 a RF-24)

Primeiro módulo construído pro papel **Conferente** — até aqui todo endpoint era Distribuidora.
`Protocolo` ganhou `IniciadoEm`/`ConcluidoEm` (+ `Duracao` computada, só existe depois de
concluído) e três transições novas (`IniciarConferencia`, `Aprovar`, `Reprovar`) — a mesma
regra de sempre: o Domain só sabe fazer a transição, quem decide se ela é permitida é o caso
de uso.

- **`VerificadorDeAlcada`** (helper interno, não é caso de uso) — "esse conferente pode pegar
  esse protocolo" é sempre a mesma pergunta (etapa permitida E tipo permitido via
  `ResolvedorAlcada`), reaproveitado por `ObterMinhaFila` (filtra o pool) e `PegarProtocolo`
  (bloqueia a ação, não só esconde).
- **`ObterMinhaFila`** (RF-19) — três colunas: pool disponível (já filtrado pela alçada),
  atribuídos e em conferência, os dois últimos só do próprio conferente.
- **`PegarProtocolo`** (RF-20) — só sai do `Pool` se estiver dentro da alçada.
- **`IniciarConferencia`** (RF-21) — só se `Atribuido` e dono bater; limite de atos
  simultâneos hardcoded em `1` (mesma pendência do semáforo — tabela `config`, seção 8,
  ainda não existe).
- **`ConcluirConferencia`** (RF-22) — aprova ou reprova, só se `Conferindo` e dono bater;
  grava `ConcluidoEm` (e, por extensão, `Duracao`).
- **`ObterConcluidosHoje`** (RF-24) — "hoje" calculado a partir de `IRelogio.Agora`, nunca
  hardcoded.
- **`DefinirObservacao`** (RF-15/RF-23) passou a aceitar `conferenteRestritoId` opcional —
  mesmo caso de uso serve Distribuidora (sem restrição) e o conferente dono (restrito);
  devolve um enum (`Sucesso`/`NaoEncontrado`/`NaoEhSeu`) em vez de `bool`, porque agora há
  três desfechos possíveis, não dois.

**`ClaimsPrincipal` como parâmetro de endpoint, pela primeira vez no projeto**: minimal API
resolve isso automaticamente (não precisa registrar nada) — usado em
`PUT /protocolos/{id}/observacao` e em todo `/minha-fila` pra ler `ClaimTypes.NameIdentifier`
(`Usuario.Id`) e resolver o `Conferente` correspondente via nova porta
`IConferenteRepository.ObterPorUsuarioIdAsync`. `PUT /protocolos/{id}/observacao` agora aceita
os dois papéis (`RequireRole(Distribuidora, Conferente)`) e decide a restrição por dentro,
olhando `usuario.IsInRole(...)`.

Endpoints novos, todos sob `/minha-fila`, `RequireRole(Conferente)` — primeiro grupo do
projeto exclusivo desse papel: `GET /` (RF-19), `POST /{id}/pegar` (RF-20),
`POST /{id}/iniciar` (RF-21), `POST /{id}/concluir` com `{aprovado: bool}` (RF-22),
`GET /concluidos-hoje` (RF-24).

Testado ponta a ponta contra o Postgres local: conferente pegando um protocolo do pool,
Distribuidora e o próprio dono editando a observação, um segundo conferente recebendo 403 ao
tentar editar a mesma observação (e o valor no banco confirmado intacto via `psql`), limite de
simultâneos barrando um segundo `iniciar` com 409, e conclusão gravando `Duracao` corretamente
em `GET /minha-fila/concluidos-hoje`. 103 testes automatizados no total (28 Domain + 75
Application).

## Aprendizado sem IA (RF-39 a RF-41)

Seção 7: "o sistema que aprende é contagem, não modelo". Decisão de arquitetura tomada com o
dono antes de codificar: **não existe tabela `evento_decisao`** como o documento sugere na
seção 8. Das quatro propostas da tabela, três são puras funções de dados que já existem
(protocolo, escrevente, conferente, equipe) — só "Tipo desconhecido" precisava de um dado
novo: `Protocolo.TipoAtoNomeOriginal` (preenchido só quando `TipoAtoId` é nulo — o texto bruto
do relatório, que antes se perdia). Um log de eventos genérico seria infraestrutura para um
caso de uso que não existe ainda; se aparecer uma proposta futura que dependa mesmo de
"previsto vs. realizado" solto, a tabela nasce ali.

- **`Dispatch.Domain/Aprendizado/`** — `PayloadSugestao` (hierarquia fechada, 4 variantes:
  `TipoDesconhecido`, `PrazoIrreal`, `EscreventeOrfao`, `RiscoQualidade`), `Sugestao`
  (`Pendente`/`Aplicada`/`Descartada`, com `Chave` pro dedup e `DescartarAte` pro descarte com
  memória — os dois mecanismos da seção 7, junto com o limiar mínimo de casos), e
  `GeradorDeSugestoes` — quatro funções puras, uma por proposta, com os limiares do documento
  como parâmetro (default), a mesma lógica de "configuração ainda hardcoded" das faixas do
  semáforo. `PrazoIrreal` mapeia a duração real (percentil 80) pra faixa mais próxima usando
  durações "típicas" de referência (1h/12h/36h/60h) — é uma aproximação consciente, a seção 11
  do próprio documento já assume que "vence no fim do dia" é fuzzy.
- **`GerarSugestoes`** — o "job diário" da seção 7, sob demanda (`POST /sugestoes/gerar`, só
  Distribuidora) — não existe scheduler/`IHostedService` no projeto ainda, decisão adiada
  igual versionamento de API. Roda as 4 funções do gerador e decide, por chave: nova (não
  achou, ou achou descartada com a janela vencida) / atualiza ocorrências (achou pendente) /
  ignora (achou descartada dentro da janela, ou já aplicada).
- **`AplicarSugestao`** (RF-40) — cada variante do payload mapeia num verbo do requisito:
  `TipoDesconhecido` → adiciona ao catálogo (`ITipoAtoRepository.Adicionar`, primeira escrita
  nesse repositório); `PrazoIrreal` → `Equipe.DefinirPrazos` + recalcula vencimentos abertos
  (`RecalculoDeVencimentos`, extraído de `EditarEquipe` pra ser reaproveitado aqui; RF-38);
  `EscreventeOrfao` → `Escrevente.MoverParaEquipe`; `RiscoQualidade` → cria `RegraAlcada` nova
  com `Origem.Aprendida` negando o nível pro tipo (primeira regra criada fora do fluxo manual
  de `CriarRegraAlcada`).
- **`DescartarSugestao`** (RF-40) — "silencia com memória": 30 dias hardcoded, mesma pendência
  de configuração das outras constantes do sistema.
- **`ListarSugestoesPendentes`** (RF-39) e **`ListarHistoricoSugestoes`** (RF-41).

**Bug real encontrado testando ponta a ponta, não pego pelos testes com fake**: `Sugestao`
passa pelo mesmo problema que `RegraAlcada` já tinha resolvido — o payload (sum type) é
"achatado" pra persistência (`SugestaoRegistro`, mesmo padrão de `RegraAlcadaRegistro`), então
o objeto de Domain que `ObterPorIdAsync`/`ObterPorChaveAtivaAsync` devolvem é uma tradução
nova a cada chamada, **não** a instância que o EF Core rastreia. Chamar `sugestao.Aplicar(...)`
nesse objeto mutava só a cópia em memória — o `SaveChanges` não via nenhuma mudança, e a
sugestão continuava `Pendente` no banco pra sempre (confirmado com `psql`: `status` nunca saía
de `Pendente`, aplicar duas vezes devolvia 204 as duas vezes). `RegraAlcadaRepository` já
evitava essa armadilha (`AtivarAsync`/`DesativarAsync` mexem direto no registro rastreado, não
chamam `RegraAlcada.Ativar()`) — só que eu não tinha reparado no padrão até esbarrar no mesmo
bug aqui. Corrigido com o mesmo approach: `ISugestaoRepository` ganhou
`AtualizarEvidenciaAsync`/`AplicarAsync`/`DescartarAsync`, que buscam o registro de novo e
mutam ele direto; os métodos `Sugestao.Aplicar`/`Descartar`/`AtualizarEvidencia` do Domain
continuam existindo (documentam a regra, são exercitados pelas fakes nos testes), só não são
mais o caminho que o repositório real usa pra persistir. Lição reforçada: **fakes com lista
em memória não têm esse tipo de "desconexão do change tracker" pra revelar** — o mesmo
princípio do bug de FK do módulo de Distribuição (fakes não têm FK pra violar), agora também
vale pra "fakes não têm change tracker pra perder a referência".

Testado ponta a ponta contra o Postgres local, os quatro caminhos: importar 5 linhas de um
tipo desconhecido → resolver na mão (RF-17) → gerar → aparece com a moda do nível certa →
aplicar → tipo entra no catálogo, sugestão vira `Aplicada`, aplicar de novo dá 409; distribuir
6 protocolos de um tipo conhecido pra um conferente Júnior, reprovar 4 → gerar → risco de
qualidade aparece (67% reprovação) → aplicar → `RegraAlcada` nova com `Origem.Aprendida`
aparece em `/regras-alcada` e já reflete em `/conferentes/alcance` (Júnior perde o tipo);
segundo ciclo de tipo desconhecido → descartar → `descartarAte` gravado 30 dias à frente →
gerar de novo não traz de volta (janela de memória) → descartar de novo dá 404 (já não está
pendente). 128 testes automatizados no total (40 Domain + 88 Application).

## Decisões adiadas conscientemente

- **Versionamento de endpoints** (`/v1/...` ou por header): não faz sentido ainda — não há
  nenhum consumidor de verdade (o `dispatch-web` não existe), então não há contrato pra
  quebrar. Reavaliar quando o front-end começar ou antes do primeiro deploy em produção; nesse
  ponto um prefixo de rota simples provavelmente já resolve, sem precisar de biblioteca.
