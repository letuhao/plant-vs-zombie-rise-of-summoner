# Spec: shield-system — shield resource + derived stats (element/combat extension)

**Status:** Draft v2 (2026-08-21) — open questions resolved by owner; awaiting final spec approval. Nothing implements until this is approved and the decisions.md amendment lands.
**Parent:** [decisions.md](decisions.md) (Effect Funnel row already anticipates: "CombatMath (DEF / element / shield) sits **above** Funnel later"). Element base: [element-hub-ssot.md](element-hub-ssot.md) — its v1 ban list ("no element-specific shield engine") is what this spec unlocks; the ban-list line gets a pointer here in the same change.
**Source ideal:** Chaos `combat-core/09_Shields_and_Protections.md` + `10_Resource_Damage_Distribution.md` (shield actors with own HP, priority drain, penetration, stacking policies, shields-before-resources distribution). This spec adapts that shape to FusionRpg scale; §9 lists what we deliberately do not port.

## Scope check (Phase 0)

One capability, one spec. The resource pool, the derived channels, and the damage gate are layers of a single testable feature — none ships or proves alone — so no capability map. Module id: `shield-system`.

## Owner decisions (2026-08-21 — supersede the draft-v1 assumptions/open questions)

1. **RPG-combat-only, locked permanently.** Shields absorb RPG overlay + standalone battle damage only. Vanilla PVZ damage (bites, pea hits, Unity `TakeDamage`) is never absorbed — a plant/zombie can still die to in-game damage while shielded, and that is by design. We extend our combat, we do not change the PVZ game itself; same principle as the stats/combat/damage design. No vanilla-refund variant, ever — moved to the Never boundary.
2. **Regen = constant trickle.** `regen` restores shield HP every drain tick while an instance is active; no after-hit delay in v1 (can be added later as a grant field without channel changes).
3. **Guardian aura targeting is in this spec's scope.** Nearby-ally shield grants (lane/radius via `TargetResolver`) ship as part of the shield system, not deferred to the demon stream.
4. **Innate shields allowed.** Content rows (elite zombies, demon species) may declare a baseline shield that auto-grants at actor registration — no effect grant required.
5. **Channels live under `combat.shield.*`** inside the combat family catalog (not a separate `shield.*` prefix). The generated catalog grows 56 → 84 and its count tests churn accordingly.
6. **Shields get their own matchup matrix.** A `ShieldElementMatrix` separate from `ElementRingMatrix`, so shield balance can diverge from combat matchups. V1 content is seeded identical to the ring + light/dark mutual counter, but the table is independently editable.
7. Unchanged from draft v1: vanilla `theShieldHealth` and the `P-SHIELD` cheat column stay vanilla-only concepts; the RPG shield never writes them.

## 1. Objective

Give the RPG a second defensive resource next to HP: an element-typed, depletable **shield pool** that absorbs RPG damage before it reaches the Funnel HP write, plus the derived stats that scale it. The summoner fantasy this serves: shields are what a summoner *grants* — guardian demons shield lanes, skills shield plants, elite zombies arrive innately shielded — while HP stays the vanilla-owned life bar.

Success looks like: a granted or innate shield absorbs overlay damage deterministically (same numbers in PvZ overlay and standalone battles), breaks with an event and a VFX cue, and its size/durability/regeneration respond to four new derived-stat families — with zero change to any existing combat number when no shield is present, and zero change to vanilla game damage always.

## 2. Design (locked on approval)

### 2.1 Shield instance model (adapted from Chaos `ShieldActor`, trimmed)

```text
ShieldInstance:
  shieldId        stable id (grantId- or content-row-derived)
  ownerKey        actor key / ptr
  element         none | fire | ice | air | earth | light | dark   (omni not valid)
  maxHp           source base + combat.shield.capacity.{omni+element} of owner at grant time
  hp              current pool
  priority        source-declared; lower drains first
  createdSeq      monotonic tiebreak
  expiresAtTick   optional duration (tick-based, no wall clock); innate shields default to none
  sourceId        grantId or content row id — provenance for merge + debug
  isInnate        content-row shields; auto-granted at actor registration
```

No per-second decay in v1 (duration expiry only). No restoration-event list — regeneration is a derived channel (§2.3).

**Sources:** effect grants (`shield.grant` action), demon traits (guardian aura, §2.6), summoner skills, and **content rows** (innate — validated at ingest like element typing; unknown element rejects the row).

### 2.2 Placement — the gate above the Funnel

Absorption happens in `CombatDamageDispatcher.DispatchInstant`, after `math.Finalize`, before `funnel.EnqueueMutation`: a damage amount (negative delta) passes through `ShieldRuntime.Absorb(ptr, amount, elements)`; only the remainder (if nonzero) is enqueued on the `hp` channel. This is exactly the slot decisions.md reserved. Consequences:

- Funnel stays hp-only, add-only, FA10 — **no new funnel channel**. Shield pool mutation is Core-internal runtime state, like StatusRuntime timed state.
- Both consumers get shields for free: the overlay injector path and the standalone battle-local funnel instance route through the same dispatcher.
- Positive deltas (heals) never touch shields.
- Vanilla damage never reaches this gate, so the RPG-only boundary (owner decision 1) holds by construction.
- Event-pipeline v2 drain is where the dispatcher already runs, so absorption is inside the existing frame budget and death-flush barriers.

### 2.3 Derived channels (28 new: 4 families × {omni + 6 elements}, catalog 56 → 84)

Four new families join `CombatChannelFamilies`, so `AllCombatChannelIds` generates them from `ElementRoster` exactly like the existing eight — flat-sum, default 0, additive omni (`total = omni + element`), registered in the Actor Hub catalog.

| Family | Side | Consumer |
|---|---|---|
| `combat.shield.capacity.{omni\|el}` | owner | flat bonus to `maxHp` of shields of that element granted to this actor (read at grant time) |
| `combat.shield.toughness.{omni\|el}` | owner | reduces damage dealt to the shield pool (§2.4) |
| `combat.shield.pen.{omni\|el}` | attacker | increases damage dealt to the shield pool — the breaker stat |
| `combat.shield.regen.{omni\|el}` | owner | shield HP restored per drain tick while an instance of that element is active (capped at `maxHp`) |

`DerivedStatRegistry` count assertions churn 56 → 84 (the sole permitted test edit, same rule as the light/dark extension). `CombatDerivedReader` gains four element→channel maps; the exhaustiveness walk (§7) covers them.

### 2.4 Absorb math (locked)

Per hit, against the highest-priority compatible shield, repeating down the stack while damage remains:

```text
incoming        = |finalized signed delta|                     (post hit/crit, pre Funnel)
elemMod         = Σ (componentWeight × ShieldElementMatrix.relationShare(componentElement, shieldElement))
                    × incoming × ShieldMatchupShareK           (untyped shield → 0)
breakerDelta    = combat.shield.pen(attacker, el) − combat.shield.toughness(owner, el)   (omni + element each side)
damageToShield  = max(0, incoming + elemMod + breakerDelta)
spent           = min(shieldHp, damageToShield)
hpRemainder     = damageToShield == 0 ? incoming
                : round(incoming × (damageToShield − spent) / damageToShield)
```

- Shield holds (`shieldHp ≥ damageToShield`) → HP takes 0. Shield breaks → HP takes the proportional share of the original hit the shield couldn't cover; the remainder then hits the next shield in the stack, then HP.
- **`ShieldElementMatrix`** is shield-owned: same STR/WEK/NEU relation vocabulary, its own table. V1 seed = ring relations + light/dark mutual counter (identical values to `ElementRingMatrix` §8.5), but edits to it never touch combat matchups and vice versa. Golden generated from `ElementRoster` — exhaustive by construction, no hand-listed pairs.
- Policy constant: **`ShieldMatchupShareK = 0.25`** (own constant, decoupled from `MatchupShareK`).
- Integer HP units end to end; intermediate math in scaled fixed-point per the standalone determinism discipline. Invariant: `hpRemainder ≤ incoming` always; a hit fully absorbed reports `hpRemainder = 0` exactly.
- No rolls anywhere — absorption is deterministic; `SeededRng` streams are not consumed.

### 2.5 Stacking and conflict policy (trimmed from the Chaos policy zoo)

- Cap: **3 active shields per actor** (innate counts toward the cap).
- Same `(sourceId, element)` re-grant → **merge**: refresh `hp` to new `maxHp`, refresh expiry (no additive stacking of the same source).
- At cap with a new distinct shield → **replace weakest** (lowest current `hp`; `createdSeq` tiebreak). Innate shields are replaceable like any other — no protected slot in v1.
- Drain order: `(priority, createdSeq)` ascending — deterministic, no HP-percentage reordering (Chaos's dynamic priority formula is rejected, §9).

### 2.6 Lifecycle, targeting, events, surfacing

- **Tick:** `ShieldRuntime.Tick` runs on the drain tick (same host as StatusRuntime ticks): apply regen (constant trickle, owner decision 2), expire by tick, prune broken instances. Death/lifecycle flush clears the actor's shields with the same barriers the event pipeline already enforces. Broken innate shields do not auto-re-form; they return only via regen-from-1-HP-before-break, a fresh grant, or actor re-registration.
- **Aura targeting (in scope — owner decision 3):** guardian-style grants resolve "nearby allies" through the existing `TargetResolver` vocabulary (lane / radius / self), producing one `ShieldRuntime.Apply` per resolved ally. The aura re-asserts on its source's cadence (trait pulse or skill cast); merge-same-source (§2.5) makes re-assertion idempotent. `trait.guardian` is the first consumer.
- **Events** (existing event vocabulary, additive kinds): `shield.granted`, `shield.absorbed` (coalescable, same-key rules as hits), `shield.broken`, `shield.expired`. `shield.broken` is the VFX/audio moment — cue registration goes through `VfxCatalog` in the VFX stream's vocabulary, detail deferred to that stream.
- **Grant path:** a new effect action kind (`shield.grant`) handled by the effect action sink → `ShieldRuntime.Apply` — the same shape as status applies. Never a Funnel mutation.
- **UI:** additive DTO fields (current/max shield per actor) for the web control room shield bar and debug boards; breakdown lines in the overlay damage debug output (`absorbed`, `hpRemainder`, per-shield spent).

### 2.7 Perf budget

Absorb path is hot (inside the drain): O(1) actor lookup (dictionary by ptr), zero allocation on the no-shield fast path (a single `TryGetValue` miss), no LINQ in `Absorb`/`Tick`. Aura re-assertion runs on trait/skill cadence, never per hit. New `PerfProbe` section `ShieldAbsorb`; budget follows the perf-probe plan's drain share — shields must not move the pipeline share measurably in the no-shield case (stress scenario re-run before live sign-off).

## 3. Tech stack

C# / .NET as the repo stands. New code in `FusionRpg.Core` only (engine-agnostic; no Unity, no SQL). Contracts get additive DTO fields.

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-single-writer.ps1; .\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1; .\scripts\guard-dal.ps1
# live perf re-check after wiring (owner-run):
.\scripts\stress-test.ps1
```

## 5. Project structure

```
src/FusionRpg.Core/Combat/Shield/     → ShieldRuntime.cs, ShieldInstance.cs, ShieldMath.cs,
                                        ShieldPolicy.cs, ShieldElementMatrix.cs, ShieldAuraGrants.cs
src/FusionRpg.Core/Stats/Derived/     → DerivedStatChannels.cs (+4 combat.shield.* families, roster-generated)
src/FusionRpg.Core/Combat/            → CombatDamageDispatcher.cs (gate hook), CombatDerivedReader.cs (+4 maps)
src/FusionRpg.Contracts/              → Dtos.cs (additive shield fields)
tests/FusionRpg.Core.Tests/Combat/Shield/ → math goldens, matrix goldens, stacking, lifecycle,
                                            aura targeting, dispatcher integration
docs/architecture/decisions.md        → amendment row (shield layer above Funnel — unlocks element-hub ban)
docs/architecture/element-hub-ssot.md → §13 ban-list line gains a pointer to this spec
```

## 6. Code style

Match `StatusRuntime` / `OverlayCombatCalculator` idiom — sealed classes, explicit policy constants, no LINQ on hot paths, channels generated from `ElementRoster`:

```csharp
public static class DerivedStatChannels
{
    // joins CombatChannelFamilies so AllCombatChannelIds generates
    // family × (omni + ElementRoster.Concrete) — exhaustive by construction
    public const string CombatShieldCapacityPrefix = "combat.shield.capacity";
}
```

## 7. Testing strategy

Same framework and layout as existing Core tests. Levels:

- **Math goldens** — absorb table generated over `ElementRoster` × relation (STR/WEK/NEU/untyped) × {pen, toughness} deltas × {hold, exact-break, overflow, multi-shield} cases. Locks §2.4 numerically, including rounding and the `hpRemainder ≤ incoming` invariant.
- **Matrix goldens** — full `ShieldElementMatrix` table generated from `ElementRoster` (defeats fail-open defaults, same lesson as light/dark), plus a seed-equality assert against `ElementRingMatrix` v1 values documenting the intentional starting point.
- **Stacking/policy** — cap, merge-same-source refresh, replace-weakest, innate-counts-toward-cap, drain order determinism.
- **Lifecycle** — regen cap, tick expiry, death-flush clears, innate auto-grant at registration, no-shield fast path leaves deltas byte-identical (regression lock: every existing combat golden passes unchanged).
- **Aura targeting** — lane/radius resolution grants to the right allies, re-assertion is idempotent via merge, no per-hit aura work.
- **Dispatcher integration** — damage → partial absorb → Funnel receives exactly `hpRemainder`; heals bypass; guard scripts stay green (no new writer/funnel tokens outside allowed paths).
- **Exhaustiveness walk** — all 28 new channels resolve through `CombatDerivedReader` without throwing; roster-driven, defeats hand-maintained switch drift.
- **Determinism replay** — a standalone battle with shields (granted + innate + aura) replays byte-identical under fixed seed/version stamp.

## 8. Boundaries

- **Always:** absorb strictly above the Funnel; Funnel stays hp-only add-only FA10; integer/fixed-point math; additive flat-sum channels with additive omni; regression-lock the no-shield path; update decisions.md + element-hub-ssot.md in the same change; run all four guards.
- **Ask first:** reflection/immunity shield types; percent penetration; changing `ShieldMatchupShareK`, the cap of 3, or drain order; diverging `ShieldElementMatrix` content from the ring seed (balance decision); protected innate slots; regen after-hit delay; any non-additive DTO/web change.
- **Never:** absorb vanilla PVZ damage (locked by owner — we extend our combat, we don't change the game); shield code writing Unity HP or `theShieldHealth`; a new Funnel channel; YAML/runtime shield registry; `System.Random`.

## 9. Rejected Chaos paths (v1)

Deliberately not ported: shield-as-actor with subsystem registration; ImmunityShield / ReflectionShield / absorb-reflect percentages; per-type conflict-policy matrix (`shields.yaml`), PvP/area override precedence; percent penetration with per-type caps; dynamic priority (`base + hp% + type modifier`); per-second lifetime decay; restoration-event lists; multi-tier resource distribution (temp/primary/secondary/special — we have exactly shield → HP); damage impact maps splitting into mana/qi.

## 10. Success criteria

1. All existing tests pass with only derived-registry count assertions updated (56 → 84); no-shield combat output is byte-identical; vanilla damage path untouched by construction.
2. Math + matrix goldens and the exhaustiveness walk green over the full roster; determinism replay green.
3. A granted shield demonstrably absorbs an overlay hit end-to-end (dispatcher → partial HP remainder in Funnel) in an integration test, and live via the debug enqueue endpoint.
4. An innate content-row shield auto-grants at registration; a guardian-style aura grant shields resolved nearby allies idempotently.
5. `shield.broken` event observable in the event stream with a VFX cue registered.
6. decisions.md amendment recorded; element-hub-ssot ban line updated to point here.
7. All four guard scripts green; stress scenario shows no measurable pipeline-share regression with zero shields active.
