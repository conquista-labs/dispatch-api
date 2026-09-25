---
name: adr-0026-corte-de-30-dias-nos-concluidos
description: GET /protocolos/distribuicao limita o bucket concluídos aos últimos 30 dias (constante), com método de repositório dedicado para não cortar o histórico de quem precisa dele inteiro
metadata:
  type: decision
  status: accepted
---

# ADR-0026: Janela de 30 dias no bucket "concluídos" da Distribuição

> Só o bucket `concluidos` (Aprovado+Reprovado) de `GET /protocolos/distribuicao` é limitado a
> `ConcluidoEm >= agora − 30 dias` (`ObterVisaoDistribuicao.DiasHistoricoDeConcluidos`, constante).
> Com `loteImportacaoId`, a janela não se aplica. O corte mora num método novo,
> `ObterParaVisaoDistribuicaoAsync`.

## Status

Accepted — 2026-09-14 (commit `fce4546`).

## Contexto

A auditoria de performance já registrava "`/protocolos/distribuicao` sem paginação cresce sem
limite". Investigação: o crescimento vem de um lugar só — `concluidos` nunca encolhe; os outros
buckets são trabalho em andamento, pequenos por natureza. `Descartado` era buscado sem aparecer em
bucket nenhum.

## Decisão

- Janela de 30 dias só em `concluidos`; `Excluido`/`Descartado` nunca buscados.
- **Método dedicado** `IProtocoloRepository.ObterParaVisaoDistribuicaoAsync(loteImportacaoId,
  concluidosDesde, ct)`: `ObterParaDistribuicaoAsync` também alimenta `GerarSugestoes` (precisa de
  histórico completo para moda/percentil) e `ListarTiposAtoComUso` (contagem de uso real) — cortar
  ali degradaria os dois em silêncio.
- O índice composto `(status, concluido_em)` (criado para o Dashboard) já sustenta a consulta.
- Constante, não 13º campo de `Configuracao`, para entregar rápido.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Paginar `/protocolos/distribuicao` | Resolve qualquer volume | RF-13 trata as visões como "mesma massa de dados"; o front filtra/conta client-side | Maior que o problema; o crescimento vem de um bucket só |
| Aplicar o corte dentro de `ObterParaDistribuicaoAsync` | Nenhum método novo | Corta em silêncio sugestões e contagem de uso de tipos | Mentiria sobre dados para dois consumidores |
| 13º campo em `Configuracao` | Ajustável sem deploy | Mais trabalho agora | Adiado; mesmo caminho que outros limiares tiveram |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Performance | ✅ Melhora | Volume da resposta estável |
| Completude | ⚠️ Piora | Sem override para "ver histórico completo" |

## Consequências

**Negativas** — sem parâmetro de override; valor fixo no código; sem `ORDER BY` nos buckets além de
`pool`/atribuídos (ver gaps). Front não mudou.

## Referências

- `docs/patterns/ef-core.md` (método dedicado em vez de cortar um compartilhado).
- `docs/historico.md`, "Corte de data no bucket \"concluídos\" de GET /protocolos/distribuicao".
