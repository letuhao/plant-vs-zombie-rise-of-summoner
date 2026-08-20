# Tasks: VFX v2 — producibility, performance, visual pack

Plan: [vfx-v2-plan.md](vfx-v2-plan.md) · Spec: [../SPEC.md](../SPEC.md) · Parallel round: perf-v3 in [todo.md](todo.md)

- [x] **T1: Anchor resolution via InjectorEntityRegistry** (SPEC W1, F4)
  - `AnchorResolver.Resolve(ptr)` → facade over `InjectorEntityRegistry.FindZombie/FindPlant(ptrHex)?.transform`; delete its cache, `Sweep()`, `Register`, `Tick`; remove the two `AnchorResolver.Register` lines in `GameHooks` Start postfixes (registry adds already there); drop dead calls in `VfxDirector`.
  - Accept: guard pin — no `FindObjectsOfType` under `Injector/Fx/`; all suites + Melon 3.9 build green.
  - Files: `Fx/AnchorResolver.cs`, `Fx/VfxDirector.cs`, `GameHooks.cs`, `Guard.Tests/LawnCoordsGuardTests.cs`. Scope: S.

- [x] **T2: Idle-cheap VfxDirector.Tick** (SPEC W2, F3)
  - `Camera.main` moves to `Draw()`, resolved only when floaters exist, cached, re-resolved on Unity-null; `Tick` early-outs when queue+floaters+bursts+flashes are all empty.
  - Accept: suites + build green; `vfx.tick ≤ 0.5%` wall @300z verified at the next perf-v3 stress run (budget recorded in T7).
  - Files: `Fx/VfxDirector.cs`. Scope: S.

### Checkpoint 1
- [x] Core+CheatCore+Guard green (835/40/38); Melon 3.9 compile green; Fx/ per-cue path FindObjectsOfType-free (guard-pinned). Perf-v3 todo A5 note updated: VfxDirector struck as suspect.

- [x] **T3: Floater visual pack** (SPEC W3, F5)
  - `VfxRules.PopScale(t)` (crit overshoot ~1.5× → settle 1.25×; plain flat 1.0) + `VfxRules.AmountScale(amount)` (<50→0.9, <200→1.0, ≥200→1.15); `VfxColorPlan.FontScaleAt(t, amount)`; shadow pass in `Draw` (black +1,+1 then color, alpha from `DamageFxFloaterRules.Alpha`).
  - Accept: curves + tiers unit-test-pinned; suites + build green.
  - Files: `Core/Vfx/VfxRules.cs`, `VfxColorPlan.cs`, `Fx/VfxDirector.cs`, core tests. Scope: S.

- [x] **T4: Burst shape math (core only)** (SPEC W4a, F6)
  - `VfxBurstShape { Radial, Rising, Directional }` on `VfxPrimitiveSpec` (default Radial = legacy math verbatim); new pure `Core/Vfx/VfxBurstMath.Particle(shape, index, count, span, life)` → (pos, vel, size, energy); catalog validation accepts the field.
  - Accept: envelope tests per shape (Radial symmetric; Rising vel.y>0 all; Directional vel.x<0 dominant); Radial matches legacy constants exactly; suites green.
  - Files: `Core/Vfx/VfxRecipes.cs`, `VfxBurstMath.cs` (new), `VfxCatalog.cs`, core tests. Scope: S.

- [x] **T5: Pool consumes shapes + impact flash + heal motes** (SPEC W4b)
  - `BurstPool.Spawn` emission loop → `VfxBurstMath.Particle`; seed catalog: `combat.hit` + `Flash` spec (first user of the primitive), `combat.heal` + `Rising` burst.
  - Accept: catalog tests updated; suites + Melon build green; rate limits unchanged.
  - Files: `Fx/BurstPool.cs`, `Core/Vfx/VfxCatalog.cs`, catalog tests. Scope: S.

### Checkpoint 2
- [x] Suites green (862 core); Melon 3.9 compile green; catalog validates Shape + new specs.

- [x] **T6: Status cue producer path** (SPEC W5, F1)
  - Core: `Action<StatusInstance>? OnApplied` property on `StatusRuntime`, invoked at the definitive-success point (`StatusRuntime.cs:189`, post-Upsert); resist + StatusICD paths emit nothing; spread hops covered automatically (rate limiter bounds per-host spam).
  - `Core/Vfx/StatusVfxCues.cs` (new): `CueId(statusId)` → `status.{id}.apply`; `Cue(instance)` → ptr-anchored `VfxCueDto`.
  - Injector: `_status.OnApplied = inst => VfxDirector.Play(StatusVfxCues.Cue(inst));` at `EffectRuntime.cs:59-68` beside `OnResisted`.
  - Seeds: all 21 statuses from one statusId→RGB table (small Burst 12–16 particles + host Flash each). No "resisted" cue (outside SSOT vocabulary).
  - Accept: recording-sink test — apply emits, resist/ICD don't, one spread-hop case; catalog test asserts 21 status cues, all validating; suites + build green.
  - Files: `Core/Status/StatusRuntime.cs` (+2 lines), `Core/Vfx/StatusVfxCues.cs`, `VfxCatalog.cs`, `Injector/Effects/EffectRuntime.cs` (+1 line), tests. Scope: M.

- [x] **T7: Docs + prove sync** (SPEC W6, F2/F7/F8)
  - `vfx-ssot.md` §16.2 + `light`(255,232,120)/`dark`(150,90,220) rows, status header note; `prove-vfx.ps1` → 6 elements, one Rising case, one status-apply case; `stress-test.ps1` vfx.tick ≤0.5% @300z warning-level assert; SPEC.md findings checked off.
  - Accept: docs consistent; script syntax-checks; suites green.
  - Scope: S.

### Checkpoint 3 (offline complete)
- [x] All suites + guards green; Melon 3.9 build green; docs synced.

### Final gate — LIVE probe
- [x] **PASSED 2026-08-21 (43/43)** — `prove-vfx.ps1 -TargetPtr` verdict in `docs/research/effect-runtime/_prove-vfx.json`; owner eyeball-confirmed (no white particles, colors true). Three LIVE render fixes landed during the gate: default emission module disabled on pooled systems (auto white dribble), texture steal deleted (soft disc always — vanilla lightning sheets were leaking in), shader preference alpha-blend first (additive washed pale colors). Plus owner call: `combat.hit` burst/flash are element-only (`RequireElement`, skip reason `no-element`) — plain damage renders numbers only. vfx.tick budget still read from next perf-v3 stress run.

Coverage (~40 asserted cases): all 6 element colors + hybrid + neutral-white; all 21 status recipes; unknown-cue / rate-limited / muted / disabled skip reasons; mute + master-toggle + element-toggle roundtrips; world-flash alias; **organic producers** (`debug.effect.enqueue-delta` → funnel → `combat.hit` cue; `debug.status.apply` → StatusRuntime → `status.wither.apply` cue); flash + floater primitives asserted on the ptr cases. Without `-TargetPtr` the organic/flash/floater cases are skipped with a warning — pass the ptr for the real gate. Ends with an eyeball checklist for pop/shadow/shapes/rainbow (not event-assertable).

**Resolved (2026-08-21):** the `E2E Catalog_every_implemented_kind_one_match` red was NOT the `IsNoisyKind` shed (that filter only trims the SignalR broadcast; the store still counts everything). Root cause: the perf round flipped `StatsConfig.LogDamage` default to opt-in (Contracts `Dtos.cs:21`), and `SimEngine.DamagePlant/DamageZombie` gate their `*.damage` emission on it — the catalog test relied on the old default. Fix: the test now opts in via `PUT /api/stats { LogDamage = true }`, honoring the new contract. All six suites green (1,268 tests).
