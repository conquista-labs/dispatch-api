---
name: adr-0004-remover-conferente-e-soft-delete
description: Remover conferente (RF-25) desativa o usuário e tira da escala, sem apagar a linha, para preservar o histórico de quem conferiu o quê
metadata:
  type: decision
  status: accepted
---

# ADR-0004: Remover conferente é soft delete

> `RemoverConferente` chama `Usuario.Desativar()` e tira o conferente da escala; a linha nunca é
> apagada. Os protocolos atribuídos a ele voltam ao pool (RF-27).

## Status

Accepted — 2026-08-26 (commit `de20a8e`).

## Contexto

RF-25 diz só "cadastrar, editar e **remover** conferente" — não diz se a remoção apaga. Protocolos,
regras de alçada pessoais e o Dashboard referenciam o conferente.

## Decisão

Vamos tratar remoção como desativação: `Usuario.Ativo = false` (setter privado + `Desativar()`) e
`NaEscala = false`. Consequências de leitura: `ListarConferentes` filtra `Ativo` na fonte (achado
em 2026-08-28 — ver histórico "Conferentes, ajustes achados testando a tela de verdade"), em vez de
cada tela lembrar de filtrar.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Hard delete (apagar `Usuario` + `Conferente`) | Banco limpo; "remover" literal | Perde o histórico de quem conferiu o quê; FKs de protocolo/pedido quebrariam ou cascateariam | "Manter histórico de quem conferiu o quê pesou mais que apagar de verdade" |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Auditabilidade | ✅ Melhora | Histórico de conferência e Dashboard continuam íntegros |
| Simplicidade de leitura | ⚠️ Piora | Toda leitura "de conferentes ativos" precisa do filtro |

## Consequências

**Positivas** — mesma filosofia depois reaproveitada para protocolo (ADR-0019).

**Negativas** — um e-mail de conta desativada continua ocupando a unicidade de e-mail.

## Referências

- ADR-0019 (exclusão de protocolo, mesma filosofia).
- `docs/historico.md`, "Cadastro de conferentes (RF-25/RF-26/RF-27)".
