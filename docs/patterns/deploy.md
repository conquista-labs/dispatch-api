---
name: deploy
description: Produção no Render + Neon — Blueprint, env vars, gotchas de porta e roteamento, CORS, migration em produção, primeira conta, clone de produção para o Postgres local com anonimização
metadata:
  type: pattern
  domains: [deploy, render, neon, postgres, operacao]
  status: stable
---

# Deploy e operação de banco

> Decisões: [ADR-0014](../decisions/0014-deploy-no-fly-io-com-neon.md) (Fly + Neon, substituído) e
> [ADR-0015](../decisions/0015-migracao-do-fly-io-para-o-render.md) (Render). Health check:
> [ADR-0025](../decisions/0025-health-check-sem-banco.md).

## Quando ler

- Antes de `git push` em `main` com migration nova ou env var nova.
- Ao aplicar migration em produção, criar a primeira conta de um ambiente, ou clonar produção.
- Quando o Render diz "live" mas a API não responde.

## Ambientes

| | Local | Produção |
| --- | --- | --- |
| API | `dotnet run --project src/Dispatch.Api` | `https://lab-dispatch-api.onrender.com` (Render, Web Service Docker, free, oregon) |
| Banco | Postgres 17 do `docker-compose.yml` (credenciais fixas de dev, não são segredo) | Neon (free tier, Postgres 18) |
| Front | Vite em `localhost:5173` | `https://lab-dispatch-web.netlify.app` (deploy manual `netlify deploy --prod --build`) |

A connection string troca sozinha por ambiente pelo sistema de configuração em camadas do ASP.NET
Core (`appsettings.json` → `appsettings.{Environment}.json` → variáveis de ambiente/secrets,
conforme `ASPNETCORE_ENVIRONMENT`) — sem `if` de ambiente no código. A de Development está em
`src/Dispatch.Api/appsettings.Development.json`; a de produção nunca fica em arquivo.

## Render

- **Auto-deploy**: push em `main` dispara deploy sozinho. Depois de um `git push`, o código já está
  indo para o ar — não presuma um passo manual (diferente do front).
- `render.yaml` (Blueprint, raiz): nome `lab-dispatch-api`, Dockerfile, região, plano,
  `healthCheckPath: /health`, env vars não secretas. Secretas com `sync: false`, preenchidas no
  dashboard na criação ("New +" → "Blueprint" → conectar o repo).
- Env vars de produção: `ASPNETCORE_ENVIRONMENT=Production`, `PORT=8080`,
  `ConnectionStrings__DispatchDb` (Neon, secreta), `Jwt__ChaveDeAssinatura` (secreta; gerada nova na
  migração do Fly, invalidou sessões antigas), `Jwt__Emissor=dispatch-api`,
  `Jwt__Audiencia=dispatch-clientes`, `Jwt__ExpiracaoMinutos=480`, `Cors__AllowedOrigin`.
- **Mudar env var de serviço já criado: pelo dashboard.** Sync do Blueprint no push não é garantido.
  Pendência registrada: confirmar `Jwt__ExpiracaoMinutos=480` no dashboard (só o dono tem acesso).
- **Gotcha de porta**: serviço Docker no Render não usa só o `EXPOSE`; precisa de `PORT=8080`
  batendo com o `ASPNETCORE_URLS=http://+:8080` do Dockerfile. Sem isso o proxy responde
  `x-render-routing: no-server` **com a app rodando e logando "Your service is live"** — parece que
  caiu, é só mismatch de porta.
- Logo após o primeiro deploy o roteamento pode levar alguns minutos (404/no-server); não precisa
  re-deployar, só esperar.
- Plano free hiberna por inatividade: cold start na próxima chamada (mesmo trade-off do Fly).
- Dockerfile multi-stage (SDK compila, runtime ASP.NET publica), porta 8080.

## Neon

- Serverless, hiberna. `UseNpgsql(..., o => o.EnableRetryOnFailure())` absorve falhas transitórias
  (cold start, blip de rede) que antes subiam como 500. Premissa: nada usa transação explícita (ver
  `ef-core.md`).
- `/health` não toca o banco (roteamento do Render); `/health/db` faz `CanConnectAsync()` e devolve
  503 se falhar.
- Latência de rede real: N+1 que é invisível local vira lentidão visível em produção.

## CORS

Uma policy só, sempre ativa, origem em `Cors:AllowedOrigin` (Development: `http://localhost:5173`;
produção: env var apontando para o Netlify). Trocar o host do front não pede recompilar, só atualizar
a variável. Testes com `curl` nunca pegam CORS — só o navegador faz preflight.

## Swagger

`MapOpenApi()` (`/openapi/v1.json`) + Swagger UI (`/swagger`) ligados **também em produção** desde
2026-08-28 (projeto pessoal; só documenta a forma — usar exige token). Gotchas de OpenAPI em
`endpoints.md`.

## Mudança de schema em produção

1. Schema só muda por migration (skill `ef-migration`); nunca editar o Neon na mão.
2. Migration que adiciona coluna referenciada pelo modelo precisa estar **aplicada no Neon antes** do
   código ir para o ar — senão toda leitura da entidade quebra. Com auto-deploy no push, aplique antes
   do push (ou imediatamente, confirmando com o dono).
3. Comando (com a connection string do Neon, confirmado com o dono antes de rodar):
   `dotnet ef database update --project src/Dispatch.Infrastructure --startup-project src/Dispatch.Api --connection "..."`
4. Prefira migrations aditivas (`ADD COLUMN ... DEFAULT`, `CreateTable`, `CreateIndex`). Quando mexer
   em dado, valide antes contra um clone (abaixo) e confira o volume afetado em produção.

## Primeira conta de um ambiente

Não existe registro público — só `POST /auth/login`. A primeira Distribuidora entra direto no banco,
com o hash gerado pelo **mesmo** `PasswordHasher<object>` do `HashDeSenhaAspNetCore` (senão
`Autenticar` não reconhece o hash). Feito uma vez para produção (a conta e o Neon sobreviveram à
migração de host). Em Development, `POST /dev/seed-e2e` cria/reseta as contas fixas de teste.

## Clonar produção para o Postgres local (anonimizado)

Usado para validar migration com dado real (motor v2) e para análise visual da Central de Regras.

- `pg_dump`/`pg_restore` (ou `psql`) rodando **num container `postgres:18` avulso**: o cliente local é
  17.x e o Neon roda 18.x — mismatch de major version trava o `pg_dump` direto.
- De dentro do container, alcance o Postgres local por `host.docker.internal`.
- Produção só é **lida** (`pg_dump`). Anonimize nomes/e-mails reais de funcionários no clone local
  antes de qualquer análise.
- Depois do clone, as contas fixas do e2e não existem (o clone tem gente real): rode
  `POST /dev/seed-e2e` — ele cria ou **reseta** senha, nome, e-mail, bloqueio e presença.

## Legado Fly.io

`lab-dispatch-api.fly.dev` ficou parado (trial expirado), não excluído; `fly.toml` ainda está no repo
(`min_machines_running = 0`).

## Referências

- ADR-0014, ADR-0015, ADR-0025.
- `ef-core.md` (migrations seguras), `autorizacao.md` (JWT, sessão).
