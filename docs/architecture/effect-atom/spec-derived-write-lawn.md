# Spec: `derived-write-lawn`

**Program:** effect-atom · **Map:** [../effect-atom-map.md](../effect-atom-map.md) ·
**Definitions (wins over this spec):** [definitions.md](definitions.md) ·
**Catalog SSOT:** [../actor-hub-ssot.md](../actor-hub-ssot.md) ·
**Kind matrix:** [atom-catalog-ssot.md](atom-catalog-ssot.md)

**Status: BUILT and PROVEN LIVE end to end, 2026-08-30 — `A5` included, as worded.**
`decisions.md` carries the "Derived-write lawn executor" row that authorised clearing D6 for the lawn.

**A5 evidence** — *"a bound **aura** raises **`combat.power.omni`** on a live lawn plant"*, with a
falsifier cycle on a real running game:

| State | plant `278F9CF7480` `combat.power.omni` |
|---|---|
| before grant | **803** |
| **Might aura granted** | **13210** |
| withdrawn | **803** |

**Δ = 12407 = `AuraMagnitude.Compute(rung: 10, share: 1.0, pTheta: 1000)`** — the shipped formula's own
output. The aura was authored as a real `world-buff` container (`ContainerKind.WorldBuff` already
existed) holding one `stat.derived` atom on the exact channel `AuraContentCatalog`'s Might aura
declares. A second, earlier cycle proved the same path with the shipped `trait.critical-hunter` atom on
`combat.crit.rate.omni` (150 → absent → 150).

**Probe fixtures were removed afterwards** — the DB is back to `effect_binding = 0` and its two
original containers. What remains owed elsewhere: **E20-E25's production binding producer**, and (for
shipped auras rather than a probe) **T16's aura-container authoring**, whose only remaining objection
is the balance-coefficient one — its "nothing reads a container" ground is void, this executor reads it.

> ⚠️ **Read the three dated corrections inside §"Build status" before trusting any older sentence in
> this document.** Two claims that were written here as fact turned out to be false when tested against
> code: *"this module's own half is done"* (the executor was **inert in production** — it read a
> transport nothing populates, and used bare owner keys that matched nothing), and the single-line
> blocker citation (`EffectBag.cs:196`), which was neither the whole blocker nor the one that mattered.
> Both are corrected in place rather than quietly overwritten, because the way they were wrong is the
> useful part.

---

## 1. Objective

Give the **lawn** an executor for `stat.derived`, so that a derived-channel write — an aura, a patron
bonus, a star, an injury, a contract modifier — reaches a live plant or zombie **through the atom
runtime** instead of through a private path invented per feature.

### The problem, stated as measured fact

`stat.derived` is the atom kind whose entire purpose is "direct derived-channel mods"
([AtomKindRegistry.cs:152](../../../src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs)). Its runtime
matrix is:

```csharp
// AtomKindRegistry.cs:149
new RuntimeSupportMatrix(RuntimeState.None, RuntimeState.Full, RuntimeState.None)
//                       ^ Lawn = None       ^ Battle = Full     ^ Sim = None
```

D6 quarantined all three in 2026-08-22 because the kind had no executor anywhere — *"A bind would have
been accepted and then done nothing forever, which is the exact failure this module exists to
prevent."* E12 then built the **battle** consumer only (`BattleStatComposer` via `TraitAtomSource`), and
the comment on that line is explicit that lawn and sim stay `None` deliberately.

**Consequence:** there is no legal atom path from an aura to a lawn entity. Not an oversight in the
aura program — an absent capability.

### The cost, already documented and now paid a fifth time

[actor-hub-ssot.md §6.1](../actor-hub-ssot.md) found four features writing derived channels with no
subsystem row and no opcode — **patron, stars, injuries, contracts** — and named the cause:

> Four features grew their own path **because there was no opcode to use**. … the catalog validates
> *which channel ids* are legal, but nothing validates *who may write them*.
> **Rule to adopt when that lands:** a derived write needs both a registered *channel* and a registered
> *producer*. **Only half of that is enforced today.**

Commander aptitudes became the fifth (`AptitudeSubsystem`, an `IActorStatSubsystem` registered directly
on `ActorHub`). The prediction in §6.1 has now been confirmed by a live incident, not just asserted.

## 2. Scope

**In:**

1. A lawn executor for `stat.derived`, flipping its Lawn cell `None → Full` (or `Partial` with the side
   path named, per [definitions.md §9](definitions.md)).
2. The producer-registration rule from §6.1: a derived write requires a registered channel **and** a
   registered producer.
3. Migration path for the five ad-hoc producers — **named and sequenced, not executed here.**

**Out:**

- Sim (`SimEffectHost`) — no consumer, and flipping it on the strength of the lawn's would repeat
  exactly the mistake D6's comment warns against. It stays `None` until it earns a consumer.
- Any change to the Foundation contract, FA opcodes, or the Funnel. This is a derived-channel writer,
  not a damage path.
- Aura *content*. This spec builds the road; `aura-content` already authored the cargo.

## 3. Where the executor attaches — **extend the built scope primitive, do not build a second one**

> **Corrected 2026-08-30 while completing the gate checklist.** An earlier draft of this section
> proposed a fresh `AtomDerivedSubsystem`. Reading `decisions.md` in full falsified it: the
> **buff/debuff scope** program (2026-08-29) already **built and verified** exactly this mechanism, and
> proposing a parallel one would have been the very defect this spec exists to close — a sixth private
> path. The correction is kept visible because it is the same mistake in miniature.

Shipped and green as of 2026-08-29 (`buff-debuff-scope-todo.md` T1–T13, zero goldens moved):

| Piece | File | What it already does |
|---|---|---|
| `ScopeCompatibility` | `Core/Scope/ScopeCompatibility.cs` | Table keyed `(AtomKindId, Where, Who, Host, Channel)` → delivery shape. **Explicitly not inferred from kind metadata** |
| `BattlefieldOwnSideReactor` | `Core/Battle/BattlefieldOwnSideReactor.cs` | Event-driven per-entity grant/withdraw. **Validates kind/host/channel against the table at construction**, so an inert grant is impossible to build |
| `ScopeMembershipEvent` | `Core/Match/ScopeMembershipEvents.cs` | Bound / Cleared / MindControlToggled — never polled, never rescanned |
| `IOwnSideOracle` | same file | `MechanicalOwnSideOracle` + `SpecimenOwnershipOracle` (aura-skill T21b) both shipped |

**So the road exists.** What is missing is narrow and precise:

1. **`ScopeCompatibility` has exactly two rows**, both `stat.modify` + `defense`
   (`ScopeCompatibility.cs:53,60`) — the G8 case that motivated the table. **There is no `stat.derived`
   row at any host.**
2. **`stat.derived`'s Lawn cell is `None`** (`AtomKindRegistry.cs:149`), so even with a table row a bind
   would be refused.
3. **No production call site constructs a reactor with a real oracle** — `DebugScopeRuntime.cs` still
   builds only `AlwaysRelationOracle`. `decisions.md` names this precisely: *"wiring either real oracle
   into a live per-aura/per-effect reactor is a separate, later task, not scope creep introduced here."*

**This spec is that named later task.** Its work is therefore:

```text
add stat.derived rows to ScopeCompatibility   (data, per host/who/channel — reviewed)
   + flip AtomKindRegistry Lawn None → Full/Partial   (clears D6 for lawn only)
   + construct BattlefieldOwnSideReactor with a real oracle at a production call site
        └─► grants land in EffectBag per entity (already works)
               └─► ActorHub.ResolveDerived folds them (already works)
                      └─► AppliedCombat → EntityStatWriter (already works, value-gated §5)
```

Every arrow after the first three already exists and is tested. **No new subsystem, no new read model,
no second delivery path.**

### Owner-key scoping

Handled by the shipped grammar — `match` / `plant:N` / `zombie:N` / `entity:{ptr}`, with
`instance:{guid}` translated to `entity:{ptr}` by the binder at Bound
([unique-entity-effects.md](../unique-entity-effects.md)). Nothing new is owed here.

### G8 is already decided and must be honoured

[definitions.md §6](definitions.md): `stat.modify` on `defense` is legal **only** at `match` scope,
because the `TakeDamage` prefix reads one side-wide cached value. Per-actor mitigation is
`stat.derived` on `combat.defense.*` — which is precisely what this executor enables. That is a
capability this spec unlocks, and its own acceptance row.

## 4. Runtime cost — the constraint that decides the design

The perf SSOT is unambiguous ([DESIGN-GATE §1](../../DESIGN-GATE.md) Performance row): lag is
**main-thread scans and uncached resolves**. A derived resolve already happens per entity per apply;
this must not add a scan or a per-hit lookup.

| Rule | Why |
|---|---|
| Bound-atom set is **cached per `owner_key`**, invalidated on bind/withdraw | A per-resolve query over bindings is the uncached-resolve defect by another name |
| The subsystem holds **no state between calls** and `ContributeDerived` stays pure | `AptitudeSubsystem`'s own contract; makes double-registration harmless |
| No `FindObjectsOfType`, no board scan, no SQLite | Injector stays SQL-free ([match-runtime.md §10](../match-runtime.md) lock 11) |
| Measured against the ≤ **50 ns/atom** budget | [definitions.md §11](definitions.md)'s stated method, on the CI reference machine |

## 5. What is already built and must not be re-litigated

**The delivery guarantee shipped 2026-08-30** and this spec depends on it rather than restating it.
`EntityApply` previously decided whether to write by enumerating contributors, so a producer absent
from that list composed correctly and was dropped silently. It now compares values:

```csharp
// EntityApply.RunPlant / RunZombie
var shouldWrite = forceReapply || final.DiffersFrom(baseline);
```

`EntityFinal.DiffersFrom` ([EntityBaseline.cs](../../../src/FusionRpg.Core/Stats/EntityBaseline.cs)) is
source-agnostic, so **a `stat.derived` lawn executor's output reaches Unity the day it exists, with no
edit to `EntityApply`.** Pinned by
`AppliedCombatReachesWriterTests.A_brand_new_derived_producer_reaches_the_writer_input_without_any_gate_edit`.

Without that fix this spec would have shipped an executor that composed correctly and wrote nothing —
which is the same silent no-op D6 exists to prevent, one layer down.

## 6. Acceptance

| # | Criterion | Proof |
|---|---|---|
| A1 | A `stat.derived` atom bound at `entity:{ptr}` moves that entity's derived channel and no other entity's | Core test with two entities |
| A2 | The same atom at `match` scope moves every living entity | Core test |
| A3 | Withdraw removes the contribution; ptr reuse never inherits it | Core test + the shipped withdraw-on-die invariant |
| A4 | `combat.defense.*` per-actor works (G8's named unlock) | Core test asserting the channel, plus a live overlay probe |
| A5 | A bound aura raises `combat.power.omni` on a **live lawn** plant and the value reaches `OverlayCombatCalculator` | LIVE probe — ⚠️ **blocked, see below** |
| A6 | Zero goldens move | Run the suites; do not assert it |
| A7 | ≤ 50 ns/atom, no new main-thread scan | `PerfProbe` section + the §11 method |
| A8 | Sim stays `None`; binding there still rejects `RuntimeUnsupported` | Kind-matrix test |

### Build status, 2026-08-30 — everything except A5

**Built, tested, and live-confirmed registered:** the executor
(`Stats/Derived/Subsystems/AtomDerivedSubsystem.cs`), its injector reader
(`Injector/Stats/GrantedDerivedAtoms.cs`), the `ScopeCompatibility` rows, and the Lawn matrix flip —
flipped **last**, so the D6 state (binds accepted, nothing applied) never existed even briefly.
17 tests in `AtomDerivedSubsystemTests` cover A1–A4 and A8; all 7 suites green (5,809). A live probe
confirms the subsystem is registered on a real lawn: `debug.aptitude-trace` reports
`subsystems=rpg.progression,rpg.aptitude,atom.derived`.

**A5 is blocked on a dependency outside this module, verified not assumed.** `EffectBag.Grant`
refuses any grant whose `EffectId` is absent from its def catalog
(`EffectBag.cs:196-197`, *"unknown effect_id"*) — confirmed live, the probe grant was rejected with
exactly that message. So an aura can only be granted on the lawn once a def **compiled from a
`stat.derived` atom** reaches that catalog, which is the atom→compile→bind chain the completeness
audit already records as the program's missing links (*"a loader, an importer run, and a producer of
bindings — so most of this layer does not reach the running game"*, **Wave 6 / E20–E25**).

This module's own half is done: the moment such a def is grantable, the executor consumes it — the
same "reaches Unity the day it exists" property §5 gives the delivery half. **A5 should be proven as
part of Wave 6, not re-litigated here.**

> #### ⛔ Correction, 2026-08-30 — the citation above is incomplete, and half of it is not a blocker
>
> Re-checked against code while building `aura-skill` TC2 (DESIGN-GATE: *"test the constraint before
> you declare it"*). The `EffectBag.cs:196` catalog rejection above is **real on the live lawn**, so
> A5 — which is a **LIVE probe** — remains correctly blocked. But it is *not* the whole blocker, and
> it is not the one that matters for an offline test:
>
> | | Verdict |
> |---|---|
> | `EffectBag.Grant` unknown `EffectId` (`EffectBag.cs:196`) | Real for the **live** lawn. **Not** a testability wall: `EffectBag` takes an `IEffectCatalog` by ctor injection (`EffectBag.cs:144-150`) and `EffectBagTests.cs:121` already registers its own defs via `InMemoryEffectCatalog`. A **content** gap. |
> | **`EffectOverlayMerge.AllowedByAction` (`EffectProcAndOwner.cs:130-154`)** | **The structural blocker, previously unrecorded.** Overlay keys are whitelisted per action across ten actions, **none of them a derived-stat action** — so even with the def registered, a grant carrying `derived.channel` is refused as `unknown overlay key`. |
>
> #### ⛔⛔ Second correction, same day — **"this module's own half is done" was FALSE**
>
> The sentence above claims *"the moment such a def is grantable, the executor consumes it."* Probing
> the real grant path proved it would consume **nothing**, for two independent reasons, both now fixed
> or pinned:
>
> 1. **Wrong transport.** `BattlefieldOwnSideReactor.BuildGrant` — the only production grant path —
>    emits `GrantId`/`EffectId`/`OwnerKind`/`OwnerKey` and **no `Overlay` at all**. The reader read
>    `grant.Overlay`. Independently confirmed: **no file under `src/` ever writes
>    `derived.channel`/`derived.op`/`derived.amount` onto a grant.** The values are meant to live on the
>    compiled def's **action-row params** (the `stat.derived` ParamSchema names them
>    `channel`/`op`/`amount`) — a different transport. Pinned by
>    `AuraDeliveryLawnTests.The_production_grant_shape_carries_no_overlay_so_the_reader_is_inert_today`.
> 2. **Wrong owner keys — a real bug, now FIXED.** The reader passed `ctx.TypeId.ToString()` and
>    `ctx.EntityKey` **bare**, while the shipped grammar (and every real grant) uses `plant:{typeId}` /
>    `entity:{ptr}`. `ForOwner` compares `StatApplyScope.Normalize` on both sides, and that normaliser
>    is **not** prefix-agnostic — it maps `entity:0xAB` → `entity:ab` but leaves a bare `0xAB` as
>    `0xab`. So **two of the three owner scopes matched nothing**; only `match` worked, and it hid the
>    bug. Fixed in `GrantedDerivedAtomReader` to use `EffectOwnerKeys.*`; falsifier (reverting to bare
>    keys) turns **5 of 8** `AuraDeliveryLawnTests` red.
>
> **The compile chain is broken too**: `AtomCompiler.OpcodeOf` maps eleven kinds to opcodes and
> `stat.derived` falls through to `null`, so a compiled `stat.derived` atom gets **no action row** —
> hence no params for anyone to read. Four missing links total, each pinned as a deliberately
> fails-when-fixed assertion in `tests/FusionRpg.Core.Tests/Atoms/StatDerivedCompileGapTests.cs`.
>
> **So `AtomKindRegistry`'s `Lawn = Full` should be read as "a consumer exists and composes correctly",
> NOT as "the path is live end to end."** It is not, yet.
>
> #### ✅ Third and final correction, same day — **the work order above was COMPLETED, not handed off**
>
> All five links were built and verified in this session:
> `EffectActions.ModifyDerivedStat` + its `AllowedByAction` row (keyed to the **compiled** op-as-key
> shape, since `ToOpcodeShape` rewrites `{op, amount}` → `{flat: N}`) + `AtomCompiler.OpcodeOf` +
> **`Compilability.OpcodeKinds`** (the decisive one — without it `Classify` returned `Runner`, so the
> kind never became an `EffectDef`) + a catalog-aware `GrantedDerivedAtomReader` wired through the
> injector adapter.
>
> **No goldens and no content hashes moved** — measured before keeping the change.
>
> `A5`'s offline half is therefore **proven end to end**: an atom compiled by the real `AtomCompiler`,
> granted through the real `EffectBag.Grant`, reaching `combat.power.omni` on a lawn plant. **Only the
> LIVE probe remains** — it needs a running game, not code.
>
> Consequence: the offline half of A5 is **no longer blocked and has been built** —
> `tests/FusionRpg.Core.Tests/Battle/AuraDeliveryLawnTests.cs` (7 green) proves aura delivery on a real
> plant `StatContext` through the real executor: type- and match-scoped delivery, side isolation at the
> identical type id, unchanged-when-absent, and withdraw. Only the **grant transport hop** and the live
> probe remain. That file also carries a deliberate tripwire test that **starts failing when Wave 6
> lands**, telling whoever lands it to write the real end-to-end grant test here.

### One defect found and fixed during the build

The reader originally matched bare `channel` / `op` / `amount` overlay keys — which are exactly what
`InjectorEffectActionSink` already reads for **FA1 ModifyStat** (`:80`) and **FA10 ApplyResourceDelta**
(`:132`). Every FA1 grant on the board would have been consumed a second time as a derived mod:
applied once as a primary modifier, again as a derived channel. Caught before shipping by asking what
else writes those keys. Keys are now namespaced (`derived.channel` / `derived.op` / `derived.amount`),
which makes the collision impossible by construction rather than by convention.

## 7. Migration — named, sequenced, not done here

Once A1–A8 hold, the five private producers collapse onto one registered path. **Each is its own task
with its own goldens**, in this order (cheapest and most isolated first):

1. **aptitude** — already an `IActorStatSubsystem`; becomes atoms or stays a subsystem, owner's call
2. **patron** — has a shipped overlay + tuning; highest golden risk
3. **stars**, 4. **injuries**, 5. **contracts** (`ContractPolicy`)

Only after all five: enforce §6.1's rule — reject a derived write whose producer is unregistered. That
enforcement is the point of the exercise, and it cannot land before the last migration or it breaks the
game.

## 8. Boundaries

- **Always:** one kind, one read model across runtimes; cache per `owner_key`; honour G8's `match`-only
  rule for `defense` on `stat.modify`.
- **Ask first:** flipping Sim; changing `DerivedComposer` compose kinds; anything touching the Funnel.
- **Never:** a second derived-write path "just for X" — that is the defect this spec closes; a
  `FindObjectsOfType` in the resolve; SQL from the injector; persisting a derived snapshot as SSOT.

## 9. Open questions (owner)

1. **Does aptitude migrate to atoms, or stay a registered subsystem?** Both satisfy §6.1's rule. Atoms
   give one vocabulary; a subsystem is already built and tested. **Recommendation: leave aptitude as a
   subsystem** — it is registered, so it is not the defect §6.1 describes, and migrating it buys
   vocabulary tidiness at real golden risk.
2. **`Full` or `Partial` for the lawn cell?** `Full` if the executor handles every derived op
   (`Flat`/`Increased`/`Replace`/`Flag`); `Partial` with the side path named if any op is deferred.
   Decide from the built executor, not up front.
3. **Does this program own the migration, or does each producer's own program?** Sequencing above
   assumes this program owns the road and each producer owns its own move.

## DESIGN-GATE checklist

```
[x] Subsystems identified: atom layer, stats/derived, injector↔game, match lifecycle.
[x] Read this session: definitions.md, atom-catalog-ssot.md (via AtomKindRegistry),
    actor-hub-ssot.md, stat-system.md, effect-funnel.md, effect-system.md (via funnel),
    match-runtime.md, event-pipeline-v2-ssot.md, overlay-control-loops.md,
    unique-entity-effects.md, DESIGN-GATE.md.
[x] decisions.md — the relevant rows READ and they changed the spec. The
    "Buff/debuff scope (2026-08-29)" row already builds the (kind, where, who, host)
    compatibility table and the event-driven reactor, and names this exact wiring as
    "a separate, later task". §3 was rewritten to extend it; the original draft would
    have duplicated shipped work. The "Action model" and "Golden ordering" rows were
    read and neither governs a lawn derived write.
[x] software-architecture.md READ IN FULL. Nothing governs a lawn derived write that
    this spec misses, and two of its §6 locked invariants back the design rather than
    constrain it: #1 "single Unity writer" (this spec strengthens the completeness
    half) and #5 "progression flats ride progression.bonus.* only" — which is exactly
    the channel family a stat.derived lawn executor would feed.
[x] Every factual claim cites file:line.
[x] Claims verified against CODE, not comments (the matrix was read from the
    constructor call, the quarantine reason from its own comment AND the code).
[x] Constraint tested, not assumed: the delivery half was fixed and proven on a real
    lawn (12-aptitude matrix), not argued.
[x] Contradicts no §2 invariant — it strengthens invariant 4 (single writer) by giving
    the derived side one registered producer path.
```

**Two boxes are unticked and both are named above.** Read `decisions.md` in full before approving.
