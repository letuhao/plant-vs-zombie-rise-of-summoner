# Class system — capability map

**Status: proposed 2026-08-25, awaiting owner review.** No module is authorized to build.

**Design record:** [class-system-ideal.md](class-system-ideal.md) — postures, aptitudes, resources; **free build**
(no player class) and classes as Zomboss patterns, per the owner correction of 2026-08-25.
**Measurement:** [../research/class-rps-balance-2026-08-25.md](../research/class-rps-balance-2026-08-25.md) — the
simulated search, the nine rules, the limitations.
**Proof:** [../research/class-analytic-balance-2026-08-25.md](../research/class-analytic-balance-2026-08-25.md) —
the closed form, its validation, and the invariance theorem.

---

## 1. What this program is for

The owner's framing, and it is the right one:

> Resolve the deterministic part first, then add tuning functions for the primary-stat and
> derived-stat distribution and scale that can be adjusted later. The simulator simulates a real
> fight, where things the math cannot control — RNG, combination, timing — live; those get resolved
> by simulation and statistical learning fine-tuning the tuning config. The simulator is a POC. A real
> system tunes on real data.

That is three layers, and **every module below belongs to exactly one of them**:

| Layer | What it is | Truth source |
|---|---|---|
| **1. Deterministic core** | Allocation → channels → per-round damage distribution → time-to-kill → win probability. Closed form. No RNG, no trials | arithmetic |
| **2. Tuning config** | Every coefficient the core reads. Versioned data, never code | a balance decision |
| **3. Residual fit** | What layer 1 cannot express — depleting pools, action order, party composition, live play. Measured, then fitted back into layer 2 | measurement |

The layers are ordered because **layer 3 is only meaningful once layer 1 exists.** Without a
prediction there is nothing for a measurement to disagree with, and a simulator with no model to
falsify is just an expensive way to produce a number.

---

## 2. Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `unit-class-close` | Fill `unitClass` for the 29 families that carry `null`. Structural, not balance: it is a property of what the formula compares each against | — |
| `aptitude-tuning` | `data/tuning/aptitudes.v{n}.json` + a Core parser + host injection. The whole balance surface, as data | `unit-class-close` |
| `aptitude-resolve` | Aptitude points → derived channels, through the two read functions. Wired into the actor's derived composition | `aptitude-tuning` |
| `deterministic-core` | The closed form, in `FusionRpg.Core`: per-round mixture → first passage → win probability | `aptitude-tuning` |
| `balance-guard` | Balance as a CI assertion, not a periodic exercise. Runs in microseconds because it never simulates | `deterministic-core` · `aptitude-resolve` |
| `point-economy` | Grants per `Θ`, allocation persistence, and **respec pricing** — free build has no class price, so respec cost is the only friction left holding a build together (ideal §7b.5) | `aptitude-resolve` |
| `zomboss-patterns` | Named allocations as **content**, resolved by id like `FactionPolicies.Resolve`. The class layer, moved off the player and onto the AI (ideal §6) | `aptitude-resolve` |
| `residual-fit` | Simulate what the core cannot express, measure the gap, fit the config to close it | `balance-guard` · `point-economy` |

**Build order:** `unit-class-close` → `aptitude-tuning` → { `aptitude-resolve`, `deterministic-core` }
→ `balance-guard` → { `point-economy`, `zomboss-patterns` } → `residual-fit`

No cycles. `aptitude-resolve` and `deterministic-core` are independent of each other and may be built
in parallel — they share only the config.

---

## 3. Why `unit-class-close` is first, and why it is not a balance decision

29 of the 50 catalog families carry `unitClass: null`
([data/seed/derived-stats/catalog.json](../../data/seed/derived-stats/catalog.json)). Twenty of them
are combat families, and **until each has a class, no coefficient anywhere in this program is a
derivation — it is a guess with a measurement attached** (class-system-ideal.md §8.6).

It is first because everything downstream multiplies by it, and it is **not tunable** because the
answer is determined by the formula, not chosen by a designer: a family compared against `baseLong` —
the hit itself — is a magnitude; a family feeding a bounded ratio through a small scale is a contest.
Measured consequence of getting it wrong: matchups fully **invert** across the ladder
([class-rps-balance-2026-08-25.md](../research/class-rps-balance-2026-08-25.md) §3.1).

---

## 4. What is already proven, and what is not

**Proven** (see the analytic record):

- The closed form predicts the simulator to **0.4% mean / 0.7% max** on single-phase fights.
- Win rate is **exactly invariant in `Θ`** — identical from `Θ`=10 to `Θ`=5,000, by homogeneity
  rather than by measurement.
- The closed form can **solve** for a balanced cycle: spread **0.4%** in **2.3 seconds**, against
  2.1% from a simulated search that took orders of magnitude longer.

**Not proven, and each is a named module above, not a hand-wave:**

- ~~Shields move the answer by up to 32 points.~~ **Closed 2026-08-25** by phase decomposition —
  effective HP plus a gate on reflection; residual back to 0.7% with shields live and purchasable
  (analytic record §6.1). `poise` will need the same treatment when it is registered.
- **Regeneration** — neither the model nor the simulator ticks `resource.regen.*` or shield regen.
  They agree, and both understate a regenerating pool.
- **Two thirds of the distribution is unfalsifiable today.** `stamina`/`qi`/`hunger`/`spirit`,
  `skill.cooldown`, `resource.efficiency` and `move.range` all price *actions*, and the action layer
  is not built — a duel spends none of them. Those coefficients are designed, not measured, and the
  config says so in its own `_meta.measurable`.
- Nothing here has met a real player, a party, an action layer or an item. `residual-fit` exists
  because a coefficient fitted against a duel is a hypothesis about the game, not a measurement of it.
- **The distribution itself does not yet pass free build's own test** — see §4a. This is measured, not
  suspected, and `balance-guard` is red on the shipped coefficients by design rather than by oversight.

---

## 4a. Free build — what the owner's correction changes here

**The player has no class** (owner, 2026-08-25). Points go wherever the player wants, at one price.
Classes survive only as **Zomboss patterns** — the `zomboss-patterns` module above.

This is not a subtraction. It **raises** what this program has to prove:

| | With classes | Free build |
|---|---|---|
| What must be balanced | three named allocations against each other | the **whole allocation space** — no build may be a best response to everything |
| What "correct distribution" means | the cycle closes near 65% | **every aptitude is the best point somewhere, and none everywhere** |
| Who enforces build commitment | the class price | **respec cost**, and nothing else |
| Who reads it | `balance-guard` compares three builds | `balance-guard` reads a **gradient** over twelve dimensions |

**Measured against the new bar, the current distribution fails.** `Fortitude` is the best marginal
point for every build against every opponent; 5–7 of 12 aptitudes are dead. Most of the cause is a
coefficient-sizing rule that was never written down — a sigmoid-consumed channel and a
reciprocal-consumed channel authored at the same `k` are not comparable investments
([spec-aptitude-tuning.md](class-system/spec-aptitude-tuning.md) §2.2,
[class-system-ideal.md](class-system-ideal.md) §7b.4).

**None of this changes the module list or the build order.** It changes what `balance-guard` asserts
and what `point-economy` owns, and both were already named. That the correction landed without moving
a dependency arrow is the argument for having drawn them.

---

## 5. Related

- [class-system-ideal.md](class-system-ideal.md) · [power/ssot-power-scale.md](power/ssot-power-scale.md) §4.6
  (PS-3), §11 (PS-8) · [tunables-ssot.md](tunables-ssot.md) · [combat-damage-ssot.md](combat-damage-ssot.md) §6
- [derived-stats-map.md](derived-stats-map.md) · [resource-hub-ssot.md](resource-hub-ssot.md)
- [../../tools/CombatSim/README.md](../../tools/CombatSim/README.md)
