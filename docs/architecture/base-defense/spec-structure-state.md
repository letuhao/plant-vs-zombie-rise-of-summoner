# Spec: `structure-state`

**Module 7 of 21 · level 3 · depends on `siege-seam` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.
**⛔ This is the golden-locked landing.** Levels 0–2 move no hash. This module writes to `WorldState`
and therefore to `WorldCanonical`. It is one batched landing, sharing a triage pass with anyone else
moving `RulesetVersion`.

---

## Objective

**Give a structure hit points and a place to stand — without moving a single existing world hash.**

Buildings are a new actor kind (owner decision 4): no level, no equipment, but they have traits and
actions, and they can be destroyed. That last part needs persisted HP: a wall breached on turn 12 is
still breached on turn 13, and a repair costs resources.

The repo has already solved this exact problem once, and recorded the failure that taught it how.

**Success looks like:** structures carry HP, damage survives the turn, repair is proportional, and
every world golden is byte-identical because a structure at full health writes no row at all.

---

## The precedent, quoted, because it is the whole design

`WorldCanonical.cs:92-99`:

```csharp
// produces exactly the bytes it always did. Appending this to the existing "faction" row
// instead would have moved every prior hash for a value that did not actually change
// (found live: it moved WorldWaveOneAcceptanceTests' own golden even at the neutral default).
foreach (var f in w.Factions)
{
    if (f.ScopeModifierMilli != 1000)
        Row(sb, "faction-scope", f.FactionId, f.ScopeModifierMilli);
}
```

**A conditional row.** Not an added column. The distinction is the module: appending a field to the
existing `slot` row moves every hash in the repo for a value that is at its default everywhere. A
separate row emitted **only when the value is non-default** produces zero bytes on every existing
world, and therefore zero golden movement.

This module copies that shape exactly, including the reason in the comment.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `WorldSlot` — `SlotIndex`, `SlotTypeId`, `Element`, `State`, `OwnerFactionId`, `GuardWaveId`,
  `GuardState`, **`StructureId`**, **`ConstructionTurnsRemaining`**.
- `StructureCatalog` — **four** hand-authored rows (`loam-source-placeholder`, `well`, `waystation`,
  `granary`), all `LoamSource` or `Storage` kind, with `CostMilli`, `YieldMultiplierMilli`,
  `BuildTurns`, `CapacityBonus`. Validated at load: kebab ids, no duplicates, no negative cost.
- `WorldCanonical.Row` + the `faction-scope` conditional-row precedent.
- `WorldSector.DepletionMilli` — **sector-scoped**, and already claimed by the loam program.

**Real gaps.**

- **No structure HP.** `StructureDef` has no `MaxHp`. A structure cannot be damaged, so it cannot be
  besieged.
- **No cell.** A structure sits on a *slot*; `district-layout` maps slots to cells, but nothing
  records that a structure occupies its cell for movement.
- **Slot-level depletion** (audit **F10**). `DepletionMilli` is on `WorldSector` and belongs to the
  loam program. The owner's decision — *"Stop mining and product, because the resource can exhausted,
  need ui notification system"* — is **per mine**, not per sector. A second consumer of the sector
  field would fight the loam program for it.

---

## The contract

### 1. `StructureDef` gains combat identity

```csharp
/// <summary>
/// **The MATERIAL TIER ordinal**, not a hit-point count. Decision 32.
///
/// <para><b>An ordinal, because a model picks it.</b> "we will use llm to generate variant like stone
/// wall, iron wall that iron wall have more defense than stone wall." Seedsmith Law 2 exactly: the
/// model writes IDENTITY (stone, iron, …) and deterministic code writes MAGNITUDE. A model has no
/// calibrated sense of scale, so a number it picks is a plausible-looking guess that survives review
/// because nothing looks wrong with it.</para>
///
/// <para>Zero means <b>indestructible by damage</b> — a real content state, not an oversight. The four
/// shipped loam rows predate any notion of a siege and stay at 0, unaffected.</para>
/// </summary>
public int MaterialTier { get; init; }

/// <summary>
/// Effective hit points. Decision 32: `P(Θ_development) × tierMultiplier`, where Θ is the SECTOR'S
/// DevelopmentLevel — a structure has no level of its own, and a developed city has stronger walls.
///
/// <para><b>long, from `P(Θ)`</b> — §6: "① Magnitudes — HP, damage — long, derived from P(Θ)". This
/// is the one ladder; there is no private f(level) here. Contrast §6's class ②: range in cells,
/// footprint and build turns are FLAT authored tunables, never P(Θ), because a build turn-count
/// growing quadratically means a wall takes hundreds of turns at depth.</para>
///
/// <para>Widen before multiplying; divide by 1000 last, exactly once; checked.</para>
/// </summary>
public static long MaxHpOf(StructureDef def, int developmentLevel) =>
    def.MaterialTier <= 0
        ? 0
        : checked(PowerScale.P(developmentLevel) * StructurePolicy.TierMultiplierMilli(def.MaterialTier) / 1000);

/// <summary>
/// Whether this structure blocks movement through its cell. A wall does; a granary you can walk
/// around does not. Default false — every shipped row keeps today's behaviour.
/// </summary>
public bool BlocksMovement { get; init; }

/// <summary>
/// Whether this structure blocks line of fire through its cell. Decision 25: an unoccupied building
/// "occupies its cell, blocks movement AND FIRE, and has HP. It simply does not act."
///
/// <para>Separate from <see cref="BlocksMovement"/> on purpose — a moat blocks movement and not fire;
/// a smoke-filled ruin could block fire and not movement. `siege-obstacles` needs both independently,
/// and this is the field that finally gives `RequiresLineOfSight` a reader.</para>
/// </summary>
public bool BlocksLineOfFire { get; init; }
```

**Validation extends** in `StructureCatalog.Validate`, matching its existing stance that *"a bad
structure row is a startup error, never a runtime surprise"*: `MaterialTier < 0` throws, and a tier
with no multiplier row in tuning throws.

> ### Decision 33 — `structure-seed` needs a deterministic planner, and this is why
>
> *"pipeline generator need a deterministic planner (not LLM) to prepare what it should generate
> first."*
>
> The tier ladder (**stone < iron < …**) must be **planned**, not emergent. If the model is simply
> asked to name materials, nothing guarantees the set is ordered, covered, or free of two names for
> one tier — and the ordering is exactly what carries the mechanical difference decision 32 relies on.
>
> So a **model-free planner stage runs first** and fixes: which kinds exist, which tiers exist, how
> many variants per (kind × tier), and which slots each may sit on. The model then writes identity
> into slots the planner already opened. That is the seedsmith rule *"order the build so the model-free
> modules come first"* promoted from advice to a required stage — a parse, a table and a plan produce
> real value with **zero tokens spent**, and they make the expensive stage's inputs reviewable.
>
> **This module consumes the tier ordinal and nothing else.** The planner belongs to `structure-seed`.

### 2. `WorldSlot` gains two nullable fields

```csharp
/// <summary>
/// Current structure HP. **Null means undamaged** — not zero, and not "no structure". Null is the
/// default on every slot in every existing world, which is what keeps the canonical row below silent
/// and every golden unmoved.
/// </summary>
public long? StructureHp { get; init; }

/// <summary>
/// Slot-level resource depletion, per-mille, 0 = untouched (audit F10).
/// <b>Deliberately NOT WorldSector.DepletionMilli</b>: that field is sector-scoped and already
/// claimed by the loam program, and the owner's decision ("stop mining and product, because the
/// resource can exhausted") is per-mine. Two consumers of one field would make each one's changes
/// look like the other's bug.
///
/// <para>Bounded ratio, 0..1000 — exempt from AGENTS.md's no-hard-ceilings rule, which names
/// "bounded ratios (per-mille, 0..1)" explicitly.</para>
/// </summary>
public int SlotDepletionMilli { get; init; }
```

**Why `StructureHp` is nullable and `SlotDepletionMilli` is not:** null/`0` are each the default that
makes the canonical row silent, and for depletion `0` already *is* "untouched" unambiguously. HP has
no such value — `0` means destroyed, which is very much not the default.

### 3. Two conditional canonical rows

```csharp
// The faction-scope precedent (line 92 above), applied twice more. A separate row emitted only when
// the value is off its default, NOT a column appended to the existing slot row — appending would
// move every prior hash for a value that did not change, which is the exact failure that comment
// records finding live.
foreach (var s in w.Sectors)
{
    foreach (var sl in s.Slots)
    {
        if (sl.StructureHp is { } hp)
            Row(sb, "slot-hp", s.SectorId, sl.SlotIndex, hp);
        if (sl.SlotDepletionMilli != 0)
            Row(sb, "slot-depletion", s.SectorId, sl.SlotIndex, sl.SlotDepletionMilli);
    }
}
```

**Row order is part of the hash.** Sectors and slots are already deterministically ordered
(`WorldSector.Slots` is documented as *"ordered by `SlotIndex`, contiguous from zero"*), so no
explicit sort is needed — but assert it, because the invariant is a comment and `DESIGN-GATE.md`
rule 1 is that a comment is not evidence.

### 4. The repair resolver

Repair is proportional to what is missing — a scratch costs a scratch's worth.

```csharp
/// <summary>
/// What it costs to repair this slot's structure to full.
///
/// <para><b>Divide by 1000 exactly once, last</b> (CLAUDE.md rule 4). The per-mille intermediate is
/// 1000× closer to the ceiling than the answer is, so any earlier division is both a precision loss
/// and a wasted headroom. <b>Widen before multiplying</b> (rule 3): the cast binds to the result,
/// so `(long)(cost * ratio)` has already overflowed by the time it is cast.</para>
/// </summary>
public static long RepairCost(StructureDef def, long currentHp)
{
    if (def.MaxHp <= 0) return 0;                       // indestructible: nothing to repair
    var missing = def.MaxHp - Math.Max(0, currentHp);
    if (missing <= 0) return 0;

    // long × long × long, one divide, at the end. Overflow throws.
    return checked(def.CostMilli * missing * StructurePolicy.RepairCostRatioMilli
                   / def.MaxHp / 1000);
}
```

**`checked` is not decoration.** `CostMilli` is already a `long` and already a magnitude
`contentScale` reaches; multiplying it by `missing` and again by a per-mille ratio is exactly the
"three magnitudes multiplied" shape `CLAUDE.md`'s rule 5 says must throw rather than wrap.

**Two divides, and the order is deliberate.** `/ def.MaxHp` converts "missing HP" to a fraction of the
building; `/ 1000` converts the per-mille ratio. Both are last, after every multiply. Combining them
into `/ (def.MaxHp * 1000)` would be arithmetically equal and is **forbidden** — that product can
itself overflow, which is the failure being avoided.

### 5. Destruction leaves rubble, using vocabulary that already exists

At `StructureHp <= 0` the slot becomes:

```csharp
sl with { State = SlotState.Ruined, StructureId = null, StructureHp = null, ConstructionTurnsRemaining = null }
```

**`SlotState.Ruined` already exists and nothing reads it.** This module is its first reader — a
declared-and-unread value becoming live, which is a wiring gap closed, not a new enum. `district-layout`
already specs `Ruined` → `Rough` terrain, so a destroyed building becomes rubble you can cross but
which slows you. That falls out for free.

### 6. Capacity halts production — decision 22, and it is not depletion

⛔ **The first draft specced depletion only. Decision 22 is a different rule**, and the audit found it
missing:

> *"A construction stock **at capacity** halts production; nothing is wasted — because a **deposit can
> be exhausted**, so discarding extracted material is a double loss. This requires a **player
> notification** telling them to build storage."*

Two distinct halts, and conflating them loses the one the owner asked for:

| Halt | Trigger | Reversible | Message |
|---|---|---|---|
| **Capacity** | the sector's stock is at its cap | **Yes** — build storage | *"production halted: no room"* |
| **Depletion** | the slot's deposit is spent | **No** | *"this deposit is exhausted"* |

```csharp
/// <summary>
/// Decision 22: at capacity, production STOPS rather than overflowing. Nothing is wasted — because
/// the deposit is finite, so material extracted and discarded is lost twice. A halt preserves it in
/// the ground until there is somewhere to put it.
/// </summary>
public static bool IsHaltedByCapacity(WorldSector sector, StructureKind kind);
```

#### F12 — and this is why capacity must grow with slots

Audit **F12**, which the first draft missed entirely:

> *"Decision 21 buys zero economy. 4 rootbeds + wells = 400/turn against a 300 cap; at equilibrium the
> marginal producer's entire output is destroyed as overflow."* Verdict: **"The design changes.**
> Capacity must grow alongside slots, or decision 21 gains *slots*, not capacity."

With decision 22's halt, the overflow is no longer *destroyed* — but the producer still yields nothing,
so decision 21 still buys nothing. **Both halves are needed:** the halt stops the waste, and capacity
growing with `DevelopmentLevel` is what makes a new slot actually produce.

`storage.capacityPerDevelopmentLevel`, a tunable, and it must be **large enough that a development
level's new slots can fill it** — an invariant worth a test rather than a hope.

### 7. Depletion stops production, and says so

Owner decision: *"Stop mining and product, because the resource can exhausted, need ui notification
system."*

```csharp
/// <summary>Whether this slot still yields. At full depletion it does not, and the turn report says
/// so once — the notification the owner's decision asks for, on the wire the FE already reads.</summary>
public static bool IsExhausted(WorldSlot slot) => slot.SlotDepletionMilli >= 1000;
```

On the transition into exhaustion, exactly once:

```csharp
report.Add(phase, TurnReportKinds.Event, sector.SectorId, "slot.exhausted:" + slot.SlotIndex,
    sectorId: sector.SectorId, audience: sector.OwnerFactionId);
```

**Fires on the transition, never on the state**, following the precedent `LegionSupply` set for
`supply.restored` — its comment is explicit that a per-turn repeat is *"the signal that survives being
checked every turn without repeating"* only because the condition stops being true. Here the condition
`>= 1000` stays true forever, so the transition must be detected against the pre-update value rather
than re-asserted each turn.

**The loam program's `LoamProduction` must consult `IsExhausted`.** That is a one-line change in
another program's file. **Read it first, and if it turns out to compute yield somewhere else, follow
the code rather than this spec.**

---

## Tunables

`data/tuning/siege.v1.json`, `structure.*` block.

| Key | Unit | Default | Why tunable |
|---|---|---|---|
| `structure.repairCostRatioMilli` | per-mille of build cost | `600` | Balance — repairing cheaper than rebuilding is the whole reason to repair |
| `structure.tierMultiplierMilli.<tier>` | per-mille | `1000, 1800, 3000, …` | Balance — decision 32's tier ladder. **The ordinal is authored by the model; every number here is not** |
| `storage.capacityPerDevelopmentLevel` | units | **unset** | **F12.** Must be large enough that a level's new slots can fill it — asserted, not hoped |
| `structure.depletionPerHarvestMilli` | per-mille | `10` | Balance — 100 harvests to exhaust |

**`StructurePolicy` is a Policy file, so it carries named tunables and no bare literals** —
[tunables-ssot.md](../tunables-ssot.md) names Policy files as the balance surface directly.

## Numeric types

| Value | Type | Justification |
|---|---|---|
| `StructureDef.MaxHp` | **`long`** | magnitude, `contentScale` reaches it. `float` fails at index 232 |
| `WorldSlot.StructureHp` | **`long?`** | same, plus the null default that keeps the row silent |
| `RepairCost` | **`long`**, `checked` | product of three magnitudes |
| `SlotDepletionMilli` | `int` | bounded ratio 0..1000 — exempt, and the comment says so |

## Boundaries

**Always:** conditional rows, never appended columns · divide by 1000 last, exactly once · `checked`
on every magnitude product · `long` for HP.

**Ask first:** giving the four shipped structures non-zero `MaxHp` (that is content, and it belongs to
`structure-seed`) · any change to `WorldSector.DepletionMilli`, which is the loam program's.

**Never:** `float` HP · a `Math.Min` on a magnitude (`AGENTS.md`: an absolute bound is *derived and
throws, never clamps*) · reuse `WorldSector.DepletionMilli` · append to the existing `slot` canonical
row.

---

## Testing

`tests/FusionRpg.Core.Tests/World/` and `tests/FusionRpg.Data.Tests/`.

| Test | Asserts |
|---|---|
| `World_goldens_are_byte_identical_at_default` | **the gate.** Null HP + zero depletion emits nothing |
| `Canonical_gains_exactly_one_row_per_damaged_slot` | and none for undamaged ones in the same sector |
| `Slot_rows_are_emitted_in_slot_index_order` | the ordering invariant, asserted rather than trusted |
| `Repair_cost_is_zero_at_full_health` | and at `MaxHp == 0` |
| `Repair_cost_is_proportional` | half-damaged costs half of a full rebuild × ratio |
| `Repair_cost_overflows_loudly` | `MaxHp = long.MaxValue / 2`, assert `OverflowException`, **not** a wrapped negative |
| `Repair_cost_divides_by_1000_last` | compare against a `BigInteger` reference at a magnitude where an early divide loses precision |
| `Destroyed_structure_leaves_a_ruined_slot` | `SlotState.Ruined`, its first reader |
| `Exhausted_slot_stops_yielding` | through the real loam production path, not a mock |
| `Exhaustion_reports_once_not_every_turn` | the transition-vs-state distinction |
| `Structure_hp_round_trips_through_sqlite` | `FusionRpg.Data.Tests` — `long`, not `int`, all the way to the column |
| `Negative_material_tier_throws_at_catalog_load` | startup error, not runtime surprise |
| `A_tier_with_no_multiplier_row_throws` | decision 32 — the ordinal is useless without its number |
| `Iron_wall_has_more_hp_than_stone_wall_at_the_same_development` | **decision 32**, as the owner stated it |
| `Hp_scales_with_sector_development_level` | the `P(Θ)` half |
| `Tier_zero_is_indestructible_and_the_four_shipped_rows_are_tier_zero` | goldens unmoved |
| `Capacity_halt_stops_production_without_waste` | **decision 22** — stock unchanged, deposit unspent |
| `Capacity_halt_is_reversible_by_building_storage` | unlike depletion |
| `Capacity_halt_and_depletion_report_differently` | the player must be able to tell them apart |
| `Capacity_grows_enough_that_a_new_slot_produces` | **F12**, as an invariant rather than a hope |
| `Blocks_line_of_fire_is_independent_of_blocks_movement` | decision 25 — a moat blocks one, not the other |

**Overflow audit before finishing** (`CLAUDE.md` requires it for any work touching a magnitude):

```powershell
python scripts/audit-overflow.py
python scripts/audit-overflow.py --targets A3
```

## Success criteria

1. Every world golden byte-identical at default — **unblessed**.
2. Two conditional rows, emitted only off-default, both order-asserted.
3. `RepairCost` throws on overflow, divides by 1000 exactly once and last, proven against a
   `BigInteger` reference.
4. `SlotState.Ruined` has a reader.
5. `WorldSector.DepletionMilli` is untouched — `git diff` shows no change to it.
6. `audit-overflow.py` reports zero criticals.
7. `StructureHp` round-trips as `long` through persistence.

## Open questions

**None.** The one open question — *which `Θ` does a structure with no level read* — was answered by the
owner on 2026-09-04 as **decision 32**: the **sector's `DevelopmentLevel`**, multiplied by an authored
**material tier** ordinal.

A developed city has stronger walls, which is local, already hashed, and gives decision 21 (*"grow
slots"*) a second payoff beyond slot count. And the tier ordinal is what carries *"iron wall has more
defense than stone wall"* without a model ever picking a number.

**Decision 33 is the dependency this creates**, and it belongs to `structure-seed`: a **deterministic
planner** must fix the tier ladder before any model call, or the ordering the mechanics rest on is
whatever the model happened to name.
