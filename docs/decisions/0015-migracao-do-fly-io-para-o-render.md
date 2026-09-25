---
name: adr-0015-migracao-do-fly-io-para-o-render
description: A API sai do Fly.io para um Web Service Docker no Render (free tier sem cartão), descrito por Blueprint (render.yaml), mantendo o mesmo Dockerfile, Neon e Netlify — substitui ADR-0014
metadata:
  type: decision
  status: accepted
---

# ADR-0015: Migração do Fly.io para o Render

> Produção em `https://lab-dispatch-api.onrender.com`: Web Service Docker no Render, plano free,
> descrito por `render.yaml` (Blueprint). Nada de código mudou — mesmo Dockerfile, mesmo Neon, mesmo
> front no Netlify.

## Status

Accepted — 2026-08-31 (commits `dacc899`, `c589aa6`). Substitui [ADR-0014](0014-deploy-no-fly-io-com-neon.md)
na parte de host. Auto-deploy em push para `main` confirmado pelo dono em 2026-09-17 (`957222f`).

## Contexto

O free trial da Fly acabou e passou a exigir cartão. O dono recusou colocar cartão. O Render tem
free tier sem cartão — mesma decisão já usada em outro projeto dele.

## Decisão

- `render.yaml` descreve nome, Dockerfile, região (oregon), plano, `healthCheckPath: /health` e as
  env vars não secretas. As secretas (`ConnectionStrings__DispatchDb`, `Jwt__ChaveDeAssinatura`)
  ficam `sync: false` e são preenchidas no dashboard na criação do serviço.
- `PORT=8080` explícito (Render não usa só o `EXPOSE`).
- Chave JWT nova gerada na migração (invalida sessões antigas, esperado).
- O app antigo no Fly (`lab-dispatch-api.fly.dev`) ficou parado, não excluído; `fly.toml` continua
  no repo.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Continuar no Fly.io com cartão | Nenhuma migração | Exige cartão | O dono recusou |
| Render free tier (escolhido) | Sem cartão; Blueprint versionado; auto-deploy no push | Hiberna por inatividade (cold start); gotcha de porta | — |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Custo | ✅ Melhora | Zero, sem cartão |
| Disponibilidade | ➖ Neutro | Mesmo trade-off de hibernação que o Fly já tinha |
| Entrega | ✅ Melhora | Push em `main` já vai pro ar sozinho |

## Consequências

**Negativas** — `git push` em `main` **é** deploy de produção (diferente do front, que é
`netlify deploy --prod --build` manual). Mudança de env var em serviço já criado não é garantida
pelo sync do Blueprint — precisa do dashboard.

**Riscos** — mismatch de porta faz o proxy responder `x-render-routing: no-server` com a app de pé
(ver `docs/patterns/deploy.md`).

## Referências

- `docs/patterns/deploy.md` — procedimento, env vars, gotchas.
- `docs/historico.md`, "Deploy — no ar (migração Fly.io → Render)".
