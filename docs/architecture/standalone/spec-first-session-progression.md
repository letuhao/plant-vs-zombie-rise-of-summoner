# Spec: first-session progression reveals

**Module id:** `first-session-progression` · **Program:** [../standalone-rpg-map.md](../standalone-rpg-map.md)  
**Depends on:** `match-source-core`, `demon-progression-source`, `species-xp`, `commander-sheet-role`, item ownership/equip  
**Status:** implemented in code; live deploy path is operational, final reward acceptance remains pending

## Purpose

Teach the first three durable facts without creating a second progression system:

1. a resolved lawn victory pays Souls and reveals Crazy Dave's commander sheet;
2. after player level 3, a qualifying general demon run shows empire species XP and automatic primary-stat allocation;
3. after player level 4, Dave receives one real basic equipment reward.

The feature is one authored sequence over the existing lawn-first loop. It does not add a summon,
capture, fusion, world-map, or save-slot loop.

## Locked product contract

### Checkpoints

The server owns three stable checkpoint ids, in this order:

| id | eligibility | grant/reveal |
|---|---|---|
| `first-win-dave` | the first settled **PvZ lawn** `match.result` whose normalized result is `victory` | existing Souls award plus Crazy Dave actor-sheet reveal |
| `level-3-general-species` | player progression is level ≥ 3 **and** the same settled **PvZ lawn** run contains a general spawn with a valid `EmpireGeneral` source claim | authoritative species XP delta, resulting level, and effective automatic primary-stat allocation for the shown species |
| `level-4-dave-equipment` | player progression is level ≥ 4 and `first-win-dave` plus `level-3-general-species` are earned | one real, deterministic basic item owned by the player and assigned to Dave's commander equipment scope |

`victory` is the only success trigger. A defeated, abandoned, stalemated, or merely completed run
does not satisfy `first-win-dave`. The existing normalizer accepts `victory`, `win`, and `won`
(`src/FusionRpg.Core/Activity/PvzActivityKinds.cs:54-66`).
The checkpoint evaluator filters the joined run's `game` through the existing PvZ profile predicate
(`src/FusionRpg.Contracts/Dtos.cs:227-236`); `webrpg-1` and other non-PvZ simulations cannot satisfy
these lawn onboarding checkpoints even if they emit the same lifecycle vocabulary.

The level gates are checks, not XP awards. XP remains in the existing player/species ledgers and
reads tuning data (`src/FusionRpg.Core/Progression/RpgProgression.cs:39-43,111-129`;
`data/tuning/progression.v1.json`). No client literal may decide whether a gate is crossed.

### Ordering and jumps

- Checkpoints are monotonic and per player. A reload, duplicate fact, or browser retry cannot grant a
  second reward.
- A single result may cross several player levels. It may earn only the first-win and species
  checkpoints whose prerequisites are satisfied; the equipment checkpoint waits behind the species
  checkpoint and is returned in the next queue position.
- Reaching level 3 without a qualifying general spawn does not auto-complete the species checkpoint.
  The next run with a valid general source claim is the trigger. Reaching level 4 before the first
  win does not reveal or mint Dave's item early.
- A checkpoint is earned and its durable reward is committed before it can be shown as ready. UI
  acknowledgement only clears presentation state; it never performs the grant.

## Durable state and transaction boundary

Add a Data-owned per-player checkpoint ledger, not browser storage and not an inferred `runs.Count`:

```text
rpg_onboarding_checkpoint(
  player_id, checkpoint_id, state, earned_run_id, reward_ref,
  payload_json, earned_utc, claimed_utc, revision,
  PRIMARY KEY (player_id, checkpoint_id)
)
```

`state` is `earned` or `claimed`; absence means locked/not yet eligible. `reward_ref` identifies an
already-committed item or ledger receipt, never a UI placeholder. The unique primary key is the
idempotency gate.

The same SQLite transaction that settles the source result must:

1. insert the fact/run result;
2. apply existing Souls and player/species XP awards;
3. evaluate newly eligible checkpoints;
4. persist checkpoint rows and their reward receipts;
5. for level 4, mint/acquire/assign Dave's item through one commander-owned item path.

If any required content or owner path is invalid, the transaction fails closed and writes no partial
checkpoint or item. A UI `claim` mutation only changes `claimed_utc` after verifying the row exists.

`Player 1` bootstrap remains the existing `RpgStore.Init` behavior (`src/FusionRpg.Data/Sqlite/RpgStore.cs:60-67,3423-3447`). It must also create an empty checkpoint view for that player without creating fake progression rows; player progression is currently lazy (`RpgStore.Progression.cs:318-365,382-397`).

## Source and progression boundaries

The level-3 encounter is valid only when the activity fact carries a parseable
`demon.progression.v1:general:<speciesId>` claim. The source grammar and fail-closed rules are
defined in [spec-progression-source-contract.md](../demons/spec-progression-source-contract.md).
The species row is the per-player/per-species empire fallback, not an individual demon unlock.

- General spawn → `EmpireGeneral`; receives the empire species progression.
- Unique specimen spawn → `UniqueSpecimen`; receives specimen progression only.
- Commander → `Commander`; receives commander progression only and is never a lawn combatant.

No checkpoint code may infer a source from `typeId`, payload `instanceId`, or a shared species name.
The capture projection now accepts only a matching typed claim (and derives a unique claim from a
persisted owned specimen); missing or contradictory claims are stored as `untrusted` and cannot
award species XP (`src/FusionRpg.Data/Sqlite/RpgStore.cs`). The normal PvZ spawn producer
now carries the typed `EmpireGeneral` claim as `sourceKind`/`sourceId`
transport fields; capture verifies the claim against side/type before applying it. Injector
restore/build and real-game acceptance use the deployed MelonLoader path; final reward acceptance remains
dependent on completing a real victory sequence.

The species reveal reports the actual applied ledger delta and the post-apply allocation. Automatic
allocation is a projection of the species level (`src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocation.cs:15-36`), not a second manually persisted build. The later allocation/respec surface remains optional.

## Dave ownership prerequisite

Crazy Dave is currently a stable commander id (`commander:dave`) returned by the commander list;
he is not a `rpg_unique_actors` row (`src/FusionRpg.Server/CommanderEndpoints.cs:43-82` and
`docs/architecture/commander-surface/spec-commander-sheet-role.md:31-48`). The existing item equip
route resolves only a persistent unique specimen (`src/FusionRpg.Server/ItemEquipEndpoints.cs:307-338`;
`ItemEquipService.TryResolveSpecimen`), while Dave's current player-owned loadout is an aura/action
loadout (`src/FusionRpg.Server/LoadoutEndpoints.cs:15-79`).

The recommended commander-owned scope is now implemented:

`rpg_player_item_assignment` keys `OwnerKind.Player` by player id and the reserved `standard` role;
Dave remains `commander:dave`. The unique-specimen route rejects `standard`, so it cannot fabricate a
Dave specimen or write the commander cell. The fixed `item.first-clear-almanac-seed` container is
minted, owned, assigned, and recorded with the level-4 checkpoint in the settlement transaction.

The onboarding must not call the unique-specimen route with a fabricated Dave instance id, and it
must not present an aura/action as equipment. The first item id, role, catalog entry, and deterministic
reward seed belong in item content/tuning once the owner scope is chosen.

## API and UI contract

Add one read endpoint and one acknowledgement mutation under the player-scoped server API:

```text
GET  /api/onboarding/{playerId}
POST /api/onboarding/{playerId}/checkpoints/{checkpointId}/claim
```

The GET response contains the ordered checkpoint state, authoritative reward payloads, current player
level, and a `revision`. It is safe to poll and rehydrate after reload. Claim is idempotent and refuses
unknown, locked, or already-claimed ids by named reason.

The FE renders a single current reveal over the current stage (Sanctum or the settled run result),
never a new top-level route. It must show loading/error/retry, never predict values, and keep later
checkpoints absent until their prerequisites are earned. Crazy Dave opens the existing commander actor
sheet; the species reveal opens the existing species/progression surface; the equipment reveal opens the
same Dave sheet after the item assignment is confirmed.

The old `FirstRunReveal` sunflower/bind component is not this flow. It is legacy UI and must be removed
or explicitly placed before this sequence in the implementation plan; it may not silently claim the
`first-win-dave` checkpoint.

## Live-test acceptance

Use a fresh SQLite directory and the real server/injector path. Record the player id, run id,
checkpoint response, Souls ledger, XP ledger, species row, allocation, item row, and assignment row.

1. Start with no database; `/api/players` returns Player 1 and the title Continue path reaches Sanctum.
2. Complete one real lawn victory. The result creates exactly one victory Soul ledger receipt for
   that fact and `first-win-dave=earned`; retrying the result changes neither receipt count nor
   checkpoint state. Later PvZ victories may still pay their normal Soul policy amount.
3. Run until player level 3, then field a known ordinary species. The fact shows an explicit general
   source; the species checkpoint reports the real XP/allocation result. A unique extra spawn in the
   same run does not alter the empire species row.
4. Reach level 4. Dave's checkpoint mints exactly one real item in the selected commander owner scope,
   survives server restart, and is visible on the commander sheet. Repeating the settled result does not
   mint or assign another item.
5. Kill/reload the browser at every reveal. The next GET returns the same ordered unclaimed queue;
   claiming once is enough and claiming again is a replay, not a second grant.

The Dave ownership prerequisite, concrete item path, species event, onboarding API, and checkpoint
transaction are implemented in the server/Data slice. The deployed server now imports the atom corpus
without attempting to parse dungeon-specific envelopes, and `lawn/quick-start` returns a live board with
target and plant pointers. The remaining acceptance work is to drive a real lawn victory and verify the
three durable checkpoints; no game binary is patched and no HP polling is used as onboarding evidence.
The simulator E2E path now drives the same `match.result` settlement and verifies the persisted first-win
and level-3 checkpoints without depending on a PVZ window; the focused Data harness covers the level-4
item transaction and replay. A real victory window remains optional smoke coverage rather than a release
gate.
The 2026-09-09 live probe also observed ordinary zombie spawns carrying the typed
`demon.progression.v1` / `general:normalzombie` source claim, followed by the scenario completion and
board snapshot events.
Existing BattleEngine death attribution is orthogonal: it supports
unique specimen lawn XP and must not be used as a substitute for general-source onboarding evidence.

## Design-gate checklist

- [x] Subsystems identified: player bootstrap, activity/progression, demon source selection, commander
      sheet/item ownership, and stage UI.
- [x] Required architecture, product-guide, and UI documents were read this session; decisions were
      checked before writing this spec.
- [x] Claims above are tied to current code or named specs.
- [x] No new loop, private XP curve, client authority, or combat stat writer is introduced.
- [x] Full implementation conformance is shipped for checkpoint schema/API, source classifier cleanup,
      commander item scope, concrete content, and in-stage reveal queue.
- [x] Simulator settlement, ordered checkpoint replay, and level-4 item durability are covered; live
      deploy-play smoke confirms injector connectivity and typed spawn telemetry. A real victory window is
      optional environment smoke, not a release gate; no game binary is patched and no HP polling is used.
