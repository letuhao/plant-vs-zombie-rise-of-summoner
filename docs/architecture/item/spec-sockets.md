# Spec: `sockets`

**Module id:** `sockets` · **Program:** [item](../item-map.md) · **Build order:** 16 of 21
**Depends on:** `equip-assign` (module 4), **`enhance-reroll` (module 15)**, `salvage-craft` (module 14)
**Downstream:** `strain-splice-gen` (module 21) generates the 102 · `item-surfaces` (module 20) renders the compendium
**Lane:** [ssot-sockets.md](ssot-sockets.md) (I4) — **read its amendment banner first; it has been amended twice**
**Rulings:** D20, **D21**, **D22 (as amended)**, D23, **D24**, D25, **D26**, **D27**

## Objective

Holes in items, the things that go in them, and **the combination evaluator** — the layer that turns a
fill into a bonus. Three capabilities:

1. **Sockets and inserts** — an insert is its **own instance** bound to the **same owner** as the host,
   never a row appended to the host's frozen atom list.
2. **The combination evaluator** — 25 generated resonances plus the **102 Strains and Splices** module 21
   generates, evaluated as one pure function of `(socket contents, socket affinities, host container)`.
3. **D21's set-piece exclusivity validator** — a set piece may not carry a Strain or a Splice.

## Design

### ⛔ Three corrections before anything else

The map's row for this module and the lane's own §5.2 are both wrong on a point that changes the build.

| # | Claimed | Verified | Consequence |
|---|---|---|---|
| **C1** | [item-map.md](../item-map.md)'s **old** row: *"no atom-table change"* | **False, and the map already flags it.** The lane asks for `bind_ordinal INTEGER NOT NULL DEFAULT 0` on `effect_binding` (§5.4). Today's DDL is `binding_id · instance_id · owner_kind · owner_key · slot · priority · source · bound_utc · revision` (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:83-92`) — **no `bind_ordinal`** | One column and one comparer arm, owned by effect-atom E6. §2 |
| **C2** | `ssot-sockets.md` §5.2: *"`item_socket` is a materialized view of I6's operation log, not the SSOT"* | **Superseded.** [D2 §6](decision-d2-mutation-contract.md) refused that request by name: *"`item_socket` is the SSOT for socket state. It is not a materialized view of anything."* D2 clause 13 exempts sockets from the reconstruction clauses entirely | Socket state is read from `item_socket`, never replayed. This module appends `socket-*` ops for **audit and idempotency only** |
| **C3** | `ContainerKind.Gem` exists / is one enum value away | `ContainerKind` is **six** values — `Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff` (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:7-15`) with six `PrefixOf` arms (`:142-151`). **No `Gem`, no `Combo`** | **D27's four kinds are an upstream dependency** on effect-atom ([item-ideal.md](../item-ideal.md) §2g, open #3). This module cannot author a single insert until `gem` and `combo` land |

⭐ **C3 also changes every combination id.** `definitions.md` §1 requires the `container_id` prefix to
match the kind, so D27's `combo` kind means the lane's `gem.combo-*` / `gem.word-*` ids become:

```text
combo.pure-fire-3      combo.ring-fire-ice     combo.eclipse      combo.diversity-3
combo.strain-<aptitude>-<archetype>            combo.splice-<aptitude-a>-<aptitude-b>
gem.<family>.t<n>                              -- inserts keep `gem`
```

### 1. The model — composition at the binding layer

An insert is **not** "an atom arriving after instantiation." It is its own `effect_instance`, bound to
the same owner as the host with a lifetime tied to the host's binding. Equip the item and the item's
atoms, every insert's atoms and every satisfied combination's atoms bind together; unequip and all of
them withdraw together.

**Nothing on the host is ever rewritten**, and that is verifiable rather than promised:
`InstanceRow.ContentFingerprint()` covers `ContainerId`, the instance's atom rows, `ThetaContent` and
`ContentScaleMilli` (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:58-62`). Socketing touches none of
them, so the reproduction contract survives untouched — **SC5 is not strained by this module at all.**

| Table | Written by socketing? |
|---|---|
| `effect_instance` / `effect_instance_atom` (**host**) | **no** |
| `effect_container` / `_atom` / `_pool` | **no** — content only |
| `effect_instance` / `_atom` (insert, combination) | yes — **new rows for new things**, not a mutation |
| `effect_binding` | yes — its normal use |
| `item_socket` | yes — **and it is the SSOT** (C2) |

### 2. `bind_ordinal` — the atom-layer defect this module is first to hit

Two identical inserts in two sockets of one item produce two bindings whose atoms sort **identically**
under the one execution order the program guarantees — `(priority DESC, container_id ASC, seq ASC)`. Same
container, same seq, same priority. That is a tie in a sort `definitions.md` §5 requires to be **total**,
and it is load-bearing: list order is what makes multi-atom `OnApply` draws reproducible, because two
atoms rolling on one hit consume the RNG stream in list order.

> **Request to effect-atom / E6:** add `bind_ordinal INTEGER NOT NULL DEFAULT 0` to `effect_binding` and
> append it to the effect-list comparer as the **final** tiebreak. Socket-layer bindings set it to
> `socket_index + 1`; everything else leaves it `0` and sorts exactly as it does today.

The tiebreak must be **content-derived** — §5 rejects `binding_id` precisely because it is generated
(`Guid.NewGuid()`, `RpgStore.AtomInstances.cs:221`). `socket_index` is content: recorded, stable, and
chosen by the player. **The comparer has no implementation yet**, so this costs a column and a sentence
now and a behaviour change later.

### 3. ⛔ `socket_max`, re-issued against the fifteen roles

`ssot-sockets.md` §4.1's table is **stale on two counts** ([item-ideal.md](../item-ideal.md) §2g, open #7):
it uses item-ideal §5.1's **old twelve** suffixed role ids (`core-protective`, `sense-utility`,
`mantle-utility`, `manipulator-offense`, `girdle-resource`, `head-protective`) and it assigns nothing to
`ward-array`, `infusion` or `retinue` — **two of which are in the twelve-role hybrid core.**

Re-issued against `ssot-equip-slots.md` §2.3's fifteen ids, ordered by their published budget weight, and
**with the `commander standard` row dropped** (out of scope per **D14**):

| # | `role_id` | Weight ‰ | `socket_max` | Why |
|---:|---|---:|---:|---|
| 1 | `armament-primary` | 160 | **4** | the identity slot — where a Strain should live |
| 2 | `core-guard` | 120 | **4** | the largest defensive budget on the body |
| 3 | `ward-array` | 90 | **3** | ⭐ **new** — the depleting layer; shields are a real second survivability currency |
| 4 | `armament-secondary` | 80 | **3** | the answering half |
| 5 | `jewel-major` | 80 | **1** | earns its place with affixes, not sockets |
| 6 | `manipulator` | 70 | **2** | rate and follow-through |
| 7 | `mantle` | 60 | **3** | the resistance home — element gems belong here |
| 8 | `head-guard` | 60 | **3** | disable resistance |
| 9 | `girdle` | 60 | **2** | the resource role |
| 10 | `sense` | 50 | **1** | narrow — accuracy and crit rate |
| 11 | `footing` | 50 | **2** | frame-split by design |
| 12 | `infusion` | 50 | **2** | ⭐ **new** — what your hits inflict |
| 13 | `retinue` | 40 | **2** | ⭐ **new** — what else is on the board |
| 14 | `jewel-minor-a` | 15 | **1** | the pair's budget is deliberately small |
| 15 | `jewel-minor-b` | 15 | **1** | identical twin, and that is the point |

**Thirty-four sockets on a fully-geared pure frame.** Two properties fall out and both are load-bearing:

- **`socket_max` is a ROLE property, fixed per role, not varied per base type.** That is what stops
  socket count being one number compared across the whole loot pool — a 1-socket ring and a 4-socket
  cuirass are not in the same conversation. ⚠ If a later module varies it *within* a role, this defence
  is gone and §8.1's failure returns at full strength.
- **The maximum is 4, and it is structural, not a progression ceiling.** It is a legibility limit — a
  four-ingredient recipe is memorable, a six-ingredient one is a wiki lookup — and the `const` must say
  so in a comment (AGENTS.md's caps rule).

### 4. ⚠ A per-actor Strain/Splice cap — and what the re-issued table does to it

§2g, open #8 records the gap: *"one-per-**item** is capped; twelve Splices on one actor is not. Tunable,
start at 3."*

**The re-issued table narrows this to a backstop, and the honest reading is worth stating:** D20 fixes a
Strain/Splice at **4 ingredients**, so only a role with `socket_max = 4` can host one. Under §3 that is
`armament-primary` and `core-guard` — **exactly the two roles §2g's own chaff-chassis note names**. The
geometric ceiling is therefore **2 per actor**, not twelve.

> **Ship the tunable anyway, at 3, as a non-binding backstop.** It is inert today and becomes live the
> moment a future `socket_max` revision widens the 4-socket set — which is precisely the change that
> would otherwise reopen the gap silently. `data/tuning/sockets.v1.json` → `maxCombosPerActor: 3`.

A fill that would exceed the cap does not fail the socketing; **the lowest-priority combination simply
does not fire**, and the socket UI says which and why. Refusing the insert would make a tuning value into
a player-facing wall.

### 5. Where sockets come from

```text
socketsAtDrop = min( base_type.socket_max, roll(rarity.socket_min .. rarity.socket_max, socketSeed) )
socketsNow    = socketsAtDrop + (recorded socket-add operations), capped at base_type.socket_max
                                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                                 available at EVERY rarity; the MATERIAL COST scales with rarity (D23)
```

`socketSeed = SeededRng.DeriveStream(roll_seed, "item.socket")` — domain-separated, so the socket draw
never consumes the affix pool's stream (`src/FusionRpg.Core/Battle/SeededRng.cs:26`). Nothing is stored,
so nothing can drift.

⚠ **`rarity.socket_min` / `socket_max` do not exist.** The `rarity` table is
`rarity_id · ordinal · prefix_rolls · suffix_rolls · min_tier · max_tier`
(`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:54-61`) **and it has zero rows today**. Two columns,
seeded by **module 7 `rarity-bands`** across the ten rungs, **append-only in the same sense `ordinal` is**
— a silent edit re-sockets every item ever dropped at that rung.

⚠ **D23's framing was overstated** (§2f.2): §4.1 *already* layered crafting top-up to
`base_type.socket_max`; only the per-rarity grant table starts at zero. **D23 confirms and prices that
layer** — it did not resolve a blocking contradiction. Module 14 owns the price row.

### 6. Affinity — a **bonus**, not a requirement (D22 as amended)

Every socket accepts every insert. **No socket ever rejects an insert for element.** Each socket carries
an **affinity** — one concrete element, or `''` — declared by the base type at drop, or **chosen by the
crafter via `socket.imbue`** on a socket that `socket-add` opened empty (**D24**).

D22 originally made affinity a hard requirement for a Strain or Splice. **Reverted by the owner
2026-09-03** (§2f.2): on a crafted low-rarity chassis every socket is crafted and D24 lets the crafter
choose the affinity, so *"the hard requirement could never fail — it was a fee wearing a gate's name."*
It also sidesteps the fact that **nothing maps the twelve aptitudes to the six elements.**

| Layer | Affinity effect when **every** contributor is attuned |
|---|---|
| **Resonance** | **+1 to effective count** — two earth inserts in earth-affinity sockets reach the k=3 step (§4.2's shipped pattern, unchanged) |
| **Strain / Splice** | ⭐ **fires at an enhanced tier** — the same `+1`, applied to the combination's granted tier rather than to a count, because a Strain has no count to raise |

`omni` is **not** an affinity — `element-hub-ssot.md` §4 is explicit that `omni` is not an actor type
slot, and `ElementRoster.Concrete` is six ids.

⚠ **Affinity never scales an insert's magnitude.** A scaled magnitude has to be frozen somewhere, and the
only honest place is the insert's own instance — at which point `gem.ember-shard.t3` is no longer the same
thing everywhere, inserts stop stacking, and the inventory defence collapses.

### 7. Inserts are fixed containers, and they stack

An insert is a `gem.*` container with **`prefix_rolls = 0` AND `suffix_rolls = 0`** — ⚠ not `pool_rolls = 0`;
that column was replaced (`ContainerRow.cs:119-127`). Five tiers, no rolled values, so every
`gem.ember-shard.t3` is identical everywhere and an insert in the bag is a **quantity**, not a row per
copy — the `rpg_demon_materials` shape (`RpgStore.cs:573-579`).

Upcycling is **stack arithmetic**: 3 × tier *k* → 1 × tier *k+1*. No instances, no mutation model. It is
the primary drain on the gem inventory.

**Rarity on an insert is a drop weight and a display colour, nothing more** — it selects no roll count
(always 0) and no tier window (the tier is in the id). Saying that plainly beats letting a column imply a
mechanism it does not have.

### 8. The combination evaluator — 127 rows, one pure function

```text
evaluate(fill, affinities, hostContainer) -> Combination[]
```

Pure, ordered, no RNG, no ambient state: **Strains/Splices first, then Pure (highest k per element), then
Ring, Eclipse, Diversity.**

| Tier | Count | Shape | Produced by |
|---|---:|---|---|
| **Resonance — Pure** | 18 | k inserts share one element, k ∈ {2,3,4} | generated at import, 6 elements × 3 |
| **Resonance — Ring** | 4 | ≥1 of each of two elements adjacent on `fire → ice → earth → air → fire` | generated |
| **Resonance — Eclipse** | 1 | ≥1 `light` and ≥1 `dark` — the mutual counter | generated |
| **Resonance — Diversity** | 2 | 3 or 4 *distinct* elements | generated |
| **Strain** | **36** | 12 aptitudes × 3 archetypes (offense · defense · balance), **4 ingredients** | ⭐ **module 21**, seedsmith |
| **Splice** | **66** | C(12,2), every unordered aptitude pair, **4 ingredients** | ⭐ **module 21**, seedsmith |
| | **127** | | |

Stacking rules:

| Rule | |
|---|---|
| **At most one Strain or Splice per item** | it is the item's identity, and two identities is one too many. If several match, the lowest `container_id` ordinal wins — content-derived, so deterministic |
| **Resonances do not consume inserts** | every shape evaluates over the full multiset, independently |
| **Within Pure, only the highest k per element fires** | three fire inserts fire `combo.pure-fire-3`, not `pure-fire-2` as well |
| **Ring, Eclipse and Diversity stack with each other and with Pure** | different shapes, different bonuses |
| **`omni` inserts count toward Diversity only** | the deliberate no-combo option: raw additive power for a player who does not want a puzzle |

⚠ **D20 supersedes §4.4's ≤20-and-learnable argument.** At 127 the catalog is past the bar §4.4 set, and
§8.2's wiki-dependency failure is live again. **The mitigation §4.4 already names becomes a requirement,
not a nicety:** the compendium reveals a recipe once the player has held every ingredient, and the socket
UI previews what the current fill produces and what is **one insert away**. Both are **module 20**'s to
build; this module's obligation is to expose `evaluate()` in a *preview* form — same function, hypothetical
fill, no writes — so module 20 has something truthful to render.

### 9. ⛔ D21's exclusivity validator

> **A set piece may not carry a Strain or a Splice.**

Two layers, two axes, and they do not overlap: a set is **across items** (collect the pieces); a
Strain/Splice is **within one item** (fill its sockets). This is also D2's own verified rule — runewords
work only in non-magical bases — arrived at here from mechanism separation rather than from copying.

⚠ **D21's *"base rarity: high"* row is struck** (§2f.2): **D15** rules the same day that a set has no
rarity and is completed from pieces of any rung. **The exclusivity rule stands on its own.** ⚠ D21's §8.6
citation is also wrong — §8.6 is *inserts counting toward set completion*, which §3.10 already settled.

**The validator, stated as behaviour:**

| Case | Behaviour |
|---|---|
| Host container carries set membership, fill matches a Strain/Splice | the combination **does not fire**. The inserts stay, every resonance still fires, and the socket UI states the reason |
| Host is a set piece, player attempts to socket toward a Strain | **allowed** — refusing the insert would punish a fill that is legal for resonance |
| Host is not a set piece | normal evaluation |

**Not a rejection code.** Nothing is refused, so nothing needs a code — a combination simply is not
satisfied. Minting a reason code for a bonus that did not fire would be a code no operator can act on.

⚠ **Still open and not this module's to close:** socket-combo budget versus set budget on one item
(§2g's surviving half of D21). **Module 9 `power-reads`** owns it — it is a budget question, and it cannot
be answered before the power reads run.

### 10. Removal

| Insert tier | Removal | Why |
|---|---|---|
| **t1–t2** | **free** | the learning tier; early play should never punish not-knowing |
| **t3** | **costed** (module 14's terms); the insert survives | the decision gains weight without becoming irreversible |
| **t4–t5** | **destroys the insert**; the item survives | the commitment tier |

**You can always empty a socket. What varies is what you keep.** An item can never be permanently ruined
by a bad insert, which is what makes it safe to socket mid-campaign gear.

⚠ **Socketing an insert costs 10 souls and nothing else** (module 14 §3). Moving a gem you already own
must never be a material decision, or players stop experimenting and the socket system dies quietly.

### 11. Operations and the op log

Four `op_kind` values, **all RNG-free**, all owned by module 15's namespace (`ssot-enhancement.md` §5.3):

| `op_kind` | Does | State |
|---|---|---|
| `socket-add` | opens a socket, affinity `''` | reserved in §5.3 |
| `socket-insert` / `socket-remove` | fill or empty a hole | reserved |
| ⭐ **`socket-imbue`** | sets an empty socket's affinity to one concrete element (**D24**) | **new — module 15 adds it** |

⚠ **`attune` was already taken.** `ssot-sockets.md` §4.2/§7.1/§7.2 use *attuned* for "an insert whose
element matches its socket's affinity". `socket.attune` would give one word two meanings in one lane.
**`imbue` is free** across the socket, item, rarity and slot vocabularies.

Per **D2 clause 13**, these ops are appended for **audit and idempotency only** (clauses 2, 8, 11). Nothing
reads them to rebuild state — `item_socket` is the SSOT. No socket operation touches the host's
`effect_instance_atom` rows or its `ContentFingerprint()`.

### 12. Reason codes

Three new, and each earns its place. ⚠ The enum is closed at 33 + `None`, asserted mechanically —
`Assert.Equal(34, reasons.Length)` (`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:33`).
**Adding three moves that assertion 34 → 37, and it is a reviewed change.**

| Code | Raised when | Why nothing existing fits |
|---|---|---|
| `NotSocketable` | the thing inserted is not an insert (`container_kind ≠ gem`, or a `combo.*` row), **or** the host's `socket_max` is 0 | both are "wrong kind of container for this operation", which no code says |
| `NoFreeSocket` | every socket is full when auto-picking; a `socket-add` would exceed `socket_max`; a remove targets an empty socket | the fix is *make room* |
| `SocketOccupied` | an explicit `socket_index` is already filled | the fix is *remove first* — folding it into `NoFreeSocket` would tell a player to add a socket when they need to empty one |

Six reused unchanged: `BadParamValue` (index out of range; a `gem` with a non-zero roll budget or a pool
row), `DuplicateKey` (a second copy of a unique-tagged insert), `StaleInstance`, `LevelTooLow`,
`UnknownContainer`, `ScopeUnsupported`.

**"Wrong type" is deliberately absent.** Under §6 a socket never rejects an insert for element, so there
is no wrong-type rejection to name. Stated because its absence would otherwise look like an oversight.

⚠ **Two inherited rejections that will bite, and they are wave-1 scope decisions, not surprises:** a
`+armour` insert at any per-actor scope is `ScopeUnsupported` (G8), and most element gems are
`stat.derived` and quarantined until E12. **Wave 1 inserts are restricted to `stat.modify`,
`resource.delta`, `status.apply`, `shield.grant` and the board/economy families**; the element gem catalog
is held. Authoring them earlier produces a row no code consumes, which is a lie in a table.

### 13. D26 — what this module does **not** do

No socket count is capped by player power. No combination is gated on `Θ`, account age, or how many items
the player owns. `socket_max` is a **legibility** limit on one item; the cost of `socket-add` scales with
the **target's** rarity, never with the player's. Content pacing is the world map's.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Combination"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSocket"
.\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --targets M1   # socket_max, the per-actor cap, removal tiers
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs      SHIPPED - effect-atom adds Gem + Combo to the
                                                       enum and PrefixOf (D27). NOT this module's
src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs   SHIPPED - effect-atom adds bind_ordinal (section 2)
src/FusionRpg.Core/Items/SocketGeometry.cs            new - socketsAtDrop, socket_max per role, the caps
src/FusionRpg.Core/Items/CombinationEvaluator.cs      new - the pure function, and its preview form
src/FusionRpg.Core/Items/ResonanceGenerator.cs        new - the 25, generated at import
src/FusionRpg.Core/Items/SetExclusivityValidator.cs   new - D21
src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs         new - item_socket + socket_combo_recipe DDL,
                                                       and the four operations (guard-dal)
data/tuning/sockets.v1.json                           new - socket_max per role, maxCombosPerActor,
                                                       removal tiers, upcycle ratio
data/seed/combos/resonance.v1.json                    generated - the 25
data/seed/combos/strain-splice.v1.json                MODULE 21's output - the 102
tests/FusionRpg.Core.Tests/Items/CombinationEvaluatorTests.cs   new
tests/FusionRpg.Core.Tests/Items/SocketGeometryTests.cs         new
```

## Code style

```csharp
// D22 as amended (item-ideal.md section 2f.2): affinity is a BONUS, never a gate. The hard
// requirement could not fail - on a crafted low-rarity chassis every socket is crafted and D24
// lets the crafter pick the affinity, so it was a fee wearing a gate's name. Resonance raises the
// effective COUNT (the shipped +1 pattern, section 4.2); a Strain has no count, so the same +1
// raises its granted TIER instead.
static int EffectiveCount(IReadOnlyList<Fill> contributors) =>
    contributors.Count + (AllAttuned(contributors) ? 1 : 0);

static int GrantedTier(ComboRecipe recipe, IReadOnlyList<Fill> ingredients) =>
    recipe.BaseTier + (AllAttuned(ingredients) ? 1 : 0);

static bool AllAttuned(IReadOnlyList<Fill> fills) =>
    fills.Count > 0 && fills.All(f => f.SocketAffinity.Length > 0 && f.SocketAffinity == f.InsertElement);

// socket_max = 4 is STRUCTURAL, not a progression ceiling (AGENTS.md caps rule): a four-ingredient
// recipe is memorable and a six-ingredient one is a wiki lookup. It bounds LEGIBILITY, not growth -
// growth past t5 rides contentScale (D29), which this layer never touches.
public const int SocketMaxCeiling = 4;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `socketing_never_writes_a_host_atom_row` | over a real store — the host's `ContentFingerprint()` is byte-identical after a six-op sequence |
| `item_socket_is_the_ssot_and_no_read_path_replays_the_op_log` | **C2 / D2 §6**, asserted directly against the stale §5.2 claim |
| `two_identical_inserts_in_one_item_sort_by_bind_ordinal` | **C1 / §2** — without it the total order is not total |
| `bind_ordinal_defaults_to_zero_and_non_socket_bindings_sort_unchanged` | the migration is inert for every existing binding |
| **`socket_max_is_defined_for_all_fifteen_roles`** | **§3** — the stale twelve-id table cannot come back, and `ward-array`/`infusion`/`retinue` are covered |
| `no_socket_max_row_exists_for_commander_standard` | D14 — out of scope, not silently included |
| `socket_max_is_fixed_per_role_and_never_varies_by_base_type` | §8.1's residual risk, closed as a test |
| `a_four_ingredient_combo_only_fits_armament_primary_or_core_guard` | §4's geometric ceiling, stated rather than assumed |
| `the_per_actor_combo_cap_is_read_from_tuning_and_is_currently_non_binding` | §4 — the backstop exists and is honest about being inert |
| `exceeding_the_per_actor_cap_drops_the_lowest_combo_and_refuses_no_insert` | a tuning value never becomes a player-facing wall |
| **`affinity_is_a_bonus_and_a_mismatched_fill_still_fires`** | **D22 as amended** — the gate is gone |
| `all_attuned_raises_resonance_count_by_one_and_strain_tier_by_one` | the shared `+1` pattern, both arms |
| `an_omni_insert_counts_toward_diversity_only` | never Pure, Ring or Eclipse |
| `only_the_highest_k_per_element_fires` | the Pure ladder cannot stack with itself |
| `one_item_fires_at_most_one_strain_or_splice` | and ties break on the lowest `container_id` ordinal, deterministically |
| **`a_set_piece_never_fires_a_strain_or_splice`** | **D21's validator** |
| `a_set_piece_may_still_be_socketed_and_still_fires_resonances` | D21 does not punish a legal fill |
| `no_reason_code_is_minted_for_an_unsatisfied_combination` | a code no operator can act on is not a code |
| `evaluate_is_pure_and_consumes_no_rng` | same fill, same affinities, same result, always |
| `the_preview_form_writes_nothing` | module 20's compendium reads it live |
| `a_gem_container_with_a_non_zero_roll_budget_is_rejected` | ⚠ `prefix_rolls`/`suffix_rolls`, **not** `pool_rolls` |
| `socket_seed_is_domain_separated_from_the_affix_pool_stream` | adding a socket later cannot move an item's affixes |
| `socket_imbue_sets_an_affinity_only_on_an_empty_crafted_socket` | **D24** — never on a filled one, never on a drop-declared one |
| `the_word_attune_is_not_reused_as_an_operation_name` | D24's naming constraint, as a test |
| `the_rejection_enum_length_assertion_moved_from_34_to_37` | §12 — a reviewed change, made visibly |
| `wave_one_inserts_use_only_kinds_with_a_live_runtime` | no `stat.derived` gem authored before E12 |

## Boundaries

**Always:** compose at the **binding** layer — an insert is its own instance on the host's owner; treat
`item_socket` as the SSOT (D2 clause 13); derive socket count from `roll_seed` with a domain-separated
stream; keep `socket_max`, the per-actor cap, the removal tiers and the upcycle ratio in
`data/tuning/sockets.v1.json`; expose `evaluate()` in a write-free preview form for module 20.

**Ask first:** varying `socket_max` **within** a role (it removes §8.1's main defence); raising the
per-actor combination cap above the geometric ceiling; a fifth resonance shape; whether the four-ingredient
count may vary per Strain (D20 fixed it at 4); whether a set tier may reference a socket condition as a
requirement (the one read-only seam offered to the set layer).

**Never:** append a row to the host's `effect_instance_atom`, or touch its `ContentFingerprint()`. Never
let a socket reject an insert for element. Never let affinity **scale** an insert's magnitude — inserts
would stop stacking and the inventory defence collapses. Never let an insert or a combination count toward
a set's piece count or thresholds. Never let a Strain or Splice fire on a set piece (**D21**). Never name
an operation `attune` (**D24**). Never treat `omni` as an affinity. Never define an `op_kind` here —
module 15 owns the namespace. Never gate a socket or a combination on a player property (**D26**).

## Success criteria

- [ ] `socket_max` is defined for **all fifteen** roles, `commander standard` is absent, and the old
      twelve suffixed ids appear nowhere — proven by test and by grep.
- [ ] `bind_ordinal` exists on `effect_binding`, is the comparer's final tiebreak, and two identical
      inserts in one item sort deterministically.
- [ ] `item_socket` is the SSOT; no read path replays the op log — asserted, not assumed.
- [ ] Affinity is a **bonus** on both layers (+1 count for resonance, +1 tier for Strain/Splice) and a
      mismatched fill still fires.
- [ ] The evaluator returns all 127 combination shapes correctly, is pure, and has a write-free preview
      form module 20 can render.
- [ ] A set piece never fires a Strain or Splice; the inserts stay and resonances still fire.
- [ ] The per-actor combination cap is in tuning, documented as a currently non-binding backstop, and
      exceeding it drops a combination rather than refusing an insert.
- [ ] `socket-imbue` exists as an `op_kind` (module 15) and has a cost row (module 14) before the first
      crafted affinity is set.
- [ ] Wave-1 inserts use only atom kinds with a live runtime; the element gem catalog is held for E12.
- [ ] Zero bare literals in `SocketGeometry` and `CombinationEvaluator` — `audit-magic-numbers.py` clean.
