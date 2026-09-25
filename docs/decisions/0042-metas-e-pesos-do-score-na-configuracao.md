---
name: adr-0042-metas-e-pesos-do-score-na-configuracao
description: As metas do Dashboard (RF-42b) e os pesos do score (RF-46) viram colunas da linha única de Configuracao, validadas no Domain e aplicadas na leitura, sem histórico por vigência
metadata:
  type: decision
  status: accepted
---

# ADR-0042: Metas e pesos do score na Configuração, aplicados na leitura

> `metaNoPrazo`/`metaAprovadoNaPrimeira` (frações 0,50–1,00) e `pesoVolume`/`pesoPrazo`/`pesoQualidade`/
> `pesoComplexidade` (inteiros ≥ 0 que somam 100) são 6 colunas novas da linha única `configuracao`
> (ADR-0023). O `GET /dashboard` calcula o score com os pesos **de agora**, para qualquer período
> consultado — não há histórico de pesos.

## Status

`Accepted — 2026-09-25`

## Contexto

RF-46: "o score é 40% volume + 30% prazo + 20% qualidade + 10% complexidade, **com pesos
configuráveis**". RF-42b: "Dentro do prazo" e "Aprovados na 1ª" têm barra com a meta marcada (padrão 95%
e 90%, **configuráveis em Configuração do sistema**). A seção 8 do requisito lista "pesos do score" em
`config(chave, valor)`.

Até aqui os pesos eram literais em `ObterDashboard` (`40.0 * volume / maxVolume`...) e as metas não
existiam no back (gaps §34 e §36). O dono decidiu em 25/09/2026 (`PLANO-dashboard-v2.md`, "Decisões do
dono", item 4): **só o Administrador edita** metas e pesos, os **pesos somam 100**, e **só a gestão vê a
meta** (o conferente não vê meta nos próprios KPIs).

O score não é gravado em lugar nenhum: é calculado a cada `GET /dashboard` a partir dos protocolos
concluídos no período (ADR-0011, ADR-0041). Ele alimenta a bonificação, que é fechada fora do sistema.

## Decisão

Vamos guardar as metas e os pesos como colunas tipadas de `Configuracao` e aplicá-los na leitura, porque
a tabela de linha única já é onde mora "configuração do sistema editável sem redeploy", já tem cache e
invalidação, e o score já é derivado na leitura.

- **Domain**: `MetasDoDashboard` e `PesosDoScore` (records em `Indicadores/`) com `Validar()` → motivo
  ou `null`, e `Padrao` (0,95/0,90 e 40/30/20/10). `Configuracao` ganha as 6 propriedades (colunas
  planas, inicializadas com o padrão, fora do construtor), as leituras `Metas`/`Pesos` e
  `DefinirMetasEPesos`, que valida de novo e lança se inválido (guarda do invariante).
- **Pesos somam exatamente 100** (decisão do dono): cada peso é o máximo da sua parcela, o score continua
  em 0–100 e as faixas fixas 85/70 continuam significando a mesma coisa. Zero desliga uma parcela;
  negativo é recusado.
- **Application**: `AtualizarConfiguracao` recebe os 6 como opcionais (`null` = mantém o atual, campo a
  campo), resolve contra a linha, valida o conjunto e devolve `MetasOuPesosInvalidos(motivo)` (→ 400)
  antes de mudar qualquer um dos 18 valores. Opcionais porque o front anterior faz o `PUT /config` sem
  eles, e não pode zerar os pesos entre o deploy da API e o do front.
- **`ObterDashboard`** lê a configuração pelo `IConfiguracaoRepository.ObterAsync` cacheado e devolve
  `metas` só na visão de gestão e `pesos` só a quem vê score (Administrador; o próprio conferente na
  visão restrita) — `null` para a distribuidora (ADR-0039).
- **Migration** `AdicionaMetasEPesosEmConfiguracao`: colunas `NOT NULL` com `DEFAULT` igual aos valores
  que eram constantes (a linha existente continua com o mesmo score). O default mora só na migration,
  não em `HasDefaultValue` (evita o EF omitir no INSERT um peso 0 e o banco trocar pelo default).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Manter as constantes em `ObterDashboard` (o que existia) | Zero migration; score estável no tempo | Contraria RF-46/RF-42b ("configuráveis"); mudar peso exige deploy | O requisito e o dono pedem configurável |
| `config(chave, valor)` como na seção 8 do requisito | Chave nova sem migration | Tudo vira texto; validação e tipo fora do banco; já recusado no ADR-0023 | Mantém o padrão tipado do ADR-0023 |
| Pesos com **vigência** (tabela `pesos_score` com `vigente_desde`; cada período usa os pesos em vigor na época) | Mudar peso não reescreve o score de meses já fechados | Tabela e busca por período; período que atravessa uma troca precisaria de regra (ponderar? usar o do fim?); sem pedido do dono | Custo sem demanda: a bonificação é fechada fora do sistema no fim do mês; o risco fica registrado abaixo |
| Pesos livres (qualquer soma), score normalizado por `soma dos pesos` | Admin não precisa fechar a conta em 100 | Parcela "32,4 / 40" deixa de ser o que soma no score; mais uma regra pra explicar | Dono decidiu "pesos somam 100" |
| Owned type (`OwnsOne`) para `MetasDoDashboard`/`PesosDoScore` | Tipos de valor mapeados direto | Destoa do resto da tabela (tudo coluna plana); mais armadilha de mapeamento | Colunas planas + leitura calculada dão o mesmo tipo de valor sem o owned type |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Configurabilidade | ✅ Melhora | Administrador ajusta metas e pesos sem deploy (RF-42b, RF-46) |
| Compatibilidade | ✅ Mantida | Os 6 campos são opcionais no `PUT`; migration com o mesmo valor de antes |
| Auditabilidade | ⚠️ Piora | O score de um período passado muda se os pesos mudarem depois; não há registro de quem trocou nem quando |
| Desempenho | ➖ Neutro | Configuração vem do cache (`IMemoryCache`, invalidado no `PUT`) |

## Consequências

**Positivas** — o front recebe o máximo de cada parcela (`pesos`) em vez de supor 40/30/20/10; a barra
de meta tem valor do back; a regra (faixa das metas, soma 100) vive e é testada no Domain.

**Negativas** — consultar o Dashboard de um mês fechado depois de trocar os pesos mostra o score
recalculado com os pesos novos, não o que valeu na época.

**Riscos** — se a bonificação passar a ser fechada **dentro** do sistema, ou se o dono pedir o score
"como estava", a vigência (alternativa 3) volta à mesa como ADR que complementa este. A troca de pesos
não gera evento de auditoria (a seção 8 pede auditoria das transições de protocolo, não de
configuração).

## Referências

- RF-42b, RF-45, RF-46; seção 8 (`config`); `PLANO-dashboard-v2.md` (raiz do workspace), "Decisões do
  dono (25/09/2026)" item 4 e "Contrato da fatia 2".
- `docs/patterns/indicadores-e-aprendizado.md` → "Dashboard"; `docs/historico.md`, entrada de
  2026-09-25 "Metas e pesos do score configuráveis".
- `docs/gaps-requisitos.md` §28, §34, §36.
- Complementa ADR-0023 (tabela `config` de linha única) e ADR-0039 (pesos seguem a mesma regra de quem
  vê score).
