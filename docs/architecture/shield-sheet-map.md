# Capability map: shield-sheet

**Status:** Phase 0 **approved by owner mandate** 2026-09-10 (Shield tab **in**; shared Hot with Condition).  
**Program id:** `shield-sheet`  
**Ideal:** [shield-sheet-ideal.md](shield-sheet-ideal.md)  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md)  
**Design SSOT:** [../design/spec-shield-and-elements.md](../design/spec-shield-and-elements.md) · [shield-system-spec.md](shield-system-spec.md)  
**Existing host (amend):** [actor-sheet/spec-shield-tab.md](actor-sheet/spec-shield-tab.md)  
**Shared Hot summary:** [condition-glance/spec-sheet-hot-projection.md](condition-glance/spec-sheet-hot-projection.md) — **depend, do not fork**  
**Shared glance piece:** [gui-lego/spec-shield-status.md](gui-lego/spec-shield-status.md) (mounted by condition-glance)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md) — pull Shield from P4 into this program  
**Sibling programs:** [condition-glance-map.md](condition-glance-map.md) · [derived-cook-map.md](derived-cook-map.md)  
**Plans (after specs):** `tasks/shield-sheet-plan.md` · `tasks/shield-sheet-todo.md`  
**DESIGN-GATE:** UI + Player menus · Elements · shield system · combat magnitudes (`long`)

**Owner mandate:** Condition needs a real shield component; Hot shield must not be glance-only.
Full stack tab + shared runtime projection. Cheap empty wells / FE fixtures are **out**.

---

## Owner decisions (locked 2026-09-10 strengthen)

| Id | Decision |
|---|---|
| **S1** | Layers on **`sheet.shieldLayers`** beside `shieldSummary` — reject player `GET …/shields` as tab SSOT |
| **S2** | Summary `elementId` = front drain-order layer; optional `stacks` = layer count |
| **S3** | Server `ActorLiveState` bag from Injector; `ProjectSheet` reads — ask before new HTTP channel |
| **D8** | Apply-outcome toast / full cascade inspect = **explicit P3 defer**; segment regen in v1 only if runtime exposes |

---

## What this program is

Ship ActorSheet **Shield** as GUI Lego: ordered stack from `ShieldRuntime`, segmented drain-order bar,
omni shield StatRows, Hot layers on the wire — sharing **one** Hot snapshot seam with
condition-glance’s `shieldSummary`.

---

## Ownership splits

| Concern | Owner | Must not |
|---|---|---|
| Hot `/sheet` `shieldSummary` + `liveStatuses` + live bag | **condition-glance** `sheet-hot-projection` | Second ProjectSheet path |
| Ordered **`sheet.shieldLayers`** (same compose) | **shield-sheet** `shield-stack-projection` | HUD AggregateByElement; player `/shields` as SSOT |
| Glance card `shield-status` | gui-lego piece; Condition mounts | Mount when null/0 |
| Tab segmented bar | `shield-stack-bar` | Three fake empty wells as product |
| Omni shield family rows | `shield-omni-rows` | Duplicate Derived god console |
| Element paint | shared `element-paint-ssot` | Private hex |

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `shield-stack-projection` | BE: ordered layers from GetShields; Hot flush with summary | ShieldRuntime, sheet-hot seam | [shield-sheet/spec-shield-stack-projection.md](shield-sheet/spec-shield-stack-projection.md) |
| `shield-stack-bar` | Segmented drain-order bar piece | paint SSOT, theme-bind | [gui-lego/spec-shield-stack-bar.md](gui-lego/spec-shield-stack-bar.md) |
| `shield-layer-inspect` | Inspect slice: layer hp/element/source fiction + cascade render | stack projection | [gui-lego/spec-shield-layer-inspect.md](gui-lego/spec-shield-layer-inspect.md) |
| `shield-omni-rows` | Omni `combat.shield.*` StatRows from sheet Derived join | sheet channels | [shield-sheet/spec-shield-omni-rows.md](shield-sheet/spec-shield-omni-rows.md) |
| `shield-surface-vm` | Pure fold → shield surface VM | stack + omni | [shield-sheet/spec-shield-surface-vm.md](shield-sheet/spec-shield-surface-vm.md) |
| `shield-recipe-wire` | Recipe + bind + thin host + bus/revision | all pieces | [shield-sheet/spec-shield-recipe-wire.md](shield-sheet/spec-shield-recipe-wire.md) |
| `shield-tab` | Amend actor-sheet thin tab | recipe-wire | [actor-sheet/spec-shield-tab.md](actor-sheet/spec-shield-tab.md) (rewritten) |

**Depends on (sibling, not owned):** `sheet-hot-projection`, `element-paint-ssot`, `theme-bind`, `shield-status`.

---

## Build order

```text
Wave 1 — Hot truth (coordinate with condition-glance Wave 1)
  sheet-hot-projection (sibling) + shield-stack-projection (same Hot snapshot)

Wave 2 — pieces
  shield-stack-bar · shield-layer-inspect · shield-omni-rows
  (shield-status draft remains condition-glance Wave 2)

Wave 3 — surface
  shield-surface-vm → shield-recipe-wire → amend shield-tab host
```

---

## Success criteria (program)

- Hot sheet: `shieldSummary` filled when shield present; cold null (condition-glance criteria).
- Hot layers: ordered ≤3 instances match `GetShields`; cold empty/pending honest.
- Tab shows segmented bar per design §3.1 — not three permanent “Empty layer” labels.
- Omni shield rows from sheet Derived; paint from element-paint-ssot.
- Glance and tab agree on totals (sum of layers = summary current/max within policy).
- No FE-invented stacks; no Ward noun; magnitudes `long`.

---

## Coverage-gap register (strengthen 2026-09-10)

| Id | Gap | Disposition |
|---|---|---|
| CG-S1 | Transport A vs B | **Closed** — **S1** `sheet.shieldLayers` |
| CG-S2 | Summary elementId / stacks | **Closed** — **S2** |
| CG-S3 | Hot live bag | **Closed in spec** — **S3** (shared with condition-glance) |
| CG-S4 | Three Empty layer wells as product | **Closed in spec** — ban; pending vs Hot-empty |
| CG-S5 | Omni row Fields / allow-list | **Closed** — `shield-omni-rows` |
| CG-S6 | Recipe / draft debt | **Closed as gate** — recipe-wire draft-exists checkboxes |
| CG-S7 | Regen on segment | **Deferred** — only if runtime exposes (**D8**) |
| CG-S8 | Cascade / apply-outcome inspect | **Deferred P3** — **D8** |
| CG-S9 | SignalR event | **Closed in spec** — preferred `ActorLiveStateChanged` |

---

## Explicitly out

| Out | Why |
|---|---|
| Absorb cascade trainer / matrix editor | Later (**D8** / P3 for inspect) |
| Player `GET …/shields` as tab SSOT | Rejected (**S1**) |
| Derived cook defects | `derived-cook` |
| Condition layout / standing / status strip | `condition-glance` |
| Lawn mute F9 (HUD-only) | HUD program |
| Bare `tasks/plan.md` | Prefixed pair only |
