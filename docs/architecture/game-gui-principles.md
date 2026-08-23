# Game GUI principles — business rules for everything the player sees

**Status: binding.** These are business rules, not style advice. A surface that breaks one of them is
defective in the same way a wrong damage formula is defective, and it is rejected in review.

**Applies to:** the web control room (`web/fusion-rpg-web`), the launcher overlay chrome, and any
in-game UI the injector draws. It does **not** govern developer tooling — see §8.

**Read with:** [DESIGN-GATE.md](../DESIGN-GATE.md) · [web/spec.md](../web/spec.md) (current FE
contract) · [fe-game-foundation.md](fe-game-foundation.md) (DPLP plane locks) ·
[launcher/overlay-spec.md](../launcher/overlay-spec.md) (the native overlay layer) ·
[decisions.md](decisions.md) rows *Standalone-first* and *Game / web overlay*.

**Why this file exists.** The FE was specified as an *audit UI* — `web/spec.md` still calls it
"Dark Lawn Almanac audit UI" and the sidebar component is literally `AuditNav` with the heading
`AUDIT`. Then `decisions.md` locked **standalone-first: "The web RPG is the core game; PvZ is
extension gameplay."** The product became a game; the interface stayed a diagnostic tool. Every
symptom named in review — "doesn't feel like a game menu", "not friendly" — comes from that one
unpaid migration. This file is the target the FE refactor is measured against.

---

## 0. The one-line thesis

**A game interface is a stage with layers. A web app is a document with pages. We built the second
one and shipped it as the first.**

Documents navigate: you leave one page to reach another, and the page you left stops existing.
Games layer: you stay where you are and something opens on top of it. Every rule below is a
consequence of that distinction.

---

## 1. The core law

### GG-1 — One stage, many layers

**Rule.** At any moment the player is on exactly **one stage** — the thing they are playing. Every
other surface (inventory, roster, world map, almanac, settings, shop, quest log) is a **layer drawn
over that stage**, openable from anywhere, and closing it returns the player to exactly the stage
state they left. Replacing the stage is reserved for genuinely leaving the session.

**Why.** This is the definitional property of a game interface, and the rule this document was
commissioned around: *a game is a screen that has a menu that can open anywhere.* A player mid-wave
who wants to check a demon's loyalty must not lose the wave to do it.

**Forbids.** Routing to a sibling screen in order to look at something. Unmounting the board to
render a table. "Go to the Roster page first, then come back."

**Testable as.** Open any layer from any stage; assert the stage component is still mounted, its
state object identical by reference, and no refetch was issued.

> **Note on scope.** GG-1 already holds at the window level: the launcher and injector open the whole
> control room over the running game via F10 / the in-game button, Esc closes it, and the board is
> deliberately paused underneath (`overlay-spec.md` §Behavior contract, §Pause while away). The law
> stops at the browser boundary. Inside the SPA, every surface is a route swap. **We are not
> inventing a principle here — we are extending one the native layer already obeys.**

### GG-2 — The stage is never a diagnostic

**Rule.** The default stage is a place in the game — a base, a sanctum, a world map, a board. It is
never a status readout, a health check, or a metrics table.

**Why.** The first screen tells the player what this software is. If it opens on ingest-queue depth
and last-flush milliseconds, it is a server console, and no amount of theming further in will undo
that first impression.

**Forbids.** Landing on `#/status`. Making telemetry the home route. Treating "the app booted" as a
screen worth showing.

**Testable as.** Boot with an empty save; assert the first paint contains a playable affordance.

### GG-3 — Home always exists

**Rule.** When no battle, run, or expedition is active, there is still a stage: the **home stage**
(base / sanctum / summoner's hall). It is somewhere to *be*, not a menu of places to go.

**Why.** Games without a hub degrade into launchers. The hub is where progression is felt — roster
on the wall, resources on the shelf, the map on the table — and it is what makes the menus feel like
parts of a place rather than tabs of an admin panel.

**Forbids.** An idle state whose only content is navigation.

### GG-4 — Changing stage is an explicit, announced act

**Rule.** There is more than one stage — the sanctum, the world map, the lawn, a battle — but only
ever **one at a time**. Moving between them is a **travel act**: deliberate, initiated by the player,
visibly different from opening a layer (different transition, longer, its own sound), and it may
cost or commit something. Layers never cause a stage change as a side effect.

**Why.** *(Corrected 2026-08-22 — the first version of this rule said only boot, session start and
quit may replace the stage, which forbade ever entering a battle from the sanctum. That was the rule
being wrong, not the game.)* What GG-1 actually forbids is **navigating to look at something**. It
does not forbid *going somewhere to do something*. The distinction is the player's intent: opening
the roster is inspection and must not move them; marching a legion into a sector is travel and
should.

**The test.** If the player is going somewhere **to act**, it is a stage. If they are going
**to look, compare, or configure**, it is a layer. When it is genuinely both — the world map is
consulted *and* acted in — it is a stage, because the losing case (a place you act in, rendered as a
panel that closes) is worse than the reverse.

**Forbids.** A layer whose close button lands the player somewhere else. A stage change with no
transition. Losing an in-progress run because a panel navigated.

**Testable as.** Stage changes come from a single declared transition API; assert no layer
open/close call reaches it.

---

## 2. The layer stack

### GG-5 — Layers live on an ordered stack with fixed bands

**Rule.** Every surface declares exactly one band. Bands order strictly; within a band, later opens
sit above earlier ones. No surface may float outside a band.

| Band | Name | What lives here | Blocks input below | Time underneath | Dismiss |
|---:|---|---|---|---|---|
| −1 | **Shell** | Boot, title, save select, fatal error | n/a — replaces stage | n/a | explicit |
| 0 | **Stage** | Board, world map, home, battle | — | running | never (see GG-4) |
| 1 | **HUD** | Resources, wave clock, hotbar, top bar, minimap | no | running | never |
| 2 | **Panel** | Roster, inventory, almanac, world map, log, shop, progression | yes (below panel) | per GG-13 | Esc / close / click-away |
| 3 | **Dialog** | Confirm, reward, level-up, contract offer | yes | paused | Esc / explicit choice |
| 4 | **Toast** | Result of an action, error, drop notice | **no** | running | auto-expire |
| 5 | **System** | Settings, keybinds, quit, connection failure | yes | paused | Esc |

**Why.** Z-order arguments are the most common source of "why is this behind that" bugs, and are
trivially avoidable with a declared band. Bands also make time and input rules mechanical rather
than per-screen judgement calls.

**Forbids.** Ad-hoc `z-index` values in feature code. A toast that can be occluded by a panel. A
dialog that a panel can cover.

**Testable as.** A lint/guard that fails on any `z-` or `z-index` outside the band tokens.

### GG-6 — Open is push, close is pop

**Rule.** Opening a layer pushes it. Esc / Back / the close control pops exactly one. The stack is
the single source of truth for what is visible and what has focus.

**Why.** One predictable mental model beats twenty per-screen behaviours. A player who learns Esc
once has learned the whole interface.

**Forbids.** Esc closing everything at once. Esc doing nothing on some panels. A close button that
navigates somewhere instead of popping.

**Testable as.** Push three layers, press Esc three times, assert the stack empties one at a time and
the stage is untouched.

### GG-7 — Everything is reachable from everywhere

**Rule.** Every player-facing layer opens from any stage and from any other layer, unless a rule
explicitly forbids that combination (e.g. no shop during a committed battle turn). Forbidden
combinations are a short, written, reasoned list — not an accident of where the link happened to be
placed.

**Why.** The core rule, stated as reach. "You can only equip from the roster screen, and the roster
screen is not reachable while a run is live" is exactly the failure being fixed.

**Forbids.** Placing a feature's only entry point inside another feature's screen.

**Testable as.** A reachability matrix test: for each (stage, layer) pair, assert open succeeds or
that the pair appears in the declared forbidden list with a reason.

### GG-8 — The URL encodes the stack, not a replacement screen

**Rule.** The address bar stays useful: it serialises *stage + open layers* (`#/home?panel=roster`,
`#/battle/42?panel=inventory&tab=relics`). Following such a link restores the stage first, then
opens the layer over it. A URL never means "throw away what you were doing".

**Why.** This is what reconciles deep-linking, refresh-survival, and the overlay's F10 resume with
GG-1. It also keeps the existing HashRouter contract in `web/spec.md` intact instead of discarding
routing wholesale.

**Forbids.** One route per feature at the top level. Back-button behaviour that differs from Esc.

**Testable as.** Deep-link to a layer URL cold; assert the stage mounted underneath and Esc returns
to the bare stage URL.

### GG-9 — One canonical home per concept

**Rule.** Each concept (a specimen, a demon, an item, a run) has exactly one authoritative surface.
Other surfaces link into it; they do not re-implement it.

**Why.** Duplicated surfaces drift, and the player learns to distrust both.

**Forbids.** Stats visible in three places with three shapes. Two roster tables.

### GG-10 — Depth is capped

**Rule.** Reaching any player action takes at most **three** pushes from the stage. A layer that
needs a fourth is a sign the information architecture is wrong, not that another sub-panel is needed.

**Why.** Games are played, not administered. Depth is where friction compounds invisibly.

---

## 3. State, time and continuity

### GG-11 — Opening a layer never destroys the stage

**Rule.** The stage keeps its component instance, its scroll, its selection, its camera, its canvas
context, and its subscriptions while any layer is open. Layers mount over it; nothing beneath
unmounts.

**Why.** This is GG-1's implementation contract, and it is the one that costs the most to retrofit
later. It is also what makes a Phaser stage viable at all — tearing down and rebuilding a WebGL
context to show a table is indefensible.

**Forbids.** Route-swapping the stage out. Destroying the game canvas to render chrome.

**Testable as.** Assert the stage's mount count is 1 across an open/close cycle of every layer.

### GG-12 — Closing a layer restores the exact prior state

**Rule.** Close returns the player to the frame they left: same selection, same scroll, same filters,
same in-progress form input. A layer's own transient state (scroll, tab, draft text) survives being
closed and reopened within a session.

**Forbids.** Losing typed input to an accidental Esc. Resetting a filter set every open.

### GG-13 — Time under a layer is a decision, per band

**Rule.** Whether the simulation advances under an open layer is declared by the band (GG-5), never
left to chance. Where the stage is authoritative elsewhere (the live PvZ board), the pause decision
is the one already locked in `overlay-spec.md` §Pause while away and is not re-litigated here.

**Why.** "Did the wave keep running while I read this?" must never be a question the player has to
ask.

**Forbids.** A blocking panel over a live board with no pause and no visible clock.

### GG-14 — The network never freezes the interface

**Rule.** No layer blocks on a request. Pending work shows as pending *on the affected control*,
while everything else stays interactive. There is no full-screen spinner over a mounted stage.

**Why.** The server is local and usually fast, but "usually" is not a design.

**Forbids.** Disabling the whole panel during a mutation. A blank page while a query settles.

### GG-15 — Acknowledge input instantly; never paint authority early

**Rule.** Every input is acknowledged within one frame — press state, sound, ghost, "sending…".
But the **outcome** is painted only when the authoritative source confirms it.

**Why.** These are two different things and conflating them breaks a hard lock.
`fe-game-foundation.md` RT-04 / RT-12 / RT-15 forbid optimistic living entities and client prediction
of procs, and `decisions.md` makes web outcomes server-authoritative. Responsiveness is a
presentation duty; authority is not.

**Forbids.** Showing a specimen as deployed before Admit. Rolling a proc client-side to make a hit
feel snappier.

**Testable as.** For every mutation: assert an immediate visual acknowledgement, and assert no
authoritative state changes before the response.

### GG-16 — Every action produces a visible result

**Rule.** Success, failure, and rejection are all reported. Silence is not an outcome.

**Why.** The app currently has **zero** notification surfaces, while `fe-game-foundation.md` RT-12
names an "HTTP error toast" as the acknowledgement mechanism. The mechanism was specified and never
built, so failed mutations are silent.

**Forbids.** A mutation whose only failure signal is that nothing changed.

**Testable as.** Force a 500 on every mutation; assert a band-4 surface appears each time.

### GG-17 — Loading, empty, error and forbidden are designed states

**Rule.** Every data surface designs four states besides "has data": loading, empty, failed, and
not-yet-unlocked. Empty states teach and offer the next action. Failed states say what failed and
offer retry. Locked states say what unlocks them.

**Why.** In a game these are content, not edge cases — the empty roster is the player's first
experience of the roster.

**Forbids.** A page that renders nothing when a query errors. `No specimens` with no path forward.

**Testable as.** Per surface, four render tests. Currently 2 of 20 pages handle query error at all.

---

## 4. Input, focus and control

### GG-18 — The top layer owns input

**Rule.** Exactly one layer receives keyboard and pointer input: the topmost blocking one. Layers
below are visible but inert. Click-through never happens.

**Testable as.** Open a panel; assert stage hotkeys and stage clicks do nothing.

### GG-19 — Every layer traps focus and names its first stop

**Rule.** On open, focus moves into the layer, to a declared initial element. Tab cycles within it.
On close, focus returns to the control that opened it.

**Why.** Keyboard and controller parity, and the difference between an interface that can be played
and one that can only be clicked.

**Forbids.** A dialog you can tab out of into the stage behind it.

### GG-20 — One key, one meaning, everywhere

**Rule.** A global verb table is declared once and honoured by every surface: Esc pops, a menu key
opens the main layer set, and each major panel has a stable direct key. No surface reassigns a
global verb.

**Why.** Keybinding consistency is the single cheapest thing a game UI can do to feel like a game.

**Forbids.** Esc closing a panel here and submitting a form there.

**Testable as.** A single keymap module; a guard that fails on `onKeyDown` handlers for global verbs
outside it.

### GG-21 — Everything clickable is operable without a mouse

**Rule.** Every interactive element is a real control (button, link, input) or carries role, tabindex
and key handlers. Every input has a programmatic label.

**Why.** Not only accessibility — it is the same machinery controller support needs later.

**Forbids.** `onClick` on a `div`. Placeholder text used as a label. The current 168 `onClick`
handlers against 1 keyboard handler and 1 `htmlFor`.

**Testable as.** Automated accessibility scan per layer, failing the build on violations.

### GG-22 — Destructive actions are confirmed and, where possible, reversible

**Rule.** Anything that deletes, retires, consumes, or overwrites confirms first, names the exact
thing being lost, and prefers undo over confirmation where the domain allows it.

**Forbids.** A one-click retire. A reset that reads like a refresh.

---

## 5. Language and presentation

### GG-23 — Player vocabulary only

**Rule.** Player-facing text uses the fiction's words. Engine, protocol, and schema vocabulary never
appears on a player surface.

**Why.** This is the most visible failure in the current build. `#/roster` — the summoner's creature
collection — asks the player to type a numeric **`typeId`** into a text box, under copy reading
*"UniqueActor Cold specimens — create, equip, deploy Intent"* and *"Equip compiles grant templates
into mods_json"*. Those are correct internal names and they are exactly wrong here.

**Forbids.** `typeId`, `ptr`, `Intent`, `UniqueActor`, `Cold`, `mods_json`, `Admit`, `revision`,
`ingest queue`, `matchKey` on any player surface.

**Testable as.** A banned-vocabulary guard over player-facing string literals, with an allow-list for
developer surfaces (§8).

### GG-24 — Recognition, never recall

**Rule.** The player chooses from what is shown — portraits, names, icons, counts. They are never
asked to remember or type an identifier.

**Forbids.** Free-text numeric id entry. A dropdown of raw enum values.

### GG-25 — Show the thing, not a row about the thing

**Rule.** Game objects (specimens, demons, items, sectors) are presented with their art, name, and
the two or three numbers that matter. Tables are for logs and ledgers, not for a collection.

**Why.** A creature collection rendered as a `DataTable` is the clearest possible statement that this
is a database viewer.

### GG-26 — Progressive disclosure

**Rule.** Each surface shows the three things a player needs. Everything else lives behind an
explicit "details" affordance. Depth is available; it is not the default.

**Forbids.** Leading with a raw JSON block.

### GG-27 — The squint test

**Rule.** Blur any surface: the hierarchy must still read. The most important number is the biggest
element. Status is legible from colour *and* shape/text, never colour alone.

**Testable as.** Design review checklist item; colour-blind simulation on status surfaces.

### GG-28 — Diegetic framing

**Rule.** The interface belongs to the fiction. Panels are almanac pages, contracts, ledgers,
summoning circles — framed, titled and iconified as such. Chrome carries the world's material, not a
generic card.

**Why.** This is the difference between "a menu" and "a game menu". It is also already the intended
direction: the theme is named *Lawn Almanac* and has been since v1 — the tokens exist, the framing
was never built on top of them.

### GG-29 — One visual system, tokens only

**Rule.** Every colour, space, radius, font and duration comes from the theme tokens. Feature code
carries no raw hex, and no palette from a framework default.

**Forbids.** The current `features/world/LaneEdge.tsx:12-17` and `LegionMarker.tsx:73` hardcoded
`#34d399` / `#a78bfa` / `#0f172a` — a second, unrelated palette inside the game's newest surface.

**Testable as.** A guard that fails on hex literals outside `src/theme/`.

### GG-30 — Contrast is a rule, not a preference

**Rule.** Text meets WCAG AA for its rendered size (4.5:1 body, 3:1 for ≥18.66px bold / ≥24px).
Interactive boundaries and status indicators meet 3:1.

**Why.** Measured on the current palette: `text on warn` **2.33 (fail)**, the active tab's
`almanac on lawn` **4.28** at 12px, the danger button's `text on bad` **3.48** at 12px, and
`border on panel` **1.53** against a 3:1 minimum for control boundaries. Dark game palettes fail
this constantly; it is cheap to fix at token level and expensive to fix per component.

**Testable as.** A contrast test over the token pair matrix, run in CI.

---

## 6. Feedback and game feel

### GG-31 — Nothing teleports

**Rule.** Layers animate in and out with a consistent, short vocabulary of transitions (typically
120–200 ms). Position, opacity and scale are the tools; the direction of motion tells the player
whether they went deeper or came back.

**Why.** Motion is how a player learns the stack shape without being told it.

**Forbids.** A panel that pops into existence identically to how a dialog does.

### GG-32 — Reduced motion is honoured, not ignored

**Rule.** `prefers-reduced-motion` collapses transitions to instant while keeping every state change
legible. Meaning never lives only in the animation.

**Note.** The token layer already does this correctly — keep it when the motion system grows.

### GG-33 — Numbers that change, show that they changed

**Rule.** Resource, XP and stat changes animate or flash at the moment of change. A number that
silently differs from what it was is a missed feedback opportunity in a game about progression.

### GG-34 — Latency is masked by motion, not by spinners

**Rule.** Where a round trip is expected, the acknowledgement animation covers it. The spinner is the
fallback for the unexpected case, not the standard presentation of a normal action.

### GG-35 — Sound is a channel, and it is optional

**Rule.** If audio feedback exists, it is per-category (UI / world / music), individually mutable,
persisted, and defaults to a level that will not startle. No surface depends on sound to convey
state.

---

## 7. Reach

### GG-36 — Layout scales; it does not assume a desk

**Rule.** Surfaces are built against a declared range of viewports and aspect ratios. Chrome docks or
collapses; content reflows; the page never scrolls horizontally.

**Why.** The control room is displayed inside a WebView2 overlay sized to the game window
(`overlay-spec.md`), so its viewport is whatever the player's game resolution is — not a browser the
player can resize to suit us. Measured today: the sidebar is a fixed `w-44` that never collapses, 12
of ~50 components use any breakpoint, and at 800px wide the page scrolls sideways with chart labels
clipped.

**The declared range, corrected 2026-08-23 — this rule named a range and never stated one.**
[`OverlaySwitchLayout.cs`](../../src/FusionRpg.Core/Overlay/OverlaySwitchLayout.cs) is the one place
in the codebase that already reasons about the game's real display floor and ceiling — its own comment
says *"never shrink: the button is already small, and a 720p target would be unhittable,"* pins
`ReferenceHeight = 1080f`, and caps `MaxScale` at `3f` (headroom to roughly 4K-class panels). That is
the evidence this rule borrows rather than a number invented for the FE:

| Bound | Value | Source |
|---|---|---|
| **Height floor** | 720 CSS px | `OverlaySwitchLayout.MinScale`'s own stated reason |
| **Height reference** | 1080 CSS px | `OverlaySwitchLayout.ReferenceHeight` |
| **Height ceiling** | none declared — more room is never a defect | `MaxScale = 3` shows the injector already treats headroom as generous, not a case to design against |
| **Width floor** | 1280 CSS px | the height floor at 16:9, the narrowest common desktop ratio |
| **Width reference / content max** | 1440 CSS px, centred (GG-37) | no wider content column existed to cite; declared here so GG-37's *"centred and bounded"* has a number |
| **Width ceiling** | none — ultrawide gets more room, never less | ultrawide (21:9+) is a *must-not-break* case, not a primary target: chrome may cap at the reference width and centre, it must never assume the extra width is unused |

**CSS pixels, not device pixels — stated so nobody re-derives it from a 4K bug report.** WebView2 is a
Chromium host and inherits standard OS DPI scaling, so a 4K panel at Windows' common 200% default
already presents roughly the same CSS-pixel viewport as a 1080p panel at 100%. The floor and reference
above are CSS pixels: what the layout actually measures against, not raw device resolution.

**Testable as.** Playwright runs of every layer at **1280×720** (floor), **1440×900** (reference), and
**1920×1080** (headroom), asserting `scrollWidth <= clientWidth`. The design plates in this session were
additionally swept at 800×900 as deliberate extra headroom below the evidenced floor — a bonus margin,
not the declared contract; do not mistake 800px for the official minimum when writing the real test.

### GG-37 — Anchor to the edges that matter

**Rule.** HUD elements anchor to safe-area corners and scale with viewport *height*, the way in-game
UI does. Content columns are centred and bounded; they do not stack in the top-left corner of a wide
display.

**Forbids.** The current `max-w-[1100px]` left-aligned column that leaves a third of a 1440px window
empty.

### GG-38 — Weight is a player-facing cost

**Rule.** A layer loads only what it needs. Heavy runtimes (the game canvas, charting, graph
rendering) load when their layer opens, never on boot.

**Why.** Today the production build is a **single 2.77 MB chunk (705 KB gzip) with zero code
splitting** — the Phaser runtime, the charting library and the graph library are all downloaded and
parsed before the first screen paints. In an overlay the player toggles mid-match, boot cost is felt
directly.

**Testable as.** A build-time budget: entry chunk ceiling, plus a check that heavy dependencies do
not appear in the entry chunk.

### GG-39 — The interface is complete offline of the game

**Rule.** Every player surface is fully usable with PvZ closed; injector presence enriches surfaces,
never gates them.

**Why.** This is `decisions.md`'s standalone-first lock restated at the UI layer, because the UI is
where it is easiest to break by accident — an empty state that says "start the game to see this" is
a gate.

---

## 8. Two audiences, two trees

### GG-40 — Player surfaces and developer surfaces are physically separate

**Rule.** Diagnostics, dumps, raw event logs, cheat controls, and protocol inspectors live in a
separate tree, behind an explicit developer mode, off by default. They are not layers of the game and
they do not appear in the game's navigation.

**Why.** The present nav mixes `IconDump`, `AlmanacText`, `Types`, `Runs`, `PvzActivity` and `Status`
with `Roster`, `World`, `Demons` and `Expeditions` at one flat level under a heading that reads
`AUDIT`. The tool and the game are the same surface, so the game inherits the tool's feel.

**Forbids.** A player-visible link to a dump page. Debug vocabulary leaking into player copy (GG-23).

### GG-41 — Developer surfaces keep their own rules

**Rule.** Inside developer mode, density beats polish: tables, raw JSON and engine vocabulary are
correct there. The **player-experience** rules do not apply — GG-23 – GG-34, GG-43 – GG-49, GG-52,
GG-53, GG-58, GG-60. The **structural** rules still do — GG-5, GG-6, GG-18 – GG-21, GG-50, GG-55 —
so the two worlds behave the same way, and a developer surface is still keyboard-operable, still
pops on Esc, still declares its behaviour at a thousand rows, and still says why a control is off.

### GG-42 — Cheats are a developer surface with one exception

**Rule.** Gameplay cheats stay a developer surface. The presentation toggles the player is meant to
own (HUD options, overlay behaviour, keybinds, audio) are a **band-5 System layer** in the game, not
a tab of the cheat console.

**Why.** Consistent with `decisions.md`'s cheats row, which already separates the cheat SoT from an
allowed lightweight overlay-settings panel.

---

## 9. Learning — the player was not born knowing this

### GG-43 — The first session is designed, not defaulted

**Rule.** A brand-new save is a scripted experience with a written beginning, not the normal
interface with everything empty. What the player sees first, what they are asked to do first, and
what is deliberately absent are all authored.

**Why.** The cold-start path is the only one every player takes and the only one the team never
looks at, because developers always have a populated save.

**Testable as.** A fresh-save run in CI that asserts the first paint against the authored script.

### GG-44 — Complexity unlocks; it is not all present on day one

**Rule.** The shell can hide layers. Menu entries, panels and mechanics appear as the player earns
them, and a locked thing says what unlocks it (GG-17) rather than being invisible.

**Why.** This game's vocabulary is enormous — twelve atom kinds, seven elements, twenty-one
statuses, five resources, tiers, rarity, contracts, sectors. Presented at once it is a spreadsheet.
It is also an **architectural** property, not a content one: a menu rail with a fixed entry list
cannot support progression, and that is decided when the shell is built, not later.

**Forbids.** A navigation surface whose entries are a compile-time constant.

### GG-45 — Teach at the moment of first encounter, in place, once

**Rule.** A mechanic is explained the first time the player meets it, on the surface where they met
it, and not again. No modal tutorial gate, no manual the game assumes was read.

**Forbids.** A tutorial sequence the player must finish before playing. Re-explaining on every visit.

---

## 10. Numbers and comparison

The section the game most depends on, and the one most easily got wrong, because the domain's
numbers are not in units a player thinks in.

### GG-46 — A number states its meaning, not only its magnitude

**Rule.** Every player-facing quantity is rendered as an effect the player can reason about. The raw
magnitude may accompany it; it may not stand alone.

**Why.** `+150 crit rate` is not information — it is a resolver-point value on a sigmoid. The
playable form is *"critical strikes 7.6% → 26.9%"*. Four unit families exist and they are not
interchangeable: primary channels in game units, derived channels in resolver points, chances in
integer per-mille, durations in ms. A renderer that does not know which family it holds will print
`+150%` for something that is not a percentage.

**Forbids.** Printing a derived-channel magnitude bare. Showing per-mille to a player as a raw
integer. Any number whose unit the component inferred rather than was told.

**Testable as.** A magnitude renderer that requires an explicit unit family and refuses without one;
golden tests per family. *(definitions.md §2)*

### GG-47 — Anything choosable is comparable, side by side

**Rule.** Wherever the player picks between options — relics, creatures, skills, contracts, sectors
— comparison against what they currently have is a first-class view, not a tooltip afterthought.
When a choice is being made, equipped-versus-candidate is the **default** presentation.

**Why.** This is the core loop of an itemisation game, and it was absent from these rules entirely.
It also decides component shape: a card built only to *display* an entity cannot later be asked to
display a *difference*.

**Forbids.** Requiring the player to remember the equipped item's numbers while looking at a
candidate. A picker that shows only absolute values.

**Testable as.** Every picker surface renders a diff state in its test matrix.

### GG-48 — Compare on the vector; sort on the scalar

**Rule.** Where a summary number exists, it may order a list. It may not be the only thing shown
when the player is deciding.

**Why.** The power scalar is `geomean(vᵢ+1) − 1` over all five categories. It is deliberately
monotone — it never ranks a strictly better thing lower — but it compresses five dimensions into
one, so two very different builds land on the same value. Its own definition calls it *"a display
and sort number… not a balance instrument."* A comparison UI showing only the scalar hides the
trade-off the player is actually making. *(definitions.md §7)*

### GG-49 — A change is attributable

**Rule.** "Why did my attack drop?" is answerable from the interface. Any derived value the player
can see can be expanded into the contributions that produced it.

**Why.** A stat system with flat/increased/more ops, per-channel caps, statuses, shields and
container atoms produces numbers no player can reverse-engineer. Unattributable numbers are how a
build-centred game loses the players who care most about it.

**Forbids.** A stat readout with no path to its sources.

---

## 11. Volume

### GG-50 — Every collection declares its behaviour at 10, 100 and 1000

**Rule.** A surface that lists entities states its strategy at each order of magnitude: render all,
virtualize, or search-first above a declared threshold. Nothing renders an unbounded list.

**Why.** This decides component shape before it decides performance. A card grid written to map over
an array is not convertible into a windowed list without rewriting every consumer.

**Testable as.** A seeded fixture at each magnitude per collection surface; assert rendered node
count and frame cost.

### GG-51 — Query state belongs to the layer and survives it

**Rule.** Search text, filters, sort and view density are part of a layer's state. Closing and
reopening within a session restores them; they are not reset to defaults.

**Why.** GG-12 says a layer restores its own state. This names the part players notice most, because
re-applying four filters is the friction that stops them opening the panel at all.

---

## 12. Reward and interruption

### GG-52 — Reward moments are designed, sequenced, and skippable

**Rule.** Level-ups, drops, contract offers and run results are authored moments with a beginning
and an end, presented one at a time in a declared order, and dismissible by a player who has seen
them a hundred times.

**Why.** This is where game feel actually lives. A progression system whose rewards arrive as a
silently updated number in a table is a spreadsheet with a win condition.

**Forbids.** Three reward dialogs racing to the same band. A celebration the player cannot skip.

### GG-53 — There is an interruption budget

**Rule.** Exactly one class of event may take a blocking layer (band 3) without the player asking,
and which class it is, is declared. Everything else reports at band 4.

**Why.** Interruption is a resource. Games that spend it freely train players to dismiss without
reading, which costs them the one dialog that mattered.

**Testable as.** A lint over band-3 openers: each must be on the declared list.

---

## 13. Truth under latency

### GG-54 — A reversal is shown and explained

**Rule.** When the authoritative result contradicts what the player was shown, the interface says so
in player words and restores the true state visibly. It never silently snaps back.

**Why.** GG-15 buys responsiveness with acknowledgement rather than prediction, which means the gap
between "acknowledged" and "confirmed" is real and occasionally ends in rejection. That moment is
designed, or it reads as a bug.

**Forbids.** A control that returns to its old value with no explanation.

### GG-55 — Never disable a control without saying why

**Rule.** A disabled control carries its reason — on hover, on focus, and adjacent where touch is
possible. If the reason cannot be stated, the control should not be disabled.

**Why.** An unexplained disabled button is indistinguishable from a broken one, and it is the most
common dead end in game interfaces.

**Testable as.** A scan asserting every disabled control has an accessible reason.

---

## 14. Reach, extended

### GG-56 — Text is CJK-safe

**Rule.** Every font stack declares a CJK fallback, and no layout assumes Latin text metrics.
Surfaces tolerate ±40% length change without breaking.

**Why.** Not hypothetical. The FE's own tests assert on the game's almanac strings — `伤害：` and
`韧性：` — so Chinese source text reaches the browser today, and `--font-display` (Lilita One) ships
Latin glyphs only. Every player-facing name rendered in the display font falls back glyph by glyph
to whatever the system happens to have.

**Testable as.** A CJK fixture rendered through every text component; visual diff on overflow.

### GG-57 — Directional input and pointer size

**Rule.** Pointer targets are at least 32×32 CSS px, 44×44 where touch is possible. The layer stack
is traversable by directional input — up/down/left/right moves focus spatially within the top layer.

**Why.** The keyboard rules (GG-19 – GG-21) already build most of this machinery. Declaring the
directional model now makes controller support an implementation rather than a redesign.

### GG-58 — Art has a contract and a fallback

**Rule.** Every entity type declares where its icon comes from and what renders when it is absent —
a designed placeholder carrying the entity's identity (side, element, rarity), never a broken image.

**Why.** Missing art is the normal case during content build-out, not an error. Today it is a 404
and a broken-image glyph.

### GG-59 — Chrome never competes with the stage

**Rule.** Opening, animating or updating a layer must not cost the stage its frame budget. Chrome
work is bounded, and heavy work is deferred until after the stage's frame.

**Why.** Performance here is settled as a main-thread problem. In the browser the stage and the
chrome share one thread exactly as they do in the game.

### GG-61 — A dense entity scrolls inside its own shell; the shell never grows to swallow the viewport

**Rule.** Every band-2/3 shell (`PanelShell`, `DialogShell`) declares a bounded height against the
GG-36 viewport contract. The shell's body scrolls internally when its content exceeds that height.
The shell itself never grows past the space the band model gives it, and the stage behind it never
scrolls to compensate.

**This is not GG-50.** GG-50 is about *many entities* — a list of 1,000 items — and its answer is
virtualization or a search-first threshold. This rule is about *one entity's own content* — one
actor's 99-channel derived-stat sheet, one item's affix list once enhancement, sockets and a set
block are all present, one comparison table. The entity is singular and bounded; its own detail is
still taller than a 720px-floor viewport can show at once. Confusing the two produces the wrong fix:
virtualizing a single actor's stat rows solves nothing, because the actor is not the list.

**Found while auditing, not while designing, and it was already live.** `PanelShell`'s own base rule
set `overflow: auto` on its body with **no height on the shell to make that overflow ever trigger** —
inert without a bound, and only one demonstration anywhere in the design set (the band-shell example
under GG-1/GG-5/GG-11) gave the shell a real height. The fully-built item card (document 2's eleven
blocks, all populated) was **already** taller than any reasonable panel bound — 945px of content
against a 720px cap — and its own `overflow: hidden` shorthand, appearing later in the same rule than
an attempted fix, was silently winning the cascade: the bottom 226px of a real item's card, including
its footer, was being **clipped with no scrollbar and no visual sign anything was missing.** Not a
design risk stated for later — a defect already sitting in the shipped plate, caught by measuring
`scrollHeight` against `clientHeight` rather than by eyeballing a screenshot that looked fine because
the clipped region was, by definition, not visible in it.

**Why.** A panel that grows past the viewport either clips its own footer (the commit button on a
salvage preview, the deploy button on an actor panel) or forces the *page* to scroll, which drags the
stage and the band-1 HUD out of view behind it — the exact failure GG-11 exists to prevent for the
canvas, arriving instead through a panel that simply got too tall.

**Testable as.** A volume fixture per dense-entity surface — an actor at 141 derived channels, an
item at the authored affix ceiling, a comparison at every channel family present — asserting the
shell's own `clientHeight` never exceeds its band-declared maximum and `scrollHeight > clientHeight`
triggers the body's scrollbar, not the page's.

---

## 15. Arbitration — when two rules pull apart

### GG-60 — Legibility wins under time pressure; fiction wins everywhere else

**Rule.** Where diegetic framing (GG-28) and plain legibility (GG-27, GG-46) conflict: on any surface
the player must act on while something is happening, legibility wins. On surfaces the player reads at
their own pace, fiction wins.

**Why.** Every game-UI project has this argument, usually late and usually as taste. Deciding it now
turns it into a lookup. An ornate frame on a wave timer is a defect; the same frame on an almanac
page is the point.

---

## 16. Rejected patterns

| Rejected | Why |
|---|---|
| One top-level route per feature | The failure this file exists to correct — makes every surface a screen replacement (GG-1) |
| Sidebar of every noun in the system, flat | Navigation as a table of contents; no grouping, no priority, no fiction (GG-3, GG-40) |
| Landing on a health/status screen | Tells the player this is a server console (GG-2) |
| Modal implemented per feature | Every one behaves differently; Esc becomes unlearnable (GG-5, GG-6) |
| `z-index` chosen per component | Guaranteed occlusion bugs; unrecoverable once spread (GG-5) |
| Raw id entry as an input method | Database form, not a game (GG-24) |
| Collections rendered as tables | States plainly that the game is a viewer over a schema (GG-25) |
| Silent mutations | The player cannot tell the difference between "worked" and "server down" (GG-16) |
| Full-screen spinner over a mounted stage | Destroys the illusion of a persistent place (GG-14) |
| Client-side prediction to improve feel | Breaks the authority locks; feel is bought with acknowledgement and motion instead (GG-15) |
| A second palette inside one feature | Two design systems in one product (GG-29) |
| Debug pages in player navigation | The tool's feel contaminates the game (GG-40) |

---

## 17. Priority — not all sixty-one rules are gates

Sixty-one rules is a reference, not a checklist. Nobody runs a sixty-item gate, and a document that
pretends otherwise gets skimmed instead of used. So the rules are tiered.

**Tier 1 — hard gates.** Break one and the work does not merge. These are the rules that are
expensive or impossible to retrofit, because they decide *structure*.

| Rule | Decides |
|---|---|
| **GG-1** one stage, many layers | The entire shell |
| **GG-5** bands | Stacking, input, time — mechanically |
| **GG-11** stage survives | Whether a canvas stage is viable at all |
| **GG-15** acknowledge ≠ authority | Compliance with the authority locks |
| **GG-23** player vocabulary | Whether this reads as a game or a schema |
| **GG-40** two audiences, two trees | Whether the tool's feel contaminates the game |
| **GG-44** complexity unlocks | Whether the shell can support progression |
| **GG-46** numbers state meaning | Whether the game is readable |
| **GG-47** choosable is comparable | The shape of every collection component |
| **GG-50** volume declared | Whether components are virtualization-ready |
| **GG-61** dense entity scrolls internally | Whether a shell can hold real content without growing past the viewport |

**Tier 2 — review findings.** Break one and it is a review comment with a fix, not a blocked merge:
GG-2, 3, 6, 7, 8, 9, 12, 13, 14, 16, 17, 18, 19, 20, 21, 22, 29, 30, 36, 38, 39, 41, 42, 48, 49, 51,
54, 55, 56, 58, 59.

**Tier 3 — craft.** Judged, not gated. They make the difference between correct and good, and they
are where taste legitimately operates: GG-4, 10, 24, 25, 26, 27, 28, 31, 32, 33, 34, 35, 37, 43, 45,
52, 53, 57, 60.

**GG-10's "at most three pushes" is a heuristic, not a measurement.** It is stated as a number so it
can be argued with. If a surface has a good reason for a fourth, the reason goes in the review, not
in silence.

---

## 18. Compliance snapshot — 2026-08-22

**This is a dated measurement, not a maintained table.** It records where the build stood when these
rules were written, so the refactor has a baseline to be scored against. It will go stale and that is
fine — the living check is §19's enforcement table, which runs. Do not update this section; add a
new dated one, or trust the checks.

| Rule | State | Evidence |
|---|---|---|
| GG-1 one stage, many layers | **Fail** | 20 top-level routes, no layer stack ([routes.tsx](../../web/fusion-rpg-web/src/app/routes.tsx)) |
| GG-2 stage is not a diagnostic | **Fail** | index redirects to `/status`; first paint is ingest queue + flush ms |
| GG-3 home exists | **Fail** | No home stage |
| GG-5 bands | **Fail** | 3 z tokens, no band model; one modal (`LawnStatsModal`) |
| GG-6 Esc pops | **Fail** | 1 keyboard handler in the whole app |
| GG-7 reachable from anywhere | **Fail** | Reach is exactly the sidebar |
| GG-8 URL encodes the stack | **Partial** | HashRouter exists; encodes screens, not layers |
| GG-11 stage survives | **Fail** | Phaser game is created/destroyed with the route |
| GG-16 visible result | **Fail** | 0 toast/notification surfaces in `src/` |
| GG-17 four states | **Fail** | 2 of 20 pages handle query error |
| GG-21 operable without a mouse | **Fail** | 168 `onClick` · 1 keyboard handler · 1 `htmlFor` · 7 `aria-label` |
| GG-23 player vocabulary | **Fail** | `typeId` input, "UniqueActor Cold", "deploy Intent", "mods_json" on `#/roster` |
| GG-25 show the thing | **Fail** | Collections are `DataTable` rows |
| GG-29 tokens only | **Fail** | `LaneEdge.tsx:12-17`, `LegionMarker.tsx:73` hardcode a framework palette |
| GG-30 contrast | **Fail** | `text on warn` 2.33 · active tab 4.28 @12px · danger label 3.48 @12px · borders 1.53 |
| GG-32 reduced motion | **Pass** | `tokens.css` honours `prefers-reduced-motion` |
| GG-36 scales | **Fail** | Fixed `w-44` rail; horizontal scroll at 800px; 12 of ~50 components use a breakpoint |
| GG-37 anchoring | **Fail** | `max-w-[1100px]` left-aligned; a third of a 1440px viewport unused |
| GG-38 weight | **Fail** | Single 2.77 MB chunk (705 KB gz), zero code splitting |
| GG-39 offline of the game | **Pass** | Surfaces render with the injector absent |
| GG-40 two trees | **Fail** | `IconDump` / `AlmanacText` / `Types` / `Runs` sit beside `Roster` / `Demons` under a heading reading `AUDIT` |

Also outstanding and unattributed to a single rule: no favicon (404 on every load), and icon 404s
render as broken images with no fallback.

---

## 19. Enforcement

A principle nobody can fail is advice. Each of these becomes a check before the refactor is called
done.

| Check | Enforces | Shape |
|---|---|---|
| Band-token lint | GG-5 | Fail on `z-index` / `z-*` outside declared bands |
| Stage-persistence test | GG-1, GG-11 | Mount count of the stage stays 1 across every open/close |
| Reachability matrix test | GG-7 | Every (stage, layer) pair opens, or is on the declared forbidden list |
| Esc/stack test | GG-6, GG-18, GG-19 | Push 3, pop 3; focus trap and restore per layer |
| Mutation-feedback test | GG-16 | Force failure on every mutation; assert a band-4 surface |
| Four-states test | GG-17 | Per data surface: loading / empty / error / locked |
| Accessibility scan | GG-21 | Automated per-layer scan in CI |
| Vocabulary guard | GG-23 | Banned engine terms in player-facing strings; developer tree allow-listed |
| Hex guard | GG-29 | No colour literals outside `src/theme/` |
| Contrast test | GG-30 | Token pair matrix vs WCAG thresholds |
| Viewport sweep | GG-36 | Every layer at 1280×720 / 1440×900 / 1920×1080; no horizontal scroll |
| Shell-height fixtures | GG-61 | A dense-entity fixture (max derived channels, max affixes) per shell; assert body scrolls, shell height never exceeds its band bound |
| Bundle budget | GG-38 | Entry chunk ceiling; heavy deps must not be in the entry chunk |
| Unit-family guard | GG-46 | Magnitude renderer refuses an unlabelled unit; golden per family |
| Diff-state matrix | GG-47 | Every picker surface renders a comparison state in its tests |
| Volume fixtures | GG-50 | Seeded 10 / 100 / 1000 per collection; assert rendered node count |
| Band-3 lint | GG-53 | Only declared event classes may open a blocking layer unprompted |
| Disabled-reason scan | GG-55 | Every disabled control exposes an accessible reason |
| CJK fixture | GG-56 | Chinese strings through every text component; overflow visual diff |
| Cold-start test | GG-43 | Fresh save; first paint asserted against the authored script |

---

## 20. Decisions

### 20.1 Decided under design authority, 2026-08-22

Taken so the design could be completed rather than blocked. Each records its reasoning so the owner
can overturn one without unpicking the rest. Full detail and consequences:
[design/information-architecture.md](../design/information-architecture.md).

| # | Question | Decision | Because |
|---|---|---|---|
| D1 | What is home? | **The Sanctum** — the summoner's hall. Default stage, and where a session with no run in progress lives | The product is summoner-led progression. A hub is where progression is *felt* — roster on the wall, pacts on the shelf, map on the table. The world map was the alternative and loses: it is a place you act in over time, so it wants to be its own stage, not the thing every panel floats over |
| D2 | Is the lawn a stage or a layer? | **A stage.** So are the world map and a battle. Four stages, one at a time | It is a place you *play*, not a thing you consult (GG-4's test). This also fixes the canvas lifetime: the Phaser game is created on entering the lawn stage and destroyed on leaving it — never on opening a panel, which is what GG-11 actually requires |
| D3 | How far does diegetic framing go? | **Split by GG-60.** Almanac/grimoire framing on surfaces read at leisure; plain legible chrome on surfaces acted on under time pressure | Applying the arbitration rule instead of choosing a global level. Also the cheaper answer: ornament is bought only where it is looked at |
| D4 | Developer-mode gate | **A settings toggle, persisted, default off, plus a `?dev` escape hatch.** The tree ships in the build | The overlay is local-only and single-user; a build variant buys nothing and doubles the CI matrix. The toggle is what makes the developer tree reachable without putting it in player navigation (GG-40) |
| D5 | Verb table | **Esc pops one layer. `F10` stays the window toggle.** Panels get single letters; nothing shadows a browser or overlay verb | One key law across the native overlay and the app, which `overlay-spec.md` already half-owns (Esc and F10 close the overlay view) |
| D6 | Interruption class (GG-53) | **Run-ending results only.** Level-ups, drops and contract offers report at band 4 and queue for the sanctum | Exactly one class may hold the privilege, and the run result is the only event the player cannot meaningfully act on later |
| D7 | CJK (GG-56) | **Design for it unconditionally.** Every stack declares a CJK fallback; the display face is used only where a Latin-only face is acceptable, and never for content names | The almanac path already carries Chinese today. Designing for it costs a font stack; retrofitting costs every layout |
| D8 | Forbidden combinations (GG-7) | **Three, and only three:** no stage travel while a battle turn is committed; no fusion or release of a creature that is deployed; no pact renegotiation while its tribute is overdue | Named, so the rule is checkable. Everything else opens from everywhere |

### 20.2 Decided 2026-08-22 — the remainder

| # | Question | Decision | Because |
|---|---|---|---|
| D9 | Migration scope and order | **Six phases**, in [design/tech-stack.md §9](../design/tech-stack.md) | Ordered so the layer stack is proven over the *existing* pages before any redesign, and the diagnostic-route sweep lands in phase 1 where it is nearly free and changes the whole impression |
| D10 | Does the Sanctum get art? | **Framed chrome now; art later, and the layout is already art-ready** | Illustration is the single largest cost driver and the least reversible thing in the design. The Sanctum composes around a backdrop, frames accept portraits, the map accepts terrain — so art is additive whenever the game's visual identity settles, and nothing is blocked meanwhile |

### 20.3 Resource model — settled, and the GUI takes no position anyway

**Settled 2026-08-22:** five actor resources in one shared set — `hp` · `stamina` · `hunger` ·
`spirit` · `qi` — with **faction differences as display labels only** (plant shows `hunger` as
*Sun* and `qi` as *Yang*; zombie shows `qi` as *Yin*). No branch, no faction-specific ids.
SSOT: [resource-hub-ssot.md](resource-hub-ssot.md).

*An earlier note here claimed a conflict between `decisions.md` and `resource-hub-ideal.md`. There
was none: the ideal doc's own §10.2a already carried this model, and the material that appeared to
disagree — its §2 and its "refused names" bullet — had been superseded within that same document.
The defect was a stale section, not a disagreement.*

**The GUI is unaffected either way.** Resource surfaces bind to `(id, label, value, max, polarity)`;
the label is resolved from the actor's faction at the display layer, so no component knows the id
list and adding a sixth resource changes nothing. One rule does fall out of the SSOT and matters
here: the plant pool labelled **"Sun" is `hunger` at actor scope** and is *not* the lawn's sun bank,
which is `pvz.*` and match-scoped. A surface showing both must separate them by scope — the bank
belongs to the stage HUD, the pool belongs to the actor's meters.

### 20.4 Viewport contract and internal scroll — added 2026-08-23

**GG-36 named "a declared range of viewports" from the day this file shipped and never declared one.**
Corrected in place rather than left as a cross-reference to nowhere: floor **1280×720 CSS px**,
reference **1440×900**, headroom to **1920×1080** and beyond with no ceiling — evidenced from
[`OverlaySwitchLayout.cs`](../../src/FusionRpg.Core/Overlay/OverlaySwitchLayout.cs)'s own stated
720p-unhittable floor and 1080p reference height, the one place in the codebase that already reasoned
about this. Full detail in GG-36 itself.

**GG-61 is new**, not a correction — no rule previously covered a single dense entity (one actor's
stat sheet, one item's full affix list) outstripping its own panel's height. GG-50 covers *many*
entities; nothing covered *one entity, a lot of content*. Found auditing `PanelShell`'s own CSS: its
body declares `overflow: auto` with no height on the shell to ever trigger it, and only one
demonstration in the whole design set gives a shell a real height. Detail in GG-61.

---

## 21. Glossary

| Term | Meaning here |
|---|---|
| **Stage** | The one thing the player is currently playing. Exactly one exists at a time |
| **Layer** | A surface drawn over the stage, opened and closed without replacing it |
| **Stack** | The ordered list of open layers; the source of truth for visibility and focus |
| **Band** | The fixed z-tier a layer declares (GG-5) |
| **Shell** | Boot / title / save select — the only surfaces that replace the stage |
| **Player surface** | Anything a player is meant to see. Governed by every rule here |
| **Developer surface** | Diagnostics and tooling. Governed by GG-40 – GG-42 only |
