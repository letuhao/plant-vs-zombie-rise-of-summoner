# Spec: match-modify (E35)

**Program:** effect-atom · **Map:** [../effect-atom-map.md](../effect-atom-map.md) §13 ·
**Ideal:** [../effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 1 ·
**Definitions (win over this spec):** [definitions.md](definitions.md) ·
**Kind registry:** [spec-atom-kind-registry.md](spec-atom-kind-registry.md)

**Status: specced, unbuilt (2026-09-03).** Wave 8, depends on E34.

E35 owns **a new attach point, `Match`** (one of two Wave 8 adds — E41's `Ui` is the other, and the map
leaves their order free, so neither is "the sixth"), and the first kind on it, `match.modify` — the ability
for a container to change the rules of the match itself rather than of an entity in it. It covers the
eleven `Board.config` fields the injector already writes from cheat state: zombie health / damage /
speed / count multipliers, starting armour, the plant and zombie modify bands, `waveInterval` and
`conveyInterval`. This is the *"curse this level"* axis, and no existing attach point is a match.

---

## 1. What exists today

**Built** — the whole Unity-side write path exists and is live-proven.

| Fact | Evidence |
|---|---|
| `CheatActions.ApplyBoardConfig` writes all eleven fields onto `board.config` | `src/FusionRpg.Injector/CheatActions.cs:635-671`; the field list is `:653-664` |
| It writes only fields the user has actually set, and no-ops otherwise | `:641-650` (`CheatState.IsUserSet` over the eleven `E-*` ids) |
| It latches `CheatState.BoardConfigLocked = true`, so the next board re-applies | `:668`, consumed at `GameHooks.cs:471-476` (`Board.Awake`) |
| The inverse exists — `LoadBoardConfigIntoCheats` reads the live config back and clears the latch | `CheatActions.cs:673-696` |
| The applied config is published as `board.modifiers` | `CheatActions.cs:665-667`; dump shape at `GameDumps.cs:185-201` |
| Proven LIVE | `docs/research/effect-runtime/07-effect-opportunities.md:51` — *"Board pressure · `Board.config` E-* · **READY** LIVE F35 (E-ZS=0.4; originSpeed scales)"*; coverage rows `docs/research/cheat-menu-coverage.md:224-235` |
| The five attach points are `Stat`, `Resource`, `Status`, `Shield`, `Board`, guarded at exactly 5 | `AtomKind.cs:7-14`; `AtomKindRegistry.cs:16`; guard `AtomKindRegistryTests.cs:23` |
| 12 kinds and 12 opcodes in an exact bijection | `AtomKindRegistry.cs:18`; `AtomCompiler.cs:180-195` (`OpcodeOf`); `EffectDtos.cs:22-45` |

**Wiring gap.**

| Gap | Evidence |
|---|---|
| `ApplyBoardConfig` is reachable **only** from cheat/debug state — no content path exists | its four callers: `CheatCommandRunner.cs:647`, `DebugActions.cs:258`, `:281`, `GameHooks.cs:474` |
| Nothing restores the eleven fields when a match ends; the latch survives it | `EffectRuntime.NotifyMatchEnd` (`EffectRuntime.cs:130`) does not touch `BoardConfigLocked` |

**Real gap.** There is no `Match` attach point (`AtomKind.cs:7-14` — five members, count guarded), no
match-scoped kind, no opcode and no sink arm (`AtomKindRegistry.cs:105-346`; `AtomCompiler.cs:180-195`;
`InjectorEffectActionSink.cs:20-46`). `stat.modify` cannot stand in: G8 makes primary `defense`
match-scope-only through a **side-wide cached prefix value**, a different mechanism covering one channel
(`spec-atom-kind-registry.md` G8).

## 2. The contract

### 2.1 The attach point

```csharp
// src/FusionRpg.Core/Effects/Atoms/AtomKind.cs
public enum AttachPoint { Stat, Resource, Status, Shield, Board, Match }
```

**E35 adds one attach point and one kind. It states its own delta, never an absolute.**
`AtomKindRegistry.AttachPointCount` (`AtomKindRegistry.cs:16`, `5` today) gains **+1**; `KindCount`
(`:18`, `12` today) gains **+1**. Both are structural cardinalities (`tunables-ssot.md` T2), both
guard-tested, neither a tuning row.

> **⛔ CORRECTED 2026-09-03 — this section named absolutes (`5 → 6`, `12 → 13`) and no spec stated the
> end state.** Wave 8 moves both constants from more than one module: **two** attach points are added
> (E35 `Match`, E41 `Ui`) and **four** kinds (E35 `match.modify`, E36 `wave.control`, E37
> `bullet.modify`, E41 `ui.present`). An absolute in one spec is false the moment a sibling lands,
> which is how E40 came to assert *"`KindCount = 12` … untouched"* while four modules were queued to
> change it, and how E37 came to say *"kind #13"* and *"Five today"* in the same section. **Every
> count claim in this file is now a delta**, and the end state is stated once, below.

**The Wave 8 end state, stated once here and referenced by the other four specs:**
`AttachPointCount = 7` — Stat, Resource, Status, Shield, Board (the five at `AtomKind.cs:7-14`) plus
**Match** (E35) and **Ui** (E41) — and `KindCount = 16`, from `12` at `AtomKindRegistry.cs:18`.

> **⛔ CORRECTED 2026-09-03 — the end state was recorded as 15 and the arithmetic does not give 15.**
> Wave 8 adds **four** kinds — `match.modify` (E35), `wave.control` (E36), `bullet.modify` (E37),
> `ui.present` (E41). **12 + 4 = 16.** The 15 came from the spec-review record and was repeated into
> every spec that cites this paragraph; the agent applying those corrections used it as directed and
> flagged the discrepancy rather than silently reconciling it, which is why it was caught.
> **`AttachPointCount = 7` was always right** (5 + `Match` + `Ui`).
>
> **This paragraph is the single place the end state is stated.** Every other spec references it and
> asserts only its own delta, so a change to the wave's contents is a one-paragraph edit — and no test
> carries a literal, because both guards are `Const == BuiltCount` self-consistency checks.

> ⚠ **One thing for the owner, not a blocker.** The four Wave 8 kinds are `match.modify` (E35),
> `wave.control` (E36), `bullet.modify` (E37) and `ui.present` (E41). Twelve plus four is **16**, not
> 15. The review of 2026-09-03 fixed the end state at 15, which holds only if one of the four does not
> land in Wave 8. **Whichever module lands last asserts what the registry actually builds** — the
> guard at `AtomKindRegistryTests.cs:22` compares `KindCount` to `All.Count`, so it is a
> self-consistency check and cannot be satisfied by a number copied out of a spec. Every module below
> states its own **delta**; only this paragraph states a total, and it is the paragraph to fix if the
> wave's contents change.

**The ADR row does not exist yet, so E35 creates it.**

> **⛔ CORRECTED 2026-09-03 — this said the attach-point ADR row *"must be amended"*.** There is no
> such row: `grep -in "attach" docs/architecture/decisions.md` returns **nothing**, while
> `AtomKind.cs:4` says *"Five, guarded by ADR"*. The guard the comment names has never been written
> down, so criterion 1 was unsatisfiable as worded. **E35 writes the row for the first time**, and it
> must record both the list and its growth rule, so the next module to add a point amends a real row
> instead of discovering the same absence.

The new `decisions.md` topic row reads *"Atom attach points"* and states: the closed list, that it is
guard-tested at `AtomKindRegistryTests.cs:23` against `Enum.GetValues<AttachPoint>().Length`, and that
growing it is a reviewed change to that row. E41 amends the same row when `Ui` lands; §6's count
hazard covers the ordering.

**`Board` versus `Match`, stated so the seam does not blur:** a `Board` kind acts on a **cell or an
entity within the running match** (`spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`,
`box.set` — each takes `row`/`col`). A `Match` kind changes a **rule the whole match is played under**
and names no cell. If a proposed param needs a row, it is `Board`.

### 2.2 The kind

```csharp
new("match.modify", AttachPoint.Match, new ParamSchema(
        new ParamDef("field",  ParamKind.String, Required: true),
        new ParamDef("amount", ParamKind.Value,  Required: true)),
    new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.None),
    AtomTriggers.MatchEvents,          // E34: OnWave, OnMatchStart, OnMatchEnd
    PowerCategory.Offense | PowerCategory.Survivability | PowerCategory.Control,
    "Sets one Board.config field for the match. Set-only: the executor assigns, and a multiply " +
    "would need to read live host state, which the overlay rule forbids.")
```

**No `op` param.** `ApplyBoardConfig` assigns (`CheatActions.cs:653-664`); declaring `op: "mul"` would
validate a key the executor drops, which is G1 exactly. Set-only, and the note in the kind says why.

**Battle and Sim are `None`.** Neither has a `Board.config` and neither has a consumer — flipping a
cell without an executor is D6 (`definitions.md` §13; `AtomKindRegistry.cs:140-143`).

### 2.3 `field` — a closed value set of eleven, and their units

E29's registry-backed per-kind value check is what refuses anything outside this list. Until E29 lands,
this kind carries its own check in `AtomKindRegistry.Validate`, the same shape as G6's channel check at
`AtomKindRegistry.cs:61-73`.

| `field` (all eleven, `CheatActions.cs:653-664`) | Unit in the atom layer | At the Unity boundary |
|---|---|---|
| `zombieHealthMultiplier` · `zombieDamageMultiplier` · `zombieSpeedMultiplier` · `zombieCountMultiplier` · `plantModifyMin` · `plantModifyMax` · `zombieModifyMin` · `zombieModifyMax` | integer per-mille (bounded ratio) | `/ 1000f`, once, last |
| `waveInterval` · `conveyInterval` | integer **ms** (`definitions.md` §2) | `/ 1000f`, once, last |
| `zombieStartAmmor` | **`long`** — a magnitude, not a ratio | narrowed with a `checked` cast; overflow **throws** |

Per-mille and ms are bounded/structural units and are exempt from the no-caps rule — stated here because
the exemption must be visible, not inferred. `zombieStartAmmor` is the one true magnitude on this kind:
it is `long` end to end, and the narrowing cast at the boundary is `checked` so an overflow throws
rather than wrapping a curse into a gift.

#### `zombieStartAmmor` needs a `long` channel on `CheatState` — the path §2.5 mandates has none

> **⛔ CORRECTED 2026-09-03 — the `long` above could not survive the write path this spec requires.**
> §2.5 routes every field through `CheatState`, and **no `long` exists anywhere on its value path**
> (the two `long` fields it does have, `DocumentRevision`/`AppliedRevision` at `CheatState.cs:268-269`,
> are bookkeeping and carry no cheat value):
> `SetFloat(id, double)` stores a `double` (`CheatState.cs:309-312`), `FVal` reads it back as a
> **`float`** (`:277-280`), `IVal` rounds that float to an **`int`** (`:283-288`), and
> `ApplyBoardConfig` assigns `c.zombieStartAmmor = CheatState.IVal("E-ZARM")`
> (`CheatActions.cs:657`). The restore direction is the same store:
> `SetFloatQuiet("E-ZARM", c.zombieStartAmmor)` (`:684`). **No `long` survives any hop.**

**The choice, made rather than left open: E35 adds a `long` channel to `CheatState`.** `SetLong(id,
long)` and `LVal(id)` alongside the existing float pair, used by `E-ZARM` only; the Unity assignment
becomes a `checked` narrow from that `long` to the host field's `int`, and `LoadBoardConfigIntoCheats`
round-trips `E-ZARM` through `SetLong` rather than `SetFloatQuiet`.

**Why not the other option — "prove the value bounded and say so".** A bound does exist: PvZ's own
`zombieStartAmmor` is an `int`, and nothing content authors can make the host field wider. But the
float hop *below* that bound is where the value actually dies: `FVal`'s `(float)` cast stops being
integer-exact at **16,777,216** (`CLAUDE.md`'s measured table, row 1), which is far under `int.MaxValue`
and well inside what `contentScale` reaches. A cursed armour value of 20,000,000 would arrive as a
different number with no error, no log and no throw. **A bound whose violation is silent is not a
bound** — it is the `fx.set_dirt_box` class of defect with arithmetic instead of a name. The `int`
ceiling stays, as a derived absolute bound that **throws** at the `checked` narrow; the float hop goes.

This is one new pair of accessors on a class that already has four (`On`, `FVal`, `IVal`, `IsUserSet`),
and it is the only place in Wave 8 where the repo's *"`long` for any magnitude, never `float`"* rule
meets a shipped store that cannot hold one.

### 2.4 Seed JSON

```json
{
  "family": "atom.curse-swarm",
  "tier": 3,
  "kind": "match.modify",
  "name": "Swarm",
  "icdKey": "curse.swarm",
  "params": { "field": "zombieCountMultiplier", "amount": 1500 },
  "when": { "trigger": "OnMatchStart" }
}
```

`amount: 1500` is 1.5× — per-mille, integer, no float anywhere in the row.

### 2.5 The opcode and the executor

`EffectActions.ModifyMatch = "ModifyMatch"` (`EffectDtos.cs`), `AtomCompiler.OpcodeOf` gains
`"match.modify" => EffectActions.ModifyMatch`, and `InjectorEffectActionSink` gains an `ExecModifyMatch`
arm beside the eleven at `:22-44`.

**Adding an opcode means growing `/effects/contract`'s `actions` array in the same change.**

> **⛔ ADDED 2026-09-03 — the published `actions` list already lies, and nothing was fixing it.**
> `EffectActions` declares **twelve** constants (`EffectDtos.cs:22-44`), and
> `/effects/contract` publishes **ten** of them under `frozen = true`
> (`DebugEndpoints.cs:388-394`) — `GrantShield` and `ModifyDerivedStat` are missing. **E33 owns
> repairing the existing two** (`spec-activation-edge.md` §2.1a); E35 owns not adding a thirteenth
> hole. `ModifyMatch` goes into that array in the same commit that adds the constant, and criterion 2
> asserts it by count.

The same obligation binds E36 (`WaveControl`) and E37 (`BulletModify`). A published list that lies is
the defect E33 and E34 exist to close, and it closes for `triggers` and stays open for `actions` unless
each opcode-adding module says this out loud.

**The executor writes through `CheatState`, never onto `board.config` directly:**

1. Map `field` → its `E-*` id (`E-ZH`, `E-ZD`, `E-ZS`, `E-ZC`, `E-ZARM`, `E-PMIN`, `E-PMAX`, `E-ZMIN`,
   `E-ZMAX`, `E-WAVE-I`, `E-CONV-I` — `CheatActions.cs:641-664`).
2. `CheatState.SetFloat(id, value, "effect")` — except `E-ZARM`, which goes through the `long`
   channel §2.3 adds (`CheatState.SetLong`), because it is the one magnitude here.
3. Call `CheatActions.ApplyBoardConfig()`.

That is one writer for `board.config`, it inherits the `BoardConfigLocked` re-application across boards
(`GameHooks.cs:471-476`) for free, and it publishes `board.modifiers` for free (`CheatActions.cs:665`).
Writing the field directly would fork the writer and lose both.

### 2.6 Scope and lifetime

- **Owner key must be `match` or `player:`.** An `entity:`/`plant:`/`zombie:` binding is
  `ScopeUnsupported` at bind — the same refusal shape G8 already uses (`definitions.md` §6).
- **A match modifier ends with the match.** `EffectRuntime.NotifyMatchEnd` (`EffectRuntime.cs:130-134`)
  calls `CheatActions.LoadBoardConfigIntoCheats()` (`CheatActions.cs:673`), which reads the live config
  back and clears `BoardConfigLocked`. Without this a curse from one match leaks into the next, and the
  latch means it would leak silently and permanently.

  > **⛔ CORRECTED 2026-09-03 — that restore erases the operator's own cheat state, every match end.**
  > `LoadBoardConfigIntoCheats` is not scoped to what a curse touched. It calls `SetFloatQuiet` on
  > **all eleven** `E-*` ids (`CheatActions.cs:677-687`), and `SetFloatQuiet` sets
  > `e.FloatValue = v; e.Enabled = true; e.IsSet = true` (`CheatState.cs:388-394`) — so after one
  > match end every `E-*` key reads back as *user-set to the level's own value*. An operator who set
  > `E-ZS = 0.4` from the cheat menu and then played a match finds it silently replaced by whatever
  > the level shipped, with no emit and no note. **The unconditional restore is a bigger bug than the
  > leak it fixes**, and this was specced as a one-line call with none of that said.
  >
  > **What E35 must do instead.** `NotifyMatchEnd` restores **only the ids a live `match.modify` grant
  > actually wrote** — the executor records them per match — and it restores them by clearing the
  > entry (`IsSet = false`), not by writing the level's value into it. `BoardConfigLocked` is cleared
  > only when no `E-*` key is left user-set by anything else. An id the operator set by hand and no
  > atom touched is never written. This is the same one-writer discipline §2.5 already holds in the
  > other direction, applied to the withdraw path.
  >
  > **The shipped call is untouched.** `GameHooks.cs:471-477` still calls `ApplyBoardConfig` or
  > `LoadBoardConfigIntoCheats` on every `Board.Awake`, and `CheatCommandRunner.cs:650` still exposes
  > it as an operator command. E35 adds a narrower restore for its own writes; it does not change
  > what the existing two callers do.
- **Last write wins within a match.** Two atoms naming the same `field` do not stack; the executor
  assigns. That is a consequence of set-only semantics and must be said out loud, because an author
  will assume stacking.

## 3. What it must NOT do

- **No second writer for `board.config`.** Everything goes through `CheatState` + `ApplyBoardConfig`
  (§2.5). This is the same discipline `EntityStatWriter` holds for combat fields.
- **No reading live host state to compute a delta.** The overlay rule is that we contribute signed
  deltas and intents and never read the foundation's current state; that is why the kind is set-only.
- **No `row`/`col`/`cells` param.** Anything needing a cell is `Board`, not `Match` (§2.1).
- **No magnitude chosen by a model.** Every `amount` in shipped content is authored or rolled from a
  value spec; band and tier numbers belong in `data/tuning/`, never in `AtomKindRegistry` or the
  executor. `long` for a magnitude, never `float`; widen before multiplying; divide by 1000 last,
  exactly once; overflow throws rather than clamping or wrapping.
- **No cap on `zombieStartAmmor` or any multiplier** beyond the `checked` narrowing that the host field
  type forces. An absolute bound derived from the arithmetic throws; it does not clamp.
- **No `float` on the `zombieStartAmmor` path.** §2.3's `long` channel is the point; routing it through
  `SetFloat`/`FVal` reintroduces the silent 16,777,216 corruption the correction there describes.
- **No blanket `LoadBoardConfigIntoCheats()` on match end.** §2.6. The restore is scoped to the ids a
  grant wrote, and it clears them rather than overwriting them with the level's values. An operator's
  hand-set `E-*` key is not this module's to touch.
- **No thirteenth opcode without the published list growing with it.** §2.5. `ModifyMatch` enters
  `/effects/contract`'s `actions` array in the same change as the constant.
- **No new attach point beyond `Match`.** `ui` (ideal §W8.4 row 7) is E41's.

## 4. Testing strategy

Core tests cover the vocabulary, the schema, the compiler and the units. **The injector is not built by
CI** — `.github/workflows/ci.yml` tests ten managed projects and never compiles `FusionRpg.Injector` —
so `ExecModifyMatch` is proven by a LIVE run, not a green pipeline.

| Case | Expect |
|---|---|
| `AttachPointCount == Enum.GetValues<AttachPoint>().Length`, both one higher than before this module | pass; the guard at `AtomKindRegistryTests.cs:23` is a self-consistency check, so the test asserts the **delta**, never a literal |
| `KindCount == AtomKindRegistry.All.Count`, both one higher than before this module, and `OpcodeOf` returns non-null for every kind | pass — the bijection is asserted over the registry, not against a copied number (`AtomKindRegistryTests.cs:22`) |
| `/effects/contract`'s `actions` array | contains `ModifyMatch`; count asserted, so a thirteenth opcode cannot arrive silently (§2.5) |
| `match.modify` with `field: "zombieHealthMultiplier"`, `amount: 1200` | `Ok` |
| `match.modify` with `field: "zombieHelthMultiplier"` (typo) | `BadParamValue`, naming the field and the eleven legal values |
| `match.modify` with `op: "mul"` | `UnknownParam` |
| `match.modify` with `row: 2` | `UnknownParam` |
| `match.modify` with `trigger: "OnDamageDealt"` | `TriggerNotAllowed` |
| Bind a `match.modify` atom to an `entity:{ptr}` owner | `ScopeUnsupported` |
| `zombieStartAmmor` at `long.MaxValue` | the narrowing cast **throws**; no wrap, no clamp |
| `zombieStartAmmor` at `20_000_000` round-tripped through `CheatState` | reads back **exactly** `20000000`. Through `SetFloat`/`FVal` it would not — that is the float hop §2.3 removes |
| **PLANTED VIOLATION** — drop the `field` value check (make it accept any string) | the typo test fails. Without it, `zombieHelthMultiplier` validates, compiles, reaches the sink, matches no case and does nothing forever — E29's own stated defect, and the reason this test is the guardrail rather than the schema |
| **PLANTED VIOLATION** — remove the scoped match-end restore | a two-match test fails: match 2 starts with match 1's multipliers still applied |
| **PLANTED VIOLATION** — replace the scoped restore with a blanket `LoadBoardConfigIntoCheats()` | a **cheat-state** test fails: set `E-ZS` by hand, bind no `match.modify` atom at all, play one match, and `IsUserSet("E-ZS")` must still be true with the operator's own value. §2.6 |

**LIVE proof (owner-run):** bind a container holding `{field: zombieSpeedMultiplier, amount: 400}` on
`OnMatchStart`, enter a level, and read `board.modifiers`. It must show `0.4`, matching F35's own
measured row (`07-effect-opportunities.md:51`). Withdraw, start a second match, and it must show the
level's own value.

## 5. Acceptance criteria

1. `AttachPoint.Match` exists and `AttachPointCount` is **one higher than before this module**, matching
   `Enum.GetValues<AttachPoint>().Length`. **A `decisions.md` topic row for atom attach points is
   *created*** — there is none today (`grep -in "attach" docs/architecture/decisions.md` returns
   nothing) — naming the list, its guard test, and that growth is a reviewed change to that row.
2. `match.modify` is registered; `KindCount` is **one higher than before this module** and equals
   `AtomKindRegistry.All.Count`; the kind ↔ opcode bijection holds for every registered kind; and
   `/effects/contract`'s `actions` array contains `ModifyMatch`, asserted by count.
3. `field` accepts exactly the eleven values in §2.3 and refuses anything else with `BadParamValue`.
4. `op`, `row`, `col` and `cells` are all `UnknownParam` on this kind.
5. Units are per-mille for the nine ratios, integer ms for the two intervals, `long` for
   `zombieStartAmmor`; the boundary conversion divides by 1000 once and narrows `checked`.
6. Only `match` and `player:` owner keys bind; anything else is `ScopeUnsupported`.
7. The executor's only write path is `CheatState` + `CheatActions.ApplyBoardConfig`, and
   `zombieStartAmmor` travels it through a **`long`** channel — no `float` hop anywhere (§2.3).
8. Match end restores **only the `E-*` ids a `match.modify` grant wrote**, by clearing them, and
   leaves an operator's hand-set key untouched; `BoardConfigLocked` clears only when nothing else
   holds an `E-*` key user-set (§2.6).
9. All three planted violations in §4 fail their tests.
10. The LIVE proof reproduces F35's measured `0.4`, and a second match is clean.

## 6. Dependencies and cross-program hazards

**Depends on:** E34 (`AtomTriggers.MatchEvents` — without it this kind has no trigger it may carry).
**Blocks:** E36 `wave-control`, which reuses the `Match` attach point.

| Hazard | Detail |
|---|---|
| **Sealed list growth, and the seal that was never written** | `AtomKind.cs:4` says attach points are *"Five, guarded by ADR"* — and `decisions.md` has **no such row**. E35 writes it (§2.1). Growing the list is a reviewed change to that row, not a constant edit; the map's §16 *"each needs a named change, not a rename to dodge it"* applies to the guard tests too |
| **CI gates that count** | `AtomKindRegistryTests.cs:22-23` assert the counts and **will go red** the moment the kind lands. That is correct; update them deliberately in the same commit and say so in the message. Both assertions are `Const == BuiltCount` self-consistency checks, so the edit is to the `const`, never to a literal in the test |
| **Two attach points and up to four kinds land in one wave** | E41 adds `Ui` on the same constant; E36 and E37 each add a kind. Whichever lands last edits the guard to the **combined** value and says so in its commit. §2.1 states the wave end state once so no module has to guess it, and no module states an absolute of its own |
| **Injector not built by CI** | `.github/workflows/ci.yml` — `effect-atom-map.md` §6 H1 recurring. `ExecModifyMatch` has no compile-time proof in the pipeline |
| **`AtomImporter` staleness trap** | Map §16: the importer reports *"nothing changed"* when only compiler **code** changed. E35 is a compiler-code change (a new `OpcodeOf` arm) as well as a schema one |
| **Stale instances** | A `catalog_revision` bump makes every previously rolled `effect_instance` unbindable (`StaleInstance`). Pre-existing for any content change — state it in the rollout note |
| **`definitions.md` §2 units** | The definitions win over this spec. §2.3's per-mille/ms choices follow §2's table; if that table moves, this section moves with it |
| **Cheat-state coupling** | The executor shares the `E-*` ids with the cheat UI, so an owner playing with the cheat menu open can overwrite a bound curse and vice versa. Acceptable and intentional — one writer — but it must be in the rollout note, not discovered live |
