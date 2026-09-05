# Spec: `allocation-surface`

Module 9 in the [species-build capability map](../species-build-map.md). **Depends on
`allocation-transport` (6).** Closes the program.

## Objective

Let the player see a species' auto-built distribution and override it — the *"unlock feature allow user
adjust distributions"* half of the original ask.

**The framing that decides the whole design (owner decision 7):** the plan is **static shipped
knowledge**, *"avoid user much learn every time play the game."* So this surface presents a stable fact
the player has already partly learned, and offers a deviation from it. It is a **Pokédex entry with an
edit button**, not a configuration screen.

**Success looks like:** a player understands what their species is built like without reading a manual,
and can change it in three interactions or fewer.

## Design

### It is a layer, not a page — GG-1 is binding

*"At any moment the player is on exactly one stage… every other surface is a layer drawn over that
stage, openable from anywhere, and closing it returns the player to exactly the stage state they left."*
GG-1 explicitly forbids *"routing to a sibling screen in order to look at something"*, and its stated
test case is the one that matters here: *"a player mid-wave who wants to check a demon's loyalty must
not lose the wave to do it."*

**GG-10 caps depth at three pushes** from the stage to any player action.

> ### ✅ Decided, owner, 2026-09-05: **`AptitudesLayer.tsx`**
>
> `web/.../layers/aptitudes/AptitudesLayer.tsx` is already named for exactly this shape and is
> **imported by nothing**, so there is no migration and no third copy to create. Its name already
> commits to what GG-1 requires — a *layer* drawn over the current stage, not a tab inside a panel.
>
> `ui/actor/ProgressionTab.tsx` stays as it is for now. It carries a duplicated copy of the same
> draft/save logic, and **consolidating the two remains a `commander-surface`/`actor-sheet` question** —
> this module does not entrench the duplication by building into it, and does not unilaterally delete
> it either.

### What it shows

Per species, three things and no more:

1. **The shipped baseline** — the plan's share vector at this species' current level. This is the
   learnable fact.
2. **The player's override**, if any, shown as a deviation *from* the baseline rather than as a separate
   build — because that is what it is.
3. **The remaining budget**, from `PointBudget.PointsFor(DemonType, speciesLevel, tuning)`.

### Rendering — the unit class already exists and its rule binds

`AptitudePoints` is the **eleventh `UnitClass`**, authorised 2026-08-26, and `"aptitudePoints"` is
already in the web contract's union (`design/spec-magnitude-and-units.md:83,101-107`). Its rule is a
real constraint, not a formatting note:

> an estimate, **allowed only on a surface with a real allocation**

So `Might 55 → +2,200 omni power` may be shown here — a real allocation backs it — and may **not** be
shown as a speculative preview of a build the player has not made. A "what if I moved 10 points here"
preview is therefore **out of scope** unless that rule is revisited with whoever owns it.

### Interaction

- **Adjust** — move points within the species' budget. Refused past budget, scope-locally (a large
  commander budget does not fund a species overspend).
- **Revert to shipped** — deletes the override. **Free**, and labelled as free.
- **Respec** — changing an existing override. **The price is shown before the confirm, never after.**
  First override is free and says so.

The price display is where this module meets `species-respec`: a player must be able to see that
switching repeatedly costs more, or the churn pricing is a hidden tax rather than a legible one.

### Copy

Written from the player's side, per the GUI principles: *"a person manages notifications, not webhook
config."* So — **"Sunflower's build"**, not "DemonType allocation scope"; **"Reset to default"**, not
"delete override row". Engine vocabulary (`typeId`, `scope_key`, `AllocationScope`) never reaches this
surface.

### States — what renders when there is no build, no data, or no answer yet

**Added 2026-09-05 by the playability audit.** This section did not exist, and its absence is why the
shipped panel invented one: it renders *"You're running the shipped build"* over twelve zeroes for a
species that has no build, keeps a failed request on a loading spinner forever, and treats a
not-yet-loaded price as free. A surface spec that names only the happy path gets the rest improvised.

Four states are real and each must be distinguishable from the others and from success:

| State | Reachable when | What renders |
|---|---|---|
| **Loading** | the species query is in flight | the loading placeholder — and **only** while genuinely pending |
| **Failed** | the species query errored | an error with the real reason and a retry. It is checked **before** any "no data yet" fallback, or it is unreachable — a failed query has no data, so an `!data` test that runs first swallows it |
| **No budget yet** | the species has never levelled: budget is `max(0, level-1) x rate`, so level 1 is exactly zero | a distinct state naming the **remedy** — field this species on the lawn — never the shipped-build copy, which asserts a build that does not exist. The disabled-save reason is visible text, not only a `title=` tooltip |
| **Price unknown** | the respec price query is pending or errored | the save path **waits or refuses**; it never falls back to treating the change as free. Spending souls without showing the price is the exact failure §"Testing strategy" criterion 5 guards, and a defaulted `isFree` walks straight around it |

The rule behind all four: **a degenerate state is rendered honestly, never as a plausible-looking
success.** An empty build and a broken build must not look the same to the player, and neither may
borrow the copy of a working one.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- SpeciesBuild
npm run test
npm run build
npx playwright test
```

## Project structure

```
web/fusion-rpg-web/src/layers/aptitudes/AptitudesLayer.tsx            ⛔ THE HOST (owner, 2026-09-05)
web/fusion-rpg-web/src/features/species-build/SpeciesBuildPanel.tsx   the panel it mounts
web/fusion-rpg-web/src/features/species-build/useSpeciesBuild.ts      hooks over the existing bus
web/fusion-rpg-web/src/lib/bus/queries.ts                             species query
web/fusion-rpg-web/src/lib/bus/mutations.ts                           override + respec
web/fusion-rpg-web/src/contract/types.ts                              species allocation DTO
```

**Hooks go through the existing bus** — TanStack Query for REST snapshots, the one SignalR hub for
live, features call `useX()` only. That is the shipped contract (`decisions.md`, *Web UI kit / bus*),
and `AptitudesUpdated` already broadcasts, so the panel refreshes without a second mechanism.

## Code style

- Follow the shipped `AptitudesPage`/`ProgressionTab` draft-and-save shape rather than inventing a third
  — and, where they duplicate each other, **extract rather than add a third copy**.
- Contract additions are additive; a narrowing or rename is a version bump and is not done here.
- Numbers render through the existing magnitude formatter with `aptitudePoints` — never hand-formatted.
- Disabled controls carry a **real reason** in `title`, following `Rail.tsx`'s own locked-state
  convention; never a bare disabled control.

## Testing strategy

1. **Baseline renders without an override** — the shipped plan is visible for a species the player has
   never touched. This is the primary read path and the one the "learn it once" framing depends on.
2. **Override renders as a deviation** from the baseline, not as a standalone build.
3. **Budget refusal:** allocating past the species budget is refused in the UI with the real reason, and
   the refusal is scope-local.
4. **Revert is free and says so**; first override is free and says so.
5. **Respec price is shown before the confirm** — a test asserts the price is present on the confirm
   path, because showing it afterwards is the failure this is guarding.
6. **GG-1 conformance:** opening the layer from a stage leaves the stage mounted, its state identical by
   reference, with no refetch — the exact assertion GG-1 names as its own test.
7. **GG-10:** reaching the override action takes ≤3 pushes from a stage.
8. **No engine vocabulary** in any rendered string — a lint-style test over the panel's copy.
9. **E2E:** a species' build is visible, adjustable, revertible, and the change survives a reload.
10. **The real plan resolves for the real roster** — asserted against the committed
    `_species-build-plan.json` and the compiled `DemonSpeciesCatalog`, never a hand-built fixture keyed
    to agree with the code under test. Criterion 1 is unfalsifiable without this: a fixture-fed baseline
    passes while every shipped species renders zero. *(Added 2026-09-05 — this is exactly what shipped.)*
11. **Each state in §"States" is covered**, the failure and pending-price paths included — the two that
    had no test and were therefore both wrong.
12. **No door is assumed.** A test or a manual step proves the surface is reachable **by clicking** from
    a cold start, not by navigating directly to a route a player has no link to.

## Boundaries

- **Always:** open as a layer over the current stage; show the price before the confirm; label free
  actions as free; render points through the `aptitudePoints` unit class.
- **Ask first:** any speculative "what-if" preview, which the `AptitudePoints` rule currently forbids;
  a new top-level route; **consolidating `ProgressionTab`'s duplicated draft/save logic** — the host is
  decided (`AptitudesLayer`), but merging the two copies is still a `commander-surface`/`actor-sheet` call.
- **Never:** route away from the stage to show this; put engine vocabulary on a player surface; render a
  points figure without a real allocation behind it; add a third copy of the draft/save logic.

## Success criteria

1. A player can see a species' shipped build, override it, revert free, and respec with the price shown
   first.
2. The layer opens over any stage and closes back to it, state-identical — proven by test.
3. Depth ≤3 pushes.
4. Web suite and build green; E2E covers the round trip.
5. No third copy of the allocation draft/save logic exists when this lands.
