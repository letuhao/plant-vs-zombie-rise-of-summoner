# Empire economy SSOT — loam, the Fracture, and what a map is worth

**Status:** **Consolidated design, 2026-08-23.** This is what holds. It supersedes
[empire-economy-ideal.md](empire-economy-ideal.md), which is retained **only as the reasoning
trail** — that document accumulated four layers of retraction across a day of design and a reader
starting at its §1 absorbs superseded claims before reaching the corrections. Where the two disagree,
**this file wins.** (Capability-map finding A6; same pattern `resource-hub-ssot.md` used with its own
ideal.)

**Tests any of this must pass:** [economy-principles.md](economy-principles.md).
**Build order and audit:** [loam-map.md](loam-map.md). **Module specs:** [loam/](loam/).

---

## 1. Vocabulary — settled, and each one avoids a real collision

| Term | Means | Why not the obvious word |
|---|---|---|
| **stock** | An empire quantity — loam, essence, souls | `resource` is taken by `resource-hub-ssot.md` for the five **actor pools**; different scope entirely |
| **loam** | The stock that keeps ground real | New. Collides with nothing in `src/`, the web app, or `docs/` |
| **the Fracture** | The force that unmakes unanchored ground | `chaos` collides with the `chaos-marked` demon trait (`TraitBattleCatalog.cs:86`), and "fracture" is already the codebase's word (`SectorTypeCatalog.cs:8`) |
| **rootworks** | `StructureKind.LoamSource` — the category of things that make loam | `anchor` is the effect-atom layer's (`AnchorResolver`, `AnchorOrigin`, 20 files) |
| **handicap** | A declared per-faction balance multiplier | `cheat` is `FusionRpg.CheatCore`'s — and a hidden fudge cannot survive replay |
| **world** | One map run. Ending one and starting another **is** the progression loop | No new noun needed — `rpg_worlds.state` already exists |

**Rift shards from the old ideal are cancelled.** `shard.common…legendary` already means rarity shards
for fusion, in the same table a map currency would have used. Loam absorbed that role.

---

## 2. Three stocks, and only three

| Stock | Buys | Scope |
|---|---|---|
| **Loam** | Position — holding ground, and eventually building on it | **World.** Never banks |
| **Souls** | Roster power — summons, contracts, rituals | **Player.** `rpg_soul_ledger` |
| **Essence** ×6 | Fusion, element-matched and deliberately non-substitutable | **Player.** `rpg_demon_materials` |

### The P4 test, run — and the answer is three (2026-08-23)

This was deferred as *"needs build costs, so it cannot be run yet."* That was wrong: the buildings are
designed, so their costs can be drafted, and **P4 only needs five real costs to look at.**

| Building | Cost |
|---|---|
| Well (on a rootbed) | loam + turns |
| Waystation | loam + turns |
| Granary | loam + turns |
| Deep root | loam + turns |
| Soul conduit | loam + turns |

**Not one bottleneck pair anywhere** — every cost is "loam and time", and time is not a stock. So a
fourth material would be a currency wearing a costume, and **P4 says no.**

But the test also shows what is *missing*: with a single build currency there is never a moment where
having more of one thing cannot rescue you from lacking another, which is the tension P4 exists to
find. The fix is not a new stock — it is a **compound cost on the stocks we already have**:

> **Some buildings cost essence alongside loam.** A soul conduit wants `essence.dark`; an ice-climate
> waystation wants `essence.ice`.

That earns its keep three ways: it creates real bottleneck pairs (`min(loam, essence.dark)` — **P4**);
it gives **essence a second sink**, since fusion was its only one and **P6** requires two that compete;
and it makes an element-typed sector matter for *building*, not only for fusion, which is **P12**
doing more work than it was. Three stocks, no fourth, and the dimensionality comes from compound costs
rather than from more currencies.

It is not a conversion, so **P5** is untouched.

---

## 3. Anchoring — the spine

**Nothing outside your own ground is really there.** What you hold, you hold by force of reality, and
reality has to be supplied.

- A holding with a working **loam source** is anchored. Without one it **fades**, gradually and
  visibly, and is finally **lost**.
- Fading destroys **structures**, never a **natural rootbed** — so rootbed sectors are permanent
  strategic features, and seatland must be founded again from nothing.
- **The fade is its own enforcement.** A claim on barren ground is *allowed*, warned, and fades. That
  keeps corridor-seizing as a real play and closes the reclaim loophole for free.
- **Loam is fungible within a connected component** of your territory. Sources produce locally and
  unconditionally; upkeep is paid from the component's pool; **severing splits the pool.** One rule
  gives automatic flow, makes severing economic warfare, and needs no routing algorithm.
- When a component cannot pay, the **weakest contributor** is released first — worst net balance,
  ordinal tiebreak. The player never distributes loam; they only choose what to give up.

### Three kinds of ground

| Ground | You can |
|---|---|
| **Rootbed** | Settle from anywhere. Rare. The prizes |
| **Seatland** | Settle if you can reach it — a waystation must be founded within range of anchored ground |
| **Barren** | Never keep it. Take it, fight on it, watch it fade |

Two expansion styles fall out: **creep** (waystation by waystation, continuous, safe) and **leap**
(take a rootbed, found an isolated colony). Most 4X games offer one.

### Upkeep is local, and most ground loses money

```
upkeep(sector) = ( base + Σ structures + Σ garrison + f(development, danger) )
                 × FractureIntensityMilli / 1000
                 × UpkeepHandicapMilli / 1000
```

**No distance term** — intensity carries remoteness, and two multipliers make a stalled empire
unfalsifiable.

**The baseline is a deficit.** Profit comes from concentration, not breadth, so expansion is a
loss-making act justified by what it *reaches*. That is what makes upkeep a tax rather than a filter,
and it satisfies **P3** permanently: there is no empire size at which you are comfortable.

**Zomboss runs exactly this economy.** No asymmetry, no second mechanism. You can starve him, and
taking his capital collapses him.

---

## 4. What a world is worth — the progression loop

**This is the frame everything else sits inside**, and the storage seam built for determinism turns
out to be the same line that decides it.

> **You keep who you are. You lose where you were.**

| Carries to the next world | Dies with the world |
|---|---|
| Demons, roster, contracts, codex | Territory and every structure on it |
| Souls, essence, materials — **banked** | Loam, and **any haul not banked** |

- **Success = take Zomboss's capital.** Not conquest: a deficit baseline gives every empire a natural
  equilibrium size, so no map is ever fully holdable. A target, not a checklist.
- **Failure = lose your own capital.** The map ends. You keep your roster and your banked treasury,
  and lose everything you were still carrying.
- Either way the world's `state` leaves `'active'` and the next one begins at a **higher size tier and
  a higher base Fracture intensity** — the two difficulty axes that already exist as data.

### World sizes

Ids are plain; display names are content (`resource-hub-ssot.md` §3). That also dodges two collisions:
`reach` is 31 source files (`ReachMap`, `SupplyReach`) and `hollow` is already a sector id.

| Id | Display | Nodes | Availability |
|---|---|---|---|
| `small` | Pocket | ~8 | `first-light` |
| `medium` | Fragment | ~14–18 | **`two-hearths`, the gate map** |
| `large` | Expanse | ~32 | Gated on `world-generator` — not hand-authorable at ~33 lines per sector |
| `huge` | Abyss | ~64 | Same, **and** blocked until `ReconnectionCost`'s `O(V⁴)` is measured rather than asserted |
| `giant` | Maelstrom | ~128 | Same, **and** needs the Tarjan-first optimisation `spec-world-topology.md:52` already describes |

**Three things this gives us for free:**

1. **`rpg_worlds.state` already defaults to `'active'` and `GetWorldHeader` already filters on it.**
   The loop is modelled and unused; `spec-world-model` even wrote *"ended worlds are retained for
   history."*
2. **It makes banking a real decision.** Unbanked haul is lost at map end, which is ideal §13's *"what
   you can lose"* made structural, and it is why shipping must be a choice rather than a trickle.
3. **It dissolves most of the 500-hour problem** (§7).

---

## 5. The reward layer — what territory actually pays

Held ground yields **souls, essence and materials** through structures, into the **player** treasury.
This closes the reward hole that would otherwise make the map a cost with no payoff.

**Banking is conditional on connection, not on a convoy.** Haul banks automatically from any sector in
the same component as your capital; a severed sector accumulates locally and is at risk. That reuses
`TerritoryComponents` a third time and needs no new entity kind. Caravans stay a later option, not a
prerequisite.

### The soul conduit — the original request, answered

A **soul conduit** is a plain building that yields souls. No converter, no daily cap, no separate
currency.

It needs no artificial throttle because **loam is the throttle.** A conduit occupies a slot that could
have been something else, and it pays loam upkeep like everything else — so souls-per-world is bounded
by how much habitable, affordable ground you can dedicate to it. That is **P6** (competing sinks) and
**P2** (a territorial faucet paid for by a territorial cost), satisfied by the mechanism rather than
by a rule bolted on top.

**`spec-soul-economy.md`'s "never earn from anything but recorded Activity facts" survives intact**:
a world turn is a durable, uniquely-identified record — one row per `(world_id, turn)` — which is
exactly the dedupe key the ledger demands. Replay re-derives the same key and earns nothing new.

> **The unifying statement: loam is the throttle on every faucet the map has.** That is what the whole
> anchoring design buys, and it is why the economy needed designing before the buildings.

---

## 6. Storage and logistics — never trade, only movement

| | Answers | Effect on the constraint |
|---|---|---|
| A market | *"I need loam, I'll buy some"* | **Destroys it** |
| Logistics | *"I have loam there and need it here"* | **Preserves it** |

**Loam is never converted, only moved.** Every answer to a shortage is route, timing, capacity, risk.

- **Sector storage** (granary) turns a flow problem into a buffer problem — without it, income and
  spend must match every turn and no plan is longer than one. Overflow is waste; a stockpile is a
  target.
- **Legion storage** comes from **bearers** — a role on `WorldEntityMember`. Every slot spent carrying
  is a slot not spent fighting, which produces a deep-expedition and a strike-force archetype with
  nothing authored, and gives duplicate commons a job.
  - Capacity scaling with *every* member is degenerate: if capacity and burn both scale with headcount,
    range = `capacity/burn` is constant and the logistics layer evaporates.
- **A legion may spend carried loam to hold the ground it stands on** — which is how the first
  rootworks in a new sector ever gets built, and it makes planting a colony the tensest moment in the
  game.
- **Marching past your loam is warned, never refused** for the player (a suicide march to sever a chain
  is a real play) and **hard-gated for the AI**.

---

## 7. The 500-hour test — and why bounded worlds dissolve most of it

**Any permanent solution to a recurring cost is eventually free.** In an endless game that is fatal —
which is why every mechanic gets asked what it looks like after 500 hours.

**Bounded worlds answer it for almost everything.** Wardens, deep root, scorched root and every
structure are **world-scoped**: they die with the map. "Permanent" means "for this world", and the
next world starts bare. The test only bites on what *persists*, which is Tier 2 — and §5 shows loam
throttles the faucets that fill it.

| Mechanic | Verdict |
|---|---|
| **Deep root**, **scorched root**, granaries, waystations | **Safe.** World-scoped; lost at map end |
| **Wardens** | **Needs its cure, and it has one:** binding a warden permanently consumes a `demon-contracts` **binding slot** — already Soul-priced, already scarce, already shipped. Each warden permanently shrinks your deployable roster, so the Nth is genuinely dearer than the first |
| **The Unmade** | **Closed — see §7a.** They *are* a farm, deliberately. Throttled by loam (farming burns it), by depletion, and by their own spread |
| **Soul conduits** | **Safe.** Throttled by loam (§5) |

A warden is therefore a permanent Tier-2 sacrifice for a Tier-1 gain — a late-map desperation move,
which is exactly where it belongs.

---

## 7a. The Unmade — your failed frontier becomes your grinding ground

**Owner decision, 2026-08-23: they are content, and farming them is a strategy.** My recommendation
was the opposite and it was wrong-genre — I argued from 4X instincts, where paying a player to give up
ground opposes a mechanic built on holding it. **This is an endless-grind RPG, and renewable content
is the point.** Anchoring is the strategy layer; the Unmade are the RPG layer. They coexist as long as
the economics do not invert.

**The rule that keeps them from inverting:**

> **Farming costs loam, exactly like holding does.** A legion parked in barren ground to cull Unmade is
> out of supply and burning what it carries, every turn. So farming is not free income — it is another
> way to spend the same scarce thing, and choosing between farming and holding is the same allocation
> decision the whole game is about.

That is the unifying statement doing its work again: **loam is the throttle on every faucet the map
has**, and farming is now one of them.

### The design

| Rule | Why |
|---|---|
| Faded ground spawns Unmade **at a rate, indefinitely** | Renewable, because the genre wants a farm. Bounded by time, not by a total, which is how grind economies are throttled |
| **They never drop loam** | The single most important rule here. A farm that funds its own upkeep is self-sustaining, and loam stops being the throttle. They pay in Tier-2 goods — the things that carry between worlds, which is what grinding is *for* |
| **They spread if not culled** | Neglect still compounds: an unfarmed faded sector raises intensity in its neighbours and eventually pushes into held ground. The farm fights back, so farming is maintenance rather than free income |
| **Spawn rate depletes locally and recovers slowly** | **P9.** Stops one legion parking forever on the single best spot; `DepletionMilli` is the field for it, already shipped and still unread |
| **Deeper ground spawns stronger Unmade with better drops** | The risk/reward gradient follows the chaos gradient, so `FractureIntensityMilli` gets a second job — and **barren deep ground becomes worth visiting even though it can never be held**, which is the best answer yet to "what is unheld territory for" |

### What this settles

- **A1's last open cure is found.** The 500-hour test asked what stops abandonment becoming optimal;
  the answer is that abandonment is not free, because farming costs loam and the farm depletes.
- **It partially fills the reward hole (G-F)** — post-gate, once `combat-handoff` decides what a world
  battle pays. Territory you *cannot* hold finally has a use.
- **A constraint lands on `combat-handoff`:** world-battle rewards must be Tier-2 only. If a world
  battle ever pays loam, this entire throttle collapses in one line of a different module's spec.

---

## 8. Sub-mechanisms — kept, and rejected

**Kept:** the Unmade (§7a — content, farmable, loam-throttled; reuses `WorldFactionKind.Wild`, no new AI) ·
fade contagion (reuses `PressureMilli`) · wardens · prospecting (hidden rootbeds — and it fixes the
observed defect where `Explore` fired three times and then never again) · deep tap · scorched root ·
reavers · Fracture surges (`TurnCalendar` already rolls `Plague`, pure in `(turn, seed)`, and its
effects have never landed).

**Rejected, with cause:** a loam market (**P5**) · loam as a battle resource (scope collision with the
actor hub) · loam grades or tiers (**P7**, and no `min(x,y)` bottleneck so also **P4**) · the Fracture
as a commanding faction (a third brain to produce what a spread pass gives free — it is a *field*) ·
per-demon loam upkeep (contracts already charge a daily soul tribute) · randomised yields (determinism
survives it; *planning* does not — variance belongs in announced surges).

**Territory is light in the dark.** `StabilityMilli` is a shipped 0–1000 per sector that already
hashes and replays; render it directly and the map's whole mood is one field. **Fading and barren must
never look alike** — one is a problem you can solve, the other was never yours to keep.

---

## 9. Still open — one item, with a method attached

**Every number.** Deliberately, and it is not a question: `loam-calc` builds the harness precisely so
they can be *measured* against a real map rather than argued over. Choosing them here would be guessing
with extra steps, and §13 of the principles already says which measurements decide them.

Everything else that was open is closed and recorded where it belongs — the P4 test in §2, the
progression loop in §4, the soul conduit in §5, the 500-hour cures in §7, the Unmade in §7a, and
G-F's framing as the playtest brief in `spec-loam-maps.md` rather than as something to remember.

### Constraints this design lands on modules outside it

Recorded here because a constraint nobody wrote down is a constraint that gets broken by someone who
never read this file.

| Module | Constraint |
|---|---|
| `combat-handoff` | **World-battle rewards are Tier-2 only.** If a world battle ever pays loam, §7a's farming throttle collapses in one line of another program's spec |
| `world-generator` | Zomboss's capital must be **reachable and takeable at equilibrium empire size** — very different from "the map must be holdable" (§4). And a map must mix habitable and barren ground, or the settlement rule has no teeth |
| `sector-development` | Development must raise yield **faster** than it raises upkeep, or nobody will ever develop (**A8**) |
| `demon-contracts` | A warden permanently consumes a binding slot (§7). That is the cure that keeps wardens from becoming free |
