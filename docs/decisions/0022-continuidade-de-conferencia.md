---
name: adr-0022-continuidade-de-conferencia
description: Protocolo reprovado que reaparece (reimportação ou cadastro manual) na mesma etapa vai direto para quem fez a primeira conferência; se essa pessoa não for mais elegível, vira exceção
metadata:
  type: decision
  status: accepted
---

# ADR-0022: Continuidade de conferência

> Quando um protocolo Reprovado reaparece na mesma etapa, ele é atribuído direto ao conferente da
> **primeira** conferência dele (linha mais antiga com essa etapa e um dono), sem passar por
> urgência/carga/pool. Se essa pessoa não é mais elegível, vira Exceção — não recai no motor normal.

## Status

Accepted — 2026-09-03 (commit `2bb189c`). Pedido do dono; **não é RF numerado** nem está no
protótipo aprovado.

## Contexto

O documento (seção 11) deixava em aberto "quando um ato não aprovado volta corrigido, se entra de
novo pelo pool ou vai direto a quem conferiu da primeira vez". Não existia código que correlacionasse
linhas do mesmo `Numero` (não único, ADR-0006).

## Decisão

- `ResolvedorDeContinuidade` (Domain, função pura): dado o histórico de um número e a etapa atual,
  devolve o `DonoId` da linha mais antiga com essa etapa e um dono.
- `MotorDistribuicao.Distribuir(..., Guid? donoDaPrimeiraConferenciaId = null)`: se o dono anterior
  está entre os elegíveis → `Atribuido` com **`RegraAplicada` nula de propósito** (quem decidiu foi
  a continuidade, não uma regra); senão `Excecao("conferente da primeira conferência não está mais
  disponível")`. Não mascara os early-exits de tipo desconhecido/desativado.
- `IProtocoloRepository.ObterPorNumerosAsync` (busca única antes do laço de `ImportarLote`) e
  `ObterDetalheProtocolo.HistoricoConferencias`.
- **Cadastro manual** segue o mesmo fluxo: `ResolvedorDeContinuidade.PodeRecriar` só bloqueia se
  algum registro do número ainda está em uso (`Pool`, `Atribuido`, `Conferindo`, `Excecao`,
  `Aprovado`); libera com `Reprovado`, `Descartado` ou `Excluido`. `SimularProtocoloManual`
  acompanha. `ExisteComNumeroAsync` foi removido.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Rodar o motor do zero (comportamento anterior) | Nenhuma regra nova | Pode cair com outra pessoa, que não conhece o ato | Pedido do dono |
| Dono da conferência **mais recente** | Reflete quem viu por último | Pedido foi explícito: a primeira | Decisão do dono |
| Dono inelegível → recair no motor normal | Nunca gera exceção | Esconde da distribuidora que a continuidade falhou | Decidido com o dono: vira Exceção |
| Manter bloqueio de número duplicado no cadastro manual | Protege contra cadastro repetido por engano | Impediria a continuidade no manual | `PodeRecriar` protege só enquanto o número está em uso |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Qualidade operacional | ✅ Melhora | Quem já conhece o ato confere de novo |
| Auditabilidade | ⚠️ Atenção | `RegraAplicadaId` nulo tanto para decisão humana quanto para continuidade |

## Consequências

**Riscos** — `Numero` precisa de índice (não único) porque passou a ser filtrado a cada importação e
a cada abertura de detalhe (feito em 2026-09-14).

## Referências

- ADR-0006 (linha de corte).
- `docs/historico.md`, "Continuidade de conferência (pedido do dono, não é RF numerado)".
