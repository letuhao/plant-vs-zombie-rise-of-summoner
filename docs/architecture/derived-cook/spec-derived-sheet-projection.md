# Module: `derived-sheet-projection`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Amends:** sheet channel DTO / `ProjectSheet` derived channels  
**Contracts:** `ActorSheetDtos.cs` · UniqueActorHubCompose  
**Depends on:** ActorHub derived snapshot, `DerivedStatRegistry`

---

## Objective

Project **registry-truth** fields onto each sheet derived channel so the FE six-state join and CAP
paint stop guessing. Prefer `/sheet` as the single Derived data path; lean `/derived` stays honest
Pending when incomplete.

## Wire fields (extend `ActorSheetChannelDto` or sibling)

| Field | Type | Notes |
|---|---|---|
| `channelId` | string | existing |
| `value` | number/long as today | existing magnitude |
| `contributions` | list | existing |
| `composeKind` | string | existing or confirm always present |
| `unitClass` | string | nine-class ledger id |
| `defaultValue` | number | registry default |
| `cap` | number? | null when uncapped (e.g. resist.omni) |
| `renderState` | enum string | one of six: active\|default\|capped\|stub\|no-producer\|unregistered — **preferred server-side**; FE may recompute only as golden parity test |
| `hasProducer` | bool? | optional if renderState authoritative |

Magnitudes remain `long`-safe on wire where GameUnits; ratios stay as sheet already sends.

## UniqueDemon / lean path

If baseline merge is omitted on `/derived`, FE must show Pending — **never invent** values
(`spec-derived-tab` open question → close with Pending honesty in this module).

## Success criteria

- [ ] Sheet JSON includes unitClass + cap + default (or documented Pending) for cook-joined channels.
- [ ] Cold and Hot sheet both project these metadata fields from registry (not only Hot).
- [ ] FE can delete `KNOWN_CAPS` / stub regex once consumers migrate (`derived-cap-ssot` / render-states).
- [ ] Tests: capped resist channel carries cap; omni resist cap null; stub progression flagged.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/actors/<id>/sheet | ConvertFrom-Json |
  Select-Object -ExpandProperty derivedChannels | Select-Object -First 3
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
```

## Boundaries

- **Always:** registry is SSOT for cap/default/unitClass.
- **Ask first:** changing contribution SourceId grammar.
- **Never:** FE inventing CAP as product truth after this lands.
