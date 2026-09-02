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

### Tags live on the affix, eligibility lives on the container

```text
Affix.tags        : { element: fire, family: combat.penetration, theme: elemental }
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
src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs   new — the tag-match + allow/deny resolution
tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs  new
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

## Boundaries

**Always:** resolve `deny` before `allow` before tags; reject an unsatisfiable eligibility rule at
load, never at roll time.

**Ask first:** adding a new tag AXIS the eligibility rule can query (beyond the affix's own `tags_json`
keys) — that widens what a feature pipeline may express.

**Never:** let a container fork a copy of a shared affix just to change its eligibility — that is what
`allow`/`deny` exists to avoid.

## Success criteria

- [ ] A container's drawable pool is exactly (tag-eligible ∪ allow) − deny, proven by test.
- [ ] Two independent features share one affix library entry with different eligibility, no forking.
- [ ] An unsatisfiable eligibility rule is a load-time rejection, never a silent under-fill.
