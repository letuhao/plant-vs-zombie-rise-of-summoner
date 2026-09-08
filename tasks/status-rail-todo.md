# Status rail — todo

Checklist for program `status-rail`. Audit: [`docs/research/status/status-coverage-audit.md`](../docs/research/status/status-coverage-audit.md).

## W0 — Audit + parity

- [x] `docs/research/status/status-coverage-audit.md`
- [x] `StatusCatalogParityTests` — JSON ids == Bootstrap (24)

## A1 — Cook / catalog

- [x] `DerivedExpandKind.StatusId` + derived-stat-catalog Status tab/families
- [x] Unlock category-four cook lock (variants from status surface + omni)
- [x] `DerivedSurfaceCook.CookStatusVariants` = Omni + 24

## A2 — FE

- [x] `actorSurface` / `derivedCook` status-id expand
- [x] Fold themes/glyphs for Omni+24
- [x] Derived / fold tests updated

## B1 — Inject StatusCatalog

- [x] `status-catalog.v1.json` real `payloadKinds` (bond empty)
- [x] `StatusCatalogFactory` + `StatusCatalogHub` via `ActorSurfaceCatalogHub.ConfigureAll`
- [x] Injector + battle hosts use `StatusCatalogHub.Current`
- [x] Bootstrap = migration golden / shim

## B2 — Battle StatMods

- [x] `BattleRunState` OnApplied/OnEnded → ledger
- [x] `BattleStatusStatModsTests`

## B3 — Lawn Unity

- [x] Clear path covers ember/hypno/kelp; unclearable = `jala` only
- [x] Plant butter-only policy documented (audit + DebugActions / sink)

## B4 — Tick / Tags

- [x] Contagion: battle passes `CombatBoardSnapshot` when board present (verify in audit)
- [x] `bond`: no PulseHp (Counter nested-burst only)
- [x] Tags: empty on defs; immunity grant-only (status-ssot)

## Docs

- [x] `tasks/status-rail-plan.md` / `status-rail-todo.md`
- [x] Amend `tasks/actor-sheet-derived-plan.md` Status expand row
- [x] Errata 21→24 in `spec-derived-stat-sheet.md`
- [x] `status-ssot.md` inject landed + Tags wording
