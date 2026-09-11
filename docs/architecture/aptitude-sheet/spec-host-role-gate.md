# Spec: `host-role-gate`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D2** · **A5** · **A10** · Mode C keep · **E1** · **S3** · **S8**  
**Code anchors:** `ActorPanel.tsx` (~74–76, ~266–270) · `AptitudesTab.tsx` · GG-63 footer draft  
**Depends on:** `unique-allocate` · `aptitudes-surface-vm` · `aptitude-preset-console` · `aptitude-preset-api`

---

## Objective

Wire ActorSheet Aptitudes so **creature → Mode A (UniqueDemon)** and **commander → Mode C**, using
thin `RecipeMount` over `aptitudes-surface-vm`. Stop using commander allocate as UniqueActor default.
Honor **E1:** Confirm commits draft; Activate calls transactional preset activate API without Confirm.

---

## Tech stack

- React ActorPanel / AptitudesTab
- Hooks from `unique-allocate` + existing commander hooks + **`POST .../activate`**
- Shell Confirm / **Cancel** (GG-63) may **mirror** decision-strip — not the only Confirm home (D7/S8)

---

## Commands

```powershell
npm test -- --run AptitudesTab
npm test -- --run ActorPanel
```

---

## Project structure

| Path | Duty |
|---|---|
| `AptitudesTab.tsx` | Thin host: pick mode from role; RecipeMount; report draft upward; open preset layer |
| `ActorPanel.tsx` | Pass role + instanceId; footer may mirror Confirm/**Cancel** (rename Reset → Cancel fiction) |
| Collapse `AptitudesPage` commander body | Same recipe Mode C (A4) |

---

## Behavior

| Condition | Mode | Read | Draft Confirm write | Activate write |
|---|---|---|---|---|
| `role === "commander"` | C | `GET /api/aptitudes/{playerId}` | `POST /allocate` | **`POST /api/aptitude-presets/activate`** (S3) |
| Creature with `instanceId` | A | `GET .../unique/{instanceId}` | `POST .../unique/allocate` | **`POST .../activate`** (S3) |
| No specimen / not ready | — | Refuse confirm; fiction empty/pending | — |

**A5b:** Mode A may show read-only commander contribution (chip subtitle or inspect footnote — pick
one in HTML draft; default **chip subtitle**).

Draft key: Mode A by `instanceId`; Mode C by `playerId`. Dirty footer only for active mode.

**Cancel (S8):** Player-facing **Cancel** everywhere (shell mirror included); bus/internal remains
`aptitude.reset` / revert.

**Favour (E3/S1):** New preset / Auto-assign may seed from favour GET permille when `speciesId`
known — never auto-fills UniqueDemon on sheet open.

---

## Code style

Host owns: query selection, mutate on **confirm** or **preset.activate → activate API**, draft via
`useAllocationDraft`. No layout chrome in host beyond RecipeMount + phase retry + nested preset mount.

---

## Testing strategy

- Role gate unit/UI tests: commander chip ≠ UniqueDemon; creature confirm hits unique mutation mock.
- Refuse confirm without instanceId in Mode A.
- Activate path calls activate API **without** requiring prior Confirm; does not split set-active + allocate.
- Apply-to-draft leaves dirty draft; Confirm then posts.
- Shell mirror button labeled Cancel.

---

## Boundaries

- **Always:** D2 role gate; leftover+decision in console; E1 verb split; transactional Activate (S3).
- **Never:** DemonType write from ActorSheet; UniqueDemon on commander sheet; Hub favour baseline;
  client split Activate.

---

## Success criteria

- [ ] UniqueActor Aptitudes no longer posts commander allocate.
- [ ] Commander sheet still allocates commander.
- [ ] Scope chip fiction matches mode.
- [ ] Activate uses activate API; Confirm still works for draft; Cancel fiction (S8).
