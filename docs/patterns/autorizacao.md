---
name: autorizacao
description: Como papéis viram claims (inclusive o Administrador que carrega Distribuidora), grupos RequireRole e como combinam, o que é só do admin e que campo é cortado pra quem não é, troca de senha obrigatória, visões restritas, PapeisEfetivos, validação do JWT com SessoesValidasApartirDe, bloqueio de login, TOTP/recuperação e auditoria de autenticação
metadata:
  type: pattern
  domains: [autenticacao, autorizacao, jwt, seguranca]
  status: stable
---

# Autenticação e autorização

> Decisões: [ADR-0003](../decisions/0003-autenticacao-jwt-propria-sem-aspnet-identity.md) (JWT
> próprio), [ADR-0010](../decisions/0010-login-devolve-usuario-e-auth-me.md) (`/auth/me`),
> [ADR-0020](../decisions/0020-totp-so-para-recuperacao-de-senha.md) (TOTP/recuperação),
> [ADR-0028](../decisions/0028-uma-conta-com-dois-papeis.md) (dois papéis),
> [ADR-0039](../decisions/0039-perfil-administrador.md) (Administrador),
> [ADR-0040](../decisions/0040-troca-de-senha-obrigatoria-no-primeiro-acesso.md) (troca de senha no
> primeiro acesso). Regra da casa: a
> restrição entre papéis é **sempre no servidor** (RNF-04), nunca só na interface.

## Quando ler

- Ao criar endpoint novo (qual grupo, qual papel) ou alargar acesso de um existente.
- Ao mexer em `Program.cs` na parte de JWT, em `EmissorDeTokenJwt` ou no fluxo de senha.
- Quando um usuário "tem o papel no banco" mas recebe 403.

## Papéis → claims

- Papéis gravados: `Distribuidora`, `Conferente`, `Administrador` (`Dispatch.Domain/Usuarios/Papel.cs`).
- `EmissorDeTokenJwt` emite **uma claim `ClaimTypes.Role` por papel efetivo** + `NameIdentifier`
  (`Usuario.Id`) + `iat` explícito.
- **Papéis efetivos** (`PapeisEfetivos`, Application): `Usuario.Papel`, mais `Conferente` se existir
  `Conferente` com aquele `UsuarioId`. **`Administrador` vira `[Administrador, Distribuidora]`** — a
  claim `Distribuidora` significa "tem acesso de gestão", e o admin passa em todo grupo de gestão sem
  nenhuma rota mudar (ADR-0039). A ordem importa: o front usa `papeis[0]`. Calculado no login e no
  `/auth/me`.
- No endpoint, "é admin?" é `usuario.EhAdministrador()` (`ClaimsPrincipalExtensions`).
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
  (`DefinirObservacao` com `conferenteRestritoId`), `GET /dashboard` (visão restrita RF-45) e `GET /dashboard/hoje` (visão "Seu dia", RF-42a — mesma regra, via `ResolverVisaoRestritaAsync`). A
  checagem é **`usuario.IsInRole(Conferente) && !usuario.IsInRole(Distribuidora)`** — ter
  Distribuidora dá sempre a visão de gestão; ser também Conferente só soma.
- Distribuidora vendo a fila de alguém: `GET /conferentes/{id}/fila` e `/concluidos-hoje` resolvem o
  `Conferente` pelo id da URL; os casos de uso (`ObterMinhaFila`/`ObterConcluidosHoje`) nunca
  dependeram de "quem está logado".
- **Recorte por rota, não por papel** (ADR-0046): `GET /minha-fila` manda `escreventeId: null` no pool e nas
  atribuídas (o conferente não escolhe o ato por quem fez); `GET /conferentes/{id}/fila` manda tudo. O
  Conferente não tem outro caminho para ligar escrevente a um ato: `GET /protocolos/{id}/detalhe` é só
  Distribuidora, e `GET /escreventes` lista nomes sem ato. Rota nova que o Conferente leia e que traga
  `escreventeId` de ato fora de conferência precisa do mesmo recorte.
- `POST /dev/seed-e2e` é anônimo e só existe em Development (gate no `Program.cs`). Semeia também
  `distribuidora@` e `administrador@cartorio.com` (senha `Senha123!`).

### O que é só do Administrador (§3, RF-29a, RF-30a, 6.8)

Rota dentro de grupo de Distribuidora ganha `.RequireAuthorization(p => p.RequireRole(nameof(Papel.Administrador)))`
— combina com **E** com o do grupo, então a distribuidora leva 403. Grupo inteiro do admin troca o
papel do próprio grupo.

| Onde | Só Administrador | Continua Distribuidora |
| ---- | ---------------- | ---------------------- |
| `/conferentes` | cadastrar, vincular, editar perfil, nível/jornada, remover | listar, presença, `alcance`, fila de alguém |
| `/regras-alcada` | criar, ativar, desativar, remover, testar | listar |
| `/tipos-ato` | criar, grupo de gestão (`com-uso`, editar, ativar/desativar, remover) | `GET /tipos-ato` |
| `/equipes`, `/escreventes` | todas as escritas e `sem-equipe` | listagens |
| `/config` | `PUT` | `GET` |
| `/sugestoes` | o grupo todo | — |
| `/contas` | o grupo todo (RF-44 a 47) | — |

### Campos cortados pra quem não é admin

O corte mora na **Application**, por uma flag que o endpoint passa com `usuario.EhAdministrador()` —
nunca no endpoint escolhendo campo, e nunca só no front. Flag com default fechado (ou obrigatória).
Invariante: **o nível só sai do servidor num token de Administrador**.

| Leitura | Flag | Sem a flag |
| ------- | ---- | ---------- |
| `GET /conferentes` (`ListarConferentes`) | `incluirNivel` | `nivel: null` (jornada continua) |
| `GET /dashboard` (`ObterDashboard`) | `incluirAvaliacaoDePessoal` (default `false`) | `nivel`, `score`, `faixa`, `parcelas` e `pesos` null; lista **por nome** (ordem por score entregaria o ranking). Visão restrita do conferente: mantém o próprio score e os `pesos`, só perde o nível (e não recebe `metas`, que é só da gestão) |
| `GET /regras-alcada` (`ListarRegrasAlcada`) | `incluirNivel` | regra por nível sai com `sujeitoNivel: null`; `regraBase: true` quando não é equipe+etapa |

A trilha do detalhe do protocolo não é cortada: o painel só usa `regraAplicadaId`, resolvido contra a
lista já mascarada; a trilha por camada só aparece no simulador Testar (admin). Inferência residual
aceita — ver ADR-0039.

### Troca de senha obrigatória (RF-45, ADR-0040)

- Conta criada por outra pessoa (`CriarConta`, `CadastrarConferente`) nasce com senha inicial de 8+
  (`RegrasDeSenha.ServeComoSenhaInicial`) e `Usuario.TrocarSenhaNoProximoAcesso`; qualquer
  `RedefinirSenha` desliga a flag.
- O token dessa conta carrega a claim `trocar_senha` (`ClaimsDoDispatch.TrocarSenha`), e login e
  `/auth/me` devolvem `trocarSenha: true`.
- Um **middleware** entre `UseAuthentication` e `UseAuthorization` (`Program.cs`) só deixa esse token
  chamar `/auth/me` e `POST /auth/trocar-senha`; o resto recebe 403
  `{ codigo: "troca_de_senha_obrigatoria" }`. Rota nova que precise funcionar durante a troca entra em
  `RotasLiberadasComTrocaDeSenhaPendente`.
- `POST /auth/trocar-senha { senhaAtual, novaSenha }` exige senha forte (RF-01j) e devolve `{ token }`
  novo, sem a claim. 400 com `codigo` `senha_atual_incorreta` / `senha_fraca`.

## Validação do JWT e sessões

- `Jwt:*` (`ChaveDeAssinatura`, `Emissor`, `Audiencia`, `ExpiracaoMinutos`). Sessão = expiração: o
  front não tem refresh nem aviso (qualquer 401 limpa a sessão). **480 min (8h)**, um expediente —
  decidido com o dono em 2026-09-11 (`appsettings.Development.json` e `render.yaml`; em produção o
  valor real é a env var do dashboard do Render).
- `JwtBearerOptions.Events.OnTokenValidated` busca o `Usuario` e rejeita o token se
  `IssuedAt < Usuario.SessoesValidasApartirDe` (RF-01k) **ou se a conta está inativa** (desativada em
  Contas ou conferente removido perde o acesso na hora, não em 8h). Custo: 1 consulta por request
  autenticado.
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

- ADR-0003, ADR-0010, ADR-0020, ADR-0028, ADR-0039, ADR-0040.
- `docs/gaps-requisitos.md` (RF-01m/n, bloqueio por origem, Argon2id).
