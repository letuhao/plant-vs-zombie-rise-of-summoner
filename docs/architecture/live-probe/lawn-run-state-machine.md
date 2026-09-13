# Lawn run state machine

**Status:** binding reference for `GET /api/debug/lawn/state` and `POST /api/debug/lawn/quick-start`.
Referenced from [live-probe-standard.md](../../contributing/live-probe-standard.md) §6.

**Why this exists.** 2026-09-14: a session spent hours reactively patching `/lawn/quick-start` for
one symptom at a time (seed-picker screen, `board.start` never firing, cycling matches, defeat state)
without ever building a complete map of the lawn's real lifecycle. The result: a state-reporting
endpoint (`/lawn/state`) that itself gave a confusing, stale-looking answer, because it was built from
three signals chosen ad hoc rather than a full inventory of what the injector actually emits. This
document is that full inventory, turned into a state machine — built once, so the next fix extends a
model instead of adding a fourth ad hoc special case.

**Ground truth only.** Every row below is cited to a real Harmony hook or `Emit` call in
`src/FusionRpg.Injector/GameHooks.cs` / `GameCaptureHooks.cs`, verified by reading the code, not
inferred from behavior. Where the vanilla game's own internal state is unknown without decompiling
the IL2CPP assembly, this document says so rather than guessing.

## 1. Every real lifecycle signal (the inputs)

| Kind | Source (file:line) | Real trigger | Reliability |
|---|---|---|---|
| `board.start` | `Board.Awake` postfix, `GameHooks.cs:525-565` | Game constructs a `Board` component | **Unreliable — can never fire.** Confirmed live 2026-09-14 on profile `pvzrh-3.9`: zero `board.start` events across a session with multiple full match cycles. Even when it fires, may arrive late (`board.economy` can predate it). |
| `board.end` | `Board.Die` postfix, `GameHooks.cs:577-597` | Game tears the board down — fires for win, lose, **and** restart alike | Reliable as "a board just ended," **not reliable as which outcome** — that's `match.result`/`match.lose`/`match.win`'s job. 3+ in 10s is itself a failure signal (`Cycling`, §3). |
| `match.result` | `BoardStatistics.GameOver` postfix, `GameHooks.cs:599-609` | Game calls `GameOver(result)` with `result != GameResult.None` | The most authoritative terminal signal; typically fires **before** `board.end`. Payload's `result` field is the enum name lowercased (`"defeat"` confirmed live; other values not yet observed in this repo's own testing). |
| `match.lose` | `GameLose.HandleGameLose` postfix, `GameHooks.cs:611-618` | Game's defeat-handling entry point | Reliable boolean pulse, empty payload — no result string, just "a loss happened." |
| `match.win` | `BoardVictory.Win` postfix, `GameHooks.cs:620-627` | Game's victory-handling entry point | Same shape as `match.lose`, for a win. |
| `board.economy` | `PollBoard()`, `GameHooks.cs:426-437` | Polled every tick; emits on `sun`/`money`/`points` change | **Gated on `Board != null`.** Silent for the entire seed-picker window (Board not yet constructed) — this is the single biggest reliability gap in the whole surface. When present and fresh, it is the best "a match is genuinely running" heartbeat available. |
| `level.name` | `PollBoard()`, `GameHooks.cs:444-449` **and** `InGameUI.SetLevelName` postfix, `GameCaptureHooks.cs:200-208` | Same `Board != null` gate as `board.economy` (poll path); the push path fires whenever the game sets the UI level-name text | Same gate/reliability as `board.economy`. Two independent emitters can both fire — harmless duplication, not two facts. |
| `catalog.zombies` | `InitZombieList.InitZombie` postfix, `GameCaptureHooks.cs:899-929` | Game initializes a level's zombie spawn list | **Fires before `Board.Awake`** — during level setup, likely while the seed-picker screen is being built. The earliest real signal that a level entry has begun. Read via a bounded lookback window (`FindLatestKind`'s 2000-event scan) — can scroll out of range in a long-running server session. |
| `card.bank` | `InitBoard.CreateCard(...)` postfix ×3, `GameCaptureHooks.cs:502-547` | Game builds each selectable plant/function/zombie card into the seed-picker's card bank | Fires once **per card** (a burst, not a single event) while the seed-picker screen is being constructed. Closest passive signal to "the seed-picker is up," but nothing in this codebase currently treats a `card.bank` burst as that fact — named here as an available, unused signal. |
| `match.restart` | `PauseMenu_Btn.Restart` postfix, `GameCaptureHooks.cs:768-772` | Player clicks the in-game pause menu's Restart button | Emitted, but `MatchRuntime.Apply` (`src/FusionRpg.Core/Match/MatchRuntime.cs:56-104`) does not read this kind at all — it has no effect on the injector's own FSM and is not consumed by the debug endpoints either. Present on the wire, dead everywhere else. |
| `injector.hello` | Emitted once per injector process start | New game process (or reconnect) | Used only to invalidate a stale `board.start` from a dead process (`FindLatestLiveBoardStart`, `DebugEndpoints.cs:843-882`). |
| `debug.setup.skip` (ack) | `SkipSetup`, `DebugActions.cs:1305-1368` | **Active probe only** — a `POST /api/debug/setup/skip` call | Its `ready`/`board`/`ui` fields describe `InitBoard`'s state **at the moment the command arrived**, not the result of the dismiss action. `InitBoard.Instance is null` is itself informative (proves we are *not* on the seed-picker), but a `NullReferenceException` from `QuickInGame()` can also occur when `InitBoard` exists but is stale (e.g. mid-defeat-overlay) — both read as `ok:false` and must not be conflated. |

### Signals that do not exist (confirmed gaps, not oversights to silently assume away)

| Transition | Why it's invisible |
|---|---|
| Seed-picker screen appears | No dedicated event. `card.bank` bursts are the closest available proxy, unused today. |
| Seed-picker dismissed → real match confirmed running | Neither `debug.setup.skip`'s ack nor `board.start` reliably proves this; only a `board.economy` tick (which needs a sun/money/points change to fire at all) does. |
| Return to main menu | `UIMgr.BackToMenu` is hooked (`GameCaptureHooks.cs:794-799`) but emits nothing — only clears an internal pause flag. No `menu.*` event kind exists anywhere. |
| Defeat/victory overlay dismissed by the player | `match.lose`/`match.win`/`match.result` fire when the game decides the outcome; nothing fires when the player clicks past the resulting overlay. `debug.reset-board` can clear entities at the simulation level and never touches this overlay. |
| Pause / resume | `UIMgr.EnterPauseMenu`/`BackToGame`/`InGameUI.PauseGame` only flip an in-process `MatchPhase` (`MatchRuntime.NotifyPaused`) that is never itself emitted as an event. A paused match reads identically to a running one from outside the injector. |

## 2. The state machine (the outputs)

Six states, computed by strict precedence (top wins) from the signals above, over a bounded recent
window — never from "does this event exist anywhere in history."

```
Cycling            <- recentBoardEnds(10s) >= 3
Defeated           <- latest of {match.result:"defeat", match.lose} newer than latest board.economy
Victorious         <- latest of {match.result:not-defeat, match.win} newer than latest board.economy
InMatch            <- board.economy (or level.name) younger than 30s
LevelEntryPending  <- catalog.zombies newer than any board.economy and newer than any terminal signal
Unknown            <- none of the above
```

| State | Meaning | Confidence |
|---|---|---|
| `Cycling` | Board is rapidly starting/ending in a loop (e.g. a "quick" setup-skip with no real plants placed, losing every wave instantly) | High — count-based, not a single fragile timestamp |
| `Defeated` | Most recent terminal signal says loss, nothing newer proves recovery | High on the *simulation* fact; **zero confidence on whether the visual overlay is still up** (see gaps table) |
| `Victorious` | Most recent terminal signal says win | Same caveat as `Defeated` |
| `InMatch` | A live match is genuinely running | High — `board.economy`/`level.name` require `Board != null`, so this cannot be a seed-picker false positive |
| `LevelEntryPending` | A level is being set up; `Board` likely not yet constructed | **Medium — this is an inference, not a certainty.** `catalog.zombies` fires before `Board.Awake` in every observed case, but nothing proves the seed-picker specifically (vs. some other pre-Board setup step) without an active `debug.setup.skip` probe |
| `Unknown` | No recent signal of any kind | **Zero confidence — cannot distinguish main menu, a frozen game, or a crashed injector.** Never report this as "idle" or "safe to proceed"; it is an honest "no data," not a state |

**Precedence rationale:** `Cycling` must win over everything else because a cycling board makes every
other signal's timestamp meaningless (it churns every 1-3 seconds). `Defeated`/`Victorious` must win
over `InMatch` because a terminal signal can arrive in the same tick window as a stale `board.economy`
read; ordering by timestamp (not "does a terminal event exist at all") is what makes this correct —
proven by the regression test `BoardEconomyAfterDefeat_reportsInMatch_defeatIsStale`, which asserts the
reverse case: a **later** `board.economy` must override an **earlier** defeat.

## 3. What this state machine explicitly does not know

Restated from the gaps table, because it is the rule most likely to be forgotten under pressure:

- **It cannot see the rendered screen.** `Defeated`/`Victorious` describe the simulation's own record,
  never whether the corresponding overlay is still blocking the view. `debug.reset-board` can make the
  simulation `InMatch`-eligible again without the overlay ever going away.
- **`Unknown` is not "safe."** It covers the main menu, the seed-picker screen, a paused match, and a
  frozen or crashed injector, all identically. Do not narrow it by assumption — narrow it only by
  adding a real signal (§4) or falling back to an active probe / asking a human.
- **A paused match reads as `InMatch`.** There is no signal at all for pause/resume today.

## 4. Extending this model

When the next live-run mistake is found, the fix is never "add another special case to
`/lawn/quick-start`" — it is: does a real signal already exist for this (add it to §1 and wire it into
§2), or does no signal exist at all (name it in §3 as a gap, and if it matters enough, that is a real
injector-side feature request — a new `Emit(...)` call on the relevant hook — not something to fake
from existing signals). Candidates already identified and not yet wired: `catalog.zombies`-based
`LevelEntryPending` narrowing (this doc's own contribution — not yet in `/lawn/state`'s shipped code
as of the date this doc was written; check the endpoint's own source for whether it has landed),
`card.bank` bursts as a seed-picker-under-construction signal, and `match.lose`/`match.win` as a
cross-check against `match.result`'s `result` string.
