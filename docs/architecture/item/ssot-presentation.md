# Lane G3 — the presentation contract: atoms to text a player can read

**Status:** Lane G3 SSOT, drafted 2026-08-22. Gap lane from
[reconciliation-plan.md](reconciliation-plan.md) R3.

Enriches [item-ideal.md](../item-ideal.md); bound by [enrichment-contract.md](enrichment-contract.md).

> **The gap, stated plainly.** The atom layer has kinds, families, tiers, variants, params, value specs,
> curves, containers, instances and bindings — and no way to render any of it as a sentence. The
> thirteen-lane round then committed to thousands of container rows across two frame vocabularies. Every
> one of those rows is text somebody has to read.
>
> This lane found something while looking for that text, and it is the most load-bearing thing in the
> document, so it goes first: **the twelve derived-channel families do not share a unit.** Six of them
> are flat game units that add straight into a damage or hit-point number. Six are sigmoid points whose
> value in percentage terms swings by 3.4× depending on the opponent. Both are called "resolver points"
> today. §3.2 has the code citations and the arithmetic.

---

> ⚠ **§10 Q5 is STALE (2026-09-03).** It holds `affliction` / `stalwart` at `status = 'pending'` because
> of defect **C2**. **C2 is fixed** — `ResistanceEvaluator.cs:348` now reads
> `clamp(1 + delta/NetFactorScale, …)` and `StatusPolicy.cs:24` cites *"T3.2 (audit F4)"*. Those families
> ship **live**. See [../item-ideal.md](../item-ideal.md) §2e.

## 1. Scope

### This lane owns

| # | Thing |
|---|---|
| 1 | The **projection**: `(instance, container, context) → DisplayModel`. Structured display data, not strings glued in a component |
| 2 | The **display template** layer — where a family's sentence lives, and how params substitute into it |
| 3 | **Number formatting and units** — the unit ledger, the per-mille rules, the rounding rules, the precision rule |
| 4 | The **item card** — which blocks, in what order, what collapses, what never collapses |
| 5 | **Roll-quality rendering** — the bar, and the three-way split that decides which lines get one |
| 6 | **Comparison rendering** — how I13's delta / dominance / roll-quality payload becomes a screen |
| 7 | **Combination legibility** — active, one-away, known-inactive, undiscovered; and the near-miss rule |
| 8 | **Localisation shape** — keys, argument bags, substitution grammar, pluralisation policy |
| 9 | The **validation** that catches an unrenderable family, an unreferenced magnitude, and a channel with no declared unit |

### This lane does NOT own

| Thing | Owner |
|---|---|
| Affix words and item-name generation | **I8** ([ssot-affixes.md](ssot-affixes.md) §4.12) — I consume the naming function's output |
| The comparison **algorithm** — deltas, the dominance partial order, roll quality in ‰ | **I13** ([ssot-inventory.md](ssot-inventory.md) §5.5) — I own only how its output is displayed |
| The colour palette, pip counts, rung display keys | **I1** ([ssot-rarity.md](ssot-rarity.md) §4.5) — measured; I consume it and do not re-derive it |
| Slot display names in two vocabularies | **I2** ([ssot-equip-slots.md](ssot-equip-slots.md) §2.3) — 32 strings, already written |
| Base type names, icons, class nouns | **I3** ([ssot-item-categories.md](ssot-item-categories.md) §5.2, `display_json`) |
| Set names and membership enumeration | **I5** ([ssot-sets.md](ssot-sets.md) §4.2) |
| Which combinations exist and when they fire | **I4** ([ssot-sockets.md](ssot-sockets.md) §4.4, §4.6) |
| UI layout, component code, CSS, routing | the web spec ([docs/web/spec.md](../../web/spec.md)) |
| The power scalar and vector | **E9** — and SC9 says I may not depend on it. §4.9 says what I do when it lands |
| Whether a number is *balanced* | nobody in this round. I render what the engine holds |

**One boundary is worth drawing sharply, because it is the one that will get crossed.** I8 owns the
*name of the item* — `Sturdy Bark Helm of Embers`. I own the *body of the card* — every other line a
player reads. The seam is that I8's naming function must hand me a **key plus arguments**, not a
finished string, or the name is the one part of the card that cannot localise (§3.6, §9.8).

---

## 2. The model

### 2.1 One projection, one renderer, two hosts

Presentation is a **pure function**, and it lives in Core:

```text
render(instance, container, catalogue, context) → DisplayModel
```

`DisplayModel` is **structured data**, not markup and not a paragraph. Every leaf that a human reads is
a `{ key, args }` pair. The web app turns that into DOM. Nothing else turns it into anything.

Three reasons the function is pure and lives in Core, not in the React tree:

1. **The comparison needs it.** I13's delta table diffs *rendered lines*, and two lines can only be
   diffed if they were produced by the same function from the same inputs. A renderer that lives in a
   component cannot be called by the server that computes the gap board.
2. **It must be testable without a browser.** The guard that says *"every atom in the catalog renders,
   and no line contains a raw id"* (§6.3) has to iterate ~775 atoms. That is a `dotnet test`, not a
   Playwright run.
3. **SC8 — standalone-first.** The card must render with the PvZ game closed. A projection in Core
   cannot accidentally reach for a Unity value.

**There is exactly one renderer and it is the SPA.** The launcher overlay and the injector-hosted
overlay both load the same web app rather than drawing their own view
([overlay-spec.md:15](../../launcher/overlay-spec.md), and the rejection of a Unity-textured browser at
`:19`). So there is no second item card to keep in sync, and this document explicitly forbids one
(§8.6). The Unity IMGUI surface draws a *button*, not an item.

### 2.2 Three levels: the line, the card, the compare

| Level | Input | Output | Consumer |
|---|---|---|---|
| **Line** | one atom + its frozen values + its unit class | one `DisplayLine` | the card, the compare, the loot toast, the compendium |
| **Card** | one instance + its container + its sockets, set, enhancement, requirements | an ordered list of `DisplayBlock` | the tooltip, the item page, the roster equip screen |
| **Compare** | two cards + I13's comparison payload | a `CompareModel` | the swap dialog, the gap board, salvage preview |

Every level is built from the one below. There is no path that produces a line except the line
function, which is what stops the "the tooltip says X and the list says Y" class of bug.

### 2.3 The unit ledger is the spine

The single most important structure in this lane is a map from **channel** to **unit class**. It is not
a table. It is code, because E1's own constitution says so:

> *A thing may be **data** if adding a row changes behaviour **without new code**. If a new row needs a
> new consumer, it must be **code**.* — [spec-atom-kind-registry.md:19](../effect-atom/spec-atom-kind-registry.md)

A channel's unit is inseparable from its reader — `combat.crit.rate.*` is sigmoid because
`OverlayCombatCalculator` puts it through a sigmoid. Declaring the unit anywhere but beside the reader
invites the two to drift, and the drift is invisible: the number still renders, it is just wrong by an
order of magnitude. So `UnitClass` sits beside `ParamSchema` in `AtomKindRegistry`, and a channel with a
reader and no declared unit is a **load rejection** (§6).

### 2.4 What the player never sees

Stated up front so it is not re-litigated per block:

- **atom ids, family ids, container ids, instance ids.** Debug surfaces only.
- **tier numbers.** A tier is an authoring band. The roll bar and the magnitude carry everything a
  player needs, and "T4" invites a wiki.
- **name bands A / B / C** (I8 §4.12). Internal to the naming function.
- **group ids** (`g.life`, `g.on-hit`). They order the affix list; they are never labels.
- **per-mille as per-mille.** Content is ‰; the card is not (§3.3).
- **the power scalar**, until E9 ships — and then only with its band (§4.9).

---

## 3. Options considered, and the recommendation

### 3.1 Where display strings live

Three real candidates. The deciding question is not elegance, it is **how many strings each one costs**,
because that number decides whether a human will actually maintain it.

| | A — per-atom string | B — per-family template | C — generated from kind + params |
|---|---|---|---|
| **Where it lives** | `effect_atom.name`, which **already exists** ([AtomRow.cs:31](../../../src/FusionRpg.Core/Effects/Atoms/AtomRow.cs)) | a new `item_display_template` row per family | nowhere; pure code |
| **Strings to author** | **~775** (355 authored + ~420 generated atoms, [atom-family-library.md](../effect-atom/atom-family-library.md) §6) | **~110** (70 family templates + ~30 fragments + 7 element words) | **0** |
| **Renders a rolled range?** | **No.** The frozen value lives on the *instance*, not the atom. `effect_atom.name` cannot know the item rolled 45 | Yes — `{value}` substitutes from `values_json` | Yes |
| **Renders "100–200 fire damage on hit"?** | only as a frozen literal that will be wrong | Yes | Only as `resource.delta fire −100..−200 OnDamageDealt` |
| **Survives adding an element** | No — 5 new rows per family | Yes — the element roster is data (E18) and the template substitutes `{element}` | Yes |
| **Validated today?** | **No.** `AtomRowValidator` never touches `Name`; an atom with an empty name loads clean | new validation, §6 | n/a |
| **SC7** | passes mechanically; fails the maintenance test | passes — one renderer consumes every row, adding a row changes text with no code | passes |

**Recommendation: B, with C as the machine fallback and A demoted to a short label.**

The 775-versus-110 gap is the whole argument. A per-atom table is not *wrong*; it is **seven times the
authoring load for strictly less expressiveness**, and the repo already demonstrates what happens to a
string field nobody validates — `effect_atom.name` has been shipped, unvalidated and undocumented, since
E4. "Half the items read as raw ids" is not a hypothetical failure mode here; it is the default outcome
of option A, and the roster screen is already living it (§8.3).

Three specifics that make B work:

- **One template per family, shared by all five tiers.** A tier changes the number, never the sentence.
  That is why 70 templates cover ~775 atoms.
- **The twelve element-generated families get one template each**, with `{element}` substituting the
  flavour name the library already authored — *Ember / Frost / Gale / Stone / Radiant / Umbral*
  ([atom-family-library.md](../effect-atom/atom-family-library.md) §3.2). Twelve templates cover ~420
  rows.
- **C is the fallback, and it must exist.** When a family has no template, the renderer emits a
  machine line from kind and params rather than an id — `stat.derived combat.crit.rate.fire +30`. That
  is ugly on purpose: it is legible to a developer, obviously provisional to a player, and it makes the
  §6 validation failure *visible* rather than blank. A missing template is a rejection at import, so
  the fallback should never reach a player; it exists for a database edited outside the importer, which
  is the same defence-in-depth E4 already applies at load
  ([spec-atom-schema.md](../effect-atom/spec-atom-schema.md), *"Validation is at load"*).

**`effect_atom.name` is retained and redefined** as a **short label** — two or three words, no numbers,
no substitution: `Vitality`, `Ember Power`, `Searing Strike`. It is what the compendium list, the
authoring tools, and the reject log show. It is never a card line. Redefining a shipped column beats
adding a second one, and it gives the column the validation it never had.

### 3.2 THE UNITS PROBLEM

This is the hard one, and the first job is to state the problem correctly, because **the premise this
lane was handed is wrong against shipped code.**

#### 3.2.1 The finding: derived channels do not share a unit

[definitions.md](../effect-atom/definitions.md) §2 says derived-channel magnitudes are *"resolver points
— sigmoid scale, `AccuracyScale = CritRateScale = 100.0`"*, and
[atom-family-library.md](../effect-atom/atom-family-library.md) §2a says *"`+10 fire power` is ten
resolver points … so ten points is 0.1 sigmoid units"*.

**Checked against code, that is true for six of the twelve families and false for the other six.**

| Family group | Channels | The one consumer | What one point actually is |
|---|---|---|---|
| `elemental_power`, `elemental_defense` | `combat.power.*`, `combat.defense.*` | [OverlayCombatCalculator.cs:84-86](../../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs), `:89`, `:104` | **one damage point**, scaled by the component's element weight. `effectiveDelta = (power − defense) + componentBonus`; `weightedDelta += c.Weight * effectiveDelta`; `powerAdjusted = BaseOverlayDamage + weightedDelta` |
| `shield_capacity` | `combat.shield.capacity.*` | [ShieldRuntime.cs:121-123](../../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) | **one shield hit point**. `maxHp = grant.BaseHp + capacity` |
| `shield_pen`, `shield_toughness` | `combat.shield.pen.*`, `combat.shield.toughness.*` | [ShieldMath.cs:34](../../../src/FusionRpg.Core/Combat/Shield/ShieldMath.cs) | **one damage point to the shield, per hit**. `raw = input + elemMod + hitCount * (pen − toughness)` |
| `shield_regen` | `combat.shield.regen.*` | [ShieldRuntime.cs:403-405](../../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) | **one shield hit point per second**. `ratePm = value × 1000`, then `carry += ratePm * deltaMs / 1000` |
| `precision`, `evasion`, `keen_edge`, `stoicism` | `combat.accuracy.*`, `combat.dodge.*`, `combat.crit.rate.*`, `combat.crit.resist.*` | [OverlayCombatCalculator.cs:91-95](../../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs) via `CombatProbability.Sigmoid(delta, 100.0)` | **a sigmoid point. No fixed value** — see §3.2.2 |
| `cruelty`, `padding` | `combat.crit.damage.*`, `combat.crit.resist.damage.*` | `:99` — `critMultFinal += weight * (1.0 + Sigmoid(critDmgDelta, 100.0))` | a sigmoid point on a multiplier **bounded to `[1.0×, 2.0×]`** |
| `affliction`, `stalwart` | `status.power.*`, `status.resist.*` | [ResistanceEvaluator.cs:190-210](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs), `:212-217`, `:164-165` | **a direct multiplier on magnitude and duration** — see §3.2.4 |

There is no `PowerScale`. [CombatPolicies.cs:9-13](../../../src/FusionRpg.Core/Stats/Derived/CombatPolicies.cs)
declares `AccuracyScale`, `CritRateScale`, `CritDamageScale`, `Steepness` — and nothing else. Grepping
`CombatDerivedReader.Power` in `src/` returns exactly two call sites, both in
`OverlayCombatCalculator.cs`, and neither passes through `Sigmoid`.

> **`+10 fire power` is +10 damage on a pure-fire hit.** It is `+10 hp`'s peer, not its order-of-magnitude
> cousin. The family that is genuinely "a tenth of the effect" is `keen_edge`, and even that is only true
> at one baseline.

This matters far beyond a tooltip: it means half the derived layer can be rendered as plain numbers and
compared directly against primary channels within the same arena, and only the other half needs the
machinery below. Left uncorrected, it would also have produced tier bands wrong by 10× on six families —
which is precisely the trap SC4 exists to prevent.

#### 3.2.2 For the six that really are sigmoid: what one point is worth

`p = 1 / (1 + e^(−delta/100))` where `delta = attacker channel − defender channel`
([CombatProbability.cs:8-9](../../../src/FusionRpg.Core/Combat/CombatProbability.cs),
[ResistanceEvaluator.cs:111](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs)).

The definitions' own calibration reproduces exactly, which is how I know the model is read correctly:
delta `−250` → **7.59 %**, and `+150` points → delta `−100` → **26.89 %**. Definitions §2 says
*"~7.6 % → ~26.9 %"*.

Now the marginal value of **+10 points**, computed across the range:

| Opposed delta | Crit before | Crit after +10 | Gain |
|---:|---:|---:|---:|
| −250 (the shipped baseline) | 7.59 % | 8.32 % | **+0.73 pp** |
| −150 | 18.24 % | 19.78 % | +1.54 pp |
| −50 | 37.75 % | 40.13 % | +2.38 pp |
| **0 (neutral)** | 50.00 % | 52.50 % | **+2.50 pp** |
| +150 | 81.76 % | 83.20 % | +1.44 pp |
| +250 | 92.41 % | 93.09 % | **+0.67 pp** |

*Computed from the shipped formula; not a balance claim.*

**One affix, a 3.7× spread in what it does.** That is the honest reason a percentage cannot be printed
on an item: printing one of those numbers is asserting an opponent the player has not met.

#### 3.2.3 The four options, and what each costs

| Option | Honesty cost | Compute cost | Verdict |
|---|---|---|---|
| **Raw points** — `+30 crit rate` | Truthful, and meaningless to a new player. 30 of what? Two items are still comparable, which is the one thing that matters | free | **necessary, not sufficient** |
| **Derived percentage on the card** — `+7.4% crit chance` | **A lie**, by the table above. It also silently changes meaning between the lawn (baseline −250) and a neutral sim | free | **rejected** |
| **Qualitative band** — `Crit rate: Major` | Never wrong, never useful. Two Majors cannot be ordered, and I13's delta table needs a number to subtract | free | **rejected as primary**; kept nowhere |
| **Converted estimate against a *named* reference** — `+30 crit rate (≈ +7.4 pp vs neutral)` | Honest **if and only if the reference appears in the line**. Drop the reference and it collapses into option 2 | one `exp()` per line | **picked, as a secondary** |
| **Live read against the selected specimen** | The most useful number there is, and it only exists where an actor is selected | one `exp()` per channel per actor, memoised | **picked for the sheet, never for the card** |

#### 3.2.4 The commitment — the two-part line

> **Every magnitude renders as an authoritative part and an optional context part, and the two are
> typographically distinct.**
>
> 1. The **authoritative part** is the integer the engine holds, in its declared unit, with the unit's
>    noun. It never varies with context. It is what the comparison table subtracts. — `+30 crit rate`
> 2. The **context part** is a derived read. It is parenthetical, always prefixed `≈`, and **always
>    names its reference**. — `(≈ +7.4 pp vs neutral)`
> 3. **A `Flat` unit class never produces a context part.** There is nothing to derive: the
>    authoritative number already *is* the effect.
> 4. **No context part is ever produced when its reference cannot be named.** Silence beats a decimal
>    point with no denominator.
> 5. The reference is **`neutral` on the item card** (both sides zero, so `delta` is the item's own
>    contribution) and **the selected specimen's current opposed value on the character sheet**. The
>    sheet says which.

Worked, on one affix, so the shape is checkable:

| Where | Reference | Rendered |
|---|---|---|
| Item card | neutral (0 vs 0) | `+30 fire crit rate  (≈ +7.4 pp vs neutral)` |
| Character sheet, defender at +80 crit resist | that defender | `+30 fire crit rate  (≈ +6.8 pp vs Conehead)` |
| Lawn, at the shipped −250 baseline | the lawn baseline | `+30 fire crit rate  (≈ +2.4 pp at lawn baseline)` |

Three different honest answers, three different named denominators, one unchanging authoritative
number. That is the whole design.

**Cost in honesty: zero** — every derived number carries its denominator, and the number that is diffed
and sorted is never derived. **Cost in computation:** one `Math.Exp` per sigmoid line; at most six such
lines on an item; the card renders on hover. The sheet variant memoises on
`(actor, catalog_revision, binding set hash)` — the key definitions §7 already established for actor
power, reused rather than reinvented.

#### 3.2.5 The two channels I refuse to render a context part for, and why

**`status.power.*` / `status.resist.*` — flagged, not designed around.** Reading the shipped path:
`delta = totalPower − totalResist`
([ResistanceEvaluator.cs:190-210](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs)), then
`netFactor = ComputeNetFactor(delta) = clamp(delta, 0, 10000)` (`:212-217`), and that factor
**multiplies both magnitude and duration** (`:164-165`). `ResistFromPowerRatio = 0.0` and
`TierPower` defaults to `1.0 × 1.0`
([StatusPolicy.cs:9](../../../src/FusionRpg.Core/Status/StatusPolicy.cs),
[ActorDerivedSnapshot.cs:44](../../../src/FusionRpg.Core/Stats/Derived/ActorDerivedSnapshot.cs)).

So an ungeared actor sits at `delta = 1.0` → `netFactor = 1.0` → authored strength. **`+1 status power`
doubles every status the wearer applies, in both magnitude and duration. `+8` makes it nine times.**
Magnitudes in this layer are integers (definitions §2), so the *smallest authorable roll on this channel
is a 2× multiplier*, and there is no tier band an author can write that is not enormous.

That is either a deliberate and extraordinarily powerful affix class, or `ComputeNetFactor` is meant to
be a normalised curve and is not. **This lane will not render a number it cannot explain.** So:

- `status.power.*` and `status.resist.*` render the **raw magnitude with a `status potency` noun and no
  context part**, and the family is marked *pending* in the template table.
- The question goes to **R1's defect register** (§9.17) and to **I8**, which cannot author a tier band on
  `g.elem-power`'s `affliction` or `g.ward`'s `stalwart` until it is answered.

**`Increased` / `More` amounts — a live unit-boundary gap.** `stat.modify` declares
`op ∈ {Flat, Increased, More}` with an integer `amount`
([AtomKindRegistry.cs:83-92](../../../src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs)). SC4 says
ratios and multipliers are integer per-mille. But `StatComposer` treats the value as a **fraction**:
`afterInc = afterFlat * (1.0 + increased)` and `afterMore *= 1.0 + m.Value`
([StatComposer.cs:25-32](../../../src/FusionRpg.Core/Stats/StatComposer.cs)) — and I can find **no
‰→fraction division anywhere in the compile path**. `AtomCompiler.ResolvedParams` copies the integer
through (`:260`, via `CurveTable.ApplyMilli`), and the only `/1000.0` in that file is on `chance`
(`:138-139`).

If that reading is right, an `Increased` atom authored as `150` (meaning +15 %) composes as **×151**,
and any tooltip saying "+15 %" is wrong by three orders of magnitude. This is exactly the *"numbers that
contradict what the engine applies"* failure, found live rather than imagined.

**Display contract regardless of how it resolves:** the renderer divides `Increased`/`More` amounts by
10 to reach a percentage, per SC4. **If the runtime does not divide by 1000 at its own boundary, the
guard in §6.3 case 2 goes red** — which is the point of writing the guard. Claim recorded for R1
(§9.17); no fix proposed here.

#### 3.2.6 The unit ledger, committed

`UnitClass`, declared in code beside `ParamSchema`:

| `UnitClass` | Authoritative render | Context part | Channels |
|---|---|---|---|
| `GameUnits` | `+45 hp` · `+12 fire power` | **none** | `hp`, `maxHp`, `atk`, `defense`, `arm1`, `arm1Max`, `arm2`, `arm2Max`; `combat.power.*`, `combat.defense.*`, `combat.shield.capacity.*`, `combat.shield.pen.*`, `combat.shield.toughness.*` |
| `GameUnitsPerSecond` | `+3 shield hp/s` | none | `combat.shield.regen.*` |
| `SigmoidPoints` | `+30 crit rate` | `≈ +7.4 pp vs <ref>` | `combat.accuracy.*`, `combat.dodge.*`, `combat.crit.rate.*`, `combat.crit.resist.*` |
| `SigmoidMultiplierPoints` | `+40 crit damage` | `≈ ×1.60 vs <ref>`, and the line notes the `[1.0×, 2.0×]` ceiling | `combat.crit.damage.*`, `combat.crit.resist.damage.*` |
| `StatusPotencyPoints` | `+8 blight potency` | **suppressed** — §3.2.5 | `status.power.*`, `status.resist.*` |
| `PerMilleRatio` | `+15% hp` (Increased) · `×1.15 hp` (More) | none | `op` amounts, chances, shares |
| `Milliseconds` | `4.0 s` · `250 ms` under one second | none | durations, `icd_ms` |
| `Count` | `2 bullets` | none | `count`, `maxTargets` |
| `Flag` | present / absent, never a number | none | `status.immune.{tag}` |

Two rules that fall out of the table and must be stated, because they are the ones a UI pass will break:

- **`Increased` and `More` never share a glyph.** `Increased` sums, `More` multiplies
  ([StatComposer.cs:25](../../../src/FusionRpg.Core/Stats/StatComposer.cs) vs `:31-32`). Rendering both as
  `+15%` erases a real mechanical difference that a player will otherwise learn correctly in five minutes.
  `Increased` → `+15% hp`. `More` → `×1.15 hp`.
- **A `GameUnits` derived channel is still labelled with its arena.** `+12 fire power` is 12 damage *on
  a fire component*, weighted by that component's share. The noun `fire power` carries that; a bare
  `+12 damage` would not, and would over-promise on a mixed-element hit.

### 3.3 Per-mille everywhere

Content is integer ‰. Players do not think in ‰. Four rules, and the first one is not a new invention.

**Rule 1 — the conversion is already shipped; adopt it, do not write a second one.**

```ts
// web/fusion-rpg-web/src/features/demons/patronView.ts:23
const pct = (milli: number) => `${(milli / 10).toFixed(1).replace(/\.0$/, "")}%`;
```

Divide by 10, one decimal, trim a trailing `.0`. `150‰` → `15%`. `185‰` → `18.5%`. That helper moves
into the shared display module and `patronView` calls it instead of owning it — one convention, not two.

**Rule 2 — never render a non-zero per-mille as `0%`.** `4‰` is `0.4%`, not `0%`. Round **away from
zero** at the display boundary so a real bonus never vanishes. This is the same `DivRoundHalfAway`
direction the engine uses ([CurveTable.cs:105](../../../src/FusionRpg.Core/Effects/Atoms/CurveTable.cs)),
kept aligned deliberately.

**Rule 3 — rounding happens exactly once, at the display boundary, and never feeds back.** The renderer
receives the **frozen integer** from `effect_instance_atom.values_json`
([Instantiator.cs:15-21](../../../src/FusionRpg.Core/Effects/Atoms/Instantiator.cs)) and formats it. It
never re-applies a curve, never re-rolls, never recomputes a magnitude. The engine already applied its
curve before the roll (definitions §2: *"the curve scales `Min` and `Max` **before** the roll"*), so a
second application in the renderer would produce a number the engine never held.

**Rule 4 — the invariant, and it is testable.** *If the card shows `+45 hp`, `values_json` holds `45`.*
§6.3 case 2 asserts it over a seeded instance for every atom in the catalog. That single test is what
makes "numbers that contradict what the engine applies" a build failure rather than a bug report.

**Rule P — precision never exceeds the source's claimed accuracy.**

| Source | Renders as |
|---|---|
| A frozen integer | exactly, no rounding at all |
| A per-mille | one decimal — 1‰ is its resolution |
| A duration in ms | `250 ms` below 1 s, `4.0 s` above, one decimal |
| A sigmoid context read | one decimal in pp; it is an estimate and says `≈` |
| E9's power scalar, when it exists | **two significant figures with its band** — `≈ 1,300 (±25%)`. Definitions §7 sets drift tolerance at ±25 % per category and documents the formula as knowingly 12.5 % wrong on multiplicative pairs. Printing `1,284` from that would be four digits of confidence on a number with one |

### 3.4 Roll quality display

I13 already defined the **number** — *"integer ‰ per atom, plus the mean … where the rolled value sits
inside the atom's own authored `[Min, Max]` after curve scaling"*
([ssot-inventory.md:439](ssot-inventory.md)). I own only the rendering.

| Option | Verdict |
|---|---|
| **Inline ‰** — `+45 hp (847‰)` | **Rejected.** Six affix lines then carry twelve numbers, and the roll quality competes with the magnitude, which is the number the player is actually buying |
| **Star rating** — `★★★★☆` | **Rejected.** Stars imply a ceiling you are meant to reach. A roll is a position in a range, not a grade |
| **Five-segment bar** ✅ | **Picked.** Reads as "where in the range", countable at a glance, and takes one small column |
| Ten-segment bar | Rejected — ten segments are not countable at a glance, and the extra resolution is noise on a number that already has a ‰ available on expansion |

```text
segments = clamp(ceil(qualityPerMille * 5 / 1000), 1, 5)   // a non-zero roll never shows an empty bar
```

**The three-way split — which lines get a bar at all.** This falls straight out of `RollPolicy`
([ValueSpec.cs:9-18](../../../src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs)) and it is the part that
makes the bar honest:

| `RollPolicy` | Bar? | Renders |
|---|---|---|
| `Fixed` (`Min == Max`) | **No bar at all** | the value. Every implicit, every base stat, most fixed-core atoms. A *full* bar here would be a lie about the item's luck — nothing could have rolled otherwise |
| `OnInstantiate` | **Bar** | the frozen value, plus the `[Min, Max]` band on expansion |
| `OnApply` | **No bar** | the **band**, not a point — `100–200 fire damage on hit`. The item did not roll it; the hit does |

That split also means the bar appears on exactly the lines I7 can reroll, which is the right coupling
and was not designed for.

**Colour: the bar uses the theme's neutral→sun ramp, never the rarity palette.** I1 made **lightness**
the rarity ladder ([ssot-rarity.md:348-352](ssot-rarity.md)). A second lightness ladder inside the same
card would compete with it, and the accessibility argument that justified the first one would be
undone by the second.

The **mean ‰** appears once, in the card footer, as a number: `roll quality 610‰`. One number in one
place, where I13's sort key and best-in-role heuristic (`ssot-inventory.md:491-503`) can be recognised.

### 3.5 Two frame vocabularies — and the cost is much smaller than it looks

The question: the same role is `head` on a humanoid and `crown` on a plant. Does the affix text change
too, or only the slot name?

**Answer: only the slot name, almost always.** And the reason is worth reading, because the instinct is
to budget for a doubling that will not happen.

Affix templates say `+45 hp`, `+12 fire power`, `100–200 fire damage on hit`, `heals 3% of damage
dealt`. **None of those references a body.** The frame vocabulary is a fact about *where an item sits*,
not about what a magnitude does.

The rule, so the next author does not have to re-derive it:

> **A template needs a `template_plant` only if it names a body part, a hand action, or walking.**
> Everything else is frame-neutral and shares one string.

Applied to I8's fifteen affix groups ([ssot-affixes.md](ssot-affixes.md) §4.1):

| Group | Needs a plant override? |
|---|---|
| `g.life`, `g.attack`, `g.armour`, `g.ward`, `g.elem-power`, `g.precision`, `g.shield-stat`, `g.on-hit`, `g.on-death`, `g.sustain`, `g.affliction`, `g.board`, `g.economy` | **No** — all thirteen are magnitudes and nouns with no body in them |
| `g.evade` (`evasion`) | **Yes** — "chance to dodge" on a rooted thing reads wrong. Plant: *sway* |
| `g.tempo` (`swiftness`) | **No** — it never rolls on a plant at all (I8 §4.4's frame filter), so it needs no plant string |
| `g.tempo` (`quickening`, `flourishing`) | **No** — "attacks 12% faster", "produces sun 8% faster" are both fine |

**One template out of seventy.** The honest per-frame cost of the *affix line layer* is under five
strings.

Where the frame cost actually lands, and who already paid it:

| Surface | Strings | Owner | State |
|---|---|---|---|
| **Slot names** | 15 roles × 2 frames + `standard` × 2 = **32** | **I2** | already written, [ssot-equip-slots.md:83-99](ssot-equip-slots.md) |
| **Base type names** | one per base type | **I3** | **zero extra cost** — `item_base_type.frame` makes a base type frame-specific, so `Bark Helm` and `Thorn Crown` are two rows, not two renderings of one row |
| **Affix words** (the item name) | `word_plant` nullable | **I8** | already designed; its own sample shows 4 of 6 need no override ([ssot-affixes.md:774-781](ssot-affixes.md)) |
| **Affix templates** (the card lines) | `template_plant` nullable, ~1 used | **G3** | this document |

**The mechanism, once, not twice.** Two nullable `_plant` columns exist across two tables — I8's
`item_affix_name.word_plant` and my `item_display_template.template_plant` — and they share **one**
resolution helper, `plantOrDefault(row, frame)`. Two columns, one function, one rule. Building a
per-frame override system twice is how the two drift.

**Hybrids (OD3) — the case that will otherwise be answered wrong.** A hybrid wears base types from
either frame. **The slot name follows the *item's* frame, not the wearer's.** A humanoid gauntlet on a
hybrid still reads `hands`. Rendering it as `leaves` because the wearer is part-plant is exactly the
"reads as a costume" failure I2 spent §2.6 avoiding, and it would make the same item read differently on
two wearers — which breaks comparison. The **frame badge** on the card shows the item's frame so the
choice is visible rather than surprising.

### 3.6 Localisation

**The evidence first, because the tree is not where anyone would assume.**

| Fact | Citation |
|---|---|
| i18n is **out of scope for web v1** | [docs/web/spec.md:105](../../web/spec.md) |
| No i18n library anywhere in the web app | grep over `web/fusion-rpg-web/src` and `package.json` — no `i18next`, no `react-intl`, no locale files |
| Display strings are **hardcoded English literals in C#** | [DemonTraitCatalog.cs:13-29](../../../src/FusionRpg.Core/Demons/DemonTraitCatalog.cs) — `new("berserker", "Berserker", …, "Hits harder as its own health falls.")` |
| Display strings are **also hardcoded Chinese literals in C#** | [DemonSpeciesCatalog.Generated.cs:14-23](../../../src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs) — `Name = "钻石套娃僵尸"`, captured from game data, with no English counterpart and no key |
| Captured game text with Unity markup already reaches the UI | [almanacText.ts:1-10](../../../web/fusion-rpg-web/src/lib/almanacText.ts) strips `<color=…>`, `<b>`, `<size=…>`; its test asserts on Chinese input ([almanacText.test.ts:6-8](../../../web/fusion-rpg-web/src/lib/almanacText.test.ts)) |
| The lanes have already started using keys | I1's `rarity.display_key` ([ssot-rarity.md:409](ssot-rarity.md)); G1's `flavour_key` ([ssot-uniques.md:581](ssot-uniques.md)) |

So the tree today ships **two languages in one catalog with no way to select either**. That is the state
a display contract must not inherit and must not extend.

**The decision is therefore not "should we localise". It is "does the string design foreclose it".**

> **Ship one language. Ship it behind keys.**

| Rule | |
|---|---|
| **L1** | Every player-visible string this lane produces is a **key plus an argument bag**, resolved by one function at the render boundary. `item.affix.vitality` + `{ value: 45, unit: "hp" }` |
| **L2** | The v1 key catalog is **one file with English values**. No per-language table, no fallback chain, no locale negotiation. Those cost nothing to add later and real complexity now |
| **L3** | Content-authored display text lives in a **`_key` column, never a literal** — the rule I1 and G1 already adopted, extended to templates and to I5's `item_set.display_name` (§9.5) |
| **L4** | **Captured game text is a fourth category and is never keyed.** It is passthrough, it stays in whatever language the game shipped it in, it goes through `stripTmpRichText`, and it is rendered **visually quoted** so a player is not confused by a language change mid-card. `DemonSpeciesCatalog`'s Chinese names are this category |

**The substitution grammar, decided now because it constrains every one of the ~110 strings.**

```text
"{sign}{value} {unit.hp}"                          → "+45 hp"
"{min}–{max} {element.fire} damage on hit"         → "100–200 fire damage on hit"
"{sign}{pct} {channel.maxHp}"                      → "+15% hp"
```

| # | Rule | Why, concretely |
|---|---|---|
| **S1** | **Named placeholders only, never positional.** `{value}`, not `{0}` | A translator reordering a sentence must not have to track argument order |
| **S2** | **No string concatenation outside a template.** A line is one template and one substitution pass | Gluing a sign, a number and a noun in code is how the unit gets dropped, and how RTL and measure-word languages break |
| **S3** | **Pluralisation is declared per key, never inferred.** A key that can pluralise carries variants selected by CLDR category; a key that cannot declares `plural: none` and is validated as such (§6) | The naive `+ "s"` is the thing being prevented |
| **S4** | **Only three key families take plural variants**: socket count, set piece count, charge count. Everything else renders a number beside an invariant noun | Counted, not assumed: `45 hp`, `12 fire power`, `4.0 s` are all invariant in English |
| **S5** | Numbers go through the platform formatter, never interpolation | Already done in one place — `toLocaleString()` at [DemonsPage.tsx:235](../../../web/fusion-rpg-web/src/features/demons/DemonsPage.tsx) — so make it the rule rather than the exception |

**Why S3/S4 are written this precisely, given the tree already contains Chinese:** Chinese has one
plural category and uses measure words, so `{count} sockets` becomes `{count} 个插槽`. The named-placeholder
rule handles that; a `+ "s"` cannot. That is the concrete reason, not an appeal to good practice.

**What is deliberately not built:** locale detection, a language picker, right-to-left layout, per-locale
number formats beyond what `toLocaleString` gives free, and translated content tables. All of them are
additive against the four rules above, and none of them is v1.

---

## 4. The design, committed

### 4.1 The item card — blocks, in order

Two zones. **Identity never collapses. Detail may.**

| # | Block | Contents | Collapses? |
|---:|---|---|---|
| 1 | **Header** | enhancement prefix (`+12`, I6) · item name (I8's grammar, or I3's base name, or G1's authored unique name) · rarity **pips + rung name in text + colour** (I1's three channels, all three, never colour alone) · base type name and class noun (I3) · **role name in the item's frame vocabulary** (I2) · frame badge · item level | **never** |
| 2 | **Requirements** | level, and I11's clause. Red when unmet, and it **names which number gates** — `Sinew 32 (29 + 3) — 29 gates` ([ssot-requirements.md:220-221](ssot-requirements.md)) | never |
| 3 | **Base stats** | the `atom.base-*` atoms at `seq 0` (I3 §5.2). Plain numbers, no bars — they are `Fixed` | never |
| 4 | **Implicit** | the one implicit at `seq 1`, separated by a rule. Italic. No bar | never |
| 5 | **Affixes** | one line per rolled atom. **Prefixes then suffixes**, each sorted by **group order, then tier DESC, then `seq` ASC** — content-derived and ordinal, the same tiebreak discipline definitions §5 forced on the effect list. Roll bar per line | never |
| 6 | **Enhancement** | **one block**, never stacked lines — I6 §5.5's suppress-and-append rule makes this possible and asks for it | never |
| 7 | **Sockets** | a row of socket cells; empty ones shown as empty. Then **active resonances**, then **the word** if any, then **near-misses** (§4.3) | the resonance *catalog* collapses; active and one-away never do |
| 8 | **Set** | set name, `3 / 4`, and **the whole threshold ladder** — active lit, inactive dimmed but readable (§4.3) | never |
| 9 | **Granted action** (G4) | name and description from `rpg_action`, plus the **battle-only** tag and the **already known** state ([ssot-granted-actions.md:823-826](ssot-granted-actions.md)) | never |
| 10 | **Flavour** | G1's `flavour_key`, italic, uniques only | may |
| 11 | **Footer** | mean roll quality ‰ · stale flag · locked flag · salvage yield (I9) · `no_reassign` if set | may |

**The disclosure rule, and it is the one that prevents failure mode 4:**

> **Nothing that can differ between two items of the same base type may be hidden.**

Everything that varies — every magnitude, every affix, every socket, every set line, every requirement,
the enhancement level, the roll bars — is on the face of the card. What hides behind expansion is
**invariant explanation**:

| Hidden until expanded | Why it is safe to hide |
|---|---|
| Each affix's `[Min, Max]` band | the bar already shows *where* in the band; the band itself is a property of the atom, not of this copy |
| The full resonance catalog beyond active and one-away | ~45 combinations (I4 §4.4); the same list on every socketed item |
| The set's full member list and each member's role | I5's `item_set_member` is enumerable ([ssot-sets.md:370](ssot-sets.md)); the count and the ladder are on the face |
| Salvage yield breakdown | one number is on the face |
| Atom ids, tiers, groups | never shown at all (§2.4) |

**The compact line**, for the armoury list and the gap board (I13 §5.9): `pips · name · role · roll bar ·
delta arrow vs the incumbent`. One row, scannable, and it is produced by the same functions as the card
so the two cannot disagree.

**The one collision to resolve.** I6 picked `+12` as a **name prefix** because *"the left edge is what
gets scanned"* ([ssot-enhancement.md:759-762](ssot-enhancement.md)) — and I1's pips also want the left
edge. Both cannot have it. I render **pips first, then `+12`, then the name**, because the pips are the
rarity ladder's accessibility channel and must not be displaced by an optional token. Flagged to the
owner as §10.2, and to I6 as §9.6.

### 4.2 Comparison rendering

I13 owns the payload — per-channel delta with a unit, a four-valued dominance verdict, roll quality in ‰
([ssot-inventory.md:432-443](ssot-inventory.md)). I own the screen.

**Deltas group by unit class, and the unit is in the group header, never in the column.**

```text
Damage and hit points (game units)
  hp                     71  →  62      −9
  fire power             18  →  24      +6

Probability (sigmoid points)
  fire crit rate          0  →  30     +30   (≈ +7.4 pp vs neutral)
  accuracy                9  →  14      +5   (≈ +1.2 pp vs neutral)
```

`+9 hp` and `+5 accuracy` never appear in one numeric column. That is the whole SC4 rule expressed as a
layout constraint, and §6.3 case 3 tests it against a generated matrix of every channel pair.

**The dominance verdict is a word and a shape, never a colour alone** — the same redundancy rule I1
established for rarity ([ssot-rarity.md:348-356](ssot-rarity.md)), applied here because a comparison UI
encoding "better" in hue alone is explicitly forbidden there (`:372`).

| Verdict | Rendered |
|---|---|
| `strictly-better` | `Strictly better ▲` |
| `strictly-worse` | `Strictly worse ▼` |
| `sidegrade` | `Sidegrade ◆` — **plus the trade, spelled out**: a *you gain* list and a *you give up* list |
| `incomparable` | `Not comparable ◇` — **plus the reason**: *"these touch different channels — the candidate has no hp line and the incumbent has no crit rate line."* An incomparable verdict with no explanation reads as a bug |

**No synthesized scalar, and the copy that says why is permanent.** I13 asked for *"one line of copy
explaining why"* (`ssot-inventory.md:657-659`). I render it as a **persistent footnote**, not a
dismissible hint: *"There is no single score. 9 hit points and 5 accuracy points are not the same
currency."* A player who dismisses it once will read its absence as a missing feature forever.

**When E9 lands**, power joins as **one row above the delta table**, rendered under Rule P as
`≈ 1,300 (±25%)`, and the delta table stays. A single number cannot say *what* got better; that was
I13's argument and it does not stop being true when the number exists.

### 4.3 Combination legibility

Progress is what makes a combination a goal rather than an accident. **Four states, closed:**

| State | Rendered | Condition |
|---|---|---|
| `active` | full colour, name, and its atom lines | the evaluator returned it |
| `one-away` | dimmed, name, atom lines, **and the exact missing ingredient named** — *"needs 1 more Ember Shard"* | `distance == 1` |
| `known-inactive` | dimmed, **name only**, atoms hidden | the player has held every ingredient (I4's compendium reveal, [ssot-sockets.md:216-219](ssot-sockets.md)) and `distance > 1` |
| `undiscovered` | **not rendered at all** | I4's reveal rule has not fired, or the combination is unreachable on this item |

**The rule that keeps it honest:** near-miss is computed by **the same pure evaluator** that computes the
active set, called once with a distance parameter. Never a second function. I4 already specifies
evaluation as *"a pure, ordered function of `(socket contents, socket affinities, catalog_revision)`"*
(`ssot-sockets.md:277-279`) — extend its return with `distance`; do not write a parallel near-miss pass.
Two functions is precisely how *"the tooltip said one more and it did not fire"* happens (§9.4).

**Distance, defined so it is not invented per shape:**

```text
distance(combination, fill) = the minimum number of insert substitutions that would satisfy it,
                              counting an empty socket as one substitution
```

- **Pure-k of element e:** `distance = max(0, k − count(e))`, and **`∞` if the item does not have k
  sockets**. An unreachable combination is `undiscovered`, never `one-away` — that is what stops a
  two-socket item promising a four-insert resonance.
- **Word:** `distance = the number of ordered positions whose required ingredient is absent`.
- **`omni` counts toward Diversity only** (I4 §4.4). The card must **say so on the omni insert's own
  line**, because an omni insert sitting in a three-fire fill that is not firing Pure looks broken
  otherwise (§9.4).

**Sets render differently on purpose.** A set has at most four thresholds and a small fixed member list,
so **the whole ladder always renders** — every threshold, active lit, inactive dimmed but with its atoms
**visible**. A set's inactive thresholds are the goal; hiding them removes the goal. The socket
catalog is ~45 and cannot all render, which is why sockets get the four-state model and sets do not.

One wording rule that prevents a real lie: **a threshold line names the piece count it needs, never
"next".** `4 pieces:` — not `Next tier:`. If the player unequips down to two, "next" meant something
different an hour ago, and a screenshot taken then is now wrong.

### 4.4 The line grammar

A `DisplayLine` is:

```text
{ key, args, unitClass, contextRead?, rollQualityPerMille?, rollPolicy, sourceKind, groupOrder }
```

`sourceKind ∈ { base, implicit, affix-prefix, affix-suffix, enhancement, socket-insert, resonance,
word, set-threshold, granted-action, unique-identity, unique-variance }`.

`sourceKind` is what lets the card group without re-deriving where a line came from, and it is what
answers G1's ask that a unique's **identity lines** be distinguishable from its **variance line**
([ssot-uniques.md:964-966](ssot-uniques.md)). Combined with `rollPolicy`, it answers it precisely:
`Fixed` core atoms on a unique are `unique-identity`; the `OnInstantiate` one is `unique-variance` and
is the only line on that card with a bar.

---

## 5. Data shape

### 5.1 Reused, unchanged

| Column / table | Used for |
|---|---|
| `effect_atom.family_id`, `variant`, `tier` | the template lookup key is `family_id`; `variant` substitutes; `tier` is **never rendered** |
| `effect_atom.params_json` | the argument bag — every `ParamKind.Value` leaf becomes a substitutable arg |
| `effect_atom.when_json` | trigger clause, `chance`, `icd_ms` → the condition fragment of a line |
| `effect_atom.tags_json` | already documented as *"element, family, category — for AI, **UI**, and cost lookup"* ([spec-atom-schema.md](../effect-atom/spec-atom-schema.md)) — the UI reader is this lane |
| `effect_instance_atom.values_json` | **the authoritative magnitude.** Rule 3 of §3.3 |
| `effect_container.rarity`, `slot`, `level_req` | header and requirement line |
| `rarity.color_hex`, `pip_count`, `display_key` | I1's N2 — the header's three channels |
| `item_base_type.display_json`, `class_id`, `frame` | I3 |
| `item_affix_name` | I8 — the item name |
| `item_set.display_name`, `item_set_member`, `item_set_tier` | I5 — the set block |

### 5.2 Redefined — one shipped column

| Column | Was | Becomes |
|---|---|---|
| `effect_atom.name` ([AtomRow.cs:31](../../../src/FusionRpg.Core/Effects/Atoms/AtomRow.cs)) | `TEXT`, documented only as "display", **validated nowhere** | a **short label key** — two or three words, no numbers, no substitution. Never a card line. `NOT NULL`, non-empty, and validated (§6) |

Redefining beats adding. The column already exists, is already in the content hash (definitions §8), and
already carries the right *intent*; what it lacked was a definition and a check. Note that
`AtomCompiler` already forwards it into `EffectDefDto.Name`
([AtomCompiler.cs:127](../../../src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs)), so the label already
reaches the effect bag and the debug log — giving it a rule makes that surface better too.

### 5.3 New — two tables and one code structure

**N1 — `item_display_template`.** One row per family. Consumer: `ItemDisplayRenderer` in Core.

```sql
CREATE TABLE item_display_template (
  family_id       TEXT NOT NULL PRIMARY KEY,   -- FK effect_atom.family_id
  template_key    TEXT NOT NULL,               -- key into the string catalog; NEVER a literal (L3)
  template_plant  TEXT NULL,                   -- plant-frame override key; NULL = use template_key
  group_id        TEXT NOT NULL,               -- I8's affix group, for card ordering
  status          TEXT NOT NULL DEFAULT 'live',-- live | pending  (pending = authored, not renderable yet)
  enabled         INTEGER NOT NULL DEFAULT 1,
  revision        INTEGER NOT NULL DEFAULT 1
);
```

**SC7 check, explicitly.** Adding a row changes what a player reads with **no new code** — one renderer
consumes every row. A new family without a row is a **rejection**, not a silent blank, so the table
cannot accumulate rows nothing reads, and it cannot be short. That is both halves of SC7.

`status = 'pending'` exists for exactly one situation: a family whose channel semantics are unresolved
(`affliction`, `stalwart` — §3.2.5). A pending family may be authored and hashed; binding one is
already refused by the atom layer's quarantine (D6), so nothing renders it.

**N2 — the string catalog.** A **file**, not a table, for v1: `content/display/en.json`, a flat map from
key to `{ template, plural? }`. It joins the content hash as a file digest.

Why a file and not a table: it is edited as a unit, diffed in review, and has no per-row lifecycle. A
table would need `enabled`, `revision`, and an importer arm for zero benefit at one language. If a
second language ships, it becomes `en.json` + `zh.json` and the loader gains a lookup — still no schema.
Flagged as §10.3 because it is reversible and the owner may prefer the table.

**N3 — `UnitClass`, in code, beside `ParamSchema`.**

```csharp
// src/FusionRpg.Core/Effects/Atoms/UnitClass.cs
public enum UnitClass {
    GameUnits, GameUnitsPerSecond, SigmoidPoints, SigmoidMultiplierPoints,
    StatusPotencyPoints, PerMilleRatio, Milliseconds, Count, Flag
}
```

plus `ChannelUnits.For(string channelId) → UnitClass` covering the 8 primary channels and the 12+4
derived families by **prefix pattern**, the same way readers already match generated element channels
([DerivedStatChannels.cs:96-99](../../../src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs)). A
new element therefore needs no new unit row, which matches E18's data-driven element roster.

**Code, not data, and E1's own rule says why** (§2.3). This is an ask on E1 (§9.15) rather than a table
this lane mints, because a unit that lives away from its reader will drift from it silently.

**N4 — one column requested on `item_base_type`.** I3's `display_json` is specified as *"name parts,
icon key"* ([ssot-item-categories.md:285](ssot-item-categories.md)). I need it to carry a **name key**,
not name parts, or the base type name cannot localise. That is an ask on I3 (§9.3), not a change I make.

### 5.4 What is code and what is data — SC7, applied honestly

| Thing | Code or data | Test |
|---|---|---|
| The template **rows** | **data** | a new family + a row = new text, no code |
| The **string catalog** | **data** | changing a word = no code |
| `UnitClass` and the channel→unit map | **code** | a new unit class needs a new render arm. A new *channel* in an existing class needs nothing — the prefix match covers it |
| The **line function**, the **card block order**, the **four combination states** | **code** | each is a consumer |
| Rarity colours, pips, display keys | **data**, and **I1's** | already decided |
| Slot names | **data**, and **I2's** | already decided |
| The **fallback renderer** (option C) | **code** | one arm per kind |

The card's block order is deliberately code and not a configuration table. A reorderable card is a table
with one consumer and one row-set that nobody will ever change — the `status.expose` shape SC7 names.

---

## 6. Validation and reason codes

### 6.1 Bad input → reason code

| # | Bad input | Reason code | Phase |
|---:|---|---|---|
| 1 | A `family_id` with rows in `effect_atom` and no `item_display_template` row | **`MissingDisplayTemplate`** *(new)* | import |
| 2 | A template references `{param}` the family's kind does not declare | `UnknownParam` | import |
| 3 | A template references `{variant}` on a family whose every row has `variant = ''` | `UnknownParam` | import |
| 4 | A kind declares a `ParamKind.Value` param the template never references | **`UnrenderedMagnitude`** *(new)* | import |
| 5 | A declared param the template never references that is **not** a magnitude (`icd_key`, a `channel` the family already implies) | **warning**, not a rejection | import |
| 6 | A template whose placeholder syntax does not parse | `BadParamValue` | import |
| 7 | A template using a **positional** placeholder (`{0}`) | `BadParamValue` — S1 | import |
| 8 | A channel that has a reader and no `UnitClass` | **`MissingUnitClass`** *(new)* | load |
| 9 | A `UnitClass` declared for a channel with **no reader** | `RuntimeUnsupported` — the `status.expose` shape, rejected rather than shipped inert | load |
| 10 | A `template_key`, `template_plant`, `display_key`, or `flavour_key` absent from the string catalog | **`MissingDisplayKey`** *(new)* | import |
| 11 | A key in the catalog that nothing references | **warning** (orphan lint) | import |
| 12 | A key declaring plural variants with no `other` form | `MissingDisplayKey` | import |
| 13 | A key declaring `plural: none` whose template contains a count placeholder | `BadParamValue` — S3/S4 | import |
| 14 | `effect_atom.name` empty, or containing a digit | `BadParamValue` — §5.2's redefinition | load |
| 15 | A template rendering a `StatusPotencyPoints` channel with a context part | `BadParamValue` — §3.2.5 | import |

**Import is all-or-nothing; load is per-row** — definitions §10's two-phase rule, unchanged. Rows 8, 9
and 14 are load-phase because they are defence in depth against a database edited outside the importer.

### 6.2 Four new codes, and the case for each

The closed list is 33 and G1 already flagged reason-code inflation as a cross-lane question
([ssot-uniques.md:967-969](ssot-uniques.md)). So each is argued rather than assumed:

| Code | Why it cannot reuse an existing one |
|---|---|
| **`MissingUnitClass`** | **The one that must exist.** Its absence *is* the failure this lane was written for: a channel with no declared unit renders as a bare number, and `+10 fire power` and `+10 crit rate` become indistinguishable. No existing code names "this number has no unit" |
| **`MissingDisplayTemplate`** | The author added a family and no sentence. `UnknownAtom` is about a missing atom; this is a present atom with no words |
| **`MissingDisplayKey`** | A template or a content row points at a key the catalog does not have. Distinct fix from a missing template: the row exists, the string does not |
| **`UnrenderedMagnitude`** | A number the engine applies and the player never sees. This is the `status.expose` defect in mirror image — not a row nothing reads, but a value nothing shows — and SC7's argument applies unchanged |

**If the owner wants fewer:** `MissingDisplayTemplate` and `MissingDisplayKey` collapse into one
`MissingDisplayString`, at the cost of the operator having to open a row to learn which of the two
tables is short. `MissingUnitClass` and `UnrenderedMagnitude` should not collapse into anything — they
are the two codes that make the units problem a build failure.

### 6.3 Guard tests this lane owes

| # | Test | Asserts |
|---:|---|---|
| **1** | **Every atom renders.** Iterate the whole catalog; render each at `Min`, midpoint and `Max` | no line contains a raw id, an unresolved `{placeholder}`, or an empty string. **This is the test that stops "half the items read as raw ids"** |
| **2** | **Rendered equals applied.** For a seeded instance, every rendered magnitude equals the integer in `values_json`, under the same `DivRoundHalfAway` the engine used ([CurveTable.cs:105](../../../src/FusionRpg.Core/Effects/Atoms/CurveTable.cs)) | the tooltip cannot disagree with the engine. **This is the test that catches §3.2.5's `Increased`/`More` gap if it is real** |
| **3** | **No unit collision.** Over a generated matrix of every channel pair, no comparison column ever mixes two `UnitClass` values | SC4, as a layout invariant |
| **4** | **Determinism.** Same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical `DisplayModel` | SC5 extends to the card. Two byte-identical items must read identically, including their generated rare name |
| **5** | **Frame parity.** Every template with a `template_plant` resolves for both frames; every one without resolves for both | no item is unrenderable on one frame |
| **6** | **Plural policy.** Exactly the three key families of S4 carry plural variants; every other key declares `plural: none` | S3 cannot rot |
| **7** | **Every combination state is reachable.** For a fixture item, all four socket states and the set ladder render | the near-miss path is exercised, not just declared |

---

## 7. Worked examples

**All numbers are illustrative, not balanced.** The sigmoid conversions are computed from the shipped
formula; the magnitudes are invented.

### 7.1 A plant-frame Fused crown at ilvl 45

Rarity `fused` — ordinal 50, `#63a4ed`, **5 pips**, tier window t2–t4, count band 2–3
([ssot-rarity.md:120](ssot-rarity.md)). Role `head-guard`, plant vocabulary → **`crown`**
([ssot-equip-slots.md:91](ssot-equip-slots.md)). Base type `Thorn Crown`. Two affixes rolled.

```text
●●●●●  Sound Thorn Crown of Rime                                    [plant]
       Fused · Crown · Head guard · item level 45

       Level 38 required.
       Sinew 24 (24 + 0) — 24 gates.                                       ✓

       Armour                                          18
       ────────────────────────────────────────────────────
       Implicit:  +6% status resist                                        (i)

       +214 hp                                              ▮▮▮▮▯
       +30 fire crit rate   (≈ +7.4 pp vs neutral)          ▮▮▯▯▯
       ────────────────────────────────────────────────────
       ◇ ◇                       two empty sockets
         Resonance: Pure Fire (2)  — needs 2 Ember Shards                  (dim)

       roll quality 610‰
```

Reading it against the contract:

| Line | Rule |
|---|---|
| Five pips, the word **Fused**, and the colour | I1's three redundant channels, all present. Colour is never alone |
| `Crown`, not `head` | the item's frame is plant (§3.5); the role id `head-guard` is never shown |
| `Sinew 24 (24 + 0) — 24 gates` | I11's ask ([ssot-requirements.md:220-221](ssot-requirements.md)) — the composed value, the split, and which number gates |
| `Armour 18` has no bar | base stat, `Fixed`, no band to sit in (§3.4) |
| The implicit has no bar and its own rule | `Fixed`, and `sourceKind = implicit` |
| `+214 hp` renders as a bare integer | `GameUnits` — no context part, because the number *is* the effect |
| `+30 fire crit rate` carries `(≈ +7.4 pp vs neutral)` | `SigmoidPoints` — the reference is named, so the estimate is honest |
| Two roll bars, four items with none | exactly the `OnInstantiate` lines get bars |
| The one-away resonance names the ingredient and the count | §4.3 — `distance == 1`… |
| …and it does **not** offer Pure Fire (3) or (4) | the item has two sockets, so those are `distance = ∞` → `undiscovered`, never shown |

### 7.2 The units problem, rendered three ways

One affix — `keen_edge.fire.t3`, `+30` — on three surfaces.

| Surface | Reference | Rendered | Arithmetic |
|---|---|---|---|
| **Item card** | neutral, 0 vs 0 | `+30 fire crit rate (≈ +7.4 pp vs neutral)` | `σ(30/100) − σ(0) = 57.44 % − 50.00 %` |
| **Character sheet**, target has +80 crit resist | that target | `+30 fire crit rate (≈ +6.8 pp vs Conehead)` | `σ(−50/100) − σ(−80/100) = 37.75 % − 31.00 %` |
| **Lawn**, at the shipped −250 baseline | the lawn baseline | `+30 fire crit rate (≈ +2.4 pp at lawn baseline)` | `σ(−220/100) − σ(−250/100) = 9.98 % − 7.59 %` |

**Three honest answers, one authoritative number.** The `+30` is what the comparison table subtracts, on
every one of the three surfaces. The parenthetical changes because the world changed.

**What a naive renderer would have printed:** `+7.4% crit chance`, on all three surfaces, with no
reference. On the lawn — where the game is actually played — that is wrong by **3.1×**. It is also
wrong in a direction the player cannot detect, because the number looks like a percentage and
percentages look final.

**And the case the premise got backwards.** The same item's other line:

```text
+12 fire power
```

No context part, no conversion, no estimate. `combat.power.*` is a **flat additive damage term**
([OverlayCombatCalculator.cs:84-89](../../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs),
`:104`), so `+12 fire power` is +12 damage on a pure-fire hit — directly comparable with `+12 atk` in
the same arena. Rendering it with a sigmoid estimate would have invented a nonlinearity the engine does
not have.

### 7.3 A comparison that is a sidegrade

Extending I13's §7.4 example ([ssot-inventory.md:647-661](ssot-inventory.md)) with grouping and the
trade lists:

```text
Candidate: Sturdy Bark Helm of Sparks      vs      Held: Sound Thorn Crown of Rime

  Sidegrade ◆

  Damage and hit points (game units)
    hp                       71  →  62        −9
  Probability (sigmoid points)
    accuracy                  9  →  14        +5   (≈ +1.2 pp vs neutral)

  You gain          +5 accuracy
  You give up       −9 hp

  roll quality      540‰  →  610‰

  There is no single score. 9 hit points and 5 accuracy points are not the same currency.
```

| Element | Rule |
|---|---|
| Two unit groups, two headers, never one column | §4.2 |
| `Sidegrade ◆` — word **and** shape | never colour alone |
| The trade lists | the honest form of the number nobody can compute |
| The footnote is permanent, not dismissible | §4.2 |
| No power row | E9 has not shipped; SC9. When it does, it lands **above** the delta table as `≈ 1,300 (±25%)`, and the table stays |

### 7.4 A set at 3 / 4 and a word one insert away

```text
Set: Ember Legion                                                   3 / 4

  2 pieces   Ignite on hit — 4.0 s, 180‰ chance                     ● active
  3 pieces   +40 fire power                                         ● active
  4 pieces   +8% hp                                                 ○ needs 1 more
             missing: armament-primary

Sockets  ◈ ◈ ◇
  ◈ Ember Shard t3        ◈ Frost Bead t2        ◇ empty

  Resonance: Eclipse                                                ○ needs 1 light or dark insert
  Word: Kindling                                                    ○ known — 2 away
  Resonance: Pure Fire (2)                                          ○ needs 1 more Ember Shard
```

| Element | Rule |
|---|---|
| The **whole** set ladder renders, inactive included | §4.3 — inactive thresholds are the goal |
| `4 pieces`, never `Next tier` | the wording rule; the label survives an unequip |
| The missing role is **named** (`armament-primary` → rendered in the wearer's frame vocabulary) | I5's `item_set_member` is enumerable ([ssot-sets.md:370](ssot-sets.md)) |
| `Eclipse` and `Pure Fire (2)` are `one-away` — dimmed, named, ingredient stated | §4.3 |
| `Kindling` is `known-inactive` — name only, atoms hidden, distance stated | the player has held every ingredient; the atoms stay behind the chase |
| The 2-piece capability sits at the **lowest** threshold | I5's deliberate inversion ([ssot-sets.md:117-127](ssot-sets.md)) — the card shows it plainly, which is what makes that inversion legible |
| `4.0 s` and `180‰ → 18%` | Milliseconds and PerMilleRatio, §3.3 |

### 7.5 The failure this prevents, as it exists today

The shipped roster screen equips by **typing a container id into a text box**:

```tsx
// web/fusion-rpg-web/src/features/roster/RosterPage.tsx:33-34
const EQUIP_SLOTS = ["weapon", "armor", "trinket"] as const;
const STUB_HINT = "stub.atk_ring | stub.butter_bead | stub.hp_charm";
```

Three hardcoded slot names against I2's fifteen roles in two vocabularies, and a placeholder that shows
the player raw ids because there is nothing else to show them. That is not a hypothetical failure mode;
it is the current state of the only item UI in the tree, and it is what §6.3 case 1 exists to make
impossible to ship again.

---

## 8. Failure modes

### 8.1 Tooltips only the designer can read

**How it happens.** The renderer is written by whoever built the data layer, so it renders the data
layer: `stat.derived combat.crit.rate.fire Flat +30`. Every field is correct and no player can use it.

**Prevented by:** the family template is the *only* path to a player-facing line, and option C's
machine fallback is a **rejection at import** (§6.1 row 1), not a shipping default. The fallback exists
so a hand-edited database degrades visibly rather than blankly — and §6.3 case 1 iterates the whole
catalog asserting no line contains an id.

**Residual risk, stated:** a template can be *authored* badly — grammatical, substituting, and still
unreadable. No validation catches prose quality. The mitigation is that there are ~110 strings, which
is few enough that one person can read all of them in a sitting; 775 is not.

### 8.2 Numbers that contradict what the engine applies

**How it happens.** Two ways, and this tree has one of each. **(a)** The renderer recomputes a magnitude
instead of reading the frozen one, and drifts on a rounding boundary. **(b)** A unit conversion happens
on one side of a boundary and not the other — which is exactly the `Increased`/`More` gap in §3.2.5,
where SC4 says ‰ and `StatComposer` reads a fraction with no division found between them.

**Prevented by:** Rule 3 of §3.3 — the renderer reads `values_json` and never recomputes — and §6.3
case 2, which asserts rendered-equals-applied over a seeded instance for every atom. Case 2 is written
specifically to go **red** if the `Increased` gap is real, which converts an argument into a build
result.

**Not prevented:** if the engine itself is wrong, the test passes and both are wrong together. That is
why §3.2.5 files the claim with R1 rather than silently rendering around it.

### 8.3 A per-atom string table nobody maintains

**How it happens.** Diablo-scale content, a string per row, no validation, and by the third content
patch a third of the items read as ids. **This repo is already one step in.** `effect_atom.name` is a
shipped, unvalidated, undocumented display column ([AtomRow.cs:31](../../../src/FusionRpg.Core/Effects/Atoms/AtomRow.cs));
`AtomRowValidator` never looks at it; the roster screen shows players `stub.atk_ring`
(`RosterPage.tsx:34`).

**Prevented by:** 110 strings instead of 775 (§3.1); a missing template is a rejection, not a blank; and
`effect_atom.name` gets a definition and a check (§5.2, §6.1 row 14) rather than staying a free field.

**The honest residual:** ~110 strings is small, but the *string catalog* can still rot — an orphan key,
a key whose template no longer matches its family's params. §6.1 rows 4, 5 and 11 are the lints, and
row 11 is deliberately a **warning**: an orphan key is untidy, not broken, and making it fatal would
mean an author cannot stage a string before its family lands.

### 8.4 So much hidden behind expansion that comparison is impossible

**How it happens.** The card is cleaned up, magnitudes move behind a "details" toggle, and now comparing
two items takes six clicks. Players build spreadsheets. This is the most common form of a *pretty*
tooltip being a worse tooltip.

**Prevented by:** the disclosure rule — *nothing that can differ between two items of the same base type
may be hidden* (§4.1). Everything hidden is invariant explanation: the band an affix could have rolled
in, the full resonance catalog, the set's member list. What is on the face is exactly what varies.

**And the second half:** the compact list line carries pips, name, role, roll bar and a delta arrow, so
the *list* answers the comparison question before the card is even opened.

### 8.5 A percentage that is a lie

**How it happens.** Someone reasonably observes that `+30 crit rate` means nothing to a player, converts
it once against whatever baseline the test fixture had, and ships `+7.4% crit chance`. It looks
authoritative. It is wrong by 3.1× on the lawn (§7.2), and no player can tell.

**Prevented by:** the context part must name its reference, or it is not emitted (§3.2.4 rule 4). A
number with a stated denominator can be checked; a number without one cannot.

**And the harder half:** `Flat` unit classes are forbidden from producing a context part at all, so
nobody "helpfully" adds a percentage to `+45 hp` — which is the same mistake in the other direction.

### 8.6 A second renderer appears and the two drift

**How it happens.** The overlay wants an item card, the SPA is "too heavy", somebody draws one in Unity
IMGUI, and six months later the two disagree about rounding.

**Prevented by:** both overlay hosts load the same SPA
([overlay-spec.md:15](../../launcher/overlay-spec.md)), and this document states that the Unity IMGUI
surface draws a *button*, never an item. If a native card is ever genuinely needed, it consumes
`DisplayModel` over the wire — the model is structured data precisely so a second *view* is possible
without a second *renderer*.

### 8.7 Localisation retrofitted after three hundred concatenations

**How it happens.** English-only ships, strings get glued in components because it is faster, and by the
time a second language is wanted the retrofit is a month of finding `"+" + value + " " + noun`. The tree
is already carrying the precondition: Chinese literals in `DemonSpeciesCatalog.Generated.cs` and English
literals in `DemonTraitCatalog.cs`, both hardcoded, with i18n out of scope in the web spec.

**Prevented by:** S1–S5 (§3.6). One language, but every string is a key with a named-placeholder
template, and §6.3 case 6 asserts the plural policy so it cannot quietly rot into `+ "s"`.

**Not prevented, and stated:** this lane's rules bind *this lane's* strings. `DemonTraitCatalog`'s
English literals and `DemonSpeciesCatalog`'s Chinese ones are outside it, and bringing them behind keys
is a separate, small, unscheduled job (§9.18).

### 8.8 The card grows until it is a spreadsheet

**How it happens.** Eleven blocks is already a lot. Add durability, add a market price, add a comparison
mini-table, and the hover card is a page.

**Prevented by:** the block list in §4.1 is **closed**, and adding a block is a reviewed change for the
same reason adding an atom kind is. The compact line is the pressure valve — anything that wants to be
"available at a glance" goes there, where there is room for exactly five fields and no more.

---

## 9. What this lane needs from other lanes

1. **I1 (rarity)** — N2's `color_hex`, `pip_count`, `display_key` are the card header's three channels
   and I consume them unchanged. Two things: **(a)** confirm the light-theme gap
   ([ssot-rarity.md:373-374](ssot-rarity.md)) stays a known gap — the overlay renders over a game frame
   whose brightness we do not control, so a dark-surface-tuned palette is a real constraint on the
   overlay and not only on the web UI. **(b)** The roll-quality bar deliberately does **not** use the
   rarity ramp (§3.4); confirm no other card element is expected to.

2. **I2 (equip slots)** — the 32 frame slot names ([ssot-equip-slots.md:83-99](ssot-equip-slots.md)) must
   land in the string catalog as **keys**, not literals, per L3. And I have committed that the slot name
   follows the **item's** frame, not the wearer's, so a humanoid gauntlet on a hybrid reads `hands`
   (§3.5). Confirm or overturn — it is a fiction call, not a mechanical one, and it is yours.

3. **I3 (item categories)** — three asks. **(a)** `display_json` is specified as *"name parts, icon
   key"* ([ssot-item-categories.md:285](ssot-item-categories.md)); I need a **name key** rather than
   parts, or the base type name is the one header field that cannot localise. **(b)** Base stats sit at
   `seq 0` and the implicit at `seq 1` (`:290`); I need a way to tell them apart at render time that is
   **not** "seq 0 and seq 1" — a marker, or a documented guarantee I may rely on. Positional convention
   in a `seq` column that also carries authoring order is fragile. **(c)** The class noun (`armour`,
   `weapon`, `jewel`, `off-hand`) appears in the header; confirm `class_id` maps to a display key.

4. **I4 (sockets)** — two, and the first is the important one. **(a)** Extend the combination
   evaluator's return with a **`distance`**, rather than adding a second near-miss function. Your §4.6
   already makes evaluation *"a pure, ordered function of `(socket contents, socket affinities,
   catalog_revision)`"* ([ssot-sockets.md:277-279](ssot-sockets.md)); one function with a distance is a
   small extension and two functions is how *"it said one more and it did not fire"* happens. **(b)**
   The `omni`-counts-toward-Diversity-only rule (`:245-248`) must be renderable **on the omni insert's
   own line**, because an omni gem sitting in a three-fire fill that is not firing Pure looks broken.

5. **I5 (sets)** — **(a)** `item_set.display_name TEXT NOT NULL` ([ssot-sets.md:345](ssot-sets.md)) is a
   literal; make it `display_key`, per L3 and per the precedent I1 and G1 already set. **(b)** You
   already grant the tooltip permission to enumerate `item_set_member` (`:370`); I rely on it to render
   the **missing role names**, not just `3 / 4`. Confirm that is a supported read and not an incidental
   one.

6. **I6 (enhancement)** — your open question 1 ([ssot-enhancement.md:759-762](ssot-enhancement.md)) picks
   `+12` as a **name prefix** because the left edge gets scanned. I1's pips want the same edge. I render
   **pips, then `+12`, then the name**, because pips are an accessibility channel and must not be
   displaced. That is a rendering call inside your decision, not a reversal of it — flag it if it
   conflicts with what you meant. Also: your §5.5 one-clean-block rule is adopted verbatim as card
   block 6.

7. **I7 (reroll)** — two render states I need and cannot derive: **(a)** a **locked** affix, so a player
   can see what a reforge will preserve; **(b)** a **just-rerolled** line, distinguishable for one
   session so the player can see what changed. Neither needs a schema column if the reroll response
   carries them; say which.

8. **I8 (affixes)** — **(a)** `item_affix_name` already names the tooltip as a consumer
   ([ssot-affixes.md:836](ssot-affixes.md)); I need the naming function to return a **key plus args**,
   not a composed string, or the item's *name* is the one line on the card that cannot localise (S2).
   **(b)** Confirm the A/B/C name bands are never player-visible — I do not render them. **(c)** Two of
   your groups, `g.elem-power`'s `affliction` and `g.ward`'s `stalwart`, sit on the `status.power.*` /
   `status.resist.*` channels whose semantics §3.2.5 could not explain. **Your tier bands on those two
   families are blocked** until that resolves, because the smallest authorable integer is a 2×
   multiplier. **(d)** Your §4.1 group ids are the card's affix ordering key; confirm the order within
   the prefix and suffix halves is the table order.

9. **I9 (cost)** — salvage yield appears in the card footer and in the salvage preview. I need it as
   **value plus unit**, not a rendered sentence, so it groups under §4.2's unit rule.

10. **I11 (requirements)** — the requirement line must say **which** number gates, which is your own ask
    ([ssot-requirements.md:220-221](ssot-requirements.md)). I need the **unassisted** and **composed**
    values as separate numeric fields plus the attribute key — not a pre-rendered `Sinew 32 (29 + 3)`
    string, which cannot localise and cannot be coloured per-part.

11. **I12 (generation)** — the loot toast is this renderer at a smaller size, so it inherits everything
    here. One check: confirm nothing in generation produces a **display-only** property — a "sparkle
    tier", a drop-banner class — because such a thing would be a second display path with its own
    lifecycle and it belongs here if it exists.

12. **I13 (inventory)** — **(a)** the `unit` field on each delta row
    ([ssot-inventory.md:437](ssot-inventory.md)) must be the **`UnitClass` enum value**, not a free
    string, or §4.2's grouping rule cannot be enforced and §6.3 case 3 cannot be written. **(b)** I own
    how `sidegrade` and `incomparable` render, including the trade lists and the reason sentence;
    confirm the payload carries enough to name *why* two items are incomparable, or I will have to
    recompute it. **(c)** Your best-in-role heuristic (`:491-503`) is explicitly labelled crude; when it
    appears on a card it must carry that label, and I need a flag on the payload rather than hardcoding
    the caveat.

13. **G1 (uniques)** — I commit to your §9.12: identity lines and the variance line are distinguished by
    `sourceKind` combined with `RollPolicy` (§4.4), and `flavour_key` renders as card block 10. One
    ask back: confirm that `Fixed` versus `OnInstantiate` on the core atoms is sufficient to make that
    split, or whether `item_unique` needs an explicit marker. Also — a unique's authored name bypasses
    I8's grammar (`ssot-affixes.md:783`), so it needs a **name key in two frame vocabularies**, which is
    an authoring cost on you rather than a mechanism cost on me.

14. **G4 (granted actions)** — I commit to your §9.6: the **battle-only** tag, the **already known**
    state, and the action's own name and description from `rpg_action` all render as card block 9. Ask
    back: `rpg_action`'s name and description must be **keys**, per L3. And the battle-only tag needs to
    be visible in the **compact list line** too, not only the card — a player scanning an armoury should
    not have to open each item to learn that half of them are inert on the lawn.

15. **E1 (atom kind registry)** — `UnitClass` and the channel→unit map belong **in code beside
    `ParamSchema`**, by that module's own code-or-data constitution
    ([spec-atom-kind-registry.md:19](../effect-atom/spec-atom-kind-registry.md)). I am asking for it
    there rather than minting a parallel table, because a unit declared away from its reader drifts from
    it silently and the drift is invisible. Concretely: `ChannelUnits.For(channelId)`, prefix-matched so
    a new element needs no new entry, plus `MissingUnitClass` as a load rejection.

16. **E4 / E14 (schema, importer)** — register `item_display_template` and the string-catalog file
    digest in the content-hash registry. That is an explicit `contentHashSchemaVersion` bump
    (definitions §8), not a silent addition. And `effect_atom.name` becomes `NOT NULL` with a format
    check (§5.2), which is a change to a shipped column and therefore an ask under E4's *"ask first:
    adding a column"* boundary — it is not adding one, but it is tightening one.

17. **R1 (defect register)** — **three claims, all read from source, none executed**, per the design
    gate's evidence rule:
    - **C1 — `Increased`/`More` unit boundary.** SC4 says ratios are integer ‰;
      `StatComposer.cs:25-32` reads them as fractions; no `/1000` found in `AtomCompiler`. If real, a
      `+15%` affix composes as ×151. *Repro sketch: author a `stat.modify` atom with
      `op: Increased, amount: 150`, bind it, read the composed `maxHp`.*
    - **C2 — `status.power.*` is an unbounded direct multiplier.**
      `ResistanceEvaluator.cs:212-217` returns `clamp(delta, 0, 10000)` and `:164-165` multiplies both
      magnitude and duration by it; the ungeared baseline is `1.0`. If real, `+1 status power` doubles
      every status. *Repro sketch: evaluate one status with and without a single point of
      `status.power.omni`.*
    - **C3 — `effect_atom.name` is an unvalidated display column.** `AtomRowValidator` never inspects
      it; an atom with an empty name loads clean. Low severity, trivially confirmable, and it is the
      seed of failure mode 8.3.

18. **The web stream** — **one renderer, two hosts.** The SPA is the only item-card renderer; both
    overlay hosts load it ([overlay-spec.md:15](../../launcher/overlay-spec.md)). No Unity IMGUI item
    card. Two consequences: the shared per-mille helper moves out of `features/demons/patronView.ts`
    into a shared display module and `patronView` calls it; and `RosterPage.tsx`'s three-slot,
    type-the-id equip UI (`:33-34`, `:274-300`) is replaced rather than extended when I2's fifteen roles
    land. Separately and outside this lane: `DemonTraitCatalog`'s English literals and
    `DemonSpeciesCatalog`'s Chinese ones should eventually move behind keys (§8.7).

---

## 10. Open questions for the owner

1. **Does the context part appear on the item card at all, or only on the character sheet?** I shipped
   it on both, with different references (§3.2.4). The cheaper and more conservative answer is
   **sheet-only** — the card then carries bare points and the player learns the scale from the sheet.
   That is one less number per line on the busiest surface, at the cost of a new player having no idea
   what 30 crit rate buys. I picked "both" because the second cost is the one that loses players.

2. **Pips or `+12` at the left edge?** I6 committed to `+12` as a name prefix because the left edge gets
   scanned ([ssot-enhancement.md:759-762](ssot-enhancement.md)); I1's pips are the colour-blind
   accessibility channel and want the same place. I render **pips, then `+12`, then the name** (§4.1).
   Both cannot be first, and this is an aesthetic call in a place where accessibility has a claim.

3. **Is the string catalog a file or a table?** I picked a **file** (`content/display/en.json`) for v1
   because it is edited as a unit, reviewed as a diff, and has no per-row lifecycle (§5.3 N2). A table
   would be consistent with everything else in the content layer and would join the content hash the
   same way every other table does. Reversible either way; I chose the cheaper one.

4. **Do we ever show tier numbers?** I say never (§2.4) — the roll bar and the magnitude carry
   everything, and "T4" is an invitation to a wiki. The counter-argument is real: PoE players *want*
   tier numbers and read them fluently, and hiding them makes crafting opaque. If crafting (I7, I6)
   turns out to need them, they belong on the expansion, never on the face.

5. **`status.power.*` cannot be rendered until its mechanism is settled** (§3.2.5, claim C2). This is
   not a display preference; it is a request for a decision. If the multiplier is intended, `affliction`
   and `stalwart` are the strongest affix families in the game by an order of magnitude and I8's tier
   bands must be written knowing that. If it is not, `ComputeNetFactor` needs a shape and until it has
   one those two families stay `status = 'pending'`.

6. **Light theme for the overlay.** I1's palette is explicitly tuned for a dark surface
   ([ssot-rarity.md:373-374](ssot-rarity.md)) and its own §10.7 defers the light pass. The overlay
   renders over a running game whose frame brightness we do not control. Not urgent; worth knowing it is
   owed.

7. **When E9 lands, is the power number shown at all?** I committed to `≈ 1,300 (±25%)` under Rule P
   (§3.3), because a number documented as 12.5 % wrong on multiplicative pairs must not render as four
   confident digits. The defensible alternative is to **suppress power on the card entirely** until the
   tolerance tightens, and show it only in a debug or sort context. A visible ±25 % band is honest and
   also slightly demoralising to look at.

---

## Design-gate checklist

| Box | State |
|---|---|
| Read [DESIGN-GATE.md](../../DESIGN-GATE.md)'s topic index for this subsystem | ✅ read the required chain in this session: [enrichment-contract.md](enrichment-contract.md), [reconciliation-plan.md](reconciliation-plan.md), [definitions.md](../effect-atom/definitions.md) §0–§10, [atom-family-library.md](../effect-atom/atom-family-library.md), [spec-atom-schema.md](../effect-atom/spec-atom-schema.md), [spec-container-schema.md](../effect-atom/spec-container-schema.md), [spec-atom-kind-registry.md](../effect-atom/spec-atom-kind-registry.md), and the six sibling lane docs the brief named |
| Verified against **code**, not comments | ✅ the units finding (§3.2.1) comes from reading `OverlayCombatCalculator`, `ShieldRuntime`, `ShieldMath`, `ResistanceEvaluator`, `CombatPolicies` and `CombatProbability`, not from the docs — and it **contradicts** two documents that both assert one unit |
| Arithmetic checked, not asserted | ✅ every sigmoid figure in §3.2.2 and §7.2 computed from the shipped formula; the definitions' own 7.6 % → 26.9 % calibration reproduces exactly, which is the sanity check that the model was read correctly |
| Constraints **tested**, not assumed | ⚠️ **partial, and stated.** The three defect claims in §9.17 are read from source and **not executed** — no repro was run and no suite was invoked. Each carries a repro sketch, and R1 owns confirming them. Everything I *design* works whether or not they are confirmed |
| A comment is not evidence | ✅ `ValueSpec.cs:25-26`'s doc comment asserts `+10 fire power` is a sigmoid resolver point. The code it documents does not support that. Code beat the comment |
| Read the section, not the line | ✅ definitions §2's *"resolver points — sigmoid scale, `AccuracyScale = CritRateScale = 100.0`"* names two scales and is correct about both; it is the family library's §2a generalisation to *power* that does not hold |
| Options considered before committing | ✅ §3.1 three, §3.2.3 five, §3.4 four, §3.6 the shape of each rule argued |
| Boundaries respected | ✅ no other lane's file edited; no git write command run; exactly one file written |
