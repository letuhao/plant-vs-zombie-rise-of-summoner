# Demon scope ideal: what keys what, general vs. unique, and where fusion inheritance actually sits

**Status: idea phase only — no spec, no plan, no code changes.**

> ⏸ **§5-9 (general-demon progression, troop-stack identity/upgrade containers, stack experience,
> near-death promotion, and the Survivor Title achievement system) are DEFERRED — owner decision,
> 2026-09-07.** *"Troop management, achievement is big feature, we will turn back to fusion and
> demon scope... and complete demon species leftover to make gameplayable first."* §1-4 (the
> general/unique/Commander/Patron vocabulary and WAVE F2.4's own scoping question) are NOT deferred —
> they feed directly into WAVE F2 (`tasks/demon-standalone-todo.md`), which resumes now. §5-9 stay
> here, fully investigated and ready to pick up, until the owner returns to them — see
> `tasks/demon-standalone-todo.md`'s own "Deferred — future work" note for the pointer back to this
> doc.

Written in response to a real,
named confusion: *"demon have general depend demon species, unique depend fusion, gatcha, summon,
capture and commander is promote of unique right? ... so fusion much key of player, specie, unique?"*
The short answer: **the confusion is real, but the terms already have a settled SSOT** — the gap is
that "general vs. unique" and "which data is shared vs. per-specimen" are two *different* axes that
look like one question. This doc separates them, shows what's built for each, and names the one
genuinely undecided architectural question this session's own build work (WAVE F2.4, fusion
inheritance) ran into.

## Step 0 — the governing principle, restated

**Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** A demon
species, a fused specimen, a player's own roster — none of it is a PvZ concept the injector's Unity
write surface constrains. The lawn *reads* these RPG-layer facts (an actor's element, its equipped
atoms) and the RPG layer *writes back* signed deltas; it never depends on PvZ representing "demon,"
"fusion," or "unique" as concepts of its own. Nothing in this doc proposes touching PvZ's own board,
spawn, or AI systems — every distinction below (general vs. unique, species vs. specimen) is an
RPG-layer data-modeling question, settled entirely in `FusionRpg.Core`/`FusionRpg.Data`.

## 1. The SSOT already exists — read before this doc, not instead of it

**`docs/architecture/demon-system-map.md`, "Vocabulary" section (its own §, added 2026-09-06)** is
the binding answer to "what is general vs. unique, and what is Commander/Patron/party member." It is
restated here in full because a downstream reader should not have to open two docs to get one answer,
per this repo's own `/idea` discipline — but that section is the source of truth; this doc explains
*why* it resolves the confusion and where the real gap sits, it does not re-decide any of it.

### Axis 1 — what the demon IS (general vs. unique)

| | **General demon** | **Unique demon** |
|---|---|---|
| Spawned by | The PvZ engine itself (a normal lawn plant/zombie) | The RPG layer — summon, fusion, gacha, **and** capture |
| Identity | Species only — no `instanceId`, nothing persists between spawns | A real specimen: `UniqueActor`, its own `instanceId`, phase FSM |
| Stats | Empire-wide, per-player, per-species progression fallback only | Own specimen progression, equipment, passive build, aspect/action slots — never the empire species fallback |
| Used for | Army-scale, disposable, or engine-spawned populations — a lawn wave, **siege garrisons, world-map legions** | An individually-meaningful, player-invested demon — your roster, a Commander/Patron, a Delve party member |
| Scale model | **Troop-stack**: one type × a count (Heroes 3's own troop-stack shape, cited in `base-defense-ideal.md`) | Never army-scale — a roster is dozens, not thousands |

**The load-bearing correction this doc makes to how the question was framed:** "general" is **not**
"a demon whose atoms are read from a shared per-player-per-species roll." It is "a demon that has no
persistent row *at all*" — a headcount, not an instance. **Every demon a player ever owns — from
gacha, summon, fusion, or capture — is a Unique demon** the instant it exists as a row. There is no
"general" tier of *player-owned* demon; general demons are the OTHER, disposable population (siege
defenders, legions), never something in your own roster. Fusion, gacha, summon, and capture are four
different **acquisition methods that all produce the same kind of thing** (a `UniqueActor` row) — they
are not four different *kinds* of demon.

**2026-09-08 owner decision:** the distinction also selects progression. A gameplay-owned spawn
mechanism declares its progression source. General demons associated with the player's empire use the
empire-wide species fallback only when that mechanism supplies no dedicated progression source.
Unique demons and Commanders use their respective dedicated sources and never combine them with the
fallback. The binding statement is `decisions.md`'s *Demon progression source and spawn ownership*
row; this idea document follows it.

### Axis 2 — the two passive-aura roles, and the third thing that isn't a role

| | **Commander** | **Patron** |
|---|---|---|
| Scope | The lawn run | The match |
| Grants | One leadership aura **plus** aptitude spend reaching the side | A specific elemental combat bonus, scaled by rarity/star/level/Θ |
| Cost | Not soul-priced | First pick free; each switch costs 100 souls |
| Fusion interaction | Not documented as locked | **Unconsumable** — fusion refuses the active Patron as sacrifice or input |

**Both are a role you *assign* to an existing Unique demon — never a separate kind, never a
promotion that changes what the demon IS.** "Commander is a promotion of unique" (the user's own
framing) is directionally right in spirit (only a Unique demon is eligible) but the more precise
model is *designation*, not evolution: the demon's own row is unchanged, a separate pointer
(`commanders.md`/`spec-patron-demon.md`) names which `UniqueActor` currently holds the role, and it
can be reassigned. Neither Commander nor Patron ever fights.

**A third, unrelated concept:** a **Delve party member** — a Unique demon that actively fights inside
a dungeon run and can go `Downed`. This is not a role at all; it's a different LOOP (`party-dungeon-ideal.md`),
and conflating "the demon fighting for me" with "Commander" is the single most common version of this
confusion per the SSOT's own wording.

## 2. The real question — what data is shared vs. individual, and it is a THIRD axis

This is where the confusion has a genuine, previously-unnamed root, found while building WAVE F2
(fusion trait/action inheritance) this session — not answered by Axis 1/2 above, because it's a
different question: *given* a Unique demon (Axis 1 says what it is), **which of its own data is
individually its own, and which is shared with every other specimen of the same species this player
owns?**

### 2.1 Built — verified against code, not assumed

| Layer | Scope key | Individual or shared? | Evidence |
|---|---|---|---|
| Species catalog (base stats, element, rarity floor) | `speciesId` only | Shared — by definition, a species fact | `DemonSpeciesCatalog` (`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs`) |
| The specimen row itself | `instanceId` | **Individual** — every specimen gets its own row | `rpg_unique_actors` / `UniqueActorDto` |
| Rarity, variant, element (rolled at mint) | `instanceId` | **Individual** — rolled once per mint, in `DemonProfileDto` | `RpgStore.Demons.cs:29` `MintDemonUnlocked` |
| Level / XP | `instanceId` | **Individual** | `rpg_unique_actors.level`, `AwardUniqueActorXpUnlocked` |
| Star (promotion investment) | `instanceId` | **Individual** | `rpg_demon_profiles.star`, `StarPolicy` |
| Equipped gear | `instanceId` | **Individual** — items are individually rolled per instance, same as any other item | `ItemEquipService`, `RolledItemEquipRuntimeTests` |
| TraitIds (old, flavour-only tags) | `instanceId` | **Individual** — but cosmetic, never resolves to real atoms today | `DemonProfileDto.TraitIds`, `spec-demon-fusion.md` locked decision 5 |
| **The specimen's own real gameplay atoms** (`species-passive.{speciesId}` container roll) | **`(playerId, speciesId)`** | **Shared** — every specimen of the same species, same player, reads the identical roll | `player_species` table (`RpgStore.PlayerSpecies.cs:42-55`), `SpeciesMaterialiser.Materialise` |

**So the real picture is not "general (shared) vs. unique (individual)."** A Unique demon already
carries a rich pile of individual data (rarity, variant, level, star, gear, cosmetic traits) — the ONE
thing that is species-shared, not per-specimen, is the real gameplay-atom roll itself. That is a
narrower, more specific fact than "unique demons are individual" implied, and it is the actual reason
WAVE F2.4 (letting a player pick inherited atoms into a *specific* fused specimen) ran into a real
seam: **the fused specimen is individual (Axis 1: Unique), but the thing F2.4 wants to customize
(its real atoms) lives at the species-shared layer, not the specimen layer.**

### 2.2 Wiring gap — none found

No inert toggle, null delegate, or debug-only path was found in this investigation. The species-shared
design is not a bug or an unfinished wire — `SpeciesMaterialiser`'s own docstring states it as a
deliberate property (*"pure — seed and catalog in, rows out, no I/O... `(worldSeed, catalogRevision)`
reproduces it exactly"*), and `player_species`'s own comment states the append-only rule as intentional
(*"a species already present for this player is never re-rolled... for free, with no version check"*).
This is a real design choice, not a wiring gap dressed up as one.

### 2.3 Real gap — the one genuinely open, undecided question

**There is no mechanism today for a demon specimen to carry its own individually-rolled gameplay
atoms, separate from its species' shared roll.** This is a real, load-bearing absence, not a smaller
thing than it sounds: closing it would mean introducing a second roll layer (per-specimen, on top of
the existing per-species one) — genuinely new architecture, not a parameter added to an existing call.

**Genre precedent is split on whether this matters, and split in an informative way:**

- **Pokémon's own model is the strongest counter-example to "species-shared is enough."** Base stats
  are fixed per species (every Charizard: 78/84/78/109/85/100) — but **every individual catch rolls
  its own IVs** (0-31 per stat, "the DNA of a Pokémon"), earns its own EVs through play, and can carry
  a customized moveset. The final stat is `f(base, IV, EV, level, nature)` — species-shared base plus
  a real, separate per-individual layer. This is widely credited as a major reason the "catch them
  all, then optimize the individual" loop has the depth it does.
- **Persona/SMT's Demonic Compendium is the other precedent, and it's closer to this repo's own
  shape.** The Compendium stores registered demons at the SPECIES/build level (an entry per demon
  *type*, its stats and learned skills as last fused) — a player re-summons a *registered* version
  from the Compendium at a soul fee rather than tracking N independently-varying live instances. This
  is much closer to "one shared, reusable roll per (player, species)" than to Pokémon's per-catch IVs.

**Neither is more "correct" — they're different design philosophies with different costs.**
Pokémon's model needs per-individual storage and a market for "which specific individual is good";
SMT's model is cheaper (one roll reused) and puts the interesting decisions at the fusion/recipe layer
instead. **This repo's `SpeciesMaterialiser` already reads as an SMT-shaped choice**, deliberately (not
accidentally) — the question this doc surfaces is whether that is still the intended shape now that
fusion inheritance (WAVE F2) wants to let a player customize the OUTCOME of a specific fusion, which
is naturally a per-instance idea being asked of a per-species system.

## 3. What this resolves for WAVE F2.4, without deciding it here

Two coherent paths exist, both compatible with everything already built — this doc does not pick one,
because that is a real product decision, not an idea-phase finding:

- **(a) Stay SMT-shaped.** Fusion inheritance customizes the shared `(player, species)` roll — meaning
  it only does anything meaningful the *first* time a player fuses into a brand-new species (before
  `player_species` has a row for it); a second fusion into an already-owned species has nothing left
  to customize, and should refuse the pick-set with a named reason rather than silently discard it.
  This needs zero new architecture — `player_species`'s own already-shipped "materialise once" rule
  already enforces the boundary.
- **(b) Move toward Pokémon-shaped.** Give each specimen (or at least fusion-born specimens) its own
  individually-composed roll, independent of the species-shared one. This is a real, separate,
  larger feature — a second `InstanceProducer.Compose` call site keyed by `instanceId` instead of
  `(playerId, speciesId)`, a schema change, and a decision about whether non-fusion specimens
  (gacha/summon/capture) also gain individual rolls or stay on the shared one. Worth naming as its
  own idea if the owner wants demons to have Pokémon-style individual depth — not something to build
  as a side effect of WAVE F2.4.

## 4. Summary — a direct answer to the confusion, restated once

- **"Demon have general depend demon species"** — not quite: *general* demons are the disposable,
  no-instance, army-scale population (siege/legion), and a species is just their type key. A
  player-owned demon of ANY acquisition method is never "general."
- **"Unique depend fusion, gatcha, summon, capture"** — yes, exactly: those four are the four
  acquisition methods, all producing the same thing, a `UniqueActor`.
- **"Commander is promote of unique"** — close: it's a *role assignment* to an existing Unique demon
  (reassignable), not a promotion that changes the demon itself. Patron is the match-scoped sibling
  role; a Delve party member is a third, unrelated, actively-fighting concept.
- **"Fusion much key of player, specie, unique?"** — fusion's own transaction is keyed on
  `(playerId, sacrificeInstanceId × 1-2, baseInstanceId?)` — purely Unique-demon-scoped; species is
  read *from* the specimens, never a fusion input in its own right. The part that feels like it spans
  all three keys is real: fusion **mints** a new Unique demon (`instanceId`-scoped), whose real
  content then comes from the **shared** `(playerId, speciesId)` roll — two different systems meeting
  at one moment, which is exactly the seam this doc names as the open question in §2.3/§3.

## 5. Extending the idea: how do general demons get bonuses and upgrades?

**A real, valid objection the owner raised after reading §1-4 above:** if general demons (siege
garrison defenders, world-map legions) are purely PvZ-engine-spawned with no persistent row, how does
the player's own progression ever reach them? *"We cannot ship an empire with non-upgradable General
units."* Investigated the same way as §2 — built/wiring-gap/real-gap, file:line for every claim —
across the base-defense, world-map, and buff-scope programs, plus genre prior art.

### 5.1 Today: general-demon combat power is disconnected from player progression — a real gap

- **World-map legions (`WorldEntityMember`, `src/FusionRpg.Core/World/WorldState.cs:275-284`)**: `Hp`/
  `Level` are plain fields, `InstanceId` is **documented as null for non-player forces and guards**
  (`:278`) — confirming §2's vocabulary exactly. Every shipped value is a **hand-authored flat
  integer per content template** (`WorldTemplateCatalog.cs:194-225`, `RaiseResolver.cs:134-138`,
  `LoamPhases.cs:280`) — no code path reads `DemonSpeciesCatalog`, `Θ`, or `P(Θ)` when building one.
- **Their combat resolution is a named placeholder**: `PlaceholderBattleResolver.Strength`
  (`src/FusionRpg.Core/World/Turn/PlaceholderBattleResolver.cs:36-42`) computes
  `total += Max(0, Hp-Wounds) * Max(1, Level)` — a private `f(level)` linear multiply, the exact
  pattern this repo's own power-ladder rule exists to forbid. Its own doc comment already flags it as
  wave-1 scaffolding, not real balance — a known, named gap, not a silent violation.
- **Siege-board waves are the one exception, and only half of one.** `WaveCatalog` already prices
  general demons through the real `P(Θ)` ladder (`base-defense-ideal.md` §3.5,
  `data/tuning/power-scale.v2.json`) — but `Θ` there is a fixed per-wave content constant (1/3/6/10),
  never a function of anything the player has invested in. Real power math, zero player-progression
  input.
- **The one existing scaling knob is count, not power, and it's off.** `spec-siege-objective.md:255`:
  `slots.legion.perDevelopmentLevel = 0` — a real wiring gap (the knob exists, defaults to zero,
  controls *how many* legion slots a side gets, never member power).
- **`DevelopmentLevel` is a real, wired, player-driven progression signal today — it just stops one
  layer short of troops.** A completed sector `develop` project raises `WorldSector.DevelopmentLevel`
  (`GrowthPhases.cs:107-143`, via `ProjectCatalog`), which already scales **structure** HP through
  `P(Θ)` (`DistrictAssaultResolver.cs:298`) and defense-slot **count**
  (`spec-siege-objective.md:251-252`). `ProjectDef` has exactly one authored effect field,
  `DevelopmentBonus` — nothing stat-shaped for a *unit*. So the pattern "player investment → real
  power-ladder scaling" is proven, shipped, and working — for buildings. It was never extended to the
  troops standing in them.

### 5.2 The mechanism that would fix this already exists — disconnected, not missing

**This is the load-bearing finding.** Two systems from opposite directions turn out to already be
shaped correctly for this, and neither has ever been told about the other:

- **`WhoKind.Type` resolves by live board `typeId`, never by `UniqueActor` or ownership**
  (`BattlefieldScopeExecutor.ResolveByType`, `src/FusionRpg.Core/Battle/BattlefieldScopeExecutor.cs:35,50-62`).
  A general demon spawned with a given `typeId` is reached exactly the same way any other board entity
  is — structurally, this already targets general demons. **But it is completely unwired**:
  `ScopeCompatibility.Table` (`src/FusionRpg.Core/Scope/ScopeCompatibility.cs:48-86`) has zero rows for
  `WhoKind.Type` on any atom kind or host, so every combination throws `ScopeUnsupportedException`
  today, and grepping the whole of `src/` for `WhoKind.Type` finds nothing beyond its own enum
  declaration and one unreached `switch` arm. A wiring gap — closing it for one atom kind is one table
  row plus one call site, not new architecture.
- **The `DemonType` allocation scope — the "general passive tree" `demon-system-map.md:51` already
  names — is already keyed by species, not by instance.** `SpeciesAllocationSource.Resolve`
  (`src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocationSource.cs:69-87`) looks a species up purely by
  `(ctx.Side, ctx.TypeId)`, and its own downstream consumer `AptitudeSubsystem.ContributeDerived`
  memoizes on `(Side, TypeId, Theta)` — never an `instanceId`, never a roster-ownership check. **If a
  general demon on the board shares a `typeId` the player has put `DemonType` points into, this chain
  would already deliver the bonus to it**, because nothing in it distinguishes "my own roster's
  specimen of this species" from "any board entity of this species." This resolves a real,
  previously-unflagged tension with `passive-tree-ideal.md`'s own D21 ("every actor carries its own
  tree state — Commander and each demon alike"), which reads as assuming per-instance state for every
  demon — structurally impossible for a general demon with no `instanceId` to key on. The species-keyed
  reality is the correct resolution: a general demon reads the shared, species-level tree, it simply
  isn't stated that way in that doc.
- **What's actually missing is three separate, smaller, real things — not a new mechanism:**
  1. *Write side, wiring gap.* Nobody has ever persisted a `DemonType` allocation in production —
     `species-build-ideal.md:149` already names this ("only `Commander` is ever written").
  2. *Production wiring, wiring gap.* The only two real callers of `SpeciesAllocationSource`
     (`src/FusionRpg.Injector/CheatState.cs:157`, `src/FusionRpg.Server/AuraDerivedEndpoints.cs:63`)
     are both lawn/preview-scoped — neither is the world-map or siege pipeline.
  3. *The connection itself, real gap.* `WorldEntityMember`/`PlaceholderBattleResolver` never build a
     `StatContext` or touch `ActorHub` at all — the world-map/siege stack and the aptitude-allocation
     stack are structurally separate systems today, joined nowhere.

### 5.3 A real, reserved carrier already exists for "empire tech buffs my army"

- **`world-map-scope` module** (`docs/architecture/buff-debuff-scope/spec-world-map-scope.md`,
  status: Draft, not yet built) is designed as a named, per-mille, hashed, replay-safe modifier on a
  `WorldFaction`/`WorldEntity` row, following the shipped `UpkeepHandicapMilli` precedent
  (`WorldState.cs:69-73`) — its own spec states the intent as "declare and hash the modifier; let each
  future consumer read it independently." This is the natural home for an empire-wide legion-power
  tech tree; it is designed to be read by exactly this kind of later consumer.
- **`ContainerKind.WorldBuff`** (a `world-buff.*` atom-container prefix) is reserved, validated, and
  round-tripped in the DB schema — and **nothing has ever authored a row in it**
  (`buff-debuff-scope-ideal.md:114-122`). A second real, idle carrier for the same idea.
- Both slots were deliberately left open by earlier design work, not overlooked — filling one of them
  is completing an intentional gap, not inventing new architecture.

### 5.4 Genre prior art: the split this repo needs is a common, well-precedented one

Two-axis progression (an individually-leveled hero/roster layer, plus a separate empire-wide layer
that scales mass-produced/generic units) is not a novel ask — it is how most of the genre's own
strongest examples already work, and several independently converge on "buff the unit *type*," the
exact shape `WhoKind.Type`/`SpeciesAllocationSource` are already built around:

- **Heroes of Might and Magic 3 — this repo's own already-cited troop-stack analogue, with real
  numbers.** A town building upgrade (Guardhouse, an empire-owned investment) permanently changes what
  the dwelling recruits: Pikeman (Atk 3-5, Speed 4) → Halberdier (Atk 4-6, Speed 5), same Defense/HP.
  This is a permanent lever on the **troop-stack tier itself**, completely orthogonal to any
  individual hero's own level, skills, or artifacts — precisely the two-axis split this doc needs.
- **Age of Empires II** — Blacksmith-line technologies apply to an entire unit *type* across the
  civilization (confirmed both in this repo's own `docs/research/genre-mechanics/07-rts-and-autobattler.md`
  §6.1 and independently by source below) — the same "buff by type, not by instance" shape as §5.2's
  disconnected mechanism.
- **StarCraft II Co-op** (already in this repo's own genre research, §6.2) ships exactly a three-layer
  split: per-commander level (individual), account-wide Mastery (empire-wide, 90 points across three
  power sets — explicitly affects mass-produced units), and Prestige (individual, exclusive
  trade-offs). A directly reusable template for keeping the two axes named and never merged.
- **Total War (Warhammer/Three Kingdoms)** — named Lords get individual skill trees and equipped
  items (the Unique-demon shape); generic units instead get a general's proximity aura plus
  faction-wide tech/skill bonuses applied to specific unit *types* — independently arriving at the
  same `WhoKind.Type`-shaped answer this repo's own half-built mechanism already points at.
- **Stellaris** — empire-wide technology buffs mass-produced fleets directly; leader (Admiral)
  leveling is explicitly, deliberately kept from bleeding into fleet-wide bonuses (a 2024 patch even
  removed a case where it had).

**The recommendation this section supports, without deciding it:** general-demon progression does not
need new architecture. It needs three things this repo already has slots for, connected: (a) a real
`Θ`/`P(Θ)`-driven stat source for `WorldEntityMember` replacing `PlaceholderBattleResolver`'s linear
`f(level)`, fed by (b) an empire-wide, per-type modifier — either finishing the already-reserved
`world-map-scope`/`world-buff.*` carriers (§5.3), or wiring the already-species-keyed `DemonType`
allocation scope (§5.2) into the world-map/siege pipeline for the first time — and (c) `slots.legion.
perDevelopmentLevel`'s existing count-growth knob turned on alongside it, so army *size* and army
*power* both answer to the same player investment.

## 6. Sharpening §5: the missing layer is per-STACK, keyed by training source — not per-species, empire-wide

**A real refinement from the owner, and it corrects where §5 was aiming.** Restated: *demon species
is the root template and should stay non-upgradable — a general demon is a copy taken from it — and
the actual confusion is that general demons have no owner-scoped upgrade container or management of
their own. In real strategy games, every troop stack (including neutral/wandering ones) carries its
own instance id and its own upgrade container, and a stack trained from a different base carries a
different container because that base's own building upgrade differs.* Verified against real code
before writing this — the picture sharpens in a genuinely useful way, and part of what looked missing
in §5 turns out to already exist one level up.

### 6.1 What's already built, one level above where §5 looked

**The confusion is real, but it is not "general demons have no owner at all" — it's that ownership and
identity exist at the wrong granularity.** `WorldEntity` (`WorldState.cs:286-...`) — a legion, warband,
**guard**, caravan, or warlord — already carries its own real `EntityId` and a real
`OwnerFactionId`, and `WorldFactionKind` (`FactionKindCatalog.cs:7-17`) already has a genuine neutral
value: **`Wild` — "unaligned wildlife and slot guards."** So "include neutral, wandering troops" is
already a real, modeled case today, not a gap — a `Guard`-kind `WorldEntity` owned by a `Wild` faction
is exactly that. **What has no identity or container of its own is one level down**: `WorldEntityMember`
(`WorldState.cs:274-283`) — the individual species-stack living *inside* a `WorldEntity`'s member list —
is a bare `SpeciesId`/`Level`/`Hp`/`Wounds`/`Role` record with no id field at all and nothing that could
hold an upgrade reference. It exists only as an anonymous list entry, addressable only by its position
inside whichever legion contains it — it cannot be independently tracked, split, merged, or reassigned,
which is exactly the "no management" half of the owner's own diagnosis.

### 6.2 The precedent for "training source shapes the output" already exists — it just stops at species choice

**Verified directly in the one real recruit call site.** `RaiseResolver.FoundLegion`
(`src/FusionRpg.Core/World/Growth/RaiseResolver.cs:124-142`) already takes the recruiting `sector` as
a parameter and already uses it to decide **which species** the new member is —
`SpeciesFor(sector.Climate)` (`:136`, "no new selection mechanism... deterministic, not rolled"). But
the SAME function's `Hp` comes from `RecruitPolicy.RaiseMemberHp` (`:138`), which traces to a single
**global tunable constant** (`RecruitPolicy.cs:45` → `WorldTuning.cs:147`,
`data/tuning/*.json`'s own `growth.raiseMemberHp`) — read with zero reference to `sector` at all,
even though `sector` is sitting right there in scope and `sector.DevelopmentLevel` already exists and
already scales *structure* HP through the real power ladder (§5.1). **The mechanism "what this sector
is" already shapes recruitment — it was only ever wired to affect *which* species, never *how strong*.**
Extending that same, already-present parameter is additive, not new architecture.

### 6.3 This is a different, complementary axis from §5 — not a replacement for it

§5's `DemonType`/`WhoKind.Type` wiring is an **empire-wide, species-keyed** bonus — the same value
reaches every zombie of that species everywhere, the AoE/Stellaris tech-tree shape. What this section
describes is **per-source, stack-keyed** — two stacks of the identical species, recruited from two
different bases with different development levels, are mechanically different, and that difference
travels *with the stack*, not with the species or the empire. This is the more precise reading of
**Heroes of Might and Magic 3's own Guardhouse precedent** already cited in §5.4: upgrading a
dwelling in *one town* makes only *that town's own future recruits* come out as Halberdiers — a
second, un-upgraded town keeps producing Pikemen indefinitely, side by side, in the same empire, same
turn. Both axes are real, both are precedented, and they are not in tension — an empire-wide research
bonus and a per-base training tier already coexist in HoMM3 exactly the way a Total War faction's tech
tree coexists with an individual Lord's own skill tree (§5.4).

### 6.4 What this concretely needs — sized against what already exists, not invented whole

- **`WorldEntityMember` needs its own light identity** — a `MemberId`, not a full `UniqueActor` (a
  troop stack is not an individually-equipped Unique demon; giving it one would blur Axis 1 back
  together). This is the minimum needed for "management" in the sense every strategy game ships it:
  addressing, splitting, or merging one specific stack independently of the legion currently holding
  it.
- **`WorldEntityMember` needs a reference to its own upgrade container** — captured **at raise time**,
  from the training source's own state (most directly, `sector.DevelopmentLevel` at the moment
  `FoundLegion` runs, mirroring exactly how `SpeciesFor(sector.Climate)` already captures a
  source-dependent fact once, permanently, into the new member). A stack does not need to re-check its
  origin sector's current level forever — HoMM3's own precedent doesn't either: the dwelling's state
  at recruitment moment is what stamps the unit, not a live link back to the town.
- **Neutral/Wild stacks need no special-casing** — a `Guard`-kind `WorldEntity` simply carries members
  with an empty/default upgrade container (or a fixed one representing that specific world encounter's
  own authored difficulty, the same way Delve/wave content already prices threat via fixed `Θ`
  constants, §5.1) — "no upgrade" is just the container's own default value, not a branch.
- **The one real call site this touches today is `RaiseResolver.FoundLegion`** — already sector-aware,
  already the sole production constructor of a founding member. No other system currently creates a
  `WorldEntityMember` from scratch (`LoamPhases.SpawnTheUnmade` is the only sibling, per `FoundLegion`'s
  own doc comment, and reads the identical shape).
- **Demon species itself needs no upgrade concept, confirmed by this investigation, not just
  asserted** — nowhere in the real code does anything treat `DemonSpeciesCatalog` as mutable or
  versioned per-empire; it is read-only content everywhere it's touched. The owner's own "species is
  the root, it should not need to upgrade" call is exactly what today's code already assumes — this is
  a case where the correct architecture and the correct product instinct already agree, and this
  section only makes the agreement explicit.

## 7. Enriching §6: stack experience, and a two-path upgrade choice that costs a respec to undo

Owner's own citation: Heroes 3's **In the Wake of Gods (WoG)** mod, specifically its creature
experience system, plus a request that merging two differently-chosen stacks force a single choice,
and that changing that choice later cost a respec. Investigated the same way as every section above —
genre facts verified by search rather than assumed, code claims verified by file:line.

### 7.1 Troop stack experience — a real gap, total, not partial

**Confirmed by reading every real construction site of `WorldEntityMember`
(`WorldTemplateCatalog.cs:194-225`, `RaiseResolver.cs:134-141`, `LoamPhases.cs:280`,
`WorldTemplateCatalog.TwoHearths.cs:287-299`): every one sets `Level` once, from a fixed content
value (1, 2, or 3) — there is no XP field on the record at all, and `Level` is never read-modify-
written anywhere after creation.** This is a genuine, total real gap, not a wiring gap dressed up —
there is no inert accumulator or disabled increment to point at.

**A second, independent finding worth flagging on its own:** `Level` already means two *different*
things to two different, uncoordinated consumers today — `PlaceholderBattleResolver.Strength`
(`:40`, `Hp × Level`) and `BattleRuleset.BaseAtk`/`BaseDefense(member.Level)`
(`DistrictAssaultResolver.cs:365-366`) are two separate private `f(level)` formulas, each already
flagged individually in §5.1 as instances of the "no private curves" problem. Finding them again here,
independently, from a different angle (stack progression) reinforces that this is one real,
recurring gap in the world-map/siege stack, not two coincidentally similar ones.

**WoG's own creature-experience system, verified by search, not assumed:**

- Stacks gain experience **only from battles led by a hero** — an unled garrison or wandering stack
  never gains XP. The amount a stack gains equals what the commanding hero itself gained that battle.
- Leveling up grants **real stat and special-ability bonuses**, viewable per-stack in its own
  experience detail screen.
- **The merge rule is specific and worth adopting as-is: merging two stacks with different experience
  totals AVERAGES their experience — never additive, never max-wins.**

### 7.2 Two upgrade paths, and whether switching costs anything — genre precedent says no, the owner's own call says yes, and that's worth stating plainly

**Two real, sourced precedents for "a creature gets a second, alternate upgrade path" exist, and both
land on FREE switching — the owner's own request is a deliberate departure from both, not something
borrowed from them:**

- **WoG's own "Alternative Upgrade" mod** gives select creatures (Crusader, Silver Pegasus, Iron
  Golem, Horned Demon, Vampire Lord, Minotaur King, Magma Elemental) a second upgrade tier, unlocked
  by buying a building — after which **"you can switch between upgrades... whenever you want when in
  town,"** at no extra cost (both upgrades are priced identically).
- **Official Heroes 5: Tribes of the East** gives every unit an alternate upgrade and lets the player
  **"switch between these variants freely while inside a city,"** provided the right buildings exist —
  again, no permanent cost or lock-in.

**So "choose 1 of 2, and switching later is a respec" is the owner's own harder-commitment design
choice, correctly attributed here as such rather than as genre precedent** — a real, deliberate
divergence in the same spirit as this doc's own earlier examples (the fusion pick-count ceiling,
§3.4 of the sibling doc) where the owner read the tradeoff and chose the less genre-typical option on
purpose.

**The mechanism for "costs a respec" already exists, built, and is the obvious thing to reuse rather
than invent a new price curve:** `TreeRespecPolicy.PriceOf`
(`src/FusionRpg.Core/PassiveTree/State/TreeRespecPolicy.cs:21-33`) — `price(count) = basePrice +
basePrice × count × escalationPermille / 1000`, escalating per respec already spent, "always
available, always priced, never refused" except on insufficient balance — is the exact structural
sibling of `RespecPolicy.PriceOf` for aptitudes (`src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs:36-48`).
A third respec counter for "which upgrade path this stack chose" would be the same shape a third time,
not a new mechanism — this repo already has the precedent for "a category, once chosen, costs an
escalating fee to un-choose" twice over.

### 7.3 Putting 7.1 and 7.2 together with §6's own upgrade-container concept

- **The per-stack upgrade container from §6.4** is where "which of the 2 paths this stack chose" and
  "this stack's own accumulated experience/level" both naturally live — neither needs a field on
  `WorldEntityMember` directly; both are exactly what that container was already proposed to hold.
- **The merge rule needs two different resolutions for two different kinds of data, and WoG's own
  precedent only covers one of them:** experience is numeric — WoG's averaging rule (§7.1) applies
  directly. The chosen upgrade path is categorical — there is no "average" of path A and path B, so a
  merge of two stacks that chose *different* paths must force a single choice at merge time (matching
  the owner's own framing: *"when merge to troop, the tree path must choose 1 of 2"*). Merging two
  stacks that already agree on their path is the trivial case — nothing to choose, the merged stack
  keeps it.
- **The choice point itself is naturally the same moment HoMM-family games already gate it at**: when
  a stack reaches its own upgrade tier (the creature's normal→upgraded transition, §5.4/§6.3's own
  per-source `DevelopmentLevel` trigger), not at raise time and not arbitrarily mid-campaign — matching
  how neither WoG nor Heroes 5 lets a player pick an alternate upgrade before the base tier exists.
- **Changing an already-made choice later** — whether by player request or forced by a merge the
  player didn't want — is priced through the reused respec curve (§7.2), giving the mechanic a real,
  named cost instead of an unbounded free toggle, which is the actual substance of the owner's own
  "consider as respec" instruction.

## 8. Enriching §7: a near-death survivor can be promoted to a Unique demon — one at a time, low chance, tunable

Owner's own proposal: a general-demon troop stack that fights, comes near death, **survives, and
levels up** gets a chance — tunable, meant to be low — for exactly **one** member to be promoted into
a real Unique demon. Investigated the same way as every section above.

### 8.1 A real, foundational prerequisite this surfaces, not previously named

**Verified directly: `WorldEntityMember` has no count field at all** — its full field list is
`InstanceId`, `SpeciesId`, `Level`, `Hp`, `Wounds`, `Role` (`WorldState.cs:274-283`), confirmed by
grep across the whole file. This repo's own cited HoMM3 analogue (`base-defense-ideal.md:574,837`)
describes the inspiration precisely — *"HOMM3's seven slots hold stacks of one creature type each, up
to 9,999"* — but the shipped model never implemented the countable-stack dimension: a `WorldEntityMember`
today is one aggregated Hp/Wounds pool, not N trackable individuals. **"Promote exactly one, leaving
the rest behind as still-general" has no natural expression without a count** — you cannot subtract
one individual from something that was never modeled as a plurality. This is a real, previously-
unnamed prerequisite gap, surfaced by this specific proposal rather than by §5-§7's own investigations,
which never needed to divide a stack into individuals.

### 8.2 What's already built and directly reusable for every other piece of the mechanic

- **"Near death," precisely.** `member.Hp - member.Wounds` is the exact effective-HP expression
  already used identically in three places (`DistrictAssaultResolver.cs:347,450-451`,
  `PlaceholderBattleResolver.cs:40,131-132`) — and a per-mille wound-ratio threshold is **already a
  real, tunable, named concept**: `FrontierRulesPolicy.RecoverAtMilli`
  (`src/FusionRpg.Core/World/Ai/FrontierRulesPolicy.cs:25-26`, "wounds above this, in per-mille of a
  member's health") — built for a different purpose (AI retreat-to-heal decisions), but the exact
  shape ("near death" as a per-mille wound band) already exists and is already tunable.
- **"Survives,"** already exactly the rule both combat resolvers use to decide who lives:
  `newWounds < member.Hp` (`DistrictAssaultResolver.cs:451`) / `wounds < m.Hp`
  (`PlaceholderBattleResolver.cs:132`) — the same two independently-implemented survivor checks §7.1
  already flagged as a coordination gap, reinforced a third time here.
- **"Levels up"** is the real gap §7.1 already named (stack experience does not exist yet) — this
  mechanic is naturally sequenced *after* §7's own stack-leveling system, since it triggers on a
  level-up event that today never fires.
- **A tunable, banded, deterministic "low chance" roll is not a new mechanism — this repo already
  built the closest analogous transition once.** `CaptureChance`
  (`src/FusionRpg.Core/Delve/Wild/CaptureAction.cs:39-`, D4.6) computes a capture roll — "a wild thing
  becomes a player's own demon" — from an `HpBandOf` banding (low/half/high, driven by
  `DungeonTuning`'s own `HpBandMilli`) combined with other authored modifiers, resolved via a seeded
  roll, everything sourced from already-shipped tuning, nothing resolved by the file itself. **This is
  the same class of transition ("something not currently a `UniqueActor` becomes one") as a stack
  promoting a member**, and its own HP-banding shape maps directly onto "near death" as the low band.
  Reuse the *pattern* (band the wound ratio, combine with a tunable base chance, roll deterministically),
  not the Delve-specific class itself, since world-map/siege promotion is a different program.
- **The mint itself is already a solved, shared path.** `RpgStore.MintDemon` (used identically by
  summon, fusion, capture, and this session's own debug-grant seam) is the one real place a
  `UniqueActor` gets created from a `DemonMintSpec` — promotion is a fifth caller of the same function,
  not a new one.

### 8.3 Genre prior art — the closest precedents found, honestly short of an exact match

Search found strong precedent for the *general* shape ("units get individually notable through
combat") but no exact match for "generic troop, specifically triggered by near-death survival, rolls
a chance to become a distinct hero" — worth stating plainly rather than force-fitting a citation:

- **Veterancy systems are a well-established RTS staple** (Company of Heroes, Command & Conquer/Kane's
  Wrath's Elite units, Warhammer 40K: Dawn of War) — a unit gains rank from kills/damage dealt and
  becomes visibly, mechanically special, reinforcing that "a generic unit earns individual notability
  through combat" is a proven, well-liked pattern, not a novel risk.
- **Total War: Three Kingdoms' own `Resilience` trait** is the closest specific precedent for
  "surviving a near-fatal wound is itself a distinct, named, sometimes-innate mechanic" — certain
  characters (some by default, via a background trait) can survive being fatally wounded or even
  killed in battle. Character-scoped, not troop-to-hero promotion, but the closest real precedent
  found for treating near-death survival as its own notable event worth a special rule.
- **The specific combination the owner is proposing — near-death survival + a level-up event + a low,
  tunable chance + promotion from generic-troop to named-individual — reads as a genuine, useful
  synthesis of these pieces rather than a single game's shipped system.** Naming this honestly matters
  more than forcing a citation that doesn't quite fit.

### 8.4 What this needs, sized against what's already there

- **The count field named in §8.1** is the one real, structural prerequisite — without it, "promote
  one, N-1 remain general" has to be approximated (e.g., the whole stack converts, and a fresh
  general stack is separately reconstituted at the source sector) rather than expressed directly.
- **The trigger check** — near-death (reuse the `RecoverAtMilli`-shaped per-mille wound band),
  survived (already the shared survivor rule), leveled up this battle (§7's own stack-XP system,
  built first) — composes three already-real or already-planned facts; no new state beyond a level-up
  event flag for the check to read.
- **The chance roll** reuses `CaptureChance`'s own banding *pattern* (§8.2) with a new, named,
  tunable base rate — "low" is a product/balance call for whoever authors the tuning row, not an
  architecture decision this doc makes.
- **The promotion itself** is a fifth `RpgStore.MintDemon` caller (§8.2) — the mechanism that turns
  "not yet a `UniqueActor`" into "a real specimen" is already solved and shared by every other
  acquisition path; this would not be a sixth, different one.

## 9. Deferred: the "Survivor Title" achievement system — a permanent, earned buff bound to one specimen

**Owner's own framing, and it's the right one: this is explicitly future work, not something to spec
or build now.** The idea: a promoted survivor (§8) earns a permanent "Survivor Title" — an effect-atom
container that buffs that one specific Unique demon forever — and titles more broadly should be
earnable through world events, Delve/quest events, and quest completion, all bound together through
new seedsmith content pipelines. Investigated the same way as every section above, so the eventual
spec starts from real ground rather than assumptions.

### 9.1 "Title" doesn't exist, and adding it is a reviewed vocabulary change — not a casual one

`ContainerKind` (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:25-38`) is an explicitly **closed,
eleven-value enum** (`Item, Trait, Skill, SpeciesPassive, Patron, WorldBuff, Enemy, Gem, Charm, Combo,
Consumable`), and its own doc comment states every addition to date was a reviewed change tied to a
named spec — including one rejected candidate (`set`) because it collided with an existing DB concept.
**A Title would be a twelfth value, following the identical review pattern, not a repurposing of
`Trait` or `WorldBuff`** — `Trait` is per-species/flavour-tag shaped (§2.1), and `WorldBuff` is
reserved for empire-wide effects (§5.3), neither matches "one specific specimen, permanently."

### 9.2 The real prerequisite: no working mechanism exists yet to bind a permanent, effective container to one specimen — confirmed at the code level, not just cited

**This is the load-bearing finding, and it sits underneath the whole idea, not beside it.** None of
this repo's existing per-specimen container shapes fit "permanent and mechanically real":

- **Equipment** is per-specimen but explicitly **removable** (un-equip) — wrong shape.
- **`SpeciesPassive`** is permanent but **shared across every specimen of a `(playerId, speciesId)`
  pair** (§2.1's own `player_species` finding) — wrong shape.
- **`Trait` has the RIGHT storage shape** — `DemonProfileDto.TraitIds` is per-`instanceId`, permanent,
  never removed — **but its effect-resolution path is a real, confirmed wiring gap, not a working
  mechanism**: `TraitAtomSource.FromContainers` (`src/FusionRpg.Core/Battle/TraitAtomSource.cs:55-90`)
  correctly reads bound `trait.{id}` containers into real battle stat mods — it works, it's tested —
  but **production never calls it with a real per-specimen container**.
  `BattleStatComposer.Traits` (`BattleStatComposer.cs:80`) instead defaults to
  `TraitAtomSource.Shipped()`, one hard-coded, catalog-wide entry (`critical-hunter` → a flat bonus
  applied to *every* demon carrying that trait id, never a per-specimen roll). This is the code-level
  proof behind §2.1's own "TraitIds… cosmetic, never resolves to real atoms today" claim, not a new
  finding on its own — but it matters here specifically: **a Title system's own payoff (the permanent
  buff actually working) depends on closing this exact wire first.** A Title mechanic riding on
  `ContainerKind.Trait`'s own pattern would need to close it as a shared prerequisite, not invent a
  parallel one.

### 9.3 The three trigger sources are at three different real states — none of them "doesn't exist," none of them "ready either"

- **World events — a genuine, total real gap.** No `spec-world-event*.md` exists, and no code
  construct answers "a notable thing happened on the world map, react to it for a reward" — the one
  thing actually named `TurnEvent`/`TurnEventQueue` (`src/FusionRpg.Core/World/Turn/TurnEventQueue.cs:1-18`)
  is a purely internal per-turn movement-sequencing mechanism (`Arrival, Contact, Crossing, Halt`),
  never player-facing, never a reward seam. This would be new design work, not a wiring fix.
- **Delve/dungeon party events — built, real, and the closest existing seam.** The `event-deck`
  module (owner-approved wave 3) ships real per-room seed-deterministic draws with player choices and
  consequence resolution, already distinct from combat resolution. **But its own locked rule is the
  opposite of what a Title needs**: grants are explicitly delve-scoped and withdrawn at extraction
  (`spec-event-deck.md:43-49`, "no event may write a stat directly… withdrawn at extraction") — a
  Title trigger reading this outcome would need a genuinely NEW "make this one permanent" path, not
  reuse of the existing withdraw-on-extraction grant.
- **Quests — built, and a real, already-designed permanent-reward seam exists.** `QuestReward.Request(quest,
  delve)` (spec-delve-quests.md §4) fires once, at extraction, on quest completion — structurally the
  same shape a Title grant would need (a one-time, non-withdrawn award), just aimed at a container mint
  instead of the loot pipeline it serves today.
- **A genuinely important operational note, not a design one:** the entire Delve content stack (quests,
  events, rooms, encounters, domains, supplies) is **not waiting to be built — it is built, committed,
  and being actively extended by another session right now** (real, uncommitted work found mid-
  session: `QuestArchetypeEventBridge.cs`, `QuestLootBindingBridge.cs`, `EventSeedFile.cs`, and new
  `data/seed/dungeon/{domains,encounters,events,rooms}/` directories). **Any eventual Title spec should
  name the two stable trigger points (`QuestReward.Request`, the event-deck's own consequence
  resolution) without depending on the specific files currently mid-edit** — this is exactly the kind
  of concurrent-session situation this repo's own established discipline treats as "read, don't touch,
  until the owner reports it done."

### 9.4 Seedsmith pipelines: no new pipeline family needed — extend what already generates Delve content

**No `world/` or `achievement`/`title` seedsmith adapter exists** (confirmed by grep, zero hits). But
the **`dungeon/` adapter already covers quests and events** for the Delve layer with mature, actively-
growing machinery (`briefs.py`, `pipelines.py`, `registries.py`, `audit.py` — the same
brief-assembler/deterministic-emitter/closed-vocabulary-validator shape `demons/anchor` already uses
for species classification). **"Bind titles across three content sources through seedsmith" is best
framed as extending this one adapter's own registries to add a `world` source and generalizing its
already-built quest/event machinery — not standing up a fourth, unrelated pipeline family.**

### 9.5 Genre prior art — and one direct, useful correction to the owner's own framing

- **World of Warcraft's own title system — the most famous example — is confirmed PURELY COSMETIC,
  granting no stat bonus at all.** This is worth stating plainly rather than assumed: **the owner's own
  proposal (a title that permanently buffs combat power) is a deliberate departure from the genre's
  dominant "title" pattern, not an extension of it** — the same honest-attribution discipline this doc
  already applied to the fusion pick-count ceiling (§3.4 of the sibling doc) and the respec-on-switch
  rule (§7.2).
- **Total War: Medieval III's "Retinue" system is the closest found precedent for "an earned trait as
  a stored, reusable object attached to one unit"** — regional retinues earn traits over time
  representing local military tradition, extending this doc's own already-cited Three Kingdoms
  `Resilience` trait (§8.3) in exactly the direction this section needs (the trait-as-object, not the
  survival-trigger itself).
- **Diablo's named uniques are drop-condition-bound, not achievement-bound** — a different shape
  ("a specific source guarantees a specific item"), closer to a loot table than a title, but a useful
  contrast: it confirms "earned by doing X specifically" is a well-worn genre pattern even outside
  MMO title systems.
- **A real, live design tension, confirmed by search rather than assumed: permanent, stat-granting
  achievement rewards are a genuine power-creep risk other live games actively argue about** (ESO's own
  Champion Point system is a standing community flashpoint on exactly this). This repo's own
  no-hard-progression-ceiling philosophy (`CLAUDE.md`) means a permanent buff is not itself forbidden
  here — endless grind is already the SSOT — but it's the same reason §8's own "low chance, one
  survivor at a time" framing matters: that's the lever real games already use to keep an
  achievement-granted permanent buff from trivializing content, and it should carry forward into
  whatever eventually specs the Title system itself.

### 9.6 What "defer to the future" concretely means here

Nothing in this section is ready to spec. In order, before a spec could responsibly start: (a) close
the `Trait`-container wiring gap (§9.2) so a permanent per-specimen buff can work AT ALL, for any
source; (b) decide whether world events are worth designing from scratch or whether Delve
quests/events alone are enough sources for v1 (§9.3's own honest split); (c) let the concurrent
session's own Delve-content work land and stabilize before naming file:line integration points that
would otherwise go stale immediately (§9.3's own operational note). None of this blocks §5-§8's own
findings, which stand on their own.

## Sources (genre prior art)

- [Demonic Compendium — Megami Tensei Wiki](https://megamitensei.fandom.com/wiki/Demonic_Compendium) — species/build-level registration and re-summon-at-a-fee model.
- [Template:Compendium — Megami Tensei Wiki](https://megatenwiki.com/wiki/Template:Compendium)
- [Pokémon Base Stats — mackfey-csep590a project page](https://mackfey-csep590a-25wi-a7725f7512efb75d5e8861f9283d5e6a0079b34f9.pages.cs.washington.edu/fp/final/base_stats.html) — fixed per-species base stat table.
- [Pokémon IVs vs EVs: Stats, Limits & Training Explained — gamingstation.org](https://gamingstation.org/pokemon-ivs-vs-evs/) — per-individual IV/EV/moveset layer on top of shared base stats.
- This repo's own already-cited genre pass: `docs/research/genre-mechanics/06-summoner-minion-fusion-rpg.md` §1.5 (Persona/Nocturne/SMT IV/V fusion-inheritance determinism arc — already used directly in `demon-mechanism-gaps-ideal.md` §3.3, not re-derived here).
- This repo's own already-cited genre pass: `docs/research/genre-mechanics/07-rts-and-autobattler.md` §6.1 (Age of Empires II: 194 techs / 245 units, mean 70.8 techs per civ, measured from shipped data) and §6.2 (StarCraft II Co-op's three-layer Commander level / account-wide Mastery / Prestige split).
- [Pikeman and Halberdier — Heroes 3 wiki](https://heroes.thelazy.net/index.php/Pikeman_and_Halberdier) and [Halberdier (H3) — Might and Magic Wiki](https://mightandmagic.fandom.com/wiki/Halberdier_(H3)) — the Guardhouse building upgrade's exact stat delta (Atk 3-5→4-6, Speed 4→5), a permanent troop-tier lever fully orthogonal to hero level/skills/artifacts.
- [Blacksmith II — Units, Forgotten Empires](https://www.forgottenempires.net/strategy/age-of-empires-ii-strategy-center/blacksmith-ii-units) — confirms Blacksmith techs apply to a whole unit type, including unique units.
- [Character Stats — Total War: Warhammer Wiki](https://totalwarwarhammer.fandom.com/wiki/Character_Stats) — named Lords get individual skill trees/items; generic units get a general's aura plus faction-wide tech bonuses applied to specific unit types.
- [Technology — Stellaris Wiki](https://stellaris.paradoxwikis.com/Technology) — empire-wide tech buffs mass-produced fleets directly, kept deliberately separate from individual leader/Admiral leveling.
- [Heroes of Might and Magic III: In the Wake of Gods — Might and Magic Wiki](https://mightandmagic.fandom.com/wiki/Heroes_of_Might_and_Magic_III:_In_the_Wake_of_Gods) and [In the Wake of Gods — Heroes III Wiki](https://homm.fandom.com/wiki/In_the_Wake_of_Gods) — creature stack experience: hero-led-battle-only gain, stat/ability bonuses per rank, and the merge-averages-experience rule.
- [In the Wake of Gods/Commanders — Heroes III Wiki](https://homm.miraheze.org/wiki/In_the_Wake_of_Gods/Commanders) — the separate Commander-unit skill system (4-of-6 primary skills, gated special abilities) — a different mechanic from creature-stack experience, not the one this section adopts, named here only to avoid conflating the two.
- [Killing For That Promotion: Veterancy Systems in RTS — Wayward Strategy](https://waywardstrategy.com/2020/11/05/killing-for-that-promotion-veterancy-systems-in-rts/) — the general RTS veterancy pattern (Company of Heroes, Command & Conquer/Kane's Wrath) of a generic unit earning individual notability through combat.
- [Resilience — Total War Wiki](https://totalwar.fandom.com/wiki/Resilience) — Three Kingdoms' own named mechanic for a character surviving a fatal wound in battle, the closest specific genre precedent found for treating near-death survival as its own distinct, mechanically-real event.
- [WoW Titles Complete Guide — rpgradar.com](https://rpgradar.com/wow-titles-guide/) and [How to get WoW titles — esportsinsider.com](https://esportsinsider.com/how-to-get-wow-titles) — confirms World of Warcraft's own title system is purely cosmetic, granting no stat bonus — the genre's dominant "title" pattern the owner's own proposal deliberately departs from.
- [Diablo 4 Unique Items — pcgamesn.com](https://www.pcgamesn.com/diablo-4/uniques) — named uniques bound to a specific drop condition, a loot-table-shaped contrast to an achievement-granted title.
- [Experience and rank — Total War: Warhammer Wiki](https://totalwarwarhammer.fandom.com/wiki/Experience_and_rank) — battle-experience rank chevrons raising unit stats, the broader Total War veterancy precedent.
- [Retinue — Total War Center](https://www.twcenter.net/threads/retinue.331613/) — Total War: Medieval III's regional retinues earning traits over time, the closest found precedent for an earned trait as a stored, reusable object bound to one unit.
- [Elder Scrolls Online forums — Champion Point power-creep discussion](https://forums.elderscrollsonline.com/en/discussion/439241/) — a live, real example of the power-creep tension a permanent achievement-granted buff invites in a live-service game.
- [Alternative Upgrade mod download — Heroes 3.5: Wake of Gods Portal](https://heroes3wog.net/alternative-units-mod-download/) — a second creature upgrade tier, freely switchable in town once unlocked, no permanent cost.
- Heroes of Might and Magic V: Tribes of the East's own alternate-upgrade system — every unit gets a second upgrade path, freely switchable in a city with the right buildings, no permanent cost (searched, no single stable primary-source URL found beyond mirrored wiki/community summaries — stated here as a well-attested community fact, not a single citable page).
