# Capability map: `rift-gate`

**Status:** Phase 0 — **approved 2026-09-15** (owner). Audit corrections and both follow-up decisions
recorded below; module specs are unblocked.
**Program id:** `rift-gate`
**Ideal:** [rift-gate-ideal.md](rift-gate-ideal.md) (idea phase complete 2026-09-15; 14 decisions)
**Sibling programs (not touched):** `story-scene` (own worktree, in `/build full`) ·
`commander-surface` (owns the commander-name seam) · `game-control` (implemented)

This map was **audited against the code before being proposed**, and the first draft was wrong in
three places. The corrections are recorded in "Audit" at the bottom, because a later session that
re-derives the same wrong shape pays for the same hour twice.

---

## What this program is

**A PVZ-game sprite — a cracked tombstone — that opens the web control room.** Click it (or press the
existing global **F10**) on the PVZ main menu and the web FE opens over the game; the FE's own
**Leave** control closes it again. On the first open the player lands at the normal entry point for a
new save, and a durable "first open" fact is recorded for the capture program to consume.

**The transport already ships.** The WebView2 overlay (launcher-hosted and injector-hosted) is built,
owner-tested, and carried by the player release with probe-gated packaging
(`PlayerPackProbe.ProbeOverlayPayload`). F10 already opens it from the main menu in both host modes
(`MainWindow.xaml.cs:270,313-352`; `OverlaySwitch.cs:113-117`, `OverlayViewHost.cs:186`). This program
adds **where the affordance lives**, **how the page asks the host to close**, and **what the first
open means** — it does not build a transport, and no module here may re-litigate host selection,
show/hide lifecycle, or release packaging.

Load-bearing rules restated inline, because a downstream session reads this map and not its links:

- **Every RPG feature lives in the RPG layer; it is never built by changing what PvZ is.** This
  program observes the host menu and reads its state; it never alters host-game behaviour, art, or
  saves.
- **The web FE is never named** (owner decision 2). Player copy says "web FE". "Rift" is reserved for
  a later mechanism. Internal protocol names (`rift.hide`) never surface as player-readable labels.
- **Two state machines, no shared state — only messages.** The menu signal below is a **read of host
  UI state for an affordance**: never a combat/domain input, never on the hit path, never sent to the
  server.
- **A cap on a magnitude is a progression ceiling until proven otherwise.** Nothing here introduces a
  magnitude; capture pacing is a **structural per-frame cap with a comment**, never a `data/tuning`
  number.
- **SOLID is binding.** One SSOT per responsibility; extend by contribution, never a parallel fork.
  The click path routes through the **existing** toggle convergence point
  (`MainWindow.xaml.cs:277` "no second behavior"), and the FE→host close reuses the **existing**
  server→injector command path rather than inventing a second channel.

---

## Module table

| Module id | Responsibility | Depends on |
|---|---|---|
| `menu-anchor` | The **narrow reviewed menu-state signal** ("a named, safe PVZ menu is open") + the **hit-target geometry contract** (stable axis-aligned hit box, minimum device-pixel size, never-cover rule). | — |
| `tombstone` | The **clickable tombstone**: draw on the reviewed surfaces, hit-test, route the click to the **existing** toggle. Also corrects the current art's over-broad draw gate. | `menu-anchor` |
| `overlay-hide` | **The page→host close.** FE **Leave** control → existing server→injector command → injector hides locally (injector-hosted) or relays a new `hide` verb over the existing pipe (launcher-hosted). Must close **without** acknowledging the story. | — |
| `first-open-signal` | The durable **once-per-player "the FE has been opened"** fact, plus the gating that makes it *actionable* (the injector must be connected for capture to run). Trigger only. | — |
| `entry-landing` | The **landing contract**: land at the new-save entry point (`/sanctum`) on first open. The player row already exists; this module does **not** create one. | `first-open-signal` (soft), `story-scene` (sibling), `commander-surface` (sibling) |

**Build order:** `menu-anchor` → `tombstone` · `overlay-hide` (parallel) · `first-open-signal` →
`entry-landing`.

**Dependency direction is acyclic.** `menu-anchor` feeds only `tombstone`. `overlay-hide` and
`first-open-signal` are independent. `entry-landing` is the only join point.

**Module specs:** `docs/architecture/rift-gate/spec-menu-anchor.md` ·
`spec-tombstone.md` · `spec-overlay-hide.md` · `spec-first-open-signal.md` · `spec-entry-landing.md`.

---

## Interfaces at the boundary (recorded here; designed in the provider's spec)

- **`menu-anchor` → `tombstone`:** a signal over a **closed enum of reviewed surfaces** plus "none
  open". The tombstone consumes the signal and never inspects host UI itself. Closed-vocabulary rule:
  the surface list is an enum the code owns and a human reviews, so a **literal pin is correct here**
  with the reason stated — unlike a derived population.
- **`overlay-hide` → `story-scene`:** hide closes the window without touching the story ledger.
  `RiftPrologueDialog.tsx:131-133` currently writes `completed | skipped` on `onOpenChange(false)` and
  `onEscapeKeyDown`; one Esc would otherwise burn the prologue. The story program consumes this verb
  and its map cedes it to rift-gate three times (`story-scene-todo.md:964`: a second channel *"would
  violate one-owner-per-mechanism"*).
- **`first-open-signal` → capture program:** a durable per-player fact with a stable refusal-reason
  vocabulary matching the existing `onboarding.story-*` style. Capture consumes it; nothing about
  capture lives here.

## Cross-program dependencies (this program depends on; it does not edit)

| Dependency | Owner | Why it matters | Status |
|---|---|---|---|
| First commander's display name = player's name | `commander-surface` (decision 10) | `entry-landing` shows the player's commander; the label is hardcoded at `PlayerEmpireCommanders.cs:14` | Seam exists; one-line change + a locked spec row (`spec-commander-list-api.md:94`) + ~7 pinned assertions, owned elsewhere |
| The story scene that plays on first open | `story-scene` (decision 4) | `entry-landing` runs after it | In `/build full` |
| Overlay transport, host selection, release packaging | **already shipped** | Out of scope | Built + owner-tested |

## Out of scope for this initiative

- **The capture mechanism** (text sweep trigger, portrait traversal, manifest, progress UI). Decision
  1: capture is *triggered* by `first-open-signal`; its mechanism is a **separate program**.
- **The story scene** — `story-scene` (decision 4).
- **The overlay transport / host selection / release payload and its probes** — shipped.
- **Combat, derived, or applied magnitudes.** ActorHub gate is **N/A** — this program produces and
  consumes no actor combat/derived number, and must not invent a fold.
- **Any `data/tuning` number** except the `riftMenu` group the ideal names (tombstone
  placement/size), owned as the existing `switchLayout` precedent is.
- **Debug surfaces / MCP tooling.** The tombstone is player chrome, not a debug verb, and must not
  become an agent control surface (`game-control` owns that, shipped).

## Session boundary

`tasks/sessions/rift-gate-idea-20260915-b7e4.json`, mode `direct`, branch
`features/derived-stat-extension`. Fence: `docs/architecture/rift-gate-ideal.md`,
`docs/architecture/rift-gate-map.md`, `docs/architecture/rift-gate/**`, the session record.
`scripts/session-boundary-check.ps1` clean apart from four pre-existing drifts owned by other active
sessions.

---

# Audit — what the first draft of this map got wrong

The owner asked for the debate before the specs. Three corrections and four gaps, each verified
against code this session. **The corrections are the deliverable of this section**; a later session
that skips it re-derives the wrong shape.

## Correction 1 — `overlay-hide` was missing its actual hard part

**What I wrote:** "one verb vocabulary, two implementations… the FE does not care which host owns it."

**Why that is wrong:** it names the two *sinks* and skips the *path*. Both hide sinks already exist —
launcher `HideOverlayToGame()` (`MainWindow.xaml.cs:354`), injector `OverlayViewHost.Hide()`
(`OverlayViewHost.cs:67`), plus Esc in both (`OverlayWindow.xaml.cs:27-31`;
`OverlayViewPolicy.cs:22-23`). What does **not** exist is any way for a **web page** to reach either
one: verified zero `WebMessageReceived` / `postMessage` / host-object channel in `src/` or
`web/fusion-rpg-web/src`, and the page cannot tell it is in an overlay at all.

**The corrected shape — reuse the server→injector command path, with the injector as router:**

1. FE **Leave** → the **existing** server→injector command path (the `Command` SignalR push, with the
   `InjectorCommandInbox` HTTP fallback already specified in `software-architecture.md:147-148`).
2. Injector receives it. **Injector-hosted:** call `OverlayViewHost.Hide()` in-process — done.
   **Launcher-hosted:** relay a new `hide` verb over the **existing** pipe
   (`OverlayPipeServer.cs:14-20`, today `toggle`/`ping`; client `OverlaySwitch.Send`,
   `OverlaySwitch.cs:172`).
3. Launcher's pipe handler routes `hide` to the same `HideOverlayToGame()` the Esc key uses.

**Why this is the better shape, not just the cheaper one:** the injector already owns the pipe client
and is already the thing that knows which host mode is active
(`OverlaySwitch.InjectorHosted`, `:58-59`). A direct FE→launcher channel would be a **second** transport
in a program whose whole premise is that the transport ships — and it would need its own guard, its
own host-mode branching in the page, and a WebView2 message surface in both hosts. The relay reuses one
path and adds one verb.

**And the command drain is already generic, despite its name.** `CheatCommandRunner` is documented as
the *"main-thread drain for SignalR/HTTP cheat commands from the web/server"*
(`CheatCommandRunner.cs:19`), but the vocabulary it dispatches is **not** cheats: the same drain already
runs `reload-stats`, `pvz.stats.reload`, `aptitudes.allocation.reload`, `commander.snapshot.reload`,
`passive-tree.bound-atoms.reload`, and `lawn-deploy.roster.reload` (`:57,63,69,82,88,94`), while the
injector's SignalR handler enqueues them on the same `Command` event (`RpgClient.cs:121`). So
`rift.hide` rides a **server→injector command seam that already carries presentation/refresh commands**,
not a gameplay-cheat surface — the class name is a misnomer, and the spec should say so rather than let
a later reader think the FE is issuing a cheat. The `[cheat-cmd]` log label (`:41`) is the same
misnomer.

**Consequence for the spec:** `overlay-hide` touches **Contracts** (the closed command name),
**Server** (the endpoint that builds it via the existing `SendInjectorCommand` helper — note it exists
**twice**, `Program.cs:1617` and `UniqueActorService.cs:281`; the spec must pick one and say why rather
than adding a third), **Injector** (the drain case + local hide + pipe client verb), **Launcher** (the
pipe verb + its `ParseCommand` row), and **Web** (the Leave control). The pipe guard
(`OverlayPipeContractGuardTests`) already asserts *"every verb the client sends is one the server
accepts"* (`:39-57`), so the new verb is **guard-covered by construction** — the spec extends that
guard's fixture rather than adding a parallel test.

**And it must update a contract document.** `docs/launcher/overlay-spec.md:3` states: *"This document is
the contract for the feature — update it before changing behavior."* Adding a third pipe verb, a page→host
close path, and a host marker at open **is** a behaviour change to that feature, so
`docs/launcher/overlay-spec.md` is a required deliverable of `overlay-hide`, not an afterthought. The
existing "all three entry points converge on one toggle method; no entry point gets its own behavior"
rule (`:80`) is what the new path must not violate — `hide` is the **same hide** the Esc key already
performs (`OverlayWindow.xaml.cs:27-31`), reached by one more route.

## Correction 2 — `first-open-signal` is not observable where I implied

**What I wrote:** a durable fact, emitted once per player, with `entry-landing` "auto-creating the
player if needed".

**Why that is wrong — three separate facts:**

- **"Auto-create the player if needed" is already built.** `SeedPlayerIfEmpty` inserts
  `(1,'Player 1')` on any empty DB and sets `current_player_id=1` (`RpgStore.cs:3865-3878`), called
  from schema unlock (`:149`). A player row **always** exists before the FE can read anything. That is
  a **built** finding, not work — `entry-landing` must not "create a player".
- **The FE cannot detect it is in the overlay.** No host marker exists: the launcher navigates to bare
  `ActiveUrl` (`MainWindow.xaml.cs:322`), the injector to bare `ServerUrl`
  (`OverlayViewHost.cs:47`), and "Open RPG UI" opens the *same* URL in a normal browser
  (`MainWindow.xaml.cs:471`). So "first open" as seen by the page is **indistinguishable** from a
  normal browser first visit.
- **The injector cannot observe the launcher's F10 in launcher mode.** Documented in the code itself:
  *"The launcher's F10 never reaches the injector"* (`OverlaySwitch.cs:136-139`). So an injector-side
  "the player opened the FE" trigger is **blind to the primary open path** in the default host mode.

**The corrected shape:** the fact is written **FE/server-side** on first load (the page can always reach
the server), and the **actionability gate is the server knowing the injector is connected** — the
injector group membership the server already tracks, sending through the existing `SendInjectorCommand`
helper (`Program.cs:920,943,961,1070`). Capture needs the injector running and in-game anyway (it reads
live Almanac dictionaries), so gating on "injector connected" is the honest condition, not a proxy.

**A deliberate consequence worth stating:** because the fact is keyed on *the FE being opened* rather
than on *the tombstone*, it is also correct when the player reaches the FE the other legitimate ways —
the launcher's **"Open RPG UI"** button, which opens the same URL in a normal browser
(`MainWindow.xaml.cs:471`), or a plain browser visit. Capture works whenever the injector is connected;
which surface opened the page is not the condition. That is a feature, not a loophole: it means the
first-open capture is not hostage to which host mode is active, and it is the same reason the server —
not the page — decides actionability.

**Consequence for the spec:** `first-open-signal` has a **Web/Server** arm (record + read the fact) and
an **Injector consume** arm (the capture trigger). Its spec must name the trigger, the durability (a
row, not a session flag, mirroring `OnboardingStoryRow`, `RpgStore.Onboarding.cs:25`), the idempotency
key (`player_id`), and the failure behaviour (never block the FE; an unactionable fact stays pending
until an injector connects).

## Correction 3 — `menu-anchor` is a small patch extension, not a new mechanism

**What I wrote:** "the one genuinely new mechanism."

**Why that overstates it:** patching `UIMgr` statics is the **existing idiom in one file** —
`EnterPauseMenu`, `BackToGame`, `BackToMenu` are all patched (`GameCaptureHooks.cs:780,790,812`), and
`BackToMenu`'s patch exists precisely to emit a signal (`menu.enter`, `:818`).
`UIMgr.EnterMainMenu` is a real callable static (used by the debug nav switch,
`DebugActions.cs:1497`) and is simply **not yet patched**. Adding that patch is the same shape as the
three beside it.

**Consequence for the spec:** `menu-anchor` is a **signal module over existing patches plus one new
patch**, with a **closed surface enum**. Its real work is not the mechanism — it is the **reviewed
allow-list** (which surfaces are safe) and the **exit edges** (the signal must clear on *every* way off
the surface, not just the one the author thought of — DESIGN-GATE §2.16's key-set edge, applied to a
state signal).

## Gap 4 — the existing art already paints where it should not (`built, defective`)

`RiftMenuOverlay.Draw` gates on `Board == null` only (`RiftMenuOverlay.cs:25`). That is **not** "the
main menu": it also covers the seed picker, the Almanac, every submenu, and the defeated-board states
(`DebugActions.cs:1470-1471` enumerates exactly those cases and refuses to guess). So the shipped art
is already drawn on surfaces nobody reviewed — the over-broad gate the ideal flagged as a *click*
risk is **already live as a paint defect**. `tombstone` owns correcting it; the reviewed signal is
what narrows it.

## Gap 5 — the hit box cannot be the painted rect

The art is drawn **rotated and wobbling** — `GUIUtility.RotateAroundPivot(degrees, rect.center)`
(`RiftMenuOverlay.cs:78`) with a pulse-driven angle/wobble (`:53-55`) — while interactive IMGUI hit
testing uses an axis-aligned `Rect` (`OverlaySwitchGui.cs:40`). A hit box equal to the painted rect
would drift with the animation. `menu-anchor` owns the contract: **a stable axis-aligned hit box**,
derived from the layout, independent of the wobble.

## Gap 6 — injector mode cannot open the view if the button preference is off

`TickInjectorHosted` starts the view only when `State.SettingsEnabled` is true, and the code comment
is explicit: *"with the button off there is no other way to open it in this mode"*
(`OverlaySwitch.cs:111-117`). So in injector mode with the preference off, **the tombstone would do
nothing and F10 would be unregistered** (the hotkey is registered on the view's own HWND,
`OverlayViewHost.cs:186`, which never starts). This is a **wiring gap**, not a wall: the view-start
condition must stop keying off the *button* preference once the tombstone is an independent entry.
The spec must decide whether the preference still suppresses the in-match button while the menu entry
stays live (recommended: yes — they are different affordances).

## Gap 7 — the menu-safe surface list must exclude the pause menu

The reviewed allow-list matters because the failure mode is real: the in-match button was deliberately
board-gated so it "cannot eat a menu click" (`OverlaySwitchState.cs:44-48`). A menu tombstone on the
**pause menu** would sit over a live board and collide with resume/quit — the exact hazard the
existing gate avoids. `menu-anchor`'s enum must be **non-gameplay, non-pause** surfaces only.

## Gap 8 — the in-match "RPG" button vs decision 11's "compact icon on the live-lawn entry"

Decision 11 asks for a compact tombstone icon on the live-lawn entry. But the live-lawn entry is
**already** the guard-pinned in-match **"RPG" button** (`OverlaySwitchGui.cs:40`), whose single-action
identity is pinned by `OverlaySwitchState`'s doc and covered by tests. So decision 11 is either
**(a)** a restyle of that button — which the ideal's own out-of-scope list calls a separate reviewed
change — or **(b)** a second lawn affordance, which violates one-owner-per-mechanism. This map does
**not** paper over it: it is the one open question below, and it is the only thing blocking the
`tombstone` spec.

## Gap 9 — the FE cannot currently tell it is embedded, and that is a real decision, not an oversight

Verified negative: no host marker, no `window.chrome.webview`, no user-agent or `window.name` check
anywhere in `web/fusion-rpg-web/src`. Both hosts navigate to the **bare** server URL
(`MainWindow.xaml.cs:322`; `OverlayViewHost.cs:47`), identically to a normal-browser visit
(`MainWindow.xaml.cs:471`). Therefore:

- A FE **Leave** control cannot know whether hiding is possible, so it has three honest options, and
  the spec must pick one: **(i)** show it always and make it a no-op outside a host (dishonest — a
  dead button); **(ii)** let the host pass a marker at open, e.g. a query/hash flag, so the page knows
  it is embedded (verified safe: `IsSameOrigin` compares only scheme/host/port,
  `OverlayViewPolicy.cs:53-55`, so a marker does not break the same-origin lock); or **(iii)** infer it
  from the server's `overlayHost` value — weakest, because a browser and the overlay are
  indistinguishable to the server.
- **Recommendation: (ii).** It is one flag at open, it keeps the page honest about whether the control
  can work, and it is the only option that also lets the page suppress browser-only chrome inside the
  game. Its cost is that **both** hosts must set it — launcher `OverlayWindow` and injector
  `OverlayViewHost` — which is a two-line change to shipped code, and the spec must say so rather than
  discover it mid-build.

## Gap 10 — the `menu-anchor` signal is a **state** signal, so its exit edges are the work

Design-gate §2.16 (event-refreshed cache: enumerate the FULL trigger set, including the state-entry
edge) applies to this signal by shape. The signal is not "a menu was entered" — it is "surface X is
open right now", and every way *out* of X must clear it: entering a match, opening a submenu, opening
the pause menu, opening a modal, losing a board, and game quit. The trap §2.16 names — the edge where
the key set moves — is here the transition into *any* other surface. The spec must list every exit
edge and test each; copying the exit set from `menu.enter` (a one-shot emit on a single patch,
`GameCaptureHooks.cs:812-818`) is exactly the wrong template.

## Decision 17 (owner, 2026-09-15) — `menu-anchor` is redefined: patch the **menu screen**, never drive navigation

**This supersedes Gap 10 and Correction 3's proposed mechanism.** The owner's rule, stated plainly:
**switch the WebView; never fight the PVZ engine.** `UIMgr.BackToMenu` / `EnterMainMenu` are
**forbidden** — they are a known live hazard ("forced entry … can destabilize the engine",
`DebugActions.cs:1495-1496`, `DebugEndpoints.cs:759-760`), `BackToMenu` is documented to land one menu
layer short (`live-test-ssot.md:242-251`), and calling them mid-run breaks a live run. The existing
observe-only `UIMgr` patches may be **consumed**, but their detect logic has never worked well and must
not be depended on.

**The owner supplied the reference implementation** —
[`darkthemer/PvZF_MainMenuFlowers`](https://github.com/darkthemer/PvZF_MainMenuFlowers), whose three
source files are the pattern to follow. It is the same shape we need and it is materially better than
the first draft:

1. **It patches the menu screen's own lifecycle, not navigation statics.**
   `[HarmonyPatch(typeof(MainMenu), "Start")]` (`FlowerClickable.cs`) fires when the main-menu object
   actually exists. That is a **presence** fact, not a hope that a nav call routes through a particular
   static.
2. **It reads the hierarchy by name, observe-only** (`mainMenu.Find("Grave/GraveBackground/Flower1")`,
   `LowerButtons`), never calling an engine action.
3. **It attaches a real uGUI `Button` into the game's own menu hierarchy** (`Image.raycastTarget`,
   `button.transition = Selectable.Transition.None`, parented under the menu transform,
   `SetAsFirstSibling`) — so the game's **own canvas, layout and raycast system owns hit-testing and
   z-order**. It is a UI element *in* the menu, not an IMGUI rect *over* it.
4. Class injection + delegate conversion where needed (`ClassInjector.RegisterTypeInIl2Cpp<T>()`,
   `DelegateSupport.ConvertDelegate<UnityAction>`), and it deliberately lowers sibling/z-order of the
   game's own lower buttons and clears `raycastTarget` on two text labels so they do not obstruct.

**Why this is strictly better than what the module specs proposed:**

| First-draft `menu-anchor` | Reference pattern (adopt this) |
|---|---|
| New patch on `UIMgr.EnterMainMenu` — a **nav call** that may or may not fire | Patch `MainMenu.Start` — the **screen's own** lifecycle; fires iff the screen exists |
| Six hand-enumerated exit edges (match/submenu/pause/modal/lose/quit), each needing its own test | **Exit edges dissolve**: the affordance is a child of the menu object, so Unity destroys it with the menu. Enter/exit are the screen's own `Start`/`OnDestroy` |
| Hand-derived axis-aligned hit box + a "never cover a host primary action" policy (gap 5) | The game's **own** uGUI raycast/layout owns hit-testing; the wobble-vs-hitbox problem does not exist |
| An IMGUI rect over the menu can eat a host click (gap 7) | A UI element **inside** the menu hierarchy, ordered by the game's own sibling order |
| Gap 4 (the over-broad `Board == null` paint) needed correcting | Moot for the affordance: it exists only while `MainMenu` exists. (The existing decorative paint is a separate, smaller cleanup.) |

**Verified in this session:** the type `MainMenu` **exists** in our 3.9 MelonLoader interop
(`H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`), our csproj
already references `UnityEngine.UI`, `Il2CppInterop.Runtime` and `Assembly-CSharp`
(`FusionRpg.Injector.MelonLoader.39.csproj:84-93,128-129`), and our global usings already include
`Il2Cpp` (`GlobalUsings.Il2Cpp.cs`) — so the reference pattern is expressible in our host today.

**Version drift is a verification step, not a risk** (owner, 2026-09-15): *no one redesigns a
main-menu hierarchy between versions.* The reference was built against 3.8.1 and our host is 3.9, so
Task 1 confirms once — against the real 3.9 interop — that `MainMenu` exposes the method the patch
targets and that the hierarchy paths resolve. That is a single check at the start of implementation,
**not** a hedge the design must carry, and it must not be re-raised as a program risk.

**The BepInEx host is secondary, and that is settled, not open.** On this machine `BepInEx/` contains
only `plugins/` (no `interop/`), and `deploy-play.ps1:5` calls that install *"the older 3.8.1 install,
kept for BepInEx-specific testing"*; MelonLoader is the deployed default (`:39,115`). **MelonLoader is
the first target.** Whether the BepInEx interop exposes `MainMenu` is checked when that host is touched.
If it does not, the tombstone is MelonLoader-only and the BepInEx host keeps F10 — a scoped difference
to state plainly, not a design input.

`ClassInjector.RegisterTypeInIl2Cpp` is not used anywhere in our repo today; if the affordance can be
built without a custom `MonoBehaviour` (a plain uGUI `Button` with a listener is enough), prefer that
and avoid introducing class injection at all.

## Enrichment (2026-09-15) — the uGUI click machinery already exists

The reference pattern looks like a new interaction idiom, but three of its four parts are **already
built here**, which makes `tombstone` a composition rather than an introduction:

| Piece the reference needs | Status | Evidence |
|---|---|---|
| Click a real `UnityEngine.UI.Button` | **Built** | `ControlClick.cs:150-153` — `if (target is UnityEngine.UI.Button button) { button.onClick.Invoke(); }` (the shipped `game-control` click path) |
| Census/inspect the uGUI hierarchy (`Button`/`Text`/`GraphicRaycaster`) | **Built** | `ControlInspect.cs:294-298` |
| `UnityEngine.UI` referenced by every host | **Built** | `.Injector.MelonLoader.39.csproj:128` · `.Injector.MelonLoader.csproj:131` · `.Injector.BepInEx.csproj:125` |
| Sprite ↔ PNG conversion (one direction) | **Built** | `TypeIconCapture.cs:158` `SpriteToPng` reads a `Sprite` — the inverse of what a uGUI `Image` needs |
| PNG file → `Texture2D` | **Built** | `RiftMenuOverlay.cs:112-118` (`ImageConversion.LoadImage`) |
| **`Texture2D` → `Sprite`** | **Real gap (one line)** | No `Sprite.Create` anywhere in `src/FusionRpg.Injector`. A uGUI `Image` needs a `Sprite`, so this is the one genuinely new call |

So the honest gap for `tombstone` is **`Sprite.Create` plus the affordance node**, not a new click or
inspection idiom. The spec should say this and point at `ControlClick` as the precedent for "click a
real game Button", so a later session does not re-derive uGUI plumbing that exists.

**One consequence worth stating:** the shipped Rift art is **IMGUI-painted** (`RiftMenuOverlay.cs:20-43`
runs inside `OnGUI`, gates on `Event.current.type == Repaint`, and draws with `GUI.DrawTexture` at
`:79`). The uGUI affordance is a **different rendering path**. Two honest options, and the spec must
pick one:
- **(a)** Keep the existing IMGUI art as-is and add the uGUI affordance *on top* of the same visual
  position, so the painted portal and the clickable node coincide but use different systems.
- **(b)** Move the menu art itself into the uGUI hierarchy (a `Sprite` on an `Image` under the menu
  transform), retiring the IMGUI path for the menu — one rendering system, and the art then inherits
  the game's own canvas scaling.

**(b) is the cleaner end state and is what the reference mod does** (it attaches real `Image`/`Button`
components rather than painting); (a) is smaller and keeps the shipped art untouched. This is the one
open enrichment question (below), and it belongs to `tombstone`.

**Consequences for the module set (unchanged count, changed content):**
- `menu-anchor` becomes **"the menu-screen presence signal"**: patch the menu screen's lifecycle, hold a
  boolean, expose a closed surface enum. Its real work is no longer six exit edges but **proving the
  signal's presence semantics** (fires on the screen, clears with it) and the 3.9 verification above.
- `tombstone` becomes **"attach a uGUI affordance to the menu hierarchy"**, consuming the presence
  signal; the hit-box contract (gap 5) is largely replaced by the game's own layout, and the
  "never-cover" policy (gap 7) becomes "order ourselves below the game's primary buttons", as the
  reference does.
- `overlay-hide`, `first-open-signal`, `entry-landing` are **unaffected** — none of them touches engine
  navigation.

**The hard rule this adds, to restate inline in the specs:** *no rift-gate code may call a `UIMgr`
navigation method (or any engine navigation action) to change what the player sees.* The injector may
**read** engine/menu presence and may **attach** a UI affordance to an existing screen; it may not
drive the engine's menu state. Switching the WebView is the whole job.

---

## Verified constraints that shaped the above (so they are not re-derived)

- **F10 already works on the main menu in both host modes** — `MainWindow.xaml.cs:270` (global
  registration) and `OverlaySwitch.cs:113-117` + `OverlayViewHost.cs:186`. The tombstone is a
  **discoverability** affordance, not a capability fix.
- **The FE must not bind F10.** `keymap.ts:18` forbids it and `keymapGuard.test.ts:10-11` asserts
  nothing else mentions it. The native host path covers it; the ideal already dropped that idea.
- **Hide ≠ acknowledge** — see the `overlay-hide` boundary above.
- **Landing is `/sanctum`** (owner, 2026-09-15). `/sanctum` is where `TitleScreen`'s Continue goes
  (`TitleScreen.tsx:34`) and where the eight rail layers unlock by real play (`railState.ts:65-74`);
  `/saves` has no unlockable pages, so it cannot be what "other pages stay locked" describes.

---

## Decisions resolved 2026-09-15 (owner)

Both audit questions are answered; no open question remains and the module specs are unblocked.

1. **Q1 — the live-lawn affordance is the existing guard-pinned "RPG" button, restyled.** Decision 11's
   "compact icon on the live-lawn entry" **is** that button; this program owns its icon/label as a
   reviewed change to the guard's scope. There is exactly **one** lawn affordance, and no second entry
   is built. Consequence for the spec: `tombstone` includes the in-match button's restyle, and the
   guard/test that pins its single-action identity is **updated, not bypassed** — the button still
   carries one action (show/hide).
2. **Q2 — the hosts set an embed marker at open.** Both the launcher and the injector append a flag
   when they navigate, so the page knows it is embedded and that **Leave** can work. Verified safe:
   `OverlayViewPolicy.IsSameOrigin` compares only scheme/host/port (`OverlayViewPolicy.cs:53-55`), so a
   query/hash marker does not trip the same-origin lock. Consequence for the specs: `overlay-hide`
   owns the marker's shape and both host call sites; `entry-landing` may branch on it (e.g. suppress
   browser-only chrome in-game). The page must still behave correctly with **no** marker (plain browser
   visit), where **Leave** is not offered.
3. **Q3 (decision 17) — `menu-anchor` is redefined.** Patch the **menu screen's own lifecycle**
   (the owner's reference: `darkthemer/PvZF_MainMenuFlowers`), never a `UIMgr` navigation static.
   `BackToMenu`/`EnterMainMenu` are **forbidden** — they are a documented live hazard and calling them
   mid-run breaks a live run. The injector may read menu presence and attach a UI affordance to an
   existing screen; it may not drive the engine's menu state. The existing observe-only `UIMgr` patches
   may be consumed but must not be trusted as the detect logic (they have never worked well).
4. **Q4 (enrichment, owner, 2026-09-15) — one rendering system for the menu.** The shipped Rift art is
   IMGUI (`OnGUI` + `GUI.DrawTexture`); the clickable affordance is uGUI. Move the menu art into the
   uGUI hierarchy as a `Sprite` on an `Image` under the menu transform (the reference mod's own shape),
   retiring the IMGUI path **for the menu**, so there is one rendering system and the art inherits the
   game's canvas scaling. The in-match "RPG" button (decision 15) stays IMGUI — it lives in match HUD
   chrome, not in a menu hierarchy, and that split is deliberate.
   *Note:* this is the `tombstone` module's rendering decision; if implementation finds the IMGUI→uGUI
   art move is not a small change, option (a) in the enrichment section (add the uGUI node over the
   existing painted art) is the documented fallback, and the spec must name it.

Everything else was already decided. `menu-anchor`, `tombstone`, `overlay-hide`, `first-open-signal`,
and `entry-landing` are all spec-able as corrected above — but the `menu-anchor` and `tombstone` specs
must be **rewritten to decision 17's shape**, not patched in place.
