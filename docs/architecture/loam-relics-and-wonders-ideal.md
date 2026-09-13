# Loam relics and wonders — the ideal

**Status:** idea phase, 2026-09-13, **enriched same day (fourth pass)**. This pass **replaces the
binary Unique-scope framing** (per-faction vs. server-wide, Open question #3 as originally asked) with
a **four-tier `WonderScope` ladder** the owner specified directly — `Sector → Empire → World →
Multiverse` — grounded this session against the real `rpg_worlds`/`rpg_world_factions` schema and
`docs/guide/the-game.md`'s own persistence promises, and it **names Multiverse scope as this doc's
biggest real gap**: no building-granted effect survives a world ending anywhere in this codebase today.
It also **resolves the relic-faucet staging question** (all named loops ship together, one caveat) and
records that this doc expects to become an `empire-development` sub-program once a sibling session
writes that map. See "The owner's fourth-pass framing, unpacked" below for the verbatim decisions.
Everything from the first through third passes not called out as corrected below is unchanged and
still holds — in particular this pass does **not** reopen relic-vocabulary shape, the basic-material
cost, or "ship Minor/Grand together," all closed third pass. Not a spec. No build authorized.
**Program id:** `loam-relics-wonders`. Extension/correction to the **sealed**
[loam-map.md](loam-map.md) capability map — not a greenfield feature. Read that doc's own §6d
("the G-F reward hole") and post-gate `loam-texture` wave before proposing anything downstream of
this doc. **Expected to be indexed as an `empire-development` sub-program** once
`docs/architecture/empire-development-map.md` exists (owner-named umbrella, a sibling session's to
write, alongside `scoped-inventory-hierarchy-ideal.md` and others) — this doc does not guess at that
map's contents, only names the expectation (§The owner's fourth-pass framing).

**Traces to:** the owner's rejection of Warden's `StabilityMilli` freeze
(`LoamPhases.cs:174-182`) as "a cheat — it doesn't help, it breaks the loam economy," recorded in
[warden-mortality-ideal.md](warden-mortality-ideal.md)'s withdrawal banner (2026-09-13). **Correction
(strengthen pass 2026-09-13, finding F15) — miscitation fixed.** This section previously said that
doc "names a file `loam-generation-rate-ideal.md` as the follow-up." Verified this session by reading
`warden-mortality-ideal.md` in full: it does not name that filename anywhere — the only place
`loam-generation-rate-ideal.md` appears is self-referentially, inside this doc's own header. The
correct statement is narrower: this doc **is itself** the follow-up to the owner's Warden rejection,
written under the path the owner actually specified for this session
(`loam-relics-and-wonders-ideal.md`) after the owner enriched the direction with the relic/Wonder
shape below. `loam-generation-rate-ideal.md` was never written and was never named by the sibling
doc — only referenced by this doc's own earlier draft, corrected here.

---

## Which loop this extends

**Added as its own heading (strengthen pass 2026-09-13, finding F12) — was folded into Step 0 prose;
content unchanged, only promoted so it survives a long session per the idea-phase skill's required
structure.**

Rise of Summoner is an RPG plus empire-building game (`the-game.md`). Loam, buildings and Wardens are
Place 3 — *"Farming, hunting, and defending the empire"* — and that page names *"buildings and
wardens"* as **WIP** explicitly (`the-loops.md:83-85`). This is not a new pitch; it finishes
reasoning about an already-named, partially-built piece of a shipped loop.

## Step 0 — principles, restated

- **Every RPG feature lives in the RPG layer; it is never built by changing what PvZ is.** Relics,
  Wonders, loam stock and structures are all `FusionRpg.Core.World` + `FusionRpg.Data` state. None
  of it touches a Unity field or asks PvZ to represent anything about the empire layer.
- **The PvZ write surface is irrelevant here.** This is a world-map concern; no lawn write is
  involved.
- **Two async systems, deltas not absolutes.** Turn resolution already applies this: `Production`
  and `Pressure` are pure phases over `(world, seed)` (`LoamPhases.cs`), and any new relic/Wonder
  phase must follow the same shape — no wall clock, no live read of a store mid-`Step`.
- **One power ladder.** A Wonder's yield bonus is a **rate multiplier/flat add on an existing
  loam-production term**, not a new magnitude curve — it reuses `StructureDef.YieldMultiplierMilli`
  / `FlatYieldPerTurn`, both already read by `LoamProduction.For` (`LoamProduction.cs:27-35,
  43-51`). No new `f(level)` is proposed.
- **The balance surface is data.** Every number this doc could introduce — relic costs, Wonder
  build turns, yield bonuses — belongs in `data/tuning/loam.v{n}.json` (or a sibling tuning file,
  §Tunables) beside the existing `structures` block, never a `const`.
- **No hard progression ceilings.** Relic stock, like `LoamStock`/`RubbleStock`/`IronworkStock`
  before it, gets an overflow-and-report treatment (`LoamPhases.Production`'s own
  `loam.overflow:` pattern), never a silent clamp. `AGENTS.md`'s "no hard progression ceilings" rule
  applies to any relic cap the same way it already applies to loam capacity.
- **Gameless-first.** Fully satisfied by construction — the world map, structures, and materials are
  all playable with Fusion closed today. Nothing here is lawn-gated.

DESIGN-GATE §1 rows read this session: Product vision (`the-game.md`, `the-loops.md`), Economy /
currencies / yields (`empire-economy-ssot.md` referenced via `loam-map.md`'s own citations — not
re-opened line-by-line this session, since `loam-map.md` is itself the consolidated, sealed
capability map for this exact subsystem and cites it throughout), Data/SQL (`data-architecture.md`
— not re-opened; no schema is proposed, only field-shape options), and the `docs/architecture/item/`
lane index for materials (`ssot-materials-crafting.md`, read this session, see below).

---

## What this is

**The player sentence:** *I find or earn relics out in the world. I bring them home, along with
ordinary building materials, and spend both to raise a Wonder in a sector — a building that does
something no ordinary structure can, starting with making that ground generate loam faster. A
Wonder is earned and built, never a free shield that stops the chaos decay from ever touching that
ground.*

This replaces what the Warden mechanic used to give for free (a sector's `StabilityMilli` frozen
outright while a bound specimen holds it, `LoamPhases.cs:181`) with the owner's stated alternative:
**raise the generation rate, never freeze the decay.** The owner named four possible levers in the
same breath — relic, special unit/trait, commander trait, building — and then scoped this doc to
one of them: **relics spent on Wonders**, a building-and-item pair. The other three (special-unit
trait, commander trait) are out of scope here; they are separate faucets into the *same* generation
mechanism this doc's Wonder already proves works (`YieldMultiplierMilli`/`FlatYieldPerTurn`), not
separate mechanisms of their own, and are not designed further in this doc.

**Relics** are a new world-map-scoped consumable/material item — found or earned, stockpiled, and
spent, never equipped, never a combat item. **Wonders** are a new special tier of `Structure` —
buildable only with relics plus ordinary materials, and (per the owner's framing) meaningfully more
powerful than an ordinary structure like a Well.

### The owner's second-pass framing, unpacked (2026-09-13)

The owner added, verbatim: *"wonder have 2 scope, empire and sector depend on it tier and rarity;
wonder play special role in the empire development system; it will boost some special resource and
enhance other game mechanism that we will add later like increase defense power/aura/buff for
defense unit in the sector, buff all empire; so we focus on loam now and reverse [read: **reserve**]
vocabulary for extend later; wonder feature is inspire of sid meier's strategy game and some other
games."*

Four separate claims, unpacked and each checked against code this session:

1. **Two effect scopes, gated by tier/rarity** — a Wonder can reach just its own sector or the whole
   empire, and which one depends on the Wonder's tier/rarity. Designed below (§The shape).
2. **"The empire development system"** — **checked against code this session and corrected.** No
   system by that name exists. The real, shipped, module-id'd system is
   [`sector-development`](world/spec-sector-development.md) (`RaiseResolver.cs`, `DevelopResolver.cs`,
   world-map W51/W52) — and it is **sector-scoped**, not empire-scoped: `DevelopResolver.Run` reads
   and writes one `WorldSector`'s own `LoamStock`/`ProjectId` (`DevelopResolver.cs:26-83`), and
   `DevelopmentLevel` is a per-sector field (`WorldState.cs:135`) read by `DevelopmentYield.For` one
   sector at a time (`LoamProduction.cs:59`). There is no code path today that raises or reads a
   development-like number at faction/empire scope. The closest **empire**-scoped precedent is a
   different, smaller thing: `WorldFaction.ScopeModifierMilli` (buff-debuff-scope T12,
   `WorldState.cs:85-93`) — *"a standing world-map buff/debuff on this faction, per-mille... hashed via
   `WorldCanonical.Write`... applied by whichever future consumer's own compute path reads it"* — which
   is a real, hashed, replay-safe faction-wide lever with **zero consumers today**
   (`docs/architecture/buff-debuff-scope/spec-world-map-scope.md:19-20`: *"there isn't one path to
   read"*). So: the owner's phrase maps onto two different real things — `sector-development` (built,
   sector-scoped, wrongly generalized to "empire" by the phrasing) and `WorldFaction.ScopeModifierMilli`
   (built as storage, empire-scoped, but an inert wiring gap with no consumer). Neither is an "empire
   development system" in the sense of a mirror of `sector-development` at faction scope — that system
   does not exist and would have to be built new if a Wonder's empire-scope effect needs a *level* or
   *project* concept rather than a flat modifier.
3. **Future effect types, named but out of scope for this build** — `defense.power`,
   an aura/buff for defending units in a garrisoned sector, and an empire-wide buff. The owner's own
   phrasing ("reserve vocabulary for extend later") is read literally: design a closed, reviewed-growth
   vocabulary now; ship only the loam member. Designed below.
4. **Genre prior art, Sid Meier specifically** — researched below; the closest match found is Civ5's
   own National-vs-World Wonder split, which is a tier→scope gate, not merely a flavor reference.

### The owner's third-pass framing, unpacked (2026-09-13)

**On "ship Grand or Minor first,"** the owner said, verbatim: **"Ship both together."** This closes
Open question #5 outright (§Open questions below): `Minor` (sector-scope) and `Grand` (empire-scope)
ship in the same wave. Consequence, stated plainly because it changes what "in scope" means for this
program's first build: the two real gaps the first pass found for `Grand` — a faction-level input to
`LoamProduction.For` (`LoamProduction.cs:18-62`, neither overload takes `WorldState`/`WorldFaction`
today) and a first reader for `WorldFaction.ScopeModifierMilli` (`WorldState.cs:85-93`, hashed and
replay-safe but zero consumers) — are **now confirmed in scope for this program's first build, not
deferred.** This doc still does not design that plumbing; it only removes the option of skipping it.

**On relic vocabulary shape,** the owner rejected both candidates the first pass offered (a fungible
stock, or a small named stock set) and gave a specific, different answer, verbatim: *"relic is unique
items, they will generate by seedsmith item generator and make drop tables to register them, we will
earn them in gameplay like delve, lawn run, expedition, world map assault, quest, etc. so this is a
lot of feature, we cannot design everything, just focus on what we already have first, this just
extend the item generator and drop tables generator, nothing new, only thing new is when legion carry
the relic to the sector, it need sector inventory to store, this is whole new feature because we only
have empire scope inventory that share all, this relic is special show we will enrich idea for new
sector scope inventory and legion scope inventory."*

Unpacked, checked against code this session, and **correcting** §The shape's original two-candidate
framing (which is now superseded, not merely narrowed):

1. **Relics are rolled/unique ITEMS, not a new stock/currency.** The owner's shape is a third option
   neither original candidate named: a relic is minted the same way any other rolled item is —
   through the existing item-generation pipeline and drop tables — never a `WorldFaction`/`WorldSector`
   `long` counter at all. §The shape's two "candidate stock shapes" are wrong at the type level, not
   just undecided between; see the corrected section below.
2. **"Nothing new" on the minting side is mostly right, verified this session, with one real
   exception named below.** The closed 15-kind item-seed vocabulary
   (`tools/seedsmith/seedsmith/adapters/items/kinds.py:56-68`, ported from
   `tools/ItemSeedValidator/Registries/KindCatalog.cs`) already has a `unique` kind — a rolled,
   rarity-gated, `AUTHORED`/`VALIDATED`/`PLANNED` item exactly shaped like "a special, found item."
   `DropEntryKind` — the closed 9-value enum a drop-table row's payload is typed as
   (`DropTableModel.cs:16-27`: `Equipment, Material, Currency, Insert, Charm, Consumable, Unique,
   Table, Nothing`) — already has a `Unique` member. And the loops the owner named are **already
   real, wired drop-table sources**, not something to invent: `DropTableValidator.KnownSourceKinds`
   (`DropTableValidator.cs:58-59`) already lists `world-sector`, `expedition-tier`, `dungeon-room`,
   `dungeon-clear`, `dungeon-quest`, and `siege-assault` — i.e. world-map assault, expedition, delve
   (the dungeon three), and quest all already have a real `SourceKind` + a `LootCorrelation.Derive`
   arm in `LootPipeline.cs`. `WorldSectorLootSource.cs` is the concrete proof for the world-map path:
   a `world-sector` drop table already resolves at runtime from a sector's own danger band — the
   exact shape a relic-yielding world-map table would reuse, not a new mechanism. The one loop the
   owner named that is **not** wired is "lawn run": the closest match, `pvz-run`, is
   `DropTableValidator.UndesignedSourceKind` (`DropTableValidator.cs:50`) — explicitly refused,
   pending an unrelated, pre-existing open question about what a PvZ match's own content level even
   is (§4.1's own note, unrelated to this program). Everything else the owner listed needs no new
   drop-table mechanism, only new authored rows pointing at the existing `unique` kind.
3. **The one real exception: today's `unique` kind is definitionally equip-shaped, and a relic per
   the owner's own description is not.** Reading `kinds.py:56-68` directly: `unique`'s `required`
   fields are `frame, baseType, rarity, fixedAtoms, counterPressure, tags, powerAxis` — `baseType`
   is a **hard reference** (`refs={"baseType"}`) to a `base-type` row, which is always role-slotted
   gear (weapon/armor via `ItemRole`, e.g. `humanoid-armament-primary-a`); `fixedAtoms`,
   `counterPressure` and `powerAxis` are combat-stat machinery. A relic, per the owner's own words,
   is **never equipped** and carries no combat role. So a relic does not drop cleanly into today's
   `unique` `KindSpec` as authored — this is a small, real, reviewable schema question (does `unique`
   grow a no-`baseType` variant, or does a relic get its own sibling kind/`DropEntryKind` member
   alongside `Unique`?), not a "nothing new" claim in full. It is still a narrow extension of the
   existing generator/registry, exactly the shape the owner asked to scope this to — never a new
   generator program, a new drop-table format, or a new loop.
4. **The genuinely new thing, and the one the owner named as such themselves: sector-scoped
   inventory.** `RpgStore.Items.cs` (`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:85-107`) confirms
   this fresh: `rpg_item`'s primary key is `instance_id` with a `player_id` column and an index on
   `player_id`; `rpg_item_stock`'s primary key is `(player_id, container_id)`. Neither table has a
   sector, world, or legion column anywhere — ownership is **player-scoped only**, and a player-scoped
   row is reachable from every sector and every legion the player controls, i.e. **shared everywhere**,
   confirming the owner's framing exactly. There is no code path today that lets an item "sit inside
   sector S until a legion carries it out." **This is a real, hard dependency, not a nice-to-have**:
   Wonder-building via legion-carried relics cannot resolve end-to-end until a sector-scoped (and
   legion-scoped, for the carrying leg) inventory concept exists. The owner is separately commissioning
   that investigation now, in parallel, as its own doc:
   **`docs/architecture/scoped-inventory-hierarchy-ideal.md`** (a sibling agent's, not this doc's, to
   write). This doc does not design that concept — it only names the dependency and the filename so
   a future reader does not re-derive it. See §Open questions and the real-gap table below.

**On the "basic material" partner cost,** the owner said, verbatim: *"use other building material that
already introduced on siege building, the siege building and sector building is the same, just
different on combat building or outside combat building. different clock level for siege clock and
world stage clock but they are same."* **Verified this session, and the owner is right — confirmed,
not merely plausible.** `StructureDef` (`StructureCatalog.cs:42-194`) is **one record** carrying both
cost shapes at once: `Cost` (peacetime, spent from a legion's `CarriedLoam`, read by the world-turn
build flow) **and** `ConstructRubbleCost`/`ConstructIronworkCost` (siege-time, spent from
`WorldSector.RubbleStock`/`IronworkStock`) on the very same row. `ConstructionCost.CanAffordBuilt`/
`SpendBuilt` (`ConstructionActions.cs:295-320`, in `FusionRpg.Core.Battle.Siege`) reads
`def.ConstructRubbleCost`/`def.ConstructIronworkCost` off that identical `StructureDef` type directly
— there is no second, siege-only structure type. `StructurePolicy` itself (repair cost, tier-to-HP,
capacity growth) lives in namespace `FusionRpg.Core.World` — the same namespace as `StructureCatalog`
and `WorldSector` — not in a separate siege namespace, even though `ConstructionActions`/
`ConstructionCost`/`SiegeDepot`/`BoardEconomy` (the round-resolving callers) live in
`FusionRpg.Core.Battle.Siege`. And the shipped seed data proves the two contexts already share one
row today: `well.json` (a pure world-map, turn-based structure, `buildTurns: 2`,
`constructRubbleCost: 0`) still authors `"acquisitionPaths": ["built"]` — the identical `Built` tag
`AcquisitionPath` names for siege construction (`Obstacles.cs:48-61`). **Conclusion: siege
construction and world-map sector construction are the same `StructureDef`/`StructureCatalog`
machinery, gated by which cost field is nonzero and which clock reads it** — peacetime `BuildTurns`
decremented turn-by-turn in `LoamPhases.Production` (`WorldState`-scoped), siege-time resolved
round-by-round through `BoardEconomy`/`SiegeDepot`/`ConstructionActivation` (board-scoped). This
**resolves Open question #4** (§Open questions below): a Wonder's "basic material" partner cost is
`RubbleStock`/`IronworkStock`, spent via the very fields (`ConstructRubbleCost`/`ConstructIronworkCost`)
already on `StructureDef` — **no new material kind, no new field, no new engine plumbing** — only a
new authored row with those two fields populated, the same as any siege structure today.

### The owner's fourth-pass framing, unpacked (2026-09-13)

**On Wonder scope/uniqueness, the owner rejected the binary Open question #3 asked.** Asked to choose
between per-faction and server-wide uniqueness, the owner said, verbatim: *"multiple scope of wonder,
sector only, empire only, world only, multi-verse only."* This is a **four-tier scope ladder**, not a
binary, and it **supersedes** both Open question #3's framing and §The shape's two-value `WonderTier`
enum (`Minor`→sector, `Grand`→empire) — struck through below, not deleted, per this doc's own
convention (§The shape).

Each tier is grounded against `docs/guide/the-game.md` and the real schema this session, not guessed:

1. **Sector** — one `WorldSector` row within one `WorldState`. Unchanged from the second pass; already
   designed (§The shape, the old `Minor` tier).
2. **Empire** — one `WorldFaction`'s holdings within one `WorldState`. **Confirmed this session**:
   `rpg_world_factions`'s primary key is `(world_id, faction_id)` (`RpgStore.World.cs:37-44`) — one
   `world_id` already carries N faction rows side by side (`WorldFactionKind`: `Player`, `Zomboss`,
   `Clan`, `FactionKindCatalog.cs:7-13`), confirming the second pass's reading that "empire" means one
   faction's slice of a shared world, never the whole world. An Empire-scope Wonder dies with that
   world: every table a faction's holdings live in (`rpg_world_sectors`, `rpg_world_entities`, etc.,
   `RpgStore.World.cs:45-105`) is keyed by `world_id` with no cross-world reference, matching
   `the-game.md:40`'s *"Loam and the holdings of a failed map do not [bank]."* Unchanged from the
   second pass; already designed (the old `Grand` tier).
3. **World** — one `rpg_worlds` row. **Correction (strengthen pass 2026-09-13, finding F20) —
   imprecise citation fixed.** This row previously cited `RpgStore.World.cs:20-35` alone for "`kind`
   defaulting to `'map'`" — verified this session, lines 20-35 are only the `CREATE TABLE rpg_worlds`
   statement (PK `world_id`, `player_id` column; no `kind` column appears in it at all). `kind` and
   `parent_world_id` are added later via `EnsureColumn` (`RpgStore.World.cs:201-202`:
   `EnsureColumn(db, "rpg_worlds", "kind", "TEXT NOT NULL DEFAULT 'map'")`,
   `EnsureColumn(db, "rpg_worlds", "parent_world_id", "TEXT")`) — the correct citation for both fields
   is `RpgStore.World.cs:201-202`, alongside `:20-35` for the base schema (`world_id`/`player_id`).
   A World-scope Wonder's effect would have to reach **every faction under that `world_id` at
   once** — broader than Empire (one faction's row) but still narrower than a new persistence
   category: the effect still lives entirely inside `world_id`-scoped tables and still dies when that
   world ends, exactly like Empire. The mechanical difference from Empire is only *how many
   `WorldFaction` rows the effect writes or reads* — "every Empire in this World simultaneously," not
   a fifth kind of storage.
4. **Multiverse — the real gap, stated plainly, per the owner's own instruction to name it as such.**
   This is the first Wonder scope that would have to survive a `world_id` ending, and **nothing a
   building grants does that anywhere in this codebase today.** Checked this session against the three
   things `the-game.md:40` promises survive a lost world: `rpg_soul_balances` (PK `player_id` alone,
   `RpgStore.Souls.cs:123-125`), the creature roster table `rpg_unique_actors` (PK `instance_id`, a
   `player_id` column, **no `world_id` column anywhere in its schema**, `RpgStore.cs:503-515`), and
   essence-class materials in `rpg_creature_materials` (PK `(player_id, material_id)`,
   `RpgStore.Materials.cs:191-193`, likewise no `world_id` column). All three confirm the pattern the
   game's own text promises — a player-scoped row with no world key, reachable after any `world_id` is
   abandoned. **But none of them is a building-granted effect, and no fourth table like them exists for
   one.** A Multiverse-scope Wonder would be the **first-ever world-surviving building-granted effect**
   in this game — a genuinely new persistence category, not a bigger number on the existing ladder.

   **Minimal shape named, not designed** (per the owner's own repeated "we cannot design everything"):
   a player-scoped ledger — the same shape as `rpg_soul_balances`/`rpg_creature_materials`, keyed by
   `player_id` (plus an effect-kind column, since a player could plausibly bank more than one such
   effect over time), **never** a `WorldState`/`rpg_world_*`-scoped row — written once when a
   Multiverse-tier Wonder is built, and read by whatever future step composes a **new** world's
   starting state. **Correction (strengthen pass 2026-09-13, finding F4) — seam citation fixed.** This
   previously named `RpgStore.World.cs:238` (`CreateWorld`'s `INSERT INTO rpg_worlds`) as the seam.
   Verified this session: `RpgStore.CreateWorld` only *persists* an already-composed `WorldState` — it
   computes no starting content itself. The real composition happens in
   `WorldTemplateCatalog.Build(templateId, seed, worldId)`, called **before** `CreateWorld` by whatever
   caller assembles a new world (e.g. `WorldEndpoints.cs:395-396`: `var built =
   WorldTemplateCatalog.Build(...); var (ok, reason, _) = store.CreateWorld(playerId, built);`). The
   correct seam is `WorldTemplateCatalog.Build`, not the `INSERT`. **Further real-gap note, also found
   this session:** even the corrected seam has no production caller today — the one call site,
   `WorldEndpoints.cs:382` (`MapWorldTest`), is explicitly commented *"SIM-only world creation — mapped
   inside the `/api/test` group"* (`WorldEndpoints.cs:379`). There is no shipped, player-facing
   "start a new world" endpoint anywhere in the codebase right now — a bigger prerequisite gap than
   this doc's first draft stated, since a Multiverse-scope reader would need a real production seam to
   attach to, and none exists yet. That names the *category* of change (a new player-scoped table plus a
   new reader at world-creation time) and *where* it would live (`FusionRpg.Data` for the row,
   `FusionRpg.Core.World` for whatever consumes it) — not a schema, not a `WonderEffectKind` member,
   and not a decision about which effects would qualify for it.

**Consequence for the rest of this doc.** `WonderTier` (`Minor`/`Grand`) is superseded by a four-value
`WonderScope` (`Sector`/`Empire`/`World`/`Multiverse`); `WonderEffectScope`'s two-value enum grows the
same two new members. Only `Sector` and `Empire` are buildable with zero new plumbing beyond what the
third pass already scoped in (owner: "ship both together," Open question #5); `World` needs the same
`LoamProduction.For` faction-input plumbing as `Empire`, applied across every faction in the world
rather than one, not new plumbing of its own; `Multiverse` needs the new player-scoped ledger above and
is **not** in scope for this program's first build — see Open questions.

**Two smaller corrections folded in this pass, unrelated to the scope question above:**

- **Relic faucets — RESOLVED, all named loops at once.** Open question #1 asked which single loop
  mints the first relic. The owner's answer this pass: relics get drop-table entries in **delve, lawn,
  expedition, world-map assault, and quest simultaneously** — not staged one loop at a time. **One
  caveat this doc already found and does not silently drop:** four of those five loops already have a
  real, wired `SourceKind` (`world-sector`, `expedition-tier`, the `dungeon-*` trio, `siege-assault`,
  §Built table) and can carry a relic row today; the fifth, **lawn**, maps to `pvz-run`, which
  `DropTableValidator` still explicitly refuses (`UndesignedSourceKind`, `DropTableValidator.cs:50`)
  for a pre-existing, unrelated reason (§4.1's own note: PvZ-run's own content level was never
  implemented anywhere). "Simultaneously" means the four wired loops ship together now; the lawn loop
  joins once that pre-existing, unrelated gap closes — this pass does not reopen or resolve `pvz-run`.
  See Open question #1.
- **This doc expects to become an `empire-development` sub-program.** The owner named a new umbrella,
  verbatim: *"this program should named empire-development and should have multiple sub program, that
  we can easier to manage and extend later."* A separate session owns writing
  `docs/architecture/empire-development-map.md`, indexing this doc alongside
  `scoped-inventory-hierarchy-ideal.md` and others as sibling sub-programs — not written here, and its
  contents are not guessed at in this doc. Once it exists, this doc's own program id
  (`loam-relics-wonders`) is expected to be listed there as one `empire-development` sub-program among
  several.

---

## What already exists

### Built

| Finding | Evidence |
|---|---|
| Well: a Rootbed's own seep multiplied 2× via `YieldMultiplierMilli` | `LoamProduction.cs:27-35` (the multiplier read); seed `data/seed/structures/extract/well.json:54` (`"yieldMultiplierMilli": 2000`) |
| Soul Conduit / Extractor: a flat per-turn loam add, independent of slot kind | `LoamProduction.cs:43-51` (the `FlatYieldPerTurn` loop, reads *every* active structure regardless of `SlotKind`); seeds `data/seed/structures/bank/soul-conduit.json`, `data/seed/structures/extract/extractor.json` |
| Sector development level: a flat per-level loam add | `LoamProduction.cs:53-59` calling `Growth.DevelopmentYield.For`; `DevelopmentYield.cs:21-32` (`checked` `long` multiply, no cap by design — comment: *"a sector's own `DevelopmentLevel` has no hard cap... so this has none"*) |
| Granary raises **capacity**, never generation | `LoamPhases.EffectiveCapacity`, `LoamPhases.cs:66-79` (only `Kind == StructureKind.Storage` rows contribute `CapacityBonus`) |
| **The generation-rate mechanism itself needs no new code.** `StructureDef.YieldMultiplierMilli` and `.FlatYieldPerTurn` already exist, are already read by `LoamProduction.For`, and either field can express a bonus of any size the tuning file authors — a Wonder with `yieldMultiplierMilli: 5000` (5×) or a large `flatYieldPerTurn` is mechanically identical to shipping a fifth Well/Extractor row today | `StructureCatalog.cs:58, 77`; `LoamProduction.cs:27-35, 43-51` |
| `StructureCatalog` loads from a real seed corpus, **not** a hand-written C# literal | `StructureCatalog.cs:213-220` ("Task 25.4: the C# literal fallback is gone... The real server wires this in `Program.cs` from the committed corpus on disk (`data/seed/structures/`)") |
| A **working seedsmith adapter for structures already exists and is LLM-capable**, built for base-defense's `structure-pipeline` (module 28) | `tools/seedsmith/seedsmith/adapters/structures/` — `generate_anchor.py` (reuses the creature-anchor SDK's `permute`/`vote`/`llm_caller` pieces verbatim, its own docstring: *"the first model call in the entire base-defense program"*), `generate_corpus.py`, `planner.py`, `metrics.py`, `anchor/schema.py`, `anchor/audit.py`, `anchor/descriptions.py` |
| Every one of the 25 shipped structure rows is `AUTHORED`, not model-generated | `grep '"source"' data/seed/structures/**/*.json` → **25/25 `"AUTHORED"`**, 0 `"MODEL"` or similar; e.g. `data/seed/structures/extract/well.json:9` `"source": "AUTHORED"` |
| A 27-id closed cost vocabulary for **gear crafting/salvage** already exists (Souls + 26 material ids across Shard/Substrate/Essence/Catalyst) | `MaterialCatalog.cs:11-28` (`MaterialClass` enum); `docs/architecture/item/ssot-materials-crafting.md` §3.1 (the five-spend table), §3.2 |
| That vocabulary is explicitly closed and scoped to **equip-operation spend**, not world-map resources — extending it is "ask-first by that enum's own doc comment" | `tools/seedsmith/seedsmith/adapters/items/materialgen/__init__.py:1-10` |
| A seedsmith adapter for that vocabulary (`materialgen`) already exists, but is explicitly **display/flavor only** for the 27 ids that already exist — it "never authors a new material id" | `materialgen/__init__.py:1-19` |
| `RelicCatalog` (battle) is a small, hand-seeded, **combat-only** relic list — weapon/armor/trinket equip slots, `fx.*` effect ids, zero reference to `World`/`Loam` | `src/FusionRpg.Core/Match/RelicCatalog.cs:1-75` (read in full — four rows, all equip-slot relics) |
| `WorldSector` already carries more than one world-scoped `long` stock field beyond `LoamStock` — an established pattern for "a new resource is a new stock field, produced and drawn like loam" | `WorldState.cs:173, 181, 187` (`LoamStock`, `RubbleStock`, `IronworkStock` — the latter two added for base-defense `siege-construction`) |
| No hard cap on `DevelopmentLevel`/`RubbleStock`/`IronworkStock`-shaped fields — the established precedent for a new resource is overflow-and-report, not clamp | `LoamPhases.Production`'s own `loam.overflow:` report line, `LoamPhases.cs:43-44` |
| **A real empire/faction-scope lever already exists and already hashes** — `WorldFaction.ScopeModifierMilli`, per-mille, 1000 = no modifier, following `UpkeepHandicapMilli`'s exact precedent | `WorldState.cs:85-93`; recommended storage shape confirmed to move **zero goldens** when emitted as its own row (`spec-world-map-scope.md:66-72`, "confirmed live, not assumed") |
| **A closed kind+payload vocabulary that grows by reviewed registration, not an open string, already ships at scale (24 ids)** — the exact shape this doc's effect-kind vocabulary should mirror | `StatusCatalogBootstrap.cs:1-69` — `StatusKind` (closed enum) crossed with `StatusPayloadKind` (closed payload-shape enum), each status id a `Register(...)` call reviewed at the point it's added |
| A closed, orthogonal **rarity** vocabulary is already this repo's convention for "how scarce", kept separate from other closed axes | `CLAUDE.md`'s own closed-vocabulary table: `DemonRarity` (10 values) as a distinct axis from trait/threat-band enums |
| **Added third pass.** The closed 15-kind item-seed vocabulary already has a `unique` kind — a rolled, rarity-gated found-item shape, the nearest existing fit for "relic" | `tools/seedsmith/seedsmith/adapters/items/kinds.py:56-68`, ported from `tools/ItemSeedValidator/Registries/KindCatalog.cs` |
| **Added third pass.** The drop-table payload enum already has a `Unique` member, alongside `Equipment`/`Material`/`Currency`/`Insert`/`Charm`/`Consumable`/`Table`/`Nothing` — 9 values, closed | `src/FusionRpg.Core/Items/Drops/DropTableModel.cs:16-27` (`DropEntryKind`) |
| **Added third pass.** World-map sector loot, expedition loot, delve (dungeon room/clear/quest) loot, and siege loot are **already real, wired drop-table `SourceKind`s** with their own `LootCorrelation.Derive` arms — none of the loops the owner named for relics need a new source mechanism | `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs:58-59` (`KnownSourceKinds`: `world-sector`, `expedition-tier`, `dungeon-room`, `dungeon-clear`, `dungeon-quest`, `siege-assault`); `WorldSectorLootSource.cs` (a `world-sector` table already resolves at runtime off a sector's own danger band — the exact shape a relic table reuses) |
| **Added third pass.** The one loop the owner named that is *not* wired: a lawn/PvZ-run drop source is explicitly refused today, for a reason unrelated to this program | `DropTableValidator.cs:50` (`UndesignedSourceKind = "pvz-run"`), §4.1's own note: PvZ-run content level "was never implemented anywhere" |
| **Added third pass.** `rpg_item`/`rpg_item_stock` are confirmed **player-scoped only** — no sector, world, or legion column on either table, so an owned item or a stock count is reachable everywhere the player is, never confined to one sector | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:85-107` — `rpg_item` PK `instance_id` + `player_id` column/index; `rpg_item_stock` PK `(player_id, container_id)` |
| **Added third pass.** Siege-time and peacetime structure construction share **one** `StructureDef`/`StructureCatalog` type — confirmed, not assumed. `well.json` (a pure peacetime row) authors the same `"acquisitionPaths": ["built"]` tag siege construction uses, with its siege-only cost fields simply zeroed | `StructureCatalog.cs:42-194` (`StructureDef` carries `Cost` **and** `ConstructRubbleCost`/`ConstructIronworkCost` on one record); `ConstructionActions.cs:295-320` (`ConstructionCost.CanAffordBuilt`/`SpendBuilt` read those same fields off that same type, from `FusionRpg.Core.Battle.Siege`); `data/seed/structures/extract/well.json` (`acquisitionPaths: ["built"]`, `constructRubbleCost: 0`); `Obstacles.cs:48-61` (`AcquisitionPath.Built` — siege's identical tag) |
| **Added fourth pass.** One `world_id` already carries N `WorldFaction` rows side by side — the schema precedent that "Empire" (one faction's holdings) and "World" (every faction under one `world_id`) are two different, real scopes today, not a hypothetical | `RpgStore.World.cs:37-44` (`rpg_world_factions` PK `(world_id, faction_id)`); `FactionKindCatalog.cs:7-13` (`WorldFactionKind`: `Player`, `Zomboss`, `Clan`) |
| **Added fourth pass.** A `rpg_worlds` row is the actual "one played-through map" unit — PK `world_id`, a `player_id` column, `kind` defaulting to `'map'`, and a later `parent_world_id` column for nested instances | `RpgStore.World.cs:20-35` (`CREATE TABLE rpg_worlds`), `:201-202` (`EnsureColumn ... "kind"`, `"parent_world_id"`) |
| **Added fourth pass.** Three real, player-scoped-only tables (no `world_id` column on any of them) already prove `the-game.md:40`'s "roster, souls, and essence bank across worlds" claim in code — the trio a Multiverse-scope Wonder effect would have to sit beside, not inside `WorldState` | `RpgStore.Souls.cs:123-125` (`rpg_soul_balances`, PK `player_id`); `RpgStore.cs:503-515` (`rpg_unique_actors`, PK `instance_id` + `player_id` column, no `world_id`); `RpgStore.Materials.cs:191-193` (`rpg_creature_materials`, PK `(player_id, material_id)`, no `world_id`) |

### Wiring gap

| Gap | The inert/underused line |
|---|---|
| The structures seedsmith adapter (`tools/seedsmith/seedsmith/adapters/structures/`) is fully built and already used for base-defense's siege content, but **has never been pointed at a Wonder-shaped row** — this is a content/authoring gap, not a missing mechanism. The plumbing (`generate_anchor.py` → `generate_corpus.py` → `StructureCorpus` → `StructureCatalog`) already accepts any `StructureKind`/magnitude combination a new anchor plan authors | `generate_corpus.py`, `StructureCatalog.ToStructureDef` (`StructureCatalog.cs:256-283`) reads whatever the corpus emits — no code change needed to add a new structure row with a large `yieldMultiplierMilli`, only a new anchor + regeneration |
| **`WorldFaction.ScopeModifierMilli` is declared, hashed, and replay-safe — and has zero consumers.** This is the exact storage a Wonder's empire-scope effect would write to, and it needs no new hashing work, only a first reader | `WorldState.cs:85-93`'s own doc comment: *"applied by whichever future consumer's own compute path reads it"*; `spec-world-map-scope.md:19-20`: *"there isn't one path to read"* |

### Real gap

| Gap | What would have to be built |
|---|---|
| **CORRECTED third pass — superseded, not merely narrowed.** The first pass framed this as "no relic item/material kind exists anywhere," implying a new closed vocabulary + store surface was needed for relics themselves. Verified this session: relics are rolled **items**, and the item-kind/drop-table machinery they need (`unique` kind, `DropEntryKind.Unique`, the `world-sector`/`expedition-tier`/`dungeon-*`/`siege-assault` source kinds) already exists (see Built table above). **What is actually missing, narrowly:** today's `unique` `KindSpec` hard-requires a `baseType` (an equip-role gear reference) plus combat-facing fields (`fixedAtoms`, `counterPressure`, `powerAxis`) — a relic, per the owner's own description, is never equipped and has no combat role, so it does not fit that `KindSpec` as authored. **Cross-reference (strengthen pass 2026-09-13, finding F22):** `deployment-hierarchy-map.md`'s External dependencies table separately asks the item program for a new repair verb + `op_kind` (`deployment-hierarchy-map.md:89`) — an independent, uncoordinated ask landing on the same item-program registry surface in the same window. Not merged; named here so the item program can review both asks together if it chooses | Either relax `unique` to a no-`baseType` variant, or register a small sibling kind/`DropEntryKind` member for a non-equip rolled item — a reviewed, narrow schema addition to an existing closed vocabulary, not a new vocabulary or a new generator |
| **RESOLVED third pass — no longer a gap.** The first pass framed the Wonder cost as possibly needing a brand-new "basic material" field on `StructureDef`. Verified this session: `ConstructRubbleCost`/`ConstructIronworkCost` already exist on `StructureDef` for exactly this purpose (siege construction's own bulk-material spend, `StructureCatalog.cs:80-90`), and siege/peacetime construction are confirmed to be the same catalog machinery (see Built table). A Wonder reuses those two fields directly | Nothing to build — only a new authored row (plus a relic-cost field, per the row above, once the item-kind question is settled) |
| **`StructureKind` is closed at five values** (`LoamSource`, `Storage`, `Yield`, `Refinery`, `Obstacle`, `StructureCatalog.cs:10-39`) with no "Wonder"/unique tier, and **no field anywhere marks a structure as build-once-per-sector or build-once-per-world** | Either a sixth `StructureKind` value, or a boolean flag orthogonal to `Kind` (the closer fit — a Wonder is mechanically still `Yield`-shaped), plus a validation rule if uniqueness is wanted (§Open questions) |
| **No faucet mints a relic yet — a content/authoring gap, not a mechanism gap (narrowed third pass).** The mechanism to mint and register a relic-shaped drop already exists (see Built table); what is missing is the actual authored `unique`/relic rows and drop-table entries pointing expedition/delve/quest/world-map-assault tables at them — this is the same "G-F reward hole" `loam-map.md` §6d already named and left open (*"nothing the player earns on the map reaches anything they care about"*) | Author the relic row(s) (once the item-kind question above is settled) and wire drop-table entries into the already-real source kinds; no new engine plumbing |
| **Empire-scope loam generation specifically has no plumbing, even though empire-scope *storage* does.** `LoamProduction.For` takes a single `WorldSector` and nothing else (`LoamProduction.cs:18`, `:70`) — there is no faction-level input in its signature at all, so even a wired-up `WorldFaction.ScopeModifierMilli` reader could not reach loam production without changing that signature and its call site. This is **narrower than the wiring gap above**: the empire-scope lever's *storage* is proven and free; *consuming* it specifically for loam yield is unbuilt plumbing, not an inert path. **Third pass: the owner's "ship both together" decision now confirms this plumbing is in scope for the program's first build, not deferred** | `LoamProduction.cs:18-62` (both overloads — neither takes `WorldState`/`WorldFaction`); caller `LoamPhases.cs:30` passes only the sector |
| **Added third pass — the load-bearing new gap.** No sector-scoped (or legion-scoped, for the carrying leg) item/inventory concept exists anywhere. `rpg_item`/`rpg_item_stock` are player-scoped only (Built table above) — there is no code path for "this relic instance sits inside sector S's own store until a legion moves it." **Wonder-building via legion-carried relics cannot resolve end-to-end without this.** Owned by a sibling investigation, not this doc: `docs/architecture/scoped-inventory-hierarchy-ideal.md` | A new inventory scope (sector, and legion-in-transit) — out of scope for this doc; see §Open questions and the cross-reference above |
| **Added fourth pass — the biggest real gap in this doc.** No building-granted effect survives a `world_id` ending anywhere in this codebase today. The three tables confirmed to survive a lost world (`rpg_soul_balances`, `rpg_unique_actors`, `rpg_creature_materials` — Built table above) are all player-earned currencies/roster state, never something a structure grants. A `Multiverse`-scope Wonder would be the first of its kind. **Corrected (strengthen pass 2026-09-13, F4):** the seam is `WorldTemplateCatalog.Build`, not `CreateWorld`'s `INSERT`, and even that seam has no production caller today (`WorldEndpoints.cs:382`'s one call site is `/api/test`-only) | A new player-scoped ledger table (PK `player_id` + effect kind, no `world_id` column, the same shape as the three tables above) written once when a Multiverse-tier Wonder is built, plus a first reader wherever `WorldTemplateCatalog.Build` composes a **new** world's starting state (not `RpgStore.World.cs:238`'s `INSERT INTO rpg_worlds`, which only persists an already-built `WorldState` — corrected F4) — named as a category and a seam only, not designed, and now flagged that even this real seam is `/api/test`-only, with no shipped production caller; see §The owner's fourth-pass framing and §Open questions |
| **Added (strengthen pass 2026-09-13, finding F3) — no combination rule for two+ Empire-scope Wonders writing the same faction scalar.** `WonderEffectDef` writes an Empire/World-scope loam effect into `WorldFaction.ScopeModifierMilli` — a single mutable `int` scalar (`WorldState.cs:85-93`) whose own doc comment anticipates "whichever future consumer['s]" compute path reads it, singular. Since `Common` rarity is explicitly uncapped (§The shape's rarity paragraph, corrected F9), a faction could build 2+ `Empire`-scope Wonders simultaneously, and no combination rule (sum/max/replace) is named for writing multiple `WonderEffectDef`s into one scalar field | **Recommend SUM** — matches how other additive loam terms already compose (e.g. `LoamProduction.For`'s `FlatYieldPerTurn` loop already sums every active structure's contribution, `LoamProduction.cs:43-51`); flagged as needing review before it is locked, since `ScopeModifierMilli`'s own doc comment was written assuming one consumer, not a summed multi-writer field — see §Open questions |

---

## Prior art

| Source | What transfers | Failure mode to avoid |
|---|---|---|
| **Civilization VI — Wonders** ([Wonder (Civ6) wiki](https://civilization.fandom.com/wiki/Wonder_(Civ6)), [Production (Civ6) wiki](https://civilization.fandom.com/wiki/Production_(Civ6))) | A Wonder costs a large, era-scaled one-time production investment, is built on a dedicated tile (not inside a regular city queue), and is capped at **one instance per game** — the exact "unique tier of building, gated by a scarce input" shape this doc proposes. Civ6 also **refunds unused production** when a wonder is lost to a rival, rather than deleting the investment outright. | **Wonder-race design failure**, confirmed by multiple independent sources ([CivFanatics: "Losing a Wonder Race"](https://forums.civfanatics.com/threads/losing-a-wonder-race.626174/), [Humankind forum discussion](https://community.amplitude-studios.com/amplitude-studios/humankind/forums/168-humankind/threads/35627-wonders-winner-takes-all-construction-style)): a winner-takes-all race where a losing player's investment simply evaporates trains players to **skip Wonders entirely** rather than risk the loss — *"without compensation for failed wonder attempts, it becomes almost always right to skip wonders."* If this program ever lets Zomboss compete for the *same* Wonder slot the player wants, it must not delete spent relics on a loss with zero compensation — Civ6's own refund is the mitigation, or (simpler here) make Wonders per-faction rather than server-wide-exclusive, which sidesteps the race shape entirely. |
| **Stellaris — Megastructures** ([Megastructures wiki](https://stellaris.paradoxwikis.com/Megastructures), [TheGamer build guide](https://www.thegamer.com/stellaris-megastructures-guide-build-how-all-every-dlc/)) | Costs a **combination of a bulk resource and a small quantity of a rare/scarce resource** (example cited: the Hyper Relay costs 25 Influence, 100 Rare Crystals, and 500 Alloys) — the exact "relic (scarce) + basic material (bulk)" pairing this doc's Wonder cost proposes. Multi-stage megastructures also demonstrate that a Wonder-tier building can be built incrementally rather than all-at-once, which maps onto this codebase's existing `BuildTurns` countdown field. | Not identified as a distinct failure mode in the sources found this session — flagged as unverified beyond the cited pages, not fabricated. |
| **Civilization V — National Wonders vs. World Wonders** ([StrategyWiki](https://strategywiki.org/wiki/Sid_Meier's_Civilization_V/Wonders), [CivFanatics: National Wonders](https://civfanatics.com/civ5/info/national-wonders/), [CivFanatics forum: "Wonders are the biggest trap in the game"](https://steamcommunity.com/app/8930/discussions/0/598513524566670325/)) | **The single closest match to the owner's "2 scope, gated by tier" framing found this session.** Civ5 splits every Wonder into exactly two scope tiers: a **World Wonder** is globally unique (one instance ever, across every civilization in the game) with a city-local effect plus a bonus to a Great Person type; a **National Wonder** is unique **per civilization** (buildable once by each player) and its bonus applies **empire-wide** despite being built in one city — the exact "tier decides whether the effect reaches one sector or the whole empire" shape the owner described. National Wonder cost is a concrete, sourced formula: **125 + 30 × (city count)** production — 275 at 5 cities, 725 at 20 — so the empire-scope tier gets explicitly *more expensive as the empire grows*, not a flat number. | **Two distinct, sourced failure modes, both directly relevant to a tier-gated empire-scope Wonder:** (1) **The prerequisite-lockout trap** — a National Wonder requires its prerequisite building constructed in *every* city the player controls; expand past that point and the Wonder becomes permanently unbuildable, which CivFanatics forum threads call "the biggest trap in the game" and describes as a hard timing dilemma between expanding and keeping the empire-scope tier reachable. If this program's empire-scope tier ever gates on "built in every sector" rather than a flat relic/material cost, it inherits this exact lockout — the recommendation is to gate empire-scope purely on relic/rarity cost, never on a per-sector prerequisite. (2) **The production-sink trap** — chasing Wonders (National or World) at the expense of basic city infrastructure is a named newbie mistake that leaves cities under-defended and under-developed even as the player's wonder count looks impressive; the parallel here is a Wonder's relic/material cost must stay a genuine trade-off against ordinary structures (Well, Granary), never a strictly-dominant spend once relics exist. |

| **Added fourth pass — Endless Legend (Amplitude Studios), Games2Gether community-quest heroes** ([Endless Legend Fandom: Quest](https://endless-legend.fandom.com/wiki/Quest), [Gamerant: "How to Get More Heroes"](https://gamerant.com/endless-legend-how-get-more-heroes/)) | **The closest real example found of a building/event-granted unlock that survives the current playthrough ending, the exact shape a Multiverse-scope Wonder would need.** A hero unlocked through a Games2Gether community quest is banked at the **player/account level, not the save**: it becomes available in the Academy/Marketplace of *every future game* that player starts, the same "outlives this run" property this doc's Multiverse tier needs — confirmed via the cited wiki/guide pages, not fabricated. This is the sourced precedent that such a mechanic is a normal, shippable 4X convention, not a novel risk. | **Not identified as a distinct failure mode in the sources found this session** — flagged as unverified beyond the cited pages, same caveat this doc already applies to the Stellaris row above. The one design implication worth naming without inventing a risk: an account-level unlock this permanent needs a very deliberate bar (Endless Legend gates it behind a community-wide quest, not routine play), which is a tuning/gating question for whoever eventually specs Multiverse scope, not an architecture one. |

Four sources now confirm the shape the owner specified is a proven, load-bearing genre convention —
not a novel mechanic — the third (Civ5) additionally confirms the **specific tier→scope gate** the
owner named the third pass, with a concrete cost formula and two named failure modes to design around
rather than discover live, and the fourth (Endless Legend) confirms this pass's new top rung —
**an account/player-level unlock that survives a playthrough ending** — is itself a real, shipped genre
convention, not a novel persistence category invented for this doc.

---

## The shape

### Relics — SUPERSEDED third pass: neither original candidate is right

**The first pass offered two stock-shaped candidates (kept below, struck through, for the reasoning
trail — never silently deleted). The owner rejected both and named a third, different-in-kind shape:
relics are rolled ITEMS, not a stock counter at all.**

~~1. A new closed small vocabulary, world/faction-scoped stock (`WorldFaction`-level, keyed by relic
   id → `long` count), following the exact precedent `RubbleStock`/`IronworkStock` already set on
   `WorldSector`.~~
~~2. A single fungible world/faction-scoped `long` stock, exactly like `LoamStock` itself.~~

Both assumed a relic is a *counted resource* — the same shape as loam, rubble, or ironwork. The owner's
own words ("relic is unique items, they will generate by seedsmith item generator and make drop tables
to register them") describe a *rolled, individually-identified item* instead — the same conceptual
shape as a piece of gear or a dungeon unique, not a fungible number. This is a correction at the type
level: a relic needs an `instance_id` (or the future sector-inventory-scope equivalent) the way a
`rpg_item` row does, not a `+1` on a counter.

**The corrected shape, verified this session (§The owner's third-pass framing above has the full
citation trail):**

- **Minting:** extend the existing item-generation pipeline. The closed 15-kind item-seed vocabulary
  already has a `unique` kind (`kinds.py:56-68`) — the nearest existing fit — but its `baseType`
  requirement (an equip-role gear reference) and combat-facing fields (`fixedAtoms`,
  `counterPressure`, `powerAxis`) assume an equippable item, which a relic (never equipped, no combat
  role) is not. **Open, narrow, reviewable question:** relax `unique` to a no-`baseType` variant, or
  register a small sibling kind/`DropEntryKind` member. Either way this is a one-`Register`-shaped
  addition to an existing closed vocabulary, never a new generator program or a new drop-table format.
- **Registering into drop tables:** no new mechanism needed. `DropEntryKind.Unique`
  (`DropTableModel.cs:16-27`) already exists, and the loops the owner named — delve, expedition, world
  map assault, quest — already have real, wired `SourceKind`s in `DropTableValidator.KnownSourceKinds`
  (`world-sector`, `expedition-tier`, `dungeon-room`/`dungeon-clear`/`dungeon-quest`, `siege-assault`).
  Only "lawn run" has no wired source today (`pvz-run` is explicitly refused, an unrelated
  pre-existing gap, §4.1). Authoring relic drop-table rows against these existing sources is content
  work, not engine work.
- **Storage, once earned:** this is where the real new work is. A relic a legion is carrying, or a
  relic sitting in a sector waiting to be spent on a Wonder, has no home today —
  `rpg_item`/`rpg_item_stock` are player-scoped only (verified this session, §Built table). **This is
  the dependency named in full in §The owner's third-pass framing and §Open questions**: Wonder-
  building blocks on `docs/architecture/scoped-inventory-hierarchy-ideal.md`, a sibling investigation.

This still does not touch `MaterialClass` (`MaterialCatalog.cs:11-28`) — that vocabulary is closed,
ask-first, and scoped to gear-crafting spend, a different question than "what kind of rolled item is
a relic" or "where does a legion-carried item live."

### Four scopes, gated by `WonderScope` — SUPERSEDED fourth pass: the two-tier ladder is now four

**The second/third-pass framing (kept below, struck through, for the reasoning trail — never silently
deleted) read the owner's "2 scope, empire and sector" as a two-value `WonderTier` crossed with an
orthogonal `Rarity` scarcity axis, with per-faction-vs-server-wide uniqueness left open as Open
question #3.** Asked to resolve that binary directly, the owner rejected it and named a different,
wider shape instead, verbatim: *"multiple scope of wonder, sector only, empire only, world only,
multi-verse only."* This **supersedes** the two-tier ladder outright — scope itself is the four-rung
axis, not a two-value tier crossed with a separate scarcity binary:

~~**Reading the owner's wording.** *"Wonder have 2 scope, empire and sector, depend on it[s] tier and
rarity"* is genuinely ambiguous between two readings: (a) tier alone selects scope and rarity is
orthogonal (scarcity only), or (b) scope is jointly derived from both. This doc picks reading (a)
and states it as an interpretation, not a fact: tier is the scope-gating axis (what the Wonder
does and how far it reaches); rarity is a separate, orthogonal scarcity axis (how many instances can
exist).~~

~~**Proposed tier ladder** — a small closed enum on the Wonder's catalog row (distinct from
`StructureDef.MaterialTier`, which already exists for durability scaling, `StructureCatalog.cs:104,
131`, and must not be reused for this):~~

~~| `WonderTier` | Scope | Buildable today (loam-only, zero new plumbing) | Rarity default |~~
~~|---|---|---|---|~~
~~| `Minor` | Sector | **Yes** | Common |~~
~~| `Grand` | Empire | **No, not yet** (wiring + real gap) | Unique — one per faction |~~

**The corrected shape — `WonderScope`, four values, distinct from `StructureDef.MaterialTier`
(durability scaling, `StructureCatalog.cs:104, 131`) for the same "no second `f(level)`" reason the
struck-through note above already gave:**

| `WonderScope` | What it reaches | Persistence boundary | Buildable today (zero new plumbing) |
|---|---|---|---|
| `Sector` | One `WorldSector`'s own structure row — reuses `YieldMultiplierMilli`/`FlatYieldPerTurn` | Dies with the sector's own `WorldState` | **Yes** — the entire mechanism already exists (§Why this is not a new mechanism) |
| `Empire` | One `WorldFaction`'s holdings inside one `WorldState` — writes the faction-wide lever, `WorldFaction.ScopeModifierMilli` or a sibling field of the identical shape | Dies with that `world_id` (`RpgStore.World.cs:37-105`, every faction/sector/entity table keyed by `world_id`, no cross-world reference) | **No, not yet** — storage is free (wiring gap only), but nothing reads a faction-level value inside `LoamProduction.For` yet (real gap, `LoamProduction.cs:18-62`). **Confirmed in scope for the program's first build** (owner: "Ship both together," §Open questions #5) |
| `World` | Every `WorldFaction` under one `world_id` at once — mechanically "apply the Empire-scope write to every faction row in this world," not a new storage shape | Dies with that `world_id`, identically to `Empire` — same tables, more rows written | **No** — needs the same `LoamProduction.For` faction-input plumbing as `Empire`, looped across every faction; not new plumbing of its own, but not shippable until `Empire`'s plumbing lands |
| `Multiverse` | Every world the player will ever start — a player-scoped effect, never a `WorldState`/`world_id`-scoped one | **Survives a lost/ended world** — the first Wonder scope with this property | **No — the real gap.** No player-scoped, building-granted effect table exists anywhere (§The owner's fourth-pass framing, §Real gap table). Not in scope for this program's first build. |

Four tiers, matching the owner's own count directly rather than the second/third pass's inference of
two. `Empire` and `World` share one mechanical concern (the `LoamProduction.For` faction-input gap);
`Sector` is free today; `Multiverse` is a new persistence category, not a bigger number on the existing
ladder (§The owner's fourth-pass framing). An `Empire`/`World`/`Multiverse` Wonder's cost should scale
with something the way Civ5's National Wonder cost scales with city count (§Prior art) — sector count
or `DevelopmentLevel` sum for `Empire`/`World`, and something like total worlds played or total
Multiverse-tier relics ever earned for `Multiverse` — a tuning decision, not an architecture one.

**Rarity, still orthogonal to scope, narrowed by this pass rather than reopened:** `Common` (no
existence cap) or `Unique` (an existence cap of one). What "one" is scoped against now tracks
`WonderScope` directly rather than needing its own separate per-faction/server-wide answer: a `Unique`
`Sector`-scope Wonder is capped per sector, a `Unique` `Empire`-scope Wonder per faction (mirroring
Civ5's National Wonder), a `Unique` `World`-scope Wonder per `world_id` (mirroring Civ6's World
Wonder — one instance across every faction *in that world*, not literally server-wide across every
player's separate `rpg_worlds` row), and a `Unique` `Multiverse`-scope Wonder per player. **Correction
(strengthen pass 2026-09-13, finding F9) — "structurally almost forced" struck, contradicted its own
doc.** This previously read *"structurally almost forced, since its storage is a player-scoped ledger
row in the first place — 'unique' and 'exists at all' collapse into the same question at that
scope."* That directly contradicts two things this same doc states elsewhere: the ledger design two
sections earlier explicitly anticipates *"a player could plausibly bank more than one such effect over
time"* (§The owner's fourth-pass framing, the Multiverse minimal-shape paragraph), and this very
paragraph's own next sentence states the two fields "stay independent by construction" for every
scope. Multiverse-scope rarity is a **tunable, like any other scope's, not architecturally special** —
nothing forces `Unique = 1` at the schema level (a player-scoped ledger keyed by `player_id` + effect
kind can hold any number of rows); whether it *should* be capped at one is a balance question for
whoever tunes it, never an architectural inevitability. This also keeps the doc compliant with this
repo's own "no hard progression ceilings" rule, which a literal "almost forced to one" reading would
risk being taken as permission to hardcode a cap. This resolves Open question #3's
original per-faction-vs-server-wide binary by replacing it with a scope-relative answer instead of
picking one side. A `Sector`-scope Wonder defaults to `Common`; nothing stops a future tuning pass
making any scope `Unique` or `Common` — the two fields stay independent by construction, the way
`DemonRarity` and a creature's other closed axes are independent of each other.

### The effect-kind vocabulary — closed today, reviewed-growth by design

**Precedent mirrored:** `StatusCatalogBootstrap.cs:1-69` registers 24 status ids against two closed
enums — `StatusKind` (what family of thing this is: `UnityCc`, `OverTime`, `Buff`, `Debuff`, `Meter`,
`CrowdControl`, `Contagion`, `Counter`) and `StatusPayloadKind` (what shape its data takes:
`UnityCc`, `PulseHp`, `ModifyStat`, `Spread`). A new status is a reviewed `Register(...)` call, never
an open string, and the vocabulary has grown from its original set to 24 by exactly that path. A
Wonder's effect vocabulary is designed the same way:

```
WonderEffectKind   — closed enum, what the effect boosts
WonderEffectScope  — closed enum, { Sector, Empire, World, Multiverse } — CORRECTED fourth pass,
                     was { Sector, Empire } — grows the same two new members WonderScope does, since
                     the two enums are meant to move together (§The shape). Set by the Wonder's
                     WonderScope, not chosen freely per instance (keeps "which scope reaches how far"
                     a catalog-authored decision, never a runtime branch)
WonderEffectDef    { Kind: WonderEffectKind, ValueMilli: long }  — per-mille magnitude, tunable.
                     A Multiverse-scope WonderEffectDef additionally implies a different write target
                     (the new player-scoped ledger, §The owner's fourth-pass framing) rather than a
                     WorldState-scoped structure row — named here so a future spec does not assume
                     one WonderEffectDef shape covers one storage shape for all four scopes
```

**Ships in v1 (loam-only, per the owner's explicit "focus on loam now"):**

| `WonderEffectKind` member | Scope it can carry | Consumer |
|---|---|---|
| `LoamGenerationRate` | `Sector` only (buildable today) — `Empire`/`World` scope for this same kind is **named but not shippable in v1**, because `LoamProduction.For` has no faction-level input yet (Real gap table above); `Multiverse` scope for this kind is not designed at all — a Multiverse-scope loam bonus would have to apply at the *next* world's creation, not to any currently-loaded `WorldState` (§The owner's fourth-pass framing) | `LoamProduction.For` via `StructureDef.YieldMultiplierMilli`/`FlatYieldPerTurn` — no new engine code |

**Named and reserved, not registered — the "reverse[reserve] vocabulary for extend later" the owner
asked for.** These are documented here as the anticipated next members, exactly as the owner listed
them, but **do not become enum members until each one is actually wired to a real consumer** — an
enum value with no reader would be the inert-path pattern this repo's own rules ban (`AGENTS.md`
"never a template" for grandfathered debt; the aura-skill incident `CLAUDE.md` records for exactly
this shape of premature declaration):

| Reserved kind | Player-facing effect | What has to exist first |
|---|---|---|
| `DefensePower` | Boosts a defending unit's combat power in a warded/garrisoned sector | A real sector-scoped combat-power read at world/siege scope — not audited this session, likely lives near `base-defense`'s siege stack |
| `AuraGrant` | An aura/buff applied to defending units in the sector, not just a flat stat add | A world-map-to-battle aura delivery path — `patron.aura`'s combat-scope precedent (cited in the buff-debuff-scope docs) is the nearest analog, but nothing wires it to the world map today |
| `EmpireBuff` | A buff reaching every sector/legion the faction owns | `WorldFaction.ScopeModifierMilli` is the storage; the consumer(s) it would apply to are per-mechanism and each is its own wiring task |

**Added (strengthen pass 2026-09-13, finding F11) — a standing warning for whoever eventually designs
`DefensePower`/`AuraGrant`, not a current violation.** Both are real actor-combat magnitudes. When
eventually designed, both must contribute via `ActorHub` (`IActorStatSubsystem`/registered atom reader)
or consume Hub output only — never a private per-sector combat fold — per this repo's binding "One
ActorHub compose / one read" hard rule (`CLAUDE.md`). Neither kind is registered today, so there is no
violation yet; this note exists so the future session that does design them reads it before inventing a
second composer.

Adding one of these later is a **reviewed one-line `Register`-shaped addition** once its consumer is
real, not a schema change — `WonderEffectDef`'s shape (`Kind` + `ValueMilli`) does not need to change
for any of the three, because all three are still "a per-mille magnitude applied somewhere," the same
shape `LoamGenerationRate` already is.

### Wonders — a Structure, not a new subsystem

A Wonder is mechanically a `StructureDef` row (`StructureCatalog.cs:42-194`) with:

- `Kind = StructureKind.Yield` (or a new orthogonal "is a Wonder" flag — see the real gap above),
  reusing the existing `YieldMultiplierMilli`/`FlatYieldPerTurn` fields the loam engine already
  reads.
- A **new cost field** denominated in relics — genuinely missing from `StructureDef` today, and the
  one item this doc still cannot close without the relic-kind question above being settled. **The
  "basic material" half is RESOLVED third pass, not a new field**: `ConstructRubbleCost`/
  `ConstructIronworkCost` already exist on `StructureDef` for exactly this purpose (siege
  construction's own bulk-material spend) and are already the same fields peacetime rows like `well`
  ship with, zeroed. A Wonder authors those two fields directly — no new field, no new engine code.
- A large `YieldMultiplierMilli`/`FlatYieldPerTurn` value, tunable, meaningfully bigger than a Well's
  2× (`well.json:54`) — the owner's own framing ("this is better" than Warden's free freeze) implies
  a Wonder should be worth more than an ordinary structure, not merely different.
- Generated the same way base-defense's siege structures already are: a new anchor authored through
  `tools/seedsmith/seedsmith/adapters/structures/generate_anchor.py`, run through
  `generate_corpus.py`, landing in `data/seed/structures/` beside the existing 25 rows — **no new
  generator program**, an extension of the one that already exists.

### Why this is not a new mechanism

The owner's stated goal — replace Warden's free stability freeze with something that raises
generation rate instead — is **already satisfiable by the shipped structure model**, once a relic
item kind and a relic-denominated cost field exist. The hard part this session's investigation
actually found is not "can the loam engine express a stronger generator" (yes, trivially — two
fields already do), and third pass narrows it further still: it is not even "what mechanism mints and
registers a relic" (the item-generation + drop-table pipeline already does this for every other rolled
item). **It is "does a relic fit `unique`'s existing shape, and where does an earned-but-not-yet-spent
relic live"** — a content/schema question and a genuinely new inventory-scope question, not an engine
question.

### Alternatives rejected

| Option | Why not |
|---|---|
| Extend `MaterialClass` (the 27-id gear vocabulary) with a `Relic` class | That enum is explicitly closed and ask-first (`materialgen/__init__.py:6-10`), and its whole taxonomy answers "what does an equip *operation* spend" — a world-map building cost is a different question at a different scope. Forcing it in would make a player-scoped, gear-crafting table also carry world/faction-scoped building costs. |
| A brand-new `.NET` seedsmith-style tool (`WonderGen`) alongside `CreatureSpeciesGen`/`DemonSpeciesGen` | The Python seedsmith `structures` adapter already exists, is LLM-capable, and already produces the exact `StructureCorpus` rows `StructureCatalog` consumes — a second, C#, parallel generator for the same corpus shape would be the SOLID/dual-compose defect this repo's hard rules already ban for combat compose, applied here to content generation. |
| Model Wonder as a new `StructureKind` value | Plausible, but a boolean/flag is the narrower change — `Kind` already answers "what economic *behavior* does this structure have" (`LoamSource`/`Storage`/`Yield`/`Refinery`/`Obstacle`), and a Wonder's behavior is still `Yield`-shaped; "is this a unique, relic-gated tier" is an orthogonal question. Left open below rather than decided, since it also depends on the uniqueness question (#3, corrected fourth pass — was mis-numbered #2 in an earlier pass). |

---

## Tunables

None of these are decided by this doc — named so a future spec knows where they land, not what they
equal:

| Tunable | Home |
|---|---|
| Relic-to-Wonder cost (quantity of relics, quantity of `ConstructRubbleCost`/`ConstructIronworkCost`) | `data/tuning/loam.v{n}.json`'s `structures` block, beside `wellCostMilli`/`waystationCostMilli`/`granaryCostMilli` — **corrected third pass:** the basic-material half is no longer an open modeling choice (§Open questions #4, RESOLVED), it is the same `RubbleStock`/`IronworkStock`-denominated fields siege construction already tunes |
| Wonder `BuildTurns` | Same block, beside `wellBuildTurns`/`waystationBuildTurns`/`granaryBuildTurns` |
| Wonder `YieldMultiplierMilli` / `FlatYieldPerTurn` | Same block — these are the exact two fields `LoamProduction.For` already reads, so no new engine-side plumbing, only new authored rows |
| Relic drop/mint rate, wherever the eventual faucet lands | Owned by whichever loop mints it (expedition drop table, delve loot, quest reward, or world-map assault) — not decided by this doc, see Open questions #1 |
| Any relic stock cap | **Corrected third pass:** relics are rolled items, not a counted stock, so this row no longer applies in its original form — the analogous concern is whatever cap (if any) the future sector-inventory concept places on stored item count, owned by `scoped-inventory-hierarchy-ideal.md`, not this doc |
| `Empire`/`World`-tier build cost scaling (added third pass, retitled fourth) — whether it scales with sector count / `DevelopmentLevel` sum the way Civ5's National Wonder scales with city count | New row in the `structures`/`development` blocks, same file; the scaling *formula* is a balance question, not decided here |
| `Multiverse`-tier build cost scaling (added fourth pass) — the natural analog per §The shape is total worlds played, or total Multiverse-eligible relics ever earned, rather than anything inside one `WorldState` | Same blocks, once the player-scoped ledger (§The owner's fourth-pass framing) exists to be spent from — a balance question layered on top of a real-gap dependency, not decided here |
| `WonderEffectDef.ValueMilli` for `LoamGenerationRate` at each scope (corrected fourth pass, was "each tier") | Same block as the existing `YieldMultiplierMilli`/`FlatYieldPerTurn` tunables above — `Sector`/`Empire`/`World`/`Multiverse` are four rows, not four mechanisms |
| Rarity existence cap for `Unique` (added third pass, narrowed fourth) — **no longer a single per-faction-or-server-wide choice**: each `WonderScope` gets its own cap unit (per sector, per faction, per world, per player — §The shape's rarity paragraph) | Wherever the eventual uniqueness validation lands per scope; the *cap itself* is a plain tunable count for each scope, never a hard-coded `1` |

---

## What this deliberately does not decide

- **Which loop mints a relic — the exact table/row, not the mechanism or the staging.** The mechanism
  (item kind + drop table + already-wired `SourceKind`s) is confirmed, third pass, and the staging
  question (one loop first vs. many at once) is **RESOLVED fourth pass** — all named loops at once, one
  caveat (§The owner's fourth-pass framing). What is still open: the exact
  expedition/delve/quest/world-map-assault/lawn table and row, and the rate for each — see Open
  questions #1.
- **CORRECTED third pass, no longer "one fungible relic vs. a small named set."** The first pass's
  framing of this as a stock-vocabulary-size question is superseded — relics are rolled items,
  discussed in full in §The shape. What is genuinely still undecided: whether `unique`'s `KindSpec`
  grows a no-`baseType` variant or a relic gets its own sibling kind, and how many distinct relic
  *identities* (names/flavors) v1 authors versus treating every relic as interchangeable at the same
  rarity. See Open questions #2 (rewritten).
- **Whether a Wonder is per-sector-buildable or a true one-per-world/one-per-faction unique** — this
  determines whether `StructureKind` needs a new value, whether a flag suffices, and whether any
  uniqueness validation is needed at all.
- **The exact bonus magnitude** — how much bigger than a Well's 2× a Wonder should be. A balance
  question for whoever tunes `data/tuning/loam.v{n}.json`, not an architecture question.
- **Whether Zomboss's AI ever competes for the same Wonder** — if it does, the wonder-race failure
  mode above (Civ6/Humankind prior art) applies directly and needs a mitigation named before that
  content ships.
- **The special-unit-trait and commander-trait levers** the owner named alongside relics. Both are
  separate faucets into the same already-working `YieldMultiplierMilli`/`FlatYieldPerTurn`
  mechanism this doc proves out via Wonders; neither is designed here.
- **`DefensePower`, `AuraGrant`, `EmpireBuff`** — named and reserved in the effect-kind vocabulary
  above, but none is designed past a one-line name-and-purpose entry. Each needs its own consumer
  investigation before it can become a real enum member (§The effect-kind vocabulary).
- **The sector-scoped (and legion-in-transit) inventory concept itself.** Named as a hard dependency
  in full above and in the real-gap table; the design belongs to
  `docs/architecture/scoped-inventory-hierarchy-ideal.md`, a sibling investigation this doc does not
  duplicate or pre-empt.
- **RESOLVED, removed third pass:** "whether `Grand`-tier ships in the same wave as `Minor`" is no
  longer undecided — the owner said ship both together. See Open questions #5 (now `Sector`/`Empire`
  under the renamed `WonderScope` ladder, §The shape).
- **The `Multiverse`-scope player ledger itself.** Named as a category and a seam in §The owner's
  fourth-pass framing and the Real gap table — not designed past that. Its table shape (columns beyond
  `player_id` + effect kind + value), its write path, and its read path at world-creation time are all
  future work, explicitly out of scope for this program's first build (§Open questions, new #7).

---

## Open questions — owner decisions only

1. **RESOLVED (staging) fourth pass, narrowed — what mints a relic, exact table still open.** The
   owner settled the staging question this pass, verbatim: relics get drop-table entries in delve,
   lawn, expedition, world-map assault, and quest **simultaneously**, not staged one loop at a time.
   Four of those five loops (`world-sector`/`expedition-tier`/`dungeon-room`/`dungeon-clear`/
   `dungeon-quest`/`siege-assault`) are real, wired `SourceKind`s today and can carry a relic row now;
   **the fifth, lawn, still cannot** — `pvz-run` is `DropTableValidator.UndesignedSourceKind`
   (`DropTableValidator.cs:50`), refused for a pre-existing, unrelated reason (§4.1's own note: PvZ-run
   content level was never implemented anywhere). This pass does not resolve that unrelated gap; it
   only confirms the other four ship together and lawn joins once `pvz-run` is separately designed.
   **Still open:** the exact row/table and drop rate for each of the four, and whether lawn gets a
   placeholder relic entry now (inert until `pvz-run` exists) or waits entirely.
2. **RESOLVED (corrected) third pass — relic vocabulary shape.** Neither original candidate (a
   fungible stock or a small named stock set) is right: relics are rolled items via the existing
   item-generation + drop-table pipeline (§The shape). **One real sub-question remains, genuinely
   open:** does today's `unique` `KindSpec` grow a no-`baseType` variant to fit a non-equip relic, or
   does a relic register as its own sibling kind/`DropEntryKind` member? Either is a small, reviewed
   addition to an existing closed vocabulary — a content/schema call for whoever specs the item-gen
   extension, not an architecture call this doc makes.
3. **Wonder scope/scarcity rule — fully RESOLVED 2026-09-13 (/spec kickoff).** The four-tier
   `WonderScope` ladder stands (`Sector`/`Empire`/`World`/`Multiverse`). Owner confirmed: **Zomboss's AI
   can compete for a `World`-scope `Unique` Wonder slot** — a real contested slot, not a player-only
   convenience. `/spec` must therefore design an explicit wonder-race mitigation (refund on loss, or no
   shared contest at all — the Civ6/Humankind failure mode in §Prior art applies directly) before any
   `World`-scope `Unique` Wonder ships. Per Open question #5 below, this work is **deferred with `World`
   scope itself** to a later wave, not blocking `Sector`/`Empire`'s first build — but the mitigation
   requirement is now locked-in, not left for later re-discovery.
4. **RESOLVED third pass — "basic material" partner to relics.** Siege construction and world-map
   sector construction are confirmed (this session, §The owner's third-pass framing) to be the same
   `StructureDef`/`StructureCatalog` machinery, gated only by which cost field is nonzero and which
   clock reads it. A Wonder's basic-material cost is `RubbleStock`/`IronworkStock`, spent through the
   `ConstructRubbleCost`/`ConstructIronworkCost` fields `StructureDef` already carries — no new field,
   no new material kind, no new engine plumbing.
5. **RESOLVED third pass — does `Empire` scope (formerly named `Grand`) ship in the same wave as
   `Sector` scope (formerly `Minor`)?** The owner said, verbatim: *"Ship both together."* Consequence,
   stated so it is not silently dropped: the `LoamProduction.For` signature change (a faction-level
   input, real gap, `LoamProduction.cs:18-62`) and a first reader for `WorldFaction.ScopeModifierMilli`
   (`WorldState.cs:85-93`) are now **confirmed in scope for this program's first build**, not
   deferred. This doc still does not design that plumbing — only removes the option to skip it.
   **RESOLVED 2026-09-13 (/spec kickoff):** `World` scope is **deferred to a later wave**, not shipped
   alongside `Sector`/`Empire`. First build is `Sector` + `Empire` only. `World` scope waits until its
   own wave, at which point it inherits both the `LoamProduction.For` faction-loop plumbing (already
   proven for `Empire`) and the wonder-race mitigation now locked in by Open question #3 above.
   `Multiverse` scope is separately named as out of scope for the first build regardless (§The shape,
   Open question #7) — that part was never in question.
6. **New this pass — sector-scoped (and legion-in-transit) inventory design.** Not this doc's to
   answer: owned by the sibling investigation `docs/architecture/scoped-inventory-hierarchy-ideal.md`.
   Named here as a **hard, blocking dependency**: Wonder-building via legion-carried relics cannot
   resolve end-to-end (mint → carry → deposit at sector → spend on Wonder) until that concept exists,
   confirmed this session by `rpg_item`/`rpg_item_stock` being player-scoped only with no sector or
   legion column (`RpgStore.Items.cs:85-107`). This program's Wonder-building flow should track that
   doc's own open questions rather than re-deriving them.
7. **New fourth pass — the `Multiverse`-scope player ledger design.** Not this doc's to answer past
   naming the category and the seam (§The owner's fourth-pass framing, §Real gap table): a new
   player-scoped table, PK `player_id` (+ effect kind), no `world_id` column, written when a
   Multiverse-tier Wonder is built and read wherever `WorldTemplateCatalog.Build` first composes a
   fresh world's starting state — **corrected (strengthen pass 2026-09-13, F4):** not
   `RpgStore.World.cs:238`'s `CreateWorld`/`INSERT INTO rpg_worlds`, which only persists an
   already-composed `WorldState`; the real composition seam is `WorldTemplateCatalog.Build`, and even
   that seam has no production caller today (`WorldEndpoints.cs:382`, `MapWorldTest`, is `/api/test`
   only — a bigger prerequisite gap than previously stated). Genuinely open, for whoever specs this:
   the table's exact columns, what caps how many Multiverse-scope effects a player can bank at once (if
   anything), whether the effect is consumed on first read or persists across every future world, and
   whether `Multiverse` scope
   ships in any near-term wave at all or stays reserved-vocabulary the way `DefensePower`/`AuraGrant`/
   `EmpireBuff` already are (§The effect-kind vocabulary) — the owner has not been asked either way.
8. **New (strengthen pass 2026-09-13, finding F3) — combination rule for multiple Empire/World-scope
   Wonders on one faction.** `Common` rarity is uncapped, so a faction could hold 2+ `Empire`-scope
   Wonders at once, each with its own `WonderEffectDef` writing into the same
   `WorldFaction.ScopeModifierMilli` scalar (`WorldState.cs:85-93`). No combination rule (sum, max,
   replace) is named. This doc **recommends SUM** — the same discipline `LoamProduction.For`'s
   `FlatYieldPerTurn` loop already uses for multiple structures' additive contributions
   (`LoamProduction.cs:43-51`) — but flags it as needing review before being locked, since
   `ScopeModifierMilli`'s own doc comment anticipated one future consumer, not a summed multi-writer
   field. Not decided here; whoever specs the `Empire`/`World`-scope plumbing should settle it.
9. **New (strengthen pass 2026-09-13, finding F21) — relic sink beyond Wonder-spend.** Relics mint
   from up to five simultaneous loops (§The owner's fourth-pass framing, relic faucets) but have no
   named sink beyond spending on a Wonder. Once a player has built every Wonder they practically want,
   relics have nowhere left to go — a faucet with no sink past a point of play. This doc deliberately
   does not design a fix (salvage, convert-to-material, sell-back are all plausible) and does not treat
   it as blocking v1: `Common`-rarity Wonders are explicitly uncapped (correctly avoiding a hard relic
   stock cap, per this repo's no-hard-progression-ceilings rule), so there is always *something* to
   spend relics on even late — but a future balance pass should expect this to surface as a
   faucet-heavy resource once players reach their practical Wonder-building ceiling. Flagged as a
   future balance-pass risk, not a v1 blocker.

## Handoff

**Added (strengthen pass 2026-09-13, finding F16).** Written: `docs/architecture/loam-relics-and-wonders-ideal.md`
(this file). Next step: `/spec` for a capability map + module specs, once the open questions above are
answered by the owner — this program's own dependency on `scoped-inventory-hierarchy-ideal.md` (Open
question #6) must clear first, per `empire-development-map.md`'s dependency edge
(`scoped-inventory → loam-relics-wonders`). Do not write specs, plans, or code from this doc alone.

## Corrections log — strengthen pass 2026-09-13

Four independent lenses (mechanism soundness · cross-program/economy · hard-rule compliance ·
spec-quality-vs-bar) ran this session. See the four lens reports this session for the full
checked-and-held list; refuted probes are not repeated here since they were not handed to this pass.

| # | Lens | Finding | Disposition |
|---|---|---|---|
| F3 | mechanism soundness | No combination rule for 2+ Empire-scope Wonders writing `WorldFaction.ScopeModifierMilli`, a scalar whose own doc comment assumes one consumer | Added as a Real-gap table row + Open question #8; recommends SUM (matches `LoamProduction.For`'s `FlatYieldPerTurn` additive precedent), flagged as needing review |
| F4 | mechanism soundness | Multiverse-scope integration-seam cited `RpgStore.World.cs:238`/`CreateWorld`'s `INSERT`, which only persists an already-composed `WorldState` | Corrected to `WorldTemplateCatalog.Build`, the real composition seam; added further note that its one call site (`WorldEndpoints.cs:382`, `MapWorldTest`) is `/api/test`-only, no production caller exists |
| F9 | hard-rule compliance | "Structurally almost forced" language for Multiverse-scope `Unique` contradicted this doc's own ledger-design paragraph and its own "independent by construction" statement two sentences later; risked reading as license for a hard-coded cap | Struck the "almost forced" framing; replaced with "a tunable like any other scope's, not architecturally special" |
| F11 | hard-rule compliance | `DefensePower`/`AuraGrant` are real actor-combat magnitudes with no standing note about the "One ActorHub compose" rule | Added a warning sentence to the reserved-effect-kind table for whoever designs them later |
| F12 | spec-quality-vs-bar | No `## Which loop this extends` heading — folded into Step 0 prose, the idea-phase skill's own named red flag | Promoted to its own top-level heading; content unchanged |
| F15 | spec-quality-vs-bar | Header misattributed a claim to `warden-mortality-ideal.md` ("names a file `loam-generation-rate-ideal.md`") that doc never makes | Verified by reading that doc in full (no match); corrected to state this doc is itself the follow-up, not a citation of the sibling doc |
| F16 | spec-quality-vs-bar | No `## Handoff` section per the idea-phase skill's Step 5 | Added, naming the written path, the `/spec` next step, and the `scoped-inventory` dependency |
| F20 | spec-quality-vs-bar | World-scope paragraph cited `RpgStore.World.cs:20-35` alone for `kind` defaulting to `'map'`; those lines are the bare `CREATE TABLE` with no `kind` column | Corrected to cite both `:20-35` (base schema) and `:201-202` (`EnsureColumn` additions), matching the doc's own already-correct Built-table row |
| F21 | mechanism soundness | Relics mint from up to five loops with no named sink beyond Wonder-spend | Added as Open question #9 — flagged as a future balance-pass risk (faucet past a point of play), not a v1 blocker; correctly does not propose a hard stock cap |
| F22 | cross-program/economy | This doc's `unique`-`KindSpec` relaxation ask and `deployment-hierarchy-map.md`'s repair-verb/`op_kind` ask both land on the item program's registry surface, uncoordinated | Added a one-line cross-reference in both docs' relevant tables so the item program can review them together |
