# Foundation Effect data tables

Logical schema for Foundation Effect catalog and grants. Secondary writes **grants + typed overlays** only — never parallel Unity write tables.

Parent: [effect-system.md](effect-system.md). Runtime: [effect-runtime.md](effect-runtime.md). Funnel / FA10 v2: [effect-funnel.md](effect-funnel.md). Target + delivery SSOT: [combat-damage-ssot.md](combat-damage-ssot.md).

---

## Split: def skeleton vs grant overlay

| Layer | Owns | Must not own |
|---|---|---|
| **`foundation_effect_def` + trigger + action** | Opcode skeleton (type, FT*, FA*, coarse enums) | chance, icd, typeId, Flat/Inc/More amounts |
| **`foundation_effect_grant.overlay_json`** | Secondary: chance, icd_ms, amounts, typeId, filters, duration | Direct game apply |
| **`foundation_effect_runtime`** | Session ICD clocks / stacks; **legacy** Counter/DoT columns until StatusRuntime ships | SSOT content |

```text
EffectCatalog.Get(effect_id)     → Def + Triggers + Actions
EffectGrantIndex.ForOwner(owner) → grants (merge overlay at execute)
```

Unknown overlay keys for the FA* being executed → **reject** (log + skip action).

---

## Tables

### `foundation_effect_def`

| Column | Type | Notes |
|---|---|---|
| `effect_id` | TEXT PK | Stable id (`fe.damage.butter`, …) |
| `effect_type` | TEXT | `Passive` \| `Triggered` |
| `name` | TEXT | Display |
| `enabled` | INT | 0/1 |
| `source_tag` | TEXT | ModifierBag / Intent source prefix (`effect:{id}`) |

**No** `chance`, `icd_ms`, or magnitude columns.

### `foundation_effect_trigger`

| Column | Type | Notes |
|---|---|---|
| `effect_id` | TEXT FK | |
| `ord` | INT | Optional order if multiple |
| `trigger` | TEXT | `OnSpawn` \| `OnDamageDealt` \| `OnDamageTaken` \| `OnDeath` \| `OnGranted` \| `OnRemoved` \| `OnTimer` |

`OnTimer` — periodic DoT/aura ticks ([combat-damage-ssot.md](combat-damage-ssot.md)). Injector hot-loop ms scheduler; not Server-driven.

### `foundation_effect_action`

| Column | Type | Notes |
|---|---|---|
| `effect_id` | TEXT FK | |
| `seq` | INT | Execution order |
| `action` | TEXT | FA1–FA9 opcode (v1). FA10 `ApplyResourceDelta` is v2 spec — [effect-funnel.md](effect-funnel.md) |
| `params_json` | TEXT | Coarse enums only — see schemas below |

### `foundation_effect_grant`

| Column | Type | Notes |
|---|---|---|
| `grant_id` | TEXT PK | |
| `effect_id` | TEXT FK | |
| `owner_kind` | TEXT | `match` \| `plant` \| `zombie` \| `player` \| `loadout` |
| `owner_key` | TEXT | ptr / typeId / player_id / `*` |
| `plugin_id` | TEXT | Secondary feature id (source tag) |
| `priority` | INT | Resolve order |
| `revision` | INT | Cache bust |
| `overlay_json` | TEXT | **Typed** Secondary payload |

### `foundation_effect_runtime`

Session-scoped proc gates and **legacy** delivery meters. When [StatusRuntime](status-ssot.md) ships, Counter/DoT/tick columns move to L2 RAM; grant ICD + `max_stacks` stay here or on grant policy as today.

| Column | Type | Notes |
|---|---|---|
| `grant_id` | TEXT | |
| `effect_id` | TEXT | |
| `match_key` | TEXT | |
| `last_fire_utc` | TEXT | Grant ICD |
| `stacks` | INT | Grant stack cap tracking (`max_stacks`) |
| `hit_counter` | INT | **Legacy** Counter meter — migrates to StatusRuntime |
| `counter_scope_key` | TEXT | **Legacy** Target/Actor meter key |
| `last_tick_ms` | INT | **Legacy** DoT alignment |
| `dot_budget_spent` | INT | **Legacy** B-DOT-BUDGET |

Active **status instances** (duration, stacks on actor, contagion hops) are **not** rows in this table — they live on StatusRuntime per [status-ssot.md](status-ssot.md).

Rich side/type filters on **grant overlay** `filters` (event matching) stay separate from **`target.filters`** (damage recipient pool) — see [combat-damage-ssot.md](combat-damage-ssot.md).

---

## Action `params_json` (Foundation — coarse)

| Action | Allowed keys | Example |
|---|---|---|
| `ModifyStat` | `channel` | `{"channel":"atk"}` |
| `ApplyStatus` | `status` | `{"status":"butter"}` |
| `ClearStatus` | `status?` | `{}` or `{"status":"butter"}` |
| `SpawnEntity` | `kind` | `{"kind":"zombie"}` |
| `BoardAction` | `op` | `{"op":"cherry"}` |
| `SpawnGridItem` | `gridItemType` | `{"gridItemType":"Grave"}` |
| `ClearGridItem` | — | `{}` |
| `SetBoxType` | `boxType` | `{"boxType":"Dirt"}` |
| `Economy` | `currency`, `op` | `{"currency":"sun","op":"add"}` |
| `ApplyResourceDelta` (FA10, v2 spec) | `channel` | `{"channel":"hp"}` — overlay `amount` signed; **add only**. Sun/money stay FA9. See [effect-funnel.md](effect-funnel.md) |

### Enums

| Field | Values |
|---|---|
| `channel` | `hp`, `maxHp`, `atk`, `def`, `attackInterval`, `produceInterval`, `zombieSpeed`, `bulletDamage`, `boardPressure` |
| `status` | `butter`, `freeze`, `cold`, `poison`, `floatSlow` |
| `kind` | `plant`, `zombie`, `bullet` |
| `op` (board) | `freeze`, `doom`, `fireline`, `cherry` |
| `op` (economy) | `set`, `add` |
| `op` (resource delta, v2) | **`add` only**; `channel` **hp only**; Funnel Guard rejects `set` |
| `currency` | `sun`, `money`, `points` |
| `boxType` | `Grass`, `Water`, `Dirt`, `Lava`, … (game BoxType names) |
| `gridItemType` | `Grave`, `IceBlock`, … |
| `target.mode` | `EventTarget`, `Actor`, `Selected`, `Single`, `Multi`, `Random`, `Area`, `All` — [combat-damage-ssot.md](combat-damage-ssot.md) |
| `target.shape` | `Row`, `Column`, `Square`, `Rectangle` (Area mode only) |
| `delivery.mode` | `Instant`, `OverTime`, `Counter` |
| `delivery.counterScope` | `Target`, `Actor` |
| `anchorOrigin` | `Corner`, `Center` (Rectangle / Square) |

---

## Grant `overlay_json` (Secondary — typed)

### Common (all grants)

| Key | Type | Notes |
|---|---|---|
| `chance` | number 0–1 | Default 1 if omitted |
| `icd_ms` | int | Required for damage triggers; **default 250** if omitted on FT2/FT3 |
| `max_stacks` | int | Optional |
| `filters` | object | See filters |

### `filters`

| Key | Type | Notes |
|---|---|---|
| `side` | `plant` \| `zombie` \| `bullet` | OnSpawn / OnDeath |
| `typeId` | int | Entity type filter |
| `actorIsKiller` | bool | OnDeath when event carries killer |

Event-matching filters only. For **damage recipient** filters use `target.filters` — [combat-damage-ssot.md](combat-damage-ssot.md).

### `target` (ApplyResourceDelta / damage grants)

Nested object on overlay. See [combat-damage-ssot.md](combat-damage-ssot.md) and [examples/combat/](examples/combat/).

| Key | Type | Notes |
|---|---|---|
| `mode` | string | `EventTarget`, `Actor`, `Selected`, `Single`, `Multi`, `Random`, `Area`, `All` |
| `ptr` | string | Required for `Single`; debug `Selected` uses cheat state |
| `count` | int | `Multi` / `Random` |
| `shape` | string | `Row`, `Column`, `Square`, `Rectangle` when `mode=Area` |
| `size` | int | Square N×N; default from policy when omitted |
| `width`, `height` | int | Rectangle; default from policy when omitted |
| `anchor` | string or object | `EventTarget` or `{ "row", "col" }` |
| `anchorOrigin` | string | `Corner` \| `Center` |
| `filters` | object | Recipient pool — `side`, `typeId`, `typeIdIn`, `excludeMindControlled`, `row`, `col` |
| `maxTargets` | int | Default 8 |

### `delivery`

| Key | Type | Notes |
|---|---|---|
| `mode` | string | **`Instant`** forward. `OverTime`, `Counter` = **legacy** until [status-ssot.md](status-ssot.md) ships |
| `periodMs`, `durationMs` | int | Legacy OverTime |
| `tickBudget` | int | Legacy OverTime — B-DOT-BUDGET |
| `everyHits`, `resetOnBurst` | int / bool | Legacy Counter |
| `counterScope` | string | Legacy `Target` \| `Actor` |

### `statusId` and status overlay (forward)

When StatusRuntime ships, timed/counter grants reference a catalog id instead of `delivery.mode = OverTime|Counter`:

| Key | Type | Notes |
|---|---|---|
| `statusId` | string | One of 21 locked ids — [status-ssot.md](status-ssot.md) §9; L2b category — §9.5 |
| `chance` | number 0–1 | L1 proc gate; default **1** if omitted. L2b combine: `p_final = chance × p_apply` — [status-ssot.md §6](status-ssot.md), [actor-hub-ssot.md](actor-hub-ssot.md) |
| `periodMs`, `durationMs`, `tickBudget` | int | OverTime overlay (wither, blight, …) |
| `amount` | signed int | PulseHp magnitude per tick or instant |
| `everyHits`, `burst`, `counterScope` | | Counter overlay (bond) |
| `stat` | object | ModifyStat overlay (rally, expose) |
| `spread` | object | Contagion — `chance`, `icd_ms`, `maxHops`, `statusId`, `target` (TargetSpec) |

Examples: [examples/status/](examples/status/).

### `burst` (Counter mode)

Nested Instant sub-packet: `amount`, `target`, `delivery: { "mode": "Instant" }`.

Optional grant keys: `procDepthLimit` (override match policy), `chainDepth` (runtime — usually not in content).

### Per-action overlay keys

| Action | Extra keys |
|---|---|
| `ModifyStat` | `flat`, `increased`, `more` (numbers; StatSystem ops) |
| `ApplyStatus` | `duration`, `level` |
| `ClearStatus` | `target` (selector string) |
| `SpawnEntity` | `typeId`, `hp`, `maxHp`, `atk`, `mindControlled`, `row`, `col`, `x` |
| `BoardAction` | `row`, `col`, `x`, `y` |
| `SpawnGridItem` | `row`, `col` |
| `ClearGridItem` | `selector` (e.g. `randomGrave`) |
| `SetBoxType` | `cells` `[{row,col},…]` |
| `Economy` | `amount`, `capPerMatch` |
| `ApplyResourceDelta` | `amount` (signed), `target`, `delivery`, `burst?`. No `absoluteHp` / `setHp`. Heal = positive amount |

Any other key → reject for that action execute.

---

## Example rows

### A. OnDamageDealt → butter (Triggered)

**def** `fe.damage.butter` · `Triggered` · source_tag `effect:fe.damage.butter`

| trigger | |
|---|---|
| `OnDamageDealt` | |

| seq | action | params_json |
|---|---|---|
| 0 | `ApplyStatus` | `{"status":"butter"}` |

**grant** (Secondary plugin `sec.lucky.butter`):

```json
{
  "chance": 0.2,
  "icd_ms": 500,
  "duration": 8,
  "filters": { "side": "zombie" }
}
```

### B. OnDeath → spawn zombie (Triggered)

**def** `fe.death.spawn_zombie` · `Triggered`

| trigger | |
|---|---|
| `OnDeath` | |

| seq | action | params_json |
|---|---|---|
| 0 | `SpawnEntity` | `{"kind":"zombie"}` |

**grant**:

```json
{
  "chance": 1,
  "icd_ms": 0,
  "typeId": 0,
  "hp": 8000,
  "maxHp": 8000,
  "mindControlled": false,
  "row": 2,
  "filters": { "side": "plant", "typeId": 0 }
}
```

### C. Passive HP/ATK on grant

**def** `fe.passive.power` · `Passive`

| trigger | |
|---|---|
| `OnGranted` | |
| `OnRemoved` | |

| seq | action | params_json |
|---|---|---|
| 0 | `ModifyStat` | `{"channel":"hp"}` |
| 1 | `ModifyStat` | `{"channel":"atk"}` |

**grant**:

```json
{
  "flat": 0,
  "increased": 0,
  "more": 0.25
}
```

(Runtime applies the same overlay magnitudes per channel row, or Secondary issues one grant per channel — product choice; prefer one grant with per-channel overlay map later. v1 docs: one overlay applies to each ModifyStat action unless `channels` map is added.)

**v1 clarification:** for multi-channel Passive, either:

- one action per channel + overlay `{ "more": 0.25 }` applied to each, or  
- overlay `{ "byChannel": { "hp": { "more": 0.5 }, "atk": { "more": 0.25 } } }`

Prefer **`byChannel`** when multiple ModifyStat rows share a grant; if absent, scalar `flat`/`increased`/`more` apply to every ModifyStat action in the def.

Ephemeral runtime rows (`foundation_effect_runtime`) are session-scoped — not content SSOT. No Unity handles as SSOT (ptrs only in event context).

---

## Read APIs (logical)

```text
IEffectCatalog
  GetDef(effectId) → Def
  ListActions(effectId) → ordered actions
  ListTriggers(effectId) → triggers

IEffectGrantStore
  Upsert(grant) / Withdraw(grantId)     // Secondary only
  ForOwner(ownerKind, ownerKey) → grants
  Revision → int
```

Secondary never parses injector combat events to invent actions — only grant store + catalog.

---

## Persistence note

Logical tables may start **in-memory / JSON on server** and later map to SQLite. Same shape either way. Server is SSOT for def+grant; injector receives revision push (cheat-mod pattern).
