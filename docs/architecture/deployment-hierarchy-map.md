# Capability map: deployment hierarchy

**Status: APPROVED 2026-09-13** — module boundaries, build order and the two prerequisite
`decisions.md` rows (§Prerequisites) all approved by the owner the same day; the rows are appended to
`decisions.md` verbatim, matching the `party-dungeon-map.md` precedent (its four rows were approved
with its map the same day). Module specs are written per wave, in dependency order, each verified
against code and the ideal before it is placed.

**Ideal it implements:** [deployment-hierarchy-ideal.md](deployment-hierarchy-ideal.md) — the tree
(inherit/settle), injury tiers, corpse-drop/decay/retrieval (owner-locked 2026-09-13), and both
enrichment tracks (void/baggage/revisit — V1–V6; durability/repair — D1–D6), all locked by the owner
2026-09-13 (third clearing round). **Module specs:** `deployment-hierarchy/`, one per module id below.
**Plan / tasks:** written after this map is approved, at `tasks/deployment-hierarchy-plan.md` ·
`tasks/deployment-hierarchy-todo.md` — the prefixed pair this repo's parallel-program convention
requires; the bare pair belongs to the perf stream and is never a fallback.

---

## What this program is

The **parent → child inherit/settle tree** that makes a deployment (a lawn Bound spawn, a delve party
slot, a siege combatant, an expedition seat) start from what its parent (roster, legion, garrison,
muster) actually has — pools, statuses, sticky flags — instead of always-full; and everything that
follows from a child **dying for real**: graded injury tiers instead of a binary flag, a corpse-cache
that holds the dead's rolled gear at the place of death, a deterministic decay clock, and three ways
to get it back (same-run baggage, revisit, or a map-less retrieval mission). Item durability/wear/
repair rides the same settlement points and ships alongside as its own track.

## What it is not

- **Not a change to what PvZ is.** Zero injector combat-write changes. Every row below is `Core/`,
  `Data/`, `Server/` — the injector's only new surface is reading `CarryInPools`/status carry-in at an
  existing bind point, never a new Unity write.
- **Not a second ActorHub compose.** Pool/status inheritance is data snapshotted at deploy time and
  applied through `ActorResourcePools`/`StatusRuntime.Apply` exactly as today's single Bound path
  does; no child invents its own combat-derived fold.
- **Not a second power ladder.** Injury-tier severity reads `%HP lost` through the existing combat
  math; `ssot-power-scale.md` §10 gains a reviewed row for it, never a private `f(level)`.
- **Not the world-map or siege death paths.** `DistrictAssaultResolver` never binds uniques and
  siege has no `instanceId ↔ ptr` table today (real gap, ideal doc "Real gap" table) — this program
  builds the shared machinery (tiers, cache, decay, retrieval) generically, once, and the world/siege
  *edges* land as follow-on work when those programs produce real deaths. No stub combat is specced
  to stand in for them here.
- **Not automatic bounties.** Bounty hunts in this program are player-posted, run by the player's own
  empire units as expedition-kind missions. A standing automatic-bounty system is its own, separate,
  bigger program under idle mechanisms — tracked, not built here.
- **Not a rewrite of `loot-pack`.** Baggage reuses its shipped `PackGrid`/`PackMoves`/floor
  machinery unmodified; this program's only edit to that program's surface is the §7 "Wiped"
  settlement rule, filed as an ask and built only after that program signs off.

## Assumptions — correct these now

1. Wave scope matches the ideal doc's owner-cleared wording verbatim: this map's build order covers
   **lawn + delve + expedition retrieval + durability/repair**. World-map and siege injury/cache/drop
   paths are **tracked dependencies**, not modules of this map, and are not proposed for build.
2. The `wound.*` status family (Prerequisite P1 below) follows the `nerve.*` precedent exactly
   (`party-dungeon-map.md` row P3, `StatusCatalogBootstrap.cs:66`): new ids, closed kind/payload
   vocabulary only, a registry-equality test, and the DESIGN-GATE status row moved in the same
   change — never a runtime-authored kind.
3. `corpse-cache` is a **new table family** in `FusionRpg.Data` (`rpg_corpse_cache*`), not a
   repurposing of `rpg_item_assignment` — the strengthen-pass correction (F2/I1, ideal doc line 237)
   already ruled out repointing assignment rows because the PK `(specimen_id, role)` has no cache
   column and would collide across two dead specimens' same-role rows.
4. The `loot-pack` §7 overturn (V1/V4) is **this program's ask, not this program's decision** — the
   `cache-field-access` module is gated on the party-dungeon program's sign-off, exactly as
   `unique-pipeline` was gated on an external module in the party-dungeon map (row 16).

## Prerequisites — `decisions.md` rows owed before any module is named for build

Drafted here for approval; on approval they are appended to `decisions.md` verbatim, matching how
party-dungeon's P1–P4 rows were drafted with its map and appended the same day.

| # | Row | Draft text |
|---|---|---|
| **P1** | Status SSOT — `wound.*` widen | *"**`wound.*` joins the closed status vocabulary** (Status SSOT row amended 2026-09-13 with the deployment-hierarchy map, following the `nerve.*` precedent set 2026-09-05). Injury-tier severity is a `wound.*`-family status container of atoms — timeless, attacker-less, exhaustion-shaped (`ExhaustionPolicy.cs` shape) — applied through `StatusRuntime.Apply` at injury-grading time, never at the power-contest step (`ResistanceEvaluator.cs:294-302` already excludes attacker-less applies from the contest by construction). The anti-spiral guarantee is **not** runtime-owned (`StatusRuntime.Apply`/`StatusCatalog.Register` enforce nothing); `wound.*` ships its own opt-in constructor check, mirroring `ExhaustionPolicy.cs:59-66`/`NervePolicy.cs:68-74`. The worsening counter lives in durable per-specimen state (the `rpg_unique_actor_pools`/`rpg_unique_actor_recovery` family, `RpgStore.cs:547-554`; mid-delve it rides party state like `NerveStacks`) — never on `StatusInstance`, which carries no count. Catalog id count (today 24 including `nerve.*`) grows by the tier count; DESIGN-GATE's status row moves in the same change."* |
| **P2** | Deployment hierarchy SSOT | *"**A deployment child inherits a parent snapshot and settles back deltas — never a live reference, never a rewritten past.** Every child (lawn Bound, delve party slot, siege combatant, expedition seat) carries three snapshotted things at deploy time: pool values (`ActorResourcePools.FromStored`, seeded through a `CarryInPools`-shaped field already on `BattleModels.cs:192`), status specs (declarative, applied through `StatusRuntime.Apply` at the child's first tick — never a live `StatusInstance` reference, which is keyed `entity:{ptr}` and dies with the match), and sticky flags (`Downed`/`DownedOnce`/`NerveStacks`/`Wounds`/phase). Settlement is exactly-once per child, idempotent on `(parentId, childId)`. A dead unique/commander's assigned **rolled** gear moves — never copies — into a per-place `rpg_corpse_cache` row instead of today's `item/ssot-inventory.md:452` 'gear is never lost' rule, which is overturned for the real-death case only. Recovery clocks are per-deployment-kind (delve-counted, world-turn, or priced ritual) — never wall-clock, never a single unified clock. Troop stacks (species-shaped, `WorldEntityMember`) are exempt from all of the above: HoMM3 top-unit count arithmetic only, no per-unit wounds, no inventory. Ideal: [deployment-hierarchy-ideal.md](deployment-hierarchy-ideal.md); map: [deployment-hierarchy-map.md](deployment-hierarchy-map.md)."* |

**Propagations owed alongside (evidence rule), not gated on approval:** `item/ssot-inventory.md:452-453`
amended in place once `corpse-cache` ships (overturn note already drafted in the ideal doc); `status-ssot.md`
id count bumped from 24; `ssot-power-scale.md` §10 gains the injury-tier row once its formula is bound to
an existing potency/power read (ideal doc Tunables table, "Injury-tier thresholds" row).

## External dependencies — other programs' modules this program consumes or asks of

| Dependency | Owner map / module | What this program needs | Gate |
|---|---|---|---|
| `spec-loot-pack.md` §7 "Wiped" amendment | `party-dungeon-map.md`, module `loot-pack` | The approved spec's carry-in-returns-home / haul-destroyed rule changed so nothing returns home on any delve wipe (V1/V4) | before `cache-field-access` |
| `RetireUniqueActorUnlocked` verification + cache-contents salvage/stale guard governance | `item-map.md` | Confirm retire-release targets the cache, not the armoury, on a death-Retire; which salvage/stale guards govern cache rows (`SalvageGuards.cs:51` vs `RpgStore.Workbench.cs:170-172` split) | before `corpse-cache` |
| Repair verb + `op_kind` (closed at ten today) | `item-map.md` | A reviewed ask-first add: field-touch-up + workbench repair recipe rows, shard-leg material class at high rungs, repair-attempt destruction chance. **Cross-reference (strengthen pass 2026-09-13, finding F22):** `loam-relics-and-wonders-ideal.md`'s real-gap table separately asks the item program to relax `unique`'s `KindSpec` (or add a sibling kind) for non-equip relic items — an independent, uncoordinated ask on the same item-program registry surface in the same window. Not merged; named so the item program can review both together if it chooses | before `item-durability-repair` |
| `CloseDelve` hook-order amendment + replay-idempotency key slot | `party-dungeon-map.md`, module `delve-attrition`/`dungeon-loot` | A named slot in the ordered settlement transaction for corpse-drop and decay-clock-start to hook into | before `corpse-cache`, `cache-decay-void` |
| Retrieval/bounty soul-sink reason | `SoulSinkPolicy` (souls economy, no dedicated map — `decisions.md` "Caps" row family) | A named `Reasons.*` + priced Θ for the retrieval sink | before `cache-retrieval-mission` |
| New expedition kind + cache-target column + gear-return manifest seat | `docs/architecture/standalone/spec-expeditions.md` (no capability map — amend in place) | `rpg_expeditions` today has no target column and the manifest has no gear seat (`:46-47`, `:38`) | before `cache-retrieval-mission` |

**World-map and siege are explicitly not external dependencies of this map.** Their death paths do
not exist yet (real gap); wiring this program's caches to them is deferred work owned by whichever
session builds `world-actor-combat` / siege's `instanceId ↔ ptr` binding, tracked in the ideal doc's
Wave 3 note — not a gate on any module below.

## Modules

Stable kebab-case ids, chosen once. Every module is provable with the game closed (Wave-1/2 scope is
entirely lawn + delve + expedition — no Fusion-only mechanic).

| # | Module id | Responsibility | Depends on | Wave |
|---|---|---|---|---|
| 1 | `deploy-carry` | Wire the parent→child inherit side that is currently inert: populate `CarryInPools` on every battle/delve/lawn/siege setup (today null at every call site, `BattleModels.cs:192`); make `DelveCarryIn.Apply` carry pools into the next room's setup (`DelveCarry.cs:33`); map `DelveMemberState.Statuses` into the next setup's initial status specs (`EventOutcomeDispatch.cs:243` appends but nothing consumes; `DelveCarry.cs:34` has no producer); keep troop representation as `Hp/Wounds` headcount, unchanged (locked, revisit only on playtest evidence) | — | **1** |
| 2 | `injury-tiers` | The `wound.*` status family (P1): tier thresholds reading `%HP lost` past a relative bound (Battle Brothers/XCOM shape) bound to an existing potency/power read; the durable per-specimen worsening counter; the untreated-worsening clock (advances on the specimen's own settlement clock, never wall time); per-deployment-kind recovery clocks (delve-counted `Recovering`, world-turn legion rest, priced ritual) | `deploy-carry`; row P1 | **1** |
| 3 | `corpse-cache` | New `rpg_corpse_cache` table family; on a real death (hardcore-delve `Retired`, or a wound tier worsened to death) or **any** delve wipe (V1/V4), **move** (never copy) the dead's assigned rolled gear — and, on a delve wipe, the whole party's carry-in gear and unbanked haul too — out of both assignment tables (`rpg_item_assignment` unique, `rpg_player_item_assignment` commander pouch) into a cache row pinned to the place of death; deploy-time-snapshot anti-fraud (the drop set is fixed at deploy, not at death); downed-but-`Recovering` members (partial-casualty, not a full wipe) drop nothing | `injury-tiers`; row P2; external `RetireUniqueActorUnlocked` verification, `CloseDelve` hook slot | **1** |
| 4 | `cache-decay-void` | Per-item seeded survival-roll decay (`SeededRng.DeriveStream`, idempotent per `(cache, tick)`); clock-start rule — `CloseDelve` for a delve cache (not the moment of death), immediate for a lawn cache (no durable place-row to wait on); ticks on the world-stage turn (`TurnCalendar.cs`) in the `Events` phase of `TurnEngine.Step`, frozen while idle; starting-shape target ≈112 turns (one calendar season-cycle); the void as the single named sink for a cache with no reachable place | `corpse-cache` | **1** |
| 5 | `cache-field-access` | Baggage: survivors loot a dead teammate's gear into free `loot-pack` `PackGrid` cells, live, room to room — no new container, consumes the existing `Pack.Place`/`PackMoves` interface; revisit-loot of a cache still pinned to its delve room, claim idempotent per `(cache, party)`; certain-on-reach (no second roll — decay is the only pre-arrival gamble) | `corpse-cache`, `cache-decay-void`; external `loot-pack` §7 sign-off | **1** |
| 6 | `cache-retrieval-mission` | A new map-less expedition kind targeting a *cache id* (no squad-on-a-board): cache-target column on `rpg_expeditions`, a gear-return manifest seat beside souls/materials/XP, priced and timed on the expedition wall-clock (the one legal wall clock in this program); the bounty-quest wrapper — a Place-7 quest naming the cache, run by the player's own empire units, no NPC hunter faction | `corpse-cache`, `cache-decay-void`; external expedition kind + souls sink reason | **1** |
| 7 | `item-durability-repair` | Per-rolled-instance `(max, current)` durability, `max` DERIVED (never authored); per-battle settlement wear decrement (never per delve-room); two-tier repair — field touch-up (substrate only, carried tool+materials required, caps at partial/eroding) and workbench (full class vocabulary, falls back to partial/eroding when material is short, shard leg at top rungs only); every repair attempt carries a tunable destruction-failure chance; death-drop extra decay hooks `corpse-cache`'s move-to-cache event; commander-pouch gear wears identically, same mechanism, different table scope | `deploy-carry` (battle-settlement hook); soft link to `corpse-cache` for the death-drop-decay sub-feature only, not a hard block; external repair verb/`op_kind` | **2** |

**Dependency direction, no cycles.** `deploy-carry` → `injury-tiers` → `corpse-cache` →
`cache-decay-void` → `{cache-field-access, cache-retrieval-mission}`. `item-durability-repair`
branches off `deploy-carry` directly and does not sit on the injury/cache chain — its one integration
point with `corpse-cache` (death-drop extra decay) is additive and does not block the rest of the
module from shipping first.

## Build order

```
Wave 1  deploy-carry → injury-tiers → corpse-cache → cache-decay-void → { cache-field-access ∥ cache-retrieval-mission }
Wave 2  item-durability-repair   (branches off deploy-carry; buildable in parallel with any of Wave 1 past module 1)
```

**Why this order.** `deploy-carry` first because every other module reads or writes through the
carry/settle points it wires — building injury tiers against a tree that does not yet carry statuses
across a redeploy would mean a wound nobody can see on the next fight. `injury-tiers` before
`corpse-cache` because "real death" is defined partly in terms of a wound tier worsening to death —
the degenerate hardcore-delve-`Retired` case does not strictly need it, but grading death in one place
keeps the story coherent. `cache-decay-void` after `corpse-cache` because there is nothing to decay
until a cache exists. The two retrieval modules (`cache-field-access`, `cache-retrieval-mission`) are
parallel siblings — one is a physical-presence read against the live cache, the other is a mission
type against the decayed/void one; neither depends on the other. `item-durability-repair` is a
sibling track the ideal doc's own wave framing already calls out as non-blocking in either direction.

## Gates

| Gate | Proves | After |
|---|---|---|
| **G0 — prerequisites** | P1/P2 appended to `decisions.md`; `loot-pack` sign-off and the `CloseDelve` hook-slot ask both acknowledged by their owning programs | before wave 1 build |
| **G1 — the tree carries** | A delve party redeployed into a second room starts from its `CarryOut` pools and status specs, not rest-max; a battle actor's `CarryInPools` is non-null end to end; replay is byte-identical | module 1 |
| **G2 — a wound is real** | A specimen graded into a wound tier fights measurably weaker on its *next* deployment (not just the current one); an untreated serious wound advances and can kill on its own settlement clock; the anti-spiral check refuses a runaway stack | module 2 |
| **G3 — a death has weight** | A real death (hardcore-delve `Retired`, or a worsened wound) moves the dead's rolled gear into a cache, not the armoury; a delve wipe (any rung) empties the whole party's pack into one cache; downed-but-`Recovering` drops nothing; the deploy-time snapshot defeats a mid-fight-strip probe | module 3 |
| **G4 — decay is deterministic** | Replaying a cache's full settlement-to-now history reproduces identical survival rolls and never double-destroys a row; a delve cache does not decay before `CloseDelve`; a lawn cache decays from the moment of death; an unreachable cache is in the void, not limbo | module 4 |
| **G5 — three ways back** | Same-run baggage pickup through the existing pack grid; revisit-loot of a cache still at its room; a map-less retrieval mission recovers a void cache under a priced, timed manifest; a field party and an expedition racing the same cache produce exactly one winner | modules 5–6 |
| **G6 — gear wears and mends** | An item's `current` decrements once per battle, never per room; it goes unusable at zero, never destroyed by wear alone; a field touch-up without carried materials refuses; a workbench repair with short material falls back to partial, never refuses outright; a repair attempt can destroy the item at its tunable rate; commander-pouch gear wears through the same path | module 7 |

## What this program does not touch

`BattleEngine`'s round order or resolver math; the `EffectBag`/Funnel/Writer paths; `ActorHub`'s
compose (this program only contributes pool/status snapshots through the existing subsystem seams);
drop volume, drop tables' weights, or the armoury's uncapped-stash rule (D26); `SummonRoller`; PvZ's
Unity write surface; the world-map or siege combat resolvers; automatic/standing bounty generation.

## Open items carried from the ideal

None owner-facing at the map level — all twelve V/D questions and the four second-clearing-round
questions are closed. Two spec-level items each module must answer in its own text: `injury-tiers`
owes the exact `base + %lost × scale` binding to an existing potency/power read and its
`ssot-power-scale.md` §10 row (flagged, not yet written); `item-durability-repair` owes the exact
destruction-failure-chance split between the field and workbench tiers (a balance number, not an
architecture one).

---

## Filed by the `species-gear-chain` initiative (2026-09-13)

Asks raised by [species-gear-chain-map.md](species-gear-chain-map.md) and its module specs. **Nothing here is built or approved** — each is an ask-first boundary this program owns, filed so it is visible to the owner rather than living only in the requesting map.

| # | Ask | Requesting module | Evidence |
|---|---|---|---|
| 1 | A new **per-instance crafting-potential pair** beside durability's `(max, current)` on `effect_instance` | ``craft-risk-ladder`` | Module 7 is **owner-locked D1–D6** and written against shipped code |
| 2 | **Potential exhaustion as a new decay source**, beside battle wear | ``craft-risk-ladder`` | D1 holds unamended — decay drives to zero; only a **repair attempt** may destroy |
| 3 | ⛔ **An amendment to module 7's own Never list.** `spec-item-durability-repair.md:408` forbids *"per-item authored durability (max is DERIVED, **never authored, matching every sibling DERIVED field**)"* — but the owner decided potential is *derived **with an authored per-base-type override*** | ``craft-risk-ladder`` | Decision at `gear-climb-ideal.md:350-352`; ⚠ the same doc calls it open at `:273`. **Never reconciled with `:408`** |
| 4 | **Schema ownership of `data/tuning/deployment-hierarchy.v1.json`** — the file does not exist, and `craft-risk-ladder` is Layer 0 so it always creates it | ``craft-risk-ladder`` | Two independently written parsers over one file with a **throw-on-missing-section** posture (T5). Agree the section layout before either builds |
