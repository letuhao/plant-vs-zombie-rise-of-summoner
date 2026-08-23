# Implementation Plan — power program

**Scope:** everything defined in the 2026-08-23 design session — the power ladder, the migration of
every consumer onto it, the caps standard, the numeric-overflow standard, the magic-number standard,
and the doc reconciliations for features not yet built.

**Status:** Phase 0, M and D **authorized and unblocked**. Phases 1–4 pending owner review of the map and specs — a review gate, not a dependency. **No open questions.**
**Task list:** [power-todo.md](power-todo.md) · **Map:** [../docs/architecture/power-map.md](../docs/architecture/power-map.md)
**SSOT:** [power/ssot-power-scale.md](../docs/architecture/power/ssot-power-scale.md) ·
[tunables-ssot.md](../docs/architecture/tunables-ssot.md) ·
**Audit:** [power/audit-2026-08-23.md](../docs/architecture/power/audit-2026-08-23.md) (12 findings)

> `tasks/plan.md` and `tasks/todo.md` belong to the **perf** stream. This program uses the prefixed
> pair per [AGENTS.md](../AGENTS.md).

---

## Overview

Three incompatible level curves ship today. This program replaces them with one index `Θ` and one
function `P(Θ)`, migrates every consumer, and installs guards so the drift cannot return. Four
standards fall out of it and are now locked in `decisions.md`: **one power ladder**, **no hard
progression ceilings**, **magnitudes are `long`**, **the balance surface is data**.

Six workstreams, three of which run in parallel:

| Stream | Phases | Gated on |
|---|---|---|
| **Overflow** | Phase 0 | nothing — in progress |
| **Ladder** | Phases 1–4 | owner *review* of map + specs |
| **Magic numbers** | Phase M | Phase 0 (shares the widening work) |
| **Doc reconciliation** | Phase D | nothing — no code |

---

## Architecture decisions

**1. `B = 0` first, dial second.** `P(Θ)` at `B = 0` *is* `BattleRuleset.BaseHp`. Every consumer
migrates at `B = 0` where the change is arithmetically a no-op, and `B` is turned up in one separate
commit. **If refactor and dial land together, every moved golden is ambiguous** between "the refactor
broke something" and "the dial did its job." This single decision shapes the whole phase order.

**2. Phase 1 ships inert, and that is deliberate.** `power-ladder` and `power-index` land with **no
callers**. That looks like horizontal slicing, and normally would be. Here it is the point: the ladder
can be reviewed and proven against shipped behaviour before anything depends on it.

The vertical proof is pulled into **Checkpoint 2** rather than dropped: *hp, atk and defense travel
Θ → P(Θ) → `BattleRuleset` end to end, with zero goldens moved.* That is a complete path, verified;
the task granularity underneath it is horizontal only because the alternative is an XL task.

**3. `status-contest` lands in two commits, ratio first.** `ResistFromPowerRatio = 1.0` alone fixes
matched pairs at every level **regardless of curve shape**. It makes the system safe to look at while
the curve change is in review. The curve alone would not: at Θ=12 vs 11 under `2^L` the gap is still
2,048.

**4. The soul earn formula is specified here, so `caps-reconcile` blocks on nobody.** An earlier
draft parked this on the economy stream. The reasoning was right — deleting the caps bare reproduces
the `+2`/kill incident — but parking it was the lazy conclusion. The formula is **today's constants
multiplied by `contentScale`** (SSOT §11.7a): `contentScale(20) = 1.000`, so it is byte-identical at
the calibration point, needs no balance decision from anyone, and scales with cost thereafter.
Cap-deletion and value-scaling land in the same commit, so there is no inflation window.

**5. Magic numbers migrate a domain at a time, values unchanged.** A migration that also retunes is
unreviewable (T7). Extract to config, prove byte-identical, tune separately.

**6. Doc reconciliation is not code.** Enhancement `+X`, rarity promotion and PvZ drop caps are
**unbuilt specs**. Reconciling them now is free; after they ship it is a rebalance. Phase D runs any
time and blocks nothing.

---

## Dependency graph

```text
Phase 0  overflow standard + audit + widening
   │        (P0.4's int->long widening is shared with Phase M)
   ▼
Phase 1  power-ladder ──► power-index
   │         │               │
   ▼         ▼               ▼
Phase 2  battle-magnitude  battle-rates   content-authoring     [all at B=0]
   │         └──────────────┴───────────────┘
   ▼                        │
Phase 3  status-contest   content-scale ──► caps-reconcile (+ earn formula, same commit)
   │         └──────────────┴───────────────┘
   ▼
Phase 4  power-guard ──► power-dial            [the only golden-moving change]

Phase M  contracts ──► loam/world ──► souls/patron ──► rest ──► vfx ──► CI
         (parallel with 1-4 after Phase 0)

Phase D  enhancement · rarity · PvZ drops · ssot-generation §4.1
         (parallel, no code, no dependencies)
```

**No external dependencies.** `Wm = 5` is derived from the shipped `SectorTypeCatalog`; the earn
formula is SSOT §11.7a and ships with the deletion; §10.4 is decided. The world program and the
economy stream may each revise a tuning weight they own — welcome, not owed.

---

## Phases

### Phase 0 — numeric overflow *(in progress)*

The ladder is quadratic, so type choices made when magnitudes were flat are now wrong. Widening
*after* consumers migrate means doing it twice, the second time against goldens that have moved.

Measured thresholds: `float` breaks at **Θ 232**, `int` per-mille at **3,213**, `int` whole at
**103,557**, `long` at **214,748,300**.

Audit baseline: **0 critical, 92 A3** (`int` whole-unit magnitudes), 14 A7 (architectural `double`).
The first tool run reported 121 critical findings, all false positives — the precision gate is the
lesson from that.

### Phase 1 — the ladder *(inert)*

`PowerLadder.Value(Θ)` and `IPowerIndexProvider`. Zero callers, zero behaviour change.

### Phase 2 — adoption at `B = 0` *(zero goldens move)*

Every consumer reads the ladder. Byte-identical output. **The proof the whole program rests on.**

### Phase 3 — fixes and new consumers *(goldens move, knowingly)*

The two P1 defects, item scaling, and the caps reconciliation — including the soul earn formula,
which ships in the same commit as the cap deletion so there is no window between them.

### Phase 4 — seal

The guard that stops drift returning, then the dial.

### Phase M — magic numbers *(parallel)*

329 findings across 37 balance-surface files. `contracts` first (34, self-contained, already touched
by the `MaxSlots` removal); `vfx` last (68, largest count, lowest stakes).

### Phase D — doc reconciliation *(parallel, no code)*

Four unbuilt-feature specs to correct before they ship.

---

## Checkpoints

| # | After | Gate |
|---|---|---|
| **0** | Phase 0 | `audit-overflow.py` exits 0 with A3 = BOUNDED-only · triage doc complete · **no golden re-blessed** · A7 decided · audit blocking in CI |
| **1** | T1.1–T1.4 | `B=0 → A=30`; `P(20)=680` for every legal `B`; odd `B` rejected; closed form ≡ iterated sum to Θ=2000; `Explain ≡ ActorIndex`; `Wf = Wa` enforced |
| **2** | **Phase 2 — the vertical proof** | **hp/atk/defense travel Θ → P(Θ) → `BattleRuleset` end to end.** All three exact vs shipped formulas across `[0,5000]`. `P(hit)` parity `0.90±0.02` holds to **Θ=10,000**. PS-3 tripwire passes. **`git status tests/` clean** |
| **3a** | T3.1 | Matched pair contests at `delta = 0` at every Θ — **even under the un-retired curve** |
| **3b** | T3.2–T3.4 | Red test flips `netFactor 4096 → 1.0`; `delta` antisymmetric; corpus byte-identical at `Θc=20`; every moved golden attributed |
| **3c** | T3.5–T3.6 | Bounds derived and throwing; soul bound dynamic; slot 512 purchasable; **stall-farm regression green on the new formula** |
| **4** | Phase 4 | Guard fails on planted violations · dial moves **zero rate goldens** · nothing at the pin moves · `v1` revert proven |
| **M** | per domain | Domain's numbers in config, **behaviour byte-identical**, audit clean for that domain |
| **D** | Phase D | Four specs corrected; no code touched |

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| A moved golden is ambiguous between refactor and rebalance | **High** | Decision 1 — `B=0` adoption and the dial are separate commits. Checkpoint 2 asserts `git status tests/` clean |
| `int → long` ripples through DTOs and serialized shapes | **High** | P0.3 triages before P0.4 widens. `Contracts/` findings get their own hash check; `BattleSetup` field changes move all four expedition hashes (`decisions.md:42`) |
| Soul caps deleted before the scaling formula → inflation | **High** | Impossible by construction — SSOT §11.7a's formula lands in the **same commit** as the deletion, and it is a no-op at Θ=20. The regression test asserts **souls-per-minute**, which is what the original incident actually measured |
| A rate golden moves during `power-dial` | **High** | It means `battle-rates` has a PS-3 violation. **The module stops.** Only visible because Phase 2 landed at `B=0` first |
| `Wm` never arrives from the world program | Medium | `WmMilli: null` loads fine and throws only when `ContentIndex` first needs it. Phases 0–2 unaffected |
| Magic-number migration silently retunes | Medium | T7 — extract with values unchanged, prove byte-identical, tune separately |
| `power-guard` G2 over-matches and becomes noise | Medium | False-positive survey **before** arming; allowlist entries each carry a reason; fail closed, never warn |
| Phase M's 329 findings stall on volume | Low | One domain at a time; `vfx` (68, lowest stakes) is last, not first |

---

## Open questions

**None.** Every question this session raised is closed:

| Was open | Closed by |
|---|---|
| `caps-reconcile` sequencing | SSOT §11.7a — the formula is specified, so nothing to sequence against |
| Soul earn formula | §11.7a — `KillDelta × contentScale`, constants unchanged |
| `Wm` | `5` (§5.3), derived from the shipped `SectorTypeCatalog` bands 0–6 |
| SSOT §10.4 economy | **Decided** — loam stays `Θ`-invariant, souls scale on `P(Θ)` |
| ADR P1 amendment | **Written** into `decisions.md`, marked pending build |

**One review gate remains, and it is not a blocker:** owner approval of the map and the ten specs,
before Phases 1–4 build. Phases 0, M and D are authorized and need nothing.

Two inputs are still *welcome* rather than *owed*: the world program can confirm or move `Wm` (a
weight in a tuning file either way), and the demon/economy stream can retune the soul constants it
already owns (they are unchanged, so silence is a valid answer).
