# Spec: `affix-legality`

**Module id:** `affix-legality` · **Program:** [item](../item-map.md) · **Build order:** 8 of 21
**Depends on:** `slot-roles` (3), `rarity-bands` (7) · consumes effect-pipeline `eligibility-tags` (8) and **D28**
**Rulings:** **D29 (the ilvl→tier ladder)**, **D3 (the affix-family relocation)**, D8, D26, §2b.1 · lane [ssot-affixes.md](ssot-affixes.md)

## Objective

Decide **which affix may roll where, and how high**. Three artefacts: the role × affix-group matrix,
the tier bands' ilvl ladder, and the frame/side/runtime filters — plus one derived table,
`item_role_family`, that nobody authors.

**Users:** 9 (`power-reads`), 10 (`item-card`), 11 (`drop-volume`), 13 and 21 (both generators respect
legality), 15 (a reroll draws from `weight > 0` only), 17 (`uniques` break these rules on purpose).

## Design

### ⭐ `item_role_family` is DERIVED — the source already exists and is complete

`ssot-equip-slots.md:429-435` declares `item_role_family(role_id, frame, family_id, max_tier)` and
d4 §5.2 sizes it at **~1,100 hand-authored cells**. §2b.1 resolves it: **derived, with a small
override list.** *"One override beats 1,100 cells."*

**Verified: the derivation source is authored, complete, and already in the corpus.** All **98** affix
families in `data/seed/items/affix-families/` (15 files) carry `roles`, `frames` and `side` — 98 of 98,
no gaps — and together they name **656 legal `(role, family)` pairs** out of `98 × 16 = 1,568`
possible cells.

```text
item_role_family := for each family f in affix-families/*.json
                      for each role r in f.roles
                        for each frame m in f.frames
                          emit (r, m, f.id, maxTierFor(r, f))
                    minus the side filter        (f.side vs the base type's side)
                    minus the runtime filter     (f.kindId's RuntimeSupportMatrix)
```

This is the same decision `eligibility-tags` made for tags and `affix-power-class` made for class —
**derive from the one place the fact already lives**, so a second home cannot contradict it. And it is
reversible in the safe direction: a family that resolves nowhere is *excluded*, so the derived table
can only ever be too narrow, and a missing affix is visible where a wrongly-admitted one is not.

⚠ **Zero of the 98 families carry a `maxTier` field**, so `maxTierFor` is a rule plus overrides, not
data.

### The one override — and §2.5's third pricing mechanism is FALSE in the corpus

`ssot-equip-slots.md:129` caps the twin minor jewels: *"`item_role_family.max_tier = 3` for every
family on both minor jewels, against `5` on `jewel-major`."* That is the only per-`(role, family)`
granularity I2 actually uses, which is exactly why deriving the rest is safe.

**But §2.5 prices the twins with *three* mechanisms, and the third does not hold.** It claims *"the six
strongest families — `bulwark`, `savagery`, and the four shield families — are **absent from the
minor-jewel family list on every frame**."* Measured against the corpus:

| Family | Legal on `jewel-minor-a` / `-b`? | §2.5 says |
|---|---|---|
| `atom.shield-capacity` · `-regen` · `-toughness` · `atom.shld-cycle` · `atom.shld-surge` | **no** | absent ✅ |
| `atom.shield-pen` | **no** | absent ✅ |
| **`atom.bulwark`** | ⛔ **yes, both** | absent ✗ |
| **`atom.savagery`** | ⛔ **yes, both** | absent ✗ |

Two of the six are legal on both twins today. **The override list therefore has two entries, not one:**
`max_tier = 3` on the twins, **and** `bulwark` / `savagery` removed from both twins' family lists —
or §2.5 is wrong and should be amended instead. ⚠ **Module 3 owns `ssot-equip-slots`; this module
reports the mismatch and applies whichever way it is settled.** Note that `classes.v1.json`'s
`_meta.designNotes` independently bars both families from every *implicit* slate because they use the
`More` operator, which is evidence for removing them rather than amending §2.5.

### D29 — the ilvl→tier ladder is settled, and the window rule travels with it

Two tables existed for one thing (d4's collision **C3**). **D29 ships I12's:**

| Tier | Minimum ilvl (**I12, ships**) | I8 §4.6 (**rejected**) |
|---|--:|--:|
| t1 | 1 | 1 |
| t2 | **1** | 12 |
| t3 | **8** | 25 |
| t4 | **18** | 40 |
| t5 | **32** | 60 |

`ssot-generation.md:370-376` against `ssot-affixes.md:453-459`. D29's reason: *"tier saturates and that
is correct… growth past t5 is carried by `contentScale`"*, which is built —
`InstanceProducer.cs:47` computes `ContentScale.Milli(thetaContent)` and multiplies every rolled
magnitude by it. I8's t5@60 delays the last band without adding growth.

⭐ **And the same ruling settles the *window* rule, which is a second collision nobody named.**

| | I8 §3.5 — **sliding window** | I12 §4.2 — **collapsing envelope** |
|---|---|---|
| At ilvl 40+ | offers the top tier and the two below; **t1 falls out** | keeps everything the rarity window allows |
| Rule | `[ilvlCeil-2, ilvlCeil]` | `env.maxTier = min(band.MaxTier, maxTierAt(ilvl))`; `env.minTier = min(band.MinTier, env.maxTier)` |

**I12's travels with I12's table.** Three reasons: it is the rule the pipeline implements and I12 names
it *"the anti-double-gating rule"*; a slide is a ceiling behaviour on the low end, which is what D29
rejected on the high end; and `contentScale` already makes a t1 roll at ilvl 500 a large number, so
dropping t1 removes variety and removes nothing else. **`ssot-affixes.md` §3.5 and §4.6 are stale.**

⚠ **One I12 behaviour must survive into this module:** if the narrowed envelope leaves fewer drawable
groups than the roll count, **narrow the count and record `envelope_narrowed`** — never reject a legal
drop from legal content, and warn the author at import instead of the player at drop.

### D3's relocation — record the inputs, and the prose overstates the work

D3 drops **`ward-array` (90) + `head-guard` (60) + `sense` (50) = 200‰** for hybrid, and states *"the
families are not lost, only the slots"* — their clusters relocate to new hosts at reduced `max_tier`,
with **I2 choosing the hosts** and this module recording the inputs.

**The inputs, measured from the corpus:**

| Dropped role | Families legal there | Groups |
|---|--:|---|
| `ward-array` | **9** | `g-armour` ×4 (`plating`, `carapace`, `arm-riveting`, `arm-welding`), `g-shield-stat` ×5 |
| `head-guard` | **45** | `g-ward` ×7, `g-precision` ×7, `g-evade` ×7, `g-life` ×6, `g-sustain` ×6, `g-on-death` ×7, `g-armour` ×4 |
| `sense` | **45** | `g-precision` ×7, `g-ward` ×7, `g-evade` ×7, `g-affliction` ×7, `g-on-hit` ×7, `g-life` ×6, `g-armour` ×4 |

⭐ **No family is orphaned.** Every family legal on one of the three dropped roles is **already** legal
on at least one of the twelve hybrid-core roles — measured across all 98, count of orphans: **0**.

> **So D3's relocation is not "find new hosts". It is "reduce `max_tier` on hosts that already have
> them."** The families survive the drop untouched; what a hybrid loses is only the 200‰ of budget, and
> the *tier* reduction is what prevents a hybrid recovering the lost breadth for free through
> `core-guard` and `jewel-major`. **D3's prose describes a bigger job than the corpus needs**, and this
> spec records that so module 3 does not go looking for hosts that are already there.

`ssot-equip-slots.md` §4.2's existing pattern is the precedent and the shape: `ward-array`'s shield
families relocate to `core-guard` at **`max_tier = 3`** against `ward-array`'s `5`. Applying the same
reduction to the other two clusters is the override list's third and fourth entries — **module 3
picks the reduced tiers; this module applies them.**

### The prefix/suffix split — shipped, and shipped differently from the ask

`ssot-affixes.md` §5.2 asks for *"two new columns, and both nullable… both NULL ⇒ today's behaviour
exactly"*, and §9.12 raises it as the formal ask-first. **It landed, and it landed `NOT NULL`:**

```text
effect_container  … prefix_rolls INTEGER NOT NULL DEFAULT 0,
                     suffix_rolls INTEGER NOT NULL DEFAULT 0    RpgStore.Containers.cs:28-29
rarity            … prefix_rolls, suffix_rolls                  RpgStore.Containers.cs:57-58
ContainerRow      … PrefixRolls, SuffixRolls                    ContainerRow.cs (record body)
```

So **§5.2's whole NULL semantics are moot** — there is no "both NULL" state and no "one set, one NULL
⇒ rejected" case. The default is `0`, and `0 + 0` is the no-pool container. `pool_rolls` no longer
exists as a column; the sum is the count.

**The class itself is derived, not authored**, exactly as §3.1 recommended:
`AffixValidator.ResolveClass` returns an authored class if present and otherwise derives it from the
concrete refs' kinds, collapsing to `Mixed` when they disagree
(`src/FusionRpg.Core/Effects/Atoms/AffixValidator.cs:131-141`). The corpus authors no `affixClass` and
the seed contract rejects one (`seed-contract.md:301-323`).

### ⛔ `stat.derived` is no longer quarantined — §4.9 is stale, and it is the biggest change

`ssot-affixes.md` §4.9 states the wave-1 constraint at full strength: *"All 12 element/crit/shield
families plus the 4 status-channel families are `stat.derived`, and `stat.derived` is quarantined
`None/None/None` today (D6)"* — 16 of 30 prefix families dead, §9.14 calling the lift *"the single
largest input to this lane."*

**It shipped.** `AtomKindRegistry.cs:255` declares
`RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None)` for the kind declared
at `:226` — Battle **Full**, Lawn **Full**, Sim None, with the executor named in the comment above it
(`AtomDerivedSubsystem` on the injector's `ActorHub` at the order-350 `foundation.effect` slot), and
267 registered derived channels (`:53`).

| Consequence | Where |
|---|---|
| `g.ward`, `g.elem-power`, `g.precision`, `g.evade`, `g.shield-stat` are **live** | §4.1's five ⛔ prefix groups |
| The `RuntimeUnsupported` import rejection for *"every `stat.derived` family in wave 1"* must be **re-derived from `AtomKindRegistry`**, never from the document | §6.1 |
| The same stale fact is baked into a **frozen** registry: `classes.v1.json` excludes 32 families citing D6 | **module 6** owns that repair |
| Sim stays `None`, so a `stat.derived` affix must still be refused for a sim-target container | unchanged |

⚠ **`warding` / `resilience` are a separate rule and are *not* unblocked by this.** G8 is about
`stat.modify` on `defense` at a non-`match` scope, not about `stat.derived`. §4.7's ban stands: they
are legal only on the commander's `standard` slot — which **D14 puts out of scope**, so in practice
they are legal nowhere in v1 and must be refused at **import**, not at bind.

### Tags — module 8 of effect-pipeline is the consumer, and D28 is what makes it true

`eligibility-tags` decided *"an affix's tags are DERIVED from its refs' atoms."* **F5 broke that:**
`AtomRow.TagsJson` carries generator provenance only — `FamilyExpansion.cs:194-198` builds
`{ generatedFrom, generator: "E43" }` and nothing else. The thematic tags exist, but on the affix-family
seed entries: **`offensive` ×41, `defensive` ×40, `utility` ×17** across the 98 families — measured,
and matching §2f.1 F5 exactly.

**D28 fixes the data rather than the derivation:** E43 carries the family's `tags` through alongside
`generatedFrom`/`generator`. This module **consumes** the result and must not build a second tag path.
⚠ **Until D28 lands, every tag-gated rule here is inert** — including D8's aptitude gate, which is
implemented as *"an aptitude affix carries a tag only high rungs admit."*

### D8 — aptitude affixes are rarity-gated, and the mechanism is not free

D8 is legal here and needs nothing new from this module's matrix: `eligibility-tags`' per-container
allow/deny is the gate. **But §2f.2 amended D8 twice and both amendments land on someone:**

- an aptitude affix grants a **share delta**, not points (share-normalised aptitudes decay as
  `P(Θ)/T²`, ~8× from Θ=20 to Θ=200);
- it needs **a 13th atom kind or an `aptitude.*` channel family, and a fifth `AllocationScope`** —
  reviewed vocabulary changes owned by **effect-atom** and **class-system** (§2g #2).

**So no aptitude affix may be authored into a pool until §2g #2 clears.** Recorded here because the
matrix is where someone would try.

### The matrix — 15 groups × 15 roles, re-issued

`ssot-affixes.md` §4.3's matrix is written against **12** old role ids plus `standard`. The binding set
is the **15** in `data/seed/items/_registry/core.v1.json`. Mapping: `core-protective → core-guard`,
`head-protective → head-guard`, `manipulator-offense → manipulator`, `mantle-utility → mantle`,
`girdle-resource → girdle`, `sense-utility → sense`, `jewel-minor → jewel-minor-a` + `jewel-minor-b`;
and **three roles are new with no old row** — `ward-array`, `infusion`, `retinue`. Each added role is
one row of 15 numbers.

⚠ **`standard` keeps its row and generates nothing** (D14) — the same disposition seedsmith gave its
`environment` kind. A generated row would make coverage report a partition covered when nothing is
there.

The matrix stays **generator input, not a table** (§5.4): nothing at runtime reads it, the generated
`effect_container_pool` rows are the SSOT, and both `effect_atom` and `effect_container_pool` are
already in the E8 content-hash registry — so a weight change moves the hash with **no
`contentHashSchemaVersion` bump**.

### Reason codes — take §2b.1, not two new codes

Three lanes propose three codes for one failure: I8's **`AffixNotLegalHere`** (§6.2), I2's
**`RoleFamilyIllegal`** (`ssot-equip-slots.md:575`), and I8's `AffixClassRollsMismatch`. §2b.1 resolves
the whole class of question — **one namespaced `ContentRuleViolated`**, because *"101 codes is a
vocabulary to maintain, document, and keep in sync with the FE forever."*

**Take it.** `ContentRuleViolated{affix.not-legal-here | affix.class-rolls | affix.role-family}`. The
closed 33-code list does not grow, and the payload carries what a bare code would have.

### What is **not** decided, and who decides

| Open | Owner |
|---|---|
| Whether `bulwark`/`savagery` leave the twin jewels, or §2.5 is amended | **module 3** (`ssot-equip-slots` is its lane) |
| The reduced `max_tier` for `head-guard`'s and `sense`'s relocated clusters | **module 3** picks; this module applies |
| 3+3 or 2+2 as the class ceiling (§10.1) | **owner**, on measured evidence — §9.15 asks the perf stream for a fully-geared roster scenario, and the lane says it would move to 2+2 on data rather than on feel |
| The four constants — `r = 1.75`, band `[0.67m, 1.33m]`, tier weights `1000/600/300/120/35` (§10.3) | **owner**; they are the first numbers a sweep moves. `r` and the band width are already frozen in `data/seed/items/_registry/bands.v1.json` |

## Commands

```powershell
dotnet run --project tools\ItemSeedValidator
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AffixLegality"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RoleFamily"
```

## Project structure

```text
data/seed/items/_registry/role-affix-weights.v1.json  new — 15 roles x 15 groups, generator input
data/seed/items/_registry/family-overrides.v1.json    new — the max_tier override list (twins + D3)
src/FusionRpg.Core/Items/RoleFamilyTable.cs           new — the DERIVED item_role_family + overrides
src/FusionRpg.Core/Items/IlvlTierLadder.cs            new — D29's ladder + I12's collapsing envelope
src/FusionRpg.Core/Items/AffixFilters.cs              new — frame / side / runtime, runtime read from
                                                        AtomKindRegistry, never from a document
src/FusionRpg.Core/Effects/Atoms/AffixValidator.cs    SHIPPED — ResolveClass; verify, do not rebuild
src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs   SHIPPED — the consumer seam
tools/ItemSeedValidator/Checks/RoleFamilyCheck.cs     new — legality at seed time
```

## Code style

```csharp
// Runtime legality is read from the registry, never from a lane document. ssot-affixes.md §4.9 still
// says stat.derived is quarantined None/None/None; AtomKindRegistry.cs:255 says Full/Full/None. The
// registry is the code, the document is a description, and a description went stale without anyone
// noticing for a week. Read the matrix.
static bool RuntimeAllows(AtomRow atom, TargetRuntime target) =>
    AtomKindRegistry.For(atom.KindId).Support.In(target) != RuntimeState.None;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `item_role_family_is_derived_from_the_family_roles_lists` | ⭐ no authored cells; 656 pairs reproduced from the corpus |
| `an_authored_item_role_family_row_is_rejected` | there is no second home for the fact |
| `the_only_max_tier_overrides_are_the_declared_list` | the override list is small and enumerated, not open |
| `both_minor_jewels_cap_every_family_at_tier_3` | `ssot-equip-slots.md:129` |
| `bulwark_and_savagery_are_absent_from_both_minor_jewels` | ⭐ §2.5's third mechanism. **Red today** |
| `the_ilvl_tier_ladder_is_1_1_8_18_32` | D29; I8's `1/12/25/40/60` must not reappear |
| `t1_never_falls_out_of_the_window_at_high_ilvl` | the collapsing envelope, not the sliding one |
| `a_narrowed_envelope_narrows_the_count_and_records_it` | never reject a legal drop from legal content |
| `no_family_is_orphaned_by_the_three_dropped_hybrid_roles` | ⭐ D3's relocation premise, measured — 0 orphans |
| `a_relocated_family_carries_a_reduced_max_tier_on_its_host` | the 200‰ price is not refunded through breadth |
| `stat_derived_families_are_legal_on_battle_and_lawn` | the lifted quarantine, asserted against the registry |
| `a_stat_derived_affix_is_refused_for_a_sim_target` | Sim is still `None`; the half that did **not** lift |
| `warding_and_resilience_are_refused_at_import_for_every_slot` | G8 + D14: `standard` is out of scope, so nowhere |
| `an_aptitude_tagged_affix_cannot_enter_a_pool_before_the_vocabulary_lands` | §2g #2 — the gate is inert without it |
| `a_group_emptied_by_a_filter_redistributes_and_logs_the_triple` | §4.4; silently shrinking a pool by a third is the worst outcome |
| `prefix_rolls_plus_suffix_rolls_is_the_count_and_neither_exceeds_3` | the quota, against `NOT NULL DEFAULT 0` |
| `an_item_with_three_or_more_rolls_has_at_least_one_of_each_class` | the anti-stat-stick / anti-proc-storm rule |
| `affix_class_is_derived_and_an_authored_one_is_rejected_in_seeds` | `AffixValidator.ResolveClass`; `seed-contract.md` |
| `standard_keeps_a_matrix_row_and_generates_nothing` | D14 |
| `legality_failures_report_one_namespaced_ContentRuleViolated` | §2b.1 — the closed 33-code list does not grow |

## Boundaries

**Always:** derive `item_role_family`; read runtime support from `AtomKindRegistry`; keep the matrix
as generator input so the generated pool rows stay the SSOT and the content hash keeps working.

**Ask first:** adding a `max_tier` override beyond the declared list; changing the class ceiling from
3+3; adding a sixteenth affix group.

**Never:** hand-author a role × family cell. Never fork a shared affix to change its legality — that
is what `eligibility-tags`' allow/deny exists to avoid. Never quote a runtime-support claim from a
lane document. Never let a family be legal on a role where its group weight is 0.

## Success criteria

- [ ] `item_role_family` is derived from the 98 families' own `roles`/`frames`/`side`, with an
      enumerated override list and **zero** authored cells.
- [ ] The ilvl→tier ladder is **1 / 1 / 8 / 18 / 32**, and the collapsing envelope — not a sliding
      window — is the window rule.
- [ ] D3's relocation is recorded with its measured inputs, including the finding that **no family is
      orphaned**, and each relocated cluster carries a reduced `max_tier`.
- [ ] Every `stat.derived` group is live on battle and lawn, refused for sim, and the legality check
      reads `AtomKindRegistry` rather than a document.
- [ ] `bulwark` and `savagery` no longer sit on the twin minor jewels, or `ssot-equip-slots.md` §2.5
      is amended to say they may.
- [ ] Legality failures surface as one namespaced `ContentRuleViolated`; no new reason code ships.
- [ ] `ItemSeedValidator` reports 0 legality errors across all 98 families and 15 roles.
