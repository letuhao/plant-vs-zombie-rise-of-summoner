# Magnitude and unit rendering — the number contract for every player surface

**Status:** Detail design, 2026-08-22. **Document 1 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Written first because it is the spine: it
closes six of that audit's eight Class-B defects and every other document consumes it.

**Adopts, does not re-derive:** [`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md)
§3.2 — the unit ledger and the two-part line are its work. This document does three things that one
does not: it **verifies every unit class against its consumer in `src/` in this session**, it **states
the TypeScript contract** the FE binds to, and it **corrects the four-family `Magnitude` type** the
stack doc already committed to.

**Corrects:** [tech-stack.md:201-208](tech-stack.md) · [web/spec.md:177](../web/spec.md) ·
[00-foundation.html §D.1](00-foundation.html).

---

## 1. Scope

**Owns:** how a number reaches a player's eye. The unit ledger, the authoritative/context split, the
per-mille and rounding rules, precision, `Increased` vs `More`, roll-quality rendering, and the guards
that keep the rendered number equal to the applied one.

**Does not own:** which numbers are *balanced* (nobody, yet — E9's sweep), the comparison **algorithm**
(I13), affix words and item naming (I8), or layout (documents 2–9).

---

## 2. The finding that forces this document

[definitions.md §2](../architecture/effect-atom/definitions.md) declares derived-channel magnitudes to
be *"resolver points — sigmoid scale."* [tech-stack.md:203](tech-stack.md) encodes that as a
`resolverPoints` family. **Verified against code this session, that is true for six of the twelve
combat families and false for the other six.**

`CombatDerivedReader.Power` has **exactly one call site in `src/`** —
[OverlayCombatCalculator.cs:84](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs) — and the
path from it is:

```csharp
var power  = CombatDerivedReader.Power(request.Attacker.Derived, c.Element);   // :84
var defense= CombatDerivedReader.Defense(request.Defender.Derived, c.Element); // :85
var effectiveDelta = (power - defense) + componentBonus;                       // :86
weightedDelta += c.Weight * effectiveDelta;                                    // :87
...
var powerAdjusted = request.BaseOverlayDamage + weightedDelta;                 // :105
```

**No sigmoid.** `+10 fire power` is `+10` damage on a pure-fire hit — `+10 hp`'s peer, not its
order-of-magnitude cousin. The sigmoid appears only at `:91`, `:95` and `:99`, on accuracy/dodge,
crit rate/resist, and crit damage.

> **A code comment currently says the opposite.**
> [ValueSpec.cs:24-26](../../src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs) reads: *"`+10 fire power` is
> ten resolver points on a sigmoid scale where CritRateScale is 100.0, so ten points is 0.1 sigmoid
> units."* That is the same error, in the file most likely to be read by whoever authors the next
> magnitude. **Flagged for correction; a comment is not evidence
> ([DESIGN-GATE.md §3.2](../DESIGN-GATE.md)), and this one is wrong.**

Left uncorrected in the FE, a single `resolverPoints` family renders six families **10× wrong** and
puts `+9 hp` and `+5 accuracy` in the same numeric column.

---

## 3. The unit ledger — twelve classes, each verified

Every derived and primary magnitude belongs to exactly one class. The right-hand column is the consumer
I read this session; a channel whose consumer I could not name does not get a class, it gets a
rejection (§8).

| `UnitClass` | Renders | Context part | Channels | Verified consumer |
|---|---|---|---|---|
| `GameUnits` | `+45 hp` · `+12 fire power` | none | `hp` `maxHp` `atk` `defense` `arm1` `arm1Max` `arm2` `arm2Max`; `combat.power.*` `combat.defense.*` `combat.shield.capacity.*` `combat.shield.pen.*` `combat.shield.toughness.*` | [OverlayCombatCalculator.cs:84-87,105](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs) · [ShieldRuntime.cs:121-123](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) (`maxHp = grant.BaseHp + capacity`) · [ShieldMath.cs:34](../../src/FusionRpg.Core/Combat/Shield/ShieldMath.cs) (`raw = input + elemMod + hitCount * breakerDelta`) |
| `GameUnitsPerSecond` | `+3 shield hp/s` | none | `combat.shield.regen.*` | [ShieldRuntime.cs:403-410](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs) — `ratePm = regen × 1000`, then `carry += ratePm * deltaMs / 1000` |
| `SigmoidPoints` | `+30 crit rate` | `≈ +7.4 pp vs <ref>` | `combat.accuracy.*` `combat.dodge.*` `combat.crit.rate.*` `combat.crit.resist.*` | [OverlayCombatCalculator.cs:91-95](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs) via [CombatProbability.cs:8-9](../../src/FusionRpg.Core/Combat/CombatProbability.cs), scale `100.0` |
| `SigmoidMultiplierPoints` | `+40 crit damage` | `≈ ×1.60 vs <ref>`, **and the line states the ceiling** | `combat.crit.damage.*` `combat.crit.resist.damage.*` | `:99` — `critMultFinal += weight * (1.0 + Sigmoid(delta, 100.0))`. Sigmoid ∈ (0,1) and the weights sum to 1, so the multiplier is bounded **(1.0×, 2.0×)** |
| `StatusPotencyPoints` | `+8 blight potency` | **suppressed** — §4.3 | `status.power.*` `status.resist.*` | [ResistanceEvaluator.cs:155-165,190-217](../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs) |
| `PerMilleRatio` | `+15% hp` (Increased) · `×1.15 hp` (More) | none | `op` amounts, `chance`, shares | [StatComposer.cs:24-31](../../src/FusionRpg.Core/Stats/StatComposer.cs) |
| `Milliseconds` | `4.0 s` · `250 ms` under one second | none | durations, `icd_ms` | authored ms ([definitions.md §2](../architecture/effect-atom/definitions.md)) |
| `Count` | `2 bullets` | none | `count`, `maxTargets` | atom param schemas |
| `Flag` | present / absent, **never a number** | none | `status.immune.{tag}` `status.immuneReduction.{tag}` | [DerivedStatRegistry.cs:92-104](../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs) — `MaxPriorityFlag`, cap `1` |
| `LadderIndex` | `Θ 20` | `→ 680 power` — **exact, not an estimate** (§3.2) | `progression.power` `progression.realm` | [ResistanceEvaluator.cs:190-217](../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs) reads it **linearly** as a contest delta; `PowerLadder.Value(Θ)` reads it as `P(Θ)` for magnitudes |
| `AptitudePoints` | `Might 55` | `→ +2,200 omni power` — **an estimate, allowed only on a surface with a real allocation** (§3.2's precedent; class-system/spec-primary-stats.md §3.2) | the twelve aptitudes (sources, never registered channels — class-system-map.md §2aa) | Read by both PS-3 functions the aptitude-tuning module owns; class-system, authorised 2026-08-26 |
| `ReciprocalPoints` | `Onslaught 40 penetration` | an estimate, same suppression rule as `StatusPotencyPoints` | `combat.penetration` `combat.absorption` `combat.amplification` `combat.reduction` | [OverlayCombatCalculator.cs](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs)'s mitigation chain — `PierceFactor`/`AmpFactorReciprocal`, both asymptotic rather than sigmoid; class-system/spec-unit-class-close.md §3.3/§3.5, authorised 2026-08-26 |

**Twelve, not four** (nine when this document was written; `LadderIndex` added 2026-08-24;
`AptitudePoints`/`ReciprocalPoints` added 2026-08-26 by the class-system program). [tech-stack.md:201-208](tech-stack.md) and [web/spec.md:177](../web/spec.md) declare
`gameUnits · resolverPoints · permille · ms`. `resolverPoints` must be **split into the three real
behaviours** (`SigmoidPoints`, `SigmoidMultiplierPoints`, `StatusPotencyPoints`), the six flat families
moved to `GameUnits`, and `GameUnitsPerSecond`, `Count`, `Flag`, `LadderIndex`, `AptitudePoints` and
`ReciprocalPoints` added. Both files are corrected by this document.

> **Tenth class added 2026-08-24.** The ledger shipped with nine and had no class for `Θ`, which is the
> most load-bearing derived channel in the game — the `derived-stats` program found the hole while
> classifying 157 new channels and could not assign `progression.power` a class at all. `Magnitude.unit`
> is a **required** field, so `Θ` was not expressible in the render contract. Owner authorised the
> addition the same day.
>
> **Eleventh and twelfth classes added 2026-08-26** (class-system program): `AptitudePoints`
> (spec-primary-stats.md §3.2) and `ReciprocalPoints` (spec-unit-class-close.md §3.3/§3.5), both
> authorised the same day they were proposed, same terms as `LadderIndex`.
>
> **Contract change landed 2026-08-26:** the `UnitClass` union in
> [contract/types.ts](../../web/fusion-rpg-web/src/contract/types.ts) gains all three strings in one
> edit — `"ladderIndex"` (owed since 2026-08-24), `"aptitudePoints"`, `"reciprocalPoints"`.

### 3.1 One rule that falls out and will otherwise be broken

**A `GameUnits` derived channel keeps its arena in the noun.** `+12 fire power` is 12 damage *on a fire
component*, weighted by that component's share of the payload
([OverlayCombatCalculator.cs:87](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs)). A bare
`+12 damage` over-promises on a mixed-element hit. The element is part of the unit, not decoration.


### 3.2 `LadderIndex` — the one class whose context part is a fact

Every other context part is an **estimate against a named reference** (§4.2) and must be rendered with
that hedging. `LadderIndex` is the exception, for two reasons that are worth stating rather than
rediscovering:

**Θ is read two different ways, and the player needs both.** Contests read it **linearly** — what
matters is `Θ_you − Θ_them`. Magnitudes read **`P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`**, which is quadratic
([power/ssot-power-scale.md](../architecture/power/ssot-power-scale.md) §4). Showing only the index
hides how fast it compounds; showing only `P(Θ)` hides that contests do not compound at all. **Both
parts, always.**

**The context part is exact.** `P(20) = 680` is the shipped pin, not a sample against a reference
specimen. So `LadderIndex` renders `→ 680 power` with **no `≈` and no `vs <ref>`** — the only class in
this table that does. Rendering it with the hedging the other classes require would tell the player a
true number is a guess.

**`progression.realm` is pinned at `1.0` permanently** (ADR P1 — realm advancement is additive in `Θ`,
never a contest multiplier). It carries this class for contract completeness and renders as `stub`
per [spec-derived-stat-sheet.md](spec-derived-stat-sheet.md) §3, not as a live index.
---

## 4. The two-part line

> Every magnitude renders as an **authoritative part** and an optional **context part**, and the two are
> visually distinct. The authoritative part is what the engine holds. The context part is an estimate
> **against a named reference**, and it is never the only number shown.

### 4.1 Why a bare percentage is refused

For the four `SigmoidPoints` channels, `p = 1/(1 + e^(−delta/100))`. Computed from the shipped formula,
the marginal value of **+10 points**:

| Opposed delta | Before | After +10 | Gain |
|---:|---:|---:|---:|
| −250 (shipped lawn baseline) | 7.59 % | 8.32 % | **+0.73 pp** |
| −150 | 18.24 % | 19.78 % | +1.54 pp |
| −50 | 37.75 % | 40.13 % | +2.38 pp |
| **0 (neutral)** | 50.00 % | 52.50 % | **+2.50 pp** |
| +150 | 81.76 % | 83.20 % | +1.44 pp |
| +250 | 92.41 % | 93.09 % | +0.67 pp |

*Computed this session; reproduces [definitions.md §2](../architecture/effect-atom/definitions.md)'s
calibration exactly — delta −250 → 7.59 %, and +150 points → 26.89 %.*

**One affix, a 3.4× spread.** Printing one of those numbers asserts an opponent the player has not met.
So the percentage is never the authoritative part, and it never appears without its reference.

`SigmoidMultiplierPoints` behaves the same way and additionally saturates:

| delta | `critMult` |
|---:|---:|
| −250 | ×1.076 |
| 0 | ×1.500 |
| +40 | ×1.599 |
| +250 | ×1.924 |

The ceiling is real and must be on the line — a player stacking crit damage past `+250` is buying
almost nothing, and nothing else on the card would tell them.

### 4.2 The two references, and where each is allowed

| Reference | Definition | Allowed on |
|---|---|---|
| `neutral` | opposed delta `0` | **the card** — an item has no opponent, so this is the only honest fixed reference |
| the selected specimen | the actor's live opposed delta | **the actor sheet only**, where an actor is selected. One `exp()` per channel, memoised |

**Never** render a context part against the lawn baseline as though it were general. It is a profile
constant, not a property of the affix.

### 4.3 The one class whose context part is suppressed, and why

`StatusPotencyPoints` renders its raw magnitude with a `status potency` noun and **no context part**.

Read from the shipped path: `delta = totalPower − totalResist`
([ResistanceEvaluator.cs:190-210](../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs)), and that
delta then feeds **two different things** — `Sigmoid(delta / effectiveApplyScale)` decides *whether* the
status lands (`:155`), while `netFactor = Clamp(delta, 0, 10000)` (`:212-217`,
[StatusPolicy.cs:10-11](../../src/FusionRpg.Core/Status/StatusPolicy.cs)) multiplies **both magnitude
and duration** (`:164-165`).

With `ResistFromPowerRatio = 0.0` and `TierPower` defaulting to `1.0`, an ungeared actor sits at
`delta = 1.0` → `netFactor = 1.0`. **So `+1 status power` doubles every status the wearer applies, in
both strength and duration.** Magnitudes here are integers, so the smallest authorable roll on this
channel is a 2× multiplier.

That is either a deliberate and extraordinary affix class or a normalisation that was never written.
**This document will not render a number it cannot explain**, so the context part is suppressed and the
family is marked `pending` until the question is answered. This is a **display** decision that costs
nothing and hides nothing — the raw magnitude is still on the face of the card.

---

## 5. The formatting rules

**R1 — per-mille never reaches the player.** Content is integer ‰
([definitions.md §2](../architecture/effect-atom/definitions.md)). Adopt the **shipped** helper rather
than writing a second one — [patronView.ts:23](../../web/fusion-rpg-web/src/features/demons/patronView.ts):
divide by 10, one decimal, trim a trailing `.0`. It moves into the shared display module and
`patronView` calls it instead of owning it.

Verified output: `4‰ → 0.4%` · `150‰ → 15%` · `185‰ → 18.5%` · `250‰ → 25%` · `1000‰ → 100%`.

**R2 — never render a non-zero per-mille as `0%`.** Round **away from zero** at the display boundary, the
same direction the engine uses
([CurveTable.cs:103-115](../../src/FusionRpg.Core/Effects/Atoms/CurveTable.cs) `DivRoundHalfAway`). A
real bonus never vanishes.

**R3 — rounding happens exactly once, at the display boundary, and never feeds back.** The renderer
receives the **frozen integer** and formats it. It never re-applies a curve, never re-rolls, never
recomputes. The engine applies its curve *before* the roll, so a second application would produce a
number the engine never held.

**R4 — `Increased` and `More` never share a glyph.**

```csharp
var increased = list.Where(m => m.Op == ModifierOp.Increased).Sum(m => m.Value);  // StatComposer.cs:25
var afterInc  = afterFlat * (1.0 + increased);                                    // :29
foreach (var m in more) afterMore *= 1.0 + m.Value;                               // :31-32
```

`Increased` **sums, then applies once**. `More` **multiplies separately**. Two `Increased` 15 % affixes
give ×1.30; two `More` 15 % affixes give ×1.3225. Rendering both as `+15%` erases a mechanic a player
would otherwise learn correctly in five minutes. → `Increased` renders `+15% hp`; `More` renders
`×1.15 hp`.

**R5 — precision never exceeds the source's claimed accuracy.**

| Source | Renders as |
|---|---|
| A frozen integer | exactly — no rounding at all |
| A per-mille | one decimal; 1‰ is its resolution |
| A duration | `250 ms` below one second, `4.0 s` above, one decimal |
| A sigmoid context read | one decimal in pp, prefixed `≈` |
| E9's power scalar, when it exists | **two significant figures with its band** — `≈ 1,300 (±25%)`. [definitions.md §7](../architecture/effect-atom/definitions.md) sets drift tolerance at ±25 % and documents the formula as knowingly 12.5 % wrong on multiplicative pairs. Four digits would be false confidence |

---

## 6. Roll quality

The **number** is I13's (‰, where the rolled value sits inside the atom's authored `[Min, Max]` after
curve scaling). This document owns only the rendering.

**Five segments**, because ten are not countable at a glance and inline ‰ competes with the magnitude —
which is the number the player is actually buying:

```text
segments = clamp(ceil(qualityPerMille * 5 / 1000), 1, 5)   // a non-zero roll never shows empty
```

**Which lines get a bar at all** falls straight out of `RollPolicy`
([ValueSpec.cs:8-19](../../src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs)) — and this is the part that
makes the bar honest:

| `RollPolicy` | Bar? | Renders |
|---|---|---|
| `Fixed` (`Min == Max`) | **no bar** | the value. A full bar here would be a lie about the item's luck — nothing could have rolled otherwise |
| `OnInstantiate` | **bar** | the frozen value, with `[Min, Max]` on expansion |
| `OnApply` | **no bar** | the **band, not a point** — `100–200 fire damage on hit`. The item did not roll it; the hit does |

The code agrees: [Instantiator.cs:15-19](../../src/FusionRpg.Core/Effects/Atoms/Instantiator.cs) — *"`Fixed`
values are copied verbatim; `OnApply` values are **left unresolved** — they belong to the hit, not to the
item."* A bar on an `OnApply` line would be rendering a roll that has not happened.

**Colour: the neutral→sun ramp, never the rarity palette.** I1 made *lightness* the rarity ladder; a
second lightness ladder inside the same card competes with it.

---

## 7. The contract

```ts
type UnitClass =
  | 'gameUnits' | 'gameUnitsPerSecond'
  | 'sigmoidPoints' | 'sigmoidMultiplierPoints' | 'statusPotencyPoints'
  | 'perMilleRatio' | 'milliseconds' | 'count' | 'flag'

type Magnitude = {
  unit:     UnitClass
  value:    number          // the frozen integer the engine holds. Never pre-formatted
  channel?: ChannelId       // required for gameUnits / sigmoid* — carries the arena (§3.1)
  op?:      'flat' | 'increased' | 'more'   // required for perMilleRatio (R4)
}

type ContextRead = { reference: 'neutral' | { specimenId: string }; text: string }

type DisplayLine = {
  key: string; args: Record<string, Magnitude | string>   // never a finished sentence
  unit: UnitClass
  context?: ContextRead                                    // absent ⇒ no context part
  rollPolicy: 'fixed' | 'onInstantiate' | 'onApply'
  rollQualityPerMille?: number                             // only when rollPolicy === 'onInstantiate'
  sourceKind: SourceKind                                   // 12 values — document 2
  groupOrder: number
}

function formatMagnitude(m: Magnitude, locale: string): string
```

**There is no overload accepting a bare `number`.** That omission *is* the GG-46 guard — an unlabelled
magnitude cannot be passed, so it cannot be rendered. Keeping `value` a raw integer rather than a
pre-formatted string is what makes R3 structurally true: the renderer is the only place formatting can
happen.

`args` holds `Magnitude` objects, not strings, so a translator reorders a sentence without ever touching
a number ([web/spec.md §6](../web/spec.md)).

---

## 8. Guards

Six, and the first three are the ones that make the rest trustworthy.

| # | Guard | Fails when |
|---|---|---|
| 1 | **Every channel with a reader declares a `UnitClass`** | a channel is renderable and unclassified. Iterates all 99 registered channels plus the five open-ended prefix families ([DerivedStatRegistry.cs:80-110](../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs)) |
| 2 | **Rendered equals applied** — *if the card shows `+45 hp`, `values_json` holds `45`* | any re-scaling creeps into the renderer. Asserted over a seeded instance for every atom in the catalog. **This is the one that makes "numbers that contradict the engine" a build failure rather than a bug report** |
| 3 | **No numeric column mixes unit classes** | a table puts `+9 hp` and `+5 accuracy` in one column. Run against a generated matrix of every channel pair |
| 4 | **No raw `‰` and no raw id on a player surface** | `‰`, `atom.*`, `family_id`, `container_id`, or a `T{n}` tier badge reaches a non-developer band |
| 5 | **`Increased` and `More` render differently** | both produce `+15%` |
| 6 | **A `pending` context part carries a player-facing reason** | §4.3's suppression ships as a blank instead of an explanation |

Guard 1 belongs beside the reader, not in a table: a channel's unit is inseparable from its consumer, and
declaring it elsewhere lets the two drift **invisibly** — the number still renders, it is just wrong by
an order of magnitude.

---

## 9. Two live defects found while verifying

Both were recorded by `ssot-presentation.md` §3.2.5 as claims for R1. **Neither appears in
[defect-register.md](../architecture/item/defect-register.md)** — grep for `increased` and `per-mille`
returns zero hits, so R1 never checked them. Re-verified here.

### 9.1 `Increased` / `More` have no ‰→fraction boundary — **latent, confirmed**

- `stat.modify` declares `amount` as an integer `ParamKind.Value`
  ([AtomKindRegistry.cs:83-92](../../src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs)), and SC4 says
  ratios are integer per-mille.
- `AtomCompiler`'s **only** `/1000.0` is on `chance`
  ([AtomCompiler.cs:152-153](../../src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs)). Nothing divides
  `amount`.
- [InjectorEffectActionSink.cs:97-99](../../src/FusionRpg.Injector/Effects/InjectorEffectActionSink.cs)
  passes the value **straight** into `factory.Increased(...)`.
- [StatComposer.cs:25,29](../../src/FusionRpg.Core/Stats/StatComposer.cs) then treats it as a fraction:
  `afterInc = afterFlat * (1.0 + increased)`.

So an `Increased` atom authored as `150` (meaning +15 %) would compose as **×151**.

**It has not fired.** Grepping `"increased"` across every JSON seed in `src/` and `tests/` returns **no
content rows** — the op is unexercised. This is a **latent** defect that fires on the first authored
`Increased` atom, not a live one.

**Display posture:** the renderer divides by 10 per SC4 and R1 above. If the runtime does not divide by
1000 at its own boundary, **guard 2 goes red on the first such atom** — which is exactly what guard 2 is
for. No fix is proposed here; the seam belongs to the atom program.

### 9.2 `ValueSpec.cs:24-26`'s comment states the corrected-away model

Covered in §2. One-line doc fix in the atom program's file; recorded here because it is the comment most
likely to re-introduce the error.

---

## 10. What changes on plate 00

Six Class-B defects from the audit close here. Applied in the same commit as this document:

| Rung | Was | Becomes |
|---|---|---|
| §D.1 row 3 | `atom.elemental-power.fire.t3` | the id is removed — debug surfaces only |
| §D.1 cards | `T3` `T4` `T5` badges | removed — a tier is an authoring band, and "T4" invites a wiki |
| §D.1 chip/row | `250 ‰` | `25%` |
| §D.1 card | *"about 7.6% to about 26.9%"* | `+150 crit rate` · `≈ +31.8 pp vs neutral` — authoritative part, then a **named** reference. *(The plate's old pair was the **lawn baseline** read, delta −250 → −100 = +19.3 pp. Neither number is wrong; showing one with no reference is.)* |
| §D.1 row | `rolled 120–180` as text | the five-segment bar, with `Fixed` lines getting none |
| new §C.8 | — | the nine unit classes, each with a real channel and a real number |

---

## 11. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — stats, effects/atoms, status, shield, UI.
[x] I read every doc in the §1 row(s) this session: DESIGN-GATE.md, stat-system context via
    StatComposer, actor-hub/atom-catalog channel vocabulary, status-ssot (headings) +
    ResistanceEvaluator, element-hub, item/ssot-presentation.md §1-§5, definitions.md §2/§7.
[x] I checked decisions.md for a lock covering this (Game GUI, Contracts, Resource model rows).
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — and found one comment (ValueSpec.cs:24-26)
    that contradicts the code it sits in. Every unit class in §3 was read at its consumer.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: I ran no test suite. The
    arithmetic in §4.1 was computed and reproduces definitions.md §2's published calibration
    exactly. The §9.1 defect is reported as LATENT on the evidence that no JSON seed in src/
    or tests/ authors "increased" — that is a grep result, not a suite run.
[x] Nothing contradicts a §2 invariant.
[~] Corrections propagated. Plate 00 (§10) lands with this document. tech-stack.md:201-208 and
    web/spec.md:177 are corrected by it and are edited in the same pass. ValueSpec.cs:24-26 is
    the atom program's file and is FLAGGED, not edited.
```
