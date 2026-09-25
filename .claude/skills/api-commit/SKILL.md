---
name: api-commit
description: Leva uma mudança terminada do dispatch-api da árvore de trabalho até um pull request aberto — branch, gate rodado nesta árvore, um commit por assunto na voz do repositório, push e `gh pr create`; o merge (que é deploy no Render) só quando o usuário pedir. Use quando o usuário pedir "commita", "abre o PR", "sobe isso", ou ao terminar uma tarefa que ele pediu pra entregar.
---

# api-commit

Adaptado da skill `/mr` do swap-benefits-web. **Toda mudança sai por branch + pull request** no
GitHub (`conquista-labs/dispatch-api`) — decisão do dono de 25/09/2026. Commit e push no `main` são
bloqueados pelo hook `guard-git.py`.

> **Merge no `main` é deploy.** O Render faz auto-deploy a cada push no `main`. A migration que o
> código novo precisa tem de estar aplicada no Neon **antes** do merge (skill `prod-ops`). O PR diz
> o que vai junto (migration, mudança de autorização, mudança de contrato que o front ainda não
> acompanha).

## Pré-condições — confira, não suponha

0. **Está numa branch, não no `main`.** Senão: `git switch -c <tipo>/<assunto-em-kebab-case>`
   (`feat/`, `fix/`, `chore/`, `docs/`, `test/`), em português.
1. **O `api-gate` rodou nesta árvore exata** (build + unidade + integração com Testcontainers). Se
   algo mudou depois, rode de novo. Nunca escreva no commit uma verificação que não rodou.
2. `git status --short` mostra só o que você quis mudar. Pode haver outra sessão no mesmo checkout:
   **nunca `git add -A` nem `git add .`** — adicione caminho por caminho. Artefatos (`coveragereport/`,
   `test-results/`, `bin/`, `obj/`) não entram.
3. Migration: os três arquivos (`<timestamp>_<Nome>.cs`, `.Designer.cs` e o
   `DispatchDbContextModelSnapshot.cs`) vão no mesmo commit da mudança de modelo.
4. Mudança de contrato que o front consome: o commit do front (skill `web-commit`) sai junto, na
   ordem de deploy combinada (normalmente API primeiro, compatível).

## Um commit por assunto

Correção de bug e feature nova são dois commits. Quando um arquivo cobre dois assuntos (um
`Fakes.cs` compartilhado), ponha no commit a que ele mais pertence e diga no corpo.

## Voz

- **Assunto**: `tipo(escopo): o que mudou`, em português, sem ponto final, até ~72 caracteres.
  `tipo` ∈ `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`. `escopo` é o agregado ou a
  área (`protocolo`, `alcada`, `equipe`, `auth`, `dashboard`, `conta`...).
- **Corpo**: o **porquê** e o mecanismo ("o change tracker perdia a entidade depois do refetch,
  então o `SaveChanges` não via a alteração"). Cite arquivo/método quando eles carregam o
  argumento. Conceito novo de .NET que apareceu: explique em uma frase — o dono está aprendendo a
  stack. Decisão que merece registro vira ADR (skill `api-adr`).
- **Rodapé**: a linha `Co-Authored-By` que a sessão fornece no lembrete de atribuição — copie como
  veio.

## Mecânica

- Mensagem de mais de uma linha: arquivo no scratchpad + `git commit -F <arquivo>`.
- `git add <caminhos>` e `git commit` em chamadas **separadas**.
- Nunca `--no-verify` nem `--amend` em commit que já foi pro remoto (o hook `guard-git.py` bloqueia
  os destrutivos).

## Push e PR

Depois dos commits, sem perguntar de novo (o usuário já pediu a entrega):

1. `git push -u origin <branch>` (push de branch não publica — o Render só observa o `main`).
2. `gh pr create --base main --head <branch> --title "<tipo(escopo): resumo>" --body-file <arquivo>`.
   Corpo com **O que entra**, **Decisões que valem leitura**, **Armadilhas** (sempre: "merge é
   deploy" e o que isso publica), **Verificação** (números exatos) e **Fora de escopo**; termina
   com a linha de atribuição de PR que a sessão fornece.

**Merge só quando o usuário pedir** ("pode mergear", "vamos subir"): `gh pr merge <n> --merge`,
depois acompanhe o deploy (skill `prod-ops` → "Conferir um deploy"; o OpenAPI de produção mostra
campo/rota nova quando a versão entrou), `git switch main && git pull --ff-only` e
`git branch -d <branch>`. Force push é bloqueado.

## Relatório

Uma linha por commit (`hash assunto`), o link do PR, o que ficou de fora e por quê.
