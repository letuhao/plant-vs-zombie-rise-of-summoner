# Spec: `threshold-grants`

**Module id:** `threshold-grants` · **Program:** [item](../item-map.md) · **Build order:** 12 of 21
**Depends on:** `equip-assign` (4), `power-reads` (9) · consumed by `set-charm-gen` (13)
**Rulings:** D3, D12, D15, D17, D27 · lanes [ssot-sets.md](ssot-sets.md) §3.1–3.2, [ssot-charms.md](ssot-charms.md)

## Objective

**One mechanism, three consumers.** Count the things an actor (or a player) currently holds that match a
predicate; look up every breakpoint at or below that count; make the owner's derived bindings equal that
set. Nothing else. Set bonuses, charm resonances and **D3's frame-mix bonus** are three predicates over
one machine, not three machines.

> D3 says so itself: the frame-mix bonus is *"an effect container granted at `OwnerKind.UniqueActor`…
> structurally a set bonus. Set machinery already does 'count equipped items matching a predicate, grant
> at breakpoints'"* ([item-ideal.md](../item-ideal.md) §2b D3, *Implementation*).

**Users:** module 13 (`set-charm-gen`) generates the containers this binds; module 16 (`sockets`) reuses
the evaluator shape at a different scope; module 20 (`item-surfaces`) renders "3 / 4".

## Design

### The mechanism, as one signature

```text
Grant(owner, consumer) :=
    n        := count( owner's held things , consumer.predicate )      -- an integer, or a per-mille
    wanted   := { t.containerId | t ∈ consumer.breakpoints , t.at ≤ n }
    reconcile( existing bindings WHERE source = consumer.sourceKey , wanted )
```

Four properties, each load-bearing and each already true of `ssot-sets.md` §2:

| Property | Why |
|---|---|
| **Cumulative** — 4 pieces holds the 2-piece *and* the 4-piece container | ssot-sets §2; a partial set is a real build, not a consolation prize |
| **Derived, never stored twice** | the durable truth is the assignment rows; these bindings are a projection, tagged `effect_binding.source` so they withdraw as a group (`RpgStore.AtomInstances.cs:89`) |
| **Pure, and it runs on change** | a pure function in Core runs with the game closed; it runs on equip/attune, never per frame |
| **Withdraw-and-rebind, never patch** | re-evaluation is total; a partial update is how derived state drifts |

### ⭐ D3's predicate is a **min over two counts**, not a count over one predicate

**This is the entire anti-cherry-pick design and it does not fit the set-bonus shape without being said
out loud.** A set predicate is *"is this equipped item a member of set S?"* — one predicate, one count.
D3's is:

```text
frameMix(owner) := min( Σ over equipped roles where frame = humanoid ,
                        Σ over equipped roles where frame = plant )
```

Two independent counts, and the grant keys on the **smaller** one. That inversion is what makes
cherry-picking and the bonus mutually exclusive: taking the best base type in 10 of 12 roles drives the
*minority* count to 2 and parks you at the floor. A generic "count things matching a predicate" evaluator
implemented literally **cannot express this**, so the evaluator's predicate type is a
`Func<HeldThing, int?>` **bucket key** plus a reducer, not a `Func<HeldThing, bool>`.

| Consumer | Bucket key | Reducer |
|---|---|---|
| set bonus | `set_id` if the piece is a member, else null | `Sum` over one bucket |
| charm resonance | the charm's `axis` | `Sum` over one bucket |
| **frame-mix** | the equipped base type's `frame` | **`Min` over the `humanoid` and `plant` buckets** |

### ⭐ §2g: the count is weighted by role `budget_permille`, not by item

> **[item-ideal.md](../item-ideal.md) §2g, *Watch*:** *"D3's mix bonus needs its predicate weighted by
> role budget. The bonus counts items; concession is cheapest in the lightest roles, so 6/6 costs ~230‰
> of a 800‰ body rather than half of it. Weighting the minority count by `budget_permille` makes D3's
> stated mechanism true."*

**Verified arithmetic** against the shipped weights in `data/seed/items/_registry/core.v1.json`
(`roles.list[].budgetWeightMilli`, read 2026-09-03): the six cheapest roles of D3's twelve-role core are
`jewel-minor-a` 15 + `jewel-minor-b` 15 + `retinue` 40 + `footing` 50 + `infusion` 50 + `girdle` 60 =
**230‰**. So an unweighted 6/6 split concedes **230 of 800‰ — 28.75%, not 50%** — and D3's *"parity,
bought with per-slot quality"* is false as written.

**So the count is per-mille of conceded budget, and parity sits at an even split of it:**

```text
minorityMilli := min( Σ budgetWeightMilli over humanoid-equipped core roles ,
                      Σ budgetWeightMilli over plant-equipped   core roles )
                 -- 0 .. 400, since the twelve core roles sum to exactly 800
```

| `minorityMilli` | Effective budget | Note |
|---:|---:|---|
| 0 | **800‰** | the floor — a pure cherry-pick |
| 200 | ~900‰ | |
| **400** | **1000‰** | parity — a genuine half-and-half body |

⚠ **The curve and its breakpoints are tunable, not code** — `data/tuning/item-frame-mix.v1.json`. A
balance pass moves them with a file save ([tunables-ssot.md](../tunables-ssot.md)). Only the *shape*
(floor 800‰, recovery capped at +200‰, keyed on `minorityMilli`) is structural.

⚠ **D11's correlated-directionality amendment is the other half of the same fix** ([item-ideal.md](../item-ideal.md)
§2f.2 D11): with independent per-role frame preference, `min(k, 12−k)` concentrates near 6 and the floor
binds on 0.63% of builds. **That half is module 6's** (`base-types`); this module inherits it and cannot
repair it.

### ⛔ A blocking contradiction in the shipped role vocabulary

**Three places disagree about which roles a hybrid has, and one of them gates CI.**

| Source | Hybrid drops | Roles | Budget |
|---|---|---:|---:|
| **D3** (owner ruling, wins) | `ward-array` · `head-guard` · `sense` | **12** | **800‰** |
| `data/seed/items/_registry/core.v1.json` — `hybridEligible: false` on two rows | `ward-array` · `jewel-minor-b` | 13 | 895‰ |
| `tools/seedsmith/seedsmith/adapters/items/registries.py:111` — `HYBRID_FRAME_EXCLUDED_ROLES` | `ward-array` · `jewel-minor-b` | 13 | 895‰ |
| `tools/seedsmith/seedsmith/metrics/linkage.py:28` — `NON_HYBRID_ROLES`, **`gates = True`** (`:60`) | `ward-array` · `jewel-minor-b` | 13 | 895‰ |

**The ruling wins**, but the reconciliation is not free: `core.v1.json` carries
`"frozen": true` and a `frozenNote` reading *"No in-place edit from here: a required change is
registryVersion 2 plus an explicit decision on which partitions re-run."* **This module needs the
weights; module 3 (`slot-roles`) owns issuing them.** State the dependency, do not edit the frozen
registry from here.

⚠ `registries.py:105`'s `HYBRID_FRAME_CITATION` is asserted substring-present in the live registry by
`tools/seedsmith/tests/test_items_adapter.py:85`, so the registry edit and the Python constant must move
together or that test goes red.

### The three consumers, and their scopes are **not** all `UniqueActor`

| Consumer | Counts | Owner scope | Container kind (D27) | `source` |
|---|---|---|---|---|
| **Set bonus** | equipped member roles | `unique-actor:{specimenId}` | `set` | `set:{set_id}` |
| **Frame-mix bonus** | `minorityMilli` over equipped core roles | `unique-actor:{specimenId}` | `set` — D27 puts it here explicitly | `frame-mix` |
| **Charm resonance** | attuned charms sharing an `axis` | ⛔ **`player:{id}`** — ssot-charms §3.1 option C | `charm` | `charm-resonance:{axis}` |

`OwnerKind.UniqueActor` ships (`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:29`, key string
`"unique-actor"` at `:62`), and `RpgStore.UniqueActors.cs:756` already calls `ProduceAndBind` against it.

⛔ **`player:{id}` does not work today, and this is code, not a lane opinion.**

| Fact | Evidence |
|---|---|
| `player:` degrades to match-wide in the stat layer | `src/FusionRpg.Core/Stats/StatApplyScope.cs:81-82` — `return true; // stub → match-wide apply` |
| `IsMatchWide` reports `player:` as match-wide outright | `StatApplyScope.cs:87-92` |
| `match` matches **both sides** before it looks at `side` | `StatApplyScope.cs:52-53` |
| the effect owner matcher does the same | `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs:80` — *"match-scoped for now; player filter is grant-time"*. ⚠ ssot-charms §2 cites `:59-60`; the comment is at `:80` |

**So a `player:`-scoped `+atk` charm on the lawn buffs the zombies.** That is a correctness bug, not a
balance one, and it means **charm resonance binds `RuntimeUnsupported` / `ScopeUnsupported` until
`player:` resolves to the player's deployed side.** The evaluator ships; its charm consumer is gated.

> **Decider: the owner.** Either (a) `player:` gets a real resolver — a change in the stat layer owned by
> effect-atom, not by this program — or (b) charm resonance binds per deployed actor at
> `unique-actor:` and ssot-charms §3.1 option C is reversed to option B. **This spec does not choose**;
> it ships the evaluator scope-parametrically so either answer is a configuration, not a rewrite.

### ⭐ Charm *carry* runtime — the unowned capability, claimed here

`ssot-charms.md` §1 owns *"the rule that holding one — not wearing one — is what makes its atoms reach an
actor"*, the attunement-point budget and the loadout gate. **No module in [item-map.md](../item-map.md)
§4 builds the binding path for an unequipped item.** Module 2 (`armoury`) stores the row; module 4
(`equip-assign`) handles items in roles. An attuned charm is in no role.

**This module claims it**, because it is the same machine: a marking produces a count, the count produces
bindings at breakpoints.

| Piece | Shape | Source |
|---|---|---|
| `charm_def` | `container_id` PK · `axis` · `ap_cost` · `unique_carry` · `frame_hint` | ssot-charms §4.2 |
| `charm_pouch` | `(player_id, instance_id)` — durable intent, **no binding** | §4.2 |
| `charm_run_hold` | snapshot per run + `UNIQUE(instance_id) WHERE active = 1` — the exclusivity rule **is** the index | §4.2, mirroring `ix_rpg_expedition_members_active` |
| `charm_attunement` | `player_id` PK · `capacity` | §4.2 |
| `charm_resonance` | `(axis, count_req) → container_id` | §4.2 — **this is a breakpoint table**, so it is this module's evaluator input verbatim |
| the AP gate | budget · axis cap 3 · copy cap 2 · `unique_carry` 1 · `level_req` | §3.3, §5 |

`ap_cost ∈ {1,2,3,5}`, capacity **6 → 20**, all authored on the base type and never rolled (§3.3). Those
are **tunables** — `data/tuning/charm-attunement.v1.json` — not constants.

**Bindings apply at run start from a snapshot, never live** (§3.8): the RPG contributes a signed delta
from past events and never reads current game state, and an expedition is *sealed at dispatch by recorded
seed*. A pouch edit mid-run refuses `CharmInUse`; it is never silently held.

⚠ **Honest sizing risk.** Charm carry is five tables, a gate, five reason codes and a run-lifecycle hook —
plausibly larger than the threshold evaluator it attaches to. **The alternative is a module 22
`charm-carry` depending on this one.** Recorded rather than assumed; the owner decides at plan time.
Claiming it here is the safe default because the capability currently belongs to nobody, and an
unclaimed capability is how a lane ships half-built.

### Container kinds, ids, and the padding rule

**D27 closes a real blocker:** `ContainerKind` is a six-value C# enum (`Item · Trait · Skill ·
SpeciesPassive · Patron · WorldBuff`, `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:7-14`) and the
id regex mirrors it (`ContainerValidator.cs:17-19`). **Nothing this module grants has a legal home today.**

| Kind | Granted by | This module? |
|---|---|---|
| `set` | set bonuses **and** the frame-mix bonus | ✅ |
| `charm` | charm resonances | ✅ |
| `combo` | socket combinations — resonances and Strains/Splices | ❌ **module 16**, per-*item* scope |
| `gem` | socket inserts | ❌ module 16 |

⚠ **The `combo` consumer is the same shape at a different scope** — count inserts in one item, grant at
breakpoints — and module 16 should reuse this evaluator rather than write a second one. It is not folded
in here because its owner is the *host item's binding*, not the actor, and merging the two would make the
scope a parameter of a thing whose whole identity is its scope.

⚠ Four enum values, four `PrefixOf` arms (`ContainerRow.cs:142`, **not** `:77-86` as ssot-charms §4.3
states), four regex arms, and a `definitions.md` §1 grammar row. **That grammar row is the SSOT the regex
mirrors, and it wins over any spec** — an ask, not an edit ([item-ideal.md](../item-ideal.md) §2g #3,
owned by effect-atom).

**Ids keep ssot-sets §4.3's zero pad, and it is load-bearing rather than cosmetic:** the actor effect list
sorts by `container_id` **ordinal**, so unpadded `set.x-10` sorts before `set.x-2` and a ten-piece set
resolves its tiers out of order.

```text
set.{set_id}-{pieces:D2}        set.ember-legion-04
set.frame-mix-{ordinal:D2}      set.frame-mix-03      -- ascending in minorityMilli
charm.res-{axis}-{count:D2}     charm.res-offense-02
```

⚠ The shipped resonance corpus uses **unpadded** ids — `charm.res-offense-2`
(`data/seed/items/charms/resonance.json`). Ten rows, and the padding is a rename, not a migration.

`priority` is **0** for set and frame-mix tiers (identical to an item binding, ssot-sets §4.4) and
**−100** for charm bindings (ssot-charms §4.1), so an actor's own gear reads before the account layer.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ThresholdGrant"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Charm"
.\scripts\guard-dal.ps1          # the five charm tables' SQL stays inside FusionRpg.Data
.\scripts\guard-funnel-delta.ps1
```

## Project structure

```text
src/FusionRpg.Core/Items/ThresholdEvaluator.cs   new — bucket key + reducer + breakpoints -> wanted ids
src/FusionRpg.Core/Items/FrameMixPredicate.cs    new — the weighted min; reads budgetWeightMilli
src/FusionRpg.Core/Items/SetEvaluator.cs         new — the set consumer (ssot-sets §4.2 names this path)
src/FusionRpg.Core/Items/CharmPouchGate.cs       new — AP budget, axis cap, copy cap, unique_carry
src/FusionRpg.Core/Items/CharmRunBinder.cs       new — attuned set -> snapshot -> bindings
src/FusionRpg.Data/Sqlite/RpgStore.ItemSets.cs   new — item_set, item_set_member, item_set_tier
src/FusionRpg.Data/Sqlite/RpgStore.Charms.cs     new — the five charm tables
data/tuning/item-frame-mix.v1.json               new — the recovery curve and its breakpoints
data/tuning/charm-attunement.v1.json             new — ap_cost domain, capacity ladder, axis/copy caps
```

`src/FusionRpg.Core/Items/` **does not exist yet** — module 3 creates it (`spec-slot-roles.md`).
`rpg_item_assignment` is module 4's (`spec-equip-assign.md`); this module reads it and writes nothing to it.

## Code style

```csharp
// The minority count is WEIGHTED by role budget, never by item count. Unweighted, a 6/6 split concedes
// the six CHEAPEST roles - 15+15+40+50+50+60 = 230 of 800 permille - so D3's "parity, bought with
// per-slot quality" would be bought at 29% of the stated price. item-ideal.md 2g names this directly.
// long, not int: budgets are permille today, but this feeds a magnitude path and CLAUDE.md's rule is
// long for anything contentScale can touch. Widen before multiplying; divide by 1000 last, once.
static long MinorityMilli(IReadOnlyList<EquippedRole> equipped, IReadOnlyDictionary<ItemRole, int> budget)
{
    long humanoid = 0, plant = 0;
    foreach (var e in equipped)
    {
        if (!HybridCore.Contains(e.Role)) continue;     // the twelve, enumerated - never inferred
        checked
        {
            if (e.Frame == Frame.Humanoid) humanoid += budget[e.Role];
            else if (e.Frame == Frame.Plant) plant += budget[e.Role];
        }
    }
    return Math.Min(humanoid, plant);                    // MIN over two buckets, not a count over one
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `grants_are_cumulative_at_four_pieces` | 4 pieces holds the 2-piece container too — ssot-sets §2 |
| `unequipping_the_fourth_piece_withdraws_only_the_four_piece_tier` | the reconcile is exact, not a wipe |
| `re_evaluation_is_withdraw_and_rebind_never_a_patch` | derived state cannot drift |
| `the_evaluator_is_pure_and_runs_with_no_game_process` | SC8 — Core only |
| `frame_mix_is_a_min_over_two_buckets_not_a_count_over_one` | ⭐ the predicate shape D3 needs |
| `frame_mix_is_weighted_by_budget_permille` | ⭐ §2g |
| `a_six_six_split_of_the_cheapest_roles_concedes_230_not_400_permille` | ⭐ the exact defect, as a fixture |
| `a_cherry_picked_ten_two_body_sits_at_the_800_floor` | the anti-cheat, asserted from the abuse side |
| `an_even_budget_split_reaches_parity` | the recovery, from the intended side |
| `the_hybrid_core_used_by_the_predicate_is_twelve_roles_summing_to_800` | the registry contradiction, pinned |
| `breakpoints_come_from_tuning_not_from_code` | no literal ladder in C# |
| `tier_container_ids_sort_ordinally_in_numeric_order` | the zero pad — `set.x-10` after `set.x-02` |
| `a_charm_binding_at_player_scope_is_refused_while_player_resolves_match_wide` | ⛔ the live correctness bug, refused not shipped |
| `attuning_creates_no_binding` | ssot-charms §3.8 — intent is not a runtime fact |
| `bindings_apply_from_the_run_start_snapshot_not_the_live_pouch` | the seal; a run reproduces from its inputs |
| `un_attuning_a_held_charm_refuses_CharmInUse` | refuse, never silently hold |
| `the_partial_unique_index_is_what_enforces_exclusivity` | the rule is the index, not a check |
| `ap_budget_axis_cap_and_copy_cap_each_refuse_with_their_own_code` | four distinct player mistakes |
| `the_gate_re_runs_at_run_start_and_can_refuse_what_attunement_allowed` | capacity can shrink |
| `three_consumers_share_one_evaluator_with_no_forked_copy` | ⭐ the module's whole claim |

## Boundaries

**Always:** treat these bindings as derived and reconcile them totally; tag every one with a
consumer-specific `source`; enumerate the twelve hybrid-core roles explicitly wherever the frame-mix
predicate reads them; keep breakpoints and the recovery curve in `data/tuning/`; refuse with a reason code
the UI can print.

**Ask first:** whether charm carry stays in this module or splits to a module 22; which answer `player:`
gets (a real resolver, or per-actor binding); adding a sixth `ContainerKind` beyond D27's four; any edit to
the frozen `core.v1.json`.

**Never:** count items where the design counts budget — that is D3's mechanism failing silently at 29% of
its stated price. Never bind a set tier at `match` scope (`ScopeUnsupported`, ssot-sets §4.4) — one
demon's gear must not become a team buff. Never let a breakpoint be a hard progression ceiling; the top
breakpoint is a content limit, and the *count* it reads is unbounded. Never write a `player:`-scoped
combat atom while `StatApplyScope.cs:81-82` returns match-wide — it buffs the zombies.

## Success criteria

- [ ] One evaluator serves set bonuses, charm resonances and the frame-mix bonus, with no forked copy —
      proven by a test that instantiates all three from the same type.
- [ ] The frame-mix predicate is `min` over two **budget-weighted** buckets, and the 230‰ defect is a
      failing-then-passing fixture.
- [ ] The twelve-role hybrid core is enumerated, sums to 800‰, and the three stale 13-role sources
      (`core.v1.json`, `registries.py:111`, `linkage.py:28`) are reconciled or explicitly deferred with an
      owner decision recorded.
- [ ] Grants are cumulative, derived, reconciled totally, and withdraw by `source` as a group.
- [ ] Charm carry has a home: attunement → snapshot → binding, with the AP gate refusing by code, and the
      `player:` scope defect **refused rather than shipped**.
- [ ] Tier container ids are zero-padded and sort ordinally in numeric order.
- [ ] Breakpoints, the recovery curve, `ap_cost` domain and the capacity ladder all live in
      `data/tuning/`; no ladder literal survives in C#.
