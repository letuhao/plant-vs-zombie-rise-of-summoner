# The item ideal — what an item is, and what wears it

**Status:** **Ideal captured 2026-08-22. Reconciled against the shipped platform 2026-09-03 — see
§2a, and read it before §§3–11. The program graduated the same day — the capability map is
[item-map.md](item-map.md).** Discussion document, not a spec and not a plan. No build is
authorized from it.

> ✅ **The decision round is complete — see §2b.** **Nineteen owner rulings (D1–D19)** plus four resolved
> by recommendation, and **D16 ratifies the ~110 lane-internal picks as a batch.** All 144 open questions
> across the seventeen lanes and four decision documents are accounted for, and **§2e verifies the three
> defect claims that had stood unverified since 2026-08-22** (one was real and has since been fixed by
> another program; two are confirmed and open; one was refuted). **§2c is down to five items, none of
> which is a decision and none of which blocks authoring.** §2d is what the round did to the
> program's shape — most importantly, **item content is now a seedsmith pipeline that consumes demon
> themes**, the same upstream the action corpus uses.
>
> ⚠ **Round 2 changed what is expensive, not what is wanted.** Five platform programs landed under this
> document after it was written, and **the hardest constraint it reports — the `stat.derived` quarantine
> — was lifted on 2026-08-30.** §2a sorts every claim into *built* / *wiring gap* / *real gap* with
> `file:line`. Where §2a and a later section disagree, **§2a wins**: it was verified against code, the
> rest was written against an older platform. Program prefix: **`item`** (free — no `docs/architecture/item-*` and no
`tasks/item-*` existed when this was written). When it graduates, the capability map is
`docs/architecture/item-map.md`, module specs go in `docs/architecture/item/`, and tasks are
`tasks/item-plan.md` / `tasks/item-todo.md`, per the parallel-programs convention in AGENTS.md.

> **Enriched and partly corrected, 2026-08-22.** Seventeen lane SSOTs, four decision documents and a
> defect register now sit in [item/](item/) — start at [item/README.md](item/README.md). Where a lane
> document and this one disagree, **the lane wins**: they were written against code, this was written
> against intent. Four claims here were corrected outright and are marked ⚠ below — §5 (twelve roles →
> fifteen), §6.2 (unique is not a rarity), §6.3 (the open question is closed), and §6.4 (equipping is
> two acts, not one). A fifth correction lands upstream: the units premise this document inherited from
> `definitions.md` §2 is wrong for six of the twelve derived families — see
> [item/atom-layer-handoff.md](item/atom-layer-handoff.md) §1.

**Inspiration:** the Diablo / Path of Exile line of item systems — base types with implicit modifiers,
affixes rolled from tiered pools at drop, one modifier per family, rarity governing how many affixes
and how strong, hand-authored uniques standing beside procedurally rolled rares.

**Read before proposing against this:** [DESIGN-GATE.md](../DESIGN-GATE.md) §1 rows for the atom layer
and for match/actor lifecycle. This document was written after reading
[effect-atom/definitions.md](effect-atom/definitions.md) (which wins over any spec),
[atom-catalog-ssot.md](effect-atom/atom-catalog-ssot.md),
[atom-family-library.md](effect-atom/atom-family-library.md),
[spec-container-schema.md](effect-atom/spec-container-schema.md),
[spec-instance-and-binding.md](effect-atom/spec-instance-and-binding.md),
[effect-atom-map.md](effect-atom-map.md), [decisions.md](decisions.md),
[standalone-rpg-map.md](standalone-rpg-map.md), and [unique-actor-runtime.md](unique-actor-runtime.md).

---

## 1. The ideal, in one paragraph

An item is a **container of atoms** that an actor wears in a **slot**. It has a **base type** that decides
which slot it fits, which frame of body can wear it, and what it always carries (its implicit). It has
**rolled affixes** drawn from a tiered pool, frozen at the moment it drops. Three families of body wear
three different vocabularies of gear — **humanoid**, **plant**, and **hybrid** — and those vocabularies
are *parallel*, not duplicated: the same twelve slot **roles** exist in each, so one affix library serves
all of them and only the base types differ. Everything that is not equipment — materials, consumables,
quest items, currency — is a separate, simpler thing with its own storage, because the moment an item
carries rolled values it stops being stackable and needs a different table.

---

## 2. What the owner decided (2026-08-22)

These are inputs to the document, not proposals inside it.

1. **Zombies equip human-like equipment.**
2. **Plants equip a special type of equipment for plants.**
3. **The player/commander is human, plant, or zombie**, and therefore wears human-like or plant-like
   equipment accordingly.
4. **Three main equipment categories** follow from 1–3.
5. **Other item types exist:** material, quest items, consumables, and more.
6. **Slot sets should be rich** for both human-like and plant-like frames.
7. **Rarity, socket mechanism, and set-item combos are deferred** to the next discussion round. §11 holds
   what must be answered there; nothing in this document pre-decides them.

---

## 2a. Round 2 — reconciliation against the shipped platform (2026-09-03)

**Why this section exists.** This document was written 2026-08-22 against the platform of that day.
Six weeks of other programs have since landed underneath it, and **the single largest constraint it
reports is no longer true.** A downstream session reading §9 today would design around a wall that was
demolished on 2026-08-30. Everything below was verified against code in one session, `file:line`, per
[DESIGN-GATE.md](../DESIGN-GATE.md) §3 — not against the documents that describe the code.

**Nothing in §§1–2 changes.** The owner's seven decisions stand, frame-not-faction stands, and the
Diablo/PoE inspiration stands. What changed is the substrate, and therefore what is expensive.

### 2a.1 The five platform changes that moved this document

| Change | Landed | What it settles here |
|---|---|---|
| **One power ladder — `P(Θ)`** | 2026-08-23/24 | §6, §8, §9: every item magnitude now has a function. §9's *"power is open, drop bands have no number behind them"* is closed |
| **`stat.derived` un-quarantined** | battle 2026-08-23, **lawn 2026-08-30** | §9's hardest row. *"`+armour` is the hardest common affix to ship"* is **obsolete** |
| **Affix bundles + prefix/suffix split** | 2026-09-01 | §6.2: the pool's roll unit is a named bundle, not a bare atom |
| **Ten-rung rarity, adopted repo-wide** | 2026-08-22 → 2026-09-01 | §11's entire Rarity block, and demons stopped being a second ladder |
| **Twelve aptitudes shipped** | 2026-08-26 | The lane index's open decision #5 (*"five primary attributes, or none"*) — neither: **twelve** |

### 2a.2 Built — things this document calls absent that now exist

| # | What | Where | Consequence for items |
|---|---|---|---|
| B1 | **`stat.derived` executes on both real runtimes.** `RuntimeSupportMatrix(Full, Full, None)` — battle via `TraitAtomSource` (E12), lawn via `AtomDerivedSubsystem` at ActorHub order-350 | `AtomKindRegistry.cs:253` · `Stats/Derived/Subsystems/AtomDerivedSubsystem.cs` | **§9's D6 row and G8 row are dead.** First-wave items are no longer restricted to five kinds. `combat.defense.*`, `crit.*`, resistances — all bindable and all executing |
| B2 | **The affix entity.** `effect_affix(affix_id, affix_class)` + `effect_affix_ref` carrying *either* a concrete `atom_id` *or* a `(slot_name, slot_domain)` slot ref | `RpgStore.Containers.cs:66-84` | *"Master of Fire and Ice"* is expressible today. §6.2's affix model is schema, not proposal |
| B3 | **`prefix_rolls` / `suffix_rolls`**, replacing one `pool_rolls` | `RpgStore.Containers.cs:28-29` | A mixed bundle consumes **one of each**, never doubling either. Derived from `kind_id`, never authored |
| B4 | **The rarity table with per-class bands** — `rarity(rarity_id, ordinal, prefix_rolls, suffix_rolls, min_tier, max_tier)` | `RpgStore.Containers.cs:54-61` | Ten rungs, ordinals spaced by 10, [ssot-rarity.md §3.3](item/ssot-rarity.md). The tier window is now a **column the resolver reads**, closing §6.2's *"needs a draw-time parameter that does not exist yet"* |
| B5 | **The resolver and the producer.** `Resolver.cs`, `InstanceProducer.cs`, `WorldSeed.cs`, `VariantShift.cs`, `ChannelPool.cs`, `AffixLibraryGenerator.cs`, `EligibilityRule.cs` | `src/FusionRpg.Core/Effects/Atoms/` | L2 — *which* derived stat an affix targets — was the missing layer. It exists |
| B6 | **`ProduceAndBind` is called in production** | `RpgStore.UniqueActors.cs:756` | The atom runtime is **not inert any more.** §3's substrate table is now understated, not overstated |
| B7 | **`OnActivate` trigger exists** — `TriggerCount = 8` | `AtomKindRegistry.cs:22,31` | The lane index's open decision #6 (the `OnUse` request gating consumables) has a legal trigger to name |
| B8 | **Twelve aptitudes, shipped in code with a seed mirror** — Might · Fortitude · Vigor · Onslaught · Agility · Composure · Pierce · Focus · Bulwark · Retribution · Precision · Ferocity | `Stats/Aptitudes/Aptitude.cs:40-51` | Open decision #5 answered by another program. **An aptitude is a *source*, not a registered channel** — so an item grants derived channels, and only a deliberate design choice would let it grant aptitude points |
| B9 | **A player commander exists** — `rpg_player_commander` | `RpgStore.PlayerCommander.cs:17` | §3's *"There is no player/commander actor"* is false. §5.6's commander slots have a row to hang on |
| B10 | **`OwnerKind.UniqueActor`** — a durable, per-actor owner scope | `Effects/Atoms/OwnerScope.cs`; decided 2026-09-02, in `decisions.md` | §6.4 traced `actor:{instanceId}` and **rejected it as unreachable**. It was subsequently designed properly and approved. The assign/bind split still holds; what changed is that binding now *has* a durable owner to name |

**The headline: `stat.derived` (B1).** [item/README.md](item/README.md) lists it as open decision #7 with
the note *"Five lanes terminate here. Wave 1's prefix pool is 7–9 families until it lifts."* It lifted.
Five lanes are unblocked, and the affix library's realistic wave-1 width went from ~9 families to the
whole catalogue.

### 2a.3 Wiring gaps — built, reachable, nothing calls it

Using the word deliberately (`CLAUDE.md`): none of these is an architectural limit, and none needs a
design decision. Each is a missing call.

| # | Gap | Evidence | Why it is wiring, not architecture |
|---|---|---|---|
| W1 | **A player install never imports content.** `ImportContent` has exactly one caller in the tree, a dev tool | `tools/AtomImporter/Program.cs:107` — sole caller of `RpgStore.Import.cs:56` | The importer works and is CI-tested. **Every authored item seed today reaches a developer's SQLite and no player's.** Tracked as `E46 player-content-boot`, [content-stack-plan.md](../../tasks/content-stack-plan.md) gate G4 |
| W2 | **Battle reads no equipment** — `ChannelMods` is still documented *"trait stat mods, equipment later"* | `Battle/BattleModels.cs:33` · `Battle/BattleStatComposer.cs:9` | The reader exists and folds at compose time; `TraitAtomSource` is a **working producer on that exact seam**. An equipment producer is the same shape, not a new path |
| W3 | **`ActionSeeder.Generate` has zero callers** | grep, whole tree | Gates G4 [ssot-granted-actions.md](item/ssot-granted-actions.md) — an item that grants an action. The corpus is the missing input, not the mechanism |
| W4 | **`stat.derived` Sim runtime stays `None`** | `AtomKindRegistry.cs:253` | Deliberate, and correctly so — `SimEffectHost` has no consumer, and flipping it would recreate D6's cause. Only matters if item balance wants to run through CombatSim |

### 2a.4 Real gaps — and two of them got *worse* by being ignored

| # | Gap | Evidence | Severity |
|---|---|---|---|
| **R1** | **Unequipping destroys the item.** The orphan sweep deletes any `effect_instance` no binding points at, and runs *"after a withdraw"* | `RpgStore.AtomInstances.cs:607-620` | ⛔ **The window closed.** This document called it *"cheap today because nothing calls that code yet."* Something calls it now (B6). A rolled item needs a **second reachability root** — ownership — or taking a hat off deletes the hat |
| **R2** | **One content import disables every rolled item.** `ResolveBindings` refuses on `instance.CatalogRevision != current`, strict equality, reason `StaleInstance` | `RpgStore.AtomInstances.cs:436` | ⛔ Same window, same cause. Refusal is not deletion, so gear survives — it just stops working, silently, for everyone, on any content patch |
| R3 | **No item entity exists.** No `rpg_item_instance`, no assignment table, no inventory table. The whole item surface is `rpg_unique_equipment(instance_id, slot, item_id)` and a 3-item hardcoded catalog | `RpgStore.cs:409` · `Match/UniqueEquipmentCatalog.cs` | Genuine, expected, and the actual scope of the program |
| R4 | **`frame` does not exist in code**, and neither does the slot-role vocabulary | grep: no `frame` field on any species type | Genuine. §4 and §5 are wholly unbuilt — as designed, since no build was authorized |

**R1 and R2 are the two amendments this document already flagged**, still open, and now urgent rather
than cheap. They are the only findings in this section that are *worse* than in August.

### 2a.5 What prior art says — with numbers, and sources

The 2026-08-22 draft closed with an honest note: *"a web research sweep was launched for this document
and did not return before it was written."* That gap is closed here.

**Affix ceilings — we independently landed on the genre's number.** Path of Exile caps a rare at
**3 prefixes + 3 suffixes = 6 explicit mods**, with jewels the exception at 2+2. Item level gates which
*tier* of an affix may roll; the game rolls the tier first, then the value inside that tier's range
([Craft of Exile](https://www.craftofexile.com/basics)). Our `almanac` rung is **3 prefix + 2–3 suffix**,
and [ssot-rarity.md §3.4](item/ssot-rarity.md) derives ten rungs from 5 count bands × 5 tier windows.
Two independent derivations, one ceiling — that is corroboration, and it means the ladder is not
eccentric.

**Rarity buys breadth and ceiling, never power.** From this repo's own sourced sweep
([03-roster-scale.md §3](../research/game-design/03-roster-scale.md)): Arknights rarity moves median
deployment cost **3 points across five tiers while class moves it 11** — which is the whole mechanism
behind low-rarity viability. Every game that kept low rarity relevant did it by **refusing to let rarity
buy the thing that matters most in its own combat model**: SPD in Summoners War (median ~100 at *every*
natural star) and in HSR (4★ mean SPD 103 *exceeds* 5★'s 101). **Our ladder already complies** — rarity
picks count and tier window, and *"rarity may never change a magnitude"* is enforced by columns. Worth
promoting from an inherited rule to a stated design invariant, because it is the genre's single most
reliable finding.

**Cap the magnitude; creep the effect vocabulary.** FGO's median 5★ ATK moved **32 points across ~450
Servants and ten years**, and the all-time maximum belongs to an early-middle release. All growth went
into effect text. Epic Seven cannot creep statlines at all — base stats come from a 216-cell
(rarity × class × zodiac) template that a dozen heroes share. **This is exactly seedsmith's P1** (*the
LLM writes identity; deterministic code writes magnitude*). The platform is already built around the
genre's proven-correct answer; that alignment should be stated, not left as coincidence.

**⚠ Durability inflates far faster than lethality — and our platform does not do this by default.**

| System | HP growth | Damage growth | Ratio |
|---|---:|---:|---:|
| Diablo II Normal → Hell (L85) | 6.2× | 1.85× | 3.4 |
| Path of Exile level 1 → 100 | 2,989× | 352× | 8.5 |
| Diablo III Torment I → XVI | 16,958× | 163× | **104** |

Every shipped ARPG grows effective HP far faster than damage. **But `P(Θ)` is one function, and
`hp`, `atk` and `defense` are all magnitudes reading it** — so an item corpus that scales offense and
defense off the same curve holds the ratio at **1.0 forever**, which no shipped game in the table does.
This is not a defect in the ladder: `P(Θ)` sets the *shape*, and per-channel-family band assignment sets
the *ratio*. **It is a design question this document never asked**, and it belongs in §11.

**Slot count is our biggest divergence from prior art, and it is not close.**

| Game | Gear slots per unit | Roster gearing |
|---|---:|---|
| Genshin Impact | **5** artifacts | documented as a major grind complaint |
| Summoners War | **6** runes | rune farming ≈ **10×** Genshin's artifact play count |
| Diablo II | 10 | single character |
| Path of Exile | 9 + flasks + jewels | single character |
| **This game** | **15 roles** ([ssot-equip-slots.md](item/ssot-equip-slots.md)) | **× a roster** |

The two games in that table that gear a *roster* rather than a hero use **5 and 6**, and both are
widely documented as having painful gear grinds at that number
([Summoners War runes](https://summonerswar.fandom.com/wiki/Runes) ·
[Genshin artifact grind](https://game8.co/games/Genshin-Impact/archives/304653)). We propose 15, times
N specimens. [ssot-inventory.md](item/ssot-inventory.md)'s two-storage-grade answer — stock items need
no rows — solves the **database** problem completely and the **player** problem not at all. §8 already
names this as *"the one that must be answered before slot counts freeze."* Prior art says the answer is
not "tune it later."

**A drop-only tier band is how crafting stays honest.** Last Epoch caps *crafted* affixes at **T5**;
**T6–T7 spawn only on dropped items**, which is what keeps drops relevant once a deterministic crafting
system exists. Crafts also drain a random amount of a finite per-item **Forging Potential**, so an item
has a bounded number of edits ([Maxroll](https://maxroll.gg/last-epoch/resources/beginner-crafting-guide) ·
[Last Epoch wiki](https://lastepoch.fandom.com/wiki/Crafting)). Our tier ladder is t1–t5 and
**saturates at t5 by structural limit** (no t6 row exists — the `VariantShift` note in
[effect-pipeline-map.md §6](effect-pipeline-map.md)). So our entire tier range is inside what crafting
could reach. Whether to reserve a drop-only band is an open choice §11 never asked, and I6/I7 inherit it.

**The industry's canonical failure is buyable progression, not bad affixes.** Diablo III's launch
itemization is the genre's most-studied failure — the auction house made *buying* the only reasonable
way to progress well into the endgame, and the fix was removing it plus class-appropriate drops and far
higher legendary rates ([Diablo III](https://en.wikipedia.org/wiki/Diablo_III), and Josh Mosqueira's GDC
post-mortem). This game has no auction house and no trading (§10 excludes it), so the mechanism cannot
occur — worth recording as a *deliberate* immunity rather than an accident.

### 2a.6 The seven open decisions, re-answered

[item/README.md](item/README.md)'s *"Open — needs the owner"* table, checked against code today.

| # | Decision | Status now |
|---|---|---|
| 1 | The two blocking amendments | **Still open, now urgent.** R1/R2 above. No longer cheap — the code they protect is live |
| 2 | The content cut, ~3 100 cells → ~880 | **Still open**, and unchanged by anything below it |
| 3 | Reason-code surface, 33 → 101 | **Still open** |
| 4 | Max reachable item level is 11 | **Superseded in framing.** Item level is no longer a private ladder — `Θ` is the index and `P(Θ)` the magnitude. The question survives as *"what `Θ` does shipped content reach?"*, which is a content question with a formula behind it |
| 5 | Primary attributes: five, or none | **Answered elsewhere: twelve aptitudes**, shipped (B8). An aptitude is a source, not a channel — so I11's proposal needs rewriting against a real system, not deciding |
| 6 | The `OnUse` trigger request | **Substantially answered** — `OnActivate` exists (B7). What remains is whether consumables name it |
| 7 | `stat.derived` quarantine / E12 | ✅ **Closed.** Full on battle and lawn (B1) |

**Two of seven closed, one superseded, one answered by another program.** Three remain, and #1 changed
character from *cheap housekeeping* to *the thing that must land before a single item row exists*.

### 2a.7 What this reconciliation adds to §11's open list

Not corrections — genuinely new questions the platform's arrival creates.

1. **The offense/defense ratio.** `P(Θ)` is one curve; every shipped ARPG grows EHP 3–100× faster than
   damage. Band assignment per channel family is the lever. What ratio do we want, and does it drift?
2. **A drop-only tier band.** t1–t5 saturates at t5. Reserve part of it for drops, or let crafting
   reach the ceiling?
3. **Aptitudes and items.** Twelve aptitudes are *sources*; derived channels are what items modify.
   May an item grant aptitude points at all, or only channels? The former is a much stronger lever.
4. **Commander vs. specimen gear.** `rpg_player_commander` exists and demons exist. Prior art says
   roster gearing is where item systems die. Does gear live on the commander (one paperdoll, always
   relevant) or on specimens (N paperdolls, the Summoners War problem)?
5. **Does the roster answer come before slot count?** §8 says yes. Prior art agrees emphatically: the
   two roster games in the genre use 5–6 slots and are still described as grindy at that number.

---

## 2b. Decisions closed — owner, 2026-09-03

Round 2's reconciliation (§2a) left a decision list. **This section closes it.** Every row below is an
owner ruling, recorded with its reasoning so a later session can tell a decision from a guess. Where a
ruling contradicts a lane document, **this section wins and the lane is stale on that point** — the
affected lanes are named per row.

Five questions were closed by the platform rather than by a ruling and are recorded in §2a.6 and
§2a.2 (`stat.derived`, `OnActivate`, twelve aptitudes, `OwnerKind.UniqueActor`, the ten-rung ladder).
Two more were closed in passing while this round was assembled:

- **d4 §7.6 — *"Who owns the durable per-actor owner scope? No lane owns it, and every equipment
  binding depends on it."*** → **`OwnerKind.UniqueActor`**, approved 2026-09-02, in `decisions.md`.
- **d4 §7.3 — *"Which rarity ladder is real?"*** I1 authored **10** rungs, I12 designed drop weights
  against **7**, I6 set enhancement caps against **5**. → **Ten.** It is shipped as the `rarity` table
  (`RpgStore.Containers.cs:54-61`) and adopted repo-wide including demons. **I12's and I6's per-rung
  tables are stale and must be re-derived against ten.**

---

### D1 — Gear is uncapped. Roster scale is not this program's problem

> *"There are no limit, commander and unique demons can equip items full 15 slots. So i dont worry
> about game balance, we just focus our item balance, other feature cover it like limit only 5 unique
> demon can be deploy to the lawn that depend in each feature, we dont care."*

The commander and **every** unique demon may wear the full role set. No specimen is excluded and no
specimen gets a reduced table.

**The important half is the second sentence.** Roster pressure is real, but it is regulated by the
features that own it — deployment caps, squad size, contract slots — not by the item system pricing
itself defensively against a problem it does not own. This is the same boundary discipline the rest of
the repo runs on, applied here.

| Consequence | Where it lands |
|---|---|
| **I2 §10.1 is settled: fifteen roles, not fourteen.** `retinue` stays | [ssot-equip-slots.md](item/ssot-equip-slots.md) §2.3, §8.5 |
| **I13 §3.1 option D stands** (two storage grades). Option C — *"small deployable squad; only 5 actors ever need gear"* — is **rejected as a storage answer**; it survives only as the balance observation it already was | [ssot-inventory.md](item/ssot-inventory.md) §3.1 |
| ⚠ **Drop volume is now two numbers, not one.** What is *live* is bounded by deployment (~5 actors); what is *ownable* is unbounded and grows with the roster. I12 calibrated the first (75 slots ≈ 10 days). **The second has no number yet** | [ssot-generation.md](item/ssot-generation.md) §11.1 |

**What this does not do:** it does not repeal the hybrid price. *"Full 15 slots"* answers *who may
gear*, not *how a hybrid body is costed* — see D3.

### D2 — No slot unlocking in v1, and the mechanism is reserved

> *"No unlocked for now but reserve for later unlock mechanism like breakthrough system, quest system,
> etc."*

Every slot is open from the start. **But the gate must exist and default to open**, so a later
breakthrough or quest system can close slots without a schema migration or a content re-author.

This settles **I2 §10.6** by declining both of its options for now: the unlock ladder is keyed to
neither per-actor level nor account progression *yet*. I2's own note is what makes this cheap — *"the
gate reads one number, and which number it reads is one line."* The requirement on I2 is therefore:
**ship the predicate, ship it defaulting to always-open, and do not hard-code fifteen-always-open.**

I2 §2.10's design is not discarded; it is unwired. That is deliberate, and it is the difference between
a decision and an omission.

### D3 — The commander may be hybrid. Hybrid floors at 80% and earns parity by mixing

> *"Hybrid allowed."* … *"hybrid loose 20% not 11%"* … *"80% is my suggestion, hybrid have advantage
> that can equipped strongest equipment from both type, so it will be cheat, we limit it slot to avoid
> cheat"* … *"we limit the hybrid slot so they can mixup item and still very strong, 80% if they dont
> mix up, can we do that?"*

**Hybrid is allowed, including for the commander.** Settles **I2 §10.4**, which read OD1 as *never
hybrid* and designed for it.

> ⚠ **Amended by D14 the same day: the `standard` slot is out of scope.** The commander is modelled as
> another unique demon for this program, so it has **15 roles pure / 12 hybrid, with no 16th slot.**
> Every budget figure below is unchanged — `standard` was always additive to them.

**And the hybrid price changes shape entirely.** I2 priced it as a flat 10.5% cut. It is now
**a floor of 80% that mixing earns back.**

#### The two halves

| | Mechanism | Value |
|---|---|---|
| **The floor** | slot count — hybrid has **12 roles**, dropping **200‰** | **800‰ = 80%** of a pure frame |
| **The recovery** | a **frame-mix bonus** keyed on `min(humanoidCount, plantCount)` across equipped items | up to **+200‰**, back to parity |

Starting breakpoints, tunable in `data/tuning/` — a balance pass moves them with a file save:

| Items of the **minority** frame | Effective budget |
|---:|---|
| 0–1 | **800‰** — the floor |
| 2 | ~870‰ |
| 4 | ~940‰ |
| 6 (a 6/6 split across 12 roles) | **~1000‰** — parity |

#### ⭐ Why this kills the cheat without a rule

The bonus keys on the **minority** frame, so **cherry-picking and the bonus are mutually exclusive**:

- **Cherry-pick** — take the strongest 12 base types regardless of frame. If the better pick is
  humanoid in 10 of 12 roles, you take them, the minority count is 2, and you sit near the floor.
- **Balance** — deliberately take the *worse* base type in some roles to hold a 6/6 split. You reach
  parity, and you have paid for it in per-slot quality.

**A hybrid cannot have both.** That is the whole design: the advantage the owner named — *"can equip
the strongest equipment from both types"* — is still available, and taking it now costs the bonus.
A hybrid at full strength is one that worked for it, and hybridity becomes an **active** choice rather
than a passive bonus.

This also supersedes **I2 §4.2's pricing argument**, which is wrong for a reason worth keeping. I2
computed the breadth gain with order statistics on uniform roll quality (*"the expected best of N
candidates is N/(N+1) of the range… a ~4.7% lift"*) and concluded 10.5% already over-priced it. **That
measures the wrong thing.** A hybrid is not *sampling* a larger pool; it is *choosing* the better of two
**designed** base types, once per role, on an axis the designer controls. Order statistics does not
apply to a deliberate pick, and I2 §4.2's *"overshooting by ~5% is the cheaper error"* rests on it.

#### Which 200‰ is dropped

**`ward-array` (90) + `head-guard` (60) + `sense` (50) = 200‰ exactly → 800‰, 12 roles.**

⚠ **This reverses the recommendation made earlier the same day**, and the reversal is instructive.
Under the old framing (*price the cherry-pick*) the best drop was `footing` — the one role I2 marks
*"frame-split by design"* — precisely because it carried the largest frame difference. Under the new
framing that is exactly backwards: **frame-differentiated roles are the engine of the choice**, so
removing the clearest one would shrink the tension the whole mechanism runs on. `footing` stays.

| Dropped | ‰ | Fiction |
|---|---:|---|
| `ward-array` | 90 | A chimera has no coherent outer layer — a body half bark and half bone does not shed a single sheath (I2 §4.2's own reasoning, unchanged) |
| `head-guard` | 60 | A two-natured head has neither creature's clean guard |
| `sense` | 50 | …nor either's clean senses. One fiction covers both: the head is the part that agrees least |

**Deliberately kept:** `footing` (the frame-split showcase), `mantle` (I2 §10.2 warns against taking
elemental resistance from the frame most likely to face mixed damage), `girdle`, `manipulator`,
`retinue`, `infusion`, both jewels, both armaments, `core-guard`.

**The families are not lost, only the slots** — following I2 §4.2's established pattern for
`ward-array`, whose shield families relocate to `core-guard` at `max_tier = 3` against `ward-array`'s
`5`. `head-guard`'s cluster (crit resist, crit-damage padding, status resist, immunity) and `sense`'s
(accuracy, crit rate) relocate the same way and at the same reduced tier, adding competition inside a
fixed budget rather than adding budget. **I2 owns choosing their hosts.**

#### Implementation — no new machinery

The bonus is an **effect container granted at `OwnerKind.UniqueActor`**, carrying derived-channel atoms
worth the recovered budget — structurally a set bonus. Set machinery already does *"count equipped items
matching a predicate, grant at breakpoints"* ([ssot-sets.md](item/ssot-sets.md) §3.1), and the durable
per-actor owner scope it needs was approved 2026-09-02. The predicate is a count over equipped items'
frames, which is one query against `rpg_item_assignment` (I13 §4.4).

#### Two consequences this creates

1. ⛔ **A hard requirement on I3 (base types).** The design collapses if humanoid and plant base types
   are numerically similar — there would be nothing to cherry-pick, no tension, and the bonus would be
   free. **Every role must have meaningfully different humanoid and plant base types.** This is now a
   correctness condition on I3, not a flavour preference.
2. ⚠ **I5 §3.7 is contradicted and must be updated.** It states *"A hybrid pays for breadth in slot
   count — and only in slot count."* That stops being true: hybrids pay in slot count **and** recover
   through an active constraint. §3.7 was written assuming a flat price, and everything else in it —
   frame-neutral sets, at most one member per `(role, frame)`, `SetRoleNotUniversal` — still holds.
   ⚠ Note also that its guarantee *"a set's member roles must all be in the hybrid role core"* now
   excludes three roles rather than two: `ward-array`, `head-guard`, `sense`.

**Frames, final shape:**

| Frame | Roles | Budget | Note |
|---|---:|---:|---|
| pure humanoid / pure plant | **15** | 1000‰ | commander included — no 16th slot (D14) |
| hybrid, unmixed | **12** | **800‰** | the floor |
| hybrid, 6/6 mixed | **12** | **~1000‰** | parity, bought with per-slot quality |

### D4 — v1 content reaches ilvl 32: the whole ladder

> *"Whole ladder to ilvl 32."*

Settles **d4 §7.5** and **README #4** (*"max reachable item level is 11 today… 40% of the tier ladder,
the enhancement risk band, and rarity rungs 80–100 cannot drop"*). Content is authored to ilvl 32 so
that half the round's design stops being unreachable.

Two consequences follow immediately, and both were already written down waiting for this answer:

1. **d4's ~880-cell cut is ON.** It was sized for exactly this choice.
2. **d4 §6.5's *"what must move to hit it"* list becomes live work**, not a contingency.

### D5 — ⭐ The blocking amendments ship as the inventory feature, not as a schema patch

> *"No, we need inventory feature, make it category and list first, reserve and share for all for now,
> we will add inventory management mini game in future."*

This declines the framing (*"authorize two amendments"*) and replaces it with the right one. **R1's
"second reachability root" is not a patch to the orphan sweep — it is ownership, and ownership is the
inventory.** An unequipped item is not unreachable; it is *in the armoury*.

**And this is already I13's design**, which is why the redirect costs nothing:

| Owner's words | Already specified as |
|---|---|
| *"we need inventory feature"* | `rpg_item` — the thin durable row above the instance, PK `instance_id`, carrying `player_id` ([ssot-inventory.md](item/ssot-inventory.md) §4.2) |
| *"share for all"* | **one player-scoped armoury** — *"no per-specimen bag, no bank, no stash tab, no warehouse"* (§2.3) |
| *"category and list first"* | the v1 surface. Unlimited capacity (§3.2); comparison and loadouts exist but are not the first cut |
| *"inventory management mini game in future"* | §2.5's five pressures — deferred, and explicitly not a bag limit |

**So R1 is closed by construction:** `rpg_item.player_id` is the ownership root, unequip becomes
*"assignment deleted, item still owned"*, and the orphan sweep at
`RpgStore.AtomInstances.cs:607-620` must be taught to treat an owned item as reachable. That is one
predicate, in service of a feature that was going to be built anyway.

> ⛔ **R2 is NOT closed by this and remains open.** `ResolveBindings` still refuses on strict
> `catalog_revision` equality (`RpgStore.AtomInstances.cs:436`), so one content import silently
> disables every rolled item every player owns. I13 anticipates the *shape* of the answer — `rpg_item`
> carries a `stale` flag *"set by the importer when an atom beneath it is disabled"* (§4.2, §5.6) — but
> **the refusal path itself is unchanged, and a `stale` flag is a report, not a fix.** This is the one
> live defect this round did not clear. It is listed in §2c.

### D6 — Offence/defence: mild drift toward defence (~3:1, the Diablo II shape)

> *"I prefer option 1, this game have many zombies in lawn game, if combat is so long cause the run
> become longer but dont make it so fast, zombie immediately die with cast any action is not fun."*

Defensive channel families get slightly steeper bands than offensive ones, so effective HP ends up
roughly **3× ahead of damage** across the ladder. Rejected: flat 1:1 (no shipped ARPG does it) and the
8×+ PoE/D3 shape.

**The reasoning is lawn-specific and it is a real constraint, not a preference.** The lawn presents
*many* targets, so per-target time-to-kill multiplies into run length in a way it does not in a
duel. The target is a readable middle band: a kill should feel earned, and a lawn full of zombies
should not stretch the run.

**The lever is per-channel-family band assignment in `data/tuning/`, never the `P(Θ)` curve.** `P(Θ)`
sets the shape and is pinned at `P(20) = 680`; the ratio is a band-assignment table, so a balance pass
moves it with a file save. Writing this ratio as a second curve would be exactly the defect the power
SSOT exists to prevent.

⚠ **Open consequence, stated because it follows directly:** the reasoning is about *the lawn*, and
battle is a different shape — few high-value targets rather than many cheap ones. **One ratio may not
serve both.** Whether lawn and battle share a band table is now a question; it is in §2c.

### D7 — Crafting reaches t5. There is no drop-only band. It is gated by cost, never by luck

> *"Option 2 but need rarity loop and price for material and success chance. So strong affix will cost
> much. Looking for a perfect item (of course very op item) will be cost very much effort but dont make
> it impossible by chance, that is not fun."*

Any tier including **t5 is craftable**. Last Epoch's drop-only T6–T7 band is **deliberately rejected**,
and the reason is worth keeping: **LE makes the ceiling a *find*; we make it a *cost*.**

**The principle this states, which is bigger than the crafting lane: effort, not luck.** A perfect item
must be reachable by grinding. It may be enormously expensive. It may never be *impossible*.

| Requirement | Owns it | State today |
|---|---|---|
| Material cost scaling steeply with affix tier and rarity | I9 [ssot-materials-crafting.md](item/ssot-materials-crafting.md) — the cost vocabulary | ⛔ **No tier-keyed cost curve exists** |
| A success chance on strong crafts | I7 [ssot-reroll.md](item/ssot-reroll.md) | ⛔ Not designed |
| **Bad-luck protection — mandatory, not optional** | I7 / I1 §2 (*"whether bad-luck protection exists and what it may key on"*) | Precedent exists: `rpg_summon_pity` |
| A "rarity loop" — rarity feeding the crafting economy back | I1 + I9 | Named, undesigned |

⚠ **Two repo rules this must obey, named now so the lane does not rediscover them:**

1. **A steep cost curve facing a scaling sink is a cap.** `ssot-power-scale.md` §11 is explicit that a
   ceiling need not be a `const` — *"a flat rate facing a scaling sink"* counts. The crafting cost curve
   must therefore be a **configurable soft cap**, never a hard stop, and it must live in
   `data/tuning/`.
2. **"Impossible by chance" is what pity exists to prevent.** A success chance without a floor is a
   lottery, and the owner ruled lotteries out by name. The floor is a design requirement, not a
   nicety.

### D8 — Items may grant aptitude points, gated by rarity

> *"Option 3 but need rarity mechanism."*

Aptitude-granting affixes are legal on any item, but **which rungs may roll them is rarity-gated**.

**This stays inside every invariant, and it is worth showing why rather than asserting it.** Rarity is
controlling *which affixes are available* — breadth — not *how large an affix rolls*. That is precisely
what §2a.5's prior art calls the genre's most reliable finding (*rarity buys breadth and ceiling, never
power*), and it does not touch I1's rule that rarity may never change a magnitude.

**The mechanism already exists and needs no invention:** `eligibility-tags` (effect-pipeline module 8)
plus its per-container allow/deny override. An aptitude affix carries a tag that only high rungs admit.

⚠ **The power consequence must be sized, not hand-waved.** One aptitude point feeds *several* derived
channels, so an aptitude affix is **multiplicative against additive ones**. Left unpriced, aptitude
affixes dominate every build and channel affixes become filler — the failure mode named in the option
itself. I8's tier bands need an aptitude row priced against that multiplication, and E9's power model
is the thing that can produce the number.

### D9 — R2 closes: per-atom compatibility replaces the revision equality check

The refusal at `RpgStore.AtomInstances.cs:436` — `instance.CatalogRevision != current` → `StaleInstance`
— **is removed**. An instance is judged by its atoms: each must still exist, still be enabled, and have
unchanged identity-defining fields (a per-atom content-hash compare).

**The blunt check was already redundant, which is why this is cheap.** Nine lines below it,
`ResolveBindings` walks `instance.Atoms` and refuses per atom with *"`{atomId}` is no longer in the
catalog"*. The revision test is a coarse pre-filter for something the loop underneath already does
precisely — it just does it with a sledgehammer, failing every instance in the database because *any*
row anywhere moved. What the per-atom loop does **not** catch today is an atom that still exists but
*changed*; the content-hash compare is the one genuinely new piece.

**Why the frozen values make this safe.** `effect_instance_atom` stores the resolved magnitudes. An
instance does not need the catalog it was rolled against in order to know what it does — only to
*re-derive* itself, which bind time never asks for. The `:435` comment (*"reproducing it would need the
catalog it was rolled against, which we do not keep"*) is true and does not apply to binding.

**Effect: a content patch invalidates exactly the items it touched, and nothing else.** This closes
§2c #1 and, with D5, retires both blocking amendments the round opened with.

### D10 — One band table, one scalar per runtime

Refines **D6**. Lawn and battle share a single offence/defence band-assignment table that owns the
*shape*; each runtime carries one multiplier against it, in `data/tuning/`.

```
bands.v1.json        <- the shared shape (D6's ~3:1 drift)
  lawn.ratioScale    = 1.0     <- D6 was reasoned from the lawn
  battle.ratioScale  = <tunable>
```

**Why not two tables.** Two independent tables is the closest shape in this design to the private-curve
defect the power SSOT exists to end. Bands are not curves, so it is not the same violation — but the
failure mode rhymes, and *"three incompatible curves shipped at once"* is in the repo's history because
nobody made the shared thing shared. One table plus a scalar keeps a single source of truth and still
lets a lawn full of cheap targets and a battle of few expensive ones diverge.

Closes §2c #2.

### D11 — ⛔ Frame differentiation: directional profiles **and** distinct implicits

**Closes §2c #7 — the one item that blocked authoring.**

For every role, the humanoid and plant base types differ on **two** axes:

1. **A directional stat profile within the same budget** — the two lean opposite ways.
2. **A distinct implicit** — a different always-on modifier on each.

I2 §2.6 already writes the pattern for one role, and it is the template:

| Role | Humanoid | Plant |
|---|---|---|
| `footing` | `feet` — evasion, movement, initiative | `roots` — stability, regeneration, resource draw |

**⭐ The load-bearing property is *directional*, not vertical.** Neither side may be strictly better.
If humanoid `torso` simply beats plant `stem`, every hybrid takes `torso` in that role, the pick stops
being a decision, and **D3's mix bonus becomes free** — the 80% floor turns into theatre. So the
constraint on I3 is not *"make them different"*; it is:

> **For every role, there must exist a build for which the humanoid base is correct and a build for
> which the plant base is correct.**

That is checkable, and it should be a lint rather than a review note. A role where one frame dominates
across all builds is a content defect with a name, not a matter of taste.

**Cost:** the highest-authoring option, landing on all 43 base-type identities (d4 §1.1, I3). Accepted
deliberately — it is the guarantee that D3's whole mechanism works, and D3 is a headline feature.

### D12 — ⭐ Sets and charms are **generated at roster scale**, not hand-authored

> *"each demon specie have 1 set and 1 charm. each primary stat build have 30 set for 10 rarity, 1
> offense set and 1 defense set and 1 balance set. they are parameter for LLM resolve, so we have more
> than 1500+ set and charm."*
>
> *"also item generator pipeline will depend on demon specie, same as action generator."*

This does not size d4's two UNSIZED entries. **It moves them to a different column.**

#### The arithmetic

| Population | Shape | Today | Full roster |
|---|---|---:|---:|
| **Build sets** | 12 aptitudes × 3 archetypes (offense · defense · balance) | **36** | 36 |
| **Species sets** | 1 per demon species | **84** | ~904 |
| **Species charms** | 1 per demon species | **84** | ~904 |
| **Total** | | **204** | **~1,844** |

> **Amended by D15 the same day.** The first cut of this table read *"× 10 rarity rungs = 360 build
> sets"*, giving 528 / ~2,168. **D15 puts rarity on the member pieces rather than on the set**, so
> there are **36 build set families**, not 360, and the totals fall to 204 / ~1,844.

At d4's ~11 rows per set that is roughly **1,320 generated rows today, ~10,340 at the full roster** —
against an item corpus that already holds 1,438 entries and ~980 atom rows, so the scale is ordinary
for generated content and impossible for hand-authoring. **That is the point.**

#### The authored surface is almost nothing

Every input already exists as a closed vocabulary:

| Input | Where it already lives |
|---|---|
| 12 aptitudes | `Stats/Aptitudes/Aptitude.cs:40-51` — shipped |
| 10 rarity rungs | the `rarity` table — shipped |
| demon species + motifs + anti-motifs + themes | `data/seed/demons/_registry/themes.v1.json` — **built** (seedsmith D4) |
| **3 archetypes** (offense / defense / balance) | ⬅ **the only new authored vocabulary in D12** |

**So d4 §1.1's two UNSIZED rows resolve to roughly three authored rows plus a pipeline**, and the
~880-cell hand-authoring cut does not grow. This is the cleanest outcome available and it was the
owner's, not the lane's.

#### The bridge already exists and is already built

The owner's *"item generator pipeline will depend on demon specie, same as action generator"* is not a
new dependency to design — it is [`spec-demon-themes.md`](seedsmith/spec-demon-themes.md), seedsmith
feature 2 module D4, **built 2026-08-31**:

- §2.1 *"Why items are not a demons kind"* — a demon must not become an item kind, because
  `Corpus.load(root)` is single-root and items live in `data/seed/items/`. **A demon is a *theme*
  instead**, and items reference it.
- §2.2 *"The bridge is a registry, and it goes one way"* — demons **publish**
  `data/seed/demons/_registry/themes.v1.json`; items **consume** it as a legal `themeKey` vocabulary.
  Nothing in the demons corpus reads an item; nothing in the items corpus writes a demon.
- **`set` already *requires* `themeKey`** (`adapters/items/kinds.py:63`), and **30 sets and 8 uniques
  already carry a theme** in the live corpus.

So *"one set per species"* is the theme registry at roster scale, running through a seam that was
designed for exactly this and already has 38 instances to pattern-match against.

#### Four consequences, three of them real risks

1. ⛔ **Generated sets may use only the 12 hybrid-core roles.** I5 §3.7 guarantees hybrids can complete
   every set by requiring *"a set's member roles must all be in the hybrid role core"*, enforced at load
   as `SetRoleNotUniversal`. **D3 just shrank that core to 12** — `ward-array`, `head-guard` and `sense`
   are out. A generator producing ~2,000 sets will hit this validator constantly unless the role pool is
   constrained up front. **This is a generator input, not a validation afterthought.**
2. ⚠ **The dead tail is a different failure from set jail, and I5 only covers set jail.** §3.5 asks what
   prevents one set dominating. At 2,168 sets with ~5 actors deployed at a time, the live question is
   the inverse: **most sets will never be seen by anyone.** The repo's own research measured this
   ([03-roster-scale.md §5](../research/game-design/03-roster-scale.md)). It is not automatically a
   problem — a species set is content that exists *because that species does* — but it must be a
   deliberate position rather than a surprise.
3. ⚠ **The two populations have different distinctness economics, and only one is safe by construction.**
   The 360 build sets are a **12 × 3 × 10 grid** — 25 authored values producing 360 identities, which is
   precisely the *"orthogonal axes beat a long flat list"* finding (Ragnarok: 27 values across 4 axes →
   417 identities). The ~904 species sets are a **flat list**, and the research says taxonomy
   vocabularies stop growing at n≈300. **The mitigating difference: a species set does not need to be
   distinct from 903 others — it needs to feel like *that species*, whose identity the player already
   knows.** The bar is recognition, not differentiation. Worth stating because it is the only reason the
   flat list is defensible.
4. ✅ **Closed by D15.** *"30 set for 10 rarity"* was ambiguous; rarity turns out to live on the member
   pieces, not on the set. **36 build set families**, no per-rung duplication, and no dead sets.

### D13 — E9, the power model, is in scope: the item program builds it

Three lanes block on it (`ssot-item-categories` §10.7 — the ≤15% implicit budget cap;
`ssot-granted-actions` §10.6 — whether a granted action costs power budget;
`ssot-presentation` §10.7 — whether a power number is shown at all), and **D8's aptitude-affix pricing
needs it too**. Rather than wait on another program's queue, the item program owns it.

⚠ **The risk this accepts, named so it can be designed against rather than discovered.** E9 is not an
item concept — demons, actions and items all produce magnitudes and all need "how strong is this
thing?" answered the same way. Building a general system *inside* one consumer is precisely how a
general system becomes consumer-shaped by accident, and this repo has already ruled on that exact
pattern once: `provenance-supersede` was moved to seedsmith **core** rather than left in the demons
feature, because *"burying a general fix inside a demons module is how it becomes demon-shaped by
accident"* (`seedsmith-map.md` §3b).

**So the ruling is *who builds it and when*, not *who it serves*.** The mitigation is one sentence and
it belongs in the module spec:

> **E9 is authored as a general power model with no item-specific concepts in its interface.** Items
> are its first consumer, not its subject. A demon or an action must be able to read it without an
> item-shaped adapter.

It also reads `P(Θ)` and adds nothing to the ladder — power is *evaluation* of a thing that exists,
where `P(Θ)` is *derivation* of a magnitude from a level. Confusing the two would create the private
curve the power SSOT exists to prevent.

### D14 — The commander is another unique demon. `standard`, artifacts and commander sets are out of scope

> *"i missing for commander sets, this will decide later, not in this scope, commander specific set
> depend on commander role/class that i don't have idea yet, so commander for now consider as other
> unique demon, not much different if it don't have passive skill and artifact (specific item for
> commander, we will discuss later in other scope, not this item generator)."*

**For this program, a commander is an actor with the same 15 roles (12 hybrid) as any unique demon.**
No 16th slot, no squad-scoped bindings, no commander-only content.

**Direction acknowledged, scope declined.** Match-scope `standard` atoms are the right shape *when*
commander gear exists — but commander-specific gear depends on a **commander role/class system that
does not exist yet**, and designing gear for an actor whose class model is undefined is authoring
against nothing. That is the same mistake README #4 named for the item-level ladder.

**Deferred out of scope, and reserved rather than discarded:**

| Concept | State |
|---|---|
| `standard` slot (match-scope atoms buffing the squad) | **Reserved.** item-ideal §5.6's design stands; nothing generates into it |
| **Artifact** — a commander-only item type | **Reserved, named by the owner.** New concept; no lane owns it |
| Commander-specific sets | **Blocked** on the commander role/class system |
| Commander passive skills | Out of scope — not an item concept |

**This closes three lanes' open questions at once**, all of which orbit the same absent system:
`ssot-sets` §10.1 (does a commander's set bonus buff the squad?), `ssot-charms` §10.3 (does the
commander change anything here?), `ssot-sockets` §10.3 (does the commander get more sockets?). **The
answer to all three is: no, because the commander is not special in this program.**

Amends **D3**: the commander is 15 roles pure or 12 hybrid, with no `standard`. Every budget figure in
D3 is unchanged, because `standard` was always additive to the 1000‰ rather than part of it.

⚠ **I2 §2.9 and §5.2 both carry `standard` as a 16th `item_role` row.** It stays declared and
ungenerated — the same disposition seedsmith gave the `environment` kind, and for the same reason:
*"the kind costs nothing and keeps the adapter shape stable"*, while generating into it would make
coverage report a partition covered when nothing real is there.

### D15 — Rarity is the quality of a set's member pieces, not a property of the set

**36 build set families** — 12 aptitudes × 3 archetypes (offense · defense · balance). **Not 360.**

D12's first cut read *"30 set for 10 rarity"* as ten sets per (aptitude, archetype). It is not: a
*Might / offense* set is **one** set, and you complete it from pieces of whatever rarity you have.
Completing it from `almanac` pieces is stronger than completing it from `grafted` pieces — same set,
better members.

| | Rejected: ten sets per build | ✅ Chosen: rarity on the pieces |
|---|---|---|
| Build sets | 360 | **36** |
| Progression | swap to a different set at each rung | **upgrade the pieces, keep the set** |
| Dead content | 9 of 10 die the moment you out-level them | none |
| I1 overlap | must decide whether a rung-30 set beats a rung-90 one | not a question — sets have no rung |

**Why this is the right answer and not merely the cheaper one:** it is how rarity already works
everywhere else in the system. Rarity is a property of a **container** — it selects `prefix_rolls`,
`suffix_rolls` and the tier window for *that item*. A set is not a container of rolls; it is a
threshold over equipped pieces. Giving a set a rarity would have invented a second meaning for the
word, which is the category error [ssot-rarity.md](item/ssot-rarity.md) §4.3 already had to correct
once when *unique* and *set* were listed as rarity rungs.

**It also removes a failure mode nobody had to solve.** Ten sets per build means nine of them become
dead content the moment a player out-levels them — the dead tail (D12 §2) arriving by construction
rather than by scale. At 36 families it does not arise.

**Revised totals:** 36 build sets + ~904 species sets + ~904 species charms = **~1,844** at the full
roster, ~204 today. Consistent with the owner's *"more than 1500+"*.

### D16 — The ~110 lane-internal picks are ratified as a batch

Every lane author's stated recommendation **is the decision.** They remain reversible — each one names
its own alternative, which is why this is safe — but they stop being an open queue.

**Revisit trigger, stated so "reversible" means something:** a ratified pick is reopened when it
*bites* — a test fails against it, a downstream lane conflicts with it, or a balance pass finds the
number wrong. Not at the start of a session, and not because someone is reading the lane for the first
time.

**Why batching is correct here rather than lazy.** These are not unanswered questions; they are answers
with an escape hatch attached. Leaving ~110 of them nominally "open" is what produced a document set
nobody could tell was finished. A recommendation nobody disputed is a decision, and writing that down
is the difference between a lane that is done and a lane that merely stopped.

### D17 — The dead tail is accepted: a species set exists because the species does

~904 species sets and ~904 charms, with roughly five actors deployed at a time. Most will never be seen
by any given player. **That is fine, and it is a position rather than an oversight.**

**The bar for a species set is recognition, not differentiation.** It does not need to be distinguishable
from 903 others; it needs to feel like *that demon*. A player who captures a species finds gear waiting
that belongs to it — and that is the whole value, delivered per player rather than per corpus.

**What makes this affordable is D12.** The cost of an unseen species set is generation tokens. There is
no authoring, no per-set balance pass, and no maintenance. The research's dead-tail warning
([03-roster-scale.md §5](../research/game-design/03-roster-scale.md)) measured games where a dead unit
cost a design and art budget; a generated set costs neither.

**Rejected, with reasons worth keeping:** *one set per family* (19 sets, and it matches the
"orthogonal axes beat a flat list" finding exactly) was refused because a species would stop having
*its* set, which is the identity the owner asked for. *Tier it by rank* was refused because rarity is a
**snapshot, not an attribute** (`seedsmith-map.md` §3b) — a growing roster moves species between tiers,
so a species could silently lose its set.

⚠ **Note D15 already removed the other dead tail** — the constructed one, where nine of ten build sets
died the moment you out-levelled them. This ruling is only about roster scale.

### D18 — ⭐ Drop volume reads the power ladder, not a private curve

> *"scale with player level, number of run in pvz and world stage, for world stage we don't have yet
> because world map still building. this is same fomula as power scale function."*

Drop volume is a function of **`Θ`**, composed exactly as
[`ssot-power-scale.md`](power/ssot-power-scale.md) §5 already composes it:

```text
Θ_actor = Wd·daveLevel + Wa·realmsAdvanced + Wr·runTerm(pvzRuns)
```

The owner's three inputs map one-to-one onto terms that already exist: *player level* is `daveLevel`,
*number of runs in PvZ* is `runTerm(pvzRuns)`, and *world stage* is the content ladder's `worldTier` /
`mapLevel(M)`.

**This is the single most important property of the answer:** it means drop volume is **not a new
curve.** The item program does not get a private `f(level)` for loot — which is the exact defect
`ssot-power-scale.md` exists to prevent, and the one that let three incompatible curves ship at once.
I12's calibration stops being a fixed point and becomes a **pin** on a ladder that already exists.

**The world-stage term is zero today, and that is graceful rather than broken.** The world map is still
being built, so `Ww` contributes nothing yet. A weighted arithmetic sum degrades to its available terms
without special-casing — which is why §5 chose a weighted sum over a product.

⚠ **Which read of the ladder: `Θ`, not `P(Θ)`.** PS-3 is explicit — *contests read `Θ`, magnitudes read
`P(Θ)`*. A drop **count** is neither a contest nor a magnitude; it is a rate. It must read **`Θ`
linearly**, because `P(Θ)` is quadratic and quadratic growth in item *count* floods an inventory whose
management minigame is deferred by D5. **Drop quality already reads `P(Θ)`** through the rarity and
tier path, so the split is clean:

| | Reads | Why |
|---|---|---|
| **How many items drop** | **`Θ`** (linear) | a rate. Quadratic counts flood the armoury |
| **How strong they are** | **`P(Θ)`** (triangular) | a magnitude, through the existing rarity/tier window |

**This also answers §2c #5 (the "ownable" number) by dissolving it.** There is no fixed ownable figure:
what a player can own is whatever their `Θ` has earned them. I12's *"75 slots ≈ 10 days"* becomes the
calibration point at one `Θ`, not a global claim.

### D19 — I11 splits: the equip gate stays, per-species aptitude vectors go to the demon program

> *"I11 is set/charm atom effect distribution? if true use option 1."*

⚠ **The premise is corrected: I11 is not that.** [`ssot-requirements.md`](item/ssot-requirements.md) is
**the equip gate** — what an item demands before an actor may wear it (frame, level, and a proposed
attribute requirement) — plus a primary-attribute proposal that the twelve shipped aptitudes have since
overtaken.

**Option 1 is applied anyway, because the correction strengthens it rather than undermining it.** Once
I11 is understood as the equip gate, the split is obvious rather than merely defensible:

| Stays in I11 | Moves to the demon program |
|---|---|
| The equip gate: **frame + level**, and any faction clause | **Per-species aptitude vectors** — `84 → ~904` species × 12 aptitudes |
| Which level `level_req` compares against (specimen or account) | Their growth curves |

**Why the vectors are not item data.** A per-species aptitude vector describes *a species*, exactly as
its stat block does — it is true whether or not the species ever equips anything. D8 already
established that **an aptitude is a source, not a registered channel**, and the demon program already
owns `DemonSpeciesDef`. Keeping species data in the item lane would put one program's content in
another's document, which is the same boundary error `spec-demon-themes.md` §2.1 refused when it
declined to make items a demons kind.

**This retires I11's stale sizing** (§2c #4): `24 species × 5 attributes` was never going to be right,
and it now leaves the item program entirely rather than being re-sized here.

> ⭐ **A real gap the question exposed.** *"Set/charm atom effect distribution"* — deciding **which
> atoms a generated set or charm actually grants** — **has no lane.** I5 owns thresholds and membership,
> I10 owns charm capacity and resonance, I8 owns affix distribution for *items*. Under D12 sets and
> charms are generated, so their effect distribution is a **generator input**, and nobody owns it.
> Added to §2c.

---

### 2b.1 Resolved by recommendation — reversible, say so if wrong

Four open questions had a defensible answer and no product content. Recorded as decided rather than
left open, because an answerable question is a task ([no-manufactured-uncertainty](../DESIGN-GATE.md) §5).

| # | Question | Decision | Why |
|---|---|---|---|
| d4 §7.4 | Is `item_role_family` derived or authored? | **Derived**, with a small override list | Saves ~1,100 hand-authored cells and removes a second source of truth. The only argument for authoring is per-`(role, family)` `max_tier` granularity, which I2 uses for **exactly one thing** — the `max_tier = 3` cap on the twin minor jewels. One override beats 1,100 cells |
| README #3 | Reason-code surface: 33 → 101, or one namespaced code? | **One namespaced `ContentRuleViolated`** | 101 codes is a vocabulary to maintain, document, and keep in sync with the FE forever. A namespaced code carries the same information in its payload and costs nothing to extend |
| I2 §10.2 | Which roles does hybrid lose? | ⚠ **Superseded the same day — see D3.** Now **three** roles totalling 200‰: `ward-array` + `head-guard` + `sense` | The question changed under it. The owner reset the hybrid price from 10.5% to **20% as a floor that mixing earns back**, so the drop had to reach 200‰ rather than 105‰ — and the *criterion* inverted: frame-differentiated roles must be **kept**, because they are what the mix bonus runs on |
| d4 §7.7 | Size the four gap lanes before committing the cut? | **Yes — size G1–G4 first** | G1 (uniques) is *"the one that breaks the generator's rules on purpose"* — hand-authored by definition, so the most expensive rows per unit in the program. Cutting to 880 hand-authored cells is worth little if G1 then adds 300 by hand |

---

### 2c. What is still open after this round

**Fifteen rulings (D1–D15) plus four resolved by recommendation.**

#### The honest denominator

The seventeen lane SSOTs and four decision documents carry **144 numbered open questions** between
them. That number is misleading, and the shape matters more than the count.

**Most of the 144 are not blockers.** They share one form: *"I picked X. The alternative is Y. Confirm
or overrule."* Every lane author made the call and named the escape hatch. Treating them as a queue
would manufacture uncertainty — they are **decided unless disputed**, and the moment to revisit one is
when it bites, not before.

**This round closed roughly 25 of them, and three rulings did most of the work:**

| Ruling | Lanes it closed | Evidence |
|---|---|---|
| **D1** — roster scale | **7** — `affixes` · `enhancement` · `materials-crafting` · `reroll` · `sets` · `sockets` · `generation` | d4 §7.1: *"nine of the thirteen lanes name this as their largest unknown"* |
| **§2b.1** — one namespaced `ContentRuleViolated` | **8** — `charms` · `enhancement` · `equip-slots` · `inventory` · `rarity` · `requirements` · `reroll` · `uniques` | each independently asks whether N new codes against a closed 33 is too many |
| **D14** — the commander is not special | **3** — `sets` §10.1 · `charms` §10.3 · `sockets` §10.3 | all three orbit a commander role/class system that does not exist |

Platform findings retired several more that were never decisions: `OwnerKind.UniqueActor`
(`requirements` §10.7), `rpg_item` (`rarity` §10.3), `OnActivate` (`consumables` §10.5), and the twelve
aptitudes (`requirements` §10.1–10.3 **entirely**). §2a.5 also *verified* `affixes` §10.1's
*"PoE's 3 + 3, recalled, unverified"* — it is correct.

#### What genuinely remains

**Five items. None is a decision, and none blocks authoring.**

| # | Open | Kind | Why |
|---|---|---|---|
| **1** | ⛔ **Cap the set generator to the 12 hybrid-core roles** (D12 §1) | generator input | **The next thing to get right.** D3 shrank the core by dropping `ward-array`, `head-guard`, `sense`; I5's `SetRoleNotUniversal` fires at load, so ~940 generated sets trip it unless the pool is capped *before* generation |
| **2** | ⭐ **Set/charm atom effect distribution has no lane** (D19) | ownership gap | *Which atoms a generated set or charm grants.* I5 owns thresholds, I10 owns capacity, I8 owns affix distribution **for items**. Under D12 this is a generator input and nobody owns it |
| **3** | ⛔ **C3 and S2 — two confirmed defects** (§2e) | fix | `effect_atom.name` is never validated (`AtomRowValidator` reads only param names), and `effect_binding` has **no FK at all** where `definitions.md:317` promises `ON DELETE CASCADE`. S2 is R1's mirror image and belongs in D5's change |
| 4 | **`E42 units-correction` gates band → number resolution** (§2e, C1) | cross-program dep | Narrow, and it does **not** block authoring — `seed-contract.md` §3's band rule closes the units trap by construction. Owned by content-stack gate G3 |
| 5 | **Mechanical follow-through** | tasks | Re-derive I12's drop weights and I6's caps against **ten** rungs · apply the D3 edit to I5 §3.7 · move I11's per-species vectors to the demon program (D19) · a light-theme palette (`rarity` §10.7 and `presentation` §10.6 ask it independently) · tune the frame-mix breakpoints from play |

**Everything else is closed.** Nineteen rulings, four recommendations, D16's batch ratification, and
§2e's five verifications account for all 144 lane questions and every standing unverified claim.

---

### 2d. What this round did to the program's shape

Three structural changes, worth stating separately from the rulings that caused them.

**1. The item generator is now a seedsmith pipeline with a demon dependency.** D12 plus the owner's
*"item generator pipeline will depend on demon specie, same as action generator"* puts item content on
the same footing as the action corpus: it consumes
`data/seed/demons/_registry/themes.v1.json`, published one-way by seedsmith's `demon-themes` (built
2026-08-31). Items reference demons; demons never reference items. **This program therefore has an
upstream it does not own**, and that upstream is built.

**2. Hand-authoring shrank; generation grew.** d4's cut aimed at ~880 hand-authored cells. D12 removes
the two largest unsized entries from that column entirely and replaces them with ~4,900–13,900
*generated* rows plus three archetype definitions. The authored floor is now dominated by
`item_role_family` (~1,100) and I3's base types — and D11 just made the latter more expensive on
purpose.

**3. The program grew a module and shed a surface.** D13 brings **E9, the power model, in scope** — the
item program now builds the thing three of its lanes were blocked on. D14 pushes **commander-specific
gear out** — `standard`, artifacts and commander sets wait on a commander role/class system that does
not exist, so the commander is modelled as another unique demon. Net: one general system in, one
speculative surface out.

**4. Loot volume joined the power ladder.** D18 puts drop volume on `Θ` — the same composition every
other ladder-derived number uses — rather than on a rate table of its own. The item program therefore
adds **no private curve**, which is the defect `ssot-power-scale.md` exists to prevent. I12's *"75 slots
≈ 10 days"* becomes a pin on that ladder instead of a standalone claim, and the "how much gear can a
player own" question dissolves: whatever their `Θ` has earned.

**5. Two features now depend on base types being *directionally* different.** D3's mix bonus and D11's
authoring rule are the same requirement seen from two ends. If I3 authors humanoid and plant bases that
are numerically alike, D3 silently degrades to "hybrid is just worse" and nothing fails a test. That is
why D11 asks for a lint rather than a review note.

---

## 2e. The unverified claims, verified (2026-09-03)

[item/README.md](item/README.md) carried a standing caveat: *"Three defect claims are unverified
(C1–C3 in the handoff), one of which would change what a status magnitude means."*
[atom-layer-handoff.md](item/atom-layer-handoff.md) §7.4 was blunter — *"verify C1 and C2 before either
is used to justify anything."* They were never verified, and two structural claims sat beside them in
the same state.

**All five are now checked against code.** This is evidence, not a ruling — no decision was needed for
any of them, which is exactly why leaving them open cost more than closing them.

| Claim | Verdict | Evidence |
|---|---|---|
| **C1** — the `Increased`/`More` unit boundary: SC4 mandates integer per-mille, `StatComposer` reads fractions, no `/1000` was found. *"If real, `+15%` composes as ×151"* | ⚠ **Neither confirmed nor refuted here — it is the units question, and it belongs to another program** | `StatComposer.cs:25-32` does read fractions (`afterFlat * (1.0 + increased)`), and `DerivedComposer.cs:44` sums raw. But this is the same defect as **`E42 units-correction`** (content-stack gate G3), which exists because `definitions.md` §2 *"still calls three channel families 'resolver points'; they are flat game units"* |
| **C2** — `ComputeNetFactor` uses a raw delta as a direct multiplier on magnitude **and** duration, so `+1 status power` doubles every status. *"Blocks tier bands on two affix families"* | ✅ **WAS REAL. NOW FIXED — by a different program** | The power program's **audit F4** found the identical defect independently (`ssot-power-scale.md` §6.6: *"uses a raw difference directly as a multiplier on magnitude and duration"*, with a table showing +2 → 2.0× and a retired world → 25×). Its proposed fix is **shipped**: `ResistanceEvaluator.cs:347-348` now reads `Math.Clamp(1.0 + delta / StatusPolicy.NetFactorScale, …)`, and `StatusPolicy.cs:24` cites *"T3.2 (audit F4)"*. **The two affix families it blocked are unblocked** |
| **C3** — `effect_atom.name` is unvalidated; empty names load clean | ⛔ **CONFIRMED, still real** | `AtomRow.Name` exists (`AtomRow.cs:31`, defaulting to `""`) and `AtomRowValidator` never reads it — every `Name` reference in that file is `def.Name`, a *parameter* name. Small, isolated, and the handoff already files it as Stage-0 work |
| **S1** — *"`effect_binding` has zero production consumers — only `RpgStore.AtomInstances.cs` and two test files"* | ✅ **REFUTED** | `ProduceAndBind` is called in production at `RpgStore.UniqueActors.cs:756`, inside the live equipment-binding sync. True when filed; overtaken by `effect-pipeline` module 4 |
| **S2** — `definitions.md` §6 promises an `ON DELETE CASCADE` FK on `effect_binding` that the shipped DDL does not declare | ⛔ **CONFIRMED, still real** | `definitions.md:317` promises *"FK `ON DELETE CASCADE`; bindings go with it"*. The DDL at `RpgStore.AtomInstances.cs:83-97` declares **no foreign key at all** on `instance_id` — three indices and nothing else. And `definitions.md` **wins over any spec**, so the doc is not merely optimistic; it is authoritative and wrong |

### What this changes

**C2's outcome is the interesting one.** An item lane filed a defect it could not verify; the power
program found the same defect from the other direction, proved it with a table, and fixed it — and
nobody told the item lanes, which have been carrying it as a blocker on tier bands ever since. That is
the cost of an unverified claim sitting in a document: not that it was wrong, but that **nobody noticed
when it stopped being true.**

**C1 does *not* block item authoring, and the reason is a rule this program already has.**
[seed-contract.md](item/seed-contract.md) §3 forbids an author from ever writing a magnitude — *"an
author may write a count, a reference, an enum, or a band. Never a magnitude"* — and says why in a line
that now pays for itself:

> *"It also closes the units trap by construction: a band carries no unit."*

So authoring proceeds; only **band → number resolution** waits on `E42`, and that is `numerics`' job,
not an author's. The dependency is real and it is narrow.

**S2 interacts with D5 and D9 and should be fixed alongside them.** With no FK and no cascade, deleting
an instance leaves orphan bindings behind — the mirror image of R1, where deleting a binding took the
instance with it. D5 introduces `rpg_item` as the durable owner; the three tables' referential integrity
should be settled once, in that change, rather than three times.

---

## 3. What already exists — the substrate this inherits

The effect-atom program built the machine an item system needs. **E1–E6 are built and green**
([tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md)), which means the following is not a
proposal — it is shipped code and schema.

| An item system needs | Already exists | Where |
|---|---|---|
| An item **template** | `effect_container` with `container_kind='item'`, `slot`, `rarity`, `min_tier`/`max_tier`, `level_req`, `pool_rolls` | [spec-container-schema.md](effect-atom/spec-container-schema.md) |
| A tiered **affix pool** with one-mod-per-family | `effect_container_pool` — `weight`, `group` defaulting to `(family_id, variant)` | same |
| A **specific dropped item** with frozen rolls | `effect_instance` + `effect_instance_atom`, `roll_seed`, reproducible from `(container_id, catalog_revision, roll_seed)` | [spec-instance-and-binding.md](effect-atom/spec-instance-and-binding.md) |
| **Equipping** | `effect_binding` — instance → owner scope, with `slot`, `priority`, bind-time rejection | same |
| A **rarity ladder** with stable ordinals | the `rarity` table, explicit append-only ordinals | E5 |
| The **affix library** | ~71 authored families × 5 tiers ≈ 355 atoms, plus ~420 generated element rows | [atom-family-library.md](effect-atom/atom-family-library.md) |
| A **player-scoped stackable inventory** | `rpg_demon_materials(player_id, material_id, qty)`, seeded by expeditions with `essence.{element}` and `shard.{rarity}` | `RpgStore.cs`, `DemonMaterialCatalog.cs` |

**The law that comes with it:** *items have no behaviour; actors do*
([definitions.md](effect-atom/definitions.md) §0). An item is a **source** that puts atoms on an actor's
effect list. It does not participate at runtime, it is not an execution unit, and `seq` inside a
container is authoring order, not execution order.

Two more inherited rules that shape everything below:

- **Rarity picks affix *count* and the *tier window*; tier carries strength. Rarity may never change a
  magnitude.** That split is already enforced by columns, not by convention.
- **`group` defaults to `(family_id, variant)`**, so an item can roll fire power *and* ice power but
  never two tiers of the same affix — the rule that stops a rolled item reading `+10 atk / +12 atk`.

### What exists for items today, honestly

A stub and nothing more:

- `rpg_unique_equipment(instance_id, slot, item_id)` — three columns, PK `(instance_id, slot)`
  (`src/FusionRpg.Data/Sqlite/RpgStore.cs:356`).
- `UniqueEquipmentCatalog` — **3 hardcoded items**, slot allowlist `weapon/armor/trinket`, each mapping
  to one grant template folded into `mods_json`; one of the three points at a placeholder effect id
  (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs`).
- Battle reads equipment **nowhere**: `ChannelMods` is documented as "trait stat mods, equipment later"
  (`src/FusionRpg.Core/Battle/BattleModels.cs:20`).

~~There is **no player/commander actor**.~~ ⚠ **False as of 2026-09-03 (§2a, B9)** —
`rpg_player_commander(player_id, default_lawn_commander_id, …)` exists
(`RpgStore.PlayerCommander.cs:17`), and `OwnerKind.UniqueActor` was added 2026-09-02 as a durable
per-actor owner scope (B10). As written: `players` is `(id, name)`; a player owns souls, materials, and a
patron designation (`RpgStore.cs:413`). A commander with equipment is still largely a **new entity**, but
it now extends a row that exists rather than inventing one.

---

## 4. Frame — the key the whole system turns on

### Faction is not body

`DemonSpeciesDef.Side` is documented as *"linked capture side (plant | zombie) — portrait/body source"*
(`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs:11`) — one field carrying faction **and** body. The
generated roster is 18 zombie-side and 6 plant-side species, and several zombie-side entries are Fusion
hybrids: `peashooterzombie`, `ironpeazombie`, `cherrynutzombie`, `bucketnutzombie`
(`DemonSpeciesCatalog.Generated.cs`). A peashooter-zombie is faction-zombie with a plant body.

**So the item system must not key on `side`.** It keys on an explicit **frame**:

| Frame | Who | Wears |
|---|---|---|
| `humanoid` | zombies, humans, the human commander | human-like gear |
| `plant` | plants, the plant commander | plant-like gear |
| `hybrid` | Fusion crossbreeds — a headline feature of the base game, not an edge case | see §5.3 |

Frame is a property of the **species / body**, declared once. Faction stays a separate field and keeps
doing what it does — element rings, capture side, portrait source. Deriving one from the other is the
mistake this section exists to prevent.

### Three different things are called "slot" — do not share a type

This word is already overloaded in the tree, and [definitions.md](effect-atom/definitions.md) §6 warns
about two of the three explicitly.

| Meaning | Where | Note |
|---|---|---|
| **Equip slot** — where an item goes on a body | `effect_container.slot`, `effect_binding.slot` | this document's subject |
| **World construction slot** — a buildable position in a sector | the `slot:{id}` owner scope | *"unrelated to an item's `slot` column. Two different concepts, one word — do not share a type"* |
| **Expedition slot** — a parallelism gate (2 → 5 via progression) | [standalone-rpg-map.md](standalone-rpg-map.md) | unrelated again |

---

## 5. Slots

### 5.1 One role table, two vocabularies

> ⚠ **Superseded by [item/ssot-equip-slots.md](item/ssot-equip-slots.md).** The count is **fifteen**,
> not twelve. The three added roles were not padding — each took a genuinely homeless affix cluster:
> `ward-array` (the four shield families plus vanilla armour), `infusion` (the 21 status families, the
> library's largest orphan), and `retinue` (spawn, board, grid, terrain). `head-guard` was also
> redefined from "small torso" to disable-resistance, because it failed the merge test as written.
> Budget weighting is integer per-mille summing to 1000. The table below is kept as the shape of the
> idea; the role list itself lives in I2.

The design that keeps the item database from doubling: **the slot roles exist once**, and each frame
names them in its own fiction. Affix families are scoped to the **role**, so they are authored once and
serve every frame. Only **base types** are frame-specific.

| # | Role | Humanoid slot | Plant slot | What the role is for |
|---|---|---|---|---|
| 1 | head-protective | `head` | `crown` | the classic helm budget — defence with a small offensive rider |
| 2 | sense-utility | `face` | `bract` | accuracy, detection, crit-rate — the "how well it aims" slot |
| 3 | core-protective | `torso` | `stem` | the largest defensive budget on the body |
| 4 | mantle-utility | `back` | `canopy` | cloak / overhead shade — resistances, ambient mitigation |
| 5 | manipulator-offense | `hands` | `leaves` | attack speed, crit, on-hit riders |
| 6 | footing | `feet` | `roots` | humanoid = mobility; **plant = anchor** — same slot, different affix flavour (§5.4) |
| 7 | girdle-resource | `waist` | `soil` | the resource slot — pools, regen, economy, consumable capacity |
| 8 | jewel-major | `neck` | `pollen` | the amulet — the strongest single non-weapon affix source |
| 9 | jewel-minor A | `ring-1` | `graft-1` | |
| 10 | jewel-minor B | `ring-2` | `graft-2` | duplicated on purpose — see §5.5 |
| 11 | armament-primary | `main-hand` | `muzzle` | identity-defining: what this actor *does* |
| 12 | armament-secondary | `off-hand` | `thorn` | shield / focus / spines — the defensive or amplifying half |

Twelve per frame is deliberately rich — Diablo 2 shipped ten and Path of Exile nine plus flasks and
jewels, and the extra two here (`sense-utility`, `mantle-utility`) exist because this game already has
an accuracy/crit layer and a resistance layer deep enough to deserve dedicated homes.

### 5.2 The plant fiction, stated so it stays coherent

A plant is rooted, has no hands, and does not walk. The vocabulary must not be a humanoid one with the
words swapped, or it will read as a costume.

| Plant slot | The fiction | The mechanical consequence |
|---|---|---|
| `crown` | the bloom or canopy at the top | takes the helm's defensive budget |
| `bract` | the leaf collar around the bloom | targeting and perception affixes |
| `stem` | trunk, bark, husk plating | the core armour |
| `canopy` | overhead leaf spread | ambient and elemental mitigation |
| `leaves` | leaves as manipulators | fire rate, on-hit riders |
| `roots` | what it stands in, not what it walks on | **stability, regeneration, resource draw** — never movement speed |
| `soil` | the pot, the bed, the earth it occupies | sun / qi economy, resource pools |
| `pollen` | scent, spores, aura | the amulet-grade aura affixes |
| `graft-1/2` | grafted cuttings and scions | the ring-grade minor affixes |
| `muzzle` | the nozzle, seedpod, or barrel it fires from | the weapon |
| `thorn` | spines, burrs, the defensive husk | the off-hand |

### 5.3 Hybrid — flexibility paid for in slot count

A hybrid body can plausibly wear either vocabulary. Left unpriced, that is strictly better than both
pure frames — it sees twice the loot pool, so every drop is useful to it and hybrids become the only
frame worth playing.

**Proposal:** a hybrid gets **ten slots instead of twelve** — it loses `mantle-utility` and one
`jewel-minor` — and each remaining role accepts a base type from **either** frame. Breadth is bought
with depth. Alternative shapes worth arguing in review: a fixed per-role frame assignment per species
(no choice, but flavourful), or twelve slots with hybrids barred from set and unique items. Open.

### 5.4 The same role can mean different things per frame

`footing` is the honest example. On a humanoid it is movement, initiative, and evasion. On a plant,
movement is meaningless — a plant that "runs faster" is nonsense, and the affix would be dead content of
exactly the kind [atom-catalog-ssot.md](effect-atom/atom-catalog-ssot.md) §8a bans (*"a row no code
consumes is not content; it is a lie in a table"*).

**Rule: an affix family declares which frames its role serves.** The role is shared; the family list per
role is frame-filtered. That is one extra column, and it is what stops `+move speed` rolling on a
turnip.

### 5.5 Why two jewel-minor slots

Duplicated slots exist so a player can express *degree*: two rings means the choice is not "this affix
or that one" but "how much of this axis do I want". Diablo and Path of Exile both keep exactly two, and
both keep them the weakest per-slot budget precisely because doubling a strong slot doubles a strong
build. Keep the pair, keep the budget small.

### 5.6 Commander slots

The commander is a new actor (§3) and gets the full twelve of their chosen frame, plus a proposal:

**One commander-only role — `standard`** (banner, seal, sigil, or root-totem depending on frame) whose
atoms bind at **`match` scope rather than to the commander's own body**, so commander gear buffs the
whole squad. That scope already exists and needs no new mechanism, and it gives commander itemisation a
reason to be different from wearing thirteen copies of a demon's gear.

Also worth deciding early, because it is a progression lever rather than a content lever: **do slots
unlock with level?** Starting a commander at six slots and opening the rest over the campaign is a
cheap, legible progression axis, and it lets early loot matter without early builds being complete.

---

## 6. What an item is made of

### 6.1 Base type — the thing slots and implicits hang on

Every item has a **base type**: `plate-helm`, `iron-crown`, `pea-nozzle`, `bark-plating`. The base type
declares:

| Base type declares | Why |
|---|---|
| `frame` — humanoid / plant / either | which bodies may wear it |
| `role` — one of the twelve | which slot it occupies |
| `implicit` — zero or more fixed atoms | the modifier every copy carries, before any roll |
| `item_level` band | which tier window its affix pool may offer |
| `requirements` | level, and possibly faction or element |

**Implicits are the reason two items in the same slot feel different.** A crown that always carries
`+regeneration` and a crown that always carries `+fire power` are the same slot and the same affix pool,
but they are not the same choice. Without implicits, base types are cosmetic and every slot collapses
into "the one with the best roll".

### 6.2 The rolled part maps onto columns that already exist

The container schema expresses the Diablo rarity model without inventing anything:

| Item rarity | What it means | Expressed as |
|---|---|---|
| Normal | base type and implicit only | `pool_rolls = 0`, no pool rows |
| Magic | one or two affixes | `pool_rolls = 1..2` |
| Rare | three to six affixes | `pool_rolls = 3..6`, wider `min_tier`/`max_tier` |
| ~~Unique~~ | ⚠ **not a rarity — see below** | — |
| ~~Set~~ | ⚠ **not a rarity either** | — |

> ⚠ **Corrected by [item/ssot-uniques.md](item/ssot-uniques.md) and
> [item/ssot-rarity.md](item/ssot-rarity.md).** Listing "Unique" and "Set" as rungs on a rarity ladder
> is a category error, and it propagated from this table into three lane documents before anyone caught
> it. A unique is a **content class**, orthogonal to rarity: its defining property is that it breaks the
> rules the generator obeys, which a rarity rung cannot express. The shipped validator already draws
> that line — `TierOutOfWindow` is applied **only inside the pool loop**, so a container's fixed core is
> never tier-checked (`ContainerValidator.cs:73-96` versus `:44-57`). Hence the rule:
> **a unique may break every rule that lives in the generator, and no rule that lives in the machine.**

⚠ Note also that `min_tier`/`max_tier` are **authoring assertions, not runtime filters** — the validator
rejects the whole container, and `Instantiator.Draw` never consults the window
([item/ssot-generation.md](item/ssot-generation.md)). Expressing a rarity's tier window therefore needs
a draw-time parameter that does not exist yet.

`pool_rolls`, `min_tier`, `max_tier`, `rarity`, `weight`, and `group` are **already columns**. The
validation that makes them safe already exists too: a pool that cannot satisfy its own `pool_rolls`
rejects as `PoolRollsExceedGroups`, an all-zero-weight pool rejects as `UnsatisfiablePool`, and an atom
outside the tier window rejects as `TierOutOfWindow`. None of that has to be built for items — it has to
be *authored against*.

**The claim to test when this is built:** a new item base type should cost one container row plus its
pool rows, and a new affix should cost one atom row. If it costs code, the inheritance failed.

### 6.3 An instance is the item

A dropped item is one `effect_instance` plus one `effect_instance_atom` per atom, with the
`OnInstantiate` rolls resolved and frozen and the `roll_seed` recorded. Re-running instantiation with
the same `(container_id, catalog_revision, roll_seed)` reproduces it byte-identically.

⚠ **Closed.** The question was: is the instance *itself* the item, or is there an `item` row above it?
Answered **yes, a thin row** — `rpg_item_instance` keyed on the instance
([item/ssot-item-categories.md](item/ssot-item-categories.md)), carrying only what an effect instance
should not: display identity, lock and favourite state, provenance. Rolls are never duplicated into it.

[item/ssot-inventory.md](item/ssot-inventory.md) adds the half nobody expected: **Normal-rarity items
need no row at all.** With `pool_rolls = 0` and Fixed-only implicits, the reproduction contract makes
every copy indistinguishable, so stock gear is a counter plus one shared canonical instance. That is
what makes a roster of 48 specimens × 15 slots into 720 *cells* rather than 720 *decisions*.

### 6.4 Equipping is two acts, not one

> ⚠ **Corrected by [item/decision-d1-durable-ownership.md](item/decision-d1-durable-ownership.md).**
> "Equipping = create a binding" cannot be right, because **no owner scope durably names a specimen** —
> `entity:{ptr}` is contractually session-scoped, `plant:N` is type-wide, `player:N` is the account.
> Five lanes hit this independently.

Equipping is **assign**, then **bind**:

- **Assign** is durable and belongs to the item program — a row saying this player put this item in this
  role on this specimen. It survives restarts, deployments and recoveries.
- **Bind** stays session-scoped and belongs to E6 — rebuilt as a **full projection** at deploy, never as
  a delta.

Adding an `actor:{instanceId}` scope was traced end to end and **rejected because it does not reach the
actor**: `AtomCompiler.Compile` takes atoms and a runtime but never an owner, and the grant it emits
leaves `OwnerKind`/`OwnerKey` unset; if something set the key anyway, `StatApplyScope.Matches` falls
through to `return false` with no log. The scope is **reserved, not added**.

This is not a workaround — it is the shipped architecture. `UpsertUniqueEquipment` already rebuilds
rather than deltas, and `UniqueOwnerBinder.ToEntityKey` already discards the instance id at deploy. It
also makes unequip atomic: one row deleted, no second writer.

Unequipping is the deletion of an assignment. The bind gate still rejects for runtime support, scope
legality, `level_req` and stale content — though note that **`level_req` is currently enforced nowhere**
(handoff §2, A4).

⚠ **Two blocking amendments before the first item row exists:** the orphan sweep deletes any instance
with no binding, so unbinding would delete owned gear; and `ResolveBindings` compares
`catalog_revision` by equality, so one content import would unequip everything every player owns. Both
are cheap today because nothing calls that code yet.

This retires the stub: `rpg_unique_equipment` and `UniqueEquipmentCatalog`'s `mods_json` grant-folding
go away, and E6 already migrates `mods_json` grants into `effect_binding` rows.

---

## 7. Items that are not equipment

The dividing line is not flavour, it is **whether the item carries rolled values**. A rolled item is
unique by construction and cannot stack; everything else can, and should live in a table that knows it.

| Category | Rolled? | Stacks? | Storage | Notes |
|---|---|---|---|---|
| **Equipment** | yes | never | gear inventory, one row per instance | §5–6 |
| **Material** | no | yes | **already exists** — `rpg_demon_materials`, generalise it beyond demons | crafting and upgrade inputs; expeditions already seed `essence.*` and `shard.*` |
| **Consumable** | no | yes, with charges | own store; possibly held in the `girdle-resource` slot | see below |
| **Quest / key** | no | usually no | own store, undroppable, unsellable | must never compete for gear space |
| **Currency** | no | yes | ledger, not inventory — souls already work this way | `rpg_soul_balances` / `rpg_soul_ledger` is the precedent |
| **Socket insert** (gem / rune) | maybe | yes until socketed | material-shaped | deferred to §11 |
| **Cosmetic** | no | no | wardrobe | out of scope for the first pass |

**Consumables are the one category that is not free.** A consumable does something *when used*, which is
an **action**, and actions are the [action program](action-map.md)'s — an action is an envelope (when) +
a container of atoms (what) + a target rule + a cost. A healing potion is therefore an item that carries
an action, not an item that carries atoms directly. Two consequences:

1. **Consumables should wait for the action layer**, or ship in a deliberately degenerate form
   (self-targeted, instant, no cost) that the action layer later absorbs without a migration.
2. The classic failure — consumable spam trivialising combat — is solved with charges refilled at rest
   and shared cooldowns, both of which are action-layer concepts, not item-layer ones.

> ⚠ **Refined by [item/ssot-consumables.md](item/ssot-consumables.md), which rejected the degenerate
> form above.** "Self-targeted, instant, no cost" would ship *a potion that does nothing*, because in
> battle a bound `resource.delta` is a verified silent no-op under D6. The shipped answer is to
> degenerate the **use path**, not the effect: spent at a menu before a run, effect lasts the run, with
> the effect authored as real atoms from day one. That mode also already exists — expeditions seal their
> outcome at dispatch, so there is no "use" moment to design.
>
> It also found that **an instant consumable has no trigger it may legally name**, while `EffectBag`
> already fires all actions immediately for a `Passive` def — the runtime does it, the schema forbids
> it. Hence the `OnUse` trigger request in the handoff. And **the atom layer has no binding with a
> lifetime**, so a timed buff must be a status, using a payload kind that has zero consumers today.

---

## 8. Where items come from and where they go

**Sources**, in the order they already have machinery:

| Source | Status |
|---|---|
| Expedition rewards | server-resolved with a sealed seed today; rolling an item instance is the same shape as rolling a material |
| Battle / wave rewards | needs a drop table, which is a weighted pool — the same primitive the container pool already is |
| Crafting and upgrade | needs materials (exist) and a recipe table (`recipes` exists for fusion, different grain) |
| PvZ extension play | bounded by the standalone charter — see §9 |

**Sinks.** An item system without sinks becomes a museum. The standard three, in increasing order of
how much design they cost:

1. **Salvage** — unwanted gear becomes materials. Cheap, and it feeds crafting immediately.
2. **Reroll / upgrade** — spend materials to re-draw an affix or push a tier. This is where the tier
   window and `roll_seed` earn their keep.
3. **Sockets and sets** — deferred to §11.

**~~The open economic question~~ ✅ DECIDED — see §2b, D1.** Gear is uncapped: the commander and every
unique demon may wear the full role set, and **roster pressure is regulated by the features that own it**
(deployment caps, squad size), not by the item system pricing itself defensively. The paragraph below is
kept as the reasoning that led to the question. Its three named options are all superseded — the answer
was *"none of these; it is not our problem to solve"*.

items are per-actor, and this game has *rosters*. Twenty demons times
twelve slots is 240 equipped items before anything sits in a bag. Either gear is scarce and most
specimens go bare, or gear is plentiful and inventory management becomes the game. Games that solved
this went one of three ways — shared account-wide stat pools, per-specimen gear that is cheap and
disposable, or a small deployable squad so only a handful of actors ever need gear. **This must be
decided before slot counts are final**, because it is the difference between twelve slots being rich and
twelve slots being a chore.

---

## 9. Constraints this inherits and cannot argue with

| Constraint | Consequence for items |
|---|---|
| **Standalone-first** ([decisions.md](decisions.md)) | Every item must be earnable and usable **with the game closed**. PvZ may enrich the item economy, never gate it, and must never be the best source of anything web mode also provides |
| **No new atom kinds** | 12 kinds, 5 attach points, 7 triggers, closed. An item that needs a thirteenth kind is a design conversation, not a row |
| ~~**`stat.derived` is quarantined `None/None/None`** (D6)~~ ✅ **LIFTED — see §2a, B1** | Battle got its consumer 2026-08-23 (E12, `TraitAtomSource`); **the lawn got one 2026-08-30** (`AtomDerivedSubsystem`, ActorHub order-350). The matrix is now `Full/Full/None` (`AtomKindRegistry.cs:253`). An item made of `+fire power` affixes **binds and executes**. First-wave items are no longer restricted to five kinds — this row was the binding constraint on the whole affix library, and it is gone |
| ~~**Power is open** (E9, build position 15)~~ ⚠ **Superseded — see §2a.1** | The power ladder shipped 2026-08-23/24. Every magnitude reads `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`, pinned at `P(20) = 680` so `B` retunes without re-resolving one item; contests read `Θ` linearly. Drop bands and authoring budgets now have a function behind them (`power/ssot-power-scale.md` §4). What §2a.5 adds is the question the ladder does *not* answer: the offense/defense ratio |
| ~~**G8 — `warding` / `resilience` are match-scoped only**~~ ✅ **Obsolete — see §2a, B1** | The row's own escape hatch is now open: per-actor mitigation uses `combat.defense.*`, which is `stat.derived`, which **executes on battle and lawn**. *"+armour is the hardest common affix to ship"* was true for eight days and is now false |
| **Battle reads no equipment** — **still true, and it is a *wiring gap*** (§2a, W2) | `ChannelMods` is still documented *"trait stat mods, equipment later"* (`BattleModels.cs:33`). But E12 shipped a **working producer on that exact seam** (`TraitAtomSource`), so an equipment producer is the same shape, not a new path. Not an architectural limit — a missing call |
| **One economy** | Web and PvZ write the same ledgers through the same ingest, source-tagged, never forked |

---

## 10. What this does *not* touch

Loot volume and filters, trading between players, durability, appearance/transmog, item level inflation
and squishes, and any change to the Foundation contract. Named here so nobody assumes silence means
approval.

---

## 11. Deliberately open — the next discussion round

> ✅ **Answered 2026-08-22.** Every question below now has a lane: rarity →
> [ssot-rarity.md](item/ssot-rarity.md), sockets → [ssot-sockets.md](item/ssot-sockets.md), sets →
> [ssot-sets.md](item/ssot-sets.md). The questions are kept as the record of what was asked and of what
> the answers had to cover. Two guesses in this section were wrong and are worth noting: sockets do
> **not** need new atom-table schema (an insert is its own instance binding on the same owner, so
> composition happens at the binding layer and the content fingerprint is untouched), and the "socket
> count by rarity" axis turned out to be layered across base type, rarity and crafting rather than owned
> by one of them.
>
> What remains open is listed in [item/README.md](item/README.md) § *Open — needs the owner* — **and that
> list was re-answered against code on 2026-09-03: two of its seven are closed, one superseded, one
> answered by another program. See §2a.6. §2a.7 adds five genuinely new questions the platform's
> arrival created, which are not in the list below.**

The owner scoped these out of the first pass. They are written as questions, not as leanings.

### Rarity
- What is the ladder, and how many rungs? The `rarity` table exists with append-only ordinals, so adding
  a rung later is cheap but reordering is not.
- Does rarity control affix **count**, affix **tier window**, or both? The schema supports both
  independently — that is a choice to make, not a constraint.
- Where do hand-authored **uniques** sit relative to rolled rares, and what keeps both relevant?
- Drop rates, and whether there is bad-luck protection. The summon pity counters are the in-tree
  precedent.

### Sockets
- Are sockets a **count rolled on the item**, a property of the base type, or added by crafting?
- Are inserts **typed** (a fire gem only fits a fire socket), or universal?
- Is a socket a real choice or a stat tax? The failure mode is well documented: a socket system where
  the correct insert is always obvious adds inventory work and no decision.
- Does socketing map onto the existing container pool, or does it need a new table? An insert is an atom
  arriving after instantiation, which the current model does not have a shape for — **this is the one
  deferred mechanic that probably needs new schema.**

### Sets
- **Discrete breakpoints** (2-piece / 4-piece / 6-piece) or **summed contribution** across pieces? These
  produce very different build spaces.
- How is set membership expressed — a tag on the container, or a container of containers?
- What prevents set-jail, where one set is so strong every build converges on it?
- Are set pieces rolled or fixed?

### Also still open from this document

> ✅ **All six were closed on 2026-09-03 — see §2b.** Kept as the record of what was asked. What is
> genuinely still open is §2c, and it is a different, shorter list.

- ~~Hybrid slot rule (§5.3)~~ — **D3**: hybrid is allowed, including for the commander; 13 roles +
  `standard`, at 89% of a pure frame. Ten flexible slots, fixed per-species assignment, or twelve with
  restrictions.
- Whether the item entity is the `effect_instance` or a thin row above it (§6.3).
- Commander `standard` slot and squad-scoped bindings (§5.6) — **D3**: the commander is 15 + `standard`
  pure, or 13 + `standard` hybrid.
- ~~Slot unlocking as progression (§5.6)~~ — **D2**: not in v1, but the predicate ships defaulting to
  always-open so a breakthrough or quest system can gate slots later without a migration.
- ~~Roster-scale gear economy (§8)~~ — **D1**: uncapped, and not this program's problem. **This was
  named as the one that must be answered before slot counts freeze; it is answered, and the count is
  fifteen.**
- Consumables: wait for the action layer, or ship degenerate (§7) — substantially answered by
  `OnActivate` existing (§2a, B7); what remains is whether consumables name it.

---

## 12. Prior art this draws on

Diablo 2 / D2R — base types with implicit modifiers, affix tiers, the ten-slot body, and rarity as affix
count. Path of Exile — modifier families with one mod per family, item level gating tier access, and
crafting as the primary sink. Diablo 4 — item power bands selecting affix ranges, so power is an input
rather than only an output. Last Epoch — a per-item spend budget and tier-plus-range-within-tier, the
closest published model to the value spec this repo already ships.

~~**Honest note on sourcing:** a web research sweep was launched for this document and did not return
before it was written.~~ ✅ **Closed 2026-09-03 — see §2a.5**, which sources the numbers: PoE's 3-prefix
/ 3-suffix ceiling and tier-then-value roll order, Last Epoch's craft-capped T5 with drop-only T6–T7 and
finite Forging Potential, the EHP-vs-lethality ratio table (D2 3.4×, PoE 8.5×, D3 104×), Arknights'
3-vs-11 rarity/class cost split, FGO's 32-point ATK drift across ten years, and D3's launch post-mortem.
The claims in the paragraph above survived contact with sources; two of them (PoE tier gating, Last
Epoch's tier-plus-range) turned out to be **more** load-bearing than stated. **Any number that ends up in
a spec must still be re-verified — §2a.5's are cited, the rest are not.**

---

## 13. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — effect-atom (container/instance/binding),
    unique-actor lifecycle, demon species, standalone economy.
[x] I read every doc in the §1 row(s) for those subsystems, this session.
[x] I checked decisions.md for a lock covering this — standalone-first, resource model,
    action model, and the golden-ordering row all bear on it; none forbid this document.
[x] Every factual claim about the repo cites file:line or a doc.
[x] I verified claims against CODE, not comments — the equipment stub, the species roster,
    the materials table, and the battle ChannelMods comment were all opened.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) any constraint I am reporting. **Closed 2026-08-22** by
    item/defect-register.md: Core 2257, Data 353, Guard 54 — 2664 green, 0 failures, all four
    boundary guards OK. Nine of ten defect claims confirmed, two refuted, one partial. Three
    further claims (handoff C1-C3) remain unverified and are marked as such.
[x] Nothing contradicts a §2 invariant.
[x] ROUND 2 (2026-09-03): principles restated before reading; every finding sorted
    built / wiring gap / real gap with file:line; prior art web-sourced with numbers.
    Verified against CODE: AtomKindRegistry, AtomDerivedSubsystem, RpgStore.Containers,
    RpgStore.AtomInstances, RpgStore.UniqueActors, RpgStore.Import, AtomImporter,
    BattleModels/BattleStatComposer, Aptitude, RpgStore.PlayerCommander.
    NOT re-verified in round 2: the seventeen lane SSOTs' internal numbers, the ~880-cell
    content cut, and the reason-code count. Those remain as round 1 left them.
[x] Corrections propagated. **Done 2026-08-22:** §5, §6.2, §6.3, §6.4, §7 and §11 carry the lane
    corrections; upstream corrections owed to definitions.md, atom-family-library.md and
    spec-action-model.md are collected in item/atom-layer-handoff.md rather than applied here,
    because they belong to other programs. Still no map, plan, or task list — those are written
    when the program graduates.
```

---

## 14. What the enrichment round changed

Recorded so the next reader knows which parts of this document were written from intent and which
survived contact with code.

| Claim here | Outcome |
|---|---|
| Twelve slot roles | **Fifteen** — three homeless affix clusters had no home (§5) |
| Unique and Set are rarity rungs | **Wrong.** A category error that propagated into three lanes (§6.2) |
| Instance-or-item-row is open | **Closed** — a thin row, plus stock items needing no row at all (§6.3) |
| Equipping is a binding | **Wrong.** Two acts: durable assign, session bind (§6.4) |
| Consumables ship degenerate | **Refined** — degenerate the use path, never the effect (§7) |
| Sockets probably need new schema | **Half wrong** — a sidecar, no atom-table change |
| Rarity picks count and tier window | **Held**, and the tier window turned out not to be enforced at draw time |
| Frame, not faction, is the key | **Held**, and every lane built on it |
| Items have no behaviour; actors do | **Held** throughout |
| Derived magnitudes are resolver points | **Wrong upstream** — six of twelve families are flat game units (handoff §1) |

---

## 15. What round 2 changed (2026-09-03)

Same table shape as §14, one round later. §14 recorded what *contact with the lane documents* changed;
this records what *six weeks of other programs shipping* changed. Full evidence in §2a.

| Claim in this document | Outcome |
|---|---|
| `stat.derived` is quarantined; `+armour` is the hardest affix to ship | ✅ **Closed.** Full on battle (2026-08-23) and lawn (2026-08-30). The single biggest constraint here, and it is gone |
| Power is open; drop bands have no number behind them | ⚠ **Superseded.** `P(Θ)` shipped, pinned at `P(20) = 680` |
| There is no player/commander actor | ⚠ **False now.** `rpg_player_commander` exists; `OwnerKind.UniqueActor` approved 2026-09-02 |
| The tier window needs a draw-time parameter that does not exist | ✅ **Exists.** `rarity(min_tier, max_tier)` is a column the resolver reads |
| The pool's roll unit is a bare atom | ⚠ **Changed.** It is a named **affix bundle** — `effect_affix` + `effect_affix_ref`, with slot refs |
| `pool_rolls` is one count | ⚠ **Split** into `prefix_rolls` / `suffix_rolls`; a mixed bundle consumes one of each |
| Battle reads no equipment | **Held** — but it is a **wiring gap**, not a limit. E12 shipped a working producer on the same seam |
| Two blocking amendments are cheap today because nothing calls that code | ⛔ **No longer cheap.** `ProduceAndBind` is in production. Unequip still deletes the instance; one import still disables every rolled item |
| Primary attributes: five, or none | **Neither — twelve aptitudes**, shipped by the class-system program |
| Rarity picks count and tier window, never magnitude | **Held, and now corroborated by prior art** — the genre's most reliable finding (§2a.5) |
| Twelve slot roles, later fifteen | **Held, and now the largest divergence from prior art.** Roster games ship 5–6 and are still called grindy (§2a.5) |
| Prior-art numbers were never sourced | ✅ **Closed** (§2a.5) — PoE's 3+3 ceiling, Last Epoch's drop-only T6–T7, the EHP-vs-damage ratio table, D3's post-mortem |
| Items have no behaviour; actors do | **Held** through both rounds |
| Frame, not faction, is the key | **Held.** Still unbuilt in code — `frame` exists in no type (§2a, R4) |
| Drop volume is I12's own calibrated rate | ⚠ **Reframed (D18).** It reads `Θ`, composed as `ssot-power-scale.md` §5 already composes it. Count reads `Θ` linearly; quality keeps reading `P(Θ)`. **No private loot curve** |
| 144 lane questions are an open queue | ✅ **No (D16).** ~25 answered outright; the rest are decided-unless-disputed, reopened only when one bites |
| C1–C3 are unverified and one *"would change what a status magnitude means"* | ✅ **Verified (§2e).** C2 was real and **is already fixed** by the power program's audit F4 — nobody told the item lanes. C3 confirmed, C1 reassigned to `E42`, and *"`effect_binding` has zero production consumers"* refuted |
| A commander is a distinct kind of geared actor (§5.6's `standard`) | ⚠ **Out of scope 2026-09-03 (D14).** The commander is another unique demon here. `standard`, **artifacts** and commander sets are reserved, pending a commander role/class system |
| Sets and charms are hand-authored, UNSIZED | ⚠ **Reframed 2026-09-03 (D12).** **Generated** — ~528 today, ~2,168 at the full roster, from ~3 authored archetype rows. d4's two biggest unsized entries leave the hand-authored column |
| Item content is authored | ⚠ **No — it is generated, and it has an upstream.** The item generator consumes seedsmith's demon theme registry, exactly as the action corpus does (§2d) |
| Hybrid is priced by a flat slot cut (I2: 10.5%) | ⚠ **Replaced 2026-09-03 (D3).** **80% floor, earned back to parity by mixing.** I2 §4.2's order-statistics pricing measured sampling; a hybrid *chooses*, so it never applied |

### The one-line summary

**The machine an item system needs is now built and running; what is missing is the item.** In August
this document described a rich design blocked by an inert substrate. Today the substrate executes in
production, and the blockers are three owner decisions, two live defects with a closed window, and a
program that has never graduated to a map.
