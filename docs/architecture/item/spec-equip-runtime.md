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

## Boundaries

**Always:** route combat writes through `EntityStatWriter` and HP deltas through the Funnel; rebuild
rather than patch; print coverage with any balance verdict.

**Ask first:** flipping the `Sim` runtime for `stat.derived` — it needs a real consumer first.

**Never:** add a second delivery path for a value `DerivedComposer` already folds
(`AtomDerivedSubsystem`'s own reasoning). Never let an item's contribution reach an actor outside the
projection — two writers is the defect the single-writer guard exists to catch.

## Success criteria

- [ ] ⭐ One hand-made item on one actor measurably changes a number **in battle and on the lawn**.
- [ ] `UniqueActor` bindings reach `AtomPushService`; the write-only state is gone.
- [ ] Unequip removes the contribution with no residue.
- [ ] All four boundary guards green.
- [ ] The first geared corner run executes, **termination stays green**, and dominance reports with
      its coverage line.
