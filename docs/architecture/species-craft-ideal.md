# Species-aware crafting — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.

**Program:** `species-craft`. Map (when approved) → `docs/architecture/species-craft-map.md`. **Not a new
program in the sense `gameplay-tiers-ideal.md:156` closed** — the work lands in the **item** program
(modules 13 `set-charm-gen`, 14 `salvage-craft`) and **creature-seed**. Sibling:
[gear-climb-ideal.md](gear-climb-ideal.md) owns the *verb* half (promotion, upgrade); this doc owns the
*species* half. Origin: `tier-system-ideal.md` D2.

## ⭐ DECIDED — a species-bound set piece is enhanced with its own species' material

**Owner, 2026-09-13:** *"if we cannot ship D2, we cannot enhance specie-specific set items… this repo is
never decide for this feature, i ask for you decide it not refuse my idea."*

**Decision: yes. A set bound to a species requires that species' material to enhance.** Recorded as a
decision because **this repo has never decided it**, and the absence of a decision was being read as a
decision against.

⚠ **A category error is recorded here so it is not repeated.** An audit was run against the owner's claim
and reported it "disproved" on the grounds that set pieces craft perfectly well with the ordinary closed
27-id vocabulary today. **That answered the wrong question.** The claim was *normative* — species sets
**should** need species materials — and was tested as *empirical* — do they currently? The current
behaviour is precisely what the proposal changes, so observing that an undecided system does nothing is
not evidence that it should continue doing nothing. **The audit's facts stand and are kept below; its
verdict does not.**

**Why the decision goes this way:**
- **844 species-themed sets already exist.** Without a species hook at the bench they are reskins: the
  species is a name on a card, not a thing the player did.
- **It is the loop the product vision already names.** `the-loops.md` describes Place 2 as
  *"Monster Hunter–style… materials for actors and empire"* — species-bound materials are that loop's
  engine, not an embellishment on it.
- **It answers "which set should I chase?" with gameplay instead of aesthetics.** Today that question has
  only a cosmetic answer.
- **The objections are all sequencing, not design.** The closed vocabulary, the id count and species
  reachability are costs and an order of operations — none of them is an argument that species-bound
  crafting is the wrong design. The one rule that looked like a design objection, the
  source-tagging ban (`ssot-materials-crafting.md:708`), **does not bite**: it forbids ids keyed on *which
  client produced them* because that lets a closed game mode gate content (SC8). A species key is *what you
  hunted*, reachable from several modes, and gates nothing.

**What follows from the decision** is the ordering in § The shape: species selection first — because MH's
own documented fix for species-bound gear whose species is unavailable was to **make the species farmable**,
not to invent a substitute — then the declared species field, then the materials themselves.

---

## Which loop this extends

**Spine C — item collection and progression** (`the-loops.md`): *"find, vault, equip, compare, craft,
socket, salvage."* Also **Spine B** (creature summon and fusion) at the seam where a creature's identity
reaches an item. No new loop, no fourth stock, no new clock.

---

## Load-bearing principles, restated inline

- **Every RPG feature lives in the RPG layer — never by changing what PvZ is.** Materials, sets and craft
  verbs resolve wholly in `FusionRpg.Core`/`FusionRpg.Data`.
- **Rarity never touches a magnitude** (`ssot-rarity.md` §3.6) — a rung sets a count band and tier window.
- **One power ladder.** Any cost ladder owes a `ssot-power-scale.md` §10 row; never a private `f(species)`.
- **The balance surface is data**, each tunable carrying its unit; a missing value is a load rejection
  naming it, never a silent default.
- **Seed → concrete → per-player.** Seedsmith emits enum-only seeds; deterministic code writes magnitudes.
- **A guardrail validates the contract and closed enums — never a derived population count.** Every corpus
  size below is a **reading that grows as content ships**.
- ⛔ **The material vocabulary is closed at 27 by design, and `MaterialCatalog.ClassOf` throws outside it.**
  `ssot-materials-crafting.md:708` additionally forbids **source-tagged** ids. Widening this is the single
  most expensive thing this document contemplates, and §"The shape" argues it is not required.

---

## What this is

In the player's language: **you have 844 sets named after specific creatures, and the creature never comes
up again.** "Celestial Descent" is the Abyss Swordstar's set; upgrading it costs the same anonymous
substrate and shards as upgrading anything else. The species is a name on a card, not a thing you did.

In system language: the species→item link **exists as a string-naming convention and not as data**, so
nothing can ever mechanically depend on it — not a recipe, not a cost, not a filter, not a UI query. Making
that link real is small, independently shippable, and needs no new material.

---

## What already exists

Verified against code and content 2026-09-13. **Counts are readings, not constants.**

### Built

- **The set corpus is large and overwhelmingly species-themed.** **885 files, 910 entries, 910 distinct
  ids.** `themeKey` namespaces split **`creature.*` = 844**, `build.*` = 36, `theme.*` = 30. A real entry:
  `set.abyssswordstar-001` "Celestial Descent", `themeKey: "creature.abyssswordstar"` — which resolves into
  `data/seed/creatures/_registry/themes.v1.json`, where that key carries the real `speciesId`. **844 of 884
  distinct themeKeys resolve into the creature registry.**
- **The set evaluator is built and class-agnostic.** `Items/Thresholds/SetEvaluator.cs:44` `Hits`, `:71`
  `Consumer` (breakpoints → container ids), `:113` `Progress`, scope-guarded at `:91`; persisted via
  `RpgStore.ItemSets.cs` and surfaced through `SetDisclosure.cs`. It reads `SetDef.Members`/`SetDef.Tiers`,
  so **a 10- or 15-role two-threshold set would need no evaluator change** — exactly as `decisions.md:132`
  claims.
- **Set pieces are craftable today, with no special handling.** Every verb — forge, upcycle, elevate,
  temper, reroll-one, reroll-all, bore, socket, salvage — treats a set piece **identically** and prices it
  identically. `SalvagePolicy.cs:71-79` returns `shard.{rung−1}` with no set-awareness.
- **Exactly one set-aware rule exists in the whole craft stack.** `grep -rn "IsSetPiece" src/` returns
  **four lines**: the flag on `SocketModel.cs:105`, its population at `RpgStore.ItemCard.cs:405` (so it is
  built, not inert), and `SetExclusivityValidator.cs:33`, which suppresses **Strain/Splice combination
  bonuses** on a set piece (D21). Socketing itself is never refused — `:41` `MaySocket(...) => true`.
- **The material vocabulary is 27**, built as a cross-product of closed enums
  (`MaterialCatalog.cs:70-88`): 10 shards + 8 substrates (2 frames × 4 grades) + 6 essences + 3 catalysts;
  the seed file carries **31** = 27 + 4 legacy shard ids. **Set pieces draw from exactly this set.**
- **The recipe corpus is 67 rows** using **18 distinct materials**, all from the closed vocabulary.

### Wiring gap

- ⛔ **The `unique-species` set class is decision-only.** `decisions.md:132` and `ssot-sets.md:178` define
  parameterized **ten-role and fifteen-role** templates with **exactly two thresholds**. The shipped corpus
  predates it: member counts are **4 × 894, 6 × 2, 8 × 12, 12 × 2** — **no 10- or 15-role set exists** —
  and threshold counts are **2 × 884, 3 × 26**. The mechanism is built and would consume such a set
  unchanged; **only the content and the declaring field are missing.**
- **`spec-set-charm-gen.md:93` promises "1 set per species"** at roughly 904; content stands at **844
  creature-themed sets**. A content reading, not a defect — but the gap is real.
- **The craft verb surface is half-wired**, and that half is [gear-climb-ideal.md](gear-climb-ideal.md)'s:
  **6 verbs playable, 10 priced, 13 distinct across three vocabularies**, with `forge`, `forge-gem`,
  `elevate` and `reroll-*` priced-but-inert and **17 authored recipe rows a player can never spend**.

### Real gap

- ⭐ **No set entry declares its species.** The field union across all 910 entries is `id, nameKey, name,
  themeKey, members, thresholds, flavor, tags, notes` (+`iconKey`/`flavorKey` on 6). **`speciesId`,
  `declaredSpeciesId` and `setClass` occur zero times in `data/seed/` or `src/`.** Species identity lives
  only inside the `themeKey` string and the id slug (`spec-set-charm-gen.md:263`). **This is the finding:
  the species↔set link is a naming convention, so nothing can query, gate, price or filter on it.**
- **No species-keyed material exists, and policy currently forbids the primitive.**
  `ssot-materials-crafting.md:708` — *"no material id may be source-tagged."* The nearest existing
  creature-derived material is `shard.{rarity}` (`CreatureMaterialCatalog.cs:25`), which is a **rarity**
  axis, never a species one.
- **No recipe targets a set at all** — zero of 67 rows output or gate a set piece.
- **Set transmutation is explicitly undesigned** — `ssot-sets.md:829`: *"'Convert a rare into a set piece' …
  deliberately not designed here."* And `ssot-materials-crafting.md:713`: *"forged bases are never set bases
  (sets are drop-only) and set pieces salvage normally."*
- **"Species-appropriate crafting" exists only as aspiration.** `crafting-coverage-engine.md:285` requires
  *"a valid acquisition/material path"* and names no species material; its four tier axes carry **no species
  axis**.

---

## Prior art

Numbers and documented failure modes, sources inline. Blocked domains are named rather than guessed around.

### Species-bound gear — the wall, and the three shipped answers

**Monster Hunter is the reference, and it documents our exact failure mode.** A Nergigante Gem drops at
**2% carve / 3% tail / 3% horn break / 6% silver investigation / 13% gold**, and **31 copies** are needed
for everything gated on it — 14 weapons + 4 armor pieces + 12 charms, one each
([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Nergigante+Gem)).

- ⛔ **The genuine hard wall is species *unavailability*, not drop rate.** MH event-quest-exclusive gear is
  craftable and upgradeable **only while its quest is live**; miss it and you wait for a rerun. Capcom's
  documented fix was **not** a substitute material — **Title Update 3 made event quests permanent or
  weekly-rotating specifically so upgrade materials stay farmable**
  ([Game8](https://game8.co/games/Monster-Hunter-Wilds/archives/500845)).
- ⚠ **A trade-in shop does not rescue an unreachable species.** The Elder Melder looks like a wildcard and
  is not — player-reported, **you must already own one of that gem before you can meld it**
  ([Steam](https://steamcommunity.com/app/582010/discussions/0/1745594817446358657/)). It unblocks
  *repeats*, never the *first copy*.
- ✅ **The true wildcard is a species-agnostic parallel path.** Wilds' **Artian** weapons are built from
  shards dropped by **any** Tempered monster, assembled from random parts, with **HR100** unlocking a
  chosen part instead of a roll ([PC Gamer](https://www.pcgamer.com/games/action/monster-hunter-wilds-artian-weapon-crafting/)).
- ✅ **Terraria states the principle most cleanly:** boss-specific gear is fine because the summoning items
  are craftable — **no substitute material is needed, because the encounter is the renewable resource**
  ([Terraria Wiki](https://terraria.wiki.gg/wiki/Mechanical_bosses)).
- **Horizon Zero Dawn** keeps machine-bound parts but makes them **purchasable** (Ravager Heart, 840)
  ([Horizon Wiki](https://horizon.fandom.com/wiki/Ravager_Heart)).
- **Dauntless replaced the system rather than patching it** — *Reforged* (1.5.0) swapped incremental
  behemoth-part upgrades for a tier system; player-reported fallout included losing farmed, levelled
  weapons ([TheSixthAxis](https://www.thesixthaxis.com/2020/12/03/dauntless-reforged-update-rework-patch-notes/)).
  ⚠ The official 1.5.0 patch notes now 404 — per-material numbers **UNVERIFIED**.

### Crafting against set items

- **Diablo 2 forbids runewords in set items** — only normal/superior grey bases convert; the stated reason
  is that a set piece would otherwise carry its set affixes *and* a runeword
  ([Diablo Wiki](https://diablo.fandom.com/wiki/Rune_Words)). **Our shipped D21 rule is the same shape.**
- **D2 caps set/unique/rare at exactly one socket** (Larzuk), where a grey base gets the base maximum
  ([Almar's Guides](https://almarsguides.com/Computer/Games/Diablo2/Quests/PermanentRewards/LarzukAddSocket/)) —
  set pieces get *fewer* sockets, not more.
- **D2 does allow set base upgrades** via Horadric Cube (D2R 2.4), preserving sockets, fillers and ethereal
  status ([Maxroll](https://maxroll.gg/d2/resources/horadric-cube-recipes)); it allows **no** set-affix reroll.
- **D3 allows augmenting set pieces** — Caldesann's Despair grants **gem rank × 5 main stat** (rank 110 →
  **550**), one augment per item, re-augmenting overwrites
  ([Maxroll](https://maxroll.gg/d3/resources/a-deep-dive-into-augments)).
- ⭐ **Monster Hunter keeps upgrade level and set membership orthogonal.** Pieces upgrade **independently**,
  and **upgrading never breaks a set bonus**, because the bonus keys off *series membership*, not upgrade
  level; Alpha/Beta/Gamma variants all count ([Game8](https://game8.co/games/Monster-Hunter-World/archives/310128)).
  Two axes: skill points (piece-agnostic) and series count (piece-bound).
- ⚠ **The cautionary tale.** D3's endgame became *"6-piece set item dominance"* — the non-set alternative,
  Legacy of Nightmares, was buffed from **100% → 750%** damage per ancient item to compete
  ([Inven Global](https://www.invenglobal.com/articles/7534/diablo-iii-legacy-of-nightmares-what-to-go-for-each-class)) —
  and **Diablo IV shipped with no set items at all**, its director citing *"the overwhelming convenience of
  set items"* ([GameRant](https://gamerant.com/diablo-4-item-sets-launch/)).
- **PoE has effectively no sets, and no GGG statement explains why** — the canonical forum thread drew
  **zero staff replies** ([PoE forum](https://www.pathofexile.com/forum/view-thread/317708)). Any "GGG said"
  claim is **UNVERIFIED**.

---

## The shape

**Three slices, strictly ordered by dependency, and only the third needs anything new in the economy.**

### 1. Declare the species — DECIDED 2026-09-13: a real field, written by deterministic code

**Owner:** *"real field, use deterministic engine to repair it, and extend seedsmith generator — this
should be decided by deterministic engine, LLM shouldn't."*

A `speciesId` (and `setClass`) field on the set entry, with `themeKey` remaining the presentation key. Two
halves, and **neither involves a model call**:

- **Forward:** extend `set-charm-gen` to *emit* the field.
- **Backward:** a **deterministic repair pass** over the existing 910 entries. The species is already
  present in the data — `themeKey: "creature.abyssswordstar"` resolves into the creature theme registry —
  so the repair *extracts* an existing fact rather than inventing one. No authoring, no ambiguity, no model.

⛔ **The LLM must not decide this, and the repo already enforces why.** Species identity is a **join**, not
a judgement: it is derivable by code, and the binding principle is that the model writes identity
(names, flavour) while deterministic code writes everything a system keys on. A model-authored `speciesId`
would be an unverifiable guess at a fact the corpus already contains — and `audit_schema` exists precisely
to keep model output out of fields like this.

**It needs no new material, no new verb, and no evaluator change** — `SetEvaluator` is already
class-agnostic. It is a schema + generator change in module 13, a deterministic repair, and a regeneration.

⚠ **Two things the repair must handle honestly:** the ~40 non-`creature.*` themeKeys (`build.*` 36,
`theme.*` 30) have **no species** and must write an explicit absent value, never a guess; and **844 of 884**
distinct themeKeys resolve today, so the repair must **refuse and report** the remainder rather than
silently dropping them.

### 2. Cost shaping — DECIDED 2026-09-13: key on the species' rarity rung, **gated above a threshold rung**

**Owner:** *"yes, and gate it above a rung."*

A set piece's craft cost keys on its declared species' **rarity rung** — an axis that already exists, since
`shard.{rarity}` is minted today — **but only above a tunable threshold rung.** Below it, crafting stays
generic.

**This is the MH shape, and the gating is what makes it so.** In MH the rare per-monster part gates the
*top* of a tree while common and generic parts carry the early steps — which is exactly what keeps low-tier
creatures relevant. Applying a species cost at every rung would instead make the first upgrade of every
piece a species errand, which is the "every recipe becomes a travel itinerary" failure
`ssot-materials-crafting.md` §3.4 already refused for zone-keyed ids.

It also makes the species legible at the bench **before** species materials exist, and it widens no
vocabulary — so it is shippable on slice 1 alone.

MH's orthogonality rule still binds: **upgrade level and set membership stay independent** — no craft verb
may break a set bonus, because the bonus counts *membership*, not upgrade state.

**And the one shipped set rule deserves a second look.** D21 suppresses Strain/Splice on set pieces exactly
as D2 forbade runewords in set items — genre-confirmed. D2 *also* capped set pieces at one socket rather
than the base maximum; whether we want that second restriction is a real question, not an oversight.

### 3. Species materials — **decided, and the point of the whole feature**

This is where the decision lands. A species-bound set piece's enhancement spends that species' material.
Slices 1 and 2 are the scaffolding that makes it expressible and legible; **this slice is the reason they
are worth building.**

It carries two real costs, and they are costs rather than objections: the closed 27-id vocabulary widens
(a reviewed change against `ssot-materials-crafting.md` §3.1, and `CostClassMatrix.Allows` must admit a
sixth class rather than throwing), and the player must be able to reach the species.

**The prior art settles the order and the escape valve.** MH's documented fix for species-bound gear whose
species is unavailable was to **make the species farmable** — not to invent a substitute; Terraria's boss
gear works because *the encounter is the renewable resource*; and a trade-in shop demonstrably does **not**
solve the first-copy problem (the Elder Melder requires you to already own one). So this slice follows
species selection, and the documented safety valve if the tail still bites is the **Artian shape** — a
species-agnostic parallel path to an equivalent item — never a wildcard material that quietly dissolves the
binding.

**Scope discipline, from the owner's own arithmetic:** MH affords ~10 materials per monster at ~100
monsters; at 904 species that ratio is impossible. So the general and family layers carry the volume and
the species layer stays deliberately thin — **1–2 per species, tunable**, settable to 1 or to 0 for general
creatures. That keeps the id count bounded against MH's own documented sprawl (`itemData` IDs 0–2315).

### ⭐ Socket caps by item kind — DECIDED 2026-09-13: a tunable table, not a single rule

**Owner:** *"add new cap for them, tunable — so we have multiple socket cap for each type of item. This
reversed for unique item… (boss item)."*

The decision generalises past the question asked. Rather than "do set pieces get fewer sockets," the socket
allowance becomes **a per-item-kind cap table in tuning**, sitting *beneath* the per-role ceiling that
already exists (`sockets.v1.json:5-21`, 15 role rows, parser-checked):

| Item kind | Allowance | Rationale |
|---|---|---|
| **Ordinary** | the base's own maximum | unchanged — today's behaviour |
| **Set piece** | **reduced, tunable — not capped at 1** | the trade for a set bonus. D2 capped set/unique/rare at exactly one socket via Larzuk, which is genre-proven but devalues 910 shipped sets; a tunable reduction keeps some socket play |
| **Unique / boss item** | ⭐ **reversed — *more* than the base maximum** | these are the trophies of world events and boss raids; extra sockets are what distinguishes them, rather than a bigger number |

**Why a table rather than a rule.** A single "set pieces get fewer" rule cannot express the inversion, and
the inversion is the interesting half: the same mechanism that makes a set piece a *commitment* makes a
boss item a *prize*. One tunable table says both, and adding a fourth item kind later costs a row.

⚠ **This interacts with a structural ceiling that has not moved.** `sockets.v1.json:23`
`structuralCeiling: 4` is mirrored by `SocketTuning.cs:41` and a boot throw at `:148-152`, so a
*"more than the base maximum"* allowance cannot exceed 4 until the decided-but-unapplied eight-socket
topology (`decisions.md:133`) lands. **Named so the inversion is not specced against headroom that does
not exist yet.**

### Reserved, not designed — three item kinds

**Owner:** *"mention unique item and boss item, void item — we will defer them, not design or ship in this
program, reserved for the future program."*

| Kind | Source named by the owner | Status |
|---|---|---|
| **Unique item** | world event | **Reserved.** ⚠ Note item **module 17 `uniques`** already exists (G1, *"hand-authored items that break generator rules"*) — so this is plausibly a **new acquisition route onto an existing kind**, not a new kind. Worth reconciling before either is specced |
| **Boss item** | a 4-party boss raid | **Reserved.** No raid content type exists today |
| **Void item** | ⭐ **Confirmed 2026-09-13: dropped by the void beast**, via a **void raid** — the void deploying beasts to siege a player-held sector. Owner: *"they need a new program for void raid… they have no empire."* See [species-selection-ideal.md](species-selection-ideal.md) § Introduced, not designed | **Reserved for its own program.** No design here. Note the raid is a *defensive* acquisition route — the content comes to you — which makes it the only reserved kind whose source is not somewhere the player travels |

**None of the three is designed, specced or shipped by this program.** They are recorded because the socket
cap table above must leave room for them, and because naming a kind early is cheaper than discovering it
inside a spec.

### Alternatives rejected, with reasons

- ~~**Species-keyed materials as the way to make sets species-aware.**~~ **Not rejected — decided (above).**
  An earlier draft demoted this to an optional third slice on the grounds that a declared species field
  delivers "the same legibility" more cheaply. It does not: a field makes the binding *queryable*, while
  the material is what makes it *earned*. Slice 1 is the prerequisite, not the substitute.
- **A trade-in shop to cover unreachable species.** Rejected on MH's own evidence — the melder requires you
  to already own one.
- **Set transmutation** ("convert a rare into a set piece"). Out — `ssot-sets.md:829` deliberately
  undesigned, and nothing here needs it.
- **Letting a craft verb break or re-check a set bonus.** Rejected on MH's orthogonality and on our own
  evaluator design, which counts membership, not upgrade state.
- **Leaning harder on sets generally.** Noted against D3's documented set-dominance spiral and D4 shipping
  without sets — a reason to keep set bonuses modest, not to expand them.

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| Per-species-rung craft-cost multiplier (`speciesCostMultiplierMilli`) | Slice 2's lever — shapes a set piece's cost by its species' rarity rung, using material ids that already exist | `data/tuning/materials.v{n}.json` beside `costBandMultiplierPerMille` |
| `setClass` templates — member-role count and threshold list per class | `decisions.md:132`'s ten/fifteen-role shapes, as data rather than a generator heuristic | `data/tuning/` set-planning file (owned by module 13) |
| **Socket allowance per item kind** (`socketAllowanceByKind`) — ordinary / set / unique+boss | The decided cap table: set pieces reduced, unique and boss items **increased**. Bounded above by `structuralCeiling` (4 today) until the eight-socket topology lands | `data/tuning/sockets.v{n}.json`, beneath the existing 15-row per-role ceiling |
| **Species material count per species (1–2)**, and general/family volume | Slice 3's thin top layer and the two layers that carry the bulk. Settable to 1, or 0 for general creatures — the dial that bounds the id count | `data/tuning/creature-yield.v{n}.json` (**does not exist today**) |
| Which craft verbs demand a species material, and at what share of the cost | Whether the species leg is required on every enhance or only above a rung | `data/tuning/materials.v{n}.json` `operations` |

**Structural (stays `const`, with a comment):** the closed 27-id material vocabulary and the five spend
classes — widening either is a reviewed change against `ssot-materials-crafting.md` §3.1/§3.4, not a tunable.

---

## What this deliberately does not decide

- **Whether species materials ship** — that is **decided** (above), not open. What is open is their
  *shape*: how many per species within the 1–2 band, and whether general creatures get zero.
- **The `unique-species` template's exact role list** — `decisions.md:132` fixes ten/fifteen and two
  thresholds; which roles fill them is content.
- **The craft verb surface** — 4 priced-but-inert verbs and 17 stranded recipes belong to
  [gear-climb-ideal.md](gear-climb-ideal.md), not here.
- **Any FE surface** — an armoury filter by species belongs to `item-surfaces` (module 20).
- **Set bonus magnitudes** — `ssot-sets.md` owns them, and D3's dominance spiral is the reason not to touch
  them opportunistically.

---

## Open questions

Owner decisions only. Each is answerable.

**Decided 2026-09-13:** the species binding is a **real field written by deterministic code** (slice 1), and
cost shaping **keys on the species' rarity rung above a threshold rung** (slice 2). Both are recorded above.

Still open:

1. **Where is the threshold rung set, and is it one value or per-verb?** Slice 2 gates the species cost
   above a rung; which rung, and whether `bore` and `temper` share it, is balance data for the spec.
2. *(Answered — see § Socket caps by item kind below.)*
3. **How many species materials per species, and do general creatures get zero?** The decided band is 1–2
   and tunable; the value is content, and setting it to 0 for general creatures is the lever that bounds
   the id count against MH's documented sprawl.

---

## Reading gate (this session, per DESIGN-GATE §1)

**Product vision:** [the-game.md](../guide/the-game.md), [the-loops.md](../guide/the-loops.md) — Spine B/C
named above; three stocks; no new loop. **Anything at all:**
[software-architecture.md](software-architecture.md), [decisions.md](decisions.md) (**Set topology classes
2026-09-10**), §2 invariant 15 (SOLID — this contributes to item modules 13/14 and creature-seed, forking
nothing). **Sets:** [item/ssot-sets.md](item/ssot-sets.md) (§3.4 topology, §3.9 rolled-vs-fixed, §3.10
inserts and completion, §4 data shape, `:829` transmutation undesigned),
[item/spec-set-charm-gen.md](item/spec-set-charm-gen.md) (`:93` one set per species, `:263` id from
`speciesId`). **Materials / cost:** [item/ssot-materials-crafting.md](item/ssot-materials-crafting.md)
(§3.1 five closed classes, **§3.4 / `:708` the source-tagging ban**, `:713` sets are drop-only).
**Rarity:** [item/ssot-rarity.md](item/ssot-rarity.md) §3.2/§3.6. **Enhancement / op_kinds:**
[item/ssot-enhancement.md](item/ssot-enhancement.md) §5.3. **Power / tunables:**
[power/ssot-power-scale.md](power/ssot-power-scale.md) §10–§11, [tunables-ssot.md](tunables-ssot.md) T1–T8.
**Validation:** [validation-ssot.md](validation-ssot.md). **Crafting direction:**
[../ideas/crafting-coverage-engine.md](../ideas/crafting-coverage-engine.md) (`:285`, `:120-128` four axes,
no species axis). **Origin + siblings:** [tier-system-ideal.md](tier-system-ideal.md) D2 and § AUDIT,
[gear-climb-ideal.md](gear-climb-ideal.md). **Code verified at the `file:line` cites above**, not from
comments. Prior art web-searched this session; blocked domains and unverified figures flagged inline.

**Boundary honesty:** this session wrote no `tasks/sessions/*.json` record (no `/session-start` in this
harness), so **the DESIGN-GATE §5 boundary box cannot be ticked.** `session-boundary-check.ps1` was run
earlier: 4 active sessions, 13 drift overlaps **between other sessions**, none claiming
`docs/architecture/species-craft-*`.

**§5 checklist:** subsystems identified ✓ · boundary record ⛔ untickable · gate docs read this session ✓ ·
`decisions.md` checked ✓ · claims cite `file:line` ✓ · verified against code, not comments ✓ (the owner's
stated coupling was tested and **disproved** rather than accepted) · surrounding sections read ✓ ·
constraints measured, not assumed ✓ (910 entries, 844 creature themes, 4 `IsSetPiece` lines, 67 recipes, 27
materials — all counted) · §2 invariants hold ✓ · no population count pinned ✓ · **ActorHub N/A and
checked**: set bonuses reach actors through the existing threshold-grant container path; this adds no
composer and no private fold · no SOLID-violating parallel path ✓.
