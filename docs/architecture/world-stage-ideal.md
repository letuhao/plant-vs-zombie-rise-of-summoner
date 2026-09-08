# World stage — the ideal

**Status:** idea phase, 2026-09-03. **Sixteen owner decisions recorded — §8 (4), §8b (4), §8d (4), §8e (4) —
across two rounds and a four-perspective adversarial design review (§8c). No open questions. Not a
spec. No build authorized.** The deliverable of this phase is this document plus
[plate 11](../design/11-world-stage.html), the component catalog drawn from it.

**Renderer HOW superseded 2026-09-05.** The SVG `viewBox` camera and "drop xyflow, therefore draw the
player map in SVG" clauses (§4.1–§4.2 implementation, §8 xyflow bullet, §8c.6 "removing xyflow now")
are superseded by [world-map-runtime-ideal.md](world-map-runtime-ideal.md). HUD, inspector, commands,
GG rules, no-minimap-on-medium, and plate 11 §A as the **panel** catalog remain. Plate 11 §O is the
map-pin catalog (HTML stand-in for the Phaser plane).

**Read §8c and §8d before §4.** The review found §4's interaction design calibrated for a game with
dozens of units while this one has **one** (§8c.1). The owner's answer was not to shrink the interface
but to **build recruitment first** (§8d.1) — so §4 stands whole, and the program now depends on a
module that had not started. **§4's scope is deliberately the destination, not the first slice**
(§8d.4): slicing is a `/plan` decision, taken against a complete idea.

**Program id:** `world-stage`. It covers the surface that two existing programs both point at and
neither built: `world-map-program.md`'s `world-fe` module (*"`#/world`: graph render, sector
inspector, order queue, End Turn, turn report playback"*) and the Game GUI refactor's **T16**, which
the owner excluded on 2026-08-23 with *"Map GUI is exclude this phase, just keep it as is — we will
have other plan for it because that is huge design, should make new GUI solid foundation before we
move to the map."* This is that plan's starting point.

**Occasioned by** the owner, 2026-09-03: *"current map design is bad, i cannot really used in a
game, it cause annoying and it is not a game ui should be. A game ui is tick the control, so user
can interact with dialog, hud, our map ui force user scroll."*

---

## 0. The principles this design is bound by, restated in full

Stated here rather than linked, because a downstream session reads this document and not its links.

1. **Every RPG feature lives in the RPG layer; it is never built by changing what PvZ is.** The world
   map does not touch the lawn's Unity write surface at all. Nothing in this document is constrained
   by what a `Plant` or `Zombie` field can represent, and no argument from that surface is valid here.
2. **Standalone-first.** *The web RPG is the core game; PvZ is extension gameplay.* Every surface
   below must be fully usable with the game closed. The injector may enrich, never gate.
3. **A game interface is a stage with layers, not a document with pages** (GG-1). The player is on
   exactly one **stage**; everything else is a **layer** drawn over it, openable from anywhere and
   closing back to the same state. The world map is a **stage**, decided by GG-4's own test — *"if the
   player is going somewhere to act, it is a stage"* — and confirmed in
   `design/information-architecture.md` §2.2 and plate 03 §E.
4. **Opening a layer never destroys the stage** (GG-11). The map keeps its component instance, its
   camera, its selection and its subscriptions while any panel is open.
5. **Six fixed z-bands** (GG-5): −1 shell, 0 stage, 1 HUD, 2 panel, 3 dialog, 4 toast, 5 system. No
   surface floats outside a band; no `z-index` in feature code.
6. **Acknowledge input instantly; never paint authority early** (GG-15). The turn engine is
   authoritative and the resolution is a server round-trip. Responsiveness is bought with
   acknowledgement and motion, never with client prediction.
7. **A number states its meaning, not only its magnitude** (GG-46, a Tier-1 hard gate). Every
   player-facing quantity is rendered as an effect the player can reason about, and a renderer that
   was not *told* its unit family may not guess one.
8. **A dense entity scrolls inside its own shell; the shell never grows to swallow the viewport**
   (GG-61, Tier-1). A band-2/3 shell declares a bounded height against the GG-36 viewport contract,
   and its body scrolls internally. The stage behind it never scrolls to compensate.
9. **The viewport contract** (GG-36, corrected 2026-08-23): floor **1280×720 CSS px**, reference
   **1440×900**, no ceiling. The page never scrolls horizontally.
10. **Player vocabulary only** (GG-23, Tier-1). Engine, protocol and schema words never appear on a
    player surface.
11. **Complexity unlocks** (GG-44, Tier-1). A navigation surface whose entries are a compile-time
    constant cannot support progression.
12. **The balance surface is data.** Any number a balance pass would change lives in
    `data/tuning/<domain>.v{n}.json`, never a `const`.
13. **One power ladder.** Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`. A map
    UI *displays* numbers and must never derive a curve to render one.
14. **No hard progression ceilings.** Caps on magnitudes are soft and configurable; absolute bounds
    are derived and throw rather than clamp silently.
15. **SQL lives only in `FusionRpg.Data`.** Any new read this stage needs is a Server-layer
    projection, never a query from Core or the web.

**Design-gate compliance.** Read in this session before any of §3 onward was written:
`game-gui-principles.md` (all 61 rules, the §17 tiering, §19 enforcement, §20 decisions D1–D10),
`design/information-architecture.md` (complete), `design/README.md` §6 and its plate index,
`architecture/fe-game-foundation.md`, `software-architecture.md` §6–§9, `decisions.md`'s Game GUI and
standalone-first rows, `world-map-program.md`, `loam-map.md`'s module table, `loam/spec-loam-fe.md`,
and `design/03-world.html` itself. Two inventories were run against `src/` and `web/` with file:line
citations; two prior-art surveys were run against published sources.

---

## 1. What this is, in a paragraph

The world map is where the empire is played: sectors you hold, lanes you march down, legions that
carry their own supply, ground that fades when you cannot feed it, and a turn you commit when you
are ready. It should read as a **place you are standing in** — one camera, a HUD anchored to the
corners that never moves, a bounded inspector that opens over the map without hiding it, and an
end-turn control that tells you what is still unresolved before you commit. Today it reads as a
flowchart with a sidebar of buttons: the map is a 620px box in the middle of a scrolling page, every
verb is a text button in a 300px column, the economy prints raw engine strings, and you cannot
deselect a sector once you have selected one.

**Nothing in that gap is an architectural limit.** The read surface is largely built and genuinely
fog-correct. What is missing is a stage, a HUD, a vocabulary, and — on the server — five fields and
two command fields, each of which names a specific line.

---

## 2. What already exists

Sorted into **built** / **wiring gap** / **real gap**, using those exact words. A wiring gap is
machinery that exists and is inert — never a wall.

### 2.1 Built — works end to end today

| Capability | Evidence |
|---|---|
| Six HTTP routes: header, fogged state, submit commands, commit turn, turn report, SIM create | `WorldEndpoints.cs:27, 36, 56, 109, 145, 235` |
| Fog of war, four states, derived not stored: `Unknown` / `Rumored` / `Scouted` / `Watched`, `FreshTurns = 5` | `Intel/FactionIntel.cs:131-140`; on the wire at `WorldDtos.cs:127, 132` |
| Belief-only projection — the endpoint reads a `BelievedWorldView`, never truth | `Intel/IWorldView.cs:76-149`, constructed `WorldEndpoints.cs:53` |
| Per-sector income / upkeep / net, computed at projection time | `WorldDtos.cs:89-95`, `WorldEndpoints.cs:420-461` |
| Territory-component identity and pooled totals — the *"my empire is fine while half of it starves"* case | `WorldDtos.cs:102-117`, `Loam/TerritoryComponents.cs:17` |
| Release forecast: `WillReleaseNextTurn`, sharing `Weakest` with the engine so the warning cannot disagree with the act | `WorldDtos.cs:125`, `Loam/LoamForecast.cs:58` |
| Lifeline / articulation overlay (opt-in via `?lifelines=true`) | `WorldEndpoints.cs:51, 382-396` |
| Five command kinds reachable end to end: `stand-fast`, `move`, `clear`, `claim`, `stance` | `WorldCommand.cs:10-34` |
| Turn report vocabulary: **21** event prefixes, 3 battle kinds, 2 calendar subjects, **37** drop reasons (33 bare + 4 carrying an argument) | counted 2026-09-03 across `src/FusionRpg.Core/World/`, not estimated — an earlier "~30" understated it |
| AI order-and-reason narration on the wire | `WorldEndpoints.cs:185-196` |
| The world fixture is **generated and byte-pinned**, not hand-written | `tests/FusionRpg.E2E.Tests/WorldFixtureTests.cs:28-50` |
| The FE's pure layer is genuinely good and fully tested: `worldViewModel.ts`, `worldSelection.ts`, `turnPlayback.ts`, `commanderIntent.ts` | 4 modules, 9 test files, all pure |
| `LoamGauge` (income/upkeep/net/stock + per-component split) and the fog treatment on sector cards | `LoamGauge.tsx:21-58`, `SectorFog.test.tsx` |

### 2.2 Wiring gap — the machinery exists and is inert

**The write surface is where the holes are, and each is blocked twice over.**

| Inert thing | The specific line |
|---|---|
| `sustain` command can never be submitted | `WorldCommandRequest` has no `Amount` (`WorldDtos.cs:205-217`), so `WorldEndpoints.cs:72-82` cannot set it → refused `amount.invalid` at `WorldCommandAdmission.cs:63` |
| …and would not survive persistence either | `CommandPayload` at `RpgStore.WorldTurns.cs:442-444` omits `Amount`; orders are re-admitted from the log at `TurnEngine.cs:134` |
| `build` command, same two blockers | `WorldDtos.cs:205-217` has no `StructureId`; `WorldCommandAdmission.cs:74`; `RpgStore.WorldTurns.cs:442-444` |
| `SustainResolver` / `BuildResolver` — fully implemented, wired into the engine, can never receive an order | `Movement/SustainResolver.cs:19`, `Movement/BuildResolver.cs:11`, wired `TurnEngine.cs:214, 280` |
| `WorldSector.WardenBindingId` — read, hashed, persisted, cleared on capture, **set non-null nowhere in production** | read `LoamForecast.cs:24`, `LoamPhases.cs:162`; hashed `WorldCanonical.cs:37`; cleared `ClaimResolver.cs:85`; written only in `LoamTextureTests.cs` |
| `RpgStore.BindAsWarden` — implemented with capacity, fee and the non-releasable flag; no production caller | `RpgStore.Contracts.cs:283`; `/api/contracts/bind` calls `BindContract` at `ContractEndpoints.cs:31` |
| `Prospecting.Reveal` — implemented, reach 4 lanes; blocked by **one line** | `IntelRecorder.cs:179`; `"dowse"` is absent from `MovementPolicy.Stances` (`Movement/LaneCost.cs:13`), so admission refuses it at `WorldCommandAdmission.cs:51` |
| Five DTO fields the economy needs: `CarriedLoam`, member `Role`, `ConstructionTurnsRemaining`, `WardenBindingId`, `NeglectedTurns` | `WorldState.cs:262, 220, 116, 173, 180` — none on a DTO |
| `WorldSlotDto.OwnerFactionId` — declared and never assigned | `WorldDtos.cs:31` vs `WorldEndpoints.cs:318-327` |
| `PressureMilli` — real live state written by fade contagion every turn, never projected | `LoamPhases.cs:266-283` writes it; `WorldDtos.cs:72` never assigned |
| Effective capacity — a UI showing `loamStock` has **no denominator** | `LoamPhases.EffectiveCapacity` at `Loam/LoamPhases.cs:58`, no DTO field |
| Legion capacity / burn / leash / runway as *state* (they reach a client only as a narration string) | `LegionSupply.cs:20, 24, 32, 46`; `LeashTurns` has zero production callers |
| No catalog is reachable over HTTP — structures, slot types, strength bands, templates, lane types | `StructureCatalog.All` is public at `:53` with no HTTP caller; same for `SlotTypeCatalog.cs:54`, `Intel/StrengthBandCatalog.cs:35` |
| The `Growth` turn phase is a named no-op | `TurnEngine.cs:199` — a bare `return world;` |
| Playback understands 5 of 21 event prefixes; the remaining 15 print raw | `turnPlayback.ts:33-42` |
| `attrition:` is a **dead branch** — recognised by the client, emitted by nothing | `turnPlayback.ts:40`; retired when `LegionSupply` replaced wound attrition (`SupplyGraph.cs:42-45`) |
| `worldTypes.ts` has already drifted from the C# DTO (`structureId` missing since L32) | `worldTypes.ts:13-21` vs `WorldDtos.cs:39`; the fixture pins JSON, not the TS type, so CI cannot see it |

**Two fog defects, in opposite directions.** Report entries are filtered on the structured `SectorId`
(`WorldEndpoints.cs:215-219`), so:

- entries with a **null** `SectorId` are shown to **every** viewer — every battle line
  (`BattleReporting.cs:36`), `legion.topup` (`LegionSupply.cs:98`), `loam.handicap` and
  `loam.shortfall.unresolved` (`LoamPhases.cs:119, 141`);
- `halt:` and `legion.runway:` pass a **lane id or an event detail** into the `SectorId` slot
  (`MovementPhase.cs:105, 195`), so `Believed(...)` returns null and the line is filtered out for
  **everyone** — meaning the client's existing `halt` keyframe can never fire against a live server.

**A GG-46 hazard that was sitting in the data, fixed 2026-09-05 (world-map W57).**
`StructureDef.Cost` (`StructureCatalog.cs:26`) and `LoamPolicy.WellCost` / `WaystationCost` /
`GranaryCost` (`LoamPolicy.cs:106, 109, 126`) were named `…CostMilli` but held **whole loam units** —
compared directly against `CarriedLoam` at `BuildResolver.cs:101` and subtracted at `:115`. A renderer
trusting the old name would have been wrong by 1000×. This was exactly the ambiguity GG-46 exists to
catch, and it was in the model, not the UI — the rename removed the trap at the source; the renderer
must still never trust a field's own suffix for anything not yet renamed.

### 2.3 Real gap — no mechanism exists anywhere

> **⛔ Read this row first — it resizes everything below it. Added 2026-09-03 after the design review,
> and verified directly rather than inherited.** **The player commands exactly one legion, and nothing
> in production creates a second.** Every `WorldEntityKind.Legion` construction site in `src/` is
> `e-dave-legion-1` — `WorldTemplateCatalog.cs:184` and `WorldTemplateCatalog.TwoHearths.cs:280`, one
> per template. The whole world holds **three** mobile entities on `first-light` (Dave's legion,
> `e-zomboss-band-1`, `e-wild-pack-1`) and **two** on `two-hearths`. There is no recruitment: the phase
> that would deliver it is `TurnEngine.cs:196-200`, which is `report.BeginPhase(Phases.Growth); return
> world;` in full.
>
> **Consequence, and it is not small.** Several controls in §4 are borrowed from games with dozens of
> units and are sized for a decision volume this game does not have: the turn cluster's
> cycle-to-next-unresolved (§4.6) cycles a list whose maximum length is 1; the outliner's legion group
> (§4.3) holds one row; the notification triage tiers sort a feed that is mostly economy, not orders.
> **The count is not on its own a reason to cut them** — it is a reason to decide the target number
> before speccing against it. See §8c.1, which makes that the gate.

| Missing | Note |
|---|---|
| **Recruitment — any way to gain a second legion** | `Growth` is a no-op (`TurnEngine.cs:196-200`); `TurnCalendar.cs:14`'s *"recruits arrive in pulses"* is a comment describing an unbuilt module, not behaviour |
| **Growth of any kind — the whole concept is declared and unbuilt.** Found while speccing `sector-development`, 2026-09-03, and verified: **every assignment to `DevelopmentLevel` in `src/` is a copy**, never an increase — two intel-recording sites (`Intel/IntelRecorder.cs:86, 100`), a DB read (`RpgStore.World.cs:434`) and the DTO projection (`WorldEndpoints.cs:297`). It is stored, hashed, believed, projected **and charged for** (it is an input to `LoamUpkeep.For`), and nothing can raise it. Likewise `SectorPhase.Developed` (`WorldState.cs:12`) is referenced nowhere else in `src/` | This is *why* `Growth` is empty, and it is `sector-development`'s to build. Worth stating plainly: today a sector's development level is a stat that only ever costs the player |
| **Any event telling the player they are back in supply.** Found while speccing `world-playback`, 2026-09-03, and verified: `supply.cut:` exists (`Movement/SupplyGraph.cs:58`) and **nothing reports the reverse**. `recovery:` (`:111`) is not it — it heals `Wounds` on legion members and reports a *garrison mending*, which is a different event that happens to sit nearby. So the player is told when a component is severed and never when it reconnects | **Correction to my own first reading:** plate 11 §L does *not* draw this dishonestly — it draws the sentence a player should see and annotates it *"no token exists… it must not be inferred silently, and today it is not shown at all."* That is the right behaviour for a design catalog. Adding the event is `world-wire`'s, and it is small |
| **A player verb for giving up ground.** `WorldCommandKinds.All` is `stand-fast, move, clear, claim, stance, sustain, build` (`WorldCommand.cs:36-37`) — there is no `abandon`/`cede`/`release`. The engine picks the sector to drop itself, every turn, via `LoamForecast.Weakest` (`LoamPhases.cs:133-146`) | **This is the economy's core tension, and it is currently a notification rather than a decision.** See §8c.2 |
| **A stage.** `#/world` is the only route in the app that does not redirect into the new shell | `tasks/game-gui-todo.md:1014-1018`; there is no `src/stages/world/` |
| **A HUD.** Nothing is persistent. The turn number is interpolated into the page's *description string* (`WorldPage.tsx:183`) and scrolls away with the header; `LoamGauge`'s own comment claims *"always visible, the way a city-builder shows power"* while it sits in a scrolling column with no `sticky` anywhere in the feature | |
| **A way to deselect.** `select-sector` accepts `null` (`worldSelection.ts:29`); nothing ever dispatches it. No close control, no `onPaneClick`, and Esc belongs to the system menu (`SystemHost.tsx:27`) | |
| **Any keyboard model.** Grep for `onKeyDown\|keydown\|Escape\|tabIndex\|role=` across `features/world/` returns exactly one hit: `aria-pressed` at `WorldPage.tsx:318` | |
| **Map overlays / lenses.** One boolean (`lifelines`) exists; there is no lens concept | |
| **Any art.** No `<img>`, no icon set, no terrain. Sector state is opacity arithmetic (`0.35 + 0.65 × stability/1000`, `SectorNode.tsx:49-52`), slot types are the single letters `S E M V L T ! $` (`:29-39`), danger is `"◆".repeat(n)` (`:104`), unscouted is the character `?` (`:97`) | |
| **A notification model.** One `useState` string, overwritten by each event, no dismiss and no history (`WorldPage.tsx:71, 213`) | |
| **A turn-report golden.** `world.spec.ts:91` stubs that route as a flat 404, so a playback redesign has nothing to build against | |
| **Stored standing / multi-turn orders.** `MarchResolver.cs:29-30` — *"a standing order is re-issued whole each turn"*; the client must resubmit. Resumable geometry is built; a server-side queue is not | |
| **Routes to list worlds, delete a world, or fetch a range of turns** | no route in `WorldEndpoints.cs` |
| **Loam vocabulary in the visual design.** Plate 03 was authored 2026-08-22, before loam shipped; grepping it for loam / upkeep / fade / warden / prospecting / structure returns **only "legion"**. It also draws no end-turn control, no turn-resolution playback and no notification feed | `docs/design/03-world.html` |

---

## 3. Prior art

Numbers and sources. Anything unverified is marked as such rather than repeated as fact.

### 3.1 Amplitude — the games the owner named

- **No minimap. In any of them** — Endless Legend, Endless Space 2 and Humankind all ship without
  one, and no developer statement explaining the choice could be found. Players have argued about it
  for a decade. **The zoom is the minimap.**
  ES2 manual p.12 and EL manual p.10 describe the same camera: click-drag, edge-scroll, arrow keys,
  wheel zoom, PageUp/PageDown — all four pan methods at once, with no separate "camera mode".
- **Endless Legend has three named zoom tiers** — the living world (3D), the map (simplified 2D
  cartographic), the world view (empires and diplomacy). *"If you zoom out far enough, the 3D
  Adventure Map transforms to a 2D cartographic representation which makes key points of interest
  more visible."* (EL manual p.10.)
- **And its documented failure is the one to design against:** the tiers each *hide* something the
  other shows — zoomed in gives tile output and city boundaries, zoomed out gives resource icons and
  paths — so *"you end up scrolling in and out to see special resources or tile resources or city
  boundaries."* ([Any Key To Start, 22 Apr 2015](https://anykeytostart.wordpress.com/2015/04/22/endless-legend/))
  The switch is also hard-coded to a zoom threshold with no hotkey to force it.
- **ES2 makes the system screen a zoom destination, not a modal.** *"You can zoom from a full-galaxy
  view all the way into the icy rings (and management screen) of your local gas giant within seconds
  by merely scrolling."* (PCGamesN ES2 review.) The manual describes the same screen as click-to-
  enter, so both routes exist; a source stating that explicitly could not be found.
- **One dismissal gesture, stated as a rule in the manual.** ES2 p.20: *"Like all other game screens,
  to leave the System Management screen and return to the previous screen you can either right-click
  on the screen or hit the Esc key."*
- **The End Turn control is a cluster, not a button.** EL's bottom-right wheel holds **eight**
  things (EL manual p.23): End Turn; cycle idle armies; execute planned moves; toggle the hex grid;
  toggle tile FIDSI values; the season display; other empires' colours and symbols; the game menu.
  ES2 trims it to three, and the third is the important one — **a live count of ships or fleets with
  movement remaining but no orders**, sitting next to the button it would block (ES2 manual p.24).
- **Two notification classes, player-reassignable, flushed by End Turn.** ES2 manual pp.24-25:
  important notifications pop a panel; lesser ones stay as icons on the right edge; *"in the Options
  menu you can select which notifications automatically pop up and which ones stay minimized"*; an
  open notification can be **dismissed** or **minimized**; and *"when you press the End Turn button
  all notifications will be flushed, except those which prevent you from ending the turn."*
  Amplitude's later refinement (Humankind, Vitruvian update, 2022) moved that setting **onto the
  offending notification itself**, not only into options.
- **Blocking is two-tiered and named:** *"Some notifications will appear when you attempt to end your
  turn… Others will prevent you from completing your turn until you have resolved them."* (ES2 p.25.)
- **Amplitude's own post-mortem names their mistake: the "Divided UI".** EL2 dev blog #996 (Sept
  2025) — the redesign *"broadly attempts to consolidate information, improve flow and remove the
  'Divided UI' concept"*, which they define as splitting information across different sides of the
  screen for logical reasons or to keep the centre clear, and which *"ended up being frustrating for
  players that didn't know what part of the screen to look at for information or actions."* Trade
  coverage put it plainly: *"Amplitude's 4X games have often struggled with relaying some of the
  information clearly."* Players filed exactly this complaint about ES2 in 2017 — *"even simple
  things like 'apply/cancel' options are on opposite sides of the screen."*
- **But the reconciliation matters:** EL2 players describe **EL1's "strict division into corners"** as
  the thing that made it accessible. The rule is therefore **per-corner role stability**, not
  per-corner content distribution. Corner anchoring is right; splitting *one decision* across corners
  is wrong.
- **EL2 (2025) shipped nested, lockable tooltips and it is their best-received UI feature in a
  decade.** Hold **Shift** to lock any tooltip; the lock gesture is itself a setting with three modes
  (Shift / dwell-time / middle-click) under `Settings → UI → Nested Tooltip Lock Mode`. Depth is
  unbounded. **Caveat worth carrying:** no source shows this solving *provenance of a number* — it
  solves navigation between concepts. A modifier ledger is still ours to design.
- **The most portable target Amplitude have stated**, Jeff Spock on ES2: *"We are continually making
  choices to ensure that the player won't drown under a flood of information, while still having them
  two clicks away from whatever it is that they want to know."*

**And the criticism, which is where the transferable rules actually live.** A caveat first, because it
changes how to weight all of it: **EL1 and ES2 are, on balance, *praised* for their interfaces** in
their own communities; EL2 is in live Early Access with an active feedback subreddit and is heavily
over-represented in complaints. The dominant register across professional reviews is *qualified
praise* — PCWorld on ES2: *"It's just so damn slick. Almost too slick, with Amplitude apparently
concerned more with artistry than creating an intuitive user interface at times… Sometimes Endless
Space 2 is so busy being pretty it forgets to actually convey any of the information it wants to
convey."* PC Gamer: *"a UI that might be too efficient for its own good… my first hundred turns or so
were spent making mistakes simply because I couldn't tell what the UI was trying to tell me."*

- **A quantified click audit of Endless Legend's turn boundary**, and it is the single most useful
  artifact found: pressing End Turn opens a research modal, *"immediately obscured by a modal
  announcing a new era"*, then one for new strategic resources, then one for era quests. The count:
  *"Endless Legend makes you click 4 times for each notification (minimize the interruption, click to
  open, click to act, right-click to dismiss)… and twice for each production completed"*, where
  *"a single press of the space bar could have accomplished the same thing."*
- **ES2 shipped a notification that blocked the turn, and had to patch it out.** *"You have to dismiss
  battle notifications before the game will let the turn end. It's a feature, not a bug"* — later
  fixed, patch note: *"Battle Result Notifications no longer block the turn."* **This is the direct
  argument for keeping the hard-block set tiny and reviewed** (§4.6).
- **Both failure directions coexist in the same game, and that is the real finding.** ES2 *"despite
  bombarding you with notifications for minor things like population growth, does not notify you when
  one of your systems is under siege."* A player ended a turn with *"fifteen pages of notifications"*
  while *"the AI will happily move their ships around before you have a chance to react."* The
  complaint is never simply "too many" or "too few" — **it is that notification weight does not track
  event importance.** Civ VII's indictment (§3.2) is the same sentence from a different game.
- **The absence of an empire overview is Amplitude's worst-documented friction.** ES2 *"lacks an
  overview of any kinds that easily lists the planets and ships available on your empire"*; *"even
  fleets can't be found at a glance, requiring several clicks"*; and the accepted community workflow
  is a manual sweep — *"probably make a grand circuit of your empire once every 20 turns or so by
  using the Left/Right buttons from system view."* Humankind: *"there's no big zoom out to review
  empire territories, resources and continents. No fast way to search for stuff in your territory."*
  **Taken together with the missing minimap, this is the strongest argument in the corpus for §4.3's
  outliner.**
- **Colour collision is real and players pay to fix it.** The ES2 mod *Intuitive GUI Colors* (2,697
  subscribers) exists because *"the color of the label indicating a planet is colonizable is exactly
  the same as the color indicating it is not colonizable"*. Palette-expansion mods are the
  **most-subscribed mods in both games** (≈22,600 for EL, ≈21,800 for ES2), and EL's explicitly
  recolours quest armies *"to help avoid any confusion with yellow players"*. That is a colour-coded
  information density failing in the base game, paid for by the community.
- **EL's only UI-size control is a binary toggle, and it is insufficient at 1080p.** PCGamingWiki,
  verbatim: *"There's 'Large UI' option but it's a toggle. Without it UI is too small even on 1080p, on
  a standard 4K screen UI will be too small even with Large UI enabled"* — the documented fix is a
  community patch plus `--uiScale=3 --fontScale=2` launch flags. **This is exactly what XAG 101's
  "scalable to 200%" requirement exists to prevent** (§3.2).
- **The zoom-as-information-switch cost, confirmed from a second direction.** Gaming Nexus on EL:
  *"Zooming out helps, as the interface changes to a simpler, solid-color mode with important items
  highlighted. This rather useful level is ugly, however, which sort of defeats the purpose of having
  a good-looking interface."* And *"clicking off of a unit makes your cities effectively disappear,
  also, blending into the background."*
- **The framing that reconciles the disagreements**, from a player and worth adopting outright:
  *"There are two things with UI that often get conflated. One is how the UI provides information to
  the player. The other is how the player interfaces with the UI to play the game… A game can succeed
  at one, and fail at the other."* Our current map fails **both**, and they need separate acceptance
  criteria.

**One caution on reading the mod evidence:** no minimap mod, tooltip-breakdown mod, army-overview mod
or notification-filter mod exists for any of the three games — but EL's Workshop mods are XML with
essentially no UI surface, which is why its community patch had to ship as a binary patch outside
Workshop. **That absence is evidence about moddability, not about demand.**

### 3.2 The wider genre

- **There is no named "anti-scroll principle" in game-UI writing** — the behaviour is universal, the
  articulation is not. What exists is a screen-budget framing: designers *remember* strategy HUDs as
  taking 10–25% of the screen; the measured figure across shipped RTS is **25–40%**
  ([treeform, "Strategy Game Battle UI"](https://blog.istrolid.com/blog/strategy-game-battle-ui.html)).
  The genre's four replacements for scrolling are: move the camera, swap the lens, open a bounded
  panel with its own single-axis scroll, or page/tab.
- **The one formal rule that does exist** is Xbox Accessibility Guideline 101: when text is scaled,
  the player must not be required to scroll **both** horizontally and vertically to read one UI.
- **HUD anchoring has a real consensus, and one genuinely unsettled question.** Top strip: resources
  with per-turn rates, date/turn. Bottom or bottom-left: selected-entity panel. Right edge:
  notifications and the entity outliner. Bottom-right: end turn, turn counter, map-mode selector.
  **Minimap side is contested** — AoE3:DE resolved it by shipping three HUD layouts after community
  feedback. AoE4's designer chose *vertical* resource stacking on explicit cognitive grounds:
  *"Lists are more efficient to scan vertically than horizontally… any cost comparisons would have
  the shortest possible eye movement"*, citing Hick's Law ([kevinhustler.me/aoe4](https://kevinhustler.me/aoe4)).
- **The end-turn button is a state machine.** Civ VI's icon becomes the next unresolved blocker;
  clicking it **navigates to the blocker** rather than ending the turn; `W` cycles units needing
  orders (**the `W` binding is unverified — see §8c.7**); **Shift+Enter force-ends**, though its
  reliability in Civ VI specifically is contested. Its documented failure is equally instructive —
  auto-cycle selects units *late*, after the player has already picked another one, so the wrong unit
  moves; and units can become un-cycleable blockers. The most-installed Civ VI mod adds a
  one-gesture "end turn regardless".
- **Humankind's version of the same machine is a cautionary tale:** its blocking end-turn produced a
  notorious "Turn Pending" soft-lock which Amplitude's own bug forum describes as *"not a single bug,
  but multiple different bugs that have the same symptom"* — plus a filed defect,
  *"Turn Button Shows End Turn When Moves Are Still Available"*. **If you build a blocking end-turn,
  the blocker's own correctness becomes a first-class reliability surface.**
- **The deeper argument is to not need the queue.** Soren Johnson on Old World: under
  "every-unit-moves", each turn is *"a very straightforward and often even boring decision, without
  any tradeoffs"*; making moves scarce produces *"actual guns-or-butter decisions every turn"*
  ([designer-notes.com](https://www.designer-notes.com/old-world-designer-notes-1-orders/)).
  The interaction cost is measurable: Phil Goetz instrumented **one turn** of Civilization III at
  **422 mouse clicks, 352 mouse movements, 290 key presses, 23 wheel scrolls and 18 screen pans**.
- **Lenses, not stacked overlays.** Civ VI ships **10**, strictly exclusive, on number-row hotkeys,
  with the toggle anchored **on the minimap** — and several **auto-activate from selection** (pick a
  settler, get the settler lens; build a district, get district placement). CK3 puts **11** map modes
  in the lower-right and encodes state with pattern as well as colour (counties mid-conversion render
  with dashes). ES2 instead couples its four Scan layers to **zoom depth**, which removes the picker
  entirely — at the cost that when two layers converged after an economy rework, players could not
  tell it was a bug.
- **Fog's hard part is the stale state, and there is no free option.** The canonical three states are
  unexplored / explored-but-not-visible / visible; WarCraft introduced the *"after-image of terrain
  features and structures"* that is the stale state. Civ VI rendered it as **aged parchment**, which
  made "remembered" unmistakable and generated a named complaint thread —
  *"New Fog of War makes the map harder to use"*. WarCraft's translucent dimming chose readability
  and gave up some distinctness. Civ VI's cleaner sub-rule is worth stealing: the boundary is drawn
  between **static facts** (terrain, structures — remembered) and **dynamic facts** (unit movement —
  hidden), not between old and new.
- **Notification spam has a canonical failure and no genre-native number.** Stellaris is the negative
  example — popups at screen centre interposing between cursor and target, *"Order Restored"* firing
  *"at least 3 every month"*, players reporting *"50 different pop-ups every minute"*. The community's
  own proposed fix names the right architecture: reserve popups for events **requiring action**, route
  everything else to a passive rail that auto-dismisses. **The only hard cap I could find is
  transferred from general UI practice, not from games: show at most 3, newest on top, remainder
  behind a badge.** Treat it as guidance, not evidence.
- **The recent, best-documented failure is Civilization VII.** Launched Mostly Negative with the UI as
  the central complaint; the sharpest indictment is a triage failure — the UI *"doesn't tell you
  things you need to know, continually tells you things you don't need to know… is incapable of
  differentiating between what's a bother and what's important."* Firaxis' official response put UI
  first. What actually shipped in patch 1.1.1 was small — health-bar visibility, one Trade lens,
  under-attack notifications — relative to the complaint.
- **Two hard accessibility numbers to design against.** XAG 101: PC text minimum **18 px at 1080p**
  (36 px at 4K), measured as body height, **scalable to 200%** without loss of content or meaning,
  line width ≤80 characters, line spacing ≥1.5; *"platform-provided screen magnification tools aren't
  an appropriate mitigation for small text size."* And **8–10% of male players** cannot rely on
  red/green — no essential information by fixed colour alone; supplement with symbol, shape or text.
  Our current sector cards fail both: state is opacity arithmetic and slot types are single letters.
- **WCAG 2.1 SC 1.4.13** governs any hover tooltip: it must be **dismissible** without moving the
  pointer, **hoverable** (the pointer can move onto it), and **persistent** until dismissed.
- **Empirical cover for a frank HUD over an atmospheric one:** Kristine Jørgensen's study (22
  individual studies plus a focus group, across Diablo II / The Sims 2 / Crysis / C&C3) found most
  players are *"Relativists"* who accept HUDs and overlays as conventional **provided they communicate
  clearly** — diegetic presentation is not required for immersion. This is the evidence behind GG-60's
  arbitration, and it resolves GG-28 vs GG-27/GG-46 in favour of legibility wherever the player is
  acting under any pressure.

**Recorded absences — do not fill these by inference.** No Amplitude GDC talk or dev diary about UI,
HUD, map rendering or art direction exists for any of the three games. No developer statement explains
the missing minimap. No published source describes how any 4X presents between-turn AI resolution.
No dedicated Civ VI UI postmortem exists. The "Rule of Seven" attributed to Endless Legend in a
community thread **could not be confirmed from any Amplitude source** and should not be cited.

---

## 4. The shape

Eleven decisions. Each states the rule, then the evidence it rests on.

### 4.1 The map is the stage, and the stage is the viewport

Full-viewport map under the shell's stage host. **The page never scrolls** — the GG-36 contract
(1280×720 floor) is satisfied by the stage filling it, not by a taller document. The map itself is
the only thing that pans and zooms.

This replaces `h-[620px]` inside an `overflow-auto` `<main>` (`WorldPage.tsx:222`, `AppShell.tsx:30`),
which today puts the map's own bottom edge below the fold at 720p before anything else renders, and
makes the wheel do two different things depending on where the pointer sits.

### 4.2 The camera is the navigation, and no minimap

Click-drag pan, wheel zoom, arrow/WASD pan, and a fit-to-extent control — the four-methods-at-once
model both Amplitude manuals describe. **No minimap**, matching all three Amplitude games: the two
**available** map tiers run 6 to ~18 sectors on an authored grid (`first-light` is 6 sectors spanning
a 7×3 grid; `two-hearths` is 16), which is inside the range a single zoom-out shows whole. Positions
come from the world, not a layout algorithm, and are scaled by the grid constants at
`worldViewModel.ts:9-11`.

**This decision is scoped to those tiers, and the scope is not decoration.** `WorldSizeCatalog`
declares **five** tiers — the three above `medium` (~32, ~64, ~128 nodes) are marked unavailable and
gated on `world-generator`, wave 4. Their node counts were *measured*, not guessed. **The first tier
above `medium` becoming available reopens this decision and §4.3's outliner argument together** — a
camera model sized for ≲20 nodes is a rebuild at 64, and saying so costs a sentence now.

**The zoom tiers must be strict supersets of legibility, never trades.** This is Endless Legend's
documented failure and the trap most worth avoiding: if zooming out hides tile yields while revealing
resource icons, the player oscillates. Zooming out may *simplify* (drop labels for banners, per
Battle Brothers) but must never *remove* a fact that only the other tier shows.

### 4.3 A persistent HUD with stable corner roles

Band 1, anchored, never scrolling, present at every zoom. Corner roles are fixed for the life of the
stage — this is the reconciliation of Amplitude's own two lessons: **corner anchoring is what players
praise; splitting one decision across corners is the "Divided UI" they are removing.**

| Anchor | Owns |
|---|---|
| Top strip | Empire loam **summary**: income · upkeep · **net** · stock-against-capacity. Turn number, and the calendar (week/month with its flavour — *not* a season; see §8b.7). This is `spec-loam-fe.md`'s gauge in the place its own comment already claimed — *"always visible, the way a city-builder shows power"* — with the **full per-component breakdown in the world panel** (§8b.5, amending that spec) |
| Top-left | The layer rail (identical on every stage, per `information-architecture.md` §3) |
| Right edge | The notification rail (§4.7), and beneath it the **outliner** — a live list of your legions and held sectors, each row selecting and centring its subject |
| Bottom-right | The **turn cluster** (§4.6) |
| Left edge | **The sector inspector** when one is open (§8e.1) — selection detail left, persistent empire state right, so the two never fight for the same 1280px. It docks *beside* the layer rail, not over it: the rail is a ~92px icon column and keeps its corner role |
| Bottom-left | Map controls: zoom, fit, lens picker, fog toggle — `information-architecture.md` §2.2's *"map controls cluster (zoom / fit / layers / fog)"* |

**The outliner — justification rewritten 2026-09-03, because its first one did not survive review.**
It originally rested on Amplitude's absence-of-an-overview friction (ES2 *"lacks an overview of any
kinds"*, and the community's manual *"grand circuit of your empire once every 20 turns"*). §8c.1
withdrew that: those are complaints about **40 systems and 25 fleets**, and six sectors all fit on
screen at once, so the inference did not transfer — and using it violated §3's own *"do not fill
absences by inference"* rule.

**What justifies it now, on two narrower grounds that do hold at our scale:**

1. **It is the keyboard entry point**, and the map has *zero* keyboard affordances today (§2.3). A
   spatial canvas needs a linear, focusable list to be reachable at all; that is true at six nodes and
   at sixty. This is the load-bearing reason.
2. **§8d.1 gives it a population, and §8e.3 gives it a size.** The target is **6–10 legions,
   tunable**. That is past the point where a flat list works: **the outliner needs grouping and
   filtering after all**, which the first version of this section explicitly denied ("the list is
   short by construction"). At 6–10 legions and up to 18 sectors it is indexing ~28 rows, and
   Stellaris' outliner groups and filters because that is the scale at which a human stops scanning
   and starts searching.

**And it carries a size trigger rather than a flat claim:** the no-grouping / no-filter / no-pagination
argument holds at `small` and `medium` — the only tiers `WorldSizeCatalog` marks available. The first
tier above `medium` shipping (`world-generator`, wave 4) reopens both this and §4.2's no-minimap
decision.

**The component-split case is a first-class HUD state, not a detail.** After the settlement rule, *"my
empire is fine"* can be false while half of it starves; `LoamGauge` already computes per-component
totals and the wire already carries `ComponentId` / `ComponentNet`. When territory is split, the top
strip says so plainly rather than making the player derive it from four numbers.

### 4.4 Selection opens a bounded band-2 inspector, and one gesture closes everything

Selecting a sector opens the **sector inspector** as a band-2 layer over the still-mounted, still-
selected map — already decided in `information-architecture.md` §2.2 and plate 03 §E, and never built.

**GG-61 applies with full force here.** A sector carries type, climate, phase, intel age, danger,
stability, four loam readings, four component readings, a slot list, a force list, structures and
their construction progress, and its actions. That is a dense single entity, and it will exceed a
720p-floor viewport. The shell declares a bounded height; **its body scrolls internally; the stage
behind it never scrolls to compensate.**

**One dismissal gesture, stated as a rule and applied without exception:** Esc pops one layer,
right-click on the map pane does the same. This is Amplitude's manual rule verbatim and it fixes the
current dead end where nothing ever dispatches `select-sector: null`.

### 4.5 Actions live on the map, at the point of decision

Today every verb is a text button in a sidebar and the order flow is explained in prose —
*"click a sector, then March here"* (`WorldPage.tsx:365-369`), printing a raw entity id.

The inspector owns the sector's own verbs (Claim, Build, Sustain, Ward, Scout). **Targeting happens on
the map**: select a legion, and reachable sectors are shown as an overlay on the map itself with the
route drawn and its turn cost — the pattern EL uses for colonisation, where *"the game displays digits
on top of the hexagon tiles"* only while the decision is live, and for movement, where a white line
marks this turn's reach and orange marks later turns.

Every action carries **its reason when it is unavailable** (GG-55, and plate 03 §E's *"disabled with
its reason beside it, always"*). Amplitude greys unavailable actions rather than hiding them; the
failure to imitate is AoW4, where disband is *"buried deeper in the interface"*.

### 4.6 The turn cluster: a control that knows what is unresolved

Bottom-right, and it is a **cluster, not a button** — the pattern is unanimous across EL (eight
functions in the wheel), ES2, Total War, HoMM3 and Civ VI.

It carries: **End Turn**; a **live count of legions with movement remaining and no orders** (ES2's
cheapest and best idea — the unresolved-work signal sits adjacent to the control it would block); a
**cycle-to-next-unresolved** control; and the file-orders action.

**Two blocking classes, declared** (ES2's own two-tier rule): *nag on attempt* — appears when you try
to end the turn but does not stop you; and *hard block until resolved*. **Which events fall in which
class is a declared list**, exactly as GG-53 requires for band 3, and D6 already establishes the
precedent that only one class may take a blocking layer unprompted.

**Default the hard-block list to empty and argue every addition.** ES2 shipped a battle notification
into that class — *"you have to dismiss battle notifications before the game will let the turn end.
It's a feature, not a bug"* — and eventually patched it back out: *"Battle Result Notifications no
longer block the turn."* A blocker is a promise that resolving it is worth more than the player's
next click, and that promise is usually false.

**And a force-end escape hatch exists**, because Civ VI, Humankind and the most-installed Civ VI mod
all converge on one: a player who knows what they are leaving undone gets to leave it undone.

> **⚠️ The keyboard half of it is not expressible today — found while speccing `world-turn`,
> 2026-09-03, and verified.** `useGlobalKeys.ts:25` calls `dispatchGlobalVerb(event.key)` and passes
> **no modifier state**, so `Shift+Enter` arrives as `"Enter"` and is indistinguishable from it in the
> verb registry. The `⇧⏎` binding plate 11 draws cannot be registered as drawn. Two resolutions, both
> costed in `spec-world-turn.md`: extend the keymap to carry modifiers (a shell change touching every
> stage), or bind force-end to an unmodified key. **The pointer path ships either way**, so this
> constrains the shortcut, not the feature.
>
> Second-order, same area: `registerGlobalVerb` **throws** on a duplicate (`keymap.ts:45-50`) while
> `conflictFor` (`layers/system/keybindings.ts:86-93` — *not* `shell/`, a path this document had wrong)
> only checks the eight bindable actions against *each other* —
> so a player rebind that collides with a stage-registered verb takes the stage down on mount.

**A warning we are taking from Humankind rather than discovering ourselves:** their blocking end-turn
produced a soft-lock family that their own bug forum called *"multiple different bugs that have the
same symptom"*, plus an end-turn button that showed "End Turn" while moves were still available.
**If we build a blocker, the blocker's own correctness is a testable surface, not an incidental.**

### 4.7 Two notification classes, flushed by the turn

Band 4 toasts for things requiring action; a passive right-edge rail for everything else; **the rail
flushes on End Turn except for blockers.** That last rule is Amplitude's, it is elegant, and it is
what prevents the Stellaris failure mode where a feed accumulates until players dismiss without
reading.

The category-to-channel assignment is **player-configurable**, and — per Amplitude's own later
correction — the control to change it lives **on the notification itself**, not only buried in
settings.

Civ VII's indictment is the acceptance test: an interface that *"is incapable of differentiating
between what's a bother and what's important"* has failed even if every notification is technically
correct. ES2 states the same failure from both ends simultaneously — it *"bombard[s] you with
notifications for minor things like population growth"* while it *"does not notify you when one of
your systems is under siege."* **The rule is not "fewer notifications". It is that notification weight
must track event importance**, and our loam vocabulary already has an obvious top tier: a component
that cannot pay for itself, and ground that will be released next turn.

**And the interaction cost is countable, so count it.** Endless Legend charges *"4 times for each
notification (minimize the interruption, click to open, click to act, right-click to dismiss)"* where
*"a single press of the space bar could have accomplished the same thing."* A per-turn click budget is
a legitimate acceptance criterion for this surface.

### 4.8 Lenses: exclusive, hotkeyed, and auto-activating from selection

A small closed set of map lenses — ownership, loam net flow, fade risk, supply and lifelines, intel
age, danger. **Exclusive, never stacked**, on number-row hotkeys, with the picker in the bottom-left
map-controls cluster.

**Recommended over ES2's zoom-coupled Scan View**, despite the elegance of having no picker to learn:
when a layer's identity is defined only by zoom depth, two layers converging becomes an invisible bug
— which is exactly what happened to ES2's Economy and Trade scans. Civ VI's model is the safer one,
and its best property is **auto-activation from selection**: choosing to build turns on the placement
lens without the player remembering to.

`?lifelines=true` already exists as an opt-in server cost gate (`WorldEndpoints.cs:51`) and becomes
the first lens rather than a boolean prop.

### 4.9 Fog: four states, four distinct treatments, static-vs-dynamic

The server already answers this better than the client renders it — `Unknown` / `Rumored` / `Scouted`
/ `Watched` (`FactionIntel.cs:133-140`), with `IntelAge` and `LastSeenTurn` alongside.

Each gets a treatment distinguishable **by shape and pattern, not by opacity or hue alone** — the
8–10% colour-vision figure and the current `0.35 + 0.65 × stability/1000` opacity encoding make this
non-negotiable. The prior art is unusually blunt here: the most-subscribed mods for both Endless
games are palette expansions (≈22,600 and ≈21,800 subscribers), and a 2,697-subscriber ES2 mod exists
because *"the color of the label indicating a planet is colonizable is exactly the same as the color
indicating it is not colonizable."* Colour-coded density fails at scale, and players pay to fix it. The stale states (`Rumored`, `Scouted`) must be simultaneously *readable* (you plan
against them) and *unmistakably not live*; Civ VI's parchment shows there is no free option here.
**Decided (§8.2): err toward distinctness** — remembered ground is unmistakably remembered, and the
spec owes an explicit legibility check on the stale states to pay Civ VI's known cost deliberately
rather than by accident.

**Civ VI's cleaner sub-rule is adopted:** the boundary is between **static facts** — terrain,
structures, ownership, remembered and shown — and **dynamic facts** — forces and movement, hidden.
That is a sharper line than "old vs new" and it matches what `IntelRecorder` already stores.

**One thing the redesign must branch on:** an unknown sector serialises every field at its record
default (`WorldEndpoints.cs:271-277`), so on the wire it is indistinguishable from a zeroed known one
**except by reading `intel`**. Rendering must key on `intel`, never on emptiness.

### 4.10 Every number states its unit, and no renderer guesses

GG-46 is a Tier-1 gate and this surface is where it bites hardest. Four families are in play and they
are not interchangeable:

- **per-mille `int`** — `StabilityMilli`, `PressureMilli`, `FractureIntensityMilli`, `HazardMilli`,
  `LaneProgressMilli`, and `MovementRemaining` (whose name says nothing about its unit at all);
- **whole loam units `long`** — every `Loam*` and `Component*` reading;
- **counts and indices `int`** — `DangerBand`, `DevelopmentLevel`, `IntelAge`, `WardLevel`;
- **enums-as-strings** in .NET casing (`"Watched"`, `"Held"`, `"Warband"`), not kebab.

**And one trap that was in the model, not the UI:** `StructureDef.Cost` (named `CostMilli` until
world-map W57's rename) held whole loam units under a name that said milli. The magnitude renderer
must **require** an explicit unit family and refuse without one regardless of any field's own name —
which is the enforcement GG-46 already specifies.

A number's **provenance** is a separate obligation (GG-49): *"why did my net income drop"* must be
answerable from the interface. Nested tooltips are the genre's navigation answer and EL2's is the
best-received in a decade — but no source shows nesting solving provenance, so **the modifier ledger
is ours to design**, and it should be capped at 2–3 levels of nesting with a lock gesture, per the
practitioner analysis and WCAG 1.4.13.

### 4.11 The engine's vocabulary is translated, once, in one place

Twenty-one event prefixes, three battle kinds, two calendar subjects and roughly thirty drop reasons
reach the client today; five are understood. The rest print as `dave loam.shortfall:340` and
`t3-move-e-dave-legion-1 dropped — path.not-contiguous`.

This is a **translation table**, not scattered string handling: engine token → player sentence, with
`sectorLabel()`-style humanising applied to every id (it exists at `worldViewModel.ts:198-203` and is
called in exactly one place today). GG-23 forbids the current output outright, and it is a Tier-1
gate.

**It also needs a golden**, which does not exist — `world.spec.ts:91` stubs the turn route as a 404.
A generated turn-report fixture, in the same shape as the existing byte-pinned `first-light.json`, is
the thing that makes this table testable rather than aspirational.

---

## 5. Tunables

**This feature introduces no balance numbers.** Nothing here derives from a level, nothing feeds
`P(Θ)`, and nothing changes what the turn engine computes. That is worth stating plainly rather than
manufacturing a tuning file to look thorough.

What it does introduce, and where each belongs:

| Number | Kind | Home |
|---|---|---|
| Zoom tier thresholds, min/max zoom | Structural — they define when a tier switches, and changing them changes whether the map works | `const` in the stage, with a comment saying why it is not tunable |
| Viewport floor / reference (1280×720 / 1440×900) | Structural, already declared | GG-36's contract; do not re-derive |
| Text minimum (18px at 1080p) and the 200% scale range | Accessibility standard, not a preference | XAG 101; a token, not a tunable |
| Notification category → channel assignment | **Player setting**, per Amplitude's own correction | Persisted UI settings, changeable from the notification itself |
| Nested tooltip lock gesture and dwell time | **Player setting** | Persisted UI settings |
| Which events hard-block End Turn | **A declared list, reviewed** — the GG-53 analogue | Declared in the spec, lint-enforced, not a config row |
| `?lifelines` server cost gate | Already exists | `WorldEndpoints.cs:51` |

If a later balance pass wants to change how much loam a well yields, that number is already in
`data/tuning/loam.v{n}.json` via `LoamPolicy` and this program does not touch it.

---

## 6. What this deliberately does not decide

- **Art.** The art *registry* and the designed placeholder (GG-58) are in scope; illustration is not.
  The GUI program already settled this shape — *"framed chrome now; art later, and the layout is
  already art-ready"* (D10).
- **The battle stage**, and what committing a legion into one looks like beyond the confirm that
  names what is staked (plate 03 §B already draws that).
- **The Expeditions and Pacts layers** — already built under the GUI refactor as band-2 layers,
  stage-independent, and unaffected by anything here.
- **Whether the world map replaces the tier ladder** (`world-graph-ideal.md`'s L2/L3 question). That
  is a game-design decision, not a UI one.
- **The world generator**, deliberately last of its own program's four waves.
- **Standing / multi-turn orders as a mechanic.** The UI can make re-issuing cheap; whether the server
  stores a standing order is a turn-engine decision with hashing and replay consequences.
- **Recruitment itself** — added 2026-09-03. §8d.1 makes it a *prerequisite* of this stage, which is
  the opposite of unimportant, but the mechanic belongs to `sector-development`: how legions are
  gained, at what rate, against which pulse. This program consumes the unit count; it does not design
  it. Same module as §8b.7's seasons, which is why those two should be specced together.
- **What an opponent is allowed to leak.** §8c.3 found that fixing the fog defect and moving the
  AI-reasons panel to the dev tree together silence Zomboss, since today his economy is legible only
  *through* that defect. The spec must choose deliberately; this document only names the choice.
- **~~Whether the AI-reasons panel survives.~~** *Decided §8.3 — it moves to the developer tree.* What
  is still open is the consequence: with the reasons list gone from the player surface, opponent
  legibility rests entirely on the map and the turn report, and how much of an opponent's intent those
  should convey is a design question for the spec.

---

## 7. Three things that are true and easy to get wrong

**"The UI is bad" is two failures, and they need separate acceptance criteria.** The framing comes
from a player reconciling why Amplitude's interfaces get praised and damned in the same breath:
*"There are two things with UI that often get conflated. One is how the UI provides information to
the player. The other is how the player interfaces with the UI to play the game… A game can succeed at
one, and fail at the other."* Our map fails **both** — it prints `loam.shortfall:340` (information)
*and* it makes you scroll a page to reach the playback controls (interaction) — so a spec that only
fixes the rendering has done half the job, and will feel like it.


**The current map's pure layer is good and should survive.** `worldViewModel.ts`, `worldSelection.ts`,
`turnPlayback.ts` and `commanderIntent.ts` are pure, tested, and mostly correct — the defect is
entirely in the view layer, and `WorldPage.tsx` holding 256 lines of uninterrupted JSX is the whole
story. A rebuild replaces the rendering and the interaction model; it does not need to re-derive the
fold.

**The server is not the problem.** Every gap in §2.2 names a line, and most are a field on a DTO. The
temptation in a "the UI is bad" session is to conclude the backend needs rework; it does not. It needs
five fields projected, two command fields plumbed through two files, one string added to a stance
list, and one call site for `BindAsWarden`.

---

## 8. Decided by the owner, 2026-09-03

The four questions this phase raised were put to the owner and answered. **No open questions remain.**

1. **`world-stage` owns the write-surface gaps too.** `sustain`, `build`, wardens and prospecting
   (§2.2) are folded into this program rather than left with loam. A map stage that cannot issue half
   the empire's verbs is not finished, and the server work is small and line-identified: two fields
   plumbed through `WorldCommandRequest` **and** `CommandPayload`, one string added to
   `MovementPolicy.Stances`, one production call site for `BindAsWarden`, five DTO fields projected.
   **Consequence:** `spec-loam-fe-2.md` is superseded in full and `tasks/loam-todo.md` Phase 12
   (L44–L50) stays closed — its server half is re-homed here rather than resumed there.

2. **Stale fog errs toward distinctness.** Remembered ground gets a strong, unmistakable treatment —
   the player must never mistake memory for live intelligence. The known cost is Civ VI's: a strong
   treatment can make the map harder to *plan on*, so the spec owes a legibility check on the stale
   states specifically, not just a visual-difference check. The static-vs-dynamic sub-rule in §4.9
   still applies and does most of the work here: terrain, structures and ownership are remembered and
   shown; forces and movement are hidden.

3. **The AI-reasons panel moves into the developer tree.** GG-40 — the tool's feel must not
   contaminate the game — and the panel is diagnostics by its own comment. It stays reachable behind
   the developer gate; it leaves the player surface. **Consequence:** opponent legibility now has to be
   carried by the map and the turn report, which raises the stakes on §4.11's translation table.

4. **No unlock — the World stage is available from session start.** The map is the core loop of the
   standalone game and gating it delays the thing the product is about. **Consequence:** the rail entry
   is unconditional, and `information-architecture.md` §7's *"World map (travel) — first sector
   contact"* row is superseded and should be corrected when the spec lands. GG-44 is satisfied
   trivially rather than ignored: the rail still renders from state, this entry's state is simply
   always available.

### 8b. Second round — decided 2026-09-03, after plate 11 was drawn

Drawing every component surfaced four more decisions. All four were put to the owner and answered.

5. **The loam gauge lives in both places — summary up, detail down.** A compact income · upkeep ·
   **net** · stock strip in the stage HUD, and the full per-component breakdown in the world panel.
   This **amends** `spec-loam-fe.md`'s sealed 2026-08-23 decision (*"the gauge belongs to the world
   panel, not the stage HUD"*) rather than contradicting it: that decision's reasoning was
   `resource-hub-ssot.md` §4's *separate the scopes* rule, and a HUD strip carrying **only** empire
   scope satisfies it — the rule forbids mixing the lawn's `pvz.*` sun bank with an actor's pools on
   one surface, not showing empire scope on a stage HUD. **Cost:** two surfaces to keep in sync, and
   the amendment must be written into `spec-loam-fe.md` itself, not only here.

6. **Plate 00's Sector ladder rung is rebuilt from plate 11's node.** The kit's `.sector`
   (`_kit/kit.css:377`) carries ownership on border-colour alone and fog as `opacity: .45` — the exact
   GG-27/GG-30 defect plate 11 exists to fix. GG-9 allows one canonical surface per concept, so the
   abstract rung yields to the real one. **Cheap:** 4 usages, all in plate 00, and **no web code
   references `.sector` at all** (verified by grep across `web/fusion-rpg-web/src/`).

7. **Seasons become real — and they are not this program's work.** The HUD's season slot has no field
   behind it, but the ground is far better prepared than "a new mechanic" suggests.
   [`TurnCalendar.cs`](../../src/FusionRpg.Core/World/Turn/TurnCalendar.cs) already runs a complete
   clock: a turn is a day, `DaysPerWeek` days a week, `WeeksPerMonth` weeks a month, rolled purely from
   `(turn, seed)` with per-boundary derived RNG streams, and every rate already tunable
   (`SpecialWeekChanceMilli`, `SpecialMonthChanceMilli`, `PlagueChanceMilli`). Its own comment states
   the deferral: *"Wave 1 records the rolls; the economic effects land with sector-development, which
   is the module that owns growth."* So seasons are **the effects half of an existing deterministic
   clock**, and they belong to `world-map-program.md`'s `sector-development` module (wave 3, not
   started) — not to `world-stage`. **This is a game-design feature with balance consequences** —
   Endless Legend's seasons change movement and yields — so it needs its own idea/spec pass before any
   build. Until it lands, the HUD slot shows the **real** calendar (week and month with their
   flavour), which already reaches the client as `calendar` report entries.
   **`world-stage` draws the slot; it does not invent the mechanic.**

8. **Plate 03 is split.** Its Expeditions (§C) and Pacts (§D) sections describe two layers that are
   built and shipped, and they move to a plate of their own; plate 03 is then retired, its world
   sections having been superseded by plate 11. **Cost:** a real editing pass plus a link sweep —
   `README.md`, `information-architecture.md` §12, `game-gui-map.md` and `game-gui-todo.md` all
   reference `03-world.html`.

**Also not open, recorded as decisions with their reasoning above:** the map is a stage (GG-4, plate 03 §E);
the inspector is band 2 (`information-architecture.md` §2.2); no minimap (§4.2); lenses are exclusive
and Civ-style rather than zoom-coupled (§4.8); `@xyflow/react` goes — that was already the recorded
acceptance criterion at `tasks/game-gui-todo.md:616`, *"no `@xyflow/react` import"*.

---

## 8c. The design review — 2026-09-03, four perspectives

After §8b was recorded and plate 11 was drawn, the whole design was put under adversarial review from
four lenses: a strategy-game designer, an implementation engineer, an accessibility practitioner, and
a scope skeptic. **Every finding below was verified against code before being written down here**, and
the ones that turned out to be wrong are recorded as wrong.

### 8c.1 — The design is calibrated for a game that does not exist yet

**Two reviewers reached this independently, from opposite directions.** It is the review's central
result and it resizes §4.

The player commands **one legion** and cannot gain another (§2.3's first row, verified). So:
`cycle-to-next-unresolved` cycles a set of maximum size 1; the live unresolved count has a maximum
value of 1; the outliner's legion group holds one row; the force-end escape hatch escapes a hard-block
list §4.6 itself defaults to **empty**; and four of §4.8's six lenses re-present facts
`SectorNode.tsx:88-130` already draws on all six nodes at once.

**The distinction the design failed to make, and which now governs it:**

| Borrowing | Transfers at any scale | Verdict |
|---|---|---|
| **Presentation rules** — corner-role stability, one dismissal gesture, disabled-with-reason, no colour/opacity alone, declared unit families, flush-on-End-Turn, hard-block-defaults-empty | **Yes.** These are about *legibility and consistency*, which a 6-node map needs exactly as much as a 2000-tile one | **Keep** |
| **Interaction patterns** — outliner, unresolved count, cycle control, force-end hatch, six-lens system, per-category notification settings | **No.** Every one is an answer to *volume* — 40 systems, 30 units, fifteen pages of notifications | **Early, not wrong** |

**This document imported the second class without checking our unit count, and in one place contradicted
its own evidence rule to do it:** §3 warns *"recorded absences — do not fill these by inference"*, then
takes Endless Space 2's *"lacks an overview of any kinds"* — a complaint about 40 systems and 25 fleets
— as *"the strongest argument in the corpus for §4.3's outliner."* That inference does not survive six
nodes that all fit on screen at once. **The outliner's justification is withdrawn**; it needs a new one
or a trigger condition.

**Volume-dependent features were going to be given trigger conditions here. §8d.1 removed the need:
the owner decided to build recruitment first**, which satisfies the main trigger by plan rather than
leaving it pending. Read §8c.1 as *"this is why the sizing question had to be asked"*, not as a list of
things to cut — §8d.1 answers it, and the answer is to give the game the units the interface was
designed for.

The one trigger that remains live is map size: `world-generator` shipping a tier above `medium`
(`WorldSizeCatalog.cs` declares five tiers, three gated on it) is what would make the *camera* model
and the outliner's no-grouping/no-filter argument need revisiting. §4.2's "6 to ~18 sectors" should
carry that qualifier rather than being stated flat.

### 8c.2 — The economy's core tension is a notification, not a decision

`LoamPhases.Pressure` picks the sector to release itself, every turn, via `LoamForecast.Weakest`
(`LoamPhases.cs:133-146`). There is no `abandon` / `cede` / `release` command kind
(`WorldCommand.cs:36-37`). Yet plate 11 §K.4 draws a panel offering *"Give up Hollowmoor instead"* and
§H.1's blocked state reads *"Choose what to release."*

**The engine does not let you choose.** Shipped as drawn, that copy is a lie the player catches on
their first shortfall. Two honest resolutions, and they are not equivalent:

- **Cheap:** change the copy to *"here is what will be released, and here is what would stop it."*
  Truthful, ships now, and the tension stays a forecast.
- **The game:** add a cede order and make `Weakest` a *default the player may override*. This is the
  decision the whole economy is about, and it is the one verb that would make the map a strategy game
  rather than an economy viewer.

This was recorded as a **real gap** in §2.3, not a wiring gap: no mechanism exists.

### 8c.3 — Two right decisions, taken together, silence the opponent

§8.3 moved the AI-reasons panel to the developer tree. §2.2 lists the null-`SectorId` fog leak as a
defect to fix. Both are correct alone. Together they remove the last channel through which an opponent
is legible — because today you can only watch Zomboss's economy fail *by accident*, through that very
leak (`BattleReporting.cs:36`, `LegionSupply.cs:98`, `LoamPhases.cs:119, 141` all pass a null
`SectorId` and are therefore shown to every viewer).

**The spec must decide deliberately what an opponent may leak**, rather than inheriting whatever
survives a bug fix. A second line decides it: `VisibleTo` gates on *"have I ever seen this sector"*,
not *"can I see it now"* (`WorldEndpoints.cs:215-219`) — so ground scouted on turn 6 still reports live
battles on turn 80, which contradicts §4.9's own static-vs-dynamic rule.

**Correction to §2.2:** the fog defect has **three** call sites, not two. `MovementPhase.cs:123-124`
schedules `Arrival` with detail `ArrivedAtSectorId ?? OnLaneId ?? ""`, so an arrival ending mid-lane is
filtered out for everyone exactly as `halt` is.

### 8c.4 — The FE contract is the blocker, not the server

§7 says *"the server is not the problem."* That is true and it hid where the problem is.

- **`SectorView.typeId` is `number`; the wire's `TypeId` is `string`** (`contract/types.ts:272` vs
  `WorldDtos.cs:66`). That is a **narrowing**, and `game-gui-map.md:142` puts narrowing behind a
  contract version bump **plus an ADR**. §4 asserted the design fits the free additive path without
  checking the six fields it names. **The first `adaptSector` cannot be written until this is decided.**
- **The contract holds one world entity.** `SectorView` (6 fields) is all of rung 9; `adapt.ts` has no
  world code; there is no `LaneView`, `LegionView`, `SlotView`, `ForceView` or turn-event view. All
  additive and free *by rule*, but it is a module, not a field.
- **The guard does not cover it.** `contractGuard.ts:55` scans `stages/`, `layers/`, `ui/`, and `:78`
  matches only `from "@/lib/bus`. A rebuilt `stages/world/` importing `@/features/world/worldTypes`
  **passes the guard while violating the rule.** The design's central FE claim has no gate behind it.

**Costs §7 under-priced, all verified:** the world fixture is byte-pinned and consumed by **seven**
files, so projecting five fields is a re-bless plus a sweep (the word "golden" did not appear in §7).
"Two command fields through two files" is **three files and five sites**, two of which fail *silently*
— `ReadCommandRow`'s own comment says *"that is exactly how `stance` was found missing."* And the
calendar §8b.7 puts in the HUD is on **neither** `WorldStateDto` nor `WorldHeaderDto` — only the
turn-report route — so it is a sixth missing projection, and the client cannot derive it because
`DaysPerWeek`/`WeeksPerMonth` are server tunables.

**Also corrected:** §2.2 called prospecting *"blocked by one line."* **Overstated.** Adding `"dowse"`
to `MovementPolicy.Stances` leaves `BudgetFor` (`LaneCost.cs:38-42`) with no `dowse` arm, so a dowser
silently receives the full march budget; `Prospecting.Reveal` still has no production caller; and no
DTO carries the revealed set. **Four changes.**

**Clean pass, worth recording:** determinism and replay are safe — old `payload_json` rows deserialize
with new members null, `WorldCanonical` never hashes commands, and a stored order with no amount
refuses exactly as today. No `decisions.md` lock is contradicted; no guard is violated.

### 8c.5 — Accessibility: the design is right and the drawing is not

The *rules* pass. Colour-independence is genuinely well done — ownership, fog, lanes, supply and the
will-release warning each carry border geometry, pattern, glyph **and** a word, and would survive a
greyscale print. GG-55 is uncommonly well honoured. The designed modifier ledger meets all three
WCAG 1.4.13 obligations plus a keyboard route.

The *rendering* fails the standards this document cites by name:

- **58 raw-`px` font sizes, 52 of them at 8–9px**, in a design whose §3.2 cites XAG 101's *"18 px at
  1080p, scalable to 200%"*. Because the tokens are `rem` and these are literal `px`, a 200% scale
  **inverts the hierarchy** — token text doubles, the 8px tier does not. The GG-46 unit-family badge,
  the Tier-1 gate's entire on-screen expression, is 8px superscript.
- **`--faint` is used as fact-bearing text in ~25 places** against its own token comment —
  `tokens.css:22`: *"decorative only, never body text"* — computing 3.22 on `--panel` and 2.89 on
  `--panel-raised`.
- **The band-2 scrim covers the band-1 HUD.** `.scrim` is `z-index: var(--band-panel)` = 200;
  `--band-hud` = 100. Opening any inspector drops `--text` on the rail from 14.08:1 to **2.12:1** and
  the turn cluster's blocker reason to 1.50:1. **This is in the shipped kit, not the plate** — so it
  is a question for the whole GUI: *is band 1 exempt from a side-panel's scrim?* There is no third
  option, and no figure in the plate draws the two together, which is why nobody caught it.
- **§8.2's stale-fog decision needs a number, and the number currently fails.** The wash was capped by
  eye at 13% / 18% *to protect legibility*; the arithmetic says 13% already puts `--muted` at **3.98**
  — below AA — for the state line, the ownership word, and the *"who stands here is not known"* strip.
  **The decision is not at fault and is not reopened:** distinctness is carried by border geometry,
  silhouette and the date stamp, not by the wash. Promoting stale-node body text from `--muted` to
  `--text` (9.34 / 8.13, both AA) keeps everything §8.2 bought.

### 8c.6 — What the review found load-bearing and would not change

Recorded because a review that lists only faults leaves nobody able to tell what is structural:

- **The pooled-component economy is the answer to the late-game "click every planet" failure**, and it
  is already in the engine. `TerritoryComponents` makes the empire N *purses*, not N sectors — at turn
  80 with fourteen sectors the player manages two or three decision objects. §4.3's component-split HUD
  state is the correct primary readout and must not be traded down to a per-sector list.
- **The corner-role reconciliation** (§4.3) — *corner anchoring is what players praise; splitting one
  decision across corners is the "Divided UI" Amplitude is removing* — resolves genuinely contradictory
  evidence into a usable rule.
- **Warning and act share `Weakest`**, so the forecast and the event cannot disagree — an engineering
  property that licenses stating the warning bluntly.
- **Hard-block defaults to empty**, with ES2's shipped-then-retracted battle blocker as precedent.
- **The translation table as one table with a golden**, not per-prefix handling — per-prefix is exactly
  how today's 5-of-21 state arose, dead `attrition:` branch included.
- **Unit families declared at the boundary**, proven by `CostMilli` being wrong by 1000×.
- **Removing `@xyflow/react` now**, and it is cheaper than feared: `worldViewModel.ts` is already
  library-agnostic (plain `{x,y}` from an authored grid, no auto-layout to replace), and it appears in
  four production files. Every component built against `NodeProps`/`Handle` is a component written
  twice.

### 8c.7 — Consistency and counting pass, 2026-09-03

A fifth review pass was planned as three agents (verify §3's prior art, audit the plate's facts,
cross-check §4's internal consistency). **All three died on an API error and produced nothing**, so
what follows was done by hand and covers less. Recorded honestly, including what is still unchecked.

**Counts verified by counting, not estimating** (the evidence rule this document is meant to obey):

| Claim | Verdict |
|---|---|
| 14 slot kinds | **Correct** — `anomaly, essence-deposit, hazard, lair, market, material-seam, rootbed, seat, shard-vein, shrine, spire, tear, vault, wildland` |
| 6 lane types | **Correct** — `corridor, deep, gated, ley, one-way, rift` (`warded`/`severed` are lane *state*, not types) |
| 21 event prefixes | **Correct.** `arrival:` and `halt:` do not appear as literals because they are built by concatenating `TurnEventKinds`; `"calendar:"` is an RNG stream name and `"https:"` a comment URL, both correctly excluded |
| "~30 drop reasons" | **Understated. Actually 37** — 33 bare plus 4 carrying an argument. §2.1 corrected |
| Five map size tiers, three unavailable | **Correct**, and stronger than stated: the 128-node tier is measured at 0.6–0.7s and needs a Tarjan-first optimisation before it could ship at all |
| The plate's "real" numbers (`carryPerBearer` 200, `burnPerMember` 10, `waystationCostMilli` 300, `wellCostMilli` 200, range 3 hops) | **All correct** against `data/tuning/loam.v1.json`, and the `…Milli` trap is handled right — the waystation renders as `300`, not `0.3` |

**Internal contradictions found and fixed:**

1. **§4.3's outliner justification contradicted §8c.1.** §8c.1 withdrew the ES2-derived argument; §4.3
   still asserted it verbatim. Rewritten to rest on the two grounds that survive at our scale — it is
   the keyboard entry point for a canvas with no keyboard affordances, and §8d.1 gives it a population
   — plus an explicit size trigger.
2. **§4.2 cited `worldViewModel.ts:9-11` for a sector-count claim**; those lines are pixel-grid
   constants and do not support it. Citation corrected and the no-minimap decision explicitly scoped to
   the two available tiers.
3. **§6 was missing two entries** that §8c/§8d created: recruitment (a prerequisite this program does
   not design) and what an opponent may leak.

**Cross-checked against `information-architecture.md` and found consistent:** §4.8's number-row lens
hotkeys are exactly what IA §5 sanctions (*"`1`–`9` · Stage-specific hotbar · owned by the current
stage"*), and §4.3's band-1 assignment matches IA §4.

**One further IA divergence to reconcile at spec time:** IA §2.2 lists the world HUD as *"Turn or tick
readout · selected legion · map controls cluster (zoom / fit / layers / fog)"*. §4.3 adds the loam
strip, the notification rail, the outliner and the turn cluster. That is an **extension, not a
conflict**, but it is the second IA row this program supersedes (the first was §8.4's unlock) and both
should be corrected in the change that registers `world-stage`.

**§3's two standards claims — verified against primary sources, 2026-09-03.** These are the most
load-bearing citations in the document (§4.10's whole argument, and the accessibility findings in
§8c.5 rest on them), so they were checked first.

- **XAG 101 — CONFIRMED, and verbatim in several places.** Every figure §3.2 attributes to it is
  correct: **PC/VR 18 px at 1080p, 36 px at 4K** (console 26/52); sizes **measured as body height** —
  *"the sum of the number of pixels in the descender space, the x-height space, and the ascender
  space"*; the 2:1 anti-aliasing rule for which edge pixels count; **line width ≤80 characters** (40
  CJK), *"measured when text is resized to 100 percent"* and excluding spaces; **line spacing ≥1.5**;
  paragraph spacing ≥2× line spacing; letter spacing ≥0.12× and word spacing ≥0.16× the font size; a
  sans-serif option and a non-stylised alternative to any stylistic face. Two quotes are exact:
  *"Players should be able to resize text up to 200 percent of the minimum font sizes… without the
  loss of content, functionality, or meaning"* and *"Platform-provided screen magnification tools
  aren't an appropriate mitigation for small text size."* And the scroll rule reads, verbatim:
  *"When text is scaled, the player isn't required to scroll both horizontally and vertically to read
  text within a single UI. (Scrolling in one direction is OK.)"*
  **One requirement this document had missed, and it sharpens §8c.5:** *"The text contained inside
  icons and glyphs should also meet the minimum default text size"* and *"Icons/glyphs should also
  scale with text scaling up to 200 percent."* Plate 11 encodes a great deal in glyphs — the slot-kind
  letters, `"◆".repeat(n)` for danger, the role pips, the strength-ladder rungs — so the 18px floor
  applies to those too, not only to prose. Source:
  [XAG 101](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101).
- **The Endless Space 2 manual quotes — CONFIRMED, verbatim, from the primary PDF.** The manual was
  downloaded and its text extracted, so these are the actual sentences and not a search-index
  reconstruction. §4.4, §4.6 and §4.7 all rest on them and all three hold:
  - *"Like all other game screens, to leave the System Management screen and return to the previous
    screen (in this case the Galaxy Map) you can either right-click on the screen or hit the Esc key
    on your keyboard."* — and **"Like all other game screens" is in the source**, which is what makes
    it a general rule rather than one screen's behaviour. §4.4's one-dismissal-gesture decision is
    correctly grounded.
  - *"When you press the End Turn button all notifications will be flushed, except those which prevent
    you from ending the turn."* — §4.7's flush rule, exact.
  - *"Important notifications will automatically pop up and open a panel. Lesser notifications will
    remain as icons on the right side of the screen until you open them. In the Options menu of the
    game, you can select which notifications automatically pop up and which ones stay minimized."* —
    §4.7's two classes **and** their player-configurability, exact.
  - *"This icon indicates the number of ships or fleets that have remaining movement points but no
    orders to move."* — §4.6's live unresolved count, exact. This is the borrowing §8c.6 called the
    cheapest good idea in the document.
  - The blocking two-tier rule, exact: *"Some notifications will appear when you attempt to end your
    turn… Others will prevent you from completing your turn until you have resolved them. **For
    example, a battle notification will prevent you from completing the turn until the battle is
    resolved.**"*
    **This last sentence corroborates §4.6's warning better than the document claimed.** The manual
    documents a *battle notification* as a hard blocker — and that is precisely the blocker ES2 later
    patched back out (*"Battle Result Notifications no longer block the turn"*). So the shipped-then-
    retracted example is not an anecdote about some notification; it is the one the manual itself held
    up as the canonical hard block. §4.6's *"default the hard-block list to empty and argue every
    addition"* is the right lesson, and the evidence for it is stronger than stated.
  - Camera (PageUp/PageDown) and the Spacebar Scan View toggle both confirmed in the same document.
- **Civ VI's end-turn state machine — PARTIALLY CONFIRMED, and the failure is mine.**
  **Confirmed:** the button's icon changes to represent what still needs attention (unit orders,
  research, production), and when it shows `!` clicking it *navigates to the blocker* — zooming to the
  next unit needing orders — rather than ending the turn. That is the mechanism §4.6 borrows and it is
  real. **Not confirmed:** that **`W`** is the cycle key. Sources describe mashing the **space bar** to
  skip units instead, and no source I found binds `W` to it. **Contested:** Shift+Enter force-ends
  reliably in Civ V, and multiple threads report it behaving inconsistently in *Civ VI* specifically.
  §4.6's design point does not depend on the exact keystroke — a cycle control and a force-end escape
  both exist — but the document should stop asserting `W`, and the force-end precedent is weaker than
  written.

- **WCAG 2.1 SC 1.4.13 — CONFIRMED as to substance; primary source blocked.** Level **AA**, three
  requirements: **dismissible** (without moving pointer or keyboard focus), **hoverable** (the pointer
  can move onto the content), **persistent** (visible until dismissed, until the trigger is removed, or
  until the information is no longer valid). `w3.org` returns 403 to automated fetch, so this rests on
  several independent secondary sources agreeing rather than on the normative text —
  [W3C WAI understanding page](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html)
  (blocked), corroborated by [WCAG.com](https://www.wcag.com/authors/1-4-13-content-on-hover-or-focus/)
  and [Deque University](https://dequeuniversity.com/resources/wcag2.1/1.4.13-content-on-hover-or-focus).
  Good enough to design against; worth one manual read before it is quoted in a spec.

**Plate 11 repairs applied, 2026-09-03.** Seven of the ten defect classes from §8c.5 are fixed in
`docs/design/11-world-stage.html`, by hand after six agent attempts died on API errors. Verified after
each edit: tags balanced, five style blocks intact, **zero** colour-hex outside them, and the plate
re-rendered in Chrome.

| # | Defect | Fix |
|---|---|---|
| 1 | 58 raw-`px` font sizes, 52 at 8–9px | **All 58 → `rem` tokens**, the 5 SVG `font-size` attributes → `em`, and the six meaning-bearing selectors promoted to the 12px floor. Raw-px count is now **0**, so a 200% scale no longer inverts the hierarchy |
| 2 | `--faint` as fact-bearing text (below AA) | **28 declarations promoted to `--muted`** (3.22 → 5.51 on panel). The 10 remaining uses are borders, a background gradient and two ornaments — deliberately left |
| 3 | §J encoded fog by opacity, contradicting §C | §J's four states now carry border **geometry** + pattern + a distinct silhouette for Unknown, matching §C. The correction is commented in place |
| 4 | Two key conflicts | `WASD`-pan removed (it collided with `W`-cycle, which is bound in six places); the arrows already pan. The HUD's `Menu [Esc]` chip removed — Esc pops the topmost layer first and only reaches System on an empty stack |
| 5 | §M.1's ledger drew a modifier with no field | The phantom *"this month is heavier ×1.15"* row is now the **faction upkeep handicap** (`1150‰`), which is real, is a declared balance lever, and which the engine already narrates as `loam.handicap:1150`. The five rows are now exactly `LoamUpkeep.For`'s five arguments, in order, and the arithmetic is unchanged |
| 6 | "Ward" named two mechanics | The sector action is now **"Bind a warden here"**; the road action stays "Ward a road". The engine already separates them (`WardLevel` on a lane, `WardenBindingId` on a sector) and the lens auto-activation now names the right verb |
| 9 (part) | Generated names in the Latin-only display face | `.ws-nm`, `.ws-svg .ws-n-l` and `.wsd-node b` moved to `--font-ui`, which carries `var(--cjk)`. Pointer targets raised to ≥32px and the four ellipsis sites given a visible defect marker when a `title` fallback is absent — both in a scoped remediation block, deliberately *not* edited into the shared kit |
| 10 | The "closed set of six" lenses had a seventh | **Placement is now declared a transient targeting overlay**, not a lens — no picker slot, no hotkey, alive only while the verb is. Same class as §E's range overlays, which keeps the six honest |

**Three defect classes remain unfixed**, and they need markup work rather than CSS: §8c.5's missing
HUD-plus-inspector figure (the composition whose absence hid three findings), the ~30 native `title`
attributes that are the sole carrier of a name, and the fixed-px grid columns that clip at 200%.

**Still unchecked, and it should not be claimed otherwise:**

- **§3's prior art, minus the two standards above.** The Amplitude manual quotes and their page
  numbers, the "Divided UI" post-mortem, Civ VI's end-turn state machine, the Jørgensen study, the
  screen-budget figure, the mod subscriber counts. Still the largest remaining hole, and §4 cites it
  more than anything else. **Three agent attempts died on API errors**; the standards claims were
  done by hand instead because they were the most load-bearing.
- **The plate's DTO and `file:line` citations**, section by section. The counts above check out, which
  is mildly reassuring, but the ideal doc's own citations ran a 15% error rate from the same
  production method.
- **Vocabulary drift across plate sections** — four agents drew it independently, and the plate is
  player-facing, so `sector`/`node`/`ground` and `legion`/`force`/`entity` inconsistency is a real
  defect rather than a nit.

---

## 8d. Decided after the review — 2026-09-03

Four decisions, all owner-made, all with consequences that change the program's shape.

### 8d.1 — Several legions by turn 40. **Recruitment is built first.**

The answer to §8c.1's sizing question is *"give the game the units the interface was designed for."*
So `sector-development`'s recruit pulses — `world-map-program.md` wave 3, never started, and the module
`TurnCalendar.cs:14`'s *"recruits arrive in pulses"* comment already points at — become a
**prerequisite of the world stage, not a later module.**

**Consequences, and they are the largest in this document:**

- **The sequencing inverts.** World-map-program wave 3 (or at least its recruitment half) lands
  *before* `world-stage` builds. This is the opposite of what §9 assumed.
- **§8c.1's "early, not wrong" verdict is retired for the interaction features.** The outliner, the
  live unresolved count, cycle-to-next-unresolved, the notification triage tiers and the six-lens
  system stop being speculative the moment a second legion can exist. They are designed against the
  game as it will be, which is the correct thing for an idea doc to do.
- **`Growth` stops being a no-op** (`TurnEngine.cs:196-200`). Whatever fills it is a hashed,
  replayed, seeded mechanic — it must be pure over `(turn, seed)` like `TurnCalendar` already is.
- **The force-end escape hatch earns its place**, because a hard-block list against several legions
  will eventually be non-empty even though §4.6 defaults it to empty.
- **It is still not this program's design work.** `world-stage` consumes the unit count; it does not
  design recruitment. That spec belongs to `sector-development`, alongside §8b.7's seasons — which is
  the same module, which makes the pairing natural rather than coincidental.

### 8d.2 — The cede order is added

`Weakest` becomes a **default the player may override**, not a verdict. This is the verb §8c.2 said
would turn the map from an economy viewer into a strategy game, and it resolves the lie plate 11 §K.4
and §H.1 currently draw.

**Consequences:**

- A **new command kind** — the first since `sustain` and `build`, and it must be plumbed through all
  five sites §8c.4 names, `CommandPayload` included, or it is lost in the reveal round-trip exactly as
  `stance` once was.
- **`LoamPhases.Pressure` changes behaviour, so a `RulesetVersion` bump is required — but the golden
  re-bless this bullet originally asserted probably is not.** Corrected 2026-09-03 while speccing
  `world-commands`, by testing the constraint rather than inheriting it (design-gate evidence rule 4:
  *"'this would move the goldens' is a claim — run the suite first"*). `StateHasher.Hash` reads
  `WorldCanonical.Write(world)` and nothing else (`Turn/StateHasher.cs:17`), and `RulesetVersion` is
  not in the canonical form — so with no cede order filed, `Weakest(…, ceded: null)` is byte-identical
  to today's behaviour and no golden should move. **The spec budgets for a re-bless but rules that a
  moved hash with no cede order filed is a defect, not a golden to bless**, following `decisions.md:103`
  where exactly that call was made and zero goldens moved.
- **The forecast and the act must still agree.** Today they cannot disagree because both read
  `Weakest` (`LoamForecast.cs:19` ← `LoamPhases.cs:138`) — §8c.6 lists that as load-bearing. An
  override must preserve it: the player's choice becomes an *input* to the shared function, never a
  second code path that computes the answer differently.

### 8d.3 — Band 1 is exempt from a side-panel's scrim

A band-2 layer scrims the **stage**, not the HUD. This fixes the 2.12:1 finding in §8c.5 and honours
§4.3's *"anchored, present at every zoom."*

**Consequences:**

- **This is a kit-wide change, not a world-stage one — and it is latent, not live.** Corrected
  2026-09-03: `.scrim` is `z-index: var(--band-panel)` = 200 against `--band-hud` = 100 in
  `_kit/kit.css:401`, but **the shipped web has no `.scrim` class at all** (grep of
  `web/fusion-rpg-web/src/` returns nothing; the Lawn records deliberately having none). So the
  2.12:1 regression lives in the **design kit and the plates**, and would be faithfully reproduced by
  the first web panel that scrims. **That is why the GG-5 band-table amendment has to land in
  `game-gui-principles.md` before that panel is built, not after** — this is the rare case where
  fixing the rule costs nothing because nothing has implemented it yet.
- **It touches a Tier-1 rule's mechanics.** GG-5's band table is what makes stacking and input
  "mechanical rather than per-screen judgement calls" — adding "band 1 is not scrimmed by band 2"
  is an amendment to that table and should be recorded in `game-gui-principles.md`, not only here.
- **The plate owes the drawing that would have caught it.** No figure shows the HUD and an open
  inspector together; §8c.5 notes that single missing composition would have surfaced this, the
  380px + 224px right-edge collision, and the GG-61-with-HUD question at once.

### 8d.4 — The full scope stands. Slicing happens in the plan, not in the idea.

The review's scope skeptic proposed a ~25–40 task minimum slice gated on a ten-turn playtest.
**Rejected as a scope reduction, adopted as a planning technique.** The owner: *"we will make spec and
plan with multiple slice to build — this is idea phase, we need solid idea first."*

So: **§4 stays whole.** §8c's findings sharpen it — they correct what was wrong (§8c.2's missing verb,
§8c.4's contract narrowing, §8c.5's type tier), withdraw one justification that did not survive
scrutiny (the outliner's, §8c.1), and price what was under-priced. They do **not** shrink it. The
question *"what ships first"* is a `/plan` question and gets answered there, against a complete idea,
rather than being smuggled into the idea by cutting sections.

**This is the right call for a reason worth stating:** an idea doc trimmed to the first slice cannot
tell a later session what the destination was, and the trimmed parts come back as re-derived guesses.
The trigger conditions in §8c.1 were an attempt to have it both ways; §8d.1 made most of them moot.

---

## 8e. Decided after the audit-completion pass — 2026-09-03

Four decisions, all owner-made. Two of them change §4 text, and one changes a claim §4.3 had only just
been rewritten to make.

### 8e.1 — The sector inspector docks **left**

Selection detail on the left, persistent empire state on the right. This resolves the collision §8c.5
found and never priced: the inspector (380px) and the outliner (224px) were both right-anchored,
claiming ~620px of a 1280px floor.

- It docks **beside the layer rail, not over it** — the rail is a ~92px icon column and keeps its
  corner role, so §4.3's fixed-role rule survives intact.
- It matches the genre convention the prior art already documents: the selected-entity panel and the
  outliner sit on **opposite** edges (Stellaris, Civ VI, Total War all do this).
- **Cost, stated honestly:** it introduces one asymmetry into the corner-role table — the left edge now
  has a conditional occupant. That is a smaller price than either alternative, both of which made a
  band-1 surface react to a band-2 layer, which is exactly the coupling §8d.3 just ruled out.
- **It unblocks the figure the plate still owes** — §8c.5's missing HUD-plus-inspector composition
  could not be drawn until this was decided.

### 8e.2 — Move the world DTOs to `lib/bus/world.ts` **and** widen the guard

Both fixes, not one. `contractGuard.ts:55` scans `stages/`, `layers/`, `ui/` and `:78` matches only
`from "@/lib/bus`, so a rebuilt `stages/world/` importing `features/world/worldTypes` would pass the
guard while violating the rule it exists to enforce.

- **Moving them** makes the existing guard bite with no guard change, and puts the world where every
  other domain's DTOs already live. The world stops being the exception — which is the same root cause
  as its hex-guard exemption and its GG-7 reachability exemption.
- **Widening the guard** closes the *class* of defect rather than this instance: any future
  feature-local DTO file is caught, not just this one.
- This lands in the FE contract module, which §9 already makes spec module 1.

### 8e.3 — The legion target is **6–10, and it is tunable**

Not just a number — a **tunable**, which puts it in `data/tuning/` per this document's own §0.12
(*"the balance surface is data"*). A recruitment rate that a balance pass would touch does not belong
in a `const`.

**This retires §8c.1's central finding and invalidates a claim §4.3 had only just been rewritten to
make.** Consequences, in order of how much they change:

1. **The outliner needs grouping and filtering after all.** §4.3's rewritten justification said the
   list was "short by construction" and needed neither. At 6–10 legions plus up to 18 sectors it
   indexes ~28 rows, which is past the point where a human scans rather than searches. Corrected in
   place. *This is the second time this section's justification has moved — worth noting, because it
   is the part of §4 that has been least stable under scrutiny.*
2. **The volume-dependent controls are now fully justified**, not merely "early": the unresolved count
   has a real maximum, cycle-to-next-unresolved walks a real set, the notification tiers sort a real
   feed, and the force-end hatch escapes a list that will genuinely fill.
3. **Recruitment's tuning row is `sector-development`'s to author**, not this program's — but the
   *target* is recorded here because §4's controls are sized against it.

### 8e.4 — Spec everything; order is the implementer's call

The owner: *"spec every thing, do as any order you want."* So both `world-stage` and
`sector-development` get specced, and the sequencing question §9 raised is answered by being dissolved
rather than decided.

**What that means in practice, and why it is a reasonable answer rather than a dodge:** the two
programs' dependency runs through *content*, not *contracts*. `world-stage`'s FE contract module, the
`typeId` ADR, the translation table and the SVG camera depend on nothing recruitment produces.
Recruitment's rate, cost and pulse depend on nothing the stage draws. Only the outliner's shape and
the turn cluster's counts sit on the seam, and §8e.3 has now fixed that seam with a number. So the two
can be specced in either order, or at once, without either waiting.

---

## 9. Next step

`/spec` — capability maps and module specs for **both** `world-stage` and `sector-development`
(§8e.4: spec everything, order is the implementer's call), **sliced** per §8d.4. Nothing in this
document authorizes a build.

**The two programs can be specced in any order or at once**, because their dependency runs through
content rather than contracts — see §8e.4. `sector-development` owns recruitment (§8d.1) and seasons
(§8b.7); `world-stage` owns everything in §4.

**Two things the spec must settle before any rendering work**, both from §8c.4 and both of the kind
that get harder every day they wait:

1. **The FE contract module is module 1.** Write `SectorView` (corrected), `LaneView`, `LegionView`,
   `SlotView`, `ForceView` and a turn-event view against the byte-pinned `first-light.json`, and
   **settle `typeId`'s type in an ADR in the same change** — it is a narrowing, which
   `game-gui-map.md:142` puts behind a contract version bump. The repo's own words:
   *"getting a field wrong is cheap while nothing binds to it and expensive once eleven modules do."*
2. **Decide whether the world DTOs move to `@/lib/bus/world.ts`**, so `contractGuard` actually covers
   them. Today a rebuilt `stages/world/` importing `features/world/worldTypes` passes the guard while
   violating the rule it exists to enforce.

**The dependency §8d.1 created, and how §8e.3 defused it:** `sector-development`'s recruitment half
produces the unit count several of §4's controls are sized against, which is why §9 originally said it
should be specced first. §8e.3 fixed that number at **6–10, tunable**, so the seam is now a recorded
constant rather than an unknown — and both programs can proceed independently (§8e.4).

Work now queued by §8b's decisions, none of it blocking the spec:

- **Rebuild plate 00's Sector rung** from plate 11's node and retire `.sector` from the kit (§8b.6).
- **Split plate 03** — Expeditions and Pacts to their own plate, 03 retired, links swept across
  `README.md`, `information-architecture.md`, `game-gui-map.md` and `game-gui-todo.md` (§8b.8).
- **Seasons need their own idea/spec pass** under `world-map-program.md`'s `sector-development`
  module — **not** under `world-stage` (§8b.7). This program draws the calendar slot; it does not
  design the mechanic.

Three further things the spec phase inherits as work, not as questions:

- **The plate is stale and needs a pass.** `design/03-world.html` predates loam entirely and draws no
  turn loop. It is the visual reference the stage will be built from, so it needs the loam vocabulary,
  the turn cluster, the notification rail and the four fog treatments before it can serve that role.
- **`information-architecture.md` §7's World unlock row is superseded** by §8.4 and should be corrected
  in the same change that registers this program.
- **A generated turn-report fixture does not exist** (`world.spec.ts:91` stubs that route as a 404).
  §4.11's translation table cannot be tested without one, and the existing byte-pinned
  `first-light.json` is the pattern to copy.
