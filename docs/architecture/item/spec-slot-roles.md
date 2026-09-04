# Spec: `slot-roles`

**Module id:** `slot-roles` · **Program:** [item](../item-map.md) · **Build order:** 3 of 21
**Depends on:** nothing to **build**. ⭐ **X1** (`frame`, seedsmith — resolved, unbuilt) is needed only
to *populate* the per-actor frame lookup; every table here is registry data — see §X1 below
**Issues:** ⛔ **`_registry/core.v1.json` registryVersion 2** — the twelve-role hybrid core, consumed by 8, 12, 13, 16, 21
**Rulings:** D1, D2, D3, D14 · lane [ssot-equip-slots.md](ssot-equip-slots.md)

## Objective

Declare the body: **fifteen equip roles**, two frame vocabularies over one role table, the twelve-role
hybrid core, and an unlock predicate that ships **defaulting to always-open**.

**Users:** modules 6 (`base-types`), 8 (`affix-legality`), 13 and 21 (both generators cap on the
hybrid core), 16 (`sockets`).

## Design

### ✅ X1 — this module **can** start; only per-species population waits

> ⚠ **This heading previously read *"this module cannot start until `frame` exists"*. That was wrong,
> and it would have stopped a builder on day one.** Corrected 2026-09-04 while confirming build
> readiness.

**Everything this module owns is registry data, not species data:**

| Deliverable | Needs X1? | Source |
|---|---|---|
| `item_role` — the 15 roles, `standard` declared and ungenerated (D14) | **no** | this spec |
| `item_role_frame` — **schema**, and which frames may use which roles | **no** | `core.v1.json` v2 |
| The **twelve-role hybrid core** at 800‰ | **no** | **D30**, settled independently of the population |
| The unlock predicate, defaulting to always-open (D2) | **no** | this spec |
| Resolving *a given actor's* legal roles | ⭐ **yes** | X1's species → frame lookup |

**So the slice is: build the tables now, populate the actor lookup when X1 lands.** The consumer that
feels the gap is module 4's `EquipGate` — its **frame arm is inert** until species carry a frame, while
its predicate, level and faction arms are live. That is a wiring gap with a named closing condition,
not a wall.

### The dependency itself, unchanged

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

### ⛔ The table above is a **ruling**, not the shipped registry — and this module owns the reconciliation

**This spec previously presented D3's twelve as settled, and enumerated `jewel-minor-b` into it. That
is false as written.** Three shipped sources disagree with D3, one of them gates CI, and every one of
them keeps `jewel-minor-b` *out*.

| Source | Hybrid drops | Roles | Budget | Verified |
|---|---|---:|---:|---|
| **D3** (owner ruling — **wins**) | `ward-array` · `head-guard` · `sense` | **12** | **800‰** | [item-ideal.md](../item-ideal.md) §2b D3 |
| `_registry/core.v1.json` — `roles.list[].hybridEligible: false` on two rows, **and** the `hybrid` frame's own `meaning` prose | `ward-array` · **`jewel-minor-b`** | 13 | **895‰** | 2026-09-04 |
| `tools/seedsmith/seedsmith/adapters/items/registries.py:111` — `HYBRID_FRAME_EXCLUDED_ROLES` | same two | 13 | 895‰ | 2026-09-04 |
| `tools/seedsmith/seedsmith/metrics/linkage.py:28` — `NON_HYBRID_ROLES`, feeding `SetCompletability` whose **`gates = True`** (`:61`) | same two | 13 | 895‰ | 2026-09-04 |

⛔ **Correcting them turns 18 of the 30 shipped sets red** — every set using `head-guard` (10) or
`sense` (11). `Linkage/SetCompletability` reports **no findings today** precisely because it is blind
to D3. This is [item-ideal.md](../item-ideal.md) **§2g #0a**, the program's oldest open decision, and
it is recorded here rather than downstream because **`spec-threshold-grants.md` and
`spec-set-charm-gen.md` both carry this table and both name module 3 as the issuer.**

**The ask: `core.v1.json` registryVersion 2.** The file carries `"frozen": true` and a `frozenNote` —
*"No in-place edit from here: a required change is registryVersion 2 plus an explicit decision on
which partitions re-run."* A bump plus a re-run decision, never a patch. **Three changes travel
together in it**, and splitting them leaves the contradiction half-alive:

| # | Change | Note |
|---|---|---|
| 1 | `hybridEligible` → `false` on `head-guard` and `sense`; **`true` on `jewel-minor-b`** | with `hybridDropReason` added for the two new drops and removed from `jewel-minor-b` |
| 2 | The `hybrid` frame's `meaning` prose — *"Carries 13 of the 15 roles (drops ward-array and jewel-minor-b)"* → **12, dropping `ward-array` · `head-guard` · `sense`** | ⚠ `registries.py:105`'s `HYBRID_FRAME_CITATION` is asserted **substring-present** in the live registry by `tools/seedsmith/tests/test_items_adapter.py:85`. Registry prose and Python constant move in one commit or that test goes red |
| 3 | `linkage.py:28`'s `NON_HYBRID_ROLES` | the gating half. Until it moves, the gate cannot see the defect it exists to catch |

✅ **RESOLVED 2026-09-04 — D30. All three changes ship; the 18 sets are re-authored.** The owner's
words: *"anchor on invalid, unbuilt data is wrong … use D3, fix shipped src."* **D3 wins over the
shipped registry** — the twelve-role core at 800‰ stands and the source is corrected to match it.

> ⚠ **A counter-proposal was withdrawn with its evidence.** I argued for keeping the shipped thirteen
> on the grounds that hybrids are ~6% of the species population. That figure came from
> `themes.v1.json`'s **84** entries; `data/seed/demons/species/` holds **386**. The registry is a stale
> snapshot of a generated corpus (now filed as a seedsmith defect — [seedsmith-map.md](../seedsmith-map.md)
> §3c-ter), so the proportion was fiction. **Never derive a design proportion from a snapshot of a
> generated corpus.**

⭐ **And the cheap answer was already on the table:** re-authoring is *one generation run* — module 13
performs it for the ~904 regardless, so the 18 legacy sets cost no additional pass.

**The 18-set disposition, as ruled.** Two answers were cheap; the third is now closed:

| Option | Cost |
|---|---|
| ⭐ **Re-author** the 18 legacy sets under the twelve-role cap — **CHOSEN (D30)** | one generation run — module 13 does it anyway for the ~904 |
| **Grandfather** them with a recorded exception list | one list, and the gate stays honest for everything new |
| ~~⚠ **Revisit which roles D3 drops**~~ — **CLOSED by D30** | was live, and not free: 895‰ over 13 is a different floor from 800‰ over 12, and **every number in module 12's recovery curve is keyed to 800** |

⛔ **Silently leaving the gate blind is the only expensive answer.**

### D3's criterion for *which* three — not just its answer

**The three drops reverse a recommendation made earlier the same day, and the reversal is the
load-bearing part.** Under the old framing — *price the cherry-pick* — the best drop was `footing`,
the one role I2 marks *"frame-split by design"*, precisely because it carried the largest frame
difference. Under D3's framing that is exactly backwards:

> **Frame-differentiated roles are kept because they are what the mix bonus runs on** — removing the
> clearest one shrinks the tension the whole mechanism depends on. **`footing` stays.**

| Dropped | ‰ | Fiction |
|---|---:|---|
| `ward-array` | 90 | a chimera has no coherent outer layer — a body half bark and half bone sheds no single sheath |
| `head-guard` | 60 | a two-natured head has neither creature's clean guard |
| `sense` | 50 | …nor either's clean senses. One fiction covers both: **the head is the part that agrees least** |

| Kept deliberately | Why |
|---|---|
| `footing` | the frame-split showcase — the criterion above, stated as a role id |
| `mantle` | I2 §10.2 warns against taking elemental resistance from the frame most likely to face mixed damage |
| both jewels-minor, `jewel-major`, both armaments, `core-guard`, `girdle`, `manipulator`, `retinue`, `infusion` | no fiction for dropping them, and each carries budget module 12's recovery curve is sized against |

**Record the criterion, not only the outcome.** Three ids with no reason attached is a list that
quietly re-acquires `footing` the next time someone re-derives it from *"which role differs most
between frames"* — the correct question under the wrong framing.

⭐ **And the relocation is smaller than D3's prose implies.** Measured across all 98 affix families,
**zero are orphaned** by the three drops — every family is already legal on at least one hybrid-core
role ([item-ideal.md](../item-ideal.md) §2g). Module 8 does not choose *hosts*; it chooses **reduced
`max_tier`s on hosts that already exist.**

### D2 — the unlock predicate ships, defaulting to open

Every slot is open from the start. **But the gate exists and defaults to open**, so a later
breakthrough or quest system can close slots without a schema migration or a content re-author.

⚠ **Do not hard-code fifteen-always-open.** The requirement is the predicate, not the outcome.

⚠ **And record what this costs:** `ssot-equip-slots.md` §8.2 names the unlock ladder as the *only*
mitigation for *"gearing a new specimen is a chore"*, and D2 turns it off while D1 declines to own
the problem. The predicate's existence is what makes that reversible.

### D14 — The commander is another unique demon, and `standard` is a live contradiction

No 16th slot. **`standard` stays declared**, and D14's disposition is *"nothing generates into it"* —
the same disposition seedsmith gave its `environment` kind, and for the same reason: the row costs
nothing and keeps the shape stable, while generating into it would make coverage report a partition
covered when nothing is there.

⛔ **The corpus already contradicts that, and this spec previously asserted the opposite as fact.**

| Claim in this spec | Reality, verified 2026-09-04 |
|---|---|
| *"`standard` stays declared and **ungenerated**"* | `data/seed/items/base-types/humanoid-standard.json` and `plant-standard.json` ship **10 entries each — 20 generated `standard` base types** |
| Test `standard_is_declared_and_nothing_generates_into_it` | **red today** against the shipped corpus |

Both files carry `_meta.authoredUtc: 2026-08-22`, `contractVersion 1`, `core` registry v1 — **twelve
days before D14.** Same shape as the 18 legacy sets above: a legacy corpus meeting a newer ruling, not
a generator defect.

**The ruling wins, so the corpus moves — but split the claim, because only one half is assertable now:**

| Rule | Status |
|---|---|
| **The generator never emits into `standard`** | ✅ true and testable today — this is D14's actual instruction |
| **The corpus holds no `standard` entry** | ⛔ false — 20 entries, pre-dating the ruling |

**Recommended disposition — retire, do not delete.** `seed-contract.md:201` already supplies the
mechanism: *"Entry should not exist → `enabled: false`, **file kept, id retired forever**."* Twenty
flags across two files, no id reuse, and coverage stops reporting a covered partition that is empty.

> ✅ **RULED 2026-09-04: retire the 20 under D14** — `enabled: false`, file kept, **id retired forever**
> (`seed-contract.md:201`). Twenty flags across two files, no id reuse, and coverage stops reporting a
> covered partition that is empty. D14 is **not** amended.
> **The red test comes down with them** — it was never the right record of a disagreement.

### D3's affix-family relocation is an input to module 8

*"The families are not lost, only the slots."* `ward-array`'s shields, `head-guard`'s crit-resist /
crit-damage padding / status-resist / immunity, and `sense`'s accuracy / crit-rate relocate at reduced
`max_tier`, following §4.2's existing pattern. **This module chooses the hosts; module 8 applies
them.**

### D3's frame-mix bonus is module 12's, and its predicate is unusual

`min(humanoidCount, plantCount)` — a **min over two counts**, not a count over one predicate. Named
here because this module owns `frame`; built in module 12.

⚠ §2g requires the count be **weighted by role `budgetWeightMilli`**. Unweighted, 6/6 costs ~230‰ of
an 800‰ body because concession is cheapest in the lightest roles.

### ⭐ §2h.1 — the mechanism stands; its stated rationale does not. Do not repeat the cheat framing

D3's own words presuppose **uncorrelated** frame preference — *"if the better pick is humanoid in 10
of 12 roles, you take them, the minority count is 2."* §2f.2 then widened D11 to require preference be
**correlated across roles** (humanoid leans offence, plant leans sustain), because uncorrelated
preference makes `min(k, 12−k)` concentrate near 6, the 800‰ floor bind on **0.63%** of builds, and D3
collapse at δ>4.4%.

⛔ **That fix dissolves the cherry-pick D3 says it punishes.** With one fixed lean per frame there is
no role where one frame is simply *stronger* — only *how much of axis A versus axis B*. The abuse
cannot occur, so it cannot be the justification.

**Restated ([item-ideal.md](../item-ideal.md) §2h.1), and this is the wording to carry forward:**

> **A hybrid pays 200‰ for the only base profile that spans both axes, and earns it back by actually
> spanning them.** A hybrid that specialises stays at 800‰ — correctly, because it is doing what a
> pure frame does, worse. A hybrid that commits to generalism reaches parity. **The floor prices
> generalism; the bonus rewards committing to it.**

**Action: restate the rationale; change no mechanism.** The role table, the 200‰ and module 12's
recovery curve are all unchanged by this.

⚠ **The hybrid population is uncounted — and as of D30 that no longer gates anything.** ⛔ **Do not
count it from `themes.v1.json`**: that registry holds 84 themes against 386 shipped species, and a
proportion taken from a stale snapshot of a *generated* corpus is fiction. X1 runs **after**
seedsmith's `theme-refresh` (D34). The count is a **sizing** input for the frame-lean table, never a
gate on the twelve-role core. Original note: `frame-classify` (X1) **has not run**, so this apparatus
serves the commander plus an unknown number of species. **Nothing here may be sized against an assumed
hybrid share until that number exists** — ⚠ and X1 is **not** "one command": it is an unbuilt
seedsmith stage that must follow `theme-refresh`. Run it, count the hybrids,
then decide how much of D11 to buy.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SlotRole"

# the registry side of the same reconciliation - all three must agree after v2
cd tools\seedsmith
python -m pytest tests/test_items_adapter.py -q          # HYBRID_FRAME_CITATION substring assert
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Linkage/SetCompletability
```

⚠ **`SetCompletability` reports zero findings today and 18 after the correction.** A clean run before
the v2 bump is the gate being blind, not the corpus being right — read it that way or the check is
worse than useless.

## Project structure

```text
data/seed/items/_registry/core.v1.json                  EDIT - registryVersion 2. THE ROLES ALREADY
                                                        SHIP HERE: roles.list[] carries all fifteen
                                                        roleIds, humanoidName, plantName,
                                                        hybridEligible and budgetWeightMilli
                                                        (total 1000), plus commanderOnly.standard
src/FusionRpg.Core/Items/ItemRole.cs                    new - the closed enum; weights READ from the
                                                        registry, never transcribed
src/FusionRpg.Core/Items/FrameVocabulary.cs             new - role -> (humanoid name, plant name)
src/FusionRpg.Core/Items/SlotUnlock.cs                  new - the predicate, default open
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs             EDIT - item_role, item_role_frame
tools/seedsmith/.../adapters/items/registries.py        EDIT - HYBRID_FRAME_EXCLUDED_ROLES (:111) and
                                                        HYBRID_FRAME_CITATION (:105), same commit
tools/seedsmith/.../metrics/linkage.py                  EDIT - NON_HYBRID_ROLES (:28), the gating half
```

⛔ **There is no `data/seed/items/_registry/roles.v1.json`, and this spec previously minted one.** The
fifteen roles **and** their `budgetWeightMilli` already ship in `core.v1.json` (`roles.list[]`,
`"frozen": true`, `budgetWeightMilliTotal: 1000`), and three sibling specs already cite it as the
source. A second registry is the two-sources-of-truth defect this program refuses everywhere else —
and it would be the *silent* kind, because both files would look authoritative.

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
| `the_fifteen_role_weights_sum_to_1000` | the per-mille contract, read from `core.v1.json` — never a transcribed constant |
| `no_second_roles_registry_exists` | ⭐ the two-sources-of-truth guard: `_registry/roles.v1.json` must not exist |
| `the_hybrid_core_is_twelve_roles_summing_to_800` | ⭐ the generator input, against the explicit list |
| `the_hybrid_core_contains_all_three_jewel_roles` | the eleven-vs-twelve prose defect, asserted |
| `jewel_minor_b_is_in_the_hybrid_core` | ⛔ the registry says `hybridEligible: false`; **D3 wins.** Red until v2 issues, and that is the point |
| `footing_is_in_the_hybrid_core` | ⭐ D3's *criterion*, not just its answer — the reversal cannot silently un-happen |
| `the_registry_the_python_constants_and_D3_name_the_same_twelve` | ⛔ the §2g #0a reconciliation, as one test over all three sources |
| `the_hybrid_frame_citation_still_matches_the_registry_prose` | mirrors `test_items_adapter.py:85`, so the prose and the constant cannot drift apart during the bump |
| `no_affix_family_is_orphaned_by_the_three_drops` | the 98-family measurement — module 8 needs hosts, not a rescue |
| `frame_is_never_derived_from_Side` | §8.6's named failure; the four Fusion hybrids are the fixture |
| `a_species_with_no_published_theme_still_has_a_frame` | X1's blocked-clause carve-out |
| `every_slot_is_open_with_no_rule_configured` | D2 |
| `a_configured_rule_can_close_a_slot_without_a_migration` | the predicate is real, not decorative |
| `the_generator_never_emits_a_standard_base_type` | ⭐ D14's actual instruction — the half that is true and testable today |
| `every_shipped_standard_base_type_is_retired` | the corpus half — **red against the 20 legacy entries** until the owner dispositions them |
| `each_role_has_a_name_in_both_frame_vocabularies` | one table, two vocabularies (`humanoidName` / `plantName`) |

⚠ **Three of these are red on purpose** — `jewel_minor_b_is_in_the_hybrid_core`,
`the_registry_the_python_constants_and_D3_name_the_same_twelve`, and
`every_shipped_standard_base_type_is_retired`. They are the contradictions written as assertions so
they cannot be forgotten; each goes green with the v2 bump and the corpus disposition, and none may be
weakened to make the suite pass.

## Boundaries

**Always:** enumerate the twelve hybrid-core roles explicitly wherever they are used as a generator
input; read weights from `core.v1.json`, never transcribe them into code; move the registry prose,
`registries.py` and `linkage.py` in one commit.

**Ask first:** ⛔ **the `core.v1.json` registryVersion 2 bump and which partitions re-run** — the
`frozenNote` requires both. **The 18-set disposition** (re-author · grandfather · revisit D3's drops).
**The 20 legacy `standard` base types** (retire under D14, or amend D14). Changing a role weight (it
moves every downstream budget); adding or removing a role.

**Never:** derive `frame` from `Side`. Never hard-code the unlock outcome instead of the predicate.
Never generate into `standard`. Never mint a second roles registry. Never present D3's twelve as
*shipped* — until the v2 bump lands it is a ruling the corpus does not yet obey, and stating otherwise
is exactly the error this revision corrects.

## Success criteria

- [ ] Fifteen roles, two vocabularies, weights summing to 1000 — **read from `core.v1.json`**, with no
      second roles registry anywhere in the tree.
- [ ] The twelve-role hybrid core is enumerated explicitly and sums to 800‰, including all three jewels
      and `footing`, with D3's **criterion** recorded beside the list.
- [ ] ⛔ **The 13-role/895‰ contradiction is resolved or explicitly deferred with an owner decision** —
      `core.v1.json` registryVersion 2, `registries.py:111`, `linkage.py:28`, the frame `meaning` prose
      and `HYBRID_FRAME_CITATION` all naming the same twelve, and the 18-set disposition recorded.
- [ ] D3's rationale is the §2h.1 wording (**generalism priced and rewarded**), not the cheat framing,
      and no number moved as a result.
- [ ] `frame` is consumed from the demon pipeline, never computed from `Side`, and exists for every
      gearable species including those with no published theme.
- [ ] The unlock predicate ships, defaults to open, and is provably closable without a migration.
- [ ] `standard` is declared, **the generator emits nothing into it**, and the 20 legacy entries are
      retired or their retention is an owner decision on the record — not a red test.
