# aptitude-sheet — implementation plan

**Program:** `aptitude-sheet`  
**Map:** [docs/architecture/aptitude-sheet-map.md](../docs/architecture/aptitude-sheet-map.md)  
**Ideal:** [docs/architecture/aptitude-sheet-ideal.md](../docs/architecture/aptitude-sheet-ideal.md)  
**Specs:** [docs/architecture/aptitude-sheet/](../docs/architecture/aptitude-sheet/)  
**Todo:** [aptitude-sheet-todo.md](aptitude-sheet-todo.md)  
**Queue:** gui-lego **P4 → Aptitudes** claimed by this stream  
**Locks binding:** D1–D14 · E1–E8 · S1–S10 (map)

## Overview

Ship one `aptitudes-console` (Modes A/B/C) so UniqueActor spends **UniqueDemon**, empire species
build shares the same console under Pacts, Bound lawn uniques apply UniqueDemon, allocate chrome
(leftover + Confirm/Cancel + auto-assign) is in-band, and aptitude **build presets** land with
donut chart, dual abs/‰, favour seed, and transactional Activate — without Vision full synergy or
Hub EffectiveUnique baseline.

## Spec coverage

**13/13 modules tasked** (AS-0.1…AS-3.5). Accept locks **G1–G13** closed in
[aptitude-sheet-todo.md](aptitude-sheet-todo.md) so every module success criterion is an explicit
Accept (A3 fed families, `lo>hi`, dual abs/‰, Hub LoadAllocation, same-species isolation, ideal
hand-off, `long` magnitudes, catalog icon→tile, host Activate split a/b/c).

Checkpoint 3 still walks the full [map success criteria](../docs/architecture/aptitude-sheet-map.md)
list — the G-locks prevent buried Done gates; they do not replace the map checklist.

---

## Architecture decisions (locked — do not re-debate in tasks)

| Id | Decision |
|---|---|
| D1 | UniqueDemon free-build; empty legal until spend |
| D2 | Creature → Mode A; commander → Mode C |
| D3 | Lawn Bound = commander + UniqueDemon |
| E1/S3 | Confirm = draft path; Activate = `POST /api/aptitude-presets/activate` only |
| E2 | D13 leftover after clamp is legal |
| E3/S1/S7 | Favour = permille GET seed only; empty → refuse + offer Even |
| E4 | recharts donut |
| E5/E6/E8 | Sum ‰=1000 on save; no level-up autorespec; soft preset/abs caps tunable |
| S2 | No Mode B ConfirmDialog |
| S4 | Lawn fetch = unique GET per Bound id |
| Soft max presets | Default **32** soft max per player in `data/tuning/aptitudes` (or aptitude-presets) — reversible balance knob, not a hard gate |

## Dependency graph

```text
stale-doc-amend
unique-allocate ──┬── aptitudes-live-bus ── unique-lawn-wire
                  │         │
catalog-icons ────┤         │
posture-packs ────┴── pieces ── surface-vm ──┬── host-role-gate
                                            └── species-host
preset-api (CRUD+favour+materialize+activate)
     │
     ├── auto-assign
     └── preset-console ── wire Activate/open into hosts
```

## Delivery phases (vertical)

### Phase 0 — Truth + UniqueDemon write path (Wave 0)

1. **stale-doc-amend** — D5 docs + queue P4 claim.
2. **unique-allocate** — GET/POST UniqueDemon + FE hooks (D1).
3. **aptitudes-live-bus** — scoped SignalR + FE invalidate + `AptitudesState.species` (S10).
4. **catalog-icons** + **posture-theme-packs** — parallel content/theme.

### Phase 1 — Lawn + presentation contracts (Wave 1)

5. **unique-lawn-wire** — Bound cache via unique GET only (S4).
6. **aptitude-pieces** — HTML drafts then factories (leftover, decision, tile, donut, …).
7. **aptitudes-surface-vm** — fold + recipe + closed bus (leftover/decision/auto/preset.open).

### Phase 2 — Hosts (Wave 2)

8. **host-role-gate** — ActorSheet Mode A/C RecipeMount; Cancel fiction; draft Confirm.
9. **species-host** — Collapse SpeciesBuildPanel; price on strip; no ConfirmDialog (S2).

### Phase 3 — Presets + auto-assign (Wave 3)

10. **preset-api** — store + CRUD + favour GET + materialize + transactional activate.
11. **auto-assign** — draft-only rules consuming favour GET / active preset.
12. **preset-console** — nested gallery/editor/donut; Apply-to-draft + Activate.
13. **Host wire-up** — preset.open / Activate / auto-assign on A/B/C hosts.

## Checkpoints

| After | Verify |
|---|---|
| Phase 0 | Unique GET/POST curl green; scoped AptitudesUpdated; D5 `rg` clean; icons+packs load |
| Phase 1 | Bound unique Hot includes UniqueDemon after allocate+reload; leftover+decision in fold fixtures |
| Phase 2 | Creature posts UniqueDemon; commander posts commander; Mode B respec without ConfirmDialog |
| Phase 3 | Activate txn; favour permille; donut; auto-assign draft-only; map success criteria |

## Risks

| Risk | Mitigation |
|---|---|
| Injector commander GET `"shares"` brittle | S4 — never extend with `uniques` map |
| Mode B Activate forks respec pricing | Activate calls existing species-build respec path |
| HTML draft lag | Drafts required before factory “done”; React hosts may stub with landmarks |
| Soft abs max content bikeshed | Ship tunable defaults; owner retunes in balance pass |

## Explicitly out

Aspect allocate · EffectiveUnique Hub baseline · Vision full synergy · posture-balance (A6) ·
lawn species glance door · level-up keep-aligned (E6) · bare `tasks/plan.md`

## Verification commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "UniqueAptitude|AptitudePreset|AptitudesUpdated"
dotnet test tests\FusionRpg.Data.Tests --filter "Allocation|AptitudePreset"
dotnet test tests\FusionRpg.Core.Tests --filter "AptitudeCatalog|AutoAssign|SpeciesAllocation"
.\scripts\guard-dal.ps1
.\scripts\guard-secondary-no-unity.ps1
cd web\fusion-rpg-web; npm test -- --run aptitude AptitudesTab AptitudesLayer
# Live (owner): .\scripts\deploy-play.ps1 -NoServer ; unique allocate → Bound lawn probe
```
