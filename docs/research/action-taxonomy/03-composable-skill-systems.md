# Composable skill systems — the grammar, the legality rules, and what stops the space degenerating

**Captured 2026-09-02. Research only** — no proposals, no recommendations, no "we should".

**Read [`../game-design/06-unsourced.md`](../game-design/06-unsourced.md) before commissioning any
follow-up.** It records what eight earlier passes searched for and could not find, and it is why this
pass leaned on datamines and source repositories instead of re-running the same queries. Modifier
stacking (`Flat / Increased / More`) and proc/trigger vocabulary are **already covered** in
[`../arpg-effects/02-modifier-stacking.md`](../arpg-effects/02-modifier-stacking.md) and
[`../arpg-effects/03-effects-procs-triggers.md`](../arpg-effects/03-effects-procs-triggers.md) —
this document cites them and does not re-derive them.

---

## The finding in one paragraph

Every shipped composition system separates **two vocabularies that look like one**: a small,
player-facing label set that communicates flavour, and a larger, internal type set that actually decides
legality. Path of Exile ships 58 display gem tags and roughly 180 internal `active_skill_type` values,
and its own wiki warns that the visible tags *"do not describe which gems can support other gems"* — the
gate is a per-support pair of `allowed_types` / `excluded_types` lists in the game data. Legality is
enforced in three layers, in the same order, almost everywhere: a **structural gate** that makes an
illegal combination unrepresentable, a **priced gate** that makes a legal-but-strong combination
expensive, and a **hand-written exclusion list** as the last resort. The single most reinvented device in
the survey is not a ban list at all — eight unrelated studios independently arrived at *restrict where a
modifier may live, not which modifiers may coexist* (Grim Dawn's trigger-type → skill-type, Diablo IV's
category → slot, Magic's keyword → colour, Hades' boon → ability slot, Borderlands' `BalanceDefinition`).
Every system that blew up did so the same way: **the priced thing and the powerful thing were not the
same thing** — PoE's triggers routed around mana cost, Noita's Chainsaw costs 0 mana and pays out in the
time axis, Diablo IV's Overpower survived an 80% coefficient cut because it was coupled to a stat players
already stacked, and three generations of Elder Scrolls spell-making priced a spell's declared parameters
while never pricing the state it acted on. Coherence is a separate problem from legality and is solved by
**template composition, never by prose**: NetHack shuffles *appearances* while authoring every *effect*;
Warframe's Riven names are a sorted encoding of the three biggest rolled stats; Cassette Beasts' move
names are a lossless spec; Shadow of Mordor rolls the *title first* and derives the traits from it, so the
two can never disagree. The failure all of them are avoiding has a name — Kate Compton's *10,000 bowls of
oatmeal*, mathematically unique outputs a player perceives as one undifferentiated mass — and Cassette
Beasts is the measured case: 120 authored monsters reviewed well, and its 14,000 generated fusions did
not.

**Where to start.** §10 is the grammar table across all systems, §11 is what each does about degenerate
combinations, §12 is naming, §13 is what got removed after shipping, and **§14 is what does not exist —
read it before commissioning any follow-up.** The deepest single profiles are §1 (Path of Exile, the only
publicly readable gating rule), §8.3 (Caves of Qud's history generator), §8.7 (Dwarf Fortress's shared
parameter envelope) and §13.1 (a uniqueness rule that shipped, ran nine months, and was withdrawn with a
published reason).

---

## 1. Path of Exile — support gems

**The single best-documented composition system in a shipped game, and the only one whose gating rule
is publicly readable as data.**

### 1.1 The two vocabularies — this is the load-bearing fact

PoE has *two* tag systems and only one of them gates anything.

| Vocabulary | Size | Where it lives | What it does |
|---|---|---|---|
| **Gem tags** (display) | **58** *(computed — tallied from the published `gem_tags.json`)* | shown on the gem in the UI | flavour and search; tells the player roughly what a gem is |
| **Active skill types** (internal) | **~180** *(computed — tallied from the published `active_skill_types.json`)* | game data, never shown | the actual legality gate |

Published exports: [`gem_tags.json`](https://repoe-fork.github.io/gem_tags.json),
[`active_skill_types.json`](https://repoe-fork.github.io/active_skill_types.json) (RePoE, a
GGPK datamine — **first-tier**).

The 58 display tags, verbatim: `fire, cold, lightning, chaos, spell, projectile, bow, melee, minion,
strength, dexterity, intelligence, aura, attack, area, duration, support, curse, chaining, totem,
trap, mine, movement, cast, vaal, grants_active_skill, trigger, warcry, golem, low_max_level,
channelling, herald, brand, physical, guard, travel, strike, blink, nova, banner, slam, stance, hex,
mark, orb, random_element, arcane, critical, exceptional, link, blessing, awakened, retaliation,
pact`.

The internal type list is a different shape entirely — it contains behavioural predicates such as
`Trappable`, `Totemable`, `Mineable`, `Multicastable`, `Multistrikeable`, `Triggered`, `Channel`
alongside the elemental and delivery types. **These are the ones that gate.**

**FACT.** The PoE wiki states plainly that gem tags *"do not describe which gems can support other
gems and are not meant to, even though they often align"*, and that compatibility is determined by the
support's complete sentence, not by one matching word
([PoE Wiki, Gem tag](https://www.poewiki.net/wiki/Gem_tag) — **second-tier**; the page itself was
unreachable this session behind an Anubis challenge, quote via search indexing).

### 1.2 The actual gating rule, from the data schema

The RePoE schema documents the support-gem record exactly
([RePoE `docs/gems.md`](https://raw.githubusercontent.com/repoe-fork/repoe/master/RePoE/docs/gems.md)
— **first-tier**, quoted verbatim):

| Field | Schema text |
|---|---|
| `letter` | *"The letter added on skill icons when they are supported by this gem. Only set for support gems."* |
| `supports_gems_only` | *"If true, this support gem only supports active skills coming from gems, not those provided by mods on items."* |
| `allowed_types` | *"Active skills must have **at least one** of these types to be supportable by this support gem."* |
| `excluded_types` | *"Active skills must **not have any** of these types to be supportable by this support gem."* |
| `added_types` | *"The active skill types this support gem **adds** to supported active skills."* |

So the rule is, precisely:

```
supportable(skill, support) :=
      (allowed_types(support) ∩ types(skill)) ≠ ∅
  AND (excluded_types(support) ∩ types(skill)) = ∅
```

**Three properties of that rule worth naming:**

1. **It is an allow-list plus a deny-list, not a single tag match.** The intuitive "share one tag"
   model is the allow-list half only, and it is not sufficient.
2. **`added_types` means supports mutate the thing they gate on.** A support can inject a type into
   the skill, which changes what *other* supports are then legal. The gate is computed over the
   composed skill, not the base skill. **INFERENCE:** this is what lets PoE express "this support
   turns your spell into something a totem can use" without a special case.
3. **The vocabulary is closed and large.** ~180 types is not a taxonomy a player learns; it is a
   machine-checkable contract. The 58-tag display list is the human-legible projection of it.

### 1.3 What a single support gem actually carries

`Spell Echo Support`, from [PoEDB](https://poedb.tw/us/Spell_Echo_Support) (a GGPK-derived database —
**first-tier-ish**; it is generated from the game files, not written by editors):

| Slot | Value |
|---|---|
| Tags | `Spell, Support` |
| **Cost & Reservation multiplier** | **140%** |
| Upside | *"Supported Skills Repeat an additional 1 times"* |
| Upside | *"Supported Skills have (40—54)% **more** Cast Speed"* |
| Quality | *"Supported Skills deal (0—10)% **increased** Spell Damage"* |
| **Hand-written exclusion** | *"Cannot support Vaal skills, totem skills, channelling skills, triggered skills, instant skills, retaliation skills, blink skills, or skills with a reservation."* |

`Increased Critical Damage Support`, same source
([PoEDB](https://poedb.tw/us/Increased_Critical_Damage_Support)): tags `Critical, Support`, **cost
multiplier 130%**, *"Supported Skills have +(100—138)% to Critical Strike Multiplier"*.

**That one gem shows all four control surfaces at once**: a structural type gate, a price
(140% cost), a magnitude with the `more` keyword (multiplicative, its own bucket), and a prose
exclusion list. See [`../arpg-effects/02-modifier-stacking.md`](../arpg-effects/02-modifier-stacking.md)
for `Flat / Increased / More` — the short version is that `increased` sums inside one bucket and
`more` multiplies as its own factor, so **a support that grants `more` is a genuine multiplier and a
support that grants `increased` is not.** GGG puts `more` on supports and `increased` on quality,
which is a deliberate ordering: the expensive multiplicative term is the one behind the socket cost.

### 1.4 How GGG stops degenerate combinations — five distinct mechanisms

| # | Mechanism | Example | Cost to the designer |
|---|---|---|---|
| 1 | **Structural type gate** | `allowed_types` / `excluded_types` | free at runtime; requires the ~180-type vocabulary to be maintained |
| 2 | **Cost multiplier** | Spell Echo 140%, Inc. Crit Damage 130% | one number per gem; must be re-tuned whenever resource generation changes |
| 3 | **Explicit downside line** | *"Supported Skills deal x% less Damage"* on many supports | authored per gem |
| 4 | **Hand-written exclusion prose** | Spell Echo's eight-clause "Cannot support…" | authored per gem; the maintenance sink |
| 5 | **A global budget** | six sockets, links, and in PoE 2 a one-copy-per-character rule | one rule, huge effect |

**The 3.15 "Expedition" manifesto is GGG stating mechanism 2 as policy.** From
[Game Balance in Path of Exile: Expedition](https://www.pathofexile.com/forum/view-post/24190927)
(**first-tier**, GGG's own forum post):

> *"When we're designing skills for Path of Exile, the mana cost of the skill is a mechanism to allow
> us to have large impactful effects… this entire mechanism is currently bypassed by triggering
> skills."*

> *"In 3.15, triggering skills through support gems will require paying their mana cost. In fact,
> sometimes it now costs more than casting the gem by hand."*

> *"We have also taken this opportunity to make mana multipliers on support gems more consistent."*

**This is the exact failure this project's cost model is built to avoid, stated by the studio that
hit it:** the price existed, and one composition path (triggers) routed around the price. The power
was legal, the cost was zero, and it took a full-league balance pass to fix.

**FACT.** Cast on Critical Strike Support carries both a cost multiplier (120% cost and reservation)
and a **0.15 s cooldown** — a second, non-price throttle stacked on the same gem, because pricing
alone had not held.

**FACT.** "Exceptional" / "Greater" supports are mutually exclusive with their base versions: you
cannot benefit from both a Greater Multistrike and a Multistrike on the same skill
([community summary](https://www.u4n.com/news/list-of-poe-exceptional-gems-supports-328.html) —
**third-tier**, flagged as such). This is an explicit "two halves of the same pair" ban, implemented
as an ordinary exclusion rather than as a general rule.

### 1.5 Scale

- **273 active skill gems and 177 support gems, 450 total** in PoE 1
  ([PoE wiki, via search indexing](https://pathofexile.fandom.com/wiki/Support_Skill_Gems) —
  **second-tier and undated**; Fandom returns HTTP 402 here so it could not be opened directly).
- PoE 2 *"introduces a new skill system with **240 active skill gems and 200 support gems**"*
  ([Wikipedia, Path of Exile 2](https://en.wikipedia.org/wiki/Path_of_Exile_2) — **second-tier but
  citation-backed**). PoE2DB lists 557 support gem *rows*
  ([PoE2DB](https://poe2db.tw/us/Support_Gems)), but that count includes tiered variants (`Bleed I`,
  `Bleed II`, …), so it is not 557 distinct effects — take 200 as the design figure and 557 as the row
  count.

**What breaks when tuned wrong:** the 3.15 answer. If the cost multiplier is the only brake and one
route bypasses cost, the whole ladder inverts — the cheapest builds become the strongest. If the
exclusion prose is the only brake, it grows without bound: Spell Echo needs eight clauses, and every
new skill archetype potentially adds a ninth to every existing support.

---

## 2. Noita — wand composition

**The most combinatorially open system ever shipped, and the clearest example of a system that is
*deliberately* allowed to degenerate.**

### 2.1 The parts

**422 spells in 8 categories** ([Noita Wiki (wiki.gg)](https://noita.wiki.gg/wiki/Spells) —
**second-tier**, but wiki.gg's Noita wiki is largely maintained from the game's own XML data files;
122 of the 422 are Projectiles). Categories: Projectile, Static Projectile, Passive, Utility,
Projectile Modifier, Material, Multicast, Other.

A wand is a container with **eight stats**
([Guide: Wand Mechanics](https://noita.wiki.gg/wiki/Guide:_Wand_Mechanics)): Shuffle, Spells/Cast,
Cast Delay, Recharge Time, Mana Max, Mana Charge Speed, Spread, Capacity.

### 2.2 The composition model — deck, hand, discard

This is not "a list of spells that fire in order". It is a card game
([Expert Guide: Draw](https://noita.wiki.gg/wiki/Expert_Guide:_Draw) — **second-tier**):

- **Deck** — the wand's spells in order (randomised on a shuffle wand).
- **Hand** — the spells participating in the current cast. A spell in the Hand cannot be drawn again
  in the same cast.
- **Discard** — spells already cast; the Hand empties into it at end of cast.

The draw loop:

1. The wand's `Spells/Cast` value creates a **draw budget**.
2. The top spell of the Deck moves to the Hand.
3. That spell executes — and **may itself draw more spells**.
4. A spell without mana or charges goes straight to Discard and the next is drawn instead.
5. Loop until the budget is spent or the Deck is empty. *"The wand cast ends after all the Draw has
   been spent or when there are no more spells to draw."*
6. Hand → Discard, apply Cast Delay. Empty deck → reload.

**Modifiers and multicasts are the same mechanism.** A modifier draws 1 spell; `Double Spell` draws
2; `Triple Spell` draws 3. Projectiles draw 0 (except triggers). **This is why the system is open:
"how many atoms does this atom pull in" is a per-spell number, and it composes recursively.**

### 2.3 The rule that keeps it coherent — shot state

The single most important sentence in the system
([Expert Guide: Draw](https://noita.wiki.gg/wiki/Expert_Guide:_Draw)):

> *"Spells never modify other spells… All spells modify something called a `shot state`."*

**FACT.** Modifiers do not bind to a target spell. They mutate an accumulator, and every projectile
created while that accumulator is live inherits it. Separation is achieved by starting a *new* shot
state — a separate wand cast, or a trigger payload, each of which gets its own.

**INFERENCE.** This is why Noita needs no legality rules between spells. There is no "may modifier X
attach to projectile Y" question, because attachment is not pairwise — it is a scope. The
combinatorial explosion is in what the scope accumulates, not in a pairing matrix, so the rule count
stays at O(1) while the outcome count stays at O(huge).

Triggers and timers create nested casts: *"Triggers cast another spell when the trigger projectile
hits something, and timers cast another spell when the timer projectile has existed for a certain
amount of time"* — each payload behaving as a miniature wand with its own shot state.

### 2.4 What actually constrains the player

Noita has almost no legality rules, so its brakes are all economic or environmental:

| Brake | Effect |
|---|---|
| **Capacity** | a wand holds N spells; N is a rolled property of the wand |
| **Mana Max / Mana Charge Speed** | a per-cast budget that scales with how many spells the cast drew |
| **Cast Delay / Recharge Time** | the throughput brake |
| **Shuffle** | a rolled property that destroys ordering, which is where most of the power lives |
| **Wand editing is location-gated** | you may only edit wands inside a Holy Mountain, and *"once the ceiling of a Holy Mountain collapses, it stops granting the ability to edit wands within"* ([Holy Mountain](https://noita.wiki.gg/wiki/Holy_Mountain)) |
| **Divine punishment for breaking the gate** | damaging Holy Mountain brickwork spawns a hostile Stevari *in every subsequent Holy Mountain for the run*; after three, Skoude replaces it |
| **Spell tiers** | which spells can appear in which biome |

**The location gate is the real one.** It converts "build the perfect wand" from a continuous
optimisation into a small number of discrete decision points, and it is enforced by an escalating
punishment rather than a hard block — a soft cap, not a wall.

### 2.5 What famously breaks it

**FACT — Chainsaw.** *"Chainsaw is the best spell for this purpose, since it provides infinite cast
delay reduction and some recharge reduction for 0 mana cost."* Placing a Chainsaw at the end of a
spell block with a large cast delay *"reduces the whole block's delay to a single frame"*
([Chainsaw](https://noita.wiki.gg/wiki/Chainsaw); mechanism corroborated on
[Steam discussions](https://steamcommunity.com/app/881100/discussions/0/4290313152632900538/) —
**third-tier**). A non-shuffle wand with one offensive spell, several Chainsaws and a Double Spell
becomes a machine gun.

**Why it breaks:** Chainsaw prices at **0 mana** but pays out in **the time axis**, and Noita's cost
model only meters mana. A part whose cost is denominated in a resource the model does not track is
free. That is the same defect class as PoE's triggers bypassing mana cost — different game, identical
shape.

**FACT — recursion.** Trigger and timer payloads nest, and the wiki maintains a dedicated
[Expert Guide: Calling and Recursion](https://noita.wiki.gg/wiki/Expert_Guide:_Calling_and_Recursion)
covering recursion limits. **INFERENCE:** a hard recursion cap exists because the composition rule is
recursive by construction, and an uncapped recursive generator is a crash, not a balance problem.

### 2.6 Noita's second, genuinely procedural layer

**FACT.** Lively Concoction and Alchemic Precursor *"have a randomized recipe for every seed. Each is
generated from three randomly selected powders and liquids"*
([Alchemy](https://noita.wiki.gg/wiki/Alchemy)). Recipes are always ternary; a recipe is 3 liquids,
or 2 liquids and 1 solid.

This is a *rule* generated per seed rather than content generated per seed — the effect is fixed and
authored, only the **key** is rolled. It is the same split NetHack uses (§5), applied to crafting
instead of identification.

**Developer commentary:** Petri Purho's GDC 2019 talk is about the falling-sand physics engine and
its emergent gameplay, not about wand grammar
([Game Developer coverage](https://www.gamedeveloper.com/design/video-understanding-the-remarkable-tech-and-design-of-i-noita-i-)).
**No Nolla Games statement on the design of the wand composition rules was found** — see
*What I could not find*.

---

## 3. Magicka — eight elements, one precedence ladder, and no combination table at all

### 3.1 The vocabulary and the queue

**FACT.** Eight base elements: **Water, Life, Shield, Cold, Lightning, Arcane, Earth, Fire**
([Wikipedia, Magicka](https://en.wikipedia.org/wiki/Magicka)). Default bindings put them on two rows
of four — `Q W E R` / `A S D F`
([Carl's Guides, Magicka spell guide](https://www.carlsguides.com/walkthroughs/magicka/spells.php) —
**second-tier fan guide, but internally consistent across its own pages and corroborated by the
Magicka 2 material below**).

**FACT.** The queue holds **up to five elements**
([Wikipedia](https://en.wikipedia.org/wiki/Magicka)), confirmed first-party for the sequel:
*"Combine up to five elements at a time"*
([Magicka 2 Steam store page](https://store.steampowered.com/app/238370/Magicka_2/)).

**FACT — four cast modes**, and they are separate from delivery type: aimed/ranged (right-click), area
(shift + right-click), self (middle mouse), weapon-imbue (shift + left-click)
([Wikipedia](https://en.wikipedia.org/wiki/Magicka); inputs from the
[LP Archive tutorial](https://lparchive.org/Magicka/Tutorial%201/)).

**FACT — compounds.** **Ice = Cold + Water**, **Steam = Fire + Water**, either order. Reversals:
Ice + Fire = Water, Steam + Cold = Water ([Carl's Guides](https://www.carlsguides.com/walkthroughs/magicka/spells.php);
[LP Archive](https://lparchive.org/Magicka/Tutorial%203/)).

**FACT — a compound occupies ONE slot, not two.** Stated for Magicka 2: *"These combination elements
are created with the last element in your Spell bar that can be combined. For example, if your Spell
bar reads Water-Water-Water and you add a Fire, it will now read **Water-Water-Steam**."*
([GameSkinny, Magicka 2 spell combination guide](https://www.gameskinny.com/tips/complete-magicka-2-master-spell-combination-guide/)).
The same arithmetic holds in Magicka 1: every published combo resolves to exactly five queue entries
despite six to nine keypresses — e.g. `Q-R-Q-R-Q-R-R-R` ("Cold Shotgun", 8 presses) resolves to
Ice · Ice · Ice · Cold · Cold *(computed from the key sequences on
[Carl's Guides](https://www.carlsguides.com/walkthroughs/magicka/spells.php))*.

**INFERENCE.** Compounding is therefore **slot compression** — it is how a five-slot queue reaches
states a five-slot queue could not otherwise hold. It converts keyboard dexterity into expressive
range.

### 3.2 The exclusion table — four opposed pairs plus one self-exclusion

**FACT** ([Carl's Guides](https://www.carlsguides.com/walkthroughs/magicka/spells.php), corroborated by
the [LP Archive](https://lparchive.org/Magicka/Tutorial%201/)):

| Pair | Note |
|---|---|
| Water ↔ Lightning | |
| Earth ↔ Lightning | Lightning is the only element with **two** opposites |
| Life ↔ Arcane | |
| Cold ↔ Fire | |
| **Shield ↔ Shield** | *"only 1 Shield may be used in each Spell"* |

**FACT — what happens when you queue both.** *"If you attempt to use two **Opposite** elements in one
spell, they will cancel each other out."* For Shield specifically: *"if you try to add Shield to a
combo that already has Shield, the two cancel out and you lose the element."*

**INFERENCE.** The cancellation **deletes both** rather than rejecting the input. You lose a slot's
worth of work instead of being blocked — the system punishes a mistake without ever refusing a
keypress.

**FACT — and this is the cleverest rule in the survey: the exclusion is evaluated over the *current
queue state*, not over the keystrokes.** *"it **is** possible to cast a spell using two elements that
would have normally cancelled each other out **as long as it's done in the correct order** … As long
as you cast the combination first, the element after it will not cancel any previous elements"*
([GameSkinny](https://www.gameskinny.com/tips/complete-magicka-2-master-spell-combination-guide/)).
Worked example: Water and Lightning are opposites, but Water + Fire → Steam first, and Steam +
Lightning is legal — *"Steam will Wet enemies much like water. Unlike water, it can be used along with
Lightning."*

**INFERENCE.** Compounding is a **legality-laundering operation**. Mastery of the system is knowing
which forbidden effects can be smuggled in through an intermediate state. That is emergent depth
produced entirely by a rule the player can state in one sentence.

### 3.3 The mechanism that stops nonsense — a total precedence order

**The player picks ingredients; the engine picks the verb.**

Two published statements of the ladder, which agree once you notice that one merges "projectile" and
"shard":

> *"Shields take precedence over projectiles (earth and ice), which take precedence over beams (life
> and arcane), which take precedence over steam, which takes precedence over lightning, which takes
> precedence over sprays (water, fire and cold)."*
> — [Wikipedia, Magicka](https://en.wikipedia.org/wiki/Magicka)

> *"Shield supercedes everything, but otherwise the order is: **Projectile, Shard, Beam, then
> Spray**."* — [Carl's Guides](https://www.carlsguides.com/walkthroughs/magicka/spells.php)

Per-element delivery types: Earth = Projectile · Ice = Shard · Arcane, Life = Beam · Water, Cold,
Lightning, Fire, Steam = Spray · Shield = Shield.

**FACT.** The delivery form is not chosen by the player and is not stored anywhere. It is **computed**
from the multiset by a fixed total order.

**INFERENCE, and it is the key one.** A table over multisets of size ≤5 from a 10-type vocabulary
would need thousands of rows. The precedence ladder replaces all of them with **one ordered list of
six delivery classes**. A queue of `Earth + Fire + Fire` is a flaming boulder, not a flamethrower,
because Earth outranks Fire — and the losing elements do not become garbage, they **degrade into
modifiers**: extra Earth increases boulder mass and direct-hit damage; extra Arcane extends beam
duration but not damage per hit; extra Life on a self-cast does nothing at all
([LP Archive](https://lparchive.org/Magicka/Tutorial%201/)).

### 3.4 The escape valve — a hand-authored named-spell dictionary on top

**FACT.** "Magicks" are discrete, authored spells triggered by an **exact key sequence**, unlocked
chapter by chapter, and explicitly not the same system: *"This is not to be confused with Magicka,
which are special named Spells, such as Grease and Thunder Bolt."* Their sequences **exceed the
five-slot queue** — Conflagration is `F-Q-F-F-Q-F-F-Q` (eight keys), Thunder Bolt `F-Q-A-S-A`
([Carl's Guides Magick list](https://www.carlsguides.com/walkthroughs/magicka/magicks.php)).

**INFERENCE.** Magicks are pattern-matched against the keystroke *sequence*, not composed from the
queue. That is why they can be longer than five and why they can produce effects with no
compositional derivation at all (Teleport, Charm, Raise Dead, Time Warp). **Anything the generative
grammar cannot express gets hand-authored as a named recipe** — the same "authored beats computed"
precedence the fusion survey found everywhere else.

### 3.5 Combinatorics, and the "over 1000 spells" claim

**FACT (negative).** The official Magicka Steam page makes **no numerical claim at all** — its feature
list says only *"Innovative and dynamic spell casting system"*
([Steam](https://store.steampowered.com/app/42910/Magicka/)). The *"over 1,000"* figure appears only in
a fan guide (*"There are over 1,000 possible spell combinations however not that many of the spells are
unique"* — [Carl's Guides](https://www.carlsguides.com/walkthroughs/magicka/spells.php)) and a
user-edited wiki ([Giant Bomb](https://giantbomb.com/wiki/Games/Magicka)). Magicka 2's official copy is
qualitative: *"you will have **thousands of spells** at your fingertips"*
([Steam](https://store.steampowered.com/app/238370/Magicka_2/)).

**Computed, for scale** *(all four figures computed in this pass, not sourced)*:

| Space | Count |
|---|---|
| Multisets of size 1–5 over 8 base elements | **1,286** — which reads exactly like "over 1,000" |
| …after applying Shield ≤ 1 and the four opposed pairs | **584** legal base-only queue states |
| …× 4 cast modes | **2,336** |
| Magicka 2, 11 queue-resident types with its published opposition table, × 4 cast modes | **8,084** |

**INFERENCE.** "Over 1,000" is almost certainly the raw multiset count with the exclusion rules
ignored. The exclusion rules prune roughly 55% of the raw space. And the fan guide's own caveat —
*"not that many of the spells are unique"* — is the honest reading, because element multiplicity often
only scales a number rather than changing the outcome.

### 3.6 Arrowhead's own account

**FACT — from the studio's postmortem**
([Postmortem: Arrowhead Game Studios' Magicka, Game Developer](https://www.gamedeveloper.com/business/postmortem-arrowhead-game-studios-i-magicka-i-)
— **first-tier**):

> *"We also wanted a dynamic spell casting system — something that in games should make the player feel
> as if they were taming the secret arcane energies of the world and not just tapping a button to drain
> an impersonal mana-bar. This ambition gave rise to the simple idea of having elements that give
> meaning to each spell, and vary its efficiency."*

And the advice they rejected, in the section titled "No Need to Ask Obi-Wan":

> *"'You'll have to remove friendly fire,' '**you can't let the player begin with all elements, he
> should have to find them throughout the game**,' and 'players should be able to hotkey their favorite
> spells so that they don't have to press several buttons just to do one attack,' were several of the
> suggestions we heard. All of these suggestions directly interfered with the main design philosophies
> at Arrowhead."*

**INFERENCE.** This is the load-bearing decision for anyone copying the system. The vocabulary is
granted **in full at minute one**; progression lives entirely in the player's fluency plus the Magick
dictionary. Gating elements would have converted a fluency curve into an unlock curve, and they refused
it explicitly.

**FACT — and a caution.** The same postmortem records that the spell system was *not* what made the
game fun in playtest. They spent weeks *"trying several different approaches for how to tweak different
spells"* to no avail; the fix came from elsewhere — *"we happened to accidentally kill each other more
often and everything was more 'haphazard' — something we managed to get just right by increasing the
power of the spells and toughness of the enemies."*

### 3.7 What Magicka 2 changed

**FACT.** Same 8 base elements, same 5-slot queue, and **three** hybrids instead of two: *"Ye olde
hybrid elements Steam and Ice from Magicka 1 are back, joined by the brand new **Poison**!"*
([Steam](https://store.steampowered.com/app/238370/Magicka_2/)). Recipes: Steam = Water + Fire, Ice =
Water + Frost, **Poison = Water + Arcane**. Opposites now include the hybrids themselves — Earth/
Lightning, Arcane/Life, **Ice/Fire**, **Steam/Ice**, Water/Lightning, Frost/Fire, **Poison/Life**,
Shield/Shield ([GameSkinny](https://www.gameskinny.com/tips/complete-magicka-2-master-spell-combination-guide/)).

**INFERENCE.** Magicka 2 **promotes the compounds to first-class citizens of the exclusion table**. In
Magicka 1 the compounds had no opposites; in Magicka 2 they do. Every hybrid routes through Water,
making Water the hub of the compound graph and the element with the busiest interaction rules.

**What the whole system costs:** one total order over delivery classes, one small symmetric exclusion
table, three compound recipes, and a hand-authored Magick dictionary as the escape valve.

**What breaks when tuned wrong:** the ladder is a *total* order, so a badly placed element is either
always-dominant (it wins precedence and erases everything queued with it) or never-visible (everything
outranks it). There is no middle setting.

---

## 4. Tyranny — Sigils: four part types, a whitelist, and a hand-written name table

Obsidian's spell crafting is the closest shipped analogue to a **priced part assembly**, and it is the
one system here whose *naming* strategy is the exact opposite of Warframe's (§12.1).

The structured tables below come from the Tyranny Wiki, reached through a reader proxy
(**second-tier prose, but its tables are structured game data and are internally consistent with the
developer interviews cited**).

### 4.1 There are FOUR part types, not three

**FACT** ([Tyranny Wiki, Spell creation](https://tyranny.fandom.com/wiki/Spell_creation)):

> *"**Core Sigils** describe the **type** of magic you are going to cast: Fire magic, frost magic,
> illusions, etc. **Expression Sigils** determine the **form** of the spell, i.e. whether it is cast on
> a single target, an area, in a line, in a cone, on your weapon or armor, etc. **Accent Sigils** are
> basic upgrades to various aspects of the spell. **Enhancement Sigils** are optional secondary
> modifiers that affect the form of the spell, allowing you to combine multiple sigils, modify the
> targets affected, and so on."*

Press coverage routinely flattens Enhancements into Accents. Mechanically they *are* a kind of accent,
but they differ in kind: **Accents are ramped intensities on one axis; Enhancements are discrete on/off
effects** — *"Each of these is its own unique effect, rather than ramping intensity of the same
effect"* ([Enhancement Sigils](https://tyranny.fandom.com/wiki/Enhancement_Sigils)).

**The rosters:** **11 Cores** (Atrophy, Emotions, Fire, Force, Frost, Illusion, Life, Lightning, Stone,
Terratus, Vigor) · **9 Expressions** · **9 Accents** · **20 Enhancements**.

| Piece | Sets | Costs Lore? |
|---|---|---|
| **Core** | damage/effect type and school | **no — free** |
| **Expression** | **targeting shape**, and the base Lore cost | yes |
| **Accent** | magnitude on one axis, at a chosen **tier I–IV** | yes, per tier |
| **Enhancement** | a discrete added effect or structural rewrite | yes |

### 4.2 Expressions — shape and base price in one part

**FACT** ([Expression Sigils](https://tyranny.fandom.com/wiki/Expression_Sigils)):

| Expression | Shape | Lore | Compatible Cores |
|---|---|---|---|
| Focused Intent | single target nearby | **15** | 10 of 11 |
| Distant Impact | ranged bolt + small area | **30** | 9 |
| Channeled Strength | cone from caster | **35** ⚠ | 9 |
| Material Force | affects target's **weapon** | **50** | 6 |
| Guarded Form | affects target's **armor** | **60** | 8 |
| Directed Force | line | **70** | 5 |
| Proximate Action | aura around target | **80** | 8 |
| Influential Domain | circular area | **90** | 4 |
| Chaotic Descent | random targets in a circle (a "rain") | **100** | 4 |

⚠ [Player.One's spell list](https://www.player.one/tyranny-pc-mage-build-complete-list-spells-core-accent-and-expression-sigils-568466)
gives Channeled Strength **40**; every other cell agrees exactly between the two sources. Unresolved.

**Note that the price is monotone in how much board the shape covers** — single target 15, line 70,
circle 90, rain 100. Shape *is* the main cost axis.

### 4.3 Accents — every axis separately tiered and separately priced

**FACT** ([Accent Sigils](https://tyranny.fandom.com/wiki/Accent_Sigils)):

| Accent | Axis | Tiers → Lore |
|---|---|---|
| Strength | damage +20/30/40/50% | 20 / 30 / 40 / 50 |
| Precise Action | accuracy +15/30/45/60 | 15 / 25 / 35 / 45 |
| Piercing Strength | armour penetration +4/8/12 | 15 / 30 / 45 |
| Staggering Force | interrupt 1 s / 2 s / 3 s | 25 / 35 / 45 |
| Limitless Boundaries | area +1/2/3 m | 25 / 35 / 45 |
| Timeless Form | duration +25/35/35% | 15 / 25 / 35 |
| Reaching Grasp | range +2/4/6 m | 10 / 20 / 30 |
| Cyclical Energies | cooldown −15/25/35% | 10 / 20 / 30 |
| Bounding Bolts | +1 / +2 bouncing projectiles | 30 / 50 |

**Nothing in the taxonomy sets cast time.** The nearest equivalents are recovery controls living in
Enhancements — Rapid Casting (*"No Recovery time for the spell"*, 25) and Spellsurge (*"−10% Recovery
time (global) for 6s"*, 30). **INFERENCE:** cast speed is a character stat (Quickness), not a
construction axis, which is why it is off the sigil grid.

### 4.4 The gating rules — four of them, and only one is a budget

**(a) Core × Expression is a hand-authored whitelist, not a cross product.** **FACT.** Each Expression
lists explicit compatible Cores, and the lists are irregular — Focused Intent takes 10, Chaotic Descent
and Influential Domain take 4 each, Force works with only 4 Expressions.

**Computed:** summing the compatibility lists gives **63 legal Core+Expression pairs out of 11 × 9 =
99** — about 64% of the grid. Counting the per-Core spell lists instead gives 64; the one-spell
difference is wiki noise. **Roughly a third of the combinatorial grid is deliberately empty, and every
filled cell is a hand-named, hand-described spell.**

**(b) Lore is the budget, and the Core is free.** **FACT.** *"Each combination of sigils requires a
certain level of Lore (**a sum of the total lore cost of Expression, Accent, and Enhancement
Sigils**)"* and *"Characters must have a skill at least equal to the spell's total difficulty in order
to cast the spell."*

```
Lore requirement = Lore(Expression) + Σ Lore(chosen Accent tiers) + Σ Lore(Enhancements)
```

**(c) There is NO cap on the number of Accents.** **FACT**, from the game director
([GameSpot, quoting Brian Heins](https://www.gamespot.com/articles/pillars-of-eternity-successor-tyranny-lets-you-cra/1100-6443246/)):

> *"The number of accent sigils you can add is limited only by the number of sigils you possess, but
> there is a catch. Every spell has a 'lore requirement.' The more complex and powerful the spell, the
> higher the lore requirement… **you can fine-tune a spell's lore requirement by adjusting the power of
> the associated accent sigils.** Instead of cranking your intensity sigil all the way to 11, for
> example, you could set it to slightly lower power to ensure your party members can actually cast the
> spell."*

**INFERENCE.** An Accent's *tiers* are alternatives to each other; *different* Accents stack freely. So
this is a **continuous budget allocation, not a slot puzzle**, and the tiering exists precisely so the
budget is spendable in small change. That is a materially different feel from a fixed-slot system, and
it is the closest published analogue to a per-rung structure budget.

**(d) The Expression doubles as a buff-stacking key.** **FACT**
([Expression Sigils](https://tyranny.fandom.com/wiki/Expression_Sigils)):

> *"Expression Sigils also determine what beneficial spells can stack with one another. For example,
> the Gift of the Golem spell (Guarded Form expression) will stack with Titan's Touch (Focused Intent
> expression) … But Titan's Touch will not stack with Spectral Blur as they both use the expression
> Focused Intent."*

**INFERENCE.** One field does double duty — targeting shape *and* stacking family. That is how Obsidian
stopped players from stacking nine custom buffs of the same kind without writing a single exclusion
rule.

### 4.5 Naming — the opposite of a grammar

**FACT.** *"Spells have default names based on the selected Core and Expression. You can rename your
spells using the spell interface."*
([Spell creation](https://tyranny.fandom.com/wiki/Spell_creation); confirmed by
[GameSpot](https://www.gamespot.com/articles/pillars-of-eternity-successor-tyranny-lets-you-cra/1100-6443246/):
*"Once you're done adding and adjusting accents, you can even name your magical creation."*)

**INFERENCE, and it is the takeaway.** The name is **not composed from the parts**. It is a lookup on
the **Core × Expression pair only**, into a hand-written table of 63–64 names, and Accents and
Enhancements never touch it. Fire + Distant Impact is always "Fireball"; Stone + Directed Force is
always "Giant Boulder"; Atrophy + Chaotic Descent is always "Acid Rain" — however you accent them.
**Obsidian bought flavour by hand-writing 64 names rather than by writing a name grammar, and gave the
player a rename box to cover the gap.** Compare Warframe (§12.1), which writes the grammar instead and
authors nothing.

### 4.6 Why the system is shaped this way

**FACT — Brian Heins** ([PCGamesN](https://www.pcgamesn.com/tyranny/tyranny-spell-creation-artifact-weapons)):

> *"you create your own spells by finding magical sigils in the world. So you start with a core sigil
> like fire, frost or lightning, and then decide how that magic type is going to express itself,
> whether it's a long range bolt or a cone, like a great cone of flames, or a fireball."*

And ([GameSpot](https://www.gamespot.com/articles/pillars-of-eternity-successor-tyranny-lets-you-cra/1100-6443246/)):
*"We're not a class-based game, we're skill-based. So you can make whatever hybrid character you want…
Magic basically sits on top of that."*

**INFERENCE.** Because Tyranny is classless, spell crafting is the *only* place a character's magical
identity is expressed, and Lore is the single dial deciding how much identity you can afford. There is
no spell list to pick from, so there is nothing else for magic progression to consist of.

**What it costs:** a 63-cell whitelist and 64 hand-written names, plus per-tier prices on nine axes.
**What breaks when tuned wrong:** the Lore sum is a flat addition with no interaction term, so any
Accent whose real value scales super-linearly with another (area × duration, for instance) is
underpriced by construction.

---

## 5. Roguelikes — NetHack, Dungeon Crawl Stone Soup, and the identification layer

**The key distinction, stated up front and then proved from source: these games procedurally generate
*appearances*, and author every *effect*.** That is the opposite split from what "procedurally
generated magic items" usually implies, and it is why the results never read as nonsense.

### 5.1 NetHack — the object table

Objects are declared in one table with the true name and the appearance side by side
([`src/objects.c`, NetHack-3.6](https://raw.githubusercontent.com/NetHack/NetHack/NetHack-3.6/src/objects.c)
— **first-tier, the game's own source**):

```c
POTION("speed",            "dark green",  1, FAST, 42, 200, CLR_GREEN),
POTION("invisibility", "brilliant blue",  1, INVIS, 40, 150, CLR_BRIGHT_BLUE),
WAND("fire",        "hexagonal", 40, 175, 1, RAY, IRON, HI_METAL),
WAND("cold",            "short", 40, 175, 1, RAY, IRON, HI_METAL),
```

The second argument is the appearance. `ELBIB YLOH` — "HOLY BIBLE" reversed — is literally
`SCROLL("genocide", "ELBIB YLOH", 1, 15, 300)`, one of 22 hand-written labels.

**The decoys are explicit, commented, and unevenly distributed.** The file carries
`/* Extra descriptions, shuffled into use at start of new game */` followed by entries with generation
probability `0` — `SCROLL(None, "FOOBIE BLETCH", 1, 0, 100)`, `"TEMOV"`, `"READ ME"`,
`"ETAOIN SHRDLU"`, `"FNORD"`, `"STRC PRST SKRZ KRK"`, and for wands `WAND(None, "forked", 0, …)`,
`"spiked"`, `"jeweled"`. These can never be generated as items; they exist purely to poison
identification by elimination.

Pool sizes, counted directly from `objects.c` at the NetHack 3.6.7 tag *(computed)*:

| Class | Real types shuffled | Decoy appearances | Total pool |
|---|---|---|---|
| Scrolls | 22 | **20** | **42** |
| Wands | 24 | **3** | **27** |
| Potions | 25 (water excluded) | 0 | 25 |
| Rings | 28 | 0 | 28 |
| Amulets | 9 | 0 (+2 fixed) | 9 |
| Spellbooks | 40 | 0 (+3 fixed) | 40 |

**FACT.** Scrolls get 20 decoys — nearly doubling the pool — and wands 3, while **potions, rings and
amulets get none and are therefore fully solvable by elimination.** That asymmetry is a deliberate
per-class tuning decision, not an oversight.

### 5.2 The shuffle, and what is excluded from it

From [`src/o_init.c`](https://raw.githubusercontent.com/NetHack/NetHack/NetHack-3.6/src/o_init.c)
(**first-tier**):

```c
static char shuffle_classes[] = {
    AMULET_CLASS, POTION_CLASS, RING_CLASS, SCROLL_CLASS,
    SPBOOK_CLASS, WAND_CLASS, VENOM_CLASS,
};
static short shuffle_types[] = {
    HELMET, LEATHER_GLOVES, CLOAK_OF_PROTECTION, SPEED_BOOTS,
};
```

The swap itself:

```c
do
    i = j + rn2(o_high - j + 1);
while (objects[i].oc_name_known);
sw = objects[j].oc_descr_idx;
objects[j].oc_descr_idx = objects[i].oc_descr_idx;
objects[i].oc_descr_idx = sw;
```

**FACT — the exclusions are the design.** `obj_shuffle_range()` holds items out of the shuffle:
*"potion of water has the only fixed description"*; for amulets, scrolls, spellbooks, rings, wands and
venom the comment is *"exclude non-magic types and also unique ones"*. Armour excludes pre-identified
and unique pieces.

**Three properties worth naming:**

1. **Shuffling is a permutation inside a class, never across classes.** A wand's appearance is always
   a wand appearance. The class *is* the type contract.
2. **Non-magic and unique members are pinned.** A generator that randomises everything destroys the
   landmarks players navigate by; NetHack pins exactly the items that must stay recognisable.
3. **The randomisation is per-game, not per-item.** All "dark green" potions in one game are the same
   potion. The roll produces a *mapping*, not a stream of unique objects — which is what makes
   deduction possible, and therefore what makes it a game rather than noise.

### 5.3 Crawl — a real composed-name grammar, in source

DCSS generates scroll labels, Pandemonium lord names and randart names from one function,
`make_name()`
([`crawl-ref/source/item-name.cc`](https://raw.githubusercontent.com/crawl/crawl/master/crawl-ref/source/item-name.cc)
— **first-tier, the game's own source**).

Length:

```cpp
size_t len = 3;
len += random2(5);
len += (random2(5) == 0) ? random2(6) : 1;

if (name_type == MNAME_SCROLL)   // scrolls have longer names
    len += 6;
```

The consonant table is **position-aware** — clusters are tagged by where in a word they may legally
appear:

```cpp
static const string consonant_sets[] = {
    // 0-13: start, middle
    "kl", "gr", "cl", "cr", "fr", "pr", "tr", "tw", "br", "pl",
    "bl", "str", "shr", "thr",
    // 14-26: start, middle, end
    "sm", "sh", "ch", "th", "ph", "pn", "kh", "gh", "mn", "ps",
    "st", "sk", "sch",
    // 27-55: middle, end
    "ts", "cs", "xt", "nt", "ll", ...
```

Vowels are weighted by repetition inside the source string, which is the cheapest possible weighting
scheme:

```cpp
static char _random_vowel()
{
    static const char vowels[] = "aeiouaeiouaeiouy  ";
    return vowels[random2(sizeof(vowels) - 1)];
}
```

`a e i o u` appear three times each, `y` once, space twice — so a space is rarer than any vowel and
`y` rarer than the five main vowels, with no weights array at all.

Constraints that stop nonsense, per the source: no repeated vowel where the previous letter was the
same (`y`/`i` special-cased, 2-in-5 chance otherwise), no consonant clusters across word boundaries,
no space between consonant pairs or at the name's edges, and a hard length cap for one name type
(`MNAME_JIYVA` caps at 8).

And a guard nobody documents but everybody needs:

```cpp
static string _unforbid(string name)
{
    set<string> forbidden_words = set<string>{
        "puvax", "snt", "avttre", "xvxr", "ovgpu", ...
    };
```

**FACT.** The forbidden-word list is stored ROT13-encoded, so the slurs are not literally in the
source tree, and a name matching one is replaced by its own ROT13 encoding. **A procedural name
generator over a phonetic alphabet will eventually emit a slur, and shipping one requires an explicit
filter.**

Scroll labels are then upper-cased:

```cpp
if (name_type == MNAME_SCROLL || i == 0 || name[i - 1] == ' ')
    uppercased_name += toupper_safe(name[i]);
```

producing the familiar `ZEFOKY WECZYXE` form.

**What this solves:** it makes an unidentified item *memorable* — the player can say "the ZEFOKY one" —
while carrying zero semantic content, which is required, because the label must not leak the effect.

**What it costs:** three tables (consonant clusters by position, vowel weights, forbidden words) plus a
handful of adjacency rules. That is the entire maintenance surface, and it has been stable for years.

**What breaks when tuned wrong:** raise the length or loosen the adjacency rules and outputs stop being
pronounceable, so they stop being memorable, and identification degrades into note-taking. Tighten them
and every scroll sounds like every other scroll — the oatmeal failure (§12.6).

The function's own docstring is worth quoting, because it is honest about the cost: *"Used for:
Pandemonium demonlords, shopkeepers, scrolls, random artefacts. **This function is insane, but that
might be useful.**"* Guards beyond the tables: a short name falls back to the literal string `"plog"`,
a trailing vowel gets a consonant appended, and clusters extend the target length (`len +=
consonant_set.size() - 2`) rather than truncating the word.

### 5.4 Crawl's *other* appearance system — composed, then rejection-sampled

DCSS does not permute a fixed list the way NetHack does. It **samples from a composed space and
rejects duplicates** ([`ng-init.cc`, `initialise_item_descriptions()`](https://github.com/crawl/crawl/blob/master/crawl-ref/source/ng-init.cc)
— **first-tier**):

```c
case IDESC_WANDS:
    you.item_description[i][j] = random2(NDSC_WAND_PRI * NDSC_WAND_SEC);
    if (coinflip())
        you.item_description[i][j] %= NDSC_WAND_PRI;
    break;
```

followed by an explicit rejection loop — *"Test whether we've used this description before"* — resample
until unique within the class.

| Class | Composition | Pool |
|---|---|---|
| Wands | 16 adjectives × 12 materials | **192** |
| Rings | 13 × 29 | **377** |
| Amulets | 13 × 29 (different lists) | **377** |
| Potions | 15 qualifiers × 22 colours | **330** |
| Staves | 10 × 4 | **40** |
| Scrolls | generated label, seeded by `seed_1 \| seed_2 << 8` | **151 × 151 = 22,801** |

The "curved wand" of the classic example is literally in source: `wand_secondary_string()` = `{"",
"jewelled ", "curved ", "long ", "short ", "twisted ", "crooked ", "forked ", "shiny ", "blackened ",
"tapered ", "glowing ", "worn ", "encrusted ", "runed ", "sharpened "}` over `wand_primary_string()` =
`{"iron", "brass", "bone", "wooden", "copper", "gold", "silver", "bronze", "ivory", "glass", "lead",
"fluorescent"}`, assembled as `secondary << primary << " wand"`.

**FACT — the `coinflip()` is a deliberate style mixer.** Half the time the adjective is dropped, giving a
bare "iron wand"; half the time a two-word "curved iron wand". `_random_potion_description()` does the
same. **INFERENCE:** without it every item in the game would be a two-word compound and the prose would
read as machine-generated. **Mixing one-word and two-word forms is a very cheap naturalness trick**, and
it is the single most portable line in this section.

The 22,801 scroll-label figure is confirmed by the game's own test harness — `_test_scroll_names()` is
documented as *"Write all possible scroll names to the given file"* and iterates
`for (int i = 0; i < 151; i++) for (int j = 0; j < 151; j++)`.

### 5.5 Angband — the same problem, solved by a Markov chain

Worth the contrast, because it is the third distinct answer to one problem.

**FACT.** Angband's flavour pools live in a static data file,
[`lib/gamedata/flavor.txt`](https://github.com/angband/angband/blob/master/lib/gamedata/flavor.txt) —
rings 39 random + 4 fixed (One Ring, Narya, Nenya, Vilya), amulets 20 + 4, staves 35, wands 35, rods 35,
mushrooms 20, potions 59, scrolls 51 *(computed by parsing the file)*. **Every entry carries a colour and
a text — except the scroll entries, which carry a colour and no text** (`flavor:283:White`).

**FACT.** Scroll titles are generated
([`src/obj-util.c`, `flavor_init()`](https://github.com/angband/angband/blob/master/src/obj-util.c)):
`randname_make(RANDNAME_SCROLL, 2, 8, end, 24, name_sections)` packs words of 2–8 letters into an
18-char buffer, then deduplicates by retry.

**FACT — the generator is a letter-trigram Markov chain trained on a word list**
([`src/randname.c`](https://github.com/angband/angband/blob/master/src/randname.c)):
`typedef unsigned short name_probs[S_WORD+1][S_WORD+1][TOTAL+1];`, built by `build_prob()` over
`(prev, cur) → next` counts, credited in-file to *"W. Sheldon Simms' random name generator algorithm
(Markov Chain stylee)."* The corpus is
[`lib/gamedata/names.txt`](https://github.com/angband/angband/blob/master/lib/gamedata/names.txt):
**601 Tolkien names** in section 1, **587 mostly-Latin scroll words** in section 2 (`abduco`, `absorbeo`,
`accipio`, `acerbus`…, with a joke prelude of `abracadabra`, `hocus`, `pocus`, `shazam`). The file's own
comment states the intent: *"If you put in Latin words, it would come up with words that sound Latin (as
it does when choosing the text for scrolls)."*

**⭐ Three games, one problem, three authoring costs — the cleanest comparison in this document:**

| Game | What the designer authors | Output space | Where naturalness comes from |
|---|---|---|---|
| **NetHack** | every appearance by hand (42 scroll labels) | fixed; must be padded with decoys | human craft, one string at a time |
| **DCSS** | a **positional grammar** (18 vowel slots, 37 consonant slots, 67 position-tagged clusters) | ~22,801 labels | hand-encoded phonotactics |
| **Angband** | a **corpus** (587 Latin words) | effectively unbounded | the corpus's own statistics, inferred |

**INFERENCE.** The Angband model is the cheapest to *retune* — swap the word list and the flavour
changes wholesale — and the most expensive to *constrain*, because there is no rule to edit.

### 5.6 Identification — what it solves, and what DCSS removed

**What problem it solves.** DCSS states its own axioms in
[`crawl_manual.rst` §N "Philosophy (pas de faq)"](https://github.com/crawl/crawl/blob/master/crawl-ref/docs/crawl_manual.rst)
(**first-tier**): major goals include *"challenging and random gameplay… meaningful decisions (no
no-brainers)… avoidance of grinding (no scumming)"*, minor goals include *"clarity (playability without
need for spoilers)"*. Its "Crusade against no-brainers" section: *"wherever there's a no-brainer, that
means the development team put a lot of effort into providing a 'choice' that's really not an
interesting choice at all."*

**INFERENCE.** Identification converts a static, memorisable item table into a per-game decision
problem. **It is a randomiser applied to knowledge rather than to content**, which is why it costs almost
nothing to author and yields a fresh opening every run.

**Price identification** was not removed — the *tell* was. **FACT** (changelog, 0.9 era):
*"Price reform: scrolls, potions, rings. **Bad items cost more than 1 gold now.**"*

**What DCSS removed, with versions** (all **FACT**, from
[`changelog.txt`](https://github.com/crawl/crawl/blob/master/crawl-ref/docs/changelog.txt)):

| Version | Change |
|---|---|
| **0.14** (2014) | *"All scrolls now identify on read."* · *"Weapons are identified immediately on wield."* |
| **0.15** (2014) | *"Identify scrolls now always identify a single item."* · *"Jewellery automatically identifies once equipped."* |
| **0.16** (2015) | *"**Wand type is auto-identified on pickup.**"* |
| **0.20** (2017) | *"All types of manuals are pre-identified."* |
| **0.27** (2021) | *"**Cursed items removed**"* · *"Rings of attention, rings of stealth, rings of teleportation, amulets of inaccuracy, scrolls of remove curse, **scrolls of random uselessness**, and boots of running are removed."* · *"Equipment is identified once the player is in reach, without needing to be worn."* |

In current master, wands identify by **walking over them** —
`/// Auto-ID whatever stuff the player stands on.` `void id_floor_items()`
([`items.cc`](https://github.com/crawl/crawl/blob/master/crawl-ref/source/items.cc)).

**INFERENCE, and the accurate summary.** DCSS did not remove identification. It **narrowed it to potions
and scrolls and removed the downside risk.** Everything else self-identifies on contact; the items whose
whole purpose was to punish experimentation were deleted in 0.27, and curses went with them. What
survives is a *resource-allocation* puzzle (which unknown potion do I drink in an emergency?) rather than
a *trap-avoidance* puzzle. **Note that the changelog states the changes plainly but rarely the reasons —
this reading is against the published philosophy document, not a quoted developer statement.**

---

## 6. Grafting modifiers onto a base skill — Grim Dawn, Last Epoch, Diablo, Hades

Four systems where the composed object is **an existing skill plus attached modifiers**, which is the
closest structural match in this survey to an action container drawing atoms.

### 6.1 Grim Dawn devotion — the binding rule is a type check, and the proc rate is normalised

**Structure (first-tier, [Crate's own game guide](https://www.grimdawn.com/guide/character/devotion/)):**
*"You can earn up to 50 Devotion Points throughout Grim Dawn's 3 Difficulty Modes. (With the Ashes of
Malmouth expansion, the Devotion Point cap is increased to 55.)"* At reveal the map was
*"79 Constellations, each with 3-8 stars for a total of 425. With 50 Devotion points, you can fill in
about 12% of this massive celestial map"*
([Grim Misadventure #82](https://forums.crateentertainment.com/t/29152), Zantai) — later
*"434 Stars across 82 Constellations with 50 Celestial Powers"*
([#84](https://forums.crateentertainment.com/t/29178)).

Five affinities (Ascendant, Chaos, Eldritch, Order, Primordial). *"Affinity is not spent to unlock
Constellations. Instead, your totals must meet or exceed the requirements"* — and requirements are
**per-colour composites** ("6 Order, 8 Ascendant"), not one number. *"The most powerful Constellations
do not grant any Affinity bonuses at all"*, so the top tier is a pure sink.

**The binding rule, verbatim (first-tier):**

> *"Not all skills can be assigned to a Celestial Power. Celestial Powers that trigger off of taking
> damage or blocking can only be assigned to **Buff Skills**, while Celestial Powers that trigger off
> of dealing damage, dealing critical strikes, or slaying enemies can only be assigned to **Attack
> Skills**."*

> *"The secondary effects are restricted to **one per skill**, but there is **no hard limit** on how
> many you can use at the same time."*
> ([GM #83](https://forums.crateentertainment.com/t/29165))

**Why the type rule exists — Zantai's own answer, and it is a scoping argument, not a balance one**
([Steam](https://steamcommunity.com/app/219990/discussions/0/2425614361140727836/)):

> *"Due to the way buffs function, it allows you to proc skills off of any attack you use, not just the
> aura itself."*

Confirmed as intent by a bug fix (v1.0.3.0,
[topic 42541](https://forums.crateentertainment.com/t/42541)): *"Fixed an issue where on Attack/Crit
Celestial Powers could be assigned to certain Buff skills. This was **unintended behavior as buffs are
meant to be bound exclusively to on Hit/Block/Low Health Powers**."*

**INFERENCE.** The trigger-type gate is not a power cap. It exists because a trigger needs an
unambiguous owner: an always-on buff has no well-defined firing event, so a proc bound to it leaks onto
every damage source in the build.

**The anti-degeneracy device — proc rate normalised to the host skill** (v1.0.0.3,
[topic 31822](https://forums.crateentertainment.com/t/31822)):

> *"Celestial Powers now have their chance to activate scale based on the assigned skill, with **longer
> cooldown abilities having up to a 100% chance to trigger**."*

**INFERENCE, and it is the most transferable idea in this section.** Without rate normalisation the
optimal binding is always the fastest-firing host, and every other host is noise. Normalising makes the
host's *identity* matter and its *speed* not matter.

**Crate's stated policy on tightening bindings after ship** — a one-way ratchet
([Zantai, 2021](https://forums.crateentertainment.com/t/109362/97)):

> *"I would consider preventing Blades of Wrath to bind with pets such as Blade Spirits to be the
> nuclear option. It's really not enjoyable to find your devotion setup to suddenly be invalidated.
> It's something I think we've only ever done in beta … and since then we've only **reenabled** devotion
> assignment options, not disabled any. A shotgun penalty for the proc could be considered."*

**FACT.** Balance problems from a *legal* binding are fixed by nerfing the proc, never by deleting the
binding.

**What it costs:** an affinity graph with per-colour thresholds, which also makes respec order-sensitive
— *"you cannot unlearn Devotion Points invested in a Constellation that provides you an Affinity bonus
which you need to maintain another Constellation."*

**What breaks when tuned wrong:** if the proc-rate normalisation is missing or partial, every build
converges on the same fastest host.

### 6.2 Last Epoch — the budget is the balance, and exclusion is a printed no-op

**The budget, stated as design intent (first-tier, EHG dev blog "Skills to Pay the Bills",
[forum topic 1333](https://forum.lastepoch.com/t/1333)):**

> *"These trees are **specifically designed to not be completable**. You will only obtain **20 points**
> to put into each tree total. This means that certain powerful nodes are **inherently mutually
> exclusive** from each other. This creates an interesting decision point. … In the end **a single node
> in a tree can be like adding a whole new skill to the game.**"*

And the slot cap, with an unusually explicit justification:

> *"if you apply restrictions to a system like this then it makes for interesting decision making. If we
> allow you to take 10 skills with you out into the wild then several of them will be the same ones you
> always take because they are just good supplemental skills to always bring. This is not interesting
> decision making."*

**Scale — read carefully, the aggregate figures here are weak.** Counting EHG's published
[Mage Skill Tree](https://support.lastepoch.com/hc/en-us/articles/46362122141467-Mage-Skill-Tree) page
directly gives **352 nodes across roughly 12 trees, about 29 nodes per tree** *(computed from one
official page)*. A whole-game tally of ~3,700 nodes across ~128 trees was reported during this pass but
**could not be re-verified against a source and is not carried here.** Treat "about 29 nodes per tree"
as the only defensible number, and treat any whole-game node count as unattested.

**Transformation nodes rewrite the skill's tags, not just its numbers.** Verbatim from EHG's official
Mage tree page (**first-tier**):

> *Fireball / Plasma Ball*: *"Half of Fireball's base fire damage is converted to lightning … Fireball
> **gains** a Lightning tag."*

> *Lightning Blast / Focal Blast*: *"Lightning Blast now **deals damage to all enemies in a line, but no
> longer chains or forks.**"*

**INFERENCE.** Rewriting the tag alongside the damage is what stops conversion from bricking gear: every
downstream modifier keyed on the tag follows automatically. A conversion that changed only the number
would silently create dead stats. (Several further conversion nodes reportedly *swap* the tag rather
than add one — *"Swaps Warpath's Physical tag for a Void tag"* — but **that node text was not
re-verified this pass; do not cite it.**)

**Exclusivity — expressed as a printed runtime no-op, not an allocation block.** Verbatim from the same
official page:

> *Lightning Blast / Focal Blast*: *"This node is **incompatible with** the Insidious Conduction node.
> **If you have taken both, Insidious Conduction will not work.**"*

The conflicting node stays allocatable and simply stops working, and **both sides of the pair print the
rule and name the same winner**, so resolution is deterministic and visible without the UI modelling
exclusion groups at all.

Scanning that one official page, exclusivity language appears on roughly **2% of its nodes**
(*"incompatible"* ×2, *"unless it has already"* ×1, *"If you have taken"* ×5) *(computed)*. The same
page shows three escalating forms:

| Form | What it does |
|---|---|
| **Reroute** | the node redirects its effect when a conflicting node is present, so no conflict ever occurs |
| **Precedence** | the conflict is defined rather than forbidden — *"…unless it has already been converted…"* |
| **Nullification** | last resort — one named node is declared inoperative, on both nodes' text |

**The idea that scales — exclude against a property, not a name.** A node that reads *"this has no effect
if the skill's damage is converted"* covers every present and future conversion node, where a named-pair
list would need one row per pair. **UNVERIFIED:** the specific example reported for this pattern
(*Detonating Arrow / Arcing Blast*) was not re-fetched; the pattern itself is visible in the *precedence*
form quoted above, which keys on "has already been converted" rather than on a node name.

**INFERENCE.** Property-based exclusion is O(1) as conversions are added; a named-pair list is O(n²).
That, rather than restraint, is the likely reason explicit exclusions stay at a low single-digit
percentage of nodes.

**Prerequisites are points-thresholded, not node-thresholded** — a node's predecessors carry a minimum
points-invested requirement rather than a binary "allocated" flag. **The per-threshold edge counts and
node point-cap distributions reported during this pass could not be re-verified and are omitted.** The
per-node cap field itself is confirmed as `Max Points`.

**Gear adds points, not nodes** ([Skill Level Affixes](https://support.lastepoch.com/hc/en-us/articles/46363217964059-Skill-Level-Affixes)):
affixes *"can exceed the normal 20 level limit of skills"*, and removing the gear removes the points —
*"Nodes which have lost points in this way will be highlighted in red when you next view that skill's
tree."* **INFERENCE:** the tree's shape stays a design-time constant, and an invalid state is *displayed*
rather than silently repaired.

**EHG on avoiding an obviously-correct path** (first-tier, "Making Last Epoch | Skill Design", Justin
Carpenter, [forum topic 78179](https://forum.lastepoch.com/t/making-last-epoch-skill-design/78179)):

> *"Commonly desirable nodes should be fairly accessible, while niche or unusual nodes are more
> difficult to reach"*
> *"individual nodes should not be so potent that you feel forced to build it in a particular way"*

That is a constraint on node **potency**, not on node count. On runaway scaling they settled on
Heartseeker's *"Recurve Chance being multiplied by 0.8 each time it recurves"*, chosen because it
*"helps constrain the value of stacking Recurve Chance"* while giving *"a simple description of its
behavior that players could intuitively understand"* — **a diminishing-returns constant picked for
legibility as well as balance.**

### 6.3 Diablo IV aspects — restrict where a modifier may live

**FACT (second-tier, [Maxroll](https://maxroll.gg/d4/wiki/legendary-aspects); see *What I could not
find* — no Blizzard page publishes this table).** Five categories with a slot eligibility map:

| Category | Eligible slots |
|---|---|
| Offensive | 1H Weapon, 2H Weapon (**power ×2**), Gloves, Ring, Amulet (**power ×1.5**) |
| Defensive | Helm, Chest, Pants, Shield, Amulet (×1.5) |
| Resource | Ring, Amulet (×1.5) |
| Utility | Shield, Helm, Chest, Pants, Boots, Gloves, Amulet (×1.5) |
| Mobility | Boots, Amulet (×1.5) |

And the stated purpose: *"Aspects are divided by category, and can be imprinted on different slots
depending on their category. **This forces you to think thoroughly about your build instead of just
putting every damage multiplier you can find on all your items.**"* The slot multiplier applies to the
aspect's downside too.

**Imprinting (first-tier, [Blizzard quarterly update, Dec 2021](https://news.blizzard.com/en-us/diablo4/23746639)):**
*"The Occultist can extract a legendary power from a Legendary item, crystallizing it into Essence
**while destroying the item in the process**. That Essence can then be implanted into another Legendary
item, **overriding the power that was present in the item at that time**."* Season 4 changed acquisition
([Loot Reborn](https://news.blizzard.com/en-us/article/24077223/galvanize-your-legend-in-season-4-loot-reborn)):
*"**Salvaging Legendary items stores their powers as Legendary Aspects in your Codex of Power to be
reused indefinitely.**"*

**Duplicate resolution is max, not sum.** **FACT (community-observed on Blizzard's own forum,
[thread](https://us.forums.blizzard.com/en/d4/t/question-stacking-legendary-aspects/14412); no blue
post).** Two copies of the same aspect do not stack — the stronger applies and the weaker is **greyed
out** in the tooltip. **INFERENCE:** resolving a duplicate to max *and showing the loser* is strictly
better than prohibiting it, because the player learns the rule from the UI.

**There is no rule against aspects that multiply.** **FACT (negative, checked across 1.0–1.2, 2.0, 2.3,
2.4, 3.0 and the Season 4 blog).** Blizzard has never stated one. Instead, additive-vs-multiplicative is
an **authored per-effect field, patched in both directions**:

- 2.4: *"Fixed an issue where Aspect of Fleet Wings was multiplying its damage bonus per stack."*
- 2.4: *"Fixed an issue where Stormcrow's Aspect was granting additive rather than multiplicative damage."*
- 3.0: *"Mark of the Old Wolf's Poisoning damage bonus is now multiplicative instead of additive."*

**And the clearest statement of the failure mode**, from the official 2.3 Developer's Notes
([patch 2.3](https://news.blizzard.com/en-us/article/24215995/diablo-iv-patch-notes-2-3)):

> *"On the PTR, we tested an **80% reduction** in these additive damage values, but **still found
> Overpower based builds to be overperforming** other options due in part to this inherent bonus."*

The eventual fix was structural — they removed the coupling entirely (*"Removed the inherent additive
damage portion of Overpower that was based on your Life and Fortify amounts"*) and replaced it with a
bounded 50%[x]. **When a multiplier scales off a stat the player already stacks, cutting its coefficient
does not fix it.**

**The Diablo III precedent — four distinct grafting verbs**, all first-tier or from Blizzard's own game
guide text:

| Verb | Example |
|---|---|
| Grant a rune you did not select | *Cosmic Strand*: *"Teleport **gains the effect of the Wormhole rune**."* |
| Grant **all** runes | *Angel Hair Braid*: *"Punish **gains the effect of every rune**."* |
| Trade a cost for a cooldown | *Aether Walker*: *"Teleport no longer has a cool down but costs 25 Arcane Power."* |
| Amplify one rune conditionally | *Trag'Oul's Corroded Fang*: *"The **Cursed Scythe rune** for Grim Scythe now has a 100% chance to apply a curse…"* |

Kanai's Cube is **three extra slots, partitioned exactly like the equipment slots they substitute for**
([Patch 2.3.0 Now Live](https://news.blizzard.com/en-us/article/19859662/patch-2-3-0-now-live)):
*"Players may have **one Weapon, one Armor, and one Jewelry power** equipped at a time."* It can never
give a fourth weapon power.

### 6.4 Hades — attach points, replacement semantics, and authored synergy pairs

Included because its shape is the closest in the survey to *"atoms attach at a small closed set of
points"*.

**FACT (second-tier — Hades Wiki via a reader proxy; wiki.gg returned 401).** Zagreus has **five ability
slots, each holding exactly one boon**: *"Zagreus' **Attack**, **Special**, **Cast**, **Dash**, and
**Call** abilities have one boon slot, and cannot hold multiple ability boons."* Each slot has a naming
convention — Strike (Attack), Flourish (Special), Shot (Cast), Dash, Aid (Call) — so *Heartbreak Strike*
and *Divine Strike* are visibly the same slot from different gods.

**Replacement, not stacking.** Accepting a new boon for an occupied slot replaces the old one, and
*"Exchanges increase rarity of the slot by one, keeping the level the same"* — the system pays you a
small consolation for overwriting.

**Duo boons are hand-authored synergy pairs with prerequisites.** **FACT.** There are **28 Duo Boons**
across 8 gods (neither Hermes nor Chaos offers one). Each requires specific boons from *both* named
gods, expressed as an OR-list per god — e.g. *Heart Rend* needs one of (Heartbreak Strike, Heartbreak
Flourish, Crush Shot, Passion Dash) **and** one of (Deadly Strike, Deadly Flourish, True Shot /
Hunter's Flare).

**And the exclusion is implicit in the slot model:** a Duo becomes unofferable if a *different* god's
boon occupies a slot the Duo needs, until you replace it. **INFERENCE:** because every boon lives in a
named slot and slots hold one thing, "these two cannot coexist" never has to be written down — it falls
out of the slot arithmetic.

**What it costs:** 28 authored prerequisite records, each naming specific boons on both sides. That is
the maintenance price of authored synergy: it is O(pairs you choose to write), and every new boon can
invalidate an existing prerequisite list.

---

## 7. Magic: The Gathering and Hearthstone — keyword vocabularies that compose

### 7.1 There is no forbidden-combination list, and that is a verified negative

The official Comprehensive Rules text
([`MagicCompRules 20260819.txt`](https://media.wizards.com/2026/downloads/MagicCompRules%2020260819.txt)
— **first-tier, Wizards' own file**) was downloaded and grepped rather than summarised.

**FACT (negative, by grep).** The Comprehensive Rules contain **no rule of the form "these two keywords
cannot coexist."** No forbidden-pair list exists.

Conflicts are resolved by a four-stage architecture instead:

| Stage | Rule | Text |
|---|---|---|
| Fixed application order | **613.1f** | *"Layer 6: Ability-adding effects, keyword counters, ability-removing effects, and effects that say an object can't have an ability."* |
| Deterministic tiebreak | **613.3** | characteristic-defining abilities first, then **timestamp order** |
| Dependency detection | **613.8a** | an effect *"depends on"* another if applying the other *"would change the text or the existence of the first effect, what it applies to, or what it does"* |
| Loop fallback | **613.8b** | *"If several dependent effects form a dependency loop, then this rule is ignored and the effects in the dependency loop are applied in timestamp order."* |

The only true prohibition is a single generic veto, **113.11**: an effect may say an object *"can't
have"* an ability, and then *"It's also impossible for an effect or keyword counter to add that ability
to the object."*

**INFERENCE.** This is an O(1) architecture where a forbidden-pairs table would be O(n²). Magic has
~170 keyword abilities (**702.170** is the highest subsection) and needs no pairwise rules at all.

### 7.2 Stacking behaviour is declared per keyword, at authoring time

**FACT.** The phrase *"are redundant"* appears **32 times** in the rules, once per static keyword:
**702.2f** deathtouch, **702.3c** defender, **702.9c** flying, **702.11h** hexproof, **702.16m**
protection *from the same quality*, **702.17c** reach, **702.18b** shroud, **702.19g** trample.

**FACT — the exception proves it.** **Ward** (702.21a) is a *triggered* ability and carries **no**
redundancy clause, so instances stack. **Enlist** gets the one explicit opposite rule, **702.154d**:
*"Multiple instances of enlist on a single creature function independently."*

**And the retirement that follows from it — the highest-value finding in this section.** Prowess was
removed from the evergreen set, and Rosewater's stated reason is compositional, not about power
([Blogatog](https://markrosewater.tumblr.com/search/prowess+evergreen)):

> *"It also was the only triggered evergreen creature keyword, which caused different issues (for
> example, **it stacked where others didn't**)."*
> *"The reason prowess didn't work out was it was **too unlike the other evergreen creature keywords**."*

**INFERENCE.** A vocabulary with one member whose composition rule differs from the rest pays for that
member permanently. The defect is the non-uniform entry, not the runtime that special-cases it.

**Subsumption is real and spelled out.** **702.16j**: *"'Protection from everything' … Such a permanent
or player can't be targeted by spells or abilities and can't be enchanted by Auras. Such a permanent
can't be equipped by Equipment, fortified by Fortifications, or blocked by creatures. All damage that
would be dealt to such a permanent or player is prevented."* — one keyword subsuming shroud, most of
hexproof, and damage prevention.

**The apparent "contradiction" case is resolved structurally.** **702.3b**: *"A creature with defender
can't attack."* **508.1c** calls that a **restriction**; **508.1d** calls "attacks if able" a
**requirement**, and only *"the maximum possible number of requirements … without disobeying any
restrictions"* must be met. A requirement that cannot be met is simply not obeyed — no error, no ban.

**Keywords are a first-class graftable payload with a closed list.** **122.1b** enumerates exactly which
keywords a keyword counter may be: *"flying, first strike, double strike, deathtouch, decayed, exalted,
haste, hexproof, indestructible, lifelink, menace, reach, shadow, trample, and vigilance, as well as any
variants of those keywords."*

### 7.3 Rosewater on vocabulary size and per-card limits

All from [Blogatog](https://markrosewater.tumblr.com) (**second-tier: a designer's public Q&A, not a
published document — Tumblr blocks direct fetch, so these were read through a text proxy**):

| Question | Answer |
|---|---|
| *"How many evergreen keywords can an uncommon have?"* | *"**Three's usually the top limit. Maybe a four as a stretch once in a blue moon.**"* |
| *"Do you think there's a hard limit to how many evergreen keywords there are?"* | *"**There is a limit. Too much vocabulary raises 'barrier to entry'.**"* |
| On two new mechanics per card | *"**We consciously avoid putting two new mechanics on the same card, except in very rare cases.**"* |
| On keyword soup | *"many enfranchised players don't realize the true barrier of a lot of vocabulary words to newer players"* |

**The admissibility rule for a new keyword is uniformity of text**, stated three ways:
*"**Keywords need to replace exact words**"* · *"**A keyword has to have the exact same text every time
it's used**"* · *"**Keywords have to be the same exact text** and those cards all work a little
differently."* And there is no wildcard: *"the black border rules **can't handle saying 'all abilities'
or even 'all keyword abilities'**."*

**The vocabulary is faction-tagged.** The current evergreen creature keywords, each with the colours
allowed to carry it ([Blogatog, March 2026](https://markrosewater.tumblr.com/post/812039328070090752/whats-the-current-list-of-evergreen-creature)):
Deathtouch (B/G) · Defender (W/U/B/R/G) · Double strike (W/R) · First strike (W/R) · Flash (U/B/G/W) ·
Flying (W/U/B) · Haste (B/R/G) · Hexproof (U/G) · Indestructible (W/G/B) · Lifelink (W/B) · Menace
(B/R) · Reach (R/G) · Trample (R/G) · Vigilance (W/U/G) · Ward (W/U/B/R/G). **Fifteen keywords, and
*"most evergreen keyword mechanics can exist in three colors"*.**

**INFERENCE.** A keyword→colour eligibility table is structurally identical to Diablo IV's
category→slot table and Grim Dawn's trigger-type→skill-type table. Three unrelated studios arrived at
the same containment device: **restrict where a modifier may live, not which modifiers may coexist.**

Retirement reasons for other keywords, from
[Evergreen Eggs & Ham](https://magic.wizards.com/en/news/making-magic/evergreen-eggs-ham-2015-06-08)
(**first-tier**): shroud → hexproof because *"We understood that their opponents couldn't target their
creatures but didn't get that they couldn't either"*; fear dropped partly because it *"couldn't be used
in other colors (a big issue when we want to be careful how many creature keywords we keep evergreen)"*.
Protection was demoted from evergreen to **deciduous** in 2022
([Deciduous](https://magic.wizards.com/en/news/making-magic/deciduous-2022-03-28)).

The **Storm Scale**
([Storm Scale: Ravnica](https://magic.wizards.com/en/news/making-magic/storm-scale-ravnica-and-return-ravnica-2016-05-02))
runs 1 (*"Will definitely see again"*) to 10 (*"this would require a major miracle"*). Dredge is a 10:
*"one of the most broken mechanics we've ever made."* Haunt is a 9: *"miserable to play."*

### 7.4 Hearthstone — a two-tier vocabulary, and the reasoning behind restricting one side of a bad pair

**FACT ([hearthstone.wiki.gg](https://hearthstone.wiki.gg/wiki/Keyword) — **second-tier**).** Roughly
**32 evergreen keywords** usable in any set (Battlecry, Deathrattle, Discover, Divine Shield, Lifesteal,
Rush, Taunt, …) plus **six evergreen class keywords locked to one class each** (Choose One → Druid,
Combo → Rogue, Outcast → Demon Hunter, Overheal → Priest, Overload → Shaman), plus roughly **40 set
keywords** confined to one expansion (Corrupt, Dredge, Echo, Excavate, Magnetic, Spellburst, Titan…).
Same evergreen/deciduous split as MTG, with the class lock adding a faction gate.

**The multiplying-combination problem, and why they restricted the keyword instead of the modifiers**
([wiki.gg Charge page](https://hearthstone.wiki.gg/wiki/Charge), relaying designer Max McCall, 2017 —
**third-tier: the original interview URL does not resolve**):

> *"Charge is historically one of the most problematic abilities in Hearthstone. This is due to the
> potential for Charge minions to be **combined with Attack-increasing buffs and copy effects**,
> producing excessively powerful burst combos, in the worst cases resulting in one turn kills."*

> *"One solution to this could be to **restrict the availability of buffs and other key combo pieces**,
> but these types of effect offer a larger design space, with the result that **restricting them would
> detract from the game far more significantly than simply restricting the availability of Charge**."*

**FACT.** The resolution was **a new, weaker keyword, not a prohibition**: Rush is Charge minus the
ability to hit the enemy hero on the turn it lands.

**And the two coexist by precedence, needing no rule:** *"Charge functionally overrides Rush effects…
This is because Rush doesn't actually prevent a minion from attacking the opponent's hero, it only
allows the minion to attack enemy minions while it would normally have summoning sickness."*

**INFERENCE.** Rush was authored as a **permission**, not a restriction. Permissions compose for free;
restrictions have to be reconciled pairwise.

**Silence is a fully specified "strip all grafted modifiers" operation**
([wiki.gg](https://hearthstone.wiki.gg/wiki/Silence)). It removes enchantments, card text, keywords,
deathrattles, ongoing effects, one-time effects and triggered effects. It does **not** remove minion
types, transform effects, damage, permanent mind-control, already-generated minions, enchantments the
target already applied elsewhere, ongoing effects sourced from another card's aura, or playerbound
enchantments. *"Silence itself is not an enchantment; rather it is an effect which is applied once to
the target minion."* **The hard parts of that list are exactly the two a strip operation always has to
answer: what happens to things the modifier already produced, and what happens to modifiers whose source
is external and still live.**

**Discover — the one piece of dev commentary in this survey specifically about a generation pool**
([wiki.gg](https://hearthstone.wiki.gg/wiki/Discover)):

> *"One of the last decisions made during the development of Discover was to **restrict the effect to
> neutral and class cards**. Prior to this, Discover effects were able to generate **any** card. With a
> far larger card pool to choose from, this made it much harder for players to predict which card an
> opponent's Discover effect had provided, and **making the effect almost impossible to play around**.
> The game-wide card pool also produced too much 'class bleed'."*

Class cards were then weighted **4× neutral cards** to keep class identity legible, and the whole effect
was priced off the existing card-draw curve because playtesting found *"Discover was in most cases
fairly similar in value to a card draw effect."*

**INFERENCE — the most directly applicable lesson for generated content in this document.** The failure
of an unconstrained pool was **not** power level. It was **unpredictability for the opponent** and **loss
of faction identity**. The fix was to narrow the pool to the generating actor's own identity, then
re-weight so that identity stayed visible against a larger neutral pool.

---

## 8. Games that procedurally *generate* abilities rather than authoring them

The highest-value category, and the thinnest. For each: **what are the parts, what are the legality
rules, and how is the result kept coherent.**

### 8.1 Warframe Rivens — the closest thing to a shipped procedural modifier generator

Fully covered in §12.1–12.2, because its naming grammar and its cost model are the same finding. In
summary:

| | |
|---|---|
| **Parts** | 31 possible stat attributes, drawn from a weapon-type-specific pool |
| **Arity** | 2–3 positives, optionally 1 negative |
| **Legality** | a short list of stats that may never be the negative (*"Positive values only"* — Cold, Electricity, Heat, Toxin Damage, Punch Through) |
| **Pricing** | a configuration → multiplier table (more parts ⇒ weaker parts; a downside buys magnitude back), times a per-weapon **Disposition** re-derived from usage telemetry every three months |
| **Coherence** | the name is a deterministic function of the roll, ordered by magnitude |

**INFERENCE.** This is the only system found where all four — parts, legality, price and name — are
derived from one roll with no authored per-combination content anywhere.

### 8.2 Caves of Qud mutations — point-buy with negative-cost parts and a pairwise exclusion list

**FACT ([Caves of Qud Wiki, Mutations](https://wiki.cavesofqud.com/wiki/Mutations), reached via a reader
proxy — **second-tier**, though this wiki is maintained by the developers' community from game data).**

Three categories: **Physical mutations**, **Mental mutations**, **Defects** (physical and mental
variants). Physical and mental mutations cost roughly 1–5 points. Defects have **negative cost**:

> *"Defects are mutations with various detrimental effects. Rather than costing points at character
> creation, they will grant the player additional points to spend when taken."*

> *"Typically, the player is only allowed to take one defect at character creation."*
> *"However, there is no limit to how many defects the player can obtain through other sources.
> Additionally, the character creation defect limit can be disabled via the Options menu."*

**Scale:** more than 70 selectable mutations in four categories — **Morphotypes** (Chimera, Esper,
Unstable Genome), **Physical** (32), **Mental** (27), **Defects** (12 physical, 8 mental) — plus a
separate pool of roughly 50 *innate* mutations used to give creatures natural abilities, not selectable
at creation. The creation budget is **12 points**; costs run 1–5; defects are the only way to exceed 12.

**Legality is enforced at four independent levels — this is the fullest example in the survey:**

| Level | Rule | Examples |
|---|---|---|
| **Point budget** | 12 points, defects have negative cost | — |
| **Anatomical slots** | mutations occupy body slots and compete for them | Beak→Face · Horns→Head · Carapace/Quills→Body · Wings→Back · Stingers→Tail · Burrowing Claws→Left Hand • Right Hand · Flaming/Freezing Ray→Hands • Feet • Face |
| **Pairwise exclusion list** | hand-authored per mutation | Carapace ⟷ Quills · Flaming Ray ⟷ Freezing Ray · the three venom Stingers mutually exclude · Horns ⟷ Psionic Migraines · Photosynthetic Skin ⟷ Albino **and** ⟷ Carnivorous · Dystechnia ⟷ Psychometry |
| **Morphotype category gate** | excludes whole categories at once | Chimera excludes `Esper · Mental Mutations · Mental Defects`; Esper excludes `Chimera · Physical Mutations · Physical Defects` |

Plus a **soft count cap**: one defect at creation, disableable in Options.

**INFERENCE — and this is the important reading.** The pairwise list stays short because the slot system
and the morphotype gate absorb most of the work, and because **the pairs that remain are semantic
contradictions, not balance contradictions**: fire vs frost, you cannot photosynthesise without pigment,
you cannot wear a helmet through your horns. **A pair banned for flavour is stable forever; a pair banned
for power has to be re-examined every balance pass.**

**One vocabulary, two consumers.** Randomly generated legendary creatures draw *"an extra 0-2 physical
mutations and 0-2 mental mutations, each at level 1-4"* from the same mutation vocabulary the player
uses, under the same exclusion rules
([Legendary creature](https://wiki.cavesofqud.com/wiki/Legendary_creature)).

**Item mods use a fourth pattern — a weighted rarity with a soft tier gate**
([Item mods](https://wiki.cavesofqud.com/wiki/Item_mods)): *"Items are limited to a maximum of **3
mods** in almost all cases"* (the `gigantic` mod is the documented exception and consumes no slot);
applicability is by **category**; individual mods carry **predicates against the target's existing
properties** (`fitted with filters` *"has an additional unique restriction that prevents it from being
applied to any item that already filters gas"*); and rarity weights are literal — Common 2000, Uncommon
800, Rare 210, Rare2 50, Rare3 3 — divided down below the mod's Native Tier by
`Weight ÷ ((ModNativeTier − ItemTier) × 5)`. **A low-tier item *can* roll a high-tier mod, but almost
never does. A soft gate, not a wall.**

### 8.3 ⭐ Caves of Qud's sultan histories — the best-documented procedural text generator shipped

**And the most surprising finding in this document: it has almost no legality rules on event order at
all.**

Primary source: Grinblat & Bucklew, *Subverting Historical Cause & Effect: Generation of Mythic
Biographies in Caves of Qud*, FDG'17
([PDF, Freehold Games](https://www.freeholdgames.com/papers/Generation_of_Mythic_Biographies_in_CavesofQud.pdf)
— **first-tier, the developers' own paper**; talk at
[GDC Vault](https://gdcvault.com/play/1024990/Procedurally-Generating-History-in-Caves)).

**The parts:**

| Part | Detail |
|---|---|
| **Historical entities** | sultans, places, items — data structures with properties |
| **Sultan initialisation** | name, pronouns, birth year, birth region, location in region, and a **domain** |
| **Domains** | *"archetypal unit[s] of culture"* — concrete (glass, jewels, ice, stars) and abstract (might, scholarship, chance). **Ten** at time of writing; a sultan starts with one and accrues 0–2 more |
| **Life events** | **nineteen** at time of writing (*sieges a city*, *challenge sultan*, …), drawn from a pool |
| **Gospels** | one text snippet per event — the atomic unit of history. ~13 events per sultan plus a death event; five sultans per world |
| **The engine** | a **Tracery-like replacement grammar** in nested JSON, plus a bespoke query language |

**FACT — the legality model is startlingly thin.** *"there's no logic behind the choice of events.
Historical cause and effect aren't intrinsic. **Events themselves are chosen at random.**"* What
constrains the output is a **property-availability check inside each event**:

> For the *challenge sultan* event, the candidate properties are allied factions, profession, and
> domains. The event first randomly determines which property to use, **rerolling if it selects a
> property for which the sultan doesn't have a value**… If no valid value is found, some events
> **create new values** by altering one of the candidate properties, thereby inventing a cause.

with a guaranteed-non-empty backstop — domains, *"which is guaranteed to have at least one value since a
domain is set for the sultan in the initialization step."*

**INFERENCE.** This is a very cheap legality pattern and it generalises directly: instead of forbidding
combinations, **each part declares which state it can speak about, rerolls off empty state, and one
always-populated field acts as a backstop, so no draw can ever fail to render.**

**Three named coherence devices, all FACT from the paper:**

1. **Ex-post rationalisation.** The event is chosen first and the *cause* is manufactured afterwards from
   whatever state exists. Pattern: `Acting against #injustice#, #sultanName# led an army to the gates of
   #location#.` — `#injustice#` fills from the sultan's allied animal factions and becomes *"the
   persecution of frogs"*. Where no cause exists, the event **writes one into the sultan's state and then
   cites it**: *"the effect causes the cause."*
2. **Shared-state glue.** *"The sultan's shared state acts as a glue that holds the disjointed events
   together… **Because of the limited number of sultan properties shared across many events**, emergent
   micro-narratives like this one are quite common."* **A small property set is the feature, not the
   limitation.**
3. **The domain as a through-line.** *"almost all gospel patterns include symbols that represent
   domains"* — so an ice-domain sultan is found as a babe holding icicles, wields a frosty hammer, and
   devastates two sites with icy winds. **Domains are what make a random walk read as a personality.**

The query syntax is worth recording verbatim, because it is the whole engine in one line:
`< domains.sultan$domains[random].practices.!random >` — resolve which domain, index into that domain's
JSON, pick the `practices` rule, `!random` selects a fragment from its array. Queries nest arbitrarily.

**And the history feeds the item generator.** Places visited become instantiated historic sites; named
items become real items with derived properties — *"Frostycus Catsfriend"* instantiates as a hammer that
deals frost damage and grants bonus reputation with cats. The `engraved` item mod's own description is
*"This item is engraved with a scene from the life of the ancient sultan (sultan)."*

**Correction to a common assumption:** Qud's **books are not procedurally generated**. The wiki lists 34
authored titles. The procedural text surface is gospels, historic-site descriptions and item
descriptions.

### 8.4 The Nemesis System — the generator has invariants, not bans

Covered for naming in §12.4. The generation model, from the patent
([US10926179B2](https://patents.google.com/patent/US10926179B2/en) — **first-tier**):

**The NPC is a parameter block** (patent Figure 4, "400"): name and title · tribe · appearance (body
type, body parts, hairstyles, behaviours, **voices**, animations) · power level · rank · relationship ·
fighting style · traits · location.

**Traits are typed into eight families, each with a distinct engine consumer:**

| Family | Patent text |
|---|---|
| Invulnerability strengths | *"determine a degree of the character's resistance to various sources of damage"*; *"may have a single value for all instances, or may be scaled by a numeric weighting factor"* |
| Hate strengths | a provocation trigger — the matching event enrages the character, which *"boosts their abilities"* |
| Attack strengths | *"determine combat moves a nemesis may perform when in combat"* |
| Weapon strengths | *"determine damage that the non-player inflicts"* while wielding a weapon |
| Misc strengths | e.g. *combat master*, immunity to sword attacks |
| Damage weaknesses | can kill *"more easily or even instantly"* |
| Fear weaknesses | *"cause the nemesis flee 'in terror'"* |
| Misc weaknesses | — |

**A second published legality rule: a global ceiling.** *"the game engine may set a maximum level for
power parameters that no character may exceed."*

**And slot caps in the shipped game** — **SECOND-TIER, unverified** (the source wiki is behind HTTP 402
here): a captain may hold no more than 2 Epic traits, and only one Weapon trait and one Gang trait each.
Treat as plausible but unconfirmed.

**Appearance is an event log, not a roll.** *"The game engine may associate every possible apparent
killing or defeat with one of the battle scars. For example, the game engine may apply a burn scar if
the NPC has been killed by fire, or a metal plate if the NPC has been killed by a headshot with an
arrow."* Past a death limit a "bag covering face" appearance is applied; promotion re-gears the
character *"with new weapons and armor befitting his personality."*

**Tribe propagates to architecture:** *"a 'Beastmaster' overlord's fort may be adorned with large
game-hunting trophies over many surfaces. A 'Regal' overlord's fort may have golden accents on the
walls."*

**A design-time legality rule of a different kind.** **FACT (second-tier)** — an earlier build had
per-orc knobs for morale, training and factional tension that were **cut**, and traits were selected on
the criterion that they *"would make players play against each orc differently"*
([Kotaku](https://kotaku.com/shadow-of-mordors-nemesis-system-couldve-been-way-more-1681120649),
[PC Gamer](https://www.pcgamer.com/shadow-of-mordors-nemesis-system-inspired-by-multiplayer-sports/)).
**A trait earns its place in the vocabulary only if it changes player behaviour** — which is the same
criterion Rosewater applies to keywords (§7.3) arrived at independently.

**The legality rule is a floor invariant, not an exclusion**, and it is the single most interesting
sentence in the patent for a generator designer:

> *"the game engine may cause any nemesis with **combat master status, which provides immunity to sword
> attacks, to always have at least one weakness**, be it a fear or a damage weakness, **so that the
> nemesis is not invincible**."*

**FACT.** The constraint is expressed as *"if you drew this, you must also draw at least one of that"* —
a **required counterweight**, not a forbidden pair. **INFERENCE:** a floor invariant is cheaper than a
ban list because it scales with the strength axis rather than with the number of parts, and it cannot
produce an unsatisfiable roll the way a dense exclusion graph can.

**The power ladder moves the whole distribution:** *"The game engine may cause a nemesis to lose
weaknesses while gaining strengths, as the nemesis increases in power."* Rank is a hierarchy —
*"an overlord non-player character … at the top … a warchief … the game may include a set number of
warchiefs (for example, five) … Captains … may be the most common rank … Soldiers … reside at a third
(lower) level."*

**Coherence is bought by deriving the mechanics from the title** (§12.4) and by composing dialogue from
phrase identifiers rather than generating text: *"the faction manager may associate a dialog identifier
with the event record, based on the NPC action."*

### 8.5 ⭐ Cassette Beasts — legality by schema, and a move name that *is* the spec

**The cleanest example of legality-by-schema in the survey: there is no compatibility table at all.**

**The fused body.** Every monster ships a **fusion config** — a Godot 2D scene declaring named part
nodes plus anchor coordinates. The required part list is fixed
([official modding guide](https://wiki.cassettebeasts.com/wiki/Modding:Monster_Making_Guide_Part_2) —
**first-tier, the developers' own documentation**):

`Body` · `Head` · `HelmetFront` · `HelmetBack` · `Arm_Back` · `Arm_Front` · `Tail` ·
`BackLeg_Front` · `BackLeg_Back` · `FrontLeg_Front` · `FrontLeg_Back`, plus three coordinate nodes —
`attack` (emission point), `hit` (impact point), `eye`.

**The rules, all FACT and all schema-shaped:**

- *"the config for the **primary** monster is used as a base, but **swaps 'parts'** with the secondary
  monster's fusion config scene."*
- **Body never swaps** — always the primary's. Body and Head *"should never be hidden"*, nor should the
  Helmet layers.
- *"the engine will always use the **'head' from one and the 'helmet' from the other**."* A strict
  one-from-each rule on the head region.
- Arms, Tail and both leg pairs *may* be hidden (Bansheep has no legs; Traffikrab has no arms) — and
  crucially *"The sprites defined for it, however, **can still be used if this monster is the secondary
  monster** in a fusion."* Hiding is a display flag, not a deletion.
- **A geometric contract:** the Head *"is always drawn in roughly the same size so that the 'helmet'
  fits on it without clipping — and mostly round"*, with a documented tolerance.
- **An animation contract:** *"all the animations are 6 frames long (except for 'hurt', which is 3)
  **which keeps all the parts animating in sync with each other**."*
- **A colour contract:** all fusion parts are authored in exactly **three colours**, and the runtime
  substitutes a palette drawn from both parents. *"other palettes used on the fusion parts will not
  change colour"* — off-contract colours silently fail to harmonise.

**INFERENCE.** Legality here is not *"may A combine with B"*. It is *"every monster must supply every
slot, in these dimensions, in these three colours, at this frame count"* — and any pair then composes
correctly **by construction**. The contract is on the *part author*, not on the *combination*.

**The fused move — and this is the naming finding.** A Fusion Power move is assembled from a **type**
(always one of the two fused monsters'), a **prefix**, a **suffix**, and a **buff or debuff**
([Fusion Power](https://wiki.cassettebeasts.com/wiki/Fusion_Power)):

| Slot | Vocabulary | What it carries |
|---|---|---|
| **Prefix** | ~6–8 authored words *per type* — Fire: Infernal, Blazing, Burning, Flaming, Fiery, Magma, Volcanic. Astral: Cosmic, Planetary, Celestial, Stellar, Ethereal, Orbital, Stygian. Glitter: Glittery, Sparkly, Rainbow, Fairy, Crafty, Homemade, Unicorn | **the element** — *"the exact prefix does not have any mechanical effect on the move (other than indicating the type)"* |
| **Suffix** | 13, and each **is** the mechanical spec — Arrow (Ranged/Individual), Axe (Melee/Individual), Beam (Ranged/Individual), Bladewheel (Melee/Team), Blast (Melee/Individual), Bomb (Ranged/Team), Fist (Melee/Team), Meteor (Ranged/Team), Shards (Ranged/Team), Smash (Melee/Team), Spikes (Ranged/Individual), Sword (Melee/Individual) | **physicality and target count** |
| **Rider** | 17 buffs, 14 debuffs, each with a fixed duration of 1 or 3 turns | the status |

**And the power budget is published:**

| Target | Kind | Power |
|---|---|---|
| Individual | Buff | 200 |
| Individual | Debuff | 200 |
| Team | Buff | 150 |
| Team | Debuff | 112 |

**Team-hitting costs power. That is the entire balance mechanism.** The mapping is deterministic per
*ordered* pair — *"The order that the two monsters are fused in does matter in determining the Fusion
Power move."* One special case is the only place a part inherits rather than rolls: *"If the chosen
buff… is Contact Dmg, **the type is based on the elemental type of the Fusion Power move**."*

**⭐ The name is a lossless encoding of the mechanics.** Prefix ⇒ element, suffix ⇒ physicality and
target count. A player reading *"Volcanic Bladewheel"* knows it is Fire, melee, and hits the team,
**before reading a tooltip**. The flavour half varies freely for novelty; the structural half is exact.
Compare Warframe (§12.1), which encodes *rank order* rather than *kind*.

**The warning case is the same game.** 120 authored monsters and *"over 14,000 fusions"* (120 × 119 =
14,280 ordered pairs, computed), and reviewers *"liked the designs"* of the authored 120 *"but did not
feel as positively about the fusions, the majority of which were procedurally generated."*
Sourced and computed in
[`../genre-mechanics/06-summoner-minion-fusion-rpg.md`](../genre-mechanics/06-summoner-minion-fusion-rpg.md)
§2.6 — **not re-derived here.** That file's §3 conclusions apply directly: *"the table almost always
picks the family and the rule almost always picks the individual"*, and *"the precedence order is always:
authored beats computed."*

**INFERENCE.** Cassette Beasts is the survey's cleanest demonstration that **schema-level legality
guarantees a valid result and guarantees nothing about whether the result is liked.** The move grammar
reads as designed because its name is its spec; the creature bodies read as generated because a slot
contract cannot express silhouette.

### 8.6 Monster Hunter Stories 2 — a 3×3 grid where geometry is the bonus rule

**FACT** ([Capcom Official Web Manual §9-3, "The Rite of Channeling"](https://game.capcom.com/manual/MHST2/en/switch/page/9/3)
— **first-tier, the publisher's own manual**).

**The parts.** A gene is a triple: **skill type** (active or passive), **attack type** = *pattern*, and
**elemental affiliation** = *colour* — *"They all have their own skill type, attack type, and elemental
affiliation, which all factor into a Monstie's overall powers."* Elements: normal, fire, water, electric,
ice, dragon. Patterns: blank, Power, Technical, Speed. Genes also carry a **size** (S / M / …), separate
from effect. The board is **nine slots in a 3×3 grid**. **Free Bingo Genes** are rainbow wildcards.

**Six legality rules:**

| # | Rule |
|---|---|
| 1 | **Locked slots** — *"You cannot select locked slots to inherit a gene."* Unlocked by level or by consuming specific items |
| 2 | **Type-mismatch overwrite** — *"If you select a slot where a gene is already present, but the gene's type differs from the new one's type, the new gene will **overwrite** the previous one and it will disappear."* |
| 3 | **Upgrade requires an exact double match** — same effect **and** same size. Capcom's own example: Iron Wall (S) + Iron Wall (S) upgrades; Iron Wall (S) + Iron Wall (M) does not |
| 4 | **A hard upgrade cap** — *"You can upgrade a gene up to **two times**."* |
| 5 | **One wildcard per creature** — *"Each Monstie can only possess one Free Bingo Gene."* |
| 6 | **No duplicate skills on a board** — *"Monsties can only have one of each skill occupying their board"* (**second-tier**, Polygon) |

**INFERENCE on rule 6.** This is the anti-stacking rule, and its shape is instructive: a duplicate is not
*rejected*, it is **forced into the upgrade path**. Nine copies of one skill is impossible; three tiers
of one skill is the intended expression of the same desire.

**And the real brake is an economy, not a rule.** *"once a Monstie passes a gene on to another Monstie,
the channeling Monstie will disappear."* Perfecting one nine-slot board costs a supply of whole
creatures.

**Coherence is legible geometry.** *"When you align three genes in a row—either vertically,
horizontally, or diagonally—you will get a Bingo Bonus… when each of the three genes has the same
**colour (element)** or **pattern (attack type)**."* Two orthogonal match axes mean one line can score
twice, and the trade-off is visible on the board: an all-Power/Fire board scores heavily and produces a
monster that *"won't be very well rounded."* **FACT — the UI shows a "Post-Channeling" table previewing
the resulting bonuses before you commit.** The generator's output is previewed, not discovered.

### 8.7 Dwarf Fortress — one parameter envelope, and everything composes for free

**FACT** ([DF Wiki: Forgotten beast](https://dwarffortresswiki.org/index.php/Forgotten_beast),
[Demon](https://dwarffortresswiki.org/index.php/Demon),
[Syndrome](https://dwarffortresswiki.org/index.php/Syndrome) — **second-tier prose, but much of it is
transcribed from the game's own generated raws**).

**The parts of a generated megabeast:** a creature profile / body shape (mostly animal forms *"with extra
features (e.g. extra eyes, feathers) or removed body parts (e.g. skinless cobras)"*) · a material (flesh
and blood, or grime/salt/steam/smoke/snow/ash, or coral, or rock/mineral/glass/gem, or metal up to steel)
· movement (walking or flying) · **spheres** (forgotten beasts always get CAVERNS plus one or two more) ·
a special attack (noxious secretions, poisonous bite, spitting glob, fire breath, fireball, toxic blood/
vapor/gas, webs, deadly dust) · a procedural syndrome attached to whichever material carries the attack.

An actual generated raw, extracted from `world.dat`, showing the assembly:

```
[CREATURE:FORGOTTEN_BEAST_53]
 [GENERATED] [FEATURE_BEAST]
 [SPHERE:CAVERNS] [SPHERE:DISEASE]
 [BODY:RCP_BASIC_BODY_STANCE_WITH_HEAD_FLAG:RCP_SHELL:RCP_TWO_WINGS]
 [FLIER]
 [TISSUE:UNIFORM_TIS] [TISSUE_MATERIAL:GRIME] [TISSUE_MAT_STATE:SOLID]
 [NOT_LIVING] [NOT_BUTCHERABLE] [ODOR_STRING:filth]
```

The body is a **concatenation of body-plan modules** (`RCP_*`), the material is a single tissue token,
and the *consequences* of the material (`NOT_LIVING`, `NOT_BUTCHERABLE`) are emitted alongside it.

**⭐ The syndrome vocabulary, and the structural fact that makes it work.** A syndrome is
`[SYNDROME]` + transmission routes (`SYN_CONTACT`, `SYN_INHALED`, `SYN_INGESTED`, `SYN_INJECTED`) +
susceptibility filters (`SYN_AFFECTED_CLASS`, `SYN_IMMUNE_CLASS`, `SYN_AFFECTED_CREATURE`,
`SYN_IMMUNE_CREATURE`) + `SYN_CONCENTRATION_ADDED` + one or more creature effects. The `CE_*` list runs
to roughly 50 tokens — `CE_NECROSIS`, `CE_BLISTERS`, `CE_PARALYSIS`, `CE_BLEEDING`, `CE_SWELLING`,
`CE_NUMBNESS`, `CE_PAIN`, `CE_NAUSEA`, `CE_FEVER`, `CE_DIZZINESS`, `CE_UNCONSCIOUSNESS`,
`CE_COUGH_BLOOD`, `CE_IMPAIR_FUNCTION`, `CE_PHYS_ATT_CHANGE`, `CE_SPEED_CHANGE`,
`CE_BODY_TRANSFORMATION`, `CE_ADD_TAG`/`CE_REMOVE_TAG`, `CE_CAN_DO_INTERACTION`, `CE_DISPLAY_NAME`,
`CE_FEEL_EMOTION`, `CE_CHANGE_PERSONALITY`, and their healing counterparts.

**Every effect takes the same parameter envelope:**

```
[CE_NECROSIS:SEV:100:PROB:100:LOCALIZED:VASCULAR_ONLY:RESISTABLE:START:50:PEAK:1000:END:2000]
```

`SEV` severity · `PROB` percent chance of manifesting · `BP:BY_CATEGORY:X` / `BY_TYPE:X` / `BY_TOKEN:X`
targeting body parts **and tissue layers** · flags `LOCALIZED`, `VASCULAR_ONLY`, `RESISTABLE`,
`SIZE_DILUTES`, `ABRUPT_START` · a `START`/`PEAK`/`END` time envelope.

**INFERENCE, and it is the single most transferable structural idea in this section.** Because every
effect shares one envelope, the syndrome generator only ever rolls a **5-tuple** — *(effect id, severity,
probability, target selector, timing)* — and **any effect composes with any other for free**. The space
is enormous and nothing malformed comes out, because malformedness is not representable.

**The legality rules, from the Demon generation documentation — the most explicit statement in DF's
docs:**

> *"the game begins by generating a table of demon subtypes (flying spirit, unique, humanoid beast,
> beast, and 'whatever') and their difficulties. **The subtype determines which random creature profiles
> are available**, with humanoid demons requiring a **humanoidable shape** … and **flying spirits being
> made of an intangible material** like snow or flame."*

> *"A demon receives spheres, one chosen from the list of evil spheres … and one to two additional
> **non-good spheres that don't conflict**."*

> *"**Unless their creature profile already possesses a special attack**, demons receive a strong attack
> tweak such as a poisonous sting, toxic breath, or fire breath."*

Three distinct devices: a **subtype tag gates the shape and material pools**; sphere selection has an
**alignment filter plus a pairwise conflict check**; and the attack tweak is applied **conditionally on
absence**, so nothing is ever double-armed. `[DIFFICULTY]` is a single scalar that drives size.

**Material propagates consequences automatically** rather than being separately validated: a grime-bodied
beast gets `[NOT_LIVING][NOT_BUTCHERABLE]`, and inorganic beasts have no blood, so injection-route
syndromes cannot apply. Some attacks are *implied* by material or shape rather than rolled — *"certain
kinds of beasts have inherent abilities, like fire balls for a beast composed of fire, or webs for a
spider-based beast."*

**And where the immunity filter is *not* applied, the wiki documents the incoherence honestly:**
fire-breathing organic beasts *"are not immune to fire and may even burn themselves to death"*, and a
fleshy deadly-dust beast *"will harm itself with its own deadly dust."*

**Coherence is a fixed sentence frame filled from the parameter block:**

> *"Nolthag was a forgotten beast. A gigantic three-eyed six-legged dimetrodon. It is slavering. Its
> pearl scales are blocky and set far apart. **Beware its noxious secretions!**"*
> *"Egngun was a forgotten beast. A gigantic humanoid composed of coral. It has a long, spiral horn and
> it squirms and fidgets. **Beware its deadly dust!**"*

The frame is `<name> was a <kind>. A <size> <shape> [composed of <material>]. It has <extra features>
and it <mannerism>. Its <colour> <tissue> <appearance modifier>. Beware its <special attack>!` — **the
final clause is the player-facing threat summary, generated straight from the attack slot, so the one
mechanically decisive fact is always the last thing you read.**

**The naming scheme is gated by the same property that gated generation:** *"a **base noun** (demon,
devil, etc) and an **adjective derived from their features**, like their colour, material, or species
profile. **Corporeal** demons will be named either demon, devil, fiend, brute, or monster. **Intangible**
demons instead pick from spirit, ghost, banshee, haunt, phantom, specter, or wraith."* A snow-bodied
creature can be a wraith but never a brute. Sprites follow: *"randomly generated creatures with random
colors that **resemble the generated appearance they've been given**."*

**And subtraction is a flavour operator:** *"A tweak common to evil creatures has some feature (such as
its eyes, nose or skin) be **removed**."*

**One candid developer note**, via the wiki's citation, on the generator shipping with a blunt safety
default: the generated interactions carry `[SYN_CONCENTRATION_ADDED:1000:0]` because — *"was a precaution
after I had one bug with effects not fully manifesting due to low levels… I decided to give everybody a
full dose of the juice until I could get a closer look at it."*

### 8.8 Wildermyth — abilities hang off body parts, and the exclusion table reads as biology

**FACT** ([Wildermyth official wiki, Theme conflicts guide](https://wildermyth.com/wiki/index.php?title=Theme_conflicts_guide)
— **second-tier prose, but written at the data-schema level and quoting the actual JSON fields**).

**The parts.** **35 transformation themes** (Beartouched, Botanical, Celestial, Crowtouched, Crystalline,
Drauven Wings, Elmsoul, Flamesoul, Gorgonoid, Mothly, Petrified, Scorpioid, Shadow, Skeletal, Sylvan,
Stormtouched, Wolftouched, …). A theme decomposes into **theme pieces**, which sit in **theme slots** —
and **the abilities attach to the pieces, not to the theme.** Crowtouched breaks into Crow Head (grants
*Peck*), Crow Wings (+1 Speed, +10 Dodge), Crow Arm L/R (*Crow Scratch*; both arms enable *Scratch and
Claw*), Crow Leg L/R, Crow Tail. **Which abilities a character ends up with depends on which pieces the
events managed to place.**

Slots: `Head` (hair stars, head, tattoo, latent) · `Torso` · `Arms` (left, right) · `Legs` (left, right)
· `Skin` · `Wings` · `Tail`. Some pieces are deliberately slotless.

**Four legality mechanisms, all named in the data:**

1. **`forbidCombineWith` — a hand-authored exclusion list, evaluated symmetrically.** *"A conflict
   between two themes occurs when the themes are defined with a `forbidCombineWith:` in **either** of the
   theme .json files."*
   ```
   gem:  "forbidCombineWith": ["bear", "wolf", "tree", "fire", "skeleton"]
   tree: "forbidCombineWith": ["bear", "wolf", "crow", "fire", "skeleton"]
   ```
   Only `gem` names `tree`, yet the block holds both ways. **INFERENCE:** the symmetric-union read is
   what makes a hand-authored table maintainable — an author declares a conflict once, from whichever
   side they happened to be thinking about, and never has to keep two lists in sync.
2. **Slot occupancy, which creates *implicit* conflicts.** *"a hero with the crystalline theme will not
   be eligible for the sylvan theme **even though the theme files do not define a conflict**. This is
   because both events target `Eligible for Theme Piece:` with the same theme slot (head)."*
3. **`Forbidden Aspects` — a tag blacklist on the event, and it is order-dependent.** The *Worlds Apart*
   event declares `Forbidden Aspects: theme_bear, theme_crow, theme_deepist, theme_gem, theme_fire,
   theme_foothill, theme_shadow, theme_star` — but *"Receiving the spell touched theme **before**
   receiving any of the themes above imposes no conflict."* **A one-way gate.**
4. **`Eligible for Theme:` — a composite precondition** checking three things at once: *"No member of the
   company may have the same theme. The hero must not have conflicting themes. The hero must have an
   empty slot for a theme piece."* **Note the first clause: uniqueness is enforced across the party, not
   the character**, so a company's transformations are guaranteed to look distinct.

**Belt and braces:** the exclusion *"also affects the `ApplyTheme:` outcome, which will not take effect
when there is a forbidCombineWith conflict"* — even a mod bypassing event targeting cannot apply the
theme.

**The edge cases are the interesting part**, because they show what a slot system does when it cannot
fully satisfy a draw: Flamesoul targets the left-arm slot but grants both an arm and a head piece, and a
hero with a filled head slot **gets only the arm** — partial application rather than refusal. Stormtouched
offers a *choice* of head, left arm or left leg, letting the player decide how much future conflict to
incur. Mortificial Enhancements requires prosthetics, *"however prosthetic limbs are replaced by theme
limbs for a hero who receives any theme with limbs"* — an earlier transformation silently destroys the
precondition for a later one. And some events clear a slot first: *"A hero with a wolf or crow tail for
example, will have the tail removed before receiving another tail."*

**Coherence — text is templated and the magnitude is one expression.** Story text carries substitution
tokens: *"Wind in his/her wings… **s/he** met a crow woman"*; ability text uses the same: *"`<self>` pecks
a nearby foe with their crow beak, blinding it on stunt."* Damage is a published formula reading
character state, with the upgrade tier as a **variable** rather than a duplicated ability entry:

```
((1d3+2)+(((self.PHYSICAL_DAMAGE_BONUS+self.POTENCY)*0.5)*(1+self.theme_crow_upgrade)))+self.theme_crow_upgrade
```

**INFERENCE.** Wildermyth's coherence comes almost entirely from the **anatomical framing**. Because every
ability hangs off a body part and every body part is a scarce slot, *"which powers does this character
have"* is answered by *"what has this character's body become"* — and the exclusion table then reads as
biology rather than as balance.

### 8.9 Borderlands — the legality schema is the shipped data

Covered for naming in §12.3; the legality half is separate and equally explicit.

**FACT** — the weapon part slots are semantically named and fixed; non-weapon items use **abstract
Greek-letter slots** because the meaning varies by item class, *"For example, Alpha might represent a
shield's capacity part, while Beta represents its recharge delay part"*
([deepwiki on gibbed's schema](https://deepwiki.com/gibbed/Gibbed.Borderlands2/5.2-balance-definitions)).
**INFERENCE:** two naming conventions for one mechanism, chosen by whether the slot has a stable physical
meaning across the type family. Guns always have a barrel; shields do not always have a comparable
"second thing".

**FACT — the `BalanceDefinition` *is* the legality schema:**

> *"Balance Definitions are metadata objects that determine **valid part combinations** for weapons and
> items… They define which manufacturers, weapon/item types, and individual parts (body, grip, barrel,
> etc.) **can legally appear together**."*

Three properties matter:

1. **Manufacturer restriction is a per-balance whitelist**, not special-case code — `Manufacturers:
   List<string>` on the balance definition. **INFERENCE:** "Jakobs guns are never elemental" is expressed
   as a restricted `ElementalParts` list on the Jakobs balances, by the same mechanism that restricts
   barrels.
2. **Inheritance.** Balances chain via `Base` and are flattened leaf-to-root, so a legendary variant
   derives from a generic definition and overrides selectively.
3. **Three merge modes**, and this is the expressive part:

| `PartReplacementMode` | Behaviour on the slot's part list |
|---|---|
| `Additive` | appends source parts to the inherited list |
| `Selective` | clears, then replaces with the source parts |
| `Complete` | clears the destination first — **a null source therefore yields an empty list** |

`Complete` with a null source is how a derived balance says *"this slot has no legal parts at all"* — the
mechanism for "this weapon family can never be elemental."

**And illegal combinations are a hard error, not a clamp:** after merging, *"Ensure final `WeaponType`
matches requested type. Throw `ResourceNotFoundException` if mismatch occurs."*

**INFERENCE.** The legality schema and the authoring/inspection tool are the same artefact — which is why
the community could reverse-engineer complete part-compatibility tables from it. **The game ships its own
legality tables as data.**

### 8.10 Ultima Ratio Regum — three named coherence techniques, and one of them is the cheapest in the survey

Mark Johnson's
[Generation Next, Part 3: How To Create Cultures](https://www.rockpapershotgun.com/how-to-procedurally-generate-culture)
is the most directly reusable prose found on keeping generated things coherent. **All three are FACT, in
his words:**

1. **Fractal rather than flat generation.** Not a hundred independently rolled variables but:
   *"civilizations vary in the variables they contain, and then vary within those variables… some
   variables are connected to others. Maybe one variable has five options, and if one option is chosen,
   then that 'unlocks' another set of five variables."* A racist culture rolls once for whether it blocks
   entry, then for *how*, then for how that is carried out. **Depth is conditional on breadth.** He notes
   URR has *"name generator variations that can only be unlocked with specific and extremely rare
   alignments of political ideologies"* — most players never see them, which is the point.

2. **⭐ Subtractive constraint — the cheapest coherence mechanism found anywhere.**
   > *"a system that picks one of those at random to start off with — mountains, for instance — and then
   > makes a randomized stylistic selection. Based on that selection, some possibilities from the other
   > categories, **that would clash (based on the designer's decisions) with these mountains, are
   > removed**. It then chooses another element, maybe horses, and picks a style, but that style [is
   > drawn from the reduced pool]."*

   **INFERENCE.** No pairwise validation, no rerolls, no exclusion lookup at combine time. **Each choice
   shrinks the domain of the later ones, so the whole assembly is coherent by construction and the cost
   is a single pass.** Every other system in this document pays either an exclusion-table lookup or a
   rejection-sample; this one pays neither.

3. **Archetypes and templates.** *"Have a particular orientation of elements that will always be
   interesting and compelling to the player? Save those elements, have the game sometimes select that
   specific orientation (instead of picking the variables that make up that orientation at random), and
   then vary the orientation enough that it's [not identical]."* He reached for this specifically on
   clothing, where *"varying these in a traditional procedural manner might produce some odd results."*

**And a scope rule worth recording:** *"I've made a point of only including cultural details that are
either already physically or visibly present in the game, or will be in near-future releases"* —
**generate nothing the player cannot encounter.**

### 8.11 Noita's per-seed alchemy — generating the *key*, not the content

Introduced in §2.6, and worth naming separately because it is the cheapest form of procedural generation
in this survey: **the effect is fully authored, and only the recipe that unlocks it is rolled per seed.**

**FACT** ([Lively Concoction](https://noita.wiki.gg/wiki/Lively_Concoction),
[Random Materials](https://noita.wiki.gg/wiki/Random_Materials)). Ingredients are drawn from **two typed
lists** — a liquid list of roughly 30 (Acid, Blood, Cement, Lava, Mud, Oil, Poison, Swamp, Teleportatium,
Toxic Sludge, Urine, Water, Whiskey, Acceleratium, Berserkium, Levitatium, Invisiblium, Chaotic
Polymorphine…) and a powder list.

**The legality rule is distributional, not prohibitive:** *"Roughly **half** of the recipes will require
only liquids, the other half will require **one powder**."* So the recipe shape is always {3 liquids} or
{2 liquids + 1 powder}, never 3 powders. **Position carries meaning:** *"the **second** material chosen
is the *reactant* that will be converted into the new liquid, while the other two remain unchanged,
essentially functioning as **catalysts**."* And the roll is stable — *"Entering New Game Plus does not
change the random materials again."*

**INFERENCE.** Coherence here is achieved by *not generating the result*. Only the recipe is randomised;
the product is a fully authored material with authored reactions and tags. **The generator produces a
puzzle, not an object.** Same shape as NetHack's appearance shuffle (§5.2) applied to crafting instead of
identification. Cost to build: one seeded draw. Cost to maintain: zero. Risk of incoherence: zero,
because nothing generated is ever shown to the player as content.

### 8.12 ⭐ DCSS randarts — a budgeted draw from a closed property vocabulary, in open source

**The single most directly comparable published implementation found**, and it is readable line by line
([`artefact.cc`, `_get_randart_properties()`](https://github.com/crawl/crawl/blob/master/crawl-ref/source/artefact.cc)
— **first-tier**):

```c
// Each point of quality lets us add or enhance a good property.
const int max_quality = 7;
const int quality = 1 + binomial(max_quality - 1, 21);
// We'll potentially add up to 2 bad properties...
int bad = 0;
if (fixed_bad < 2) bad = binomial(2 - fixed_bad, 21);
int good = max(quality + fixed_bad + bad - fixed_good, 0);
// We want to avoid generating more than 4-ish properties or things get spammy.
int max_properties = 4 + one_chance_in(20);
```

Every element of the pattern is here:

| Element | How it appears |
|---|---|
| **A quality budget** | `quality = 1 + binomial(6, 21)` — a bell-shaped draw, not uniform |
| **Downsides as negative-cost parts** | up to 2 bad properties, each *increasing* the good budget |
| **A soft arity cap with a stated reason** | *"We want to avoid generating more than 4-ish properties or things get spammy"* — and the `+ one_chance_in(20)` is a deliberate rare overflow |
| **Per-part legality gates** | `_artp_can_go_on_item()` and `_artp_can_randomly_generate()` |
| **Surplus budget goes to depth, not breadth** | leftover "good" is spent **enhancing existing properties rather than adding new ones** |

**FACT.** *"We want to avoid generating more than 4-ish properties or things get spammy"* is the design
constraint stated out loud, in a comment, by the people who ship it.

**INFERENCE — the honest formulation for the whole roguelike family.** Traditional roguelikes
procedurally assign *appearances* to a hand-authored effect table. Where they generate *effects* at all,
they do it by **budgeted sampling from a closed, hand-authored property vocabulary, with per-property
legality checks and a soft cap on how many properties one object may carry.** And note that randarts get
their *names* from the very same `make_name()` used for scroll labels — **the appearance layer and the
effect layer are built by entirely separate machinery even in the one case where both are procedural.**

### 8.13 Warframe's *other* generator — the unveiling challenge

Worth one paragraph because it is a composed **quest**, not a composed power, and the vocabulary split is
visible in the data.

**FACT.** A veiled Riven's challenge is a base objective with a randomised numeric range (*"Kill
[11-125] enemies"*, *"Find [8-12] Syndicate Medallions"*) plus **complications** drawn from an
enumerated list whose internal paths are published: `/Lotus/Types/Challenges/Complications/Undetected`,
`.../Sliding`, `.../AimGliding`, `.../ResetOnDamageTaken`, `.../ResetOnDowned`, `.../PetPresent`,
`.../SoloPlayer`, `.../ResetOnAlarmRaised`, `.../ResetOnProc`, `.../Invisible`, four `Equipped*DebuffKey`
variants, six `ResetOnGear*` variants, `.../ResetOnNewDay`.

**INFERENCE.** The naming convention encodes the taxonomy: `ResetOn*` complications are **fail
conditions**, the rest are **state conditions**. A generated quest is `objective + count + 0..n
complications`, and the two complication kinds compose freely because they attach to different points in
the objective's lifecycle.

---

## 9. Tabletop prior art — Ars Magica, the only fully published priced-composition model

Not a video game, and included for one reason: **it is a complete, public, shipped cost model for
composed spells**, and the video games with the same shape do not publish theirs.

**The grammar is verb × noun.** Five **Techniques** × ten **Forms** = 50 pairs, each with its own
published guideline ladder
([Ars Magica 5th ed. supplemental spell guidelines, Atlas Games](https://atlas-games.com/pdf_storage/ArM5Guidelines.pdf)
— **first-tier, the publisher's own PDF**).

**The cost model, stated exactly:**

- A spell starts from a **base guideline** — an authored line of prose with a level attached, e.g.
  *"Restore a body part that has been cut off, as long as the caster has the severed part."*
- Range, Duration and Target each add **magnitudes**.
- **`+1 magnitude = +5 levels`** (quoted from the Atlas PDF).
- Level ÷ 5 rounded up is the spell's Magnitude; below level 5 each magnitude adds only +1 level until
  the level reaches 5
  ([redcap.org, ArM5 Ch. 9](https://www.redcap.org/page/Ars_Magica_5E_Standard_Edition,_Chapter_Nine:_Spells)
  — **second-tier**).

The three axis ladders in full
([The Iron-Bound Tome](https://ironboundtome.wordpress.com/2014/07/03/list-of-ars-magica-ranges-durations-and-targets/)
— **second-tier blog reproducing the core-book tables**):

| Range | Cost | Duration | Cost | Target | Cost |
|---|---|---|---|---|---|
| Personal | +0 | Momentary | +0 | Individual / Circle / Taste | +0 |
| Eye-Contact, Touch | +1 | Concentration, Diameter | +1 | Part, Touch | +1 |
| Road, Voice | +2 | Ring, Sun | +2 | Group, Room, Smell | +2 |
| Sight | +3 | Fire, Moon, Bargain | +3 | Bloodline, Hearing, Structure | +3 |
| Arcane Connection | +4 | Until, Year | +4 | Boundary, Vision | +4 |

**Why this is the closest published analogue to a structure budget:** the axes are orthogonal and
closed, each has a small integer ladder, and the *sum* is the price. A designer adding a new Duration
must place it on a 0–4 ladder, and that placement is the entire balance decision. The base guideline
carries the effect; the three ladders carry the shape; the sum carries the cost. Nothing else in this
survey separates those three so cleanly.

**What it costs to maintain:** 50 guideline lists, hand-written — one per Technique × Form pair. That
is precisely why video games do not ship this: the authored surface is the *product* of two
vocabularies, not their sum.

**What breaks when tuned wrong:** a ladder step worth more than +5 levels is a free multiplier. The
system's known soft spots are exactly there — a cheap Range or Target step that unlocks
disproportionate effect for one magnitude.

---

## 10. ⭐ The grammar table

Across every system in this document: what the atomic unit is, what combines with what, what is
forbidden, and **what enforces it**. The last column is the one that matters — it separates systems
where illegality is *unrepresentable* from systems where it is *checked* from systems where it is
merely *priced*.

| System | Atomic unit | What combines | Budget / arity | What is forbidden | **What enforces it** |
|---|---|---|---|---|---|
| **PoE support gems** | a gem (active or support) | support → active skill | 6 sockets/links; PoE 2 adds one-copy-per-character | supports whose `excluded_types` intersect the skill's types; duplicate copies on one skill; Greater vs base variants | **data**: `allowed_types` / `excluded_types` per support, over a ~180-value type vocabulary, plus authored "Cannot support…" prose |
| **PoE item affixes** | a mod | mods → item | prefix/suffix counts by rarity | two mods sharing a `group` | **data**: `group` field — *"only one mod of a group can appear on an item at the same time"* |
| **Noita** | a spell (422, in 8 categories) | spells → shot state, recursively | wand `Capacity`, `Spells/Cast`, mana | almost nothing | **nothing structural** — mana, cast delay, capacity, shuffle, and a location gate on editing |
| **Magicka** | an element (8 base + 2 derived) | elements → a queue of ≤5 | queue length 5 | opposed pairs (fire/cold) cancel | **the queue itself** — opposition prunes, then a **total precedence order** computes the delivery form |
| **Tyranny** | Core / Expression / Accent | accents → a core+expression | Lore skill total | parts above your Lore | **a character skill threshold** |
| **NetHack** | an object *class* | appearance ↔ item, within a class | one permutation per game | cross-class swaps; non-magic and unique items | **code**: `shuffle_classes[]`, and `obj_shuffle_range()` exclusions |
| **DCSS `make_name()`** | a consonant cluster / vowel | fragments → a label | probabilistic length | clusters in the wrong word position; forbidden words | **position-tagged tables** + adjacency rules + a ROT13 blocklist |
| **Grim Dawn devotion** | a celestial power | power → a player skill | 50–55 points over 425+ stars (~12%); one power per skill | on-hit powers on buff skills, and vice versa | **a type table**: trigger-type → skill-type, plus per-colour affinity thresholds |
| **Last Epoch** | a tree node | node → its one skill | **20 points, tree deliberately not completable** | a small handful of named node pairs (~2% of nodes on the one page counted) | **printed rules text, not an allocation block** — the node stays allocatable and becomes a no-op |
| **Diablo IV aspects** | an aspect | aspect → an item slot | one per item; 3 extra Cube slots in D3 | an aspect on an ineligible slot category | **a category → slot eligibility table**; duplicates resolve to **max**, loser greyed out |
| **Hades boons** | a boon | boon → one of 5 ability slots | 5 slots, one boon each | implicit — a slot is occupied | **the slot model**; 28 authored Duo prerequisite records |
| **MTG keywords** | a keyword ability (~170) | keywords → a permanent | *"three's usually the top limit"* per card (informal) | **nothing pairwise** | **layers (613.1f) → timestamps (613.3) → dependency (613.8a) → loop fallback (613.8b)**; one generic veto (113.11) |
| **Hearthstone** | a keyword (~32 evergreen + ~40 set) | keywords → a minion | none stated | **nothing pairwise**; class keywords locked to one class | **precedence** (Charge overrides Rush) and **availability** (print fewer Charge cards) |
| **Warframe Rivens** | a stat roll | 2–3 positives + ≤1 negative | fixed arity by configuration | a short list of stats that may never be the negative | **a per-slot exclusion list** + a configuration→multiplier table |
| **Caves of Qud** | a mutation | mutations → a character | point budget; **one defect at creation** | named pairs, and whole categories via morphotype | **point costs + a pairwise exclusion list + category gates** |
| **Diablo II affixes** | an affix | prefix + suffix → item | rarity-dependent | two affixes sharing a `group`; affix on wrong `itype` | **data**: `group`, `itype1..7`, `level`/`levelreq` |
| **Borderlands** | a part | parts → one `BalanceDefinition` | one part per named slot | any part not listed for that balance | **the `BalanceDefinition` itself** — a per-slot allow-list, inherited and merged; mismatch **throws** |
| **Cassette Beasts** | a body part / a move fragment | parts → a fused form; prefix+suffix+rider → a move | 11 fixed part slots; 1 prefix + 1 suffix + 1 rider | nothing — every monster must supply every slot | **a schema contract on the part author** (dimensions, 6 frames, 3 colours), plus a published power table |
| **Monster Hunter Stories 2** | a gene | genes → a 3×3 board | 9 slots; upgrade capped at ×2; 1 wildcard | duplicate skills on one board; mismatched-size upgrades | **slot locks + an exact double-match rule**, and the donor creature is **consumed** |
| **Wildermyth** | a theme piece | pieces → anatomical slots | one piece per slot; **unique across the party** | `forbidCombineWith` (symmetric), `Forbidden Aspects` (order-dependent) | **JSON exclusion lists + slot occupancy**, enforced at targeting *and* at outcome |
| **Dwarf Fortress** | a `CE_*` creature effect | effects → a syndrome → a material → a beast | subtype gates the shape and material pools | conflicting spheres; a second attack when one exists | **one shared parameter envelope** — malformedness is not representable |
| **Ultima Ratio Regum** | a cultural variable | variables → a civilisation | fractal, conditional depth | whatever the previous choice removed | **subtractive constraint** — each pick shrinks later domains |
| **Ars Magica** | a base guideline + 3 axis steps | Technique × Form, then R/D/T | none — the *price* is the budget | nothing | **arithmetic**: `+1 magnitude = +5 levels`, summed |
| **Nemesis System** | a trait (8 named families) | traits → an orc | rank-dependent; a global power ceiling | *"combat master status … always have at least one weakness"* | **an invariant on the generator**, not a pair ban |

### What the table says

1. **Three enforcement classes, and every system uses at least two.**
   - **Structural** — the illegal combination cannot be represented (PoE's type gate, Hades' slots,
     NetHack's classes, Magicka's precedence).
   - **Priced** — legal but expensive (PoE's cost multipliers, Ars Magica's magnitudes, Warframe's
     configuration multipliers, Last Epoch's 20-point budget).
   - **Authored exclusion** — a hand-written list (Spell Echo's eight clauses, Qud's mutation pairs,
     Last Epoch's 28 nodes, D2/PoE `group`).

2. **The authored-exclusion list is the only one that grows without bound, and every mature system
   works to keep it small.** Last Epoch holds it to roughly **2% of nodes** on the page counted, by
   preferring rerouting and precedence (*"…unless it has already been converted…"*) over naming a pair.
   Magic holds it to **zero** by replacing pairwise rules with an ordering. Both are O(1); a
   forbidden-pairs table is O(n²).

3. **"Restrict where a modifier may live" is the single most reinvented device in the survey.** Grim
   Dawn (trigger-type → skill-type), Diablo IV (category → slot), Diablo III's Cube (three partitioned
   slots), Magic (keyword → colour), Hearthstone (class keywords), Hades (boon → ability slot), PoE
   (`allowed_types`), D2/PoE affixes (`itype` / `spawn_weights`). **Eight independent studios, one
   answer.** It is O(1) per part, it is legible to players, and it never invalidates an existing build.

4. **The systems with the *fewest* legality rules are the ones with the *strongest* accumulator model.**
   Noita needs no pairwise rules because modifiers mutate a shot state rather than binding to a target
   spell. Magicka needs no combination table because a precedence ladder computes the delivery form from
   the multiset. **A well-chosen intermediate representation removes rules; a badly-chosen one demands
   them.**

5. **Nobody enforces coherence and legality with the same mechanism.** Legality is a type check;
   coherence is a naming and presentation problem, solved separately (§12).

6. **The cheapest legality rules are the ones that make illegality unrepresentable.** Dwarf Fortress's
   single `CE_*` parameter envelope means the generator rolls a 5-tuple and *any* effect composes with
   any other for free. Cassette Beasts puts the contract on the *part author* — every monster supplies
   every slot at fixed dimensions, 6 frames and 3 colours — so any pair composes by construction. **In
   both cases there is no compatibility table because there is nothing a compatibility table could
   forbid.**

7. **A required counterweight beats a forbidden pair.** The Nemesis patent's rule is *"any nemesis with
   combat master status … always have at least one weakness … so that the nemesis is not invincible."*
   Caves of Qud and Warframe say the same thing with prices rather than invariants. **A floor invariant
   scales with the strength axis rather than with the number of parts, and unlike a dense exclusion
   graph it can never produce an unsatisfiable roll.**

8. **Subtractive constraint is cheaper than any of them.** URR (§8.10) picks one element, then *removes*
   from the other categories whatever would clash, and picks the next from the reduced pool. One pass,
   no lookup, no rejection sampling, coherent by construction. **It is the only technique in this table
   whose cost does not grow with the vocabulary.**

---

### ⭐ Every mechanism, priced: what it solves, what it costs, what breaks

| Mechanism | Problem it solves | Cost to build | Cost to maintain | What breaks when tuned wrong |
|---|---|---|---|---|
| **Type gate** (`allowed_types` / `excluded_types`) | illegal pairings are unrepresentable | one type vocabulary + two lists per part | **grows with the vocabulary** — every new type must be classified against every existing part | too coarse and everything is legal; too fine and the vocabulary becomes an unlearnable taxonomy (AoE II's 38 armour classes, [`../game-design/05-failure-modes.md`](../game-design/05-failure-modes.md) §6) |
| **Placement rule** (category → slot) | one part cannot be everywhere at once | one small table | **O(1) per part** — the cheapest of the structural family | a slot with too many eligible categories becomes the only slot that matters |
| **Cost multiplier** | strong combinations are legal but expensive | one number per part | re-tuned whenever resource generation changes | a route that bypasses the resource makes the whole ladder free (PoE 3.15) |
| **Configuration budget** (Warframe's table, Last Epoch's 20 points, Ars Magica's magnitudes, DCSS's `quality`) | more parts must mean weaker parts | a small integer ladder | stable — the ladder rarely changes | a ladder step worth more than its price is a free multiplier |
| **Downside as negative-cost part** (Warframe, Qud defects, DCSS `bad`) | players can buy power with drawbacks | one price per downside | needs a cap or downsides become free power | an under-priced downside is a strictly better roll, so every optimal build takes it |
| **Floor invariant** ("must also have a weakness") | no output is unbeatable | one predicate per strength axis | **scales with axes, not with parts** | an invariant that is satisfiable by a token weakness is decorative |
| **Shared parameter envelope** (Dwarf Fortress `CE_*`) | any effect composes with any other for free | one envelope design, up front | near zero — new effects inherit it | an effect that needs a parameter outside the envelope forces either a special case or a bad fit |
| **Precedence ladder** (Magicka, MTG layers, Hearthstone Charge/Rush) | conflicts resolve without a pair table | one total order | every new member must be placed in the order | a badly placed member is either always-dominant or never-visible; there is no middle setting |
| **Exclusion group** (`group` in D2 and PoE) | two halves of one family cannot co-roll | one integer per part | low — authors set it once | groups drawn too wide delete legitimate variety; too narrow and the family stacks |
| **Named pairwise exclusion** | the last resort for a genuinely bad pair | one row per pair | **O(n²) — the only mechanism that grows without bound** | the list is always behind; the pair that matters is the one nobody wrote down |
| **Property-based exclusion** ("if already converted") | covers present and future members at once | one predicate | **O(1)** | a predicate over a property nobody maintains silently stops matching |
| **Subtractive constraint** (URR) | coherence with no validation at all | one clash table consulted while narrowing | low | over-aggressive removal collapses the space to one outcome |
| **Rate normalisation** (Grim Dawn proc chance ÷ host cooldown) | the fastest host stops being the only host | one formula | low | without it, every build converges on one host; over-corrected, host choice stops mattering |
| **Name from a template** (Warframe, Cassette Beasts, D2 magic) | the output reads as designed | a fragment per part | grows linearly with the vocabulary | a name that encodes nothing is oatmeal; a name that encodes too much is unreadable |
| **Name from a curated list** (D2 rare, PoE rare, Nemesis) | flavour without a grammar | a word list, gated by type | authors add words | the name stops relating to the object — acceptable only if nothing depends on it |
| **Dilution** (Archnemesis) | complex outputs stay rare without being removed | a weight change | low | dilute too far and the interesting outputs are never seen |

---

## 11. ⭐ How each system stops degenerate combinations — banned, priced, or left open

| System | Banned outright | Priced | Left open | What blew up |
|---|---|---|---|---|
| **PoE** | wrong-type supports; duplicate supports on one skill; Greater vs base | cost multiplier (120–140%+), `less` downside lines, cooldowns on trigger supports, socket scarcity | everything else | **triggers bypassed the price entirely** — GGG: *"this entire mechanism is currently bypassed by triggering skills"* (3.15) |
| **Noita** | essentially nothing | mana, cast delay, recharge, capacity, wand-editing location gate | everything | **Chainsaw**: costs 0 mana, pays out in the time axis, which the cost model does not meter |
| **Magicka** | opposed element pairs | queue length 5 | every legal queue | *(no documented blow-up found)* |
| **Grim Dawn** | on-hit procs on buff skills | affinity thresholds, point budget, proc rate scaled to host cooldown | multiple procs across skills | pre-normalisation, the fastest host was always optimal |
| **Last Epoch** | a few named node pairs, enforced as printed no-ops | 20-point budget; 5 specialisation slots | everything else | two named nodes *"needed several iterations … to avoid degenerate gameplay"* |
| **Diablo IV** | aspect on wrong slot category | slot multipliers (×1.5 amulet, ×2 two-hander) | **any two aspects that multiply** | **Overpower**: *"we tested an **80% reduction** … but still found Overpower based builds to be overperforming"* — an 80% coefficient cut did not fix a coupling |
| **MTG** | nothing pairwise; one generic *"can't have"* veto | mana cost, card slots, the three-keyword informal ceiling | all keyword combinations | **Dredge**, Storm Scale 10: *"one of the most broken mechanics we've ever made"* |
| **Hearthstone** | nothing; class keywords are scoped | card cost, availability (print fewer Charge cards) | buffs × copy × Charge | **Charge**: *"combined with Attack-increasing buffs and copy effects, producing excessively powerful burst combos … one turn kills"* |
| **Warframe** | a short list of stats that can never be negative | configuration multipliers; **Disposition, re-derived from usage telemetry every 3 months** | any stat pair | popularity pricing retroactively devalues a player's investment |
| **Caves of Qud** | named mutation pairs; morphotype category locks | mutation points; defects as negative cost | everything else | *(not surveyed)* |
| **Ars Magica** | nothing | `+1 magnitude = +5 levels`, summed across three axes | everything | ladder steps worth more than their +5 |
| **Diablo II / PoE affixes** | two affixes in one `group` | item level, affix `level`/`levelreq` windows, spawn weights | cross-group stacking | *(the `group` mechanism has been stable for 25 years)* |

### The five things that actually blew up, and what they have in common

1. **PoE triggers (3.15)** — a route that bypassed the price.
2. **Noita's Chainsaw** — a part whose cost is denominated in a resource the model does not meter.
3. **Diablo IV Overpower** — a multiplier coupled to a stat the player already stacks.
4. **Hearthstone Charge** — a narrow keyword multiplied by a broad, load-bearing modifier class.
5. **MTG Dredge** — a mechanic that replaced a cost (drawing) with a resource the game had no other
   sink for.

**All five are the same defect: the priced thing and the powerful thing were not the same thing.** In
each case the cost model measured one quantity and the power grew in another. **INFERENCE:** a cost
model is only as good as its coverage of the axes power can grow along, and every one of these was
caught only after ship, by players.

### ⭐ The Elder Scrolls spell-maker — three generations of a published price function, and every way it broke

**The closest historical analogue to a priced composition system, and the only one whose pricing
function is fully published across three games.** All formulas from UESP's own pages, cross-checked
against OpenMW's implementation (**first-tier for the Morrowind formula, since the open-source
reimplementation agrees to the unit digit**).

**Morrowind** ([UESP](https://en.uesp.net/wiki/Morrowind:Spellmakers); confirmed against
[OpenMW `calcEffectCost`](https://raw.githubusercontent.com/OpenMW/openmw/master/apps/openmw/mwmechanics/spellutil.cpp)):

```
Touch/Self:  floor( base_cost · ((min + max) · (duration + 1) + area) / 40 )
Target:      floor( 1.5 · base_cost · ((min + max) · (duration + 1) + area) / 40 )
```

**Oblivion** ([UESP](https://en.uesp.net/wiki/Oblivion:Spell_Making)):

```
B = Base Cost / 10 ,  M = Magnitude ^ 1.28 ,  D = Duration ,  A = Area × 0.15
Total = B × M × D × A          ( ×1.5 if Targeted; M, D, A each floored at 1 )
```

The `1.28` exponent is confirmed — three independent checks reproduce UESP's own published costs to the
unit digit. Mastery gating is computed on the **base** cost before the skill discount, and *"For
multi-effect spells, the requirement will be in the school of the **single** effect with the highest base
Magicka cost."*

**⭐ Every major exploit is one term of the price function failing to track one axis of value:**

| Exploit | The broken term |
|---|---|
| **Duration is cheap** (Morrowind) | the `+1` offset — price per delivered point is `(D+1)/D`, i.e. 2.0 at D=1 and → 1.0 as D grows. The Morrowind Code Patch's fix is **literally deleting that `+1`** |
| **Effect splitting** (Oblivion) | `M^1.28` is convex in magnitude but linear in duration, so price per point ∝ `M^0.28`. UESP: *"20pts Fire + 20pts Frost + 20pts Shock is a cheaper spell than 60 pts Fire Damage."* `3 × 20^1.28 = 137.7` vs `60^1.28 = 189.7` — **a 27% discount for spelling the same 60 points three ways** *(computed)* |
| **1-second duration** (Oblivion) | the floor, plus a UI freeze — *"When a window is opened or a conversation is started, **any spell effects remain active indefinitely**."* The price charges for *duration*; the value is *one check* |
| **1-point area** | `A = Area × 0.15` floored at 1, so Area 0–6 all price identically. **A six-foot blast radius is free.** In Morrowind area is *additive* and never multiplied by magnitude — **the `magnitude × area` interaction term is simply missing** |
| **Fortify Intelligence alchemy loop** (Morrowind) | cost is a **plain sum over effects** with no superadditivity, and price is **independent of the caster's pool**, so a spell that raises the pool that pays for it closes a loop. UESP: *"the values for each successive potion will go up **exponentially**"* |
| **The uint16 wrap** (Morrowind) | *"a spell with a projected casting cost of 65,542 will actually cost only 6. The Spellmaker still charges the full price (in this case, **458,794 Gold**)."* Because success chance *subtracts* cost, casting difficulty wraps too |
| **Weakness-to-Magic stacking** (Oblivion) | *"Weakness to Magic amplifies any following harmful spell, **including weakness to magic itself**."* UESP's published ladder reaches **108,900 damage** from a 100-damage base. **The price function has no term for the target's current state** — it is defined over a strictly smaller input space than the value function |
| **The `max` vs `sum` bug** (Oblivion) | *"the game checks the magicka cost of the **single most expensive effect, instead of summing all the magicka costs** as it should."* **The gate aggregates with `max`; the cost aggregates with `sum`** — which also enables *school laundering*, wrapping a Destruction effect inside an Illusion-gated spell |
| **Downward-rounded rates** (Daggerfall) | *"If adjusted to '1 per **2** level(s),' the cost of this entire component becomes **zero due to downward rounding**."* Integer truncation applied to a *rate* rather than a *total*, so the exploit gets better as you level |

**⭐ The one-line design read (INFERENCE).** Across three generations the pricing function was always a
function of the **spell's declared parameters**, and never of **the state it acts on** or **the resource
it consumes**. Every major exploit is an instance of that one gap. And note that convexity did not save
it: Oblivion's `M^1.28` made stacking magnitude expensive, so players moved composition off the priced
axis onto an unpriced one.

### GGG's published price bands — the only public "how much is a downside worth" table

**FACT**, from the 3.15 Expedition manifesto
([pathofexile.com](https://www.pathofexile.com/forum/view-thread/3147157) — **first-tier**), stating the
problem first:

> *"There are gems that grant huge multiplicative damage bonuses and there are gems that do a bunch of
> stuff you don't really care about. **When you're building a character, by far the correct choice is
> just to stack on all the multiplicative damage bonuses and ignore all the interesting utility support
> gems because their opportunity cost is just too high.**"*

> *"The support gem changes affect six-linked skills far more than they do those with fewer links,
> **because of how each support gem compounds with each other**…"*

And then the standardisation they adopted:

> *"Damage multipliers between Support Gems have been standardised following these rules:*
> - *Gems that provide a useful benefit in addition to damage usually grant **less than 25%** more damage at gem level 20.*
> - *Those that provide only a damage bonus or have only a very mild restriction or penalty grant around **30-35%**.*
> - *Those with downsides or restrictions grant between **38% and 48%** more damage, depending on how severe the cost of using the support is."*

Plus the motivating observation: *"Many players are able to complete mid-difficulty boss fights in a
fraction of a second… **Our ability to create interesting boss fights and monster combat is removed when
players are routinely killing those monsters or bosses before they have used a single ability.**"*

**INFERENCE.** This is a **three-band ordinal price for a downside**, published. It is the same shape as
Warframe's four-row configuration table (§12.2) and Ars Magica's magnitude ladder (§9) — a small integer
scale on *how restrictive* a part is, converted into a magnitude multiplier. Nobody in this survey
publishes a continuous function for it.

### What the survey says about banning versus pricing

**FACT — nobody bans a pair when they can restrict a placement.** See §10 point 3.

**FACT — when a pair genuinely must be broken, three studios independently chose to restrict the
narrower half.** Hearthstone's stated reasoning is the clearest:
*"restricting [buffs and copy effects] would detract from the game far more significantly than simply
restricting the availability of Charge."*

**FACT — the tightening direction is asymmetric.** Crate: *"since then we've only **reenabled** devotion
assignment options, not disabled any. A shotgun penalty for the proc could be considered."* Blizzard,
after the 1.1.0 backlash: *"we will take a more surgical approach … if we're adjusting something
overpowered, we will provide compelling alternatives"*
([campfire recap, July 2023](https://news.blizzard.com/en-gb/diablo4/23985148/)). **Legality tables
loosen after ship; magnitudes tighten.** Removing a legal combination invalidates builds players
already own, so the shipped practice is to nerf the number, not delete the pairing.

**FACT — duplicate handling is resolved to `max`, not banned, wherever it appears.** Diablo IV greys out
the weaker copy. Magic declares *"are redundant"* on every static keyword. Only PoE actually forbids two
copies of the same support on one skill, and even that is a socket-level rule rather than a stated
prohibition.

---

## 12. Coherence — how a composed thing gets a name and reads as designed

**Nothing in this survey generates prose at runtime.** Every shipped system composes its names from
templates, syllable tables or fixed word lists. The four grammars below are all fully specified, and
they use four genuinely different strategies.

### 12.1 Warframe Rivens — the name is a function of the rolled parts, ordered by magnitude

**The most directly transferable grammar found.** A Riven is a procedurally generated mod: 2–3 positive
stats, optionally 1 negative, drawn from a weapon-type-specific pool.

The naming rule, quoted
([Warframe Wiki, Riven Mods](https://wiki.warframe.com/w/Riven_Mods), reached via a reader proxy —
**second-tier, but the wiki is maintained from datamined values**):

> *"The prefix and core are determined by the stats with the highest and second highest randomized
> modifier, respectively. The suffix is always the stat with the lowest randomized modifier."*

Name shape: **`Prefix-CoreSuffix`**, or **`CoreSuffix`** when there are only two named stats. Each of
the **31 possible attributes** owns a designated prefix, core and suffix syllable — Damage is
`Visi` / `Ata`, Critical Chance is `Crita` / `Cron`. The worked example given is
`Vectis Sati-critaata`: Multishot highest, Critical Chance second, Base Damage lowest.

**Four properties worth naming:**

1. **The name is a pure function of the roll.** No table lookup on the combination, no authored
   per-combination string. Two identical rolls always produce the same name.
2. **The ordering carries information.** A player reads the prefix and knows the dominant stat. The
   name is a *legible summary*, not decoration.
3. **The vocabulary is per-atom, not per-combination.** 31 attributes × 3 syllable slots = 93 authored
   fragments *(computed)* covering the entire name space.
4. **Only three stats are ever named**, however many rolled. The grammar has a fixed arity; the roll
   does not.

### 12.2 Warframe Rivens — and the cost model that goes with it

The same page carries a pricing table that is the closest shipped analogue to a structure budget:

| Configuration | Bonus multiplier | Malus multiplier |
|---|---|---|
| 2 positive, 0 negative | 0.99 | 0 |
| 2 positive, 1 negative | **1.2375** | −0.495 |
| 3 positive, 0 negative | **0.75** | 0 |
| 3 positive, 1 negative | 0.9375 | −0.75 |

**FACT.** Drawing a third positive costs about 24% of every positive's magnitude (0.99 → 0.75).
Accepting a negative *pays back* about 25% (0.99 → 1.2375). **More parts means weaker parts, and a
downside buys magnitude back** — priced, not banned.

**FACT — the legality rule.** Certain stat types can never roll as the negative: *"Positive values only
(i.e. these attributes will never be a negative trait)"* applies to Cold, Electricity, Heat, Toxin
Damage and Punch Through. A short hand-written exclusion list on the *slot*, not on the pair.

**FACT — the live price.** Riven **Disposition** is *"a stat multiplier… that collates the usage
popularity of a given weapon by players of high Mastery Rank and internal rankings based on the
'strength' of a weapon"*, on a five-band 0.5–1.55 scale, **updated every three months**. New weapons
start at 0.5. This is a composition system whose price is re-derived from live telemetry on a fixed
cadence — the only example of that found in this survey.

**What breaks when tuned wrong:** disposition inverts the incentive if it moves too fast — a weapon
becomes strong, gets used, and its rivens are devalued, which is intended; but a player who invested in
that riven is retroactively nerfed. That is the known and much-discussed cost of pricing power by
popularity.

### 12.3 ⭐ Borderlands — the name is *itself* two of the parts

**The cleanest architecture in the survey, and fully datamined.**

**FACT.** The weapon serial stores **eleven part slots**, and **two of them are name parts**
([`BaseWeapon.cs`, gibbed's Borderlands 2 save-editor source](https://github.com/gibbed/Gibbed.Borderlands2/blob/master/projects/Gibbed.Borderlands2.FileFormats/Items/BaseWeapon.cs)
— **first-tier**):

```
_BodyPart, _GripPart, _BarrelPart, _SightPart, _StockPart, _ElementalPart,
_Accessory1Part, _Accessory2Part, _MaterialPart, _PrefixPart, _TitlePart
```

**This is the load-bearing fact: Borderlands does not compute the name at display time. It rolls the
name once at generation and persists the chosen prefix word and title word as parts, next to the barrel
and the grip. The name is data, not a function.**

**FACT — where each fragment comes from.** Every functional part carries optional `titles` and/or
`prefixes` arrays pointing at name-part objects
([gibbed's `Weapon Parts.json` dump](https://github.com/gibbed/Borderlands2Dumps/blob/master/Weapon%20Parts.json)
— **first-tier**):

```json
"GD_Weap_AssaultRifle.Barrel.AR_Barrel_Dahl": {
  "type": "Barrel",
  "titles":   ["GD_Weap_AssaultRifle.Name.Title.Title_Barrel_Dahl_Carbine",
               "GD_Weap_AssaultRifle.Name.Title_Bandit.Title_Barrel_Dahl_Carbine"],
  "prefixes": ["GD_Weap_AssaultRifle.Name.Prefix_Bandit.Prefix_Barrel_Dahl_Carbine"] }

"GD_Weap_Pistol.Grip.Pistol_Grip_Bandit": {
  "type": "Grip",
  "prefixes": ["GD_Weap_Shared_Names.Name.Prefix_Bandit.Prefix_Grip_Bandit"] }

"GD_Weap_AssaultRifle.elemental.AR_Elemental_Fire": {
  "type": "Elemental",
  "prefixes": ["GD_Weap_AssaultRifle.Name.Prefix.Prefix_Elemental_Fire"] }
```

**FACT.** **Barrels carry `titles`; accessories, grips and elementals carry only `prefixes`.** The title
comes from the barrel/body; the prefix from the accessory, element or grip. The weapon *type* supplies
the fallback pair.

Name-part object paths are `GD_Weap_<Type>.Name.{Prefix|Title}[_<Manufacturer>].{Prefix|Title}_<PartId>`,
and the display string lives in a `PartName` field. **The same physical part resolves to a different
word per manufacturer** — from a community mod that rewrites them
([BLCMods](https://github.com/BLCM/BLCMods/blob/master/Borderlands%202%20mods/Orudeon/Prefix%20Rework%20V2.1a%20by%20AngrierPat.txt)):

```
set GD_Weap_Pistol.Name.Prefix.Prefix_Laser_Accuracy          PartName Critical
set GD_Weap_Pistol.Name.Prefix_Bandit.Prefix_Laser_Accuracy   PartName Splurt
set GD_Weap_Pistol.Name.Prefix_Dahl.Prefix_Laser_Accuracy     PartName Raddled
set GD_Weap_Pistol.Name.Prefix_Hyperion.Prefix_Laser_Accuracy PartName Stimulating
set GD_Weap_Pistol.Name.Prefix_Jakobs.Prefix_Laser_Accuracy   PartName Deadeye
set GD_Weap_Pistol.Name.Prefix_Tediore.Prefix_Laser_Accuracy  PartName Special Promotion:
set GD_Weap_Pistol.Name.Prefix_Torgue.Prefix_Laser_Accuracy   PartName KerPowza
```

**FACT — the anti-nonsense mechanism is total per-combination authorship.** Every fragment in every
context was typed by a human. **The generator only selects; it never combines words.** Legendaries get a
hand-authored title part flagged `"unique": true` —
`"GD_Weap_Shotgun.Name.Title_Torgue.Title_Legendary_Flakker": {"name": "Flakker"}`.

**FACT.** The same grammar covers non-weapons, with generic Greek slot names
([`Item Parts.json`](https://github.com/gibbed/Borderlands2Dumps/blob/master/Item%20Parts.json)) —
Alpha, Beta, Gamma, Delta, Epsilon, Zeta, Eta, Theta, Material — each carrying the same `titles` /
`prefixes` arrays.

**FACT — the generator's public history.** It was a designer tool called **Gearbuilder**, presented at
**SXSW 2010** (not GDC) by Matthew Armstrong and Jimmy Sieben. The real gun count at launch was
**16,164,886**, marketed as "87 bazillion"; 12 manufacturers each supply a different grip, body,
cylinder, barrel and accessory set. Armstrong's framing was *"I want you to be attached to your gun,"*
and the stated engineering win was auto-balancing — if a gun is balanced at level 1 and level 50 it is
balanced at every level between
([Engadget, SXSW 2010](https://www.engadget.com/2010-03-16-sxsw-creating-87-bazillion-guns-for-borderlands.html)).

**SECOND-TIER (search-summary only; the Borderlands wiki is 401/402 here).** Prefixes are generated in
tiers and **the highest-priority applicable prefix overrides all others**, with three main groups —
accessory, element, grip. All Legendary/Seraph/Pearlescent and all non-unique purple weapons always have
an accessory; green/blue sometimes; white never. **The priority numbers themselves are stripped from the
public dumps** — see *What I could not find*.

**Three things to take from this and nothing else:**

1. **Two of the eleven part slots are name slots.** The name is rolled and stored, not derived at
   display time — so it is stable, hashable and diffable forever.
2. **Fragments are authored per (part × manufacturer × weapon type).** There is no free recombination of
   words anywhere. The *selection* is generative; the *vocabulary* is not.
3. **A priority rule picks one prefix**, so the arity of the name is fixed regardless of how many parts
   could have contributed one.

### 12.4 Diablo II — two grammars, and the rare one is deliberately *not* derived from the parts

Read directly from the shipped game tables
([`blizzhackers/d2data`](https://github.com/blizzhackers/d2data) JSON exports of the `.txt` files —
**first-tier**):

| Table | Entries | Carries a mod? | Carries item-type gating? |
|---|---|---|---|
| `magicprefix.json` | **337–358** ⚠ | yes (`mod1code`, `mod1min`, `mod1max`) | yes (`itype1..7`) |
| `magicsuffix.json` | **334** | yes | yes |
| `rareprefix.json` | **46** | **no — name only** | yes (`itype1..3`) |
| `raresuffix.json` | **155** | **no — name only** | yes (`itype1..6`) |

*(Counts read from the files. ⚠ Two independent counts of `magicprefix.json` in this pass returned 337
and 358; the shipped 1.13 `MagicPrefix.txt` has **671 lines** including header and blank spacer rows
([mirror](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MagicPrefix.txt)), so the JSON
figure depends on the converter's filtering. Treat it as ~340–360 and do not quote a precise number.)*

**FACT — the rare tables are confirmed cosmetic-only by the modding community's own documentation.**
Phrozen Keep moderator k0r3l1k: *"These two text files actually have nothing to do with the stats the
spawn on an item. **They only affect the names the items will receive when generated.**"* and *"only
certain item types can have certain rare names"*
([d2mods.info](https://d2mods.info/forum/viewtopic.php?t=55981) — **second-tier but authoritative for
this file format**).

A magic prefix row, verbatim:

```json
"1": { "Name": "Sturdy", "version": 0, "spawnable": 1, "rare": 1,
       "level": 4, "levelreq": 3, "frequency": 0, "group": 101,
       "mod1code": "ac%", "mod1min": 20, "mod1max": 30,
       "itype1": "armo", "multiply": 0, "add": 0 }
```

A rare prefix and a rare suffix row, verbatim:

```json
"0": { "name": "Beast", "version": 0, "itype1": "armo", "itype2": "weap", "itype3": "misc" }
"0": { "name": "bite",  "version": 0, "itype1": "swor", "itype2": "knif",
       "itype3": "spea", "itype4": "pole", "itype5": "axe", "itype6": "h2h" }
```

**Two different grammars in one game:**

- **Magic items:** `[prefix] <base> [suffix]` where the prefix and suffix **are** the affixes. *Sturdy
  Plate Mail* means "+20–30% armour class", because `Sturdy` is that mod. The name is the mechanics.
- **Rare items:** `<rare prefix> <rare suffix>` drawn from two **name-only** tables, gated by item type
  and **unrelated to the up-to-six affixes actually rolled**. *Beast Bite* is a sword whose name says
  nothing about its mods.

**FACT.** 46 × 155 = **7,130 possible rare names** *(computed)*, before item-type gating narrows the
pool. `bite` is legal only on `swor, knif, spea, pole, axe, h2h` — so the flavour never contradicts the
object.

**INFERENCE, and it is the design lesson:** D2 uses the derived grammar where the part count is small
and fixed (one prefix, one suffix, and each *is* a mod) and switches to an undecorated two-word name
where the part count is large and variable (rares carry up to six affixes). **A name derived from the
parts stops working once the parts outnumber the name slots** — and rather than pick three of six,
Blizzard stopped deriving.

**And the exclusion mechanism, stated by the file-format documentation:** the `group` column exists
*"to prevent an item from spawning say with Ferocious and Cruel at the same time"* — the game cannot
pick more than one affix from each group, enforced across prefixes, suffixes and AutoMagic
simultaneously. `iType1-7` whitelists item types; `eType1-5` blacklists and overrides the whitelist;
`frequency` weights selection *within* a group as `frequency / total_frequency`
([d2mods.info file guide](https://d2mods.info/forum/kb/viewarticle?a=445)).

### 12.5 Path of Exile rare names — two words from pools partitioned by *what is being named*

**FACT** ([PoEDB, Words](https://poedb.tw/us/Words) — **first-tier, generated from game data**):

| Word list | Entries | Examples |
|---|---|---|
| `RareItemPrefix` | **305** | Dire, Cinder, Iron, Steel, Agony, Dark, Cruel, Death, Celestial |
| `RareItemSuffix` | **525** | Abyss, Apex, Bind, Cage, Chambers, Coffers, Core, Court, Zenith |
| `RareMonsterPrefix` / `Suffix` / `Epithet` | 167 / 211 / 216 | Acid, Agony, Azure / adder, back, blade / the Accursed, the Blessed |
| `RareChestPrefix` / `Suffix` | 26 / 33 | Armageddon, Doom, Plunder / Vault, Device, Contraption |
| `SettlerPrefix` / `Suffix` | 122 / 76 | personal names / surnames |

Roughly **1,707 word entries in total**, and the rare-item name space is 305 × 525 = **160,125**
combinations *(computed)*.

**FACT (second-tier, PoE wiki).** *"Two words are picked from a pool of possible words and combined to
form the final name. Names do not affect the item in any way, although they can be important for certain
vendor recipes."*

**The anti-nonsense mechanism is part-of-speech partitioning.** The suffix pool is almost entirely nouns
of place, body and object; the prefix pool almost entirely adjectives and material nouns. **Any pairing
is grammatical by construction**, so no compatibility rule is needed at all. PoE buys legibility from the
mod list, not from the name, and accepts a wide tolerance for odd names.

**Open question:** whether `RareItemPrefix`/`RareItemSuffix` are gated per item class. PoEDB shows no tag
column, but the community "Rare Item Name Index" is organised by item class, which hints at gating. See
*What I could not find*.

### 12.6 Dwarf Fortress — the most completely published slot grammar, and its admitted failure

**FACT — the template**, for fortresses, groups, artifacts and symbols of office
([DF Wiki, Names and symbols](https://dwarffortresswiki.org/index.php/Names_and_symbols) — **second-tier
but the canonical reference for the raw format**):

```
[Front][Rear] the [Adj 1] [Adj 2] [hyphen compound]-["the" Noun] of ["of" noun]
```

Front and Rear join with **no space**. Adjectives modify the "the"/"of" noun, not the compound. Any slot
may be empty. Worked outputs: `Foobar the Great Moral Thraal-Generals`, `Foobar the Goblin-Chunks of
Exploding`.

**FACT — gate 1: each word declares which slots it may occupy.** From the shipped
[`language_words.txt`](https://raw.githubusercontent.com/DF-Wiki/DFRawFunctions/master/raws/v50/language_words.txt)
(**first-tier, the game's own raws**):

```
[WORD:ABBEY]
	[NOUN:abbey:abbeys]
		[FRONT_COMPOUND_NOUN_SING]
		[REAR_COMPOUND_NOUN_SING]
		[THE_NOUN_SING]
		[REAR_COMPOUND_NOUN_PLUR]
		[OF_NOUN_PLUR]
```

Tag census over that file *(computed)*: 2,196 `WORD`, 1,738 `NOUN`, 849 `ADJ`, 698 `VERB`; 1,673
`THE_NOUN_SING`, 1,380 `REAR_COMPOUND_NOUN_SING`, 1,364 `FRONT_COMPOUND_NOUN_SING`, 1,294 `OF_NOUN_PLUR`.
`ABBEY` is not tagged `OF_NOUN_SING`, so *"of Abbey"* is unreachable.

**FACT — gate 2: 84 symbol pools, whitelisted and blacklisted per culture *and per name class*.**
`language_SYM.txt` defines groups (FLOWERY, NATURE, HOLY, EVIL, NEGATOR, MAGIC, VIOLENT, DEATH,
ARTIFICE, EARTH, plus `NAME_WAR`, `NAME_BRIDGE`, `NAME_BUILDING_TEMPLE`, …). Each civilisation then
declares, in eight lines
([`entity_default.txt`](https://raw.githubusercontent.com/DF-Wiki/DFRawFunctions/master/raws/v50/entity_default.txt)):

```
[SELECT_SYMBOL:WAR:NAME_WAR]        [SUBSELECT_SYMBOL:WAR:VIOLENT]
[SELECT_SYMBOL:BRIDGE:NAME_BRIDGE]
[SELECT_SYMBOL:REMAINING:ARTIFICE]  [SELECT_SYMBOL:REMAINING:EARTH]
[CULL_SYMBOL:ALL:DOMESTIC]  [CULL_SYMBOL:ALL:SUBORDINATE]  [CULL_SYMBOL:ALL:EVIL]
[CULL_SYMBOL:ALL:FLOWERY]   [CULL_SYMBOL:ALL:NEGATIVE]     [CULL_SYMBOL:ALL:UGLY]
[CULL_SYMBOL:ALL:NEGATOR]
```

The elf entity is identical except `REMAINING` is `NATURE` + `FLOWERY`, and it does **not** cull FLOWERY.
Three reusable primitives: **`SELECT_SYMBOL`** (whitelist keyed to what is being named),
**`SUBSELECT_SYMBOL`** (second-level narrowing), **`CULL_SYMBOL:ALL`** (culture-wide blacklist).

**FACT — and the admitted failure mode, which is the honest warning.** Epithets are *"selected somewhat
randomly, leading to epithets that translate as gibberish, such as **'the Hardy Ring-Cobra of
Dashing'**."*

**INFERENCE.** DF checks slot legality and pool membership. It does **not** check semantic relation
*between filled slots*. That is the single failure a slot grammar cannot fix from inside itself.

### 12.7 DCSS randart names — a weighted choice among whole templates

The most sophisticated open grammar found, and structurally different from every other entry here: the
top level is not a slot sequence but a **weighted choice among ~35 complete templates**, each expanded
recursively.

**FACT**, verbatim from
[`dat/database/randname.txt`](https://raw.githubusercontent.com/crawl/crawl/master/crawl-ref/source/dat/database/randname.txt)
(**first-tier**):

```
weapon
of @_power_or_anger_@
of @_battle_or_war_@
w:1  of @player_doom@
w:8  of @death_or_doom@
w:1  of @_verbing_@ @death_or_doom@
w:6  of @_adjective_@ @_strategy_or_justice_@
w:3  of the @_verbing_@ @_people_name_@
w:5  of the @_verbing_@ @_weapon_animal_@
w:8  of @branch_name@
w:9  of @god_name_possessive@ @divine_esteem@
w:30 "@_plain_weapon_name_@"
```

Default weight is `w:10`. Final assembly is `<base item> " " <name>`. Fragment files are split by item
class — `rand_wpn.txt`, `rand_arm.txt`, `rand_all.txt`, `randbook.txt` — plus hardcoded `player_name`,
`player_species`, `branch_name`, `god_name`, `xom_name`.

**Five distinct anti-nonsense mechanisms, all documented in the file or the code:**

1. **Per-item-class keyword files.** Armour draws `_armour_animal_` and `_profession_name_`; weapons draw
   `_weapon_animal_` and `_battle_or_war_`. Disjoint by construction.
2. **A hard length cap with reroll, not truncation.** *"Randart names may only have a maximum length of
   25 symbols (spaces included). This comparison takes place after all replacements have been taken care
   of… If a name turns out to be longer than this threshold, the game will roll another one."*
   Implemented as `do {…} while (--tries > 0 && strwidth(name) > 25);` with 100 tries and an
   `"of Bugginess"` fallback ([`artefact.cc`](https://raw.githubusercontent.com/crawl/crawl/master/crawl-ref/source/artefact.cc)).
3. **Cross-slot semantic filtering — the only instance found in any system surveyed.** *"the god will not
   be picked entirely at random as there are some restrictions to make sure that e.g. no good god is
   chosen for evil weapons, or that Zin doesn't get picked for randarts with mutagenic properties."*
4. **A collision blacklist against hand-authored content**, as comments in the data file itself:
   `# "Power" is not literally here, to prevent generating conflicts with the unrand` and
   `# Don't use "Pain", it's easily confused with the brand.`
5. **A second, non-grammar path.** Only about half of weapon/armour randarts (1 in 5 for jewellery) use
   the template grammar; the rest get an invented word from `make_name()` (§5.3).

**INFERENCE.** Point 3 is the one nobody else does. Every other system in this document checks *whether a
fragment may occupy a slot*; DCSS additionally checks *whether two chosen fragments contradict each
other*, and it does it against the item's mechanical properties. That is the fix for the "Hardy
Ring-Cobra of Dashing" failure, and it costs one compatibility predicate per semantically loaded slot.

### 12.8 Caves of Qud — the name is *predicted* by the mechanics

Qud is closed-source, so the template below is inference from published outputs — but the *gating* is
documented and is the interesting part.

**FACT — relic names split by provenance** ([Qud Wiki, Relic](https://wiki.cavesofqud.com/wiki/Relic),
[Historic site](https://wiki.cavesofqud.com/wiki/Historic_site) — **second-tier**):

- **Lore-event relic**, named from the sultan-history event that created or lost it: *"…he lost his
  prized **Shiningacus Succulentswoe** during the course of the conflict."* Also `Radiantucus
  Flowersboon`; curios `Blockecus`, `Batteroca`, `Charmica`, `Lightyca`, `Rootoca`.
- **Site relic**: `The <adjective> <base noun> of <site name>` — e.g. `The Weighted Chain of Salep
  Seminary`.
- **Floor relic**: named after the generated floor it sits on.

**INFERENCE on the template:** `<theme-stem + Latinate -acus/-ucus>` `<theme-noun + affect-noun>`, where
the affect noun (`-woe` / `-boon`) tracks whether the originating event was a loss or a gain.

**FACT — the anti-nonsense mechanism is a theme key that drives name, lore, item mods *and* architecture
together.** Eleven [sultan themes](https://wiki.cavesofqud.com/wiki/Sultan_themes), each owning a closed
noun list and a closed power list: Glass → prisms, mirrors → Glazed, Reflect, Clairvoyance. Time →
hourglasses, atomic clocks → Temporal Fugue. Might → swords, gauntlets, skulls → +Strength.

**The name is not merely *permitted* by the mechanics — it is *predicted* by them.**

**FACT — a second gate: eras.** Early and Late sultanate pools are disjoint per slot
([Sultan histories](https://wiki.cavesofqud.com/wiki/Sultan_histories)) — adjectives Early = star,
temporal, cosmic, empyrean, astral, luminous; Late = sand, salt, dust, slag, sea, cinder. Sultans 1–2 are
Early, 3 is a 50:50 mix, 4–5 are Late. Nested angle-bracket slots (`<3D shape>`, `<city-state>`) confirm
a recursive replacement grammar underneath.

**And themes enter diegetically** — an "inspiring experience" event both adds a theme and names the noun
that triggered it, so new vocabulary only arrives with a narrated cause.

The talk to cite: **"Math for Game Developers: End-to-End Procedural Generation in *Caves of Qud*",
Brian Bucklew & Jason Grinblat, GDC 2019**, whose description explicitly names *"replacement grammars to
generate text"* ([GDC Vault](https://www.gdcvault.com/play/1026313/Math-for-Game-Developers-End)).

### 12.9 No Man's Sky — the cheapest anti-nonsense rule in the survey

Hello Games publish nothing; the structures below come from the community MBIN decompiler
(**second-tier: reverse-engineered, but it is the shipped struct layout**).

**FACT.** Place names are a **Markov chain over letter sequences**, not a word template:
`MarkovSelectorEnum { Generic, Mineral, Region_NO, Region_RU, Region_CH, Region_JP, Region_LT,
Region_FL }`
([`GcNameGeneratorTypes.cs`](https://raw.githubusercontent.com/monkeyman192/MBINCompiler/development/libMBIN/Source/NMS/GameComponents/GcNameGeneratorTypes.cs)).
Names are pronounceable by construction because they are sampled from a model of real name strings.

**FACT — and this is the rule worth stealing.** The descriptive half of a place name is selected by
**what the terrain actually is**: `SectorNameEnum { Generic, Elevated, Low, Trees, LushTrees, Lush, Wet,
Cave, Dead, Buildings, Water, Ice }`
([`GcNameGeneratorSectorTypes.cs`](https://raw.githubusercontent.com/monkeyman192/MBINCompiler/development/libMBIN/Source/NMS/GameComponents/GcNameGeneratorSectorTypes.cs)).
**A frozen world cannot draw from the `LushTrees` list.**

**FACT — procedural products use a placeholder template with rarity-tiered fills**
([`GcProceduralProductData.cs`](https://raw.githubusercontent.com/monkeyman192/MBINCompiler/development/libMBIN/Source/NMS/GameComponents/GcProceduralProductData.cs)):

```csharp
public class GcProceduralProductWord {
    public GcNameGeneratorWord RareWord;
    public GcNameGeneratorWord UncommonWord;
    public GcNameGeneratorWord Word;
    public NMSString0x20       ReplaceKey;   // the placeholder token in the base string
}
```

Three gates: **28 per-category tables** (Loot, Document, BioSample, Fossil, Plant, Tool, Salvage, Bones,
FreighterCaptLog, MessageInBottle, …) so a fossil can never be named out of the freighter-log vocabulary;
**rarity and vocabulary are the same axis** — a rare item reads rare because the *pool* changed, not
because an adjective was appended; and per-biome drop weights close the loop with the sector naming.

### 12.10 The Nemesis System — the title comes first, and the traits are derived from it

The inverse of every other system here, quoted from the patent
([US10926179B2](https://patents.google.com/patent/US10926179B2/en) — **first-tier**):

> *"Each nemesis may be assigned its own unique name and title, for example, by random initial
> assignment of one name and one title for each NPC from a list of possible names and titles, at the
> time game play is initiated."*

> *"Titles can be useful as they may tell the nemesis' habits and behavior. To achieve this, the game
> engine may assign some or all of each nemesis' traits based on its title."*

**FACT.** The title is rolled first, and the traits are then chosen to *justify* it. "Ratbag the
Coward" is a coward because the generator drew `the Coward` and then gave him fear-driven behaviour —
not the other way round.

**INFERENCE.** This is the cheapest possible coherence guarantee: if the label is drawn before the
mechanics and the mechanics are constrained by the label, the two can never disagree. The cost is
expressive range — the number of distinct characters is bounded by the title list, not by the trait
combinatorics.

Dialogue is composed the same way, not written: *"the faction manager may associate a dialog identifier
with the event record, based on the NPC action… the NPC dialog will be the phrase selected by the
faction manager for the faction member based on the prior event."* Identifiers into a phrase bank —
never generated text.

### 12.11 Crawl — a phonetic name with deliberately zero semantic content

Covered in §5.3. The relevant contrast: DCSS's `make_name()` is the only grammar here that must
**not** leak information, because the label is the thing the player is trying to identify. So it
composes from position-tagged consonant clusters and weighted vowels rather than from the item's
properties, and it needs an explicit profanity filter because a phonetic generator over an English
alphabet will eventually produce one.

### 12.12 Cassette Beasts and Dwarf Fortress — when the name *is* the datasheet

Both covered in §8. Restated here because they are the two ends of one idea:

- **Cassette Beasts** encodes the *kind* — prefix ⇒ element, suffix ⇒ melee/ranged and single/team. The
  name is a lossless spec, and the flavour half is free to vary.
- **Dwarf Fortress** encodes the *threat* — the description frame ends `Beware its <special attack>!`,
  so the one mechanically decisive fact is always the last clause the player reads.
- **Warframe** (§12.1) encodes the *rank order* — prefix, core and suffix are the highest,
  second-highest and lowest rolled stat.

**INFERENCE.** Three different things a composed name can encode: what the object *is*, what it will
*do to you*, and which of its numbers is *largest*. All three read as designed. What does not read as
designed is a name encoding nothing — which is D2's rare-name case (§12.4), and D2 gets away with it
only because the name is drawn from a curated word list rather than assembled.

### 12.13 The baseline a grammar has to beat — total authorship

**FACT — Rogue Legacy is not a grammar.** The title is a localisation format string with two holes:
`string.Format(getResourceString("LOC_ID_LINEAGE_OBJ_12_NEW"), playerName, romanNumerals)` (a separate
`_14_NEW` for female). Given names are read line by line from a shipped `HeroNames.txt`; the numeral is
computed from lineage depth
([`Game.cs`](https://raw.githubusercontent.com/flibitijibibo/RogueLegacy1/main/RogueCastle/src/Game.cs)
— **first-tier, the released source**).

Traits are a **closed enum of 38** (ColorBlind, NearSighted, Dyslexia, Gigantism, Dwarfism, Alzheimers,
Dextrocardia, Tourettes, OCD, Vertigo, TunnelVision, Prosopagnosia, …) with hand-set rarities, and an
heir carries exactly two — `public Vector2 Traits;`
([`TraitType.cs`](https://raw.githubusercontent.com/flibitijibibo/RogueLegacy1/main/RogueCastle/src/Types/TraitType.cs)).

**FACT — Destiny 2 composes nothing.** A weapon's name is `displayProperties.name` on its
`DestinyInventoryItemDefinition`, fixed per item hash; randomisation lives entirely in the socket and
plug definitions
([Bungie API schema](https://bungie-net.github.io/multi/schema_Destiny-Definitions-DestinyInventoryItemDefinition.html)).
**"Randomly-rolled" never touches text.**

**Anti-nonsense mechanism: total authorship.** Nothing is composed, so nothing can read wrong. Worth
stating explicitly, because it is the bar a grammar has to clear to be worth its maintenance cost.

### 12.14 Tracery — the canonical formalism, and the honest limit

**FACT.** A Tracery grammar is a JSON map from symbol to rule array; expansion is recursive `#symbol#`
substitution; variables bind an entity across a whole sentence
(`#[hero:#name#][heroPet:#animal#]story#`); modifiers chain after a dot
([galaxykate/tracery](https://github.com/galaxykate/tracery) — **first-tier, the reference
implementation**).

The complete built-in English modifier set, read from `modifiers.js`: `capitalize`, `capitalizeAll`,
`inQuotes`, `comma` (appends `,` unless already ending in `,.?!`), `a` (an/a by leading vowel), `s`
(…y after vowel → +s; …y after consonant → −y+ies; …x → −x+en; …z → −z+es; …h → −h+es; else +s), `ed`
(…y after consonant → −y+ied; …e → +d; else +ed — **operates on the first word only**, so `#verb.ed#`
works on "pick up the sword"), `beeSpeak`.

**⭐ The design conclusion, and it is the sentence to keep: a replacement grammar guarantees agreement,
not sense.** Two symbols expanded in the same rule are independent draws. **Every mechanism in this
section — DF's per-word slot permissions and culled symbol pools, Qud's theme keys, DCSS's per-class
files and god-compatibility check, D2's affix groups, NMS's biome-keyed pools, Borderlands' authored
per-manufacturer words — exists as a layer on top of the bare model, and each one exists because the
bare model produces *"the Hardy Ring-Cobra of Dashing."***

### 12.15 ⭐ The naming grammar table

| System | Slot order | Fragment source | Anti-nonsense mechanism |
|---|---|---|---|
| **Warframe Riven** | `Prefix-CoreSuffix` | 31 attributes × 3 syllable slots = 93 fragments *(computed)* | Slots assigned by magnitude rank; prefix and suffix pools are disjoint vocabularies |
| **Borderlands 2** | `<Prefix> <Title>`, manufacturer shown separately | name-part objects referenced by functional parts — title ← barrel/body, prefix ← accessory/element/grip; **both persisted into the item serial** | every word authored per (part × manufacturer × weapon type); zero free recombination; a priority rule picks one prefix |
| **Cassette Beasts move** | `<Prefix> <Suffix>` | ~6–8 prefixes per type; 13 suffixes | the suffix **is** the mechanical spec; the prefix is free flavour keyed to the type |
| **Diablo II magic** | `<MagicPrefix> <base> <MagicSuffix>` | the mechanical affixes themselves | 1 prefix + 1 suffix max; `group` exclusion; `iType` whitelist / `eType` blacklist; `frequency` weighting |
| **Diablo II rare** | `<RarePrefix> <RareSuffix>` above the base | separate cosmetic tables, 46 × 155 | both halves `itype`-gated per item class; name fully decoupled from affixes |
| **Path of Exile rare** | `<RareItemPrefix> <RareItemSuffix>` | 305 + 525 word tables | pools partitioned by what is named; adjective-pool × noun-pool is grammatical by construction |
| **Dwarf Fortress** | `[Front][Rear] the [Adj1] [Adj2] [hyphen]-[the Noun] of [of noun]` | 2,196 words, glossed per language | per-word slot permission tags + 84 symbol pools whitelisted/blacklisted per civ **and per name class**, with `SUBSELECT` narrowing |
| **DCSS randart** | weighted choice among ~35 whole templates, expanded recursively | `rand_wpn` / `rand_arm` / `rand_all` / `randbook` + 5 hardcoded symbols | per-class files · 25-char cap with 100 rerolls · **god/item compatibility filter** · blacklist against unique-name collisions · syllable-builder fallback |
| **Caves of Qud relic** | `<theme-stem+acus> <theme-noun+affect-noun>` or `The <adj> <noun> of <site>` | theme noun lists; era-gated adjective/location pools | theme key drives name, lore, mods **and** architecture together; Early/Late eras use disjoint pools |
| **No Man's Sky place** | Markov string + terrain descriptor | 8 Markov corpora; 12-value sector enum | the descriptor pool is a **function of the actual terrain** |
| **No Man's Sky product** | base string with `ReplaceKey` placeholders | 3 parallel pools per placeholder, selected by rarity | 28 per-category tables + rarity-matched pools + per-biome weights |
| **Nemesis** | `<name> the <Title>` | authored name list + authored title list | **the traits are derived from the title**, so the two cannot disagree |
| **Titan Quest / Grim Dawn** | `[prefix] <base> [suffix]` | affix tables scoped per slot **and** per melee/mage archetype | table membership is the whitelist; prefix and suffix live in disjoint trees |
| **Tyranny** | hand-written table lookup | 63–64 authored names on Core × Expression | not a grammar at all — plus a player rename box |
| **Rogue Legacy / Destiny 2** | format string / none | authored lists | total authorship |

**⭐ The five reusable techniques, ranked by cost against benefit:**

1. **Make the word pool a function of the thing being named.** NMS terrain, Qud themes, D2 `itype`, DF
   name class, Cassette Beasts' type-keyed prefixes. **Cheapest, most effective, and it is the technique
   every strong system in this table uses.**
2. **Partition the vocabulary into mutually exclusive groups and take at most one from each** (D2 and
   PoE `group`). Kills the "Ferocious Cruel Axe" class of nonsense outright, with one integer per part.
3. **Author every fragment per context rather than recombining freely** (Borderlands). Most expensive,
   best-reading result — and it still scales, because the *selection* stays generative even though the
   *vocabulary* is not.
4. **Reroll on a quality guard rather than truncating** (DCSS: 25 characters, 100 tries, a named
   fallback). Failure is bounded and visible.
5. **Persist the chosen name fragments as data** (Borderlands `_PrefixPart` / `_TitlePart`). The grammar
   runs once; the name is then stable, hashable and diffable forever.

**And the one technique only DCSS implements: a cross-slot semantic check** — *"no good god is chosen
for evil weapons"*. Everything else in this table checks whether a fragment may occupy a slot; only DCSS
checks whether two chosen fragments contradict each other.

### 12.16 The oatmeal problem — the failure all of these grammars are avoiding

The standard name for it, from Kate Compton's *So you want to build a generator…*
([galaxykate.tumblr, via reader proxy](https://galaxykate0.tumblr.com/post/139774965871/so-you-want-to-build-a-generator)
— **first-tier, the originator of the term**):

> *"I can easily generate 10,000 bowls of plain oatmeal, with each oat being in a different position
> and different orientation, and mathematically speaking they will all be completely unique. But the
> user will likely just see a lot of oatmeal."*

She separates two bars a generator can clear:

- **Perceptual differentiation** — *"the feeling that this piece of content is not identical to the
  last."* The low bar.
- **Perceptual uniqueness** — *"the feeling that each artifact has a distinct personality."* The bar
  that makes an output memorable.

**Every grammar above is a different way to buy perceptual uniqueness cheaply.** Warframe buys it by
making the name a legible summary of the roll. Cassette Beasts buys it by making the name the spec. D2
buys it by pinning the name to a curated word list and refusing to derive it. Nemesis buys it by
deriving the *mechanics* from the name. Qud buys it by making one theme key drive the name, the lore,
the item mods and the architecture together. Crawl buys it by making names pronounceable and therefore
repeatable in conversation. Borderlands buys it by authoring every word.

**And URR (§8.10) names the cheapest purchase of all: subtractive constraint.** Pick one element, then
*remove* from every other category the options that would clash with it, and pick the next from the
reduced pool. One pass, no validation, no rerolls, and the result is coherent by construction.

**The prior pass measured what happens without it.** Cassette Beasts shipped 120 authored monsters and
*"over 14,000 fusions"* built by modular part assembly; its reviewers *"liked the designs"* of the
authored 120 *"but did not feel as positively about the fusions, the majority of which were
procedurally generated"* — see
[`../genre-mechanics/06-summoner-minion-fusion-rpg.md`](../genre-mechanics/06-summoner-minion-fusion-rpg.md)
§2.6, which sourced and computed that comparison. **120 authored designs read well; 14,000 generated
ones did not.**

---

## 13. Where composition systems were removed or cut back after shipping

**The pattern in one line: what gets cut is almost never the *idea* of composing — it is a member of
the vocabulary whose composition rule differs from the rest, or a system whose maintenance cost
outgrew its payoff.**

| System | What was cut | When | Stated reason |
|---|---|---|---|
| **MTG — prowess** | removed from the evergreen keyword set | ~2024 | *"It also was the only triggered evergreen creature keyword, which caused different issues (for example, **it stacked where others didn't**)."* · *"too unlike the other evergreen creature keywords"* |
| **MTG — shroud** | replaced by hexproof as evergreen | 2015 | *"We understood that their opponents couldn't target their creatures but didn't get that they couldn't either"* — shroud *"often kept you from helping out your creature"* |
| **MTG — fear, intimidate** | dropped from evergreen | 2015 | colour-based evasion was swingy; fear additionally *"couldn't be used in other colors (a big issue when we want to be careful how many creature keywords we keep evergreen)"* |
| **MTG — landwalk** | dropped from evergreen | 2015 | it forced an unwinnable deckbuilding choice |
| **MTG — protection** | demoted evergreen → **deciduous** | 2022 | reclassified as *"abilities/mechanics/tools that are not evergreen, but things that R&D has access to whenever they feel a set needs it"* |
| **Hearthstone — Charge** | not removed; **availability restricted**, superseded by the weaker Rush | 2018 | *"restricting [buffs and copy effects] would detract from the game far more significantly than simply restricting the availability of Charge"* |
| **Hearthstone — Discover's pool** | narrowed from *any card* to neutral + class, before ship | dev | an unconstrained pool made the effect *"almost impossible to play around"* and *"produced too much 'class bleed'"* |
| **Hearthstone — Silence** | effectively stopped being printed | post-launch | Ben Brode, Apr 2016: the developers considered Silence effects *"may be undercosted right now"* |
| **Nemesis System** | pared back **twice** — once mid-development, once for last-gen ports; its online half deleted entirely | dev; 2021-01-12 | *"It was made more complex during the game's early development, incorporating personal relationships among Orcs, but was later pared down when the studio considered it **too complicated**"*; *"the Nemesis system was too large for older consoles"* |
| **Diablo III — Kanai's Cube** | shipped deliberately capped at 3 slots, partitioned by category | 2.3.0 | *"Players may have one Weapon, one Armor, and one Jewelry power equipped at a time"* — the cap is the design, not a later cut |
| **Diablo IV — aspect extraction** | **loosened**, not cut: single-use Essence items → permanent Codex entries | Season 4 | *"Salvaging Legendary items stores their powers as Legendary Aspects in your Codex of Power to be reused indefinitely."* |
| **Grim Dawn — devotion bindings** | explicitly **never** tightened after ship | policy | *"since then we've only **reenabled** devotion assignment options, not disabled any. A shotgun penalty for the proc could be considered."* |
| **PoE — trigger supports** | not removed; **repriced** | 3.15 | *"the mana cost of the skill is a mechanism to allow us to have large impactful effects… this entire mechanism is currently bypassed by triggering skills"* |
| **Diablo IV — Overpower's additive coupling** | **removed**, after an 80% coefficient cut failed | 2.3 | *"we tested an 80% reduction in these additive damage values, but still found Overpower based builds to be overperforming"* |

Sources for each row are given in §6, §7 and §12; the Nemesis rows are sourced in
[`../genre-mechanics/06-summoner-minion-fusion-rpg.md`](../genre-mechanics/06-summoner-minion-fusion-rpg.md)
§6.6 and are not re-derived here.

### 13.1 ⭐ Path of Exile 2 — a uniqueness rule shipped, ran nine months, and was withdrawn

**The single most on-point case in this document, because GGG published both the rule and the reason for
reversing it.**

PoE 2 moved sockets off gear and onto the skill gem — *"Each Skill can be modified by up to five Support
Gems which change their behavior in drastic ways"*
([pathofexile2.com](https://pathofexile2.com/)) — with two support slots at base, expandable to five.
And it added a hard uniqueness rule: **one copy of each support gem per character.**

**FACT — GGG's own 0.3.0 patch notes, under "Support Gem System Overhaul"**
([pathofexile.com](https://www.pathofexile.com/forum/view-thread/3826682), Aug 2025 — **first-tier**):

> *"Support gems are an area that got some of the most major changes to the core design. They are what
> enable so many interesting mechanics and different ways to play, but PoE2's support gem system has
> just never been quite right.*
>
> *One of the major issues is the limitation of only having one support gem of each type on your
> character. **We didn't want builds to become just using the same 5 most powerful supports on every
> skill, but we also wanted to encourage you to combo your abilities.***
>
> *We've removed the restriction of having one of each support gem per character. You can use as many
> copies as you like."*

**⭐ And the crucial detail: the restriction was relocated, not abolished.** The same patch did three
things at once:

| Change | Effect |
|---|---|
| Removed the per-character support limit | uniqueness gone from the general vocabulary |
| **Added a new restriction on skill gems** — *"You can no longer socket multiple copies of the same Skill Gem into your main skill sockets"* | uniqueness moved to a different vocabulary |
| Introduced **40 Lineage Supports** — *"much more powerful Supports that are only available in Endgame"* — which **keep** the one-per-character rule (*"Only one copy of each Lineage support gem can be socketed across all your skills. Solus Ipse allows you to socket up to three copies…"*) | uniqueness re-applied to the power tail, with a named escape hatch |

**INFERENCE, and it is the finding.** GGG did not conclude that a uniqueness rule is bad. They concluded
it was **priced wrong when applied flat across the whole vocabulary**, and re-applied it to the ~40
strongest pieces. **Uniqueness cost now scales with the power of the piece.**

**SECOND-TIER** (a shop-site summary of a podcast; no transcript verified) — Jonathan Rogers:
*"The limitation of one support per character was kind of working against our goals"* and *"The problem
of people feeling like they have to all-in on one skill was actually a larger problem than what we were
solving by having that restriction."* Treat as unverified.

**FACT — genuine deletions in the same patch:** *"Blood in the Eyes, Discombobulate, Unsteady Tempo,
Unbating, Unbending, Untouchable, and Unyielding can no longer be engraved in the Gemcutting menu, and
existing Gems will be deleted upon logging in."* Plus two composability narrowings: *"Support Gems which
support skills 'you use yourself' can no longer support Persistent Skills"*, and a bugfix stopping those
supports reaching totem and triggered skills.

### 13.2 Path of Exile 1 — the pattern is *re-price*, almost never *delete*

The folklore overstates the deletions. Checked one by one:

| Gem | What actually happened |
|---|---|
| **Item Quantity Support** | **Genuinely drop-disabled** — 1.1.0, **March 2014**. The only support gem on the wiki's drop-disabled list; existing copies still work and trade |
| **Reduced Mana Support** | **Renamed** in 3.8.0 → Inspiration Support. Same internal id `SupportGemReducedMana` |
| **Chance to Ignite** | **Renamed** 3.3.0 → Combustion Support |
| **Poison Support** | **Renamed and gutted** 3.15.0 → Critical Strike Affliction; lost the poison chance and the `more` multiplier |
| **Blood Magic Support** | **Removed and split**, 3.14.0 → Arrogance + Lifetap; existing copies auto-converted |
| **Earthbreaker Support** | **Deleted, effect moved onto an item**, 3.25.0 |
| **Cast when Damage Taken** | **Never removed — restricted five times.** 1.2.2: *"Gems may now only be supported by a single trigger gem, gems supported by multiple trigger gems will be disabled."* 3.15.0: 250% cost multiplier. 3.19.0: from *6% more* to *27% less* damage at gem 20 |
| **Alt-quality gems** | A whole dimension deleted in 3.23.0: *"Anomalous, Divergent and Phantasmal Quality Gems have been removed from the game."* |
| **Greater / non-Greater pairs** | 3.28.0: *"Greater versions of Supports no longer work alongside their non-Greater version… the Greater one takes priority."* |

**FACT — the oldest surviving design statement, from 0.9.3, is strikingly close to the PoE 2 argument
fifteen years later:** *"Support gems now have a flat multiplier to the mana cost of the skill that does
not go up exponentially as they level up… **Support gems are balanced such that only a few of them are
needed to make a skill feel powerful.**"*

**And PoE 1 is now removing socket colours entirely** (July 2026). Designer Octavian: *"I never really
found socket colors to be all that engaging of a system personally."* Director Mark Roberts: *"We're
100% questioning now things that are sacred."*
([PC Gamer](https://www.pcgamer.com/games/rpg/after-13-years-path-of-exiles-devs-are-finally-ready-to-ditch-one-of-its-most-iconic-and-frustrating-mechanics/)).

### 13.3 ⭐ Archnemesis — a compose-a-monster system removed after shipping, with a full post-mortem

**The closest precedent in the survey to generating an enemy or an action from stacked modifiers, and
GGG wrote down exactly why they pulled it.** All verbatim from the 3.20 manifesto
([pathofexile.com](https://www.pathofexile.com/forum/view-thread/3322245), Nov 2022 — **first-tier**):

> *"The issues that players often had with Archnemesis were:*
> - *The keyworded mod names were **not fully descriptive of what they did***
> - *The mods often had **multiple effects bundled** which made them harder to understand*
> - *Due to how many effects were included in a single mod, it made **too many encounters too complex***
> - *The way Archnemesis rewards were set up meant that many players felt like they couldn't just kill a
>   monster, they had to consider if they wanted to bring a magic find character in to maximise
>   rewards"*

> *"The goals of the new system are: **Mods do one specific thing** · **Mods say what they do rather than
> having a thematic name you must learn and remember** · Encounters are simplified on average while
> retaining interesting fights · Players are no longer required to do annoying actions to maximise
> rewards"*

The decomposition, in their own example:

> *"The Magma Barrier Archnemesis mod did a whole lot of stuff… it converted some of the monster's
> physical damage to fire damage, it added some extra fire damage on top, it granted fire resistance…
> It also spawned volatile flamebloods. **The new equivalent modifier just puts a magma barrier around
> the monster and does nothing else.**"*

**⭐ And the technique that saved the interesting parts — dilution, not deletion:**

> *"The pool of mods that involve complex interaction… **have been heavily diluted by the presence of
> the simpler mods**. This means that you encounter more complex fights less frequently. But interesting
> and challenging emergent behaviour from overlapping mods can still happen, just less often."*

**They changed the distribution, not the vocabulary.**

**Plus a subtle point about legibility as a balance lever, not just UX:** rewards were tied to individual
mods, so players could *read* a monster and derive the payoff — which made a magic-find character swap
mandatory. **GGG's fix was to hide the reward.**

### 13.4 ⭐ Elder Scrolls spell-making — deleted outright, and the replacement tells you why

**FACT** — UESP's own cross-game comparison table: Morrowind, custom spells *"created by paying a
Spellmaker"*; Oblivion, *"created for gold at an Altar of Spellmaking"*; Skyrim, **"Custom spells are not
available."**
([UESP](https://en.uesp.net/wiki/General:Differences_Between_Morrowind,_Oblivion,_and_Skyrim)).

**What replaced it**, same source: *"There are skill perks to reduce the magicka cost of different levels
of spells. Spells must be assigned to one or both hands and can be dual cast for greater effect with the
use of a perk… **Spells never fail to cast due to level or difficulty.**"* Plus enchanting by
disenchanting, capped by Enchanting skill.

**INFERENCE, and the load-bearing observation.** Every replacement moves authorship from *authoring a
priced object* to *selecting from a curated set*. Perks, dual-casting and disenchant-then-reapply are
discrete, hand-priced choices. **Bethesda did not fix the pricing function (§11) — they deleted the
free-parameter space that made one necessary.** Note also that *"spells never fail to cast"* removed
Morrowind's *second* price, in which cost was also a success-chance penalty.

> **⚠ Do not print the Todd Howard "spreadsheety" / "takes the magic out of magic" quote as sourced.**
> It traces to a single interview page that is currently unreachable by every method tried (direct fetch,
> reader proxy, Wayback, archive.today), and the only surviving trace hedges itself. See *What I could
> not find*.

**A verified substitute, with its caveat.** Skyrim's lead designer Bruce Nesmith, March 2026:

> *"I think Todd actually puts it best. He says, 'The idea is for the game to get out of your way.'…
> It's about getting out of the player's way; **don't make it so you're playing a spreadsheet; make it so
> you're in the game doing the cool stuff.** That's been kind of a driving concept since Daggerfall."*
> — [Time Extension](https://www.timeextension.com/news/2026/03/interview-im-the-luckiest-son-of-a-bch-in-the-industry-skyrims-bruce-nesmith-on-tsr-elder-scrolls-and-daggerfalls-miserable-crunch)

**Caveat: this is about character generation and levelling. Nesmith does not mention spell crafting
anywhere in that interview.** Cite it as a studio principle, not as a spellmaking statement.

### 13.5 Diablo III — runestones cut pre-launch, and the reason is a combinatorics argument

**FACT — Jay Wilson, on cutting runestones as items** (beta patch 13, Feb 2012):

> *"Originally, we tied this in to the itemization system because it felt like a good fit, as Diablo is
> all about the item drops. But with around **120 base skills**, that meant there were around **600 rune
> variants**; on top of that, each variant had five quality levels each, meaning ultimately there would
> be something like **3,000 different runes in the game** and we knew we were heading toward a problem."*

> *"Later in the game, having to juggle all of those various runes was not only un-fun, it was a serious
> and tedious inventory problem."*

What replaced it: runes unlock by level; **rune ranks removed entirely** — *"we've instead made each
around the equivalent to what the rank 4 or 5 rune was previously."*

**FACT — skill points and skill trees, cut pre-launch.** Jay Wilson: *"that finite limit of how many
skills you can take versus the number that you have means that you have to make a very restrictive
choice… as opposed to skill points, which are really **commitments before you even know what you're
committing to**."* Bashiok: *"With skill point spending your skills get better as you invest points into
them. **The problem is that this destroys combat depth.**"*
([Blizzplanet's reproduction of the IGN interview](https://blizzplanet.substack.com/p/ign-blizzard-on-ditching-skill-points-in-diablo-iii)).

**FACT — the auction house**, official post by production director John Hight: *"When we initially
designed and implemented the auction houses, the driving goal was to provide a convenient and secure
system for trades… **it ultimately undermines Diablo's core game play: kill monsters to get cool
loot.**"*
([news.blizzard.com](https://news.blizzard.com/en-us/article/10974978/diablo-iii-auction-house-update)).

### 13.6 MMOs — three deletions, three stated reasons, one shape

**⭐ WoW talent trees.** Greg "Ghostcrawler" Street, Dec 2011 — the sharpest combinatorics quote found
anywhere in this survey
([Engadget](https://www.engadget.com/2011-12-08-ghostcrawler-on-seeing-the-forest-for-the-talent-trees.html)):

> *"Look, we tried the talent tree model for seven years. We think it's fundamentally flawed and
> unfixable."*
>
> *"**The problem is the extreme number of combinations. When you have such a gigantic matrix, the
> chances of having unbeatable synergies, or combinations of talents that just don't work together is
> really high. That's not lazy design. That is recognizing how math works.**"*
>
> *"So given that we don't think it's humanly possible to have 40-50 fun, interesting and balanced
> talents in a tree, the alternative is to continue on with bloated trees that have a ton of
> inconsequential talents."*

The replacement deliberately pulls *"away from the math of talents"* — talents grant powers and utilities
rather than percentages, so *"it is very hard to figure which is 'best'… and instead is more about what
the player likes."*

**WoW glyphs, removed in stages** (**second-tier**, wiki.gg): introduced 3.0.2; split into
Prime/Major/Minor in 4.0.1; **Prime glyphs removed entirely in 5.0.4** (*"We're not happy with how Prime
glyphs have worked out"*); exclusive categories added in 6.0.2 to prevent certain simultaneous
combinations; **Major glyphs removed entirely in 7.0.3** along with the glyph interface. **INFERENCE:**
the pure-numbers tier died first and the behaviour tier second, in the same expansion that moved
customisation into Artifact trees. **Relocation, not abolition** — the same move PoE 2 made in §13.1.

**FFXIV — two removals against the same failure.** Cross-class skills were removed in 4.0 and replaced
by **role actions**; then in 5.0 the role-action *selection* was itself removed and the set fixed. The
stated 4.0 problem was too many actions per job, not enough hotbar slots, and cross-class skills forcing
players to level jobs they did not want. **INFERENCE:** both are the same failure — **composition that
becomes obligation** — and both times the fix was to delete the choice rather than balance it.

**Guild Wars 1 → 2 — secondary professions.** Lead designer Eric Flannum: *"The concept of the 'primary'
attribute as it was used in Guild Wars 1 is not something we needed in Guild Wars 2."* With secondaries
gone, primary attributes had nothing left to differentiate. **SECOND-TIER:** the GW2 wiki records that
secondaries existed in early development and were removed *"to allow for more unique customization of
each profession and eliminate the associated balancing issues with multiple professions using the same
skills."*

**Star Wars Galaxies NGE** (**second-tier**): free mix-and-match skill boxes across **34 professions**
collapsed to **9** iconic professions with levels 1–90, attributed to balance tractability.

### 13.7 Looter shooters — the modifier system reworked, and one rework that never shipped

**Destiny 2 removed random rolls, then reinstated them.** **FACT** — Luke Smith, E3 2017: *"There aren't
random rolls on weapons anymore. Better Devils is a Crucible hand cannon, and what it has on it is what
it has on it. Period."* The stated reason was balance controllability: *"If something is off with an
item, the developer can make a change that impacts everyone the same way."* Random rolls returned with
Forsaken in Sept 2018 — **but no verbatim Bungie reinstatement statement could be obtained.**

**⭐ Anthem — the clearest statement of the modifier-composition failure mode.** BioWare studio director
Christian Dailey, 31 July 2020
([blog.bioware.com](https://blog.bioware.com/2020/07/31/anthem-update-loot-equipment-goals/) —
**first-tier**):

> *"A good player experience depends on the loot system being extensible and robust, and a lot can go
> wrong. **A lot did go wrong.** We fell short here and we realized that building something new from the
> ground up was going to be required."*
>
> *"No more useless items because they were missing must-have inscriptions (see 'Increased weapon dmg by
> +225%')"*
>
> *"Each item has an inscription **'budget'**, based on its Power and Rarity"*
>
> *"Exceptional items are about getting the exact types of bonuses you want, instead of maxing values on
> every bonus."*

The rework was cancelled on 24 Feb 2021 and never shipped.

**INFERENCE.** When one modifier is mandatory, the combination space collapses to *"does this roll have
the mandatory thing"* and every other axis becomes decorative. **BioWare's answer was not to remove
composition but to bound it with a budget and make it re-rollable** — converting a lottery into a
currency sink. That is the same answer Warframe already ships (§12.2).

**Marvel's Avengers** (patch 2.2, Nov 2021, via a reproduction — the original blog no longer resolves):
*"The need for Catalysts, Nanotubes, Nanites, Plasma, and Uru are being removed"* and *"A new upgrading
system is being added that will allow players to **infuse** their gear."* **INFERENCE:** the
*"kill the churn, keep the item"* move — loot stops being a re-composition problem and becomes an
investment problem.

### 13.8 Two systems that were *not* cut, and why

**Divinity: Original Sin 2 skill crafting survived Definitive Edition intact.** **FACT** — the rule is
asymmetric: *"combining an elemental and non-elemental skill book will produce a new skill book"* —
elemental-to-elemental does **not** work. Four elemental schools × six non-elemental = **24 base
hybrids**, mirrored by 24 Source-tier. Crafted skills require ability points in **both** parent schools.
What Larian nerfed in Definitive Edition were the **outcome outliers** — the 3-point Source nukes, Lone
Wolf's attribute cap, Overpower — **not the composition rule.**

**Magicka 2's composition survived; the hierarchy was re-tuned.** *(Correction worth recording: Magicka 2
was developed by **Pieces Interactive**, not Arrowhead.)* **FACT — Paradox associate producer Peter
Cornelius**, and it is the most on-point quote in this section:

> *"We've also worked hard to balance the spell system. For instance we put Lighting over Arcane in the
> spell hierarchy… **and we're proud to say there is no one über spell combination.**"*
> — [GamingBolt](https://gamingbolt.com/magicka-2-interview-fans-will-feel-right-at-home)

**INFERENCE.** The success claim is framed as an **absence** — *no one über combination* — not as a count
of options. The balancing burden moved into the **resolution hierarchy** (§3.3) and was tuned post-launch
through a public "Spell Balance Beta" shipped as a separate Steam app.

**Why both survived:** the combination space is **closed and hand-authored**. A 4×6 grid with a named,
designed skill behind every cell is not a generative system; it is 24 pieces of content with a discovery
mechanic attached. Magicka 2's hierarchy is a fixed total order over ten types.

### 13.9 ⭐ The pattern across every removal

**Three outcomes, and which bucket a system lands in is decided by whether its combination space is
closed and hand-authored or open-ended.**

| Outcome | Cases | What the studio actually did |
|---|---|---|
| **Kept the composition, restricted the resolution** | Magicka 2, DOS2 Definitive Edition | reorder the precedence table; nerf outlier *results*, never the rule |
| **Kept the composition, bounded and re-priced it** | Anthem's inscription budget + reroll · PoE 1's standardised 25% / 30-35% / 38-48% bands · PoE 2's relocation of uniqueness onto the 40-gem power tail · Archnemesis' dilution | the failure named is never *"too many combinations"* — it is **"one dominant combination"** |
| **Deleted the composition** | WoW talents and glyphs · GW2 secondaries · FFXIV cross-class then role actions · SWG NGE · D3 skill points and item-runestones · TES spellmaking | see the two stated reasons below |

**Every stated reason for a deletion reduces to one of two claims:**

- **(a) The matrix is too large to balance.** Ghostcrawler's *"That is recognizing how math works"*;
  D3's 120 skills → 600 variants → 3,000 runes; SWG's 34 → 9.
- **(b) The system has a discoverable right answer, so it is obligation rather than choice.** Jay
  Wilson's *"Any system where there's a 'right' answer is not a good system for customization"*
  (**second-tier**, BlizzCon 2010, unverified against a primary source); GGG's *"by far the correct
  choice is just to stack on all the multiplicative damage bonuses"*; FFXIV's forced off-job levelling.

**Three further patterns, all sourced above:**

1. **The non-uniform vocabulary member is what gets cut.** Prowess was neither overpowered nor
   unpopular; it was the one evergreen creature keyword whose *stacking rule differed from the rest*.
   Shroud was the one whose *text meant something different from what players read*. **A composition
   vocabulary pays permanently for any member that does not obey the vocabulary's own rule.**
2. **Studios restrict the narrow half of a bad pair, and restrict *availability* rather than
   *legality*.** Hearthstone said so explicitly. Nobody here removed a broad, load-bearing modifier
   class to fix a combo.
3. **The direction of change after ship is asymmetric.** Legality tables loosen — Crate only re-enables,
   Diablo IV made aspects more reusable, GGG removed the uniqueness rule. Magnitudes tighten. **Removing
   a legal combination invalidates builds players already own, and Crate calls doing so "the nuclear
   option".**

**⭐ And the two techniques worth naming, both from GGG:**

- **Dilution over deletion** (Archnemesis): decompose bundled composites into single-effect atoms, name
  atoms after what they *do* rather than thematically, then **dilute the complex atoms with simple ones**
  so scary overlaps stay rare instead of being removed. The vocabulary survived; the *distribution*
  changed.
- **Legibility is a balance lever, not just UX.** Archnemesis was partly reverted because players could
  read a monster's mods and derive the reward, which made an optimisation mandatory.

**INFERENCE.** The one genuine deletion of a whole *generator* in this document — the Nemesis System's
social layer — was cut for **build and platform cost**, not for balance. That is the honest risk profile
of a rich generator: **it dies of maintenance, not of degeneracy.**

---

## 14. What I could not find

**This section is the point of the pass, not an appendix.** Everything below was looked for and not
found, or was found but could not be verified. Re-running these searches costs the same budget for the
same result. It extends
[`../game-design/06-unsourced.md`](../game-design/06-unsourced.md), which should be read alongside it.

### 14.1 Numbers that could not be verified — do not quote these

| Claim | Status |
|---|---|
| **Last Epoch's whole-game node statistics** — ~3,700 nodes / 128 trees, "87.5% of nodes contain no behaviour verb", "~99 conversion nodes", "28 of 3,694 exclusivity nodes", the prerequisite-edge and point-cap distributions | Reported during this pass and **withdrawn on re-check**. Only *~29 nodes per tree*, computed from the one official Mage page (352 nodes / ~12 trees), survives |
| **Last Epoch engine field names** `lockNode`, `baseLockNode`, `mastery`, `masteryRequirement` | Withdrawn — could not be re-verified against a source |
| **Last Epoch node texts** for Warpath/Apocalypse Whirl, Upheaval/Glacial Cascade, Swipe/Umjol's Guidance, the Volatile Reversal set, Snap Freeze/Permafrost, Frost Claw nodes, Decoy/Remote Detonator, Summon Skeleton, Detonating Arrow/Arcing Blast | Withdrawn. Only Focal Blast and Plasma Ball were re-fetched from the official Mage tree page |
| **`magicprefix.json` entry count** | Two counts of the same file in this pass gave **337** and **358**. The shipped `.txt` has 671 lines including blanks. Treat as ~340–360 |
| **PoE `active_skill_types.json` = ~180 entries** and **`gem_tags.json` = 58** | Computed by parsing the published files in this pass; not official figures. The 58 is a full verbatim list and is solid; the 180 is a count only |
| **PoE 1 gem totals — 273 active / 177 support** | From an undated wiki page reached via search indexing only; Fandom is HTTP 402 here |
| **Magicka combinatorics — 1,286 / 584 / 2,336 / 8,084** | All **computed in this pass** from the published rules. Not sourced, and not a developer statement |
| **Tyranny's 63–64 legal Core × Expression pairs** | Computed by summing the wiki's own compatibility lists; the two counting methods differ by one |

### 14.2 Genuine absences — nobody has published these

- **⛔ The Todd Howard "spreadsheety" / "takes the magic out of magic" quote on Skyrim's spell-making.**
  It traces to one interview page that is currently unreachable by direct fetch, reader proxy, Wayback
  (HTTP 429 throughout) and archive.today (CAPTCHA). The only surviving trace hedges itself. **Do not
  print it as sourced.** Bruce Nesmith's *"don't make it so you're playing a spreadsheet"* is verifiable
  but is about character generation, not spellmaking — he does not mention spell crafting at all.
- **⛔ No Blizzard page publishes the Diablo IV Aspect category → slot table**, or the amulet ×1.5 /
  two-hander ×2 multipliers. It appears to be in-game documentation only. Three Maxroll pages disagree on
  the Shield/Off-hand and Resource cells and the conflict is unresolved.
- **⛔ No Blizzard statement that duplicate aspects do not stack**, or that Uniques cannot be imprinted.
  Both are community-observed only.
- **⛔ No forbidden-keyword-combination rule exists in Magic's Comprehensive Rules.** This is a
  **confident negative**, established by grepping the official rules file rather than by absence of
  search results.
- **⛔ No published Wizards templating document stating a per-card keyword cap.** The closest is
  Rosewater on Blogatog — *"Three's usually the top limit"* at uncommon — which is a designer's public
  Q&A, not a document. Asked about keyword *print order*, he answered *"The editors have a order they
  use. I'm not sure exactly what it's based on."*
- **⛔ No Blizzard statement of any forbidden Hearthstone keyword combination.** Their published position
  is that Charge was restricted in *availability* and superseded by a weaker keyword.
- **⛔ No Nolla Games statement on the design of Noita's wand composition rules.** Purho's GDC 2019 talk
  is about the falling-sand engine.
- **⛔ No Arrowhead interview or talk on Magicka's combination rules** — the opposition table, the
  precedence ladder, why five slots. The postmortem covers motivation and rejected advice, not
  mechanics. No GDC talk on Magicka's spell system was found.
- **⛔ The Magicka "over 1000 spells" figure cannot be traced to Arrowhead or Paradox.** The official
  Steam page carries no spell count at all. The figure appears only in a fan guide and a user-edited
  wiki. Magicka 2's official copy says *"thousands of spells"*, qualitatively.
- **⛔ No Obsidian commentary on the *problems* of Tyranny's sigil system**, and nothing at all from
  Matt MacLean or Josh Sawyer about it. Only Brian Heins' pre-release interviews. There is no Tyranny
  postmortem.
- **⛔ No quoted developer rationale for the DCSS 0.27 removals.** The changelog states the changes but
  not the reasons; §5.6's causal reading is against the published philosophy document, not a quote.
- **⛔ No Toady One commentary on constraining the Dwarf Fortress generator against absurd results.**
  The only direct quotes surfaced are on syndrome concentration.
- **⛔ No primary Gearbox technical account of the weapon generator.** The SXSW 2010 coverage gives the
  gun count and the design intent; the Game Informer BL4 feature body is unreachable. **What fraction of
  Borderlands' combinatorial space is legal is unknown.**
- **⛔ The syllable-grammar hypothesis for orc names appears to be FALSE.** The patent describes drawing
  *"one name and one title… from a list of possible names and titles"* — finite authored lists, not a
  morphology. Everything returned by searching for a syllable generator was third-party fan tooling.
- **⛔ No verbatim Bungie statement on reinstating Destiny 2 random rolls in Forsaken.** The
  contemporaneous articles are paraphrased stream recaps.
- **⛔ No first-party GGG reason for moving support sockets from gear onto skill gems.** The mechanical
  fact is GGG's; the rationale is a journalist's framing of the ExileCon 2023 keynote, and no transcript
  exists.
- **⛔ No manifesto for PoE's Item Quantity Support removal or the Blood Magic split** — patch notes only.

### 14.3 Access blocks — the material may exist but was unreachable

| Blocked | Effect |
|---|---|
| **fandom.com — HTTP 402/403 throughout** | Forced fallbacks for Tyranny, Borderlands, Hades, Magicka, PoE, Shadow of War, Diablo. A reader proxy recovered some of it |
| **`wiki.gg` — HTTP 401 on several wikis** (Hades, Tyranny, Cassette Beasts, Borderlands, Diablo) | Noita's wiki.gg worked; the others did not |
| **`poewiki.net` / `poe2wiki.net` — Anubis challenge page** | **Note for future passes: these clear on a second navigation in a real browser. The block was a fetcher limitation, not a site policy** |
| **`web.archive.org` — HTTP 429 throughout** | The Todd Howard quote and several dead Blizzard posts stayed unreachable |
| **UESP — Cloudflare / CAPTCHA on direct fetch** | Recovered indirectly; the formulas in §11 come from a pass that reached them |
| **`wiki.warframe.com` — HTTP 403 to every method** | Recovered through a reader proxy |
| **GDC Vault — video-only, no transcripts** | Chris Hoge's Nemesis talk, Grinblat & Bucklew's Qud talk, and the Qud slide PDF (exceeded the fetch size limit) were all unopened |
| **`tyranny.paradoxwikis.com`, `tyranny.fextralife.com`, `avengers.crystald.com`, `diablowiki.net`** | Dead, 403, or non-resolving |
| **WebSearch budget (200 calls) exhausted mid-pass** | Later topics were reached by direct fetch only, which biased coverage toward sites with guessable URLs |

**Reusable access notes:** `https://r.jina.ai/<url>` worked as a reader proxy for several
Cloudflare-blocked and 402'd sites. The Caves of Qud wiki blocks normal fetches but **serves raw wikitext
fine via `index.php?title=X&action=raw`**. GitHub raw URLs and the GitHub contents API worked
consistently and were the source of every first-tier code and data citation in this document.

### 14.4 Not researched at all — named so the gap is not mistaken for an absence

The search budget ran out before these. Listed in rough order of expected value:

1. **City of Heroes' Enhancement Diversification (Issue 6, 2005)** — six-slotting hard-capped by
   retroactive diminishing returns applied to a shipped composition system. **The strongest untouched
   candidate for §13.**
2. **Monster Hunter Stories 1** entirely — gene taxonomy, board size, and how its rules differ from
   MHS2. The Capcom manual found is MHST2-only.
3. **How a fused Cassette Beasts monster's *name* is produced.** The move grammar is fully documented;
   the creature name is not, on the wiki or in any reachable Bytten Studio post. Part 3 of the modding
   guide, which the wiki says covers the fusion palette derivation, **has not been written**.
4. **Whether PoE's `RareItemPrefix`/`RareItemSuffix` are gated per item class.** PoEDB shows no tag
   column; the community index is organised by item class, which hints at gating.
5. **Diablo III / IV rare-item naming** — whether rares draw from a word list or from affixes.
6. **Dungeons of Dredmor's XML content schema**, Unexplored / Unexplored 2 (Joris Dormans' cyclic
   generation), Barony, RimWorld traits.
7. **Grim Dawn's literal display-token order**, and Torchlight's affix file format.
8. **Wildermyth's official event/story modding reference** — the theme legality rules came from a wiki
   guide quoting the JSON, not from the schema itself.
9. **Whether Fangs of Asterkarn changed Grim Dawn's devotion point cap** — the official guide still
   states 50/55 and has no expansion page.
10. **A Larian statement on why Baldur's Gate 3 has no DOS2-style skill crafting.**

### 14.5 Live conflicts, unresolved

| Conflict | Status |
|---|---|
| **Tyranny — Channeled Strength Lore cost** | Wiki says **35**; Player.One's spell list says **40**. Every other Expression cell agrees exactly. Unresolved |
| **Magicka 1 — is Poison an element?** | Wikipedia implies Water+Arcane→Poison in Magicka 1; the player documentation says Poison was *"hacked into the game"* and the exploit was removed, and Magicka 2's own store page calls Poison *"brand new"*. **Treat Wikipedia as unreliable here** |
| **Magicka — the delivery precedence ladder** | Wikipedia gives six classes; the fan guide gives four plus Shield. They reconcile if Wikipedia's "projectiles (earth and ice)" merges Projectile and Shard, but neither is first-party |
| **Diablo IV Aspect slot table** | Three Maxroll pages disagree on Shield/Off-hand and Resource. The most recently maintained one is quoted |
| **`magicprefix.json` count** | 337 vs 358 (§14.1) |
