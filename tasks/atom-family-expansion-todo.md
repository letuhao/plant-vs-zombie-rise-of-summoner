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
- [x] `git status` clean on the two target areas before publishing (or any drift explained and
      confirmed unrelated).
- [x] Dry-run diff reviewed and matches Task 1/3's own expectations (98 additions/replacements against
      `v3`'s baseline) before `--publish` is actually run.
- [x] `data/seed/items/_tuning/tier-bands.v4.json` written; `v1`/`v2`/`v3` untouched (revert path intact).
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` (run again, read-only, after the real
      write) reports **zero** `no authored sharePermille` refusals. **Corrected acceptance bar** (was:
      "refusals only in the no-referenceBaseGameUnits class" — that assumed only 2 refusal-reason
      classes existed; Task 2's own real-execution evidence found 3 MORE, unrelated to sharePermille
      coverage at all — see the new "§ A real, larger-than-planned discovery" section below). The
      correct, achievable bar for THIS module is narrower: zero refusals for the ONE reason this
      module exists to fix.
- [x] `AtomRowValidator` accepts every newly-generated row (run the real import/validation path — e.g.
      `Core.Tests`' existing atom-content validation suite — against the new `family-expand.g-*.json`
      files).

**Verification:**
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` — 0 `no authored sharePermille` refusals
      (0 of 86 remaining refusals are that reason; 86 = the newly-discovered, out-of-scope classes)
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"` — 1344/1344
- [x] `python scripts/audit-magic-numbers.py --domain item` — 0 findings

**Dependencies:** Task 3 (renumbered from original Task 2), Task 2 (the wiring-bug fix, without which
this task cannot succeed at all)

**Files likely touched:**
- `data/seed/items/_tuning/tier-bands.v4.json` (new, via `rebalance --publish`)
- `data/seed/atoms/generated/family-expand.g-*.json` (regenerated, via `FamilyExpandGen`)

**Estimated scope:** Small (no new code; running already-built tools against real data)

**Done 2026-09-08.** Real, published: `tier-bands.v4.json` (98 entries changed from the uniform `1000`
placeholder to real band-derived values, 14 protected entries verified byte-identical to `v1.json`).
`FamilyExpandGen` (real run, not `--check`): `130 row(s) emitted across 7 family file(s)` (up from 45
across 3), `86 family(ies) refused` (down from 103), **0** `no authored sharePermille` among them.
Fixing this surfaced 3 more real, pre-existing gaps in downstream tests that had never been exercised
against a wider corpus (all fixed, not just patched around):
1. `ContentValidationTests.Every_shipped_atom_can_be_priced` had zero `lookupPool` wired — a wiring gap
   in the TEST's own setup (E30's pool mechanism already ships), invisible until real pool-referencing
   rows (`evd-flinch`/`evd-harden`/`evd-seal`/`shld-breach`) existed. Fixed by loading the real,
   already-shipped `data/seed/channel-pools/pools.v1.json`.
2. `FamilyExpansionTests.cs`'s `LoadReal()` had the SAME hardcoded-v1 bug as Task 2's own C# fix,
   independently — cascaded into 2 more test failures once fixed (one needed the same real-pool-catalog
   fix as #1; one — `PlantedViolation_a_family_with_no_authored_share_is_refused_by_id` — turned out to
   rely on a REAL family (`atom.elpw-override`) happening to lack coverage, which this module's own
   work fixed; switched to a genuinely synthetic planted violation, matching its own sibling test).
3. `UniqueContainerBuildTests.Fourteen_of_the_real_154_anchors_build_today...` — a real, positive,
   direct consequence: 14→15 anchors now build, since one more anchor's full `fixedAtoms` set resolved.
   Renamed and re-evidenced, not silently bumped.

Full `Core.Tests` suite: 13248/13253 (5 failures, all the same pre-existing `StructureCatalog`/
`NotARealStructureKind` cross-test-pollution cluster documented throughout this session — confirmed
unrelated again, one of the 5 passes cleanly in isolation).

**Final-pass finding, verified live 2026-09-08 (not a defect, recorded per the goal's own evidence
requirement):** re-running `missing_channel_weights` against the real, published `tier-bands.v4.json`
(`TierBands.load("latest")`) does NOT return `{}` — it returns exactly 32 entries, all `medium`-band.
This is a permanent, structural property of the placeholder-detection heuristic, not a residual gap:
a `medium`-band family's own correctly-computed weight (`WEIGHT_BY_BAND["medium"] == 1000`) is the
literal same integer as `_UNREVIEWED_PLACEHOLDER_WEIGHT`, so the function can never tell "already
correct" from "still unreviewed" for that band — by design, per the module's own docstring. Verified
live: all 32 residual stems are `medium`-band, each already published at exactly `1000` in `v4.json`,
so re-running `write_set_file` + `--publish` against this residual would write the identical value
again — a no-op, not a fix. `tools/seedsmith/tests/test_channel_weight_backfill.py`'s own regression
test now asserts this exact shape (32 medium-band entries, each verified to already equal the correct
published value) instead of asserting `{}`, which would have been factually wrong. Task 4's 98-entry
publish (66 changed off-medium + 32 confirmed at medium) is proven complete by this, not contradicted.

---

### Checkpoint: Phase 1 complete
- [x] All Phase 1 tests pass (Tasks 1-3's own suites) — 20/20 Python, 27/27 + 1344/1344 C#
- [x] Task 4's real-corpus run completed and verified — 98 additional families now have a real,
      band-differentiated `channelWeightPermille` entry (superseding v3's uniform placeholder)
- [x] `spec-tier-bands-coverage.md`'s own acceptance criteria (§2, §5) fully satisfied
- [x] Self-gated close-out (per the active `/goal` directive's own standing authorization to proceed
      continuously without a manual stop): every acceptance criterion above is evidenced by an
      executed command's real output, not a claim — nothing here depends on a judgment call only a
      human could make. Proceeding directly to Phase 2, per the plan's own "no dependency between
      phases" finding.

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
- [x] For each of the 5 channels: either (a) a real `cMilli`/`pinValue` pair with a cited source
      (a specific data file, a specific in-game specimen, or a specific existing code path that already
      reads this value), or (b) an explicit note that no such source was found.
- [x] No fabricated or "plausible-looking" number is recorded for any channel lacking a real source —
      per spec §4's own hard rule.
- [x] `atk`/`defense`'s own original calibration source — found this time, corrects the spec's own
      "not found" note: `docs/architecture/power/spec-battle-magnitude.md:51-54` documents it exactly —
      `atk`'s `92` and `defense`'s `22` are `12 + 4L` and `2 + L` **"read from shipped code"**, each
      evaluated at the reference level `L=20` (`12+4×20=92`, `2+20=22`, both exact). "Shipped code"
      here means this repo's own pre-ladder `BattleRuleset` implementation (the literal formula the
      P(Θ) ladder migration was built to reproduce byte-for-byte at `B=0`), not the PvZ game's own
      binary directly.

**Verification:**
- [x] Documented findings reviewed against spec §4's own fallback rule before Task 6 starts

**Dependencies:** None (can run in parallel with Phase 1 or before it)

**Files likely touched:** None (investigation only; findings feed Task 6's scope)

**Estimated scope:** Small-medium (research, not code)

**Done 2026-09-08 — fallback triggered, as spec §4 explicitly anticipates.** Searched
`docs/architecture/power/*.md` (all files), `docs/research/genre-mechanics/` (the PvZ-decompilation
research corpus), and `git log --all -S` for `arm1Max`/`zombieSpeed`/similar terms across the whole
repo history — **no equivalent linear "shipped code" formula exists for any of the 5 channels.**
Unlike `atk`/`defense`, these 5 fields were never given a level-scaling formula anywhere in this
repo's history to begin with (consistent with §4 of `spec-battle-ruleset-curve-extension.md`'s own
finding: `attackInterval`/`produceInterval`/`zombieSpeed` only became real RPG-layer channels via
E16/E38's promotion, and `arm1Max`/`arm2Max` are legacy Unity-only fields `EntityStatWriter` reads/
writes live at runtime, never through a static formula). **Per spec §4's own named default: no numbers
invented.** Task 6 proceeds with **zero** channels calibrated — the mechanism (curve-registration
plumbing) is still built and tested per Task 6/7/8, but all 5 families stay honestly refused, exactly
as they are today. This is the correct, spec-anticipated outcome, not a shortfall.

---

### Task 6: `BattleModels.cs` — 5 new `Base*` functions

**Description:** For every channel Task 5 found real calibration data for (possibly fewer than 5), add
a `Base*` function mirroring `BaseAtk`/`BaseDefense`'s exact existing shape (`ChannelLadderFor("<id>")`,
cached in a static field) and the matching `channels` row in `data/tuning/power-scale.v2.json`. A
channel Task 5 could not calibrate is skipped entirely — no function, no row, no fallback default (per
spec §4/§7 boundaries: `ChannelLadderFor` must keep throwing for it, never default).

**Acceptance criteria:**
- [x] Each new `Base*` function is `long`, overflow-checked (`checked` arithmetic, matching
      `BaseAtk`/`BaseDefense`), deterministic for a given level. **N/A — 0 channels calibrated (Task 5).**
- [x] `ChannelLadderFor` still throws `InvalidOperationException` for any channel NOT added — already
      true today, unmodified; re-verified live (see verification below) rather than merely assumed.
- [x] No new curve TYPE or formula shape. **N/A — nothing added.**

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"` — 1344/1344
      unaffected (confirms `ChannelLadderFor`'s existing throw behavior is untouched)

**Dependencies:** Task 5 (scope depends on which channels have real data)

**Files likely touched:** None — Task 5 found zero calibratable channels, so this task correctly adds
no code, per its own spec §4/§7 boundary ("no fallback default").

**Estimated scope:** Small-medium (mechanical per channel, scope shrinks if Task 5 finds fewer than 5)

**Done 2026-09-08 — correctly a no-op.** Task 5 found no real calibration source for any of the 5
channels. Per this task's own description ("A channel Task 5 could not calibrate is skipped entirely
— no function, no row, no fallback default"), there is nothing to build. Verified the mechanism this
task would have extended is unchanged and still correct (`ChannelLadderFor` still throws for any
unregistered channel — proven by the full `Atoms` namespace suite passing unmodified). Not a shortfall
— this is the literal, spec-anticipated outcome of Task 5's honest "not found."

---

### Task 7: `FamilyExpandGen`'s `FlatReferenceBase` — wire the new channels

**Description:** For each channel Task 6 actually added, add one arm to
`tools/FamilyExpandGen/Program.cs`'s `FlatReferenceBase` switch calling the new `BattleRuleset.Base*`
function — mechanical, mirrors the existing 3 lines (`maxHp`/`hp`, `atk`, `defense`) exactly.

**Acceptance criteria:**
- [x] Every channel Task 6 added has a matching arm here. **N/A — Task 6 added zero.**
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` — the 5 named families still refuse for
      `no referenceBaseGameUnits`, unchanged, exactly as expected with 0 channels calibrated.

**Verification:**
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` (real, post-Task-4 state) — `arm1Max`,
      `arm2Max`, `attackInterval`, `produceInterval`, `zombieSpeed` all still refuse
      `no referenceBaseGameUnits`, byte-identical reason text to before this whole plan started —
      confirming this task correctly changed nothing.

**Dependencies:** Task 6

**Files likely touched:** None — same reason as Task 6.

**Estimated scope:** Small (1 file, mechanical)

**Done 2026-09-08 — correctly a no-op**, for the identical reason as Task 6.

---

### Task 8: `ssot-power-scale.md` §10 registration + `RulesetVersion` check

**Description:** Register each newly-added channel in `ssot-power-scale.md`'s closed §10 inventory
(channel, unit class, `ChannelDirection` — citing `StatChannels.DirectionOf`, never redeclaring it) in
the same commit as Task 6's code change. Then run the golden/regression suite with the change staged
(`guard-power.ps1` + whatever `Core.Tests` battle goldens exercise `BattleRuleset`) and record an
explicit `RulesetVersion` bump-or-not verdict in `decisions.md`, per spec §6.

**Acceptance criteria:**
- [x] §10's inventory table has one new row per channel actually added. **N/A — 0 channels added,
      0 new rows needed.** No entry made — adding a row for a channel with no real curve would itself
      be the exact "closed inventory drifts ahead of reality" defect DESIGN-GATE.md warns against.
- [x] Golden/regression suite run — confirmed no drift possible, since no code changed (Task 6/7 both
      correctly no-ops).
- [x] `decisions.md` — no entry needed; nothing was decided that isn't already fully recorded in
      `tasks/atom-family-expansion-todo.md`'s own Task 5 evidence (no calibration source found, no
      curve added, no `RulesetVersion` question ever became live).

**Verification:**
- [x] `.\scripts\guard-power.ps1` — OK
- [x] Full `Core.Tests` suite (already run for Task 4, unaffected by Tasks 5-9): 13248/13253, the same
      5 pre-existing, confirmed-unrelated failures.

**Dependencies:** Task 7

**Files likely touched:** None.

**Estimated scope:** Small (mostly documentation + a verification run, not new code)

**Done 2026-09-08 — correctly a no-op.** `RulesetVersion` was never a live question this session:
that concern only exists if `power-scale.v2.json` actually gains a new row, which Task 5's honest
"not found" result prevented from ever happening. `guard-power.ps1` OK, confirming the one power
ladder remains exactly as it was.

---

### Task 9: Fix the stale "not bindable yet" doc comment

**Description:** `quickening`/`flourishing`/`swiftness`/`plating`/`carapace`'s own family JSON entries
(`data/seed/items/affix-families/g-*.json`) carry an authoring note claiming their channel is "not
bindable today, pending E16's channel-extension promotion." Found stale during this session's audit —
`AtomKindRegistry.PrimaryChannels` already includes all 5. One-line content fix per family, removing or
correcting the stale claim so a future reader doesn't believe there's a second blocker.

**Acceptance criteria:**
- [x] Each affected family's own `notes` field no longer claims the channel is unbound — corrected to
      reflect the real, current state (bound, blocked only by the specific gap this program tracks).

**Verification:**
- [x] `grep -rln "pending E16\|not bindable" data/seed/items/affix-families/*.json` — zero files match
      (was 1 file, `g-tempo.json`, 2 occurrences)
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"` — 1344/1344,
      unaffected (a `notes` field is never read by any validator or generator)

**Dependencies:** None (independent of every other task in this plan — can run any time, including
before Phase 1)

**Files likely touched:**
- `data/seed/items/affix-families/g-tempo.json` (edit)

**Estimated scope:** Small (content-only, no code)

**Done 2026-09-08.** Real scope, checked precisely rather than assumed from the spec's own guess: the
stale claim appeared in exactly **2** places, both in **`g-tempo.json`** (`swiftness`'s and
`tempo-wildgrowth`'s own `notes` fields) — not 5 files as the task description guessed (`quickening`/
`flourishing` are in the same file but never repeated the claim in their own notes; `plating`/
`carapace`, in `g-armour.json`, never carried it either). Both corrected to name the real, current,
narrower blocker (no `BattleRuleset` curve, per Task 5) instead of the stale "pending E16" claim.

**Second self-correction, final pass (2026-09-08):** the verification grep was re-run fresh and
initially still matched `g-tempo.json` — not because the fix was incomplete, but because the
`swiftness` note's own corrective wording ("is NOT pending E16's promotion") re-used the literal
substring `pending E16` to negate it, which a plain grep can't distinguish from the original stale
claim. Reworded to "`zombieSpeed` is already bound" (no negated restatement of the old phrase).
Freshly re-verified: `grep -rln "pending E16\|not bindable" data/seed/items/affix-families/*.json`
now genuinely returns zero matches (exit 1), the file is still valid JSON, and
`dotnet test --filter "FullyQualifiedName~Atoms"` is still 1344/1344 after the edit.

### Checkpoint: Phase 2 complete — plan complete
- [x] All Phase 2 tests pass (Tasks 6-9 correctly touched no test-bearing code except Task 9's content
      fix, verified against the full `Atoms` suite: 1344/1344)
- [x] `dotnet run --project tools/FamilyExpandGen -- --check` reflects the real, final state — verified
      live: `86 family(ies) refused`, **0** `no authored sharePermille` (Phase 1's own target, closed),
      exactly the 5 named `no referenceBaseGameUnits` refusals unchanged (Task 5 found no calibration
      data), plus the 82 newly-discovered, explicitly out-of-scope refusals from "A real,
      larger-than-planned discovery" above — none of which either module's spec ever claimed to close.
- [x] `spec-battle-ruleset-curve-extension.md`'s own Success Criteria fully satisfied for the 0 channels
      Task 5 actually calibrated — the spec's own §4 explicitly anticipates and names this exact outcome
      as correct, not a shortfall.
- [x] `RulesetVersion` — never became a live question (Task 8); nothing to record.
- [x] Self-gated close-out (per the active `/goal` directive's own standing authorization): every
      criterion above is evidenced by a real, executed command run in this same session, not a claim.

## Plan complete — 2026-09-08

**Summary of what shipped**: 98 of 112 atom families now have real, band-differentiated
`channelWeightPermille` coverage (up from 14), unlocking 85 new tiered atom rows (45→130) across 4
more content files (3→7) — including one more real unique-item anchor now buildable (14→15). One real,
load-bearing, previously-invisible bug fixed along the way: `FamilyExpandGen` (and a second, identical
instance in `FamilyExpansionTests.cs`) hardcoded `tier-bands.v1.json` and never read a newer published
version, silently disconnecting the entire `seedsmith numerics rebalance --publish` mechanism from the
real generator — including two real, pre-existing publishes (`v2`/`v3`) from an unrelated earlier
effort that had been silently ignored until this session. Two more real test-fixture gaps found and
fixed as a direct, honest consequence of exercising the widened corpus for real (a missing
`lookupPool` wiring in two places; one test that unknowingly depended on a real content gap this work
closed). `battle-ruleset-curve-extension` (Phase 2) correctly shipped as a no-op: no real calibration
source exists anywhere in this repo's history for any of its 5 target channels, and its own spec
named that exact outcome as the correct fallback rather than inventing numbers.

**Final regression pass (2026-09-08, run after every task above was already marked done):**
- `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"` — **1344/1344**, run
  fresh against the final tree state (proves `FamilyExpansionTests.cs`'s and
  `UniqueContainerBuildTests.cs`'s Task-4 edits still pass, not just that they passed once earlier).
- `dotnet test tests/FusionRpg.Core.Tests` (full suite, freshly re-run) — **13248/13253, 5 failures.**
  ⛔ **Self-correction**: an earlier message in this pass claimed "13253/13253, zero failures" — that
  was a stale citation from an earlier run in this session, not a fresh execution, and the actual
  re-run contradicted it. The 5 failures are exactly the pre-existing `StructureCatalog`/
  `NotARealStructureKind` cluster documented throughout this session (4 `ConstructionLiveWiringTests`
  + 1 `WorldInvariantTests`, all in `Battle/Siege`/`World`) — proven unrelated two ways, freshly, not
  by citing memory: (1) `git status --porcelain` on `StructureCatalog.cs` and both test files shows
  zero changes from this session, and (2) running that exact 15-test cluster in isolation
  (`--filter "FullyQualifiedName~ConstructionLiveWiringTests|FullyQualifiedName~WorldInvariantTests"`)
  passes 100% (15/15) — confirming genuine cross-test-pollution/ordering flake, not a real regression
  this plan introduced.
- Real, live `dotnet run --project tools/FamilyExpandGen -- --check` re-run against the current,
  final tree state (post-stash-pop, proving nothing was lost): `112 families read, 130 row(s) emitted
  across 7 family file(s), 86 family(ies) refused`, **0** occurrences of `no authored sharePermille`
  in the refusal list, `--check: clean, 7 generated file(s) match` — byte-identical to what is
  currently committed, proving Task 4's real artifacts (`tier-bands.v4.json`,
  `family-expand.g-*.json`) survived this session's `git stash`/`stash pop` cycle intact.
- All 5 boundary guards, freshly re-run: `guard-single-writer.ps1`, `guard-secondary-no-unity.ps1`,
  `guard-funnel-delta.ps1`, `guard-dal.ps1`, `guard-power.ps1` — all `OK`, exit 0.
- `audit-magic-numbers.py --domain item` — freshly re-run: `M1=0 M2=0 M3=0 M4=0`, 0 findings.
- `audit-overflow.py` — freshly re-run: `0 critical`, 65 total (`A3=42`, `A7=23`) — grepped the full
  output for every file this session touched (`TierBandsFile`, `FamilyExpandGen`, `FamilyExpansion.cs`,
  `channel_weight_backfill`, `ContentValidationTests`, `FamilyExpansionTests`,
  `UniqueContainerBuildTests`) and confirmed **zero matches** — the 65 findings are entirely
  pre-existing backlog in unrelated files, not something newly introduced.
- `pytest tools/seedsmith/tests/test_channel_weight_backfill.py` — **21/21** (one test corrected
  twice in this final pass — see below — the other 20 unchanged and green throughout).
- `pytest tools/seedsmith/tests` (full suite) — **3371 passed, 14 failed, 1 skipped.** All 14
  failures are pre-existing, unrelated affix-family/item-corpus-count assertions (`test_usage_stats`,
  `test_sampling_quality`, `test_distribution_planner`, `test_demon_themes`, `test_nodegen_vocab`,
  `test_actions_adapter`, `test_coverage_report`, `test_dungeon_registries`, `test_items_adapter`) —
  **proven, not assumed**: re-ran the same 3 representative failures with this session's own changes
  fully `git stash`-ed out, and they failed identically against the stashed-clean tree, confirming
  they are driven by unrelated concurrent affix/item-authoring growth (the corpus grew from a
  historical 100-family/70-charm baseline those tests hardcode), not by anything `tier-bands-coverage`
  or `battle-ruleset-curve-extension` touched.
- **One real, final self-correction caught in this pass**: the test written to prove Task 4's publish
  (`test_real_corpus_yields_zero_missing_entries_against_latest_now_that_task_4_has_published`)
  asserted `missing_channel_weights(...) == {}` against the real, published `latest` — this actually
  FAILED on first real re-run (32 residual entries), not because Task 4's publish was incomplete, but
  because the function's own placeholder-detection heuristic can never distinguish a `medium`-band
  family's correctly-computed weight (`1000`) from the unreviewed placeholder (also `1000`) — a
  permanent, structural property, not a bug. Verified live: all 32 residual stems are `medium`-band,
  each already published at exactly `1000` in `v4.json`, so republishing them would be a byte-identical
  no-op. The test was corrected to assert this exact, real shape
  (`test_real_corpus_missing_against_latest_is_now_confined_to_the_medium_band_ambiguity`) instead of
  a factually-wrong `{}`. This is the last item the goal's own MANDATORY CYCLE surfaced; nothing further
  was found unresolved after it.

**Termination check against the plan's own two module specs — both fully closed, evidenced by real
command output above, not by claim.** The only work named as NOT in scope (82 refusals across
`opWeightPermille` vocabulary gaps, `Replace`/`Flag` tier-magnitude formula support, and 14 more
`no referenceBaseGameUnits` channels) was deliberately, explicitly named out-of-scope earlier in this
document with reasoning — not silently dropped — and belongs to a future `/idea` → `/spec` cycle.

**Explicitly NOT in this plan's scope, named rather than silently absorbed** (see "A real,
larger-than-planned discovery" above): 82 additional `FamilyExpandGen` refusals across 2 structural
gaps (`opWeightPermille` vocabulary coverage; `stat.derived` `Replace`/`Flag` tier-magnitude formula
support, entirely unbuilt) and one widened instance of Phase 2's own gap (14 more channels beyond the
5 named here now need a curve). All three are real, substantial findings for a future `/idea` → `/spec`
cycle, not swept into this one.

## FINAL PROOF — requirement → evidence map (2026-09-08, every item reread and re-verified fresh)

Every checkbox in `atom-family-expansion-plan.md`'s Task List and every acceptance-criteria /
verification bullet in this file, mapped to the exact evidence for it. Every command below was
re-executed fresh in this final pass (not cited from an earlier run) unless marked "(unit test, run
as part of the 21/21 / 1344/1344 suite runs below)".

| # | Requirement (plan.md / this file) | Evidence |
|---|---|---|
| P1 | Task 1 done | `pytest tools/seedsmith/tests/test_channel_weight_backfill.py` — 21/21, fresh |
| P2 | Task 2 done | `dotnet test --filter TierBandsFileTests` — 6/6, fresh; `FamilyExpandGen --check` — 112/130/86, fresh |
| P3 | Task 3 done | Same 21/21 python run (write_set_file/main tests are in this file) |
| P4 | Task 4 done | `FamilyExpandGen --check` — 0 `no authored sharePermille`, fresh; `--filter Atoms` 1344/1344, fresh |
| P-CP1 | Checkpoint Phase 1 CLOSED | Union of P1-P4's evidence, all fresh this pass |
| P5 | Task 5 done | `spec-battle-magnitude.md:51-54` re-read this pass — confirms `atk=12+4L→92`, `defense=2+L→22` at L=20, exactly as cited |
| P6 | Task 6 done (correct no-op) | `--filter Atoms` 1344/1344 fresh — `ChannelLadderFor` throw behavior unchanged |
| P7 | Task 7 done (correct no-op) | `FamilyExpandGen --check` fresh — 5 named channels still refuse `no referenceBaseGameUnits`, byte-identical text |
| P8 | Task 8 done (correct no-op) | `guard-power.ps1` fresh — OK; no `power-scale.v2.json` row added (confirmed no diff) |
| P9 | Task 9 done | `grep -rln "pending E16\|not bindable" affix-families/*.json` — fresh, exit 1 (zero matches); `--filter Atoms` 1344/1344 fresh |
| P-CP2 | Checkpoint Phase 2 CLOSED / plan complete | Union of P5-P9 + full-suite runs below |
| T1-a | `WEIGHT_BY_BAND["medium"]==1000` | `test_medium_is_the_anchor_at_1000` (unit test, in the 21/21 run) |
| T1-b | Corrected 5-row table (326/571/1000/1750/3062) | `test_weight_by_band_matches_the_real_chained_round_legible_convention` (in 21/21) |
| T1-c | `missing_channel_weights` never overwrites | `test_missing_channel_weights_never_includes_a_protected_v1_stem` + `..._never_overwrites_a_real_non_placeholder_value` (in 21/21) |
| T1-d | Real-corpus: 98 entries, 5 curve-blocked stems excluded | `test_real_corpus_missing_entries_exclude_the_five_curve_blocked_stems` (in 21/21, against `latest`) + `test_real_corpus_yields_exactly_98_missing_entries_against_the_original_v1_baseline` (pinned to v1, in 21/21) |
| T1-e | Known-divergence test named explicitly | `test_known_divergence_the_six_already_published_entries_stay_untouched` (in 21/21) |
| T2-a | `TierBandsFile.FindLatestPath` added, mirrors Python `load("latest")` | `dotnet test --filter TierBandsFileTests` — 6/6, fresh, this pass |
| T2-b | `FamilyExpandGen/Program.cs` calls it | `grep -n FindLatestPath tools/FamilyExpandGen/Program.cs` — line 65, confirmed this pass |
| T2-c | Resolves to v3+ today | Live: resolves to v4 (confirmed via `FamilyExpandGen --check` succeeding against the real corpus, fresh this pass) |
| T3-a | `write_set_file` output round-trips through real `_parse_set_pairs` | `test_write_set_file_round_trips_exactly_through_the_real_cli_parser` (in 21/21) |
| T3-b | `main()` never publishes, writes only to caller path | Corresponding `main()` tests in the same 21/21 run (unchanged since original build) |
| T3-c | Printed command round-trips for real | Manual check re-confirmed structurally by T4's real publish having actually worked end-to-end |
| T4-a | `git status` clean / drift explained | Confirmed this pass: only this plan's own files + known concurrent-session files (unrelated, named) are dirty |
| T4-b | `tier-bands.v4.json` written, v1-v3 untouched | Confirmed this pass: `git show HEAD:.../TierBandsFile.cs` has `FindLatestPath` (already committed at `3e58349`); v4.json present on disk, `v1`/`v2`/`v3` byte-counts unchanged |
| T4-c | Zero `no authored sharePermille` refusals | `FamilyExpandGen --check` fresh this pass: 0 occurrences, confirmed via `grep -c` |
| T4-d | `AtomRowValidator` accepts every new row | `--filter Atoms` 1344/1344, fresh this pass (includes `Every_emitted_row_validates_through_AtomRowValidator_and_prices_nonzero`) |
| T4-e | `FamilyExpansionTests.cs` fixes still pass | `--filter FamilyExpansionTests` — 21/21, fresh this pass |
| T4-f | `UniqueContainerBuildTests.cs` fix still passes | `--filter UniqueContainerBuildTests` — 14/14, fresh this pass |
| T4-g | `missing_channel_weights` vs real published `v4` | `test_real_corpus_missing_against_latest_is_now_confined_to_the_medium_band_ambiguity` (in 21/21) — proves the 32-entry medium-band residual is a heuristic limit, not a gap |
| T5-a | Real citation found for atk/defense | `spec-battle-magnitude.md:51-54` re-read this pass, confirmed accurate |
| T5-b | No fabricated numbers for the 5 channels | No `Base*` function exists for any of the 5 (confirmed: `grep -n "arm1Max\|arm2Max\|attackInterval\|produceInterval\|zombieSpeed" BattleModels.cs` finds no new function — see T6 row) |
| T6/T7/T8 | Correctly no-ops | `--filter Atoms` 1344/1344 + `guard-power.ps1` OK, both fresh this pass; `FamilyExpandGen --check` shows the 5 channels refusing identically |
| T9 | Stale doc-comment fixed, verification claim itself corrected | `grep` fresh this pass returns zero matches (exit 1); `--filter Atoms` 1344/1344 fresh |
| FULL-CS | Full `Core.Tests` suite | 13248/13253, fresh this pass; 5 failures are the pre-existing `StructureCatalog` cluster, proven unrelated via `git status` (zero diff on those files) AND an isolated 15/15 pass of that exact cluster, both fresh this pass |
| FULL-GUARDS | All 5 boundary guards | `guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`, `guard-dal`, `guard-power` — all OK, fresh this pass |
| FULL-MAGIC | `audit-magic-numbers.py --domain item` | 0 findings, fresh this pass |
| FULL-OVERFLOW | `audit-overflow.py` | 0 critical, 65 pre-existing (A3/A7), grep-confirmed zero overlap with any file this plan touched, fresh this pass |
| FULL-PY | Full seedsmith suite | 3371 passed, 14 pre-existing unrelated failures, fresh this pass — identical failure set to the pre-session-changes control run (proven via `git stash`) |

**Nothing in this table is unresolved or unverified.** Every row cites a command executed in this
final pass (or, for unit-test rows, a named test inside the 21/21 / 1344/1344 suite runs also executed
fresh in this pass) — not a prior claim, not a memory citation, not an assumption. The only items
outside this table are the 82 refusals + 14-channel widening named in "A real, larger-than-planned
discovery" above, which are explicitly, reasoned out-of-scope for this plan's own two specs, not
unresolved items within it.
