#!/usr/bin/env python3
"""PostToolUse (Edit|Write|MultiEdit): `dotnet format whitespace` só no .cs que acabou de mudar.

Mesma ideia do `prettier-on-edit.py` do dispatch-web: formatar só o arquivo tocado deixa o diff no
que se quis mudar. Só **whitespace** (indentação, espaços, quebras) — não mexe em estilo nem em
análise, que mudam código e pedem revisão. Modo `--folder` (sem carregar a solução): ~0,7s por
arquivo contra ~3s com `Dispatch.slnx`. Na adoção, `dotnet format whitespace --folder
--verify-no-changes` no repositório inteiro deu zero divergência, então o hook não gera ruído em
arquivo que não era pra mudar.

Nunca bloqueia: qualquer falha sai com 0 e só avisa no stderr.
"""

import json
import os
import subprocess
import sys

PASTAS_IGNORADAS = ("/bin/", "/obj/", "/Migrations/", "/coveragereport/", "/test-results/")


def raiz_do_repo() -> str:
    """O repositório deste hook, achado pelo próprio arquivo (`<repo>/.claude/hooks/x.py`)."""
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    if payload.get("tool_name") not in {"Edit", "Write", "MultiEdit"}:
        return 0

    arquivo = payload.get("tool_input", {}).get("file_path", "")
    if not arquivo.endswith(".cs") or not os.path.isfile(arquivo):
        return 0
    if any(pasta in arquivo for pasta in PASTAS_IGNORADAS):
        return 0

    repo = raiz_do_repo()
    arquivo = os.path.abspath(arquivo)
    if not arquivo.startswith(repo + os.sep):
        return 0

    try:
        resultado = subprocess.run(
            ["dotnet", "format", "whitespace", "--folder", "--include", os.path.relpath(arquivo, repo)],
            cwd=repo,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        if resultado.returncode != 0 and resultado.stderr.strip():
            sys.stderr.write(f"dotnet format: {resultado.stderr.strip()[:500]}\n")
    except (OSError, subprocess.TimeoutExpired) as erro:
        sys.stderr.write(f"hook do dotnet format pulado: {erro}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
