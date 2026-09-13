# First-session progression reveals

**Status:** Confirmed product direction; audited 2026-09-08. The implementation contract is
[spec-first-session-progression.md](../architecture/standalone/spec-first-session-progression.md).
This brief is not implementation authorisation by itself.

## Audit verdict

The three-beat order is coherent and should stay. The idea was not yet live-testable, however:

- The existing first-session UI still implements the old sunflower bind beat, while
  `docs/design/07-flows.html:276-303` still specifies sunflower, elemental, and relic beats. That
  flow is now legacy and must be retired or explicitly placed before this sequence; it cannot claim
  the first-win checkpoint.
- Crazy Dave is a stable commander view, not a persistent unique actor
  (`src/FusionRpg.Server/CommanderEndpoints.cs:43-82`). The existing item equip route only accepts
  persistent unique specimens (`src/FusionRpg.Server/ItemEquipEndpoints.cs:307-338`), so “Dave's first
  equipment” has no valid owner/write path yet. The reward must not be faked with an aura loadout or a
  fabricated specimen id.
- Checkpoints have no durable table, API, or atomic reward receipt. `Player 1` is seeded, but player
  progression is lazy (`src/FusionRpg.Data/Sqlite/RpgStore.cs:3423-3447`;
  `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:318-397`). A browser-only flag would replay or lose
  rewards after reload.
- The capture projection now fails closed for missing or contradictory source claims and the mapper
  ignores payload `instanceId`/opaque source tokens. The remaining gap is producer-side: normal PvZ
  spawns still need to emit a typed `EmpireGeneral` claim before the level-3 checkpoint can be live.
  The source contract says the spawn mechanism must declare `EmpireGeneral`/`UniqueSpecimen`/
  `Commander`; type id alone is not authority (`src/FusionRpg.Data/Sqlite/RpgStore.cs:1678-1734`).
- The ordinary species, level-pacing proof, Dave item id/slot, and live reset/replay script are not
  selected. Until those are fixed, a “live test” can only prove transport, not the product idea.

The corrected contract fixes these gaps without adding a new loop: one per-player checkpoint ledger,
settled-victory trigger, source-validated species evidence, ordered idempotent queue, and an explicit
commander-item ownership prerequisite.

## Problem statement

How might we teach a new player that Rise of Summoner is a lawn-first RPG with durable progression, without presenting unique creatures, equipment, species builds, or the world map all at once?

## Recommended direction

The game has one automatically persisted local profile, not a player-facing save-slot flow. When the local SQLite database is empty, the server creates and selects `Player 1`; the title's Continue action can therefore enter the Sanctum directly. This bootstrap already exists in `RpgStore` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:60-66`, `3302-3315`). The current title/save-selection UI is legacy presentation to be aligned in a later implementation slice.

The first-session learning order is deliberately narrow:

1. **First win — Crazy Dave.** The player earns Souls and sees Crazy Dave's Commander actor sheet. Dave is the first proof that the game has durable actors and a progression home, not merely a lawn overlay.
2. **Player level 3 — general creature species progression.** A non-unique general creature appears in a lawn run. After the authoritative run result, the player sees that species earn XP, level up when the result warrants it, and receive its automatically distributed primary-stat allocation.
3. **Player level 4 — Dave's first equipment.** Dave receives one real basic piece of commander equipment. The reveal leads to Dave's existing sheet; it is not a separate inventory tutorial.

The level-3 beat introduces **empire-scoped species progression**, not an individual creature unlock. Every general creature of the same species belonging to the player's empire reads the same per-player, per-species progression and automatic allocation. The player initially experiences this as “this species grows wherever it fights.” The world map may later reveal the empire-wide reach through legions and other populations, but it is not introduced in this onboarding. The encounter must be emitted as a durable `EmpireGeneral` source claim, so the reveal teaches the real fallback rather than accidentally rewarding a same-`typeId` unique creature.

The rule is a **fallback selected by the gameplay's spawn mechanism**, not a bonus inferred from a creature's type. A player-empire general creature with no dedicated progression source uses empire species progression. Unique creatures use their own specimen progression and deployment/acquisition flow; Commanders use Commander progression. Neither unique creatures nor Commanders also receive the empire species fallback. This keeps several spawn mechanisms and power scales legible without turning them into competing buffs on the same actor.

This matches the existing species-build direction: the durable allocation is keyed by player and species, while the computed baseline is driven by that species' level (`docs/architecture/species-build-map.md:71-74`; `src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocation.cs:15-36`). The existing lawn progression path already deduplicates each fielded species per run and records the award under the player and species (`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:73-115`).

## Player journey

```text
Empty SQLite
  → Player 1 is created and selected automatically
  → Sanctum / first lawn win
  → Souls + Crazy Dave actor-sheet reveal
  → Player level 3 lawn run
  → general-creature species XP, level, and auto primary-stat reveal
  → Player level 4
  → Dave's first basic equipment on his actor sheet
  → normal Sanctum and lawn progression
```

The result sequence is server-authoritative and persisted in SQLite. The frontend may acknowledge a completed run immediately, but it must not predict XP, a level-up, an allocation, or an item before the authoritative result arrives. This follows the UI authority rule in `docs/architecture/game-gui-principles.md:260-278`. Checkpoint application is monotonic and idempotent: if one settled result crosses more than one player-level gate, the server records each earned checkpoint once and returns the reveal queue in learning order (species lesson before Dave's equipment), rather than skipping or racing the intermediate lesson.

## Scope rules

- A **general creature** is species-level, non-unique, and has no persistent individual identity; unique creatures have their own specimen identity and equipment. The distinction is documented in `docs/architecture/creature-scope-ideal.md:43-55`.
- Species progression is **per player / empire and per species**. It buffs all applicable general creatures in that player's empire; it is not a separate level for every spawn.
- The spawn mechanism declares the progression source. A catalog `typeId` identifies a species but may not decide that a unique creature or Commander uses the empire fallback.
- The level-3 lawn event is eligible for species XP only when its durable activity provenance parses as `EmpireGeneral`; generic run-completion logic must not infer eligibility from a spawn's `typeId`.
- A unique creature uses specimen progression and its own deploy/acquisition mechanism; a Commander uses Commander progression. Neither combines that source with empire species progression.
- Crazy Dave is a Commander surface, not a creature deployment flow. His canonical drill-in is the Commander actor sheet (`docs/architecture/commander-surface/spec-commander-sheet-role.md:19-48`).
- Player levels 3 and 4 are the unlock gates in this concept. XP amounts and thresholds remain tuning data, never onboarding literals.
- The onboarding is authored and persisted. It is not inferred only from a browser session or displayed as an empty normal Sanctum state, per GG-43 (`docs/architecture/game-gui-principles.md:588-597`).
- Reveals use the existing game-stage layer model: the Sanctum and lawn remain stages; actor sheets and progression inspection open over them; a level-up or reward can use a blocking dialog band (`docs/architecture/game-gui-principles.md:34-63`).

## Key assumptions to validate

- [ ] A level-3 lawn encounter can associate the shown general creature with the current player's empire without inventing a unique specimen, and passes an explicit progression-source classification instead of relying only on `typeId`.
- [ ] Player-level gates that are crossed together persist and return every unclaimed reveal in their authored order; reload, duplicate result delivery, and a skipped browser acknowledgement cannot mint Dave's item twice or suppress the level-3 lesson.
- [ ] The configured species XP award for that encounter reliably produces a meaningful reveal. The implementation must display the server result, not assume a specific XP threshold in the client.
- [ ] A commander-owned item scope (or an explicitly approved persistent Dave actor) exists, with a real
      item catalog entry, `standard` role, mint/acquire/assign transaction, and sheet projection. The
      current unique-specimen equip route is not sufficient.
- [ ] The onboarding checkpoint survives reload, retry, and interrupted runs without replaying a reward. Its persistence belongs in the Data layer because SQLite access is owned exclusively there (`docs/architecture/data-architecture.md:104-110`).

## MVP scope

- Retain the existing automatic `Player 1` bootstrap and remove save-slot choice from the normal first-player path.
- Persist first-session checkpoints and unlock state in SQLite.
- Reveal Dave's Commander actor sheet after the first win, with the real Souls and player-progression result.
- At player level 3, introduce one general creature species on the lawn and show the authoritative species-progression and automatic-primary-stat result.
- At player level 4, award one real basic item for Dave and open the relevant existing sheet state.
- Keep the Sanctum's first-run path focused: one current instruction and absent future complexity, rather than a fully populated but disabled shell (`docs/design/information-architecture.md:67-76`).

## Not doing

- **Unique creature unlocks, summoning, fusion, capture, or a creature roster in onboarding** — those add an individual-ownership model before the player understands shared species growth.
- **World map, legions, or empire-management UI** — the species buff is empire-scoped from the start, but its broader strategic presentation waits for world-map unlock.
- **Empire species fallback on unique creatures or Commanders** — their dedicated progression sources remain separate, even when they share a species catalog entry with a general creature.
- **Manual species allocation, respec, or build editing** — level 3 only shows the automatic primary-stat result. The later allocation surface is an optional override layer, not a first-session obligation (`docs/architecture/species-build/spec-allocation-surface.md:1-55`).
- **First-run equipment** — Dave's equipment waits until player level 4, after the species-progression lesson.
- **A second save system, cloud profiles, or browser-local tutorial authority** — one local SQLite profile is the product model.

## Resolved implementation rules and remaining gates

- **Checkpoint trigger:** first settled **PvZ lawn** `match.result` with normalized `victory`; defeat,
  stalemate, abandoned runs, and `webrpg-1`/other non-PvZ simulations do not count. The existing
  Soul policy may still pay later victories; “first” applies to the onboarding checkpoint only.
- **Level jumps:** gates are evaluated after the transaction applies XP. A jump never skips queue order;
  level 3 still needs a qualifying general spawn, and level 4 waits behind the first two checkpoints.
- **State:** Data-owned `rpg_onboarding_checkpoint` rows with a unique `(player_id, checkpoint_id)`;
  reward minting and checkpoint insertion commit atomically with the settled result.
- **Species content:** choose one catalog-known ordinary species and one real lawn spawn mechanism, then
  record its source claim in the live-test fixture. No type-id-only fallback is accepted.
- **Dave item:** choose the item and `standard` role only after the commander ownership scope is approved.
- **Title:** Continue should enter Sanctum for the auto-seeded Player 1; save-slot management remains an
  optional legacy surface, not a prerequisite for the normal first-player path.
- **Live-test gate:** do not call the idea complete until the four-step acceptance in the implementation
  spec passes from a fresh SQLite directory, including restart and duplicate-result replay.

## Design-gate evidence

- Subsystems covered: player bootstrap and SQLite persistence; onboarding and player-visible UI; Commander actor sheet; species progression and automatic allocation.
- Read this session: `docs/architecture/software-architecture.md`, `docs/architecture/decisions.md`, `docs/guide/the-game.md`, `docs/guide/the-loops.md`, `docs/architecture/data-architecture.md`, `docs/architecture/game-gui-principles.md`, `docs/design/information-architecture.md`, `docs/design/README.md`, `docs/architecture/fe-game-foundation.md`, `docs/architecture/species-build-map.md`, `docs/architecture/species-build/spec-species-xp.md`, `docs/architecture/species-build/spec-allocation-surface.md`, `docs/architecture/creature-scope-ideal.md`, and `docs/architecture/commander-surface/spec-commander-sheet-role.md`.
- Code checked: automatic player bootstrap (`RpgStore.cs:60-66`, `3302-3315`), per-player species awards (`RpgStore.Progression.cs:73-115`), and level-derived automatic allocation (`PointBudget.cs:30-40`; `SpeciesAllocation.cs:17-36`).
- No test was run: this brief changes no production code and reports no test-dependent constraint.
