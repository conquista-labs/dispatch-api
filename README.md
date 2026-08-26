# Dispatch API

Back-end do Dispatch — sistema de distribuição e conferência de protocolos (atos notariais)
para cartório, com um motor de distribuição determinístico e auditável no lugar de uma
planilha manual.

> Contexto completo do projeto (stack, arquitetura, convenções, decisões) vive em
> [CLAUDE.md](CLAUDE.md). O domínio (regras de negócio) está descrito em
> `../dispatch-prototype/Dispatch - Requisitos.dc.html`.

## Stack

.NET 10 · ASP.NET Core (minimal APIs) · Entity Framework Core · PostgreSQL (Neon) · Fly.io

## Rodando localmente

```bash
docker compose up -d                      # sobe o Postgres local
dotnet build                              # compila a solution inteira
dotnet test                               # roda os testes
dotnet run --project src/Dispatch.Api     # sobe a API
```

## Banco de dados

- **Local**: Postgres via `docker-compose.yml`, credenciais fixas de desenvolvimento (não são
  segredo — só existem no container local). Connection string em
  `src/Dispatch.Api/appsettings.Development.json`.
- **Produção**: Postgres no Neon. A connection string real não fica em nenhum arquivo do
  repositório — é configurada como secret no Fly.io (`fly secrets set`) quando o deploy for
  configurado.

## Docker

```bash
docker build -t dispatch-api .
docker run -p 8080:8080 dispatch-api
```

O Dockerfile faz um build multi-stage: compila com a imagem do SDK e publica na imagem de
runtime do ASP.NET, mais enxuta. Ainda não há `fly.toml` — a configuração de deploy no
Fly.io entra quando o deploy for de fato configurado (via `fly launch`).
