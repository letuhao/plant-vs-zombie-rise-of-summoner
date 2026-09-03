# Spec: `slot-roles`

**Module id:** `slot-roles` · **Program:** [item](../item-map.md) · **Build order:** 3 of 21
**Depends on:** **X1** (`frame`, seedsmith's demon pipeline — resolved, unbuilt)
**Rulings:** D1, D2, D3, D14 · lane [ssot-equip-slots.md](ssot-equip-slots.md)

## Objective

Declare the body: **fifteen equip roles**, two frame vocabularies over one role table, the twelve-role
hybrid core, and an unlock predicate that ships **defaulting to always-open**.

**Users:** modules 6 (`base-types`), 8 (`affix-legality`), 13 and 21 (both generators cap on the
hybrid core), 16 (`sockets`).

## Design

### ⛔ X1 — this module cannot start until `frame` exists

`frame` (`humanoid | plant | hybrid`) **exists on no species type**, and by D19's reasoning it is not
ours to declare — a frame describes a *body*, exactly as an aptitude vector describes a species.
**Resolved 2026-09-03: seedsmith's demon pipeline classifies it** ([item-map.md](../item-map.md) §3.1).

⚠ **Frame must publish independently of theme status.** `spec-demon-themes.md` makes publishing a
theme for a `basis = blocked` demon a **Never** — but a species can lack a *flavour* judgement while
still having a *body*. A species with no frame has no roles, no base types, and cannot be geared.

**And frame is not `Side`.** `DemonSpeciesDef.Side` conflates faction with body
(`DemonSpeciesCatalog.cs:11`), and the shipped roster already breaks it: `peashooterzombie`,
`ironpeazombie`, `cherrynutzombie` and `bucketnutzombie` are zombie-**side** with plant **bodies**.
Deriving frame from `Side` is the failure item-ideal §4 exists to prevent.

### The fifteen roles, and the twelve-role hybrid core

One role table; each frame names the same role in its own fiction, so the affix library is authored
once (§2.2). Weights are integer per-mille of one fully-geared pure frame and sum to 1000.

**D3 drops three roles for hybrid** — `ward-array` (90) + `head-guard` (60) + `sense` (50) = **200‰**,
leaving **800‰ over twelve roles**.

> ⛔ **Enumerate the twelve explicitly. D3's prose names eleven** — it says *"both jewels"* where
> **three** jewel roles are kept ([item-ideal.md](../item-ideal.md) §2g #6). A generator seeded from
> that prose silently drops `jewel-major`, the second-largest non-weapon budget.

| # | The twelve-role hybrid core | ‰ |
|---|---|---:|
| 1 | `armament-primary` | 160 |
| 2 | `core-guard` | 120 |
| 3 | `armament-secondary` | 80 |
| 4 | `jewel-major` | 80 |
| 5 | `manipulator` | 70 |
| 6 | `mantle` | 60 |
| 7 | `girdle` | 60 |
| 8 | `footing` | 50 |
| 9 | `infusion` | 50 |
| 10 | `retinue` | 40 |
| 11 | `jewel-minor-a` | 15 |
| 12 | `jewel-minor-b` | 15 |
| | **total** | **800** |

**This list is a generator input** — modules 13 and 21 cap on it before generating, because I5's
`SetRoleNotUniversal` fires at load and ~1,000 generated sets would trip it.

### D2 — the unlock predicate ships, defaulting to open

Every slot is open from the start. **But the gate exists and defaults to open**, so a later
breakthrough or quest system can close slots without a schema migration or a content re-author.

⚠ **Do not hard-code fifteen-always-open.** The requirement is the predicate, not the outcome.

⚠ **And record what this costs:** `ssot-equip-slots.md` §8.2 names the unlock ladder as the *only*
mitigation for *"gearing a new specimen is a chore"*, and D2 turns it off while D1 declines to own
the problem. The predicate's existence is what makes that reversible.

### D14 — the commander is another unique demon

No 16th slot. **`standard` stays declared and ungenerated** — the same disposition seedsmith gave its
`environment` kind, and for the same reason: the row costs nothing and keeps the shape stable, while
generating into it would make coverage report a partition covered when nothing is there.

### D3's affix-family relocation is an input to module 8

*"The families are not lost, only the slots."* `ward-array`'s shields, `head-guard`'s crit-resist /
crit-damage padding / status-resist / immunity, and `sense`'s accuracy / crit-rate relocate at reduced
`max_tier`, following §4.2's existing pattern. **This module chooses the hosts; module 8 applies
them.**

### D3's frame-mix bonus is module 12's, and its predicate is unusual

`min(humanoidCount, plantCount)` — a **min over two counts**, not a count over one predicate. Named
here because this module owns `frame`; built in module 12.

⚠ §2g requires the count be **weighted by role `budget_permille`**. Unweighted, 6/6 costs ~230‰ of an
800‰ body because concession is cheapest in the lightest roles.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SlotRole"
```

## Project structure

```text
data/seed/items/_registry/roles.v1.json          new — 15 roles + weights, checked in
src/FusionRpg.Core/Items/ItemRole.cs             new — the closed enum + weights
src/FusionRpg.Core/Items/FrameVocabulary.cs      new — role -> (humanoid name, plant name)
src/FusionRpg.Core/Items/SlotUnlock.cs           new — the predicate, default open
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs      EDIT — item_role, item_role_frame
```

## Code style

```csharp
// The predicate ships now and opens everything. D2: "ship it defaulting to always-open" - the gate is
// what makes a later breakthrough/quest system a tuning change instead of a migration. Hard-coding
// `true` here would satisfy v1 and cost a schema change later, which is the outcome D2 declined.
public bool IsUnlocked(ItemRole role, ActorContext actor) =>
    _rule is null || _rule.Evaluate(role, actor);   // no rule configured => open
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_fifteen_role_weights_sum_to_1000` | the per-mille contract |
| `the_hybrid_core_is_twelve_roles_summing_to_800` | ⭐ the generator input, against the explicit list |
| `the_hybrid_core_contains_all_three_jewel_roles` | the eleven-vs-twelve prose defect, asserted |
| `frame_is_never_derived_from_Side` | §8.6's named failure; the four Fusion hybrids are the fixture |
| `a_species_with_no_published_theme_still_has_a_frame` | X1's blocked-clause carve-out |
| `every_slot_is_open_with_no_rule_configured` | D2 |
| `a_configured_rule_can_close_a_slot_without_a_migration` | the predicate is real, not decorative |
| `standard_is_declared_and_nothing_generates_into_it` | D14 |
| `each_role_has_a_name_in_both_frame_vocabularies` | one table, two vocabularies |

## Boundaries

**Always:** enumerate the twelve hybrid-core roles explicitly wherever they are used as a generator
input; keep weights in the registry, not in code.

**Ask first:** changing a role weight (it moves every downstream budget); adding or removing a role.

**Never:** derive `frame` from `Side`. Never hard-code the unlock outcome instead of the predicate.
Never generate into `standard`.

## Success criteria

- [ ] Fifteen roles, two vocabularies, weights summing to 1000.
- [ ] The twelve-role hybrid core is enumerated explicitly and sums to 800‰, including all three jewels.
- [ ] `frame` is consumed from the demon pipeline, never computed from `Side`, and exists for every
      gearable species including those with no published theme.
- [ ] The unlock predicate ships, defaults to open, and is provably closable without a migration.
- [ ] `standard` is declared and ungenerated.
