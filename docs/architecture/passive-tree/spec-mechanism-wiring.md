# Spec: `mechanism-wiring`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Program:** passive-tree · **Wave:** 0 · **Depends on:** nothing ·
**Depended on by:** `tree-language --write` — **this module's A10 is now a build gate**
(`passive-tree-map.md:42-47`), not merely a wave-0 sibling. See §1 and §11.1.

**Definitions that win over this spec:** [../effect-atom/definitions.md](../effect-atom/definitions.md) ·
**Kind matrix:** [../effect-atom/atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) ·
**Hub SSOT:** [../actor-hub-ssot.md](../actor-hub-ssot.md) ·
**Surface specs:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md),
[../../design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)

---

## 0. What this module owns, and the frame it is written in

Four inert lines stand between *"a passive node writes a `combat.*` channel"* and *"that channel has
the value, and the balance sweep can see it."* This module closes them.

> **⛔ Every one of the four is a WIRING GAP, not an architectural wall.** An inert path — a
> default-off toggle, a null delegate, a debug-only entry, a built API with no production caller — is
> unfinished wiring. *"Does the lawn support mechanism nodes"* is the wrong question: mechanism nodes
> resolve in the RPG layer, in `ActorHub` → `DerivedComposer` → `OverlayCombatCalculator`, and none of
> that needs PvZ to have heard of dodge, erosion or retaliation.

**This module adds no atom kind, no trigger, no attach point and no derived channel.** The closed
vocabularies stay closed: **7 attach points, 16 kinds, 13 triggers**
(`AtomKindRegistry.cs:21`, `:31`, `:36` — verified by reading the constants and the `AtomTriggers.All`
array at `AtomKind.cs:97-101` this session). That is D22 stated as a build constraint.

---

## 1. Objective

`passive-tree-ideal.md` §3.5 swept tree power as aptitude-point-equivalents across
`b ∈ {0,2,5,10,20}` × `Fmax ∈ {1.0,1.25,1.5}` and found **no cell reverses the ordering** — the focused
build loses at every setting, and at `b = 20` it loses marginally *harder* than at `b = 0`. Its
conclusion is the charter for this module:

> **A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM.**

So mechanism nodes are not a flavour tier — they are the entire reason the layer exists. And today the
only node class that reaches a live actor end to end is the plain permanent derived write. Everything
`05-mechanism-taxonomy.md` ranks above it is blocked on one of the four lines below.

**What lands when this module lands**, expressed as mechanism classes unblocked:

| Rank | Mechanism class | Blocked by | Unblocked by |
|---|---|---|---|
| **1** | **Erosion** — a flat, per-layer defensive debuff applied by `status.apply` on `OnDamageDealt`, carrying a `ModifyStat` payload naming `combat.defense.omni`, `combat.dodge.omni`, `combat.block.rate.omni` … (taxonomy §4c) | G1 | G1 alone |
| **5** | **Layer parity** — a floor on every defensive channel, read from the actor's own allocation (taxonomy §4a) | G1 | G1 alone. It is *itself* an `IActorStatSubsystem`, and G1 is the proof that a fourth one composes |
| **4** | **Conditional scaling** — *"damage scales with damage taken"*, `class-system-map` §4b's own first named fix | G1 + G2 | G1 on the lawn; G1 + G2 in Battle |
| **2** | **Retaliation / reflect** | — | **Live on the LAWN and in Sim; switched OFF in Battle.** Corrected below |
| **3** | **Threshold trigger** | — | Live on the lawn. Same unstated scope as row 2 — a threshold rider reaching Battle needs the trigger Battle actually raises (`OnDamageDealt`), not the ones it does not |
| — | **Scoring any of the above** | G3 | G3, and only G3 |

**Why this module is wave 0, and why an arrow now points out of it.** `tree-plan` must reserve budget
for mechanism nodes at deep tiers. If the wiring never lands, that budget buys nodes that measurably do
nothing, and nobody finds out until `tree-resolve`.

The 2026-09-05 audit round turned that from a risk into a **build gate, and this module owns its
release condition.** `passive-tree-map.md:42-47`: **A10 gates `tree-language --write`, not
`tree-plan --emit`.** The plan is cheap, mints no ids and costs one regeneration if it is wrong; the
step after it costs **~4,680 model calls** for the generic corpus, **~105,840** for species, and ~34
human hours per review pass. Committing that against an unmeasured premise is how a program buys
35,160 nodes and discovers in wave 3 that the deep tiers do nothing.

**So A10 is not an internal acceptance test any more — it is what another module waits on**, and it
has to be able to fail. §11.1 gives it an effect size, a direction and a half-width for exactly that
reason. The measurement runs in `squad-harness` (§8, and `spec-squad-harness.md` §10.1).

### The four gaps, at a glance

| | The inert line | State | Size |
|---|---|---|---|
| **G1** | `ActorHub.cs:145,148,155` registers exactly **three** `IActorStatSubsystem`s; a status's derived `StatMods` go into the **primary** bag at `EffectRuntime.cs:81`, which none of the three reads | ⛔ absent | **S** — ~90 lines by the `AtomDerivedSubsystem` precedent |
| **G2** | `BattleRunState.RecomposeDerived` has **one** production caller, at construction, inside the `foreach (var aura in setup.ActiveAuras)` loop — see the ⚠️ below | built, called once | **S** |
| **G3** | `stat.derived` is `RuntimeState.None` in **Sim** (`AtomKindRegistry.cs:534`), so the balance sweep cannot score the node class §3.5 prescribes | quarantined deliberately | **M** |
| **G4** | `stat.derived` declares `AtomTriggers.None` (`AtomKindRegistry.cs:535`) | built as designed — **and it is a design law, not an oversight** | **not done here** |

> ⚠️ **`BattleModels.cs`, `BattleRunState.cs` and `BattleEngine.cs` are under concurrent edit, and
> this spec cites all three BY SYMBOL, never by line.** The rule started here, as a note about one
> file; the 2026-09-05 seam audit found seventeen stale line citations across the program's specs and
> traced every one of them to these three, so it is now the program's rule and this spec states it as
> such.
>
> The evidence for making it a rule rather than a warning is that **the audit's own corrections have
> already drifted again.** Re-checked this session against the working tree: `RecomposeDerived`'s call
> site moved `:313` → `:323` (the audit's correction) → **`:343`**; `ActiveCommanderAura` `:266` →
> **`:279`**; `BattleOutcome.Stalemate` `:272` → **`:285`**. `battle-tempo`'s `reaction-lane` and
> `base-defense`'s siege work are both open, and all three files are dirty right now.
>
> `passive-tree-ideal.md` §13.2 and `15-dependency-map.md` §6.3 both cite the old numbers; they were
> correct when written. Cite `BattleRunState.RecomposeDerived` and the
> `foreach (var aura in setup.ActiveAuras)` loop in the setup constructor — those survive an edit, and
> a line number does not.

### One fact that is routinely got wrong, verified again here

**Battle raises no `OnDamageTaken`, `OnSpawn` or `OnDeath` — but it DOES raise `OnDamageDealt`.**
A grep over `src/FusionRpg.Core/Battle/` returns zero hits for any of the four, which is how the
"Battle fires nothing" claim keeps getting made. The emit lives in
`src/FusionRpg.Core/Actions/BasicAttack.cs:182-190`, whose trigger line is
`Trigger = AtomTriggers.OnDamageDealt` at `:184` — inside `public static partial class BattleEngine`
(`BasicAttack.cs:17`), in a **different folder**. Verified this session by grepping the whole of `src/`
rather than the Battle folder.

**Consequence for this module:** an on-hit mechanism node — Erosion included — is measurable in Battle
today, once G1 and G2 land. Raising the other three triggers in Battle is **B7**, owned by
`battle-timeline`/`action`, and is not in this module's scope.

### A second fact, corrected here rather than carried forward — reflect is not "already live" everywhere

Rows 2 and 3 of the table above used to read *"Already live (`EffectRuntime.cs:491`). Content, not
code."* That is true on the lawn and misleading everywhere else, and a builder reading it concludes
retaliation is shippable content for the whole program. `squad-harness` §10 caught it first; re-read
this session, and it is worse than "unwired" in Battle — **it is switched off by a null check.**

- `CombatDamageDispatcher.TryReflect` (`CombatDamageDispatcher.cs:85`) is reached from exactly one
  place, `DispatchInstant`, behind `if (actorResolve != null && rng != null && …)`.
- `bag.ActorResolve` is assigned in exactly **two** places in `src/`: the injector's
  `EffectRuntime.cs:496` (the lawn) and `FoundationHarness.cs:118` (which is what `tools/CombatSim`
  drives, `Simulator.cs:66`).
- **Battle builds its own bag at `BattleEffects.cs:55` and never sets it**, and applies HP through
  `DamageApplyPipeline.Apply` instead. A case-insensitive grep for `reflect` across
  `src/FusionRpg.Core/Battle/` returns nothing.

So the honest scope is **lawn: live · Sim: reachable · Battle: off**. The consequence that matters to
this program: **M7 Retaliation is not measurable at squad scope**, because `squad-harness` resolves
over `BattleEngine`. `EffectRuntime.cs:491` was never the reflect wiring either — those lines are the
`ShieldGate` assignment; the reflect line is `:496`.

---

## 2. Commands

```powershell
# Build (Core is Unity-free and builds anywhere)
dotnet build src/FusionRpg.Core

# Tests this module must keep green
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Guard.Tests

# Boundary guards (CI runs them too)
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-dal.ps1
.\scripts\guard-power.ps1

# Numeric + balance-surface audits
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --summary

# Test quality on the one new class
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.Stats.Derived
```

The injector half (G1's adapter) cannot be unit-tested — that assembly needs the game's interop DLLs —
so it is covered by a text guard in `FusionRpg.Guard.Tests`, exactly as `StatusStatApplierGuardTests`
already covers `EffectRuntime.cs`. Building it at all needs a game dir:

```powershell
# MelonLoader host is the default; no flag and no env var needed
.\scripts\deploy-play.ps1 -NoServer
```

---

## 3. Project structure

### New files

| Path | What |
|---|---|
| `src/FusionRpg.Core/Stats/Derived/Subsystems/StatusDerivedSubsystem.cs` | **G1.** The fourth `IActorStatSubsystem`. ~90 lines including the doc comment, by the `AtomDerivedSubsystem.cs` precedent (89 lines) |
| `src/FusionRpg.Injector/Stats/LiveStatusMods.cs` | **G1.** The injector adapter: reaches the live `EffectRuntime.Status` static and returns `ForHost(ptr)`. Thin by design, mirroring `GrantedDerivedAtoms.cs` (52 lines, all logic in Core) |
| `tests/FusionRpg.Core.Tests/Stats/StatusDerivedSubsystemTests.cs` | **G1.** Unit + composed-value tests |
| `tests/FusionRpg.Core.Tests/Status/StatusDerivedComposeSeamTests.cs` | **G1.** The end-to-end seam test: a status writing `combat.*` reaches the composed value |
| `tests/FusionRpg.Guard.Tests/StatusDerivedWiringGuardTests.cs` | **G1.** Text guard over `CheatState.cs` — the registration cannot be silently dropped |
| `tests/FusionRpg.Core.Tests/Battle/BattleDerivedRecomposePerRoundTests.cs` | **G2.** Per-round recompose, including the byte-identical no-op case |
| `tests/FusionRpg.Core.Tests/Effects/SimDerivedConsumerTests.cs` | **G3.** The Sim consumer, then the matrix cell |

### Modified files

| Path | Change | Gap |
|---|---|---|
| `src/FusionRpg.Core/Stats/Derived/ActorHub.cs` | `ActorHubBootstrap.CreateDefault` gains one **optional** delegate, `liveStatuses`, and one `hub.Register(...)` guarded by it — the identical opt-in shape `aptitudeTuning` (`:146-153`) and `boundDerivedAtoms` (`:154-155`) already use, so every existing caller, including the hundreds of tests that call this bare, is unaffected | G1 |
| `src/FusionRpg.Core/Status/StatusStatPayload.cs` | Extract the existing two-clause derived test (`:123-128`) into a public `IsDerivedChannel(string)`, and refuse `more` on a derived channel at parse (see §4.1) | G1 |
| `src/FusionRpg.Injector/CheatState.cs` | Pass `liveStatuses: LiveStatusMods.For` alongside the existing `boundDerivedAtoms:` argument at `:59` | G1 |
| `src/FusionRpg.Core/Battle/BattleRunState.cs` | One `RecomposeDerived` call per actor per round. **Cite by symbol** — this file is under concurrent edit right now | G2 |
| `src/FusionRpg.Core/Effects/SimEffectHost.cs` | The Sim derived consumer: fold contributions onto the pinned snapshot instead of returning it bare | G3 |
| `src/FusionRpg.Core/Effects/FoundationHarness.cs` | Same fold — `tools/CombatSim` drives this host, not `SimEffectHost` (`Simulator.cs:66`) | G3 |
| `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs` | `stat.derived`'s Sim cell `None → Full`. **The LAST edit of G3, never the first** | G3 |
| `tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs` | `Battle_support_is_narrow_and_honest`'s Sim assertion (`:386`) moves with the cell | G3 |
| `tests/FusionRpg.Core.Tests/Items/IlvlTierLadderTests.cs` | `:87` asserts `AffixFilters.RuntimeAllows("stat.derived", RuntimeId.Sim)` is **false**; it moves with the cell | G3 |
| `docs/architecture/actor-hub-ssot.md` | One new row in §6's subsystem registry (see §4.1). **In the same change** — evidence rule 6 | G1 |
| `docs/architecture/effect-atom/atom-catalog-ssot.md` | The `stat.derived` runtime row (`:65`) reflects the new Sim cell | G3 |
| `docs/architecture/decisions.md` | The "Derived-write lawn executor" row (`:106`) says *"Sim stays `None` — it still has no consumer."* When G3 gives it one, that sentence is amended, not left to rot | G3 |

**Nothing else.** No `data/`, no web, no `FusionRpg.Data`, no new tunable.

---

## 4. The four gaps, ordered by critical path

### 4.1 G1 — a status's derived-channel write never composes  *(critical path)*

**The inert line.** `ActorHub.ResolveDerived` folds only registered subsystems —

```csharp
// ActorHub.cs:57-58
foreach (var subsystem in _subsystems)
    subsystem.ContributeDerived(ctx, mods);
```

— and `ActorHubBootstrap.CreateDefault` registers exactly three: `RpgProgressionSubsystem`
(`ActorHub.cs:145`), `AptitudeSubsystem` (`:148`), `AtomDerivedSubsystem` (`:155`). Meanwhile a live
status's `StatMods` are upserted into the **primary** stat bag:

```csharp
// EffectRuntime.cs:81
CheatState.Stats.Upsert(Core.Status.StatusStatPayload.ToModifiers(inst));
```

`StatSystem.Resolve` composes that bag into `EntityFinal` — the 23 primary Unity channels. **None of
the three subsystems reads the status bag.**

**What it blocks.** `StatusStatPayload` deliberately accepts derived channels: its own documented shape
is `{"atk": {"more": -0.1}, "combat.power.fire": {"flat": 25}}` (`StatusStatPayload.cs:30-32`), and
`IsKnownChannel` admits any `combat.*` channel plus eight `status.power.*`/`status.resist.*` ones
(`:123-128`). So a status naming `combat.dodge.omni` is parsed, validated, stored, source-tagged, and
withdrawn on expiry — and **never composed into `ActorDerivedSnapshot`.** That is the exact silent
no-op the payload's own comment refuses in the abstract:

> *"A channel nothing composes would be a modifier that is created, stored, withdrawn on expiry, and
> never once read — the silent no-op this whole layer refuses."* (`StatusStatPayload.cs:79-81`)

This is the single line between *"a status writes a `combat.*` channel"* and *"that channel has the
value"*, and **Erosion, layer parity and conditional scaling all sit behind it.**

**It is not only the tree's problem.** Two shipped runtimes already produce derived-channel `StatMods`
and would compose nothing the day they get a production caller:

- `StanceRuntime.Raise` — the defence stance's *"raised defensive channels"* (`StanceRuntime.cs:59-83`).
  Its own tests use `new StatusStatMod("combat.defense.omni", "flat", 25)`
  (`DefenceActionStanceTests.cs:36`). Callers today: tests only.
- `ExhaustionPolicy.Sync` — the exhaustion debuff (`ExhaustionPolicy.cs:120-133`). Its tests use
  `combat.defense.omni` and `combat.power.omni` (`ExhaustionPolicyTests.cs:83,157`). Callers today:
  its own constructor declaration and tests.

Both are wiring gaps of their own; **G1 is their missing consumer too.**

**The fix.** A fourth `IActorStatSubsystem`, `StatusDerivedSubsystem`, registered by
`ActorHubBootstrap.CreateDefault` behind an optional `liveStatuses` delegate. Shape in §5.

Four sub-decisions, all made here rather than left open:

1. **A new subsystem, not a widened `AtomDerivedSubsystem`.** That class *is* the `stat.derived` atom
   executor; a status's `StatMods` are not atoms. A separate `SubsystemId` also keeps attribution
   honest — the sheet's contribution list (`spec-derived-stat-sheet.md` §5.3) reads `SourceId`, and
   `status:{instanceId}` is what `StatusStatPayload.SourceIdOf` already emits (`:180`).
2. **Order 400, and one new row in `actor-hub-ssot.md` §6.** The reserved
   `foundation.effect | 350 | session bag | future timed derived` slot is already taken by
   `AtomDerivedSubsystem` (`AtomDerivedSubsystem.cs:49`), so this needs its own row —
   `status.timed | 400 | session bag | timed derived from live statuses` — added in the same change,
   never discovered later. Order carries no semantic weight today: `FlatSum`/`SumIncreased` are
   commutative sums and `FlatReplace`/`MaxPriorityFlag` order by `Priority`/`SourceId`, not by list
   position (`DerivedComposer.cs:42-69`).
3. **`more` is refused at parse, on derived channels only.** `StatusStatPayload.Ops` is
   `flat | increased | more` (`:37`) and **there is no `More` on the derived side** —
   `DerivedModifierOp` is `Flat | Increased | Replace | Flag` (`DerivedModifier.cs:10-16`), and
   `AtomDerivedSubsystem.TryParseOp`'s own doc says silent coercion *"is how a wrong number ships
   looking correct"* (`:64-68`). So `TryParse` refuses `more` on a derived channel with a named error,
   and the subsystem's own op parser is defence in depth. **Verified safe: no shipped content authors a
   status `stat` overlay at all** — `grep -rn '"stat"' data/seed/` returns nothing.
4. **The injector adapter never throws.** `GrantedDerivedAtoms.For` wraps its static read in
   `try/catch` and returns empty on failure (`:37-48`), because a bag that is not up yet is a normal
   state. `LiveStatusMods.For` does the same around `EffectRuntime.Status`, which lazily calls
   `Ensure()` (`EffectRuntime.cs:31-38`, `:49-58`).

**One real behaviour change, named.** `InjectorStatusBridge.ResolveDerived` calls
`hub.ResolveDerived(ctx)` (`:58`), and `StatusRuntime.Apply` calls that delegate during the L2b resist
evaluation. After G1, that resolve reads the host's currently active statuses — so a status that raises
`status.resist.dot` makes the *next* status harder to apply. That terminates (`ForHost` is a dictionary
read, never a resolve) and it is what a resist status means. It is still a change no golden covers.
See §12 question 1.

**Size: S.** `AtomDerivedSubsystem.cs` is 89 lines including its doc comment.

---

### 4.2 G2 — Battle's derived recompose runs once, at construction

**The inert line.** `BattleRunState.RecomposeDerived` exists, is idempotent, and has exactly **one**
production call site — inside the construction-time aura loop
(the `foreach (var aura in setup.ActiveAuras)` loop in the setup constructor). The seam's own doc says so:

> *"the explicit recompose entry point — **deliberately not called anywhere in `Resolve`'s own loop.**
> 'Explicit, never implicit per-tick' … a real trigger (an aura toggling on/off, T13) calls this at the
> moment it happens; nothing calls it on a schedule."*

and the call site's comment agrees: *"Delivered once, at construction — a live mid-match toggle is
T13's own job, not this one's."*

`BattleEffects.cs`'s `Ledger.Recompose` is a **different** ledger (`BattleStatModifierLedger`, primary
channels) — checked, not assumed.

**What it blocks.** Any mechanism whose value changes *during* a Battle. A status applied on round 3
that raises `combat.power.omni` is composed into `BattleDerivedModifierLedger` by nothing and read by
nothing until the next battle. Conditional scaling in Battle is exactly this shape. On the lawn G1
alone is enough (the lawn re-resolves per apply); in Battle both are needed.

**The fix.** Call `RecomposeDerived(actorKey)` once per actor at round start, in `Resolve`'s loop.

**Why this is safe, arithmetically rather than by hope.** `BattleDerivedModifierLedger.Recompose`
writes `baseDerived.Get(channel) + Σ(active sources)` — **always computed from the frozen base, never
from `live`'s own prior value** (`BattleDerivedModifierLedger.cs:12-16`, `:60-64`). So calling it N
times produces the same value as calling it once. And an **empty** ledger is a hard no-op: it visits
only channels it has tracked, so `live` is byte-identical to before the call — a property its own test
`An_empty_ledger_recomposes_nothing` already pins (`:54-59`). Every battle today either has an empty
ledger or a construction-time aura ledger, and both are byte-identical under repetition. **Zero goldens
should move.** That is a claim to *run*, not to assert — see §6.

**Ownership note.** `aura-skill` T13 is named in the code as the owner of *"a live mid-match toggle."*
This module takes the narrower half — a per-round recompose — and leaves the toggle event to T13. See
§12 question 2.

**Size: S.** One call, one loop position, plus the golden re-run.

---

### 4.3 G3 — `stat.derived` is unscored in Sim, so the sweep cannot see the node class §3.5 prescribes

**The inert line.**

```csharp
// AtomKindRegistry.cs:534
new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None),
//                       ^ Lawn              ^ Battle            ^ Sim
```

with the comment directly above it (`:532-533`): *"SIM stays None — `SimEffectHost` still has no
consumer."* The field order is `(Lawn, Battle, Sim)` — `AtomKind.cs:54`, read, not assumed.

**What it blocks.** The balance proof lives in Sim, and `RuntimeState.None` is a **rejection**, not a
degradation: `BindGate.cs:70-75` and `Compilability.cs:104-107` both refuse with `RuntimeUnsupported`.
So the sweep cannot score a mechanism node — the one class §3.5 says is the only thing that works.

The blocker is deeper than the cell, and this is the part that decides the size:

- `SimEffectHost` and `FoundationHarness` resolve derived stats from a **pinned dictionary**
  (`ActorDerivedLookup`, `ActorDerivedProfiles.cs:135-157`) — `Pin(ptr, snapshot)` in, snapshot out.
  There is no `ActorHub`, no subsystem fold, nothing for a bound atom or a live status to contribute to.
- **No host constructs a `BindContext(RuntimeId.Sim)` today.** The only production `BindContext` is
  `RpgHub.cs:107`, and it passes `RuntimeId.Lawn`. So flipping the cell on its own authorizes nothing.
- `tools/CombatSim` drives `FoundationHarness`, not `SimEffectHost` (`Simulator.cs:66`) — so the fold
  is owed on **both** hosts or the harness still cannot see it.

**The fix, in the order decisions.md already fixed for the lawn.** `decisions.md:106` states the rule
outright — the lawn cell moved *"deliberately the LAST step of that change, not the first: flipping
before the executor existed would have re-created D6's exact state (binds accepted, nothing applied)."*
Same order here:

1. Give `ActorDerivedLookup` a contribution fold: pinned snapshot as the base, plus bound
   `stat.derived` atoms and live status derived mods, via `ActorDerivedSnapshot.OverlayAdd`
   (`ActorDerivedSnapshot.cs:65`) — the same *"plain sum is what `FlatSum` composing IS"* reasoning
   `BattleDerivedModifierLedger.cs:18-26` already established.
2. Wire it into `SimEffectHost` and `FoundationHarness`.
3. Add a `BindContext(RuntimeId.Sim)` call site in the harness, so a bind is actually attempted.
4. **Then** flip the cell, and move the two tests that assert it —
   `AtomKindRegistryTests.cs:386` and `IlvlTierLadderTests.cs:87`.

**`Full` or `Partial` is decided from the built executor, not up front.** `decisions.md:106`'s owner
decision (2): `Full` only if it honours all four derived ops (`Flat`/`Increased`/`Replace`/`Flag`), else
`Partial` with the side path named (`definitions.md` §9). A fold built on `OverlayAdd` honours `Flat`
and `Increased` and does **not** honour `Replace`/`Flag` — so **the honest first landing is `Partial`,
and `Full` only if the fold is routed through the real `DerivedComposer`.** Do not write `Full` into
this cell without running the four ops.

**Blast radius, named.** `AffixFilters.RuntimeAllows` reads this matrix (`AffixFilters.cs:30-34`) —
callers today are tests only, so the radius is one assertion, not a live filter change.

**Size: M.** Two hosts, one lookup type, one bind site, one cell, two tests.

---

### 4.4 G4 — `stat.derived` declares `AtomTriggers.None` — **out of scope, and here is why**

**The inert line.** `AtomKindRegistry.cs:535` — `AtomTriggers.None`, so `stat.derived` cannot
re-evaluate per hit.

**This one is not a wiring gap in the same sense as the other three, and calling it one would be
wrong.** It is a normative rule in the document that **wins over every spec**:

> *"`stat.modify` and `stat.derived` are **permanent modifiers**: they declare **no trigger at all**,
> and apply/revert is a lifecycle mechanic the runtime owns. Authoring a trigger on either is
> `TriggerNotAllowed`."* — [definitions.md](../effect-atom/definitions.md) §14.2

Restated in code at `AtomKind.cs:129` (*"A permanent modifier declares no trigger at all — it is not
event-driven"*), in `atom-catalog-ssot.md:85`, and pinned by three assertions in
`AtomKindRegistryTests.cs:133-146,170` plus its `permanentModifiers` set at `:73`.

**So the fix is not to widen the trigger set.** The conditional-scaling capability arrives through
**G1**, by the route the taxonomy already names: `status.apply` on `OnDamageTaken`/`OnDamageDealt`,
carrying a `ModifyStat` payload whose channels are derived. That kind already carries the full trigger
set (`AtomKindRegistry.cs:46-48`, `:610`), and its condition leaves already have real readers —
`LeafId.HpBelowMilli`/`HpAboveMilli` at `PredicateNode.cs:26-27` (⚠️ `05-mechanism-taxonomy.md` cites
`:24-25`; the enum has shifted, `ActorIsKiller` sits at `:24` — corrected here), read by
`FactReader.HpMilli` at `:71`. **Close G1 and G4 stops mattering for every node this program needs.**

**If a genuinely trigger-capable derived write is ever wanted**, the route exists and is named, and it
is a reviewed change, not a convenience: `stat.modify` took exactly that route via `TriggerOptional: true`
(`AtomKindRegistry.cs:497`, `:503`), through spec `spec-battle-live-stat-modifiers.md` §4 and a
`decisions.md` row — **and that widen broke the permanent-modifier path first**, because
`AtomRowValidator.ValidateWhen` infers *"trigger REQUIRED"* from `Triggers.Count > 0`
(`AtomKindRegistry.cs:490-495`, found by running an existing fixture, not by reading). That is the cost
of this change, measured once already.

**Size: not sized here. Ask first — §10.**

---

## 5. Code style — the fourth subsystem

The shape is fixed by the two shipped siblings: an injected per-context delegate, so the module owns
*resolving mods into channel values* and never owns *where the state is stored*
(`AtomDerivedSubsystem.cs:36-41`, `AptitudeSubsystem.cs:12-18`); `SubsystemId` + `Order`; a
`ContributeDerived` that is idempotent and stateless between calls; and a static op parser that refuses
rather than coerces.

```csharp
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// The consumer for a status's DERIVED-channel <c>StatMods</c> — the line between "a status writes
/// <c>combat.dodge.omni</c>" and "that channel has the value".
///
/// <para><b>Why this exists.</b> <see cref="StatusStatPayload"/> accepts derived channels on purpose
/// (its own worked example is <c>{"combat.power.fire": {"flat": 25}}</c>), and the lawn upserts them
/// into the PRIMARY session bag (<c>EffectRuntime.cs:81</c>), which composes into the 23 Unity fields.
/// <see cref="ActorHub.ResolveDerived"/> folds only registered <see cref="IActorStatSubsystem"/>s, and
/// none of the three read that bag — so a derived-channel status was validated, stored, withdrawn on
/// expiry, and never once composed. Exactly the silent no-op <c>StatusStatPayload</c>'s own doc refuses
/// in the abstract.</para>
///
/// <para><b>Why a fourth subsystem and not a widened <see cref="AtomDerivedSubsystem"/>.</b> That class
/// IS the <c>stat.derived</c> atom executor; a status's mods are not atoms. A distinct SubsystemId also
/// keeps attribution honest — the derived-stat sheet's contribution list reads <c>SourceId</c>, and
/// <c>status:{instanceId}</c> is what <see cref="StatusStatPayload.SourceIdOf"/> already emits, so one
/// expiring stack can never withdraw another's.</para>
///
/// <para><b>Order 400</b> is a NEW row in actor-hub-ssot.md §6 (<c>status.timed | 400 | session bag |
/// timed derived from live statuses</c>), added with this change — the reserved 350 slot is already
/// occupied by <see cref="AtomDerivedSubsystem"/>. Order carries no semantic weight today: FlatSum and
/// SumIncreased are commutative, and FlatReplace/MaxPriorityFlag order by Priority/SourceId rather than
/// by list position.</para>
///
/// <para><c>ContributeDerived</c> holds no state between calls and <c>ActorHub.Register</c> replaces by
/// <see cref="SubsystemId"/>, so a double registration can never double-add. Never static: a static
/// cache would leak one scoped test host's statuses into another.</para>
/// </summary>
public sealed class StatusDerivedSubsystem : IActorStatSubsystem
{
    /// <summary>
    /// The live status instances on this actor. A delegate for the same reason
    /// <see cref="AtomDerivedSubsystem"/> and <see cref="AptitudeSubsystem"/> use one: this module
    /// resolves values, it does not own the runtime. Production passes the injector's
    /// <c>EffectRuntime.Status.ForHost</c> adapter; tests pass a list.
    /// </summary>
    readonly Func<StatContext, IReadOnlyList<StatusInstance>> _activeFor;

    public StatusDerivedSubsystem(Func<StatContext, IReadOnlyList<StatusInstance>>? activeFor = null) =>
        _activeFor = activeFor ?? (_ => Array.Empty<StatusInstance>());

    public string SubsystemId => "status.derived";

    public int Order => 400;

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        var active = _activeFor(ctx);
        if (active is null || active.Count == 0) return;

        foreach (var instance in active)
        {
            if (instance.StatMods.Count == 0) continue;
            var sourceId = StatusStatPayload.SourceIdOf(instance);

            foreach (var mod in instance.StatMods)
            {
                // Primary channels are StatSystem's and are already wired (EffectRuntime.cs:81).
                // Reading the SAME predicate the parser uses is what stops the two disagreeing about
                // what "derived" means — a disagreement would be invisible, not a build error.
                if (!StatusStatPayload.IsDerivedChannel(mod.ChannelId)) continue;

                // There is no `More` on the derived side (definitions.md §14's kind note). `more` is
                // already refused at parse for derived channels; this is defence in depth, and it
                // SKIPS rather than coercing to Flat — silent coercion is how a wrong number ships
                // looking correct.
                if (!TryParseOp(mod.Op, out var op)) continue;

                mods.Add(new DerivedModifier(mod.ChannelId, op, mod.Value, SourceId: sourceId));
            }
        }
    }

    /// <summary>Maps a status op to a composer op. `more` and anything unknown are refused.</summary>
    public static bool TryParseOp(string? op, out DerivedModifierOp parsed)
    {
        switch (op)
        {
            case "flat": parsed = DerivedModifierOp.Flat; return true;
            case "increased": parsed = DerivedModifierOp.Increased; return true;
            default: parsed = default; return false;
        }
    }
}
```

Registration, in `ActorHubBootstrap.CreateDefault`, immediately after the `boundDerivedAtoms` block and
in the identical opt-in shape:

```csharp
if (liveStatuses is not null)
    hub.Register(new Subsystems.StatusDerivedSubsystem(liveStatuses));
```

---

## 6. Testing strategy

### The one test this module exists for

> **A status writing a `combat.*` derived channel reaches the composed value.** This is the thing that
> silently does not happen today, and *silently* is the operative word: nothing throws, nothing logs,
> the modifier is created and withdrawn correctly, and the number never moves.

`StatusDerivedComposeSeamTests` — through the **real** `ActorHub` + `DerivedComposer`, never a stub, in
the same spirit as `StatusStatApplierSeamTests` proving the primary half:

```
Given a real ActorHub with the fourth subsystem registered over a real StatusRuntime
And   a live `expose` instance on host "Z1" carrying StatusStatMod("combat.defense.omni", "flat", -25)
When  ActorHub.ResolveDerived(ctxFor("Z1")) runs
Then  snapshot.Get("combat.defense.omni") == registryDefault - 25
And   the same channel for host "Z2" is unchanged            (per-actor, D21)
When  the instance ends and the runtime drops it
Then  the channel returns to its registry default            (withdrawn, not sticky)
```

Plus the falsifier that makes the test worth having: **run the same fixture against a hub with the
three shipped subsystems only, and assert the channel does NOT move.** Without that arm, a green test
proves the fixture, not the fix.

### Per gap

| Gap | Test | What would fail without it |
|---|---|---|
| **G1** | `A_status_derived_channel_reaches_the_composed_value` (above) | the whole module |
| **G1** | `A_status_on_a_PRIMARY_channel_still_composes_through_StatSystem_only` | double-application — the subsystem must not also contribute `atk`, which `EffectRuntime.cs:81` already handles |
| **G1** | `Two_stacks_withdraw_independently` — two instances, one ends | one expiring stack silently removing the other's contribution; `SourceIdOf` is instance-keyed for exactly this |
| **G1** | `A_more_op_on_a_derived_channel_is_refused_at_parse` | a `more` mod silently coerced to `Flat`, or silently dropped with no author feedback |
| **G1** | `Contributions_name_the_status_instance` via `ActorHub.ResolveDerivedWithContributions` (`:80-87`) | the derived-stat sheet's contribution list rendering an `unattributed` row for a source it could have named (`spec-derived-stat-sheet.md` §5.3) |
| **G1** | `An_empty_status_runtime_contributes_nothing` — the default `Array.Empty` delegate | goldens moving on a host that registered the subsystem but has no statuses |
| **G1** | Guard: `StatusDerivedWiringGuardTests` asserts `CheatState.cs` still passes `liveStatuses:` | a refactor quietly unregistering it; the injector cannot host a test project, which is why `StatusStatApplierGuardTests` exists in exactly this shape |
| **G2** | `Recompose_per_round_is_idempotent` — resolve, recompose twice, assert equal | a recompose that accumulates onto `live` instead of recomputing from `BaseDerived` |
| **G2** | `An_empty_ledger_recomposes_nothing_per_round` | the golden-neutrality claim in §4.2 being assumed rather than run |
| **G2** | **Re-run the battle goldens.** The claim *"zero goldens move"* is a claim, not a fact, until `dotnet test tests/FusionRpg.Core.Tests` is green | evidence rule 4 — an assumed constraint costs the owner a decision they never needed to make |
| **G2** | `A_status_applied_mid_battle_changes_the_composed_channel_by_the_next_round` | the gap reopening as soon as someone binds an Erosion node |
| **G3** | `Sim_folds_bound_derived_contributions_onto_the_pinned_snapshot` — on **both** `SimEffectHost` and `FoundationHarness` | the cell flipping while `tools/CombatSim` still reads a bare pinned snapshot |
| **G3** | `A_stat_derived_bind_in_Sim_is_accepted` through the real `BindGate` | the cell being flipped with no bind site, i.e. authorizing nothing |
| **G3** | `The_four_derived_ops_decide_Full_versus_Partial` — exercise `Flat`/`Increased`/`Replace`/`Flag` and assert the cell matches what the fold honours | writing `Full` into a cell that silently drops `Replace` — D6's exact failure, re-created |
| **G4** | `stat_derived_still_refuses_every_trigger` — keep `AtomKindRegistryTests.cs:133-146,170` green, unchanged | the law in `definitions.md` §14.2 being eroded by a convenience |
| **A10** | **Run the Erosion differential.** §4c's *"costs a spread build several times what it costs a corner"* is INFERENCE from curve shapes, and §11.1 gives it a bar it can fail. `squad-harness --erosion` produces `D` with a 95% interval; nothing in this repo is entitled to believe §4c until that run exists | evidence rule 4, again — and here the cost of assuming is ~4,680 model calls released against an unmeasured premise |

### Mutation

`.\scripts\mutate.ps1` over the new subsystem. The two mutants that matter: flipping the
`IsDerivedChannel` guard to always-true (must be caught by the primary-channel test) and replacing the
`TryParseOp` default arm with `Flat` (must be caught by the `more` test). A survivor in either needs an
explanation next to the code.

---

## 7. Numeric rules

**This module introduces no new magnitude arithmetic**, and that is the point of stating the rules
rather than the reason to skip them.

- **Any magnitude this module's downstream content touches is `long`.** `CLAUDE.md`'s measured table:
  `float` stops being integer-exact at `Θ` = **232** and `int` per-mille at **3,213** — both inside
  normal play. Erosion's per-stack `E` is a per-mille share of `P(Θ)`, so it is computed where the
  ladder is already read correctly: `AtomCompiler.cs:463-464` widens with `(long)spec.PowerLadderKMilli
  * pThetaValue`, divides by 1000 exactly once, and is `checked` so overflow **throws**. Nothing in
  this module recomputes it.
  **Ownership, stated once because three specs have claimed it and this one disclaimed it:
  `spec-tree-binder.md` owns the `PowerLadderK` coefficient** — it computes the coefficients and is the
  only module that can test the `checked((int)…)` rounding at `AtomCompiler.cs:464`. `spec-tree-resolve.md`
  previously assigned it here; it does not live here, and this line is the pointer rather than a
  re-litigation.
- **Widen before multiplying, divide last, let overflow throw.** No `unchecked` anywhere in the new
  code. There is no multiply in the subsystem at all — it forwards an already-resolved value.
- **`double` in the derived layer is legal here, by the table's own rule.** `DerivedModifier.Value` and
  `ActorDerivedSnapshot` are `double` by shipped design. `CLAUDE.md` bans `double` *"in a hashed or
  persisted path"*; the derived snapshot is neither — `actor-hub-ssot.md` §7 bans persisting it as SSOT
  outright. **Widening this layer to `long` is not in scope and must not be attempted as a side effect.**
- **The `(int)` narrowing at `ActorHub.cs:92-95` is not reached.** `MergeAppliedCombat` narrows
  `progression.bonus.*`; this subsystem contributes only `combat.*` and the eight `status.power.*` /
  `status.resist.*` channels, because `StatusStatPayload.IsKnownChannel` admits nothing else
  (`:123-128`). Stated so a later widening of that predicate is recognised as reaching a cap.
- **No cap is added.** The one bound this module inherits is the taxonomy's **even-split ceiling**
  (§5.1) — a *relative* bound between two builds at the same investment, **not** a ceiling on either,
  so PS-8 is untouched. That distinction is load-bearing and any node spec adopting it must say so.
  Channel caps that already exist (`status.resist.dot/cc/contagion` at 0.95) are bounded ratios and
  exempt; `status.resist.omni` is uncapped and stays uncapped.
- **No tunable is introduced.** Nothing here is a number a balance pass would change: this module is
  plumbing. Erosion's `E`, the layer-parity `f`, and the tier ladder are `tree-plan`'s and
  `tree-binder`'s, and land in `data/tuning/passive-tree.v1.json` per the ideal §14.
  `python scripts/audit-magic-numbers.py --summary` must not gain a row.

---

## 8. Sim scoring is what makes mechanism nodes measurable — the `squad-harness` coupling

`squad-harness` and `mechanism-wiring` are both wave 0, share no files, and have no dependency arrow
between them. **They are still coupled, and the coupling should be stated once here rather than
discovered in wave 3.**

- `DominanceGuard.Measure` takes `IReadOnlyList<AptitudeAllocation>` (`DominanceGuard.cs:38`) and
  resolves each arrow with `Predictor.Predict` (`:55`). **A mechanism node is not expressible as an
  input to that signature** — a type-level fact, not a coverage gap.
- `StrikeMixture` does not re-implement combat math; it **calls the shipped functions**
  (`StrikeMixture.cs:16-20`). So a mechanism expressed as a **snapshot difference** — which Erosion and
  layer parity both are — is scored with **zero harness work**, the moment the snapshot difference
  actually exists. That is G1.
- Anything per-hit — ICDs, charges, stacking, timing — is outside the closed form by construction
  (`Predictor.cs:161-171` models one swing per side per round) and needs trials. The trial engines
  exist: `tools/CombatSim/Simulator.cs:59` drives the real `CombatDamageDispatcher.DispatchInstant`,
  and `BattleEngine` is a pure seeded resolver on the same SSOT path. **Neither is reachable from
  `DominanceGuard.Measure`.**
- **G3 is what lets a trial-based harness carry a mechanism node at all.** Without a Sim consumer, a
  `stat.derived` bind is refused with `RuntimeUnsupported` before the trial starts.

**So: if `squad-harness` is built on `Predictor`, it can never score a mechanism node, and the number
it reports for a focused build will be §3.5's number again.** Whether it is built on trials is
`squad-harness`'s decision, not this module's — but this module is the thing that makes the trial
option real, and that should be visible in both specs. It is: `spec-squad-harness.md` §5 puts
`duelTrials` and `squadTrials` on `BattleEngine.Resolve`, and its §10.1 is A10's measurement.

**One route through this section is cheaper than it looks, and it is what puts A10 in wave 0.** A
mechanism expressed as a **snapshot difference** needs no atom, no bind and no `BindContext` — it needs
a different snapshot. `BattleActorSetup.ChannelMods` supplies one today: an additive derived-channel
overlay of `(ChannelId, long Amount)` that `BattleStatComposer.Compose` folds in, validated against the
full registered channel set. So **A10a — the static Erosion — is measurable over `BattleEngine` before
G1, G2 or G3 land** (§11.1). What the gaps buy is A10b: the shipped stacking-status vehicle producing
the same differential. Both belong in the harness's `coverage` block so they are never conflated.

---

## 9. Explicitly excluded: the 17th atom kind (D16)

**Out of scope for this module, and it is a REAL gap, not a wiring gap.** Saying so precisely matters
more than the exclusion itself.

D16 requires *"conversion nodes rewrite element payload tags, not just magnitudes"*, because a
conversion that changed only the number *"would silently create dead stats."* The resolver expresses
that perfectly — `ElementPayload` is a weighted component list, and matchup, power, penetration,
absorption, defense, amplification, accuracy, dodge and crit are **all read per component**
(`OverlayCombatCalculator.cs:128-173`; the per-component fold starts at `:128` with
`foreach (var c in request.Components)`).

**But no kind among the 16 writes an element payload.** The payload comes from whatever built the
packet; none of the kinds carries a packet-shaping parameter. This is attach point (B) — packet-time —
and **(B) has no passive vehicle at all.**

**And the failure is silent.** The fold at `:128-173` loops the payload's *own* components, so an
ice-keyed affix on a payload with no ice component contributes exactly zero, forever, with no error and
no log line. Nothing rejects it; the number on the sheet simply never moves.

**What it would take**, so this is a scoped item rather than a shrug:

1. A **17th atom kind** on the `Board` or a new packet attach point — `KindCount` `16 → 17`
   (`AtomKindRegistry.cs:31`), an executor in every runtime that claims support, a `ParamSchema`, a
   `PowerCategory`, and a `RuntimeSupportMatrix` justified per runtime from the built executor.
2. A **reviewed `decisions.md` row.** *"Adding a kind is a reviewed code change because a kind without
   an executor is dead on arrival"* (`AtomKind.cs:153-157`), and the "Atom attach points" row
   (`decisions.md:112`) says growing the attach-point list is an amendment to that row.
3. Propagation to `DESIGN-GATE.md` §1's atom row, which **wins over every spec** and has already gone
   stale twice on these counts.
4. `atom-catalog-ssot.md` §2's matrix and `AtomKindRegistryTests`' self-consistency assertions.

Tracked as **B2** in [15-dependency-map.md](../../research/passive-tree/15-dependency-map.md), owned by
`content-stack`, and it has no spec, no task and no map row today.

**Consequence for planning, stated in the ideal and repeated here: allocate no budget to conversion
nodes until B2 lands.** A conversion node authored before then is a node that does nothing, silently.

**The cheaper thing to try first:** the action carries the payload and the passive picks the action.
That costs no code at all, and it should be tried before a kind is proposed.

---

## 10. Boundaries

### Always

- Add the fourth subsystem as **new code in the existing architecture** — `IActorStatSubsystem`,
  `ActorHub.Register`, `DerivedComposer`. No second delivery path.
- Keep every new host wiring **opt-in** (a null delegate registers nothing), so the hundreds of bare
  `ActorHubBootstrap.CreateDefault()` callers are provably unaffected.
- Read the **same predicate** the parser uses for "is this a derived channel", from one place.
- **Refuse, never coerce.** An unknown or unmappable op is skipped and surfaced, never silently turned
  into `Flat`.
- Propagate in the same change: `actor-hub-ssot.md` §6's registry row (G1),
  `atom-catalog-ssot.md`'s runtime row and `decisions.md:106`'s *"Sim stays `None`"* sentence (G3).
- Cite `BattleModels.cs`, `BattleRunState.cs` and `BattleEngine.cs` **by symbol, never by line**. All
  three are under concurrent edit by `battle-tempo` and `base-defense`, and the seam audit's own
  line-number corrections for them had already gone stale by the time this spec was next opened.
- Run the guards and the goldens before claiming either is unaffected.

### Ask first

- **Adding an atom KIND or a TRIGGER.** This is a **reviewed change to `decisions.md`, not a
  convenience** — stated explicitly because it is the exact shortcut this module is positioned to take
  and must not. It covers all of:
  - the **17th kind** for D16's element-payload conversion (§9);
  - **widening `stat.derived`'s trigger set** (G4) — which additionally contradicts
    `definitions.md` §14.2, the document that wins over this spec, so it needs that document amended
    too, and `AtomRowValidator.ValidateWhen`'s required-vs-allowed inference re-checked;
  - any new **attach point** (`decisions.md:112` names itself as the place that is amended).
- Moving the `stat.derived` **Sim cell** to `Full` rather than `Partial` — decided from the built
  executor per `decisions.md:106`'s owner decision (2), not chosen up front.
- Taking `aura-skill` **T13**'s live-toggle scope rather than only the per-round recompose (§12 q2).
- Any change to the L2b resist path's inputs beyond the one named in §4.1 (§12 q1).

### Never

- Never add a **derived channel**. 267 are registered and one is addressed by a shipped atom
  (`atom-catalog-ssot.md:165`); the problem is producers, not vocabulary.
- Never write a Unity field from this module. Combat writes go through `EntityStatWriter`; HP deltas
  through the Funnel → FA10. `guard-single-writer.ps1` and `guard-funnel-delta.ps1` enforce it.
- Never read PvZ's current state or make a mechanism depend on PvZ representing a concept.
- Never persist `ActorDerivedSnapshot` or `AppliedCombat` as SSOT (`actor-hub-ssot.md` §8 ban list).
- Never make the subsystem `static` or give it a static cache — the exact `AptitudeTuningHub` race this
  repo fixed once, and it would leak one scoped test host's statuses into another.
- Never gate a passive-tree feature on the lawn. The overlay resolver is default-off
  (`OverlayCombatFeature.cs:13`) and that is **B4d, soft** — Battle and Sim run the resolver
  unconditionally, and standalone-first says the injector may enrich a feature, never gate one.
- Never introduce a magnitude cap, a `float` magnitude, or a bare balance literal.

---

## 11. Success criteria

| # | Criterion | How it is proven |
|---|---|---|
| **A1** | A status writing `combat.defense.omni` changes the composed `ActorDerivedSnapshot` value, and returns to default when it ends | `StatusDerivedComposeSeamTests`, through the real hub, **with the three-subsystem falsifier arm** |
| **A2** | Registering the subsystem with no statuses moves **nothing** | the empty-delegate test, plus a full green `dotnet test tests/FusionRpg.Core.Tests` |
| **A3** | A status applied mid-Battle changes the composed channel by the next round | `BattleDerivedRecomposePerRoundTests` |
| **A4** | The battle goldens are **byte-identical** after G2 | the suite is run, not reasoned about |
| **A5** | A `stat.derived` bind is accepted in Sim through the real `BindGate`, and the fold produces the value | `SimDerivedConsumerTests` |
| **A6** | The Sim cell reads `Full` **or** `Partial` according to which of the four derived ops the fold honours | the four-op test, and the cell moves last |
| **A7** | `AtomKindRegistry.KindCount == 16`, `TriggerCount == 13`, `AttachPointCount == 7` — unchanged | the registry's own self-consistency tests |
| **A8** | The four guards and both audits are green | §2's command block |
| **A9** | The `actor-hub-ssot.md` §6 row exists and names order 400 | re-grep after the change; evidence rule 6 |
| **A10** | **Erosion punishes breadth by a stated margin, in a stated direction, resolved above its own half-width** — §11.1 | one `squad-harness` run of the four arms in §11.1, reported with a 95% interval. This is the taxonomy's open question 2, the acceptance test for the whole design, and the map's gate on `tree-language --write` |

### 11.1 A10 in full — the one criterion that has to be able to fail

**A10 is the one that matters.** A1–A9 prove the wiring; A10 proves the wiring was worth doing. If
Erosion costs a spread build no more than it costs a corner, §4c's claim is INFERENCE that did not
survive measurement, and `tree-plan` needs to know that before it reserves deep-tier budget.

**Which is exactly why the old wording was a defect.** It read *"produces a **different** win share for
a spread defender than for a corner defender"* — no effect size, no direction, no half-width, against a
trial harness with a **measured 0.9pp noise floor at 3,000 trials** (`Marginal.cs:21-23`). **Any two
cells differ.** A criterion that cannot fail is not a gate, and this one now releases ~4,680 model
calls for the generic corpus and ~105,840 for species (`passive-tree-map.md:42-47`). Stated properly:

#### The quantity

Four arms, one attacker, one Θ, one seed stream. `squad-harness` §10.1 owns producing them.

```text
ΔW_spread = W(corner attacker WITH erosion  vs spread defender)
          − W(corner attacker WITHOUT erosion vs spread defender)
ΔW_corner = W(corner attacker WITH erosion  vs corner defender)
          − W(corner attacker WITHOUT erosion vs corner defender)
D         = ΔW_spread − ΔW_corner
```

#### The three bars, and where each number comes from

| Bar | Value | Where it comes from |
|---|---|---|
| **Direction** | `D > 0`, and neither arm negative | §4c's whole claim is that Erosion's value reads the **opponent's** breadth — it is the only mechanism in the taxonomy that raises corner-vs-spread without raising corner-vs-corner. A negative `D` is a refutation, not a small pass. Neither arm may be negative because Erosion removes mitigation and never adds damage |
| **Effect size** | 95% **lower** bound on `D` above **3.0pp** | Anchored to the cell Erosion exists to move. `passive-tree-ideal.md` §3.3's measured matrix puts a corner attacker at **41.2%** against a spread defender and **50.0%** against another corner — an **8.8pp** deficit that §3.5 then swept `b ∈ {0,2,5,10,20}` × `Fmax ∈ {1.0,1.25,1.5}` across and could not close by a single point. `tree-plan` reserves deep-tier budget for several mechanism nodes, so **one Erosion node at full stack carrying about a third of that gap is a coherent path to closing it.** Below that, Erosion is flavour, and flavour does not justify the corpus spend |
| **Resolution** | `D`'s own 95% half-width **≤ 1.0pp**, reported | `D` is a linear combination of four win-share estimates, so absent common random numbers its half-width is `1.96 · 2 · sqrt(0.25/n) = 1.96/√n` — **1.0pp at n ≈ 38,400**, which is `squad-harness` §9.2's existing `--refine 40000` tier. Its common random numbers pair the with/without arms on the identical seed, so that is the conservative bound, not the target |

Plus one **selectivity** bar, independent of the three: `ΔW_spread ≥ 2 × ΔW_corner`. This is what
separates *"Erosion punishes breadth"* from *"Erosion is a damage node with extra steps"*. A node that
raises both cells equally moves corner-vs-corner too — the cell `balance-guard` already reports and
§3.3 warns this design must leave alone.

**3.0pp and 2× are design thresholds and are stated as such**, not tunables: they are the bar a
decision is taken against, not numbers a balance pass would retune, so §7's *"no tunable is
introduced"* still holds. Changing either is a change to this spec.

#### Three verdicts, not two

| Verdict | Condition | What follows |
|---|---|---|
| **PASS** | 95% lower bound on `D` > 3.0pp **and** `ΔW_spread ≥ 2 × ΔW_corner` | §4c holds. `tree-plan` may reserve deep-tier budget for Erosion-class nodes; the `tree-language --write` gate opens |
| **FAIL** | 95% upper bound on `D` < 3.0pp, or `D ≤ 0` | §4c's inference did not survive measurement. Tell `tree-plan` before it reserves the budget — that is the whole reason this criterion exists |
| **UNRESOLVED** | the interval straddles 3.0pp, or the half-width exceeds 1.0pp | **Not a pass.** Refine, or report that the harness cannot resolve it |

⛔ **UNRESOLVED holds the gate exactly as FAIL does.** Under the old wording a run that measured nothing
would have read as a pass and released the corpus spend. That is the specific failure this rewrite
closes, and it is the one thing not to soften later.

#### What A10 actually needs wired — corrected against code this session

The old row said *"after G1 + G3."* Checked against code, that is wrong in both directions: the
measurement that matters needs **neither**, and the half that does need wiring needs **G2**, which the
old row did not name, plus a producer nothing has scoped. A10 splits cleanly:

| | What it proves | What it needs |
|---|---|---|
| **A10a** — the static snapshot difference | §4c's **causal** claim: a flat per-layer subtraction costs breadth more than focus | **nothing.** `BattleActorSetup.ChannelMods` is the caller's own additive derived-channel overlay, `BattleStatComposer.Compose` folds it in against the full registered channel set (throwing on an unknown id), and `OverlayCombatCalculator` reads all eight defensive families off the defender's snapshot — absorption `:109`, reduction `:116`/`:160`, dodge `:118`/`:163`, parry and block rate `:183-184`. This is doc 05 §6.4 step 1: *"zero — it is a design constraint, not a task"* |
| **A10b** — the shipped vehicle reproduces A10a | the stacking on-hit status delivers the same differential the static form measured | G1 + G2 **+ a Battle producer that does not exist yet** (below) |

**The Battle producer, named rather than smuggled into G2.** Erosion as designed is a stacking
`status.apply` on `OnDamageDealt`. In Battle, `BattleStatusSpec` is
`(StatusId, MagnitudePerPulse, DurationMs, PeriodMs, GrantChanceMilli)` — **it carries no `StatMods` at
all** — and `BattleDerivedModifierLedger.Add` has exactly one caller in `src/`, the construction-time
`foreach (var aura in setup.ActiveAuras)` loop in `BattleRunState`. So G1's composer and G2's
per-round recompose are necessary and **not sufficient**: something has to put a landed status's
derived-channel mods into that ledger. That work is **not** in §3's modified-files table and is not
scoped here. A10b waits on it; A10a does not.

**G3 is the Sim path** (`tools/CombatSim` → `FoundationHarness`). It is what lets a `stat.derived`
bind be scored at all (§4.3), and it is off A10's critical path entirely, because `squad-harness`
resolves over `BattleEngine`.

**A correction owed to `passive-tree-map.md`, which this module may not edit.** Its build-order note
says *"land `mechanism-wiring` G1 and G3, give A10 an effect size and a half-width, run it, then spend
the calls."* Verified against code this session, **A10a needs neither G1 nor G3** — the harness can run
it in wave 0, which makes the gate cheaper than the map assumes rather than more expensive. The gate
itself is right and stands.

#### Where it is measured

`squad-harness`'s `duelTrials` and `squadTrials` columns, never `duelClosedForm`:
`DominanceGuard.Measure` takes `IReadOnlyList<AptitudeAllocation>` and nothing else, so a
channel-level mechanism has no way into the closed form through it (§8). The harness's side is
`spec-squad-harness.md` §10.1, which carries the same four arms, the same bars and the same
three-verdict table.

---

## 12. Open questions

Two, both genuinely open. Neither is a template slot.

1. ~~**Does the L2b resist path get to read status-granted resist channels on the first landing?**~~
   ✅ **CLOSED 2026-09-05 by the owner: yes — a status contributes everything it writes, resist
   included.** Two facts found while answering narrowed this from a combat-math risk to a content
   rule, and both were verified in code:

   - **Resistance and potency derived stats already work.** `ResistanceEvaluator` already reads
     `ActorDerivedSnapshot` for attacker and defender and already keys on
     `DerivedStatChannels.StatusImmune(tag)` / `StatusImmuneReduction(tag)`. Aptitudes, items,
     traits and auras all reach the roll correctly today. **Nothing about that path is broken.**
   - **No shipped status writes a derived stat at all** — `grep '"stat"' data/seed/` returns
     nothing. The capability exists, has never been authored against, and composes nothing.

   So G1 changes the behaviour of **zero shipped content**. The order-sensitivity it introduces
   (`warding` then `wither` ≠ `wither` then `warding`) only becomes reachable when a tree node
   authors such a status — content this program has not written. That makes it a **design
   constraint on authoring**, not a regression risk, and the alternative (compose everything
   *except* resist) was rejected because it would make the subsystem lie about what it composes.

   **Required by this closure:** a dedicated feedback-path test, and a note in `tree-language`'s
   authoring rules that a status raising `status.resist.*` makes application order significant.

2. **Does this module take `aura-skill` T13's job?** `BattleRunState`'s own comment names T13 as the
   owner of the live mid-match toggle. This module needs less than that — a per-round recompose — and
   the ledger's empty-case no-op makes it safe. **Recommendation: take the per-round call, leave the
   toggle event to T13, and note the split in `aura-skill`'s task list.** Needs that program's ack, not
   a decision from this one.

---

## 13. Decisions implemented

| Decision | What this module does about it |
|---|---|
| **D22** — passives compose from the shipped atom catalog, no passive-specific effect vocabulary | **Implemented as a build constraint.** Zero new kinds, triggers, attach points or channels. Every mechanism this unblocks is `status.apply` + `ModifyStat` + an existing derived channel, or an `IActorStatSubsystem` |
| **D5** — `Fmax` stays a small nudge; the multiplier is not the lever | **This module is the lever.** §3.5 measured that no `Fmax` and no `b` reverses the ordering; mechanism does, and mechanism is what these four lines gate |
| **D13** — the plan must distinguish MECHANISM nodes from MAGNITUDE nodes, and deep tiers must carry mechanisms | **Makes the distinction real.** Without G1 the "mechanism" category is nominal: a mechanism node would compile, bind, and change no number. `tree-plan` reserving deep-tier budget depends on this — and on **A10 passing first**, which is why §11.1 states an effect size, a direction and a half-width rather than an outcome |
| **D21** — every actor carries its own tree state | **Per-`StatContext`, never global.** The subsystem is instance-scoped with a per-context delegate and no static cache, so two actors on the same board resolve independently. Pinned by the "same channel for host Z2 is unchanged" arm of A1 |
| **D33** — squad-scope measurement | **G3 is the coupling** (§8). The harness's balance numbers land as tunables later; what this module owes it is the ability to score a mechanism node at all. ⚠️ D33 was amended to *"not a gate"*; **A10 is a separate thing and it IS a gate** (`passive-tree-map.md:42-47`, §11.1). D33 asks whether the 1v1 ordering survives at six actors; A10 asks whether mechanism does what magnitude provably does not. Only the second one holds `tree-language --write` |
| **D16** — conversion nodes rewrite element payload tags | ⛔ **EXPLICITLY EXCLUDED** (§9). A real gap, needing a 17th kind and a reviewed `decisions.md` change. Allocate no budget to conversion nodes until B2 lands |

### Decisions this module could NOT place

Stated rather than hidden, per the gate's honest-gap rule:

- **D14** (property-based exclusion, printed as a runtime no-op) — needs an **atom-tag vocabulary**.
  Tags are free-form JSON (`AtomRow.cs:40`) and the corpus carries three semantic values, so D14's
  ~2%-of-nodes target is unreachable today. That is **B1**, owned by `content-stack`, and it has no
  home in this module. `AffixTags.cs` (124 lines, tested) exists with **zero production callers** — a
  wiring gap, but one whose only named call site is `effect-pipeline` ep-11/ep-12, which are specced
  and in no task list (**B12**).
- **D19 / D31 / D35** (status trees' gate quantity) — settled at `tree-plan`/`tree-state`. D31 is
  superseded by D35, and D35 explicitly requires **no shipped code change**.
- **D25 / D26 / D29 / D36** (rising unlock cost, tier ladder, tree shape) — `tree-plan`'s, and they owe
  §10 rows in `ssot-power-scale.md` (**B8**) that `guard-power.ps1` cannot detect the absence of.
- **D28** (cross-unlock credits one posture-mate) — `tree-resolve`'s.
- **D18** (full respec, priced in souls) — `tree-state`'s. **This spec used to say `RespecPolicy.PriceOf`
  returns Hunger and has zero callers (B11). Both halves are wrong**, re-read this session:
  `PriceOf` returns `RespecResource.Soul` (`RespecPolicy.cs:46`; the `Hunger` mention at `:15` is the
  policy's own note that Hunger was the *prior* placeholder value), and it has **two** production
  callers — `RpgStore.SpeciesRespec.cs:176` and `SpeciesBuildEndpoints.cs:91`. D18 and the shipped code
  agree, so there is nothing here for anything to be blocked on.

---

## DESIGN-GATE checklist

```
[x] I identified the subsystem(s) this touches - Stats/ActorHub, the atom layer, status,
    combat resolver, Battle, the balance guards.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md (in full), passive-tree-map.md, passive-tree-ideal.md (§2 decisions,
    §3.5, §4, §5, §13, §14, §15), research 05 (§0, §1, §3 M1-M3, §4, §5, §6, §7, §8) and
    research 15 (§1, §2, §6, §7), effect-atom/definitions.md (§9, §10, §13 D6, §14.2),
    effect-atom/atom-catalog-ssot.md (the stat.derived rows), actor-hub-ssot.md (§6, §6.1,
    §7 lifecycle, §8 ban list), design/spec-derived-stat-sheet.md (§4 render states, §5.3),
    decisions.md rows 106 and 112.
[x] I checked decisions.md for a lock covering this - "Derived-write lawn executor" (:106)
    locks the flip-order rule and the Full-vs-Partial rule; "Atom attach points" (:112)
    locks kind/attach-point growth as a reviewed change. Both are honoured, not proposed
    against.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments. Specifically re-derived this session:
    the three registrations (ActorHub.cs:145,148,155), the primary-bag upsert
    (EffectRuntime.cs:81), both AtomKindRegistry cells (:534 matrix, :535 triggers) and the
    (Lawn,Battle,Sim) field order (AtomKind.cs:54), RecomposeDerived's single caller, the
    Battle OnDamageDealt emit (BasicAttack.cs:184, in Actions/ not Battle/), and the three
    vocabulary counts by reading the constants and the AtomTriggers.All array.
    2026-09-05 audit fold adds: RespecPolicy returns RespecResource.Soul with TWO production
    callers (RpgStore.SpeciesRespec.cs:176, SpeciesBuildEndpoints.cs:91) - both halves of the
    old "Hunger, zero callers" line were wrong; the two and only two bag.ActorResolve
    assignments (FoundationHarness.cs:118 and the injector's EffectRuntime.cs:496), against
    BattleEffects.cs:55 which never sets it, which is what makes reflect switched OFF in
    Battle rather than merely unwired; BattleActorSetup.ChannelMods and
    BattleStatComposer.Compose's fold-and-throw, which is what makes A10a runnable with no
    new wiring; BattleStatusSpec's five fields, which carry no StatMods; and
    BattleDerivedModifierLedger.Add's single caller.
[x] I read the surrounding section of every rule I quoted - notably definitions.md §14.2,
    which is why G4 is scoped OUT rather than "fixed".
[~] I tested (not assumed) any constraint I am reporting. PARTIAL, and stated: I ran no
    dotnet test and no guard - this is a spec, no build is authorized. Every "inert /
    expressible" claim is read from code and cited. The two claims that MUST be run before
    they are believed are marked as such in §6: "zero goldens move" for G2, and A10's
    Erosion measurement. §4c's cost-to-spread-vs-corner claim remains INFERENCE - which is
    the whole point of §11.1 giving A10 a bar it can fail against rather than an outcome it
    cannot. The 3.0pp figure is a DESIGN threshold anchored to a measured 8.8pp deficit
    (ideal §3.3), not itself a measurement; the 1.0pp half-width is arithmetic
    (1.96/sqrt(38,400)), shown.
[x] Nothing contradicts a §2 invariant. Invariant 9 (standalone-first) is what puts the
    default-off overlay toggle out of scope; invariant 4 (single writer) and 5 (the Funnel)
    are in the Never list; invariant 13's magnitude rules are §7.
[x] Corrections propagated - carried in prose AND in Structure/Boundaries/Success criteria:
    BattleRunState's line drift (313 -> 323 -> 343 this session, which is why all three
    Battle files are now cited BY SYMBOL), PredicateNode's HpBelowMilli (:24-25 -> :26-27),
    decisions.md:106's "Sim stays None" sentence which G3 must amend, the reflect scope
    (lawn and Sim live, Battle off), and RespecPolicy. Two doc rows are listed as owed in
    §3's modified-files table, and one correction is owed OUTSIDE this file and named in
    §11.1: passive-tree-map.md's build-order note says A10 needs G1 and G3, and A10a needs
    neither. This spec may not edit that file.
```
