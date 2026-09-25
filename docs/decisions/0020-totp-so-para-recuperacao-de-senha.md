---
name: adr-0020-totp-so-para-recuperacao-de-senha
description: TOTP real (RFC 6238) registrado voluntariamente serve só como prova de identidade na recuperação de senha em 3 etapas, sem e-mail/SMS; login normal continua só e-mail+senha
metadata:
  type: decision
  status: accepted
---

# ADR-0020: TOTP como prova de identidade só na recuperação de senha, sem mensageria

> O autenticador TOTP não é 2FA obrigatório no login: é um fluxo autoiniciado cuja única função é
> provar identidade na recuperação de senha (e-mail → código TOTP → nova senha), sem nenhum envio de
> e-mail ou SMS. Trocar a senha encerra todas as sessões (`SessoesValidasApartirDe`).

## Status

Accepted — 2026-09-01 (commit `226fb7d`). RF-01m e RF-01n ficaram de fora por decisão explícita do
dono (ver `docs/gaps-requisitos.md`).

## Contexto

RF-01a-l e RNF-14: nada pode depender de serviço pago, domínio de envio ou infraestrutura do
cartório. Relendo o protótipo: o "Entrar" autentica direto, sem código; "Registrar autenticador" é
um link separado já na tela de login.

## Decisão

- **TOTP de verdade** (`TotpComOtpNet`, lib `Otp.NET`): janela de ±1 bloco (30s), bloqueia reuso
  (contador do bloco aceito precisa ser maior que o último). Diferente do protótipo, que aceitava
  qualquer código ≠ "000000".
- `UsuarioTotp` 1:1 com `Usuario` (chave = `UsuarioId`): segredo **cifrado** (RNF-15, `CifradorAes`
  AES-CBC com IV aleatório, chave em `Totp:ChaveDeCifragem` fora do banco), tentativas/bloqueio
  (RF-01i: 5 erradas → 15 min), hash do token de recuperação.
- **Token de recuperação opaco** `{usuarioId:N}.{aleatório}`, não JWT de sessão; guardado só como
  hash. O `usuarioId` vai embutido porque o `PasswordHasher` salga a cada chamada — não dá para
  consultar `WHERE hash = @candidato`.
- **Anti-enumeração (RF-01h)**: `IniciarRecuperacaoSenha` sempre 200; `ValidarCodigoRecuperacao`
  devolve o mesmo `CodigoInvalido` para e-mail inexistente, TOTP não confirmado e código errado.
- **RF-01k**: `Usuario.SessoesValidasApartirDe` (truncado ao segundo) + `OnTokenValidated` rejeitando
  token com `IssuedAt` anterior — 1 consulta a mais por request autenticado, aceitável para o
  volume. Atos **em conferência** do usuário voltam ao pool.
- `RegrasDeSenha` (Domain): ≥12 caracteres, não começar com `senha|123|cartorio|dispatch`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| TOTP como 2FA em todo login | Mais segurança no login | Não é o que o protótipo aprovado mostra; mais atrito diário | O protótipo autentica direto |
| Recuperação por e-mail/SMS | Fluxo conhecido | Custo recorrente, SMTP do cliente, entregabilidade, e-mail aberto em balcão compartilhado | Proibido pelo requisito (RNF-14) |
| JWT como token de recuperação | Reaproveita o emissor | Mistura sessão com recuperação; não é de uso único | Token opaco de uso único com hash |
| Buscar o token de recuperação por hash | Token sem id embutido | Hash salgado nunca se repete | Impossível com `PasswordHasher` |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Segurança | ✅ Melhora | Recuperação sem canal interceptável; sessões encerradas na troca |
| Custo | ✅ Melhora | Zero serviço externo |
| Performance | ⚠️ Leve custo | Uma consulta de `Usuario` por request autenticado |

## Consequências

**Negativas** — quem não registrou autenticador não se recupera sozinho, e RF-01m/n (liberação pela
distribuidora, códigos de emergência) não existem ainda.

**Riscos** — mudança na validação do JWT é invisível para `build`/`test`: dois bugs só apareceram
com login real (cast para `JsonWebToken`, claim `iat` ausente). Ver `docs/patterns/autorizacao.md`.

## Referências

- `docs/patterns/autorizacao.md`.
- `docs/historico.md`, "TOTP e recuperação de senha, caminho feliz (RF-01a a RF-01l)".
