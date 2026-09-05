# Capability map: world stage

**Status: proposed 2026-09-03, pending owner approval.** No module spec is written until the module
boundaries, dependency direction and build order below are approved — getting a map wrong is
expensive and reviewing it is not.

**Ideal it implements:** [world-stage-ideal.md](world-stage-ideal.md) — 16 owner decisions across four
rounds (§8, §8b, §8d, §8e) plus a four-perspective adversarial review (§8c) whose findings were
verified against code.
**Component catalog:** [design/11-world-stage.html](../design/11-world-stage.html) — every component in
all its states, with the field that drives it.
**Rules:** [game-gui-principles.md](game-gui-principles.md) GG-1…GG-61, 11 of them Tier-1 hard gates.
**Surface map:** [design/information-architecture.md](../design/information-architecture.md) §2.2.
**Plan / tasks:** `tasks/world-stage-plan.md` · `tasks/world-stage-todo.md` (prefixed pair per
AGENTS.md — `tasks/plan.md` is the perf stream's and is never a fallback).

---

## What this program is

The world map rebuilt as a **stage** rather than a page. Today `#/world` is a flowchart library in a
620px box inside a scrolling document: every verb is a text button in a 300px sidebar, the economy
prints raw engine strings at the player, and once a sector is selected there is no way back to
nothing selected. It is also the only route in the app that never entered the new shell.

This program builds the stage the information architecture already specified and nobody built.

## What it is not

- **Not a redesign of the turn engine.** Waves 1–2 of `world-map-program` are built, green and
  hashed. This program observes and commands; it does not change how a turn resolves — with two
  named exceptions in `world-commands` that the owner has authorised (§8d.2's cede order, and the
  `dowse` stance).
- **Not recruitment or seasons.** Those are `sector-development`'s, an existing module of
  [world-map-program.md](world-map-program.md), specced at
  `docs/architecture/world/spec-sector-development.md`. This program **consumes** the unit count
  (§8e.3: 6–10 legions, tunable); it does not produce it.
- **Not art.** The art registry and the designed placeholder (GG-58) are in; illustration is not
  (D10 — *"framed chrome now, art later, and the layout is already art-ready"*).

## Assumptions — correct these now

1. **In-place rebuild** of `web/fusion-rpg-web`, under the existing `StageHost`. Not a second app.
2. **`#/world` keeps working until its replacement lands.** No flag day. It is currently exempt from
   the shell's redirects, the hex guard and the GG-7 reachability matrix; all three exemptions retire
   when the old route is deleted, not before.
3. **`@xyflow/react` goes.** Locked at `decisions.md:93`. **Three** production files import it —
   `WorldPage.tsx:2-3`, `SectorNode.tsx:2`, `LaneEdge.tsx:2` (`routes.tsx:9` only names it in a
   comment) — and
   `worldViewModel.ts` is already library-agnostic (plain `{x,y}` from an authored grid, no auto-layout
   to replace), so the camera is a `viewBox` transform rather than a port.
4. **Components bind to the sealed FE view contract, never a REST DTO** — and §8e.2 makes that
   enforceable rather than aspirational.
5. **The two available map tiers only.** 6–18 sectors. The first tier above `medium` shipping
   (`world-generator`, wave 4) reopens the camera model and the outliner's shape together (§4.2).
6. **English only.** A second locale is enabled by the work, not delivered by it — but CJK-safe font
   stacks are non-negotiable now (GG-56, D7), because retrofitting them costs every layout.

---

## Modules

Fifteen. Levels 1–3 are the seam and the shell; level 4 is where most of the surface is and most of
it parallelises.

| Module id | Responsibility | Depends on |
|---|---|---|
| `world-contract` | **The sealed FE view contract for the world domain** — `SectorView` (corrected), `LaneView`, `LegionView`, `SlotView`, `ForceView`, `TurnEventView`; the `adaptWorld*` adapters against the byte-pinned fixture; **the `typeId` ADR**; moving the world DTOs to `lib/bus/world.ts` **and** widening `contractGuard` (§8e.2) | — |
| `world-wire` | **The server projections** — the five missing DTO fields, `PressureMilli`, effective capacity, the calendar on the header, the prospected set; the **three** fog-filter defects; a generated turn-report fixture | — |
| `world-commands` | **The write surface** — `Amount`/`StructureId` through `WorldCommandRequest` *and* `CommandPayload`; the **cede** order (§8d.2); the **ward** command; the `dowse` stance plus its missing `BudgetFor` arm; the first production `BindAsWarden` call site | — |
| `world-shell` | The stage itself under `StageHost`: viewport-filling, **no page scroll**, the SVG `viewBox` camera (drag / wheel / arrows / fit), `@xyflow/react` deleted | `world-contract` |
| `world-render` | Sector nodes in every state, lanes in 6 kinds × 5 states, legion markers, the **four** fog treatments, lifeline and supply overlays — tokens only, no state by colour or opacity alone | `world-shell`, `world-contract` |
| `world-hud` | Band 1: the loam summary strip, turn + calendar, the **fixed corner roles**, the component-split state, and **band 1's exemption from a band-2 scrim** (§8d.3, a kit-wide GG-5 amendment) | `world-shell`, `world-contract` |
| `world-numbers` | **Extends the existing magnitude renderer** — `i18n/magnitude.ts` already switches on a sealed `UnitClass` union — **thirteen** members at `contract/types.ts:33-55` since `loamUnits` landed 2026-09-04, governed by [design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md). The world's families **map onto that union**; they never start a parallel one, which `DESIGN-GATE.md`'s Stats row names as the failure it exists to prevent. Owns the `CostMilli` trap, the verified `formatPerMille` defect, and the nested lockable **modifier ledger** with its WCAG 1.4.13 obligations | `world-contract` |
| `world-inspector` | The **left-docked** (§8e.1) band-2 bounded shell with internal scroll (GG-61, Tier-1); identity, loam, component, slot, force, warden and prospecting blocks; the action cluster with a reason on every disabled verb | `world-render`, `world-numbers` |
| `world-turn` | The turn **cluster** — end-turn states, the live unresolved count, cycle-to-next, the force-end hatch, and the **two blocking classes with the hard-block list defaulting to empty** | `world-hud` |
| `world-notify` | Two notification classes; the passive right rail; **flush on End Turn except blockers**; per-category channel settings, changeable *on the notification* | `world-hud`, `world-turn` |
| `world-outliner` | The right-edge list of legions and held sectors — **with grouping and filtering** (§8e.3: ~28 rows at 6–10 legions and 18 sectors); the keyboard entry point into a canvas that has none | `world-hud` |
| `world-lenses` | Six **exclusive** lenses, number-row hotkeys, auto-activation from selection; **Placement is a transient targeting overlay, not a lens** | `world-render` |
| `world-targeting` | Targeting **on the map**: route preview with this-turn vs later reach, range overlays, blocked targets carrying translated reasons, the queued-order state | `world-render`, `world-commands` |
| `world-playback` | The **one** translation table — 21 event prefixes, 3 battle kinds, 2 calendar subjects, **37** drop reasons — plus the keyframe rail and its transport, tested against a golden | `world-contract`, `world-wire` |
| `world-confirms` | Band 3: commit-a-legion, **bind-a-warden** (permanent, two-step, with the low-Souls second confirmation), and the abandon warning drawn *before* the turn | `world-inspector`, `world-commands` |

**No cycles.** `world-numbers` deliberately does not depend on `world-shell` — a magnitude renderer
needs no stage — which is what lets it be built in parallel with the shell and reused by the
inspector, the HUD and the playback rail.

## Build order

```text
1.  world-contract · world-wire · world-commands        (parallel, no deps — the seam)
2.  world-shell · world-numbers                          (parallel)
3.  world-render · world-hud                             (parallel)
4.  world-inspector · world-turn · world-outliner · world-lenses · world-targeting · world-playback
5.  world-notify · world-confirms
```

Three orderings are deliberate and worth confirming:

- **`world-contract` is module 1 and is not negotiable.** `SectorView.typeId` is declared `number`
  while the wire is `string` — a **narrowing**, which `game-gui-map.md:142` puts behind a contract
  version bump plus an ADR. Nothing FE can be adapted until that is settled, and the repo has already
  written the reason down: *"getting a field wrong is cheap while nothing binds to it and expensive
  once eleven modules do."*
- **The three level-1 modules are genuinely independent**, which is why they parallelise: the contract
  may declare a field `Pending` before the wire fills it, and the command plumbing touches no FE file.
- **`world-playback` sits at level 4, not later**, despite being the least visual. GG-23 is Tier-1 and
  today's map prints `dave loam.shortfall:340` at the player — a stage that has fixed the layout but
  not the vocabulary has fixed one of the two failures §7 names.

---

## Gates

Two, and they are different in kind.

**Gate A — the seam holds** (after level 1). The `typeId` ADR is recorded; `contractGuard` catches a
feature-local DTO import; the five fields plus `PressureMilli` reach a client and the fixture is
re-blessed; a `sustain` and a `build` order survive the reveal round-trip, which is the trip that lost
`stance` once already. Nothing above level 1 is safe to build before this passes.

**Gate B — the owner plays it.** The loam program's own Checkpoint 5 is the precedent: a ten-turn
playtest on `two-hearths`, gated on **play rather than completeness**. Three questions no test can
answer — did you scroll; could you tell what happened last turn without reading an engine string; did
you ever reach for a control you could not find. §8d.4 keeps the full scope, and the plan slices
toward this gate rather than trimming the design to reach it.

---

## What the review already settled, so the specs need not re-argue it

Recorded here because a module spec written without these will re-derive them wrongly:

- **Presentation borrowings transfer at any scale; interaction borrowings do not** (§8c.1). Corner-role
  stability, one dismissal gesture, disabled-with-reason, no colour/opacity alone, declared unit
  families and flush-on-End-Turn are safe. The volume-dependent controls were sized for a game with
  dozens of units — and §8e.3's 6–10 target is what now justifies them.
- **The economy's core tension was a notification, not a decision** (§8c.2). §8d.2's cede order fixes
  that, and `world-commands` owns it. Until it lands, no surface may say *"choose what to release"*.
- **Fixing the fog leak and moving the AI-reasons panel to the dev tree together silence the opponent**
  (§8c.3). `world-wire` and `world-playback` jointly owe a deliberate answer to what an opponent may
  leak.
- **The server is not the problem; the contract is** (§8c.4). Every server gap names a line.
- **The rules pass and the drawing did not** (§8c.5). Plate 11's colour-independence and
  disabled-reason work is genuinely good; its type tier, `--faint` usage and one fog encoding were
  defects, seven of ten now repaired.

## Arbitration — added 2026-09-03 after the spec audit

**The map is the arbiter. Where a module spec disagrees with a row below, this table wins.**

Five audits ran over the 15 specs (two on citations, one on cross-spec consistency and decision
coverage, two adversarial). Both adversarial reviews independently recommended the same remedy: one
ownership table, because four agents wrote these concurrently and the collisions are all of the same
shape — two specs confidently owning one thing, or four specs assigning work to a fifth that never
accepted it.

### A. Contested decisions, settled

| Contested | Settled |
|---|---|
| **`RulesetVersion`** — `world-commands` and `sector-development` *both* take 5 → 6, and `world-wire` asserts *"still 5"* as a success criterion | **`world-commands` takes 5 → 6. `sector-development` takes 6 → 7.** `world-wire`'s criterion is correct **for `world-wire`** and must read *"this module does not change it"* rather than pinning a value. Whichever bumper lands second reads the current value; neither hard-codes 6 |
| **`ward` names two mechanics** — a **sector** binding (`world-commands`) and a **lane** ward (`world-targeting`, `world-lenses`) | **Two different kinds, and the collision was already repaired once in plate 11.** The sector action is **`bind-warden`** (sets `WorldSector.WardenBindingId`); the lane action keeps **`ward`** (raises `WorldLaneDto.WardLevel`) and remains unbuilt. `world-commands` renames its kind |
| **`features/world/` deletion** — `world-shell` deletes it at level 2; `world-wire`, `world-targeting` and `world-playback` write into it at levels 1, 4, 4 | **The pure layer moves, it does not die.** `worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts` and both fixtures move to `stages/world/` **at their consuming module's level**; `world-shell` deletes only what is left — the old page, its components, its route — and only after level 4. `world-shell` SC7 moves to a **retirement task at the end of the program**, not level 2. **✅ Done 2026-09-05** — the whole pure layer (`worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts`, `labels.ts`, `playbackKeyframes.ts`, `playbackTable.ts`, both fixtures, plus `playbackKeyframes.ts`/`playbackTable.ts` this row didn't originally name) moved to `stages/world/` in one pass rather than per-level; the `worldTypes.ts` re-export shim and `features/world/` itself are both gone; every consumer imports the wire DTOs straight from `lib/bus/world.ts` |
| **`worldVerbs.ts` registration** — `world-turn` wraps a throw into a player-readable condition; `world-lenses` forbids wrapping in a Boundaries **Never** | **Do not wrap. `world-lenses` is right**: a swallowed throw is a silently dead hotkey, which is worse than a loud failure. The collision is prevented at the source — `keybindings.ts` refuses a rebind onto `1`–`9` (IA §5 already reserves them). `world-turn` drops its wrapper and its SC7 |
| **`keybindings.ts` ownership** — `world-lenses` edits it; `world-turn` and `world-outliner` call it ask-first | **`world-lenses` owns the edit**, having the reserved-range rule. The other two consume it |
| **The hex-guard exemption** — deleted by both `world-shell` (level 2) and `world-render` (level 3) | **`world-render` deletes it**, in the change that makes the map token-only. `world-shell` drops the claim |
| **The turn-report golden's name** — `turn-report.json` vs `first-light-turn.json` | **`first-light-turn.json`**, beside `first-light.json`, naming the world it came from. `world-wire` produces it; `world-playback` consumes that exact path |
| **The calendar's source** — `world-wire` decides `WorldStateDto`; `world-hud` builds from the header plus `calendar` report entries | **`WorldCalendarDto` on `WorldStateDto`**, per `world-wire`. `world-hud`'s SC3 and its test change. **The report-entry source is wrong on its own terms:** `TurnEngine.cs:225-231` emits calendar entries only on a week boundary, so that slot would be blank on 6 of every 7 turns |
| **The loam unit type** — `world-contract`'s prose rejects a branded `LoamUnits` as a third classification, and its own code block then defines one | **No brand.** A `loamUnits` member on the sealed `UnitClass` union, filed ask-first as `world-numbers` already does. `world-contract`'s code block and `world-targeting`'s signature both change |
| **Band 1 "never scrolls"** vs `world-notify` and `world-outliner`, both band-1 and both scrolling | **A bounded shell scrolling its own body is not "band 1 scrolling."** `world-hud`'s rule means the *band* never grows or moves the stage; its test asserts that, not the absence of internal overflow |
| **`W`** — `world-shell` re-imports `WASD` pan, colliding with `world-turn`'s cycle key | **Arrows pan; `W` cycles.** `WASD` was removed from the plate on 2026-09-03 for this exact collision and must not return |

### B. Obligations four specs assigned to `world-wire`, which never accepted them

`world-wire` declares a closed batch of nine additions and **Open questions: None**, so each of these
would have been discovered mid-build by a downstream module. All four are now **`world-wire`'s**, and
its batch is nine plus four:

| Obligation | Named by | Why it is `world-wire`'s |
|---|---|---|
| Per-lane march cost for the selected legion | `world-targeting` | `LaneCost.For` needs the lane-type catalog and the legion's banner element, neither on the wire. Computing it in TypeScript would be a private curve |
| The `LoamUpkeep` operand breakdown | `world-numbers` | `WorldSectorDto` carries totals only; the ledger cannot decompose what it is not sent |
| A legion display name | `world-playback` | Every playback line renders a raw kebab id today |
| A `supply.restored` engine line | `world-playback`, and ideal §2.3 | `supply.cut:` exists and nothing reports the reverse. `recovery:` is a garrison mending, not this |

### C. Decisions that landed in no module

The coverage audit found two of the sixteen owner decisions implemented nowhere. Both are
player-visible:

- **§8.3 — the AI-reasons panel moves to the developer tree.** Two specs cite it as *background*;
  neither owns the move, and `WorldEndpoints.cs:185-196` is untouched by all fifteen. **Assigned to
  `world-wire`** (it owns that endpoint's projection) with the dev-tree surface itself out of scope.
- **§8.4 — no unlock; the rail entry is unconditional; `information-architecture.md` §7's row is
  superseded.** No spec mentions availability. **Assigned to `world-shell`**, which owns the route and
  the three exemptions.

Also unowned: §8.1's consequence that `spec-loam-fe-2.md` is superseded and loam Phase 12 stays
closed. That is documentation, not a module — it is done, and recorded here so it is not re-found.

### D. The live scrim defect is not where §8d.3 says it is

**This is the finding most likely to have shipped a fix that missed.** §8d.3 and `world-hud` both
target `_kit/kit.css:401`'s `.scrim`. **The shipped web does not use that class at all** — grep
returns nothing. The live defect is `web/fusion-rpg-web/src/shell/PanelShell.tsx:61`:

```tsx
className={cn(band === "system" ? "band-system" : "band-panel", "fixed inset-0 bg-black/50")}
```

A full-viewport 50% black overlay at band-panel, over a band-hud HUD. So `world-hud`'s fix must touch
**`PanelShell.tsx`**, not only the kit and the principles file. The kit and GG-5 amendment remain
correct and necessary — they stop the defect being re-authored — but on their own they would have
left the regression exactly where it is.

### E. GG-50 — a Tier-1 gate that was in none of the fifteen specs

The test-efficacy audit found **GG-50 mentioned in zero of the fifteen specs** — no `virtualize`, no
`windowed`, no 10/100/1000 declaration anywhere. That is not merely an omission, because the repo
already enforces this gate **exhaustively**: `web/fusion-rpg-web/src/ui/volumeMatrix.test.ts` closes
with

```ts
it("declares the full known set", () => {
  expect(COLLECTION_SURFACES).toHaveLength(8);
});
```

**So landing this program without registering its collection surfaces turns a shipped, green test
red**, and no spec anticipated it.

Five surfaces ship here. Each now carries its declaration in its own spec (strategy, reason, and the
fixture that proves it), and the **registry edit is a single shared task**, because no one module owns
a file all five must appear in:

| Surface | Owning spec | Strategy |
|---|---|---|
| Outliner (legion + sector rows) | `world-outliner` | `render-all` — ~28 rows at §8e.3's target, bounded by the two available map tiers |
| World notification rail | `world-notify` | `render-all` — flushes every End Turn except blockers, visible stack capped at three |
| Turn playback keyframe rail | `world-playback` | `render-all` — one turn's transcript, discarded at the next; revisit above ~300 entries |
| Sector inspector — slot rows | `world-inspector` | `render-all` — four slots max in shipped content (`SlotIndex` tops out at 3) |
| Sector inspector — force rows | `world-inspector` | `render-all` — single-digit; enemies appear as bands, not per-unit rows |

**The shared task:** add these five rows to `COLLECTION_SURFACES` and change `toHaveLength(8)` to
`toHaveLength(13)`. It lands with whichever of the four modules ships last, and every one of them
lists it so it cannot be dropped.

**One thing worth noticing about these five.** Not one needs virtualizing, and that is a real result
rather than a convenient one: every world-stage collection is bounded by something structural — a map
tier, a per-turn flush, authored sector content, or the fact that enemy forces render as bands rather
than per-unit rows. The existing registry has exactly one `virtualize` entry (Creatures, where a
player binds indefinitely over a long save), and the world stage adds none. **If a later change makes
one of these unbounded, its row is where that becomes visible** — which is what the gate is for.

## Open questions

**None.** All sixteen decisions are recorded in the ideal doc's §8/§8b/§8d/§8e, and the four
questions this map would otherwise have raised — the right-edge collision, the guard fix, the legion
target, and the spec order — were answered on 2026-09-03 before it was written.

## Filed by the party-dungeon program (2026-09-05)

| Ask | Filed by | Shape |
|---|---|---|
| The map door issues the delve request | `party-dungeon/spec-domain-catalog.md` §6 (R10) | a `world-inspector` action + `world-commands` order posts the same `POST /api/delve/start` body the Sanctum picker sends, with `domainId` and `parentWorldId`; no legion leaves the map; the response `{delveId, worldId}` bootstraps the delve stage |
