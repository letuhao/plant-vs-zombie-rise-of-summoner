# Game control — the ideal

**Status:** superseded idea record, 2026-09-15. The implementation was authorized and
shipped; the capability map, module specs, and `tasks/game-control-{plan,todo}.md` are
the current contract. This document preserves the exploration and its original rejected
options, not the current API roster.
**Program:** `game-control` (proposed id, renameable at `/spec`) · **Session:** `game-control-idea-20260914`.

## Which loop this extends

None directly — developer/verification infrastructure, not a player feature. Same honest
answer as `live-probe-ideal.md` and `debug-mcp-ideal.md`: this exercises real loops to let
agents drive and prove other features, instead of extending one. It serves Place 1 (lawn)
and Spine A/B live runs, and menu navigation anywhere. No change to `the-loops.md` is
proposed or needed. Gameless-first is untouched: control attaches to a live game when one
is open and gates nothing with the game closed.

## What this is

A Playwright-shaped control surface for the Unity game: where Playwright gives an agent
`screenshot` + element refs + `click`/`fill` + `evaluate` over a web page, this gives an
agent `screenshot` (shipped, live-probe `lawn-screenshot`) + control locators + `click` +
verb calls over the live lawn and menus — through the existing debug command path, with
every response structured, filtered, and paged so an agent never drowns in a raw dump.

Load-bearing principles, stated inline (not linked):

- **Every control action lives behind the debug command discipline.** The RPG layer owns
  game truth; the injector observes past events and contributes signed deltas later. A
  control verb may *trigger* a real engine operation (select a card, call a menu method),
  but it must never *fabricate the result* of an operation the RPG pipeline is supposed to
  produce (`live-probe-standard.md`: a debug API may trigger a real operation, never
  fabricate its result).
- **Two scopes, named per call.** A verb that drives engine state is Game Injector Debug
  (proves the engine reflects it — no domain logic, no persistence). A verb that reads
  persisted RPG state is RPG Server Debug. A pass in one scope is never proof of the other.
- **Record-then-drain.** Control input joins the Cold loop (server → injector inbox →
  main-thread drain); delay is the designed mode, never a defect to engineer around.
- **No new write paths.** Clicks and verbs reuse the guarded apply seams that already exist
  (Writer, Funnel, Intent, UIMgr calls); inventing a parallel apply is a SOLID defect, not
  a shortcut.
- **Adapter-only tooling.** MCP tools wrap these verbs; they never re-implement them
  (the rule that already governs `tools/debug-mcp`).

## What already exists

### Built

| Finding | Evidence |
|---|---|
| Frame capture on demand with UI overlays, proven live (menu frame opened twice) | `src/FusionRpg.Injector/ScreenshotRunner.cs` (Repaint-hook readback), `DebugEndpoints.cs` screenshot routes, `tools/debug-mcp/tools/debug_screenshot.py` |
| ~20 menu navigation verbs callable today | `src/FusionRpg.Injector/DebugActions.cs:1477-1498` (`UIMgr.BackToMenu/EnterMainMenu/EnterAlmanac/…`, via `debug_ui_nav`) |
| Lawn card/cell verbs callable today | `Mouse.ClickOnCard`, `TryToSetPlantByCard/ZombieByCard`, `DisassemblePlant`, `TryToSetPlantByGlove` (all hooked, `GameCaptureHooks.cs:449-631`); board powers via `board.boardAction.*` (`DebugActions.cs:423-563`) |
| Universal object handle: hex native pointer on every entity emit | `GameDumps.Ptr` (`GameDumps.cs:10`); carried by plant/zombie/bullet/mower/grid/bucket/pet/card payloads |
| `FindObjectsOfType<T>` enumeration works in-host for ~15 types | 69 call sites (`CheatActions.cs`, `DebugActions.cs`, `DebugRuntime.cs`, icon capture) |
| Screen↔lawn coordinate math both directions | `LawnCoords` (`Lawn/LawnCoords.cs`), `TryWorldToGui` (used by `VfxDirector.cs`) |
| Main-thread drain for commands + P/Invoke isolation precedent | `CheatCommandRunner.cs`, `Host/InjectorLoop.cs`, `Hud/Win32.cs` |
| Input layer is legacy-only (no synthetic Unity input exists) | `OverlayInput.cs:27` reads `Input.GetKeyDown`; game interop carries `InputLegacyModule` but no InputSystem package |

### Wiring gap

- Card pick→place as one agent verb: `Mouse.ClickOnCard` takes the `CardUI` object and
  `TryToSetPlantByCard` reads `Mouse.Instance.theMouseColumn/Row` (`GameCaptureHooks.cs:459-473`)
  — all pieces exist, no single command chains them. **This is not a wall.**
- Lawn-cell click by screen point: `LawnCoords` already converts both directions; no command
  takes a screen point and performs the cell action. **This is not a wall.**
- Menu-button click by id: the `UIMgr` method table exists (`DebugActions.cs:1477-1498`); no
  id→method map names them as clickable controls. **This is not a wall.**

### Real gap

1. No generic object lister: every enumeration is a bespoke per-type path — no
   `debug.list-objects {type}` over a closed type allowlist.
2. No real-cursor path: legacy input is read-only, so true UX clicks need Win32 `SendInput`
   (new P/Invoke beside `Hud/Win32.cs`) — moves the operator's actual mouse, disruptive by
   nature.
3. No uGUI hit-testing: `GraphicRaycaster` scene presence is unverified (assembly present in
   interop, scene usage unknown — one live `FindObjectsOfType` probe settles it).
4. No Playwright-shaped response discipline on control verbs: today a verb returns `ok:true`
   and the agent re-polls raw event tables (forward-from-`afterId`, filter-after-window) —
   no refs, no filtering, no newest-first tail except the screenshot's own endpoint.

## Prior art

Researched 2026-09-14. The lawn is a canvas app, and Playwright already solved the
"agent drives a canvas" problem — copy its response discipline, not just its verbs.

- **Snapshot-over-screenshot** ([playwright.dev/mcp/snapshots](https://playwright.dev/mcp/snapshots)).
  Playwright MCP operates on the accessibility tree, not pixels: every action returns a
  structured tree where only interactive elements get refs (`e5`, `e10` …). Refs are
  stable *within one snapshot* and die on the next page change; after navigation the tool
  returns a fresh snapshot. Our analogue: control refs scoped to one inspect snapshot
  (our native `ptr` handles are *reused after death* — `match-runtime.md` — so a ref must
  be re-resolved and type-checked before acting, never trusted across snapshots).
  Cost anchor from the same page: ~200–400 tokens per snapshot vs thousands for DOM or
  screenshots — the reason interaction rides structure and screenshots stay separate,
  exactly as our `lawn-screenshot` module already does.
- **Snapshots-with-screenshots for canvas apps** (same page). For visual surfaces Playwright
  returns *both*: the tree for interaction, the screenshot for layout understanding. That
  is precisely the lawn: `inspect` refs + `debug_screenshot` frame, paired per action.
- **The token-blowup failure mode is documented, with measurements**
  ([community guide](https://gist.github.com/monotykamary/24b7d38a2688f742b48ed34aec45209d),
  [issue #395](https://github.com/microsoft/playwright-mcp/issues/395)). Full snapshots
  after every action exhaust 25k-token tool budgets on long pages. Measured mitigations:
  a `return_snapshot` toggle on action tools, truncation at ~100KB with an explicit
  `[TRUNCATED]` marker, stripping the Page-state section. Encoding tweaks are noise
  (JSON-vs-YAML ≈ −0.2%); dropping `cursor=pointer` noise ≈ −12%. Lesson for us, stated
  as a rule: **every list is budgeted with cursor paging and a truncation marker.** The
  shipped owner decision deliberately keeps a current, budgeted snapshot on inspect/click/
  act responses; cursor remains a minimal receipt because it works from raw coordinates.
- **Capability gating** ([playwright.dev/mcp/capabilities](https://playwright.dev/mcp/capabilities)).
  `--caps=vision,pdf,devtools` unlock tool groups; default is minimal core. Our analogue:
  the real-cursor tier (moves the operator's actual mouse) ships behind an explicit
  opt-in, never default-on.
- **Evaluate is labeled RCE-equivalent by its own authors** (`browser_run_code_unsafe`,
  trusted-clients-only; `browser_evaluate` takes an expression plus optional ref).
  That confirms our stance below is industry-standard, not caution: no arbitrary-execution
  verb; the allowlist is the bound.
- **AltTester** (open-source Unity automation; now ships its own MCP server): find/click/
  screenshot/`CallComponentMethod` over a socket, plus recorder. Validates the module
  split; we differ in one load-bearing way — our verbs ride the repo's existing debug
  command path and scope discipline instead of a second protocol.
- **GameDriver** (commercial): HierarchyPath queries + input simulation. Prior art for the
  locator vocabulary question only; not a dependency, never a second surface.

## The shape

Four verbs plus a response contract. Every verb goes through the existing debug command
path (`CheatCommandRunner` drain → emit → poll); every MCP tool adapter-wraps the verb;
no new protocol, no parallel apply.

- **`inspect`** (Playwright `browser_snapshot`): budgeted control tree of the current
  screen — menus as named controls, lawn as cells/cards/entities. Params: `scope`
  (menu/lawn), `limit`, `cursor`. Only interactables get refs (`cN`, snapshot-scoped);
  over-budget responses truncate with a `[TRUNCATED]` marker and a `next_cursor`.
- **`click`** (Playwright `browser_click`): act on a ref from the current snapshot.
  Two tiers: white-box invoke (re-resolve ptr, confirm type, call the known method —
  default) and real-cursor `SendInput` (capability-gated opt-in, foreground check,
  rate-limited). Stale ref → error naming the fresh snapshot, never a blind retry.
- **`act`** (lawn verbs): card-pick→cell-place, shovel, item use — chained white-box
  calls with read-back through the normal telemetry path, one receipt per verb.
- **`screenshot`** (shipped): the paired visual frame, on demand, never bundled into
  every response.
- **Response contract (superseded):** the original minimal-receipt / opt-in-snapshot
  proposal was not adopted. The shipped owner decision is a budgeted, truncation-marked
  current snapshot with inspect/click/act; cursor is the deliberate minimal exception.

Rejected: pixel-coordinate clicks as the primary model (ambiguous, unreproducible —
  coordinates only inside the gated real-cursor tier); a second socket protocol
  (doubles maintenance, drifts — the `debug-mcp` assessed shape already forbids this);
  `evaluate` (arbitrary execution is RCE-equivalent per Playwright's own labeling;
  `debug_call`'s allowlist is the bounded equivalent and stays the only one).

## Tunables

None. This is developer tooling, not the balance surface — same ruling as the
screenshot module. The numbers it will introduce are structural constants, each exempt
with a stated reason in a comment (tunables-ssot.md T2): inspect page size, poll
windows, real-cursor rate limit, truncation byte cap. No `data/tuning` file, no power
number, no progression cap — `ssot-power-scale.md` §11 and the ActorHub gate do not
apply (no actor magnitude is produced or consumed; control verbs trigger engine
operations, they never compose combat numbers).

## What this deliberately does not decide

- Whether the real-cursor tier ships in v1 or stays a named follow-up (recommendation:
  follow-up — white-box verbs cover menus + lawn; the cursor tier's only unique proof
  is true UX input path, needed rarely).
- The exact control-locator vocabulary (who names control ids — a closed registry beside
  the `UiNav` action list, or derived paths). Named as the spec's first decision.
- Whether `inspect` needs the uGUI `GraphicRaycaster` probe or the existing
  card/cell/menu enumerations suffice for v1 (recommendation: enumerations first —
  the raycast is one live probe away if a gap appears).

## Open questions

1. Program id: `game-control` ok, or a narrower name (e.g. `lawn-driver`)? Recommendation:
   `game-control` — menus are in scope from day one.
2. Real-cursor tier in v1 or follow-up? Recommendation: follow-up (see above).
3. Who owns the build: new session per module at `/spec` time, or one session for the
   capability map first? Recommendation: map first, then modules in dependency order
   (`inspect` → `click` → `act`), mirroring how `live-probe` sequenced.
