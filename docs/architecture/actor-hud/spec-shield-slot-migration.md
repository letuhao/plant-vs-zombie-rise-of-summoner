# Spec: `shield-slot-migration`

**Module id:** `shield-slot-migration` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-unity` · **Blocks:** —
**Status:** implemented 2026-08-31 — shipped; `ShieldBarPool` removed, VfxDirector cutover green.
**LIVE parity (reconciled 2026-09-05):** shield row must match old **Body + worldYOffset**, bar size, and
**stack pips** via `ActorHudPool` — not a second pool. Blocked on unity visual correction code follow-up.

---

## Assumptions

1. **Cutover only after** `ActorHudPool` renders shield resource row correctly on LIVE lab board (unity
   module acceptance + owner eyeball).
2. **Runtime unchanged** — `ShieldRuntime`, combat math, funnel paths untouched ([shield-system-spec.md](../shield-system-spec.md)).
3. **Sustain VFX unchanged** — `VfxDirector` status motion grammar stays; only standalone shield bar tick
   removed from hot path.
4. **No duplicate bars** — at most one shield visual per owner (HUD resource row). Confirms
   [pipeline audit §7](../../research/actor-hud-data-pipeline-audit-2026-08-30.md) retirement of
   `ShieldBarPool` parallel read path.
5. **F9 / cheat toggles** — any shield display mute applies to HUD resource row, not sustain auras.

---

## Objective

Retire standalone `ShieldBarPool.TickSync` from `VfxDirector.Tick` and subsume shield presentation into
`ActorHudPool` resource row.

**Success:** Shielded unit shows one element-colored bar via ActorHudPool; `ShieldBarPool.WorldBars` stays
0; sustain VFX still play; perf probe shows no regression (or improvement) on shield-heavy scenario.

---

## Program acceptance share

Guard + manual LIVE:

- Grep/CI: `VfxDirector.Tick` does not call `ShieldBarPool.TickSync`
- LIVE: shield + `pact_mark` sustain on same unit — bar + marker VFX both visible, no double shield bar

---

## Commands

```powershell
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\probe-perf.ps1 -Scenario B2 -DurationSec 60
.\scripts\deploy-play.ps1 -NoServer
```

---

## Project structure

| Path | Change |
|------|--------|
| `src/FusionRpg.Injector/Fx/VfxDirector.cs` | edit — remove `ShieldBarPool.TickSync` call |
| `src/FusionRpg.Injector/Fx/ShieldBarPool.cs` | edit — deprecate or delete after migration |
| `src/FusionRpg.Injector/Hud/ActorHudDirector.cs` | edit — sole shield world presenter |
| `docs/architecture/shield-system-spec.md` | edit — §2.6 pointer to actor-hud program |
| `tests/FusionRpg.Guard.Tests/LawnCoordsGuardTests.cs` | edit — remove or redirect ShieldBarPool guard if deleted |

---

## Design

### Migration steps

1. Verify `ActorHudPool` shield row matches retired ShieldBarPool visuals (Body+offset, element
   segments, **stack pips**, familiar bar size from actor-hud tuning).
2. Feature flag or compile-time gate: `ActorHudDirector` owns shield sync.
3. Remove `ShieldBarPool.TickSync()` from `VfxDirector.Tick`.
4. Delete or `[Obsolete]` `ShieldBarPool` if fully subsumed — extract shared color/segment helpers to
   `FusionRpg.Core` or `Injector/Hud` if both needed during transition.
5. Update shield spec §2.6 world presentation section to reference `actor-hud-unity`.

### Rollback

Keep `ShieldBarPool.cs` in tree until one LIVE session confirms migration; delete only after owner sign-off
on todo checkbox.

### Perf expectation

`VfxDirector.Tick` should not stay hot solely because shields exist — HUD director uses dirty cache.
Document before/after in `docs/research/perf/` if probe run.

---

## Boundaries

- Do not change shield damage math or `ShieldGate`.
- Do not remove `rpgShieldHp` from GameDumps until fold fully on `actorHud` (may already be dual during P3).

---

## Test plan

| Test | Assert |
|------|--------|
| Guard no TickSync | Static read VfxDirector.cs |
| LIVE dual VFX | Marker + HUD bar same unit |
| Perf B2 | Note ms in research log — no mandatory threshold v1 |

---

## Related

- [spec-actor-hud-unity.md](spec-actor-hud-unity.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
- [VfxDirector.cs](../../../src/FusionRpg.Injector/Fx/VfxDirector.cs)
