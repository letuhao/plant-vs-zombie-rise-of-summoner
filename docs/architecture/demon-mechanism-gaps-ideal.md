# Ideal: demon mechanism gaps — hybrid typing, fusion inheritance, threat-driven difficulty

**Program:** facet of `demon-system` / `demon-seed`. **Phase:** idea — **all four open questions
decided the same day, §6**. **No spec, no plan, no code follows from this document**; a spec is the
next real step for whichever mechanism gets built first. Written 2026-09-07.

**Predecessor:** [demon-seed-ideal.md](demon-seed-ideal.md) Q9 named two programs the full aspect
vision depends on and left them unbuilt on purpose. This document is the promised follow-up —
checking, with code open, whether those two programs (and a third, adjacent gap the owner asked to
fold in) are still as unbuilt as Q9 recorded, before anyone proposes a spec against stale
information.

---

## 0. The principles this is built on, restated inline

**1. Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** PvZ owns
the board, vanilla damage, spawn/die, and the sun bank. The RPG observes its events and contributes
signed deltas back. *"Can the lawn express X"* is almost always the wrong question — *"does the RPG
layer have a channel/atom/runtime for X, and is that path wired or inert"* is the right one. An inert
path (a null delegate, a default-off toggle, a debug-only entry point) is a **wiring gap**, never an
architectural wall. All three mechanisms below live entirely in the RPG layer already — none of them
touch what PvZ is.

**2. Seed → concrete → per-player is binding for every demon generator.** Species *stats* are
deterministic and shared; only *effects* roll, per player, at runtime, from a `species-passive.
{speciesId}` container. A generated species with no container does nothing — this is the exact gap
`demon-seed-map.md` §3a closed for species generally, and it is the seam fusion inheritance (§3 below)
has to build on, not around.

**3. Items, traits, and species passives have no behaviour — actors do.** An item, trait, skill, or
species passive is a *source* that puts an atom on an actor's list; none of them participates at
runtime by themselves (`demon-seed-ideal.md` §7.1, quoting `effect-atom/definitions.md` §0). "What
does this demon do" always decomposes to "which atoms does its container carry" — never to the
species' own flavour text.

**4. One power ladder.** Every magnitude derives from `P(Θ)`; every contest from `Θ`. `Θ`'s own
composition is a **closed, reviewed list** (`ssot-power-scale.md` §10) — a power-shaped number not in
it has no permission to exist. This is the direct constraint on §4 below: a demon's own threat cannot
just start contributing to difficulty without a reviewed change to that closed list.

**5. Rarity buys breadth, never power.** A rarity rung sets a count-band and a tier window — how many
atoms roll, from which tier ceiling — never a magnitude multiplier. This is settled
(`demon-seed-ideal.md` principle 4, §4.5) and it is why fusion's own existing `slotsByRarity`/
`recipeCost`/`promotionCostByRarity` shapes (§3.1 below) are the correct precedent to reuse for a new
inheritance cost, not a reason to invent a new curve.

---

## 1. Scope, and why these three

The owner's own framing: of the leftover demon-species mechanisms, exclude `demon-capture` and
`world-events` (both unstarted, both large, both their own future programs) and extend this
exploration to cover the rest that actually affect gameplay. Two of the remaining candidates —
**hybrid element typing** (aspect-scope's own stated blocker) and **fusion trait/action
inheritance** — are the same design space wearing two hats: both are about what makes an individual
demon distinctly itself, and both build on the identical atom-container substrate. The third,
**a demon's own threat contributing to world/encounter difficulty**, is a narrower, structurally
separate power-ladder question — bundled in on explicit request, not because it shares a mechanism
with the other two.

Two candidates were deliberately left off even though they touch demons: the species-build content
gap (16 species need a new anchor — pure content authoring, no design question) and the Patron LIVE
sign-off (an owner-only verification step, nothing to design). Neither is an idea-phase question.

---

## 2. Mechanism A — hybrid element typing

### 2.1 What was actually decided, and why it still matters

Q9 (`demon-seed-ideal.md`) reverted `aspect-scope` — the design that would have let **one species
yield N aspects**, each potentially a different element, trait bias, and starting kit (fire-Peashooter
/ ice-Peashooter as separate playable variants). The owner's own words:

> *"revert aspect feature, original demon need original aspect, no element/status — that is better
> than make a bundle of aspect, they will become chaos and hard to rebalance, so we have a playable
> game first."*

**The load-bearing reason was balance-surface size, not missing technology.** *"N aspects per species
multiplies the balance surface by N before a single demon is playable"* — the class-system's own
dominance matrix was already *"red by design"* at one aspect per actor. Two further blockers were
named as the reason this is a deferral, not a cancellation: hybrid element typing and a passive skill
graph. The passive-tree program has since shipped in full (`tasks/passive-tree-todo.md`, Phases A–J),
closing that second blocker. This section checks the first one, with code open — Q9 itself already
flagged it as *"closer to a wiring question than a design one,"* which is exactly what this section
confirms.

### 2.2 Built

- **An actor may already carry 0, 1, or 2 concrete element types.** `element-hub-ssot.md:86`;
  validated (`primary == secondary` invalid, empty secondary valid, empty+empty means neutral) at
  `element-hub-ssot.md:125-131`.
- **The full dual-type matchup model is shipped**, Pokémon-style but deliberately gentler:
  `element-hub-ssot.md` §8.5 (`:342-354`) — each slot's relation becomes a multiplier (`STR→1.25`,
  `WEK→0.75`, `NEU/SAME→1.0`), the two slots multiply, the product converts back to an additive share
  of `baseOverlayDamage`. A golden test generates all 36 real element pairs from `ElementRoster`
  (`element-hub-ssot.md:336`).
- **A demon species already carries `ElementSecondary` as real data** — nullable, on
  `DemonSpeciesDef` (cited directly in `spec-aspect-scope.md`'s own code excerpt; confirmed live: **21
  of 841** real generated species carry a non-`none` secondary element today, measured this session).
- **Web-battle already reads and uses both elements for the demon's own stats.**
  `src/FusionRpg.Core/Battle/BattleStatComposer.cs:157-160` — `if (setup.ElementPrimary is { }
  primary) AddAffinity(...)`, `if (setup.ElementSecondary is { } secondary) AddAffinity(...)`. Both
  run unconditionally; there is no gate here.
- **The lawn side already resolves both elements too.**
  `src/FusionRpg.Core/Demons/LawnElementResolver.cs:78-89` builds a real `ActorElementTypes` from a
  species' `ElementPrimary`/`ElementSecondary` for every lawn actor, cached per match.
- **A genuine two-component weighted attack payload is fully implemented and tested.**
  `src/FusionRpg.Core/Battle/HybridPayload.cs:33-54` (`HybridPayload.Build`) — given a primary,
  optional secondary, and a per-mille weight, returns exactly two `ElementPayloadComponent`s summing
  to 1.0, or the single-component pre-hybrid shape when the weight is 0 or no secondary exists.
- **The atom/container layer gets dual-element effects for free.** Pool grouping defaults to
  `(family_id, variant)`, so one container can roll *fire* power and *ice* power as two variants of
  one family — *"dual-element typing expressed in the atom layer with no extra work"*
  (`demon-seed-ideal.md:1164-1166`, §7.2).

### 2.3 Wiring gap — not a real gap

- **`hybrid.secondaryWeightMilli` defaults to 0.** `HybridPayload.cs:14-19`'s own docstring: *"Inert
  at the shipped default... Raising it is a balance decision `combat-unification-todo.md` marks
  ask-first, and it moves the expedition goldens: wave demons carry a real `ElementSecondary`
  (`WaveCatalog.cs:115`) even though the hand-built battle goldens do not."* This is the entire
  remaining distance between "a demon's secondary element already affects its own stats" (built,
  unconditional, §2.2) and "a demon's secondary element ever appears in its own attack" (inert until
  this one number moves). Not a missing mechanism — a single already-named, already-scoped tunable.
- **Verified 2026-09-07 (was an open question — now resolved by tracing the code, not asked):** the
  lawn side has **no automatic equivalent of `HybridPayload.Build` at all**, which makes this a
  *bigger* gap on lawn than on web-battle, not the same one. `HybridPayload.Build` is called from
  exactly one place in the whole codebase — `BattleEngine.cs:46` (grep-confirmed) — so nothing on the
  lawn path derives an attack's element weighting from an attacking demon's own
  `ElementPrimary`/`ElementSecondary` at all. A lawn hit's element payload comes from
  `DamagePacketBuilder.ParseElementPayload` (`src/FusionRpg.Core/Combat/DamagePacketBuilder.cs:89-112`),
  which reads an explicitly-authored `elementPayload` list off the overlay/grant JSON — pure content
  authoring, never automatic from actor typing — and `OverlayCombatCalculator.ParseComponents`
  (`:357-373`) returns an **empty** component list, not a single-primary fallback, when nothing was
  authored. `LawnElementResolver` (§2.2) resolves an actor's dual-typing for *matchup lookup against
  an incoming hit's typed payload* — a genuinely different consumer from "this attacking demon's own
  attack should itself carry both its elements," which nothing produces automatically on this side.
  Closing this needs real new code (a lawn-side analogue of `HybridPayload.Build`, wired wherever a
  demon's own lawn attack constructs its overlay), not a tunable flip — see the corrected Path 1 in
  §2.6.
- **`stat.derived` atoms are quarantined everywhere** — *"no opcode, no bag branch, no sink arm; battle
  reads channel mods only from `TraitBattleCatalog`... an aspect built on it is inert until that
  wiring lands. A wiring gap on a scheduled path, not a wall"* (`demon-seed-ideal.md:1192-1195`,
  §7.4). Relevant here because it is the one atom kind a dual-typed demon's own passive would most
  naturally reach for to express "resist both my elements," and it would silently do nothing today.

### 2.4 Real gap

- **The aspect tier itself — one species fanning out into N differently-typed variants — remains
  genuinely unbuilt and reverted, not merely unfinished.** This is a different claim from "hybrid
  typing doesn't exist": a single demon already having two fixed elements is (near-)fully wired
  (§2.2–2.3); *multiple* elemental variants of the *same* species is the part Q9 actually rejected,
  for the balance-surface reason restated in §2.1.
- **Class-system's `point-economy` fourth allocation scope stays degenerate.** It is keyed on
  `(typeId, element)` — *"which strain"* — gated by `element_mastery`
  (`spec-point-economy.md:38,209-213`). Per Q9's own consequence table, this scope *"survives, but
  collapses to 1:1 with the species — degenerate, not missing."* Wiring §2.3's one tunable does
  **not** un-degenerate this scope; only reviving the full N-aspect design would give a player an
  actual *choice* of strain to spend points on.

### 2.5 Genre prior art

**Pokémon's own multiplicative model has a real, named failure mode this project's design already
avoids by construction.** Dual-type defense is the literal product of both single-type multipliers —
2×/0.5×/0× compounding to 4×/0.25×/0×. The community term is *"quad weak"* (cited examples: Gastrodon,
Magcargo, Omastar, Charizard — [PokéBase](https://pokemondb.net/pokebase/206256/what-are-all-pokemon-that-have-4x-weaknesses)),
and it compounds lethally with entry hazards: Stealth Rock deals ⅛ max HP at neutral and scales with
the *same* type multiplier, so a 4×-weak Pokémon loses 50% max HP switching in once
([Smogon](https://www.smogon.com/forums/threads/how-does-stealth-rock-damage-calculation-work.37178/)).
Game Freak's actual fix took until Generation 8: a held item (Heavy-Duty Boots) granting full hazard
immunity, introduced specifically because 4×-weak types were unplayable against hazard-stacked teams
([Game8](https://game8.co/games/pokemon-sword-shield/archives/274476)).

**This project's own §8.5 model cannot reach that failure mode as designed.** The per-slot swing is
±25% (`1.25`/`0.75`), not Pokémon's ×2/×0.5, so the worst two-slot compound is `0.75 × 0.75 = 0.5625`
and the best is `1.25 × 1.25 = 1.5625` — nowhere near a 4×/0.25× swing. This was true before this
document and is worth stating plainly: the existing design already priced out the industry's best-known
version of this failure, and nothing here needs to change that.

**SMT's alternative is worth naming for if the roster ever needs more than two slots.** Demons carry
independently-authored per-element ranks — Weak / Resist / Null / Drain / Repel — never multiplied
together; a demon can be simultaneously Weak-Fire, Null-Ice, and Drain-Elec, and conflicts on one
element resolve by a fixed priority (Drain > Repel > Null > Resist > Weak), with Null still letting 1
damage through rather than reaching true immunity
([Megami Tensei Wiki](https://megamitensei.fandom.com/wiki/Affinities_(mechanic))). Not proposed here
— the two-slot product model is already safe — but the fallback shape to reach for if a future
decision ever widens past two types.

### 2.6 ✅ Decided (owner, 2026-09-07): both surfaces, one shared function, no parallel implementation

**Owner's own framing, which corrects this document's original path split:** *"Do all, we only have
on[e] battle engine, do not make duplicated code, lawn game still use same battle engine, reconcile or
retire duplicate[d] code if need[ed]."* Both 1a (web-battle) and 1b (lawn) ship, but not as two
separately-built mechanisms — the lawn side must call the **same** `HybridPayload.Build`
(`HybridPayload.cs:33-54`) web-battle already uses, not grow a second, parallel weighting
implementation. **Checked before writing this down, not assumed:** grepped
`src/FusionRpg.Injector` for any existing primary/secondary weighting arithmetic
(`1.0 - secondaryWeight`-shaped code) — **none exists**. The lawn side has never had its own
hybrid-weighting logic to duplicate or retire; `DamagePacketBuilder.ParseElementPayload` only ever
parsed an already-weighted, explicitly-authored list (§2.3). So the real shape of this work is
**additive, not a reconciliation of two existing implementations**: wire a call to the existing
shared `HybridPayload.Build(primary, secondary, weight)` into whatever constructs a demon's own lawn
attack overlay, using the same `hybrid.secondaryWeightMilli` tunable both surfaces will now share,
rather than authoring a second copy of the weighting math. `spec-aspect-scope.md`'s full
N-aspects-per-species revival (the former "Path 2") was **not** part of this decision — it stays a
separate, larger, independent call, neither chosen nor rejected here.

---

## 3. Mechanism B — fusion trait/action inheritance

### 3.1 Built

- **A "pick one, roll rest" mechanism already shipped** (`spec-demon-fusion.md`, locked decision 5,
  2026-08-21): *"player picks ONE guaranteed trait from any input; remaining slots (1/2/2/3 by result
  rarity) seeded-roll from the combined input pool."* **But this operates on the old, flavour-only
  `TraitPool` string tags** (curated per species — see the repo's own `TraitPool curation` work),
  never on the mechanically-real atom containers. It is a real, shipped mechanism at the wrong layer
  for what "inheritance" is being asked to mean now.
- **The real per-player roll mechanism already exists at the right layer.**
  `SpeciesMaterialiser.Materialise` (`src/FusionRpg.Core/Demons/Materialise/SpeciesMaterialiser.cs:35-70`)
  rolls a `species-passive.{speciesId}` container against one player's world seed into a real
  `MaterialisedRoll`/`InstanceRow` — pure, seeded, reproducible, zero model calls. This is the exact
  "T5.3 species-effects pool+roll mechanism" a fusion-inheritance feature would need to retarget.
- **Dual-element pool grouping (§2.2) is directly reusable here too** — "which element's version of
  this trait carries over" is the same `(family_id, variant)` grouping already built for hybrid
  typing, not a second mechanism to invent.
- **A real, tuned, already-shipped cost-scaling shape exists to copy.** `data/tuning/fusion.v1.json`
  (read in full): `recipeCost` 150→1000 souls across 7 rarity rungs, `promotionCostByRarity` 150→1000
  across all 10, `slotsByRarity` 1/2/3. Any inheritance cost curve has an obvious, already-precedented
  shape to reuse rather than invent.

### 3.2 Real gap

- **`InstanceProducer.Compose` (`src/FusionRpg.Core/Effects/Atoms/InstanceProducer.cs:28-82`) has no
  parameter for forcing a specific pool pick.** Its pool half is always freshly drawn via
  `Resolver.Resolve` (`:60-61`). "Force these N atoms from the parents' own rolls into the output,
  roll only the rest" does not exist at this layer — it is a genuine new capability, not a flag to
  flip.
- **Fusion does not read a specimen's own materialised content at all today.** `spec-demon-fusion.md`'s
  current text: *"Recipe inputs are SPECIMENS of those species (any stars; stars are not refunded).
  All inputs consumed."* The specimen's own roll is consumed, never inspected — there is no code path
  from "here are the two sacrificed specimens" to "here is what each one actually rolled, pick from
  it."

### 3.3 Genre prior art

**This repo already has a datamined, sourced research pass on exactly this mechanic**
(`docs/research/genre-mechanics/06-summoner-minion-fusion-rpg.md` §1.5) — used directly rather than
re-derived:

- **Three separable gates, all data-driven.** *How many transfer*: Persona 5's step function on
  combined parent skill count (3–5→1 … 42+→8); Nocturne averages the parents' own counts, capped at 5
  (two-parent) or 6 (sacrificial three-parent). *Child capacity*: a flat slot count per game — **8** in
  most SMT/Persona entries, **10** in Persona 5 Royal (*"the single most load-bearing number in the
  whole system... they moved it exactly once"*). *Eligibility*: Nocturne's per-demon 9-category mask
  (median 4/9 allowed) versus Persona 5's shared 14×12 inheritance-type grid (129/168 = 77% allowed,
  a demon's *own* element always blocked) — the shared-grid shape is the cheaper-to-maintain lesson a
  later game in the same studio actually chose.
- **The series' own determinism arc never settled, which is itself the finding.** Nocturne shipped
  fully random inheritance and Atlus patched in a player toggle to disable it **in 2021, fifteen years
  later**. SMT IV moved to full player choice (with named exceptions — unique skills like Alice's
  signature move are flagged non-inheritable). **SMT V's base game reverted to random**, weighted by
  parent skill count. Vengeance's fix (2024) is Essence Fusion: a single-use captured per-demon
  skill-set item, still gated — most Essences start with only 3 of 5 skills visible until a 3-tier
  unlock. Read across the whole arc: a franchise on its *fifth mainline entry of the same mechanic*
  still oscillates between random and chosen, never converging — the strongest available evidence that
  there is no clean consensus answer here, only a tradeoff to pick on purpose.
- **"Fusion accident"** is the series' own named term for a random unintended result overriding what
  was requested — the franchise's blunt answer to "should fusion always give exactly what you asked
  for": no, deliberately, as a gate on specific content.
- **A structurally different alternative:** Dragon Quest Monsters synthesis inherits *partial points*,
  not discrete skills — *"3 Talents inherited; half the accumulated skill points are retained and a
  quarter of the unspent points become free to reassign."* Worth naming as a genuinely different shape
  (a resource pool passed down, not a slot list), not adopted here but real prior art for "inheritance"
  meaning something other than picking named things.
- **How much determinism a crafting-adjacent system can tolerate before it breaks the game around
  it** (freshly researched, since this repo has no existing pass on it): PoE's Harvest league (2020)
  let players target-craft specific mod categories outright. GGG's own stated reasoning for gutting it
  in patch 3.14: *"we don't want to take away the feeling of closing your eyes and Exalting an item,
  scared to see whether you ruined it or not"* — ordinary players had reached near-best-possible gear,
  flattening the game's own trade economy
  ([Destructoid](https://www.destructoid.com/path-of-exile-is-messing-with-harvest-crafting-and-the-community-is-up-in-arms/)).
  Last Epoch's answer bounds determinism per item instead of banning it: a **depleting** Forging
  Potential pool (higher-rarity bases start with less), a 25%-chance mercy item (Glyph of Hope, not a
  guarantee), and a deliberate re-randomization item (Glyph of Chaos)
  ([Last Epoch support](https://support.lastepoch.com/hc/en-us/articles/46361877750043-Runes-and-Glyphs)).

### 3.4 ✅ Decided (owner, 2026-09-07)

The already-shipped "pick one, roll rest" shape is closer to the genre's *healthier* answer (SMT
IV/Persona's bounded player choice) than its worst one (Nocturne's original all-random model, which
the developer itself eventually patched away from) — the actual gap is layer, not philosophy: it picks
from cosmetic tags, not mechanically-real atoms. Both open decisions were put to the owner with the
genre tradeoff stated plainly, and both landed on the *more* deterministic, *more* novel option rather
than the safer default this document recommended — recorded here as a deliberate choice, not
softened into the recommendation:

- **Pick-count ceiling: the full `slotsByRarity` count (1/2/3 by rarity), not a smaller fraction.** At
  Almanac (3 slots), a player may inherit all 3 picks and roll nothing — the PoE Harvest shape this
  document flagged as the one the genre's own evidence shows getting walked back, chosen anyway.
  **One real mitigating difference worth naming, not to relitigate the call but so the risk is
  understood accurately:** PoE Harvest's own failure mode was specifically about **flattening a
  player-to-player trading economy** — full determinism meant nobody needed to buy a
  someone-else's-lucky-roll item anymore. This fusion system has no player-to-player trade at all
  (specimens are sacrificed, not sold), so the *specific* economic failure GGG cited does not
  transfer directly here. The cost gate below is this system's own friction against over-use instead.
- **Cost basis: the inherited pick's own source rarity, not the output's.** A pick's soul cost is
  keyed by the rarity of the trait being carried over — inheriting an Almanac-tier pick costs more
  than a Chaff-tier one, regardless of what the fused output itself turns out to be. This still reuses
  the *existing* `recipeCost`/`promotionCostByRarity` **table shape** (150→1000 souls across the
  rarity rungs, `data/tuning/fusion.v1.json`) — only the index into it changes, from the output's rung
  to the picked trait's own rung. No new curve shape is needed; a per-pick lookup against the already-
  tuned table is.
- **Net shape, stated plainly:** a player can pay more to guarantee more of a fused output's identity,
  bounded only by souls and by which traits its two sacrifices actually rolled — full control is
  possible at the top rarity, but every pick that pushes toward "exactly what I wanted" costs
  proportionally to what it's worth, which is this system's own answer to the tension PoE and Last
  Epoch resolved two different ways.

---

## 4. Mechanism C — a demon's own threat feeding world/encounter difficulty

### 4.1 Built

- **`Θ`'s composition is closed, documented, and read in full for this document** —
  `ssot-power-scale.md` §5: `Θ_actor = Wd·daveLevel + Wa·realmsAdvanced + Wr·runTerm(pvzRuns)`,
  `Θ_content = Wz·zombossLevel + Wm·mapLevel(M) + Ww·worldTier + Wf·realmsAdvanced`. **Six axes total.
  Zero species or demon term, confirmed by reading the formula, not assumed.**
- **A real, tuned, per-rung offset column already exists and is simply unconsumed.**
  `data/tuning/demon-threat.v1.json`'s `thetaOffset`: `nuisance=0 … calamity=40`, an evenly-stepped
  ladder, real numbers, already committed.

### 4.2 Real gap

- **Confirmed zero consumers anywhere in `src/`, by direct grep this session** — `threatBand` itself
  is discarded during species generation and unreachable from the live `DemonSpeciesDef` catalog. This
  is not a small wiring flip like §2.3's tunable: nothing today has an opinion about *when* a demon's
  own threat should matter for difficulty. A demon becomes "content" in at least three structurally
  different contexts — Zomboss's own lawn deploy (shipped, `demon-lawn-deploy`, already has its own
  independent wave-gated rarity-ceiling roster, unrelated to `Θ`), a captured wild encounter
  (`demon-capture`, unbuilt), a Delve party's opposition (`party-dungeon`, shipped) — and each might
  reasonably want a different answer to "does this feed `Θ_content`, and how."

### 4.3 Genre prior art

**D&D 5e's Challenge Rating** is the closed-formula precedent, and its own well-documented failure is
directly relevant to any "read one number off a monster" design. CR is computed as an average of a
Defensive CR (from HP + AC) and Offensive CR (from damage/round + to-hit bonus), each read off a
published expected-value table, then nudged for outliers (concrete row: CR 5 expects AC 15, 131–145
HP, +6 to hit, 33–38 damage/round —
[nerdsandscoundrels.com](https://www.nerdsandscoundrels.com/how-to-calculate-cr-5e/)). **The
documented, developer-acknowledged flaw**: the averaging assumes one typical round, so multiattack and
legendary actions (which fire on *other* creatures' turns) push real output far above what the formula
assumes — systematically underrating action-economy-heavy monsters. Lead designer Jeremy Crawford: the
old method effectively asked *"if the DM chooses the most powerful option every round, here is the
monster's CR"* rather than modelling what actually happens at the table; the 2024 revision changed
methodology specifically so a legendary creature holds its stated CR regardless of which legendary
actions are chosen ([Wargamer](https://www.wargamer.com/dnd/challenge-rating-reboot)). **The lesson for
this project:** a single derived number is easy to compute and easy to get systematically wrong for
exactly the outlier content that matters most — worth deciding *up front* whether `threatBand` is
meant to predict raw combat output (where this failure mode reproduces) or something narrower.

**Pokémon's own "pseudo-legendary" ~600 BST convention is a cautionary contrast, not a model to copy.**
It is explicitly **fan-coined** — Bulbapedia states plainly Game Freak has never used or documented the
term; it is reverse-engineered from release patterns, not an authored rule anywhere
([Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/Pseudo-legendary_Pok%C3%A9mon)). This project's
own `threatBand` is closer in kind to **Dragon Quest Monsters' own developer-authored F→S rank** (9
tiers, gating rarity and stat ceilings by design,
[DQM Wiki](https://dragonquestmonsters.fandom.com/wiki/Monster_Ranks)) — a real, intentional column,
not an inferred community pattern. Worth stating so nobody undersells what already exists here by
treating it as informally as Pokémon's own community convention.

**If the real need turns out to be "many demons → one encounter/zone difficulty number" rather than
"this one demon's own danger," two shipped genres solve that differently, and both are a materially
different shape from directly summing `thetaOffset` into `Θ_content`:** Risk of Rain 2's Director
spends a linearly-scaling **per-stage monster-credit budget** (230 credits at Medium/2 players), each
enemy costing an authored multiple of a baseline cost (elites 6×–36× a normal spawn —
[RoR2 Wiki](https://riskofrain2.wiki.gg/wiki/Directors)); Pathfinder 2e's tabletop encounter budget
works the same way with XP (a party-level creature costs 40 XP, +4 levels costs 160 XP, against named
thresholds from Trivial=40 to Extreme=160 for four characters —
[Archives of Nethys](https://2e.aonprd.com/Rules.aspx?ID=2715)). Both are **static, authored budgets a
designer spends**, not a live sum of individual creature stats read at encounter time.

### 4.4 ✅ Decided (owner, 2026-09-07): defer, tracked, two named future consumers

*"Defer and track, we will ship it in dungeon party and world map event later."* `thetaOffset` stays
exactly as §4.2 found it — a real, tuned, unconsumed column — until one of its two named future
consumers is actually being built:

- **`party-dungeon`** (the Delve) — already shipped/partial; a demon's own threat rung would be the
  natural per-encounter difficulty signal inside a run.
- **World map events** — named by the owner even though `world-events` was explicitly out of scope
  for this document (§1); recorded here as a real future consumer so it is not rediscovered as a
  surprise when that program starts, without this document reaching into that program's own design.

Notably **not** named as a target: `demon-lawn-deploy` (which correctly keeps its own independent,
already-tuned difficulty curve, unrelated to `Θ`) and `demon-capture` (also out of this document's
scope, per §1). Which of the two shapes named in §4.3 — a direct per-demon `Θ_content` read, or an
authored per-encounter budget (RoR2/PF2e-style) — fits better is left to whichever of the two
programs actually picks this up first; this document does not pre-decide it.

---

## 5. Cross-cutting synthesis

**A.** Hybrid element typing ships on both surfaces through one shared function
(`HybridPayload.Build`), never a second parallel implementation — confirmed no existing lawn-side
weighting code needed retiring, only a call needed adding (§2.6). Full aspect-scope revival (the
N-aspects-per-species design Q9 deferred for a stated balance reason) stays a separate, independent,
still-undecided call.

**B.** Fusion trait inheritance's existing shape ("pick one, roll rest") already sat on the genre's
healthier answer; the owner's own decision pushes it further toward player control than this
document's own recommendation — full `slotsByRarity` picks, priced by each pick's own source rarity
(§3.4). The one real risk this document flagged (PoE Harvest's trade-economy collapse) does not
transfer cleanly to a system with no player-to-player trading, which is why the owner's choice is a
reasoned bet, not an oversight of the genre evidence.

**C.** The species Θ offset stays exactly what §4 found it to be — a real, tuned, unconsumed number —
by deliberate choice, not by default. Two real future consumers are now named (`party-dungeon`, world
map events) so the next session to pick this up starts from a decision, not a blank page.

None of the three requires changing what PvZ is, and none of them requires a new closed vocabulary —
every mechanism named above already has a home in an existing, shipped system. All three of this
document's own open questions closed the same day it was written, two by investigation and two by a
direct owner decision — see §6 for the full record.

---

## 6. Decisions and their record (2026-09-07)

This document originally closed with four open questions. All four are now resolved — kept as a full
record here, matching this repo's own convention of closing rows rather than deleting them, so a
future reader sees what was asked, what the code actually showed, and what was decided, rather than
just a final answer with the reasoning lost.

| # | Question | Resolved by | Outcome |
|---|---|---|---|
| — | Does lawn-side overlay combat build a hybrid attack payload at all? | **Investigation**, not a decision | No — `HybridPayload.Build` has exactly one caller (`BattleEngine.cs:46`); the lawn path only ever parses pre-authored `elementPayload` content, defaulting to empty (§2.3). This is *why* question 1 below reframed from "flip a tunable" to "share a function." |
| 1 | Hybrid typing — how far to go? | **Owner decision** | Both surfaces, via the one shared `HybridPayload.Build` — no parallel lawn-specific implementation. Full aspect-scope revival stays separate and undecided (§2.6). |
| 2 | Fusion inheritance — pick-count ceiling? | **Owner decision** | Full `slotsByRarity` (1/2/3) — the more player-controlled option, not this document's recommended smaller fraction (§3.4). |
| 3 | Fusion inheritance — cost basis? | **Owner decision** | The inherited pick's own source rarity, reusing the existing `recipeCost`-shaped table indexed differently — not the output's rarity (§3.4). |
| 4 | Species Θ offset — which consumer first? | **Owner decision** | Deferred and tracked; named future consumers are `party-dungeon` and world map events, not `demon-lawn-deploy` or `demon-capture` (§4.4). |
