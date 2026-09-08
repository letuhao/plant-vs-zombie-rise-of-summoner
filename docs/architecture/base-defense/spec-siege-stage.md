# Spec: `siege-stage`

**Module 17 of 29 · level 8b · depends on `board-render` (8), `siege-resolver` (7) · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **✅ Unblocked** — the `decisions.md` fifth-stage amendment was approved
by the owner on 2026-09-04.

---

## Objective

**The fifth stage: `#/siege/{id}` — where a siege is played.**

Decisions 8 and 9 put the board on its own turn-based stage rather than on `battle`, so a besieged
base's HUD and transport never constrain a squad battle's. Owner decision (round 6): *"Turn-based, but
its own stage."*

**Success looks like:** a player enters a siege from the world map, plays it turn by turn, and returns
to a world whose state reflects what happened — with the same resolver that CI already proves.

---

## The approved amendment, and what it obliges

`decisions.md`'s Game GUI row is amended: **five stages**, `Sanctum · World · Lawn · Battle · Siege`.
Three costs came with the approval and are this module's obligations, not footnotes:

| # | Cost | Obligation |
|---|---|---|
| 1 | **20 CI checks** reference the four-stage count | Re-scope them: the count assertion becomes 5, and the **GG-7 reachability matrix gains a row** |
| 2 | IA documentation is now wrong in three places | Correct `design/information-architecture.md` §1 (*"Four stages, one at a time"*), §2's four-entry catalog, and the verb table's `Space` row (*"Lawn and battle only"*). Also `game-gui-principles.md:965`'s D2 |
| 3 | **Two** declared-but-unbuilt stage ids | `railState.ts:31` already declares `battle` with nothing behind it. Adding `siege` makes two. Say so in the IA doc rather than leaving it to be discovered |

**GG-4's test is satisfied** and is why this is a stage rather than a layer: a siege is a place you go
**to act**, not to look. The alternative — reusing `battle` with a second driver — was considered and
rejected in `base-defense-ideal.md` §5.11.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.** `src/stages/world/` (6,902 LOC, subdirectories `hud/ inspector/ playback/ render/
targeting/` — **the shape to copy**), `src/stages/lawn/`, `src/stages/sanctum/`, the shell rail, the
six z-bands, `board-render`'s generic layer, `siege-resolver`.

**Real gap.** No `siege` stage, route, or view model.

---

## The contract

### 1. Route and stage id

`#/siege/{siegeId}`. `RailUnlockInputs.currentStageId` gains `"siege"`.

**The URL encodes stage + open layers, never a replacement screen** (GG-1). A siege with its structure
panel open is `#/siege/abc?layer=structures`, and closing it returns to the same board state.

### 2. Six conditional shell files

The shell needs per-stage entries in six places. Each is a **row**, matching the four that exist:

1. `railState.ts` — the stage id union and its rail entry
2. the route table
3. the stage → default-layer map
4. the Esc/back-target map
5. the reachability matrix (**GG-7** — the new row cost 1 names)
6. the stage-label i18n catalog (Lingui, English-first per the tech-stack decision)

**Six rows, zero branches.** If a `if (stage === "siege")` appears in shell code, the shell has grown a
special case and the next stage will need another — the same *"adding a mode adds a row, never a
branch"* discipline `battle-clock-profile` enforces in the kernel.

### 3. Subdirectory layout — copy `world`

```
src/stages/siege/
  SiegeStage.tsx
  hud/          turn indicator, resources, action budget
  inspector/    selected unit / structure detail
  render/       board-render bindings
  targeting/    action + move target selection
  playback/     replay a resolved siege
```

`world` earned this shape over 6,902 LOC. Inventing a different one here means two stages organised
two ways and a reviewer who has to learn both.

### 4. §7 cost 5 — `stages/` files may not name a `*Dto`

`contract/contractGuard.ts:57` guards `stages`, `layers` and `ui`. A board view type therefore goes in
`contract/types.ts` (additive) **or** repeats the `features/`-local pattern the lawn already uses.

Cheap to satisfy and expensive to discover late — the guard fails the build, not the review, and it
fails it after the stage is written.

### 5. Turn-based interaction, and the boundary that must not blur

Owner decision (round 6, option 1): *"homm3 and other game have different turn for world map and each
battle, explicit boundary."*

> ### ⛔ Map step = **turn**. Battle step = **round**. Never convert between them.
>
> Stated here in full rather than linked, because a downstream session reads this doc. A siege
> consumes **one world turn** regardless of how many rounds it takes internally, and no UI element
> anywhere may display a round count as a turn count or vice versa.
>
> This is the world/combat seam that already binds the whole repo: combat is stateless between turns,
> combat never writes world state, and world never reads combat internals. A UI that quietly converts
> one into the other re-couples them in the player's mental model, and then in the next feature's
> design.

The HUD shows **rounds** inside the siege and **turn N** as context. Two labels, never one number.

### 6. Played vs auto-resolved — one path

`siege-ai`'s `SiegeIntentSource` takes an optional played-side delegate. The FE **supplies that
delegate**; it does not implement a parallel resolution path.

So *"the player is defending"* and *"nobody is watching"* differ by one nullable field, and the played
siege and the CI-proven siege are **the same code**. A separate interactive resolver would drift from
the auto-resolver within one release, and the divergence would appear as "the replay doesn't match".

### 7. `spec-interactive-turns.md` owns the played seat — consume, do not re-derive

Audit **F4**, the largest scope overlap found. T6/T10/T11 — the interactive dwell, the timeout, the
commitment window — belong to that spec. **Read it before implementing this module** and consume its
machinery.

`BattleModeProfile.RequiresLiveInput` already exists (shipped by T6/B21) and the `siege` row sets it.
That is the seam; do not build a second one.

### 8. Entering and leaving

**Enter:** from the world stage, on a sector with an assault available. The world stage stays mounted
underneath — GG-1's *"closed back to the same state"*.

**Leave:** the siege resolves → `BattleOutcome` → the world turn advances → back to `#/world` with the
result surfaced as a toast plus a report entry. Leaving mid-siege is **not** a withdrawal — `Withdrawn`
is an in-battle action a unit takes, not a navigation event. Closing the tab must not surrender.

---

## Tunables

None. Presentation constants are module-local (see `board-render`).

## Numeric types

TypeScript. Structure HP arrives from the server as a **`long`** and must not lose precision:
`WorldSlot.StructureHp` can exceed `Number.MAX_SAFE_INTEGER` under a sufficiently scaled `contentScale`.

**Carry HP as a string or `bigint` across the wire and format for display** — never parse into a
`number` and back. This is the FE half of `CLAUDE.md`'s overflow rule, and it is the half that is
easiest to forget because JavaScript will not complain.

## Boundaries

**Always:** six rows, zero shell branches · copy `world`'s subdirectory shape · rounds and turns
labelled separately · consume `spec-interactive-turns.md` · lazy-load the stage.

**Ask first:** a seventh shell integration point · changing the route shape.

**Never:** convert rounds to turns or display one as the other · a second resolution path for played
sieges · `if (stage === "siege")` in shell code · parse a `long` HP into a JS `number` · treat
navigation as a withdrawal.

---

## Testing

| Test | Asserts |
|---|---|
| `Stage_count_assertion_is_five` | cost 1, and the check that fails first if the amendment is half-applied |
| `GG7_reachability_matrix_has_a_siege_row` | cost 1's second half |
| `Shell_has_no_stage_specific_branch` | source scan for `=== "siege"` in `src/shell/` |
| `Route_round_trips_with_open_layers` | `#/siege/abc?layer=structures` |
| `Esc_pops_one_layer_and_returns_to_the_same_board_state` | GG-1 |
| `World_stage_state_survives_a_siege` | mounted underneath |
| `Rounds_and_turns_are_never_the_same_number` | the boundary, as a test |
| `Played_and_auto_resolved_sieges_use_one_path` | same resolver, delegate present vs null |
| `Leaving_mid_siege_is_not_a_withdrawal` | the tab-close case explicitly |
| `Structure_hp_survives_as_bigint` | a value above `MAX_SAFE_INTEGER` renders exactly |
| `Siege_stage_is_lazily_loaded` | the entry-chunk budget |
| `Keyboard_reaches_every_siege_action` | GG accessibility |
| `No_Dto_named_type_under_stages` | §7 cost 5 — `contractGuard.ts:57` |
| `No_targeting_ui_exists` | §5.20's ⛔: *"configurability is a convenience; statability is the requirement"* |
| `IA_docs_name_five_stages` | a docs assertion, so cost 2 cannot be silently skipped |

## Success criteria

1. All three amendment costs discharged: 20 CI checks re-scoped, IA + principles docs corrected, the
   two-unbuilt-ids note recorded.
2. Six shell rows, zero branches.
3. Played and auto-resolved sieges run the same resolver.
4. Rounds and turns never conflated, asserted.
5. `long` HP survives the wire without precision loss.
6. Entry chunk unchanged.

## Open questions

**None.** ✅ **Decision 41** — the engine supports pause. ✅ **Decision 46** — and it is a **persisted
decision log**, not a session held in memory.

> *"pause the game, this is single play game, the engine should be support pause."* — decision 41
>
> *"we won't store battle state? maybe it correct in heroes of might and magic and other game? they
> have reason for it, maybe we should follow."* — decision 46

A closed client **pauses**. It does not auto-resolve, does not forfeit, and **stores no battle state.**

### ⛔ Why the first draft of this section was wrong

It held the session in the server process and then had to invent a clause — *"a pause must never
survive a world-turn boundary"* — to keep §2 rule 7 (*"never a battle paused in memory"*) true. That
clause worked, but it was **load-bearing scaffolding around a mechanism that did not need to exist.**

**HoMM3's reason for refusing a mid-battle save is that a battle is re-derivable from its inputs.** It
is not squeamishness about memory. Games that *do* persist tactical state — XCOM, Fire Emblem, AoW4 —
are ones where the tactical layer **is** the game. Ours is not; §2 rule 7 says so, and decision 24
already borrowed HoMM3's turn/round boundary for the same reason.

### The shape: `(setup, seed, trace)` → replay

```text
pause  →  persist the DECISION LOG          (input, not state)
resume →  replay it                          (the exact board, byte-identically)
```

**Every piece is already built, and one is missing:**

| Piece | Evidence | State |
|---|---|---|
| `DecisionTrace` | `(Tick, ActorKey, ActionId, TargetKey, Source)`, ordered by `(Tick, Seq)`, replay cursor, `ReplayExhausted` | **Built** |
| The replay driver | `InteractiveIntentSource`'s replay constructor — *"read the trace, never the player … a completed trace reproduces its battle byte-identically, and an AFK timeout replays as the same timeout"* | **Built** |
| The column | `RpgStore.cs:603` `EnsureColumn(…, "decisions_json", "TEXT")`, read at `RpgStore.WebMatches.cs:180` | **Built and read** |
| **A writer** | — | ⚠️ **MISSING** — §3.7: *"`DecisionsJson` is read and never written … a column, a reader and a guard with no producer"* |

`DecisionTrace`'s own doc comment names this exact failure as the reason it exists:

> *"**Appended per decision, never written at the end.** A trace produced only on completion is
> worthless for the failure it exists to cover — **a disconnect mid-battle** would leave a row that
> still *looks* auto-resolvable, and the boot sweep would re-resolve it with AI decisions, **silently
> overwriting a player's real result.** That is the hole T6 must not ship without."*

### ⛔ Scope — the writer is NOT this program's

Audit **F4**: *"the played seat is another program's … **consume T6/T10/T11, never re-derive**."*
`spec-interactive-turns.md` owns T10 and `decisions_json`.

**This module consumes it, and names it a prerequisite** — stated so it is not discovered at landing:

> **Prerequisite:** a `decisions_json` **writer**, appended per decision. Without it a paused siege
> cannot resume, and — worse, per the comment above — the boot sweep may overwrite a played result
> with an AI re-resolve. **That risk exists today, for every played battle, independently of this
> program.**

### What this buys, against the in-memory version it replaces

| | In-memory pause (superseded) | Decision 46 |
|---|---|---|
| §2 rule 7 | true **only** under an added clause | **unconditionally true** — no battle is in memory |
| §2 rule 8's save model (*"`(seed, template, command log)`"*) | not used | **is exactly this** |
| Survives a server restart | ❌ no | ✅ **yes** |
| Mechanism added | a `Paused` session state + a turn-commit gate + a timeout suppressor | **none** — closes a wiring gap |
| Pass 4's *"pause must not survive a turn boundary"* clause | required | **moot, and removed** |

### What still holds from decision 41

- The **world turn does not commit** while a siege is unresolved. Not because a session is held, but
  because the engagement has not produced an outcome — which was always the real reason.
- **No timeout fires on a paused siege.** `ConsecutiveTimeouts` is an interactive-multiplayer concept;
  this is single-player. A `Timeout` in the trace is *"a real decision, not an absence"* and must never
  be manufactured by a player simply being away.
- `MayWriteResult => State == Live && Completed` is **unchanged and still correct** — a partial trace
  never writes a result.

### Tests

| Test | Asserts |
|---|---|
| `A_paused_siege_stores_no_board_state` | **decision 46** — scan the persisted row for cells, HP, initiative |
| `Resume_replays_the_trace_to_the_exact_board` | byte-identical |
| `Resume_survives_a_server_restart` | the thing the in-memory version could not do |
| `An_afk_timeout_replays_as_the_same_timeout` | `InteractiveIntentSource`'s own contract |
| `A_partial_trace_never_writes_a_result` | `MayWriteResult` |
| `The_boot_sweep_never_re_resolves_a_traced_battle` | the hole `DecisionTrace`'s comment names |
| `The_world_turn_does_not_commit_while_unresolved` | decision 24's boundary |
| `No_timeout_fires_on_a_paused_single_player_siege` | |
| `Decisions_json_has_a_writer` | ⚠️ **the prerequisite** — and a companion asserting it had none before |
