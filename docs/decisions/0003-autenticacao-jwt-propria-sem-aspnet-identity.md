---
name: adr-0003-autenticacao-jwt-propria-sem-aspnet-identity
description: Login por e-mail+senha com JWT emitido pela própria API, hash via PasswordHasher (sem o ASP.NET Core Identity inteiro) e papel como ClaimTypes.Role
metadata:
  type: decision
  status: accepted
---

# ADR-0003: Autenticação própria — JWT + `PasswordHasher`, sem ASP.NET Core Identity

> A API autentica por e-mail+senha (`POST /auth/login`), emite um JWT próprio com o papel em
> `ClaimTypes.Role`, e usa só o `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` — sem
> trazer o ASP.NET Core Identity completo.

## Status

Accepted — 2026-08-26 (commit `3decc29`). Estendido depois por ADR-0010 (login devolve usuário),
ADR-0020 (TOTP/recuperação) e ADR-0028 (uma claim de role por papel efetivo).

## Contexto

Até aqui nenhum endpoint tinha proteção. RNF-04 exige autorização por papel **no servidor**. O
requisito pede só e-mail+senha; cadastro de usuário completo ficaria para o RF-25.

## Decisão

Escopo deliberadamente mínimo:

- `Usuario`/`Papel` em `Dispatch.Domain/Usuarios/` (id, nome, email, senha_hash, papel, ativo). O
  algoritmo de hash não mora no Domain.
- `Autenticar` (Application) com portas `IUsuarioRepository`, `IHashDeSenha`, `IEmissorDeToken`.
  O resultado (`Autenticado`/`Rejeitado`) não diferencia e-mail inexistente de senha errada.
- `HashDeSenhaAspNetCore` usa `PasswordHasher<object>` — o parâmetro de tipo é só extensibilidade,
  a implementação não usa a instância do usuário.
- `EmissorDeTokenJwt` (`System.IdentityModel.Tokens.Jwt`) põe o papel como `ClaimTypes.Role`, não
  claim customizada — `RequireRole`/`[Authorize(Roles=...)]` funcionam sem policy customizada.
- Config em `Jwt:*`; a chave de assinatura de produção entra como secret do host (nunca em arquivo).
- Sem endpoint de registro público: a primeira Distribuidora de cada ambiente entra direto no banco
  (ver `docs/patterns/deploy.md`).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| ASP.NET Core Identity completo | Reset de senha, confirmação de e-mail, lockout, external login prontos | Traz tabelas, UI e conceitos que o requisito não pede | "Não precisamos de reset de senha, confirmação de e-mail, external login etc." — só o hasher resolve |
| Papel em claim customizada + policy própria | Nome de claim sob controle | Exige policy customizada para cada checagem | `ClaimTypes.Role` deixa `RequireRole` funcionar pronto |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Segurança | ✅ Melhora | Fecha o buraco de endpoints abertos; anti-enumeração desde o primeiro corte |
| Simplicidade | ✅ Melhora | Um pacote pequeno em vez do Identity inteiro |
| Evolutividade | ⚠️ Atenção | Tudo que o Identity daria de graça (lockout, invalidação de sessão) teve de ser escrito à mão depois (ADR-0020, bloqueio de login) |

## Consequências

**Positivas** — autorização por papel com os mecanismos nativos do ASP.NET Core.

**Negativas** — invalidar sessão exigiu customizar a validação do JWT (`OnTokenValidated`,
`SessoesValidasApartirDe`); o token não tinha `iat` até isso ser corrigido (ver
`docs/patterns/autorizacao.md`).

**Riscos** — RNF-15 do documento pede "Argon2id ou bcrypt com custo ajustado"; o
`PasswordHasher` usa PBKDF2 (ver `docs/gaps-requisitos.md`).

## Referências

- `docs/patterns/autorizacao.md` — grupos `RequireRole`, claims, sessões.
- `docs/historico.md`, "Autenticação e autorização".
