# Spec: `lawn-occupant-adapt`

**Module id:** `lawn-occupant-adapt` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §3.2 · §4.5 ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** — · **Blocks:** `cell-occupancy-dock` (required before dock rows).

---

## Assumptions

1. Today's `ActorView` requires `instanceId`. Living lawn occupants include **generals** with none.
2. Adapter output is a **collection / sheet bind payload**, not a fourth durable identity.
3. Unique Bound → open sheet by `instanceId` (`?sel=`). Generals → **session-scoped observe handle**
   that dies with the occupant; never enter `?sel=` / address bar.
4. `ptr` is never player-facing (GG-23). Generation ≠ ptr lifetime (IL2CPP reuse).
5. Labels: **Fielded** (Bound unique) / **Wave** (general plant or zombie). Do not write **Wild**.

→ Correct these now or this spec proceeds as written.

---

## Objective

Map living lawn occupants (unique Bound + general Wave) into props safe for `ActorCollection` and
ActorSheet bind, without inventing durable ids or feeding Unity pointers to the player band.

**Success:** Dock can list a mixed stack; unique drill-in uses `instanceId`; general drill-in uses
in-memory observe only; URL never gains a general `sel`.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | Pure adapt functions over `LawnViewModel` / observe snapshots |
| Sheet | Existing ActorSheet bind roles: `lawn-bound` unique vs general Wave chrome (GG-17) |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/lawn/adaptOccupant
# or wherever the adapt module lands under ui/lawn or ui/actor
```

---

## Project Structure

```text
web/.../ui/lawn/adaptOccupant.ts          # pure map
web/.../ui/lawn/adaptOccupant.test.ts
```

---

## Design

### Output row (minimum)

Display name, species, side, rarity-or-none, Fielded/Wave chip, observe HP fraction, optional
`instanceId`, session observe handle for generals (opaque to URL).

### Sheet bind

| Occupant | Sheet bind | URL |
|---|---|---|
| Unique Bound | `instanceId` + this-cell live observe | `?sel=<instanceId>` |
| General living | Session observe handle; locks per design §3.2 | **no** `sel` |

### Honesty

Observe may lag (RT-14) — show *"binding catching up"*, not a blank unique sheet. Do not predict
procs (GG-15).

---

## Tunables

None. Identity rules are structural.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Unique with bind → `instanceId` present; general → absent + Wave chip |
| Unit | Adapter never copies `ptr` into player-visible fields |
| Unit | Handle invalidation when occupant leaves the cell / dies |

---

## Boundaries

- **Always:** Adapt before `ActorRow`; Fielded/Wave; generals out of `?sel=`.
- **Ask first:** Promoting a general into a durable id of any kind.
- **Never:** Pass `instanceId` as `targetPtr`; invent a fourth durable id; label Wave as Wild.

---

## Success Criteria

- [ ] Mixed cell fixture produces valid collection rows
- [ ] URL helper refuses to encode general selection
- [ ] Sheet lock reasons for Wave match design landing §3.2

---

## Open Questions

None blocking. Exact TypeScript type name for the observe handle is an implement detail after approval.
