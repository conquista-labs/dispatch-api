#!/usr/bin/env python3
"""PreToolUse (Bash): recusa comando git que destrói trabalho ou escreve fora do Dispatch.

Adaptado do `block-protected-branch.py` do swap-benefits-web. Aqui não existe branch protegida
(os dois repositórios commitam direto no `main`), então as regras são sobre não perder trabalho:

1. **Escrita git fora do Dispatch.** Só `dispatch-web` e `dispatch-api` são nossos. Outros
   repositórios da máquina (os da Swap, por exemplo) podem ser lidos (`log`, `show`, `diff`,
   `fetch`), nunca escritos.
2. **Operação destrutiva dentro do Dispatch.** `reset --hard`, `clean -f`, `push --force`,
   descartar a árvore inteira (`checkout .`, `restore .`), `branch -D` e pular os hooks do husky
   (`--no-verify`). Mais de uma sessão pode estar trabalhando no mesmo checkout (há worktrees em
   `.claude/worktrees/`), e essas operações apagam o que outra deixou sem commitar. Se o usuário
   pediu isso explicitamente, ele roda o comando com `!` no prompt.

O comando é **tokenizado por cláusula** (`shlex`), não casado por regex: `git show HEAD:x/push.ts`
não é push, e um `--force` dentro da mensagem de um commit não é push forçado.

Exit 2 bloqueia; o stderr volta pro modelo como motivo. O que não der pra interpretar passa — um
hook que chuta erraria mais do que um que se abstém.
"""

import json
import os
import re
import shlex
import sys

# Repositórios do Dispatch: irmãos na pasta do workspace (`dispatch/`).
REPOS_DO_DISPATCH = {"dispatch-web", "dispatch-api"}

# Subcomandos que mexem na árvore ou nos refs. Fora do Dispatch, todos são proibidos.
ESCRITAS = {
    "add", "am", "apply", "checkout", "cherry-pick", "clean", "commit", "merge", "mv", "pull",
    "push", "rebase", "reset", "restore", "revert", "rm", "stash", "switch", "worktree",
}
# `git branch`/`git tag` só escrevem com uma destas flags (sem elas é leitura: `--show-current`).
ESCRITAS_CONDICIONAIS = {"branch", "tag"}
FLAGS_QUE_ESCREVEM = {"-d", "-D", "-m", "-M", "-f", "--delete", "--move", "--force", "--copy", "-c"}

OPCOES_GLOBAIS_COM_VALOR = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--exec-path"}
SEPARADORES = re.compile(r"&&|\|\||[;|\n]")


def raiz_do_repo() -> str:
    """O repositório deste hook, achado pelo próprio arquivo (`<repo>/.claude/hooks/x.py`).

    `CLAUDE_PROJECT_DIR` não serve: quando a sessão abre em `dispatch/` (a pasta do workspace),
    ele aponta pra lá, não pra este repositório.
    """
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def chamadas_git(comando: str, cwd: str):
    """Gera `(pasta_efetiva, subcomando, tokens)` pra cada `git` do comando.

    Acompanha `cd <pasta>` entre cláusulas (`cd dispatch-api && git mv ...`): sem isso a guarda
    julgava o `git` pela pasta onde o shell estava antes do `cd` — falso positivo real, visto no
    primeiro uso.
    """
    atual = cwd
    for clausula in SEPARADORES.split(comando):
        try:
            tokens = shlex.split(clausula)
        except ValueError:
            continue  # aspas desbalanceadas: não é nosso interpretar

        if tokens and tokens[0] == "cd":
            destino = tokens[1] if len(tokens) > 1 else os.path.expanduser("~")
            atual = os.path.abspath(os.path.join(atual, os.path.expanduser(destino)))
            continue

        for indice, token in enumerate(tokens):
            if os.path.basename(token) != "git":
                continue
            repo = None
            resto = tokens[indice + 1 :]
            pos = 0
            while pos < len(resto):
                candidato = resto[pos]
                if candidato in OPCOES_GLOBAIS_COM_VALOR:
                    if candidato == "-C" and pos + 1 < len(resto):
                        repo = resto[pos + 1]
                    pos += 2
                elif candidato.startswith("-"):
                    pos += 1
                else:
                    pasta = os.path.abspath(os.path.join(atual, repo)) if repo else atual
                    yield pasta, candidato, resto[pos:]
                    break
            break


def motivo_destrutivo(subcomando: str, tokens: list[str]) -> str | None:
    args = tokens[1:]
    if subcomando == "reset" and "--hard" in args:
        return "`git reset --hard` descarta alterações não commitadas"
    flags_curtas = "".join(a[1:] for a in args if a.startswith("-") and not a.startswith("--"))
    if subcomando == "clean" and ("f" in flags_curtas or "--force" in args):
        return "`git clean -f` apaga arquivos não versionados"
    if subcomando == "push" and ("f" in flags_curtas or any(a.startswith("--force") for a in args)):
        return "`git push --force` reescreve o histórico do remoto"
    if subcomando in {"checkout", "restore"} and ("." in args or ":/" in args):
        return f"`git {subcomando} .` descarta todas as alterações da árvore"
    if subcomando == "branch" and ("D" in flags_curtas or ("--delete" in args and "--force" in args)):
        return "`git branch -D` apaga uma branch sem checar se foi mesclada"
    if subcomando in {"commit", "push"} and "--no-verify" in args:
        return f"`git {subcomando} --no-verify` pula os hooks de pre-commit"
    # Em `git commit`, `-n` é `--no-verify`, não dry-run.
    if subcomando == "commit" and "-n" in args:
        return "`git commit -n` (= --no-verify) pula os hooks de pre-commit"
    return None


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    if payload.get("tool_name") != "Bash":
        return 0

    comando = payload.get("tool_input", {}).get("command", "")
    cwd = payload.get("cwd") or os.getcwd()
    workspace = os.path.dirname(raiz_do_repo())
    nossos = [os.path.join(workspace, nome) for nome in REPOS_DO_DISPATCH]

    for repo, subcomando, tokens in chamadas_git(comando, cwd):
        dentro = any(repo == r or repo.startswith(r + os.sep) for r in nossos)
        # O resto da pasta do workspace (`dispatch/`, `dispatch/.claude`, `dispatch-prototype`) não é
        # repositório git: ali o git só lê ou falha, não há trabalho de ninguém pra destruir.
        if not dentro and (repo == workspace or repo.startswith(workspace + os.sep)):
            continue

        escreve = subcomando in ESCRITAS or (
            subcomando in ESCRITAS_CONDICIONAIS and any(f in FLAGS_QUE_ESCREVEM for f in tokens[1:])
        )
        if subcomando not in {"commit"} and "--dry-run" in tokens:
            escreve = False

        if not dentro and escreve:
            sys.stderr.write(
                f"Bloqueado: `git {subcomando}` em `{repo}`, fora do Dispatch. Só dispatch-web e "
                f"dispatch-api são nossos; outros repositórios se leem (`log`, `show`, `diff`, "
                f"`fetch`) e nunca se escrevem.\n"
            )
            return 2

        if dentro:
            motivo = motivo_destrutivo(subcomando, tokens)
            if motivo:
                sys.stderr.write(
                    f"Bloqueado: {motivo}. Pode haver trabalho de outra sessão neste checkout. "
                    f"Pergunte ao usuário; se ele quiser mesmo, ele roda o comando com `!` no prompt.\n"
                )
                return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
