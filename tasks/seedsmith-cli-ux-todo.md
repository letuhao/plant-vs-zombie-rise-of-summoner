# Todo: seedsmith-cli-ux

- [x] Fix `load_config` dotenv path + `resolve_live_transport`; extend `.env.example`
- [x] `items generate --write`: endpoint/out-dir fall through to `.env` + production dirs
- [x] Add `items fill` (dependency-ordered, ledger resume, partition discovery, dry-run)
- [x] CLI/unit tests for `.env` fallback, out-dir default, fill order/dry-run
- [x] Rewrite `.agents/skills/seedsmith` for agentic one-liners + truthful `.env`/kinds docs
- [x] Write `tasks/seedsmith-cli-ux-plan.md` and this todo
- [x] Fill audit: batch limits, exit contract, recipe count, allow-gate, stable sort
- [x] Topo + smoke unit tests
- [x] Skill/tasks smoke procedure

## Operator smoke (small batch)

```powershell
cd tools/seedsmith
python -m seedsmith items fill --limit 1 --max-partitions 1 --count 1 --batch-size 1 --dry-run
python -m seedsmith items fill --limit 1 --max-partitions 1 --count 1 --batch-size 1
python -m pytest tests/test_items_fill_ux.py -q
```

Widen `--max-partitions` / `--limit` only after smoke is green. Use `--full` only when intentional.

### Smoke evidence (2026-09-09)

- Dry-run: 12 phase-ordered steps; strain before splice; affix jobs filtered to `stat.modify`/`stat.derived`.
- Live (bounded): material / milestone / base-type / charm / recipe / drop-table **ran**; gem **refused**; set + combination **gap** (model escalate); affix first attempt failed on free-pair/schema (caught as `error`, no crash).
- Exit contract: non-zero on gap/error; JSON report complete; no unbounded runaway.
