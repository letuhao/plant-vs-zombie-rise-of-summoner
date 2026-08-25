# Spec — `status-potency`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `catalog-extension` · **Parallel with:** the rest of the band
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Let a status be long-but-weak or short-but-brutal.** It cannot be either today.

[status-ssot.md](../status-ssot.md) §6's Phase 2 scales both axes by one number:

```text
Phase 2: effectiveMagnitude = base × netFactor; effectiveDuration = base × netFactor
```

One knob, two outcomes, permanently locked together. Every status in the 21-id catalog gets longer
exactly as fast as it gets stronger, and no build, item or element can trade one against the other.
This is the single biggest limit on the status balance surface — named in
[../research/chaos-derived-stats-audit.md](../research/chaos-derived-stats-audit.md) §8.1 as the gap a
session is most likely to miss.

**Second objective, cheaper and equally overdue:** `status.resist.{element}` **already resolves** and
nothing reads it (Q1).

---

## 2. What changes

### 2.1 Two deltas instead of one

Phase 1 is untouched — apply chance stays one sigmoid roll over one delta. **Only potency splits:**

```text
durationDelta  = totalPower + status.duration.{…}  − totalResist − status.durationReduction.{…}
intensityDelta = totalPower + status.intensity.{…} − totalResist − status.intensityReduction.{…}

effectiveDuration  = base × netFactor(durationDelta)
effectiveMagnitude = base × netFactor(intensityDelta)
```

Both new pairs are **`Contest`**, resolved as differences, **neither half capped**
([spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.2). `netFactor` is the shipped
`1 + delta/NetFactorScale` from T3.2 — reused, not reinvented.

### 2.2 Byte-identical at zero, by construction

All four new families default to `0`, so both deltas equal today's single `delta`, and both
`netFactor`s equal today's. **Every existing status resolves identically.**

That is the acceptance test, and it is the same proof shape the power program used at `B=0`: land the
structure where the change is arithmetically a no-op, then let content move numbers separately.

The three special cases in §6 survive unchanged and are asserted individually — `delta = 0 → netFactor
= 1.0` for potency; `netFactor ≤ 0 → Resisted (potency_floor)`; partial immunity multiplying
`(1 − immuneReduction)`. **The potency floor now needs an explicit rule:** it fires on the
**intensity** delta only. A zero-intensity status does nothing; a zero-*duration* one is
instantaneous, which is a legitimate effect, not a resist.

### 2.3 Q1 — one term, zero new channels

`status.resist.fire` resolves today through
[DerivedStatRegistry.cs:88-92](../../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs)'s open
prefix. Only the combine rule is short:

```text
totalResist = tierPower × ResistFromPowerRatio
            + resist.omni + resist.{category} + resist.{element} + resist.{statusId}
```

`{element}` comes from the **status def's own element tag**, not the attacker's. Additive with the
others, per §7's omni rule. A burn tagged `fire` sums four already-legal ids.

**Statuses with no element tag contribute nothing** — not a default, a genuine absence, per T5's rule
that a missing value is never invented.

---

## 3. `status-ssot.md` is stale in four places — verified against code

Not part of the objective; found while reading, and this module is the only one that will be here.
**Each was checked against the shipped source, not inferred:**

| §6 says | Shipped | Evidence |
|---|---|---|
| `effectiveApplyScale = max(Floor, K.{category} × matchPower)` | **`matchPower` was dropped** | `ResistanceEvaluator.cs:151` — *"T3.2 (audit F3): no longer scaled by matchPower"* |
| `ResistFromPowerRatio` — *"ratio 0 v1 stub"* | **`1.0`** | `data/tuning/status.v1.json` · T3.1 |
| *"v1 uses hardcoded `progression.power = 1.0` stub until power ADR"* | **`Θ`**, `0` un-hydrated | ADR P1 amended, [actor-hub-ssot.md](../actor-hub-ssot.md) §3B |
| *"v1 stub: `progression.power = 1.0` → `effectiveApplyScale = 100`"* | follows from the above | same |

All four predate the power program. **Correcting them lands here**, not in `unbuilt-reconcile` — that
module handles *unbuilt* specs, and status is shipped (S0–S7). A shipped spec describing retired math
is worse than an unbuilt one: it is read as current.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Status"
dotnet test tests\FusionRpg.Core.Tests        # goldens must not move
.\scripts\guard-stat-pairs.ps1
python scripts\audit-magic-numbers.py --domain status
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Status/ResistanceEvaluator.cs` | two potency deltas; `+ resist.{element}` term |
| `src/FusionRpg.Core/Status/StatusPolicy.cs` | no new constants — reuses `NetFactorScale`, `MinNetFactor`, `MaxNetFactor` |
| `docs/architecture/status-ssot.md` §6 | the split, the Q1 term, **and the four corrections in §3** |
| `docs/architecture/actor-hub-ssot.md` §4 | Phase 2 rewritten to two deltas |

**No new tunable.** Reusing the shipped `netFactor` shape is what keeps §2.2's no-op true — a new
scale constant would move goldens on landing, which T7 forbids in a structural change.

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| **`AllStatusGoldensUnchanged`** | The whole point. All four families at `0` → byte-identical (§2.2) |
| `LongWeakIsExpressible` | `+duration`, `−intensity` → longer, weaker. Fails today |
| `ShortBrutalIsExpressible` | The mirror |
| `DeltaZeroStillOne` | Both deltas honour `delta = 0 → netFactor = 1.0` |
| `PotencyFloorOnIntensityOnly` | Zero **duration** is instantaneous, not `Resisted`; zero **intensity** is `Resisted` (§2.2) |
| `PartialImmunityScalesBoth` | `(1 − immuneReduction)` applies to both deltas |
| `ElementResistRead` | `status.resist.fire` reduces a `fire`-tagged burn; an untagged status is unaffected |
| `UntaggedContributesNothing` | No invented default (T5) |

---

## 7. Boundaries

**Always** — reuse the shipped `netFactor`. Default every new family to `0`. Verify a doc claim against
code before correcting it (§3 did).

**Ask first** — changing Phase 1. Apply chance is one roll over one delta and splitting it is a
different, larger design.

**Never** — use `p_apply` for potency (§6's standing ban). Cap one half of a pair. Multiply omni ×
category. Ship a balance value for the new families — that is a tuning pass.

---

## 8. Success criteria

- [ ] Duration and intensity carry independent deltas.
- [ ] **All status goldens byte-identical** with the new families at `0`.
- [ ] Long-weak and short-brutal both expressible and tested.
- [ ] Potency floor fires on intensity only; zero-duration is instantaneous.
- [ ] `status.resist.{element}` read; **0 new channels**; untagged statuses contribute nothing.
- [ ] All four §3 staleness corrections landed with their code citations.
- [ ] No new tunable constant.

---

## 9. Open questions

**One, inherited and unchanged by this module.** [status-ssot.md](../status-ssot.md) §12 asks whether
derived values should re-evaluate mid-duration or stay snapshotted at Apply (v1: snapshot). The split
makes it **more** visible — a duration buff landing mid-DoT still does nothing — but does not decide
it, and deciding it here would be scope creep into the status stream's own open question.
