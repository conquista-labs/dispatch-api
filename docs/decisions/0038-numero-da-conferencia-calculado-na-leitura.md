---
name: adr-0038-numero-da-conferencia-calculado-na-leitura
description: O número da conferência (RF-24k, "↻ 2ª conferência") é derivado do histórico do mesmo Número na leitura, não gravado numa coluna rodada; o motivo da não aprovação é a Observação da linha reprovada
metadata:
  type: decision
  status: accepted
---

# ADR-0038: Número da conferência calculado na leitura

> `NumeroDaConferencia = 1 + linhas anteriores do mesmo Número, na mesma etapa, que terminaram
> Reprovadas`, calculado a cada leitura — sem coluna `rodada`. O "motivo da não aprovação" do
> histórico é a Observação daquela linha.

## Status

`Accepted — 2026-09-25`

## Contexto

RF-24k (requisitos v2): "Protocolo que volta depois de não aprovado recebe a tag neutra
'↻ 2ª conferência' (3ª, 4ª…) em cards, listas e detalhe. O histórico do detalhe lista cada
conferência anterior com o motivo da não aprovação." O modelo de dados sugerido (§8) traz uma coluna
`rodada` que "começa em 1 e soma 1 a cada retorno após não aprovado".

No Dispatch, cada volta já é uma **linha própria** de `Protocolo` com o mesmo `Numero` (ADR-0006:
`Numero` nunca é único; a reimportação depois da linha de corte cria a linha nova). Então a rodada já
está implícita no histórico — a pergunta era se vale materializá-la.

O "Não aprovar" nunca pediu motivo (`ConcluirConferenciaRequest(bool Aprovado)`), e no protótipo os
motivos do histórico são texto fixo de exemplo.

## Decisão

Vamos calcular o número da conferência na leitura, a partir do histórico do mesmo Número, porque um
valor gravado ficaria desatualizado em vários caminhos que já existem:

- `ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico)` (Domain, puro) sobre o recorte
  leve `RegistroDoNumero(Id, Numero, Etapa, AndamentoEm, Status)`. Conta só `Reprovado`, só a mesma
  etapa, só andamento **estritamente anterior**, nunca a própria linha.
- `IProtocoloRepository.ObterRegistrosPorNumerosAsync` projeta o recorte (sem carregar ciclos,
  pausas e ajustes); `NumeroDaConferenciaEmLote` faz **uma query por listagem** pelos números
  distintos (índice em `protocolos.numero` já existe). `ObterDetalheProtocolo` reaproveita as linhas
  que já carregava pro histórico.
- `ProtocoloResumo`, `DetalheProtocoloResponse` e `HistoricoConferenciaResponse` ganham
  `NumeroDaConferencia`; `HistoricoConferenciaResponse` ganha `Observacao`.
- **Motivo da não aprovação = Observação da linha reprovada** (decisão do dono). Como cada rodada é
  uma linha, a observação dela é a nota daquela conferência. Sem campo novo nem mudança no fluxo de
  reprovar.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Coluna `rodada` gravada na criação da linha (§8) | Leitura trivial, sem query extra | Desatualiza com: edição de etapa (`EditarProtocoloManual`), excluir/restaurar uma linha anterior, correção Reprovado→Aprovado (`CorrigirResultado`) e dois do mesmo Número no mesmo lote (`ImportarLote` lê o histórico uma vez, antes do laço). Exige migration com backfill | Cada um desses caminhos precisaria recalcular as linhas seguintes; calcular na leitura é sempre correto |
| Campo `motivo` obrigatório/opcional ao reprovar | Motivo explícito e separado da observação livre | Migration, mudança na API de concluir e na UI do card | Dono escolheu a Observação — a mesma informação, sem mudar o fluxo |
| Contar `CiclosAnteriores` do próprio protocolo | Já está no agregado | Pausar também fecha um ciclo (ADR-0033), e reabrir não é "voltar depois de não aprovado" | Mede outra coisa |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Correção | ✅ Melhora | Nunca desatualiza — é função do estado atual do histórico |
| Desempenho | ⚠️ Piora (pouco) | Uma query projetada a mais por listagem, 1–3 linhas por número, indexada |
| Simplicidade | ✅ Melhora | Sem migration, sem backfill, sem recálculo em cascata |

## Consequências

**Positivas** — restaurar uma linha excluída ou corrigir o resultado reflete na hora; o Domain tem a
regra isolada e testada (`NumeroDaConferenciaTests`).

**Negativas** — a Observação pode ser editada depois de concluir, então o "motivo" não é imutável
(aceito: é a nota daquela rodada). Quem quiser a rodada fora da API (relatório SQL) precisa refazer a
conta.

**Riscos** — número divergente entre telas se algum endpoint montar `ProtocoloResumo` sem o
dicionário (o `ParaResumo` exige o parâmetro, sem default, pra isso quebrar na compilação);
`Numero` com formatação diferente entre importações contaria como outro número — hoje o `Numero`
chega cru do relatório, o mesmo critério que a continuidade (ADR-0022) já usa.

## Referências

- RF-24k; `docs/gaps-requisitos.md` §22.
- ADR-0006 (Numero não único), ADR-0022 (continuidade — mesma família de regra, mesmo arquivo),
  ADR-0033 (pausa fecha ciclo).
- `docs/patterns/motor-e-prazos.md` → "Número da conferência".
- Testes: `NumeroDaConferenciaTests` (Domain), casos em `ObterMinhaFilaTests`,
  `ObterVisaoDistribuicaoTests`, `ObterDetalheProtocoloTests`, e `NumeroDaConferenciaIntegracaoTests`.
