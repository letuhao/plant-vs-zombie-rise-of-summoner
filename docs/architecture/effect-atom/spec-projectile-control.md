# Spec: projectile-control (E37)

Module **E37** in the [atom effect map](../effect-atom-map.md) §13 (Wave 8). Depends on **E28**
(`param-parity`). Ideal: [effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 3.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit.
> Where this spec and the definitions disagree, **the definitions win**.

> **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the coefficient data path.**
> Coefficients do **not** live in `data/tuning/effects.v1.json`: that file has no coefficient section
> (its keys are `matchupRead` and `damageFxFloater`), and `CoefficientTable` never reads `data/tuning/`
> at all — the only reader is `RpgStore.GetPowerTables` over the **content-hashed** `power_coefficient`
> table (`RpgStore.Power.cs:61-72`; hash registry V3 at `ContentHashRegistry.cs:148-160`). **The path is
> a seed kind, `data/seed/power/coefficients.v1.json`**, decided in full at
> [`spec-power-sweep.md`](spec-power-sweep.md) §4.1. `CoefficientTable.Authored()` stays the
> no-database fallback and this module does not edit it. **This spec's coefficient row lands in that
> seed file.**


## Objective

Make a projectile's damage and behaviour authorable. Two halves, and they are different problems:
**a bullet an atom spawns** — `spawn.entity{kind:bullet}` can create one and cannot say how hard it
hits — and **a bullet the game fires**, where `Bullet.InitData` is already patched but only cheat
state can reach it. E37 owns both, and it is the module that closes the spawn-prices-at-zero defect,
because the price of a spawn is the body it makes and a bullet's body is its damage.

## 1. What exists today

### Built

| Fact | Where |
|---|---|
| `CreateBullet.Instance.SetBullet(x, y, row, type, moveWay, false)` — the spawn factory | `src/FusionRpg.Injector/DebugActions.cs:147` |
| `SpawnBullet` already reads `damage`, `y`, `moveWay`, `fromType` from its JSON payload | `DebugActions.cs:143-154` |
| `Bullet.InitData` postfix writes `Damage` (set and percent), swaps `theBulletType`, forces `MoveWay = Track` | `src/FusionRpg.Injector/CheatPrefixes.cs:68-91` |
| Bullet spawn is captured as an event (`bullet.init`, mapped to `OnSpawn`) | `src/FusionRpg.Injector/Effects/EventDrainHost.cs:120-128` |
| A spawn is priced by the body it makes, at depth 1 | `src/FusionRpg.Core/Effects/Atoms/Power/CostFunction.cs:182-204` |

### Wiring gap — the write path exists and no atom can reach it

| Gap | Where |
|---|---|
| `ExecSpawnEntity`'s `bullet` arm forwards only `typeId`/`bulletType`/`row`/`x`. `damage`, `y`, `moveWay`, `fromType` are dropped | `src/FusionRpg.Injector/Effects/InjectorEffectActionSink.cs:372-383` |
| `spawn.entity` declares `atk` with `NotImplementedNote: "the sink drops atk for every spawn kind"`, so it is **refused at load** | `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:297-298` |
| `hp`/`maxHp` are `HonouredOnlyWhen: "kind=zombie"` | `AtomKindRegistry.cs:294-295` |
| Consequence, exactly: `SpawnBody` returns `PowerVector.Zero` when `hp == 0 && atk == 0`, and a plant or bullet spawn can supply neither — **every non-zombie spawn prices at zero** | `CostFunction.cs:193` |
| The `BulletInitCheat` postfix reads `CheatState` only. No grant, no owner key, no atom | `CheatPrefixes.cs:73-88` |

### Real gap

| Gap | Note |
|---|---|
| No kind addresses a bullet the **game** fires. `spawn.entity` only creates one | New kind `bullet.modify`, §2b |
| Nothing prices a bullet's damage as offense | `CoefficientTable.Authored()` has no `spawn.entity` channel row; the kind falls back to `("spawn.entity", "", 1000, 1)` at `CoefficientTable.cs:141` |

## 2. The contract

### 2a. `spawn.entity`, bullet arm — params honoured, not dropped

| Param | Type | Honoured when | Unity target |
|---|---|---|---|
| `atk` | `Value` | **all kinds** (E28 removes the `NotImplementedNote`; E37 supplies the bullet arm) | `Bullet.Damage` when `kind=bullet` |
| `y` | `Value` | `kind=bullet` | `SetBullet`'s `y` argument |
| `moveWay` | `String` | `kind=bullet` | `BulletMoveWay`, closed set — **membership is UNVERIFIED and comes from the sweep below**, not from the guess `right` \| `left` \| `up` \| `down` \| `track` this row used to state as fact |
| `fromType` | `Int` | `kind=bullet` | `Bullet.fromType` (`PlantType`) |

`atk`, not a second `damage` key: the pricing path already reads `atk` (`CostFunction.cs:191`), and a
parallel name would be a magnitude the cost function cannot see. `SpawnBullet` reads `damage` from its
payload today, so the **sink** translates `atk` → `damage` at the payload boundary
(`InjectorEffectActionSink.cs:372-383`) and `DebugActions` is unchanged.

`moveWay` is a **string** in the atom and an int in the payload. The enum belongs to the game; an
author writing `2` and getting an undocumented direction is the `boxType` defect
(`AtomKindRegistry.cs:334-336`) repeated, so the closed string set above is the vocabulary and E29's
value guard refuses everything else.

> **⛔ CORRECTED 2026-09-03 — the five-member set was asserted with no assembly sweep.** Only **two**
> `BulletMoveWay` members appear anywhere in `src/`: `Track` (`CheatPrefixes.cs:87`) and `MoveRight`
> (`DebugActions.cs:146`, the default). Nothing in this repo shows the enum has members for left, up
> or down, or what any of them are called — `right`/`left`/`up`/`down`/`track` was written from the
> shape the names suggest, not from the game.
>
> **Sweep the assembly first, and wire only what it found.** That is exactly the discipline
> `spec-plant-side-status.md` (E39) mandates for plant-side CC, for the same reason and citing the
> same precedent: **E17 shipped three statuses declared against Unity methods that did not exist**
> (`StatusCatalogBootstrap.cs:36-50` records the correction — *"an assembly-metadata sweep of
> Assembly-CSharp found SetEmbered / SetJalaed / SetKelped but no SetCharm\*"*), and every application
> queued a plan item that matched no case and did nothing. `spec-spawn-non-grid.md` (E40) holds the
> same line for `CreateItem.SetCoin`, refusing to claim the arm before proving it.
>
> **So:** E37's first task is an `Assembly-CSharp` metadata sweep of `BulletMoveWay`. The closed
> string set is whatever that sweep returns, one string per real member, written into
> `docs/research/effect-runtime/03-status-and-spawn-surface.md` beside E39's. A member the sweep does
> not find is **not** in the vocabulary — never declared and left inert.

### 2b. New kind — `bullet.modify`

**E37 adds one kind and no attach point.** `KindCount` (`AtomKindRegistry.cs:18`) gains **+1**;
`AttachPointCount` (`:16`) is not changed **by this module**.

> **⛔ CORRECTED 2026-09-03 — this section contradicted itself in three lines.** It called
> `bullet.modify` *"kind #13"* while §3 said *"No new attach point. **Five today**"*, and criterion 4
> asserted *"`KindCount` reads 13"*. `#13` is only true if no sibling lands first, and *"five today"*
> is a statement about the baseline that reads as a claim about the end state. **Every count here is
> now a delta.** The Wave 8 end state is stated once, in `spec-match-modify.md` §2.1:
> `AttachPointCount = 7`, `KindCount = 16`, from `5` and `12` today.

| | |
|---|---|
| Attach point | `Board` — **E37 adds none.** A bullet is a board entity; E35 (`Match`) and E41 (`Ui`) own the two points Wave 8 adds |
| Params | `op` (`String`, required: `set` \| `add` \| `scale`) · `amount` (`Value`, required) · `bulletType` (`Int`, optional swap) · `moveWay` (`String`, optional — from the swept set, §2a) |
| Triggers | **none** — `AtomTriggers.None`, the permanent-modifier shape `stat.derived` uses (`AtomKindRegistry.cs:161`). The grant's presence is the effect. **This requires amending a shipped test — see §2b.1** |
| Runtime matrix | `(Lawn: Full, Battle: None, Sim: None)`. Battle has no projectile — record it `pending`, never `never` (E1's living-table rule) |
| Power categories | `Offense` |
| Opcode | `EffectActions.BulletModify`, plus an `AtomCompiler.OpcodeOf` arm — the kind ↔ opcode bijection is asserted, so a kind with no opcode fails it. **And it must reach `/effects/contract`'s `actions` array — see §2b.2** |
| Executor | A resolved read inside the existing `Bullet.InitData` postfix, keyed by the firing plant's owner key. **Cheat state is applied last**, so `D-DMG-SET` still wins |

### 2b.1. `AtomTriggers.None` cannot pass `AtomKindRegistryTests` as it stands

> **⛔ ADDED 2026-09-03 — the spec chose a shape a shipped test forbids and never named the test.**

`Every_kind_declares_a_runtime_a_trigger_and_a_power_category`
(`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:36-73`) walks every registered kind and
asserts:

```csharp
var permanentModifiers = new[] { "stat.derived" };   // :53
…
if (permanentModifiers.Contains(kind.KindId))        // :66
    Assert.Empty(kind.Triggers);                     // :67
else
    Assert.True(kind.Triggers.Count > 0, $"{kind.KindId} allows no trigger");   // :68-69
```

An empty trigger list is legal for **exactly one** kind id. `bullet.modify` with `AtomTriggers.None`
goes red at `:69` — *"bullet.modify allows no trigger"* — the moment it registers.

**The amendment, stated exactly:** `permanentModifiers` becomes `{ "stat.derived", "bullet.modify" }`,
with a comment beside it saying why the second is there — a `bullet.modify` grant's presence is the
effect, read at `Bullet.InitData`, so there is no event for it to carry. That is a deliberate edit to a
guard, in the same commit as the kind, said out loud in the commit message; the guard exists so that
growing the exempt set is noticed, and a silent edit is what would defeat it.

**Not an alternative:** giving `bullet.modify` a trigger to keep the test green. The kind has no event
to fire on — the seam is a Unity postfix, not an atom event — and a declared trigger nothing raises is
the `status.expose.*` defect (`spec-atom-kind-registry.md`, the code-or-data rule).

### 2b.2. The published `actions` array must grow too

`/effects/contract` publishes **ten** of `EffectActions`' twelve constants under `frozen = true`
(`DebugEndpoints.cs:388-394`; the constants at `EffectDtos.cs:22-44`) — `GrantShield` and
`ModifyDerivedStat` are missing. **E33 repairs those two** (`spec-activation-edge.md` §2.1a); E37 owns
adding `BulletModify` beside them rather than making it three. Criterion 4 asserts it by count.

`op: scale` is per-mille (`amount` 1500 means ×1.5), like every other ratio in this layer. `set` and
`add` are whole damage units.

### 2c. Pricing

- A `spawn.entity` bullet with `atk` prices through `SpawnBody` unchanged. The fix is that `atk` now
  reaches it, so `CostFunction.cs:193` stops short-circuiting to zero.
- `bullet.modify` needs its own row in `CoefficientTable.Authored()`. The reference scale is the raw
  damage unit, matching `("stat.modify", "atk", 1000, 2)` at `CoefficientTable.cs:127`.
- `op: scale` prices off the **mean** of the authored range like every other magnitude
  (`CostFunction.MeanMagnitude`, `CostFunction.cs:212-230`), which under-prices a multiplier over a
  large base. Say so in the row's comment; do not invent a second pricing path here.

## 3. What it must NOT do

- **No new attach point.** `AttachPointCount` (`AtomKindRegistry.cs:16`) is not changed **by this
  module**. It reads 5 today (`AtomKind.cs:7-14`) and Wave 8 takes it to 7 — E35 and E41 own both
  additions. E37 adds a kind on the existing `Board` point and nothing else.
- **No opcode without the published list growing with it.** §2b.2. `BulletModify` enters
  `/effects/contract`'s `actions` array in the same change as the constant.
- **No `moveWay` member the assembly sweep did not find.** §2a. A declared direction with no enum
  member behind it is E17's exact defect, and it fails silently at execute.
- **No new rejection reason code.** The list is closed at 33 (`definitions.md` §10). A bullet whose
  target is gone is a runtime `return false`, not a load-time code.
- **`long` for every magnitude, never `float`.** `Bullet.Damage` is a Unity `int`, so clamp at the
  **write boundary** the way `EntityStatWriter` already does (`ZombieCombatFields.ClampToInt32`,
  `EntityStatWriter.cs:50`) — never by narrowing mid-arithmetic.
- **Widen before multiplying** (`(long)a * b`, never `(long)(a * b)`), and **divide by 1000 exactly
  once, last**, for `scale`. **Overflow throws** — no `unchecked` on the damage path.
- **No hard ceiling on damage.** Any bound is an overflow guard derived from the `int` field and it
  **throws**. The `Math.Max(1, …)` floor already in `CheatPrefixes.cs:81` is a **structural** minimum
  (a zero-damage bullet is inert, not balanced) and must carry a comment saying so.
- **No literal on the balance surface.** Coefficients live in `data/seed/power/coefficients.v1.json`
  (see the decision below); other defaults live in `data/tuning/effects.v1.json`;
  `CoefficientTable.Authored()` is the code fallback a host with no
  database runs on, not a place to park a tuned number.
- **Do not rewrite `BulletInitCheat`.** Add alongside it, cheat last. Routing the operator path through
  atoms would make an operator knob depend on content loading.
- **Do not touch `TakeDamage`.** Per-hit resolves in that prefix are the pattern the 2026-08 perf audit
  blamed for combat lag. `InitData` fires once per bullet and is the correct seam.

## 4. Testing strategy

| Case | Expect |
|---|---|
| `spawn.entity{kind:bullet, atk:{min:500,max:500}}` prices | non-zero — `CostFunction.cs:193` is not reached |
| **Planted violation:** delete the `atk` forwarding from the bullet arm | the payload-shape test fails. It must not pass because `DebugActions` defaults `damage` |
| **Planted violation:** re-add `NotImplementedNote` to `atk` | the load test fails with `ParamNotImplemented`, loudly |
| `moveWay: "spiral"` | `BadParamValue` at load, never an unmatched cast at execute |
| Every string in the `moveWay` set maps to a real `BulletMoveWay` member | pass — the set is what the assembly sweep found (§2a), asserted against the enum, not against this spec's prose |
| `bullet.modify` carrying any trigger | `TriggerNotAllowed` — it is a permanent modifier |
| `Every_kind_declares_a_runtime_a_trigger_and_a_power_category` (`AtomKindRegistryTests.cs:36-73`) | **green with `permanentModifiers` amended to `{ "stat.derived", "bullet.modify" }`** (§2b.1). Unamended it goes red at `:69`; the amendment is deliberate and named in the commit |
| `/effects/contract`'s `actions` array | contains `BulletModify`; count asserted (§2b.2) |
| `bullet.modify{op:scale, amount:2000}` over a damage near `int.MaxValue` | throws; never wraps, never clamps silently |
| `bullet.modify` bound in Battle | `RuntimeUnsupported` at bind |
| Cheat `D-DMG-SET` set while a `bullet.modify` grant is live | cheat wins; an ordering test asserts it |

**The injector is not built by CI** — `.github/workflows/ci.yml:75-103` runs ten test projects and no
injector build, because it needs the game assemblies. So every assertion that can live in Core does:
plan-item shape, pricing, validation. The sink's forwarding is covered by a text guard in the
`scripts\guard-*.ps1` family, the same technique `guard-single-writer.ps1` uses and that
`EntityStatWriter.cs:109-110` warns about. Live confirmation is an owner-run lawn proof.

## 5. Acceptance criteria

0. The `BulletMoveWay` assembly sweep is done and recorded before any `moveWay` value ships; the closed
   string set is exactly what it found (§2a).
1. `spawn.entity{kind:bullet}` with `atk` produces a bullet whose `Damage` is that value, proven by the
   `debug.spawn.bullet` emit (`DebugActions.cs:156-164`).
2. A bullet or plant spawn carrying a body prices non-zero; a spawn with neither `hp` nor `atk` still
   prices zero and reports why.
3. `y`, `moveWay` and `fromType` reach `SetBullet`; a planted deletion of any one fails a test.
4. `bullet.modify` is registered on `AttachPoint.Board`; `KindCount` is **one higher than before this
   module** and equals `AtomKindRegistry.All.Count`; `AttachPointCount` is **not changed by this
   module**; `AtomKindRegistryTests.cs:53`'s `permanentModifiers` set is amended to include
   `bullet.modify` (§2b.1); `/effects/contract`'s `actions` array contains `BulletModify`, asserted
   by count. Every one of those guard edits is deliberate and named in the commit message.
5. A `bullet.modify` grant changes the damage of a bullet the **game** fired, with no cheat key set.
6. Cheat state still wins over a `bullet.modify` grant, asserted by an ordering test.
7. `bullet.modify` has a coefficient row; no atom of this kind ever reports `unpriced`.
8. Overflow on `op: scale` throws, and no `float` appears anywhere on the damage path.

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **E28 `param-parity`** | Owns removing `atk`'s `NotImplementedNote` and the `count` loop. E37 owns the **bullet arm** and the new kind. Do not build E28's half here — map §16 seam discipline |
| **E29 `kind-value-guard`** | `moveWay`'s closed set is refused by E29's per-kind value check. If E29 has not landed, E37 ships a local check with a comment naming E29 — never a silent accept |
| **VFX program** | Bullet spawn drives `bullet.place` capture (`GameCaptureHooks.cs:359-374`), and a damage change alters what the floater shows. Sequence against any open blind-identity trial |
| **CI gates** | Three go red on this module, not one: the kind-count guard (`AtomKindRegistryTests.cs:22`), `Every_kind_declares_a_runtime_a_trigger_and_a_power_category` at `:69` (§2b.1), and `EffectCatalogExecutionParityTests`. Each needs a named change, not a rename to dodge it |
| **Count collisions across Wave 8** | E35 (`match.modify`), E36 (`wave.control`) and E41 (`ui.present`) each move `KindCount`; E35 and E41 also move `AttachPointCount`. Whichever lands last edits the guard to the combined value and says so. `spec-match-modify.md` §2.1 states the wave end state once so no module states an absolute of its own |
| **`AtomImporter` staleness** | E37 is a registry/compiler **code** change; the importer reports "nothing changed" because its hash covers seed data. State it in the rollout note |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`). Pre-existing for any content change |
