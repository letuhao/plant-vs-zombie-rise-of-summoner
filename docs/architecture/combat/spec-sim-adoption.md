# Spec: sim-adoption

Module id `sim-adoption` in the [combat unification map](../combat-unification-map.md). Depends on `damage-apply-pipeline`. Small module; **build held** with the rest.

## Objective

Route `SimEngine`'s damage (`DamagePlant`/`DamageZombie`) through the shared apply pipeline with a sim-local `ShieldRuntime` + gate, and expose a sim shield-grant debug command — giving the owner a **server-side shield probe with no game running** (the decoupled-architecture proof they asked for).

## Design (locked on approval — audit-corrected 2026-08-21)

- Sim mounts `ShieldRuntime` + `ShieldGate` per sim session; sim entity keys as owner keys; neutral snapshots where sim has none.
- **Sim has no funnel and no `EffectBag` at all (audit-verified)** — it is always the pipeline's **direct-sink** mode (`IHpDeltaSink` writing `e.Hp`, clamped as today). Standing up a bag just to write HP is exactly the sim-only variant the Never list forbids. `StatMath.ScaleIncoming` stays sim flavor applied *before* the pipeline (verified consistent with today's order); `noteOverlayDamage: false`.
- Sim dumps already emit `rpgShieldHp/rpgShieldMax` (0 today) — they report the sim runtime's totals, so the web bar works against a sim session.
- **Grant surface (audit fix — there is no `sim.*` command dispatcher; sim is HTTP-only):** new route `POST /api/sim/shield/grant` in `SimEndpoints` (target ptr, base, element, durationMs — **ms at the boundary**, converted to sim ticks) calling a new `SimEngine` method. Absorption is surfaced through `GET /api/sim/state` and the dumps — **not** `test.probe`, which echoes its input and reads no sim state (the draft's `shieldAbsorbed`-in-probe claim is dropped).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Sim"
```

## Structure

```
src/FusionRpg.Core/SimEngine.cs      (pipeline routing via direct sink, shield mount, dump totals, grant method)
src/FusionRpg.Server/SimEndpoints.cs (POST /api/sim/shield/grant; state exposes shield totals)
tests/FusionRpg.Core.Tests/          (SimEngine shield probe tests)
```

## Testing strategy

Sim E2E: grant → damage → absorbed remainder on HP + dump totals reflect it; no-shield sim runs byte-identical to today (regression lock); probe payload includes absorption.

## Boundaries

- **Always:** sim stays a telemetry-shaped simulator — no vanilla behavior claims; no-shield path byte-identical.
- **Ask first:** giving sim the full resolver (hit/crit) — out of scope here; this module is apply-path only.
- **Never:** sim writing through Unity paths; SQL; a sim-only shield variant.

## Success criteria

1. Server-side probe: grant + hit + absorb visible via sim endpoints with the game closed. 2. No-shield sim regression byte-identical. 3. Web shield bar renders against a sim session.
