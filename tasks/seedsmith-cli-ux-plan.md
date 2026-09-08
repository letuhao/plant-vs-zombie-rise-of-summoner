# Plan: seedsmith-cli-ux — agentic fill + `.env` defaults

**Program:** `seedsmith-cli-ux` · **Status:** implemented  
**Artifacts:** this file + [`seedsmith-cli-ux-todo.md`](seedsmith-cli-ux-todo.md)

## Goal

Make item generation usable for operators and agents: configure once in `tools/seedsmith/.env`, then `items fill` / `items generate --kind X --write` without retyping `--endpoint`, `--out-dir`, and `--allow-production-tree`.

## What shipped

1. **`resolve_dotenv_path` / `resolve_live_transport`** in `pipeline/llm_caller.py` — CWD or package `.env`; empty CLI falls through to config.
2. **`adapters/items/defaults.py`** — `SEEDSMITH_ALLOW_PRODUCTION_TREE`, production out-dirs, `FILL_KIND_ORDER`.
3. **`items generate --write`** — out-dir / allow-production / endpoint defaults; passthrough `run.py` files use `resolve_live_transport`.
4. **`items fill`** — dependency-ordered walk, partition discovery, ledger resume, `--dry-run`.
5. **Skill rewrite** — `.agents/skills/seedsmith` one-liners + truthful `.env` contract.
6. **Tests** — `tests/test_items_fill_ux.py` + updated refuse tests.

## Audit follow-up (fill smoke) — shipped

**Verdict:** `FILL_KIND_ORDER` is a valid topo of `item-seedgen-map.md` §5 (no cross-kind inversion). Failure mode was unbounded fill + silent GAP/`SystemExit`, not wrong order.

Fixes:

- `--limit` / `--count` / `--batch-size` / `--max-partitions` / `--full` on `items fill`
- Refuse set/charm/combination without `--limit` or `--full`
- Catch `SystemExit`; stop on `EXIT_GAP`; recipe emits `--count`; allow-production only when those kinds are selected; strain before splice
- Topo + smoke unit tests

## Authority

- `docs/architecture/seedsmith/spec-foundation.md` §7.3 — every flag has a config equivalent.
- `docs/architecture/item-seedgen-map.md` §5 — fill kind order.
- `docs/architecture/item-seedgen/spec-generator-harness.md` — RunLedger resume.

## Out of scope (unchanged)

Cross-domain mega-fill; auto-`--write` on generate; inventing new partitions; schema/prompt changes; expanding `items validate --deps` beyond combination.
