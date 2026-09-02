# Spec: `affix-schema`

**Module id:** `affix-schema` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 1 of 10
**Depends on:** — (foundation module)

## Objective

Make the affix/slot layer and the prefix/suffix split **real in code**. The design is not new: it was
written normatively into `effect-atom/definitions.md` §4a and `effect-atom/spec-container-schema.md`
on 2026-09-01 (`seed-to-concrete` T0.4/T0.5/T0.6) — those two documents **win over this spec** wherever
they disagree with it, per this repo's own doc-precedence rule. **This module's job is the
implementation gap those docs left behind**, verified against the real schema on 2026-09-02:

| Already normative in docs | Exists in code today |
|---|---|
| `prefix_rolls` / `suffix_rolls` columns replacing `pool_rolls` (`spec-container-schema.md:27`) | **no** — `effect_container` still has one `pool_rolls INTEGER` (`RpgStore.Containers.cs:27`) |
| `affix_class` on `effect_container_pool` (`spec-container-schema.md:48`) | **no** — the table has `container_id`/`atom_id`/`weight`/`group_key` only (`RpgStore.Containers.cs:42-48`) |
| a `slot` declaration and an **affix bundle** as the pool's roll unit (`definitions.md:171-202`) | **no** — `ContainerPoolRow` references one bare `atom_id` (`ContainerRow.cs:32`) |
| mixed-bundle affixes consume one prefix roll **and** one suffix roll (`spec-container-schema.md:91`, A1's fix) | **no** — no bundle concept exists to consume anything |

Success is `effect_container_pool` referencing **affixes** (bundles of atom refs, which may include
slots) instead of bare atoms, `effect_container` carrying `prefix_rolls`/`suffix_rolls` instead of one
`pool_rolls`, and every validation rule `spec-container-schema.md` already specifies actually enforced.

## Design

### What is genuinely new here vs. what is a straight port

**Straight port from `spec-container-schema.md`** (already fully specified, this module just builds
it): the `prefix_rolls`/`suffix_rolls` split, `affix_class` on pool rows, the per-class
one-per-`group` rule, the `PoolRollsExceedGroups` validation family, the rarity/tier axis split.
Read that document's Design and Testing strategy sections directly — they are not repeated here.

**New in this module** (design lives in `definitions.md` §4a, not yet expressed as a schema): the
**slot** and the **affix** entity.

```text
slot E1 : domain = element, pick = 1
atom ref: atom.elemental-power.$E1
```

A slot is a parameterised atom reference on an affix — it names a domain (e.g. `element`) and a pick
count rather than one concrete atom. **The atom catalog does not change**: `atom_id` derivation and its
unique key `(family_id, tier, variant)` stay exactly as they are. Only the affix's *reference* becomes
parameterised, and only at the container/affix level — never in the atom table.

```text
Container.Pool references Affixes, not bare atoms.
Affix = { affix_id, refs: [AtomRef | SlotRef], affix_class: prefix | suffix | mixed }
AtomRef  = { atom_id }
SlotRef  = { slot_name, domain, pick }
```

An affix bundle is what makes *"master of fire and ice"* (`definitions.md:196-198`) expressible: four
atoms, two families, one element choice shared across both families, drawn together as **one roll**.
Today's `effect_container_pool` draws one atom per row and cannot correlate two independent draws —
that is the exact gap this table closes.

### Mixed-class bundles (A1, closed 2026-09-01)

An affix bundle may carry refs of both prefix-kind and suffix-kind atoms (e.g. `+X fire defense`,
`stat.derived`/prefix, alongside `burn attackers on hit`, `status.apply`/suffix — the `effect-
pipeline-ideal.md` A1 example). Per the map's §7 verdict and `spec-container-schema.md:91`: **a mixed
bundle consumes one prefix roll and one suffix roll simultaneously, never doubling either count.** This
is well-defined and needs no new authored field — `affixClass` is still derived per-atom from `kindId`
(`item/seed-contract.md` §2.1), and the bundle's own class is the union of what its members derive to.

### Validation — additive to `spec-container-schema.md`'s existing table

| Check | Detail |
|---|---|
| every slot's domain resolves for **every** eligible member at load | a missing element row is a load-time rejection, never a roll-time surprise (`definitions.md:182-183`) |
| an affix's refs are internally distinct | no atom appears twice in one bundle |
| an affix's `affix_class` is derived, never authored | same `seed-contract.md` §2.1 rule the atom-level derivation already follows — **present in a seed file → reject** |
| a mixed-class affix is charged against **both** `prefix_rolls` and `suffix_rolls` in the drawability check | A1 |

Everything `spec-container-schema.md`'s own Testing strategy table already lists (unsatisfiable pools,
zero-weight groups, duplicate atoms, negative weight, override validation) applies unchanged — affixes
sit **above** that layer, they do not replace it.

### `core` maps to the fixed core, not to a weight (A2, closed 2026-09-01)

An affix marked `core` by a feature's authoring pipeline (see module 9, `affix-authoring`) belongs in
`effect_container_atom` — **always present** — never in the weighted pool with a large weight. A weight
cannot express "always"; twelve `core` affixes against `prefix_rolls = 3` would silently mean nine of
them never appear, with no error, because the container stays valid. The fixed core needs its own
rarity-scoped budget for this reason (a rung-1 container could otherwise carry five guaranteed effects
while its pool says 0-1) — that budget is a `data/tuning/` table each feature owns (module 9's
concern), not a schema change here.

### Migration: eight seed files, not 149

`data/seed/` holds 149 committed seed files; eight declare `poolRolls` today
(`effect-pipeline-map.md` §6, "How big is the split, measured"). Renaming `poolRolls` to
`prefixRolls`/`suffixRolls` (with every existing pool row's `affixClass` derived from its `kindId`,
matching the existing item authoring convention — no row needs hand-editing beyond the field rename)
touches exactly those eight. **Timing is the argument for doing this now**: after `affix-authoring`
(module 9) ships content, this becomes a migration of everything ever authored instead of eight files
in one sitting.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContainerStore"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~(ContainerValidator|Affix)"
.\scripts\guard-dal.ps1
python scripts/audit-magic-numbers.py --targets M1   # rarity's prefix/suffix bands are tuning, not code
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs        edit — prefix_rolls/suffix_rolls, affix_class,
                                                          a new effect_container_affix table
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs         edit — AffixRow, SlotRef, ContainerRow gains
                                                          PrefixRolls/SuffixRolls (replaces PoolRolls)
src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs   edit — the additive checks above
src/FusionRpg.Core/Effects/Atoms/AffixResolver.cs        new — slot domain resolution at load time
tests/FusionRpg.Data.Tests/ContainerStoreTests.cs        edit
tests/FusionRpg.Core.Tests/Atoms/ContainerValidatorTests.cs  edit
tests/FusionRpg.Core.Tests/Atoms/AffixResolverTests.cs   new
data/seed/**/*.json                                      edit — the eight poolRolls-declaring files
```

## Code style

```csharp
// The affix is the pool's roll unit, not the atom (definitions.md §4a). A slot is a parameterised
// ref inside an affix — the atom catalog and its unique key never change, only how a container
// REFERENCES one.
public sealed record SlotRef(string SlotName, string Domain, int Pick);
public sealed record AffixRow(string AffixId, IReadOnlyList<AtomRef> Refs, AffixClass Class);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `prefix_rolls_and_suffix_rolls_replace_pool_rolls_end_to_end` | a container round-trips through `RpgStore` with the new columns; `PoolRolls` is gone from `ContainerRow` |
| `affix_bundle_resolves_all_its_refs_together` | "master of fire and ice"-shaped fixture: one draw yields all four correlated atoms |
| `slot_domain_missing_a_member_rejects_at_load` | not at roll time |
| `mixed_class_affix_consumes_one_prefix_and_one_suffix_roll` | A1 |
| `core_affinity_never_lives_in_the_weighted_pool` | a guard test over the fixed-core / pool split, A2 |
| `eight_migrated_seed_files_still_validate` | the real migration, not a fixture |
| every existing `ContainerValidatorTests` case | still passes unchanged — this module is additive |

## Boundaries

**Always:** treat `definitions.md` §4a and `spec-container-schema.md` as the winning design; keep the
atom catalog and `atom_id` derivation unchanged; reject a bad affix whole, with its id and reason.

**Ask first:** widening the `container_kind` enum; changing the one-per-group rule; changing the
rarity/tier split.

**Never:** let a model author `affixClass` (derived, per `seed-contract.md` §2.1); put activation,
cooldown, or targeting in these tables; let rarity change an atom's magnitude.

## Success criteria

- [ ] `effect_container` has `prefix_rolls`/`suffix_rolls`; `pool_rolls` is gone.
- [ ] `effect_container_pool` rows reference affixes; a bundle draws as one correlated unit.
- [ ] Every check in `spec-container-schema.md`'s Testing strategy table passes against the new schema.
- [ ] The eight seed files declaring `poolRolls` migrate and still validate.
- [ ] A1 and A2 are each closed by a named test, not by inspection.
