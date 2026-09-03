# Spec: `enhance-reroll`

**Module id:** `enhance-reroll` · **Program:** [item](../item-map.md) · **Build order:** 15 of 21
**Depends on:** `power-reads` (module 9), `salvage-craft` (module 14)
**Lanes:** [ssot-enhancement.md](ssot-enhancement.md) (I6) · [ssot-reroll.md](ssot-reroll.md) (I7) ·
[decision-d2-mutation-contract.md](decision-d2-mutation-contract.md)
**Rulings:** **D7**, D9, D23, **D26**, D29

## Objective

The two operations that change an item after its rolls are frozen — **enhancement** (`+X`, the same item
made stronger) and **reroll** (the same item made *different*) — built on **one** mutation contract, so
there is exactly one op log, one replay law, one idempotency story, and one place a later module can
break them.

The contract is not designed here. **[D2 §9](decision-d2-mutation-contract.md) already ruled it** —
fifteen numbered clauses, adopted verbatim (§1). What this module owns is the two operations, their
prices, their risk shape, and **D7's mandate: a perfect item is reachable by grinding, never blocked by
luck.**

## Design

### 1. The mutation contract is adopted, not re-derived

D2 §9's fifteen clauses are binding and replace `ssot-enhancement.md` §7.8 wherever the two disagree.
The four that shape every line below:

| D2 clause | What it forces here |
|---|---|
| **1 — the head is the SSOT** | `effect_instance_atom.values_json` always holds current numbers. No read path composes anything. Enhancement rewrites in place |
| **3 — the guarantee is 1′ + 2 + 3** | `replay(origin_values_json, ops[1..n]) == head`, byte-exact, **with no catalog involved**. `state_hash` is the check; a mismatch is `ReplayDivergence`, a defect |
| **4 — record the result, never the recipe** | `result_json` holds materialised deltas and the decided `outcome`. Replay never re-runs the formula and never re-rolls the dice. **This is what makes a rebalance structurally unable to touch an owned item**, and it must not be traded for log size |
| **14 — there is no `effect_instance_atom.overrides_json`** | ✅ **Verified.** The DDL is `instance_id · seq · atom_id · values_json · power_json` (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:73-80`) and `Instantiator.Freeze` leaves an `OnApply` spec **as authored** (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:306-311`, `_ => raw`). Enhancing an `OnApply` affix rewrites `min`/`max` **inside the spec object in `values_json`** |

Columns this module adds — the only schema this module owns:

| Table | Column | Why |
|---|---|---|
| `effect_instance` | `enhance_level INT NOT NULL DEFAULT 0` | the `+X`. One writer |
| | `enhance_pity_counter INT NOT NULL DEFAULT 0` | §4's catalyst counter, reset on guarantee |
| | `mutation_seq INT NOT NULL DEFAULT 0` | `= max(op_seq)`. Structural cap 4096, and it says so in a comment |
| | `state_hash TEXT` | definitions §8's canonical form — SHA256, length-prefixed, sort-then-concatenate, **XOR-fold banned** |
| | `origin_values_json TEXT NULL` | D2 rung 1′. Written lazily at first mutation (D2 §11.3's lean) |
| `effect_instance_atom` | `suppressed INT NOT NULL DEFAULT 0` | D2 clause 9 — identity change is suppress-then-append, `seq` is never renumbered |
| `effect_instance_op` | *(new table, D2 §9 clause 2)* | the ledger. `UNIQUE(instance_id, correlation_id)` |

⚠ `origin_catalog_revision` is **not** a new column — it already exists as `effect_instance.catalog_revision`
(`RpgStore.AtomInstances.cs:66`), and D2 §7.1 granted it as a **semantic lock**: origin-only, no operation
rewrites it. I6 §5.1's request for a new column was refused.

### 2. ⛔ Platform correction — `pool_rolls` does not exist, and it breaks I7's algebra

I7 is built end to end on `pool_rolls`: `T` targets, `K = pool_rolls − T` anchors, `ANCHOR_MULT = 2^K`,
`T > pool_rolls` rejects, and a *"two sources of truth for `pool_rolls`"* hazard handed to I1 (§4.2).
**Verified: the column is gone on both tables.**

| I7 claim | Verified |
|---|---|
| `ContainerRow.PoolRolls` (`ContainerRow.cs:64`) | **`PrefixRolls` / `SuffixRolls`** (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:119-127`), DDL `RpgStore.Containers.cs:27-28`. The doc comment states it *"replaces the single `PoolRolls`"* |
| `RarityRow.PoolRolls` (`ContainerRow.cs:93`) — the second source of truth | **`RarityRow(RarityId, Ordinal, PrefixRolls, SuffixRolls, MinTier, MaxTier)`** (`ContainerRow.cs:158`), DDL `RpgStore.Containers.cs:54-61`. ⭐ **The hazard I7 handed to I1 no longer exists** — both tables split the same way, and the container's is still authoritative |
| `effect_container_pool(container_id, atom_id, weight, group)` | **`(container_id, affix_id, weight, group_key)`** (`RpgStore.Containers.cs:44-50`, `ContainerRow.cs:38`). A reroll re-draws **affixes**, not bare atoms |
| `Instantiator.Draw(container, lookupAtom, seed)` with one exclusion set | **`Draw(container, lookupAtom, lookupAffix, rollSeed)`**, which runs `DrawBudget` **twice** — once per budget, each with its own RNG stream `pool.{budgetName}.{containerId}` (`Instantiator.cs:180-203`) |

**Consequence — anchoring is per budget, and it is a better design than the one it replaces:**

```text
T_prefix + T_suffix  >= 1                      // at least one target, or the op is a paid no-op
K_prefix = container.PrefixRolls - T_prefix
K_suffix = container.SuffixRolls - T_suffix
ANCHOR_MULT = 2 ^ (K_prefix + K_suffix)        // superlinear, unchanged in shape
```

A partial redraw seeds each budget's exclusion set with the **groups of that budget's retained affixes**
before drawing — the one behavioural change this module needs from `Instantiator`, and it is now two
parameters on `DrawBudget` rather than a new signature on `Draw`. The existing call site passes the full
counts and an empty set, so **instantiation is byte-unchanged**.

⚠ **The reroll post-op invariant restates per budget:**

```text
count(drawn prefix affixes) == container.PrefixRolls
count(drawn suffix affixes) == container.SuffixRolls
distinct groups(drawn)      == count(drawn)                     // one-per-group holds
every drawn affix           in container.Pool
every drawn atom's tier     in [container.MinTier, container.MaxTier]
```

⚠ **A `Mixed` affix consumes one prefix roll AND one suffix roll simultaneously** (`ContainerRow.cs:41-47`).
Rerolling a `Mixed` affix therefore frees a slot in **both** budgets and must redraw into both, or the
invariant fails. `Instantiator.Draw`'s own comment calls today's two-independent-draws model *"an interim,
honestly-documented simplification"* (`Instantiator.cs:174-178`) — **this module must not build a second
simplification on top of it.** If module 2 `resolution-order` has not landed the real semantics, a reroll
targeting a `Mixed` affix is refused with `NotRerollable` until it has. Stated so it is a decision, not a
bug found later.

### 3. ⛔ D7 — crafting reaches t5, gated by COST, never by luck

> *"Looking for a perfect item (of course very op item) will be cost very much effort but dont make it
> impossible by chance, that is not fun."*

Three requirements, each answered by a named mechanism:

| D7 requirement | Mechanism | State |
|---|---|---|
| Material cost scaling steeply with affix tier and rarity | module 14's cost table, keyed on the **target's** rung and tier | ✅ built by 14 |
| A success chance on strong crafts | §4's three bands | this module |
| **Bad-luck protection — mandatory, not optional** | §5's guaranteed-tier counter | this module |

**And the top of the ladder is reachable.** `ssot-rarity` rule 7 was lifted by the owner 2026-09-03
([item-ideal.md](../item-ideal.md) §2f.2, D7 row): promotion reaches ordinal 100, **no drop-only band
exists on any axis**, so no affix family sits behind luck.

### 4. The risk shape, and why the cost curve is the only cap

Three bands (I6 §4 D3+D4, kept):

| Band | Levels | Success | Failure |
|---|---|---|---|
| **Safe** | +1 … +8 | 1000‰ | — |
| **Risk** | +9 … +14 | 950‰ → 600‰ | materials spent, level unchanged, pity counter +1 |
| **Peril** | +15 … up | 500‰ → 200‰ | as above; from **+17** a failure may drop **one** level unless a `ward.enhance` is loaded |

**There is no destroy outcome — not as an enum value, not as a reason code.** A code nothing emits is a
lie in a table, and reserving one invites a later session to wire it up.

#### ⛔ The cap rule — this is where AGENTS.md bites

`ssot-power-scale.md` §11 is explicit that *"a flat rate facing a scaling sink"* **is a cap**. A steep
enhancement cost curve facing an unbounded content ladder is exactly that shape.

| Thing | Standing | Where it lives |
|---|---|---|
| The cost curve | **configurable soft cap** — it makes the next level less worth it, it never refuses | `data/tuning/enhancement.v1.json` |
| The risk curve (falling success) | **configurable soft cap** — same shape, same file | same |
| `mutation_seq ≤ 4096` | **structural**, and the comment says so: a retry loop, not a design ceiling | `const` in code |
| `ilvl_cap(ilvl) = max(4, 4 + ilvl/4)` | **floor only, no ceiling** — ilvl 128 → +36, ilvl 500 → +129 | tuning |
| ⛔ `rarity_cap` per rung, topping out at +20 | **REMOVED.** I6 §7.3 already reconciled this once; the ten-rung ladder and D29's unbounded content ladder finish the job | — |

D29: **tier saturates at t5 and that is correct** — growth past it is carried by `contentScale`, which is
built (`InstanceProducer.cs:47`, `ContentScale.Milli`). An ilvl-500 t5 affix is the same tier as an
ilvl-32 one and a far bigger number. **So an enhancement cap keyed on tier would be a ceiling on a system
that has none.**

⛔ **D26 applies here too:** the enhancement cost curve reads the **target's** rarity ordinal, item level
and current `+n`. It never reads the player's `Θ`, power index, item count or any per-day counter.

### 5. ⛔ Bad-luck protection — and a genuine cross-document conflict, resolved

`rpg_summon_pity` is the in-tree precedent and it is **verified**:

| Claim | Verified |
|---|---|
| a persisted per-player pity table | `rpg_summon_pity(player_id PK, pulls_since_epic, pulls_since_legendary, updated_utc)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:529-534`) |
| two counters, cross-banner, visible | `PityState(PullsSinceHeirloom, PullsSinceSunwoven)` (`src/FusionRpg.Core/Demons/SummonRoller.cs:12`) — the SQL column names deliberately kept their old labels (`SummonRoller.cs:6-11`) |
| read and written inside the pull transaction | `RpgStore.Summons.cs:200`, `:210` |
| hard pity at 25, soft ramp from 41, hard at 55, a 10-pull floor | stated in `SummonRoller.cs:23-30` |

#### The conflict

- **D7** requires *"a perfect item must be reachable by grinding… never impossible"*, which on a
  tier-hunting reroll means a **tier** guarantee.
- **`ssot-rarity.md` §3.8** forbids exactly that: *"Pity may key on **rung only** — never on roll quality,
  never on tier. A quality pity makes draws non-independent, and §3.5's invariant is measured on
  independent draws."*
- §3.5's overlap invariant is **measured, not asserted** — 2 × 10⁵ rolls per rung, seed `20260822`,
  `U(n,1)` 7.9–28.3 % against a required 5–30 % (`ssot-rarity.md` §3.5). **Adding a tier pity to the draw
  would invalidate the measurement**, not merely change a number.

#### The resolution — decided, and it is not a compromise

> **§3.8's rule is scoped to *drop* pity. Craft pity is a separate deterministic mechanism, and it
> touches no weight.**

Read §3.8's own heading row before quoting it as a universal law: every one of its rules is about
**counted drop sources** — *"expedition completion, boss kill, chest open"* — and every one of its levers
is a **weight shift** on a draw. §3.5's independence premise is a premise about *drops*. It says nothing
about an operation the player pays for on an item they already own.

| | Drop pity (`ssot-rarity` §3.8) | **Craft pity (this module)** |
|---|---|---|
| Triggered by | a drop event the player did not pay for | a **catalyst spend** the player chose |
| Keys on | rung only | **tier**, per `(instance, affix group)` |
| Mechanism | shifts the rung weights | ⭐ **a counter that, at N, makes the next draw's tier deterministic** |
| Touches the weight table | **yes** | **no — never** |
| Effect on §3.5's independence | would break it | **none.** The weighted draw is untouched; at the threshold it is *not run at all* |

**Concretely:** every failed reroll on a target group increments `enhance_pity_counter`. At the tuned
threshold the next reroll on that group **does not roll a tier** — it is placed at `max_tier` of the
container's window and the counter resets. Independent draws stay independent, because the guaranteed
draw is not a draw.

⭐ **This is I7's own Imprint, corrected.** I7 §3.4 already reached for a deterministic escape hatch and
placed it at the window **floor** (*"deliberately mediocre"*), then rejected a pity counter for needing
durable state. Two things changed: D7 makes the *ceiling* reachable by cost, not the floor; and the
durable state is one integer on a column this module is adding anyway.

**Decider if you disagree: the owner.** The alternative is to leave D7 unimplementable on the tier axis
and tell the player the top tier is a lottery — which D7 rules out by name. `ssot-rarity.md` §3.8 needs a
one-line scope edit (*"drop pity may key on rung only"*), which is module 7's to make.

### 6. The two operations

| Operation | Redraws | Targets | `op_kind` |
|---|---|---|---|
| **Enhance** | nothing — adds a scalar + milestone atoms | the whole item | `enhance` |
| **Temper** | the **value** of one affix, in its own range | exactly one drawn `seq` | `reroll-value` |
| **Reforge** | **identity, tier and value** of a chosen subset | `T ≥ 1` drawn `seq` values, per budget | `reroll-affix` |
| **Imprint** | nothing — **places** a chosen group deterministically | one drawn `seq` | `reroll-affix` |
| **Restore** | administrative rollback to a recorded `op_seq` | — | `restore` |

The `op_kind` namespace is **this module's** (`ssot-enhancement.md` §5.3) and modules 14 and 16 draw
from it. Module 16 needs three that do not exist yet:

| `op_kind` | Owner | Note |
|---|---|---|
| `socket-add` · `socket-insert` · `socket-remove` | 16 | reserved in §5.3 |
| ⭐ **`socket-imbue`** | 16 | **new — D24's operation had no `op_kind`.** Added here, because inventing it in module 16 would fork the namespace |

**Enhancement's two components** (I6 §3.3, unchanged): a **+20‰-per-level scalar** applied to the origin
value and never compounded, and **milestone atoms** at +4/+8/+12/+16/+20 drawn from a reserved family
space no affix pool may draw from. Implicits are never scaled. At its cap the whole ladder is worth
roughly **one rarity rung** — enough that a maxed lower rung overlaps the next, never enough to clear it.

### 7. What can never be rerolled

The line is **drawn versus authored**, and it is what makes "an item the generator could never have
dropped" structurally impossible:

| Never | Why |
|---|---|
| Base type | it is `container_id`, the first term of the reproduction contract |
| Implicits and base stats | `effect_container_atom` rows — the fixed core, never in the pool |
| Affix **count** | `PrefixRolls`/`SuffixRolls` are rarity-selected container columns |
| Rarity | lives on the container (`ContainerRow.cs:109`), not the instance |
| Set membership | a container tag |
| Sockets and their inserts | module 16's; a reroll must leave every insert in place and must never reset socket count |

### 8. ⚠ D23 is a **pricing** ruling, and its framing was overstated

D23 reads as *"this resolves a blocking contradiction"* — that a Strain was structurally unbuildable
because low rarities grant zero sockets. **§2f.2 corrected it:** `ssot-sockets.md` §4.1 *already* layered
crafting top-up to `base_type.socket_max`; only the per-rarity **grant table** starts at zero.

So what D23 actually decides is this module's business and nothing more:

> **`socket.add` is available at every rarity. Rarity sets the price, not the possibility.** That is a
> **soft cap** — AGENTS.md's required shape — and it is D7's *"cost, never luck"* applied to a third
> mechanism. `base_type.socket_max` stays a **hard structural cap** (max 4, fixed per role): a legibility
> limit, not a progression ceiling, and it must say so in a comment.

Module 14 owns the price row; module 16 owns the operation. This module owns only the `op_kind` and the
guarantee that the op is logged, idempotent and atomic like every other.

### 9. ⚠ Two shipped defects this module cannot ship over

Both belong to **module 1 `durable-ownership`**, both verified today, both stated here because this
module is the one that dies on them:

| Defect | Verified at | Effect on this module |
|---|---|---|
| **Unequipping deletes the item.** `CollectOrphanInstancesUnlocked` deletes every `effect_instance` with no `effect_binding` row, and runs after every withdraw | `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:611-622`, called at `:565` and `:583` | The natural workbench flow — take it off, improve it, put it back on — **destroys the item**. No reroll operation can ship first |
| **A content import refuses every instance.** `if (instance.CatalogRevision != current) … StaleInstance` | `RpgStore.AtomInstances.cs:437-441` | D9 removes it. ⚠ **D9's premise was corrected (§2f.2): the bind path never reads the frozen values** — `ResolveBindings` uses `instance.Atoms` as an id list and populates from the **live** catalog. **Sequencing: make frozen values authoritative at bind time FIRST, then drop the revision check** |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Enhance"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Reroll"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Replay"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
.\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --targets M1   # odds, scalar, escalation all in tuning
python scripts\audit-overflow.py                     # long on every scaled magnitude
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/Instantiator.cs       SHIPPED - DrawBudget gains `count` and
                                                        `excludeGroups`; Draw's signature is unchanged
src/FusionRpg.Core/Items/MutationOp.cs                 new - the op record, op_kind enum, result deltas
src/FusionRpg.Core/Items/MutationReplay.cs             new - transcript replay + state_hash compare
src/FusionRpg.Core/Items/EnhancePolicy.cs              new - scalar, bands, odds, caps. Pure
src/FusionRpg.Core/Items/RerollPolicy.cs               new - temper/reforge/imprint, per-budget anchors
src/FusionRpg.Core/Items/CraftPityCounter.cs           new - the guaranteed-tier counter (section 5)
src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs      new - effect_instance_op DDL + append (guard-dal)
data/tuning/enhancement.v1.json                        new - scalar, odds, pity threshold, cost curve,
                                                        escalation cap. THE soft cap lives here
tests/FusionRpg.Core.Tests/Items/MutationReplayTests.cs   new
tests/FusionRpg.Core.Tests/Items/EnhancePolicyTests.cs    new
tests/FusionRpg.Core.Tests/Items/RerollPolicyTests.cs     new
```

## Code style

```csharp
// Transcript replay, D2 clause 4: apply the RECORDED delta, never re-run the formula. This is what
// makes a rebalance structurally unable to reach backwards into an item a player already owns - a
// re-simulating replay would silently un-succeed an attempt they paid for. The rules table is not
// even reachable from here, which is the enforcement.
static InstanceHead Replay(InstanceHead origin, IReadOnlyList<MutationOp> ops)
{
    var head = origin;
    foreach (var op in ops)                       // dense, gapless, in order (D2 clause 7)
        head = ApplyRecordedDeltas(head, op.ResultJson);
    return head;
}

// Craft pity: at the threshold the tier is PLACED, not rolled. ssot-rarity section 3.8 forbids a
// tier pity that shifts DRAW WEIGHTS, because section 3.5's overlap invariant is measured on
// independent draws (2e5 rolls, seed 20260822). This touches no weight - at the threshold the
// weighted draw is not run at all - so the measurement stands. See spec section 5.
static int TierFor(RerollContext ctx, AtomRandom rng, EnhancementTuning t) =>
    ctx.PityCounter >= t.CraftPityThreshold
        ? ctx.Container.MaxTier!.Value                    // guaranteed, counter resets
        : rng.PickTier(ctx.Container.MinTier!.Value, ctx.Container.MaxTier!.Value);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `replay_of_origin_plus_ops_equals_the_head_for_every_mutated_instance` | D2 clause 3, over a **whole fixture database**, not a spot check |
| `a_rebalance_of_the_odds_table_changes_no_owned_item` | clause 4, the property this design most deliberately buys |
| `replay_never_reads_the_rules_table` | enforced by the type, then asserted |
| `a_replayed_correlation_returns_the_recorded_result` | clause 8, copied from `RpgStore.Souls.cs:189-213` |
| `a_reused_correlation_with_different_parameters_is_refused` | not silently applied |
| `op_seq_is_dense_and_an_out_of_order_arrival_is_OpSequenceGap` | clause 7 |
| `a_head_log_mismatch_raises_ReplayDivergence_loudly` | clause 12 — a defect, never a warning |
| `seq_is_never_renumbered_and_an_identity_change_suppresses_then_appends` | clause 9 |
| `an_OnApply_affix_is_enhanced_by_rewriting_min_max_inside_values_json` | clause 14 — **no `overrides_json`**, pinned against `Instantiator.cs:306-311` |
| **`anchoring_is_computed_per_budget_not_from_pool_rolls`** | §2 — the platform correction, asserted so the stale algebra cannot come back |
| `a_reforge_preserves_prefix_rolls_and_suffix_rolls_exactly` | the post-op invariant, per budget |
| `rerolling_a_mixed_affix_redraws_into_both_budgets_or_is_refused` | §2's `Mixed` hazard, decided rather than discovered |
| `a_partial_redraw_seeds_the_exclusion_set_with_retained_groups` | one-per-group survives a partial reroll |
| `a_rerolled_item_always_validates_as_freshly_instantiated` | the "impossible item" failure, structurally |
| `a_reroll_never_touches_a_socket_or_an_insert` | module 16's boundary |
| **`the_pity_counter_guarantees_max_tier_at_the_threshold`** | **D7** — the top tier is reachable by cost |
| **`craft_pity_shifts_no_draw_weight`** | §5's resolution — the guarantee **replaces** the draw, it does not bias it, so §3.5's measurement stands |
| `pity_resets_on_a_guaranteed_draw_and_persists_across_sessions` | the `rpg_summon_pity` shape, reused |
| `there_is_no_destroy_outcome_in_the_enum_or_the_reason_codes` | asserted directly — a code nothing emits is a lie in a table |
| `no_enhancement_cap_is_a_hard_stop` | `rarity_cap` is gone; `ilvl_cap` has a floor and no ceiling |
| `the_cost_and_odds_curves_are_read_from_data_tuning` | AGENTS.md's balance-surface rule, mechanically |
| `no_cost_or_odds_input_reads_a_player_property` | **D26**, same guard shape as module 14's |
| `mutation_seq_is_capped_at_4096_and_the_comment_says_it_is_structural` | the one legal ceiling, and why |
| `every_scaled_magnitude_is_long_and_overflow_throws` | `+20` on an ilvl-500 t5 affix is not an `int` |

## Boundaries

**Always:** adopt D2 §9's fifteen clauses verbatim; record the result, never the recipe; append to
`effect_instance_op` on every mutation; carry a `correlation_id` with `UNIQUE(instance_id, correlation_id)`;
derive randomness via `SeededRng.DeriveStream(op_seed, "item.{op_kind}")`
(`src/FusionRpg.Core/Battle/SeededRng.cs:26`) — one named stream per op kind, recorded even when unused;
commit op row, material debit and head rewrite in one transaction; keep every odds, scalar and cost number
in `data/tuning/enhancement.v1.json`.

**Ask first:** ⛔ **scoping `ssot-rarity.md` §3.8's pity rule to drop pity** (§5 — recommended, **decider:
the owner**); adding an `op_kind`; a player-facing un-enhance; whether enhancement extends to charms and
inserts (scoped here to equipment).

**Never:** re-simulate replay — a nerf must not un-succeed a paid attempt. Never add
`effect_instance_atom.overrides_json` (D2 refused it, and the premise it rested on is refuted by a passing
test). Never a destroy outcome. Never a hard cap on `+X` — the risk and cost curves are the cap and they
live in tuning. Never a cost or odds term reading the player's `Θ`, level or a per-day counter (**D26**).
Never touch a socket, an insert or `item_socket` — module 16 owns them and D2 clause 13 exempts them from
clauses 3 and 4. Never renumber `seq`. Never delete an op row.

## Success criteria

- [ ] D2 §9's fifteen clauses are implemented and each has a named test.
- [ ] `replay(origin_values_json, ops) == head` byte-exact for **every** mutated instance in a fixture
      database, with no catalog involved.
- [ ] Anchoring, targeting and the post-op invariant are all expressed **per budget** — no `pool_rolls`
      anywhere in the module, proven by grep and by test.
- [ ] **D7 holds: `max_tier` is reachable by spending, on every affix group, with no luck floor** —
      proven by the pity test, and the guarantee shifts no draw weight.
- [ ] `ssot-rarity.md` §3.8 carries the drop-pity scope edit, or the owner has ruled otherwise.
- [ ] No hard cap on `+X`; the cost and risk curves live in `data/tuning/enhancement.v1.json` and
      `audit-magic-numbers.py` reports no M1 target in `EnhancePolicy` or `RerollPolicy`.
- [ ] `socket-imbue` exists in the `op_kind` namespace before module 16 needs it.
- [ ] Module 1's two defects (orphan sweep, revision-equality) are closed before the first operation ships.
