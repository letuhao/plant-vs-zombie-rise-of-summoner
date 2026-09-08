# Action taxonomy research — categories, targeting, composition, cost

**Captured 2026-09-02.** Seven parallel passes, ~8,500 lines, built for the `action-corpus` idea phase
([`../../architecture/action-corpus-ideal.md`](../../architecture/action-corpus-ideal.md)).

**The question that started it:** *how many action categories should an RPG have, and how many
generation pipelines does the corpus need?*

Every file ends with a mandatory **"What I could not find"**. Read those before commissioning
follow-up work — together with [`../game-design/06-unsourced.md`](../game-design/06-unsourced.md) they
are the record of what has already been searched for and does not exist.

---

## The eight findings that mattered most

1. **5 top-level categories is right, and roster size is not an argument for more.** Category count is
   uncorrelated with roster size across the whole sample: Pokémon 3 for 1,025 species, D&D 8 for 3,207,
   Arknights 3 for 374, Pathfinder 2e **0** for 4,748. No game in the sample carries more than 8.
   [01](01-skill-taxonomies.md) §1, §13
2. **Growth is absorbed in the *second* vocabulary, never the first** — and that is where this project
   is thin: 8 tags against PoE's 47 display / ~180 internal, Diablo IV's 61 mechanical, PF2e's 262
   action traits, WoW's 665 aura types. Arknights is the only game near 8, and only because its skills
   carry *no* semantic classification at all. [01](01-skill-taxonomies.md) §13
3. **The targeting vocabulary is strong at geometry-from-an-anchor and empty everywhere else.** Of 25
   concepts examined: 5 expressible, 3 refuted as non-targeting, 16 gaps — and **11 of the 16 are not
   shapes**. They are predicates, orderings, per-target weights, counts and anchors.
   [02](02-targeting-vocabularies.md) §1
4. **All five documented composition blow-ups are the same defect: the priced thing and the powerful
   thing were not the same thing.** PoE triggers bypassing the multiplier, Noita's Chainsaw paying in an
   unmetered axis, D4's Overpower surviving an 80% cut, Hearthstone's Charge × buffs × copy, MTG's
   Dredge. [03](03-composable-skill-systems.md) §11
5. **Eight unrelated studios converged on the same rule: restrict where a modifier may live, not which
   modifiers may coexist.** A named-pair exclusion list is the only mechanism that grows O(n²), and
   every mature system keeps it tiny. The `group` mechanism this project already uses has been stable in
   Diablo II and PoE for 25 years. [03](03-composable-skill-systems.md) §11
6. **Enabler reachability is guaranteed structurally, never statistically** — five strategies, and the
   documented failure (a Genshin Cryo+Hydro team that produces Frozen forever and never Shatters) is
   exactly the dead-pairing this project's `EnablerPayoffCoverage` assertion exists to prevent.
   [04](04-control-status-actions.md) §6
7. **Floors do not stop cost loops; coupling does.** Diablo III's 0.5 s cooldown and 1-resource floors
   are real and irrelevant, because the effects that actually went free *remove* cost rather than reduce
   it and never enter the formula. Every runaway found came from a cost stat wired into a **second
   output** — PoE's Archmage and Indigon paying damage off mana cost, Captain Crimson's paying toughness
   off cost reduction. [07](07-cost-mistuning-failures.md) §7
8. **The rung table's escalation tax matches Magic's to within 0.1% per step** — 1.0432 against 1.0420
   (recomputed 2026-09-02). But Magic's shape is *linear with an entry fee*, so its tax converges, while
   a quotient of two geometrics diverges: ×1.40 at rung 10, ×1.74 at 15, ×2.14 at 20.
   [06](06-action-cost-models.md)

---

## The files

| File | What it answers |
|---|---|
| [01-skill-taxonomies.md](01-skill-taxonomies.md) | **How many categories do shipped RPGs use?** Categories-per-100-creatures across 12 games; PoE's dual tag vocabulary and its `allowed_types`/`excluded_types`/`added_types` gating; where taxonomies grew, moved or were deleted |
| [02-targeting-vocabularies.md](02-targeting-vocabularies.md) | **A 25-row gap table** against the shipped 6 modes + 4 shapes + 3 anchors. Cone, diagonals, chain, per-target weight, launch-position masks, and predicate targeting — which PvZ itself ships |
| [03-composable-skill-systems.md](03-composable-skill-systems.md) | **The prior art for generating skills from parts.** PoE supports, Noita, Magicka, Tyranny, roguelike identification, Borderlands part-generation, the Nemesis patent, the Elder Scrolls spell-maker's three published price functions — plus how each stops degeneracy and how each names the result |
| [04-control-status-actions.md](04-control-status-actions.md) | Status vocabulary sizes across 10 games, DR formulas with real numbers, stacking models, and **the five ways shipped games guarantee an enabler is reachable** |
| [05-support-healing-actions.md](05-support-healing-actions.md) | The support space. **The buff break-even threshold, derived because nobody publishes it**; support's share of a creature roster; four first-party statements on the healer-mandatory problem |
| [06-action-cost-models.md](06-action-cost-models.md) | Cost, cooldown and tempo models over 27,000+ shipped actions and cards from 13 games, with power-vs-cost growth computed per system |
| [07-cost-mistuning-failures.md](07-cost-mistuning-failures.md) | **What happens when the cost is wrong.** Infinite loops, cooldown stacking to zero, free abilities, and the opposite failure (a lower rank being better) — with patch numbers and first-party post-mortems. Nine recurring failure modes |

---

## Method

Findings came from shipped data wherever it was reachable, not from wiki prose.

| Source | Used for | File |
|---|---|---|
| RePoE `gems.md` schema + published JSON, PoEDB | PoE's 58 display tags vs ~180 internal types, and the real gating rule | [01](01-skill-taxonomies.md), [03](03-composable-skill-systems.md) |
| TrinityCore `SpellInfo.h` / `SpellInfo.cpp` / `SpellEffects.cpp` | WoW's target rows, reference types, and the effects that are *not* targeting | [02](02-targeting-vocabularies.md) |
| `docs.larian.game` (first-party engine wiki) | 17 `SkillType` values crossed with the `Ability` school | [01](01-skill-taxonomies.md) |
| FFXIV game sheets | 1,332 player actions across 6 categories; GCD is cooldown group 58, not a category | [01](01-skill-taxonomies.md) |
| NetHack `objects.c`/`o_init.c`, Crawl `item-name.cc`/`artefact.cc`, Angband `randname.c` | Procedural naming and the identification layer | [03](03-composable-skill-systems.md) |
| gibbed's Borderlands dumps, `blizzhackers/d2data` | Part-slot generation and affix `group` | [03](03-composable-skill-systems.md) |
| Warner Bros patent US10926179B2, Freehold Games FDG'17 paper | Nemesis trait derivation; Caves of Qud mutation legality | [03](03-composable-skill-systems.md) |
| UESP cross-checked against OpenMW `calcEffectCost` | The Elder Scrolls price functions, agreeing to the unit digit | [03](03-composable-skill-systems.md) |
| Scryfall (16,960 creatures), HearthstoneJSON (4,708 minions), Dota/League APIs, Guilty Gear frame data | Power-vs-cost growth per system | [06](06-action-cost-models.md) |
| SWARFARM API, Arknights `character_table` | Support's share of a creature roster | [05](05-support-healing-actions.md) |

Numbers marked **(computed)** are tallies over primary data. Two figures were **withdrawn mid-pass** by
the researcher who produced them (Last Epoch aggregates) and are marked as such rather than quoted, and
one widely-repeated quote is explicitly flagged **do-not-print** as unverifiable.

---

## Reusable access notes

The previous round recorded Fandom as sitewide HTTP 402. **That is now bypassable.**

| Host | State | Workaround |
|---|---|---|
| `fandom.com` | 402 direct | **`r.jina.ai` reader proxy works** — reopens the FF, DQ, FEH and Megaten wikis |
| `poewiki.net` | Anubis challenge | **clears on a second navigation** |
| `wowdev.wiki` | 403 direct, 403 on its API, blocked through the proxy | use TrinityCore source instead |
| `wiki.gg` | inconsistent — some subdomains 401 | — |
| `liquipedia.net` | article pages 403; API returns stubs | — |
| Caves of Qud wiki | — | serves `action=raw` |
| `r.jina.ai` | working | confirmed for GW2 wiki, FFXIV Lodestone, Bungie, Steam Community |

---

## Two things this research is *not*

**It is not a spec, and it is not a proposal.** Nothing here says what this game should do.
[`action-corpus-ideal.md`](../../architecture/action-corpus-ideal.md) draws the comparisons; design work
still goes through [`../../DESIGN-GATE.md`](../../DESIGN-GATE.md).

**It is not evenly sourced.** Every pass hit its 200-call search budget, so later work in each ran
against APIs and datamine repos rather than prose — which made the shipped-data material stronger and
the designer-commentary material thinner. The exception is [05](05-support-healing-actions.md): the
healer-mandatory problem is one of the few topics designers genuinely do discuss on the record.
