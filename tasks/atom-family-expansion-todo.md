# Task list: atom-family-expansion

See `tasks/atom-family-expansion-plan.md` for architecture decisions, dependency graph, and risks.
Specs: `docs/architecture/atom-family-expansion/spec-tier-bands-coverage.md`,
`spec-battle-ruleset-curve-extension.md`.

## Phase 1 — `tier-bands-coverage`

### Task 1: `channel_weight_backfill.py` — formula + pure computation

**Description:** Add `tools/seedsmith/seedsmith/numerics/channel_weight_backfill.py` with
`WEIGHT_BY_BAND: dict[str, int]` (the 5-row table, computed from `bands.v1.json`'s ordinal map and
`seedsmith.numerics.MAGNITUDE_RATIO_PERMILLE` — **import the existing exported constant directly**
rather than re-declaring a mirrored copy; confirmed during planning that `numerics/__init__.py` already
exports it at value `1750`) and `missing_channel_weights(families, tuning) -> dict[str, int]` (pure,
additive-only against `tuning.channel_weight_permille`).

**Acceptance criteria:**
- [x] `WEIGHT_BY_BAND["medium"] == 1000` exactly.
- [x] `WEIGHT_BY_BAND` matches the **corrected** table: trivial=326, low=571, medium=1000, high=1750,
      extreme=3062 (each via `round_legible`, not float rounding).
- [x] `missing_channel_weights` never includes a family stem already present in
      `tuning.channel_weight_permille`, even when the formula would compute a different value for it.
- [x] Real-corpus regression: run against the actual 112-family corpus and real `tier-bands.v1.json` —
      exactly 98 entries returned; the 5 curve-blocked stems (`plating`, `quickening`, `flourishing`,
      `swiftness`, `carapace`) are NOT among them.
- [x] **Known-divergence test, named explicitly** (spec §5): `fortitude`/`ferocity`/`resilience`/
      `mending`/`bulwark`/`savagery` are proven absent from `missing_channel_weights`'s output (already
      published), AND a separate assertion states what `WEIGHT_BY_BAND` would have computed for each
      (`571`/`571`/`571`/`571`/`1750`/`1750`) — the accepted divergence is a visible, tested fact, not
      an implementation detail.

**Verification:**
- [x] Tests pass: `pytest tools/seedsmith/tests/test_channel_weight_backfill.py` — 13/13
- [x] `pytest tools/seedsmith/tests` (full suite) — no new failures beyond the pre-existing 13 named in
      this session's own memory (real-corpus-count drift, unrelated to this module)

**Dependencies:** None

**Files likely touched:**
- `tools/seedsmith/seedsmith/numerics/channel_weight_backfill.py` (new)
- `tools/seedsmith/tests/test_channel_weight_backfill.py` (new)

**Estimated scope:** Small (2 files, pure functions, no I/O beyond reading real corpus for the
regression test)

**Done 2026-09-08.** Real, self-caught defect during build: the spec's own table (trivial=327,
extreme=3063) used a closed-form power calculation with round-half-up, matching the C# `RoundLegible`
signature — but this project's real, shipped Python `round_legible(value: float) -> int` is
single-argument (Python's own `round()`, round-half-to-even) and the real, established convention for
"apply this ratio N steps in a row" (`tier_ladder`) is **chained, per-step rounding**, not a
closed-form power. Verified directly against the real `tier_ladder`/`round_legible` functions before
writing any table: chained gives trivial=326/extreme=3062, not 327/3063. Fixed before it shipped, not
after — the spec and this file are now consistent. Also: `WEIGHT_BY_BAND` correctly imports
`seedsmith.numerics.MAGNITUDE_RATIO_PERMILLE` directly rather than re-declaring a private copy, per
the plan's own suggested improvement.

---

### Task 2 (NEW — inserted mid-execution, real defect found while starting Task 3): fix `FamilyExpandGen`'s hardcoded `tier-bands.v1.json`

**Description:** Discovered while preparing to run Task 3 (originally numbered Task 2/3 below, now
renumbered 3/4): `tools/FamilyExpandGen/Program.cs` hardcoded the literal path
`"tier-bands.v1.json"` — never "latest". `seedsmith numerics rebalance --publish` already writes
`tier-bands.v{n+1}.json` (and had done so twice before this session, from an earlier, separate effort:
`tier-bands.v2.json`/`v3.json` already existed on disk, dated before this plan started, with 109/112
real `channelWeightPermille` entries each — both **completely unread by the real generator**). This
means the entire versioned-rebalance mechanism this whole plan depends on was silently disconnected
from the thing it exists to feed, for every publish that has ever happened. Not something either spec
anticipated; a genuine prerequisite bug for Task 4 (the real-corpus run) to have any chance of working.

**Acceptance criteria:**
- [x] `TierBandsFile.FindLatestPath(tuningDir)` added to `src/FusionRpg.Core/Effects/Atoms/Generation/TierBandsFile.cs`,
      mirroring `seedsmith.numerics.tier_bands_io.load("latest")`'s own glob-and-pick-highest-version
      logic exactly (numeric comparison, not lexicographic — `v10` beats `v2`).
- [x] `tools/FamilyExpandGen/Program.cs` calls it instead of the hardcoded literal.
- [x] Real-repo regression: resolves to `tier-bands.v3.json` (or higher) today, not `v1.json`.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TierBandsFileTests"` — 6/6
- [x] `dotnet build tools/FamilyExpandGen` — 0 errors
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` — real, observed behavior change:
      `112 families read, 45 row(s) emitted, 3 files, 103 refused` (stale v1, before fix) →
      `112 families read, 130 row(s) emitted, 7 files, 86 refused` (real v3, after fix) — **0** remaining
      `no authored sharePermille` refusals (the exact class Task 4 exists to close), confirming the fix
      actually closes the specific gap this plan targets, not just that it runs without error.

**Dependencies:** None (found independent of Task 1, blocks Task 4)

**Files likely touched:**
- `src/FusionRpg.Core/Effects/Atoms/Generation/TierBandsFile.cs` (edit — new `FindLatestPath`)
- `tests/FusionRpg.Core.Tests/Atoms/Generation/TierBandsFileTests.cs` (new)
- `tools/FamilyExpandGen/Program.cs` (edit — use `FindLatestPath`, fix stale header comment)

**Estimated scope:** Small (mechanical fix, but a real, load-bearing one)

**Done 2026-09-08.** See `[[atom-family-expansion-real-gap-discovery]]`-shaped evidence below (§ "A
real, larger-than-planned discovery") for the full consequence of running this fix for real: the
refusal landscape past this point is NOT what either spec assumed.

---

### Task 3: CLI wiring — `write_set_file` + `main()`

**Description:** Add `write_set_file(missing, path)` (writes `channelWeight.<id>=<value>` lines,
sorted, matching `seedsmith numerics rebalance --set-file`'s exact expected format) and `main(argv)` to
the same module: reads the real corpus + `TierBands.load("latest")`, computes `missing_channel_weights`,
writes a temp `--set-file`, and prints the exact `seedsmith numerics rebalance --set-file <path>
--publish` command for the operator to run. **Does not call `--publish` itself.**

**Acceptance criteria:**
- [x] `write_set_file`'s output is byte-for-byte parseable by `cli.py`'s own `_parse_set_pairs` (proven
      by round-tripping through it in a test, not just eyeballing the format).
- [x] `main()` writes to a caller-specified path (via `tmp_path` in tests), never the real repo state,
      and never invokes `--publish` — a test asserts no new `tier-bands.v*.json` exists after `main()`
      runs.
- [x] `main()`'s printed command, when actually run, produces a next-version file whose
      `channel_weight_permille` has exactly the new entries plus the untouched originals — proven via a
      full scratch-dir round trip (compute → write → real `_parse_set_pairs` → real `TierBands.adjust`
      → real `tier_bands_io.save`/`load`).

**Verification:**
- [x] Tests pass: `pytest tools/seedsmith/tests/test_channel_weight_backfill.py` — 20/20
- [x] Manual check: `python -m seedsmith.numerics.channel_weight_backfill` run against the real repo —
      prints a correct, copy-pasteable `seedsmith numerics rebalance --set-file ... --publish` command

**Dependencies:** Task 1

**Files likely touched:**
- `tools/seedsmith/seedsmith/numerics/channel_weight_backfill.py` (edit — add `write_set_file`, `main`)
- `tools/seedsmith/tests/test_channel_weight_backfill.py` (edit)

**Estimated scope:** Small (1 file, CLI glue)

**Done 2026-09-08.** Two real defects self-caught before shipping, neither in the original spec:
(1) `_parse_set_pairs`/`TierBands.adjust` both document and implement values as **ratio multipliers**
(`1.0` == `1000`‰), not raw per-mille integers — `write_set_file` originally wrote raw integers, which
would have silently published values 1000x too large. Fixed by dividing by `1000.0` before writing,
proven via a real round-trip test through the actual `_parse_set_pairs`/`TierBands.adjust` functions,
not a hand-rolled reimplementation. (2) The "additive-only against `tuning`" design assumed `tuning`
(`"latest"`) still reflected the owner's actual decision — but `TierBands.load("latest")` resolves to
`v3.json`, which (per Task 2's discovery) already has all 112 stems at a uniform placeholder `1000`
from an unrelated earlier effort. Checking "already covered" against `latest` would have made this
whole module a no-op. Fixed by introducing `PROTECTED_V1_STEMS` (the real, literal 14 stems the
owner's decision was actually made against) plus a defense-in-depth check (a non-protected stem
already carrying something OTHER than the known placeholder value is left alone too, in case any
future effort deliberately set one for real).

---

### Task 4: Real-corpus run — publish, regenerate, verify

**Description:** Run the actual workflow against the real repo (not a test fixture): `git status`
check first (confirm no concurrent session mid-edit on `tier-bands.v*.json` or
`affix-families/*.json`), dry-run the printed `rebalance` command and review the diff, then `--publish`,
then `dotnet run --project tools/FamilyExpandGen` for real (no `--check`) to write the new
`family-expand.g-*.json` files.

**⛔ Corrected before execution**: `tier-bands.v2.json`/`v3.json` already exist (an earlier, separate
effort, `tasks/seedsmith-todo.md` S5 — 109/112 entries, every one uniformly `1000`, i.e. the "uniform
default" this plan's own decision explicitly chose NOT to ship). Publishing will write
`tier-bands.v4.json`, not `v2.json`. `TierBands.load("latest")` (used by Task 1's own
`missing_channel_weights`) already reads `v3.json` as its "already published" baseline, so Task 1's
"98 missing" count is computed against `v3`'s 112-entries-all-1000 state, not the original 14-entry
`v1.json` — **the same 98 stems are still missing real per-band differentiation either way**, since
v3's own 98 non-original entries are ALSO uniformly `1000`, not powerBand-derived. Publishing v4
supersedes those 98 uniform placeholders with this module's real, band-derived values — the 14
originally-published entries (8 medium + 6 diverging, spec §6) stay untouched regardless.

**Acceptance criteria:**
- [ ] `git status` clean on the two target areas before publishing (or any drift explained and
      confirmed unrelated).
- [ ] Dry-run diff reviewed and matches Task 1/3's own expectations (98 additions/replacements against
      `v3`'s baseline) before `--publish` is actually run.
- [ ] `data/seed/items/_tuning/tier-bands.v4.json` written; `v1`/`v2`/`v3` untouched (revert path intact).
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check` (run again, read-only, after the real
      write) reports **zero** `no authored sharePermille` refusals. **Corrected acceptance bar** (was:
      "refusals only in the no-referenceBaseGameUnits class" — that assumed only 2 refusal-reason
      classes existed; Task 2's own real-execution evidence found 3 MORE, unrelated to sharePermille
      coverage at all — see the new "§ A real, larger-than-planned discovery" section below). The
      correct, achievable bar for THIS module is narrower: zero refusals for the ONE reason this
      module exists to fix.
- [ ] `AtomRowValidator` accepts every newly-generated row (run the real import/validation path — e.g.
      `Core.Tests`' existing atom-content validation suite — against the new `family-expand.g-*.json`
      files).

**Verification:**
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check` — 0 `no authored sharePermille` refusals
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"` (or wherever the real
      atom-content validation suite lives) — no new failures
- [ ] `python scripts/audit-magic-numbers.py --domain item` — 0 findings (this module authors tunable
      values only through the existing `rebalance` CLI, never a bare literal)

**Dependencies:** Task 3 (renumbered from original Task 2), Task 2 (the wiring-bug fix, without which
this task cannot succeed at all)

**Files likely touched:**
- `data/seed/items/_tuning/tier-bands.v4.json` (new, via `rebalance --publish`)
- `data/seed/atoms/generated/family-expand.g-*.json` (regenerated, via `FamilyExpandGen`)

**Estimated scope:** Small (no new code; running already-built tools against real data)

---

### Checkpoint: Phase 1 complete
- [ ] All Phase 1 tests pass (Tasks 1-3's own suites)
- [ ] Task 4's real-corpus run completed and verified — 98 additional families now have a real,
      band-differentiated `channelWeightPermille` entry (superseding v3's uniform placeholder)
- [ ] `spec-tier-bands-coverage.md`'s own acceptance criteria (§2, §5) fully satisfied
- [ ] Review with human before proceeding to Phase 2 (independent — may also proceed without waiting,
      per the plan's own "no dependency between phases" finding)

## A real, larger-than-planned discovery (found executing Task 2, 2026-09-08)

Once `FamilyExpandGen` actually reads real, complete tuning data (any of v2/v3/the future v4), the true
refusal landscape is **86 refusals across 3 reason classes neither spec anticipated** — measured live
against `tier-bands.v3.json` (112/112 name coverage, uniform `1000`):

| Reason | Count | What it actually is |
|---|---|---|
| `no opWeightPermille entry for op 'X'` | 23 | Some real families are authored with an op string (`add`, `Flag`, others) that isn't one of the 3 members (`Flat`/`Increased`/`More`) `opWeightPermille` recognizes — a vocabulary gap, not a missing number |
| `op '' has no supported tier-magnitude formula ... Replace/Flag carry no tier-band magnitude` | 40 | `FamilyExpansion.cs` (the C# generator) has **no tier-magnitude formula for `stat.derived` Replace/Flag ops at all** — mirrors the identical gap this session's own investigation already found on the seedsmith Python side (`resolve_tier_curve` raising `NumericsPathUnavailableError` for the same ops). A structural capability gap, not missing calibration data. |
| `no referenceBaseGameUnits for channel X` | 19 | Now that sharePermille no longer blocks resolution first, MORE families reach the reference-base check than the 5 this plan's own `battle-ruleset-curve-extension` spec named — e.g. `status.power`, a channel neither spec mentions |

**None of this is in either module's spec.** Both were written against `FamilyExpandGen --check`
output read through the stale, hardcoded `v1.json` (14 entries) — which happened to make "no authored
sharePermille" the ONLY visible blocker for 98 families, masking whatever came after it in the
resolution pipeline for every one of them. Fixing the wiring bug (Task 2) was necessary and firmly
in-scope (Task 4 cannot work without it); the 3 reason classes above are not — they are new, real,
substantial findings (23 + 40 + 19 = 82 refusals, most requiring actual engineering, not data
authoring) that belong in their own future `/idea` → `/spec` cycle, not silently absorbed into this
plan's own two already-scoped, already-decided modules.

**This plan's own remaining commitment is unchanged in kind, corrected in acceptance bar**: Task 4
closes the `no authored sharePermille` class completely (98 families). `battle-ruleset-curve-extension`
(Phase 2 below) closes `no referenceBaseGameUnits` **for the 5 channels its own spec explicitly named**
(`arm1Max`/`arm2Max`/`attackInterval`/`produceInterval`/`zombieSpeed`) — the other 14 newly-visible
channels in that same reason class are the newly-discovered scope above, not this plan's.

## Phase 2 — `battle-ruleset-curve-extension`

### Task 5: Calibration data-gathering

**Description:** Per spec §4's own named resolver: find real vanilla PvZ zombie/plant baseline values
for `arm1Max`, `arm2Max`, `attackInterval`, `produceInterval`, `zombieSpeed` at a reference specimen
comparable to whatever `atk`'s `92`/`defense`'s `22` pinValues represent (a normal early-game
zombie/plant, by magnitude) — the same source `EntityStatWriter.WritePlantExtras`/`WriteZombieExtras`
already reads at runtime. This is an investigation task, not a code task; its output is either real
numbers with a cited source, or an explicit, documented "not found" that triggers the named fallback.

**Acceptance criteria:**
- [ ] For each of the 5 channels: either (a) a real `cMilli`/`pinValue` pair with a cited source
      (a specific data file, a specific in-game specimen, or a specific existing code path that already
      reads this value), or (b) an explicit note that no such source was found.
- [ ] No fabricated or "plausible-looking" number is recorded for any channel lacking a real source —
      per spec §4's own hard rule.
- [ ] If `atk`/`defense`'s own original calibration source is discoverable along the way (this session
      did not find one), cite it — useful precedent for future channels even though not required to
      unblock this task.

**Verification:**
- [ ] Documented findings reviewed against spec §4's own fallback rule before Task 6 starts

**Dependencies:** None (can run in parallel with Phase 1 or before it)

**Files likely touched:** None (investigation only; findings feed Task 6's scope)

**Estimated scope:** Small-medium (research, not code)

---

### Task 6: `BattleModels.cs` — 5 new `Base*` functions

**Description:** For every channel Task 5 found real calibration data for (possibly fewer than 5), add
a `Base*` function mirroring `BaseAtk`/`BaseDefense`'s exact existing shape (`ChannelLadderFor("<id>")`,
cached in a static field) and the matching `channels` row in `data/tuning/power-scale.v2.json`. A
channel Task 5 could not calibrate is skipped entirely — no function, no row, no fallback default (per
spec §4/§7 boundaries: `ChannelLadderFor` must keep throwing for it, never default).

**Acceptance criteria:**
- [ ] Each new `Base*` function is `long`, overflow-checked (`checked` arithmetic, matching
      `BaseAtk`/`BaseDefense`), deterministic for a given level.
- [ ] `ChannelLadderFor` still throws `InvalidOperationException` for any channel NOT added (proven with
      a test naming a channel guaranteed absent) — the guard is never weakened.
- [ ] No new curve TYPE or formula shape — only new calibration inputs to the existing
      `ChannelLadder`/`PowerLadder` mechanism.

**Verification:**
- [ ] Tests pass: extend `tests/FusionRpg.Core.Tests`' existing `BattleRuleset`/`BaseAtk`/`BaseDefense`
      test file (find via `grep -rln "BaseAtk" tests/FusionRpg.Core.Tests`) with one new test per added
      channel
- [ ] `python scripts/audit-overflow.py` — 0 critical findings on the new functions

**Dependencies:** Task 5 (scope depends on which channels have real data)

**Files likely touched:**
- `src/FusionRpg.Core/Battle/BattleModels.cs` (edit)
- `data/tuning/power-scale.v2.json` (edit)
- `tests/FusionRpg.Core.Tests/Battle/*.cs` (edit — extend existing `BaseAtk`/`BaseDefense` coverage)

**Estimated scope:** Small-medium (mechanical per channel, scope shrinks if Task 5 finds fewer than 5)

---

### Task 7: `FamilyExpandGen`'s `FlatReferenceBase` — wire the new channels

**Description:** For each channel Task 6 actually added, add one arm to
`tools/FamilyExpandGen/Program.cs`'s `FlatReferenceBase` switch calling the new `BattleRuleset.Base*`
function — mechanical, mirrors the existing 3 lines (`maxHp`/`hp`, `atk`, `defense`) exactly.

**Acceptance criteria:**
- [ ] Every channel Task 6 added has a matching arm here; any channel Task 5 could not calibrate is
      left returning `null` (unchanged, still honestly refused).
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check` — the families for newly-wired channels
      no longer refuse for `no referenceBaseGameUnits` (they may still refuse for
      `no authored sharePermille` if Phase 1 hasn't landed yet — that's Phase 1's territory, not a
      regression here).

**Verification:**
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check` — refusal reasons for the newly-wired
      channels have changed from `no referenceBaseGameUnits` to either "resolved" or
      `no authored sharePermille` (proving this task's own fix landed, independent of Phase 1's state)

**Dependencies:** Task 6

**Files likely touched:**
- `tools/FamilyExpandGen/Program.cs` (edit)

**Estimated scope:** Small (1 file, mechanical)

---

### Task 8: `ssot-power-scale.md` §10 registration + `RulesetVersion` check

**Description:** Register each newly-added channel in `ssot-power-scale.md`'s closed §10 inventory
(channel, unit class, `ChannelDirection` — citing `StatChannels.DirectionOf`, never redeclaring it) in
the same commit as Task 6's code change. Then run the golden/regression suite with the change staged
(`guard-power.ps1` + whatever `Core.Tests` battle goldens exercise `BattleRuleset`) and record an
explicit `RulesetVersion` bump-or-not verdict in `decisions.md`, per spec §6.

**Acceptance criteria:**
- [ ] §10's inventory table has one new row per channel actually added (matches Task 6's real scope,
      not the original hoped-for 5).
- [ ] Golden/regression suite run once with the change staged; result (moved or didn't) is the basis
      for the `RulesetVersion` verdict, not assumed.
- [ ] `decisions.md` gets an explicit new row or an addendum to the existing "Power scale"/"Power dial"
      rows recording the verdict — never left silent either way.

**Verification:**
- [ ] `.\scripts\guard-power.ps1` — OK (one ladder, pin holds, no private `f(level)`)
- [ ] Full `Core.Tests` battle-related suite — no unexplained golden drift (or, if drift occurs, a
      `RulesetVersion` bump is made and re-blessed goldens are committed alongside it)

**Dependencies:** Task 7

**Files likely touched:**
- `docs/architecture/power/ssot-power-scale.md` (edit)
- `docs/architecture/decisions.md` (edit)
- Golden files, only if `RulesetVersion` actually bumps (unlikely per spec §6's own expectation)

**Estimated scope:** Small (mostly documentation + a verification run, not new code)

---

### Task 9: Fix the stale "not bindable yet" doc comment

**Description:** `quickening`/`flourishing`/`swiftness`/`plating`/`carapace`'s own family JSON entries
(`data/seed/items/affix-families/g-*.json`) carry an authoring note claiming their channel is "not
bindable today, pending E16's channel-extension promotion." Found stale during this session's audit —
`AtomKindRegistry.PrimaryChannels` already includes all 5. One-line content fix per family, removing or
correcting the stale claim so a future reader doesn't believe there's a second blocker.

**Acceptance criteria:**
- [ ] Each of the 5 families' own `notes` field no longer claims the channel is unbound — corrected to
      reflect the real, current state (bound, blocked only by the specific gap this program tracks).

**Verification:**
- [ ] `grep -rn "pending E16" data/seed/items/affix-families/` — no remaining hits after the fix
- [ ] No test regression (this is a content/documentation-field change, not a schema change)

**Dependencies:** None (independent of every other task in this plan — can run any time, including
before Phase 1)

**Files likely touched:**
- `data/seed/items/affix-families/g-armour.json`, `g-life.json`, `g-tempo.json` (or wherever the 5
  affected families actually live — confirm exact files when executing)

**Estimated scope:** Small (content-only, no code)

---

### Checkpoint: Phase 2 complete — plan complete
- [ ] All Phase 2 tests pass
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check` reflects the real, final state — 0
      refusals if both phases' scope fully landed, or a precisely-named smaller set if Task 5 could not
      calibrate every channel
- [ ] `spec-battle-ruleset-curve-extension.md`'s own Success Criteria (§5, §7) fully satisfied for
      whatever subset of channels Task 5 actually calibrated
- [ ] `RulesetVersion` verdict recorded in `decisions.md`, either way
- [ ] Review with human — plan complete
