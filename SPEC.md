# SPEC — Status state visuals (vfx-v3): sustained per-status VFX

**Status:** Draft — pending owner review.
**Scope:** Injector VFX only. The **13 custom statuses** get unique while-active visuals; the 8 engine-wrapped vanilla statuses (butter, freeze, cold, poison, hypno, ember, jala, kelp) keep their original game visuals **untouched**.
**Parent:** [docs/architecture/vfx-ssot.md](docs/architecture/vfx-ssot.md) (locked; this spec uses its reserved extension points). Prior round: [docs/architecture/vfx-v2-spec.md](docs/architecture/vfx-v2-spec.md) (complete, LIVE-proven).

---

## 1. Objective

Vanilla PVZ communicates status through **duration-bound** visuals — the butter blob stays while buttered, the freeze tint stays while frozen. Our 13 custom statuses are **invisible while active**: a player cannot see that a zombie is withering, marked by a pact, or exposed. This round gives each custom status a unique, glanceable visual identity that lives exactly as long as the status does.

**Architecture answer (asked and settled):** the locked cue → recipe → primitive design supports this without structural change. What's needed is exactly what the SSOT reserved: the `status.{id}.expire` cue (§4.1, vocabulary reserved since v1) plus one new *sustained* primitive family whose lifetime runs from apply-cue to expire-cue instead of a fixed `LifeSeconds`. Everything else — catalog, admission, pooling discipline, prove harness — extends as data.

Done means: every custom status is identifiable on sight by its visual alone; vanilla visuals byte-identical; the tight budget holds (no lag); apply→sustain→end proven LIVE for lifecycle edges (expire, clear, host death, match end).

## 2. Capability map (build order)

| Module | Delivers | Depends on |
|---|---|---|
| **M1 expire producer** | `StatusRuntime.OnExpired` (+ clear path) mirroring `OnApplied`; `status.{id}.expire` cues; `VfxCueDto.DurationMs` carried on apply cues (TTL safety) | — |
| **M2 sustained state tracker** | Director-side registry keyed `(hostPtr, statusId)`: start on apply, refresh on re-apply, end on first of expire / clear / host-gone / TTL cap / match end / eviction; `debug.fx.state.*` events | M1 |
| **M3 Aura primitive** | Pooled looping attached particles; motion styles from pure `VfxAuraMath` (Drip, Orbit, RiseSparkle, CrackleJitter, PulseRing, StreamOut, plus batch identity styles WispOut/BubbleRise/ChunkFall, SparkStrobe/ShardGlitter, SporeDrift/CharmHeartbeat, PactFootPulse/CommandCrownPulse) | M2 |
| **M4 Tint primitive** | `TintCompositor`: per-renderer tint stack, multiplicative blend (≤35%), base capture/restore, 0.25s re-assert vs vanilla color writes | M2 |
| **M5 Marker primitive** | Floating badge above unit; procedurally generated shape textures (Ring, Diamond, TriangleDown, Cross); gentle bob | M2, M3 (shares pool) |
| **M6 designs + prove** | 13 seed compositions (§4), prove-vfx lifecycle cases, eyeball checklist per status | M3–M5 |

M3/M4/M5 are independent of each other; each ships green separately.

## 3. Architecture extension (locked shapes)

- **Producer:** `OnExpired` fires when `StatusRuntime` prunes an expired instance and when a clear removes it (payload: the `StatusInstance`). Death of the host emits nothing — the tracker reaps via anchor Unity-null (host-gone), same lesson as floaters.
- **Cue contract:** apply cues gain `DurationMs` (from `EffectiveDuration`) so the tracker sets a hard TTL = duration + 2s; the expire cue normally ends the visual first. A visual may **never** outlive its TTL — a missed expire cue self-heals.
- **Recipe model:** `VfxPrimitiveSpec` gains `Sustained = true` variants via `Kind ∈ {Aura, Tint, Marker}` + `AuraStyle`, `MarkerShape` fields. Sustained specs ignore `LifeSeconds` (lifetime is the status's). Existing transient kinds unchanged.
- **Keying and refresh:** one sustained visual set per `(hostPtr, statusId)` — re-apply refreshes TTL, never duplicates (matches Refresh stacking).
- **Budget (locked, "tight"):** `AuraPool` = **24** looping systems global (markers share it); **max 2 sustained visual sets per host** — priority: marker-bearing statuses (they are gameplay-reactive) over newest, evicted sets end with reason `evicted`. Aura particle discipline follows the LIVE lessons: emission module **off**, particles pulsed manually from the director tick (≤6 live per aura, pulse every ~0.3s), explicit per-particle colors always.
- **Tint safety (the risky one, rules locked):** custom statuses only; compositor owns one stack per renderer; composite = base × lerp(white, statusColor, ≤0.35); base captured at first custom tint, restored when the stack empties; re-asserted every 0.25s — if an external write is detected at re-assert (vanilla hurt-flash), adopt the new value as base and re-composite. If a status is both tinted and vanilla-tinted territory, tint loses (Marker/Aura carry the identity).
- **Events:** `debug.fx.state.started` / `debug.fx.state.ended` with `cueId`, `ptr`, and enumerated end `reason`: `expired`, `cleared`, `host-gone`, `ttl-cap`, `evicted`, `match-end`, `disabled`. `SYS-DAMAGE-FX` off ends all sustained visuals immediately.
- **Idle-cheapness preserved:** the director's early-out gains one count (live sustained sets); with none live the tick cost is unchanged.

## 4. The 13 identities (design table — the creative core)

Composition per status mixes the three methods freely; no two share a motion × color × marker combo. Colors extend `VfxSeedCatalog.StatusFx`.

| Status | Fantasy | Aura (style, color) | Tint | Marker | Read-at-a-glance |
|---|---|---|---|---|---|
| `wither` | life draining out | WispOut, ash wisps up/out | 25% desaturating grey-brown | — | unit visibly "graying out" |
| `blight` | spreading disease | BubbleRise, sickly-green bubbles from feet | 20% green | — | rising green bubbles (lane sickness) |
| `rot` | earthy decay | ChunkFall, dark umber chunks in narrow column | 20% umber | — | heavy downward decay |
| `spark` | electrified | SparkStrobe, yellow-white sparks teleporting in tight box | — | — | electric strobe crackle |
| `spore` | fungal host | SporeDrift, lime spores drifting upward | — | — | circling spores rising |
| `pact_mark` | marked for the pact | PactFootPulse at feet, violet | — | Diamond, violet | **must** be instantly readable — it's a target mark |
| `leech` | being drained | StreamOut, deep-red motes sinking inward | 15% red | — | red seep |
| `expose` | armor opened | CrackleJitter, gold glints (sparser than spark) | — | TriangleDown, gold | "hit this one now" |
| `shatter` | armor shattered | ShardGlitter, cyan-white horizontal shard glints | 15% cyan | — | icy shards distinct from spark strobe |
| `bond` | linked | Orbit, pink motes, slow | — | Ring, pink | paired units share the ring |
| `rally` | rallied buff | RiseSparkle, warm gold | 10% warm | — | buffs rise (heal-motes grammar) |
| `command` | commanded | CommandCrownPulse above head, blue-violet halo | — | Ring, blue-violet | crown-like halo |
| `charm_pulse` | charmed | CharmHeartbeat, magenta motes with heartbeat pulse | 15% magenta pulse | — | magenta heartbeat |

Grammar rules that keep the battlefield legible: **Drip = DoT**, **CrackleJitter = generic armor/electric fallback**, **SparkStrobe/ShardGlitter = batch-2 crackle identity**, **SporeDrift/CharmHeartbeat = batch-3 orbit identity**, **PactFootPulse/CommandCrownPulse = batch-5 pulsering identity**, **Orbit = passive affliction/link fallback**, **PulseRing = active mark fallback**, **Rise = buff**, markers only on states the player must react to (pact_mark, expose, bond, command). Apply-moment bursts diverge per batch-1/2/3/4 status overrides (all 13 custom statuses).

## 5. Commands

```powershell
# offline (per module)
dotnet test tests\FusionRpg.Core.Tests; dotnet test tests\FusionRpg.Guard.Tests
$env:FUSIONRPG_ML_GAMEDIR='H:\Games\PVZ-Fusion-3.9_MelonLoader'
dotnet build src\FusionRpg.Injector.MelonLoader.39\FusionRpg.Injector.MelonLoader.39.csproj -p:OutputPath="$env:TEMP\fusionrpg-vfx-build\"

# LIVE (established cycle: close game → build to Mods → relaunch → enter level → lab → prove)
.\scripts\setup-lab-run.ps1
.\scripts\prove-vfx.ps1 -TargetPtr <ZombiePtr>
# debug: POST /api/debug/fx/state (new) dumps live sustained sets
```

## 6. Project structure (touched)

```
src/FusionRpg.Core/Vfx/       VfxRecipes (+Aura/Tint/Marker kinds, styles), VfxAuraMath (new, pure),
                              VfxRules (sustained caps/TTL constants), VfxCatalog (13 sustained rows),
                              StatusVfxCues (+Expire cue)
src/FusionRpg.Core/Status/    StatusRuntime (+OnExpired at prune/clear sites)
src/FusionRpg.Contracts/      VfxCueDto (+DurationMs)
src/FusionRpg.Injector/Fx/    AuraPool (new), TintCompositor (new), VfxDirector (state tracker,
                              start/end, eviction, new events), FxResources (marker shape textures)
src/FusionRpg.Injector/       CheatCommandRunner (+debug.fx.state), Server DebugEndpoints (+/fx/state)
tests/…/Vfx/                  aura math envelopes, tracker lifecycle (pure decision core), tint
                              compositor math, catalog completeness (13 sustained sets)
scripts/prove-vfx.ps1         lifecycle cases: apply→started, expire→ended(expired), kill→ended(host-gone)
```

## 7. Code style

House rules unchanged: constants in `VfxRules`; decision logic pure in Core (the tracker's start/refresh/end/evict decisions are a pure class the director drives — same pattern as `VfxAdmission`); no throws into the loop; every end path emits its reason; comments state constraints (especially TintCompositor's restore rules); neutral voice.

## 8. Testing strategy

- **Unit:** `VfxAuraMath` envelopes per style (Drip falls, Orbit closes a loop, Crackle stays in-bounds); tracker lifecycle as a pure state machine (apply/refresh/expire/ttl/evict orderings, including expire-after-ttl and re-apply-after-evict); tint composite/restore math; catalog completeness (each of 13 has a sustained set; engine-wrapped 8 have **none**).
- **Guard:** Fx/ stays `FindObjectsOfType`-free; no vanilla-status visual code paths touched.
- **LIVE (the real oracle, per the v2 lesson):** prove-vfx lifecycle cases event-asserted; per-status eyeball checklist (13 rows) — visuals are judged by your eyes, events only prove lifecycle.

## 9. Boundaries

**Always:** vanilla status visuals untouched (engine-wrapped 8 get no sustained visuals at all); every sustained visual ends via TTL even if all signals are missed; restore tints on every end path including `ClearAll`; each module ships green before the next.
**Ask first:** raising the 24/2 budget; adding sustained visuals to engine-wrapped statuses; any new marker shape needing non-procedural art; changes to locked rate-limit values.
**Never:** gameplay reads/writes from VFX; `renderer.material`; auto-emission without explicit color (LIVE lesson); scene scans; timeline DSL; git commits (owner commits).

## 10. Open questions for review

1. Tint re-assert vs vanilla hurt-flash: the 0.25s adopt-latest-base heuristic is my best design; accept, or drop Tint from statuses where it proves flickery LIVE (fallback is always aura-only)?
2. `bond` pairs: same ring on both linked units is v1; a visible link line between them is a possible future primitive (Beam) — out of scope here?
3. Marker glyphs are geometric (procedural). Good enough, or do you want a tiny pixel-art asset pipeline someday (bigger question, separate spec)?
