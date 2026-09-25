---
name: adr-0040-troca-de-senha-obrigatoria-no-primeiro-acesso
description: Conta criada por outra pessoa (Contas ou cadastro de conferente) nasce com TrocarSenhaNoProximoAcesso; o token leva uma claim e um middleware só deixa passar /auth/me e /auth/trocar-senha até a troca
metadata:
  type: decision
  status: accepted
---

# ADR-0040: Troca de senha obrigatória no primeiro acesso

> Conta criada pelo admin (ou conferente cadastrado) entra com uma senha inicial de 8+ caracteres e é
> obrigada a trocar por uma forte antes de usar o sistema. O bloqueio é no servidor: claim
> `trocar_senha` no token + um middleware.

## Status

`Accepted — 2026-09-25`

## Contexto

RF-45: "senha inicial (mínimo 8 caracteres, com botão de gerar) ... a pessoa troca a senha no primeiro
acesso"; o modelo de dados (§8) prevê `trocar_senha`. Sem isso, a senha gerada de 8 caracteres
valeria pra sempre, e quem criou a conta conheceria a senha. O cadastro de conferente nem validava
senha (o front pedia 6).

## Decisão

Vamos marcar o usuário, levar a marca no token e barrar num ponto só:

- `Usuario.TrocarSenhaNoProximoAcesso` (migration `AdicionaTrocarSenhaAUsuario`). `CriarConta` e
  `CadastrarConferente` ligam; `RedefinirSenha` (qualquer troca) desliga.
- `EmissorDeTokenJwt` põe a claim `trocar_senha` quando a flag está ligada; `trocarSenha` também vai no
  login e no `/auth/me`, pro front mandar pra tela de troca.
- **Middleware** entre `UseAuthentication` e `UseAuthorization`: token com a claim só chama
  `/auth/me` e `POST /auth/trocar-senha`; o resto recebe 403 `troca_de_senha_obrigatoria`.
- `TrocarSenhaInicial`: confere a senha atual, exige `RegrasDeSenha.EhForte` (RF-01j), grava — o que
  encerra as sessões anteriores — e devolve um token novo sem a claim.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Policy de autorização (`RequireAssertion` sem a claim) | Idiomático no ASP.NET | Teria de ser anexada a cada um dos ~15 grupos (fácil esquecer num novo); a `FallbackPolicy` só vale pra rota **sem** política própria, então não pegaria as nossas | Um ponto só é mais seguro |
| Endpoint filter por grupo | Roda perto do endpoint | Mesmo problema de anexar em cada grupo | Idem |
| Login recusar e só oferecer a troca | Sem token parcial | Exigiria um fluxo de troca anônimo, parecido com a recuperação, sem ganho | Mais código, mesma segurança |
| Bloquear só no front | Simples | Token funcionaria na API | Contraria RNF-04 |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Segurança | ✅ Melhora | Senha inicial vale só até o primeiro acesso; cadastro de conferente passa a validar senha |
| Custo por request | ➖ | Checar uma claim já em memória |

## Consequências

**Positivas** — rota nova autenticada já nasce protegida, sem ninguém lembrar de nada.

**Negativas** — rota que precisar funcionar durante a troca tem de entrar na lista
`RotasLiberadasComTrocaDeSenhaPendente` (`Program.cs`).

**Riscos** — as contas seed (`/dev/seed-e2e`) são resetadas via `RedefinirSenha`, o que desliga a flag;
se isso mudar, o Playwright travaria na tela de troca.

## Referências

- RF-45; ADR-0039; ADR-0020 (recuperação de senha, mesma regra forte); `docs/patterns/autorizacao.md`.
- Testes: `TrocarSenhaInicialTests` e `CriarContaTests` (em `ContasTests.cs`), `CadastrarConferenteTests`,
  `AdministradorIntegracaoTests.ContaNova_TrocaSenhaNoPrimeiroAcesso_EDesativadaPerdeOAcesso`.
