# Spec: lawn-deploy-progression (`demon-lawn-deploy` module 2)

**Status:** approved 2026-09-08; specification only.  
**Depends on:** `lawn-deploy-core`, [progression-source-contract](../demons/spec-progression-source-contract.md).  
**Decision:** [decisions.md](../decisions.md) — *Unique lawn XP receipts (2026-09-08)*.

## Objective

Make a lawn-deployed unique demon grow through the thing it uniquely does: fighting as an individual specimen. A Bound specimen earns its own XP for enemy kills it is proven to have made and for active time it survives on the lawn. These rewards use the specimen XP curve and their own tunable award values; they are not a multiplier or a proxy for general-demon species progression.

The player benefits from keeping a unique demon alive and using it effectively, while the general demon system remains the empire-wide fallback for disposable populations.

## Scope

This module adds the durable, Cold projection for a lawn `UniqueSpecimen` source:

- capture and validate `killerPtr` attribution for plant and zombie deaths;
- create a unique lawn binding session only after the normal deploy pipeline has reached Bound;
- settle specimen kill XP and active-Bound-duration XP through one idempotent transaction;
- preserve the existing specimen level/Xp cost ladder and its downstream action-unlock handling;
- expose a player-readable XP receipt later through the existing specimen/actor-sheet surfaces, not a new lawn route.

It does not change the deploy trigger cadence or cost (`lawn-deploy-events` owns that), create a new Unity prefab, award Souls, change general demon species XP, make a Commander or Patron deployable, or determine a unique allocation plan. It consumes the source-isolation contract; it does not reopen its source variants.

## Source and eligibility contract

Every lawn XP candidate must carry a parsed `DemonProgressionSource.UniqueSpecimen(playerId, instanceId)`. A candidate is eligible only when the following all hold:

1. The specimen is owned by `playerId`, and Data has produced the canonical `UniqueSpecimen(playerId, instanceId)` claim from its row plus the accepted deploy correlation. An echoed `instanceId`, `source="extra"`, type, or side is not a source claim.
2. Its normal deploy has progressed `Roster → Deploying → ActiveBound`, and a durable lawn-binding session exists for the active run/correlation and binds its `instanceId` to the ptr observed at Bound.
3. For a kill, the death event has a replay-stable per-match lifecycle occurrence id and a `killerPtr` captured from the same verified fatal interaction. That ptr resolves to the same still-open binding and the target was an opposing side. A cached “last attacker,” bullet type, or inferred owner is not evidence.
4. For participation, the ending death or match-result fact has a replay-stable lifecycle occurrence id and closes that same binding with an injector-emitted active-match timestamp.

Missing `killerPtr`, lethal provenance, lifecycle occurrence id, an unambiguous open binding, source claim, ownership match, opposing target, or active-match timestamp is a no-award diagnostic. Neither `typeId`, species id, side alone, `source="extra"`, a cached last attacker, nor server arrival time may repair missing attribution. A projectile or indirect-damage path is eligible only after a hook probe proves its causal attacker bridge; otherwise it emits no unique-kill candidate.

## Runtime and persistence flow

```text
Deploy intent
  → existing PendingSpawn
  → spawn capture / ack
  → Bound(instanceId ↔ ptr) + durable lawn-binding session

Enemy death(occurrenceId, verified killerPtr, activeMatchMs)
  → resolve open binding by ptr and run
  → insert kill receipt atomically
  → AwardUniqueActorXpUnlocked in the same transaction

Specimen death or match.result(occurrenceId, activeMatchMs)
  → close its binding session
  → whole active intervals × specimenBoundIntervalXp
  → insert participation receipt atomically
  → AwardUniqueActorXpUnlocked in the same transaction
```

The Injector adds a per-match monotonic `lifecycleOccurrenceId` to every relevant lifecycle record, preserves it on transport retry, and emits a causal `killerPtr` only when the actual fatal interaction proves it. It also emits a monotonic `activeMatchMs` for bind, death, and match-result lifecycle records. `activeMatchMs` measures Unity scaled active match time and therefore excludes pause time. The injector remains SQLite-free: it records facts and returns. The Server/Data projection performs the lookup and durable write after capture. The current ptr-only death dedupe (`PvzActivityKinds.cs:35-47`) is insufficient because a reused ptr would suppress a later death; the implementation must use `lifecycleOccurrenceId` to locate the canonical activity fact on both first delivery and replay.

Current evidence makes this addition necessary: `EffectEventDto` can carry `killerPtr` (`src/FusionRpg.Contracts/EffectDtos.cs:161`), but the current `zombie.die` payload omits it (`src/FusionRpg.Injector/GameHooks.cs:1032-1041`). `RpgStore.Project` records a death but has no unique-kill projector (`src/FusionRpg.Data/Sqlite/RpgStore.cs:2617-2621`).

### Durable records

Add two Data-owned tables; no caller outside `FusionRpg.Data` writes either one.

| Record | Required identity | Purpose |
|---|---|---|
| `rpg_unique_lawn_bindings` | generated `binding_id`; `instance_id`, `player_id`, `run_id`, `correlation_id`, typed source, ptr, Bound fact | One Bound deployment session. Require `UNIQUE(instance_id, run_id, correlation_id)`, `UNIQUE(run_id, correlation_id)`, and partial uniqueness for an open `(run_id, ptr)`. A same-run redeploy creates a new correlation session. |
| `rpg_unique_lawn_xp_receipts` | `binding_id`, `award_kind`, `activity_fact_id` with a unique constraint | Atomic replay guard and audit row for exactly one kill or participation award derived from one canonical captured fact. Store run, correlation, immutable delta, active duration, target/terminal identity, and timestamp alongside the key. |

The unique constraint is the first idempotency mechanism, not a preceding `SELECT`. On a conflict, Data reads the existing receipt and compares every immutable identity and award field: an exact replay is a no-op, while a different binding, run, correlation, source fact, delta, target, duration, or terminal payload is a data-integrity error. Receipts are retained with the specimen history; no TTL can be shorter than event replays or compaction recovery.

The existing `rpg_xp_ledger` keys actor-kind/type-id progress and cannot safely represent a durable `instanceId`; do not overload it. `AwardUniqueActorXpUnlocked` remains the single specimen-XP mutator and runs in the same transaction as the receipt. When it reports gained levels, the projector calls the same transactional action-unlock path used by expedition settlement; it does not silently omit a specimen's normal level-gain effects.

The binding creation/close, receipt comparison/insert, XP mutation, level-gain unlocks, and the existing `ActiveBound → Roster` recovery are one Data transaction rooted in the capture projector. The current post-capture `ObserveUniqueActorEvents` recovery cannot be left as a separate settlement path for these events: a crash between recovery and reward would otherwise lose or misattribute participation XP. Server-side atom-push notification happens only after that transaction commits.

## Reward policy and tuning

The existing `xpCurve.specimen` in `data/tuning/progression.v{n}.json` remains the specimen's arithmetic XP cost ladder. Add these separate whole-`long` award parameters to the next published progression tuning version:

| Key | Unit | Meaning |
|---|---|---|
| `awards.specimenLawnKill` | XP per eligible enemy death | Unique combat contribution. |
| `awards.specimenBoundIntervalMs` | active-match milliseconds | Duration of one participation interval. |
| `awards.specimenBoundIntervalXp` | XP per completed interval | Survival/field-presence reward. |

The duration award is `floor(boundActiveMs / specimenBoundIntervalMs) × specimenBoundIntervalXp`, calculated once when the binding closes. The parser rejects `specimenBoundIntervalMs <= 0`, `specimenLawnKill <= 0`, and `specimenBoundIntervalXp <= 0`; zero or a negative value is invalid tuning, not a disabled feature. Widen before multiplication and use `checked`; all XP, elapsed milliseconds, and receipt deltas are `long`, and overflow throws. The interval is a balance cadence, not a persistence poll. There is no per-run, lifetime, or level cap.

These keys are intentionally separate from `data/tuning/species-progression.v{n}.json` and its placement/run-completion awards. A tuning pass can make unique lawn advancement faster or slower without changing general-demon empire growth.

## ActorHub and live-combat boundary

This module produces durable XP only. It does not compose combat stats, mutate a Unity actor, add a private level-to-power formula, or add a `ContributionSourceId`. The existing specimen level may be consumed by the approved dedicated progression resolver; any actor-derived result still goes through ActorHub.

A level-up never waits in the hit/death hook and never pushes a direct live stat write. It is observed Cold and takes effect through the existing safe rehydrate or a later deployment. This is deliberate: the hit path remains Injector-local and event capture remains record-then-drain.

## Project structure and code style

```text
src/FusionRpg.Injector/...                  # lifecycle capture: killerPtr + activeMatchMs only
src/FusionRpg.Contracts/...                 # additive lifecycle payload fields if needed
src/FusionRpg.Data/Sqlite/...               # binding/receipt DDL and one transactional projector
src/FusionRpg.Core/Progression/...          # typed tuning parsing and named award reasons
data/tuning/progression.v{n}.json           # published values; never hand-edited
tests/FusionRpg.{Core,Data,Injector}.Tests/ # unit, transaction, and capture coverage
```

Use the existing result-record style for expected refusals and a closed source switch before progression is calculated:

```csharp
if (source is not DemonProgressionSource.UniqueSpecimen unique)
    return UniqueLawnXpProjection.NoAward("source.not_unique_specimen");

return receipts.TryClaim(unique.InstanceId, AwardKind.Kill, activityFactId)
    ? AwardUniqueActorXpUnlocked(db, unique.InstanceId, delta)
    : UniqueLawnXpProjection.Replay();
```

The schema change is an implementation-time **Ask first** boundary. It must be additive, use Data's migration conventions, and update reset/storage cleanup in the same change.

## Testing strategy

```powershell
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Injector.Tests
dotnet test tests/FusionRpg.Server.Tests
dotnet test tests/FusionRpg.Guard.Tests
```

- Injector: a real lifecycle death payload carries the causal killer ptr and active-match ms; an unknown causal killer stays absent rather than guessed.
- Injector: an unverifiable, indirect, or stale attacker produces no `killerPtr`; every eligible lethal death has a replay-stable occurrence id.
- Data: Bound creates one session; correlation or open-ptr collisions are refused; the same correlation replay does not make another; a later redeploy gets a new session.
- Data: an attributed enemy kill gives exactly one unique award; duplicate event delivery resolves to the same fact and receipt; ptr reuse gets a distinct fact; a same-species general and a unique do not cross-credit.
- Data: participation pays completed active intervals on specimen death and match end; paused time, missing timestamps, malformed bindings, and a second terminal fact pay nothing.
- Data: receipt insertion, XP mutation, level-gain unlocks, binding close, and roster recovery roll back together on a failure; an existing source fact with conflicting receipt identity is refused.
- Core: zero or negative specimen lawn tuning is rejected before it can reach arithmetic.
- Regression: no unique lawn award writes `RpgActorKinds.Species`, and no lawn award waits on a Server call from the Injector Hot path.

## Boundaries

- **Always:** source-validate against the Bound binding, use active-match time, use atomic receipts, and route specimen XP through the existing unique-XP mutator.
- **Ask first:** changing the two-table durable receipt design, adding a client-facing XP-claim endpoint, changing the specimen XP curve shape, or adding a new live ActorHub contribution.
- **Never:** infer killer ownership from `typeId`/species; count paused/server wall-clock time; award species XP; let a Commander/Patron deploy; run SQLite or a server round-trip in the hit path; silently clamp XP/duration overflow.

## Success criteria

- [ ] A Bound unique with a verified kill earns exactly its configured specimen kill XP once.
- [ ] A unique earns duration XP only from its own completed active-Bound intervals.
- [ ] General and unique actors sharing a species never share a lawn XP faucet or progression row.
- [ ] Duplicate capture/replay, ptr reuse, and same-run redeploy preserve exact-once rewards.
- [ ] A specimen level-up remains Cold and reaches lawn combat only through the existing rehydrate/deploy and ActorHub path.

## Design-gate checklist

- [x] Read the design gate, demon-system map, lawn-deploy map/specs, unique runtime, match runtime, event/control-loop, data, stat/ActorHub, tuning, and power-scale documentation in this session.
- [x] Verified lifecycle, source, XP, and death-attribution claims against the cited code locations.
- [x] Recorded the new behavior lock in `decisions.md` before this specification.
- [x] Kept reward values out of code and named their configuration units.
- [x] Identified implementation and verification commands.
- [ ] No constraint test was run: this is a documentation-only specification.
