# Spec: `base-types`

**Module id:** `base-types` · **Program:** [item](../item-map.md) · **Build order:** 6 of 21
**Depends on:** `slot-roles` (3) — and through it **X1** (`frame`)
**Rulings:** **D11 (+ the §2f.2 widening)**, D3, D15, D26, D29 · lane [ssot-item-categories.md](ssot-item-categories.md)

## Objective

Make every role's **humanoid and plant base types differ on two axes** — a directional stat profile
inside one budget, and a distinct implicit — and prove it with a **dominance lint**: for every role
there is a build for which the humanoid base is correct and a build for which the plant base is
correct.

**D11 is not flavour. It is the correctness condition D3's whole mechanism rests on.** If one frame's
base wins every role, a hybrid cherry-picks for free, the mix bonus is a gift, and the 800‰ floor is
theatre.

**Users:** 8 (`affix-legality` — the `affix_pool_tag`), 9 (`power-reads` — the implicit budget cap),
11 (`drop-volume`), 12 (the frame-mix predicate counts *these* frames).

## Design

### ⛔ The corpus already exists — and it fails D11 on both axes

**740 base-type entries ship today** in `data/seed/items/base-types/` (24 per `(role, frame)` across
15 roles, plus 10 per frame for `standard`), inside a 1,438-entry corpus of 126 files
(`dotnet run --project tools/ItemSeedValidator`). This module does **not** author base types from
nothing; it repairs a corpus that was authored 2026-08-22, before D11 existed.

| D11 clause | State today | Evidence |
|---|---|---|
| **Distinct implicit per frame** | ⛔ **fails in 14 of 16 roles** — the humanoid and plant implicit-family sets are *identical* | measured over `data/seed/items/base-types/**` |
| | the two exceptions differ by **one family each**: `armament-secondary` (humanoid-only `atom.mending`), `girdle` (plant-only `atom.vitality`) | same |
| **Directional stat profile** | ⛔ **not expressible.** No entry field carries one | entry keys are `id · nameKey · name · frame · role · class · band · implicit · socketMax · iconKey · flavorKey · flavor · tags · enhanceTrack`; the shape is fixed in `seed-contract.md:324-343` and `tools/seedsmith/seedsmith/adapters/items/kinds.py:49-51` |
| **Correlated across roles** | ⛔ nothing to correlate — see the row above | — |

⭐ **And the root cause is one stale fact, not 740 bad decisions.**
`data/seed/items/_registry/classes.v1.json` (`registryVersion: 3`, `frozen: true`) globally excludes
**32 families** from every implicit slate, most of them on the stated grounds that *"`stat.derived` —
quarantined None/None/None (D6)"*. **That is false today.** `stat.derived` ships
`RuntimeSupportMatrix(Full, Full, None)` — Battle **Full**, Lawn **Full**, Sim None
(`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:255`, kind declared at `:226`), with 267
registered derived channels (`:53`).

The registry's own `_meta.designNotes` says the consequence out loud: four roles — `ward-array`,
`mantle`, `head-guard`, `sense` — have *"their entire §2.3-named family cluster excluded"* and carry
*"a small stopgap slate drawn from generic, already-bindable families."* **Generic stopgap slates are
frame-blind by construction**, which is precisely why those roles show two implicit families per frame
and the same two on both frames.

> **So the first task of this module is not authoring. It is lifting a quarantine that was lifted
> upstream and never propagated**, then re-slating the four stopgap roles from their real clusters.

### What D11 requires, restated with the §2f.2 widening

Three clauses. The third is the one D11 originally omitted and the one that decides whether D3 works.

| # | Clause | Formally |
|---|---|---|
| 1 | **Distinct implicit** | `implicitFamilies(role, humanoid) ∩ implicitFamilies(role, plant) = ∅` |
| 2 | **Directional profile inside one budget** | the two slates spend the same `budget_permille` on different channels, and neither strictly dominates |
| 3 | ⭐ **Correlated across roles** | one fixed axis assignment `humanoid → A`, `plant → B` such that **every** role in the twelve-role hybrid core leans the same way |

**Why clause 3 is load-bearing, with the arithmetic** (item-ideal §2f.2). If each role's frame
preference were an independent coin flip, the minority count `min(k, 12−k)` for `k ~ Bin(12, ½)`
concentrates near 6 — a hybrid **conceding nothing** averages **958‰**, the 800‰ floor binds on
**0.63 %** of builds, and D3 collapses at any per-role advantage above 4.4 %. Correlated preference is
what makes the floor bind. **D11 as written never said which, and it is the whole difference.**

### Where the direction lives — one registry block, not 740 entries

| Option | Cost | Verdict |
|---|---|---|
| **(a)** a `statProfile` field on every base-type entry | 740 authored blocks; nothing enforces correlation; a per-entry field is 740 chances to break clause 3 | **Reject** |
| **(b)** a `direction` block per class (24 classes) | 24 blocks; correlation holds per ladder but four ladders can still disagree | half right |
| **(c)** ⭐ **one lean per `(ladder, frame)` — 8 blocks** | 8 blocks; **clause 3 holds by construction** because every role drawing from a frame's ladder inherits one lean | **Recommended** |

**(c) makes correlation structural rather than checked.** The class ladders are already parallel and
frame-keyed — humanoid `cloth · leather · scale · plate` against plant `fibre · husk · bark ·
heartwood`, same `rung`, same `roles` list, same weight `tags`
(`data/seed/items/_registry/classes.v1.json` → `classLadders.armour`). What they carry is a rung and a
weight tag; **what they do not carry is a direction.** Adding it once per `(ladder, frame)` is the
smallest place the fact can live and the only place it cannot drift per role.

Per-mille shares in a registry are established practice here, not a new pattern:
`core.v1.json.roles.list[].budgetWeightMilli` and
`data/seed/items/_tuning/tier-bands.v1.json.channelWeightPermille` both do it. And it stays inside
`seed-contract.md:88-105` — the numeric rule binds **authors of entries**; a registry share is the
resolver's own input, exactly as `budgetWeightMilli` already is.

```text
frame-lean.v1.json          (new registry, one block per ladder × frame = 8)
  armour / humanoid   baseSplitPermille { maxHp 700, combat.dodge 300 }   implicitAxis "burst"
  armour / plant      baseSplitPermille { maxHp 700, combat.regen 300 }   implicitAxis "sustain"
  weapon  / humanoid  …                                                    implicitAxis "burst"
  weapon  / plant     …                                                    implicitAxis "sustain"
  … offhand, jewel
```

⚠ **The split channels must both be bindable on both frames.** `plating` / `carapace` write
`arm1Max` / `arm2Max`, Unity fields that exist only on zombies
(`ssot-affixes.md:302` — the side filter), so they may **not** be a humanoid lean channel: side is not
frame, and a plant-side humanoid-framed body would get nothing. The derived channels are the safe
axis, and they are legal now that the quarantine is lifted.

### The dominance lint — the thing that makes D11 checkable

A role where one frame dominates across all builds is **a content defect with a name**, not a matter
of taste. The lint is the name.

```text
for each role r in the twelve-role hybrid core:
    H = the humanoid slate's power vector at the role's budget
    P = the plant slate's power vector at the same budget
    assert ∃ corner c in the aptitude corner matrix : score(H, c) > score(P, c)
    assert ∃ corner c'                              : score(P, c') > score(H, c')
    assert lean(r, humanoid) == globalLean(humanoid)      # clause 3
```

- The corner matrix is the class system's, already built and already measured over **144 evaluations**
  (`item-ideal.md` §2f.3 D29). The guards are `TerminationGuard.Assert` and `DominanceGuard.Measure`
  (`src/FusionRpg.Core/Balance/Guards/TerminationGuard.cs:67`,
  `src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:38`).
- `score` reads module 9's power vector (`PowerScalar.Of`,
  `src/FusionRpg.Core/Effects/Atoms/Power/PowerReads.cs:39`, five categories at `:34`).
- ⚠ **The lint therefore cannot run until module 9 lands, and module 9 waits on X6** (`E44
  power-sweep`, all 20 coefficients flat at `CoeffMilli = 1000`). Until then it runs in **channel-split
  mode**: assert the two `baseSplitPermille` blocks differ on at least one channel and that neither is
  a superset. That is weaker, it is honest, and it catches the failure that exists today.

**Standing:** SOFT (reports with coverage) for the per-role clause, **HARD** for clause 3. A single
role that fails clause 1 or 2 is content to fix; a broken correlation silently repeals D3.

### The base stat stays an atom, and that is settled

`ssot-item-categories.md` §4.A resolves it and nothing since disturbs it: base damage is
`atom.base-damage.{class}.t{band}`, base guard is `atom.base-guard.{class}.t{band}`, and there is no
`weaponDamage` column because there is no such channel. A column would need a second composer, a
second pricer, a second display path and a hand-written bridge to the same `EntityStatWriter` the atom
already reaches — the exact bypass `scripts/guard-single-writer.ps1` exists to refuse.

### Three lane numbers that the shipped corpus overtook

| `ssot-item-categories` says | Shipped corpus | Disposition |
|---|---|---|
| §4.D / §7.6 — **four** item-level bands, `43 × 2 × 4 = 344` containers | **two** bands, `a` and `b` (380 / 360 entries) | the corpus is the SSOT; §7.6's own "ship bands 1 and 3 only" cut was taken. **Bands are append-only** — c and d are additive |
| §5.4's guard-share table, keyed on the **old twelve** role ids (`core-protective`, `sense-utility`…) | 15 roles with `budgetWeightMilli` summing to 1000 (`core.v1.json`) | **Re-issue §5.4 against the fifteen.** §5.4's own note says the shares re-split rather than inflate |
| §5.2 — `socket_capacity` per base type | `socketMax` ∈ {0, 1, 2}; **24 entries carry none** | ⚠ D20 fixes the Strain/Splice ingredient count at **4** and `socket_max` caps at 4. A corpus topping out at 2 cannot host a 4-ingredient Splice. **§2g #7 is this**, and it lands here |

### What is **not** decided, and who decides

| Open | Owner |
|---|---|
| Whether the four re-slated roles' entries need **re-flavouring** — an entry keeps its name and prose while its implicit family changes, so `atom.fortitude` prose can end up over an `atom.stoicism` implicit | the **authoring fleet** (`authoring-fleet-plan.md`). This module emits an `ImplicitFlavourDrift` **warning** per entry; it does not call a model |
| The ≤15 % implicit budget cap (`ssot-item-categories.md` §10.7) | **module 9** — it needs a power number, and there is none until X6 clears |
| Which concrete channels each frame leans on | **owner**, at the frame-lean table's first review. This module ships the mechanism and a starting table; the channels are a balance surface |

## Commands

```powershell
dotnet run --project tools\ItemSeedValidator                    # 126 files / 1,438 entries today
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BaseType"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FrameDominance"
```

## Project structure

```text
data/seed/items/_registry/frame-lean.v1.json      new — 8 blocks (4 ladders x 2 frames)
data/seed/items/_registry/classes.v2.json         new version — the 32-family exclusion list
                                                    re-derived against AtomKindRegistry, and the four
                                                    stopgap slates replaced. v1 is FROZEN: never edit
data/seed/items/base-types/**                     EDIT — implicit reassignment only; ids never reused
src/FusionRpg.Core/Items/FrameLean.cs             new — the lean table + the clause-3 invariant
src/FusionRpg.Core/Items/BaseTypeSlate.cs         new — slate per (role, frame)
src/FusionRpg.Core/Balance/Guards/FrameDominanceGuard.cs  new — the D11 lint, corner-matrix backed
tools/ItemSeedValidator/Checks/FrameDirectionCheck.cs     new — clauses 1 and 2 at seed time
```

## Code style

```csharp
// Clause 3 (item-ideal §2f.2): the lean is per (ladder, frame), NEVER per role. A per-role lean lets
// each role's preference be an independent coin flip - and then min(k,12-k) for k~Bin(12,1/2)
// concentrates near 6, a hybrid conceding nothing averages 958 permille, and D3's 800 floor binds on
// 0.63% of builds. Structural correlation is the whole point of putting it here.
public static Lean Of(ClassLadder ladder, Frame frame) => _table[(ladder, frame)];

public static bool CorrelationHolds(IEnumerable<ItemRole> hybridCore) =>
    hybridCore.All(r => Of(LadderOf(r), Frame.Humanoid).Axis == GlobalAxis(Frame.Humanoid));
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_role_pair_has_disjoint_implicit_families` | D11 clause 1, over the real corpus. **Red today in 14 of 16 roles** |
| `every_role_pair_differs_on_at_least_one_base_channel` | D11 clause 2 in channel-split mode — the form that runs before module 9 |
| `the_frame_lean_is_identical_across_every_hybrid_core_role` | ⭐ D11 clause 3, HARD. The §2f.2 widening |
| `a_per_role_lean_table_is_rejected_at_load` | clause 3 cannot be defeated by relocating the field |
| `neither_frame_wins_every_corner_for_any_role` | the dominance lint proper, once module 9 lands |
| `a_role_where_one_frame_dominates_is_a_named_finding` | the lint reports the role id, not a boolean |
| `stat_derived_is_not_excluded_from_any_implicit_slate` | the stale-quarantine repair, asserted against `AtomKindRegistry` rather than against a document |
| `no_lean_channel_is_side_restricted` | `plating` / `carapace` write zombie-only Unity fields; a lean on them silently voids one frame |
| `implicit_slates_are_tier_equal_within_a_role` | `classes` `_meta.designNotes`' existing guarantee survives re-slating |
| `no_More_op_family_appears_on_any_implicit_slate` | `bulwark` / `savagery` stay rolled-only — the registry's own rule |
| `base_stat_atoms_never_appear_in_a_container_pool` | `BaseStatInPool`, `ssot-item-categories.md` §6 code 1 |
| `a_base_type_id_is_never_reused_after_an_implicit_change` | `seed-contract.md` §7.2 |
| `band_letters_are_append_only` | `a`/`b` today; `c`/`d` add, never renumber |

## Boundaries

**Always:** put the lean in the registry, one block per `(ladder, frame)`; re-derive an exclusion list
from `AtomKindRegistry`, never from a document; keep the base stat an atom.

**Ask first:** the concrete lean channels (a balance surface); raising `socketMax` above 2 (it changes
what a Splice can capture — the §2g "watch" item); adding a fifth class ladder.

**Never:** author a `statProfile` per entry — that is 740 chances to break clause 3. Never edit a
`frozen: true` registry in place; mint `v{n+1}`. Never reuse a base-type id. Never let one frame's
slate be a strict superset of the other's — that is dominance wearing difference's clothes.

## Success criteria

- [ ] Every role's humanoid and plant implicit-family sets are **disjoint** (currently 14 of 16 fail).
- [ ] A directional profile exists as data, in exactly one place, and is **per `(ladder, frame)`**.
- [ ] Clause 3 is a HARD test: one axis per frame across all twelve hybrid-core roles.
- [ ] The dominance lint names any role where one frame wins every corner, and is green.
- [ ] `classes.v2.json`'s exclusion list is derived from `AtomKindRegistry` and no longer cites a
      quarantine that was lifted at `AtomKindRegistry.cs:255`.
- [ ] The four stopgap-slate roles (`ward-array`, `mantle`, `head-guard`, `sense`) carry their real
      clusters.
- [ ] `ItemSeedValidator` reports 0 errors on `base-types/`, and every implicit change that outran its
      prose is a named `ImplicitFlavourDrift` warning rather than a silent edit.
