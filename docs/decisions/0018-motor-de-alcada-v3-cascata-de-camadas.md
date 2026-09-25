---
name: adr-0018-motor-de-alcada-v3-cascata-de-camadas
description: Motor de alçada v3 — cascata de 3 camadas (nível, ajuste por equipe, exceção por pessoa) avaliada contra o caso inteiro, permissão Reserva e grupo de tipo como alvo — substitui ADR-0017
metadata:
  type: decision
  status: accepted
---

# ADR-0018: Motor de alçada v3 — cascata de camadas, reserva, grupo como alvo

> O resolvedor passa de "resolve um alvo" para "resolve um caso" (`CasoAlcada(Etapa, TipoAto,
> Guid? EquipeId)`): três camadas avaliadas em ordem — Base por nível → Ajuste por equipe → Exceção
> por pessoa — onde a de baixo sobrescreve a de cima quando tem opinião. Entram `PermissaoRegra.Reserva`
> e `AlvoAlcada.PorGrupoTipoAto`.

## Status

Accepted — 2026-09-01 (commit `14aa605`). Substitui [ADR-0017](0017-motor-de-alcada-v2-lista-fechada-por-dimensao.md).
Continua valendo; complementado (não substituído) por ADR-0024 (alvo equipe+etapa) e ADR-0030
(esse alvo checado antes da cascata, como a Reserva).

## Contexto

Investigando o redesign da aba Alçada (Camadas/Matriz/Testar), a ferramenta interativa
(`Dispatch.dc.html`) mostrou um algoritmo **diferente** do v2 — confirmado lendo a lógica-fonte do
protótipo (`bloqueioPuro`/`decideCamada`/`camadaDe`/`trilhaPura`) e ao vivo via Playwright. O
documento de requisitos (seção 4) ainda descreve o v2.

## Decisão

1. **Reserva** checada antes de qualquer camada: reserva ativa batendo no caso bloqueia todo mundo
   que não é o sujeito dela; não concede acesso sozinha ao próprio sujeito.
2. **Cascata** contra o caso inteiro (etapa + tipo + equipe): `Base por nível` (sujeito Nível,
   qualquer alvo) → `Ajuste por equipe` (pessoa, alvo equipe) → `Exceção por pessoa` (pessoa, outros
   alvos). Dentro da camada: negação que bate vence; senão alçada plena satisfaz; senão cada dimensão
   (equipe/etapa/grupo/tipo) com alguma permissão vira lista fechada. Camada sem regra aplicável não
   opina.
3. `Resolver` (veredito, caminho quente) e `Explicar` (trilha `PassoTrilha` por camada, só para
   leituras explicativas) compartilham a lógica; `DecisaoAlcada.Motivo` é enum
   (`Etapa`/`Tipo`/`Grupo`/`Equipe`/`Geral`/`Reservado`) **sem nome próprio** — o front monta o texto.
4. `AvaliacaoCandidato` vira `(Conferente, DecisaoAlcada)`; `SimularAlcada` + `POST /regras-alcada/testar`.
5. **Ordem do dono: motor primeiro, tela depois**; **não editar** `Dispatch - Requisitos.dc.html` —
   a divergência vive na documentação deste repo.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter v2 ("Model A": escopo binário pessoa-ou-nível por família) | Já em produção, documentado no requisito | Não calcula o que a ferramenta interativa aprovada promete | O protótipo aprovado é a referência operacional |
| Construir a tela nova sobre o back v2 | Entrega visual mais rápida | Mostraria dado que o back não sabe calcular do jeito prometido | Decisão do dono: motor correto e testado primeiro |
| Atualizar o documento de requisitos | Uma fonte da verdade | É gerado por ferramenta de design externa do dono | Mesma decisão do v2: documentar a divergência aqui |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Explicabilidade | ✅ Melhora | Trilha por camada no painel de detalhe e no simulador |
| Performance | ✅ Neutra/Melhora | Uma chamada `Resolver` por candidato (antes três); `Explicar` fora do caminho quente |
| Exatidão de leituras agregadas | ⚠️ Piora | "Quantos tipos alcança" (RF-34) virou aproximação por caso representativo |

## Consequências

**Negativas** — `ObterAlcancePorConferente` fixa um caso representativo por eixo (etapa
PosConferencia + sem equipe; tipo representativo), documentado no código como aproximação. Vários
casos de uso passaram a precisar de `ITipoAtoRepository` (precisam do `TipoAto` inteiro pelo
`.Grupo`).

**Riscos** — "a de baixo vence" não distingue exceção pessoal do *mesmo alvo* de opinião sobre
*qualquer alvo*; mordeu em produção com o alvo equipe+etapa (ADR-0030).

## Referências

- Substitui ADR-0017; complementado por ADR-0024 e ADR-0030.
- `docs/patterns/motor-e-prazos.md` — o algoritmo atual passo a passo.
- `docs/historico.md`, "Motor de alçada v3 — cascata de camadas, reserva, grupo como alvo".
