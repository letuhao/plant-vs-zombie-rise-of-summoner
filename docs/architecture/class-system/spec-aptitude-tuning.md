# Spec: `aptitude-tuning` — the class system's balance surface, as data

**Module id:** `aptitude-tuning` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: proposed 2026-08-25, awaiting owner review. Not authorized to build.**

**Depends on:** `unit-class-close` · **Blocks:** `aptitude-resolve`, `deterministic-core`

---

## 1. Objective

Make **every number in the class system a tuning row**, so that a balance pass is a file save rather
than an edit-rebuild-test loop — and so that the deterministic core and the simulator provably read
the *same* configuration, which is what makes their disagreement a measurement instead of a bug.

Three properties, in priority order:

1. **Nothing is a code constant.** Coefficients, read scales and grants all load from
   `data/tuning/aptitudes.v{n}.json` (tunables-ssot.md T1, T3). There are no class prices: **the player
   has no class** (owner, 2026-08-25), so every point costs one point.
2. **One config, two consumers.** `aptitude-resolve` (what the game does) and `deterministic-core`
   (what the math predicts) read one file. If they read two, the residual measures the drift between
   two configs rather than the gap between model and reality, and `residual-fit` becomes meaningless.
3. **A missing key is a rejection naming it, never a default** (T5). A default is a number nobody
   chose that behaves like one somebody did.

**Users:** whoever runs a balance pass; `deterministic-core`; `balance-guard`; the simulator.

**Success is measurable:** turning `read.contest.shareExponentMilli` from `1000` to `1400` must change
the predicted matchup matrix with **no rebuild** — and `balance-guard` must go red or green on the new
value, in milliseconds.

---

## 2. The four blocks, and why the split is the design

```jsonc
{
  "schemaVersion": 1, "version": 1, "_meta": { ... },

  "grant":      { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
  "read":       { "contest":   { "spanPoints": 100.0, "shareExponentMilli": 1000 },
                  "magnitude": { "shareExponentMilli": 1000 } },
  "familyRead": { "combat.power": "magnitude", "combat.accuracy": "contest", ... },
  "edges":      [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 2200 }, ... ]
}
```

| Block | Is | Changing it |
|---|---|---|
| `grant` | the point **economy** | changes how much a player has |
| `read` | the two **scale functions** (PS-3) | changes how a point becomes a number |
| `familyRead` | the **`unitClass` decision** per family | changes which of the two applies |
| `edges` | the **distribution** | changes what an aptitude feeds |

**`familyRead` is one block, not a flag on each edge**, and that is load-bearing. The read mode is a
property of the **channel** — what the formula compares it against — never of the edge that feeds it.
Repeating it per edge invites two edges into the same channel with different modes, which is
unrepresentable in the engine and silently wrong in the config.

**Channels name their source, not the other way round.** 12 aptitudes × 59 families is a 700-cell
matrix nobody can author; the inverted form is sparse and makes *"what feeds this?"* one row
(class-system-ideal.md §7a.1).

### 2.1 The two read functions

```text
contest   value  =  kMilli/1000 · share^gamma_c · spanPoints
magnitude value  =  kMilli/1000 · share^gamma_m · P(Theta)

share = (points in this aptitude) / (points spent across all aptitudes)
```

**`share`, never an absolute count.** Points accrue ∝ `Θ`, so an absolute read makes the *difference*
between two builds grow ∝ `Θ` and saturates every sigmoid — measured: the cycle held only near `Θ`=100
and collapsed to 0/100 by `Θ`=300
([class-rps-balance-2026-08-25.md](../../research/class-rps-balance-2026-08-25.md) §3.1). Reading
share reproduces the property `ssot-power-scale.md` §2 already locks with `BaseAccuracy = 220 + 26L`
against `BaseDodge = 26L`: **level cancels at parity.**

**`spanPoints` is not `stats.accuracyScale`.** `spanPoints` is *how many contest points a whole
allocation is worth*; `accuracyScale` is *how many contest points move a probability*. Two different
concepts that happen to share a value today. Neither may be derived from the other, and neither may be
copied into the other's file (tunables-ssot.md §2 — a copied number is a drift bug with a delay fuse).

**`gamma` is the dial for how much specialising is worth.** At `1.0` a point is a point wherever it
lands. Above `1.0` concentration pays superlinearly; below, spreading pays. It is a power function, so
it is smooth and differentiable and stays inside the closed form — which is precisely why it can be a
dial at all.

> **Consequence a balance pass must know:** at `gamma ≠ 1` the shares no longer sum to 1, so `gamma`
> changes **total output**, not only its distribution. A specialist at `gamma`=1.4 is stronger than a
> generalist by more than the reallocation alone. Intended, and it must be stated on the row.

### 2.2 Sizing a coefficient — against the consumer's SHAPE, not only its scale

**Free build makes this a correctness rule, not a taste one.** With no class gate, a player puts points
wherever they pay most, so any channel whose coefficient is oversized relative to its peers is not a
strong option — it is the only option.

Every contest channel is authored as `kMilli` and consumed by dividing by a scale. Equalising
"scale-units" (`k · spanPoints / consumingScale`) is **not** enough, because the consuming functions do
not have the same shape:

| Consumer | Shape | Marginal, relative to what it controls | Sized by |
|---|---|---|---|
| `accuracy` · `crit.rate` · `crit.damage` | sigmoid `1/(1+e^-x)` | `(1 − p)` — **collapses** toward 1 | how far it moves `p` |
| `reduction` · `amplification` | reciprocal `1/(1+x)` | `1/(1+x)` — **compounds**, never zero | the **total multiplier** at full allocation |
| `penetration` · `absorption` | reciprocal, via `pierceFactor` | same | same |
| `parry.rate` · `block.rate` · `reflect.rate` | linear per-mille, clamped | constant, then flat at the clamp | where the clamp sits |

> **The rule: a full allocation of any one aptitude must deliver a comparable TOTAL effect, whatever
> consumes it.** A full allocation of `accuracy` moves damage by about ×1.9 (p 0.5 → 0.95). A full
> allocation of `reduction` at the shipped `kMilli: 300` delivers **×0.25 — a 4× swing** — and
> `penetration` at `kMilli: 1000` delivers **×0.09**. Those are not three options; they are one option
> and two decorations.

Measured 2026-08-25 ([class-system-ideal.md](../class-system-ideal.md) §7b.4): the shipped coefficients
make `Fortitude` the best point against every opponent for every build, with 5–7 of 12 aptitudes dead.
Resizing the three reciprocal-consumed channels to a ×1.9-comparable total takes the best marginal from
**+3.56% to +1.67%** and revives several — most of the gap, not all of it (§8.7 there keeps the residue
open).

**This is not an argument for capping the reciprocal shapes.** They exist because
`max(0, 1 + d/s)` reaches exactly zero and confers total immunity (decisions.md, *Combat mitigation
shapes*), and PS-8 forbids a hard ceiling. The shape is right. What was missing is this sizing rule,
and its natural home is a **guard**, not a convention — `balance-guard` asserts it.

---

## 3. Commands

```powershell
# Republish a new version. Never hand-edit (T4).
python tools\tuning\publish.py aptitudes read.contest.shareExponentMilli=1400 --publish

# Audit: no balance literal may remain in code
python scripts\audit-magic-numbers.py --domain aptitudes

# Tests
dotnet test tests\FusionRpg.Core.Tests --filter AptitudeTuning

# POC, ahead of the build: the same config shape, driven by the simulator
cd tools\CombatSim
dotnet run --no-build -- predict -a force-ns,finesse-ns,bastion-ns --theta 100 -n 4000
dotnet run --no-build -- search --analytic -m aptitudes.v1 -a force-ns,finesse-ns,bastion-ns --theta 100
```

---

## 4. Project structure

```text
data/tuning/aptitudes.v1.json                          the config (shipped home)
src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuning.cs   the record — no I/O, no defaults
src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuningLoader.cs   pure parser over a string
src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuningHub.cs      Configure(...) / Current
tests/FusionRpg.Core.Tests/Stats/Aptitudes/AptitudeTuningTests.cs
tools/CombatSim/tuning/aptitudes.v1.json               the POC copy, until this module ships
```

**`Core` reads no file — hosts load and inject** (tunables-ssot.md §7.2). This is exactly the shipped
shape: `FusionRpg.Server/Program.cs:47-49` already does
`CombatPolicy.Configure(CombatTuningLoader.Parse(File.ReadAllText(...)))`, and the injector does the
same from its plugin folder. No new architecture and no new seam.

---

## 5. Code style

Copy `CombatTuning.cs` / `CombatTuningLoader.cs` verbatim in shape — a `sealed record` of values, a
static `Parse(string json)` with no file access, and a rejection type that names the missing key:

```csharp
/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class AptitudeTuningLoader
{
    public static AptitudeTuning Parse(string json)
    {
        ...
        var familyRead = FamilyRead(root);      // rejects an unknown mode, never defaults
        var edges = Edges(root, familyRead);    // rejects an edge whose family has no entry
        return new AptitudeTuning(...);
    }
}
```

**Units in every name** (T6): `kMilli`, `shareExponentMilli`, `spanPoints`.
`kMilli` is per-mille so the whole config is integer and diffable; the `/1000.0` happens once, at
parse.

**`long` for anything that reaches a magnitude** — an edge coefficient multiplies `P(Θ)`, which is
quadratic. `double` for the share and the exponent only, which are bounded ratios.

---

## 6. Testing strategy

`tests/FusionRpg.Core.Tests`, xUnit, constructed inline — no fixture files (§7.2).

| # | Test | Asserts |
|---|---|---|
| 1 | `Missing_key_is_rejected_naming_it` | every required key, one case each. **No default is ever produced** |
| 2 | `Unknown_read_mode_is_rejected` | `"familyRead": {"combat.power": "sigmoid"}` throws naming the value |
| 3 | `Edge_with_unclassified_family_is_rejected` | an edge whose family has no `familyRead` row throws. This is the `unitClass: null` blocker, made unrepresentable |
| 4 | `Every_edge_channel_is_registered` | resolved against `DerivedStatRegistry.CreateDefault()` — a typo'd channel cannot silently read zero |
| 5 | `Shipped_config_parses` | `data/tuning/aptitudes.v1.json` round-trips. A malformed ship is a red test, not a runtime surprise |
| 6 | `Contest_read_is_theta_free` | the contest function's output does not depend on `Θ`. **The invariance theorem's first premise, as an assertion** |
| 7 | `Magnitude_read_is_proportional_to_P` | doubling `P(Θ)` doubles the value. The second premise |
| 8 | `Gamma_one_is_linear_in_share` | two aptitudes at 0.5 equal one at 1.0 when `gamma` = 1 |
| 9 | `No_aptitude_is_mandatory_or_dead` | the free-build condition, over the shipped config: every aptitude is the best marginal point against some opponent, and none against all of them. **Red on the shipped coefficients today** (§9.3) — it belongs to `balance-guard`, and it is listed here because this file is what it reads |

Tests 6 and 7 are the ones worth arguing for: they are the premises of the proof in
[class-analytic-balance-2026-08-25.md](../../research/class-analytic-balance-2026-08-25.md) §3, and if
either fails the invariance is gone whether or not any matchup looks wrong.

---

## 7. Boundaries

**Always**

- Add a tuning row rather than a `const`, whenever both readings are defensible (tunables-ssot.md §1).
- Reject a missing or unknown value naming it.
- Keep `familyRead` and `data/seed/derived-stats/catalog.json`'s `unitClass` **in agreement** —
  `unit-class-close` fills the catalog; this file must not disagree with it.
- State the unit in the key name.

**Ask first**

- Changing `read.contest.spanPoints` — it rescales every contest at once and moves every matchup.
- Adding a fifth block. Four is the whole surface today; a fifth means a concept nobody has named.
- Any change to `familyRead` after `unit-class-close` ships — it is a structural claim about a
  formula, not a balance choice.

**Never**

- A default for a missing key.
- A balance number in `Policy` / `Catalog` / `Rules` / `Ruleset` / `Math` (T3).
- Hand-editing a published version (T4) — republish `v{n+1}` and keep the old file for revert.
- Landing a refactor and a rebalance together (T7) — a golden that moves must be attributable to
  exactly one of them.
- A per-edge read mode. §2.

---

## 8. Success criteria

1. `data/tuning/aptitudes.v1.json` exists, parses, and both hosts inject it at composition root.
2. `python scripts/audit-magic-numbers.py --domain aptitudes` reports **zero** M1/M2 findings.
3. Every one of the eight tests in §6 passes.
4. Moving a coefficient in the file and restarting changes the predicted matrix — **no rebuild**.
5. `deterministic-core` and `aptitude-resolve` are both wired to `AptitudeTuningHub.Current`, and a
   test asserts they resolve the same channel values for the same allocation.

---

## 9. Open

**9.1 `gamma` starting value — and it now carries more than it did.** `1.0` is what the proof was run
at and what the measured cycle used. Under free build `gamma` is the *only* thing making specialising
pay differently from spreading, because the class price that used to do that job is withdrawn
([class-system-ideal.md](../class-system-ideal.md) §7a.3). Recommend shipping `1.0` — neutral between
specialist and generalist, letting the matchup decide — and treating any change as a reviewed balance
pass. The dial existing is the point; moving it can wait.

**9.2 ~~Where the class → posture mapping lives~~ — CLOSED by free build.** `price` needed to know an
aptitude's posture. There is no price, so this file needs no posture mapping at all. Posture survives
as **vocabulary for humans and a shape for Zomboss patterns**, not as a lookup this module performs.

**9.3 Whether the shipped coefficients may go in as-is.** They may not, and this is the one open item
that blocks a *balance* claim rather than a *build* one. §2.2 measured that the shipped `kMilli` values
make `Fortitude` mandatory and leave 5–7 of 12 aptitudes dead under free build. The module can be built
on them — the schema and loader are indifferent to the numbers — but **`balance-guard` will be red on
day one**, and that is the correct outcome rather than a reason to delay. Do not silently retune while
extracting; T7 forbids landing a refactor and a rebalance together.

---


## 10. Design-gate checklist

```
[x] Subsystems identified: stats, tunables, power scale, combat damage, caps.
[x] Read this session: DESIGN-GATE.md, tunables-ssot.md, ssot-power-scale.md (§3, §4, §9, §11.6),
    design/spec-magnitude-and-units.md, decisions.md (power/caps/magic-number/mitigation rows),
    class-system-ideal.md (§4, §7a, §8.5b/c), class-rps-balance-2026-08-25.md.
[x] decisions.md checked — the Power scale, Caps, Magic numbers and Combat mitigation shapes rows
    all bear on this; none is contradicted.
[x] Every factual claim cites file:line or a document section.
[x] Verified against CODE, not comments — OverlayCombatCalculator.cs:60-300,
    CombatDamageDispatcher.cs:70-123, OverlayCombatMath.cs:42-43, ClampedContest.cs,
    CombatTuning.cs, Server/Program.cs:47-49, catalog.json (29 nulls counted, not quoted).
[x] Read the surrounding section of every rule quoted (PS-3 from §4.6, T5 from §3, §7.2 in full).
[x] Constraints tested, not assumed. The claim "the closed form predicts the simulator" was RUN:
    0.4% mean / 0.7% max over 6 arrows at 4,000 duels each. The claim "shields break it" was RUN:
    19.6% mean / 32.0% max. The reflect-bypass claim was checked against the code after the
    cross-check disagreed, not asserted from it.
[~] Nothing contradicts a §2 invariant. PARTIAL — invariant 11 (no hard ceilings) deserves a note:
    `share` is bounded [0,1] by construction, which is a bounded RATIO (PS-8 exempt), not a cap on
    progression. Absolute points remain uncapped; what is bounded is the fraction of one's own
    allocation, which cannot exceed all of it. Stated rather than assumed.
[~] Corrections propagated. The map, this spec and the analytic record land together. The POC config
    under tools/CombatSim/tuning/ is NOT the shipped file and says so in its _meta.
```

---

## 11. Related

- [class-system-map.md](../class-system-map.md) · [class-system-ideal.md](../class-system-ideal.md) §7a
- [../tunables-ssot.md](../tunables-ssot.md) · [../power/ssot-power-scale.md](../power/ssot-power-scale.md) §4.6
- [../../research/class-analytic-balance-2026-08-25.md](../../research/class-analytic-balance-2026-08-25.md)
- [../../design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 — the ten-class ledger `familyRead` must agree with
