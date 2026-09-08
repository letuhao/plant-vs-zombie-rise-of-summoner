# Spec: `world-map-scope`

**Module id:** `world-map-scope` · **Program:** [buff-debuff-scope-map.md](../buff-debuff-scope-map.md) ·
**Status:** Draft — pending owner review. **The least-precedented module in this program — read this
header before the rest.**

**Depends on:** `scope-model` · **Blocks:** nothing

---

## Read this first: this module is genuinely riskier than its siblings

`battlefield-scope` reuses a proven delivery mechanism (`owner_kind = match`, `EffectBag.Grant`,
patron.aura as a working precedent). **World map has none of that.** No `BattleEffectHost`, no Funnel, no
`EffectGrant`. This spec is grounded in real code — `WorldState.cs`, `WorldCanonical.cs`, and (as of the
2026-08-29 audit pass) every real consumer of `UpkeepHandicapMilli` — read this session, not assumed from
`spec-ai-commander.md`'s prose alone (Design Gate evidence rule 2: documentation is not code). **The
compute-path question is now resolved (§ below): there isn't one path to read, and that turns out to be
good news** — this module declares and hashes the modifier; it does not need to wire itself into any
consumer ahead of time.

**Crosses [DESIGN-GATE.md](../DESIGN-GATE.md) §1's World map row** ("Specs pending owner review — no
build authorized") **under explicit owner authorization**, 2026-08-29 ("build both now, full parity") —
see [buff-debuff-scope-ideal.md](../buff-debuff-scope-ideal.md) §4.2. Worth a `decisions.md` line once
this reaches Plan/Tasks, so the authorization is traceable outside this conversation.

## What's real, read this session

`WorldCanonical.Write` ([WorldCanonical.cs:29-30](../../../src/FusionRpg.Core/World/WorldCanonical.cs))
already hashes one per-faction lever: `f.UpkeepHandicapMilli`, documented on `WorldFaction`
([WorldState.cs:69-73](../../../src/FusionRpg.Core/World/WorldState.cs)) as *"a declared balance lever,
not a cheat — hashed, replayed, and named in the turn report whenever it is not 1000."` **This is the
closest existing precedent to a world-map "buff/debuff"** — a named, per-mille, hashed, replay-safe
multiplier on a faction row — and this module should follow its exact shape rather than inventing a
different one.

`WorldCanonical.Write` also hashes, per entity: `e.OwnerFactionId` and, per legion member,
`m.InstanceId` ([WorldCanonical.cs:49-59](../../../src/FusionRpg.Core/World/WorldCanonical.cs)). So
**own-side resolution at the world-map layer is a plain `OwnerFactionId` comparison** — structurally
identical in shape to `ZoneOfControl.IsHostile`'s *"pure faction-id comparison"*
([spec-ai-commander.md](../world/spec-ai-commander.md)) — and **unique-demon resolution has a real path**:
walk `WorldState.Entities[].Members[]` for a matching `InstanceId`, the world-map equivalent of
`MatchUniqueBindingsFacet.TryGet`.

## Objective

Given a `scope-model` triple `(WorldMap, WhoSelector, kind)`, apply a standing modifier to the right
faction/entity/legion-member rows in `WorldState`, deterministically, replayed byte-identically from a
command log — the same non-negotiable property `spec-ai-commander.md` already established for AI
decisions (*"Filing commands instead makes AI decisions data: replay never re-runs a policy"*). A
world-map buff must be **data on the state**, never **logic that re-derives differently on replay**.

**Success is measurable:** a per-faction or per-entity modifier round-trips through `WorldCanonical`'s
hash; a world reloaded from a command log reproduces the identical modifier state; own-side/enemy-side
resolves by `OwnerFactionId` comparison, tested directly against a multi-faction fixture.

## Design (draft — the part most likely to change once the compute path is read)

Two candidate shapes, **not decided here** — this is the one open design question in this program left
for Plan/Tasks rather than resolved in Specify, because it depends on reading code not yet read:

1. **A named field on `WorldFaction`/`WorldEntity`**, following `UpkeepHandicapMilli`'s exact precedent
   — simplest, hashes for free through the existing `Row(...)` calls, but a fixed field can't express an
   open-ended "any commander, any magnitude" the way a grant-shaped system could. **Correction found
   during implementation, 2026-08-29:** appending the new field to the *existing* `"faction"` row
   (mirroring `UpkeepHandicapMilli` literally) moved `WorldWaveOneAcceptanceTests.
   The_scenario_hashes_to_its_golden` — every faction row gained one more hashed cell, even at the
   neutral default, which is a real row-shape change, not a value change. The file's own Intel section
   already established the right answer for exactly this case (*"Belief is state, so it is hashed like
   state. Written last so a world with no intel yet... produces exactly the bytes it always did"*):
   emit the new field as its **own row**, only when non-default, appended at the end rather than
   folded into an existing row. Zero goldens moved once built this way — confirmed live, not assumed.
2. **A small owned table of active modifiers** (faction/entity id → source → per-mille value), hashed as
   its own `WorldCanonical` row group — more flexible, closer in spirit to `EffectGrant`, but is new
   surface area `WorldCanonical` doesn't have a precedent for today.

**Recommendation, strengthened during audit, 2026-08-29 — confirmed against code, not just argued:**
read [`TurnEngine.Step`](../../../src/FusionRpg.Core/World/Turn/TurnEngine.cs#L71) directly. Every stage
(`Reveal → Movement → Sieges → Production → Growth → Pressure → Events → Snapshot → Observe`) is a pure
function taking the previous `WorldState` and returning a new one via C# `with` expressions
(`opening = world with { Entities = ... }`) — **nothing is ever mutated in place.** Shape 1 (a named
field on `WorldFaction`/`WorldEntity`) is not a new pattern layered onto this pipeline; it is exactly
what every existing stage already does. A world-map buff, if it needs to change turn over turn, is one
more `with`-rewrite — either its own pipeline stage or folded into an existing one — with zero new
mutation mechanism to invent. This resolves what was flagged as an open risk before verification:
whether `WorldFaction` is ever mutated in place turned out not to be a live question — it never is,
anywhere, by design. Start with shape 1; revisit shape 2 only if a second source needs to stack
independently.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~WorldMapScope
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
# WorldDeterminismGuardTests lives in Guard.Tests, not Core.Tests — corrected during implementation,
# confirmed live (the Core.Tests filter matches zero tests; this one is the real project).
dotnet test tests\FusionRpg.Guard.Tests --filter FullyQualifiedName~WorldDeterminismGuard
.\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/World/WorldMapScopeExecutor.cs      → resolution + application, shape TBD by design above
tests/FusionRpg.Core.Tests/World/WorldMapScopeTests.cs
```

Lives under `World/`, inheriting `WorldDeterminismGuardTests`'s existing scan for free — the same reason
`spec-ai-commander.md`'s own `Ai/` directory did: *"nothing to wire up."*

## Code style

Integer per-mille throughout, matching `UpkeepHandicapMilli`'s own convention — never `double`/`float`
(the kernel-wide ban already covers `World/`). Pure functions over `WorldState`, nothing cached between
turns, matching `spec-ai-commander.md`'s own stated code style for this directory tree.

## Testing strategy

- **Hash round-trip:** a world with an active modifier hashes differently from one without; replaying
  the same command log twice reproduces the identical hash.
- **Own-side resolution:** `OwnerFactionId` comparison proven directly against a multi-faction, multi-
  entity fixture.
- **Unique-demon resolution:** a `Members[].InstanceId` lookup proven against a legion carrying more than
  one member.
- **Determinism:** same `(WorldState, scope)` twice ⇒ byte-identical result; `WorldDeterminismGuardTests`
  stays green with no exemption added.

## Boundaries

- **Always:** integer per-mille; hashed through `WorldCanonical`; replay-safe (data, never re-derived
  logic).
- **Ask first:** which of the two design shapes above ships first — this is a real open call, not a style
  preference.
- **Never:** a `double`/`float` magnitude; state that isn't hashed (an unhashed modifier is invisible to
  replay and to every determinism test this program already has).

## Success criteria

1. A modifier round-trips through `WorldCanonical`'s hash in both directions (present changes it, absent
   doesn't drift it).
2. Own-side and unique-demon resolution both proven against real multi-faction fixtures.
3. Replay byte-identity holds across two runs of the same command log.
4. `WorldDeterminismGuardTests` and all Core/Data suites green.

## Resolved during audit, 2026-08-29 — the compute-path question

Read every consumer of `UpkeepHandicapMilli` directly, since it's this module's own chosen precedent.
**There is no single compute path — three independent call sites read it, each for its own reason:**

- [`World/Loam/LoamUpkeep.cs:25`](../../../src/FusionRpg.Core/World/Loam/LoamUpkeep.cs) — multiplies it
  into a sector's garrison upkeep cost.
- [`World/Loam/LoamPhases.cs:118-120`](../../../src/FusionRpg.Core/World/Loam/LoamPhases.cs) — reports it
  in the turn log when non-default (observability, not computation).
- [`World/Ai/FrontierRulesPolicy.cs:172`](../../../src/FusionRpg.Core/World/Ai/FrontierRulesPolicy.cs) —
  the AI's own economic evaluation reads it independently, for its own production/upkeep/stock totals.

**This resolves the question by dissolving it.** `world-map-scope` was never going to "hook into the
compute path" because there isn't one to hook into — `UpkeepHandicapMilli` itself proves the pattern is
"declare a named, hashed field; let each future consumer read it independently, wherever that consumer's
own logic lives." So this module's job stays exactly what §Design already said (declare and hash the
modifier, per shape 1) — it does **not** additionally need to wire the modifier into Loam upkeep, Loam
production, or AI evaluation. Those are separate, later, content-driven changes to whichever specific
subsystem a future commander buff actually targets (e.g., a production-boosting buff would be
`LoamPhases.cs`'s concern to read, not this module's to anticipate) — matching this program's own
standing boundary that aura *content* is deferred, not this module's to build ahead of need.
