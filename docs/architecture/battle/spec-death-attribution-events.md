# Spec: battle-death-attribution-events

Module id `battle-death-attribution-events` in the [battle-timeline capability map](../battle-timeline-map.md). It extends the server-owned `BattleEngine` report contract; it does not change the PvZ injector contract.

**Status:** approved 2026-09-08; implementation landed in Core with focused coverage.

## Objective

Attach authoritative killer identity to existing `plant.die` and `zombie.die` report events produced by server-owned battles. This gives web and standalone battles a deterministic source for unique-creature kill rewards without asking the injector to infer or capture a deferred Unity death.

Success means that a lethal interaction resolved by `BattleEngine` carries the attacker's stable actor key through `BattleEventRec` and the existing `BattleReportEmitter` maps it to the report's synthetic `web:{matchKey}:{n}` pointer. Attackerless deaths remain explicitly unattributed.

The implementation keeps killer identity out of the battle determinism golden hash: it is report
provenance, not combat math. Full report serialization remains byte-deterministic and dedicated
engine/emitter tests cover the identity and pointer mapping.

## Assumptions

1. `BattleEngine` is authoritative only for server-owned web, expedition, and other standalone battles.
2. Live PvZ lawn runs remain Unity-authoritative for HP and lifetime. Their captured `plant.die`/`zombie.die` event is not replaced or duplicated by this module.
3. Unique-creature XP settlement consumes an attributed report event through the existing receipt transaction; this module does not add a second XP ledger or a new progression curve.
4. The existing event kinds remain the vocabulary. `killerPtr` is an additive payload field, not a new event kind.

## Contract

### Internal battle event

Extend `BattleEventRec` with an optional `KillerActorKey` after the existing shield fields so current constructors remain source-compatible.

- A death caused by an attacker during the same resolved hit carries that attacker's `BattleActorSetup.Key`.
- Guardian-share deaths carry the attacker that caused the hit, not the guardian victim.
- Deaths discovered by a later sweep, status/environment damage without explicit attacker context, retreat cleanup, or other attackerless paths carry `null`.
- A future damage path may set a killer only when it passes an explicit attacker context into death adjudication. Last-attacker caches, event arrival time, or target type are never substitutes.

The existing `BattleRunState` death tally and `BattleEventRec` ordering stay unchanged. The killer key is metadata on the already-recorded lifecycle occurrence, not a second death occurrence.

### Emitted report event

`BattleReportEmitter` keeps emitting the existing `plant.die` or `zombie.die` event. When `KillerActorKey` is non-null, it resolves that key through the same stable `ptrByKey` map used for actor payloads and adds:

```json
{
  "ptr": "web:match-123:4",
  "type": 10001,
  "source": "web",
  "reason": 0,
  "round": 3,
  "killerPtr": "web:match-123:1"
}
```

When no authoritative killer exists, `killerPtr` is absent. The emitter never invents a pointer. An internal key that is not present in the report actor map is a resolver/emitter contract failure and must fail a test rather than silently credit another actor.

`BattleEngine` remains pure: no wall clock, I/O, server calls, or Data access. `WebMatchService` continues to resolve first and ingest the complete report in its existing dedicated transaction.

## Compatibility and golden policy

The field is additive to the in-memory record and event payload. Killer identity is report provenance,
not combat math, so the battle golden hash blanks `KillerActorKey` alongside the existing platform,
content, and warning provenance fields. Full report serialization still remains byte-deterministic and
the dedicated replay test covers the attribution values and ordering. A future change that lets
attribution affect resolution must leave this policy, move the relevant goldens, and follow the
normal measured version-bump process; never hand-edit expected hashes.

### Live-lawn boundary

The server progression projector may consume a live death fact, but it must not synthesize another lifecycle `die` event from `combat.hit` or `*.damage`. Without an exact killer fact, live unique-kill XP remains a no-award result; active-duration XP is unaffected. Running a shadow `BattleEngine` against live telemetry is forbidden because the server does not own Unity's current HP, deferred lifetime, or scheduling.

## ActorHub and numeric policy

This module consumes the actor keys and existing battle results only. It adds no combat or derived magnitude and therefore introduces no new ActorHub contribution. Existing battle damage and HP continue through the battle-local Funnel/FA10 path.

`KillerActorKey`, pointers, rounds, and actor ids are identity fields. No balance number is introduced. Existing XP amounts remain the named `long` tuning values in the progression tuning file; no cap or narrowing conversion is permitted.

## Project structure

```text
src/FusionRpg.Core/Battle/BattleModels.cs       # BattleEventRec.KillerActorKey
src/FusionRpg.Core/Battle/BattleRunState.cs     # lethal-cause assignment
src/FusionRpg.Core/Battle/BattleReportEmitter.cs # key → synthetic killerPtr
src/FusionRpg.Server/WebMatchService.cs         # unchanged orchestration/ingest seam
tests/FusionRpg.Core.Tests/Battle/              # event and determinism coverage
tests/FusionRpg.E2E.Tests/                      # report → ingest → projection coverage
```

## Code style

Keep attribution at the point where the battle state already knows both actors:

```csharp
if (!victim.Alive && RecordedDeaths.Add(victim.Setup.Key))
{
    attacker.Kills++;
    Events.Add(new BattleEventRec(
        round, BattleEventKinds.Die, victim.Setup.Key,
        victim.Setup.TypeId, victim.Setup.Side,
        KillerActorKey: attacker.Setup.Key));
}
```

Death sweeps use the same constructor with `KillerActorKey: null`. Do not add a second event, mutate the report after emission, or query a server/Data service from the resolver.

## Testing strategy

- **Direct attribution:** a lethal basic attack emits one die record with the attacker key and increments exactly that attacker's kill tally.
- **Guardian attribution:** a shared hit that kills the target and guardian attributes both deaths to the initiating attacker.
- **Attackerless refusal:** status/environment/sweep deaths omit `killerPtr` and produce no unique-kill credit.
- **Emitter mapping:** a valid actor key maps to the correct synthetic pointer; an invalid key fails the test contract; null remains absent.
- **Determinism:** the same setup and seed produce byte-identical reports, including killer keys and event order.
- **Replay/idempotency:** repeated web-match ingestion produces one lifecycle fact and one downstream receipt; no second `die` event is created.
- **Boundary:** a live-PvZ event stream is never passed through `BattleEngine`, and no injector change is required for this module.

Commands:

```powershell
dotnet test tests\FusionRpg.Core.Tests --no-restore
dotnet test tests\FusionRpg.Data.Tests --no-restore
dotnet test tests\FusionRpg.E2E.Tests --no-restore
.\scripts\guard-dal.ps1; .\scripts\guard-funnel-delta.ps1
```

## Boundaries

- **Always:** reuse `plant.die`/`zombie.die`; assign causes from battle-owned state; preserve stable ordering and seeded replay; keep report emission pure.
- **Ask first:** a new event kind; changing report versioning or golden policy; attributing indirect/status damage; changing unique-XP reward policy.
- **Never:** touch injector killer capture; shadow-simulate live PvZ; infer killers from last-hit caches or server arrival order; emit duplicate lifecycle deaths; write SQL outside Data; add a private combat formula.

## Success criteria

1. `BattleEventRec` can carry an optional killer actor key without changing the existing event vocabulary.
2. Every direct lethal battle hit emits exactly one die record with the correct killer key.
3. `BattleReportEmitter` emits `killerPtr` only for a validated key and omits it for unknown causes.
4. Same setup and seed replay byte-identically, including death attribution.
5. Web-match ingest and unique-XP receipts remain idempotent.
6. Live lawn runs retain Unity as the sole lifecycle authority and require no injector modification for this server-owned feature.

## Open questions

Indirect and status damage attribution is intentionally deferred. It requires an explicit attacker-carrying battle context and a separate test matrix; until then those deaths remain unattributed.
