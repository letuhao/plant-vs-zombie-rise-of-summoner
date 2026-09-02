# Spec: action-model (A1)

**Status: REVISED 2026-08-27** against the sealed [action-ideal.md](../action-ideal.md). Module **A1** in
the [action map](../action-map.md). The foundation: **the action data structure, its tables, and its
dataflow.** Every other module is a consumer of what is defined here.

> **Where this spec and the ideal disagree, the ideal wins.** Where the ideal and shipped code disagree,
> the code wins.

**What changed in this revision**, all traceable:

| | Change | Source |
|---|---|---|
| 1 | **`rpg_action_grant` is its own table** — the previous *"reuse `effect_binding`"* was wrong | [item/ssot-granted-actions.md](../item/ssot-granted-actions.md) §5.5 item 5 |
| 2 | **Two new flags** — `grantable`, `default_attack_eligible` | same, items 2 and 3 |
| 3 | **Three action kinds** and loadout capacity | ideal §1, decisions 1, 2, 25 |
| 4 | **Six resources**, not five | `decisions.md` Resource model, six 2026-08-26 |
| 5 | **`rung`** joins the identity group | ideal §4.1, decisions 9, 16 |
| 6 | **Restriction is a self-debuff** — no new machinery, `scope: caster` | ideal §8.7, decision 23 |

## Objective

Define what an action **is**, where it is stored, and what happens between "something decided to use this"
and "the effects landed."

```text
action = envelope (when) + container of atoms (what) + target rule (who)
       + costs (what it takes) + usability condition (may I) + rung (how strong)
```

**The membership rule** — the test that decides whether something is an action at all:

> Anything an actor does that interacts with the environment or itself, costs resource or time, and needs a
> cooldown, **is an action. No exception.**

Attack, guard, move, skill, summon and pass are actions. Passive traits, status pulses and exhaustion
debuffs are not — the actor does not *do* them.

## Design

### 1. Three kinds, and only one of them costs loadout capacity

| Kind | Count | Loadout capacity? | Source |
|---|---|---|---|
| **Basic** — attack · guard · move | 3 | **no** | intrinsic on the species row |
| **Innate** | 1 | **no** — a free sixth | **the species row** — `SpeciesBasics.InnateActionId`, nullable |
| **Earned skill** | **5 equipped** | yes — the scarcity | the unlock ladder (`A11`) or any paid grant |

**Loadout capacity caps the granted set only.** Intrinsic actions are never bound, so **there is nothing
for a cap to count** — stronger than a rule saying "basic actions are free", because there is no rule to
forget.

> ⚠️ **`loadout capacity` is not `slot`.** `slot` means the kernel's concurrency width `W`
> (`slot_consuming`). Two meanings of one word in one subsystem is how `block`/`guard` and
> `primary`/`aptitude` each cost this repo a rename.

Two validations keep the kind split true:

- A species row omitting any of the three basics is **rejected at load, naming the species**.
- A grant whose `action_id` collides with a basic is **rejected**, never double-counted.

### 2. `rpg_action` — one row per action

Every value that scales is a **`ValueSpec`** (`Core/Effects/Atoms/ValueSpec.cs`), reused rather than
redefined, so `effect_curve` serves actions too and no second scaling mechanism exists.

| Group | Columns |
|---|---|
| identity | `action_id` PK · `name` · `kind` · **`rung`** · `tags_json` · `enabled` · `revision` |
| **grant** | **`grantable`** · **`default_attack_eligible`** |
| effects | `container_id` → `effect_container(container_id)` |
| timing | `time_cost_ticks` · `speed_channel` · `windup_ticks` · `resolve_offsets_json` · `recovery_ticks` · `commitment` · `interruptible` · `interrupt_refund_milli` · `slot_consuming` · `priority_band` |
| cooldown | `cooldown_class` · `cooldown_key` · **`cooldown_channel`** · `cooldown_ticks` · `starts_at` · `interrupt_cooldown_milli` |
| targeting | `target_spec_json` · `min_range` · `max_range` · `range_channel` · `anchor_source` · `requires_line_of_sight` |
| usability | `conditions_json` |

**`kind`** is a closed enum — `basic` · `innate` · `skill`. It is what §1's capacity rule reads, and what
makes "is this intrinsic" a column rather than a join.

**`rung`** indexes `A12`'s rung table. It is **not** a magnitude: the table holds the multipliers, and a
row here holds only the index. One table, many faucets (ideal §3.5).

**`tags_json`** is a closed set — `offensive · defensive · heal · buff · debuff · movement · summon ·
utility`. It is what `A7` chooses on, and the atom program's standing rule applies: **AI reads tags, never
internals.**

**The timing columns are the existing `ActionEnvelope`**, not a second copy. `A5` folds in the three gaps
the map identified; this spec reserves the columns so that fold is additive.

#### 2.1 The two grant flags, and why they are two

Both requested by [item/ssot-granted-actions.md](../item/ssot-granted-actions.md) §5.5, items 2 and 3.

| Flag | Means | Why separate |
|---|---|---|
| `grantable` | an item, passive or variant may grant this | `move`, `pass` and the stance actions are actor-intrinsic; an item granting `act.pass` is nonsense |
| `default_attack_eligible` | this may **replace** the species basic attack | An action can be legal as an extra ability and illegal as a replacement — anything with a resource cost, anything tagged `summon`, anything whose envelope `A5` cannot drive byte-identically |

> **Collapsing them into one would make every grantable action a legal default attack.** Only this layer
> knows which are which, and the item layer must be able to **reject at import**, not discover at runtime.

### 3. `rpg_action_cost` — `(action_id, resource_id, amount_spec, when)`

A table, not columns, because an action costs several resources. `amount_spec` is a `ValueSpec`.
**Six resources** — `hp` · `stamina` · `hunger` · `spirit` · `qi` · `poise`. `A3` owns the cost model; this
spec owns the shape.

`when` is **`onCommit`** (default) or **`perTick`**. Per-tick is the channel shape: pay each resolve
offset, and **failing to pay ends the action** through the existing interrupt path.

**Consumption is atomic.** Validate every cost, consume every cost, roll back all of them if any fails. An
action that consumed stamina and then found no qi must leave the actor exactly as it found them.

**Committing is what costs, not landing.** An interrupted channel has paid; a fizzled action has paid; a
missed attack has paid.

> **One documented split, not an exception:** `poise` pays a **flat commit** (the action — committing
> costs) **plus** an absorb drain proportional to what the guard stopped (the mitigation — output is
> priced) **plus** a `perTick` hold. Two rules governing two things rather than one rule broken. See
> [class-system/spec-guard-economy.md](../class-system/spec-guard-economy.md) and `A8`.

### 4. `rpg_action_effect_scope` — `(action_id, atom_id, scope)`

Which of the action's atoms hits whom. `scope` is a closed enum: **`caster` · `primaryTarget` ·
`eachTarget` · `casterAllies`**. Rows are optional; an atom with no row defaults to `eachTarget`.

Kept action-side deliberately: putting `scope` on `effect_container_atom` would change a sealed contract
and make atoms less reusable outside actions.

> **This is also how a RESTRICTION is expressed** (ideal §8.7): the burst is `scope: primaryTarget`, the
> price is `scope: caster` + a debuff status. **No new machinery** — a status that lowers derived channels
> is `rally`'s machinery with the sign flipped, and E17 shipped the `ModifyStat` consumer.

### 5. `rpg_action_grant` — a NEW table, not `effect_binding`

**This corrects the previous revision of this spec**, and the correction came from another program.

The old text said granted actions *"reuse `effect_binding`'s owner vocabulary; no second binding
concept."* [item/ssot-granted-actions.md](../item/ssot-granted-actions.md) §5.5 item 5 verified in code
that this cannot work:

> `effect_binding.instance_id` is `TEXT NOT NULL` (`RpgStore.AtomInstances.cs:76-77`) and points at an
> `effect_instance` — a row carrying `roll_seed`, frozen `values_json` and `power_json`.
> **A granted action has no instance and no rolls.**

**The vocabulary is reusable; the table is not.**

```sql
rpg_action_grant(owner_kind, owner_key, action_id, source, grant_role, ...)
```

Reuses, verbatim: the **seven owner scopes** (`match`, `plant:`, `zombie:`, `entity:`, `player:`,
`sector:`, `slot:` — `definitions.md` §6) and the **`source` withdraw key**, which already has an index.
That is what *"reuse the vocabulary"* should have meant.

**Rejected alternative:** relaxing `effect_binding.instance_id` to nullable. It makes the table
polymorphic — half its rows point at an instance and half do not — and every existing query has to learn
which.

| Source | Rule |
|---|---|
| **Intrinsic** | `species.action_ids`. *"A default must never depend on authored data"* |
| **Granted** | `rpg_action_grant`. Items, passives, variants, learned skills, and `A11`'s unlocks |

**Resolution is intrinsic ∪ granted, deduplicated, ordered by `action_id` ordinal** — never sorted on a
generated id. Assembly, cap enforcement and default-attack precedence belong to **`A15`**, not here.

### 6. Intra-action effect ordering — resolved

**An action applies its own atoms directly at its resolve tick, and they never enter the actor's effect
list.** Not a bypass — a different population:

| Population | What it is | Ordering |
|---|---|---|
| **Standing grants** — items, traits, species passives | effects that *wait* for a trigger | the actor's effect list, priority-sorted |
| **An action's own atoms** | effects that *are* the event | container `seq`, applied as one unit at the resolve tick |

Settled by the atom program's own sealed docs: `definitions.md` says *"An actor attacks. The attack raises
an event. An atom on that actor's effect list responds"* — the attack is not on the list, it is what the
list reacts to. And **all seven triggers are reactive**; there is no `OnActionUsed`, so an action's own
atoms could not be list responders.

Their execution order is `(priority DESC, container_id ASC, seq ASC)`, so **atoms from one container are
contiguous and in `seq` order** — which is exactly the guarantee an action needs, and what makes a
future linkage atom able to read what the atom before it did.

> ⛔ **This direct application is also the "grant path" two shipped comments are waiting for.**
> `resource.delta` D6: *"Battle's sink does handle FA10, but no ATOM can reach it — `BattleEngine` never
> grants and never calls `OnEvent`… **Full again when battle grows a grant path.**"* `shield.grant` D6 says
> the same. **`A5` is the proof that this closes them.**

### 7. Dataflow

```text
author rows (SQLite, server)
  -> compile + push                                            [A6]
  -> action set assembled at run start                         [A15]
  -> IIntentSource selects (action_id, target)                 [A7]
  -> usability: bound -> cooldown -> afford -> range -> cond   [A4]
  -> commit:  validate all costs -> consume all -> roll back on any failure
              acquire slot (if slot_consuming)
              schedule resolve handles                          [kernel, shipped]
  -> resolve: resolve target set per commitment                 [A2]
              for each atom x scope -> apply                    [direct, SS6]
  -> finish:  release slot - start cooldown - schedule recovery [kernel, shipped]
```

### 8. Grid parameters are authored now and inert until `A10`

Range is **not retrofittable**. The columns land now, and:

> **With no board, every range check passes.** Not an error, not an empty result.

**One exception is loud, not silent:** `Mode = Area` needs cells to enumerate, so an area action is
**rejected at bind time** while no board exists.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionModel"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Actions/ActionRow.cs           (the record, closed enums)
src/FusionRpg.Core/Actions/ActionValidator.cs     (every rule in SS9, each naming its row)
src/FusionRpg.Data/Sqlite/RpgStore.Actions.cs     (DDL + DAL for the four tables)
tests/FusionRpg.Core.Tests/Actions/ActionModelTests.cs
tests/FusionRpg.Data.Tests/ActionStoreTests.cs
```

## 9. Testing strategy — every rule rejects a planted row

| Case | Expect |
|---|---|
| Round-trip a row through `RpgStore` | identical |
| Unknown `container_id` | reject, naming the column |
| Unknown `resource_id` | reject — **and the six ids are asserted, not five** |
| `min_range > max_range` | reject |
| Unknown tag, unknown `kind`, unknown `scope` | reject — never coerce |
| A scope naming an atom the container does not hold | reject |
| An atom with no scope row | defaults to `eachTarget` |
| A species row missing any of the three basics | reject, **naming the species** |
| A grant colliding with a basic `action_id` | reject |
| An item granting an action with `grantable = 0` | reject at import — `ActionNotGrantable` |
| `default_attack_eligible = 0` used as a default attack | reject — and a test asserts the two flags are **independent**, by planting a row with `grantable = 1, default_attack_eligible = 0` |
| Resolution order | `action_id` ordinal, asserted against a shuffled input |
| A withdraw by `source` | removes exactly that source's actions, leaving others |
| `rpg_action_grant` schema | a test asserts it has **no `instance_id` column** — the correction, made unforgettable |
| Zero grants | the actor still has all three basics and its innate |

## Boundaries

**Always:** reuse `ValueSpec` and `effect_curve`; keep every enum closed; reject rather than coerce; keep
`rung` an index and never a magnitude.

**Ask first:** adding a column to `rpg_action`; adding an owner scope; changing what `kind` may hold.

**Never:** a second scaling mechanism; a second binding concept beyond the seven owner scopes; storing a
computed magnitude on an action row; sorting a resolution order on a generated id; SQL outside
`FusionRpg.Data`.

## Success criteria

1. A row round-trips, and **every validation rule rejects a planted bad row naming it**.
2. `rpg_action_grant` exists, has no `instance_id`, and reuses the seven owner scopes.
3. The two grant flags are independent, proven by a planted row.
4. An actor with no grants has exactly three basics plus its innate.
5. Six resources are asserted directly, so a five-resource regression is a red test.
