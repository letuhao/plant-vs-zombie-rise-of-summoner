"""LangGraph checkpoint wiring — engine layer, so `runner.py` stays engine-free (the seam).

Owner decision 2026-09-01: `guard-dal`'s "SQL only in FusionRpg.Data" invariant protects the
SHIPPED GAME's data layer; `tools/seedsmith/` is dev tooling that never ships. **Scope is pinned:
sqlite3 here is checkpoint state only.** Python still never reads the game's SQLite (`types`,
`almanac_seed`, `recipes`) — that stays C#-through-the-DAL, for a reason shipping does not affect.
"""
from __future__ import annotations

import sqlite3
from pathlib import Path

__all__ = ["open_checkpointer"]


def open_checkpointer(db_path: "str | Path"):
    """Returns `(saver, connection)`. The caller closes the connection."""
    from langgraph.checkpoint.sqlite import SqliteSaver

    Path(db_path).parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(db_path), check_same_thread=False)
    return SqliteSaver(conn), conn
