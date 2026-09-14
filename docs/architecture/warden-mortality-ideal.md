# Warden mortality — the ideal

**Status: WITHDRAWN by the owner 2026-09-13.** Superseded, not deleted — the reasoning trail below is
kept because the investigation (commander ≠ Warden, the correct `WorldCommand` reconciliation seam) is
still correct and reusable if a Warden-shaped mechanic ever returns. **What withdrew it:** the owner
judged the Warden mechanic itself — not just its death handling — to be a design defect: freezing a
sector's `StabilityMilli` outright while bound (`LoamPhases.cs:174-182`) lets a single specimen zero
out the chaos-decay side of the loam economy for free, bypassing the intended
**generator → storage → decay** loop entirely (decay should always draw from storage; storage running
out should let stability fade and **notify the player**, never be preventable by a costless shield).
The owner's replacement direction — raise the **generation rate** instead of freezing **decay**
(relics, special-unit/commander traits, buildings) — is captured in
[loam-relics-and-wonders-ideal.md](loam-relics-and-wonders-ideal.md) (a correction/extension to the
sealed `loam-map.md` program) plus a separate cross-cutting **notification-system** idea the owner
explicitly asked for as its own SSOT, written at [notification-ssot-ideal.md](notification-ssot-ideal.md). **Do not build anything from this doc.**
Any future "a bound specimen occupies a strategic seat and can die" mechanic should re-read this
doc's §The shape (the `WorldCommand`/`WardenResolver` reconciliation pattern) before re-deriving it.

---

**Original status:** idea phase, 2026-09-13. Not a spec. No build authorized.
**Program id:** `warden-mortality`.
**Traces to:** [deployment-hierarchy-ideal.md](deployment-hierarchy-ideal.md)'s owner-locked line
(2026-09-13, §"Open questions"): *"Commanders are mortal — a commander that truly dies follows the
same corpse-cache path as any unique (seat lapses for re-designation by default)."* This doc exists
because that line does not name a mechanism that can be built as written — see §What this is.

## Step 0 — principles, restated

- **The genre and the loop.** Rise of Summoner is an RPG plus empire-building game (`the-game.md`).
  This idea extends **Place 3 — "Farming, hunting, and defending the empire"** (`the-loops.md:83-99`),
  which names *"buildings and wardens"* as **WIP** explicitly — the Warden is not a new pitch, it is
  an already-named, partially-built piece of the shipped loop this doc finishes reasoning about.
- **Every RPG feature lives in the RPG layer; it is never built by changing what PvZ is.** A Warden's
  death, its seat, and its re-designation are `FusionRpg.Core.World` + `FusionRpg.Data` bookkeeping —
  the same `RetireUniqueActorUnlocked` path every other specimen death already uses. Nothing here
  touches a Unity field or asks PvZ to represent "a sector has a defender."
- **The PvZ write surface is irrelevant here.** This is a world-map/roster concern; no lawn write is
  involved at all.
- **Two async systems, deltas not absolutes.** A Warden's death is a Data-layer state transition
  (`Roster/Retired` phase flip) observed and reacted to — never a live reference the world sector
  holds onto.
- **One power ladder.** This doc adds no magnitude and no curve — a Warden's death is a phase
  transition, not a number.
- **The balance surface is data.** If a re-designation cost or cooldown is ever added (open question,
  below), it is a tunable, not a `const`.
- **No hard progression ceilings.** N/A — nothing here caps a magnitude.
- **Gameless-first.** Fully satisfied by construction — the world map, `RetireUniqueActorUnlocked`,
  and sector state are all playable with Fusion closed today.

DESIGN-GATE §1 rows read this session: World (`data-architecture.md` table map, `WorldState.cs`,
`ClaimResolver.cs`, `WardenResolver.cs`, `LoamPhases.cs`), Creature progression / UniqueActor FSM
(`decisions.md` "UniqueActor (dual FSM)" row), and the deployment-hierarchy program's own three
just-shipped module specs (`corpse-cache`, `injury-tiers` — read for their exact `Retired` trigger
contract, since this doc's whole conclusion rests on reusing it verbatim).

## What this is

**The short version: "commander mortality" was never a real mechanism — it was a naming collision,
and the real gap it was pointing at is much smaller than a new mortality system.** This session traced
"commander" to its one, closed, verified meaning in this codebase and found it cannot be what that
locked line meant. The line's *actual* intent — a bound specimen dying and its strategic seat lapsing
for re-designation — already has a real, named, in-progress home: the **Warden**.

- **`CommanderId` is closed at exactly two values: `Dave` and `Zomboss`** (`CommanderId.cs:20-24`),
  and the doc comment states why: *"there are exactly two commanders total (owner decision,
  2026-08-30... for now only have 2 of them for lawn run), not one per player — Dave is the player's
  own commander, Zomboss is the opposing AI's."* Dave **is** the player (`the-game.md:17`: *"You are
  Crazy Dave"*); Zomboss is the fixed antagonist whose fortress capture **is the win condition**
  (`the-game.md:35`). Neither can "truly die" mid-run in a sense that would trigger a corpse-cache
  drop — Dave dying is the game's own top-level loss state (*"the homeworld falls"*, `the-game.md:36`),
  and Zomboss dying is the endgame, not an injury-tier event. `CommanderId` also doubles as the world
  map's `OwnerFactionId` (`ClaimResolver.cs:101` — `OwnerFactionId = command.CommanderId`), confirming
  it names the two **factions**, not individual mortal units.
- **`rpg_player_item_assignment` ("commander pouch")** — cited in this program's own
  `spec-corpse-cache.md`/`spec-item-durability-repair.md` as needing the same death-drop treatment as
  a unique's gear — is keyed by `player_id` (`RpgStore.Items.cs:164-171`) and, for Dave, that key is
  literally `player:{playerId}` (`CommanderId.cs:70`, the *same* string `AptitudeEndpoints.ScopeKey`
  already uses for the player). It is Dave's own personal loadout. It has no "death" to trigger a drop
  from, for the same reason above.
- **The Warden is a real specimen bound to a world seat, and it already dies through the shared path.**
  `WorldSector.WardenBindingId` (`WorldState.cs:216`) is an `instanceId` — `WardenResolver.cs:47` sets
  it from `command.WardenId`/`contract.InstanceId`, i.e. a real row in `rpg_unique_actors`, the exact
  table `injury-tiers`/`corpse-cache` (this program's own modules 2/3, spec'd 2026-09-13) already hook
  for **every** unique's real-death trigger: *"the entire real-death trigger is
  `rpg_unique_actors.phase == Retired`"* (`spec-injury-tiers.md` §Interface). A Warden's specimen dying
  on the lawn, in a delve, or on the world map (once world-map death lands, tracked separately) already
  flips that same phase and already produces a corpse-cache row under this program's own already-spec'd
  modules — **with zero new mortality mechanism**, because a Warden is not a new kind of entity, it is
  an existing `UniqueActor` wearing a strategic hat.
- **What is missing is one reaction, not one mortality system.** Nothing today clears
  `WardenBindingId` when the bound specimen retires. The only place `WardenBindingId` is cleared today
  is sector **capture** (`ClaimResolver.cs:107`, an enemy takes the ground) — a different trigger for a
  different reason (*"capture ends the binding outright, no transfer to the new owner"*, the code's own
  comment). Losing your *own* Warden to death, while keeping the sector, leaves `WardenBindingId`
  pointing at a `Retired` specimen forever — a stale reference, never reconciled.

**Player sentence:** *the creature I posted to hold that sector can die like anyone else — in a
fight, on the lawn, in a delve — and when it does, the post sits empty until I put someone else there.
It never quietly points at a ghost.*

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `CommanderId` closed to `{Dave, Zomboss}`, the two world factions, not mortal units | `src/FusionRpg.Core/Commanders/CommanderId.cs:20-24, :32-37, :68-73` |
| `WorldSector.WardenBindingId : string?` — an `instanceId` | `src/FusionRpg.Core/World/WorldState.cs:216` |
| `WardenResolver` binds a real specimen's `instanceId` to a sector via a `bind-warden` command | `src/FusionRpg.Core/World/Movement/WardenResolver.cs:47`; command shape `src/FusionRpg.Core/World/Turn/WorldCommand.cs:129` |
| `WardenBindingId` cleared on sector **capture** only | `src/FusionRpg.Core/World/Movement/ClaimResolver.cs:107` (`OwnerFactionId = command.CommanderId,` same `with` block, :101) |
| A warded sector is exempted from something in loam upkeep/forecast (fade/neglect — exact effect not re-derived this session, cited for follow-up) | `src/FusionRpg.Core/World/Loam/LoamPhases.cs:181`, `LoamForecast.cs:31` — both `if (...WardenBindingId is not null) continue` / `.Where(... WardenBindingId is null)` |
| The `Retired`-phase trigger this doc reuses verbatim, already spec'd this session | `docs/architecture/deployment-hierarchy/spec-injury-tiers.md` §Interface; `docs/architecture/deployment-hierarchy/spec-corpse-cache.md` §Design 2 |
| `WardenBindingId` canonical-hash inclusion (world goldens already hash this field) | `src/FusionRpg.Core/World/WorldCanonical.cs:34-45` |
| Place 3 names "wardens" as a shipped-loop WIP piece, not a new pitch | `docs/guide/the-loops.md:83-85` |

### Wiring gap

| Gap | The inert line |
|---|---|
| Nothing observes a Warden's bound specimen transitioning to `Retired` | confirmed this session — `grep -rn "WardenBindingId" src/` returns only bind (`WardenResolver.cs`), read (`LoamPhases.cs`, `LoamForecast.cs`), clear-on-capture (`ClaimResolver.cs`), and the canonical hash (`WorldCanonical.cs`); zero results anywhere near `RetireUniqueActorUnlocked`, `UniqueActorPhases.Retired`, or any Data→World reconciliation |
| No re-designation flow exists — binding is one-directional (assign only) | `WardenResolver.cs` is the only writer of a *populated* `WardenBindingId`; nothing offers "replace the current warden" as a distinct command from "bind a warden to an unwarded sector" (unverified whether `WardenResolver` refuses a re-bind over an existing live warden — real gap to confirm at spec time, not assumed either way) |

### Real gap

None. Once the wiring gap above is closed, Warden mortality needs **no new mechanism** — it is a
reconciliation between two already-built systems (`UniqueActor` FSM, `WorldSector` binding), not a
third system.

## Prior art

| Source | What transfers | Failure mode to avoid |
|---|---|---|
| **Total War — lord death/disband** ([Steam discussion, "Replacing dead general"](https://steamcommunity.com/app/779340/discussions/0/1640916564830660715/)) | A dead/disbanded lord's army post goes to the recruit pool; the lord is "wounded" and unusable for a fixed number of turns, then reemployable — the seat is never permanently gone, only on cooldown. | Total War's cooldown is wall-adjacent (turn-counted, not instant) — if this program ever adds a re-designation delay, it must be **turn-counted** (this repo's own R6 no-wall-clock precedent), never a timer. |
| **Stellaris — vacant sector governor** ([Governor wiki](https://stellaris.paradoxwikis.com/index.php?title=Governor&redirect=no), [reassign discussion](https://steamcommunity.com/app/281990/discussions/0/1776010325112816804/)) | A vacant governor seat is an explicit UI state ("empty window") the player fills from a candidate pool — never auto-filled, never silently defaulting to the first available unit. | The failure Stellaris avoids: auto-assignment removes the player's actual choice at exactly the moment (a death) that should feel consequential. This program's "seat lapses for re-designation **by default**" (the locked line's own words) already picks the Stellaris shape over an auto-fill. |
| **Crusader Kings 3 — ruler succession** ([succession guide](https://www.gamewatcher.com/crusader-kings-3-succession-guide), [PCGamesN succession laws](https://www.pcgamesn.com/crusader-kings-3/ck3-succession-laws)) | Death is never a dead end — the realm passes to a designated heir automatically, and the game continues playing *as* the new holder; a new ruler inherits a reputation penalty simply for being new. | **Rejected as a shape for this feature**: CK3 auto-transfers to an heir, which is the opposite of "lapses for re-designation" — the locked line explicitly wants a manual, player-driven refill, not an automatic heir. Cited to show the alternative exists and was implicitly declined by the original wording. |

## The shape

### Reconciliation, not a new system

At the point `RetireUniqueActorUnlocked` (or `injury-tiers`' equivalent second caller) commits for a
specimen whose `instanceId` matches some sector's `WardenBindingId`:

1. Read whether any `WorldSector.WardenBindingId` equals the retiring `instanceId` (a lookup, not a
   join this codebase doesn't already support — `WorldState.Sectors` is a plain list scanned
   elsewhere, e.g. `LoamPhases.cs:181`'s own `.First(...)` pattern).
2. Clear `WardenBindingId` to `null` on that sector — the seat **lapses**, exactly the locked line's
   word. No auto-fill (Stellaris shape, not CK3).
3. The sector's own loam/fade exposure (whatever `WardenBindingId is not null` currently exempts,
   `LoamPhases.cs:181`) reverts to unwarded the very next resolve — a Warden's death has a real,
   already-existing consequence the moment the seat empties, with no new tunable needed for that part.
4. Re-designation is the **existing** `bind-warden` command (`WorldCommand.cs:129`) issued again by
   the player, at whatever cost/eligibility rule it already enforces — this doc does not propose a new
   command, only that the old seat is empty and biddable again.

### Where the reconciliation runs — resolved this session, not a Data-vs-World binary

The first draft of this doc posed this as "Data-side (direct SQL write) vs. World-side (`TurnEngine.Step`
reads UniqueActor state)." **Both are wrong, and neither is ad-hoc-acceptable** — tracing the actual
architecture this repo already enforces rules both out:

- **A direct Data-side SQL write to `WardenBindingId`, from `RetireUniqueActorUnlocked`'s transaction,
  is a canonical-hash violation.** `WardenBindingId` is a **hashed field** in the world's canonical
  state (`WorldCanonical.cs:34-45`, confirmed this session). Every value that field ever takes must be
  reproducible by replaying `TurnEngine.Step(world, commands, seed)` from the stored command history —
  that replay is exactly what `RpgStore.WorldTurns.cs:641-644`'s own replay path does, and it is what
  world goldens verify. A side-channel SQL write from an unrelated Data function (a specimen dying, on
  the *lawn's* clock, nothing to do with a world turn) would never appear in that replay — the stored
  command history and the live row would silently desync the moment anyone replays.
- **`TurnEngine.Step` reading `rpg_unique_actors.phase` directly breaks its purity.** `Step` is a pure
  function of exactly three inputs (`world`, `commands`, `seed`) — confirmed this session (and by
  `cache-decay-void`'s own spec) that it performs zero SQL today. Reaching into `rpg_unique_actors`
  mid-`Step` would make the engine's output depend on live database state at call time, which is the
  exact property "given `(world, commands, seed)`, the next world is determined" exists to rule out.

**The actually-correct seam is the one this codebase already uses for every world-affecting event
that does not originate as a player's own world-turn order: synthesize a `WorldCommand`.** `bind-warden`
itself is a `WorldCommand` (`WorldCommand.cs:51`), gathered into a durable per-turn list
(`ListWorldCommandsUnlocked`, `RpgStore.WorldTurns.cs:725`, *"every order filed for a turn... the
reason a replay reproduces a turn exactly"*) **before** `TurnEngine.Step` ever runs
(`RpgStore.WorldTurns.cs:530-531`), and resolves in the **Snapshot** phase via `WardenResolver.Run(...,
Phases.Snapshot)` (`TurnEngine.cs:399`) — deliberately ordered right after `Build`/`Raise`/`Develop` so
a claim and a bind-warden landing the same turn compose correctly (`TurnEngine.cs:397-398` comment).

So: when `RetireUniqueActorUnlocked` (or `injury-tiers`' equivalent) commits for a specimen holding a
live `WardenBindingId`, the correct action is not to touch `WardenBindingId` at all — it is to **file
a new, system-issued `WorldCommand`** (a sibling kind to `BindWarden`, e.g. `ReleaseWarden`, carrying
the sector id) into that world's pending-turn command list, on the same connection/transaction as the
retire. The command sits in the durable list exactly like a player's own order until the next
`CommitWorldTurn` runs `TurnEngine.Step`, at which point **`WardenResolver` itself** (extended with
one more command kind, not a new resolver) clears the binding in the Snapshot phase — symmetric with
how it was set, replayable from the stored command the same way every other order is, and never a
live cross-store read from inside `Step`. This is not a choice between two seams; it is the one seam
this repo's own architecture already provides for exactly this shape of event, and using it is what
"follow the architecture, not ad hoc" means here.

**One consequence worth naming:** a Warden's seat does not visibly lapse at the instant of death — it
lapses at the next `CommitWorldTurn`, the same one-turn cadence every other system-issued world effect
already lives on (the `LegionSupply`/loam family, `sector-development`'s growth pulses). This is
consistent with the game's own "a turn is a day" virtual-time model (`decisions.md` Battle time model
row; `world-graph-ideal.md:160`), not a compromise — an instant, live-mid-match update to World state
would itself be the odd one out, since nothing else in the World subsystem updates faster than a turn.

### Alternatives rejected

| Option | Why not |
|---|---|
| A new "commander" mortality system (uniques get a second FSM for "commander-held" specimens) | There is nothing for a second FSM to do that `UniqueActor`'s existing `Retired` phase does not already do — this would be exactly the SOLID/DRY dual-compose defect this repo's own hard rule already names for combat compose, applied here to specimen lifecycle instead. |
| Auto-fill the vacant seat with the sector's nearest/strongest eligible specimen | Rejected by the locked line's own wording ("for re-designation," implying a player action) and by the Stellaris-over-CK3 prior-art comparison above. |
| Treat Dave/Zomboss as mortal and give them a corpse-cache path anyway | Contradicts the game's own top-level win/lose design (`the-game.md:33-40`) — Dave "dying" already means game over, not a recoverable injury event; there is nothing left to drop gear *into*. |

## Tunables

None are required for the reconciliation itself (§The shape items 1-3 read existing state and write
one existing field to `null`). **If** a re-designation cost, cooldown, or eligibility rule is added
later (Total War's "wounded N turns" precedent), it belongs beside `bind-warden`'s own existing rule
set in `data/tuning/world.v{n}.json`, never a new `const` — named here as a possible future tunable,
not decided.

## What this deliberately does not decide

- Whether a re-designation cost/cooldown exists at all (Total War's "wounded N turns" vs. Stellaris's
  "always immediately assignable") — a balance question, not architecture.
- Whether the reconciliation runs Data-side or World-side (§The shape, both seams named, neither
  chosen) — an implementation-time call once `corpse-cache`'s exact transaction boundaries are final.
- Whether `WardenResolver` should refuse re-binding over an already-occupied seat today (unverified
  this session — a real gap to confirm, not a design question this doc needs to answer).
- Anything about world-map combat producing warden deaths in the first place — that is the
  `world-actor-combat` tracked dependency `deployment-hierarchy-ideal.md` already named as blocked on
  a different program producing real world-map deaths; this doc's reconciliation is correct and
  buildable the moment that lands, and needs no changes when it does.

## Open questions — owner decisions only

1. **Correction, not a question — stated for the record:** the "commander mortality" line in
   `deployment-hierarchy-ideal.md` should be read as **retracted and replaced** by this doc's Warden
   finding. `corpse-cache`'s and `item-durability-repair`'s own "commander pouch wears/drops
   identically" language should be understood as narrower than originally written: `rpg_player_item_assignment`
   durability/wear still applies (Dave's own gear still wears down in battle — that part was never
   about mortality), but the **death-drop trigger** those two specs flagged as a real gap does not
   apply to Dave at all, and does apply to Wardens through the mechanism already spec'd for every
   other unique. No owner decision is needed to accept this — it is a correction of a naming
   collision, not a product choice. Say so if this reading is wrong.
2. **Reconciliation seam — CLOSED this session, not an owner question.** Neither original option was
   architecturally sound (§The shape's rewritten subsection has the full reasoning: a direct Data-side
   write corrupts the canonical hash's replay guarantee; a World-side read inside `TurnEngine.Step`
   breaks its purity). The correct seam is the one this repo already has for exactly this shape of
   event — a system-issued `WorldCommand` (a `ReleaseWarden` sibling to `BindWarden`), filed at retire
   time, resolved by `WardenResolver` in the existing Snapshot phase on the next turn. No owner
   decision needed to accept this; flag it if the reasoning is wrong.
3. **Re-designation cost/cooldown, if any** — genuinely open, a balance/content question. See the
   answer given in conversation for the feature overview this needs before deciding.
