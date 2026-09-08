# Lane G2 — consumables, and the action-layer seam

**Status:** Lane G2 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Gap lane from [reconciliation-plan.md](reconciliation-plan.md) §R3 — one of four mechanisms the
thirteen-lane enrichment round left unowned.

---

## 1. Scope

### This lane owns

- **What a consumable is**: an item that is spent to produce an effect, and the rule that separates it
  from equipment (it is destroyed by use; equipment is not).
- **The taxonomy** — six classes, each with its trigger shape, its duration model, and whether v1
  authors it or only declares it.
- **The v1 shape** — the deliberately degenerate form, and the proof that the action layer absorbs it
  without a migration.
- **The mode split.** Consumables reach three runtimes by three different roads, and one of them is
  not the action queue. §2.2 is the crux of this document.
- **Charges, exclusion groups, and where a cooldown will live** — including the decision *not* to build
  one yet, and what makes that safe.
- **The carry limit** — how many a player brings into a run, which is the balance lever.
- **The atom mapping** — what a consumable binds, to whom, for how long, and withdrawn by what. And
  what the atom layer does not have (§4.5).
- **The seam declaration** to the action program: `grants_action_id`, `cooldown_key`, and the three
  things `A3 action-costs` has to widen. Declared, not designed.

### This lane does NOT own

| Thing | Lane that owns it |
|---|---|
| The action layer — envelopes, targeting, usability conditions, action costs. **I declare a seam and stop** | **action program** ([action-map.md](../action-map.md)) |
| Bags, stacking, stack caps, salvage safety, the item event log | **I13** ([ssot-inventory.md](ssot-inventory.md)) |
| Recipe pricing, the cost vocabulary, salvage yield. **I say what a consumable *is*; I9 prices it** | **I9** ([ssot-materials-crafting.md](ssot-materials-crafting.md)) |
| The **six** locked actor resources (`hp` · `stamina` · `hunger` · `spirit` · `qi` · `poise`). **I refill them; I do not redesign them** | Resource model, locked ([decisions.md](../decisions.md) row *Resource model (2026-08-22, **six** 2026-08-26)*) |
| The category taxonomy and the `item_category` row for `consumable` | **I3** ([ssot-item-categories.md](ssot-item-categories.md)) |
| Equip slots and roles, including `girdle` | **I2** ([ssot-equip-slots.md](ssot-equip-slots.md)) |
| The rarity ladder. **Consumables never enter it** — §4.6 | **I1** |
| Carried-bonus mechanics from unequipped inventory (charms, attunement, resonance) | **I10** ([ssot-charms.md](ssot-charms.md)) |
| Turning a drop event into an instance | **I12** |
| Post-drop mutation of a frozen instance | **I6** |
| The status catalog and its payload kinds. **I ask for one payload; I do not build it** | status stream / **E12** |

---

## 2. The model

A **consumable** is a container of atoms with a **quantity** instead of an identity. Using one
decrements the quantity by one and applies the container's atoms. When the quantity reaches zero there
is nothing left — that is the whole difference from equipment, and every other property follows from it.

Because a consumable is destroyed by use, it cannot roll values. A rolled item is unique by
construction and cannot stack ([item-ideal.md](../item-ideal.md) §7); a consumable stacks, so it is
**unrolled** — fixed core only, `pool_rolls = 0`, no tier window. Its strength axis is an authored
**grade**, not a rarity rung. §4.6.

### 2.1 The central tension, stated before anything else

Actions are locked as **a battle-mode concept**, and the lawn never schedules one:

> *"**Actions are a battle-mode concept only** — PvZ mode is a stateless observer with no queue and no
> per-actor machine, so the lawn never schedules an action."*
> — [decisions.md](../decisions.md), *Action model (2026-08-22)*, **LOCKED**

And [item-ideal.md](../item-ideal.md) §7 says a consumable *is* an action:

> *"A consumable does something **when used**, which is an **action** … A healing potion is therefore an
> item that carries an action, not an item that carries atoms directly."*

Put those together and PvZ mode — the mode where a player most expects to throw something at a zombie —
is the one mode where the mechanism that would deliver it does not exist and is not going to.

**The answer is not one mechanism. It is three roads, and this lane says so out loud rather than
implying coverage it does not have.**

### 2.2 Three modes, not two

The brief framed this as battle-only versus lawn-via-intent. That is one mode short. This game has
**three** runtimes and they differ on the only axis that matters here — *is there a moment at which a
player can decide to use something?*

| Mode | Who owns the clock | Is there a "use" moment? | The road a consumable would take |
|---|---|---|---|
| **Expedition** (standalone loop #1) | Nobody — the outcome is **sealed at dispatch** and revealed at collection | **No.** `ExpeditionResolver` is pure: `(tier, squad, seed, elapsedTicks)` → outcome (`src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:39-49`). There is no mid-run input | **Pre-dispatch.** The consumable is an *input to the seal*, joining the squad snapshot before the seed resolves |
| **Battle** (standalone loop #2, web/interactive) | The battle-timeline kernel | **Yes** — this is what the action layer is for | The action queue. `A1`–`A5`, unbuilt |
| **Lawn** (PvZ mode) | Unity | **Yes**, but nothing overlay-side may schedule it | The **intent / command road** — `pvz.*` commands, not the action queue. §2.4 |

The expedition row is the one the brief did not anticipate, and it is the most important one, because it
is the only mode that **ships, needs no in-combat input, and works with the game closed.** It is
therefore where v1 lives.

### 2.3 The v1 shape — degenerate, and out of combat

> **A v1 consumable is a preparation item. It is spent at a menu, before a run, and its effect lasts
> that run.** No targeting, no cost beyond the item, no cooldown, no in-combat use.

That is the "deliberately degenerate form" the ideal offers, with one correction: the ideal proposed
*self-targeted, instant, no cost* **in combat**. Combat is exactly where it cannot ship, because in
battle a bound `resource.delta` is a verified silent no-op —

> *"Battle's sink does handle FA10, but no ATOM can reach it — `BattleEngine` never grants and never
> calls `OnEvent`, so a bound `resource.delta` is a silent no-op."*
> — `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:125-128` (defect **D6**)

Shipping "self-targeted instant heal, in battle" would ship a potion that does nothing. Moving the use
moment **out** of combat, to the menu that already exists, is what makes v1 real instead of decorative.

### 2.4 Two modes may need two mechanisms — and they do

Stated plainly, as the brief requires:

- **Battle-mode consumables are the action layer's**, and this lane will not build a second scheduler
  for them. The seam is one nullable column (§5.2) and it is the same column `I2` already declared for
  weapons (`ssot-equip-slots.md:204-206`).
- **Lawn-mode consumables are not actions and never will be.** Their road exists today and is
  completely separate: a `pvz.*` intent command ([pvz-intent.md](../pvz-intent.md)) enqueued on the
  injector inbox, drained on the Unity main thread (`src/FusionRpg.Injector/CheatCommandRunner.cs:42-49`),
  applying an effect through the shipped `effects.grants.apply` path
  (`src/FusionRpg.Core/Effects/EffectGrantSession.cs:64-67`;
  `src/FusionRpg.Injector/CheatCommandRunner.cs:97`, `:719-750`). Idempotent by `correlationId`,
  auditable as an Activity fact — the pattern the intent doc exists to enforce.

**Both are deferred past v1**, for different reasons: battle waits on `A1`–`A5`; lawn waits on a use
affordance in the overlay UI and on `capPerMatch`, which is in the FA9 allowlist with **no
implementation anywhere** (gap **G4**; `AtomKindRegistry.cs:138-140` carries the
`NotImplementedNote`). A lawn consumable with no per-match cap is unbounded spam by construction.

### 2.5 The one thing v1 must get right

Everything above is sequencing. The design decision that actually matters is this:

> **v1 authors the *effect* as a container of atoms from day one, and degenerates only the *use path*.**

That is what makes the later absorption free (§4.1). A v1 that shortcut the effect into a
`heal_amount INT` column would have to be migrated, re-priced, re-displayed and re-hashed the moment the
action layer arrived — the exact failure `I3` argued out for base damage
(`ssot-item-categories.md:112-131`). The container is the invariant; the use path is the temporary part.

---

## 3. Taxonomy

Six classes. The seventh candidate — permanent stat-up — is refused with cause (§3.2).

### 3.1 The classes

| Class | What it does | Trigger shape | Duration model (locked units) | v1 |
|---|---|---|---|---|
| `restore` | Refills a pool now — hp today, the other four resources when they exist | fires **once at use** | instant; no lifetime | **author** |
| `draught` | A stat buff for the coming run | applies at **run start**, with the squad snapshot | **run-scoped** — withdrawn at run end. Not a ms clock, §4.5 | **author** |
| `ward` | A depleting absorption layer | applies at **battle setup** | **integer ms** — `BattleInnateShield.DurationMs` (`src/FusionRpg.Core/Battle/BattleModels.cs:31-35`). The only real clock v1 has | **author** |
| `board` | Something thrown at the lawn — a bomb, a wall, a freeze | fires **once at use**, lawn only | instant | **declare only** |
| `revive` | Returns a `Downed` actor to the fight | fires **once at use**, targets one actor | instant | **declare only** |
| `utility` | A non-combat state change — cure, unlock, rename, teleport | fires **once at use**, at a menu | permanent state change, outside combat | **declare only** |

**Why `revive` is declare-only and not impossible.** Its target state already exists: the turn FSM has
`Downed` as a distinct non-terminal state, and `Downed → Charging` is a legal transition
(`src/FusionRpg.Core/Battle/Timeline/TurnState.cs:22`, `:53-61`), with the comment at `:30-31` saying
the state exists precisely so a revive does not attempt an illegal `Dead → Charging`. What is missing is
not the state — it is the use moment, which is battle-mode, which is the action layer's. So `revive` is
the cleanest possible proof that the seam is real rather than a hand-wave.

**Why `board` is declare-only.** It is the class a PvZ player actually wants, and it is blocked on two
concrete things named in §2.4 — an overlay use affordance, and `capPerMatch` (G4). Authoring it before
either exists produces rows nothing consumes, which SC7 forbids.

### 3.2 Refused: the permanent stat-up

A consumable that permanently raises a stat is refused **as a consumable**, on three grounds:

1. **It has no container to bind to afterwards.** Every bonus in this system is atoms reaching an actor
   through a binding (SC1). Consume the item and the binding's source is gone; the only way to make the
   bonus stick is a second, permanent, sourceless write — which is the ad-hoc stat path the whole
   program exists to remove.
2. **It is invisible to the power model.** `actorPower` prices what is on the effect list
   ([definitions.md](../effect-atom/definitions.md) §7). A consumed permanent is nowhere on that list,
   so every budget, comparison, and display understates the actor forever.
3. **It duplicates `I6`.** "Make this thing permanently better" is enhancement, and enhancement already
   has a mutation model, a cost, and a reproducibility rule.

**What is allowed instead:** a permanent stat-up as a **quest reward** — `quest` category, `I3`,
one-shot, authored not farmed, so it is a progression event with an item wrapper rather than a resource
the player grinds. That is how the genre actually ships it (Diablo 2's Lam Esen's Tome, Path of Exile's
Book of Skill — recalled, **unverified**), and it is the version that does not create a treadmill.

### 3.3 Charges, stacks, and cooldowns

| Question | v1 answer | Why |
|---|---|---|
| Single-use or multi-charge? | **Single-use.** The stack *is* the charge counter — `rpg_item_stock.qty` (`ssot-inventory.md:259` already scopes it to hold "unrolled consumables"), decremented by one per use | Zero new state. A charge counter on an instance needs per-binding runtime state, which is `E15`'s and unbuilt ([effect-atom-map.md](../effect-atom-map.md):81) |
| Refill at rest? | **No, and deliberately not** | *"Rest is not a concept this game has yet"* ([action-map.md](../action-map.md) §10.4a) — the candidates are a wave boundary, a world node, or an expedition return, and they are not the same thing. Naming one here would invent a world-map concept this lane does not own |
| Shared cooldown groups? | **Needed later, not now — and the mechanism is already chosen, twice.** v1 declares the column and leaves it `NULL` | See below |
| Diminishing returns? | **No** | The carry limit is the lever (§4.4). Two levers on one axis is two things to tune and one to forget |

**On cooldowns, in full**, because "shared cooldown groups are the standard defence against spam" is
true and the temptation to build one here is real.

A cooldown guards a door. **v1 has no door**: a consumable is used at a menu, before a run, and the carry
limit caps how many can be used at all. A cooldown on a once-per-run menu action is a clock that never
ticks. Building one would be a third cooldown mechanism in a tree that already has two:

| Mechanism | Where | Shape |
|---|---|---|
| `icd_key` | [definitions.md](../effect-atom/definitions.md) §14.1 | Compile-time grouping key — atoms sharing one compile into a single grant, so they share one clock. This *is* "shared cooldown groups", implemented at the atom layer |
| `cooldown_class` / `cooldown_key` / `cooldown_ticks` | [action-map.md](../action-map.md) §4a, `rpg_action` | The action layer's, unbuilt, and explicitly designed for exactly this |

So the decision is: **when in-combat use arrives, the shared cooldown group is `rpg_action.cooldown_key`,
and `consumable_def.cooldown_key` names it.** The column is authored now and inert, for the same reason
`MinRange`/`MaxRange` are authored before the board exists — a cooldown group retrofitted after content
is authored re-prices every row that already shipped.

One warning that belongs here and nowhere else, because it is a live property of the shipped runtime:

> **The grant-lifecycle path bypasses `chance` and ICD.** `EffectBag.FireGrant` short-circuits both
> `PassesOverlayFilters` and `_proc.TryPass` when the trigger is `OnGranted`/`OnRemoved`
> (`src/FusionRpg.Core/Effects/EffectBag.cs:372-388`). A consumable that fires through the lifecycle
> path therefore gets **no chance roll and no internal cooldown**, whatever it authors. So: **a
> consumable may never author `chance` or `icd_ms`** — validated, not trusted (§6.1, `ParamNotHonoured`).

---

## 4. Options considered, and the recommendation

### 4.1 Wait for the action layer, or ship degenerate?

| Option | What it is | Tradeoff |
|---|---|---|
| **(a) Wait** | Author nothing until `A1`–`A5` land | Honest and cheap. But the action program is **blocked on the atom program** (`action-map.md` §8, decision D1), which is itself long. Waiting means consumables miss the standalone game entirely, and the standalone game is the product |
| **(b) Degenerate in combat** — the ideal's suggestion | Self-targeted, instant, no cost, used mid-fight | **Rejected.** There is no combat to use it in. Battle's only opcode consumer cannot be reached by an atom (D6, `AtomKindRegistry.cs:125-128`), and the lawn has no use affordance. This ships a potion that does nothing |
| **(c) Degenerate out of combat** *(chosen)* | Spent at a menu before a run; effect lasts the run | Delivers a real mechanic on shipped rails. Gives up the *feeling* of a clutch heal — which is honest, because v1 has no clutch to heal |

**Recommendation: (c).**

#### The no-migration proof

The claim is: when the action layer lands, absorbing v1 consumables changes **no container row, no atom
row, no instance, and no player's stack.** Here is exactly what it does change.

| Artefact | Before absorption | After | Migrated? |
|---|---|---|---|
| `effect_container` row (`consumable.restore-vital.g1`) | `container_kind = 'consumable'`, `pool_rolls = 0` | identical | **No** |
| `effect_container_atom` rows (the effect) | `resource.delta`, `amount: +250 hp` | identical | **No** |
| `rpg_item_stock` rows (the player's 14 potions) | `(player_id, container_id, qty = 14)` | identical | **No** |
| `material_recipe` row (the cost) | `forge`, output the container | identical | **No** |
| `consumable_def.use_context` | `menu` | `menu,battle` — a **widening** | **No** — adding a context never invalidates an existing one |
| `consumable_def.grants_action_id` | `NULL` | `action.quaff-restorative` | **No** — `NULL` stays legal and means "menu only" |
| `rpg_action` row | does not exist | **new row**, `container_id` FK pointing at the *same, unchanged* container | New, additive |

The absorption is **one UPDATE on two nullable columns and one INSERT into a table that does not exist
yet.** Nothing is rewritten because the effect was never encoded in the use path — the container held it
from day one (§2.5).

What the absorption genuinely *adds*, and could not have been faked earlier: a target rule (v1 has one
target — the squad), a resource cost (v1's only cost is the item), a usability condition (v1's is "do you
hold one"), an envelope (v1 is instant at a menu), and a cooldown (v1 has none). Each is a column on
`rpg_action`, on the new row, beside the old data. **That is what "absorbs without a migration" means,
spelled out.**

#### The one thing that is not free

The **fire point** is not free, and pretending otherwise would be the dishonest part of this proof. See
§4.2 — it needs one reviewed change to the atom layer, and that change is needed by the action layer
anyway.

### 4.2 What makes an instant consumable fire? — the trigger problem

This is the lane's hardest finding, and it is verified in code rather than argued.

**An instant consumable has no trigger it may legally name.** `resource.delta` — the heal — is allowed
exactly five triggers: `OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`, `OnTimer`
(`AtomKindRegistry.cs:19-21`, applied at `:129`), and `AtomKindRegistry.ValidateTrigger` rejects a null
trigger with `UnknownTrigger` (`:71-72`). A potion is *"fires once, now, because the player said so"*,
which is none of them. And the lifecycle pair is explicitly non-authorable:

> *"Grant attach / detach. These are **runtime lifecycle states, not authorable triggers**
> (definitions.md §14.2) … Letting content name only the `OnGranted` half was how a permanent buff could
> leak, so no kind carries these."*
> — `src/FusionRpg.Core/Effects/Atoms/AtomKind.cs:76-82`

Meanwhile — and this is the part that makes the request cheap — **the runtime already does the right
thing.** `EffectBag.Grant` fires every one of a def's actions immediately when the def is
`EffectType = Passive` or names `OnGranted` (`EffectBag.cs:194-204`), and that dispatch reaches the
`ApplyResourceDelta` branch like any other (`:417`). The capability ships. Only the schema forbids
reaching it.

| Option | Shape | Tradeoff |
|---|---|---|
| **(a) An eighth trigger, `OnUse`** *(chosen)* | One entry in `AtomTriggers.All`; allowed on the kinds whose executor is reachable from a grant; **not** allowed on `stat.modify` / `stat.derived`, which stay triggerless permanents. `E7` compiles an `OnUse` atom as `EffectType = Passive`, granted then withdrawn in one transaction | Breaks SC2's closed 7 — so it goes up as a **named request with a reason**, exactly as SC2 instructs. In exchange the fire point is explicit, indexable, auditable, and priceable (`power_trigger_frequency` gets a row) |
| **(b) Triggerless, compiled `Passive`** | Author a `resource.delta` with no trigger; let `container_kind = 'consumable'` carry the meaning | No new trigger. **Rejected:** it gives "no trigger" a second, opposite meaning. Today "no trigger" means *permanent modifier, never expires* (definitions §14.2). Under (b) it would also mean *fires once and is gone*. Two opposite lifetimes on one encoding, distinguished only by a column on a different table, is exactly the ambiguity §14.2 was written to close |
| **(c) Reuse `OnTimer` with a zero delay** | Author `OnTimer` and fire immediately | **Rejected outright.** `OnTimer` is the injector's hot-loop ms scheduler. It exists only where the injector exists, so this would make every consumable require the game to be running — a direct SC8 violation |

**Recommendation: (a).** Written up as request #1 in §9, with the code cites, because SC2 says a
thirteenth kind is a named request and not an assumption — and the same discipline is owed for an eighth
trigger.

The action layer needs this too: an action's container must distinguish *the atoms that fire when the
action resolves* from *the atoms that are permanent modifiers on the actor holding it*. Making that
distinction the atom's own trigger is cheaper than a second `rpg_action_effect_scope`-shaped table.

### 4.3 Where a consumable is held

| Option | What it is | Tradeoff |
|---|---|---|
| **(a) The `girdle` equip role** | The brief's candidate holder | **Rejected, and note the role was renamed.** `I2` shortened `girdle-resource` to `girdle` (`ssot-equip-slots.md:117-119`) and gave it a **60‰** budget for the five resource pools and the economy families (`:91`). Putting a potion there costs the player an affix source to carry it — a trade nobody takes, which kills the role and the consumable together |
| **(b) A quickbar** | N dedicated in-combat slots | **Rejected for v1.** A quickbar is a battle-mode UI, and battle-mode use is deferred. Building the holder before the use is the "mandatory rotation" failure with none of the payoff. It is also a *fourth* meaning of "slot" in a program whose contract opens by cutting three (contract §1) |
| **(c) Bag only, plus a per-run draught manifest** *(chosen)* | The stack lives in `rpg_item_stock`. At dispatch the player names up to **N** consumable stacks; they are spent immediately and recorded | No new storage, no new slot vocabulary, and the carry number is one integer |

**Recommendation: (c). `N = 2`** (illustrative and unverified — it is the number the owner should move).

Why 2 is the actual balance lever, and why it is not arbitrary: it caps the pre-run tilt at a knowable
fraction of a geared actor's contribution, and it is the only number in this design that can make
consumables dominant or irrelevant. Everything else — grades, magnitudes, costs — moves the tilt
linearly. `N` moves it multiplicatively and sets the ceiling.

**How the manifest reaches an actor: it mirrors `I10` exactly, deliberately.** Charms snapshot at run
start and bind at `player:{id}` with `source = 'charm'`, `slot = NULL`, `priority = -100`
(`ssot-charms.md:319-328`). Draughts do the same with `source = 'draught'`. One snapshot mechanism, two
sources — not two mechanisms. §9 item 10 makes that a shared dependency rather than a coincidence.

### 4.4 The spam problem

The failure is real and universal: *"the player can chug forty potions."* Four defences exist; this lane
picks three and rejects one.

| Defence | Verdict | Reasoning |
|---|---|---|
| **Carry limit** — `N = 2` per run, refused at the gate | **Primary** | Structural, not a nudge. `DraughtLimitExceeded` refuses the dispatch, and there is no path around it because the manifest is an input to the sealed run |
| **One per exclusion group** | **Secondary** | Two fire-power draughts do not stack. This reuses the *shipped* rule — `group` defaults to `(family_id, variant)` (definitions §4), the same rule that stops an item rolling `+10 atk / +12 atk`. Zero new machinery, and it forces breadth over depth |
| **Scarcity through a competing sink** | **Tertiary** | A consumable costs `catalyst.forge`, which is also what crafting a base type and boring a socket cost (`ssot-materials-crafting.md:117`). Potions and gear compete for the same verb. That competition is the price signal |
| **Cooldowns** | **Rejected for v1** | §3.3 — a cooldown on a once-per-run menu action is a clock that never ticks. Adopted wholesale from `rpg_action.cooldown_key` when in-combat use lands |

**And the opposite failure, which is the one loot games actually lose to.** "Save it for the boss that
never comes" is more common than spam and much harder to notice. Three properties in this design prevent
it, and they are properties, not hopes:

1. **A draught is spent at dispatch, not held.** There is no inventory screen where an unused potion
   accumulates guilt — the decision is made at the moment you were already making decisions.
2. **The ceiling is deliberately low.** An authoring budget of **≤ 10%** of a fully-geared actor's
   contribution (illustrative, unverified) means no draught is ever the right answer to a hard fight. If
   the correct play is never "save it", the hoarding instinct has nothing to feed on.
3. **Supply is high and the sink is cheap.** Recipes output a **batch** (`output_qty = 5`, illustrative),
   funded by the grade of substrate `I9` explicitly calls *"the cheap class, the one a player should
   never hesitate to spend"* (`ssot-materials-crafting.md:302`).

The combination is deliberate: **low ceiling, high supply.** A consumable should feel like the free
choice, not the precious one. Games that inverted this (Final Fantasy's Megalixir — recalled,
**unverified**) produce players who finish with an unspent inventory, which is a designed mechanic that
never ran.

### 4.5 Timed buffs — does the atom layer have a binding with a lifetime?

**Checked, in code. It does not.**

| Where a lifetime would live | What is actually there |
|---|---|
| `effect_binding` | Columns are `binding_id` · `instance_id` · `owner_kind`/`owner_key` · `slot` · `priority` · `source` · `bound_utc` · `revision` ([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md):36-44). **No expiry, no duration, no until-tick** |
| `EffectGrantDto` | `GrantId` · `EffectId` · `OwnerKind` · `OwnerKey` · `PluginId` · `Priority` · `Overlay` (`src/FusionRpg.Core/Effects/EffectGrantSession.cs:49-62`). **No duration** |
| Withdrawal | Explicit and manual — `EffectBag.Withdraw(grantId)` (`EffectBag.cs:209-227`). Nothing expires on its own |
| Durable runtime state | *"ICD clocks, stacks, counters, charges … live in **session memory** … **No new durable runtime table**"* (`spec-instance-and-binding.md:78-80`) |

**What does have a real clock, and there are exactly two:**

1. **`StatusRuntime`** — `DurationMs` (default 5000), `StatusIcdMs`, stacking modes, resistance
   (`src/FusionRpg.Core/Status/StatusRuntime.cs:45-46`, `:165-192`, `:261-266`). Shipped and working.
2. **`BattleInnateShield.DurationMs`** — integer ms at the content boundary
   (`src/FusionRpg.Core/Battle/BattleModels.cs:31-35`). Shipped, and the reason the `ward` class can be
   authored in v1 while `draught` cannot have a ms lifetime.

**So the conclusion is:** a timed buff must be a **status**, not a timed binding. And the status payload
it needs — *a status whose effect is a container of atoms* — is declared and dead.
`StatusPayloadKind.ModifyStat` is carried by `rally`, `expose`, `command`, and `shatter`
(`src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:28`, `:30`, `:31`, `:32`) and referenced **nowhere
else under `src/`** — four references, all in the file that declares them. Zero consumers, which is the
`status.expose.*` failure SC7 names.

**What is missing, precisely: one mechanism, and it is already owed to somebody else.** The locked
Resource model requires the identical thing for exhaustion —

> *"expressed as a **status** (reusing `StatusRuntime`'s instances, stacking, resistance, VFX cues, and
> `icd_ms`…) whose debuff is a **container of atoms**, never a hardcoded channel list"*
> — [decisions.md](../decisions.md), *Resource model (2026-08-22)*, **LOCKED**

So this lane is not asking for new machinery. It is asking for the machinery a locked decision already
requires, and asking for it **jointly**, so it gets built once. §9 item 3.

**Until it lands, v1's duration model is the run**, and that needs nothing: bind at run start, withdraw
at run end, exactly as charms already do. A run-scoped buff is a lifetime expressed as a lifecycle rather
than a clock, which is why it works on shipped rails.

### 4.6 The container kind

`effect_container.container_kind` is a closed enum — `item` · `trait` · `skill` · `species-passive` ·
`patron` · `world-buff` — with `container_id` prefixed to match (definitions §1), and SC3 reserves four
additions in advance: `item`, `gem`, `set`, `charm`. `consumable` is not among them.

| Option | Tradeoff |
|---|---|
| **(a) Reuse `item`** | No enum change. **Rejected:** the `item.` prefix carries an equipment contract a consumable satisfies none of — an `item_base_type` row with `frame`/`class_id`/`band`/`socket_capacity` (`ssot-item-categories.md` §5.2), a `slot` role, `I11`'s requirement gate, and an `rpg_item_assignment` row keyed by role. Every one would become "NULL means consumable" — a discriminator by absence, which leaks into every query that forgets to check |
| **(b) Mint `consumable`** *(chosen)* | One enum value, `container_id` prefix `consumable.`, flagged as a reviewed change against `E5` per SC3. The bind gate learns one rule: a `consumable` container has `slot IS NULL` and binds only through the run snapshot |

**Recommendation: (b)**, as the fifth addition after SC3's four reserved values, justified rather than
assumed.

**And the rarity ladder stays closed to it.** A consumable does not roll, so it has no affix count and no
tier window, so it has no rarity in the sense this tree uses the word — rarity *is* affix count plus tier
window (definitions §4). Consumables carry `rarity = NULL`, `pool_rolls = 0`, `min_tier`/`max_tier`
`NULL`, and an authored **grade** instead. This is the same argument `I9` makes for materials
(`ssot-materials-crafting.md:151-158`) and it lands the same way. Registered with `I1` as a "nothing
needed" so they do not budget rungs for it (§9 item 9).

---

## 5. Data shape

Two new tables. Everything else is reused.

### 5.1 Reused, unchanged

| Table | What a consumable puts in it |
|---|---|
| `effect_container` | `container_kind = 'consumable'`; `container_id` prefixed `consumable.`; `slot` **NULL**; `rarity` **NULL**; `min_tier`/`max_tier` **NULL**; `pool_rolls` **0**; `level_req` enforced at the use gate with the existing `LevelTooLow`; `tags_json`, `enabled`, `revision` as-is |
| `effect_container_atom` | The whole effect. Fixed core only, `seq` 0-based. This is the invariant the no-migration proof rests on (§4.1) |
| `effect_container_pool` | **Never a row.** A consumable does not roll |
| `effect_binding` | One row per draught at run start: `owner_kind = 'player'`, `slot = NULL`, `source = 'draught'`. Mirrors `I10`'s charm binding shape |
| `rpg_item_stock` (I13) | `(player_id, container_id, qty)` — the bag row *and* the charge counter. `ssot-inventory.md:259` already scopes it to hold "unrolled consumables" |
| `material_recipe` / `material_recipe_cost` (I9) | `operation = 'forge'`, `output_kind = 'container'`, `output_ref` = the consumable container, `output_qty > 1`. `output_qty INT ≥ 1` is already legal (`ssot-materials-crafting.md:319`) |
| `item_category` (I3) | The `consumable` row exists and correctly says *do not author* (`ssot-item-categories.md:208`, `:229`). This lane is what flips its `consumer` column to non-empty |

### 5.2 `consumable_def` — the declaration, 1:1 on the container

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT PK, FK → `effect_container(container_id)` | must have `container_kind = 'consumable'` |
| `class_id` | TEXT NOT NULL | **closed enum, six values**: `restore` `draught` `ward` `board` `revive` `utility`. Not content — each needs an executor, so adding one is code (SC7) |
| `use_context` | TEXT NOT NULL | **closed set, comma-joined**: `menu` · `dispatch` · `battle` · `lawn`. v1 authors `menu`, `dispatch` and — since **2026-09-05** — `battle` (§9 item 5(b); the leaf reads the precondition, `IStockLedger` takes the stack at commit). `lawn` alone is still refused. Widening is additive and never invalidates a row (§4.1) |
| `grade` | INT NOT NULL | 1–5, the strength axis. **Must equal the tier of every atom in the core** — the same band-consistency rule `I3` applies to base types |
| `exclusion_group` | TEXT NOT NULL | The one-per-run key. Defaults to the container's dominant `(family_id, variant)`, which is the shipped `group` default (definitions §4) |
| `manifest_cost` | INT NOT NULL DEFAULT 1 | How many of the `N` manifest places this consumable occupies. Lets a strong draught cost both places without a second table |
| `grants_action_id` | TEXT NULL | **The seam.** Same column name `I2` declared for weapons (`ssot-equip-slots.md:204`). `NULL` means menu/dispatch only. Opaque to this lane |
| `cooldown_key` | TEXT NULL | Reserved for `rpg_action.cooldown_key`. Inert in v1, authored now because a cooldown group is not retrofittable (§3.3) |
| `enabled`, `revision` | INT | joins the `E8` content hash |

**SC7 — the named consumer.** `ConsumableCatalog` (Core, pure): loads and validates at startup, and
exposes `Resolve(containerId) → ConsumableDef` plus `GateManifest(playerId, entries) → Rejection[]`. It
is called by the dispatch endpoint and by the squad builder. Without it these rows are
`status.expose.*` — registered, valid, hashed, and read by nobody.

**Is this data or code?** The table is data — adding a `restore` row ships a new potion with no new code.
`class_id` is code, because each class names an executor. That split is the whole reason the column is a
closed enum rather than free text.

### 5.3 `rpg_run_draught` — what a run consumed

| Column | Type | Notes |
|---|---|---|
| `run_kind` | TEXT NOT NULL | `expedition` \| `battle` — PK part |
| `run_id` | INTEGER NOT NULL | PK part; `rpg_expeditions.id` today (`src/FusionRpg.Data/Sqlite/RpgStore.cs:499-511`) |
| `seq` | INT NOT NULL | PK part. Stable order for the sealed snapshot and for display |
| `container_id` | TEXT NOT NULL | FK → `effect_container` |
| `qty` | INT NOT NULL | ≥ 1 |
| `consumed_utc` | TEXT NOT NULL | |

PK `(run_kind, run_id, seq)`. **Consumer:** the squad builder at run start, and the audit view.

**Why a table and not a JSON column on `rpg_expeditions`.** The expedition's outcome is sealed at
dispatch from `(tier, squad, seed, elapsedTicks)`; a draught changes the squad, so it must be part of the
sealed input, and a sealed input needs a stable row order for the snapshot to be reproducible. `seq`
gives that. Folding it into `squad_json` would hide a determinism input inside a blob.

**Spent, not reserved.** Rows are written in the same transaction that decrements `rpg_item_stock`.
Recall pro-rates rewards to completed ticks ([standalone-rpg-map.md](../standalone-rpg-map.md):20) and
**does not refund draughts** — otherwise dispatch-and-instantly-recall is a free buff preview.

### 5.4 How a draught reaches an actor — and why it is a projection, not a binding

The scopes are seven: `match` · `plant:N` · `zombie:N` · `entity:hex` · `player:id` · `sector:id` ·
`slot:id` (definitions §6). **There is no `actor:{instanceId}`** — which is exactly the question debate
**D1** in the reconciliation plan exists to settle. So:

| Draught targeting | v1 mechanism |
|---|---|
| Player-wide | An `effect_binding` at `player:{id}`, `source = 'draught'`. Shipped road, mirrors `I10` |
| Per-specimen | **Not a binding.** A `BattleChannelMod` contributed to that member's `BattleActorSetup` at squad build |

The second is not a workaround — it is the road already built for exactly this. `BattleActorSetup`
carries `ChannelMods`, documented as *"Additive derived-channel adjustments (trait stat mods, **equipment
later**)"* (`src/FusionRpg.Core/Battle/BattleModels.cs:20-21`), plus `InitialStatuses` (`:23-24`) and
`InnateShield` (`:26-27`). The expedition resolver already uses that road for injuries: `ApplyInjuries`
appends a `BattleChannelMod` to each victim before the battles resolve
(`src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:236-248`).

A draught is the same transform with the opposite sign. **That is the whole v1 runtime.**

### 5.5 Deliberately not created

- **A quickbar table.** No in-combat use, so no in-combat holder (§4.3).
- **A charges table.** The stack is the charge counter (§3.3).
- **A cooldown table.** Two mechanisms exist already; a third would be a third (§3.3).
- **A consumable instance table.** Consumables do not roll, so there is nothing to freeze — no
  `effect_instance` row, no `roll_seed`, no SC5 mutation question. This is the one lane in the program
  that SC5 does not strain, and that is worth stating rather than leaving as an absence.

---

## 6. Validation and reason codes

Both phases reject, per definitions §10: **import is all-or-nothing**, **load is per-row**. Use-time is a
third surface and rejects per action, with the reason shown to the player.

### 6.1 Reused codes

| Bad input | Reason code | Phase |
|---|---|---|
| `container_id` prefix is not `consumable.` while `container_kind = 'consumable'` | `IdMismatch` | import |
| `grants_action_id` names an action that does not exist | `UnknownContainer` | import (once `rpg_action` exists) |
| A `consumable` container the catalog does not know | `UnknownContainer` | use |
| An atom naming a trigger its kind forbids — **what an instant consumable hits today**, §4.2 | `TriggerNotAllowed` | import |
| `OnUse` authored before request #1 lands | `UnknownTrigger` | import |
| A consumable authoring `chance` or `icd_ms` — the lifecycle path honours neither (`EffectBag.cs:372-388`) | `ParamNotHonoured` | import |
| An atom whose kind is `None` in the target runtime — e.g. any `resource.delta` used **in battle** (D6) | `RuntimeUnsupported` | use |
| `stat.modify` on `defense` at any scope but `match` — a "potion of iron skin" cannot be per-actor (**G8**) | `ScopeUnsupported` | import |
| `level_req` set and the player is below it | `LevelTooLow` | use |
| An atom beneath a held stack is disabled by an import | `StaleInstance` | use |
| `qty ≤ 0`, `grade` outside 1–5, `manifest_cost < 1` | `BadParamValue` | import |
| Two atoms in one container with the same `seq` | `DuplicateSeq` | import |
| The same atom twice in one container | `DuplicateAtomInContainer` | import |

### 6.2 Proposed new codes — four

SC6 says propose in a table rather than stretch an existing code past its meaning. Four, kept small
deliberately; each names a distinct player-visible refusal.

| # | Code | Fires when | Why not an existing code |
|---|---|---|---|
| 1 | **`ConsumableRolls`** | A `consumable` container declares `pool_rolls > 0`, a `min_tier`/`max_tier` window, or a non-NULL `rarity` | `UnsatisfiablePool` means the pool cannot be drawn from. This is the opposite: a pool that must not exist at all. Conflating them makes the operator message actively misleading |
| 2 | **`DraughtLimitExceeded`** | The manifest's summed `manifest_cost` exceeds `N` | This is the mechanic's primary refusal and a player sees it constantly. `BadParamValue` would name the input, not the rule. `I10` argues the same for `CharmBudgetExceeded` (`ssot-charms.md:436`) |
| 3 | **`DraughtFamilyConflict`** | Two manifest entries share an `exclusion_group` | `DuplicateAtomInContainer` is an *authoring* collision inside one container. This is a *player* collision across two containers at the gate |
| 4 | **`UseContextUnsupported`** | A consumable is used in a context its `use_context` does not name, **or** in a context the host cannot serve — ~~`battle` before the action layer~~ (served since 2026-09-05, §9 item 5(b)), `lawn` with no injector **and** not bindable at all per `spec-usability-conditions.md` §3a's mode matrix | `RuntimeUnsupported` is about a *kind's* executor. This is about a *use site*. A player told "runtime unsupported" for "you cannot drink this mid-fight yet" learns nothing |

### 6.3 Startup and property validation

| Property | Checked at | Code |
|---|---|---|
| Every `consumable_def.container_id` resolves to a `consumable` container | catalog load | `UnknownContainer` |
| Every `consumable` container has a `consumable_def` row — **an orphan container is not usable content** | catalog load | `UnknownContainer` |
| `grade` equals the tier of every core atom | catalog load | `BadParamValue` |
| `class_id` and every `use_context` token are inside their closed sets | catalog load | `BadParamValue` |
| Every atom in the core is legal in **every** runtime named by `use_context` | catalog load | `RuntimeUnsupported` |
| `pool_rolls = 0` and `slot IS NULL` on every `consumable` container | catalog load | `ConsumableRolls` / `BadParamValue` |
| A recipe whose `output_ref` is a `consumable` container spends no `shard.{band}` | catalog load (I9's surface) | `CostClassForbidden` (I9's proposed code) |
| Manifest at dispatch: summed `manifest_cost ≤ N`; no repeated `exclusion_group`; every stack has `qty ≥ 1` | dispatch gate | `DraughtLimitExceeded` / `DraughtFamilyConflict` / `BadParamValue` |

---

## 7. Worked examples

**All numbers are illustrative, not balanced.** Units are stated on every one, per SC4.

### 7.1 Lesser Restorative — the `restore` class

| Field | Value |
|---|---|
| `container_id` | `consumable.restore-vital.g1` |
| `container_kind` / `pool_rolls` / `rarity` | `consumable` / `0` / NULL |
| `class_id` / `use_context` / `grade` | `restore` / `menu` / `1` |
| Core atom, `seq 0` | `resource.delta` · `amount` Fixed **+250** (hp, **game units**) · `target` self · trigger **`OnUse`** |
| `exclusion_group` | `atom.vitality\|` |
| `manifest_cost` | 1 |
| `grants_action_id` / `cooldown_key` | NULL / NULL |

**What runs today:** on the lawn, grant → the bag fires the action through the `Passive` lifecycle path
(`EffectBag.cs:194-204`, dispatching to `:417`) → withdraw. A verified path, not a proposal.
**What does not:** in battle, `resource.delta` binds and does nothing (D6). Hence `use_context = menu`
and not `battle` — validation refuses the lie rather than shipping it.

### 7.2 Ember Draught — the `draught` class, and one honest wrinkle

| Field | Value |
|---|---|
| `container_id` | `consumable.draught-fire-power.g3` |
| `class_id` / `use_context` / `grade` | `draught` / `dispatch` / `3` |
| Core atom, `seq 0` | `stat.derived` · `channel` `combat.power.fire` · `op` `Flat` · `amount` **+120** (**resolver points**) · **no trigger** — a permanent modifier for as long as it is bound (definitions §14.2) |
| Lifetime | **the run.** Bound at dispatch, withdrawn at collection |
| `exclusion_group` | `atom.elemental-power\|fire` — so two fire draughts refuse with `DraughtFamilyConflict` |
| `manifest_cost` | 1 |

**Calibration for +120 points:** `critical-hunter` grants **+150** crit-rate points, moving crit from
~7.6% to ~26.9% (definitions §2). So +120 on a power channel is a real but not decisive tilt — roughly
the intended ≤10% ceiling (§4.4).

**The wrinkle, stated rather than hidden.** `stat.derived` is quarantined `None/None/None` (defect D6)
and **binds nowhere** until `E12` ships the first consumer (`atom-catalog-ssot.md` §2). So the *binding*
road is dead for this atom today. The *projection* road is not: the squad builder reads the draught and
emits `BattleChannelMod("combat.power.fire", 120)` on each member's `BattleActorSetup`
(`BattleModels.cs:20-21`) — the same road `ApplyInjuries` uses (`ExpeditionResolver.cs:243-247`).

So this consumable **works in v1 and becomes a binding when E12 lands**, with no content change. That is
the no-migration property holding under a real defect rather than in the abstract.

### 7.3 Bulwark Tonic — the `ward` class, and a declared SC1 deviation

| Field | Value |
|---|---|
| `container_id` | `consumable.ward-omni.g2` |
| `class_id` / `use_context` / `grade` | `ward` / `dispatch` / `2` |
| Effect | `BattleInnateShield(BaseHp: 400, Element: null, Priority: 10, DurationMs: 20000)` — **400 hp** of absorption, **20 000 ms** |
| `manifest_cost` | 1 |

**SC1 deviation, declared as the contract requires.** This one is **not a container of atoms** in v1. The
atom that should carry it is `shield.grant` (kind #7), and `shield.grant`'s battle runtime support is
**`None`** (`atom-catalog-ssot.md` §2) — `ExecGrantShield` needs `Bag.ShieldGate`, set only by
`FoundationHarness` and the injector's `EffectRuntime`. The atom road is dead in exactly the mode this
consumable serves, while the setup road ships (`BattleModels.cs:26-27`, `:31-35`).

I take the shipped road and record the deviation rather than authoring an atom that binds to nothing. The
fix is the one line of wiring the atom catalog already names, and it is §9 item 12(b).

### 7.4 Storm Flask — a `board` consumable, refused

| Field | Value |
|---|---|
| `container_id` | `consumable.board-lightning.g2` |
| `class_id` / `use_context` | `board` / `lawn` |
| Core atom, `seq 0` | `board.action` · trigger `OnUse` |

**Authoring this today rejects `UseContextUnsupported`**, and it should. There is no overlay use
affordance, and `capPerMatch` — the only thing that would bound how many the player throws — is in the
FA9 allowlist with no implementation anywhere (**G4**, `AtomKindRegistry.cs:138-140`).

This example is here because it is the consumable a PvZ player most wants, and because "reject, never
ignore" (SC6) is easier to state than to demonstrate. The row is refusable, the reason is nameable, and
nothing silently does nothing.

### 7.5 A cost, in I9's vocabulary

Forging one batch of **Lesser Restorative** (§7.1):

| Line | Class | Qty |
|---|---|---|
| `catalyst.forge` | catalyst — the **verb** (make) | **1** |
| `substrate.humanoid.crude` | substrate — the **body** | **2** |
| souls | permission — the flat fee | **25** |
| *(no `shard.{band}`)* | ceiling | — a consumable has no rarity ceiling to buy (§4.6) |
| *(no `essence.{element}`)* | direction | — this one is elementless. Ember Draught (§7.2) adds `essence.fire` × **1** |

`operation = 'forge'`, `output_kind = 'container'`, `output_ref = 'consumable.restore-vital.g1'`,
**`output_qty = 5`**. Batch output is what makes §4.4's "high supply, low ceiling" true, and
`output_qty INT ≥ 1` is already legal in `I9`'s schema (`ssot-materials-crafting.md:319`).

**The sink this creates:** `catalyst.forge` is capped and never salvage-recoverable
(`ssot-materials-crafting.md:298`, `:493`), so every potion batch is a batch of gear not forged. That
competition is the economy answer to spam, and it costs no new currency to express.

---

## 8. Failure modes

Specific, and each paired with the thing in this design that prevents it — not with a hope.

| # | Failure | Where it shipped | What prevents it here |
|---|---|---|---|
| 1 | **Potion spam trivialises combat.** The player carries 40 and chugs through every hard fight | Diablo 2 pre-runeword belts; most survival games (recalled, **unverified**) | Three structural limits, none of them a clock: `N = 2` per run refused at the gate (`DraughtLimitExceeded`), one-per-`exclusion_group` (`DraughtFamilyConflict`), and no in-combat use at all in v1. You cannot chug what you cannot reach mid-fight |
| 2 | **"Save it for the boss that never comes."** Players finish with a full inventory of unused elixirs | Final Fantasy's Megalixir; most JRPGs (recalled, **unverified**) | Low ceiling (≤10%, §4.4) so saving is never correct; spend-at-dispatch so the decision happens where decisions already happen; batch recipes so supply is never the constraint |
| 3 | **The quickbar becomes mandatory rotation.** What was a safety net becomes a damage button pressed on cooldown | WoW-era potion and trinket macros (recalled, **unverified**) | No quickbar exists — §4.3(b), rejected explicitly. When in-combat use arrives it arrives as an *action*, priced by the action layer's cost and cooldown model, so it competes with every other action for the same clock rather than being a free extra press |
| 4 | **Consumables duplicate what gear already does.** A `+atk` potion is just a worse weapon | Endemic | Two cuts. The exclusion group is `(family_id, variant)` — the same key gear rolls on — so a draught and an affix on one family do not stack, they collide visibly. And the classes only consumables do (`ward` with a real ms clock, `restore`, `revive`) have no gear equivalent |
| 5 | **The invisible nerf.** A consumable quietly does nothing because its atom is dead in the runtime it was used in | This tree, three times: `status.expose.*`, the eight inert statuses, `StatusPayloadKind.ModifyStat` | Validation refuses at catalog load: every atom must be legal in **every** runtime the `use_context` names (§6.3). §7.1 and §7.3 both show that check changing what gets authored |
| 6 | **The chance/ICD trap.** An author writes `chance: 500` on a potion and it fires 100% of the time | Live in the shipped runtime — `EffectBag.cs:372-388` bypasses both on the lifecycle path | `ParamNotHonoured` at import. The rule is enforced, not documented |
| 7 | **The sealed-run exploit.** Dispatch with draughts, peek at the outcome, recall, get the draughts back | Any game with a preview and a refund | Draughts are **spent** at dispatch, in the same transaction that decrements the stack; recall pro-rates rewards and refunds nothing (§5.3) |
| 8 | **Catalog drift.** An import disables an atom under 40 stacks of a potion the player already owns | `StaleInstance` exists for exactly this at the instance layer | Use-time `StaleInstance`. **But stock rows have no `stale` flag** — `rpg_item.stale` is on the rolled-instance table (`ssot-inventory.md` §4.2) and `rpg_item_stock` has four columns, none of them it (§4.3). A real hole; §9 item 6 |
| 9 | **A second scheduler.** The consumable system grows a timer, then a cooldown, then a queue, and becomes a shadow action layer | Endemic to this exact seam | v1 has no timer (run-scoped lifetimes), no cooldown (§3.3), and no queue (menu use). Each is refused by name, with the lane that owns it named too |

---

## 9. What this lane needs from other lanes

Twelve items. Items 1–3 are the ones without which v1 does not exist.

1. **`E1` (`atom-kind-registry`) — an eighth trigger, `OnUse`. An SC2 named request, not an assumption.**
   Rationale in §4.2. The precise ask: add `OnUse` to `AtomTriggers.All` (`AtomKind.cs:70-71`); allow it
   on the kinds whose executor is reachable from a grant; **forbid** it on `stat.modify` and
   `stat.derived`, which stay triggerless permanents so §14.2's invariant is untouched; and have `E7`
   compile an `OnUse` atom as `EffectType = Passive`, granted and withdrawn in one transaction — the path
   `EffectBag.Grant` already runs (`EffectBag.cs:194-204`).
   **The argument for cheapness: the runtime already does this; only the schema forbids it.** The action
   layer needs the same distinction to tell an action's on-resolve atoms from its passive ones, so this is
   one change serving two programs. *If it is refused*, v1's fallback is option (b) in §4.2 — a
   triggerless `resource.delta` under a `consumable` container — and this lane will take it, but it
   overloads "no trigger" with two opposite lifetimes and should be refused on those grounds.

2. **`E5` (container schema) — the `consumable` `container_kind`.** The fifth addition after SC3's four
   reserved values, with prefix `consumable.`, `slot IS NULL`, `pool_rolls = 0`. Justification in §4.6,
   flagged as a reviewed change per SC3.

3. **`E12` / the status stream — a status whose payload is a container of atoms.** §4.5. This is
   `StatusPayloadKind.ModifyStat` given a consumer; it has four references, all in
   `StatusCatalogBootstrap.cs:28`, `:30`, `:31`, `:32`, and none anywhere else under `src/`.
   **Ask it jointly with the Resource model**, which already requires the identical mechanism for
   exhaustion debuffs (`decisions.md`, *Resource model (2026-08-22)*). Two lanes need one thing; it should
   be built once. Until it exists, timed-in-ms buffs are not authorable and v1's draughts are run-scoped.

4. **`R2`/`D1` (durable ownership debate) — the outcome decides whether a per-specimen draught is a
   binding or a projection.** Today there is no `actor:{instanceId}` scope (definitions §6), so §5.4
   routes per-specimen draughts through `BattleActorSetup.ChannelMods`. If D1 adds the scope, the
   projection becomes a binding and this lane's *data shape does not change* — only the run-start code
   path does. Recorded so D1 knows it has a second consumer.

5. **The action program (`A1`, `A3`, `A4`) — the seam, and three things it must widen.**
   - **(a)** `consumable_def.grants_action_id` and `cooldown_key` are the whole seam from this side.
     `A1` owns what they mean. This lane asserts only that the columns are legal on a `consumable`
     container and that `NULL` means menu/dispatch use.
   - **(b) `A3`'s cost model has no shape for this.** `rpg_action_cost` is
     `(action_id, resource_id, amount_spec, when)` (`action-map.md` §4a), priced against the five locked
     actor resources. **A consumable's cost is an item, which is not a resource.** `A3` must either widen
     `resource_id` to admit an item stock row, or state that consuming the item is a *precondition*
     (`A4`) rather than a *cost* (`A3`). Either is fine; leaving it unstated means the first consumable
     action has nowhere to declare what it spends.

     > ✅ **ANSWERED 2026-08-27, and BUILT 2026-09-05. This ask was closed for more than a week before
     > anyone annotated it here, and that omission is the whole reason it kept being restated as open.**
     >
     > **The answer: a precondition, not a cost.** `spec-action-costs.md` §8 declines to widen
     > `resource_id` and gives three reasons, the decisive one being that *"costs scale with `Θ` and
     > rungs; an item does not — one potion is one potion at every level."*
     > `spec-usability-conditions.md` §3a takes it from there and states it as settled: *"So consuming
     > the item is a **precondition**, and this module reads it."* Both documents were revised
     > **2026-08-27**.
     >
     > **What shipped:** `LeafId.HoldsStock` — `(stockId, minQty)`, a flat allocation-free probe on
     > `FactReader`, read by gate 5 of `UsabilityEvaluator` (`action-todo.md` T10, done **2026-08-28**).
     >
     > **What was still missing until 2026-09-05, and is the narrower thing this row really tracked:**
     > the leaf only ever **read** a quantity. Nothing decremented a stack when an action gated on it
     > fired, so a `battle` consumable would have been free forever. Closed now —
     > `ActionCompiler` lifts each conjunctive `holdsStock` leaf into `CompiledAction.StockDemands`
     > (the compiled predicate cannot do this itself: it interns the `stockId` away to a 0-3 slot),
     > `ActionStockCommit`/`IStockLedger` spend at commit, and `RpgStore.TrySpendStock` takes the
     > stacks in one transaction through the same conditional decrement `TrySpendDraughts` uses.
     >
     > **Consequence for §5.2:** `use_context = battle` is **authored** as of 2026-09-05
     > (`consumables.v1.json`'s `contextsAuthored`). §6.2 code 4's reason for refusing it —
     > *"`battle` before the action layer"* — no longer holds. **`lawn` stays refused**, on its own
     > two reasons rather than by association with `battle`: `spec-usability-conditions.md` §3a's mode
     > matrix makes a `holdsStock` action **not bindable** in lawn mode at all (the overlay is a
     > stateless observer and never reads current inventory — `ActionCompiler` refuses it by name with
     > `ConsumableUnsupportedInMode`), and `capPerMatch` (**G4**, §9 item 12(a)) is still unimplemented.
   - **(c) `A4`'s usability condition needs a leaf** that reads "do I hold ≥ 1 of this stock row". The
     leaf list is closed at ~8 (`atom-catalog-ssot.md` §8) and none of them reads inventory.

6. **`I13` (inventory) — two things.** *(a)* A `stale` marker on `rpg_item_stock`. The flag exists on
   `rpg_item` for rolled instances (`ssot-inventory.md` §4.2) but `rpg_item_stock` has four columns
   (§4.3) and cannot say "this potion's atom was disabled" — failure mode 8. *(b)* Confirmation that the
   dispatch gate may decrement a stock row inside the dispatch transaction, and that a decrement is
   **not** salvage: it does not write a tombstone and does not enter the undo window.

7. **`I9` (materials and cost) — recipe rows for consumables.** `operation = 'forge'`,
   `output_kind = 'container'`, `output_qty > 1`, spending `catalyst.forge` +
   `substrate.{frame}.crude|sound` + souls, and **never `shard.{band}`** (§4.6, §7.5). Two confirmations
   needed: that a consumable's absent rarity does not break `variant_from = output-band` (these recipes
   simply never use it), and that `CostClassForbidden` can express "no shard on a consumable recipe".

8. **`I3` (categories) — flip the `consumable` row's `consumer` column.** `I3` correctly ships it as
   *"the action layer, unbuilt — do not author"* (`ssot-item-categories.md:229`). That is now wrong in one
   direction: the **menu/dispatch** consumer is `ConsumableCatalog` (§5.2); the **battle/lawn** consumer is
   still unbuilt. The row needs the first. And `stack_intent` should read `qty`, not `charges`
   (`:208`), since v1 has no per-item charges (§3.3).

9. **`I1` (rarity) — nothing, and that is the point.** Consumables carry `rarity = NULL` and never enter
   the ladder (§4.6). Registered here so `I1` does not budget rungs, `material_band` mappings, or affix
   windows for a category that has none. An explicit "nothing needed" is cheaper than a later collision.

10. **`I10` (charms) — one run snapshot, not two.** Both lanes bind at run start at `player:{id}` and
    withdraw at run end by `source`; charms use `source = 'charm'`, draughts `source = 'draught'`
    (`ssot-charms.md:319-328`). **Whoever builds the run-start snapshot first owns it and the other
    adopts it.** Two independent snapshot mechanisms over one run is how ordering bugs are born, and the
    effect list's order is content-derived by contract (definitions §5).

11. **The expedition / standalone stream — the draught manifest is a determinism input.**
    `rpg_run_draught` rows must be written before the seed resolves, must be part of whatever the
    resolution reproduces from, and must survive recall without refund (§5.3). `ExpeditionResolver` is
    pure over `(tier, squad, seed, elapsedTicks)` (`ExpeditionResolver.cs:39-49`); the draught transform
    belongs in the squad it *receives*, beside `ApplyInjuries` (`:236-248`), not inside the resolver.

12. **`E15` (`atom-runner`) and the shield wiring — two later dependencies, named now so they are not
    discovered mid-build.** *(a)* Multi-charge consumables and any real cooldown need per-binding state,
    which is `E15`'s and unbuilt (`effect-atom-map.md`:81) — including `capPerMatch`, without which
    lawn-mode consumables cannot be bounded (**G4**). *(b)* `shield.grant`'s runtime support is `None`
    outside the two hosts that set `Bag.ShieldGate` (`atom-catalog-ssot.md` §2), which is why §7.3 takes
    the setup road. When that wiring lands, the `ward` class becomes a container of atoms and the SC1
    deviation in §7.3 closes.

---

## 10. Open questions for the owner

Six decisions deliberately not made here.

1. **`N` — how many consumables may a player bring into one run?** I propose **2**, and §4.3 argues it is
   the single most consequential number in this design. It is one integer and it should be yours.

2. **Do consumables ever exist in PvZ mode?** I recommend **yes, later**, through the intent/command road
   rather than the action queue (§2.4). The alternative — consumables are standalone-only, forever — is
   coherent and cheaper, and it keeps PvZ mode purely an observer, which is what the locked action
   decision already says it is. The reason to say yes anyway is that throwing something at a lawn is the
   most PvZ-native thing a consumable could do.

3. **Permanent stat-ups: refused as consumables, allowed as quest rewards.** §3.2. Confirm, or overrule —
   if you want farmable permanent stat-ups, the objection in §3.2(1) becomes a real design problem and the
   answer is probably a progression system rather than an item.

4. **Are draughts per-squad or per-specimen?** v1 is **per-squad**, because there is no actor scope
   (§5.4). Per-specimen is expressible today through `ChannelMods` and would make the manifest a targeting
   decision rather than a shopping list — richer, and one more thing to explain on a screen the player
   sees every dispatch.

5. **`OnUse` as the eighth trigger** (§4.2, §9 item 1), or the triggerless-`Passive` overload? SC2 puts
   this at your level by design. My recommendation is the trigger; the fallback works and I have stated
   why it is worse.

6. **What is "rest"?** Not this lane's to answer, but this lane, the resource model, and `A3` all wait on
   it (`action-map.md` §10.4a). If a rest point is ever defined, the follow-up here is whether consumables
   refill at it — I recommend **no**, because a refilling consumable is a resource with an item wrapper,
   and this game already has five resources.
