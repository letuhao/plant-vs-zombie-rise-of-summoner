# Spec: requirement-profiles

**Status:** Proposed — module 23 in [item-map.md](../item-map.md). This is the
first spec in the approved requirement-trials-and-maintenance sequence. It
does not implement activation, resource charging, set reconciliation, or UI.

## Objective

Resolve every concrete equipment item's optional requirement profile once and
evaluate its non-resource trial condition without ever making that generated
profile an equip-assignment refusal.

The player can equip a legal item immediately. A profile gives a visible route
to activation; only a future activation module decides whether its effects are
currently active. Strong items may later have maintenance as their resource
tradeoff, but this module does not charge it.

This extends the **item collection and progression** and **level up and power**
loops in [the-loops.md](../../guide/the-loops.md:38-54). It preserves free
aptitude allocation: no player class or new primary-stat vocabulary is added.

### Assumptions to validate

1. A rolled item's `roll_seed`, catalog revision, concrete content `Theta`, and
   atoms are available together at mint; `InstanceProducer` already persists a
   roll seed (`Effects/Atoms/InstanceProducer.cs:149`).
2. Module 24 owns deployment-scoped activation separately from durable
   assignment; this module returns a pure evaluation only.
3. A set seed can later supply a frozen envelope to constrain member profiles;
   module 25 owns that additional input.
4. Existing static frame/faction/slot/legacy-level gates remain in
   `EquipGate` (`Items/EquipGate.cs:54-65`). A generated level profile is a
   trial clause, not `EquipItemFacts.LevelReq`.

## Contract

### Frozen profile

At mint, the resolver persists this exact profile with the concrete item and
its resolver, catalog, and tuning revisions:

```text
RequirementProfile {
  levelTrial?: minimumLevel,
  buildTrial?: fixed(aptitudeId, minimumPoints)
             | ratio(aptitudeId, minimumShareMilli),
  upkeep?: resourceId, reserve, cost, periodTicks,
  profileKind: none | level | fixed | ratio | sustained | jackpot
}
```

`levelTrial`, `buildTrial`, and `upkeep` are composable fields, not competing
runtime modes. Version 1 permits at most one aptitude id per profile. A
`sustained` profile may have no build clause; a focused item selects its target
only from its validated, ordinally sorted build-favor pool.

The profile is an instance fact. Retuning affects future items only; moving an
existing item, reloading, or re-evaluating a loadout never re-resolves it. Its
upkeep clause is durable data, but its due tick, suspension, and HP recovery
latch exist only in module 24's deployment-run status. Any future profile
migration must be explicit and report old and new profiles.

### Deterministic resolver

```text
(rollSeed, resolverRevision, catalogRevision, tuningRevision,
 content Theta, P(Theta), PowerVector, rarityId, buildFavorPool)
  -> RequirementProfile | RequirementProfileRejection
```

The resolver uses `SeededRng.DeriveStream` (`Battle/SeededRng.cs:26`) with one
fixed stream name per draw:

```text
item.requirements:v1:profile
item.requirements:v1:aptitude
item.requirements:v1:threshold
item.requirements:v1:resource
item.requirements:v1:reserve
item.requirements:v1:period
```

Candidates are ordinally sorted and unique before a draw. A non-positive weight
total, duplicate candidate, unknown id, missing revision, impossible matrix
cell, or unavailable required lookup returns a named rejection. It never falls
back to a random profile.

`P(Theta)` selects a tuning-owned power band; `PowerVector`
(`Effects/Atoms/Power/PowerVector.cs:18`) selects a focus band. They are the
only power reads. The resolver creates no level curve and does not use actor
level as a numeric input.

Rarity chooses the distribution-matrix row only. After a profile kind is
selected, threshold, reserve, cost, and period lookups must not receive rarity.
This mechanically preserves rarity overlap while preventing a hidden
rarity-to-cost multiplier.

### Trial evaluator

`RequirementTrialEvaluator` is pure and takes the frozen profile plus
unassisted actor inputs. It returns `ready` or named unmet clauses; it neither
writes an assignment nor toggles effects.

```text
TrialEvaluation {
  ready: bool,
  unmet: level | fixed_aptitude | ratio_aptitude | none
}
```

It reads the twelve-aptitude allocation, never an equippable item contribution.
For a ratio, it must not call the `double` convenience reader
(`AptitudeAllocation.Share`, `Stats/Aptitudes/AptitudeAllocation.cs:81`). It
uses checked `long` arithmetic instead:

```text
aptitudePoints * 1000 >= grandAllocationPoints * minimumShareMilli
```

`1000` is the structural denominator of the persisted per-mille ratio, not a
balance value. An empty allocation fails every positive ratio requirement.
Overflow throws; it is not clamped or coerced to a passing result.

The evaluator deliberately ignores `upkeep`. Module 24 evaluates that
obligation through the shared cost path, whose `TryPay` already provides
all-or-nothing payment (`Actions/Cost/CostLedger.cs:106`). This split prevents
a second resource-payment mechanism.

### Tuning and Seedsmith boundary

All balance values live in a new versioned file:

```text
data/tuning/equipment-requirements.v1.json
```

It owns the rarity × power-band × focus-band profile weights; maintenance
eligibility; requirement, reserve, cost, and period bands; jackpot weight; and
the set-envelope budget consumed later by module 25. Lower power bands give
maintenance zero weight. Rarity alone cannot create upkeep.

Seedsmith may classify a closed build-favor label while generating a seed. The
validator resolves that label to legal aptitude ids and persists the resulting
pool in catalog content. A model never supplies a magnitude, probability,
duration, resolver decision, or runtime input. Invalid classification is
`blocked` and writes no seed, following the pipeline's validate-before-accept
rule (`seedsmith/spec-pipeline.md:52-72`).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RequirementProfile"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AptitudeAllocation"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Power"
python scripts\audit-magic-numbers.py
```

## Project structure

```text
src/FusionRpg.Core/Items/Requirements/
  RequirementProfile.cs             frozen contract and named rejections
  RequirementProfileResolver.cs     seeded resolution and tuning lookup
  RequirementTrialEvaluator.cs      pure, unassisted requirement evaluation
  RequirementProfileTuning.cs       tuning-file loader and validation
src/FusionRpg.Data/Sqlite/
  RpgStore.Items.cs                 later persistence seam; schema change is ask-first
data/tuning/equipment-requirements.v1.json
tests/FusionRpg.Core.Tests/Items/RequirementProfileTests.cs
```

## Code style

```csharp
// A trial reports a route to activation; it is never an equip refusal.
// Ratio comparison stays integer-exact: bounded per-mille is structural,
// while the profile's threshold belongs to tuning.
public static TrialEvaluation EvaluateRatio(long aptitudePoints, long totalPoints,
    long minimumShareMilli)
{
    if (minimumShareMilli < 0 || minimumShareMilli > PerMille) throw new ArgumentOutOfRangeException();
    var ready = checked(aptitudePoints * PerMille) >= checked(totalPoints * minimumShareMilli);
    return ready ? TrialEvaluation.Ready : TrialEvaluation.RatioUnmet;
}
```

Magnitudes are `long`; candidate ordering is `StringComparer.Ordinal`; public
results use named discriminated outcomes instead of null/zero sentinels.

## Testing strategy

| Test | Asserts |
|---|---|
| replay is byte-identical | same complete input creates the same frozen profile |
| isolated streams | an unrelated draw does not change this profile |
| candidate order is irrelevant | shuffled source data resolves identically |
| invalid matrix cell blocks | no fallback profile or partial persistence |
| rarity boundary | identical selected profile inputs yield identical values across rarities |
| low-power maintenance | lower power bands never select upkeep |
| ratio equality and empty allocation | exact boundary passes; empty allocation fails positive floor |
| checked overflow | a magnitude overflow throws |
| unassisted input | equippable effects cannot make their own trial pass |
| profile is not an equip refusal | a legal assignment persists while its trial is unmet |
| Seedsmith numeric ban | classification output cannot contain a balance magnitude |

## Boundaries

**Always:** freeze concrete profiles; use named RNG streams; resolve all
balance values through the tuning revision; sort candidates ordinally; return
named rejections; evaluate build trials from unassisted allocation only.

**Ask first:** adding persistence columns/tables; changing `EquipGate`'s static
gate semantics; adding an aptitude vocabulary entry; changing the closed power
read inventory; adding a resource; or migrating existing profiles.

**Never:** use a model in runtime resolution; use `System.Random`; invent a
private level curve; make a generated profile deny equipment assignment; let
rarity multiply a resolved threshold or upkeep value; clamp a magnitude
overflow; create resource payment outside `CostLedger`; or add a second effect
delivery path.

## Success criteria

- [ ] Every valid concrete input resolves to one frozen, replayable profile or
      one named rejection.
- [ ] A generated requirement cannot turn an otherwise legal assignment into a
      refusal; an unmet trial is observable as evaluation data.
- [ ] Ratios are integer-exact, scope-summed, unassisted, and overflow-safe.
- [ ] All balance values come from `equipment-requirements.v1.json`; source
      contains no balance literals.
- [ ] Rarity changes profile distribution only, never post-selection values.
- [ ] Invalid Seedsmith classification or tuning blocks acceptance with no
      partial profile.

## Design-gate record

- [x] Subsystems: items, Seedsmith, aptitudes, resources, and power.
- [x] Read the applicable item, seed, power, resource, tuning, and action-cost
      contracts in this session.
- [x] Verified the critical seams in `EquipGate`, `AptitudeAllocation`,
      `SeededRng`, `PowerVector`, and `CostLedger` against code.
- [x] No actor magnitude is composed here; the module consumes existing
      aptitude allocation and power reads only.
- [ ] Schema and runtime activation ownership remain follow-up modules and are
      intentionally not locked by this spec.
