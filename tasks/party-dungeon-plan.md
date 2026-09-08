# Plan: party-dungeon — "the Delve"

**Status: DRAFTED 2026-09-05, for owner review.** Built from the seventeen APPROVED module specs in
[docs/architecture/party-dungeon/](../docs/architecture/party-dungeon/) and the approved capability map
[party-dungeon-map.md](../docs/architecture/party-dungeon-map.md). Task list:
[party-dungeon-todo.md](party-dungeon-todo.md). Nothing here re-decides a spec — where this plan and a
spec disagree, the spec wins and this file is wrong.

---

## 1. What is already true (checked in the working tree, 2026-09-05)

The map named a **G0 prerequisites gate**. Its premise was checked against the source of truth today
rather than assumed, and **it is satisfied — there is no gate to wait on:**

| G0 clause | State | Evidence |
|---|---|---|
| Four `decisions.md` rows (P1–P4) appended | **Done** | `decisions.md` carries *Game GUI — sixth stage `delve`*, *World store — delve worlds*, *Status SSOT + Resource model — nerve*, *Action model — extended action slots*, all dated 2026-09-05 |
| Propagations made | **Done** | Landed across waves 1–5 verification passes: `item-map` §9, `seedsmith-map`, `action-map` §13, `effect-atom-map` §19, `status-ssot` §9, `unique-actor-runtime` §11.1, `demon-system-map`, `world-stage-map`, `world-map-program`, `standalone-rpg-map`, `information-architecture` §2.4a/§4/§5, `game-gui-map` |
| `threat-audit` scheduled | **Not done, and not blocking** — see §5 | Measured today: **841 species anchors, 184 carry a `threatBand`, 657 do not** |

Two external dependencies the map listed as gating are **already built**, so two modules that looked
blocked are not:

- **`battle-clock-profile`** — `MaxRounds` and `RoundDurationMs` are on `BattleModeProfile`
  (`BattleModeProfile.cs:153,158`). `delve-battle-profile` can add its row immediately.
- **`siege-ai`** — `src/FusionRpg.Core/Battle/Siege/SiegeAi.cs` ships and dispatches on `SideOf`.
  The automated policy for un-steered parties is available; raids of 2 and 4 are not blocked.

One is genuinely absent and is handled by a refusal the specs already wrote: the **`consumable`
`ContainerKind`** (D27) is still `ConsumableContainerKindAvailable = false` (`ConsumableDef.cs:201`),
and **A3's item-cost row on actions** does not exist (`ActionRow.cs:122-123` is resource ids only).

---

## 2. Dependency graph

```
                       dungeon-registries (1)
                                │
                    dungeon-seed-contract (2)
                    ┌───────────┼───────────┐
             delve-scope (3)          difficulty-ladder (5)
                    └───────────┬───────────┘
                       delve-graph-roll (4)          ── CHECKPOINT G1
                                │
                    encounter-generator (6)
                                │
                   delve-battle-profile (7)
                                │
                     delve-attrition (8)             ── CHECKPOINT G2
                    ┌───────────┴───────────┐
              event-deck (9)          dungeon-loot (10)
                    └───────────┬───────────┘
                        loot-pack (11)
                                │
                 supplies-and-objects (12)            ── CHECKPOINT G3
        ┌──────────┬────────────┴──────┬─────────────┐
   wild-room  delve-quests      domain-catalog   unique-pipeline
      (13)        (14)               (15)             (16)         ── CHECKPOINT G4
        └──────────┴───────────────────┴─────────────┘
                                │
                        delve-stage (17)              ── CHECKPOINT G5
```

Arrows point one way; there are no cycles. `unique-pipeline` depends on `dungeon-loot` for its table
binding and on `dungeon-seed-contract` for the adapter — **never on the stage**.

**Why this order.** Every model-free module comes first: a registry, a schema, a loader, a pure roller
and a composer produce real value with zero tokens spent, and they make each generator's inputs
reviewable before a single call is made. `encounter-generator` is early because it is where `Θ_content`
finally meets the species offset, and every later generator reads that number. The seedsmith pipelines
run once the runtime that consumes their output exists, so a bad corpus is caught by a real consumer
rather than by review. The stage is last because every surface it draws is another module's read model.

---

## 3. Phases

Each phase ends at a checkpoint that reviews work already done. **None of these blocks the *start* of
anything** — a checkpoint that fails sends its own phase back, it does not freeze the program.

### Phase 1 — the skeleton (modules 1–5, wave 1) · 28 tasks

Registries and tuning schemas; the seed contract and its offline adapter; the delve world row and its
store; the pure graph roller; the ten-rung ladder and the first production composer of `Θ_content`.
Every task here is pure C# or JSON plus tests — no model calls, no UI, no battle.

Parallel after task **D1.6**: `delve-scope` (D1.12–D1.17) and `difficulty-ladder` (D1.18–D1.22) do not
touch each other's files and can be built in either order or together. `delve-graph-roll` needs both.

**D1.28 is the map door**, and it sits in this phase because owner decision 2 put it there: *"Entrance:
both from day one — the Sanctum picker **and** the world-map door ship in the first wave … Scope
consequence accepted by the owner: the first wave touches the map FE."* It is the one authorised
exception to the map-FE freeze, and it is deliberately the smallest possible change — an additive action
row, no layout, no new UI.

> **CHECKPOINT G1 — scoped world.** A delve world row exists beside a map world; `GetActiveWorld`
> returns the map; `WorldValidation` accepts a rolled graph under the delve profile and still rejects it
> under the map profile; **all world goldens byte-identical**; `TurnEngine.Step` is never called on a
> delve world, proven by a guard test.

### Phase 2 — a room is a fight (modules 6–8, wave 2) · 22 tasks

Encounter anchors resolve into `BattleActorSetup`s at `θ = Θ_room + thetaOffset`; the `delve` profile
row and the explicit `BattleEngine.Resolve` call; carry-across-rooms state, nerve, Downed and the
extraction settlement.

The single largest risk in the program lives here — see §4.

> **CHECKPOINT G2 — a room is a fight.** One rolled room resolves through `BattleEngine.Resolve` with
> the `delve` profile and an automated intent source at `Θ_room + thetaOffset`, byte-identical on
> replay; **all four battle hashes, the 32-seed sweep and the four expedition tier hashes unchanged**;
> a steered fight freezes and resumes from its decision log.

### Phase 3 — a delve is a run (modules 9–12, wave 3) · 30 tasks

The event deck and its four filters; the first production host of `LootPipeline`; the 4×10 carry grid
with a first-fit-decreasing arranger; supplies and room objects.

Parallel: `event-deck` (D3.1–D3.9) and `dungeon-loot` (D3.10–D3.17) share no files and run together.
`loot-pack` needs both; `supplies-and-objects` needs `loot-pack`.

> **CHECKPOINT G3 — a delve is a run.** A full solo delve on autopilot: rooms, events, loot into the
> pack, extraction. The souls-per-minute regression holds (two row-1 rooms then extract loses to a clean
> run); hunger binds between rests; a downed demon sits out N **delves**; a permadeath rung Retires a
> `downedOnce` demon at extraction.

### Phase 4 — content (modules 13–16, wave 4) · 31 tasks

All four are independent of each other and can be built in any order or all at once — they share only
files their own specs already reconciled (`RpgStore.Delve.cs`, `DelveEndpoints.cs`), and those seams are
named per task.

This is the first phase that spends model tokens: `dungeon-seed-contract`'s pipeline half runs here to
produce the six first-ship domains, and `unique-pipeline` runs the uniques extension.

> **CHECKPOINT G4 — content.** Six domains from the pipelines pass the schema audit, the budget check
> and a byte-identical rerun; the encounter cell-coverage metric passes per domain; a four-party raid
> resolves with per-party packs, pity and hauls, and one boss fight inside the fight-length band.

### Phase 5 — played (module 17, wave 5) · 13 tasks

The sixth stage. Six shell rows and zero shell branches; the room graph as the stage with the fight
drawn on it; six band-2 panels; one band-3 summary; band-4 reports queued behind it.

> **CHECKPOINT G5 — played.** The stage renders a live delve over SignalR; the band-3 opener lint holds;
> `vocabularyGuard` rejects every engine word — `Θ` and `‰` included; the Sanctum picker and the
> map-door request reach the same `POST /api/delve/start`.

**One G5 clause is already satisfied, ahead of the phase.** The vocabulary guard could never match `Θ`
or `‰` (`BANNED_WORD_PATTERN` wraps every entry in `\b…\b`; neither is a word character), so the map's
own instruction to add them to `BANNED_WORDS` would have been a silent no-op. Fixed 2026-09-05 at the
owner's instruction: `BANNED_SYMBOLS` + `BANNED_SYMBOL_PATTERN` in `i18n/vocabularyGuard.ts`, matched
against the whole line, with three fixture tests — and it immediately caught two live violations
(`AptitudesPage.tsx:58`, `ProgressionTab.tsx:103` both rendered the power index as its letter), now
fixed. 10/10 green.

---

## 4. Risks

| # | Risk | Why it is real | Mitigation |
|---|---|---|---|
| **R1** | **A golden moves in phase 2.** `BattleActorSetup` gains seven additive fields and `BattleModels.cs` is on the hashed path for expedition tiers | Battle goldens hash `BattleReport` only, but the four expedition tier hashes include `BattleSetup`. A field that serialises when defaulted moves them | Every added field is nullable and `[JsonIgnore(WhenWritingDefault)]`. **D2.8 is a dedicated hash task** run before any behaviour rides on the fields: all four battle hashes, the 32-seed sweep and the four expedition tier hashes re-run and compared byte for byte. If one moves, the field is wrong, not the golden |
| **R2** | **657 of 841 species anchors have no `threatBand`**, so `threatWindow` filters mean little | Measured today. `threat-audit` is another program's module and is not scheduled | Not a blocker: `encounter-generator` **refuses loudly** on a null band rather than defaulting (its §2), and the six first-ship domains draw only from the 184 anchors that carry one. Coverage rises when the audit lands; nothing is rewritten |
| **R3** | **A world golden moves in phase 1.** `rpg_worlds` gains two columns | The header row hashes `TemplateId, Seed, CurrentTurn` only, so it should not — but "should not" is a claim | **G1 asserts it**, and D1.13 re-runs the world golden suite as its own verification step before anything else touches the store |
| **R4** | **`RpgStore.Delve.cs` is written by six modules** (scope, pack, event-deck, loot, wild, quests) | Six specs each add columns or a transaction to one file. Merge pressure and accidental double-writes | The specs already assigned one owner per column and one transaction boundary per writer. Each task names the exact members it adds; `CloseDelve` gains one call per module in a fixed order stated in D3.16 |
| **R5** | **The `consumable` kind (D27) and the item-cost row (A3) do not exist** | Verified: `ConsumableDef.cs:201` is `false`; `ActionRow.cs:122-123` is resource ids only | Both specs already ship the refusal: `battle`-context supply use and `act.capture`'s seal cost refuse with `capture.not-landed` behind a `CrossProgramLandedFlags` shape. **Rest and curio uses do not wait on A3.** No task is blocked; two are scoped smaller |
| **R6** | **A balance number lands as a `const`** | Seventeen specs, hundreds of numbers | Every task that adds a number names its `data/tuning/` file and key in its acceptance line. `python scripts/audit-magic-numbers.py --targets M1` is a checkpoint step in every phase |
| **R7** | **A magnitude overflows or is typed `int`** | `P(Θ)` is quadratic; a per-mille `int` breaks at Θ 3,213 | `long` for every magnitude, stated per task. `python scripts/audit-overflow.py` at each checkpoint |
| **R8** | **The map front end is touched beyond the one authorised door.** It is frozen pre-refactor | Phase 5 sits next to it, and D1.28 deliberately opens it once | The freeze has **exactly one authorised exception** — owner decision 2's map door, built additively in D1.28. Everything else: D5.1 adds `delveRoute()` as the door's only import, and G5's diff scan over `stages/world/`, `features/world/` and `lib/bus/world.ts` must show **only** D1.28's action row |

---

## 5. External dependencies — none of them blocks a task

Each ships behind a default the owning spec already wrote, so the program never waits.

| Dependency | Owner | Default until it lands | Affects |
|---|---|---|---|
| **Contracts upkeep and slot/ritual prices on cleared-content Θ** | `demon-system-map` — `demon-contracts` follow-up | **The map schedules this in *this program's first wave*** (`party-dungeon-map.md:99`) because it is the sink that keeps binding costly past the pin (review S2-8). `ContractPolicy.BaseUpkeepPerDay(rarity)` is a flat `int` while every other contract price is Θ-scaled. `spec-dungeon-loot.md:196-198` files it and explicitly builds nothing. Tracked as **F10**; until it lands the Delve's faucet outruns that one sink | none blocked — F10 |
| `threat-audit` (657 anchors) | `demon-seed-map` module 7 | Refuse a null `threatBand`; first-ship domains draw from the 184 that have one | D2.1–D2.4 coverage |
| `consumable` `ContainerKind` (D27) | `item-map` | Supplies instantiate as items; the `battle` use context is refused | D3.24–D3.26 |
| Item-cost row on actions (A3) | `action-map` | `act.capture` refuses `capture.not-landed`; rest and curio uses are unaffected | D3.24, D4.6 |
| `structure-schema` 18th field `interaction` | `base-defense-map` 23–29 | v1 objects are curios in the event deck, exactly as specced | D3.27–D3.30 |
| `siege-board` / `board-render` (A10) | `base-defense-map` | v1 ships 1-D rank on `SideIndex`; the 2-D board is adopted later | D2.3, D5.4 |
| `world-generator` entrance placement | `world-map-program` wave 4 | The Sanctum picker offers found domains; no map placement needed | D4.20 |
| `DemonMintSpec.Level`, `SummonRoller` `poolFilter`, a personality mint override | `demon-system-map` | Mint at level 1 (today's line); the altar pulls the whole summonable catalog; `altar.poolFromDomain` stays `false` | D4.5, D4.7 |

---

## 6. Parallelism

| Phase | Runs together | Must be sequential |
|---|---|---|
| 1 | `delve-scope` ∥ `difficulty-ladder` after D1.6 | registries → seed contract → those two → graph roll |
| 2 | — | encounter → profile → attrition (each consumes the last) |
| 3 | `event-deck` ∥ `dungeon-loot` | then `loot-pack`, then `supplies-and-objects` |
| 4 | all four modules | — |
| 5 | contract + shell rows ∥ panel components after D5.3 | projection endpoint first |

---

## 7. Definition of done

The program is done when G5 passes **and**:

- `dotnet test` is green across Core, Data, Guard, E2E; `npm test` is green in `web/fusion-rpg-web`.
- `python scripts/audit-overflow.py` reports zero critical, and `audit-magic-numbers.py --summary`
  shows no new bare literal in a Policy/Catalog/Rules/Ruleset/Math file.
- `.\scripts\guard-single-writer.ps1`, `guard-secondary-no-unity.ps1`, `guard-funnel-delta.ps1` and
  `guard-dal.ps1` pass.
- The four battle hashes, the 32-seed sweep, the four expedition tier hashes and every world golden are
  byte-identical to their pre-program values.
- **Both entrances work** (owner decision 2): the Sanctum picker and the world-map door reach the same
  `POST /api/delve/start` with the same body.
- A player can open the Sanctum descent door, pick one of six domains at a named difficulty, walk a
  room graph, steer one band through fights while three are played, fill a 4×10 pack, and extract —
  with no engine word, no per-mille sign and no party index anywhere on screen.

## 8. Out of scope

The injector and anything under `pvz.*`; `BattleEngine`'s round order or resolver math; the
`EffectBag`/Funnel/Writer paths; the world turn phases; drop volume, drop-table weights and the armoury
(D26); `SummonRoller`'s rates and pity; expedition tiers or hashes; the map front end beyond the single
door owner decision 2 authorised (D1.28).
