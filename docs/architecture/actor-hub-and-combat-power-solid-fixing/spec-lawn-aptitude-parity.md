# Spec: `lawn-aptitude-parity`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-lawn · ownership lock Bound UniqueDemon  
**Wave:** 3 (after fuse; **does not fork** lawn wire)  
**Depends on:** [`aptitude-sheet/spec-unique-lawn-wire.md`](../aptitude-sheet/spec-unique-lawn-wire.md) (implementation owner) · `battle-hub-fuse`  
**Code anchors:** `CheatState.SpeciesAllocation` · `MatchUniqueBindingsFacet.TryGetByPtr` · `UniqueActorHubCompose` commander+UniqueDemon · unique GET S4

---

## Objective

This module is a **Done / parity gate**, not a second lawn-wire design. Bound UniqueActor Hot aptitude input must match sheet: **`commander + UniqueDemon(instanceId)`**. Empire generals stay **`commander + DemonType(species)`**.

Implementation work lives in `unique-lawn-wire`. This spec defines **cross-program acceptance** once Hub is sole compose (post-fuse): lawn Derived/aptitude for a Bound specimen equals Server Hub for the same instance within tolerance of identical allocation inputs.

Success: aptitude-sheet `unique-lawn-wire` success criteria **and** the parity checks below are green; combat-power program checklist ticks HF-lawn.

---

## Tech stack

- Injector: per `unique-lawn-wire` (RpgClient unique GET cache, CheatState resolve)
- Prove: Core unit and/or live probe comparing Hub inputs

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~SpeciesAllocation|UniqueDemon|Bound"
# Live (owner): deploy-play → Bound unique → allocate → lawn Hub/probe matches sheet
```

---

## Project structure

| Path | Duty |
|---|---|
| aptitude-sheet `unique-lawn-wire` | Implement fetch + resolve |
| This program checklist / prove script | Parity assert Bound lawn vs `UniqueActorHubCompose` aptitude input |
| Map cross-link | Both maps point at each other for HF-lawn Done |

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Bound → unique allocation; general → species; same typeId unique ≠ empire |
| Integration/prove | Same instanceId: lawn aptitude edges ≡ Server Hub aptitude edges |
| Regression | S4: unique GET only — no commander `uniques` map |

---

## Boundaries

- **Always:** Defer implementation ownership to `unique-lawn-wire`; UniqueDemon for Bound.
- **Ask first:** Moving lawn-wire files into this program folder.
- **Never:** Second HTTP channel; typeId→UniqueDemon inference; claim Hub-on-lawn alone as Done without UniqueDemon input.

---

## Success criteria

- [ ] `unique-lawn-wire` success criteria all checked.
- [ ] Parity prove: Bound lawn aptitude input matches Server UniqueDemon compose.
- [ ] HF-lawn ticked on both program maps / ideal register.
