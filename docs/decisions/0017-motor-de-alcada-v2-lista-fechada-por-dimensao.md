---
name: adr-0017-motor-de-alcada-v2-lista-fechada-por-dimensao
description: Motor de alçada v2 — qualquer Permite numa dimensão vira lista fechada, novos alvos equipe do escrevente e todos os atos (alçada plena), grupo de tipo — substitui ADR-0002
metadata:
  type: decision
  status: superseded
---

# ADR-0017: Motor de alçada v2 — lista fechada por dimensão, equipe, alçada plena

> Se um nível/pessoa tem qualquer regra `Permite` numa dimensão (tipo de ato ou equipe do
> escrevente), isso vira **lista fechada**: tudo fora dela é bloqueado por omissão, mesmo sem
> negação explícita. Entram os alvos `PorEquipeDeEscrevente(Guid?)` e `PorTodosOsAtos` (alçada
> plena), e `TipoAto.Grupo`.

## Status

Superseded by [ADR-0018](0018-motor-de-alcada-v3-cascata-de-camadas.md) — a ferramenta interativa do
protótipo evoluiu para uma cascata de camadas.

Aceito em 2026-08-31 (commit `a3a3cca`). Substitui [ADR-0002](0002-motor-de-alcada-v1-precedencia-por-escopo.md).

## Contexto

Bug relatado em produção: regras `Permite` criadas pela distribuidora não tinham efeito nenhum
(padrão aberto de verdade). O dono atualizou documento e protótipo; confirmado **ao vivo** no
simulador "Testar" do protótipo: Júnior com 5 `Permite` de tipo ficou bloqueado num tipo fora da
lista ("Base por nível · X fora da alçada"); pessoa com 1 `Permite` de equipe ficou bloqueada de
"sem equipe".

## Decisão

Algoritmo por **família de alvo** (Etapa/Tipo/Equipe; `PorTodosOsAtos` conta como Tipo). Dentro do
escopo que "toca" a família (pessoa, se tiver regra ativa na família; senão nível):
1. Nega específica do alvo vence;
2. Permite específica;
3. alçada plena (só família Tipo) — cede à Nega específica do passo 1, por isso roda no mesmo escopo
   (RF-29b: "continua sujeita às restrições explícitas de negação");
4. lista fechada — Permite na família sem cobrir o alvo bloqueia ("fora da alçada");
5. ausência de regra (ou só Negas de outros alvos) = permitido.

Persistência: `RegraAlcadaRegistro` ganha discriminador explícito `AlvoTipoRegistro`; migration com
**backfill manual** do `alvo_tipo` antes de recriar o `CHECK`. Contador de usos (RF-33) derivado de
`Protocolo.RegraAplicadaId`. Decisão consciente de não editar o documento de requisitos (gerado por
ferramenta externa do dono).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter padrão aberto (v1) | Nenhuma mudança | `Permite` sem efeito — o bug relatado | Não é o que a distribuidora espera nem o que o protótipo faz |
| Par nulo/preenchido na persistência do alvo (padrão antigo) | Sem discriminador | Não representa variante sem payload (`PorTodosOsAtos`) nem payload legitimamente nulo (`PorEquipeDeEscrevente(null)`) | Não escala |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Regras Permite passam a restringir |
| Risco de migração | ⚠️ Atenção | CHECK novo sem backfill quebraria qualquer banco com regra — validado contra clone de produção |

## Consequências

**Positivas** — as 5 regras de teste da v1 continuaram passando sem alteração.

## Referências

- Substitui ADR-0002; substituído por ADR-0018.
- `docs/patterns/motor-e-prazos.md`; `docs/patterns/ef-core.md` (backfill antes de CHECK).
- `docs/historico.md`, "Motor de alçada v2 — lista fechada por dimensão, equipe, alçada plena, grupo de tipo".
