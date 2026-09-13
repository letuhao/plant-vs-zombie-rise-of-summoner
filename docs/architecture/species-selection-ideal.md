# Species selection — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.

**Program:** `species-selection`. Map (when approved) → `docs/architecture/species-selection-map.md`.
**Not a new program in the sense `gameplay-tiers-ideal.md:156` closed** — the work lands in programs that
already own the surfaces: **battle** (`WaveCatalog`), **party-dungeon** (`Encounter`/`SlotFill`),
**expeditions**, and the quest/objective vocabulary. This doc is the reasoning trail.

**Why it exists.** `tier-system-ideal.md` § AUDIT found that species tiers, trophies, drop tables and
family materials are all downstream of a creature the player never meets — and that this, not any tier
question, is the real first module. Owner chose a dedicated idea phase for it.

---

## Which loop this extends

From [the-loops.md](../guide/the-loops.md), which is the product-vision SSOT. No new loop is invented.

- **Place 3 — farm, hunt, defend**, specifically the **Hunt** verb: *"lawn kills, capture, wild joins,
  roaming, delves, map prey."* Hunt is a named verb in the vision that has no mechanism today.
- **Place 2 — idle expeditions**, already described as *"Monster Hunter–style… materials for actors and
  empire."*
- **Place 6 — the Delve**, which owns the one real weighted selector in the repo.
- **Place 1 — the lawn**, whose waves are the most-played encounter surface.
- **Spine B — creature summon and fusion**: what you *fight* and what you *collect* are two different
  pipelines, and this doc is about the first.

---

## Load-bearing principles, restated inline

- **Every RPG feature lives in the RPG layer — never by changing what PvZ is.** Which creature appears in
  a wave, a room or a hunt is decided by selectors in `FusionRpg.Core`. The lawn's own spawner is
  untouched; the RPG observes and contributes, it does not rewrite PvZ.
- **One power ladder.** Any difficulty or threat read goes through `Θ` (contests) and `P(Θ)` (magnitudes);
  a tier reaches a magnitude only as an additive per-rung `thetaOffset`, from a table, applied once — never
  a private `f(level)`.
- **The balance surface is data.** Encounter weights, slot counts, rates and thresholds live in
  `data/tuning/<domain>.v{n}.json`, carry their units, and a missing value is a load rejection naming it.
- **No hard progression ceilings.** A per-area pool is content breadth, never a wall on growth.
- **Gameless-first is capability.** Hunting must work with Fusion closed; the injector may enrich the lawn
  surface, never gate the verb.
- **A guardrail validates the contract and closed enums, never a population count.** Every roster figure
  below is a **reading that grows as content ships**.
- **Two async systems; record-then-drain.** Anything touching the live lawn observes past events and
  contributes a signed delta later; delay is the designed degradation mode.
- **General vs unique creature are different things** (`creature-system-map.md` Vocabulary): what you
  **fight** is a *general creature* — engine-spawned, species-only, troop-stack shaped, no persistent
  instance. What you **collect** is a *unique creature* — a real `UniqueActor` specimen. This doc is about
  the first, and conflating them is the documented failure this vocabulary section exists to prevent.

---

## What this is

In the player's language: **you can own almost every creature in the game and fight almost none of them.**
The altar can summon 871 of 906 species. The battlefield shows you the same ten, forever, in the same
order — and there is no way to say "I want to hunt *that* one."

In system language: the collection pipeline is wide and rolled; the **encounter** pipeline is narrow and
**deterministic**. Every enemy selector in the repo picks by `pool[i % count]`, ordinal-first, or
`argmax(rarity)` — none of them rolls. The one selector that *is* rolled, weighted and anti-repeat-capped
is unwired. And the objective vocabulary has no species target, so a hunt cannot be asked for.

---

## What already exists

Verified against code and the committed corpora 2026-09-13. **Counts are readings, not constants.**

### Built

- **Acquisition is wide open.** Measured over 906 generated species: **871 carry `Summonable` (96%)**;
  the exceptions are 29 `CaptureOnly`, 4 `EventOnly`, 2 both, 2 none. `SummonRoller` band-gates by rarity
  with a downgrade walk (`SummonRoller.cs:152-153`) and ships real pity — epic hard at 25, legendary soft
  ramp from 41, hard at 55, plus a 10-pull rare floor.
- **The Delve owns a genuinely good selector.** `SlotFill.Draw` (`SlotFill.cs:64` → `WeightedChoice.Pick`)
  is weighted RNG with **climate weighting** (on-climate 1000, off-climate `offClimateMilli`,
  `SlotFill.cs:55`) and a **same-species seat cap** — `⌈n × sameSpeciesMaxMilli / 1000⌉`
  (`SlotFill.cs:43`, enforced `:54`), draw-without-replacement past the cap. That is an XCOM-style
  anti-repeat guard, already written.
- **The expedition wild-met roll is the only real species roll a player experiences.**
  `ExpeditionResolver.cs:235` — `band[rng.NextInt(band.Count)]`, rarity-gated at `:229-232`
  (`<840‰` Chaff, `<990‰` Cultivated, else Heirloom), fresh per tick from
  `SeededRng.DeriveStream(seed, "tick:"+t)`. It is not a fight — you recruit the creature or it leaves an
  essence.
- **The acquisition vocabulary exists** — `CreatureAcquisition { Summonable, CaptureOnly, EventOnly }`
  (`CreatureRarity.cs:32-38`), plus `CreatureDeployMode { PlantAvatar, HypnoAlly }`.
- **Wave rosters are real content**, authored in `data/tuning/waves.v1.json` and assembled by
  `WaveCatalog.Band` / `.Enemies`.
- ⭐ **Creatures already spawn on the world map, in production, today.** This is the single most important
  thing in this document, and it was nearly missed:
  - `UpdateNeglectAndSpawnTheUnmade` (`World/Loam/LoamPhases.cs:237-269`) runs inside the turn engine and
    spawns **`Wild`-owned `Warband` entities onto neglected, `Lost`, barren sectors**, gated on
    `neglectedTurns >= LoamPolicy.UnmadeSpawnAfterTurns` with an **occupancy guard** (`:255-258`) and a
    real turn-report event, `"unmade.spawned:" + sectorId` (`:261`).
  - `WorldFactionKind.Wild` exists (`FactionKindCatalog.cs:17`, id `"wild"`) and **every world template
    instantiates a Wild faction row** (`WorldTemplateCatalog.cs:79`, `TwoHearths.cs:30`).
  - `WorldEntity` carries a real map position — `AtSectorId`, *"At a sector, or on a lane — never both,
    never neither"* (`WorldState.cs:293`) — and `WorldEntityKind.Warband` is already used for a
    hand-authored wild pack (`WorldTemplateCatalog.cs:220`, `e-wild-pack-1`).
  - `WorldEntityMember.InstanceId` is explicitly *"null for non-player forces and guards"*
    (`WorldState.cs:277`) — the member model was **designed for wild creatures**, not adapted to them.

  **So "creatures spawn on the map" is not new work.** The faction, the entity kind, the position field,
  the occupancy guard, the turn-report event and the spawn trigger are all built and running.

### Wiring gap

*Machinery exists and is inert or mis-parameterised. None of these is a wall; each names its line.*

- ⛔ **`WaveCatalog` has no RNG at all.** `WaveCatalog.cs:165` — `var species = pool[i % pool.Count];`
  with `count << pool.Count`, so it always takes the **first N by ordinal `SpeciesId`**
  (`Band` sorts `StringComparer.Ordinal`, `:153-154`). **Zero seeds, zero rolls.**
- ⛔ **`Band` covers 4 of 10 rungs** — Chaff, Cultivated, Heirloom, Sunwoven = 246 of 904 eligible; the
  other six rungs (**658 species, 73%**) are unreachable by any wave. Its own comment at `:121-126` admits
  the four-rung limit was an artifact of the old 84-species catalog — **that comment is now stale**, since
  the live roster populates all ten.
- **Consequence, verified on two independent derivations:** the union across all four waves is
  **10 distinct species** — AshThreePeater, BoatImp, BucketZombieDuck, ConeZombie, Apple, Bamboo,
  BigSunNut, BedRockSnowZombie, BlackElephantZombie, AcientSunNut. `Fused` — **486 species, 54% of the
  roster** — appears in no wave band at all. The `Sunwoven` band holds only 4 species, so `rift-tyrant`'s
  single sunwoven slot is **always** AcientSunNut.
- ⛔ **The good selector is transitively test-only.** `Encounter.Build` (`Encounter.cs:91`) is called at
  `DomainEncounterCoverage.cs:96` and `EncounterPreflight.Run` at `DomainEncounterPreflightBridge.cs:75`,
  but both callers are themselves test-only; `EncounterCorpusBuilder.Build`, `EncounterSeedFile.LoadAll`,
  `DomainEncounterPreflight.Build` and `RpgStore.ImportDungeonDomains` have **zero production callers**.
- ⛔ **The selector refuses most of the corpus.** `SlotFilter.Candidates` (`SlotFilter.cs:143-144`) throws
  `EncounterRefusal` on the first anchor with a null `ThreatBand` — *"never the rung-4 default"* — and only
  **419 of 904** anchors carry one. Correct behaviour, starved input.
- **Expedition battles do not roll either** — `ExpeditionResolver.cs:85` indexes a hardcoded 4-entry wave
  chain per tier (`:197-204`); the expedition seed seeds combat, never species choice.
- **`WaveCatalog.Band` has no acquisition filter** (`:153-154`), so a `CaptureOnly` or `EventOnly` species
  is wave-eligible — a pinned quirk that matters the moment bands widen.
- ⛔ **The map spawner picks one hardcoded species.** `SpawnTheUnmade` (`LoamPhases.cs:271-282`) builds
  every member as `new WorldEntityMember { SpeciesId = "normalzombie", Level = 1, Hp = … }` (`:280`).
  **A string literal is the entire species-selection logic for the only wild spawner on the map.** Same
  defect shape as every other selector in this document — built machinery, constant choice.
- **World raise is ordinal-first per climate** — `RaiseResolver.cs:157-169`, *"Deterministic, not rolled"*:
  exactly **6 possible species world-wide**, one per element.
- **Lawn Zomboss deploy is `argmax(rarity)` with an ordinal tie-break** (`ZombossDeployPolicy.cs:79-82`);
  the RNG decides only *whether* to deploy, never *what*. Because ceilings step only at waves 1/5/10/15/20,
  a full run yields **~3 distinct reinforcement species**.
- ⚠ **A latent divergence to fix when wiring:** `SlotFilter.cs:49` computes
  `CreatureTypeId => floor + GameTypeId` **without** the `+50,000` plant offset that
  `ConcreteSpeciesSeedReader.cs:91` and `CreatureSpeciesGenerator.cs:74` both apply. Latent only because
  the path is inert.

### Real gap

- **No hunt interaction.** Reaching a wild creature on the map does nothing today — there is no encounter
  verb for a `Wild`-owned warband beyond ordinary world-map combat. **Deferred by owner direction** to the
  world-stage extension (§ The shape, move 3), so this is a *named* gap with a trigger, not an open one.
  For the record, the abstract alternative also does not exist: `ObjectiveTargetKind` is closed at
  `{ RoomKind, CurioKind, ItemKind, Boss, None }` (`ObjectiveTemplateCatalog.cs:5-12`) with **no
  `SpeciesKind`**, `grep -i species src/FusionRpg.Core/Delve/Quests/` returns **no matches**, and a
  repo-wide grep for `targetSpecies|huntTarget|bounty` finds only an item affix and two drop-table names.
- **No per-area or per-sector species table.** Wave content names rarity *bands*, not species slots, and
  the map spawner names a literal — so there is nowhere to express "these creatures live here, at these
  weights."
- **No roster-coverage metric.** Nothing measures what fraction of the roster a player has actually met, so
  the problem this document describes was invisible until it was counted.

---

## Prior art

Numbers, formulas and documented failure modes, sources inline. Blocked domains are named.

### Pokémon — the canonical answer to "1,000 species, ten visible at a time"

Fixed-size **encounter slots per area**: **10** slots in early generations, **12** in Gen V, each slot
holding one species plus a level range and a **pinned probability**. The Gen V distribution is
**20 / 20 / 10 / 10 / 10 / 10 / 5 / 5 / 4 / 4 / 1 / 1 %**; Gen I resolves slots against fractions of 256
rather than percent ([GameFAQs encounter tables](https://gamefaqs.gamespot.com/gameboy/367023-pokemon-red-version/faqs/64175/encounter-tables);
Bulbapedia returned **HTTP 403** to automated fetch this session, so its table is cited at second hand).
So the shipped answer is **a small rolled table with a long tail, and high table turnover across areas** —
and critically, **rarity lives in the slot weight, not in a separate system.**

⭐ **The line that indicts our current selector:** with a fixed or rotating set and no roll, *the long tail
(the 1% and 4% slots) is never reachable at all* — so **removing the roll removes rarity as a concept.**
That is precisely what `pool[i % pool.Count]` did here: our ten-rung ladder means nothing at the encounter
layer, because nothing rolls against it.

### Monster Hunter — targeting as *extra rolls*, not a different table

- **Investigations** are earned by playing — they drop from killing, capturing, breaking parts and
  investigating tracks, so the targeting currency is a **byproduct of play**, never a purchase
  ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Investigations)).
- Each carries a randomised envelope: **2–5 bonus reward boxes** (bronze/silver/gold), **3–8 attempts**,
  **1–5 faints**, **15–50 min**, and *"harsher conditions generally means more gold slots"* — **the
  constraint dial and the reward tier are coupled.**
- ⭐ **The design lever is extra reward rolls, not a modified drop table.** Per-tier rate deltas are
  **UNVERIFIED** — no sourced numeric table was found.
- **Hoarding has a shipped answer:** 250 investigations held, 50 registered, and at the cap **the oldest
  are silently deleted (FIFO)** (player-reported; not found on a first-party Capcom page, so **UNVERIFIED
  at source**).

### Guiding Lands — a level-gated pool plus a consumable deterministic override

Six regions, levels **1–7**, with monsters gated by level (Zinogre at Forest 2; Brute Tigrex, Gold Rathian
and Silver Rathalos at **6**; Ruiner Nergigante at any region **≥5**)
([RPG Site](https://www.rpgsite.net/feature/8955-monster-hunter-world-iceborne-the-guiding-lands-and-the-endgame-explained)).
**Zero-sum decay is the core mechanic** — *"as you strengthen one region, all the others will weaken"* —
with a player-reported summed cap of ~**20** and a default shape of **7/7/7/4/1/1**. At region level **6**
any non-elder lured is guaranteed tempered; at **7**, elders too (player-reported).

⭐ **The escape hatch is the shape to steal:** fully analysing a Special Track yields a **single-use Lure**
that summons a chosen monster **regardless of region level**. A stochastic, gated pool **plus a consumable
deterministic override.**

⚠ Documented complaint: the decay makes progress feel self-cancelling, with grind horizons
players put in the hundreds of hours (player-reported).

### Bounties as a targeting verb

- **The Witcher 3** ships **26** contracts total (11 Velen, 8 Novigrad, 6 Skellige, 2 White Orchard) —
  notice → haggle → investigate → kill, and completing one **writes the bestiary entry**
  ([Fextralife](https://thewitcher3.wiki.fextralife.com/Contracts)). Note the scale: 26 is a
  **hand-authored** count, not a generated one.
- **Destiny 2** caps pursuits at **63 per character**, shared between quests and bounties, with
  anti-hoarding as the stated reason; players call the shared cap *"pretty dumb"*
  ([Dot Esports](https://dotesports.com/destiny/news/how-to-stack-bounties-in-destiny-2); forum
  player-reported).

### The failure modes — and both directions are documented

- ⛔ **Making the roster unreachable has a named cost.** Pokémon Sword/Shield's "Dexit" cut the dex to
  **400 of ~890 (~45%)** and barred non-dex species from transfer entirely, producing a sustained backlash;
  **200+** species were later added back via DLC
  ([Nintendo Life](https://www.nintendolife.com/news/2020/01/more_than_200_old_pokemon_are_being_added_to_sword_and_shields_national_dex)).
- ⛔ ⭐ **The opposite failure is the one a hunt verb can cause.** Monster Hunter Wilds: players report
  **tempered Arkveld is the only tempered 8★ worth fighting** — it alone drops meaningful R8 weapon
  materials, has the best R8 armour and gives the most XP, with best-in-slot reachable in ~**40 h** and
  *"no real reason to grind any of the other monsters"* (player-reported,
  [Steam](https://steamcommunity.com/app/2246340/discussions/0/501694185121861187/)).
  **The mechanism is reward-dominance, not the targeting UI: if one target's loot strictly dominates, a
  targeting verb accelerates monoculture.**
- ✅ **A developer adding a verb to re-surface content, and it worked.** FFXIV's **Duty Roulette** bundles
  old dungeons into random-pick categories with daily bonuses, keeping level-50/60/70/80/90 content queued
  years later ([Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/Duty_Roulette)). ⭐ **Note the
  inversion: the reward is attached to *accepting randomness* — the opposite of a bounty.** Counter-evidence:
  players petition to exclude content they have not seen (player-reported).
- ✅ **Targeting that multiplies rolls rather than replacing them.** Pokémon Legends Arceus' **Massive Mass
  Outbreaks** (~15 simultaneous, quest-unlocked) let a player pick a species to farm; outbreak members get
  **25 extra shiny rolls**, moving the base rate from **1/4096 to ~1/158** — a ~**26×** multiplier on the
  desired outcome **while keeping the underlying roll**
  ([RotomLabs](https://rotomlabs.net/guide/shiny-hunting-massive-mass-outbreaks-legends-arceus)).

---

## The shape

**Three moves, and the third is the one that needs a guard.** Every one of them contributes to a selector
that already exists; none forks a parallel path.

### 1. Restore the roll — the cheapest fix, and the one that restores rarity

`WaveCatalog` should draw from a **slot table with pinned weights**, seeded, instead of
`pool[i % pool.Count]`. This is Pokémon's model and the reason to copy it is not aesthetic: **with no roll,
the rarity ladder is inert at the encounter layer.** The same change widens `Band` past its four rungs —
its own comment already concedes the limit was a stale-roster artifact — and should add the missing
**acquisition filter**, so a `CaptureOnly` species cannot march in a wave.

The slot table is content: **which species appear where, at what weight**, expressed per wave/region, with
a long tail. Weights are tunable data, never a `const`.

### 2. Wire the selector that is already right

`Encounter`/`SlotFill` needs a production caller, not a design. It already has climate weighting, an
anti-repeat same-species cap, and a 419-anchor pool — and its refusal on a null `ThreatBand` is *correct*,
which makes **filling `threatBand` (tier-system DO #2) the true prerequisite**, not a parallel task. Fix
`SlotFilter.cs:49`'s missing plant offset in the same change.

### 3. Creatures populate the map — **owner direction, 2026-09-13, and it is one line**

**Owner:** *"the creature will spawn on the map, we will extend world stage later to hunt them."*

This is the decided shape, and it is **cheaper than the quest-board verb an earlier draft proposed** —
because the spawner already exists. `SpawnTheUnmade` (`LoamPhases.cs:271-282`) already places
`Wild`-owned warbands at real sectors, guards occupancy, and reports the event. **The change is to replace
the `"normalzombie"` literal at `:280` with a roll against a weighted table** — the same slot-table shape
as move 1, keyed on the sector rather than the wave.

**Why this is the better targeting mechanism, not merely the cheaper one:**
- **The map already answers "where."** A creature on a sector *is* a target with a location; fog, lanes,
  march and claim are the verbs a player already has for reaching one. A quest board would invent an
  abstraction next to a spatial system that already exists.
- **It is the Guiding Lands shape**, which is the genre's own answer to roster surfacing: a gated,
  stochastic pool of creatures distributed across regions.
- **Sector context is a natural weighting axis.** Climate already selects species elsewhere
  (`RaiseResolver.SpeciesFor(sector.Climate)`), so "which creatures live here" has an existing input —
  and `SlotFill` already implements climate weighting (on 1000 / off `offClimateMilli`).
- **It needs no new vocabulary.** No `SpeciesKind`, no quest schema change, no second targeting surface.

**Deferred by owner direction, with the trigger named:** the **hunt interaction** — what a player does when
they reach a wild creature — extends the **world stage** later. This doc stops at *creatures are there and
are varied*; it does not design the encounter. **Trigger: the world-stage extension.**

⛔ **The guard this move still requires, stated as a rule rather than a hope:** *no single species'
rewards may strictly dominate.* The Wilds/Arkveld case proves that once a hunt verb exists, monoculture
follows **reward-dominance, not the UI** — and a map full of creatures whose drops are strictly ordered
will be farmed at exactly one sector. This is the distribution discipline `roster-metrics` already exists
for, pointed at encounters instead of anchors — and it must land **with** the hunt interaction, not after it.

### Wild map admission — DECIDED 2026-09-13: per-flag, not a blanket filter

`CreatureAcquisition` is read **per flag**, because the flags mean different things on a map than in a wave:

| Flag | Wild map | Wave | Why |
|---|---|---|---|
| `Summonable` (871 species) | admit | admit | the ordinary roster |
| `CaptureOnly` (29) | **admit** | refuse | the wild **is** its acquisition route — encountering it is how you capture it. Refusing it on the map strands the species entirely |
| `EventOnly` (4) | **refuse** | refuse | events own their own gating; a wild spawn would bypass it |

This is deliberately **not** the same rule the wave band needs (`WaveCatalog.Band` has no acquisition
filter today at all, `:153-154`, which is a bug there and would be a bug here if copied blindly).

### ⭐ Introduced, not designed — a third creature category

**Owner, 2026-09-13:** *"we will introduce some neutral unit that never become a legion troop — like demon
from the hell (new empire that need serious huge program) and void beast (non empire, empty, that come from
the void). I just introduce them here, we mention them but nothing feature design for them yet."*

**Recorded so the vocabulary exists before something needs it, and explicitly NOT designed here:**

| Concept | What it is | Status |
|---|---|---|
| **Demon** | A creature of a **new empire** — a faction in its own right, hostile, with its own territory logic | **Named only.** Owner: *"needs a serious huge program."* No design, no spec, no content |
| **Void beast** | **Non-empire** — it belongs to no faction and holds no ground; it arrives *from the void* | **Named only.** No design, no spec, no content |

**The void raid — named 2026-09-13, explicitly not designed.** Owner: *"they need a new program for void
raid — the void will deploy void beasts to attack the sector in a siege mode. They have no empire. This
also mention only, no design yet."*

So the void is not a place the player goes; it is a thing that **arrives**, deploying beasts against a
sector the player holds. ⭐ **One observation worth recording, because it is the same lesson this whole
document turned on:** the machinery is largely built.

- **Siege exists as a program.** `docs/architecture/base-defense/` carries a dozen-plus specs
  (`spec-siege-ai`, `spec-siege-board`, `spec-siege-construction`, `spec-siege-cover`,
  `spec-siege-economy`, `spec-district-layout`, …), and `SiegeObjective.Evaluate`
  (`Battle/Siege/SiegeObjective.cs:36`) already resolves real outcomes — `CoreTaken`, `AssaultBroken`.
- ⭐ **The assault resolver keys on an attacking *entity*, not a faction.**
  `DistrictAssaultResolver` reads `request.AttackerEntityId` (`:59`) — so **an attacker with no empire is
  already expressible.** A void raid does not need a faction invented for it.
- **A neutral, territory-less attacker already has a home in the model:**
  `WorldFactionKind.Wild` plus a `WorldEntity` at a sector, exactly as the Unmade spawner already produces.

**Nothing here designs the void raid** — not its trigger, cadence, composition, escalation or rewards. It
is recorded so that (a) the wild-spawn table leaves room for a non-recruitable attacker, and (b) whoever
opens that program starts from "siege and non-faction attackers already exist" rather than from scratch.

⭐ **The one property both carry, and it is a real constraint on this doc:** they are **neutral units that
never become a legion troop.** That is a *third* category beside the existing two — today a creature is
either a **general creature** (engine-spawned, troop-stack shaped, recruitable into legions) or a **unique
creature** (a `UniqueActor` specimen). A permanently non-recruitable neutral is neither, and
`creature-system-map.md`'s Vocabulary section does not currently have a row for it.

**What this means for species selection, stated now so it is not discovered later:** the wild-map spawner
this doc proposes is the natural host for both — `WorldFactionKind.Wild` already models a neutral,
non-player faction, and `WorldEntityMember.InstanceId` is already *"null for non-player forces"*. So the
spawn-table shape should not assume every wild creature is recruitable. **Nothing else here depends on
them, and no part of this document designs them.**

### Alternatives rejected, with reasons

- **Hand-authored contracts per species.** Rejected on scale: Witcher 3 ships **26** hand-authored
  contracts; we have 904 species. A generated, weighted table is the only shape that reaches the roster.
- **A hunt verb that guarantees the target's rare drop.** Rejected — it replaces the table instead of
  multiplying rolls against it, and it is the fastest route to the Arkveld monoculture.
- **Guiding-Lands-style zero-sum region decay.** Rejected: the documented complaint is that progress feels
  self-cancelling, and we have no need to throttle a roster the player cannot currently reach at all. The
  *Lure* half of that system — a consumable deterministic override — is worth keeping.
- **Widening `Band` alone, without restoring the roll.** Rejected: ordinal-first-N over a wider pool still
  yields a fixed set, just a different fixed set. The roll is the fix; the width is the amplifier.
- **Making the lawn spawn arbitrary species via the injector.** Out of scope and against the grain —
  gameless-first means the hunt verb must work with Fusion closed, and the lawn is an enrichment surface.

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| Encounter slot count per wave/region, and the per-slot weight table (`weightPerMille`) | Move 1's table — Pokémon's 10–12 slots with a long tail is the shape to start from | `data/tuning/waves.v{n}.json` (extends the shipped file) |
| `offClimateMilli`, `sameSpeciesMaxMilli` | Already shipped in the Delve selector; they become live when move 2 wires it | existing dungeon tuning |
| Per-sector species weight table, and how climate shifts it | Move 3's lever — what replaces the `"normalzombie"` literal. Same slot-table shape as move 1, keyed on sector instead of wave | `data/tuning/loam*.json` beside `UnmadeSpawnAfterTurns`/`UnmadeMemberCount`, or a new world-spawn domain file |
| `UnmadeSpawnAfterTurns`, `UnmadeMemberCount`, `UnmadeMemberHp` | **Already shipped** in `LoamPolicy` — the spawn cadence and pack size the species roll plugs into | existing loam tuning |
| *(Deferred with the hunt interaction)* hunt-token rate, roll multiplier, holding cap | MH deletes oldest at 250; Destiny refuses at 63. Owed **only if** the world-stage hunt uses a token economy at all | owed at that extension |
| Roster-coverage target (what fraction of the roster a player should have met by stage) | The metric that would have caught this; a **reporting target**, never an acceptance assertion | `roster-metrics` domain |

**Structural (stays `const`, with a comment):** the seeded-RNG stream derivation and the
draw-without-replacement rule inside `SlotFill` — those are determinism guarantees, not balance.

---

## What this deliberately does not decide

- **The slot tables themselves** — which species appear where is content, owed to a generator and a
  balance pass.
- **Whether hunting is a quest, a bounty board, or a consumable lure** — three shipped shapes, all viable;
  the doc fixes only that it multiplies rolls and that its currency is earned.
- **Anything about the live lawn's own spawner** — the injector observes and contributes; changing what
  PvZ spawns is out of scope.
- **The `threatBand` fill** — that is `tier-system` DO #2 and a creature-seed ask; this doc only records
  that move 2 depends on it.
- **Any FE surface** — a hunt board or a bestiary belongs to the GUI programs and goes through `/idea-ui`.

---

## Open questions

Owner decisions only. Each is answerable.

**Decided 2026-09-13 (owner):** creatures populate the **world map** via the existing wild spawner, and the
**hunt interaction extends the world stage later**. The quest-board alternative is not taken.

Still open:

1. **Should the roll restoration ship before or with the `threatBand` fill?** Move 1 (waves) and move 3
   (map spawn) need no `threatBand`; move 2 (delve) refuses without it. **Recommendation: moves 1 and 3
   first** — both are independent, both are cheap, and they prove the slot-table shape before the Delve
   inherits it.
2. *(Answered — see § Wild map admission below.)*
3. **Does the map spawn table share the wave slot table, or is it its own?** Recommendation: **its own**,
   keyed on sector and climate — a sector's residents and a wave's roster answer different questions, and
   `SlotFill`'s climate weighting already exists for the former.
4. **What is the roster-coverage target?** Nothing measures it today. A number here turns "the player meets
   ten species" from an accident into a tracked property — and it should be a **report**, never a test
   assertion, since roster size is a reading.

---

## Reading gate (this session, per DESIGN-GATE §1)

**Product vision:** [the-game.md](../guide/the-game.md), [the-loops.md](../guide/the-loops.md) — Place
1/2/3/6 and Spine B named above; three stocks; no new loop. **Anything at all:**
[software-architecture.md](software-architecture.md), [decisions.md](decisions.md), §2 invariant 15 (SOLID
— every move contributes to an existing selector), session-boundary policy. **Creature vocabulary:**
[creature-system-map.md](creature-system-map.md) "Vocabulary" (general vs unique creature; the troop-stack
framing; progression source selected by spawn mechanism) — **the section this doc most depends on.**
**Creature generation:** [creature-seed-map.md](creature-seed-map.md) (18 modules; `threat-band` is module
4, `roster-metrics` module 14). **Power / caps / tunables:**
[power/ssot-power-scale.md](power/ssot-power-scale.md) §4.6 PS-3, §5.3 the `thetaOffset` rule, §10–§11;
[tunables-ssot.md](tunables-ssot.md) T1–T8. **Rarity:** [item/ssot-rarity.md](item/ssot-rarity.md) §3.2/§3.6
(the ladder whose meaning the missing roll destroys at this layer). **Validation:**
[validation-ssot.md](validation-ssot.md) (roster size is a reading, never an assertion). **Origin and
siblings:** [tier-system-ideal.md](tier-system-ideal.md) § AUDIT A1/A10,
[species-craft-ideal.md](species-craft-ideal.md), [gear-climb-ideal.md](gear-climb-ideal.md).
**Code verified at the `file:line` cites above**, not from comments — including two stale comments found
and named (`WaveCatalog.cs:121-126`'s four-rung claim, and the divergent `CreatureTypeId` formula at
`SlotFilter.cs:49`). Counts verified by counting; populations treated as readings. Prior art web-searched
this session; blocked domains (Bulbapedia, HTTP 403) and unverified figures flagged inline.

**Boundary honesty:** this session wrote no `tasks/sessions/*.json` record (no `/session-start` in this
harness), so **the DESIGN-GATE §5 boundary box cannot be ticked.** `session-boundary-check.ps1` was run
earlier: 4 active sessions, 13 drift overlaps **between other sessions**, none claiming
`docs/architecture/species-selection-*`.

**§5 checklist:** subsystems identified ✓ · boundary record ⛔ untickable · gate docs read this session ✓ ·
`decisions.md` checked ✓ · claims cite `file:line` ✓ · verified against code, not comments ✓ · surrounding
sections read ✓ · constraints measured rather than assumed ✓ (871/906 summonable, 10 species across four
waves on two derivations, 419/904 anchors with `threatBand`, 246/904 wave-eligible) · §2 invariants hold ✓ ·
no population count pinned ✓ · **ActorHub N/A and checked** — selecting which species appears produces no
actor combat/derived magnitude; the spawned creature's stats compose through the existing paths ·
no SOLID-violating parallel path ✓.
