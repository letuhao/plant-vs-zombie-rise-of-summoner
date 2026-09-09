# Module: `sheet-hot-projection`

**Program:** `condition-glance` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Owner mandate:** Q6 — BE in scope; not a cheap FE mock  
**Related:** [../actor-sheet/spec-condition-tab.md](../actor-sheet/spec-condition-tab.md),
[../unique-actor-runtime.md](../unique-actor-runtime.md), [../match-runtime.md](../match-runtime.md),
status / element Hub SSOTs  
**Sibling (layers, not summary):** [../shield-sheet/spec-shield-stack-projection.md](../shield-sheet/spec-shield-stack-projection.md) —
same Hot snapshot; this module fills `shieldSummary` + `liveStatuses` only.  
**Contracts:** [ActorSheetDtos.cs](../../../src/FusionRpg.Contracts/ActorSheetDtos.cs) ·
[UniqueActorHubCompose.ProjectSheet](../../../src/FusionRpg.Server/UniqueActorHubCompose.cs)

---

## Objective

When the actor is in a **Hot** session, `GET /api/actors/{id}/sheet` projects **real**
`liveStatuses` and `shieldSummary` from the RPG runtime into the existing DTO fields. Cold
UniqueActor sheet keeps `[]` / `null`. FE **omits** those modules (Q3) — honest empty, not chrome,
not fixtures.

Today `ProjectSheet` hard-codes empty Hot fields (see compose comment at LiveStatuses/ShieldSummary
assignment) — this module removes that permanent cold-only path for Hot sessions.

---

## Hot session definition

**Hot** = Injector is connected to a running match/lawn (or documented battle host) **and** the
UniqueActor is bound to a live entity runtime that owns effect instances and/or shield bags
(`EffectRuntime` / `ShieldGate` path — RPG layer, not Unity field reads).

**Cold** = UniqueActor exists in store only; no live entity binding for statuses/shield.

Cite: [match-runtime.md](../match-runtime.md), [unique-actor-runtime.md](../unique-actor-runtime.md),
effect/shield runtime used by injector FoundationHarness.

---

## Entrypoints (amend — do not fork)

| Layer | Path | Duty |
|---|---|---|
| Server compose | `FusionRpg.Server.UniqueActorHubCompose.ProjectSheet` | Stop always assigning `LiveStatuses = []` / `ShieldSummary = null`; fill from Hot snapshot when available |
| DTO | `ActorStatusGlyphDto`, `ActorShieldSummaryDto`, `ActorSheetDto` | Existing wire shapes; widen only if runtime needs stacks (see below) |
| Injector | Effect/status runtime + shield bag on bound entity | Own live instances; push or query-serve to Server |
| Transport | Injector → Server (existing match ingest / Intent path — **ask before new channel**) | Keep Hot snapshot reachable for ProjectSheet |
| FE | `useActorSheet` queryKey `["actorSheet", instanceId]` | Invalidate on live-state SignalR (or poll only if owner accepts — prefer push) |
| SignalR | `RpgHub` (or sibling) | Emit sheet-relevant invalidate (e.g. actor live-state changed) → FE `invalidateQueries(["actorSheet", id])` |

Pieces never fetch; host invalidation → re-fold → `revision` bump ([spec-recipe-wire.md](spec-recipe-wire.md)).

---

## DTO schemas (wire)

### `ActorStatusGlyphDto` (existing)

| Field | Type | Notes |
|---|---|---|
| `statusId` | `string` | Catalog join key |
| `remainingPermille` | `int?` | Duration/ring; null if N/A |

Fold joins catalog `hudToken` / `color` for `StatusGlyph`.

### `ActorShieldSummaryDto` (existing)

| Field | Type | Notes |
|---|---|---|
| `elementId` | `string?` | Paint via element-paint-ssot / theme-bind |
| `current` | `long?` | Shield HP — **long** magnitude |
| `max` | `long?` | |

**Stacks:** not on DTO today. If runtime exposes stack count needed by `shield-status` pips, **widen**
`ActorShieldSummaryDto` with `stacks` (`int`) in the same module — do not invent FE-only stacks.

### Mount rules for FE (Q3)

| Server value | FE |
|---|---|
| `liveStatuses` empty / missing | omit `status-glyph-strip` |
| `shieldSummary` null OR `(current ?? 0) <= 0` | omit `shield-status` + radial ring |
| Hot with data | mount pieces; paint from catalogs/packs |

---

## ActorHub gate

- **Consume** Hub / effect / shield runtime for projection.
- Do **not** invent a private FE fold or fake Hot on cold UniqueActor.
- If a Hub write is required for Hot visibility, contribute via `IActorStatSubsystem` +
  `ContributionSourceIds` — named in implement plan.
- Magnitudes: **`long`** for shield HP; overflow throws; no float.

---

## Success criteria

- [ ] Cold UniqueActor: `liveStatuses = []`, `shieldSummary = null` (unchanged).
- [ ] Hot with live effects: sheet JSON `liveStatuses` matches runtime instance set (ids + remainingPermille).
- [ ] Hot with shield: `shieldSummary.current/max/elementId` match runtime; `long`-safe.
- [ ] `ProjectSheet` no longer unconditionally empties Hot fields when Hot snapshot exists.
- [ ] FE invalidates `["actorSheet", id]` on documented SignalR (or approved transport) so Condition `revision` advances.
- [ ] Live curl proof: cold vs Hot actor documented in test notes / e2e artifact.
- [ ] Standing + `resourcePools` Hub path unchanged (no regression).
- [ ] Hot flush coordinated with `shield-stack-projection` so glance summary and tab layers agree.

---

## Commands

```powershell
# Cold sheet
curl -s http://127.0.0.1:5088/api/actors/<coldInstanceId>/sheet | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary, standing, resourcePools

# Hot sheet (Injector match bound — use live instanceId)
curl -s http://127.0.0.1:5088/api/actors/<hotInstanceId>/sheet | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary

dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerivedEndpoints
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~ResourceBaseline
# Add Hot projection tests under Server.Tests when implement lands
```

---

## Testing

- Server: cold sheet empty live fields (existing).
- Server: Hot fixture/session → projected statuses/shield match runtime snapshot.
- Guard: FE tests must not ship fixtures as the only product path for Hot fields.

## Boundaries

- **Always:** cold honesty; RPG-layer sources only; amend `ProjectSheet`.
- **Ask first:** new Injector→Server transport channel; widening DTO with `stacks`.
- **Never:** fabricate statuses on cold UniqueActor; FE-only fake Hot as “done”; piece-level SignalR.

## Tunables

Status colors/tokens: `status-catalog.v{n}.json`. Shield element paint: element catalog + packs.
No new power curve.
