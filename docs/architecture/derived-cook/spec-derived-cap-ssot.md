# Module: `derived-cap-ssot`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** `derived-sheet-projection`, [tunables-ssot.md](../tunables-ssot.md), registry caps  
**Tuning:** `data/tuning/derived-stats.v{n}.json` (`categoryResistCap` et al.)

---

## Objective

**One home for a channel cap** reaches the Derived UI: registry/policy → wire → fold paint. Delete
FE `KNOWN_CAPS` and literal `0.95` / `1` as product truth.

## Rules

- Cap marker (`CAP`) only when wire/registry says capped **and** value is at cap (or state=`capped`).
- `status.resist.omni` remains **uncapped** (actor-hub / design) — never paint CAP from FE guess.
- Soft caps stay PS-8 exempt with comments at registry; UI does not invent new ceilings.

## Success criteria

- [ ] `KNOWN_CAPS` removed from production FE.
- [ ] Cap values match tuning/registry in Server tests + FE fold goldens.
- [ ] Omni uncapped channels never show CAP.

## Commands

```powershell
rg -n "KNOWN_CAPS|0\.95" web/fusion-rpg-web/src/features/gui-lego
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~DerivedStat
```

## Boundaries

- **Never:** second clamp in UI; fake CAP to “look done.”
