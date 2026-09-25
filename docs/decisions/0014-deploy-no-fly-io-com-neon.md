---
name: adr-0014-deploy-no-fly-io-com-neon
description: Primeiro deploy de produção no Fly.io (free tier, container Docker) com Postgres no Neon (free tier) e secrets fora do repositório
metadata:
  type: decision
  status: superseded
---

# ADR-0014: Deploy no Fly.io com Postgres no Neon

> Produção roda como container Docker no Fly.io (`lab-dispatch-api`, `min_machines_running = 0`),
> com banco Postgres no Neon. Connection string e chave JWT entram como `fly secrets`, nunca em
> arquivo.

## Status

Superseded by [ADR-0015](0015-migracao-do-fly-io-para-o-render.md) — o free trial do Fly acabou e
passou a exigir cartão. **O Neon continua** (a parte do banco desta decisão segue valendo).

Aceito em 2026-08-28 (commit `519f9ef`); stack declarada desde o scaffold (2026-08-26).

## Contexto

Projeto pessoal, custo zero como restrição. Precisava de um Postgres gerenciado e de um host de
container gratuitos.

## Decisão

- **Banco**: Neon (free tier), Postgres serverless. Local usa um container Postgres do
  `docker-compose.yml`, sem dado de produção; o sistema de configuração em camadas do ASP.NET Core
  troca a connection string por ambiente, sem `if`.
- **Host**: Fly.io (free tier), `fly.toml` com `min_machines_running = 0` (dorme ocioso),
  Dockerfile multi-stage na porta 8080.
- CORS deixa de ser Development-only: uma policy sempre ativa, origem em `Cors:AllowedOrigin`.
- Primeira Distribuidora de produção criada direto no banco.

## Alternativas consideradas

Não há registro de alternativas comparadas para o host na época — o Fly era o free tier conhecido.
O que ficou registrado é a troca posterior (ADR-0015).

## Consequências

**Negativas** — cold start após hibernação; dependência de um free tier que mudou de regra 3 dias
depois.

## Referências

- Substituído por [ADR-0015](0015-migracao-do-fly-io-para-o-render.md).
- `docs/patterns/deploy.md`.
- `docs/historico.md`, "Deploy em produção: Fly.io + Neon".
