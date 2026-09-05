# Lane I11 SSOT — the equip requirement gate, and a proposal for primary actor attributes

**Status:** Lane I11 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

> **Part B needs owner sign-off.** Per **OD7**, primary actor attributes **do not exist** — not in code,
> not in a spec, not in a decisions row. A repo-wide sweep for `attribute` in `src/` returns nothing but
> assembly metadata. Part B below is a **proposal reverse-derived from what the gate needs**, and it is
> the one part of this document that is not a design *within* locked inputs. **Part A does not depend on
> Part B** — the frame, faction, and level clauses ship whole if attributes are refused. That separation
> is deliberate, and it is how this lane avoids holding the item program hostage to one open decision.

---

## 1. Scope

### This lane owns

- The **equip requirement gate**: the predicate that decides whether a given actor may bind a given item
  instance into a given slot, and what the player is told when it says no.
- The **requirement axes** — frame, faction, level, primary attributes, element affinity — and which are
  hard and which are soft.
- The **shape of a requirement row**: where it lives, how it is validated at load, how it is evaluated at
  bind.
- **What happens after the fact** when a legally equipped item's requirement stops being met.
- The **cycle rule** that stops two items each satisfying the other.
- A **proposal for the primary attribute set** (Part B), including each attribute's named consumer.

### This lane does NOT own

| Thing | Lane |
|---|---|
| Equip slots, roles, and how many exist per frame | **I2** |
| The six actor resources — **LOCKED**, see [decisions.md](../decisions.md) "Resource model (2026-08-22, **six** 2026-08-26)" | resource hub; not an item lane at all |
| Base types, implicits, and which frame a base type declares | **I3** |
| The rarity ladder and its ordinals | **I1** |
| Rolled affixes and tier bands, including any `+attribute` affix | **I8** |
| Post-drop mutation that changes a requirement | **I6** |
| What a requirement failure looks like in the comparison UI's layout | **I13** (this lane owns the *content* of the message) |

---

## 2. The model

An item declares **clauses**. An actor presents a **profile**. The bind gate evaluates every clause
against the profile and either binds the whole instance or refuses it with a named reason. There is no
third outcome, because the atom layer has no partial bind
(`spec-instance-and-binding.md` Boundaries: *"never let a rejected bind degrade into a partial bind"*).

```text
item instance ──► clauses: frame ∈ {…} · faction? · level_req · attribute ≥ n (×0–2)
                              │
actor ──────────► profile: frame · faction · level · unassisted attributes
                              │
                        BindGate.Check ──► bind, or one rejection naming every unmet clause
```

Five axes, and they are not equal:

| Axis | Hard or soft | Why |
|---|---|---|
| **frame** (`humanoid` \| `plant` \| `hybrid`) | **hard** | A crown does not fit a zombie. This is a fit, not a difficulty. It is also the widest gate, so it must be the most legible one |
| **level** | **hard** | Already ships. `effect_container.level_req` + `LevelTooLow`, enforced at `BindGate.cs:47` |
| **primary attributes** | **hard at the equip transition, soft while worn** | §2.2 |
| **faction** (`plant` \| `zombie`) | **hard when present, and it is almost never present** | §2.3 |
| **element affinity** | **not a gate at all** | §2.4 |

### 2.1 Frame is the only wide gate, and that is on purpose

**OD1** keys equipment on frame, and **OD3** gives hybrids a base type from either frame per role. So a
frame clause excludes roughly half the roster for a pure-frame item and nobody for a hybrid wearer. That
is the largest single exclusion in the system, and every other axis has to be narrower than it, or the
"most drops unusable by most of the roster" failure (§8.3) arrives immediately.

Frame is stored as a **set**, not a scalar: an item that fits either body has two frame rows. `hybrid`
satisfies both `humanoid` and `plant` rows. There is no `either` keyword and no third enum value — two
rows say it, and a set has no ordering question.

### 2.2 Hard at the transition, soft while worn — the pick, and the code reason for it

The brief's framing is right: refusing is clean but frustrating, derating is forgiving but hides a cliff.
The resolution is that these are **two different moments**, and the shipped machinery answers them
differently.

**At the equip transition: hard.** Not as a taste call. A soft attribute penalty means binding an item's
atoms at reduced magnitude, and the atom layer cannot express that:

- `effect_instance_atom.values_json` is **frozen at instantiate** and reproducible from
  `(container_id, catalog_revision, roll_seed)` (SC5, definitions §5).
- **Bind rolls nothing** — `spec-instance-and-binding.md` Boundaries: *"Never: roll anything at bind
  time."*
- There is no runtime magnitude multiplier between a bound atom and its consumer.

So a soft equip penalty is a **new mechanism**, not a tuning knob — an SC1 finding, and this lane declines
to open one for a problem the next paragraph solves.

**While worn: soft, via a status.** If the actor later falls below a clause, the item is **not**
unequipped. The actor gains an `overburdened` status whose debuff is a **container of atoms**, exactly the
pattern the resource hub locked one day earlier for exhaustion
([decisions.md](../decisions.md), Resource model row: *"expressed as a **status** … whose debuff is a
**container of atoms**, never a hardcoded channel list"*). That buys, for free, the four things
`StatusRuntime` already ships: visibility, stacking, resistance, and `icd_ms` — which is the mechanism
that stops an apply/clear flicker when a value oscillates around a threshold.

**What the player sees.**

| Moment | Surface |
|---|---|
| Browsing an item they cannot wear | The item is dimmed, and **every** unmet clause is listed with both numbers: *"Frame: needs plant (this body is humanoid)"*; met clauses in grey, *"Aim 32 required — this specimen has 29"* in red |
| Attempting the equip anyway | The same list, from the rejection's detail string. Never a silent no-op, never a generic "cannot equip" |
| Falling below while worn | A status icon and a one-line banner: *"Overburdened — Aim 32 required, you have 31. Gear stays on; combat stats reduced until the shortfall clears."* |

**Report every unmet clause, not the first.** `AtomRejection` carries one `Reason` today
(`src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs`), which is correct — one reason code — but the detail
string must enumerate all failures. A gate that reveals one shortfall at a time makes the player play
whack-a-mole with their own character sheet.

### 2.3 Faction is a clause, and it is confined

Frame is the body; faction is allegiance (item-ideal §4, and `DemonSpeciesDef.Side` is documented as
carrying *both* today at `src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs:11` — that conflation is a
finding for I3, not a design input).

A faction clause is legal but **content-restricted**: allowed only on hand-authored uniques and set
pieces, forbidden on any base type with a rolled pool. Reason: a faction clause on a rolled base type
multiplies with the frame clause and takes a drop from "half the roster" to "a quarter of the roster",
which is §8.3 arriving through the back door. On a hand-authored unique it is flavour with a known
audience, which is what uniques are for.

### 2.4 Element affinity is not a gate

It was the obvious candidate for the soft axis, and it should be refused. Three reasons:

1. It has the same expressibility problem as a soft attribute penalty (§2.2) — a derate needs a magnitude
   multiplier that does not exist.
2. The element matrix is **already** a matter of degree. A fire item on an ice actor is weaker through
   `combat.power.fire` versus that actor's element, resolved by the Element Hub. Adding a second,
   requirement-shaped element penalty prices the same thing twice.
3. `element.type.primary` / `element.type.secondary` are actor **metadata**, not derived channels
   ([actor-hub-ssot.md](../actor-hub-ssot.md) §3.E). Gating on them would make a metadata field
   load-bearing in the bind path for no mechanical gain.

**Element affinity gets an advisory, not a clause:** the UI marks off-affinity gear, and the player learns
it from the damage numbers, which is where element already lives.

### 2.5 How this extends `level_req` rather than replacing it

`level_req` and `LevelTooLow` **ship today**. Verified: `effect_container.level_req INTEGER`
(`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:26`), read into `ContainerRow.LevelReq`
(`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:61`), enforced at
`src/FusionRpg.Core/Effects/Atoms/BindGate.cs:47-49`, tested at
`tests/FusionRpg.Core.Tests/Atoms/BindGateTests.cs:168-184`. 34 BindGate tests pass as of this writing.

This lane changes **none** of that. It adds three things beside it:

1. A **sibling table** for the non-level clauses (§5.1). `level_req` stays a column on `effect_container`,
   unchanged, because it is universal and every container has at most one.
2. An **actor profile** on `BindContext`, alongside the `OwnerLevel` that is already there.
3. **Three reason codes**, in the same shape as `LevelTooLow`.

#### A fail-open in the shipped gate, found while reading it

```csharp
if (levelReq is { } req && ctx.OwnerLevel is { } level && level < req)
```
— `src/FusionRpg.Core/Effects/Atoms/BindGate.cs:47`

When `ctx.OwnerLevel` is `null`, the clause is skipped and the bind **succeeds**. For a host with genuinely
no notion of level (`match` scope, a world buff) that is defensible and clearly intended — the XML doc at
`BindGate.cs:12` says *"Null when the host has no notion of one."* For an **item** it is a hole: an item
with `level_req 50` binds freely to any caller that omits the level. No test covers the case
(`BindGateTests.cs:177-184` covers "met" and "requirement absent", never "requirement present, level
unknown").

**This lane's rule:** for `container_kind` in `item` · `gem` · `set` · `charm`, a **missing profile is a
rejection**, not a pass — `ScopeUnsupported`, message *"this host supplies no actor to gate against"*.
That is SC6 applied to the gate itself. It is a change this lane needs from the effect-atom program
(§9.1), not something I11 can assert unilaterally.

### 2.6 Requirements after the fact

An actor equips legally, then drops below. Every game that ties requirements to mutable stats hits this,
and the three shipped answers are all bad:

| Shipped answer | Failure |
|---|---|
| Force-unequip | Cascades. Removing one item drops another below, which removes another. Recalled from Diablo 2, **unverified** — the swap-order dependence there is well known but I have not re-checked a source |
| Leave it, do nothing | The requirement is decorative. An actor can arrange to be permanently under and never notice |
| Block the *cause* (refuse the debuff) | Makes gear immune to debuffs, which is worse than the problem |

**This lane picks a fourth: the item stays on and the actor is `overburdened`** (§2.2). It is better than
all three because it is visible, reversible, and needs no new mechanism.

**When can a shortfall actually happen?** Narrower than it looks, because of the cycle rule in §2.7 — the
gate reads attributes excluding every equippable source, so unequipping gear can never cause one. What
remains:

| Cause | Real? |
|---|---|
| **Level demotion** | Yes. `rpg_actor_progression` carries `demotion_count` and `highest_level` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:308-309`) — levels in this repo go down |
| **A trait, injury, or patron debuff lowering an attribute** | Yes. Those are non-equippable container kinds and do reach the gate value |
| **A content revision raising a requirement** | Yes, and it is the nastiest: it strands already-equipped copies. Requirements are read at bind; a revision does **not** retroactively unbind — it produces `overburdened` at the next resolve |
| **Unequipping the gear that granted the attribute** | **No.** Impossible by §2.7 |

### 2.7 The cycle rule — one line, and it closes the whole class

Two items, each granting the attribute the other requires: equip A first and B is legal, equip B first and
A is legal, and neither is legal from bare. Fixed-point iteration terminates but is order-dependent, and
partial failure during a cascade has no defined state.

> **The gate reads attributes composed from every source EXCEPT containers of the four equippable
> kinds — `item`, `gem`, `set`, `charm`.**

Those four are exactly the kinds SC3 reserves for equipment, sockets, sets, and charms. Traits, species
passives, patrons, and world buffs **do** count, and that is good design: a trait that lets a specimen
wield heavy gear is a real choice, and it cannot cycle because a trait is not something the player equips
and unequips to game an ordering.

Call the resulting number the **unassisted** value. It is the only number the gate ever reads. The
displayed sheet shows the full composed value with the equipment contribution broken out, so a player
sees `Sinew 32 (29 + 3)` and the tooltip says which number gates.

Consequences, stated so nobody is surprised:

- **`+attribute` affixes are real but non-enabling.** A `+3 sinew` roll still feeds every derived channel
  `sinew` feeds. It just cannot unlock gear. I8 should price it accordingly.
- **One indirection has to be closed:** a container of an equippable kind may not grant a binding of a
  non-equippable kind. Without that, a charm grants a trait and the cycle walks back in through I10.
  This is a load-time validation, and it is an ask of the effect-atom program (§9.2).

### 2.8 Hybrids

A hybrid satisfies both frame rows, so the frame clause is trivial for it. **OD3** already prices that
breadth in slot count (12–13 rather than 15), so this lane adds no hybrid-specific penalty. Adding one
would be paying twice.

The real hybrid problem is not the gate, it is **dead affixes**. Item-ideal §5.4 establishes that an affix
family declares which frames its role serves — `+move speed` on a plant `roots` slot is dead content. A
hybrid wearing a plant `muzzle` whose implicit is `flourishing` (`produceInterval`, a plant-only channel)
gets an inert line.

Rejecting the bind would break OD3's promise; letting the line sit inert violates SC6. The correct cut is
neither, and it belongs upstream:

> **A base type whose implicit uses a frame-locked channel must declare exactly one frame.** Then a
> hybrid simply cannot wear it, cleanly, via `FrameMismatch`, and the check is load-time on the base type
> rather than a bind-time surprise.

That is an ask of **I3** (§9.3), not a gate rule. With it in place, the gate needs no hybrid special case
at all.

---

## 3. Attributes are not resources — the line, held explicitly

Before proposing anything, the constraint. **Six actor resources are LOCKED** — `hp` · `stamina` ·
`hunger` · `spirit` · `qi`, one shared set, faction differences are display labels only, magnitudes are
Actor-Hub derived channels `resource.max.{id}` / `resource.regen.{id}`, current values are lazy per-actor
runtime state ([decisions.md](../decisions.md), Resource model row). Nothing below redesigns any of it.

| | Resource | Attribute |
|---|---|---|
| **What it is** | A pool that is spent and refills | A rating that does not move within an encounter |
| **Changes** | Every action | On level, training, or a permanent consumable |
| **Stored as** | `(value, lastTick)`, resolved lazily on read | Composed on read from content + specimen row |
| **Channel** | *Has* channels: `resource.max.*`, `resource.regen.*` | *Feeds* channels; is not one itself |
| **Gates equipment** | Never | Yes — that is why it is being proposed |
| **Empty means** | An exhaustion status | Nothing. There is no zero rail |

Per-attribute, against the resource it sounds closest to:

| Attribute | Nearest resource | The one-line difference |
|---|---|---|
| `bulk` | `hp` | `bulk` sets how large the `hp` pool is; `hp` is how much of it is left right now |
| `sinew` | `stamina` | `sinew` is how hard one action hits; `stamina` is how many actions remain |
| `reflex` | `stamina` | `reflex` is how *soon* the next action comes; `stamina` is whether it can be paid for |
| `aim` | — | Nothing in the resource set is about landing a hit. No overlap to police |
| `sap` | `spirit` / `qi` | `sap` sets the size and refill rate of those pools; the pools are the spendable contents |

**The rule that keeps them apart in practice: an attribute may feed a `resource.max.*` or
`resource.regen.*` channel, and a resource may never feed an attribute.** One direction only. The moment
that reverses, spending stamina would change what gear you can wear mid-fight, and the two layers have
merged.

---

## 4. Options considered, and the recommendation

### 4.1 Part A — the gate

| Option | Tradeoff |
|---|---|
| **A1 — hard everywhere** | Simplest, fully expressible today, zero new mechanisms. But the after-the-fact case (§2.6) has no answer except force-unequip, which cascades |
| **A2 — soft everywhere** | Forgiving, no cliff. **Not expressible**: values freeze at instantiate and bind rolls nothing (§2.2). Would need a bind-time magnitude multiplier, i.e. a new mechanism, i.e. an SC1 violation |
| **A3 — hard at the transition, soft while worn** ✅ | Both halves use shipped machinery: the bind gate for the transition, `StatusRuntime` for the shortfall. The cliff is not hidden, because crossing it produces a visible, named status |

**Recommendation: A3.**

Requirement storage, separately:

| Option | Tradeoff |
|---|---|
| Columns on `effect_container` | Cheap for one or two, but the attribute count is a *proposal* — a column per attribute bakes an unsigned-off decision into schema, and five sparse columns are NULL on almost every row |
| `tags_json` | Free, and the reason `status.expose.*` is a scar: an unparsed blob has no validation and no consumer contract. SC7 forbids it in spirit |
| **A sibling `container_requirement` table** ✅ | Sparse by construction, one validated row per clause, absorbs a change in attribute count without a migration, and its consumer is nameable in one word: the bind gate |

**Recommendation: the sibling table.**

### 4.2 Part B — the attribute set

| Option | Tradeoff |
|---|---|
| **B0 — no attributes at all** | Honest and cheap. The derived layer already has 99 channels; an item can grant `+combat.accuracy.omni` directly, so an attribute is just a linear map into channels affixes already hit. **The case against:** without attributes, requirement gating collapses to level, which is the exact failure mode the brief names. Frame and level alone cannot express "this nozzle wants a precise body, not an old one." Part A ships regardless, so B0 is a live option, not a strawman |
| **B1 — three (`bulk` / `edge` / `sap`)** | Maximally legible. But `edge` merges force, speed, and precision, so every offensive build maxes it and the attribute makes no decision. Three attributes where one is mandatory for most of the roster is two attributes with extra steps |
| **B2 — five** ✅ | Each has a disjoint consumer set (§6.2), each feeds at least one channel that exists on every frame, and no two collapse without recreating B1's do-everything stat |
| **B3 — seven or more** | Every additional attribute needs a consumer nothing else has. The channel catalog does not offer seven disjoint clusters; the sixth and seventh would be re-slices of the first five, which is bookkeeping |

**Recommendation: B2, five attributes — subject to owner sign-off per OD7.**

---

## 5. Data shape

### 5.1 `container_requirement` — NEW

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT FK → `effect_container` | |
| `axis` | TEXT | `frame` \| `faction` \| `attribute` |
| `key` | TEXT | frame id, faction id, or attribute id |
| `value` | INT | frame/faction: `1` (membership). attribute: the threshold in **attribute points** |
| `revision` | INT | joins the content hash |

PK `(container_id, axis, key)`. Frame membership is one row per acceptable frame, which is why there is no
`either` value to enumerate and no ordering question.

**Consumer, named as SC7 requires: `BindGate.Check`.** Nothing else reads it. A row with no clause to
evaluate cannot exist, because the axis enum is closed and every value is checked.

**Content-hash coverage.** Definitions §8 makes covered tables *"a registry, not a list"*, versioned as
`contentHashSchemaVersion`. Adding this table is an explicit bump, not silent breakage of every prior
stamp. Say so in the migration.

### 5.2 Columns reused unchanged

| Existing | Used for |
|---|---|
| `effect_container.level_req` | the level clause. Not moved, not duplicated into the new table |
| `effect_container.container_kind` | selects whether a missing profile is a rejection (§2.5) |
| `effect_binding.slot` | the role being filled; the gate checks the wearer *has* that role (I2's data) |
| `rpg_unique_actors.level` | the specimen level — see §5.5 |

### 5.3 `BindContext` gains a profile

```csharp
public readonly record struct ActorProfile(
    string Frame,                                    // humanoid | plant | hybrid
    string Faction,                                  // plant | zombie
    int Level,
    IReadOnlyDictionary<string,int> Unassisted);     // attribute id -> points, §2.7
```

Added as `ActorProfile? Profile = null` beside the existing `int? OwnerLevel`
(`src/FusionRpg.Core/Effects/Atoms/BindGate.cs:13-17`). Two level fields is one too many; the migration
folds `OwnerLevel` into the profile and keeps a nullable `OwnerLevel` shim for the `match`-scope callers
that legitimately have no actor.

### 5.4 The attribute registry is **code**, not data

Five entries in an `AttributeCatalog` in Core, matching the `StatusCatalog` ADR-locked code-first
precedent and the resource hub's identical call (*"a small code catalog … Five entries."*).

This is SC7 read literally: **adding a sixth attribute requires a new channel mapping, which is new code,
so the attribute set is code.** A data table here would be rows that do nothing until someone writes the
consumer — the `status.expose.*` mistake, repeated.

Each entry carries: `id`, per-frame display labels, the channels it feeds and the coefficient for each,
and its species-base and growth-curve *keys* (the values are content).

### 5.5 Per-specimen state — NEW

| Table | Grain |
|---|---|
| `rpg_actor_attributes(instance_id, attribute_id, trained, consumed, revision)` | Per **specimen**, holding only what was *bought* — training points and permanent-consumable points |

Species base and level growth are **not stored**. They are computed on read from species content plus
`rpg_unique_actors.level`, following the standing compute-on-read law. Storing the composed total would go
stale the moment a growth curve is retuned, and a stale gate value means a player holding gear the rules
say they cannot hold.

**Per-specimen, not per-type — and the schema already shows why.** Two levels exist today:
`rpg_actor_progression.level` at `(player_id, kind, type_id)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:306`)
and `rpg_unique_actors.level` per specimen (`RpgStore.cs:343`). Equipment binds to a specimen —
`rpg_unique_equipment` is keyed on `instance_id` (`RpgStore.cs:356-361`). If attributes were per-type,
levelling one Peashooter would unlock gear for every Peashooter the player owns, and the gate would gate
the *type*, not the *wearer*. **Species base is per-type content; the current value is per-specimen
state.**

### 5.6 The finding that has to be said out loud: there is no durable per-specimen owner scope

The seven owner scopes are `match` · `plant:{typeId}` · `zombie:{typeId}` · `entity:{ptr}` · `player:{id}`
· `sector:{id}` · `slot:{id}` (`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:11-20`). None of them names
a demon specimen. `entity:` is the closest, and definitions §6 is explicit that it is **session-scoped and
never durable** — a recycled pointer would silently retarget.

Meanwhile the stub `rpg_unique_equipment` is keyed on `instance_id` (`RpgStore.cs:356`) — a specimen key
the atom layer has no scope for.

**So durable equipment on a demon specimen cannot be expressed by the shipped binding model.** That is a
program-level gap, not an I11 gap, but it lands on this lane because the gate's whole job is to read the
wearer's profile and there is currently no supported way to name the wearer. Raised in §9.4.

---

## 6. PART B — the primary attribute proposal

> **Owner sign-off required (OD7).** Everything in §6 is a proposal.

### 6.1 The set — five, frame-neutral ids with per-frame labels

Only two of the five read wrong on a frame, so only two carry labels. The mechanism is the one
[decisions.md](../decisions.md) already locked for resources — *"faction differences are display labels
only … labels are **content**, never a channel id and never a branch"* — with one deliberate difference:
**attribute labels key on frame, not faction**, because an attribute is a property of the body and frame
is the body axis (item-ideal §4). A peashooter-zombie is faction zombie with a plant body, and it should
read **Fibre**, not Serum. That is the frame/faction split doing real work rather than being asserted.

| id | Humanoid label | Plant label | What it is |
|---|---|---|---|
| `bulk` | Bulk | Bulk | Structural mass — how much punishment the body's own substance absorbs |
| `sinew` | Sinew | **Fibre** | Motive force — how hard the body drives anything |
| `reflex` | Reflex | Reflex | Reaction speed — how soon and how often it acts |
| `aim` | Aim | Aim | Targeting — whether the thing it drives lands where it meant to |
| `sap` | **Serum** | Sap | The animating fluid — what fuels its pools and how forcefully its effects take hold |

Names are biological and mechanical, not fantasy. There is no Strength, Dexterity, Intelligence, Wisdom,
or Charisma. `focus`, `will`, `essence`, and `mana` are on the refused-names record already
(resource-hub-ideal §8) and none of them appears here. `Serum` is chosen over any blood word deliberately:
it is Zomboss-the-scientist's register, which resource-hub-ideal §8 argues the franchise prefers over
decay-horror.

**Collisions checked**, using the discipline resource-hub-ideal §10.4 wrote after `rot` collided with a
shipped status. Each candidate was checked against the 71 atom families in
[atom-family-library.md](../effect-atom/atom-family-library.md), the 21 status ids, the five resource ids,
and `StatChannels` (`src/FusionRpg.Core/Stats/ModifierOp.cs:11-21`):

| Candidate | Verdict |
|---|---|
| `bulk` · `sinew` · `reflex` · `aim` · `sap` | **clear** |
| `precision`, `evasion`, `might`, `vitality` | **rejected** — all four are existing atom family names |
| `spirit`, `qi`, `stamina` | **rejected** — locked resource ids |
| `spore`, `bloom`, `spark`, `rot` | **rejected** — shipped status ids, or one character from one |

**Unit: attribute points.** Integer, floor 0, no ceiling. Under SC4 this is a new unit and must be named
as one — attribute points are **neither game units nor resolver points**. The map from an attribute point
to a channel magnitude is per channel and per frame, exactly as tier bands are per channel family and
never copied across. Every requirement value and every grant in this document is in attribute points.

### 6.2 What each one actually does — the SC7 table

The critical constraint. **An attribute that feeds nothing is dead content.** Every row names real channels
from the real vocabulary, and every row is honest about whether the consumer exists today.

| Attribute | Feeds | Channel layer | Consumer today? |
|---|---|---|---|
| **`bulk`** | `maxHp` | primary (`StatChannels.MaxHp`) | ✅ shipped — compose → `EntityStatWriter` |
| | `combat.defense.omni` | derived, the 84-channel set | ✅ shipped — overlay combat resolver |
| | `status.resist.omni` | derived | ✅ shipped — `ResistanceEvaluator` L2b |
| **`sinew`** | `atk` | primary (`StatChannels.Atk`) | ✅ shipped |
| | `combat.power.omni` | derived | ✅ shipped — overlay combat resolver |
| | `combat.shield.pen.omni` | derived | ✅ shipped — `ShieldRuntime` |
| **`reflex`** | `turn.speed` | derived | ⚠️ **declared, not registered** — `Battle/Timeline/DerivedTurnChannels.cs`; actor-hub-ssot §11.4 |
| | `combat.dodge.omni` | derived | ✅ shipped |
| | `attackInterval` (plant) / `zombieSpeed` (zombie) | primary — **promised** | ⚠️ owner-approved 2026-08-22, unbuilt (atom-family-library §5) |
| **`aim`** | `combat.accuracy.omni` | derived | ✅ shipped |
| | `combat.crit.rate.omni` | derived | ✅ shipped |
| **`sap`** | `resource.max.{spirit,qi}` | derived | ⚠️ **proposed, not registered** — actor-hub-ssot §3.G |
| | `resource.regen.{stamina,hunger,spirit,qi}` | derived | ⚠️ same |
| | `status.power.omni` | derived | ✅ shipped |

**Two consumers in that table do not exist and one is unregistered. Named, not hidden:**

1. `turn.speed` / `turn.haste` are constants in `src/` that `DerivedStatRegistry.RegisterDefaults()` does
   not register — actor-hub-ssot §11.4 records that the "unknown channel → reject" rule *"would fire the
   moment a `turn.*` modifier reached the compose path."* `reflex` cannot feed it until the
   battle-timeline program registers it. `reflex` still has two live consumers meanwhile.
2. `resource.max.*` / `resource.regen.*` are **proposed** in actor-hub-ssot §3.G and explicitly *"not in
   the catalog yet"*, needing an ADR row. `sap` cannot feed them until then. `sap` still has
   `status.power.omni` meanwhile.
3. `attackInterval` / `produceInterval` / `zombieSpeed` are owner-approved for promotion to real channels
   and unbuilt.

**No proposed attribute depends solely on a consumer that does not exist.** That was the acceptance bar
for the set, and it is why there is no sixth attribute for "economy": the only economy surfaces are
`resource.economy` atoms and the lawn sun bank, neither of which an attribute can feed. An `economy`
attribute would be a row nothing consumes, which SC7 calls a lie in a table.

**The anti-dump-stat rule, stated as a law:** *every attribute must feed at least one channel that exists
on every frame.* `bulk`→`maxHp`, `sinew`→`atk`, `reflex`→`turn.speed`, `aim`→`combat.accuracy.omni`,
`sap`→`resource.regen.*`. A Wall-nut has little use for `aim` and none for `attackInterval`, but it acts on
the turn kernel and it can be missed, so no attribute is fully dead on any body.

### 6.3 How an attribute reaches a channel

Not by a new subsystem. Actor-hub-ssot §6.1 found **four unregistered producers** — patron, stars,
injuries, and contracts — writing derived channels with no subsystem row and no opcode, and named the fix:
*"a derived write needs both a registered channel and a registered producer"*, with `stat.derived` as the
opcode all four adopt.

Attributes adopt the same seam: the attribute layer resolves five integers and emits `stat.derived` atoms
through **one registered producer**, rather than becoming the fifth unregistered writer in a document
whose whole point is that there are already four.

**And this is where the quarantine bites, as SC2 requires me to say:** `stat.derived` is quarantined
`None/None/None` (defect **D6**) and has **no executor in any runtime today**. The first consumer ships in
**E12** (`BattleStatComposer` at squad build). So:

| Half of I11 | Blocked by the quarantine? |
|---|---|
| **The gate** — reads integers, compares to clauses, rejects | **No.** Ships whenever the profile exists |
| **The effect** — attributes moving combat numbers | **Yes.** Waits for E12 |

The gate is the half this lane owns, and it is the half that is not blocked. Worth stating because it
inverts the usual expectation: attributes can be *enforced* long before they *do* anything.

### 6.4 How attributes are gained

| Source | Grain | Notes |
|---|---|---|
| **Species base** | per **type** (content) | A five-integer vector on `DemonSpeciesDef`. This is where roster identity lives |
| **Level growth** | per **specimen** (computed) | A curve per species per attribute. Reuse `effect_curve` — it already has `input: level`, `points_json` in ‰, validated sorted `x`, clamp-never-extrapolate (definitions §2). A second curve mechanism would be the same mistake as a second modifier bag |
| **Training** | per **specimen** (stored) | Spends materials or souls in **I9's** cost vocabulary. **Capped**, see below |
| **Permanent consumables** | per **specimen** (stored) | The classic tome. Same column, same cap, different source — so the two cannot stack past the ceiling |
| **Gear, gems, sets, charms** | binding | Feeds channels; **invisible to the gate** (§2.7) |

**Training cap: total bought points ≤ the specimen's level.** One number, legible, and it grows with
progression without a second curve. It is also what stops "grind training at level 1 to unlock
everything", which would make `level_req` decorative.

**No free allocation on level-up.** A real design commitment, so it should be visible: the player does
**not** get points to spend into attributes on level. Two reasons, one decisive:

1. It kills the dump stat at the root. You cannot dump what you cannot allocate. Every shipped
   allocate-your-own-stats ARPG produces the same behaviour — exactly the minimum into requirement stats,
   everything else into the damage stat. Recalled from Diablo 2, **unverified**.
2. It keeps roster identity in the species. This game's content is a roster of ~24 species, not one
   avatar. If every specimen can be shaped into anything, the species vector stops meaning anything — and
   the species vector is the only thing making a Wall-nut different from a Peashooter at this layer.

The cost is player agency; training is the bounded, paid form of it. **Flagged for the owner** in §10.4.

### 6.5 One sheet or two?

**Recommendation: one shared sheet, per-frame labels, per-species base vectors and growth curves.**

The case for two sheets: a plant genuinely is not a humanoid, and separate sheets would let plant
attributes be about rooting, canopy, and photosynthesis in a way a shared sheet cannot.

The case against, and it wins on four counts:

1. **The affix library doubles.** A `+aim` affix would need a plant twin, and the whole reason
   item-ideal §5.1 shares twelve slot *roles* across frames is *"so one affix library serves all of
   them."* A second attribute sheet undoes that saving in the same document.
2. **Hybrids become incoherent.** OD3 says a hybrid takes base types from either frame. Which sheet does
   it present? Any answer is arbitrary.
3. **The repo has ruled this way twice already.** Slot roles: shared, per-frame names (item-ideal §5.1).
   Resources: one set of five, per-faction labels (decisions.md). A third ruling the same way is
   consistency; a different ruling here would need a reason this lane does not have.
4. **Differentiation survives anyway**, through three things that cost nothing: the species base vector,
   the per-species growth curve, and the fact that some channels only exist on one frame. `reflex` buys a
   plant a faster `attackInterval` and a zombie more `zombieSpeed` — same attribute, different purchase.
   That is real frame character without a second sheet.

---

## 7. Worked examples

**Numbers are illustrative, not balanced.** Requirement and attribute values are in **attribute points**
(§6.1); `level_req` is in levels.

### 7.1 One specimen's sheet at two levels

Species `peashooter`, frame `plant`, faction `plant`. Base vector is per-type content; growth is a
per-species `effect_curve` per attribute.

| | `bulk` | `sinew` | `reflex` | `aim` | `sap` |
|---|---|---|---|---|---|
| Species base (level 1) | 8 | 12 | 10 | 14 | 9 |
| **Level 1 — unassisted total** | **8** | **12** | **10** | **14** | **9** |
| Level growth to 20 | +11 | +17 | +13 | +21 | +11 |
| Training bought (cap 20 at level 20; 5 spent) | — | — | — | +5 | — |
| **Level 20 — unassisted total** | **19** | **29** | **23** | **40** | **20** |
| Equipped `+3 sinew` affix | — | +3 | — | — | — |
| **Level 20 — displayed total** | 19 | **32** | 23 | 40 | 20 |

The last two rows are the whole point of §2.7: the sheet **displays** `Sinew 32 (29 + 3)`, and the gate
reads **29**. The `+3` is real for every channel `sinew` feeds and worth nothing for unlocking gear.

For contrast, a `wallnut` specimen at level 20 might sit at `bulk 44 / sinew 14 / reflex 9 / aim 11 /
sap 16`. Same sheet, different species vector and different curves — which is what makes an `aim 32`
clause select **who** rather than **when** (§8.1).

### 7.2 Three items with real requirements

| Item | Base type | Frame rows | `level_req` | Attribute clauses | Faction |
|---|---|---|---|---|---|
| **Iron Crown** | `iron-crown`, role `head-protective`, Rare | `humanoid` | 12 | `bulk ≥ 18` | — |
| **Pea Nozzle Mk3** | `pea-nozzle-mk3`, role `armament-primary`, Magic | `plant` | 20 | `aim ≥ 32`, `sinew ≥ 24` | — |
| **Brainpan Sigil** | `brainpan-sigil`, role `jewel-major`, Unique | `humanoid`, `plant` | 30 | `sap ≥ 28` | `zombie` |

Stored as `container_requirement` rows — Pea Nozzle Mk3 is three rows (`frame/plant/1`,
`attribute/aim/32`, `attribute/sinew/24`) plus the `level_req` column it does not touch.

Brainpan Sigil demonstrates two things at once: `frame: either` expressed as two rows (§2.1), and the rare
faction clause confined to a hand-authored unique (§2.3).

### 7.3 The level-20 peashooter against all three

| Item | Evaluation | Outcome |
|---|---|---|
| Iron Crown | frame: wearer is `plant`, rows are `{humanoid}` | **`FrameMismatch`** — *"Iron Crown fits humanoid bodies. This specimen is a plant."* Level 20 ≥ 12 and bulk 19 ≥ 18 are both met and irrelevant |
| Pea Nozzle Mk3 | frame plant ✅ · level 20 ≥ 20 ✅ · aim 40 ≥ 32 ✅ · sinew 29 ≥ 24 ✅ | **binds** |
| Brainpan Sigil | frame plant ✅ · level 20 < 30 ✖ · faction plant ≠ zombie ✖ · sap 20 < 28 ✖ | **rejected** — reason code `FactionMismatch` (the permanent failure outranks the two fixable ones), detail string listing **all three**: *"Needs faction zombie (this specimen is plant). Needs level 30 (is 20). Needs Sap 28 (has 20)."* |

The third row is §2.2's "report every unmet clause" doing its job. Reporting only `LevelTooLow` would send
the player off to grind ten levels for an item they can never wear.

### 7.4 Falling below while worn

The peashooter, wearing Pea Nozzle Mk3, takes an injury that applies `-9 aim` through a non-equippable
container (§2.7 counts it). Unassisted `aim` drops 40 → 31, below the clause of 32.

| Step | What happens |
|---|---|
| 1 | The nozzle **stays equipped**. No unbind, no cascade, no inventory shuffle |
| 2 | `overburdened` is applied — a status, with `icd_ms` set, so an `aim` value oscillating around 32 cannot flicker the debuff on and off each tick |
| 3 | Its debuff is a container of atoms. Illustrative content: `stat.derived` `combat.accuracy.omni` at **−40 resolver points** and `combat.crit.rate.omni` at **−40 resolver points** — the channels the unmet attribute feeds, not a hardcoded global list |
| 4 | UI: the status icon, plus the shortfall named — *"Overburdened — Aim 32 required, you have 31."* |
| 5 | The injury expires. `aim` returns to 40, `overburdened` clears at the next read (attributes resolve on read, so a lazily-resolved crossing is re-evaluated on read, not only on write — the same rule the resource hub set for exhaustion) |

Note the units in step 3. `−40` here is **resolver points**, not attribute points and not per-mille —
`CritRateScale = 100.0`, so 40 points is 0.4 sigmoid units, a real but non-crippling hit. Getting this
wrong by an order of magnitude is exactly the units trap definitions §2 warns about.

---

## 8. Failure modes

### 8.1 Attribute requirements that just gate by level with extra steps

**The sharpest one.** If every attribute grows monotonically with level and every requirement scales with
item level, then `aim ≥ 32` *is* `level ≥ 20` with more typing and a worse error message. Three defences,
and the second is load-bearing:

1. **At most two attribute clauses per container**, validated at load. Three or more is a level check
   wearing a costume.
2. **Growth is per-species divergent.** `aim 32` is level 20 on a Peashooter and unreachable on a Wall-nut
   (§7.1). The clause therefore selects **who**, not **when** — which is the entire justification for the
   axis existing. **If growth curves were shared across species this defence evaporates and B0 becomes the
   correct answer.** That is the single assumption Part B rests on, and it should be checked before build,
   not after.
3. **Training buys past a clause early, at a cost.** Level cannot be bought; attributes partly can. That
   is a decision level alone cannot offer.

Plus the load-time check that a clause must name an attribute the item's own atoms actually consume: a
helm requiring `aim` when nothing on it touches `combat.accuracy.*` or `combat.crit.rate.*` is a tax with
flavour text.

### 8.2 A dump stat nobody wants

Prevented twice: by **no free allocation** (§6.4) — there is no dumping move available — and by the
anti-dump-stat law in §6.2, which requires every attribute to feed at least one channel present on every
frame. Verified against all five.

The residual risk is a **near-dump stat for one species**: `aim` on a Wall-nut. That is role
differentiation, not a dump stat. A dump stat is one *no* build wants; `aim` is maxed by every shooter on
the roster.

### 8.3 Requirements that make most drops unusable by most of the roster

Item-ideal §8 already flags the roster-scale problem — twenty demons × twelve slots is 240 equipped items
before anything sits in a bag. Requirements multiply that pressure. Three content rules:

1. **Frame is the only wide gate** (§2.1), and it is ~50/50 by construction. Faction clauses are confined
   to uniques (§2.3) so the axes cannot multiply on a rolled drop.
2. **Attribute clauses are a minority.** Recommended budget: zero on Normal, at most one on Magic, at most
   two on Rare and Unique — and **none at all on the two `jewel-minor` roles**, so every actor always has
   two slots that accept any drop of their frame.
3. **Clause value is bounded by item level, never by rarity.** Forced by **OD4**: rarity overlaps
   deliberately, so a high-roll low-rarity item can beat a low-roll high-rarity one. If rarity raised
   requirements, the low-roll high-rarity item would be gated *harder* than the better item it loses to,
   and OD4's overlap would invert into a trap.

### 8.4 Attributes duplicating the resource layer

Answered structurally in §3: different change rate, different storage, different role, one-directional
feed. The checks to run when this is built: **no attribute may be spent, and no resource id may appear in
a `container_requirement` row.** Both are one-line validations and both are cheap permanently.

### 8.5 The unequip cascade

Removing one item drops another below its requirement, which removes another. **Impossible by
construction** — the gate never reads an equipment-sourced attribute (§2.7), so unequipping cannot lower a
gate value.

### 8.6 Requirement creep on a content revision

An item whose clause rises in a patch strands equipped copies. Requirements are evaluated **at bind**; a
revision does not retroactively unbind. The wearer becomes `overburdened` at the next resolve, visibly,
and can act on it. The alternative — a silent mass unequip on patch day — is how a live game loses a
weekend.

### 8.7 The gate reads a stale attribute

If the composed total were cached or persisted, a growth-curve retune would leave the gate comparing
against a number no longer true. Prevented by §5.5: only *bought* points are stored; base and growth are
computed on read.

### 8.8 The requirement UI teaches the wrong number

If the sheet shows `Sinew 32` and the gate reads 29, the player concludes the gate is broken. This is the
predictable cost of §2.7 and it is a UI obligation, not a design flaw: the equipment contribution must be
broken out on the sheet and the tooltip must say which number gates. Raised with I13 (§9.11).

---

## 9. What this lane needs from other lanes

1. **Effect-atom program (E5/E6)** — the `container_requirement` table, the `ActorProfile` on
   `BindContext`, the three new reason codes, the fail-open fix in §2.5, and registration of the new
   table into the content-hash registry with a `contentHashSchemaVersion` bump. This lane cannot add
   reason codes to a closed 33-item list on its own authority.
2. **Effect-atom program (E5)** — the load-time rule that a container of an equippable kind
   (`item` · `gem` · `set` · `charm`) may not grant a binding of a non-equippable kind. Without it the
   §2.7 cycle rule has a hole through I10.
3. **I3 (base types)** — two things. (a) Every base type declares its frame set, and a base type whose
   implicit uses a frame-locked channel (`produceInterval`, `zombieSpeed`, `arm1`/`arm2`) must declare
   exactly one frame (§2.8). (b) `DemonSpeciesDef` needs an explicit `Frame` field —
   `DemonSpeciesCatalog.cs:11` documents `Side` as carrying faction *and* body, and four generated
   zombie-side species are plant-bodied Fusion hybrids (`peashooterzombie`, `ironpeazombie`,
   `cherrynutzombie`, `bucketnutzombie`). The gate cannot key on `Side`.
4. **The item program as a whole** — a durable per-specimen owner scope. §5.6: none of the seven shipped
   scopes names a demon specimen, `entity:` is explicitly non-durable, and the equipment stub is keyed on
   `instance_id`. Until this is resolved the gate has no wearer to read a profile from. **This is the
   largest single blocker on I11 and it is not I11's to fix.**
5. **I2 (equip slots)** — the role↔frame table, and a ruling on whether the gate should also verify the
   wearer *has* the role being filled (a hybrid with 12 slots lacks two a pure frame has). I11 assumes yes
   and would reject with `FrameMismatch`; I2 should confirm that code choice or name a better one.
6. **I1 (rarity)** — confirmation that **rarity never raises a requirement** (§8.3). OD4's overlap breaks
   if it does. I1 owns the ladder; this lane owns the consequence and needs the ruling.
7. **I8 (affixes)** — whether `+attribute` is a rollable affix family. I11 recommends **yes, and
   non-enabling** (§2.7). If I8 declines, `sap` and `bulk` lose an affix-layer expression and the gate is
   unaffected.
8. **I6 (instance mutation)** — an enhancement that raises a requirement can strand an equipped item. I6
   owns the mutation model; I11 needs its recorded-operation log to include requirement changes, so the
   `overburdened` transition is explicable rather than mysterious.
9. **I12 (loot → instance)** — a drop whose clauses no actor in the player's roster can meet is noise.
   I12 should either bias generation toward the roster or accept that some fraction of drops are salvage
   fodder by design. I11 has no preference; the choice must be made somewhere.
10. **I9 (cost vocabulary)** — training and permanent consumables spend in I9's terms. I11 defines the cap
    (≤ specimen level) and not the price.
11. **I13 (inventory and comparison)** — the comparison view must carry the full unmet-clause list from
    §2.2, and must distinguish *unassisted* from *displayed* attribute values or §2.7 reads as a bug
    (§8.8).
12. **Resource hub / Actor Hub** — `resource.max.*` and `resource.regen.*` are proposed and unregistered
    (actor-hub-ssot §3.G; needs a decisions row). Two of `sap`'s three consumer groups are behind that.
13. **Battle-timeline program** — `turn.speed` / `turn.haste` are declared in `src/` and not registered
    (actor-hub-ssot §11.4). One of `reflex`'s three consumers is behind that.
14. **Actor Hub** — one registered producer for attribute-derived writes, so attributes do not become the
    fifth entry in the unregistered-producers table (actor-hub-ssot §6.1).
15. **Status stream** — the `overburdened` status id, its category, and its `icd_ms`. `StatusCatalog` is
    ADR-locked code-first, so this is their row to add, not ours.

---

## 10. Open questions for the owner

1. **Does Part B ship at all?** OD7 says attributes do not exist. B0 — frame and level only, no attributes
   — remains coherent, and Part A ships whole either way. **This is the decision.**
2. **Five, or three?** §4.2 recommends five and argues three collapses into a do-everything offence stat.
   If bookkeeping is the larger worry, three is defensible and the gate does not care which.
3. **The names.** `bulk` · `sinew` · `reflex` · `aim` · `sap`, with plant `sinew` shown as **Fibre** and
   humanoid `sap` shown as **Serum**. Collision-checked, but naming is taste and taste is the owner's.
4. **No free allocation on level-up** (§6.4). This trades player agency for roster identity. It is the
   most opinionated call in Part B, easy to reverse before build and hard after.
5. **Faction clauses at all?** §2.3 allows them, confined to uniques. Forbidding them outright is one
   fewer axis, one fewer reason code, and costs only some unique flavour.
6. **Which level does `level_req` compare against** for an item — `rpg_unique_actors.level` (specimen) or
   `rpg_actor_progression.level` (type)? The shipped gate takes whatever the caller passes and the
   question has never been asked. I11 recommends **specimen**, matching the attribute grain.
7. **The missing durable per-specimen owner scope** (§5.6). Adding one is an "ask first" change under E6's
   boundaries. It is not this lane's call and it blocks this lane.
8. **Three new reason codes** taking the closed list from 33 to 36 (§7.1). Definitions §10 calls adding one
   a reviewed change; three at once deserves an explicit yes or no.

---

## 11. Design-gate checklist

```
[x] I identified the subsystems this touches — effect-atom (container / instance / binding, the bind
    gate), Actor Hub derived channels, the resource model, demon species, unique-actor lifecycle.
[x] I read every required doc this session, in the order the contract names: enrichment-contract.md,
    item-ideal.md, resource-hub-ideal.md, the decisions.md Resource model row, actor-hub-ssot.md,
    effect-atom/definitions.md, spec-container-schema.md, spec-instance-and-binding.md,
    atom-family-library.md.
[x] I checked decisions.md for a lock covering this — the Resource model row is the binding one, and
    §3 holds the line against it rather than re-deriving it.
[x] Every factual claim about the repo cites file:line or a doc section.
[x] I verified claims against CODE, not comments — BindGate.cs, AtomRejection.cs, OwnerScope.cs,
    ContainerRow.cs, RpgStore.cs schema, RpgStore.Containers.cs, DemonSpeciesCatalog.cs,
    ModifierOp.cs (StatChannels), and BindGateTests.cs were all opened.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) the constraint I report.
    `dotnet test tests/FusionRpg.Core.Tests --filter BindGate` → 34 passed, 0 failed.
    The `OwnerLevel is null` fail-open in §2.5 is read from BindGate.cs:47 and confirmed uncovered —
    no test in BindGateTests.cs supplies a level_req with a null OwnerLevel.
[ ] Nothing contradicts a §2 invariant. **Partial:** nothing contradicts one, but Part B *proposes* a
    layer that has no decisions row. That is OD7's instruction, and it is flagged in the header, in
    §6, and in §10.1 rather than buried.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item-program map, plan, or task list exists yet.** Per the enrichment contract,
    reconciliation into item-ideal.md happens in one pass after all lanes land, and this lane must
    not edit it.
```
