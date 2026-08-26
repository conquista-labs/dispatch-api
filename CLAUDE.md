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
