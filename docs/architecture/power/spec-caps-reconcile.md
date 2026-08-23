# Spec: caps-reconcile

Module **`caps-reconcile`**, wave 3 in the [power map](../power-map.md). Depends on **`content-scale`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md) §11** — the caps register. Where this spec and
> the SSOT disagree, **the SSOT wins**.

**Status:** Draft — pending owner review. No build authorized.

---

## 1. Objective

Remove the ceilings that wall endless grind, and make the ones that stay say why.

Endless grind is an owner decision and the SSOT other systems reconcile *to* (SSOT §11). This module
is that reconciliation in code. It touches seven sites across five files, and it **writes no new
formula** — every change deletes a ceiling or converts a silent clamp into a loud throw. Deleting a
ceiling of course changes output past that ceiling; that is the point. What it does not do is invent
replacement maths, which is §2.3's boundary.

**Why it sits after `content-scale`.** Before magnitudes scale, none of these ceilings is reachable
and the change is unmotivated. After, they are the first thing a deep run hits.

---

## 2. Design

### 2.1 The three magnitude ceilings — derive and throw

| Site | Today | After |
|---|---|---|
| `ShieldMath.MaxInput` | `1_000_000_000`, `Math.Clamp` | derived bound, **throws** |
| `ResourceDeltaMath.AmountCap` | `1_000_000_000L`, clamp | derived bound, **throws** |
| `RpgStore.MaxSoulAward` | `1_000_000_000`, throws in `AwardSouls`, **clamps** in `RpgStore.Expeditions.cs:301` | derived bound, throws on **both** paths |

**Derived from what — and they are not all the same kind of bound** (audit F12).

| Bound | Derive from | Shape |
|---|---|---|
| `ShieldMath.MaxInput` | the widest intermediate of **one** computation | a constant |
| `ResourceDeltaMath.AmountCap` | same | a constant |
| **`MaxSoulAward`** | **balance headroom — `int64Max − currentBalance`** | **dynamic, checked per award** |

The soul bound's own comment states the real constraint: the danger is *accumulated* SQLite integer
addition, not one award. A constant derived from the award path would sit near `int64` — technically
correct and operationally useless, because what overflows is `balance + award` after ten thousand
awards. Checking live headroom is both the correct bound and a real guarantee rather than a guess
about how rich a player gets.

**A derived bound may read other caps** (audit F13). `ShieldMath`'s intermediate involves penetration,
itself bounded by `ShieldPolicy.PenCapKPm`. Each derived bound therefore **declares which constants it
reads**, and a test asserts that dependency graph is acyclic — one line of obligation against a class
of bug that is invisible later.

For the two constants: computed from the widest intermediate the path performs. **The three actual
numbers are not in this spec** — deriving them means reading each path's arithmetic, which is
implementation work, and inventing them here would reproduce the `1e9` mistake in a new font. What is
specified is the *property* and the test that checks it — the same obligation
`PowerLadder.Value` already has (`spec-power-ladder.md` §2.5). `MaxSoulAward`'s own comment already
states the real constraint: *"keeps SQLite integer addition far from 64-bit overflow, which silently
degrades to REAL and permanently corrupts the snapshot."* That is a genuine reason for a bound; `1e9`
is not the number it implies.

**Why throw rather than clamp.** A clamp turns *"your gear stopped mattering past world 40"* into a
bug with no symptom — no log, no exception, no failing test, just a curve that quietly goes flat. A
throw at a bound nobody should reach is a loud, testable assertion. The two paths that already throw
are the model; the three that clamp are the defect.

### 2.2 The narrowing casts — SSOT §11.2a

| Site | Code |
|---|---|
| `EffectBag.cs:707` | `Damage = (int)Math.Min(int.MaxValue, Math.Abs(n.Amount))` |
| `EventDrain.cs:458` | `Damage = (int)Math.Clamp(rec.Amount, int.MinValue, int.MaxValue)` |
| `EventDrain.cs:475` | same |

A `long` amount narrowed to an `int` field, silently pinned at 2.147e9. These are **the same defect
the overflow audit sees as A3** — a magnitude in an `int`. Fixing them is widening the field, which
belongs to `power-plan.md` P0.4.

> **This module does not fix them; it asserts they are fixed.** Duplicating the widening in both
> places is how a half-migration ships.
>
> **Correction (self-audit, 2026-08-23):** a first draft called this a tripwire that *"fails while
> `Damage` is still `int`, forcing P0.4 and this module to agree."* It cannot. P0.4 is **Phase 0** and
> this module is **wave 3** (`power-plan.md`: *"Phase 0 must land before Phase 2"*), so the widening is
> already done by the time this test first runs — it would be green from birth and force nothing.
>
> It is therefore a **regression guard**, not a forcing function: it fails if someone later narrows
> the field back. Useful, but a weaker claim, and worth stating as the weaker one.

### 2.3 The deletions

| Site | Change |
|---|---|
| `ContractPolicy.MaxSlots = 48` | **Deleted.** `AvailableSlots` drops its `Math.Min`; the arithmetic price `300·n` is the real cap (SSOT §11.1a) |
| `SoulEarnPolicy.KillCapPerMatch = 50` | **Deleted** + comment pointing at SSOT §11.7 |
| `PatronPolicy.KillSoulCap = 50` | **Deleted** + comment |
| **`SoulEarnPolicy.VictoryFullPerDay = 3`** | **Deleted** (audit F11). Victory pays +100 full for three wins a day, then halved — a wall-clock throttle in a single-player game. **Three cap sweeps missed it** because it is named for a threshold, not a ceiling, and refuses nothing |

**Not here: the PvZ item drop caps (2/run, 12/day).** An earlier draft listed them as a deletion.
They are **not in code** — `ssot-generation.md:827` specifies them and item drop generation is
unbuilt (audit F10). They join enhancement and rarity promotion as a **design reconciliation on an
unbuilt feature**: correct the spec now, before it ships, at no migration cost.

**The replacement formula lands with the deletion, in one commit** (SSOT §11.7a):
`soulsPerKill = KillDelta × contentScale(Θ_enemy)`, and victory/defeat likewise. Constants unchanged,
so it is a no-op at Θ=20 and scales with cost thereafter.

> **Resolved 2026-08-23 — the module does not block.** An earlier draft blocked this on the economy
> stream, reasoning that deleting the caps without a replacement formula reproduces the `+2`/kill
> incident. The reasoning was right; the conclusion was lazy. **The formula is now specified**
> (SSOT §11.7a) and it is the smallest possible change: today's constants, multiplied by
> `contentScale`.
>
> `contentScale(20) = 1.000`, so **the deletion plus the formula is byte-identical at the calibration
> point.** There is no inflation window, because there is no interval where a cap is gone and value
> scaling is absent — they land in the same commit. The economy stream owns the constants and they are
> unchanged, so nothing is owed before this proceeds.

### 2.4 Documenting what stays

SSOT §11.3–11.10 exempts 41 caps. **The audit cannot re-derive those judgements**, so each exempt cap
gets a one-line comment naming its class — `// structural: ring buffer depth, not a balance number`.

That is T2 from [tunables-ssot.md](../tunables-ssot.md) applied to the register's output, and it is
what stops the next sweep re-triaging all 41 from scratch.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Server.Tests
python scripts\audit-magic-numbers.py --domain contracts
```

---

## 4. Structure

```
src/FusionRpg.Core/Combat/Shield/ShieldMath.cs         (edit — derived bound, throw)
src/FusionRpg.Core/Effects/ResourceDeltaMath.cs        (edit — derived bound, throw)
src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs            (edit — derived bound)
src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs      (edit — clamp -> throw, one policy)
src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs  (edit — MaxSlots deleted)
src/FusionRpg.Core/Demons/SoulEarnPolicy.cs            (edit — cap deleted, owner comment)
src/FusionRpg.Core/Demons/Patron/PatronPolicy.cs       (edit — cap deleted)
docs/architecture/item/ssot-generation.md              (edit — 4.6 PvZ drop caps, unbuilt: doc only)
tests/FusionRpg.Core.Tests/Power/CapsReconcileTests.cs
```

---

## 5. Testing strategy

| Case | Expect |
|---|---|
| Shield bound | a value one under throws nothing; one over throws, naming the site and the value. **Never clamps** |
| HP-delta bound | same |
| Soul bound, both paths | `AwardSouls` **and** the expedition path behave identically — one policy, not two |
| Soul bound is **dynamic** | an award legal at balance 0 is **refused** near `int64Max`; the bound tracks headroom, not a constant (F12) |
| Bound dependency graph | acyclic, asserted — a derived bound may read another cap (F13) |
| `VictoryFullPerDay` gone | the 4th victory of a day pays the same as the 1st (F11) |
| Bound is derived | each equals its path's computed overflow limit, not `1e9`. Asserted against the computation, so a future arithmetic change moves it |
| Slots past 48 | slot 49 purchasable; `NextSlotPrice(48) == 14,700`; cumulative matches the triangular series |
| Slot price is the cap | at slot 512 the price is 150,300 — asserted, because it is the argument for deleting the ceiling |
| Warden mechanic intact | binding a warden still consumes a slot and the Nth still costs more — the `empire-economy-ssot` §7 property, which depended on price and not on the ceiling |
| Kill income uncapped | 200 kills earns 200× the per-kill value, no plateau |
| **Stall-farm regression** | the 80-kill stall-defeat must **not** out-earn a fast clean win — on the **new formula**. The one test that proves this is a fix and not a removal |
| **§11.2a tripwire** | fails while `Damage` is still `int`. Forces P0.4 and this module to agree |
| Exempt caps documented | every SSOT §11.3–11.10 cap carries a class comment (T2) |

---

## 6. Boundaries

**Always** — derive a bound from its path's arithmetic · throw, never clamp · one policy per ceiling
across all its call sites · comment every cap that stays.

**Ask first** — deleting a cap not in SSOT §11.1's list · changing an earn formula (that is the
economy stream's) · relaxing a structural or ratio cap from §11.3–11.6.

**Never** — replace a clamp with a bigger clamp · ship the soul-cap deletion in a different commit
from the scaling formula (§2.3) · duplicate the `int`→`long` widening that P0.4 owns.

---

## 7. Success criteria

1. Three magnitude bounds derived, not literal; all throw; the soul path has one policy.
2. `MaxSlots` gone; slot 512 purchasable at 150,300; warden property asserted.
3. Kill income uncapped **and** the stall-farm regression green on the new formula.
4. §11.2a tripwire red before P0.4, green after.
5. All 41 exempt caps carry a class comment.
6. Suites green. **Goldens: expected to move** on the soul path — attributed per change, like `power-dial`.

---

## 8. Open

**None.** The sequencing question — whether to block on the economy stream — dissolved once the
replacement formula was actually specified (SSOT §11.7a). It is today's constants times
`contentScale`, byte-identical at the calibration point, so cap-deletion and value-scaling land in
the same commit with no window between them and no constant for another stream to choose.
