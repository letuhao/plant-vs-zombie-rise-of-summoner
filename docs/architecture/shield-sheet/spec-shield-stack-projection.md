# Module: `shield-stack-projection`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Runtime SSOT:** `ShieldRuntime.GetShields` / `Totals`  
**Shared seam:** [../condition-glance/spec-sheet-hot-projection.md](../condition-glance/spec-sheet-hot-projection.md)  
**Prior art:** `CheatCommandRunner` shield owner snapshot stacks

---

## Objective

Project **ordered shield layers** for the Shield tab from the same Hot RPG runtime that fills
`ActorShieldSummaryDto`. Cold honesty: empty list / Pending — never FE invent.

## DTO (proposed)

```csharp
ActorShieldLayerDto {
  string ShieldId;
  string? ElementId;   // null = untyped
  long Current;        // Hp
  long Max;            // MaxHp
  int Priority;
  string SourceId;
  bool IsInnate;
}
```

Order = drain order (priority DESC, CreatedSeq ASC). Cap = `ShieldPolicy.MaxShieldsPerActor` (3).

## Transport options (pick in plan; both legal)

| Option | Shape |
|---|---|
| A | `GET /api/actors/{id}/shields` → `{ layers, totals }` |
| B | Widen `/sheet` with `shieldLayers: ActorShieldLayerDto[]` |

**Must:** same Hot flush as `shieldSummary` (one snapshot). Do **not** use HUD
`AggregateByElement` as tab SSOT.

## Summary consistency

`sum(layers.Current)` / `sum(layers.Max)` must match `Totals` / `shieldSummary` within documented
policy (same runtime call).

## Success criteria

- [ ] Hot with 1–3 shields: layers match GetShields fields.
- [ ] Cold: empty/null; FE Pending or omit — no three fake empties as “known empty.”
- [ ] Server tests + curl proof alongside sheet-hot-projection.
- [ ] Magnitudes `long`; overflow throws.

## Commands

```powershell
# After wire lands
curl -s http://127.0.0.1:5088/api/actors/<hotId>/shields
# or sheet.shieldLayers
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Shield
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~Shield
```

## Boundaries

- **Always:** extend shared Hot seam; RPG-layer only.
- **Ask first:** new Injector→Server channel.
- **Never:** second ProjectSheet; FE fixtures as product Hot.
