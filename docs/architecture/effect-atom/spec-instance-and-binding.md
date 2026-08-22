# Spec: instance-and-binding (E6)

Module **E6** in the [atom effect map](../effect-atom-map.md). Depends on **E5**. Schema plus the bind-time gate; nothing in the game changes yet.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Turn a container *template* into a **specific owned thing** with its rolls frozen, and attach that thing to an **owner**. This is roll moment 2 (instantiate) and moment 3 (bind) from E2 — and moment 3 deliberately rolls nothing.

## Design (locked on approval)

### `effect_instance` / `effect_instance_atom`

| `effect_instance` | Notes | | `effect_instance_atom` | Notes |
|---|---|---|---|---|
| `instance_id` TEXT PK | | | `instance_id` TEXT FK | |
| `container_id` TEXT FK | | | `atom_id` TEXT FK | |
| `roll_seed` INT | replays the drop exactly | | `seq` INT | resolve order, copied at instantiate |
| `created_utc` TEXT | | | `values_json` TEXT | **frozen** `OnInstantiate` results |
| `origin` TEXT | drop \| craft \| grant \| migration | | `power_json` TEXT | **nullable** — E9 owns power and lands later; E9 backfills |

**Instantiation** = draw the pool (one atom per `group`, `pool_rolls` times, weighted), append the fixed core, resolve every `OnInstantiate` value spec from `roll_seed`, and stamp power. `Fixed` values are copied. `OnApply` values are **left unresolved** — they belong to the hit, not the item.

Re-running instantiation with the same `(container_id, catalog_revision, roll_seed)` must reproduce the instance byte-identically — **excluding `instance_id` and `created_utc`**, which are generated. The comparison is over the atom set, `values_json`, and `power_json`. Without that exclusion the test could never pass.

**`seq` for drawn atoms:** fixed-core atoms keep their authored `seq`; drawn atoms are appended **after** the core in draw order, continuing the numbering. The deterministic part comes first.

### `effect_binding`

Replaces the logical `foundation_effect_grant` and absorbs today's grant blobs out of `rpg_unique_stat_mods.mods_json`.

| Column | Notes |
|---|---|
| `binding_id` TEXT PK | |
| `instance_id` TEXT FK | |
| `owner_kind` / `owner_key` | see scopes below |
| `slot` | for items |
| `priority` | INT, default 0 — **the primary sort key of the actor effect list** (`priority DESC, binding_id ASC`). The one execution-order guarantee in the program needs a column, not just a sentence |
| `priority` | INT, default 0 — **the primary sort key of the actor effect list** (`priority DESC, binding_id ASC`). The one execution-order guarantee in the program needs a column, not just a sentence |
| `source` | plugin or feature id, for withdraw |
| `bound_utc`, `revision` | |

### Owner-key scopes — 7

`match` · `plant:{typeId}` · `zombie:{typeId}` · `entity:{ptr}` · `player:{id}` · **`sector:{id}`** · **`slot:{id}`**

The last two are new (owner decision 2026-08-22). They exist because the world map is real — `rpg_world_sectors`, `rpg_world_slots`, `SlotTypeCatalog`, `MarchResolver` are all in the tree — and two of its five needs (a building that fortifies a sector, a sector's ambient environment) are **only** blocked by scope. The kinds already fit.

**Not added:** `OnWorldTick`, `OnSectorEnter`, `OnBuildComplete`. Triggers stay at 7 until a world spec asks, because those need the world clock and lifecycle settled and that is the world stream's call.

### `mods_json` — what moves and what stays

`rpg_unique_stat_mods.mods_json` currently holds `{ absolutes, grants }` in one blob per instance.

- **`grants` move** into `effect_binding`, one row each. That is the whole point.
- **`absolutes` stay where they are.** They are Tab B/C `Override` writes on a hand-built channel map, not effect grants, and effects cannot emit `Override` at all (E1). Moving them would smuggle a fourth write path into this program.

Migration is one-way and idempotent: re-running it on an already-migrated instance is a no-op.

### The bind-time gate — where runtime and scope legality are enforced

Load-time validation (E4/E5) proves a row is *well-formed*. Bind time proves it is *executable here*. Both reject; neither ignores.

| Check | Reason code |
|---|---|
| every atom's kind supports the target runtime (four states — `PlanOnly` accepted only by a planner host) | `RuntimeUnsupported` |
| `stat.modify` on `defense` at **any** scope other than `match` | `ScopeUnsupported` — **G8**: the prefix reads **one side-wide cached value**, so `plant:N` and `zombie:N` are as broken as `entity:`. The earlier rule rejected only `entity:` and left the per-type cases silently dead. Per-actor mitigation is `stat.derived` on `combat.defense.*`; per-entity primary defense waits for perf **O5** |
| `level_req` set and the owner's level is lower | `LevelTooLow` |
| `sector:` / `slot:` scope in a runtime with no world host | `ScopeUnsupported` |
| owner key malformed for its kind | `BadOwnerKey` |
| instance references a withdrawn or disabled atom | `StaleInstance` |

The same container may bind on the lawn and be rejected in battle. That is correct and expected — battle consumes one opcode today, and the matrix (E1) is a living audited table.

### Runtime state stays in RAM

ICD clocks, stacks, counters, charges, and status instances live in **session memory**, exactly as they do now. **No new durable runtime table.** The old `foundation_effect_runtime` sketch is not built: `entity:{ptr}` grants are meaningless across a process restart, and per-match counters are E15's job.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceStore|BindingStore"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs      (new — instance + binding DDL/IO)
src/FusionRpg.Core/Effects/Atoms/Instantiator.cs         (new — pool draw, freeze; power stamped later by E9)
src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs           (new — the 7 scopes, parse + validate)
src/FusionRpg.Core/Effects/Atoms/BindGate.cs             (new — the rejection table above)
tests/FusionRpg.Data.Tests/AtomInstanceStoreTests.cs
tests/FusionRpg.Core.Tests/Atoms/InstantiatorTests.cs
tests/FusionRpg.Core.Tests/Atoms/BindGateTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Same `(container, revision, seed)` instantiated twice | byte-identical instance. **Power is null here** — E9 lands nine positions later and backfills it |
| Different seeds over 10⁴ draws | **exact expected counts** for the fixed seed sequence; `weight = 0` never drawn. A tolerance on a seeded test is an invitation to widen it |
| Pool with two atoms in one `group` | at most one appears |
| `OnApply` value at instantiate | **left unresolved** — asserted, not assumed |
| `Fixed` value | copied verbatim |
| Bind `stat.modify`/`defense` to `entity:abc` | rejected `ScopeUnsupported` (G8) |
| Bind the same to `plant:7` | rejected `ScopeUnsupported` — per-type is as dead as per-entity |
| Owner key `entity:0xABC` | rejected `BadOwnerKey` before G8 is reached — lowercase hex, no `0x` |
| Bind a board-kind atom in battle | rejected `RuntimeUnsupported` |
| Bind `sector:` with no world host | rejected `ScopeUnsupported` |
| `mods_json` migration | grants become bindings; **absolutes untouched**; re-run is a no-op |
| Instance referencing a disabled atom | `StaleInstance` at bind, not a silent skip |
| Process restart | no durable runtime rows exist to reload |

## Boundaries

**Always:** freeze `OnInstantiate` at instantiate and never later; store `roll_seed`; reject at bind rather than no-op; keep runtime state in RAM; treat `entity:` bindings as **session-scoped and never durable** — a pointer can be recycled, and a durable row aimed at a recycled address silently retargets.

**Ask first:** adding an owner scope; adding a trigger for the world; moving `absolutes`.

**Never:** roll anything at bind time; create a durable runtime-state table; let a rejected bind degrade into a partial bind; write SQL outside `FusionRpg.Data`.
