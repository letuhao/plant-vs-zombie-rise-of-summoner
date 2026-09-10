# Module: `derived-cap-ssot`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** `derived-sheet-projection`, [tunables-ssot.md](../tunables-ssot.md), registry caps  
**Tuning:** `data/tuning/derived-stats.v{n}.json` (`categoryResistCap` et al.)

---

## Objective

**One home for a channel cap** reaches the Derived UI: registry/policy → wire → fold paint. Delete
FE `KNOWN_CAPS` and literal `0.95` / `1` as product truth.

## Wire rules

| Rule | Detail |
|---|---|
| CAP marker | Only when wire `cap` is non-null **and** state is `capped` (or value at cap) |
| Omni uncapped | `status.resist.omni` → `cap: null` — never FE-special-case as product SSOT |
| Category resist | Cap from `categoryResistCap` / registry (0.95 today) |
| Immune families | `status.immune.*` / `status.immuneReduction.*` cap at **1** — on wire |
| Soft caps | PS-8 exempt with registry comments; UI does not invent ceilings |

## Delete path (FE)

1. Remove `KNOWN_CAPS` from `derivedCook.ts`.
2. Remove open-prefix FE `0.95` / `1` branches.
3. Paint CAP only from `channel.cap` + `renderState`.

## Success criteria

- [ ] `KNOWN_CAPS` removed from production FE.
- [ ] Cap values match tuning/registry in Server tests + FE fold goldens.
- [ ] Omni uncapped never shows CAP; immune at 1 shows CAP when at bound.
- [ ] Grep clean for literal `0.95` as CAP SSOT in FE cook path.

## Commands

```powershell
rg -n "KNOWN_CAPS|0\.95" web/fusion-rpg-web/src/features/gui-lego
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~DerivedStat
```

## Sample

```json
{ "channelId": "status.resist.dot", "cap": 0.95, "renderState": "capped", "value": 0.95 }
```

## Boundaries

- **Never:** second clamp in UI; fake CAP to “look done.”
