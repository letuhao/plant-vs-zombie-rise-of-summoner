# Economy principles — the rules any currency in this game must obey

**Status:** **Principles (2026-08-22)** — the foundation layer, written before any decision about how
many resources exist or what they do. Owner's instruction: *"first use economic knowledge domain and
make principles first before we decide how many resources are and how they affect the gameplay."*

This document does not name a single new resource. It states the tests a resource has to pass. §12
then shows which open design questions these principles **already answer**, and which are genuinely
still choices.

**Grounded in what already ships:** [demons/spec-soul-economy.md](demons/spec-soul-economy.md) ·
`SoulEarnPolicy.cs` · [demon-system-map.md](demon-system-map.md) ·
[world-map-program.md](world-map-program.md) (the determinism lock) ·
[resource-hub-ssot.md](resource-hub-ssot.md) (a **different** hub — actor pools, not empire stock).

---

## 0. The economy we already run

Before any theory: this game has a working economy, and it is better-designed than it is documented.
`SoulEarnPolicy.Reasons` is a faucet/sink table nobody called one.

| Faucets (sources) | Sinks (drains) |
|---|---|
| `kill` — +1, **capped at 50 counted kills per match** | `summon` — the primary sink |
| `victory` — +100, **full for 3/day then halved** | `fusion` · `patron` |
| `defeat` — consolation | `contract-slot` · `contract-ritual` |
| `discovery` — priced by rarity (25/75/200/500) | `upkeep` — *"daily contract tribute, one row per settled UTC day"* |
| `milestone` — 500 at 50% codex, 1500 at 90% | |

Two of those faucets carry an explicit throttle and one sink is **recurring and proportional to
holdings**. That is a real economy. Everything below is the reasoning that was already being applied,
made explicit so the map's economy does not have to rediscover it.

**And this repo has already had one inflation incident, documented.** The original `+2` per kill,
uncapped, produced ~20–25 pulls/hour against a ~5–8 target and *"consumed the collection arc in a
weekend."* Every principle in §1–§3 is a way of not paying for that lesson twice.

---

## A. Balance — the arithmetic that decides whether an economy survives contact

### P1. An economy is faucets, sinks, and the gap between them

Money supply obeys an identity: `Δstock = Σfaucets − Σsinks`. A persistent positive gap is
**inflation** — prices stop meaning anything and the currency stops being a decision. A persistent
negative gap is **starvation** — the player cannot act and stops playing.

> **Rule P1.** Every faucet added must name the sink that absorbs it, in the same change.
> A faucet without a named sink is an inflation commit.

**Test:** for each stock, write both columns as in §0. If you cannot fill the sink column, do not
build the faucet.

### P2. A faucet that scales with holdings needs a sink that scales with holdings

This is the principle a territorial economy lives or dies on. Map income is `O(sectors held)`. If
costs are `O(1)` — a summon always costs the same — then income outruns expenditure by construction,
and the late game is trivially rich no matter how the numbers are tuned. Tuning cannot fix a
mismatch in *growth rate*; only another growth term can.

The standard cure, across every 4X that works, is **upkeep**: a recurring cost proportional to what
you hold. It converts a stock problem into a flow problem and caps growth without a hard cap.

> **Rule P2.** Territorial income requires territorial upkeep. Garrisons, buildings, and development
> levels must cost something *every turn*, scaled to how much you hold.

**This repo already does it once:** contract `upkeep` is a daily tribute proportional to bound
demons. The map should copy that shape, not invent one.

**Corollary — the honest version of "difficulty".** Upkeep is also what makes over-expansion punish
itself, which is the same job `ValueMap`'s overextension penalty does for the AI. The AI and the
economy should punish the same mistake, or one of them is lying to the player.

### P3. Scarcity is the product

Economics is the allocation of scarce means. A resource in surplus has no price and generates no
decision — it is decoration with a UI row.

> **Rule P3.** Every stock must be able to *bind* — there must be reachable situations where the
> player cannot do what they want because of that stock specifically.

**Test:** name a turn where this stock, and not another, is the reason you cannot act. If you cannot,
delete the stock.

---

## B. Dimensionality — how many resources there should be, decided rather than guessed

This is the section that answers the owner's question. The answer is not a number picked by taste;
it falls out of three tests.

### P4. Two stocks are genuinely separate only if something needs *both* and cannot trade one for the other

Microeconomics distinguishes **complements** from **substitutes**. A Leontief production function —
`output = min(x/a, y/b)` — describes perfect complements: you need both, and the *scarcer* one is the
only one that matters at the margin. Perfect substitutes collapse: if a cost can be paid with either,
the two goods are one good with two names.

> **Rule P4.** A stock earns its existence only if at least one important cost is a **bottleneck
> pair** — `min(x, y)` — where having more of one cannot rescue you from lacking the other.

**Test — write five real costs.** If none of them is a bottleneck pair, you have one currency wearing
several costumes, and the extra names cost you a UI row, an `INeedVector` axis, a `ValueMap` weight,
and a balance surface each.

**Worked example of the test passing:** *"a fire-demon fusion needs `essence.fire` specifically"* is a
true bottleneck — a mountain of `essence.ice` does not help, and that is exactly why element-typed
sectors are worth fighting over (see P11).

### P5. Convertibility destroys dimensionality — price it, gate it, or forbid it

If A converts to B at a fixed public rate, the economy is one-dimensional and the second resource is
a unit conversion. Arbitrage does the rest: players route everything through whichever faucet is
cheapest and ignore the others entirely.

> **Rule P5.** Any conversion between stocks must be **lossy, rate-capped, or gated** by a building,
> a turn cost, or a location. A free two-way conversion is a merge, so do the merge honestly.

**Consequence worth naming now:** a building that turns one stock into another — a converter — is a
*convertibility decision*, not just a building. It is allowed, but it must be priced under this rule,
and its rate becomes one of the most load-bearing numbers in the game.

### P6. A stock with one sink generates no decision

Opportunity cost is where strategy lives: the real cost of anything is the best alternative given up.
A currency spent on exactly one thing is not a currency — it is a progress bar with extra steps.

> **Rule P6.** Every stock needs at least **two competing sinks**, and they should pull on **different
> time horizons** — something that pays now versus something that pays later.

**Test:** name two things that compete for this stock and that a reasonable player would disagree
about. Souls pass today: summon (now) versus contract slots (capacity).

### P7. The player is not a spreadsheet

Bounded rationality: people satisfice rather than optimize once a problem has too many dimensions.
Past roughly four headline quantities, strategy play degrades into bookkeeping, and the players who
enjoy that are not the ones this game is for.

> **Rule P7.** Keep the **decision layer** to 3–4 headline stocks. Sub-types are free if they read as
> one concept — six element essences are one row called "essence", not six rows.

**Test:** can the player state their economic situation in one sentence? *"Rich in flux, starving for
ice essence"* passes. A sentence needing four clauses does not.

---

## C. Dynamics — what the economy does over a campaign, not over a turn

### P8. Investment versus consumption is the core loop, and payback period is its knob

Intertemporal choice: a unit now is worth more than a unit later, discounted by time preference.
Building an extractor is investment; summoning now is consumption. This tension *is* the strategy
layer of a builder game.

> **Rule P8.** Every investment must have a **legible, finite payback period** — the player must be
> able to reason "this pays for itself in N turns" without a calculator.

The failure modes are symmetric and both fatal:
- Payback far shorter than the campaign → investment always dominates → snowballing, and the winner
  is decided in the first ten turns.
- Payback longer than the campaign → buildings are a trap → nobody builds, and the builder layer is
  dead content.

**This makes campaign length an economic parameter.** [world-graph-ideal.md](world-graph-ideal.md)
§14 open thread 1 asks "how long is a campaign in turns?" as a pacing question. It is not — it is the
denominator every payback period is measured against, and it must be answered before any yield
number is picked.

### P9. Diminishing returns are what stop monoculture

Diminishing marginal product: the Nth unit extracted from the same place yields less than the first.
Without it, the optimal play is to find the single best sector and never do anything else.

> **Rule P9.** Extraction from one place must decay, and recovery must be slower than depletion.

**Already modelled and unused:** `WorldSector.DepletionMilli` exists in the shipped world model with
nothing reading it. It is the field this principle asks for, already hashed and already replayed.

### P10. The greedy play must not also be the safe play

A negative externality is a cost your action imposes elsewhere. Where extraction is free of
consequence, there is no decision to make about *how hard* to extract — only about whether you can.

> **Rule P10.** The highest-yield options must raise a cost someone has to deal with later.

**Also already modelled:** `StabilityMilli` and `PressureMilli` are on the shipped `WorldSector`, and
[world-graph-ideal.md](world-graph-ideal.md) §5.1 already frames a rift tear as *"pressure source;
shards if tapped"* — greed for yield, paid for in pressure. The machinery exists; the rule is that
this is the *shape* every rich option should take.

---

## D. Structure — where output comes from

### P11. Land, labour, capital — if output depends on only one, the others are decoration

The classical factors of production map onto this game almost exactly:

| Factor | Here |
|---|---|
| **Land** | sectors and their slots — what you hold |
| **Labour** | demons and bodies — who is standing there |
| **Capital** | buildings — what you have built |

> **Rule P11.** Output should depend on more than one factor. If a building produces the same amount
> whether or not anyone garrisons the sector, then labour is decoration and the army is pure cost.

This is worth taking seriously because of what it fixes for free: making garrisoned demons an *input*
to production gives "presence" ([world-graph-ideal.md](world-graph-ideal.md) §13) an economic job,
and makes the same bodies simultaneously a P2 upkeep sink and a production input. One mechanism, both
sides of the ledger, and a real reason to choose between garrisoning and marching.

### P12. Comparative advantage — specialization must beat self-sufficiency

Ricardo's result is that specialization plus trade beats autarky *even when one party is better at
everything*. In a territorial game this is what makes the map's geography matter and what gives
diplomacy something to trade.

> **Rule P12.** No player should be able to become self-sufficient in everything by expanding
> normally. Some inputs must be locatable only in places you do not hold.

**The element ring already does this for free.** Fusion demands element-matched essence, sectors have
element climates, and there are six elements against a handful of sectors. A player cannot hold all
six early. Keeping essences **non-substitutable** (P4/P5) is therefore not a balance detail — it is
the single thing making territory choice and future trade partners meaningful.

---

## E. Constraints this codebase imposes on any economy built in it

### P13. Determinism is an economic constraint, not only a technical one

The world map's first lock: *a save is `(seed, template, command log)` and replay must be
byte-identical*. That constrains the economy's mathematics directly.

> **Rule P13.** Yields, prices, and rates are **integer per-mille**, computed inside `step`, from
> state. No floating rates, no wall-clock accrual, no lazy "compute on read" that the state hash
> cannot see.

Note this contradicts [world-graph-ideal.md](world-graph-ideal.md) §7.2, which proposes on-read
accrual with "no ticking, no scheduler". That was written for the expedition system. The turn engine
**is** the scheduler, and §12.5 of the same document already says the right thing — one upkeep step
per held sector per turn. Where the two disagree, §12.5 wins.

### P14. Ledger before balance, everywhere

The soul economy's shape — append-only ledger as SSOT, watermarked balance as projection, a dedupe
key on every row — is proven, compaction-safe, and already survives trim and cold archive.

> **Rule P14.** Any new stock reuses that pattern. Every mutation carries a dedupe key derived from a
> durable fact id, so replay and re-ingest can never double-earn.

**This is also the mechanism that lets the map earn souls at all** without breaking
`spec-soul-economy.md`'s *"never earn from anything but recorded Activity facts"*: a world turn is a
durable uniquely-identified record, one row per `(world_id, turn)`, and that is a perfectly good
dedupe key.

### P15. Honest-server threat model — do not over-engineer against the player

Documented and accepted in `spec-soul-economy.md`: localhost, no auth, user-owned SQLite. Every
guarantee is tamper-**evident**, not tamper-**proof**. Single-player self-cheat is out of scope.

> **Rule P15.** Anti-exploit machinery is justified by *accident* prevention — double-earn on replay,
> a crash between fact and earn — not by adversarial players. Do not pay for defences against someone
> who owns the database file.

---

## 12. What these principles already decide

The point of writing principles first is that most "open questions" turn out not to be open.

| Question | Decided by | Answer |
|---|---|---|
| Can production write straight into the player's wallet? | **P13** | **No.** An effect outside `step` breaks replay. The seam has to be an explicit act that appears in the command log |
| Can the map mint souls at all? | **P14** | **Yes**, keyed on `(world_id, turn)`. The existing "never" survives intact, widened from match facts to recorded facts |
| Is a flux→souls converter building legitimate? | **P5** | **Only if lossy, rate-capped, or gated.** It is a convertibility decision; its rate becomes a top-tier balance number |
| Does a soul faucet need anything alongside it? | **P1 + P2** | **Yes — a territorial sink, in the same change.** A faucet scaling with sectors held against a flat summon price is the +2/kill incident with extra steps |
| Should materials and flux both exist? | **P4** | **Only if a real cost is `min(materials, flux)`.** Write five costs. If none bottlenecks, they are one stock |
| How many headline stocks? | **P7** | **3–4.** Six essences count as one, because they read as one concept |
| Should essences stay non-substitutable? | **P12** | **Yes**, and this is load-bearing, not a detail — it is what makes territory choice mean anything |
| Should extraction deplete? | **P9** | **Yes**, and `DepletionMilli` is already there, hashed and replayed, waiting for a reader |
| Should garrisons affect output? | **P11** | **Yes** — otherwise labour is decoration and the army is pure cost |
| How long is a campaign? | **P8** | **Answered 2026-08-22: there is no campaign.** Endless-grind RPG; the bound is *map size*, and payback is measured against how long a map is held |

**Still genuinely open**, because principles constrain them without determining them:

1. ~~Campaign length in turns~~ — **closed 2026-08-22**: no campaign, endless RPG. Replaced by **map size**, settled as a five-tier ladder in `empire-economy-ideal.md` §12.2.
2. **What shipping stock home costs** — free on the supply chain, a building, or an interceptable
   caravan. P6 favours *some* cost (it creates the second decision); it does not say which.
3. **The soul faucet's shape** — converter versus capped faucet. P5 permits either; P2 requires the
   matching sink in both cases.
4. **Per-sector versus per-world stock** — P10 and P3 both lean per-sector (it gives raiding a target
   and lets stock bind locally), but neither forces it.

---

## 13. Instrumentation — how we will know it is working

Principles that cannot be measured become opinions within a month. Each of these is cheap because the
turn engine already produces a report per turn.

| Metric | Principle | Healthy |
|---|---|---|
| **Net flow per stock per turn** — faucets minus sinks | P1 | Oscillates around zero over a campaign; never monotone positive |
| **Sink share by reason** | P6 | No single sink above ~70% of spend, or the others are not real choices |
| **Binding stock frequency** — which stock blocks an action, how often | P3, P4 | Every stock blocks sometimes; none blocks always |
| **Payback period of each building**, in turns, against campaign length | P8 | Comfortably inside the campaign, comfortably outside the first act |
| **Income growth rate versus upkeep growth rate** as territory grows | P2 | Same order. Divergence is the failure, and it is visible long before it is felt |
| **Yield concentration** — share of income from the single best sector | P9, P12 | Falls as depletion bites; a flat line means P9 is not wired |

A `world economy report` asserting the first two over a scripted campaign is a test, not a dashboard,
and it belongs in the same suite as the determinism goldens.

---

## 14. What this document is not

- Not a resource list. It deliberately names no new currency.
- Not a change to [resource-hub-ssot.md](resource-hub-ssot.md) — those five **actor pools** are a
  different scope and are untouched.
- Not authorized to build. Principles precede the SSOT; the SSOT precedes the spec.
