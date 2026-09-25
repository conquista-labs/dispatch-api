---
name: adr-0030-equipe-e-etapa-absoluto-fora-da-cascata
description: A negação PorEquipeEEtapa passa a ser checada antes da cascata de camadas, com a mesma prioridade absoluta da Reserva — nenhuma exceção pessoal de outro alvo (inclusive alçada plena) a reabre
metadata:
  type: decision
  status: accepted
---

# ADR-0030: "Equipe não faz etapa" é absoluto, checado antes da cascata

> `Resolver`/`Explicar` ganham `NegaEquipeEEtapaQueBloqueia` (mesmo molde de `ReservaQueBloqueia`):
> uma `Nega`/`PorEquipeEEtapa` que valha para o conferente (nível ou pessoa) e bata no caso devolve
> `Negado`/`MotivoAlcada.EquipeEEtapa` direto, sem entrar nas 3 camadas.

## Status

Accepted — 2026-09-16 (commit `d3e3dae`). Substitui [ADR-0024](0024-motor-de-alcada-v4-equipe-nao-faz-etapa.md)
na posição do alvo; a cascata do [ADR-0018](0018-motor-de-alcada-v3-cascata-de-camadas.md) continua
intacta para todos os outros alvos.

## Contexto

Bug em produção: uma conta combo com regra pessoal de alçada plena (`Permite`/`PorTodosOsAtos`)
continuava recebendo atos de pré-conferência da equipe Quinto Andar depois de "Quinto Andar não faz
pré-conferência" (Nega de nível, 3 níveis). Causa confirmada mecanicamente contra o código e os
dados de produção: na cascata, a camada Pessoa opinava sobre **todo** caso (alçada plena) e vencia a
de nível mesmo sem relação com a negação. A regra "a de baixo vence" é correta e obrigatória no caso
geral (exemplo resolvido da seção 4) — só não distingue exceção *do mesmo alvo* de opinião sobre
*qualquer alvo*. O pedido original era "ninguém, independente de quem".

## Decisão

Checagem antecipada, só para este alvo. Como a Api só aceita esse alvo com `Nega`, não existe
exceção legítima do mesmo alvo para ceder. Teste antigo
`EquipeEEtapa_ExcecaoPessoalSobrescreveANegacaoDeNivel` renomeado para
`EquipeEEtapa_NegacaoEhAbsolutaMesmoComExcecaoPessoalDoMesmoAlvo` e invertido (o Domain precisa ser
absoluto por conta própria, não só confiar na validação de borda); novo
`EquipeEEtapa_NegacaoEhAbsolutaMesmoComAlcadaPlenaPessoal` reproduz produção.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter dentro da cascata (ADR-0024) | Um mecanismo só | Alçada plena pessoal reabre "ninguém" | Contradiz o pedido original |
| Fazer a cascata distinguir "exceção do mesmo alvo" para todos os alvos | Resolve a classe inteira | Mexe no comportamento correto e obrigatório do caso geral (exemplo resolvido da seção 4) | Escopo deliberadamente restrito a este alvo |
| Confiar só na validação da Api | Nenhuma mudança no Domain | O cenário de produção não passa pela validação (regras válidas, combinação ruim) | Domain precisa ser absoluto sozinho |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | "Ninguém" significa ninguém |
| Uniformidade do algoritmo | ⚠️ Piora | Duas regras fora da cascata (Reserva e EquipeEEtapa) |

## Referências

- Revisa ADR-0024; preserva ADR-0018.
- `docs/patterns/motor-e-prazos.md`.
- `docs/historico.md`, "Bug real em produção: exceção pessoal de outro alvo reabria \"ninguém, independente de quem\"".
