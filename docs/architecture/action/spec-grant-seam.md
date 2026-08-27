# Spec: grant-seam (A15)

**Status: proposed 2026-08-27.** Module **A15** in the [action map](../action-map.md). New module.

Depends on **A1** (the tables and flags) and **A11** (the levelling faucet).

## Objective

**Answer the handshake another program is already blocked on.**

[item/ssot-granted-actions.md](../item/ssot-granted-actions.md) §5.5 is nine numbered items *"written so
`A1` can implement against them and tick them off."* A scan on 2026-08-27 found **six unanswered**, and one
of them was a correction to a shipped claim in `A1`.

> **The item lane did the honest thing: it wrote the contract from its own side and named what it could not
> decide.** This module is the other side of it.

## Design

### 1. The nine items, and where each is answered

| # | Item | Answered |
|---|---|---|
| 1 | A resolvable `action_id` namespace | **`A1`** §2 — PK plus a load-time exists-and-enabled lookup |
| 2 | A per-action `grantable` flag | **`A1`** §2.1 |
| 3 | A per-action `default_attack_eligible` flag, separate from (2) | **`A1`** §2.1 |
| 4 | An action-set **assembly entry point** | **here**, §2 |
| 5 | A grant table that is **not** `effect_binding` | **`A1`** §5 — `rpg_action_grant` |
| 6 | A **named snapshot moment** | **here**, §3 |
| 7 | Written **removal semantics per FSM state** | **here**, §4 |
| 8 | A **cap policy** and its number | **here**, §5 |
| 9 | Written acknowledgement that **per-grant overrides** are never accepted | **here**, §6 |

### 2. Assembly — item 4

> *"Given an actor and a list of `(source, action_id, grant_role)`, return the ordered action set with
> intrinsic and granted merged, deduped, the default attack resolved, and the cap enforced —
> deterministic, ordinal, never sorted on a generated id. **The item layer must not implement this.**"*

```text
assemble(actor, grants) ->
   intrinsic (species basics + innate)
 U granted   (rpg_action_grant, live rows only)
 -> dedupe by action_id, keeping provenance
 -> resolve default attack by declared precedence
 -> enforce the cap, REJECTING rather than truncating
 -> order by action_id ORDINAL
```

**Provenance is kept, not collapsed.** Two items granting one action produce **one** set entry and **two**
grant rows — so removing one source leaves the action, because the other row is still live.

**Decided from shipped code, not preference:** `CooldownLedger` keys `(ActorKey, CooldownKey)`
(`CooldownLedger.cs:8`), so two "instances" of one action would share one clock regardless. **A schema
that cannot express two independent instances should not pretend to.**

**Default-attack precedence is declared, never emergent:**

1. `armament-primary`'s `default-attack`, if any.
2. Otherwise the species' intrinsic basic attack.

There is no third rung, because `default-attack` is legal only on `armament-primary`, and a two-handed item
occupies primary and reserves secondary — so it cannot conflict with itself. **An unarmed actor keeps the
species attack.**

**An item granting an action the species already has** is deduped and **reported, not silently swallowed** —
the player must be able to tell that line of the item is doing nothing *for this actor*. Not a rejection:
the same item on a different species is a real upgrade.

### 3. The snapshot moment — item 6

**It must be the one equipment already freezes at.** `phase != Roster` refuses equip
(`UniqueActorService.cs:41-44`), and the action set is assembled **once at run start** and frozen for the
run.

> **One freeze moment, or `(setup, seed, trace)` stops being a complete description of a battle.**

A grant that arrives mid-run does not change the assembled set. It applies at the next run start.

### 4. Removal semantics per FSM state — item 7

The action set is frozen for the run, so removal is a **next-run** concern for the set. What is *not* frozen
is the underlying row's validity, and the kernel must not learn about inventory.

| Actor state when the source is removed | Behaviour |
|---|---|
| `Charging` / `Ready` | nothing — the set is frozen; the action stays usable this run |
| `Committed` / `Resolving` / `Recovering` **mid-action on that action** | the action **completes normally** |
| any | the grant row is marked withdrawn; the **next** assembly omits it |

> ⛔ **No inventory event ever becomes an `InterruptCause`.** The kernel's interrupt causes describe combat,
> and an un-equip is not combat. Letting inventory reach `ActionRunner.Interrupt` would make the timeline's
> exit paths depend on a system that has no clock relationship with it — the two-async-systems invariant, in
> miniature.

### 5. Cap policy — item 8, and the question it re-frames

The item lane asked *"whether a maximum granted-action count exists, what it is, and whether exceeding it
rejects or truncates,"* recommended **reject**, and proposed **8** as *"illustrative, not balanced."*

**The number is not 8, and it is not one number**, because two different scarcities were being conflated:

| Scarcity | Number | Owner |
|---|---|---|
| **Levelling unlocks held** | **10**, tunable | `A11` — the free faucet, capped because it is free |
| **Equipped at once** | **5** skills + 1 innate + 3 basics | `A1` §1 — the real bottleneck |
| **Granted by paid sources** | **uncapped** | §5.1 |

**Exceeding an actual cap REJECTS at equip time, naming the item** — the item lane's recommendation, kept.
*"Honest cost of the cap: a legitimate build can become unequippable. That is the price of refusing to
truncate."* Truncation would silently pick a winner, and the player would never learn which.

**`default-attack` never counts against anything.** It replaces rather than adds.

#### 5.1 Why paid grants are uncapped

[power/ssot-power-scale.md](../power/ssot-power-scale.md) §11.1a, on removing `MaxSlots`: *"The hard cap was
redundant. Scarcity came from the **escalating price**, not from the ceiling."*

> **An uncapped pool grows the choice, never the power** — power per fight is bounded by the equipped set
> regardless of pool size. That is what makes uncapped paid grants safe rather than merely permitted.

⛔ *"Do paid sources share the levelling cap?"* is a **malformed question** (ideal §0.3): a cap cannot
contain an uncapped set. They are different faucets with different limiters.

### 6. Per-grant overrides are never accepted — item 9

> *"If the action layer intends to allow them, the item side needs to know before it ships a six-column
> table — because that is the difference between a seam and a second action system."*

**Written acknowledgement: never.** A grant names an `action_id`, a `source` and a `grant_role`. It carries
no magnitude, no envelope field, no cost row, no target spec.

Two reasons, and the second is structural:

1. An override would make the **content hash** meaningless for actions — the same `action_id` would behave
   differently per item, and `A6`'s registration into E8 could not detect a balance change.
2. It would put a **balance surface inside the item table**, where no action-side validator, budget or
   E9 price can see it. That is not a seam; it is a second action system with worse tooling.

**If an item needs a stronger version of an action, it grants a different `action_id` at a different rung.**
That is what `A12`'s *"one table, many faucets"* is for.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~GrantSeam"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionGrant"
```

## Structure

```
src/FusionRpg.Core/Actions/Grants/ActionSetAssembler.cs   (the entry point, item 4)
src/FusionRpg.Core/Actions/Grants/GrantRemoval.cs         (item 7)
src/FusionRpg.Core/Actions/Grants/CapPolicy.cs            (item 8 - reject, never truncate)
tests/FusionRpg.Core.Tests/Actions/GrantSeamTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| An actor with **no items** | exactly the three basics and its innate — the item lane's own GA1 criterion |
| **Two items granting the same action** | **one** set entry, **two** grant rows |
| Remove one of two sources | the action **stays** |
| An item granting what the species already has | one entry, **and a report** — not silence, not rejection |
| `default-attack` on a non-`armament-primary` item | rejected at import |
| Unarmed actor | keeps the species attack |
| Over the cap | **`TooManyGrantedActions`**, naming the item — **and a test asserts nothing was truncated** |
| **Assembly order** | `action_id` ordinal, asserted against a **shuffled** grant list |
| Un-equip mid-action | the action **completes**; an architecture test asserts **no inventory type reaches `InterruptCause`** |
| A grant arriving mid-run | does **not** change the assembled set |
| Snapshot moment | assembly happens once; a second call in the same run returns the identical set |
| A grant row carrying any magnitude column | **schema test fails** — item 9, made unforgettable |
| `rpg_action_grant` has no `instance_id` | asserted — the `effect_binding` correction |

## Boundaries

**Always:** assemble here, never in the item layer; reject over the cap; keep provenance; freeze at run
start; order ordinal.

**Ask first:** the equipped-set number; adding a `grant_role`; changing the default-attack precedence.

**Never:** a per-grant override; an inventory event as an `InterruptCause`; truncating over the cap;
sorting on a generated id; the item layer implementing assembly.

## Success criteria

1. **All nine handshake items are answered in writing**, and the item lane can tick them off.
2. Two items granting one action produce one entry and two rows.
3. Over-cap rejects, names the item, and truncates nothing.
4. No inventory type can reach the kernel's interrupt path — asserted architecturally.
5. A grant row cannot carry a magnitude, asserted by schema.
