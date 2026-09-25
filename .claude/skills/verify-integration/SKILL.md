---
name: verify-integration
description: Confirma que um endpoint ou caso de uso novo/alterado funciona de verdade no pipeline HTTP real — teste de integração (WebApplicationFactory + Postgres via Testcontainers) e um smoke manual com a API rodando (`dotnet run` + curl com token real). Use depois de implementar ou mudar endpoint, caso de uso, registro de DI, autorização ou persistência — não só teste de unidade com fake.
---

# verify-integration

Teste de unidade (skills `add-domain-rule`, `api-testing-strategy`) prova que a regra está certa
isolada. Esta skill cobre a camada seguinte: o sistema **ligado** funciona — roteamento,
serialização, validação de request, DI, autorização, EF Core, Postgres.

Infra da suíte, bugs que só o banco real pega e o checklist manual: `docs/patterns/testes.md`.

## Passos

1. **Teste de integração** em `tests/Dispatch.Api.Tests` para o que fake não prova (skill
   `api-testing-strategy`, passo 1.3/1.4): chame **pela HTTP** (`client.PostAsJsonAsync(...)`),
   não o caso de uso direto; valide status e corpo; pra persistência, releia numa query nova.
   Autenticação: `AutenticarComoAsync(Papel.X)` (login real via `POST /dev/seed-e2e`). Nunca
   aponte teste pra produção — o Postgres é efêmero (Testcontainers).
2. **Suba a API de verdade**: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Dispatch.Api
   --no-launch-profile --urls http://localhost:5245`. Minimal API só falha ao montar o endpoint
   quando falta registro no DI (`Failure to infer one or more parameters`) — `build` e `test` passam
   limpos mesmo assim. Espere com laço `until curl -s localhost:5245/health` (não `sleep`).
3. **Smoke manual** com token real: `POST /auth/login` com uma conta seed, depois o endpoint novo
   via `curl` — o caminho feliz e pelo menos um erro (403 do papel errado, 404, 409). Filtre saída
   com `grep -viE "password|senha|token"`. Confira no banco (`psql` no container local) quando o
   efeito é persistência.
4. **Fluxo de tela**: quando a mudança tem tela no `dispatch-web`, o fluxo ponta a ponta no
   navegador é a skill `verify-visual` de lá (Playwright com login real contra esta API local).
5. Derrube a API ao terminar (`pkill -f "Dispatch.Api"`) se foi você que subiu.

## Reporte

O que rodou e o resultado (status/corpo relevantes), o que conferiu no banco, e o que **não** foi
verificado.
