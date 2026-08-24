# Spec: power-index

Module **`power-index`**, wave 1 in the [power map](../power-map.md). Depends on **`power-ladder`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**. This module implements §5; it decides nothing.

**Status:** Owner approved 2026-08-24 — build authorized. Built: T1.3/T1.4 (power-todo.md), done and verified the same day.

---

## 1. Objective

Produce **`Θ`** — the single integer every other system reads instead of a raw level — from the
game's five ladders, and report where it came from.

**Done means:** `IPowerIndexProvider` exists with a Core-pure default and hydrated implementations
for injector and server; `Θ_actor` and `Θ_content` compose from weighted ladders; every read can
explain its own composition. Like `power-ladder`, **this module ships with no consumers.** Wiring is
waves 2–3.

**Why the two halves are separate.** `Θ_actor` and `Θ_content` are the two sides of every contest
(SSOT §5). Fusing them into one "power" number is what makes a difference-based system impossible to
reason about — the whole point is that a contest is `Θ_actor − Θ_content`, and both sides must be
independently inspectable.

---

## 2. Design

### 2.1 Composition

```text
Θ_actor(ctx)   = milliToWhole( Wd·daveLevel + Wa·realmsAdvanced + Wr·pvzRuns )
Θ_content(ctx) = milliToWhole( Wz·zombossLevel + Wm·dangerBand + Ww·worldTier + Wf·realmsAdvanced )
```

Weights are per-mille integers from the tuning file `power-ladder` already loaded (SSOT §9.1) — one
file, one load. `milliToWhole` rounds half away from zero, **once**, matching
[definitions.md](../effect-atom/definitions.md) §2.

**Every axis is uncapped** (owner decision, SSOT §5.2). `pvzRuns` included — a cap is a cliff, and
the one-axis rule is held by weight and measurement instead (§2.4).

**`realmsAdvanced` appears on both sides, with `Wf = Wa` exactly.** That is what keeps the contest
gap *constant* while both sides climb forever.

Two drafts got this wrong. The first had the axis on the actor side only (+20 Θ/world divergence,
audit F2). The second set `Wf = 20` against `Wa = 25`, which still diverged at +5 Θ/world and
saturated a `/100` sigmoid by world ~100 (audit F8) — it held a constant 20% *ratio*, but the sigmoid
reads the *difference*.

**`Wf = Wa` is an invariant, not a weight.** `Wf != Wa` is rejected at load. Per-world progression is
delivered by breadth and roster (SSOT §4.5), which content has no equivalent of and which live
outside `Θ` entirely.

### 2.2 The interface

Replaces `IProgressionPowerProvider`'s curve role. Core stays DB-free; hosts hydrate — the same
shape the existing provider already uses, so the migration is mechanical.

```csharp
public interface IPowerIndexProvider
{
    int ActorIndex(StatContext ctx);
    int ContentIndex(ContentContext ctx);
    PowerAxisReport Explain(StatContext ctx);   // §2.4
}
```

`StatContext` already carries `PlayerId`, `Side`, `TypeId` — the key
`InjectorProgressionPowerProvider` uses today. **`ContentContext` is new**: `(dangerBand, worldTier,
zombossLevel, realmsAdvanced)`, a plain record, because content has no `StatContext` and forcing one
would put a fake actor on every wave definition.

> **Correction, found building T1.3 (2026-08-24):** this section originally listed `ContentContext`
> as three fields — `(dangerBand, worldTier, zombossLevel)`, omitting `realmsAdvanced`. But §2.1's
> own formula is `Θ_content = Wz·zombossLevel + Wm·dangerBand + Ww·worldTier + Wf·realmsAdvanced`, and
> SSOT §5.1 is explicit that `realmsAdvanced` must land on **both** sides at `Wf = Wa` to keep the
> actor/content gap constant — without a fourth field, `ContentContext` could never even be
> constructed for the F2/F8 divergence tripwire's "500 simulated worlds" test (§5). Per this file's
> own header — "where this spec and the SSOT disagree, the SSOT wins" — the field is added. Everything
> else on this page (the interface, the implementation table, the testing table) already assumed a
> `realmsAdvanced`-bearing `ContentContext`; only this one prose line was stale.

| Implementation | Lives in | Source |
|---|---|---|
| `StubPowerIndexProvider` | Core | returns `0` for both — the identity; `P(0) = C` |
| `HydratedPowerIndexProvider` | Core | reads an injected snapshot; no I/O |
| `InjectorPowerIndexProvider` | Injector | replaces `InjectorProgressionPowerProvider` |
| `ServerPowerIndexProvider` | Server | reads `rpg_actor_progression` + `rpg_worlds` via Data |

### 2.3 `Wm` is null until the world program lands

`WmMilli: null` loads fine and **throws `PowerWeightMissing` when `ContentIndex` first needs it**
(SSOT §9.1: reject, never guess). Deferring the throw to first use rather than to load is deliberate:
`Θ_actor` and everything in wave 2 must be buildable and testable while the world program is still
deciding its weight. A load-time throw would block work that has no dependency on it.

### 2.4 The axis report — PS-6 made measurable

Rule PS-6 says PvZ runs must never become the fastest source of `Θ`, **and that this is measured, not
assumed**. `Explain` returns the per-axis contribution and share:

```csharp
public sealed record PowerAxisReport(
    int Total,
    IReadOnlyList<PowerAxisContribution> Axes);   // (AxisId, Milli, Whole, SharePermille)
```

Shares are per-mille integers summing to 1000 ± rounding drift, and the drift is asserted ≤ 1‰. This
is the same shape `economy-principles.md` §13 uses for every other balance claim here: an assertion
with a metric behind it, not a constant chosen once and trusted forever.

**Cheap by construction** — the report is the composition, so `Explain` and `ActorIndex` share one
code path and `ActorIndex` is `Explain(...).Total` with the allocation elided. Two implementations
would let them drift, which is precisely the class of bug this program exists to end.

### 2.5 What this module does *not* do

- **No curve.** `P(Θ)` is `power-ladder`'s. This module produces the index only.
- **No magnitudes.** A caller wanting a number calls `PowerLadder.Value(Θ)` itself (PS-3: contests
  read `Θ`, magnitudes read `P(Θ)` — the *caller* chooses, and that choice is reviewable at the call
  site rather than hidden here).
- **No hydration policy.** Caching and invalidation belong to each host.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PowerIndex"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests        # DAL boundary — the server impl reads through Data
.\scripts\guard-dal.ps1
```

---

## 4. Structure

```
src/FusionRpg.Core/Power/IPowerIndexProvider.cs      (new — interface + Stub + Hydrated)
src/FusionRpg.Core/Power/ContentContext.cs           (new — dangerBand, worldTier, zombossLevel, realmsAdvanced — §2.2 correction)
src/FusionRpg.Core/Power/PowerAxisReport.cs          (new — §2.4)
src/FusionRpg.Core/Power/PowerIndexComposer.cs       (new — the weighted sum, pure)
src/FusionRpg.Injector/Stats/InjectorPowerIndexProvider.cs   (new — replaces InjectorProgressionPowerProvider)
src/FusionRpg.Server/Power/ServerPowerIndexProvider.cs       (new — hydrates via Data)
tests/FusionRpg.Core.Tests/Power/PowerIndexTests.cs
tests/FusionRpg.Core.Tests/Power/PowerAxisReportTests.cs
```

`InjectorProgressionPowerProvider` is **deleted, not deprecated** — its `SetLevel` has zero callers
(SSOT §6.4), so nothing depends on it and leaving it would leave two providers to choose between.

---

## 5. Testing strategy

| Case | Expect |
|---|---|
| Stub provider | `ActorIndex == 0`, `ContentIndex == 0` — the identity |
| Single axis | `Wd=1000`, dave 10, others 0 → `Θ_actor == 10` |
| Weighted sum | dave 10, realms 3, runs 40 at `1000/25000/250` → `10 + 75 + 10 == 95`, exactly |
| Rounding once | fractional weights round **half away from zero** at the sum, not per-axis: `Wr=250` with 3 runs → `0.75 → 1`, not `0` |
| Uncapped runs | 10,000 runs contributes 2,500 — **no saturation**, asserted explicitly so a cap cannot be reintroduced without failing a test |
| Report sums | Σ axis contributions `== Total`; Σ shares `== 1000 ± 1‰` |
| Report ≡ index | `Explain(ctx).Total == ActorIndex(ctx)` over a generated matrix — the two paths cannot drift |
| **PS-6 tripwire** | at shipped weights, a player with 200 realms and 10,000 runs has run-share **below** realm-share. Fails loudly if `Wr` is ever retuned past the one-axis rule |
| **F2/F8 divergence tripwire** | over 500 simulated worlds at `Wf = Wa`, `Θ_actor − Θ_content` is **exactly constant** — not "slowly growing". Asserted as equality, because a ratio test would pass on the diverging `Wf = 20` variant that F8 rejected |
| `Wf != Wa` rejected at load | `PowerWeightInvalid` naming both — a diverging contest is not a tuning choice |
| `Wm` null | `ContentIndex` throws `PowerWeightMissing` naming the weight; `ActorIndex` **still works** |
| Negative ladder input | clamped to 0, not thrown — a missing progression row is absence, not corruption |
| Purity | `PowerIndexComposer` allocation-free; same inputs → same output, 1000 calls |
| Injector parity | `InjectorPowerIndexProvider` with no hydrated levels returns `0`, matching the old provider's `GetLevel → 0` — **the deletion changes no live behaviour** |
| DAL boundary | `guard-dal.ps1` green; no SQL outside `FusionRpg.Data` |

---

## 6. Boundaries

**Always**
- Compose from the tuning weights `power-ladder` loaded. One file, one load.
- Round once, half away from zero, at the sum.
- Keep `Θ_actor` and `Θ_content` separately inspectable.
- `Explain` and `ActorIndex` share one code path.

**Ask first**
- Adding a sixth ladder to either side — that is an SSOT §5 change.
- Making any axis non-linear. The arithmetic composition is the SSOT's decision.
- Caching inside Core.

**Never**
- Cap an axis. Owner decision, SSOT §5.2 — the weight is the instrument.
- Compute a magnitude here. `Θ` only; `P(Θ)` is `power-ladder`'s.
- Guess `Wm`. Throw naming it.
- Read SQL from Core, or keep `IProgressionPowerProvider` alive alongside this.

---

## 7. Success criteria

1. Weighted composition exact on the §5 table.
2. `Explain(ctx).Total == ActorIndex(ctx)` over a generated matrix.
3. The PS-6 tripwire passes at shipped weights and fails when `Wr` is inflated — proving it measures.
4. `Wm` null: content throws naming it, actor unaffected.
5. `InjectorProgressionPowerProvider` deleted; suite green; **no golden re-blessed.**
6. Guard suite and `guard-dal.ps1` green.

---

## 8. Open

**None.** All six weights have starting values (SSOT §5.3), the no-cap decision is recorded, and
`Wm = 5` is derived from the shipped `SectorTypeCatalog` bands (SSOT §5.3), and its absence has a
defined behaviour regardless. The world program may move the weight; it owes nothing.

**Resolved during T1.3's build (2026-08-24):** §2.2's `ContentContext` prose was missing
`realmsAdvanced` — corrected in place, see the note there. No design decision changed; the formula
and testing table were already correct, only one field list was stale.
