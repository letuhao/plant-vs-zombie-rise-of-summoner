# Spec: `equip-runtime`

**Module id:** `equip-runtime` · **Program:** [item](../item-map.md) · **Build order:** 5 of 21 — ⭐ **the payoff**
**Depends on:** `equip-assign` (4)
**Rulings:** D29 · closes wiring gap **W2** and the consume half of **§2f.1 F2**

## Objective

**Make an equipped item change a number in a real fight.** After this module, one hand-made item on
one actor is observable on the lawn and in battle. Everything before it is plumbing with no visible
effect; everything after it is content and depth.

> ⭐ **This is the earliest point the program can prove itself end to end**, and it is deliberately at
> module 5 of 21 — the same discipline `effect-pipeline` used when it put its producer at module 4 of
> 10.

## Design

### What is actually missing — and it is small

**Not a mechanism. Two missing calls.**

| Half | State |
|---|---|
| **Produce + bind** | ✅ live — `ProduceAndBind` at `RpgStore.UniqueActors.cs:756`, inside the equipment-binding sync |
| **Consume** | ⛔ **`UniqueActor` bindings are write-only.** `RpgHub.cs:106` builds an `AtomPushService` push for `OwnerKind.Player` and nothing else, so no item binding ever reaches an actor — `decisions.md:106` states this outright: *"the binding is currently write-only … the legacy `mods_json`/`loadoutJson` grant remains the only path an actual spawned unique actor's stats take."* |

⚠ **§2a's B6 claimed *"the atom runtime is not inert any more."* That was half true** and is corrected
in §2f.1 F2. The produce half runs; this module is the consume half.

### Battle — the seam exists and has a working producer to copy

`BattleStatComposer` folds `ChannelMods` at squad build, and the field is documented *"trait stat
mods, equipment later"* (`BattleModels.cs:33`, `BattleStatComposer.cs:9`).

**`TraitAtomSource` is a working producer on that exact seam** — E12 shipped it so a trait's bound
`stat.derived` atoms merge at compose time. An equipment producer is **the same shape**, reading the
projection module 4 builds rather than a trait catalog. That is what makes W2 a wiring gap and not an
architectural limit.

### Lawn — the executor already exists

`stat.derived` is `RuntimeSupportMatrix(Full, Full, None)` (`AtomKindRegistry.cs:255`), and
`AtomDerivedSubsystem` is registered on the injector's `ActorHub` at the reserved order-350
`foundation.effect` slot (`ActorHub.cs:155`). It takes bound `stat.derived` atoms through an injected
per-`owner_key` delegate.

**So the lawn needs the delegate to see `UniqueActor` bindings.** No new subsystem, no new ordering
band.

### `Sim` stays `None`, deliberately

`SimEffectHost` has no consumer, and flipping it on the strength of the other two would recreate D6's
original cause — a bind accepted and then doing nothing forever. ⚠ **Consequence worth stating:**
CombatSim cannot read item effects, so item balance cannot be simulated there until it does.

> ⚠ **Amended 2026-09-06 — the premise above expired, and the outcome is the one this section wanted.**
> `mechanism-wiring` E5 gave `SimEffectHost` the real consumer this section said it lacked
> (`ActorDerivedLookup`'s contribution fold, reached through
> `SimEffectHost`/`FoundationHarness.ContributeDerived`), so `stat.derived` now reads
> **`Partial`/`Full`/`Full`** for Sim/Battle/Lawn, not `None`/`Full`/`Full`. This module's own test
> asserts the live matrix rather than the old constant —
> `Sim_runtime_opens_partially_and_the_spec_says_why`, `EquipRuntimeTests.cs`. `Partial`, not `Full`,
> because the fold is a plain sum honouring `Flat`/`Increased` and not `Replace`/`Flag`. **So
> `tools/CombatSim` CAN now simulate an item's `Flat`/`Increased` channels**; a `Replace`/`Flag`-
> authored item still composes wrong there until the fold routes through the real `DerivedComposer`.
> Nothing in this module was flipped to get there — the kind was re-opened by its own consumer
> landing, which is exactly the order D6 requires.

### ⭐ Amended 2026-09-07 — the "Never: second delivery path" boundary is revised, for a concrete reason

**What changed since this spec was written.** This module's own design (§ above, and the code-style
example below) deliberately scoped `EquipAtomSource` to `stat.derived` only, and its own Boundaries
section forbade adding a second delivery path for a value `DerivedComposer` already folds. That was a
reasonable call **on the assumption that equip content would be authored as `stat.derived`.** It
wasn't: measured directly against the shipped corpus, virtually all real equip content — the entire
109-family affix corpus and every relic atom in `unique-equip.json` — is `stat.modify`, not
`stat.derived`. Module 4/5's binding half now works correctly (`RpgStore.MaterializeRolledEquipRuntime`,
2026-09-07 — a rolled item's `effect_binding` rows are real and correctly scoped), but
`EquipAtomSource.EquippedDerived`'s `stat.derived`-only filter silently drops every one of them, so an
equipped item still changes zero battle numbers for any content that actually ships.

**Why "just re-author the content as `stat.derived`" is not the fix.** `AtomDerivedSubsystem.TryParseOp`
is a closed four-op set (`flat`/`increased`/`replace`/`flag`). Measured directly: the shipped
`family-expand.g-life.json` and `family-expand.g-attack.json` corpora both author real rows with
`"op": "more"` — an op `TryParseOp` does not recognise and `EquipAtomSource.ModsFor`'s own doc comment
already names as a content error to skip. Re-authoring that content away from `more` would be a real
balance change to already-shipped, already-verified content, not a technical rename — and widening
`DerivedComposer`'s own op vocabulary would touch a shared system every OTHER `stat.derived` consumer
in the repo depends on, for equip's benefit alone.

**Why the second path is not new machinery.** `stat.modify` already has a real, shipped, tested
consumption path with **no relationship to `EquipAtomSource`/`BattleStatComposer` at all**:
`AtomCompiler.Compile` → `EffectDefDto`/`EffectGrantDto` → `BattleEffects.ExecModifyStat` →
`BattleStatModifierLedger` → `ActorState.LiveAtk`/`Derived` — and it already handles `flat`/
`increased`/`more` correctly (`AtomCompiler.ToOpcodeShape`). It is exactly the mechanism
`ActionContainerEffectResolverFactory.Build`/`RegisterInto` already drives for action-granted atoms,
proven live at three real production call sites (`WebMatchService.cs:134,192,328`, inside
`BattleEngine.Resolve`'s `onEffectHostReady` hook) — atom-kind-agnostic by construction (`Build` calls
plain `AtomCompiler.Compile`, no kind special-casing). The gap is narrow: nothing has ever pointed that
same compiler/registration pattern at a specimen's EQUIP bindings instead of (or alongside) its
action-container bindings.

**The revised boundary.** "Never add a second delivery path" now reads: **never add a delivery path
for a value the FIRST path (the `stat.derived` composer-fold) can already carry correctly.** A second
path for a value the first path structurally cannot carry (a closed op vocabulary refusing real,
shipped content) is not a duplicate — it is the value's only path. The two producers read disjoint atom
kinds (`stat.derived` vs `stat.modify`) off the SAME binding set (module 4's projection); neither can
double-count the other's contribution because neither iterates the other's kind.

**New objective for this amendment, additive to the module's original one:** a specimen's equipped
`stat.modify` atoms reach the same real battle a granted action's atoms already do, through the same
already-proven compiler/registration pattern — fed by `ResolveBindings` at `UniqueActor` scope, which
module 4/5's 2026-09-07 fix already populates correctly for both rolled and relic equip.

**New/edited files (this amendment only):**
```text
src/FusionRpg.Data/Sqlite/ActionContainerEffectResolverFactory.cs
                                                  EDIT (or a sibling factory) — also resolve a
                                                  specimen's UniqueActor-scoped equip bindings, not only
                                                  action-container bindings that today's Build(_store)
                                                  reads
src/FusionRpg.Server/WebMatchService.cs          EDIT — the three real onEffectHostReady call sites
                                                  (lines 134, 192, 328) register the equip-sourced defs
                                                  alongside the existing action-sourced ones
```
The exact internal shape of "also resolve equip bindings" (extend `Build`'s own query vs. a parallel
factory merged at the call site) is a build-time discovery, not a spec-blocking decision — either
satisfies this amendment's objective as stated.

**New test, additive to the table below:**
| Test | Asserts |
|---|---|
| ⭐ `an_equipped_items_stat_modify_atom_reaches_a_real_battle_through_the_compiler_path` | the actual payoff for real content — a real affix atom (`op: "increased"` or `"more"`), not a synthetic `stat.derived` fixture, changes `DamageDealt`/a derived channel geared vs. bare, mirroring `BattleLiveStatModifiersTests`'s own existing proof shape for the non-equip case |
| `equip_sourced_and_action_sourced_defs_coexist_in_one_battle_without_collision` | the two `RegisterInto` sources (equip, action-granted) don't clash on `EffectHost` registration |

### ⭐ D29 — this module is the gate for the first geared corner run

**Item balance is validated by the class-system's existing two guards**, not by an item-specific
ratio:

| | **Termination** | **Dominance** |
|---|---|---|
| Asserts | no pairing of builds that both hold offence has `netAttrition ≤ 0` on both sides | no corner beats every other on win rate, with no clock |
| Repairable later? | **No** — an economy identity; content on top inherits the defect | **Yes** |
| Standing | **HARD — fails the build** | **SOFT — reports with coverage** |

Gear feeds the same derived channels aptitudes do, so it moves the same 144-evaluation corner matrix.
**It can only run once gear reaches battle — which is here.** The first geared run is this module's
acceptance evidence, and it must print coverage alongside verdict, exactly as `spec-balance-guard.md`
§2.1 requires: *"a red row must read as 'the live part of these builds is unbalanced' and never as
'this design is unbalanced.'"*

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipRuntime"
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
# the first geared corner run
dotnet run --project tools\CombatSim -- --corners --with-gear
```

## Project structure

```text
src/FusionRpg.Core/Battle/EquipAtomSource.cs        new — mirrors TraitAtomSource, reads assignments
src/FusionRpg.Core/Battle/BattleStatComposer.cs     EDIT — ChannelMods gains the equipment producer
src/FusionRpg.Server/RpgHub.cs                      EDIT — push UniqueActor bindings, not only Player
src/FusionRpg.Core/Stats/Derived/Subsystems/AtomDerivedSubsystem.cs
                                                    EDIT — delegate sees UniqueActor owner keys
tools/CombatSim/                                    EDIT — geared corners
```

## Code style

```csharp
// The same shape TraitAtomSource already ships (E12): bound stat.derived atoms merge at COMPOSE time,
// a path battle already runs. Equipment differs only in where the bindings come from - the durable
// assignment projection (module 4) rather than a trait catalog. Nothing new in the pipeline.
public IReadOnlyList<BattleChannelMod> ModsFor(long specimenId) =>
    _resolveBindings(OwnerScope.UniqueActor(specimenId))
        .SelectMany(b => b.Atoms)
        .Where(a => a.KindId == "stat.derived")
        .Select(ToChannelMod)
        .ToList();
```

## Testing strategy

| Test | Asserts |
|---|---|
| ⭐ `an_equipped_item_changes_a_battle_number` | the payoff, end to end |
| ⭐ `an_equipped_item_changes_a_lawn_number` | the other runtime |
| `unequipping_removes_the_contribution` | symmetry — the projection is rebuilt, not patched |
| `a_UniqueActor_binding_reaches_AtomPushService` | ⭐ closes the write-only half of F2 |
| `equipment_and_trait_mods_compose_without_double_counting` | two producers on one `ChannelMods` seam |
| `combat_writes_still_go_through_EntityStatWriter` | `guard-single-writer` |
| `hp_deltas_still_go_through_the_Funnel` | `guard-funnel-delta` |
| `sim_runtime_stays_None_and_the_spec_says_why` | the deliberate gap, asserted not assumed |
| `the_geared_corner_run_prints_coverage_with_its_verdict` | D29 / `spec-balance-guard.md` §2.1 |
| `termination_stays_green_with_gear` | ⭐ **HARD** — the one guard no later layer can repair |
| ⭐ `an_equipped_items_stat_modify_atom_reaches_a_real_battle_through_the_compiler_path` | **added 2026-09-07** — the actual payoff for real content (see the amendment above) |
| `equip_sourced_and_action_sourced_defs_coexist_in_one_battle_without_collision` | **added 2026-09-07** |

## Boundaries

**Always:** route combat writes through `EntityStatWriter` and HP deltas through the Funnel; rebuild
rather than patch; print coverage with any balance verdict.

**Ask first:** flipping the `Sim` runtime for `stat.derived` — it needs a real consumer first.

**Never:** add a delivery path for a value the FIRST path can already carry correctly — the original
concern this rule protected against. ⚠ **Revised 2026-09-07**: this does NOT forbid a second path for a
DIFFERENT atom kind the first path structurally cannot carry (`stat.derived`'s closed four-op vocabulary
refusing real, shipped `more`-op content) — that is the value's only path, not a duplicate. It still
forbids exactly what it always did: two writers for the same kind, or an item's contribution reaching an
actor outside the projection.

## Success criteria

- [ ] ⭐ One hand-made item on one actor measurably changes a number **in battle and on the lawn**.
- [ ] `UniqueActor` bindings reach `AtomPushService`; the write-only state is gone.
- [ ] Unequip removes the contribution with no residue.
- [ ] All four boundary guards green.
- [ ] The first geared corner run executes, **termination stays green**, and dominance reports with
      its coverage line.
- [ ] ⭐ **Added 2026-09-07.** A real, shipped `stat.modify` affix atom (not a synthetic fixture),
      including one authored with `op: "more"`, measurably changes a number in a real battle when its
      item is equipped — this is the criterion that makes "item is playable" true for the content that
      actually exists, not just for `stat.derived`'s narrower slice.
