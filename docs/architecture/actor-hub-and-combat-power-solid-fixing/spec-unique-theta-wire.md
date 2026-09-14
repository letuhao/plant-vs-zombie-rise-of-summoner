# Spec: `unique-theta-wire`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** finding 7 · HF-chip optional `Θ {n}` · `chip-honesty`  
**Wave:** 4 (after `chip-honesty`)  
**Code anchors:** Unique aptitude GET / `AptitudeEndpoints` · `UniqueActorDto` / sheet payload · `IPowerIndexProvider` · FE `foldAptitudesSurfaceVm` scopeFiction

---

## Objective

Chip may show **`Θ {n}`** only when a **real** power index is on the wire. Today unique GET often has **no `theta`**, so FE must not invent Θ from `specimenLevel`. Expose authentic Θ (from the same provider sheet/Hub already use) on unique aptitude (and/or unique actor) GET when the actor has a defined index.

Success: unique mode chip can render `Θ {n}` from payload; absent Θ → omit Θ line (Lv only); never `theta ?? specimenLevel`.

---

## Tech stack

- Server: aptitude unique GET and/or unique actor GET DTO field `theta` (`long` / int index — match existing commander theta type)
- FE: `chip-honesty` already expects optional Θ — consume new field
- Core: `IPowerIndexProvider` / actor Θ source already used elsewhere

---

## Commands

```powershell
dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~Aptitude|UniqueActor|Theta"
cd web/fusion-rpg-web
npm test -- --run foldAptitudesSurfaceVm
```

---

## Project structure

| Path | Duty |
|---|---|
| Unique aptitude/actor GET | Add `theta` when resolvable |
| Contract types FE | Optional `theta` on unique payload |
| `scopeFiction` | Prefer wire Θ; never level fallback |
| Docs | Finding 7 closed for wire half |

---

## Testing strategy

| Level | Cases |
|---|---|
| Server | Unique GET includes theta matching provider for fixture actor |
| FE | With theta → subtitle has `Θ`; without → Lv only, no `power` |
| Negative | specimenLevel never copied into theta field |

---

## Boundaries

- **Always:** Real Θ or omit; Lv separate; chip-honesty vocabulary.
- **Ask first:** Also putting Θ on Standing DTO.
- **Never:** Fabricate Θ from level; label Θ as “power.”

---

## Success criteria

- [ ] Unique wire carries real theta when known.
- [ ] Chip shows `Θ` only from wire.
- [ ] No `theta ?? specimenLevel` anywhere on aptitude scope path.
