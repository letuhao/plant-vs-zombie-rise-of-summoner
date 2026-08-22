# The ideal — one resource hub for every pool in the game

**Status:** **Ideal capture (2026-08-22)** — a vision document, not a spec. No module ids, no build order, no acceptance criteria. It graduates to `resource-hub-ssot.md` when it is locked, matching the convention of [element-hub-ssot.md](element-hub-ssot.md), [actor-hub-ssot.md](actor-hub-ssot.md), and [status-ssot.md](status-ssot.md).

Origin: the owner's faction-resource proposal (2026-08-22), reproduced in §2 and amended in §4–§8. Grounding: [action-map.md](action-map.md) §9, [effect-atom-ideal.md](effect-atom-ideal.md), and the code audit in §3.

**Owner decisions (2026-08-22):**

- **Resources serve *our* mechanics — actions and skills — not the PvZ channel.** They are `rpg.*` layer, registered in the Actor Hub. See §5.4.
- **`soul` stays the summoner mechanism's currency and is *not* a per-actor battle resource.** `spirit` takes the per-actor essence slot, so the two never share a name. This supersedes the proposal's shared-`soul` row in §2.
- Four resource ids were locked earlier by [action-map.md](action-map.md) §8 as `hp` / `sun` / `soul` / `stamina`; that list predates this decision and its `soul` entry is now the **player-scoped** currency, not an actor pool.
- Refused names, on the record: `mana`, `essence`, `focus`, `will`, `qi` — generic fantasy, not PvZ.

---

## 1. The premise

> Every spendable, accruing, or draining number in the game is a **resource**, described by one registry, with a **scope**, a **polarity**, and an **accrual rule**. Nothing declares a pool in code again.

Today there is no such registry. HP is special-cased, shields are their own subsystem, sun lives in the lawn sim, souls live in a store, and the four pools the action program locked (`hp`, `sun`, `soul`, `stamina`) have no shared home. This document is the argument that they need one **before** any of them is built, because two of the four are already ambiguous (§5).

## 2. The proposal as received

Five resources per faction, arranged as Body + Energy + Essence:

| | Plant | Zombie |
|---|---|---|
| **Body** | `hp`, `stamina`, `sun` | `hp`, `stamina`, `hunger` |
| **Essence** | `soul`, `spirit` | `soul`, `rot` |

With the mirrors `sun ↔ hunger`, `spirit ↔ rot`, and `soul` shared.

The lore reasoning: Zomboss's zombification is a *technology* that replaces a living organism's animating force while leaving the body functional — so a zombie is not a corpse but a corrupted living system. `rot` measures how completely the zombie state has replaced the original, trading **body power for identity**: high rot means tougher, stronger, and less itself. `hunger` is metabolic instability rather than a food bar — it rises, and rising hunger costs regeneration and control until the zombie goes berserk; brains reduce it, and different brains do different things.

Zombification therefore becomes a **progression**, not a flag: `Soul 90 / Rot 15` → `Soul 40 / Rot 70` → `Soul 5 / Rot 100`.

## 3. What the code actually has

Verified 2026-08-22 in `src/`:

| Pool | Where it lives | Scope today |
|---|---|---|
| HP | The only battle pool; `EntityStatWriter` / FA10 / Funnel | Per actor |
| Shields | `ShieldRuntime`, element-typed, 4 derived families | Per actor |
| Sun | `SimEngine` / `SimModels` — **lawn sim only, never reaches an RPG battle** | Per **match** |
| Souls | `SoulEarnPolicy`, `RpgStore.Souls.cs`, expeditions, demon binding + daily tribute | Per **player**, persistent |
| Stamina, spirit, hunger, rot | Do not exist | — |
| Derived stat channels | 84 combat (asserted at exactly 84 by test) + status + progression | — |

**No resource registry exists.** No `resource.*` channel family exists.

## 4. The best idea in the proposal, and why

**Body + Energy + Essence is a membership rule, not decoration.** It answers "does this new number belong, and where?" — which is the question a registry has to answer forever. Keep it.

**Rot as an inverse resource is the strongest single mechanic here.** Almost every pool in a game is "more is better." A resource where *more makes you stronger and less yourself* is a real decision axis, and it is the one thing in this proposal that could carry a whole progression system. Keep it.

**Zombification-as-progression is the game's actual hook**, and it is better than the resource list it came wrapped in.

## 5. Three things that break in code as written

### 5.1 `sun` names two things in two layers — and that is fine, once said out loud

There are two suns, and they are not in conflict because they are not in the same layer (§5.4):

| | Layer | Scope | Owner |
|---|---|---|---|
| **Lawn sun** | `pvz.*` game foundation | Per match, a shared bank fed by sunflowers and spent to plant | The game. Untouched by anything here |
| **RPG sun** | `rpg.*` | **Per actor** — a plant's metabolic energy, spent on actions and skills | This hub |

An earlier draft of this section objected that a per-actor sun pool "deletes the sunflower→bank→plant loop." That objection conflated the layers: the lawn economy is `pvz.*` and this hub never touches it. **The owner's symmetric design works as proposed** — `sun ↔ hunger` are both per-actor body-energy gauges, and the mirror holds.

**Closed by the owner, 2026-08-22: they are not connected, and "connected" was never a real option.**

> The two games have two state machines. **No state is shared in either direction.** The only thing that crosses is **messages** — we capture events out of PvZ to build our own data, and we send `pvz.*` intent commands and Writer stat changes back in. Neither side reads the other's state.

So lawn sun and RPG sun are simply **two different things that share a word**, the way two programs can both have a variable called `count`. There is nothing to bridge, and asking whether to bridge them was the same layer confusion that produced the objection above.

The practical rule for this hub: **an RPG resource never reads a PvZ value.** If the RPG ever wants to know something about the lawn, it arrives as a captured *event fact* like any other telemetry — never as a shared number.

### 5.2 Polarity is missing, and it is not a detail

`hp`, `stamina`, `sun`, `spirit`, `soul` are **assets**: they fill up, you spend them, empty is bad. `hunger` and `rot` are **burdens**: they fill up, you purge them, full is bad.

One word — "resource" — currently covers both, and the moment a shared code path says `Regenerate(resource, amount)`, half the resources heal and half get worse. Every generic operation (`max`, `regen`, `restore`, `drain`, `is depleted`, "show a low warning") means the opposite thing depending on polarity.

**Amendment: `polarity: asset | burden` is a required field on every resource**, and the registry decides what regen means from it. This is cheap now and a bug farm later.

### 5.3 `soul` — resolved by the owner, 2026-08-22

**Settled: `soul` is the summoner mechanism's currency and nothing else.** It stays player-scoped and persistent (`rpg_soul_balances`, `rpg_soul_ledger`, `SoulEarnPolicy`, demon binding, daily tribute, expeditions — all shipped). `spirit` takes the per-actor essence slot, so the word is never overloaded.

The flow described below still works and is worth keeping as *lore*: an actor's spirit, extinguished, is what the summoner harvests as soul. It is a conversion between two named resources at two scopes, not one resource wearing two hats — which is the version that survives contact with code.

<details><summary>Superseded analysis (kept for the reasoning)</summary>

### The original problem

It already means the **player's persistent currency** (`SoulEarnPolicy`, expeditions, demon binding, daily tribute — all shipped). The proposal adds **per-actor identity**. The action map already flagged the first collision; this makes three readings of one word.

**Amendment, and this one is a synthesis rather than a rename:** the player is a **summoner who harvests souls from the fallen**. So an actor's soul and the player's soul bank are *the same substance at two scopes*, connected by a flow: an actor dies, its soul enters the player's bank. That is lore-coherent, it is what the game already does mechanically, and it means `soul` is one resource with a `scope` field rather than two resources sharing a name.

This only works if scope is a first-class registry field. Which it should be anyway — see §6.

</details>

## 5.4 Which layer resources live in — and it is not the PvZ one

These resources exist for **our** game mechanics: actions, skills, costs, and the turn kernel. They are not PvZ attributes, and the architecture already separates those two things on purpose ([pvz-middle-layer.md](pvz-middle-layer.md), [pvz-stats.md](pvz-stats.md), [software-architecture.md](software-architecture.md) §3).

The existing split, in the repo's own vocabulary:

| Layer | Owns | Reaches Unity |
|---|---|---|
| `Pvz*` / `pvz.*` — the **game foundation** | `StatChannels` (`hp · maxHp · atk · defense · arm1 · arm1Max · arm2 · arm2Max`), facts, intents | Yes — `EntityApply` → `EntityStatWriter` is the only write path |
| `rpg.*` — **content and progression that *uses* the foundation** | Derived channels in the Actor Hub, overlay combat, status, shields | **No, by design.** "Direct Unity writes / bypass capture" is explicitly listed as what this layer does not own |

**Resources belong to the `rpg.*` layer, in the Actor Hub** — the shared substrate that is *"the only place derived channels are registered/composed."* Asking whether stamina reaches a Unity field is a category error: the layering exists so that it does not.

`hp` is the one exception, and only in PvZ mode, because the overlay principle names Unity as SSOT for current HP. Every other resource is ours outright — the same way shields already are.

And in **standalone / web mode there is no Unity at all**: the scope note in `software-architecture.md` §3 is explicit that web-mode matches have no Unity and *"the server's battle engine owns their state outright."* So the RPG battle — the place actions and skills actually live — is the unconstrained runtime. PvZ mode is the special case, not the reference.

Two things this settles:

1. **Resources are a derived channel family**, registered in the Actor Hub like `combat.*` and `status.*`. They are not `StatChannels` entries, and they must not join `AllCombatChannelIds` (asserted at exactly 84).
2. **A resource's `visibility` field is about our UI**, not about the lawn — the overlay owns the display surface for anything in this hub.

## 6. The registry shape this implies

Every resource declares:

| Field | Values | Why it exists |
|---|---|---|
| `id` | `hp`, `stamina`, `sun`, `soul`, `spirit`, `hunger`, `rot` | |
| `scope` | `actor` · `side` · `match` · `player` | Resolves the sun and soul ambiguities without renaming anything |
| `polarity` | `asset` · `burden` | §5.2 — decides what every generic operation means |
| `class` | `body` · `energy` · `essence` | The membership rule from §4 |
| `accrual` | `none` · `regen` · `onEvent` · `generated` | §7 — the owner asked for generators, and they are a distinct shape |
| `bounds` | max channel, floor, and whether it may exceed max | |
| `onEmpty` / `onFull` | what happens at the rail | Berserk at full hunger, death at zero HP, transformation at full rot |
| `visibility` | which UI surfaces show it | Not every pool is a bar |

**The set is per actor, not per species.** This falls out of zombification-as-progression: a human that becomes a zombie **loses `sun` and gains `hunger` and `rot`**. If the resource set were a species constant, transformation could not be expressed. That is a real architectural consequence of the best idea in the proposal, and it is easy to miss.

## 7. Accrual — three shapes, not one

The owner asked for "resource generators for actor," and generators are genuinely a third thing:

| Shape | Example | Structure |
|---|---|---|
| **Regen** | stamina per tick, spirit per tick | A rule *on the pool*, driven by the timeline kernel |
| **On event** | soul on kill (the `soul-eater` trait already does this), rot on being hit by zombie tech | A rule *on a trigger* — an effect atom, not a resource feature |
| **Generated** | a sunflower producing sun into the side bank | An **actor with an output**, writing into a pool it does not own |

The third is the one with no precedent in the codebase, and it is the interesting one: it makes "what do I put on the board" an economic decision rather than a combat one, which is the PvZ loop.

Note the second shape belongs to the **effect-atom** program, not here. Resources declare *that* they can be granted; atoms declare *when*. Keeping that line is what stops this from becoming a fifth content system.

## 8. Naming notes

**The Brain collision.** The proposal offers `brain` as a cartoonish alternative to `rot`. It collides: `hunger` is reduced by *eating brains*, so `brain` would be both the consumable and the cognition stat inside one faction. Pick one meaning for the word.

**Tone.** PvZ canon makes Zomboss a *scientist* — zombification is engineering, not a curse. `rot` imports a decay-horror register the franchise mostly avoids. The mechanic is right either way; only the label is in question, and a name pointing at *installed technology* rather than decay would sit closer to the source. This is a small point and `rot` is defensible — the games do have visibly decaying zombies.

**Already refused** (action map §9.2): `mana`, `essence`, `focus`, `will`, `qi` — generic fantasy, not PvZ.

**Still available and lore-native:** `brains` as the zombie-side deploy bank mirroring sun (PvZ Heroes uses exactly this pairing), and `plantFood` as a charge-and-spend burst (PvZ2). Neither is in the proposal; both fit the registry.

## 9. Cost, stated honestly

Seven resources is not seven numbers. Each is a max channel, a regen channel, an accrual rule, a serialization field, a UI element, a balance axis, and — once it appears in a battle report — **a golden-visible number that moves `RulesetVersion`**.

Two mitigations make this affordable, and both are already the plan:

1. **The registry is data.** Adding the eighth resource costs a row, not a system. That is exactly why the owner asked for an SSOT file first, and it is the right instinct.
2. **Resource channels are their own family list.** They must **not** join `AllCombatChannelIds`, which a test asserts is exactly 84.

## 10. Inventory — every resource in the game, 2026-08-22

### 10.1 Shipped today

| Resource | Scope | Layer | Where |
|---|---|---|---|
| `hp` | actor | `pvz.*` write channel in PvZ mode (Unity is SSOT); overlay-owned in web mode | `StatChannels`, FA10, `EntityStatWriter` |
| **shields** — 7 element-typed pools per actor | actor | `rpg.*` | `ShieldRuntime`; 4 derived families (`capacity` / `toughness` / `pen` / `regen`) × omni + 6 elements |
| `soul` | **player**, persistent | `rpg.*` | `rpg_soul_balances` + `rpg_soul_ledger`, `AwardSouls` / `TrySpendSouls` with an overflow ceiling; `SoulEarnPolicy`, demon binding, daily tribute, expeditions |
| `xp` | actor, persistent | `rpg.*` | `rpg_xp_ledger` + `rpg_actor_progression` |
| demon materials | player, persistent | `rpg.*` | `rpg_demon_materials` — fusion inputs |
| lawn `sun` | match | **`pvz.*` — not ours** | `SimModels.Sun`; the sunflower→bank→plant economy, untouched by this hub |

**Shields are already a resource and nobody has been calling them one.** They have capacity, regen, depletion, and per-element pools. The registry either adopts them or explicitly excludes them, and silently doing neither is how a second resource system gets born.

### 10.2 Proposed — new, none built

| Resource | Class | Polarity | Plant | Zombie |
|---|---|---|---|---|
**Superseded 2026-08-22 by §10.2a.** The faction-specific ids below (`sun`, `lifeEnergy`, `rot`, and the `bloom`/`graft` candidates) were collapsed into one shared set with per-faction display labels. Kept only as the reasoning trail.

| `stamina` | body | asset | ✅ | ✅ |
| `sun` (rpg-layer, per actor) | energy | asset | ✅ | — |
| `hunger` | energy | **burden** | — | ✅ |
| `spirit` | essence | asset | ✅ | ✅ |
| `lifeEnergy` | essence | asset | ✅ | — |
| `rot` → **needs renaming, §10.4** | essence | **burden** | — | ✅ |

### 10.2a The locked model — five ids, one set, display-mapped per faction

Owner decision 2026-08-22, replacing everything above: **both factions carry the same five resources.** The faction difference is a **front-end label**, not a different pool.

| id | Class | Plant label | Zombie label | Exhaustion |
|---|---|---|---|---|
| `hp` | body | HP | HP | **None** — depletion is death, already owned by the turn FSM's `Downed` state |
| `stamina` | body | Stamina | Stamina | ✅ debuff |
| `hunger` | energy | **Sun** | Hunger | ✅ debuff |
| `spirit` | essence | Spirit | Spirit | ✅ debuff |
| `qi` | essence | **Yang** | **Yin** | ✅ debuff |

Verified: `qi`, `yin`, and `yang` collide with nothing in `src/` or the web app.

**Why this is the right call, stated plainly:** the previous model had nine ids across two factions to express five concepts, and it produced two collisions (`rot` against a shipped status, `sun` against the lawn economy) and an asymmetric count. One set of five kills all of it. `rot` is no longer a resource, so the status keeps its name uncontested.

**On `qi`:** it appeared on the refused-names list as generic fantasy. That refusal was about *player-facing* naming, and this design separates the **id** from the **label** — players read Sun / Yang / Yin, which are lore-native; `qi` is only ever an internal key. The objection does not apply to the layer it now sits in.

**Where the labels live:** the plant↔zombie mapping is **content, not code** — a species or faction declares its labels, and the registry stores an id. Baking `if (plant) "Sun"` into the model would reintroduce the faction branching the single set just removed.

### 10.2b Exhaustion — the mechanic that gives the pools teeth

Owner decision 2026-08-22: **every resource except `hp` has an exhaustion mechanism that debuffs derived stats.**

This is the piece that makes the model a *system* rather than four bars. It also lands on the best-supported seam in the whole codebase: an exhaustion debuff is a **`stat.derived` atom**, which per the atom catalog is the one kind with full runtime support — lawn ✅, battle ✅, sim ✅ — while most kinds are lawn-only. Nothing new has to be built to carry it.

Three things the spec must settle, because each is a live failure mode:

**1. Hysteresis, or the debuff flickers every tick.** A pool sitting at exactly zero while regen trickles will apply and clear the debuff on alternate ticks, spamming status churn and VFX cues — and this repo has already been bitten once by per-tick churn near the hot path. Exhaustion needs two thresholds: enter at ≤ X, leave at ≥ Y, with **Y strictly greater than X**. Minecraft's hunger bar is the reference implementation of this whole mechanic, thresholds included.

**2. The death-spiral floor.** Derived stats include the turn channels (`turn.speed`, `turn.haste`). **See Q1 in §11 — this is narrower than it first appears**: with time-based regen, slowness is recovery-positive, and the only true spiral is an exhaustion debuff that touches a channel feeding its own resource's regen. A proportional floor on the turn channels is still required, but for kernel-stall reasons rather than balance ones.

**3. Which channels each exhaustion hits.** Derived ops are `Flat · Increased · Replace · Flag` with per-channel caps (resist caps at 0.95), and there is deliberately **no `More`**. Four exhaustion debuffs stacking on one actor must respect those caps, and the composition order has to be defined once rather than per resource.

**`hunger` is an asset that depletes, not a burden that fills.** Full bar means fed; empty means starving. That inverts the plain-English reading of the word, and it is fine — Minecraft's hunger bar works exactly this way and players read it correctly. It does mean the earlier `polarity` field (§5.2) simplifies: with `rot` gone, **every resource in the locked set is an asset**, and polarity becomes a field the registry can carry for future use rather than one the current set needs.

### 10.3 The count — balanced at five each

Owner decision 2026-08-22: **`lifeEnergy` fills the plant's open essence slot.** No collision anywhere in the repo. camelCase matches the existing channel convention (`maxHp`, `arm1Max`).

**Superseded by §10.2a** — the balance below was achieved instead by one shared set of five ids with per-faction labels, which reaches the same count without nine ids or two id collisions.

| | Body | Energy | Essence | Total |
|---|---|---|---|---|
| **Plant** | `hp`, `stamina` | ~~`sun`~~ | `spirit`, ~~`lifeEnergy`~~ | **5** |
| **Zombie** | `hp`, `stamina` | `hunger` | `spirit`, ~~`rot`~~ | **5** |

**`lifeEnergy ↔ rot` is the sharpest mirror in the whole model, and it is better than the one it replaced.** It is not a polarity mirror — one is an asset and one is a burden — it is a mirror of *what animates you*. A plant is alive, driven by life energy. A zombie is not, and is driven by what Zomboss installed in place of it.

That makes zombification a **resource conversion** rather than a flag: `lifeEnergy` drains as `rot` rises, and the same actor record carries both through the transition. It is exactly the progression the proposal wanted, expressed as arithmetic instead of a state machine.

### 10.3.1 Three speeds — the rule that stops these collapsing into each other

Five pools per faction only stay distinct if they move at different rates. Otherwise `lifeEnergy` becomes a second `sun`, and `spirit` becomes a second `stamina`, and balance quietly merges them.

| Speed | Resources | Changes when |
|---|---|---|
| **Per action** | `stamina`, `sun`, `hunger`, `spirit` | Every action — spent, regenerated, accrued. These are the ones the action layer prices against |
| **Per battle** | `hp`, shields | Damage and healing within an encounter |
| **Per transformation** | `lifeEnergy`, `rot` | Only on major events — zombification, possession, cure, death. **Never a per-turn pool** |

The third row is the load-bearing one. `lifeEnergy` and `rot` are *identity gauges*, not budgets: an action never spends them, and that is precisely what keeps them from duplicating `sun` and `hunger`. It also settles the last standing balance risk in §11 — `spirit` is the per-action essence pool, and nothing else in the essence class moves at that speed.

### 10.4 `rot` collides with a shipped status id — **resolved 2026-08-22 by dropping the resource**

`rot` is no longer a resource (§10.2a), so the status keeps its name uncontested. The finding is kept because it is the concrete evidence for a rule this hub should follow permanently: **check a proposed resource id against the status catalog, the VFX catalog, and the table names before locking it.** Two of the four faction-specific ids proposed in one afternoon collided with something already shipped.

Verified 2026-08-22: `rot` is a **working contagion status** in three places — registered in `StatusCatalogBootstrap` (`Contagion`, `Spread` + `PulseHp`), categorised in `StatusCategoryRegistry`, and carrying a VFX cue in `VfxCatalog` (drip aura). It is one of the 11 statuses the atom catalog lists as functional.

Naming a resource `rot` puts one word on two subsystems at exactly the moment the effect-atom program is building a **closed id vocabulary**. Rename the resource, not the status — the status ships and spreads.

`graft` is the recommendation: it means foreign tissue attached to a living body, which is precisely what Zomboss does, and it fits the franchise's scientist-not-sorcerer framing better than decay language does. No collision.

## 10.5 "Rest" — proposed definition

Pools persist across a run and refill at rest (owner, 2026-08-22). The game has no rest concept, so here is one.

> **A rest is a place, not a timer. A *run* is a sortie away from the summoner's base; a *rest* is the return.**

Rest as a place rather than a duration is what makes attrition legible: the player always knows whether they are "out" or "home", and no timer has to be balanced. It also maps onto both structures that already exist without inventing a third:

| Structure | Run | Rest |
|---|---|---|
| Expedition (`rpg_expeditions`, `rpg_expedition_members`) | The expedition, across all its encounters | Return to base |
| World map (sectors, lanes, turns) | Travel between safe sectors | Arriving at a home or friendly sector |
| A one-off web skirmish | A run of exactly one encounter | Immediately after — so a skirmish always starts full and persists nothing |

**Refill is full.** Partial refills, camp supplies, and rest-site upgrades are content that can arrive later; a fractional default would be a balance number invented before there is anything to balance it against.

Four consequences the spec must carry:

1. **Pool state needs somewhere to live between encounters.** Today nothing stores a demon's stamina between fights. This is a per-member row on the run, not a per-battle value.
2. **`ExpeditionResolver` must thread pool state through its encounters.** It resolves several battles back to back with no player; if pools do not carry, expeditions silently ignore the entire resource system.
3. **`hp` follows the same rule, and this is a real gameplay change.** A demon that ends a battle at 10 HP starts the next one at 10. That is the intended attrition, but it changes expedition outcomes and therefore every balance number derived from them.
4. **A run must always be able to end.** If a world-map player can wander indefinitely without reaching a friendly sector, exhausted pools become a soft-lock. Either rest sites are guaranteed reachable, or there is a fallback — retreat, or a slow out-of-combat trickle that is not a rest.

## 10.6 What `now` means across a save, a load, and a battle boundary

`CooldownLedger` stores absolute ticks and keeps counting while an actor is suspended; resources resolve lazily from `(value, lastTick)`. Both are correct alone, and they must agree.

> **Lazy within a battle; concrete between battles.**

- **Inside a battle**, `now` is the simulation clock tick and nothing else. It is persisted with the battle state, so a save and a load resume cooldowns and lazy pools from the same tick. No wall-clock value ever enters either.
- **At battle end**, every lazy pool is **resolved to a concrete value** and `lastTick` is dropped. What persists across the run is a number, not a number-plus-a-timestamp from a clock that is about to reset to zero.
- **Cooldowns do not survive a battle boundary** — they are measured in a clock that no longer exists. A cooldown intended to span encounters is a *run-scoped* effect and belongs to the run, not to `CooldownLedger`.

That last rule is the one worth stating loudly: carrying a tick-valued cooldown into a battle whose clock restarts at zero would make it expire instantly, and the bug would look like content being wrong rather than time being wrong.

## 11. Open questions

Each carries a proposed resolution. None is locked.

### Q1 — Does exhaustion debuff the turn channels?

**The spiral is smaller than it first looks, and a correction is owed.** An earlier note in this document claimed exhaustion debuffing `turn.speed` traps an actor. That holds only if regeneration is *per action*. With the virtual-time kernel, regen is **per tick of simulated time** — so a slowed actor waits longer between actions and therefore regenerates *more* before each one. Slowness is recovery-positive, and the remaining cost is simply acting less often than the enemy, which is the intended punishment.

The real hazard is narrower and precise:

> **An exhaustion debuff must never touch a channel that feeds its own resource's regeneration.** That, and only that, is a true spiral.

It is checkable rather than judged: the registry knows which channels feed each resource's regen, so content validation can reject the cycle at load — the same shape as the atom program's bind-time rejection.

**One separate matter is a kernel correctness requirement, not balance.** Readiness is `work / rate`. As `rate` falls toward zero the arrival tick runs away toward never, which stalls the queue rather than slowing it. The readiness model already applies `max(1, …)`, but a rate of 1 against a base of 100 is a 100× wait, which is a stall in everything but name. **A real floor on the turn channels — a fraction of base, not an absolute 1 — belongs in the readiness spec regardless of exhaustion.**

**Proposed:** allow exhaustion to debuff turn channels; forbid self-regen cycles by validation; add a proportional floor to the turn channels in the readiness model.

### Q2 — Thresholds, channels, and composition

**Exhaustion should be a status, not a new concept.** `StatusRuntime` is shipped and already owns instances, stacking, family mutex, resistance, VFX cues, and — the relevant one — **`icd_ms`**, which exists precisely to stop an apply/clear cycle from churning. The flicker problem in §10.2b(1) is solved by a mechanism already in the tree.

Making exhaustion a status buys three more things for free: it is **visible**, it is **resistable** (a trait or item can grant resistance to stamina exhaustion), and it is **dispellable**. Those are all good design that would otherwise need inventing.

Hysteresis still wants explicit thresholds — `exhaustEnter‰` / `exhaustLeave‰` on the registry row, validated as `leave > enter` at load, defaulting to enter 0‰ / leave 100‰.

**Which channels each exhaustion hits is content, not code.** The registry stores a **container id**; the container is atoms. That is the whole point of the atom program, and hardcoding a channel list here would make this the fifth content system it exists to prevent. A sketch of the intent, to be authored as data rather than fixed here: stamina hits physical output, hunger hits sustain, spirit hits status resistance, qi hits elemental power.

**Composition needs no new rule.** Exhaustion debuffs are ordinary derived-channel mods and go through the same `DerivedComposer` with the same four compose kinds and the same per-channel caps as everything else. What it needs is a **test that four simultaneous exhaustions still respect the caps** — the cap logic exists; nothing has ever pushed four debuffs through it at once.

### Q3 — Do shields join the registry?

**Proposed: exclude, explicitly and with the reason recorded.**

Shields have capacity, regen, and depletion, which is why they keep looking like resources. But nothing ever *pays* a shield to act. They are consumed by the damage pipeline, they are element-typed across seven pools, and they carry `toughness` and `pen` — damage-interaction semantics no action cost has or wants. They are also shipped, specced, and locked by tests, so folding them in means reopening a byte-identity-locked subsystem to gain nothing.

The registry's job is the **action economy**. Shields belong to the **damage layer**. Recording the exclusion is what stops this being re-argued every quarter — and the FE can still render them beside the resource bars without the model conflating them.

### Q4 — Where the registry lives

**Proposed: an SSOT doc plus a channel family, not a new program.**

Two halves, and they land in different places:

- **The magnitudes are Actor Hub derived channels** — `resource.max.<id>` and `resource.regen.<id>`. There is no choice here: the Actor Hub is *"the only place derived channels are registered/composed."* They form their own family list and must not join `AllCombatChannelIds`, asserted at exactly 84.
- **The registry itself** — ids, class, labels, exhaustion container, scope, thresholds — is a small **code catalog**, matching `StatusCatalog`'s ADR-locked code-first precedent and the atom program's "kinds are code, values are data" rule. Five entries.

So the artefact is `resource-hub-ssot.md` plus a `ResourceCatalog` in Core, and this hub relates to the Actor Hub exactly as the Element Hub does. Not its own program, and too widely read to be a module inside `action` — the atom program needs it for exhaustion containers and `resource.delta`, the kernel needs it for regen, and the FE needs it for bars.

### Q5 — Regeneration must be lazy, not scheduled *(new)*

Not previously listed, and it matters more than it sounds. Four regenerating pools across 200 actors is **800 recurring events**. The kernel's measured steady state is 2.8 events per frame against a 0.15 ms slice; 800 recurring regen events would dominate it outright and turn a comfortable budget into the thing being optimised.

There is already a law for this. The atom ideal refuses background recomputation on exactly this ground: *"compute on read or on write, never on a schedule."*

**Proposed: resources regenerate lazily.** Store `(value, lastTick)` and resolve on read as `value + rate × (now − lastTick)`, clamped. Zero scheduled events, exact under the integer clock, and it makes save/load trivial because a pool that was not read simply has not moved. The only case needing care is **exhaustion crossing**: a lazily-resolved pool can cross the leave-threshold without anything observing it, so the exhaustion status must be re-evaluated on read, not only on write.

*Resolved:* `soul` versus per-actor essence (owner: `soul` is the summoner's alone, `spirit` is the actor's) · the plant/zombie split (owner: one shared set of five, faction differences are FE labels — §10.2a) · the `rot`-versus-status collision (dissolved; `rot` is no longer a resource) · sun's scope (dissolved; lawn sun stays `pvz.*`, the RPG pool is `hunger` displayed as Sun) · whether `spirit` collapses into another essence pool (no — the three-speed rule, §10.3.1) · polarity (with `rot` gone, every locked resource is an asset that depletes).
