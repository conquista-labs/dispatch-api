---
name: adr-0025-health-check-sem-banco
description: /health (usado pelo Render para rotear) nunca toca o banco; a checagem real do Postgres fica em /health/db, separada, para diagnóstico
metadata:
  type: decision
  status: accepted
---

# ADR-0025: `/health` não toca o banco; `/health/db` é a checagem real

> `/health` responde `{"status":"ok"}` sem depender do Postgres. `/health/db` chama
> `dbContext.Database.CanConnectAsync()` e devolve 503 se falhar — para diagnóstico manual ou
> monitoramento externo, **não** ligado ao roteamento do Render.

## Status

Accepted — 2026-09-14 (commit `cb536bb`, segunda rodada da auditoria de performance/resiliência).

## Contexto

`/health` é o `healthCheckPath` do `render.yaml` — o Render decide por ele se o container é
roteável. Nada detectava "app de pé, Postgres inacessível". O Neon é serverless e hiberna.

## Decisão

Dois endpoints: `/health` puro (roteamento) e `/health/db` (checagem real). Complementa o
`EnableRetryOnFailure()` no `UseNpgsql` (mesma auditoria), que absorve falhas transitórias de
conexão.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| `/health` checando o banco | Um endpoint só; detecção automática | Cold start do Neon derrubaria o health check; o Render poderia parar de rotear ou reiniciar o container bem quando a conexão precisa de uma chamada real para "acordar" | Tira tráfego por lentidão transitória |
| Nenhuma checagem de banco (status quo) | Simples | Falha de banco invisível | Era o achado da auditoria |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Disponibilidade | ✅ Melhora | Hibernação do banco não tira a API do ar |
| Observabilidade | ✅ Melhora | Existe um ponto de checagem real |

## Consequências

**Negativas** — ninguém é alertado automaticamente se o banco cair; `/health/db` precisa ser
consultado (não há monitoramento externo configurado).

## Referências

- `docs/patterns/deploy.md`.
- `docs/historico.md`, "Segunda rodada da auditoria — os 4 itens restantes, todos implementados".
