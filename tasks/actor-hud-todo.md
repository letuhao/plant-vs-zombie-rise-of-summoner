# Actor HUD — task list

**Program:** `actor-hud` · **Plan:** [actor-hud-plan.md](actor-hud-plan.md) ·
**Map:** [docs/architecture/actor-hud-map.md](../docs/architecture/actor-hud-map.md)

**Program status (2026-08-31):** **Implementation complete.** All modules shipped; specs signed off below.
**Remaining gate:** LIVE manual only — Unity eyeball + `prove-actor-hud-live.ps1` (optional polish, not code work).

---

## P0 — Review gate

- [x] Owner review: [actor-hud-map.md](../docs/architecture/actor-hud-map.md) — signed off with shipped implementation
- [x] Owner review: [actor-hud-data-pipeline-audit-2026-08-30.md](../docs/research/actor-hud-data-pipeline-audit-2026-08-30.md) — SSOT table, pipeline, forbidden reads
- [x] Owner review: [spec-actor-hud-core.md](../docs/architecture/actor-hud/spec-actor-hud-core.md)
- [x] Owner review: [spec-actor-hud-dump.md](../docs/architecture/actor-hud/spec-actor-hud-dump.md)
- [x] Owner review: [spec-actor-hud-fold.md](../docs/architecture/actor-hud/spec-actor-hud-fold.md)
- [x] Owner review: [spec-actor-hud-unity.md](../docs/architecture/actor-hud/spec-actor-hud-unity.md)
- [x] Owner review: [spec-actor-hud-phaser.md](../docs/architecture/actor-hud/spec-actor-hud-phaser.md)
- [x] Owner review: [spec-shield-slot-migration.md](../docs/architecture/actor-hud/spec-shield-slot-migration.md)
- [x] Cross-program sign-off: commander-surface (Band A), vfx/UnitFrame, shield-system-spec
- [x] Confirm v1: boss tier omitted, HP sliver off, Phaser required for done
- [x] Confirm pipeline: Hot-only HUD read; EntityApply pin for `levelBand`; no SQL/REST mid-match

---

## P1.5 — EntityApply derived pin

Spec: [spec-actor-hud-dump.md](../docs/architecture/actor-hud/spec-actor-hud-dump.md) § EntityApply derived pin

- [x] `InjectorDerivedOverride.Pin` in `EntityApply.RunPlant` after `ActorHub.Resolve`
- [x] `InjectorDerivedOverride.Pin` in `EntityApply.RunZombie` after `ActorHub.Resolve`
- [x] Clear on match end (existing `InjectorDerivedOverride.Clear()` hook)
- [x] Doc comment updated — production cache, not cheat-only
- [x] Unit test: pin survives until die/end
- [x] Acceptance: builder can read `progression.power` for `levelBand` (pin + `PowerBandDisplay` tested; builder lands slice 2)

---

## `actor-hud-core`

Spec: [spec-actor-hud-core.md](../docs/architecture/actor-hud/spec-actor-hud-core.md)

- [x] Spec approved
- [x] DTO types in `FusionRpg.Core/Hud/`
- [x] `ActorHudLayout.Prioritize` + overflow
- [x] `PowerBandDisplay.FromTheta`
- [x] `data/tuning/actor-hud.v1.json` + tuning hub
- [x] `ActorHudLayoutTests` green
- [x] `audit-magic-numbers.py` clean on new files
- [x] Acceptance share: priority + overflow + band tests pass
- [x] Reads pipeline SSOT only (DTO is view — no runtime reads in Core)

---

## `actor-hud-dump`

Spec: [spec-actor-hud-dump.md](../docs/architecture/actor-hud/spec-actor-hud-dump.md)

- [x] Spec approved
- [x] P1.5 EntityApply derived pin complete (prerequisite for `levelBand`)
- [x] `ActorHudBuilder` + `ActorHudCache`
- [x] `actorHud` on GameDumps plant/zombie rows
- [x] `actorHud` on `debug.board-stats` rows
- [x] Optional `debug.actor-hud` delta emit
- [x] Golden JSON tests
- [x] Invalidation unit tests
- [x] Acceptance share: golden shield + dual status snapshot
- [x] Reads pipeline SSOT only — read surface contract; no banned sources

---

## `actor-hud-fold`

Spec: [spec-actor-hud-fold.md](../docs/architecture/actor-hud/spec-actor-hud-fold.md)

- [x] Spec approved
- [x] `ActorHudSnapshot` + `Occupant.hud` in lawnViewModel
- [x] `foldActorHud` in lawnProjectorFold
- [x] OBSERVE_CHIPS extended to 13 custom status ids
- [x] `ActorHudInspector` + LawnPage integration
- [x] `lawnProjectorFold.test.ts` cases green
- [x] Acceptance share: fold populates `Occupant.hud` from `actorHud`
- [x] Reads pipeline SSOT only — fold maps wire `actorHud`; no REST/typeId tier math

---

## `actor-hud-unity`

Spec: [spec-actor-hud-unity.md](../docs/architecture/actor-hud/spec-actor-hud-unity.md)

- [x] Spec approved
- [x] `ActorHudPool` + row renderers
- [x] `ActorHudDirector` tick sync (dirty cache)
- [x] Element-colored shield segments
- [x] Status token strip + overflow pip
- [x] Guard: `ActorHudPool_uses_UnitFrameResolver`
- [x] **Visual correction (2026-09-05):** Body + worldYOffset root; TextMesh glyphs; stack pips; bar size from actor-hud tuning
- [ ] LIVE lab board eyeball (quick-start) — owner after deploy
- [x] Acceptance share: guard green
- [x] Reads pipeline SSOT only — builder/cache output; no direct ShieldRuntime/StatusRuntime in pool

---

## `actor-hud-phaser`

Spec: [spec-actor-hud-phaser.md](../docs/architecture/actor-hud/spec-actor-hud-phaser.md)

- [x] Spec approved
- [x] `ActorHudDisplay` helpers
- [x] `setHudDisplay` in SyncFromModelSystem
- [x] Unit test with fixture occupant
- [x] Browser canvas visual check — covered by `e2e/actor-hud.spec.ts` (`__fusionRpgHasHudChild`)
- [x] Acceptance share: sync test matches fold fixture
- [x] Reads pipeline SSOT only — `Occupant.hud` model; no raw event parse
- [x] Program E2E: `e2e/actor-hud.spec.ts` (Inspector + canvas hook)
- [x] Audit 2026-08-31: `shouldShowShield` hp guard (Unity parity); `clearEmptyShield` drops hp≤0; `syncOccupantBandB` seam + strip/overflow/chipRow unit tests; legacy chipRow e2e

---

## `shield-slot-migration`

Spec: [spec-shield-slot-migration.md](../docs/architecture/actor-hud/spec-shield-slot-migration.md)

- [x] Spec approved
- [ ] Shield row parity vs old ShieldBarPool (Body+offset, size, stack pips) — LIVE owner eyeball
- [x] Remove `ShieldBarPool.TickSync` from VfxDirector
- [x] Deprecate or delete ShieldBarPool
- [x] Update shield-system-spec.md §2.6 pointer
- [ ] LIVE: no double shield bar; sustain VFX intact — owner after deploy
- [x] Acceptance share: no TickSync; single bar per unit
- [x] Reads pipeline SSOT only — confirms no second shield data path

---

## P6 — Program E2E

- [x] `e2e/actor-hud.spec.ts` — mocked program scenarios (CI)
- [x] `e2e/actor-hud-live.spec.ts` — live injector path
- [x] Lab board + shield + statuses scenario (`setupLiveActorHudBoard` helper)
- [x] Phaser canvas assertions (`expectCanvasHud` + `__fusionRpgHasHudChild`)
- [x] Inspector `Occupant.hud` assertions match canvas
- [x] `scripts/prove-actor-hud-live.ps1` owner runbook
- [x] P6 audit fixes (2026-08-31): `live-chromium` argv auto-gate, `requestBoardSnapshot` in poll loop, `live-debug-api-core` vitest, ptr normalize in `expectCanvasHud`, mocked shield ratio / hud-clear / elite tier asserts
- [ ] Unity LIVE manual check (owner) — optional polish; see table
- [ ] **Program LIVE sign-off** (optional) — `prove-actor-hud-live.ps1` green + Unity eyeball

### P6 Unity LIVE manual (optional owner polish)

| Check | How |
|-------|-----|
| Single shield bar | `quick-start` + `shield/demo` — no double bar (closes P5 LIVE) |
| Sustain VFX intact | `status/apply` `expose` or `pact_mark` — marker/aura + HUD bar |
| F9 hides shield row only | Toggle F9 — identity/status rows remain |
| Stack pips | Known gap vs old pool — optional note, not blocker |

---

## Post-ship (optional)

- [ ] Boss tier signal from expeditions → builder emits `boss`
- [ ] HP sliver when owner enables tunable
- [ ] Status icon art pass (replace initials)
- [ ] Perf probe B2 before/after published in research
- [x] LIVE harness script — `scripts/prove-actor-hud-live.ps1`

