---
name: adr-0010-login-devolve-usuario-e-auth-me
description: POST /auth/login devolve o usuário junto com o token e GET /auth/me reidrata a sessão — o front nunca decodifica o JWT
metadata:
  type: decision
  status: accepted
---

# ADR-0010: Login devolve o usuário + `GET /auth/me`; o front nunca decodifica o JWT

> Token é credencial, não fonte de dado de perfil. `POST /auth/login` devolve
> `{ token, usuario }` e `GET /auth/me` devolve o mesmo `usuario` a partir do token, para o boot da
> SPA reidratar a sessão.

## Status

Accepted — 2026-08-27 (commit `c4c9d53`), decidido planejando o `dispatch-web`. O campo `papel`
virou `papeis` (lista) em 2026-09-15 (ADR-0028).

## Contexto

O front precisava saber quem está logado. Decodificar o JWT no cliente acopla o front à forma exata
das claims e não resolve o F5: a SPA guarda o token, mas "quem é o usuário" não sobrevive se a única
fonte for a resposta do login.

## Decisão

- `ResultadoAutenticacao.Autenticado` ganha os campos do usuário; `POST /auth/login` devolve
  `{ token, usuario: { id, nome, email, papeis } }` — evita uma chamada extra logo após logar.
- `GET /auth/me` (autenticado, qualquer papel) resolve o usuário por `ClaimTypes.NameIdentifier`.
  `ObterUsuarioAtual` é pass-through fino (a Api nunca injeta repositório direto).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Front decodifica o JWT | Zero endpoint novo | Acopla o front às claims; mistura credencial com perfil | Token é credencial, não dado de perfil |
| Só `/auth/me`, login devolve só o token | Uma fonte só | Chamada extra obrigatória após todo login | Devolver o usuário no login é barato |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Acoplamento | ✅ Melhora | Claims podem mudar sem quebrar o front |
| Custo por request | ➖ Neutro | Uma chamada no boot |

## Consequências

**Riscos** — papéis no JWT ficam fixos até novo login; o `/auth/me` reflete o banco (ver
`docs/patterns/autorizacao.md`).

## Referências

- `docs/historico.md`, "Login devolve o usuário + GET /auth/me".
- `../dispatch-web/CLAUDE.md` (lado do front).
