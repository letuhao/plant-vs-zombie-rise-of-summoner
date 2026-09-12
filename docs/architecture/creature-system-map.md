# Capability map: Creature gameplay system

Source vision: the creature gameplay design note (external; its **ideals** are adopted, its architecture is not — everything below builds on the shipped overlay stack). Status: **approved 2026-08-21** (owner resolved the three shaping decisions below). Module specs live in [creatures/](creatures/), one per module id, written in dependency order. Implementation plan/task list: [tasks/creature-progression-plan.md](../../tasks/creature-progression-plan.md) / [tasks/creature-progression-todo.md](../../tasks/creature-progression-todo.md).

## Resolved decisions (2026-08-21)

1. **What a deployed creature is:** both modes, chosen per species — most creatures deploy as **plant-side avatars** (empowered unique plants carrying the creature's traits/element as effect grants); designated boss-class species deploy as **hypno-zombie allies**. Capture/deploy modules spec the details later; hypno mode inherits the MatchRuntime hypno-fold caveats.
2. **Elements:** the ElementHub ring is **extended** (new prerequisite module `element-extension`); `void`/`chaos` from the vision stay traits, not elements. Requires a decisions.md amendment + matchup golden tests before creature typing lands.
3. **V1 slice:** `element-extension` → `creature-core` → `soul-economy` → `creature-summoning` (gacha). Capture, contracts depth, fusion, FE domain, world events follow.

## Design rules adopted (the "ideal")

1. Gacha never replaces gameplay; every acquisition method feels different.
2. Duplicates and low-rarity creatures keep value (fusion material, trait donors).
3. Fusion creates build possibilities, not just bigger numbers.
4. Creatures are individuals: personality, loyalty, history, lineage — not equipment.
5. The world contains creatures worth hunting; rare discoveries feel earned ("I earned this capture").
6. Target story: found in the world → barely won → contracted → trained → fused → inherited a trait → evolved → raid-carried → strongest army member. Systems, not scripts.

## Mapping the vision onto the shipped stack

| Vision concept | Lands on (existing) | Notes |
|---|---|---|
| Creature (individual) | **UniqueActor specimen** (`instanceId`, phase FSM, equip, XP) | Extended with rarity, variant, traits, contract state |
| Creature species / Codex | `types` catalog + almanac dumps + new discovery flags | Codex = almanac FE with discovery states |
| Traits / skills | **Foundation Effects grants** (EffectBag templates per specimen) | Trait = grant template; inheritance = template transfer |
| Elements | **ElementHub** (locked ring: fire/ice/air/earth + omni) | Doc's 8 elements map onto the locked roster (see Q2) |
| Capture condition (weakened, statused) | Live HP + **StatusRuntime** instances at attempt time | Read via MatchRuntime/board snapshot — Hot-plane data |
| Deploy / fight | **MatchRuntime** UniqueBindings + `pvz.*` Intent | Same Admit → PendingSpawn → Bound path as today |
| Souls | New server-side ledger driven by **PvzActivity** facts | Same append+watermark pattern as XP |
| Contracts / loyalty / personality | New Cold-plane state on the specimen (Server + Data) | Affects deploy-time checks and overlay decisions, never Unity AI |
| Fusion | New roster-level system (distinct from plant mixing) | Consumes specimens, mints a new one; recipes discoverable |
| Ecology / blood moon / roaming | Run modifiers + encounter injection via Intent (`spawn.extra`) | "Exploration" is reinterpreted — see Q3 |
| Summoner's Domain | Web FE screens (`#/domain`, evolving `#/roster`) | FE-only until facilities earn server state |

**Hard constraints carried over:** in-run creature behavior is Unity-owned — personality influences *overlay* decisions (obedience checks, deploy gating, effect grants), never zombie pathing/AI. All combat mutation stays on the Funnel/Writer path. No server round-trip on the hit path: capture *resolution* is Cold, capture *conditions* are read Hot.

## Vocabulary: general creature, unique creature, and the two aura roles (added 2026-09-06)

**Written because the terms below get guessed wrong, the same reason the player guide keeps its own
"blind spots" tables — read this once before designing anything that touches more than one of them.**
Two axes are independent and get conflated if read as one: *what a creature IS* (general vs. unique) and
*what ROLE it has been given* (none, Commander, Patron, or — in a Delve specifically — party member).

### Axis 1 — what the creature is

| | **General creature** | **Unique creature** |
|---|---|---|
| Spawned by | The PvZ engine itself (a normal lawn plant/zombie) | The RPG layer — summon, fusion, gacha, capture |
| Identity | Species only — no `instanceId`, nothing persists between spawns | A real specimen: `UniqueActor`, its own `instanceId`, phase FSM |
| Stats | Empire-wide, per-player, per-species progression fallback (primary stats, the general passive tree) | Own specimen progression and `UniqueCreature` allocation, plus equipment (T6.1), passive build, and (once built) aspect/action slots — **never** the empire species fallback |
| Where it's used | Anywhere a large, disposable, or engine-spawned population is needed — a lawn run's own zombie wave, and (per the owner's own 2026-09-06 framing) **siege defenders and world-map legions** | Anywhere an individually-meaningful, player-invested creature belongs — your own roster, a designated Commander or Patron, a Delve party |
| Scale model | **Troop-stack shaped**: one general-creature *type* × a count, the same "multiply one unit by N" shape `base-defense-ideal.md`'s own research already cites (Heroes 3's troop stacks, 7 slots, up to 9,999 each) — not N individually-tracked rows. This is *why* it exists as a distinct kind: a legion or a siege garrison at army scale cannot be N separate `UniqueActor` rows | Never army-scale by design — a roster is dozens, not thousands |

**The "why" in one line:** you cannot build a legion or a siege garrison out of a million individually
-tracked `UniqueActor` rows — general creatures are the lightweight, count-based representation that
scales, unique creatures are the individually-customized representation that doesn't need to.

### Progression source is selected by the spawn mechanism (2026-09-08)

Each gameplay mechanism owns its spawn mechanism and declares the spawned creature's progression source.
A player's empire general creature with no dedicated progression mechanism uses the empire-wide species
fallback. A unique creature uses its own specimen progression; a Commander uses the Commander source.
Neither may also consume the empire species fallback. A `(side, typeId) → species` catalog lookup
identifies a species but is insufficient to select a progression source. This is the binding
`decisions.md` **Creature progression source and spawn ownership** row; unique composition now reads
dedicated allocation, while terminal provenance validation remains tracked in the lawn-deploy plan.

### Axis 2 — the two passive-aura roles (both assignable only to a unique creature)

**Commander and Patron are structurally the same shape — designate one creature, receive one continuous,
passive, side-wide aura for the run — and are easy to conflate for exactly that reason. Neither one
ever fights.** They differ in *what* the aura is and *which loop* it belongs to:

| | **Commander** | **Patron** |
|---|---|---|
| Scope | The lawn run (`commanders.md`) | The match (`spec-patron-creature.md`) |
| What it grants | One active leadership aura (`commander-auras.md` — "one active at a time") **plus** aptitude spend reaching the side | A specific elemental combat bonus (`combat.power.{element}`/`combat.defense.{element}`), scaled by the creature's own rarity/star/level/Θ |
| Cost | Not soul-priced (aptitude spend is its own economy) | First pick free; each switch costs 100 souls |
| Fusion interaction | Not documented as locked | The active patron is **unconsumable** — fusion refuses it as a sacrifice or input |
| Status | WIP (pick + aptitude spend exist thin; full aura fantasy still catching up) | SIM shipped; LIVE gate open; magnitude-delivery migration to the atom system in flight (`patron-absorption`, T6.2) |

**What Commander and Patron are *not*:** an active combat participant. That concept is real, but it
belongs to a third, separate context — a **Delve party member** (`party-dungeon-ideal.md`) — a unique
creature that actively fights, can go `Downed`, inside a dungeon run specifically. A Delve party member is
not a lawn-run role at all, and neither Commander nor Patron ever join a fight the way a party member
does. Do not use "commander" to mean "the creature fighting for me" — that is a party member, in a Delve,
a different loop from the lawn Commander/Patron pick entirely.


> ### ⛔ Two rows below are stale as of 2026-09-01 — read before building either
>
> **`aspect-scope` is REVERTED, not authorized.** Its row still reads *"APPROVED 2026-08-31, authorized
> to build."* The owner reverted it during the creature-seed idea phase: *"revert aspect feature, original
> creature need original aspect, no element/status … the aspect depend on some feature we have not design
> and build yet."* The two it depends on — hybrid element typing and the passive skill graph — are
> unbuilt. **Do not start this module.** The formal amendment is listed as owed in
> [creature-seed-map.md](creature-seed-map.md) §5.
>
> **`creature-summoning`'s "trait rolls" now means something different.** Per
> [effect-pipeline-ideal.md](effect-pipeline-ideal.md) Q10, `traits_json` stays the source of truth for
> *which* traits a creature has, and a `trait.{traitId}` container becomes *what that trait does*. This
> module keeps writing ids exactly as it does now — **no change is required here** — but the ids it
> writes will start carrying effects once `effect-pipeline` ships. Recorded so the change is not
> mistaken for a regression.

## Modules

| Module id | Responsibility | Depends on | Wave |
|---|---|---|---|
| `element-extension` | Extend the ElementHub roster + matchup matrix (light/dark); decisions.md amendment; golden tests | — | **V1** |
| `creature-core` | Specimen identity superset: species link, rarity, variants, trait slots, element typing, Codex discovery state | element-extension | **V1** |
| `progression-source-contract` | Typed spawn/progression-source contract. A gameplay mechanism declares whether an actor resolves through the empire-general fallback, its own unique specimen, or Commander progression; source is never inferred from `typeId`. Spec: [creatures/spec-progression-source-contract.md](creatures/spec-progression-source-contract.md) | creature-core | partial 2026-09-08 |
| `general-empire-fallback` | Lawn-facing, per-player/per-species general-creature progression. Applies only when the declared source is the empire-general fallback; owns the corresponding species-XP eligibility rules. Spec: [creatures/spec-general-empire-fallback.md](creatures/spec-general-empire-fallback.md) | progression-source-contract, species-build allocation transport | partial 2026-09-08 |
| `dedicated-progression-isolation` | Route unique creatures to specimen progression and Commander effects to Commander progression. Removes empire species fallback and species XP from dedicated-source paths. Spec: [creatures/spec-dedicated-progression-isolation.md](creatures/spec-dedicated-progression-isolation.md) | progression-source-contract, unique-actor-runtime | partial 2026-09-08 |
| `soul-economy` | Souls ledger: earn rules from Activity facts, spend API, balances | creature-core | **V1** |
| `creature-summoning` | Summoning/gacha: banners, Souls-funded pulls, rarity/variant/trait rolls, mint specimens | creature-core, soul-economy | **V1** |
| `creature-contracts` | Binding slots (Soul-priced capacity) + loyalty with daily upkeep decay, personality rate modifiers, hard deploy refusal for unbound/insubordinate creatures — **shipped 2026-08-21**, spec in [creatures/spec-creature-contracts.md](creatures/spec-creature-contracts.md); server + web only | creature-core, soul-economy, creature-fusion | shipped |
| `aspect-scope` | **Move element typing off the species and make it a sub-tier.** `CreatureSpeciesDef.ElementPrimary/Secondary` and `TraitPool` move down one level; `CreatureSpeciesGenerator.TraitsFor` gains an `element` argument, so one species yields N aspects with derived trait bias — **generated, never authored**. Strengths/weaknesses need nothing: an aspect's are its element's. **Requested by the class-system program** ([class-system-map.md](class-system-map.md) §2b), which needs the tier as its third allocation scope; owned here because every file it edits is this program's. Spec: [creatures/spec-aspect-scope.md](creatures/spec-aspect-scope.md) — **APPROVED 2026-08-31, authorized to build** (resolving [seedsmith-creatures-ideal.md](seedsmith-creatures-ideal.md) §5 Q2; seedsmith's creatures feature ships an `aspect` kind that needs this tier). **A byte-identical migration path exists** — seed the element salt so each species' own current element reproduces today's trait pool (spec §3.1) | creature-core | **approved 2026-08-31** |
| `creature-capture` | In-run encounters, weaken→capture attempt flow (Hot reads, Cold resolution) | creature-core, creature-contracts, soul-economy | later |
| `creature-fusion` | Star merges (identity-preserving) + discoverable recipes + trait inheritance + capped promotion — **shipped 2026-08-21**, spec in [creatures/spec-creature-fusion.md](creatures/spec-creature-fusion.md) | creature-core, soul-economy, expedition materials | shipped |
| `patron-creature` | Element aura from one designated creature (stars+rarity+level scaled), soul-priced switching, +1 Soul/10 kills — anchors locked 2026-08-21, spec in [creatures/spec-patron-creature.md](creatures/spec-patron-creature.md); SIM half shipped 2026-08-21, LIVE owner gate open | creature-fusion, creature-summoning | SIM shipped |
| `creature-domain-fe` | Web FE: Codex, summon altar, capture UX, fusion lab, contract board (grows out of `#/roster`) | reads all above | incremental |
| `world-events` | Ecology conditions, roaming/boss encounters, raids, factions, release/legacy/lineage | creature-capture, creature-contracts | last |

Build order (revised 2026-09-08; expeditions shipped): `element-extension` → `creature-core` → `progression-source-contract` → (`general-empire-fallback` after species-build allocation transport || `dedicated-progression-isolation` after unique-actor-runtime) → `soul-economy` → `creature-summoning` (V1 internal gate, shipped) → *(standalone program: match-source + expeditions = announced ship, shipped)* → **`creature-fusion`** (duplicate pressure makes it the next sink) → `patron-creature` → `creature-contracts` (shipped) → **`creature-capture`** → `world-events`.

> **Standalone-first program (2026-08-21):** the [standalone RPG map](standalone-rpg-map.md) makes the web RPG the core game and PvZ an extension. Its combined roadmap interleaves with this program: `creature-capture` explicitly becomes the PvZ-mode module (exclusive capture species), and `expeditions` (web battles) becomes the primary consumer of creatures. Where the two maps disagree, the combined roadmap in the standalone map wins.

## Deliberately deferred (not in any v1 module)

Personality-driven in-run AI (Unity-owned), faction kingdoms/diplomacy, creature offspring/breeding mechanics beyond lineage records, prison/garden/market facilities, negotiation outcomes in raids, community discovery sharing.

## Filed by the party-dungeon program (2026-09-05)

| Ask | Module here | Filed by | Shape | Until it lands |
|---|---|---|---|---|
| `CreatureMintSpec.Level` | `creature-core` | `party-dungeon/spec-wild-room.md` §4 | additive `long? Level` on the spec; `RpgStore.Creatures.cs:53` writes `$level = spec.Level ?? 1` — null is today's line for every caller | a recruit or capture mints at level 1 instead of `Θ_room + thetaOffset` |
| `SummonRoller.Roll` optional `poolFilter` | `creature-summoning` | wild-room §6 | a trailing `Func<CreatureSpeciesDef, bool>? poolFilter = null` on `Roll` (`SummonRoller.cs:61`); null = today's pool; `altar.poolFromDomain` stays `false` until it exists | the altar pulls from the whole summonable catalog |
| A personality mint override | `creature-contracts` | wild-room §2 (**ask first**) | the talk's `PersonalityFor("dungeon:wild:{r}:{c}")` recorded on the mint instead of `PersonalityFor(instanceId)` over a fresh `Guid` (`RpgStore.Creatures.cs:45`) | v1 accepts the mismatch |
