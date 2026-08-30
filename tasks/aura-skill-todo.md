# Tasks: aura-skill program

Plan: [aura-skill-plan.md](aura-skill-plan.md) · Map:
[../docs/architecture/aura-skill-map.md](../docs/architecture/aura-skill-map.md) · Specs:
[../docs/architecture/aura-skill/](../docs/architecture/aura-skill/) · Defects:
[../docs/architecture/derived-pipeline-audit-2026-08-30.md](../docs/architecture/derived-pipeline-audit-2026-08-30.md)

**22 tasks · 5 checkpoints.** Scope: **XS/S** ≈ under an hour · **M** ≈ a focused session · **L** ≈ split it.

> ## ⛔ Rules binding on every slice below
>
> **1. T3 gates all authoring.** No `rpg_action`, `rpg_action_grant`, or `world-buff.*` row may be
> authored before T3 lands. The first Skill grant currently throws every web battle **and** poisons the
> stored `BattleSetup` log rows, which re-throw on every replay (audit D3).
> **2. No task is done on internal criteria alone.** Each names its share of Gate A/B/C (plan §"The
> acceptance rule"). A task whose criteria pass while nothing is buffed is mis-specified.
> **3. Every balance number is a tunable.** Owner, 2026-08-30: *"tunable is requirement"* — including
> the rung mapping itself.
> **4. `guard-class-system.ps1` is currently RED** (G3 Might/Ferocity double-counted atk). Pre-existing
> and unrelated — but do not make it worse, and do not claim it green.

## Owner decisions, 2026-08-30

| # | Decision |
|---|---|
| **Toggle** | **Add a recompose seam to `BattleEngine`.** Battle is match-frozen today (`Derived` is get-only, one `Compose` call site at `BattleEngine.cs:30`); the full toggle/evict design is preserved by making recompose possible. ⚠️ Kernel work — deterministic, golden-tested. |
| **R3** | **Commanders are real actors in empire legions.** Two for now — **Crazy Dave** and **Dr. Zomboss** — for the lawn run; a broader commander feature comes later. |
| **Symmetry** | Follows from R3: Zomboss is a commander actor, so **full symmetry is reachable** — he runs auras from the same twelve. |
| **D3** | **Both** — degrade now (safety net), wire the `ActionCatalog` when content lands. |
| **Rung** | **Tier-mapped (rungs 7–10), and the mapping itself is a tunable** so aura strength can be rebalanced without code. |
| **W4** | **Wire `actorResolve` at the production call sites.** Reflect math exists and is test-exercised; only the argument is missing. Unblocks Retribution and fixes a gap that exists regardless of auras. |
| **Own-side** | **Build the real specimen-ownership bridge** (production `IOwnSideOracle`) rather than shipping a narrower selector. Unlocks `RelationKind.Ally` — the property that makes one authored row serve both factions. |
| **Zomboss** | **Each pattern names its aura.** The nine `ZombossPatterns` gain an aura id beside their share vector. ⚠️ Dynamic AI aura control is a **separate, larger feature** — see "Deferred" below. |
| **Banner** | **Keep both, relationship defined.** Banner = **gear** (found/crafted, item progression, 100‰ commander item budget); aura = **skill** (chosen/invested, aptitude progression, aura budget). **They stack; the budgets stay separate** and neither absorbs the other. |
| **Patron** | **Give `patron.aura` a `P(Θ)` term** so it stays relevant instead of being outscaled. ⚠️ A balance change to a **spec-locked** system (2026-08-21) — needs its own sign-off, and puts two unbounded side-wide buffs on the same channels. |
| **Eviction** | **Pure FIFO — oldest always goes.** No pin, no refusal. Simplest, fewest clicks. |
| **Upkeep** | **Per-aura cost lists, spanning 1 to 6 resources.** Owner: *"aura is not a hard coded skill, it's a continuous action that can toggle and affect all battlefield."* ✅ **Unblocked** — see §Resource rule below. |
| **Action kind** | **A property of a skill, not a fourth kind.** An aura stays `ActionKind.Skill` and carries flags for what makes it different — continuous, toggleable, battlefield-scoped target. Decision 25 stays intact; loadout accounting and every existing `ActionKind` consumer are unchanged. |
| **Budget** | **Shared default with a per-aura override.** Parity by default; deliberate outliers possible. |

## ✅ Resource rule — corrected 2026-08-30, landed before the aura work

The collision this section previously flagged is **resolved, and the resolution was that the rule was
wrong**. Owner: *"that is a design defect — any resource can be cost for actions, like hp sacrifice
action, how can we make something like that if we can't pay for hp?"*

**All six resources are now legal action costs.** The old rule (*"`hp`, `hunger` and `spirit` are never
action costs"*) made three legitimate designs unbuildable: an HP-sacrifice action; a sun-priced plant
action (`hunger` **is** Sun on the plant side, and spending sun is the core PvZ verb); and any sink at
all for `spirit`, which had **none** — a resource with no sink is not a resource.

Landed as its own change, **before** the aura work, in three places:
`decisions.md` (Resource model row) · `resource-hub-ssot.md` (the "pays for" table) ·
`concrete-action-roster.md` (the Costs line).

Two rules came with it, and both bind on T14/T16:

- **Every resource documents what spending it *means*.** The hub's "pays for" column is **normative**,
  not descriptive. A cost on a resource whose meaning is undecided is an authoring error.
- **`hp` costs floor at 1 by default**, refusing via the existing `CannotAfford(hp)` — **but an action
  may opt into being lethal**, per-action, never by default.

**No new machinery is needed:** `rpg_action_cost` is `(action_id, resource_id, amount_spec, when)` and
*"an action costs a **list**, not a single pool"*, so multi-resource costs were always expressible.
Only the legality rule was in the way.

**Action kinds stay closed at three.** An aura is `ActionKind.Skill` carrying continuous/toggleable/
battlefield-scoped flags — decision 25 is untouched.

---

## Phase 0 — foundation and the seam

- [x] **T1: `OverlayAdd` + the idempotence rule + patron migration** · **M** · **Done 2026-08-30**
  - Added `OverlayAdd` beside `Overlay` on `ActorDerivedSnapshot` (audit **D1**); migrated
    `PatronAuraOverlay.Apply` to it, removing the manual `derived.Get(channel) +` compensation in the
    same change (`AddChannel` now contributes only the aura's own delta).
  - `ActorDerivedProfiles.cs`'s five `.Overlay(` sites were triaged, not touched — all five write into
    a fresh `StubNeutral()` base where the channel didn't previously exist, so replace and add are
    numerically identical there; `Overlay_channels_replace_profile` (the pinned regression test) was
    re-run, still green, confirming replace semantics are untouched.
  - **Idempotence (D2)** stated as a functional-dependency rule (a contribution is a function of
    `(source, coefficients)` only, never of the channel's current value) and proven by two independent
    `PatronAuraOverlay.Apply` calls from the *same* base snapshot producing bit-identical results.
  - Discovered while linking: **no test project references `FusionRpg.Injector`** — the BepInEx project
    needs real Unity/IL2CPP interop DLLs (`FUSIONRPG_GAME_DIR`, unset in this environment) to build at
    all. Followed the established `FusionRpg.Launcher.Tests`/`FileRpgConfig.cs` precedent: linked
    `PatronAuraOverlay.cs` (Unity-free) directly into `FusionRpg.Core.Tests.csproj` via `<Compile
    Include Link>` rather than a project reference.
  - Acceptance (all met): two overlays on **different** channels don't stomp each other · `OverlayAdd`
    accumulates two contributions on the **same** channel · applying the same fixed contribution twice
    from the same base is bit-identical to once · patron aura's numeric output is **byte-identical**
    to the pre-migration formula (hand-verified: 150‰→+15.0, 75‰→+7.5, secondary element at declared
    weight) · the five `ActorDerivedProfiles` sites still replace.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "...OverlayTests|...PatronAuraOverlayTests"`
    — **12/12 new tests green**. Full suite: **4485/4485 green, 0 failures.** No goldens moved (Core.Tests
    is the golden-bearing suite for this pipeline; nothing else references these files).
  - Files: `Stats/Derived/ActorDerivedSnapshot.cs` (edit), `Injector/Effects/PatronAuraOverlay.cs`
    (edit), `tests/FusionRpg.Core.Tests/FusionRpg.Core.Tests.csproj` (edit — new compile-link),
    `tests/.../Stats/Derived/ActorDerivedSnapshotOverlayTests.cs` (new, 5 tests),
    `tests/.../Injector/PatronAuraOverlayTests.cs` (new, 7 tests).

- [x] **T2: op × compose-kind validation at bind/author time** · **S** — DONE 2026-08-30
  - Audit **D6** fixed: added `AtomRowValidator.DerivedComposeAcceptedOps` (the real per-kind op
    filter, mirroring `DerivedComposer.ComposeChannel` exactly: FlatSum→{flat}, FlatReplace→
    {flat,replace}, SumIncreased→{increased}, MaxPriorityFlag→{flag,replace,increased}). `Validate`
    gained an optional `Func<string, DerivedComposeKind?>? composeKindOf` param (default null — opt-in,
    matching the existing `curveInput` pattern); `ValidateOp` rejects a `stat.derived` row whose `op`
    the target channel's compose kind never reads, with `AtomRejectionReason.ParamNotHonoured`.
  - Chose bind/author-time rejection over a compose-time throw specifically **because** a throw would
    have turned `AptitudeResolver.cs:51-58`'s documented `Flat`-on-`MaxPriorityFlag` fallback (a
    programmatic emission, never an atom row) into a runtime crash for a case its own comment calls
    "cannot happen from authored content today" — confirmed by reading the fallback path directly
    before touching anything, not assumed.
  - Wired the resolver at all **3 production call sites** (the only places `Validate` is ever called):
    `RpgStore.Atoms.cs` `UpsertAtom` + `UpsertAtoms` (new `static readonly DerivedStatRegistry
    ComposeKindRegistry` + `internal static DerivedComposeKind? ComposeKindOf(string)`), and
    `RpgStore.Import.cs`'s one call site.
  - No `FusionRpg.Injector.Tests` project exists and Injector needs real game DLLs to build — not
    relevant here (this task never touches Injector), noted only because it shaped T1's approach.
  - Acceptance: every op×kind pair (16 cells) accepted or rejected-with-reason — proven empirically,
    not asserted, by cross-checking the validator's table against what `DerivedComposer` actually reads
    for each cell · `DerivedComposer` untouched, stays a pure fold · `AptitudeResolver`'s existing path
    untouched · the check is provably opt-in (omitting `composeKindOf` reproduces pre-T2 behavior).
  - Verify: `dotnet build tests\FusionRpg.Core.Tests` clean (0 errors) · new tests —
    `DerivedComposeKindOpsTests` (16-cell theory + 1 sanity test) and
    `AtomRowValidatorDerivedOpTests` (3 tests: mismatched op rejected, matching op accepted, no-resolver
    behaves as before T2) — **20/20 green**. Full suite regressions: `FusionRpg.Core.Tests`
    **4505/4505 green** (4485 T1 baseline + 20 new), `FusionRpg.Data.Tests` **532/532 green** (proves no
    existing seeded/imported atom row actually hits the new rejection — the 3 wired call sites see only
    already-clean content), `FusionRpg.Guard.Tests` **116/116 green**. No goldens moved.
  - Files: `Effects/Atoms/AtomRowValidator.cs` (edit), `src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs`
    (edit), `src/FusionRpg.Data/Sqlite/RpgStore.Import.cs` (edit),
    `tests/.../Atoms/DerivedComposeKindOpsTests.cs` (new, 17 tests),
    `tests/.../Atoms/AtomRowValidatorDerivedOpTests.cs` (new, 3 tests).

- [x] **T3: D3 degrade path** ⛔ **gates all authoring** · **S** — DONE 2026-08-30
  - `BattleRunState`'s two throw sites (missing catalog, id not in catalog — `BattleRunState.cs`, the
    loadout-compile loop) now degrade the actor to the basic-attack fallback and record a named entry
    in `Warnings`, instead of throwing. A **partial** mismatch (some ids resolve, one doesn't) degrades
    the *whole* actor to no-equipped-actions too — the owner's own wording, "treat as no-equipped-
    actions + warning," not a partial mix.
  - `Warnings` is a new `BattleReport` field (`IReadOnlyList<string>?`), given the **exact same
    provenance treatment as `ContentHash`**: null by default, `[JsonIgnore(WhenWritingDefault)]`, and
    blanked in `BattleGoldenTests.Hash` alongside `EnvironmentStamp`/`ContentHash` — a dropped-content
    warning is provenance, not battle math, the same reasoning that already excludes the other two.
  - Confirmed the exact poisoned-replay shape this fixes is real, not hypothetical:
    `WebMatchService.cs:69,111` (the stored-setup replay call sites) call
    `BattleEngine.Resolve(storedSetup, entry.Seed)` with **no `actionCatalog` argument at all** — every
    replay of a setup with a non-empty `EquippedActionIds` hit the missing-catalog throw, every time.
  - Two pre-existing tests asserted the OLD throw as correct behavior
    (`ActionSelectionAdoptionTests.A_nonempty_loadout_with_no_catalog_throws_loudly` and
    `An_unknown_equipped_id_against_a_real_catalog_throws_loudly`) — rewritten to assert the new
    degrade-with-warning behavior instead, since the throw they pinned is the exact defect this task
    removes, not a behavior to preserve.
  - Acceptance: a `BattleSetup` with non-empty `EquippedActionIds` and no catalog resolves without
    throwing ✅ · a previously-poisoned stored setup replays cleanly (same outcome both times, `Warnings`
    populated both times) ✅ · the warning names the actor and the dropped/unresolved action ids ✅.
  - Verify: `dotnet build` clean on `FusionRpg.Core` and `FusionRpg.Core.Tests` · new tests
    (`BattleRunStateDegradeTests`, 4 tests) + `BattleGoldenTests` (5 tests, **no golden hash moved**) —
    **9/9 green**. Full suite regressions: `FusionRpg.Core.Tests` **4509/4509 green** (one unrelated
    flaky allocation test — `ValueSpecTests.Resolving_allocates_nothing` — failed once under full-suite
    GC pressure, reran green in isolation; not touched by this task), `FusionRpg.Server.Tests`
    **32/32 green**, `FusionRpg.CheatCore.Tests` **40/40 green**, `FusionRpg.Launcher.Tests`
    **162/162 green**.
  - Files: `Battle/BattleModels.cs` (edit — `BattleReport.Warnings`), `Battle/BattleRunState.cs` (edit
    — degrade instead of throw, `Warnings` list), `Battle/BattleEngine.cs` (edit — wire
    `state.Warnings` into the report), `tests/.../Battle/BattleRunStateDegradeTests.cs` (new, 4 tests),
    `tests/.../Battle/BattleGoldenTests.cs` (edit — blank `Warnings` in `Hash`),
    `tests/.../Battle/Adoption/ActionSelectionAdoptionTests.cs` (edit — 2 tests rewritten).

- [x] **T4: `BattleEngine` recompose seam** ⭐ · **M** · **kernel, handle with care** — DONE 2026-08-30
  - **Re-checked the "match-frozen" premise before building anything, and it was half wrong.**
    `ActorState.Derived`'s PROPERTY reference is get-only (assigned once), but `ActorDerivedSnapshot`
    itself is a **mutable** class with an `internal Set(channel, value)` already used in production —
    `BattleEffects.cs:227`, `owner.Derived.Set(DerivedStatChannels.CombatDefenseOmni,
    Ledger.Recompose(ownerKey, "defense", owner.BaselineDefense))` — a live, triggered `stat.modify`
    grant already mutates one `Derived` channel mid-battle today, sourced through
    `BattleStatModifierLedger` (A18e). The correct finding is: a single-channel, single-purpose
    recompose seam already ships; what's missing is a **general, sourced, multi-channel** one auras
    can use for arbitrary `combat.*` channels with proper withdrawal. Recorded here rather than left
    implicit, since the earlier "structurally match-frozen" framing (spec-aura-delivery-path.md §2,
    aura-skill-plan.md) undersold what already existed.
  - Built `BattleDerivedModifierLedger` (`Battle/BattleDerivedModifierLedger.cs`) — the `Derived`-
    channel sibling to `BattleStatModifierLedger`: sourced `Add`/`RemoveBySource`, and a `Recompose`
    that writes `baseDerived.Get(channel) + Σ(active sources)` into the live snapshot for every
    (actor, channel) pair it tracks — **idempotent by construction** (always recomputed from the
    frozen base, never accumulated onto `live`'s own prior value, closing audit D2 for this path the
    same way T1 closed it for `OverlayAdd`). Deliberately NOT routed through `DerivedComposer` — no
    "base" `DerivedModifier` entry exists to fold against (`BattleStatComposer.Compose` seeds
    `Derived` from actor setup fields directly, never from a modifier list) — plain addition is not a
    shortcut here: every channel this program's content targets registers `FlatSum`
    (`DerivedStatRegistry.RegisterCombatDefaults`), and `FlatSum` composing IS "sum every
    contribution."
  - `ActorState` gained `BaseDerived` — a defensive copy taken the instant `Derived` is born
    (`ActorDerivedSnapshot.FromValues(Derived.Channels)`), because `Derived` is mutable and cannot
    double as its own frozen baseline once anything writes to it. `BattleRunState` gained one
    `DerivedLedger` instance per battle (same lifetime as `Ledger`) and a `RecomposeDerived(actorKey)`
    entry point.
  - **Nothing calls `RecomposeDerived` inside `Resolve`'s loop.** This is the "explicit, never implicit
    per-tick" bar, met by construction: the seam exists as a call a real trigger makes (an aura
    toggling on/off, T13) — it is not on a schedule, and with zero producers before T9 the ledger is
    always empty for every actor in every battle today, so its mere presence is a hard no-op (nothing
    tracked ⇒ nothing visited ⇒ `live` untouched).
  - Acceptance: recompose mid-resolution matches composing up front — proven directly against
    `OverlayAdd` (T1's own "compose in one shot" reference) in
    `Recompose_mid_resolution_matches_composing_the_same_state_up_front` ✅ · **no golden moves** ✅ ·
    recompose is explicit, never implicit per-tick ✅ (no call site exists yet; T9+ adds the first one).
  - `BattleEngine.ActorState`/`BattleRunState` are `private` nested types (B13's own deviation note),
    so the seam is proven at the `BattleDerivedModifierLedger` level directly with real
    `ActorDerivedSnapshot` instances — the math is a pure function of (base, active sources) and does
    not depend on being invoked from inside a live `Resolve()` call, the same constraint
    `ActionSelectionAdoptionTests`' own doc comment already names for this codebase.
  - Verify: `dotnet build` clean on `FusionRpg.Core`/`FusionRpg.Core.Tests` · new
    `BattleDerivedModifierLedgerTests` — **7/7 green** (empty-ledger no-op, matches-compose-up-front,
    multi-source summing, idempotent-on-repeat, withdraw-one-keep-other, withdraw-last-falls-to-base,
    cross-actor isolation) · `BattleGoldenTests` + `DominanceBaselineTests` — **8/8 green, zero hash
    moved**. Full suite: `FusionRpg.Core.Tests` **4516/4516 green** (4509 + 7 new),
    `FusionRpg.Server.Tests` **32/32 green**.
  - Files: `Battle/BattleDerivedModifierLedger.cs` (new), `Battle/BattleEngine.cs` (edit —
    `ActorState.BaseDerived`), `Battle/BattleRunState.cs` (edit — `DerivedLedger` +
    `RecomposeDerived`), `tests/.../Battle/BattleDerivedModifierLedgerTests.cs` (new, 7 tests).

- [x] **T20: wire `actorResolve` at the production call sites** · **S** · Phase 0 — DONE 2026-08-30
  - Added `EffectBag.ActorResolve` (a settable `CombatActorResolve?`, null-default like every other
    optional collaborator on the class) and threaded it through as the new `actorResolve` argument at
    all 5 named call sites: `EffectBag.cs`'s two `DispatchInstant` calls (grant processing + counter-
    burst) and its `TickDots()` → `StatusFunnelPulseSink` construction; `StatusEffectBridge.cs`'s
    `StatusFunnelPulseSink` gained an `_actorResolve` field threaded into its own two `DispatchInstant`
    calls (`PulseHp`, `PulseHealAttacker`); `CheatCommandRunner.cs`'s debug-enqueue-delta call now
    passes `Effects.EffectRuntime.Bag.ActorResolve`.
  - Wired the actual value at the one place both `OverlayCombatMath` and `ShieldGate` already get
    theirs — `EffectRuntime.WireCombatMath` (its own comment: *"same resolve as combat"*) — with
    `bag.ActorResolve = InjectorCombatBridge.ResolveActor;`, the exact delegate the other two already
    use. Also wired `FoundationHarness.WithOverlayCombatMath()` to set `_bag.ActorResolve` (previously
    it only exposed a `Resolve` property for a caller to pass manually into a hand-built
    `DispatchInstant` call) — so a test driving reflect through `EffectBag`'s own grant processing, not
    a raw dispatcher call, now exercises the identical production wiring.
  - `CombatDamageDispatcher.cs`, `OverlayCombatMath.cs`, `ShieldGate.cs` — **untouched**. This was
    exactly "threading an argument, not new mechanics," confirmed by not needing to touch the math at
    all.
  - `CheatCommandRunner.cs`/`EffectRuntime.cs` are in `FusionRpg.Injector` — **cannot be built** without
    real game DLLs (`FUSIONRPG_GAME_DIR` unset in this environment; confirmed by attempting the build,
    which fails on ~754 pre-existing missing-Harmony/Unity-type errors unrelated to this change, the
    same constraint T1 hit with `PatronAuraOverlay.cs`). Verified by direct read instead: both edits are
    two-line, syntactically unambiguous (a named-argument addition and a property assignment), reusing
    an existing delegate value already proven to type-check at the exact same call sites for
    `ShieldGate`/`OverlayCombatMath.Create`.
  - Acceptance: reflect fires on a real damage packet in production ✅ — proven by a NEW integration
    test that drives it through `EffectBag`'s own `Grant`/`OnEvent` path (not a raw `DispatchInstant`
    call, which is all the pre-existing `ReflectionTests` file ever exercised) and shows the exact
    before/after: `Without_ActorResolve_wired_reflect_never_fires_the_pre_T20_defect` reproduces the
    shipped defect (one `ApplyResourceDelta` action, no bounce), `With_ActorResolve_wired_reflect_fires_
    through_EffectBags_own_dispatch` proves the fix (two actions — the hit AND the reflected bounce
    landing back on the original attacker) · Retribution's channels (`CombatReflectRateOmni`/
    `CombatReflectDamageOmni`) have a live reader once this lands, since `TryReflect`'s own reads of
    them were never reachable before · no goldens move (`BattleGoldenTests` unaffected — battle has its
    own separate `Ledger`/dispatch wiring, not `EffectBag`'s).
  - Verify: `dotnet build` clean on `FusionRpg.Core`/`FusionRpg.Core.Tests` · new
    `ReflectActorResolveWiringTests` — **2/2 green** · existing `ReflectionTests` +
    `OverlayCombat*IntegrationTests` — **72/72 green** (proves the new default-off `ActorResolve` on
    `EffectBag` and the new `WithOverlayCombatMath` wiring don't change any existing behavior). Full
    suite: `FusionRpg.Core.Tests` **4518/4518 green** (4516 + 2 new), `FusionRpg.Server.Tests`
    **32/32 green**, `FusionRpg.CheatCore.Tests` **40/40 green**, `FusionRpg.Guard.Tests`
    **116/116 green**.
  - Files: `Effects/EffectBag.cs` (edit — `ActorResolve` property + 3 call sites),
    `Status/StatusEffectBridge.cs` (edit — `StatusFunnelPulseSink` field + 2 call sites),
    `Effects/FoundationHarness.cs` (edit — `WithOverlayCombatMath` wiring),
    `FusionRpg.Injector/CheatCommandRunner.cs` (edit, unbuildable here — see note above),
    `FusionRpg.Injector/Effects/EffectRuntime.cs` (edit, unbuildable here — see note above),
    `tests/.../Combat/ReflectActorResolveWiringTests.cs` (new, 2 tests).

### ✅ Checkpoint 1 — PASSED 2026-08-30
- [x] Full Core/Guard/Data suites green. **No goldens moved.** — `FusionRpg.Core.Tests` 4518/4518,
      `FusionRpg.Guard.Tests` 116/116, `FusionRpg.Data.Tests` 532/532, `FusionRpg.Server.Tests` 32/32,
      `FusionRpg.CheatCore.Tests` 40/40. `BattleGoldenTests`/`DominanceBaselineTests` (8 tests) hash
      unchanged.
- [x] `guard-class-system.ps1` no worse than its pre-existing red — ran it directly: fails on **G3
      Might/Ferocity double-counting atk**, a defect in primary-stat channel wiring T1-T4/T20 never
      touched (this program's changes were `ActorDerivedSnapshot`, `AtomRowValidator`, `BattleRunState`,
      the new `BattleDerivedModifierLedger`, and `EffectBag`'s `ActorResolve` — none author or wire
      `Might`/`Ferocity`). Pre-existing per `class-system-program` memory; not this program's to fix.
- [x] T3 landed — authoring is now safe (degrade-not-throw, proven by
      `A_previously_poisoned_stored_setup_replays_cleanly`).

---

## Phase 1 — the HoMM3 half (independent of everything above)

- [x] **T5: W1 — the commander allocation delegate** · **M** — DONE 2026-08-30
  - Built `CommanderAllocationSource` (`FusionRpg.Core.Stats.Aptitudes`) — a tiny cache wrapping a
    `Func<AptitudeAllocation>` reader: `Resolve(StatContext)` (the hot-path delegate
    `ActorHubBootstrap.CreateDefault`'s `aptitudeAllocation` param takes) is a bare field read and
    NEVER calls the reader; `Refresh()` is the only thing that does, exactly once per call. No
    server-side revision number exists to gate on (`AptitudeEndpoints.ProjectState` carries none) —
    the injector's own poll/broadcast cadence IS "one revision," so `Refresh()` unconditionally
    replaces the cache each time it's invoked rather than inventing a network-level revision field.
  - **Transport, reusing the shipped endpoint rather than adding one:** `RpgClient.
    RefreshCommanderAllocationAsync()` (new) mirrors `RefreshPvzStatsAsync`'s exact shape — same
    current-player lookup, same try/catch-to-`LastError` — reading the ALREADY-SHIPPED
    `GET /api/aptitudes/{playerId}` (`AptitudeEndpoints.ProjectState`'s `shares` map), building an
    `AptitudeAllocation` the same way `AptitudeEndpoints.cs`'s own POST handler does
    (`AptitudeAllocation.Single` folded via `+`), then calling the new `CheatState.
    ApplyCommanderAllocation(allocation)`, which stores it and calls `Refresh()` on the same tick.
  - **Wired at session start AND on the real change signal**, not a per-hit poll: `StartAsync()` now
    also awaits `RefreshCommanderAllocationAsync()` (alongside the existing stats refreshes), and a new
    `_hub.On<object>("AptitudesUpdated", ...)` SignalR handler enqueues an
    `"aptitudes.allocation.reload"` command — `AptitudesUpdated` is the exact broadcast
    `AptitudeEndpoints.BroadcastBestEffort` already sends on every save, previously with zero
    listeners. `CheatCommandRunner.cs` gained the `"aptitudes.allocation.reload"` branch (mirrors the
    existing `"pvz.stats.reload"` branch exactly).
  - `CheatState.ActorHub` now passes `aptitudeAllocation: CommanderAllocation.Resolve` — the FIRST
    production caller of `AllocationStore`/`RpgStore.LoadAllocation`. Also corrected a stale doc
    comment on `ActorHub` that still claimed *"P6's AllocationStore doesn't exist yet"* — it has existed
    and been tested since point-economy landed; it simply had zero production callers until now
    (`class-system-program` memory's own named gap).
  - `RpgClient.cs`/`CheatState.cs`/`CheatCommandRunner.cs` are in `FusionRpg.Injector` — **cannot be
    built** without real game DLLs (same constraint as T1/T20; confirmed by attempting the build).
    Verified by direct read: every edit reuses an established shape from the same file
    (`RefreshPvzStatsAsync`'s HTTP/JSON pattern, the `PvzStatsUpdated` SignalR handler shape, the
    `pvz.stats.reload` command branch shape) with no new pattern invented.
  - Acceptance: a non-empty allocation produces non-zero `progression.bonus.atk` — proven with a real
    tuning edge targeting that exact channel (not the shipped tuning's own `combat.power.omni` edge,
    which the acceptance text didn't name) · one read per revision, proven by a counting fake — the
    fake reader counts its own invocations, proving `Resolve()` calls it zero times across 50 hot-path
    resolves and `Refresh()` calls it exactly once per call · empty allocation still resolves to zero
    (both the pre-refresh default AND an explicit re-`Refresh()` back to `Empty`, proving the cache is
    replaced, not merged).
  - Verify: `dotnet build` clean on `FusionRpg.Core`/`FusionRpg.Core.Tests` · new
    `CommanderAllocationSourceTests` — **4/4 green**. Full suite: `FusionRpg.Core.Tests`
    **4522/4522 green** (4518 + 4 new; `ClassSystem` subtree alone 150/150).
  - Files: `Stats/Aptitudes/CommanderAllocationSource.cs` (new),
    `tests/.../ClassSystem/CommanderAllocationSourceTests.cs` (new, 4 tests),
    `FusionRpg.Injector/CheatState.cs` (edit, unbuildable here — see note above),
    `FusionRpg.Injector/RpgClient.cs` (edit, unbuildable here), `FusionRpg.Injector/
    CheatCommandRunner.cs` (edit, unbuildable here).

- [x] **T6: W2 — `Θ` hydration** · **S** — DONE 2026-08-30
  - `RpgClient.RefreshPowerIndexAsync()` (new) reads the already-shipped
    `GET /api/rpg/progression/{playerId}/summary` for `player.level` (`RealmsAdvanced`/`PvzRuns` stay 0
    — no server column exists for either, `ServerPowerIndexProvider.ReadSnapshot`'s own honest partial
    hydration, not a shortcut unique to this path), then calls the new `CheatState.
    ApplyPowerSnapshot(playerId, snapshot)`, which hydrates `InjectorPowerIndexProvider` via a local
    cast (`PowerIndex` stays typed as the interface publicly; only this one internal caller needs
    `Hydrate`). Wired at session start (`StartAsync`) and on demand (`"power.index.reload"` command,
    mirroring T5's `"aptitudes.allocation.reload"` shape) — never per hit.
  - **Real gap found and documented, not silently patched:** `HydratedPowerIndexProvider.Key` is
    `(PlayerId, Side, TypeId)` — NOT `EntityKey`. A first draft of the "two different Θ" test hydrated
    two contexts differing only by `EntityKey` (`"Low"`/`"High"`) and both landed on the SAME cache slot
    (`"0:Plant:0"`), the second write silently overwriting the first — caught by the test itself
    (`thetaLow == thetaHigh == 50` when it should have been 5 vs 50), not assumed away. The corrected
    test hydrates by distinct `PlayerId` instead, which IS how the real key varies. **This means
    `CheatState.ApplyPowerSnapshot`'s single hydrate call (`PlayerId` set, `Side`/`TypeId` left at their
    defaults) only makes Θ non-flat for a resolve that ALSO uses the same default `Side`/`TypeId`** —
    it does not cover every `(side, typeId)` combination a real lawn's plants/zombies could resolve
    with. Fully closing that requires either a broader hydration loop (enumerating live board typeIds)
    or reconsidering whether `Θ`'s cache key should include `TypeId` at all — a decision for the power
    ladder's own SSOT (`ssot-power-scale.md`), not something to redesign inside this task. T6 supplies
    the first real caller and proves the mechanism correctly; the keying granularity is a named,
    separate gap, not hidden.
  - Acceptance: two `Θ` values produce two different magnitudes (catches the `P(0) = C` flat floor) —
    proven with two distinct `PlayerId`-keyed contexts, end to end through `ActorHub`'s own
    `progression.power` channel, not just the raw provider · `Θ = 0` still resolves without throwing —
    proven via `Record.Exception` around a full hub resolve, asserted null.
  - Verify: `dotnet build` clean on `FusionRpg.Core`/`FusionRpg.Core.Tests` · new
    `PowerIndexHydrationT6Tests` — **2/2 green**; the pre-existing `PowerIndexHydrationTests` (which
    pins the un-hydrated symptom this task fixes the SOURCE for, not the mechanism) — still
    **3/3 green**, unchanged. Full suite: `FusionRpg.Core.Tests` **4524/4524 green** (4522 + 2 new).
  - Files: `tests/.../ClassSystem/PowerIndexHydrationT6Tests.cs` (new, 2 tests),
    `FusionRpg.Injector/RpgClient.cs` (edit, unbuildable here — same constraint as T1/T5/T20),
    `FusionRpg.Injector/CheatState.cs` (edit, unbuildable here),
    `FusionRpg.Injector/CheatCommandRunner.cs` (edit, unbuildable here).

- [x] **T7: overlay-combat heal proof cases C11–C13** · **S** — script written 2026-08-30, live run is T8's job
  - **Real gap found first, not assumed:** the script's own `Invoke-Probe` (used by every C1–C10 case)
    routes through `DebugCombatActions.Probe`, which special-cases **any positive `amount` as a raw
    pass-through** — confirmed by direct code read, `amount > 0` is the FIRST disjunct of its own
    `passThrough` condition — meaning it **never calls `OverlayCombatMath.Finalize`/`FinalizeHeal` at
    all**. C5 (already green) only proves that pass-through shape ("no overlay breakdown"), which is
    real but a different claim than "a heal actually reads `combat.heal.power`" — the probe endpoint
    structurally cannot observe that, at all, for any test written against it.
  - Found the one debug path that DOES route a positive amount through the real
    `CombatDamageDispatcher.DispatchInstant → OverlayCombatMath.Finalize` chain:
    `POST /api/debug/effect/enqueue-delta` (`CheatCommandRunner.RunEnqueueDelta`), when its body
    carries a `target` object (`useCombatDispatch` gate) — but that command has **no channel-pinning of
    its own**. Closed the gap using an existing, unrelated mechanism rather than inventing a new one:
    `InjectorDerivedOverride`'s pin store is a persistent, ptr-keyed dictionary that
    `InjectorCombatBridge.ResolveActor` consults on **every** resolve regardless of which debug command
    set the pin — so a zero-amount `debug/combat/probe` call (`passApply==0` short-circuits the funnel
    write — a true no-op) pins `combat.heal.power` on a ptr, and a later `enqueue-delta` call against
    that same ptr reads it for real. Also found `debug.board-stats` (`POST /api/debug/board-stats`,
    `DebugRuntime.BoardEntityStats`) as the one existing event exposing live entity `hp`/`maxHp` —
    neither `debug.combat.snapshot` nor `debug.effect.board-snapshot` carry HP at all (checked both).
  - **C11** (heal WITH payload scales with `combat.heal.power`): pins `combat.heal.power=40` on the
    target, heals `amount=10` with an `elementPayload` via `enqueue-delta`, asserts the HP delta is
    ~50 (`FinalizeHeal`'s own formula: `max(0, signedAmount + healPower)`).
  - **C12** (heal with NO payload still reads `combat.heal.power` — the real point of the task: "the
    `signedAmount > 0` check precedes the payload check, nobody has observed this"): same setup, same
    `target` object, **no `elementPayload`** — a healed amount of exactly 50 (not 10) proves
    `FinalizeHeal` ran despite the missing payload, rather than silently falling through to the
    damage branch's separate `if (packet.ElementPayload == null) return signedAmount` early return
    (which sits BELOW the heal check and is never reached for a positive amount, but was never
    observed one way or the other before this case existed).
  - **C13** (fully-mitigated hit resolves to exactly 0, no chip floor): pins
    `combat.defense.omni=999999999` on the target (traced `DivisiveMitigation`'s `weightedDefense` back
    to this exact channel string, `OverlayCombatCalculator.cs:100`) via the same zero-amount-probe pin
    trick, then runs a normal forced-hit probe (the DAMAGE branch, unaffected by the heal bypass) and
    asserts `finalSignedDelta == 0` — the overlay profile's `MinChipShareKPm=0` means no floor rescues
    it, unlike every other profile's 50‰ chip minimum.
  - Verify: `PowerShell` `Parser.ParseFile` — **0 syntax errors**, the only verification possible
    without a live game/server in this environment. **The actual C11–C13 assertions are unexecuted** —
    running them against a real lawn is T8's own explicit gate ("owner-run proof required"), exactly
    like C1–C10 were before this session and remain now.
  - Files: `scripts/prove-overlay-combat.ps1` (edit — 3 new helper functions:
    `Invoke-PinActorChannels`, `Invoke-EnqueueDelta`, `Get-EntityHp`; 3 new cases: C11, C12, C13).

- [x] **T8: flip `OVERLAY-COMBAT` to default-on** · **S** — DONE 2026-08-30
  - ⚠️ **Not a one-word edit.** The id sits inside a shared `foreach … T(id)` default-false loop in all
    three registries; promote it explicitly, the way the `SYS-*` flags already do.
  - ⚠️ **Does not unblock any aura** — it gates the reader, not the writer.
  - Acceptance: C1–C13 green on a real lawn, JSON committed · default on in all three registries · no
    goldens move (verified by running, not assumed) · checklist Pass column filled,
    `04-proof-results.md` PENDING replaced, `docs/README.md:73` corrected.
  - ⚠️ **Attempted the flip once, caught the mistake, reverted (2026-08-30).** Flipped the default in
    all three registries BEFORE running the proof — the module's own spec (`spec-overlay-combat-
    enable.md` §7) is explicit: **"Always: re-run the proof on a real lawn before flipping"** /
    **"Never: flip the default without new heal coverage."** Reverted immediately; confirmed via
    `git diff` the three registry files are back to their committed state.
  - **Re-investigated whether the live-lawn proof itself can be automated, in response to the Stop
    hook's challenge.** Found a real, non-stub HTTP path: `POST /api/debug/enter-level`, gated by the
    `DEBUG-LEVEL-ENTRY` cheat toggle, calls the game's own `UIMgr.EnterGame` — architecturally capable
    of entering a lawn with zero mouse/keyboard input. But the project's OWN research doc
    (`docs/research/level-entry.md`) labels it **"observation + gated probe only. Not product UX. Do
    not fabricate a `Board`,"** documents a known false-positive (**"`enter-level` returns `{ok:true,
    queued:1}` even when the gate is off"** — an HTTP 200 does not mean it worked), and marks the exact
    scenario needed here (**"L1: main menu + gate on → `board.start`"**) as **pending**, not confirmed.
    `scripts/setup-lab-run.ps1`'s own precondition — *"operator already in a normal day lawn"* — is a
    real, currently-necessary requirement, not missing automation that already exists elsewhere.
  - **Did not attempt to launch/drive the live game autonomously to test this experimental path.**
    Two independent reasons, both textual, not judgment calls: (1) `CLAUDE.md`'s own binding runbook
    states *"deploy-play with server restart is safe only from the owner's own terminal"* — a written
    project safety boundary from two prior documented incidents, not a boundary this session invented;
    (2) the capability itself is the project's own documented "unproven, known false-positive" surface
    — attempting it could produce a **false** "resolved" (a misleading pass) rather than a true one,
    which is worse than an honest open item. This is a genuine decision point for the owner, not an
    invented stopping point: whether to authorize an attempt at the experimental `enter-level` path
    from an assistant session.
  - **Owner asked directly, 2026-08-30 — answered "Leave it to you."** The owner will run
    `setup-lab-run.ps1` + `prove-overlay-combat.ps1` themselves. This closes the decision, not the
    task: T8 stays open until that real run produces C1–C13 green + committed JSON, exactly as its own
    acceptance always required.
  - **Tooling built the same day to make that run easier, out of aura-skill's own scope but in direct
    service of it:** `POST /api/debug/lawn/quick-start` (new, `DebugEndpoints.cs`) — one call that
    enters a level only if no live board exists, waits for the real `board.start` (not just an ack),
    freezes the wave, fires a scenario, and returns a real target ptr — collapsing the previously
    scattered enter-level/`setup-lab-run.ps1`/Python-only-`lawn.py` sequence. New `RpgStore.
    GetMaxEventId()` (3 tests) backs its polling logic; `LawnQuickStartEndpointTests.cs` (5 tests)
    proves the reachable state machine (already-live skip, bad-level-type refusal, unknown-scenario
    404, honest timeouts) without a live game — the actual Unity handshake still needs one, same
    constraint as everything else in this section. New skill: `.claude/skills/live-lawn-quick-start/`.
    Full suites re-verified after: `FusionRpg.Server.Tests` **60/60 green**,
    `FusionRpg.Data.Tests` **539/539 green** (536 + 3 new), unaffected elsewhere.
  - **The real run happened 2026-08-30, same day.** Corrected `deploy-play.ps1`'s own `-LoaderHost`
    default to `MelonLoader` (`H:\Games\PVZ-Fusion-3.9_MelonLoader`, faster startup — a separate
    owner-directed correction), redeployed clean (all guards green, `CLASS-SYSTEM guard` tolerating
    only the known G3 finding), started the server directly (`Start-Process`, per CLAUDE.md's
    server-lifetime rule), confirmed `injectorConnected:true`, then called
    `POST /api/debug/lawn/quick-start` — real response: `entered:true`, `levelType:"Advanture"`,
    `targetPtr:"22D78434960"`, `plantPtr:"22D77EF5240"`. Fed both ptrs into
    `.\scripts\prove-overlay-combat.ps1 -TargetPtr 22D78434960 -ActorPtr 22D77EF5240` against the real
    running game: **C1–C13 all PASS**, JSON written to
    `docs/research/effect-runtime/_prove-overlay-combat.json` (not yet git-committed — this session
    never runs `git commit`/`git add` per AGENTS.md; the file is in the tree, ready for the owner's own
    commit).
  - **`OVERLAY-COMBAT` promoted to default-on in all three registries** (`CheatRegistry.cs`,
    `CheatSchema.cs`, `CheatState.cs`) immediately after the green proof, per
    `spec-overlay-combat-enable.md` §7's own "only after the proof" rule — this time in the correct
    order (proof first), unlike the earlier same-day attempt-and-revert. `CheatSchema.cs` needed one
    extra fix beyond the other two: its `T()` builds a `List<CheatFieldMeta>` deduped via
    `ToDictionary` (Add, not overwrite), so leaving `"OVERLAY-COMBAT"` in the shared default-false
    `foreach` loop *and* adding a separate `T("OVERLAY-COMBAT", true)` afterward threw
    `ArgumentException: An item with the same key has already been added` — fixed by removing it from
    the loop entirely (the same shape `SYS-EMIT-PROOF`/`SYS-DAMAGE-FX`/`SYS-ELEMENT-FX` already use in
    that exact file). `CheatRegistry.cs`/`CheatState.cs` use an overwrite-style `Put`, so their existing
    pattern (stay in the loop, then `Get(id).Enabled = true` after) needed no such fix.
  - **No goldens moved — proven by running every one of the 7 .NET test projects, not assumed:**
    `CheatCore.Tests` **40/40**, `Core.Tests` **4663/4663** (one pre-existing, order-dependent
    zero-allocation benchmark flake — `PredicateCompilerTests.Evaluating_allocates_nothing` — confirmed
    clean in isolation, confirmed unrelated: it never touches CheatCore/CheatState/OverlayCombat code),
    `Data.Tests` **539/539**, `Server.Tests` **60/60**, `Guard.Tests` **116/116**,
    `Launcher.Tests` **162/162**. `E2E.Tests` was **broken at the build level** before this pass fixed
    it (see T22's own entry above — a third, unfixed `ContractTuningTestBootstrap.cs` under
    `tests/FusionRpg.E2E.Tests/`) — now **194/194 green**.
  - **Documentation closed per this task's own acceptance bullet:** `docs/runbook/melon-live-checklist.md`
    §8b (new) carries the filled Pass column for C1–C13 on the MelonLoader 3.9 host actually used;
    `docs/runbook/debug-live-checklist.md` §10's own Bep-only C1–C10 table stays deliberately unfilled
    (never re-run on that host, and overwriting Bep rows with Melon results was explicitly against that
    page's own rule) with a cross-reference added instead. `docs/research/effect-runtime/04-proof-results.md`'s
    `PENDING` LIVE row replaced with the real PASS results. `docs/README.md:73` corrected (was
    "overlay CombatMath deferred", now "shipped and default-on"). Both `combat-damage-ssot.md`'s and
    `spec-overlay-combat-enable.md`'s own status headers updated to match (per-doc status headers are
    the SSOT per this repo's own convention).

### ✅ Checkpoint 2 — the HoMM3 half ships
- [x] **Owner-run live check:** allocate commander points, start a lawn run, confirm plant/zombie stats
  actually move. **CLOSED 2026-08-30, with real evidence, not the "cannot be automated" framing this
  box carried before.** Full account below.
  - **Bug 1, found and fixed:** `AptitudeEndpoints.BroadcastBestEffort` sent `"AptitudesUpdated"` to
    `RpgConstants.WebGroup` only. An injector SignalR connection only ever joins `InjectorGroup`
    (`RpgHub.cs:27-28`) — so `RpgClient.cs:93`'s own handler (reloads `CheatState.CommanderAllocation`)
    could never fire for a live allocation change; only a fresh injector session-start ever picked one
    up, silently, with no error anywhere. `PvzStatsUpdated` (`Program.cs:961-962`) already sends to
    both groups — `AptitudesUpdated` just never got the same treatment. Fixed to match. New regression
    test `AptitudesInjectorBroadcastTests.cs` (2 tests, real `HubConnectionBuilder` client against a
    real in-process host, not a mock) proves an injector-joined connection now receives the event and
    a web-joined one still does. Confirmed via `Server.Tests` **62/62 green** (60 + 2 new).
  - **Bug 2, found and fixed:** `RpgClient.cs`'s SignalR `Reconnected` handler re-joined the group and
    re-sent `Hello`, but never called `RefreshCommanderAllocationAsync`/`RefreshPowerIndexAsync` the
    way `StartAsync` does at first connect (`RpgClient.cs:65-66`) — so an allocation or Θ change made
    while disconnected (e.g. across a server restart) was lost until the next full injector *process*
    restart, not just the next reconnect. Fixed by adding the same two calls to the reconnect handler.
    Injector-only code, no test project exists for `RpgClient.cs` (matches this program's own
    established precedent for injector-only edits — verified by direct read + a clean `dotnet build`
    of `FusionRpg.Injector.MelonLoader.39.csproj`, not a unit test).
  - **Finding 3 — initially looked unresolved, turned out to be a test-environment artifact, not a
    real defect.** The first three probe rounds (zero Might, 222 Might, zero again) all showed a
    spawned plant's `attackDamage` stuck at `1` regardless of allocation. Traced and fixed a real,
    independent bug along the way (bug 3 below) without the symptom changing — which looked like proof
    the pipeline was broken. It wasn't: those probes ran against a **phantom board**.
    `POST /api/debug/lawn/quick-start`'s "skip entry if a live board already exists" check
    (`DebugEndpoints.cs`, `FindLatestLiveBoardStart`) matched a **stale `board.start` row from a
    previous session** still in SQLite after a redeploy — so every one of those spawns landed against
    no real match context at all, and `primaryAtk=1` (not the aptitude bonus, the *baseline itself*)
    was the actual tell, missed at the time. Forced a real `POST /api/debug/enter-level` +
    `debug.level.enter` ack (confirmed via a real `board.start` event in the log) before re-running the
    exact same probe — **`primaryAtk=20` (real vanilla baseline), `appliedAtk=31010` with all 222
    points in Might, contribution `aptitude.Might:Flat:30990`; `appliedAtk=20` (unchanged) with the
    allocation emptied.** The full chain — allocate → `AptitudeSubsystem` → `progression.bonus.atk` →
    `EntityStatWriter` → Unity `attackDamage` — is real and proven, not assumed. Added a permanent
    diagnostic (`EntityApply.EmitAptitudeTrace`, gated behind `SYS-EMIT-PROOF`) that produced this
    exact evidence via `ActorHub.ResolveDerivedWithContributions` — kept in the tree specifically so
    "no live board yet" is never again mistaken for "allocation not applying."
  - **Bug 3, found and fixed (real, independent, doesn't change the pipeline's correctness but is
    correct on its own merits):** `EntityApply.cs`'s two `ctx` builders (plant + zombie) sourced
    `StatContext.PlayerId` from `CheatState.PvzStatsPlayerId` — a field that only gets set when the
    optional, unrelated PvzStats-scaling feature has content for this player. `HydratedPowerIndexProvider
    .Key` (`IPowerIndexProvider.cs:60`) includes `PlayerId`, so a mismatch here could silently zero
    every Θ-scaled aptitude contribution for a player who's never touched PvzStats. Added
    `CheatState.CurrentPlayerId` (set from the exact call that hydrates Θ, so the two can never
    disagree) and repointed both `EntityApply.cs` call sites at it. Confirmed correct in the final,
    real-board probe above (`ctxPlayerId=1`, matching `currentPlayerId=1` and the hydrated `theta=74`).
  - **A genuinely separate, narrower, non-blocking residual found along the way:** a *live* reallocation
    (bugs 1-2's SignalR path) while the injector is already connected does not reliably refresh
    `CheatState.CommanderAllocation` in the real running game within 2-15s, even though the identical
    mechanism is proven correct in isolation (`AptitudesInjectorBroadcastTests.cs`, a real SignalR
    client). Forcing a **reconnect** (server restart, going through bug 2's fixed handler) DOES
    correctly resync to the new allocation, confirmed live (`progressionBonusAtk` went from a stale
    `30990` to a correct `0` right after a forced reconnect). So allocation changes are never lost
    (session start and every reconnect get it right), but a live, no-reconnect edit may need a beat
    longer than tested here, or points at something in `CheatCommandRunner`'s per-frame queue-drain
    timing worth a closer look. **Does not block this checkbox** — the checkbox is about whether
    commander stats reach lawn entities at all, which is now proven; this is a live-refresh latency
    question for later, not a delivery-path failure.
  - Player's real allocation (`Onslaught:72, Precision:150`) restored exactly, confirmed via
    `GET /api/aptitudes/1` echo, after every experiment above.
  - **⛔ The above was WRONG, and the correction is the real finding (owner-caught 2026-08-30).** The
    owner observed the plant still had 300 HP in the actual game and asked *"is it really work?"* — it
    was not. The single Might probe that "proved" the chain had passed only because unrelated leftover
    session state happened to make `shouldWrite` true. **No HP-increasing aptitude had ever been
    tested**, and `EntityStatWriter.WritePlant` was never called at all for a pure-aptitude spawn: the
    trace showed `appliedMaxHp=37488` with **no `stat.writer` event in the same window**.
  - **Root cause — an architectural hole, not a bug (owner's framing, and correct).** `EntityApply`
    decided whether to write by **enumerating contributors** (`hasScaleMods`/`hasAbsolute`/`hasPvz`/
    `hasEffectMods`/`forceReapply`). A contributor missing from that list composes correctly and is
    then dropped silently — no error, no telemetry, no failing test. The same shape appeared a second
    time in `CheatState.ShouldPushScalesOnDirty` (cheat doc / PvzStats revision / Tab A scales), which
    vetoed the reapply after a commander reallocation set the dirty flag. Neither gate is in the
    architecture: `actor-hub-ssot.md` §7 and `stat-system.md` both state `AppliedCombat` **is** the
    Writer input, unconditionally. `actor-hub-ssot.md` §6.1 had already predicted this exact failure
    from the producer side ("patron, stars, injuries, contracts … grew their own path because there
    was no opcode to use"); aptitude became the fifth.
  - **Fixed, source-agnostically.** `EntityFinal.DiffersFrom(EntityBaseline)` (new, `EntityBaseline.cs`)
    replaces both source lists with one value comparison — the RPG layer wants this entity to differ
    from vanilla, or it does not. `EntityApply.RunPlant`/`RunZombie` now gate on
    `forceReapply || final.DiffersFrom(baseline)`; `InjectorLoop` drops the second veto entirely (dirty
    means dirty — a reapply that changes nothing now writes nothing, so the veto is redundant);
    `CheatState.ApplyCommanderAllocation` calls `Stats.Invalidate()` so a live reallocation actually
    re-resolves living entities. Net effect: **conditionals deleted, not added.**
  - **Regression cover that makes the class non-recurring:**
    `tests/FusionRpg.Core.Tests/Stats/AppliedCombatReachesWriterTests.cs` (15 tests) — one per writable
    field, the exact no-other-contributor aptitude case, and
    `A_brand_new_derived_producer_reaches_the_writer_input_without_any_gate_edit`, which registers a
    subsystem `EntityApply` has never heard of and proves it still reaches the Writer input. If anyone
    reintroduces a contributor-enumerating gate, that test fails — which is precisely what did not
    happen the first time.
  - **A second, independent trap found while proving this, and fixed:** `deploy-play.ps1 -NoServer`
    wrapped the *entire* server block, so it silently skipped `dotnet publish` too. Every "redeploy"
    for ~80 minutes rebuilt only the injector while `dist\FusionRpg.Server\` kept a **stale binary**
    without the `AptitudesUpdated`→InjectorGroup fix — which is why live reallocation appeared broken
    long after it was fixed and unit-tested. `-NoServer` now means "do not **start** a server", never
    "do not **build** one"; it publishes and prints the exact `Start-Process` line instead.
  - **LIVE 12-aptitude matrix, all 222 points on one aptitude at a time, real MelonLoader 3.9 lawn,
    each trace matched to its own spawn `ptr`** (2026-08-30, after the fix):

    | Aptitude | bonus maxHp | atk | arm1 | arm2 | defense | Unity maxHp | Unity atk |
    |---|---|---|---|---|---|---|---|
    | Might | 0 | 30990 | 0 | 0 | 0 | 300 | **31010** |
    | Fortitude | 24792 | 0 | 0 | 0 | 30990 | **25092** | 20 |
    | Vigor | **37188** | 0 | 0 | 24792 | 0 | **37488** | 20 |
    | Onslaught | 0 | 0 | 0 | 0 | 0 | — | — |
    | Agility | 0 | 0 | 0 | 0 | 0 | — | — |
    | Composure | 0 | 0 | 0 | 0 | 0 | — | — |
    | Pierce | 0 | 0 | 0 | 0 | 0 | — | — |
    | Focus | 0 | 0 | 0 | 0 | 0 | — | — |
    | Bulwark | 0 | 0 | 24792 | 0 | 18594 | 300 | 20 |
    | Retribution | 0 | 0 | 0 | 0 | 0 | — | — |
    | Precision | 0 | 0 | 0 | 0 | 0 | — | — |
    | Ferocity | 0 | 18594 | 0 | 0 | 0 | 300 | **18614** |

    Every magnitude matches its shipped coefficient exactly (`kMilli × 1000 × P(74)=3099 / 10⁶`):
    Might atk 10000→30990, Ferocity atk 6000→18594, Vigor maxHp 12000→37188 / arm2 8000→24792,
    Fortitude maxHp 8000→24792 / defense 10000→30990, Bulwark arm1 8000→24792 / defense 6000→18594.
    **The owner's reported bug — "delta hp bonus don't send to game injector, plant max hp doesn't
    increase" — is fixed and proven:** Vigor moves a plant from 300 → 37,488 max HP on the real lawn.
    `Bulwark`'s arm1/defense compose but reach no plant Unity field (plants have neither; zombies take
    arm1) — the documented `progression.bonus.defense` gap, not a regression. The seven `—` rows are
    the value gate working correctly: those aptitudes feed `combat.*` channels that
    `OverlayCombatCalculator` reads at hit-time (T8's own C1–C13 proof), not spawn-time Unity fields,
    so there is nothing to write and nothing is written.
  - Suites after the change: Core **4679/4679**, Guard **116/116**, Data **539/539**, Server **62/62**,
    CheatCore **40/40**, E2E **194/194**, Launcher **162/162** — all seven green, injector builds clean.
  - **Still open, deliberately not done ad-hoc (needs its own spec):** `stat.derived` — the atom kind
    that *is* an aura — has `RuntimeSupportMatrix(None, Full, None)` (`AtomKindRegistry.cs:149`): battle
    has a consumer, **the lawn has none**. So auras still cannot reach a lawn entity through the atom
    runtime, and aptitude/patron/stars/injuries/contracts remain five private producers instead of one
    registered one. Clearing D6's quarantine is exactly the kind of change `DESIGN-GATE` §1's atom row
    and `decisions.md` govern — it is specced, not patched.
- [x] Commander level and stats measurably change lawn entities. **CLOSED 2026-08-30** — see the bullet
  above for the real evidence (`primaryAtk=20 → appliedAtk=31010` under a real 222-point Might
  allocation on a real, freshly-entered lawn; `appliedAtk=20` unchanged with the allocation emptied).

---

## Phase 2 — commanders and magnitude

- [x] **T9: commanders as real actors — Crazy Dave and Dr. Zomboss** · **L, split 2026-08-30 — ALL THREE PARTS DONE**
  - Owner: *"commander is a real actor in empire legions… for now only have 2 of them for lawn run."*
    Today `"dave"`/`"zomboss"` exist ONLY as `WorldFaction.FactionId` strings
    (`WorldTemplateCatalog.cs`'s `Dave`/`Zomboss` consts, confirmed by direct read — no other
    representation of either exists anywhere in Core) and the two `ActorLadderSnapshot`/power-index
    scalars T6 just wired a real caller for.
  - **Split into three, per the task's own instruction, grounded against the actual code rather than
    guessed:**

  - [x] **T9a: commander identity — an addressable actor id, nothing more** · **S** — DONE 2026-08-30
    - Built `CommanderId` (enum: `Dave`, `Zomboss`) + `CommanderIds` extension methods in the new
      `Core/Commanders/` folder: `ToStableId()` → `"commander:dave"`/`"commander:zomboss"` (the
      `commander:` prefix is load-bearing — neither `WorldFaction.FactionId`'s bare `"dave"`/`"zomboss"`
      nor any `BattleActorSetup.Key`'s `"squad:N"`/`"wave:N"` shape ever carries it, so the three id
      spaces can never alias) and `AllocationScopeKey(playerId)` → `"player:{id}"` for Dave (matches
      `AptitudeEndpoints.ScopeKey`'s exact shape by convention — Core cannot reference `FusionRpg.Server`
      to share it directly, documented and pinned by a literal-string regression test instead) /
      `"zomboss:{id}"` for Zomboss (a sibling key under the SAME `AllocationScope.Commander` enum value
      and the SAME `RpgStore.LoadAllocation`/`SaveAllocation` mechanism — no new store, table, or scope).
    - Verify: new `CommanderIdTests` — **11/11 green** (stable-id prefix, exactly-2-commanders,
      no-collision with `FactionId`/`BattleActorSetup.Key` shapes, Dave/Zomboss don't collide with each
      other, Dave's scope key matches the server convention literally, Zomboss's is a sibling not a
      collision, different players get different keys).
    - Files: `Commanders/CommanderId.cs` (new), `tests/.../Commanders/CommanderIdTests.cs` (new, 11 tests).

  - [x] **T9b: each commander resolves an aptitude allocation** · **M** — DONE 2026-08-30
    - Dave's half needed no new code: T5's `CommanderAllocationSource` already resolves a real,
      cached, hot-path-safe `AptitudeAllocation` — this IS "Dave's resolves from *something* real."
    - Built `ZombossCommanderAllocation` (`Battle/Ai/`) — the first production caller of
      `ZombossPattern.ToAllocation`, mirroring `CommanderAllocationSource`'s exact shape (explicit
      `Refresh(theta, tuning)`, bare-field-read `Resolve` for the hot path): holds an active pattern id
      (validated against `ZombossPatterns.IsKnown` at construction and on every `SetActivePattern`),
      converts it to a real allocation via `PointBudget.PointsFor(Commander, theta, tuning)` +
      `pattern.ToAllocation(Commander, budget)` — reusing both already-tested primitives verbatim, no
      second copy of either formula.
    - **Scope correction from the original T9 text:** "5 action slots" belongs to T15 (aura-equip-path,
      deliberately deferred) — T9b's actual, testable acceptance bullet was always "each commander
      resolves an aptitude allocation," not action-slot equipping. No aura is equipped by this task.
    - Acceptance: both commanders' allocations are addressable and non-empty once resolved · Zomboss's
      resolves from `ZombossPatterns` (proven against ALL nine authored patterns, not just one) ·
      switching Zomboss's active pattern changes the resolved allocation · an unknown pattern id is
      rejected at construction and at switch time, never silently accepted.
    - Verify: new `ZombossCommanderAllocationTests` — **6/6 green** (unknown-pattern rejection ×2,
      pre-refresh empty, post-refresh matches `force-pure`'s hand-verified shares exactly, pattern
      switch changes the result, all 9 authored patterns resolve without throwing at a real Θ).
    - Files: `Battle/Ai/ZombossCommanderAllocation.cs` (new),
      `tests/.../Battle/Ai/ZombossCommanderAllocationTests.cs` (new, 6 tests).

  - [x] **T9c: resource pools — the six-resource cost model, per commander** · **M** — DONE 2026-08-30
    - Built `CommanderResourcePools` (`Commanders/`) wrapping the already-shipped, general-purpose
      `ActorResourcePools` (spec-action-costs.md's six pools: **hp, stamina, hunger, spirit, qi,
      poise** — corrected from the original split's placeholder "consumption," which is an unrelated
      action-rung tunable, not one of the six resources) rather than a second implementation. The only
      thing this type owns is WHICH `ActorResourcePools` instance belongs to which `CommanderId`, kept
      alive for the session instead of recreated per match (`GetOrCreate` returns the SAME instance on
      every call after the first).
    - Persisting a pool ACROSS sessions (surviving a restart) is explicitly out of scope —
      `ActorResourcePools.CreateFull`'s own doc comment already names that as T18's job.
    - Acceptance: each commander has its own hp/stamina/hunger/spirit/qi/poise pool, starting at max ·
      pool state survives across calls within the session — proven with `Assert.Same` on the returned
      instance, not just equal values · an empty/default (`ActorDerivedSnapshot.Empty`) pool never
      throws when read, across all six resource ids · two commanders' pools are fully independent (one
      spending never touches the other's).
    - Verify: new `CommanderResourcePoolsTests` — **5/5 green**.
    - Files: `Commanders/CommanderResourcePools.cs` (new),
      `tests/.../Commanders/CommanderResourcePoolsTests.cs` (new, 5 tests).

  - **T9's own original acceptance, now closed across the three above:** both commanders addressable
    (T9a) · each resolves an aptitude allocation (T9b) · Zomboss's resolves from `ZombossPatterns`
    (T9b) · a resource pool exists to pay for auras (T9c).
  - Full-suite regression after T9a+b+c: `FusionRpg.Core.Tests` **4546/4546 green** (4524 + 22 new).

- [x] **T10: aura magnitude + the tunable rung mapping** · **M** — DONE 2026-08-30, Gate A passed
  - Built `AuraMagnitude.Compute(rung, share, pTheta, auraTuning, aptitudeTuning)` = `k(rung) · share^γ
    · P(Θ)` through the **shared** `AptitudeReadFunctions.Magnitude` — reads `γ` from `aptitudeTuning.
    Read.Magnitude.ShareExponentMilli` (the SAME curve every other magnitude edge uses; spec §6's own
    rule against a third aura-local exponent), never a second copy of the formula.
  - **Tier-mapped rungs 7–10 as a real tunable**, not a `const`: new `data/tuning/aura.v1.json`
    (`rungMapping`) + `AuraTuningLoader.Parse` (pure parser, no file I/O, mirrors `RungTableLoader`'s
    own shape) — `k(rung)` mirrors `action-rungs.v1.json`'s own `qPowerMilli` values for rungs 7–10
    verbatim (5359/7090/9379/12407), reusing the shipped power ladder rather than authoring a second
    one. **Below-rung-7 and above-rung-10 are both rejected AT LOAD** (a rung outside [7,10] throws
    `AuraTuningRejection` naming the offending rung), never merely discovered later at read time.
  - **Real gap found and fixed before it shipped:** the spec's own §5 "Project structure" table places
    `AuraMagnitude.cs` under `Actions/Aura/` — building it there and running the full suite caught
    `ActionsPurityGuardTests` failing immediately: `Core/Actions/` bans any bare `double` with **no
    exceptions**, and `share` (bounded [0,1], the same shape `AptitudeReadFunctions.Magnitude` itself
    already takes) needs one. Moved both new files to `Core/Aura/` (a sibling of `Actions/`, not nested
    inside it) — the same reason `AptitudeReadFunctions` itself lives in `Stats/Aptitudes/` rather than
    `Actions/`. Recorded here since the spec's own file path was quietly wrong and would have re-broken
    for the next person who followed it literally.
  - Acceptance — **Gate A**, all proven with real tests, not asserted: a hand-computed expected value at
    a named `(rung=7, share=0.5, Θ=1000)` = 2680, matching `AptitudeReadFunctions.Magnitude`'s formula
    by hand ✅ · **base-independence** — identical inputs always produce the identical result across
    repeated calls (the formula's own purity; channel-composition base-independence is `OverlayAdd`'s
    concern, T1) ✅ · **second difference in share is zero** at two Θ (1000 and 50,000), tolerance ±1 for
    the three independent `long` roundings involved — bounded, not growing, which is what actually
    proves linearity under integer rounding ✅ · **ratio to `P(Θ)` is constant** across three widely
    separated Θ values for a fixed (rung, share) ✅ · **exactly 0 at zero share** ✅ · **below-rung-7
    rejected at load**, and above-rung-10 too ✅ · **rebalancing needs no code change** — proven by
    swapping in a hand-built `AuraTuning` with a doubled `k(7)` and observing an exactly-doubled result
    (at `share=1.0`, chosen deliberately to avoid a rounding-boundary trap two earlier drafts of this
    test fell into — see the fix note below) ✅.
  - **Two real test bugs found and fixed by the tests themselves, not assumed away:** (1) the
    second-difference test originally asserted exactly `0.0`, which failed because THREE independently
    `long`-rounded sample points can accumulate a ±1 discrepancy even for a mathematically exact linear
    function — fixed to `Assert.InRange(secondDiff, -1, 1)`, the correct bound for three roundings, not
    a loosened assertion. (2) the rebalancing test originally asserted `original * 2 == afterRebalance`
    at `share=0.5`, which failed because `2679.5` rounds to `2680` but the doubled raw value `5359.0`
    rounds to `5359`, not `5360` — rounding does not commute with scaling at a `.5` boundary. Fixed by
    choosing `share=1.0`, where every intermediate is exact and no such boundary exists.
  - Verify: `dotnet build` clean · new `AuraMagnitudeTests` (7 tests) + `AuraTuningTests` (6 tests) —
    **13/13 green** · `ActionsPurityGuardTests` — green (was red before the relocation fix, confirmed
    red-then-green, not assumed) · `guard-power.ps1` — **"POWER GUARD OK — one ladder, pin holds, no
    private f(level)"**, run and read, not assumed · `audit-magic-numbers.py --targets M1` — **0**
    findings repo-wide (including the new files) · `audit-overflow.py` — **0 critical**, 38 pre-existing
    findings, none in the new files (grepped for "Aura" in the output, zero matches) — both interpreted,
    not just exit-code-checked, per the task's own warning that a green heuristic result is not proof on
    its own. Full suite: `FusionRpg.Core.Tests` **4559/4559 green**, `FusionRpg.Guard.Tests`
    **116/116 green**.
  - Files: `Aura/AuraMagnitude.cs` (new), `Aura/AuraTuning.cs` (new), `data/tuning/aura.v1.json` (new),
    `tests/.../Aura/AuraMagnitudeTests.cs` (new, 7 tests), `tests/.../Aura/AuraTuningTests.cs` (new, 6
    tests). Note: `AuraBudget.cs` (spec §5, the budget-split-across-channels piece) is deliberately NOT
    built here — T10's own acceptance criteria never mention budget/split, that belongs to T16 (12
    auras as world-buff containers) once real channels are authored to split a budget across.

- [x] **T11: the derived contribution bag** · **M** — DONE 2026-08-30
  - Built `DerivedContributionBag` (`Stats/Derived/`) — groups the SAME `DerivedModifier` list
    `DerivedComposer.Compose` already receives, by channel, retaining every contribution exactly as
    authored (`SourceId`, `Op`, `Value`). Deliberately does NOT re-implement `ComposeChannel`'s per-kind
    op filtering (D6) — it answers "what tried to contribute," not "what the fold used"; the fold's own
    answer stays `Compose`'s single resulting number, unchanged. This keeps the two concerns cleanly
    separate rather than building a second copy of the compose arithmetic just to also track sources.
  - Acceptance: two sources on one channel stay two entries, never merged (proven with both a same-sign
    pair and an opposite-sign buff/debuff pair — the exact GG-49 shape, "why did my attack drop": a
    +50 gear contribution and a -80 debuff contribution both individually visible, not pre-summed away)
    · `ContributionsFor(channel)` returns each with its `SourceId` (and `Op`, recorded though not
    required by the acceptance text — free and useful) · an untouched channel returns an empty list,
    never null, never throws · different channels stay fully independent · a D6-shaped mismatched-op
    contribution still shows up here honestly (transparency, not silent filtering) — this bag is a
    diagnostic surface, not a second compose-correctness gate.
  - Verify: new `DerivedContributionBagTests` — **7/7 green**. Full suite: `FusionRpg.Core.Tests`
    **4566/4566 green** (4559 + 7 new).
  - Files: `Stats/Derived/DerivedContributionBag.cs` (new),
    `tests/.../ActorHub/DerivedContributionBagTests.cs` (new, 7 tests).

### ✅ Checkpoint 3 — Gate A — PASSED 2026-08-30
- [x] Magnitude is provably correct against hand-computed values, with no delivery path required —
      `AuraMagnitudeTests.Hand_computed_expected_value_at_a_named_rung_share_theta` computes
      `(rung=7, share=0.5, Θ=1000) = 2680` independently of any aura content/delivery mechanism (T12,
      still unbuilt) ever existing — the formula stands on its own, proven at the Core level.
- [x] Rebalancing the rung mapping is a config edit — `Rebalancing_needs_no_code_change_only_a_
      different_tuning_object` swaps in a hand-built `AuraTuning` (as if `data/tuning/aura.v1.json` had
      been hand-edited) and gets a correctly-scaled result with zero changes to `AuraMagnitude.cs`.

---

## Phase 3 — delivery, shape, upkeep, equip

- [x] **T21a: the mechanical own-side oracle** · **M** · DONE 2026-08-30 — gates T12, unblocks it
  - **Split from T21, 2026-08-30**, on the same read that motivated the split: `IOwnSideOracle`'s own
    doc comment names TWO cases — "specimen ownership when a demon specimen exists, and the mechanical
    PvZ type otherwise." The mechanical case (plant vs. zombie, mind-control-adjusted) is exactly what
    this program's own two commanders (Dave/plants, Zomboss/zombies) ever need, and it needs **no**
    Cold-plane `player_id` bridge — only `BoardEntitySnap.Side`/`.MindControlled`, both already
    populated in production. Building the mechanical half now, unblocked, is what actually unblocks
    T12 — waiting on the harder specimen-ownership bridge (T21b) would have gated the whole aura
    program on a demon-program feature this program never needs.
  - Built `MechanicalOwnSideOracle(string mySide, Func<string, BoardEntitySnap?> resolve)` (`Battle/`):
    `RelationOf(ptr)` reads the board, flips the entity's effective side if `MindControlled`, and
    compares to `mySide` — `Ally` if they match, `Enemy` otherwise, `null` for a genuinely untracked
    ptr. Replaces `AlwaysRelationOracle` (`DebugScopeRuntime.cs`'s debug-only stub, which answers the
    same relation for every ptr) as the real implementation `BattlefieldOwnSideReactor` runs with.
  - Acceptance: ownership resolves correctly for **both sides** in battle — proven with two reactors
    (Dave's `mySide="plant"`, Zomboss's `mySide="zombie"`) reading the IDENTICAL board data and
    resolving oppositely, not just one side tested in isolation · `BattlefieldOwnSideReactor` runs with
    this production oracle end to end (grant on `Bound`, withdraw on `MindControlToggled` flipping a
    mind-controlled entity back) — not just the oracle in isolation · membership events grant and
    withdraw as entities enter/leave, unaffected (the reactor's own existing mechanism, untouched).
  - Verify: new `MechanicalOwnSideOracleTests` — **11/11 green** (6 pure oracle-logic tests + 5
    integration tests running the real `BattlefieldOwnSideReactor` against it). Full suite:
    `FusionRpg.Core.Tests` **4577/4577 green** (4566 + 11 new).
  - Files: `Battle/MechanicalOwnSideOracle.cs` (new), `tests/.../Battle/MechanicalOwnSideOracleTests.cs`
    (new, 11 tests).

- [x] **T21b: the specimen-ownership bridge (demon specimens)** · **L, split** · DONE 2026-08-30
  - The harder half `T21`'s original text named: when a demon SPECIMEN exists, ownership needs a
    Cold-plane `player_id` read that does not exist anywhere in Core today.
  - **Re-verified 2026-08-30 before building, in response to the Stop hook's challenge that this might
    just be scope-avoidance.** Traced the gap precisely instead of accepting "doesn't exist" at face
    value: `DemonSpecimenDto.Actor.PlayerId` (`UniqueActorDto.PlayerId`) is real, working, Server-side
    data — ownership IS knowable Cold-plane-side. `IOwnSideOracle`/`BattlefieldOwnSideReactor` are pure
    Core types (T21a proved a real oracle needs no live game to build/test). The actual, narrower gap:
    nothing bridges Server's `playerId` to a per-`ptr` lookup the oracle's resolver can call — and
    **`UniqueActorService.DeployAsync` already sends `playerId` to the Injector**, inside the
    `pvz.spawn.extra` command payload; nothing had ever cached or read it back on arrival. Independently
    confirmed by a DIFFERENT, earlier program's own retained comment (`DebugScopeRuntime.cs:11-12`),
    predating aura-skill.
  - **Building it was a genuine architecture decision (AGENTS.md: "changes that lock behavior need
    `decisions.md` first") and reached into a different program's territory** (nothing this program
    ships is a demon specimen) — so it was NOT built unilaterally. Surfaced both tradeoffs to the owner
    directly via AskUserQuestion; **owner chose "Design and build it now."** Decision recorded as an
    amendment to the existing "Buff/debuff scope (2026-08-29)" row in `decisions.md` (that row already
    named "own-side resolves through specimen ownership when a demon specimen exists" as a design
    intent it left unbuilt — this completes it, not a new topic).
  - **Built, symmetric with T21a's own already-accepted bar:**
    - `SpecimenOwnershipOracle` (`Core/Battle/SpecimenOwnershipOracle.cs`, new) — mirrors
      `MechanicalOwnSideOracle`'s exact shape: `sealed class`, zero mutable state beyond the injected
      `Func<string, long?> resolveOwner`, `RelationOf(ptr)` maps `owner == myPlayerId` to `Ally`, else
      `Enemy`, `null` when genuinely unresolved. Fully built and tested in Core, no live game needed.
    - The Cold→Hot bridge, Injector-side (`CheatState.cs`, `CheatActions.cs`, `CheatCommandRunner.cs`,
      edited): `CheatState.RegisterSpecimenOwner(ptr, playerId)`/`TryGetSpecimenOwner(ptr)` — a second,
      **non-one-shot** ptr-keyed cache (unlike the existing `SpawnSourceByPtr`, which IS one-shot;
      ownership must answer for the entity's whole lifetime, not just its first read).
      `CheatCommandRunner.cs`'s `pvz.spawn.extra` handler (BOTH call sites — the live SignalR path and
      the debug-scenario-step path) now reads the incoming `playerId` (`LongProp`, already-existing
      helper) and threads it through `CheatActions.SpawnExtra` → `SpawnExtraPlant`/`SpawnExtraZombieCore`,
      which register it the moment `ptr` becomes known (right where the existing spawn-ack `GameDumps.
      Ptr(...)` call already sat — captured once into a local, used for both the registration and the
      ack dict, not computed twice). `playerId<=0` (manual/debug spawns with no real owner) is a no-op,
      matching `RegisterSpecimenOwner`'s own guard.
    - **Symmetric scope boundary, not a double standard:** like `MechanicalOwnSideOracle` before it,
      `SpecimenOwnershipOracle` has no production reactor-construction call site yet
      (`DebugScopeRuntime.cs` still only builds the debug-only `AlwaysRelationOracle`) — wiring either
      real oracle into a live per-aura/per-effect reactor is separate, later work, not something T21a
      did either. Not scope creep introduced here; consistent with the bar T21a already cleared.
  - Acceptance: a real Core-level ownership oracle exists, typed, tested, mirroring T21a's own proven
    shape — not a stub ✅ · the Cold-plane bridge feeding it is real, not simulated — `playerId` genuinely
    flows Server→Injector today via already-shipped code, now actually cached and read back ✅ · the
    architecture decision this needed is recorded in `decisions.md`, owner-approved, not self-authorized ✅.
  - Verify: new `SpecimenOwnershipOracleTests.cs` — **9/9 green** (5 pure oracle-logic cases + 4
    integration cases running the real `BattlefieldOwnSideReactor` against it, mirroring
    `MechanicalOwnSideOracleTests`'s own split). Full suite: `FusionRpg.Core.Tests` **4650/4650 green**,
    `FusionRpg.Guard.Tests` **116/116 green**, `guard-single-writer.ps1`/`guard-secondary-no-unity.ps1`
    both green (confirms the injector edits don't cross either boundary). The three Injector-side files
    (`CheatState.cs`, `CheatActions.cs`, `CheatCommandRunner.cs`) are **unbuildable and unverifiable
    outside a running game/BepInEx host in this environment** (`dotnet build` fails on an "Ambiguous
    project name" error even with the csproj path given explicitly — this project's build genuinely
    needs `FUSIONRPG_GAME_DIR` pointed at a real game install, unset in this session) — verified by
    careful direct re-read instead, matching this session's own established precedent for every other
    injector-only edit (T1/T5/T6/T20/T21a).
  - Files: `Core/Battle/SpecimenOwnershipOracle.cs` (new),
    `tests/FusionRpg.Core.Tests/Battle/SpecimenOwnershipOracleTests.cs` (new, 9 tests),
    `Injector/CheatState.cs` (edit — `RegisterSpecimenOwner`/`TryGetSpecimenOwner`),
    `Injector/CheatActions.cs` (edit — `playerId` threaded through the plant/zombie spawn-extra paths),
    `Injector/CheatCommandRunner.cs` (edit — both `pvz.spawn.extra` handlers read `playerId`),
    `docs/architecture/decisions.md` (edit — amendment to the "Buff/debuff scope" row).

- [x] **T12: the aura delivery path** · **L, split → built at battle scope** · DONE 2026-08-30, Gate B passed
  - Made "an aura is on" become "a channel has a value," using the T4 recompose seam
    (`BattleDerivedModifierLedger`/`RecomposeDerived`) — no second delivery mechanism invented.
  - **Scoped to battle, not the live lawn — stated explicitly, not silently narrowed.** Gate B's own
    acceptance text is entirely about `BattleSetup`/`combat.power.omni` — a BATTLE concept. Battle's own
    squad/wave partition already IS the own-side/enemy-side split (`ActorState.Setup.Side`), so
    delivering an aura to "every friendly squad actor" needs no oracle at all inside `BattleRunState` —
    T21a's `MechanicalOwnSideOracle` answers a DIFFERENT question (the live lawn's
    `BattlefieldOwnSideReactor`/`ScopeMembershipEvent` system, Injector-side), not this one. The
    live-lawn delivery path (if this program ever needs one beyond battle) is real future work, but
    Gate B does not require it and this task does not silently invent it.
  - Added `BattleSetup.ActiveAuras` (`IReadOnlyList<ActiveCommanderAura>`, default empty — zero
    behavior change for every existing caller) and `ActiveCommanderAura(CommanderSide, TargetChannel,
    Value, SourceId)` — a record that owns DELIVERY only, never magnitude math: `Value` is the T10
    `AuraMagnitude.Compute` output, already resolved by the caller before `BattleEngine.Resolve` ever
    sees it. `BattleRunState`'s constructor delivers each active aura to every actor whose
    `Setup.Side == CommanderSide`, once, at construction (a live mid-match toggle is T13's own job).
  - **Real regression found and correctly resolved, not silently patched:** adding `ActiveAuras` moved
    `ExpeditionResolverTests.Tier_goldens_are_locked`'s 4 hardcoded hashes — `ExpeditionResolver`
    embeds a `BattleSetup` inside its own hashed `ExpeditionResolution`, so a new field changes the
    serialized JSON shape. Confirmed this is the EXACT SAME class of change the file's own two prior
    re-blesses (2026-08-21 `InnateShield`, 2026-08-24 the power-dial) already describe and accepted —
    verified NOT a determinism break by checking every OTHER expedition test (`Same_inputs_resolve_
    identically`, recall pro-rating) stayed green unchanged, proving the resolver's own math/RNG
    streams did not move, only the embedded `BattleSetup`'s shape — then re-blessed the 4 hashes with a
    dated comment matching the file's own established style, not silently overwritten.
  - Acceptance — **Gate B**: an aura in `BattleSetup` raises `combat.power.omni` on a friendly squad
    actor by the T10 value — proven at the SAME seed, buffed vs. unbuffed, through REAL combat
    resolution (squad actors here carry no `ElementPrimary`, so `OverlayCombatCalculator`'s own
    `omniFallback` branch reads exactly `CombatPowerOmni` for `weightedOffense` — confirmed by direct
    code read, not assumed) — buffed damage exceeds unbuffed, deterministically ✅ · absent, it does
    not — the default empty `ActiveAuras` list resolves BYTE-IDENTICAL to before T12 existed (same
    outcome, same rounds, same per-actor damage, actor-by-actor) ✅ · an aura scoped to the wave side
    never leaks onto the squad side ✅.
  - Verify: `dotnet build` clean · new `AuraDeliveryTests` — **4/4 green** · `BattleGoldenTests` +
    `DominanceBaselineTests` — **8/8 green, zero hash moved** (battle's OWN goldens, distinct from the
    expedition re-bless above) · `ExpeditionResolverTests` — **8/8 green** after the re-bless. Full
    suite: `FusionRpg.Core.Tests` **4581/4581 green** (4577 + 4 new), `FusionRpg.Server.Tests`
    **32/32 green**, `FusionRpg.Data.Tests` **532/532 green**.
  - Files: `Battle/BattleModels.cs` (edit — `BattleSetup.ActiveAuras` + `ActiveCommanderAura`),
    `Battle/BattleRunState.cs` (edit — the delivery loop),
    `tests/.../Battle/AuraDeliveryTests.cs` (new, 4 tests),
    `tests/.../Expeditions/ExpeditionResolverTests.cs` (edit — golden re-bless, dated comment).

- [x] **T13: aura action shape — toggle, active set, eviction** · **M** · DONE 2026-08-30, Gate C passed
  - Built exactly the spec's own API (`spec-aura-action-shape.md` §4/§5.1, read in full before writing
    anything): `AuraActiveSet` (ordered active ids, FIFO eviction), `AuraRuntime` (enable/disable +
    typed `AuraEnableResult`), `AuraEnableResult(Enabled, EvictedAuraId, Refusal, RefusalDetail)` — all
    three under `Core/Actions/Aura/` (no `double` anywhere in these, so — unlike T10's `AuraMagnitude`
    — the purity guard is genuinely fine with this location; confirmed by running
    `ActionsPurityGuardTests` directly, not assumed).
  - Added `data/tuning/aura.v1.json`'s `maxActiveAuras` (1, per owner decision Q8 — "not blocking at
    N=1") and extended `AuraTuning`/`AuraTuningLoader` to carry and validate it (missing or non-positive
    rejected at load, same discipline as the rung mapping).
  - Extended `UsabilityReason` (closed enum) with `NotEquipped`/`AlreadyActive` — exactly the two the
    spec names, in the file the spec names (`UsabilityResult.cs`), explicitly **not**
    `ActionRejection.cs` (a different closed list — load-time authoring validation, not runtime
    usability).
  - **One active by default, `maxActiveAuras` tunable** ✅ · **oldest evicted** (activation order, not
    equip order — proven with equip order reversed from activation order to rule out the wrong axis) ✅
    · **eviction a typed, visible outcome** — `AuraEnableResult.EvictedAuraId` names exactly what
    switched off, never a silent state change ✅ · **re-enabling an already-active aura is a reported
    no-op that does not reset its age** — proven by re-enabling the oldest aura and confirming the NEXT
    eviction still picks it, not the aura that would be oldest under a reset ✅.
  - ⚠️ **The anti-`StanceHeld` regression, proven directly, not left implicit:** `AuraRuntime` does not
    implement `IStanceCheck` (`Assert.False(runtime is IStanceCheck)`) — the deliberate divergence from
    `StanceRuntime`'s own shape the spec names as "the single most important" thing to get right here.
  - Acceptance — **Gate C**: disabling returns the channel to its prior value — proven by composing
    `AuraRuntime` (which aura is active) with `BattleDerivedModifierLedger` (T4's recompose seam):
    enable → `ledger.Add` + `Recompose` raises the channel by the T10 value; disable → `ledger.
    RemoveBySource` + `Recompose` returns it to EXACTLY the prior value, not approximately · the SAME
    guarantee holds for the AUTOMATIC eviction path (a caller withdraws the evicted aura's own
    contribution using `AuraEnableResult.EvictedAuraId`, not a separate mechanism) · three full
    enable/disable cycles never drift from the true prior value (D2 idempotence, the same discipline T1
    already proved for `OverlayAdd` and T4 for `BattleDerivedModifierLedger.Recompose`).
  - Verify: `dotnet build` clean · new `AuraRuntimeTests` (9 tests) + `AuraToggleGateCTests` (3 tests) +
    2 new `AuraTuningTests` (`maxActiveAuras` validation) — **14/14 green** · `ActionsPurityGuardTests`
    — green (no `double` introduced this time, confirmed by running it, not assumed) ·
    `audit-magic-numbers.py --targets M1` — **0** findings. Full suite: `FusionRpg.Core.Tests`
    **4595/4595 green** (4581 + 14 new).
  - Files: `Actions/Aura/AuraActiveSet.cs` (new), `Actions/Aura/AuraRuntime.cs` (new),
    `Actions/Aura/AuraEnableResult.cs` (new), `Actions/UsabilityResult.cs` (edit — 2 new reasons),
    `Aura/AuraTuning.cs` (edit — `MaxActiveAuras`), `data/tuning/aura.v1.json` (edit),
    `tests/.../Actions/Aura/AuraRuntimeTests.cs` (new, 9 tests),
    `tests/.../Actions/Aura/AuraToggleGateCTests.cs` (new, 3 tests),
    `tests/.../Aura/AuraTuningTests.cs` (edit — 2 new tests + `maxActiveAuras` assertion),
    `tests/.../Aura/AuraMagnitudeTests.cs` + `tests/.../Battle/AuraDeliveryTests.cs` (edit — fixed
    `AuraTuning` constructor call sites for the new `MaxActiveAuras` parameter).

- [x] **T14: the per-tick upkeep driver** · **M** · audit **D4** — DONE 2026-08-30
  - Built the caller D4 names as missing: `AuraUpkeepDriver.ChargeTick(actorKey, auraId, runtime, rng)`
    calls the EXISTING `CostLedger.TryPay(..., ActionCostTiming.PerTick, ...)` — an aura id IS simply
    the "action id" `CostLedger`'s own `costsByActionId` dictionary is keyed by, so this adds no second
    payment mechanism. On success the aura stays active; on a shortfall it calls `AuraRuntime.Disable`
    (T13's own interrupt path) and returns a typed, visible `AuraUpkeepTickResult` naming the blocking
    resource — never a silent deactivation.
  - **Cost is a per-aura list of resource rows** — already `CostLedger`'s own native shape
    (`IReadOnlyDictionary<string, IReadOnlyList<ActionCostRow>>`), 1 to 6 rows per aura id, no new
    authoring shape invented.
  - **The hp-floor rule landed at its real home, not duplicated in the driver.** Traced
    `resource-hub-ssot.md`'s own wording ("hp costs floor at 1 by default, refusing with the existing
    CannotAfford(hp) typed reason") to its correct location: `CostLedger` itself (a GENERIC,
    already-tested primitive with **zero production callers of any kind**, so extending it carried no
    regression risk against real usage). Added `ActionCostRow.AllowLethal` (new field, default `false`,
    every existing 4-argument call site unaffected) and one shared `HpFloorAdjustedBound` helper used
    by BOTH `Check` (OnCommit polling) and `TryPay` (real payment) — an hp row's affordability bound is
    raised by exactly 1 unless `AllowLethal`, so a payment that would bring hp to 0 reads as
    `CannotAfford("hp")`, the same typed refusal every other shortfall already uses. `AuraUpkeepDriver`
    itself adds NO hp-specific logic — it only reacts to whatever `CostLedger` already decided.
  - **A real boundary named, not hidden:** an aura authored with ZERO cost rows is charged
    successfully forever (`CostLedger.TryPay`'s own early return for "no rows for this actionId") —
    this driver has no visibility into "should this aura have a cost," only into the rows it was
    given, so it cannot itself enforce the termination invariant's "nothing free" rule. That
    enforcement belongs to T16 (content authoring: every aura must be authored with at least one
    `PerTick` row) — recorded as a named, deliberate boundary of this task, proven by a test that
    shows the gap exists rather than silently assuming it away.
  - Acceptance: upkeep is charged per tick across every pool in the aura's list ✅ · a shortfall names
    which pool blocked it (`CannotAfford(resourceId)`) ✅ · running dry disables the aura through the
    interrupt path, typed and visible — proven that the aura is GENUINELY off (`IsActive` false after),
    not merely reported off ✅ · payment is validate-all-then-consume-all, never partial — proven with a
    shortfall on the SECOND row leaving the first row's pool completely untouched ✅ · the termination
    invariant's enforcement point is correctly identified as T16's job and the gap is named, not papered
    over (see above) — "holds" in the sense that nothing here VIOLATES it, though nothing here can fully
    guarantee it without authored content to check.
  - Verify: `dotnet build` clean · new `AuraUpkeepDriverTests` — **7/7 green** (affordable multi-pool
    charge, shortfall naming, disable-through-interrupt, validate-all-then-consume-all, hp floor
    refusal, explicit lethality opt-in, the zero-cost-rows boundary named) · existing `CostLedgerTests`
    — still **10/10 green**, unchanged (confirms the hp-floor addition is purely additive) ·
    `ActionsPurityGuardTests` — green · `audit-magic-numbers.py --targets M1` — **0** findings. Full
    suite: `FusionRpg.Core.Tests` **4602/4602 green** (4595 + 7 new).
  - Files: `Actions/Aura/AuraUpkeepDriver.cs` (new), `Actions/ActionRow.cs` (edit —
    `ActionCostRow.AllowLethal`), `Actions/Cost/CostLedger.cs` (edit — `HpFloorAdjustedBound`, wired
    into `Check` and `TryPay`), `tests/.../Actions/Aura/AuraUpkeepDriverTests.cs` (new, 7 tests).

- [x] **T15: equip path — endpoint and persistence** · **DONE 2026-08-30, scope narrowed to Dave**
  - **Found the real scope before building anything:** `LoadoutStoreTests.cs` already fully proves
    `SetLoadout`/`GetLoadout`'s own persistence, mid-run refusal, and reject-leaves-existing-untouched
    behavior — the "L, split" framing overstated the gap. The ACTUAL missing piece, exactly as D3
    named it, is narrower: *"no production caller… no `/api/loadout*` endpoint exists."* Built that.
  - New `LoadoutEndpoints.cs` (`GET /api/loadout/{playerId}`, `POST /api/loadout`), mirroring
    `AptitudeEndpoints.cs`'s own established shape exactly — a thin, player-facing surface over
    already-shipped, already-tested store methods, `isHeld` wired to the REAL `RpgStore.GetAction`
    (not a mock).
  - **Scoped to Dave only, stated explicitly, not silently narrowed:** `OwnerScope(OwnerKind.Player,
    playerId)` is exactly correct for Dave (he IS the player's own commander, same reasoning T9a's
    `CommanderIds.AllocationScopeKey` already used). Zomboss has no loadout endpoint —
    `OwnerKind` is a closed, 7-value, REVIEWED vocabulary (`OwnerScope.cs`'s own doc comment,
    `definitions.md §6`) with no scope Zomboss could legally use without an 8th value, which is a
    reviewed change this task does not make unilaterally. This is not a coverage gap: Zomboss's aura
    is authored data (`ZombossPattern`, T9b/T17), never a player-equipped loadout.
  - **`isMidRun` is a named, honest gap, not a fake `true`/`false` guess:** confirmed by direct search
    that NO production "is this player mid-run" signal exists anywhere at the Server layer today
    (`WebMatchService` resolves synchronously, holds no session state). The MECHANISM this endpoint
    threads (`LoadoutSet.Validate`'s own parameter) is already fully proven
    (`LoadoutStoreTests.MidRunRejectsAndPersistsNothing`) — wired to `() => false` here so equip
    requests are never spuriously refused while that oracle doesn't exist, recorded in the endpoint's
    own doc comment rather than hidden (the same T21b precedent).
  - **Real test gap found and closed:** every existing `LoadoutStoreTests` test used exactly ONE
    `RpgStore` instance for its whole lifetime, so "survives a restart" had never actually been
    exercised — SQLite writing to a real file was proven, a SECOND instance reading it back was not.
    Added `ALoadoutSurvivesClosingAndReopeningTheStoreAgainstTheSameDirectory` (closes store A, opens a
    fresh store B on the same directory, confirms `GetLoadout` round-trips) — this is what "survives a
    restart" actually means for a new process.
  - Acceptance: equipping persists and survives a restart — proven with the new reopen test above ✅ ·
    active state does not persist (RAM only) — unaffected by this task; `AuraRuntime` (T13) never
    touches `RpgStore` at all, confirmed by inspection, not a new claim ✅ · mid-run equip still refuses
    `MidRun` — already proven at the store level (`LoadoutStoreTests.MidRunRejectsAndPersistsNothing`,
    pre-existing) while toggling an equipped aura is allowed — `AuraRuntime.Enable`/`Disable` (T13) has
    no `isMidRun` check at all, by construction, so this holds trivially ✅.
  - Verify: `dotnet build` clean on `FusionRpg.Server`/`FusionRpg.Data.Tests`/`FusionRpg.Server.Tests` ·
    new `LoadoutEndpointsTests` — **6/6 green** (empty-on-fresh-player, 404 unknown player ×2, real
    round trip through a real seeded action, 409 conflict on an unheld action with nothing saved, 400
    on a missing body field) · new restart-survival test — **green**. Full suite:
    `FusionRpg.Data.Tests` **533/533 green** (532 + 1 new), `FusionRpg.Server.Tests` **38/38 green**
    (32 + 6 new), `FusionRpg.Core.Tests` **4602/4602 green**, unaffected (confirms this task touched
    only Data/Server).
  - Files: `Server/LoadoutEndpoints.cs` (new), `Server/Program.cs` (edit — `app.MapLoadout()`),
    `tests/FusionRpg.Data.Tests/LoadoutStoreTests.cs` (edit — 1 new restart-survival test),
    `tests/FusionRpg.Server.Tests/LoadoutEndpointsTests.cs` (new, 6 tests).

### ✅ Checkpoint 4 — Gates B and C — PASSED 2026-08-30
- [x] An aura, enabled, measurably raises a channel on a friendly actor; disabled, it returns — T12's
      `AuraDeliveryTests` (real combat damage, at battle scope) + T13's `AuraToggleGateCTests` (exact
      channel-value return via the T4 ledger) together prove this end to end.
- [x] Upkeep is charged and running dry ends it honestly — T14's `AuraUpkeepDriverTests` proves charging,
      shortfall naming, and the genuine (not merely reported) disable on running dry.

---

## Phase 4 — content and surface

- [x] **T16: the twelve auras — as data, not as `world-buff.*` containers** · **M** · DONE 2026-08-30, scope named
  - **Read the spec's own §2 before building anything, and it changes the task.** `spec-aura-content.md`
    §2 proves, in its own words, that *"a `world-buff.*` container is not read by anything today"* —
    `TraitAtomSource.FromContainers` only accepts `ContainerKind.Trait`, and making a `world-buff.*` row
    reachable through the live-lawn scope/grant pipeline (`ScopeCompatibility`/
    `BattlefieldOwnSideReactor`) is explicitly named as `aura-delivery-path`'s own job — a module this
    spec says T16 *"cannot ship before."* `aura-delivery-path` remains unspecced and deferred (this
    program's own earlier finding, R4/audit D5) — building `world-buff.*` DB rows now would be
    authoring content nothing can read, exactly the defect §2 warns against.
  - **Built the twelve auras as DATA instead, feeding the delivery mechanism THIS program actually
    proved end to end** — T12's `ActiveCommanderAura`/`BattleDerivedModifierLedger`, battle-scoped, not
    the live-lawn container path. `AuraContentCatalog` (`Core/Aura/`): 12 `AuraContentRow`s (aura id,
    aptitude id, grant channels, contest channels), every channel a real registered `.omni` id from
    `DerivedStatChannels` (not a bare family — `combat.power` alone would fail `ValidateChannel`).
  - **Omni, not element slots — verified against the spec's own arithmetic, not preference:**
    `CombatDerivedReader` reads `omni + element` additively with weights summing to 1.0, so an omni
    write and an all-six-element write are numerically IDENTICAL at 1/6th the authoring cost, and
    parry/block/reflect/crit-resist are read omni-only in production — an element-slot version of
    those four auras (Onslaught, Bulwark, Retribution, Composure) would be read by nothing.
  - **Focus's reversal is named, not built.** It does not compose through the grant/contest channel
    shape this catalog uses at all (`RelationKind.Self`, buffs the commander's OWN action cooldowns,
    divisive form) — `IsReversed = true`, empty grant/contest lists, a declared exemption rather than a
    silent gap or a half-built wrong shape.
  - **Budget split per aura (spec §6) deliberately NOT built.** `AuraBudget.cs` splits ONE authored
    total across an aura's signature channels for BALANCE purposes (Retribution's 3 channels vs.
    Bulwark's 2) — T16's own acceptance text never mentions budget/split, and building a splitter with
    no real per-aura kMilli/split tunable data authored yet would be inventing numbers ahead of the
    actual balance pass. Every aura here uses the SAME `AuraMagnitude.Compute` value across all of its
    own grant channels (T10's single-axis-per-call shape) — correct for v1, not yet budget-differentiated.
  - Acceptance: opposition closure holds over the non-exempt set — proven by cross-referencing the
    catalog against itself (every non-exempt aura's contest channel matches some OTHER aura's real
    grant channel), not asserted by eye ✅ · Retribution's `reflect.resist.damage` confirmed genuinely
    unbacked (nothing in the catalog grants it) — the exemption is real, not assumed ✅ · Focus confirmed
    empty/reversed ✅ · no aura grants to `Enemy` — structurally true: the catalog carries no side
    concept at all, `Ally`-only delivery is entirely T12's `ActiveCommanderAura.CommanderSide`'s job,
    proven there (T12's own `AuraDeliveryTests`) ✅ · one row serves both factions — trivially true for
    the same reason (content is side-agnostic; Dave's and Zomboss's own `ActiveCommanderAura` records
    both reference the SAME `AuraContentCatalog` row) ✅ · every aptitude id cross-checked against the
    REAL `ZombossPatterns`' own force/finesse/bastion pure builds, catching a possible typo/13th-aptitude
    mistake rather than trusting the table by eye ✅.
  - Verify: `dotnet build` clean · new `AuraContentCatalogTests` — **8/8 green** ·
    `audit-magic-numbers.py --targets M1` — **0** findings. Full suite: `FusionRpg.Core.Tests`
    **4610/4610 green** (4602 + 8 new).
  - Files: `Aura/AuraContentCatalog.cs` (new), `tests/.../Aura/AuraContentCatalogTests.cs` (new, 8
    tests).

- [x] **T17: Zomboss runs auras — each pattern names one** · **M** · DONE 2026-08-30
  - Extended `ZombossPattern` with `AuraId` — **derived from each pattern's own already-authored
    `SharePermille`** (the highest-weighted aptitude in that pattern, ties broken alphabetically),
    rather than a second, independent hand-pick disconnected from the pattern's real identity: e.g.
    `force-pure`'s highest weight is Might (396) → runs Might; `bastion-pure`'s highest is Ferocity
    (402) → runs Ferocity. All 9 patterns assigned this way, two pairs sharing an aura where two
    mixed-pattern variants share the same dominant aptitude (`Onslaught` ×2, `Pierce` ×2) — acceptable,
    since nothing requires distinct auras across DIFFERENT Zomboss patterns.
  - Added `ZombossCommanderAllocation.ActiveAuraId` — a bare lookup
    (`ZombossPatterns.Resolve(activePatternId).AuraId`), exactly "no AI logic."
  - Acceptance: each of the nine patterns names a valid aura — proven against the REAL
    `AuraContentCatalog.IsKnown` for all 9, not asserted by eye ✅ · Zomboss's aura resolves from his
    active pattern, and changes when the pattern switches ✅ · **two commanders running opposed auras
    measurably cancel in one contest** — the acceptance bullet this whole program's own-side-only
    property rests on: Dave's Might (`force-pure`'s own aura) vs. Zomboss's Fortitude
    (`force-defence-bastion-breaks-guard`'s aura, which `AuraContentCatalog` confirms contests Might's
    exact grant channel) run in the SAME battle at the SAME seed — squad damage with BOTH auras active
    is strictly lower than with Dave's Might alone, proven through real combat resolution (T12's
    delivery), not just channel arithmetic ✅ · the aura id is tunable/authored data (a plain field in
    `ZombossPatterns.cs`, not a branch of code) ✅.
  - Verify: `dotnet build` clean · new `ZombossAuraTests` — **11/11 green** (9 per-pattern validity
    theory cases + active-aura-resolves-from-pattern + the opposed-auras-cancel integration test) ·
    existing `ZombossPatternTests` — still green, unaffected (the closed-shape reflection test only
    checks for an "Element"-named property, `AuraId` doesn't match) · `BattleGoldenTests` +
    `ExpeditionResolverTests` — green, no new golden moves (this task added a record field to
    `ZombossPattern`, a type never embedded in either hash). Full suite: `FusionRpg.Core.Tests`
    **4621/4621 green** (4610 + 11 new).
  - Files: `Battle/Ai/ZombossPattern.cs` (edit — `AuraId` field),
    `Battle/Ai/ZombossPatterns.cs` (edit — 9 catalog rows), `Battle/Ai/ZombossCommanderAllocation.cs`
    (edit — `ActiveAuraId`), `tests/.../Battle/Ai/ZombossAuraTests.cs` (new, 11 tests).

- [x] **T18a: `ActorHub.ResolveDerivedWithContributions`** · **S** · DONE 2026-08-30 — split from T18
  - **The one Core-level prerequisite GG-49 actually needed, isolated from the rest of T18.**
    `ActorHub.ResolveDerived` built a `mods` list, folded it through `DerivedComposer.Compose`, then
    discarded it — the exact per-source information T11's `DerivedContributionBag` needs was one
    method away from being retained for free. Added `ResolveDerivedWithContributions(ctx)`, returning
    `(Snapshot, Contributions)` from the SAME `mods` list — not a second compose, so the two can never
    disagree about what contributed.
  - Acceptance: the snapshot matches `ResolveDerived`'s own output exactly (proven directly, not
    assumed) · a real subsystem's contribution is named and its value accounts for the whole channel
    (a live GG-49 answer, not a placeholder) · an empty hub resolves with zero contributions, never
    throws.
  - Verify: new `ResolveDerivedWithContributionsTests` — **3/3 green**. Full suite:
    `FusionRpg.Core.Tests` **4624/4624 green** (4621 + 3 new).
  - Files: `Stats/Derived/ActorHub.cs` (edit — new method),
    `tests/.../ActorHub/ResolveDerivedWithContributionsTests.cs` (new, 3 tests).

- [x] **T18b: the derived-channel-with-contributions server endpoint** · **M, split from T18** · DONE 2026-08-30
  - Re-investigated the "Needs" gap named below before accepting it as a deferral, per the Stop hook's
    challenge that a categorization is not proof. **It was not a real architectural blocker** — unlike
    T8 (genuinely blocked by the `overlay-combat-enable` spec's own "Always: re-run the proof on a real
    lawn before flipping" boundary plus a GUI-only precondition, `setup-lab-run.ps1:2`'s "operator
    already in a normal day lawn"), this was ordinary unbuilt wiring. Built it. (T21b's Cold-plane
    `player_id` read looked the same at the time this note was written — re-investigated later and
    also built, once the owner authorized the architecture decision it needed; see T21b's own entry.)
  - `GET /api/actors/{instanceId}/derived`: resolves the real `UniqueActorDto` by `instanceId`
    (`RpgStore.GetUniqueActor`, already shipped), derives an `EntityBaseline` from its level via
    `BattleRuleset.BaseHp/BaseAtk(level)` (the same statics `WebMatchService.BuildSquad` already uses
    for battle, applied here to a LAWN actor for the first time), builds a real `StatContext` via
    `StatContextFactory.ForPlant/ForZombie` keyed on the row's own `side`, then calls a per-request
    `ActorHubBootstrap.CreateDefault(...)` — one `ActorHub` per request, matching every other
    per-request Core object this Server project already constructs on demand; nothing registers
    `ActorHub` in DI (confirmed by reading `Program.cs`'s `AddSingleton` block directly). The aptitude
    allocation delegate reuses `WebMatchService.AptitudeChannelMods`'s exact
    `store.LoadAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId))` call — no
    second allocation-read implementation. Response: `{ instanceId, channels: [{ channelId, value,
    contributions: [{ sourceId, op, value }] }] }`, channels sorted by id for a stable contract.
  - Acceptance: unknown `instanceId` 404s · a real actor resolves real `progression.power`/
    `progression.realm` channels sourced `"rpg.progression"` (proves `ActorHub` actually ran, not a
    stub) · both plant and zombie sides resolve (the `ForPlant`/`ForZombie` branch is exercised both
    ways) · **a real, store-saved aptitude allocation produces a non-vacuous
    `"aptitude.Might"`-sourced contribution that was absent before the allocation** — GG-49 answered
    with live data for the first time at the Server layer, not a placeholder.
  - Verify: new `AuraDerivedEndpointsTests.cs` — **4/4 green**. Full suite:
    `FusionRpg.Server.Tests` **42/42 green** (38 + 4 new), `FusionRpg.Core.Tests` **4625/4625 green**,
    unaffected (no Core file changed).
  - Files: `Server/AuraDerivedEndpoints.cs` (new), `Server/Program.cs` (edit — `app.MapAuraDerived()`),
    `tests/FusionRpg.Server.Tests/AuraDerivedEndpointsTests.cs` (new, 4 tests).

- [x] **T18c: the web surface itself** · **L, split from T18** · depends on T18b · DONE 2026-08-30
  - Re-investigated before accepting "not scheduled" as final, per the Stop hook's challenge: the
    named blocker ("no server-side runtime-state wiring... zero HTTP surface") described NEW work
    needed, not a missing prerequisite from another program — the same shape T18b's own "Needs" text
    turned out to be. Built the whole slice: two more small server endpoints, then the full frontend.
  - **Found and fixed a real prerequisite gap before any of this could work at all:**
    `AuraContentCatalog` ids (T16) are never `ActionRow`s — a deliberately separate authoring catalog
    — so `LoadoutEndpoints.cs`'s original `isHeld` check (`store.GetAction(id) is not null`) refused
    every real aura id, meaning NO aura could ever be legally equipped through the existing loadout
    endpoint. Fixed at the source: `isHeld` now also accepts `AuraContentCatalog.IsKnown(id)` — not
    worked around downstream. New regression test:
    `Post_aRealAuraId_savesEvenThoughItIsNeverAnActionRow`.
  - **Server (3 new endpoints + 1 new tuning hub, none existed before this task):**
    - `AuraTuningHub` (new, `Core/Aura/AuraTuning.cs`) — every other tuning in this repo has a
      `XxxTuningHub`; `AuraTuning` was the one exception (T10/T13 never needed a Server consumer
      before). Configured at Server startup alongside every other hub, same call shape.
    - `GET /api/auras` (new `AuraCatalogEndpoints.cs`) — the full 12-aura catalog, since the web needs
      to render every locked-OR-equipped slot, not just one player's active/equipped subset.
    - `GET/POST /api/aura-runtime/{playerId}[/enable|/disable]` (new `AuraRuntimeEndpoints.cs`) —
      wraps T13's `AuraRuntime`/`AuraActiveSet` in a **process-local, bare-static session cache**
      (matching this codebase's own `PatronRuntimeState` pattern exactly — T15 already established
      "active state does not persist (RAM only)" as the correct shape). Equipped-check reads the
      player's REAL loadout fresh on every call (`store.GetLoadout`), never cached at construction, so
      equipping a new aura via `POST /api/loadout` takes effect on the very next enable attempt.
    - Test-only `ResetForTests()` added and documented: the static cache is keyed by bare `playerId`,
      and every test's fresh SQLite file restarts its own autoincrement id sequence at the same
      values — without a reset, a later test's "player 1" would inherit an earlier test's still-active
      aura. Never called from `Program.cs`.
  - **Frontend:** `lib/bus/aura.ts` (new — DTOs + `useAuraCatalog`/`useAuraRuntime`/`useActorDerived`
    query hooks, `useEnableAura`/`useDisableAura` mutations with `meta.entity` for the existing global
    toast feedback, matching `patron.ts`'s established self-contained-domain-file shape exactly) ·
    `ui/actor/AuraSlot.tsx` (new, presentational — active/equipped-inactive/locked, a colored `Badge`
    for the state so it is "unmistakably distinct, not a subtle tint") · `ui/actor/
    ChannelContributions.tsx` (new, presentational — renders each source + magnitude, an honest empty
    note when nothing has contributed yet, never a fabricated grid) · `ActionsTab.tsx` (edit — real
    aura slots, resolved from `/api/auras` × `/api/aura-runtime`, as a distinct group ABOVE the
    still-locked placeholder action grid, resolving the spec's own open question 1) · `DerivedStatsTab.
    tsx` (edit — a live section fed by T18b's endpoint, shown separately from the still-pending
    `channelSummary`/`StatSummaryGrid` contract rather than force-fitting this endpoint's simpler shape
    into that richer, still-unproduced one) · `ActorPanel.tsx` (edit — `<ActionsTab data={data} />`,
    matching every sibling tab).
  - **A real architectural rule caught and fixed a design mistake before it shipped:** this repo's own
    `contractGuard.test.ts` (`no file under stages/, layers/ or ui/ imports a REST DTO type`) failed
    the first `ChannelContributions.tsx` draft, which imported `DerivedContributionDto` straight from
    `@/lib/bus/aura` — exactly the violation `spec-aura-surface.md` §8's own "Bind to `@/contract`,
    never a REST DTO directly" boundary warns against. Fixed by adding a proper, additive
    `contract/types.ts` type (`DerivedContribution` — deliberately NOT force-fit into the existing,
    richer, still-unproduced `ActorChannelDetail.contributions` shape, whose `Magnitude`/`source` field
    names and types don't match this endpoint's simpler real one) and importing that instead.
  - Acceptance — every bullet checked:
    - [x] Active vs equipped-inactive unmistakable — a colored `Badge` (`ACTIVE`/`EQUIPPED`), not a
      tint; confirmed both by unit test and by inspecting the real rendered screenshot.
    - [x] Enabling at the cap names the aura that switched off (GG-55) — proven by
      `ActionsTab.test.tsx`'s `enabling at the cap names the aura that switched off` AND by
      `e2e/aura.spec.ts`'s real-browser equivalent (Might → Fortitude eviction, the note visible in
      the DOM, not just in a transient toast).
    - [x] Upkeep visible before committing — **DONE 2026-08-30, second pass.** The first pass's
      "genuinely not buildable" verdict was wrong — it confused "no aura has a real cost authored
      today" (true) with "the feature can't be built without fabricating one" (false). Checked
      `RpgStore.UpsertCost`/`ListCosts(actionId)` (`RpgStore.Actions.cs:319-360`) directly: the
      `rpg_action_cost` table has **no foreign-key requirement on a real `ActionRow`** — an aura id is
      already a legal cost key today. Built the real read path instead of declaring it blocked:
      `GET /api/auras` now includes each aura's real `upkeep` (`store.ListCosts(auraId)`, mapped to
      `{resourceId, amountMin, amountMax, when}`) — genuinely empty for all twelve today (still
      confirmed by `grep -rn PerTick data/` finding zero), never fabricated, and it will start
      rendering real content the moment a balance pass calls `UpsertCost` for a real aura id, with
      **zero code change**. `AuraSlot.tsx` shows the note before the toggle when present, renders
      nothing when absent (proven by a dedicated "never a fabricated placeholder" test both in
      isolation and through `ActionsTab`). Proven live in the e2e spec too — a mocked authored cost
      renders "5 stamina per tick" before the button; a sibling aura with none shows nothing.
      New tests: `AuraCatalogEndpointsTests` +2 (empty-today, real-cost-appears-with-no-code-change),
      `AuraSlot.test.tsx` +2, `ActionsTab.test.tsx` +2, `e2e/aura.spec.ts` +1. All green (see Verify).
    - [x] Every locked aura states its real reason — "Not equipped — assign it in your loadout first"
      (the one real reason knowable today; gating behind unshipped systems like T8's overlay flag is
      not distinguishable from this data and was not fabricated).
    - [x] A derived channel shows its contributions — **GG-49 satisfied non-vacuously for the first
      time**, proven by a real HTTP round trip in both a unit test and the e2e spec.
    - [x] Pending/loading/error states are honest, nothing fabricated — `actions-tab-loading`,
      `derived-stats-live-loading`, `derived-stats-live-error`, `derived-stats-live-empty` all render
      real, distinct reasons.
  - Verify: `npm run test` — **722/722 green** (99 files) · `npm run build` — clean ·
    `npx playwright test e2e/aura.spec.ts` — **7/7 green** (upkeep visibility, enable, cap eviction
    naming, locked reason, GG-49 contributions, 2 visual screenshots) · full `npx playwright test` —
    **190/191 green**, the 1 failure (`actor-menu-scope-picker.spec.ts`, unrelated — a different demo
    page, touches none of these files) re-ran **5/5 green in isolation**, confirming a parallel-load
    flake, not a regression · both desktop and mobile screenshots **actually opened and inspected**,
    not just captured — active/equipped/locked states and the upkeep note all read correctly, no
    overflow. Server-side: `FusionRpg.Server.Tests` **55/55 green** (36 + 19 new, across
    `AuraRuntimeEndpointsTests`/`AuraCatalogEndpointsTests`/the `LoadoutEndpointsTests` regression),
    `FusionRpg.Core.Tests` **4625/4625 green**, `FusionRpg.Data.Tests` **536/536 green**,
    `FusionRpg.Guard.Tests` **116/116 green** — all unaffected, confirming this task's changes stayed
    inside `FusionRpg.Server`/the web tree.
  - Files: `Core/Aura/AuraTuning.cs` (edit — `AuraTuningHub`), `Server/AuraCatalogEndpoints.cs` (new,
    edited again for real `upkeep`), `Server/AuraRuntimeEndpoints.cs` (new), `Server/LoadoutEndpoints.cs`
    (edit — `isHeld` fix), `Server/Program.cs` (edit — 3 new `Map*` calls + `AuraTuningHub.Configure`),
    `tests/FusionRpg.Server.Tests/AuraCatalogEndpointsTests.cs` (new, 3 tests),
    `tests/FusionRpg.Server.Tests/AuraRuntimeEndpointsTests.cs` (new, 9 tests),
    `tests/FusionRpg.Server.Tests/LoadoutEndpointsTests.cs` (edit, +1 regression test),
    `web/.../lib/bus/aura.ts` (new, incl. `AuraUpkeepCostDto`), `web/.../contract/types.ts`
    (edit — `DerivedContribution`), `web/.../ui/actor/AuraSlot.tsx` + `.test.tsx` (new, incl. upkeep
    note), `web/.../ui/actor/ChannelContributions.tsx` + `.test.tsx` (new),
    `web/.../ui/actor/ActionsTab.tsx` + `.test.tsx` (rewritten, incl. `upkeepNoteFor`),
    `web/.../ui/actor/DerivedStatsTab.tsx` + `.test.tsx` (edit), `web/.../ui/actor/ActorPanel.tsx`
    (edit), `web/.../e2e/aura.spec.ts` (new, 7 tests).

- [x] **T22: give `patron.aura` a `P(Θ)` term** · **S** · owner sign-off 2026-08-30 — DONE 2026-08-30
  - Owner explicitly approved landing this now (AskUserQuestion: "Yes, implement it now") after this
    task's own text surfaced the spec-lock/sign-off requirement — a genuine audit-defined gate, not an
    invented one, correctly held open until the owner actually answered it.
  - Formula: `AuraMilli = flatPart (UNCHANGED — rarityBase+perStar·star+level, still clamped at
    `AuraClampMilli`) + pThetaTermMilli (NEW — `PThetaKMilli/1000 · PowerLadder.Value(pTheta)`, inside a
    `checked` block, uncapped)`. The flat part never moved; only the new term makes patron stay relevant
    past content depth, and it is intentionally uncapped per this repo's no-hard-ceiling rule.
  - `pThetaKMilli = 220` (new tunable, `data/tuning/patron.v1.json`) grounded in the shipped
    `power-scale.v2.json` pin (`P(20)=680`), chosen so the new term ≈150 at a "typical early" Θ≈20 — a
    continuity point with the old flat ceiling, documented in the tunable's own `_meta.note`, not an
    arbitrary guess.
  - Overflow discipline (CLAUDE.md): `PatronAura`'s 4 fields (`PowerMilli`/`DefenseMilli`/
    `SecondaryPowerMilli`/`SecondaryDefenseMilli`) widened `int`→`long` since the term is now
    ladder-scaled and unbounded; every real consumer traced via grep before touching the type
    (`PatronAuraOverlay.cs`, `PatronCommand.cs`, `PatronEndpoints.cs`, plus 2 test files) — no half-done
    ripple.
  - `PatronEndpoints.cs`'s `Compute` now resolves the player's real `Θ` the same way
    `AptitudeEndpoints.cs` does, via an inline `ServerPowerIndexProvider(store, PowerTuningHub.Tuning)`
    — avoided threading `IPowerIndexProvider` via DI through 4 unrelated external callers
    (`Program.cs`, `RpgHub.cs`, `EventIngest.cs`, `SimEndpoints.cs`) since the provider only needs
    values already in scope.
  - `ssot-power-scale.md` §10 row 16 **updated (reviewed doc edit, not a tuning tweak, per this task's
    own acceptance bullet)**: it no longer says "Never reads `PowerTuning`, never should" — it now
    documents the two-axis shape (`flatPart` bounded/level-free as before; `pThetaTermMilli` a
    legitimate, reviewed `P(Θ)` read added by T22) and PS-4 is corrected to carve out the one exception
    inside row 16 itself rather than blanket-exempting the whole row from `contentScale`.
  - Acceptance — every bullet checked, not assumed:
    - [x] Patron stays meaningful past ~15 points of commander investment — proven by
      `The_pTheta_term_grows_with_Theta_this_is_the_whole_point_of_T22`: the term strictly grows with
      Θ and at Θ=1000 exceeds the old flat ceiling (`AuraClampMilli`).
    - [x] The §10 row is updated — see above.
    - [x] `guard-power.ps1` run and interpreted — **`POWER GUARD OK — one ladder, pin holds, no private
      f(level)`**. The new term calls the shared `PowerLadder`, not a private curve, so the guard stays
      green.
    - [x] No golden moves — confirmed by running (not assuming) `BattleGoldenTests` +
      `DominanceBaselineTests` + `ExpeditionResolverTests` filtered (**16/16 green**): `PatronAura`/
      `PatronPolicy` are not embedded in any hash input those tests cover.
  - Verify (every suite re-run after the change, none `--no-build`d past the last real edit):
    `FusionRpg.Core.Tests` **4625/4625 green** (full, unfiltered — confirms the `int`→`long` widening
    broke nothing anywhere else in Core), `FusionRpg.Server.Tests` **38/38 green** (confirms the
    `PatronEndpoints.cs` Θ-resolution change), `FusionRpg.Data.Tests` **536/536 green** (after fixing a
    SECOND, separate `ContractTuningTestBootstrap.cs` fixture in `tests/FusionRpg.Data.Tests/` that this
    project also owns and that the Core.Tests-side fixture fix did not touch — caught by an actual build
    failure, not assumed clean), `FusionRpg.Guard.Tests` **116/116 green**, `FusionRpg.Launcher.Tests`
    **162/162 green**, `FusionRpg.CheatCore.Tests` **40/40 green**. `audit-magic-numbers.py --targets
    M1`: **0 findings, repo-wide** (the new `pThetaKMilli` tunable is properly in
    `data/tuning/patron.v1.json`, not a bare literal). `audit-overflow.py`: **0 critical**, and no
    finding references any Patron file (confirms the `checked` block and `long` widening are clean).
  - **Gap found and fixed 2026-08-30 (during T8's live-proof pass):** a THIRD `ContractTuningTestBootstrap.cs`
    fixture exists at `tests/FusionRpg.E2E.Tests/` — this task's own verify line above never ran or even
    built `FusionRpg.E2E.Tests`, so its unfixed `PatronTuning(...)` call site (missing `PThetaKMilli`)
    left that whole test project broken at the source level since T22 landed. Found via a routine
    "run every suite" pass, not a targeted search. Fixed (added `PThetaKMilli: 220` matching the other
    two fixtures); `FusionRpg.E2E.Tests` now **194/194 green** — confirms this task's "no golden moves"
    claim actually holds across all six .NET test projects, not five.
  - Files: `Core/Demons/Patron/PatronPolicy.cs` (edit — formula + widened `PatronAura`),
    `Core/Demons/Patron/PatronTuning.cs` (edit — `PThetaKMilli` field + loader),
    `data/tuning/patron.v1.json` (edit — new tunable + `_meta.note`),
    `Injector/Effects/PatronAuraOverlay.cs` (edit — `AddChannel` param widened, unbuildable here, direct
    read only per this session's established precedent), `Injector/Effects/PatronCommand.cs` (edit —
    `GetInt`→`GetLong`), `Server/PatronEndpoints.cs` (edit — real Θ resolution in `Compute`),
    `tests/FusionRpg.Core.Tests/ContractTuningTestBootstrap.cs` (edit — `DefaultPatron` fixture),
    `tests/FusionRpg.Data.Tests/ContractTuningTestBootstrap.cs` (edit — its OWN separate
    `DefaultPatron` fixture, the build-break fix), `tests/FusionRpg.Core.Tests/Demons/
    PatronPolicyTests.cs` (heavily edited — 2 tests rewritten, 1 new, **11/11 green**),
    `docs/architecture/power/ssot-power-scale.md` (edit — row 16 + PS-4).

- [x] **T19: wire the `ActionCatalog`** · **S** · D3 part two — DONE 2026-08-30
  - Built `RpgStore.BuildActionCatalog(rungTable, onRejected?)` — the FIRST production caller of
    `ActionCompiler.Compile` at bulk scale: enumerates every authored `ActionRow`
    (`ListActionIds`/`GetAction`, already-shipped bulk reads), loads its costs/scopes/container atom
    ids, compiles each via the EXISTING pipeline, and collects successes into a real `ActionCatalog` —
    no new compilation logic, only the "load every row, compile it, collect what succeeds" loop nothing
    in production had ever run. A row that fails to compile is skipped, not fatal, matching
    `AtomRowValidator`'s own whole-row-rejection discipline applied at catalog-assembly scope;
    `onRejected` gives the caller visibility into what got skipped, never silently swallowed.
  - Wired it into **all three** `BattleEngine.Resolve` call sites in `WebMatchService.cs` — the ONLY
    production caller of `Resolve` — passing `actionCatalog: _store.BuildActionCatalog(RungPolicy.
    Table)` (`RungPolicy.Table` already configured at Server startup, `Program.cs`). Before this, ALL
    THREE call sites passed no catalog at all, meaning every non-empty `EquippedActionIds` anywhere in
    production always hit T3's degrade path — even for a perfectly valid, real, granted skill.
  - Acceptance: equipped actions resolve properly rather than being silently dropped — proven with a
    real authored+valid skill action seeded through the store, compiled by `BuildActionCatalog`, and
    resolved from the built catalog by its own action id (not just "the code compiles") · an empty
    store still builds an empty catalog without throwing (the pre-existing, still-legal "nothing
    equipped" state) · one rejected row (an authoring mistake — a missing container) does not take
    down the whole catalog's ability to resolve every OTHER valid action.
  - Verify: `dotnet build` clean on `FusionRpg.Data`/`FusionRpg.Server` · new `ActionCatalogBuilderTests`
    (`FusionRpg.Data.Tests`, where `RungPolicy` is already globally configured via
    `ContractTuningTestBootstrap`'s `[ModuleInitializer]`, matching `Program.cs`'s own real startup
    sequence) — **3/3 green**. Full suite: `FusionRpg.Data.Tests` **536/536 green** (533 + 3 new),
    `FusionRpg.Server.Tests` **38/38 green**, unaffected (confirms the real-battle wiring change broke
    nothing already-exercised — `BuildSquadEquippedActionsTests.cs`'s own real-grant/real-squad tests
    run through this same `WebMatchService` unchanged), `FusionRpg.Core.Tests` **4621/4621 green**.
  - Files: `Data/Sqlite/RpgStore.ActionCatalog.cs` (new), `Server/WebMatchService.cs` (edit — 3
    `Resolve` call sites), `tests/FusionRpg.Data.Tests/ActionCatalogBuilderTests.cs` (new, 3 tests).

### ✅ Checkpoint 5 — program complete *(build complete; see Phase 5 for the coverage correction)*

> ⚠️ **Read this before quoting the green-suite line below.** Every box in this checkpoint is true as
> written — the build shipped and the suites are green. But **green ≠ covered**: an audit on 2026-08-30
> established that the twelve-aptitude distribution is tested one edge deep (`Might` only), the
> twelve-aptitude matrix this file cites is a **live manual probe recorded as prose, not a test**, and
> the PvZ-engine write path has no test project at all. Phase 5 (TC1–TC3) owns closing that. Do not
> read *"full Core/Guard/Data + web suites green"* as *"the twelve aptitudes are covered end to end."*
- [x] Gates A, B and C all assert against real entities — re-confirmed by this session's own gate work
  across T9–T21 (`ActiveCommanderAura`, `AuraContentCatalog`, `MechanicalOwnSideOracle`, etc.), each
  proven against real battle/store/commander state, never a stub.
- [x] Full Core/Guard/Data + web suites green; no goldens moved — re-verified as of T18c's own second
  pass (2026-08-30, the program's actual last code change): Core **4625/4625**, Server **55/55**, Data
  **536/536**, Guard **116/116**, Launcher **162/162**, CheatCore **40/40**, all 0 failures; golden test
  classes filtered and confirmed unchanged (16/16). Web: `npm run test` **722/722 green** (99 files),
  `npm run build` clean, full `npx playwright test` **190/191 green** (the 1 failure — a different,
  untouched demo page — re-ran 5/5 green in isolation, confirming a parallel-load flake, not a
  regression), both `aura.spec.ts` visual screenshots actually opened and inspected.
- [x] Owner-run live check on the lawn — **CLOSED 2026-08-30, with real evidence.**
  `/api/debug/enter-level` via `lawn/quick-start` opened a real lawn (T8), and a real
  allocate→spawn→`stat.writer` probe ran against it. Two real bugs were found and fixed in the
  commander-allocation live-sync path (SignalR group mismatch; missing reconnect resync). An initial
  reading of the probe looked like a third, unresolved defect (`attackDamage` stuck at `1` regardless
  of allocation) — that turned out to be a test-environment artifact (a `lawn/quick-start`
  stale-board false-positive after a redeploy, not a real board), caught by re-running the identical
  probe against a genuinely fresh `board.start`: **`attackDamage` went from a real vanilla baseline of
  `20` to `31010` under a 222-point `Might` allocation, and back to `20` with the allocation emptied.**
  Full account in Checkpoint 2's own entry. The owner's "Leave it to you" (2026-08-30) authorized
  attempting the automation in the first place; the automation then did its job.
- [x] Every balance number is a tunable; `audit-magic-numbers.py --targets M1` clean — **0 findings,
  repo-wide** (confirmed via `--summary` too, so this is a real zero, not a silently-empty run).

---

## Phase 5 — coverage gaps found by the 2026-08-30 audit ⛔ OPEN

**Why this phase exists.** Checkpoint 5 above says *"full Core/Guard/Data + web suites green"*. That was
true and is still true — but **green is not covered**, and the owner asked the sharper question:

> *"have we really done aura skill and test coverage — include aura level and 12 primary stats
> distribution include in pvz engine and rpg engine?"* — owner, 2026-08-30

Audited against the tree rather than against this file's own checkboxes. **The honest answer is no.**
Aura *level* is genuinely covered; the *twelve-aptitude distribution* is one edge deep, and the PvZ
side has no automated test at all. Recorded here rather than quietly left, because Checkpoint 5 as
written would let a future session believe this was closed.

| What was asked | Verdict | Evidence read this session |
|---|---|---|
| Aura level (rung) | ✅ **genuinely done** | `AuraMagnitudeTests.cs` — all four rungs 7/8/9/10 exercised (`:34` `:41` `:51` `:71`), hand-computed value at rung 7, linearity in share, Θ-ratio constant for fixed rung, rung 3 rejected at use (`:95`); `AuraTuningTests.cs` rejects <7 and >10 **at load** |
| Twelve auras exist, one per aptitude | ✅ | `AuraContentCatalogTests.cs:16` + opposition closure, both declared exemptions |
| Twelve aptitudes → derived channels | ❌ **only `Might`** | Every magnitude/resolver test builds a synthetic **one-edge** tuning: `"edges": [{ "channel": "combat.power.omni", "source": "Might", "kMilli": 1000 }]`. `AptitudeResolverTests.cs:42` is `MightAllocation_resolvesCombatPowerOmni`. **Nothing iterates the 490 real edges in `aptitudes.v2.json`.** |
| RPG-layer delivery | ⚠️ **Sim host only** | `Battle/AuraDeliveryTests.cs` — 4 facts, squad vs. wave side. Zero references to lawn, `StatContext`, or the injector. |
| Lawn delivery | ❌ untested **and blocked** | `spec-derived-write-lawn.md` A5 — `EffectBag.Grant` rejects an unregistered `EffectId`; needs the atom→compile→bind chain |
| PvZ-engine write, per aptitude | ❌ **no test project exists** | There is no `FusionRpg.Injector.Tests` under `tests/`. `EntityApply.cs` — the class that writes Unity fields — has zero unit tests. |
| Both engines agree | ⚠️ **one edge** | `ProveAptitudeJsonEmitTests.cs:18` — `BothComposersAgree_mightToCombatPowerOmni`. Its own third fact names an unclosed gap: `UnfilteredRun_surfacesTheKnownCapAsymmetryGap_documentedNotHidden`. |

**The load-bearing correction:** the twelve-aptitude matrix this program keeps citing as proof (Checkpoint
2 and Checkpoint 5) **was run live, by hand, against the running game. It is not a test.** It lives as
prose in this file. Nothing in CI would notice if edge 300 of 490 stopped resolving tomorrow. That is
exactly the shape of defect the write-gate bug was — composed correctly, dropped silently, found by the
owner playing the game rather than by the suite.

`ReaderCensusTests` and `SeamCoverageTests` are the nearest existing checks and are deliberately
**structural** — a census of reader-less families, and an IL-body-size probe telling wired from inert.
Neither asserts a value. They are not substitutes for TC1.

---

- [x] **TC1: `AptitudeMatrixTests` — all twelve aptitudes over the real shipped edge set** · **M** · **DONE 2026-08-30**
  - **Built:** `tests/FusionRpg.Core.Tests/ClassSystem/AptitudeMatrixTests.cs` — **13 tests, 13 green.**
    Twelve aptitudes × their declared edges = **all 486**, each resolved and compared against a value
    recomputed in the test from the raw JSON. Full Core suite **4735/4735 green, 0 failures** (was
    4723 before this file; +12 at that point, +1 more added after review, re-run below).
  - **Independent oracle, not a second call to the resolver.** The test re-parses `aptitudes.v2.json`
    with `JsonDocument` and restates two rules itself — `FamilyOf`'s exact-match-then-strip-one-suffix,
    and `EffectiveKMilli`'s recovery-wins-then-mitigation dial order — because asking the loader to
    confirm the loader proves nothing. Shares of 1.0 collapse `share^γ` to exactly 1 so the expected
    value is plain integer arithmetic stated in the test, independent of `AptitudeReadFunctions`'
    decimal path; `Fractional_shares_hold_too` then exercises the real `share^γ` branch at share 0.5.
  - ### ⭐ The falsifier that proves this was worth building
    Three mutations were applied to **production** code, run, and reverted (residue check: `grep -rn
    "FALSIFIER" src/ tests/` → clean; `git diff` on both files → `AptitudeResolver.cs` byte-identical):

    | Falsifier (temporary mutation) | New `AptitudeMatrixTests` | Pre-existing suite |
    |---|---|---|
    | **1.** Recovery dial dropped in `EffectiveKMilli` (the literal 2026-08-27 regression) | **Failed: 4** | — |
    | **2.** One edge of 486 silently skipped (`Onslaught → combat.power.omni`) | **Failed: 5** | **65 aptitude tests: all PASS** |
    | **3.** A 6th bridge channel added to `ActorHub.MergeAppliedCombat` | **Failed: 1** | **496 ActorHub/writer tests: all PASS** |

    Falsifiers 2 and 3 are the load-bearing evidence: **a single dropped edge and a leaking PvZ bridge
    channel are both completely invisible to the existing suite** and caught only here. That is
    precisely the defect shape the owner hit by playing the game — composed correctly, dropped silently.
  - ### ⛔ A defect found in this task's own work, during REVIEW
    The first draft of `Progression_bonus_is_the_only_edge_family_that_can_reach_a_pvz_unity_field`
    **hardcoded the five bridge ids** and its own doc comment claimed a sixth would surface there. It
    would not have — a hardcoded list agrees with itself no matter what `MergeAppliedCombat` does.
    Rewritten to be **behavioural**: it drives `ActorHub.Resolve` once per distinct channel the shipped
    edges target and reads `ReferenceEquals(AppliedCombat, RuntimePrimary)` (Merge returns the primary
    *instance* when no bridge channel carries a value, so identity is an exact reading of "did this
    cross into the Writer input?"). Falsifier 3 above was then run specifically to prove the corrected
    version catches what the draft could not. The wrong version was never reported as passing.
  - Acceptance — every box verified, not assumed:
    - [x] Every edge resolves to a **registered** channel — asserted for all 486 in
      `Every_edges_compose_op_matches_its_target_channels_registered_compose_kind`; the "unregistered
      *throws*" half is `AptitudeResolverTests.cs:103`, pre-existing and cited rather than duplicated.
    - [x] All twelve ids computed from the file and compared to `AptitudeCatalog`, never a typed roster
      — `All_twelve_aptitudes_source_at_least_one_edge_computed_from_the_file_not_a_hardcoded_roster`.
    - [x] Each edge's value matches its declared read function, at **two Θ** (10 and 74) —
      `Every_one_of_the_486_shipped_edges_resolves_to_its_independently_computed_value` `[Theory]`, plus
      `Magnitude_edges_scale_with_pTheta_and_contest_edges_do_not_across_the_whole_shipped_set`, which
      asserts exact proportionality per edge and refuses to pass vacuously (it fails if either mode is
      absent from the set, and rejects a zero coefficient that would fake Θ-invariance).
    - [x] Channel-family split computed, **`progression.bonus.*` proven the only PvZ-reaching family** —
      measured behaviourally as above; the RPG-layer remainder's families are asserted to be exactly
      `combat, move, progression, resource, skill, status`.
    - [x] Reader-less edges enumerated and cross-checked **from a resolve**, not a static scan —
      `The_eighteen_reader_less_edges_still_resolve_and_the_count_matches_meta_measurable`: 18 of 486
      across 6 families, the numbers parsed out of `_meta.measurable` itself rather than retyped, and
      every one proven to resolve to a **live nonzero value**. Reader-less ≠ inert: the points are
      spent, the value composes, nothing consumes it. `ReaderCensusTests` proves the same count by
      static scan; this is the opposite side of the claim, deliberately not a duplicate.
    - [x] Overflow — `No_shipped_edge_wraps_at_extreme_theta_it_resolves_exactly_or_throws` `[Theory]`
      at Θ = 1 / 1,000 / 100,000: every edge either resolves exactly or throws `OverflowException`;
      a negative (wrapped) magnitude fails the test.
  - Files: `tests/FusionRpg.Core.Tests/ClassSystem/AptitudeMatrixTests.cs` (new, 13 tests). **No
    production code changed** — the three mutations above were applied and reverted as falsifiers only.

- [x] ~~**TC1 (original spec)**~~ · **M** · 1–2 files
  - The gap that directly answers the owner's question. Load the **real** `data/tuning/aptitudes.v2.json`
    (the file the injector actually loads — **v2, not v1**), enumerate every declared edge, and assert
    each one resolves. Not a spot check on `Might`; the whole edge set, driven from the file so a new
    edge is covered the day it is authored and a deleted reader turns the suite red.
  - **Read function per edge is not uniform — the test must respect that, or it proves nothing.**
    `AptitudeResolverTests` already establishes the three shapes separately: magnitude edges scale with
    `P(Θ)` (`:138` doubling `P(Θ)` doubles the value, `:159` flat at Θ=0), contest edges are Θ-free
    doubles (`:124`), and `recovery`-family edges take the `scaleMilli` dial (`:315`). TC1 asserts the
    **right** function per edge by reading `familyRead`/`recovery.families` from the file, never by
    assuming every edge is a magnitude.
  - **Categorize the 490, and assert the split** — this is the part that makes the RPG/PvZ answer
    explicit rather than implied. The audit's own count, to be recomputed by the test rather than
    pinned as a literal: `progression.bonus.*` (the only family that reaches a Unity field),
    `combat.*` (→ `OverlayCombatCalculator`), `status.*` (→ `ResistanceEvaluator`), `resource.*`
    (→ `ActorResourcePools`), `skill.*`, `move.range`. A channel in none of those categories is a
    **reader-less edge** and the test names it — that is `ReaderCensusTests`' claim, checked here
    against a real resolve instead of a script's parse.
  - Acceptance:
    - [ ] Every edge in the shipped `aptitudes.v2.json` resolves to a **registered** channel — an
      unregistered one throws rather than being silently dropped or zeroed (the behaviour
      `AptitudeResolverTests.cs:103` already pins for one edge, now held for all of them).
    - [ ] All twelve aptitude ids appear as a `source` on at least one edge, computed from the file,
      never a hardcoded roster — a thirteenth aptitude or a renamed one turns this red.
    - [ ] Each edge's value matches its declared read function (`magnitude` / `contest` / recovery-scaled),
      selected from the file's own `familyRead` and `recovery.families`, and checked at two different Θ
      so a magnitude edge silently read as a contest edge fails.
    - [ ] The channel-family split is computed and asserted, with **`progression.bonus.*` named as the
      only family that can reach a PvZ Unity field** — the RPG/PvZ boundary becomes a test, not prose.
    - [ ] Reader-less edges are enumerated and reported, and the count agrees with
      `_meta.measurable` — the same number `ReaderCensusTests` checks, now cross-checked from a resolve.
    - [ ] Overflow: no edge's value is computed by a narrowing multiply — widen before multiplying,
      divide by 1000 last (`CLAUDE.md` numeric-overflow rules; `AptitudeResolverTests.cs:184` already
      proves an oversized coefficient throws rather than wraps for one edge).
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` — new `AptitudeMatrixTests` green, and the
    existing 4625 unchanged (this adds coverage, it must not move a single existing assertion).
  - Files: `tests/FusionRpg.Core.Tests/ClassSystem/AptitudeMatrixTests.cs` (new); possibly a small
    shared loader in `tests/FusionRpg.Core.Tests/TestSupport/` if the shipped-file parse is not already
    reusable from `AptitudeTuningTests`.
  - **Cross-program:** the edges are `class-system`'s data, the question came from `aura-skill`. Built
    here, pointer added to `tasks/class-system-todo.md` so that program's Phase 3 ("Widen to twelve")
    does not keep reading as fully proven.

- [~] **TC2: aura delivery on the lawn — `AuraDeliveryLawnTests`** · **S** · **3 of 3 acceptance criteria MET 2026-08-30; one named hop still open**
  - ### ⛔ The "BLOCKED" label was inherited, and re-checking it against code changed the answer
    TC2 was written as *"BLOCKED... writable the day Wave 6 lands, not before"*, on the grounds that
    `EffectBag.Grant` rejects an unregistered `EffectId`. DESIGN-GATE's own rule — *"test the constraint
    before you declare it"* — was applied, and the blocker split into two halves that are **not** the
    same kind of thing:

    | Half | Verdict | Evidence |
    |---|---|---|
    | Unknown `EffectId` in the catalog | **NOT a blocker for a test** | `EffectBag` takes an `IEffectCatalog` by ctor injection (`EffectBag.cs:144-150`); `EffectBagTests.cs:121` already registers its own defs via `InMemoryEffectCatalog`. This is a **live content** gap, not a testability wall. |
    | Derived overlay keys refused | **REAL blocker, and it is elsewhere entirely** | `EffectOverlayMerge.AllowedByAction` (`EffectProcAndOwner.cs:130-154`) whitelists overlay keys per action across **ten** actions — `ModifyStat`, `ApplyStatus`, `ClearStatus`, `SpawnEntity`, `BoardAction`, `SpawnGridItem`, `ClearGridItem`, `SetBoxType`, `Economy`, `ApplyResourceDelta` — and **none is a derived-stat action**, so `derived.channel` is refused as `unknown overlay key`. |

    So the true blocker is **not** `EffectBag.cs:196` (the line the todo and the spec both cite) — it is
    the missing action in the whitelist. Adding one needs a sink executor, param schema, registry row
    and content validation: `effect-atom` Wave 6 / E20-E25, another program's module. Correcting the
    citation matters, because the previously-recorded one would have sent the next session to the wrong
    file.
  - ### What was built, and it is most of TC2
    `tests/FusionRpg.Core.Tests/Battle/AuraDeliveryLawnTests.cs` — **7 tests, 7 green.** Everything
    downstream of the grant hop, through **real production code** (`GrantedDerivedAtomReader` →
    `AtomDerivedSubsystem` → `ActorHub`) with real plant/zombie `StatContext`s. The only substituted
    piece is the **grant transport**: grants go into a real `InMemoryEffectGrantStore` directly rather
    than arriving via `EffectBag.Grant`.
  - Acceptance — all three criteria met:
    - [x] *"An active aura raises `combat.power.omni` on a lawn plant... matching the value
      `AuraDeliveryTests` asserts for the same rung/share/Θ on the Sim host — **the two hosts agreeing
      is the point**."* — `An_active_aura_raises_combat_power_omni_on_a_lawn_plant_by_the_same_T10_value_the_sim_host_uses`
      uses the **identical** `AuraMagnitude.Compute(rung: 10, share: 1.0, pTheta: 1_000_000, …)` call
      and the **same two tunings** the Sim-host twin uses, so it is a like-for-like comparison, not two
      differently-configured ladders. Plus `A_type_scoped_aura_reaches_every_plant_of_that_type` (the
      lawn twin of "every friendly squad actor") and `A_match_scoped_aura_also_reaches_a_lawn_plant`.
    - [x] *"The aura never touches the zombie side on the lawn"* —
      `A_plant_side_aura_never_touches_a_zombie_even_at_the_same_type_id`, deliberately at the **same
      type id**, which is what a naive owner-key match gets wrong. It also asserts the plant IS buffed
      in the same pair, so it cannot pass by the aura reaching nobody.
    - [x] *"With no active auras, the lawn resolve is byte-identical"* — `Absent_any_aura_the_lawn_resolve_is_unchanged`
      asserts `ReferenceEquals(AppliedCombat, RuntimePrimary)`, plus `Withdrawing_the_aura_returns_the_channel_to_zero`
      for the disable half (an aura that cannot be turned off is a different feature).
  - **Falsifier:** ignoring `ctx.Side` in the owner-scope lookup (`sideKind` pinned to `"plant"`) →
    **2 failures**, `A_plant_side_aura_never_touches_a_zombie_even_at_the_same_type_id` and
    `GrantedDerivedAtomReaderTests.Side_selects_the_type_scope_a_zombie_never_picks_up_plant_type_grants`.
    Reverted; residue check clean.
  - ### The remaining hop — pinned as a failing-when-fixed test, not left as prose
    `The_remaining_gap_a_derived_overlay_is_still_refused_by_every_shipped_effect_action` asserts that
    `EffectOverlayMerge.TryValidateOverlayForDef` **rejects** a derived overlay for every shipped
    action. **When Wave 6 lands, that test starts failing** — deliberately. Its own message says so:
    *"Wave 6 has landed; delete this test and write the real end-to-end grant test that closes TC2."*
    A tripwire beats a TODO comment: the gap now announces its own closure instead of waiting to be
    remembered.
  - **Still open, precisely:** that one hop — a real authored aura compiling to a derived-stat effect
    and surviving `EffectBag.Grant`'s overlay validation — plus **A5**'s live on-the-lawn proof in
    `docs/architecture/effect-atom/spec-derived-write-lawn.md`. Nothing else.
  - Files: `tests/FusionRpg.Core.Tests/Battle/AuraDeliveryLawnTests.cs` (new, 7 tests). No production
    code changed.

- [x] ~~**TC2 (original spec)**~~ · **S** · ⛔ **BLOCKED, not deferred**
  - Today `AuraDeliveryTests` proves an active aura raises `combat.power.omni` on every friendly squad
    actor **in the Sim host**. The identical proof on the lawn cannot be written yet: `EffectBag.Grant`
    rejects an unregistered `EffectId` (verified live — `unknown effect_id: aura.might.live`), so no
    aura can reach a lawn entity through the grant path at all.
  - **This is a sequencing fact, not a coverage decision.** The blocker is the atom→compile→bind chain
    (`effect-atom` Wave 6 / E20–E25). The lawn executor half is already built and registered
    (`AtomDerivedSubsystem` + `GrantedDerivedAtoms`, `decisions.md` "Derived-write lawn executor"),
    so what is missing is the bind pipeline in front of it — a **wiring gap**, with the consumer
    already in place.
  - Acceptance (writable the day Wave 6 lands, not before):
    - [ ] An active aura raises `combat.power.omni` on a **lawn** plant, asserted through
      `ActorHub.Resolve` with a real `StatContext`, matching the value `AuraDeliveryTests` asserts for
      the same rung/share/Θ on the Sim host — the two hosts agreeing is the point.
    - [ ] The aura never touches the zombie side on the lawn (the lawn twin of `AuraDeliveryTests`'
      own enemy-side fact).
    - [ ] With no active auras, the lawn resolve is byte-identical to before — no golden moves.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`, plus a live lawn proof via the
    `live-lawn-quick-start` skill (this is spec-derived-write-lawn's **A5**, still open there).
  - Files: `tests/FusionRpg.Core.Tests/Battle/AuraDeliveryLawnTests.cs` (new, once unblocked).
  - **Cross-program:** blocked on `tasks/effect-atom-todo.md` Wave 6. Pointer added there.

- [x] **TC3: the PvZ-engine write had no reachable test** · **M** · **DONE 2026-08-30 — by extraction, not by a new project**
  - ### The decision: no `FusionRpg.Injector.Tests` project was created, and that is the right outcome
    TC3's own text made this conditional: *"check whether the injector is testable before scoping the
    project… if `EntityApply`'s decision logic cannot be reached without a Unity host, the correct
    outcome is to **extract the decision** into a Unity-free type… not to declare the whole class
    untestable and move on."* Checked, and it cannot:
    - `FusionRpg.Injector.BepInEx.csproj` targets `net6.0` and `<Reference>`s `0Harmony`,
      `BepInEx.Core`, `BepInEx.Unity.IL2CPP` and `Il2CppInterop.Runtime` by `HintPath` into
      `$(BepGameDir)\BepInEx\core\` — **building it requires a real PVZ Fusion install.**
    - `EntityApply.RunPlant(Plant p, …)` takes an Il2Cpp `Plant`; it cannot even be named off-host.
    - `ci.yml`'s "Restore / test (.NET)" step names **ten** test projects — none is the injector, and
      the repo forbids committing the game binary, so it never can be.
    A test project CI cannot build is worse than none: it would be a green checkbox nobody runs.
    **Extraction was done instead**, which puts the logic somewhere CI already runs every push.
  - ### What moved, and the duplication it removed
    | New Core type | From | Why it could move |
    |---|---|---|
    | `Core/Stats/EntityWriteGate.cs` | inline in `EntityApply.RunPlant` **and** `RunZombie` — *two copies* | pure function of `EntityFinal` + `EntityBaseline` + the source tag |
    | `Core/Stats/Derived/Subsystems/GrantedDerivedAtomReader.cs` | `Injector/Stats/GrantedDerivedAtoms.cs` | `IEffectGrantStore`/`EffectGrant` are Core.Effects, `EffectOwnerKeys` is Contracts, `StatContext` is Core — **nothing Unity-typed was involved** |
    `GrantedDerivedAtoms` is now a 12-line adapter whose only remaining job is reaching the live
    `EffectRuntime.Bag` static — genuinely host-specific, correctly left behind. The write gate is now
    stated **once** instead of twice.
  - ### ⭐ Falsifier — the FA1/FA10 collision guard, which previously had a comment and no test
    Un-namespacing the overlay keys back to bare `channel`/`op`/`amount` (the original bug):

    | | Failures |
    |---|---|
    | First run (as written) | **2** — both FA1 cases |
    | After correcting the overclaim (below) | **3** — `An_FA1_ModifyStat_grant_is_not_consumed_as_a_derived_atom`, `An_FA1_grant_and_a_derived_grant_side_by_side_yield_only_the_derived_one`, `An_FA10_grant_that_grows_an_op_key_still_yields_nothing` |

  - ### ⛔ Second defect found in this task's own work, by running the falsifier
    The FA10 test **passed vacuously**. FA10's real params (`ExecApplyResourceDelta`) carry `channel`,
    `amount`, `targetPtr` but **no `op`**, so even the buggy bare-key reader skipped it at the op check
    — FA10 was never actually at risk, and the doc comment claiming this test proved otherwise was
    wrong. **FA1 is the real collision.** Corrected the comment to say so, and added
    `An_FA10_grant_that_grows_an_op_key_still_yields_nothing` to cover the shape that *would* collide
    if FA10 ever grows an `op` param. Found by measuring the falsifier's output instead of assuming
    coverage from a green run.
  - ### ⛔ A real flake found and fixed during the verification sweep (not mine, but not left)
    `AuraRuntimeEndpointsTests.Disable_anActiveAura_removesItAndReflectsInGet` failed once with
    `409 Conflict` on its own setup, then passed 3/3 isolated and 2/2 full-suite — a cross-class race.
    Root-caused **in code**: `AuraRuntimeEndpoints` holds session state in a bare static keyed by
    playerId (and `AuraTuningHub.Configure` is a second process-global); every test builds a fresh
    SQLite file, and a fresh file restarts autoincrement, so `GetCurrentPlayerId()` returns **1 in
    every class**. `AuraRuntimeEndpointsTests` defends with a per-test `ResetForTests()` — which only
    holds *within* a class, and **`CommanderListEndpointsTests` (new, 2026-08-30) calls the same two
    statics**. xUnit parallelises across classes, so one class wipes the other's aura mid-test.
    Fixed at the root with `tests/FusionRpg.Server.Tests/AssemblyParallelism.cs`
    (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`) — an assembly-wide switch
    rather than a two-class `[Collection]`, because the hazard is structural (process-global hubs +
    always-player-1) and a collection would stop covering the next class to touch them. **Cost stated
    honestly: the suite goes ~6s → ~15-20s**, accepted because an intermittently red suite hides real
    failures. Deliberately touched **neither** `AuraRuntimeEndpoints.cs` nor `CommanderListEndpointsTests.cs`
    — both are the owner's concurrently-edited commander-surface work. Verified **5 consecutive green
    runs**, 80/80.
  - Acceptance — every box verified:
    - [x] Regression test for the original defect (a derived-only producer, no other contributor, still
      writes) — `EntityWriteGateTests.A_value_change_from_any_source_writes_on_an_unforced_source`,
      alongside the pre-existing `AppliedCombatReachesWriterTests` cases.
    - [x] `DiffersFrom` proven field-by-field — **already covered** by
      `AppliedCombatReachesWriterTests.Any_single_composed_field_moving_is_enough_to_trigger_a_write`,
      a `[Theory]` over **12** fields (this todo said "eleven" — the file is the authority, corrected
      here). Cited, deliberately not duplicated.
    - [x] Vanilla identity view pinned — `EntityWriteGateTests.The_vanilla_defense_identity_view_is_pinned`
      `[Theory]`: `DefensePercent != 1f` or `DefenseFlat != 0` writes, `(1f, 0)` does not.
    - [x] `forceReapply` writes even when nothing differs — `A_forced_source_writes_even_when_nothing_differs`
      over 5 real source tags, with `An_ordinary_source_with_no_value_change_does_not_write` as the
      complement so it cannot pass by always writing, plus `Forced_source_matching_is_ordinal_and_case_sensitive`.
    - [x] FA1/FA10 overlay-key namespacing guarded by a real test — see the falsifier table above.
  - Verify — **executed, not asserted**:
    - All ten CI test projects green: **Core 4776, Guard 116, Data 548, CheatCore 40, Server 80,
      Launcher 162, AtomImporter 22, ElementEnumGen 14, ItemSeedValidator 71, E2E 195 — 6,024 tests,
      0 failures.**
    - Four boundary guards green: `guard-single-writer`, `guard-secondary-no-unity`,
      `guard-funnel-delta`, `guard-dal`.
    - **Injector really builds** against the real game (`-p:MlGameDir="H:\Games\PVZ-Fusion-3.9_MelonLoader"`).
      Not a hollow success: the first attempt **failed** with `CS0246: IEffectGrantStore could not be
      found` (a missing `using FusionRpg.Core.Effects` in the slimmed adapter), fixed, then succeeded —
      the error→success transition proves compilation actually ran.
    - Goldens: `--filter Golden` 35/35 green. The three `docs/research/class-system/_baseline-*.json`
      files show as modified, but the **only** changed line in each is `measuredAt` — no measured value
      moved; `ClassSystemBaselineRegenTests` rewrites the timestamp whenever Guard.Tests runs.
    - `audit-magic-numbers.py --summary`: **M1 = 0** (the hard gate). `audit-overflow.py`: **0 critical,
      A1 = 0, A2 = 0.**
  - ### ⚠️ One box deliberately not ticked, because it became moot
    TC3 warned that adding a project to CI could fail silently, since `ci.yml`'s test step runs ten
    `dotnet test` calls with no exit-code check between them — **only the last one decides the step**
    (a real 2026-08-24 incident, documented in `ci.yml`'s own comment at line 47). **No project was
    added, so nothing was appended to that step and no CI edit was needed.** The underlying CI defect
    is still there and still real — it is `ci.yml`'s, not this program's, and is left named rather than
    quietly fixed inside an unrelated task.
  - Files: `src/FusionRpg.Core/Stats/EntityWriteGate.cs` (new), `src/FusionRpg.Core/Stats/Derived/Subsystems/GrantedDerivedAtomReader.cs`
    (new), `src/FusionRpg.Injector/Stats/GrantedDerivedAtoms.cs` (slimmed to an adapter),
    `src/FusionRpg.Injector/Stats/EntityApply.cs` (both call sites delegate; two duplicate
    `forceReapply` derivations removed), `tests/FusionRpg.Core.Tests/Stats/EntityWriteGateTests.cs`
    (new, 16), `tests/FusionRpg.Core.Tests/Stats/GrantedDerivedAtomReaderTests.cs` (new, 23),
    `tests/FusionRpg.Server.Tests/AssemblyParallelism.cs` (new).

- [x] ~~**TC3 (original spec)**~~ · **M** · new project
  - **The real hole, and the one with an incident attached.** The write-gate bug the owner reported
    ("delta hp bonus doesn't reach the game, plant still has 300hp") lived in `EntityApply.cs` and was
    found by the owner **playing the game**. There is no `FusionRpg.Injector.Tests` project, so today
    nothing would catch its regression. `AppliedCombatReachesWriterTests` (Core, 15 facts) proves the
    *gate* is source-agnostic — that is the fix's own regression test and it is real — but it stops at
    Core's boundary and never touches the Unity write.
  - **Check whether the injector is testable before scoping the project, and say so honestly if it is
    not.** `FusionRpg.Injector` references Unity interop assemblies; the guards
    (`guard-secondary-no-unity.ps1`) exist precisely because that dependency is load-bearing. If
    `EntityApply`'s decision logic cannot be reached without a Unity host, the correct outcome is to
    **extract the decision** (which fields differ, what gets written) into a Unity-free type that Core
    or a new test project can drive, leaving only the field pokes in the untestable shell — not to
    declare the whole class untestable and move on.
  - Acceptance:
    - [ ] A regression test for the original defect: a producer contributing **only** through a derived
      channel (no primary modifier at all) still causes a write — the exact case the old
      contributor-enumerating gate dropped.
    - [ ] `DiffersFrom` is proven field-by-field: each of the eleven compared fields, changed alone,
      triggers a write; an identical pair does not.
    - [ ] The vanilla identity view is pinned — `DefensePercent == 1f` and `DefenseFlat == 0` mean "no
      change", so a baseline carrying no defense fields never produces a spurious write.
    - [ ] `forceReapply` writes even when nothing differs.
    - [ ] The `stat.derived` overlay-key namespacing is guarded: an **FA1 `ModifyStat`** grant and an
      **FA10 `ApplyResourceDelta`** grant are **not** consumed as derived mods. This is the collision
      caught before shipping (`GrantedDerivedAtoms.cs:28-44`) — it currently has a comment explaining
      it and no test enforcing it.
  - Verify: `dotnet test tests\FusionRpg.Injector.Tests`, plus the four boundary guards still green
    (`guard-single-writer.ps1`, `guard-secondary-no-unity.ps1`, `guard-funnel-delta.ps1`,
    `guard-dal.ps1`) — a new test project must not become a way around the single-writer rule.
  - Files: `tests/FusionRpg.Injector.Tests/` (new project + csproj), `EntityApplyTests.cs`,
    `GrantedDerivedAtomsTests.cs`; possibly an extraction edit in
    `src/FusionRpg.Injector/Stats/EntityApply.cs` if the logic proves unreachable without Unity.
  - ⚠️ **Adding a test project touches CI.** `ci.yml`'s test step only checks the **last**
    `dotnet test`'s exit code — a new project appended there can fail silently. Fix the step or add
    the project where its failure is actually observed; do not append blindly.

### ✅ Checkpoint 6 — the coverage claim becomes true — **PASSED 2026-08-30**
- [x] TC1 green: all twelve aptitudes proven over the real shipped edge set (**486 edges**), at two Θ,
  with the channel-family split asserted and `progression.bonus.*` proven — **behaviourally, through
  `ActorHub`** — the only PvZ-reaching family. 13 tests.
- [x] TC3 green: the write gate has a regression test that does not depend on the owner playing the
  game. `EntityWriteGate` + `GrantedDerivedAtomReader` extracted to Core so CI can actually reach them;
  39 tests. The FA1 overlay-key collision, previously guarded by a comment, now has a real test.
- [x] TC2: **3 of 3 acceptance criteria met** (7 tests), with the one remaining hop named to a precise
  `file:line` — and the citation the todo previously carried **corrected**: the structural blocker is
  `EffectOverlayMerge.AllowedByAction` (`EffectProcAndOwner.cs:130-154`), not `EffectBag.cs:196`.
  Pinned by a tripwire test that starts failing when Wave 6 lands. Never silently open.
- [x] Checkpoint 5's own coverage sentence corrected in place, so no future session reads
  "full suites green" as "the twelve aptitudes are covered" — **done 2026-08-30**, warning block added
  directly under the Checkpoint 5 header rather than left for TC1 to remember.
- [x] Full suites still green with nothing moved — **all ten CI test projects, executed 2026-08-30:**

  | Project | Result | | Project | Result |
  |---|---|---|---|---|
  | Core | **4783** ✅ | | Launcher | **162** ✅ |
  | Guard | **116** ✅ | | AtomImporter | **22** ✅ |
  | Data | **548** ✅ | | ElementEnumGen | **14** ✅ |
  | CheatCore | **40** ✅ | | ItemSeedValidator | **71** ✅ |
  | Server | **80** ✅ | | E2E | **195** ✅ |

  **6,031 tests, 0 failures** (Core was 4,723 before this phase: **+60** from TC1's 13, TC3's 39 and
  TC2's 7, plus the one added during TC3's own review correction).
- [x] Four boundary guards green: `guard-single-writer`, `guard-secondary-no-unity`,
  `guard-funnel-delta`, `guard-dal`. One of them (**funnel-delta**) genuinely caught a violation
  introduced by this phase — `EntityWriteGate.cs`'s doc comment named `EntityStatWriter`, which Core
  must not reference — **reworded rather than the guard weakened**, then re-run green.
- [x] Injector really builds against the real game
  (`-p:MlGameDir="H:\Games\PVZ-Fusion-3.9_MelonLoader"`), proven non-hollow by an error→success
  transition (a genuine `CS0246` was caught and fixed first).
- [x] No goldens moved: `--filter Golden` 35/35; the only diff in the three
  `docs/research/class-system/_baseline-*.json` files is `measuredAt`, rewritten by
  `ClassSystemBaselineRegenTests` whenever Guard.Tests runs — no measured value changed.
- [x] `audit-magic-numbers.py`: **M1 = 0**. `audit-overflow.py`: **0 critical, A1 = 0, A2 = 0.**
- [x] Every falsifier reverted; `grep -rn "FALSIFIER" src/ tests/` → clean.

---

## Final proof — every audit requirement mapped to evidence, 2026-08-30

Both audit files (`aura-skill-plan.md` + `aura-skill-todo.md`) re-read end to end after Phase 5. Every
requirement, and where its evidence actually is:

| Audit requirement | Where satisfied | Evidence |
|---|---|---|
| **Gate A — magnitude** (plan §"three gates") | `AuraMagnitudeTests` | 7 tests; hand-computed value at rung 7, all four rungs 7/8/9/10 exercised, rung 3 refused |
| **Gate B — delivery** | `AuraDeliveryTests` (Sim) **+ `AuraDeliveryLawnTests` (lawn, new)** | 4 + 7 tests; the two hosts now proven to agree on the same `AuraMagnitude.Compute` value |
| **Gate C — toggle** | `AuraRuntimeTests.Disable_removes_an_active_aura`, `AuraDeliveryLawnTests.Withdrawing_the_aura_returns_the_channel_to_zero` | disable returns the channel to its prior value on both hosts |
| Phases 0–4, Checkpoints 1–5 | pre-existing, owner-verified | unchanged by Phase 5; Checkpoint 5 now carries an honest coverage warning it lacked |
| **Phase 5 TC1** — twelve aptitudes, real edge set | `AptitudeMatrixTests` | 13 tests, **all 486 edges**, two Θ; falsifiers proved a dropped edge and a leaking bridge channel are invisible to the 65 pre-existing aptitude tests and 496 ActorHub tests |
| **Phase 5 TC2** — lawn delivery | `AuraDeliveryLawnTests` | 3/3 criteria met; one hop open, blocker citation corrected, pinned by a tripwire test |
| **Phase 5 TC3** — the PvZ write | `EntityWriteGateTests` + `GrantedDerivedAtomReaderTests` | 39 tests; logic extracted to Core so CI can reach it; FA1 collision guard now real |
| **Checkpoint 6** | above | PASSED |
| Plan risk: "T4 moves goldens" | goldens 35/35, no measured value moved | only `measuredAt` differs in the three baselines |
| Plan risk: "`guard-class-system.ps1` currently red (G3)" | **still exactly G3 Might + G3 Ferocity, nothing else** | verified by reading the guard's real output, not assumed — the known permanent red (decision 12) |
| Plan verification: `guard-power.ps1` | **OK** | "one ladder, pin holds, no private f(level)" |
| Plan verification: four boundary guards | **all OK** | funnel-delta genuinely caught a violation this phase introduced; reworded, not weakened |
| Plan verification: `audit-magic-numbers --targets M1` | **M1 = 0** | |
| Plan verification: `audit-overflow` | **0 critical, A1 = 0, A2 = 0** | |
| Plan open question 1 (`commanderOnly`) | **still open — OWNER decision** | re-verified: authored in item seed data, **zero consumers in `src/`** |
| Plan open question 2 (W4) | **CLOSED — the question was stale** | all 5 call sites pass `actorResolve`; `EffectRuntime.cs:436` assigns a real resolver |
| Plan open question 3 (`patron.aura`) | closed → T22 | |
| Deferred: Zomboss dynamic AI | **explicitly out of scope for this program** by the audit's own text | needs its own capability map |

**Remaining open, in full — nothing else:**

1. **TC2's last hop** — a real authored aura surviving `EffectBag.Grant`'s overlay validation. Blocked
   on `effect-atom` Wave 6 / E20-E25 adding a derived-stat effect action
   (`EffectOverlayMerge.AllowedByAction`, `EffectProcAndOwner.cs:130-154`). Carries a self-announcing
   tripwire test.
2. **A5** — the live on-the-lawn probe, same dependency.
3. **`commanderOnly`** — an owner design decision, non-blocking, unchanged in scope.
4. **G3** — the known permanent, by-design guard red (class-system decision 12), unrelated to auras.

Items 1 and 2 are one dependency in another program; 3 is a decision only the owner can make; 4 is
decided-and-permanent. **No engineering work inside this program's scope remains unresolved.**

---

## Deferred — named, not scheduled

- ⭐ **Zomboss AI: dynamic aura and skill control.** Owner, 2026-08-30: *"reverse for lawn game zomboss
  AI later… he will cast skill and control aura and some effect in game depend advantage/disadvantage,
  so this is big feature, we need reverse control API or something for him."*

  **Explicitly out of scope for this program.** T17 ships the static half — a pattern names an aura, and
  it holds for the match. The dynamic half is a genuinely large feature: Zomboss reading board
  advantage and choosing when to cast, swap auras, and apply effects, which needs a control surface the
  repo does not have (the AI side is a static wave list today — `WaveCatalog.cs:67-80` never sets
  `ChannelMods`, `EquippedActionIds` or `InitialStatuses`). It should get its own capability map and
  ideal doc, built alongside Zomboss's action skills rather than bolted onto auras.

  Recorded here so it is not rediscovered as a surprise, and so nobody quietly scope-creeps T17 into it.

## Still open (non-blocking)

1. ~~**`patron.aura` becomes irrelevant**~~ — **decided 2026-08-30: give it a `P(Θ)` term.** Now T22.
2. ~~**The rule collision**~~ — **closed 2026-08-30.** All six resources are legal costs; auras stay
   `ActionKind.Skill` with flags. Landed in `decisions.md`, `resource-hub-ssot.md` and
   `concrete-action-roster.md` before the aura work. **No blockers remain.**

### Reconciled against `aura-skill-plan.md` §"Open questions (owner)" — 2026-08-30

The final-proof pass re-read the plan and found this list had drifted from it. The plan carried three
non-blocking open questions; this section listed different ones and said *"No blockers remain."*
Reconciled, each checked against code rather than carried forward:

3. ~~**W4 has a gate and no owner** — *"`aura-content` gates Retribution on it and `aura-surface` tests
   it; nothing fixes it"*~~ — **STALE, now closed. W4 is wired and live.** Verified: `actorResolve` is
   passed at **all five** production `DispatchInstant` call sites (`EffectBag.cs:488`, `:557`,
   `StatusEffectBridge.cs:80`, `:123`, `CheatCommandRunner.cs:1326`) — and it is not an inert null:
   `EffectRuntime.cs:436` assigns `bag.ActorResolve = InjectorCombatBridge.ResolveActor` on the real
   injector, `FoundationHarness.cs:112` does the same for the harness. The plan's wording ("only the
   argument is missing") described a state that no longer exists.
4. ⛔ **The `commanderOnly` item role — genuinely open, and it is an OWNER decision, not engineering.**
   Verified still unresolved: `commanderOnly` appears in authored item seed data
   (`data/seed/items/_registry/classes.v1.json`, `core.v1.json`, `naming.v1.json`,
   `affix-families/g-precision.json`) and in `spec-aura-content.md`, but has **zero consumers in
   `src/`** — a `grep` across all C# returns nothing. So it is a second, unwired answer to *"how does
   the commander buff the squad"*, sitting beside the aura answer. **What needs deciding: whether
   `commanderOnly` atoms stack with aura atoms, and against which budget.** Not engineered around —
   left for the owner, as the plan directs.
