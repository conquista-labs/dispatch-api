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

## Deploy — no ar

`https://lab-dispatch-api.onrender.com` (Web Service Docker no **Render**, plano free). Migrou
do Fly.io porque o free trial da Fly acabou e passou a exigir cartão — o dono recusou colocar
cartão, e o Render tem um free tier sem cartão (mesma decisão já usada em outro projeto dele).
Nada de código mudou na migração: mesmo `Dockerfile` (build multi-stage, porta 8080), mesmo
Neon, mesmo Netlify — só a plataforma que builda/roda o container.

**`render.yaml`** (raiz, Blueprint) descreve o serviço — nome, Dockerfile, região, plano,
`healthCheckPath: /health`, e as env vars não-secretas. As duas secretas
(`ConnectionStrings__DispatchDb`, `Jwt__ChaveDeAssinatura`) ficam de fora do arquivo
(`sync: false`) e são preenchidas manualmente uma vez, na criação do serviço via dashboard
("New +" → "Blueprint" → conectar o repo) — nunca versionadas.

**Gotcha da migração**: serviços Docker no Render não usam só o `EXPOSE` do Dockerfile pra
saber a porta — precisa também de uma env var `PORT` explícita (aqui, `PORT=8080`, batendo com
o `ASPNETCORE_URLS=http://+:8080` fixado no Dockerfile) ou o roteamento do Render nunca acha o
container (`x-render-routing: no-server` no proxy, mesmo com a app rodando e logando "Your
service is live" normalmente — o sintoma engana, parece que a app caiu, mas é só um mismatch de
porta entre app e proxy). Também vale notar: logo depois do primeiro deploy, o roteamento pode
levar alguns minutos a mais pra propagar (primeiras chamadas deram 404/no-server, resolveu
sozinho pouco depois) — não é preciso re-deployar se isso acontecer, só esperar um pouco.

Sem `min_machines_running`/equivalente configurado — o plano free do Render também hiberna o
serviço por inatividade e acorda com cold start na próxima chamada, mesmo trade-off que o Fly
já tinha.

**Auto-deploy no ar, confirmado pelo dono** — push em `main` já dispara um novo deploy sozinho
no Render, sem passo manual nenhum (diferente do front no Netlify, que é sempre
`netlify deploy --prod --build` manual). Relevante pra sessões futuras: depois de um `git push`
num repositório clonado localmente, o código já está indo pro ar sozinho — não presumir que
falta um passo de deploy explícito do back como se presume pro front.

**CORS não é Development-only.** Uma policy só, sempre ativa, com a origem vindo de config
(`Cors:AllowedOrigin` — `appsettings.Development.json` fixa `localhost:5173`; produção é env
var do Render `Cors__AllowedOrigin` apontando pra URL do Netlify). Trocar de host do front não
pede recompilar a API, só atualizar essa variável no dashboard do Render.

**Sem endpoint de registro público** — só existe `POST /auth/login`. A primeira conta
Distribuidora de cada ambiente entra direto no banco (hash da senha gerado com o mesmo
`PasswordHasher<object>` do `HashDeSenhaAspNetCore`, senão `Autenticar` não reconhece o hash na
hora de verificar login). Feito uma vez manualmente pra produção, antes da migração — a conta e
o banco (Neon) não mudaram, só quem hospeda a API.

Variáveis de ambiente em produção (dashboard do Render, nunca em arquivo do repo):
`ASPNETCORE_ENVIRONMENT=Production`, `PORT=8080`, `ConnectionStrings__DispatchDb` (Neon),
`Jwt__ChaveDeAssinatura` (gerada nova na migração — invalida sessões JWT antigas, esperado),
`Jwt__Emissor`, `Jwt__Audiencia`, `Jwt__ExpiracaoMinutos`, `Cors__AllowedOrigin`.

O app antigo no Fly (`lab-dispatch-api.fly.dev`) ficou parado (trial expirado) — não foi
excluído, só abandonado; não custa nada enquanto não reativar.

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

**Regra confirmada com a operação** (fechava o ponto em aberto da seção 11 do documento — "os
nomes e prazos reais de cada equipe"): D+0 vence no fim do dia atual (segue modelado como
"início do dia seguinte", mesmo instante, cálculo mais simples); **D+1 são 24 horas corridas a
partir da referência, D+2 são 48 horas** — não "fim do dia seguinte"/"fim de dois dias depois"
como a primeira versão assumia. Além disso, **os três (D+0/D+1/D+2) agora consideram dia útil**:
se o vencimento calculado cai num sábado ou domingo, empurra pro próximo dia útil (segunda), no
mesmo horário — sem calendário de feriado, só fim de semana. "1 hora" fica de fora desse ajuste
de propósito: é o prazo mais urgente do sistema (RF-13), empurrar isso pra depois de um fim de
semana contradiz o motivo dele existir.

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

**RF-08 (prévia por linha)**: `ResumoImportacao.Linhas` — `IReadOnlyList<LinhaPreviaImportacao>?`,
populado só em `PreVisualizarAsync` (nulo em `ConfirmarAsync`, já que o front não usa e um lote
pode ter centenas de linhas — não vale carregar isso na resposta de gravar). Cada linha já tinha
prazo/equipe/avaliação de alçada resolvidos dentro do laço de `ProcessarAsync`; só não sobreviviam
além do `switch` que os reduzia a contador agregado — RF-08 é aproveitar esse cálculo, não uma
regra nova.

- **Equipe vai por nome (`string?`), não por Id.** Diferente de `ProtocoloResumo` (que só manda
  `EscreventeId` e deixa o front cruzar com `GET /escreventes`), aqui o escrevente da linha pode
  nem existir no banco ainda — RF-09 só cria o registro na confirmação. Não dá pra cruzar o que
  ainda não existe.
- **`Prazo` vai cru (`TipoPrazo?`), sem formatar "D+1"/"1 hora".** Mesmo padrão de `FaixaSemaforo`
  e de `EquipeResponse` (Central de regras): back manda o fato, front decide o rótulo. O texto
  "5º andar · pós-conferência" do protótipo é `Equipe.Nome` + a etapa que o front já sabe (é
  parâmetro do próprio pedido de importação, não varia por linha) — nada disso precisa viajar
  formatado.
- **Linha antes da linha de corte (`JaExiste: true`) não resolve nada.** Ela nunca é distribuída
  de verdade nesta operação, então `Equipe`/`Prazo`/`VencimentoEm`/`Semaforo` ficam nulos — não
  tem fato nenhum pra mostrar além de "já processada antes".
- **`ComAlcada`** (RF-10, "quantos têm alçada pra este tipo/etapa") exigiu um ajuste em
  `ResultadoDistribuicao.Atribuido`, que só guardava o candidato escolhido — sem a lista completa
  de elegíveis, não dava pra saber quantos qualificavam quando o motor manda direto pra alguém
  por urgência. Ganhou `Elegiveis` (mesma forma que `EnviadoParaPool`/`Excecao` já tinham),
  preenchida em `MotorDistribuicao.cs` com a mesma lista que já existia em escopo — sem lógica
  nova de domínio.
- Faixas do semáforo (`FaixaAtencao`/`FaixaUrgente`) chegam como parâmetro em
  `PreVisualizarAsync` — mesmos valores hardcoded 4h/60min de `DistribuicaoEndpoints`, duplicados
  ali até a tabela de config (seção 8) existir. `ConfirmarAsync` passa `TimeSpan.Zero` pros dois
  porque `persistir: true` nunca monta `Linhas` — os valores nunca chegam a ser lidos.

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

## GET /conferentes — fecha o gap encontrado planejando o front

Levantamento rápido de tela × endpoint pro `dispatch-web` (RF-25) achou um buraco real: não
existia **nenhuma** leitura que juntasse `Conferente` (Domain) com `Usuario.Nome`/`Email` —
`Conferente` não guarda nome, isso é dado de `Usuario`, e toda leitura existente até aqui
(`ObterAlcancePorConferente`, a visão "por conferente" da Distribuição, `Minha fila`) só
devolvia `conferenteId` puro. Não dava pra montar nenhuma tela que mostra "quem é" um
conferente.

`ListarConferentes` (novo caso de uso) resolve isso buscando os dois lados e juntando em
memória — `IUsuarioRepository` ganhou `ObterVariosPorIdsAsync` (busca em lote pelos
`UsuarioId`s dos conferentes, evita N+1 de `ObterPorIdAsync` um de cada vez). Devolve
`ConferenteComUsuario` (record de leitura só com primitivos — não é entidade de Domain
vazando, mesmo padrão de `AlcanceDoConferente`/`ResumoImportacao`), exposto em
`GET /conferentes` (Distribuidora, mesmo grupo dos outros endpoints de conferente). Testado
ponta a ponta: nome e e-mail batendo com o que foi cadastrado, inclusive pra conferentes
antigos já existentes no banco. 130 testes automatizados no total (40 Domain + 90
Application).

## RF-28 (carga real) e RF-30 (aviso de cobertura) — planejando a tela Conferentes do front

Antes de construir a tela "Conferentes" no `dispatch-web`, levantamento contra RF-25 a RF-30
achou um bug adormecido e dois gaps reais.

**Bug**: `Conferente.CargaAtual` é coluna persistida desde o início, mas nunca era atualizada
depois do cadastro (sempre `0`). Isso não é só um problema de exibição — `MotorDistribuicao.cs`
usa esse campo pra desempatar quem pega um protocolo urgente
(`elegiveis.OrderBy(a => a.Conferente.CargaAtual).First()`); com todo mundo empatado em 0 pra
sempre, esse desempate nunca funcionou de verdade (sempre caía no primeiro da ordenação
estável, por acaso).

**Correção — carga sempre recalculada na leitura, nunca guardada**: em vez de escrever
`CargaAtual` toda vez que um protocolo é atribuído/devolvido (duplicaria estado e
dessincronizaria mais cedo ou mais tarde — mesmo raciocínio já aplicado a `Semaforo.Calcular`,
sempre computado, nunca persistido), `ConferenteRepository.ObterTodosAsync`/`ObterNaEscalaAsync`
agora projetam `Conferente` com uma subquery correlacionada contando
`Protocolos` com `DonoId` igual e `Status` em `Atribuido`/`Conferindo`. Testado contra o
Postgres local (não só unit test com fake): atribuiu um protocolo a um conferente, `GET
/conferentes` passou a mostrar `cargaAtual: 1` na hora, sem nada escrito na tabela.

**Importante, e é por isso que só essas duas leituras mudaram**: `ObterPorIdAsync` (usado por
`EditarNivelEJornada`/`MarcarPresenca`/`RemoverConferente` — os três únicos fluxos que *mutam*
um `Conferente`) continua uma query simples, sem projeção — uma entidade construída dentro de
um `.Select()` sai desconectada do change tracker do EF, e mutar/salvar ela não gravaria nada
(mesma armadilha já documentada pra `RegraAlcada`/`Sugestao` — ver seção de Aprendizado/Central
de regras).

**RF-28, capacidade estimada**: `ListarConferentes` ganhou `CapacidadeEstimada` no
`ConferenteComUsuario` — `jornadaHoras × 60 ÷ 18min`, arredondado, mínimo 1. 18min não é
invenção do protótipo: é premissa explícita da seção 11 do documento de requisitos ("tempo
médio de 18min por ato para cálculo de capacidade") — hardcoded do mesmo jeito que as faixas do
semáforo (4h/60min), até existir tabela de config.

**RF-30, aviso de cobertura**: `ObterCoberturaDeAlcada` (novo caso de uso) — "tipo em
circulação" é qualquer `TipoAtoId` presente nos protocolos de hoje (não o catálogo inteiro; um
tipo sem nenhum protocolo não é um problema de cobertura agora). Reaproveita
`ObterAlcancePorConferente` em vez de rodar `ResolvedorAlcada` de novo — `TiposPermitidosIds` já
resolve "esse conferente alcança esse tipo" checando só o eixo tipo, sem cruzar com etapa
(mesma simplificação do protótipo aprovado, que usa uma etapa fixa como proxy pra essa
pergunta). Cruza com quem está `NaEscala` (não com todo o cadastro) — alguém de folga não conta
como cobertura hoje. Devolve duas listas (`SemNinguemHabilitado`/`DependeDeUmaPessoa`), expostas
em `GET /conferentes/cobertura`. Tipo fora do catálogo (protocolo com `TipoAtoId` nulo, RF-09)
não entra em nenhuma das duas — já é sinalizado como "tipo desconhecido" na importação, não é
uma questão de alçada. Testado contra o Postgres local: atribuiu um protocolo de um tipo com só
um conferente na escala habilitado, `GET /conferentes/cobertura` devolveu esse tipo em
`dependeDeUmaPessoa` na hora.

7 testes novos (`ObterCoberturaDeAlcadaTests` + 2 em `ListarConferentesTests` pra
`CapacidadeEstimada`) — 103 testes de Application no total (143 com Domain).

## Conferentes, ajustes achados testando a tela de verdade (RF-25)

Três coisas que só apareceram usando a tela no navegador, não em teste isolado:

- **`GET /conferentes` sem `ORDER BY`**: a lista "pulava" de posição a cada ação (qualquer
  edição invalida a query inteira no front, RF-26/27 disparam refetch). Postgres não garante
  ordem estável sem `ORDER BY` explícito. `ListarConferentes` agora ordena por nome.
- **Remover não filtrava quem foi removido**: `GET /conferentes` devolvia todo mundo, ativo ou
  não — um conferente removido (soft delete, `Usuario.Ativo = false`) continuava aparecendo pra
  sempre. Cogitei filtrar isso no front, mas não faz sentido: `ativo=false` significa "não é
  mais conferente" pra qualquer tela que use esse endpoint (a de Conferentes, o seletor de
  atribuição manual em Exceções, o que mais vier), então o filtro entrou em `ListarConferentes`
  — uma correção na fonte em vez de confiar que cada consumidor lembra de repetir o filtro.
- **`EditarPerfilConferente`** (novo caso de uso, RF-25 "editar" — faltava): nome/e-mail são do
  `Usuario`, separado de `EditarNivelEJornada` (que só mexe em campos do `Conferente`) de
  propósito — são agregados diferentes, e o front trata como ações diferentes (nível/jornada
  direto no card; nome/e-mail um fluxo à parte). `Usuario.Nome`/`Email` ganharam setter privado
  + `AtualizarPerfil(nome, email)`. Unicidade de e-mail checada só quando o e-mail muda de
  verdade (`ExisteComEmailAsync` sozinho rejeitaria a pessoa contra o próprio e-mail atual, um
  falso positivo). `PUT /conferentes/{id}/perfil`, `204`/`404`/`409` (e-mail já cadastrado).
  Testado contra o Postgres local: editar mantendo o mesmo e-mail passa, editar pro e-mail de
  outro conferente dá 409, editar de verdade persiste e aparece no próximo `GET /conferentes`.

4 testes novos (`EditarPerfilConferenteTests`) — 109 testes de Application no total (149 com
Domain).

**Ordenação estável, segunda rodada**: `OrderBy(Nome)` sozinho não bastava — dois conferentes de
teste têm o mesmo nome (só o e-mail muda), e nomes empatados continuavam sujeitos à ordem
"crua" instável do Postgres entre uma leitura e outra (achado pelo dono clicando nos steppers
de jornada e vendo a lista trocar de ordem de novo). `ThenBy(Id)` fecha o desempate de vez —
`Guid` é sempre único, differente de nome.

## GET /conferentes/{id}/fila e /concluidos-hoje — Distribuidora vendo a fila de alguém (RF-19)

O protótipo aprovado tem "Minha fila" no menu de quem é gestão também (não só Conferente) —
achado revendo o protótipo depois que o dono mandou print perguntando por isso; a varredura
inicial (só a tela isolada) não pegou porque o nav do protótipo é construído com um filtro
(`soGestao || !d[3]`) que libera todos os itens pra gestão, não só os marcados como "só
gestão" — "Minha fila" tem essa flag em `false`, então aparece pros dois papéis, só que pra
Conferente é a própria fila e pra Distribuidora é a fila de quem ela escolher (o protótipo
cicla entre conferentes com um botão "Ver como outro conferente").

Nada de caso de uso novo — `ObterMinhaFila`/`ObterConcluidosHoje` (Application) já recebiam um
`Conferente` qualquer como parâmetro, nunca dependeram de "quem está logado" (isso sempre foi
responsabilidade do endpoint, via `ResolverConferenteAsync` a partir do JWT em
`MinhaFilaEndpoints.cs`). Só precisou de dois endpoints novos em `ConferenteEndpoints.cs`
resolvendo o `Conferente` pelo `id` da URL em vez do token — `GET /conferentes/{id}/fila` e
`GET /conferentes/{id}/concluidos-hoje`, `RequireRole(Distribuidora)` (o grupo `/conferentes`
já exige isso). `ParaResumo`/`ParaResumoConcluido` (mapeamento `Protocolo` → DTO) viraram
`internal` em vez de `private` em `MinhaFilaEndpoints` pra reaproveitar sem duplicar — isso é
mapeamento de verdade, diferente das faixas do semáforo (que ficam duplicadas de propósito por
serem config, não lógica).

Testado contra o Postgres local: `404` pra id de conferente inexistente, `403` quando um
Conferente tenta chamar (o grupo já barra por papel, nem chega a resolver o id), resposta
válida (vazia, sem protocolo nenhum atribuído no momento do teste) pro conferente de verdade.

## Login devolve o usuário + GET /auth/me

Decisão tomada planejando o `dispatch-web`: o front **nunca decodifica o JWT** pra saber quem
está logado — token é credencial (o que vai no `Authorization` header), não fonte de dado de
perfil. Misturar os dois acopla o front à forma exata das claims e não resolve sozinho o caso
de refresh de página (SPA guarda o token, mas "quem é o usuário" não sobrevive a um F5 se a
única fonte for a resposta do login).

- **`POST /auth/login`** agora devolve `{ token, usuario: { id, nome, email, papel } }` —
  `ResultadoAutenticacao.Autenticado` ganhou os campos do usuário, evitando uma chamada extra
  logo depois de logar.
- **`GET /auth/me`** (novo, autenticado, qualquer papel) devolve o mesmo formato de `usuario`,
  resolvido a partir do `ClaimTypes.NameIdentifier` do token — é o que o front chama no boot
  da aplicação pra reidratar a sessão a partir de um token persistido, sem abrir o token na
  mão. `ObterUsuarioAtual` é um pass-through fino (mesmo motivo de `ListarEquipes`/
  `ListarRegrasAlcada`: a Api nunca injeta repositório direto, mesmo pra leitura trivial).

Testado ponta a ponta: login devolvendo usuário completo, `/auth/me` com token válido pros
dois papéis (Distribuidora e Conferente), 401 sem token. 132 testes automatizados no total
(40 Domain + 92 Application).

## CORS pro dispatch-web

Achado planejando o front: nenhum teste anterior (todos via `curl`) esbarrou em CORS, porque
`curl` não faz preflight — só o navegador faz. `Program.cs` ganhou `AddCors`/`UseCors`, só em
`Development`, liberando `http://localhost:5173` (porta padrão do Vite dev server). A origem
de produção entra como config quando o `dispatch-web` tiver deploy de verdade — mesma lógica
de "decisão adiada conscientemente" do versionamento de API, abaixo.

## ProtocoloResumo ganha IniciadoEm — fecha o gap do cronômetro (RF-21)

Achado construindo a tela Minha fila no front: `ProtocoloResumo` (compartilhado entre
`GET /protocolos/distribuicao` e `GET /minha-fila`) não expunha `Protocolo.IniciadoEm`, embora
o campo já existisse no domínio desde a própria feature de Minha fila. Sem isso o front não
tinha como calcular o cronômetro ao vivo do card "Em conferência" (RF-21: "arranca um
cronômetro") sem inventar dado. Campo adicionado no fim do record (evita quebrar quem já
desestrutura posicionalmente) — cálculo do tempo decorrido (`agora - iniciadoEm`) é feito no
front, atualizado por polling/tick local, não recalculado a cada request.

## Decisões adiadas conscientemente

- **Versionamento de endpoints** (`/v1/...` ou por header): não faz sentido ainda — não há
  nenhum consumidor de verdade (o `dispatch-web` não existe), então não há contrato pra
  quebrar. Reavaliar quando o front-end começar ou antes do primeiro deploy em produção; nesse
  ponto um prefixo de rota simples provavelmente já resolve, sem precisar de biblioteca.

## Painel de detalhe do protocolo (RF-18a/b) — v2 do protótipo/requisitos

O dono atualizou o protótipo e o documento de requisitos pra "v2", bem mais detalhado. A
primeira frente construída: o painel de detalhe que abre ao clicar em qualquer card de
protocolo em Distribuição, com linha do tempo, "quem pode conferir este ato" e duas ações
novas. Expôs três gaps reais que já existiam, não só coisa nova:

- **`Protocolo` não sabia quando foi atribuído.** Grava `IniciadoEm`/`ConcluidoEm` mas nunca
  guardou o instante da atribuição — sem isso não dava pra montar a linha do tempo do painel.
  `AtribuirA` agora recebe `DateTimeOffset agora` (todo call site precisou de `IRelogio`:
  `DistribuirProtocolo`, `RedistribuirPool`, `PegarProtocolo`, `AtribuirManualmente`).
  **Não limpa ao voltar pro pool** (`EnviarParaPool`) — fica como "a última vez que foi
  atribuído", já que o sistema ainda não tem um log de eventos de verdade (o `evento_decisao`
  do modelo de dados sugerido, seção 8 do documento, não existe ainda).
- **RNF-02 não era honrado até o fim.** "Toda decisão automática registra a regra que a
  originou" — verdade só na resposta HTTP do momento da distribuição
  (`AvaliacaoCandidato.RegraAplicada`), nunca persistida pra consulta depois. `Protocolo` ganhou
  `RegraAplicadaId` (nullable, **sem FK** — é auditoria, não dependência; remover uma regra de
  alçada mais tarde não pode travar a leitura de um protocolo antigo que a citou). Quando
  etapa e tipo decidem por regras diferentes, guarda a de tipo (mais específica). Só
  preenchido em atribuição automática (`AplicadorDeDistribuicao`) — atribuição humana (`PegarProtocolo`,
  `AtribuirManualmente`, a nova `AtribuirAoMenosCarregado`) deixa nulo de propósito, não é "a
  regra que decidiu", foi a pessoa.
- **Não existia "quem pode conferir este protocolo especificamente"** — só o inverso
  (`GET /conferentes/alcance`, por conferente). `ObterDetalheProtocolo` resolve isso
  reaproveitando `ResolvedorAlcada.Resolver` puro pra cada conferente na escala, nas duas
  dimensões (etapa e tipo do protocolo) — mesma resolução que o motor já faz internamente,
  zero regra nova.

Duas ações novas, escopadas a um protocolo só (diferente das que já existiam em lote ou só
pra exceção):
- **`POST /protocolos/{id}/devolver-ao-pool`** — só válido se `Atribuido`. Ação pontual da
  distribuidora, não reavaliação de alçada (isso já existe: `RedistribuirPool`, RF-16).
- **`POST /protocolos/{id}/atribuir-ao-menos-carregado`** — reaproveita `VerificadorDeAlcada`
  (já usado por `PegarProtocolo`/`ObterMinhaFila`), escolhe o elegível de menor `CargaAtual`.
  Válido em `Pool` ou `Excecao` — diferente de `AtribuirManualmente` (RF-17), que só resolve
  exceção e deixa a distribuidora escolher a pessoa; aqui a escolha é automática.

Migration `AdicionaAtribuidoEmERegraAplicadaEmProtocolos` — dois campos nullable em
`protocolos`, sem backfill (protocolos antigos ficam sem essa informação, aceitável).

**Achado no caminho**: `RedistribuirPool`/`PegarProtocolo`/`AtribuirManualmente`/`DistribuirProtocolo`
não tinham `IRelogio` injetado — nunca precisaram até `AtribuirA` passar a exigir `agora`.
Testado ponta a ponta contra o Postgres local: `atribuir-ao-menos-carregado` grava `atribuidoEm`
corretamente e rejeita com 409 quando ninguém tem alçada; `devolver-ao-pool` volta pro pool e
**preserva** o `atribuidoEm` antigo como histórico. 172 testes automatizados no total.

## Correção de resultado + pedido de reabertura (RF-24a-d) — terceira frente do "v2"

`Protocolo` ganhou `CorrigidoEm`/`ReabertoEm` (nullable) e dois métodos:
`CorrigirResultado(agora)` (inverte Aprovado↔Reprovado, exige estar num dos dois; permite
corrigir mais de uma vez dentro da janela — nem o protótipo aprovado nem o requisito proíbem)
e `ReabrirConferencia(agora)` (`Status = Conferindo`, `IniciadoEm = agora`, **`ConcluidoEm`
volta a nulo** — o ato deixou de estar concluído, `Duracao` volta a não existir até uma nova
conclusão — `DonoId` não muda).

**`PedidoReabertura` (entidade nova, mapeamento direto)** — primeiro registro de auditoria de
verdade do sistema (`Id`, `ProtocoloId`, `SolicitanteId`, `CriadoEm`, `Status`
[`Pendente`/`Aprovado`/`Negado`/`Cancelado`], `DecididoPorId`, `DecididoEm`). RNF da seção 8
("toda transição gera registro de auditoria com autor e horário") não tem infraestrutura
genérica no projeto (decisão deliberada da fase de Aprendizado — sem `evento_decisao`) — aqui
não precisou de tabela genérica porque o próprio pedido já é a linha de auditoria da decisão.

Cinco casos de uso novos, todos com `abstract record ResultadoX` fechado (padrão de
`RemoverTipoAto`/`CriarRegraAlcada`):
- **`CorrigirResultado`** — só o dono, só `Aprovado`/`Reprovado`, só dentro de
  `JanelaDeCorrecao = 15min` (constante pública no próprio caso de uso, mesma decisão de
  manter hardcoded que as faixas do semáforo — sem tabela `config` ainda).
- **`PedirReabertura`** — só o dono, só um pedido `Pendente` por protocolo por vez. Não checa
  a janela de correção de propósito: o requisito não proíbe pedir reabertura mesmo dentro da
  janela.
- **`CancelarPedidoReabertura`** — só o solicitante, só se `Pendente`.
- **`DecidirPedidoReabertura`** — aprovar chama `Protocolo.ReabrirConferencia` + marca o
  pedido `Aprovado`; negar só marca o pedido `Negado`, protocolo intacto.
- **`ReabrirConferencia`** (ação direta, sem pedido) — mesma transição, disponível pra
  qualquer protocolo `Aprovado`/`Reprovado`, usada tanto pela decisão de um pedido quanto pelo
  painel de detalhe (RF-18a).
- **`ListarPedidosReaberturaPendentes`** — join em memória (protocolo + nome do solicitante,
  via `Usuario`), mesmo padrão de `ListarConferentes`/`ObterCoberturaDeAlcada`.

Endpoints: `POST /minha-fila/{id}/corrigir-resultado`, `POST /minha-fila/{id}/pedir-reabertura`
(201 com `pedidoId`), `POST /minha-fila/pedidos-reabertura/{id}/cancelar` (Conferente);
`GET /protocolos/pedidos-reabertura`, `POST /protocolos/pedidos-reabertura/{id}/aprovar` |
`/negar`, `POST /protocolos/{id}/reabrir-conferencia` (Distribuidora).
`ProtocoloConcluidoResumo` (`/minha-fila/concluidos-hoje` e `/conferentes/{id}/concluidos-hoje`,
que reaproveita o mesmo mapeamento) ganhou `CorrigidoEm`/`PedidoReaberturaPendenteId` — o
segundo resolvido em lote (`ObterPendentesPorProtocolosAsync`, evita N+1). `DetalheProtocoloResponse`
ganhou `CorrigidoEm`/`ReabertoEm` pra a linha do tempo do painel.

**Bug real achado testando de verdade, não pego pelos testes com fake**:
`Dictionary<Guid, Guid>.GetValueOrDefault(chaveInexistente)` devolve `Guid.Empty`
(`00000000-0000-0000-0000-000000000000`), não `null` — mesmo o parâmetro de destino sendo
`Guid?`. O JSON de resposta saía com um Guid zerado em vez de `pedidoReaberturaPendenteId:
null` pra quem não tinha pedido nenhum. Corrigido tipando o `ToDictionary` explicitamente como
`Dictionary<Guid, Guid?>` (`Guid? (p) => p.Id` no seletor de valor) — sem isso o `GetValueOrDefault`
nunca alcança o `default` de `Guid?` (`null`), porque o dicionário em si já não é nullable.

Migration `AdicionaCorrecaoEReaberturaDeProtocolos` — duas colunas nullable em `protocolos` +
tabela nova `pedidos_reabertura` (FK `Restrict` pra `conferentes.solicitante_id`, `Cascade`
pra `protocolos.protocolo_id` — apagar um protocolo, se algum dia isso existir, não deveria
deixar pedido órfão).

Testado ponta a ponta contra o Postgres local, o ciclo completo: concluir → corrigir dentro da
janela (Aprovado→Reprovado) → pedir reabertura → pedir de novo dá 409 (já pendente) → cancelar
→ pedir de novo funciona (cancelado não bloqueia) → aprovar (protocolo volta a `Conferindo`,
mesmo dono, `ConcluidoEm` limpo) → concluir de novo → reabrir direto pelo painel (sem pedido)
→ reabrir de novo dá 409 (não está mais concluído) → concluir → pedir → negar (protocolo
continua `Aprovado`, intacto). 219 testes automatizados no total (54 Domain + 165 Application).

## Dashboard (RF-42-46) — quarta frente do "v2"

**RF-46 (fórmula do score) não está definida matematicamente no documento de requisitos** —
só nomeia os 4 fatores e os pesos (40% volume + 30% prazo + 20% qualidade + 10%
complexidade). A fórmula concreta veio do protótipo aprovado (fonte operacional, não o
requisito): `40*(volume/volumeMáximoDoGrupo) + 30*%noPrazo + 20*%aprovado +
10*(complexidadeMédia/complexidadeMáximaDoGrupo)` — volume e complexidade normalizados pelo
melhor do grupo no período; prazo e qualidade já são frações diretas. Faixa de bonificação:
`score >= 85` Integral, `>= 70` Parcial, abaixo Fora (limiares do protótipo).

Duas simplificações conscientes, documentadas no código (`ObterDashboard.cs`):
- **"Aprovado" usa o resultado ATUAL** (`Status == Aprovado`), não "aprovado na 1ª vez" — não
  existe histórico do resultado original antes de uma correção (RF-24a) salvo à parte; seria
  preciso inferir via `CorrigidoEm == null`, e essa não é uma leitura confiável o bastante pra
  virar métrica de bonificação sem confirmar com a operação.
- **Sem KPI de "custo por ato"** (RF-43 pede) — exigiria um dado de custo/salário que não
  existe em lugar nenhum do sistema; inventar um valor violaria a regra de não inventar dado
  que o back não calcula. Documentado como próximo passo, não esquecido. ("Cumprimento de
  prazo por equipe/etapa", a outra metade dessa mesma simplificação, foi fechado depois — ver
  seção "Cumprimento de prazo por equipe" mais abaixo.)

**`ObterDashboard`** (novo caso de uso) — `ExecutarAsync(PeriodoDashboard periodo, Guid?
conferenteRestritoId, ...)`. Período é janela móvel a partir de `IRelogio.Agora` (7/30/90
dias — RF-42 só pede "semana/mês/trimestre", não mês calendário, então essa é a leitura mais
simples que ainda cobre o requisito). `IProtocoloRepository` ganhou
`ObterConcluidosNoPeriodoAsync(desde, ate, ct)` — de TODOS os donos, diferente do
`ObterConcluidosPorConferenteAsync` que já existia pra RF-24 (só um conferente). Calcula, por
conferente: volume, %no-prazo, %aprovado, complexidade média (peso médio do `TipoAto` dos
protocolos concluídos), score e as 4 parcelas já ponderadas (não percentuais crus — o front
mostra "32.4 / 40" direto). Também KPIs agregados do período e desempenho por tipo de ato
(volume/tempo médio/%reprovação).

**RF-45 (visão do conferente) — "sem faixa de bônus" interpretado como nem a própria faixa**:
quando `conferenteRestritoId` é informado, a resposta tem só a linha do próprio conferente
(nome preenchido — é ele mesmo, não um colega) + uma linha "média da casa" com `Nome`/`Nivel`
nulos (RF-45: "sem identificar ninguém"), e **`Faixa` nula em ambas** (a leitura mais
conservadora do texto — "sem faixa de bônus" leu-se como a tela do conferente não ter esse
elemento, faixa é decisão de gestão sobre bonificação, não informação pessoal). `Parcelas`
continua preenchida pro próprio conferente (RF-45 pede explicitamente "o detalhamento das
parcelas"), só nula na linha "média da casa" (não faz sentido detalhar parcela de uma média).
`PorTipoAto` vem vazio na visão restrita — RF-45 não pede isso pro conferente.

Endpoint único `GET /dashboard?periodo=Semana|Mes|Trimestre`, `RequireRole(Distribuidora,
Conferente)` — resolve a restrição por dentro conforme o papel do token, mesmo padrão de
`PUT /protocolos/{id}/observacao`.

Testado ponta a ponta contra o Postgres local (dado real acumulado de sessões de teste
anteriores): visão Distribuidora com 2 conferentes, score/faixa/parcelas calculados
corretamente para ambos; visão Conferente só com a própria linha + média da casa sem nome,
`faixa: null`, `porTipoAto: []`; os 3 períodos respondendo 200.

**Achado escrevendo o teste, não no código de produção**: o helper de teste que simulava
"protocolo fora do prazo" usava `Prazo(TipoPrazo.D2)` — só que D2 passa pelo ajuste de
"próximo dia útil" (`Prazo.cs`), então o vencimento calculado saía num dia diferente do
esperado quando a data de referência caía perto de um fim de semana, fazendo o teste esperar
"fora do prazo" quando na verdade o vencimento real (pós-ajuste) ainda cobria a data de
conclusão. Corrigido usando `TipoPrazo.UmaHora` no helper (o único tipo que não sofre esse
ajuste — RF-13, é o prazo mais urgente do sistema, empurrar pra depois de um fim de semana
contradiria o motivo dele existir), garantindo um vencimento exato e prevísivel no teste.

225 testes automatizados no total (54 Domain + 171 Application).

## Tipos de ato — CRUD completo (RF-34a, b, d, e, f) — segunda frente do "v2"

`TipoAto` deixou de ser `record` e virou `class` (mesmo motivo de `RegraAlcada` antes:
RF-34b/d/f pedem mudança de estado ao longo do tempo) — ganhou `PesoComplexidade` (RF-34f,
mínimo 1, alimenta o score do conferente do RF-46/Dashboard, que ainda não existe) e
comportamento (`Renomear`, `Ativar`, `Desativar`, `DefinirPesoDeComplexidade`). Mapeamento
continua **direto** (como `Protocolo`), não indireto como `RegraAlcada` — `TipoAtoConfiguration`
já mapeava a entidade real sem precisar de um "Registro" achatado à parte, então
`TipoAtoRepository.ObterPorIdAsync` devolve um objeto já rastreado pelo change tracker do EF;
mutar e chamar `SaveChanges` basta, sem o `AtivarAsync`/`DesativarAsync` que `RegraAlcadaRepository`
precisa.

- **RF-34d (ativar/desativar)**: `MotorDistribuicao` ganhou um segundo motivo de exceção,
  `"tipo desativado"`, distinto de `"tipo desconhecido"` — a causa (e a resolução: reativar ou
  mesclar) é diferente, então o front precisa poder diferenciar os dois. Desativar não apaga
  histórico nenhum, só barra protocolos futuros desse tipo.
- **RF-34e (remover)**: **exclusão de verdade**, diferente do soft delete de `RemoverConferente`
  — mas só quando "sem nenhum uso". `TipoAtoId` não tem FK em lugar nenhum (é referenciado por
  Guid solto tanto em `Protocolo.TipoAtoId` quanto em `AlvoAlcada.PorTipoAto`, dentro de
  `RegraAlcada`), então o banco não bloqueia sozinho — `RemoverTipoAto` checa as duas coisas
  antes de excluir (`IProtocoloRepository.ExisteComTipoAtoAsync`, novo — existence check, não
  carrega a coleção inteira; e `IRegraAlcadaRepository.ObterTodasAsync().Any(...)`). **RF-34c
  (mesclar dois tipos) ficou de fora desta rodada** — é uma operação maior, que migraria as
  duas referências pra um Id só em vez de só bloquear a exclusão; fica documentado aqui como
  próximo passo, não foi esquecido.
- **RF-34a (leitura agregada pra tabela)**: `ListarTiposAtoComUso` — reaproveita
  `ObterAlcancePorConferente` (mesmo padrão de `ObterCoberturaDeAlcada`, RF-30) pra contar
  quantos conferentes na escala têm alçada pra cada tipo, cruzado com uma contagem de
  protocolos por `TipoAtoId` (via `ObterParaDistribuicaoAsync(null, ...)`, já existente).
  Nenhuma tabela/coluna nova só pra isso — é leitura derivada, igual `CargaAtual`/`Semaforo`.

Endpoints novos, todos `RequireRole(Distribuidora)`: `GET /tipos-ato/com-uso`,
`PUT /tipos-ato/{id}` (renomear, 409 se o nome já existir em outro tipo),
`PUT /tipos-ato/{id}/peso`, `POST /tipos-ato/{id}/ativar`, `POST /tipos-ato/{id}/desativar`,
`DELETE /tipos-ato/{id}` (409 com motivo `"tipo de ato em uso..."` se protocolo ou regra
referenciar). Migration `AdicionaPesoDeComplexidadeEmTiposAto` — `peso_complexidade integer
NOT NULL DEFAULT 1` (não `DEFAULT 0`: violaria o próprio invariante de "peso mínimo 1" pros
tipos já cadastrados).

Testado ponta a ponta contra o Postgres local: criar, renomear (com normalização e conflito de
nome), redefinir peso, desativar (reflete em `/tipos-ato/com-uso`), reativar, remover um tipo
sem uso (204) e confirmar que removê-lo de novo dá 404; tentar remover um tipo com protocolo
associado devolve 409 com o motivo certo. 191 testes automatizados no total (50 Domain + 141
Application).

## Motor de alçada v2 — lista fechada por dimensão, equipe, alçada plena, grupo de tipo

O dono atualizou o documento de requisitos e o protótipo com uma revisão grande do motor de
alçada, motivada por um bug real relatado em produção: regras "Permite" criadas pela
distribuidora não tinham efeito nenhum — o comportamento até aqui era mesmo "padrão aberto" de
verdade (ausência de regra = permitido, Permite só importava quando sobrepunha uma Nega que
existiria de outra forma). Isso batia com o texto *antigo* da seção 4, mas não com o que a
distribuidora esperava nem com o que o protótipo atualizado faz de verdade — confirmado ao vivo
navegando a nova ferramenta "Testar" da aba Alçada (simulador real de "quem pode conferir X"),
não por interpretação de markup ou do texto sozinho.

**Semântica nova, confirmada com o dono**: se um nível/pessoa tem qualquer regra Permite numa
dimensão (tipo de ato ou equipe do escrevente), isso vira **lista fechada** — tudo que não
estiver na lista é bloqueado automaticamente, mesmo sem negação explícita. Dois testes ao vivo
decisivos: nível Júnior com "pode conferir Venda e Compra, Doação, Procuração..." (5 Permite)
ficou bloqueado de um tipo fora da lista, motivo "Base por nível · X fora da alçada", sem
nenhuma regra de negação existir pra esse tipo; pessoa com "pode conferir atos de Balcão" (1
Permite de equipe) ficou bloqueada de "sem equipe" pelo mesmo motivo.

**Algoritmo** (`ResolvedorAlcada.cs`), organizado por **família de alvo** (Etapa/Tipo/Equipe —
`PorTodosOsAtos` conta como família Tipo): dentro do escopo que "toca" a família (pessoa
primeiro, se ela tiver qualquer regra ativa naquela família — senão nível), resolve nesta
ordem: (1) Nega específica do alvo consultado sempre vence; (2) Permite específica; (3) alçada
plena (só família Tipo) — permite qualquer tipo, mas cede à Nega específica do passo 1 (por
isso a checagem roda dentro do MESMO escopo, antes de cair pro nível — senão uma Nega de nível
bloquearia até quem tem alçada plena pessoal, contrariando RF-29b: "continua sujeita às
restrições explícitas de negação"); (4) lista fechada — se o escopo definiu qualquer Permite
naquela família sem cobrir o alvo consultado, bloqueia por omissão ("fora da alçada"); (5)
ausência de regra do escopo na família (ou só Negas de outros alvos) = permitido, padrão aberto.
As 5 regras de `ResolvedorAlcadaTests.cs` já existentes continuam passando sem alteração —
nenhuma delas tinha "Permite pra outro alvo da mesma família", cenário que só o passo 4 cobre.

**`AlvoAlcada`** ganhou duas variantes: `PorEquipeDeEscrevente(Guid? EquipeId)` (nulo é "sem
equipe" como alvo válido — RF-29a, o simulador do protótipo trata assim, não como "sem
restrição") e `PorTodosOsAtos` (sem payload — alçada plena, RF-29b). `AvaliacaoCandidato` ganha
`DecisaoEquipe`; `MotorDistribuicao.Distribuir` ganha `Guid? equipeDoEscreventeId = null`
(default compatível com todo call site/teste antigo).

**Persistência**: `RegraAlcadaRegistro` precisou de um discriminador explícito pro alvo
(`AlvoTipoRegistro { Etapa, TipoAto, Equipe, TodosOsAtos }`, mesmo padrão de
`TipoSugestaoRegistro`) — o padrão antigo de par nulo/preenchido não escala pra uma variante sem
payload (`PorTodosOsAtos`) nem pra uma variante com payload legitimamente nulo
(`PorEquipeDeEscrevente(null)`). Migration
`AdicionaEquipeETodosOsAtosEmRegrasAlcadaEGrupoEmTiposAto` precisou de **backfill manual** do
`alvo_tipo` pras linhas já existentes (`UPDATE ... WHERE alvo_etapa IS NOT NULL` /
`WHERE alvo_tipo_ato_id IS NOT NULL`) antes de recriar o `CHECK` — sem isso a migration quebraria
em qualquer banco com regra já cadastrada (aconteceria em produção). Confirmado aplicando contra
uma cópia real dos dados de produção clonada pro Postgres local (`pg_dump`/`psql` via um
container `postgres:18` avulso, já que o cliente local é 17.x e o Neon roda 18.x — mismatch de
major version trava o `pg_dump` direto).

**`TipoAto`** ganha `Grupo` (`GrupoTipoAto?`: Transmissões/Sucessões/Família/Garantias/
Notariais — os 5 valores vistos ao vivo na Matriz da aba Alçada do protótipo). Sem tela de
gestão de grupo no protótipo (só leitura agrupada) — `DefinirGrupoDoTipoAto` +
`PUT /tipos-ato/{id}/grupo` é inventado, mesmo molde de `DefinirPesoDeComplexidadeDoTipoAto`.

**RF-33 (contador de "usos")**: não é campo novo no banco — `RegraAlcadaResponse.Usos` conta
`Protocolo.RegraAplicadaId == regra.Id` via `IProtocoloRepository.ContarComRegraAplicadaAsync`,
mesmo padrão de leitura agregada de `CargaAtual`/`Semaforo`.

**Carga acumulada dentro da própria rodada** (premissa da seção 11, confirmada como requisito
formal nesta revisão): `ImportarLote`/`RedistribuirPool` processam vários protocolos numa
chamada só, mas o desempate por carga do motor (`OrderBy(CargaAtual)`) só enxergava o valor já
gravado no banco — dois protocolos urgentes concorrendo pelo mesmo candidato no mesmo lote
sempre caíam na mesma pessoa. `Conferente.CargaAtual` ganhou `private set` +
`IncrementarCargaAtual()` (só em memória, nunca persistido — mesmo princípio já usado pro
próprio `CargaAtual`), chamado por `AplicadorDeDistribuicao.Executar` (cobre `ImportarLote` e
`DistribuirProtocolo`, que compartilham esse método) e por `RedistribuirPool.ExecutarAsync`
(não passa por `AplicadorDeDistribuicao`, chama o motor direto).

**Bug de composition root achado testando de verdade, não pelos testes automatizados**:
esqueci de registrar `DefinirGrupoDoTipoAto` em `ServiceCollectionExtensions.cs` — a API subia
sem erro nenhum durante `dotnet build`/`dotnet test`, mas explodia no `dotnet run` com
`InvalidOperationException: Failure to infer one or more parameters` (minimal API não consegue
decidir se um parâmetro de handler é rota/corpo/serviço quando o tipo do serviço não está no
container de DI, e reporta isso só na hora de montar o endpoint, não em tempo de compilação).
Lição: **sempre subir a API de verdade (`dotnet run`) depois de adicionar um caso de uso novo**,
não confiar só em build/test verde — nenhum teste unitário instancia o composition root inteiro.

**Escopo desta rodada**: só back-end + extensão mínima do construtor de regra do front
(`AbaAlcada.tsx`) pra dar pra criar regra de equipe/alçada plena pela UI que já existe. A
reformulação visual grande da aba Alçada do protótipo v2 (3 sub-abas novas — Camadas/Matriz/
Testar) fica de fora, é projeto à parte.

Testado ponta a ponta contra uma cópia real dos dados de produção (clonados pro Postgres
local): `GET /conferentes/alcance` confirmando a lista fechada por nível (Júnior restrito a 1
tipo, Sênior aos 3 que tem regra) e a dimensão equipe (todo mundo aberto, já que nenhuma regra
de equipe existia nos dados reais ainda); `POST /regras-alcada` criando regra de equipe, de
equipe=sem-equipe e de alçada plena, todas persistindo e voltando corretas no `GET`. 236 testes
automatizados no total (60 Domain + 176 Application).

## Índice de confiança real da sugestão (RF-39-41) — fecha a simplificação consciente do módulo de Aprendizado

Nem o documento de requisitos (seção 7) nem o protótipo aprovado definem uma fórmula de
confiança — o protótipo mostra um número mockado, hardcoded por item de exemplo, sem relação
calculável com os outros campos do mesmo objeto (diferente do score do Dashboard/RF-46, aqui
nem o protótipo dá uma pista). Fórmula decidida e documentada em código
(`CandidatoSugestao.cs`): cada uma das 4 funções de `GeradorDeSugestoes` já calculava, por
dentro, uma proporção só pra comparar com o próprio limiar — nunca sobrevivia até o candidato
final. Essa proporção **é** o sinal de confiança natural (quanto mais concentrado o padrão,
mais forte a sugestão), e as 4 já nascem na mesma escala [0,1], comparável entre si sem inventar
peso novo:

- `TipoDesconhecido` — força da moda do nível (contagem do nível majoritário / total resolvido
  na mão). `Moda`/`ModaGuid` viraram `ModaComForca`/`ModaGuidComForca`, devolvendo o valor E a
  proporção do grupo majoritário numa tacada só.
- `PrazoIrreal` — `percentualEstouro` (já calculado pra comparar com o limiar de 60%).
- `EscreventeOrfao` — dominância da equipe sugerida (contagem da equipe majoritária / total de
  ocorrências nos mesmos lotes).
- `RiscoQualidade` — `percentualReprovacao` (já calculado pra comparar com o limiar de 50%).

`Sugestao.IndiceConfianca` é recalculado na mesma cadência de `Ocorrencias`/`Evidencia` —
`AtualizarEvidencia` (Domain) e `AtualizarEvidenciaAsync` (`ISugestaoRepository`) ganharam o
parâmetro junto, mesmo ponto onde os outros dois já eram atualizados a cada rodada do gerador.
`SugestaoRegistro`/`SugestaoRegistroConfiguration` seguem o mesmo padrão de sempre (coluna
simples, `builder.Property` explícito). Migration `AdicionaIndiceConfiancaEmSugestoes` —
`indice_confianca double precision NOT NULL DEFAULT 0` (sugestões antigas ficam com 0 até a
próxima rodada do gerador regenerar a mesma chave — não há histórico real que dependa de um
valor retroativo). `SugestaoResponse` ganhou `IndiceConfianca` (0.0–1.0; o front multiplica por
100 e mostra como "N% de confiança" + barra, igual ao protótipo).

225 testes automatizados no total (54 Domain + 171 Application) — a cobertura do índice de
confiança entrou como asserções novas em testes já existentes (`GeradorDeSugestoesTests`,
`GerarSugestoesTests`), não testes novos: a fórmula é derivada do mesmo cenário que cada teste
já montava pra cobrir o limiar, então também já dava pra conferir o índice sem nenhum arranjo
adicional.

## Distribuição/Minha fila v2 (prioridade manual, RF-14/16/18c/18e/24f) — quinta frente do "v2"

Fase escolhida pelo dono entre as opções apresentadas depois do motor de alçada v2. Só o item
de prioridade manual mexeu em domínio de verdade — o resto (RF-14, RF-16, RF-18c) é leitura já
suportada pelos DTOs existentes, e RF-18e/RF-24f (barra de filtros) é 100% client-side, sem
endpoint novo (ver `../dispatch-web/CLAUDE.md`, mesma seção, pra essa parte).

**`Protocolo.Prioridade` não tinha nenhum caminho de produção real que a definisse como
`Alta`** — só o endpoint avulso `/protocolos/distribuir` aceitava isso no request, nunca
`ImportarLote` (o fluxo real). Confirmado com o dono: em vez de esperar por uma fonte real
(relatório do cartório não carrega essa informação), virou ação manual —
`Protocolo.DefinirPrioridade(prioridade)` (setter público virou `private set` + método) e o
caso de uso `DefinirPrioridadeDoProtocolo` (`ExecutarAsync(Guid, Prioridade) => Task<bool>`,
mesmo molde de `DefinirGrupoDoTipoAto`). `POST /protocolos/{id}/definir-prioridade`
(`RequireRole(Distribuidora)`), corpo `{ prioridade }`, 204/404. `ProtocoloResumo` (duplicado
em `DistribuicaoEndpoints.cs`/`MinhaFilaEndpoints.cs`, mesmo padrão de sempre) ganhou
`Prioridade` nos dois.

**Bug real encontrado na verificação com Playwright, não pelos testes automatizados** (a
lição do "DI composition root" da seção do motor de alçada v2 foi aplicada — `dotnet run`
depois de registrar `DefinirPrioridadeDoProtocolo`, sem repetir aquele erro; este foi um bug
diferente, de autorização): `GET /equipes`, `GET /escreventes` e `GET /tipos-ato` eram
`RequireRole(Distribuidora)` — corretos para as telas de gestão que só a Distribuidora usa
(Central de Regras), mas a nova barra de filtros de **Minha fila** (RF-24f, papel Conferente)
também precisa dos três pra cruzar `escreventeId → equipeId → nome` e `tipoAtoId → nome` no
próprio filtro. Logado como Conferente de teste, as três chamadas voltavam 403 — o front não
quebrava visualmente (o filtro "funcionava", marcava "1 filtro ativo"), mas como
`escreventePorId`/`equipePorId` ficavam sempre vazios (a chamada falhou), **todo protocolo
caía no valor default de "sem equipe"** — filtrar por "sem equipe" não reduzia nada (mostrava
tudo) e filtrar por uma equipe de verdade zerava a lista inteira. Só apareceu escrevendo uma
asserção de comportamento real no Playwright (contagem do pool antes/depois do filtro,
comparadas) — a suíte de TypeScript/build não pega isso, e a suíte de screenshot sozinha
também não, porque o layout renderiza "certo" (só o resultado do filtro está errado).

**Correção**: `GET /equipes` e `GET /escreventes` (as leituras de listagem geral) e `GET
/tipos-ato` passaram a aceitar `Distribuidora` OU `Conferente`; as mutações (`POST`/`PUT` de
equipe, `POST /escreventes/{id}/mover`, `GET /escreventes/sem-equipe`, tudo em
`TipoAtoEndpoints.cs` além do `GET` raiz) continuam exclusivas de Distribuidora — RF-24f não é
ação de gestão, mas ainda não dá acesso de escrita a nada de Central de Regras pro Conferente.

**Detalhe de minimal API que não é óbvio**: repetir `.RequireAuthorization(...)` numa rota
individual **não substitui** a policy já aplicada no `MapGroup(...)` — as duas se combinam com
E (cada `RequireAuthorization`/`[Authorize]` aplicado a um endpoint é mais um requisito que
**todos** precisam satisfazer, não uma lista de alternativas que substitui a anterior). Por
isso `equipesGrupo`/`escreventesGrupo` (`EquipeEndpoints.cs`) deixaram de ter uma policy no
`MapGroup` em si — cada rota individual declara a sua (`GET /` com os dois papéis, mutações só
com Distribuidora). `GET /tipos-ato` já não estava dentro de um grupo com policy própria (é
`app.MapGet` solto, só o `grupo` de `/tipos-ato/{id}/...` tem policy de grupo), então bastou
alargar o `RequireAuthorization` da própria rota.

Testado ponta a ponta contra o Postgres local: `GET /equipes`/`/escreventes`/`/tipos-ato` como
Conferente voltando 200 depois da correção (403 antes), `POST /equipes` e `GET
/escreventes/sem-equipe` continuando 403 pro mesmo token (confirma que a correção não abriu
mutação nenhuma). 239 testes automatizados no total (60 Domain + 179 Application).

## Motor de alçada v3 — cascata de camadas, reserva, grupo como alvo

Investigando o redesign visual da aba Alçada (as 3 sub-abas novas do protótipo v2 — Camadas/
Matriz/Testar, a frente escolhida pelo dono depois do v2), achei que a ferramenta interativa
(`Dispatch.dc.html`) evoluiu pra um algoritmo de alçada **diferente** do "Motor v2" já em
produção — confirmado lendo a lógica-fonte do protótipo (`bloqueioPuro`/`decideCamada`/
`camadaDe`/`trilhaPura`) e ao vivo nas 3 sub-abas via Playwright, não só por leitura de markup.
O documento de requisitos formal (`Dispatch - Requisitos.dc.html`, seção 4) **ainda descreve o
modelo v2** — a ferramenta interativa avançou sem isso virar texto formal. Decisão do dono:
motor v3 primeiro (correto e testado), a tela depois — construir a tela sobre o back antigo
mostraria dado que ele não sabe calcular do jeito prometido. **Decisão consciente: não editei
`Dispatch - Requisitos.dc.html`** — é gerado por uma ferramenta de design externa do dono, igual
já foi decidido pro Motor v2; a documentação da divergência e do algoritmo novo vive aqui.

**O algoritmo mudou em três pontos**, em relação ao "Model A" v2 (escopo binário pessoa-ou-
nível por família):

1. **Cascata de 3 camadas**, avaliadas nesta ordem contra o **caso inteiro** (etapa + tipo +
   equipe do escrevente juntos, não uma dimensão isolada) — a de baixo sobrescreve a de cima
   quando tem opinião, mesmo em dimensões que ela nem tocou:
   - `Base por nível` — toda regra cujo sujeito é Nível, qualquer alvo.
   - `Ajuste por equipe` — regra de PESSOA cujo alvo é a equipe do escrevente.
   - `Exceção por pessoa` — regra de PESSOA cujo alvo não é equipe (tipo/grupo/etapa/todos).
   Dentro de uma camada: negação que bate no caso vence primeiro; senão, entre as permissões da
   camada, alçada plena satisfaz sozinha; senão, cada dimensão (equipe/etapa/grupo/tipo, nesta
   ordem) que tiver alguma permissão na camada vira lista fechada (mesma regra do v2, agora
   rodada por camada, cobrindo as 4 dimensões possíveis, não só a família do alvo perguntado).
   Camada sem regra aplicável não opina e não interfere na cascata.
2. **Terceiro tipo de permissão: `PermissaoRegra.Reserva`** — checado **antes** de qualquer
   camada: reserva ativa batendo no caso bloqueia todo mundo que não é o sujeito dela, direto.
   Não concede acesso sozinha ao próprio sujeito — ele ainda precisa de outra regra (ou do
   padrão aberto) pra ter Permitido de verdade.
3. **`AlvoAlcada.PorGrupoTipoAto(GrupoTipoAto)`** — mira todos os tipos de um grupo de uma vez
   (mesmo enum `TipoAto.Grupo` do Motor v2), sem listar tipo por tipo.

**`ResolvedorAlcada` — API mudou de "resolve um alvo" pra "resolve um caso".** Novo tipo
`CasoAlcada(Etapa, TipoAto, Guid? EquipeId)` (leva o `TipoAto` inteiro, não só o Id — precisa
do `.Grupo`). `Resolver(Conferente, CasoAlcada, regras) → DecisaoAlcada` roda a cascata e
devolve só o veredito final (usado no caminho quente da distribuição). `Explicar(Conferente,
CasoAlcada, regras) → IReadOnlyList<PassoTrilha>` reaproveita a mesma lógica interna mas devolve
o passo-a-passo por camada — só chamado pelas leituras explicativas (painel de detalhe,
simulador "Testar"), não pelo `MotorDistribuicao`/`ImportarLote`, pra não alocar lista à toa no
caminho mais quente do sistema. `DecisaoAlcada` ganhou `Motivo` (`MotivoAlcada?` — enum leve:
`Etapa`/`Tipo`/`Grupo`/`Equipe`/`Geral`/`Reservado`, **sem nome próprio embutido**: o texto final
("Testamento fora da alçada", "reservado a Márcio Gomes") é responsabilidade do front, que já
tem os lookups de nome prontos — mesma disciplina de "back manda o fato cru" já seguida em todo
o resto do projeto, mesmo o protótipo interpolando nomes direto no motivo).

**`AvaliacaoCandidato` simplificou**: de `(Conferente, DecisaoEtapa, DecisaoTipo, DecisaoEquipe)`
— 3 decisões pra AND'ar — pra `(Conferente, DecisaoAlcada Decisao)`, já que agora é uma decisão
só por candidato. `MotorDistribuicao` monta um `CasoAlcada` só por protocolo e chama `Resolver`
uma vez por candidato (não mais três). `AplicadorDeDistribuicao.RegraAplicadaDe` colapsou pra
`avaliacao.Decisao.RegraAplicada?.Id` — a cascata já decidiu qual regra venceu, não precisa mais
de prioridade manual entre 3 campos.

**Simplificação consciente em `ObterAlcancePorConferente` (RF-34)**: com o caso resolvido de
forma holística, "quantos tipos alcança" deixou de ser um fato puro por tipo — uma regra de
equipe pode depender da combinação inteira. Adotei a mesma aproximação já usada pelo RF-30/
cobertura e pelo próprio simulador do protótipo: fixa um **caso representativo** por eixo
(`Etapa = PosConferencia` + sem equipe pra "tipos permitidos"; um tipo representativo — o
primeiro que a pessoa já alcança nesse caso-base, senão o primeiro do catálogo — pra "etapas" e
"equipes permitidas"). Documentado no código como aproximação, não fato exato.

**Novo caso de uso `SimularAlcada`** — base do "Testar" do protótipo: mesmo padrão de
`ObterDetalheProtocolo`, mas sobre um caso hipotético (etapa/tipo/equipe escolhidos na hora),
não um `Protocolo` real. `POST /regras-alcada/testar`, corpo `{etapa, tipoAtoId, equipeId?}`,
404 se o tipo não existir. `ObterDetalheProtocolo` também passou a usar `Explicar` (não só
`Resolver`) e ganhou `ITipoAtoRepository` — o painel de detalhe agora mostra a trilha completa,
não só elegível/não elegível.

**Persistência**: `RegraAlcadaRegistro` ganhou `AlvoGrupoTipoAto` (`GrupoTipoAto?`) e
`AlvoTipoRegistro` ganhou `Grupo` — mesmo padrão de discriminador já usado pro Motor v2, sem
precisar de backfill desta vez (nenhuma linha existente usa o discriminador novo, diferente da
migration anterior que teve que popular linha existente). `PermissaoRegra.Reserva` não pediu
nenhuma mudança de schema — a coluna já é `HasConversion<string>()` solto, sem `CHECK` travando
o conjunto de valores. Migration `AdicionaGrupoEmRegrasAlcada`: 1 coluna nova nullable + 1
branch a mais no `CHECK` de alvo, nada mais.

**Endpoints**: `CriarRegraAlcadaRequest`/`RegraAlcadaResponse` ganham `AlvoGrupo`
(`GrupoTipoAto?`), XOR de criação passa a contar 5 campos. `PermissaoRegra.Reserva` já flui sem
mudança nenhuma no endpoint de criar (é só mais um valor do mesmo enum que o request já aceita).

**Blast radius que precisou de `ITipoAtoRepository` novo** (não tinham antes): `PegarProtocolo`,
`ObterMinhaFila` (`MinhaFila.cs`), `AtribuirAoMenosCarregado`, `ObterDetalheProtocolo` — todos
precisam do `TipoAto` inteiro agora (não só o Id) pra montar o `CasoAlcada`. Cada um trata tipo
desconhecido/removido do catálogo como "sem alçada" antes mesmo de chamar o resolvedor, mesma
semântica de exceção que `MotorDistribuicao` já tinha.

Testado ponta a ponta contra o Postgres local depois de aplicar a migration: `POST
/regras-alcada/testar` devolvendo a trilha por camada certa (uma regra de nível permitindo
sobrescrita por uma exceção pessoal negando por etapa, motivo `"Etapa"`); `POST /regras-alcada`
criando regra de grupo (`alvoGrupo: "Notariais"`) e de reserva (`permissao: "Reserva"`),
persistindo e voltando corretas no `GET`; XOR rejeitando dois alvos ao mesmo tempo com 400.
244 testes automatizados no total (64 Domain + 180 Application).

**Escopo desta rodada**: só o motor (Domain/Application/Infrastructure/Api). A reformulação
visual da aba Alçada (Camadas/Matriz/Testar no front) é a próxima frente, consumindo este back.

## Pool ordenado por vencimento (Distribuição e Minha fila)

Pedido do dono: quem vence primeiro no pool aparece primeiro na tela, tanto na coluna "Pool
aberto" de Distribuição quanto em "Pool disponível" de Minha fila. Nenhuma das duas queries
(`ObterPoolAsync`/`ObterParaDistribuicaoAsync`) tinha `ORDER BY` — Postgres não garante ordem
estável sem isso, mesma armadilha já documentada em `ListarConferentes` (seção Conferentes,
acima). `ObterVisaoDistribuicao.ExecutarAsync` ordena o bucket `pool` por
`VencimentoEm ?? DateTimeOffset.MaxValue` (nulo — sem prazo definido — vai pro fim, não pro
início); `ObterMinhaFila.ExecutarAsync` ordena `poolDisponivel` do mesmo jeito. Escopo
deliberadamente restrito ao pool (não estendido a atribuídos/em conferência/concluídos/exceções
— o pedido foi específico). 2 testes novos (ordem ascendente, nulo por último) — 246 testes
automatizados no total (64 Domain + 182 Application).

## Cumprimento de prazo por equipe — fecha metade do gap do RF-43

O dono escolheu RF-43 (gaps do Dashboard) entre as opções apresentadas depois do redesign de
Filtros do front. Investigação prévia (`dispatch-web/CLAUDE.md` já documentava) confirmou que
"desempenho por tipo de ato" já existia de ponta a ponta — só faltava mesmo "cumprimento de
prazo por equipe e etapa", a simplificação registrada na seção do Dashboard acima.

`ObterDashboard` ganhou `IEscreventeRepository`/`IEquipeRepository` (portas já existentes,
usadas em outros casos de uso — nenhum repositório novo). Agrupa `concluidosNoPeriodo` por
`(EquipeId do escrevente, Etapa)` — **não** só por equipe: o prazo combinado é
`Equipe.PrazoPara(Etapa)`, então a mesma equipe pode ter cumprimento bem diferente entre pré e
pós-conferência, misturar as duas escondería isso. Escrevente sem equipe entra como grupo
próprio (`EquipeId: null`, `EquipeNome: "sem equipe"`) — ele tem prazo real (D+1 padrão, ver
`ResolvedorDePrazo`), só não tem equipe pra nomear; ficar de fora seria esconder informação real
de cumprimento. `CalcularCumprimentoPrazoPorEquipe` reaproveita o `EstaNoPrazo` já usado por
`CalcularKpis`/`CalcularDesempenho` — mesma regra de "no prazo", sem nova definição. Ordenado
por pior percentual primeiro (`OrderBy(PercentualNoPrazo)`), igual ao protótipo aprovado
(`slaEquipes.sort((a,b) => a.noPrazo - b.noPrazo)`, confirmado direto no `Dispatch.dc.html`).

Novo record `CumprimentoPrazoEquipe(EquipeId, EquipeNome, Etapa, Prazo, Total,
PercentualNoPrazo)` — `Prazo` é `TipoPrazo?` (o `Prazo.Tipo` do primeiro protocolo do grupo; só
pro texto informativo "pós-conferência · 1 hora" do card, não entra no cálculo do percentual).
Vem vazio na visão restrita do conferente (`conferenteRestritoId != null`), mesmo padrão de
`PorTipoAto` — RF-45 não pede isso pro conferente. `DashboardResponse` ganhou o campo espelhado
(`CumprimentoPrazoEquipeResponse`).

Testado contra o Postgres local depois de subir a API de verdade (`dotnet run`, não só
`dotnet build`/`dotnet test` — evita repetir a lição do "composition root" documentada no Motor
de alçada v2, mesmo não sendo um caso de uso novo desta vez, só um construtor com dependência
nova): `GET /dashboard?periodo=Trimestre` respondendo com `cumprimentoPrazoEquipe: []` (banco
local sem protocolo concluído nesse período — resposta vazia e coerente, não quebrou nada).
1 teste novo (`CumprimentoPrazoEquipe_AgrupaPorEquipeEEtapa_PiorPercentualPrimeiro` — 2 equipes
+ 1 grupo "sem equipe", 3 combinações de etapa/percentual, confirma agrupamento e ordenação) —
247 testes automatizados no total (64 Domain + 183 Application).

## Protocolo manual — criar, editar, excluir com desfazer (RF-18f a RF-18j)

O dono notou o botão "Novo protocolo" do protótipo, que nunca existiu no app. Investigação
prévia (documentada no plano de implementação) achou que a maior parte de "criar" já existia —
`POST /protocolos/distribuir` já resolve prazo, roda o motor e persiste, exatamente o que
RF-18f pede. Faltava mesmo: bloquear número duplicado, um modo de **simular sem persistir**
pra prévia do modal, editar (campos imutáveis até aqui), e excluir com desfazer.

**`TipoAtoId`/`EscreventeId`/`Etapa` de `Protocolo` viraram `private set`** — só isso já exigia
migration nenhuma (são as mesmas colunas), só destravava `EditarDadosBasicos(tipoAtoId,
escreventeId, etapa)` novo.

**Exclusão é soft-delete permanente, não hard-delete + snapshot pro desfazer.** Mesma filosofia
de `RemoverConferente` (preserva histórico, nunca apaga linha). `StatusProtocolo` ganhou
`Excluido`; `Protocolo.Excluir()` guarda o status atual em `StatusAntesDeExcluir` (campo novo,
migration `AdicionaProtocoloManualEExclusao` — 1 coluna nullable, sem backfill) e
`Restaurar()` só devolve esse valor. Como nada além de `Status` é tocado, "restaura com o mesmo
vencimento, dono e histórico" (RF-18j) fica **trivialmente verdadeiro** — não tem lógica de
reconstrução nenhuma pra escrever ou testar. "Desfazer por alguns segundos" é só um timer no
front (toast) — o back permite restaurar a qualquer momento, sem job de limpeza (o projeto não
tem scheduler, e não precisa: registro soft-deletado só não aparece em tela nenhuma, igual
conferente removido).

**Duas queries existentes precisaram de ajuste antes de introduzir `Excluido`** (achado
audit­ando todo filtro de `Status` do repositório antes de mexer, não depois):
`ObterParaDistribuicaoAsync` não tinha filtro de status nenhum (alimentava RF-13 sem
exclusão — um protocolo excluído vazaria pra Distribuição); `ObterAbertosPorEscreventesAsync`
tinha exclusão por lista (`!= Aprovado && != Reprovado && != Descartado`) que também precisou
ganhar `!= Excluido`, senão RF-38 recalcularia vencimento de protocolo excluído. Os dois
cobertos por teste (`ProtocoloExcluido_NaoAparecePraDistribuicao`/
`ProtocoloExcluido_NaoEntraNoRecalculoDeVencimentosAbertos`).

**`ResolvedorDeEscreventePorNome`** (novo, `CasosDeUso/`) — extraído da lógica que já vivia
inline no handler de `POST /protocolos/distribuir` (busca por nome case-insensitive, cria sem
equipe se for a primeira vez), reaproveitado pelos 3 casos de uso novos que também precisam
disso. **Público, não `internal`** como os outros helpers da pasta (`AplicadorDeDistribuicao`,
`RecalculoDeVencimentos`, `VerificadorDeAlcada`) — o endpoint avulso existente chama direto,
sem passar por um caso de uso, então precisa ser visível fora de `Dispatch.Application`.
Pequena mudança de comportamento no endpoint avulso, de propósito: escrevente novo agora
passa por `NormalizadorDeTexto.ParaNomeProprio` (igual `ImportarLote` já fazia) — antes gravava
o nome cru do request, sem normalizar.

- **`SimularProtocoloManual`** (RF-18f, prévia) — monta um `Protocolo`/`Escrevente` só em
  memória (`adicionarSeNovo: false` no resolvedor — não persiste nada, nem o escrevente se for
  novo) e roda `AplicadorDeDistribuicao.Executar` normalmente (ele nunca persistiu sozinho — 
  quem persiste sempre foi o chamador). Devolve equipe/prazo/grupo/vencimento/destino previsto
  e se o número já está em uso. `POST /protocolos/manual/simular`.
- **`CriarProtocoloManual`** (RF-18f, de verdade) — bloqueia número duplicado
  (`ExisteComNumeroAsync`, novo em `IProtocoloRepository` — número não é único no banco por
  índice, de propósito, pra reprocessamento de relatório; aqui é regra de aplicação só pro
  cadastro manual, uma ação humana pontual). Livre, resolve escrevente de verdade e reaproveita
  `DistribuirProtocolo` (injetado) pra persistir — zero regra nova. `POST /protocolos/manual`,
  201 ou 409.
- **`EditarProtocoloManual`** (RF-18g/h) — `identidadeMudou` compara tipo/escrevente/etapa
  antes/depois; só quando muda de fato chama `ResolvedorDePrazo.Resolver` +
  `protocolo.DefinirPrazo(prazo, protocolo.AndamentoEm)` — referência é sempre o `AndamentoEm`
  original, nunca "agora" (mesma regra de `RecalculoDeVencimentos`/RF-38). Prioridade e
  observação sempre são aplicadas, nunca disparam recálculo. RF-18h: se identidade mudou e o
  protocolo tem dono, `VerificadorDeAlcada.TemAlcada` decide se ele volta pro pool sozinho —
  sem tipo conhecido, pula essa checagem (não reabre a discussão de "tipo desconhecido" que
  esse fluxo de edição não trata). `PUT /protocolos/{id}`.
- **`ExcluirProtocolo`**/**`RestaurarProtocolo`** — bem finos, só a transição (`protocolo.Excluir()`/
  `Restaurar()`) mais a validação de existência (e, pra restaurar, de que realmente estava
  excluído). `DELETE /protocolos/{id}`, `POST /protocolos/{id}/restaurar`.

Testado ponta a ponta contra o Postgres local depois da migration: `POST
/protocolos/manual/simular` não persiste nada (confirmado por não aparecer em nenhuma lista
depois); `POST /protocolos/manual` cria (201), repetir o mesmo número dá 409; `PUT
/protocolos/{id}` trocando etapa recalcula o vencimento a partir do `andamento_em` original
(não do instante da edição — confirmado comparando os dois valores); `DELETE` some da
Distribuição, `POST .../restaurar` traz de volta, restaurar de novo (já não excluído) dá 404.

21 testes novos (3 domínio — `Excluir`/`Restaurar`/`EditarDadosBasicos` — e 18 de aplicação
entre os 5 casos de uso novos) — 268 testes automatizados no total (67 Domain + 201
Application).

**Escopo desta rodada**: só o back (Domain/Application/Infrastructure/Api). O front (modal
"Novo protocolo"/"Editar protocolo", wiring no painel de detalhe, toast de desfazer) é a
próxima frente, consumindo este back.

**Segunda passada, depois de reconferir o markup do protótipo de propósito** (o dono perguntou
"está fiel ao protótipo 100%?" — releitura de `novoAberto`/linhas ~1739-1808 achou um gap real:
`CriarProtocoloManual` não aceitava `observacao`, mesmo o protótipo tendo esse campo já na
criação, não só na edição). `ExecutarAsync` ganhou o parâmetro `observacao` (aplicado via
`Protocolo.DefinirObservacao`, método que já existia); endpoint/request DTO acompanharam. 1
teste novo (`ComObservacao_GravaJuntoNaCriacao`) — 269 testes automatizados no total (67 Domain
+ 202 Application).

## TOTP e recuperação de senha, caminho feliz (RF-01a a RF-01l)

Login sempre foi só e-mail+senha (decisão de escopo mínimo documentada desde o início deste
arquivo). O dono pediu pra construir o resto: registro de autenticador TOTP (RFC 6238) e
recuperação de senha em 3 etapas sem e-mail/SMS — mas **explicitamente sem** RF-01m/RF-01n (os
casos de exceção "sem autenticador"/liberação da distribuidora e códigos de emergência do
admin), porque dependem deste caminho feliz existir primeiro e nem têm tela no protótipo.

**Achado relendo o protótipo com atenção**: TOTP não é 2FA obrigatório no login normal — o
"Entrar" de sempre autentica direto, sem pedir código nenhum. Registrar o autenticador é um
fluxo separado, autoiniciado pelo próprio usuário ("Registrar autenticador", visível já na tela
de login), cuja única função real é servir de prova de identidade na recuperação de senha.
Implementado exatamente assim — mais simples que 2FA em todo login, e é o que o protótipo
aprovado de fato mostra.

**TOTP de verdade, não mock** — diferente do protótipo (que aceita qualquer código de 6
dígitos exceto "000000", QR decorativo): `TotpComOtpNet` usa a lib `Otp.NET`, RFC 6238 real,
janela de tolerância de 1 bloco (±30s) e barra reuso de código (contador do bloco usado tem que
ser maior que o último aceito). Testado ponta a ponta com um TOTP calculado na mão em Python a
partir do segredo Base32 devolvido por `POST /auth/totp/registrar` — não só com mock.

**`UsuarioTotp`** (`Dispatch.Domain/Usuarios/`) — 1:1 com `Usuario` (chave é o próprio
`UsuarioId`, sem FK própria — primeira entidade do projeto nesse formato). Guarda o segredo
**cifrado** (RNF-15: `CifradorAes`, AES-CBC com IV aleatório por chamada, chave de
`Totp:ChaveDeCifragem` — fora do banco, mesmo padrão de `Jwt:ChaveDeAssinatura`), tentativas e
bloqueio (RF-01i: 5 erradas bloqueia 15min), e o hash do token de recuperação (nunca o token em
claro — mesmo `IHashDeSenha`/`PasswordHasher` da senha).

**Token de recuperação não é JWT de sessão** — é um token opaco (`{usuarioId:N}.{aleatório}`)
que a etapa 2 (`ValidarCodigoRecuperacao`) emite depois de validar o código, e a etapa 3
(`RedefinirSenha`) exige pra trocar a senha. Por que não indexar/consultar por hash: o
`PasswordHasher` salga a cada chamada, então o mesmo texto nunca produz o mesmo hash duas vezes
— não dá pra fazer `WHERE hash = @candidato`. Por isso o `usuarioId` vai embutido no próprio
token (não é segredo — nesse ponto do fluxo o front já sabe o e-mail), e quem garante posse
legítima é o `hashDeSenha.Verificar` contra a parte aleatória.

**RF-01h (anti-enumeração)**: `IniciarRecuperacaoSenha` sempre devolve 200 genérico, e-mail
existindo ou não — só grava evento de auditoria se o usuário existir de verdade (não vaza nada
pela resposta). `ValidarCodigoRecuperacao` devolve o mesmíssimo `CodigoInvalido` pra e-mail
inexistente, TOTP não confirmado e código errado — nunca dá pra saber, pela resposta, qual dos
três foi (mesmo espírito de `ResultadoAutenticacao.Rejeitado`).

**RF-01k (encerrar sessões) exigiu customizar a validação do JWT pela primeira vez no
projeto** — `Usuario` ganhou `SessoesValidasApartirDe` (`DateTimeOffset`, `MinValue` por
padrão — sem quebrar o construtor existente, e semanticamente correto: quem nunca trocou a
senha não tem carimbo nenhum que importe). `RedefinirSenha` (Domain) bumpa esse carimbo junto
com o hash, truncado pro segundo (ver bug abaixo). `Program.cs` ganhou
`JwtBearerOptions.Events.OnTokenValidated`: busca o `Usuario` pelo claim de id e rejeita o
token se `IssuedAt < SessoesValidasApartirDe` — 1 consulta a mais por request autenticado,
aceitável pro volume deste sistema (cartório interno, não API pública de alto tráfego). RF-01k
também devolve pro pool os atos que estavam **em conferência** (não "atribuídos") com o
usuário: resolve `Conferente` a partir do `Usuario` via `ObterPorUsuarioIdAsync` (mesma
resolução que Minha fila já faz a partir do JWT), depois
`ObterEmConferenciaPorConferenteAsync` + `EnviarParaPool()` — nada novo no Domain.

**Dois bugs reais, achados só rodando de verdade (`dotnet run` + curl), não por
`dotnet build`/`dotnet test`**:

1. **ASP.NET Core 10 valida o JWT via `Microsoft.IdentityModel.JsonWebTokens.JsonWebToken`, não
   mais `System.IdentityModel.Tokens.Jwt.JwtSecurityToken`** — o handler novo. Um cast direto
   pro tipo antigo dentro de `OnTokenValidated` compila liso e só explode em runtime, na
   primeira chamada autenticada (`InvalidCastException`). Corrigido castando pro tipo certo.
2. **`EmissorDeTokenJwt` nunca setava a claim `iat`** — a sobrecarga de `JwtSecurityToken`
   usada não preenche isso sozinha; o token saía sem `IssuedAt` de verdade (confirmado
   decodificando o payload na mão). Enquanto `SessoesValidasApartirDe` de todo mundo era
   `MinValue` (ninguém tinha trocado senha ainda) isso nunca dava problema — só apareceu
   quando testei a troca de senha de ponta a ponta e o `auth.spec.ts` (e mais 7 specs) do
   `dispatch-web` começaram a falhar tentando logar: todo login passou a devolver um token que
   `GET /auth/me` rejeitava na hora (401), porque `IssuedAt` bugado ficava sempre "menor" que
   o carimbo real gravado na troca de senha. Corrigido adicionando a claim `iat` explícita
   (`JwtRegisteredClaimNames.Iat`, Unix seconds) na emissão do token. Truncar
   `SessoesValidasApartirDe` pro segundo (não guardar milissegundos) evita a mesma classe de
   problema no caso extremo de login e troca de senha no mesmíssimo segundo. **Lição**: uma
   mudança na validação do JWT (`Program.cs`) é praticamente invisível pra `dotnet
   build`/`dotnet test` — só um login de verdade seguido de uma chamada autenticada real prova
   que o pipeline inteiro funciona. Rodar a suíte e2e permanente do `dispatch-web` depois dessa
   mudança pegou o bug antes do dono ver.

**Regras de senha (RF-01j)** — `RegrasDeSenha` (Domain, estático): ≥12 caracteres e não
começar com `senha|123|cartorio|dispatch` (case-insensitive) — mesmas 3 regras do protótipo.
Validado no back (fonte da verdade); o front replica pra feedback ao vivo.

**Fora de escopo, por decisão explícita do dono**: RF-01m (liberação sem autenticador pela
distribuidora) e RF-01n (códigos de recuperação de emergência do admin) — ficam pendentes,
documentados aqui pra não esquecer, mas sem nenhuma tela no protótipo pra referenciar quando
chegar a vez.

Endpoints novos em `AuthEndpoints.cs`: `POST /auth/totp/registrar` (autenticado),
`POST /auth/totp/confirmar` (autenticado), `POST /auth/recuperar/iniciar` (anônimo, sempre
200), `POST /auth/recuperar/validar-codigo` (anônimo, 200/401/423), `POST
/auth/recuperar/redefinir-senha` (anônimo, 204/400/401).

Testado ponta a ponta contra o Postgres local com TOTP real (código calculado em Python a
partir do segredo Base32): registrar → confirmar com código certo (204) → validar-codigo na
recuperação → token de recuperação → redefinir com senha fraca (400) → redefinir com senha
forte (204) → reusar o mesmo token (401, uso único) → sessão antiga rejeitada depois da troca
(401, RF-01k) → login com senha antiga falha, com a nova funciona. Suíte e2e permanente do
`dispatch-web` voltou a passar 100% nos specs de autenticação depois do fix do `iat` (os 8
specs que continuam falhando são de áreas que esta rodada não tocou — tipos de ato, alçada,
importação — e não têm nenhuma mudança de código do lado deles; é deriva de dado acumulado no
Postgres local ao longo da sessão, não regressão).

38 testes novos (7 domínio — `UsuarioTests`, `UsuarioTotpTests`, `RegrasDeSenhaTests` — e 31 de
aplicação entre os 5 casos de uso novos) — 305 testes automatizados no total.

**Escopo desta rodada**: só o back. O front (telas de registro de TOTP e recuperação de senha,
QR de verdade renderizado no cliente, links na tela de login) é a próxima frente.

## Prioridade com 3 níveis (Baixa/Média/Alta)

O app só tinha `Normal`/`Alta` — simplificação de uma rodada anterior, documentada como gap.
O protótipo aprovado sempre teve 3 níveis (confirmado no markup: seletor de 3 botões, eixo de
filtro com 3 opções). Fechado agora: `Prioridade` (`src/Dispatch.Domain/Prioridade.cs`) ganhou
`Baixa`.

**Achado que evitou uma migration**: releitura da seção 4/RF-18d do documento de requisitos
confirma que 3 níveis é só detalhe de UI do protótipo, não um RF numerado — a única regra de
negócio formal continua binária ("urgente = prioridade **alta** OU prazo curto"), e como
`Prioridade` já é mapeada como string (`HasConversion<string>().HasMaxLength(20)`, sem CHECK
constraint), um valor novo no enum não pede `dotnet ef migrations add` nenhuma — é só mais uma
string possível dentro da mesma coluna. `Protocolo.Urgente` não mudou uma linha.

**"Normal" continua se chamando assim no C#/banco, não virou "Media"** — todo protocolo já
gravado tem `Prioridade = "Normal"`; renomear o enum quebraria a leitura desses registros
(`HasConversion<string>` faz `Enum.Parse`, e um "Normal" gravado não bateria com um membro
`Media` novo). O rótulo "Média" mostrado ao usuário é só do `dispatch-web`
(`PRIORIDADE_LABEL`) — mesmo padrão que "Alta" já usa (`'Alta (urgente)'` no seletor,
divergência de rótulo aceita antes).

1 teste novo (`ProtocoloTests.PrazoD1OuD2ComPrioridadeBaixa_NaoEhUrgente`, par do teste
equivalente já existente pra `Normal`) — 307 testes automatizados no total.

## Fix: simulador "Testar" da aba Alçada agora roda o motor de verdade

Achado numa auditoria de qualidade do front (pedida pelo dono): `SimularAlcada.cs` só devolvia
elegibilidade por conferente (`ResolvedorAlcada.Explicar`) — o destino (pool/atribuído/
exceção) mostrado no simulador "Testar" (`dispatch-web`, `AbaAlcadaTestar.tsx`) era inferido no
front só pela contagem de elegíveis, o que dava errado sempre que urgência importasse (a regra
real, `MotorDistribuicao.cs:34-46`, decide primeiro por `Protocolo.Urgente`, não por contagem —
ex.: 1 único elegível mas não urgente vai pro pool, não "direto pra ele"; vários elegíveis mas
urgente atribui a um só, o de menor carga, não "pool aberto").

**Fix**: `SimularAlcada.ExecutarAsync` ganhou o parâmetro `Prioridade prioridade`. Depois de
montar as avaliações de elegibilidade (inalterado), monta um `Protocolo`+`Escrevente`
transitórios (mesma técnica de `SimularProtocoloManual.cs` — nunca persistidos, `Escrevente`
só precisa do `EquipeId` recebido, sem nome de verdade) e chama
`AplicadorDeDistribuicao.Executar` (já existente, `internal`, mesmo pacote) pra obter o
`ResultadoDistribuicao` real. `ResultadoSimulacaoAlcada` ganhou `Destino`/`ConferenteId`/
`Motivo`, espelhando o mesmo shape que `SimularProtocoloManual` já expõe.
`TestarAlcadaRequest`/`TestarAlcadaResponse` (`RegraAlcadaEndpoints.cs`) acompanharam.

4 testes novos em `SimularAlcadaTests.cs` (não existia antes) cobrindo exatamente os 3
cenários que provavam a diferença entre a inferência antiga (por contagem) e a regra real (por
urgência) — 311 testes automatizados no total.

## Auditoria de qualidade do back (endpoints + casos de uso)

Mesma auditoria já feita no `dispatch-web`, agora no `dispatch-api` — 2 agentes em paralelo por
cluster de endpoint mais verificação pessoal direta dos achados mais graves. 14 achados
reportados; correção em fases, checando `dotnet build && dotnet test` (e `dotnet run` de
verdade nas fases que mexem em `Program.cs`/DI/rota) entre cada uma.

- **Bug real**: `DecidirPedidoReabertura` aprovava um pedido sem checar o status atual do
  protocolo — diferente do `ReabrirConferencia` (ação direta), que já guardava isso. Um
  protocolo excluído (soft-delete) com pedido de reabertura pendente podia ser forçado de volta
  pra `Conferindo` só aprovando o pedido velho, por baixo do fluxo de
  `RestaurarProtocolo`. Ganhou a mesma guarda (`ResultadoDecidirPedidoReabertura.StatusInvalido`,
  409). Teste novo cobrindo o cenário exato (excluir → aprovar pedido velho → 409).
- **`MotorDistribuicao` ganhou `EscolherMenosCarregado<T>`** — o desempate "menor carga vence"
  que só existia inline em `Distribuir` agora é reaproveitado por `AtribuirAoMenosCarregado`
  (Application), que reimplementava a mesma regra na mão fora do caminho normal do motor.
- **Removido `POST /protocolos/distribuir`** (o primeiro endpoint do projeto) — zero
  consumidor no `dispatch-web` (confirmado por grep), superado por `POST /protocolos/manual` e
  `POST /protocolos/importar/confirmar`. `DistribuirProtocolo` (caso de uso) e
  `DistribuirProtocoloResponse` continuam — `CriarProtocoloManual` reaproveita os dois.
  `ResolvedorDeEscreventePorNome` voltou a ser `internal` (só o endpoint removido precisava que
  fosse `public`).
- **`RegraAlcadaEndpoints.cs`**: a validação de `POST /regras-alcada` (XOR de sujeito, XOR de
  alvo + construção do `AlvoAlcada`) saiu de ~35 linhas soltas na lambda do endpoint pra dois
  métodos privados (`TentarMontarSujeito`/`TentarMontarAlvo`). O XOR de alvo em particular
  enumerava as 5 variantes de `AlvoAlcada` duas vezes (um array de contagem + um switch de
  construção, separados) — virou um array só de `(bool, Func<AlvoAlcada>)`, uma 6ª variante
  futura só precisa entrar num lugar.
- **404 sem `motivo`**: `DescartarExcecao`/`DefinirPrioridadeDoProtocolo`
  (`ProtocoloEndpoints.cs`) agora devolvem `{ motivo: "..." }` como todo o resto do arquivo, em
  vez de `Results.NotFound()` vazio.
- **`ClaimsPrincipalExtensions.ObterUsuarioId()`** (novo, `Dispatch.Api/`) — substitui
  `Guid.Parse(FindFirstValue(ClaimTypes.NameIdentifier)!)` repetido em 8 lugares (7 endpoints +
  `Program.cs`, `OnTokenValidated`).
- **`ParaResumo` (Protocolo → `ProtocoloResumo`)** deixou de ter uma cópia em
  `DistribuicaoEndpoints.cs` e outra em `MinhaFilaEndpoints.cs` — a segunda (já `internal`)
  passou a ser reaproveitada pela primeira também, igual `ConferenteEndpoints` já fazia.
- **`AuthEndpoints.cs` dividido em 3**: `AuthEndpoints.cs` (login/me), `TotpEndpoints.cs`
  (registrar/confirmar), `RecuperacaoSenhaEndpoints.cs` (as 3 etapas de RF-01g) — só
  reorganização de arquivo, nenhuma rota/comportamento mudou.
- **`GeradorDeSugestoes`**: `ModaComForca(Nivel)` e `ModaGuidComForca(Guid)` (mesmo algoritmo,
  tipos diferentes) viraram um `ModaComForca<T>` só.
- **`ResolvedorAlcada.Resolver`/`Explicar`**: o laço de percorrer as 3 camadas (Nível → Equipe →
  Pessoa) estava duplicado entre os dois métodos, cada um com sua própria cópia da orquestração
  por cima do helper compartilhado `DecideCamada`. Extraído `CamadasComOpiniao` (iterator
  privado) — `Resolver` reduz pra a última opinião (mesma semântica "a de baixo sobrescreve a
  de cima"), `Explicar` mapeia todas pra `PassoTrilha`. O prelúdio de reserva ficou
  deliberadamente de fora dessa unificação — formas de saída genuinamente diferentes (bloqueio
  único vs. trilha auditável), e essa é a parte do motor que o próprio projeto já documentou
  como historicamente propensa a erro (reescrita 2×). Suíte inteira rodada antes de seguir, não
  só os testes de `ResolvedorAlcada`.
- **`ListarPedidosReaberturaPendentes`**: N+1 (`ObterPorIdAsync` num loop) virou uma busca em
  lote — `IProtocoloRepository` ganhou `ObterVariosPorIdsAsync`, mesmo molde do que
  `IUsuarioRepository` já tinha (aliás usado 2 linhas acima, no mesmo método, sem que o padrão
  tivesse sido replicado pro protocolo até agora).
- **`ImportarLote`**: as duas buscas por linha (`escreventesConhecidos.FirstOrDefault`,
  `catalogoTipos.FirstOrDefault`) — O(n×m) num lote que pode ter centenas de linhas — viraram
  `Dictionary<string, T>` por nome normalizado, construídos uma vez antes do laço.
- **`ServiceCollectionExtensions.cs`**: os ~65 `AddScoped<UseCase>()` (antes uma lista só, sem
  estrutura) agora estão agrupados por arquivo de endpoint consumidor, com comentário por
  grupo — mesmo objetivo de "não esquecer de registrar um caso de uso novo" (já mordeu o
  projeto uma vez, ver "Motor de alçada v2" acima) só que por organização em vez de disciplina.

Verificação de cada fase: `dotnet build && dotnet test` (312 testes, era 311 antes do teste
novo da Fase 0) e `dotnet run` de verdade contra o Postgres local nas fases que mexeram em
`Program.cs`/rotas/DI, com smoke test via curl cobrindo os endpoints tocados.

## Continuidade de conferência (pedido do dono, não é RF numerado)

Quando um protocolo Reprovado reaparece num relatório seguinte (RF-07/"linha de corte") na
mesma etapa, ele é atribuído direto ao conferente que fez a **primeira** conferência dele (a
linha mais antiga com essa etapa e um dono, não a mais recente) — em vez de rodar o motor do
zero e possivelmente cair com outra pessoa. Investigação prévia confirmou que isso **não é um
RF numerado nem está no protótipo aprovado** — o próprio documento de requisitos trata "volta
pro mesmo escrevente?" como pergunta em aberto, nunca respondida; e não existia nenhum código
que correlacionasse múltiplas linhas de `Protocolo` pelo mesmo `Numero` (que não é único, de
propósito). Decisões tomadas com o dono: se o conferente anterior não está mais elegível (saiu
da escala, foi removido, perdeu alçada pro tipo desde então) vira Exceção — não recai pro
motor normal; quando elegível, é atribuição direta, sem passar por urgência/carga/pool.

- **`ResolvedorDeContinuidade`** (`Dispatch.Domain/Distribuicao/`, novo) — função pura, mesmo
  molde de `ResolvedorDePrazo`/`ResolvedorAlcada`: dado o histórico de um Número e a etapa
  atual, devolve o `DonoId` da linha mais antiga com essa etapa e um dono (ou nulo).
- **`MotorDistribuicao.Distribuir`** ganhou `Guid? donoDaPrimeiraConferenciaId = null` (mesmo
  padrão de quando `equipeDoEscreventeId` entrou no Motor v3). Se informado e o dono anterior
  está entre os elegíveis: `Atribuido` direto, **`RegraAplicada` fica nula de propósito** — não
  foi uma `RegraAlcada` que decidiu, foi a continuidade (mesma convenção de decisão humana,
  RNF-02). Se não está mais elegível: `Excecao("conferente da primeira conferência não está
  mais disponível", ...)`. Continuidade não mascara os early-exits de "tipo desconhecido"/
  "tipo desativado" — só se aplica depois deles.
- **`IProtocoloRepository.ObterPorNumerosAsync`** (novo) — todas as linhas (qualquer status/
  lote) com um conjunto de números. Serve dois consumidores: a checagem de continuidade em
  lote dentro de `ImportarLote` (busca única antes do laço, agrupada por Número — mesmo
  cuidado de N+1 do resto do método) e o histórico do painel de detalhe.
- **`ObterDetalheProtocolo`** ganhou `HistoricoConferencias` (outras linhas com o mesmo
  Número, mais recente primeiro) — exposto no `GET /protocolos/{id}/detalhe` já existente,
  **nenhum endpoint novo**. `HistoricoConferenciaResponse` é o formato cru de sempre (back
  manda o fato, front resolve nome do dono e rótulo de status).
- **Front (`dispatch-web`)**: `PainelDetalheProtocolo.tsx` ganhou a seção "HISTÓRICO DE
  CONFERÊNCIAS" (só aparece quando há histórico), reaproveitando o mesmo padrão visual de
  `ListaAlcada`/`LINHA DO TEMPO` já existentes — sem sessão de protótipo nova, é extensão de
  uma tela que já existe.

Testado ponta a ponta contra o Postgres local: importar → conferente pega/inicia/reprova →
reimportar a mesma linha (mesmo Número, andamento novo, mesma etapa) → atribuição direta
confirmada ao mesmo conferente (`atribuidosPorConferente` no resumo, sem passar por
`enviadosParaPool`), `regraAplicadaId: null` no detalhe, e a seção de histórico aparecendo com
o registro anterior (`Reprovado`, mesmo dono). Suíte e2e permanente do `dispatch-web` rodada de
novo — mesmas 8 falhas pré-existentes, nenhuma nova. 8 testes novos (4
`ResolvedorDeContinuidadeTests`, 4 `MotorDistribuicaoTests`, 2 `ImportarLoteTests`) — 322
testes automatizados no total.

**Estendido pro cadastro manual** (pedido do dono, "tem que seguir o mesmo fluxo que fizemos na
importação"): `CriarProtocoloManual` bloqueava qualquer `Número` duplicado com 409
(`ExisteComNumeroAsync`, removida — ficou sem uso), sem checar o status do protocolo existente.
Isso protege contra cadastro duplicado por engano, mas a importação não precisa dessa proteção
(tem a linha de corte) — então manter o bloqueio idêntico impediria a mesma continuidade que
acabou de ser construída pra reimportação. Decisão: **`ResolvedorDeContinuidade.PodeRecriar`**
(novo) — só bloqueia se algum registro existente pro Número ainda está "em uso" (`Pool`,
`Atribuido`, `Conferindo`, `Excecao` ou `Aprovado`); libera quando todos já chegaram a um estado
que não bloqueia mais (`Reprovado` — o cenário real —, `Descartado` ou `Excluido`). Quando
libera, `ResolvedorDeContinuidade.Resolver` decide a atribuição igual à importação.
`SimularProtocoloManual` (a prévia do modal) acompanhou a mesma lógica — sem isso, o modal
mostraria "número indisponível" ou um destino diferente do que `CriarProtocoloManual` ia
produzir de verdade ao confirmar. Nenhuma mudança no front: `numeroDisponivel` já era um
booleano cru vindo do back, sem suposição de "por quê" — muda só quando o back diz que mudou.

Testado ponta a ponta contra o Postgres local: protocolo Atribuído → cadastro manual do mesmo
Número bloqueia 409 (igual antes) → reprova via Minha fila → cadastro manual do mesmo Número
sucede (201) e atribui direto ao mesmo dono → simulador confirma o mesmo destino antes de
confirmar. 13 testes novos (10 `PodeRecriar` parametrizados, 2 `CriarProtocoloManualTests`, 1
`SimularProtocoloManualTests`) — 335 testes automatizados no total.

## Tabela `config` (seção 8) — fecha o item do backlog

12 constantes que várias partes do código já citavam como "até a tabela config existir" viraram
uma tabela de verdade: `Configuracao` (Domain, linha única) com `FaixaAtencao`/`FaixaUrgente`
(RF-14/19/24), `LimiteDeAtosSimultaneos` (RF-21), `JanelaDeCorrecao` (RF-24a),
`DiasDeMemoriaDescarte` (RF-40), `TempoMedioPorAtoMinutos` (RF-28) e os 6 limiares do módulo de
aprendizado (`GeradorDeSugestoes`). `DuracaoTipica` (dicionário `TipoPrazo → TimeSpan` usado por
`PrazoIrreal`) ficou de fora de propósito — é estrutura mapeada, não escalar.

- **`GET`/`PUT /config`** (Distribuidora) — editável sem redeploy via curl/Swagger, sem tela
  própria no front ainda (decisão consciente, mesmo padrão de "back primeiro, tela depois" já
  usado em outras frentes). `PUT` substitui os 12 valores juntos (sem edição parcial, mesmo
  molde de `PUT /equipes/{id}`) e valida (positivo, ou 0–1 nos dois percentuais) antes de
  gravar — 400 com motivo em vez de clampar silenciosamente (diferente de
  `DefinirPesoDeComplexidadeDoTipoAto`, que clampa: aqui é edição deliberada de config, não um
  valor derivado, então clampar sem avisar esconderia erro de digitação).
- **`ObterConfiguracao`** — pass-through fino, mesmo molde de `ObterUsuarioAtual`, consumido
  tanto por `GET /config` quanto pelos 4 endpoints que precisam das faixas do semáforo pra
  montar `ProtocoloResumo`/`DetalheProtocoloResponse` (`/protocolos/{id}/detalhe`,
  `/protocolos/distribuicao`, `/minha-fila/`, `/conferentes/{id}/fila`,
  `/protocolos/importar/pre-visualizar`) — cada endpoint busca a config uma vez por request,
  os 3 campos `static readonly` duplicados (`ProtocoloEndpoints`/`MinhaFilaEndpoints`/
  `ImportacaoEndpoints`) saíram.
- `IniciarConferencia`, `CorrigirResultado`, `DescartarSugestao`, `ListarConferentes`,
  `GerarSugestoes` (Application) injetam `IConfiguracaoRepository` direto — só a camada Api
  segue a regra de "nunca repositório direto, sempre um caso de uso" (`ObterConfiguracao`).
- Migration `AdicionaConfiguracao`: `CreateTable` + `InsertData` semeando uma linha com os
  valores que eram hardcoded antes (4h/60min/1/15min/30dias/18min/5/8/0.6/3/6/0.5) — sem a
  semente, `ConfiguracaoRepository.ObterAsync` (`SingleAsync`, não `SingleOrDefaultAsync` — a
  ausência de linha é erro de setup, não caso de negócio) quebraria em runtime.

Testado ponta a ponta contra o Postgres local: `GET /config` mostrando os defaults semeados;
`PUT /config` com `limiteDeAtosSimultaneos: 2` mudando o comportamento real na hora (segundo
`iniciar` que dava 409 com o valor antigo passou a dar 204, sem reiniciar a API); `PUT` com
valor inválido (`limiteDeAtosSimultaneos: 0`) devolvendo 400 com motivo; `GET
/protocolos/distribuicao`/`/protocolos/importar/pre-visualizar` continuando normais com as
faixas vindas do banco. 6 testes novos (`ObterConfiguracaoTests`, `AtualizarConfiguracaoTests`)
— 341 testes automatizados no total.

## "N feitos hoje" + tempo de conferência — fecha o gap do card de conferente em Distribuição

Gap documentado desde a construção de Distribuição: o card de conferente na aba "Por
conferente" não mostrava "N feitos hoje" no subtítulo, e o card "Concluídos" (aba "Por status")
mostrava aprovado/não aprovado no canto em vez do tempo de conferência — a causa nos dois casos
era `ProtocoloResumo` (DTO da visão de distribuição) não expor `ConcluidoEm`/`Duracao`, que já
existem no domínio desde "Minha fila" (RF-19 a RF-24). Não precisou de campo novo no banco.

- **`ProtocoloResumo`** (`DistribuicaoEndpoints.cs`) ganha `ConcluidoEm`/`Duracao` no fim do
  record (mesma convenção de não quebrar quem desestrutura posicionalmente, já usada quando
  `IniciadoEm` entrou). `MinhaFilaEndpoints.ParaResumo` (mapeamento compartilhado por
  Distribuição/Minha fila/Conferentes) passa a preenchê-los a partir do `Protocolo`.
- **`ObterVisaoDistribuicao`** ganha `IRelogio` injetado (mesmo padrão de `ObterConcluidosHoje`
  — só o caso de uso decide o que "início do dia" significa) e um novo campo em
  `VisaoDistribuicao`: `ConcluidosHojePorConferente` (`IReadOnlyList<ConcluidosHojeDoConferente>`,
  `(ConferenteId, Total)`) — filtra `concluidos` (que já é todo o histórico, usado pela aba "Por
  status") por `ConcluidoEm >= início do dia` e agrupa por `DonoId`. Quem não concluiu nada hoje
  simplesmente não aparece na lista (front trata ausência como 0), em vez de vir com `Total: 0`
  — mais simples de consumir e evita uma lista do tamanho de todos os conferentes cadastrados.
- `VisaoDistribuicaoResponse`/`GET /protocolos/distribuicao` acompanham (`ConcluidosHojePorConferenteResponse`).
- 2 testes novos em `ObterVisaoDistribuicaoTests.cs` (que já existia, só ganhou os construtores
  atualizados com `IRelogio`): conferente com 2 concluídos hoje + 1 concluído ontem conta só 2;
  conferente sem nenhum concluído hoje não aparece na lista. 343 testes automatizados no total.

Testado ponta a ponta contra o Postgres local, fluxo real (não só fake): criou um protocolo
manual, `pegar`/`iniciar`/`concluir` como conferente de teste, confirmou
`concluidosHojePorConferente` com `total: 1` pro conferente certo e o protocolo concluído
carregando `concluidoEm`/`duracao` no `GET /protocolos/distribuicao`.

**Escopo desta rodada**: só o back. O front (subtítulo "N feitos hoje" em `AbaPorConferente`,
canto do card trocando aprovado/não aprovado por tempo de conferência em
`DistribuicaoProtocoloCard`) é fechado do lado do `dispatch-web` — ver `dispatch-web/CLAUDE.md`,
mesma seção.

## Motor de alçada v4 — equipe inteira não passa por uma etapa

Pedido de um conferente testando o sistema em produção pela primeira vez ("precisamos de uma
forma de falar que a equipe Quinto Andar não passa por pré-conferência"). Confirmado com o
dono: significa **ninguém tem alçada** pra conferir atos daquela etapa, pra aquela equipe —
mesmo efeito que já existe pra pessoa específica ("Marina Witter não pode fazer
pós-conferência"), só que precisa valer pra uma equipe inteira de uma vez. É regra de alçada
nova (Central de Regras), não mudança de fluxo/importação.

**Novo alvo, não novo sujeito.** Em vez de um sujeito "todos os níveis" (mudança maior, tocaria
`ValePara`/discriminador de persistência do sujeito/seletor no front), a distribuidora expressa
"ninguém" criando uma regra `Nega` por nível (Júnior/Pleno/Sênior) — mesmo trabalho manual que
já existe hoje pra qualquer "Base por nível", não é regressão.

- `Alcada/AlvoAlcada.cs` — nova variante `PorEquipeEEtapa(Guid? EquipeId, Etapa Etapa)`.
  `EquipeId` nulo é "sem equipe" válido, mesmo padrão de `PorEquipeDeEscrevente` (RF-29a).
- **Restrito a `Nega`** — validado na Api (`RegraAlcadaEndpoints`, 400 se vier com
  `Permite`/`Reserva`). Motivo: se fosse permitido `Permite` com esse alvo, ele entraria na
  "lista fechada por dimensão" do motor v2/v3 (mesma regra que já vale pra
  Tipo/Grupo/Equipe/Etapa) e uma única regra desse tipo passaria a bloquear por omissão
  qualquer combinação equipe+etapa não coberta por ela — efeito desproporcional pra uma
  funcionalidade pensada só pra exceção pontual. `Nega` nunca participa da lista fechada
  (resolve antes, por curto-circuito em `DecideCamada`), então a restrição elimina o risco.
- `ResolvedorAlcada.cs`: `Dimensao` (enum interno) ganhou `EquipeEEtapa`, **deliberadamente fora
  de `OrdemDasDimensoes`** (só existe pra `MotivoDaDimensao` conseguir mapear pra um
  `MotivoAlcada` quando uma negação desse alvo decide o caso — nunca participa de lista
  fechada). `AlvoBate` compara `EquipeId` e `Etapa` juntos. `CamadaDe` trata esse alvo como
  `Camada.Equipe` quando o sujeito é pessoa (mesmo grupo de `PorEquipeDeEscrevente`) — na
  prática o caso de uso real (sujeito nível) já cai em `Camada.Nivel` antes disso.
- `MotivoAlcada` ganhou `EquipeEEtapa`. 5 testes novos em `ResolvedorAlcadaTests.cs` (nega bate
  equipe+etapa exatos, etapa diferente permite, equipe diferente permite, "sem equipe" só bate
  com "sem equipe", exceção pessoal sobrescreve a negação de nível).
- Persistência: `AlvoTipoRegistro` ganhou `EquipeEEtapa` — **reaproveita as colunas
  `alvo_etapa`/`alvo_equipe_id` que já existiam**, nenhuma coluna nova, só um valor a mais no
  discriminador e um branch a mais no `CHECK` (`alvo_tipo = 'EquipeEEtapa' AND alvo_etapa IS
  NOT NULL AND alvo_tipo_ato_id IS NULL AND alvo_grupo_tipo_ato IS NULL`). Migration
  `AdicionaAlvoEquipeEEtapaEmRegrasAlcada` — só `DropCheckConstraint`/`AddCheckConstraint`, sem
  `ALTER TABLE ADD COLUMN`.
- Api: `CriarRegraAlcadaRequest`/`RegraAlcadaResponse` ganham `AlvoEhEquipeEEtapa` (reaproveita
  `AlvoEtapa`/`AlvoEquipeId` que já existiam no request/response, mesmo truque que
  `AlvoEhEquipe` já fazia sozinho com `AlvoEquipeId`). `TentarMontarAlvo` ganhou um candidato
  novo, com dois cuidados encontrados em revisão de código antes de rodar (não por teste):
  o candidato de `PorEtapa` precisou excluir `AlvoEhEquipeEEtapa` explicitamente (os dois
  reaproveitam o mesmo campo `AlvoEtapa`, sem a exclusão os dois bateriam juntos e quebrariam o
  XOR); e o candidato novo exige `AlvoEtapa is not null` na própria condição (não só dentro do
  construtor), senão um request incompleto (`alvoEhEquipeEEtapa: true` sem `alvoEtapa`) geraria
  `NullReferenceException` (500) em vez do 400 genérico.

**Bug real achado testando de verdade pelo front, não pelos testes automatizados nem pelo
`curl`**: `RegraAlcadaEndpoints.ParaResponse` (o `GET /regras-alcada`) montava `AlvoEtapa`/
`AlvoEquipeId` da resposta só com `(regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa` e `(regra.Alvo as
AlvoAlcada.PorEquipeDeEscrevente)?.EquipeId` — pra uma regra `PorEquipeEEtapa`, os dois casts
sempre davam null (tipo diferente), então a resposta HTTP saía com `alvoEtapa`/`alvoEquipeId`
nulos **mesmo com a linha persistida certa no Postgres** (confirmado via `psql` direto: os dois
campos gravados corretamente). O `curl` de smoke test logo depois de implementar não pegou isso
porque eu só conferi que os campos apareciam na resposta, nunca que o valor de uma regra
recém-criada especificamente batia; o `POST /regras-alcada/testar` também não pegou, porque
esse endpoint resolve a partir de `ObterAtivasAsync`/`ParaDominio` (lê direto das colunas do
registro), nunca da `RegraAlcadaResponse` — o bug era isolado à *leitura da lista*, não ao
motor de decisão em si. Só apareceu construindo o front e vendo a frase virar "fazer undefined
da equipe X" num teste Playwright de comportamento real. Corrigido trocando os dois casts por
`switch` que também cobre `AlvoAlcada.PorEquipeEEtapa`. **Lição**: quando um alvo novo
reaproveita as colunas físicas de um alvo antigo, todo `as AlvoAntigo` espalhado pela resposta
precisa ser auditado — não é óbvio a partir da definição do record novo, só varrendo os pontos
que fazem downcast pro tipo antigo.

**Front (`dispatch-web`)**: construtor guiado (RF-32) ganhou o alvo "equipe não faz etapa…",
travando a permissão em Nega assim que esse alvo é escolhido (mesmo raciocínio da restrição do
back — evita o usuário bater no 400 sem entender por quê). Ver `dispatch-web/CLAUDE.md`, mesma
seção, pro desenho do seletor (produto cartesiano equipe×etapa, valor composto).

Testado ponta a ponta contra o Postgres local com `dotnet run` de verdade (não só
`dotnet test`): 400 pra `Permite`+`alvoEhEquipeEEtapa`, 201 pra `Nega`; e, depois do fix do bug
acima, verificado via Playwright que criar a regra pela UI, consultá-la de volta e simular o
caso em "Testar" batem entre si (frase, camada "Base por nível", veredito vermelho com motivo
"equipe fora da alçada nesta etapa"). 348 testes automatizados no total (108 Domain + 240
Application).

### Bug real em produção: exceção pessoal de outro alvo reabria "ninguém, independente de quem"

Achado em uso real (produção), reportado pelo dono: Maria Vittoria (conta combo
Distribuidora+Conferente, ver seção acima sobre múltiplos papéis) tem uma regra pessoal de
alçada plena (`Permite`/`PorTodosOsAtos`) — ela pode conferir qualquer ato, exceção de sempre.
Depois de criar "Quinto Andar não faz pré-conferência" (`Nega`/`PorEquipeEEtapa`, Base por
nível, os 3 níveis) pra ela mesma, os atos de pré-conferência da equipe continuavam caindo na
fila **disponível** dela em vez de irem pra Exceção.

Causa raiz, confirmada mecanicamente (não por suposição) contra o código de `ResolvedorAlcada.cs`
e os dados reais de produção: a cascata do Motor v3 (`Resolver`) roda em 3 camadas (Nível →
Equipe → Pessoa) e "a de baixo vence a de cima quando tem opinião" — regra **correta e
obrigatória** pro caso geral (é o "exemplo resolvido" da seção 4 do documento de requisitos,
coberto por `RegraPessoalPermiteMesmoComRegraDeNivelNegandoOMesmoAlvo_Permite`). O problema é que
essa regra geral não distingue "a camada Pessoa tem uma exceção **para este mesmo alvo**" de "a
camada Pessoa tem uma opinião sobre **qualquer alvo**, inclusive um sem nenhuma relação com a
negação de nível" — a alçada plena de Maria (alvo `PorTodosOsAtos`) opinava sobre TODO caso,
inclusive os que a negação `PorEquipeEEtapa` deveria bloquear de forma absoluta, e "vencia" na
cascata mesmo sem ter nenhuma relação com "Quinto Andar não faz pré-conferência". Isso contradiz
o pedido original desse alvo, já confirmado com o dono na v4: **"ninguém, independente de
quem"** — uma exceção pessoal de qualquer tipo (inclusive alçada plena) não deveria conseguir
reabrir isso.

**Fix**: `PorEquipeEEtapa`/`Nega` ganhou a mesma prioridade absoluta que `Reserva` já tinha —
checado **antes** de entrar na cascata de 3 camadas, não dentro dela. `Resolver`/`Explicar`
ganharam um novo helper `NegaEquipeEEtapaQueBloqueia` (mesmo molde de `ReservaQueBloqueia`):
acha uma regra `Nega`/`PorEquipeEEtapa` que valha pro conferente (nível ou pessoa) e bata no
caso — se achar, `Negado`/`MotivoAlcada.EquipeEEtapa` direto, sem passar pela cascata. Como a Api
só aceita esse alvo com `Nega` (ver acima), essa negação nunca tem uma exceção own-alvo
legítima pra ceder — a checagem antecipada só muda o resultado nos casos em que uma exceção de
OUTRO alvo (como alçada plena) terminava opinando na mesma camada Pessoa e vencendo por engano.

Escopo deliberadamente restrito a este único alvo: a cascata "a de baixo vence a de cima"
continua intacta pra todo alvo normal (`PorEtapa`/`PorTipoAto`/`PorGrupoTipoAto`/
`PorEquipeDeEscrevente`/`PorTodosOsAtos`) — só `PorEquipeEEtapa` virou absoluto, junto com
`Reserva`.

`ResolvedorAlcadaTests.cs`: o teste antigo `EquipeEEtapa_ExcecaoPessoalSobrescreveANegacaoDeNivel`
documentava o comportamento ANTIGO com uma combinação (`Permite`+`PorEquipeEEtapa`) que a Api já
bloqueia na prática (400) — renomeado pra
`EquipeEEtapa_NegacaoEhAbsolutaMesmoComExcecaoPessoalDoMesmoAlvo` e invertido pra `Negado` (o
Domain precisa ser absoluto por conta própria, não só confiar na validação de borda). Novo teste
`EquipeEEtapa_NegacaoEhAbsolutaMesmoComAlcadaPlenaPessoal` reproduz o cenário exato de produção
(nível nega EquipeEEtapa + pessoa com alçada plena → `Negado`). 384 testes automatizados no
total (114 Domain + 270 Application).

## Fix de performance: N+1 em GET /regras-alcada

Reportado pelo dono direto (não achado numa auditoria): "as requests da aba de Central de
Regras tão demorando muito" em produção. Investigação (agente em background) confirmou um N+1
clássico em `RegraAlcadaEndpoints.MapGet("/")` — o `foreach` que montava a resposta chamava
`protocolos.ContarComRegraAplicadaAsync(regra.Id, ...)` **uma vez por regra** (RF-33, contador
de "usos"), então 1 query pra listar + N queries pra contar, sequenciais, dentro de uma única
chamada HTTP. Com ~95 regras em produção (número real, visto ao vivo numa sessão de teste),
isso é ~96 round-trips síncronos contra o Neon — que tem latência de rede real, diferente do
Postgres local. Piorado por um segundo achado: `Protocolo.RegraAplicadaId` nunca teve FK
(decisão deliberada, é auditoria — RNF-02 — não dependência de verdade, ver seção acima), e
sem FK o EF Core não cria índice automático nessa coluna como cria nas outras (`DonoId`/
`EscreventeId`/`TipoAtoId`/`LoteImportacaoId`) — cada uma das N execuções do `COUNT` fazia
sequential scan completo de `protocolos`.

**Fix, dois lados**: `IProtocoloRepository.ContarComRegraAplicadaAsync(Guid, ...)` (uma regra
por vez) virou `ContarPorRegraAplicadaAsync(...)` (todas de uma vez) — `GroupBy(RegraAplicadaId)`
+ `Count()`, 1 query só, independente de quantas regras existam. O endpoint monta um
`Dictionary` a partir disso e faz `GetValueOrDefault(regra.Id)` (regra sem nenhum protocolo
aplicado não aparece na coleção agregada — ausência tratada como 0). `ProtocoloConfiguration`
ganhou `builder.HasIndex(p => p.RegraAplicadaId)` explícito — migration
`AdicionaIndiceEmRegraAplicadaIdDeProtocolos`, só `CreateIndex`, nada de dado tocado.

Mesma lição já registrada antes neste arquivo (Motor de alçada v2, "carga acumulada"): sempre
que uma coluna de auditoria/leitura-derivada não tem FK de propósito, ela também não ganha
índice de graça — se ela vira alvo de filtro/agregação (aqui, o `COUNT` de "usos"), precisa de
`HasIndex` manual, não é automático como pra chave estrangeira.

348 testes automatizados continuam passando (a fake do repositório em
`Dispatch.Application.Tests/Fakes.cs` acompanhou a troca de assinatura). Sem mudança nenhuma no
contrato JSON (`RegraAlcadaResponse.Usos` continua um `int` por regra) — o front não precisou
de nenhuma alteração.

## Validação cruzada urgência < atenção — fecha o gap do protótipo reexportado

O dono reexportou o protótipo com uma tela de "Configuração do sistema" de verdade (seção 8),
incluindo uma validação que o próprio requisito nunca especificou explicitamente: as duas
faixas do semáforo contam pra trás a partir do vencimento, então se `faixaUrgente >=
faixaAtencao`, o card pula direto de amarelo pra vermelho e a faixa de urgência (laranja) nunca
aparece na prática. `AtualizarConfiguracao.Validar` ganhou esse cruzamento (só depois de
confirmar que os dois campos individualmente já são > 0, pra não dar dois erros ao mesmo
tempo). 2 testes novos (`ValorInvalido_RejeitaSemAlterarNada`, casos "igual" e "maior"). 350
testes automatizados no total (108 Domain + 242 Application).

Testado ponta a ponta contra o Postgres local: `PUT /config` com `faixaUrgenteMinutos` igual a
`faixaAtencaoMinutos` devolve 400 com o motivo certo.

## "Hora de entrada" no cadastro manual de protocolo (RF-18f)

Reportado pelo dono: a importação de lote já lê a hora real do andamento do relatório
(`dataHoraAndamento` no CSV), mas o cadastro manual (`POST /protocolos/manual`) sempre assumia
`IRelogio.Agora` — sem jeito de registrar um ato que chegou antes do momento em que a
distribuidora está digitando.

`CriarProtocoloManual.ExecutarAsync`/`SimularProtocoloManual.ExecutarAsync` ganharam
`DateTimeOffset? andamentoEm = null` (posicional, antes do `CancellationToken` — mesmo truque
de sempre pra forçar todo call site a passar pelo compilador; nenhum call site existente
quebrou, porque nenhum passava `cancellationToken` posicionalmente depois de `observacao`/
`prioridade`). `null` preserva o comportamento antigo (`andamentoEm ?? relogio.Agora`/`agora`).
**Só `Protocolo.AndamentoEm` muda** — o `agora` usado por `AplicadorDeDistribuicao.Executar`
pra `AtribuirA` (RNF-16, quando a atribuição de fato aconteceu) continua sendo o instante real
da chamada, nunca o valor retroativo informado.

`CriarProtocoloManualRequest`/`SimularProtocoloManualRequest` ganharam `DateTimeOffset?
AndamentoEm = null`. 3 testes novos (valor informado é respeitado nos dois casos de uso;
ausência mantém o comportamento antigo). 364 testes automatizados no total (111 Domain + 253
Application).

## Corte de data no bucket "concluídos" de GET /protocolos/distribuicao

Reportado pelo dono direto: a preocupação já registrada na auditoria anterior ("sem paginação,
cresce sem limite conforme a base cresce") virou prioridade real. Investigação confirmou que o
crescimento sem limite vem de **um lugar só**: o bucket `concluidos` (Aprovado+Reprovado) nunca
encolhe — todo protocolo que termina conferência fica ali pra sempre. Os outros 4 buckets por
status (`pool`/`atribuidos`/`emConferencia`/`excecoes`) são trabalho em andamento, ficam
pequenos por natureza (o sistema existe pra resolver esse trabalho e tirá-lo desses status).
`Descartado` era buscado mas não aparece em nenhum bucket — desperdício puro de leitura.

**Decisão**: janela de **30 dias**, aplicada só a `concluidos`, como **constante hardcoded**
(`ObterVisaoDistribuicao.DiasHistoricoDeConcluidos`) — não um 13º campo em `Configuracao`, pra
entregar rápido, mesmo padrão que vários outros limiares do projeto tiveram antes de virar
config. Quando um `loteImportacaoId` específico é pedido, a janela não se aplica (o lote já é
naturalmente pequeno, e o pedido é explicitamente por aquele histórico).

**Achado que mudou o desenho, antes de escrever qualquer código**: `ObterParaDistribuicaoAsync`
(o método que ia ganhar o corte) **não é exclusivo desta tela** — também alimenta
`GerarSugestoes` (aprendizado precisa do histórico completo pros cálculos de moda/percentil) e
`ListarTiposAtoComUso` (contagem de uso real de cada tipo de ato). Aplicar a janela ali dentro
cortaria os dois silenciosamente — degradando a qualidade das sugestões e mentindo sobre
"quantos protocolos usam este tipo", sem nenhum aviso. Por isso a solução é um método **novo e
dedicado**, `IProtocoloRepository.ObterParaVisaoDistribuicaoAsync(loteImportacaoId,
concluidosDesde, ct)` — `ObterParaDistribuicaoAsync` continua exatamente como estava, sem corte
nenhum, servindo só quem já o usava.

`ObterParaVisaoDistribuicaoAsync` exclui `Excluido`/`Descartado` sempre; sem `loteImportacaoId`,
só deixa passar `Aprovado`/`Reprovado` com `ConcluidoEm >= concluidosDesde` — as demais status
(trabalho em andamento) passam direto, independente da idade. O índice composto
`(status, concluido_em)` **já existia** (criado antes pra `ObterConcluidosNoPeriodoAsync` do
Dashboard) — sustenta essa consulta sem precisar de migration nova.

Sem mudança de schema, sem mudança na assinatura pública de `ObterVisaoDistribuicao.ExecutarAsync`
nem no endpoint (`IRelogio` já estava injetado, usado por `concluidosHojePorConferente`). 5
testes novos em `ObterVisaoDistribuicaoTests.cs` (dentro/fora da janela, trabalho em andamento
sempre aparece, lote específico ignora a janela, `Descartado` nunca aparece).

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade): backdated um protocolo
concluído real pra 40 dias atrás via `psql` — sumiu de `GET /protocolos/distribuicao` (bucket
`concluidos`), mas continuou aparecendo normalmente em `GET /tipos-ato/com-uso` (contagem de uso
do seu tipo) e `POST /sugestoes/gerar` continuou respondendo 200 normalmente — confirma que o
corte não vazou pros dois consumidores que precisam do histórico completo. 361 testes
automatizados no total (111 Domain + 250 Application).

**Fora de escopo desta rodada, decisão consciente**: sem parâmetro de override pra "ver
histórico completo" (se precisar um dia, é um acréscimo pequeno, mesmo padrão de
`loteImportacaoId` opcional); sem `ORDER BY` nos buckets além de `pool` (gap conhecido à parte);
sem promover o valor de 30 dias pra `Configuracao` por ora. Nenhuma mudança no `dispatch-web` —
o front já trata a resposta como "a lista completa que existe" (filtros client-side, contagens
"N de M", sheet "ver mais"), então o comportamento visível muda só no volume de histórico
mostrado por padrão na aba "Concluídos", sem exigir nenhum ajuste de código.

## Bloqueio de tentativas de login por senha + origem no evento de auditoria

O dono reexportou o protótipo com um fluxo de "Configuração do sistema" e TOTP mais detalhado;
o gap-analysis contra a v2 do documento de requisitos achou dois furos reais: **não existia
nenhum bloqueio de tentativas erradas de login por senha** (RF-01i já cobria isso pro código
TOTP, mas o login normal por e-mail+senha ficava sem limite nenhum de tentativa) e
`EventoAutenticacao` não guardava "origem" (RNF-16: "autor, origem e horário" — só tinha autor
e horário).

- **`Usuario`** ganhou `TentativasLoginFalhas`/`BloqueadoAte` + `EstaBloqueado(agora)`/
  `RegistrarTentativaLoginFalha(agora)`/`RegistrarLoginComSucesso()` — mesmo mecanismo e mesmos
  números do bloqueio de TOTP já existente (`UsuarioTotp.TentativasFalhas`/`BloqueadoAte`, 5
  tentativas → 15 minutos), só que no `Usuario` em vez do `UsuarioTotp`, porque login por senha
  vale pra qualquer usuário, com ou sem autenticador registrado.
- **`Autenticar`** (Application) reescrito: se o usuário existe e está bloqueado, rejeita e
  audita (`LoginBloqueado`) sem nem checar a senha; senha errada incrementa o contador e audita
  (`LoginFalhou`); sucesso zera o contador. **RF-01h (anti-enumeração) estendido pro bloqueio**:
  conta bloqueada e senha errada devolvem o mesmíssimo `ResultadoAutenticacao.Rejeitado()` → 401
  genérico — o cliente HTTP nunca sabe se foi "senha errada" ou "conta bloqueada".
- **`EventoAutenticacao`** ganhou `Origem` (`string?`) — todo `ExecutarAsync` que grava um evento
  de auditoria (`Autenticar`, `RegistrarTotp`, `ConfirmarRegistroTotp`, `IniciarRecuperacaoSenha`,
  `ValidarCodigoRecuperacao`, `RedefinirSenha`) ganhou o parâmetro `string? origem` (posicional,
  antes do `CancellationToken` de sempre — usado deliberadamente pra forçar erro de compilação
  em todo call site antigo e não deixar nenhum passar batido sem thread-ar o valor). A Api
  resolve o valor via `HttpContextExtensions.ObterOrigem()` (novo) — `X-Forwarded-For` primeiro
  (é o cabeçalho que um proxy real, ex.: Render, preenche), `Connection.RemoteIpAddress` como
  fallback.
- **Duas armadilhas de EF Core de novo** (mesma categoria já documentada nesta seção, "propriedade
  só-com-setter-privado"): `Usuario.TentativasLoginFalhas`/`BloqueadoAte` e
  `EventoAutenticacao.Origem` precisaram de `builder.Property(...)` explícito em
  `UsuarioConfiguration`/`EventoAutenticacaoConfiguration` antes da migration rodar limpa.

Migration `AdicionaBloqueioDeLoginEOrigemEmEventosAutenticacao` — 3 colunas novas
(`usuarios.tentativas_login_falhas` `NOT NULL DEFAULT 0`, `usuarios.bloqueado_ate` nullable,
`eventos_autenticacao.origem` nullable, `varchar(64)` — cabe IPv4/IPv6 e a lista separada por
vírgula que `X-Forwarded-For` pode carregar), nenhum backfill necessário.

Testado ponta a ponta contra o Postgres local com `dotnet run` de verdade: 5 tentativas de
login com senha errada, a 6ª (mesmo com a senha certa) continua 401; confirmado via `psql` que
`tentativas_login_falhas`/`bloqueado_ate` gravaram certo e os eventos `LoginFalhou`/
`LoginBloqueado` carregam `origem` (o IP de loopback do teste local). 356 testes automatizados
no total (111 Domain + 245 Application).

## Sessão de 8 horas (Jwt:ExpiracaoMinutos)

Reportado pelo dono junto com o item acima: "o token tá expirando muito rápido". O valor real
em produção não é auditável pelo repo (é `Jwt__ExpiracaoMinutos` no dashboard do Render, nunca
versionado — ver seção "Deploy — no ar"). Front não tem refresh nem aviso prévio de expiração
(`http-client.ts`: qualquer 401 limpa a sessão na hora, best-effort mesmo) — então o valor de
expiração *é* o tempo real de sessão sem reautenticar.

Decidido com o dono: **8 horas (480 minutos)**, um expediente inteiro. `appsettings.Development.json`
(`Jwt:ExpiracaoMinutos`) atualizado de `60` pra `480`, pro ambiente local bater com produção.
**Pendente, fora do alcance de código**: atualizar a env var `Jwt__ExpiracaoMinutos=480` no
dashboard do Render (produção) — só o dono tem acesso a esse painel.

## Auditoria de performance/índices do banco — três correções

Pedido explícito do dono ("uma análise muito boa do banco, lentidão, índices etc"), feito com
um agente em background depois do fix do N+1 acima. Achados confirmados e corrigidos:

- **Índice em `protocolos.status`** — o filtro mais repetido da tabela mais quente, sustenta o
  caminho mais quente do sistema (`ObterPoolAsync` → `GET /minha-fila`, carregado por todo
  conferente toda vez que abre a própria fila). Sem índice, cada leitura varria a tabela
  inteira. Mesmo padrão do índice de `RegraAplicadaId` já corrigido.
- **Índice em `protocolos.numero`** — não é único de propósito (RF-07: reprocessamento/
  reimportação de um protocolo reprovado gera nova linha com o mesmo número), mas é filtrado
  com frequência real: `ObterPorNumerosAsync` roda a cada importação de lote (checagem de
  continuidade) e a cada abertura do painel de detalhe de um protocolo (histórico de
  conferências). Índice não-único resolve sem contradizer a decisão de nunca torná-lo único.
- **`EnableRetryOnFailure()` no `UseNpgsql`** (`ServiceCollectionExtensions.cs`) — o Neon é
  serverless e pode hibernar por inatividade; sem retry, uma falha transitória de conexão
  (cold start, blip de rede) subia como exceção não tratada até o cliente, 500 puro, sem
  nenhuma tentativa automática de recuperação. Confirmado antes de ligar que o projeto não usa
  transação explícita em lugar nenhum (`BeginTransactionAsync`) — `EnableRetryOnFailure` exige
  que operações multi-passo rodem dentro de uma "execution strategy" própria, e como todo
  código aqui já é uma chamada só a `SaveChangesAsync` por vez, não precisou de nenhuma mudança
  de padrão pra ligar isso com segurança.

Migration `AdicionaIndicesEmStatusENumeroDeProtocolos` — só 2 `CreateIndex`, nada de dado
tocado. Testado ponta a ponta contra o Postgres local depois de reiniciar a API de verdade
(`dotnet run`, não só `dotnet test`): leitura (`GET /regras-alcada`, `GET
/protocolos/distribuicao`) e escrita (criar+remover um tipo de ato) respondendo normal com a
policy de retry ativa. 348 testes automatizados continuam passando.

**Achados da mesma auditoria, registrados mas não corrigidos ainda (hipótese/observação, não
incêndio hoje)**: `GET /protocolos/distribuicao` sem filtro de lote carrega todo o histórico
que já existiu, sem paginação — por desenho do requisito (RF-13, "mesma massa de dados, visões
diferentes"), não é bug pontual de índice, é uma decisão de arquitetura que precisaria de
paginação/corte de data se algum dia doer de verdade. Nenhum outro endpoint pagina nada hoje —
aceitável pras tabelas de cadastro (pequenas por natureza do domínio), mas
`/protocolos/distribuicao` é quem mais vai doer conforme a base cresce.

## Segunda rodada da auditoria — os 4 itens restantes, todos implementados

O dono pediu pra fechar tudo que tinha ficado como "hipótese/observação" na rodada anterior,
mais dois achados novos de resiliência.

- **Índice composto `(status, concluido_em)`** — migration
  `AdicionaIndiceCompostoStatusConcluidoEmDeProtocolos`. Sustenta `ObterConcluidosNoPeriodoAsync`
  (RF-46, Dashboard): antes, mesmo com o índice simples de `status`, o filtro por período ainda
  precisava varrer toda a fatia já filtrada por status procurando as linhas do intervalo — com
  o composto, as duas condições já vêm estreitadas juntas.
- **N+1 em `GerarSugestoes.cs`** — mesmo formato do já corrigido em `GET /regras-alcada`: um
  `foreach` chamando `ISugestaoRepository.ObterPorChaveAtivaAsync(chave, ...)` uma vez por
  candidato. Virou `ObterMaisRecentesPorChavesAsync` (uma query só, `WHERE chave IN (...)`
  agrupada por chave em memória, pegando a mais recente de cada grupo — mesma semântica que a
  versão antiga garantia por chave individual). Volume baixo hoje (dezenas de candidatos por
  rodada), mas é a mesma classe de risco que já mordeu o projeto uma vez.
- **`ObterCoberturaDeAlcada` carregava a tabela `protocolos` inteira** só pra extrair
  `TipoAtoId` distintos em memória. Novo método `IProtocoloRepository.ObterTipoAtoIdsDistintosAsync`
  — `SELECT DISTINCT tipo_ato_id FROM protocolos WHERE tipo_ato_id IS NOT NULL`, direto no
  banco, nenhuma linha de protocolo trafega pra aplicação.
- **`/health/db`, novo** — `/health` (o `healthCheckPath` do `render.yaml`, usado pelo Render
  pra decidir se o container está roteável) nunca verificava o banco, só respondia
  `{"status":"ok"}` sempre — nada detectava "app de pé, Postgres inacessível". Decisão
  deliberada: **não** fazer o `/health` em si tocar o banco — um cold start do Neon derrubaria
  o health check e o Render poderia parar de rotear pro serviço (ou reiniciar o container) por
  causa de uma lentidão transitória do banco, tirando tráfego bem na hora que a conexão mais
  precisaria de uma chamada real pra "acordar". `/health/db` é a checagem de verdade
  (`dbContext.Database.CanConnectAsync()`, 503 se falhar), separada, pra diagnóstico manual/
  monitoramento externo — não ligada ao roteamento do Render.
- **Cache da tabela `configuracao`** — linha única, quase nunca muda, mas era lida do banco em
  toda chamada por ~6 consumidores diferentes por request (`GET /config` + as faixas do
  semáforo/limiares usados por `IniciarConferencia`, `CorrigirResultado`, `ListarConferentes`,
  `DescartarSugestao`, `GerarSugestoes`, e os 4+ endpoints que montam `ProtocoloResumo`/
  `DetalheProtocoloResponse`). `ConfiguracaoRepository.ObterAsync` agora cacheia em
  `IMemoryCache` (TTL de 5 min, rede de segurança). **Cuidado que isso exigiu**:
  `AtualizarConfiguracao` (o `PUT /config`) não pode ler pelo caminho cacheado — um objeto
  cacheado pode ter vindo do `DbContext` (scoped) de uma requisição *anterior*, já finalizado;
  mutar esse objeto e chamar `SaveChangesAsync` no `DbContext` da requisição *atual* não
  persistiria nada, porque o change tracker atual nunca viu aquele objeto (mesma armadilha de
  "objeto desconectado do change tracker" já documentada pra `RegraAlcada`/`Sugestao`, agora
  também batendo em `Configuracao` por causa do cache novo). Corrigido com
  `IConfiguracaoRepository.ObterParaEdicaoAsync` (sempre fresco, sem cache, só usado por
  `AtualizarConfiguracao`) + `InvalidarCache()` chamado depois do `SaveChangesAsync` bem-sucedido.
  `services.AddMemoryCache()` registrado em `ServiceCollectionExtensions`.
- **`render.yaml`**: `Jwt__ExpiracaoMinutos` corrigido de `"60"` pra `"480"` (8h — decisão já
  tomada e aplicada no `appsettings.Development.json`, mas o Blueprint tinha ficado
  desatualizado). Aplicar em produção continua exigindo o dono confirmar/ajustar a env var no
  dashboard do Render diretamente (Blueprint sync no `git push` não é garantido pra serviço já
  criado) — o valor no repo agora pelo menos documenta a intenção corretamente.

Testado ponta a ponta contra o Postgres local, `dotnet run` de verdade: `/health` responde sem
tocar o banco; `/health/db` responde `{"status":"ok"}` com o banco de pé; `POST
/sugestoes/gerar` e `GET /conferentes/cobertura` funcionando normalmente depois das mudanças;
`GET /config` → `PUT /config` (mudando `limiteDeAtosSimultaneos`) → `GET /config` de novo
confirma que o valor novo aparece na hora, não o cacheado (`InvalidarCache` funcionando).

## `ProtocoloResumo` ganha `AndamentoEm` — "data de entrada" no card

Pedido do dono: mostrar a data de entrada do ato (RF-18f) direto no card do protocolo (Minha
fila e Distribuição), não só no painel de detalhe. `DetalheProtocoloResponse` já carregava
`AndamentoEm` (usado na linha do tempo) — `ProtocoloResumo` (o DTO mais "magro" usado pelos
cards, compartilhado entre `MinhaFilaEndpoints`/`DistribuicaoEndpoints` via `ParaResumo`) nunca
tinha esse campo. Campo novo (`DateTimeOffset AndamentoEm`, sempre preenchido — diferente de
`IniciadoEm`/`ConcluidoEm`, que são opcionais) adicionado no fim do record, mapeado direto de
`Protocolo.AndamentoEm` (já existe no domínio desde a importação de lote). Sem migration, sem
mudança de lógica — só um campo a mais na leitura. 364 testes automatizados continuam passando
(nenhum teste novo — é passagem direta de um campo já existente do domínio, sem branch de
lógica pra cobrir).

## `AtribuirManualmente` deixa de ser exclusivo de exceção — "mandar um ato pra alguém"

Pedido do dono: uma opção pra distribuidora mandar um ato manualmente pra um conferente
escolhido, não só pra resolver exceção (RF-17, o único caso que existia até aqui). A guarda de
status de `AtribuirManualmente` (Application) mudou de `Status == Excecao` (única opção antes)
pra `Status is Pool or Excecao or Atribuido` — cobre "protocolo ainda sem dono" (Pool) e
"redirecionar pra outra pessoa direto, sem devolver ao pool antes" (Atribuido), além do caso já
existente. **Deliberadamente não inclui `Conferindo`** — reatribuir um ato que já está sendo
conferido interromperia trabalho em andamento; isso fica de fora até (e se) virar um pedido
explícito. Enum `ProtocoloNaoEstaEmExcecao` renomeado pra `ProtocoloNaoElegivel` (motivo do 409
atualizado de acordo).

**Sem validação de alçada** — decisão consciente, confirmada com o dono: a distribuidora pode
escolher qualquer conferente cadastrado, mesmo sem alçada pra aquele tipo/etapa, igual já
acontecia pra resolver exceção. É decisão humana deliberada; RNF-02 só exige que fique
auditável (`AtribuirA` já grava o carimbo de quando foi feito), não que passe pelo motor.

`Protocolo.AtribuirA` (Domain) não precisou de mudança nenhuma — já era uma transição
incondicional (sem guarda de status prévio), então funciona igual em cima de um protocolo já
`Atribuido` (só sobrescreve `DonoId`/`AtribuidoEm`, limpa `MotivoExcecao` que já era nulo).
3 testes novos em `AtribuirManualmenteTests.cs` (Pool com sucesso, redireciona de um dono pra
outro sem passar pelo pool, rejeita em Conferindo) — 366 testes automatizados no total (111
Domain + 255 Application).

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade): criado protocolo manual
(nasce no Pool) → atribuído à distribuidora manualmente (204) → reatribuído direto a um segundo
conferente sem devolver ao pool (204, dono trocou) → forçado `Status = Conferindo` via SQL →
nova tentativa de atribuir manualmente devolve 409 com o motivo certo.

Front (botão "Atribuir a…"/"Reatribuir a…" no painel de detalhe do protocolo) implementado na
mesma rodada — ver `dispatch-web/CLAUDE.md`, mesma seção.

## Uma conta com os dois papéis — distribuidora que também confere

Pedido do dono: a esposa dele, Maria Vittoria, é a distribuidora do cartório mas também confere
atos pessoalmente às vezes — hoje `Papel` (Distribuidora/Conferente) é tratado como exclusivo em
todo o sistema, o que a forçaria a ter duas contas separadas (login/logout pra trocar de
"chapéu"). Pedido: permitir que a MESMA conta acumule os dois papéis.

**Decisão de design (a que menos invade o sistema): `Usuario.Papel` continua exatamente como
era — um valor único, sem migration, sem mudança de schema em `usuarios`.** A capacidade de
"também é conferente" é **derivada** de já existir um `Conferente` vinculado àquele `UsuarioId`
— o mesmo dado que o sistema já usa em todo canto (alçada, fila, dashboard restrito) pra saber
"essa pessoa confere". A tabela `conferentes` já tinha tudo que precisava: FK + índice único em
`UsuarioId` (`ConferenteConfiguration.cs`) — nada no schema jamais impediu um `Conferente`
apontar pra um `Usuario` com `Papel = Distribuidora`; só faltava um caminho de escrita que
fizesse isso (`CadastrarConferente` sempre cria um `Usuario` novo, nunca vincula a um
existente). **Zero migration nesta rodada.**

- **`PapeisEfetivos`** (helper interno novo, `Dispatch.Application/CasosDeUso/`, mesmo molde de
  `VerificadorDeAlcada`): `usuario.Papel == Conferente` → `[Conferente]`; senão, busca um
  `Conferente` vinculado ao `UsuarioId` — se existir, `[usuario.Papel, Conferente]`, senão só
  `[usuario.Papel]`.
- **`VincularConferenteAUsuario`** (novo caso de uso) — único jeito de dar a capacidade de
  conferente a uma conta já existente. Busca por **e-mail** (não por lista — não existe `GET
  /usuarios` hoje, e não precisa existir só pra isso: um cartório tem poucas contas de
  distribuidora). Rejeita se já existe um `Conferente` vinculado ao `UsuarioId` (cobre sozinho o
  caso de tentar vincular alguém que já é `Papel.Conferente`, sem checagem de papel à parte —
  `CadastrarConferente` sempre cria o `Conferente` junto). `POST /conferentes/vincular`
  (`RequireRole(Distribuidora)`, mesmo grupo de `POST /conferentes`), corpo `{ email, nivel,
  jornadaHoras }`, 201 (reaproveita `CadastrarConferenteResponse`) / 404 "usuário não
  encontrado" / 409 "esse usuário já é conferente".
- **`IEmissorDeToken.EmitirToken`** ganha `IReadOnlyCollection<Papel> papeis` — `EmissorDeTokenJwt`
  monta uma claim `ClaimTypes.Role` **por papel da lista** (JWT/`ClaimsIdentity` já suportam
  múltiplas claims do mesmo tipo nativamente, sem mudança nenhuma de biblioteca).
  `ResultadoAutenticacao.Autenticado`/`ObterUsuarioAtual.UsuarioAtual` trocam `Papel Papel` por
  `IReadOnlyList<Papel> Papeis` (calculado via `PapeisEfetivos` em `Autenticar` e em `GET
  /auth/me`); `UsuarioResponse` (`AuthEndpoints.cs`) acompanha.
- **Distribuidora sempre tem prioridade na visão restrita** (confirmado com o dono: ter o papel
  Distribuidora dá acesso à visão completa de gestão sempre — ser também Conferente só soma
  capacidade, nunca reduz o que ela já via como gestora). As duas checagens que existiam (`if
  (usuario.IsInRole(Conferente))` — `DashboardEndpoints.cs` visão restrita, `ProtocoloEndpoints.cs`
  `PUT /observacao` restrição por dono) passam a ser `if (usuario.IsInRole(Conferente) &&
  !usuario.IsInRole(Distribuidora))`. `RequireRole`/`IsInRole` já leem qualquer claim de role
  presente (nativo do ASP.NET Core) — `RequireRole(Distribuidora)` continua batendo,
  `RequireRole(Conferente)` também, porque a pessoa carrega as duas claims — nenhuma mudança de
  policy em grupo de endpoint algum.

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade): `POST
/conferentes/vincular` numa conta Distribuidora de teste → login de novo → `papeis:
["Distribuidora", "Conferente"]` no JWT/`/auth/me` → `GET /minha-fila` (rota Conferente-only)
responde 200 pro mesmo token → `GET /dashboard` continua mostrando a visão de gestão completa
(`porTipoAto` populado, `mediaDaCasa` nulo, `desempenho` com várias linhas — não a restrita de
um só conferente). 6 testes novos (`AutenticarTests`/`ObterUsuarioAtualTests`: papéis com/sem
Conferente vinculado; `VincularConferenteAUsuarioTests`: sucesso, usuário não encontrado, já é
conferente) — 372 testes automatizados no total (111 Domain + 261 Application).

Front (`dispatch-web`) implementado na mesma rodada — nav mesclada pra quem tem os dois papéis,
sidebar mostrando os dois, e um botão novo em Conferentes pra vincular uma conta existente — ver
`dispatch-web/CLAUDE.md`, mesma seção.

## Primeira paginação de verdade do sistema — GET /tipos-ato/com-uso

O dono, olhando a lista de Tipos de ato em produção, pediu duas coisas: tirar o seletor de
grupo por linha (poluía a tela — ver `dispatch-web/CLAUDE.md`, decisão de manter o conceito de
grupo intacto no resto do sistema, só remover esse seletor específico) e paginação de verdade
pra essa lista. Primeira vez que um endpoint deste sistema pagina — todo o resto continua com
"busca + rolagem contida" no front (mitigação client-side, decisão já registrada na auditoria de
listagens anterior).

- **`Paginado<T>`** (novo, `Dispatch.Application/CasosDeUso/Paginado.cs`) — record genérico
  mínimo (`Itens`, `Total`), sem cursor nem metadata que nenhum consumidor precisa ainda.
- **`ListarTiposAtoComUso.ExecutarAsync`** ganha `string? busca, int pagina = 1, int
tamanhoPagina = 20`. Busca filtra por nome (case-insensitive, `Contains`) **antes** de paginar
  — senão "página 2" nunca bateria com o que a busca do usuário esperava ver. `pagina`/
  `tamanhoPagina` clampados (`Math.Max(1, ...)`, `Math.Clamp(1, 100)`) em vez de rejeitar com
  400 — parâmetro de leitura fora do intervalo esperado é mais barato de tolerar (corrige
  sozinho pro valor mais próximo válido) do que de validar com erro, diferente de `PUT /config`
  (edição deliberada, onde um valor fora do range é bug de digitação que vale a pena avisar).
- **`GET /tipos-ato/com-uso`** ganha os 3 query params (`busca`, `pagina`, `tamanhoPagina`, todos
  opcionais com default) — resposta muda de array solto pra `PaginaDeTipoAtoComUsoResponse
{ Itens, Total }`. Primeira mudança de shape de resposta (array → objeto paginado) no
  sistema — qualquer paginação futura deveria seguir o mesmo formato, não inventar um novo.

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade): `?pagina=2&tamanhoPagina=5`
devolvendo os 5 itens seguintes com `total: 24`; `?busca=venda` devolvendo `total: 2` com os dois
nomes certos ("Escritura de Compra e Venda", "Venda e Compra"). 2 testes novos
(`Busca_FiltraPorNomeAntesDePaginar`, `Pagina2_DevolveOsItensSeguintesEOTotalReal`) — 374 testes
automatizados no total (111 Domain + 263 Application).

Front (`dispatch-web`) na mesma rodada — busca com debounce, componente de paginação do
shadcn, e uma regressão real achada rodando a suíte e2e inteira (specs que dependiam do rótulo
antigo de nav "Minha fila" pra uma conta que virou combo) — ver `dispatch-web/CLAUDE.md`, mesma
seção.

## Clone de produção pra dev — achado real: a suíte e2e dependia de dado que só "por acaso" existia

Pedido do dono: clonar a base de produção (Neon) pro Postgres local, pra analisar visualmente o
tanto de coisa acumulada na Central de Regras (candidata a revisão de design). Feito via
`pg_dump`/`pg_restore` num container `postgres:18` avulso (mesmo mismatch de versão já
documentado no Motor de alçada v2 — cliente local é 17.x, Neon roda 18.x), contra
`host.docker.internal` pra alcançar o Postgres local a partir do container. Nomes/e-mails reais
de funcionários foram anonimizados no clone local antes de qualquer análise (produção em si
nunca foi tocada — só leitura via `pg_dump`).

**Isso expôs um problema real, não hipotético**: a suíte e2e do `dispatch-web` inteira parou de
funcionar, porque quase todo spec loga com contas seed fixas (`distribuidora@cartorio.com`,
`conferente-rf27@cartorio.com`, `conferente-visual@cartorio.com`) que só existiam porque
alguém as criou à mão numa sessão anterior — o clone de produção não tem essas contas (tem
gente de verdade, com e-mail de verdade). O dono comentou: "um bom teste não depende de dado
local, a não ser que o dado seja criado pelo teste e depois apagado". A parte de "criar e
apagar dado próprio" já valia pra maioria dos specs (protocolo, conferente extra, equipe — cada
um cria e limpa via API); o que faltava era exatamente a identidade de login, que ninguém
automatizava.

- **`SemearContasE2E`** (novo caso de uso) — garante as 3 contas fixas com senha e estado
  conhecidos (`Senha123!`), idempotente: cria quem não existe, **reseta** (senha, nome, e-mail,
  bloqueio de login) quem já existe — não bastava só resetar senha, o clone de produção tinha
  essas contas com nome de gente de verdade, e um teste que espera "Distribuidora Teste" na
  tela quebraria mesmo com o login funcionando. Conferente também garante o `Conferente`
  vinculado (cria se faltar) e marca presença na escala (`MarcarPresenca(true)`) — não pode
  ficar refém do que um spec anterior fez com a mesma conta (ex.: marcou ausente).
- **`POST /dev/seed-e2e`** (`DevSeedEndpoints.cs`) — só mapeado quando
  `app.Environment.IsDevelopment()` (gate no próprio `Program.cs`, não no endpoint em si) —
  nunca existe em produção, então "senha previsível" não é risco de segurança lá. Anônimo (não
  precisa de token — é chamado antes de qualquer login existir).
- Front (`dispatch-web`) chama esse endpoint uma vez, antes da suíte inteira rodar
  (`playwright.config.ts` → `globalSetup`) — ver `dispatch-web/CLAUDE.md`, mesma seção.

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade, contra o clone
anonimizado): `POST /dev/seed-e2e` cria as 3 contas na primeira chamada (banco não tinha
nenhuma com esses e-mails), reseta senha/nome/bloqueio na segunda chamada sem duplicar (mesmo
Id) quando já existem. 2 testes novos (`SemearContasE2ETests`: banco vazio cria as 3; contas já
existentes — inclusive bloqueada e com nome/senha antigos — resetam sem duplicar) — 375 testes
automatizados no total (111 Domain + 264 Application). **Achado corrigindo, não previsto no
design original**: o primeiro corte só resetava senha/bloqueio, não nome — rodando contra o
clone de verdade, um spec que espera "Distribuidora Teste" na tela quebrou porque a conta já
existia com o nome real de quem a possui (renomeado só o e-mail antes, não o nome). Corrigido
chamando `Usuario.AtualizarPerfil(nome, email)` também no caminho de "já existe".

## Cadastro manual de escrevente

Pedido do dono, junto com uma pergunta maior sobre um futuro papel "Subscritor" (ver
`dispatch-web/CLAUDE.md`, mesma seção, pro porquê de não mexer nisso agora — não existe em
lugar nenhum do documento de requisitos, só como comentário-âncora no front antecipando um
papel futuro). O cadastro de escrevente em si é independente disso e de baixo risco.

Até aqui, `Escrevente` só nascia como efeito colateral de importar um lote (RF-09) ou de criar/
editar um protocolo manual com nome novo (`ResolvedorDeEscreventePorNome`, que resolve
silenciosamente pra um já existente pelo nome). **`CriarEscrevente`** (novo caso de uso) é o
primeiro caminho deliberado — igual `CriarTipoAto`, nome duplicado (case-insensitive, depois de
normalizado por `NormalizadorDeTexto.ParaNomeProprio`) é 409, não reaproveitamento silencioso;
equipe é opcional na criação, validada contra `IEquipeRepository` se informada (404 se não
existir). `POST /escreventes` (`EquipeEndpoints.cs`, mesmo grupo dos outros endpoints de
escrevente, `RequireRole(Distribuidora)`).

Testado ponta a ponta contra o Postgres local (`dotnet run` de verdade): criar sem equipe
(201), criar duplicado com caixa diferente (409, motivo certo), criar com equipe inexistente
(404 "equipe não encontrada"). 4 testes novos (`CriarEscreventeTests`) — 379 testes
automatizados no total (111 Domain + 268 Application).

Front (`dispatch-web`) na mesma rodada — botão "Novo escrevente" na aba Prazos por equipe,
mesmo padrão visual de "Novo tipo de ato" — ver `dispatch-web/CLAUDE.md`, mesma seção.

## Bug real: KPIs do Dashboard vazavam o total da operação pra visão restrita do conferente

Relatado pelo dono usando o app de verdade: "na visão do conferente ele deveria ver só os
números dele, não o total geral". `ObterDashboard.ExecutarAsync` calculava `kpis` (os 4 números
do topo — Atos conferidos/Dentro do prazo/Aprovados/Tempo médio) **antes** de qualquer branch de
restrição, sempre sobre `concluidosNoPeriodo` inteiro (todos os conferentes) — a restrição por
`conferenteRestritoId` (RF-45) só filtrava `Desempenho`/`MediaDaCasa`/`PorTipoAto`/
`CumprimentoPrazoEquipe`, nunca `Kpis`. Resultado: um conferente via, por exemplo, "3 atos
conferidos" no topo (o total de todo mundo) enquanto a própria linha de desempenho logo abaixo
mostrava volume 1 ou 2 — os dois pareciam dados desencontrados na mesma tela, quando na verdade
o de baixo estava certo e o de cima errado.

**Fix, uma linha**: `kpis` passa a ser calculado sobre `porDono.GetValueOrDefault(conferenteRestritoId.Value, [])`
quando a visão é restrita — reaproveita o mesmo agrupamento por dono que `Desempenho` já usava
(`porDono`, calculado antes disso no método), sem duplicar lógica nem tocar `CalcularKpis` em si
(já era agnóstica de "quem", só operava sobre a coleção que recebesse).

**Achado que evitou o mesmo bug reaparecer**: o teste já existente
(`VisaoRestrita_SoMostraOProprioDesempenhoESemFaixa`) nunca checava `resultado.Kpis` — só
`Desempenho`/`MediaDaCasa`/`PorTipoAto`. Isso é exatamente o tipo de buraco de cobertura que deixa
um bug deste tipo passar despercebido por testes automatizados: a lista de asserções parecia
completa (nome certo, sem faixa, sem nome na média da casa, sem tipo de ato) mas nunca perguntou
"o KPI do topo é meu ou é de todo mundo?". Ganhou a asserção que faltava, mais um teste dedicado
novo (`VisaoRestrita_KpisRefletemSoOProprioConferente_NaoOTotalDaOperacao`, com Ana aprovada e
Bruno reprovado 3x — se o total vazasse, Ana veria 4 atos/25% aprovação em vez dos próprios
1 ato/100%) — 381 testes automatizados no total (111 Domain + 270 Application).

Testado ponta a ponta contra o clone de produção anonimizado no Postgres local: visão de gestão
mostrando `atosConferidos: 3` (2 de um conferente + 1 da distribuidora combo); o mesmo conferente
logado, visão restrita, mostrando `atosConferidos: 2` — batendo com o volume da própria linha de
desempenho, não mais o total. Sem mudança nenhuma no front — ele só exibe `dashboard.kpis` tal
como a API manda (confirmado lendo `VisaoConferente.tsx`/`VisaoGestao.tsx`, os dois consomem o
mesmo campo sem filtragem client-side).

## Bug real: Duracao de um protocolo reaberto perdia o tempo do ciclo anterior

Relatado pelo dono com um protocolo real de produção (nº 264137): reprovado errado, reaberto,
conferido de novo — o card mostrava só a duração do **segundo** ciclo (uns 5 min), não o total
de tempo que o ato ficou de fato em conferência (primeiro ciclo + reabertura).

**Causa**: `Protocolo.Duracao` sempre foi `ConcluidoEm - IniciadoEm`, um intervalo só.
`ReabrirConferencia` (RF-24c) reatribui `IniciadoEm`/`ConcluidoEm` na hora — não existia lugar
nenhum, no domínio ou na persistência, guardando o tempo do ciclo que acabou de ser sobrescrito.
Não era só "não mostrado na tela": o dado já não existia mais depois da reabertura.

**Fix**: `Protocolo` ganha `TempoAcumuladoAnterior` (`TimeSpan`, `private set`, default
`TimeSpan.Zero`) — `ReabrirConferencia` soma o ciclo que está terminando (`ConcluidoEm -
IniciadoEm`) nesse acumulador **antes** de zerar os dois campos; `Duracao` passa a ser
`TempoAcumuladoAnterior + (ConcluidoEm - IniciadoEm)`. Cobre qualquer número de reaberturas
(cada uma soma o ciclo anterior de novo). Migration `AdicionaTempoAcumuladoAnteriorEmProtocolos`
— 1 coluna nova (`interval`, `NOT NULL DEFAULT '00:00:00'`), sem backfill possível (protocolos
já reabertos antes deste fix já perderam o dado do primeiro ciclo pra sempre — não tem como
recuperar retroativamente; o fix vale só daqui pra frente).

**Achado no caminho, de cobertura de teste**: o teste já existente de `ReabrirConferencia`
(`ReabrirConferencia_VoltaPraConferindoComCronometroDoZero`) só checava o estado imediatamente
depois de reabrir (`Duracao` nulo, correto) — nunca chegou a concluir de novo pra ver se o
total batia. 2 testes novos (`ReabrirConferenciaEConcluirDeNovo_DuracaoSomaOsDoisCiclos`,
`ReabrirConferenciaDuasVezes_AcumulaOsTresCiclos` — três ciclos seguidos, prova que acumula, não
só substitui) — 383 testes automatizados no total (113 Domain + 270 Application).

**Migration aplicada em produção (Neon) na hora, não só local** — sem isso, o deploy do código
novo quebraria toda leitura de `Protocolo` em produção (coluna nova referenciada pelo EF Core,
inexistente no banco real). Confirmado com o dono antes de rodar (`dotnet ef database update
--connection "..."` contra o Neon), migration aditiva/segura (`ADD COLUMN ... DEFAULT`, sem
tocar dado existente).

Testado ponta a ponta contra o Postgres local, cenário real completo via API (não só teste
unitário): criou protocolo, atribuiu a um conferente, reprovou (1º ciclo ~14.54s), reabriu via
distribuidora, aprovou de novo (2º ciclo ~14.24s) — `GET /protocolos/distribuicao` confirmou
`duracao: 00:00:28.7800620`, batendo exatamente com a soma dos dois ciclos medidos (não uma
aproximação — os dois valores somados batem byte a byte com o total devolvido).

## Reabrir conferência devolve pra Atribuído, não liga o cronômetro na hora

Achado em uso real (produção, protocolo 263605): "Reabrir conferência" fazia
`ReabrirConferencia` ir direto pra `Status = Conferindo` (cronômetro ligado na mesma hora,
`IniciadoEm = agora`) — o dono relatou que o ato "reabriu como em conferência e não na fila da
pessoal", esperando que ele voltasse pra "Atribuídas a você" (esperando a pessoa clicar
"Iniciar conferência" de novo), não já contando tempo sem ninguém ter feito nada.

Segundo problema, levantado junto pelo dono (cenário real): às vezes o conferente pede
reabertura e a distribuidora só aprova o pedido bem depois — fora do horário de trabalho dele,
por exemplo. Com o comportamento antigo, esse intervalo de espera (aprovação → o conferente
realmente sentar e retomar) entrava direto no cronômetro, contando como se fosse tempo de
conferência.

**Fix**: `Protocolo.ReabrirConferencia` (Domain) passou de `Status = Conferindo` +
`IniciadoEm = agora` pra `Status = Atribuido` + `IniciadoEm = null` — mesmo dono, mas o
cronômetro só liga quando `IniciarConferencia` é chamado de novo (o mesmo método que já existia
pra primeira vez), igual uma atribuição nova. Isso resolve os dois problemas de uma vez: o ato
aparece nas atribuídas da pessoa (não em conferência), e o intervalo entre a aprovação da
distribuidora e o reinício de verdade não conta pra `Duracao` (só o que acontece entre
`IniciarConferencia` e a conclusão seguinte entra no ciclo novo, somado a
`TempoAcumuladoAnterior`).

Usado pelos dois caminhos que já chamavam `ReabrirConferencia` — ação direta no painel de
detalhe (`ReabrirConferencia.cs`, caso de uso) e aprovação de pedido de reabertura
(`DecidirPedidoReabertura.cs`) — nenhuma mudança adicional precisou nos dois, só o
comportamento do método de Domain por baixo. Nenhuma mudança de contrato de API (front já lê
`Status` genérico pra decidir em qual coluna mostrar o card — Atribuído vs Conferindo —, sem
nenhuma suposição hardcoded de "reabertura sempre vira Conferindo").

**Testes atualizados** (não são testes novos, são os mesmos ajustados pro novo comportamento):
`ProtocoloTests.ReabrirConferencia_VoltaPraAtribuidoSemLigarOCronometro` (renomeado, antes
"...ComCronometroDoZero" — agora `IniciadoEm` fica nulo, não "zerado pra agora"); os dois testes
de acumulação de ciclos (`ReabrirConferenciaEConcluirDeNovo_DuracaoSomaOsDoisCiclos`,
`ReabrirConferenciaDuasVezes_AcumulaOsTresCiclos`) ganharam uma chamada explícita de
`IniciarConferencia` entre cada `ReabrirConferencia` e o `Aprovar`/`Reprovar` seguinte — sem
isso os testes não refletiam mais o fluxo real (antes o teste presumia que reabrir já deixava
`IniciadoEm` setado). O primeiro desses dois também ganhou um intervalo deliberado entre
"reabriu" e "iniciou de novo" (1h de espera não contando), provando o segundo cenário do dono.
`ReabrirConferenciaTests`/`DecidirPedidoReaberturaTests` (Application) tiveram as asserções de
`Status`/`IniciadoEm` trocadas de `Conferindo`/`Agora` pra `Atribuido`/`null`.

Verificado ponta a ponta contra o Postgres local (não só teste unitário): criado protocolo,
atribuído, iniciado, aprovado; reaberto pela distribuidora — confirmado via `psql` que ficou
`Atribuido`/`IniciadoEm` nulo; confirmado via `GET /minha-fila` que aparece em `atribuidos`, não
em `emConferencia`; iniciado de novo e concluído — `Duracao` final soma os dois ciclos
corretamente. 384 testes automatizados no total (114 Domain + 270 Application).

## `TempoAcumuladoAnterior` vira `CiclosAnteriores` — Dashboard não pode mais herdar tempo de outra pessoa

Continuação direta da seção acima, mesma conversa com o dono. Ele testou o fix e reportou dois
pontos novos, os dois genuínos:

1. **"Às vezes um conferente pede reabertura e a distribuidora aprova fora do horário de
   trabalho dele — esse tempo não pode contar."** Já resolvido de graça pelo fix anterior (o
   cronômetro só liga quando `IniciarConferencia` roda de novo, não na aprovação) — só faltava
   confirmar, o que o teste `ReabrirConferenciaEConcluirDeNovo_DuracaoSomaOsDoisCiclos` (seção
   acima) já prova com um intervalo de 1h entre reabrir e iniciar.
2. **A pergunta de verdade, que abriu escopo maior**: "quando o ato reabre, ele sempre volta pro
   mesmo conferente, exceto se a pessoa estiver fora da escala" — e esse tempo de conferência
   "é usado pra medir carga/produtividade" numa conta de bonificação **fora do sistema** (RF-46
   só define o score = 40% volume + 30% prazo + 20% qualidade + 10% complexidade, sem nenhuma
   parcela de tempo — confirmado relendo o documento de requisitos antes de mexer; o uso real do
   tempo é externo ao app). Confirmado com o dono: "não podemos perder nada desses dados" — dado
   o peso financeiro disso, valia a pena fazer o modelo certo, não um atalho.

**Causa raiz do gap**: `TempoAcumuladoAnterior` (`TimeSpan`, seção acima) somava certo pra
`Duracao`, mas era cego — não sabia **de quem** era cada ciclo. Se um protocolo reabre e é
reatribuído pra outra pessoa (só acontece quando o dono original saiu da escala, ver abaixo), o
Dashboard (`ObterDashboard.CalcularDesempenho`) lia `Protocolo.Duracao` inteiro e jogava tudo na
conta de quem quer que fosse o dono **atual** — a pessoa nova herdava o tempo que a pessoa
antiga já tinha gastado, e a pessoa antiga não aparecia em lugar nenhum.

### `CicloConferencia` — um registro por ciclo, não um acumulador cego

- **`CicloConferencia.cs`** (novo, `Dispatch.Domain`) — `ConferenteId`/`IniciadoEm`/`ConcluidoEm`
  (+`Duracao` computada). Um ciclo de conferência já encerrado.
- **`Protocolo.cs`** — `TempoAcumuladoAnterior : TimeSpan` (getter só, sem setter público) virou
  `CiclosAnteriores : IReadOnlyList<CicloConferencia>` (mesmo padrão de encapsulamento, backing
  field privado `_ciclosAnteriores`). `ReabrirConferencia` agora **adiciona um
  `CicloConferencia`** ao fechar o ciclo (com o `DonoId` de agora, antes de zerar
  `IniciadoEm`/`ConcluidoEm`) em vez de somar um `TimeSpan`. `Duracao` continua sendo a soma de
  tudo (ciclos anteriores + o ciclo atual, se concluído) — o valor exibido no card não muda nada.
- **`ProtocoloConfiguration.cs`** — primeira coleção-filha do projeto (`OwnsMany`), tabela nova
  `ciclos_conferencia` (`protocolo_id` FK com `Cascade`, `conferente_id`, `iniciado_em`,
  `concluido_em`, chave própria via shadow property `Id` — `CicloConferencia` não tem
  identidade fora do protocolo, é auditoria histórica, não uma entidade buscável sozinha).
  **Gotcha novo de EF Core, documentado aqui pela primeira vez**: coleção owned (`OwnsMany`),
  diferente de uma propriedade escalar, não precisa de `UsePropertyAccessMode` explícito — o EF
  acha o backing field `_ciclosAnteriores` sozinho pela convenção de nome
  (`_<propriedade em camelCase>`), a mesma convenção que já resolve `IReadOnlyList<T>` sem
  setter público em outros cantos do projeto. Índice em `conferente_id` pensando no consumidor
  real (`ObterDashboard` somando tempo por pessoa).
- **Migration `AdicionaCiclosDeConferencia`** — cria a tabela, dropa
  `tempo_acumulado_anterior`. **Backfill obrigatório antes de dropar** (achado pensando em "não
  podemos perder nada desses dados"): protocolos que já tinham `TempoAcumuladoAnterior > 0`
  antes desta migration só tinham a soma cega, sem saber de quem era — não dá pra reconstruir o
  histórico exato (não sabemos os horários reais de cada ciclo passado), mas em vez de deixar o
  dado sumir, um `INSERT` sintetiza um único ciclo por protocolo, atribuído ao dono **atual**
  (`COALESCE(iniciado_em, reaberto_em) - tempo_acumulado_anterior` até
  `COALESCE(iniciado_em, reaberto_em)`) — o cenário confirmado como esmagadoramente comum é o
  mesmo conferente refazer a conferência, então essa aproximação é honesta na prática. Conferido
  contra produção antes de rodar: só **1 protocolo** (o próprio 263605) tinha tempo acumulado,
  com dono e âncora de horário presentes — nenhum dado ficou de fora. `Down()` é simétrico
  (soma os ciclos de volta pro `TimeSpan` antes de derrubar a tabela).

### `ObterDashboard.cs` — tempo por conferente vem dos ciclos, não do protocolo inteiro

- **`ConstruirTemposPorConferente`** (novo, privado) — achata cada protocolo concluído em
  `(conferenteId, duração)` por ciclo: um por `CicloConferencia` já encerrado, mais o ciclo
  final/atual (o que ainda está direto em `Protocolo.IniciadoEm`/`ConcluidoEm`/`DonoId`, sempre
  do dono de agora). Devolve `Dictionary<Guid, List<TimeSpan>>`.
- **`CalcularKpis`** ganhou um parâmetro `temposProprios` — `null` pra visão agregada (o "tempo
  médio da operação" e o "por tipo de ato" continuam somando o protocolo inteiro, não interessa
  quantas pessoas passaram por ele — é "quanto tempo esse ato leva", não "quanto tempo essa
  pessoa trabalhou", então **não mudou**); uma lista (mesmo vazia) pra visão restrita de um
  conferente — RF-45 ("os números dele") agora reflete só os ciclos que ele mesmo fez.
- **`CalcularDesempenho`** — `TempoMedio` (a tabela de desempenho, e a linha "meus números")
  vem de `temposProprios` (os ciclos da pessoa), não mais de `protocolosDoConferente.Select(p =>
  p.Duracao)`. **Volume/Score/PercentualNoPrazo/PercentualAprovado/ComplexidadeMedia não
  mudaram** — continuam do dono atual do protocolo inteiro, escopo que o dono não pediu pra
  mexer.
- **Achado corrigindo, não pedido explicitamente**: a lista de desempenho (`todosOsDesempenhos`)
  antes só iterava `porDono` (quem é dono de algum protocolo concluído no período) — alguém que
  fez um ciclo e teve o protocolo reatribuído antes de concluir de vez (o cenário raro: saiu da
  escala no meio) nunca apareceria em lugar nenhum, mesmo tendo de fato trabalhado. Corrigido
  unindo `porDono.Keys` com `temposPorConferente.Keys` — essa pessoa aparece com `Volume: 0`
  (não é dona de nada agora) mas `TempoMedio` refletindo o que ela realmente fez.

### RF-27 aplicado à reabertura — "fora da escala" não fica preso em Atribuído

Fechando a pergunta "sempre volta pro mesmo, exceto se a pessoa estiver fora da escala":
`ReabrirConferencia.cs` e `DecidirPedidoReabertura.cs` ganharam `IConferenteRepository` — depois
de `protocolo.ReabrirConferencia(agora)`, checam se o dono (o mesmo de antes, `ReabrirConferencia`
não muda `DonoId`) ainda está `NaEscala`; se não, chamam `protocolo.EnviarParaPool()` (mesma
transição que `MarcarPresenca(ausente)` já usa pro RF-27) em vez de deixar em Atribuído pra
alguém que ninguém está mais olhando. `donoAnterior is null` (o `Conferente` sumiu de verdade)
trata como não-elegível também, mesmo raciocínio conservador.

Testes novos: `ReabrirConferenciaTests.ProtocoloConcluido_VaiParaOPool_QuandoODonoSaiuDaEscala` e
`DecidirPedidoReaberturaTests.Aprovar_VaiParaOPool_QuandoOSolicitanteJaSaiuDaEscala` — os testes
existentes que verificavam o caminho feliz (`ProtocoloConcluido_Reabre`,
`Aprovar_ReabreOProtocoloComMesmoDono`) foram renomeados e passaram a criar o `Conferente` com
`naEscala: true` explícito (antes não precisavam de `Conferente` nenhum, só de um `DonoId` cru).

`ObterDashboardTests.ProtocoloReabertoEReatribuido_TempoMedioNaoHerdaDoConferenteAnterior` —
prova o cenário completo: Ana faz 20 min, reprova; reabre e é reatribuído pro Bruno (ela saiu da
escala); Bruno faz 5 min e aprova. Bruno fica com `Volume: 1`/`TempoMedio: 5min` (dono atual,
comportamento de sempre); Ana fica com `Volume: 0`/`TempoMedio: 20min` (não é dona de nada, mas
o tempo dela não desaparece); `Protocolo.Duracao` continua somando os 25 min certinho.

Verificado ponta a ponta contra o Postgres local (não só teste unitário, dado o peso financeiro
do dado): cenário completo via API real — protocolo criado, atribuído à Conferente RF27,
reprovado (1º ciclo), reaberto e reatribuído à Conferente Visual, aprovado (2º ciclo) —
`GET /dashboard` confirmou RF27 com `volume: 0`/`tempoMedio` batendo exatamente com a duração
do ciclo dela sozinho (não a soma dos dois), e Visual com `volume: 1`/`tempoMedio` batendo só
com o ciclo dela. Testado também o caminho de "fora da escala": conferente marcada ausente via
`POST /conferentes/{id}/presenca`, protocolo reaberto — confirmado via `psql` que foi pro Pool
(`dono_id` nulo), não ficou preso em Atribuído. 389 testes automatizados no total (116 Domain +
273 Application).

## "Atribuídas a você" também ordenada por vencimento

Pedido do dono: o pool disponível já vinha ordenado por vencimento (achado numa auditoria
anterior, ver "Pool ordenado + '+N protocolos' em Minha fila" no `dispatch-web/CLAUDE.md`), mas
`ObterAtribuidosAAsync` (repositório) não tinha `ORDER BY` nenhum — a ordem de "Atribuídas a
você" ficava por conta do que o Postgres decidisse devolver, não garantida.

`ObterMinhaFila.ExecutarAsync` — `atribuidos` ganhou o mesmo `.OrderBy(p => p.VencimentoEm ??
DateTimeOffset.MaxValue)` que `poolDisponivel` já usava (quem tá vencendo primeiro no topo, sem
vencimento por último). Único caso de uso que lê `ObterAtribuidosAAsync` pra exibição — os
outros dois consumidores (`MarcarPresenca`/`RemoverConferente`) só usam pra devolver protocolos
ao pool em lote, onde ordem não importa. Corrige as duas telas que reaproveitam
`ObterMinhaFila` (Minha fila do próprio conferente e Fila de conferentes da distribuidora, RF-19)
de uma vez só, mesmo caso de uso por trás das duas. `EmConferencia` não pedido, ficou de fora
(RF-21 já limita a 1 simultâneo por padrão, ordem quase nunca importa ali).

Teste novo `ObterMinhaFilaTests.Atribuidos_OrdenaPorVencimentoAscendente`, espelhando
`PoolDisponivel_OrdenaPorVencimentoAscendente` que já existia. Verificado também contra o
Postgres local via API real: 2 protocolos criados com `andamentoEm` deliberadamente fora de
ordem (um vencendo depois criado primeiro) — `GET /minha-fila` devolveu na ordem certa de
vencimento, não na ordem de criação. Sem migration (não mexe em schema). 390 testes
automatizados no total (116 Domain + 274 Application).

## Pausar conferência — "a pessoa sai pra almoçar, por exemplo"

Pedido do dono, não é RF numerado nem está no protótipo/requisito (confirmado buscando "pausa"
no documento — não existe). Um protocolo em conferência pode ser pausado sem devolver o ato pra
fila: continua ocupando o limite de simultâneos (RF-21, confirmado com o dono — decisão
deliberada, não padrão implícito), aparece em "Em conferência" com "Pausado" no lugar do
cronômetro e um botão "Retomar". O tempo pausado não pode contar (mesma disciplina já
estabelecida pra reabertura, ver seção acima) — reaproveita exatamente o mesmo mecanismo de
"fechar o ciclo em andamento" que `ReabrirConferencia` já usa.

- **`Protocolo.cs`** — `PausadoEm : DateTimeOffset?` (novo). `Pausar(agora)`: fecha o ciclo
  corrente em `CiclosAnteriores` (mesmo `CicloConferencia`, com o dono de agora) e zera
  `IniciadoEm`, **sem** mudar `Status` (continua `Conferindo` — é isso que faz o limite de
  simultâneos continuar contando este ato, `ObterEmConferenciaPorConferenteAsync` filtra só por
  `Status`). `Retomar(agora)`: abre um ciclo novo (`IniciadoEm = agora`), limpa `PausadoEm`.
  Nenhuma mudança em `Duracao` (já soma `CiclosAnteriores` + ciclo atual, ver seção anterior) —
  o intervalo pausado nunca entra em nenhum dos dois lados.
- **`PausarConferencia.cs`/`RetomarConferencia.cs`** (novos, `Dispatch.Application`) — mesmo
  molde de `IniciarConferencia`: valida dono + `Status == Conferindo`, mais a checagem
  específica (`PausadoEm` nulo pra pausar, não-nulo pra retomar).
- **`ConcluirConferencia.cs`** ganhou uma guarda nova: `EstaPausado` — concluir com `IniciadoEm`
  nulo (o estado durante a pausa) gravaria `ConcluidoEm` sem ciclo aberto correspondente,
  `Duracao` (que exige os dois) voltaria nulo, escondendo o que já está em `CiclosAnteriores`.
  Precisa retomar antes de aprovar/reprovar — reforçado também no front (some o botão
  Aprovar/Não aprovar enquanto pausado, só mostra "Retomar").
- **Endpoints novos**: `POST /minha-fila/{id}/pausar`, `POST /minha-fila/{id}/retomar` (mesmo
  grupo/padrão de `/iniciar`/`/concluir`). `ProtocoloResumo` (DTO compartilhado por Minha
  fila/Distribuição) ganhou `PausadoEm`.
- **Migration `AdicionaPausaEmProtocolos`** — só `AddColumn` nullable, aditiva/segura.

Testes novos: `ProtocoloTests` (4 — pausa zera `IniciadoEm` sem mudar `Status`, guarda o ciclo
certo, retomar abre ciclo novo, e o cenário completo pausa→retoma→conclui provando que a
`Duracao` soma os dois pedaços sem a hora da pausa), `PausarConferenciaTests`/
`RetomarConferenciaTests` (novos, 5+4 casos), `IniciarConferenciaTests` ganhou
`ProtocoloPausadoAindaContaNoLimiteDeSimultaneos` (prova que pausado continua bloqueando iniciar
outro), `ConcluirConferenciaTests` ganhou `ProtocoloPausado_RetornaEstaPausado`.

Verificado ponta a ponta contra o Postgres local (não só teste unitário): criado protocolo,
atribuído, iniciado, pausado — confirmado que concluir dá 409 ("está pausado — retome antes de
concluir") e que `GET /minha-fila` mostra em `emConferencia` com `iniciadoEm: null`/`pausadoEm`
preenchido; retomado e concluído depois — `duracao` final bateu exatamente com a soma dos dois
pedaços medidos (1.06s + 2.05s = 3.11s), excluindo o intervalo da pausa (~13s). 405 testes
automatizados no total (120 Domain + 285 Application).

### Visibilidade das pausas — "como garantir que ninguém abusa da pausa pra melhorar o tempo dela?"

Pergunta do dono, feita na mesma conversa, antes de aplicar a migration em produção — genuína e
importante, já que esse tempo alimenta uma conta real de bonificação (ver seção "CiclosAnteriores
vira Dashboard", acima). Hoje pausar não tem NENHUM controle nem registro: a pessoa pode pausar
quantas vezes quiser, por quanto tempo quiser, sem ninguém saber depois. Decisão do dono:
**visibilidade, não bloqueio** — time pequeno, confiança resolve, mas o dado não pode ficar
invisível.

- **`PausaConferencia.cs`** (novo, `Dispatch.Domain`) — `PausadoEm`/`RetomadoEm` (+`Duracao`
  computada), uma pausa já encerrada. Mesmo raciocínio de `CicloConferencia`, mas mais simples:
  uma pausa é sempre do dono atual (nunca muda de pessoa como um ciclo reaberto pode mudar), não
  precisa de `ConferenteId`.
- **`Protocolo.cs`** — `Pausas : IReadOnlyList<PausaConferencia>` (nova coleção-filha, mesmo
  padrão de `CiclosAnteriores`). `Retomar(agora)` agora registra a pausa que está terminando
  (`PausadoEm` → `agora`) antes de limpar `PausadoEm` — antes, esse intervalo simplesmente
  desaparecia, sem rastro nenhum.
- **`ProtocoloConfiguration.cs`** — `OwnsMany(p => p.Pausas, ...)`, tabela `pausas_conferencia`
  (sem FK de conferente, só `protocolo_id`). Migration `AdicionaHistoricoDePausas` (aditiva, só
  `CreateTable`).
- **`DetalheProtocoloResponse`** (`GET /protocolos/{id}/detalhe`) ganha `PausadoEm` (estado atual,
  se estiver pausado agora) e `Pausas: PausaConferenciaResponse[]` (`PausadoEm`/`RetomadoEm`/
  `Duracao` de cada pausa já encerrada) — front decide como resumir.

Testes novos: `ProtocoloTests.Retomar_RegistraAPausaEncerrada` e
`PausarERetomarVariasVezes_AcumulaUmaPausaPorCiclo` (duas pausas seguidas, cada uma com sua
própria duração, não uma soma cega). Verificado ponta a ponta: protocolo pausado e retomado duas
vezes seguidas, `GET /protocolos/{id}/detalhe` devolvendo as 2 pausas certinhas (`pausadoEm`/
`retomadoEm`/`duracao` batendo com os intervalos reais medidos). 407 testes automatizados no
total (122 Domain + 285 Application).

## Reabertura recalcula o vencimento a partir de agora

Achado em uso real (produção): um protocolo reaberto dias depois de concluído continuava com o
vencimento calculado a partir da entrada original — aparecia "vencido há Xd" na hora, mesmo
sendo uma conferência nova pedida agora. Confirmado com o dono com um exemplo concreto antes de
mexer (entrada 10/09, D+1, reaberto 15/09 → vencimento devia virar 16/09, não continuar 11/09).

**Importante, para não confundir com a mudança anterior desta mesma conversa**: isso é sobre
`VencimentoEm` (prazo/semáforo), não sobre `Duracao` (tempo de conferência trabalhado,
`CiclosAnteriores`) — os dois são conceitos independentes. `Duracao` continua somando os ciclos
normalmente (decisão já confirmada e testada, ver seções acima) — só o vencimento é que reseta.

`Protocolo.ReabrirConferencia` ganhou, no fim do método, `if (Prazo is { } prazoAtual) {
DefinirPrazo(prazoAtual, agora); }` — reaproveita o mesmo `DefinirPrazo` já existente (mesmo
`TipoPrazo` que o protocolo já tinha, só troca o momento de referência de `AndamentoEm` pra
`agora`). **`AndamentoEm` não muda** — continua sendo o histórico real de quando o ato entrou no
sistema pela primeira vez (é `{ get; }`, imutável de propósito). Guarda defensiva: se `Prazo`
por algum motivo ainda for nulo (não deveria acontecer — todo protocolo que chega a
Aprovado/Reprovado já passou por `DistribuirProtocolo`, que sempre define um `Prazo`), não
inventa vencimento nenhum, só não recalcula.

Dois testes novos em `ProtocoloTests`: `ReabrirConferencia_RecalculaVencimentoAPartirDeAgora`
(prova que `VencimentoEm` muda e `AndamentoEm` não) e
`ReabrirConferencia_SemPrazoDefinidoAntes_NaoQuebraENaoInventaVencimento` (a guarda defensiva).
Nenhuma mudança em `ReabrirConferencia.cs`/`DecidirPedidoReabertura.cs` (Application) — o
recálculo é parte da própria transição de domínio, os dois caminhos que já chamavam
`ReabrirConferencia` ganham o comportamento de graça.

Verificado ponta a ponta contra o Postgres local: protocolo criado com `andamentoEm` 5 dias
atrás (prazo D1, vencimento já no passado), concluído, reaberto — `AndamentoEm` continuou
idêntico, `VencimentoEm` saiu do dia seguinte à entrada original pro dia seguinte à reabertura
(D+1 a partir de agora). Sem migration (mudança pura de lógica de domínio). 409 testes
automatizados no total (124 Domain + 285 Application).
