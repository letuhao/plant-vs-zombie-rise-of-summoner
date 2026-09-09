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

Cross-domain mega-fill; auto-`--write` on generate; inventing new partitions; schema/prompt changes; expanding `items validate --deps` beyond combination; migrating `gem.word-*` / sockword combination corpus; wiring affixfamgen onto `call_with_self_heal` (today uses `live_answer_caller` one-repair).

## Smoke resolve (2026-09-09)

Root cause of gem fill `refused`: fill emitted `--batch-size`, which the outer `items generate` parser rejects (shared flag is `--count`). Fixed + skip full affix / empty gem slots before `max_partitions`. See todo for operator evidence.

## Root-cause fixes (2026-09-09) — generator schema/vocab, not skips

1. **Affix** — `required`+nullable wire schema, free-pair-only `channelOp`, derive `nameKey` ([`affixfamgen/schema.py`](../tools/seedsmith/seedsmith/adapters/items/affixfamgen/schema.py)).
2. **Set** — `legal_set_stat_pool` + capability family enums so brief/schema match D14/More distributor bans.
3. **Combination** — apply `--limit` after ledger resume; same required+nullable identity schema as setgen.

Sockword retirement remains a separate migrate stream.

## Gap resolve (2026-09-09) — investigate + fix remaining fill gaps

Immediate splice `gap` was not “content fate”: combination/set write required `persisted` this batch, so fill `--limit 1` + model `blocked` → EXIT_GAP, and blocked subjects were not ledgered (resume re-hit). Affix already returned 0 on blocked.

Shipped:

1. **`_exit_for_graph_batch`** in [`cli.py`](../tools/seedsmith/seedsmith/report/cli.py) — EXIT_CLEAN unless any `escalated`.
2. **Combo/set ledger blocked** — mark done with `{outcome, blockedReason}`; combo `_ledger_is_valid` accepts that shape.
3. **Brief framing** — [`combogen/brief.py`](../tools/seedsmith/seedsmith/adapters/items/combogen/brief.py) strain defense/balance + splice tension sentence (`strain-splice-gen/2`).
4. **Set corpus skip** — [`setgen/run.py`](../tools/seedsmith/seedsmith/adapters/items/setgen/run.py) `plan_run` skips set ids already on disk (fixes theme-rename ledger drift collisions).

Prove: `items fill --limit 1 --max-partitions 1 --count 1 --batch-size 1` (no `--continue-on-error`) → EXIT=0, all steps ran.

## Full-fill runnable (2026-09-09)

Closes the overnight gaps after bounded smoke:

1. **Escalate terminal ledger** — `RunLedger.mark_terminal` / `terminal_row`; combo + set write escalate rows so resume does not re-hit; batch still EXIT_GAP if any escalate.
2. **`--full` drain** — without explicit `--count`/`--batch-size`, open kinds use pass size 8; gem uses remaining unauthored count per slot.
3. **Build sets in fill** — species then build population steps.

Operator overnight:

```powershell
cd tools/seedsmith
python -m seedsmith items fill --full --dry-run
python -m seedsmith items fill --full --continue-on-error
# Re-run same command until EXIT=0 (escalate ledgers advance; gaps surface once)
```

Sockword migrate remains a follow-on after combo coverage.
