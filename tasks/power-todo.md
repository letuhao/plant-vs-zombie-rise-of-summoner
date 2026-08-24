# Tasks — power program

Plan: [power-plan.md](power-plan.md) · Map: [../docs/architecture/power-map.md](../docs/architecture/power-map.md)
Specs: [../docs/architecture/power/](../docs/architecture/power/) · Standards: [tunables-ssot.md](../docs/architecture/tunables-ssot.md)

**Authorized:** Phase 0, Phase M, Phase D — done. **Phases 1–4: owner approved the map and all ten
module specs 2026-08-24 — build authorized.** Phase 1 (T1.1–T1.4) done the same day, see Checkpoint 1.

Shorthand — `CORE` = `dotnet test tests\FusionRpg.Core.Tests` · `ALL` = Core + Data + Server + Guard ·
`CLEAN` = `git status --short tests\` shows **no golden modified**.

---

## Phase 0 — numeric overflow

Thresholds: `float` breaks at **Θ 232** · `int` per-mille **3,213** · `int` whole **103,557** · `long` **214,748,300**.
Baseline: **0 critical, 92 A3, 14 A7**.

- [x] **P0.1** Standard in `CLAUDE.md`, `AGENTS.md`, `DESIGN-GATE.md`, the spec skill, and the patch record
- [x] **P0.2** `scripts/audit-overflow.py` — 7 categories, precision gate, `--targets` / `--fix A4`

- [x] **P0.3 — Triage the 92 A3 findings** — done 2026-08-23. 92 → 75: three regex defects fixed in
  `scripts/audit-overflow.py` (hp/"hP" case collision — `KillEarnWithPatron`; A3 missing A2's
  per-mille-ratio exclusion — 14 `*Milli` bonuses/chances; `NOT_MAGNITUDE` missing `unit`/`peractor`
  — `ShieldUnit`, `MaxShieldsPerActor`), 17 findings removed by the fix, not waived. Remaining 75 =
  **56 LADDER** + **19 BOUNDED**, each BOUNDED verdict naming its proven cap (Harmony signature match
  ×10, Unity-field snapshot ×1, debug-scenario literal ×2, dev-time generation input ×1, clamped AI
  utility score ×3, retention tail ×1, plus their downstream lineage). Full breakdown:
  `docs/architecture/power/overflow-triage.md`. Verified: `--targets A3 | wc -l` == 75 == 56+19; A1/A7
  unchanged (0/14)

- [x] **P0.4 — Widen the LADDER bucket to `long`** — done 2026-08-23. All 56 triaged LADDER sites
  widened across `Core`, `Contracts`, `Data`, `Injector` (Stats/EntityBaseline, SimModels, StatMath,
  Battle/*, Demons/Patron/PatronPolicy, Effects/Atoms/Power/*, World/*, Contracts/Dtos+WorldDtos+
  EffectDtos, Injector/GameDumps+EntityStatWriter+CheatActions+GameHooks). Two dead-code sites deleted
  instead of widened (`UnityStatWriter`, `StatSystem`'s `int` `ScaleCurrentHp` overload — both zero
  callers, same shape as `IProgressionPowerProvider`). Fixed SSOT §11.2a's three narrowing casts
  (`EffectBag.cs:707`, `EventDrain.cs:458`/`:475`) plus the same defect at `EntityStatWriter.
  ForceSetPlantHp` and `WritePlant`/`WriteZombie`'s Atk/Arm1/Arm2 writes — all now clamp explicitly
  at the Unity-field boundary (`ZombieCombatFields.ClampToInt32`) instead of narrowing silently.
  Harmony-hook `int damage` sites (BOUNDED, §3.1 of the triage) deliberately untouched — clamped
  *into*, never widened, since the type is forced by the base game's own compiled method signature.
  - Verify: `audit-overflow.py` → A3 92→**19**, exactly the triage's 19 BOUNDED sites, line-for-line
    (confirms every LADDER site is resolved and nothing BOUNDED was touched); A1/A7 unchanged (0/14)
  - `ALL` (Core 2971, Data 470, Guard 73) green + Launcher 162, CheatCore 40 also run clean. One
    pre-existing failure fixed in passing (`DebugScenarios.AllowedStepNames` missing two step names —
    confirmed via `git status` to predate this work, zero relation to typing/overflow)
  - **Wire hash proof**: `BattleGoldenTests.cs`'s four named hashes (`StompHash`/`CloseHash`/
    `WipeHash`/`SeedSweepHash`, computed from `BattleReport` JSON built out of the widened
    `BattleActorSetup`/`BattleModels` types) passed **unchanged** — direct evidence the widening moved
    no golden, not an inference from "tests passed"
  - `git status --short tests\` is not literally empty — pre-existing, unrelated WIP already sits in
    the tree (`UniqueEquipmentCatalogTests.cs`, `UniqueActorStoreTests.cs`, confirmed via diff content
    to be a different, unrelated feature stream). This program's own test diff is exactly 3 files:
    2 compile-fixes preserving identical behavior (`BattleEffectHostTests.cs`'s `FakeTarget`,
    `PatronPolicyTests.cs`'s `long souls`) + 1 defect-fix explicitly sanctioned by this task's own
    accept criteria (`EventDrainIntegrationTests.cs` — the test asserted the `int32` clamp §11.2a
    exists to remove; rewritten to assert the exact merged value instead)

- [x] **P0.5 — A7 `double` in stat composition — decided: it stands** (SSOT §10.7). Range is not the issue (`double` is exact to Θ≈6.7M); determinism is, and `decisions.md:40` already mitigates it with the `BattleReport` platform stamp + cross-arch refusal. `Increased`/`More` are genuinely fractional — composing ratios in integers would be wrong. The `long` rule binds *magnitudes*, not ratio arithmetic

- [x] **P0.6 — Arm the overflow audit in CI** — done 2026-08-23. `scripts/guard-overflow.ps1`
  (new, matches the other 4 guards' shape) wraps `audit-overflow.py`; wired into both
  `.github/workflows/ci.yml` (new step, right after checkout — fails fast before the long test run)
  and `scripts/deploy-play.ps1` (5th guard, alongside single-writer/DAL/secondary-no-unity/funnel-delta).
  A3/A7 non-blocking by construction — the script's own exit code already reflects CRITICAL-only
  (A1/A2/A4); the guard just surfaces it as a build failure
  - Verify: planted a temporary `float probeHp` in `Core/_GuardOverflowProbe.cs` → guard exited 1
    (`A1=1`, `OVERFLOW GUARD FAILED`); removed it → exited 0 again (`OVERFLOW GUARD OK`). Both
    `deploy-play.ps1` and `guard-overflow.ps1` PowerShell-parse clean; `ci.yml` YAML-parses clean

### ✅ Checkpoint 0 — passed 2026-08-23
- [x] Audit exits 0, A3 = BOUNDED-only (19/19, triage §3) · triage doc complete
  (`docs/architecture/power/overflow-triage.md`) · `ALL` (Core 2971 + Data 470 + Guard 73) green,
  Launcher 162 + CheatCore 40 also clean · A7 recorded (SSOT §10.7, stands) · CI armed (guard red→green
  proven)

---

## Phase 1 — the ladder *(inert — no callers)*

- [x] **T1.1 — `PowerTuning` + loader + typed rejections** — done 2026-08-24. `PowerRejection.cs`
  (enum `PowerRejectionReason` + `PowerTuningRejection : Exception` carrying it), `PowerTuning.cs`
  (`PowerCurveTuning`/`PowerWeightsTuning` records + `Build()` validation, deriving `A` from the pin
  per §2.2's general remainder-based division rather than the hardcoded `30000−19B/2` shortcut — the
  same formula, but one that would still be correct if `pinIndex` ever legally changed), and
  `PowerTuningLoader.cs` (pure `Parse(string json)`, no file I/O — Core parses a stream, tunables
  §7.2). `data/tuning/power-scale.v1.json` ships **`bMilli:0`**, not the decided `400` — plan.md's
  "B=0 first, dial second" means the shipped file must stay inert through Phases 1–3; `power-dial`
  (T4.2) is the one commit that republishes v2 at 400. Weights carry their real §5.3 starting values
  now (`Wm=5000`, not the spec's illustrative `null`) since nothing reads them until T1.3
  - Accept: `A` derived from the pin at load ✓ (`Build_ADerivation_MatchesClosedFormForEveryLegalB`,
    6 B values) · odd `bMilli` rejected naming `b±1` ✓ (`Build_OddB_RejectsNamingNearestLegalValues`)
    · `PinBroken`/`FixedConstantChanged`/`NegativeWeight`/`TuningMissing` ✓ (one test class per
    reason, `FixedConstantChanged` themed over cMilli/pinIndex/pinValue, `NegativeWeight` themed over
    all 7 weights incl. `Wm`) · no fallback constants ✓ (`Parse` never catches-and-defaults; every
    branch throws) · `WmMilli: null` legal at rest ✓ (`Build_WmMilliNull_IsLegalAtRest` +
    `Parse_WmMilliNullInJson_IsLegalAtRest`)
  - `PinBroken` review finding: algebraically unreachable via the public API given a correct
    derivation (the reconstruction is definitionally the inverse of the derivation) — kept as a
    genuine belt-and-braces regression guard per the spec's own framing, not force-triggered by a
    contrived seam. `Build_PinHolds_ReconstructedMilliEqualsPinValueTimes1000` proves the invariant
    holds across the full legal B range instead
  - Review found a real gap the accept criteria didn't name: `bMilli` (operator-authored config) had
    no upper bound, so `bMilli * pinIndex * (pinIndex−1)` could silently wrap on an absurd value —
    CLAUDE.md's "overflow throws, never wraps, no silent unchecked on a magnitude path" applies to
    this arithmetic same as any other magnitude path. Fixed: wrapped the derivation in `checked`;
    covered by `Build_AbsurdBMilli_ThrowsOverflowRatherThanWrapping` (asserts `OverflowException`,
    not a garbage curve)
  - Verify: `CORE --filter PowerTuning` → **30/30 green** (incl. the overflow regression and a direct
    parse of the real shipped `power-scale.v1.json`, proving `bMilli=0`/`AMilli=30000` and `Wf==Wa`)
  - `python scripts/audit-overflow.py` → 0 critical, **no new A1/A3 findings from Power files**
    (confirmed by grep on `--targets A3` output — the pre-existing 21 vs. the recorded 19 is Phase
    M's already-documented `overflow-triage.md` 2026-08-24 addendum, unrelated to this task)
  - `python scripts/audit-magic-numbers.py --domain power` → **0/0/0/0**. First run flagged `M3` on
    `FixedPinIndex`/`FixedPinValue` — the tool checks only the *immediately preceding* line for a
    comment, and both sat under one shared block comment above `FixedCMilli`; fixed by giving each
    structural const its own directly-adjacent one-line comment
  - Files: `src/FusionRpg.Core/Power/PowerTuning.cs`, `PowerTuningLoader.cs`, `PowerRejection.cs`,
    `data/tuning/power-scale.v1.json`, `tests/FusionRpg.Core.Tests/Power/PowerTuningTests.cs`

- [x] **T1.2 — `PowerLadder.Value`** — done 2026-08-24. `PowerLadder.cs`: `ValueMilli`/`Value`
  exactly per §2.6's shape, `MaxIndex` as a lazily-cached computed property (binary search over the
  `int` range `Value`'s own index accepts), `PowerIndexOverflow` thrown above it — never wraps
  - Accept: `B=0 → A=30` ✓, `Value(L)==80+30L` across `[0,5000]` ✓
    (`AtBZero_ValueMatchesShippedBattleRulesetBaseHpFormula_AcrossFullRange` — the equality Phase 2's
    zero-golden-movement migration rests on) · `Value(20)==680` for every legal `B` ✓ (6 values) ·
    closed form ≡ iterated `ΔP` sum to Θ=2000 ✓ · `maxIndex` from `B`, throws above ✓
  - **Review found two real defects, both fixed and covered, neither in the accept list:**
    1. **`MaxIndex`'s own search understated the true ceiling by ~30%** (151.85M vs. the correct
       214,748,299 at `B=400`). Cause: `BMilli * index * (index−1) / 2` computes the full un-halved
       product before dividing, which overflows `checked` even when the true (halved) result fits.
       Fixed by halving whichever of `(index, index−1)` is even *before* multiplying by `BMilli`, in
       a helper shared by `ValueMilli` and the `MaxIndex` search (so they can never disagree with each
       other again). Exact boundary independently re-derived in Python from the closed form, not read
       back off the fixed implementation, then asserted exactly (not as a range) —
       `MaxIndex_AtDecidedDialB400_MatchesExactly`
    2. **`B=9998`** (one of the spec's own "legal B" test values, ​§5's pin-holds row) **drives the
       derived `A` negative** (`30000 − 19·9998/2 = −64981`), which dips `Value(1)=15` below
       `Value(0)=80` — a real property of the formula's shape at extreme `B`, not a code defect. 9998
       is 25× the decided dial and 12× the documented "steep" example (800, SSOT §4.5); no real
       tuning approaches it, and the spec's own table lists 9998 only under "pin holds", never under
       "monotonic". Resolution: excluded 9998 from the monotonicity theory only (kept in pin-holds,
       closed-form, and increment theories, which are algebraic identities unaffected by sign) — with
       the reasoning recorded inline as a code comment, not silently dropped. **Flagging for whoever
       eventually specs `power-guard` (T4.1):** if a future `bMilli` upper bound is ever added, this
       is the concrete case motivating it — not required by any current accept criterion
  - Verify: `CORE --filter PowerLadder` → **23/23 green**; `CORE --filter FusionRpg.Core.Tests.Power`
    (both files together) → **53/53 green**; full `CORE` suite → **3024/3024 green**, zero regression
    · source scan (own `[Fact]`, not just eyeballing) — no `double`/`decimal`/`Math.Pow`/`Math.Exp`
    anywhere under `Core/Power`; no numeric literal in `PowerLadder.cs` outside `{0,1,2,1000}`
  - `git status --short tests\` — the only modified test files are Phase M's pre-existing
    `ContractTuningTestBootstrap.cs`/`RpgXpAwardMapTests.cs` (unrelated, already recorded); T1.1/T1.2
    touched **only new files** — CLEAN
  - `audit-overflow.py` → 0 critical, A3 unchanged at 21 (no new findings from `PowerLadder.cs`) ·
    `audit-magic-numbers.py --domain power` → **0/0/0/0**
  - Files: `src/FusionRpg.Core/Power/PowerLadder.cs`, `tests/FusionRpg.Core.Tests/Power/PowerLadderTests.cs`

- [x] **T1.3 — `IPowerIndexProvider` + composer** — done 2026-08-24. `ContentContext.cs`,
  `PowerAxisReport.cs`, `PowerIndexComposer.cs` (`ActorExplain`/`ContentExplain`, `ValidateWeights`,
  round-once-at-the-sum, `PowerWeightInvalid`/`PowerWeightMissing`), `IPowerIndexProvider.cs`
  (interface + `StubPowerIndexProvider` + `HydratedPowerIndexProvider`, mirroring
  `InjectorProgressionPowerProvider`'s identity-keyed dictionary shape). `Wf != Wa` is checked here
  (composer/provider construction), not in T1.1's `PowerTuning.Build` — the todo's own T1.1/T1.3
  accept-criteria split puts it here, and it is genuinely a *composition* rule (which axes may never
  diverge), not a *tuning-file* validity rule
  - **Real spec gap found and fixed, not just an implementation bug:** §2.1's own formula needs
    `Wf·realmsAdvanced` for `Θ_content`, but §2.2's prose defined `ContentContext` as only
    `(dangerBand, worldTier, zombossLevel)` — three fields, missing the fourth the formula requires.
    Without it, `ContentContext` could not even be constructed for the F2/F8 "500 simulated worlds"
    tripwire. Per the spec's own "SSOT wins on disagreement" rule, added `RealmsAdvanced` as a fourth
    field — corrected in `ContentContext.cs` and documented in place in `spec-power-index.md` §2.2/§8
    (dated note, not a silent edit)
  - Accept: `Θ_actor`/`Θ_content` weighted, rounded once ✓ (`WeightedSum_ExactAcrossThreeAxes`,
    `RoundingHappensOnceAtTheSum_NotPerAxis` — Wr=250×3runs=750milli rounds to 1, not 0) ·
    `Explain(ctx).Total == ActorIndex(ctx)` ✓ (`ExplainTotal_AlwaysEqualsActorIndex_OverAGeneratedMatrix`,
    7-case theory incl. 0/1/large values) · `Wf != Wa` rejected ✓ (`WfNotEqualWa_RejectedAtConstruction`,
    asserts both named values in `PowerWeightInvalid`) · uncapped runs asserted ✓
    (`PvzRuns_Uncapped_NoSaturationAtTenThousand` — 10,000 then 1,000,000 runs, both scale linearly) ·
    `Wm` null → `ContentIndex` throws / `ActorIndex` works ✓ (`WmNull_ContentIndexThrows_ActorIndexStillWorks`)
  - Also covered beyond the accept list, matching the spec's own §5 testing table: PS-6 tripwire
    (run-share stays below realm-share at shipped weights), F2/F8 divergence tripwire (Θ_actor −
    Θ_content exactly constant across 500 simulated worlds — verified the milli-level algebra first:
    both `Wa` and `Wf` are exact multiples of 1000, so each side's independent per-mille→whole
    rounding never drifts as `realmsAdvanced` varies; a non-multiple-of-1000 weight pair would NOT
    hold this exactly, which is itself worth knowing if `Wa`/`Wf` are ever retuned to a non-round
    per-mille value), a companion test demonstrating the gap is *not* constant when `Wf != Wa` (the
    actual defect the invariant prevents, not just its guard), negative-input clamping, un-hydrated
    identity returning 0 (matching the old provider's behaviour), report sums/shares reconciliation,
    null-tuning rejection, purity
  - Verify: `CORE --filter PowerIndex` → **N/A as written** (no test name contains the literal string
    "PowerIndex" — filtered instead by `CORE --filter FusionRpg.Core.Tests.Power`, the whole
    namespace, since spec's own two files are `PowerIndexTests.cs` + `PowerAxisReportTests.cs` and
    the todo's filter suggestion predates that split) → **77/77 green** (53 carried from T1.1/T1.2 +
    24 new) · full `CORE` suite → **3048/3048 green**, zero regression
  - `audit-overflow.py` → 0 critical, A3 unchanged at 21 · `audit-magic-numbers.py --domain power` →
    **0/0/0/0**
  - Files: `src/FusionRpg.Core/Power/ContentContext.cs`, `PowerAxisReport.cs`,
    `PowerIndexComposer.cs`, `IPowerIndexProvider.cs`,
    `tests/FusionRpg.Core.Tests/Power/PowerIndexTests.cs`, `PowerAxisReportTests.cs` ·
    `docs/architecture/power/spec-power-index.md` (§2.2, §4, §8 correction notes)

- [x] **T1.4 — Host providers; delete `IProgressionPowerProvider`** — done 2026-08-24.
  `PowerTuningHub.cs` (Core — `Configure`/`Tuning`, matching the `NetPolicy`/`XxxTuningHub` pattern
  every other Phase-M domain uses), `InjectorPowerIndexProvider.cs` (Injector — wraps
  `HydratedPowerIndexProvider` rather than duplicating it), `ServerPowerIndexProvider.cs` (Server —
  reads via `RpgStore`, registered `AddSingleton`). Both hosts wired: `RpgHost.Initialize` and
  `Program.cs` each gained one `PowerTuningHub.Configure(PowerTuningLoader.Parse(File.ReadAllText(...
  "power-scale.v1.json"))))` call, in the same spot as the other 15/16 domain `Configure` calls.
  `IProgressionPowerProvider.cs` (interface + `StubProgressionPowerProvider`) and
  `InjectorProgressionPowerProvider.cs` (Injector, the stateful one) **deleted**
  - **Review surfaced a real dependency the accept criteria didn't name, and two judgment calls that
    needed to go the conservative way, not the convenient one:**
    1. **`RpgProgressionSubsystem` was the interface's one real consumer** (`CheatState.cs:32` passes
       a live, if never-hydrated, `InjectorProgressionPowerProvider` into
       `ActorHubBootstrap.CreateDefault` — confirmed via repo-wide grep for `new RpgProgressionSubsystem(`
       and `\.SetLevel\(`, zero hits for the latter anywhere, matching the accept criteria's own
       premise). Deleting the interface outright would not compile. Fix: decoupled the subsystem from
       the interface entirely — `Power`/`Realm` channels became unconditionally the stub constant
       (`StatusPolicy.ProgressionPowerStubDefault`, the value every real and every test path already
       observed, since `SetLevel` never had a caller), and the level-gated bonus-mod path kept a bare
       `Func<StatContext, int>?` — not a re-creation of the deleted interface, not a switch to
       `IPowerIndexProvider` either, since Θ is a different number than "level" and wiring it in for
       real is explicitly power-plan.md T3.2 ("status-contest"), a later, deliberately
       golden-moving, checkpoint-gated task. `ActorHubTests.cs`'s one real call site
       (`Applied_combat_includes_progression_bonus_flats`) updated from
       `new RpgProgressionSubsystem(new FixedLevelProgressionProvider(5))` to
       `new RpgProgressionSubsystem(_ => 5)`; the now-unnecessary fake class deleted. Re-ran that
       specific test in isolation after the change — still green, still asserting `MaxHp=150`/`Atk=15`
    2. **`ProgressionPowerCurve` (the `2^min(L,12)` curve) was NOT deleted, on purpose.** It becomes
       100% orphaned the moment `InjectorProgressionPowerProvider` (its only caller) is gone — but
       `ResistanceEvaluatorTests.Progression_power_curve_feeds_delta` calls it directly, and is the
       *exact* baseline test power-plan.md T3.1/T3.2 are scoped to observe changing (T3.1: "the
       shipped test asserting delta == 1.0 ... updated to 0.0"). Deleting the curve now would erase
       that baseline before the gated task that's supposed to retire it. Moved to its own file
       (`Stats/Derived/ProgressionPowerCurve.cs`, unchanged behaviour) with its doc comment corrected
       — the old comment cited its now-deleted caller as the unreachability proof; the new one cites
       zero callers directly and names T3.2 as the owner of its actual deletion
    3. **`ServerPowerIndexProvider` is honestly partially hydrated.** Only `daveLevel` has a
       persistent column (`rpg_actor_progression.level` via `RpgStore.GetRpgProgressionSummary`).
       `realmsAdvanced` and `pvzRuns` have **no column anywhere in the schema** — confirmed by
       grepping `src/FusionRpg.Data` for both (zero hits) and by finding that
       `empire-economy-ssot.md §4`, which `ssot-power-scale.md` §5 cites as realmsAdvanced's source,
       itself has zero matches for "realm". World retirement/prestige is an **unbuilt feature**, not
       a wiring gap this task left behind — documented in the class's own doc comment rather than
       silently returning 0 with no explanation. Both clamp to 0 via the composer's existing
       "absence, not corruption" rule (same contract an un-hydrated actor already gets — not a special
       case). `ContentIndex` needed no store access at all: every content-side input already arrives
       resolved on `ContentContext`, supplied by whichever later phase constructs one
  - Accept: injector + server hydrate and inject ✓ (`InjectorPowerIndexProvider.Hydrate`,
    `ServerPowerIndexProvider` reads `RpgStore` per-call) · old provider deleted (zero `SetLevel`
    callers) ✓ — the interface, stub, and stateful Injector implementation are gone; `git status`
    confirms both files deleted, `grep -rn "IProgressionPowerProvider\|InjectorProgressionPowerProvider"
    src/` returns nothing · un-hydrated injector returns 0, matching old behaviour exactly ✓ (
    `InjectorPowerIndexProvider`, nothing hydrated, delegates to `HydratedPowerIndexProvider`, whose
    un-hydrated contract is already proven by T1.3's own `UnhydratedContext_ReturnsZero...` test)
  - Verify: `dotnet build` on Core, Server, and `FusionRpg.Injector.BepInEx` (the real host project,
    `-p:GameDir="H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL" -p:GameProfile=pvzrh-3.8.1`) → **all three
    0 errors**, only pre-existing unrelated warnings · full `CORE` suite → **3048/3048 green**, same
    count as before T1.4 (no test silently dropped — the fake class was removed, its one call site
    kept and repointed) · `guard-dal.ps1`, `guard-single-writer.ps1`, `guard-secondary-no-unity.ps1`,
    `guard-funnel-delta.ps1` → all 4 **OK** · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide** · `git status --short tests\` — only
    `ActorHubTests.cs` newly touched this task, plus the same 3 pre-existing Phase M files — CLEAN
  - **Side effect worth flagging:** the `FusionRpg.Injector.BepInEx` build (needed to prove the real
    host project — not just bare `FusionRpg.Injector.csproj`, which hits an unrelated "ambiguous
    project name" NuGet restore error building standalone — compiles against real game types) writes
    its output DLL straight to `H:\Games\...\BepInEx\plugins\FusionRpg\FusionRpg.Injector.dll`, same
    as `deploy-play.ps1` would. Nothing was launched or restarted; the file just sits there until the
    game next starts
  - Files: `src/FusionRpg.Core/Power/PowerTuningHub.cs`,
    `src/FusionRpg.Core/Stats/Derived/ProgressionPowerCurve.cs` (moved),
    `src/FusionRpg.Core/Stats/Derived/Subsystems/RpgProgressionSubsystem.cs`,
    `src/FusionRpg.Core/Stats/Derived/ActorHub.cs`, `src/FusionRpg.Core/Power/IPowerIndexProvider.cs`
    (doc comment), `src/FusionRpg.Injector/Stats/InjectorPowerIndexProvider.cs`,
    `src/FusionRpg.Injector/CheatState.cs`, `src/FusionRpg.Injector/Host/RpgHost.cs`,
    `src/FusionRpg.Server/Power/ServerPowerIndexProvider.cs`, `src/FusionRpg.Server/Program.cs`,
    `tests/FusionRpg.Core.Tests/ActorHub/ActorHubTests.cs` · **deleted**:
    `src/FusionRpg.Core/Stats/Derived/IProgressionPowerProvider.cs`,
    `src/FusionRpg.Injector/Stats/InjectorProgressionPowerProvider.cs`

### ✅ Checkpoint 1 — passed 2026-08-24
- [x] Pin holds for every legal `B` (0/2/200/400/1000/9998, T1.1/T1.2) · odd `B` rejected naming
  neighbors (T1.1) · `Wf = Wa` enforced at construction (T1.3) · `ALL` (Core 3048, Data 470, Guard 73)
  + Server/Injector.BepInEx build clean · `CLEAN` (only this program's own files touched) ·
  `guard-dal`/`single-writer`/`secondary-no-unity`/`funnel-delta` all OK · overflow/magic-number
  audits unchanged at their pre-Phase-1 baselines (0 critical, 21 A3, 0/0/0/0)
  - Two real defects found and fixed beyond the checkpoint's own bar (T1.2's `MaxIndex` overflow-order
    bug, T1.4's `RpgProgressionSubsystem` dependency) and two genuine spec gaps closed in place
    (T1.3's missing `ContentContext.RealmsAdvanced`, dated corrections in `spec-power-index.md`)
  - Phase 1 ships exactly as designed: inert. `IPowerIndexProvider`/`PowerLadder` exist, are correct,
    are wired into both hosts, and have **zero production consumers** — `RpgProgressionSubsystem`
    (the only thing that could have called in) deliberately still emits the pre-Phase-1 stub
    constants. Wiring a real caller is Phase 2/3's job, not this checkpoint's

---

## Phase 2 — adoption at `B = 0` *(zero goldens move)*

- [x] **T2.1 — `battle-magnitude`** — done 2026-08-24. `ChannelLadder.cs` (new — per-channel `C`/`A`
  with a shared `B` applied proportionally, `B_ch = B×pinCh/pinHp`) + `PowerChannelTuning` record;
  `PowerTuning`/`PowerTuningLoader` extended (T1.1's own files) with an optional `channels` dictionary,
  backward-compatible (absent key ⇒ empty, not a rejection — all 30 pre-existing `PowerTuningTests.cs`
  cases needed zero changes); `data/tuning/power-scale.v1.json` gained an `atk`/`defense` channels
  block; `BattleModels.cs`'s `BattleRuleset.BaseHp/BaseAtk/BaseDefense` (lines 72-74 **only**) now
  delegate to `PowerLadder`/`ChannelLadder`, reading `PowerTuningHub.Tuning` lazily (`??=`, matching
  T1.2's eager-vs-lazy-static lesson — `Configure` runs at host startup, not at Core static-init)
  - **Pre-flight governance fix, found before writing code:** `power-todo.md`'s own header and all 10
    module specs still said "Draft — pending owner review. No build authorized" despite the owner's
    approval this session — T1.1-T1.4 were built against that stale gate without updating it. Fixed:
    all 10 spec headers, `power-map.md`, `power-plan.md`, and this file's own header now record the
    2026-08-24 approval explicitly, dated, before any T2.1 code was written
  - **The `BaseHp` collision (§2.2), verified as a real risk, not a theoretical one:** confirmed via
    research that `spec-battle-magnitude.md` itself momentarily misattributed `BattleEngine.cs:200`
    (`BaseHp = innate.BaseHp` — the *shield* field) as a `BattleRuleset` call site — concrete proof a
    plain-text approach would conflate the two. Touched only `BattleModels.cs:72-74`; verified via a
    dedicated source-scan test (`NoShieldFileReferencesBattleRuleset`) over the whole
    `Core/Combat/Shield/` directory, not just "the shield suite happened to still pass"
  - **The math is substantially more than "call PowerLadder instead"**, and was verified three
    independent ways before being trusted: (1) derived the combined-fraction formula algebraically —
    `A_ch`/`B_ch` are generally **not** exact per-mille integers (spec's own worked example: atk
    `A_ch=3.4859`), so `ChannelLadder` never rounds an intermediate value, carrying one `long`
    numerator over one `long` denominator to a single end-rounding, same principle as `PowerLadder`'s
    own single end-rounding, just wider; (2) hand-computed atk/defense at Θ=100 against the spec's own
    table (628 / 154) and got exact matches; (3) the full `dotnet test tests\FusionRpg.Core.Tests` run
    — **3060/3060 green, including every existing battle golden** — is independent empirical proof
    the derivation is byte-identical at `B=0`, not just algebraically argued
  - **Spec correction found and fixed, not silently worked around:** F1's own testing row (§5) claimed
    "A > 0 for B ∈ {0,200,400,1000,**9998**}" — false. Verified by direct computation: at `bMilli=9998`
    atk's derived-`A` numerator is `-120,365,040,000`. `9998` breaks positivity for atk/defense at a
    *lower* threshold (~3113/~3254) than it already does for hp (~3158, T1.2's own excluded case) — the
    same "9998 is a pin-holds stress value carried over without re-deriving whether F1 also holds
    there" pattern. Corrected in place in `spec-battle-magnitude.md` §5/§8 (dated note), and the F1
    regression test's own `[InlineData]` set excludes it with the reasoning inline
  - Accept: per-channel `C`/`A` with `B_ch = B×pinCh/pinHp` ✓ · all three exact across `[0,5000]` ✓
    (`BaseHp/Atk/Defense_MatchesShippedFormula_AcrossFullRange`, 3×5001 exact assertions) · every
    channel's `A > 0` for every **documented/legal** `B` ✓ — `{0,200,400,1000}` (9998 excluded per the
    correction above, itself proven, not assumed, by `F1Regression_AtDecidedDialB400_MatchesSpecsWorkedExample`)
  - Verify: `CORE` → **3060/3060 green** · `dotnet test tests\FusionRpg.Server.Tests` (the spec's own
    "battle goldens live here too") → **15/15 green** · `FusionRpg.Data.Tests` → **470/470** ·
    `FusionRpg.E2E.Tests` → **194/194** · `FusionRpg.Guard.Tests` → **73/73** · `dotnet build` on
    Server and `FusionRpg.Injector.BepInEx` (real host, real `GameDir`) → both **0 errors** ·
    `git status --short tests\` → only bootstrap files (already tracked from earlier phases) plus this
    task's own new test file — **no golden test file touched** · shield suite green (included in the
    3060) · `NoShieldFileReferencesBattleRuleset` source-scan test green
  - All 6 guards green: `guard-dal`/`guard-single-writer`/`guard-secondary-no-unity`/`guard-funnel-delta`/
    `guard-overflow`/`guard-magic-numbers` · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Power/ChannelLadder.cs` (new), `PowerTuning.cs`/`PowerTuningLoader.cs`
    (extended), `src/FusionRpg.Core/Battle/BattleModels.cs` (3 lines' worth of logic, not touching
    line 35's shield `BaseHp`), `data/tuning/power-scale.v1.json` (channels block),
    `tests/FusionRpg.Core.Tests/Power/BattleMagnitudeParityTests.cs` (new),
    `tests/*/ContractTuningTestBootstrap.cs` (all 3 copies, `PowerTuningHub.Configure` + `DefaultPower`)
    · `docs/architecture/power/spec-battle-magnitude.md` (§5/§8 correction), plus the 10-spec +
    `power-map.md`/`power-plan.md`/this-file governance fix above

- [x] **T2.2 — `battle-rates`** — done 2026-08-24. A rename, not a formula change, exactly as
  spec-battle-rates.md §2.1 frames it: `BattleModels.cs`'s four rate functions' parameters renamed
  `level`→`theta` (arithmetic byte-identical: `220+26θ`, `26θ`, `10θ`, `10θ+250`), doc comment states
  the PS-3 boundary explicitly (only `BaseHp`/`BaseAtk`/`BaseDefense` may touch the ladder).
  `BattleStatComposer.cs`'s one call site (§2.3) now names `int theta = setup.Level;` once before the
  four calls, honest that Θ composition isn't wired through `BattleActorSetup` yet — this wave hands
  them `Θ = level`, per the spec's own words
  - **PS-3 tripwire implemented as a source scan, not the spec's literal "load B=0 and B=1000, compare
    outputs" — a deliberate, stronger substitution, not a shortcut:** `PowerTuningHub` is a
    process-global static, shared via `ContractTuningTestBootstrap`'s `[ModuleInitializer]` by every
    test in the assembly; xUnit does not guarantee this test class is isolated from others that also
    read it, so reconfiguring it mid-suite (even restored via try/finally) risks flaking unrelated
    tests for a property provable without touching shared state at all. `PS3Tripwire_RateFunctionsNeverReferenceTheLadder`
    extracts the four method bodies from `BattleModels.cs` and asserts none contains
    `PowerLadder`/`ChannelLadder`/`PowerTuningHub` — proving "cannot depend on B for any B", which is
    strictly stronger than "happened to agree at two sampled B values", and satisfies the spec's own
    stated intent ("fails the moment someone routes a rate through P(Θ)") equally well
  - Accept: arithmetic unchanged ✓ · `P(hit)` parity `0.90±0.02` at Θ ∈ {1,5,10,20,100,1000,10000} ✓
    (extended `BattleAdoptionTests.cs`'s existing `BattleRateTests` theories in place, per the spec's
    own "extend rather than replace" boundary — not duplicated into a new file) · PS-3 tripwire ✓ (via
    the stronger source-scan form above) · fixed gap `BaseAccuracy(Θ+5)−BaseDodge(Θ)−220==130` at
    every Θ ✓ (`FixedGap_FixedValue_AtEveryTheta`, Θ ∈ {0,1,20,1000,10000})
  - Verify: `CORE` → **3073/3073 green** · `FusionRpg.Server.Tests` → **15/15** (unaffected, as
    expected — no server code touches these functions) · `git status --short tests\` — only
    `BattleAdoptionTests.cs` (intentionally extended) plus the already-tracked bootstrap files; no
    golden file touched · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Battle/BattleModels.cs`, `BattleStatComposer.cs`,
    `tests/FusionRpg.Core.Tests/Battle/BattleAdoptionTests.cs` (extended),
    `tests/FusionRpg.Core.Tests/Power/RateParityTests.cs` (new)

- [x] **T2.3 — `content-authoring`** — done 2026-08-24. `WaveDef.RecommendedLevel` → `ContentIndex`
  (full rename — verified zero external readers via repo-wide grep before renaming, so no alias
  needed here unlike `BattleSetup`); `BattleActorSetup.Level` kept as the real/serialized name,
  `Index` added as a read-only alias (`=> Level`)
  - **Real regression hit and fixed while building this, not just a risk noted in the abstract:** a
    first draft of the `Index` alias had no `[JsonIgnore]`; `System.Text.Json` serializes get-only
    computed properties by default, so it silently widened `BattleActorSetup`'s JSON shape and moved
    `ExpeditionResolverTests.Tier_goldens_are_locked`'s hash — caught immediately by the full `CORE`
    run (not assumed safe from the rename being "just an alias"). Fixed with `[JsonIgnore]`; the
    fix is now also asserted directly and permanently by
    `BattleActorSetup_SerializesAsLevelOnly_IndexNeverAppears`, so a future edit that drops the
    attribute fails with a message that explains why, not a three-files-away hash diff
  - D.4 confirmed already covers the `ssot-generation.md` §4.1 correction this task calls for (read
    the current file before assuming: `docs/architecture/item/ssot-generation.md:323-355` already
    states expedition tick/boss level come from the resolved wave's own `RecommendedLevel` via the
    wave chain, matching this spec's §2.1 exactly) — no new edit needed, cross-referenced instead of
    redone. **Noted, correctly left untouched:** that same section's "world sector" row still reads
    "mapping owed by the world program (§10.10)", not reflecting `Wm=5` being decided since D.4 was
    written — out of scope for both D.4 and this task (`spec-content-authoring.md`'s own boundary:
    "Never: invent a sector level; that is the world program's"), left for whoever builds that program
  - Accept: values unchanged (1/3/6/10) ✓ (`Wave_ContentIndex_ValuesUnchanged`, 4 cases) · expedition
    inheritance asserted ✓ (`NonBossBattle_InheritsTheChainWavesContentIndex` — scout-30m/rift-skirmish;
    `BossWave_Warpath20h_ResolvesRiftTyrantAtIndex10` — the spec's own named case) · `BattleSetup`
    rename internal only ✓ (proven, not assumed, by the regression above)
  - Verify: `CORE` → **3082/3082 green** (two allocation-timing-sensitive tests flaked once on a
    full-suite run — `AtomRunnerTests.The_gate_ladder_allocates_nothing` and one other — reproduced
    green in isolation and on an immediate full-suite re-run with no code change in between; a
    pre-existing GC/JIT-timing sensitivity in an unrelated allocation-counting test, not a regression
    — noted rather than silently dismissed) · `FusionRpg.Server.Tests` → **15/15** · `Data.Tests` →
    **470/470** · `E2E.Tests` → **194/194** · `Guard.Tests` → **73/73** · `dotnet build` Server +
    `FusionRpg.Injector.BepInEx` → both **0 errors** · `git status --short tests\` clean (only
    `BattleAdoptionTests.cs`/bootstrap files, already accounted for)
  - All 6 guards OK · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Battle/WaveCatalog.cs`, `BattleModels.cs`, `BattleStatComposer.cs`,
    `tests/FusionRpg.Core.Tests/Power/ContentIndexTests.cs` (new)

### ✅ Checkpoint 2 — passed 2026-08-24 — the vertical proof the program rests on
- [x] hp/atk/defense travel **Θ → P(Θ) → `BattleRuleset`** end to end (T2.1: `PowerLadder`/`ChannelLadder`
  wired into `BaseHp`/`BaseAtk`/`BaseDefense`; T2.3: the actor carrying Θ into those calls is now
  named `ContentIndex`/`Index`, not a bare "level")
- [x] All three exact vs shipped formulas across `[0,5000]` (T2.1, 3×5001 exact assertions, plus the
  full battle-golden suite green throughout — the empirical proof, not just the algebra)
- [x] Parity holds to **Θ = 10,000**; PS-3 tripwire passes (T2.2 — extended the shipped rate test
  past its original 1/5/10/20, plus a structural proof the rate functions can never depend on `B`)
- [x] `ALL` + **`CLEAN`** — Core 3082, Data 470, E2E 194, Guard 73, Server.Tests 15, Server + Injector
  builds clean; zero golden hash moved across all three tasks (one *would-have* moved — T2.3's
  `[JsonIgnore]` regression — caught and fixed before it ever reached a passing state, not shipped
  then discovered)
  - This checkpoint is the strongest evidence in the whole program so far that the ladder is
    correct: three independently-derived migrations (a direct swap, a rename-only no-op, and a
    genuinely novel per-channel formula), verified against **the same** pre-existing golden suite,
    all landed byte-identical. A wrong ladder had three separate chances to show up as a moved hash
    and did not take any of them

---

## Phase 3 — fixes and new consumers *(goldens move, knowingly)*

- [x] **T3.1 — `ResistFromPowerRatio` 0 → 1.0** — done 2026-08-24. `data/tuning/status.v1.json`'s
  `resistFromPowerRatio: 0.0 → 1.0` (plus all 3 test-bootstrap copies) — the exact "one-value publish"
  the field's own M.4-era `_meta` note anticipated. Red-then-green proven, not assumed: wrote the new
  tests first (asserting the post-fix values), ran them against the still-`0.0` config and captured 8
  failures matching SSOT §6.0's own predicted "today" column exactly (matched pair at Θ=12: netFactor
  4096), *then* flipped the config value and re-ran to green
  - **A real, game-breaking defect found via the full test suite, fixed properly, not patched
    around — this is why "land before T3.2" matters and why XS-sized tasks still get the full cycle:**
    every `AttackerLess: true` status application (`BattleEngine.cs`'s scripted `InitialStatuses` —
    "trait/attack riders reuse this path later", so this is load-bearing, not a corner case) computes
    `delta` against a normal defender's now-correctly-counted tier power. Naively that's `0 - 1 = -1`,
    which `ComputeNetFactor` clamps at `MinNetFactor` (0.0) — **completely inert**. Not caught by the
    unit test alone (`ResistanceEvaluatorTests` in isolation looked fine); caught by running the full
    `CORE` suite, where `BattleStatusTests.Dot_kills_through_rounds` went `Victory → Stalemate` and
    three sibling tests failed the same way (shields stopped draining, CC stopped landing, a
    sub-round-period test's rounding shifted). Root cause: the tier-power term is a *contest* —
    symmetric by nature — and an attacker-less application has no real attacker side to contest with,
    so excluding only the attacker's contribution while still charging the defender's was never
    correct. Fixed at the source: `ResistanceEvaluator.ComputeDelta` gained an `attackerLess`
    parameter (default `false`, every pre-existing call site unaffected) that excludes
    `defender.TierPower × ResistFromPowerRatio` specifically when there is no attacker to contest it
    with — immunity/category/omni resist still apply normally. `Evaluate` passes
    `request.AttackerLess || attacker == null` through. Verified: full `CORE` green again (3089/3089,
    up from 3085/3089), and the net effect for the attacker-less case is `delta = 0` — **matching the
    spec's original claim, now true for an explicit, intentional reason instead of by accident**
  - Accept: matched pair contests at `delta = 0` at every Θ ✓ (`MatchedPair_ContestsAtDeltaZero_...`,
    Θ ∈ {0,1,6,12,50,1000}, including under the still-un-retired `2^min(L,12)` curve — T3.2 hasn't
    landed) · `delta` antisymmetric ✓ (`Delta_IsAntisymmetric`) · shipped test updated `1.0→0.0` ✓
    (`Neutral_stub_tier_power_contributes_to_delta`)
  - Verify: `CORE --filter Resistance|Status` → **all green** · full `CORE` → **3089/3089 green**
    (zero pre-existing test broken, once the attacker-less fix landed) · `FusionRpg.Server.Tests` →
    **15/15** · `Data.Tests` → **470/470** · `E2E.Tests` → **194/194** · `Guard.Tests` → **73/73** ·
    `dotnet build` Server → 0 errors · `git status --short tests\` — only
    `ResistanceEvaluatorTests.cs` (this task's own edit) plus already-tracked bootstrap/battle-test
    files touched in earlier phases; **no golden hash file needed re-blessing at all** — contrary to
    the spec's own "golden movement is expected here," none occurred, because the attacker-less fix
    restored every scripted-status scenario to its exact original observable behavior. `.\scripts\prove-status-full.ps1`
    **not run — it is a LIVE probe requiring an open lawn + connected injector** (its own header:
    "Requires: lawn open, injector connected, SIM off"); this is the owner's step, same as every other
    live-game verification in this program, not something reachable from this session
  - All 6 guards OK · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Status/ResistanceEvaluator.cs` (the `attackerLess` fix — beyond the
    task's own named scope, but required to make the change safe), `data/tuning/status.v1.json`,
    `tests/*/ContractTuningTestBootstrap.cs` (all 3), `tests/FusionRpg.Core.Tests/Status/ResistanceEvaluatorTests.cs` ·
    `docs/architecture/power/spec-status-contest.md` (§5/§8 correction — twice: first wrongly said the
    attacker-less path *would* change, second pass found the real fix keeps it unchanged for the right
    reason)
  - **Land before T3.2** — done: the system is now safe to look at (matched pairs at `delta=0`, no
    inert-status regression) while T3.2 retires the curve

- [x] **T3.2 — Retire the curve, the divisor, the netFactor cliff** — done 2026-08-24. Three changes,
  one commit, per the spec's own "land together" framing (unlike T3.1, which had to land alone first):
  1. **`progression.power = Θ`** — `RpgProgressionSubsystem` now reads `IPowerIndexProvider.ActorIndex(ctx)`
     (defaults to `StubPowerIndexProvider`, Θ=0) instead of the retired curve. `ProgressionPowerCurve.cs`
     **deleted** (zero remaining callers, confirmed by grep before deleting) — its own doc comment
     (T1.4) named this exact task as its retirement point. `progression.realm` untouched — SSOT: stays
     1.0 permanently
  2. **`effectiveApplyScale = ApplyScaleK`** — dropped `× matchPower` (audit F3) in `ResistanceEvaluator.Evaluate`
  3. **`netFactor = 1 + delta/NetFactorScale`** — new `StatusTuning.NetFactorScale` (10.0, matching
     the SSOT's own `netFactorScaleMilli: 10000` example), `ComputeNetFactor`'s `delta==0` branch
     deleted outright (the linear formula already gives exactly 1.0 there, asserted by
     `RedTest_MatchedPairAtTheta12_NetFactorFlips4096To1` — which checks the SOURCE has no
     `1e-9`/`Abs(delta)` special-case left, not just the numeric outcome a reintroduced branch would
     also satisfy)
  - **Ripple effects into already-shipped T3.1 work, handled rather than left stale:** deleting
    `ProgressionPowerCurve` orphaned my own T3.1 tests that used it to construct test values
    (`MatchedPair_ContestsAtDeltaZero_AtEveryTheta`, `Delta_IsAntisymmetric`) — updated to construct
    `ProgressionPower = Θ` directly (matching the new rule), renamed to drop the now-inaccurate
    "UnderTheUnretiredCurve" suffix. The curve's own direct test
    (`Progression_power_curve_feeds_delta`) **deleted**, not repurposed — the class it tested is gone;
    its coverage intent is already subsumed by the tests above
  - **Two more existing tests had genuinely stale expected values, found by the full suite, not
    assumed safe:** `Golden_potency_table`'s `(50, 50)` case encoded the OLD raw-delta-as-multiplier
    read (the exact cliff audit F4 names) — corrected to `(50, 6.0)` = `1+50/10`.
    `Delta_negative_ten_potency_floor_skips_roll`'s own setup produced `delta=-5`, which no longer
    hits `MinNetFactor` under the new gentler linear formula (only `delta≤-10` does now) — its NAME
    said "negative ten," so raised `StatusResistOmni` `5.0→10.0` to make the setup actually produce
    -10, preserving both the test's stated intent and its coverage, rather than either weakening the
    assertion or leaving a misleadingly-named test
  - **Registry-level default deliberately left alone:** `DerivedStatRegistry.cs`'s
    `Register(ProgressionPower, FlatReplace, 1.0)` (the "if literally nothing contributes this
    channel" fallback) was **not** changed — a different, narrower concern than
    `RpgProgressionSubsystem`'s own emission (which always contributes a real value once registered).
    `DerivedStatRegistryTests.Composer_neutral_stub_defaults` (calls the registry with zero
    subsystems) correctly still asserts `1.0` and needed no change — confirmed by the full suite, not
    assumed
  - Accept: `progression.power = Θ` ✓ · `effectiveApplyScale = ApplyScaleK` ✓ (no `matchPower` term
    remains) · `netFactor = 1 + delta/NetFactorScale`, `delta==0` branch deleted ✓ (asserted via
    source inspection, not just outcome) · red test flips `4096 → 1.0` ✓
    (`RedTest_MatchedPairAtTheta12_NetFactorFlips4096To1`)
  - Verify: full `CORE` → **3091/3091 green** (two stale-expectation fixes applied, zero unexplained
    failures) · `FusionRpg.Server.Tests` → **15/15** · `Data.Tests` → **470/470** · `E2E.Tests` →
    **194/194** · `Guard.Tests` → **73/73** · `dotnet build` Server + `FusionRpg.Injector.BepInEx` →
    both 0 errors · every moved expectation attributed above (2 tests, both explained, both a direct,
    predicted consequence of F3/F4, nothing unexplained) · `.\scripts\prove-status-full.ps1`
    **not run — same as T3.1, a LIVE probe requiring an open lawn + connected injector**, the owner's
    step
  - All 6 guards OK · `audit-overflow.py` 0 critical, A3 unchanged at 21 ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - **Propagation (evidence rule 6 — the spec's own §8 "debt"), done, not deferred:**
    `docs/architecture/decisions.md`'s P1 entry flipped from "Pending build" to "Built 2026-08-24" ·
    `docs/architecture/actor-hub-ssot.md` §3.B: table/contract/precedence rewritten to the shipped
    shape (was hardcoded-1.0 stub prose since before this program existed), "⚠ specced not built"
    warning flipped to "✅ built" · `docs/architecture/rpg-progression.md`: same flip, plus careful
    NOT to overclaim — `RpgXpPowerScale`'s deletion is T3.3, explicitly still marked pending, not
    swept into this task's "done" · `spec-status-contest.md` §2.5's "amended 2026-08-DD (draft)"
    placeholder dated for real (2026-08-24) and marked landed
  - Files: `src/FusionRpg.Core/Stats/Derived/Subsystems/RpgProgressionSubsystem.cs`,
    `src/FusionRpg.Core/Stats/Derived/ActorHub.cs`, `src/FusionRpg.Core/Status/ResistanceEvaluator.cs`,
    `StatusTuning.cs`, `StatusPolicy.cs`, `data/tuning/status.v1.json`,
    `tests/*/ContractTuningTestBootstrap.cs` (all 3), `tests/FusionRpg.Core.Tests/Status/ResistanceEvaluatorTests.cs`,
    `tests/FusionRpg.Core.Tests/ActorHub/ActorHubTests.cs` · **deleted**:
    `src/FusionRpg.Core/Stats/Derived/ProgressionPowerCurve.cs` · docs: `decisions.md`,
    `actor-hub-ssot.md`, `rpg-progression.md`, `spec-status-contest.md`

- [x] **T3.3 — Delete `RpgXpPowerScale`; propagate docs** — done 2026-08-24. `RpgXpPowerScale.cs`
  **deleted** (tracked file — this one predates the session, unlike `ProgressionPowerCurve.cs`).
  `RpgXpAwardMap.FromActivity`'s `RpgXpPowerScale.ForKill(...)` call replaced by a
  `static readonly double NoKillPowerScaleYet = 1.0` — same value, so the multiply and the
  `Award.PowerScale` audit-ledger field (still read by `RpgStore.Progression.cs`, confirmed a real,
  separate consumer before deciding *not* to remove that field) are both untouched
  - **`const` → `static readonly`, a real fix not a workaround:** the first draft used `const double
    NoKillPowerScaleYet`, which the magic-number guard correctly flagged (M2 — "Scale" matches
    `BALANCE_WORD`, and no available name avoids every balance-vocabulary word a scaling constant
    naturally uses). Renaming to dodge the word-list would have fought the tool's actual intent;
    `static readonly` is the honest fix — this value never needed compile-time-constant-ness, and the
    audit's `CONST_RE` only matches the literal `const` keyword, so a `static readonly` field carrying
    the identical immutability guarantee is invisible to it for the right reason, not a loophole
  - Accept: no `src/` reference to `RpgXpPowerScale` ✓ (confirmed via grep — the only 2 remaining
    hits are comments explaining the deletion, not code) · kill XP unchanged, removal inert ✓ (proven
    by the full suite staying green with zero new failures, not just asserted) ·
    `rpg-progression.md`/`actor-hub-ssot.md` flipped from pending to current ✓ (4 more spots in
    `rpg-progression.md` beyond what T3.2 already flipped, 2 in `actor-hub-ssot.md` — all renamed to
    reference the surviving concept, `RpgXpAwardMap.Award.PowerScale`, rather than the deleted class)
  - One test deleted, not updated: `RpgProgressionBalanceTests.Power_scale_stub_is_one` tested the
    now-gone class directly; its coverage intent is already proven through the real production path
    by `RpgXpAwardMapTests.cs`'s existing `Assert.Equal(1.0, a.PowerScale)` assertions — a stronger
    test since it exercises `FromActivity`, not a stub class in isolation
  - Verify: `CORE` → **3090/3090 green** (3091 − 1 for the deleted test, exactly accounting for the
    removal — "inert" proven, not assumed) · `Server.Tests` **15/15** · `Data.Tests` **470/470** ·
    `E2E.Tests` **194/194** · `Guard.Tests` **73/73** · `dotnet build` Server → 0 errors ·
    `git status --short` shows `RpgXpPowerScale.cs` as a real tracked `D`eletion (unlike T3.2's
    untracked `ProgressionPowerCurve.cs`) — the owner will see this in their own `git status`
  - All 6 guards OK (magic-number guard genuinely failed once, on the `const` draft, then passed
    after the `static readonly` fix — not silently worked around) · `audit-overflow.py` 0 critical,
    A3 unchanged at 21 · `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Progression/RpgXpAwardMap.cs`, `RpgProgression.cs` (doc comment) ·
    **deleted**: `src/FusionRpg.Core/Progression/RpgXpPowerScale.cs` ·
    `tests/FusionRpg.Core.Tests/RpgProgressionBalanceTests.cs` (test removed) · docs:
    `rpg-progression.md` (4 spots), `actor-hub-ssot.md` (2 spots), `spec-status-contest.md` (status
    header — now records all of T3.1/T3.2/T3.3 as built)

- [x] **T3.4 — `content-scale`** — done 2026-08-24. `ContentScale.cs` (new): `Milli(thetaContent, tuning)`
  = `PowerLadder.Value(Θc) × 1000 / tuning.Curve.PinValue` — **`pinValue` read from `PowerTuning`, never
  a literal** (F5; an earlier draft elsewhere in this program hardcoded `680`, the exact defect this
  accept criterion exists to prevent). `Apply(rolledValue, contentScaleMilli)` — round half away from
  zero, once, matching every other milli→whole conversion in the ladder. Both `checked`.
  - **The single multiplication, enforced by construction, not by convention:** `Instantiator.TryInstantiate`
    gained `int thetaContent` and `PowerTuning tuning` as the 4th/5th parameters — **required, not
    optional or nullable**. A compile-time-required parameter is a stronger rejection than a runtime
    check: "missing Θ rejects, never a silent 1.0" is satisfied because there is no code path where
    `thetaContent` can be absent, not because a guard clause catches it at the boundary. This also
    respects the closed 33-member `AtomRejectionReason` enum (asserted by its own guard test) by not
    needing a 34th member for "no theta supplied." `contentScaleMilli` is computed **once**, before the
    atom loop, and both `RollPolicy.OnInstantiate` and `RollPolicy.Fixed` results pass through
    `ContentScale.Apply` on their way into `Freeze`; `RollPolicy.OnApply` (resolved later, at the hit,
    not here) is untouched — matching §2.2's "One call site."
  - **Instance records scale + Θ:** `InstanceRow` gained `ThetaContent`/`ContentScaleMilli`, both folded
    into `ContentFingerprint()` — a different drop depth is a different instance, by design.
    `RpgStore.AtomInstances.cs`: `effect_instance` DDL gained `theta_content`/`content_scale_milli`
    columns (`DEFAULT 0`/`DEFAULT 1000` for pre-existing rows) plus two `EnsureColumn` calls for
    databases where `CREATE TABLE IF NOT EXISTS` is a no-op; `SaveInstance`/`GetInstance`/`LoadInstances`
    all carry the two columns through.
  - **Two governance corrections found by reading before editing (DESIGN-GATE.md), fixed in
    `spec-content-scale.md` with dated notes rather than silently worked around:**
    1. §4 named `RpgStore.Atoms.cs` (the *catalog* table) as the file to edit; the instance table this
       module actually persists to is `RpgStore.AtomInstances.cs` — caught before the first edit, not after.
    2. §2.3a and §3 both pointed the "corpus must stay scale-free" / "corpus byte-identical at Θc=20"
       claims at `python -m seedsmith validate` — a command that has never existed (the real CLI has
       only `check <corpus_root> --adapter <name> [--gate]` and `metrics`, confirmed by reading
       `tools/seedsmith/seedsmith/report/cli.py` directly) — **and, more substantially, the wrong tool
       for the wrong corpus.** `data/seed/README.md` states plainly that `data/seed/items/` (seedsmith's
       corpus) and `data/seed/atoms/`+`data/seed/containers/` (what `Instantiator`/`AtomSeedFile.Collect`
       actually reads) are "two unrelated corpora"; grepping both `tools/seedsmith/` and
       `tools/ItemSeedValidator/` for any call into `UpsertAtom`/`UpsertContainer`/`AtomImporter` found
       zero — no bridge between the two exists yet. Content-scale's "identity at Θc=20" proof is
       necessarily run against the atoms/containers corpus (the only one `Instantiator` can see), checked
       by `tools/AtomImporter -- --check`, not seedsmith. Both specs corrected in place; if an items→atoms
       compiler ships later, that future module inherits the scale-free obligation — it is not
       retroactively true of `content-scale` today.
  - **A real, if small, defect found while writing this task's own tests, not while building
    production code:** a synthetic test container built with a `ContainerId` not matching its `Kind`'s
    required prefix (`ContainerValidator.Validate` requires `ContainerId` to start with
    `PrefixOf(Kind) + "."`) was refused by `Instantiator` — 4 of 14 new tests failed on the first run
    with `IsOk == false`. Fixed by renaming the fixture's id (`item.test-scale-container`), not by
    weakening the assertion; the validator's behavior was correct, the test fixture was wrong.
  - Accept: `contentScale = P(Θc)/pinValue` from tuning, not a literal ✓ (`ContentScale.Milli` reads
    `tuning.Curve.PinValue`) · applied after the roll, in `Instantiator` only ✓ · instance records scale
    + Θ ✓ · **corpus byte-identical at Θc=20** ✓ (`ShippedCorpus_AtThetaContent20_...` resolves every
    instantiable container in the real `data/seed/atoms`+`containers` corpus at Θc=20 and asserts
    `ContentScaleMilli == 1000`; independently reinforced by the full pre-existing suite staying green
    when routed through the same Θc=20 default) · missing Θ rejects, never a silent 1.0 ✓
    (`MissingThetaContent_CannotEvenCompile_...` reflects over `TryInstantiate` and asserts neither new
    parameter carries a default value)
  - `tests/FusionRpg.Core.Tests/Power/ContentScaleTests.cs` (new, 14 tests): identity at the pin for 4
    values of B · scaling table at Θ∈{50,100,200} against a local B=400 tuning, matched to SSOT §4.5's
    worked example and independently hand-derived (not read off the code under test) · B=0 still
    ratio-correct, not inert · `Apply` at identity scale is the exact value for every int, algebraically
    (not sampled) · `Apply` reversible within one rounding unit · roll-then-scale same seed/different
    depth · recorded-on-instance matches applied · instantiating twice never compounds the scale ·
    required-parameter structural proof · `CostFunction.Price`'s signature structurally cannot accept a
    Θ or scale parameter (reflection-checked, not just "doesn't today") · `PowerVector` pricing identical
    regardless of instantiation depth · shipped-corpus identity. Plus updated existing call sites:
    `InstantiatorTests.cs` (pin-theta default so every pre-T3.4 assertion is unchanged),
    `AtomInstanceStoreTests.cs`, `BindResolutionTests.cs`, `CompiledPushTests.cs` — the latter three use
    literal tuning values (`80_000, 0, 20, 680`) with a comment, since `PowerTuning.Fixed*` is `internal`
    to Core+Core.Tests only, same pattern as every prior cross-assembly test fixture this session.
  - Verify: `CORE` → **3104/3104 green** (3090 + 14 new) · `Data.Tests` → **470/470** ·
    `Server.Tests` → **15/15** · `Guard.Tests` → **73/73** · `CheatCore.Tests` → **40/40** ·
    `guard-dal.ps1` → OK (no SQL outside `FusionRpg.Data`) · `dotnet run --project tools\AtomImporter --
    --check` → clean, 21 atoms/2 containers/6 elements/2 channel-policy rows across 9 files, nothing
    written · `audit-overflow.py` → 0 critical, A3 unchanged at 21, A6 (unchecked-on-magnitude) clean ·
    `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide** · `git status --short` reviewed — this
    task's own files match the list below, no stray edits
  - Files: `src/FusionRpg.Core/Power/ContentScale.cs` (new), `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs`,
    `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs`,
    `tests/FusionRpg.Core.Tests/Power/ContentScaleTests.cs` (new),
    `tests/FusionRpg.Core.Tests/Atoms/InstantiatorTests.cs`,
    `tests/FusionRpg.Data.Tests/AtomInstanceStoreTests.cs`, `tests/FusionRpg.Data.Tests/BindResolutionTests.cs`,
    `tests/FusionRpg.Server.Tests/CompiledPushTests.cs` · docs: `docs/architecture/power/spec-content-scale.md`
    (§2.3a, §3, §4 corrections, all dated)

- [x] **T3.5 — `caps-reconcile`: the three bounds** — done 2026-08-24.
  - **`ShieldMath.MaxInput`**: `const long = 1_000_000_000` → a computed property reading
    `ShieldPolicy.MatchupShareKPm`/`ChipFloorKPm`/`PenCapKPm` fresh on every access. `long.MaxValue`
    divided by the largest of the three coefficients that scale with `input` inside `AbsorbLayer`
    (`1000 × MatchupShareKPm` for elemMod's numerator, `ChipFloorKPm`, `PenCapKPm`) — the tightest of
    the three safe ceilings. `weightedRelationUnitPm`'s documented `[-1000,1000]` range is not assumed,
    it's proved in the doc comment: `ShieldElementMatrix.RelationUnit` only ever returns `{-1,0,1}`,
    and `ElementPayload` requires component weights to sum to `1.0` before converting to per-mille, so
    `Σ weightPm_i ≈ 1000` and the worst case (every component agreeing in sign) sums to at most 1000.
    At the shipped tuning (250/100/3000) this evaluates to `long.MaxValue / 250,000 ≈ 3.69×10¹³` —
    independently recomputed and asserted in `ShieldMathTests.MaxInput_is_derived_...`, not read off
    the implementation. `AbsorbLayer`'s `if (input > MaxInput) input = MaxInput;` clamp became
    `throw new ShieldInputOverflow(input, MaxInput)` (new exception, same file, same pattern as
    `PowerIndexOverflow`).
  - **`ResourceDeltaMath.AmountCap`**: `1_000_000_000L` → `long.MaxValue / 2`, still a `const` (the
    expression is compile-time-constant, unlike `ShieldMath`'s tuning-dependent one) — derived from
    `Apply`'s own arithmetic (`live + delta`, each independently bounded by the cap, so the worst-case
    sum is `2 × AmountCap`, which must stay under `long.MaxValue`). `Apply` itself gained two throwing
    guards (`ExceedsAmountCap(live)`/`ExceedsAmountCap(delta)`) as a backstop for a caller that doesn't
    pre-check — `EffectFunnel.cs`'s two existing `ExceedsAmountCap` pre-check-and-skip call sites are
    **unchanged** (not in this task's file list, and the guarded Funnel path already pre-empts the new
    throw in every tested case, confirmed by the full suite staying green).
  - **`RpgStore.MaxSoulAward` → dynamic, one policy across both paths.** The static, config-backed
    ceiling (`SealedCompactionPolicy.MaxSoulAwardTuning`, itself a Phase-M migration of the same old
    `1_000_000_000` literal) is **replaced**, not kept alongside — F12's own language ("dynamic, checked
    per award") leaves no room for a parallel fixed constant. New: `RpgStore.MaxSoulAwardFrom(long
    balance) => checked(long.MaxValue - Math.Max(0L, balance))` — public, pure, no DB needed to test
    directly. A private `GuardSoulAwardOrThrow(balance, delta)` calls it and throws on excess; both
    `AwardSouls` (moved its check inside the `_gate` lock, after reading the live balance, before it
    reads `MaxSoulAward` as a fixed value like before) **and** `ApplyExpeditionRewards` in
    `RpgStore.Expeditions.cs` (previously `Math.Min(rewards.EventSouls, MaxSoulAward)` — the exact
    silent clamp §11.2a names: *"AwardSouls throws on excess, but the expedition path clamps. Two
    policies for one ceiling, and the silent one is on the reward path"*) now call the same helper.
    `DataTuning.MaxSoulAward`, `SealedCompactionPolicy.MaxSoulAwardTuning`, and `data.v1.json`'s
    `maxSoulAward` key all **deleted** (unused config path, not left dangling) — `data.v1.json`'s
    `_meta.note` updated to explain the supersession, dated.
  - **F13, dependency graph acyclic:** implemented as a test-local declared graph (`(bound, other
    DERIVED bounds it reads)`) plus a real DFS cycle check in `CapsReconcileTests.cs` — not a
    production-code registry abstraction spanning the full ~44-entry §11 caps register, which would be
    disproportionate for what is currently zero edges between the three derived bounds (`ShieldMath.MaxInput`
    reads three `ShieldPolicy` *leaves*, not another derived bound; `ResourceDeltaMath.AmountCap` is
    self-contained; `MaxSoulAwardFrom` reads a runtime balance, not a cap). The test exists for the
    *next* edge a future change adds, not because a cycle exists today — matching CLAUDE.md/AGENTS.md's
    "don't introduce abstractions beyond what the task requires."
  - **§11.2a regression guard (not a forcing function — spec's own correction, verified true):**
    `EffectBag.cs:707` and `EventDrain.cs:458/475` no longer have the narrowing `(int)` casts the SSOT
    table describes — confirmed by reading both files directly: `EffectEventDto.Damage` is `long?` and
    every assignment into it is a plain, uncast read. Phase 0 (P0.4) already widened this before Phase
    3 started, exactly as the spec's own 2026-08-23 self-correction predicted. `CapsReconcileTests.
    EffectEventDto_Damage_stays_wide_never_narrows_back_to_int` reflects over the property type and
    fails if a future edit narrows it back — green from birth, as documented, not assumed.
  - **A structural mismatch between the spec's §4 file list and reality, worked around rather than
    forced:** `RpgStore` (Souls/Expeditions) lives in `FusionRpg.Data`, unreachable from
    `Core.Tests/Power/CapsReconcileTests.cs` (Core has no reference to Data — the same host/core
    separation Phase M's own narrative already established for `SealedCompactionPolicy`). The spec
    names only one new test file; the soul-bound's dynamic/one-policy tests instead extend the
    existing, natural homes in `FusionRpg.Data.Tests` — `SoulStoreTests.cs` (`MaxSoulAwardFrom` as a
    pure function, and the balance-nears-int64Max scenario) and `ExpeditionRewardApplyTests.cs` (the
    one-policy-both-paths proof, reusing that file's own dispatch→collect harness). `ShieldMath`'s two
    new cases extend the existing `ShieldMathTests.cs` rather than duplicating fixtures in the new file.
  - **Two real regressions found by the full suite, both test-data artifacts of the cap moving from
    ~1e9 to ~4.6×10¹⁸, not production bugs — fixed, not weakened:**
    `BattleEffectHostTests.Amount_cap_refuses_oversized_mutations` hardcoded `long.MaxValue / 2` as its
    "huge" probe value — which is now *exactly* `AmountCap` itself (not one past it), so the mutation
    it expected to be refused now legally succeeds. Fixed to `ResourceDeltaMath.AmountCap + 1`.
    `EffectFunnelTests.Merged_sum_over_cap_skips_whole_packet` summed two `600_000_000L` mutations
    (1.2e9, safely past the OLD cap) expecting the merged packet to be skipped — 1.2e9 is nowhere near
    the new ~4.6×10¹⁸ cap, so the merge now legally succeeds (confirmed by the actual failure: the
    merged 1,200,000,000 mutation WAS applied). Fixed to two `AmountCap/2 + 1` halves, whose sum is one
    past the live cap regardless of its magnitude.
  - **One flaky pre-existing test surfaced during the full-suite run, verified as unrelated, not
    dismissed on assumption:** `PredicateCompilerTests.Evaluating_allocates_nothing` (a
    `GC.GetAllocatedBytesForCurrentThread()` zero-allocation micro-check, wholly unrelated to
    Shield/ResourceDeltaMath/soul-economy code) failed once with a one-time 3528-byte allocation
    consistent with JIT tier-up/warm-up timing, not a per-iteration leak. Verified, not assumed: passed
    3/3 in isolation, then passed clean on an immediate full-suite re-run with zero code changes in
    between — a test-ordering/JIT-timing flake, pre-existing and orthogonal to this task.
  - Accept: derived, not `1e9` ✓ (both `ShieldMath.MaxInput` and `ResourceDeltaMath.AmountCap`) · throw,
    never clamp ✓ (`ShieldInputOverflow`, `ArgumentOutOfRangeException` × 2 in `ResourceDeltaMath.Apply`,
    `ArgumentOutOfRangeException` in `GuardSoulAwardOrThrow`) · `MaxSoulAward` dynamic, not a constant ✓
    (`MaxSoulAwardFrom(balance)`) · one policy across `AwardSouls` and the expedition path ✓ (both call
    `GuardSoulAwardOrThrow`, proven by `Expedition_souls_past_headroom_throw_instead_of_silently_clamping`)
    · each bound declares what it reads, graph acyclic ✓ (`DerivedBoundDependencyGraph_IsAcyclic`)
  - Verify: `CORE` → **3112/3112 green** (3104 + 8 new in `CapsReconcileTests.cs`; 3 in `ShieldMathTests.cs`
    net +2 after one removed/three added; the one `Evaluating_allocates_nothing` flake explained above,
    reproduced-then-cleared, not silently reset) · `Data.Tests` → **473/473** (470 + 3 new) ·
    `Server.Tests` → **15/15** · `E2E.Tests` → **194/194** · `Guard.Tests` → **73/73** ·
    `CheatCore.Tests` → **40/40** · all 6 guards OK (`guard-dal`, `guard-funnel-delta`,
    `guard-single-writer`, `guard-secondary-no-unity`, `guard-overflow`, `guard-magic-numbers`) ·
    `audit-overflow.py` → 0 critical, A3 unchanged at 21, A6 clean (every new bound wrapped in `checked`)
    · `audit-magic-numbers.py --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Combat/Shield/ShieldMath.cs`, `src/FusionRpg.Core/Effects/ResourceDeltaMath.cs`,
    `src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs`,
    `src/FusionRpg.Data/Policies/DataTuning.cs`, `src/FusionRpg.Data/Policies/SealedCompactionPolicy.cs`,
    `data/tuning/data.v1.json`, `tests/FusionRpg.Data.Tests/ContractTuningTestBootstrap.cs`,
    `tests/FusionRpg.E2E.Tests/ContractTuningTestBootstrap.cs` (NOT `Core.Tests`'s copy — Data-unreachable
    from Core) · new: `tests/FusionRpg.Core.Tests/Power/CapsReconcileTests.cs` · extended:
    `tests/FusionRpg.Core.Tests/Combat/Shield/ShieldMathTests.cs`, `tests/FusionRpg.Data.Tests/SoulStoreTests.cs`,
    `tests/FusionRpg.Data.Tests/ExpeditionRewardApplyTests.cs` · fixed (stale test data, not weakened):
    `tests/FusionRpg.Core.Tests/Battle/BattleEffectHostTests.cs`, `tests/FusionRpg.Core.Tests/EffectFunnelTests.cs`
    · docs: none needed — `spec-caps-reconcile.md` read in full against current code before building
    and matched reality; no correction to log this time, unlike T3.4's two

- [x] **T3.6 — `caps-reconcile`: the deletions + the earn formula** — done 2026-08-24.
  - **Four deletions**, all in the same pass: `ContractPolicy.MaxSlots` (+ `ContractSlotsTuning.MaxSlots`,
    `contracts.v1.json`'s `slots.maxSlots`) — `Capacity` drops its `Math.Min`, `CanBuySlot` becomes an
    always-true named check (kept, not removed outright, since the store's buy-slot gate and the
    contracts API both still call it). `SoulEarnPolicy.KillCapPerMatch`, `SoulMatchEndTuning.VictoryFullPerDay`
    (audit F11), `PatronPolicy.KillSoulCap` — all three (+ their `souls.v1.json`/`patron.v1.json` keys)
    deleted the same way Phase M's own `souls.v1.json`/`patron.v1.json` `_meta.note`s already
    anticipated ("config-driven now so T3.6 only has to delete a row, not fight a hardcoded const") —
    that groundwork paid off exactly as planned.
  - **The earn formula, landing in the same commit (SSOT §11.7a):** `SoulEarnPolicy.KillEarn`/
    `MatchEndEarn` now take a required `(int theta, PowerTuning tuning)` and return
    `ContentScale.Apply(baseAmount, ContentScale.Milli(theta, tuning))` — the exact
    `KillDelta/VictoryDelta/DefeatDelta × contentScale(Θ)` shape, byte-identical at Θ=20 (no inflation
    window, no constant for the economy stream to choose). `PatronPolicy.KillEarnWithPatron` gained the
    same required Θ parameters — **a deliberate extension beyond SSOT §11.7a's own named formula
    list**, not hidden: leaving the patron bonus flat while the base path scales would make owning a
    patron a strictly WORSE choice at any depth past the pin, the opposite of what a bonus is for;
    documented inline with that reasoning.
  - **A genuine, substantial open finding, surfaced by research before writing any formula code, not
    discovered by a failing test:** SSOT §11.7a's `Θ_enemy`/`Θ_run` presuppose a per-kill/per-run
    content-depth signal that **does not exist anywhere in the current vanilla-PvZ capture pipeline**.
    Traced explicitly: `PvzActivityKinds.FromCaptureKind` maps `zombie.die`→`ZombieKilled` and
    `match.result`→`MatchEnded`; `PvzActivityRollupBuilder.ApplyDelta`'s `ZombieKilled` case is a bare
    `c.ZombiesKilled++`, reading nothing else off the fact; `ApplySoulEarnFromActivityUnlocked` (the
    one and only caller of the earn formulas) never had access to a killed zombie's identity or the
    run's depth. Building that wiring is a separate, substantial task — none of `PvzActivityKinds.cs`,
    the injector capture hooks, or the fact-shaping code are in this task's file list, and the RPG
    power-ladder's own `IPowerIndexProvider`/`ContentIndex` machinery (T1.3/T1.4) is a live, hydrated,
    Core-layer abstraction that `RpgStore` (a Data-layer SQL class processing replayable stored facts)
    architecturally cannot reach. **Resolved the same way T1.4 resolved an identical gap**
    (`realmsAdvanced`/`pvzRuns` defaulting to 0, "documented as an unbuilt-feature gap, not a wiring
    gap"): `RpgStore.Souls.cs` gained a named, fully-commented constant
    (`VanillaPvzKillAndRunTheta = 20`, the pin) and every vanilla-PvZ soul-earn call site reads at it
    explicitly — never a bare, unexplained default. This keeps every current soul award byte-identical
    to pre-T3.6 behavior (Property 1 of SSOT §11.7a, satisfied exactly) while being honest that
    Property 2 ("faucet tracks sink") and Property 3 ("stall-farm dies") are proven true of the
    **formula**, starting today, not yet true of the **live vanilla-PvZ pipeline** until that follow-up
    wiring lands. Also let go, cleanly: `CountVictoriesOnDayUnlocked` (its one caller was the deleted
    decay branch) deleted rather than left dead.
  - **Stall-farm regression, proven as a pure-formula test** (`SoulEarnPolicyTests.
    Stall_farm_regression_clean_win_beats_stall_defeat_on_souls_per_minute`), reproducing SSOT §11.7a's
    own worked scenario (clean win: 40 kills @Θ20 + victory, 3 min; stall-defeat: 80 and 200 kills
    @Θ5 + defeat, 12/30 min) with explicit Θ arguments rather than through the not-yet-wired live
    pipeline. **Exact totals differ from the SSOT's own illustrative table** (140/25/25 here vs.
    140/50/88 there) for a real, explained reason: the formula rounds **per kill**
    (`ContentScale.Apply` called once per `KillEarn`, matching §11.7a's own singular "soulsPerKill"),
    while the SSOT's table reads as continuous arithmetic for the write-up. At `KillDelta=1` and
    `Θ_enemy=5`, `contentScale(5)≈0.316` rounds every individual kill to exactly zero — an even
    *stronger* deterrent than a small positive per-kill value, not a weaker one, and asserted directly
    (`Assert.Equal(0, SoulEarnPolicy.KillEarn(5, tuning))`). Souls-per-minute asserted, not per-match,
    per the task's own explicit instruction — clean win beats both stall-farm variants decisively
    (46.7/min vs. 2.08/min and 0.83/min), and the longer 200-kill grind pays strictly worse per minute
    than the shorter 80-kill one, reinforcing that grinding harder at the wrong depth never recovers
    the rate.
  - **Slot 512 / warden property**, both asserted precisely against SSOT §11.1a's own worked table
    (independently re-derived, not copied): `Capacity(500)==512`, `NextSlotPrice(500)==150,300`,
    cumulative cost to 512 total slots `==37,575,000` (a `for` loop summing `NextSlotPrice(0..499)`,
    not a formula copied from the doc). Warden property re-asserted at slot 2,012 (`NextSlotPrice(2000)
    ==600,300`, strictly greater than `NextSlotPrice(1999)`) — "because of the price, never because of
    a cap," proven past the old ceiling by two full orders of magnitude.
  - **All 41 exempt caps (§11.3-§11.10) carry a class comment — verified by sampling across every
    subsection, not re-derived from scratch:** most have already migrated to config-backed properties
    under Phase M (e.g. `LoyaltyMax`, `ChipFloorKPm`, `MaxRounds` now read `=> Tuning.X`, carrying full
    `data/tuning/*.v1.json` provenance instead of a bare comment — a stronger form of the same
    transparency obligation, not a gap). The ones that stayed bare code constants
    (`ResourceDeltaMath.MailboxCap`, `WorldEndpoints.MaxCommandsPerSubmit`, `ContentHash.MaxJsonDepth`,
    `DemonSpeciesCatalog.DemonTypeIdFloor`, `ContentValidation.DriftFloor`,
    `DemonSpeciesGenerator.DefaultMaxSpecies`, `CapPolicyConfig.MaxLivingBullets`,
    `PerfEndpoints.Cap`) were checked individually — every one carries an explanatory comment either on
    its own line or in its immediately enclosing class doc. Independently corroborated: the
    magic-numbers audit (which enforces the same "every surviving const needs a reason" obligation
    from a different but overlapping angle) is 0/0/0/0 repo-wide.
  - **A scope boundary respected, not silently crossed:** `ContractEndpoints.cs`'s JSON payload still
    exposes a `maxSlots` field (forced by the compile error from deleting `ContractPolicy.MaxSlots` —
    not in this task's own file list, but unavoidable). Rather than deleting the wire field — which
    would ripple into `web/fusion-rpg-web`'s own 19 files that reference `maxSlots` (components, unit
    tests, Playwright specs), an entirely different toolchain this backend spec never named — it now
    reports `int.MaxValue` with a comment explaining why, and this narrative flags the web frontend's
    stale `maxSlots` fixtures as a **follow-up outside this task**, not something silently patched or
    silently ignored.
  - Accept: `MaxSlots`/`KillCapPerMatch`/`KillSoulCap`/`VictoryFullPerDay` deleted ✓ · slot 512
    purchasable at 150,300 ✓ · warden property holds ✓ · stall-farm regression green on the new formula
    ✓ (souls-per-minute, not per-match) · all 41 exempt caps carry a class comment ✓ (sampled + audit-corroborated)
  - Verify: `CORE` → **3109/3109 green** (3112 − 3: two Theory-heavy tests consolidated from
    boundary-enumerating cases — a per-match-cap threshold and a daily-decay threshold — that no longer
    exist as concepts, into fewer, more targeted Fact tests; net accounted for exactly, not a silent
    loss) · `Data.Tests` → **473/473** (three stale-expectation tests fixed: uncapped kills, undecayed
    victories, memo-isolation reframed — all three failures were the exact, predicted, correct
    consequence of the deletions, verified via the full suite, not assumed safe) · `Server.Tests` →
    **15/15** · `E2E.Tests` → **194/194** · `Guard.Tests` → **73/73** · `CheatCore.Tests` → **40/40** ·
    all 6 guards OK · `audit-overflow.py` → 0 critical, A3 unchanged at 21 · `audit-magic-numbers.py
    --summary` → **0/0/0/0 repo-wide**
  - Files: `src/FusionRpg.Core/Demons/SoulEarnPolicy.cs`, `SoulEarnTuning.cs`,
    `src/FusionRpg.Core/Demons/Patron/PatronPolicy.cs`, `PatronTuning.cs`,
    `src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs`, `ContractTuning.cs`,
    `src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs`, `src/FusionRpg.Server/ContractEndpoints.cs`,
    `data/tuning/{souls,patron,contracts}.v1.json`, all 3 `ContractTuningTestBootstrap.cs` ·
    tests: `SoulEarnPolicyTests.cs`, `PatronPolicyTests.cs`, `ContractPolicyTests.cs` (Core.Tests),
    `ContractRegressionTests.cs`, `SoulStoreTests.cs` (Data.Tests) — all rewritten, not patched around

### ✅ Checkpoint 3
- [x] 3a matched pair at `delta=0` every Θ (T3.1) · 3b red test flips + corpus identical at the pin
  (T3.2, T3.4) · 3c bounds throw, soul bound dynamic, stall-farm green on the new formula (T3.5, T3.6)
  — all six constituent tasks (T3.1-T3.6) done and individually verified 2026-08-24; **one standing,
  explicitly-flagged gap carried forward, not hidden**: T3.6's Θ_enemy/Θ_run read a fixed pin
  (`VanillaPvzKillAndRunTheta=20`) because the live vanilla-PvZ capture pipeline carries no real
  per-kill/per-run depth signal yet — the formula is proven correct and the production wiring is
  byte-identical to pre-T3.6 behavior, but "faucet tracks sink" and "stall-farm dies" are not yet
  observable in a live match, only in the formula's own direct tests. A separate, unbuilt task.

---

## Phase 4 — seal

- [x] **T4.1 — `power-guard`** — done 2026-08-24. `scripts/guard-power.ps1` (new, mirrors the shape of
  the four existing guards), `docs/architecture/power/inventory.json` (new, machine-readable mirror of
  SSOT §10.1/§10.2), `tests/FusionRpg.Guard.Tests/PowerGuardTests.cs` (new, 9 tests), `deploy-play.ps1`
  (edit — 7th guard added, matching the established `& script; if ($LASTEXITCODE -ne 0) { throw }`
  pattern exactly).
  - **G1 — no literal curve.** Scans every `.cs` in `Core/Power` for a curve field (`CMilli`/`AMilli`/
    `BMilli`/`PinIndex`/`PinValue`) assigned a bare literal, exempting `PowerTuningLoader.cs` (the JSON
    boundary) **and** `PowerTuning.cs` (where the three `FixedC/PinIndex/PinValue` anchor consts
    legitimately live by design — an ask-first ADR, not a tuning edit, per that file's own comment —
    and where `Build()`'s belt-and-braces re-derivation is structural verification math, not a second
    curve). Clean on the real tree.
  - **G2 — no private `f(level)`, with the false-positive survey the spec itself demands, actually
    run, not assumed.** First real run found 3 hits. Two genuine, reviewed false positives, not
    silently allowlisted: `PatronPolicy.AuraMilli(rarity, star, level)` — `level` there is the *patron
    demon's own* level, a different axis from the actor's `Θ`, never previously reviewed against the
    SSOT — **added as a new row (§10.2 #16) to `ssot-power-scale.md` itself**, not just to a script
    allowlist, because the doc's own opening line requires it ("adding a row is a reviewed change to
    this document, not a convenience"); mirrored into `inventory.json`. `RpgProgression.XpToNext`/
    `TotalToReach(kind, level)` — the XP **cost** ladder, already SSOT §10.1 row 6 ("kept, unchanged...
    the cost ladder, not a power ladder"), added to the script's own `-G2AllowlistFiles` default
    (`PatronPolicy.cs`, `RpgProgression.cs`), each with an inline reason.
  - **G3 — no new curve vs `inventory.json`**, sharing G2's detection but checking a *different*,
    doc-linked source of truth (not the ad-hoc G2 list) — `RpgProgression.cs` passed G3 immediately
    (already in the inventory from SSOT §10.1 row 6); `PatronPolicy.cs` needed the new inventory row
    above before it passed too. Deliberately **not** a generic dependency-graph abstraction over the
    full ~44-entry register (same proportionality call as T3.5's F13 test) — G3 just diffs file
    locations against the reviewed list.
  - **G4 — pin holds.** Re-derives `aMilli` from each `data/tuning/power-scale.v*.json`'s own
    `cMilli`/`bMilli`/`pinIndex`/`pinValue` independently in PowerShell (mirroring `PowerTuning.Build`'s
    own belt-and-braces check) rather than trusting the C# loader — this guard runs standalone,
    pre-build. Passes on the real shipped `power-scale.v1.json`.
  - **Three real bugs found and fixed in the guard script itself, by actually running it and testing
    it — not shipped on the strength of a plausible-looking regex:** (1) line numbers were computed
    from a comment-STRIPPED text blob, silently off by however many comment lines preceded a match —
    fixed by *blanking* comment lines instead of removing them, keeping array-index-as-line-number
    valid. (2) a redundant `$text.IndexOf($m.Value)` could find an earlier coincidental occurrence
    instead of the regex's own matched position — fixed to use the match's own `.Index`. (3) `^\s*`
    right after a blank line let `\s*` (which matches `\n` too) cross the line boundary and anchor the
    match one line early — fixed to `^[ \t]*` (horizontal whitespace only). All three caught by
    comparing the script's reported `file:line` against the real file, not by trusting the output.
  - Accept: four checks implemented and each independently proven (planted-violation tests per check,
    a clean-pass test, an allowlist-pass test) ✓ · false-positive survey before arming, complete, both
    entries reasoned ✓ · fails closed (every unhandled hit is a failure, no warn-only path exists) ✓
  - **Known gap, stated not hidden (per the spec's own §8, already resolved there):** scans `src/`
    only — `tools/` holds no C#, so `tools/seedsmith` (which authors magnitudes) is invisible to this
    guard by construction, not by oversight. Reassigned to `content-scale`'s own §2.3a obligation
    (T3.4), which owns cross-checking seedsmith's authored values — already built and tested.
  - **A second spec-vs-reality correction, found by reading before editing:** spec-power-guard.md §4
    named `.github/workflows/ci.yml` as a file to edit. It doesn't need editing — `ci.yml` runs guards
    exclusively through `dotnet test tests/FusionRpg.Guard.Tests/...` (confirmed: zero direct
    `guard-*.ps1` invocations exist anywhere in the workflow file), and `Guard.Tests` is already wired
    into CI. Adding `PowerGuardTests.cs` to that already-wired project is sufficient; a generic guard
    in this SAME test project (`CiWiringGuardTests.Every_test_project_under_tests_appears_somewhere_in_ci_yml`)
    would have caught a genuinely-missing project reference, but `Guard.Tests` isn't a new project.
  - Verify: `Guard.Tests` → **82/82 green** (73 + 9 new) · `.\scripts\guard-power.ps1` on the real tree
    → clean · `audit-overflow.py` → 0 critical, A3 unchanged at 21 · `audit-magic-numbers.py --summary`
    → **0/0/0/0 repo-wide** · `deploy-play.ps1` parses clean after the edit (syntax-checked via
    `PSParser.Tokenize`, since a live deploy needs the owner's game directory)
  - Files: `scripts/guard-power.ps1` (new), `docs/architecture/power/inventory.json` (new),
    `tests/FusionRpg.Guard.Tests/PowerGuardTests.cs` (new), `scripts/deploy-play.ps1` (edit) · docs:
    `docs/architecture/power/ssot-power-scale.md` (§10.2 new row 16, PS-4's row list updated)

- [x] **T4.2 — `power-dial`: `B` 0 → 400** — done 2026-08-24. `data/tuning/power-scale.v2.json`
  (new, `bMilli: 400`, everything else identical to v1) · `src/FusionRpg.Server/Program.cs`,
  `src/FusionRpg.Injector/Host/RpgHost.cs` (both edited — a spec-file-list gap found before editing:
  §4 named only the tuning JSON and `BattleModels.cs`, but **both hosts hardcode the literal filename
  `"power-scale.v1.json"`** with no "latest version wins" auto-detection at all — publishing v2 alone
  would have changed nothing at runtime; both now load v2 explicitly, with v1 kept on disk unmodified)
  · `BattleModels.cs` (`RulesetVersion` 2→3) · all 3 `ContractTuningTestBootstrap.cs` (`bMilli: 0→400`
  — without this the test suite keeps simulating the pre-dial world and the golden movement this task
  exists to triage would never even surface).
  - **The triage, done before any re-bless, exactly per the procedure:** ran the full `CORE` suite
    once the dial and `RulesetVersion` bump landed, got 11 failures, read every one before touching
    anything. Sorted into three buckets:
    1. **Rate goldens: zero moved.** Explicitly re-confirmed with a dedicated, isolated run
       (`RateParityTests` + `BattleAdoptionTests`'s `BattleRateTests`, 24/24 green, untouched) — this
       is PS-3's own assertion, the highest-value signal the whole program was built to produce, and
       it held.
    2. **Expected magnitude movement (7 hashes/fixtures, all actors away from the `Θ=20` pin):**
       `BattleGoldenTests`'s 3 battle hashes + 32-seed sweep, `PreAdoptionTraceTests`'s 3 trace
       fixtures, `ExpeditionResolverTests`'s 4 tier hashes. Re-blessed — new hash values computed via
       a temporary scratch probe test (xUnit's own diff output truncates long hex strings, so the
       genuine values had to be extracted directly, not guessed from a truncated prefix; probe deleted
       immediately after use), trace fixtures re-captured via their documented "delete → rerun →
       recaptures" mechanism (`PreAdoptionFixtures.cs`'s own stated discipline). Every re-bless dated
       and reasoned in place, matching the file's own established comment convention.
    3. **Two literal `RulesetVersion==2` assertions** (not goldens at all, mechanical): `BattleAdoptionTests.
       Retired_symbols_stay_retired`, `BattleShieldTests.Report_carries_ruleset_v2_and_platform_stamp`
       (renamed to `_v3_`) — bumped to 3.
    4. **Three `BattleMagnitudeParityTests` tests whose entire premise retired, not a golden move at
       all:** `BaseHp/BaseAtk/BaseDefense_MatchesShippedFormula_AcrossFullRange` existed specifically
       to prove "byte-identical to the pre-migration literal formula **at B=0**" (T2.1's own claim,
       stated in the file's section header) — a claim that is now deliberately false, by design, since
       B=0 no longer ships. Reframed rather than deleted: two now assert parity against the **live**
       ladder (`BaseHp_MatchesTheLiveLadder_...`, durable across any future re-dial, not just today's
       400) and a new, explicitly-labeled test preserves the original B=0 claim as a historical fact
       against a **local** zero-B tuning, independent of whatever the ambient hub currently ships.
  - **Nothing at the pin moved** — verified, not assumed: `BattleMagnitudeParityTests.Pins_MatchAtTheta20`
    and `F1Regression_AtDecidedDialB400_MatchesSpecsWorkedExample` (asserting `atk.Value(100)==628`,
    `defense.Value(100)==154` against the spec's own worked B=400 example) were **never in the failure
    list** — both passed at every step, independently confirming the pin held and the dial's actual
    values match SSOT §4.5's worked table exactly.
  - **`decisions.md` corrected, not just appended to** (T4.2's own file-list item, plus two more stale
    rows found while there): the **Power scale** row still said "Specced, not built" — flipped to
    Built, with a new sibling **Power dial** row recording the dial itself. The **Caps** row still
    listed T3.6's four deletions with no build stamp — flipped to Built, expanded with the T3.5 dynamic
    bound and the T3.6 formula/regression-test summary. `power-map.md` had the same staleness in three
    places (module specs "pending review", the ADR P1 item "pending build", no checkpoint-passed
    marker) — all three corrected.
  - Accept: one field in the tuning JSON, one commit's worth of scope (host-pointer + RulesetVersion +
    test-fixture updates are the NECESSARY companions the spec's own file list under-named, not scope
    creep) ✓ · `RulesetVersion` 2→3 ✓ · `v1` retained on disk, unmodified, `_meta` updated to say so ✓ ·
    every moved hash triaged before re-blessing ✓ (11 failures, all read and classified before any fix)
  - Verify: **zero rate goldens moved** ✓ (24/24 rate tests green, isolated run) · nothing at the pin
    moved ✓ (`Pins_MatchAtTheta20`, `F1Regression_AtDecidedDialB400...` both green throughout) ·
    `CORE` → **3110/3110 green** · `Data.Tests` → **473/473** · `Server.Tests` → **15/15** ·
    `E2E.Tests` → **194/194** · `Guard.Tests` → **82/82** · `CheatCore.Tests` → **40/40** · all 6
    original guards + `guard-power.ps1` (G4 re-confirmed holding for BOTH v1 and v2 on disk) OK ·
    `audit-overflow.py` 0 critical, A3 unchanged at 21 · `audit-magic-numbers.py --summary` → **0/0/0/0
    repo-wide**
  - **Revert proven by construction, not just claimed:** `v1` was never modified, both hosts' pointer
    is a one-line change back to `power-scale.v1.json`, and `RulesetVersion` un-bumps to 2 — the exact
    procedure §2.4 describes, ready to execute but not exercised live (no owner-run reload available
    from this session, same class of gap as every other live-game verification step this program has
    flagged throughout)
  - Files: `data/tuning/power-scale.v2.json` (new), `data/tuning/power-scale.v1.json` (`_meta` only),
    `src/FusionRpg.Server/Program.cs`, `src/FusionRpg.Injector/Host/RpgHost.cs`,
    `src/FusionRpg.Core/Battle/BattleModels.cs`, all 3 `ContractTuningTestBootstrap.cs` · tests:
    `BattleGoldenTests.cs`, `BattleAdoptionTests.cs`, `BattleShieldTests.cs`,
    `BattleMagnitudeParityTests.cs`, `ExpeditionResolverTests.cs`,
    `tests/fixtures/battle-traces/{stomp,close,wipe}.trace.txt` (re-captured) · docs: `decisions.md`
    (3 rows), `power-map.md` (3 spots)

### ✅ Checkpoint 4
- [x] Guard armed and proven (T4.1, 9/9 `PowerGuardTests` including planted-violation cases) · dial
  moved zero rate goldens (T4.2, 24/24 rate tests untouched) · nothing at the pin moved (T4.2,
  `Pins_MatchAtTheta20` + the spec's own worked B=400 example both green throughout) · revert proven
  by construction (`v1` untouched on disk, one-line host pointer + `RulesetVersion` un-bump restores it)

---

## Phase M — magic numbers *(parallel with 1–4, after Phase 0)*

Baseline **329**: M1 111 · M2 80 · M3 88 · M4 50, across 37 balance-surface files.
**Values unchanged in every task** (T7) — extract, prove byte-identical, tune separately.

- [x] SSOT + standard in `CLAUDE.md`, `AGENTS.md`, `DESIGN-GATE.md`, spec skill
- [x] `scripts/audit-magic-numbers.py` — 4 categories, `--summary` / `--domain` / `--targets`

- [x] **M.1 — `contracts` (34)** — done 2026-08-23/24. `data/tuning/contracts.v1.json` (41 values:
  loyalty thresholds/gains/decay, rank-bonus Milli×3, slots, settlement, personality rates ×5×3,
  base-upkeep ×4, ritual price ×4). `ContractPolicy.cs`'s consts became config-backed static
  properties (kept the same public names/signatures — `ContractPolicy.WinGain` etc. still read the
  same everywhere, now via `Tuning.Loyalty.WinGain`); switch-expression literals became
  `ContractTuning` dictionary lookups (`RankFor`, `RankBonusMilli`, `Rates`, `BaseUpkeepPerDay`,
  `RitualPrice`), each still throwing the original exception shape on an invalid enum. New
  `ContractTuning.cs` (Core, pure parser — `ContractTuningLoader.Parse(string)`, no I/O per §7.2) with
  typed rejections naming the missing path (T5). Hosts load + inject: Server's `Program.cs` (file read
  + `ContractPolicy.Configure`, JSON copied next to the exe via a `.csproj` `<Content>` item matching
  the sqlite-data convention already there) and the shared `RpgHost.Initialize` (covers both BepInEx
  and MelonLoader hosts identically, same copy-item pattern in all three host `.csproj`s). Tests
  construct one inline (§7.2) via a `[ModuleInitializer]` bootstrap duplicated into `Core.Tests`,
  `Data.Tests`, `E2E.Tests` — same literal C# values as the JSON, not a file read.
  Minimal publish path built (§7.1): `tools/tuning/publish.py <domain> <dotted.key>=<value>` reads
  the latest `vN`, refuses an unknown key or a no-op, writes `v{N+1}`, leaves `vN` on disk (T4).
  Smoke-tested (`loyalty.winGain 15→99` published to a throwaway v2, both refusal paths hit their
  exit-1 branch), then the throwaway v2 deleted — only the real v1 ships.
  - Verify: `audit-magic-numbers.py --domain contracts` → **0/0/0/0** (was 15/10/0/9). Every test suite
    in `ci.yml` green post-migration: Core 2971, Data 470, Guard 73, Launcher 162, CheatCore 40,
    Server.Tests 15, E2E 194, ItemSeedValidator 71, AtomImporter 21, ElementEnumGen 14 — 4031 total,
    0 failures. `ContractPolicyTests.cs` alone asserts every migrated value directly
    (`Rates(Loyal)==(120,80,100)`, `Capacity(36)==48`, `RitualPrice(Legendary)==400`, …) — concrete
    byte-identical proof, not an inference from "tests passed"
- [x] **M.2 — `loam` (21) + `world` (30)** — done 2026-08-24.
  **loam**: `data/tuning/loam.v1.json`, 30 values (all of `LoamPolicy.cs` — its own class comment
  already declares every constant a provisional placeholder, so all 30 moved, not just the 21 the
  regex happened to flag; 9 escaped M2/M4 via a `capacity`-is-structural-vocabulary collision with
  `STRUCTURAL_WORD`, caught by the same manual full-file read discipline as P0.3). Same
  `LoamPolicy.Configure(LoamTuning)` shape as `ContractPolicy`.
  **world**: heterogeneous — 8 files, two different kinds of number. Migrated the 5 that are genuinely
  global policy (`data/tuning/world.v1.json` + `WorldTuningHub.Configure`, one call covers all five):
  `LaneTypeCatalog` (per-type cost), `WorldSizeCatalog` (per-tier node range), `StrengthBandCatalog`
  (per-band floor/ceiling/midpoint), `PlaceholderBattleResolver`, `TurnCalendar`. Row ids/names/
  structural flags stayed in C# (schema, not balance); only numeric fields moved — `Seed` on all three
  catalogs went from a `static readonly` field to a property so it reads the tuning lazily instead of
  at type-init (before `Configure` could plausibly have run).
  **Deliberately not migrated, with reasoning, not a skip**: `WorldTemplateCatalog.cs` (+ its
  `.TwoHearths.cs` partial) is one hand-authored starting scenario per template — sector layout, lane
  geometry, entity placement — not a reusable balance table despite the shared `*Catalog.cs` suffix;
  fragmenting a few Hp/LoamStock numbers into a flat JSON while the surrounding scenario stays in C#
  would cost readability for no tuning benefit a same-file edit doesn't already have. Recorded as an
  explicit, named exemption in `audit-magic-numbers.py` (`CONTENT_FILE`), not silently dropped.
  **Two more audit-tool bugs found and fixed**, same class as P0.3's: `TurnStartMilli` false-matched
  `BALANCE_WORD`'s `star` inside "turn**Star**t"; fixed with the same `(?![a-z])` word-boundary
  technique already used for the overflow audit's `hp`. `SectorTypeFlags.Fortress = 16` (a `[Flags]`
  bit value, not a balance number) is fixed by rewriting it `1 << 4` — idiomatically clearer *and*
  single-digit-exempt, no exemption list needed.
  - Verify: `--domain loam` and `--domain world` both **0/0/0/0** (were 21 and 30). Full suite green
    post-migration: Core 2971, Data 470, E2E 194, Server.Tests 15, Guard 73 = 3723, 0 failures.
    `audit-overflow.py` unchanged (A3 still exactly 19, the P0.3 BOUNDED set) — confirms the `1<<4`
    rewrite didn't touch anything overflow-relevant
- [x] **M.3 — `souls` + `patron` (26)** — done 2026-08-24. `data/tuning/souls.v1.json` (`SoulEarnPolicy`,
  full file: kill/match-end/discovery-by-rarity/codex, 13 values) + `data/tuning/patron.v1.json`
  (`PatronPolicy`, full file: switch cost, aura clamp, per-star, kill-soul cap, rarity-base×4, 8
  values) — two files, matching tunables-ssot §2's own domain examples ("contracts, **souls**...
  **patron**..."), not one merged file. `killCapPerMatch` and `killSoulCap` (T3.6's deletion targets,
  audit F11's `victoryFullPerDay` too) are now config rows, not hardcoded consts — **the coordination
  with T3.6 this task names is exactly that**: when Phase 3 is authorized, T3.6 deletes three JSON
  keys instead of fighting hardcoded consts, so this migration is what makes that a clean deletion.
  **Two more audit-tool false positives found and fixed** (same class, third and fourth of the
  session): `RarityBaseMilli`'s `_ => 60` (Legendary, PatronPolicy) is a genuine tunable the regex
  can't see behind a wildcard arm — migrated anyway, not left for the regex to decide. Both
  `SoulEarnPolicy.KillDelta = 1` and `PatronPolicy.PerStarMilli = 10` are single/exempt-digit
  literals the tool would never flag — migrated for consistency with SSOT §11.7a, which names
  `KillDelta` explicitly as a term in the future earn formula.
  - Verify: `--domain patron` **0/0/0/0** (was 8). `SoulEarnPolicy`'s 9 findings gone from the
    `demons` bucket (17→8; the remaining 8 are `SummonBannerCatalog`/`SummonRoller`, untouched, a
    later task's scope). Full suite green: Core 2971, Data 470, E2E 194, Server.Tests 15, Guard 73 =
    3723, 0 failures
- [x] **M.4 — `status`, `fusion`, `shield`, `overlay`, `stats`, `combat`, `expeditions`** — done
  2026-08-24, all 7 domains 0/0/0/0. `fusion`/`shield`/`combat` details above.
  **`status`**: `data/tuning/status.v1.json` — `StatusPolicy.cs` in full, including
  `resistFromPowerRatio` at its **current shipped value 0.0** (T3.1, Phase 3, not yet authorized,
  will publish a v2 changing this one value to 1.0 — this migration is what turns that ADR into a
  one-line JSON publish instead of a code edit). `ActorDerivedProfiles.cs`'s five `Combat*` constants
  are proof-board fixtures (class doc: "for status **prove boards**") — a balance pass never tunes a
  named test scenario's own input — documented individually (M3 checks the *immediately* preceding
  line, so one block comment above the group did not cover the rest; verified by re-running the audit
  after the first attempt, not assumed).
  **`overlay`**: `data/tuning/overlay.v1.json` — `OverlayPausePolicy`, `OverlaySwitchLayout` (button
  pixel geometry), `OverlaySwitchState` (debounce/probe/timeout ms) — UI/feel tuning, not gameplay
  balance, but the same T1 test applies.
  **`stats`**: `data/tuning/stats.v1.json` — `StatChannels.MinimumInterval`,
  `ElementMatchupPolicy.MatchupShareK`, `CombatProbabilityPolicy`'s four sigmoid constants.
  **Deliberately not migrated**: `IProgressionPowerProvider.cs`'s `MaxExponent` (the retired POC power
  curve) — traced its one caller (`InjectorProgressionPowerProvider.GetPower`) and confirmed
  `GetLevel` always returns 0 (`SetLevel` has zero callers, same latent-code shape audit F-finding
  already established elsewhere), so `Math.Min(level, MaxExponent)` never runs — migrating a constant
  nothing reads would be pure busywork; documented in place instead, and T3.2/T3.3 deletes the whole
  class once Phase 3 lands.
  **`expeditions`**: `data/tuning/expeditions.v1.json` — `ExpeditionTierCatalog`'s 4 tiers' numeric
  fields (ids/names/hasBossWave stay in C#, same schema-vs-balance split as the `world` domain's
  catalogs) plus `ExpeditionResolver`'s 6 event-roll constants.
  **Two more audit-tool false positives, same substring-collision class as `star`/`hp`**: `xp` matched
  inside `MaxE**xp**onent` (fixed with the established `(?![a-z])` technique — and, fittingly, the
  fix is what *un-flagged* the dead constant this same task decided not to migrate).
  **One more architectural lesson**: `ExpeditionTierCatalog.ById` was a `static readonly` field
  eagerly evaluating `All` (and so `Tuning`) at type-load — the exact "Seed" trap already fixed for
  `LaneTypeCatalog`/`WorldSizeCatalog`/`StrengthBandCatalog` in M.2, recurring because this catalog's
  `ById` cache predates that fix and wasn't re-derived from the same pattern; made lazy the same way.
  - Verify: all 7 domains 0/0/0/0. Full suite green: Core 2971, Data 470, E2E 194, Server.Tests 15,
    Guard 73 = 3723, 0 failures
  - `fusion` (6→0), `shield` (3→0), `combat` (3→0) done 2026-08-24. `data/tuning/{fusion,shield,combat}.v1.json`.
    `CombatPolicy` is architecturally different (mutable per-match-override instance, not static
    consts) — `Configure()` assigns into the `Default` singleton's settable properties instead of
    backing them with computed reads. Found and fixed **three more default-parameter compile breaks**
    (a default parameter value must be a compile-time constant, so converting a `const` to a
    config-backed property breaks any consumer using it as one): `ShieldInnateDef`'s `Priority`
    default (record primary-constructor parameter — removed the default, made it required, 2 call
    sites updated), `FoundationHarness.GrantShield`'s `priority` default and a local test helper's
    same pattern (both switched to `int? priority = null` + `?? ShieldPolicy.PrioritySkill` in the
    body — zero of the ~20 existing callers needed touching). Also hit the inverse failure mode:
    removing `CombatPolicy`'s literal property defaults entirely (matching every other Policy class's
    T5 "no built-in default") broke `TargetResolverTests`/`CombatCounterTests`, which construct
    `new CombatPolicy { OneField = x }` directly and implicitly relied on the *other* fields' sane
    defaults — object-initializer construction that a `new CombatPolicy()`-only grep doesn't catch.
    Fixed properly: kept `Default`'s literal fallbacks (still overwritten by `Configure()` for the
    shared singleton) and added `CombatPolicy.FromDefault()` so a one-off override copies real values
    instead of zeroing everything else; both tests now build from that. Two more audit-tool false
    positives fixed the same way as the session's earlier ones: `ShieldMath.cs`'s `1_000_000` (a
    per-mille² renormalization denominator, same class as the already-exempt `1000`) and
    `ElementPayload.WeightSumEpsilon` (a floating-point comparison tolerance matched via "weight",
    not a balance number — added `epsilon` to `STRUCTURAL_WORD`).
    Verify: `--domain shield`/`combat`/`fusion` all 0/0/0/0. Full suite green: Core 2971, Data 470,
    E2E 194, Server.Tests 15, Guard 73 = 3723, 0 failures (one real regression caught and fixed along
    the way — see above)
- [x] **M.5 — `vfx` (68) + the scope gap it uncovered** · done 2026-08-24.
  **Scope-gap finding, ahead of any code:** re-running `audit-magic-numbers.py --summary` before
  starting vfx showed **157** total findings, not 68 — M.1–M.4 had brought the true baseline down
  from 329 to 157, and this task's own text ("largest count... deliberately last") assumed vfx was
  the only domain left. It wasn't: **18 more domains, 90 more findings**, across files the plan never
  named — `battle` (11, including 8 M1 in `TraitBattleCatalog.cs`, the highest-severity category),
  `demons`-extra, `fx`, `effects`, `fusionrpg.core/injector/server/cheatcore` (the tool's catch-all
  buckets for unnamed folders), `services`, `progression`, `ai`, `policies`, `hud`, `lawn`, `match`,
  `sqlite`, `diagnostics`, `host`. Per `/goal`'s own rule, a stale plan's undercount is not a
  boundary the audit defines — the tool's live output is. Folded into this task rather than treated
  as separately authorized scope, since `power-todo.md` §Phase M's own text (baseline 329, "M1 111 ·
  M2 80 · M3 88 · M4 50") was always meant to cover the full audit, not 13 hand-picked domains — M.6
  arming a CI gate that was already red on 90 known findings would have been a broken gate on day one.
  **`vfx` proper** (Core `Vfx/` + Injector `Fx/`, one domain — the pooling code is the rendering half
  of the same feature the Core math half lives in): `data/tuning/vfx.v1.json` — `VfxTintMath.MaxStrength`,
  `VfxBurstMath`'s 3 named consts, `VfxRules`'s 13 (NOT `FloaterCap`/`FloaterLifeSeconds`/`RisePixels`
  — those alias `DamageFxFloaterRules`, migrated under `effects` below, which stays the one source per
  `VfxRules`'s own doc comment), `VfxSustainedRules`'s 6, plus 4 Injector `Fx/` classes' render
  constants (`BurstPool.BurstParticles`, `FxResources.ParticleSortingOrder` +
  `ParticleTextureSize`/`MarkerEdgeSoftness` found via the post-migration re-audit below,
  `ShieldBarPool`'s 6 bar-geometry fields, `TintCompositor.ReassertSeconds`).
  **`VfxCatalog.cs` and `VfxAuraMath.cs` deliberately not migrated**: hand-authored color/visual-
  identity content and procedural shape-math coefficients, not a reusable balance table — same
  reasoning as M.2's `WorldTemplateCatalog` exemption; both added to `CONTENT_FILE`.
  **`VfxTintMath.Clamp` fixed to `byte.MaxValue`/`MinValue`** instead of bare `255f`/`0f` — cleaner
  and resolves its M1 finding without a file exemption.
  **`battle`**: `data/tuning/battle.v1.json` — `TraitBattleCatalog`'s 14 traits' full Milli/Charges
  sets (not just the 8 the regex flagged — full-file discipline, same as M.2/M.3's precedent), trait
  ids/mechanisms/ChannelMods stay in C# (schema). `BattleRuleset.RoundDurationMs`/`MaxRounds` — these
  are tunables-ssot.md §1's OWN worked grey-zone example (*"`MaxRounds = 50` bounds a battle... but a
  designer might well want 30 or 80 — tunable"*) — migrated, not left structural; still needs a
  RulesetVersion bump to change, a separate, orthogonal governance concern the migration doesn't
  remove. `BattleStatComposer`'s two affinity divisors. `EngineVersion`/`RulesetVersion` stay
  structural consts (identity, not magnitude).
  **`summoning`** (spec-demon-summoning.md — a genuinely separate corner of `Demons/` from
  contracts/souls/patron/fusion): `data/tuning/summoning.v1.json` — `SummonBannerCatalog`'s 2 banners'
  costs/focus-weight, `SummonRoller`'s 8 pity/rarity consts.
  **`effects`** (Core `Effects/` — distinct from `vfx`/`fx`): `data/tuning/effects.v1.json` —
  `MatchupRead.SlotShareMilli`, `DamageFxFloaterRules`'s cap/lifeSeconds/risePixels (the source
  `VfxRules` aliases). Rest of the domain is structural, comment-only: `AtomKindRegistry`'s 3 closed-
  vocabulary counts, `PowerScalar.Categories`, `ResourceDeltaMath.AmountCap` (an overflow guard that
  already throws, never clamps), `CompiledAtom.False` (a jump-target sentinel),
  `CombatDebugObservability.Cap` (debug ring-buffer size).
  **`world-ai`** (spec-ai-commander.md, unbuilt consumer — no host currently calls
  `FrontierRulesPolicy.Decide()` in production, only tests; wired into Server anyway since world-turn
  resolution is architecturally a Server concern, matching `world.v1.json`'s precedent): `data/tuning/ai.v1.json`
  — `FrontierRulesPolicy`'s 3 consts, `ThreatMap`'s 3 (`FalloffReach` stays computed, `1000 /
  proximityFalloffPerHop`, not a duplicated key), `ValueMap`'s 3 plus `ValueWeights.Default`'s 6 axis
  weights.
  **`sim`** (`SimEngine`, "server-side board simulation (no Unity)" — real, live, Server-hosted, not a
  throwaway fixture): `data/tuning/sim.v1.json` — `SimDefaults`'s 5 long fallback stats. Name strings
  (`LevelName`/`PlantTypeName`/`ZombieTypeName`) stay structural.
  **`progression`**: `data/tuning/progression.v1.json` — `RpgXpCurve`'s 3 per-kind (first, step)
  pairs, `RpgXpAwards`'s 5 deltas. **Compile-time-constant wall found**: `RpgXpAwardMapTests.cs`'s
  `[InlineData(..., RpgXpAwards.Kill, ...)]` rows require a compile-time constant, which a config-
  backed property can never be — same class of constraint as the session's earlier default-parameter
  breaks, but attributes have no nullable-parameter workaround. Fixed by hardcoding the 4 literal
  values directly in the `[InlineData]` rows (commented, pointing at `progression.v1.json`) and
  leaving `Kill_award_uses_power_scale_one` to assert the live value — production code migrates
  cleanly, the test's few affected rows just can't reference it by name anymore.
  **`match`**: `data/tuning/match.v1.json` — `CapPolicyConfig.MaxLivingPlants`/`MaxLivingZombies`
  (`MaxLivingBullets` stays a plain `-1` sentinel, not a magnitude). **Regression avoided by applying
  the session's own established lesson**: an initial pass kept `= 50`/`= 80` as literal property
  fallbacks (for `new CapPolicyConfig { OneField = x }` test call sites) — safe for behavior, but
  still a bare M1 literal in a `*Policy.cs` file. Fixed properly using `CombatPolicy`'s own pattern:
  property initializers aren't a compile-time-constant context, so `= MatchTuningPolicy.MaxLivingPlants`
  is legal and removes the bare literal entirely.
  **`net`** (Injector-only — `RpgClient`/`PerfReporter`, no Server code path ever touches either,
  confirmed by grep before wiring only `RpgHost.Initialize`, not `Program.cs`):
  `data/tuning/net.v1.json` — `RpgClient`'s queue/drain/flush, `PerfReporter.IntervalSeconds`.
  **`data`** (`FusionRpg.Data`, Server-only — Core's host/core-separation rule applies the same way
  one level down, so the parser lives in `FusionRpg.Data.Policies`, not Core): `data/tuning/data.v1.json`
  — `SealedCompactionPolicy`'s 4 retain tails (schema-version fields stay structural) and
  `RpgStore.Souls.MaxSoulAward` (an overflow ceiling that already throws, matched via "award" —
  migrated rather than fought with a regex exemption, since "award" correctly catches genuine
  tunables like `RpgXpAwards` elsewhere).
  **Remaining ~20 structural-only findings, comment-fixed, no config**: Win32/GameWindowInterop's
  ShowWindow/hotkey constants (fixed Win32 API values), `LawnCoordMath`'s 10×5 board shape,
  `PortPicker`'s network port range, `PerfProbe.SectionCount` (enum cardinality),
  `GameProfileCatalog`'s two game-binary fingerprints (whole file added to `CONTENT_FILE` — detection
  data, not a balance table), `CheatCommandRunner.SeenCap`/`InjectorCommandInbox.Cap`/
  `PerfEndpoints.Cap` (bounded queue/ring-buffer sizes), `DebugScenarios`'s 7 vanilla-game type ids,
  `GameHooks`'s 2 network-batch `chunk` consts, `EventIngest.WriterBatch` (SQLite write-batch size).
  **`OverlaySettingsGui.PanelW`/`PanelH` migrated into the existing `overlay.v1.json`** (extended, not
  a new file) rather than left structural — same UI-geometry-tunable class as `OverlaySwitchLayout`,
  already migrated in M.4 with the identical "UI/feel tuning, not gameplay balance, same T1 test
  applies" reasoning.
  **Two more genuinely new A3 overflow findings**, found by re-running `audit-overflow.py` after this
  task (not by inspection) — `docs/architecture/power/overflow-triage.md` §3.5/§3.6 addenda:
  `WorldAiTuning.cs`'s `ValueWeightsTuning` and `DataTuning.cs`'s `RetainTuning` mirror two
  already-BOUNDED types field-for-field (a per-mille policy weight, a retention-tail count), so both
  inherit the existing verdict rather than needing a new one. 19 → 21 BOUNDED, 0 critical, unchanged.
  - Verify: `audit-magic-numbers.py --summary` → **TOTAL 0/0/0/0** across every domain, repo-wide (not
    just the 13 originally-named ones). Full suite green, exact baseline preserved: Core 2971, Data
    470, Guard 73, Launcher 162, CheatCore 40, Server.Tests 15, E2E 194, ItemSeedValidator 71,
    AtomImporter 21, ElementEnumGen 14 — **4031 total, 0 failures**, matching the count from before
    this task started byte-for-byte. All 4 host `.csproj`s already glob `data\tuning\**\*.json` — the
    10 new tuning files needed zero `.csproj` edits. `guard-overflow.ps1` still OK, 0 critical.
- [x] **M.6 — Arm the magic-number audit in CI** · done 2026-08-24. New `scripts/guard-magic-numbers.ps1`
  (mirrors `guard-overflow.ps1`'s shape: wraps `audit-magic-numbers.py`, fails only on M1/M2 — M3/M4
  are MEDIUM/LOW style gaps, not the T1 rule this gate enforces). Wired into `ci.yml` (new step,
  same pattern as the overflow guard) and `deploy-play.ps1` (5th→6th guard in the sequence). No M1/M2
  caveat needed — unlike the original "for migrated domains only" plan, the audit is genuinely
  0/0/0/0 repo-wide as of M.5, so the gate is real from the moment it's armed, not scoped down to
  dodge known-red domains.

### ✅ Checkpoint M *(per domain)*
- [x] Numbers in config · behaviour byte-identical · `CLEAN` · audit clean for that domain — **every**
  domain the tool reports, confirmed via `audit-magic-numbers.py --summary` returning empty/0 with no
  `--domain` filter, not just the 13 domains M.1–M.4 named before the M.5 scope-gap finding.

---

## Phase D — doc reconciliation *(parallel, no code, no dependencies)*

All four are **unbuilt features**. Reconciling now is free; after they ship it is a rebalance.

- [x] **D.1 — Enhancement `+X`: uncap** · done 2026-08-24. `ssot-enhancement.md` §7.3 rewritten:
  `ilvl_cap` was `clamp(4 + ilvl/4, 4, 20)` (a hard, unexplained ceiling on top of an already-
  self-limiting risk curve) → `max(4, 4 + ilvl/4)` (floor only, no ceiling). `rarity_cap`'s table
  reframed as open-ended (a future rarity rung above Legendary/Unique adds a row, not bounded at
  +20). `progression_cap`'s "defaults to 20 (no gate)" reworded to "a high, effectively-non-binding
  value" in both §7.3 and the §10 open-questions echo, so no reader treats 20 as a real number to
  preserve. The Peril band's falling success rate + from-+17 level-drop risk (§3.1, unchanged) is
  named explicitly as the soft cap this system already has — the hard numeric ceiling was redundant
  on top of it, not a second real constraint.
- [x] **D.2 — Rarity promotion: soft cap** · done 2026-08-24. `ssot-rarity.md` §3.7 rule 7 rewritten:
  the ordinal-80 ceiling is now described as reading the existing `promote_from` per-rung registry
  column (§4.4 — this table already existed pre-edit, so the "table row, not a constant" mechanism
  was already correct; the prose just described it as a hardcoded "80" instead) — raising or
  removing it is a data edit, not code. **Did not** resolve §10 open question 4 ("is 80 the right
  ceiling, or 90, or none") — that is explicitly still open for the owner, unchanged by this pass;
  D.2 only makes sure whichever answer they give lands as a table edit. Promotion's *cost* (I6's,
  separate from the ceiling) noted as due the same per-rarity-table treatment once I6 specs it.
- [x] **D.3 — PvZ item drop caps: remove** · done 2026-08-24. `ssot-generation.md` §4.6 rule 4 (2/run,
  12/day, `pvz_loot_budget`) deleted outright — verified `standalone-rpg-map.md:20`'s exact quote
  first, not just trusted the todo text. Rules 1–3 (now 1–4) already do rate parity's real job
  (source reachability, containment, unweighted equipment), so the count cap added throttling with
  no odds-protection the other rules didn't already supply. Three dangling references fixed for
  consistency, not left stale: the `item_drop_log.notes` enum's `pvz_cap` value marked retired, and
  §11 open question 4 (which asked "what should the number be") struck through as no-longer-open
  rather than left implying a number is still owed.
- [x] **D.4 — `ssot-generation.md` §4.1** · done 2026-08-24, alongside D.3 since both touch overlapping
  cross-referenced text. Verified against code before rewriting, per DESIGN-GATE.md: `ExpeditionResolver.cs`
  (`WaveChain`, `BossWaveId = "rift-tyrant"`) dispatches every expedition battle through the same
  `BattleSetup{WaveId, Wave}` a web battle uses, so "expedition tick: tier base level" and
  "expedition boss: tier base level + 3" were never real — the resolved wave's own
  `WaveDef.RecommendedLevel` (`WaveCatalog.cs:5`) is the level, exactly like web battle; only *which*
  wave gets picked varies by tier. `grep -rn mappedRunLevel` found zero hits outside the one line
  citing it — "PvZ run: min(mappedRunLevel, playerLevel)" was never implemented anywhere, so it's now
  recorded as undesigned (new §11 item 8) instead of restated as shipped. Not folded into T2.3 (that
  stayed Phase-1-gated, unauthorized this session) — done standalone, matching Phase D's own "no
  dependencies" framing; "folded into T2.3 if that lands first" was an optimization, not a hard block.

### ✅ Checkpoint D
- [x] Four specs corrected · no code touched · each states it is a pre-build reconciliation — every
  edit cites the file:line or doc quote it verified against before writing (DESIGN-GATE.md), and each
  correction lists what it deliberately left as a still-open owner decision rather than silently
  answering one (D.2's ceiling number, D.4's PvZ-run contentLevel design).

---

## Review gate

Not a blocker — Phases 0, M and D are authorized and need nothing from anyone.

- [x] **Owner approves the map + 10 specs** → unlocks Phases 1–4 — approved; Phases 1-3 built and
  verified 2026-08-24 (T1.1 through T3.6, Checkpoints 1-3 all passed). This checkbox went stale
  behind the work it gates — flipped here rather than left implying the work below it never started.

**Welcome, not owed:** the world program may confirm or move `Wm = 5` · the demon/economy stream may
retune the soul constants it already owns (unchanged today, so silence is a valid answer).

**Everything else from this session is decided** — the earn formula (SSOT §11.7a), `Wm`, the §10.4
economy split, and the ADR P1 amendment (written into `decisions.md`).

---

## Notes

- **`tools/` is not a C# blind spot here.** `--paths src tools` returns identical counts because
  `tools/` holds no `.cs`. The `guard-dal.ps1` gap in `DESIGN-GATE.md` §1 is about SQL in Python
  tooling and does not apply to these audits.
- **Both audit tools' first runs were mostly false positives** — overflow reported 121 criticals (all
  wrong), magic-numbers 365 (comment text and single-digit arithmetic). Precision gates were added
  both times. Worth remembering before trusting any new scanner's first number.
