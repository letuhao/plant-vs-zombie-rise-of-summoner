# Actor HUD audit — multi-perspective (user-first)

**Date:** 2026-08-30  
**Scope:** Per-unit Band B HUD on the lawn — identity, resources, status strip; dual-render SSOT  
**Out of scope:** Band A match chrome (commander-surface), status sustain motion grammar (vfx-v3), ActorPanel full sheet  
**Status:** Research reference. **Audit only — no build authorized.** Verified against code 2026-08-30.

**Governed by:** [actor-hud-ideal.md](../architecture/actor-hud-ideal.md) ·
[game-gui-principles.md](../architecture/game-gui-principles.md) (GG-1, GG-23, GG-60) ·
[information-architecture.md](../design/information-architecture.md) ·
[vfx/spec-unit-frame.md](../architecture/vfx/spec-unit-frame.md) ·
[commander-surface-ideal.md](../architecture/commander-surface-ideal.md)

**Visual guide:** [10-actor-hud.html](../design/10-actor-hud.html) · plate §H player scenarios

**Why this file exists.** The actor-hud ideal captured architecture and slot grammar. This audit
strengthens it with the same multi-perspective pass used for commander-surface and status VFX:
**player questions first**, then FE/IA, injector/perf, data/RPG layer, and cross-program boundaries —
each ending in **built / wiring / gap** tables verified against shipped code.

---

## Executive summary

**Verdict:** There is **no unified per-unit HUD** today. The player reads unit state through **three
partial stacks** with no shared model:

| Surface | What it shows | Primary path |
|---------|---------------|--------------|
| Unity world | Aggregate RPG shield bar only | `ShieldBarPool.TickSync` under `VfxDirector.Tick` |
| Phaser lawn canvas | Vanilla HP bar + numeric label | `SyncFromModelSystem.setHpDisplay` |
| Web Inspector | KeyValue text (HP, ATK, armor, shield) + status chip strings | `lawnProjectorFold` → `LawnPage` selection |

**Strengthen goal:** One **Band B** snapshot (`Occupant.hud`) built in the injector, rendered in
Unity (`ActorHudPool`), mirrored on Phaser, and expanded in web Inspector — **complementary** to
status sustain VFX (motion), not a replacement.

**Code verification (2026-08-30):** Grep finds **no** `Occupant.hud`, `ActorHud`, `ActorHudPool`, or
`ActorHudBuilder` in the tree. `ShieldBarPool` remains the only Unity world UI for RPG shields.
Phaser draws HP only; fold carries `rpgShield*` but canvas never reads it.

---

## 1. User perspective — questions the lawn must answer under time pressure

**Anchor:** GG-60 — legibility wins on surfaces the player acts on while something is happening;
diegetic framing lives on surfaces read at leisure (almanac, ActorPanel).

### 1.1 Player questions at a glance

Each question maps to one slot in [actor-hud-ideal.md §4](../architecture/actor-hud-ideal.md). If two
slots answer the same question, one must go (plate 10 §B).

| Player question | Ideal slot | Today (user pain) |
|-----------------|------------|-------------------|
| Boss or elite? | Tier frame + level band | No frame on unit; level not on lawn |
| My demon / bound specimen? | Role badge | No badge; binding only in Inspector when selected |
| Why isn't my hit doing damage? | Shield element segments + resource row | Unity bar shows fill only — no element read; Phaser has no shield |
| What's afflicting this unit? | Status strip (icons) | Unity: motion VFX only; web: comma-separated chip **text** in Inspector |
| Which unit is the threat? | Side + type icon (identity row) | Type icon in Inspector only; canvas shows HP bar without identity |
| How hurt is it? | Optional HP sliver (v1 default off) | Phaser vanilla HP bar; Unity has no HP sliver |

### 1.2 Scenario walkthroughs — user read (predicted)

**Horde wave.** Eight or more zombies cross the lawn; two elites carry shields and mixed statuses.
The player cannot pause to open Inspector for each tile. **Ideal read:** tier frames pop on elites;
status strip shows top-priority CC and afflictions; overflow collapses to `+N` — never micro-shrinks
tokens below readable size (GG-60). **Today:** shield bars may appear on shielded units; no tier,
no status icons on canvas; crowding is unreadable if we relied on Inspector text.

**Elite + dual status.** A boss-tier zombie has a fire shield, `expose`, and `command` active.
**Ideal read:** resource row shows fire-colored segment; status strip shows two tokens; sustain VFX
(Orbit, CrackleJitter, CommandCrownPulse) provide **motion** without repeating the same fact as the
icons. **Today:** VFX motion is present on Unity; no static icon strip; shield bar does not encode
element identity at a glance.

**Bound demon plant.** A specimen the player deployed from the roster should be recognizable on the
lawn without selecting it. **Ideal read:** role pip + level band on identity row. **Today:** web fold
may carry `instanceId` when bound, but nothing draws on the unit sprite or Phaser cell.

**Phaser spectator.** A second screen (browser lawn canvas) should tell the same story as in-game Unity
HUD. **Ideal read:** identical slot semantics from one fold. **Today:** Phaser shows HP bar only;
Unity shows shield only — **deliberate drift**, not a shared model.

### 1.3 Accessibility and readability checks

| Check | Rule | Ideal | Today |
|-------|------|-------|-------|
| Shield element | Color + shape (not color-only) | Element-colored segments + catalog shape | Gray/green fill steps; element in data, not shown |
| Status identity | Icon + optional CC corner | Almanac token at 16–24px | Text chip ids in Inspector |
| Overflow | `+N` pip, priority drop | Plate 10 §D | No strip — N/A |
| Engine vocabulary on lawn | GG-23 | Bands and player tokens only | Inspector shows `instanceId`, `ptr`, raw chip ids |
| Full stat wall on lawn | GG-60 | ActorPanel for numbers | Inspector KeyValue table if player selects unit |

---

## 2. FE / IA perspective

### 2.1 Band A vs Band B

| Band | Scope | Owner program | Per-unit HUD? |
|------|-------|---------------|---------------|
| **A** | Sun, wave, commander + aura chip, transport | commander-surface | **No** — match snapshot at `board.start` |
| **B** | Compact readout above each unit | actor-hud (this) | **Yes** |

Commander identity is **match-level**, frozen at board start — not repeated on every zombie tile
([commander-surface-ideal.md §0](../architecture/commander-surface-ideal.md)).

### 2.2 Entity ladder

| Rung | Lawn surface | Actor HUD role |
|------|--------------|----------------|
| **Token** | Status strip icons, tier pip | Primary Band B density |
| **Chip** | Identity row composite | Tier + role + level badge |
| **Panel** | ActorPanel (`C` / click) | Full numbers — **not** replaced by HUD |

Token rung on the lawn = canvas + world overlay. Panel rung = Band-2 layer over stage (GG-1).

### 2.3 Web today — Inspector is schema readout, not glance HUD

[`LawnPage.tsx`](../../web/fusion-rpg-web/src/features/lawn/LawnPage.tsx) selection panel:

- `TypeIcon` + type name + **monospace `ptr`**
- KeyValue rows: Cell, HP, ATK, Armor, Armor2, **Shield** (`rpgShield/rpgShieldMax`), Speed, Interval
- **Chips:** comma-separated `statusChips` strings
- **instanceId** with "binding unknown/stale" when missing

This is useful for debugging and selection detail, but it is a **table of fields** (GG-25 shape) —
not what a player under time pressure should need. Actor HUD moves glance read to the canvas/world;
Inspector **expands** the same `Occupant.hud` fold, not a divergent layout.

### 2.4 Phaser today

[`SyncFromModelSystem.ts`](../../web/fusion-rpg-web/src/game/systems/SyncFromModelSystem.ts):

- `setHpDisplay` — vanilla `hp` / `maxHp` ratio bar + numeric label
- **No** `rpgShield`, identity row, or status strip

### 2.5 Built / wiring / gap — web + Phaser

| Capability | Built | Wiring | Gap |
|------------|-------|--------|-----|
| Occupant fold (stats, chips) | Yes | `lawnProjectorFold` | No `hud` sub-object |
| RPG shield in fold | Yes | `rpgShield*` keys on occupant | Not drawn on Phaser |
| Status chips in fold | Partial | 9 ids in `OBSERVE_CHIPS` | 13 custom VFX ids; no icon strip |
| Inspector detail | Yes | `LawnPage` KeyValue | Not tied to future `Occupant.hud` |
| Phaser HP bar | Yes | `setHpDisplay` | No Band B rows |

---

## 3. Injector / perf perspective

### 3.1 ShieldBarPool — vertical slice that never generalized

[`ShieldBarPool.cs`](../../src/FusionRpg.Injector/Fx/ShieldBarPool.cs):

- Shield-only MeshRenderer pool (~7 renderers per owner: track, segments, pips)
- `TickSync` from [`VfxDirector.Tick`](../../src/FusionRpg.Injector/Fx/VfxDirector.cs) whenever
  `ShieldBarPool.WorldBars > 0` — hot path coupling
- Placement: `UnitFrameResolver.Resolve` + legacy `WorldYOffset` tunable — shares UnitFrame SSOT
  with status VFX but duplicate Y logic vs future unified HUD stack
- Fill: `ShieldBarVisual.DisplayRatio` (10% steps); stacks from `ShieldRuntime.VisitOwners`

**Perf note:** 2026-08 audit flagged per-hit scans and uncached resolves on the main thread — HUD
build should **invalidate on events** (`shield.*`, `debug.status.*`, binding changes) rather than
re-walking all owners every frame when nothing changed. Final policy is a build-time choice; audit
recommends event-driven invalidation (ideal §11 item 4).

### 3.2 UnitFrame — reuse for Band B placement

Status VFX program shipped `UnitFrameResolver` and tuning ([`spec-unit-frame.md`](../architecture/vfx/spec-unit-frame.md)).
Actor HUD **must** attach at the same crown/body anchors — no ad-hoc `BodyWorld` or `Renderer.bounds`
reads outside the resolver (guard-enforced).

### 3.3 Built / wiring / gap — injector

| Capability | Built | Wiring | Gap |
|------------|-------|--------|-----|
| UnitFrame anchors | Yes | VfxDirector, ShieldBarPool | ActorHudPool not started |
| Shield resource bar | Yes | ShieldBarPool only | Not in fold model; no element on bar |
| Status sustain VFX | Yes | VfxDirector | Complementary to HUD strip — by design |
| Progression level band | Resolved in Core | DerivedStatChannels | Not displayed on lawn |
| Specimen binding | Yes | MatchUniqueBindingsFacet | Web fold only when selected |
| `Occupant.hud` snapshot | **No** | — | **Real gap** — builder + dump + pool |

---

## 4. Data / RPG layer perspective

**Rule (CLAUDE.md / DESIGN-GATE):** RPG features live in the RPG layer. Dodge, shield, level, and
status magnitudes are overlay channels — HUD reads **injector snapshot**, not PvZ plant/zombie fields.

| Signal | Authoritative store | Lawn display today | HUD slot |
|--------|---------------------|-------------------|----------|
| Shield stacks | `ShieldRuntime` | Unity bar | Resource row |
| Status instances | `StatusRuntime` | VFX + partial web chips | Status strip |
| Specimen binding | `MatchUniqueBindingsFacet` | Inspector `instanceId` | Identity role pip |
| PvZ unique plant | `plant.unique` event | Web `flags.unique` | Identity pip |
| Progression power | `progression.power` | Nowhere on lawn | Level badge band |
| Commander leader | `MatchCommanderSnapshot` | Band A chip only | **Not per-unit** |
| Boss tier | Expedition/battle | **Inert on lawn** | Tier frame TBD |

**Wiring vs real gap:**

| Finding | Classification |
|---------|----------------|
| Level band resolved but not drawn | **Wiring gap** |
| Boss flag not on lawn | **Real gap** — needs expedition signal (ideal non-goal until defined) |
| 9/13+ status ids in web fold | **Wiring gap** — extend chip set + icon strip |
| No `Occupant.hud` builder | **Real gap** — new module |

**ptr vs instanceId:** Fold keys occupants by normalized ptr; binding attaches `instanceId` when
known. HUD builder must tolerate stale/unbound ptr (hide role pip, not crash).

---

## 5. Cross-program boundaries

| Program | Relationship |
|---------|--------------|
| **Status VFX v3** | Motion on body/feet/crown; HUD strip = static semantic icons — **both ship** |
| **commander-surface** | Band A match chrome only; no per-tile commander badge |
| **actor-sheet** | Full stats on ActorPanel; HUD never replaces six-tab sheet |
| **shield-system-spec** | Runtime unchanged; world bar **migrates** into HUD shield slot |
| **element-hub-ssot** | Shield segment colors; element ring on icons |
| **status-ssot** | Closed `statusId` vocabulary for strip tokens |

---

## 6. Debates closed — do not reopen (recommended for ideal §0)

| Debate | Decision | Rationale |
|--------|----------|-----------|
| HUD replaces status VFX? | **No** | User read: icon = meaning, VFX = motion |
| Numeric ATK/DEF on lawn v1? | **No** | GG-60; ActorPanel for numbers |
| Dual-render Unity + Phaser | **Gate v1** | Owner chose both; drift today is the bug |
| HP sliver on lawn v1? | **Default off** | Reduce clutter; unify under `Occupant.hud` first |
| Per-unit commander mark? | **No** | Match snapshot Band A only |
| ShieldBarPool vs ActorHudPool | **Subsumption** | Shield becomes one resource slot; retire standalone pool |
| Inspector as primary readout? | **No** | Token/chip on canvas; Inspector expands same fold |

---

## 7. Incidents this audit prevents

| Failure mode | Without this pass |
|--------------|-------------------|
| "Lawn can't show dodge/shield because no Unity field" | Wrong frame — RPG layer has channels; this is display wiring |
| Phaser ships without Unity parity | Two products; spectator lies |
| Status icons duplicate VFX 1:1 | Visual noise; motion grammar wasted |
| Shield bar grows more rows ad hoc | Second ShieldBarPool; perf coupling repeats |
| Inspector KeyValue becomes "the HUD" | GG-25 schema viewer on the run stage |

---

## 8. Program acceptance (draft)

> *During a live lawn with elites, shields, and custom statuses, a player on Unity and a spectator on
> Phaser can identify boss tier, shield element, and top-priority statuses on a unit **without opening
> Inspector**; web Inspector shows the same `Occupant.hud` fold fields, not a divergent text layout.
> Toggling shield display hides the resource row only — sustain VFX remain.*

**Future automation:** Playwright lawn snapshot assert + optional LIVE probe (named in capability map,
not built in this audit).

---

## 9. Open items — owner input still required

| Item | Audit recommendation | Unblocks |
|------|---------------------|----------|
| Boss tier signal on lawn | Reserve tier frame; defer until expedition spawn exposes flag | Expedition/battle module + ideal non-goal lift |
| Level badge source v1 | Display band from `progression.power` Θ, not raw `theLevel` | `actor-hud-core` band enum |
| Status icon art SSOT | Almanac tokens + `StatusVfxIdentity` color; shape from catalog | `actor-hud-phaser` + unity sprite sheet |
| Max strip width (world units) | Tunable in `actor-hud.v1.json` | Map module spec |
| Event-driven vs poll | Prefer invalidate on shield/status/binding events | Perf probe before/after |
| Status VFX batch-6 human trials | Separate vfx stream — does not block HUD ideal approval | Owner eyeball + forced-choice JSON |

---

## 10. Verification log

| Check | Result |
|-------|--------|
| `Occupant.hud` / `ActorHud*` in repo | **Absent** (grep 2026-08-30) |
| ShieldBarPool only Unity RPG shield UI | **Confirmed** — `VfxDirector.Tick` → `TickSync` |
| Phaser RPG shield | **Absent** — `setHpDisplay` uses vanilla hp only |
| `OBSERVE_CHIPS` count | **9** ids (`lawnProjectorFold.ts`) vs 13 custom status VFX ids |
| UnitFrame in ShieldBarPool | **Yes** — `UnitFrameResolver.Resolve` at line 202 |

---

## Related documents

| Doc | Role |
|-----|------|
| [actor-hud-ideal.md](../architecture/actor-hud-ideal.md) | Architecture ideal — strengthened from this audit |
| [10-actor-hud.html](../design/10-actor-hud.html) | Visual guide — §H player scenarios |
| [status-identity-audit-2026-08-30.md](vfx/status-identity-audit-2026-08-30.md) | User-perspective pattern for VFX |
| [commander-fe-audit-2026-08-30.md](commander-fe-audit-2026-08-30.md) | Multi-lens audit pattern |
