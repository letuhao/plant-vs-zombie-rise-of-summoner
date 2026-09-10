# Shield sheet — the ideal

**Status:** idea phase locked into `/spec` 2026-09-10 (owner: Shield tab **in**; Hot shield shared with Condition).  
**Program id:** `shield-sheet`  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md)  
**Surface:** ActorSheet **Shield** tab — full stack readout (not Condition glance card alone).  
**Design SSOT:** [design/spec-shield-and-elements.md](../design/spec-shield-and-elements.md) · [shield-system-spec.md](shield-system-spec.md)  
**Sibling programs:** [condition-glance-map.md](condition-glance-map.md) (glance `shield-status` + Hot summary) · [derived-cook-map.md](derived-cook-map.md)  
**Plans (later):** `tasks/shield-sheet-plan.md` · `tasks/shield-sheet-todo.md`

---

## Out loud (load-bearing)

1. Shield is an **RPG-layer** stack (`ShieldRuntime`) — never blocked by Plant armour Unity fields.
2. Stage + layers — Shield is a band-2 tab; Condition glance is a sibling consumer of the same runtime.
3. Recipe + fold + bus — no chrome-only god TSX that always paints “Empty layer”.
4. Theme packs + `element-paint-ssot` color typed segments — untyped hatch is a pack/theme rule.
5. Buy before build — **recharts** (or locked kit) for segmented/radial fills; paint hex from packs.
6. Each bug is a module: Hot projection, stack DTO, segmented bar piece, omni rows, tab recipe.
7. Noun is **Shield**, never Ward. No engine `SourceId` as the only player label.

---

## Why Shield tab cannot stay out

Condition glance mounts **`shield-status`** from Hot `/sheet.shieldSummary`. That summary is an
**aggregate** of the same runtime the Shield tab must show as **ordered layers**. Shipping Hot only for
glance would freeze a second, incomplete presentation of shield as “done.” Owner lock 2026-09-10:
**three programs, three plans** — Condition, Derived cook, Shield sheet.

---

## One runtime SSOT, two projections

```text
ShieldRuntime.GetShields(owner)     ← sole live SSOT (≤3, drain order)
  │
  ├─ Totals() ──► ActorShieldSummaryDto     → condition-glance (shield-status, radial ring)
  │                 elementId = front drain-order (S2); stacks = count
  │
  └─ each instance ──► sheet.shieldLayers[]  → shield-sheet tab (segmented bar + inspect)
                         same ProjectSheet flush (S1) — not GET /shields as tab SSOT
```

Hot: Injector → Server `ActorLiveState` bag → `ProjectSheet` (**S3**).  
HUD `AggregateByElement` and lawn `rpgShield*` are **observe siblings**, not a third sheet SSOT.
Do not reverse-fold lawn dumps into the sheet.

### Owner locks (strengthen 2026-09-10)

**S1–S3**, **D8** — see [shield-sheet-map.md](shield-sheet-map.md). Transport A rejected for player tab.

---

## Inventory (2026-09-10)

### Built

| Item | Evidence |
|---|---|
| Core stack runtime, absorb math, max 3 | `Combat/Shield/*` |
| Wire `ActorShieldSummaryDto` on sheet | `ActorSheetDtos.cs` |
| Lawn / HUD totals + debug layer snapshot | SimEngine, ActorHud, CheatCommandRunner |
| FE sheet type mirrors | `aura.ts` |

### Built, defective / wiring

| Item | Notes |
|---|---|
| `ProjectSheet` always `ShieldSummary = null` | Permanent stub — condition-glance `sheet-hot-projection` removes it |
| FE `ShieldTab` | Always three “Empty layer” wells + PendingNote — reads as empty stack, not unwired API |
| `ActorView.shieldStack` | Always pending |

### Real gaps

| Item | Notes |
|---|---|
| Ordered layer DTO / `GET …/shields` or `sheet.shieldLayers` | No player API |
| Segmented drain-order bar piece | Design §3.1; tab draft said 3 radials — **this ideal locks design §3.1** |
| Omni shield StatRows on tab | Spec wanted; not built |
| `shield-status` HTML draft + recipe slot | condition-glance piece debt |
| Hot Injector→Server snapshot for ProjectSheet | Shared seam with condition-glance |

---

## Visual grammar lock

**Authoritative:** [spec-shield-and-elements.md](../design/spec-shield-and-elements.md) §3.1 —

- **One** segmented bar, drain order left→right.
- Segment width ∝ `maxHp`; fill ∝ `hp/maxHp`.
- Broken layer keeps empty slot (does not vanish).
- Priority tiers labelled (aura / skill / innate).
- Element colour (or untyped hatch); regen rate on segment when present.
- Cascade breakdown for inspect — **server sends cascade; UI does not recompute** remainder.

**Supersedes** thin `spec-shield-tab.md` “three radials” as the primary grammar. Optional small
per-layer radials may appear in inspect chrome later — not as the stack SSOT.

Empty wells when Hot and stack count &lt; 3: dashed slots OK. When API **pending**: lifecycle pending
piece — **never** three fake “Empty layer” labels pretending the stack is known-empty.

---

## Closed module catalog

| Module id | Owner | Spec |
|---|---|---|
| `sheet-hot-projection` | **condition-glance** (summary + live statuses) | Shared seam; this program **depends**, does not fork |
| `shield-stack-projection` | **shield-sheet** | Ordered layers on wire; same Hot flush as summary |
| `shield-stack-bar` | gui-lego piece | Segmented bar |
| `shield-layer-inspect` | gui-lego / surface | Layer detail + apply-outcome honesty later |
| `shield-omni-rows` | shield-sheet | Omni `combat.shield.*` from Derived sheet join |
| `shield-surface-vm` | shield-sheet | Pure fold |
| `shield-recipe-wire` | shield-sheet | Recipe + thin host |
| `shield-tab` | amend actor-sheet host | Thin tab |

---

## Correct shapes / bans

| Ban | Correct |
|---|---|
| FE-invented stacks for glance | Hot `shieldSummary` from Totals |
| HUD element-merge as tab SSOT | Instance layers from GetShields |
| Always-empty three wells as “UI” | Pending until Hot/layers wired; then real count |
| Recompute absorb remainder in FE | Render server cascade |
| Ward noun | Shield |
| Second ProjectSheet Hot path | One compose; stack module extends same snapshot |

---

## Deferred

| Out | Why |
|---|---|
| Full absorb cascade trainer / sandbox | Later content |
| Shield matrix editor (combat vs shield table diff) | Elements / balance tools |
| Derived cook defects | `derived-cook` |
| Condition layout / standing | `condition-glance` |

---

## The real question

Can Condition glance and Shield tab **tell the same truth** from `ShieldRuntime` without a fake FE
stack? This program answers yes — stack projection + segmented bar + omni rows, sharing Hot summary
with condition-glance.
