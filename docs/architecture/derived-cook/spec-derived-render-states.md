# Module: `derived-render-states`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Design:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §3  
**Locks:** **D2** wire authoritative · **D4** Show-unchanged = default only  
**Depends on:** `derived-sheet-projection`, `derived-cook-ia`  
**Code today (defective):** `derivedCook.ts` `resolveDerivedRenderState`

---

## Objective

Normative six-state machine for cook expansion × sheet channels. No third classification.
`unregistered` reachable; `default` ≠ `no-producer`. Kill dead `NO_PRODUCER_HINT` same-as-else.

## State machine (normative)

| State | When |
|---|---|
| `active` | Present; meaningful non-default value and/or live contributions (design: non-zero with contribs — wire may refine) |
| `default` | Registered; at default; nothing interesting writing |
| `capped` | At registry/wire cap |
| `stub` | Placeholder curve (e.g. progression.power/realm) |
| `no-producer` | Registered; nothing in game can write yet |
| `unregistered` | Id in expand path but registry rejects / ValidateChannel throws |

### Absent from snapshot

| Case | State |
|---|---|
| Open-prefix / catalog-known gearable, no live row | Prefer wire `renderState`; else `default` or `no-producer` from producer metadata — **never** collapse all absences to one state |
| Registry rejects | `unregistered` |

### Show-unchanged (**D4**)

Hide rows where `renderState === "default"` only. **Do not** hide `no-producer`.

## Wire authority (**D2**)

1. Prefer `channel.renderState` from sheet.
2. FE `resolveDerivedRenderState` may exist **only** for golden parity tests against wire.
3. Product path must not diverge from wire when field present.

## Golden fixture ids (required)

| Fixture id | Expected state |
|---|---|
| `golden:status.resist.dot@cap` | `capped` |
| `golden:status.resist.omni@default` | `default` |
| `golden:progression.power` | `stub` |
| `golden:progression.bonus.arm1` | `no-producer` |
| `golden:turn.moveSpeed` | `unregistered` |
| `golden:combat.power.fire@live` | `active` |

## Success criteria

- [ ] Unit goldens for all six states.
- [ ] `unregistered` returned for `turn.moveSpeed` (or equivalent).
- [ ] Dead `NO_PRODUCER_HINT` branch removed.
- [ ] Show-unchanged hides default only.
- [ ] Product fold uses wire `renderState` when present.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run derivedCook foldDerivedSurfaceVm
```

## Boundaries

- **Never:** invent a seventh state; blank for default; hide no-producer as unchanged.
