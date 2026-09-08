# Spec: spawn-non-grid (E40)

Module **E40** in the [atom effect map](../effect-atom-map.md) §13 (Wave 8). Depends on **E28**
(`param-parity`). Ideal: [effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 6.

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

Pets, buckets, coins and mowers cannot be placed by any atom. `spawn.entity` knows three kinds
(`zombie`, `plant`, `bullet`) and `grid.spawn` covers `GridItemType` only — twelve values per map §13,
a count that lives in the game assembly and is **UNVERIFIED** from this repo. Everything else the lawn
can put on the board is reachable from the cheat menu and from nothing else. E40 closes that, and it is
the module the economy and reward designs are waiting on.

## 1. What exists today

### Built — the write paths exist, one call each

| Spawnable | Call | Where |
|---|---|---|
| Pet | `MiniPet.SetPet(board, LawnCoords.CellCenter(col, row), (PetType)type)` | `src/FusionRpg.Injector/CheatActions.cs:806` |
| Grid item | `GridItem.SetGridItem(col, row, (GridItemType)type, graveType)` | `CheatActions.cs:817`, and `DebugActions.PlaceGridItem` at `DebugActions.cs:616-636` |
| Bucket | `GameAPP.itemManager.SetBucket(board, (BucketType)type, CellCenter)` | `CheatActions.cs:831` |
| Present (open) | `Present.RandomPlant()` | `CheatActions.cs:843` |
| `spawn.entity` executor, three kinds, unknown kind **throws** | `default: throw new InvalidOperationException("SpawnEntity kind " + kind)` | `src/FusionRpg.Injector/Effects/InjectorEffectActionSink.cs:384-385` |
| The throw is caught, logged and returns false — an unknown kind already fails loudly | `InjectorEffectActionSink.cs:76-87` |

### Wiring gap

| Gap | Where |
|---|---|
| `grid.spawn` is the only board-placement kind, and it is `GridItemType`-only | `AtomKindRegistry.cs:314-323` |
| `spawn.entity.kind` is a free `String` with no declared domain, so `kind: "pet"` validates at load and throws at execute | `AtomKindRegistry.cs:282` |
| Pet / bucket / present are reachable only from `CheatCommandRunner` and the cheat menu; no atom, no plan item | `CheatActions.cs:800-847` |
| `CreateItem.SetCoin(int theColumn, int theRow, int theItemType)` — the coin factory. We patch it for capture and never call it | `src/FusionRpg.Injector/GameCaptureHooks.cs:420-435` |
| `CreateMower.SetMower(MowerType mowerType, float x, int row)` — the mower factory. Same: patched for capture (`GameHooks.cs:892-900`), never called | `src/FusionRpg.Injector/GameHooks.cs:892` |

### Real gap

| Gap | Note |
|---|---|
| Nothing prices a non-body spawn | `SpawnBody` prices `hp`/`atk` (`CostFunction.cs:182-204`); a pet or mower atom carries neither, so it falls to `MeanMagnitude`'s "one reference unit" (`CostFunction.cs:228-230`) against the channel-less row `("spawn.entity", "", 1000, 1)` at `CoefficientTable.cs:141`. **A pet and a mower therefore price identically.** See §2c |
| `H-MOWER-INF` is **partial** — it blocks `Mower.Die` and does not respawn | `docs/research/cheat-menu-coverage.md:63`, `src/FusionRpg.Injector/CheatPrefixes.cs:128-131` |
| Auto-collect is a per-tick scan over `CoinSun` / `CoinMoney`, not a spawner | `CheatActions.cs:860-890` |

## 2. The contract

### 2a. Widen `spawn.entity.kind`, do not add a kind

`kind` gains a **closed domain**: `zombie` \| `plant` \| `bullet` \| `pet` \| `bucket` \| `coin` \| `mower`.

Widening rather than adding a kind, for a stated reason: FA4's opcode, plan-item shape, coefficient row
and executor switch all exist, and a new kind would need each of them again for no new semantics. The
`default:` arm already throws (`InjectorEffectActionSink.cs:384`), so a `kind` the executor does not
know has never been a silent no-op — which is what makes widening safe.

The domain becomes a **value-guarded** param (E29's per-kind check). Today `kind` is an undeclared-domain
string; after E40 an unknown one is a **load-time** refusal instead of an execute-time throw.

| `kind` | Params honoured | Executor call |
|---|---|---|
| `pet` | `typeId` (`PetType`), `row`, `col` | `MiniPet.SetPet` |
| `bucket` | `typeId` (`BucketType`), `row`, `col` | `ItemManager.SetBucket` |
| `coin` | `typeId` (item type), `row`, `col` | `CreateItem.SetCoin` — **UNVERIFIED** that it is callable outside the game's own drop flow; §3 |
| `mower` | `typeId` (`MowerType`), `row`, `x` | `CreateMower.SetMower` |

`row`/`col` reach `LawnCoords.ClampCol` / `ClampRow` on the way in, the same as
`DebugActions.PlaceGridItem` (`DebugActions.cs:618-619`). `x` is bullet-and-mower-only, matching the
existing `HonouredOnlyWhen: "kind=zombie|bullet"` shape at `AtomKindRegistry.cs:293`.

### 2b. `present` is not a spawn — scoped out with its reason

`Present.RandomPlant()` (`CheatActions.cs:843`) **opens a present that already exists**; it places
nothing. Modelling it as a spawn would name a capability the call does not have. Its correct home is a
`board.action` op alongside `freeze` / `doom` / `fireline` / `cherry` (`AtomKindRegistry.cs:304-312`),
and that is a one-row change for whoever owns board ops — not E40.

### 2c. Pricing — E40 does not invent a second key

A pet, a bucket and a mower are worth different amounts and `CostFunction`'s `(kindId, channel)` key
cannot express "price by `kind`" (`CoefficientTable.cs:47`, `:75-82`). That is the **same** limitation
map §12 records for E30 — *"owns pricing a pooled atom, which `CostFunction`'s `(kindId, channel)` key
cannot do today"*.

So: E40 prices what it can and **flags the rest rather than guessing**.

- A spawnable that carries a body (`hp`/`atk`) prices through `SpawnBody` as it does today.
- Everything else prices through the channel-less `spawn.entity` row, which means **one flat unit for
  every non-body kind**. That is recorded in the row's comment and in the acceptance criteria as a known
  under-discrimination, not hidden.
- **A per-`kind` price key is E30's to build.** E40 must not add a parallel one; two pricing keys for
  one kind is the defect this program exists to prevent.

## 3. What it must NOT do

- **No new kind and no new attach point.** `spawn.entity` widens; `KindCount`
  (`AtomKindRegistry.cs:18`) and `AttachPointCount` (`:16`) are **not changed by this module**.
  > **⛔ CORRECTED 2026-09-03 — this read *"`KindCount = 12` … `AttachPointCount = 5` … untouched"*,**
  > which states the whole wave's end state as a fact and is false the moment a sibling lands. Four
  > Wave 8 modules move those constants: E35 (`Match` + `match.modify`), E36 (`wave.control`),
  > E37 (`bullet.modify`) and E41 (`Ui` + `ui.present`). **E40's claim is about E40 only**, and the
  > wave end state is stated once in `spec-match-modify.md` §2.1: `AttachPointCount = 7`,
  > `KindCount = 16`, from `5` and `12` today.
- **Do not add a second pricing key.** §2c.
- **Do not claim the coin path before proving it.** `CreateItem.SetCoin`'s signature is known from the
  Harmony patch (`GameCaptureHooks.cs:422`), and whether calling it outside the game's own drop flow is
  safe is **UNVERIFIED**. Prove it in a live lawn run before shipping the `coin` arm; if it is not safe,
  the arm is refused at load with the reason recorded here — never shipped inert.
- **Do not touch `H-MOWER-INF`.** It is a separate, partial cheat toggle with its own probe owed.
- **Do not extend `grid.spawn`.** `graveType` is E28's row (map §12); `GridItemType` stays that kind's
  domain. Two kinds placing the same item is a seam violation.
- **`long` for any magnitude** (a coin's value, a spawn `count`) — **never `float`**; widen before
  multiplying; **divide by 1000 last, exactly once**; overflow **throws**.
- **No hard progression ceiling.** No cap on spawn `count` or on how many pets may exist. The
  `LawnCoords` row/column clamps are **structural** (a cell outside the board is not a balance question)
  and must say so in a comment; `MatchCaps` limits such as `MaxLivingBullets`
  (`DebugActions.cs:996`) are per-runtime caps and are exempt for the same reason — with the comment.
- **No number a balance pass would change, in code.** Spawn coefficients live in
  `data/seed/power/coefficients.v1.json` (see the decision below); any per-kind weight that is not a
  coefficient lives in `data/tuning/effects.v1.json`.

## 4. Testing strategy

| Case | Expect |
|---|---|
| `spawn.entity{kind:"pet", typeId:0, row:2, col:3}` | a plan item whose payload carries type, row and col; the executor calls `SetPet` once |
| **Planted violation:** drop `col` from the pet arm's payload | the payload-shape test fails — it must not pass because the executor defaults to `CheatState.SpawnCol` |
| **Planted violation:** widen the `kind` domain without adding the executor arm | the round-trip test fails: every value in the closed domain must have an arm, asserted by iterating the domain |
| `kind: "sunflower"` | `BadParamValue` at **load**, not an `InvalidOperationException` at execute |
| Row/col outside the board | clamped by `LawnCoords`, never an out-of-range write |
| A non-body spawn's price | non-zero, and a test pins the flat-unit value so the day E30 differentiates it, the change is visible |
| `count` above 1 on a pet spawn | E28's loop spawns that many; overflow on a large `count` throws |
| `present` as a `kind` | refused — it is not a spawn (§2b) |

**The injector is not built by CI** (`.github/workflows/ci.yml:75-103` runs ten test projects and no
injector build). The domain, validation, plan-item shape and pricing assert in `FusionRpg.Core.Tests`;
the four executor arms are covered by a text guard in the `scripts\guard-*.ps1` family and confirmed by
an owner-run lawn proof — one placement per kind, verified by the existing `debug.spawn.*` /
`item.drop` emits.

## 5. Acceptance criteria

1. `spawn.entity.kind` has a closed, value-guarded domain of seven, and an unknown value is refused at
   load.
2. Every value in that domain has an executor arm; a test iterates the domain rather than listing arms.
3. Pet and bucket spawn from an atom with no cheat state set, proven by their capture emits
   (`GameCaptureHooks.cs:659-673` for pet, `:789` for bucket).
4. Mower spawns from an atom, or the `mower` arm is refused with the reason recorded — never inert.
5. The `coin` arm is either live-proven or refused with its reason. §3.
6. `present` is not a `spawn.entity` kind, and the spec says where it belongs instead.
7. Non-body spawns price non-zero and the flat-unit behaviour is pinned by a test naming E30 as the fix.
8. `grid.spawn` is unchanged; `KindCount` and `AttachPointCount` are **not changed by this module**.
   Sibling Wave 8 modules do change both, so this criterion is a statement about E40's own diff and
   is verified by reading it, not by reading the constants after the wave has landed.

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **E28 `param-parity`** | Owns `spawn.entity`'s `count` loop and `grid.spawn`'s `graveType`. E40 owns the four new `kind` arms. Do not build E28's half here (map §16) |
| **E29 `kind-value-guard`** | The `kind` domain is refused by E29's per-kind value check. Without E29, E40 ships a local check commented with E29's name — never a silent accept |
| **E30 `channel-pool`** | Owns the pricing key that would let a pet and a mower price differently. E40 must not pre-empt it |
| **Wave 8 count churn** | E35, E36, E37 and E41 all move `KindCount`; E35 and E41 also move `AttachPointCount`. E40 moves neither, but its tests must not assert a literal for either — `AtomKindRegistryTests.cs:22-23` are `Const == BuiltCount` self-consistency checks and stay green through every sibling |
| **Loam / economy program** | A coin spawn is an economy source. Any yield attached to it reconciles to the economy SSOT, not to a number chosen here |
| **Perf** | `AutoCollectTick` already scans `CoinSun`/`CoinMoney` every 150 ms (`CheatActions.cs:860-890`). Spawning coins from content raises that population; note it if a spawn `count` is ever unbounded in content |
| **`AtomImporter` staleness** | E40 is registry/executor **code**; the importer reports "nothing changed" because its hash covers seed data. State it in the rollout note |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`) |
