---
name: conceitos-dotnet
description: Glossário dos conceitos de .NET/ASP.NET Core/EF Core que apareceram no projeto, explicados para quem vem de outro ecossistema, com onde cada um é usado aqui
metadata:
  type: pattern
  domains: [aprendizado, dotnet, aspnetcore, ef-core]
  status: stable
---

# Conceitos de .NET usados no projeto

> O dono está aprendendo .NET com este projeto (vem de outros ecossistemas — Prisma, JS). Ao
> introduzir um conceito novo, explique o "porquê" antes de aplicar, e acrescente uma linha aqui.

## Quando ler

- Ao explicar uma escolha de .NET para o dono.
- Ao encontrar no código um mecanismo que "parece mágica".

## Solution e projetos

- **Solution (`Dispatch.slnx`) + projetos (`.csproj`)**: cada camada é um projeto; a referência entre
  projetos é o que impõe a regra de dependência — Domain nem enxerga EF Core. Ver `arquitetura.md`.
- **Ferramentas locais** (`.config/dotnet-tools.json`): `dotnet-ef` e `reportgenerator` instalados por
  projeto (como devDependencies), não globais. `dotnet tool restore` instala.
- **Top-level statements**: o `Program.cs` sem `class`/`Main`. O compilador gera uma classe `Program`
  `internal` — por isso o `public partial class Program;` no fim, para o
  `WebApplicationFactory<Program>` dos testes enxergá-la.

## ASP.NET Core

- **Minimal APIs**: rotas declaradas com `app.MapGet/MapPost` e lambdas, sem controllers. Os
  parâmetros da lambda são inferidos: rota, query, corpo JSON, **serviço do DI** ou tipos especiais
  (`ClaimsPrincipal`, `CancellationToken`, `HttpContext`). Se o tipo não está registrado no DI, a
  inferência falha **ao montar o endpoint, em runtime** ("Failure to infer one or more parameters").
- **Injeção de dependência (DI)**: `services.AddScoped<T>()` registra um tipo com tempo de vida de uma
  requisição; o container monta os construtores. Composition root em `ServiceCollectionExtensions.cs`.
  `DbContext` é scoped — um objeto carregado numa requisição não pertence ao `DbContext` da próxima
  (é por isso que cache + edição exige cuidado, ver `ef-core.md`).
- **Configuração em camadas**: `appsettings.json` → `appsettings.{Environment}.json` → variáveis de
  ambiente. `Jwt__ChaveDeAssinatura` (dois underscores) em env var vira `Jwt:ChaveDeAssinatura`.
  Troca de valor por ambiente sem `if` no código.
- **Autenticação/autorização**: `ClaimsPrincipal` é o usuário do request; `ClaimTypes.Role` alimenta
  `RequireRole`/`IsInRole` sem policy customizada. `RequireAuthorization` empilhado combina com E.
  `JwtBearerOptions.Events.OnTokenValidated` é o gancho para validação extra do token.
- **`RequireRole(a, b)` é OU; políticas empilhadas são E.** Dentro de uma chamada, qualquer um dos
  papéis basta; a policy do `MapGroup` + a da rota precisam passar as duas. É assim que uma rota
  "só do Administrador" vive num grupo de Distribuidora sem mexer nas outras (ADR-0039).
- **`FallbackPolicy`** (`AddAuthorization(o => o.FallbackPolicy = ...)`) só vale para endpoint **sem
  nenhuma** política própria — não é um "filtro global" das rotas que já têm `RequireAuthorization`.
- **Pipeline de middleware**: cada `app.Use...` é um elo que roda em ordem, podendo seguir (`next()`)
  ou responder e parar. A ordem é o contrato: `UseAuthentication` preenche `context.User`,
  `UseAuthorization` aplica as policies. Um `app.Use(async (context, next) => ...)` entre os dois vê o
  usuário já identificado e age em **toda** rota antes das policies — é o bloqueio da troca de senha
  obrigatória (ADR-0040).
- **`IMemoryCache`**: cache em memória do processo (`AddMemoryCache()`), usado para a configuração.

## EF Core

- **`DbContext` + change tracker**: o contexto rastreia as entidades que carregou; `SaveChanges`
  grava o que mudou nelas. Objeto que o contexto não carregou (projeção, tradução, cache) não é
  rastreado — mutá-lo não grava nada.
- **`IEntityTypeConfiguration<T>`**: mapeamento fluente por entidade, fora da entidade (a entidade
  de Domain não leva atributo de ORM).
- **`ValueConverter`**: converte um tipo do C# para uma coluna (`Prazo` ↔ texto, enum ↔ string,
  `DateTimeOffset` → UTC).
- **Owned types** (`OwnsOne`/`OwnsMany`): tipo sem identidade própria que vive "dentro" de outro;
  `OwnsMany` cria tabela filha (ciclos, pausas, ajustes). Backing field `_nome` achado por convenção.
- **Migrations**: classes C# geradas (`Up`/`Down`) versionando o schema; `dotnet ef migrations add` e
  `dotnet ef database update`. Equivalem às migrations do Prisma, mas editáveis (backfill, `InsertData`).
- **`IUnitOfWork`** (porta do projeto, não do EF): um `SaveChanges` por caso de uso — equivalente
  explícito ao `prisma.$transaction([...])`.

## C#

- **`record`**: tipo com igualdade por valor e sintaxe curta — usado para DTOs, value objects e
  resultados. **Record posicional** (`record X(int A, int B)`): campo novo vai no fim.
- **Hierarquia fechada** (record abstrato + construtor privado + tipos aninhados): emula sum type /
  union discriminada; `switch` sobre ela no lugar de `if (x is ...)` espalhado.
- **`private set` + métodos**: entidade controla as próprias transições.
- **`Guid?`/`TimeOnly?`**: nullable value types. Cuidado: `Dictionary<Guid, Guid>.GetValueOrDefault`
  devolve `Guid.Empty`, não `null`.
- **`PasswordHasher<TUser>`**: hasher do Identity usável sozinho; o `TUser` é só extensibilidade.

## Testes

- **xUnit**: `[Fact]`/`[Theory]` + `[InlineData]` (parametrizado).
- **`WebApplicationFactory<Program>`** (`Microsoft.AspNetCore.Mvc.Testing`): sobe a API inteira em
  memória e dá um `HttpClient` — HTTP de verdade sem porta aberta.
- **Testcontainers**: sobe um container Docker (Postgres) por execução de suíte. **Respawn**: limpa
  as tabelas entre testes respeitando FK.
- **coverlet + reportgenerator**: coleta e relatório de cobertura (ver `testes.md`).

## Referências

- `arquitetura.md`, `ef-core.md`, `endpoints.md`, `autorizacao.md`, `testes.md`.
