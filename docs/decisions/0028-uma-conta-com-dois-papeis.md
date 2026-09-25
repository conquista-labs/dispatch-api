---
name: adr-0028-uma-conta-com-dois-papeis
description: Uma conta Distribuidora também confere quando existe um Conferente vinculado ao seu UsuarioId — papéis efetivos derivados, sem mudar Usuario.Papel nem o schema; Distribuidora sempre prevalece na visão
metadata:
  type: decision
  status: accepted
---

# ADR-0028: Uma conta com os dois papéis, derivado de `Conferente.UsuarioId`

> `Usuario.Papel` continua um valor único. A capacidade "também é conferente" é **derivada** de
> existir um `Conferente` vinculado ao `UsuarioId`. O JWT carrega uma claim `ClaimTypes.Role` por
> papel efetivo; quando a conta tem Distribuidora, a visão de gestão sempre prevalece.

## Status

Accepted — 2026-09-15 (commit `45e6ff9`). Zero migration.

## Contexto

A distribuidora do cartório também confere às vezes. Com `Papel` exclusivo, precisaria de duas
contas e login/logout para trocar de "chapéu". O schema já permitia: `conferentes.usuario_id` tem FK
+ índice único e nada impedia apontar para um usuário Distribuidora — só faltava o caminho de
escrita. O documento (seção 3) descreve isso como flag "também confere".

## Decisão

- `PapeisEfetivos` (helper interno, Application): `Papel == Conferente` → `[Conferente]`; senão
  `[Papel]` + `Conferente` se houver vínculo.
- `VincularConferenteAUsuario` + `POST /conferentes/vincular` (`{ email, nivel, jornadaHoras }`,
  busca por e-mail — não existe `GET /usuarios` e não precisa só para isso); 404/409.
- `IEmissorDeToken.EmitirToken(..., IReadOnlyCollection<Papel> papeis)`: uma claim de role por papel.
  `Autenticado`/`UsuarioAtual`/`UsuarioResponse` trocam `Papel` por `Papeis`.
- Visões restritas passam a checar `IsInRole(Conferente) && !IsInRole(Distribuidora)`
  (`DashboardEndpoints`, `PUT /protocolos/{id}/observacao`). Nenhuma policy de grupo mudou —
  `RequireRole` já aceita qualquer claim presente.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Duas contas separadas | Nada muda | Login/logout para trocar de papel; histórico dividido | É exatamente o problema relatado |
| `Papel` vira lista/flags no `Usuario` | Explícito no cadastro | Migration de schema; duas fontes para "essa pessoa confere" | O vínculo `Conferente` já é o que alçada, fila e Dashboard usam |
| Coluna `confere` no usuário (como a seção 8 sugere) | Fiel ao modelo sugerido | Duplica a informação do vínculo | Mesma razão — derivar do dado que já existe |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Invasividade | ✅ Melhora | Zero migration, zero mudança de policy |
| Previsibilidade | ⚠️ Atenção | Papéis ficam fixos no JWT até novo login |

## Consequências

**Riscos** — após vincular, a pessoa precisa **logar de novo** para ganhar a claim; a nav do front
mudou de rótulo e quebrou specs e2e que dependiam do texto antigo. Casos de regra de alçada pessoal
de conta combo mordem o motor (ADR-0030).

## Referências

- `docs/patterns/autorizacao.md`.
- `docs/historico.md`, "Uma conta com os dois papéis — distribuidora que também confere".
