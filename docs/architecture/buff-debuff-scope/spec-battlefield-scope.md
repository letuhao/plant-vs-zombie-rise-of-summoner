# Spec: `battlefield-scope`

**Module id:** `battlefield-scope` · **Program:** [buff-debuff-scope-map.md](../buff-debuff-scope-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `scope-model` · **Blocks:** nothing (own-side completeness soft-depends on
`membership-events`, but target/type/unique-demon ship without it)

---

## Corrected during audit, 2026-08-29 — read before the rest

**This module is two hosts sharing one front end, not one host.** The original draft assumed "PvZ lawn"
and "expeditions/web-RPG battles" were the same execution context because both are `BattleEngine`-shaped.
Verified against code this pass, they are not: `BattleEngine`/`BattleEffectHost` is the SIM/expedition
kernel (C#-only, no Unity, this session's own A17/A18 machinery) — **live PvZ never runs through it at
all.** Live PvZ's own damage/read path is Unity-side (`FusionRpg.Injector`), and the RPG's write side
deliberately never touches it (`EntityStatWriter.cs`'s own doc comments: *"Never TakeDamage"*). What the
two hosts genuinely **share** is the grant-issuing front end — one `EffectBag`, `owner_kind = match`,
proven for live PvZ by patron.aura and for SIM by this session's own A18 work. What they do **not**
share is the reader: SIM needs new wiring into `BattleEffectHost` (this spec's own §"SIM host" below);
live PvZ needs **no new reader at all** — patron.aura already proves the injector's own overlay/Funnel
path works, and this module's live-PvZ job is to issue grants shaped correctly for what already reads
them, not to build a second reader.

This is why `scope-model`'s compatibility table carries a `host` (`Live`/`Sim`) dimension under
`Battlefield` — the two hosts can (and for G8-shaped kinds, do) disagree about what's supported.

## Assumptions I am making

1. **Unlike `scope-model`, this module is free to depend on `Actions/` and `Battle/Effects` types.**
   It is the executor, not the vocabulary — reusing `ActionTargetFilters` and `EffectBag` directly is the
   point, not a violation of `scope-model`'s own dependency-direction rule (which applies to `Scope/`
   only).
2. **The SIM host is where this module's real new code lives; the live-PvZ host is mostly a grant-shape
   contract.** Confirmed via `grep`: `EffectBag.Withdraw(grantId)`, `EffectBag.WithdrawForOwner(ownerKind,
   ownerKey)`, and `EffectFunnel.WithdrawByPluginId(pluginId, ownerKey)`
   ([`Effects/EffectBag.cs:121,211,250`](../../../src/FusionRpg.Core/Effects/EffectBag.cs),
   [`Effects/EffectFunnel.cs:143`](../../../src/FusionRpg.Core/Effects/EffectFunnel.cs)) already exist —
   bulk withdrawal by owner or by source plugin needs no new mechanism on either host. (The earlier draft
   cited `EffectProcAndOwner.ClearGrant`'s prefix-match instead — that clears proc/ICD bookkeeping for
   one grant, a different and narrower thing than withdrawing a population of grants.)

## Objective

Given a `scope-model` quadruple `(Battlefield, WhoSelector, kind, host)`, make the effect actually reach
the right entities — on the **SIM host** (expeditions/web-RPG, `BattleEffectHost`/Funnel), by wiring a
new reader into this session's own A18 machinery; on the **live-PvZ host**, by issuing correctly-shaped
grants into the same shared `EffectBag` the injector's already-proven overlay path already reads, adding
no new reader.

**Users:** the future aura-skill/commander work (deferred, not this module); any other future standing
effect that needs a scope rather than a single target, on either host.

**Success is measurable:** a scope targeting "own side" grants to every currently-qualifying entity and
nothing else, **on the SIM host** (measured directly); a scope requiring the side-wide-constant shape
(G8, live-only) reads as one value, not N grants; a unique-demon scope resolves through the specimen's
real binding; nothing in this module moves an existing golden; **and, separately, a LIVE gate** (owner
checklist, matching `patron-demon`'s own precedent) proves a live-PvZ grant is visible and correct in a
real match — SIM-passing is not proof for the live host, the same way it wasn't for patron-demon.

## Design

### Shared front end — both hosts, resolution and grant construction

| WHO | Mechanism | Reuses |
|---|---|---|
| target | one `EffectGrant`, `owner_kind = entity` | `EffectBag.Grant` directly |
| type | filter by `TypeIds` at grant time, one grant per currently-matching entity | `ActionTargetFilters.TypeIds`'s existing filter logic |
| unique demon | resolve `instanceId → ptr` via `TryGet`, `entity:{ptr}` owner_key | `MatchUniqueBindingsFacet` (`Match/UniqueBindings.cs`) unchanged |
| own/enemy side | **event-driven**: grant per qualifying entity on a `membership-events` spawn/hypnotize-on transition; `EffectBag.WithdrawForOwner("entity", ptr)` on clear/hypnotize-off | `EffectBag.Grant`/`WithdrawForOwner` (verified to exist, §Assumptions) |

Every grant this module issues carries a shared `PluginId` per aura source, so a whole source's grants
can be swept in one call via `EffectFunnel.WithdrawByPluginId` if a source itself ends (e.g., a
commander leaving) — not just per-entity via `WithdrawForOwner`.

**Which kinds need the G8 side-wide-constant shape, on which host, is `scope-model`'s compatibility table's
job to say — never this module's to guess per-kind.** This section only covers the per-entity-grant shape,
which is what "own/enemy side" needs on the **SIM host** and on the **live-PvZ host for every kind except
the G8-shaped ones**.

### SIM host — new reader wiring into `BattleEffectHost`

Follows the exact pattern this session already established three times (`Status`/`StatusRng` for A18d,
`Ledger`/`ResolveStatTarget` for A18e): a settable property on `BattleEffectHost`, forwarded to
`BattleEffectSink`, because `BattleRunState`'s constructor builds `Host` before most of its own other
fields exist. No new pattern — the fourth use of an established one. **This is where this module's real
new code lives.**

### Live-PvZ host — grant-shape contract only, no new reader

The injector's own overlay/Funnel path already reads grants from the same shared `EffectBag`
(`owner_kind = match`) — proven by `patron.aura`, which is exactly this shape today: *"a match-owner
effect grant... enters through the Secondary plugin Grant path → Funnel... the overlay combat calculator
reads the deltas through the existing derived-channel compose"*
([spec-patron-demon.md](../demons/spec-patron-demon.md)). **This module does not build a second reader
for live PvZ.** Its job on this host is narrower: issue grants shaped so that already-working path reads
them correctly, and — for any kind that path does **not** already support at `Full` (`scope-model`'s
table says which) — reject rather than silently issue an inert grant.

For the G8-shaped case specifically (`stat.modify`/`defense`, live only): this module does not deliver it
at all. It reads through the existing side-wide-cached-value path unchanged, and this module's own
compatibility check (via `scope-model`) simply confirms a caller asking for the per-entity shape on this
(kind, host) pair gets `ScopeUnsupported`, not a silently-inert grant.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~BattlefieldScope
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
$env:FUSIONRPG_GAME_DIR = "<game dir>"; .\scripts\deploy-play.ps1 -NoServer   # LIVE gate (owner)
```

Three guards named deliberately: this module grants through `EffectBag` (funnel-delta's territory), sits
adjacent to combat-relevant reads (single-writer's territory), and its live-PvZ half must add no Unity
read of its own (secondary-no-unity) — even though the reader itself is unchanged, existing code. The
LIVE gate command matches `patron-demon`'s own precedent exactly: SIM passing is not proof for this host.

## Project structure

```
src/FusionRpg.Core/Battle/BattlefieldScopeExecutor.cs
tests/FusionRpg.Core.Tests/Battle/Adoption/BattlefieldScopeTests.cs
```

One new file under `Battle/`, flat — matching `BattleStatModifierLedger.cs`'s own precedent from this
session rather than inventing a new subdirectory. Tests live under `Battle/Adoption/`, the same directory
every A18a-e test file this session used, since this is architecturally the same kind of work: wiring a
new capability into the shipped kernel.

## Code style

Reuse existing types directly rather than re-wrapping them — `ActionTargetFilters` for type filtering,
`MatchUniqueBindingsFacet` for demon resolution, `EffectGrantDto` for the grant shape. This module's own
code is the **glue**, not a parallel implementation of anything that already exists.

## Testing strategy

- **Per-WHO resolution**, against a real multi-entity board: target/type/unique-demon each proven to
  reach exactly the entities they should and no others.
- **The G8 case**, using the real shipped kind it applies to: proven to read as one side-wide value, not
  granted per entity — the direct execution-side test for `scope-model`'s own Assumption 2.
- **Membership reaction**: a demon spawning mid-match gains the grant; one dying/clearing loses it — built
  against a **test double for `membership-events`' transition shape** if that module hasn't landed yet by
  build time, matching this program's own established precedent (`StubIntentSource` built against a seam
  before its real caller existed).
- **Golden-neutrality**: full suite + all 8 golden fixtures unmoved — nothing currently authored calls
  this module, so this is a direct, measured proof, not an assumption.
- **LIVE gate (owner checklist, `patron-demon`-style — required before this module is "done" on the
  live-PvZ host, not just on SIM):** deploy → grant an own-side scope in a real match → (1) the debug
  effects view shows one grant per qualifying entity, named correctly, (2) a demon spawning mid-match
  gains it without a restart, (3) one leaving loses it, (4) the G8-shaped kind is confirmed **not**
  delivered as a grant at all (still reads through the unchanged side-wide path), (5) perf probe shows no
  new hot-path cost.

## Boundaries

- **Always:** route every mutation through `EffectBag`/Funnel/`EntityStatWriter` — the hard AGENTS.md
  rule, unconditional; treat SIM-passing and the LIVE gate as two separate proofs, never one standing in
  for the other.
- **Ask first:** any new combat-write path; any change to which kinds require the G8 shape (that is
  `scope-model`'s table to own); any live-PvZ reader code (the design's whole premise is that none is
  needed — discovering otherwise is a real scope change).
- **Never:** a second grant/withdraw mechanism parallel to `EffectBag`'s existing one; a cached/rescanned
  population (§4.1/4.4 of the ideal document already settled this — event-driven only); a second reader
  for live PvZ when the injector's own overlay path already works.

## Success criteria

1. All four WHO values resolve correctly against a real board, tested directly, **on the SIM host**.
2. The G8 side-wide-constant shape is proven distinct from the per-entity shape by an executed test, and
   proven to still be live-only (§scope-model's host dimension).
3. Full suite + all goldens unmoved.
4. All boundary guards green, including `guard-secondary-no-unity.ps1`.
5. The LIVE gate checklist passed by the owner — a real match, not just SIM.
