# Plan: `rift-gate`

**Map:** [../docs/architecture/rift-gate-map.md](../docs/architecture/rift-gate-map.md) (approved 2026-09-15)
**Ideal:** [../docs/architecture/rift-gate-ideal.md](../docs/architecture/rift-gate-ideal.md)
**Specs:** `docs/architecture/rift-gate/spec-{menu-anchor,tombstone,overlay-hide,first-open-signal,entry-landing}.md`
**Todo:** [rift-gate-todo.md](rift-gate-todo.md)

> **Read the map's Audit section before this plan.** The first draft of the map was wrong in three
> places and the corrections are load-bearing: `overlay-hide` is a relay through the **existing**
> command path (not a new FE→host transport); `first-open-signal` is written **FE/server-side** because
> the injector cannot see the launcher's F10 and the player row already exists; `menu-anchor` is a
> **patch extension** over an existing idiom, whose real work is the **exit edges** of a state signal.

---

## What this program is

A PVZ-game sprite — a cracked tombstone — that opens the web FE, plus the landing contract that makes
the first open meaningful. **The transport already ships** (WebView2, both host modes, owner-tested,
release-packaged with probe gates). This program adds **where the affordance lives**, **how the page
asks the host to close**, and **what the first open means**. No module here may re-litigate host
selection, the show/hide lifecycle, or release packaging.

## Module dependency graph

```text
                    menu-anchor ──────────► tombstone
                                          (needs the reviewed signal + the hit-box contract)

   overlay-hide      (independent) ──► provides the embed marker + the page→host close path

   first-open-signal (independent) ──► provides the durable once-per-player fact
                    │
                    └──(soft)────────► entry-landing ◄──(sibling seams, consume-only)
                                          ▲   ▲
                                          │   └── story-scene   (the scene that plays first — sibling)
                                          └────── commander-surface (the commander name — sibling, decision 10)
```

- **Acyclic.** `menu-anchor` feeds only `tombstone`. `overlay-hide` and `first-open-signal` are
  independent of each other and of everything else. `entry-landing` is the only **join point** — and
  it is a *soft* dependency: it consumes the first-open fact and two sibling seams, and edits none of
  them.
- **The one shared artifact `entry-landing` needs from `overlay-hide`** is the embed marker; the one
  it needs from `first-open-signal` is the fact. Neither is edited by `entry-landing`.

**Build order (from the approved map):**

```text
menu-anchor  →  tombstone  ·  overlay-hide  (parallel)  ·  first-open-signal  →  entry-landing
```

`tombstone`, `overlay-hide`, and `first-open-signal` are independent once `menu-anchor` lands, so
**three streams can run in parallel** after slice 1. `entry-landing` waits for `first-open-signal`
(soft) and for the two sibling programs to have their seams available, which is why it is last.

---

## Slice 1 — `menu-anchor` (everything else's prerequisite)

**Why first:** it is the only module that produces a signal other modules consume. It is small
(one new patch, one new Core class, one new tuning group) but its **exit edges are the real work** —
a state signal that fails to clear is the defect DESIGN-GATE §2.16 names, applied to state.

- `EnterMainMenu` gains the patch beside the existing three (`GameCaptureHooks.cs:780,790,812`).
- The closed surface enum + pure state carrier in Core (the `OverlaySwitchState` shape).
- The **hit-box contract** in `RiftMenuOverlayLayout` (axis-aligned, wobble-independent, min
  device-pixel, never-cover) — `tombstone` depends on this.
- The `riftMenu` tuning group (authored by hand once, then publishable — `publish.py:130-131`).
- Every **exit edge** tested individually: match / submenu / pause / modal / lose / quit.

**Checkpoint A:** signal reads `MainMenu` on the main menu; **every** exit edge clears to `None`, one
test each; hit box stable at `(0,0)`/720p/1080p/4K; pause menu excluded by name; `riftMenu` group loads
or rejects by name.

---

## Slice 2 — two parallel streams

### Stream 2a — `tombstone` (needs slice 1)

- The interactive sibling (`RiftMenuTombstoneGui`) with `OverlaySwitchGui`'s event filter, gated on
  the reviewed signal, clicking through `OverlaySwitch.RequestToggle()`.
- `RiftMenuOverlay`'s **paint** gate stays broad (the ideal keeps paint broad); the **click** gate is
  the reviewed signal. The seed-picker/Almanac paint defect is corrected for clicks.
- **Gap 6 fix** in `OverlaySwitch.cs:111-117`: the injector-host view start stops keying off the button
  preference once the menu entry is independent.
- The in-match **"RPG" button restyle** (decision 15): presentation only; one action preserved; the
  guard/test updated, not bypassed.
- One added `OnGUI` call line per host (`Plugin.cs:39-42`; `MelonFusionRpgMod.cs:76-79`).

**Checkpoint B:** main-menu click opens the FE, identical to F10 in both host modes; no hit target on
any non-reviewed surface; pipe guard still green (no new verb); button still one action.

### Stream 2b — `overlay-hide` (independent)

- The named vocabulary constant (`OverlayCommandNames.Hide`) — not a bare literal in the drain.
- Server endpoint via the **existing** `SendInjectorCommand` (`Program.cs:1617`); **no third copy**.
- Injector drain case → `OverlayViewHost.Hide()` (injector) or `OverlaySwitch` pipe `hide` (launcher).
- Launcher pipe: `OverlayPipeCommand.Hide` + `ParseCommand` row + `HideOverlayToGame()` case.
- FE **Leave** control + embed marker; no marker → no Leave.
- **`docs/launcher/overlay-spec.md` updated in the same commit** (required deliverable).
- **Hard contract:** hide writes **no** story ledger — tested at the FE (no ack mutation) and the
  Server (no `rpg_onboarding_story` change).

**Checkpoint C:** Leave hides and refocuses the game in **both** host modes; the pipe guard covers the
new verb by extension; no embed marker → no Leave; hide never acknowledges the story.

### Stream 2c — `first-open-signal` (independent)

- The durable once-per-player row (`INSERT OR IGNORE`, `player_id` key), mirroring `OnboardingStoryRow`.
- `POST`/`GET /api/onboarding/{playerId}/first-open` on the existing group.
- Actionability = the server's own `InjectorConnected` (`RpgStore.cs:1059-1060`).
- The injector consume arm at the **existing** cadence — no new poll.
- FE one-shot write; never blocks; retried next load.
- **No capture mechanism** (separate program); capture pacing recorded as a structural per-frame cap
  with a comment, never a tuning key.

**Checkpoint D:** the fact records once, durably; a second load is a no-op; actionability flips on
injector connect; the FE is never blocked; no capture is claimed.

---

## Slice 3 — `entry-landing` (the join)

- A FE-side landing decision (`shouldLandOnSanctum`) + a one-shot effect; once per player.
- Land on `/sanctum` on first open — the surface that **already** mounts the story scene
  (`SanctumStage.tsx:85-89,260-265`), so the trigger is not duplicated.
- **Never creates a player** (`SeedPlayerIfEmpty` already does, `RpgStore.cs:3865-3878`).
- **Consumes** `commander-surface`'s seam and `story-scene`'s scene; edits neither.
- Host keeps navigating the bare origin (`PlaySession.cs:38`).

**Checkpoint E:** first open lands on `/sanctum`; a returning player keeps their route; a plain browser
visit still shows `TitleScreen`; no redirect loop; build + bundle budget pass.

---

## Cross-stream risks and mitigations

| Risk | Why it is real | Mitigation |
|---|---|---|
| **A state signal that never clears** (`menu-anchor`) | The trap DESIGN-GATE §2.16 names, applied to state: the forgotten edge is *any other* surface | List every exit edge, one test each (todo T2); the pause menu is excluded **by name** |
| **One Esc burns the prologue** (`overlay-hide`) | `RiftPrologueDialog.tsx:131-133` treats dismissal as a durable `skipped` write | Hide writes no ledger; tested at both ends (todo T9) |
| **A second FE→host transport appears** (`overlay-hide`) | The ideal records zero FE→host channel exists; the temptation is WebView2 `postMessage` | Reuse the existing command path + one pipe verb; the pipe guard covers the verb by extension |
| **A third `SendInjectorCommand` copy** | The helper already exists **twice** (`Program.cs:1617`, `UniqueActorService.cs:281`) | Pick `Program.cs`'s; adding a third is Ask-first (todo T8) |
| **The click grows its own toggle path** (`tombstone`) | `MainWindow.xaml.cs:277` pins "no second behavior" | Click routes through `OverlaySwitch.RequestToggle()`; a red pipe guard means it didn't |
| **A population count sneaks into a test** | Repo-wide rule; onboarding/roster numbers grow with content | Every acceptance asserts contracts/enums/joins/structure; the closed `RiftMenuSurface` enum may pin a literal **with its reason** |
| **Injector-mode view never starts** (gap 6) | `OverlaySwitch.cs:111-117` keys the view start off the *button* preference | Fix the wiring: the menu entry is independent; the preference suppresses only the in-match button |
| **A `float` magnitude or an invented tuning key** | Repo invariants; `publish.py` refuses to invent a key | No magnitude exists here; the only tuning keys are the `riftMenu` group + the existing `switchState` |
| **Cross-boundary change runs the wrong suite** | `overlay-hide` touches Contracts+Server+Injector+Launcher+Web | Scoped `verify-change.ps1` per path first; the cross-boundary full run is the **finishing** checkpoint only (AGENTS.md) |

## Verification strategy

- **Default:** `scripts/verify-change.ps1 -Paths <changed files> -Session <build session>` for every
  task. Boundaries exist for `src/FusionRpg.Core/**`, `src/FusionRpg.Injector/**`,
  `src/FusionRpg.Launcher/**`, `src/FusionRpg.Data/**`, `src/FusionRpg.Server/**`,
  `src/FusionRpg.Contracts/**` (`verification-boundaries.v1.json:23-33`).
- **Injector tasks:** build `src/FusionRpg.Injector.MelonLoader.39` with real interop — CI cannot build it.
- **The one cross-boundary change** (`overlay-hide`): scoped selection per path, then a full local run
  at the end as the finishing checkpoint (AGENTS.md's three cases: a change crossing module boundaries).
- **Web-only tasks** (`entry-landing`, the FE half of `overlay-hide`/`first-open-signal`): run vitest +
  `npm run build` + `npm run check:bundle` directly; the FE verification mapping is a known gap (F1).
- **Live probes** at each checkpoint that has a live half, per `live-probe-standard.md` §3: read the
  changed state back through the normal path, never a response body alone.

## Tunables summary

| Number | Home | Owner module |
|---|---|---|
| Tombstone placement/size percentages, min hit-target device px, hit-box padding | `data/tuning/overlay.v1.json` → `riftMenu` (authored by hand once, then publishable) | `menu-anchor` |
| In-match button geometry | `overlay.v1.json` → `switchLayout` (exists) | unchanged |
| Probe/debounce/send-timeout if the bridge adds any | `overlay.v1.json` → `switchState` (exists) | `overlay-hide` |
| First-open capture pacing | **structural per-frame cap, hardcoded with a comment** — never a tuning key | `first-open-signal` (rule); the capture program implements it |
| Entry landing | **none** — a route decision and a durable boolean | `entry-landing` |

## What this plan does not do

- No story content, no beat authoring, no scene scheduling → `story-scene` (sibling).
- No commander-name change → `commander-surface` (sibling, decision 10).
- No capture mechanism (sweep, traversal, manifest, progress UI) → separate program (decision 1).
- No transport, host selection, show/hide lifecycle, or release packaging change — all shipped.
- No `decisions.md` lock is created by this program; if implementation finds one is needed (e.g. a
  second pipe verb is judged to lock behaviour), that is an Ask-first stop, not a silent edit.
