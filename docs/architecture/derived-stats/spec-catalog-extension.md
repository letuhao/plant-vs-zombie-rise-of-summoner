# Spec — `catalog-extension`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `stat-taxonomy` · **Unblocks:** every remaining module
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Register the 157 approved channels and change no behaviour doing it.**

[actor-hub-ssot.md §3H](../actor-hub-ssot.md) is approved: 157 new channels, **99 → 256**. This module
lands them in the catalog, classified per `stat-taxonomy`, with every consumer still reading zero
values. Nothing computes differently at the end of this module — that is the point.

The discipline is the power program's, and it is the reason that program could attribute every moved
golden: **land the structure at a value where the change is arithmetically a no-op, then change
behaviour separately.** A new channel that no formula reads and that defaults to `0` is exactly such a
no-op. If a golden moves here, something is wrong — it is not a rebalance.

---

## 2. What lands

| From §3H | Channels | Axis |
|---|---|---|
| H.1 element combat | **112** | 16 families × (`omni` + 6 roster) |
| H.2 status potency | **16** | 4 families × (`omni`·`dot`·`cc`·`contagion`) |
| H.3 action-category | **10** | 2 families × (`attack`·`defense`·`support`·`movement`·`status`) |
| H.4 healing | **1** | flat — `heal.power` only, unpaired `Pool` (owner, 2026-08-24) |
| H.5 resource | **15** | 3 families × 5 resource ids |
| H.6 movement | **1** | `move.range` |
| H.7 progression | **2** | `xpRate`, `breakthroughSuccess` |
| | **157** | |

**Q1's reader change is `status-potency`'s, not this module's** — it owns the combine rule.
`status.resist.fire` already resolves through the open prefix
([DerivedStatRegistry.cs:88-92](../../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs)), so
Q1 adds **zero channels** and nothing lands here. Noted because an earlier draft had both modules
claiming it.

### 2.1 Three axes, three generators — never one

[actor-hub-ssot.md §3G](../actor-hub-ssot.md) rule 1: a non-element family that joins
`AllCombatChannelIds` breaks the roster assertion *and* gets swept into element expansion. Resources
are not element-typed; neither are action categories.

| Generator | Expands over | Feeds |
|---|---|---|
| `CombatChannelFamilies` | `omni` + `ElementRoster.Concrete` | 28 families → **196** |
| status-category | `omni`·`dot`·`cc`·`contagion` + open `{statusId}` | 16 |
| flat / id-keyed | resource ids, action categories, or nothing | 30 |

### 2.2 R1 lands **first**, before the code

The count moves `84 → 196` the moment the 16 families join `CombatChannelFamilies`.
[decisions.md](../decisions.md)'s *Element Hub SSOT* row still says **"84 combat derived channels"**.

> **Task order is not incidental: restate the decisions row before the family list grows.** Doc ahead
> of code, so no window exists where a shipped lock contradicts shipped code.

R1's agreed form: replace the literal with *"families × roster, generated — the count is derived, not
fixed"*, which is what the lock always meant and never has to be amended again.
`element-families` owns the [element-hub-ssot.md](../element-hub-ssot.md) §6 semantics; this module
owns only the registration and the row that gates it.

### 2.3 Two classes per channel, and one of them is not ours

Per [spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.5, correcting a draft that proposed a **third**
classification scheme:

- **`statClass`** — `Contest` · `Race` · `Pool` · `Feeder`. Ours. Answers *does it need a counterpart?*
- **`unitClass`** — the **nine-class ledger** in
  [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3, each class already
  verified against its consumer in `src/` and already bound in the web contract. Answers *what
  arithmetic is it, and how does it render?* **Not this program's to redefine.**

Orthogonal, both required, neither inferred from the other.

**§8's rejection rule binds here and must not be worked around.** That ledger refuses a class to any
channel whose consumer cannot be named — and **none of the 157 has a consumer at registration time**,
by design. They carry `unitClass: null` until the module wiring their reader assigns one. That is the
sheet's **`no-producer`** state, which already has copy for it. **Never invent a placeholder
`unitClass` to get past the rejection** — T5 forbids exactly that.

- **`DerivedStatDef` keeps `double`.** §10.7 of [ssot-power-scale.md](../power/ssot-power-scale.md)
  decided it: `Increased`/`More` are ratios, and integer composition would be wrong.
- **`GameUnits` / `GameUnitsPerSecond` materialize as `long` where they leave composition** —
  `EntityStatWriter`, `DamagePacket`, `BattleRuleset`. Where invariant 13 actually binds.
- **Every cap registers in §11.6** with its PS-8 class named. No bare `0.95`.

### 2.4 The count tests are **already** formula-based — only a canary moves

**Corrected 2026-08-24.** An earlier draft said `DerivedStatRegistryTests.cs:22` "asserts a literal" and
should be rewritten as a formula. Reading the whole test body falsified that: it **already** computes

```csharp
var expected = DerivedStatChannels.CombatChannelFamilies.Count * (ElementRoster.Concrete.Count + 1);
Assert.Equal(84, expected);            // <- the ONLY literal: a deliberate canary
Assert.Equal(expected, DerivedStatChannels.AllCombatChannelIds.Count);
```

and a sibling in `ElementRosterDataTests` is named
**`The_channel_count_is_the_formula_not_the_literal_eighty_four`**. Someone already did this work.

So the real change is small and different:

| File | Change |
|---|---|
| `DerivedStatRegistryTests.cs:21` | canary `84` → **`196`**. It is *meant* to be a literal — it asserts what the formula currently equals, so a silent roster change is caught |
| same, test name | `Combat_channel_count_is_12_families_x_roster_plus_omni` → 28 families |
| `ElementRosterDataTests.cs` | **nothing.** Both assertions are roster-relative (`before + families.Count`) and already correct. Only the name `A_seventh_element_generates_its_twelve_channels…` reads wrong at 28 |

**Do not "fix" tests that are already right.** That draft would have had someone rewrite correct
assertions into equivalent ones and call it progress.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DerivedStat"
dotnet test tests\FusionRpg.Core.Tests          # full - goldens must not move
.\scripts\guard-stat-pairs.ps1
.\scripts\guard-power.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
python -c "import json;d=json.load(open('data/seed/derived-stats/catalog.json'));print(len(d['entries']))"
```

---

## 4. Project structure

| Path | Change |
|---|---|
| `docs/architecture/decisions.md` | **first** — R1 restatement (§2.2) |
| `src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs` | +16 entries in `CombatChannelFamilies`; new const blocks for the status-potency, action-category, healing, resource, movement and progression families |
| `src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs` | register the non-generated families; `+ resist.{element}` term; `double` fields **unchanged** |
| `data/seed/derived-stats/catalog.json` | +new families with `class`, `counterpart`, `role`; new `axes` entries for resource-id and action-category |
| `docs/architecture/power/ssot-power-scale.md` §11.6 | every new bounded-ratio cap, with its exemption comment |
| `tests/.../DerivedStatRegistryTests.cs` · `ElementRosterDataTests.cs` | assertions become derived (§2.4) |

---

## 5. Code style

New families follow the shipped generation idiom exactly — a family constant plus roster expansion,
never a hand-written channel list:

```csharp
public const string CombatParryRatePrefix = "combat.parry.rate";
public static string CombatParryRate(ElementTypeId e) => $"{CombatParryRatePrefix}.{e.ToElementId()}";
```

Non-element families are plain consts outside that generation, following the precedent
`DerivedTurnChannels` set and its comment already explains: *"speed is not an element, so these must
stay out of the generated combat roster."*

---

## 6. Testing strategy

### 6.1 Structural

| Test | Asserts |
|---|---|
| `CatalogResolves256` | Every named channel resolves; count is **derived**, not literal (§2.4) |
| `NonElementFamiliesStayOutOfCombatRoster` | No `resource.*`, `skill.*`, `progression.*`, `move.*` in `AllCombatChannelIds` — §3G rule 1, and the failure that rule exists to prevent |
| `SeedCatalogMatchesCode` | `catalog.json` expands to exactly what `CreateDefault()` registers. **The mirror must be proven, not assumed** — a drifting mirror is worse than none |
| `EveryNewChannelIsClassified` | `stat-taxonomy`'s guard passes over the enlarged catalog |
| `StatusResistElementTermResolves` | `status.resist.fire` resolves **and is now read** by the combine rule |

### 6.2 Behaviour — the acceptance test for the whole module

| Test | Asserts |
|---|---|
| **`git status tests/` clean** | **Zero goldens moved.** Registration alone changes nothing |
| `MatchedActorsUnchanged` | Two stub actors produce byte-identical resolve before and after |
| `UnknownChannelStillRejects` | The reject rule survives a 2.6× larger catalog |

### 6.3 Performance — one inherited defect, made 2.6× worse here

**`PvzStatsSheetComposer` rebuilds the entire registry per call.**
[PvzStatsSheetComposer.cs:39](../../../src/FusionRpg.Core/Stats/PvzStatsSheetComposer.cs) calls
`DerivedStatRegistry.CreateDefault()` *inside* `TryCanonicalizeOrDerivedChannel` — a fresh dictionary
plus one `DerivedStatDef` per channel, on a path called **per modifier row** — then signals the unknown
case with a **thrown exception** caught one line later, which is the normal path for any primary
channel.

Today that is 99 allocations per call; after this module it is **256**.

**Not introduced here, but made to matter here**, and the repo already fixed this exact defect once:
E25 rewrote `StatusStatPayload.IsKnownChannel`, which *"used to `.Contains` a freshly allocated
84-element list on every channel it parsed."* `PvzStatsSheetComposer` is the second instance, missed by
that sweep.

**Use E25's own idiom, not a bare `static readonly`** — cache by reference identity against
`ElementTable.Current`, rebuilding only when the roster object changes. A plain static would break
`ElementTable.UseScoped`, which tests rely on to swap rosters beside one another.

| Test | Asserts |
|---|---|
| `SheetComposerAllocatesOnce` | N validations build the registry once, not N times |
| `ScopedRosterStillHonoured` | `UseScoped` swaps the cache — the failure a bare static would introduce |
| `ComposerAllocationAt196` | `BattleStatComposer` re-measured at the new roster size. **196 is 2.3× the 84 the E25 cache was measured against** — re-run the test rather than assume it still absorbs it |

---

## 7. Boundaries

**Always**
- Restate `decisions.md` (R1) **before** growing `CombatChannelFamilies` (§2.2).
- Generate element families; never hand-list an expansion.
- Give every `Cap` a §11.6 row and a comment.

**Ask first**
- Any channel not in the approved §3H list — approval covered 157, not 157-ish.
- Wiring a *reader* for a new channel. That is a later module's job and it is where goldens legitimately move.

**Never**
- Let a non-element family reach `AllCombatChannelIds`.
- Widen `DerivedStatDef`'s composition `double` (§2.3).
- Ship a balance value for a new channel — T7: extract with values unchanged, tune separately. **Every new channel defaults to `0`.**
- Re-bless a golden in this module. A moved golden here is a defect, not a rebalance.

---

## 8. Success criteria

- [ ] **256** channels resolve; both count assertions derived from `families × roster`.
- [ ] `decisions.md` restated **before** the family list grew — provable from task order.
- [ ] `catalog.json` expands to exactly `CreateDefault()`, asserted by a test.
- [ ] No non-element family in `AllCombatChannelIds`.
- [ ] `status.resist.{element}` read by the combine rule; **zero new channels** for Q1.
- [ ] Every new `Cap` in §11.6 with an exemption comment; `audit-overflow.py` and `audit-magic-numbers.py` clean.
- [ ] **`git status tests/` clean.** No golden moved.
- [ ] Composer allocation re-measured at 196, not assumed from the 84 baseline.

---

## 9. Open questions

**One, and it is a measurement, not a decision.** §6.3 — whether the `AllCombatChannelIds` reference
cache still absorbs a 2.3× larger roster at the per-actor call site. Answerable by running the
existing allocation test at 196; it needs no owner input, only a number. Recorded rather than assumed,
because "the cache handles it" is exactly the kind of claim [DESIGN-GATE.md](../../DESIGN-GATE.md)
evidence rule 4 says to test before declaring.
