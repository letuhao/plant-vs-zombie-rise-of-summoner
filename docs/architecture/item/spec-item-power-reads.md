# Spec: `item-power-reads`

**Module id:** `item-power-reads` · **Program:** [item](../item-map.md) · **Build order:** 9 of 21
**Depends on:** `rarity-bands` (module 7), `affix-legality` (module 8), **X6 — `E44 power-sweep`** (effect-atom)
**Rescoped:** owner audit 2026-09-03 — **D13 is VOID** ([item-ideal.md](../item-ideal.md) §2f.2)

> ⚠ **Renamed 2026-09-04.** This module was `item-power-reads`, which **collides with a shipped module id**:
> effect-atom **E10 `item-power-reads`** (`../effect-atom/spec-item-power-reads.md`, BUILT 2026-08-22) — and this
> module's whole job is *consuming* E10. `AGENTS.md` makes module ids stable and referenced by every
> downstream plan, so two programs cannot share one.

## Objective

Answer the four power questions the item program actually has, by **calling E9**. Three lanes and one
ruling block on a power number and have blocked since 2026-08-22; none of them needs a power *model*,
because one shipped that day.

| # | Read | Asked by |
|---|---|---|
| **R1** | Is this base type's implicit within its budget share? | I3 — `ssot-item-categories.md:177-180`, open Q7 at `:803-804` |
| **R2** | What does a granted action cost against the item's budget? | G4 — `ssot-granted-actions.md` §10 Q6 |
| **R3** | What power number does the card show, and with what honesty band? | G3 — `ssot-presentation.md` §3.3 Rule P, open Q7 |
| **R4** | What does an aptitude affix cost? | **D8**, as amended 2026-09-03 (share delta, not points) |

**This module builds no curve, no cost function and no vector.** It is four call sites, one tuning file
and the honesty rules that keep an approximate number from reading as an exact one.

## Design

### ⛔ D13 is VOID — E9 shipped 2026-08-22, and the lanes that say otherwise are stale

> D13 read: *"E9, the power model, is in scope: the item program builds it."*
> It was answered by a platform fact nobody checked. **Verified this session, file by file:**

| Piece | Evidence |
|---|---|
| The module is **BUILT** | `effect-atom-map.md:82` — *"**E9** `power-vector` — **BUILT 2026-08-22.**"* |
| The five-category vector | `src/FusionRpg.Core/Effects/Atoms/Power/PowerVector.cs:18-19` — `readonly record struct PowerVector(int Offense, int Survivability, int Control, int Utility, int Economy)` |
| Per-atom pricing | `Power/CostFunction.cs:37,47` — `CostFunction.Price(AtomRow, PowerTables?, int depth, Func<string, ChannelPoolRow?>?) → PricedAtom` |
| Composition + memoisation | `Power/ActorPowerCache.cs:39,53,109` — `Of`, `Compose`, `PriceBody` |
| The display scalar and the two conditioned reads | `Power/PowerReads.cs:30,93,168` — `PowerScalar`, `MatchupRead`, `MarginalRead` |
| The budget / drift / spread checks | `Power/ContentValidation.cs:62` — `Budget(containers, atomsOf, ceilingFor)` |
| Live consumers | `Actions/Loadout/AutoEquip.cs:17`; `Actions/Rungs/RungMonotonicity.cs:9,32` |
| A production pricing call | `src/FusionRpg.Data/Sqlite/RpgStore.Power.cs:212` — `CostFunction.Price(atom, t)` |

⚠ **The three lanes still say E9 does not exist.** `enrichment-contract.md:153` (**SC9** — *"Power is
open, and you may not depend on it"*), `ssot-item-categories.md:803`, `ssot-granted-actions.md` §10 Q6
and `ssot-presentation.md` §10 Q7 were all written against that. **SC9 is stale on this point.** Where
SC9 and this spec disagree, D13-VOID wins (`item-ideal.md` §2b preamble: a ruling beats a lane).

### What this module does NOT build

| Not ours | Whose |
|---|---|
| `PowerVector`, `CostFunction`, `PowerScalar`, `MatchupRead`, `MarginalRead`, `ActorPowerCache` | **effect-atom E9/E10 — shipped** |
| Fitting the 20 coefficients | **effect-atom E44** — X6, see below |
| Solving multiplicative pricing (D2) | **effect-atom** — two attempts refuted, `spec-power-sweep.md` §3 |
| A 13th atom kind or an `aptitude.*` channel family, and a fifth `AllocationScope` | **effect-atom + class-system** — `item-ideal.md` §2g rows 2. **R4 is inert until one lands** |
| Whether item bands are *balanced* | **D29** — the class-system's termination (HARD) and dominance (SOFT) guards, extended to geared corners, gated on item module 5 |

### X6 — the coefficients are flat, and the owner already ruled

**Verified:** `Power/CoefficientTable.cs:120-148` — `PowerTables.Authored()` declares **20**
`PowerCoefficientRow`s and **every one has `CoeffMilli = 1000`**. Only `ReferenceScale` varies
(10 / 2 / 25 / 1).

> **Owner, 2026-09-03** (`effect-atom-map.md:323`): the E44 gate *"stays but may be passed
> deliberately — we cannot avoid tuning in this game, so that is normal."*

So X6 is **not a blocker**; it is a precision bound, and each read must state its own sensitivity to it
rather than inheriting a blanket "blocked":

| Read | Sensitive to flat coefficients? | Why |
|---|---|---|
| **R1** implicit share | **No, for the guard it actually needs.** I3's shipped guard is tier-equality across a role's slate (`ssot-item-categories.md:177-179`); the ≤15% cap is a *second* check on a **ratio of two prices computed by the same function**, and a uniform coefficient error cancels in a ratio | ratio, not level |
| **R2** granted-action price | **Partly.** An action's price and an affix bundle's price come from different shapes, so the error does not cancel. Report it as a **band**, never a threshold | cross-shape |
| **R3** card display | **No** — it is already reported with a ±25% band under Rule P | band is the answer |
| **R4** aptitude affix price | **Yes, and doubly** — an aptitude feeds several derived channels, which is the multiplicative case D2 has not solved. **R4 ships as a reported number with the marginal read beside it, never as a gate** | multiplicative |

### The four reads

All four are pure, integer, and take their tables from `PowerTables.Current`
(`CoefficientTable.cs:165`) so a test can scope a table without disturbing one running beside it
(`UseScoped`, `:173`).

#### R1 — implicit budget share

```text
implicitShareMilli(baseType) := 1000 × price(implicitAtom).Total
                                     ÷ ContentValidation ceiling for the base type's rarity
cap: implicitShareMilli ≤ ImplicitShareCapMilli        (data/tuning/item-power.v1.json, default 150)
```

The denominator is **the same ceiling `ContentValidation.Budget` already reads** (`ceilingFor(rarity)`,
`ContentValidation.cs:62-73`) — the `rarity_budget` key module 7 seeds. Inventing a second denominator
would be a second answer to a question the budget check already answers.

**Standing, and this is the whole point of stating it:** a breach is a **content finding, not a
generation input.** `ContentValidation`'s own words apply unchanged — *"a content test that fails naming
the offender — and **never** a generation input"* (`ContentValidation.cs:59-61`). An over-budget implicit
fails a lint; it never silently shrinks at drop time.

#### R2 — granted-action price

An action's price does **not** need a new pricer. `RungMonotonicity` already prices a rung through E9:

```csharp
// src/FusionRpg.Core/Actions/Rungs/RungMonotonicity.cs:32,42
static readonly PowerVector Reference = PowerVector.FromCategory(PowerCategory.Offense, 1000);
var priced = Reference.ScaleMilli(row.QPowerMilli).Total;
```

So `grantedActionPrice(actionId) := Reference.ScaleMilli(rungOf(actionId).QPowerMilli)`, the same path,
reported against the item's rarity ceiling as a **share with a band**. G4's fear — *"pricing it at zero
would make every action-granting item strictly dominant"* — is answered by **never pricing it at zero**:
an action with no resolvable rung is `unpriced`, and unpriced is refused, not free (see below).

⚠ This read is **reportable today and gating only when module 19 `granted-actions` lands** — X3
(`ActionSeeder.Generate` has zero callers) gates the consumer, not this read.

#### R3 — the card's power number

**Rule P, unchanged** (`ssot-presentation.md` §3.3): `≈ 1,300 (±25%)` — two significant figures with the
band, never four digits of confidence. The ±25% is not a hedge; it is
`ContentValidation.DriftTolerancePercent`'s own documented reason (`ContentValidation.cs:44-49`): the
cost function is knowingly ~12.5% wrong on multiplicative pairs.

⭐ **This read makes `PowerScalar.Of` production code for the first time.** Its own doc comment says so —
*"nothing stamps this scalar into a hashed report today — `PowerScalar.Of` has no production caller"*
(`PowerReads.cs:18-22`). The integer fifth root (`:65`) exists precisely so two machines showing the same
item show the same figure; module 10 `item-card` is what finally makes that load-bearing.

**The suppression alternative stays open and is the owner's** (G3 §10 Q7: show it with a band, or
suppress it until the tolerance tightens). This module ships the number *and* a
`ShowPowerOnCard` tunable, so the decision is a file save.

#### R4 — aptitude-affix pricing (D8, as amended)

> **D8, amended 2026-09-03:** an aptitude affix grants a **share delta**, not points. Aptitudes are
> share-normalised — `Share = Total / GrandTotal` (`Stats/Aptitudes/AptitudeAllocation.cs:14-17,81-88`)
> — so granting points silently drains the other eleven.

**R4 is specified now and inert until its vocabulary lands.** `AllocationScope` is a four-value enum
(`AptitudeAllocation.cs:8` — `Commander, DemonType, Aspect, UniqueDemon`); there is no item scope, and
there is no `aptitude.*` channel family among the twelve aptitudes (`Stats/Aptitudes/Aptitude.cs:38-52`).
Both are **other programs'** reviewed vocabulary changes (`item-ideal.md` §2g row 2).

What this module owns when they land:

```text
aptitudeAffixPrice(shareDeltaMilli, actor) := MarginalRead.Of(actorAtoms, aptitudeAtom)
```

**The marginal read, not the stored price** — and for the exact reason `MarginalRead`'s own doc gives:
*"this is how multiplicative pairs get priced correctly. The difference captures whatever multiplies, by
construction"* (`PowerReads.cs:158-163`). D8's named failure mode is aptitude affixes dominating because
they are multiplicative against additive ones; the stored context-free price cannot see that and the
marginal read can.

### Two rules that hold across all four

**1. Unpriced is never zero.** `PricedAtom` carries a `PriceVerdict` with a reason
(`CostFunction.cs:7-17`), and `PowerTables.Find` returns null *"and the caller must treat that as
**unpriced**, not as zero. A missing coefficient silently pricing at zero is how a whole family becomes
free"* (`CoefficientTable.cs:71-74`). Every read here surfaces `unpriced` as its own outcome. A read that
coerces it to `0` is a bug, and test `unpriced_never_reads_as_zero` is what says so.

**2. Integer, `long`-safe, no float on any magnitude path.** E9 is already integer-exact and says why
(`PowerVector.cs:14-16`). This module adds no arithmetic that leaves that discipline: shares are
per-mille integers, divided by 1000 exactly once, at the end.

### The tunables

`data/tuning/item-power.v1.json` — every number a balance pass would touch, per
[tunables-ssot.md](../tunables-ssot.md).

| Key | Default | What it is |
|---|---|---|
| `implicitShareCapMilli` | 150 | I3's ≤15%, as ‰ of the rarity ceiling |
| `grantedActionShareCapMilli` | *unset* | reported only until module 19 lands |
| `showPowerOnCard` | `true` | G3 §10 Q7's reversible half |
| `powerDisplaySigFigs` | 2 | Rule P |
| `powerDisplayBandPercent` | 25 | **derived, not chosen** — mirrors `ContentValidation.DriftTolerancePercent`; a test asserts they are equal |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemPowerReads"

# the E9 substrate this consumes — verify it is green before blaming a read
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Power"
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/Power/*.cs        SHIPPED (E9/E10) — consume, never rebuild
src/FusionRpg.Core/Items/Power/ItemPowerReads.cs   new — R1, R2, R3; the four call sites
src/FusionRpg.Core/Items/Power/AptitudeAffixPrice.cs  new — R4, inert until the vocabulary lands
src/FusionRpg.Core/Items/Power/ItemPowerTuning.cs  new — loader for item-power.v1.json
data/tuning/item-power.v1.json                     new — the table above
tests/FusionRpg.Core.Tests/Items/ItemPowerReadsTests.cs   new
```

## Code style

```csharp
// Unpriced is a third outcome, never a zero. CoefficientTable.cs:71-74 states the rule for the
// table lookup; a read that swallows it is how a whole family becomes free at the item layer too.
public static ImplicitShare Read(AtomRow implicitAtom, int rarityCeiling, ItemPowerTuning t)
{
    var priced = CostFunction.Price(implicitAtom);
    if (!priced.Ok) return ImplicitShare.Unpriced(priced.Verdict.Reason);
    if (rarityCeiling <= 0) return ImplicitShare.Unpriced("rarity has no seeded budget ceiling");

    // Widen before multiplying, divide by 1000 last, exactly once (AGENTS.md numeric rules).
    var shareMilli = checked((long)priced.Power.Total * 1000L) / rarityCeiling;
    return new ImplicitShare(shareMilli, Over: shareMilli > t.ImplicitShareCapMilli);
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `e9_is_consumed_not_reimplemented` | no type under `Items/Power/` declares a vector, a coefficient or a cost function — the D13-VOID boundary, as a test |
| `implicit_share_is_a_ratio_of_two_prices_from_one_function` | R1's coefficient-insensitivity claim, by pricing the same content under two coefficient tables and asserting the **share** is unchanged |
| `implicit_over_budget_is_a_finding_not_a_generation_input` | nothing about the drop path changes when the cap is breached |
| `granted_action_price_uses_the_rung_path` | R2 equals `Reference.ScaleMilli(QPowerMilli)`, the same value `RungMonotonicity` computes |
| `an_action_with_no_rung_is_unpriced_never_free` | G4's stated dominance fear, refused directly |
| `card_power_renders_two_sig_figs_with_its_band` | Rule P — `≈ 1,300 (±25%)`, never `1,284` |
| `display_band_equals_content_validation_drift_tolerance` | the band is derived from `DriftTolerancePercent`, not independently chosen |
| `power_scalar_is_deterministic_across_repeated_reads` | the integer fifth root's guarantee, now that a production caller exists |
| `show_power_on_card_false_suppresses_the_row_and_nothing_else` | G3 §10 Q7's reversal is a file save |
| `unpriced_never_reads_as_zero` | all four reads, one table |
| `aptitude_price_uses_the_marginal_read` | R4 prices with `MarginalRead.Of`, not the stored context-free price |
| `aptitude_read_is_inert_and_says_why_without_its_vocabulary` | no `AllocationScope` item value and no `aptitude.*` family ⇒ a named refusal, never a guessed number |
| `flat_coefficients_are_reported_not_hidden` | every read that is coefficient-sensitive carries the X6 caveat in its result object |
| `no_float_on_any_read_path` | reflection over the module's public surface — magnitudes are `long`/`int` only |

## Boundaries

**Always:** call E9; report `unpriced` as its own outcome; carry the ±25% band on any displayed
number; state per-read sensitivity to X6 rather than a blanket "blocked"; keep every threshold in
`data/tuning/item-power.v1.json`.

**Ask first:** turning any read into a **generation input** (today all four are validation or display);
setting `grantedActionShareCapMilli` to a gating value; changing `powerDisplayBandPercent` away from
`DriftTolerancePercent`.

**Never:** re-implement a cost function, a coefficient table or a power vector inside the item program
— that is D13-VOID's whole content, and `seedsmith-map.md` §3b already named the pattern (*"burying a
general fix inside a feature is how it becomes feature-shaped by accident"*). Never treat an unpriced
atom as free. Never introduce a `float` on a magnitude path. Never gate a drop on a power read.

## Success criteria

- [ ] All four reads call E9; no vector, coefficient or cost function is declared in `Items/Power/`.
- [ ] R1 returns a **share** whose value is unchanged under a uniform coefficient rescale, proven by test.
- [ ] R2 equals the rung path's own number, and an action with no rung is `unpriced`, never `0`.
- [ ] R3 renders under Rule P, with the band derived from `DriftTolerancePercent` rather than chosen.
- [ ] R4 is specified, uses the marginal read, and **refuses by name** until its atom kind / channel
      family and fifth `AllocationScope` land elsewhere.
- [ ] Every threshold lives in `data/tuning/item-power.v1.json`; no bare literal in the read code.
- [ ] SC9's *"you may not depend on power"* is corrected in `enrichment-contract.md` — or the correction
      is filed against it, naming D13-VOID.
