# Spec: `eligibility-tags`

**Module id:** `eligibility-tags` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 8 of 10
**Depends on:** `affix-schema` (module 1), `affix-library` (module 3)

## Objective

Tag-based affix eligibility, plus a per-container allow/deny override — what PoE does (Q6,
`effect-pipeline-ideal.md` §7). A container declares which affix tags it draws from; a per-container
override can allow or deny a specific affix beyond what its tags alone would select. This is what keeps
the affix library **shared** (Q6: *"reconciled... the affix library stays shared and tag-gated... that
is what stops 'elemental mastery' being authored once for items and again for species"*) rather than
forking per feature.

## Design

> **⛔ RE-VERIFIED AND DECIDED 2026-09-03 (owner removed themselves as a gate) — the resolver is
> BUILT; the tag source is what is missing, and it is decided here.**
>
> `EligibilityResolver.DrawablePool` and `.Validate` ship in
> `src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs` (`:60-95`), deny-before-allow-before-tags and
> the `UnsatisfiablePool` check included. **Its only callers are its own tests**
> (`tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs`).
>
> **The gap is `Affix.tags`, which does not exist.** `AffixRow` is
> `(string AffixId, AffixClass Class, IReadOnlyList<AffixRefRow> Refs)` (`ContainerRow.cs:81`) — no
> tags — and `effect_affix` is `affix_id` / `affix_class` / `revision` (`RpgStore.Containers.cs:66-70`)
> — no tags column. The resolver takes tags through a
> `Func<string, IReadOnlyDictionary<string, string>> tagsOf` delegate that **nothing in production
> supplies**.
>
> **DECIDED: an affix's tags are DERIVED from its refs' atoms, exactly as its `class` already is. No
> schema change, no new authoring field.**
>
> ```text
> tagsOf(affixId) := union over the affix's CONCRETE refs of  AtomRow.TagsJson
>                    (a slot ref contributes its slotAtomPattern's family tags, or nothing)
> ```
>
> **Every piece is shipped.** `AtomRow.TagsJson` exists (`AtomRow.cs:40`) and `effect_atom.tags_json`
> is a real column (`RpgStore.Atoms.cs:51`). The 98 authored families **already carry tags** —
> `"tags": ["offensive"]`, `"tags": ["offense", "elemental"]` — and E43 stamps them onto every emitted
> row. So the data exists, is authored, and is content-hashed; it just has no reader.
>
> **Why derived rather than a new `effect_affix.tags_json` column:**
>
> - **It cannot contradict the bundle.** That is the same argument `seed-contract.md` §2.1 makes for
>   `class`, and the same one seedsmith's `derive.py` makes: *"a model that names its own class can
>   contradict the bundle it just picked."* An authored affix tag set could say `elemental` over a
>   bundle of physical atoms; a derived one cannot.
> - **It is where the tags already are.** Adding a column would create a second home for a fact the
>   atom layer already owns — the duplicate-vocabulary defect this program exists to prevent.
> - **It is reversible.** If a *bundle-level* tag is ever needed that no member carries — a theme that
>   emerges from the combination — that is an **additive** column read as a union with the derived set,
>   and it is one migration away.
> - **The safe-direction rule already covers slots.** A slot ref with no resolvable pattern contributes
>   nothing, so the derived set can only ever be too **narrow** — the affix is excluded from a pool it
>   might have qualified for. That is the same direction `OrphanAtoms` deliberately fails in
>   (`ContentValidation.cs:288-291` — *"a safe direction for a non-blocking lint to fail in"*), and it is the safe one: a missing affix is visible, a wrongly
>   admitted one is not.
>
> **What would overturn it:** eligibility needing to key on something no atom knows — the item's frame,
> the owner's class. Those are **container-side** facts and belong in the `eligible` rule, which is
> where this spec already puts them. The tag half stays derived.

### Tags live on the affix, eligibility lives on the container

```text
Affix.tags        : { element: fire, family: combat.penetration, theme: elemental }
                    -- DERIVED from the refs' AtomRow.TagsJson, never authored (see above)
Container.eligible : { requireTags: [element], anyOfTags: [theme:elemental, theme:offense] }
Container.allow    : [ affix.id.explicitly.allowed ]   -- beyond what tags alone select
Container.deny     : [ affix.id.explicitly.denied ]    -- excluded even if tags would select it
```

A container's drawable pool (module 1's validation, "distinct drawable groups") is computed as: every
affix whose tags satisfy the container's `eligible` rule, **plus** `allow`, **minus** `deny`. The tag
rule is the common case; `allow`/`deny` is the escape hatch for the exception that proves it (PoE's own
mechanism — a handful of mods are hand-excluded from specific item bases despite matching every tag).

### Why this is a separate module and not folded into `affix-schema`

Module 1 owns *what an affix is*. This module owns *which containers may draw which affixes* — a
different axis, and one that changes independently: a new feature (module 9's own future callers) adds
eligibility rules constantly without ever touching the affix entity's shape. Keeping them separate is
what lets `species-effects` (demon-seed module 15) declare its own eligibility without a schema change
here.

### Validation, additive to module 1's table

| Check | Detail |
|---|---|
| a container's `eligible` rule selects at least one drawable affix per class it has a non-zero roll for | else `UnsatisfiablePool`, same failure module 1 already names for an empty drawable set |
| `allow`/`deny` reference real affix ids | else reject, same law as every other id reference in this program |
| `deny` always wins over `allow` for the same affix id on the same container | explicit, tested — not "last one wins" by list order |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Eligibility"
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs   SHIPPED — verify only, do not rebuild
src/FusionRpg.Core/Effects/Atoms/AffixTags.cs        new — the derived tagsOf, the production
                                                       supplier EligibilityResolver has never had
tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs  SHIPPED — extend, do not replace
```

## Code style

```csharp
// deny always wins - an explicit exclusion is a stronger authoring signal than a tag match, and
// PoE's own item-specific mod exclusions are exactly this shape.
static bool IsEligible(AffixRow affix, EligibilityRule rule) =>
    rule.Deny.Contains(affix.AffixId) ? false
    : rule.Allow.Contains(affix.AffixId) ? true
    : TagsMatch(affix.Tags, rule);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `tag_match_selects_the_eligible_set` | the common case |
| `allow_admits_an_affix_the_tags_alone_would_exclude` | the escape hatch, positive direction |
| `deny_excludes_an_affix_the_tags_alone_would_include` | the escape hatch, negative direction |
| `deny_wins_over_allow_for_the_same_affix` | explicit precedence, not list order |
| `an_unsatisfiable_eligible_rule_rejects_at_load` | same law as module 1's empty-drawable-pool check |
| `two_features_declare_independent_eligibility_over_the_same_shared_affix` | Q6's own reconciliation, proven — "elemental mastery" authored once, eligible for both |
| `affix_tags_are_the_union_of_its_refs_atom_tags` | the derived tag source, over a real multi-ref bundle |
| `a_slot_ref_with_no_resolvable_pattern_narrows_rather_than_widens` | the safe direction, asserted rather than assumed |
| `the_production_tagsOf_is_wired_and_not_only_a_test_delegate` | the gap this module actually closes |

## Boundaries

**Always:** resolve `deny` before `allow` before tags; reject an unsatisfiable eligibility rule at
load, never at roll time.

**Ask first:** adding a new tag AXIS the eligibility rule can query (beyond the affix's own `tags_json`
keys) — that widens what a feature pipeline may express.

**Never:** let a container fork a copy of a shared affix just to change its eligibility — that is what
`allow`/`deny` exists to avoid.

## Success criteria

- [ ] A container's drawable pool is exactly (tag-eligible ∪ allow) − deny, proven by test.
- [ ] `tagsOf` has a **production** supplier, derived from `AtomRow.TagsJson`, with no `effect_affix`
      schema change (decided 2026-09-03).
- [ ] Two independent features share one affix library entry with different eligibility, no forking.
- [ ] An unsatisfiable eligibility rule is a load-time rejection, never a silent under-fill.
