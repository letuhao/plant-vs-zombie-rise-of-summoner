# Spec: `poise-unification`

Module `poise-unification` in the [battle-tempo map](../battle-tempo-map.md).
**A root — depends on nothing. `reaction-lane` depends on it.**

**Read before editing:** [resource-hub-ssot.md](../resource-hub-ssot.md) ·
[class-system/spec-guard-economy.md](../class-system/spec-guard-economy.md) ·
[class-system/spec-poise-resource.md](../class-system/spec-poise-resource.md) ·
[tunables-ssot.md](../tunables-ssot.md) · [ssot-power-scale.md](../power/ssot-power-scale.md) §11.

---

## 1. Objective

**One `poise` pool and one riposte formula.**

Verification round 2 (2026-09-04) found **two independent `poise` implementations**, built ten days
apart under different programs, neither referencing the other:

| | `Combat/Guard/PoiseRuntime.cs` | `Actions/Defence/PoiseLedger.cs` + `Riposte.cs` |
|---|---|---|
| Program | class-system `P7.1–P7.3` (2026-08-27) | action `T25/T26/T27` |
| **Pool** | ⛔ its own `Dictionary<string, long>` | ✅ `ActorResourcePools` — the six-resource SSOT |
| Commit cost | `Commit(key, flatCost)` — **floors at 0, never refuses** | `TryCommit(...)` — **all-or-nothing, refuses** |
| Absorb drain | `Absorb(key, stopped, milli)` | `AbsorbDrainAmount` / `TryPayAbsorbDrain` |
| Per-tick hold | ⛔ none | `TryPayHoldTick` |
| Regen | `Regen(key, perTick, max)` — caller-driven | `ActorResourcePools.Resolve` — lazy, anchored |
| Riposte | `PoiseRuntime.Riposte` | `Riposte.DamageFromSpentPoise` |
| Exhaustion | `IsExhausted(key)` | `Resolve(...) <= 0` |

⭐ **The irony names the defect.** `PoiseLedger`'s own doc says it is *"a thin wrapper over T15's
`ActorResourcePools.TrySpend` — **never a second pool mechanism**."* `PoiseRuntime` **is** that second
pool mechanism — written first, under another program, and invisible from this one.

✅ **Both have ZERO production callers**, verified by grep. Nothing is broken today, and this module
changes no live behaviour. It exists because `reaction-lane` is the **first module that would call
one**, and calling either while both stand entrenches the fork permanently.

### 1.1 Why this is its own module and not a task inside `reaction-lane`

It edits **another program's completed, reviewed, tested work** (class-system P7.1–P7.3, 12 green
tests). That deserves its own spec, its own acceptance criteria and its own visible line in the build
order — not a bullet buried in a module about counters.

⚠️ **Owner decision 13 (2026-09-04): reconcile now, inside `battle-tempo`.** The alternative — hand the
fork to the class-system stream — was declined. This module is that decision.

---

## 2. Design

### 2.1 The pool: `ActorResourcePools` wins

Every part of `PoiseRuntime`'s pool ownership is replaced by the hub:

- It is the **resource SSOT** — `DerivedStatChannels.ResourceIds` is
  `{ hp, stamina, hunger, spirit, qi, poise }`, and `resource-hub-ssot.md` defines six pools, not seven.
- `resource.max.poise` / `resource.regen.poise` / `resource.efficiency.poise` are **already registered
  channels**. A private dictionary cannot read them, so `PoiseRuntime` would silently diverge from every
  regen, cap and telemetry value the hub owns the moment either side went live.
- ⭐ **The telemetry already reads the hub, not the runtime.** `ActorHub.ResolveDerived` emits
  `resource.regen.poise` from the channel — so today's gauge describes a pool `PoiseRuntime` does not
  use. Unifying makes an already-shipped metric truthful.

### 2.2 ⛔ The one real semantic conflict: floor-at-zero vs refuse

**This is the only place the two stacks actively contradict each other, and it must be decided, not
merged.**

| | `PoiseRuntime.Commit` | `PoiseLedger.TryCommit` |
|---|---|---|
| Insufficient poise | spends what is there, floors at **0**, returns void | spends **nothing**, returns `false` |
| Stated reason | *"exhaustion, not a hard block — this program refuses hard caps, PS-8, and a 'cannot afford to guard' refusal would be exactly that in a different shape"* | ordinary `TrySpend` all-or-nothing |

✅ **Refuse wins. Three reasons, in order of weight:**

1. ⭐ **Decision 10 depends on it.** The reaction lane declines a counter through the **typed
   `CannotAfford` refusal**, so declining is a *selectability* outcome in the intent source.
   Floor-at-zero produces no refusal at all — there is nothing for the intent source to read, and the
   lane loses the very decision decision 10 was made to create.
2. ⛔ **The PS-8 argument is a misapplication, and this module says so plainly.** PS-8 forbids **hard
   progression ceilings** — a cap on a magnitude that stops gear or levels from mattering. An
   *affordability* refusal is not a ceiling: it is what `stamina` and `qi` already do through the same
   `TrySpend`, and nobody calls those progression caps. Accepting the original reading would make every
   resource cost in the game a PS-8 violation.
3. **All-or-nothing is the hub's contract**, and a single resource behaving differently from the other
   five is exactly the drift this module exists to remove.

⚠️ **"Exhaustion, never death" is NOT what changes here, and must not.** `resource-hub-ssot.md`'s rule —
every resource except `hp` exhausts rather than kills — holds under refuse semantics exactly as it did
under floor semantics. **Refusing to pay is not dying.** Keep the test that proves it.

⚠️ **This does change documented guard behaviour** (*"an actor with 10 poise can still commit a 50-cost
guard; it simply exhausts"*). Guard is unbuilt and unwired, so **nothing observable changes** — but
`spec-guard-economy.md` §3's Reading C narrative must be updated in the same pass, or the next reader
finds a spec describing behaviour the code no longer has.

### 2.3 The riposte: keep the validating copy

The two implementations are the same arithmetic (`spentPoise × share / 1000`, widened, divided once).
They are **not** equally safe:

- ✅ **Keep `Actions/Defence/Riposte.DamageFromSpentPoise`** — it validates `shareMilli` against the
  `[0, 1000]` bound and throws outside it.
- ⛔ **Delete `PoiseRuntime.Riposte`** — it validates only `spentPoise`, so an out-of-range share
  silently produces nonsense.
- Both carry the **PS-8 bounded-ratio exemption comment** the register requires. The surviving copy keeps
  it: a bounded ratio over an **uncapped pool**, never a cap on damage.

### 2.4 Regen: the hub's lazy model replaces the per-tick loop

`PoiseRuntime.Regen(key, perTick, max)` is caller-driven and capped per call.
`ActorResourcePools.Resolve` regenerates **lazily from an anchor tick**. The mechanism is different, so
the property must be **re-proven, not assumed to carry**:

⚠️ **P7.2's `r = poiseRegen / peerPressure < 1` guarantee was proven against the per-tick loop.** Its
sustained-pressure test found a real, interesting equilibrium (drain to 0, refill by exactly
`regenPerTick`, every round). Under lazy regen the arithmetic is the same but the *observation points*
differ. **Port the test and re-run it; do not port the claim.**

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Poise|FullyQualifiedName~Riposte|FullyQualifiedName~ActorResourcePools"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~Aptitude|FullyQualifiedName~Predictor"
dotnet test tests\FusionRpg.Core.Tests
python scripts\audit-overflow.py --paths src/FusionRpg.Core/Actions/Defence
python scripts\audit-magic-numbers.py --summary
```

---

## 4. Project structure

```
src/FusionRpg.Core/Combat/Guard/PoiseRuntime.cs        pool + Riposte REMOVED; file deleted if nothing remains
src/FusionRpg.Core/Actions/Defence/PoiseLedger.cs      the surviving cost path (no change expected)
src/FusionRpg.Core/Actions/Defence/Riposte.cs          the surviving riposte (no change expected)
src/FusionRpg.Core/Balance/Analytic/PhaseModel.cs      reads poise regen -- verify it still reads the hub
docs/architecture/class-system/spec-guard-economy.md   §3 Reading C narrative updated to refuse semantics
tests/FusionRpg.Core.Tests/Combat/Guard/PoiseRuntimeTests.cs   12 tests MIGRATED, never deleted
```

⚠️ **`PhaseModel.RecoveryPerRound` already takes a `poiseRegen` parameter** fed from
`DerivedStatChannels.ResourceRegen("poise")` — the channel, not `PoiseRuntime`. Confirm it is untouched;
it is evidence the analytic layer already sided with the hub.

---

## 5. Code style

```csharp
// One pool per resource id. `poise` pays like `stamina` and `qi` do -- through the hub, all-or-nothing,
// with a typed CannotAfford on refusal. The private dictionary this replaced was a SECOND pool for a
// resource the hub already owned (battle-tempo D9); `resource.regen.poise` telemetry was already
// reading the hub, so the runtime it described was never the one being spent.
if (!PoiseLedger.TryCommit(pools, flatCommitAmount, nowTick, derived))
    return UsabilityResult.Refuse(UsabilityReason.CannotAfford, PoiseLedger.ResourceId);
```

---

## 6. Testing strategy

1. ⭐ **Every one of the 12 `PoiseRuntimeTests` properties still holds** against the hub — migrated, not
   dropped. Named explicitly, because deleting a test is how a reconciliation quietly loses a guarantee:
   - `Raising_a_guard_costs_even_when_nothing_lands` — the flat commit is unconditional.
   - `Absorb_drain_is_proportional_to_what_was_stopped`, and never exceeds the pool.
   - `Poise_at_zero_applies_exhaustion_not_death` — ⛔ **must survive the semantic change**.
   - `Riposte_scales_with_the_ladder` — an astronomical spend produces proportional output, **no clamp**.
   - `Heavy_hits_break_the_guard_and_attrition_does_not`, and the sustained `r < 1` break.
2. **The refusal is typed:** an unaffordable commit yields `CannotAfford("poise")` and **changes no
   pool** — falsifier: making it floor-and-spend must redden it.
3. **One pool, proven:** a spend through `PoiseLedger` is visible to `ActorResourcePools.Resolve` and to
   `SettleAll`. Under the fork this was false by construction.
4. **One riposte:** `PoiseRuntime.Riposte` no longer exists; an out-of-range `shareMilli` throws.
5. **Nothing else moved:** full `Core.Tests` green, goldens byte-identical, and `ProvePredictor`'s four
   axes reproduce their recorded max-diffs.

---

## 7. Boundaries

- **Always:** route every `poise` spend through `ActorResourcePools`; migrate a test rather than delete
  it; update `spec-guard-economy.md` in the same pass as the semantic change.
- **Ask first:** removing `IsExhausted` as a named concept; changing any *other* resource's semantics
  while here; touching `PhaseModel`'s regen parameter.
- **Never:** leave two pools for one resource id; keep the non-validating riposte; let the refusal reach
  `hp`; treat exhaustion as death; delete a class-system test without porting its property.

---

## 8. Success criteria

1. Exactly one `poise` pool (`ActorResourcePools`) and exactly one riposte function, repo-wide.
2. All 12 migrated properties green, exhaustion-not-death among them.
3. An unaffordable commit refuses with `CannotAfford("poise")` and mutates nothing.
4. `spec-guard-economy.md` no longer documents floor-at-zero.
5. Full `Core.Tests` green; goldens unmoved.

---

## 9. Golden movement

**None, and this is provable rather than hoped.** Both stacks have **zero production callers** — no
battle path, no damage pipeline, no resolver reads either. The change is invisible to every fixture.

⚠️ **Measure anyway.** Three predicted movers in `battle-timeline` moved nothing, and one unpredicted
mover also moved nothing. Run the goldens and report what actually happened.
