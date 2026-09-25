#!/usr/bin/env python3
"""Registra uso de skill deste repositório em `.claude/skill-usage.jsonl` (ignorado pelo git).

Adaptado do swap-benefits-web. Serve pra ver quais skills de fato disparam e quais nunca são
usadas (descrição fraca, ou skill que não se paga). Registrado em dois eventos, porque a skill
chega por dois caminhos:
- `PostToolUse` com `tool_name == "Skill"`: o modelo decidiu invocar;
- `UserPromptSubmit`: o usuário digitou `/nome ...` — nem sempre passa pelo tool `Skill`.

Só conta skill que existe em `.claude/skills/<nome>/SKILL.md` DESTE repositório. Com a sessão
aberta em `dispatch/`, o Claude Code qualifica nome repetido (`dispatch-web:x`); um prefixo que
não é deste repositório é ignorado, pra não contar o uso duas vezes.

Pra agregar:
    python3 -c "import json,collections; print(collections.Counter(json.loads(l)['skill'] \
      for l in open('.claude/skill-usage.jsonl')).most_common())"

Nunca bloqueia.
"""

import datetime
import json
import os
import re
import sys

ARQUIVO_DE_LOG = os.path.join(".claude", "skill-usage.jsonl")
COMANDO_BARRA = re.compile(r"^\s*/([a-z0-9][a-z0-9:_-]*)(?:\s+(.*))?$", re.DOTALL)


def raiz_do_repo() -> str:
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def skills_do_repo(repo: str) -> set[str]:
    pasta = os.path.join(repo, ".claude", "skills")
    if not os.path.isdir(pasta):
        return set()
    return {n for n in os.listdir(pasta) if os.path.isfile(os.path.join(pasta, n, "SKILL.md"))}


def normalizar(nome: str, repo: str) -> str | None:
    """`dispatch-web:gate` → `gate` se o prefixo for este repo; outro prefixo → None."""
    nome = nome.strip("/")
    if ":" in nome:
        prefixo, nome = nome.rsplit(":", 1)
        if prefixo != os.path.basename(repo):
            return None
    return nome


def extrair(payload: dict, repo: str, conhecidas: set[str]) -> tuple[str, str, str] | None:
    evento = payload.get("hook_event_name", "")
    if evento == "PostToolUse" and payload.get("tool_name") == "Skill":
        entrada = payload.get("tool_input") or {}
        nome = normalizar(str(entrada.get("skill") or entrada.get("skill_name") or ""), repo)
        return (nome, str(entrada.get("args") or ""), "model") if nome in conhecidas else None
    if evento == "UserPromptSubmit":
        casou = COMANDO_BARRA.match(payload.get("prompt") or "")
        if not casou:
            return None
        nome = normalizar(casou.group(1), repo)
        return (nome, (casou.group(2) or "").strip(), "user") if nome in conhecidas else None
    return None


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    repo = raiz_do_repo()
    registro = extrair(payload, repo, skills_do_repo(repo))
    if registro is None:
        return 0
    skill, args, origem = registro
    linha = {
        "ts": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
        "skill": skill,
        "origem": origem,
        "args": args[:200],
        "session_id": payload.get("session_id", ""),
    }
    try:
        with open(os.path.join(repo, ARQUIVO_DE_LOG), "a", encoding="utf-8") as log:
            log.write(json.dumps(linha, ensure_ascii=False) + "\n")
    except OSError as erro:
        sys.stderr.write(f"hook de uso de skill pulado: {erro}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
