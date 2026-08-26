---
name: verify-integration
description: Use after implementing or changing an endpoint or use case, to confirm it actually works end-to-end against the real HTTP pipeline — not just unit tests with fakes. Extends to Playwright browser flows once dispatch-web exists.
---

# Validar comportamento real, não só unidade

Teste de unidade (coberto pela skill `add-domain-rule`) prova que uma regra está certa isolada.
Esta skill cobre a camada seguinte: o sistema *de verdade* funciona quando tudo está ligado —
roteamento, serialização, validação de request, DI, banco?

## Sobre Playwright

Playwright é uma ferramenta de automação de **navegador**. Só faz sentido quando existir uma UI
real pra interagir — hoje o `dispatch-web` está vazio, então não se aplica ainda. Enquanto o
Dispatch é só a API, o equivalente correto em .NET é um **teste de integração HTTP real** via
`Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`): ele sobe a aplicação
inteira em memória — DI, middlewares, roteamento — e faz requisições HTTP de verdade contra os
endpoints. Quando o `dispatch-web` existir, esta skill ganha uma segunda seção com fluxos
Playwright ponta a ponta contra a aplicação real.

## Passos (hoje — backend)

1. Se ainda não existir, crie um projeto de teste de integração (ex: `Dispatch.Api.Tests`)
   referenciando `Microsoft.AspNetCore.Mvc.Testing` e o projeto `Dispatch.Api`.
2. Para cada endpoint novo ou alterado, escreva um teste que sobe a `WebApplicationFactory`,
   chama o endpoint via HTTP real (`client.GetAsync(...)`, `client.PostAsJsonAsync(...)`) e
   valida status code + corpo — **chame pela HTTP, não o caso de uso diretamente**, pra pegar
   problema de rota, serialização ou validação que um teste de unidade não pega.
3. Se o endpoint mexe no banco, use um banco de teste real (Postgres local via Docker, ou um
   branch/banco de desenvolvimento separado no Neon) — nunca aponte teste de integração pra
   produção.
4. Antes de considerar uma fatia pronta: rode `dotnet test` (unidade + integração) **e** suba a
   API localmente (`dotnet run --project src/Dispatch.Api`) pra bater no endpoint manualmente
   ao menos uma vez (curl, o arquivo `.http`, ou Postman). Teste automatizado não substitui ver
   o comportamento de negócio acontecendo de verdade.

## Passos (futuro — quando dispatch-web existir)

5. Fluxos de usuário completos (ex: "importar relatório → revisar → confirmar distribuição →
   protocolo aparece na fila do conferente") passam a ser cobertos por testes Playwright,
   rodando contra o front-end real conectado a uma API real ou de teste.
