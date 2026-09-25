---
name: api-adr
description: Registra uma decisão de arquitetura/produto como ADR em docs/decisions/ seguindo o template e a numeração deste repo, incluindo supersessão de ADR anterior. Use quando uma mudança escolher entre alternativas reais (biblioteca, padrão, modelagem, regra de negócio interpretada, infraestrutura), quando o usuário pedir "registra essa decisão", "cria um ADR", ou quando uma decisão aceita estiver sendo revertida.
---

# adr

Cria um ADR novo em `docs/decisions/`, no formato que o repositório já usa — não invente outro.

Leia antes: `docs/decisions/TEMPLATE.md` (estrutura) e pelo menos um ADR existente como exemplo do
nível de detalhe — `docs/decisions/0022-continuidade-de-conferencia.md` (decisão de negócio com
alternativas do dono) ou `docs/decisions/0030-equipe-e-etapa-absoluto-fora-da-cascata.md` (revisão de
decisão anterior).

## Quando isto é um ADR — e quando não é

ADR é para uma decisão que **escolheu um caminho entre alternativas reais** com custos diferentes:
biblioteca ou ferramenta, padrão de código, modelagem de dado, interpretação de um requisito ambíguo,
comportamento pedido pelo dono que diverge do documento de requisitos, infraestrutura/deploy.

**Não** é ADR:
- adição pequena sem alternativa que valha pesar → entrada em `docs/historico.md`;
- lição aprendida / armadilha / como fazer → o pattern correspondente em `docs/patterns/` (ou a skill);
- lacuna em relação ao documento de requisitos → `docs/gaps-requisitos.md`.

Se a situação não pede ADR, **diga isso** em vez de criar um só porque a skill foi chamada.

## Entrada

`$ARGUMENTS` é uma descrição curta da decisão. Se vier vazio ou vago, pergunte: o que foi decidido,
que alternativas foram de fato consideradas e por que caíram, o que continua em aberto ou arriscado.
**Não fabrique alternativas** que não foram discutidas — se só existiu uma opção, provavelmente não é
ADR (ver acima). Se o dono decidiu algo na conversa, a alternativa que ele recusou é uma alternativa
real; cite-a como ele disse.

## Passo 1 — número

Liste `docs/decisions/[0-9][0-9][0-9][0-9]-*.md` (ignorando `TEMPLATE.md`), pegue o maior prefixo e
use o próximo, com 4 dígitos. Nunca reutilize nem renumere um ADR existente.

```bash
ls docs/decisions | grep -E '^[0-9]{4}-' | sort | tail -1
```

Nome do arquivo: `NNNN-titulo-curto-em-kebab-case.md`, em português, sem acento.

## Passo 2 — preencher o template

Copie **todas** as seções do `TEMPLATE.md` (Status, Contexto, Decisão, Alternativas consideradas,
Characteristics impactadas, Consequências, Referências). Tudo em **português**; identificadores de
código como estão no código. Frontmatter: `name: adr-NNNN-titulo-curto`, `description` (uma frase
objetiva), `metadata.type: decision`, `metadata.status`.

- `status: proposed` enquanto não estiver implementado e verificado.
- `status: accepted` só quando está no código **e** verificado do jeito que este repo verifica
  (`dotnet build`, `dotnet test`, e `dotnet run` + chamada real quando toca endpoint/DI/auth — skill
  `gate`). No corpo: `Accepted — AAAA-MM-DD (commit abc1234)`.
- Contexto cita RF/RNF do documento de requisitos quando houver, e diz se a decisão diverge dele.
- Tabela de alternativas com prós/contras reais para cada linha; menos de duas alternativas reais →
  volte à seção "Quando isto é um ADR".

## Passo 3 — supersessão (quando revisa uma decisão anterior)

ADR aceito é **imutável**: não se edita para mudar de ideia. Quando a decisão nova substitui (toda ou
parte de) uma anterior:

1. O ADR novo diz no Status: `Substitui ADR-00XX` (e, se for parcial, qual parte).
2. No ADR antigo, altere **só** o campo Status (e o `metadata.status` para `superseded`):
   `Superseded by [ADR-NNNN](NNNN-...md) — <uma linha do motivo>`. Nada mais no corpo muda.
3. Se a nova decisão só **complementa** a anterior (não a invalida), não use supersessão: cite a
   anterior nas Referências e diga "complementa".

Exemplos no repo: motor de alçada 0002 → 0017 → 0018 (+0024 → 0030); deploy 0014 → 0015; tipo
desconhecido 0008 → 0012.

## Passo 4 — ligações

- Referências do ADR novo: commit(s), a entrada correspondente de `docs/historico.md`, patterns
  relacionados, itens de `docs/gaps-requisitos.md` (ex.: "gaps §12").
- Nos patterns afetados (`docs/patterns/*.md`), acrescente o link para o ADR novo e corrija qualquer
  trecho que descreva o comportamento antigo.
- Se o ADR fecha ou cria um gap, atualize `docs/gaps-requisitos.md` (marque o item, nunca renumere).
- **Não** acrescente seção de changelog ao `CLAUDE.md`. Só mexa no CLAUDE.md se a decisão mudar algo
  da lista "Armadilhas que toda sessão precisa saber" ou do índice.

## Passo 5 — reporte

Diga o número/arquivo, o status, a decisão numa frase, e quais ADRs/patterns/gaps foram tocados. Lembre
que, depois de aceito, este ADR não é editado — mudança de ideia vira um ADR novo que o substitui.
