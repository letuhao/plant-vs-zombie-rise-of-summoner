# Spec: actions-tab-fe-wiring (A30)

Module **A30** in the [action map](../action-map.md) §17. Reads
[action-playability-ideal.md](../action-playability-ideal.md) gap #4. Depends on **A27**
(specimen loadout endpoints) and **A28** (discard endpoint).

> **Scope note (spec-driven-development's own carve-out):** this module wires *data*, not visual
> design. If it turns out to need a genuinely new component shape (not a copy of `AuraSlot`'s pattern),
> stop and run `/idea-ui` first — this spec assumes reuse, not redesign.

## Objective

`ActionsTab.tsx:7-14,118` explicitly stubs regular actions (`PLACEHOLDER_ACTIONS`, locked grid, own doc
comment: *"no catalog exists yet... Unlocks once the action system ships (approved, not yet built)"*).
Once A27/A28 exist, this is a real gap to close, following the **exact pattern already live in the same
file** for auras (`useAuraCatalog`/`useAuraRuntime`/`useEnableAura`/`useDisableAura`).

## Design

1. **No new catalog endpoint needed.** Unlike auras (which show locked-but-existing items), the
   meaningful "catalog" for a specimen's actions is exactly what A27's `GET` already returns — the held
   set is the catalog. Reuse it; do not build a second listing endpoint.
2. New `web/fusion-rpg-web/src/lib/bus/action.ts`, mirroring `lib/bus/aura.ts`'s shape one-for-one:
   - `useActionLoadout(instanceId)` → `GET /api/actors/{instanceId}/loadout` (held + equipped)
   - `useSetLoadout(instanceId)` → `POST /api/actors/{instanceId}/loadout`
   - `useDiscardUnlock(instanceId)` → `POST /api/actors/{instanceId}/unlock/discard`
3. `ActionsTab.tsx`: delete `PLACEHOLDER_ACTIONS` and its rendering block entirely. Add a real action
   grid below (or beside, matching whatever layout the aura grid already established) the aura grid.

   **⚠️ Audit finding: `AuraSlot`'s 3-state model does not fully fit actions, do not reuse it as-is.**
   Auras are a toggle (on/off); actions additionally have a **cooldown** state (equipped, held, but
   temporarily unusable) that has no aura equivalent. A straight reuse of
   `"active" | "equipped-inactive" | "locked"` has no slot for "equipped, not on cooldown" and would
   need to conflate it with one of the three — a genuine, non-cosmetic state-model gap. Recommend a 4th
   state (e.g. `"equipped-cooldown"`) rather than forcing a fit; if the visual treatment for that state
   is non-trivial, that specific piece is `/idea-ui` territory, not a mechanical copy of `AuraSlot`.

4. Reuse the existing refusal-note pattern (`REFUSAL_TEXT`, `setNote`) for equip errors — same UX as
   auras, not a new interaction pattern.
5. **Discard needs a confirm step before firing.** Unlike enabling/disabling an aura (free, reversible),
   discarding a held unlock is a **priced, irreversible-within-the-run** action — the chance ratchet
   never rewinds (action-ideal.md §3.1/§3.3). A one-click discard button, styled identically to a
   reversible toggle, risks an accidental costly action. This repo's own general standard (confirm
   before an irreversible action) applies here — add a confirm interaction (a second click, a modal, or
   equivalent), not a bare button matching the aura toggle's zero-friction pattern.

## Tunables

None — this module renders and mutates already-real state.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- ActionsTab
npm run build
```

## Project Structure

```
web/fusion-rpg-web/src/lib/bus/action.ts          (new, mirrors lib/bus/aura.ts)
web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx     (PLACEHOLDER_ACTIONS removed, real grid added)
web/fusion-rpg-web/src/ui/actor/ActionsTab.test.tsx (new/extended)
```

## Code Style

Match `ActionsTab.tsx`'s existing conventions exactly: plain structural types at the `ui/` boundary (not
imported `@/lib/bus` DTOs, per this repo's own contract guard), the same loading/empty-state shape, the
same `data-testid` naming convention (`actions-tab-*`).

## Testing Strategy

| Case | Expect |
|---|---|
| A specimen with 2 held, 1 equipped action | grid shows 2 real slots, correct equip state per slot |
| Equip a held-not-equipped action | mutation fires, grid updates, matches A27's round-trip |
| Equip rejected (6th slot, or a category error) | refusal note renders, matching the aura pattern's `onError` shape |
| Discard a held unlock | requires confirm interaction before the mutation fires through A28; slot disappears from the grid on success |
| A held, equipped action currently on cooldown | renders as its own distinct state, not conflated with "locked" or "active" |
| `PLACEHOLDER_ACTIONS` | gone — grep confirms zero references remain |
| No specimen selected / loading | existing loading-state test pattern, unchanged |

## Boundaries

**Always:** mirror `lib/bus/aura.ts` and `ActionsTab.tsx`'s existing aura block — same hook shape, same
component composition, same refusal-note UX.

**Ask first:** if the held/equipped visual shape genuinely can't reuse `AuraSlot`'s states (e.g. actions
need a 5th, action-specific state auras don't have) — that's a design question, route it through
`/idea-ui`, not through this spec.

**Never:** build a bespoke design system for this one tab, or fetch inside a leaf UI component
(`ui/` binds to values passed in, per this repo's own contract guard — same rule the aura block already
follows).

## Success Criteria

1. `PLACEHOLDER_ACTIONS` and its rendering block are gone.
2. A specimen's real held/equipped actions render, equip, unequip, and discard through real endpoints.
3. Same UX pattern (loading, refusal notes, eviction notes) as the aura block already established.
