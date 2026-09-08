# Spec: delve-stage

Status: **APPROVED by the owner 2026-09-05 (wave 5) — unbuilt.**
Every `file:line` below was opened in this session and checked against the working tree, not HEAD; anything that had moved
is reported in §19 rather than quietly corrected. Every number here is a **starting shape so the system runs** — never a
balance decision.

Module id `delve-stage`, row 17 of the [party-dungeon map](../party-dungeon-map.md) (`party-dungeon-map.md:127`), wave 5,
the last. Gate **G5** (`party-dungeon-map.md:161`). Binding row: `decisions.md:114` *"Game GUI — sixth stage `delve`
(2026-09-05)"*. Depends on all sixteen approved siblings; it renders their read models and owns none of their rules.
Format: [spec-expeditions.md](../standalone/spec-expeditions.md); shell precedent:
[base-defense/spec-siege-stage.md](../base-defense/spec-siege-stage.md).

## 1. Objective

**The sixth stage: `#/delve/{delveId}` — the place a raid is played.** The room graph is the stage; a fight is drawn on it;
the pack, the wild talk, the event, the object prompt and the supply bag are panels over it; one extraction summary closes
the run; drops, level-ups, joins and first clears report without stopping anything. Success looks like: a player opens the
descent door in the Sanctum, picks a found domain at a named difficulty, descends, walks a graph they can only partly see,
steers one band through fights while the others are played for them, carries a haul they can run out of room for, and
leaves with it — with **no number on screen the server did not resolve**, no engine word anywhere, and a browser refresh
mid-fight losing nothing, because the state was never in the client.

## 2. Locked anchors (quoted, not paraphrased)

- **`decisions.md:114`** — *"`delve` is the SIXTH stage (`#/delve/{id}`) … a place the player acts in (GG-4), so it is a
  stage, not a layer. The `battle` id at `railState.ts:31` is base-defense's … and is not reused. The room graph is the
  stage; a fight is drawn on it; the pack, wild-talk and event surfaces are band-2 layers; the extraction summary (with any
  wipe/permadeath notice folded in) is the one band-3 result; drops, level-ups, joins and first-clears report at band 4.
  `information-architecture.md` §1/§2/§4/§5/§7 and the stage-count CI assertion move with this row."*
- **GG-4's test** (`game-gui-principles.md:100-103`) — *"If the player is going somewhere **to act**, it is a stage. If they
  are going **to look, compare, or configure**, it is a layer."* **GG-23** (`:356-369`) — *"Engine, protocol, and schema
  vocabulary never appears on a player surface … Testable as: a banned-vocabulary guard over player-facing string literals."*
- **Contract extension rule** (`game-gui-map.md:135-142`) — an optional field, a new entity or a new variant is *"✅ yes,
  any time"*; *"Rename or remove a field, narrow a type, change a unit family"* is *"❌ contract version bump + ADR."*
- **The map front end is frozen pre-refactor.** `world-stage-map.md` is *"proposed 2026-09-03, pending owner approval"* and
  its arbitration table reorganises `stages/world/` wholesale. The one delve coupling is already filed there
  (`world-stage-map.md:262-266`): *"a `world-inspector` action + `world-commands` order posts the same `POST
  /api/delve/start` body the Sanctum picker sends … the response `{delveId, worldId}` bootstraps the delve stage."*
- **No wall-clock pacing** (`party-dungeon-map.md:47-48`) — *"No stamina, no daily limit, no real-time recovery … Recovery
  is measured in delves."* This stage renders no timer that spends the player's day and no control disabled by the clock.

## 3. Scope and boundary

| This module owns | This module never touches |
|---|---|
| The route `#/delve/{delveId}`, the stage id row, the six shell rows, the descent door and its unlock | `src/stages/world/`, `src/features/world/`, `lib/bus/world.ts`, `WorldEndpoints.cs` — the map FE is frozen |
| The band assignment of every delve surface and the components that draw them | Rules: no roll, price, Θ, band arithmetic, pack fit, sight radius or quest evaluation |
| The client read model — the `Delve*View` types, their `adapt*` functions, the label table; and `GET /api/delve/{delveId}`, the one projection endpoint, from readers `delve-scope` exposes | The sixteen sibling resolvers, their DTOs' meaning and their refusal ids; `TurnEngine.Step`, the battle resolver, the loot pipeline, the seed import path |
| The live session client: subscribe, steer, declare, freeze, resume, reconnect | `BattleEngine`, `InteractiveIntentSource`, `BattleSessionRegistry` — consumed, never re-derived |
| The player vocabulary for this program and the `vocabularyGuard` extension enforcing it | The injector and anything under `pvz.*`. Zero injector work in this program |

**Nothing on this surface is an RPG rule.** The delve is an RPG-layer feature end to end; this module is its window, and a window that computes is a second copy of the game.

## 4. Route, stage id, and the six shell rows

`#/delve/{delveId}`. Layers ride the query string per the URL grammar (`information-architecture.md:232-246`):
`#/delve/8812?panel=pack`, `?panel=talk`, `?panel=fight`. Following any cold restores the stage first, then opens the panel;
`Esc` returns to the bare stage URL. Six rows, zero branches — the discipline `spec-siege-stage.md` §2 states. An
`if (stage === "delve")` under `src/shell/` is a build failure by test, not a review comment.

| # | Row | File | Change |
|---|---|---|---|
| 1 | Stage id union | `src/shell/railState.ts:31` | `currentStageId` gains `"delve"`. The `battle` id stays untouched — it is base-defense's |
| 2 | Route | `src/app/routes.tsx:93-108` (the `world` shape) | `<Route path="delve/:delveId">`, lazy, with a `ChunkFallback` — the `LawnStage` shape (`routes.tsx:13`) |
| 3 | Stage → default layer | shell stage map | `delve` opens no layer by default |
| 4 | Esc / back target | `src/shell/keymap.ts:10` | Esc pops one layer; on an empty stack the stage claims it (the `WorldStage` precedent) and clears room selection |
| 5 | Reachability matrix | GG-7 matrix | A `delve` row. Nothing is blocked: no cost is committed by opening a panel, and travel away resolves nothing |
| 6 | Stage-label catalog | `src/i18n/locales/en/messages.po` via `@lingui/macro` | One message, `"Delve"`, extracted by `npm run extract` |

**The stage-count assertion.** `information-architecture.md:14` reads *"Four stages"* under an amendment at `:17` naming
six. The count moves to **6 declared**; since neither `siege` nor `delve` exists at `railState.ts:31`, the honest assertion
is a pair — 6 declared, plus a test naming which are *built* — the shape `spec-board-render.md:203` already asks for.

**The rail does not grow.** Delve is not a rail entry. The descent door is a **Sanctum affordance** (the map table's
precedent, `information-architecture.md:64`) opening the picker as a band-2 layer with no rail row and no key, exactly as
the sector inspector does (`:144`). Locked until *"Delve — first domain found (expedition)"* (`:225`) is satisfied, shown
with what unlocks it (GG-17) — never invisible, never present-but-dead.

## 5. The read model — what the client fetches, and what it never computes

```text
mount   GET /api/delve/{delveId}                  → DelveDto (whole state, revision-stamped)
live    RpgHub → DelveUpdated{delveId, revision}  → invalidate → refetch the same projection
act     POST …/rooms/{id}/answer · …/talk · …/pray · …/cage · the pack and supply routes
        → nothing in the response is trusted; the refetch is truth
fight   the SignalR session: dwell → declare → resolve (spec-delve-battle-profile.md §3)
```

The projection is assembled server-side from exactly the parts `delve-scope` names (`spec-delve-scope.md:347-348`:
*"`LoadWorldState` + the two delve tables + `Visibility.SeenBy` + `DelveSight.ForParty`"*) plus each sibling's own read
model. **No second source.** The client holds one query key per delve and no derived cache; a room's state after an answer
is whatever the refetch says. Closing the tab mid-room loses the panel, not the run.

| The client renders | The client never derives |
|---|---|
| Room kind, archetype name, sight state, doors and gates | Which room is next, whether a door is passable, what a gate costs |
| The room's difficulty as a **name** (`EffectiveBandName`, `spec-difficulty-ladder.md:174-176`) | Θ, `bandDelta`, the entrance band, a rung's deltas |
| Six pool meters and the nerve stage per member (`spec-delve-attrition.md:419-420`) | The nerve threshold, the hunger charge, exhaustion |
| The pack grid, occupied cells, the floor list (`PackDto`, `spec-loot-pack.md:226`) | First-fit placement, footprints, value-per-cell |
| Talk verbs and the band as a name (`TalkTree.Offered`/`Step`, `spec-wild-room.md:402`); event choices and the banner (`EventResolution`, `spec-event-deck.md:421`) | The disposition shift, the offer floor, the capture chance; the outcome draw, the pity counter, the repeat scope |
| Quest `have / need` and done (`QuestDto`, `spec-delve-quests.md:341`); every price as a finished figure (`DelvePrices`, `spec-dungeon-loot.md:394`) | `QuestProgress.Evaluate` — a pure read model, on the server; any price at all, including a "preview" |

## 6. Contract additions

All additive: new view types plus one optional field. **`CONTRACT_VERSION` stays `2`** (`contract/types.ts:26`) — nothing
is renamed, removed or narrowed, so the extension rule (`game-gui-map.md:136-142`) admits every row free.

**Types** in `src/contract/types.ts`: `DelveView`, `RoomView`, `DoorView`, `PartyView`, `MemberView`, `PackView`,
`PackCellView`, `TalkView`, `EventView`, `ObjectPromptView`, `SupplyView`, `FightView`, `QuestView`, `ExtractionView`,
`DomainOfferView`, `RungOfferView`. **Adapters** in `src/contract/adapt.ts`, one per type (`adaptDelve`, `adaptDelveRoom`,
`adaptDelveParty`, `adaptDelveMember`, `adaptPack`, `adaptTalk`, `adaptEvent`, `adaptRoomObject`, `adaptSupply`,
`adaptFight`, `adaptDelveQuest`, `adaptExtraction`, `adaptDomainOffer`) — the `adaptWorld*` family's shape
(`contract/adapt.ts:278-490`). **No `stages/` file may name a `*Dto`** (`contract/contractGuard.ts:57` guards `stages`,
`layers`, `ui`), so every wire type stays behind an adapter. `formatMagnitude` has no bare-`number` overload by design
(`i18n/magnitude.ts:15`, GG-46), so a number with no row below cannot reach the screen:

| Figure | `UnitClass` | `op` | Why |
|---|---|---|---|
| Pool current and max (hp · stamina · hunger · spirit · qi · poise) | `count` | — | A stock, not a delta: `count` is the unsigned whole-number renderer (`magnitude.ts:31-32`); `gameUnits` is signed (`:56-61`) and would print `+4,120` |
| Pool fill for the meter | `perMilleRatio` | `flat` | `formatPerMille`'s unsigned branch (`magnitude.ts:88-90`) — `610` renders `61%`, the shape `spec-delve-attrition.md:28` describes |
| Souls: balance, unclaimed haul, every price; rooms cleared, quest `have`/`need`, carry space left, retinue count | `count` | — | The same stock reading, then plain whole counts. See §13 for the `long` rule |
| Recovery left | `count` | — | Counted **in delves** (`spec-delve-attrition.md:37-38`), never in days |
| Damage, heal, shield hp in the fight feed; any contest rate it shows | `gameUnits`; `sigmoidPoints` | — | Carries its `channel`, and signed is correct for a delta; the rate gets the `ContextRead` `formatSigmoidContext` composes (`magnitude.ts:130-140`) |
| Dwell remaining in the fight panel | `milliseconds` | — | `formatMilliseconds` (`magnitude.ts:107-112`) |
| Room difficulty, rung, raid mode, entry, nerve stage, sight | **not a number** | — | A name from §8. `SectorView.dangerBand` is a `count` magnitude on the map (`adapt.ts:291`); the delve never repeats that |

**One optional field, filed and defaulted:** `Magnitude.exact?: string` — the exact decimal for a `long` figure above
`Number.MAX_SAFE_INTEGER`, preferred by `formatMagnitude` when present. An optional field is free under the extension
rule; see §18 ask 8 and §13.

## 7. Bands and surfaces

Every surface declares exactly one band (GG-5, `game-gui-principles.md:115`; its band table `:123`). This table **is** the decisions row, expanded.

| Band | Surface | Notes |
|---|---|---|
| **0 Stage** | **The room graph** — rooms, doors, gates, one-way arrows, secret dead ends, party markers, the three sight treatments (unlit · glimpsed · seen, `Visibility.cs:6-16`) | The stage. Never unmounted by a panel (`useStageMountGuard`, `shell/stageHost.tsx:12`) |
| **0 Stage** | **The fight, drawn on the room it is in** — the room node expands in place; enemies, ranks and the strike feed animate there | `decisions.md:114`: *"a fight is drawn on it"*. Not a separate screen, not a stage change |
| **1 HUD** | Party strip (parties by name), six pool meters and nerve stage per member, haul and unclaimed souls, quest tracker, room readout, the initiative rail during a fight, connection state | The rail (`shell/Rail.tsx`) is unchanged and identical here, as on every stage |
| **2 Panel** | **Pack** — the per-party carry grid, move and drop | `PackDto` (`spec-loot-pack.md:226`); this module owns the grid UI, that module the model |
| **2 Panel** | **Wild talk** — verbs, the band as a name, the quote, the offer | `TalkTree.Offered`/`Step` (`spec-wild-room.md:402`) |
| **2 Panel** | **Event** — choices, warnings, the banner | `EventResolution` (`spec-event-deck.md:421`); the banner arrives through the `ui.present` sink (`spec-event-deck.md:176`) |
| **2 Panel** | **Object prompt** — one panel per `RoomObject`, its offered verbs, each disabled verb carrying its reason | `RoomObjectBuilder.For` (`spec-supplies-and-objects.md:362`); verbs `open · disarm · pray · loot · destroy · garrison` (`spec-dungeon-registries.md:78`) |
| **2 Panel** | **Supply bag** — what the party carries that can be used here | `SupplyUse.Use` (`spec-supplies-and-objects.md:361`) |
| **2 Panel** | **Fight panel** — the steered party's input surface: initiative detail, action chooser, target picker | The **input** surface is a panel; the fight stays on the stage. Closing it is not a retreat (§9) |
| **2 Panel** | **Descent picker** — found domains, difficulty names, raid mode, parties, provisioning | Opened from the Sanctum door; renders `DomainOfferDto` (`spec-domain-catalog.md:380`) |
| **3 Dialog** | **The extraction summary** — the one result, with any wipe or permanent-loss notice **folded in** | The only band-3 result this stage produces (`ExtractionSettlement.Decide`, `spec-delve-attrition.md:423`; `DelveLoot.AtExtraction`, `spec-dungeon-loot.md:393`) |
| **3 Dialog** | Descent confirm (single-descent domains, and the Oath), extract confirm, retreat confirm | Confirms, not results — they name what is staked before it is staked |
| **4 Toast** | Drops · level-ups · a wild demon joining · a first clear | Queued, never racing band 3: a report arriving while the summary is open waits behind it |

**The lint this table earns:** a band-3 opener test. Nothing but the extraction summary and the three confirms may push a
`dialog` entry from `stages/delve/` — a drop, join or level-up that opens one is the failure the review found
(`audit-2026-09-05.md:224`).

## 8. Player vocabulary

**Ids on the wire, one translation table in the client** — the `world-playback` precedent (`world-stage-map.md:81`: *"the
**one** translation table — 21 event prefixes, 3 battle kinds …"*). Authored content (a domain's `name` and `flavor`, a
boss's almanac name — `spec-domain-catalog.md:63`, *"the only fields a player reads"*) travels as text and renders
verbatim; **vocabulary** travels as an id and is translated by `src/stages/delve/labels.ts`, whose messages live in
`src/i18n/locales/en/messages.po` (Lingui `msg`/`Trans`). A new string is registered by writing the macro at the use site
and running `npm run extract` — there is no second registry to keep in step.

| Engine concept | On the wire | The player reads |
|---|---|---|
| `Θ`, `Θ_room`, `Θ_run`, `thetaOffset`, `contentScale`, `bandDelta`, `dangerBand`, `theta_run` | **never sent** | — |
| Composed band → `EffectiveBandName`; rung id; tail step | `bandName` id; `rungId`; `tailPlus` | *Quiet · Grim · Dire · Black* … (the `bandNames` registry, §18 ask 3); *Very hard* (the ten rungs read as written, `spec-dungeon-registries.md:76`); *Beyond the abyss · third step* |
| `entry: once \| many`; `raidMode: solo \| pair \| quad` | `entryKey: "single-descent" \| "standing"`; `raidMode` id | *One descent only* · *Open ground*; *One band · Two bands · Four bands* |
| `PartyIndex` | `partyIndex` (never rendered) | *First Banner · Second Banner · Third Banner · Fourth Banner* — four fixed names in `labels.ts` |
| `nerve.unsettled \| shaken \| afflicted` | `nerveStage` id | *Unsettled · Shaken · Afflicted* |
| `Downed`; `Recovering(n)`; `Retired`; `won`; `Retreated` | `downed`; `recoveringDelves`; `lost`; `cleared`; `withdrew` | *Down*; *Recovering — two more descents*; **Fallen** (never "retired"); *Cleared*; *Withdrew* |
| `SectorSight.Full \| Glimpse \| None` | `sight` id | Not words: a drawn treatment. A glimpsed room names its kind only (`spec-supplies-and-objects.md:362`) |
| Room kinds `fight · elite · cache · curio · wild · shrine · rest · merchant · trap · unknown · boss` | `kind` id | *Fight · Elite · Cache · Curio · A stranger · Shrine · Camp · Trader · Trap · Unclear · The lair* |
| Pack cells, `rows × cols`; `souls_unbanked` | `rows`, `cols`, `cells[]`; `unclaimedSouls` | *Carry space* — "four spaces left", never "cells"; *Souls you have not carried out yet* |
| `delve.price-undesigned`; `extract` / `retreat` | the rule id; the decision kinds | *Not for sale here.*; *Leave with the haul* · *Fall back* |

**The guard extension.** `BANNED_WORDS` (`i18n/vocabularyGuard.ts:42-53`) gains `bandDelta · dangerBand · PartyIndex ·
Retired · thetaOffset · rungId · delveId · sectorId · archetypeId · perMille`. Two mechanical facts decide the rest, both
verified in the working tree:

- **`Θ` and `‰` cannot go in `BANNED_WORDS`** — `BANNED_WORD_PATTERN` wraps every entry in `\b…\b` (`vocabularyGuard.ts:55`)
  and neither is a word character, so both would be silent no-ops. They go in a second `BANNED_SYMBOLS` list matched without
  word boundaries, through the same two scanners (JSX text `:106-113`, string literals `:120-129`).
- **`once` and `many` are not added as bare words** — both are ordinary English and would false-positive across the tree.
  Their rule is enforced by a stage-local test asserting the rendered `entryKey` values are the two translated phrases.

`stages/delve/` and `layers/delve/` are **not** added to `ALLOW_LISTED_PREFIXES` (`vocabularyGuard.ts:16-37`) — this is
player chrome, with no developer exemption to claim.

## 9. Live input and session behaviour

The profile is `delve`, `RequiresLiveInput: true` (`spec-delve-battle-profile.md:69`), and the session is that spec's §3,
consumed whole. This module builds the client half and nothing else.

| Event | What the player sees | Mechanism |
|---|---|---|
| A fight starts in the steered party's room | The room expands on the stage; the fight panel opens once, then obeys the player | `DelveBattle.Run` over the SignalR session |
| The dwell opens, then elapses | The chooser is live with the window shown as time; on elapse the fallback is taken **and shown as taken** | `inputWindowMs` 1500 ms (`spec-delve-battle-profile.md:256`); `DecisionSource.Timeout` recorded (`InteractiveIntentSource.cs:81`) |
| The panel is closed | Nothing pauses; the fight keeps taking fallbacks and the room stays live | Closing a panel is not an act (GG-1) |
| Three consecutive timeouts, or the connection drops | The fight **freezes** and says so: *"Your band is waiting."* A band-1 connection state, never a dialog; the room stays in progress | `MaxConsecutiveTimeouts = 3` (`BattleSessionRegistry.cs:67`, `:144-155`) → `Disconnect` (`:109`), which preserves session and trace |
| The player returns | Refetch → the room reads *in progress* → resume replays the recorded prefix, then goes live | `Resume` (`BattleSessionRegistry.cs:119`) + the replay-then-live constructor (`spec-delve-battle-profile.md:134-139`) |
| Steering moves to another party; an un-steered party fights | The first fight freezes, and no automated policy finishes it; an un-steered party gets a read-only feed on its room node, no chooser | *"No finish-on-autopilot"* (`spec-delve-battle-profile.md:131`); the same automated policy CI runs |
| An un-steered party takes drops | They land on the floor list by the autopilot's rule; that party's pack move handles are **disabled with a reason** | *"autopilot never moves the pack"* (`spec-loot-pack.md`); GG-55, `ui/disabledReasonGuard.ts` |
| The player leaves the stage mid-delve | Nothing resolves. The delve stays `Active`; the door leads back in | Navigation is not `retreat` and not `extract` — the siege precedent (`spec-siege-stage.md:141`) |

**Being away never costs the run, and nothing here is timed against the player's day.** A timeout is a recorded decision
inside one fight, not a penalty for not being there.

## 10. Refusals and error surfaces

**A refusal the client can predict is a disabled control carrying its reason, never a request that fails.** Only a genuine
race or a server-side roll refusal may interrupt.

| Refusal (`spec-domain-catalog.md:182-204`, in its order) | Surface |
|---|---|
| 1 · `domain.unknown` · `domain.stale` · `domain.not-found` | The domain is **absent** from the picker. Never a greyed row, never an error |
| 1 · `domain.sealed` | The row renders as *Closed to you*, with the reason. Descend is not offered |
| 1 · `delve.in-progress` | The row renders as *In progress*, with a **Return** action rather than a Descend one |
| 1 · `correlation.mismatch` | Band-4 warn toast plus a refetch. The only unpredictable member of step 1 |
| 2 · `rung.not-offered` · `oath.implied` · `raid.mode-not-offered` · `raid.party-shape` | Descend disabled with the reason. Refused rungs are **omitted**, never greyed (`spec-domain-catalog.md:137`) |
| 3 · `member.unavailable:{id}` | That member's row in the party builder is disabled with its own reason (on an expedition · recovering · already carrying) |
| 4 · `delve.souls-insufficient` | The provisioning step shows price and shortfall inline; Descend disabled with the reason. Nothing is debited |
| 5 · 6 · 7 (frozen terms, seed and roll, the one transaction) | Server-internal: a band-3 dialog — *"The way did not open."* The rule id reaches the developer tree only |
| `delve.price-undesigned` (merchant, `spec-dungeon-loot.md:394`) | The buy control is disabled: *"Not for sale here."* Never a price of zero, never a blank |

Every disabled control carries a `title` — `disabledReasonGuard.ts` fails the build otherwise — and the reason is a
sentence, not a rule id.

## 11. Out of v1

No 2-D fight board (rank is 1-D until `board-render` is adopted — `party-dungeon-map.md:93`). No playback of a finished
delve, though the decision log that would drive one is written. No drag-reorder in the pack beyond move and drop. No forge,
no garrison (both refuse at the model), and no cage interaction beyond a present-but-refusing prompt
(`spec-supplies-and-objects.md:212`). No second locale, no map-FE work of any kind, no spectating.

## 12. Tunables

Per [tunables-ssot.md](../tunables-ssot.md) §1. A delve's balance surface is entirely the server's; what is left here is
presentation pacing, tunable because a feel pass would change it. New file `data/tuning/delve-ui.v1.json`, following
`data/tuning/actor-hud.v1.json` (read by Core, delivered on the projection so the client carries no copy), edited only
through `python tools/tuning/publish.py`.

| Key | Unit | Owner | Starting shape |
|---|---|---|---|
| `banner.durationMs` | ms | this module | 2600 — the default for `ui.present`'s `ShowBanner(bannerId, durationMs)` when an outcome names none |
| `reveal.toastMs` · `reveal.maxQueued` | ms · count | this module | 4000 — a band-4 report's dwell; 3 — above it, queued reports collapse into one summary report |
| `dwell.inputWindowMs` · `dwell.afkTimeoutMs` | ms | **T6's, consumed** (`spec-delve-battle-profile.md:256`) | 1500 · 5000 — read from the session message, never a client constant |

**Not tunables, each saying so in its comment:** the motion durations M1–M9 (`information-architecture.md` §10 — the
vocabulary is structural; changing one breaks what direction *means*), the seven `.band-*` stacking tiers
(`shell/bandGuard.ts:46-56` — `scrim` added 2026-09-04), and `MaxConsecutiveTimeouts` (`BattleSessionRegistry.cs:67` — a
session bound, not a feel number). **No cap on anything the player earns** lives on this surface.

## 13. Numeric types

Souls, pool maxima, prices and damage are `long` on the server and reach real magnitudes under
[the one power ladder](../power/ssot-power-scale.md) — `Number.MAX_SAFE_INTEGER` is reachable in normal play, and
JavaScript will not complain, which is why this is written down.

- Every `long` figure crosses the wire **as a decimal string** beside its `number`, in `Magnitude.exact`;
  `formatMagnitude` renders `exact` through `Intl.NumberFormat` on a `BigInt` when present, and the `number` otherwise.
- **Never** parse a `long` into a `number` and back, and **never** do arithmetic on a figure in the client — there is no
  client arithmetic on this surface at all, which is the cheapest possible way to satisfy the rule.
- Ordinals, counts, cells, rows, rung ordinals and band ints are `int`; ids are strings, compared ordinally. Per-mille
  meter fills are bounded ratios and stay `int` — the exemption is stated at the field.

## 14. Testing strategy

Web unit tests are colocated `*.test.ts(x)` under `src/`, run by `npm test` (vitest, `package.json:12`); browser tests are
`web/fusion-rpg-web/e2e/*.spec.ts` under `npm run test:e2e` (`:15`). The projection endpoint gets a
`tests/FusionRpg.E2E.Tests` row; nothing here needs a Core or Data suite of its own.

| Test | Asserts |
|---|---|
| `Stage_count_assertion_is_six` · `Built_stage_ids_are_named_separately` | The count moves with `decisions.md:114`; no id is declared and empty without being named (`spec-board-render.md:203`) |
| `Shell_has_no_delve_specific_branch` · `Route_round_trips_with_every_panel` | Source scan for `=== "delve"` under `src/shell/`; `?panel={pack,talk,event,object,supply,fight}` cold-loads stage-then-panel |
| `Esc_pops_one_panel_and_returns_to_the_same_graph_state` | GG-1; selection and camera survive |
| `Closing_the_fight_panel_is_not_a_retreat` · `Leaving_the_stage_resolves_nothing` | No `retreat`/`extract` decision is posted; the delve stays `Active` |
| `Three_timeouts_freeze_and_say_so` · `Reconnect_replays_then_goes_live` | `MaxConsecutiveTimeouts`; the room reads *in progress*, not *lost*; resume from the recorded prefix matches byte for byte |
| `Autopilot_party_pack_handles_are_disabled_with_a_reason` | The sibling ruling, plus `disabledReasonGuard` |
| `Only_the_summary_and_three_confirms_open_band_3` · `Reports_land_at_band_4_and_wait_behind_the_summary` | The band-3 opener lint over `stages/delve/`; drops, level-ups, joins and first clears toast, never dialog, and never race band 3 |
| `No_engine_token_reaches_player_text` · `Theta_and_permille_symbols_are_actually_caught` | `vocabularyGuard` over `stages/delve/` and `layers/delve/` with the new words; the `\b` finding — a fixture containing `Θ` and `‰` must fail |
| `Entry_kind_renders_as_a_phrase_never_the_enum` · `Party_labels_are_names_not_indices` | The `once`/`many` rule, since those words cannot be banned; no rendered string carries a bare party ordinal |
| `No_Dto_named_type_under_stages_or_layers` · `Every_rendered_number_has_a_unit_class` · `A_long_soul_balance_renders_exactly` | `contractGuard.ts:57`; `magnitudeGuard`; a value above `MAX_SAFE_INTEGER` renders digit for digit through `exact` |
| `Delve_stage_is_lazily_loaded` · `Keyboard_reaches_every_delve_action` | The entry chunk is unchanged (`npm run check:bundle`); focus order and `1`–`9` (`information-architecture.md:188`) |
| `Volume_matrix_declares_the_delve_collections` · `Map_FE_files_are_untouched` | `COLLECTION_SURFACES` 13 → 17, virtualize still exactly one; a diff scan over `stages/world/`, `features/world/`, `lib/bus/world.ts` |
| `The_picker_and_the_map_door_post_the_same_body` | G5's own criterion, shared with `spec-domain-catalog.md:371` |

## 15. Structure

```
src/stages/delve/  DelveStage.tsx · graph/ (rooms, doors, sight, the fight in place) · hud/ (party strip, pools,
                   haul, quests, initiative rail) · layers/ (Pack · Talk · Event · ObjectPrompt · Supply · Fight) ·
                   confirms/ (Descend · Extract · Retreat) · summary/ExtractionSummary.tsx · labels.ts (the one
                   id → message table) · session.ts (the SignalR client) · route.ts (delveRoute — the door's
                   only import from this module)
src/layers/delve/  DelvePickerLayer.tsx — the Sanctum descent door's band-2 picker
src/contract/{types,adapt}.ts  additive view types + adapters (§6)   ·  src/shell/railState.ts + src/app/routes.tsx
src/i18n/vocabularyGuard.ts    BANNED_WORDS + BANNED_SYMBOLS (§8)    ·  src/ui/volumeMatrix.test.ts (13 → 17)
src/FusionRpg.Server/DelveEndpoints.cs   GET /api/delve/{delveId}; the RpgHub DelveUpdated broadcast
data/tuning/delve-ui.v1.json
UNTOUCHED: src/stages/world/**, src/features/world/**, src/lib/bus/world.ts, WorldEndpoints.cs,
           src/stages/lawn/**, every sibling's Core file
```

## 16. Boundaries

- **Always:** six shell rows and zero shell branches · every wire type behind an adapter · every number with a unit class ·
  every disabled control with a reason · ids on the wire and one label table in the client · the server's answer refetched
  rather than predicted · the map FE untouched.
- **Ask first:** a seventh shell integration point · a rail entry for the Delve · a new `UnitClass` member · a client-side
  cache of anything a sibling resolves · a band reassignment for any surface in §7.
- **Never:** compute Θ, a band, a price, a chance, a roll, a pack fit or a sight radius in the client · render an engine id,
  a per-mille symbol or a party index · open a dialog for a drop · treat navigation as a retreat · finish a steered fight on
  autopilot · parse a `long` into a `number` · add UI to the map front end · a timer that spends the player's day · a cap on
  a rendered figure.

## 17. Success criteria

1. **G5:** the stage renders a live delve over SignalR; the Sanctum picker and the map-door request reach the same
   `POST /api/delve/start`; `vocabularyGuard` rejects every engine word, `Θ` and `‰` included.
2. The band-3 opener lint passes: one result, three confirms, nothing else. A fight survives a mid-round refresh and a
   network drop, resuming byte-identically from the recorded prefix.
3. A raid of four resolves with one steered band and three played, four named parties, four packs, and no party index
   anywhere in rendered text.
4. Every refusal in §10 is reachable in a test and renders as its stated surface — none reaches the player as a raw rule id
   outside the developer tree.
5. `CONTRACT_VERSION` is still `2`; `contractGuard`, `magnitudeGuard`, `bandGuard`, `keymapGuard`, `pendingCopyGuard`,
   `reactivityGuard`, `disabledReasonGuard` and `hexGuard` are green; the entry chunk is unchanged; the map FE diff is empty.

## 18. Interface — asks filed on siblings and other programs

| # | Ask | Owning file | Default if refused |
|---|---|---|---|
| 1 | `DelveProjection.For(delveId, playerId)` — the assembler `delve-scope` names but exposes no member for | `spec-delve-scope.md:347-348` | This module assembles it in `DelveEndpoints.cs` from the four readers already named there |
| 2 | A `DelveUpdated { delveId, revision }` broadcast on `RpgHub` | `spec-delve-battle-profile.md:296` (it owns the `RpgHub` change) | This module adds the broadcast; refetch-on-focus is the fallback |
| 3 | A `bandNames` registry file — named by the ladder, absent from the registry table | `spec-dungeon-registries.md:68-80`; named at `spec-difficulty-ladder.md:174-176` | `labels.ts` maps the band ordinal to a message until it lands |
| 4 | A display label per `nerveStage` member | `spec-dungeon-registries.md:80` | The three messages live in `labels.ts` |
| 5 | A `movable` flag per pack cell, so an autopilot party's grid is disabled at the model | `spec-loot-pack.md:226` | The stage derives it from the party's autopilot state |
| 6 | `provisionable[]` on `GET /api/delve/domains/{playerId}`, so the picker can build the provisioning step | `spec-domain-catalog.md:380` | The picker offers provisioning only from supplies already held, and says so |
| 7 | Player labels for the six interaction verbs and the room-object prompts | `spec-supplies-and-objects.md:362` | `labels.ts` holds them |
| 8 | `Magnitude.exact?: string` (optional, additive) and the `formatMagnitude` branch preferring it | `docs/design/spec-magnitude-and-units.md` / `game-gui-map.md:135-142` | This module adds it — additive, so no bump and no ADR is owed |
| 9 | A `UnitClass` member for a soul or resource **stock**, if `count` reads wrong to the owner | `docs/design/spec-magnitude-and-units.md` + a `decisions.md` ADR row (the `loamUnits` precedent) | `count`, as specified in §6 |
| 10 | `information-architecture.md` §4's band table, its band-2 row and §5's `Space` row — **landed during this session** by the verification pass | `docs/design/information-architecture.md:168`, `:170`, `:187` | Closed; kept here as the record of what this module owed |
| 11 | `COLLECTION_SURFACES` 13 → 17 with the four delve collections, virtualize still exactly one | `web/fusion-rpg-web/src/ui/volumeMatrix.test.ts:96` | Lands with this module |
| 12 | The map door navigates to `#/delve/{delveId}` on `{delveId, worldId}` | `world-stage-map.md:262-266` (already filed) | The door imports `delveRoute()` — one function, no UI; the map FE otherwise untouched |
| 13 | The four Banner names and the party-naming rule are this module's; no sibling owns them | — | Owned here, in `labels.ts` |

**Landed 2026-09-05 (verification pass), so the rows above are asks no longer:** `DelveProjection.For(delveId, playerId)`
named on `spec-delve-scope.md` (ask 1); the `DelveUpdated{delveId, revision}` broadcast on `spec-delve-battle-profile.md`'s
structure block (ask 2); display names per band member and per nerve stage on `spec-dungeon-registries.md`'s `bands.v1.json`
(asks 3, 4); `movable` per cell on `spec-loot-pack.md`'s `PackDto` (ask 5); `provisionable[]` on `spec-domain-catalog.md`'s
`DomainOfferDto` (ask 6); `Magnitude.exact?` and the stock-unit question filed on `docs/design/spec-magnitude-and-units.md`
(asks 8, 9); `information-architecture.md` §2.4a, §4's band table, §4's band-2 row and §5's `Space` row amended as
`decisions.md:114` requires (ask 10); the `BANNED_SYMBOLS` defect, the ten new words and the paired stage-count assertion
filed on `game-gui-map.md`, and `party-dungeon-map.md` row 17 corrected to match (it read `Θ` as a `BANNED_WORDS` entry,
which cannot match). Asks 7, 11, 12 and 13 were already owned here or already filed.

## 19. Drift found this session (report, not fixed here)

1. **`information-architecture.md` was amended in three of five places when this spec was drafted** — §1 (`:17`), §2.4a
   (`:113`) and §7 (`:225`) carried the sixth stage while §4's band table and §5's `Space` row still read *"Sanctum ·
   World map · Lawn · Battle"* and *"Lawn and battle only"*. The verification pass closed both during this session; the
   working tree now reads six stages at `:168`, this stage's panels at `:170` and the inert `Space` row at `:187`.
2. **`vocabularyGuard.ts` citations have moved** — `BANNED_WORDS` is at `:42-53`, `ALLOW_LISTED_PREFIXES` at `:16-37`,
   while `audit-2026-09-05.md:224` cites `:37-48` and `base-defense-ideal.md:989` cites `:16-33`. And **`UnitClass` is
   thirteen members at `contract/types.ts:33-55`**, not *"a sealed 12-class union (`contract/types.ts:28-44`)"* as
   `world-stage-map.md`'s `world-numbers` row has it — `loamUnits` landed 2026-09-04.
4. **FIXED 2026-09-05, the same day** (owner's instruction at the wave-5 gate). `BANNED_WORD_PATTERN`
   (`vocabularyGuard.ts:55`) could not match `Θ` or `‰` — every entry is wrapped in `\b…\b` and
   neither is a word character, so adding them as `party-dungeon-map.md:127` originally worded it was a silent no-op.
   §8's `BANNED_SYMBOLS` design shipped: a second list matched against the whole line, three fixture tests, 10/10 green.
   It immediately found two live violations — `features/aptitudes/AptitudesPage.tsx:58` and `ui/actor/ProgressionTab.tsx:103`
   both rendered the power index as its letter — now reading *"spent · power 47"*. This module therefore inherits a guard
   that already works, and §14's `Theta_and_permille_symbols_are_actually_caught` row is satisfied before the stage exists.
5. **`railState.ts:31` is correct as cited**, and there is **no `siege` id anywhere under `src/shell/`** — the fifth
   stage's row is unbuilt too, so this module adds the *second* declared-but-unbuilt id, not the third, and
   `spec-board-render.md:203`'s retirement of the `battle` id has not happened either.
6. **Nothing delve-shaped exists in the web tree.** A case-insensitive scan of `web/fusion-rpg-web/src` for `delve` returns
   zero hits: no route, view type, adapter or query key. Every path in §15 is new.
7. **No drift between the map row and the decisions row on the fight:** `party-dungeon-map.md:127` reads *"the fight drawn
   on the stage"* and lists only the pack, wild talk and event banner as layers — exactly `decisions.md:114`. §7's split,
   fight on the stage with its **input panel** at band 2, is this module's own reading of both, and its reason is there.
8. **`SectorView.dangerBand` renders as a `count` magnitude** on the map (`contract/adapt.ts:291`) — the map's business,
   but the exact shape §8 forbids here, named so the delve's adapters are not written by copying it.

## 20. Build checklist

```
[x] Subsystems: Game GUI bands and stages, the FE view contract, the magnitude/unit layer, the i18n and vocabulary
    guards, the shell rail and keymap, SignalR sessions, the party-dungeon read models.
[x] Read this session: party-dungeon-map.md (row 17, G5, external deps, build order); decisions.md:114 in full;
    design/information-architecture.md end to end; game-gui-principles.md GG-4/GG-5/GG-23; game-gui-map.md:135-146;
    world-stage-map.md (the freeze, the arbitration table, :262-266); standalone/spec-expeditions.md;
    base-defense/spec-siege-stage.md in full and spec-board-render.md:190-207; all sixteen approved siblings' surfaces.
[x] Code opened and verified in the working tree, not HEAD: railState.ts, app/routes.tsx, shell/{stageHost,layerStack,
    keymap,toastStack,bandGuard}, contract/{types,adapt,contractGuard}, i18n/{magnitude,vocabularyGuard,index},
    ui/disabledReasonGuard.ts, ui/volumeMatrix.test.ts:96, package.json:7-19, Core/World/Intel/Visibility.cs:6-16,
    Battle/Timeline/{BattleSessionRegistry,InteractiveIntentSource}.cs, Server/RpgHub.cs, data/tuning/actor-hud.v1.json.
[ ] Constraints not tested — nothing was run; this spec changes no code. The guard behaviours in §8 and §14 are read
    from the guard sources, not executed; the first build task is to turn each into a failing test.
[x] Gaps stated rather than hidden: the projection assembler, the hub broadcast, the bandNames registry, the
    provisioning offer and the Magnitude.exact field are §18 rows with defaults, not assumptions.
[x] No repo invariant contradicted: no injector work; no private f(level) and no Θ arithmetic in the client; no cap on a
    rendered figure; no wall-clock pacing, stamina timer or energy gate; presentation numbers in data/tuning; no SQL; the
    map FE untouched. Every RPG feature stays in the RPG layer — this module is the window and owns no rule.
```
