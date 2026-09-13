# Plan: actor-sheet program (catalog-era)

Source: [actor-sheet-map.md](../docs/architecture/actor-sheet-map.md) · Ideal:
[actor-sheet-ideal.md](../docs/architecture/actor-sheet-ideal.md) · Specs under
[actor-sheet/](../docs/architecture/actor-sheet/).

Task list: [actor-sheet-todo.md](actor-sheet-todo.md). Prefixed pair only — never
`tasks/plan.md` / `tasks/todo.md`.

**Supersedes** the 2026-08-29 six-tab plan (trail; all tasks closed). Do not implement trail specs.

---

## Spec gate — no implementation (2026-09-07)

**Repo standard:** SPECIFY → PLAN → TASKS → IMPLEMENT. Module specs are still **Draft — pending
owner review** (`No build authorized until approved`).

A prior session violated that gate by writing this plan **and** starting install/catalog/code work
before owner-approved specs. **Parked:** do not continue T0–T16 implementation until the owner
approves the catalog-era ActorSheet specs (and derived expand/join locks). Accidental early
artifacts are not authorization to finish the build:

- npm presentation libs; draft `data/tuning/*-catalog*.json`
- Core `src/FusionRpg.Core/ActorSurface/*` loaders/hubs + `ActorSurfaceCatalog` tests (T2-shaped;
  landed while SPECIFY was re-entered) — **do not proceed to T3 Server/Injector wiring** until
  specs are approved

Lawn stage chrome is a **separate** program:
[lawn-interactive-map.md](../docs/architecture/lawn-interactive-map.md) — not part of this plan.

---

## 1. Shape of the work

**10 live modules · T0–T16 · 5 checkpoints.** Vertical slices; one `ActorPanel.tsx` editor at a time.
**Tasks remain listed for after SPECIFY approval only.**

```text
T0  npm presentation libs
T1–T4  actor-surface-catalog (JSON → Core → hosts/API → FE labels)
T5–T7  actor-sheet-shell (size → widgets → eight-tab chrome)
T8–T10 condition · aptitudes · derived
T11–T15 status · shield · elements · kit · paths
T16 HUD resolve from status-catalog
```

## 2. Architecture decisions

- T7.2 host inject; Core `Parse` + `Configure` only.
- First catalog publish byte-faithful from C# + seeds — no combat golden drift.
- Keep `ActorPanel`; export alias `ActorSheet`. Buy-before-build presentation libs.
- Pending honesty — never fabricate Standing / xpToNext / shields / equip.
- Paths kind wraps PassivesTab; xyflow read-only on push (not Phaser / not `#/world`).
- Commander leftover v1; leftover footer only on Aptitudes / dirty draft.
- Size `min(1800px, 96vw)` × `min(960px, 92vh)`.
- **Derived:** catalog holds **families**; combat families **expand** over omni + injected elements
  to join `GET /api/actors/{id}/derived` (268 registered channels in Core). Never treat ~33 family
  rows as the channel ceiling.

## 3. Out of scope

Trail specs · Promote · UniqueCreature allocate POST · action corpus · Phaser Path VFX · Band B enlarge ·
**lawn-interactive** (own map).

## 4. Risks

| Risk | Mitigation |
|---|---|
| Spec gate skipped again | No IMPLEMENT until owner ticks Draft specs |
| Family catalog under-displays vs 268 | Expand combat × elements; join `/derived` |
| ActorPanel thrash | One task edits it at a time |

## 5. Non-blocking follow-ups

Commander HUD chip → sheet · UniqueCreature allocate · lawn-interactive plan:
[lawn-interactive-plan.md](lawn-interactive-plan.md) (separate program; not this checklist).

## 6. Verification (when unlocked)

FE focused tests + build · `dotnet test … --filter ActorSurfaceCatalog` · guards after host wiring.
