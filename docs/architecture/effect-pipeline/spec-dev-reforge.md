# Spec: `dev-reforge`

**Module id:** `dev-reforge` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 10 of 10, last
**Depends on:** `instance-producer` (module 4), `patron-absorption` (module 6)

## Objective

`POST /api/debug/reforge-world` — re-derive a player's roster from the current catalog against the
SAME world seed. Debug surface only, never shipped to players.

`effect-pipeline-ideal.md` A4's own framing: Q5 freezes existing rolls forever (*"a retuned affix
cannot be observed without creating a new profile"*), and that is a direct tax on balance iteration —
the loop this repo has spent real effort protecting everywhere else by moving numbers into
`data/tuning/` so a change costs a file save, not a rebuild. A player-facing "reforge" option was
considered and rejected (it would let a player farm a retune, defeating the frozen-forever guarantee
Q5 exists to give). **A dev-only command is a different thing.**

## Design

### Why this is free — the roster was already designed to be derivable

`effect-pipeline-ideal.md` §3.6: given `(worldSeed, catalog_revision)`, the whole per-player roster is
reproducible by construction — *"generate by deterministic function, so very fast"*. This module costs
one endpoint precisely because `world-seed` (module 7) and `instance-producer` (module 4) already made
the roster a **derivation**, not a stored fact with no recipe. `dev-reforge` does not invent
regeneration; it exposes the regeneration that already has to exist for crash recovery.

```text
POST /api/debug/reforge-world { playerId }
    -> read the player's world_seed (unchanged)
    -> read the CURRENT catalog_revision (may have moved since the player's rolls were frozen)
    -> re-run InstanceProducer for every container the player's roster references
    -> overwrite that player's rolled rows, in place
```

### Guardrails — this is a debug endpoint, not a soft "reforge" feature

| Guardrail | Why |
|---|---|
| behind the existing debug-endpoint auth gate, same as every other `/api/debug/*` route | never reachable by a normal client |
| refuses in a Release/player build, same convention as the rest of `DebugEndpoints.cs` | A4: "it ships behind the debug surface, never to players" |
| logs the before/after `catalog_revision` and the player id it touched | balance-testing traceability — a dev needs to know what actually changed |
| does not touch `world_seed` itself | the seed's create-once guarantee (module 7) is untouched; only the catalog side of the derivation re-runs |

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~DevReforge"
curl -X POST http://127.0.0.1:5088/api/debug/reforge-world -d '{"playerId":1}'   # owner's dev machine only
```

## Project structure

```text
src/FusionRpg.Server/DebugEndpoints.cs        edit — POST /api/debug/reforge-world
tests/FusionRpg.Server.Tests/DevReforgeTests.cs   new
```

## Code style

```csharp
// Costs one endpoint because the roster was already a DERIVATION (world-seed + instance-producer),
// not a stored fact with no recipe (effect-pipeline-ideal.md §3.6). Dev-only, per A4 — a player-
// facing reforge was considered and rejected because it would let a player farm a retune.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `reforge_reproduces_the_same_roster_when_catalog_is_unchanged` | idempotent when nothing moved |
| `reforge_picks_up_a_retuned_affix_after_catalog_revision_bumps` | the actual payoff — balance iteration reaches an existing profile |
| `reforge_never_changes_the_players_world_seed` | only the catalog side re-derives |
| `endpoint_refuses_outside_the_debug_build` | A4's own "never to players" guarantee, mechanically enforced |
| `endpoint_requires_the_same_debug_auth_gate_as_every_other_debug_route` | no special-cased bypass |

## Boundaries

**Always:** gate behind the existing debug auth; log the before/after catalog revision; leave
`world_seed` untouched.

**Ask first:** exposing any reforge capability to a real client build, under any framing — A4 already
closed this decision.

**Never:** let this endpoint become reachable outside a debug build; let it silently succeed with no
log of what changed.

## Success criteria

- [ ] The endpoint re-derives a player's roster against the current catalog and the player's unchanged
      world seed.
- [ ] It is unreachable outside a debug build, proven by test.
- [ ] A balance retune is observable on an existing profile without creating a new one — closing A4's
      named tax on the iteration loop.
