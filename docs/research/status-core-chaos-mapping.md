# Chaos Status Core → FusionRpg mapping

**Status:** Research / design reference only — not product ADR. Normative spec: [../architecture/status-ssot.md](../architecture/status-ssot.md). Derived stats: [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md).

External design source (do not vendor code or YAML trees):

`chaos-backend-service/docs/status-core` and `docs/element-core` on the author's machine.

---

## What we borrow

| Chaos concept | FusionRpg mapping |
|---|---|
| Apply pipeline: Validate → Immunity → roll → magnitude → duration → Apply | **L2 StatusRuntime** + **L2b ResistanceEvaluator** at Apply/Refresh |
| **Two-phase resolve:** sigmoid apply chance + separate potency | Phase 1: `p_apply = sigmoid(delta / effectiveApplyScale)`; Phase 2: linear `netFactor` for magnitude/duration — [actor-hub-ssot.md §4](../architecture/actor-hub-ssot.md) |
| **Omni additive-only:** `total = omni + category + per-id` | `status.power.omni` + `status.power.{category}`; same for resist |
| Attacker mastery / magnitude scaling (`fire_mastery_multiplier`, `scaling_stat` on apply) | **`status.power.{category}`** + **`progression.power(attacker)`** on attacker snapshot |
| Defender resistance in delta | **`status.resist.*`** + **`progression.power(defender) × ResistFromPowerRatio`** (ratio 0 v1 stub) |
| Partial immunity (`final *= (1 - reduction)`) | `status.immuneReduction.{tag}` on defender; scales netFactor before potency_floor check |
| Potency floor before roll | When `netFactor <= 0` after partial immunity → **Resisted** (`potency_floor`) — skip sigmoid |
| Category / tag matching for resistance | StatusDef primary category — [status-ssot.md §9.5](../architecture/status-ssot.md) |
| Resistance stacking additive on actor | DerivedComposer + cap in policy |
| Apply-time evaluation | v1 snapshot at Apply — not re-roll every tick unless a future doc opens it |
| Level/realm feeds power magnitudes | **`progression.power`** from RpgProgression — see [actor-core-chaos-mapping.md](actor-core-chaos-mapping.md) |

Key Chaos docs used:

- `05_Status_Core_Core_System_Design.md` — engine apply order
- `04_Status_Core_Configuration_System_Design.md` — resistance cap, duration/damage formulas
- `configs/interactions/resistance_with.yaml` — resistance interaction shape
- `configs/immunity/partial_immunity.yaml` — partial block shape
- [element-core/01_Probability_Mechanics_Design.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/01_Probability_Mechanics_Design.md) — sigmoid apply
- [element-core/06_Implementation_Notes.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/06_Implementation_Notes.md) — Omni rule, `trigger_scale` (reference only)

---

## What we do not port

| Chaos feature | FusionRpg decision |
|---|---|
| YAML config loader / hot reload | **No** — code-first `StatusCatalog` + grant overlay |
| Plugin registry / dynamic load | **Deferred** — optional `IStatusDefProvider` later for modding |
| Element Core bridge / element mastery SQL | Map to **category tags** on Fusion families (`elemental`, `overlay`, …) |
| **Fixed `trigger_scale` as Fusion ApplyScale** | **Fusion divergence:** dynamic `effectiveApplyScale = K × avg(progression.power)` — [actor-hub-ssot.md §5](../architecture/actor-hub-ssot.md) |
| Defender-only `(1 - resist)` multiplier without attacker power | **Replaced** by subtractive delta model |
| Condition Core async status conditions | Hot loop stays sync on injector thread |
| VFX / audio immunity indicators | Lawn chips + debug events only when implemented |
| Server-side status FSM | Ban unchanged — Hot only |
| Chaos intensity ODE (`dI/dt = α·Δ − β·I`) | Note in research only — not v1 |

---

## Two-phase resolve (Chaos → Fusion)

Chaos Element Core separates **probability roll** from **potency**:

| Phase | Chaos | Fusion |
|---|---|---|
| Apply chance | `p = sigmoid((attacker_omni + attacker_elem) - (defender_omni + defender_elem)) / scaling_factor)` | `p_apply = sigmoid(delta / effectiveApplyScale)` where `delta = totalPower - totalResist` |
| Combined with grant | Various refractory models | **`p_final = grant.chance × p_apply`** (default) |
| Potency | Often `base × mastery × (1 - resist)` or intensity dynamics | **`effectiveMagnitude = base × clamp(delta)`** — linear netFactor, not sigmoid |

**Numeric sanity (Fusion stub scale):** delta **0**, ApplyScale **100** → p_apply ≈ **50%**. Delta **1500**, ApplyScale **100** → p_apply ≈ **100%**.

---

## ApplyScale: Chaos fixed vs Fusion dynamic

| | Chaos | Fusion |
|---|---|---|
| Divisor source | Fixed **`status_scaling_factor`** / **`trigger_scale`** (~50–100) per element in YAML | **`max(ApplyScaleFloor, ApplyScaleK × avg(progression.power))`** |
| Progression role | Inflates power/resist in **delta** via mastery | Inflates **delta** and **divisor** via `progression.power` |
| v1 stub | N/A | `progression.power = 1.0` → effectiveApplyScale = 100 |

Do not treat Chaos fixed scale as Fusion product lock. Document divergence in ADR and actor-hub spec.

---

## Layer alignment

```text
Chaos StatusCoreEngine          →  FusionRpg StatusRuntime (L2)
Chaos ImmunityManager           →  ResistanceEvaluator partial/complete (L2b)
Chaos StatusCalculator          →  Two-phase: sigmoid apply + linear potency at Apply
Chaos element_mastery scaling   →  progression.power + status.power.*
Chaos status effect definitions →  StatusCatalog in Core (in-memory)
Chaos Combat Core bridge        →  Instant DamagePacket → Funnel → FA10 (L3/L4)
Chaos fixed trigger_scale       →  NOT ported — see actor-hub ApplyScale
```

---

## Resistance / power keys (actor SSOT)

Content sets values via derived catalog channels on `entity:{ptr}` (plant or zombie):

| Modifier pattern | Example |
|---|---|
| `progression.power` | Hardcoded 1.0 v1; future level/realm curve |
| `status.power.omni` | Summoner-wide or gear baseline |
| `status.power.{category}` | `status.power.dot`, `status.power.cc`, `status.power.contagion` |
| `status.power.{statusId}` | Per-id override when needed |
| `status.resist.{category}` | `status.resist.dot`, … |
| `status.resist.{statusId}` | Per-id override |
| `status.resist.omni` | Uncapped boss knob |
| `status.immune.{tag}` | Complete block for tag |
| `status.immuneReduction.{tag}` | Partial block 0–1 |

Resistance and power are **not** stored on StatusDef payload or grant overlay magnitude fields alone — Apply reads **ActorDerivedSnapshot**.

---

## Telemetry parity (when implemented)

| Chaos | FusionRpg |
|---|---|
| `StatusEffectFailureReason::Immunity` | `debug.status.resisted` |
| Partial apply | `debug.status.partial` with `magnitudeScale`, `durationScale` |
| Probability roll fail | `debug.status.resisted` with `reason: apply_roll` |

---

## Related

- [../architecture/status-ssot.md](../architecture/status-ssot.md)
- [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md)
- [actor-core-chaos-mapping.md](actor-core-chaos-mapping.md)
- [../architecture/combat-damage-ssot.md](../architecture/combat-damage-ssot.md)
- [arpg-effects/04-ailments-status.md](arpg-effects/04-ailments-status.md) — inspiration only
