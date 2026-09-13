# Spec: `item-durability-repair`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `item-durability-repair`, row 7 of the
[deployment-hierarchy map](../deployment-hierarchy-map.md) (wave 2, `:112, :124` — branches off
`deploy-carry` directly, not the injury/cache chain). Depends on `deploy-carry` (module 1, **built**
— the battle-settlement hook this module reads is `BattleActorResult`, `BattleModels.cs:509-512`).
**Soft link, not a build gate:** `corpse-cache` (module 3, **unbuilt** — no `rpg_corpse_cache` table
exists on disk yet) for the death-drop-extra-decay sub-feature only — map `:112, :136`: *"its one
integration point with `corpse-cache`... is additive and does not block the rest of the module from
shipping first."* Ideal: [deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"Item
durability, wear, and repair" (D1–D6, locked 2026-09-13) and §"Resolved 2026-09-13 (third clearing
round)" items 6–11. Decisions: [decisions.md](../decisions.md) "Deployment hierarchy SSOT
(2026-09-13)" (P2 — snapshot/settle rule; durability is a Data-layer item property, not a deployment
snapshot field, so P2 does not directly govern it, noted so it is not miscited). **`wound.*`/P1 does
not apply to this module** — durability never touches `StatusRuntime` or the status catalog.

## Objective

Every rolled equipment instance gains a `(max, current)` durability pair. `max` is DERIVED at import
from three fields every base type already carries as a closed, VALIDATED registry value —
`class`/`rarity`/`tags` (`item/seed-contract.md:70`) — never authored, matching the seed contract's
existing "an author may never type a magnitude" rule (`seed-contract.md:88-91`). `current` decrements
once per battle a piece of equipped gear's owner fought in — never per delve room, never mid-fight —
and reaching zero makes the item **unusable, never destroyed by wear alone**: its grants stop
contributing at the next materialize, exactly as if it had been unequipped, through the *existing*
equip-projection reconciliation path rather than a new Hub gate. Two repair tiers restore `current`:
a substrate-only **field touch-up** that requires the party to be carrying the tool and materials and
caps at a partial/eroding result, and a **workbench** repair that normally restores to full but falls
back to the same partial result when material is short. Every repair attempt — either tier — carries
a tunable chance to destroy the item outright instead of restoring it.

Success looks like: an item worn through ten battles with no repair reaches 0 `current` and is
excluded from its owner's next `MaterializeRolledEquipRuntime` grants, while `max` has not moved by a
single unit; a field touch-up with tool and materials present restores some fraction, capped below
`max`; a workbench visit with the full material set restores to `max` unless the destruction roll
fires; commander-pouch gear wears and repairs through the identical mechanism, scoped to
`rpg_player_item_assignment` instead of `rpg_item_assignment`.

## Locked anchors

Quoted verbatim from the ideal doc's "Resolved 2026-09-13 (third clearing round)" block, items 6–11
— not re-litigated, only turned into a buildable shape:

- **D1 — broken-at-zero.** *"An item at zero durability is unusable until repaired (never destroyed
  by wear alone), but every repair *attempt* — field or workbench — carries a tunable failure chance
  that destroys the item permanently instead of restoring it."*
- **D2 — repair-to-full vs erosion, both, graded.** *"Field touch-up (substrate only) caps at a
  partial/eroding restore; workbench repair with the full material set normally restores to full, but
  falls back to the same partial/eroding result when the material on hand can't cover the damage...
  never a binary success/refuse."*
- **D3 — wear granularity, per-battle settlement only, never per delve-room.** *"A long delve wears
  gear no harder than the same battle count fought anywhere else."*
- **D4 — field touch-up ships in v1, and needs carried supplies.** *"Only if the party is physically
  carrying the repair tool and materials (pack-grid cells / actor inventory)... no tool or materials
  on hand, no field repair."*
- **D5 — shard leg at top rungs only.** *"Low/mid-rung repair stays substrate + temper + souls;
  ceiling-content repair additionally consumes a shard leg."*
- **D6 — commander-pouch wear, identical mechanism.** *"Same `(max, current)` shape, same
  wear/repair/destruction rules, just scoped to the commander-pouch assignment table."*

Two structural facts from the wider ideal doc, restated because they bound this module's Design:

- **Stock-backed assignments never wear** (ideal doc §"Item durability..." first bullet) — a fungible
  counter (`rpg_item_stock`, `RpgStore.Items.cs:96-101` per the loot-pack spec's own citation) has no
  instance row to hold a durability pair on; only rolled, instance-pinned rows (`ref_kind =
  "rolled"`, `EquipRefKinds.Rolled`) can wear.
- **Repair never raises `max`; enhancement still owns `+n`.** Durability and `enhance_level`
  (`MutationOp.cs:88` `InstanceHead(int EnhanceLevel, ...)`) are two independent head fields on the
  same instance, never conflated.

## What already exists

Sorted **built / wiring gap / real gap**, verified against code opened this session.

### Built

| Finding | Evidence |
|---|---|
| Durability is named DERIVED with **zero** implementation anywhere | `item/seed-contract.md:84` — `price · weight · durability · salvage yield \| DERIVED \| §8 — none exist yet, all arrive free`; `:231` — `Durability / repair \| class · rarity · tags \| ✅` (the *intended* input columns, not a shipped reader); `item/defect-register.md:194` — `Nothing tests durability, because nothing implements it`; `DelvePrices.cs:15` quotes the same seed-contract line as the reason a merchant price has no default yet — the identical "arrives free, none exist" status applies to durability |
| `class`/`rarity`/`tags` are closed, VALIDATED registry fields on every base type today — zero new authoring surface for max-derivation | `item/seed-contract.md:69-70` — `tags \| VALIDATED \| closed registry`; `role, frame, class, rarity, theme, element \| VALIDATED \| frozen wave-0 registries` |
| The exact head-column migration shape this module extends: `enhance_level`/`enhance_pity_counter`/`mutation_seq`/`state_hash` added onto `effect_instance` via idempotent `ALTER TABLE ... ADD COLUMN` | `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs:29-37` — `EnsureColumn(db, "effect_instance", "enhance_level", "INTEGER NOT NULL DEFAULT 0");` (four sibling columns same shape); helper `RpgStore.cs:3893-3897` — `try { Exec(db, $"ALTER TABLE {table} ADD COLUMN {column} {def};"); } catch { /* already exists */ }` |
| The mutation ledger a repair op appends to: `effect_instance_op` (`op_kind`, `correlation_id`, `op_seed`, `result_json`, `cost_json`), idempotent per `(instance_id, correlation_id)` | `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs:46-66` (`UNIQUE INDEX ux_effect_instance_op_correlation`); `AppendMutationOp` doc, `:91-99` — "commit op row... in one transaction", replay returns the recorded result |
| `op_kind` (`MutationOpKind`) — **exactly ten members**, closed, ask-first to extend | `src/FusionRpg.Core/Items/Mutation/MutationOp.cs:13-48` — `Enhance, RerollValue, RerollAffix, EnhanceTransferOut, EnhanceTransferIn, Restore, SocketAdd, SocketInsert, SocketRemove, SocketImbue`; doc comment `:9-11` — *"It is this module's, and it is closed... Adding a member is ask-first."* |
| **A second, separately-closed ten-member enum** for *priced* operations — `CraftOperation` — not the same namespace as `op_kind` | `src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs:1-44` — `Forge, Upcycle, ForgeGem, Bore, Imbue, Socket, Elevate, Temper, RerollOne, RerollAll`; doc comment `:4-9` — *"The ten priced operations... This is not a second `op_kind` namespace... this enum is the subset of it that has a price, plus three mints that have no `op_kind` at all. Adding a verb here is code."* |
| The exact call shape a `Repair` workbench method follows: resolve → recipe lookup → named seeded stream keyed `(instance, correlation)` → policy resolve → append | `src/FusionRpg.Server/ItemWorkbench.cs:240-269` (`Enhance`) — `TryResolve` (locked/disposition check, `:490-508`) → `TryRecipe(recipeId, CraftOperation.Temper, ...)` → `SeededRng.DeriveStream(unchecked((ulong)RpgStore.DeriveOpSeed(instanceId, correlationId)), MutationOpKinds.StreamName(MutationOpKind.Enhance))` (`:257-259`) → `EnhancePolicy.Resolve` → `AppendMutationOp` |
| `data/tuning/materials.v1.json`'s `operations.{verb}` shape — ten rows, each a `souls`/`substrate`/`shard`/`essence`/`catalyst` leg set scaled by a named `variable` class (`grade`\|`rung`\|`flat`\|`enhanceNext`\|`ceilThirdEnhanceNext`) — the exact shape an eleventh `repair` row extends, matching the ideal doc's own "eleventh operation row" citation | `data/tuning/materials.v1.json:28-93` — ten `operations.*` keys (`forge` through `reroll-all`); `elevate` (`:67-73`) and `reroll-all` (`:86-92`) already carry a `shard` leg — the D5 "shard leg at top rungs" precedent already exists on two sibling operations |
| The 600‰ siege repair-ratio precedent, and the exact widen-first/divide-last `checked` formula the ideal doc names | `src/FusionRpg.Core/World/StructurePolicy.cs:37-47` — `RepairCost(long cost, long maxHp, long currentHp)` → `checked(cost * missing * SiegeTuningPolicy.Structure.RepairCostRatioMilli / maxHp / 1000)`, comment `:33-35` *"Divide by 1000 exactly once, last... Widen before multiplying"*; ratio fed from `data/tuning/siege.v1.json:30` — `"repairCostRatioMilli": 600`; bounds-checked `[0,1000]` at load, `src/FusionRpg.Core/Battle/Board/SiegeTuning.cs:192-194` |
| `rpg_item_assignment` (unique specimens) and `rpg_player_item_assignment` (commander pouch) both exist, are populated, and are already two separate tables under two separate primary keys — D6's "same mechanism, different table scope" is a real, already-built split, not a design to invent | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:152-159` — `CREATE TABLE ... rpg_item_assignment (specimen_id, role, ..., PRIMARY KEY (specimen_id, role))`; `:164-171` — `rpg_player_item_assignment (player_id, role, ..., PRIMARY KEY (player_id, role))` |
| Battle-settlement facts a per-battle wear roll can read and seed from | `src/FusionRpg.Core/Battle/BattleModels.cs:509-512` — `BattleActorResult(string Key, string Side, string SpeciesId, int TypeId, long HpRemaining, long DamageDealt, int Kills, bool Survived, ...)`; `:634` — `BattleReport.Seed` (`ulong`), byte-identical per resolved battle; `:637` — `BattleReport.Rounds` (battle-wide, not per-actor) |
| `rpg_item.Disposition` — closed four-value vocabulary a repair-destruction failure writes into | `src/FusionRpg.Data/Sqlite/RpgStore.ItemUniques.cs:71-73` — `"owned" \| "salvaged" \| "transferred" \| "destroyed"` |
| The deploy-time equip-materialize function whose `assignments` list is the exact seam that already excludes anything "not currently equipped" from stat-granting — the seam this module gates on `durability_current > 0`, never a new ActorHub composer | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:838-875` — `MaterializeRolledEquipRuntime(specimenId, level)`: reads `ListAssignments(specimenId)` filtered to `EquipRefKinds.Rolled` (`:840-842`), diffs against currently-granted sources and withdraws any no-longer-present one (`:846-859` — *"read what is actually stored, withdraw any source no longer backed by a current assignment"*), then projects bindings (`ApplyEquipProjection`) and grants (`ApplyEquippedGrants`) from what remains |

### Wiring gap

None specific to durability itself — there is nothing dormant to wire, because nothing exists yet
(see Real gap). The one true wiring-shaped item: `MaterializeRolledEquipRuntime`'s `assignments` query
(`RpgStore.Items.cs:840-842`) is a single `.Where(...)` clause away from excluding a broken instance —
the withdraw-on-absence machinery it already runs (`:846-859`) needs no new logic, only a durability
read added to the filter predicate alongside the existing `ref_kind == Rolled` check.

### Real gap

| Gap | What would have to be built |
|---|---|
| Durability storage | No columns anywhere. This module's own job — see Design §1. |
| `op_kind` extension | `MutationOpKind` has no `Repair` member; adding one is ask-first per its own doc comment (Built table, above) — **filed on the item program**, not built by this module unilaterally. |
| `CraftOperation` extension | `CraftOperation` also has no `Repair` member — a **second, separate** ask, since it is a different closed enum from `op_kind` (a drift correction against this task's own brief, which named only "`op_kind`" — the priced-operation namespace is a distinct closed list that also needs a reviewed add). |
| `operations.repair` tuning row | `materials.v1.json`'s `operations` object has ten keys, no `repair` key. An eleventh row, same shape as the existing ten. |
| Per-actor round/hit tallies | The ideal doc's Battle-wear bullet names `roundsParticipated` as an input "where available." It is **not** available: `BattleActorResult` (`BattleModels.cs:509-512`) has no rounds-fought or hit-count field, and `BattleReport.Rounds` (`:637`) is battle-wide, not per-actor. This narrows the legal wear formula — see Design §3. |
| Carried-tool/materials check for field touch-up | D4 requires the party to *physically carry* the repair tool and materials, citing "pack-grid cells / actor inventory" as the location. The pack grid (`PackGrid`) is `party-dungeon`'s `loot-pack` module — **approved 2026-09-05, unbuilt** (`party-dungeon/spec-loot-pack.md:3` — *"Status: APPROVED... unbuilt"*), and it is not listed as a dependency of this module in the deployment-hierarchy map at all. There is today no shipped concept of "what a party/specimen is carrying" outside the pack grid. |
| `rpg_corpse_cache` / death-drop hook | Does not exist — confirmed by absence: no such table in `src/FusionRpg.Data`, no `spec-corpse-cache.md` on disk. The death-drop-extra-decay feature has no move-to-cache event to attach to yet (soft link, not a block — see Design §4). |
| `data/tuning/deployment-hierarchy.v1.json` | The new domain file the ideal doc's own Tunables table assigns wear-per-battle ‰ and death-drop-decay ‰+floor to does not exist on disk. If this module lands before `injury-tiers`/`corpse-cache` create it, **this module creates it** with its own two keys; a later module adds to the same file, never a second one. |
| Commander-pouch grants materialize path | `rpg_player_item_assignment` has no `MaterializeRolledEquipRuntime` equivalent — only plain read/write (`RpgStore.Items.cs:716-756`, `SavePlayerItemAssignment`/`ListPlayerItemAssignments`). D6's "identical mechanism" covers storage/wear/repair; it does not retroactively build a commander grants-projection pipeline that does not exist — **out of this module's scope**, named so parity is not assumed where none exists yet. |

## Design

### 1. Storage — two new columns on `effect_instance`, not a new table

Follow the exact `enhance_level` precedent (`RpgStore.InstanceOps.cs:29-37`) rather than inventing a
sibling table:

```
EnsureColumn(db, "effect_instance", "durability_max",     "INTEGER");        -- NULL until derived
EnsureColumn(db, "effect_instance", "durability_current",  "INTEGER");        -- NULL until derived
```

Both nullable at the column level: a pre-existing instance (imported before this module ships) has no
derived `max` yet, and NULL is the correct "not yet derived" state — never a fabricated `0` or a
fabricated full value. `DurabilityOf(instanceId)` derives and backfills `max`/`current = max` lazily
on first read for any instance whose columns are still NULL, mirroring `origin_values_json`'s own
lazy-write contract (`RpgStore.InstanceOps.cs:35-36` — *"written LAZILY at first mutation... an item
that is never mutated never pays for a second copy of its own numbers"*). Non-equipment instances
(materials, consumables, currency stacks — `DropEntryKind` per the loot-pack spec's own citation)
never populate these columns at all; only `EquipRefKinds.Rolled`-eligible base types (weapon/armour
roles) derive a non-null `max`.

### 2. Max derivation — a load-time table over closed VALIDATED fields

`class`/`rarity`/`tags` are already closed registry values on every base type (`seed-contract.md:70`,
confirmed above) — the same load-time-derivation shape `PackFootprintTable.Build` already uses for
footprint (`party-dungeon/spec-loot-pack.md:51` — *"computes `footprint(baseTypeId)` once at load
from the corpus's `role` and `tags[]`"*). `DurabilityTable.Build(baseTypeEntries, tuning)` is this
module's own equivalent: `max(class, rarity, tags) = baseByClass[class] × rarityMultiplierMilli /
1000`, a `checked`, widen-first, divide-last `long` computation, refusing at load (never defaulting)
on an unknown class or rarity id — the same refusal discipline `PackFootprintTable` uses for an
unknown role.

### 3. Battle wear (D3) — flat per-mille of `max`, once per battle, seeded from the battle

D3's own text is the constraint that decides the formula shape: *"only battle count matters, not room
count."* The ideal doc's aspirational input list (`roundsParticipated`, hit tallies) does not exist on
`BattleActorResult` (Real gap, above) — so this module reads only what is shipped:

```
wear = ceil(durability_max × wearPerBattleMilli(deploymentKind) / 1000)
current = max(0, current - wear)
```

Applied once, at battle settlement, for every `rpg_item_assignment`/`rpg_player_item_assignment` row
whose owner has a `BattleActorResult` in that battle — **regardless of `Survived`**: a downed
specimen's gear wore during the fight it was downed in, exactly as much as a survivor's. Idempotency
key: `(instanceId, BattleReport.Seed)` — a replayed settlement re-derives the identical decrement and
never double-wears, the same discipline `MutationOpKinds.StreamName` already documents (`MutationOp.cs
:175-180` — *"one per op kind, recorded even when the operation rolls nothing, so adding a roll later
never shifts another operation's sequence"*). No `DamageDealt`/`Kills` scaling in v1: richer
performance-scaled wear is a **tuning-only** follow-up once/if per-actor round tallies ship on
`BattleActorResult`, never an architecture change — recorded here so a future session does not
re-derive the same narrowing from scratch.

### 4. Death-drop extra decay — named interface, soft link only

At move-to-cache time (an event this module does not own — `corpse-cache`'s), one deterministic
one-shot chunk comes off `current` (never `max`): `extraDecay = ceil(current × deathDropDecayMilli /
1000) + deathDropDecayFlatFloor`, seeded on the corpse-decay sub-stream `corpse-cache` itself
generates, so the two modules never race two independent RNG draws over the same event. Because
`corpse-cache` does not exist yet, this module ships `DurabilityPolicy.ApplyDeathDropDecay(current,
max, tuning) : long` as a pure function with **no caller** — exactly `PartyPoolsCarry`'s own shipped
shape before `deploy-carry` wired it (`deploy-carry` spec, "Built" table: *"pure, unit-tested, zero
production callers"*). Whichever module lands second wires the call; neither module blocks on the
other's build order. See Interface exposed to dependents.

### 5. Repair — two tiers, one shared restore function

Both tiers resolve through the same pure `RepairPolicy.Resolve(current, max, materialCoverageMilli,
tierCapMilli, rng) : (newCurrent, destroyed)` — `tierCapMilli` is 1000 (full) for workbench-with-full-
material and a tunable partial cap for field touch-up or a short-workbench fallback (D2's "both,
graded by damage and material" collapses to one function with a cap parameter, not two code paths).

**Cost, siege-precedent shape (D5's shard leg folds in at the caller, not the formula):**

```
repairCost = soulsFee + forgeLeg × missingFraction × repairRatioMilli / 1000     // checked, long, ONE divide, last
missingFraction = (max - current) × 1000 / max                                   // per-mille, widened first
```

matching `StructurePolicy.RepairCost`'s exact shape (`StructurePolicy.cs:46`) — `repairRatioMilli`
bounded `[0, 1000]` at load, same as siege's `RepairCostRatioMilli` bound (`SiegeTuning.cs:194`), with
the siege 600‰ value (`siege.v1.json:30`) as the cross-domain precedent, not an inherited number — a
balance pass owns this module's own starting value.

**Destruction, every attempt:** before computing the restore, roll
`destructionChanceMilli(tier)` on the op's own named stream
(`SeededRng.DeriveStream(opSeed, MutationOpKinds.StreamName(MutationOpKind.Repair))`, the exact
`Enhance` idiom at `ItemWorkbench.cs:257-259`). On destruction: `AppendMutationOp` records the attempt
(`Result = MutationResult.Nothing("destroyed")`), `rpg_item.Disposition` is set to `"destroyed"`
(the closed vocabulary's existing fourth value, `RpgStore.ItemUniques.cs:73`) and the underlying
`effect_instance` is deleted through the same path a voluntary salvage already uses — **never** a
new deletion path. On success: `current` moves per §Design intro formula, `AppendMutationOp` records
`MutationResult` with the new value under `MutationOpKind.Repair`, replay-idempotent per
`correlation_id` exactly like `Enhance`.

**Field touch-up (D4):** executes wherever the party is (camp, mid-expedition) rather than through
`ItemWorkbench` (a `FusionRpg.Server` home-crafting class, `ItemWorkbench.cs:1-13`) — a field repair
needs its own call site, gated on a carried-tool-and-materials check this module defines as an
interface (`ICarriedSupplyCheck.Has(partyOrActorId, toolId, materialIds) : bool`) rather than a
concrete pack-grid read, because the pack grid does not exist yet (Real gap, above). `PackGrid`
landing later implements this interface; until then the field-repair call site has no live caller and
is exactly `PartyPoolsCarry`'s own "pure, zero production callers" starting shape. `tierCapMilli` for
field touch-up is a tunable below 1000 (partial/eroding, D2) regardless of material sufficiency —
field touch-up **cannot** reach full even with everything on hand, by design.

**Workbench repair:** extends `ItemWorkbench` with a `Repair` method following `Enhance`'s exact shape
(`ItemWorkbench.cs:240-269`): `TryResolve` (locked/disposition refusal) → `TryRecipe(recipeId,
CraftOperation.Repair, ...)` (the ask-first eleventh `CraftOperation` member) → named stream → policy
resolve → `AppendMutationOp`. Material coverage below what `missingFraction` needs falls back to the
same `tierCapMilli`-capped partial result field touch-up produces (D2's "never refuse outright") —
the **same** `RepairPolicy.Resolve` call, only `tierCapMilli` computed from actual material coverage
instead of a fixed partial constant.

**Shard leg (D5):** at low/mid rungs `operations.repair`'s cost legs are `souls` + `substrate` +
`catalyst.temper` only, matching `elevate`'s shape (`materials.v1.json:67-73`); at top rungs an
additional `shard` leg is required, matching `elevate`'s own already-shipped shard precedent
(`:71`) and `reroll-all`'s (`:89`) — reusing the identical rung-gated leg pattern, not a new one.

### 6. At-zero enforcement — a filter, not a new Hub gate

`MaterializeRolledEquipRuntime`'s `assignments` query (`RpgStore.Items.cs:840-842`) gains one
predicate: `&& (DurabilityOf(a.RefId)?.Current ?? 1) > 0`. A broken instance is then invisible to
`ApplyEquipProjection`/`ApplyEquippedGrants`, and the function's own already-shipped reconciliation
(`:846-859`, *"withdraw any source no longer backed by a current assignment"*) removes its grants
exactly as it already does for a genuinely unequipped item — **the same code path**, no new branch.
This satisfies "One ActorHub compose" (`AGENTS.md` Hard boundaries) by construction: no new
`IActorStatSubsystem`, no new composer, no Hub channel touched at all — durability gates *what reaches
the grant-materialize step*, never anything inside `ActorHub`. **Named honestly:** the exact class
that turns a materialized grant into a live combat-derived channel contribution was not re-verified
this session past `ApplyEquippedGrants`'s own call site; if a *second* reader independently walks
`rpg_item_assignment` for combat contribution (bypassing `MaterializeRolledEquipRuntime`), that reader
needs the identical filter and is implementation's first task to confirm, the same honesty
`spec-deploy-carry.md` used for its own unlocated setup-builder call site.

### 7. Commander-pouch parity (D6)

Every function above (`DurabilityTable.Build`, battle wear, `RepairPolicy.Resolve`, destruction) takes
an instance id and never reads `rpg_item_assignment` directly — the caller supplies the assignment
scope. A second wear-application call site iterates `rpg_player_item_assignment` instead, and
`ItemWorkbench.Repair` accepts either scope's resolved instance the same way `TryResolve` already
does. What does **not** get built here: a commander-pouch grants-materialize pipeline
(`MaterializeRolledEquipRuntime`'s equivalent) — none exists yet (Real gap, above), so §6's at-zero
filter has no commander-side call site to attach to until that pipeline lands; commander gear still
wears, still reaches 0, still refuses further wear past 0 — only the "stops contributing" enforcement
is unique-scope-only until the commander pipeline exists, named so it is not silently assumed parallel.

## Tunables

Every number this introduces, and which `data/tuning/` file owns it. No `const` balance numbers
(`tunables-ssot.md`).

| Number | Owner | Notes |
|---|---|---|
| Durability max derivation: `baseByClass[class]`, `rarityMultiplierMilli[rarity]` | `data/tuning/deployment-hierarchy.v1.json` (new domain file — **this module creates it if it lands first**; `injury-tiers`/`corpse-cache` add their own keys alongside, never a second file) | DERIVED inputs (`class`/`rarity`) are already closed; only the coefficients are new |
| `wearPerBattleMilli` by deployment kind (lawn/delve/siege/expedition) | same file | D3: flat per-mille of `max`, once per battle, never per room |
| `deathDropDecayMilli` + flat floor | same file | Stacks with battle wear; never touches `max`; consumed by `corpse-cache` once it exists (soft link, Design §4) |
| `repairRatioMilli` (bounded 0..1000) | `data/tuning/materials.v1.json` `operations.repair` leg (new, eleventh row) | Siege 600‰ precedent (`siege.v1.json:30`), **not** inherited — this module picks its own starting value; PS-8 bounded-ratio exemption comment required at the tuning file, matching `materials.v1.json:15`'s own `capNote` idiom |
| `operations.repair.souls`/`.substrate`/`.catalyst` legs, scaled per `variable` class (rung/grade) | `data/tuning/materials.v1.json` `operations.repair` | Same shape as the existing ten rows (`:28-93`) |
| Shard-leg quantity at top rungs (D5) | `data/tuning/materials.v1.json` `operations.repair.shard` | Matches `elevate`'s (`:71`) and `reroll-all`'s (`:89`) already-shipped rung-gated shard legs |
| `tierCapMilli` for field touch-up (D2, always partial) | same file | Bounded ratio, < 1000 by construction (never reaches full via field repair alone) |
| Destruction-failure chance ‰ — **field and workbench tiers separately tunable** (D1) | `data/tuning/materials.v1.json` `operations.repair` leg | Map's own "Open items" row: *"the exact destruction-failure-chance split between the field and workbench tiers (a balance number, not an architecture one)"* — this spec deliberately does not pick the split, only names it as two keys |
| Structural (commented, not tuned): `MutationLimits.MutationSeqCap` (4096, shared with every op kind incl. Repair), `effect_instance_op` PK shape | code `const` with exemption comments already in place (`MutationOp.cs:69-78`) | Nothing new — Repair reuses the existing ledger's existing structural bound |

## Numeric types

`durability_max`/`durability_current` are `long` (SQLite `INTEGER`, C# `long`) — a magnitude, never
`float`/`double` per `CLAUDE.md` "Numeric overflow": a `float` magnitude stops being integer-exact at
`Θ=232`, inside normal play, and durability is exactly the kind of per-instance magnitude that table
governs. `wearPerBattleMilli`/`deathDropDecayMilli`/`repairRatioMilli`/destruction-chance are `long`
per-mille values (CLAUDE.md rule 4: divide by 1000 exactly once, last). `missingFraction` and every
intermediate in the repair-cost formula are `checked` `long`, widened before multiplying (CLAUDE.md
rule 3 — `(long)a * b`, never `(long)(a * b)`), matching `StructurePolicy.RepairCost`'s own proven
shape verbatim. `MutationOpKind`/`CraftOperation` additions are `int`-backed enums (existing shape,
unchanged). No new `double`, no `System.Random` — every roll goes through `SeededRng.DeriveStream` on
a named stream, matching `MutationOpKinds.StreamName`'s existing per-op-kind domain separation.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.Durability"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.Mutation"   # op_kind/CraftOperation additions don't regress the existing ten
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Items"
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~Workbench"       # Repair alongside Enhance
.\scripts\guard-dal.ps1                # every new SQL string stays in FusionRpg.Data
.\scripts\guard-actor-hub.ps1          # no new composer — §Design 6 is a filter, not a Hub contribution
python scripts\audit-magic-numbers.py --targets M1   # DurabilityTable/RepairPolicy are Rules-shaped: no bare literals
python scripts\audit-overflow.py       # durability is a `long` magnitude path
```

## Structure

```
src/FusionRpg.Core/Items/Durability/
  DurabilityTable.cs      (Build at load from class/rarity — refusals on unknown id, mirrors PackFootprintTable.Build)
  DurabilityPolicy.cs      (BattleWear, ApplyDeathDropDecay — pure, integer-only)
  RepairPolicy.cs          (Resolve — pure: (current, max, coverage, cap, rng) -> (newCurrent, destroyed))
  ICarriedSupplyCheck.cs   (interface only; PackGrid implements it once loot-pack ships)
src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs   gains durability_max/durability_current columns,
                                                     DurabilityOf(instanceId) lazy-derive-and-read
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs         MaterializeRolledEquipRuntime (:840-842) gains the
                                                     one durability>0 predicate; commander-pouch wear
                                                     application call site (new, §Design 7)
src/FusionRpg.Server/ItemWorkbench.cs               gains Repair(...) alongside Enhance(...)
src/FusionRpg.Server/<field-repair endpoint>         exact file named once ICarriedSupplyCheck has a
                                                     real implementation to call — not pinpointed this
                                                     session, implementation's first task (matching
                                                     deploy-carry's own unresolved-call-site precedent)
data/tuning/deployment-hierarchy.v1.json            NEW if not already created by an earlier-landing
                                                     sibling module — wearPerBattleMilli, durability max
                                                     coefficients, deathDropDecayMilli+floor
data/tuning/materials.v1.json                        operations.repair (new, eleventh row)
tests/FusionRpg.Core.Tests/Items/Durability/         new
tests/FusionRpg.Data.Tests/Items/                    gains durability column + MaterializeRolledEquipRuntime
                                                     filter regression tests
tests/FusionRpg.Server.Tests/Workbench/              gains Repair tests alongside existing Enhance tests
UNTOUCHED: ActorHub.cs, StatusRuntime.cs, EffectBag.cs, Funnel, EntityStatWriter — zero Unity write
           surface, zero Hub subsystem, zero status catalog change
```

## Code style

Pure functions over records, `checked` widen-first/divide-last arithmetic, refuse-at-load discipline —
matching `StructurePolicy.RepairCost` and `PackFootprintTable.Build` exactly, not a new idiom.

```csharp
// The repair-cost formula, StructurePolicy.RepairCost's exact shape reused for a rolled instance
// instead of a world structure. ONE divide by max, THEN one divide by 1000 — never combined.
public static long RepairCost(long soulsFee, long forgeLegPerUnit, long max, long current, long repairRatioMilli)
{
    if (max <= 0) return 0;                       // no durability derived yet: nothing to price
    var missing = max - Math.Max(0, current);
    if (missing <= 0) return 0;
    var missingFraction = checked(missing * 1000 / max);                       // per-mille, ONE divide
    return checked(soulsFee + forgeLegPerUnit * missingFraction * repairRatioMilli / 1000); // ONE divide, last
}
```

## Testing strategy

- **Property — wear is idempotent per (instance, battle):** replaying a battle's settlement twice
  never decrements `current` twice; the wear amount is a pure function of `(durability_max,
  wearPerBattleMilli, deploymentKind)` and does not vary with `DamageDealt`/`Kills` in v1.
- **Zero is unusable, never destroyed by wear:** a run of battles that would take `current` below 0
  clamps at 0 and stops decrementing further — wear alone never flips `Disposition`.
- **At-zero enforcement reuses the existing withdraw path:** an instance at `durability_current == 0`
  is absent from `MaterializeRolledEquipRuntime`'s next `assignments` read, and its previously-granted
  sources are withdrawn through the *same* `:846-859` reconciliation a genuine unequip already
  exercises — asserted by inspecting the withdrawn-source list, not just the grant count.
- **Field touch-up refuses without carried supplies:** `ICarriedSupplyCheck.Has` returning false
  refuses the repair naming the missing tool/material, never a silent no-op.
- **Field touch-up never reaches full:** even with every material present, `tierCapMilli` bounds the
  restore below `max`.
- **Workbench falls back to partial, never refuses outright:** short material resolves at the same
  capped shape field touch-up produces, with a distinguishable reason code, never a hard refusal.
- **Destruction is possible on every attempt, at a tier-specific rate:** a seeded sweep across both
  tiers confirms both a success branch and a destruction branch are reachable, and a destroyed
  instance's `Disposition` becomes `"destroyed"` through the existing four-value vocabulary — never a
  fifth value invented for this module.
- **Repair never raises `max`:** every repair-success case asserts `max` unchanged; only `current`
  moves.
- **Commander-pouch parity:** the same `DurabilityTable`/`RepairPolicy`/wear functions, called against
  a `rpg_player_item_assignment`-sourced instance, produce byte-identical numeric results to the
  unique-scope path for the same inputs — proving "same mechanism" rather than asserting it.
- **`op_kind`/`CraftOperation` additions don't regress the existing ten:** `MutationOpKinds.AllIds`
  and `CraftOperations`' own id list each grow by exactly one, existing ids unchanged, existing
  `TryParse` round-trips unaffected.
- **`the_repair_path_never_writes_hp`:** a source scan over `Items/Durability/` and the Repair method
  for `EntityStatWriter`/`Funnel`/`DamagePacket` references — durability is a Data-layer property and
  must never touch the combat-write surface (mirrors loot-pack's own `the_pack_never_reads_armoury_
  capacity` idiom).

## Boundaries

- **Always:** derive `max` at load from `class`/`rarity`, refusing an unknown id by name; decrement
  `current` exactly once per `(instanceId, BattleReport.Seed)`; clamp at 0, never below; roll
  destruction on every repair attempt, both tiers; append every repair (success or destruction)
  through `AppendMutationOp` under the ask-first `Repair` `op_kind`; keep the commander-pouch path on
  the identical pure functions as the unique path.
- **Ask first:** the `Repair` member on `MutationOpKind` (item program); the `Repair` member on
  `CraftOperation` (item program, a **separate** ask from the one above); the `operations.repair` row
  in `materials.v1.json` (same reviewed-add discipline every existing row got); the `CloseDelve`
  hook-order slot for death-drop-extra-decay once `corpse-cache` exists (that module's own ask, this
  module only implements its side of the interface); the exact field-repair endpoint file (not
  pinpointed this session).
- **Never:** a stat-decay curve on non-zero durability (D1: full power until 0, explicitly no
  gradual decay); a hard destroy-on-zero (zero is unusable, not deleted); a second `op_kind`-shaped
  enum invented instead of extending the closed one; a new `IActorStatSubsystem`/Hub composer for
  at-zero enforcement (§Design 6 is a filter on an existing query, nothing more); wear scaled per
  delve room; a wall-clock repair cooldown (every clock in this program is settlement-counted or
  turn-counted, never wall time, per the ideal doc's own R6 precedent); repair raising `max`;
  per-item authored durability (max is DERIVED, never authored, matching every sibling DERIVED field).

## Success criteria

1. `durability_max`/`durability_current` populate lazily on first read for any rolled instance, `long`,
   `checked` throughout. 2. A ten-battle unrepaired item reaches 0 and is excluded from its owner's
   next materialize, with `max` unchanged. 3. A field touch-up with tool+materials present restores
   below `max`; without them it refuses naming the gap. 4. A workbench repair with full material
   restores to `max`; short material falls back to the same capped partial a field touch-up produces.
   5. Every repair attempt can destroy the item at its tier's tunable rate; a destroyed item's
   `Disposition` is `"destroyed"`. 6. Commander-pouch gear produces byte-identical wear/repair numbers
   to unique gear for the same inputs. 7. `guard-actor-hub`/`guard-dal`/`audit-overflow` green; zero
   new `IActorStatSubsystem`, zero new SQL outside `FusionRpg.Data`, zero Unity write surface touched.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `DurabilityPolicy.ApplyDeathDropDecay(current, max, tuning) : long` — pure, no caller yet | `corpse-cache` (module 3) wires the call at its own move-to-cache event once it exists; this module never calls it itself (Design §4) |
| `ICarriedSupplyCheck.Has(partyOrActorId, toolId, materialIds) : bool` — interface only | `cache-field-access` / whichever module implements `loot-pack`'s `PackGrid` wires a real implementation; field touch-up has no live caller until then |
| `DurabilityOf(instanceId) : (long? Max, long? Current)`, `RepairPolicy.Resolve(...)` | `item` program — if the item program's own surfaces (item card, armoury listing) want to *display* durability, these are the read/write seams to call, not a second derivation |
| The `MaterializeRolledEquipRuntime` filter shape (§Design 6) | Any future second reader of `rpg_item_assignment` for combat contribution must apply the identical `durability_current > 0` predicate — named so a parallel reader does not silently skip it |

## Design-gate checklist

```
[x] Subsystems: item durable-ownership/mutation ledger (Data), item workbench (Server), battle
    settlement read (Core, read-only) — no Status/World/ActorHub subsystem touched by this module.
[x] Read this session: deployment-hierarchy-ideal.md §"Item durability, wear, and repair" (D1-D6) and
    §"Resolved 2026-09-13 (third clearing round)" items 6-11 in full; deployment-hierarchy-map.md row
    7, its Build order/Gates/External-dependencies sections; decisions.md "Deployment hierarchy SSOT
    (2026-09-13)" and "Status SSOT" rows (to confirm wound.*/P1 does not apply here); spec-deploy-carry.md
    in full, as the house-style/format template alongside spec-loot-pack.md.
[x] Code cited by file:line, opened this session: item/seed-contract.md (:69-70, :84, :88-91, :231),
    item/defect-register.md (:194), DelvePrices.cs (:15), MutationOp.cs (:9-11, :13-48, :69-78, :88,
    :175-180), CostClassMatrix.cs (:1-44), RpgStore.InstanceOps.cs (:29-37, :42-66, :91-99),
    RpgStore.Items.cs (:152-171, :716-756, :838-875), ItemWorkbench.cs (:1-13, :240-269, :490-508),
    RpgStore.ItemUniques.cs (:71-73), BattleModels.cs (:509-512, :634, :637), StructurePolicy.cs
    (:33-47), SiegeTuning.cs (:192-194), siege.v1.json (:30), materials.v1.json (:28-93), RpgStore.cs
    (:3893-3897), AtomDerivedSubsystem.cs (skimmed to confirm it is not the item-grant reader).
[x] Drift reported: the task brief named one closed namespace ("op_kind"); code shows **two** separate
    closed-at-ten enums (`MutationOpKind` and `CraftOperation`) both needing a reviewed add — folded
    into Real gap and the Boundaries "Ask first" list as two distinct asks. `roundsParticipated`, named
    in the ideal doc as a wear input "where available", is not present on `BattleActorResult` —
    narrowed the wear formula to a flat per-battle rate rather than silently assuming a richer input
    exists. `PackGrid`/carried-inventory for D4 is not a listed module dependency in the map, yet D4's
    own text requires it — resolved via an interface seam (`ICarriedSupplyCheck`) rather than a false
    claim of an existing carry mechanism.
[ ] The exact reader that turns a materialized grant into a live ActorHub combat-derived channel
    contribution (downstream of `ApplyEquippedGrants`) was not traced end-to-end this session — only
    `MaterializeRolledEquipRuntime`'s own assignment-read seam was verified. If a second, independent
    reader of `rpg_item_assignment` exists for combat contribution, it needs the same durability filter
    and locating it is implementation's first task (Design §6, named honestly rather than assumed).
[ ] The exact field-repair endpoint/call site (where a live delve or expedition session would invoke
    field touch-up) was not pinpointed this session — it has no caller today because `ICarriedSupplyCheck`
    has no real implementation yet (`loot-pack` unbuilt); naming the precise file is implementation's task.
[x] No §2 invariant contradicted: `long` for every magnitude (no `float`), widen-before-multiply /
    divide-by-1000-last throughout, no hard progression ceiling (destruction is a chance, not a wall;
    `repairRatioMilli` is a bounded ratio with its PS-8 exemption comment owed at the tuning file), SQL
    only in `FusionRpg.Data`, no second ActorHub composer, Foundation/PvZ untouched, no bare literal on
    a balance surface (every number named lives in `data/tuning/`).
[x] ActorHub gate: this module contributes to and reads from no Hub channel at all — durability is a
    Data-layer item property; §Design 6's enforcement is a filter on an existing SQL-backed query, not
    a Hub subsystem, matching `guard-actor-hub.ps1`'s allowlist by construction (nothing new to allow).
[x] SOLID: extends two existing closed gates (`op_kind`, `CraftOperation`) by the same ask-first
    process every prior member used — never a private parallel repair-verb namespace; reuses
    `MaterializeRolledEquipRuntime`'s existing withdraw path rather than forking a second grants
    materializer for "broken" items.
```
