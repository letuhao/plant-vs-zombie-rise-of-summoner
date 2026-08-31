# Capability map: actor-hud

Source: [actor-hud-ideal.md](actor-hud-ideal.md) (strengthened 2026-08-30) ·
[actor-hud-audit-2026-08-30.md](../research/actor-hud-audit-2026-08-30.md) ·
[actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md) ·
[10-actor-hud.html](../design/10-actor-hud.html) · [vfx/spec-unit-frame.md](vfx/spec-unit-frame.md).

**Status: implemented 2026-08-31** — all six modules shipped. Specs signed off in [actor-hud-todo.md](../../tasks/actor-hud-todo.md). Optional LIVE eyeball remains.

Module specs live in [actor-hud/](actor-hud/), one per module id, written in dependency order once this
map is approved.

---

## What this program is

**Band B per-unit HUD** — compact readout above each lawn unit (plant or zombie): identity row, resource
row (shield slot subsumes today's bar), status strip. One **`Occupant.hud`** snapshot from the injector
feeds **Unity world render**, **Phaser canvas**, and **web Inspector expansion**.

**Dual-render is a v1 gate** — Phaser parity is required for program done, not a follow-up phase.

**Explicitly not this program:** Band A match chrome (commander-surface); status sustain motion grammar
(vfx-v3); ActorPanel full stat sheet; boss tier semantics until expedition spawn exposes a signal;
mid-run commander picker on units.

---

## The acceptance rule that governs every module

**No module may be marked done on internal criteria alone.** Shield could render in Unity while Phaser
still showed HP-only and Inspector still used comma-separated chip text — that is the shipped defect
today.

> **Program-level acceptance:** *During a live lawn with elites, shields, and custom statuses, a player
> on Unity and a spectator on Phaser can identify boss tier, shield element, and top-priority statuses
> on a unit **without opening Inspector**; web Inspector shows the same `Occupant.hud` fold fields, not
> a divergent text layout. Toggling shield display hides the resource row only — sustain VFX remain.*

Until **Playwright** (`web/fusion-rpg-web/e2e/actor-hud.spec.ts`) asserts fold + Phaser canvas semantics,
the program is **not done**, regardless of module status. Unity LIVE eyeball is a manual supplement until
in-game capture exists.

Each module below names its **acceptance share** — one concrete automated slice.

---

## Decisions this map is built on (ideal §0 — do not reopen)

| Decision | Rationale |
|----------|-----------|
| Band B only; Band A = commander-surface | IA + plate 04 — match snapshot, not per-tile commander |
| Three rows: identity / resources / status strip | Industry grammar + UnitFrame placement |
| Dual-render SSOT: one `Occupant.hud` → Unity + Phaser + web fold | Eliminates Phaser/Unity drift |
| Status VFX + HUD strip complementary | User read: icon = meaning, VFX = motion |
| v1 lawn: no full numeric stat readout | GG-60; ActorPanel for numbers |
| HP sliver default **off** v1 | Reduce clutter; unify model before optional sliver |
| ShieldBarPool migrates into resource row | Subsumption, not parallel systems |
| No per-unit commander mark | Commander identity is Band A only |
| Inspector expands fold — not primary glance readout | Token/chip on canvas under time pressure |

### Pipeline decisions — do not reopen

| Decision | Rationale |
|----------|-----------|
| Hot-only HUD read during match | [overlay-control-loops.md](overlay-control-loops.md) — no Cold/SQL on display path |
| Derived pinned at `EntityApply` | `levelBand` from `progression.power` via `InjectorDerivedOverride` — not `theLevel` |
| Single builder entry | `ActorHudBuilder.Build(ptr)` only — renderers read cache/model |
| No REST/SQL mid-match for HUD | UniqueActor FSM affects HUD after bind + apply only |
| Feature gate per field | Slot ships only when SSOT row wired and Read API used — [pipeline audit §9](../research/actor-hud-data-pipeline-audit-2026-08-30.md) |

---

## Data SSOT and pipeline

Full tables, FSM alignment, forbidden reads, duplicate retirement:
[actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md).

### Condensed master table

| HUD field | SSOT | Read API (builder) | v1 |
|-----------|------|-------------------|-----|
| `identity.role` | `MatchUniqueBindingsFacet` | `TryGetByPtr(ptr)` | wired |
| `identity.levelBand` | Derived pin @ EntityApply | `InjectorDerivedOverride.TryGet` → `PowerBandDisplay` | wired |
| `identity.tier` | observe flags / derived | unique partial; boss omit | partial |
| `resources.shield` | `ShieldRuntime` | Shield gate runtime totals/stacks | wired (slice 2) |
| `statuses[]` | `StatusRuntime` | instances by owner ptr | wired (slice 2) |
| `overflow` | `ActorHudLayout` | computed | wired (slice 2) |

### Single pipeline

```text
EntityApply pin → ActorHudBuilder → actorHud on observe → fold → Occupant.hud → Unity + Phaser + Inspector
```

**P1.5 dependency:** `actor-hud-dump` requires EntityApply derived pin before levelBand is considered wired.

---

## DTO and observe SSOT

Types defined in **`actor-hud-core`**; populated by **`actor-hud-dump`**; consumed by fold, unity, phaser.

### `ActorHudSnapshot` / `Occupant.hud`

| Field | Owner | Shape / rule |
|-------|-------|--------------|
| `identity.tier` | dump | `"normal"` \| `"elite"` \| `"boss"` \| `"unique"` — **v1:** emit `unique` from `flags.unique`; `elite` from demon/rarity when wired; **omit `boss`** until expedition |
| `identity.role` | dump | `"specimen"` when `instanceId` bound; else `"vanilla"` |
| `identity.levelBand` | dump | Display `int` from `progression.power` Θ band — **not** raw Θ or `theLevel` |
| `identity.flags` | dump | `string[]` e.g. `"unique"`, `"demon"` |
| `resources.shield` | dump | `{ hp, max, stacks: [{ element, hp, max }] }` from `ShieldRuntime` |
| `resources.hpSliver` | dump | **Omit in v1** (`hpSliverEnabled: false` in tunables) |
| `resources.meters` | dump | Optional `{ id, ratio }[]` — v1 may omit if no lawn meters |
| `statuses[]` | core + dump | `{ id, cc, magnitudeBand }` — ids ⊆ status-ssot; sorted by `ActorHudLayout.Prioritize` |
| `overflow.statusCount` | core | Hidden status count for `+N` pip |

JSON keys on the wire use **camelCase** (`levelBand`, `magnitudeBand`) matching existing observe payloads.

### Observe transport (owned by `actor-hud-dump`)

| Path | When | Payload |
|------|------|---------|
| **`actorHud` on entity rows** | `debug.board-stats` plants/zombies arrays; `entity.stats` deltas | Nested object per ptr — same shape as `Occupant.hud` |
| **`debug.actor-hud`** (optional) | Invalidation-only delta | `{ ptr, actorHud }` — avoids full board-stats rescan |
| **Web fold** | `lawnProjectorFold` | Maps `actorHud` → `Occupant.hud` on living occupants |

**Not used:** live REST poll for HUD fields mid-match. Presentation reads observe only.

**Back-compat:** Existing `rpgShieldHp` / `rpgShieldMax` on entity payloads remain until fold migrates
Inspector to `hud.resources.shield`; fold may derive shield from either during transition (dump spec).

---

## Cross-program rules

| Program | Rule |
|---------|------|
| **vfx / UnitFrame** | HUD attaches via `UnitFrameResolver` only — no ad-hoc `BodyWorld` / bounds reads |
| **commander-surface** | Band A chip only; no per-tile commander badge |
| **shield-system-spec** | `ShieldRuntime` unchanged; world bar presentation migrates to HUD shield slot |
| **status-ssot** | Strip `id` values ⊆ closed vocabulary |
| **element-hub-ssot** | Shield segment colors; element ring on status tokens |
| **actor-sheet** | Full numbers on ActorPanel — HUD never replaces six-tab sheet |

---

## Modules

| Module id | Responsibility | Depends on | Blocks | Acceptance share |
|-----------|----------------|------------|--------|------------------|
| `actor-hud-core` | DTO types, slot catalog, priority sort, level band display math, tunable load | — | all | `ActorHudLayoutTests`: priority, overflow, band mapping |
| `actor-hud-dump` | `ActorHudBuilder`, EntityApply derived pin, invalidation, observe `actorHud` | core + P1.5 pin | fold, unity | Golden JSON + read-surface compliance; pin at apply |
| `actor-hud-fold` | `Occupant.hud` types, projector fold, extend status ids, Inspector bind | core, dump contract | phaser | `lawnProjectorFold.test.ts`: `Occupant.hud` populated from `actorHud` |
| `actor-hud-unity` | `ActorHudPool`, UnitFrame placement, three-row renderers | core, dump | migration | Guard: `ActorHudPool_uses_UnitFrameResolver`; LIVE lab board eyeball |
| `actor-hud-phaser` | `SyncFromModelSystem` HUD chips from model | fold | E2E | Sync unit test: fixture occupant matches fold golden |
| `shield-slot-migration` | Remove standalone `ShieldBarPool.TickSync`; shield via pool only | unity | — | No duplicate shield bar; sustain VFX unchanged; perf probe note |

### Build order

```text
actor-hud-core
    └── actor-hud-dump
            ├── actor-hud-fold ──► actor-hud-phaser ──► E2E
            └── actor-hud-unity ──► shield-slot-migration ──► E2E
```

`actor-hud-fold` and `actor-hud-unity` may proceed in parallel once dump contract is frozen.

---

## Open questions — resolved for v1 specs

| Item | v1 resolution |
|------|---------------|
| Boss tier | Omit `boss` in builder; tier frame shows `unique` / `elite` only |
| HP sliver | Tunable off; Phaser keeps vanilla HP bar |
| Level badge | `progression.power` Θ → display band |
| Event vs poll | Event invalidation + fallback tick (dump spec) |
| Phaser parity | Required for program done |
| Status icon art | Almanac initials / procedural chips v1; art pipeline follow-up |

Owner may override in P0 review.

---

## Related artifacts

| Artifact | Path |
|----------|------|
| Ideal | [actor-hud-ideal.md](actor-hud-ideal.md) |
| Audit (user) | [actor-hud-audit-2026-08-30.md](../research/actor-hud-audit-2026-08-30.md) |
| Audit (pipeline) | [actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md) |
| Plate | [10-actor-hud.html](../design/10-actor-hud.html) |
| Plan | [tasks/actor-hud-plan.md](../../tasks/actor-hud-plan.md) |
| Tasks | [tasks/actor-hud-todo.md](../../tasks/actor-hud-todo.md) |

---

## DESIGN-GATE §5 checklist (this map)

| Box | Status |
|-----|--------|
| Subsystem located in DESIGN-GATE §1 index | Player GUI, stats, status, shield, VFX rows read |
| Proposals verified against code | Audit verification log; `GameDumps.AddRpgShield`, no `Occupant.hud` yet |
| Section context respected | Band A = commander-surface; Band B = this map |
| Constraints tested before claimed | No golden movement claimed — greenfield program |
| GG-60 / GG-1 / GG-23 applied | Acceptance rule + audit user perspective |
| Cross-program boundaries stated | § Cross-program rules above |
