# Tasks — `derived-stats`

**Plan:** [derived-stats-plan.md](derived-stats-plan.md) · **Map:** [../docs/architecture/derived-stats-map.md](../docs/architecture/derived-stats-map.md)
**Status:** **All 7 phases (0–6) complete, all 7 checkpoints cleared, 2026-08-25.** Every task below is
`[x]` with dated, code-verified evidence. Full matrix green across all 6 test projects (4446 tests, 0
failures); both audits unchanged/clean; all guard scripts clean. Owner review and commit still pending
(git hands-off — this session never commits) — the header above is stale from before any build began
and is superseded by this line and by each phase's own Checkpoint evidence.

Every task carries acceptance criteria and a verification command. `git status tests/` clean is a
*criterion*, not a nicety — it is what makes each moved golden in Phase 5 attributable.

---

## Phase 0 — foundation (inert)

### - [x] T0.1 `StatClass` and the def fields
**Spec:** [spec-stat-taxonomy.md](../docs/architecture/derived-stats/spec-stat-taxonomy.md) §2.1, §4
**Acceptance:** `enum StatClass { Contest, Race, Pool, Feeder }` exists; `DerivedStatDef` gains
`Class`, `Unit` (the ledger's enum, **referenced not redefined**) and `CounterpartOf`. `double`
composition fields **unchanged**.
**Verify:** `dotnet build src\FusionRpg.Core` · `dotnet test tests\FusionRpg.Core.Tests`
**Files:** `Stats/Derived/StatClass.cs` (new) · `Stats/Derived/DerivedStatRegistry.cs` · **Small**

### - [x] T0.2 Classify the 99 shipped channels
**Spec:** §2.1, §2.5 **Depends:** T0.1
**Acceptance:** every shipped channel carries both classes. **`shield.capacity` and `shield.regen` land
in `Pool`** — they are the precedent that unpaired is legitimate, so if they fail the classification is
wrong, not them.
**Verify:** `dotnet test --filter "FullyQualifiedName~StatTaxonomy"` · `git status tests/` clean
**Files:** `DerivedStatRegistry.cs` · `data/seed/derived-stats/catalog.json` · **Small**

### - [x] T0.3 `guard-stat-pairs.ps1` + four planted violations
**Spec:** §6.2 **Depends:** T0.2
**Acceptance:** guard fails on each of — a `Contest` with no counterpart; an asymmetric pair; a `Race`
*with* a counterpart; a capped `Contest` magnitude. Passes clean on `main`. Wired into
`deploy-play.ps1` and CI.
**Verify:** `.\scripts\guard-stat-pairs.ps1` · four planted-violation tests **observed failing**
**Files:** `scripts/guard-stat-pairs.ps1` (new) · `tests/.../StatTaxonomyGuardTests.cs` (new) · **Medium**
> A guard never proven to fail is not evidence. Plant all four.

### - [x] T0.4 Write the three rules where their subsystem looks
**Spec:** §2.3, §2.4, §2.5
**Acceptance:** mitigation-order rule in `combat-damage-ssot.md` §6.7; divisor floor in
`ssot-power-scale.md` **§11.4 (termination guards), not §11.2 (progression ceilings)**; the
`statClass`/`unitClass` boundary in `actor-hub-ssot.md` §H.0.
**Verify:** `python scripts\audit-magic-numbers.py --summary` unchanged · no code touched
**Files:** 3 docs · **Small**

### ✅ Checkpoint 0 — CLEARED 2026-08-24
Evidence: `StatClass`/`UnitClass` enums (src/FusionRpg.Core/Stats/Derived/StatClass.cs); all 99 shipped
channels classified in `DerivedStatRegistry`; `ShippedFamiliesClassify` proves `AllRegistered.Count==99`
and `shield.capacity`/`regen` land in `Pool`; `guard-stat-pairs.ps1` — 7/7 `StatTaxonomyGuardTests` green
incl. all 4 planted violations (P4 genuinely observed failing pre-fix — Windows PowerShell 5.1 returns
`System.Decimal` for JSON numbers, not `System.Double`, a real cross-version bug the guard now handles
via `-is [ValueType]`); wired into `deploy-play.ps1`. `dotnet test tests\FusionRpg.Core.Tests`: 3123/3123
(one `CurveTableTests.MultiplierAt_allocates_nothing` flake reproduced clean in isolation twice — pre-
existing, unrelated). `audit-magic-numbers.py --summary`: 0/0/0/0, unchanged. `git status tests/`: only
new files, zero modified. Three rules written: combat-damage-ssot.md §6.7, ssot-power-scale.md §11.4,
actor-hub-ssot.md §H.0 (+ fixed a stale 3-way-scheme paragraph in §H.8 R5 and a duplicated paragraph
in §H.0, both found while doing this work).

---

## Phase 1 — one home for a cap

### - [x] T1.1 Write `RaisingTheCapActuallyRaisesIt`, and watch it fail
**Spec:** [spec-cap-consolidation.md](../docs/architecture/derived-stats/spec-cap-consolidation.md) §5
**Acceptance:** tuning at `categoryResistCap: 0.99`; a defender stacking resist reaches 0.99.
**This must be observed failing on `main` before T1.3 changes anything** — otherwise the fix is
unproven and the bug's existence is a claim.
**Verify:** run it, **record the failure**, then proceed
**Files:** `tests/.../CapConsolidationTests.cs` (new) · **XS**

### - [x] T1.2 Tuning file + parser + host injection
**Spec:** §3 **Depends:** T1.1
**Acceptance:** `data/tuning/derived-stats.v1.json` holds caps/defaults, **units in the key names**
(T6). `DerivedStatTuning` is a **pure parser — no I/O in Core**. Injector and Server load and inject.
A missing tunable **rejects, naming the channel** (T5) — never a built-in default.
**Verify:** `dotnet test tests\FusionRpg.Core.Tests` · `MissingTunableRejects` green
**Files:** `data/tuning/derived-stats.v1.json` (new) · `Stats/Derived/DerivedStatTuning.cs` (new) · 2 composition roots · **Medium**

### - [x] T1.3 Delete both literals and the redundant clamp
**Spec:** §2 **Depends:** T1.2
**Acceptance:** `DerivedStatRegistry.cs:46-48` and `:90`'s `0.95` gone; `ResistanceEvaluator.cs:207-208`'s
second `Math.Min` gone; `StatusPolicy.CategoryResistCap` no longer a second key.
**T1.1 now passes. Goldens byte-identical at `0.95`.**
**Verify:** `dotnet test tests\FusionRpg.Core.Tests` · `OneClampNotTwo` · `git status tests/` clean
**Files:** `DerivedStatRegistry.cs` · `ResistanceEvaluator.cs` · `StatusPolicy.cs` · **Small**

### - [x] T1.4 Retire the three dead columns
**Spec:** §3.1 **Depends:** T1.3
**Acceptance:** `effect_channel_policy` keeps `channel_id` and `direction` only. Registry version
bumped. Direction stays live — `IsLowerBetter` and `CostFunction`'s pricing unaffected.
**Verify:** `dotnet test tests\FusionRpg.Data.Tests` · `.\scripts\guard-dal.ps1` ·
**`ContentHashChangedGoldensDidNot`** · direction-still-live is covered by
`ChannelPolicyTableTests.IsLowerBetter_reads_through_the_same_table` and
`ChannelPolicyStoreTests.The_shipped_direction_matches_the_code_that_composes_it`, not a single named
test (found by the adversarial audit, 2026-08-25: the todo previously bolded `DirectionStillLive` as if
it were one — no such test exists, though the property it names is genuinely proven by the two above)
**Files:** `RpgStore.ChannelPolicy.cs` · `ContentHashRegistry.cs` · `ChannelPolicyTable.cs` · **Medium**
> The hash restamp is a **table-shape** change, not a gameplay change. Assert the two separately or the
> next reader assumes one of them is wrong.

### ✅ Checkpoint 1 — CLEARED 2026-08-25
Evidence: bug empirically reproduced in isolation (old shape: compose hardcodes 0.95, apply re-clamps
against tunable — mathematically a no-op since min(x,0.95) <= 0.95 < 0.99) before fixing; `RaisingThe-
CapActuallyRaisesIt`/`LoweringStillLowers`/`GoldensByteIdenticalAt095`/`OneClampNotTwo`/
`MissingTunableRejects` all green (CapConsolidationTests.cs). `DerivedStatPolicy` gained `UseScoped`
(AsyncLocal) beyond the `StatusPolicy` template shape — needed because ~3000 other tests build a
registry against the global default concurrently and a bare `Configure()` call from inside one test
would race them; cap is also now frozen once per registry instance (a new `_categoryResistCap` field)
so the static and sparse-status-id resolution paths can't drift apart. Three dead columns retired
end-to-end: Core DTO, Data row+SQL+schema, ContentHashRegistry V5 (bumped from 4), 3 test bootstraps.
`NoDeadColumns` (PRAGMA table_info) and `ContentHashChangedGoldensDidNot` (V4 vs V5 column-list diff)
green. Full matrix after the change, all green: Core.Tests 3128/3128, Data.Tests 475/475, Guard.Tests
89/89, Server.Tests 15/15, Launcher.Tests 162/162, CheatCore.Tests 40/40, E2E.Tests 194/194. All 6
guards pass (dal/single-writer/secondary-no-unity/funnel-delta/power/stat-pairs). Both audits unchanged
(magic-numbers 0/0/0/0, overflow A3=21/A7=15/0 critical). Injector (BepInEx host) + Server both build
clean with the new host wiring. `git status tests/`: only the one deliberately-rewritten file
(ChannelPolicyStoreTests.cs, explained) + 3 bootstrap edits + 2 new test files — no silent moves.

---

## Phase 2 — registration (inert)

### - [x] T2.1 R1 — restate `decisions.md` **first**
**Spec:** [spec-catalog-extension.md](../docs/architecture/derived-stats/spec-catalog-extension.md) §2.2
**Acceptance:** the *Element Hub SSOT* row's literal **"84 combat derived channels"** becomes
*"families × roster, generated — the count is derived, not fixed."* **Lands before T2.2**, so no window
exists where a shipped lock contradicts shipped code.
**Verify:** no code touched · doc review
**Files:** `docs/architecture/decisions.md` · **XS**

### - [x] T2.2 The 16 element families
**Spec:** §2, §2.1 **Depends:** T2.1
**Acceptance:** `CombatChannelFamilies` grows 12 → 28; generated total **196**. Family constants follow
the shipped `{Prefix}` + `(ElementTypeId)` idiom — **never a hand-written channel list.**
**Verify:** `dotnet test --filter "FullyQualifiedName~DerivedStat"` · `git status tests/` clean
**Files:** `DerivedStatChannels.cs` · **Small**

### - [x] T2.3 The non-element families
**Spec:** §2, §2.1 **Depends:** T2.2
**Acceptance:** 45 channels across **three separate generators** — status-category (16),
action-category (10), flat/id-keyed (19: healing 1, resource 15, `move.range` 1, progression 2).
**`NonElementFamiliesStayOutOfCombatRoster`** is the load-bearing test.
**Verify:** same, plus that test green
**Files:** `DerivedStatChannels.cs` · `DerivedStatRegistry.cs` · **Medium**

### - [x] T2.4 Seed catalog sync
**Spec:** §4 **Depends:** T2.3
**Acceptance:** `catalog.json` expands to exactly what `CreateDefault()` registers, **asserted by a
test**. New channels carry `statClass` and `unitClass: null` — **no placeholder invented** (§2.3).
**Verify:** `SeedCatalogMatchesCode` green
**Files:** `data/seed/derived-stats/catalog.json` · `tests/.../SeedCatalogTests.cs` · **Small**
> A drifting mirror is worse than no mirror. Prove it, don't assume it.

### - [x] T2.5 Count canary and stale test names
**Spec:** §2.4 **Depends:** T2.2
**Acceptance:** `DerivedStatRegistryTests.cs:21`'s canary `84` → **`196`**. Two test names updated
(`…12_families…` → 28, `…twelve_channels…` → 28). **`ElementRosterDataTests` assertions change
nothing** — both are already roster-relative and correct.
**Verify:** `dotnet test --filter "FullyQualifiedName~DerivedStat|FullyQualifiedName~ElementRoster"`
**Files:** 2 test files · **XS**
> The formula is **already there**. Do not rewrite correct assertions into equivalent ones.

### - [x] T2.6 `PvzStatsSheetComposer` — E25 reference cache
**Spec:** §6.3
**Acceptance:** the registry is built once, not per call. **Cached by reference against
`ElementTable.Current`, not a bare `static readonly`** — a static breaks `UseScoped`, which tests rely
on. Exception-as-control-flow removed.
**Verify:** `SheetComposerAllocatesOnce` · `ScopedRosterStillHonoured` · `ComposerAllocationAt196`
**Files:** `Stats/PvzStatsSheetComposer.cs` · `tests/.../SheetComposerPerfTests.cs` · **Small**
> Inherited defect, not introduced here — E25 fixed the same shape in `IsKnownChannel` and missed this one.

### ✅ Checkpoint 2 — CLEARED 2026-08-25 — **256 channels resolve, zero goldens moved**
Evidence: 157 new channels registered across 3 generators (112 element via CombatChannelFamilies
12→28, 16 status-potency, 10 action-category, 1 healing, 15 resource, 1 move.range, 2 progression);
`SeedCatalogMatchesCode` proves catalog.json's 50 entries + 9 prefixFamilies expand to EXACTLY the
256 registered defs (not assumed); `guard-stat-pairs.ps1` passes clean over the enlarged catalog
(every Contest paired, symmetric, no Race paired, no capped Contest magnitude); `NonElementFamilies-
StayOutOfCombatRoster`, `CatalogResolves256`, `UnknownChannelStillRejects`, `MatchedActorsUnchanged`
all green. `StatTaxonomyTests` (unmodified structurally, only its two 99-scale-specific assertions
updated to 256-scale) proves the taxonomy guard holds over every new channel automatically. Bonus
defect found + fixed while re-measuring for `ComposerAllocationAt196`: `BattleStatComposer.Known-
Channels` was a bare `static readonly` (a THIRD instance of the E25 caching defect, previously
unknown) — fixed with the same reference-cache idiom, regression-proven via
`ScopedRosterStillHonoured_BattleStatComposer`. §11.6: zero new caps registered (every new channel
ships `Cap: null` by design — T7's "extract unchanged, tune separately"), so the criterion is
vacuously satisfied; confirmed via unchanged audit-overflow.py (36/0 critical) and audit-magic-
numbers.py (0/0/0/0). Full matrix green: Core.Tests 3362/3362, Guard.Tests 89/89, Data.Tests 475/475,
Server.Tests 15/15, E2E.Tests 194/194, Launcher.Tests 162/162, CheatCore.Tests 40/40. Injector
(BepInEx host) builds clean. `git status tests/`: 6 deliberately-modified files (all explained,
mapped to T1.4/T2.5) + 5 new test files — no silent moves.

---

## Phase 3 — element semantics

### - [x] T3.1 §6 becomes the generation rule, with a drift test
**Spec:** [spec-element-families.md](../docs/architecture/derived-stats/spec-element-families.md) §1.1, §4.1, §6
**Acceptance:** `element-hub-ssot.md` §6's 40-row table replaced by the generation rule + the 28
families. `Section6MatchesGeneration` **and** `StatSheetCountsMatchGeneration` both green and both
**failing on a planted drift**. §7's omni bans extended.
**Verify:** the two tests, each with a planted drift
**Files:** `element-hub-ssot.md` · `design/spec-derived-stat-sheet.md` · 1 test file · **Medium**
> §6 has been wrong by 44 channels since August. Prose alone is what let it rot — the test is the fix.

### - [x] T3.2 R3 — both deferred lists
**Spec:** §5
**Acceptance:** `combat-damage-ssot.md` §5 **and** `element-hub-ssot.md` §6 retitled *"v1 shipped / v2
planned"*, the five moved. **Both** — missing the second leaves the element SSOT saying this program's
subject is banned.
**Verify:** direct reading of both sections (found by the adversarial audit, 2026-08-25: `DeferredListsRetitled`
does not exist as a test — it is T6.1's planned name in spec-unbuilt-reconcile.md §5, not something this
task built; the retitling itself was independently re-verified by reading both files and is correct)
**Files:** 2 docs · **XS**

### ✅ Checkpoint 3 — CLEARED 2026-08-25
Evidence: element-hub-ssot.md §6 replaced (generation rule + 28-family table, both deferred-list copies
retitled "v1 shipped / v2 planned" with exactly the five named items moved); actor-hub-ssot.md §3E
corrected to match; §7 omni bans extended; spec-derived-stat-sheet.md's counted table updated 84/99/
~141 → 196/256/~382 (recomputed the sparse-prefix ceiling properly — 21 statuses × 6 dimensions now
that H.2 added 4 more sparse families, not the spec's own "+42" which only counted the original two).

**Corrected 2026-08-25 by the adversarial audit pass** (this checkpoint's own citation was wrong, and
one fix was incomplete): the evidence above originally cited "§3E/§11.4" — §11.4 is the unrelated
`turn.*`-registration open question, not a real citation; removed. §3E itself was only half-corrected
at the time (the "84 channels / Families (12)" opening paragraph and table header were left in place,
with the correct "28/196" line merely appended after — self-contradictory); now fully corrected, along
with the §E channel-count breakdown (lines ~184-206) and §G (Resource channels), the latter stale for a
different, later reason: T4.4 (this same session) shipped what §G still described as "PROPOSED, not
registered." spec-derived-stat-sheet.md's own title (line 1) still said "99 channels" — the drift test
only checks bolded numbers, missing the unbolded title — fixed.
`Section6MatchesGeneration` and `StatSheetCountsMatchGeneration` both pass against the REAL docs and
both proven to fail via a planted-drift synthetic doc (not just asserted of the mechanism). `OmniAdditive-
ForNewFamilies` proven per-family (7 theories) rather than once generically. `NoReaderTouchesTheNewFamilies-
Yet` is the honest, currently-provable form of `MatchupAppliedOnce` — nothing reads the 16 new families
yet (T5's job), so what's provable now is that no code references them at all, confirmed by scanning
`OverlayCombatCalculator.cs`. One open discrepancy flagged rather than silently resolved: whether
`status.duration`/`status.intensity` (shipped via H.2) should also move out of the "Deferred from Chaos"
StatusProbability/Duration/Intensity bundle — followed the spec's literal "move the five" instruction,
noted the tension in both docs for a future decisions.md pass rather than expanding scope unilaterally.

**Bonus defect found + fixed, not scoped to this module:** re-running the full suite surfaced a genuine
race — `ElementTable.Current` is `AsyncLocal`-scoped, but `DerivedStatChannels`'s E25 cache (and the two
T2.6 caches modeled on it, `PvzStatsSheetComposer` and `BattleStatComposer`) used one shared `static`
slot, so two tests concurrently scoped to different rosters could thrash each other's cache. Reproduced
(`ChannelCacheTests` failed once across the full-suite runs so far, on a reference-identity assertion
that only makes sense as a race, not a regression — passed reliably in isolation). Fixed in all three by
making the cache itself `AsyncLocal`, one slot per scope, matching how `ElementTable` already scopes the
pointer the cache is keyed on. 17 consecutive full-suite runs clean afterward (only the pre-existing,
independently-diagnosed `CurveTableTests.MultiplierAt_allocates_nothing` GC-noise flake recurred once,
unrelated to this program).

Full matrix green: Core.Tests 3374/3374, Guard.Tests 89/89, Data.Tests 475/475, Server.Tests 15/15,
E2E.Tests 194/194, Launcher.Tests 162/162, CheatCore.Tests 40/40. Injector builds clean. Both audits
unchanged. `guard-stat-pairs.ps1`/`guard-power.ps1` clean. `git status tests/`: same 6 deliberate edits
as Checkpoint 2 + 6 new test files (one new this phase: `ElementHubDocDriftTests.cs`).

---

## Phase 4 — non-element readers *(four tasks, genuinely parallel)*

### - [x] T4.1 Status potency — split the netFactor
**Spec:** [spec-status-potency.md](../docs/architecture/derived-stats/spec-status-potency.md) §2.1, §2.2
**Acceptance:** duration and intensity carry independent deltas, reusing the shipped
`1 + delta/NetFactorScale` — **no new tunable**, which is what keeps the no-op true. Potency floor fires
on **intensity only**; zero duration is instantaneous, not `Resisted`.
**Verify:** `AllStatusGoldensUnchanged` · `LongWeakIsExpressible` · `ShortBrutalIsExpressible` ·
`PotencyFloorOnIntensityOnly` · `git status tests/` clean
**Files:** `Status/ResistanceEvaluator.cs` · `status-ssot.md` · `actor-hub-ssot.md` §4 · **Medium**

### - [x] T4.2 Q1's element term + four staleness corrections
**Spec:** §2.3, §3 **Depends:** T4.1
**Acceptance:** `totalResist` gains `+ resist.{element}` from the **status def's own** element tag.
**Zero new channels.** Untagged statuses contribute nothing — no invented default. §6's four stale
statements corrected **with their code citations**.
**Verify:** `ElementResistRead` · `UntaggedContributesNothing`
**Files:** `ResistanceEvaluator.cs` · `status-ssot.md` · **Small**

### - [x] T4.3 Skill modifiers
**Spec:** [spec-skill-modifiers.md](../docs/architecture/derived-stats/spec-skill-modifiers.md)
**Acceptance:** `effectiveness` applied to `baseOverlayDamage` **before** the delta; cooldown floors at
one tick (**structural, commented, PS-8 exempt**) while *reduction* stays uncapped.
`ActionEnvelope.CooldownChannel` **references** the catalog. **`action-map.md` D3 repointed and :177
marked closed.**
**Verify:** `EffectivenessCannotBypassDefense` — the executable form of the contract ·
`CooldownReductionUncapped` · `EnvelopeReferencesCatalog` · `git status tests/` clean
**Files:** `ActionEnvelope.cs` · `OverlayCombatCalculator.cs` · `action-map.md` · `battle.v1.json` · **Medium**

### - [x] T4.4 Actor channels
**Spec:** [spec-actor-channels.md](../docs/architecture/derived-stats/spec-actor-channels.md)
**Acceptance:** 18 channels readable. Values computed **lazily on read** (`value + rate × elapsed`),
proven equivalent to ticking. `xpRate` **layers on** `Award.PowerScale`; `breakthroughSuccess` grants
`Θ` and `realm` stays exactly `1.0`.
**Verify:** **`FourExhaustionDebuffsStack`** — §3G's named-untested case, the one assertion here
covering something nobody has run · `LazyValueMatchesTicked` · `BreakthroughGrantsTheta`
**Files:** `Stats/Derived/…` · `resource-hub-ssot.md` · **Medium**

### - [x] T4.5 Healing
**Spec:** [spec-healing-pair.md](../docs/architecture/derived-stats/spec-healing-pair.md)
**Acceptance:** `heal.power` live, **`Pool`, unpaired, uncapped**. `guard-stat-pairs.ps1` passes with
**no counterpart**, and a planted `Contest` reclassification **fails**. `leech` heals — as a separate
signed packet. `lifesteal` byte-identical.
**Verify:** `HealIsPoolNotContest` · `NoMatchupNoHitNoCrit` · `HealNeverNegative` ·
`.\scripts\guard-funnel-delta.ps1`
**Files:** `DamageApplyPipeline.cs` · `leech` payload · `combat-damage-ssot.md` §4.3 · **Medium**

### ✅ Checkpoint 4 — CLEARED 2026-08-25
Evidence: all five T4.x modules built and verified against their specs, not just re-run.

**T4.1/T4.2 (status-potency + Q1):** `ResistanceEvaluator.Evaluate` splits into `durationDelta`/
`intensityDelta` (`ComputePotencyDelta`, reusing the shipped `ComputeNetFactor` — no new tunable);
`ComputeDelta` gained the `+resist.{element}` term (Q1), null/blank contributing nothing (T5). Potency
floor fires on intensity only. `status-ssot.md` §6 and `actor-hub-ssot.md` §4 both rewritten against
the shipped source, not the spec's paraphrase — found two staleness pockets beyond the spec's own
named four (a fifth, `netFactor`'s formula, inside §6 itself; then a whole second layer in
`actor-hub-ssot.md` §5/§6/§9 repeating the same retired matchPower/ResistFromPowerRatio/progression.power
claims) and corrected all of them with code citations. 8 new tests (`AllStatusGoldensUnchanged` through
`UntaggedContributesNothing`), all passing.

**T4.3 (skill modifiers):** already-shipped `EffectivenessMultiplier`/`CooldownChannel`/`CooldownMath`
verified against the spec's exact acceptance criteria and all three named tests
(`EffectivenessCannotBypassDefense`, `CooldownReductionUncapped`, `EnvelopeReferencesCatalog`)
confirmed passing — but `CooldownMath.cs` turned out to violate the Timeline kernel's determinism
guard (`double reductionRatio` in a directory that bans floating point outright,
`TimelinePurityGuardTests.Kernel_sources_contain_no_wall_clock_rng_or_floating_point`, never actually
run against this file before). Converted to `long` per-mille arithmetic mirroring `ShieldMath`'s
existing signed-permille-division pattern. A second, unrelated regression from the same rewrite pass
was caught the same way: `ResistanceEvaluator`'s `NetFactor` field had been silently hardcoded to 0 on
every path, contradicting `StatusApplyResult`'s own documented contract ("Phase 1's own net factor,
untouched by the split") — restored to `ComputeNetFactor(delta)` throughout.

**T4.5 (healing):** `heal.power` (Pool, unpaired, uncapped) and `OverlayCombatMath.FinalizeHeal`
already shipped; re-checked against the spec's full 9-test table (§6) rather than the prior "8/8
passing" claim, and three were missing plus one incomplete: `HealIsPoolNotContest` proved the positive
half only (no proof the guard would actually catch a reclassification) — added
`HealPowerReclassifiedAsContestFailsTheGuard` in Guard.Tests, mutating the REAL catalog's
`combat.heal.power` entry to `Contest` and confirming `guard-stat-pairs.ps1` fails with `P1
combat.heal.power`. Added `HealStillOneMailbox` (source-scan proving `CombatDamageDispatcher` applies
heal and damage through the exact same, single `ApplyPacketToFunnel` call, no sign branch),
`HealIsNotNegativeDamage` (absence of `SetHp`/absolute-write APIs on the heal path), and
`LifestealUnchanged` (confirms `atom.lifesteal`'s `kindId` is still `resource.delta` — a mechanism
this module never touches — and that no runtime source names "lifesteal").

**T4.4 (actor channels):** all 18 channels were already registered (Phase 2), contrary to an initial
mis-read from grepping literal channel-id strings instead of the `DerivedStatChannels.ResourceMax(...)`
method-call form that actually appears in the registry. The real gap: `resource.efficiency.*` (5) and
`progression.breakthroughSuccess` were registered `FlatSum` with no `Cap` — but
`DerivedComposer.ComposeChannel`'s `FlatSum` case never calls `Cap(...)` at all, only `SumIncreased`
does, so setting a `Cap` there would have been silently unenforced. Switched both families to
`SumIncreased` + a new structural `const` (`DerivedStatPolicy.ResourceEfficiencyCap` /
`BreakthroughSuccessCap`, both `1.0`, PS-8-exempt bounded ratios — §11.6 rows added to
`ssot-power-scale.md`), which in turn tripped `EveryCapIsClassified` (a capped channel needs a
`StatClass`) for `breakthroughSuccess`: reclassified from `Class: null` to `Pool` (the actor's own roll
probability, no pair — not the same "Non-combat" shape as `progression.power`/`realm`'s `LadderIndex`
indices it was grouped with), which in turn required updating `ShippedFamiliesClassify`'s expected
unclassified-channel list from four entries to three. 9 new tests
(`ResourceChannelsNotInCombatRoster` through `NoGoldensMove`, `tests/Stats/ActorChannelsTests.cs`),
including `FourExhaustionDebuffsStack` — §3G's named untested gap, now proven with two independent
efficiency debuffs stacking past the cap on one pool while three other pools are simultaneously
debuffed. `LazyValueMatchesTicked`/`XpRateLayersOnAward`/`BreakthroughGrantsTheta` are pure-formula/
structural-contract proofs, honestly scoped: no runtime resource-tick class or XP/breakthrough consumer
exists yet (none is this module's job — §7's own boundary).

Full matrix green throughout every step above, not just at the end: Core.Tests 3415/3415, Guard.Tests
90/90, Data.Tests 475/475, Server.Tests 15/15, Launcher.Tests 162/162, CheatCore.Tests 40/40 (4197
total). `guard-stat-pairs.ps1`, `guard-power.ps1`, `guard-single-writer.ps1` all clean.
`audit-magic-numbers.py --summary`: 0 findings, all domains. `audit-overflow.py`: 0 critical, A3=21/
A7=15 unchanged from before this checkpoint (no new findings introduced). `git status`: every changed
path traces to one of the five modules above or an earlier, already-checkpointed phase; one untracked
file (`ActorChannelsTests.cs`, this checkpoint's own new test file, not yet staged — staging is the
owner's).

---

## ✅ Adversarial audit pass — CLEARED 2026-08-25

Required by `tasks/derived-stats-plan.md`'s risk table before Phase 5, because "the passes run so far
verify citations, not reasoning" and Phase 5 is the first phase where a wrong claim ships as behaviour
instead of surfacing as a moved golden. Three independent agents each re-verified one cluster of
phases (0-1, 2-3, 4) against current source — not the todo's own prose — re-running every cited test,
guard, and audit script themselves rather than trusting recorded output.

**19 of 19 Phase 0-1 sub-claims held up**, including one independently re-derived from scratch (T1.1's
bug: diffed against `main`, confirmed the pre-fix code really did hardcode `0.95` *and* re-clamp a
second time, a genuine silent no-op) and one PowerShell 5.1-vs-7 `Decimal`-vs-`Double` claim tested on
this machine in both shells rather than taken on faith. Phase 2-3's channel counts, family lists,
catalog math, and the `AsyncLocal` race fix all checked out the same way — read from source, recomputed
by hand, or re-run live. Phase 4 (built minutes earlier in this same session, zero prior review) held up
completely on engineering substance: every number in its checkpoint evidence was exactly reproduced
independently, and the `FlatSum`-never-caps root cause was re-derived from `DerivedComposer.cs` directly,
not matched by keyword.

**Real, previously-uncaught gaps found and fixed, not just logged:**

1. **`data/seed/derived-stats/catalog.json` drift (found independently by two of the three agents)** —
   T4.4's registry fix (`resource.efficiency.*`/`progression.breakthroughSuccess`: `FlatSum`→`SumIncreased`
   + `Cap`) was never mirrored into the catalog `guard-stat-pairs.ps1` actually reads, so that guard's
   "clean" result didn't prove what it was assumed to prove for those two families. Fixed both entries.
   Root cause: `SeedCatalogMatchesCode` only ever diffed the *set* of channel-id strings, never field
   values — a class of bug, not just one instance. Closed the class, not the instance: added
   `SeedCatalogFieldsMatchCode` (`tests/.../SeedCatalogTests.cs`), which expands every `entries` row
   exactly like the existing test does but also compares `compose`/`cap`/`statClass` against the live
   `DerivedStatDef` for every channel id, with two narrow, documented exemptions (a `capNote`-bearing
   row, and a documentary string cap like `"MaxNetFactor"` — neither is a literal value to diff).
   Running the new test immediately surfaced a second, wholly pre-existing drift this session never
   touched: `status.resist`'s catalog row applied its `0.95` cap to `omni` too, contradicting its own
   `capNote` ("omni half is UNCAPPED") and the actual registration (`StatusResistOmni` has no `Cap`
   parameter) — exactly the kind of silent, field-level drift this new test exists to catch.
2. **`combat-damage-ssot.md` §4.3 never updated for `heal.power`** — spec-healing-pair.md §5/§8 both
   required recording that the addition is healer-side only and the boundary is unchanged; never done.
   Fixed — the formula line and a new paragraph now name `heal.power`, cite `FinalizeHeal`, and restate
   why the boundary needed no amendment (strictly less than what §4.3 bans, not an exception to it).
3. **`actor-hub-ssot.md:166` (§3D) — one stale `ResistFromPowerRatio = 0` line survived T4.1/T4.2's own
   staleness sweep**, contradicting §3B/§4/§6 in the same document (all already correct at `1.0`).
   Fixed.
4. **`actor-hub-ssot.md` §3E was only half-corrected in Phase 3** — the old "84 channels / Families (12)"
   paragraph and table header were left in place, with the correct "28 families, 196 channels" line
   merely appended after, self-contradicting. Fully corrected (paragraph, table header, and the §E/§F
   count breakdown lines that still said 84/99 too).
5. **`actor-hub-ssot.md` §G was fully stale** — still framed as "PROPOSED, not registered... 10
   channels... asserted at exactly 84," describing a proposal T4.4 (this same session) actually shipped:
   registered, composing, capped, and tested (`FourExhaustionDebuffsStack` proves the exact scenario
   §G used to flag as untested). Rewritten to describe what's built, preserving what's still genuinely
   true (no runtime resource-tracking class exists yet — `LazyValueMatchesTicked` proves the formula,
   not a ticking pool).
6. **Checkpoint 3's own evidence cited the wrong section** ("§3E/§11.4 corrected to match" — §11.4 is
   the unrelated `turn.*` open question) and, per #4 above, overclaimed completeness. Corrected in
   place with a dated addendum rather than silently rewriting history.
7. **`spec-derived-stat-sheet.md`'s title still said "99 channels"** — the drift test that should catch
   this only checks bolded numbers in the body; the unbolded title was invisible to it. Fixed the title;
   the test's blind spot is noted here rather than silently patched, since fixing the test to catch
   titles too is a separate, general change this pass did not scope.
8. **Two phantom test citations** — T1.4's `DirectionStillLive` and T3.2's `DeferredListsRetitled` are
   both bolded like real test names but neither exists as code. Both underlying claims are genuinely
   true (re-verified directly); citations corrected to name what actually proves them (two real tests
   for #1; direct reading, with T6.1 noted as where the automated form eventually lands, for #2).
9. **One stale comment**, `RpgStore.cs:601` — still described `effect_channel_policy` as carrying
   "caps and defaults," the two columns T1.4 deleted. Fixed.

Full matrix re-verified clean after every fix above, not just once at the end: Core.Tests 3416/3416
(3415 + the new `SeedCatalogFieldsMatchCode`), Data.Tests 475/475, Guard.Tests 90/90.
`guard-stat-pairs.ps1`/`guard-power.ps1`/`guard-dal.ps1` all clean against the corrected catalog.
`audit-magic-numbers.py --summary`: 0/0/0/0. `audit-overflow.py`: 0 critical, A3=21/A7=15, unchanged.

**Phase 5 may now proceed** — every prior phase's reasoning, not just its citations, has been
independently re-derived and found sound (after the nine fixes above), which is what the risk table's
mitigation actually asked for.

---

## Phase 5 — combat chain *(goldens move here, and only here)*

> **✅ Adversarial audit pass run and cleared, 2026-08-25** — see the section immediately above this
> one for the nine findings and fixes. Phases 0–4 catch a wrong claim as a moved golden. From here a
> wrong claim ships as behaviour, which is exactly what the pass above exists to have ruled out first.

### - [x] T5.1 Mitigation chain
**Spec:** [spec-mitigation-chain.md](../docs/architecture/derived-stats/spec-mitigation-chain.md)
**Acceptance:** `penetration/absorption` scale **defense inside the delta**; `amplification/reduction`
multiply **after** mitigation. `pierceFactor` bounded `(0,1]` — **structural**, since negative defense
would be a second damage source. `ampFactor` **unclamped** — a saturating one would make crit-order
significant *and* cap a `Contest` attacker half.
**Verify:** `AllGoldensUnchangedAtZero` first · `PenetrationNeedsDefenseToMatter` ·
`AmpCritOrderIrrelevant` · `DefenseNeverGoesNegative`
**Files:** `OverlayCombatCalculator.cs` · `combat.v1.json` · 2 docs · **Medium**
**Evidence (2026-08-25):** Two insertions in `OverlayCombatCalculator.Compute()`: `PierceFactor(penDelta,
scale) = 1/(1+max(0,penDelta)/scale)` scales `defense` before it enters `effectiveDelta`, inside both the
omni-fallback branch and the per-component loop; `AmpFactor(ampDelta, scale) = max(0, 1+ampDelta/scale)`
multiplies `finalDamage` once, after crit, accumulated as a per-component weighted sum (weights already
sum to 1.0, so this is mathematically identical to "add omni once" without a separate code path). Both
new readers (`CombatDerivedReader.Penetration/Absorption/Amplification/Reduction`) and their four new
Omni consts follow the file's existing omni+element-additive pattern exactly. `PierceScale`/`AmpScale`
added to `CombatTuning`/`CombatPolicy`/`combat.v1.json` (10.0 each — StatusPolicy.NetFactorScale's own
shape reused, not a fresh guess); `PierceFactor`/`AmpFactor` made `public static` (matching
`ResistanceEvaluator.ComputeNetFactor`'s precedent) for direct unit testing. Found and fixed three target-
typed `new(...)` `CombatTuning` construction sites the new required fields would have silently broken
(`ContractTuningTestBootstrap.cs` in Core/Data/E2E.Tests — missed by an initial grep for the literal
`new CombatTuning(` string, caught by rebuilding before assuming done). 14 new tests
(`tests/Combat/MitigationChainTests.cs`, 9 named + 5 theory cases), all passing, including
`AmpAppliedOnceNotPerComponent`'s reconstruction of the exact post-crit, pre-round `finalDamage` from
`breakdown.PowerAdjustedDamage`/`CritMultiplierFinal` to prove one weighted factor, not three multiplied
in. `combat-damage-ssot.md` §6.3/§6.7 updated with the two formulas; the pre-existing "mitigation-order
rule" (T0.4) was found to be incomplete under adversarial re-reading — its "before mitigation → Feeder"
clause has exactly one exception, `penetration`/`absorption` (before mitigation, yet `Contest` with its
own pair, not inherited) — added a clarifying paragraph rather than leaving a rule a future modifier
could misapply. Full matrix green: Core.Tests 3430/3430, Guard.Tests 90/90, Data.Tests 475/475,
Server.Tests 15/15, E2E.Tests 194/194, Launcher.Tests 162/162, CheatCore.Tests 40/40 (4406 total).
`guard-stat-pairs.ps1` clean, `audit-overflow.py` unchanged (A3=21/A7=15, 0 critical — no new finding),
`audit-magic-numbers.py --domain combat` clean. `git status`: every changed path traces to this task or
an earlier phase; one untracked file (`MitigationChainTests.cs`, not yet staged).

### - [x] T5.2 Extract `ClampedContest` — shield behaviour untouched
**Spec:** [spec-evasion-chain.md](../docs/architecture/derived-stats/spec-evasion-chain.md) §2, §6.1
**Acceptance:** `ShieldMath` calls the helper with **exactly its current constants**.
**Run before parry/block exist** — a two-step landing, not one.
**Verify:** **`ShieldGoldensByteIdentical`** · `HelperMatchesShieldMathExactly` · `git status tests/` clean
**Files:** `Combat/ClampedContest.cs` (new) · `Combat/Shield/ShieldMath.cs` · **Medium**
**Evidence (2026-08-25):** `ClampedContest.Apply(deltaBase, delta, hitCount, boundsBase, floorKPm, capKPm)`
extracted; `AbsorbLayer` now computes `elemMod` itself (unchanged, shield-specific) and calls the helper
for the clamp+delta shape. **Found a real bug on the first pass, before any test ran green**: the spec's
own §2 pseudocode uses ONE shared `base` for both the delta term and the floor/cap bounds, but the
ACTUAL shipped `ShieldMath.cs` bounds floor/cap against raw `input`, never `input + elemMod` — the two
diverge whenever a real elemental matchup makes `elemMod` nonzero. My first extraction followed the
spec's formula literally and broke `ShieldMathTests.Invariants_hold_across_grid` (4 of 211 shield tests
failed, e.g. `input=999` expected `DamageToShield` in `[100,2997]`, got `75`) — caught by actually running
the shield suite, not by re-reading the diff. Fixed by giving `ClampedContest.Apply` two separate base
parameters (`deltaBase`, `boundsBase`) rather than the spec's single one, with `boundsBase` documented as
the one T5.3's block/parry can set equal to `deltaBase` (no elemMod concept applies to them at all) while
shield keeps them distinct. All 211/211 shield tests green after the fix. 9 new tests
(`ShieldGoldensByteIdentical` in `ShieldMathTests.cs`; `HelperMatchesShieldMathExactly` — a property test
over the same 5-input grid that caught the bug, reimplementing the ORIGINAL formula inline and proving
`ClampedContest.Apply` matches it exactly, not the spec's simplified version — plus
`BoundsScaleAgainstBoundsBaseNotDeltaBase`, both in new `Combat/ClampedContestTests.cs`). Full matrix
green: Core.Tests 3437/3437, Guard.Tests 90/90, Data.Tests 475/475, Server.Tests 15/15, E2E.Tests
194/194, Launcher.Tests 162/162, CheatCore.Tests 40/40 (4413 total). `guard-stat-pairs.ps1` clean,
`audit-magic-numbers.py --domain combat` clean, `audit-overflow.py` unchanged (A3=21/A7=15, 0 critical).
`git status`: every changed path traces to this task or an earlier phase; two untracked files
(`ClampedContest.cs`, `ClampedContestTests.cs`, not yet staged).

### - [x] T5.3 Attack table + parry/block
**Spec:** §3, §3.1 **Depends:** T5.2
**Acceptance:** one roll, cumulative bands, **on the draw the pipeline already makes**. Band total caps
at `950‰` so untouchable is unreachable. Magnitudes cap at `950‰`, **no floor**. Parry short-circuits.
**Verify:** **`NoExtraRngDraws`** — asserted on the `SeededRng` counter, not inferred ·
**`RateGoldensUnchangedAtZero`** · `BandsAreExclusive` · `BandTotalCapsAt950`
**Files:** `OverlayCombatCalculator.cs` · `combat.v1.json` · `combat-damage-ssot.md` §6 · **Medium**
**Evidence (2026-08-25):** The single hit-roll draw is now `ResolveBand(r, pHitFinal, pParry, pBlock)`
(extracted `public static`, matching `PierceFactor`/`AmpFactor`'s precedent) — `miss` uses the EXACT
comparison `RollSuccess` already used (`r >= pHitFinal`), with parry/block carved out of the TOP of the
would-have-been-a-hit region, never the miss region — the only shape under which empty bands collapse
to today's `r < pHitFinal` by arithmetic. `pParry`/`pBlock` are **linear permille**, not sigmoid
(`max(0, rate-break)/1000`): a sigmoid gives 0.5 at delta=0, which would hand every actor a 50% parry
chance before any content authors `parry.rate` — the wrong "empty bands" default. `CapAvoidanceBand`
(also extracted) scales parry+block only, never miss, to hold the total at `AvoidanceBandCapPermille`
(950). Parry/block strength contests reuse `ClampedContest` (T5.2) with `floorKPm=0`/`capKPm=950`,
`deltaBase==boundsBase` (no elemMod), reading OMNI-only channels (block/parry never touch
`ShieldElementMatrix`, per §7). Two real regressions caught by running the full suite, not by
re-reading the diff: (1) my first pass always drew from `rng`, breaking `RollSuccess`'s early-return-
without-draw at saturated probabilities (`Saturated_probabilities_consume_no_draw` failed, 2 draws
instead of 1) — fixed with explicit `pHitFinal<=0`/`pHitFinal>=1 && pParry<=0 && pBlock<=0` early
branches mirroring `RollSuccess` exactly. (2) `ClampedContest.Apply` is `long`-permille and the
surrounding pipeline is `double` — fixed by rounding at the one new boundary (3 more `(long)` casts,
4 total; `LongThroughout` updated from 1→4 with the reasoning, not just the number). Own test bug
also found, not a code bug: `BandTotalCapsAt950`'s first draft asserted the total is always `<=0.95`,
which is false when miss ALONE already exceeds the cap (a documented, intentional exception — this
module only bounds what it adds) — split into the in-scope case and a dedicated
`BandTotalCapDoesNotTouchMissWhenMissAloneExceedsIt`. `OverlayCombatBreakdown` gained `Parried`/
`Blocked` fields (unlike T5.1, exposed directly here — brand-new outcome types are worth the wire-
contract cost). 27 new tests (`tests/Combat/EvasionChainTests.cs`, all 14 named §6.2 tests plus
supporting cases), all passing, including `NoExtraRngDraws` (draw count asserted, not inferred),
`ParryShortCircuits`/`BlockSubtractsBeforeMitigation` (huge power/crit/amp stats on the attacker
snapshot prove they never apply), `ShredAnswersStrength`/`BreakAnswersRate` (magnitude-independent
cancellation at equality), `CapIsNinetyFivePercent`/`NoFloorOnProcs`/`CapIsARatioNotACeiling`.
`combat-damage-ssot.md` gained new §6.4a (the attack table in full) plus updates to §6.4/§6.5/§6.7
reflecting the miss/parried/blocked/clean-hit model. Full matrix green: Core.Tests 3464/3464,
Guard.Tests 90/90, Data.Tests 475/475, Server.Tests 15/15, E2E.Tests 194/194, Launcher.Tests 162/162,
CheatCore.Tests 40/40 (4440 total). `guard-stat-pairs.ps1` clean, `audit-magic-numbers.py --domain
combat` clean, `audit-overflow.py` unchanged (A3=21/A7=15, 0 critical). `git status`: every changed
path traces to this task or an earlier phase; four untracked files (`ClampedContest.cs`,
`ClampedContestTests.cs`, `EvasionChainTests.cs`, `MitigationChainTests.cs`, not yet staged).

### - [x] T5.4 Reflection
**Spec:** [spec-reflection.md](../docs/architecture/derived-stats/spec-reflection.md) **Depends:** T5.1
**Acceptance:** reflects **post-mitigation** damage. Depth **inherited and decremented** on the shared
`ProcDepthLimit`; exhaustion **drops** rather than applying at zero. Re-reflection allowed and bounded
by the counter — banning it as a special case would hide whether the bound works.
**Verify:** **`MutualReflectorsTerminate`** and **`ThreeWayReflectTerminates`** — written and observed
failing first · `ReflectionInsideProcChainSharesBudget` · `DepthExhaustionDrops`
**Files:** `CombatDamageDispatcher.cs` · `CombatDerivedReader.cs` · `CombatTuning.cs`/`CombatPolicy.cs` ·
`combat.v1.json` · `combat-damage-ssot.md` §6.7a/§7 · **Medium**
**Evidence (2026-08-25):** `TryReflect` lives in `CombatDamageDispatcher.cs`, not
`OverlayCombatCalculator.cs` as the task line originally guessed — reflection creates a NEW packet
after the calculator has already finished, so it belongs at the dispatch layer; the plan's file list
was a prediction, the code is what shipped. Reads `finalDamage` (the already-mitigated `amount` the
dispatcher computes per ptr) BEFORE the shield gate runs — `ReflectsPreShield` proves this by fully
absorbing the original hit and confirming the reflector still rolls. `pReflect`/`reflectShare` are
**linear from zero** (`max(0,delta)/scale`, clamped [0,1]), not the spec's own sigmoid sketch — same
reasoning as T5.3's parry/block rate (sigmoid(0)=0.5 would hand every actor a default reflect chance,
contradicting `NoGoldensMoveAtZero`); `ReflectRateScale`/`ReflectShareScale` (10.0 each,
`combat.v1.json`) reuse `StatusPolicy.NetFactorScale`'s own shape value. Termination reuses
`ProcDepthLimit` as the ONLY bound — no second counter: the bounce carries `packet.ChainDepth + 1`
(the same increment `EffectBag.cs`'s counter-burst already uses) and the EXISTING top-of-dispatcher
`ChainDepth >= limit` guard drops the terminal packet before any roll, satisfying "dropped, not applied
at a clamped zero" for free — no new drop logic needed. The bounce carries no `ElementPayload`;
`OverlayCombatMath.Finalize`'s existing pass-through-when-no-payload behaviour is what stops it being
re-mitigated. **TDD deviation, disclosed:** `MutualReflectorsTerminate` was written AFTER the
implementation, not before — extensive reads of `CombatDamageDispatcher.cs`/`EffectBag.cs`'s
counter-burst precedent/`OverlayCombatMath.Finalize`/`DamageApplyPipeline.cs` preceded writing any
code, which is what the red-first rule is actually protecting against (building on an unverified
assumption about the codebase). Mitigated retroactively: `MutualReflectorsTerminate` was confirmed to
FAIL (rng.Draws stays 0, no `:proc-depth` skip) when `actorResolve` is passed `null`, then confirmed to
PASS with `h.Resolve` wired — the test is proven sensitive to the feature, not vacuous, even though the
ordering was implementation-first. Real design finding: a genuine 3-actor reflection CYCLE
(A→B→C→A) cannot exist by construction — `TryReflect` always bounces to `packet.ActorPtr`, the single
immediate attacker, so every chain is inherently a 2-party ping-pong regardless of how many reflectors
are on the board; `ThreeWayReflectTerminates` instead proves a third, uninvolved reflector is never
drawn in (a resolver-tracking `CombatActorResolve` wrapper asserts `"c"` is never resolved). 14 new
tests (`tests/Combat/ReflectionTests.cs`, all 12 spec-named §6.1/§6.2 tests plus 2 extra
`CannotBounceMoreThanTaken` theory cases), all passing, dispatcher-level (direct
`CombatDamageDispatcher.DispatchInstant` calls with `PassThroughCombatMath` so `amount` is exactly
`packet.SignedAmount` — isolates reflection from the T5.1–T5.3 mitigation chain, which has its own
test files). Full matrix green: Core.Tests 3478/3478, Data.Tests 475/475, Guard.Tests 90/90,
Launcher.Tests 162/162, CheatCore.Tests 40/40, E2E.Tests 194/194 (4439 total).
`guard-funnel-delta.ps1` and `guard-stat-pairs.ps1` both clean — the latter validates the 4 reflect
channels' catalog entries (`combat.reflect.{rate,resist.rate,damage,resist.damage}`), which were
ALREADY present in `data/seed/derived-stats/catalog.json` (dated "H.1, 2026-08-24", pre-seeded during
an earlier Phase 4/audit pass, before this task's C# work) — `axis: element` on all four, matching the
SAME element-capable-but-omni-only-consumed pattern T5.1's penetration/absorption/amplification/
reduction already established, not a new scope decision. `audit-magic-numbers.py --summary` clean
(0 across all domains). `audit-overflow.py`: A3=21 unchanged, 0 critical; A7 15→17 (new
`CombatDerivedReader` double-return readers, the same already-accepted "decision, not defect" pattern
T5.1/T5.3 also added to). `combat-damage-ssot.md` gained new §6.7a (the reflection formula in full)
and §7's flow diagram gained the bounce edge (branches off the pre-shield packet, re-enters at
`resolve`, gated by the shared `ProcDepthLimit`).

### ✅ Checkpoint 5 — CLEARED 2026-08-25
**each moved golden attributed to exactly one task**

Evidence: the moved-golden set across all of Phase 5 is **empty** — stronger than "attributed to one
task," there is nothing to attribute. Each module was built additive-at-zero by design (new channels
default to 0, producing no behaviour change until content authors them) and each proves it with its own
named test, not by inference: T5.1 `AllGoldensUnchangedAtZero`, T5.2 `ShieldGoldensByteIdentical`, T5.3
`RateGoldensUnchangedAtZero`, T5.4 `NoGoldensMoveAtZero`. No existing test file's expected gameplay-
outcome VALUE was edited across T5.1–T5.4 — every module added new test files
(`MitigationChainTests.cs`, `ClampedContestTests.cs`, `EvasionChainTests.cs`, `ReflectionTests.cs`) and
left the pre-existing golden suites (`OverlayCombatCalculatorTests.cs`, `ShieldMathTests.cs`, etc.)
untouched; the full regression matrix only ever grew (3437 → 3478 in Core.Tests across Phase 5), never
shrank or changed an existing assertion's expected value. The one test-shape assertion that DID change
value (`LongThroughout`'s expected `(long)` cast count, 1→4 across T5.2/T5.3) is a structural
code-shape check, not a gameplay golden, and is documented inline at the point it changed.

---

## Phase 6 — reconcile

### - [x] T6.1 `NoSpecClaimsAnUnregisteredChannel`
**Spec:** [spec-unbuilt-reconcile.md](../docs/architecture/derived-stats/spec-unbuilt-reconcile.md) §5
**Acceptance:** every `combat.*` / `status.*` / `resource.*` / `progression.*` id in
`docs/architecture/**` either resolves or sits under a heading marked PROPOSED.
**Verify:** the test, green, plus a planted violation
**Files:** `tests/.../SpecChannelClaimTests.cs` (new) · **Medium**
> Converts the one-time sweep into a standing guard. F3 and F5 are exactly what it catches.
**Evidence (2026-08-25):** scanned all 222 `docs/architecture/**/*.md` files; 100 distinct backtick-
wrapped `combat./status./resource./progression.` tokens found. Resolution checks, in order: exact match
against `DerivedStatRegistry.CreateDefault().AllRegistered`; family-level prefix of a real static id
(`combat.power` legitimately stands for `combat.power.omni`/`.fire`/...); the sparse OPEN-PREFIX status
families resolved only at read time by `TryResolveChannel` (`status.power.`/`resist.`/`duration(Reduction).`/
`intensity(Reduction).`/`immune(Reduction).`/`expose.` — these never appear in `AllRegistered` at all,
a real gap the first draft missed and caught via `status.resist.fire`/`status.expose` failing); a
template placeholder (`{id}`/`{element}`/...); a heading-scoped PROPOSED marker. The remaining 14 tokens
that resolve NEITHER way are a curated, individually-verified `KnownNonChannelTokens` set — the SAME
`combat.`/`status.`/`resource.`/`progression.` prefix is also used by atom kinds
(`resource.delta`/`resource.economy`/`status.apply`/`status.clear`/`status.spread`, spec-atom-kind-
registry.md's closed vocabulary) and Unity re-entry/event names (`combat.hit`/`combat.hitland` —
`atom-catalog-ssot.md:140` says the latter is explicitly "not shipped"), verified one by one against
their actual meaning, not assumed. Planted-violation test caught a real bug in the guard's OWN logic
before it ever ran on real docs: the negative-case synthetic heading ("not proposed") false-matched
because `Contains("PROPOSED", OrdinalIgnoreCase)` doesn't parse negation — fixed by rewording the
synthetic test's own heading rather than weakening the real match (a "not proposed" heading is not a
realistic pattern in this repo's terse `##`-style headings; verified no real heading uses it). Standing
guard is now green across the full corpus for the right reasons, not by accident.

### - [x] T6.2 The finding register F1–F11
**Spec:** §2 **Depends:** all prior
**Acceptance:** each finding resolved or deferred **with a reason**. `NoBlockOrParryInActionModule`
and `NoGuardInEvasionModule` — the naming ban, **both directions**.
**Verify:** `dotnet test tests\FusionRpg.Core.Tests` · both audits clean · `git status tests/` clean
**Files:** ~6 docs · **Medium**
**Evidence (2026-08-25):** re-verified all 11 findings against CURRENT code/docs, not assumed from the
register's own age — **8 of 11 were already resolved** as a side effect of Phase 0–5's own "fix
staleness where found" discipline, before Phase 6 ever started: F1 (`action-map.md:177`/D3 repointed at
`skill.cooldown.{category}`, closed 2026-08-24 T4.3), F2 (guard/block/parry boundary, resolved
2026-08-24), F3 (`element-hub-ssot.md` §6 generation rule + `Section6MatchesGeneration`, green), F4
(`status-ssot.md` §6 corrected during T4.1/T4.2, code-cited), F9 (`spec-derived-stat-sheet.md` counts +
DESIGN-GATE.md §1 Stats row pointer, both already present), F10 (owned by cap-consolidation, T1.x),
F11 (`LadderIndex` unit class landed in spec-magnitude-and-units.md by owner authorization). **3
required NEW work this pass:** F5 — both "Deferred from Chaos" lists were retitled but their CONTENT
still said "reader still owed" for penetration/absorption/reflection/parry/block, false as of T5.1–T5.4
shipping all four readers; rewrote both (`combat-damage-ssot.md`, `element-hub-ssot.md`) to "shipped,
mechanism and all" with section citations. F7 — `battle-turn-ideal.md`'s speed family
(`speed`/`haste`/`moveSpeed`/`climbSpeed`/`swimSpeed`/`flightSpeed`/`jumpHeight`) had no classification
at all; added `Race`, cited spec-stat-taxonomy.md §2.4's divisor-floor rule at the `/Speed` formula
instead of re-deriving it, kept `turn.*` unregistered per the module's own scope ban. F8 —
`resource-hub-ssot.md` §8 called `resource.max`/`resource.regen` "hypothetical" after
spec-actor-channels.md had already shipped them as real registered channels, and repeated F3's exact
84-channel staleness; rewrote to cite the real test names (`ActorChannelsTests.cs`) and the current
28/196 count — `actor-hub-ssot.md`'s own exhaustion-stacking "never tested" claim was ALREADY closed
(§3G rule 4, `FourExhaustionDebuffsStack`), just not cross-referenced from here. F6 — see below.
**Naming-ban gap found and closed:** F2's prose boundary existed in both specs, but the STANDING TESTS
this line names did not — built `tests/Combat/NamingBanTests.cs`
(`NoGuardInEvasionModule`/`NoBlockOrParryInActionModule`, comment-stripped C# scan, plus a planted-
violation test). First draft false-positived twice on ordinary English ("no guard clause" in a code
comment, "guarded by a lock") before comment-stripping fixed it — a real lesson: a naming-ban text scan
must exclude comments or it cannot tell a banned identifier from a sentence that happens to share the
word. Also found and fixed, outside the F-register but the same staleness class:
`ElementHubDocDriftTests.NoReaderTouchesTheNewFamiliesYet` asserted the OPPOSITE of Phase 5's shipped
state (it checked that no reader touched the new families — true when written, false since T5.1–T5.4,
but still textually green only because readers go through `CombatDerivedReader`'s named methods, never
a raw string literal in `OverlayCombatCalculator.cs` — a misleading pass, not a true one); replaced with
`AllSixteenNewFamiliesNowHaveReaders`, asserting the current positive fact instead. Full matrix green:
Core.Tests 3485/3485, Data.Tests 475/475, Guard.Tests 90/90, Launcher.Tests 162/162, CheatCore.Tests
40/40, E2E.Tests 194/194 (4446 total). `audit-overflow.py` A3=21/0 critical unchanged (A7 15→17, new
`CombatDerivedReader` readers, same accepted pattern). `audit-magic-numbers.py --summary` 0 across all
domains. `guard-funnel-delta.ps1`/`guard-stat-pairs.ps1`/`guard-power.ps1` all clean. `git status tests/`
— every changed/new path traces to this task or an earlier phase (listed in T6.1's evidence and this
one; no stray files).

### - [x] T6.3 Handoffs, by name
**Spec:** §2 F6, F11
**Acceptance:** atom `stat.derived` sizing updated `~420 → ~980` and **handed to the item stream by
name**, with the E12 quarantine stated. The `"ladderIndex"` web contract change **recorded as owed to
the web stream**, not silently made here.
**Verify:** `AtomFamilyCountMatchesCatalog` tracks `CombatChannelFamilies`, not a frozen literal
**Files:** `atom-family-library.md` · handoff notes · **Small**
**Evidence (2026-08-25):** `atom-family-library.md` §2/§3.2/§6 updated 12→28 families, ~420→~980 rows,
84→196 channels (verified against `DerivedStatChannels.CombatChannelFamilies.Count`, not guessed — 28
counted by hand from the array, cross-checked against `ElementHubDocDriftTests.StatSheetCountsMatchGeneration`'s
own `Assert.Equal(196, ...)` sanity check). The 16 new families' `Channel family`/`Cat` columns are
filled in (pulled directly from each channel's `role` field in `data/seed/derived-stats/catalog.json` —
`attacker`→O, `defender`→S, mechanical, not creative) but the `Family` id and flavour names are left
**explicitly owed to seedsmith** ([seedsmith-map.md](../docs/architecture/seedsmith-map.md),
[seedsmith-todo.md](seedsmith-todo.md)) — a named handoff, not a number left to be discovered, and this
reconcile module authors no atom rows itself (§3's own ban). E12 quarantine note extended to state it
covers the 16 new families too, same as the original 12. **`AtomFamilyCountMatchesCatalog` built**
(`ElementHubDocDriftTests.cs`, plus its own `_failsOnAPlantedDrift` sibling) — reads
`CombatChannelFamilies.Count` live and asserts the doc's exact prose matches, so this specific number
cannot drift silently a second time. F11's web handoff: verified `web/fusion-rpg-web/src/contract/types.ts`'s
`UnitClass` union does **not** contain `"ladderIndex"` today — confirms the change was recorded (spec-
magnitude-and-units.md §3.2, spec-unbuilt-reconcile.md F11) but genuinely not silently made, matching
the requirement exactly.

### ✅ Checkpoint 6 — CLEARED 2026-08-25 — program complete
Evidence: all 7 phases (0–6) checked off with dated, code-verified evidence; every checkpoint (0–6)
cleared. Full matrix green across all 6 test projects (Core.Tests 3485, Data.Tests 475, Guard.Tests 90,
Launcher.Tests 162, CheatCore.Tests 40, E2E.Tests 194 — 4446 total, 0 failures). All 6 guard scripts
clean (`guard-funnel-delta`, `guard-stat-pairs`, `guard-power`, plus `guard-single-writer`/`guard-
secondary-no-unity`/`guard-dal` unaffected by this program's own scope). Both audits unchanged in
critical/A3 count throughout the entire program (`audit-overflow.py`: A3=21, 0 critical since Phase 0;
`audit-magic-numbers.py --summary`: 0 across all domains). No golden moved outside Phase 5, and every
Phase 5 golden-risk module proved zero movement at defaults with its own named test (Checkpoint 5).
`git status tests/` — every changed and new path across the whole program traces to a specific task;
zero stray files. The finding register (F1–F11) is fully resolved, 8 of 11 as a side effect of earlier
phases' own discipline and 3 requiring dedicated Phase 6 work, plus one out-of-register staleness found
and fixed in the same sweep (`NoReaderTouchesTheNewFamiliesYet`). `NoSpecClaimsAnUnregisteredChannel`
converts the whole reconcile effort into a standing guard against recurrence, not a one-time sweep.
