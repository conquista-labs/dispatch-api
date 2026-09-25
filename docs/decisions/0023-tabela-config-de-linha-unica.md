---
name: adr-0023-tabela-config-de-linha-unica
description: As constantes de operação (faixas do semáforo, limite de simultâneos, janela de correção, memória de descarte, tempo médio por ato, limiares do aprendizado) viram uma tabela de linha única editável via GET/PUT /config
metadata:
  type: decision
  status: accepted
---

# ADR-0023: Tabela `configuracao` de linha única, editável sem redeploy

> 12 constantes que o código citava como "até a tabela config existir" viram a entidade
> `Configuracao` (linha única, colunas tipadas), semeada pela migration e editável por
> `GET`/`PUT /config` (Distribuidora).

## Status

Accepted — 2026-09-03 (commit `2bb189c`). Cache de 5 min acrescentado em 2026-09-14 (commit
`cb536bb`); validação cruzada urgência < atenção em 2026-09-14 (`1a149a0`).

## Contexto

Faixas do semáforo (4h/60min), limite de atos simultâneos (1), janela de correção (15min), dias de
memória do descarte (30), tempo médio por ato (18min) e os 6 limiares do aprendizado estavam
hardcoded e duplicados em vários endpoints/casos de uso. A seção 8 sugere `config(chave, valor)`.

## Decisão

- `Configuracao` (Domain) com `FaixaAtencao`, `FaixaUrgente`, `LimiteDeAtosSimultaneos`,
  `JanelaDeCorrecao`, `DiasDeMemoriaDescarte`, `TempoMedioPorAtoMinutos` e os 6 limiares.
  `DuracaoTipica` (dicionário `TipoPrazo → TimeSpan`) ficou de fora — é estrutura, não escalar.
- Migration `AdicionaConfiguracao` com `InsertData` dos valores antigos;
  `ConfiguracaoRepository.ObterAsync` usa `SingleAsync` (ausência é erro de setup).
- `PUT /config` substitui os 12 valores juntos e **valida com 400** (positivo, 0–1 nos percentuais,
  `faixaUrgente < faixaAtencao`) — não clampa, porque é edição deliberada.
- Api lê via `ObterConfiguracao` (pass-through); casos de uso da Application injetam
  `IConfiguracaoRepository` direto.
- Cache `IMemoryCache` (TTL 5 min) na leitura; `PUT` lê por `ObterParaEdicaoAsync` (sem cache) e
  chama `InvalidarCache()` após salvar.
- Sem tela própria no front ainda ("back primeiro, tela depois").

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Constantes no código (status quo) | Zero infraestrutura | Duplicadas; mudar exige redeploy | Era o item do backlog a fechar |
| `appsettings`/env vars | Nativo do ASP.NET Core | Mudar exige redeploy/reinício; não editável pela distribuidora | O requisito trata como configuração do sistema editável |
| `config(chave, valor)` como na seção 8 | Extensível sem migration | Perde tipo e validação por campo | Linha única tipada valida e lê como objeto (divergência do modelo sugerido; ver gaps) |
| Clampar valor inválido no PUT | Nunca rejeita | Esconde erro de digitação | Diferente de `DefinirPesoDeComplexidadeDoTipoAto`, que clampa por ser valor derivado |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Operabilidade | ✅ Melhora | Mudar o limite de simultâneos teve efeito imediato, sem reiniciar |
| Performance | ✅ Melhora (com cache) | Antes, ~6 leituras da mesma linha por request |
| Consistência | ⚠️ Atenção | Objeto cacheado pertence a um `DbContext` já descartado — nunca editar pelo caminho cacheado |

## Consequências

**Negativas** — modo de operação (Híbrido/Tudo atribuído/Tudo no pool), pesos do score e metas do
Dashboard, que o documento também põe em config, ainda não existem (ver gaps). O corte de 30 dias
dos concluídos (ADR-0026) ficou fora da tabela.

## Referências

- `docs/patterns/ef-core.md` (cache × change tracker).
- `docs/historico.md`, "Tabela `config` (seção 8)", "Segunda rodada da auditoria", "Validação cruzada urgência < atenção".
