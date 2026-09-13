# Capability map: loam relics and wonders

**Status: APPROVED 2026-09-13.** Module boundaries and build order proposed and approved together,
matching `scoped-inventory-hierarchy-map.md`'s precedent. Recorded in
[decisions.md](decisions.md) ("Loam relics and wonders SSOT"). Module specs proceed in dependency
order below.

**Ideal it implements:** [loam-relics-and-wonders-ideal.md](loam-relics-and-wonders-ideal.md) — idea
phase complete, strengthen-pass corrected, all blocking owner questions resolved 2026-09-13 (this
session): World scope deferred to a later wave (not this map), Zomboss can contest a future World-scope
Wonder (mitigation named, not built here since World scope isn't in this wave), relic vocabulary
resolved to rolled items via the existing item-gen pipeline. **Sub-program of:**
[empire-development-map.md](empire-development-map.md), the sub-program `scoped-inventory-hierarchy`
(spec'd, unbuilt) hard-blocks.

## What this program is

**Sector**- and **Empire**-scope Wonders only (World/Multiverse explicitly deferred, per the idea doc's
own /spec-kickoff resolution) — a special structure tier, funded by relics (rolled unique items minted
across delve/expedition/world-map-assault/quest simultaneously) plus the existing `RubbleStock`/
`IronworkStock` basic-material cost every structure already uses, granting a loam-generation-rate boost
via a closed, single-member-today `WonderEffectKind` vocabulary designed to grow later without a schema
rewrite.

## What it is not

- **Not World or Multiverse scope.** Both are real, named, and locked as *out of this map* — `World`
  needs the same `LoamProduction.For` plumbing looped over every faction plus a wonder-race mitigation
  (already decided: Zomboss can compete); `Multiverse` needs a wholly new player-scoped, world-surviving
  ledger table that nothing in this codebase has a precedent for yet. Both wait for their own future map.
- **Not the sector-storage/legion-cargo build.** `scoped-inventory-hierarchy` is spec'd, not built.
  This program's Wonder-*build flow* (spending a legion-carried relic at a sector) cannot run
  end-to-end until that program ships — named as a hard external dependency, not re-solved here.
- **Not a seedsmith unit-pipeline extension, not a multi-empire program, not sector-to-sector trade
  routes.** All three are real, separately-tracked future work this map does not touch.
- **Not a rewrite of `StructureCatalog`/`LoamProduction`.** Every module below extends the existing
  catalog and production pipeline; none forks a parallel one.

## Modules

Stable kebab-case ids.

| # | Module id | Responsibility | Depends on | Wave |
|---|---|---|---|---|
| 1 | `relic-item-kind` | Relics as rolled, never-equipped unique items: resolves the real schema question named in the ideal doc (does `unique`'s `KindSpec` grow a no-`baseType` variant, or does a relic register as its own sibling `DropEntryKind`/item kind?), wires the four already-confirmed drop-table `SourceKind`s (`world-sector`/`expedition-tier`/`dungeon-room`/`dungeon-clear`/`dungeon-quest`/`siege-assault`) to carry relic rows, names the exact table/row/rate for each (owner question, still genuinely open — this module's own spec-time content call). Lawn (`pvz-run`) stays excluded, an unrelated pre-existing gap. | — | 1 |
| 2 | `wonder-structure` | The Wonder as a buildable structure: resolves whether it needs its own `StructureKind` value (a 7th, since `ItemStorage` just claimed the 6th) or an orthogonal tag/field on an existing kind — an evidence-based call this module makes fresh, not assumed. Ships the closed `WonderScope` (`Sector`/`Empire` only — `World`/`Multiverse` reserved-but-unregistered, matching this repo's own anti-inert-vocabulary discipline), `Rarity` (`Common`/`Unique`, per-scope cap, never hard-coded to 1), and the closed `WonderEffectKind`/`WonderEffectScope`/`WonderEffectDef` vocabulary shipping exactly one member (`LoamGenerationRate`) with `DefensePower`/`AuraGrant`/`EmpireBuff` named-but-unregistered. Basic-material cost reuses `ConstructRubbleCost`/`ConstructIronworkCost` — no new material field. | — | 1 |
| 3 | `wonder-effect-empire` | Wires a built Wonder's effect into the real loam economy: the `LoamProduction.For` signature change (a faction-level input, real gap, confirmed this session), the first real reader for `WorldFaction.ScopeModifierMilli` (hashed, replay-safe, zero consumers today), and the combination rule for multiple simultaneous `Empire`-scope Wonders on one faction (this doc recommends SUM, matching `LoamProduction`'s own existing additive discipline for structure yields — adopted here as the working design, reviewable at build time). `Sector`-scope effects need no faction-level plumbing (they read the local sector directly, the same shape a Well already does) — this module's real new work is the `Empire`-scope path specifically. | `wonder-structure` | 2 |
| 4 | `wonder-build-flow` | The actual construction verb: spend a relic (owned, unassigned, reachable per whatever scoped-inventory-hierarchy state it's sitting in — legion cargo or sector storage) plus `RubbleStock`/`IronworkStock` to construct a Wonder at a sector, subject to its `Rarity` cap. **Cannot run end-to-end until `scoped-inventory-hierarchy` is built** (external dependency, hard block, already named in that program's own map) — this module's spec is written now so the recipe/verb shape is ready the moment that dependency lands, not so it can be built before then. | `relic-item-kind`, `wonder-structure`; external `scoped-inventory-hierarchy` (built) | 2 |

**Dependency direction, no cycles.** `relic-item-kind` and `wonder-structure` are independent (both
wave 1 — minting a relic and defining what a Wonder *is* don't reference each other). `wonder-effect-
empire` only needs `wonder-structure` (the effect vocabulary it wires in). `wonder-build-flow` needs
both wave-1 modules (it spends a relic to build a structure) plus the external program.

## Build order

```
Wave 1  relic-item-kind ∥ wonder-structure
Wave 2  wonder-effect-empire ∥ wonder-build-flow
```

**Why this order.** The two wave-1 modules answer two independent questions ("what is a relic" and
"what is a Wonder") that don't need each other to be *specified*, even though they need each other to
be *spent*. Wave 2's two modules are also independent of each other (one wires the effect into the
economy, the other wires the spend-to-build verb) but both need `wonder-structure`'s vocabulary to
exist first. `wonder-build-flow` additionally can't actually *run* until `scoped-inventory-hierarchy`
ships — its spec is written in this wave regardless, since the recipe shape doesn't change once that
lands, only the runtime call site does.

## External dependencies

| Dependency | Owner map / module | What this program needs | Gate |
|---|---|---|---|
| `scoped-inventory-hierarchy` (legion-cargo, sector-storage, cargo-transfer) | `scoped-inventory-hierarchy-map.md` | A real place to spend a relic from — a legion's cargo or a sector's storage | before `wonder-build-flow` runs (spec may still be written) |
| ~~`unique` `KindSpec` schema relaxation, or a sibling relic kind~~ — **self-resolved 2026-09-13**, no cross-program change needed | item program (`item-map.md`, still no formal ask table there) | `spec-relic-item-kind.md` designed a new sibling `relic` `KindSpec`/`DropEntryKind.Relic`/`ContainerKind.Relic` unilaterally, on the reasoning that it's additive (matching the `Gem`/`Charm`/`Combo` precedent), never touching `unique`'s own surface. **Genuinely open, not yet asked:** does this still need a formal item-program sign-off before `relic-item-kind` builds, since it registers 3 new members in that program's own closed vocabularies? (strengthen-pass finding, not yet resolved) | before `relic-item-kind` ships |

## Tunables

| Number | Owner | Notes |
|---|---|---|
| Relic drop rate per confirmed loop | `data/tuning/loam-relics-wonders.v1.json` (new domain file) | Owner question, still open — a balance call for `relic-item-kind` |
| `WonderEffectDef.ValueMilli` per scope | same file | `Sector`/`Empire` rows only this wave |
| Rarity existence cap per scope | same file | Never a hard-coded `1` — a tunable count, per `Sector`/`Empire` |
| `Empire`-scope combination rule (SUM, adopted) | same file (structural constant, commented as such — the combination *function*, not a tunable number) | Reviewable at build time per the ideal doc's own flag |

## What this program does not touch

`StructureCatalog`'s existing 5 `StructureKind` values (**corrected 2026-09-13** — `spec-wonder-structure.md`
§Design 1 verified the real enum fresh and found 5, not 6; `wonder-structure` adds none, shipping a
Wonder as an orthogonal facet field instead); `LoamProduction`'s `Sector`-scope math (already proven, a
Well is the precedent); `scoped-inventory-hierarchy`'s own tables (consumed, never modified); the
multi-empire program; `World`/`Multiverse` scope (reserved vocabulary only).

## Open items carried from the ideal

Exact relic drop table/row/rate (module 1's own spec-time call); **resolved 2026-09-13** — whether
`wonder-structure` needs a new `StructureKind` value or an orthogonal tag: neither, an orthogonal
*field* on the existing record (`spec-wonder-structure.md` §Design 1, mirroring `Obstacle`'s own
precedent). **New, genuinely open (strengthen-pass finding, 2026-09-13):** does `relic-item-kind`'s
own unilateral addition of 3 new item-program closed-vocabulary members need that program's own
sign-off first (see External dependencies row above); a heavily-Wondered empire can drive decay
pressure to near-zero across its whole territory since no Wonder carries an offsetting upkeep cost —
not a mechanical cheat (nothing short-circuits, every number still flows through the real
generator→storage→decay loop), but worth a balance-pass look before Empire-scope Wonders ship in
volume.
