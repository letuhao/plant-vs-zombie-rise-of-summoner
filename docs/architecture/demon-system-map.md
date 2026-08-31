# Capability map: Demon gameplay system

Source vision: the demon gameplay design note (external; its **ideals** are adopted, its architecture is not — everything below builds on the shipped overlay stack). Status: **approved 2026-08-21** (owner resolved the three shaping decisions below). Module specs live in [demons/](demons/), one per module id, written in dependency order.

## Resolved decisions (2026-08-21)

1. **What a deployed demon is:** both modes, chosen per species — most demons deploy as **plant-side avatars** (empowered unique plants carrying the demon's traits/element as effect grants); designated boss-class species deploy as **hypno-zombie allies**. Capture/deploy modules spec the details later; hypno mode inherits the MatchRuntime hypno-fold caveats.
2. **Elements:** the ElementHub ring is **extended** (new prerequisite module `element-extension`); `void`/`chaos` from the vision stay traits, not elements. Requires a decisions.md amendment + matchup golden tests before demon typing lands.
3. **V1 slice:** `element-extension` → `demon-core` → `soul-economy` → `demon-summoning` (gacha). Capture, contracts depth, fusion, FE domain, world events follow.

## Design rules adopted (the "ideal")

1. Gacha never replaces gameplay; every acquisition method feels different.
2. Duplicates and low-rarity demons keep value (fusion material, trait donors).
3. Fusion creates build possibilities, not just bigger numbers.
4. Demons are individuals: personality, loyalty, history, lineage — not equipment.
5. The world contains demons worth hunting; rare discoveries feel earned ("I earned this capture").
6. Target story: found in the world → barely won → contracted → trained → fused → inherited a trait → evolved → raid-carried → strongest army member. Systems, not scripts.

## Mapping the vision onto the shipped stack

| Vision concept | Lands on (existing) | Notes |
|---|---|---|
| Demon (individual) | **UniqueActor specimen** (`instanceId`, phase FSM, equip, XP) | Extended with rarity, variant, traits, contract state |
| Demon species / Codex | `types` catalog + almanac dumps + new discovery flags | Codex = almanac FE with discovery states |
| Traits / skills | **Foundation Effects grants** (EffectBag templates per specimen) | Trait = grant template; inheritance = template transfer |
| Elements | **ElementHub** (locked ring: fire/ice/air/earth + omni) | Doc's 8 elements map onto the locked roster (see Q2) |
| Capture condition (weakened, statused) | Live HP + **StatusRuntime** instances at attempt time | Read via MatchRuntime/board snapshot — Hot-plane data |
| Deploy / fight | **MatchRuntime** UniqueBindings + `pvz.*` Intent | Same Admit → PendingSpawn → Bound path as today |
| Souls | New server-side ledger driven by **PvzActivity** facts | Same append+watermark pattern as XP |
| Contracts / loyalty / personality | New Cold-plane state on the specimen (Server + Data) | Affects deploy-time checks and overlay decisions, never Unity AI |
| Fusion | New roster-level system (distinct from plant mixing) | Consumes specimens, mints a new one; recipes discoverable |
| Ecology / blood moon / roaming | Run modifiers + encounter injection via Intent (`spawn.extra`) | "Exploration" is reinterpreted — see Q3 |
| Summoner's Domain | Web FE screens (`#/domain`, evolving `#/roster`) | FE-only until facilities earn server state |

**Hard constraints carried over:** in-run demon behavior is Unity-owned — personality influences *overlay* decisions (obedience checks, deploy gating, effect grants), never zombie pathing/AI. All combat mutation stays on the Funnel/Writer path. No server round-trip on the hit path: capture *resolution* is Cold, capture *conditions* are read Hot.

## Modules

| Module id | Responsibility | Depends on | Wave |
|---|---|---|---|
| `element-extension` | Extend the ElementHub roster + matchup matrix (light/dark); decisions.md amendment; golden tests | — | **V1** |
| `demon-core` | Specimen identity superset: species link, rarity, variants, trait slots, element typing, Codex discovery state | element-extension | **V1** |
| `soul-economy` | Souls ledger: earn rules from Activity facts, spend API, balances | demon-core | **V1** |
| `demon-summoning` | Summoning/gacha: banners, Souls-funded pulls, rarity/variant/trait rolls, mint specimens | demon-core, soul-economy | **V1** |
| `demon-contracts` | Binding slots (Soul-priced capacity) + loyalty with daily upkeep decay, personality rate modifiers, hard deploy refusal for unbound/insubordinate demons — **shipped 2026-08-21**, spec in [demons/spec-demon-contracts.md](demons/spec-demon-contracts.md); server + web only | demon-core, soul-economy, demon-fusion | shipped |
| `aspect-scope` | **Move element typing off the species and make it a sub-tier.** `DemonSpeciesDef.ElementPrimary/Secondary` and `TraitPool` move down one level; `DemonSpeciesGenerator.TraitsFor` gains an `element` argument, so one species yields N aspects with derived trait bias — **generated, never authored**. Strengths/weaknesses need nothing: an aspect's are its element's. **Requested by the class-system program** ([class-system-map.md](class-system-map.md) §2b), which needs the tier as its third allocation scope; owned here because every file it edits is this program's. Spec: [demons/spec-aspect-scope.md](demons/spec-aspect-scope.md) — **APPROVED 2026-08-31, authorized to build** (resolving [seedsmith-demons-ideal.md](seedsmith-demons-ideal.md) §5 Q2; seedsmith's demons feature ships an `aspect` kind that needs this tier). **A byte-identical migration path exists** — seed the element salt so each species' own current element reproduces today's trait pool (spec §3.1) | demon-core | **approved 2026-08-31** |
| `demon-capture` | In-run encounters, weaken→capture attempt flow (Hot reads, Cold resolution) | demon-core, demon-contracts, soul-economy | later |
| `demon-fusion` | Star merges (identity-preserving) + discoverable recipes + trait inheritance + capped promotion — **shipped 2026-08-21**, spec in [demons/spec-demon-fusion.md](demons/spec-demon-fusion.md) | demon-core, soul-economy, expedition materials | shipped |
| `patron-demon` | Element aura from one designated demon (stars+rarity+level scaled), soul-priced switching, +1 Soul/10 kills — anchors locked 2026-08-21, spec in [demons/spec-patron-demon.md](demons/spec-patron-demon.md); SIM half shipped 2026-08-21, LIVE owner gate open | demon-fusion, demon-summoning | SIM shipped |
| `demon-domain-fe` | Web FE: Codex, summon altar, capture UX, fusion lab, contract board (grows out of `#/roster`) | reads all above | incremental |
| `world-events` | Ecology conditions, roaming/boss encounters, raids, factions, release/legacy/lineage | demon-capture, demon-contracts | last |

Build order (revised 2026-08-21; expeditions shipped): `element-extension` → `demon-core` → `soul-economy` → `demon-summoning` (V1 internal gate, shipped) → *(standalone program: match-source + expeditions = announced ship, shipped)* → **`demon-fusion`** (duplicate pressure makes it the next sink) → `patron-demon` → `demon-contracts` (shipped) → **`demon-capture`** → `world-events`.

> **Standalone-first program (2026-08-21):** the [standalone RPG map](standalone-rpg-map.md) makes the web RPG the core game and PvZ an extension. Its combined roadmap interleaves with this program: `demon-capture` explicitly becomes the PvZ-mode module (exclusive capture species), and `expeditions` (web battles) becomes the primary consumer of demons. Where the two maps disagree, the combined roadmap in the standalone map wins.

## Deliberately deferred (not in any v1 module)

Personality-driven in-run AI (Unity-owned), faction kingdoms/diplomacy, demon offspring/breeding mechanics beyond lineage records, prison/garden/market facilities, negotiation outcomes in raids, community discovery sharing.
