# Capability map: aptitude-sheet

**Status:** Phase 0 — map + module specs **strengthened** (S1–S10 debate, 2026-09-10). Covers
allocate-chrome + build presets (ideal **D6–D14** + **E1–E8**).  
**Program id:** `aptitude-sheet`  
**Ideal:** [aptitude-sheet-ideal.md](aptitude-sheet-ideal.md) (enriched; E7 drift closed by this map)  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md) · [spec-driven-development](../.claude/skills/spec-driven-development/SKILL.md)  
**Parent kit:** [gui-lego-map.md](gui-lego-map.md) · [gui-lego-authoring.md](gui-lego-authoring.md)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md) **P4 → Aptitudes** claimed by this stream  
**Stale docs to overturn (D5):** [actor-sheet/spec-aptitudes-tab.md](actor-sheet/spec-aptitudes-tab.md) ·
[class-system/spec-aptitude-allocation-surface.md](class-system/spec-aptitude-allocation-surface.md) ·
[../guide/mechanisms/aptitudes.md](../guide/mechanisms/aptitudes.md)  
**Species write economy (do not fork):** [species-build/spec-allocation-surface.md](species-build/spec-allocation-surface.md) ·
[species-build/spec-species-respec.md](species-build/spec-species-respec.md)  
**Species favour plan (read-only seed):** `SpeciesBuildPlanCatalog.SharesFor` ·
`data/generated/demons/_species-build-plan.json` — **Built**; do not re-author  
**Plans (after `/plan`):** `tasks/aptitude-sheet-plan.md` · `tasks/aptitude-sheet-todo.md`  
**DESIGN-GATE (this session):** Player menus · UI · Class system / aptitudes · Demon progression source ·
species-build allocation · ActorHub (consume / existing AptitudeSubsystem — no private fold) ·
tunables / soft caps (E8)

---

## ASSUMPTIONS (correct now or these stand)

1. Ideal **A1–A19** locks stand (A14–A19 locked by enrich; silent defaults for earlier A*).
2. **D1–D14** and **E1–E8** in the ideal are binding law for every module spec.
3. Aptitude-specific piece contracts live under `aptitude-sheet/spec-*`; reuse existing gui-lego
   `chip` / `split-inspect` / `inspect-pane` by reference — do not fork mute twins.
4. **`posture-balance` deferred** (A6) — not a Done gate.
5. No Aspect allocate; no EffectiveUnique / Hub auto-baseline without a separate ADR (**E3**).
6. Menu queue **P4 Aptitudes** is this stream only — not all P4 rail layers.
7. **Activate** commits live allocate + sets active; **Confirm** is draft-only path (**E1**).
8. After D13 materialize, **leftover unspent is legal** — no silent redistribute (**E2**).
9. Preset target permille **sums to 1000** on Save (**E5**); abs min/max are constraints only.
10. **No auto rematerialize on level-up** in Wave 1 (**E6**).
11. Chart grammar = **recharts donut** (**E4**); optional posture stacked bar only.
12. Vision full synergy loadout stays **out** of this program (D9).
13. Favour GET returns **target permille** map; FE never calls C# `SharesFor` or treats species
    baseline **points** as template ‰ (**S1**).
14. Mode B Confirm/Activate price gates use in-console strip + chrome — **no ConfirmDialog** (**S2**).
15. **`POST /api/aptitude-presets/activate`** is the single transactional Activate (**S3**).
16. Lawn Bound fetch = **`GET /api/aptitudes/unique/{instanceId}`** only — no commander `uniques` map (**S4**).
17. Preset named-library prior art = **`RpgStore` item loadouts** — not aura `LoadoutEndpoints` (**S5**).
18. Scale math: `checked { (long)budget * permille / 1000L; }` (**S6**).
19. Empty favour → refuse favour seed; UI offers Even (**S7**).
20. Player fiction **Cancel**; bus/internal `revert` / `aptitude.reset` (**S8**).
21. Map module ids `unique-allocate` / `unique-lawn-wire` are canonical (**S9**).
22. FE commander GET nested `species` map typing + invalidate honesty owned by `aptitudes-live-bus` (**S10**).

---

## What this program is

Kill three product lies **and** finish allocate chrome + aptitude build presets:

1. UniqueActor Aptitudes edits **UniqueDemon** (not commander-by-default).
2. Empire species build shares the **same console** (Mode B under Pacts/`AptitudesLayer`).
3. UniqueDemon spend **applies on lawn** for Bound specimens (Injector wire).
4. Remaining points + Confirm/Cancel **in console** (D6/D7) — not footer-only.
5. Auto-assign fills **draft** only (D8); Activate is the commit verb from presets (E1).
6. Own **build-preset** library + nested console + donut + dual abs/‰ + species favour **seed**
   (D9/D12–D14) — not Vision full synergy.

Commander Mode C stays legal on the commander sheet and Pacts — never as UniqueActor default.

---

## Ownership splits (binding)

| Concern | Owner | Must not |
|---|---|---|
| UniqueDemon GET/POST + budget projection | `unique-allocate` | FE invent shares; EffectiveUnique |
| Scoped `AptitudesUpdated` payload + FE nested `species` type honesty | `aptitudes-live-bus` | Thin `{ playerId }` only forever; omit commander GET `species` from FE types |
| Injector Bound UniqueDemon apply | `unique-lawn-wire` | Claim Hub/battle as lawn proof; extend commander GET with `uniques` map |
| Species write / price / revert | Existing species-build endpoints | Reopen free `/species/allocate` |
| Mode B host collapse | `species-host` | Forever-parallel `SpeciesBuildPanel` god UI; ConfirmDialog for Confirm |
| Role-keyed ActorSheet host | `host-role-gate` | UniqueDemon on commander sheet |
| Recipe + fold + leftover/decision/auto bus | `aptitudes-surface-vm` · pieces | God TSX / footer-only leftover |
| Auto-assign draft rules | `aptitude-auto-assign` | Silent POST; Hub favour fill; FE `SharesFor` |
| Preset CRUD + active + materialize + **transactional Activate** | `aptitude-preset-api` | FE-localStorage; auto level-up respec; split active-then-allocate client |
| Preset nested console + donut editor | `aptitude-preset-console` | Chip-bar-only presets; pie/radar peers |
| Catalog glyph keys | `catalog-icons` | Private SVG registry |
| Stale commander-v1 prose | `stale-doc-amend` | Leave contradicting Done |

**ActorHub:** Aptitude magnitudes continue to **contribute** via existing `AptitudeSubsystem` /
Hub `aptitudeAllocation` delegate. This program does **not** invent a private fold. Favour seed
never auto-fills Hub UniqueDemon (**E3**).

**Magnitudes:** aptitude point shares, budgets, abs constraints are **`long`**; overspend **throws /
409**, never clamps (PS-8). Soft max presets + per-row abs max are **tunable** soft constraints (**E8**),
not global progression hard ceilings.

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `unique-allocate` | Player GET/POST UniqueDemon by `instanceId`; FE hooks; free-build contract (D1) | store + PointBudget | [aptitude-sheet/spec-unique-allocate.md](aptitude-sheet/spec-unique-allocate.md) |
| `aptitudes-live-bus` | Shared `AptitudesUpdated` shape (`scope` + key + `playerId`); FE invalidate keys | unique-allocate routes; species/commander broadcasts amended | [aptitude-sheet/spec-aptitudes-live-bus.md](aptitude-sheet/spec-aptitudes-live-bus.md) |
| `unique-lawn-wire` | Injector cache + resolve UniqueDemon for Bound `instanceId` on reload/bind | unique-allocate · live-bus | [aptitude-sheet/spec-unique-lawn-wire.md](aptitude-sheet/spec-unique-lawn-wire.md) |
| `catalog-icons` | Add `icon` to aptitude-catalog; tile glyph path | catalog SSOT | [aptitude-sheet/spec-catalog-icons.md](aptitude-sheet/spec-catalog-icons.md) |
| `posture-theme-packs` | Three posture packs + non-null `vfx.select` | theme registry | [aptitude-sheet/spec-posture-theme-packs.md](aptitude-sheet/spec-posture-theme-packs.md) |
| `aptitude-pieces` | Piece contracts: scope-chip, leftover-gauge, decision-strip, preset-entry, posture-band, tile, inspect, species-build-chrome, layout, **donut chart** | catalog-icons · posture packs · gui-lego chip/inspect | [aptitude-sheet/spec-aptitude-pieces.md](aptitude-sheet/spec-aptitude-pieces.md) |
| `aptitude-auto-assign` | Draft-only fill rules: Even · posture · active preset · species favour (D8/E3) | pieces · surface-vm · favour read from preset-api | [aptitude-sheet/spec-aptitude-auto-assign.md](aptitude-sheet/spec-aptitude-auto-assign.md) |
| `aptitude-preset-api` | CRUD library; active; D13 materialize; leftover legal; favour GET (permille); **transactional Activate** | store · SpeciesBuildPlanCatalog · live-bus · species-build respec | [aptitude-sheet/spec-aptitude-preset-api.md](aptitude-sheet/spec-aptitude-preset-api.md) |
| `aptitude-preset-console` | Nested gallery/editor; Activate via activate API; Apply-to-draft; favour New seed | preset-api · pieces · auto-assign | [aptitude-sheet/spec-aptitude-preset-console.md](aptitude-sheet/spec-aptitude-preset-console.md) |
| `aptitudes-surface-vm` | Pure fold + `aptitudes-console` recipe + bus (leftover, decision, auto, open-preset) | pieces · live-bus · auto-assign · preset-entry | [aptitude-sheet/spec-aptitudes-surface-vm.md](aptitude-sheet/spec-aptitudes-surface-vm.md) |
| `host-role-gate` | ActorPanel/AptitudesTab: creature→A, commander→C; Confirm draft vs Activate API; thin RecipeMount | unique-allocate · surface-vm · preset-console | [aptitude-sheet/spec-host-role-gate.md](aptitude-sheet/spec-host-role-gate.md) |
| `species-host` | Collapse SpeciesBuildPanel into Mode B; price on strip (no ConfirmDialog); Activate API | surface-vm · species-build BE · preset-console | [aptitude-sheet/spec-species-host.md](aptitude-sheet/spec-species-host.md) |
| `stale-doc-amend` | Overturn commander-scope v1 claims in D5 docs + queue P4 note | map approved | [aptitude-sheet/spec-stale-doc-amend.md](aptitude-sheet/spec-stale-doc-amend.md) |

**Deferred (not Done gate):** `posture-balance` (A6); lawn species glance door (Wave 2 after A8b);
“keep aligned” rematerialize on level-up (E6).

---

## Debate strengthen (S1–S10) — binding (2026-09-10 audit)

| Id | Tension | Lock |
|---|---|---|
| **S1** | Favour unit / FE `SharesFor` | Favour GET = target **permille** map; FE consumes GET only — never baseline points as ‰ |
| **S2** | Species ConfirmDialog vs GG-63 | Mode B Confirm/Cancel via decision-strip + chrome; **retire** ConfirmDialog; free paths through Confirm too |
| **S3** | Split Activate soft | **`POST /api/aptitude-presets/activate`** one txn (active + allocate/respec); hosts call only that |
| **S4** | Dual lawn fetch | Bound reload = unique GET per id + CheatState cache; **no** commander `uniques` map |
| **S5** | LoadoutEndpoints prior art | Cite `RpgStore` `rpg_item_loadout` / `ListLoadouts` / `SaveLoadout` |
| **S6** | Auto-assign overflow footgun | `checked { (long)budget * permille / 1000L; }` |
| **S7** | Empty favour | Refuse favour seed; offer Even — no silent zero draft |
| **S8** | Cancel vs Reset | Fiction **Cancel**; bus `aptitude.reset` / revert |
| **S9** | Ideal `aptitude-unique-*` vs map ids | Canonical map ids: `unique-allocate` / `unique-lawn-wire` |
| **S10** | FE omits nested `species` | `aptitudes-live-bus` owns FE type + invalidate honesty |

---

## Build order

```text
Wave 0 — truth + docs
  stale-doc-amend
  unique-allocate · aptitudes-live-bus
  (parallel: catalog-icons · posture-theme-packs)

Wave 1 — lawn + presentation contracts
  unique-lawn-wire
  aptitude-pieces → aptitudes-surface-vm
  (leftover-gauge + decision-strip + tile harden in pieces)

Wave 2 — hosts land
  host-role-gate · species-host
  HTML drafts accept → React RecipeMount thin hosts

Wave 3 — allocate chrome helpers + presets
  aptitude-preset-api  (CRUD + materialize + favour GET)
  aptitude-auto-assign (draft rules; consumes favour + active)
  aptitude-preset-console (nested layer; Activate / Apply-to-draft / donut)
  wire surface-vm bus: auto-assign · preset.open · Activate path in hosts
```

---

## Success criteria (program)

- [ ] UniqueActor Aptitudes spends UniqueDemon; commander sheet spends commander (D2).
- [ ] UniqueDemon GET returns persisted shares + budget/leftover — no EffectiveUnique (D1).
- [ ] Bound lawn unique receives UniqueDemon allocation after allocate + reload (D3).
- [ ] Species Mode B uses same console under Pacts/`AptitudesLayer`; respec priced; free allocate stays retired (D4).
- [ ] `AptitudesUpdated` carries scope+key; FE invalidates unique/species/commander queries correctly.
- [ ] Remaining points visible in console hero; Confirm/Cancel in-band (D6/D7) — not footer-only.
- [ ] Auto-assign never POSTs; Confirm commits draft; Activate commits + sets active via **activate API** (E1/D8/S3).
- [ ] Mode B shows price before Confirm **and** before Activate; **no ConfirmDialog** (S2).
- [ ] Preset library persists (not localStorage); Save requires permille sum 1000 (E5).
- [ ] Materialize uses D13 dual abs/‰; leftover after clamps legal (E2); `lo > hi` refuses.
- [ ] Favour GET returns permille; empty favour refused for favour seed (S1/S7).
- [ ] Bound lawn fetch uses unique GET only (S4).
- [ ] Chart is recharts **donut** (E4); species favour seeds New/Auto-assign only (E3/D14).
- [ ] No auto rematerialize on level-up Wave 1 (E6).
- [ ] FE `AptitudesState` includes nested commander GET `species` honesty (S10).
- [ ] No god AptitudesTab / SpeciesBuildPanel chrome as SSOT — recipe + pieces + theme packs.
- [ ] D5 stale docs no longer claim commander-only v1.
- [ ] Magnitudes `long`; overspend 409/throw never clamp; soft preset caps tunable (E8).

---

## DESIGN-GATE checklist

- [x] Read ideal (incl. E1–E8) + UI/menus gate rows + class-system / demon progression / species-build seams.
- [x] Verified against code: `SpeciesBuildPlanCatalog.SharesFor`, `Program.cs` catalog Configure,
      AptitudeEndpoints / species-build / UniqueActorHubCompose / RpgClient lawn resolve (prior + this pass).
- [x] ActorHub: consume/contribute via existing aptitude path — no private fold; favour ≠ Hub fill.
- [x] Tunables/catalog homes named; soft max presets + row abs max in tuning (E8).
- [x] Caps: overspend throws/409 (PS-8); leftover empty legal; D13 leftover after clamp legal (E2).
- [ ] Full guard/test sweep — deferred to `/plan` implementation waves.

---

## Hand-off

**Map:** [docs/architecture/aptitude-sheet-map.md](aptitude-sheet-map.md)  
**Specs:** [docs/architecture/aptitude-sheet/](aptitude-sheet/)  
**Next:** implement from [tasks/aptitude-sheet-plan.md](../tasks/aptitude-sheet-plan.md) /
[tasks/aptitude-sheet-todo.md](../tasks/aptitude-sheet-todo.md) (never bare `tasks/plan.md`).
