# Module: `derived-sheet-projection`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Locks:** **D2** (renderState authoritative) · **D6** (Value stays `double` for now)  
**Amends:** `ActorSheetChannelDto` / `ProjectSheet` derived channels · FE `aura.ts` twin  
**Contracts:** [ActorSheetDtos.cs](../../../src/FusionRpg.Contracts/ActorSheetDtos.cs) · UniqueActorHubCompose  
**Depends on:** ActorHub derived snapshot, `DerivedStatRegistry`, UnitClass ledger in
[../../design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)

---

## Objective

Project **registry-truth** metadata onto each sheet derived channel so the FE six-state join and CAP
paint stop guessing. Prefer `/sheet` as the single Derived data path; lean `/derived` stays honest
Pending when incomplete.

## Entrypoints

| Layer | Path | Duty |
|---|---|---|
| DTO | `ActorSheetChannelDto` | Widen fields below |
| Compose | `UniqueActorHubCompose.ProjectSheet` | Fill metadata from registry + snapshot |
| FE | `aura.ts` / sheet types | Mirror wire; no invent |
| Lean | `GET /derived` | Pending if baseline merge omitted (UniqueDemon) |

## DTO fields (`ActorSheetChannelDto`)

| Field | Type | Required | Notes |
|---|---|---|---|
| `channelId` | `string` | yes | existing |
| `displayName` | `string` | yes | fiction / catalog |
| `reading` | `string` | no | catalog reading |
| `composeKind` | `string` | yes | FlatSum / FlatReplace / … |
| `value` | `double` | yes | **D6:** keep `double` for now; overflow widen is a separate Core ticket. Comment exempt on DTO |
| `contributions` | list | yes | existing |
| `unitClass` | `string` | yes | UnitClass ledger id (cite magnitude SSOT — not “nine-class”) |
| `defaultValue` | `double` | yes | registry default |
| `cap` | `double?` | no | **null** when uncapped (e.g. `status.resist.omni`) |
| `renderState` | enum string | yes | one of six — **D2 authoritative** |
| `isStub` | `bool` | no | if not folded into renderState |
| `hasProducer` | `bool` | no | optional; prefer renderState |

Shield HP magnitudes elsewhere stay **`long`** — not this DTO.

## UniqueDemon / lean path

If baseline merge is omitted on `/derived`, FE must show **Pending** — never invent values.
Close [spec-derived-tab](../actor-sheet/spec-derived-tab.md) open question via this signal.

## Cold / Hot matrix

| Session | Derived channel metadata |
|---|---|
| Cold UniqueActor | Project from Hub snapshot + registry (same metadata fields) |
| Hot | Same + live contributions; does not require ActorLiveState bag |

## Sample JSON

```json
{
  "channelId": "status.resist.dot",
  "displayName": "Resist · damage over time",
  "composeKind": "SumIncreased",
  "value": 0.95,
  "unitClass": "StatusPotencyPoints",
  "defaultValue": 0,
  "cap": 0.95,
  "renderState": "capped",
  "contributions": []
}
```

## Success criteria

- [ ] Sheet JSON includes `unitClass`, `cap`, `defaultValue`, `renderState` for cook-joined channels.
- [ ] Omni resist: `cap` null; category resist: cap from policy/tuning.
- [ ] FE can delete `KNOWN_CAPS` / stub regex after consumers migrate.
- [ ] UniqueDemon lean path: Pending when baseline omitted — test documented.
- [ ] DTO comments note **D6** double exempt; shield `long` elsewhere.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/actors/<id>/sheet | ConvertFrom-Json |
  Select-Object -ExpandProperty derivedChannels | Select-Object -First 3
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
```

## Testing

- Server: capped resist carries cap; omni null; stub progression flagged.
- FE twin types match Contracts.

## Boundaries

- **Always:** registry is SSOT for cap/default/unitClass/renderState.
- **Ask first:** widening channel `Value` to `long` (separate ticket).
- **Never:** FE inventing CAP as product truth after this lands.
