# Capability map — Game GUI refactor

**Status: awaiting approval. No build authorized.**
**Parent:** [decisions.md](decisions.md) row **Game GUI** (2026-08-22).
**Rules:** [game-gui-principles.md](game-gui-principles.md) (GG-1…GG-60).
**Map of the surfaces:** [design/information-architecture.md](../design/information-architecture.md).
**Stack + gap register:** [design/tech-stack.md](../design/tech-stack.md).
**Visual reference:** eight plates, [design/README.md](../design/README.md).
**Module specs:** `docs/architecture/game-gui/spec-<module-id>.md` — none written yet.
**Plan / tasks:** `tasks/game-gui-plan.md` · `tasks/game-gui-todo.md` — not written yet.

*Path note: `SPEC.md` holds vfx-v3 and `tasks/plan.md` / `tasks/todo.md` hold perf v3, so this
initiative uses the prefixed paths above per AGENTS.md.*

---

## Assumptions — correct these now

1. **In-place refactor** of `web/fusion-rpg-web`. Not a second app built alongside.
2. **Old routes keep working until their replacement lands.** No flag day.
3. **No server API changes.** The one server-side touch is `fe-contracts`, which adds fixture
   emission to the existing `FusionRpg.E2E.Tests`.
4. **React 18 stays.** A React 19 upgrade is a separate, boring change.
5. **Presentation only, against a sealed contract.** No new gameplay. The FE **view contract is
   authored in full up front** — including fields no server endpoint fills yet. Those are declared
   `Pending` with a reason (§Contract). The server fills them when the feature ships; **no component
   changes when it does.**
6. **Art assets are out; the art *registry* is in.** `artFor(entity)` plus the generated placeholder
   ships; actual illustration does not.
7. **Audio assets are out.** The Sound tab ships disabled with its reason stated (gap G10).
8. **English only.** A second locale is enabled by the work, not delivered by it.

---

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `fe-tokens` | Token layer generated from `design/_kit/tokens.css`; self-hosted fonts + CJK stacks; the contrast check | — |
| `fe-i18n` | Lingui, English catalog, dev pseudolocale, extraction guard; the `Magnitude` type and `formatMagnitude` | — |
| `fe-contracts` | **The sealed FE view contract** — every shape a component may bind to, including not-yet-servable fields; the DTO→view adapter; shared JSON fixtures from `FusionRpg.E2E.Tests` for drift (tech-stack T1) | — |
| `fe-shell` | `LayerStack` store, six band shells, stage host, verb table, focus trap/restore, toasts, per-layer error boundaries, router-as-URL-adapter | `fe-tokens` |
| `fe-kit` | Primitives, the four states, the eleven entity ladders, comparison, control clusters | `fe-tokens`, `fe-i18n`, `fe-contracts` |
| `fe-bundle` | Code splitting, per-chunk budgets in CI, removal of `recharts` and `@xyflow/react` | `fe-shell` |
| `fe-devtree` | The developer tree, its gate, and the sweep of nine diagnostic routes into it | `fe-shell` |
| `fe-sanctum` | Title, save select, the Sanctum stage, the rail with unlock states, the stage HUD | `fe-shell`, `fe-kit` |
| `fe-collection` | Creatures, Relics, Fusion; equipped-vs-candidate comparison; virtualization | `fe-kit`, `fe-shell` |
| `fe-world` | World map stage as SVG, sector inspector, Expeditions, Pacts | `fe-kit`, `fe-shell`, `fe-bundle` |
| `fe-run-stages` | Lawn stage re-hosted under the stage model (Phaser lazy); Battle stage | `fe-shell`, `fe-bundle` |
| `fe-reference` | Almanac, Chronicle, the four chart primitives that replace recharts | `fe-kit` |
| `fe-system` | Settings, keymap, rebinding, Display and Sound tabs | `fe-shell` |
| `fe-flows` | Loadout, deploy targeting, the pact offer, the four first-session beats | `fe-collection`, `fe-run-stages` |

**Fourteen modules. No cycles** — `fe-kit` does not depend on `fe-shell` (ladders need no layer
stack), which is what lets the two heaviest foundation modules be built in parallel. `fe-contracts`
is a level-1 leaf that `fe-kit` and every surface module bind to; nothing binds to a REST DTO
directly.

---

## Build order

```text
1.  fe-tokens · fe-i18n · fe-contracts        (parallel, no deps)
2.  fe-shell  · fe-kit                        (parallel)
3.  fe-bundle · fe-devtree · fe-sanctum
4.  fe-collection · fe-world · fe-run-stages · fe-reference · fe-system   (parallel)
5.  fe-flows
```

This is the six-phase migration order from [design/tech-stack.md §9](../design/tech-stack.md)
expressed as modules. Two orderings in it are deliberate and worth confirming:

- **`fe-shell` is proven over the *existing* pages before any redesign.** Wrap a current page in a
  panel shell and assert the stage never unmounts. That de-risks GG-1/GG-11 while there is still
  something to compare against.
- **`fe-devtree` lands at level 3, not last.** Sweeping the nine diagnostic routes behind the gate is
  nearly free and is what stops the navigation reading as `AUDIT`. The old routes stay reachable
  inside the tree, so nothing is lost mid-migration.

---

## The contract — sealed now, filled later

> **⛔ HOLD ON SEALING — added 2026-08-22.** *"Authored in full"* is not currently true. The entity
> inventory this contract is built from
> ([design/README.md §6](../design/README.md)) missed [`item/`](item/) and [`action/`](action/), so the
> contract has **no item, action, socket, set, shield, or element-matchup entity** — 29 in all
> ([design/gap-audit-2026-08-22.md](../design/gap-audit-2026-08-22.md)).
>
> The extension rule below makes *adding* free forever, which is why most of the gap lands additively
> and why this is a hold rather than a rewrite. But **rename / remove / narrow costs a version bump and
> an ADR**, and shapes decided against eleven entities routinely need narrowing once forty are in view.
> **Do not declare this sealed until step 1 has been re-run as a complete sweep.** Sealing an incomplete
> vocabulary converts the cheap fix into the expensive one.

**Decision (2026-08-22).** The FE view contract is authored **in full, before the surfaces are
built**, and it is **sealed**: components bind to it and never to a REST DTO. Two consequences make
this worth doing up front.

### 1. Data that does not exist yet is *declared*, not deferred

Several designed surfaces need facts no endpoint produces today — unlock state, loadout berths, a
creature being "recovering" or "away", pact tribute timing. Those fields exist in the contract from
day one, wrapped:

```ts
type Pending<T> =
  | { state: 'known';   value: T }
  | { state: 'absent' }                      // the server knows, and the answer is none
  | { state: 'pending'; reason: string }     // no endpoint fills this yet
```

**`absent` and `pending` are different and conflating them is the bug this prevents.** "You have no
relics" and "relics are not implemented" look identical to a naive optional field and must never look
identical to a player.

The `reason` string is not a developer note — **the UI renders it**. That is what makes a
not-yet-servable surface ship looking deliberate rather than broken: *"Expeditions unlock when you
hold a sector"* satisfies GG-17's locked state and GG-55's never-disable-without-saying-why, using
the states the kit already has.

### 2. Filling a field later touches one file

When the server ships the endpoint, the **adapter** changes `pending` to `known`. No component, no
test fixture shape, no layer. That is the whole return on sealing it early.

### Extension rule — additive only

| Change | Allowed |
|---|---|
| Add an optional field | ✅ yes, any time |
| Flip a `pending` field to `known` | ✅ yes — that is the mechanism |
| Add a new entity or a new variant | ✅ yes |
| Rename or remove a field, narrow a type, change a unit family | ❌ **contract version bump + ADR** |

Same shape as the repo's other sealed surfaces: Foundation Effects is sealed at its contract version,
and the atom vocabulary is closed with an ADR to extend. This is that pattern applied to the FE.

**`fe-contracts` therefore lands first and is reviewed hardest after `fe-shell`.** Getting a field
wrong is cheap while nothing binds to it and expensive once eleven modules do.

---

## Enforcement is per module, not a final phase

Each module lands its own checks from [game-gui-principles.md §19](game-gui-principles.md). There is
deliberately **no `fe-guards` module** — a checks-at-the-end module is how checks get cut.

| Module | Lands |
|---|---|
| `fe-contracts` | fixture drift vs the server · every `pending` field carries a non-empty reason · no component imports a REST DTO |
| `fe-tokens` | contrast matrix · no-hex-outside-theme |
| `fe-i18n` | catalog completeness · CJK fixture · unit-family formatter has no bare-number overload |
| `fe-shell` | band lint · stage-persistence · Esc/stack · focus trap and restore · mutation feedback |
| `fe-kit` | four-states per surface · axe scan · banned vocabulary |
| `fe-bundle` | entry-chunk ceiling · Phaser absent from entry |
| `fe-collection` | volume fixtures at 10/100/1000 · diff-state matrix |
| `fe-sanctum` … `fe-flows` | reachability matrix · viewport sweep |

---

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | **The contract is authored before most of its consumers exist**, so it will miss fields | Additive extension is free by rule (§Contract); only rename/remove/narrow costs a version bump. The failure mode is therefore "add a field later", not "rewrite the surfaces" |
| R1b | A `pending` field ships with a weak or missing reason, so a surface reads as broken rather than unbuilt | `fe-contracts` lands a check: every `pending` field must carry a non-empty, player-facing reason |
| R2 | `fe-shell` is the keystone — if the layer model is wrong, eleven modules inherit it | It is proven against existing pages first, and it is the one module whose spec should be reviewed hardest |
| R3 | Test suite is `data-testid`-heavy; the new rules make role/name queries viable | Gap G11 — migrate opportunistically per module, not as a big-bang pass |
| R4 | Coverage config scopes 9.3% of the FE with the game modules at 0% | Gap G6 — `fe-shell` rewrites the include list as part of its own work |
| R5 | Two cost pools (`stamina` / `qi`) reach the FE before the action layer is built | Presentation only: the actor panel renders whatever the registry returns. No FE cost logic |

---

## Out of scope for this initiative

Illustration and final art · audio assets · a second locale · the sector-graph authoring tool ·
React 19 · any server API change beyond fixture emission · new gameplay of any kind.
