# Spec: element-roster-data (E18)

Module **E18** in the [atom effect map](../effect-atom-map.md). Depends on **E4**, **E8**. **Sequenced between E11 and E9** — E10's matchup read consumes its two matrix tables, so it is not optional. **It does not move goldens** — the roster is unchanged, so the generated channel set is unchanged. Checkpoint E stays E12's alone.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Move the element roster and both matchup matrices from hardcoded enum and `switch` into rows, so adding an element is **rows plus regeneration** rather than a code change across five files.

## Design (locked on approval)

### Why elements qualify as data, and channel families do not

The program's rule: *a thing may be data if adding a row changes behaviour without new code.*

Elements pass cleanly. The 84 combat channels are **generated** from families × roster, and `CombatDerivedReader` reads them **by pattern, not by name**. A seventh element regenerates its 12 channels and every existing consumer picks them up with **no new code**.

The 12 channel **families** fail the same test — each has a named reader (`Power` → resolver, `ShieldCapacity` → shield runtime, `CritRate` → sigmoid), so a thirteenth added as a row would have no consumer and be dead on arrival. Families stay code. This module changes the roster, not the family list.

### Three tables

**`effect_element`** — `element_id`, `display_name`, `ordinal`, `enabled`, `revision`.

**`effect_element_matrix_combat`** — `attacker_element`, `defender_element`, `relation`, `share_milli`.

**`effect_element_matrix_shield`** — same shape, **separate rows**.

### ⚠️ Two matrices, because they genuinely differ

The shield matrix is **asymmetric with the combat ring**: light and dark are **mutually +1** in shields, so a light attack on a dark shield *and* a dark attack on a light shield both burn 25% harder. The combat ring does not behave that way.

Sharing one table would silently corrupt one of them. Whether that asymmetry is intentional or a mirrored-table slip is a **question for the shield stream** — E18 preserves it exactly as shipped and flags it, rather than "fixing" it into a golden change nobody asked for.

### ⚠️ The ordinal is load-bearing

`ActorElementTypes` is `Fire=0, Ice=1, Air=2, Earth=3, Light=4, Dark=5`, and the enum order drives the generated roster. **A reordered roster silently changes every generated channel id.**

Locked protections:

- `ordinal` is explicit in the table, never inferred from row order.
- Ordinals are **append-only**: an existing element's ordinal may never change, and a retired element's ordinal is never reused.
- A test pins the six shipped elements to their current ordinals, so a reorder fails loudly rather than moving the channel set.
- The enum becomes a **generated mirror** of the roster, so code and data cannot disagree.

### The 84-count test becomes a formula

`DerivedStatRegistryTests` asserts exactly `84`. That becomes `families × (roster + omni)` — 12 × 7 today, still 84, but now it tracks the roster instead of contradicting it.

### Content hash

The three tables join the hash (E8). Adding an element changes the hash **and** the channel count together, which means an element addition can never be mistaken for a code regression — and a golden that moves has an attributable cause.

### Element ids stay strict

`ElementRoster.TryParse` accepts only the lowercase names and **rejects numeric strings**; `omni` is not a legal actor element slot; primary and secondary must differ and secondary requires primary. All of that survives — moving to data must not loosen parsing.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Element|DerivedStatRegistry"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Element"
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.Elements.cs               (new — 3 tables)
tools/ElementEnumGen/                                    (new — build-time codegen; a C# enum cannot be
                                                          generated from rows at load. Precedent: tools/DemonCatalogGen)
tools/ElementEnumGen/                                    (new — build-time codegen; a C# enum cannot be
                                                          generated from rows at load. Precedent: tools/DemonCatalogGen)
src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs        (enum becomes generated mirror)
src/FusionRpg.Core/Combat/Element/ElementRingMatrix.cs       (switch → table lookup)
src/FusionRpg.Core/Combat/Shield/ShieldElementMatrix.cs      (switch → table lookup)
src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs      (generation reads the roster)
tests/FusionRpg.Core.Tests/ActorHub/DerivedStatRegistryTests.cs  (84 → formula)
```

## Testing strategy

| Case | Expect |
|---|---|
| Six shipped elements | ordinals pinned; a reorder **fails the test** |
| Channel count | `families × (roster + omni)` = 84 today |
| Combat matrix from rows | identical to the shipped `switch` for all 36 pairs |
| Shield matrix from rows | identical for all 36 pairs, **including the light/dark asymmetry** |
| Add a seventh element in a test fixture | 12 new channels generated; readers resolve them with **no code change** |
| Retired element's ordinal | never reused |
| `TryParse("3")` | still rejected |
| `omni` as an actor slot | still rejected |
| Content hash | changes on any roster or matrix edit |
| Existing goldens | unchanged while the roster is unchanged |

## Boundaries

**Always:** explicit append-only ordinals; two separate matrix tables; generate the enum from the roster; keep parsing strict.

**Ask first:** changing an ordinal; changing a matrix value; "fixing" the shield/combat asymmetry — that is the shield stream's call.

**Never:** infer ordinal from row order; reuse a retired ordinal; share one matrix table; loosen element-id parsing; add a channel family as a row.
