# Ideal: `demon-seed` — the species anchor, classified from lore and expanded by arithmetic

**Program:** `demon-seed` · **Phase:** idea. **No spec, no plan, no code follows from this document.**
Written 2026-09-01.

**Predecessor:** [seedsmith-demons-ideal.md](seedsmith-demons-ideal.md) explored generating *content
about* 84 species that already existed. This explores generating **the species themselves**, and
replaces that document's assumption that the roster arrives pre-made.

---

## 0. The principles this is built on, restated inline

A downstream session reads this document, not its links. So the load-bearing rules are written out
here rather than cited.

**1. Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** PvZ owns
the board, vanilla damage, spawn/die, and the sun bank. We observe its events and contribute signed
deltas back. We never rewrite it, never read its current state, and never make a feature depend on it
representing a concept. *"Can the lawn express X"* is almost always the wrong question; *"does the RPG
layer have a channel/atom/runtime for X, and is that path wired or inert"* is the right one. An inert
path is a **wiring gap**, never an architectural wall.

**2. Two async systems.** The RPG and PvZ share no clock. Hooks record and return; decisions happen in
a later budgeted drain. Delay is the designed degradation mode, not a failure to engineer around.

**3. One power ladder.** Every magnitude derived from a level goes through a single index `Θ` and a
single function `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`. **Contests read `Θ` (linear, difference-based);
magnitudes read `P(Θ)` (triangular).** Never the other way round. The inventory of power-shaped
scales is **closed** — a curve not in it has no permission to exist, and writing a private `f(level)`
in a subsystem is the exact defect that let three incompatible curves ship at once.

**4. Rarity never touches a magnitude.** A rarity rung sets a **count band** and a **tier window** —
how many affixes, and how far below the top the pool may reach. That is all. A multiplier on the rung
makes rarity dominant and destroys the overlap between rungs that makes a long ladder legible.

**5. No hard progression ceilings.** A cap on a magnitude is a progression ceiling until proven
otherwise: remove it, or make it a configurable soft cap. Absolute bounds are derived from the
arithmetic and **throw, never clamp silently** — a clamp turns *"your gear stopped mattering"* into a
bug with no symptom.

**6. The balance surface is config, not code.** Any number a balance pass would change lives in
`data/tuning/<domain>.v{n}.json`. A number in code costs an edit, a rebuild and a test run; a number
in config costs a file save.

**7. Magnitudes are `long`.** Never `float` — it stops being integer-exact at `Θ` = 232, inside normal
play. Widen before multiplying, divide by 1000 last, let overflow throw.

**8. The vocabularies are closed on purpose.** 5 attach points · 12 atom kinds · 7 triggers ·
6 elements + `omni` · 6 resources · 12 aptitudes · 10 item rarity rungs. Adding one is a reviewed
change, not a convenience.

**9. Never invent a second classification of something already classified.** Two classifications of a
stat channel already exist and are verified against consumers; inventing a third is a named, repeated
failure in this repo. This document already made that mistake once during the conversation that
produced it — see §5 Q1.

---

## 1. What the owner asked for

Verbatim, 2026-09-01:

> *"extend seedsmith generator, don't use in game C# generator it useless … make multiple pipelines
> that LLM read almanac data for each plant/zombie. each pipeline answer a question like what family
> classify, power scale (not a real number, a open or closed enum), aspect and some other …
> a demon specie data (json) is an anchor it have multiple closed/open enum. we will use this anchor
> to generate other gameplay mechanism for demon — like deterministic power scale generator base on
> closed/open set power scale enum that define on demon specie seed."*

Three claims, and all three are architecturally correct:

1. **The LLM classifies; it never computes.** Every answer is an enum drawn from a closed (or
   deliberately open) vocabulary.
2. **The species JSON is an anchor** — a record of those enums and nothing else.
3. **Deterministic generators read the anchor** and produce every real mechanic, including magnitudes.

This resolves a tension the predecessor document could not. seedsmith's schemas **mechanically reject
any numeric field** (`tools/seedsmith/seedsmith/pipeline/model.py:8-9`: *"a schema carrying a numeric
magnitude field is rejected by `audit_schema`, not by review"*). That reads as a limitation only while
you expect the model to produce mechanics. Under the anchor model it becomes the **load-bearing
guarantee**: the model is structurally incapable of inventing a number, so every number in the game
still comes from one ladder.

---

## 2. Findings — built · wiring gap · real gap

### BUILT

**B1 — The anchor shape already exists, and is already almost pure enums.**
`DemonSpeciesCatalog.cs:9-23` carries `SpeciesId`, `Name`, `Side`, `GameTypeId`, `DemonTypeId`,
`ElementPrimary`, `ElementSecondary`, `BaseRarity`, `DeployMode`, `Acquisition`, `Variants[]`,
`TraitPool[]`. Only the two ids are numbers, and they are identity, not magnitude. **This feature does
not invent a shape; it changes where the enum values come from.**

**B2 — The power ladder is built, exact, and callable.** `Power/PowerLadder.cs:26`, with
`ValueMilli(int index)` at `:34` documented as *"P(Θ) in per-mille, before the single end rounding.
Exact — no float anywhere."* A `powerBand → Θ` lookup has a real consumer the day it is written.

**B3 — The twelve aptitudes are a 3 × 4 matrix with a counter-cycle, not a flat list.**
`Stats/Aptitudes/Aptitude.cs:28-53` — three postures (Force · Finesse · Bastion) × four each, with
`Count` computed as `PostureCount * PerPosture` so a thirteenth changes by construction. From
`data/seed/aptitudes/roster.json`'s own `role` column, the breaks form a cycle:

| Posture | Its defence | Its breaks | Therefore counters |
|---|---|---|---|
| Force | Fortitude (mitigation) · Vigor (shield) | Onslaught (guard + reflect) | **Bastion** |
| Finesse | Agility (dodge) · Composure (crit-denial) | Pierce (mitigation + shield) | **Force** |
| Bastion | Bulwark (guard) · Retribution (reflect) | Precision (dodge) · Ferocity (crit-denial) | **Finesse** |

Might (universal offence) and Focus (utility — qi, cooldowns) are the two non-cyclic slots.

**B4 — Nine measured aptitude allocations already exist.** `Battle/Ai/ZombossPatterns.cs` ships 9
patterns — 3 pure, whose shares are *ported from `tools/CombatSim`'s own measured archetypes*, and 6
mixed, each a (defence-posture, breaks-posture) pair chosen because it is not self-cancelling. Each
pattern **is** a 12-way per-mille allocation. Any `aptitude → allocation` expansion table has a
calibration reference instead of inventing shares.

**B5 — Six resources, not five.** `Stats/Derived/DerivedStatChannels.cs:510`:
`{ "hp", "stamina", "hunger", "spirit", "qi", "poise" }`. `resource-hub-ssot.md:128` calls this a
closed set — *"adding one is an ADR (`poise` added 2026-08-26)"*. `poise` is the guard pool: a flat
commit cost to raise a guard plus a drain proportional to what it absorbed; empty means guard broken,
never death (`Combat/Guard/PoiseRuntime.cs`).

**B6 — A ten-rung rarity ladder exists, and was built for growth.**
`data/seed/items/_registry/core.v1.json` → `rarity.ladder`: chaff 10 · sprout 20 · grafted 30 ·
cultivated 40 · fused 50 · chimeric 60 · heirloom 70 · firstseed 80 · sunwoven 90 · almanac 100.
Ordinals are *"pre-spaced by 10 precisely so a future rung can be inserted without renumbering."*
Rungs 70 and 90 are pity-guarded; 100 is deliberately unguarded but must have a deterministic source.

**B7 — Demon gacha is shipped and tuned to four rungs.** `Demons/SummonRoller.cs`: common 74% / rare
20% / epic 5% / legendary 1%; epic hard pity 25; legendary soft ramp +6%/pull from 41, hard 55; a
10-pull rare floor.

**B8 — seedsmith's generation runtime is built and proven.** Constrained decoding via LM Studio
(`response_format: json_schema`), a LangGraph workflow with three independent stop conditions, six
deterministic validators, provenance recording what each entry was generated from, and skip-existing
idempotency. 497 tests green; 84 commander effects generated end to end against a local Gemma-26B.

**B9 — The closed vocabularies an anchor would draw from are all real.** 5 attach points
(stat · resource · status · shield · board), 12 atom kinds, 7 triggers of which **5 are authorable**
(`OnSpawn` · `OnDamageDealt` · `OnDamageTaken` · `OnDeath` · `OnTimer`; `OnGranted`/`OnRemoved` are
runtime lifecycle no atom may author), 6 concrete elements plus `omni` — and `omni` **may not appear
in a primary or secondary slot** (`element-hub-ssot.md:128`).

**B10 — The two number systems stay SEPARATE. Only a progression delta crosses.**

An earlier draft of this finding claimed *"the RPG's magnitudes must be commensurate with PvZ's own"*
and treated the `韧性` corpus as a calibration anchor between the two ladders. **That was wrong, and
the owner corrected it:**

> *"we don't really need to know in game stats exactly — we only need to send delta for demon
> progression level and power. the base stats keep on our rpg engine, so our rpg database don't need
> same as pvz database. our base stats will use for web battle area features."*

The correct model, and it follows directly from the standalone-first decision (*the web RPG is the
core game; PvZ is extension gameplay*):

| | Owns the base | What crosses |
|---|---|---|
| **Web battle** (the core game) | the RPG's own demon base stats — server-authoritative | nothing; it is all RPG-side |
| **PvZ lawn** (extension) | PvZ owns the entity's own base (`EntityBaseline` Y0, captured at spawn) | **only the demon's progression/power delta** |

So a demon's base HP is never sent to the lawn and never needs to resemble a gargantuar's 640,000.
What is contributed is *what this demon has earned* — its progression — and that is a smaller,
independent quantity. **`P(Θ)` is not being asked to match PvZ's scale**, which is why no
cross-calibration table is needed.

**What survives from the original worry:** the progression delta still has to be *felt* on the lawn,
and `EntityBaseline` (`Stats/EntityBaseline.cs:3`, *"Immutable game baseline Y0 for one entity
instance"*) plus `DerivedModifierOp.Increased` (`Stats/Derived/DerivedModifier.cs:13`) mean a percent
contribution is available if a flat one reads as noise. That is a tuning choice inside the RPG, not an
architectural bridge between two ladders.

**And the `韧性` corpus keeps its real job** — it seeds `powerBand`, which says how strong a species
*is relative to other species*. It was never needed as a unit conversion.

### WIRING GAP

**W1 — `stat.derived` atoms are quarantined everywhere.** The kind exists and is one of the twelve,
but has *no opcode, no bag branch, no sink arm*, and battle reads channel mods only from
`TraitBattleCatalog`, never from a grant. This is the kind an aspect would most naturally use to write
a derived channel. It re-opens per runtime as consumers ship — **a wiring gap on a scheduled path, not
a wall.**

**W2 — seedsmith declares an `aspect` kind that nothing writes.**
`tools/seedsmith/seedsmith/adapters/demons/kinds.py` declares `demon`, `aspect`, `commander-effect`,
`environment`; only `commander-effect` has a generator. `data/seed/demons/aspect/` does not exist.

**W3 — `Distribution/MotifSharing` is inert.** It reports *"no demon entry carries motif data yet"*
against a corpus where `motif-assignments.json` holds 135 motifs for all 84 demons, because nothing
merges the generated file back onto the corpus entries `Corpus.load` reads.

**W4 — Both terminal outputs are unread by the game.** `themes.v1.json` publishes 84 theme keys and
**zero items reference one**; `commander-effect/all.json` holds 84 name+doctrine pairs and no lawn
code opens the file. The pipeline is internally consistent and connects to gameplay at neither end.

### REAL GAP

**R1 — Nothing derives a species from lore.** `DemonSpeciesGenerator.cs:57` assigns rarity by HP rank
(`RarityForRank(rank, pool.Count)`), `:81` assigns traits by `TraitsFor(rarity, row.TypeId)`, and
element by round-robin coverage. **No code path reads almanac prose to decide anything.** The
LLM-reads-the-almanac step is `power-estimate` (D5) — decided 2026-08-31, never specced, never built.

**R2 — Capture coverage, not code, now bounds the roster.** Measured against
`dist/FusionRpg.Server/data/rpg-hot.sqlite` on 2026-09-01: **904** `almanac_seed` rows (677 plant /
227 zombie), **889** carrying flavour text — but only **82** with observed HP
(`stats_observed = 1`); **822** have none. `DemonSpeciesGenerator.cs:39` requires `HpBase > 0`, so
889 rows of usable lore are invisible to a roster of 84. **The old 24-species cap is gone and this
replaced it.**

**R3 — `aspect-scope` is approved and unbuilt, and it moves two of the anchor's fields.**
`demons/spec-aspect-scope.md` is *"APPROVED by the owner 2026-08-31. Authorized to build"* and moves
`ElementPrimary`/`ElementSecondary`/`TraitPool` **off the species onto an aspect tier**, so one species
can have many elements. Verified unbuilt: those fields are still on the species at
`DemonSpeciesCatalog.cs:17,23`, and no `Aspect` type exists anywhere under `src/`.

> **⚠️ SUPERSEDED by §5 Q9 (owner, 2026-09-01): `aspect-scope` is to be REVERTED.** This finding
> originally concluded that an anchor keeping element on the species contradicts an approved spec.
> With the revert, keeping element and traits **on the species is correct**, and one demon has exactly
> one aspect — its own. The finding is left in place because the *fact* it verified is still true
> (the spec is approved and unbuilt); only its conclusion changed.

**R4 — There is no `powerBand` axis anywhere.** No `PowerBand`/`PowerTier` enum exists in `src/`.
This is the one axis in the whole design that is genuinely free — nothing consumes it, so its length
and semantics can be chosen without migrating a single consumer.

**R5 — ⛔ A locked decision blocks the headline change.** `decisions.md:95` records the species
catalog as *"generated deterministically from captured game data (types/almanac/icons/spawn_stats),
output checked in."* Replacing that generator with LLM classification is an amendment to a locked
row, and this repo's rule is that architecture changes which lock behaviour need `decisions.md`
changed **first**. This is procedural, not a refusal — but it is not a silent swap.

---

## 3. The shape this suggests

### 3.1 Two stages, and the boundary between them is the whole idea

```
   almanac row (Chinese prose, 889 of 904 have it)
              │
              ▼   LLM — one pipeline per question, every answer an ENUM
   ┌──────────────────────────────────────────┐
   │  THE ANCHOR — enums only, no magnitudes  │
   └──────────────────────────────────────────┘
              │
              ▼   deterministic — tables and the one power ladder
   magnitudes · allocations · atoms · matchups · drop tables
```

The model never sees a number and never emits one. Every number in the game continues to come from
`P(Θ)` and the tuning tables. The `audit_schema` numeric ban stops being a limitation and becomes the
enforcement mechanism for principle 3.

### 3.2 What the anchor carries

**Identity — carried, not classified.** `speciesId` · `nativeName` · `side` · `gameTypeId` ·
`demonTypeId` · `sourceText` (the exact almanac fields it was classified from) · `basis`
(`text` | `name` | `blocked`) · `provisional`.

**Classified — one pipeline per question.**

| Field | Openness | Draws from | Note |
|---|---|---|---|
| `family` | **open** | grows; 19 today | already built |
| `aptitudePrimary` | closed, 12 | `AptitudeCatalog` | replaces the "archetype" idea — see §5 Q1 |
| `aptitudeSecondary` | closed, 12 or null | `AptitudeCatalog` | posture derived, never asked |
| `powerBand` | closed ordinal | **new, and free** (R4) | the magnitude axis |
| `rarity` | closed, 4 | `DemonRarity` | the breadth axis — count + tier window only |
| `deployMode` | closed, 2 | `PlantAvatar` · `HypnoAlly` | exists |
| `acquisition` | closed flags | Summonable · CaptureOnly · EventOnly | exists |
| `variants` | closed, 7 | normal · ancient · mutated · corrupted · blessed · cursed · shiny | exists |
| `resourceProfile` | closed subset, 6 | hp · stamina · hunger · spirit · qi · **poise** | may be derivable from posture |

| `elementPrimary` | closed, 6 | fire · ice · air · earth · light · dark — **`omni` is illegal in a slot** | stays on the species per Q9 |
| `elementSecondary` | closed, 6 or null | same; **0..2 concrete types total** | stays on the species per Q9 |
| `traits` | **open** | `TraitPool` | stays on the species per Q9 |

**Element and traits stay on the species** (§5 Q9). The earlier draft of this document moved them to
an aspect tier on the strength of an approved spec; the owner reverted that spec, so one demon has
exactly one aspect — its own — and its typing lives on the anchor.

**Still not on the anchor:** any magnitude. Not one field above is a number.

### 3.3 What deterministic generators derive from it

| Input | Generator | Output | Constraint it must respect |
|---|---|---|---|
| `powerBand` | lookup table → `Θ` → `P(Θ)` | hp · atk · defense | **A table, never a formula.** A `f(band)` is a private curve; the inventory is closed (principle 3) |
| `aptitudePrimary` + `Secondary` | expansion table calibrated on `ZombossPatterns` (B4) | 12-way per-mille allocation | Feeds the demon-type allocation scope that already exists and has no supplier |
| `aptitudePrimary` | direct read | posture → counter-cycle position | Derived, so it can never contradict the aptitude |
| `rarity` | count band + tier window | how many aspects/atoms, which tiers | **Never a magnitude** (principle 4) |
| `resourceProfile` | pool registration | which of the six pools exist | |

### 3.4 The two axes are orthogonal, and that is the point

`powerBand` answers *how strong*; `rarity` answers *how many*. This is the item model exactly — ilvl
decides how strong an affix may be, rarity decides how many affixes and how far below the top the
pool reaches. Keeping them separate is what makes a long ladder legible rather than dominant, and it
is why a top-rung drop from the tutorial lawn is structurally impossible rather than merely
discouraged.

### 3.5 Provenance is the upgrade path, not bookkeeping

The predecessor document established this and it matters more here: with **822 of 904** types
carrying lore but no stats (R2), and enrichment a planned later pipeline, the corpus must be able to
answer *"which species were classified from a name only, under which prompt version, from which
source text"* — or it can only be rebuilt wholesale rather than improved incrementally. `basis`,
`provisional` and `sourceText` are load-bearing.

---

## 4. Prior art — eight research passes, 2026-09-01

> **Raw material: [../research/game-design/](../research/game-design/)** — the full data tables,
> verbatim designer quotes with attribution, documented failure modes, and
> **[06-unsourced.md](../research/game-design/06-unsourced.md), which records what does not exist.**
> Read that before commissioning any further research on unit design; several hundred searches were
> spent here and the negative findings are as valuable as the positive ones.

Nine game families surveyed from **shipped data**, not wiki prose: Pokémon (PokéAPI + Showdown dex and
ladder stats), StarCraft I/II (OpenBW, BWAPI, Blizzard's own `.sc2mod` catalogs), Warcraft III and
AoE II (Blizzard's `classic.battle.net`, genieutils), Command & Conquer (EA's GPL release), Company of
Heroes and Total War (Relic Essence exports, RPFM schemas), Genshin / HSR / Arknights / FGO / FEH /
Summoners War (game data tables and official APIs), D&D 5e and PF2e (Open5e's 3,207 creatures,
Archives of Nethys' 4,748), Diablo II (1.13 data files), Ragnarok Online (`mob_db.yml`).

### 4.1 ⭐ The ratio that decides roster design

Units per **grid cell** — the product of a game's primary categorical axes — against documented
power-creep severity:

| Units/cell | Games | Creep |
|---|---|---|
| **~1** | Summoners War 1.02 · HSR 1.8 · Arknights 1.97 | Low, structurally constrained |
| **~3** | Genshin 3.4 · FGO 3.2 | Low — FGO's base stats have not moved in ten years |
| ~7 (median 4) | Pokémon | Managed by a **second vocabulary** (abilities) |
| **~15, max 129** | **Fire Emblem Heroes** | **The worst-documented case in the genre** |

FEH is causal, not coincidental: 1,410 heroes into 96 cells, **129 Red Sword Infantry in one cell**,
and a BST ceiling that moved from ~147–169 at launch to **216**. Its Arena scoring buckets BST into
bins of 5 *before* weapons and merges, converting stat creep directly into revenue.

**Our own number, computed the same way** (element-combination × aptitudePrimary; rarity excluded,
as the method excludes it):

| Design | Cells | Units/cell at 904 species | Lands in |
|---|---|---|---|
| Single element × 12 aptitudes | 6 × 12 = **72** | **12.6** | **FEH's failure zone** |
| **Dual element × 12 aptitudes** | 21 × 12 = **252** | **3.59** | **The Genshin/FGO safe band** |

**This is the strongest quantitative result of the round, and it settles hybrid typing on evidence
rather than taste.**

The three ways games hold ~1–3 per cell, in ascending order of fit for a generated roster:

1. **Grow the vocabulary with the roster.** Arknights runs 425 operators at a median of 2 per
   (subclass × rarity) cell by treating the subclass enum as a **content stream** — 72 branches and
   counting, five of which exist on CN but not Global.
2. **Make the grid the primary key.** Summoners War's `family_id × element` is filled 821/870 with
   **median exactly 1 and max exactly 1** — no two obtainable monsters share a cell.
3. **⭐ Orthogonal axes beat a long flat list.** **Ragnarok Online: 27 authored values across four axes
   (Race 10 × Element 10 × ElementLevel 4 × Size 3) produce 417 realised mechanical identities for
   2,675 monsters.** A flat list would need 417 maintained entries for the same expressiveness.

Point 3 answers the `family` sizing question and dissolves it. A rarefaction test — resample *n*
creatures from a full corpus, count distinct categories, 200 draws — shows **type vocabularies
saturate at n≈300 and never grow again**: D&D 5e uses **14 types for 322 SRD creatures and for all
3,207**. A flat model at 900 would want ~270 families; a multiplicative one wants a few small axes,
which is what this design already has. **`family` does not need to grow toward 270.**

### 4.2 ⭐ Distinctness is not carried by stats. It is carried by abilities.

Measured over full rosters:

- **63%** of 3,207 D&D 5e creatures share their exact `(CR, AC, HP)` triple with another. PF2e: **83%**.
- Adding **type + speed modes + resistances** lifts uniqueness to **93%**.
- **71%** of 5e's 2,472 distinct trait names appear on exactly **one** creature. PF2e: 8,429 ability
  names across 4,748 creatures, **66% used once**.
- Pokémon, the same finding from the other direction: type combination alone gives 154 cells, median
  3 species. Type **+ ability set** gives **730 cells, median 1, 68% singletons**. True near-duplicate
  rate **0.5%** — 18 pairs in 1,025 species, every one a deliberate designed twin. Carried by ~310
  abilities and 934 moves, not by 18 types.
- Genshin makes it literal: 119 characters share only **72 distinct HP values** and **68 distinct ATK
  values**. Diluc (v1.0, 2020) and Odette (v7.0, 2026) have **identical HP 12,980 / ATK 334**.

**A 900-unit roster needs roughly 1,500–3,500 named ability instances.** That is the real cost of this
program, and it is a generation problem of its own — almost certainly the shape of the
passive-skill-graph work named in §5 Q9.

### 4.3 The genre abandoned N×N matrices, and said why

**Four AAA franchises independently dropped the table, and none went back.** SC2 replaced a universal
3×3 with 22 sparse per-weapon bonuses; AoE4 replaced 38 armour classes with 4 damage types; Total War
went from 5 bonus categories to **2**; Company of Heroes shipped no matrix at all. **42 cells
(Warcraft III) is the largest fixed matrix any of them shipped.**

Dustin Browder, on the SC1 → SC2 change, verbatim:

> *"We wanted to make that system a lot more transparent and obvious. **Before, you had to be a
> hardcore player or surf the web to understand how the system worked.** … So instead of doing 50 or
> 75 or 100 percent damage, we added a single damage bonus against certain unit types."*

**In that very quote he mis-states SC1's own numbers** — he says 100/75/50; the real table is
100/50/25 and 50/75/100. The lead designer of the sequel got the source system wrong in an interview
about how confusing it was. That is evidence, not anecdote.

**Legibility is inferrability, not size.** No researcher or designer has published a threshold *N* at
which a matrix stops being learnable. The argument for the other axis comes from a **strategy critic,
not a developer** — Brandon Casteel, writing on Game Developer — and it names Warcraft III's **18**
non-neutral cells as unlearnable while Pokémon's **120** are not: *"Why one unit takes triple damage
from a sword versus the unit next to it taking 25% damage from the same sword is a matter of
memorizing tables and playing over and over — **there's no good visual indicator for the player to
use.**"* Attribution matters here: **no Blizzard developer has ever conceded the WC3 matrix was hard
to learn.**

**Blizzard's stated rule:** *"Units' attacks always do at least 100% of the damage value shown on the
screen."* They removed **penalty** cells because the printed number stopped being trustworthy.
`ElementRingMatrix` currently has penalty cells (`Weak => −k`).

### 4.3a ⭐ A counter matrix removed, then re-added — with reasons on both sides

Fire Emblem is the only natural experiment in the survey: the series **dropped** its weapon triangle in
*Three Houses* (2019) and a sister studio **restored** it in *Three Hopes* (2022). Both decisions have
designer statements, and they disagree in a useful way.

**Why it was removed** — Toshiyuki Kusakihara, director, Intelligent Systems:

> *"We think that the weapon triangle is somewhat of a stylized system, **it isn't really realistic**.
> If you have a situation where a novice axe user takes down an advanced lance user, well, that makes
> sense? Probably not. So, we wanted to make something that comes across as more realistic to warfare
> and have players develop their weapons skills individually."*

The objection is that a **categorical counter overrides accumulated investment** — a novice beats an
expert because of a category. That is exactly what a strong matchup multiplier does.

**Why it was restored** — Hayato Iwata, director, Omega Force / Koei Tecmo, on *Three Hopes*:

> *"**We thought that simple visual cues make for better choices and gameplay.** … we thought players
> should be able to understand the concept fairly easily as it is not an entirely new concept to
> them."*
> *"We originally gave each class a single weapon and decided what weapons would be effective against
> what based on that, but **we ultimately went with the weapon triangles**."*

And his colleague Hayashi, on playing without it: *"We didn't use that system in the beginning and
**felt that the gameplay wasn't as interesting**."*

**The two positions are both right, about different games.** A counter matrix buys *legibility and
decision texture* and costs *investment mattering*. Nintendo's own editorial framing in Ask the
Developer treats the triangle as **series identity** — *"a gameplay feature passed down from past
games as one of the characteristics of the Fire Emblem series"* — not as an optimum.

**For this design the tension is live**: a demon roster is built on accumulated investment (levels,
rarity, aptitudes), which is the axis Kusakihara says a triangle overrides. That is an argument for
keeping matchup *soft* — which our ±25% already is — or for moving the counter's payload out of the
damage formula entirely, as Engage did by replacing the modifier with a **Break** status.

⚠️ Both quotes are verified only against their English republishers; GameSpot and Jeuxvideo returned
403 to direct retrieval.

### 4.4 Reactions vs matrices — the cost argument, with counts

| | Pokémon (matrix) | Genshin (reactions) |
|---|---|---|
| Types / elements | 18 | 7 |
| Live interactions | 306 ordered cells · **120 non-neutral (37%)** | 26 productive of 30 · 17 of 21 pairs |
| **Facts to learn** | **120**, each an independent authored cell | **~22** — 16 named reactions + 4 direction cases + 2 inertness rules |
| Facts per element | 6.7 | **3.1** |
| Cost growth | **O(n²)** | **O(named reactions)** — the designer chooses |
| Composes? | No — a cell is terminal | **Yes** — a reaction produces an object (Dendro Core, Quicken aura, Frozen aura) that feeds the next |
| Depth | One depth, 120 cells, for everybody | **Two, set independently** — 16 names for players, gauge / aura-tax / decay / ICD for optimisers |

**~5.5× fewer facts despite 2.6× more elements.** And the cheapness has a named source:

> *"The reaction system's cheapness comes from deliberately leaving pairs empty. Genshin's 4 dead
> pairs and DOS2's 5 non-blessable surfaces are not gaps — they are the budget."*

When Genshin added Dendro, pairs went 15 → 21 but they shipped only **6** new reactions, leaving 4 of
the 6 new pairs inert.

**Pokémon's own depth engine is the dual-type product rule**, not the type count: 120 authored facts
become **1,589 non-neutral interactions** — a 13× expansion at zero authoring cost. The design space
is already **94.7% exhausted** (162 of 171 combos used), and the nine holes are declined on *flavour*
grounds, several being strong typings. **More types would not have helped.** Gen IX's answer was
Terastallization — a 19th type name adding essentially **zero** matrix cells.

**⛔ The Geo warning, and it applies here by construction.** Geo was acknowledged broken in December
2020 and was still ranked **worst of 7** in August 2024, through two dedicated characters that did not
fix it. The stated reason: *"Geo has a single elemental reaction, Crystallize, whereas most other
elements have three or four."* The eventual fix was the **Lunar reaction family** — an orthogonal layer
giving *every* element a new reaction slot. **Extend by adding a reaction family, not by adding
elements.**

`light` and `dark` are neutral against the entire ring and interact only with each other. In a matrix
that is merely sparse; in a reaction model it is **Geo's shape, worse** — one interaction each against
the ring four's two apiece.

### 4.5 ⭐ Rarity buys breadth and ceiling. In every game studied, never power.

| Game | Tiers | What rarity actually controls |
|---|---|---|
| **Arknights** | 6 | **skills 0/0/1/2/2/3 · talents 1→2 · max level 30/30/55/70/80/90 · elite ceiling · modules 4★+ · mastery 4★+** |
| Genshin | 2 | Ceiling only — same 3 talents, 3 passives, 6 constellations at both tiers |
| HSR | 2 | Ceiling only — same 5 skills, 6 Eidolons, 18-node trace tree |
| FGO | 6 | Party cost 3/4/7/12/16 and max level — **every Servant has exactly 3 actives and 5 Appends** |
| FEH | 5 | **~5 BST across the entire range**, plus skill *access*. And it is **mutable** — 46 heroes demoted a tier in one day |
| Summoners War | 5 | Almost pure acquisition rate — **SPD is flat at ~100 at every natural star** |

Arknights is the cleanest model: **rarity moves median deployment cost by 3 points across five tiers;
class moves it by 11.** That ratio is the whole mechanism behind low-rarity viability.

And the recurring refusal: **every game that kept low rarity viable did so by refusing to let rarity
buy the thing that matters most in its own combat model** — SPD in Summoners War and HSR (4★ mean SPD
*exceeds* 5★), deployment economy in Arknights, NP level in FGO (raised only by duplicates, while 3★
Servants drop from a free currency).

This validates principle 4 and §5 Q4: adopting the ten-rung ladder is safe **because count-band and
tier-window are exactly breadth**.

### 4.6 Cap the magnitude field; creep the effect vocabulary

**FGO's median 5★ ATK moved 32 points in ten years across ~450 Servants**, and the all-time highest
belongs to an early-middle release. Everything that got stronger got stronger in *effect text* — 50%
NP charge became 80%, single-target became party-wide, 3-turn became 5-turn.

**Epic Seven goes further and does not author per-unit statlines at all**: base stats come from a
216-cell (rarity × class × zodiac) template, so heroes sharing all three are *numerically identical*.
*"E7 cannot creep statlines without moving a cell a dozen heroes share."* **That is a defensive
property a derived-stat generator gets for free**, and it reframes the Warzone 2100 tradeoff in §2:
losing per-unit hand-tuning also buys structural immunity to per-unit inflation.

**⚠️ One finding that challenges the single-curve rule.** Every surveyed system inflates **durability
far faster than lethality**:

| System | HP growth | Damage growth |
|---|---|---|
| Diablo II Normal → Hell (L85) | 6.2× | 1.85× |
| Path of Exile level 1 → 100 | 2,989× | 352× |
| **Diablo III Torment I → XVI** | **16,958×** | **163×** — HP grows **104× faster** |

PoE's map-tier table sets boss damage to **+0% at every tier from 66 to 90**. Principle 3 has all
magnitudes read one `P(Θ)`, so HP and ATK grow identically — a choice **no surveyed game made**. It
may still be right, but it should be deliberate. See §5 Q19.

### 4.7 Enum selection is the most bias-prone LLM task shape there is

Every pipeline in this design is discrete choice over a labelled set — precisely the shape with the
largest documented non-semantic failure modes.

- **Selection bias splits into label bias** (an uncontextual preference for certain label *names*)
  **and position bias.**
- **Position bias is severe:** shuffling option order has been reported to change GPT-4 accuracy by
  **up to 75%**.
- **Mitigations are cheap and measured:** permutation with majority voting and multi-evidence
  calibration recover up to **8 percentage points**; `PriDe` is a label-free inference-time debiasing
  method with demonstrated cross-domain transferability.
- **Conformance is not quality.** An audit of public structured-output benchmarks found *"every public
  benchmark examined was full of erroneous and inconsistent ground-truth outputs."* Reported
  reliability is usually schema validity (e.g. 98.0% JSON validity), which is orthogonal to whether the
  content is right — a lesson this program already paid for twice, at 8/8 on shoehorned content and
  83/83 same-named effects.

### 4.8 Failure modes, with corpses

**⛔ A data table picked the winning build.** Diablo II Hell immunities across 703 monsters:
**137 cold · 131 poison · 113 fire · 105 lightning — and 11 magic.**
*"which is why Hammerdin won Hell — a data table picked the winning build, not a designer."*
**This is the sharpest warning of the round for a generated roster**: an uneven element/resistance
distribution across 900 demons silently selects the meta, and selection bias (§4.7) guarantees
unevenness unless it is measured. It converts §5 Q8 from a quality nicety into the control that
prevents this outcome.

**⛔ A single wrong cell broke a metagame for a console generation.** Pokémon Gen I shipped
Ghost → Psychic at **0×** when it should have been 2×. Nintendo's own guides, two anime episodes, and
**an NPC inside the game** all said otherwise. Psychic's only functional weakness became Bug, and Gen I
had no strong Bug moves. The lesson: *"anyone shipping a matrix needs a test that asserts the table
against declared intent."*

**⛔ Gates converge every build, and Larian retracted one after nine years.** DOS2's armour had to reach
0 before any status could land. Swen Vincke on their own AI: *"it focused on one character, made sure to
destroy their physical and magical armour, and then would start to control it, then kill it… **it was a
dominant tactic.**"* Removed in 2026; Pechenin's replacement criterion is *"you will not have to wait
before you can use your fun skills on enemies."* **General rule: if a mechanic's rule is "nothing
interesting happens until X", optimal play is always "make X happen first" and every build converges.**
`PoiseRuntime` has that shape; the riposte rule (spent poise converts to damage) is a real mitigation
Larian did not have, but the shape is worth watching.

**⛔ Tag absence is a stat.** SC2's Archon, Ghost, Ravager, Baneling and Queen carry **neither** Light
nor Armored, making them immune to a large share of every bonus-damage term in the game. **Omitting a
tag is not neutral — it is a defensive buff.** Hence §6's explicit-`none` rule.

**⛔ Closed vocabularies leak under pressure.** SC2 patch 5.0.13 removed the Sentry's Light tag *and in
the same line* added `+4 vs Shields`, an ad-hoc pseudo-attribute outside the closed eight. `Psionic` was
dead vocabulary for an entire expansion cycle before Interference Matrix revived it. Four live attribute
swaps shipped **with no designer note**. A closed contract stays closed only if it has a designated
escape hatch; otherwise one gets improvised.

**⛔ Taxonomy accretion.** AoE2's 38 armour classes include one named *"All Buildings (except Port)"* for
a building that never shipped, one named *"Unique Units (except Turtle Ship)"* that **contains Turtle
Ships**, a class created and decommissioned in place, and a Mosque with **no armour class at all**, so
every attack against it does exactly 1 damage. Hidden from players for ~20 years.

**⛔ "A faster Banshee is still a Banshee."** Browder on SC2's pre-release redundancy crisis:
*"You'd play as the Zerg with something called a Spore Beast and we'd say, 'Oh my god, this is just a
Banshee, isn't it?' We'd try to tune it as like a really fast Banshee, and it was like, 'Okay, dude, but
that's still a Banshee.' **It hadn't fundamentally changed its role.**"* And the rule: *"You don't want a
'Marine 1' and 'Marine 2' scenario… you'd just end up picking the 'best' one."*
**A pure magnitude axis does not make a different unit.** `powerBand` and `rarity` both fail that test;
only element, aptitude, and the §6.2 tempo/reach variables pass it.

**⛔ Integer ceilings are real.** WoW's Ra-den shipped at **~1.5 billion HP — 70% of the signed 32-bit
ceiling** — forcing four game-wide stat squishes. Principle 7 with a corpse attached.

**The dead tail, measured.** Pokémon: **177 species (36% of everything tiered) sit in the bottom tier**,
and **18 species fill 50% of all competitive team slots**; of 1,025 species, 762 appear at all and only
82 reach 1% usage. Genshin: among players who **own** the character, eight sit under 4% usage while
Kazuha sits at 95.7% — a **~319× spread** — and Klee shows a **69% vacancy rate** (levelled past 71,
never fielded).

**And the industry's answer to power creep was retroactive rewrites, not restraint.** All three
HoYoverse titles converged independently — Novaflare (HSR), Hexerei (Genshin), ZZZ v2.5 — periodic named
batches that rewrite old kits wholesale, including scaling stats and Eidolon/Constellation effects.
HSR's are **toggleable**; Genshin's are **gated behind quest content** and attach a party-synergy tag.
**Each of those is a content program. For a generated roster it is a pipeline re-run** — a structural
advantage this design has and they do not, and the reason provenance must stay load-bearing.

### 4.9 What nobody has published

- **No quantified counter-strength target.** No studio states "a counter should win by X%". Blizzard's
  statements are entirely qualitative and always name micro or terrain as the override. A number for
  how much element matchup should matter must be derived here, not borrowed.
- **No threshold at which a matrix becomes unlearnable.**
- **No designer statement capping Pokémon at 18 types** — only evidence of per-addition cost.
- **No industry-standard closed role taxonomy.** Blizzard's two official "unit types" pages both
  explicitly disclaim completeness. Role is a per-unit editorial judgement used to detect redundancy,
  never an enum a unit is assigned from.
- **Almost no designer commentary on roster or grid design at all.** Genshin's official "Developers
  Discussion" series, enumerated across 18 entries from 2023-09 to 2025-07, is **entirely
  quality-of-life**; not one entry discusses roster growth, duplicate avoidance, grid coverage, or
  power creep.

---

## 5. Open questions

**ALL CLEARED — Q2 through Q22, by the owner 2026-09-01. Nothing in this document is waiting on a
decision.** Q21 and Q22 were raised by the prior-art round and answered the same day; both answers
corrected the question rather than picking an option.

Q9's answer named two unbuilt programs the full `aspect` vision depends on, and Q18's answer
retracted the premise of finding B10, which is corrected in place rather than deleted.

### Q1 — `archetype`: superseded before it was written, recorded so it is not re-proposed

During the conversation that produced this document, an eight-value `archetype` enum
(bruiser/tank/swarm/artillery/controller/support/assassin/summoner) was proposed and then withdrawn:
it is a **second classification of what the twelve aptitudes already classify**, which is the named
failure principle 9 exists to prevent. The owner's correction — *"we have 12 primary stats mean 12
base class"* — is the shape carried into §3.2. **Not an open question; a recorded correction.**

### ✅ Q2 — `aptitudeSecondary`: **different posture by default; SAME posture allowed and flagged `pure`.** *(owner)*

A same-posture pair is a deliberate specialist, not an accident — which is exactly what the three
pure `ZombossPatterns` already are (`force-pure`, `finesse-pure`, `bastion-pure`). Flagging it makes
the distinction readable in data instead of inferred from the pair.

Different-posture pairs reproduce the six shipped mixed patterns and cannot self-cancel. The `pure`
flag marks the ones that deliberately double down, so a balance pass can find them in one query.

### ✅ Q3 — `powerBand`: **a `Θ` OFFSET, ~10 rungs.** *(owner)*

The band shifts the species' index on the single ladder; `P(Θ)` does the rest. A stronger species is
*further along*, which composes with world depth and player level without double-counting, because
there is still exactly one curve. A multiplier was rejected for the reason principle 5 exists: it
compounds with `contentScale` at depth and would need a cap on a magnitude.

**The lookup lives in `data/tuning/`, never as a formula** — a private `f(band)` is precisely the
defect the closed power inventory was written to end.

### ✅ Q4 — demon rarity: **ADOPT THE FULL 10-RUNG ITEM LADDER.** *(owner)*

Owner override, taken with the migration cost stated. Demons leave `DemonRarity`'s four values and
join `chaff`·10 → `almanac`·100 — one vocabulary, one set of colours and pips, one sort order across
items and demons.

**This is an amendment to [item/ssot-rarity.md](item/ssot-rarity.md) §4.1 and §4.3**, whose current
text says *"Demons keep their own ladder"* and rejects exactly this. The amendment must be written
before the change is built, and it carries five verified consumers:

| Consumer | What breaks |
|---|---|
| `SummonRoller` rates | 74/20/5/1 is a four-rung distribution; ten rungs needs a new curve |
| `SummonRoller` pity | epic hard 25 / legendary soft 41 / hard 55 all key on four-rung names |
| `FusionRoller.SlotsFor` | slot counts keyed by rarity |
| `SoulEarnPolicy.DiscoveryDelta` | soul yield keyed by rarity |
| `shard.{rarity}` material ids | id strings embed the four names |

**The item ladder already answers the pity concern the genre raises** (§4.3): it guards only rungs
**70** and **90**, and requires rung **100** to have a deterministic (quest/boss) source. Demons can
adopt that same two-guard shape rather than inventing ten thresholds.

### ✅ Q5 — `resourceProfile`: **ALWAYS A PIPELINE. The LLM picks the pools.** *(owner)*

Owner override of the measure-first option. Lore about a starving plant or a spirit-eating zombie
reaches the right pool directly, rather than being flattened into whatever the posture implied.

**The cost this accepts, stated plainly:** the pipeline can contradict the posture — a Bastion demon
that never gets `poise`, or a Focus demon with no `qi`. That contradiction is invisible to every
tier-2 validator in seedsmith today, because each answer is individually legal. It needs a
**cross-field validator**, and what that validator does on a conflict is Q12.

### ✅ Q6 — the 822: **DROP THE HP GATE. Parse HP from almanac text as an optional seed.** *(owner)*

Owner: *"drop hp gate, but hp still exist in zombie description in almanac, can use it as optional
seed."*

**Verified, and it is larger than the question assumed — it is in plant descriptions too.** Measured
2026-09-01 against `rpg-hot.sqlite`, parsing `韧性` (toughness) and `伤害` (damage) out of
`flavor_info`/`flavor_introduce`:

| Basis | Count | Where the power signal comes from |
|---|---|---|
| `observed` | **82** | `spawn_stats` — met in-game |
| `stated` | **637** | `韧性`/`伤害` in the almanac text — **deterministic regex, no model call** |
| `inferred` | **170** | prose only; the LLM judges `powerBand` from lore |
| `blocked` | **15** | no text at all |

**719 of 904 types carry a numeric power seed, up from 82.** The great majority of the roster is
therefore *not* an LLM judgement at all — it is parsing a number the game already printed.

`韧性` spans **200 → 640,000** (n=381, median 3000). Across 10 rungs that log-spaces to ≈**2.24× per
rung**, which is a natural fit for Q3's ten-rung offset ladder and is evidence the two decisions
agree rather than merely coexist.

**This makes `basis` a four-value ordinal, and it is the feature's precision ladder**: a later capture
upgrades a species from `inferred` → `stated` → `observed`, and provenance says exactly which rows to
re-derive. Nothing is ever rebuilt wholesale.

### ✅ Q7 — C# path: **KEEP CAPTURE. MOVE EVERY GENERATOR TO SEEDSMITH.** *(owner)*

Owner: *"keep capture because it is pvz fusion source of truth but we move every generator to
seedsmith so we can support AI native generator."*

Sharper than the option offered. The dividing line is **observation vs derivation**, not C# vs Python:

| Stays in C# | Moves to seedsmith |
|---|---|
| The injector capturing the live game | `DemonSpeciesGenerator`'s rarity / element / trait rules |
| `almanac_seed`, `spawn_stats`, `recipes` — PvZ Fusion's own facts | The species roster itself |
| The DAL that owns those tables | Corpus emission |

**Rationale, in the owner's frame:** capture is the source of truth *because PvZ produced it*; every
step that *derives* something belongs where derivation is cheap to iterate and AI-native. This also
amends `decisions.md:95` more broadly than option 1 would have — the row's *"generated
deterministically from captured game data"* becomes *"captured deterministically; derived in
seedsmith"*.

⚠️ **This answer created Q10** — seedsmith is Python in `tools/`, and the captured facts live in
SQLite. See below.

### ✅ Q8 — bias control: **PERMUTE EVERYWHERE; majority-vote the load-bearing fields.** *(owner)*

Option order is seeded from `speciesId` — deterministic, but not constant across subjects — which
removes the systematic ordering artifact at zero extra cost. Majority voting across permutations is
spent only where the answer drives mechanics: **`powerBand` and `aptitudePrimary`**.

**Disagreement rate across permutations becomes a reported quality metric**, in the same place `basis`
and coverage are reported. This is the first quality signal in this program that can see a defect
*no per-item validator can*, which is the exact class of failure that produced 87% code-switching and
83-of-83 duplicate names — both individually legal, both invisible to tier 2.

### ✅ Q9 — `aspect`: **REVERT `aspect-scope`. One demon, one original aspect. No element/status variants.** *(owner)*

Owner: *"revert aspect feature, original demon need original aspect, no element/status — that is
better than make a bundle of aspect, they will become chaos and hard to rebalance, so we have a
playable game first."*

This is the largest decision in this document, and it moves in the opposite direction from every
option offered. **`element` and `traits` stay on the species.** A demon has exactly one aspect: its
own. There is no fire-Peashooter / ice-Peashooter fan-out.

**Consequences, stated rather than discovered later:**

| | Effect |
|---|---|
| [demons/spec-aspect-scope.md](demons/spec-aspect-scope.md) | **APPROVED 2026-08-31 — now to be reverted.** Reverting an approved spec is itself a `decisions.md`-level change, exactly like R5's |
| §2 finding **R3** | **Superseded.** It said an anchor keeping element on the species contradicts an approved spec. With the revert, keeping it there is now correct |
| §3.2 anchor | `elementPrimary`, `elementSecondary`, `traits` **return to the species** |
| class-system `point-economy` | Its third allocation scope survives, but **collapses to 1:1 with the species** — degenerate, not missing. `decisions.md:101`'s four scopes still hold |
| `DemonSpeciesGenerator`'s element round-robin | Still deleted — element becomes an **LLM classification from lore**, which is what a Pokemon-style typing wants anyway |

**The rebalance argument is the load-bearing one.** N aspects per species multiplies the balance
surface by N before a single demon is playable. This program's own history supports it: the
class-system's dominance matrix is *"red by design today"* at one aspect per actor.

### ⚠️ Q9 also named two unbuilt programs the full aspect vision depends on

Recorded here because they are the reason the revert is sequencing, not cancellation:

1. **Demon hybrid element typing** — Pokemon-style. The element hub already carries the substrate:
   6 concrete elements, a matchup ring plus `light ⇄ dark` mutual counter, per-component hybrid and
   a dual-type product rule, `MatchupShareK = 0.25`. **Hybrid typing is closer to a wiring question
   than a design one** — but the *demon-facing* design does not exist.
2. **A passive skill graph** — 12 primary stats × 2 build paths (offensive / defensive) × each
   element type × each status type, in the Path of Exile / Last Epoch tradition. **The owner expects
   seedsmith to build it**, which makes it a second, much larger generation program — and the real
   reason *"aspect feature is very huge"*.

Neither is designed. Both belong in their own idea phase, and `aspect` should not be specced until at
least the first exists.

### ✅ Q10 — data access: **EXPORT A DUMP. It is the development-phase source of truth.** *(owner)*

Owner: *"seedsmith is just a dev tool, it not work in the game runtime, so we need to dump data game
data and use it as SOT in our development phase, not player who play the game."*

Sharper than the option as posed. The dump is **not a cache of the database** — it is the SOT *for the
development phase*, and that phase distinction is what makes it correct:

| | Runtime (players) | Development (this pipeline) |
|---|---|---|
| Source of truth | the live game + the server's DB | the committed dump |
| seedsmith | **does not exist** — it never ships | reads only the dump |
| SQL | inside `FusionRpg.Data`, as the rule says | **none anywhere** |

This removes the DAL tension completely rather than negotiating an exemption: no SQL enters `tools/`,
so `guard-dal.ps1`'s `tools/` blind spot stops mattering for this feature instead of being relied on.

It also has direct precedent — `data/seed/demons/**` is already exactly this shape: a committed dump
emitted from the DB, read by seedsmith with no database access.

**Follow-on, now Q13:** what the dump contains, and how staleness against a newer capture is detected.

### ✅ Q11 — two ladders: **BOTH TEN RUNGS, with vocabularies that cannot be confused.** *(owner)*

Rarity keeps its botanical ladder — `chaff` · `sprout` · `grafted` · `cultivated` · `fused` ·
`chimeric` · `heirloom` · `firstseed` · `sunwoven` · `almanac`. `powerBand` takes a **threat vocabulary
from a different world entirely**, so the two can never read as the same kind of thing even at a
glance, while the 1:1 tuning symmetry is kept.

**The `powerBand` vocabulary itself is not chosen — that is Q14.** The constraint it must satisfy: no
word may plausibly belong to a botanical rarity ladder, and the ordinal direction must be obvious
from the words alone.

### ✅ Q12 — posture/resource conflict: **REJECT AND REPAIR, naming the conflict.** *(owner)*

A draft whose `resourceProfile` contradicts its posture — Bastion without `poise`, Focus without `qi`
— is rejected, and the repair prompt states the exact conflict. Same shape as the shipped anti-motif
validator, and it keeps the corpus internally consistent by construction rather than by review.

This is a **cross-field** validator, a shape seedsmith does not have yet: every validator today reads
one field. It is the same lesson `name_collision` taught one level out — a property no single field
can express needs a check that sees more than one field.

### ✅ Q14 — `powerBand` vocabulary: **THREAT-SCALE NOUNS.** *(owner)*

`nuisance` · `pest` · `menace` · `threat` · `scourge` · `terror` · `horror` · `nightmare` ·
`cataclysm` · `calamity` — ten rungs, direction legible without a legend, and no word could belong to
a botanical rarity ladder. Exact wording is a content pass; the register is decided.

### ✅ Q13 — the dump: **full tables + capture stamp + content hash**, plus a **preflight skill**. *(owner)*

`almanac_seed`, `spawn_stats` and `recipes` dumped whole, with the capture timestamp and a **content
hash** in `_meta`. Every generated file records that hash in its provenance, so *"was this derived
from the current dump?"* is a comparison rather than a guess — the same mechanism that caught the
stale theme registry on 2026-09-01.

**New requirement, owner-added:** a **preflight skill that runs before seedsmith** — an agent that
checks the requirements are present (dump exists, hash current, model reachable, venv installed) and
**asks the human to supply anything missing** rather than proceeding on a silent default. This is the
class of defect this program keeps paying for: the 2026-08-31 "real run" used scratch scripts that
lived nowhere, and a falsifier attempt on 2026-09-01 silently failed to plant and reported green. A
preflight that refuses is cheaper than a run that quietly used the wrong input.

### ✅ Q15 — rarity rungs: **ALL TEN, two pity guards at 70 and 90.** *(owner)*

Mirrors the item ladder exactly: the same ten rungs, the same two-guard shape, and rung **100
(`almanac`) deliberately unguarded by pity, requiring a deterministic quest/boss source**. One rarity
philosophy across items and demons, and the shipped two-counter `PityState` shape ports over with its
thresholds retuned rather than its structure rewritten.

### ✅ Q16 — `powerBand`: **NUMBER WINS, and the LLM AUDITS the result.** *(owner)*

Where a parsed `韧性`/`伤害` or an observed HP exists (719 species), a tuning table maps it to a rung
**deterministically** — no model call decides the band. The LLM then **reviews** the assigned band
against the lore and **flags disagreements without overriding them**. For the 170 prose-only species
the LLM assigns the band directly.

The most expensive of the three options, chosen for signal: **the disagreements are the product.** They
say where the almanac's own numbers misrepresent what a creature is — information no other mechanism
here can produce — and no model ever silently moves a magnitude.

### ✅ Q17 — `deployMode` / `variants` / `acquisition`: **all three classified from lore.** *(owner)*

The almanac usually says outright whether a zombie is driven or hypnotised. `variants` still must
never be a number the model emits: the model names *which* variants exist; **how many** a rung permits
stays rarity's count band.

Chosen for a reason much larger than these three fields — see finding **B10**, which constrains the
whole power ladder.

### ✅ Q18 — contribution: **the two number systems stay separate; only a progression delta crosses.** *(owner)*

Not one of the three options — the owner dissolved the question. The RPG keeps its own base stats for
demons and uses them fully in **web battle**; on the **lawn**, PvZ owns the entity's base and receives
only the demon's **progression/power delta**. The RPG database does not need to resemble PvZ's.

**This retracts the premise of B10 as first written**, which claimed the two ladders had to be
commensurate. They do not. See B10 for the corrected finding.

Left as a tuning choice, not an architecture one: whether that delta is `Flat` or `Increased`
(percent). Both ops exist; the percent form is available if a flat delta reads as noise on a
640,000-HP entity.

### ✅ Q19 — the existing 84: **RE-DERIVE EVERYTHING ONCE, then append-only from there.** *(owner)*

One clean rebuild against the new anchor, so all ~900 species are classified by identical rules and
there is no two-generation seam for a balance pass to reason about.

**The cost, stated plainly:** the 84 committed commander effects and 84 themes are regenerated
stochastically — today's names and doctrines will not survive verbatim. The append-only window is
already closed (G4 wrote its first row), so this is a **deliberate reviewed correction**, exactly the
category `generate_families.py`'s refusal guard was built to force a human to confirm.

**Sequencing that falls out of it:** the rebuild happens *once*, before the roster grows — re-deriving
84 rows is cheap, re-deriving 900 is not.

### ✅ Q20 — run scope: **FULL RUN, behind a state machine with pause / resume / cancel / rerun / overwrite-all.** *(owner)*

Owner: *"so we will generate what we want and stop when we want."*

**This is a real feature, not a run mode.** seedsmith today has per-subject checkpointing
(`SqliteSaver`, thread-id per subject) and skip-existing idempotency, but **no run-level control** —
a run is started and either finishes or is killed. What is asked for is a *job* with observable state:

| Control | What it must mean |
|---|---|
| pause / resume | stop between subjects without losing completed work — the checkpointer already supports the per-subject half |
| cancel | stop and leave the corpus in a consistent state, never half-written |
| rerun | regenerate a named subset — `--only` and `--stale` already exist as the primitive |
| overwrite-all | the deliberate full re-derivation Q19 calls for, and it must refuse without an explicit acknowledgement |

The existing `--only` / `--stale` / `--force` flags plus the append-only refusal guard are the raw
material; the gap is a **run record** that survives the process, so a resumed run knows what the
previous one finished.

### ✅ Q21 — HP vs damage: **ONE CURVE. The primary stats already carry the divergence.** *(owner)*

Owner: *"no, we use primary stats, hp and damage system already cover it, don't add more mechanism,
imbalance."*

**The prior-art finding was real but the conclusion was wrong.** Every surveyed system does inflate
durability faster than lethality (D2 6.2× vs 1.85×, PoE 2,989× vs 352×, D3 **16,958× vs 163×**) — but
those games have no aptitude layer, so the only place they *can* express the divergence is the curve.
This design has somewhere better to put it.

**Where the divergence actually lives.** `data/tuning/aptitudes.v2.json`'s `familyRead` classifies
`combat.power` and `combat.defense` **both as `magnitude`** — so both read `P(Θ)`, one curve, PS-3
intact. What differs is the **share** each channel receives, and share is the aptitude system's job:

| Aptitude | Its channel family (roster.json `role`) |
|---|---|
| Might | universal offence — power |
| Fortitude | mitigation — defense · absorption · reduction |
| Vigor | shield — capacity / regen / toughness |

Two demons at the same `Θ` with different `aptitudePrimary` already produce different effective HP and
different damage. **A per-channel growth rate would be a second mechanism computing the same thing,
and two mechanisms for one outcome is how imbalance gets in.**

**So the rule stands unchanged: one index, one function, magnitudes read `P(Θ)`.** The HP-vs-damage
question is answered upstream, by allocation.

### ✅ Q22 — the contract: **a well-defined JSON STRUCTURE with per-attribute descriptions — not a frozen enum list.** *(owner)*

Owner: *"closed contract is not closed enum, it is well defined structure json, so LLM know how to
generate each attribute in json because it understand the description of each attribute. each pipeline
must cover 1 or some attributes."*

**This reframes the question rather than answering it, and dissolves the Arknights tension.** The
options offered assumed "closed" meant "frozen vocabulary". It does not. Reliability comes from three
things, none of which is enum immutability:

1. **A defined JSON structure** — the schema shape, enforced by constrained decoding, so a malformed
   answer is unsampleable rather than merely detected.
2. **A description per attribute** — the model generates the right value because it *understands what
   the field means*, not because the value list is short. This is what JSON Schema `description` is
   for, and it is the part a frozen-enum framing ignores entirely.
3. **Narrow pipelines** — *"each pipeline must cover 1 or some attributes."* One question per call.
   A pipeline answering one well-described attribute is reliable in a way that a pipeline answering
   fifteen at once is not.

**Consequence for §6.2 ④, which was written against the wrong frame.** The freeze/grow split it
proposed is still *useful* — knowing which axis is cheap to widen remains true, and `element` really
is the expensive one — but it is **not the reliability mechanism**, and it should not be treated as a
constraint on the vocabularies. A vocabulary may grow (Arknights' content stream is legitimate) as
long as the structure and the descriptions stay well-defined.

**Consequence for pipeline design.** The contract's eighteen variables do **not** imply eighteen
pipelines, nor one pipeline. They imply a decomposition where each pipeline owns *one or a few*
attributes that share a judgement — for instance `elementPrimary` + `elementSecondary` together
(one typing judgement), `aptitudePrimary` + `aptitudeSecondary` together (one build judgement), and
`powerBand` alone (which is number-derived and LLM-audited anyway). **The decomposition is a spec-phase
decision; what this answer settles is the principle it must follow.**

---

## 6. The contract — revised against prior art

The owner's framing: *"we need to clear all variables that affect the generator before we make
generator spec. LLM need a closed contract to make reliable outcome."*

§6.1 is the contract as the twenty answers left it. **§6.2 is what the eight research passes
changed** — recorded as changes rather than folded in silently, so the reasoning survives.

### 6.1 The variables

| Variable | Openness | Vocabulary | Size | Source |
|---|---|---|---|---|
| `side` | closed | plant · zombie | 2 | captured |
| `elementPrimary` | closed | fire · ice · air · earth · light · dark | 6 | classified |
| `elementSecondary` | closed | same, **or `none`**; max 2 concrete, `omni` illegal in a slot | 6+1 | classified |
| `aptitudePrimary` | closed | the 12 aptitudes | 12 | classified |
| `aptitudeSecondary` | closed | the 12, **or `none`**; `pure` flag when same posture | 12+1 | classified |
| `posture` | closed | Force · Finesse · Bastion | 3 | **derived** from `aptitudePrimary` |
| `powerBand` | closed ordinal | nuisance → calamity (threat nouns) | 10 | number-derived, LLM-audited |
| `rarity` | closed ordinal | chaff → almanac (the item ladder) | 10 | classified |
| `deployMode` | closed | PlantAvatar · HypnoAlly | 2 | classified |
| `acquisition` | closed flags | Summonable · CaptureOnly · EventOnly | 3 | classified |
| `variants` | closed set | normal · ancient · mutated · corrupted · blessed · cursed · shiny | 7 | named by LLM; **count** from rarity |
| `resourceProfile` | closed subset | hp · stamina · hunger · spirit · qi · poise | 6 | classified, posture-validated |
| `basis` | closed ordinal | observed · stated · inferred · blocked | 4 | **derived** from which source produced the number |
| `family` | **open** | 19 today | grows | classified |
| `traits` | **open** | — | grows | classified |
| **`attackTempo`** | closed ordinal | **NEW — see §6.2** | ~5 | classified |
| **`reach`** | closed ordinal | **NEW — see §6.2** | ~4 | classified |
| **`targetPreference`** | closed | **NEW — see §6.2** | ~6 | classified |

**Eighteen variables. Two derived, one hybrid, fifteen classified. Not one is a number.**

### 6.2 What the research changed

**① Dual typing is required, not optional.** §4.1 computes it: single-element gives **12.6 units per
grid cell — Fire Emblem Heroes' failure zone**; dual gives **3.59 — the Genshin/FGO safe band**. The
owner already wanted Pokémon-style hybrid typing; this makes it load-bearing rather than flavour.
`elementSecondary` is therefore expected to be populated for most of the roster, not exceptional.

**② Three variables were missing, and they are universal everywhere else.** Attack rate, range and
targeting appear in *every* engine surveyed — SC2 carries a targets-allowed mask on 57 of 57 weapons —
and §4.2 measures their worth directly: type + **speed modes** + resistances lifts creature uniqueness
from **63% to 93%**. They are also the only axes besides element and aptitude that survive the
"a faster Banshee is still a Banshee" test (§4.8).

Verified absent on our side: `DerivedStatChannels` registers **`move.range` and nothing else** for
tempo or reach. `EntityBaseline.AttackInterval` exists but is *captured from PvZ*, not RPG-native —
so on the lawn PvZ supplies tempo, and in **web battle**, which the owner named as the home of the
RPG's own base stats, a generated demon currently has none.

They are ordinals, never numbers: `attackTempo` (something like *ponderous · slow · steady · quick ·
flurry*), `reach` (*melee · short · long · siege*), `targetPreference` (which population it prefers).
The deterministic layer turns each into a real interval or distance, exactly as `powerBand` becomes `Θ`.

**③ Every closed enum needs an explicit `none`, never an omitted field.** SC2's Archon, Ghost,
Ravager, Baneling and Queen carry **neither** Light nor Armored, which makes them immune to a large
share of every bonus-damage term in the game (§4.8). **Tag absence is a stat.** A model that is merely
unsure must not be able to hand a demon a hidden defensive buff by leaving a field out. `none` is an
answer; a missing key is a defect.

**④ Know the cheap-to-widen axis — but it is not the reliability mechanism.** §5 Q22 corrected the
frame this item was written under: a *closed contract* is a well-defined JSON structure with a
description per attribute, **not a frozen vocabulary**. So the table below is planning information,
not a constraint — a vocabulary may grow, as Arknights' does, provided the structure and descriptions
stay well-defined. Every studio widened the axis with
the fewest downstream dependencies and left the load-bearing one alone — FEH widened weapon colour
(feeds one ±20% modifier) five times and **never** touched movement type (~100 skills key off it).
For this design:

| Axis | Cost to widen | Verdict |
|---|---|---|
| `element` | matchup table + 196 combat channels + every reaction | **Most expensive. Treat as frozen.** |
| `aptitude` | 12 is a computed product of 3 postures × 4; the class system, aura ids and `ZombossPatterns` all key off it | **Expensive.** |
| `rarity` | five verified consumers (summon rates, both pity thresholds, `FusionRoller.SlotsFor`, `SoulEarnPolicy.DiscoveryDelta`, `shard.{rarity}` ids) | Expensive. |
| **`powerBand`** | **nothing consumes it yet** | **Cheapest — the designated growth axis.** |
| **`family` · `traits`** | already open by construction | Free. |

**⑤ `family` does not need to grow toward 270.** The rarefaction result (§4.1) says a *flat* taxonomy
at 900 units wants ~270 families — but Ragnarok Online gets 417 mechanical identities from **27
authored values across four orthogonal axes**. This contract is already multiplicative, so ~19
families is adequate and the axis stays open for organic growth.

**⑥ The distinctness burden does not rest on this contract.** §4.2 is unambiguous: stats do not
distinguish units — **abilities do**, and a 900-unit roster needs roughly **1,500–3,500 named ability
instances**. The anchor's job is to be a correct, cheap, orthogonal *index*; the thing that makes 900
demons feel different is the ability layer, which is a separate and much larger generation program
(§5 Q9's passive skill graph). **Recording this so a downstream session does not mistake a complete
anchor for a complete roster.**

### 6.3 The combinatorial position, restated

With dual typing the primary grid is **21 element-combinations × 12 aptitudes = 252 cells** for ~904
species — **3.59 per cell**, inside the band where no surveyed game has documented power-creep
problems. Adding `powerBand` and `rarity` multiplies the space enormously, but neither is a
distinctness axis: a stronger or rarer version of a demon is not a different demon (§4.8). **The grid
that matters is 252 cells, and it is adequately, not comfortably, filled.**

### 6.4 Both rulings have since been made — recorded so this section is not re-opened

This section listed two owed rulings while it was written. **Both were answered by the owner the same
day**, and the answers are §5 Q21 and §5 Q22. Kept as a pointer rather than deleted, because the
reasoning that made them look open is still worth reading:

- **Q21 — HP vs damage curve → ✅ ONE CURVE.** The prior-art divergence is real, but those games have
  no aptitude layer. Here the divergence lives in *allocation share*, not in the curve. See §5 Q21.
- **Q22 — closed contract vs content stream → ✅ neither, as posed.** A closed contract is a
  well-defined JSON structure with a description per attribute, not a frozen vocabulary. See §5 Q22,
  which also demotes §6.2 ④ from a constraint to planning information.

---

## 7. The aspect → atom gap, closed 2026-09-01

An earlier draft of this document recorded an honest gap: the aspect→atom decomposition was left out
because `effect-atom/definitions.md` had only been read at section level, and **that document wins
over any spec**. It has now been read in full. The gap is closed, and the answer is smaller than
expected.

### 7.1 An aspect needs no new atom vocabulary. It is a `species-passive` container.

`definitions.md:41` gives the container grammar:

```
container_id : ^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$
```

**`species-passive` is already a legal container kind.** So an aspect is not a new concept in the atom
layer — it is a container that puts atoms on a demon's effect list, exactly as an item or a trait does.

The model §0 states plainly, and it settles the shape:

> **Items have no behaviour. Actors do.** … An item, trait, skill, or species passive is a **source**
> that put the atom on the list. None of them participates at runtime.

So "what does this demon do" decomposes to: *which atoms does its species-passive container carry.*

### 7.2 ⭐ Rarity's atom mechanism is the mechanism we already adopted

This is the find. §4 of `definitions.md` defines what a container's rarity governs:

| Column | Meaning |
|---|---|
| `pool_rolls` | **how many atoms are drawn** |
| `min_tier` / `max_tier` | **the tier window the pool may offer** |

That is **exactly** the count-band-plus-tier-window that §5 Q4 adopted from the item rarity ladder,
and it is the same reason §4.5's prior art gives for rarity buying breadth rather than power. **The
demon-rarity decision and the atom-container mechanism are one mechanism, not two that must be
reconciled.** A demon's rung sets how many atoms its aspect rolls and from which tier window; nothing
new has to be designed for that.

Two further rules fall out for free:

- **Pool grouping defaults to `(family_id, variant)`**, so a container may roll *fire* power and *ice*
  power — two variants of one family. **That is dual-element typing expressed in the atom layer with
  no extra work.**
- `pool_rolls ≤ count(distinct group HAVING max(weight) > 0)`, and an all-zero-weight pool is rejected
  `UnsatisfiablePool`. **Silent under-filling is already impossible.**

### 7.3 What a generator may and may not emit

`definitions.md` decides this, and it tightens the contract in §6:

| Field | Who produces it |
|---|---|
| `atom_id` | **Never authored.** Derived as `{family_id}[.{variant}].t{tier}` and validated against its columns; a mismatch is `IdMismatch` |
| magnitudes | **Never authored.** Tier bands are authored *per channel family* and *"never copied across"*; units are non-negotiable — game units for primary channels, resolver points for derived, integer per-mille for chances |
| `family_id`, `variant`, `tier` | authorable |
| trigger | authorable, from **4 event triggers** (`OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`) plus `OnTimer` |

**⛔ `stat.modify` and `stat.derived` must declare NO trigger at all.** They are permanent modifiers;
apply and revert are runtime lifecycle. Authoring a trigger on either is `TriggerNotAllowed`, and
E7 must compile them as `EffectType = Passive` — *"a triggerless atom compiled with the default type
satisfies neither and would never apply at all. This is a compiler rule, not an optional one."*

This is the same shape as the anchor's own rule: **the model chooses categories; magnitudes come from
tables.** An aspect pipeline would emit `(family, variant, trigger)` and never an id, a tier value, or
a number.

### 7.4 What is still genuinely blocked

- **`stat.derived` is quarantined everywhere** (§2 W1) — no opcode, no bag branch, no sink arm, and
  battle reads channel mods only from `TraitBattleCatalog`. It is the kind an aspect would most
  naturally use to write a derived channel, so an aspect built on it is **inert until that wiring
  lands**. A wiring gap on a scheduled path, not a wall.
- **`aspect-scope` is reverted** (§5 Q9), so one demon has exactly one aspect — its own. The container
  is 1:1 with the species, which makes `species-passive.{speciesId}` the natural container id.
- **The full aspect vision remains blocked on two unbuilt programs** named in Q9: hybrid element
  typing, and the passive skill graph. This section closes the *decomposition* question, not those.

### 7.5 What this changes about the demon-seed program

**Nothing in this document's scope, which is why the gap was safe to carry.** Aspect generation is not
among §6's eighteen anchor variables and is not part of this program. What §7 buys is that when aspect
*is* specced, it starts from a settled decomposition rather than reopening the atom layer — and that
the rarity work already done is directly reusable rather than parallel.

---

## 8. What this document deliberately does not do

No spec. No plan. No schema. No code. **§7 closes the one honest gap this document previously
carried**; the aspect *decomposition* is settled, but aspect generation remains out of scope and
blocked on the two unbuilt programs §5 Q9 names.
