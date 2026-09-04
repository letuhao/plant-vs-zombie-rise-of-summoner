# Spec: `rarity-bands`

**Module id:** `rarity-bands` · **Program:** [item](../item-map.md) · **Build order:** 7 of 21
**Depends on:** — (nothing in this program)
**Rulings:** **D7 (rule 7 lifted)**, D8, D15, D18, D26, **D29** · lane [ssot-rarity.md](ssot-rarity.md)

## Objective

Put ten rows in an empty table. Seed the **ten rungs**, each rung's **prefix and suffix bands**, and
the **`rarity_budget`** registry — then **re-derive the two per-rung tables that were authored against
the wrong ladder length**: I12's drop weights (written for **7** rungs) and I6's enhancement caps
(written for **5**).

**Users:** 6 (`base-types` — the D11 lint's `score` denominator), 8 (`affix-legality` — the tier
window), 9 (`item-power-reads` — `ceilingFor`), 11 (`drop-volume`), 14–15 (the sinks price by rung),
17 (`uniques` — the parity metric's harness).

**And four things that are not rows**, each recorded here because nothing else owned them: the
**provisional `power_ceiling` seed** that lets module 6's dominance lint leave its weak mode; the
**overlap simulator** `spec-uniques.md` declined; the **three upstream edits** E1–E3 (drop-pity scope,
`registryVersion 2` at D3's twelve, the two §3.3 rows); and the **`enhance_cap` resolution** that stops
this module and module 15 from asserting incompatible tests.

## Design

### The table is real, empty, and narrower than the lane assumes

| Fact | Evidence |
|---|---|
| `rarity` exists with exactly six columns — `rarity_id · ordinal UNIQUE · prefix_rolls · suffix_rolls · min_tier · max_tier` | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:54-61` |
| `RarityRow(string RarityId, int Ordinal, int PrefixRolls, int SuffixRolls, int MinTier, int MaxTier)` | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:163` |
| **Zero rows ship.** `data/seed/rarity/` holds one `README.md` and nothing else, and says so on purpose | `data/seed/rarity/README.md` |
| A seed reader already exists — `id · ordinal · prefixRolls · suffixRolls · minTier · maxTier` | `AtomSeedFile.ReadRarity`, `src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs:381-389` |
| The power-ceiling **reader** exists as a delegate; the table behind it does not | `ContentValidation.Budget(…, Func<string,int?> ceilingFor)`, `src/FusionRpg.Core/Effects/Atoms/Power/ContentValidation.cs:62-65` |

⛔ **The band is not representable.** `PrefixRolls` and `SuffixRolls` are single `int`s.
`ssot-rarity.md` §3.3's halves are **ranges** (`0–1`, `2–3`, …) and §5.3 N1 asked for a
`pool_rolls_max`; the columns that shipped are `prefix_rolls`/`suffix_rolls`, and no `_max` came with
them. **Seeding a range into this schema is impossible today.** Two ways out, and the second is
recommended:

| | Cost |
|---|---|
| Seed the **floor** only, treat the ceiling as fixed | Loses one of the three variances §3.5's overlap invariant is measured on. `U(n,1)` drops toward the bottom of the 5–30 % band — §5.3's own stated fallback |
| ✅ **Add `prefix_rolls_max` / `suffix_rolls_max`, nullable, defaulting to the floor** | Two nullable columns; NULL reproduces today's behaviour exactly. **Ask-first under E5's boundaries** — `spec-container-schema.md` publishes at Checkpoint B |

### ⛔ Two ordinal spaces, one ladder — say which one `rarity.ordinal` is

This has already produced a wrong claim in a shipped design document, so it is stated as a rule rather
than left to context.

| Space | Values | Where |
|---|---|---|
| **Registry / DB ordinal** | **10, 20, … 100**, spaced by 10 | `data/seed/items/_registry/core.v1.json` → `rarity.ladder[].ordinal`, `frozen: true`, append-only *"pre-spaced by 10 precisely so a future rung can be inserted"* |
| **C# enum member index** | **0 … 9**, consecutive | `src/FusionRpg.Core/Demons/DemonRarity.cs:16-28`; `DemonRarityLadder.RungCount = 10` |

**`rarity.ordinal` is the registry space, 10 … 100.** The enum member index is **not** an ordinal and
must never be written to that column. The enum already carries the warning in its own doc comment —
*"a bare `(int)r-1` meant 'one rung of four' before and 'one rung of ten' after, with no compiler
error either way"* (`DemonRarity.cs:10-15`) — and `DemonRarityLadder` exists so intent survives a width
change (`src/FusionRpg.Core/Demons/DemonRarityLadder.cs:10-53`).

⚠ **item-ideal §2f.1 F4's correction is about the *power-class* enum, not about `rarity.ordinal`.**
F4 is right that no C# roster spaces its members by 10; it does not repeal the frozen registry's
spacing, and D7's *"promotion reaches ordinal 100"* is written in the registry space. The two
statements are consistent once the spaces are named. **Naming them is this module's job.**

The join key between the two is the **string id**, and it already resolves both ways
(`DemonRarityIds.ToId` / `.TryParse`, `DemonRarity.cs:52,67`).

### ⚠ Two §3.3 rows do not sum to their published band — fix before seeding

`ssot-rarity.md` §3.3 publishes a combined *Count band* per rung and, added 2026-09-01, a prefix band
and a suffix band. Eight of ten halves sum correctly. **Two do not** (item-ideal §2g #10):

| Rung | Published band | Halves as written | Sum |
|---|---|---|---|
| `sprout` | **1–2** | `0–1` + `0–1` | **0–2** ✗ |
| `heirloom` | **3–4** | `2–2` + `2–2` | **4–4** ✗ |

⭐ **There is a derivation that fixes both and removes the chance of a third.** §3.4's ladder alternates
strictly — *odd steps widen the pool, even steps add an affix* — and that alternation is exact across
all ten rungs. So:

> **A window step keeps the halves of the rung below it. Only a count step may move them.**

| Step | Rung | Halves |
|---|---|---|
| — | `chaff` | 0 + 0 |
| count | `sprout` | **0–1 + 1–1 = 1–2** ← corrected (was `0–1 + 0–1`) |
| window | `grafted` | 0–1 + 1–1 = 1–2 |
| count | `cultivated` | 1–2 + 1–1 = 2–3 |
| window | `fused` | 1–2 + 1–1 = 2–3 |
| count | `chimeric` | 1–2 + 2–2 = 3–4 |
| window | `heirloom` | **1–2 + 2–2 = 3–4** ← corrected (was `2–2 + 2–2`) |
| count | `firstseed` | 2–3 + 2–2 = 4–5 |
| window | `sunwoven` | 2–3 + 2–2 = 4–5 |
| count | `almanac` | 3–3 + 2–3 = 5–6 |

Every row now sums to its published band, and the halves become **derivable from the alternation**
rather than authored twice. **Fix it before seeding: ordinals are append-only, so a post-seed
correction is a migration.**

⚠ `almanac`'s count step moves **both** halves (prefix `2–3 → 3–3`, suffix `2–2 → 2–3`) where every
other count step moves one. It still adds exactly one affix and still sums to 5–6, so it is legal —
recorded because it is the one row a reader will query.

### D7 lifted rule 7 — no rung is drop-only, on any axis

`ssot-rarity.md` §3.7 rule 7 capped promotion at ordinal 80, leaving `sunwoven` and `almanac`
drop-only. **item-ideal §2f.2 lifts it:** *"Promotion reaches ordinal 100, so no drop-only band exists
on any axis."* The audit's reason is the interesting half — with D8 gating aptitude affixes by rung,
rule 7 put **the strongest affix family behind luck**, which is the exact thing D7's *"cost, never
luck"* forbids.

**Consequences this module seeds:**

- `promote_from = 1` on **all ten** rungs. §4.4's row (*"1 for ordinals 10–70, 0 for 80–100"*) is
  stale.
- §3.7's other six rules stand unchanged, including rule 4 (new affixes roll in the item's recorded
  `tier_ceiling`, never a fresh one) and rule 6 (`promoted_from_ordinal` is marked).
- ⚠ §3.8's *"rung 100 must have at least one deterministic source"* **survives** and is now cheaper:
  promotion is one. This module registers the requirement; **[spec-drop-volume.md](spec-drop-volume.md)
  now carries it as a named obligation** rather than leaving it addressed to a lane.
- Cost, not luck, means the promotion price is a **configurable soft cap** in `data/tuning/`, never a
  hard stop (AGENTS.md; `ssot-power-scale.md` §11 — *"a flat rate facing a scaling sink"* counts as a
  cap). Module 15 owns the curve.

### The three upstream edits this module owns

Seeding the ladder is not only ten rows. Three documents outside this spec key on the ladder and are
wrong about it today; **all three are assigned here and none was recorded here before.** Each is
cheap now and a migration after the first seed.

| # | Edit | Target | Cost |
|---|---|---|---|
| **E1** | Scope §3.8's pity rule to **drop** pity | `ssot-rarity.md` §3.8 | one line |
| **E2** | Reissue the hybrid-core roster at D3's **twelve** | `_registry/core.v1.json` → `registryVersion 2`, plus two Python sites | **18 shipped sets go red** |
| **E3** | Correct the two §3.3 rows that do not sum | `ssot-rarity.md` §3.3 | two rows (already derived above) |

#### E1 — §3.8's pity rule is scoped to *drop* pity (§2g #0b)

§3.8 reads *"pity may key on **rung only** — never on roll quality, never on tier."* D7 requires a
**tier** guarantee on crafting, so as written the two cannot both hold and D7 is unimplementable on the
tier axis.

**The rule survives intact once it is scoped, because its premise is about drops.** §3.5's overlap
invariant is **measured on independent draws** — 2 × 10⁵ rolls per rung, seed `20260822` — and every
lever §3.8 names is a **weight shift** on a counted drop source (*"expedition completion, boss kill,
chest open"*). Craft pity shifts no weight: at the threshold it **places** the tier at the container's
`max_tier` and the weighted draw **is not run at all**
([spec-enhance-reroll.md](spec-enhance-reroll.md) §5). A draw that does not happen cannot make the
remaining draws non-independent, so the measurement stands unchanged.

✅ **RESOLVED 2026-09-04 — D31, with an ordering constraint.** The owner: *"implement it before D7."*
The §3.8 scope edit is a **predecessor** of D7's tier pity, not a co-delivery — D7 is unimplementable
on the tier axis until E1 lands, so E1 ships first and alone if need be.

> **The edit, verbatim:** §3.8's rule row becomes *"**Drop** pity may key on rung only — never on roll
> quality, never on tier. Craft pity is module 15's and is a placement, not a weight shift."*

⚠ **Ask-first, decider the owner** — `spec-enhance-reroll.md`'s *Ask first* already names it. This
module makes the edit; it does not make the decision.

#### E2 — the hybrid core is D3's twelve, and correcting it turns 18 shipped sets red (§2g #0a)

This module seeds the ladder that `core.v1.json` carries, so the same `registryVersion 2` bump lands
both. **Three sources agree with each other and disagree with the ruling:**

| Source | Says | Standing |
|---|---|---|
| `data/seed/items/_registry/core.v1.json` | **13** hybrid-eligible roles summing to **895‰** — `ward-array` (90‰) and `jewel-minor-b` (15‰) carry `hybridEligible: false`. Re-measured 2026-09-04: 15 roles, `budgetWeightMilli` sums to 1000 | `"frozen": true`, `registryVersion: 1` |
| `tools/seedsmith/seedsmith/adapters/items/registries.py:111` | the same pair, hardcoded | — |
| `tools/seedsmith/seedsmith/metrics/linkage.py:28` `NON_HYBRID_ROLES` | the same pair | ⛔ feeds a **gating** metric |
| **D3** | **12** roles, **800‰** — drops three | **the ruling wins** |

**Measured, not predicted** (item-ideal §2g #0a): the linkage check is clean today and returns
**18 findings over 30 shipped sets** once corrected to D3's twelve. So E2 is *"re-author 18 sets"* or *"revisit which roles D3 drops"* —
a real cost either way, and it is a **gating** metric, so it cannot be carried as a warning.

⭐ **Recorded here because this module is the one that bumps the registry.** The ordinals in
`core.v1.json` are append-only and `frozen`; a second bump later to fix the role roster would be a
second migration over the same frozen file.

### Re-derivation 1 — drop weights, authored against 7 rungs

`ssot-generation.md:416-423` gives seven rows out of 100,000. **One of them is not a rung at all:**
R7 `unique` is a container property, not a rarity (§3.6, D15 — *"a unique carries a rung like anything
else"*). So the source is **six** equipment rungs, not seven, and the unique row must be re-expressed
as a flag on the drop entry rather than a weight on the ladder.

**Method, so it can be re-run.** `chaff` is **the balancing row, not a held property.** The earlier
wording held the bottom rung's share *and* balanced on it, which is why the published solve did not
reproduce the published table. Two values are pinned; everything else falls out.

```text
w(sprout) = a,   w(rung n) = a · rho^(n-1)     for the nine pooled rungs sprout … almanac
pinned:   a = 21,000            targets I12's bottom share (41.0%); it lands on the balancing
                                row within 0.3 points — see the check below
pinned:   w(almanac) = 700      I12's rarest equipment rung, R6 `relic` at 0.7%
derived:  rho = (700 / 21,000)^(1/8) = 30^(-1/8) = 0.65367
          each pooled row rounded to two significant figures
          sum(pooled, rounded) = 59,300
balances: w(chaff) = 100,000 − 59,300 = 40,700    (40.70%, against I12's measured 41.0%)
```

⚠ **The previous solve — `rho = 0.654`, `a = 20,916`, `sum(pooled) = 59,130` — is 170 short of the
table and is withdrawn.** ⭐ **The table itself was always right**: it sums to exactly 100,000, is
monotone decreasing, and `cultivated`-and-above is 24.6%. The derivation text moved to meet it, not the
other way round — correcting the table after seeding would be a migration.

| Rung | Weight /100,000 | Share |
|---|--:|--:|
| `chaff` | 40,700 | 40.70 % |
| `sprout` | 21,000 | 21.00 % |
| `grafted` | 13,700 | 13.70 % |
| `cultivated` | 9,000 | 9.00 % |
| `fused` | 5,900 | 5.90 % |
| `chimeric` | 3,800 | 3.80 % |
| `heirloom` | 2,500 | 2.50 % |
| `firstseed` | 1,600 | 1.60 % |
| `sunwoven` | 1,100 | 1.10 % |
| `almanac` | 700 | 0.70 % |
| | **100,000** | |

**Check against I12's own measured property.** `ssot-generation.md:246` states *"rare-or-better is 28 %
of items"* — verifiable as R3+R4+R5+R6+R7 = 28,000. The ten-rung equivalent (`cultivated` and above)
is **24.6 %**, 3.4 points lower, and the gap is the ladder being longer rather than a change of
intent.

**These are starting values in `data/tuning/`, not code.** D18 governs how *many* items drop —
`Θ`, linear — and this table governs only *which rung* one is; D26 keeps both out of the business of
metering the player.

### Re-derivation 2 — enhancement caps, authored against 5 rungs

`ssot-enhancement.md:446-452` gives `Normal +4 · Magic +8 · Rare +12 · Epic +16 · Legendary/Unique +20`
— a **step of +4 per rung** — and says the table is *"open-ended: a future rarity rung above
Legendary/Unique adds a higher row… not a hard stop at +20."*

**The step is the design quantity; the ladder got longer, so the top gets higher.** Keeping +4 per
rung over ten:

| Rung | naive `+X` cap | | Rung | naive `+X` cap |
|---|--:|---|---|--:|
| `chaff` | +4 | | `chimeric` | +24 |
| `sprout` | +8 | | `heirloom` | +28 |
| `grafted` | +12 | | `firstseed` | +32 |
| `cultivated` | +16 | | `sunwoven` | +36 |
| `fused` | +20 | | `almanac` | +40 |

**This creates no ceiling**, and the arithmetic says why: `cap(item) = min(rarity_cap, ilvl_cap,
progression_cap)` with `ilvl_cap(ilvl) = max(4, 4 + ilvl/4)`, unbounded (`ssot-enhancement.md:437-438`).
At v1's content reach of ilvl 32, `ilvl_cap = 12` — so **`rarity_cap` binds only for `chaff`, `sprout`
and `grafted` today**, and the rest of the column is inert until the content ladder grows (**X5**).
Verified, and worth stating: a table that looks generous is mostly unreachable.

⛔ **But I1's registered constraint on `enhance_cap` is violated at the top of the ladder, and this is
a finding, not a re-derivation.** §4.4 constrains the key: *"total gain at cap ≤ one rung step in
expectation"*, and §9.5 sizes a rung step at **~+70 %**. I6 §3 delivers *"roughly 2× the item's own +0
magnitude"* at cap — a single figure. Against §7.3's measured ladder (ceiling 5: sprout 17 · grafted
39 · cultivated 65 · fused 133 · chimeric 187 · heirloom 379 · firstseed 487 · sunwoven 630 · almanac
770 hp) the adjacent-rung ratio is **not constant**: **2.294×** at the bottom, **1.222×** at the top.

⚠ **One definition, fixed before the numbers.** *"Rung steps"* was previously counted two ways in one
sentence — `2.0 / 1.7 = 1.2` at the bottom (a ratio of multipliers) and `ln 2 / ln 1.222 ≈ 3` at the top
(a count of steps). **A step composes multiplicatively, so a gain is measured in steps as
`ln(gain) / ln(step)`** and nothing else. Under that one definition:

| Base for `step` | `step` | `2×` at cap, in steps | Verdict |
|---|--:|--:|---|
| §9.5's nominal rung step (~+70%) | 1.700× | **1.31** | breaks the constraint |
| measured bottom, `sprout → grafted` | 2.294× | **0.83** | holds |
| measured top, `sunwoven → almanac` | 1.222× | **3.46** | breaks it by 3.5× |

**The single figure is the defect, not its size.** A flat `2×` sits inside the constraint at the bottom
and 3.5 steps past it at the top, where a maxed `firstseed` clears a natural `almanac`
(487 × 2 = 974 > 770).

#### ⭐ Resolution — a shrinking **soft** cap, and this module seeds its asymptote (§2g #0c)

Module 15 removes the cap and asserts `no_enhancement_cap_is_a_hard_stop`; this module registers it as
HARD. **Both are right about their own half**, and under this module's own SC7 rule the disagreement is
not academic: `enhance_cap` with no shipped consumer **rejects**, so the seed load fails.

> **Enhancement gain asymptotes *below* one rung step instead of stopping at it:**
> `gain(n) = enhance_cap(rung) × n / (n + K)`. It never reaches its ceiling, so it never refuses a
> level — AGENTS.md's required shape — and the ladder still cannot invert.
>
> **`enhance_cap` is re-specified accordingly.** It is no longer a maximum `+X`; it is the **per-mille
> asymptote of total enhancement gain**. Module 15 consumes it as the curve's ceiling, so SC7 is
> satisfied rather than broken. Recorded identically in
> [spec-enhance-reroll.md](spec-enhance-reroll.md) §4a.

`step(rung)` is smoothed over **two** rungs before use, because §3.4's ladder alternates count and
window steps and the raw adjacent ratio alternates with it (2.05× · 1.41× · 2.03×). The two-rung
geometric mean `sqrt(v(r+2) / v(r))` is monotone, and that is what makes the column *shrinking* rather
than merely *per rung*:

| Rung | `step(rung)` | `enhance_cap` ‰ | | Rung | `step(rung)` | `enhance_cap` ‰ |
|---|--:|--:|---|---|--:|--:|
| `chaff` | — | 860 | | `chimeric` | 1.614 | 552 |
| `sprout` | 1.956 | 860 | | `heirloom` | 1.289 | 260 |
| `grafted` | 1.847 | 762 | | `firstseed` | 1.257 | 232 |
| `cultivated` | 1.696 | 627 | | `sunwoven` | 1.222 | 200 |
| `fused` | 1.688 | 619 | | `almanac` | — | 200 |

`enhance_cap(rung) = StepMarginAlphaMilli × (step(rung) − 1)`, with `StepMarginAlphaMilli = 900` in
`data/tuning/item-rarity.v1.json` — **one tunable over a measured table**, not ten authored numbers.
Two rows are not derived and say why: `chaff` rolls no affixes so it has no measured magnitude and
takes `sprout`'s, and `almanac` has no rung above it, so the constraint is vacuous there and it takes
`sunwoven`'s — the conservative reading, and the one that keeps the column monotone.

⚠ **This binds today, not at some future content depth.** At v1's reach `ilvl_cap(32) = 12`, and I6's
linear +20‰-per-level gain reaches **240‰** — already past `firstseed`'s 232‰ and past `sunwoven`'s and
`almanac`'s 200‰.

⚠ **The `+4`-per-rung level table above is superseded by this column** and is kept only as the naive
extension and the reason it fails. `enhance_cap` seeds a ‰ asymptote; it does not seed a level count,
and no seeded row is a hard stop.

### ⭐ The overlap simulator is **claimed here**, and it was orphaned

`ssot-rarity.md` §3.5 registers a **measured** adjacent-rung upset band and §6.3 proposes it as a CI
test (`Adjacent_rung_upset_rate_is_within_band`, `Distance_three_upset_rate_is_under_two_percent`).
Nothing owned the harness. `spec-uniques.md` **declines it explicitly** — *"it needs I1's overlap
simulator, which this module does not own … **do not implement a second simulator**"*
(`spec-uniques.md:113-115`) — and ships its parity invariant `W ∈ [25%, 75%]` as a metric **with no
threshold**, because it is unmeasurable without the harness.

**So the only consumer of the harness declined to build it, and the lane that specified it had no
module. This module claims it**, for the reason that decides every other question in this file: the
invariant is a property of *the bands this module seeds*, and it is what says whether the ten rungs are
a ladder or a spread.

| | |
|---|---|
| **Owner** | this module. One harness, in `src/FusionRpg.Core/Items/RarityOverlapSimulator.cs` |
| **Method** | §3.5's, verbatim and re-runnable: 2 × 10⁵ rolls per rung, one channel family (`vitality`, `maxHp` flat, hp units), tier uniform inside the window, magnitude uniform inside the tier, seed `20260822` |
| **Asserts** | `U(n,1) ∈ [5%, 30%]` · `U(n,2) ≤ 10%` · `U(n,3) ≤ 2%` · `U(n,4) ≈ 0`, as **exact counts** — definitions §11: *"a tolerance on a seeded test is an invitation to widen it"* |
| **Exposed to** | module 17 `uniques` as `Parity(unique, rung) → W`, so its invariant becomes measurable and its metric gains its threshold |
| **Not exposed to** | anything on the drop path. It is a test harness, never a generation input |

⚠ **The measurement is re-run, not inherited.** §3.5's published band (7.9%–28.3%) was measured against
§3.3's *uncorrected* halves; this module corrects `sprout` and `heirloom` before seeding, so the first
run re-establishes the band and the corrected numbers are what CI pins. A band carried across a change
to the thing it measures is not a measurement.

⚠ **A rolled socket count would be a fourth variance and invalidates the run** (§3.5, §9.4). `socket_min`
/ `socket_max` are unseeded pending I4; if module 16 lands a *rolled* count, this harness re-runs with
sockets included before the band is quoted again.

### `rarity_budget` — the KV registry, and SC7 enforced

`rarity_budget(rarity_id, budget_key, value_int)`, keys validated against a closed code-side registry
naming each key's consumer. An unknown key rejects; **a key whose consumer has not shipped rejects**
rather than sitting inert. That is what makes *"a row no code consumes is a lie in a table"*
mechanical instead of aspirational.

| Key | Read by | Seeded now? |
|---|---|---|
| `promote_from` | 15 | ✅ **1 on all ten** (D7) |
| `pity_guarded` | 11 | ✅ 1 at **`heirloom` (70)** and **`sunwoven` (90)** — keyed on rung **id**, never on I12's stale `r4`/`r6` labels (§3.8). ⚠ D7 lifted rule 7, so `almanac` (100) is reachable by promotion and stays **unguarded**; its deterministic source is module 11's |
| `drop_weight_default` | 11 | ✅ the table above |
| `enhance_cap` | 15 | ✅ the table above, **with the shrinking-step constraint attached** |
| `power_ceiling` | 9, via `ContentValidation.Budget`'s `ceilingFor` | ✅ **seeded provisionally, ratio-exact — see below.** Was *blocked on X6*; refusing it was the more expensive choice |
| `socket_min` / `socket_max` | 16 | ⛔ awaiting I4. ⚠ §4.4 requires it to declare whether the count is **rolled** — a rolled count is a fourth variance and moves every number in §3.5 |
| `set_eligible` | — | ⛔ **DROPPED — not seeded.** D15 makes it vacuous (a set has no rarity and completes from pieces of any rung) and **`spec-set-charm-gen.md` never mentions it**, so under SC7 a seeded row would reject. Resolved here rather than deferred to module 13 again — see below |
| `reroll_cost_mult` | 15 | ⛔ awaiting I7 — must scale with **affix count**, not rung alone (§9.7) |
| `salvage_yield` | 14 | ⛔ awaiting I9 — must **not** reuse `shard.{DemonRarity}` ids (`DemonMaterialCatalog.cs`); §9.8 suggests `dust.{rarity_id}` |
| `charm_potency` | — | ⛔ **NOT REGISTERED.** I10 defines it and **`spec-set-charm-gen.md` never mentions it**; SC7 forbids registering a key ahead of its consumer. Module 13 requests it when it needs it — see below |
| — | 11 (`level_req`) | **negative registration: rarity is not an equip gate** |

#### ⭐ `power_ceiling` ships provisionally — the highest-leverage unblock in the program

**Refusing to seed it is not neutral, and the cost is mechanical rather than theoretical.**
`ContentValidation.Budget` skips any container whose ceiling is `null` —
`if (ceilingFor(container.Rarity!) is not { } ceiling) continue;`
(`src/FusionRpg.Core/Effects/Atoms/Power/ContentValidation.cs:71`) — so with the key unseeded the budget
check **evaluates zero containers and reports green**. Downstream:

```text
power_ceiling unseeded → module 9 R1 returns Unpriced
                       → module 6's D11 dominance lint has no `score`
                       → it runs in CHANNEL-SPLIT mode indefinitely (spec-base-types.md:127-129)
                       → it checks only "the two slates differ", never "neither dominates"
                       → D3 degrades with nothing failing a test        ← §2d's named failure
```

§2h.2 ranks this #8 and calls it *"the single highest-leverage unblock in the program."*

**Module 9's own argument shows a provisional value is safe**, and it is a ratio argument, not an
optimism argument:

> *"the ≤15% cap is a second check on a **ratio of two prices computed by the same function**, and a
> uniform coefficient error cancels in a ratio"* — `spec-item-power-reads.md:75`

All 20 coefficients are flat at `CoeffMilli = 1000` (`Power/CoefficientTable.cs:120-148`), which is a
**uniform** error by construction. ⭐ **The seed is shaped to make that argument literally true rather
than merely plausible:**

```text
power_ceiling(rung) = pinAE × ladderShareMilli(rung) / 1000

  ladderShareMilli   §7.3's measured ceiling-5 hp ladder, normalised to almanac = 1000.
                     Coefficient-INDEPENDENT — it is an hp measurement, not a price.
  pinAE              the price of one reference `almanac` slate through the SAME cost function
                     the consumers use (`ActorPowerCache.Compose`), computed at seed time.
                     Coefficient-DEPENDENT, and it is the only such term.
```

A uniform coefficient rescale moves an atom's price and `pinAE` by the same factor, so every **share**
read off this column is invariant under X6. Only the absolute `Budget` threshold moves.

| Rung | ‰ of top | | Rung | ‰ of top |
|---|--:|---|---|--:|
| `chaff` | 0 | | `chimeric` | 243 |
| `sprout` | 22 | | `heirloom` | 492 |
| `grafted` | 51 | | `firstseed` | 632 |
| `cultivated` | 84 | | `sunwoven` | 818 |
| `fused` | 173 | | `almanac` | 1000 |

| | Rule |
|---|---|
| **Standing** | `provisional`, carried **in the result object** — never in a comment. Copies module 9's `flat_coefficients_are_reported_not_hidden` pattern (`spec-item-power-reads.md:238`) |
| **Legal uses** | shares and ratios — R1's implicit budget, the D11 lint's `score`, any comparison of two prices from one function. These are **exact today** |
| ⛔ **Illegal uses** | quoting the absolute AE figure without the `provisional` flag; gating a drop on it (`ContentValidation`'s own rule — *"a content test that fails naming the offender, and **never** a generation input"*, `ContentValidation.cs:57-59`) |
| **X6 landing** | re-price `pinAE` and clear the flag. The shares do not move and **no consumer changes** |
| **Staleness** | `data/tuning/item-rarity.v1.json` records the `coefficientTableId` `pinAE` was priced under; a mismatch against `PowerTables.Current` is a warning, not a failure |

#### The two `rarity_budget` keys this module **resolves by dropping**

Both were deferred to module 13 by an earlier draft. **`spec-set-charm-gen.md` mentions neither**, and
SC7 makes an unconsumed key **reject** — so deferring them again would ship a seed file that fails to
load. They are resolved here.

| Key | Resolution | Reason |
|---|---|---|
| `set_eligible` | **dropped from the registry** | D15: a set has no rarity and completes from pieces of any rung, so the key can only ever hold `1`. `ssot-rarity.md:377` assigns it to I5, which never used it |
| `charm_potency` | **not registered** | I10 defines it; module 13 does not read it. When module 13 needs it, it registers the key **with** its consumer, which is what SC7 exists to force |

⚠ **This is a lane edit, not just a spec note:** `ssot-rarity.md` §5's `rarity_budget` roster carries
both rows and must lose them, and §6.2's per-kind matrix must stop naming `set_eligible` on the `set`
row and `charm_potency` on the `charm` row.

`rarity_budget` must join the content-hash registry, which is an explicit `contentHashSchemaVersion`
bump — the thing `definitions.md` §8 says must never happen silently.

### Two shipped-store defects this module must close

1. **`UpsertRarity` renumbers an existing rung.** It refuses an ordinal owned by a *different* id
   (`RpgStore.Containers.cs:147-151`) but the upsert body is
   `ON CONFLICT(rarity_id) DO UPDATE SET ordinal = excluded.ordinal` (`:159-167`) — so moving `fused`
   from 50 to 55 succeeds. Ordinals are load-bearing for sorting and for the budget lookup; the
   table's own comment says a reorder *"silently re-prices every container naming one."*
   `RarityLadderMutated` is the code (§6.1) and it does not exist.
2. **`effect_container.rarity` is free TEXT with no foreign key** (`:24`), and `ContainerValidator`
   never mentions rarity. `UnknownRarity` is the FK check the schema never had.

Both are **reviewed additions to definitions.md's closed 33-code list**, together with
`RarityBandViolated`. ⚠ §2b.1 resolved the reason-code question the other way — **one namespaced
`ContentRuleViolated`** rather than N new codes. **Take §2b.1**: three codes here become
`ContentRuleViolated{rarity.unknown | rarity.band | rarity.ladder-mutated}`, and no closed list grows.

### What is **not** decided, and who decides

| Open | Owner |
|---|---|
| The two `_max` columns (ask-first, Checkpoint B contract) | **owner**, via effect-atom E5 |
| E1 — the §3.8 drop-pity scope edit | **owner** decides, this module makes the edit |
| E2 — re-author 18 sets, or revisit which three roles D3 drops | **owner** (product call), `registryVersion 2` lands here |
| `pinAE`'s final value, after **X6** | **module 9**. The ‰ shares are this module's and do not move |
| A deterministic source for `almanac` | **module 11** — registered in [spec-drop-volume.md](spec-drop-volume.md), no longer only here |
| A light-theme palette for the ten colours (§10.7) | **module 20 `item-surfaces`** |
| ~~`set_eligible`~~ · ~~`charm_potency`~~ | **closed above** — dropped and unregistered, not deferred again |

## Commands

```powershell
dotnet run --project tools\AtomImporter -- data\seed\rarity        # 0 files today
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Rarity"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RarityLadder"
```

## Project structure

```text
data/seed/rarity/ladder.v1.json                   new — the ten rows AtomSeedFile.ReadRarity reads
data/tuning/item-rarity.v1.json                   new — drop weights, enhance caps: tunables, not code
src/FusionRpg.Core/Items/RarityLadder.cs          new — id <-> ordinal, the two-space rule, halves
src/FusionRpg.Core/Items/RarityBudgetKeys.cs      new — the closed key registry + consumer names
src/FusionRpg.Core/Items/RarityOverlapSimulator.cs new — I1 §3.5's harness, claimed here; test-only
data/seed/items/_registry/core.v2.json            new — D3's twelve hybrid roles (E2); v1 stays on disk
docs/architecture/item/ssot-rarity.md             EDIT — §3.3 halves (E3), §3.8 drop-pity scope (E1),
                                                    §5/§6.2 lose set_eligible + charm_potency
src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs  EDIT — rarity_budget; the ordinal-mutation refusal;
                                                    prefix_rolls_max / suffix_rolls_max (ask-first)
src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs  EDIT — read the two _max fields when they land
```

## Code style

```csharp
// Two ordinal spaces exist for one ladder and they are 10x apart. `rarity.ordinal` is the REGISTRY
// space (10..100, frozen in core.v1.json, pre-spaced so a rung can be inserted); DemonRarity's member
// index is 0..9 and is NOT an ordinal. The string id is the join. Writing (int)rarity here would put
// `almanac` at ordinal 9, below `chaff` at 10, and every sort in the game would invert.
public static int OrdinalOf(DemonRarity rarity) => Ladder[rarity.ToId()].Ordinal;   // never (int)rarity
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_ten_rungs_match_the_frozen_registry_exactly` | id and ordinal pairs against `core.v1.json`; a renumber fails CI |
| `rarity_ordinal_is_never_the_enum_member_index` | ⭐ the two-space rule, as a test rather than a comment |
| `every_rung_halves_sum_to_its_published_count_band` | the sprout and heirloom defects, **red before the fix** |
| `a_window_step_keeps_the_halves_of_the_rung_below` | the derivation, so a third defect cannot be authored |
| `promote_from_is_one_on_all_ten_rungs` | D7's lift; the old `0 at 80-100` row is gone |
| `an_existing_rungs_ordinal_cannot_be_changed_on_upsert` | closes `RpgStore.Containers.cs:159-167` |
| `a_container_naming_an_unknown_rarity_is_rejected` | the FK `effect_container.rarity` never had |
| `drop_weights_sum_to_100000_and_are_monotone_decreasing` | the re-derived table, exactly — no tolerance |
| `unique_is_not_a_rarity_rung` | I12's R7 was a container flag; §3.6, D15 |
| `enhance_cap_gain_never_exceeds_one_rung_step_at_any_rung` | ⭐ the constraint I1 registered and I6 breaks at the top |
| `ilvl_cap_binds_below_rarity_cap_at_ilvl_32` | the "mostly unreachable" claim, measured not asserted |
| `a_rarity_budget_key_with_no_shipped_consumer_is_rejected` | SC7, enforced |
| `power_ceiling_share_is_invariant_under_a_uniform_coefficient_rescale` | ⭐ the seed's whole safety argument — price the same content under two coefficient tables and assert the **share** is byte-identical |
| `power_ceiling_results_carry_the_provisional_flag` | module 9's `flat_coefficients_are_reported_not_hidden` pattern, reused verbatim |
| `content_validation_budget_evaluates_every_rung_after_seeding` | `ContentValidation.cs:71` skips a null ceiling; before the seed the check reports green over **zero** containers |
| `the_d11_lint_leaves_channel_split_mode_once_power_ceiling_is_seeded` | the unblock, asserted at the consumer rather than claimed here |
| `enhance_cap_asymptotes_below_one_rung_step_at_every_rung` | ⭐ replaces `enhance_cap_gain_never_exceeds_one_rung_step_at_any_rung`; HARD, and it is the test module 15 rewrites against the same curve |
| `enhance_cap_is_monotone_non_increasing_across_the_ten_rungs` | *shrinking*, not merely per-rung — the two-rung smoothing is what buys it |
| `a_gain_in_steps_uses_one_definition` | `ln(gain)/ln(step)` everywhere; pins the 0.83 / 1.31 / 3.46 figures so the two-base mix cannot return |
| `adjacent_rung_upset_rate_is_within_band` | I1 §3.5's harness, claimed here — exact counts, seed `20260822`, re-measured against the corrected halves |
| `distance_three_upset_rate_is_under_two_percent` | same harness |
| `set_eligible_and_charm_potency_are_not_in_the_key_registry` | the SC7 resolution, as a test — a re-added key with no consumer fails at seed load |
| `palette_lightness_is_monotone_under_a_deuteranope_transform` | §4.5's measured rule, carried forward |
| `no_rarity_id_collides_with_a_power_class_or_a_slot_role` | the two-axes guard (`spec-affix-power-class.md`) |

## Boundaries

**Always:** seed by string id and let the registry supply the ordinal; keep drop weights and enhance
caps in `data/tuning/`; register a `rarity_budget` key only when its consumer ships.

**Ask first:** the two `_max` columns; any `contentHashSchemaVersion` bump; changing a rung's colour,
name or count band after seeding.

✅ **~~E1 and E2~~ are RULED, 2026-09-04.** **E1** — scoping §3.8 to drop pity — is **D31**, and it
lands **before** D7, not alongside it. **E2** — `registryVersion 2` at D3's twelve — is **D30**, and the
18 red sets are **re-authored**, in the same generation run module 13 performs for the ~904.
This module still owns only the edit.

**Never:** register a `rarity_budget` key whose consumer has not shipped — `set_eligible` and
`charm_potency` were dropped for exactly that reason and re-adding either is an SC7 violation. Never
quote `power_ceiling`'s absolute AE figure without its `provisional` flag, and never let it gate a drop.
Never write a second overlap simulator (`spec-uniques.md:113-115`). Never write a C# enum member index
into `rarity.ordinal`. Never renumber a seeded ordinal — it is
a migration, not an edit. Never let rarity touch a magnitude: `CurveInput.Rarity` exists
(`CurveTable.cs`) and is banned on `container_kind = 'item'`. Never re-introduce a drop-only band —
D7 removed the last one.

## Success criteria

- [ ] Ten rows in `rarity`, ordinals `10 … 100` matching the frozen registry, seeded from
      `data/seed/rarity/`.
- [ ] Every rung's prefix and suffix halves sum to its published count band — `sprout` and `heirloom`
      corrected **before** the first seed.
- [ ] `promote_from = 1` on all ten; no rung is drop-only on any axis.
- [ ] Drop weights re-derived over ten rungs, summing to 100,000, with the method written down and
      `unique` removed from the ladder.
- [ ] Enhancement caps re-derived over ten rungs, and the **shrinking-step** constraint is registered
      with the measured `step(rung)` table and handed to module 15.
- [ ] `rarity_budget` exists, keys validate against a closed registry, and an unconsumed key rejects —
      with `set_eligible` and `charm_potency` **absent**, not deferred.
- [ ] An existing rung's ordinal can no longer be moved by an upsert.
- [ ] ⭐ `power_ceiling` is seeded on all ten rungs, its shares are invariant under a uniform coefficient
      rescale, every result carries the `provisional` flag, and module 6's D11 lint leaves channel-split
      mode.
- [ ] `enhance_cap` is a **‰ gain asymptote**, monotone non-increasing, derived from one tunable over the
      measured `step(rung)` table — and `spec-enhance-reroll.md` records the same resolution.
- [ ] Gains are counted in steps by one definition, `ln(gain)/ln(step)`, everywhere in this file.
- [ ] The overlap simulator exists here and nowhere else, re-measured against the corrected halves, and
      module 17's parity metric can call it.
- [ ] E1, E2 and E3 are each either **landed** or **filed with the owner naming the cost** — E2's cost is
      18 red sets on a gating metric.
