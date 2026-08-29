# Spec — `cap-consolidation`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `stat-taxonomy` · **Blocks:** `catalog-extension`
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Give a channel cap exactly one home, before 157 new channels arrive with caps of their own.**

Today it has three, and one of them is a live bug.

### 1.1 The bug, found 2026-08-24 during the spec audit

`status.resist.{dot|cc|contagion}` is capped at `0.95` **twice**:

| | Where | When | Source of the number |
|---|---|---|---|
| 1 | [DerivedStatRegistry.cs:46-48, 90](../../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs) → applied by [DerivedComposer.cs:72](../../../src/FusionRpg.Core/Stats/Derived/DerivedComposer.cs) `Math.Min(value, def.Cap.Value)` | **compose** | **hardcoded literal `0.95`** |
| 2 | [ResistanceEvaluator.cs:207-208](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs) `Math.Min(…, StatusPolicy.CategoryResistCap)` | **apply** | `data/tuning/status.v1.json` |

The composer clamps **first**, so the effective cap is `min(0.95 hardcoded, tunable)`.

> **Publishing `status.v2.json` with `categoryResistCap: 0.99` changes nothing.** The value was already
> clamped to `0.95` before the evaluator ever read it. Lowering the tunable works; **raising it is a
> silent no-op.**

A balance pass would edit that key, observe no change, and have no way to see why — the exact failure
[RpgStore.ChannelPolicy.cs](../../../src/FusionRpg.Data/Sqlite/RpgStore.ChannelPolicy.cs)'s own comment
says *"this whole program exists to refuse."*

**The magic-number audit does not catch it**, because both sites are individually defensible: one is a
registry default, the other reads a named tunable. Only the *pair* is wrong. Recorded so the audit's
clean run is not read as proof this class of defect is absent.

### 1.2 The third home, and the trap

`effect_channel_policy` carries `default_value`, `cap_milli` and `compose_kind` —
[ChannelPolicyTable.cs:11-20](../../../src/FusionRpg.Core/Stats/ChannelPolicyTable.cs) is blunt:
*"none of which any code anywhere reads, for any channel, primary or derived"*, and the table
*"is scoped to `StatChannels.All` … and cannot even name a derived resist channel."*

Only `direction` is live. But the column names are exactly what a derived cap needs, and the table
**joins the content hash at registry v4**, so it reads as authoritative. Adding 157 capped channels
next to it makes it the obvious-but-wrong destination.

---

## 2. The decision — owner, 2026-08-24

> **A channel cap is a *tunable*, not content.** It is a number a balance pass changes, which is
> [tunables-ssot.md](../tunables-ssot.md) §1's own test. **One home: `data/tuning`.**

Three consequences, and all three land together:

1. **The registry reads tuning; the hardcoded `0.95` dies.** `DerivedStatRegistry` takes a loaded
   tuning object — the shipped Core-stays-DB-free pattern from tunables-ssot §7.2 and the exact shape
   `StatusPolicy => Tuning` already uses.
2. **`ResistanceEvaluator`'s second clamp is deleted as redundant.** One enforcement point, at compose.
3. **The three dead columns are retired** from `effect_channel_policy`. Removing the trap beats
   signposting it. `direction` — the one live column — stays.

### 2.1 Why not the table

`DerivedStatRegistry` is constructed in **Core**, which is DB-free by design. A table could only reach
it through a host-injected static (`ChannelPolicyTable.Use`'s pattern) — workable, but it would mean a
cap has a seed file, an import path, a DB row, a host load, *and* a tuning file that also exists.
Retiring the columns is less machinery and one fewer place to look.

---

## 3. What lands

| Path | Change |
|---|---|
| `data/tuning/derived-stats.v1.json` | **new** — caps and defaults per channel family, with units in the key names (T6) |
| `src/FusionRpg.Core/Stats/Derived/DerivedStatTuning.cs` | **new** — pure parser, no I/O (tunables-ssot §7.2) |
| `src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs` | `CreateDefault()` takes tuning; **literals `0.95` at `:46-48` and `:90` deleted** |
| `src/FusionRpg.Core/Status/ResistanceEvaluator.cs` | second clamp at `:207-208` deleted |
| `src/FusionRpg.Core/Status/StatusPolicy.cs` | `CategoryResistCap` **moves** to the derived tuning file — one key, not two |
| `src/FusionRpg.Data/Sqlite/RpgStore.ChannelPolicy.cs` | drop `default_value`, `cap_milli`, `compose_kind` |
| `src/FusionRpg.Core/Effects/Atoms/ContentHashRegistry.cs` | table shape changes → **registry version bump** |
| Injector + Server composition roots | load and inject the tuning object |

### 3.1 The registry version bump is real and must be deliberate

`effect_channel_policy` joins the content hash at **v4**. Changing its columns changes the hashed
shape, so the registry version moves and every content hash is restamped.

**That is not a golden move and must not be confused for one.** No gameplay number changes; the hash
covers a table *shape*. The two get asserted separately (§5), because a session that sees "hashes
changed" and "goldens clean" in the same commit will otherwise assume one of them is wrong.

---

## 4. Ordering — this module blocks `catalog-extension`

`catalog-extension` registers 157 channels, several with bounded-ratio caps. **Registering them into a
two-home system doubles the defect** — 157 more caps that a tunable cannot raise.

So the order is `stat-taxonomy` → **`cap-consolidation`** → `catalog-extension`, and the map's build
graph moves accordingly. This is the same rule `catalog-extension` §2.2 already follows for R1: fix the
thing the new rows would multiply, *before* adding the rows.

---

## 5. Testing strategy

| Test | Asserts |
|---|---|
| **`RaisingTheCapActuallyRaisesIt`** | Publish a tuning object with `categoryResistCap: 0.99`; a defender stacking resist now reaches 0.99. **This test fails on `main` today** — write it first and watch it fail, or the fix is unproven |
| `LoweringStillLowers` | `0.50` clamps at 0.50 — the direction that already worked keeps working |
| `OneClampNotTwo` | An architecture test: exactly one `Math.Min` against a cap on the resist path |
| **`GoldensByteIdenticalAt095`** | With tuning at the shipped `0.95`, **every golden is unchanged**. The refactor is invisible at the current value |
| `ContentHashChangedGoldensDidNot` | §3.1 — the hash restamp and the golden set are asserted **separately**, so neither is read as evidence about the other |
| `MissingTunableRejects` | A tuning file with no cap for a capped channel **fails to load, naming the channel** — T5: never a built-in default |
| `NoDeadColumns` | `effect_channel_policy` has `channel_id` and `direction` only |
| `DirectionStillLive` | `StatChannels.IsLowerBetter` and `CostFunction`'s direction pricing unaffected |

`RaisingTheCapActuallyRaisesIt` is the module. Everything else protects it.

---

## 6. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
python scripts\audit-magic-numbers.py --summary
.\scripts\guard-dal.ps1
```

---

## 7. Boundaries

**Always** — one enforcement point per cap. Every tunable carries its unit in the key name (T6). A
missing tunable is a **load rejection naming the channel**, never a default (T5).

**Ask first** — changing any shipped cap *value*. This module moves where `0.95` lives; it does not
move the number. T7: never land a refactor and a rebalance together.

**Never** — reintroduce a cap literal in the registry. Cap a `Contest` magnitude
([spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.2). Put a derived cap in `effect_channel_policy`
(§2). Let Core read a file (tunables-ssot §7.2) — hosts load, Core takes the object.

---

## 8. Success criteria

- [ ] **`RaisingTheCapActuallyRaisesIt` green** — written failing first.
- [ ] Exactly one clamp on the resist path; both registry literals gone.
- [ ] `data/tuning/derived-stats.v1.json` is the sole home; `CategoryResistCap` no longer duplicated.
- [ ] Three dead columns retired; `direction` still live and still read.
- [ ] Registry version bumped; **content-hash change and golden-set stability asserted separately**.
- [ ] **Goldens byte-identical at `0.95`.**
- [ ] A missing tunable rejects, naming the channel.

---

## 9. Open questions

**None.** The content-vs-tuning question was decided by the owner 2026-08-24 (§2), and §1.1's bug is
verified in shipped code rather than inferred.
