# Spec: `actor-hud-unity`

**Module id:** `actor-hud-unity` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-core`, `actor-hud-dump` · **Blocks:** `shield-slot-migration`
**Status:** implemented 2026-08-31 — shipped; guard tests green.

---

## Assumptions

1. **UnitFrame SSOT** — placement via `UnitFrameResolver` at crown anchor for the stack (plate 10 §A);
   guard test required like `ShieldBarPool_uses_UnitFrameResolver`.
2. **Shield row** reuses segment grammar from [ShieldBarPool.cs](../../../src/FusionRpg.Injector/Fx/ShieldBarPool.cs)
   — element-colored segments mandatory (audit accessibility).
3. **Status row** uses static sprites/textures — almanac initials v1 (plate mock `SP`, `WI` style); not
   sustain VFX duplication.
4. **Coexist with VfxDirector** — sustain auras remain on body/feet/crown; HUD sorts above with tunable
   `sortOffsetAboveUnit` (may share vfx tuning or actor-hud row offsets).
5. **Sync source:** read `ActorHudBuilder` / `ActorHudCache` output only — **no direct `ShieldRuntime`,
   `StatusRuntime`, or derived re-resolve in render path**. Builder owns gather; pool owns draw
   ([pipeline audit §5](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)).

---

## Objective

Render three-row Band B HUD above each unit in Unity world space from the same snapshot the web fold uses.

**Success:** LIVE lab board — shielded zombie shows element-colored segments + status tokens at UnitFrame;
sustain VFX still visible on body; F9 mute behavior documented.

---

## Program acceptance share

1. Guard: `ActorHudPool_uses_UnitFrameResolver` in `LawnCoordsGuardTests.cs`
2. Manual LIVE: `/lawn/quick-start` + shield + status cheat — owner eyeball matches plate 10 §A

Automated mesh tests optional v1; guard is mandatory.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Guard.Tests --filter ActorHudPool
.\scripts\guard-single-writer.ps1
.\scripts\deploy-play.ps1 -NoServer
```

---

## Project structure

| Path | Change |
|------|--------|
| `src/FusionRpg.Injector/Hud/ActorHudPool.cs` | **new** — slot pool, row renderers |
| `src/FusionRpg.Injector/Hud/ActorHudRowIdentity.cs` | **new** — tier, badge, role pip |
| `src/FusionRpg.Injector/Hud/ActorHudRowResources.cs` | **new** — shield segments |
| `src/FusionRpg.Injector/Hud/ActorHudRowStatuses.cs` | **new** — token strip |
| `src/FusionRpg.Injector/Hud/ActorHudDirector.cs` | **new** — tick sync entry (optional) |
| `src/FusionRpg.Injector/Fx/VfxDirector.cs` | edit — call `ActorHudDirector` instead of shield-only path (migration defers removal) |
| `tests/FusionRpg.Guard.Tests/LawnCoordsGuardTests.cs` | edit — guard test |

---

## Design

### Layout

```text
UnitFrame crown anchor (X from bounds center, Y from resolver)
  Row 0 identity:  tier frame | role pip | level badge
  Row 1 resources: shield track (segments) | meter ticks (optional)
  Row 2 statuses:  tokens | +N overflow
```

Row Y offsets from `actor-hud.v1.json` as fractions of `UnitFrame.Span()`.

### Pool mechanics

- Cap on concurrent HUD slots (tunable) — reuse MeshRenderer / sprite pool pattern from ShieldBarPool
- Owner key = normalized combat ptr
- Slot lifecycle: acquire on first shield/status/tier signal; release on entity death / board clear
- **HP sliver:** not rendered when `hpSliverEnabled == false`

### Shield segments

- Migrate `ShieldBarColor.Stop` gradient logic
- Max segments from tunable (inherit vfx shield bar max or actor-hud tunable)
- Display ratio: keep `ShieldBarVisual.DisplayRatio` stepped fill unless tunable changes

### Status tokens

- Text mesh or sprite with 2-letter almanac token v1
- CC corner accent when `cc: true`
- Overflow pip when `overflow.statusCount > 0`

### Perf

- Prefer `ActorHudCache` dirty set from dump module
- Full reconcile fallback ≤ once per frame if dirty set non-empty

---

## Boundaries

- Presentation-only — no `EntityStatWriter` / funnel writes.
- No `BodyWorld` or raw `Renderer.bounds` outside `UnitFrameResolver`.
- **No direct runtime reads** — `ActorHudPool` consumes builder/cache snapshot only; same separation as
  fold/Phaser (no parallel shield data path after migration).
- Do not remove `ShieldBarPool` in this module — migration spec owns cutover.

---

## Test plan

| Test | Assert |
|------|--------|
| Guard UnitFrame | No direct bounds reads in ActorHudPool |
| Unit slot cap | Eviction policy documented if over cap |
| LIVE eyeball | Shield element visible; statuses readable |

---

## Related

- [spec-unit-frame.md](../vfx/spec-unit-frame.md)
- [spec-shield-slot-migration.md](spec-shield-slot-migration.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
- [ShieldBarPool.cs](../../../src/FusionRpg.Injector/Fx/ShieldBarPool.cs)
