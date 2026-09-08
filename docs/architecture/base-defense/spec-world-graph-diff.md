# Spec: `world-graph-diff`

**Module 21 of 29 · level 0 · no dependencies · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by the completeness audit.** §8 prerequisite 1, which the
original 17 specs dropped.

**⚠️ Owned here, buildable elsewhere.** Owner decision: *spec it here so it is costed and owned; the
world program may absorb the build.* The risk this module exists to avoid is it being **nobody's** —
which is what happens when a prerequisite is only mentioned.

---

## Objective

**Stop rewriting the entire world graph on every turn commit, before decision 21 multiplies the rows.**

The code names its own revisit trigger by hand, which is why this is a prerequisite rather than an
optimisation someone might get to. `RpgStore.World.cs`:

> *"Writes the whole graph (factions, sectors, slots, lanes, entities, members) for a world. Used by
> creation and by every turn commit: the turn engine hands back a complete world, so **the store
> replaces the graph rather than diffing it. At six sectors that is far cheaper than the bug surface a
> partial-update path would carry; revisit if a world ever reaches hundreds.**"*

And the commit path, verified at `RpgStore.WorldTurns.cs:511-512`:

```csharp
var result = TurnEngine.Step(world, commands, header.Seed);

ClearWorldGraphUnlocked(db, tx, worldId);
WriteWorldGraphUnlocked(db, tx, result.World);
```

**Clear, then rewrite. Every turn. Every row.**

Base defense makes the trigger real from three directions at once:

1. **Decision 21** — a sector gains capacity by *growing slots*. Slot rows multiply directly.
2. **Decision 19's scale** — 18 sectors × ~20 structures ≈ **360 slot rows rewritten per turn commit**,
   and §0 flags it: *"A diffing writer stops being optional."*
3. **`structure-state`** adds two more fields per slot, so each rewritten row is wider.

§8 states the verdict without hedging: *"No longer a follow-up."*

---

## ⚠️ Measure first — C5 says the largest term may not be the one we are looking at

The audit's own build cost **C5**:

> *"Turn-commit cost **omits its largest term** — `rpg_world_faction_intel` serializes `slots_json` per
> (faction × sector), and `Insert` creates a **fresh `SqliteCommand` per row**. **Measure before
> choosing a diffing writer.** Statement reuse may recover most of it."*

**So step 1 of this module is not a diff. It is a measurement**, and it may conclude that the diff is
unnecessary.

Two candidate costs, and they have very different fixes:

| Candidate | Fix | Cost of the fix |
|---|---|---|
| **Row count** — clear-and-rewrite of ~360 slots | a diffing writer | High: a partial-update path is *"the bug surface"* the original comment declined |
| **Per-row overhead** — a fresh `SqliteCommand` per row, plus `slots_json` re-serialised per (faction × sector) | **prepared-statement reuse** | Low, local, and it moves no logic |

**If measurement says the second dominates, take the cheap fix and stop.** Shipping a diffing writer to
solve a problem that was actually statement construction would take on the exact bug surface the
original author declined, for no gain.

> **This is `DESIGN-GATE.md` evidence rule 3 applied to performance**: *"Test the constraint before you
> declare it."* The 360-row figure is arithmetic, not a measurement.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `WriteWorldGraphUnlocked` (`RpgStore.World.cs:214`) — factions, sectors, slots, lanes, entities,
  members. Straight-line `INSERT` loops.
- `ClearWorldGraphUnlocked` — the wipe half.
- The commit transaction at `RpgStore.WorldTurns.cs:509-512` — `Step`, clear, write, log, commit.
- `WorldCanonical.Write` — the hash, computed from `WorldState`, **not from the rows**. Load-bearing:
  it means how the graph is *stored* cannot move a golden.

**Real gaps.** No diff. No statement reuse. **No measurement.**

---

## The contract

### Step 1 — measure, and publish the number

A benchmark over a realistic world (18 sectors × ~20 slots, per decision 19's scale), attributing
turn-commit wall time across:

- `ClearWorldGraphUnlocked`
- `WriteWorldGraphUnlocked`, split by table
- `rpg_world_faction_intel` / `slots_json` serialisation (**C5's suspect**)
- `SqliteCommand` construction vs execution

Landed in `docs/research/perf/` beside the existing baselines, following the perf stream's own
convention.

**The decision gate:** if statement reuse and `slots_json` account for the majority, steps 2 and 3 are
**cancelled** and this module ships as a measurement plus a cheap fix. Record that outcome explicitly
rather than leaving the module open.

### Step 2 (conditional) — prepared-statement reuse

Hoist `SqliteCommand` construction out of the row loops; bind and execute per row. **No logic change,
no schema change, no diff.** The cheapest possible fix and the one C5 predicts is sufficient.

### Step 3 (conditional) — the diffing writer

Only if step 1 says row count genuinely dominates.

```csharp
/// <summary>
/// Writes only what changed between two world states. The alternative to clear-and-rewrite, and the
/// reason it was NOT the original choice is recorded in WriteWorldGraphUnlocked's own comment: a
/// partial-update path carries a bug surface a full rewrite does not — a row that should have been
/// deleted and was not is invisible until it is read back.
///
/// <para><b>So the diff is guarded by an equivalence assertion, not by review.</b></para>
/// </summary>
static void DiffWorldGraphUnlocked(SqliteConnection db, SqliteTransaction tx,
                                   WorldState previous, WorldState next);
```

**The equivalence guard is the module's whole safety argument:**

```csharp
// A diffing writer's failure mode is a row that quietly did not change. This assertion makes that
// failure loud and immediate: after a diff write, reading the graph back must produce a WorldState
// whose canonical hash equals the one a full rewrite would have produced.
//
// Debug builds and the full test suite always; production behind a tuning flag so a suspected
// divergence can be caught on a live save rather than reproduced.
Debug.Assert(WorldCanonical.Hash(ReadBack(db, tx, worldId)) == WorldCanonical.Hash(next));
```

**`WorldCanonical` hashes `WorldState`, not rows** — so the hash is an *independent* check of the
write path rather than a restatement of it. That is what makes the assertion meaningful.

**Deletion is the hard half.** A slot removed, an entity destroyed, a lane severed. The diff must emit
`DELETE` for every row present in `previous` and absent from `next`, and the equivalence assertion is
what catches a missed one — because a stale row changes the read-back hash.

### What does not change

- **No schema change.**
- **No `WorldCanonical` change**, so **zero goldens move** in any of the three steps.
- **SQL stays inside `FusionRpg.Data`** — `guard-dal.ps1` enforces it and this module is squarely
  inside that boundary.
- The transaction shape is untouched: still one `tx`, still commit-or-rollback as a unit.

---

## Tunables

| Key | Unit | Default | Why |
|---|---|---|---|
| `world.graphWriteEquivalenceCheck` | bool | `false` in release, `true` in debug/test | **Structural**, a diagnostic gate rather than balance. Comment must say so |

## Numeric types

None introduced. `LoamStock` and `IronworkStock` are already `long` and must stay `long` through the
diff path — a narrowing in a write path would be invisible until a value grew, which is the worst
possible discovery time.

## Boundaries

**Always:** measure before building · SQL only inside `FusionRpg.Data` · the equivalence assertion on
every diff write in debug and test · `long` preserved through the write path.

**Ask first:** any schema change · touching the transaction boundary.

**Never:** build the diff before step 1's measurement · a diff without the equivalence guard · move
`WorldCanonical` · skip `DELETE` handling.

---

## Testing

`tests/FusionRpg.Data.Tests/`.

| Test | Asserts |
|---|---|
| `Turn_commit_cost_is_attributed` | step 1's benchmark exists and is recorded |
| `Statement_reuse_preserves_the_read_back_hash` | step 2 changes nothing observable |
| `Diff_write_read_back_equals_full_write_read_back` | **the equivalence guard**, over 500 randomised world mutations |
| `Deleted_slot_is_deleted` | the hard half |
| `Deleted_entity_is_deleted` | |
| `Severed_lane_is_deleted` | |
| `Grown_slot_list_writes_only_the_new_rows` | decision 21's case, directly |
| `Unchanged_world_writes_nothing` | the degenerate case that proves the diff is a diff |
| `World_goldens_byte_identical` | **the gate** — no canonical change in any step |
| `Long_stocks_survive_the_diff_path` | no narrowing |
| `guard_dal_passes` | SQL stays in `FusionRpg.Data` |

## Success criteria

1. **A measurement exists and is published**, and steps 2/3 are taken or cancelled on its evidence.
2. Whatever ships preserves the read-back hash over 500 randomised mutations.
3. Deletion is handled and tested three ways.
4. Zero goldens moved.
5. `guard-dal.ps1` green.
6. If the diff is cancelled, **that outcome is recorded here** — an unanswered prerequisite is how this
   became nobody's in the first place.

## Open questions

None. The one real uncertainty — *which cost actually dominates* — is step 1's job to answer rather
than a question to put to the owner.
