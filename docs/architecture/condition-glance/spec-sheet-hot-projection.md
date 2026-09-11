# Module: `sheet-hot-projection`

**Program:** `condition-glance` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Locks:** **Q6** · **S1–S3** · **Q9** sibling parity  
**Related:** [../actor-sheet/spec-condition-tab.md](../actor-sheet/spec-condition-tab.md),
[../shield-sheet/spec-shield-stack-projection.md](../shield-sheet/spec-shield-stack-projection.md),
[../unique-actor-runtime.md](../unique-actor-runtime.md), [../match-runtime.md](../match-runtime.md)  
**Contracts:** [ActorSheetDtos.cs](../../../src/FusionRpg.Contracts/ActorSheetDtos.cs) ·
[UniqueActorHubCompose.ProjectSheet](../../../src/FusionRpg.Server/UniqueActorHubCompose.cs)

---

## Objective

When the actor is in a **Hot** session, `GET /api/actors/{id}/sheet` projects **real**
`liveStatuses`, `shieldSummary`, and (via sibling) `shieldLayers` from the RPG live bag into the
sheet DTO. Cold UniqueActor keeps `[]` / `null` / empty layers. FE **omits** glance modules (Q3).

Today `ProjectSheet` hard-codes empty Hot fields — this module + `shield-stack-projection` remove
that permanent cold-only path for Hot sessions **in one compose call**.

---

## Hot session definition

**Hot** = Injector connected to a running match/lawn (or documented battle host) **and** UniqueActor
bound to live effect/shield runtime (RPG layer).

**Cold** = store-only UniqueActor; no live binding.

---

## Live bag (**S3**)

| Step | Duty |
|---|---|
| Injector | Push statuses + shields into Server **`ActorLiveState`** bag |
| Transport | Prefer extend existing match/dump ingest; **ask before new HTTP channel** |
| Server | Store per-instance Hot snapshot |
| `ProjectSheet` | Read bag → fill `liveStatuses`, `shieldSummary`, `shieldLayers` |

Never FE fixtures as product Hot.

---

## Entrypoints (amend — do not fork)

| Layer | Path | Duty |
|---|---|---|
| Server compose | `UniqueActorHubCompose.ProjectSheet` | Stop always emptying Hot fields; fill from live bag |
| DTO | `ActorStatusGlyphDto`, `ActorShieldSummaryDto`, `shieldLayers` | Summary + layers same flush (**S1**) |
| FE | `useActorSheet` `["actorSheet", instanceId]` | Invalidate on live-state SignalR |
| SignalR | Preferred event name: **`ActorLiveStateChanged`** | Ask-first if emitter/channel missing — implement later |

Pieces never fetch; host invalidation → re-fold → `revision` bump.

---

## DTO schemas

### `ActorStatusGlyphDto` (existing)

| Field | Type | Notes |
|---|---|---|
| `statusId` | `string` | Catalog join |
| `remainingPermille` | `int?` | |

### `ActorShieldSummaryDto` (**S2**)

| Field | Type | Notes |
|---|---|---|
| `elementId` | `string?` | Front drain-order layer; else null |
| `current` / `max` | `long?` | From Totals |
| `stacks` | `int?` | Layer count when widened |

### `shieldLayers`

See [spec-shield-stack-projection.md](../shield-sheet/spec-shield-stack-projection.md) — filled in
**same** ProjectSheet call. Parity: `sum(layers) == Totals == summary`.

### Mount rules for FE (Q3) — glance only

| Server value | FE |
|---|---|
| `liveStatuses` empty | omit `status-glyph-strip` |
| `shieldSummary` null OR current≤0 | omit `shield-status` + radial ring |
| Hot with data | mount; paint from catalogs/packs |

Shield **tab** empty wells: Hot + N&lt;3 dashed OK — **not** Q3 omit of the whole tab.

---

## ActorHub gate

- Consume Hub / effect / shield runtime via live bag.
- Magnitudes: **`long`** for shield HP; overflow throws; no float.

---

## Success criteria

- [x] Cold: `liveStatuses=[]`, `shieldSummary=null`, `shieldLayers=[]`.
- [x] Hot with effects: `liveStatuses` matches runtime. *(proven via bag POST + `ActorSheetHotLiveStateTests`)*
- [x] Hot with shield: summary + layers match GetShields/Totals; elementId/stacks per **S2**.
- [x] Same Hot fixture proof shared with `shield-sheet` Wave 1.
- [x] FE invalidates `["actorSheet", id]` on documented SignalR (or approved transport).
- [x] Curl cold vs Hot documented — [docs/runbook/actor-sheet-hot-live-state.md](../../runbook/actor-sheet-hot-live-state.md).
- [x] Standing + `resourcePools` unchanged.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/actors/<coldId>/sheet | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary, shieldLayers, standing, resourcePools
curl -s http://127.0.0.1:5088/api/actors/<hotId>/sheet | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary, shieldLayers
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerivedEndpoints
```

## Testing

- Cold empty live fields (existing).
- Hot fixture → statuses/summary/layers agree.
- FE tests must not ship fixtures as the only Hot product path.

## Boundaries

- **Always:** cold honesty; RPG-layer; amend ProjectSheet once.
- **Ask first:** new Injector→Server channel; SignalR emitter details.
- **Never:** fabricate on cold; FE-only fake Hot; piece-level SignalR; fork ProjectSheet for layers.
