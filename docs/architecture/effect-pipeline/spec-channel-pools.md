# Spec: `channel-pools`

**Module id:** `channel-pools` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 12 of 12
**Depends on:** `eligibility-tags` (module 8), `affix-power-class` (module 11)
**Added:** owner decision 2026-09-03 — the L0 layer, [effect-pipeline-ideal.md](../effect-pipeline-ideal.md) §5.6

## Objective

Turn one affix pool into many. Given a container, the **channel** delivering the effect, and the
container's rarity, produce the **weighted** candidate list L1 draws from — so that a trash drop, a boss
drop, a set completion, a socket, a unique and a craft stop being the same faucet.

This is the deterministic half of L0. [`affix-power-class`](spec-affix-power-class.md) (module 11)
supplies the classification; **this module owns every number**, and none of them is authored by a model.

```text
poolFor(container, channel, rarity)  ->  [ (affixId, weight) ]
```

## Design

### The problem this closes, stated against shipped code

Every lever that differentiates an acquisition channel today is a **volume** lever, never a **kind**
lever: `loot_source` gives each source its own drop table, and `drop_table_entry.rarity_floor` /
`rarity_weight_shift_json` skew which rungs appear (`item/ssot-generation.md` §5.1). **But once a
container is chosen, its affix pool is a property of the container, not of the source** — so a
`plate-helm` from a trash zombie and one from a boss draw the same affixes.

**`AGENTS.md`'s no-hard-ceilings rule is what makes that fatal rather than merely loose.** With a level
cap, an ilvl gate holds forever; with endless grind as the SSOT, **every volume gate eventually opens.**
Enough trash kills reach the boss's outcome, and sets, sockets, uniques, crafting and the world map
become redundant paths to something the player was getting anyway.

### The policy is a small table, and it is the balance surface

Two closed enums in, one integer out:

```text
data/tuning/affix-channels.v1.json
             drop     boss      set     socket   unique    craft
 filler      1000     1000        0       800        0      1000
 notable      600      900      400      600        0       900
 potent       150      600      900      300        0       700
 defining      20      300     1000      100        0       400
 pinnacle       1       80     1000       40        0       150
```

**5 power classes × 6 channels = 30 cells.** Illustrative starting values, and they live in
`data/tuning/` because they *are* the balance surface — `tunables-ssot.md`'s test applies exactly:
*would a balance pass ever want to change this number?* Every cell, constantly.

⭐ **This is the "orthogonal axes beat a long flat list" pattern the roster research measured**
(`research/game-design/03-roster-scale.md` §1 — Ragnarok's 27 authored values across four axes producing
417 realised identities). Thirty hand-tunable cells govern the availability of ~980 affixes across six
channels. A per-container authored pool would be tens of thousands of rows and unbalanceable.

### The six channels (owner, 2026-09-03)

| `channel` | Delivered by | Supplied by |
|---|---|---|
| `drop` | an affix rolled on an ordinary dropped item | `item` module 11 `drop-volume` |
| `boss` | an affix rolled on an item from an elite or boss source | `item` module 11 `drop-volume` |
| `set` | a set-completion bonus | `item` module 13 `set-charm-gen` |
| `socket` | a socket insert or a resonance combination | `item` module 16 `sockets` |
| `unique` | fixed on a hand-authored unique | `item` module 17 `uniques` |
| `craft` | produced by crafting or rerolling | `item` module 15 `enhance-reroll` |

**The channel is a call-site fact, never stored on the affix.** The same affix is reachable through
several channels at different rates — that is the entire mechanism. Storing a channel on the affix would
make it single-source and rebuild the problem one level down.

### ⭐ Three properties that are requirements, not observations

**1. It consumes no RNG.** `poolFor` is a pure function of `(container, channel, rarity)`. It *composes*
the candidate list; L1's existing `affix.draw` stream draws from it.

This is the whole reason a fifth layer can be added to a shipped resolver at all.
`effect-pipeline-ideal.md` §5.4 warns that adding a layer **shifts every historical roll**, because
`CatalogRevision` detects a *content* change and not a change in how many numbers the resolver consumed
— so every owned item would silently re-resolve differently on replay. **L0 escapes that only by drawing
nothing.** If this module ever rolls, it acquires that fragility and the escape is gone.

**2. It extends `eligibility-tags`, never replaces it.** Module 8 answers *may this affix appear here*
(binary: `(tag-eligible ∪ allow) − deny`). This module answers *at what rate*. **Binary allow/deny
cannot express 0.01%**, which is the point. The composition order is fixed:

```text
1. eligible := EligibilityResolver.DrawablePool(catalog, tagsOf, container.Eligible)   [module 8]
2. weighted := eligible.Select(a => (a, policy[powerClassOf(a), channel]))             [this module]
3. weighted := weighted.Where(w => w.weight > 0)                                        [drop zeroes]
4. Draw(weighted, rollSeed)                                                             [L1, shipped]
```

An affix module 8 denies is never weighted; a weight of zero removes it from the draw without ever
looking like an eligibility decision. **The two mechanisms stay legible because they answer different
questions.**

**3. A `drop`-channel weight may be vanishingly small and may never be zero.** Owner, 2026-09-03. The
floor is a **named minimum in the tuning file**, so a balance pass cannot silently write a zero.

> This is `item-ideal.md` **D7** stated from the other direction, and together they are one principle:
> **there is always a path, and the path costs the right thing.** D7: crafting is gated by cost, never
> luck — *"don't make it impossible by chance, that is not fun."* Here: the strongest affixes are
> near-zero from trash and reliable through the channel that exists *for* them.

⚠ **`unique` is structurally zero across every class, and that is legitimate.** A hand-authored unique's
affixes are fixed, not rolled — *"a unique may break every rule that lives in the generator, and no rule
that lives in the machine"* (`item/ssot-uniques.md`). Per AGENTS.md a structural limit is exempt from the
no-ceilings rule **and must say so in a comment**: this is a content-availability rule, not a cap on a
magnitude.

### The seam into the shipped resolver — additive, never a signature change

`Instantiator.Draw(container, lookupAtom, lookupAffix, rollSeed)` is **shipped and called in production**
(`RpgStore.UniqueActors.cs:756`), and it reads `container.Pool` directly.

**Add an overload; do not change the existing signature.**

```text
Draw(container, lookupAtom, lookupAffix, rollSeed)                    // SHIPPED — delegates, unchanged
Draw(container, lookupAtom, lookupAffix, rollSeed, weightedPool)      // new — L0 supplies the pool
```

The four-argument form delegates to the five-argument form passing `container.Pool`, so **every existing
caller behaves byte-identically and no golden moves.** A caller that knows its channel passes a composed
pool; one that does not gets today's behaviour. That is what makes this landable against live data.

### Why not materialise per-channel pool rows

Rejected: writing `effect_container_pool` rows per `(container, channel)`.

It multiplies every container's pool rows by six, and with ~1,844 generated sets (`item-ideal.md` D12)
plus the item corpus that is a large, redundant, hand-unreviewable table whose every row is derivable
from thirty tuning cells. **A generated row nobody can diff is a row nobody can review**
(`item/seed-contract.md` §1). Computing at roll time keeps one policy, one place, one diff — and costs
nothing, because the composition is a dictionary lookup per candidate.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ChannelPool"
python scripts\audit-magic-numbers.py --targets M1   # the policy table must not be in code
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/AffixChannel.cs          new — the six-value closed enum
src/FusionRpg.Core/Effects/Atoms/ChannelPoolPolicy.cs     new — (powerClass x channel) -> weight,
                                                            loaded from data/tuning, no literals
src/FusionRpg.Core/Effects/Atoms/ChannelPoolComposer.cs   new — poolFor(container, channel, rarity)
src/FusionRpg.Core/Effects/Atoms/Instantiator.cs          ADD an overload; existing signature unchanged
src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs       SHIPPED — consume, do not modify
data/tuning/affix-channels.v1.json                        new — 30 cells + the drop floor
```

## Code style

```csharp
// The drop floor is a NAMED minimum, not a literal: AGENTS.md forbids a balance number in code, and
// the owner's rule is that a drop channel may be vanishingly rare and never impossible (D7's twin).
var weight = _policy.Weight(powerClass, channel);
if (channel == AffixChannel.Drop)
    weight = Math.Max(weight, _policy.DropFloorWeight);

// `unique` is zero at every class BY CONSTRUCTION - a unique's affixes are fixed, never rolled
// (ssot-uniques.md). Structural limit, exempt from the no-ceilings rule, and this comment is the
// exemption AGENTS.md requires.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `pool_for_returns_weights_from_the_policy_table` | the core mapping |
| `the_same_affix_has_different_weights_in_different_channels` | ⭐ the mechanism, stated as a test |
| `a_pinnacle_affix_is_near_zero_on_drop_and_high_on_set` | the owner's own worked example |
| `a_drop_channel_weight_is_never_zero` | the floor, over **every** power class |
| `the_drop_floor_comes_from_tuning_not_from_a_literal` | `audit-magic-numbers` would not catch a `Math.Max(w, 1)` |
| `unique_channel_is_zero_at_every_class` | structural, deliberate, commented |
| `pool_for_draws_no_random_numbers` | ⭐ **the property the whole layer rests on** — assert the RNG is never advanced |
| `composing_then_drawing_matches_the_shipped_draw_when_the_policy_is_flat` | a flat policy is a no-op; **no golden moves** |
| `the_existing_four_arg_Draw_is_byte_identical_after_the_overload_lands` | live-data safety, asserted not assumed |
| `an_affix_denied_by_eligibility_is_never_weighted` | composition order, module 8 first |
| `a_zero_weight_removes_from_the_draw_without_looking_like_a_denial` | the two mechanisms stay distinct |
| `an_unclassified_family_is_weighted_as_notable_and_reported` | module 11's visible default, honoured here |
| `an_all_zero_weighted_pool_rejects_as_UnsatisfiablePool` | reuses module 1's existing failure, at load |
| `the_policy_table_has_a_cell_for_every_class_x_channel` | 30 cells, no silent default |

## Boundaries

**Always:** run module 8's eligibility first, then weight; keep the policy in `data/tuning/`; keep
`poolFor` free of RNG; add an overload rather than change a shipped signature.

**Ask first:** adding a seventh channel (`world` is the expected next one and is deliberately absent
until the world map exists — a channel nothing supplies would make coverage report a partition covered,
the same trap seedsmith named for its `environment` kind); making any `drop` cell zero.

**Never:** draw a random number in this module. Never store a channel on an affix — the same affix must
be reachable through several channels at different rates, and storing one makes it single-source. Never
write a weight into code. Never let a zero weight substitute for an eligibility denial, or the two
mechanisms stop being separately debuggable.

## Success criteria

- [ ] `poolFor(container, channel, rarity)` returns weighted candidates and **advances no RNG**, proven
      by test.
- [ ] The same affix demonstrably resolves at different rates in `drop` versus `set`.
- [ ] No `drop` cell is zero, at any power class, enforced by test against the tuning floor.
- [ ] A flat policy reproduces today's draw **byte-identically** — zero goldens move.
- [ ] The existing four-argument `Draw` is unchanged and its production caller is untouched.
- [ ] The 30-cell policy lives in `data/tuning/` and `audit-magic-numbers.py` reports no new M1 target.
