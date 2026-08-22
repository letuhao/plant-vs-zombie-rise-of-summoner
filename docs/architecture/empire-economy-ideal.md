# The ideal — empire economy: what a territory produces and where it lands

**Status:** **Ideal capture (2026-08-22)** — a vision document, not a spec. No module ids, no build
order, no acceptance criteria, nothing committed. Written *before* buildings and map objects on
purpose (owner): a building is a thing that makes a resource, so the resource has to mean something
first, or every building is a guess.

> ⚠️ **Read [economy-principles.md](economy-principles.md) first.** It was written after this
> document, at the owner's direction, and it is the layer underneath: the tests a currency must pass
> before it may exist. Where this file *picks* (§4's four-stock set, §6's option menus), the
> principles file *decides* — see its §12. In particular this file's §4 argues for cutting materials
> on a "buys a different axis" intuition; principle **P4** replaces that with an actual test
> (is any cost a `min(x, y)` bottleneck?), and the test has not been run yet.

**Read with:** [economy-principles.md](economy-principles.md) (**the foundation — read first**) ·
[world-graph-ideal.md](world-graph-ideal.md) §7 and §13 (what the map wants) ·
[world-map-program.md](world-map-program.md) (what the map already is) ·
[demons/spec-soul-economy.md](demons/spec-soul-economy.md) (the one ledger that already works) ·
[resource-hub-ssot.md](resource-hub-ssot.md) (**a different thing with the same name — see §2**).

---

## 1. What already exists, and it is more than it looks

The empire has a wallet today. It was built for the demon game and nobody has called it an economy,
but it is one, and the map should bank into it rather than beside it.

| What | Where | Shape |
|---|---|---|
| **Souls** | `rpg_soul_ledger` + `rpg_soul_balances` | Append-only ledger, watermarked balance projection, dedupe key per row, atomic `TrySpendSouls`, cold-archive + trim. This is the good one — copy its pattern, do not invent a second one |
| **Essences** | `rpg_demon_materials(player_id, material_id, qty)` — `RpgStore.cs:520` | `essence.fire` … `essence.dark`, six concrete elements from `ElementRoster.Concrete` (`ActorElementTypes.cs:21`) |
| **Rarity shards** | same table | `shard.common` · `shard.rare` · `shard.epic` · `shard.legendary` (`DemonMaterialCatalog.cs`) |

Both material families are built by `DemonMaterialCatalog.Build()` and validated on every write
(`RpgStore.Expeditions.cs:209`, `RpgStore.Fusion.cs:391`). **The vocabulary is already closed and
already enforced** — which is exactly the property we want, and also the reason a new resource
cannot simply be typed into a string somewhere.

What does **not** exist: any generic construction material, any position currency, and any notion of
a resource that lives somewhere other than the player's wallet.

---

## 2. Two collisions to settle before anything is built

This design set has been burned twice by two things sharing a word — the lawn's **Sun** versus a
plant's `hunger` pool needed a whole section of [resource-hub-ssot.md](resource-hub-ssot.md) §4 to
untangle. Here are the next two, named now while they are free.

### 2.1 "Resource" is taken

`resource-hub-ssot.md` owns the word for **five actor pools** — `hp`, `stamina`, `hunger`, `spirit`,
`qi` — one set per creature, spent on actions inside a battle. That hub is design-locked and its
channel family is `resource.*`.

An empire resource is a different scope entirely: a **stockpile**, owned by a player or a sector,
spent on buildings and summons across turns. It must never be `resource.*`, never share an id space,
and never appear in the same list on a UI without a scope label.

> **Proposed word: `stock`.** An empire holds *stock*; an actor holds a *resource*. One word each,
> and the two can sit on the same screen without a reader having to ask which is which.

### 2.2 "Shard" is taken, and the map wants it back

[world-graph-ideal.md](world-graph-ideal.md) §7.2 asks for a **shard tap** producing **rift shards**,
"the strategic currency". But `shard.common` … `shard.legendary` already exist and mean **rarity
shards for fusion**. Two unrelated currencies, one word, one id prefix, one inventory table.

This is the Sun problem again, and it is worse: these two would sit in the *same table* under the
*same prefix*. Nothing about the code would stop `shard.rare` and a rift shard being added together.

> **The map's currency needs a different name.** Checked against `src/` and the web app —
> `flux`, `riftstone`, and `mote` collide with nothing. `anchor` (16 files) and `tribute` (6) do.
> My pick is **flux**: short, it reads as something the fracture produces, and "a sector yields
> flux" needs no explanation.

---

## 3. The shape: two tiers, one seam

The fork that matters is not *what* the resources are. It is **where they sit**, and the world
map's first architectural lock decides it:

> A save is `(seed, template, command log)` and replay must be byte-identical.
> — [world-map-program.md](world-map-program.md), locked shape #4

If the `Production` phase writes into `rpg_soul_ledger`, then `step` has an effect outside its own
state, and replaying the command log no longer reproduces the game. Every determinism guarantee
built across W1–W41 stops holding. So production cannot pay the wallet directly.

But if production *only* ever fills a world-local pot, the map never feeds fusion or the roster —
and feeding them was the whole reason the ideal wanted an essence extractor (§7.2).

**Both halves are satisfied by making the trip home a thing that happens in the game:**

```
  ┌─ Tier 1 ── STOCK ───────────────┐        ┌─ Tier 2 ── TREASURY ─────────┐
  │ lives in WorldState             │        │ rpg_soul_ledger              │
  │ per sector, unbanked            │  ship  │ rpg_demon_materials          │
  │ hashed by StateHasher           │ ─────▶ │ player-scope, ledgered       │
  │ replayed from the command log   │  home  │ spends on summon / fusion    │
  │ can be raided, cut off, lost    │        │ safe                         │
  └─────────────────────────────────┘        └──────────────────────────────┘
        produced in the Production phase           an outcome of a command,
        by buildings on slots                      not a side effect of a phase
```

Three things fall out of this, and all three are good:

1. **Determinism survives.** `step` reads and writes only `WorldState`. The banking happens because a
   command said so, and the command is in the log.
2. **The ideal already asked for it.** §13: *"unbanked haul is what you can lose."* This is that
   sentence, made structural. A supply line that gets cut is now a supply line with something on it.
3. **The AI gets a real want.** `INeedVector` ships as `UniformNeeds`, documented as the stub until
   stockpiles exist. Tier 1 is that stockpile. Zomboss can then want *ice essence specifically*
   rather than wanting everything equally.

**The open question inside this is what "ship home" costs** — a free trickle from any connected
sector, a building (supply hub) that enables it, or a caravan entity that can be intercepted. That is
§6.1.

---

## 4. The stock set — and an argument for four, not five

> **Superseded by §7.9.** This section picked currencies and then looked for jobs for them. §7
> starts from a mechanism instead, and the list that falls out of it is shorter. Kept as the
> reasoning trail, not as a decision.

[world-graph-ideal.md](world-graph-ideal.md) §13 gives each currency a distinct job, which is the
test a currency has to pass: *if two currencies buy the same axis, they are one currency with extra
UI.*

| Stock | Buys | Faucet | Status |
|---|---|---|---|
| **Souls** | **Roster power** — summons, rarity, later rituals | soul conduit on a slot | ledger exists ✅ |
| **Essence** (×6 elements) | **Fusion**, element-matched | essence extractor on a deposit | ids exist ✅ |
| **Flux** | **Position** — projects, development level, capacity, stabilization | flux tap on a rift vein | new |
| ~~Materials~~ | construction stock | quarry on a material seam | **proposed cut — see below** |

**The cut.** The ideal lists both *materials* ("construction and fusion stock") and *shards* ("buy
position"). Both are spent on building things. The distinction — stock versus capacity — is real but
thin, and every currency added costs a UI row, an `INeedVector` axis, a `ValueMap` weight, an AI
want, and a balance surface. The ideal's own budget (§12.6) is *"roughly seventy authored things"*,
spent deliberately.

**Recommendation: fold materials into flux.** A material seam then yields flux at a higher rate but
with no strategic ceiling, and a rift vein yields less but is contested. Same slot variety, one fewer
currency. If construction later needs a stock/capacity split to be interesting, it can be added when
we have a build queue to feel it against — not before.

**Recruits are not a stock.** A lair produces *bodies*, and a body becomes a `WorldEntityMember`
inside a legion. Counting them as a currency would make them fungible, and the whole point of a
demon is that it is not.

---

## 5. The soul mine — the owner's example, and why it is the sharpest question here

A **soul conduit** building is exactly right as a design: the summon feature needs a faucet the
player can *build* rather than only *earn*, and a mine is the readable version of that.

It is also the one thing in this document that contradicts something already written down.
[demons/spec-soul-economy.md](demons/spec-soul-economy.md), Boundaries:

> **Never:** … earning from anything but recorded Activity facts …

That rule exists for a reason worth keeping: the dedupe key on every soul row *is* the activity fact
id, which is what makes re-ingest and replay unable to double-earn. A faucet with no fact behind it
has no natural dedupe key, and a world that replays would mint souls twice.

**It is solvable without weakening the rule.** A world turn already produces a durable, uniquely
identified record — the turn log row, one per `(world_id, turn)`. Banking a turn's soul yield as a
fact keyed on `(world_id, turn)` gives the ledger exactly the dedupe key it demands, and replaying
the turn re-derives the same key and earns nothing new. The rule becomes *"earning from recorded
facts"* — the world turn simply becomes another kind of fact alongside the match.

**What is not solvable by architecture is the balance question.** The soul spec targets
**~5–8 pulls per hour of active play**, and reaching that number cost an economy review: the original
+2/kill uncapped yielded 20–25/hour and *"consumed the collection arc in a weekend"*. A buildable
soul faucet is a second tap into that same bathtub, and it scales with territory rather than with
play. Held territory would set the summon rate.

That is a **game-design decision, not an implementation detail**, and the spec already lists
"changing earn values" and "new spend sinks" under **Ask first**. Options are in §6.3.

---

## 6. Open decisions

> **§7 answers 6.2 and narrows 6.1 and 6.4.** Read it before treating these as live.

### 6.1 What does shipping stock home cost?
- **Free trickle** — any sector connected to the homeworld banks a share each turn. Simple; supply
  connectivity already exists and already computes exactly this set (`SupplyGraph.ConnectedSectors`).
  Losing the chain already costs attrition; it would now also cost income.
- **A building** — a supply hub project on the sector enables banking. Makes it a choice, costs a
  turn and a slot.
- **A caravan** — an entity that carries stock down a lane and can be intercepted. The most
  interesting and the most work; it needs a new entity kind and an interception ruling.

### 6.2 Do we cut materials, or keep five stocks? (§4)

### 6.3 How does a soul mine sit against the summon economy? (§5)
- **A faucet with a ceiling** — conduits pay, but total map-earned souls per day is capped, the way
  match victories decay after three. Territory accelerates, it does not replace.
- **A converter, not a faucet** — a conduit does not mint souls; it converts flux into souls at a
  rate. Territory then buys *conversion*, and the total is bounded by what the map produces.
- **A separate currency** — the map pays a "warsoul" that only buys map things (garrisons, legion
  slots) and never touches summoning. Cleanest separation, but the owner's stated wish was a soul
  generator for the *summon* feature, so this answers a different question.
- **Uncapped** — territory sets the summon rate outright. Honest and simple; explicitly re-opens the
  balance the 2026-08-21 economy review closed.

### 6.4 Is stock per-sector or per-empire-per-world?
Per-sector makes supply cuts bite and gives raiding a target. Per-world is one number and much less
UI. Per-sector is the one the ideal's fiction wants; it is also more state to hash.

---

## 7. The spine: anchoring, and the resource that pays for it

**Owner, 2026-08-22:** *"a resource only has value when it has a consumer, and this is gameplay
mechanism design — so this means we make the whole game mechanism follow the resources."*

That reverses the order of §4 and §6, correctly. Those sections picked currencies and then went
looking for jobs. This section starts from a **mechanism** and lets the resource fall out of it —
which is also the stronger reading of principle **P1**: the sink is not a line item to be named, it
is the thing being designed.

### 7.1 The mechanism

> **Nothing outside the homeworld is really there.** The fracture swallowed those timelines; a legion
> standing in Ancient Egypt is standing on a memory of it. What you hold, you hold by force of
> *reality*, and reality has to be supplied. When the supply stops, the ground stops being ground.

- A holding that is supplied is **anchored**.
- A holding that is not begins to **fade** — visibly, over several turns, with a countdown.
- A holding that fades completely is **lost**: ownership clears, structures ruin, slots revert toward
  hazard. It becomes the *collapsed* sector the world-graph ideal §6 already describes — *"a sector
  that already fell; ruined slots, cheap to retake, poor until repaired."*
- An **army** carries its own. Beyond the supply chain it burns what it brought, and a legion that
  runs out is **adrift**, then unmade. Its range is what it can carry.

**This is not a new idea in this codebase — it is an unbuilt comment.**
`SectorTypeCatalog.cs:8` describes the homeworld as *"the one sector the fracture has not
swallowed"*, and `WorldState.cs:95` as *"the homeworld, which the fracture never touched."* The
fiction already says the fracture swallows things and home is the exception. Nothing has ever made
that true.

**Name the force "the Fracture", not "chaos".** `chaos-marked` is already a shipped demon trait with
essence-proc mechanics (`TraitBattleCatalog.cs:86`); a world-level force with the same word would
collide in every search and every doc. "Fracture" is already the established vocabulary, in code.

### 7.2 The resource: **Loam**

Soil from Dave's lawn. Real earth from the one timeline that is still real — you carry a bit of the
lawn with you, and where you spread it, the ground remembers how to be ground.

Checked against `src/`, the web app, and `docs/`: **`loam` collides with nothing.** So do `ballast`,
`bedrock`, `mooring`, and `tether`, if a colder word is preferred. `loam` is the one that sounds like
this game: plants need soil, the mechanic is literally *keeping the garden real*, and "the outpost ran
out of lawn" is the right amount of funny for a series about a man defending his house with peas.

### 7.3 Where it comes from — and the free answer this gives us

> **Only the homeworld makes loam.** Nowhere else, at least at first.

> ⚠️ **Amended by §8.1.** This is too strong: a resource produced only where nobody can reach it is
> not contested, and the owner requires loam to be the map's primary objective. The rule becomes
> *loam flows only along an unbroken chain to the homeworld* — sources exist on the map, and every
> one of them goes dark when home is lost, so the countdown below survives intact.

Four things fall out of that single choice, and all four are things the design was otherwise going to
have to solve separately:

1. **It answers the biggest open question in the ideal.** §14 thread 2 asks *"what does losing the
   homeworld actually cost?"* — flagged there as *"the biggest tone decision left in the design"*,
   with a menu of penalties in §10.5. Under anchoring it needs no menu: losing home is not a penalty,
   it is **a countdown on everything you own**. Every sector begins to fade at once. That is more
   frightening than any penalty table and it costs nothing to build.
2. **Expansion becomes self-limiting, legibly.** Your empire is exactly as large as your loam
   production. No arbitrary sector cap, no "you may hold 7 provinces" — just an economy that says no.
3. **Supply topology becomes existential.** `SupplyGraph.ConnectedSectors` currently decides who takes
   5%-per-turn attrition. Under anchoring it decides who is *real*. And `world-topology` — already
   built, `ArticulationPoints` and `ReconnectionCost`, currently informational — becomes the most
   important screen in the game: cutting one sector starts a fade on everything behind it.
4. **Distance gets a price.** Cost per sector scaling with hops from home (`Hops` is already built)
   makes the map's *shape* matter rather than just its contents, and stops "leapfrog to the richest
   ground and ignore the middle."

### 7.4 The two sinks, and the decision they create

| Sink | Horizon | What it buys |
|---|---|---|
| **Hold** — every held sector, every turn, scaled by development and distance | ongoing | breadth |
| **Project** — every legion beyond the supply chain, every turn, from what it carries | burst | reach |

**That is the strategic core, in one line: every sector you hold is a legion you cannot send far.**
Breadth against reach, competing for one pool, on different time horizons — which is exactly what
principle **P6** asks a currency to provide, and it arrives here for free rather than by construction.

It also makes **retreat a real strategy** for the first time. Giving up a sector is not just losing
ground; it is buying range.

### 7.5 The army leash

A legion carries capacity `C`, burns `B` per turn outside supply, and therefore has a leash of `C/B`
turns beyond the chain. Inside supply it tops up free — the chain carries loam outward. Scouts are
light and carry proportionally more, so scouting stays cheap and the frontier stays reachable.

**This replaces attrition rather than adding to it.** `SupplyGraph.AttritionWoundMilli = 50` gives an
out-of-supply force ~20 turns to die, which is too slow to change any decision. A 4–8 turn leash is a
constraint the player actually plans around, and "we ran out and the dark took them" is a better
story than a wound counter.

### 7.6 The fade must be graded, not instant

Instant loss at zero reads as a bug. `WorldSector.StabilityMilli` already exists, is already hashed,
already replays — and is read by nothing. It is exactly the countdown this needs:

```
anchored      → stability recovers toward full
unanchored    → stability drains per turn      ← visible, several turns long
stability = 0 → sector is Lost; structures ruin, slots revert toward hazard
```

The gap between "not anchored" and "gone" is where the player gets to react — reroute supply, abandon
something else, march a relief column. Without it, this is a punishment. With it, it is a decision.

### 7.7 Four ways this fails, and what stops each

Stating these now because each is cheaper to design against than to discover in a playtest.

| Risk | What it looks like | Mitigation |
|---|---|---|
| **Death spiral** | one bad turn → fading → weaker → more fading. Positive feedback into failure is brutal and unfun | **A heartland radius.** Sectors within N hops of home cost little or nothing. You can never lose your core to an economic slip — only the frontier fades. Early game generous, late game tense |
| **It punishes exploring**, which is the fun part | player stops leaving home | In-supply resupply is **free**, so expanding *along* your chain costs nothing. Only reaching *past* it burns. The frontier becomes a real edge rather than a wall |
| **It is a tax, not a decision** | you can always afford it, so it is arithmetic with extra steps | Loam must **bind** (P3). The faucet has to grow slower than the map's temptations. This is the primary tuning target, and §13's "binding frequency" metric measures it directly |
| **Micromanagement** | distributing 400 loam across 12 sectors every turn | Allocation is **automatic** by a stated priority (heartland first, then player-set order). The player only ever chooses **what to abandon** when short — one decision, made when it matters, not a spreadsheet every turn |

### 7.8 Running it against our own principles

Written yesterday's-first, so this is a real test rather than a victory lap.

| | Verdict |
|---|---|
| **P1** faucet names its sink | ✅ they are the same design |
| **P2** territorial income needs territorial upkeep | ✅ **the strongest possible pass** — and thematic rather than bookkeeping |
| **P3** must be able to bind | ✅ by construction; it is *the* binding stock |
| **P4** complements, not substitutes | ✅ expansion is `min(loam, build cost)` — rich in materials, poor in loam, and you cannot expand |
| **P5** convertibility | ⚠️ **rule needed: loam must not be freely purchasable** with souls or materials, or the whole constraint dissolves. An emergency valve at a punitive capped rate is defensible; a market is not |
| **P6** two competing sinks, different horizons | ✅ breadth vs reach — arrives for free |
| **P7** 3–4 headline stocks | ✅ loam + souls + essence = **three**, leaving room for one build material |
| **P8** legible payback | ⚠️ **unresolved, and blocked**: a sector must repay its loam cost within the campaign, and campaign length is still unknown |
| **P9** diminishing returns | ✅ distance-scaled cost is a spatial form of it |
| **P11** land / labour / capital | ✅ opportunity: **a garrison could slow the fade**, making labour an input rather than pure cost |
| **P13** determinism | ✅ integer per-mille, computed in the Production/Pressure phases from state |

**Two real gaps: P5 needs an explicit non-convertibility rule, and P8 is blocked on campaign length**
— which is now the second time that number has blocked a decision.

### 7.9 What this does to §4's currency list

Loam probably **absorbs flux entirely**. Flux was invented in §4 to buy "position — projects,
development level, capacity"; anchoring buys position more sharply and with a story attached. That
leaves:

| Stock | Buys |
|---|---|
| **Loam** | position — holding ground, reaching past the chain, and probably building on it |
| **Souls** | roster power — summons, contracts, rituals |
| **Essence** ×6 | fusion, element-matched and deliberately non-substitutable (P12) |
| *materials?* | **open** — only if a real build cost is a `min(materials, loam)` bottleneck (P4) |

Three stocks with one candidate fourth, decided by a test rather than by taste. §4's four-stock table
and its "fold materials into flux" recommendation are **superseded by this section.**

---

## 8. The objects — what makes loam, what eats it, and what everyone fights over

**Loam approved by the owner, 2026-08-22**, with a requirement that changes §7.3:

> *"this is an essential resource of this whole map gameplay — human or AI will give high priority to
> take control of this resource because it helps the empire defend or expand itself."*

### 8.1 The correction §7.3 needs: the chain, not the hearth

§7.3 said **only the homeworld makes loam**. That gives the beautiful "lose home, everything fades"
answer — and it gives the map **nothing to fight over**. A resource produced entirely at a place
nobody can reach is not contested, and an AI has no reason to prioritise it.

Both halves survive with one rule:

> **Loam flows only along an unbroken chain to the homeworld.** There *are* sources on the map. Cut
> one off from home and it goes dark — it does not stockpile, it does not feed a rebel empire, it
> simply stops.

- Lose home → **every** source goes dark at once → the countdown is intact. §7.3's answer to
  "what does losing the homeworld cost" survives unchanged.
- Map sources exist → there is ground worth taking *for loam specifically*, which is the owner's
  requirement.
- And cutting a lane no longer just fades sectors — it **switches off their production**. The
  existing `SupplyGraph.ConnectedSectors` already computes this exact set.

### 8.2 Producers

| Object | Where | What it does |
|---|---|---|
| **The Hearth** | homeworld only, unique, upgradeable | The base rate. The empire's heartbeat, and the thing whose loss stops everything |
| **Loam well** | on a **rootbed** slot (new slot kind — ground where the old world still shows through) | Loam per turn while chained to home. Rare; scattered so that no cluster is self-sufficient (P12) |
| **Waystation** | built on a **Seat**, in any held sector | **Produces nothing, and is the most important object on the map.** See §8.4 |
| **Deep root** | sector project | Permanently lowers *this* sector's upkeep. The alternative to expanding: get cheaper instead of bigger |

### 8.3 Consumers

| Sink | Shape | Why |
|---|---|---|
| **Holding a sector** | per turn: `base × development × distance` | P2 — territorial income needs territorial upkeep |
| **A legion beyond supply** | per turn, from what it carries | §7.5 — the leash |
| **Building anything** | upfront lump | You are making ground real enough to build on |
| **Raising development level** | lump **and** a permanent upkeep increase | Growth costs twice — the classic cure for unbounded development |
| **A stronghold** | large upfront, then **reduces** that sector's upkeep | It anchors itself. This is what makes a capital worth the investment |

### 8.4 The waystation — the object both sides will fight for

> **Distance is measured to the nearest anchor point, where anchor points are the homeworld *and*
> every waystation you have built.**

Plant one on the frontier and everything beyond it becomes affordable again. That single rule does a
remarkable amount of work:

- It is the **expand** verb, made concrete. An empire grows by planting forward bases, which is how
  empires actually grow.
- It makes chokepoints and articulation points the obvious place to build — so the map's *shape*
  drives the player's decisions, not just its contents.
- It is **the highest-value thing to destroy**, which gives the enemy a target that is not a stack of
  troops.
- **It finally gives the Seat slot a job.** Today a Seat gates a stronghold that does not exist;
  `WorldValidation.Rule5SeatCounts` places them on every base-capable sector type and nothing reads
  them. Anchor points are what Seats have been waiting for.

**And it costs no new graph code.** Anchor distance is a multi-source BFS from
`{homeworld} ∪ {waystations}` — which is exactly `SupplyReach.From(seeds, links, usable)`, extracted
in W30 for precisely this "same rule, different seeds" reason.

### 8.5 Units

| Unit | Role |
|---|---|
| **Legion** (exists) | Carries its own loam. Capacity by size and stance; scouts are light and carry proportionally more, so scouting stays cheap |
| **Rootwain** *(proposed)* | A slow cargo entity carrying bulk loam **outward** to a forward position, and haul **home** on the return. Interceptable |

The rootwain is the most interesting and the most work — a new entity kind and an interception ruling
— but note it collapses §6.1 into the same object: the thing that ships haul home is the thing that
carries loam out. One mechanism, two directions, and a supply line that is genuinely a front.

### 8.6 Modifiers — where the interesting numbers live

- **A garrison slows the fade.** Bodies standing on ground help hold it real. This is principle P11's
  opportunity taken: labour becomes a *production input*, not only a cost, and the same demons are
  simultaneously an upkeep sink and an anchor. It also gives `hold` stance a third job.
- **Sector type scales cost.** Storm and no-base ground costs more — the fracture is stronger there.
  A nexus costs more because it is a big junction.
- **Ley lanes carry loam cheaper.** `LaneGraph.Build` already takes a `bannerElement` for exactly this
  kind of elemental affinity discount.
- **Depletion applies to wells** (P9) — `DepletionMilli` exists, hashed, unread.

### 8.7 Zomboss does not need loam — and that is the best version

He is *of* the fracture. He is already real there. **You** are the invader carrying your world out in
sacks; he is home.

So his interest in loam is **denial**, not acquisition: raze a waystation, cut a chain, and your
empire fades without him winning a single battle. That is a distinct enemy rather than a mirror of
the player, and it makes his warbands frightening without giving them a single extra stat.

**What it asks of the AI, and what is already built:**

`ReconnectionCost.For(sectorIds, lanes, climateOf, include:)` takes *"the empire to ask about"* and
returns, per sector, how much worse every surviving pair of that empire's connections gets if it is
removed. **Point it at the player's holdings and it is already a raid-target score.** The module's own
doc comment calls this out — *"a junction is worth defending even if it produces nothing"* — and the
inverse is a junction worth cutting.

The work is one new rule in `FrontierRulesPolicy`'s ordered chain — **Sever**, sitting high, above
Expand — plus a severance axis on `ValueMap`.

**The honest cost of asymmetry:** the AI's value model stops mirroring the player's, so there are two
economies to balance rather than one. The symmetric alternative (Zomboss anchors from his fortress
exactly as you do) is cheaper to build and to tune, and reuses every existing weight — but he becomes
a reskin of you, and the fiction stops meaning anything. Recommending asymmetric, flagging the bill.

### 8.8 What this needs that does not exist yet

| Needed | Status |
|---|---|
| `WorldSlot.StructureId` | **Missing.** The slot record has type, state, owner and guard — nowhere to put a building |
| A structure catalog (buildings, costs, yields) | Missing entirely |
| `SlotKind.Rootbed` + a `rootbed` slot type | New catalog rows |
| Per-sector loam stock on `WorldSector` | New field — and this decides §6.4 in favour of **per-sector** |
| Anchor-distance map | New, but it is `SupplyReach.From` with different seeds |
| Fade countdown | **`StabilityMilli` already exists**, hashed and replayed, read by nothing |
| Depletion on wells | **`DepletionMilli` already exists**, same |
| Severance axis + `Sever` rule | New: one `ValueMap` axis, one rule in the existing chain |

### 8.9 The numbers that have to be chosen, in dependency order

1. **Campaign length in turns** — still blocking, now for the third time. Every payback period below
   is measured against it (P8).
2. **Hearth base rate**, and what a development level adds to it.
3. **Upkeep per sector per turn**, and the distance multiplier — the two numbers that set how big an
   empire can get.
4. **Heartland radius `N`** — how many hops from home cost nothing (§7.7's death-spiral brake).
5. **Legion capacity and burn** — together, the leash in turns. §7.5 argues for 4–8.
6. **Well yield**, and how many rootbeds a map of ~20 sectors should carry.

Only (1) is a taste decision. (2)–(6) are solvable arithmetic once it is fixed, and §13's
instrumentation measures whether the answers were right.

### 8.10 The settlement rule — and the three kinds of ground it creates

**Owner, 2026-08-22:**

> *"only a sector that contains at least 1 object of the kind 'loam generator' can settle a new base
> here. Without this kind of object we cannot settle the base, because the chaos will consume it and
> fade it per turn."*

This is the rule that makes loam **essential** rather than merely expensive, and it is worth noticing
what it does to the design: **it collapses two concepts I had separate.** §8.4 invented "anchor
points" for measuring distance; §8.2 had "producers" for making loam. Under this rule they are the
same objects. A loam source is what makes a sector *habitable*, *productive*, and *a distance origin*
— one concept, three consequences.

> **`StructureKind.LoamSource`** — a category, not a building. Several structures belong to it, and a
> sector is habitable if and only if it holds a working one.
>
> Player-facing name: **rootworks**. **Not "anchor"** — `AnchorResolver` / `AnchorOrigin` already
> mean something specific in the effect-atom layer (20 hits in `src/`), and a second meaning would
> poison every search.

| Structure | Kind | Built on | Notes |
|---|---|---|---|
| **Hearth** | LoamSource | homeworld, unique | The largest. Cannot be built or rebuilt |
| **Loam well** | LoamSource | a **rootbed** slot | Only where the map placed one. The prize |
| **Waystation** | LoamSource | a **Seat** slot | Buildable — but see the range rule below |

#### Three kinds of ground

This produces a map with real structure, where before it had only "sectors with different contents":

| Ground | Has | You can |
|---|---|---|
| **Rootbed** | a natural rootbed slot | **Settle it from anywhere.** The ground is a piece of the old world and holds itself real. Rare — these are what both sides fight for |
| **Seatland** | a Seat, no rootbed | **Settle it if you can reach it** — a waystation must be founded within range `R` of ground you already hold, because the loam to bootstrap it has to be carried in |
| **Barren** | neither | **Never keep it.** March through, fight on it, even claim it — and watch it fade |

**Two ways to expand, and they play completely differently.** *Creep*: waystation by waystation, one
hop at a time, continuous territory, safe and slow. *Leap*: take a rootbed sector and found a colony
far from home, isolated and exposed and worth it. A 4X that offers only one of those is flatter than
one that offers both, and this gets both from a single rule.

#### Transient ground is a feature, not a gap

"You can take it but never keep it" is one of the better things here. Barren sectors become
**corridors and buffers** — you seize one to cut an enemy chain, hold it while it fades, and let it
go. Ground with a timer on it is more interesting than ground you cannot enter, and it means the map
has places that are permanently nobody's.

**This is a real UI obligation, though.** A claim on barren ground must announce itself as temporary
in the turn report, or the first player to try it will file a bug. `TurnReportEntry` gained a
`SectorId` in W39, so the entry can point at the ground it is talking about.

#### What it does to `first-light`, and a thread from yesterday closes

The rule has teeth only if **most sectors are not settleable**. `first-light` currently gives a Seat
to nearly every sector, because `SectorTypeCatalog` marks `stable`/`rich`/`nexus`/`homeworld`
base-capable and `WorldValidation.Rule5SeatCounts` then requires exactly one Seat on each.

Yesterday I started thinning Seats on that map, reverted it, and said it was *"the type system
working, not a map bug."* That was correct and beside the point: the type system was working toward
nothing, because Seats gated a stronghold that does not exist. Now they gate habitability, and a map
where everything is habitable has no geography.

**So the `world-generator` constraint is now concrete rather than a note:** an interesting map is
mostly barren, with Seatland along the routes and rootbeds as the objectives worth a campaign. Ratios
are tuning; the shape is a requirement.

#### The sub-decision: does a well work when home has fallen?

§8.1 says every source goes dark when its chain to home is cut. Applied strictly to wells, "leap"
colonies cannot exist — an isolated rootbed colony is by definition unchained.

- **Strict** — wells go dark too. Losing home is total, immediate collapse. Cleanest rule, and §7.3's
  answer at full strength. But no isolated colonies, so *leap* stops working.
- **Wells are self-sufficient at reduced output** *(recommended)* — a natural well holds its own
  sector and perhaps one neighbour, but cannot project. Leap colonies work. And losing the homeworld
  becomes: the Hearth is gone, **every waystation goes dark at once** because borrowed reality
  collapses, and you are left clinging to whatever natural wells you hold. That is still a
  catastrophe, still mostly a countdown — but it leaves a last stand and makes *retaking home* the
  objective, which is a better ending than a loss screen.

Recommending the second. It softens §7.3 from "you lose" to "you are down to your last real ground",
which is more frightening to play and more interesting to recover from.

#### What it asks of the AI

`ValueMap` needs a **habitability gate**: a barren sector's *hold* value collapses toward zero while
its *sever* value (§8.7) stays intact — so Zomboss stops trying to keep ground he cannot keep, and
still takes it to cut you. Rootbed sectors should carry a large standing bonus for both sides, which
is what makes them read as objectives rather than as tiles.

---

## 9. Sub-mechanisms — what else grows out of loam and the Fracture

Everything here is **derived**, not bolted on: each item is a consequence of anchoring that the design
already implies, and each names what it reuses. Ordered by how much the game needs it, with a
rejected list at the end — a design budget is spent by saying no, and
[world-graph-ideal.md](world-graph-ideal.md) §12.6 sets that budget at *"roughly seventy authored
things."*

### 9.1 Core — the four that finish the mechanism

Without these, anchoring is an upkeep tax with good fiction. With them it is a game.

#### The Unmade — what a faded sector leaves behind

Right now a sector that finishes fading simply stops being yours. That is a *subtraction*, and
subtractions are forgettable. Instead: **fully faded ground births something.** The Fracture does not
leave a hole, it fills one.

- **Why it is derived:** the fiction already says the Fracture *consumes*. Something that eats leaves
  something behind. And barren space currently has no population, so the map's empty half is inert.
- **What it reuses:** `WorldFactionKind.Wild` — *"unaligned wildlife and slot guards"* — already runs
  `StandFastPolicy`. The Unmade are wild entities. **No new AI, no new faction, no new policy.**
- **The decision it creates:** letting something fade now costs more than the ground. Neglect
  compounds, and a map you have half-abandoned becomes actively dangerous rather than merely empty.
- **Second-order:** barren corridors accumulate Unmade over a campaign, so the map develops a natural
  difficulty gradient *where nobody lives*, without a single authored spawn table.

#### Fade contagion — why the first loss is the one that matters

A sector adjacent to faded ground fades faster.

- **Why it is derived:** if reality is a fabric held by loam, a hole in it should spread. Anything
  else makes the fade a per-sector timer that happens to run in parallel.
- **What it reuses:** `PressureMilli`, already on `WorldSector`, hashed, read by nothing. Contagion is
  a spread pass over lanes — `SupplyReach`-shaped, and §12.5 of the world ideal already budgets *"one
  spread pass over lanes per turn."*
- **The decision it creates:** an urgent, legible one — **stop the first fade or pay compound
  interest.** It converts a slow bleed into a front.
- **Second-order, and this is the good one:** losing an articulation point already fades everything
  behind it. With contagion it fades *fast*. `ReconnectionCost` stops being an interesting number on a
  screen and becomes the thing you feel.

#### Wardens — spend a creature to hold a place

**Bind a demon permanently to a sector. It becomes part of the ground. The sector stops fading.**
You never get that demon back.

- **Why it is derived:** loam is *"the ground remembers how to be ground"*. A demon that stays long
  enough becomes something the ground remembers. It is the same mechanic as a well, paid for with a
  life instead of geology.
- **What it reuses:** `demon-contracts` shipped 2026-08-21 with **binding slots and loyalty** — the
  binding machinery exists. This is a new binding *target*, not a new system.
- **The decision it creates:** the best one in the design. A roster sink that is **not** summoning,
  priced in something you cannot buy back, and aimed at a *territory* problem. Giving up a specific
  creature you have fused and levelled to hold one far sector is a decision with a face on it.
- **Second-order:** wardens are how you hold ground that no waystation can reach and no rootbed
  blesses. They are the *third* expansion mode after creep and leap — expensive, permanent, personal.

#### Prospecting — and the exploration defect it fixes

**Rootbeds are hidden.** A sector's slots are known only when scouted, so *where the prizes are* is
the map's central unknown. A **dowser** — a light unit or a legion stance — reveals rootbeds
specifically, at range.

- **Why it is derived:** §8.10 makes rootbeds the objectives worth a campaign. Objectives you can see
  from turn one are a checklist; objectives you must find are a game.
- **It fixes a defect I actually observed.** In the 20-turn playtest, `Explore` fired about three
  times and then never again — curiosity read zero once the map was known, exactly as W37's warning
  predicted. Hidden rootbeds give curiosity a **permanent** job: the map being *charted* stops meaning
  the map being *understood*.
- **What it reuses:** `world-intel`'s belief model and `IntelState`, plus `ValueMap`'s curiosity axis,
  which is built and currently starves.
- **Second-order:** Zomboss wants to find them too — not to settle, but to **deny** (§8.7). Both sides
  prospecting the same dark map, for opposite reasons, is a better mid-game than both sides expanding
  into known space.

### 9.2 High value — the texture pass

#### Deep tap — the greedy option that is not the safe one

Pull extra loam from a well by over-drawing it: more yield now, faster `DepletionMilli` and rising
`PressureMilli`. Principle **P10** in one lever, and both fields already exist.

#### Scorched root — retreat as a decision

**Burn a well for one large payout; the rootbed is gone permanently.** Denies it to the enemy and
funds the withdrawal that saves your army. Irreversible, which is what makes it memorable — and it
gives *retreat* a verb instead of being the absence of one.

#### Reavers — an enemy that reads differently

Zomboss units built to raze rootworks rather than to fight armies: fast, fragile, and pointed at
buildings. Under §8.7 he does not want your ground, he wants your ground to stop being ground, and a
unit that expresses that is worth more than a stat line on a warband.

#### Fracture surges — the calendar finally does something

`TurnCalendar` already rolls `WeekBoundary`, `MonthBoundary`, `SpecialWeek`, `SpecialMonth` and
**`Plague`**, pure in `(turn, seed)` — and its own doc comment says the effects *"land with
sector-development"*. They have never landed. A plague month becomes a **surge**: fade rates rise
world-wide for a month, and every marginal holding is suddenly a decision.

- The roll is already deterministic, already hashed, and already visible ahead of time — the comment
  notes *"a client can honestly show next week before it arrives"*. **A visible incoming surge is a
  planning problem**, which is strictly better than a surprise.

### 9.3 The visual identity this hands us for free

Worth stating because it is rare for a mechanic to answer an art question.

**Territory is light in the dark.** `StabilityMilli` is a 0–1000 number per sector that already
exists, already hashes, already replays. Render it directly: anchored ground is bright, fading ground
dims, barren ground is dark, and the Unmade move in the dark. The whole map's mood is one field.

That gives the world map a **single readable HUD gauge** — total loam, income, and upkeep, the way
Frostpunk's temperature or a city-builder's power grid does. The player should be able to look at one
number and one map and know whether they are overextended, without opening a panel.

### 9.4 Rejected, and why

Saying no is most of a design budget.

| Rejected | Why |
|---|---|
| **A loam market / trading loam for souls** | Violates **P5**. A market converts the binding constraint into money and the entire mechanism evaporates. **Owner-confirmed 2026-08-22, and replaced rather than merely refused — see §10: logistics, not trade.** Loam is never converted, only moved |
| **Loam as a battle resource** | Scope collision with `resource-hub-ssot.md`'s five actor pools (§2.1). Loam is empire-scope. Two scopes, one word, is the mistake that document exists to prevent |
| **Loam grades or tiers (raw / refined / pure)** | Violates **P7**. It is three UI rows and three balance surfaces to express one number, and no cost would be a `min(x, y)` bottleneck (**P4**) |
| **The Fracture as a commanding faction with its own AI** | A third brain triples the AI balance surface to produce what a spread pass over `PressureMilli` produces for free. The Fracture is a **field**, not a commander. The Unmade it leaves behind are `Wild`, and `Wild` already stands fast |
| **Loam upkeep on individual demons** | Upkeep belongs to *holdings*, not bodies — `demon-contracts` already charges a daily soul tribute per bound demon, and a second per-creature upkeep in a different currency is bookkeeping the player cannot hold in their head |
| **Randomised loam yields per turn** | Determinism survives it (seeded), but planning does not. Anchoring is a *planning* mechanic; noise on the input makes the plan a guess. Variance belongs in the calendar's surges, where it is announced in advance |

### 9.5 Where the multiplication happens

The value of a design set is in its interactions, not its list length. The five worth naming:

| Interaction | What it produces |
|---|---|
| **Contagion × articulation points** | `ReconnectionCost` becomes visceral: cut one junction and everything behind it fades *fast*. The topology module stops being informational |
| **Unmade × barren corridors** | The map's empty half grows teeth over a campaign. A difficulty curve from neglect, with no authored content |
| **Wardens × fog** | Isolated colonies deep in the dark, held by a creature you gave up. The most memorable thing on any given map |
| **Prospecting × denial** | Both sides searching the same dark map for the same prizes, for opposite reasons |
| **Scorched root × retreat** | Withdrawal becomes a play with a decision in it, rather than the absence of a plan |

---

## 10. Storage and logistics — the mechanism that replaces the market

**Owner, 2026-08-22:** *"no loam market, accepted — but we will have another mechanism for this. Loam
needs storage: buildings, and units in a legion. A legion must bring it along for exploration."*

### 10.1 Logistics, not trade — and why that keeps P5 intact

This is the right replacement, and the distinction is worth stating as a rule because it is what makes
one acceptable and the other fatal:

| | Answers | Effect on the constraint |
|---|---|---|
| **A market** | *"I need loam — I will buy some"* | **Destroys it.** Loam becomes a price, and any other currency becomes loam. P5 |
| **Logistics** | *"I have loam over there and I need it here"* | **Preserves it.** The total never changes; only its *position* does |

> **Rule.** Loam is never **converted**, only **moved**. Everything the player can do about a loam
> shortage is a question of route, timing, capacity and risk — never of price.

That is a strictly better set of decisions than a market anyway. A market has one variable; a supply
network has geography in it.

### 10.2 Storage turns a flow problem into a buffer problem

Worth naming because it is the actual reason storage matters, and it is easy to build without
realising: **with no storage, income and spend have to match every single turn.** You cannot save for
a push, you cannot ride out a bad month, and every plan is one turn long.

Storage buys the player a **planning horizon**. It also introduces two consequences that are features:

- **Overflow is waste.** Production above capacity is lost, so a full granary is pressure to spend or
  to expand. A cap that cannot be hit is not a cap.
- **A stockpile is a target.** Razing a full granary destroys the *savings*, not just the income —
  which makes §8.7's denial strategy considerably nastier and gives a raid a headline outcome.

### 10.3 Sector storage — the granary

| | Effect |
|---|---|
| **No granary** | The sector holds a thin buffer — a turn or two of its own production. Enough to function, never enough to stage |
| **Granary** | The sector stockpiles. This is what makes a **forward base** possible: accumulate at the frontier, then push deep |

**A forward base is rootworks + granary.** One makes the ground real, the other lets you gather
enough there to reach past it. Two buildings, two decisions, and the pair is the single most
important thing a player builds on a frontier — which is exactly what you want the enemy hunting
(§9.2's reavers).

### 10.4 Legion storage — bearers, and why not the obvious alternatives

Capacity comes from **bearers**: a `Role` on `WorldEntityMember`. A bearer carries and does not
fight.

> **Every slot spent carrying is a slot not spent fighting.**

Two alternatives, and both are worse for reasons worth recording:

- **Flat per-legion capacity** — no decision at all. Capacity becomes a constant and the player never
  thinks about it again.
- **Capacity from every member** — looks natural and is **degenerate**: if both capacity *and* burn
  scale with headcount, then range = `capacity / burn` is **constant regardless of army size**. A
  bigger army would reach exactly as far as a smaller one, and the whole logistics layer would
  evaporate into a rounding error. Bearers break that symmetry deliberately: they add capacity
  without adding teeth, so range and strength trade against each other.

**Two archetypes fall out with nothing authored:**

| Composition | What it is |
|---|---|
| Mostly bearers, lightly armed | A deep expedition — long leash, cannot win a fight, needs to avoid one |
| All teeth, few bearers | A strike force — hits hard, short leash, must succeed quickly or come home |

**And it gives junk demons a job.** Commons and duplicates that are not worth fusing become the
logistics corps. [demon-system-map.md](demon-system-map.md) already names *"duplicate pressure"* as
the live problem that made fusion the next sink; bearers are a second outlet for it, and one that
does not consume the specimen.

### 10.5 The exploration requirement — warn the player, bind the AI

A legion leaving the chain without loam is walking to its death, so the game has to say so. But
**refusing the order is the wrong fix**: a suicide march to sever an enemy chain (§8.7) is a
legitimate play, and `WorldCommandAdmission` refusing it would delete a real strategy to prevent a
mistake.

> **Recommended split: soft for the player, hard for the AI.**
> The order is admitted and the turn report carries a **projected exhaustion turn** — *"this legion
> runs dry on turn 14."* The player may proceed knowingly. `FrontierRulesPolicy` treats insufficient
> carried loam as a hard gate, because an AI that marches its army into the dark by accident reads as
> a bug, not as a character.

This is the same shape as §8.10's transient-ground warning, and it reuses the same vehicle:
`TurnReportEntry` gained a `SectorId` in W39, so the warning can point at the ground it concerns.

### 10.6 Carry it, or ship it

The two ways to get loam forward should both exist, because they trade off against each other
cleanly:

| | Cost | Risk |
|---|---|---|
| **Bearers** (inside the legion) | combat slots — the army is weaker for it | Safe: it travels behind the shields it paid for |
| **Rootwain** (a separate convoy, §8.5) | none to the army | Interceptable — and a convoy is the softest target on the map |

That is a real decision with no dominant answer, which is the test a mechanic has to pass.

### 10.7 The misery to avoid

Distributing loam by hand across a dozen sectors every turn is the version of this that gets the
whole mechanic cut. Same discipline as §7.7:

> **Loam flows automatically along the chain toward demand.** The player's only routine decision is
> what to *abandon* when short (§7.7) and where to *stage* when pushing. Manual routing is an
> override for a deliberate build-up, never the default way to play a turn.

### 10.8 What this needs that does not exist

| Needed | Note |
|---|---|
| `WorldEntityMember.Role` | The record has `InstanceId`, `SpeciesId`, `Level`, `Hp`, `Wounds` — no role. One field |
| Carried loam on `WorldEntity` | New field; hashed like everything else |
| Sector loam stock + capacity on `WorldSector` | Already implied by §8.8's per-sector stock |
| **Granary** structure + the overflow rule | New catalog rows, once `StructureId` exists |
| Projected-exhaustion line in the turn report | Uses W39's `SectorId`-bearing entries |
| A loam gate in `FrontierRulesPolicy` | One condition on the march rules, alongside §8.10's habitability gate |

---

## 11. Gap register — holes found by attacking the design

A deliberate adversarial pass over §7–§10, looking for rules that are missing rather than wrong.
§11.1 are gaps I could close and did. §11.2 are the ones that need the owner.

### 11.1 Closed here

#### G1 · The bootstrap paradox — how does the *first* rootworks in a sector get built?

Building a waystation needs loam *in that sector*, and the sector has no source yet. Worse,
construction takes turns, so the ground is **fading while it is being built**.

> **Resolution: a legion may spend its carried loam to hold the ground it stands on.**

Planting a colony becomes the tensest moment in the game: your army burns its own reserves keeping
the ground alive until the rootworks finishes, and if it takes too long you walk away or die there.
It also makes bearers (§10.4) directly load-bearing for expansion rather than only for range, and it
gives the escort a job beyond fighting.

#### G2 · What happens to a legion standing in a sector that fades?

Fading does not hurt the army — the Fracture eats *ground*, not creatures. When the fade completes,
the sector is simply barren and the legion is unsupplied on it, burning carried loam like anywhere
else. No new rule, and no special case in the damage path.

#### G3 · Can faded ground be retaken?

> **Fading destroys structures. It never destroys a natural rootbed.**

So a **rootbed sector is a permanent strategic feature** — lose it, retake it, rebuild the well. That
is what makes rootbeds worth a campaign rather than worth a turn. **Seatland** loses its waystation
and must be founded again from scratch, which makes the two ground types feel genuinely different in
defeat as well as in victory.

#### G4 · Does the homeworld need anchoring?

No. It is the one timeline the fracture never touched — already the shipped comment on
`SectorTypeFlags.Home`. It can be besieged and lost to an army; it can never fade.

#### G5 · Range `R` for founding a waystation — measured from what?

From an **anchored** sector, not merely a held one. Measuring from held ground would let a player
chain barren sectors outward indefinitely, and the settlement rule would have no teeth.

#### G6 · Can a sector hold several rootworks?

**One per sector.** They are the anchor, not a stackable industry. Allowing several turns one
super-sector into an unassailable engine and rewards turtling, which is the opposite of what the
mechanic is for.

#### G7 · Is fade graded, or does everything happen at zero?

**Graded.** Production falls as `StabilityMilli` falls, well before the sector is lost. A number
sliding toward zero is information; a cliff at zero is a surprise. The player should *feel* a sector
slipping, not read about it having slipped.

#### G8 · Does loam ever bank into the player's treasury?

**No — loam is Tier 1 only.** It is spent where it is, on the map, and never enters
`rpg_demon_materials` or any player-scope wallet. §3's two-tier ship-it-home seam applies to
**essence and souls**; loam never crosses it. That is a simplification worth having explicitly:
one fewer thing on the home screen, and one fewer conversion to police under **P5**.

#### G9 · Does a warden still cost daily soul tribute?

No. Binding a demon as a warden **consumes the specimen and frees its binding slot**, ending its
`demon-contracts` daily tribute. So a warden is a permanent cost to the *roster* and a permanent
**relief** to the *soul economy* — two economies touching in one decision, which is exactly the sort
of thing that makes a choice memorable rather than arithmetic.

#### G10 · Do lanes have loam throughput limits?

**No**, deliberately — **P7**. Distance cost already prices remoteness; a second dimension for
"how much fits down this lane" is a spreadsheet the player cannot hold in their head. Revisit only if
distance alone proves not to bite.

#### G11 · What does storage save *for*?

Storage without a burst sink is a bigger number that never gets spent. **Founding a waystation is
the burst sink** — deliberately expensive, so that expansion means *saving up*, staging at a
frontier granary, and then committing. That is the rhythm the whole mechanism wants.

#### G12 · The AI cannot plan multi-turn logistics — except it can, and for free

This looked like the hardest gap in the set. `FrontierRulesPolicy` is a **one-turn reactive rule
chain**; founding a waystation is a 3–5 turn commitment (stage loam → escort → build). A reactive
chain cannot hold a plan, and holding one means cross-turn memory, which becomes hashed replayed
state — an explicit ask-first boundary.

> **Resolution: the world state already *is* the memory.**

A legion standing forward carrying loam, a granary stocked at a frontier, a half-built waystation —
these are durable, hashed, replayed facts that **encode intent without storing it**. The AI reads its
own footprint and continues. A goal function that is stable across turns is indistinguishable from a
remembered plan, and no new state is created.

**And this incidentally fixes the oscillation I observed in the playtest.** Zomboss alternated
`defend black-gate` / `expand to verdant-shelf` from T8–T13 precisely because nothing he had done
carried forward — every turn was decided fresh from an unchanged board. Once expanding requires
*staging loam first*, turn one of staging leaves evidence on the board, and turn two reads it. **The
logistics layer supplies the hysteresis the AI was missing.** The momentum term I flagged as needing
cross-turn memory turns out not to need it.

One caution to build carefully: the AI must weight the **option value of the asset** (a forward
legion with loam is genuinely worth more) and never the **sunk cost** (what was already spent is
gone). Those look similar and only one of them is rational.

### 11.2 Open — these need the owner

| # | Gap | Why I cannot settle it |
|---|---|---|
| **O1** | **Campaign length in turns** | Pure taste, and it is the denominator of every payback period (**P8**). Blocking Hearth rate, sector upkeep, distance multiplier, heartland radius, legion leash and well yield — six numbers, all downstream of one |
| **O2** | **Conquered enemy ground: inherit or re-anchor?** | Zomboss does not need loam (§8.7), so his sectors have no rootworks to capture. Taking his ground may therefore give you nothing you can *keep*. That is either the best difficulty curve in the design or a wall at the endgame, and it is a pacing judgement, not a derivation |
| **O3** | **Do wells survive the homeworld falling?** | §8.10's sub-decision, still unanswered. Strict = total collapse, cleanest rule. Lenient = a rump survives on natural wells and retaking home becomes the objective |
| **O4** | **Late-game surplus** | Once several rootbeds are held, loam may stop binding (**P3**) and become a tax again. Superlinear upkeep in holdings is the standard cure and I would recommend it — but it changes the whole feel from "smooth growth" to "a wall you approach", which is a taste call |

---

## 12. What this is not

- Not a change to [resource-hub-ssot.md](resource-hub-ssot.md). The five actor pools are untouched;
  this document never uses the word for them.
- Not a new ledger pattern. Souls' append + watermark + dedupe shape is proven and compaction-safe;
  Tier 2 reuses it rather than inventing.
- Not a real-money or shop concept, ever. `spec-soul-economy.md` says never, and that stands.
- Not authorized to build. This is an ideal; the SSOT follows once §6 is decided.
