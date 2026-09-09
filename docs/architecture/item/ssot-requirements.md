# Lane I11 SSOT — the equip requirement gate

**Status:** Current-state SSOT, reconciled 2026-09-09. Enriches
[item-ideal.md](../item-ideal.md) and is bound by
[enrichment-contract.md](enrichment-contract.md).

> **Supersedes the five-attribute proposal in this file's prior revision.** The
> shipped primary-stat system is the twelve-aptitude catalog owned by the class
> system. An item requirement based on aptitude distribution is not designed or
> implemented by I11; it needs its own approved extension after the current
> gate is stable. This document does not create a parallel attribute layer.

---

## 1. Scope

### This lane owns

- The durable assignment gate: whether a specimen may assign an item to a role.
- The currently supported requirement axes: role unlock, frame, level, and an
  optional faction restriction.
- The approved, unimplemented static set-identity extension for family and
  unique-species sets.
- The distinction between a hard assignment refusal and a lapsed requirement
  discovered while projecting a standing assignment.
- The player-facing refusal content supplied by `EquipRefusal`.

### This lane does not own

| Thing | Owner |
|---|---|
| Equip roles and their unlock predicate | Item module 3 (`slot-roles`) |
| Actor frame, species, family, and unique-demon classification | Demon / Seedsmith frame-classify work |
| The ten-rung rarity ladder | Item module 7 (`rarity-bands`) |
| Rolled affixes and their tier bands | Item module 8 (`affix-legality`) |
| The twelve primary stats (aptitudes) | Class system |
| Actor resources and exhaustion | Resource Hub |
| Item upkeep or resource-driven suspension | Future item-requirements extension |

The resource set is `hp`, `stamina`, `hunger`, `spirit`, `qi`, and `poise`.
It is a shared actor model, not a requirement vocabulary.

---

## 2. Current gate

An assignment is allowed only when all applicable checks pass:

```text
role unlock  ->  frame compatibility  ->  faction compatibility  ->  specimen level
```

`EquipGate.Explain` performs those checks and returns a typed refusal rather
than a generic failure. The current refusal vocabulary is:

| Reason | Meaning |
|---|---|
| `RoleLocked` | The specimen has not unlocked the role. |
| `RoleNotOnFrame` | The item does not fit the specimen's body frame. |
| `LevelTooLow` | The specimen level is below `level_req`. |
| `FactionMismatch` | An optional faction restriction is unmet. |

`level_req` remains the field on `effect_container`; it is not duplicated into
a requirements table. It is checked against the **specimen** level by the item
gate, so a specimen can wear the content it has progressed to.

Frame is a body-fit rule. Faction is allegiance and is reserved for
hand-authored uniques or set pieces; rolled base types do not multiply their
frame restriction with a faction restriction. Element affinity is advisory,
not an equip gate: the Element Hub already supplies its mechanical trade-off.

### 2.1 Approved static set-identity extension

Family and unique-species set restrictions are static eligibility facts. They
are neither optional requirement trials nor faction aliases. The set header
resolves one identity contract at catalog time and every member assignment reads
that same contract:

```text
SetIdentityRequirement {
  setClass: general | family | unique-species
  requiredFamilyId?: declared family id
  requiredSpeciesId?: declared demon species id
  requiresUniqueDemon: bool
  hybridEligibility: allowed | forbidden
}
```

| Set class | Static requirement |
|---|---|
| `general` | No family or species requirement; hybrid eligibility follows the ordinary frame/role rules. |
| `family` | The wearer must have the exact declared `requiredFamilyId`. It may be a hybrid only when its ordinary frame and role checks pass. |
| `unique-species` | The wearer must be a unique demon of the exact declared `requiredSpeciesId`, and its frame must not be `hybrid`. Both ten-role and fifteen-role unique templates use this same restriction. |

The actor facts come from the authoritative unique-actor/species projection:
`speciesId`, `familyId`, whether the actor is a unique demon, and `frame`. No
rule may infer a family from a display name, species-id prefix, faction, side,
or an LLM classification. A missing required identity fact fails closed.

These are hard assignment and projection checks. They belong after ordinary
role/frame/faction checks and before the level check:

```text
role unlock -> frame -> faction -> set identity -> specimen level
```

The implementation extends the typed gate vocabulary with `FamilyMismatch`,
`SpeciesMismatch`, `UniqueDemonRequired`, and `HybridSetForbidden`. It also
extends the gate's specimen input with the four authoritative identity facts
above. These are an approved future contract, not claims about the current
`EquipGate` implementation.

Only `family` and `unique-species` set headers may carry this contract. A
restricted member must not be shared by two restrictive set headers with
different identity contracts; the catalog validator rejects that ambiguous
membership instead of making a player guess which set caused a refusal.

---

## 3. Assignment and projection are different moments

Assignment is hard: an unmet role, frame, faction, static set-identity, or
level requirement refuses the new assignment.

Projection at deploy is intentionally weaker. It rechecks the stable role, frame,
faction, and static set-identity facts but does not drop an existing assignment when its level
requirement has lapsed. `EquipProjector` reports that row as a shortfall and
keeps the binding in its projection. This prevents a level change from silently
deleting or cascading through equipment.

The projector currently reports a shortfall; it does **not** apply an
`overburdened` status. A status-based penalty remains a possible future design,
not shipped behavior.

---

## 4. Aptitudes are the only primary-stat vocabulary

Player-facing text may call the twelve values **primary stats**; code, config,
and future requirement data use **aptitudes**. They are:

```text
Might, Fortitude, Vigor, Onslaught,
Agility, Composure, Pierce, Focus,
Bulwark, Retribution, Precision, Ferocity
```

An aptitude is a source whose share is computed from the actor's allocation. It
is not a derived channel and equipment must not grant aptitude points. That
protects the allocation denominator and prevents item effects from changing a
build's aptitude distribution.

Consequently, a future aptitude requirement must:

1. Read the existing `AptitudeAllocation` / class-system resolver; it must not
   add `bulk`, `sinew`, `reflex`, `aim`, or `sap`.
2. Specify whether it reads individual aptitude points, posture share, or both.
3. Exclude equippable-item sources by construction, so gear cannot enable itself.
4. Be designed with the power ladder and Seedsmith contracts before schema or
   generator work begins.

No `container_requirement` table, `ActorProfile` requirement payload, or
attribute-growth schema exists today. They are not implied by this document.

---

## 5. Resources and future item upkeep

Resources are mutable pools; they are not hard equip requirements. A current
resource value must never force an unequip, because combat spending would turn a
temporary shortage into an inventory cascade.

If an item later needs a continuing price, it should be a separate maintenance
contract:

```text
assigned and bound -> active while reserve/upkeep payment succeeds
                    -> suspended while payment fails
                    -> automatically reactivates when payment can succeed
```

Suspension keeps the item assigned and makes its effects unavailable. It is
distinct from resource exhaustion, which is a status-driven debuff owned by the
Resource Hub. The maintenance profile must name a resource's established meaning
and cover all six resources through the shared registry; it must not hand-list a
subset.

This capability is not implemented. Its future specification must define its
payment clock, whether a missed interval permits a partial payment, the atom
suspension boundary, persistence, and UI state.

---

## 6. Boundaries

**Always:** report an actionable typed refusal; preserve the durable assignment
when a level requirement lapses; keep frame, faction, family, and species
separate; use the existing twelve-aptitude catalog for any future primary-stat
work.

**Never:** create a second five-stat system; let a resource shortage force an
unequip; use rarity as a direct requirement multiplier; infer family/species
from presentation or faction; make an item satisfy its own future aptitude
requirement; add a private curve from level to requirement.

**Ask first:** adding an aptitude requirement axis, an item-upkeep schema or
runtime, a static eligibility class beyond the three set classes, a new refusal
reason beyond the approved identity reasons, or a new container kind / column.

---

## 7. Evidence

- `src/FusionRpg.Core/Items/EquipGate.cs` — assignment checks and refusal
  vocabulary.
- `src/FusionRpg.Core/Items/EquipProjector.cs` — projection retains lapsed
  assignments and reports shortfalls.
- `docs/architecture/demon-system-map.md` — unique demons are individual
  `UniqueActor` specimens; general demons are species-only and cannot satisfy
  a unique-demon item restriction.
- `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs` — the twelve-aptitude catalog.
- `docs/architecture/class-system/spec-primary-stats.md` §2–§3 — primary-stat
  terminology and aptitude-source rule.
- `docs/architecture/resource-hub-ssot.md` §1–§2 and §10 — the six resources
  and exhaustion boundary.
- `docs/architecture/power/ssot-power-scale.md` §4 and §9 — the one power
  ladder and tuning ownership.

## 8. Design-gate checklist

```text
[x] I identified the item gate, aptitudes, resources, and power ladder.
[x] I read the required SSOTs and checked the architecture decisions.
[x] I verified the current gate and aptitude catalog against code.
[x] The approved set-identity contract is recorded separately from the current
    gate; implementation, schema, and typed gate changes remain follow-up work.
[x] The prior five-attribute proposal is explicitly superseded rather than
    treated as current design.
```
