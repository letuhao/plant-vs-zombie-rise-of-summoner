# Spec: param-parity (E28)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.2 defects 3–5
and the capability map's [§12](../effect-atom-map.md). Module **E28**, Wave 7. **No dependencies.**

**What it owns: closing the gap between what a kind declares and what its executor honours.** Seven
params are declared-and-dropped, refused-by-note, or narrower than their vocabulary. Each is a place
where an author is told *"yes"* and the game does nothing, or told *"no"* about something the injector
can already do.

---

## 1. The seven, each verified

| # | Kind · param | State | Evidence |
|---|---|---|---|
| **1** | `resource.delta` · `channel` | **hp only.** Any other channel sets `skipped = true` | `InjectorEffectActionSink.cs:146-151` |
| **2** | `board.action` · `damage` | **Declared, validated, dropped.** The payload is `op/row/col/x/y`; `damage` never enters it, so `DebugActions` takes its own default of **1800** | declared `AtomKindRegistry.cs:308`, default 1800 at `DebugActions.cs:306`, dropped `InjectorEffectActionSink.cs:398-405` |
| **3** | `status.clear` · `status` | **4 of 21.** Only `butter`/`freeze`/`cold`/`poison` | `InjectorEffectActionSink.cs:307-318` |
| **4** | `grid.clear` · cell | **No `row`/`col` declared at all.** With more than one match and `random:false` the executor refuses outright | schema `AtomKindRegistry.cs:325-331`; refusal `DebugActions.cs:666-669` |
| **5** | `spawn.entity` · `count`, `atk` | **Refused at load** by `NotImplementedNote` | `AtomKindRegistry.cs:286-289`, `:297-298` |
| **6** | `grid.spawn` · `graveType` | **Refused at load**, though `DebugActions.cs:382-383` supports it | `AtomKindRegistry.cs:318-319` |
| **7** | `box.set` · `cells[]` | **Refused at load**; executor is single-cell | `AtomKindRegistry.cs:340-341` |

**Sorted:** 1–4 are **wiring gaps** (declared or reachable, not honoured). 5–7 are **honestly declared**
gaps — the `NotImplementedNote` mechanism working exactly as designed, and now the notes come off.

Plus one **content** defect: **`fx.set_dirt_box` authors `boxType: 1`, which is `Water`. Dirt is `2`.**
The registry's own D7 comment shows the migration froze the broken value rather than correcting it, and
`ExecSetBox`'s default is also 1.

---

## 2. Why #1 and #5 rank above the rest

**`resource.delta` at one channel makes the six-resource layer unreachable.** The stat layer now governs
`hp`, `stamina`, `hunger`, `spirit`, `qi`, `poise`, with all four families covering all six and a drift
guard holding it there. **No atom can touch five of them on the lawn.** Every generated support action
that restores stamina or spends qi is inert until this lands.

**`spawn.entity.atk` is why every non-zombie spawn prices at exactly zero.** `CostFunction.SpawnBody`
returns `PowerVector.Zero` when `hp == 0 && atk == 0` (`CostFunction.cs:193`); `hp`/`maxHp` are
`HonouredOnlyWhen: "kind=zombie"` (`AtomKindRegistry.cs:294-295`); and `atk` carries a
`NotImplementedNote` so it is refused for **every** kind (`:297-298`). A plant or bullet spawn can
therefore supply neither field. **So every plant spawn prices at exactly zero** — and still passes
`Every_shipped_atom_can_be_priced`, which only asserts `Ok`. **The budget gate stays green while the
number is meaningless.** Unblocking `atk` is what closes it, which is why **E28 is a prerequisite of
E30**, not a sibling.

> **⛔ CORRECTED 2026-09-03 — the headline read *"84% of a spawn-heavy corpus"*, and that number had no
> source.** No count in this repo produces it, and nothing here says which corpus it was over. **The
> mechanism is verified** — the three `file:line` cites above were each opened and checked — and it is
> the mechanism that carries the argument: *every* non-zombie spawn prices at zero, which is a stronger
> and checkable claim than a percentage of an unnamed set. If a share of the authored corpus is wanted
> later, it is a count someone runs, not a figure a spec asserts.

---

## 3. The contract

For each of the seven: **either honour the param end to end, or keep the refusal and say why in the
note.** A third state — declared, accepted, ignored — is what this module exists to remove.

| # | Change |
|---|---|
| **1** | `ExecApplyResourceDelta` honours all six `ResourceIds`. A channel outside the set is a **refusal**, not a skip |
| **2** | `ExecBoardAction`'s payload carries `damage`. **Also fix the mirror defect:** the payload forwards `x`/`y`, which the schema does **not** declare, so authoring them is refused while the sink reads them — the contract is wrong in both directions |
| **3** | `ExecClearStatus` reaches every status `status.apply` can apply on that runtime. Where no withdraw path exists — `ember`/`jala` have **no Unity-side expiry** (`DebugActions.cs:893-899`) — the refusal is **explicit and named**, never a silent miss |
| **4** | `grid.clear` declares `row`/`col`; the sink forwards them; `selector` keeps its `random`/targeted meaning. **Fix the shipped naming lie:** `fx.grid_item_cycle` variant b authors `selector: "last"`, which selects **randomly** |
| **5** | `count` loops the executor; `atk` reaches the spawned body. `count` is **floored at 1** — structural, and the comment must say so |
| **6** | `graveType` is honoured and forwarded |
| **7** | `cells[]` paints multiple cells in one apply |
| **content** | `fx.set_dirt_box` → `boxType: 2` |

---

## 4. What this module must NOT do

- **Add a kind, an opcode or an attach point.** Wave 8 owns new capability; E28 honours what is declared.
- **Widen a vocabulary beyond its runtime.** `status.clear` reaches what that runtime can withdraw — no
  more. An unwithdrawable status is a **named refusal**, not a pretend success.
- **Silently clamp.** Out of range is a refusal that names the value.
- **Use `float` for a magnitude**, or divide before the last step. `long`, widen before multiplying,
  divide by 1000 once, overflow throws. **`ExecEconomy` already casts a magnitude to `float`
  (`InjectorEffectActionSink.cs:452`) — pre-existing, and this module should not add a second.**
- **Cap a magnitude.** `count`'s floor of 1 is structural and must carry the comment `AGENTS.md` requires.
- **Change `fx.set_dirt_box`'s id or family.** Only the wrong value.

---

## 5. Testing strategy

**One end-to-end test per param, each asserting the effect actually lands** — not that the call was made.
That distinction is the whole point: every defect here passed validation already.

| # | Test | Proves |
|---|---|---|
| 1 | A `resource.delta` on **each of the six resources** changes that pool | #1, and the six-resource layer is reachable |
| 2 | A `board.action` with `damage: 400` deals **400, not 1800** | #2 — a hardcoded default was masking it |
| 3 | `x`/`y` are either declared **and** forwarded, or neither | #2's mirror defect |
| 4 | Apply then clear, for **every** status the runtime can withdraw | #3 |
| 5 | `ember` clear is a **named refusal** citing the missing expiry | An honest gap stays visible |
| 6 | `grid.clear` at an explicit cell clears **that** cell with two matches present | #4 |
| 7 | `spawn.entity{kind:"plant", atk:N}` **prices non-zero** | #5 — the every-non-zombie-spawn-at-zero defect |
| 8 | `count: 3` spawns **three** entities | #5 |
| 9 | `graveType` produces the named grave; `cells[]` paints every listed cell | #6, #7 |
| 10 | `fx.set_dirt_box` paints **Dirt** | The content fix |
| 11 | **Planted violation:** a seventh resource id is **refused**, not skipped | Refusal replaced skip |
| 12 | **Planted violation:** a declared param that reaches no executor **fails a test** | The defect class cannot return |

**Test 12 is the durable one.** Every defect here is *"declared and dropped"*, so a test that walks each
kind's declared params and asserts each reaches its executor is what stops the class recurring.

**The injector is not built by CI** — these need a local build and an owner-run live check.

---

## 6. Acceptance criteria

1. All six resources reachable by `resource.delta`; a seventh is refused.
2. `board.action` honours `damage`; the `x`/`y` asymmetry is resolved in one direction.
3. `status.clear` reaches every withdrawable status; the rest refuse by name.
4. `grid.clear` targets a cell; the `selector: "last"` naming lie is corrected.
5. `count` and `atk` are honoured; a plant spawn prices non-zero; `count` floors at 1 with a structural
   comment.
6. `graveType` and `cells[]` honoured.
7. `fx.set_dirt_box` paints Dirt.
8. **No param anywhere is declared, accepted, and ignored** — proven by test 12 across all 12 kinds.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. May run first in Wave 7 |
| **Unblocks** | **E30** (`atk`, and the six resources a pool would draw over) · **E37**, **E39**, **E40** in Wave 8 |
| **battle-timeline B25/B26** | Rewrites `EffectRuntime`'s `_dotAccum`/`_shieldAccum` grids while this edits the same drain chain — §6's H1 hazard. Sequence them |
| **combat-unification E1** | Pins itself to *"existing vocabularies (status catalog ids…)"* and demands zero-rider battles byte-identical. `status.clear` 4 → 21 touches that surface |
| **VFX** | The lawn status path is a closed Unity switch that **bypasses `StatusRuntime.OnApplied`**, the only status-VFX producer. Statuses newly reachable here would land with **no aura, tint or marker** — a real gap to name, not to fix in this module |
