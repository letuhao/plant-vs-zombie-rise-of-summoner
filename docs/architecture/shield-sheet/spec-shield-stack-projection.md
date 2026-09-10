# Module: `shield-stack-projection`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Locks:** **S1** `sheet.shieldLayers` · **S2** summary element/stacks · **S3** live bag · **D8** cascade defer  
**Runtime SSOT:** `ShieldRuntime.GetShields` / `Totals`  
**Shared seam:** [../condition-glance/spec-sheet-hot-projection.md](../condition-glance/spec-sheet-hot-projection.md)  
**Prior art:** `CheatCommandRunner` shield owner snapshot stacks

---

## Objective

Project **ordered shield layers** onto `/sheet` beside `shieldSummary` from the same Hot RPG
runtime flush. Cold honesty: empty list — never FE invent. **Reject** player `GET …/shields` as tab SSOT (**S1**).

## Entrypoints

| Layer | Path | Duty |
|---|---|---|
| DTO | `ActorSheetDto.ShieldLayers` | `ActorShieldLayerDto[]` |
| Compose | `UniqueActorHubCompose.ProjectSheet` | Same call fills summary + layers + liveStatuses from live bag |
| Runtime | `ShieldRuntime.GetShields` / `Totals` | Order + totals |
| Live bag | Injector → Server `ActorLiveState` (**S3**) | Prefer extend match/dump ingest; **ask before new HTTP** |

## DTO — `ActorShieldLayerDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `shieldId` | `string` | yes | |
| `elementId` | `string?` | no | null = untyped |
| `current` | `long` | yes | Hp |
| `max` | `long` | yes | MaxHp |
| `priority` | `int` | yes | drain key |
| `sourceId` | `string` | yes | fiction label at fold |
| `isInnate` | `bool` | yes | |
| `regenPerSecond` | `long?` | no | **omit if runtime not exposed (D8)** |
| `broken` | `bool` | no | empty fill, slot retained |

Order = drain order (priority DESC, CreatedSeq ASC). Cap = `ShieldPolicy.MaxShieldsPerActor` (3).

## Envelope (on sheet)

```json
{
  "shieldSummary": {
    "elementId": "ice",
    "current": 100,
    "max": 200,
    "stacks": 2
  },
  "shieldLayers": [ /* ≤3 ActorShieldLayerDto */ ],
  "liveStatuses": []
}
```

## Parity invariant

`sum(layers.current) == Totals.Hp == shieldSummary.current` (and max analogously) within same snapshot.

## Summary policy (**S2**)

| Field | Rule |
|---|---|
| `elementId` | Front drain-order layer’s element; else null |
| `stacks` | `layers.Count` when widened |
| omit glance | null or current≤0 |

## Success criteria

- [ ] Hot 1–3 shields: layers match GetShields; same ProjectSheet as summary.
- [ ] Cold: `shieldLayers` empty; summary null.
- [ ] No player tab dependency on `GET …/shields`.
- [ ] Curl proof: cold vs Hot sheet includes both fields.
- [ ] Magnitudes `long`; overflow throws.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/actors/<hotId>/sheet | ConvertFrom-Json |
  Select-Object shieldSummary, shieldLayers
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Shield
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
```

## Boundaries

- **Always:** same Hot flush; RPG-layer only.
- **Ask first:** new Injector→Server HTTP channel (**S3**).
- **Never:** second ProjectSheet; FE fixtures; HUD AggregateByElement as tab SSOT.
