# Spec: Creature drop tables (`creature-drop-tables`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `creature-drop-tables`
**Owning program:** `drop-tables`
**Depends on:** `species-magnitude-synth`, and at least one of `wave-species-roll` / `wild-species-spawn` / `delve-species-wiring`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) § The shape 2 — edges E1, E2, E3a

---

## Objective

**Make killing a creature yield material that reflects what it was.**

This closes the middle of the spine: the player can now reach a species, and this is where the species
starts producing something. Three edges, in increasing order of honesty about cost.

| Edge | What it is |
|---|---|
| **E2** | A drawn `Material` entry actually credits the material shelf |
| **E1** | A creature kill is a loot **source** with its own tables |
| **E3a** | The shard a creature yields keys on **its own rung**, not a hardcoded pair |

⚠ **E3b — the three-layer general/species-unique material model — is NOT in this module.** It is
`species-materials`, and it is the vocabulary-widening ask-first change.

---

## What exists today — verified against code

### Built

- **`DropEntryKind` is a closed nine-member enum** — `Equipment, Material, Currency, Insert, Charm,
  Consumable, Unique, Table, Nothing` (`Items/Drops/DropTableModel.cs:16-27`). **`Material` is already
  drawable**; E2 is not about making it drawable.
- **`LootSourceRow(SourceKind, SourceId, TableId, ContentLevel)`** is already the generic carrier —
  `source_kind` is a `TEXT` column, not an enum, so adding a ninth kind is **content and vocabulary,
  not a schema migration**.
- ~~`decisions.md:133` already puts `KillerActorKey` / `killerPtr` on the `die` occurrence, so *"who
  killed this"* is already on the wire.~~ ⛔ **STRUCK — overstated, and the ideal had already said so.**
  `tier-system-ideal.md:148-150`: *"**`decisions.md:131` was overstated.** `KillerActorKey` is scoped
  to *'web/standalone battles where the server owns HP and resolution'*; **on the lawn it carries
  nothing** — which contradicts E1's 'what exists'."* This spec quoted the pre-correction claim for
  its own headline edge. **Kill attribution exists for server-resolved battles only; the lawn path is
  a real gap**, and gameless-first means E1 must work without it.
- **`data/tuning/drop-rate-floor.v1.json`** ships with `minRatePerMillion: 1`, read by
  `Items/Drops/DropRateFloor.cs`. ⚠ **Consume it; do not re-introduce a floor.** An earlier draft of
  the ideal proposed one before finding it already shipped.
- ⭐ **There are TWO drop-table corpora and neither replaces the other.** `data/seed/loot/README.md`
  says so in its own section heading (*"Why this is not `data/seed/items/drop-tables/`"*):

  | Corpus | Owner | Read by |
  |---|---|---|
  | `data/seed/loot/` (5 files) | item module 11 `drop-volume` | `LootCorpusReader` (`Items/Drops/LootCorpus.cs`), judged by `DropTableValidator`, drawn by `LootPipeline` — **the runtime corpus** |
  | `data/seed/items/drop-tables/` (4 files) | item-seedgen module 10 `drop-tables-gen` | `droptablegen` **writes here** (`droptablegen/tuning.py:29 DROP_TABLES_DIR`), `_meta.model` present |

  ⚠ **Two corrections at once.** `tier-system-ideal.md:216-217` claims *"`data/seed/loot/**` does not
  exist"* — **it does.** And an earlier draft of this spec called `data/seed/loot/**` *"droptablegen
  output"* — **it is not**; droptablegen writes the other one. **E1's runtime tables go in
  `data/seed/loot/`; the generator's output tree is `data/seed/items/drop-tables/`.**

- `droptablegen` already loads rarity ids
  from tuning (`droptablegen/tuning.py:138-142`) — the T-1 pattern.

### Wiring gap

- **E2:** a `Material` entry can be drawn and the grant is emitted, but the shelf is not credited.
  ⚠ **Sizing corrected.** An earlier draft called this *"one arm"*; `tier-system-ideal.md:172-176` had
  already struck that: *"⛔ **E2 is not 'one arm'.** … Plus a player-id type mismatch
  (`PersistLootUnlocked` keys on `string`, the credit helpers on `long`). Honest size: **2–3 Data files
  + tests**, not one call."* The type mismatch is the part that makes it more than a wiring edit.

### ⛔ Real gap — and E3a is NOT "one read"

The ideal's § The shape 2 describes E3a as *"one read"* — replacing hardcoded shard ids at
`ExpeditionResolver.cs:172-173`. **That is wrong, and the ideal's own audit already struck it.** The
verified state:

`ExpeditionResolver.cs:94-98`, inside the battle-plan loop:

```csharp
// Shards drop at plan time, win or lose — the resolver never sees battle outcomes
// (they resolve later at collect), and win-gating here would break the manifest's determinism.
materials.TryGetValue(isBoss ? ShardRare : ShardCommon, out var have);
materials[isBoss ? ShardRare : ShardCommon] = have + 1;
```

Three facts make this more than a read:

1. **The shard is chosen by `isBoss`, not by species** — a two-value ternary over two consts.
2. **It mints at *plan* time**, and the comment records why: *"the resolver never sees battle
   outcomes… win-gating here would break the manifest's determinism."*
3. **There is no species in scope** at that line. `setup.Wave` holds the enemy list; the mint is
   per-tick, not per-enemy.

**So E3a needs the species threaded to the mint point, and it must preserve plan-time determinism.**
That is a real change to what the manifest is computed from — not a substitution.

⭐ **This is the honest sequencing consequence:** E2 and E1 are small. **E3a is the one that needs
design**, and it should be specced after the selection modules land, when there is a real species at
that point to thread.

---

## Design

### 1. E2 — credit the shelf

One arm in `LootMintAt.Mint` for `DropEntryKind.Material`, one credit call in
`PersistLootUnlocked`. The grant is already emitted; this makes it land.

### 2. E1 — a ninth `source_kind`, and the paired arm nobody mentioned

`source_kind` is `TEXT`, so the change is: a new id in the closed source-kind vocabulary, its
authored tables, and kill attribution wired to resolve a species.

⛔ **And a paired change the earlier draft missed.** `Items/Drops/DropTableValidator.cs:52-59` lists
the eight shipped kinds (`web-wave`, `expedition-tier`, `world-sector`, `pvz-run`, `dungeon-room`,
`dungeon-clear`, `dungeon-quest`, `siege-assault`) with its own warning:

> *"Each also gains a `LootCorrelation.Derive` arm (`LootPipeline.cs`) — **neither list is complete
> without the other**."*

**A ninth kind without its `Derive` arm is a self-documented incompleteness.** Both, in one change.

⚠ **And kill attribution is only half-built** (see the struck bullet above): it exists for
server-resolved battles, not the lawn. E1 must therefore key on a source the **expedition / delve /
wild** paths can supply, not on a lawn kill.

**Table shape to start from:** *1 generic + 3 commons + 1 rare gate*, with weights and gate rates in
per-million, matching the unit `drop-rate-floor.v1.json` already uses.

### 3. E3a — the species' own rung selects the shard

Replace `isBoss ? ShardRare : ShardCommon` with a lookup on the **killed species' rarity rung**, which
already has a minted id (`shard.{rarity}`). This is MH re-tiering **with ids that already exist** —
no vocabulary change.

⚠ **Determinism is the constraint, not a nicety.** The manifest is computed at plan time and its
determinism is load-bearing. Two shapes are possible:

| Shape | Note |
|---|---|
| **Plan-time, from the planned wave** | The wave's species are known at plan time, so the rung is derivable without seeing outcomes. **Preserves the existing contract exactly.** ✅ Recommended |
| Collect-time, from actual kills | More accurate, but moves the mint out of the manifest and changes what determinism means |

**Recommendation: plan-time, from the planned wave's species.** It keeps the comment at `:94-96`
true.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items/Drops`, `FusionRpg.Core/Expeditions`, `FusionRpg.Data`),
Python 3 (`droptablegen`), xUnit + pytest.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Drop"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Loot"
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k droptable
.\scripts\guard-dal.ps1
python scripts/audit-overflow.py
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Items/Drops/` | `LootMintAt.Mint`, `DropRateFloor` |
| `src/FusionRpg.Data/Sqlite/` | `PersistLootUnlocked`, the `source_kind` row |
| `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:94-98` | E3a |
| `tools/seedsmith/.../droptablegen/` | The table generator |
| `data/seed/loot/**` | The **runtime** corpus (item module 11 `drop-volume`) — where E1's tables land |
| `data/seed/items/drop-tables/**` | `droptablegen` **output** — regenerated, never hand-edited |
| `src/FusionRpg.Core/Items/Drops/LootPipeline.cs` | ⛔ The paired `LootCorrelation.Derive` arm — see below |

## Code style

```csharp
// E3a: the shard keys on the KILLED SPECIES' OWN RUNG, from the planned wave — not on isBoss.
// Plan-time by necessity: the manifest's determinism depends on the resolver never seeing battle
// outcomes (see the comment above), so the rung is derived from the PLANNED enemies, which are
// known here, rather than from actual kills, which are not.
var shardId = MaterialIds.ShardFor(PlannedRungFor(setup.Wave));
```

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| Per-species-kind drop weights (`weightPerMillion`) | E1's tables; the *1 generic + 3 commons + 1 rare gate* shape | `data/seed/loot/**` (runtime corpus, module 11) |
| Rare-gate rates (`gateRatePerMillion`) | How often the gated entry fires | Same |
| Species-tier → material grade/rung map | Which grade a creature of rung *n* yields (MH's `Scale` → `Scale+` → `Shard`) | New `data/tuning/creature-yield.v1.json` — ⚠ **shared with `species-materials`; one file, never two** |
| ~~A drop-rate floor~~ | ⛔ **Already ships.** `drop-rate-floor.v1.json` `minRatePerMillion: 1` | *(shipped — consume it)* |

**Structural (stays `const`, with a comment saying why):** the `DropEntryKind` enum and the
source-kind vocabulary — closed lists the code owns.

## Numeric types

- Weights and gate rates are **per-million `int`s** — bounded ratios, exempt and commented as such.
  ⚠ Note the unit is **per-million**, matching `drop-rate-floor.v1.json`, **not** per-mille like the
  socket and cost tables. Mixing the two is the most likely bug in this module.
- Yield **quantities** are `long`. They are magnitudes that scale with content.
- **Widen before multiplying; divide last, exactly once; overflow throws.** Never `float`.

## ActorHub gate

**N/A and checked.** A drop table produces materials, not actor combat / derived / AppliedCombat
magnitudes. Nothing composes, contributes, or folds. `guard-actor-hub.ps1` stays green.

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit | E2 — a drawn `Material` entry **credits the shelf**, end to end |
| Unit | E1 — a creature kill resolves to its source row and draws from its table |
| Unit | E3a — the shard id follows the **species' rung**, across several rungs |
| Unit | ⭐ **Plan-time determinism is preserved** — the same expedition seed yields a byte-identical manifest, before and after E3a |
| Unit | The shipped drop-rate floor is **consumed**, not re-implemented — a sub-floor weight still draws |
| Unit | Per-million and per-mille units are not mixed — a table asserting a known rate against a hand-computed expectation |
| Contract | Every `source_kind` is in the closed vocabulary; every table id resolves |
| pytest | `droptablegen` emits tables whose weights sum as declared and whose material ids are in the closed 27 |
| Report | Per-species yield distribution — **a reading, printed, never asserted** |

⛔ **No test asserts a drop rate as a balance outcome or a table count.** Assert the **draw contract**
and **closure**; print the distribution.

## Boundaries

**Always**
- **Fix the generator and regenerate.** `data/seed/loot/**` is `droptablegen` output.
- Consume the shipped drop-rate floor.
- Preserve plan-time manifest determinism.
- Keep per-million and per-mille units straight, and say which in every comment.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **The ninth `source_kind` id.** It is a closed vocabulary.
- Moving the shard mint out of plan time — that changes what the manifest's determinism means.
- Creating `creature-yield.v1.json` if `species-materials` has not — **one file, never two.**

**Never**
- ⛔ Widen the closed 27-id material vocabulary here. That is `species-materials`', and it is
  ask-first against `ssot-materials-crafting.md` §3.1.
- ⛔ Introduce source-tagged material ids. §3.4 bans them, and MH's own `itemData` sprawl (IDs
  0–2315) is the documented failure mode.
- Re-introduce a drop-rate floor.
- Hand-edit a generated loot table.
- Let one species' yield **strictly dominate** — see below.
- Assert a population count.

## ⛔ The guard this module makes load-bearing

**No single species' rewards may strictly dominate.**

`wild-species-spawn` records this obligation; **this is the module that creates the risk.** A map full
of creatures whose drops are strictly ordered will be farmed at exactly one sector — the documented
Wilds/Arkveld outcome, where monoculture followed **reward dominance, not the UI**.

The discipline is `roster-metrics` (creature-seed module 14) pointed at yields. It is a **report,
never a test assertion**. The design lever is **overlap**: a higher-rung creature should yield *better
odds*, never a strictly superior set — the same overlap principle `rarityGrant`'s windows already use
deliberately (*"adjacent windows OVERLAP by design… so socket count never becomes a strict ladder"*).

## Success criteria

1. A drawn `Material` entry credits the shelf, proven end to end.
2. A creature kill resolves to a loot source and draws from its own table.
3. The shard a creature yields follows **its rung**, not `isBoss`.
4. ⭐ Expedition manifests remain byte-identical for a given seed — plan-time determinism intact.
5. The shipped drop-rate floor is consumed; no second floor exists.
6. No new material id; the 27-id vocabulary is untouched.
7. A per-species yield **distribution report exists and is printed**.
   ⚠ **Split from what this module cannot close.** *"No species' yield strictly dominates another's"*
   needs `roster-metrics` (creature-seed module 14) pointed at yields — which is **not one of the
   nineteen**, and which this initiative's own map defers with the hunt interaction. **The dominance
   guard is a named obligation on the world-stage hunt module, not a criterion this module can meet.**
8. `guard-dal.ps1`, `guard-actor-hub.ps1` green; Core, Data and pytest suites green.

## Open questions

1. ⭐ **Plan-time or collect-time for E3a?** **Recommendation: plan-time, from the planned wave.** It
   preserves the manifest contract exactly, and the planned species are known where the mint happens.
2. **Does a creature drop equipment, or only materials, in v1?** **Recommendation: materials only.**
   Equipment drops open rarity, affix and set-roll questions that belong to the item program, and the
   spine only needs materials to close.
3. **One table per species, per rung, or per species-kind?** **Recommendation: per rung, with a
   per-species override slot left empty.** 904 authored tables is the MH sprawl failure; a rung table
   plus a thin override is the bounded shape, and it is the same 1–2-per-species discipline
   `species-materials` uses.
