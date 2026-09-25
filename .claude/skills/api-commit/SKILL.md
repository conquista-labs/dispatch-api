---
name: api-commit
description: Leva uma mudança terminada do dispatch-api da árvore de trabalho pra commits no main — confere que o gate rodou nesta árvore, separa um commit por assunto, escreve a mensagem na voz do repositório e só faz push quando o usuário pedir (push no main é deploy no Render). Use quando o usuário pedir "commita", "fecha o commit", "sobe isso", ou ao terminar uma tarefa que ele pediu pra commitar.
---

# api-commit

Adaptado da metade "commits" da skill `/mr` do swap-benefits-web. Aqui não há branch nem PR: o
dispatch-api commita direto no `main` (GitHub `conquista-labs/dispatch-api`).

> **Push no `main` é deploy.** O Render faz auto-deploy a cada push. Migration nova roda contra o
> Neon de produção no boot. Push só quando o usuário pedir, e avisando o que vai junto (migration,
> mudança de autorização, mudança de contrato que o front ainda não acompanha).

## Pré-condições — confira, não suponha

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

## Push

Só quando o usuário pedir: `git push origin main`, e depois acompanhe o deploy no Render (health
em `/health`) se ele quiser. Force push é bloqueado.

## Relatório

Uma linha por commit (`hash assunto`), o que ficou de fora e por quê, se houve push e o que ele
publicou.
