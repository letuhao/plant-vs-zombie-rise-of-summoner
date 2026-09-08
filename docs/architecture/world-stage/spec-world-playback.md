# Spec: world-playback

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-playback` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-contract` and
`world-wire`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.11, §7, §8.3, §8c.3, §8c.6.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §L.

---

## Objective

Build **the one translation table**: engine token → player sentence, in one module, tested against a
golden.

Today `classify()` recognises **five** prefixes and falls through on everything else
(`turnPlayback.ts:33-42`), and the fall-through prints the raw string —
`` return `${entry.subject} ${entry.detail}` `` (`:94-95`). So a turn in which the empire starves reads
literally `dave loam.shortfall:340`, and a refused order reads
`t3-move-e-dave-legion-1 dropped — path.not-contiguous` (`:91`). GG-23 is a Tier-1 gate and this is
its most visible failure in the product.

**It is one table, not per-prefix handling.** Per-prefix handling is precisely how the 5-of-21 state
arose — including a branch for an event nothing has emitted in two waves (§3). A table has a shape a
test can walk; a chain of `if (detail.startsWith(...))` has not.

**Success is that a turn report contains no engine token a player can read, and CI notices the day a
new token starts falling through.**

## Design

### 1. The vocabulary, counted rather than estimated

Verified across `src/FusionRpg.Core/World/` on 2026-09-03 — **21 event prefixes, 3 battle kinds, 2
calendar subjects, 37 drop reasons** (33 bare plus 4 carrying an argument). An earlier "~30" for the
drop reasons understated it, which is the reason the count is stated as a count.

The report's own shape is what makes a table possible: `WorldTurnEntryDto` is
`{ SectorId?, Phase, Kind, Subject, Detail }` (`WorldDtos.cs:261-270`), and `Kind` is one of five
constants (`TurnReport.cs:3-10`: `command.accepted`, `command.dropped`, `calendar`, `event`,
`battle`). So the table's key is **`(kind, detail-prefix)`**, and everything else is arguments parsed
off the tail.

| Family | Tokens | The player sentence, in one line |
|---|---|---|
| **March / arrival** | `arrival:<sector>` | *"Legion I reaches Frost Mire."* |
| **Halt / zone of control** | `halt:zoc:<sector>`, `zoc:<sector>` | *"Legion I stops short of Ashfall. An enemy force controls the ground ahead."* |
| **Supply** | `supply.cut:<sector>`, `recovery:<sector>` | *"Frost Mire is cut off… It earns nothing while it stands alone."* / *"Legion I’s wounded are mending."* — `recovery:` is a **garrison**, not a sector; see §3 |
| **Battles** | kind `battle`, three shapes: `sector:<s>:<winner>`, `lane:<l>:<winner|none>`, `guard:<s>:<winner>` | *"Legion I won the fight for Ashfall."* / *"…Neither side broke."* / *"…broke the lair's guard."* |
| **Claims** | `claim.held:`, `claim.barren:`, `claim.already-yours:` | Three outcomes, and **barren and fading must never look alike**: barren ground can never pay for itself; fading ground is losing a fight it could still win |
| **Calendar** | kind `calendar`, subjects `week` / `month`, details `ordinary` / `special` / `plague` | *"A new week."* / *"A strange week begins."* / *"A plague month."* |
| **Loam** | `loam.overflow:`, `loam.handicap:`, `loam.shortfall:`, `loam.shortfall.unresolved:`, `loam.lost:`, `unmade.spawned:` | The economy's whole narrative. `loam.handicap:150` is **per-mille** — it renders as *"15% more"*, never as *"150"* |
| **Legion supply** | `legion.topup:`, `legion.burn:`, `legion.starved:`, `legion.runway:` | The four the loam program added and playback has never seen |
| **Production** | `build.started:<structure>`, `sustain:<amount>` | *"Work began on a Well at Frost Mire. Ready in 2 nights."* |
| **Intel** | `intel.new:<n>` | *"Three new places are on your map."* |
| **Entity** | `entity.held`, `entity.routed` | *"Thornwake was broken in the field and will take no orders this turn."* |
| **Dropped orders** | kind `command.dropped`, 37 reasons | The same table `world-targeting` §5 reads for its on-map refusals — **one table, two surfaces** |

### 2. Phases are the rail's structure, and one of them emits nothing

The report carries `Phases` in the order they ran (`WorldDtos.cs:276`), and the engine's list is
closed: **Reveal, Movement, Sieges, Production, Growth, Pressure, Events, Snapshot, Intel**
(`TurnEngine.cs:44-63`). Playback is a straight walk in report order — re-sorting would tell a
different story than the one the server recorded.

**`Growth` is a named no-op** — `report.BeginPhase(Phases.Growth); return world;`
(`TurnEngine.cs:196-200`) — so it appears in the phase list and contributes zero entries. The rail
must render an empty phase **without looking broken**: a phase heading with *"nothing grew this
night"*, not a blank gap the player reads as a loading failure (GG-17).

That will change. §8d.1 makes recruitment a prerequisite of this stage and `Growth` is where it lands,
so this module's phase rendering is written to accept a phase gaining entries without a code change.

### 3. Two honest notes about the engine's own vocabulary

Both surfaced only because someone sat down to write the player sentence for every token, which is the
argument for doing it as a table.

**`attrition:` is a dead branch. Delete it; do not translate it.** The client still recognises it —
`turnPlayback.ts:40` classifies it as `supply` and `:89` renders *"takes attrition"* — but **nothing
in `src/FusionRpg.Core` emits it.** `LegionSupply.Resolve` replaced wound attrition and `SupplyGraph`
says so in its own comment: *"What happens to a force standing outside supply is no longer this
method's job… `LegionSupply.Resolve` runs after `LoamPhases.Pressure` and owns the whole burn/destroy
decision now that carried loam has replaced wound-based attrition"* (`SupplyGraph.cs:42-45`). The only
other reference is a test fixture (`turnPlayback.test.ts:70`), which goes with it.

Translating a dead token would have looked like progress and produced a sentence no player can ever
see. This is the specific failure mode a table catches and a prefix chain hides.

**There is no `supply.restored`.** The engine emits `recovery:` (`SupplyGraph.cs:111`) —
`report.Add(phase, Event, entity.EntityId, "recovery:" + (entity.AtSectorId ?? ""), …)` — and that
line is about a **garrison mending**, not about a sector rejoining the supply chain. So the plate's
*"Frost Mire is back in supply"* has no token behind it: supply recovery is currently the **absence**
of a `supply.cut:` line next turn.

The decision, and it is a decision rather than a question:

> **Playback does not infer restoration by diffing two reports.** Deriving an event from an absence is
> exactly the kind of client-side inference GG-15 and the ideal's §0.13 rule out, and it would be
> wrong the first time a report is trimmed. `recovery:` is translated as what it is — a garrison
> recovering — and a *sector* rejoining supply is a line the **engine** must emit. That is a
> `world-wire` / turn-engine item, named here because this is the surface that made it visible, and
> until it lands the rail simply does not claim it.

The naming inconsistency is worth recording as one: `supply.cut:` takes a **sector** id and
`recovery:` takes an **entity** subject with a sector detail. They read as a pair and are not one.

### 4. Every id goes through the humaniser, and today exactly one does

`sectorLabel()` turns `ember-hollow` into `Ember Hollow` (`worldViewModel.ts:197-203`). It is called in
**exactly one place** in production — `worldViewModel.ts:300`, building a node label. Every playback
line therefore shows raw kebab-case ids today.

The humaniser widens to a small set of typed labellers, because four id kinds reach this rail and they
are not interchangeable:

| Id kind | Example | Rendered |
|---|---|---|
| Sector | `frost-mire` | *Frost Mire* |
| Legion / entity | `e-dave-legion-1` | *Legion I* — a name, not a title-cased id |
| Faction | `dave`, `zomboss` | *the Grave Host* — from `WorldFactionDto.Name`, which is already on the wire |
| Lane | `l-ridge-ash` | *the Ridge road* |

Sector and lane can be humanised from the id. **Legion and faction cannot** — a legion's display name
is not derivable from `e-dave-legion-1`, and inventing one in a `split("-")` is how `Legion 1` becomes
`E Dave Legion 1`. The faction name is already projected; the legion name is a `world-wire` field, and
until it arrives the labeller returns a `pending` value with a player-readable reason rather than a
guess (`world-contract`'s `Pending<T>`).

### 5. What an opponent's turn report may reveal — §8c.3's half of a shared decision

This is a deliberate design decision, not an inherited side effect, and the map assigns it jointly to
this module and `world-wire`.

The background, verified: §8.3 moved the AI-reasons panel to the developer tree (GG-40), and §2.2
listed the null-`SectorId` fog leak as a defect. Both are right alone. Together they remove the last
channel through which an opponent is legible — because today you can watch Zomboss's economy fail only
*by accident*, through that leak. Entries with a null sector are shown to every viewer:
`VisibleTo(sectorId, believed)` returns `true` when `sectorId is null`
(`WorldEndpoints.cs:215-219`), and `BattleReporting.cs:36`, `LegionSupply.cs:98` and
`LoamPhases.cs:119, 141` all pass one.

**The decision this module takes, for the player-facing rail:**

1. **A line about nowhere in particular is about *your* empire, and it says so.** `legion.topup:180`
   becomes *"Your legions drew 180 loam from your stores."* If the projection is fixed and the line
   is yours, that sentence is correct. If the line is not yours it should never have arrived, and the
   fix is in the projection, not in a filter here.
2. **Playback never re-filters.** A rail that hides lines the server sent is a second fog
   implementation, and two fog implementations disagree. Whatever reaches the client is rendered.
3. **Opponent legibility is carried by the map and by battle lines that name ground you can see** —
   which is what `VisibleTo` already means — and not by the economy narration. That is the deliberate
   answer §8c.3 asked for: you learn what an opponent is doing by seeing it, not by reading their
   ledger.

One further inconsistency named by §8c.3 and left to `world-wire`: `VisibleTo` gates on *"have I ever
seen this sector"*, not *"can I see it now"* — so ground scouted on turn 6 still reports live battles
on turn 80, which contradicts §4.9's static-vs-dynamic rule. It is a projection defect and this module
does not paper over it.

### 6. The fall-through is loud in development and impossible in CI

The current default branch prints the token quietly. The replacement:

- **In development**, an unmatched token renders a visibly broken row and logs — a missing translation
  should look like a defect, because it is one.
- **In production**, it degrades to a neutral sentence naming the phase and the subject in player
  words, never the token.
- **In CI**, it cannot happen: §7's golden asserts every token in the vocabulary has a row, and a
  completeness test walks the token inventory and fails on a gap.

## What stays out

- **The transport's layout.** The keyframe rail's chrome sits in the stage; this module owns the
  keyframes, their phases and their sentences. Transport controls (`⏮ ◀ ▶ ⏭`) are drawn here because
  they are meaningless elsewhere, but the band and anchoring are `world-hud`'s contract.
- **The projection fixes.** The three fog defects, the missing legion name, a `supply.restored` line
  and the `VisibleTo` recency question are all `world-wire`'s. This module names them and refuses to
  compensate for them client-side.
- **On-map refusal placement.** `world-targeting` §5 decides where a refusal sentence lands; this
  module owns the sentence itself. One table, two consumers.
- **Notifications.** `world-notify` decides what gets promoted out of the report into a toast or a
  rail item; this module decides what the line says.
- **The magnitude rendering.** `world-numbers` renders `180 loam` and `15%`; this module supplies the
  value with its unit family attached — which is why `loam.handicap:150` cannot be rendered as `150`
  by accident.


### GG-50 — this surface's volume declaration

**Tier-1 gate, and it was missing from all fifteen specs until the 2026-09-03 audit.** `ui/volumeMatrix.test.ts`
is an *exhaustive* registry — its last test is `expect(COLLECTION_SURFACES).toHaveLength(8)` — so a new
collection surface that does not register **turns a shipped test red**. Registration is not optional
paperwork; it is how this program lands without breaking CI.

| Surface | `Turn playback keyframe rail` |
|---|---|
| Strategy | **`render-all`** |
| Reason | One turn's transcript, not a save's. Entry count scales with legions × sectors × the nine phases, so at §8e.3's target a heavy turn is order-10² rows — large, bounded, and **discarded at the next turn**. This is the world-stage surface whose volume grows fastest with empire size, so the threshold is stated rather than assumed: **above ~300 entries in one turn, revisit** |
| Proof | The generated `first-light-turn.json` golden, plus a synthetic heavy-turn fixture at the target legion count |

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build
npm run lint
```

```powershell
# The golden. Follows WorldFixtureTests.cs's pattern exactly, including the bless switch.
dotnet test tests\FusionRpg.E2E.Tests
$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1"   # re-bless after a deliberate engine change
```

## Project structure

```
web/fusion-rpg-web/src/
  features/world/
    turnPlayback.ts              → keyframes, phases, transport (kept — it is pure and tested)
    turnPlayback.test.ts         → the `attrition:` fixture at :70 removed with the branch
    playbackTable.ts             → THE table: (kind, prefix) → sentence template + arg parser
    playbackTable.test.ts        → completeness: every token in the inventory has a row
    labels.ts                    → sectorLabel (moved from worldViewModel) + lane / faction / legion
    fixtures/
      first-light-turn.json      → the generated turn-report golden (new)
  stages/world/playback/
    PlaybackRail.tsx             → phase headings, keyframe rows, the empty-phase state
    PlaybackTransport.tsx
tests/FusionRpg.E2E.Tests/
  WorldTurnFixtureTests.cs       → generates and byte-pins the turn-report fixture
```

## Code style

A row is data. The sentence is a template with named arguments, so a translator — or a second locale —
edits text and never logic.

```ts
/** One row of the table. `args` names what the tail carries, so a renderer cannot mis-order them. */
export type PlaybackRow = {
  kind: "event" | "battle" | "calendar" | "command.dropped";
  prefix: string;
  /** Player sentence. `{subject}`, `{sector}` and named args only — no positional holes. */
  say: string;
  args?: ReadonlyArray<{ name: string; family: "loam" | "permille" | "count" | "id" }>;
};

// The per-mille trap, made unrepresentable at the row rather than remembered at the call site.
{ kind: "event", prefix: "loam.handicap:", say: "Everything you hold costs {pct} more to keep this month.",
  args: [{ name: "pct", family: "permille" }] },
```

## Testing strategy

Vitest plus one new xUnit golden. The golden is the level that makes the rest real.

1. **Completeness** — a test walks the full token inventory (21 event prefixes, 3 battle kinds, 2
   calendar subjects, 37 drop reasons) and asserts every one has a table row. **This is the test that
   makes CI notice a new engine token**, which is the thing nothing does today.
2. **No token reaches the player** — render every row and assert the output contains no `:` -delimited
   engine token and no kebab-case id. A single regex over rendered text, run against the whole table.
3. **The golden** — `first-light-turn.json`, generated the way `first-light.json` is:
   `WorldFixtureTests.cs:27-49` creates a world through `/api/test/world/create`, serialises the live
   route, and asserts byte equality with an env-var bless switch. The turn fixture does the same for
   `/api/world/first-light/turn/{n}` — the route `world.spec.ts:91` currently stubs as a flat **404**,
   which is why no golden exists and why every row in plate 11 §L is a design rather than a covered
   behaviour.
4. **Per-mille discipline** — `loam.handicap:150` renders *15%*; a test asserts `150` never appears.
   Same for `legion.runway:11` (a **turn number**, not a magnitude) and `sustain:120` (whole loam).
5. **The dead branch is gone** — a test asserts `attrition:` has **no** table row, so re-adding one is
   a deliberate act. Its fixture at `turnPlayback.test.ts:70` goes with it.
6. **The empty phase** — a report whose `Growth` phase has zero entries renders a phase heading with
   copy, and the rail's length is unchanged. Not a blank.

## Boundaries

- **Always:** put a new token in the table, never in a branch. Humanise every id. Attach a unit family
  to every number the sentence interpolates. Render phases in report order.
- **Ask first:** any change to `turnPlayback.ts`'s keyframe shape — `world-notify` and the stage both
  bind to it. Emitting a **new engine token** (a `supply.restored`, a legion display name) — that is a
  turn-engine change with hashing and re-bless consequences and belongs to `world-wire`.
- **Never:** print an engine token, a kebab-case id, or a per-mille integer at a player. Never derive
  an event from the absence of a line. Never re-filter the report client-side — a fog bug is fixed in
  the projection. Never translate a token nothing emits.

## Success criteria

1. One table, in one module, covering **all** 21 event prefixes, 3 battle kinds, 2 calendar subjects
   and 37 drop reasons — proven by a completeness test, not by inspection.
2. A generated, byte-pinned turn-report golden exists, and `world.spec.ts:91`'s 404 stub is replaced
   by it.
3. No engine token and no raw id appears in any rendered playback line, asserted by a regex over the
   whole table's output.
4. `attrition:` is deleted from `classify()`, `describe()` and the test fixture, with the reason
   recorded next to the table.
5. `recovery:` is translated as a garrison mending; nothing on the rail claims a sector rejoined
   supply, and the missing engine line is filed against `world-wire`.
6. `sectorLabel` is used everywhere an id is shown, and lane / faction / legion labellers exist — with
   the legion name `pending` and readable rather than guessed.
7. The `Growth` phase renders as a designed empty state.
8. `npm test`, `npm run build`, `npm run lint` and `dotnet test tests\FusionRpg.E2E.Tests` are green.

## Open questions

**None.** §4.11 decided one table; §8c.6 confirmed the golden; the `supply.restored` gap and the
`VisibleTo` recency defect are named work items owned by `world-wire`, and §5 records this module's
half of the opponent-legibility decision rather than deferring it.
