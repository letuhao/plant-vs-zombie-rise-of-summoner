# Spec: `relic-item-kind`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `relic-item-kind`, row 1 of the
[loam-relics-and-wonders map](../loam-relics-and-wonders-map.md) (wave 1, no internal dependency —
builds in parallel with `wonder-structure`). Ideal:
[loam-relics-and-wonders-ideal.md](../loam-relics-and-wonders-ideal.md) §The shape (Relics), §Real
gap table row 1, Open question #2. Decisions:
[decisions.md](../decisions.md) "Loam relics and wonders SSOT (2026-09-13)".

## Objective

A relic is a **rolled, never-equipped, individually-identified item** — minted through the existing
item-generation and drop-table machinery, never a `WorldFaction`/`WorldSector` counter. This module
resolves the one open schema question the ideal doc left for spec time (does today's `unique`
`KindSpec` grow a no-`baseType` variant, or does a relic register as its own sibling
kind/`DropEntryKind` member?), wires the real, already-shipped drop-table `SourceKind`s so any of
them can carry a relic row, and proposes — as this module's own spec-time content call, not locked
architecture — the tunable home for relic drop rates.

Success looks like: a relic anchor authors cleanly through a new, narrow `relic` `KindSpec`; it
mints through the drop pipeline into a durable, player-owned `rpg_item` row with **no fabricated
equip role, frame-as-body-fact, or base type**; and every one of the seven real, wired `SourceKind`s
can carry a `relic` drop-table entry, authored through a small, named seedsmith addition this module
also owns (§Design 4a — a new `relic` `entryKind` plus a targeted append operation), with zero
C#/`LootPipeline` runtime change and, just as importantly, **zero hand-edited JSON** in any existing
`_meta.model`-stamped drop-table file.

## Locked anchors

- **Relics are rolled items, never a stock/currency.** Superseded by the owner directly (ideal doc
  §The owner's third-pass framing): *"relic is unique items, they will generate by seedsmith item
  generator and make drop tables to register them."* This module does not revisit that — it resolves
  the one sub-question the owner's framing left open.
- **Relics register as their own sibling kind — `unique`'s `KindSpec` is NOT relaxed.** See §The
  schema decision below for the full evidence trail. Downstream modules (`wonder-build-flow`
  especially) should expect a relic to be a `DropEntryKind.Relic` entry pointing at a
  `ContainerKind.Relic` row, **not** a `DropEntryKind.Unique` entry with a null base type.
- **A relic's per-instance mint does not write `item_generation`.** That table's three NOT NULL
  columns (`base_type_id`, `role`, `frame` — `RpgStore.Loot.cs:24-26,113-118`) are an equip-shaped
  provenance stamp every existing `DropEntryKind` (including `Unique`) satisfies today. A relic has
  none of the three by definition. This module's mint path writes `rpg_item` (ownership) directly and
  skips `item_generation` (drop-pity/analytics provenance) — see §Design 3.
- **`web-wave` is a real, wired `SourceKind` the ideal doc's own listing omitted.** Corrected here,
  once, so downstream modules do not inherit the undercount — see §Built table.
- **Lawn (`pvz-run`) stays excluded.** `DropTableValidator.UndesignedSourceKind` (`DropTableValidator.cs:50`)
  refuses it for a pre-existing, unrelated reason (no `contentLevel` source for a PvZ match). This
  module does not resolve that gap and authors no relic table against it.
- **The exact relic table/row/rate is this module's own open content call**, per the map's own
  Tunables row, not settled architecture. §Tunables below proposes a default, not a lock.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| The closed 15-kind item-seed vocabulary, including `unique` — the nearest existing fit before this module's own addition | `tools/seedsmith/seedsmith/adapters/items/kinds.py:48-109` (`KINDS` tuple, `assert len(KINDS) == 15`); ported from `tools/ItemSeedValidator/Registries/KindCatalog.cs` |
| `unique`'s `KindSpec` hard-requires `frame`, `baseType` (a **reference field**, `refs={"baseType"}`), `rarity`, `fixedAtoms`, `counterPressure`, `tags`, `powerAxis` | `kinds.py:56-68`; the mirrored C# side, `KindCatalog.cs:70-105` (`Defined("unique", ...)`) |
| `unique`'s whole validation apparatus is built around **competing against rolled rares for the same equip role** — counter-pressure (`drawback`/`conditional`/`narrow`, checked against content), a budget cap (rung baseline + 1.5 AE), axis-collision across `(role, rung band, power_axis)`, an 8-of-15 role quota, a `jewel-minor` ban, and an explicitly **unmeasured but designed-for** parity invariant (`W ∈ [25%,75%]` against a randomly rolled rare) | `docs/architecture/item/ssot-uniques.md` §3.5, §3.7 (device 1-4); `docs/architecture/item/spec-uniques.md` §"The mutual-relevance mechanism" (three HARD validators + one reported metric) |
| A unique's minting path (`MintUnique`) hard-requires a resolvable `(Frame, BaseTypeId)` pair via `UniqueBaseTypeFor`, **for a persistence reason, not an authoring-convenience reason**: the comment states plainly that `item_generation.frame`/`.base_type_id` are NOT NULL, so a unique minted without this pair "could never be persisted" | `src/FusionRpg.Core/Items/Drops/LootPipeline.cs:420-473` (`MintUnique`, comment at :455-459) |
| `item_generation` — the per-instance drop-provenance stamp every existing `DropEntryKind` mints into — declares `base_type_id TEXT NOT NULL`, and its row shape (`ItemGenerationRow`) also carries a NOT NULL `Role`/`Frame` | `src/FusionRpg.Data/Sqlite/RpgStore.Loot.cs:24-26` (`ItemGenerationRow`), `:113-118` (`CREATE TABLE item_generation`, `base_type_id TEXT NOT NULL`) |
| `rpg_item` — the durable ownership root every rolled item (including a future relic) needs — carries **no** `base_type_id`/`role`/`frame` column at all; it is a thin FK to `effect_instance.instance_id` plus ownership metadata | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:46-98` (`RpgItemRow`, `CREATE TABLE rpg_item`) |
| `effect_container.base_type_id` is **nullable at the container-template level** — the NOT NULL constraint is only on the per-instance `item_generation` stamp, not on the container schema itself | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:20-35` (`CREATE TABLE effect_container`, `base_type_id TEXT` with no `NOT NULL`) |
| `ContainerKind` — 11 values, closed, each addition single-purpose and single-owner (`Gem`/`Charm`/`Combo`/`Consumable` each own a generator and an id prefix; `Enemy` owns `encounter-generator`) | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:1-38` |
| `DropEntryKind` — 9 values, closed, `Unique` already a member | `src/FusionRpg.Core/Items/Drops/DropTableModel.cs:16-27` |
| `DropTableDraw.UnavailableKinds` — the closed refusal-by-name list for kinds not yet resolvable; `Unique` was removed from it once `MintUnique` shipped (D4.27) | `src/FusionRpg.Core/Items/Drops/DropTableModel.cs:145-178` |
| **`KnownSourceKinds` is 8 values, not 6** — the ideal doc and the map row both list only `world-sector`/`expedition-tier`/`dungeon-room`/`dungeon-clear`/`dungeon-quest`/`siege-assault`, omitting `web-wave` entirely (and folding `pvz-run` into prose rather than the count) | `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs:58-59`: `{ "web-wave", "expedition-tier", "world-sector", "pvz-run" (= `UndesignedSourceKind`), "dungeon-room", "dungeon-clear", "dungeon-quest", "siege-assault" }` |
| `web-wave` is a real, standalone-first (gameless) combat-wave source, already deriving its own loot correlation key | `src/FusionRpg.Core/Items/Drops/LootPipeline.cs:118-122` (`Derive`: `"web-wave" => $"loot:{sourceId}"`) |
| `LootPipeline.Resolve`/`LootCorrelation.Derive` are **source-kind-agnostic** with respect to `DropEntryKind` — no code branches "this `SourceKind` may only carry these entry kinds." A `SourceKind` is a tag on a `loot_source` row pointing at a `DropTableRow`; any `DropTableRow` may contain any mix of entry kinds | `LootPipeline.cs:118-126` (the `Derive` switch keys only on correlation-key shape, never on allowed entry kinds); `DropTableValidator.cs:61-117` (`Validate` checks kind membership and the `pvz-run` refusal, nothing per-source about entry kinds) |
| `world-sector` already resolves a real, live drop table off a sector's own danger band — the concrete proof the world-map loop is wired end-to-end today | `src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs:33-39` |
| A `DropTableRow`'s `SourceAllow` must contain `"web"` at import — the standalone-first/gameless-first guarantee already applies to any table a relic entry is added to, with no new work from this module | `DropTableModel.cs:62` (doc comment: *"SourceAllow MUST contain `web`"*) |
| The item program's own precedent for "a sibling, non-equip content class gets its own tunable/registry file" — `uniques.v1.json` for `unique`'s content-side numbers (rung floor, etc.), separate from any generic item file | `docs/architecture/item-map.md:309` (`data/tuning/uniques.v1.json:5`, `rungFloorOrdinal`) |
| **`droptablegen` (the real seedsmith generator behind `items fill --kind drop-table`) has a closed, 6-value `entryKind` vocabulary and no code path that authors `unique`, `charm`, or `table` rows at all** — `RowPlan.entry_kind` is documented as `"equipment" \| "material" \| "currency" \| "insert" \| "consumable" \| "nothing"`, and `assemble_entry` only ever calls `_equipment_row`/`_material_row`/`_consumable_row`/`_insert_row` | `tools/seedsmith/seedsmith/adapters/items/droptablegen/emit.py:74-219` (`RowPlan`, `assemble_entry`); `brief.py:32-35` (`EQUIPMENT_SLOT_COUNT`/`MATERIAL_SLOT_COUNT`/`CONSUMABLE_SLOT_COUNT`/`GEM_SLOT_COUNT` — no `UNIQUE_SLOT_COUNT`/`RELIC_SLOT_COUNT`) |
| **A separate, THIRD closed `entryKind` gate exists at seed-authoring time** — not the Python `KindSpec` (`kinds.py:102-104`, which only shapes the whole `drop-table` file), not the C# runtime `DropEntryKind` enum this module already plans to extend (`DropTableModel.cs`) — but `ItemSeedValidator`'s own `EntryKinds` array, the thing `entry-shapes.md §9` calls "a closed 9-value enum" | `tools/ItemSeedValidator/Checks/DropTableCheck.cs:19-27` (`EntryKinds = { "equipment", "material", "currency", "insert", "charm", "consumable", "unique", "table", "nothing" }`, plus a second ref-requiring set at :27) |
| **`items fill`'s dependency graph never wires `unique` to `drop-table` at all**, and `discover_drop_table_slots`/the `--kind drop-table --slot N` step (the mechanism an earlier adversarial pass cited as already sufficient) mints brand-new `droptable.d{slot}-{seq:03}` **rows**, never appends a group/entry into an EXISTING, already-shipped row | `tools/seedsmith/seedsmith/adapters/items/fill.py:35-48` (`MAP_DEPENDENCY_EDGES` — `gem`/`consumable`/`material`/`base-type` → `drop-table`, no `unique` edge); `fill.py:293-302` (`discover_drop_table_slots`); `fill.py:532-540` (the `drop-table` fill step calls `assemble_entry` via a brand-new `--count`-many-new-tables pass, never an append) |
| **The real corpus already contains 84 `entryKind: "unique"` rows** (`groupKey: "r2-unique"`, one such group in the FIRST table of each of `d1`/`d2`/`d4.json`, `dropBand` fixed per slot) that **no current seedsmith command can reproduce** — confirmed no emit function, no brief slot, no dependency edge for `unique`. `entry-shapes.md §9`'s own "Added 2026-08-23 (wave R2)" note documents *when* `unique`/`consumable` joined the documented vocabulary, but only `consumable` ever got real generator code (`_consumable_row`, `CONSUMABLE_SLOT_COUNT`); the `unique` rows predate or bypass `droptablegen` entirely. **This module must not cite that pattern as a working precedent** — it is unexplained content debt, not a sanctioned path | Read directly this session: `data/seed/items/drop-tables/d1.json` (`droptable.d1-001`, group `r2-unique`, 40 rows), `d2.json` (`droptable.d2-001`, 40 rows), `d4.json` (`droptable.d4-001`, 32 rows); `docs/architecture/item/entry-shapes.md:552-573` |

### Real gap

| Gap | What would have to be built |
|---|---|
| **No `relic` `KindSpec`.** The 15-kind vocabulary has nothing shaped like "a rolled item with no equip role and no base type" — every existing kind that reaches a player as a found item (`unique`, `charm`, `gem`, `consumable`) is anchored to an `item_base_type`/role concept, per the item program's own base-type-first design | A new `KindSpec` (`relic`), registered in `tools/seedsmith/seedsmith/adapters/items/kinds.py` and its C# mirror `tools/ItemSeedValidator/Registries/KindCatalog.cs`, per §Design 1 |
| **No `DropEntryKind.Relic`.** The 9-value enum has no member for a non-equip rolled unique | A 10th `DropEntryKind` member, per §Design 2 |
| **No `ContainerKind` value for a relic's own `effect_container` row.** Reusing `ContainerKind.Item` would mean a relic sits in the same partition as every equip-shaped container despite having no role/frame identity to filter on | A 12th `ContainerKind` value (`Relic`), owned by this module's own generator, per §Design 2 |
| **No mint arm that persists a relic without an `item_generation` row.** `MintUnique` (and, by construction, every existing `Mint*` arm) always produces an `ItemGenerationRow` with NOT NULL `base_type_id`/`role`/`frame` — a relic has none of the three | A new `MintRelic` arm in `LootPipeline.cs` that writes `rpg_item` directly and skips `item_generation`, per §Design 3 |
| **No relic anchor content.** Authoring work only, once the kind/enum additions above exist | Author relic anchors through the new `relic` `KindSpec` (§Design 1) |
| **No seedsmith mechanism adds a new `entryKind` to an EXISTING, already-generated drop-table row.** `droptablegen` only mints whole NEW `droptable.*` rows with a fixed six-kind shape (§Built table); appending a group to a table a `loot_source` row already points at — exactly what wiring the seven real `SourceKind`s needs — has no code path today, and the closest-looking precedent (`unique`'s own 84 `r2-unique` rows) is not reproducible by any current command | A 10th `EntryKinds` member in `tools/ItemSeedValidator/Checks/DropTableCheck.cs` (the real seed-time validator gate) plus a new `_relic_row` emit function and a targeted append-to-existing-table operation in `droptablegen`, per §Design 4a |

## The schema decision, with evidence

**The ideal doc framed this as one question: does `unique`'s `KindSpec` grow a no-`baseType`
variant, or does a relic register as its own sibling kind/`DropEntryKind` member?** Investigated
fresh this session by opening the KindSpec, the validator apparatus it drives, and the minting
persistence path underneath it — not by re-reading the ideal doc's own citations. **Conclusion:
sibling kind. Relaxing `unique` is both architecturally the wrong shape and mechanically blocked at
the schema level, not merely inconvenient.**

**1. `unique`'s validation apparatus is not "an equip-role item with strict rules" that a relic could
opt out of piecemeal — it is a device for keeping a hand-authored item *mutually relevant with rolled
rares in the same role*, and a relic has no role to be relevant in.** Every one of the four devices
in `ssot-uniques.md` §3.7 (counter-pressure, the 1.5 AE budget, anti-convergence by
`(role, rung band, power_axis)`, and the parity invariant against a "randomly rolled rare at rung
n") is defined **in terms of the equip role and the rolled-rare pool the unique competes against**.
A relic, per the owner's own description, is never equipped and never competes against a rolled
drop for a slot. Making `unique` accept a null `baseType` would not shrink this apparatus by one
required field — it would leave every one of these validators either vacuously true (dishonest) or
requiring its own carve-out (a second class of `unique` row, hiding inside one KindSpec, which is
the exact "a class inferred from a shape is a class anyone can forge" failure `spec-uniques.md`
§4.2 already rejected for a different reason). A sibling kind with its own, much smaller validator
(no role, no counter-pressure, no axis-collision, no parity target) is the narrower change.

**2. The blast radius is not just the KindSpec — it is the persistence schema underneath it, and
that schema is NOT NULL.** `MintUnique`'s own comment states the reason a base type is
required is not authoring convenience but persistence: *"`item_generation.frame`/`.base_type_id` are
NOT NULL, so a unique instance minted without this pair could never be persisted"*
(`LootPipeline.cs:455-459`). Opening `item_generation`'s schema confirms this directly:
`base_type_id TEXT NOT NULL` (`RpgStore.Loot.cs:116`), and `ItemGenerationRow` also carries NOT NULL
`Role`/`Frame` (`RpgStore.Loot.cs:24-26`). **This is a hard schema constraint on every existing
`DropEntryKind`'s mint path, not a soft rule the KindSpec question alone can relax.** Even a
hypothetical no-`baseType` `unique` variant would still hit this wall the moment it tried to mint
through the existing pipeline — the real fix has to touch the mint arm and the provenance table,
which is the same amount of work whether the KindSpec is `unique` (relaxed) or `relic` (new).
**Given the mint-path work is unavoidable either way, the sibling kind is strictly cheaper**: it
adds one new, small, no-role validator instead of retrofitting an exemption into a heavily-loaded
144-row content class's validator suite.

**3. `effect_container.base_type_id` is nullable at the template level, which is exactly why the
`item_generation` constraint above is the real blocker, not a red herring.** `effect_container`
itself (`RpgStore.Containers.cs:20-35`) declares `base_type_id TEXT` with **no** `NOT NULL` — so a
relic's own container template could exist with a null base type today, with zero schema change.
The wall is entirely in the per-instance mint stamp (`item_generation`), confirming the fix belongs
in the mint arm (§Design 3), not in the container or KindSpec schema.

**4. Precedent: every existing narrow, non-equip content addition to this closed vocabulary got its
own `ContainerKind` and its own generator, never a relaxation of an existing equip-shaped kind.**
`Gem`/`Charm`/`Combo`/`Consumable` were each added as a new `ContainerKind` value, each owned by
exactly one generator, each with its own id prefix (`ContainerRow.cs:1-38`). None of them was
implemented as "loosen `unique`'s required fields." A relic — never equipped, no role, no
counter-pressure story — is the same shape of addition these four already are, and this module
follows the same, already-proven pattern rather than inventing a different one.

**Net:** `relic` is a new, small `KindSpec` (§Design 1); `DropEntryKind.Relic` and
`ContainerKind.Relic` are new, narrow enum members (§Design 2); a `MintRelic` arm persists to
`rpg_item` only, bypassing `item_generation` (§Design 3). Nothing about `unique`'s existing 144-row
corpus, its validators, or its `KindSpec` is touched.

## Design

### 1. The `relic` `KindSpec`

Registered beside `unique` in both the Python and C# registries, mirroring the port discipline
`kinds.py`'s own header already documents (transcribed from C#, not re-derived):

```
kind: "relic"
directory: "relics"
namespace: "relics"
required: COMMON_REQUIRED | { "flavorKey" }   # no frame, no baseType, no role, no counterPressure,
                                                # no powerAxis -- none of these describe a relic
optional: COMMON_FIELDS | { "theme", "themeKey", "acquisition", "fixedAtoms" }
refs: {}                                       # no reference fields -- nothing to a base type or role
```

`fixedAtoms` stays **optional**, not required: a relic's whole identity per the owner's framing is
"a thing you carry and spend," not a combat-stat bundle — many relics may carry zero atoms and exist
purely as a Wonder-build cost token. Where a relic *does* carry an atom (a flavor-only passive, a
world-map-scope effect reserved for `wonder-effect-*` to read later), it uses the same
`effect_container_atom` mechanism every other kind does — no new atom-authoring path.

`acquisition` reuses the same closed vocabulary `unique` already validates against
(`drop`/`source-locked`/`deterministic`, `ssot-uniques.md` §4.5) — not re-invented, since the
concept ("how deterministic is finding this") is identical for a relic and a unique.

### 2. `DropEntryKind.Relic` and `ContainerKind.Relic`

```csharp
// DropTableModel.cs — 10th value. Comment updated the same way the file's own header already
// tracks its own drift history (7 -> 9 -> 10), per this repo's "propagate the correction" rule.
public enum DropEntryKind
{
    Equipment, Material, Currency, Insert, Charm, Consumable, Unique, Relic, Table, Nothing,
}
```

```csharp
// ContainerRow.cs — 12th value. Owned by relic-item-kind's own generator, id prefix "relic".
public enum ContainerKind
{
    Item, Trait, Skill, SpeciesPassive, Patron, WorldBuff, Enemy, Gem, Charm, Combo, Consumable, Relic,
}
```

`DropTableDraw.UnavailableKinds` gains a `[DropEntryKind.Relic] = "..."` entry the moment this
module lands but before the mint arm ships (mirroring exactly how `Unique` sat in that dictionary
until `MintUnique` existed, `DropTableModel.cs:135-137,173-177`) — never silently resolved to
nothing, per the file's own closed-refusal discipline.

### 3. `MintRelic` — persists to `rpg_item` only, never `item_generation`

```csharp
// LootPipeline.cs — beside MintUnique. A relic has no Frame/BaseTypeId/Role: it is never equipped
// and occupies no item_base_type row, so it cannot satisfy item_generation's NOT NULL triple
// (RpgStore.Loot.cs:24-26, 113-118) and must not try to. This mirrors rpg_item's own design --
// "ownership is policy, not content" (RpgStore.Items.cs:40-44) -- by writing ownership directly
// and skipping the drop-provenance/pity stamp a relic has no pity mechanism to feed anyway
// (ssot-uniques.md's "no unique pity" reasoning applies here even more directly: a relic's rung
// concept, if it has one at all, is not the item rarity ladder -- see Tunables).
LootGrant? MintRelic(DropTableEntryRow entry, out AtomRejection rejected)
{
    var i = index++;
    var rollSeed = SeededRng.DeriveStream(lootSeed, LootStreams.RollSeed(i)).NextULong();

    var grant = new LootGrant(
        i, DropEntryKind.Relic, entry.RefId, 1, entry.AffixChannel,
        BaseTypeId: null, Frame: null, RarityId: null, RarityOrdinal: null,
        ItemLevel: itemLevel, RollSeed: rollSeed);

    if (view.MintRelic is { } mint)
    {
        var minted = mint(grant);            // writes rpg_item + effect_instance only
        if (!minted.Rejection.IsOk) { rejected = minted.Rejection; return null; }
        grant = grant with { InstanceId = minted.InstanceId };
    }
    rejected = AtomRejection.Ok;
    return grant;
}
```

`view.MintRelic` is a new delegate on `LootContentView` alongside the existing `Mint` — kept
**separate**, not overloaded, because the two write different tables (`item_generation`+`rpg_item`
vs. `rpg_item` alone) and a shared delegate would need a branch inside the host implementation that
this design keeps at the call-site instead (One ActorHub-style discipline: the caller picks the
right narrow contribution point, never a fat delegate with an internal switch).

**`LootGrant`'s existing record shape already accepts nulls for `BaseTypeId`/`Frame`/`RarityId`/
`RarityOrdinal`** (they are the same fields `MintUnique` populates, and C# reference/nullable-value
fields default to absent) — no change to `LootGrant` itself is needed, only the new arm and the new
delegate.

### 4. Wiring the seven real `SourceKind`s — authoring, not engine work

Confirmed this session (§Built table): `LootPipeline.Resolve`/`LootCorrelation.Derive` never branch
on which `DropEntryKind`s a given `SourceKind` may carry. A `loot_source` row names a `SourceKind`
and a `TableId`; the `DropTableRow` at that id may mix any `DropEntryKind`s, `Relic` included, the
moment §Design 2's enum member exists. So at the **C# runtime layer**, wiring the seven real
`SourceKind`s (`web-wave`, `expedition-tier`, `world-sector`, `dungeon-room`, `dungeon-clear`,
`dungeon-quest`, `siege-assault`) to carry a relic needs zero new correlation-key shapes and zero new
`LootPipeline` branches beyond §Design 3's mint arm.

**That is not the whole story, and stating it alone is exactly the framing that produced this
spec's own confirmed hard-rule violation (see §Design 4a).** "Add `DropTableEntryRow`s of
`Kind = Relic` into whichever `DropTableRow`s those seven `SourceKind`s' `loot_source` rows already
point at" describes a **seed-authoring** step, and those `DropTableRow`s live in
`data/seed/items/drop-tables/*.json` files that already carry a `_meta.model` generator-provenance
stamp. Per AGENTS.md's binding rule, reaching into one of those files by hand to insert a JSON row —
which is what "author... into whichever tables already point at" reduces to without further work —
forks the corpus from its generator. Verified fresh this session: no seedsmith command today can add
a `Relic`-kind entry (or, for that matter, a `unique`/`charm`-kind entry — see §Built table) to an
already-shipped drop-table row. **§Design 4a names the small, real generator capability this module
must add before "author relic anchors + drop-table rows" can mean anything other than a hand-edit.**

`pvz-run` (lawn) is excluded, unchanged from the ideal doc's own conclusion — `DropTableValidator`
refuses it by name for an unrelated, pre-existing reason (`DropTableValidator.cs:50,112-117`). This
module authors no relic table against it and does not attempt to resolve that gap.

### 4a. seedsmith `drop-table` adapter gains a `relic` `entryKind` + a targeted append operation

**This is a real, scoped sub-task this module owns — named plainly, not papered over.** §Design 4's
own evidence (§Built table) shows `droptablegen` cannot author a `relic` row today, and the one thing
in the real corpus that looks like a precedent (`unique`'s 84 `r2-unique` rows already in
`d1`/`d2`/`d4.json`) is not reproducible by any current command — it predates or bypasses this
generator, and this module does not get to lean on it as evidence a mechanism already exists. The
gap is two-layered, and both layers need a small, named fix:

**Layer 1 — the closed vocabularies.** Three separate places currently list drop-table entry kinds,
and all three are missing `relic`:

1. `tools/ItemSeedValidator/Checks/DropTableCheck.cs:19-27` — the real seed-time validator gate.
   Gains `"relic"` in `EntryKinds` and in the ref-requiring set, exactly the way `unique`/`charm`/
   `consumable` already sit there.
2. `docs/architecture/item/entry-shapes.md` §9 — the hand-authored (not generated) doc that states
   the enum in prose and its own ref-resolution table. This file carries no `_meta` stamp and is
   **not** the corpus this rule protects; editing it directly, the same way its own "Added
   2026-08-23 (wave R2)" note already documents `unique`/`consumable` joining, is the correct and
   only way this vocabulary bump gets recorded for a human reader.
3. `src/FusionRpg.Core/Items/Drops/DropTableModel.cs` — the C# runtime `DropEntryKind` enum, already
   covered by §Design 2. Distinct from #1 (seed-time authoring gate) and #2 (docs) — all three must
   move together or the corpus, the validator, and the runtime silently disagree about what `relic`
   means.

**Layer 2 — the generator itself has no way to place a row, new or appended.** Two additions, both
inside `tools/seedsmith/seedsmith/adapters/items/droptablegen/`:

- `emit.py` — a new `_relic_row(ref, drop_band, legal_refs)`, mirroring `_insert_row`/
  `_consumable_row` exactly: refuses a `ref` that does not resolve against the real, on-disk relic
  corpus (`legal_relic_refs()`, a new `tuning.py` reader mirroring `load_gem_ids`/
  `load_consumable_ids`, once `data/seed/items/relics/` has content per §Design 1).
- `brief.py`/`fill.py` — a `relic_refs` field on `RowSlotPlan` plus a `RELIC_SLOT_COUNT` (proposed
  default 0, offered per call rather than on every table, mirroring `rarityFloor`'s own "optional,
  not on every row" shape) covers a **brand-new** table minted from here on. It does **not**, by
  itself, put a relic in the seven already-shipped tables the real `SourceKind`s already point at —
  that needs the second half:
- **A targeted append operation — the genuinely new capability, not a variant of an existing one.**
  A way to add ONE new group (e.g. `<slug>-relic`) with `Relic`-kind entries into an EXISTING,
  on-disk `droptable.*` row, addressed by its own id, validated the same way `assemble_entry`
  validates a brand-new table (id grammar, legal refs, legal `dropBand`s) and then rewritten to that
  partition's file through the SAME production-write path (`--write`/`--allow-production-tree`)
  every other kind already uses. This is the piece with no sibling to copy — every existing
  `droptablegen` entry point mints a new row; none edits one in place. Until it exists, the seven
  real `SourceKind`s cannot receive a relic entry through any sanctioned path, and this module's own
  scope includes building it, not just the KindSpec/enum plumbing above. Once built, the regenerate
  step this module runs (proposed shape; the exact flag name is this module's own implementation
  call, not locked here) looks like:

  ```powershell
  python -m seedsmith items generate --kind drop-table --append droptable.d1-001 \
      --entry-kind relic --ref relic.<id> --drop-band <band> --write
  ```

  run once per table this module needs to touch (`web-wave`, `expedition-tier`, `world-sector`,
  `dungeon-room`, `dungeon-clear`, `dungeon-quest`, `siege-assault` each resolve to a real, already-
  identified `droptable.*` id via their `loot_source` rows) — never a manual JSON insert, and the
  regenerated diff (still carrying the file's real `_meta` stamp) is what gets committed.

**Why this is small, not a re-architecture.** Every validator this needs already exists in
`droptablegen.tuning` (`load_drop_band_enum`, the id-grammar regexes in `emit.py`); the append
operation reuses them wholesale. The new surface is: one `EntryKinds` entry (Layer 1), one emit
function (mirrors four already-shipped siblings), one relic-ref reader (mirrors two already-shipped
siblings), and one append entry point that reads-validates-rewrites a single existing file — the
same shape every `--write` path in this adapter already has, aimed at an existing id instead of a
freshly minted one.

### 5. Relic rarity/tier — deliberately NOT the 10-rung item ladder

A relic does not adopt `effect_container.rarity`/the shared `CreatureRarity`-aligned ten-rung ladder
(`ssot-rarity.md`). That ladder's entire mechanism — count bands, tier windows, an overlap invariant
between rolled magnitude distributions — governs *rolled* content; a relic (per §Design 1) is
authored with at most a handful of fixed atoms and no pool draw, the same non-rolled shape a
unique's fixed core has, but without even a unique's one variance slot. Forcing a `rarity` value onto
a relic purely to satisfy `effect_container.rarity`'s column (nullable, per §Built table) would
imply participation in a ladder mechanism nothing about a relic exercises. If a future Wonder-cost or
drop-rate design wants a coarse "how special is this relic" axis, that is a **new, small,
orthogonal tag on the relic anchor itself** (e.g., an authored `tier` field distinct from item
rarity) — named here as an open content question for whoever authors the first relic anchors, not
decided by this module.

## Tunables

| Number | Owner | Notes |
|---|---|---|
| Relic drop rate per confirmed loop (`web-wave`/`expedition-tier`/`world-sector`/`dungeon-room`/`dungeon-clear`/`dungeon-quest`/`siege-assault`) | `data/tuning/loam-relics-wonders.v1.json` (per the map's own Tunables row) | **Proposed default, not locked**: a flat low `Weight` (e.g. matching a unique's own "low weight" random-drop precedent, `ssot-uniques.md` §4.5) on each table's relic entry — genuinely a balance call the map itself named as still open (map §"Open items carried from the ideal") |
| Relic anchor content-authoring numbers (any per-relic `fixedAtoms` magnitude, if a relic ever carries one) | `data/tuning/relics.v1.json` (new, mirroring `uniques.v1.json`'s precedent, `item-map.md:309`) | Only needed once a relic anchor authors a magnitude — the KindSpec itself (§Design 1) makes `fixedAtoms` optional, so this file may start empty |
| Relic "tier" axis, if authored (§Design 5) | Same file as above | Open content call, not this module's to lock |

No number in this module is a hard-coded existence cap: `DropTableDraw.EffectiveWeight`'s
weight-based draw already treats an unreachable/disabled entry as `weight = 0` (kept, never
deleted, `DropTableModel.cs:191-201`), the same overflow-safe, no-silent-clamp discipline every
other drop-table entry uses.

## Numeric types

`Weight`, `MinCount`/`MaxCount` on `DropTableEntryRow` are `int`, matching every existing entry
(`DropTableModel.cs:88-102`) — a per-entry draw weight is a bounded, structural quantity (the same
class of number `DropTableDraw.Draw`'s own overflow check already guards, `DropTableModel.cs:212-236`),
not a `contentScale`-reachable magnitude. `RollSeed` stays `ulong` via `SeededRng`, unchanged from
every existing mint arm. No new `long`-magnitude field is introduced by this module — a relic
carries no combat-stat magnitude of its own by construction (§Design 1).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Relic"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Relic"
cd tools/seedsmith
python -m pytest tools/seedsmith/tests/test_drop_tables_gen.py -q   # _relic_row + append operation (§Design 4a)
python -m seedsmith check data/seed/items --adapter items   # once relic anchors exist
# Regenerate, never hand-edit, once relic anchors exist and §Design 4a lands:
python -m seedsmith items generate --kind relic --write             # data/seed/items/relics/
python -m seedsmith items generate --kind drop-table --append <table-id> \
    --entry-kind relic --ref relic.<id> --drop-band <band> --write  # one per SourceKind table (§Design 4a)
.\scripts\guard-dal.ps1        # MintRelic's rpg_item write stays inside FusionRpg.Data
```

## Structure

```text
tools/seedsmith/seedsmith/adapters/items/kinds.py                  edit — add "relic" KindSpec (§Design 1)
tools/ItemSeedValidator/Registries/KindCatalog.cs                   edit — mirror the C# side, same fields
tools/ItemSeedValidator/Checks/DropTableCheck.cs                    edit — 10th EntryKinds member "relic",
                                                                     the real seed-time validator gate (§Design 4a)
docs/architecture/item/entry-shapes.md                              edit — §9's own hand-authored doc gains
                                                                     "relic" in its documented entryKind table
                                                                     (doc only, carries no _meta stamp, never
                                                                     generated seed data) (§Design 4a)
tools/seedsmith/seedsmith/adapters/items/droptablegen/emit.py       edit — new _relic_row emit function (§Design 4a)
tools/seedsmith/seedsmith/adapters/items/droptablegen/brief.py      edit — relic_refs slot + RELIC_SLOT_COUNT,
                                                                     plus the new append-to-existing-table entry
                                                                     point (§Design 4a)
tools/seedsmith/seedsmith/adapters/items/droptablegen/tuning.py     edit — load_relic_ids() reading
                                                                     data/seed/items/relics/ (§Design 4a)
tools/seedsmith/seedsmith/adapters/items/fill.py                    edit — relic -> drop-table dependency edge
                                                                     (MAP_DEPENDENCY_EDGES), CLI plumbing for the
                                                                     append operation (§Design 4a)
src/FusionRpg.Core/Items/Drops/DropTableModel.cs           edit — DropEntryKind.Relic (§Design 2)
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs           edit — ContainerKind.Relic (§Design 2)
src/FusionRpg.Core/Items/Drops/LootPipeline.cs             edit — MintRelic arm + LootContentView.MintRelic delegate (§Design 3)
src/FusionRpg.Data/Sqlite/RpgStore.Loot.cs                 edit — the MintRelic host implementation (rpg_item write only)
data/seed/items/relics/                                    NEW — relic anchor content, authored through the new
                                                             `relic` KindSpec/seedsmith pipeline (this module's own
                                                             authoring), never hand-typed JSON
data/seed/items/drop-tables/d1.json, d2.json, d3.json, d4.json (whichever the 7 SourceKinds'
                                                             loot_source rows resolve to)
                                                             REGENERATE, not hand-edit — the new droptablegen
                                                             append operation (§Design 4a) writes one new
                                                             `Relic`-kind group into each table; the regenerated
                                                             diff (still `_meta`-stamped) is what gets committed
data/tuning/relics.v1.json                                 NEW (may start near-empty, §Tunables)
tests/FusionRpg.Core.Tests/Items/RelicTests.cs             NEW
tests/FusionRpg.Data.Tests/Items/RelicMintTests.cs         NEW
tools/seedsmith/tests/test_drop_tables_gen.py                       edit — cover _relic_row + the append operation (§Design 4a)
UNTOUCHED: unique's KindSpec/validator/corpus (kinds.py:56-68, UniqueValidator.cs, the 144-row
           corpus), item_generation schema, rpg_item schema, StructureCatalog/LoamProduction
           (wonder-structure's own module), scoped-inventory-hierarchy tables (consumed later by
           wonder-build-flow, never touched here); every non-Relic row already in d1..d4.json
```

## Code style

```csharp
// A relic mint writes rpg_item directly -- never item_generation, which has no room for a thing
// with no base type, role or frame (RpgStore.Loot.cs:24-26, 116-118 -- all three NOT NULL). This
// is the load-bearing sentence for this module: if a relic mint ever needs an item_generation row,
// the design has drifted back toward treating a relic as equipment.
LootMintResult MintRelicUnlocked(SqliteConnection db, SqliteTransaction tx, LootGrant g, string playerId)
{
    var instanceId = Guid.NewGuid().ToString("n");
    // effect_instance row: content-derived fingerprint only, no player_id (RpgStore.Items.cs:40-44's
    // own "ownership is policy, not content" rule -- unchanged for a relic).
    InsertEffectInstanceUnlocked(db, tx, instanceId, g.RefId, g.RollSeed);
    InsertRpgItemUnlocked(db, tx, instanceId, playerId, originKind: "drop", originRef: g.RefId);
    return new LootMintResult(instanceId, AtomRejection.Ok);
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `a_relic_anchor_with_no_frame_or_base_type_loads_clean` | the new `relic` `KindSpec` accepts what `unique`'s would reject |
| `a_relic_kindspec_has_no_reference_fields` | `refs = {}` — a relic never points at a base type or role |
| `drop_entry_kind_relic_is_unavailable_until_mint_relic_exists` | mirrors `Unique`'s own pre-`MintUnique` history in `UnavailableKinds` |
| `mint_relic_writes_rpg_item_and_never_item_generation` | queries both tables after a mint; `item_generation` has zero new rows |
| `mint_relic_produces_no_base_type_role_or_frame` | the persisted `rpg_item` row carries none of the three — there is nowhere on that table to put them |
| `any_of_the_seven_known_source_kinds_can_carry_a_relic_entry` | one drop table per real `SourceKind`, each with a `Relic` entry, all resolve without a validator error |
| `drop_table_check_accepts_relic_entry_kind` | `DropTableCheck.cs`'s `EntryKinds` (§Design 4a Layer 1) accepts `"relic"` and requires a resolvable `ref` for it, mirroring `unique`/`charm`/`consumable` |
| `droptablegen_emit_relic_row_refuses_an_unresolved_ref` | `_relic_row` (§Design 4a) raises `IllegalChoiceError` for a `ref` absent from the real relic corpus, mirroring `_insert_row`/`_consumable_row` |
| `droptablegen_append_writes_one_new_group_to_an_existing_table_only` | the append operation (§Design 4a) adds exactly one `Relic`-kind group to the named existing table id and leaves every other row/group in that file byte-identical |
| `pvz_run_still_refuses_regardless_of_entry_kind` | unchanged behavior — a relic entry on a `pvz-run` table still hits `drop.source-kind-undesigned` |
| `a_relic_entry_disabled_or_out_of_ilvl_band_draws_at_weight_zero` | reuses `DropTableDraw.EffectiveWeight`'s existing, unmodified logic |
| `unique_corpus_tests_are_unaffected` | the existing 144-row `UniqueCorpusTests` suite is green with zero changes, proving `unique`'s own machinery was never touched |

## Boundaries

- **Always:** register `relic` as a new `KindSpec`/`DropEntryKind`/`ContainerKind`, never as a
  variant of `unique`'s; write a relic's mint to `rpg_item` only; keep `DropTableDraw.UnavailableKinds`
  naming `Relic` until `MintRelic` ships; author drop-table rows only against the seven real
  `SourceKind`s, never `pvz-run`.
- **Ask first:** any change to `unique`'s `KindSpec`, `UniqueValidator`, or the 144-row corpus (none
  needed by this module, and touching it would cross into the item program's own owned surface,
  `item-map.md`'s External dependencies row for this exact ask); any change to `item_generation`'s
  schema (this module avoids needing one at all, per §Design 3).
- **Never:** give a relic a `baseType` reference, a `role`, a `counterPressure` value, or a
  `powerAxis` — these describe an equip-competing item, which a relic categorically is not; force a
  relic through `item_generation`; put a relic on the 10-rung item rarity ladder (§Design 5); author
  a relic table entry against `pvz-run`; invent a per-relic stock cap (relics are individually rolled
  items, and any future volume ceiling is a tunable weight/rate, never a hard existence cap, per
  AGENTS.md's no-hard-progression-ceilings rule); **hand-edit any `_meta.model`-stamped file under
  `data/seed/items/**`** — every `Relic`-kind row this module puts into `d1`/`d2`/`d3`/`d4.json`
  reaches the file through the §Design 4a append operation and gets committed as its regenerated
  diff, never typed directly into the JSON (AGENTS.md's binding rule; this spec's own prior
  Structure-table line describing a hand-edit was the confirmed violation this revision fixes).

## Success criteria

1. A `relic` anchor authors and loads without a `frame`/`baseType`/`counterPressure`/`powerAxis`
   value, and `unique`'s own validator suite runs unmodified and green.
2. A relic mints to a durable `rpg_item` row with a real `instance_id`, and `item_generation` gains
   zero rows from that mint.
3. Every one of the seven real, wired `SourceKind`s (`web-wave`, `expedition-tier`, `world-sector`,
   `dungeon-room`, `dungeon-clear`, `dungeon-quest`, `siege-assault`) can resolve a drop table
   containing a `Relic` entry, proven by one test per `SourceKind`.
4. `pvz-run` still refuses by name, unchanged, with a relic entry present in the test fixture to
   prove the refusal is not accidentally bypassed by the new kind.
5. `guard-dal.ps1` and the full `FusionRpg.Core.Tests`/`FusionRpg.Data.Tests` suites stay green,
   including the existing 144-row unique corpus tests, with zero edits to unique's own files.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `DropEntryKind.Relic`, `ContainerKind.Relic` | `wonder-structure`/`wonder-build-flow` (module 2/4) — the closed vocabulary a relic-typed cost line references |
| `relic` `KindSpec` (seedsmith) | Content authoring only — no downstream module code depends on the KindSpec shape directly |
| `rpg_item` rows with `origin_kind = "drop"`, `origin_ref` = the relic's container id | `wonder-build-flow` (module 4) — reads an owned, unassigned relic instance the same way it will read any other owned item, once `scoped-inventory-hierarchy` lands a sector/legion-scoped reachability layer on top |
| The seven real `SourceKind` strings, unchanged | `wonder-structure`/downstream balance authoring — no new correlation-key shape to learn |

## Design-gate checklist

```
[x] Subsystems: item generation/drop-table machinery (Core, Data), seedsmith item adapter — no
    Status/ActorHub/Combat subsystem touched.
[x] Read this session: loam-relics-and-wonders-ideal.md (full); loam-relics-and-wonders-map.md
    (full); decisions.md "Loam relics and wonders SSOT (2026-09-13)" (verbatim row); item-map.md
    (full); scoped-inventory-hierarchy/spec-legion-cargo.md (house-style template);
    docs/architecture/item/ssot-uniques.md (full, 1049 lines); docs/architecture/item/spec-uniques.md
    (full); docs/architecture/item/entry-shapes.md §9 (:504-587, re-read this revision — the
    documented drop-table `entryKind` enum and its own "wave R2" precedent).
[x] Code cited by file:line, opened this session: kinds.py (:1-158); KindCatalog.cs (:70-105);
    DropTableModel.cs (:1-301); DropTableValidator.cs (:1-117, :327-344); LootPipeline.cs
    (:118-126, :420-491); ContainerRow.cs (:1-38, :181-195); RpgStore.Containers.cs (:1-55);
    RpgStore.Loot.cs (:14-142, :394-513); RpgStore.Items.cs (:1-115); WorldSectorLootSource.cs
    (:33-39). This revision (adversarial fix), also opened fresh: droptablegen/emit.py (:1-215),
    droptablegen/brief.py (:1-185), droptablegen/schema.py (:1-78), droptablegen/tuning.py
    (:1-164); fill.py (:35-48, :283-546); DropTableCheck.cs (:1-70); the real
    `data/seed/items/drop-tables/d1.json`/`d2.json`/`d4.json` corpus directly (not the spec's own
    worked example).
[x] Drift reported: the ideal doc's and the map's own `SourceKind` listing (6 values) undercounts
    the real enum (8 values: 7 usable + `pvz-run` refused) by omitting `web-wave` — corrected in
    §Locked anchors and §Built table, not silently propagated forward. Second, more serious drift
    corrected this revision: this spec's own §Structure table previously read `data/seed/items/
    drop-tables/  edit — add Relic entries to the 7 real SourceKind tables`, a direct hand-edit of
    `_meta.model`-stamped generator output and a confirmed violation of AGENTS.md's binding rule.
    An earlier adversarial pass's own proposed lead — that `items fill --kind drop-table --slot N`
    (`fill.py:293-294,532-538`) already covers this — was re-verified fresh and found insufficient:
    that path only mints brand-new table rows with a fixed six-kind shape and cannot append an
    entry to an existing, already-wired table. §Design 4a now names the real, small generator
    capability this module owns before any relic entry reaches `d1`/`d2`/`d3`/`d4.json`. That
    earlier lead's citation itself (`fill.py:293-294,532-538`) was factually accurate about what the
    code does — it was the inference drawn from it ("so this already covers wiring the relic
    entries") that was wrong; re-verified fresh rather than carried forward at face value.
[x] Every RPG feature lives in the RPG layer: this module touches only `FusionRpg.Core.Items`,
    `FusionRpg.Data`, and the seedsmith content pipeline — no PvZ/Unity write of any kind, matching
    the ideal doc's own Step 0 framing (relics are minted, carried and spent entirely off the lawn).
[x] No magic numbers: the one number this module proposes (relic drop weight) is named as a tunable
    in `data/tuning/loam-relics-wonders.v1.json`/`relics.v1.json`, not a literal (§Tunables).
[x] No hard progression ceilings: relic volume is a drop weight, never an existence cap; `Boundaries`
    states this explicitly.
[x] `long` for magnitudes, never `float`: this module introduces no new magnitude field (§Numeric
    types) — a relic carries no combat-stat number by construction.
[x] Generated seed data is never hand-edited: `data/seed/items/relics/` is new generator output
    from day one, authored through the new `relic` `KindSpec` (§Design 1) — no hand-typed JSON.
    The seven real `SourceKind` tables DO need a new `Relic`-kind row added to existing,
    `_meta.model`-stamped files (`d1`/`d2`/`d3`/`d4.json`), and this revision corrects the earlier
    draft's plan to do that by direct JSON edit (the confirmed violation). Verified this session
    that no current seedsmith command can add an entry to an already-shipped drop-table row at all
    (`droptablegen` only mints brand-new rows; §Built table, §Design 4a) — so the fix is not "use
    the existing command instead," it is a small, named generator addition: a `relic` `EntryKinds`
    member in `DropTableCheck.cs` plus a targeted append operation in `droptablegen` (§Design 4a).
    Every `Relic`-kind row this module puts into the real corpus reaches it as that operation's
    regenerated diff, committed as generator output, never a manual edit.
[x] A guardrail validates the contract, never a population count: §Testing strategy asserts kind
    membership, mint-path table writes, and per-`SourceKind` resolvability — no test asserts how
    many relics exist or their authored names/flavor text.
[x] No §2 invariant contradicted: SQL only in `FusionRpg.Data` (`MintRelicUnlocked`); no second
    ActorHub composer (this module has no actor-combat magnitude at all); no second ownership root
    (`rpg_item` stays the only place a relic is owned, per Locked anchors); no `f(Θ)` introduced.
[ ] The exact relic table/row/rate, and whether a relic authors a "tier" tag (§Design 5), are left
    open per the map's own instruction — named as this module's own spec-time content call, not
    assumed solved.
```
