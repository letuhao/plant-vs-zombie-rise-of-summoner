# Actor HUD — implementation plan

**Program:** `actor-hud` · **Map:** [docs/architecture/actor-hud-map.md](../docs/architecture/actor-hud-map.md) ·
**Ideal:** [docs/architecture/actor-hud-ideal.md](../docs/architecture/actor-hud-ideal.md) ·
**Audit:** [docs/research/actor-hud-audit-2026-08-30.md](../docs/research/actor-hud-audit-2026-08-30.md) ·
[docs/research/actor-hud-data-pipeline-audit-2026-08-30.md](../docs/research/actor-hud-data-pipeline-audit-2026-08-30.md) ·
**Tasks:** [actor-hud-todo.md](actor-hud-todo.md)

**Status:** implemented 2026-08-31 — all modules shipped; CI + guard acceptance green.
**Placement SSOT (2026-09-05):** Unity Body + `worldYOffset` (center-bottom). Visual correction code
landed (glyphs, pips, Body root). LIVE eyeball remains — see [actor-hud-todo.md](actor-hud-todo.md).

---

## Goal

Ship Band B per-unit Actor HUD: one `Occupant.hud` snapshot from the injector, rendered in Unity world,
mirrored on Phaser canvas, expanded in web Inspector — shield slot subsumes standalone `ShieldBarPool`,
status strip complements (does not replace) sustain VFX.

**Dual-render is a v1 gate.** Unity-only ship does not satisfy program acceptance.

---

## Program acceptance (end-to-end)

During a live lawn with elites, shields, and custom statuses, a player on Unity and a spectator on Phaser
can identify shield element and top-priority statuses on a unit **without opening Inspector**; web
Inspector shows the same `Occupant.hud` fold fields, not a divergent text layout. Toggling shield display
hides the resource row only — sustain VFX remain.

Until **Playwright** `web/fusion-rpg-web/e2e/actor-hud.spec.ts` asserts fold + Phaser semantics, the
program is **not done**. **(Met 2026-08-31** — 6 mocked scenarios green; live harness shipped.)

---

## Module specs

| Module | Spec |
|--------|------|
| `actor-hud-core` | [spec-actor-hud-core.md](../docs/architecture/actor-hud/spec-actor-hud-core.md) |
| `actor-hud-dump` | [spec-actor-hud-dump.md](../docs/architecture/actor-hud/spec-actor-hud-dump.md) |
| `actor-hud-fold` | [spec-actor-hud-fold.md](../docs/architecture/actor-hud/spec-actor-hud-fold.md) |
| `actor-hud-unity` | [spec-actor-hud-unity.md](../docs/architecture/actor-hud/spec-actor-hud-unity.md) |
| `actor-hud-phaser` | [spec-actor-hud-phaser.md](../docs/architecture/actor-hud/spec-actor-hud-phaser.md) |
| `shield-slot-migration` | [spec-shield-slot-migration.md](../docs/architecture/actor-hud/spec-shield-slot-migration.md) |

---

## Phases

### P0 — Owner review (gate)

- Review [actor-hud-map.md](../docs/architecture/actor-hud-map.md)
- Review all six module specs in [actor-hud/](../docs/architecture/actor-hud/)
- Review [actor-hud-data-pipeline-audit-2026-08-30.md](../docs/research/actor-hud-data-pipeline-audit-2026-08-30.md) — SSOT table, forbidden reads, duplicate retirement
- Cross-program sign-off: commander-surface (Band A boundary), vfx/UnitFrame, shield-system-spec
- Confirm v1 resolutions: boss tier omitted, HP sliver off, Phaser required, event invalidation, EntityApply pin for `levelBand`

**Exit:** owner approves map + specs + pipeline audit; todo "spec approved" checkboxes ticked.

---

### P1 — Core + tunables

**Module:** `actor-hud-core`

| Step | Work |
|------|------|
| P1.1 | DTO types in `FusionRpg.Core/Hud/` |
| P1.2 | `ActorHudLayout.Prioritize`, overflow, `PowerBandDisplay` |
| P1.3 | `data/tuning/actor-hud.v1.json` + tuning hub load |
| P1.4 | `ActorHudLayoutTests` green |
| P1.5 | `audit-magic-numbers.py` clean on new Core files |

**Exit:** `dotnet test tests\FusionRpg.Core.Tests --filter ActorHud` green.

---

### P1.5 — EntityApply derived pin

**Prerequisite for `identity.levelBand` in dump module.** Part of dump wiring; no separate module id.

| Step | Work |
|------|------|
| P1.5.1 | After `ActorHub.Resolve` in `EntityApply.RunPlant` / `RunZombie` → `InjectorDerivedOverride.Pin(ptr, resolved.Derived)` |
| P1.5.2 | Clear pin on match end via existing `InjectorDerivedOverride.Clear()` hook |
| P1.5.3 | Update `InjectorDerivedOverride` doc comment — production Hot cache, not cheat-only |
| P1.5.4 | Unit test: pin survives until die/end; builder reads `progression.power` → `levelBand` |

**Exit:** Pinned derived available to builder; no `theLevel` / SQL / REST on level path.

---

### P2 — Injector dump + observe

**Module:** `actor-hud-dump` · **Requires P1.5 pin for levelBand**

| Step | Work |
|------|------|
| P2.1 | `ActorHudBuilder` + cache + invalidation — **read surface contract only** |
| P2.2 | Wire `actorHud` on `GameDumps` plant/zombie + `BoardEntityStats` |
| P2.3 | Optional `debug.actor-hud` delta emit |
| P2.4 | Golden JSON tests + invalidation unit tests |
| P2.5 | Static review / test doubles — no banned field access in builder |

**Exit:** Observe payload includes `actorHud` on lab entity; goldens pass; read-surface compliance verified.

---

### P3 — Web fold + Unity pool (parallel)

**Modules:** `actor-hud-fold`, `actor-hud-unity`

| Step | Work | Owner |
|------|------|-------|
| P3a.1 | `Occupant.hud` types + `foldActorHud` | fold |
| P3a.2 | Extend OBSERVE_CHIPS to 13 custom ids | fold |
| P3a.3 | `ActorHudInspector` + LawnPage bind | fold |
| P3a.4 | `lawnProjectorFold.test.ts` | fold |
| P3b.1 | `ActorHudPool` three-row render | unity |
| P3b.2 | UnitFrame guard test | unity |
| P3b.3 | LIVE lab board eyeball (`/lawn/quick-start`) | unity |

**Exit:** Fold tests green; Unity shows shield + statuses on LIVE board; Inspector matches fold.

**Contract freeze:** P3 starts only after P2 observe shape is stable (map DTO table).

---

### P4 — Phaser parity

**Module:** `actor-hud-phaser`

| Step | Work |
|------|------|
| P4.1 | `ActorHudDisplay` + `setHudDisplay` in SyncFromModelSystem |
| P4.2 | Unit test with fixture occupant |
| P4.3 | Visual check on browser lawn canvas |

**Exit:** Phaser canvas shows same fixture semantics as fold unit test.

---

### P5 — Shield migration

**Module:** `shield-slot-migration`

| Step | Work |
|------|------|
| P5.1 | Confirm ActorHudPool shield row parity with ShieldBarPool |
| P5.2 | Remove `ShieldBarPool.TickSync` from VfxDirector |
| P5.3 | Deprecate/delete ShieldBarPool; update shield spec §2.6 |
| P5.4 | Optional perf probe B2 before/after note |

**Exit:** Single shield bar per unit; sustain VFX unchanged; no TickSync in VfxDirector.

**Ordering hazard:** P5 is **last** — never migrate before P3b LIVE eyeball passes.

---

### P6 — Program E2E

| Step | Work |
|------|------|
| P6.1 | Author `e2e/actor-hud.spec.ts` (mocked CI path) |
| P6.2 | `e2e/actor-hud-live.spec.ts` — lab quick-start + shield/demo + status/apply |
| P6.3 | Assert Phaser HUD hooks + Inspector `data-testid` agree |
| P6.4 | Document Unity LIVE assertion as manual supplement |

**Live spec requires vite dev (`5173`)** so REST/SignalR hit `http://127.0.0.1:5088` (`import.meta.env.DEV`). CI preview (`4173`) runs mocked spec only. Owner gate: `.\scripts\prove-actor-hud-live.ps1` with `ACTOR_HUD_LIVE_E2E=1`.

**E2E sketch:**

1. Navigate to `#/lawn`; ensure lab board via quick-start or cheat API
2. Apply shield + `expose` + `spark` (or equivalent) to target ptr
3. Select unit on canvas
4. Expect Inspector tier/shield/status fields from `Occupant.hud`
5. Expect Phaser container named children (`hudShield`, status tokens)

**Exit:** Playwright green; program acceptance satisfied for web path; Unity manual tick on todo.

---

## Verification (full program)

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter ActorHud
dotnet test tests\FusionRpg.Guard.Tests
cd web\fusion-rpg-web
npm run test -- lawnProjectorFold
npm run test -- SyncFromModelSystem
npx playwright test e2e/actor-hud.spec.ts
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-single-writer.ps1
python scripts\audit-magic-numbers.py --summary
```

LIVE (owner terminal):

```powershell
.\scripts\deploy-play.ps1 -NoServer
# /lawn/quick-start — eyeball Unity HUD vs plate 10 §H
```

---

## Cross-program dependencies

| Program | Need |
|---------|------|
| vfx / UnitFrame | Shipped — consume resolver |
| commander-surface | Band A only — no conflict |
| shield-system-spec | Runtime stable — presentation migrates |
| status-ssot | Closed ids for strip |
| actor-sheet | No overlap — panel for full stats |

---

## Risks

| Risk | Mitigation |
|------|------------|
| Double shield bar during P3–P5 overlap | Migration module last; flag until cutover |
| Perf regression from two tick paths | Event invalidation + remove TickSync in P5 |
| Phaser/Unity drift | Single fold SSOT; E2E on Phaser; Unity manual |
| Boss tier scope creep | v1 omit `boss`; tier frame for unique/elite only |

---

## Suggested commit message (for owner)

```
docs(actor-hud): strengthen specs with data SSOT and Hot pipeline

Add pipeline audit, EntityApply derived pin decision, master SSOT table,
forbidden duplicate paths, and map/spec/plan updates for reliable FSM-aligned reads.
```
