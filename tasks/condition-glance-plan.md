# condition-glance — implementation plan

**Program:** `condition-glance`  
**Map:** [docs/architecture/condition-glance-map.md](../docs/architecture/condition-glance-map.md)  
**Ideal:** [docs/architecture/condition-glance-ideal.md](../docs/architecture/condition-glance-ideal.md)  
**Todo:** [condition-glance-todo.md](condition-glance-todo.md)  
**Siblings:** [derived-cook-plan.md](derived-cook-plan.md) · [shield-sheet-plan.md](shield-sheet-plan.md)  
**Queue:** gui-lego P1 Partial → Done after Waves land

## Overview

Finish ActorSheet **Condition glance** end-to-end: shared element paint SSOT, Hot
`liveStatuses` + `shieldSummary` via `ActorLiveState` bag (**S3**), themed badges/gauges/radar,
rail `role-badge`, recipe + fold amend — architecture-correct GUI Lego, not a CSS patch.

Shield **layers** (`sheet.shieldLayers`) are filled in the **same** `ProjectSheet` call by
[shield-sheet-plan.md](shield-sheet-plan.md) Wave 1 — coordinate Hot fixture **`ActorSheetHotLiveStateTests`**; do not fork compose.

## Sibling cross-links

| Seam | Path |
|---|---|
| Shared Hot fixture | **`ActorSheetHotLiveStateTests`** — CG-A4 owns; SS-A2 asserts `shieldLayers` |
| Injector fill | CG-A4b ↔ SS-A0 (one transport) |
| SignalR emit | CG-A5b (Condition owns; Shield rides invalidate) |
| Element paint | CG-A1 `resolveElementPaint` ↔ derived-cook **DC-6** (same path) |
| Ideal length | [condition-glance-ideal.md](../docs/architecture/condition-glance-ideal.md) ~279 lines on disk — no restore unless truncated |

## Architecture decisions (locked)

| Id | Decision |
|---|---|
| Q2–Q9, S1–S3 | See capability map |
| **S3 default** | Server `IActorLiveStateStore` keyed by instanceId. Injector fills via **extend existing match/dump ingest** when a payload path already exists; otherwise ship a **minimal internal POST** (`/api/internal/actors/{id}/live-state`) as reversible default — owner may rename/merge later. **Not a hard gate.** Prefer SignalR event name `ActorLiveStateChanged` for FE invalidate |
| Hot layers | Owned by shield-sheet; this program fills summary + statuses only |

## Dependency graph

```text
element-paint-ssot → theme-bind ─┬→ badges / gauges / hosts
fiction-copy ────────────────────┤
ActorLiveState bag + ProjectSheet (summary+statuses) ──→ shield-status omit/mount
         │
         └── same compose: shieldLayers (shield-sheet)
recipe-wire ← layout ← all pieces
ActorSummarize ← role-badge
```

## Delivery phases (vertical)

### Phase A — SSOT + Hot seam (Wave 1)

1. **element-paint-ssot + theme-bind** — FE `resolveElementPaint`; bindSurface resolves `themeRef` / `shieldThemeRef`. Redirect/delete private table in `actorHudDisplayTokens.ts` (paint SSOT must-migrate). Same path consumed by derived-cook **DC-6**.
2. **fiction-copy** — strip stub `.md` titles from fold/drafts.
3. **ActorLiveState bag + ProjectSheet Hot fill** — cold empty; Hot fills `liveStatuses` + `shieldSummary` (**S2**). Named fixture **`ActorSheetHotLiveStateTests`** (this program owns; shield-sheet **SS-A2** asserts layers). Coordinate `shieldLayers` same compose.
4. **Injector fill (CG-A4b)** — push statuses + shields into bag (dump extend or internal POST).
5. **FE invalidate + Server emit** — Server emits `ActorLiveStateChanged` on bag write (CG-A5b); host invalidates `["actorSheet", id]` (CG-A5).

### Phase B — shared pieces (Wave 2)

6. HTML drafts then factories: `element-badge`, `phase-badge`, `role-badge`, `shield-status`.
7. Mount `role-badge` on expanded `ActorSummarize` (kill mute `Lv · role` string).

### Phase C — glance pieces + hosts (Wave 3)

8. Drafts + factories: progression-gauge, pool-meter, pool-radial (shield ring), standing-radar (recharts), standing-bars, status-glyph-strip (omit when 0). **Acceptance:** animate/update on `revision` bump (Q5), not static mock fills.
9. Hosts: actor-identity, cond-hero (shield slot), stand-row.

### Phase D — surface land (Wave 4)

10. condition-layout landmarks + recipe-wire (omit rules, revision, closed bus catalog tested; FE recipe sync with design `condition-console.json` shield slot).
11. Queue P1 Done evidence: curl cold/Hot + SPA landmarks + tests.

## Checkpoints

| After | Verify |
|---|---|
| Phase A | Cold sheet empty Hot fields; `ActorSheetHotLiveStateTests` summary(+layers via SS-A2); Injector fill + emit; paint + HUD redirect |
| Phase B | Role badge themed on rail; shield-status omit when null |
| Phase C | Radar recharts; revision updates gauges/radar; no surplus scroll |
| Phase D | RecipeMount only; bus catalog + recipe shield slot sync; queue P1 Done |

## Risks

| Risk | Mitigation |
|---|---|
| Hot Injector path unclear | S3 default internal POST + store; extend dump later |
| Drift from shield-sheet compose | Same ProjectSheet method; shared Hot fixture in Server.Tests |
| Draft debt blocks React | Author HTML before factory “done”; parallel drafts OK |

## Explicitly out

Derived cook harden · Shield tab stack UI (sibling plans) · Core OverlayAdd · bare `tasks/plan.md`

## Verification commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
cd web\fusion-rpg-web; npm test -- --run foldCondition ConditionTab ActorSummarize
.\scripts\deploy-play.ps1 -NoServer -NoGame
# Hot: curl sheet cold vs hot instanceId (with Injector)
```
