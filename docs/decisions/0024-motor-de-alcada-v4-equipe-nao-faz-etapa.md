---
name: adr-0024-motor-de-alcada-v4-equipe-nao-faz-etapa
description: Motor de alçada v4 — novo alvo PorEquipeEEtapa, restrito a Nega, para expressar "a equipe X não passa pela etapa Y" (ninguém tem alçada)
metadata:
  type: decision
  status: superseded
---

# ADR-0024: Motor de alçada v4 — alvo "equipe não faz etapa"

> `AlvoAlcada.PorEquipeEEtapa(Guid? EquipeId, Etapa Etapa)`, aceito só com `Nega`. A distribuidora
> expressa "ninguém" criando a negação para cada nível (Júnior/Pleno/Sênior). Nesta versão o alvo
> entrava na cascata normal do v3.

## Status

Superseded by [ADR-0030](0030-equipe-e-etapa-absoluto-fora-da-cascata.md) **na posição do alvo**
(dentro da cascata → checagem absoluta antes dela). O alvo, a restrição a `Nega` e a persistência
continuam valendo.

Aceito em 2026-09-11 (commit `53811b2`). Complementa [ADR-0018](0018-motor-de-alcada-v3-cascata-de-camadas.md).

## Contexto

Pedido de um conferente testando em produção pela primeira vez: "a equipe Quinto Andar não passa por
pré-conferência". Confirmado com o dono: significa **ninguém** tem alçada para atos dessa etapa
dessa equipe — o que já existia por pessoa, mas para uma equipe inteira.

## Decisão

- Novo **alvo**, não novo sujeito. `EquipeId` nulo = "sem equipe" (como `PorEquipeDeEscrevente`).
- **Restrito a `Nega`** (400 na Api com `Permite`/`Reserva`): um `Permite` desse alvo entraria na
  lista fechada por dimensão e passaria a bloquear por omissão toda combinação equipe+etapa não
  coberta. `Nega` nunca participa da lista fechada.
- `Dimensao.EquipeEEtapa` fica fora de `OrdemDasDimensoes` (só existe para mapear o motivo);
  `MotivoAlcada.EquipeEEtapa`.
- Persistência reaproveita `alvo_etapa`/`alvo_equipe_id`; só um valor novo no discriminador e um
  branch no `CHECK` (migration `AdicionaAlvoEquipeEEtapaEmRegrasAlcada`, sem coluna nova).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Novo sujeito "todos os níveis" | Uma regra só em vez de três | Mudança maior: `ValePara`, discriminador do sujeito, seletor no front | Três negações por nível é o mesmo trabalho que qualquer "Base por nível" já exige |
| Aceitar `Permite` com esse alvo | Simetria com os outros alvos | Efeito desproporcional pela lista fechada | Pensado só para exceção pontual |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Expressividade | ✅ Melhora | Regra operacional real passa a ser expressável |
| Robustez | ⚠️ Piora (descoberto depois) | Uma exceção pessoal de outro alvo (alçada plena) reabria a negação — ADR-0030 |

## Consequências

**Riscos** — reaproveitar colunas de alvos antigos exige auditar todo `as AlvoAntigo` na resposta
(bug real no `GET /regras-alcada`, ver `docs/patterns/endpoints.md`).

## Referências

- Complementa ADR-0018; revisado por ADR-0030.
- `docs/historico.md`, "Motor de alçada v4 — equipe inteira não passa por uma etapa".
