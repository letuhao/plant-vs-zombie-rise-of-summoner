# Spec: `affix-library`

**Module id:** `affix-library` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 3 of 10
**Depends on:** `affix-schema` (module 1) · **Model calls:** none, ever

## Objective

Module 1 made `effect_container_pool` reference **affixes**, not bare atoms. That is a breaking change
for every existing single-atom pool row unless something produces the trivial wrapper automatically.
**This module is that generator** — the same shape as `atom-family-library.md` §2's own rule ("do not
hand-author 196 channel families... one family per combat family, with the element slot as the
`variant` column"), applied one level up: **do not hand-author a single-atom affix for every one of the
~980 generated atom rows.** Rule-generate them from the 28 authored combat families instead.

## Design

### The rule, mirrored from the atom layer

`atom-family-library.md` §2 turns 28 authored combat families into ~980 generated atom rows
(`{family_id}[.{variant}].t{tier}`, 28 families × 7 element slots × 5 tiers). This module turns each of
those ~980 atom rows into a **single-atom affix** — an `AffixRow` whose `Refs` is exactly one
`AtomRef` naming that atom, no slot, `affix_class` derived from the atom's `kind_id` (`seed-contract.md`
§2.1, unchanged rule).

```text
atom.elemental-power.fire.t3   -->  affix.elemental-power.fire.t3  (single-atom, class derived)
```

**Not every affix is single-atom, and this module does not generate the rest.** A correlated bundle
("master of fire and ice" — two families, one shared element choice) or a slot-bearing affix
("`element master of X`" — one family, the variant chosen at resolve time, per `definitions.md` §4a) is
an **authored judgement**, not a derivation — that is module 9 (`affix-authoring`)'s job, and Q9
(`effect-pipeline-ideal.md` §7) already closed the split: *"hybrid — single-family affixes
rule-generated from the atom library; multi-atom named affixes LLM-authored, because their identity is
a judgement."*

### Regeneration, not re-authoring

Same payoff as the atom layer: adding a seventh element, or a sixth tier, **regenerates** the
single-atom affix set from the (unchanged) atom rows — it does not touch a single authored file. This
is the same "adding a column costs zero seed files" property `seed-contract.md` §1 states for the atom
layer, one level up.

### Where the boundary actually sits

| | Generated here (zero model calls) | Authored (module 9) |
|---|---|---|
| Shape | exactly one atom ref | two or more refs, or any slot |
| Identity | inherited from the atom's own family/tier/variant | a name and a judgement — *"Master of Fire and Ice"* |
| Regenerates on a new element/tier | yes, automatically | no — an authored affix names its refs explicitly |
| `affix_class` | derived from the one atom's `kind_id` | derived from the union of the bundle's refs (module 1, mixed-bundle rule) |

## Commands

```powershell
dotnet run --project tools/AtomImporter -- --regen-single-family-affixes   # or wherever this hooks in;
                                                                             # see Project structure
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AffixLibrary"
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/AffixLibraryGenerator.cs   new — the rule, pure function over the
                                                               already-loaded atom catalog
tests/FusionRpg.Core.Tests/Atoms/AffixLibraryGeneratorTests.cs  new
```

**Not a seedsmith module.** This generator runs where the atom catalog already lives — C#, at import
time or catalog load, the same boundary `species-generator` (demon-seed module 12) draws for the same
reason: it must call the shipped code, not reimplement it, and there is nothing here for a model to
decide.

## Code style

```csharp
// Mirrors atom-family-library.md §2's own rule one level up: do not hand-author an affix for every
// generated atom row. A single-atom affix's identity is inherited from its one atom - regenerating
// on a new element or tier costs nothing authored.
static AffixRow SingleAtomAffix(AtomRow atom) =>
    new(AffixId: "affix." + atom.AtomId["atom.".Length..], Refs: new[] { new AtomRef(atom.AtomId) },
        Class: AffixClassOf(atom.KindId));
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_generated_atom_gets_exactly_one_single_atom_affix` | 1:1, no atom left unwrapped |
| `single_atom_affix_class_matches_the_atoms_own_derivation` | same rule as `seed-contract.md` §2.1, applied through the wrapper |
| `adding_a_new_element_variant_regenerates_without_touching_authored_affixes` | the regeneration property, proven not asserted |
| `an_authored_multi_ref_affix_is_never_overwritten_by_this_generator` | the boundary with module 9 holds |
| `zero_model_calls_anywhere_in_this_module` | grepped, matching `commander_effect.py`'s own zero-call convention elsewhere in this repo |

## Boundaries

**Always:** derive `affix_class` from the wrapped atom's `kind_id`, never author it; regenerate rather
than persist a stale single-atom affix list when the atom catalog changes.

**Ask first:** widening what counts as "single-atom" (e.g. a two-atom affix that is still mechanically
derivable, not a judgement) — that changes the module-9 boundary.

**Never:** call a model; hand-author a single-atom affix; let this generator overwrite a module-9
authored affix that happens to share an id pattern.

## Success criteria

- [ ] Every atom the catalog currently generates has a corresponding single-atom affix.
- [ ] The generator makes zero model calls, proven by test.
- [ ] Regenerating over an unchanged atom catalog is byte-identical (same discipline as `species-generator`'s `--check`).
- [ ] An authored (module 9) affix is never silently overwritten by this generator.
