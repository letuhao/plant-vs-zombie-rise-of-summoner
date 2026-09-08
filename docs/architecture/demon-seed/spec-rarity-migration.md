# Spec: `rarity-migration`

**Module id:** `rarity-migration` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 11 of 16
**Model calls:** none. **This is the largest and riskiest non-runtime module in the program, and it
carries a data migration.**

## Objective

Migrate `DemonRarity` from four values to the ten-rung ladder, update every consumer, migrate persisted
shard material ids, and reverse `ssot-rarity.md` §4.3.

Owner, Q4: *"Adopt the full 10-rung item ladder for demons."*
Owner, Q24: *"Migrate DemonRarity to 10 values now."*
Owner, Q15: *"All ten, two pity guards at 70 and 90."*

## Design

### 1. What is being reversed, stated plainly

[item/ssot-rarity.md](../item/ssot-rarity.md) §4.3 is not silent on this. It decided the opposite:

> *"**Demons keep their own ladder.** `DemonRarity` stays a four-value code enum, and a one-way band
> map lets the two be compared without being fused … The map exists so a legendary demon's drop table
> can be written in item rungs and so `shard.legendary` can be priced — **not** so the two ladders can
> be merged later."*

The owner has overruled that. **The document is amended, not ignored** — its §4.1 option A and §4.3
rewrite to record that demons adopted the ladder on 2026-09-01, why, and that the four-row band map
became a one-time migration table rather than a permanent boundary. Leaving a shipped decision
document contradicting shipped code is how the next session designs against the wrong constraint.

### 2. The real consumer inventory — the ideal doc said "five"

Verified by grep, 2026-09-01. It is not five.

| Consumer | What breaks |
|---|---|
| `Battle/WaveCatalog.cs:41-44` | four explicit bands, one per value |
| `Demons/DemonMaterialCatalog.cs:18-19` | enumerates all four; composes `shard.{rarity.ToId()}` |
| `Demons/Fusion/FusionRoller.cs:77` | `SlotsFor` switch, four arms |
| `Demons/Fusion/StarPolicy.cs` | `StarCap`, and promotion gated on `Legendary` being the top |
| `Demons/Fusion/FusionTuning.cs` | `StarCap` / `RecipeCost` dictionaries keyed by rarity; `ParseRarity` from strings |
| `Demons/Contracts/ContractTuning.cs` · `ContractPolicy.cs` | `BaseUpkeepPerDay`, `RitualPriceSouls` keyed by rarity |
| `Demons/SoulEarnPolicy.cs:43` · `SoulEarnTuning.cs:16` | `DiscoveryDelta` keyed by rarity |
| `Demons/SummonRoller.cs:21` · `SummoningTuning.cs:8` | rates 74/20/5/1 and both pity thresholds |
| `Demons/Fusion/DemonRecipeCatalog.cs:50,60` | **ordinal arithmetic and a relational comparison — see §3** |
| `Expeditions/ExpeditionResolver.cs:168-169` | **hardcoded id strings — see §4** |
| `Data/Sqlite/RpgStore.Fusion.cs:374` · `Server/FusionEndpoints.cs:202` | compose shard ids from rarity |

### 3. The two silent landmines in one method

`DemonRecipeCatalog.cs:60`:

```csharp
(DemonRarity)((int)output.BaseRarity - 1)
```

*"One rung below the output"* — written when a rung was one of four. **After the migration it still
compiles, still runs, and silently means one rung of ten.** A fusion recipe that used to demand
inputs a quarter of the ladder below now demands inputs a tenth below, and nothing anywhere reports
an error.

`DemonRecipeCatalog.cs:50` — ten lines earlier in the same method — is the other half:

```csharp
.Where(s => s.BaseRarity >= DemonRarity.Rare && s.Acquisition != DemonAcquisition.CaptureOnly)
```

*"Rare or better"* meant **three quarters of the ladder**. After widening it means **nine tenths**, so
the set of species that can be a fusion output grows from ~75% of the roster to ~90% — with no
compiler error and no test failure, because both expressions are valid at both widths.

**These two are the single most dangerous change in the migration**, precisely because neither is a break. **A widened enum changes the
meaning of every comparison and every cast that mentions one of its members**, and the compiler is
silent about all of them.

They are found by grepping for two shapes — casts to or from `DemonRarity`, and comparisons against a
named member with `<`, `>`, `<=`, `>=` — never by the build. Each is then either rewritten against the
ten-rung spacing or replaced with a named helper (`OneRungBelow`, `RungsBelow(n)`, `AtLeast(rung)`)
whose behaviour a test pins. `.OrderBy(s => s.BaseRarity)` at `:51` is *safe* — ordering by
ordinal still means "weakest first" at any width — and is listed here so a reviewer does not
"fix" it.

### 4. Persisted ids — a rename orphans owned items

`DemonMaterialCatalog.cs:19` composes `shard.{rarity.ToId()}`, and those ids are **stored in player
inventories**. `shard.common` → `shard.chaff` is not a rename; it is an orphaning.

Worse, `ExpeditionResolver.cs:168-169` holds them as **string literals**:

```csharp
const string ShardCommon = "shard.common";
const string ShardRare   = "shard.rare";
```

These do not mention `DemonRarity` and are invisible to every grep for it. They would survive the
migration untouched, pointing at materials that no longer exist.

**The migration owns a data step**, not just a code change:

1. Ten shard materials exist after the migration, one per rung.
2. The four legacy ids map forward by `ssot-rarity.md` §4.3's own band map, choosing the band's
   **lowest** rung so no player gains value: `common → chaff`, `rare → cultivated`,
   `epic → heirloom`, `legendary → sunwoven`.
3. A DAL migration rewrites owned stacks, merging where a player holds both.
4. The four legacy ids remain **resolvable but unissuable** for one release, so a stale client or a
   saved reference does not hard-fail.

**No player ends the migration with fewer materials than they started with.** That is the acceptance
condition, and it is a test over a fixture inventory, not an intention.

### 5. Tuning, not code

Every rarity-keyed dictionary grows from four entries to ten **in `data/tuning/`**: summon rates, star
caps, recipe costs, upkeep, ritual price, discovery delta. None of these numbers belongs in code, and
several currently sit in tuning already — the migration widens the files, it does not move the numbers
into the enum.

Two of them need real thought rather than interpolation:

| Table | Why interpolating is wrong |
|---|---|
| summon rates (74/20/5/1) | ten rates must still sum to 1000‰. A naive spread makes the top rung a rounding error, and Q15's pity guards at 70 and 90 assume a reachable top |
| `StarCap` | star caps interact with promotion; a ten-rung ladder with the old caps makes mid rungs strictly worse than adjacent ones — `ssot-rarity.md` §8.6's named failure mode |

Starting values are proposed in the tuning files with a comment saying they are starting values, in the
same spirit as `ssot-power-scale.md` §5.3's own weights: *"None of these is a considered balance
decision."*

### 6. Pity — Q15

Two guards, at 70 and 90. These are **rung ordinals from `ssot-rarity.md` §3.3** (`sunwoven` and
`almanac`), which is why the two ladders being ordinal-spaced by ten matters: the guard is expressed in
the ladder's own units and needs no translation. The existing `EpicHardPity` / `LegendarySoftStart` /
`LegendaryHardPity` fields in `SummoningTuning.cs:8` are renamed to name their rungs, so a reader is
never guessing which rarity "epic" means after the migration.

### 7. Order of operations

1. Amend `ssot-rarity.md` §4.1 and §4.3 — the decision document leads.
2. Widen the enum, with `ToId()` producing the ladder's own ids.
3. Fix every ordinal-arithmetic and relational-comparison site behind a named helper.
4. Widen every tuning table.
5. Add the shard materials and the forward map.
6. Write and run the DAL migration.
7. Delete the legacy string literals.

**Step 1 is not paperwork.** `decisions.md` requires architecture changes that lock behaviour to be
recorded first, and this locks the demon economy's units.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter Rarity
dotnet test tests/FusionRpg.Data.Tests --filter Migration
.\scripts\guard-dal.ps1
python scripts/audit-magic-numbers.py --targets M1
```

## Project structure

```text
src/FusionRpg.Core/Demons/DemonRarity.cs              ten values, ToId() -> ladder ids
src/FusionRpg.Core/Demons/DemonRarityLadder.cs        OneRungBelow / RungsBelow, the named helpers
src/FusionRpg.Data/Sqlite/Migrations/ShardRungs.cs    the data step
data/tuning/summoning.*.json, fusion.*.json, contracts.*.json, soul-earn.*.json
docs/architecture/item/ssot-rarity.md                 amended sections 4.1, 4.3
```

## Code style

```csharp
// Ordinal arithmetic on a rarity is a rung-count, and the ladder now has ten rungs, not
// four. DemonRecipeCatalog used a bare (int)r - 1 and meant "a quarter of the ladder";
// after the widening the same expression silently means a tenth. Name the intent.
public static DemonRarity RungsBelow(DemonRarity r, int rungs)
```

## Testing strategy

| Test | Asserts |
|---|---|
| `no_bare_cast_between_int_and_DemonRarity_outside_the_ladder_helper` | a guard test; the §3 landmine cannot come back |
| `no_relational_comparison_against_a_named_DemonRarity_member` | the second half of §3 — `>= DemonRarity.Rare` cannot return |
| `fusion_output_set_is_pinned_by_rung_not_by_proportion` | the ~75% -> ~90% silent widening |
| `every_rarity_keyed_tuning_table_has_ten_entries` | over all six tables |
| `summon_rates_sum_to_1000_permille` | and the top rung is reachable |
| `no_rung_is_strictly_worse_than_the_one_below` | §8.6's failure mode, over star cap × slots × recipe cost |
| `legacy_shard_id_resolves_after_migration` | the unissuable-but-resolvable window |
| `migration_never_reduces_a_player_material_count` | over a fixture inventory holding all four |
| `merging_stacks_sums_rather_than_overwrites` | the both-held case |
| `expedition_shard_constants_reference_live_ids` | the invisible-literal regression |
| `pity_guards_name_their_rungs` | Q15 |

## Boundaries

**Always:** amend `ssot-rarity.md` first; map legacy ids to the band's lowest rung; keep every rarity
number in tuning; name ordinal arithmetic.

**Ask first:** the starting summon-rate spread and star caps — these are balance decisions, and this
spec proposes rather than sets them.

**Never:** rename a persisted material id without a migration; interpolate summon rates and call it
done; leave a bare `(int)` cast on a rarity; ship code that contradicts an un-amended decision doc.

## Success criteria

- [ ] `ssot-rarity.md` §4.1 and §4.3 record the reversal, with the date and the reason.
- [ ] A guard test forbids bare int↔`DemonRarity` casts outside the ladder helper.
- [ ] All six rarity-keyed tuning tables have ten entries and summon rates sum to 1000‰.
- [ ] No fixture player loses a material across the migration.
- [ ] `ExpeditionResolver`'s literals reference ids that exist.
- [ ] No rung is strictly worse than the rung below it.
