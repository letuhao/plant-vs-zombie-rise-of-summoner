# Actor HUD — the ideal

**Status:** Proposed — strengthened 2026-08-30. **Not a spec. No build authorized.**

**Strengthen pass:** multi-perspective audit in
[actor-hud-audit-2026-08-30.md](../research/actor-hud-audit-2026-08-30.md) (user perspective first,
built/wiring/gap verified against code). Data pipeline SSOT:
[actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md).

Visual guide: [`10-actor-hud.html`](../design/10-actor-hud.html) (§H player scenarios) · Match chrome
(separate band): [`commander-surface/spec-lawn-hud-chip.md`](commander-surface/spec-lawn-hud-chip.md) ·
Anchor SSOT: [`vfx/spec-unit-frame.md`](vfx/spec-unit-frame.md)

**Map and plan:** [actor-hud-map.md](actor-hud-map.md) · [actor-hud-plan.md](../tasks/actor-hud-plan.md) ·
Module specs: [actor-hud/](actor-hud/)

**Read before proposing against this:** [DESIGN-GATE.md](../DESIGN-GATE.md) §1 rows for player GUI,
stats, status, shield, VFX. Verify against code, not plate comments alone.

---

## 0. Present — decisions and handoffs

### 0.1 Decisions — do not reopen

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

Source: [actor-hud-audit-2026-08-30.md §6–§7](../research/actor-hud-audit-2026-08-30.md).

### 0.3 Pipeline decisions — do not reopen

| Decision | Rationale |
|----------|-----------|
| Hot-only HUD read during match | No Cold/SQL/REST on display path — [overlay-control-loops.md](overlay-control-loops.md) |
| Derived pinned at `EntityApply` | `levelBand` from pinned `progression.power` — not `theLevel`, not per-frame SQL |
| Single builder entry | `ActorHudBuilder.Build(ptr)` gathers; renderers read model/cache only |

Source: [actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md).

### 0.2 Handoff — status VFX (separate program)

The UnitFrame + status-identity stream is **done for its scoped deliverables**, with normal follow-ups:

| Area | State |
|------|--------|
| UnitFrame anchor/span | Shipped — `UnitFrameResolver`, `spec-unit-frame.md` |
| 13 custom status sustain VFX | Shipped; LIVE audit passes with `/lawn/quick-start` |
| Marker placement + glow textures | Fixed 2026-08-30 (Crown lift decoupled from body/feet Y bump) |
| Owner lawn eyeball | Pending deploy when game is closed (Mods DLL lock) |
| Batch-6 forced-choice human trials | Open — `tasks/vfx-identity-batch6-live-plan.md` |

**Actor HUD is a new program.** It consumes UnitFrame for placement; it does not replace status
motion grammar (VFX) or match-level commander chips.

---

## 1. Problem — why the shield bar is the wrong seed

Today the player reads unit state through **three partial HUDs** with no shared model:

| Surface | What it shows | Path |
|---------|---------------|------|
| Unity world | Aggregate RPG shield bar only | `ShieldBarPool` under `VfxDirector.Tick` |
| Phaser lawn canvas | Vanilla HP bar only — no RPG shield | `SyncFromModelSystem.setHpDisplay` |
| Web Inspector | `Shield: N/M` text + partial status chips | `lawnProjectorFold` → `LawnPage` selection |

All three read from overlapping but incompatible sources. `ShieldRuntime` is authoritative for
stacks; `entity.stats` fold carries aggregate `rpgShieldHp`/`rpgShieldMax`; the Unity bar polls
`ShieldRuntime.VisitOwners` every frame with its own MeshRenderer pool (~7 renderers per owner).

**Why it feels like waste:**

1. **Single-purpose pool** — shield-only; duplicate anchor logic (`worldYOffset` vs UnitFrame lift).
2. **Low information density** — 10% stepped fill + three stack pips; no boss/demon/level/commander read.
3. **Perf coupling** — `VfxDirector.Tick` stays hot whenever any shield exists, even with F9 off.
4. **No web/Phaser parity** — fold keys exist; Phaser never draws RPG shield; status chips cover 9/13+ custom ids.

Replacing the bar with a **generic Actor HUD** (shield = one resource slot) is the right direction.

---

## 2. Industry patterns — one channel, one meaning

Games that communicate unit identity without stat walls reuse a small grammar. Each visual channel
carries **one semantic dimension**; redundancy is reserved for accessibility, not for repeating the
same fact three ways.

| Pattern | Examples | Channel | Meaning |
|---------|----------|---------|---------|
| Tier frame on vitals | TFT star level integrated into health bar; elite/boss colored frames | **Shape / frame** | Power tier, rarity, or threat class |
| Trait / buff icon strip | TFT trait icons; MOBA buff pips above portrait | **Icon** | Active effect identity |
| Color tier without numbers | TFT bronze → silver → gold → iridescent trait tiers | **Gradient / hue band** | Stacked magnitude or tier depth |
| Level badge | ARPG corner numeral on portrait | **Badge** | Level (compact; no sentence) |
| Silhouette + motion | Enemy design — threat read before stats | **Motion grammar** | Complements HUD (our status VFX) |
| Layered vitals stack | MOBA hero panel: portrait, liquid-fill bars, buff row | **Vertical band** | Identity top, resources middle, statuses bottom |

**Anti-patterns for Rise of Summoner:**

- Duplicate meaning (shield color + shield text + shield bar all saying the same thing).
- Engine vocabulary on the lawn (`typeId`, raw `instanceId` hex, `Intent`).
- Navigating away from the board to read unit state (violates GG-1).
- Raw derived magnitudes on the run stage (GG-60 — bands and icons only; full numbers in ActorPanel).

References: Coherent MOBA UI sample (hero panel bars + buff row); TFT UI (trait icons, star-on-bar,
scoreboard health); ARPG HUD patterns (level badge + gradient vitals).

---

## 3. User perspective — questions under time pressure

**Anchor:** GG-60 — legibility wins on the lawn; fiction and full numbers live in ActorPanel and
almanac. Detail, scenarios, and accessibility checks:
[actor-hud-audit-2026-08-30.md §1](../research/actor-hud-audit-2026-08-30.md). Visual scenarios:
[10-actor-hud.html §H](../design/10-actor-hud.html#h).

| Player question | Ideal slot | Today |
|-----------------|------------|-------|
| Boss or elite? | Tier frame + level band | No frame on unit |
| My demon / bound specimen? | Role badge | Inspector only when selected |
| Why isn't damage landing? | Shield element segments | Fill only; no element on bar |
| What's afflicting this unit? | Status strip icons | VFX motion; text chips in Inspector |
| Which unit is the threat? | Identity row (type icon) | Inspector / HP bar only on canvas |
| How hurt is it? | HP sliver (v1 off) | Phaser vanilla HP; Unity none |

**Scenario clusters (predicted user read):**

1. **Horde** — overflow `+N`; never shrink below readable token size.
2. **Elite + dual status** — icons for meaning; VFX for motion — no duplicate fact.
3. **Bound demon** — role pip visible without opening Inspector.
4. **Phaser spectator** — same `Occupant.hud` semantics as Unity world HUD.

---

## 4. Two bands — do not merge

### Band A — Match chrome (existing program)

**Scope:** Sun, wave, timer, commander + aura chips, deployed specimens, transport.

**Owner:** commander-surface program — [`spec-lawn-hud-chip.md`](commander-surface/spec-lawn-hud-chip.md),
[`LawnHud.tsx`](../../web/fusion-rpg-web/src/features/lawn/LawnHud.tsx).

**Rule:** Per-unit Actor HUD **does not replace** Band A. Commander identity is match-level, frozen at
`board.start`, not repeated on every zombie tile.

### Band B — Per-unit world HUD (this program)

**Scope:** Compact readout **above each unit** (plant or zombie) while it is on the lawn.

**Anchor:** [`UnitFrameResolver`](../../src/FusionRpg.Injector/Fx/UnitFrameResolver.cs) — same SSOT as
status VFX. HUD must not call `BodyWorld` or read `Renderer.bounds` outside the resolver (guard-enforced).

**Layout (three rows, priority-capped):**

```text
        [Identity row — glance read]
  tier frame | role icon | level badge | unique/demon pip
        [Resource row — optional slots]
  shield segments (element-colored) | HP sliver (optional) | meter ticks
        [Status strip — icons only, max N]
  status tokens | CC corner glyph | +N overflow pip
```

**Slot rules:**

- Each slot type maps to **one** semantic dimension (documented in plate §B legend).
- **Priority when crowded:** CC > commander-mark > unique/demon > shield > top statuses > level.
- **Overflow:** collapse to `+N` pip — never shrink text to illegibility.
- **Tunables:** slot caps, Y offsets, tier thresholds in `data/tuning/actor-hud.v1.json` (future).
- **Presentation-only:** HUD never writes gameplay state (same boundary as VFX).

---

## 5. Slot catalog

### 5.1 Identity row

| Slot | Player read | Data signal | Source today | Lawn wired? |
|------|-------------|-------------|--------------|-------------|
| **Tier frame** | Normal / elite / boss / unique threat | Rarity, expedition tier, unique flag | Demon rarity; `flags.unique`; boss TBD | Partial (`unique` on web) |
| **Role icon** | Commander-led, demon specimen, vanilla | `instanceId` binding, demon profile | `debug.snapshot` bindings; roster API | Partial |
| **Level badge** | Power band at a glance | Θ band from `progression.power` | `DerivedStatChannels`; `EntityApply` | Resolved, not displayed |
| **Unique pip** | This unit is special | PvZ unique plant; bound specimen | `plant.unique`; bindings | Web fold partial |

### 5.2 Resource row

| Slot | Player read | Data signal | Source today | Lawn wired? |
|------|-------------|-------------|--------------|-------------|
| **Shield segments** | How much shield, which element | Per-stack `Hp`, `MaxHp`, `Element` | `ShieldRuntime` | Unity bar only |
| **HP sliver** | Optional vanilla/RPG HP hint | `entity.stats` hp/max | `GameDumps` | Phaser HP bar (no RPG overlay) |
| **Meter ticks** | Bond / command / channel meters | StatusRuntime meters | `StatusRuntime` | Debug dump only |

Shield slot **subsumes** today's `ShieldBarPool` — same element-colored segment grammar, inside the
generic row instead of a bespoke pool.

### 5.3 Status strip

| Slot | Player read | Data signal | Source today | Lawn wired? |
|------|-------------|-------------|--------------|-------------|
| **Status token** | Which affliction/buff | `statusId` | `StatusRuntime`; `debug.status.*` | VFX + partial web chips |
| **CC glyph** | Hard control active | CC flag on instance | `StatusRuntime` | Event fold partial |
| **Magnitude band** | Strong vs weak (not raw number) | Magnitude → band enum | Core resolve | Not on lawn |

**HUD icons ≠ sustain VFX.** Static readable glyphs at token size (~16–24px). Motion grammar stays in
`VfxDirector` (Orbit, WispOut, etc.). Icon shape comes from `StatusVfxIdentity` / almanac tokens;
color from element or status RGB.

Extend web [`OBSERVE_CHIPS`](../../web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.ts) to all
13 custom ids in `StatusVfxIdentity.CustomIds`.

---

## 6. Dual-render SSOT — one model, two presenters

Owner chose **both** Unity world HUD and Phaser/web mirror from the **same observe snapshot**.

```text
Injector (event-invalidated)
  EntityApply → InjectorDerivedOverride.Pin(ptr, derived)
  ActorHudBuilder
    ← ShieldRuntime, StatusRuntime, bindings, pinned progression.power
  → GameDumps / debug.actor-hud (per ptr)
  → ActorHudPool (Unity sprites/meshes at UnitFrame anchors)

Web lawn
  lawnProjectorFold → Occupant.hud
    → SyncFromModelSystem (Phaser chip sprites on canvas)
    → LawnPage Inspector (expanded readout on selection)
```

**Contract sketch** (ideal only — not implemented):

```typescript
// Future: Occupant.hud / ActorHudSnapshot
hud: {
  identity: {
    tier?: "normal" | "elite" | "boss" | "unique";
    role?: "specimen" | "commander-mark" | "vanilla";
    levelBand?: number;       // display band, not raw Θ
    flags: string[];          // e.g. "unique", "demon"
  };
  resources: {
    shield?: {
      hp: number;
      max: number;
      stacks: { element: string; hp: number; max: number }[];
    };
    hpSliver?: { ratio: number };  // optional — v1 default off
    meters?: { id: string; ratio: number }[];
  };
  statuses: {
    id: string;
    cc: boolean;
    magnitudeBand: "low" | "mid" | "high";
  }[];
  overflow: { statusCount: number };  // for +N pip
}
```

**Migration path:**

1. Introduce `ActorHudBuilder` + dump keys (read-only).
2. `ActorHudPool` renders all rows; shield slot uses existing `ShieldBarColor` / `ShieldBarVisual` math.
3. Retire direct `ShieldBarPool.TickSync` from `VfxDirector`; gate unified `ActorHudDirector`.
4. Phaser `SyncFromModelSystem` reads `Occupant.hud` — same geometry as Unity (band-relative offsets).
5. Deprecate shield-only section in `shield-system-spec.md` §2.6 when superseded.

---

## 7. Data inventory — wired vs inert

Full SSOT table, Hot pipeline, duplicate retirement, and feature gate:
[actor-hud-data-pipeline-audit-2026-08-30.md](../research/actor-hud-data-pipeline-audit-2026-08-30.md).

| Signal | Authoritative store | Lawn display today | Actor HUD slot |
|--------|---------------------|-------------------|----------------|
| Shield stacks | `ShieldRuntime` | Unity bar | Resource row |
| Status instances | `StatusRuntime` | VFX + 9 web chips | Status strip |
| Specimen binding | `MatchUniqueBindingsFacet` | Web `instanceId` when bound | Identity row |
| PvZ unique plant | `plant.unique` event | Web `flags.unique` | Identity pip |
| Demon profile | Server roster / `DemonDtos` | ActorPanel only | Identity (when bound) |
| Progression power | `progression.power` channel (pinned @ EntityApply) | Nowhere on lawn | Level badge band |
| Commander leader | `MatchCommanderSnapshot` | Match HUD chip only | Not per-unit (Band A) |
| Boss flag | Expedition/battle | **Inert on lawn** | Tier frame TBD |
| Bond/command meters | `StatusRuntime` meters | Debug dump | Meter ticks |
| Full derived sheet | `/api/actors/{id}/derived` | ActorPanel layer | **Not on lawn** (GG-1) |

**Key gap:** Rich RPG data resolves every apply; **display** is fragmented. Actor HUD closes the lawn
glance layer without duplicating ActorPanel.

---

## 8. Today vs ideal — audit-verified built / wiring / gap

Verified 2026-08-30 — full tables in
[actor-hud-audit-2026-08-30.md](../research/actor-hud-audit-2026-08-30.md).

### Injector

| Capability | Built | Wiring | Gap |
|------------|-------|--------|-----|
| UnitFrame anchors | Yes | VfxDirector, ShieldBarPool | ActorHudPool not started |
| Shield resource bar | Yes | ShieldBarPool only | Not in fold; no element on bar |
| Status sustain VFX | Yes | VfxDirector | Complementary — by design |
| `Occupant.hud` snapshot | No | — | Builder + dump + pool |

### Web + Phaser

| Capability | Built | Wiring | Gap |
|------------|-------|--------|-----|
| Occupant fold | Yes | `lawnProjectorFold` | No `hud` sub-object |
| RPG shield in fold | Yes | `rpgShield*` on occupant | Phaser does not draw |
| Status chips | Partial | 9 `OBSERVE_CHIPS` ids | Extend to 13; icon strip |
| Phaser HP bar | Yes | `setHpDisplay` | No Band B rows |

**Grep confirmation:** no `Occupant.hud`, `ActorHud`, `ActorHudPool`, or `ActorHudBuilder` in the
repo at audit time.

---

## 9. Relationship to adjacent programs

| Program | Relationship |
|---------|--------------|
| **commander-surface** | Band A match chrome; no per-tile commander picker |
| **actor-sheet** | Band-2 layer — full numbers, tabs, actions; opened over stage (GG-1) |
| **vfx / UnitFrame** | Shared anchor; VFX = motion; HUD = static tokens |
| **status-ssot** | Closed `statusId` vocabulary for strip icons |
| **shield-system-spec** | Runtime stays; world bar presentation migrates to HUD shield slot |
| **element-hub-ssot** | Shield segment colors; element ring for icons |

---

## 10. Non-goals (this program)

- **Boss on lawn** — no boss flag in injector today; tier frame reserved, semantics TBD with expeditions.
- **Zomboss in player commander list** — commander-surface scope.
- **Full derived channel sheet on unit** — ActorPanel only.
- **Replacing damage floaters** — combat feedback stays in VfxDirector floaters/bursts.
- **Mid-run commander picker on unit** — violates commander-surface decisions.
- **Magic numbers in code** — caps and offsets in `actor-hud.v1.json` when built.

---

## 11. Governance — DESIGN-GATE reads before build

| If touching… | Read |
|--------------|------|
| Player-visible lawn UI | `game-gui-principles.md` — GG-1, GG-60 |
| Stage vs layer | `information-architecture.md` §2.3 Lawn |
| Channel display mapping | `stat-system.md`, `spec-derived-stat-sheet.md` — no third classification |
| Status ids | `status-ssot.md` |
| Shield runtime | `shield-system-spec.md` |
| World anchors | `vfx/spec-unit-frame.md` |
| Web fold | `web/spec.md`, `lawnProjectorFold.ts` patterns |

**Hard boundaries:** presentation-only; magnitudes as bands on lawn; overflow throws in Core paths
unchanged; SQL stays in `FusionRpg.Data`.

---

## 12. Future modules (teaser — no map yet)

After ideal approval, expect module ids along:

| Module id | Job |
|-----------|-----|
| `actor-hud-core` | Pure layout, slot catalog, priority, band enum math |
| `actor-hud-dump` | Injector snapshot builder + `debug.actor-hud` event |
| `actor-hud-unity` | `ActorHudPool` world renderers |
| `actor-hud-phaser` | Canvas chip sync from model |
| `actor-hud-fold` | Web projector + Inspector expansion |
| `shield-slot-migration` | Retire `ShieldBarPool` as standalone |

---

## 13. Open questions — owner review

Each item includes audit recommendation; §0 decisions stand unless owner overrides.

| # | Question | Recommendation | Unblocks |
|---|----------|----------------|----------|
| 1 | **Boss tier on lawn** — which signal defines `tier: boss`? | Reserve frame; defer until expedition spawn exposes flag | Expedition module + non-goal lift |
| 2 | **HP sliver** — show on lawn at all? | **Default off v1** (§0) | Owner yes/no on sliver |
| 3 | **Level badge source v1** | Band from `progression.power` Θ, not raw `theLevel` | `actor-hud-core` band enum |
| 4 | **Event-driven vs poll** | Invalidate on shield/status/binding events | Perf probe before/after build |
| 5 | **Phaser parity priority** | **Gate v1 on both renders** (§0) — not Unity-only ship | Dual-render module order |
| 6 | **Status icon art SSOT** | Almanac tokens + `StatusVfxIdentity` color | Map spec for sprite sheet |

---

## 14. Program acceptance (not yet authorized)

> *During a live lawn with elites, shields, and custom statuses, a player on Unity and a spectator on
> Phaser can identify boss tier, shield element, and top-priority statuses on a unit **without opening
> Inspector**; web Inspector shows the same `Occupant.hud` fold fields, not a divergent text layout.
> Toggling shield display hides the resource row only — sustain VFX remain.*

Until Playwright + LIVE probes assert that end-to-end, the program is not done.

**Next artifacts after owner approval:** capability map and module specs are drafted — see
[actor-hud-map.md](actor-hud-map.md) and [actor-hud-plan.md](../tasks/actor-hud-plan.md). Build
starts after P0 review. Visual acceptance reference: [10-actor-hud.html §H](../design/10-actor-hud.html#h).

---

## DESIGN-GATE §5 checklist (this ideal)

| Box | Status |
|-----|--------|
| Subsystem located in DESIGN-GATE §1 index | Player GUI, stats, status, shield, VFX rows read |
| Proposals verified against code | Audit §10 verification log; grep confirms no HUD builder |
| Section context respected | Band A rules under commander-surface; Band B under this ideal |
| Constraints tested before claimed | No golden/test claims — doc-only strengthen pass |
| GG-60 / GG-1 / GG-23 applied | User perspective §3; no numeric wall on lawn |
| Cross-program boundaries stated | §9 + audit §5 |
