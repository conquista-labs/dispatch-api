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

## Decisões adiadas conscientemente

- **Versionamento de endpoints** (`/v1/...` ou por header): não faz sentido ainda — não há
  nenhum consumidor de verdade (o `dispatch-web` não existe), então não há contrato pra
  quebrar. Reavaliar quando o front-end começar ou antes do primeiro deploy em produção; nesse
  ponto um prefixo de rota simples provavelmente já resolve, sem precisar de biblioteca.
