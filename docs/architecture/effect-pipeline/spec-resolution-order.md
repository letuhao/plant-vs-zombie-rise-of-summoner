# Spec: `resolution-order`

**Module id:** `resolution-order` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 2 of 10
**Depends on:** `affix-schema` (module 1)

## Objective

Implement the resolver that walks the five-step order `definitions.md` §4a already made normative
(slots → affixes → atoms → tiers → values), each step on its **own** named RNG stream, replacing
`Instantiator.Draw`'s current single-stream, single-`pool_rolls`, atom-only draw
(`Instantiator.cs:160-196`) with the affix-aware version module 1's schema now supports. Also owns
**variant shifts** (Q12) — a variant moves the tier window or a roll count and authors nothing.

## Design

### The order, and why it is not the order the layers were designed in

`definitions.md:204-212` (normative, wins over this spec if they ever disagree):

```text
1. slots      pick concrete variants for every slot in the container/affix
2. affixes    draw which affixes appear (the container's pool draw)
3. atoms      expand each drawn affix's refs against the resolved slots
4. tiers      pick tier within the container's min_tier/max_tier window
5. values     roll each magnitude in its range
```

A slot must resolve before the concrete atom it names can be looked up; a tier must resolve before the
value range that tier implies can be read. The dependency direction — not the order the four layers
were designed in — fixes the order. **Stating it is what makes a roll reproducible**; today's
`Instantiator.Draw` conflates "which atom" and "which tier" into one weighted pick over
already-concrete `atom_id`s, which is exactly what an affix bundle with a slot cannot express.

### Four named streams, never one shared

```text
SeededRng.DeriveStream(seed, "system:purpose")   -- shipped pattern, FusionRoller.cs:27
```

| Layer | Stream name |
|---|---|
| slots | `affix.slot` |
| affixes | `affix.draw` |
| tiers | `affix.tier` |
| values | `atom.value` |

`definitions.md:231-236`'s own reasoning, restated because it is the module's whole reason to be more
than a refactor: if every layer drew from one stream, inserting a fifth layer later would shift every
historical roll's consumption, and `catalog_revision` cannot protect against that — it detects a
*content* change, not a change in how many random numbers the resolver consumes. Separate streams make
each layer's consumption independent, so a future layer costs nothing to the layers that already
existed.

**This repo already has the naming convention partially in use, not universally** (verified
2026-09-02): `fusion:traits`, `fusion:variant`, `fusion:promotion`, `battle:{index}` follow
`system:purpose`; a handful of older call sites (`BattleEngine.cs:92`, `BattleEffects.cs:238`) use bare
single-word streams predating the convention. This module's four new streams follow `system:purpose`
exactly — they are new code, not a retrofit of the older ones.

### The resolver's shape

```csharp
// One call per container instantiation. Each numbered step reads only its own stream and the
// output of the step before it — never a later step's output, or the dependency direction breaks.
public static ResolvedDraw Resolve(ContainerRow container, Func<string, AtomRow?> lookupAtom,
    long rollSeed, VariantShift? variant)
{
    var slotRng   = DeriveStream(rollSeed, "affix.slot" , container.ContainerId);
    var slots     = ResolveSlots(container, slotRng);                              // step 1

    var affixRng  = DeriveStream(rollSeed, "affix.draw" , container.ContainerId);
    var prefixRolls = variant?.ShiftPrefixRolls(container.PrefixRolls) ?? container.PrefixRolls;
    var suffixRolls = variant?.ShiftSuffixRolls(container.SuffixRolls) ?? container.SuffixRolls;
    var affixes   = DrawAffixes(container, affixRng, prefixRolls, suffixRolls);    // step 2

    var atoms     = ExpandRefs(affixes, slots, lookupAtom);                        // step 3 — no RNG

    var tierRng   = DeriveStream(rollSeed, "affix.tier" , container.ContainerId);
    var (min, max) = variant?.ShiftTierWindow(container.MinTier, container.MaxTier)
                     ?? (container.MinTier, container.MaxTier);
    var tiered    = ResolveTiers(atoms, min, max, tierRng);                        // step 4

    var valueRng  = DeriveStream(rollSeed, "atom.value" , container.ContainerId);
    return RollValues(tiered, valueRng);                                          // step 5
}
```

Step 3 (expand refs) is deterministic given steps 1-2's output — it needs no RNG of its own, which is
consistent with `definitions.md` listing it without a stream.

### ⛔ New dependency, added 2026-09-03 — a fifth stream for `channel-pool` (E30)

`spec-channel-pool.md` (E30, effect-atom Wave 7) lets an atom's `params.channel` name a pool instead of
one concrete channel — `{ "pool": "pool.element-power", "count": 1 }` — and needs this module to turn
that reference into concrete channels at roll time. E30's own §4 boundary correction already declares
the dependency in this direction ("E30 declares a dependency on effect-pipeline module 2"); this is the
matching acknowledgement on this side, since a spec that is depended on should say so too.

**Execution semantics are decided** (`spec-channel-pool.md` §3.2a, 2026-09-03): a pooled reference
expands into `count` separate `ResolvedAtom`s, same `atom_id`, same ONE rolled magnitude, different
concrete `channel` each — never one entry carrying an array. This runs inside step 5 (values), after an
atom's identity is already fixed by step 4, on a **fifth named stream**, `channel.pool`, derived exactly
like the other four:

| Layer | Stream name |
|---|---|
| slots | `affix.slot` |
| affixes | `affix.draw` |
| tiers | `affix.tier` |
| values | `atom.value` |
| **channel pool (E30)** | **`channel.pool`** |

The draw itself (weighted, without replacement unless `allowRepeat`) is the same weighted-pick shape
this module's own affix draw already implements — no second algorithm, this module's existing one
reused at a different layer. **Not yet implemented** — `RollValues` today clones a pool-object `channel`
value verbatim rather than resolving it, so a pooled reference currently freezes unread. This is the
named next step for whoever picks up this module's code again, not a silent gap.

`effect-pipeline-ideal.md` §7 Q12, and `ssot-rarity.md` §3.6: rarity buys breadth and tier ceiling,
never magnitude directly — *"a multiplier on the rung makes rarity dominant and destroys the overlap
the owner asked for"* (`CurveInput.Rarity` is banned on `container_kind = 'item'`). A demon-seed
`variant` (per `demon-seed`'s own `variants` anchor field) shifts a **resolution parameter**, and
authors nothing:

> **⛔ RE-VERIFIED 2026-09-03 (owner removed themselves as a gate) — the variant shift table is
> SHIPPED, and its one genuinely underspecified row has been resolved in data.** An objection that
> `corrupted`'s *"rerolls one element slot"* invents a field is **not true today**: the field is
> `VariantShift.RerollsOneElementSlot` (`src/FusionRpg.Core/Effects/Atoms/VariantShift.cs:19-23`),
> parsed from `rerollsOneElementSlot` by `VariantShiftTable.Parse` (`:90`), and honoured by
> `Resolver.ResolveSlots`' `corruptedRerollSpent` (`Resolver.cs:93`, `:111-115`) — *"spent at most once per
> resolve regardless of how many element slots exist."*
>
> **The row that WAS underspecified is `mutated`.** *"+1 pool draw"* never said **which budget**, and
> a resolver with two independent budgets cannot act on it. **It is resolved as `prefixRollShift: 1`**
> (`data/tuning/variant-shifts.v1.json`), which is the reading that keeps `mutated` distinct from
> `cursed` (suffix-weighted) and pairs its `-1 tier` with breadth on the same side `blessed` adds to.
> The table below is corrected to name the shipped fields rather than prose. **What would overturn
> it:** a balance pass wanting `mutated` to widen the suffix side — a one-key edit to the tuning file,
> which is exactly why it lives there.

| Variant | `tierWindowShift` | `prefixRollShift` | `suffixRollShift` | `rerollsOneElementSlot` |
|---|--:|--:|--:|---|
| `ancient` | +1 | 0 | 0 | — |
| `mutated` | −1 | **+1** | 0 | — |
| `corrupted` | 0 | 0 | 0 | **true** |
| `blessed` | 0 | +1 | 0 | — |
| `cursed` | 0 | −1 | +1 | — |
| `shiny` | 0 | 0 | 0 | — (cosmetic only) |

Zero new containers, zero new authoring per variant per species — a variant is felt across the whole
roster the instant its shift table changes. The shift table itself is `data/tuning/variant-
shifts.v1.json` (a balance surface, per `tunables-ssot.md`), never a literal in code.

**⚠️ t5 saturation.** A rung-10 `ancient` pushes the tier window past t5 — the highest tier that
exists. This **saturates**: `ShiftTierWindow` clamps to t5, and the clamp is a **structural** limit (no
t6 row exists to select), exempt from `AGENTS.md`'s no-hard-caps rule **and required to carry the
comment saying so**, or a later overflow/magic-number sweep correctly flags it as an illegal cap.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~(Resolver|VariantShift)"
python scripts/audit-magic-numbers.py --targets M1   # variant-shifts.v1.json is the balance surface
python scripts/audit-overflow.py                     # long for every rolled magnitude
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/Resolver.cs             new — the five-step Resolve(), replaces Draw
                                                            as the production entry point (Draw stays,
                                                            used internally by step 2/4, per-affix-class)
src/FusionRpg.Core/Effects/Atoms/VariantShift.cs          new — the shift table + t5 clamp
data/tuning/variant-shifts.v1.json                        new
tests/FusionRpg.Core.Tests/Atoms/ResolverTests.cs         new
tests/FusionRpg.Core.Tests/Atoms/VariantShiftTests.cs     new
```

## Code style

```csharp
// Each layer's stream is derived from the CONTAINER's own id, matching Instantiator.Draw's existing
// convention (AtomStreams.Pool + "." + container.ContainerId) — two containers rolled from one seed
// never share a sequence, and the same container always replays identically.
var slotRng = SeededRng.DeriveStream(rollSeed, "affix.slot" + "." + container.ContainerId);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `resolution_follows_the_five_step_order` | slots resolve before the atoms that reference them exist |
| `each_layer_draws_from_its_own_named_stream` | a stub RNG per stream proves no cross-layer consumption |
| `adding_a_future_layer_does_not_shift_an_existing_layers_draws` | the reproducibility law's own regression guard — proven by construction (a 5th stream added in test, the other four unchanged) |
| `master_of_fire_and_ice_resolves_as_one_correlated_draw` | the affix-bundle proof, end to end through the resolver |
| `variant_shifts_the_tier_window_and_authors_nothing` | no new container, no new atom |
| `ancient_at_rung_10_saturates_at_t5_not_a_progression_cap` | the structural-limit comment is present and the clamp fires |
| `same_seed_same_container_same_variant_reproduces_identically` | the reproducibility law, end to end |
| every existing `Draw`/`TryInstantiate` test | still passes — `Draw` is not deleted, only no longer the sole entry point |

## Boundaries

**Always:** resolve in the stated order; one named stream per layer; treat a variant as a resolve
parameter, never a container.

**Ask first:** adding a sixth resolution layer; changing a variant's shift table's *shape* (adding a
field the resolver reads, not adjusting its tuned numbers).

**Never:** let rarity or a variant multiply a magnitude directly (`ssot-rarity.md` §3.6); share one RNG
stream across two layers; hardcode a shift amount outside `data/tuning/variant-shifts.v1.json`; clamp a
tier window silently without the structural-limit comment.

## Success criteria

- [ ] `Resolver.Resolve` implements all five steps, each on its own named stream.
- [ ] A container with an affix bundle and a slot resolves correctly end to end.
- [ ] A variant shifts a resolution parameter with zero new authored rows.
- [ ] The t5 saturation clamp is present, tested, and carries the required structural-limit comment.
- [ ] Re-running an unchanged `(container_id, catalog_revision, roll_seed, variant)` is byte-identical.
