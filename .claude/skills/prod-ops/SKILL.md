---
name: prod-ops
description: Procedimentos que tocam produção do Dispatch (Neon + Render) com as travas certas — aplicar migration no Neon antes do push, rodar SQL avulso documentado (ex. promover o primeiro administrador), clonar produção para o Postgres local com anonimização, e conferir um deploy. Use sempre que uma tarefa for ler ou escrever no banco de produção, dar push que publica, ou quando o usuário pedir "aplica em produção", "sobe", "clona a base", "promove fulano".
---

# prod-ops

Produção é o cartório funcionando. O **conhecimento** (ambientes, env vars, gotchas de porta,
CORS, Swagger) está em `docs/patterns/deploy.md` — leia antes. Esta skill é o **procedimento**,
com as travas.

## Travas — valem pra tudo abaixo

1. **Nada escreve em produção sem o usuário confirmar aquele comando específico, naquela hora.**
   Aprovação de antes, ou de outra operação, não vale. Mostre o comando (com a connection string
   mascarada) e o efeito esperado; espere o "sim".
2. **Connection string e segredos nunca vão pro transcript nem pra arquivo do repo.** Peça ao
   usuário pra exportar numa variável de ambiente da sessão (`export NEON_URL=...` digitado por ele
   com `!` no prompt) e use `"$NEON_URL"`; não ecoe, não faça `cat`/`env`.
3. **Leia antes de escrever.** Toda escrita começa com um `SELECT` que mostra quantas linhas e quais
   seriam afetadas. Número diferente do esperado → pare e pergunte.
4. **Uma coisa por vez**, e confira o resultado antes da próxima.
5. **Push no `main` do api é deploy** (Render, auto-deploy). A migration que o código novo precisa
   tem de estar aplicada no Neon **antes** do push.

## Aplicar migration no Neon

1. A migration existe, foi aplicada e testada localmente (skill `ef-migration`, suíte do `api-gate`).
2. Veja o SQL que vai rodar: `dotnet ef migrations script <UltimaAplicadaEmProd> <Nova> --project
   src/Dispatch.Infrastructure --startup-project src/Dispatch.Api --idempotent`. Aditiva (`ADD COLUMN
   ... DEFAULT`, `CREATE TABLE`, `CREATE INDEX`)? Se mexe em dado (`UPDATE`, backfill, `DROP`),
   valide antes contra um clone (abaixo) e conte as linhas afetadas em produção.
3. Com a confirmação: `dotnet ef database update --project src/Dispatch.Infrastructure
   --startup-project src/Dispatch.Api --connection "$NEON_URL"`.
4. Confira: `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 3;`.
5. Só então o push (skill `api-commit`).

## SQL avulso documentado

Só SQL que está escrito num doc ou plano (ex.: a promoção do primeiro administrador, no
`PLANO-melhorias.md`/ADR do perfil Administrador). Nunca SQL improvisado em produção.

1. `SELECT` de conferência (quem/quantos serão afetados).
2. Mostre o `UPDATE`/`INSERT` exato; confirmação.
3. Rode dentro de `BEGIN; ... ; SELECT ...; COMMIT;` — confira o `SELECT` antes do `COMMIT` e use
   `ROLLBACK` se não bater.
4. Efeito colateral que o doc prevê (ex.: `sessoes_validas_apartir_de = now()` força novo login) —
   avise o usuário.

Cliente: `psql` de um container `postgres:18` avulso (o local é 17.x, o Neon é 18.x):
`docker run --rm -it postgres:18 psql "$NEON_URL"`.

## Clonar produção para o local (anonimizado)

Procedimento completo em `docs/patterns/deploy.md` → "Clonar produção". Travas específicas:
produção só é **lida** (`pg_dump`); anonimize nome/e-mail de funcionário **antes** de qualquer
análise; depois rode `POST /dev/seed-e2e` pra recriar as contas de teste (o clone não as tem).

## Conferir um deploy

1. Depois do push: `curl -s https://lab-dispatch-api.onrender.com/health` até responder (cold start
   do plano free leva segundos; logo após o primeiro deploy, minutos de 404/`no-server` são normais).
2. `curl -s https://lab-dispatch-api.onrender.com/health/db` — 200 confirma o Neon.
3. `x-render-routing: no-server` com o log dizendo "live" é mismatch de porta (`PORT=8080`), não
   queda — ver `deploy.md`.
4. Front: `netlify deploy --prod --build` é manual e é o usuário quem decide quando; com mudança de
   contrato, a ordem é API primeiro (compatível), front depois.

## Reporte

O que rodou em produção (comando sem segredo), o `SELECT` de conferência antes/depois, e o que ficou
pendente pro usuário fazer no dashboard (env var no Render, por exemplo).
