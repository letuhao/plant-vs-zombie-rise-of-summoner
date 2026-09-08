# Spec: `actor-menu-scope-picker`

**Module id:** `actor-menu-scope-picker` · **Program:** [fe-essentials-map.md](../fe-essentials-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** nothing · **Blocks:** `hide-legacy-entry` (only once this exists to replace whatever it hides)

---

## Objective

One reusable component that lets a caller pick **who** a standing effect reaches — the FE-side
counterpart to the buff-debuff-scope program's `WhoSelector` (`Target` / `Type` / `UniqueDemon` /
`Relation`, all four modes per owner decision). Built now, ahead of the commander/aura-skill feature
that will eventually consume it — matches this program's own precedent of building the scope primitive
before the feature that uses it.

**FE-only.** Emits a `WhoSelector`-shaped value to its caller; does not call any API, does not know
about `EffectBag`/`BattlefieldOwnSideReactor`/grants. Wiring this to something real is explicitly the
deferred commander/aura-skill work, not this module's job.

**Users:** whatever screen eventually authors a commander's aura (not yet built — this ships as a
standalone, demoable component ahead of its consumer, the same way `ui/actor/ActorLadderDemoPage.tsx`
already demos the Actor ladder ahead of every screen that uses it).

**Success is measurable:** one component, four mode tabs, each producing a value matching the backend's
own `WhoSelector` shape exactly (`Kind` plus the one payload field that kind needs); switching modes
never leaves a stale payload from the previous mode; every mode has its four states (loading/empty/
error/ready — GG-17, the same rule `ActorRungState` already enforces for every Actor rung).

## Design

### Shape — one container, four mode panels, matching `WhoSelector` field-for-field

```ts
// Mirrors src/FusionRpg.Core/Scope/WhoSelector.cs's own shape — Kind + exactly the payload that kind needs.
type ScopePickerValue =
  | { kind: "target"; targetPtr: string }
  | { kind: "type"; typeIds: number[] }
  | { kind: "uniqueDemon"; instanceId: string }
  | { kind: "relation"; relation: "ally" | "enemy" };

function ActorMenuScopePicker(props: {
  value: ScopePickerValue | null;
  onChange: (value: ScopePickerValue) => void;
  // Each mode's own data source — the picker renders states from these, never fetches itself
  // (matches the Actor ladder's own "components bind to a shape, never fetch" rule).
  targetCandidates: ActorRungState[];   // for "target" mode's list
  uniqueDemonCandidates: ActorRungState[]; // for "uniqueDemon" mode's list (same shape, different source)
  typeOptions: { typeId: number; label: string }[]; // for "type" mode's multi-select
}): JSX.Element;
```

### Per-mode UI, reusing what already exists rather than inventing parallel pickers

| Mode | UI | Reuses |
|---|---|---|
| `target` | A searchable list of `ActorRow`s, single-select | `ui/actor/ActorRow` directly — its own doc comment already names "deploy pickers" as an intended use |
| `uniqueDemon` | Same list pattern as `target`, different candidate source (durable specimens, not live board ptrs) | `ActorRow` again — same component, different data feed, matching this program's own "bind to a shape" rule so the picker doesn't need two list implementations |
| `type` | A simple multi-select over species/type names — **no existing component covers this** (confirmed: this session's FE audit found zero `TypeToken`/`TypeChip` anywhere) | New, small: a checkbox list using existing `Checkbox`/`Row` primitives — not a new entity ladder, just a plain multi-select |
| `relation` | Two radio options, Ally/Enemy — trivial, no actor data needed at all | Existing form primitives only |

### Mode switching, and why it can't just be a `<select>`

A `TabList` (already exists: `ui/TabList.tsx`) switches the active mode. On switch, `value` clears
to `null` rather than carrying a stale payload shaped for the old mode — a caller reading
`value.kind === "type"` must never see a leftover `targetPtr` from before the switch.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- ActorMenuScopePicker
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/scope/
  ActorMenuScopePicker.tsx    the mode-switching container
  TypeMultiSelect.tsx         the one genuinely new primitive (type mode)
  ActorMenuScopePicker.test.tsx
  ActorMenuScopePickerDemoPage.tsx   matches ActorLadderDemoPage.tsx's own precedent
```

A new `ui/scope/` sibling to `ui/actor/` — this is not an Actor-ladder rung (it composes actor rows,
it isn't one), so it doesn't belong inside `ui/actor/` itself.

## Code style

Match `ui/actor/`'s own conventions exactly: a `kind`-discriminated state/value type, a `shared.tsx`-
style file for anything two of the four mode panels both need, `data-testid` on every interactive
element (matching `ActorRow`/`ActorCard`'s own testid pattern), Tailwind + `cn()` for styling, no new
CSS files.

## Testing strategy

- **Round-trip per mode**: selecting in each of the four modes produces exactly the `ScopePickerValue`
  shape that mode's own type declares — no cross-mode field leakage.
- **Mode switch clears stale value**: switching from `type` (with 2 type ids selected) to `relation`
  and back to `type` does not silently resurrect the old selection as the new one's default.
- **Four states per data-driven mode**: `target`/`uniqueDemon`/`type` each render loading/empty/error/
  ready correctly (GG-17) — reuses `RungStateFallback` for the two actor-row modes; `type` needs its
  own small equivalent for an empty/error type-option list.
- **Reuses `ActorRow`, not a parallel implementation**: a test asserting `target` and `uniqueDemon`
  modes both render via the real `ActorRow` component (not a lookalike), so the two list UIs cannot
  visually drift apart the way `docs/design/README.md`'s own "one entity, one ladder, no forks" rule
  is meant to prevent.

## Boundaries

- **Always:** emit a `WhoSelector`-shaped value; bind to caller-supplied candidate lists, never fetch;
  reuse `ActorRow` for both actor-listing modes.
- **Ask first:** any wiring to a real API/backend — this module's whole point is shipping ahead of the
  commander feature, not anticipating its exact integration shape.
- **Never:** a second list-row component for actors when `ActorRow` already exists; a `<select>` for
  mode switching when the existing `TabList` primitive already does this job.

## Success criteria

1. All four modes implemented, each producing the exact `WhoSelector`-matching shape.
2. `target`/`uniqueDemon` both reuse the real `ActorRow`, proven by test, not just by convention.
3. Mode switching never carries a stale cross-mode value.
4. A demo page exists, matching `ActorLadderDemoPage.tsx`'s own precedent for shipping ahead of a consumer.
