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

Solution (`Dispatch.slnx`) e os 4 projetos + `Dispatch.Domain.Tests` criados e compilando,
sem lógica de negócio ainda. Próximo passo: modelar as entidades do domínio e o motor de
distribuição (seção 4 do documento de requisitos) dentro de `Dispatch.Domain`.
