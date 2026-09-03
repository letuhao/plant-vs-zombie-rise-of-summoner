# Spec: power-sweep (E44)

**Status: DRAFTED 2026-09-03** — owner decision: *"keep gate but pass it if need, maybe consider fine
tune for balance later, we cannot avoid tuning in this game, so that is normally."* Module **E44**,
effect-atom. **Research work with a deliverable, not a code module.** Depends on **E9** (built).

**What it owns: the fitted coefficients E9 was always scheduled for, and closing D2.** It is the only
thing standing between decision **C1** and its enabling — and it has been named as *"blocked"* by every
document that mentions it while being owned by none.

---

## 1. Why this exists, and the owner's framing

`effect-atom-map.md` §8 records both halves as open: *"Both E9's **coefficients** and its **function**
remain open."* The defect log is more precise:

> *"Pricing multiplicative effects has no closed-form answer — every linear cost function makes the
> marginal read inert, and a nonlinear one has to be **fitted by a simulation sweep**, which is exactly
> what E9's coefficients were always scheduled for. **Treating it as a design gate was a mistake; it is a
> research task with a known home.**"*

**The owner's framing sharpens this and is binding here:** the gate stays, **but it may be passed
deliberately**, and *"we cannot avoid tuning in this game, so that is normal."*

**So E44 is not a blocker to be waited on. It is the work that replaces guessing with measurement**, and
until it lands the gate is passed with an owner's decision rather than by accident.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `CostFunction.Price(AtomRow)` — 5-category vector, integer-exact | **built** | D1/D3/D4 all closed in code |
| The conditionality chain incl. predicate pricing | **built** | `PredicatePricer` |
| `ContentValidation.Drift` (±25%) | **built**, production-shaped | — |
| **All 20 coefficients flat at `CoeffMilli = 1000`** | ⛔ **unfitted** | `CoefficientTable.cs:125-147`; only `ReferenceScale` (10/2/25/1) varies |
| `power_coefficient` as a **data** table with a sweep-written proposal side table and a drift-reporting test | **built** — *"hand-authored now, fitted later"* is **mechanically possible**, not aspirational | E9's own map row |
| **D2 — multiplicative pricing** | ⛔ **open** after two failed attempts | `definitions.md` §13 |
| `ActorPowerCache.Compose` is additive | **built**, and knowingly wrong | `CostFunction.cs:30-35` — *"knowingly wrong on multiplicative pairs, by design"* |

**The infrastructure for the answer already shipped.** What is missing is the measurement.

---

## 3. Why the two prior attempts failed — read before proposing a third

Recorded so this module does not re-derive them:

1. **The marginal read** (*"stored power stays context-free, AI reads marginal instead"*). **Inert.**
   `actorPower = Σ atom.power`; a sum has no cross terms, so `marginal = Σ_{A∪{x}} − Σ_A = p(x)` for
   **every** actor. The marginal read returns exactly the number it was meant to improve on.
2. **Aggregate channel totals, then price.** **Also inert.** `normalize` is linear, so
   `price(Σm) = Σ price(m)`. Worse, crit rate and crit damage are on **different** channels, so each total
   *is* one atom's magnitude and there is no composition at all.

**Both failed for the same reason: a linear price function cannot see a multiplicative interaction, and
neither attempt introduced non-linearity.** A third attempt that does not is already refuted.

---

## 4. The deliverable

### 4.1 A fitted coefficient set

`power_coefficient` rows written from a **simulation sweep**, replacing the flat 1000s, with the sweep's
inputs and date recorded so any coefficient traces to the run that set it. The proposal side table and
the drift test already exist for exactly this.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the coefficient data path is a **seed kind**, `data/seed/power/coefficients.v1.json`

**This is the canonical statement.** `spec-projectile-control.md` (E37), `spec-entity-fields-12plus.md`
(E38), `spec-spawn-non-grid.md` (E40) and `spec-ui-attach-point.md` (E41) all require a new coefficient
row, and **every path they offered was one this repo forbids.** Four modules were blocked on a question
none of them owned. It is answered here, once.

**What was wrong with the options on the table:**

| Option the specs named | Why it fails |
|---|---|
| *"Coefficients live in `data/tuning/effects.v1.json`"* (E37 §5, E38 §2c/§3, E40 §5) | **That file has no coefficient section** — its keys are `matchupRead` and `damageFxFloater`, nothing else. And **`CoefficientTable` never reads `data/tuning/` at all**: the only reader is `RpgStore.GetPowerTables` over the `power_coefficient` table (`RpgStore.Power.cs:61-72`; the empty-table fallback is `:68`) |
| *Edit `CoefficientTable.Authored()`* | Forbidden by the table's own design note, and for a reason that is not stylistic: *"Data rather than a constant, deliberately… as a code constant it would move every golden with **no content-hash change** — the one outcome E8 exists to prevent"* (`CoefficientTable.cs:17-20`) |
| *Call `RpgStore.UpsertPowerTables`* | It has **zero production callers**, and its own comment says so (`RpgStore.Power.cs:105-107`). A whole-table replace with no caller is not a data path |

**And `data/tuning/` could never have been the answer**, which is the part worth stating plainly:
`power_coefficient` has been a **content-hashed table since registry V3**
(`ContentHashRegistry.cs:148-160`, four hashed columns). `data/tuning/*.json` is not hashed by anything.
Putting coefficients there would reproduce the exact defect `CoefficientTable.cs:17-20` names — a
number that moves every golden while the content stamp stands still.

**The decision:**

```jsonc
// data/seed/power/coefficients.v1.json
{ "schemaVersion": 1, "kind": "power-coefficient", "entries": [
  { "kindId": "stat.derived", "channel": "combat.dodge.fire", "coeffMilli": 1000, "referenceScale": 1 }
]}
```

Four connections, each mirroring one that already exists for atoms:

1. `SeedContent` gains `Coefficients` — beside `Atoms`, `Containers`, `Affixes` (`AtomSeedFile.cs:53-74`).
2. `AtomSeedFile.TryKind` gains `"power-coefficient"` (`:457-471`) and a `ReadCoefficient` mirroring
   `ReadChannelPolicy` — the closest existing sibling, also a flat row type.
3. `SeedScanner.OwnedFolders` gains `power` (`SeedScanner.cs:14-15`).
4. `ImportContent` writes them through the **already-built** `WritePowerTablesUnlocked`
   (`RpgStore.Power.cs:114`), inside its existing single transaction and single revision bump —
   reusing the writer that `UpsertPowerTables` calls, which is why this needs no new SQL.

**Every property that made the other options wrong is satisfied by construction:**

- **Content-hashed.** It lands in `power_coefficient`, already in the hash registry — a coefficient
  edit moves the stamp, which is what E8 exists for.
- **Not a code constant.** `CoefficientTable.Authored()` is untouched and stays what it already
  documents itself as: the **no-database fallback**. `GetPowerTables` returns it when the table is
  empty (`RpgStore.Power.cs:68`), so deleting the seed file restores today's behaviour exactly.
  **That is what makes this reversible.**
- **Not a new mechanism.** Every piece is shipped: the table, the writer, the reader, the fallback, the
  hash entry, the drift test. **The only missing link was the seed reader**, which is the same link
  E32 is closing for affixes and E30 for channel pools — one more row type through a path that already
  carries seven.
- **A sweep still cannot touch it.** §4.1's *"a sweep writes proposals and never touches what ships"*
  is unchanged: the sweep writes `power_coefficient_proposal`, a human copies an adopted proposal into
  the seed file, and the import writes it. That is *"hand-authored now, fitted later"* with a file to
  hand-author in.

**What would overturn it:** a decision that coefficients should not be player-visible content at all —
that they belong to the build rather than the catalog. Then the answer is a generated C# table with a
`--check` gate, the `DemonSpeciesGen` shape, and `power_coefficient` leaves the content hash. Nothing
in the repo argues for that today, and `ContentHashRegistry` V3 argues against it.

### 4.2 A non-additive composition for the pairs that need it

**Not a general nonlinear price** — the narrow thing D2 actually needs. The known multiplicative pairs:

- **crit rate × crit damage** — different channels, no composition today
- **the element ring** — 28 families over 7 slots
- **shield layers** — capacity × toughness × pen × regen

**Success is measurable and stated: `marginal(x, A)` must differ by `A` for at least these pairs.** If it
still equals `p(x)`, the attempt has failed the same way as the first two, and **that must be reported,
not adjusted around.**

### 4.3 The generated corpus is the sharp edge, and it is the reason for urgency

**The atom corpus generates exactly the shape D2 mis-prices** — `keen_edge` and `cruelty` are both
designed families, the element ring is 28 of them, shield layers are 4. So the fitted set is not a
refinement of a working number; it is the difference between a budget that means something across the
bulk of the corpus and one that does not.

---

## 5. What this module must NOT do

- **Block anything.** Per the owner: the gate may be passed. E44 replaces a guess with a measurement; it
  does not hold the wave.
- **Claim a balance result is final.** *"We cannot avoid tuning in this game."* A fitted set is a better
  starting point, not an end state — and the drift test exists because it will move again.
- **Re-run a refuted attempt** (§3) without introducing non-linearity.
- **Change `CostFunction`'s integer contract.** Per-mille, `long`, widen before multiplying, divide by
  1000 last, overflow throws. A sweep that needs floats has the wrong output type.
- **Fit against synthetic data alone.** `RungMonotonicity` already prices one synthetic vector and proves
  nothing about real content — that is the mistake to avoid repeating at scale.
- **Silently widen `ContentValidation.Drift`'s ±25%** to accommodate a poor fit.

---

## 6. Verification

| # | Check | Proves |
|---|---|---|
| 1 | `marginal(x, A)` **differs by `A`** for crit rate × crit damage | D2 is actually closed, by the test both prior attempts failed |
| 2 | Same for the element ring and shield layers | Not a single-case fix |
| 3 | Every fitted coefficient traces to a recorded sweep run | *"Hand-authored now, fitted later"* becomes *"fitted, and here is by what"* |
| 4 | Re-running the sweep on the same inputs is **reproducible** | It is a measurement, not an opinion |
| 5 | Pricing stays integer-exact; overflow **throws** | The numeric contract survives fitting |
| 6 | `ContentValidation.Drift` still passes at **±25%**, unwidened | The fit did not move the goalposts |
| 7 | **A planted degenerate pair is priced above the sum of its halves** | The non-linearity is real, not nominal |

---

## 7. Acceptance criteria

0. The coefficient data path exists — `data/seed/power/coefficients.v1.json` imports into
   `power_coefficient` (§4.1, decided 2026-09-03). **Without it there is nowhere to put a fitted
   number**, and this was the unstated prerequisite of every earlier attempt.
1. Coefficients are fitted from a recorded, reproducible sweep, replacing the flat 1000s.
2. `marginal` differs by context for crit rate × crit damage, the element ring and shield layers.
3. Each coefficient traces to its sweep run.
4. The integer contract is unchanged; overflow throws.
5. Drift tolerance is not widened.
6. **C1's family-access widening becomes enableable** — and enabling it stays a separate, explicit
   decision, not a side effect of this module landing.
7. **If the fit fails**, that is reported with the evidence — a third refuted attempt recorded beside the
   first two is a real outcome, not a failure to deliver.

---

## 8. Dependencies

| | |
|---|---|
| **E9** `power-vector` | **BUILT.** This module fits its coefficients; it does not rebuild it |
| **A-G1** `tier-access-gate` | Opens two of C1's three gates. **E44 opens the third** |
| **C1** | An owner decision that stays gated meanwhile — **and the gate may be passed deliberately**, per the owner |
| **E43** `family-expand` | Its ~490 rows are the first real content to price at scale — **the natural fitting corpus** |
| **`definitions.md` §13 D2** | The open record. This module closes it or adds a third refuted attempt to it |
