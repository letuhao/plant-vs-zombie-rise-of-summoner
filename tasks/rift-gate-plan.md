# Plan: `rift-gate`

**Status:** **approved 2026-09-15** (owner). Phase 2 (Plan) complete; implementation is authorized via `/build full`.
**Map:** [../docs/architecture/rift-gate-map.md](../docs/architecture/rift-gate-map.md) (approved 2026-09-15; **Decision 17 adds the owner's "never fight the PVZ engine" rule**)
**Ideal:** [../docs/architecture/rift-gate-ideal.md](../docs/architecture/rift-gate-ideal.md)
**Specs:** `docs/architecture/rift-gate/spec-{menu-anchor,tombstone,overlay-hide,first-open-signal,entry-landing}.md`
**Todo:** [rift-gate-todo.md](rift-gate-todo.md)

> **Read the map's Audit section *and* Decision 17 before this plan.** The first draft of the map was
> wrong in three places, and its corrections are load-bearing: `overlay-hide` is a relay through the
> **existing** command path (not a new FE→host transport); `first-open-signal` is written
> **FE/server-side** because the injector cannot see the launcher's F10 and the player row already
> exists; and **`menu-anchor` is a menu-screen *presence* signal (Decision 17), never a navigation
> static patch**. Decision 17 supersedes Gap 10's six-exit-edge mechanism:
> `UIMgr.BackToMenu`/`EnterMainMenu` are **forbidden** — a documented live hazard
> (`DebugActions.cs:1495-1496`), and calling them mid-run breaks a live run. The rule, verbatim:
> **switch the WebView; never fight the PVZ engine.**

---

## What this program is

A PVZ-game sprite — a cracked tombstone — that opens the web FE, plus the landing contract that makes
the first open meaningful. **The transport already ships** (WebView2, both host modes, owner-tested,
release-packaged with probe gates). This program adds **where the affordance lives**, **how the page
asks the host to close**, and **what the first open means**. No module here may re-litigate host
selection, the show/hide lifecycle, or release packaging, and **no module may drive the engine's menu
state**.

## Module dependency graph

```text
                    menu-anchor ──────────► tombstone
                                  (needs the menu-screen presence signal + the placement contract)

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

**Why first:** it is the only module that produces a signal other modules consume. It is small (one
lifecycle patch, one new Core class, one tuning group), and under Decision 17 its real work is
**proving the presence semantics** — the signal is set by the menu screen's own `Start` and cleared by
its own hide/exit, with the type guard being the load-bearing half.

- **Patch the menu screen's lifecycle, not a navigation static**: `[HarmonyPatch(typeof(MainMenu),
  "Start")]` (string name — `Start` is **private** in 3.9) → `Set(MainMenu)`; `BaseMenu`'s
  `OnHide`/`OnExit` postfixes, guarded by `__instance is MainMenu` → `Clear(MainMenu)`.
- The closed surface enum + pure presence carrier in Core (the `OverlaySwitchState` shape).
- **Task 1 is a one-time verification, not a risk to carry.** Per the owner, *no one redesigns a
  main-menu hierarchy between versions*: Task 1 confirms **once** against the real 3.9 interop that
  `MainMenu` exposes the patch target and that the hierarchy paths resolve. Already verified in the spec
  session (`spec-menu-anchor.md`'s "Verified in this session"): the type, the `Start` method, and the
  lifecycle callbacks. What Task 1 finishes is the live path resolution.
- **MelonLoader-first, BepInEx secondary — settled.** MelonLoader is the deployed default
  (`deploy-play.ps1:39,115`); BepInEx is *"the older 3.8.1 install, kept for BepInEx-specific testing"*
  (`:5`). If the BepInEx interop lacks `MainMenu` (checked when that host is touched), the tombstone is
  Melon-only and BepInEx keeps **F10** — a scoped difference, stated plainly, not a design input.
- The `riftMenu` tuning group (authored by hand once, then publishable — `publish.py:130-131`).
- A **red guard** asserts no rift-gate source calls a `UIMgr` navigation method.

**Checkpoint A:** the signal reads `MainMenu` after the screen's `Start`; it clears on the screen's
own hide/exit; a **non-`MainMenu`** `BaseMenu` hiding does **not** clear it; the no-navigation guard is
red-green; the `riftMenu` group loads or rejects by name; the live hierarchy paths are resolved.

---

## Slice 2 — three parallel streams

### Stream 2a — `tombstone` (needs slice 1)

- **Attach a real uGUI `Button` into the menu's own hierarchy** (the owner's reference,
  `darkthemer/PvZF_MainMenuFlowers`): a child `GameObject` with a stretched `RectTransform`, an `Image`
  with `raycastTarget = true`, and a `Button` with `transition = None`, `targetGraphic = null`, whose
  listener calls `OverlaySwitch.RequestToggle()`.
- **Decision 18 — the menu art moves to uGUI too**: the Rift art becomes a `Sprite` on an `Image` under
  the menu transform, so the menu has **one** rendering system and the art inherits the game's canvas
  scaling. The IMGUI `RiftMenuOverlay` draw path is retired **for the menu**. The in-match "RPG" button
  (decision 15) **stays IMGUI** — that split is deliberate. Fallback **(a)** (keep the paint, add the
  uGUI node over the same position) is the documented alternative if the move proves not small.
- **The honest gap is `Sprite.Create` + the affordance node** — everything else is already built here:
  clicking a real game `Button` (`ControlClick.cs:150-153`), censusing the uGUI hierarchy
  (`ControlInspect.cs:294-298`), `UnityEngine.UI` in every host's csproj, PNG→`Texture2D`
  (`RiftMenuOverlay.cs:112-118`), `Sprite`→PNG (`TypeIconCapture.cs:158`). The specs cite these so a
  later session does not re-derive uGUI plumbing.
- **The hand-derived hit box and the never-cover policy are deleted** — the game's own
  layout/raycast owns hit-testing, and "do not eat a host click" becomes sibling order
  (`SetAsFirstSibling`) + clearing `raycastTarget` on labels we would obstruct, exactly as the reference
  does.
- **No `ClassInjector`**: verified it appears nowhere in this repo, and a static listener avoids it.
- No added `OnGUI` draw lines; the first draft's two are **gone** (the existing menu-art draw sites are
  removed with the IMGUI path).
- **Gap 6 fix** in `OverlaySwitch.cs:111-117`: the injector-host view start stops keying off the button
  preference once the menu entry is independent.
- The in-match **"RPG" button restyle** (decision 15): presentation only; one action preserved; the
  guard/test updated, not bypassed; **stays IMGUI** (decision 18).
- **MelonLoader-first:** the tombstone ships on the deployed default; BepInEx keeps **F10** if its
  interop lacks `MainMenu` — stated, not assumed.

**Checkpoint B:** main-menu click opens the FE, identical to F10; a **real menu button beside it still
works** (no stolen click); the affordance is absent in a match, on the pause menu, and on every other
screen; the menu has **one** rendering system (uGUI) while the RPG button stays IMGUI; the no-navigation
guard is green; no `OnGUI` line was added; pipe guard still green.

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
| **Driving the engine's menu state** (`menu-anchor`) | Decision 17: `UIMgr.BackToMenu`/`EnterMainMenu` are a documented live hazard (`DebugActions.cs:1495-1496`) and calling them mid-run breaks a live run | Patch the screen's **own lifecycle**; a **red guard** asserts no rift-gate source calls a `UIMgr` nav method (todo T1/T2) |
| **A presence signal that never clears** (`menu-anchor`) | The screen is *hidden*, not destroyed — verified: `BaseMenu` declares `OnHide`/`OnExit` but **no** `OnDestroy` | Clear on `BaseMenu`'s hide/exit callbacks, **type-guarded** so a submenu hiding cannot clear the main menu (todo T2) |
| **Introducing class injection for no need** (`tombstone`) | The reference uses `ClassInjector` because of its lambda capture; `ClassInjector` appears **nowhere** in this repo today | Build a plain uGUI `Button` with a **static** listener; introducing `ClassInjector` is Ask-first (todo T4) |
| **Eating a host menu click** (`tombstone`) | An overlay UI element can steal a click the game wanted (old gap 7) | Order below the game's primary buttons + clear `raycastTarget` on labels we obstruct — the reference's own fix; confirmed live on a real button (todo T4) |
| **Two rendering systems for the menu** (`tombstone`) | Decision 18: the art is IMGUI and the affordance is uGUI; leaving both is the defect the decision removes | Move the art to a uGUI `Sprite`/`Image`; a guard asserts the menu no longer paints via IMGUI while the RPG button keeps it (todo T4) |
| **One Esc burns the prologue** (`overlay-hide`) | `RiftPrologueDialog.tsx:131-133` treats dismissal as a durable `skipped` write | Hide writes no ledger; tested at both ends (todo T9) |
| **A second FE→host transport appears** (`overlay-hide`) | The ideal records zero FE→host channel exists; the temptation is WebView2 `postMessage` | Reuse the existing command path + one pipe verb; the pipe guard covers the verb by extension |
| **A third `SendInjectorCommand` copy** | The helper already exists **twice** (`Program.cs:1617`, `UniqueActorService.cs:281`) | Pick `Program.cs`'s; adding a third is Ask-first (todo T8) |
| **The click grows its own toggle path** (`tombstone`) | `MainWindow.xaml.cs:277` pins "no second behavior" | Click routes through `OverlaySwitch.RequestToggle()`; a red pipe guard means it didn't |
| **A population count sneaks into a test** | Repo-wide rule; onboarding/roster numbers grow with content | Every acceptance asserts contracts/enums/joins/structure; the closed `RiftMenuSurface` enum may pin a literal **with its reason** |
| **Injector-mode view never starts** (gap 6) | `OverlaySwitch.cs:111-117` keys the view start off the *button* preference | Fix the wiring: the menu entry is independent; the preference suppresses only the in-match button |
| **A `float` magnitude or an invented tuning key** | Repo invariants; `publish.py` refuses to invent a key | No magnitude exists here; the only tuning keys are the `riftMenu` group + the existing `switchState` |
| **Cross-boundary change runs the wrong suite** | `overlay-hide` touches Contracts+Server+Injector+Launcher+Web | Scoped `verify-change.ps1` per path first; the cross-boundary full run is the **finishing** checkpoint only (AGENTS.md) |

> **Not a risk, by owner decision:** the 3.8.1-reference-vs-3.9-host gap is **one verification step**
> (Task 1), not a hedge the design carries — *no one redesigns a main-menu hierarchy between versions*.
> Likewise BepInEx is **secondary and settled** (MelonLoader is the deployed default,
> `deploy-play.ps1:39,115`); if its interop lacks `MainMenu`, the tombstone is Melon-only and BepInEx
> keeps F10 — stated plainly, not a design input.

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
| Affordance placement (screen percentages) + min device-pixel size + padding | `data/tuning/overlay.v1.json` → `riftMenu` (authored by hand once, then publishable) | `menu-anchor` |
| In-match button geometry | `overlay.v1.json` → `switchLayout` (exists) | unchanged |
| Probe/debounce/send-timeout if the bridge adds any | `overlay.v1.json` → `switchState` (exists) | `overlay-hide` |
| First-open capture pacing | **structural per-frame cap, hardcoded with a comment** — never a tuning key | `first-open-signal` (rule); the capture program implements it |
| Entry landing | **none** — a route decision and a durable boolean | `entry-landing` |

**Deleted by Decision 17 (do not re-add):** the first draft's hit-box tuning keys
(`minimumHitDevicePx`, hit-box padding as a *separate* never-cover policy). The affordance's rect is
owned by the game's `RectTransform`; only its placement/size remain dials.

**Added by Decision 18:** none. The uGUI art node inherits the game's canvas scaling, so the move adds
no tuning key — the same `riftMenu` placement/size numbers position it.

## What this plan does not do

- No story content, no beat authoring, no scene scheduling → `story-scene` (sibling).
- No commander-name change → `commander-surface` (sibling, decision 10).
- No capture mechanism (sweep, traversal, manifest, progress UI) → separate program (decision 1).
- No transport, host selection, show/hide lifecycle, or release packaging change — all shipped.
- **No engine navigation:** no rift-gate module calls a `UIMgr` navigation method. The injector reads
  menu presence and attaches a UI element; it does not drive menu state (Decision 17).
- **No class injection** (`ClassInjector` / `RegisterTypeInIl2Cpp`) unless a plain uGUI `Button` proves
  insufficient — Ask-first, with the reason recorded.
- **No second rendering system for the menu:** the menu art and the affordance are both uGUI
  (Decision 18); the in-match RPG button deliberately stays IMGUI.
- No `decisions.md` lock is created by this program; if implementation finds one is needed (e.g. a
  second pipe verb is judged to lock behaviour), that is an Ask-first stop, not a silent edit.
