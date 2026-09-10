# Actor sheet Hot live-state — curl proof

**Programs:** `condition-glance` (CG Checkpoint A) · `shield-sheet` (SS-A3)  
**Store:** `POST /api/internal/actors/{instanceId}/live-state` → `IActorLiveStateStore`  
**Emit:** SignalR `ActorLiveStateChanged` `{ instanceId }` (FE invalidates `["actorSheet", id]`)

Server must be up on `http://127.0.0.1:5088`. Use a real UniqueActor `instanceId` from
`GET /api/actors` (or create one via your usual UniqueActor flow).

## Cold sheet

```powershell
$id = "<cold-instanceId>"
curl -s "http://127.0.0.1:5088/api/actors/$id/sheet" | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary, shieldLayers, standing, resourcePools
```

Expect: `liveStatuses` empty array, `shieldSummary` null, `shieldLayers` empty array.
Standing + resourcePools still projected (unchanged by Hot bag).

## Hot sheet (bag fill without Injector)

```powershell
$id = "<instanceId>"
$body = @{
  liveStatuses = @(
    @{ statusId = "status.dot.burn"; remainingPermille = 500 }
  )
  shieldLayers = @(
    @{
      shieldId = "aura:ice:1"; elementId = "ice"; current = 40; max = 80
      priority = 30; sourceId = "aura.ice"; isInnate = $false; broken = $false
    }
    @{
      shieldId = "skill:fire:1"; elementId = "fire"; current = 20; max = 40
      priority = 20; sourceId = "skill.fire"; isInnate = $false; broken = $false
    }
  )
} | ConvertTo-Json -Depth 6

Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:5088/api/internal/actors/$id/live-state" `
  -ContentType "application/json" -Body $body

curl -s "http://127.0.0.1:5088/api/actors/$id/sheet" | ConvertFrom-Json |
  Select-Object liveStatuses, shieldSummary, shieldLayers
```

Expect: statuses present; `shieldLayers` length 2 in drain order; `shieldSummary.elementId`
= front layer (`ice`); `stacks` = 2; `current`/`max` = sum of layers.

## Automated proof

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~ActorSheetHotLiveStateTests
```

Covers cold honesty, Hot summary+layers parity, and `ActorLiveStateChanged` emit on POST.

## Injector path

`FusionRpg.Injector` `ActorLiveStatePush` POSTs the same bag from RPG runtime
(GetShields + live statuses) on a Bound UniqueActor flush — no FE fixtures required.
