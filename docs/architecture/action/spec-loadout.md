# Spec: loadout (A16)

**Status: proposed 2026-08-27.** Module **A16** in the [action map](../action-map.md). New module, opened by
**audit C3** — the equip step had no module, no table, no persistence and no validation.

Depends on **A11** (the held pool) and **A12** (the rung, for auto-equip). Feeds **A15**'s assembly.

## Objective

**Which of your held actions are active this run.**

`A11` says how many you *hold*. `A15` says how items *grant*. Neither says which are **equipped** — and that
is the scarcity the whole design leans on:

> [action-ideal.md](../action-ideal.md) §3.4's argument that uncapped paid pools are safe is *"an uncapped
> pool grows the choice, never the power"* — **which is only true if something bounds the choice.** Nothing
> did.

## Design

### 1. The equipped set

| | Count | Chosen? |
|---|---|---|
| **Basic** — attack · guard · move | 3 | no — intrinsic, always present |
| **Innate** | 1 | no — the demon type's, always present |
| **Earned skill** | **5** | **yes — this module** |

`rpg_actor_loadout(owner_kind, owner_key, ordinal, action_id)`, reusing `A1`'s seven owner scopes rather
than inventing an eighth.

**`ordinal` is the display and tie-break order**, not a priority. `A7` chooses on tags; it does not read
loadout order as preference, or the loadout becomes a hidden AI script.

### 2. Validation — four rules, each rejecting

| Rule | Reason code |
|---|---|
| At most **5** skill entries | `LoadoutFull` |
| Every entry is **held** (in `A11`'s pool or granted per `A15`) | `ActionNotHeld` |
| No duplicate `action_id` | `DuplicateInLoadout` |
| No `kind = basic` or `kind = innate` entry | `IntrinsicNotEquippable` — they are never in the set, so putting one there is a category error, not a waste of a slot |

**Rejects, never truncates.** Same rule `A15` §5 applies to the granted cap, and for the same reason:
truncation silently picks a winner and the player never learns which.

### 3. ⛔ Auto-equip — this is what makes Zomboss a real opponent

**Owner, 2026-08-27:** *"new loadout + random equipment prefer stronger action, so that can help Zomboss.
We will extend the auto mechanism in the future — use power scale for now."*

**Every actor needs a loadout, and only one of them has a player.** Zomboss patterns, generated demons,
wild encounters and any actor an AI drives must arrive equipped — so auto-equip is not a convenience, it is
what stops non-player actors from fighting with three basics.

```text
autoEquip(actor) =
    candidates = held actions, skill kind only
    rank by POWER SCALE, descending          # E9 PowerVector -> PowerScalar, or the rung as a proxy
    take 5, breaking ties by action_id ordinal
```

**Power scale is the stand-in, and it is named as one.** `PowerScalar.Of` is a geomean over E9's five
categories and the power SSOT §10.2 row 13 records it as **display-only with no production caller** — this
would be its first. That is acceptable for a v1 preference and **not** acceptable as a balance input, so:

> **Auto-equip reads power to ORDER candidates. It never feeds a number back into balance.** The ranking
> is a selection, not a magnitude, so PS-4 does not apply and nothing downstream reads the score.

**Why "prefer stronger" is the right v1 and not a placeholder to apologise for:** it is the same heuristic a
player uses before they learn combos, it is deterministic, and it needs no model. The named upgrade path is
a smarter mechanism later — one that reads `A13`'s **enabler/payoff pairings** so an AI can assemble a combo
rather than five unrelated big numbers.

⚠️ **A pure "take the 5 strongest" loadout is exactly the corner the class system's dominance matrix cannot
see** (ideal §8.6): that matrix compares allocations, not loadouts. So auto-equip's output is **recorded in
the battle report** alongside the allocation, or a dominant auto-loadout is invisible to the guard built to
find dominant builds.

### 4. The freeze moment is `A15`'s, not a second one

The loadout is **an input to assembly**, not a parallel snapshot. `A15` freezes the action set at run start;
this module supplies the selection that assembly reads.

> **One freeze moment, or `(setup, seed, trace)` stops being a complete description of a battle.**

**Changing a loadout mid-run is refused**, with the same shape as the shipped equip gate (`phase != Roster`)
and the same shape as `A11`'s discard rule. Three features, one rule — not three rules.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Loadout"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Loadout"
```

## Structure

```
src/FusionRpg.Core/Actions/Loadout/LoadoutSet.cs      (the 5, validation, reason codes)
src/FusionRpg.Core/Actions/Loadout/AutoEquip.cs       (power-ranked; deterministic tie-break)
src/FusionRpg.Data/Sqlite/RpgStore.Loadouts.cs
tests/FusionRpg.Core.Tests/Actions/LoadoutTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| A 6th skill entry | **`LoadoutFull`**, rejected — **and a test asserts nothing was truncated** |
| An entry the actor does not hold | `ActionNotHeld` |
| A `basic` or `innate` in the set | `IntrinsicNotEquippable` — a category error, not a full-slot error |
| An actor with **fewer than 5 held** | valid; a short loadout is legal, not padded |
| An actor with **no loadout row at all** | **auto-equips** — a Zomboss pattern must never fight with three basics |
| Auto-equip determinism | same held set, two runs → identical 5, **and identical across a shuffled input order** |
| Auto-equip tie-break | equal power → `action_id` ordinal, asserted with two deliberately equal-power actions |
| Loadout change mid-run | refused with a typed reason; the assembled set is unchanged |
| The power score | **never read by anything except the ranking** — an architecture test, because PS-4's blast radius is why row 13 says display-only |
| Auto-equip output | present in the battle report beside the allocation — asserted, because a dominant auto-loadout is otherwise invisible to the dominance guard |
| `ordinal` | display order only; `A7` produces the same choices under a reordered loadout |

## Boundaries

**Always:** reject rather than truncate; auto-equip any actor with no loadout; keep the freeze in `A15`;
record the auto-equipped set in the report.

**Ask first:** the equipped count; a smarter auto mechanism; letting `A7` read loadout order.

**Never:** a second freeze moment; a truncating cap; an intrinsic action in the set; the power score reaching
any balance input; a mid-run loadout change.

## Success criteria

1. "5 equipped" is **enforced**, not merely stated in a table.
2. Every actor is equipped, including ones with no player.
3. Auto-equip is deterministic and order-independent.
4. The power score orders candidates and reaches nothing else.
5. One freeze moment across loadout, discard and equip.
