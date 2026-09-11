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

### Smoke resolve (2026-09-09, follow-up)

- [x] Fill gem argv uses `--count` (not `--batch-size`) so outer CLI accepts it
- [x] Affix partitions with no free `(channel, op)` planned as `skipped`; filter before `max_partitions`
- [x] Gem slots with `toGenerate=0` planned as `skipped`; filter before `max_partitions`
- [x] `tests/test_items_fill_ux.py` — 26 passed
- Operator: gem `--slot 1 --count 1 --write` → **persisted** (fresh=1)
- Operator: affix `g.armour` / `g.tempo` → model still fails identity fields / taken pairs (content, not fill argv)
- Operator: set `--limit 1` → escalate (forbidden atom / bad capability pick)
- Operator: `items validate --deps` → exit 0
- Operator: combination strain `--limit 1` → `planned=0` (sockword / `gem.word-*` corpus debt)

### Root-cause fixes (2026-09-09) — not hide

- [x] Affix wire schema: `required` + nullable, free-pair `channelOp` enum, derive `nameKey` — live **persisted** `atom.arm-plate`
- [x] Set brief/schema: `legal_set_stat_pool` drops D14/More; capability family enum — live **persisted** 1 set
- [x] Combination: `--limit` after `plan_needing_work`; same required+null schema fix as setgen
- [x] Bounded `items fill --limit 1 --max-partitions 1 --count 1 --batch-size 1 --continue-on-error`: affix/set/gem/material/…/strain **ran**; only splice **gap** (model/content), not empty-plan or argv refuse

### Gap resolve (2026-09-09) — blocked exit + ledger, not hide

- [x] Live splice investigate: `combination-splice-might-fortitude` → **blocked** (not escalate), process EXIT=1 under old contract
- [x] Combination + set/charm: ledger `blocked` as done; `_exit_for_graph_batch` → EXIT_CLEAN unless escalate; combo `_ledger_is_valid` accepts blocked rows
- [x] Strain brief: defense/balance omit offense-shaped "Hit harder" paste; splice brief frames opposing readings as fusion material (`PROMPT_VERSION` → `strain-splice-gen/2`)
- [x] Set `plan_run`: skip entry ids already on disk (theme rename / ledger drift → caltropnut collision)
- [x] Unit tests: combogen blocked resume + exit helper; BriefTests paradox/splice framing; set corpus skip; wiring blocked ledger
- [x] Bounded fill **without** `--continue-on-error`: process **EXIT=0**, all 12 steps `status=ran` / `exit_code=0` (charm+strain legitimate blocked; splice persisted)

### Full-fill runnable (2026-09-09)

- [x] Escalate ledgered like blocked (`RunLedger.mark_terminal` / `terminal_row`); EXIT_GAP still surfaces escalate; resume advances
- [x] `--full` without explicit `--count`/`--batch-size`: open kinds use elevated pass (8); gem drains remaining unauthored families
- [x] Fill set steps: `species` then `build`
- [x] Unit tests: escalate resume, full plan shape, continue-on-error past gap
- [x] Prove: `items fill --full --dry-run` (build set + gem batch=29 + count=8); live `--full --continue-on-error --kinds set,charm,combination,material,recipe` → **EXIT=0**, all steps `ran` (incl. build set + strain/splice)
