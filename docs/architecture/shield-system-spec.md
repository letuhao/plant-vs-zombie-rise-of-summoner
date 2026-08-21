# Spec: shield-system — shield resource + derived stats (element/combat extension)

**Status:** **Implemented offline (2026-08-21)** — approved after the two-lens audit (§12) with owner decisions 1–10, then built via [../../tasks/shield-todo.md](../../tasks/shield-todo.md) T1–T16 (156 shield tests; all suites + guards green). Remaining gates: owner-run live proof (deploy → `debug.shield.grant` → re-routed `debug.combat.probe`) and the no-shield stress re-check. Standalone absorption still lands with Battle-C2 (§10); the `BattleActorSetup` innate seam is deferred to that stream. One implementation correction is recorded in owner decision 9 (drain order = higher priority number first).
**Parent:** [decisions.md](decisions.md) (Effect Funnel row already anticipates: "CombatMath (DEF / element / shield) sits **above** Funnel later"). Element base: [element-hub-ssot.md](element-hub-ssot.md) — its v1 ban list ("no element-specific shield engine") is what this spec unlocks.
**Source ideal:** Chaos `combat-core/09_Shields_and_Protections.md` + `10_Resource_Damage_Distribution.md`. §9 lists what we deliberately do not port.

## Scope check (Phase 0)

One capability, one spec. Module id: `shield-system`. §10 records the one hard external dependency (standalone absorption waits on Battle-C2).

## Owner decisions (2026-08-21)

1. **RPG-combat-only, locked permanently.** Shields absorb RPG damage only. Vanilla PVZ damage (bites, pea hits, Unity `TakeDamage`) is never absorbed — a shielded plant can still die to in-game damage, by design. We extend our combat, we do not change the PVZ game. No vanilla-refund variant, ever.
2. **Regen = constant trickle.** No after-hit delay in v1 (future grant field, ask-first).
3. **Guardian aura targeting is in this spec's scope** — with the honest scope split the audit forced: this spec ships multi-target area grants and the cadence path; the `trait.guardian` → grant-template wiring stays with the demon deploy-facing module where demon V1 explicitly deferred it (§2.6).
4. **Innate shields allowed** — content rows auto-grant at actor registration (barrier rule in §2.6).
5. **Channels live under `combat.shield.*`** in the combat family catalog; catalog grows 56 → 84. Verified safe: nothing parses channel segments, and `combat.crit.resist.damage.*` is existing 4-segment precedent.
6. **Shields get their own matchup matrix** (`ShieldElementMatrix`), seeded from the ring relations but independently editable.
7. Vanilla `theShieldHealth` and the `P-SHIELD` cheat stay vanilla-only; the RPG shield never writes or reuses them — including their web payload keys (§2.6 UI).
8. **Balance constants confirmed:** cap 3 shields/actor, chip floor 0.10×, pen cap 3×, shield matchup K 0.25 — the goldens freeze these values.
9. **Default drain priorities — outer-to-core:** aura **30** → skill/effect **20** → innate **10** (**higher priority number drains first**; content rows may override). Cheap re-assertable aura layers burn before paid skill shields; the innate shield is the last line. (Consistency fix during T5: the draft carried Chaos's "lower drains first" wording, which contradicted these approved numbers — outer-to-core with 30/20/10 means higher-first, locked here.)
10. **Live proof via a debug grant endpoint:** a small debug/cheat action (grant shield to selected ptr: base, element, duration) ships in the debug task, same pattern as `enqueue-delta`, enabling §11.3's live absorb proof.

## 1. Objective

Give the RPG a second defensive resource next to HP: an element-typed, depletable **shield pool** absorbed above the Funnel HP write, plus four derived-stat families that scale it. Shields are what a summoner *grants* — guardian demons shield areas, skills shield plants, elite zombies arrive innately shielded — while HP stays the vanilla-owned life bar.

Success looks like: a granted or innate shield absorbs overlay damage deterministically, breaks with an event and a VFX cue, responds to the four stat families — with byte-identical combat output when no shield is present, zero change to vanilla damage always, and the same `ShieldMath` numbers available to the standalone engine the day Battle-C2 routes its damage through the shared pipeline (§10).

## 2. Design (locked on approval)

### 2.1 Shield instance model (adapted from Chaos `ShieldActor`, trimmed)

```text
ShieldInstance:
  shieldId        stable id (derived from sourceId + element)
  ownerKey        actor key / ptr
  element         none | fire | ice | air | earth | light | dark   (omni not valid)
  maxHp           source base + combat.shield.capacity.{omni+element} of owner, read per §2.6 barrier
  hp              current pool
  priority        source-declared; HIGHER drains first (outer-to-core); defaults
                  aura 30 / skill 20 / innate 10 (owner decision 9 — content may override)
  createdSeq      monotonic tiebreak
  expiresAtTick   optional duration (tick-based, no wall clock); innate default none
  sourceId        grantId or content row id; innate use the stable form "innate:{typeId}"
                  (registry resyncs re-fire Add — a stable sourceId + merge makes that idempotent)
  refillOnMerge   bool — hp policy on same-source re-grant (§2.5); skill/effect grants default
                  true, aura re-asserts default false
  isInnate        content-row shields; auto-granted at registration
```

No per-second decay (duration expiry only). No restoration-event list — regen is a channel.

**Sources:** effect grants (`shield.grant` action), aura grants (§2.6), summoner skills, and content rows (innate — validated at ingest; unknown element rejects the row; `maxHp ≤ 0` after capacity contributions rejects the grant, see §2.5).

### 2.2 Placement — the gate above the Funnel

Absorption lives in `CombatDamageDispatcher.DispatchInstant`, after `math.Finalize`, before `funnel.EnqueueMutation`: negative amounts pass through `ShieldRuntime.Absorb(ptr, amount, hitCount, elements)`; only the remainder is enqueued on the `hp` channel. Funnel stays hp-only, add-only, FA10 — no new funnel channel; shield pool mutation is Core-internal runtime state.

**Verified coverage (audited against every `EnqueueMutation` call site):**

| Damage path | Through the gate? |
|---|---|
| Effect-grant instant damage (`EffectBag` → dispatcher) | yes |
| Counter/bond bursts (`EffectBag.TryFireCounterBurst`) | yes |
| **Status DoT pulses** (`StatusRuntime.Tick` → `StatusFunnelPulseSink` → dispatcher) | **yes — DoTs are absorbable, locked.** The pulse carries its status element as one full-weight component for `elemMod`; with the chip floor (§2.4) every tick spends ≥ 10% of its size, making sustained DoT the natural anti-shield pressure. No `bypassShield` flag in v1 (ask-first). |
| Debug `enqueue-delta` with target spec / element payload | yes |
| Debug `enqueue-delta` bare amount; funnel secondary envelope | no — **bypass by design** (raw funnel testing stays raw) |
| `debug.combat.probe` overlay branch | currently enqueues the computed delta directly — **in scope: re-route through `DispatchInstant`** so the live shield proof (§11.3) actually exercises the gate |
| Standalone `BattleEngine` | **not today** — mutates actor HP directly; battle-local funnel/dispatcher is the unimplemented match-source-core wave C2. See §10. |
| Vanilla Unity damage | never reaches the gate — owner decision 1 holds by construction |

Positive deltas (heals) never touch shields. The gate's no-shield fast path is one dictionary miss.

### 2.3 Derived channels (28 new: 4 families × {omni + 6 elements}, catalog 56 → 84)

Four families join `CombatChannelFamilies`; `AllCombatChannelIds` generates them from `ElementRoster` — flat-sum, default 0, additive omni. Audit confirmed all four catalog consumers auto-scale; nothing else enumerates the families.

| Family | Side | Consumer |
|---|---|---|
| `combat.shield.capacity.{omni\|el}` | owner | flat bonus to `maxHp`, read at the §2.6 barrier (and re-read on merge) |
| `combat.shield.toughness.{omni\|el}` | owner | reduces damage dealt to the shield pool (§2.4) |
| `combat.shield.pen.{omni\|el}` | attacker | increases damage dealt to the shield pool (§2.4) |
| `combat.shield.regen.{omni\|el}` | owner | shield HP per second, front-shield rule (§2.6), permille carry accumulation |

Untyped (`element = none`) shields read the **omni half only** for toughness/regen; attacker pen against them likewise uses `pen.omni` only; `elemMod = 0`.

**Known churn (exact, from audit):** `DerivedStatRegistryTests` line asserting `Assert.Equal(56, expected)` (+ its comment/test name), the `8 × 7 = 56` doc comment in `DerivedStatChannels.cs`, and the "**56 combat derived channels**" figure in the decisions.md Element Hub row. `CombatDerivedReader` gains four element→channel switch maps; the exhaustiveness walk (§7) covers them.

### 2.4 Absorb math (locked — clamped, HitCount-aware, permille integer)

All `ShieldMath` arithmetic is **64-bit integer at ×1000 permille scale**; policy constants are permille ints:

```text
ShieldMatchupShareKPm = 250     ShieldChipFloorKPm = 100     ShieldPenCapKPm = 3000
```

Per coalesced damage record (`input(1) = |finalized signed delta|`, `hitCount` from the record, 1 if uncoalesced), cascading over **all** active shields in drain order — priority **descending**, `createdSeq` ascending as tiebreak:

```text
for shield i while input > 0:
  elemMod(i)      = Σ over ORIGINAL payload components (w × relUnit(compEl, shield(i).element))
                      × ShieldMatchupShareKPm × input(i) / 1000
  breakerDelta(i) = pen(attacker, omni + shield(i).element)
                    − toughness(owner, omni + shield(i).element)
  raw(i)          = input(i) + elemMod(i) + hitCount × breakerDelta(i)
  damageToShield  = clamp(raw(i), ceilPm(ShieldChipFloorKPm × input(i)),
                                  ShieldPenCapKPm × input(i) / 1000)
  spent           = min(shieldHp(i), damageToShield)
  input(i+1)      = input(i) == 0 ? 0
                  : (input(i) × (damageToShield − spent) + damageToShield/2) / damageToShield
final input → Funnel hp channel
```

Locks and rationale (each closes an audit finding):

- **Chip floor** (`≥ 0.10 × input`, ceiling-rounded): toughness saturates at 10× shield efficiency. Immunity is impossible by construction — the shield always spends — and the old `damageToShield == 0` fork (permanent-immunity vs full-bypass, both broken) is unreachable except at `input = 0`, where the remainder is 0.
- **Pen cap** (`≤ 3 × input`): pen at best triples shield burn; a 2-damage chip hit can no longer delete a 200-HP shield, and per-layer capping bounds the whole cascade. The floor/cap asymmetry (0.1× vs 3×) is deliberate: defense saturates harder so shields can't be made unkillable.
- **`hitCount × breakerDelta`**: the drain coalesces same-key hits (`Amount` summed, `HitCount = n`), and a flat per-record delta would make shield burn depend on frame load. Scaling by `hitCount` makes coalesced ≡ n× uncoalesced algebraically — the same idiom the pipeline uses for proc chance. `elemMod`, the floor, and the cap are proportional to `input` and compose safely as-is.
- **`relUnit` is the unit relation `−1 | 0 | +1`** from `ShieldElementMatrix`; K is applied exactly once in `elemMod`. This deliberately differs from `ElementRingMatrix.relationShare` (which bakes K in) — the seed-equality golden compares *relations*, not shares, so the K² double-scaling ambiguity is closed.
- **Rounding:** round half away from zero via `(num + d/2) / d` (all operands non-negative); `ceilPm(x) = (x + 999) / 1000`. Never C# `Math.Round` (banker's).
- **Invariants (golden-enforced):** `0 ≤ input(i+1) ≤ input(i)`; `spent ≤ min(shieldHp, damageToShield)`; `shieldHp ≥ damageToShield → input(i+1) = 0` exactly; remainder monotone non-increasing in `shieldHp`. Overflow: `long` everywhere; safe to ~3×10⁹ HP-permille — clamp `input` at gate entry to a sane content bound.
- **Chosen tie golden:** `input = 1, d = 2, spent = 1` → remainder `= 1` (half-away-from-zero: shield spent HP yet full damage leaked on the 1-HP hit). Locked as an explicit golden, not a surprise.

**Worked cascade golden (locked):** 240 fire hit (one component, w = 1.0), no pen/toughness, stack S1 ice 60 HP → S2 earth 100 HP → S3 untyped 200 HP:
S1: fire→ice STR, `elemMod = +60`, `d = 300`, spent 60, remainder `round(240 × 240/300) = 192`.
S2: NEU, `d = 192`, spent 100, remainder `round(192 × 92/192) = 92`.
S3: untyped, `d = 92 ≤ 200` → holds, spent 92, remainder **0**. HP takes 0; 252 shield spent on a 240 hit.

Note per-layer `breakerDelta` charges flat pen fresh at every layer — bounded by the per-layer cap, and stated so it's a choice, not an accident. No rolls anywhere; `SeededRng` streams are not consumed.

### 2.5 Stacking and conflict policy

- Cap: **3 active shields per actor** (innate counts; no protected slot in v1).
- **Merge** on same `(sourceId, element)`: always refresh expiry and recompute `maxHp` from current capacity channels; then `hp = new maxHp` if `refillOnMerge` (skill/effect recast = a paid refill) else `hp = min(currentHp, new maxHp)` (aura re-assert is genuinely idempotent — re-asserting an undamaged aura changes nothing, and a capacity downgrade clamps, never heals). Merge recompute reaching `maxHp ≤ 0` removes the instance as `shield.expired`.
- **Admission at cap** for a distinct new shield: admitted **only if `newMaxHp > weakest.currentHp`**; otherwise dropped (debug line, no event). This kills the griefing case where a 20-HP aura trickle evicts a 400-HP shield every pulse. An evicted shield emits `shield.expired`, **not** `shield.broken` (no break VFX for an administrative eviction).
- One `sourceId` may hold multiple slots via different elements (merge keys on the pair) — an element-swapping source costs a slot per element; stated so aura authors know.
- Drain order: priority **descending** (outer-to-core), `createdSeq` ascending tiebreak — deterministic; Chaos's dynamic HP-percentage priority is rejected (§9).
- Grants with `maxHp ≤ 0` (negative capacity contributors) are rejected at apply (debug line, no instance, no event).

### 2.6 Lifecycle, targeting, events, surfacing

**Tick host and cadence (audit-corrected).** Overlay: `ShieldRuntime.Tick` joins `InjectorLoop` on the same **100 ms grid** as `EffectRuntime.TickDots`, behind its **own** `HasAnyInstances()` guard (the TickDots guard early-returns when no *status* exists — shields must not ride it). The per-frame event drain (dispatch/absorb) and the 100 ms shield tick are different cadences; locked frame order: **drain dispatch first, then shield upkeep** (regen → expiry/prune → emit `shield.expired`). Consequences, stated: a shield expiring at tick T still absorbs that frame's drained damage; a shield broken during dispatch is pruned immediately with one `shield.broken` and same-tick regen never revives it; a 1-HP survivor regens normally. Death/lifecycle flush clears the actor's shields inside the existing barriers. Standalone: the engine adopts the same relative order (dispatch before upkeep) within a round when C2 lands.

**Regen (front-shield rule).** Per shield tick, exactly **one** instance per actor regenerates: the first in drain order with `hp < maxHp`. Amount = `regen.omni + regen.{itsElement}` (omni only for untyped) interpreted as **HP per second**, accumulated on the 100 ms grid in permille with carry (deterministic, no drift). Capped at `maxHp`, no spill. Total actor regen is independent of shield count — no omni multi-dip.

**Innate grant barrier.** Innate grants are *queued* at actor registration (overlay hook: the injector entity registry's `Add`; standalone: the spawn loop reading a new innate field on `BattleActorSetup`) and *applied on the first shield tick after the owner's derived snapshot is complete* — capacity is read there, not at raw registration, so contributor load order can't fork live vs replay values. Registry resyncs re-fire `Add`; the stable `innate:{typeId}` sourceId + merge rule makes that a no-op. Innate lookup keys on `TypeId` (the identity the board snapshot actually carries).

**Aura targeting (in scope — owner decision 3, audit-corrected vocabulary and scope split).**

- *Cadence:* auras ride the **existing `OnTimer` effect trigger** — an aura is an effect grant whose timer fires the `shield.grant` action on its period. No new "trait pulse" mechanism (none exists; the audit confirmed trait→grant wiring was explicitly deferred out of demon V1).
- *Targeting:* `shield.grant` accepts the **actual** `TargetResolver` vocabulary — `Actor` (self), `Area` with shapes `Row` (lane), `Square`/`Rectangle`, plus the existing pool filters (`side`, `row`/`col`, `excludeMindControlled`). There is no radius shape and no ally-relative side filter today; v1 auras use `Square` areas and an explicit absolute `side` from the grant content. Adding a true radius shape or a `side: "same"` relative token is a `TargetResolver` (shared combat file) extension — **ask first**.
- *Application:* one `ShieldRuntime.Apply` per resolved target, `refillOnMerge = false`, idempotent via §2.5. Aura work runs on grant cadence, never per hit.
- *Scope split:* this spec ships and tests the full aura path (OnTimer grant → area resolve → multi-apply). Wiring `trait.guardian`'s grant template to demon deploys stays with the demon stream, which owns trait→grant templates.

**Events (audit-corrected: string envelope stream, not the hot ring).** `shield.granted`, `shield.absorbed`, `shield.broken`, `shield.expired` are **observability events on the string event stream** — shields are Core runtime state like statuses, and putting them in the v2 ring would cost 4 enum slots, `ToDto` arms in a fail-open switch, and a contradiction with `IsCoalescible`'s `SourceGrantIdx < 0` rule. Instead:

- `ShieldRuntime` aggregates absorption itself: one `shield.absorbed` per `(ownerKey, shieldId)` per drain flush window (summed amount + hit count in the payload) — per-shield breakdown survives for the debug UI by construction. A `shield.broken` flushes that shield's pending aggregate first so events stay ordered for VFX.
- `shield.absorbed` joins `RpgConstants.IsNoisyKind` so it doesn't flood the SignalR live feed; the other three kinds are low-frequency and broadcast normally.
- Server ingest needs no change (no kind whitelist exists; unknown kinds store harmlessly); `docs/protocol/events.md` gains the four kinds + payload keys.
- Nothing procs off shield events in v1 (stated: no `OnShieldBroken` trigger yet — ask-first).

**VFX.** `shield.broken` gets a new `VfxCueIds` const + core recipe entry (a shield cue is not a status id — it does not fit the status-keyed seed table; audit-verified). Cue art/tuning stays with the VFX stream.

**Debug grant (owner decision 10).** A debug/cheat action grants a shield to a selected ptr (base, element, duration, priority default 20) through `ShieldRuntime.Apply` — same command pattern as `enqueue-delta`, never a Funnel write. This is the live-proof grant surface for §11.3.

**UI / payload surface (audit-corrected).** There is no per-actor DTO in Contracts to extend. The real surface is the untyped per-actor dump dictionaries (Core `SimEngine.PlantDump`/`ZombieDump` + the injector's GameDumps) flowing to the web lawn projector. New keys: **`rpgShieldHp` / `rpgShieldMax`** — deliberately distinct because the web fold already maps vanilla `theShieldHealth`/`theShieldMaxHealth` into the **armor** bar; reusing those keys would silently merge the RPG shield into vanilla armor and violate owner decision 7. Web renders a separate shield bar from the new keys (additive fold change); debug boards add per-shield lines (`element`, `hp/maxHp`, `spent` last window); the overlay damage debug output gains `absorbed` / `hpRemainder` breakdown lines.

### 2.7 Perf budget

Absorb path is hot (inside the drain): O(1) owner lookup, zero allocation on the no-shield fast path (single `TryGetValue` miss), no LINQ in `Absorb`/`Tick`, event aggregation reuses pooled buffers. Shield tick is 10 Hz behind its own instance guard. New `PerfProbe` section `ShieldAbsorb`. Gate: stress scenario re-run shows no measurable pipeline-share regression with zero shields active (§11.7).

## 3. Tech stack

C# / .NET as the repo stands. New code in `FusionRpg.Core` (engine-agnostic; no Unity, no SQL) plus the small injector/web wiring named in §5.

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
src/FusionRpg.Core/Stats/Derived/     → DerivedStatChannels.cs (+4 families; fix the 56 doc comment)
src/FusionRpg.Core/Combat/            → CombatDamageDispatcher.cs (gate), CombatDerivedReader.cs (+4 maps)
src/FusionRpg.Core/SimEngine.cs       → dump keys rpgShieldHp/rpgShieldMax
src/FusionRpg.Injector/               → InjectorLoop (shield tick), GameDumps (keys),
                                        DebugCombatActions (probe re-route through dispatcher,
                                        debug board shield lines), InjectorEntityRegistry (innate queue)
src/FusionRpg.Contracts/Dtos.cs       → RpgConstants.IsNoisyKind += shield.absorbed
web/fusion-rpg-web/                   → lawn fold + view model: separate shield bar from rpgShieldHp keys
tests/FusionRpg.Core.Tests/Combat/Shield/ → goldens + integration (see §7)
docs/protocol/events.md               → four shield.* kinds + payload keys
docs/architecture/decisions.md        → amendment row (shield layer above Funnel — unlocks element-hub ban)
                                        + Element Hub row channel count 56 → 84
docs/architecture/element-hub-ssot.md → §13 ban-list line gains a pointer to this spec
```

## 6. Code style

Match `StatusRuntime` / `OverlayCombatCalculator` idiom — sealed classes, explicit permille policy constants, no LINQ on hot paths, channels generated from `ElementRoster`.

**Guard naming discipline (audit landmine):** `guard-funnel-delta.ps1` greps **all of Core** for the bare substrings `EntityStatWriter`, `AddPlantHp`, `AddZombieHp`, `targetPtrs`. `Core/Combat/Shield/` must not contain those literals **in code or comments** — e.g. the natural variable name `targetPtrs` in `ShieldAuraGrants` fails CI; use `resolvedOwners`.

## 7. Testing strategy

- **Math goldens** — generated over `ElementRoster` × relation × {pen, toughness} × {hold, exact-break, overflow, multi-shield}, now including: chip-floor engagement (toughness > input), pen-cap engagement (pen ≫ input), **`hitCount > 1` rows with a coalesced ≡ n× uncoalesced equivalence assert**, untyped-shield (`none`, omni-only reads) rows, the `input=1, d=2` tie golden, `input=0` → remainder 0, and the §2.4 invariants.
- **Cascade golden** — the worked 240-fire / ice-earth-untyped example locked byte-exact, plus per-layer flat-pen compounding cases under the cap.
- **Matrix goldens** — full `ShieldElementMatrix` from `ElementRoster` (fail-open defense), seed-equality vs `ElementRingMatrix` **comparing unit relations, not K-scaled shares**.
- **Stacking/policy** — cap, merge refresh (both `refillOnMerge` values, capacity-downgrade clamp), admission rule (`newMaxHp > weakest.currentHp`, eviction emits `expired` not `broken`), `maxHp ≤ 0` rejection, multi-element same-source slots, drain-order determinism.
- **Lifecycle** — regen front-shield rule + permille carry + no multi-dip, tick expiry, frame-order consequences (expiring shield absorbs its last frame; broken never revives same tick), death-flush clears, innate queue-then-apply barrier (capacity read after snapshot completeness), resync re-fire idempotency, no-shield fast path byte-identical (regression lock on every existing combat golden).
- **Aura path** — OnTimer grant → `Area/Row` and `Square` resolve → multi-apply idempotent via merge; no per-hit aura work.
- **Dispatcher integration** — instant damage and **status DoT ticks** → shield spends, Funnel receives exactly the remainder; heals bypass; re-routed `debug.combat.probe` absorbs; guard scripts green.
- **Events** — per-(owner, shieldId) aggregation, broken-flushes-pending ordering, `IsNoisyKind` suppression.
- **Exhaustiveness walk** — all 28 channels through `CombatDerivedReader` without throwing; roster-driven.
- **Determinism** — `ShieldMath` + `ShieldRuntime` replay-exact under fixed inputs now; the full standalone-battle replay E2E lands with Battle-C2 (§10) and is specified here so C2 inherits it.

## 8. Boundaries

- **Always:** absorb strictly above the Funnel; Funnel stays hp-only add-only FA10; permille `long` math with the locked rounding rule; additive flat-sum channels with additive omni; regression-lock the no-shield path; guard naming discipline (§6); update decisions.md + element-hub-ssot.md in the same change; run all four guards.
- **Ask first:** reflection/immunity shield types; percent penetration; a `bypassShield` damage flag; `OnShieldBroken` proc triggers; changing `ShieldMatchupShareKPm`/`ShieldChipFloorKPm`/`ShieldPenCapKPm`, the cap of 3, or drain order; diverging `ShieldElementMatrix` from the ring seed; protected innate slots; regen after-hit delay; radius shape or ally-relative side in `TargetResolver`; shield kinds in the v2 ring enum; any non-additive web/payload change.
- **Never:** absorb vanilla PVZ damage (locked by owner — we extend our combat, we don't change the game); write Unity HP or `theShieldHealth`; reuse the `theShieldHealth`/`theShieldMaxHealth` payload keys; a new Funnel channel; YAML/runtime shield registry; `System.Random`; float in game-affecting shield math.

## 9. Rejected Chaos paths (v1)

Shield-as-actor with subsystem registration; ImmunityShield / ReflectionShield / absorb-reflect percentages; per-type conflict-policy matrix (`shields.yaml`) and PvP/area override precedence; percent penetration with per-type caps; dynamic priority (`base + hp% + type modifier`); per-second lifetime decay; restoration-event lists; multi-tier resource distribution; damage impact maps splitting into mana/qi.

## 10. Dependencies and sequencing

- **Standalone absorption is blocked on Battle-C2** (match-source-core: battle-local `EffectFunnel` + shared combat channels; today `BattleEngine` mutates HP directly and never touches the dispatcher). This spec keeps `ShieldRuntime`/`ShieldMath` engine-agnostic and Battle-C2 inherits shields by routing through the shared dispatcher — but "same numbers in both modes" is a claim C2 completes, not this spec. §11 splits the criteria accordingly.
- **`trait.guardian` template wiring** belongs to the demon deploy-facing module (deferred there by demon V1); this spec delivers the aura mechanism it will call.
- **VFX cue art** for `shield.broken` lands via the VFX stream; this spec registers the cue id and emit point.

## 11. Success criteria

1. All existing tests pass with only the audited churn (§2.3): the `56` registry literal (+name/comment), the `DerivedStatChannels` doc comment, the decisions.md count row. No-shield combat output byte-identical; vanilla damage untouched by construction.
2. Math, cascade, matrix goldens and the exhaustiveness walk green, including HitCount-equivalence and clamp-boundary rows; `ShieldMath` deterministic replay green.
3. A granted shield absorbs an overlay hit end-to-end in an integration test (dispatcher → partial remainder in Funnel), and live: debug grant endpoint → **re-routed** `debug.combat.probe` shows absorption on a running game.
4. A status DoT tick demonstrably spends shield and delivers only the remainder to the Funnel.
5. An innate content-row shield applies through the queue barrier at registration; an OnTimer aura grant shields a resolved area idempotently.
6. `shield.broken` observable on the event stream with its cue id registered; `shield.absorbed` aggregates per (owner, shield) and is noise-suppressed on the live feed.
7. All four guard scripts green (including the §6 naming discipline); stress scenario shows no measurable pipeline-share regression with zero shields active.
8. decisions.md amendment recorded; element-hub-ssot ban line updated; `docs/protocol/events.md` updated.

## 12. Audit trail (2026-08-21)

Two-lens audit on draft v2: a design red-team (16 findings — 3 critical) and a code-integration verification of all 11 structural claims. Material outcomes folded above: absorb math re-locked with chip floor + pen cap + HitCount scaling (was: immunity/bypass fork, coalescing nondeterminism, chip-hit shield stripping); cascade written out with a locked worked example; unit-relation matrix contract (was: K² double-scale ambiguity); admission rule (was: trickle-evicts-tank griefing); `refillOnMerge` split (was: aura full-heal exploit + downgrade surprise); innate capacity barrier (was: registration/contributor race); permille rounding lock; DoT absorption made explicit and verified gated in code; front-shield regen (was: omni multi-dip); tick order + cadence corrected to the real hosts; events moved to the string stream with runtime aggregation (was: v2-ring contradiction); aura re-specced onto `OnTimer` + real `TargetResolver` vocabulary (was: nonexistent trait-pulse/radius); payload keys de-collided from the vanilla armor mapping (was: wrong DTO pointer); guard naming landmine documented; standalone dependency made explicit (was: claimed free, actually blocked on Battle-C2).
