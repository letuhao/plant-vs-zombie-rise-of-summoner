# Spec: `actor-hud-unity`

**Module id:** `actor-hud-unity` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-core`, `actor-hud-dump` · **Blocks:** `shield-slot-migration`
**Status:** implemented 2026-08-31 — shipped; guard tests green.
**Visual correction (2026-09-05):** Body + `worldYOffset`; TextMesh glyphs + stack pips — code landed.

---

## Assumptions

1. **UnitFrame SSOT** — placement via `UnitFrameResolver` at **Body** + tunable `worldYOffset`
   (default −0.35; plate 10 §A + ideal Band B). Guard: no raw `Renderer.bounds` / `BodyWorld` outside
   the resolver.
2. **Shield row** reuses segment grammar from the retired ShieldBarPool — element-colored segments +
   **stack pips** mandatory; bar W/H from `actor-hud` tuning (not live `vfx.v3` `render.shieldBar`).
3. **Status row** uses **TextMesh (or equivalent) 2-letter almanac tokens** (plate mock `SP`, `WI`
   style); not sustain VFX duplication. Display token rules shared with Phaser via Core.
4. **Identity row** draws **tier letter + level digits** (not blank colored quads).
5. **Coexist with VfxDirector** — sustain auras remain on body/feet/crown; HUD root is Body+offset;
   row local Y stacks upward from the shield slot.
6. **Sync source:** read `ActorHudBuilder` / `ActorHudCache` output only — **no direct `ShieldRuntime`,
   `StatusRuntime`, or derived re-resolve in render path**. Builder owns gather; pool owns draw
   ([pipeline audit §5](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)).
7. **F9** mutes shield resource row only.

---

## Objective

Render three-row Band B HUD at **center-bottom** of each unit in Unity world space from the same
snapshot the web fold uses.

**Success:** LIVE lab board — shielded zombie shows element-colored segments + stack pips + readable
status letters + level digits under the unit (Body+offset); sustain VFX still visible; F9 mute
documented.

---

## Program acceptance share

1. Guard: `ActorHudPool_uses_UnitFrameResolver` / Body anchor (not Crown-only)
2. Manual LIVE: `/lawn/quick-start` + shield + status cheat — owner eyeball matches plate 10 §A (bottom)

Automated mesh tests optional v1; guard is mandatory.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Guard.Tests --filter ActorHud
.\scripts\guard-single-writer.ps1
.\scripts\deploy-play.ps1 -NoServer
```

---

## Project structure

| Path | Change |
|------|--------|
| `src/FusionRpg.Injector/Hud/ActorHudPool.cs` | slot pool, Body+offset root, pip slots |
| `src/FusionRpg.Injector/Hud/ActorHudRowIdentity.cs` | tier letter, level digits, role pip |
| `src/FusionRpg.Injector/Hud/ActorHudRowResources.cs` | shield segments + stack pips |
| `src/FusionRpg.Injector/Hud/ActorHudRowStatuses.cs` | 2-letter tokens + overflow |
| `src/FusionRpg.Injector/Hud/ActorHudDirector.cs` | tick sync entry |
| `src/FusionRpg.Injector/Fx/VfxDirector.cs` | call `ActorHudDirector` on live match path |
| `tests/FusionRpg.Guard.Tests/ActorHudUnityGuardTests.cs` | placement + UnitFrame guards |

---

## Design

### Layout

```text
UnitFrame Body anchor (X = bounds center via resolver) + worldYOffset (default -0.35)
  local Y ≈ 0     — Resource row: shield track + element segments + stack pips
  local Y > 0     — Status strip: 2-letter tokens | +N overflow
  local Y higher  — Identity: tier letter | role pip | level digits
```

Row Y offsets and bar W/H from `actor-hud` tuning (fractions of span and/or fixed world sizes).

### Pool mechanics

- Cap on concurrent HUD slots = **96** — **structural** pool buffer (not a balance dial; omit at capacity, no eviction). Not a tuning key.
- Owner key = normalized combat ptr
- Slot lifecycle: acquire on first shield/status/tier signal; release on entity death / board clear
- **HP sliver:** not rendered when `hpSliverEnabled == false`

### Shield segments

- `ShieldBarColor.Stop` gradient logic
- Max shield segments = **4** — **structural** (segment array length); stack pip count clamped by tunable `maxStackPips`
- Display ratio: `ShieldBarVisual.DisplayRatio` stepped fill

### Status tokens

- TextMesh (or sprite) with 2-letter almanac token v1 — Core `ActorHudDisplayTokens` SSOT
- CC corner accent when `cc: true`
- Overflow pip when `overflow.statusCount > 0`

### Perf

- Prefer `ActorHudCache` dirty set from dump module
- Full reconcile fallback ≤ once per frame if dirty set non-empty
- `VfxDirector` must still tick HUD on live match when WorldBars is still 0 (no chicken-egg)

---

## Boundaries

- Presentation-only — no `EntityStatWriter` / funnel writes.
- No `BodyWorld` or raw `Renderer.bounds` outside `UnitFrameResolver`.
- **No direct runtime reads** — `ActorHudPool` consumes builder/cache snapshot only; same separation as
  fold/Phaser (no parallel shield data path after migration).
- `ShieldBarPool.cs` is deleted — do not reintroduce.

---

## Test plan

| Test | Assert |
|------|--------|
| Guard UnitFrame Body | ActorHudPool uses Body + worldYOffset; no Crown-only root |
| Guard tokens | Core display tokens match Phaser initials |
| LIVE eyeball | Bar under unit; statuses readable; level digits visible |

---

## Related

- [spec-unit-frame.md](../vfx/spec-unit-frame.md)
- [spec-shield-slot-migration.md](spec-shield-slot-migration.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
