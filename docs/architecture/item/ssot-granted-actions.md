# Lane G4 SSOT — item-granted actions, and the shape of the `grants_action_id` seam

**Status:** Lane G4 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md). Gap lane **G4** of the
[reconciliation plan](reconciliation-plan.md) §R3.

Terminology per the contract §1. **equip slot** and **role** are I2's words; **base type** is I3's.
The word **action** is used only in the [action program](../action-map.md)'s sense — an envelope
(*when*) + a container of atoms (*what*) + a target rule (*who*) + a resource cost + a usability
condition ([decisions.md:90](../decisions.md), locked).

> **This document defines a seam. It does not design the action layer.** Where the line falls is
> stated in §2 and enforced by the "must NOT store" list in §5.3, which is the more load-bearing
> half of this lane's data shape.

---

### Where the brief's ten questions are answered

| # | Question | Section |
|---|---|---|
| 1 | Reference shape — name an id, or carry a definition | §4.1 |
| 2 | Does this reuse `container_kind = 'skill'` | §3.3, §4.2 |
| 3 | May a base type declare a default attack | §4.3 |
| 4 | Lifecycle, including mid-battle | §3.5 |
| 5 | Battle-only, and what that means for the lawn | §3.6 |
| 6 | Stacking and conflict | §3.7 |
| 7 | What the item stores, and what it must not | §5.2, §5.3 |
| 8 | The handshake | §5.5 |
| 9 | v1 scope | §5.6 |
| 10 | Validation and reason codes | §6 |

---

## 2. Scope

### This lane owns

- **How an item declares that it grants an action** — the reference shape, and the refusal of every
  alternative to a reference.
- **What the item side of that contract carries**: the minimum column set, and the explicit list of
  what it may never carry.
- **Lifecycle**: when a grant becomes real, when it stops, and what happens if it stops while the
  turn kernel is mid-action.
- **Stacking and conflict**: two items granting one action, an item granting what the species
  already has, and the cap.
- **The handshake** (§5.5) — the numbered contract the action program implements against. This is
  the document's main product.
- The **validation table and reason codes** for the item side of the seam.

### This lane does NOT own

| Not mine | Whose |
|---|---|
| Activation, wind-up, recovery, resolve offsets, priority band | the **envelope** — `ActionEnvelope` (`src/FusionRpg.Core/Battle/Timeline/ActionEnvelope.cs`), shipped |
| Cooldown class, key, ticks, start point, interrupt charge | the **kernel** + `rpg_action.cooldown_*` ([spec-action-model.md](../action/spec-action-model.md) §1) |
| Target rules, range, anchor, line of sight | **A2 targeting**, `rpg_action` targeting group |
| The resource cost model, the five pools, `when` semantics | **A3 action-costs**, `rpg_action_cost` |
| Usability predicates and their leaves | **A4**, contributing to **E3**'s closed leaf list |
| What an action's atoms are, and which target each hits | **A1**, `effect_container` + `rpg_action_effect_scope` |
| The action-set merge, and where intrinsic actions come from | **A1** §5 — see handshake item 4 |
| Base-type rows, class ladders, implicits, base stats | **I3** |
| Which equip roles exist, and their budgets | **I2** |
| Rolled affixes and the magnitudes a weapon supplies | **I8** / **I3** |
| Uniques as a content class | **G1** (`ssot-uniques.md` — did not exist when this was written) |
| Consumables and their charge model | **G2** (`ssot-consumables.md` — did not exist when this was written) |
| Rendering an action onto item text | **G3** |

**The line, stated once so it can be held:** the item supplies an **identifier and a role**.
Everything that answers *when*, *how much*, *at whom*, or *how often* belongs to the action layer. A
seam that quietly specifies a cooldown has failed, and the way this document fails is by growing a
column — so §5.3 is written as a Never list rather than as prose.

---

## 3. The model

### 3.1 Why the gap exists

I2 declared the seam and deliberately did not design it:

> *"The seam, declared and not designed: I3's base-type record may carry a nullable
> **`grants_action_id`**… Everything about what it means — activation, cost, targeting, cooldown,
> whether unarmed has a default action — is the action layer's, and until it ships **weapons are
> numbers only**."* ([ssot-equip-slots.md:204-207](ssot-equip-slots.md))

That was the right call for I2 and it left a hole, because **I3 never added the column.**
`item_base_type`'s row shape ([ssot-item-categories.md:273-289](ssot-item-categories.md)) has
`frame`, `class_id`, `band`, `socket_capacity`, `implicit_family`, `affix_pool_tag`, `req_json`,
`display_json` — and no action column. So the seam exists in exactly one sentence of one lane doc,
and three lanes need it: weapons that define an attack (I2/I3), uniques whose identity *is* an
ability (G1), and consumables that are an action in item form (G2). Left alone, each would invent
its own shape, and the action program would arrive to find three.

### 3.2 What a grant actually is

An action grant is **membership**, not effect.

Everything else in this folder resolves to *(container → instance → binding → atoms on the actor's
effect list)* — SC1. A granted action does not, and saying so plainly is required by SC1's own
clause about declaring what does not fit.

An action's atoms *do* live in a container, and they *do* resolve through the atom layer — but they
resolve **when the action fires**, applied as a unit at its resolve tick
([spec-action-model.md](../action/spec-action-model.md) §4), not as a standing grant sitting on the
actor's effect list waiting for a trigger. The item does not add an atom to the actor. It adds an
**entry to the actor's list of things it may choose to do**.

That is a different mechanism, and it is the finding SC1 asks for. It is not a second *effect*
mechanism — no modifier bag, no bespoke stat path, no second condition language. It is a
**selection-set membership**, and the action program already owns the concept
([spec-action-model.md](../action/spec-action-model.md) §5: intrinsic vs granted). This lane hands
it a source, not a mechanism.

### 3.3 Reuse of `container_kind = 'skill'` — answered from the shipped schema

`ContainerKind` in code is `Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff`
(`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:7-15`). `rpg_action.container_id` is an FK to
`effect_container` ([spec-action-model.md](../action/spec-action-model.md) §1), and the action map
already verified that this works against `Skill` unchanged:

> *"There is no `Action` kind and there does not need to be: `A1`'s sketch of a separate `rpg_action`
> row carrying a `container_id` FK works against `Skill` unchanged. The dependency on the atom
> program is now zero API surface."* ([action-map.md](../action-map.md) §10.5a)

So the answer is **yes, and it is already decided — but it is not the item's container.**

| Container | Kind | Holds | Authored by |
|---|---|---|---|
| The item's own bonuses | `item` | base stats, implicit, rolled affixes | I3 / I8 |
| The granted action's effects | `skill` | the atoms the action applies when it fires | the **action program** |

**Two containers, two kinds, two owners, and they must never be merged.** Merging them produces one
of two bugs, both real: the item's passive affixes would fire as part of the action, or the action's
damage atom would apply permanently at equip. The `effect_container` schema states the boundary from
its own side — *"never put activation, cooldown, or targeting in these tables"*
([spec-container-schema.md](../effect-atom/spec-container-schema.md), Boundaries) — and this lane
states the mirror image: **never put an action's effects in an item's container.**

**No new `container_kind`.** SC3 reserves four names (`item`, `gem`, `set`, `charm`); this lane asks
for none of them and proposes no fifth.

### 3.4 What a weapon supplies, restated so I2's ruling survives

I2's rule is *"A weapon supplies numbers and legality. It never supplies activation."*
([ssot-equip-slots.md:196](ssot-equip-slots.md)). A reference-shaped seam keeps that true, literally:

- The weapon supplies a **string** — which action.
- The action supplies **every activation property** — cooldown, wind-up, cost, target rule, range.
- The weapon supplies the **numbers the action scales on**, exactly as before: its `stat.derived`
  atoms move the derived channels the action's `ValueSpec` reads
  ([spec-action-model.md](../action/spec-action-model.md) §1: *"every value that scales is a
  `ValueSpec`… so the atom program's `effect_curve` serves actions too"*).

So "a better nozzle" is still numbers. "A *different* nozzle" is a different action id. Two axes,
neither borrowing the other's mechanism. That is the reconciliation, and it is why this seam does
not need to reopen I2's rule — only to tighten one clause of it (§4.3, §9.3).

### 3.5 Lifecycle — and it is shorter than it looks, because mid-battle equip does not exist

**Verified in code before designing against it.** Equipment cannot change mid-run today:

- `UniqueActorService.PutEquipment` refuses unless the actor's phase is `Roster`, returning
  `phase.not_roster` (`src/FusionRpg.Server/UniqueActorService.cs:41-44`). `ClearEquipment` routes
  through the same method (`:60-62`), so unequip is refused on the same gate.
- The runtime doc records it as an intentional state: *"Mid-run ActiveBound equip held"*
  ([unique-actor-runtime.md:243](../unique-actor-runtime.md)), with *"ActiveBound mid-match equip /
  Hot re-push"* listed as still out (`:247`).

So the shipping rule is the cheap one:

> **The actor's granted-action set is assembled at run start and is immutable for the run.**

Same shape I10 chose for charms, for the same two reasons
([ssot-charms.md](ssot-charms.md) §3.8): the RPG and the game are **two async systems** and the RPG
never reads or guesses current game state ([definitions.md:550-556](../effect-atom/definitions.md));
and an expedition's outcome is sealed at dispatch by recorded seed, so a loadout that changes after
the seal makes the seal a lie. Under the battle time model determinism is
`(setup, seed, decision-trace)` ([decisions.md:41](../decisions.md)) — an action set that mutates
mid-run is an unrecorded input.

| Moment | What happens |
|---|---|
| Item acquired | nothing. Owning is free |
| Item equipped (Roster phase) | the grant row becomes eligible. No runtime effect — there is no battle |
| **Run start** (battle build / expedition dispatch) | the equipped set is read once, the action set is assembled (handshake item 4), and it is frozen for the run |
| During the run | the set does not change. Equip is already refused at the service |
| Run end | nothing to withdraw — no binding row was created (§3.2) |

#### What happens if removal ever becomes possible mid-battle

Written now because it is free now and expensive after someone builds mid-match equip.

Removal is **recorded and applied at the next quiescent point for that actor**, and it **never
cancels a committed action.** Against the shipped FSM
(`src/FusionRpg.Core/Battle/Timeline/TurnState.cs:14-24`):

| Actor state at removal | Rule | Why |
|---|---|---|
| `Charging`, `Ready` | the action leaves the selectable set immediately | Nothing has been paid. `IIntentSource.TryDeclare` simply stops offering it (`IntentSource.cs:29-36`) |
| `Committed`, `Resolving` | the run completes: costs stay paid, resolve handles fire, cooldown starts | *"Committing is what costs, not landing"* ([spec-action-model.md](../action/spec-action-model.md) §2). Cancelling here needs a refund path that rule forbids |
| `Recovering` | removal applies at `Recovering → Charging` | The only transition out (`TurnState.cs:42`) |
| `Downed`, `Dead`, `Withdrawn` | removal is recorded and applied if the actor returns | `Downed → Charging` is legal (`TurnState.cs:59`) |

**An inventory event must never become an `InterruptCause`.** The enum is `CrowdControl` and
`Damage` (`src/FusionRpg.Core/Battle/Timeline/ActionRunner.cs:41-45`), and its exit path releases the
slot and charges cooldown. Adding a third cause for "the item left" would put an item-layer concern
inside the kernel's slot accounting — the one place in this repo with a zero-allocation contract and
a byte-identical gate in front of it. This lane refuses it, and asks the timeline program to record
the refusal (§9.10).

**Cooldown survives removal, and the shipped key shape already guarantees it.**
`CooldownLedger` is keyed `CooldownSlot(ActorKey, Slot)` where `Slot` comes from the envelope's
cooldown key, not from the item (`src/FusionRpg.Core/Battle/Timeline/CooldownLedger.cs:8`). So
unequip-then-re-equip does not reset a cooldown. That is the correct behaviour and it closes the
classic swap exploit (§8.4) **for free** — nobody should "fix" it.

**Nothing needs reverting.** An action's atoms are events applied at a resolve tick, not standing
modifiers with an `OnRemoved` half. The apply/revert lifecycle that `stat.modify` and `stat.derived`
carry ([definitions.md](../effect-atom/definitions.md) §14.2) does not apply here, because a granted
action creates no binding.

### 3.6 Battle-only, and the lawn — the honest answer

Locked: *"Actions are a battle-mode concept only — PvZ mode is a stateless observer with no queue and
no per-actor machine, so the lawn never schedules an action."*
([decisions.md:90](../decisions.md)).

**So a weapon that grants an action does nothing on the lawn. That is a real hole, not a rounding
error, and it is worse than it first looks.** The atom kind runtime matrix
(`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs`) says why:

| Kind | Lawn | Battle | Line |
|---|---|---|---|
| `stat.modify` | Full | **None** | `:88` |
| `stat.derived` | None | None | `:106` |
| `resource.delta` | Full | **None** | `:128` |
| `resource.economy` | Full | **None** | `:141` |
| `status.apply` | Full | **Partial** | `:159` |
| `status.clear` | Full | **None** | `:171` |
| `shield.grant` | Full | **None** | `:192` |
| `spawn.entity` | Full | **None** | `:217` |
| `board.action` | Full | **None** | `:227` |
| `grid.spawn` / `grid.clear` / `box.set` | Full | **None** | `:238`, `:246`, `:260` |

Eleven of twelve kinds have **no battle consumer at all**; one is `Partial`. So today the *numbers*
half of a weapon (`stat.modify`, lawn `Full`) works only on the lawn, and the *action* half is
battle-only — **and neither runtime executes both.** The weapon fantasy is currently split across two
runtimes with no overlap. That is worth the owner knowing before any of this is scheduled.

> ⛔ **CORRECTION, 2026-09-05 (module 19, at build time). The matrix above is stale end to end and
> its headline conclusion INVERTS.** Verified against `AtomKindRegistry.cs`, not recalled:
> **five kinds are `Battle = Full`** — `stat.modify` (`:217`), `stat.derived` (`:255`),
> `resource.delta` (`:290`), `status.apply` (`:344`), `shield.grant` (`:396`) — and **no kind is
> `Partial`**. The remaining seven stay `Battle = None`: the **five** `AttachPoint.Board` kinds
> (`spawn.entity` `:403`, `board.action` `:431`, `grid.spawn` `:445`, `grid.clear` `:460`,
> `box.set` `:476`) plus `resource.economy` (`:296`) and `status.clear` (`:358`). Five + five + two
> = the twelve registered kinds.
>
> **So "neither runtime executes both halves" is false.** A weapon's `stat.modify` and `stat.derived`
> atoms compose in battle (`BattleStatModifierLedger`, `BattleStatComposer`) and an action resolves
> there. The lawn gap is unchanged and option **(b)** still stands, but the case for it is now
> *"the lawn has no queue"*, not *"no runtime executes both halves"*. §6.3's anti-silence check keeps
> its teeth for the seven that remain.

Three ways to handle the lawn gap:

| Option | Shape | Verdict |
|---|---|---|
| **(a) Lawn fallback** — the item carries a second, non-action effect used on the lawn | Two behaviours per item, chosen by mode | **Rejected.** It doubles authoring, it is a second content path per item, and it makes an item mean different things in different places — the worst possible outcome for a tooltip |
| **(b) Battle-frame content** — items with granted actions are battle content, and say so | One behaviour, honestly scoped | **Recommended** |
| **(c) Say nothing** | — | This is how `status.expose.*` happened |

**Pick (b), with a display requirement that is part of the pick, not a nicety:** a base type with a
grant row carries a **battle-only** presentation tag, and G3 must render it. An item whose headline
property silently does nothing in the mode the player is standing in is the `status.expose.*`
failure moved to the UI layer.

**SC8 is satisfied, and the asymmetry runs the other way than expected.** SC8 requires every
mechanic to work with the PvZ game closed. Granted actions are battle mechanics and battle is the
standalone runtime — so SC8 is fine. The mode this seam *cannot* reach is the one with a shipped
runtime today.

### 3.7 Stacking and conflict

The unit of truth is the **action set**: a set of `action_id`, assembled per actor at run start.
Grants are **provenance rows**; the set is derived from them by group-by. That single choice answers
all four cases.

**(a) Two items granting the same `action_id`.** One entry in the set. No stacking, no doubled
charges, no halved cooldown. **Decided from shipped code, not preference:** `CooldownLedger` keys
`(ActorKey, CooldownKey)` (`CooldownLedger.cs:8`), so two "instances" of one action would share one
clock regardless. A schema that cannot express two independent instances should not pretend to.
Provenance is kept: removing one of two sources leaves the action, because the other grant row is
still live.

**(b) An item granting an action the species already has.** Dedup — one entry — and the grant is
**reported, not silently swallowed**: G3 renders "already known". The item is not broken, but the
player must be able to tell that this line of its text is doing nothing for *this* actor. Not a
rejection: the same item on a different species is a real upgrade, so refusing the equip would be
wrong.

**(c) The default attack, which is the one case that is a replacement rather than a merge.**
`grant_role = 'default-attack'` deliberately replaces the species' intrinsic basic attack. Precedence
is declared, never emergent:

1. `armament-primary`'s `default-attack`, if any.
2. Otherwise the species' intrinsic basic attack.

There is no third rung, because `default-attack` is **legal only on `armament-primary`** (§4.3). A
two-handed item occupies primary and reserves secondary
([ssot-equip-slots.md](ssot-equip-slots.md) §2.7), so it cannot conflict with itself. An unarmed
actor keeps the species attack — which answers I2's open *"whether unarmed has a default action"*
([ssot-equip-slots.md:206](ssot-equip-slots.md)) with the action program's own rule: *"a default
must never depend on authored data"* ([spec-action-model.md](../action/spec-action-model.md) §5).

**(d) More granted actions than the UI can show.** Cap the `granted` count per actor and **reject at
bind rather than truncate.** Truncation makes an equipped item silently do nothing; a refusal at
equip time is legible and names the item. Proposed cap **8** — illustrative, not balanced, and the
number is the action layer's call (handshake item 8). `default-attack` never counts against it.

Display order must be deterministic and content-derived: `(equip role ordinal, seq, action_id)`,
compared **ordinal**. Never by a generated id — definitions §5 is explicit that `binding_id` is
generated and sorting on it produces different bytes from identical inputs.

Honest cost of the cap: a legitimate build can become unequippable. That is the price of refusing to
silently drop content, and it is the right side of the trade for a game whose scar tissue is a
registered channel with zero readers.

---

## 4. Options considered, and the recommendation

### 4.1 Reference shape: name an action, or carry a definition

| Option | Shape | For | Against |
|---|---|---|---|
| **(A) Name an existing `action_id`** | one TEXT FK | Loose coupling; zero duplication of `rpg_action`'s ~25 columns; one action authored once and referenced by forty items; the item table cannot drift from the action | The action must exist before the item can reference it; an item cannot express "the same skill, tuned" without a second action id |
| **(B) Carry an action definition** | the item row holds envelope + costs + targeting | Maximum expressiveness; an item is self-contained | Duplicates the entire action schema onto item rows; two authoring surfaces for one concept; two validators; two places a cooldown can be wrong. **This is the "second action system" failure by construction** |
| **(C) Name an id, plus per-item overrides** | (A) + `overrides_json` | Looks like a compromise | It is (B) arriving one column at a time. The first override is `cooldown`, the second is `cost`, and by the third the item table is a partial copy of `rpg_action` with none of its validation |

**Recommendation: (A). A reference, and never anything else.**

(C) deserves the explicit refusal because it is the option that will be proposed later, by someone
reasonable, for a good reason. The counter is not aesthetic: `effect_container.overrides_json`
already exists and works precisely because an override there *"replaces a value spec on the
referenced atom"* and *"may not introduce a param the kind does not declare"*
([spec-container-schema.md](../effect-atom/spec-container-schema.md)). There is no equivalent
constraint for an action, because an action's fields are not value specs — `cooldown_class`,
`commitment`, and `target_spec_json` are structure, not magnitude. An override mechanism with no
closure rule is a second schema.

**What (A) does to content authoring:**

- Actions become a **shared library**. One `act.cleave`, referenced by every axe. Balance changes in
  one row.
- An item that wants a *stronger* version of an action does not get one. It gets **numbers** — its
  own affixes move the derived channels the action's `ValueSpec` reads (§3.4). This is exactly I2's
  ruling holding, and it is why the seam and I2 do not actually conflict.
- An item that wants a *different* version names a different id (`act.cleave` vs
  `act.cleave.wide`). That is a content decision with a visible cost — a new authored action — which
  is the correct place for that cost to land.
- **Ordering constraint:** actions are authored before the items that name them. For a generated
  base-type catalogue (I3's 344 containers) that is fine, because grants are authored per **class**,
  not per base type (§8.6) — a handful of action ids, not hundreds.

### 4.2 Where the action's effects live

Settled by shipped schema rather than preference — see §3.3. The action's atoms live in a
`container_kind = 'skill'` container referenced by `rpg_action.container_id`; the item's own bonuses
stay in its `item` container. No new `container_kind`, no change to a sealed contract.

The rejected alternative was *"put the action's atoms in the item's container and let a flag mark
which ones are action effects."* It fails on the atom program's own ordering rule: standing grants
resolve through the actor's effect list sorted `(priority DESC, container_id ASC, seq ASC)`
([definitions.md](../effect-atom/definitions.md) §5), while an action's atoms apply as a unit at the
resolve tick ([spec-action-model.md](../action/spec-action-model.md) §4). One container cannot hold
two populations with two ordering laws and stay debuggable.

### 4.3 May a base type declare a default attack?

This is the sharpest question in the brief, because I2 ruled weapons supply numbers only — and yet a
nozzle that fires a spread and a nozzle that lobs an arc are different **actions**, not different
numbers, and that difference is the entire fantasy of a weapon slot.

| Option | Shape | Tradeoff |
|---|---|---|
| **(A) No.** Weapons stay numbers forever | Nothing to build | Weapon class collapses to a stat profile. I3 already flagged that `blunt` has no way to trade cadence for damage and is therefore *"simply better than `blade` at the same band"* ([ssot-item-categories.md](ssot-item-categories.md) §7.5) — that hole gets permanent |
| **(B) Yes, on both armament roles** — I2's literal assertion | Two columns' worth of conflict resolution | A 1H + off-hand pair can both declare one, and the runtime must arbitrate. Runtime arbitration of authored content is a bug generator |
| **(C) Yes, on `armament-primary` only** | One replacement per actor, by construction | Off-hand loses the ability to define an attack. It can still grant an *extra* action (`grant_role = 'granted'`) |

**Recommendation: (C).** It delivers the weapon fantasy, and it makes the conflict case
*unrepresentable* rather than *resolved* — the difference between a rule and an arbitration.

**What it costs, stated without softening:**

1. **Weapon class stops being a tag and becomes behaviour.** A spread nozzle and an arc nozzle are no
   longer comparable by a single number, and each needs its own win-rate evidence. That widens
   I3 §7.5's cadence hole rather than closing it — the hole moves from "we cannot express it" to "we
   can express it and now we must balance it."
2. **It couples base-type authoring to action content.** I3's generator emits 344 base types from 43
   identity definitions ([ssot-item-categories.md](ssot-item-categories.md) §7.6). Actions do not
   generate. If `default-attack` is authored per **base type**, that is 344 hand-authored actions and
   the lane has just invented an unaffordable content budget. **Mitigation, and it is a content rule
   rather than a schema one: `default-attack` is authored per weapon class per frame** — roughly
   `3 weapon classes × 2 frames = 6` actions, not 344. Within a class, the choice stays numbers.
   This is §8.6 and it needs G1's and R4's agreement.
3. **It amends I2 by one clause.** I2 asserted the column is legal on `armament-primary` *and*
   `armament-secondary` ([ssot-equip-slots.md:205](ssot-equip-slots.md)). Under (C), the
   `default-attack` role is primary-only; the off-hand keeps `granted`. That is a tightening of
   I2's assertion, not a contradiction of its principle, and it is flagged for R4 (§9.3).

**It does not violate "weapons supply numbers only."** The weapon supplies an identifier. Activation
stays entirely in `rpg_action`. §3.4 is the argument; this is where it is spent.

### 4.4 Where the grant row lives: base type, or instance?

**Base type.** Three reasons, and the first is decisive:

1. **A rolled instance freezes values, not identity** (E6: *"Instantiation = draw the pool… resolve
   every `OnInstantiate` value spec from `roll_seed`"*,
   [spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)). An action id is
   identity. Putting it on the instance would make it a rollable property, which would drag it into
   SC5's determinism contract and I6's mutation model for no gain.
2. **Two copies of the same sword must grant the same action.** An item whose ability varies per copy
   is a unique, and uniques are base types too (G1).
3. Keeping it on the base type means **nothing about this seam is rolled**, so nothing about it needs
   reproducing from a seed. SC5 is satisfied by having no surface.

---

## 5. Data shape

### 5.1 What already exists, and what does not

| Thing | State | Evidence |
|---|---|---|
| `effect_container` / `effect_container_atom` / `effect_container_pool` | **shipped** | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs` |
| `effect_instance` / `effect_binding` | **shipped** | `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:55-89` |
| `rpg_action`, `rpg_action_cost`, `rpg_action_effect_scope` | **do not exist** | `grep -rn "rpg_action" src/` returns nothing; `src/FusionRpg.Core/Actions/` does not exist |
| `item_base_type`, `item_category` | **do not exist** | proposed by I3; `grep -rn "item_base_type" src/` returns nothing |
| Real weapons | **do not exist** | `UniqueEquipmentCatalog` is *"Stub item_id → grant template map for W8-A Cold equip (not a gear shop)"* with a `weapon/armor/trinket` allowlist (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:7-12`) |

Three of the five things this seam joins are unbuilt. §5.6 is written against that.

### 5.2 `item_granted_action` — the whole item side, six columns

Keyed on the **base type's container id** (§4.4), not on an instance.

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT NOT NULL | FK → `item_base_type(container_id)`; the container must have `container_kind = 'item'` |
| `seq` | INT NOT NULL | stable authoring and display order. PK is `(container_id, seq)` |
| `action_id` | TEXT NOT NULL | FK → `rpg_action(action_id)`. **This is the entire seam** |
| `grant_role` | TEXT NOT NULL | closed set: `default-attack` \| `granted` |
| `enabled` | INT NOT NULL DEFAULT 1 | content is disabled, never deleted (definitions §6) |
| `revision` | INT NOT NULL DEFAULT 0 | joins the E8 content hash |

**A child table rather than a nullable column on `item_base_type`**, for one reason that pays for
itself immediately: a unique that grants two abilities (G1's entire subject) is expressible with no
schema change, and the "at most one `default-attack`" rule becomes a constraint the validator can
state rather than a comment.

**Reused, unchanged:** `effect_container.slot` already carries the frame-neutral role
([ssot-item-categories.md](ssot-item-categories.md) §5.2), so the "primary only" check in §4.3 is one
string compare against a column that exists. `effect_container.level_req` and I11's `req_json` gate
the item; a granted action adds **no** requirement clause of its own (§9.7).

**New:** one table, six columns, no new index beyond the PK plus `ix_item_granted_action_action` on
`action_id` so the action layer can answer "what grants this".

### 5.3 What the item side must NOT store — the more valuable list

Every row here is a column someone will propose. Each names its real owner.

| Never on the item side | Owner |
|---|---|
| `cooldown_ticks`, `cooldown_class`, `cooldown_key`, `starts_at`, `interrupt_cooldown_milli` | `rpg_action` cooldown group ([spec-action-model.md](../action/spec-action-model.md) §1) |
| `time_cost_ticks`, `windup_ticks`, `recovery_ticks`, `resolve_offsets_json`, `speed_channel`, `priority_band`, `slot_consuming` | the envelope — `ActionEnvelope.cs`, shipped |
| `commitment`, `interruptible`, `interrupt_refund_milli` | the envelope |
| `resource_id`, cost amount, cost `when` | `rpg_action_cost` ([spec-action-model.md](../action/spec-action-model.md) §2) |
| `target_spec_json`, `min_range`, `max_range`, `range_channel`, `anchor_source`, `requires_line_of_sight` | `rpg_action` targeting group / **A2** |
| `conditions_json` or any usability predicate | `rpg_action` / **A4**, over **E3**'s closed leaf list |
| the action's `container_id`, its atoms, or per-atom scope | `rpg_action.container_id`, `rpg_action_effect_scope` |
| charges, uses-per-battle, uses-per-rest, "recharges at rest" | the action layer. A charge is a **resource**, and the resource registry is blocker **B2** ([action-map.md](../action-map.md) §10.1) |
| the action's display name, icon, or description | the action's, surfaced by **G3** |
| whether the action is on cooldown right now | `CooldownLedger`, in RAM. E6's boundary is explicit: **no durable runtime table** |
| **any override of any of the above** | nobody. §4.1 option (C), refused |

**The test, so this list does not need to be memorised:** if a column would let two items naming the
same `action_id` behave differently, it belongs to the action layer or it does not exist.

### 5.4 The derived thing: an actor's action set

Not a table. Assembled at run start (§3.5) by the action layer (handshake item 4) from:

```
intrinsic (species row)
  + grants from equipped items      [this lane]
  + grants from traits, skills, patrons   [not this lane]
  ────────────────────────────────────────
  → dedup by action_id
  → apply default-attack replacement (armament-primary wins over intrinsic)
  → enforce the granted cap, rejecting rather than truncating
  → order (equip role ordinal, seq, action_id), ordinal comparison
```

The item layer contributes rows to the input and **implements none of that pipeline**. If items,
traits, and skills each wrote their own merge, they would disagree — and the disagreement would
surface as a nondeterministic action set, which under the battle time model is a broken replay
([decisions.md:41](../decisions.md)).

### 5.5 The handshake — what the action program must expose

**This is the document's main product.** Nine numbered items, written so `A1` can implement against
them and tick them off. Items 2, 3, 4, and 5 are the ones without which this seam cannot exist at
all.

1. **A resolvable `action_id` namespace.** `rpg_action(action_id)` as a primary key, plus a load-time
   lookup answering *does this id exist and is it enabled*. Without it the item validator cannot
   raise `UnknownAction`, and the seam's headline failure mode is unenforceable.

2. **A per-action `grantable` flag.** Not every action may be item-granted. `move`, `pass`, and the
   defence actions are actor-intrinsic; an item granting `act.pass` is nonsense. One boolean column,
   or a member of the closed `tags_json` set ([spec-action-model.md](../action/spec-action-model.md)
   §1). The item layer must be able to **reject at import**, not discover at runtime.

3. **A per-action `default_attack_eligible` flag, separate from (2).** An action may be legal as an
   extra ability and illegal as a replacement for the basic attack — anything with a resource cost,
   anything tagged `summon`, anything whose envelope `A5` cannot drive byte-identically. Only the
   action layer knows which. Two flags, because collapsing them into one would make every grantable
   action a legal default attack.

4. **An action-set assembly entry point.** Given an actor and a list of
   `(source, action_id, grant_role)`, return the ordered action set with intrinsic and granted
   merged, deduped, the default attack resolved, and the cap enforced — deterministic, ordinal,
   never sorted on a generated id ([definitions.md](../effect-atom/definitions.md) §5). **The item
   layer must not implement this.**

5. **A grant table that is not `effect_binding`** — and this is the correction this document exists
   to deliver. [spec-action-model.md](../action/spec-action-model.md) §5 says granted actions
   *"reuse `effect_binding`'s owner vocabulary; no second binding concept."* The **vocabulary** is
   reusable; **the table is not.** `effect_binding.instance_id` is `TEXT NOT NULL`
   (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:76-77`) and points at an `effect_instance`
   — a row with `roll_seed`, frozen `values_json`, and `power_json`. **A granted action has no
   instance and no rolls.** Two ways out:

   | | Shape | Verdict |
   |---|---|---|
   | (a) `rpg_action_grant(owner_kind, owner_key, action_id, source, grant_role, ...)` | a small parallel table reusing the 7 owner scopes and the `source` withdraw key | **Recommended** |
   | (b) Relax `effect_binding.instance_id` to nullable | one migration on a shipped table | Makes the table polymorphic — half its rows point at an instance and half do not — and every existing query has to learn which. E6 lists *"adding an owner scope"* as ask-first; this is larger |

   Reuse the **7 owner scopes** verbatim (`match`, `plant:`, `zombie:`, `entity:`, `player:`,
   `sector:`, `slot:` — [definitions.md](../effect-atom/definitions.md) §6) and the **`source`
   withdraw key**, which already has an index (`RpgStore.AtomInstances.cs:88`). That is what "reuse
   the vocabulary" should have meant.

6. **A named snapshot moment**, and it must be the same one equipment already freezes at —
   `phase != Roster` refuses equip (`src/FusionRpg.Server/UniqueActorService.cs:41-44`). One freeze
   moment, or `(setup, seed, trace)` stops being a complete description of a battle.

7. **A written removal-semantics rule, per FSM state.** §3.5 proposes it; the action layer owns it.
   It must name `ActionRunner`'s exit paths explicitly and must state that no inventory event
   becomes an `InterruptCause`.

8. **A cap policy and its number.** Whether a maximum granted-action count exists, what it is, and
   whether exceeding it rejects or truncates. This lane recommends **reject**; the number is a
   balance call.

9. **A written acknowledgement that per-grant overrides will never be accepted.** If the action layer
   intends to allow them, the item side needs to know before it ships a six-column table — because
   that is the difference between a seam and a second action system.

### 5.6 v1 scope — what an item can do today, and what ships first

**Today an item can do nothing with this seam, and there are four independent reasons. Any one alone
is sufficient.**

1. **`rpg_action` does not exist.** No table, no `src/FusionRpg.Core/Actions/` directory,
   no rows. `decisions.md:90` records the action model as *"Approved 2026-08-22, not yet built."*
2. **`item_base_type` does not exist either** — I3 proposes it; nothing in `src/` creates it. The
   grant table has no parent to key on.
3. **The battle runtime executes almost no atoms.** Eleven of twelve kinds are `Battle = None`; one
   is `Partial` (`AtomKindRegistry.cs:88`–`:260`, table in §3.6). So even with `rpg_action` shipped,
   an action whose container holds `spawn.entity` or `stat.modify` would resolve to **nothing** in
   battle. Authoring grants before that is fixed is the `status.expose.*` failure with extra steps.
4. **There are no real weapons.** Three hardcoded stub items on a `weapon|armor|trinket` allowlist
   (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:7-12`).

> ⛔ **CORRECTION, 2026-09-05 (module 19, at build time). Two of the four reasons are now false, and
> one of the two that remain has changed shape.**
>
> | # | Verdict |
> |---|---|
> | 1. *"`rpg_action` does not exist"* | ⛔ **False.** `rpg_action` ships with `grantable` and `default_attack_eligible` (`RpgStore.Actions.cs:22-32`), `src/FusionRpg.Core/Actions/` holds 30+ files, and `rpg_action_grant` exists as option (a) verbatim — its DDL comment cites item 5 of §5.5 by name |
> | 2. *"`item_base_type` does not exist"* | ✅ **Still true, and narrower than it reads.** Module 6 shipped the 740-row corpus and the Core readers but **no table**, so `item_granted_action.container_id` carries no FK — a wiring gap, not a wall |
> | 3. *"Eleven of twelve kinds are `Battle = None`"* | ⛔ **False** — see §3.6's correction above |
> | 4. *"There are no real weapons"* | ✅ **Still true.** Three stubs (`UniqueEquipmentCatalog`) and four relics |
>
> **The v1 answer is unchanged and the reasons for it are not.** What actually blocks content today is
> **X3**: nothing turns an action seed into an `rpg_action` row (`ActionSeeder.Generate` has zero
> production callers), so a grant would name a table nothing fills. Gate **GA2** — DDL, validator,
> reason codes, **zero content rows** — shipped 2026-09-05 and is the honest half.
>
> Handshake **item 8** is likewise no longer open: `CapPolicy` (action program, T24) answers it by
> naming which existing cap governs — held unlocks and equipped skills are capped, **granted by paid
> sources is uncapped on purpose** — so §3.7(d)'s proposed 8 and `TooManyGrantedActions` have no
> raiser on either side of the seam.

**So v1 for this lane is a written seam and zero rows in the database.** Under SC7 — *a row no code
consumes is not content; it is a lie in a table* — shipping the column early would be the defect this
folder was written to prevent. Saying "nothing" here is the finding, not a gap in the work.

**What ships first when the action layer lands.** Four gates, in order, each independently checkable:

| Gate | Ships | Proof it is real |
|---|---|---|
| **GA1** | `rpg_action` with handshake flags (2) and (3); the assembly entry point (4) returning intrinsic-only | An actor with no items has exactly one action, and it is the species' basic attack |
| **GA2** | `item_granted_action` DDL + validator + the four reason codes in §6. **Zero content rows** | A planted bad row is rejected by id with its code, per SC6 — a validator with no planted-violation test is an untested validator |
| **GA3** | **One** weapon base type with `grant_role = 'default-attack'` and one real action, driven through a battle | The actor's attack changes because of the item, visibly, in the battle trace |
| **GA4** | The `granted` role: one unique (G1) grants one extra ability. Cap, dedup, and the two-items-one-action rule get their first real test | Two items granting the same action produce **one** entry in the action set |

**Why GA3 before GA4.** The default-attack path reuses `A5`'s basic-attack adoption work and needs no
new UI — the actor already has an attack and it simply becomes a different one. A granted *extra*
ability needs a selection surface (`A7`, or the FE) that does not exist. Shipping the harder one
first would block the seam on the interactive layer for no benefit.

**One thing that can be done today, and it is worth doing at R4:** record in
[decisions.md](../decisions.md) that the item side of an action grant is **a reference and a role,
never a definition**, so `A1` starts its build against a settled seam instead of negotiating one
mid-build. That is a documentation change with no code and no rows.

---

## 6. Validation and reason codes

Two phases, matching [definitions.md](../effect-atom/definitions.md) §10: **import** is
all-or-nothing (E14), **load** is per-row rejection (E4/E5), and **bind** is E6's gate.

### 6.1 Content errors — import / load

| Bad input | Reason code | New? |
|---|---|---|
| `action_id` names no row in `rpg_action` | **`UnknownAction`** | **new** |
| `action_id` names a row with `enabled = 0` | **`UnknownAction`** | reuses the new code — a disabled action is not referenceable, same as a disabled atom is not bindable |
| Action exists but is not flagged grantable (handshake 2) | **`ActionNotGrantable`** | **new** |
| `grant_role = 'default-attack'` on a role other than `armament-primary` | **`DefaultAttackNotAllowed`** | **new** |
| `grant_role = 'default-attack'` on an action not flagged `default_attack_eligible` | **`DefaultAttackNotAllowed`** | same code, different clause |
| Two `default-attack` rows on one base type | `DefaultAttackNotAllowed` | — |
| `container_id` names no `item_base_type` row | `UnknownContainer` | existing |
| `container_id` names a container whose `container_kind ≠ 'item'` | `UnknownContainer` | existing |
| Duplicate `(container_id, seq)` | `DuplicateSeq` | existing |
| Same `action_id` twice on one base type | `DuplicateKey` | existing |
| `grant_role` outside the closed set | `BadParamValue` | existing |

### 6.2 Bind-time — depends on the actor, so it cannot be a content check

| Bad input | Reason code | New? |
|---|---|---|
| Equipping would push the actor's `granted` count over the cap | **`TooManyGrantedActions`** | **new** |
| The item's `level_req` exceeds the actor's level | `LevelTooLow` | existing, `BindGate.cs:47-49` |

Joins E6's bind-gate table alongside `LevelTooLow`
([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)).

### 6.3 Four new codes, and why not five

`UnknownAction` · `ActionNotGrantable` · `DefaultAttackNotAllowed` · `TooManyGrantedActions`.

That takes the closed list from 33 to 37. Adding a code is a reviewed change
([definitions.md](../effect-atom/definitions.md) §10), and these are proposed, not assumed.

**The fifth code was deliberately not proposed.** The check it would have carried is real and must
still run:

> **The anti-silence check.** An item granting an action whose container holds **no atom the battle
> runtime can execute** must produce a warning at import.

Every kind but `status.apply` is `Battle = None` today (§3.6), so this check would fire on almost
everything, immediately, loudly — which is the point. But it reuses **`RuntimeUnsupported`**, which
already exists and already means exactly this. A new code would be a second name for the same fact.
It is a **warning at import, not a rejection**, because the matrix is a living audited table and an
action that is inert today may be executable next wave — refusing the content would make the matrix's
own evolution a migration.

### 6.4 What must never be silent

Three states this seam can reach, all of which must be visible rather than inferred:

| State | Surfaced as |
|---|---|
| The actor already has the granted action | "already known" in item text — **G3** (§9.6) |
| The item's granted action cannot fire in the current mode | the **battle-only** presentation tag — **G3** |
| The granted action's atoms have no runtime consumer | the import warning in §6.3, reusing `RuntimeUnsupported` |

---

## 7. Worked examples

**Every number below is illustrative, not balanced.** Units per SC4: ticks are integer ms,
per-mille is integer, derived-channel magnitudes are resolver points.

### 7.1 Two nozzles that are the same numbers and a different game

Both are plant-frame `armament-primary`, band 3, same rarity, same 160‰ role budget
([ssot-equip-slots.md](ssot-equip-slots.md) §2.8 context).

| | Brass Nozzle | Arcing Nozzle |
|---|---|---|
| `item_base_type` row | `class_id = nozzle`, `band = 3`, `affix_pool_tag = weapon-nozzle` | identical |
| Its own container (`item` kind) | `atom.base-damage` at `seq 0`, implicit at `seq 1`, `pool_rolls = 3` | identical |
| `item_granted_action` | `(seq 0, act.spray-cone, default-attack)` | `(seq 0, act.lob-arc, default-attack)` |

And the two actions, **entirely in `rpg_action`, none of it on the item**:

| `rpg_action` column | `act.spray-cone` | `act.lob-arc` |
|---|---|---|
| `time_cost_ticks` | 1000 | 1400 |
| `windup_ticks` | 200 | 400 |
| `cooldown_ticks` | 0 | 0 |
| `min_range` / `max_range` | 1 / 4 | 3 / 7 |
| `requires_line_of_sight` | 1 | 0 (an arc goes over) |
| `container_id` | `skill.spray-cone` | `skill.lob-arc` |

**One TEXT column is the entire difference**, and readiness does the rest: at `turn.speed` 200 the
cone actor acts every `1000 / 2 = 500` ticks and the arc actor every `700`
([action-map.md](../action-map.md) §10.4d). Both weapons remain numerically comparable — a roll of
`+140` resolver points on an offence channel makes *this* nozzle better than *that* nozzle, exactly
as I2 intended.

### 7.2 A unique with two rows

**Thornbind Lash**, humanoid `armament-primary`, unique.

| `item_granted_action` | |
|---|---|
| `(seq 0, act.lash-strike, default-attack)` | replaces the species basic attack |
| `(seq 1, act.thornbind, granted)` | an extra ability — this is what makes it a unique rather than a good weapon |

`act.thornbind`: `cooldown_ticks = 8000`, one `rpg_action_cost` row of `40 stamina` at `onCommit`,
`container_id = skill.thornbind` whose single atom is `status.apply` with a 3000 ms root.

**This is the only example in this section whose effect could execute in battle at all today** —
`status.apply` is the one kind with a battle consumer, and only `Partial`
(`AtomKindRegistry.cs:159`). Every other worked example is inert until the battle runtime grows
executors. That is §5.6 shown rather than asserted.

The item stores two rows and eleven values across them. It stores **zero** of the six numbers in the
paragraph above.

### 7.3 Two rings, one action

Two `jewel-minor` items, both with `(seq 0, act.emberburst, granted)`.

| Event | Action set | Cooldown |
|---|---|---|
| Both equipped | **one** `act.emberburst` entry, two provenance rows | one clock |
| One removed | still one entry — the other row is live | untouched |
| Both removed | entry gone | **still untouched** |
| Both re-equipped later in the same battle *(if mid-battle equip ever ships)* | entry returns | **still on cooldown** |

The last row is the point: `CooldownLedger` keys `(ActorKey, CooldownKey)`
(`CooldownLedger.cs:8`), never the item. The swap exploit (§8.4) is closed by a key shape that
already shipped for an unrelated reason.

### 7.4 The cap refusing rather than truncating

An actor wearing eight `granted` sources equips a ninth. With a cap of 8 (illustrative), the bind
**refuses** with `TooManyGrantedActions`, naming the item. It does not equip and silently drop the
ninth ability — which is what the player would then spend an hour failing to notice.

---

## 8. Failure modes

Unsentimental, each with what in this design prevents it. Game references are **recalled and
unverified** per the contract §7.

1. **The seam grows into a second action system.** How it actually happens: someone adds
   `grants_action_cooldown_override` for one good reason, then a cost override, and within two waves
   the item table holds a partial, unvalidated copy of `rpg_action`. **Prevented by** §5.3 as a
   Never list, §4.1's explicit refusal of option (C), and handshake item 9 — the action program
   acknowledges in writing that overrides never arrive.

2. **Items granting actions the kernel cannot schedule.** Path of Exile ships items granting skills
   the character cannot meaningfully use *(recalled, unverified)* — the item reads as a huge upgrade
   and does nothing. **Prevented by** `ActionNotGrantable` and `DefaultAttackNotAllowed` at import,
   which require handshake items 2 and 3 to exist. Without those two flags this failure is
   unpreventable, which is why they are the first things asked for.

3. **A granted action that silently does nothing because no runtime consumes it.** This repo's own
   scar tissue: `status.expose.*` is a registered derived channel with zero readers
   (`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:29`,
   `DerivedStatRegistry.cs:107`), cited as a cautionary precedent inside the atom code itself
   (`AtomKindRegistry.cs:103`, `PredicateNode.cs:5`). Today's battle matrix would reproduce it
   exactly (§3.6). **Prevented by** shipping zero rows at v1 (§5.6), gating content behind GA2, and
   the import warning in §6.3.

4. **Cooldown reset by swapping the item.** World of Warcraft's on-use trinket swapping
   *(recalled, unverified)*. **Prevented for free** by `CooldownSlot(ActorKey, Slot)`
   (`CooldownLedger.cs:8`). Recorded here so nobody "fixes" the ledger to key on the grant.

5. **Ability bloat — more granted actions than anyone can read.** Diablo 3's item-granted procs
   against a six-slot bar *(recalled, unverified)*: the build stops being chosen and starts being
   accumulated. **Prevented by** the cap and by rejecting rather than truncating (§3.7d).

6. **The weapon becomes the class.** If every weapon defines its attack, the species and skill layers
   stop mattering and "best weapon" becomes a globally solved question. **Not preventable by
   schema** — it is a content rule: `default-attack` is authored **per weapon class per frame**
   (~6 actions), never per base type (~344). Within a class the choice stays numbers. This needs
   R4's agreement, and it is the single most likely place this design is quietly violated later,
   because violating it looks like generosity.

7. **The tooltip lie.** An item whose headline is an ability that cannot be used in the mode the
   player is in. **Prevented by** the mandatory battle-only presentation tag (§3.6, §6.4) — which is
   part of the option (b) pick, not decoration on it.

8. **Mid-battle removal cancelling a committed action and corrupting slot accounting.** **Prevented
   by** §3.5's rule that removal never cancels a `Committed` or `Resolving` run, and by refusing to
   add an inventory `InterruptCause`. `ActionRunner`'s exits are already exactly three — resolve,
   fizzle, interrupt — and each one releases the slot on a path that is tested; a fourth would need
   its own proof at the most expensive checkpoint in the timeline program.

9. **The action set becoming nondeterministic.** If the merge sorted on a generated id, the same
   loadout would produce different action orders across runs, and under
   `(setup, seed, decision-trace)` that is a broken replay. **Prevented by** handshake item 4's
   ordinal, content-derived ordering — the same mistake definitions §5 already caught once with
   `binding_id`.

---

## 9. What this lane needs from other lanes

1. **The action program (A1)** — the nine-item handshake in §5.5. Items **2, 3, 4, and 5** are hard
   dependencies; without them this seam cannot be validated, assembled, or stored. Item 5 is also a
   **correction to their §5**: `effect_binding` cannot carry an action grant, because
   `instance_id` is NOT NULL and a granted action has no instance
   (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:76-77`).

2. **I3 (item-categories)** — confirm `item_base_type.container_id` is a stable authoring key that
   will not be re-derived, since `item_granted_action` keys on it. I3 needs **no new column**: the
   grant lives in its own child table.

3. **I2 (equip-slots)** — one tightening, for R4 rather than for I2 to redo: `default-attack` is
   legal on **`armament-primary` only**, not on both armament roles as
   [ssot-equip-slots.md:205](ssot-equip-slots.md) asserts. `armament-secondary` keeps `granted`. The
   principle in I2 §2.8 is preserved; only the role list narrows.

4. **G1 (uniques)** — the `granted` role is a unique's primary use, and `ssot-uniques.md` did not
   exist when this was written. G1 should adopt `item_granted_action` rather than invent per-unique
   ability fields, and should adopt §8.6's per-class rule for anything it makes a default attack.

5. **G2 (consumables)** — a consumable **is** an action in item form, which is the same seam with a
   different lifetime. The cheap answer is a third `grant_role` value (`on-use`) rather than a second
   table, but the charge model, the "who is the actor" question, and the inventory lifetime are
   **G2's**, not mine. Flagged, not decided.

6. **G3 (presentation)** — three things must be renderable: the **battle-only** tag (§3.6), the
   **already known** state (§3.7b), and the granted action's own name and description pulled from
   `rpg_action`, not from the item.

7. **I11 (requirements)** — confirm that a granted action adds **no** requirement clause. The item's
   existing gate (frame, level, `req_json`) is sufficient; a per-action requirement would be a second
   gate on the same equip.

8. **I12 (generation)** — the generator must **never** roll a grant. `grants_action_id` is base-type
   identity, not a rolled property (§4.4), and a generator that could produce it would drag the seam
   into SC5's determinism contract for nothing.

9. **effect-atom (E5 / E8 / E14)** — register `item_granted_action` in the content-hash registry
   ([definitions.md](../effect-atom/definitions.md) §8) when it ships. **Do not add a
   `container_kind`** — §3.3 needs none. Separately, and larger than this lane: the battle runtime's
   eleven `None` cells (§3.6) are the reason granted actions cannot do anything, and that is worth
   the atom program's attention independently of items.

10. **battle-timeline (T2 / B5)** — confirm in writing that no inventory event may become an
    `InterruptCause` (§3.5), so a later mid-battle-equip feature cannot reach into the kernel's slot
    accounting.

11. **R1 (defect register)** — one claim in this document is code-read and not executed, per the
    design gate: the battle-column matrix in §3.6 is read from
    `AtomKindRegistry.cs` and **not verified by running a bind in a battle host**. It is a table of
    declared support, and a declared cell can be stale in either direction. Worth one check.

---

## 10. Open questions for the owner

1. **Is a weapon allowed to change what the actor's basic attack *is*?** §4.3 says yes and reconciles
   it with I2's ruling, but it is a design decision with real balance consequences, and I2 explicitly
   handed it to the action layer rather than deciding it. If the answer is no, this lane shrinks to
   the `granted` role only and §4.3 is deleted.

2. **The granted-action cap.** 8 is a placeholder. The number is a UI and balance call.

3. **Per weapon class, or per base type?** §8.6 says class (~6 authored actions); per base type is
   ~344 and is not affordable. Recording this as a decision is what stops it being violated one
   generous item at a time.

4. **Is mid-battle equip ever coming?** Everything in §3.5's second half is contingent on it. If the
   answer is a firm no, that half becomes a Never and the design gets simpler.

5. **May non-weapon roles grant actions?** A `jewel-major` that grants a spell is a genre staple, and
   uniques will want it (G1). I2's assertion currently limits the column to the two armament roles.
   This lane needs a yes or no before G1 authors against it.

6. **Does a granted action count against the item's power budget?** SC9 says the power model is open
   and no lane may depend on it — so this lane ships without an answer. But it is worth stating what
   this lane *would* want: a granted action is the largest single thing an item can do, and pricing
   it at zero would make every action-granting item strictly dominant in whatever budget system
   arrives. E9 should know the case exists before it closes its cost function.
