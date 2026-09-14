# Spec: `sim-hub-parity`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** place matrix — Sim `ActorDerivedLookup` Named Partial  
**Wave:** 4 (after `battle-hub-fuse`)  
**Code anchors:** `ActorDerivedLookup` / `ActorDerivedProfiles.cs` · `SimEffectHost` · `AtomKindRegistry` Sim Partial for `stat.derived` OverlayAdd · `FoundationHarness`

---

## Objective

Sim combat Derived must not remain a **Named Partial** fork that ignores Replace/Flag (or other ops) while sheet/battle (post-fuse) honor Full Hub atom ops. Sim either **consumes ActorHub** for actor combat Derived or uses the **same op-aware contribution fold** as Hub — no weaker parallel API (SOLID L/D).

Success: AtomKind matrix for Sim combat `stat.derived` is **Full** (or documented structural exempt with owner comment); equip/replace atoms change Sim Derived like Hub; Partial lie removed from ideal place row.

---

## Tech stack

- Core: `ActorDerivedLookup`, SimEffectHost, AtomKindRegistry, optional thin ActorHub in sim hosts
- Prefer one code path with Hub over duplicating Compose

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorDerived|SimEffect|AtomKind|Sim"
```

---

## Project structure

| Path | Duty |
|---|---|
| `ActorDerivedLookup` | Honor ops (or delegate to Hub) for combat channels |
| AtomKindRegistry | Flip Sim Partial → Full for combat `stat.derived` when code matches |
| SimEffectHost / FoundationHarness | Wire Hub or Full lookup |
| Ideal place matrix | Reclassify Sim row Compliant / Wiring |

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Flat / Increased / Replace on Sim host matches Hub snapshot for same atoms |
| Unit | AtomKind Sim Full assertion for `stat.derived` combat |
| Regression | SimEffectHost still binds when E5/runtime flags require |

---

## Boundaries

- **Always:** Full combat ops or Hub; long; SOLID.
- **Ask first:** Leaving non-combat Sim kinds Partial for structural reasons (must comment exempt).
- **Never:** New BattleStatComposer-like sim composer; deepen ignore-op.

---

## Success criteria

- [ ] Sim combat equip ops match Hub semantics.
- [ ] Named Partial for combat `stat.derived` retired or narrowly exempted with comment.
- [ ] Ideal place matrix Sim row updated.
