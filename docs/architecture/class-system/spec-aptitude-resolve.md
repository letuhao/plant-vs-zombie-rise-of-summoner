# Spec: `aptitude-resolve` — points into channels, once, for both engines

**Module id:** `aptitude-resolve` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `aptitude-tuning` · **`distribution-reconcile`** · **Blocks:** `balance-guard`, `point-economy`, `guard-economy`, `zomboss-patterns`

---

## 1. Objective

Turn an `AptitudeAllocation` into derived-channel values, through the two PS-3 read functions, and
deliver them into **both** composition paths without writing the arithmetic twice.

```text
AptitudeAllocation  ──►  share per aptitude  ──►  for each edge in the tuning config:
                                                    contest   -> k · share^γc · spanPoints
                                                    magnitude -> k · share^γm · P(Θ)
                                                  ──►  IReadOnlyList<DerivedModifier>
```

**Users:** the overlay's `ActorHub` path; the battle/web engine; `deterministic-core`, which must
predict exactly what this produces.

**Success is measurable:** the same allocation resolves to byte-identical channel values through
`DerivedComposer` and `BattleStatComposer`, asserted by a test — and `deterministic-core` reading the
same config predicts the same numbers.

---

## 2. The seam is `IActorStatSubsystem` — ⛔ **CORRECTED 2026-08-26**

**An earlier version of this section said the seam was `ClassStatPlugin` (`rpg.class`, Order 100) and
that this module should fill its empty `Contribute`. That was wrong.** The owner flagged the stubs;
the sweep that followed is [spec-distribution-reconcile.md](spec-distribution-reconcile.md), and it
found the plugin is on the **wrong pipeline**.

**There are two, and they do not meet:**

| Pipeline | Contract | Carries | Composed by |
|---|---|---|---|
| **Primary** — where `ClassStatPlugin` sits | `IStatModifierPlugin.Contribute(StatContext, IModifierBagEditor)` → `Upsert(StatModifier)` | `StatModifier` | `StatComposer` → `EntityFinal` |
| **Derived** — where aptitudes belong | `IActorStatSubsystem.ContributeDerived(StatContext, ICollection<DerivedModifier>)` | `DerivedModifier` | `DerivedComposer` → `ActorDerivedSnapshot` |

Aptitudes feed **83 derived channels** (ideal §4.2), and a derived channel is not reachable from a
`ModifierBag`. So:

> **This module implements `IActorStatSubsystem`, registered through `ActorHub.Register`** —
> the same door `RpgProgressionSubsystem` already uses
> ([ActorHub.cs:31-37, 53-61](../../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs)). It does **not**
> touch `ClassStatPlugin`, whose fate is `distribution-reconcile` §3.1's to decide.

**Why the mistake is worth keeping on the page.** The stub was found, its registration was verified at
`StatSystemBootstrap.cs:17`, and a shipped test even pins its order — and it was still the wrong seam.
**Finding a seam is not the same as reading what flows through it**, and that is a sharper version of
the gate's own rule than "open the file".

**Two contract consequences that survive the correction**, because `IActorStatSubsystem` is registered
the same way:

1. **`ContributeDerived` must be idempotent.** *"Non-idempotent `Contribute` (double Upsert without
   Withdraw)"* is a named anti-pattern ([stat-system.md](../stat-system.md) §Anti-patterns), and
   `ActorHub.Register` replaces by `SubsystemId` — so a double registration is silent. The resolver is
   pure and emits its whole output each call.
2. **Changing an allocation is a feature-state change, then `Invalidate`.** Not a `WithdrawSource`:
   *"`WithdrawSource` only clears the **session** bag. It does **not** stop a plugin from re-emitting
   the same `SourceId` on the next `Contribute`."* Store → `Invalidate` → re-`Resolve` from `Y0`.
   [spec-point-economy.md](spec-point-economy.md) owns the store half.

### 2.0 ⛔ Three preconditions this module cannot satisfy for itself

All three are [spec-distribution-reconcile.md](spec-distribution-reconcile.md)'s, and until they land
this module's central test **fails by construction rather than by a bug**:

| # | Precondition | Today |
|---|---|---|
| 1 | The battle path can carry aptitude output | **Partly.** It never runs subsystems (§2a) — by design, and that stays. But its known-channel set is narrower than the distribution, so a `ChannelMod` on `resource.*`, `skill.*`, `move.range`, `progression.*` or `status.duration/intensity.*` **throws** (`distribution-reconcile` §3.2a) |
| 2 | `Θ` is hydrated | **It is not.** `CheatState.cs:32` builds the hub with no `IPowerIndexProvider`, so `Θ = 0` — and its own comment says `PowerIndex` is *"inert until then"* |
| 3 | `progression.bonus.*` has an owner | A level-scaled stub with bare literals, superseded by ideal §4's allocation |

> **Precondition 2 is the one that would waste a day.** The magnitude read is `k · share^γ · P(Θ)`, so
> **at `Θ = 0` every magnitude edge collapses to `P(0) = C`** — the same floor for every build. Contest
> edges keep working, because they are `Θ`-free by construction. The symptom is *"rates behave, every
> magnitude is flat"*, which reads exactly like a coefficient problem and is not one.

---

## 2a. Two composers, and only one of them has a subsystem pipeline

Verified in code this session:

| Path | Entry | Merges by |
|---|---|---|
| **Overlay / PvZ** | [DerivedComposer.cs:13](../../../src/FusionRpg.Core/Stats/Derived/DerivedComposer.cs) — `Compose(IEnumerable<DerivedModifier>?)` | validating each modifier's channel, then `ComposeChannel` per registered channel |
| **Battle / web** | [BattleStatComposer.cs:88](../../../src/FusionRpg.Core/Battle/BattleStatComposer.cs) — `Compose(BattleActorSetup)` | `ActorDerivedSnapshot.FromValues`, then `AddAffinity`, then trait stat mods |

`BattleStatComposer`'s own summary calls itself *"the web-mode analogue of the ActorHub compose
path"*, and [decisions.md](../decisions.md)'s *Combat resolution SSOT* row is unambiguous: **"One
combat formula set + one apply path, everywhere."**

> **So this module produces `DerivedModifier` rows and nothing else.** It does not know which composer
> will consume them.

> **⛔ But only the overlay path has a subsystem pipeline.** `BattleStatComposer` builds its snapshot
> directly and merges **trait mods** and **`setup.ChannelMods`** — both additive, both validated — while
> never touching `ActorHub`, `DerivedComposer` or `IActorStatSubsystem`. An earlier version of this
> section read that trait merge as proof the seam existed for subsystems too. **It is not.**

> **The battle-side seam is `BattleActorSetup.ChannelMods`, and the composers stay separate on
> purpose** (`distribution-reconcile` §3.2, decided 2026-08-26 after reading the battle stream's own
> plan). `StarPolicy` already contributes progression stats to a battle actor exactly this way —
> *"ChannelMods — never engine changes (battle goldens stay byte-identical)"* — and aptitudes become
> the fifth producer. **So this module emits one thing and it is adapted at two seams**: a subsystem on
> the overlay, a `ChannelMods` list on battle. No fourth composition path, and no change to
> `BattleStatComposer`'s logic.

**Why this is not merely tidy.** `residual-fit` compares the closed form against the simulator, and
`balance-guard` asserts a property of the shipped numbers. If the two composers resolved aptitudes
independently, a divergence between them would show up as *model error* and be fitted away — the exact
failure `aptitude-tuning`'s "one config, two consumers" rule exists to prevent, one layer down.

### 2a.1 ⚠️ A double-count is already sitting in the code, waiting

[BattleStatComposer.cs:96-98](../../../src/FusionRpg.Core/Battle/BattleStatComposer.cs) carries this
warning, in the file:

```csharp
// battle-adoption mapping table: Atk is the resolver's BaseOverlayDamage - it must
// NOT also sit in power.omni (double count). Defense stays: the defense channel is
// its only consumer.
```

**⛔ MEASURED 2026-08-26 — two aptitudes feed both sides, not one.** Scanned the shipped edges in
`tools/CombatSim/tuning/aptitudes.v1.json`:

```text
Might      combat.power.omni  +  progression.bonus.atk   k=10000
Ferocity   combat.power.omni  +  progression.bonus.atk   k= 6000
```

An earlier version of this section named only `Might`, from ideal §4. **`Ferocity` was missed** — so
the rule below is red on the shipped config for **2 of 12 aptitudes**, which makes its test a
regression test rather than a hypothetical.

Ideal §4 gives `Might` `combat.power` **and** `progression.bonus.atk`. `progression.bonus.*` is added to Writer input at `AppliedCombat`
([decisions.md](../decisions.md) *P2*), and `atk` becomes `BaseOverlayDamage`. So a `Might` point can
reach the damage number twice by two legitimate routes.

> **Rule for this module: one aptitude may contribute to `combat.power.*` or to
> `progression.bonus.atk`, never both in the same resolution.** A test asserts it (§7 test 6). This is
> the third recorded instance of this defect class in this file's history and the first one caught
> before it shipped.

**⛔ CORRECTED 2026-08-27 — "red today" was not verified against the live overlay pipeline, and it does
not hold.** class-system-todo.md P3.2 traced the actual dispatch, not the comment: `EffectRuntime.cs:
417-428` wires `bag.CombatMath = new ConditionalOverlayCombatMath(overlay) { IsEnabled = () =>
OverlayCombatFeature.Enabled }`, and `ConditionalOverlayCombatMath.Finalize` (`ConditionalOverlayCombatMath.cs:21-26`)
is a strict either/or — enabled calls `_overlay.Finalize(...)` and **returns its result**; disabled
calls `_passThrough.Finalize(...)` instead. Never both for the same hit. `OverlayCombatCalculator.cs`
computes its own damage number from `combat.power.omni` directly (`:97-98`) and — grepped, zero matches
— never reads `EntityFinal.Atk`/`DefenseFlat` at all. So for any given hit, either vanilla's own
computation runs (using `Atk`, boosted by `progression.bonus.atk`) **or** the overlay calculator runs
(using `combat.power.omni`) — never summed. **The line this section quoted, "Atk is the resolver's
BaseOverlayDamage", is `BattleStatComposer.cs`'s own comment about `BattleActorSetup.Atk` — the
`battle-adoption` program's own (unbuilt, "Build held") internal wiring, a genuinely different Atk
consumer from `AppliedCombat`'s `EntityFinal.Atk`.** The two got conflated across a compaction
boundary; §2a.1's citation of `decisions.md`'s *P2* row is accurate, but the leap from "`Atk` reaches
`AppliedCombat`" to "and *also* reaches `OverlayCombatCalculator`'s damage number" does not hold once
`ConditionalOverlayCombatMath`'s dispatch is actually read.

**What survives the correction, and why the guard is not deleted:** the *rule* — "one aptitude may
contribute to `combat.power.*` or `progression.bonus.atk`, never both" — is still worth holding, as a
**forward-looking safeguard**, not an active-bug fix. If `battle-adoption` ships its own mapping table
(§ above: "Atk → BaseOverlayDamage only, `combat.power.omni` baseline removed") the way it is currently
specced, a *battle-mode* actor's `Atk` (fed by `progression.bonus.atk`) would become exactly the input
that section's comment already warns about — at which point this rule stops being precautionary. G3
stays in `guard-class-system.ps1`, and `data/tuning/aptitudes.v1.json` is **not edited** by this
correction — there is no live bug to fix today, only a claim to retract and a guard to keep pointed at
the future case it was actually protecting against.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter AptitudeResolve
dotnet test tests\FusionRpg.Core.Tests --filter "Composer|BattleStat"    # both seams still green
.\scripts\guard-single-writer.ps1
python scripts\audit-overflow.py --targets A3
```

---

## 4. Project structure

```text
src/FusionRpg.Core/Stats/Aptitudes/AptitudeResolver.cs        allocation + tuning + Theta -> DerivedModifier[]
src/FusionRpg.Core/Stats/Derived/Subsystems/AptitudeSubsystem.cs   IActorStatSubsystem - the registered seam
# AptitudeReadFunctions.cs is aptitude-tuning's (decided 2026-08-26) - this module CALLS it
tests/FusionRpg.Core.Tests/Stats/Aptitudes/AptitudeResolverTests.cs
tests/FusionRpg.Core.Tests/Stats/Aptitudes/BothComposersAgreeTests.cs
```

**No new composer, no new snapshot type, no new pipeline.** `ActorHub.Register(IActorStatSubsystem)` is
the shipped door and `RpgProgressionSubsystem` is the shipped example; this module adds a second
subsystem through it. **`ClassStatPlugin` is untouched** — `distribution-reconcile` §3.1 decides it.

---

## 5. Code style

```csharp
/// <summary>
/// Pure. Allocation + tuning + Theta in, derived modifiers out. No I/O, no statics, no cache.
/// </summary>
public static class AptitudeResolver
{
    public static IReadOnlyList<DerivedModifier> Resolve(
        AptitudeAllocation allocation, AptitudeTuning tuning, long theta);
}
```

**Four rules:**

1. **`long` all the way to the channel value.** A magnitude edge multiplies `P(Θ)`, which is quadratic.
   Widen before multiplying — `(long)k * p`, never `(long)(k * p)` — and **divide by 1000 exactly once,
   last**. `double` appears only for `share` and the exponent, which are bounded ratios.
2. **`share` comes from `AptitudeAllocation.Share`, never recomputed here.** One denominator, one place
   ([spec-primary-stats.md](spec-primary-stats.md) §6 rule 4).
3. **No cache, and if one is ever added, it is `AsyncLocal` keyed by reference identity.**
   `BattleStatComposer` carries a 25-line comment recording that a bare `static readonly` cache was the
   **same defect three times** — `E25`, `PvzStatsSheetComposer`, and its own `KnownChannels` — silently
   rejecting legitimately registered channels when the roster was swapped, then thrashing across
   concurrent tests until it became `AsyncLocal`. The cheapest way not to be the fourth is to have no
   cache; the second cheapest is to copy that idiom exactly.
4. **Overflow throws.** No `unchecked` on a magnitude path.

---

## 6. The two read functions, restated as this module implements them

From [spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2.1 — repeated here because this is where
they execute. **This module does not implement them.** `AptitudeReadFunctions` belongs to
`aptitude-tuning` (decided 2026-08-26) precisely so `deterministic-core` and this module cannot drift:

```text
contest   value  =  kMilli/1000 · share^γc · spanPoints        <- Theta-FREE by construction
magnitude value  =  kMilli/1000 · share^γm · P(Theta)          <- proportional to P(Theta)
```

**Those two properties are the premises of the invariance theorem**
([class-analytic-balance-2026-08-25.md](../../research/class-analytic-balance-2026-08-25.md) §3), and
they are what makes win rate exactly `Θ`-invariant. They are asserted here as tests, not assumed —
§7 tests 3 and 4 — because if either fails the invariance is gone whether or not any matchup looks
wrong.

**`familyRead` decides which applies**, per family, never per edge. This module reads that decision; it
does not make it.

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Both_composers_resolve_the_same_values` | The same allocation through `DerivedComposer` and `BattleStatComposer` yields identical channel values. **The module's reason to exist** |
| 2 | `Every_resolved_channel_is_registered` | Against `DerivedStatRegistry.CreateDefault()`. A typo'd channel reads zero silently otherwise |
| 3 | `Contest_read_is_theta_free` | Output identical at `Θ` = 10 and 5,000 |
| 4 | `Magnitude_read_is_proportional_to_P` | Doubling `P(Θ)` doubles the value |
| 5 | `Empty_allocation_resolves_to_nothing` | Zero shares → zero modifiers, not twelve zero-valued ones — the composer should see no aptitude contribution at all |
| 6 | `No_aptitude_reaches_atk_twice` | §2a.1. Scans the shipped edges for any aptitude feeding both `combat.power.*` and `progression.bonus.atk` |
| 7 | `Magnitude_at_high_theta_is_exact_and_throws_on_overflow` | `Θ` in the millions; `long` arithmetic exact; a deliberately oversized coefficient throws rather than wrapping |
| 8 | `Divide_by_1000_happens_once` | A per-mille coefficient chain produces the same value as the same arithmetic done in one division |
| 9 | `Contribute_derived_is_idempotent` | Calling it twice yields one set of modifiers, not two. §2's named anti-pattern, and `ActorHub.Register` replaces by `SubsystemId` so a double registration is silent |
| 10 | `Magnitude_is_flat_when_theta_is_zero` | §2.0 precondition 2's symptom, pinned so it is recognisable rather than mistaken for a coefficient bug |
| 11 | `Resolver_matches_the_simulator` | Same allocation, same tuning, same `Θ` → same channel values as `tools/CombatSim`'s `AptitudeModel`. **What makes `residual-fit`'s residual a measurement instead of drift** |

---

## 8. Boundaries

**Always** — emit `DerivedModifier`; read `share` from the allocation type; widen before multiplying;
divide by 1000 last, once.

**Ask first**

- Adding a cache (§5 rule 3).
- Any change that makes the two composers take different paths.

**Never**

- Write to a Unity field, or to `EntityStatWriter` — this module produces modifiers and stops
  (`guard-single-writer.ps1`).
- Recompute `share`.
- Let one aptitude reach `atk` by two routes (§2a.1).
- Add a fourth composition path, unify the two composers, or change `BattleStatComposer`'s compose
  logic (§2a) — T5's byte-identical gate is why.
- Accumulate inside `Contribute`, or expect `WithdrawSource` to undo a respec (§2).
- `float` or `int` on a magnitude path.
- Read `Posture` (it is a UI/AI read, [spec-primary-stats.md](spec-primary-stats.md) §2.2).

---

## 9. Success criteria

1. Both composers produce identical values for the same allocation, asserted.
2. The contest read is `Θ`-free and the magnitude read is proportional to `P(Θ)`, asserted.
3. Every resolved channel is registered.
4. No aptitude reaches `atk` twice.
5. Exact at high `Θ`; overflow throws.
6. Values match `tools/CombatSim` for the same inputs.
7. The seam is one registered `IActorStatSubsystem`; no new pipeline and no fourth composition path.
8. `distribution-reconcile`'s three preconditions (§2.0) are landed — **criterion 1 cannot pass without them**.
9. **Zero goldens move on an empty allocation.** Nothing today has an allocation, so wiring the
   resolver with nobody allocated must change no hash — the property that lets this land before
   `point-economy`.

---

## 10. Design-gate checklist

```
[x] Subsystems identified: stats (primary + derived), combat damage, battle, power scale, overflow.
[x] Read this session: DESIGN-GATE.md, decisions.md (Stats, Stat compose, Actor Hub SSOT, P2,
    Combat resolution SSOT, Power scale, Magic numbers rows), stat-system.md,
    ssot-power-scale.md §4.6/§4.7, CLAUDE.md overflow table, spec-aptitude-tuning.md,
    class-system-ideal.md §4/§7a.
[x] Verified against CODE: DerivedComposer.cs:13-36 (the modifier seam and channel validation),
    BattleStatComposer.cs:13-40 (the AsyncLocal cache comment and its three-instance history),
    :88-98 (Compose, and the Atk/power.omni double-count warning quoted verbatim in §2a.1),
    StubStatPlugins.cs:3-9, StatSystemBootstrap.cs:17-23, IActorStatSubsystem.cs:5-10,
    ActorHub.cs:31-37/53-61, RpgProgressionSubsystem.cs, CheatState.cs:25-40. Read, not grepped.
[x] Read the surrounding section of every rule quoted - the Combat resolution SSOT row in full,
    PS-3 from §4.6 under its own heading.
[x] Constraints TESTED, not assumed - CLOSED 2026-08-26. §2a.1's double-count was scanned against the
    shipped edge list, not inferred: it FIRES, on Might AND Ferocity (an earlier draft named only
    Might). Separately, all 84 channels the edges name ARE registered in the catalog, but 47 of them
    fall outside BattleStatComposer's known-channel set - spec-distribution-reconcile.md §3.2a.
[x] Nothing contradicts a §2 invariant. Invariant 4 (single writer) is why §8 forbids touching
    EntityStatWriter; invariant 13 (magnitudes are long) drives §5 rule 1.
[x] Corrections propagated - §2 and §2a both CORRECT earlier versions of this same spec: the first
    named ClassStatPlugin as the seam (wrong pipeline), the second read a trait merge as proof the
    battle path takes subsystems (it does not). Both corrections are recorded in place rather than
    silently edited, and spec-distribution-reconcile.md §2 carries the same note. §2a.1's
    double-count rule is new and is carried into the map's checkpoint 2.
```

---

## 11. Related

- [spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2.1 — the two read functions this executes
- [spec-primary-stats.md](spec-primary-stats.md) — the allocation type and its `share`
- [stat-system.md](../stat-system.md) · [actor-hub-ssot.md](../actor-hub-ssot.md) — the compose paths
- [../research/class-analytic-balance-2026-08-25.md](../../research/class-analytic-balance-2026-08-25.md) §3 — the invariance premises tests 3 and 4 assert
