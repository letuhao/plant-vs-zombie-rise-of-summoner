#!/usr/bin/env python3
"""Strip known watermark trailer lines from a commit message file (prepare-commit-msg)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

STRIP_LINE = re.compile(
    r"(?i)^\s*(?:"
    r"co-authored-by\s*:"
    r"|signed-off-by\s*:"
    r"|generated-by\s*:"
    r"|assisted-by\s*:"
    r"|made-with\s*:"
    r"|made with\s+cursor"
    r").*$"
)


def strip_file(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    kept = [ln for ln in lines if not STRIP_LINE.match(ln)]
    # Collapse trailing blank lines to a single newline ending
    while kept and kept[-1].strip() == "":
        kept.pop()
    path.write_text("\n".join(kept) + "\n", encoding="utf-8")


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: strip_watermarks.py <msg-file>", file=sys.stderr)
        return 2
    strip_file(Path(argv[1]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
