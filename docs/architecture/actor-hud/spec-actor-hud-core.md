# Spec: `actor-hud-core`

**Module id:** `actor-hud-core` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md)
**Depends on:** — · **Blocks:** all other actor-hud modules
**Status:** implemented 2026-08-31 — shipped; `ActorHudLayoutTests` green.

---

## Assumptions

1. **Pure Core** — no Unity, no SQL, no HTTP. Namespace `FusionRpg.Core.Hud`.
2. **Presentation-only DTO** — `ActorHudSnapshot` is a **view of Hot snapshot data**, not a new SSOT store.
   The injector builder fills DTOs from runtimes and pins; Core types carry no gameplay logic.
3. **Level display** uses [ssot-power-scale.md](../power/ssot-power-scale.md) — display band from Θ, not raw
   magnitude on the lawn (GG-60). **`PowerBandDisplay.FromTheta` input comes from pinned `progression.power`
   only** — builder passes Θ from `InjectorDerivedOverride` pin, never from Unity fields or REST.
4. **Tunables** live in `data/tuning/actor-hud.v1.json` — loaded via existing tuning hub pattern
   ([tunables-ssot.md](../tunables-ssot.md)).
5. **Status strip cap** and row offsets are tunable; priority order is structural (documented in code comment).

---

## Objective

Provide the shared **Actor HUD vocabulary**: snapshot DTOs, slot priority, overflow math, and level-band
display mapping used by dump, fold, Unity, and Phaser.

**Success:** Given fixture statuses + cap, `Prioritize` returns CC-first order with overflow count;
given Θ, `PowerBandDisplay` returns stable display int for badge.

---

## Program acceptance share

`tests/FusionRpg.Core.Tests/Hud/ActorHudLayoutTests.cs` — priority ordering, overflow count, band mapping
edge cases (Θ=0, large Θ). Module not done until these tests pass.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter ActorHud
python scripts\audit-magic-numbers.py --targets M1
```

---

## Project structure

| Path | Change |
|------|--------|
| `src/FusionRpg.Core/Hud/ActorHudSnapshot.cs` | **new** — DTO records |
| `src/FusionRpg.Core/Hud/ActorHudTier.cs` | **new** — tier enum |
| `src/FusionRpg.Core/Hud/MagnitudeBand.cs` | **new** — low/mid/high |
| `src/FusionRpg.Core/Hud/ActorHudLayout.cs` | **new** — `Prioritize`, overflow |
| `src/FusionRpg.Core/Hud/PowerBandDisplay.cs` | **new** — Θ → display band |
| `src/FusionRpg.Core/Hud/ActorHudTuning.cs` | **new** — tunable shape |
| `src/FusionRpg.Core/Hud/ActorHudTuningHub.cs` | **new** — load `actor-hud.v1.json` |
| `data/tuning/actor-hud.v1.json` | **new** — v1 defaults |
| `tests/FusionRpg.Core.Tests/Hud/ActorHudLayoutTests.cs` | **new** |

---

## Design

### DTO shape (C# — mirrors TS in fold spec)

```csharp
public sealed record ActorHudSnapshot(
    ActorHudIdentity Identity,
    ActorHudResources? Resources,
    IReadOnlyList<ActorHudStatusToken> Statuses,
    ActorHudOverflow Overflow);

public sealed record ActorHudIdentity(
    ActorHudTier Tier,
    string Role,           // "specimen" | "vanilla"
    int? LevelBand,
    IReadOnlyList<string> Flags);

public sealed record ActorHudResources(
    ActorHudShield? Shield,
    ActorHudHpSliver? HpSliver,
    IReadOnlyList<ActorHudMeter>? Meters);

public sealed record ActorHudShield(
    long Hp, long Max,
    IReadOnlyList<ActorHudShieldStack> Stacks);

public sealed record ActorHudStatusToken(
    string Id, bool Cc, MagnitudeBand MagnitudeBand);
```

### Priority order (plate 10 §D)

`ActorHudLayout.Prioritize(statuses, maxVisible)`:

1. CC statuses first (`Cc == true`)
2. Remaining by stable id order (deterministic — no frame-to-frame shuffle)
3. Return `(visible, overflowCount)` where `overflowCount = max(0, total - maxVisible)`

Identity row slot priority when rows compete for space (unity/phaser use same ordering):

1. CC glyph / frozen status
2. Unique/demon pip
3. Shield segments
4. Top N status tokens
5. Level badge · tier frame

### Level band

`PowerBandDisplay.FromTheta(long theta)` — map Θ to compact display int (e.g. 1–99 cap for badge width).
Use power ladder SSOT; **never** emit raw Θ on lawn. Caller (dump builder) supplies Θ from pinned derived
`progression.power` only — Core does not read pins or runtimes directly.

**DTO is not SSOT:** if pin is missing, builder omits `LevelBand`; Core layout code does not invent defaults.

### Tunables (`data/tuning/actor-hud.v1.json`)

| Key | v1 default | Notes |
|-----|------------|-------|
| `statusStripMax` | 3 | Visible status tokens before `+N` |
| `hpSliverEnabled` | false | When false, builder omits `hpSliver` |
| `rowOffsetIdentity` | tunable | World Y offset fractions (unity reads) |
| `rowOffsetResources` | tunable | |
| `rowOffsetStatuses` | tunable | |
| `eliteTierThreshold` | TBD | Structural placeholder for elite band |

---

## Boundaries

- No Unity types, no injector references.
- No status id validation beyond non-empty string — closed vocabulary enforced in dump builder.
- Boss tier enum value exists but builder **must not emit** until expedition signal wired.

---

## Test plan

| Test | Assert |
|------|--------|
| `Prioritize_cc_first` | CC statuses precede non-CC at same cap |
| `Prioritize_overflow_count` | 5 statuses, cap 3 → overflow 2 |
| `FromTheta_monotonic` | Higher Θ → higher or equal band |
| `Tuning_loads_defaults` | Missing file throws or uses documented fallback |

---

## Related

- [spec-actor-hud-dump.md](spec-actor-hud-dump.md) — fills DTO from Hot read surface
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
- [10-actor-hud.html](../../design/10-actor-hud.html) §B legend
