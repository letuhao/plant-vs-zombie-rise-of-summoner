# Spec: set-requirement-reconciliation

**Status:** Proposed — module 25 in [item-map.md](../item-map.md). Depends on
`threshold-grants` (12), `set-charm-gen` (13), and `equipment-activation` (24).

## Objective

Guarantee that every generated set has one deterministic, attainable path to
activate its full effects, and one deterministic answer to who may equip its
members. A player may equip every **eligible** piece regardless of its trial
state; the reconciler prevents separately generated member requirements from
demanding incompatible builds or maintenance resources.

The shipped set evaluator counts distinct equipped roles (`SetEvaluator.cs:37-74`)
and intentionally permits partial sets. This module preserves that count and
adds an activation condition for the set's effects only.

## Contract

### Static set identity requirement

Set identity is resolved before member items are minted. It is a hard
assignment/projection rule owned by I11, not a trial, an upkeep clause, or a
set-bonus activation condition:

```text
SetIdentityRequirement {
  setId, setClass,
  requiredFamilyId?: string,
  requiredSpeciesId?: string,
  requiresUniqueDemon: bool,
  hybridEligibility: allowed | forbidden
}
```

The deterministic Set Topology Plan is the only input. It provides the set
class plus a declared family or species id where required; no model, display
name, id prefix, faction, or current player state participates.

| Set class | Contract | Assignment result |
|---|---|---|
| `general` | no identity selector; `hybridEligibility = allowed` | ordinary role/frame/faction/level gate only |
| `family` | one declared `requiredFamilyId`; `hybridEligibility = allowed` | exact actor family is required, then ordinary gate rules apply |
| `unique-species` | one declared `requiredSpeciesId`; `requiresUniqueDemon = true`; `hybridEligibility = forbidden` | exact unique-demon species is required and a hybrid is refused before the member can be assigned |

`family` and `unique-species` are not fallback filters. Missing actor identity,
an unknown catalog selector, or a class/selectors mismatch blocks the set plan.
The future `EquipGate` maps failures to the typed I11 reasons
`FamilyMismatch`, `SpeciesMismatch`, `UniqueDemonRequired`, and
`HybridSetForbidden`.

All members of one set read the same identity requirement. A base type may not
be a member of two restrictive sets with different identity requirements;
`SetIdentityMembershipConflict` blocks the catalog before member seeds are
written. This prevents an arbitrary set-membership ordering from changing who
may equip one item.

Static identity decides whether a piece can be equipped. The existing
requirement profile and set envelope decide only whether the equipped piece's
effects are active. An unmet family/species rule is never represented as a
`set_trial_unmet` result.

### Frozen set envelope

At catalog generation, before member items are minted, resolve and persist:

```text
SetRequirementEnvelope {
  setId, resolverRevision, catalogRevision, tuningRevision,
  identityRequirement: SetIdentityRequirement,
  favoredAptitudeId?: string,
  fixedCeiling?: long,
  ratioCeilingMilli?: long,
  upkeepResourceId?: string,
  upkeepCostTotal?: long,
  upkeepReserve?: long,
  upkeepPeriodTicks?: long,
  memberProfiles: role -> RequirementProfile
}
```

The only resolver input is frozen catalog data:

```text
(setId, resolvedSetTopologyPlan, setRequirementPlanSeed, catalog/tuning revisions,
 set PowerVector, set P(Theta), sorted member roles)
  -> SetRequirementEnvelope | set_requirement_unsatisfiable
```

Every member may resolve to `none`; otherwise it may use only the envelope's
one favored aptitude and one upkeep resource. Fixed and ratio floors may vary
per role but cannot exceed the envelope ceiling. Sustained members share the
same period, and their costs sum within the envelope's tuning-owned total.

The envelope and member profiles are frozen with the catalog revision. Player
inventory, drop order, currently equipped pieces, and model availability are
not inputs. A model may have supplied the earlier closed build-favor label, but
this reconciliation is deterministic code only.

### Full-set activation

`SetEvaluator.Progress` continues to show every legally equipped set member,
including `trial` and `suspended` members. Therefore a player can equip the
last piece and see complete `N / total` progress.

`SetTrialEvaluator` then evaluates the envelope once against the wearer and
the same deployment key that owns every counted member's run status:

```text
set tier active := tier is threshold-wanted
                   AND every counted required member is active
                   AND envelope trial and upkeep conditions pass
```

Until it passes, the tier bindings remain durable derived state but are filtered
by module 24 with `set_trial_unmet`; they grant no capability or stat effect.
When it passes, every wanted tier becomes active together. A later lapse
suspends the tier effects together but never removes member assignments,
membership counts, or the visible completion path.

This prevents a dormant full set from granting a free capability while avoiding
the invalid experience of a set whose pieces cannot all be equipped.

Only an individually equipped combat participant can evaluate a set in a siege
engagement. A world-map troop legion or structure has no durable item assignment
in this module, so it neither evaluates an envelope nor pays maintenance.

### Validation

Extend the item Seedsmith validation with `SetRequirementCompletability`.
Before a set seed is accepted it must prove:

1. The static identity requirement matches the set class: general has none,
   family has exactly one known family, and unique-species has exactly one known
   unique-demon species plus `hybridEligibility = forbidden`.
2. Every declared member role has exactly one frozen profile and inherits the
   same set identity requirement.
3. No restrictive member is shared by a set with a different identity
   requirement.
4. All non-empty build clauses use the same legal aptitude id.
5. All upkeep clauses use the same legal resource and period, and their sum is
   within the envelope budget.
6. Existing set role/frame/slot rules can reach the top threshold. The existing
   corpus already rejects a tier above distinct member-role count
   (`SetCorpus.cs:142-150`); this validation adds no alternative counter.
7. A matching actor identity plus a pure witness allocation and resource snapshot derived from the envelope
   satisfies every member profile and the full-set activation evaluation within
   one deployment.

Failure emits `set_requirement_unsatisfiable`, marks the Seedsmith partition
blocked, and writes no set seed or member profiles. It never weakens a live
item, adds a player grant, rerolls a profile, or silently substitutes a target.

### Trial projection

`EquipmentTrialPlanner` gains a read-only set projection for module 20. It
returns set count, top threshold, envelope target, active/suspended members,
and the minimal unmet clause. It must never mutate allocations, resources,
profiles, assignments, or tier bindings.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SetRequirement"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SetEvaluator"
cd tools\seedsmith
python -m pytest tests/test_set_charm_gen.py tests/test_linkage.py -q
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Linkage/SetCompletability
```

## Project structure

```text
src/FusionRpg.Core/Items/Requirements/
  SetIdentityRequirement.cs           frozen static family/species restriction
  SetRequirementEnvelope.cs             frozen set contract
  SetRequirementReconciler.cs           pure catalog-time resolution
  SetTrialEvaluator.cs                  envelope evaluation over equipped roles
tools/seedsmith/seedsmith/adapters/items/
  set_requirement_completability.py     blocking content metric
data/tuning/equipment-requirements.v1.json
tests/FusionRpg.Core.Tests/Items/SetRequirementReconciliationTests.cs
tools/seedsmith/tests/test_set_requirement_completability.py
```

## Code style

```csharp
// Membership remains the existing role-based count. This adds an activation
// predicate; it never changes the set evaluator or turns a trial into unequip.
if (!allCountedMembersActive) return SetTrialResult.UnmetMember;
return envelopeTrial.Ready ? SetTrialResult.Ready : SetTrialResult.UnmetEnvelope;
```

Candidate roles and ids are ordinally sorted. Costs and fixed floors are
`long`; ratios are bounded per-mille; all balance values remain tuning data.

## Testing strategy

| Test | Asserts |
|---|---|
| frozen replay | same set input yields byte-identical envelope and members |
| family assignment | a matching family is admitted; another family gets `FamilyMismatch` |
| unique-demon assignment | only the exact unique demon species is admitted; a general demon gets `UniqueDemonRequired` and another species gets `SpeciesMismatch` |
| hybrid unique refusal | a hybrid receives `HybridSetForbidden` for both ten-role and fifteen-role unique templates |
| restrictive member overlap | conflicting family/species memberships block before any member seed writes |
| no conflicting targets | every non-empty member profile uses the envelope aptitude/resource |
| full-set witness | generated witness satisfies every member and activates every wanted tier |
| equip all pieces | unmet profiles do not block assignment or `N / total` set progress |
| dormant tier | a full but unmet set grants no set capability/effects |
| atomic full activation | satisfying the envelope activates all wanted tiers together |
| lapse symmetry | a failed member/upkeep suspends all set tier effects without removing pieces |
| deployment boundary | a new deployment reevaluates members and envelope with no inherited due state |
| duplicate role | existing role dedupe still prevents count inflation |
| invalid envelope | Seedsmith blocks and writes no partial output |
| order independence | member/input ordering cannot alter the envelope |

## Boundaries

**Always:** resolve static set identity before mint; reuse `SetEvaluator`
membership and threshold semantics; validate an identity and activation witness;
preserve partial-set progress after legal assignment; expose a read-only
activation route.

**Ask first:** set seed/schema fields; catalog migration; changing set tier
binding ownership; changing slot/frame/top-threshold rules; or allowing more
than one build/resource direction per set.

**Never:** turn aptitude/upkeep trials into an equip denial; turn a static
family/species restriction into a trial; reroll a member from player state;
replace the existing role counter; grant a dormant set tier; use a model at
runtime; or create another set-effect delivery path.

## Success criteria

- [ ] Every accepted generated set has one frozen, witnessed requirement
      envelope that can activate its complete intended loadout.
- [ ] Every family and unique-species set has one frozen, witnessed static
      identity contract; a hybrid can never assign a unique-demon set member.
- [ ] A player can equip all legal set pieces while trials remain unmet.
- [ ] Set progress remains role-deduped and visibly complete before activation.
- [ ] No full set grants effects until its one envelope is satisfied.
- [ ] Invalid or contradictory set requirements are blocked before content or
      concrete member profiles are written.
