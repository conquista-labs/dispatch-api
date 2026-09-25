---
name: autorizacao
description: Como papéis viram claims, grupos RequireRole e como combinam, visões restritas, PapeisEfetivos, validação do JWT com SessoesValidasApartirDe, bloqueio de login, TOTP/recuperação e auditoria de autenticação
metadata:
  type: pattern
  domains: [autenticacao, autorizacao, jwt, seguranca]
  status: stable
---

# Autenticação e autorização

> Decisões: [ADR-0003](../decisions/0003-autenticacao-jwt-propria-sem-aspnet-identity.md) (JWT
> próprio), [ADR-0010](../decisions/0010-login-devolve-usuario-e-auth-me.md) (`/auth/me`),
> [ADR-0020](../decisions/0020-totp-so-para-recuperacao-de-senha.md) (TOTP/recuperação),
> [ADR-0028](../decisions/0028-uma-conta-com-dois-papeis.md) (dois papéis). Regra da casa: a
> restrição entre papéis é **sempre no servidor** (RNF-04), nunca só na interface.

## Quando ler

- Ao criar endpoint novo (qual grupo, qual papel) ou alargar acesso de um existente.
- Ao mexer em `Program.cs` na parte de JWT, em `EmissorDeTokenJwt` ou no fluxo de senha.
- Quando um usuário "tem o papel no banco" mas recebe 403.

## Papéis → claims

- Papéis: `Distribuidora`, `Conferente` (`Dispatch.Domain/Usuarios/Papel.cs`). O documento v2 já fala
  em `Administrador` — ainda não existe (ver gaps).
- `EmissorDeTokenJwt` emite **uma claim `ClaimTypes.Role` por papel efetivo** + `NameIdentifier`
  (`Usuario.Id`) + `iat` explícito.
- **Papéis efetivos** (`PapeisEfetivos`, Application): `Usuario.Papel`, mais `Conferente` se existir
  `Conferente` com aquele `UsuarioId`. Calculado no login e no `/auth/me`.
- **Claims ficam fixas até novo login.** Vincular uma conta como conferente não muda o token atual —
  a pessoa precisa logar de novo. `/auth/me` reflete o banco.
- Ler o usuário no endpoint: parâmetro `ClaimsPrincipal usuario` (minimal API resolve sozinha, sem
  registro) + `usuario.ObterUsuarioId()` (`ClaimsPrincipalExtensions`, substituiu
  `Guid.Parse(FindFirstValue(...)!)` repetido em 8 lugares). Minha fila resolve o `Conferente` via
  `IConferenteRepository.ObterPorUsuarioIdAsync`.

## Grupos e policies

- Padrão: `app.MapGroup("/x").RequireAuthorization(p => p.RequireRole(nameof(Papel.Distribuidora)))`.
  `/minha-fila` é o grupo exclusivo de `Conferente`.
- **`RequireAuthorization` repetido numa rota NÃO substitui o do `MapGroup` — combina com E.** Cada
  requisito aplicado precisa ser satisfeito. Para uma rota do grupo aceitar mais papéis, tire a policy
  do grupo e declare por rota (foi o que `equipesGrupo`/`escreventesGrupo` em `EquipeEndpoints.cs`
  passaram a fazer). `GET /tipos-ato` é `app.MapGet` solto, então bastou alargar a própria rota.
- Leituras de listagem que o Conferente precisa para os filtros de Minha fila (RF-24f) aceitam os dois
  papéis: `GET /equipes`, `GET /escreventes`, `GET /tipos-ato`. Mutações e `GET /escreventes/sem-equipe`
  continuam só Distribuidora. Sintoma quando falta: 403 silencioso no front e todo protocolo cai em
  "sem equipe".
- Endpoints que servem os dois papéis e restringem por dentro: `PUT /protocolos/{id}/observacao`
  (`DefinirObservacao` com `conferenteRestritoId`), `GET /dashboard` (visão restrita RF-45). A
  checagem é **`usuario.IsInRole(Conferente) && !usuario.IsInRole(Distribuidora)`** — ter
  Distribuidora dá sempre a visão de gestão; ser também Conferente só soma.
- Distribuidora vendo a fila de alguém: `GET /conferentes/{id}/fila` e `/concluidos-hoje` resolvem o
  `Conferente` pelo id da URL; os casos de uso (`ObterMinhaFila`/`ObterConcluidosHoje`) nunca
  dependeram de "quem está logado".
- `POST /dev/seed-e2e` é anônimo e só existe em Development (gate no `Program.cs`).

## Validação do JWT e sessões

- `Jwt:*` (`ChaveDeAssinatura`, `Emissor`, `Audiencia`, `ExpiracaoMinutos`). Sessão = expiração: o
  front não tem refresh nem aviso (qualquer 401 limpa a sessão). **480 min (8h)**, um expediente —
  decidido com o dono em 2026-09-11 (`appsettings.Development.json` e `render.yaml`; em produção o
  valor real é a env var do dashboard do Render).
- `JwtBearerOptions.Events.OnTokenValidated` busca o `Usuario` e rejeita o token se
  `IssuedAt < Usuario.SessoesValidasApartirDe` (RF-01k). Custo: 1 consulta por request autenticado.
- **Gotcha**: ASP.NET Core 10 valida com `Microsoft.IdentityModel.JsonWebTokens.JsonWebToken`, não
  `JwtSecurityToken`. Cast para o tipo antigo compila e explode em runtime (`InvalidCastException`) na
  primeira chamada autenticada.
- **Gotcha**: a sobrecarga de `JwtSecurityToken` usada não preenche `iat` sozinha. Sem `iat`, todo
  token passou a ser rejeitado assim que alguém trocou a senha. Claim `JwtRegisteredClaimNames.Iat`
  (Unix seconds) explícita; `SessoesValidasApartirDe` truncado ao segundo (login e troca no mesmo
  segundo).
- Mudança na validação do JWT é invisível para `build`/`test`: prove com login real + chamada
  autenticada e rode o e2e do front.

## Senha, bloqueio e anti-enumeração

- Hash: `PasswordHasher<object>` (`HashDeSenhaAspNetCore`). Salga a cada chamada — nunca consulte por
  hash. A primeira conta de um ambiente precisa ser gerada com esse mesmo hasher (ver `deploy.md`).
- `RegrasDeSenha` (Domain): ≥12 caracteres, não começar com `senha|123|cartorio|dispatch`. Back é a
  fonte da verdade; o front replica para feedback ao vivo.
- **Bloqueio de login por senha** (`Usuario.TentativasLoginFalhas`/`BloqueadoAte`): 5 erradas → 15 min,
  mesmos números do bloqueio de TOTP, mas no `Usuario` (vale com ou sem autenticador). Bloqueado é
  rejeitado sem checar a senha; sucesso zera o contador.
- **Anti-enumeração**: login rejeitado, conta bloqueada e e-mail inexistente devolvem o mesmo
  `Rejeitado()` → 401 genérico. `IniciarRecuperacaoSenha` sempre 200; `ValidarCodigoRecuperacao`
  devolve o mesmo `CodigoInvalido` para os três motivos.

## TOTP e recuperação (RF-01a a RF-01l)

- Endpoints: `POST /auth/totp/registrar` e `/confirmar` (autenticados, `TotpEndpoints.cs`);
  `POST /auth/recuperar/iniciar` (sempre 200), `/validar-codigo` (200/401/423), `/redefinir-senha`
  (204/400/401) — anônimos, `RecuperacaoSenhaEndpoints.cs`. Login/me em `AuthEndpoints.cs`.
- `UsuarioTotp` (chave = `UsuarioId`): segredo cifrado (`CifradorAes`, chave `Totp:ChaveDeCifragem`),
  contador anti-reuso, tentativas/bloqueio, hash do token de recuperação.
- Token de recuperação opaco `{usuarioId:N}.{aleatório}`, uso único.
- Trocar a senha: bumpa `SessoesValidasApartirDe` e devolve ao pool os atos **em conferência** da
  pessoa.
- Para testar TOTP de verdade: calcule o código a partir do segredo Base32 devolvido pelo `registrar`
  (foi feito com Python), não com mock.

## Auditoria de autenticação (RNF-16)

`EventoAutenticacao` (tipos em `TipoEventoAutenticacao`: registro TOTP, recuperação, `LoginFalhou`,
`LoginBloqueado`...) com autor, horário e **`Origem`**: `HttpContextExtensions.ObterOrigem()` —
`X-Forwarded-For` primeiro (o proxy do Render preenche), `Connection.RemoteIpAddress` como fallback;
coluna `varchar(64)` (cabe IPv6 e lista separada por vírgula). Todo `ExecutarAsync` que audita recebe
`string? origem` posicional antes do `CancellationToken`. Ainda **sem tela de consulta** (gap).

## Referências

- ADR-0003, ADR-0010, ADR-0020, ADR-0028.
- `docs/gaps-requisitos.md` (RF-01m/n, administrador, bloqueio por origem, Argon2id).
