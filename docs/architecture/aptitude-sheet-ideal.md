# Aptitude sheet — the ideal

**Status:** idea **enriched** (E1–E8); map/specs **synced + strengthened S1–S10** ([aptitude-sheet-map.md](aptitude-sheet-map.md)).
Do not implement from the ideal alone — `/plan` next.  
**Program id:** `aptitude-sheet` (ActorSheet **Aptitudes** tab under `gui-lego` + `actor-sheet` / class-system;
shared console also hosts **empire species-build** for general creatures).  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md) → amend `/spec` → `/plan`.  
**Owner briefs:** (1) surface is low/cheap — no VFX, thin info, no icons → multi-piece frame;
(2) **commander-only allocate is a critical defect** — Aptitudes must support **every UniqueActor**,
not only the commander pool;
(3) **shared aptitude panel with empire CreatureType / species-build** — lawn general creatures fall back
to per-player-per-species allocation and need the same posture/tile/inspect grammar (owner, 2026-09-10);
(4) **allocate chrome is incomplete** — remaining points, Confirm/Cancel, auto-assign;
(5) **build presets need a serious own container + BE** — open from Aptitudes, create/setup,
select, activate; **distribution chart**; **fixed + percent min/max**; **species favour as default**
(owner, 2026-09-10 `/idea-ui`).

---

## Step 0 — principles (restated for this enrichment)

1. **Every RPG feature lives in the RPG layer.** Missing leftover / Confirm / auto-assign is never
   “because Plant has no points UI.” Aptitudes are catalog + budget + allocate APIs + Hub.
2. **Stage + layers (GG-1).** Fix the Aptitudes console over the stage — do not invent `/aptitudes`.
3. **Recipe + fold + bus — never a god TSX.** Each missing control is a **module** (piece + bus +
   optional BE), not a CSS pass on `AptitudesTab`.
4. **Theme packs own paint.** Leftover and decision chrome get themeRefs — not mute footer text.
5. **Buy before build.** Keep **recharts** (leftover gauge + **preset donut** — E4); lucide for glyphs.
6. **No engine vocabulary.** “Remaining points,” “Confirm,” “Cancel,” “Auto-assign,” “Presets” —
   not `AllocationScope` or `PointBudget`.

## Which loop this extends

| Loop / place | Role |
|---|---|
| **Spine A — Level up and power** | Free-build aptitude points are how power spreads without a class |
| **Place — Sanctum / lawn (ActorSheet)** | Band-2 sheet: UniqueActor → UniqueCreature; commander role → commander pool |
| **Place — Empire / species (Pacts layer)** | Same console bound to **speciesId**: empire species-build for **all general creatures** of that species on lawn and later world map |

This is **not** a new top-level route. It is one **aptitudes-console** recipe mounted by thin hosts
(role-keyed ActorSheet tab vs Pacts/`AptitudesLayer` species host), plus the allocate **and lawn apply**
seams those hosts need — so the UI stops lying that only the commander pool is real, stops shipping a
second god panel for species, and does not “save UniqueCreature” while the lawn still ignores it.

---

## Load-bearing principles (restated inline)

1. **Every RPG feature lives in the RPG layer.** Aptitudes are catalog + budget + Hub edges
   (`aptitude-catalog`, `aptitudes.v*.json`, `/api/aptitudes/*`, `AllocationScope`). They are never
   blocked by Plant or Zombie Unity fields. “The lawn can’t show Might” is the wrong frame.
2. **Four allocation scopes sum — commander is not the only spend surface.** Class-system lock:
   an actor’s allocation is **commander → CreatureType → Aspect → UniqueCreature**, weighted commander
   smallest / unique largest (`decisions.md` Class system; `class-system-ideal.md` §7c). A UniqueActor
   sheet that can only edit commander shares is **product-false**, not “v1 honesty.”
3. **Progression source isolation (decisions 2026-09-08).** Unique specimens and Commanders **never**
   receive empire species fallback. General lawn creatures with `EmpireGeneral` consume
   `EffectiveSpeciesAllocation(playerId, speciesId)`. The shared panel must not blur those writes.
4. **One presentation console, three host bindings — not one write API.** Shared Lego pieces; scope
   mode chooses key + read/write seam. Species writes stay on **species-build respec** (priced);
   UniqueCreature on specimen allocate; Commander on existing roster-wide endpoints.
5. **Allocate “works” only if the right runtime applies it.** Server Hub/battle consuming UniqueCreature
   does not prove the Injector lawn Bound path does. Mode A Done includes lawn wire.
6. **A game is a stage with layers, not a document with pages (GG-1).** Aptitudes open as a layer /
   sheet over the stage. Do not invent a sibling `/aptitudes` product page to “fix” the tab.
7. **Player menus are recipe + pure fold + closed bus — never a god TSX.** A host that owns
   layout, paint, joins, steppers, and inspector copy is the defect. Each player bug is one or more
   **modules** (piece + optional theme pack + fold slice + HTML draft).
8. **Theme packs own paint.** Pieces declare slots; packs supply `css` + `paint` hex + optional
   `vfx`. Mute bordered tiles and plain text inspector are Lego violations, not taste.
9. **Buy before build for presentation.** Leftover already uses **recharts** — keep it; promote
   to a piece. Icons use **lucide** / catalog glyph keys — do not invent a private SVG registry.
   Fat chunk → split; do not ban libs.
10. **No engine vocabulary on the player surface.** No raw posture ids (`force`), no channel ids,
    no “fed by combat.power.fire” as product chrome — fiction readings + catalog displayNames
    (“Sunflower’s build”, not `AllocationScope.CreatureType`).
11. **HTML draft fidelity.** Author `docs/design/gui-lego/pieces/*.html` + recipe before treating
    React as done. Dual hosts (`AptitudesTab`, `AptitudesPage`, `SpeciesBuildPanel`) collapse to
    **one** composition with different bindings.

---

## UI ↔ BE ↔ runtime coverage matrix

End-to-end truth for player allocate — not “does a store row exist.”

| Mode | BE player API | FE host today | Runtime consumer | Bucket |
|---|---|---|---|---|
| **A UniqueCreature** | **Missing** GET/POST | ActorSheet `AptitudesTab` still commander (`AptitudesTab.tsx`; ignores `instanceId`) | Server Hub/battle **yes** (`UniqueActorHubCompose.cs` ~43–60; `WebMatchService` unique channel mods); **Injector lawn no** (`RpgClient.RefreshCommanderAllocationAsync` ~401–441; `CheatState.SpeciesAllocation` = commander+species only) | **Wiring gap / critical** (API + FE + lawn wire) |
| **B CreatureType** | **Built** — `GET /api/aptitudes/species/{playerId}/{speciesId}` (effective + baseline); write `POST /api/species-build/respec` + price GET (`AptitudeEndpoints.cs` ~67–73; `SpeciesBuildEndpoints.cs`) | `SpeciesBuildPanel` / `useSpeciesBuild` — parallel god UI; also via `AptitudesLayer` from Pacts | Injector + lawn generals **yes** | BE **Built**; FE **Built, defective** presentation |
| **C Commander** | **Built** — `GET /api/aptitudes/{playerId}`; `POST /api/aptitudes/allocate` hardcoded Commander | AptitudesTab (wrong default for specimens), AptitudesPage, AptitudesLayer commander tab | Injector + Hub **yes** | **Built**; **Built, defective** as UniqueActor sheet SSOT |

```text
FE today                    BE today                         Runtime today
─────────                   ────────                         ─────────────
AptitudesTab ─────────────► GET/POST Commander ────────────► Injector Cmd+Species
SpeciesBuildPanel ────────► Species GET + respec ──────────► Injector Cmd+Species
                            UniqueCreature GET/POST MISSING ──► Server Hub Cmd+Unique
                                                             Injector Bound unique: NO UniqueCreature
```

**SignalR today:** `AptitudesUpdated` payload is `{ playerId }` only
(`AptitudeEndpoints.cs` / `SpeciesBuildEndpoints.cs` broadcast helpers). FE invalidates commander +
speciesAptitudes queries (`hub-provider.tsx` ~144–150) — no unique/specimen query key yet.

**Commander GET honesty:** nested `species` map already embeds **EffectiveSpeciesAllocation** for
levelled species (`AptitudeEndpoints.ProjectState` ~101–110), but FE `AptitudesState` omits that
wire field (`types.ts` ~449–457) — type/wiring gap for Mode B consumers that might share the bus.

**No player GET returns UniqueCreature shares today** — not sheet DTO, not commander GET, not species GET.

---

## Debate locks (D1–D5) — ideal law

### D1 — Mode A is free-build, not species-effective

Species GET returns **effective** shares (persisted CreatureType override if nonzero total, else computed
plan baseline via `EffectiveSpeciesAllocation`). Unique Hub uses **raw**
`LoadAllocation(UniqueCreature, instanceId)` — empty means **no unique contribution** (commander only).

`UniqueCreatureAllocation.Baseline` exists for **passive-tree point gates** (specimen level → budget),
**not** to auto-fill combat allocation. Mode A GET = **persisted shares + specimen budget / leftover /
specimenLevel (and Θ as needed)**. Do **not** invent `EffectiveUnique` / Hub auto-baseline without a
separate ADR that changes Hub math.

### D2 — ActorSheet host is role-keyed

| ActorPanel role | Aptitudes mode |
|---|---|
| UniqueActor / creature (`instanceId` present) | **Mode A** UniqueCreature |
| Commander (`role === "commander"`) | **Mode C** commander allocate |

`ActorPanel.tsx` already distinguishes commander vs `instanceId` (~74–76). Demanding UniqueCreature on
Dave’s sheet is wrong. Mode C remains legal on the **commander** sheet and on Pacts; illegal as the
default for a UniqueActor Aptitudes tab.

### D3 — “Allocate works” includes lawn apply for Bound uniques

Shipping Mode A API+FE alone still leaves lawn Bound specimens on Injector `SpeciesAllocationSource`
(commander+species). Server Hub proving UniqueCreature for sheet/battle is **not** lawn proof.

**Amended 2026-09-12:** until Bound Hot uses UniqueCreature, this is an **FSM / ActorHub split** —
same Hub consumers, different aptitude input — not “lawn unfinished while sheet Done.” Dual resolve
is out of order under the sole Hot compose gate. Vocabulary for combat power vs level/Θ:
[combat-power-number-ideal.md](combat-power-number-ideal.md) (HF-lawn).

**Done gate:** module **`aptitude-unique-lawn-wire`** — Injector cache + resolve UniqueCreature per Bound
`instanceId` on aptitude reload / bind (same reload cadence as commander/species). In-program; not a
silent “Hub already does it.”

### D4 — Shared console ≠ shared write contract

| Mode | Write |
|---|---|
| A | UniqueCreature allocate POST — never species |
| B | `POST /api/species-build/respec` only — **never** reopen free `/api/aptitudes/species/allocate` |
| C | Existing commander allocate |

SignalR must carry **`scope` + key** (`unique`+`instanceId` | `species`+`speciesId` | `commander`) plus
`playerId` so FE invalidates the right query keys.

### D5 — Stale doc debt is in-program

First `/spec` wave must overturn (not leave contradicting Done):

| Doc | Stale claim |
|---|---|
| [actor-sheet/spec-aptitudes-tab.md](actor-sheet/spec-aptitudes-tab.md) | “v1 allocate = commander scope”; UniqueCreature out of scope |
| [class-system/spec-aptitude-allocation-surface.md](class-system/spec-aptitude-allocation-surface.md) | commander-only; UniqueCreature waits on specimen picker |
| [guide/mechanisms/aptitudes.md](../guide/mechanisms/aptitudes.md) | commander-scope live; other scopes still ahead |

---

## Allocate chrome — owner bugs (2026-09-10 `/idea-ui`)

Player report: the Aptitudes screen does not feel like an allocate surface — no clear distribute,
no remaining points, no Confirm/Cancel, no auto-assign, no presets.

### Audit (what code actually does)

| Player ask | Code today | Bucket |
|---|---|---|
| **Distribute / assign stats** | Tile `+`/`−` call `allocation.setValue` (`AptitudesTab.tsx` ~163–164; `AptitudeTile.tsx` ~38–56) | **Built, defective** — mute mini steppers; wrong scope on UniqueActor (commander); no affordance that this *is* the spend verb |
| **Show remaining points** | Shell footer `LeftoverBar` when tab=aptitudes or draft dirty (`ActorPanel.tsx` ~77, ~125–156; `LeftoverBar.tsx` ~1–42) | **Wiring gap / Built, defective** — math exists and reports upward (`onDraftState` leftover); chrome is a **tiny footer chart** that **replaces** Deploy/Release, easy to miss, **not in the console band** |
| **Confirm / Cancel** | Footer **Confirm** + **Reset** (GG-63); Confirm disabled until dirty+withinBudget (`ActorPanel.tsx` ~131–147) | **Wiring gap / Built, defective** — decision controls exist but only in shell footer; no **Cancel** fiction (Reset); invisible if player never notices footer |
| **Auto-assign** | No button / helper | **Real gap** |
| **Build presets** | No store/API/console; Vision guide = full synergy only | **Real gap** (serious UI+BE required) |

### Debate locks (D6–D9) — allocate chrome

#### D6 — Leftover must live in the console band

GG-63 still allows shell Confirm. That does **not** license hiding remaining points.  
**Lock:** `leftover-gauge` is a **first-class console piece** (hero strip), always visible in Modes
A/B/C while allocate is open. Shell footer may **mirror** Confirm — it is not the only place leftover
appears. Copy: **“Remaining points”** (fiction), not only “Leftover.”

#### D7 — Decision strip is a piece; Cancel = discard draft

**Lock:** piece `allocate-decision-strip` with **Confirm** + **Cancel** (Cancel = discard draft /
`revert`, same as today’s Reset). Disabled rules unchanged (dirty, withinBudget, saving).  
Do **not** nest a band-3 dialog for Confirm (GG-63). In-console strip + optional shell mirror.

**Scope of Confirm (E1):** Confirm commits **draft** changes from tile steppers, Auto-assign, or
**Apply to draft**. It is **not** required after **Activate** (Activate has its own commit path).

#### D8 — Auto-assign is draft-first; Activate commits (E1)

**Lock — two verbs, two paths:**

| Gesture | Effect |
|---|---|
| **Auto-assign** | Fills the **draft** only (never a silent POST). Rules: Even · Posture lean · **Active preset** · **Species favour** (Mode A/B, D14). Then player uses **Confirm**. Overspend refused. |
| **Apply to draft** (preset console) | Loads materialized shares into draft only; **Confirm** on aptitudes console finishes |
| **Activate** (preset console) | Sets **active** for this binding **and** commits live allocation immediately: Mode A → UniqueCreature POST; Mode C → commander POST; Mode B → **priced** `species-build/respec` with **price shown before Activate** (same law as Confirm). Aptitudes Confirm strip is **not** used for this path |

No new private power curve — shares still under PointBudget after D13 materialize.

#### Debate enrich (E1–E8) — binding (2026-09-10 audit)

| Id | Tension | Lock |
|---|---|---|
| **E1** | Activate commits vs Confirm strip | Split verbs — see D8 rewrite |
| **E2** | D13 clamps can break sum-to-budget | **Leftover unspent is legal**; no silent redistribute into other aptitudes. Chart/preview shows leftover. Refuse only when one aptitude has `lo > hi` |
| **E3** | D1 empty UniqueCreature vs D14 favour | Favour is **New-preset / Auto-assign seed only** — never Hub EffectiveUnique / never auto-fill combat |
| **E4** | D12 chart grammar open | Lock **recharts donut** for 12 aptitudes (posture theme colors as segment groups). Optional small posture stacked bar only — not pie/radar as peers |
| **E5** | Template sum underspecified | On Save: target **permille must sum to exactly 1000**. Abs min/max are constraints at materialize — not a second sum SSOT |
| **E6** | Active + level-up | Active = remembered intent; **no auto rematerialize on level-up** in Wave 1. Player Auto-assign / re-Activate. Defer “keep aligned” |
| **E7** | Ideal ahead of map/specs | **Closed by `/spec` (2026-09-10)** — map + module specs extended for D6–D14 / E1–E8; see map Wave 3 |
| **E8** | “max 1000” vs PS-8 | Per-aptitude abs max is a **tunable soft constraint on a preset row**, not a global progression hard ceiling. Budget itself stays uncapped by endless-grind SSOT |

#### D9 — Aptitude build presets: own container + BE (owner overturn 2026-09-10)

Owner: presets need **serious design with their own container** — open from Aptitudes, **set up**
own presets, **select** and **activate**. Serious **UI and BE**. Thin chip-only / catalog-static
vectors are **not** enough.

| Kind | In aptitude-sheet? | Meaning |
|---|---|---|
| **Aptitude build presets** | **Yes — Done gate** | Player-authored (plus seeded system) named **share templates** over the twelve aptitudes. Own nested console. Persist, edit, select, activate. |
| **Full synergy presets** (patron + relics + aptitudes + field list) | **No** | Stays [build-presets.md](../guide/mechanisms/build-presets.md) **Vision** — other program. Aptitude presets are the **first shippable slice** of that fantasy, not a substitute for the whole loadout. |

Species Mode B keeps shipped redistribution-plan baseline as a **system** template — not a second
player-preset format.

##### Preset product shape (ideal lock)

**Player language:** “Build presets” on the Aptitudes screen — save a lean, open the library, pick
one, activate it on **this** creature / commander / species binding.

**Container:** `aptitude-preset-console` — nested layer over ActorSheet / AptitudesLayer (GG-1).
Depth: stage → sheet/layer → preset console ≤ **3** (GG-10). Not a sibling `/presets` route.
Not a one-row chip bar as the only UI.

**Entry from aptitudes-console:** `preset-entry` control (“Build presets…”) opens the container;
shows **active preset name** when one is bound.

**Inside the container (serious UI):**

| Region | Job |
|---|---|
| **Gallery** | List player + system + favour seed (name, posture lean summary, active badge) |
| **Inspect / editor** | Name; twelve-aptitude editor (permille sum **1000** — E5); dual abs/% min/max; **donut chart** |
| **Actions** | **Select** · **Apply to draft** · **Activate** (commit — E1) · Save / Delete · Close |

Theme packs own gallery/editor chrome. Pieces never fetch.

##### Preset visualization + constraints

###### D12 — Distribution chart (E4)

**Lock:** Preset editor and gallery inspect show a live **recharts donut** of the twelve aptitudes as
**percent of the template** (or of budget when previewing). Segment colors follow posture theme packs
(Force / Finesse / Bastion groups). Optional companion: small posture stacked bar — **not** a second
primary grammar (no peer pie/radar).

| View | Chart shows |
|---|---|
| Editing template (no budget) | Permille → % of 1000 |
| Apply/Activate preview with budget | Scaled points + % of budget; **leftover** slice or twin leftover readout when D13 leaves remainder (E2) |

Empty / zero-budget: honest empty chart — not fake equal slices. Updates on `revision` bump.

###### D13 — Dual constraints: fixed points AND percent, min/max (AND) + leftover legal (E2, E8)

Owner example: a stat may be capped at **1000 points** *and* **10%** of the budget — both bind.

**Lock:** Each aptitude row on a player preset may carry:

| Field | Unit | Meaning |
|---|---|---|
| `minAbs` / `maxAbs` | `long` points | Absolute floor/ceiling when materialized |
| `minPermille` / `maxPermille` | 0..1000 | Floor/ceiling as share of budget (10% = **100** permille) |
| Template target | **permille** (SSOT) | Must sum to **1000** on Save (E5). Abs fields are constraints/preview only |

**Materialize rule** (widen before multiply; divide by 1000 last; `long`):

```
lo = max(minAbs, floor(budget * minPermille / 1000))   // unset abs/‰ → ignore that axis
hi = min(maxAbs, floor(budget * maxPermille / 1000))
if lo > hi → refuse (named reason) — never silent-fix the constraint pair
share = clamp(targetScaled, lo, hi)
# after all aptitudes: leftover = budget - sum(shares) is LEGAL (E2)
# do NOT silently redistribute remainder into other aptitudes
```

Per-aptitude abs max (e.g. 1000) is a **tunable soft constraint on the preset row** (E8) — not a global
progression hard ceiling. Soft max presets-per-player remains PS-8 soft. Share overspend on POST still
throws/409.

Editor: dual fields for fix number and/or percent; labels like “Might 120 · 12% · max 1000 & 10%.”

###### D14 — Species favour default (E3) — plan Built, UI wiring gap

| Finding | Evidence | Bucket |
|---|---|---|
| Classified favour → plan | `SpeciesBuildPlanner.cs`; anchors `AptitudePrimary`/`Secondary` | **Built** |
| Shipped vectors | `_species-build-plan.json`; `SpeciesBuildPlanCatalog.SharesFor` (`Program.cs` ~137–144) | **Built** |
| Species/Unique baseline helpers | `EffectiveSpeciesAllocation`; `UniqueCreatureAllocation.Baseline` | **Built** (gates / species path — **not** Mode A Hub fill) |
| Preset UI default = favour | — | **Wiring gap** |

**Lock — default seed (New preset / Auto-assign “Species favour”):**

| Binding | Default template |
|---|---|
| Mode A UniqueCreature (`speciesId` known) | `SpeciesBuildPlanCatalog.SharesFor(speciesId)` — fiction “Species favour” |
| Mode B CreatureType | Same plan — shipped favour |
| Mode C / no species | System **Even** — never invent a species favour |

**E3:** Favour seeds the **editor / Auto-assign draft** only. It does **not** make UniqueActor Hub
auto-apply plan shares (D1 free-build: empty UniqueCreature until player spends / Activates).
Copy-on-edit into player library; never mutate the generated plan file from UI.

##### Preset BE (serious — Real gap today)

Mirror item-loadout discipline — **new** aptitude-preset persistence, not FE-localStorage.

| Concern | Lock |
|---|---|
| **Template unit** | Target permille sum **1000** on Save (E5) + D13 abs/‰ constraints |
| **Materialize** | Scale + D13 clamp; leftover legal (E2); `long` math |
| **Library key** | Per `playerId` |
| **Activation key** | One active per `(scope, scopeKey)` |
| **Level-up** | No auto rematerialize Wave 1 (E6) |
| **System / favour seeds** | Even · posture leans · species favour (read plan) — copy-on-edit |
| **API sketch** | CRUD; get/set active; materialize(budget); GET favour for `speciesId` |
| **SignalR** | CRUD / active / allocate commits from Activate |
| **Caps** | Soft max presets (tunable); E8 abs max tunable; overspend 409 |

**Prior art:** item loadout; species plan; donut/% charts with caps. Failures: silent redistribute after
clamp; EffectiveUnique from favour; absolute-only presets; auto-respec on level-up without consent.

#### D9 overturn

| Was | Now |
|---|---|
| Thin preset chip bar | Own console + BE + Activate commit |
| Open chart grammar | **Donut** (E4) |
| Permille-only | **D13** dual abs & ‰ + leftover legal (E2) |
| Ignore species favour | **D14** + E3 seed-only |
| Confirm always after Activate | **E1** split verbs |

### Owner bugs → modules (allocate chrome + presets)

| # | Player bug | Module(s) | Bucket | Evidence |
|---|---|---|---|---|
| **8** | Can’t tell how to distribute | `aptitude-tile` harden; Mode A wire | Built, defective + wiring | |
| **9** | Remaining points invisible | `leftover-gauge` (**D6**) | Wiring / Built, defective | |
| **10** | No Confirm / Cancel on screen | `allocate-decision-strip` (**D7**, draft path **E1**) | Wiring / Built, defective | |
| **11** | No auto-assign | `aptitude-auto-assign` (**D8**) | Real gap | |
| **12** | No build presets / activate | `aptitude-preset-api` + `aptitude-preset-console` + `preset-entry` | **Real gap** | |
| **13** | No % distribution chart | `preset-distribution-chart` donut (**D12/E4**) | Real gap | |
| **14** | Need fix + percent min/max | D13 constraints in API + editor | Real gap | |
| **15** | Species favour not default | Wire plan catalog (**D14/E3**) | Wiring gap | |

### Spec amend list + map drift (E7) — **done** (`/spec` 2026-09-10)

Index: [aptitude-sheet-map.md](aptitude-sheet-map.md) (Wave 3 + D6–D14 / E1–E8 assumptions).  
New: `spec-aptitude-auto-assign.md` · `spec-aptitude-preset-api.md` · `spec-aptitude-preset-console.md`.  
Amended: pieces · surface-vm · host-role-gate · species-host.

---

## Critical defect — three product lies

1. **Wrong sheet scope** — UniqueActor Aptitudes edits commander only.
2. **Second species UI** — generals’ empire build lives in a mute parallel panel.
3. **Save without lawn apply** — UniqueCreature can be persisted and shown on Server Hub while Injector
   Bound uniques still resolve commander+species only.

### Architecture audit (what is true today)

| Layer | What exists | Verdict |
|---|---|---|
| **Math / SSOT** | `AllocationScope { Commander, CreatureType, Aspect, UniqueCreature }` (`AptitudeAllocation.cs`); PointBudget per scope; unique largest weight | **Built** |
| **Persistence** | `LoadAllocation` / `SaveAllocation`; UniqueCreature keyed by `instanceId` | **Built** |
| **Unique budget helper** | `UniqueCreatureAllocation` / `PointBudget.UniqueCreatureSourceFromLevel` | **Built** (gates / budget; not Hub auto-fill — D1) |
| **Hub / sheet / battle** | `UniqueActorHubCompose` commander + UniqueCreature; never CreatureType on unique path | **Built** |
| **Injector lawn aptitude** | Commander + species only (`RpgClient` ~401–441; `CheatState.SpeciesAllocation`) | **Wiring gap** for Bound UniqueCreature — D3 |
| **Empire general fallback** | `EmpireGeneral` → `EffectiveSpeciesAllocation` | **Built** |
| **Player write (commander)** | `POST /api/aptitudes/allocate` → Commander | **Built**; defective as UniqueActor default |
| **Player read (commander)** | `GET /api/aptitudes/{playerId}` (+ nested effective species map) | **Built** |
| **Species read + write** | Species GET; respec POST/price; free allocate retired | **Built** — Mode B BE ready |
| **Species FE** | `SpeciesBuildPanel` — parallel god panel | **Built, defective** |
| **ActorSheet tab** | Commander chip; draft/Confirm commander; `instanceId` unused | **Built, defective** — critical |
| **Aspect allocate** | Blocked / reverted | Stay out |
| **Historical excuse** | Specimen picker missing | **Stale** — ActorSheet **is** the picker |

### Proposed fix (ideal lock — for `/spec` to detail)

**One `aptitudes-console` with scope modes**, role-keyed hosts, scoped SignalR, and lawn UniqueCreature wire.

#### Mode A — Specimen (UniqueActor / creature sheet)

**Edits UniqueCreature for the selected `instanceId`.** Free-build (D1). Not used for commander role (D2).

| Seam | Proposed duty |
|---|---|
| **GET** | `GET /api/aptitudes/unique/{instanceId}` → **persisted** shares + budget + leftover + specimenLevel (Θ as needed). No effective-baseline fill |
| **POST** | `POST /api/aptitudes/unique/allocate` `{ instanceId, shares }` → `SaveAllocation(UniqueCreature, instanceId, …)`; 409 overspend never clamps |
| **SignalR** | `scope: unique` + `instanceId` + `playerId` |
| **FE host** | `AptitudesTab` when ActorPanel has creature `instanceId`; refuse allocate with no specimen |
| **`aptitude-scope-chip`** | Fiction: **this creature’s build**. Optional read-only commander contribution (A5b) |
| **`leftover-gauge`** | UniqueCreature leftover for this specimen |
| **Lawn wire** | `aptitude-unique-lawn-wire` — Injector applies UniqueCreature for Bound `instanceId` (D3) |
| **Must not** | Load/write CreatureType as this specimen’s spend; invent EffectiveUnique without ADR |

#### Mode B — Species (empire / general creatures)

**Same pieces, keyed by `speciesId`.** BE already Built; presentation collapses onto the console.

| Seam | Proposed duty |
|---|---|
| **GET** | Existing `GET /api/aptitudes/species/{playerId}/{speciesId}` (effective `shares` + `baseline` + `hasOverride`) |
| **POST** | Existing `POST /api/species-build/respec` — never reopen free species allocate |
| **SignalR** | `scope: species` + `speciesId` + `playerId` |
| **FE host** | **Locked:** collapse `SpeciesBuildPanel` into shared console under existing **Pacts → `AptitudesLayer`** door (`PactsLayer.tsx` already mounts the layer with `speciesId`). Lawn species glance = Wave 2 optional, not first door |
| **`aptitude-scope-chip`** | Fiction: **“{SpeciesName}’s build”** |
| **Species-only chrome** | Baseline vs override, respec **price before confirm**, revert-to-shipped **free** |
| **`leftover-gauge`** | CreatureType leftover from species level / PointBudget |

#### Mode C — Commander (commander sheet + Pacts; not UniqueActor default)

| Seam | Proposed duty |
|---|---|
| **GET/POST** | Existing commander endpoints |
| **FE host** | ActorSheet when `role === "commander"`; AptitudesLayer / Pacts commander tab; Dave flows |
| **Illegal** | Default allocate path on a UniqueActor Aptitudes tab |

| Aspect | Stay out until owner reopens |

**Rejected “fixes”**

| Reject | Why |
|---|---|
| Keep commander-only “until UniqueCreature later” | Critical defect; Hub already sums UniqueCreature |
| API+FE Mode A without lawn wire | Lie #3 — save without lawn apply (D3) |
| EffectiveUnique / Hub auto-baseline | Changes combat math; needs ADR (D1) |
| UniqueCreature allocate on commander sheet | Role gate (D2) |
| Edit all four scopes on one board | Noise; Aspect blocked; modes exclusive per host |
| CreatureType write on UniqueActor tab | Isolation lock |
| Infer UniqueCreature from typeId / speciesId | Decisions forbid type→progression inference |
| FE-only fake specimen shares | Must hit `SaveAllocation(UniqueCreature, instanceId)` |
| Second god TSX for species forever | Shared panel; BE ready |
| Reopen free species allocate POST | Bypasses respec pricing |
| Thin SignalR `{ playerId }` forever | Dual/triple hosts need scope+key (D4) |

---

## What this is (player language)

When you open **Aptitudes** on a UniqueActor, you shape **that creature’s** free-build: three
postures, twelve named aptitudes, leftover UniqueCreature points, inspect fiction — with optional
honesty that a small commander-wide allocation still adds on top. Spending those points must also
matter when that creature is Bound on the lawn.

When you open **Aptitudes** for a **species** (from Pacts / species build), you see and can override
**that species’ empire build** — the same board language — knowing every lawn general of that
species shares it. Respec may cost Souls; reset to the shipped plan is free.

When you open **Aptitudes** on the **commander**, you edit the small roster-wide pool — not a fake
specimen key.

Not a commander spreadsheet pasted onto every specimen, not two unrelated aptitude UIs, and not a
save that the lawn ignores.

---

## What already exists

### Built

| Finding | Evidence |
|---|---|
| Four-scope enum + PointBudget | `AptitudeAllocation.cs`; tuning `aptitudes.v8.json` |
| UniqueCreature persist + budget helper | store; `UniqueCreatureAllocation.cs` (D1: not Hub auto-fill) |
| Hub/battle sums commander + UniqueCreature | `UniqueActorHubCompose.cs` ~42–60 |
| Empire general → species allocation on lawn | `spec-general-empire-fallback.md`; injector species cache |
| Commander allocate GET/POST + 409 | `AptitudeEndpoints.cs` ~26–58, 95–121 |
| Species GET + respec POST/price | `AptitudeEndpoints.cs` ~67–73; `SpeciesBuildEndpoints.cs` |
| Commander GET nested effective species map | `AptitudeEndpoints.ProjectState` ~101–110 |
| SignalR `AptitudesUpdated` (thin payload) | broadcast helpers; FE invalidate ~144–150 |
| Sheet tab mounts `AptitudesTab` | `ActorPanel.tsx` ~267–269 |
| Role split commander vs instanceId | `ActorPanel.tsx` ~74–76 |
| Species panel + Pacts layer door | `SpeciesBuildPanel`; `PactsLayer` → `AptitudesLayer` |
| Catalog tiles / draft / leftover / Confirm | catalog; `useAllocationDraft`; `LeftoverBar`; GG-63 |
| Derived `aptitude.*` fiction + `bucket.aptitude` | `derivedCook.ts`; theme packs |

### Wiring gap

| Finding | Evidence | Note |
|---|---|---|
| **UniqueCreature player GET/POST missing** | No `/api/aptitudes/unique*` | Critical — Mode A API |
| ActorSheet creature tab → commander hooks | `AptitudesTab.tsx` | Wire Mode A; commander role → Mode C |
| **Injector Bound unique ignores UniqueCreature** | `RpgClient` ~401–441; `CheatState.SpeciesAllocation` | **`aptitude-unique-lawn-wire` Done gate** |
| Species FE second panel | `SpeciesBuildPanel` vs tab | Collapse onto console Mode B via AptitudesLayer |
| FE `AptitudesState` omits nested species map | `types.ts` ~449–457 | Bus honesty for Mode B |
| SignalR payload thin | `{ playerId }` only | D4 — scope + key |
| Theme / inspect / Lego unused | packs; inspector; hand JSX | Presentation modules |
| Nested AptitudesPage labels | `AptitudesPage.tsx` | Same recipe |

### Real gap

| Finding | Evidence |
|---|---|
| No `aptitudes-console` recipe / aptitude piece drafts | gui-lego recipes / pieces |
| Catalog has **no `icon`** field | `aptitude-catalog.v1.json` |
| Per-posture theme packs + VFX | `bucket-aptitude.json` vfx null |
| RecipeMount thin hosts | god compositions |
| Species-mode Lego chrome (baseline / price / revert) | must land as piece slots |
| UniqueCreature allocate API + FE + lawn wire | Critical defects above |
| **Aptitude build-preset library + activate API** | No store/endpoints today | **D9** |
| **`aptitude-preset-console` gallery/editor** | No recipe/pieces | **D9** |
| **`preset-distribution-chart`** | — | **D12** |
| Dual abs + ‰ min/max on presets | — | **D13** |
| Species favour as preset default | Plan **Built** (`SpeciesBuildPlanCatalog`); UI unwired | **D14 wiring** |
| **Auto-assign** draft helper | — | **D8** |

### Built, defective

| Finding | Evidence |
|---|---|
| **Commander-only chip as UniqueActor sheet SSOT** | Tab + stale specs (D5) | **Critical** |
| Mute cheap chrome (sheet + species) | tiles; SpeciesBuildPanel | |
| Dual / triple UX | Layer / Page / SpeciesBuildPanel | |
| Menu queue **P4** | `menu-refactor-queue.md` | |
| Stale “commander-scope v1” docs | D5 list | Amend on `/spec` |

---

## Owner bugs → module breakdown

| # | Player bug | Module(s) | Bucket |
|---|---|---|---|
| **0** | **Commander-only — can’t build UniqueActors** | `unique-allocate` (map id; ideal alias `aptitude-unique-allocate`) + FE draft keyed by `instanceId` + scope chip + role gate | **Wiring gap / critical** |
| **0b** | **Generals / species build is a second mute panel** | `aptitudes-console` Mode B + collapse `SpeciesBuildPanel` under Pacts/`AptitudesLayer` + respec chrome | **Wiring + presentation** (BE Built) |
| **0c** | **Saved UniqueCreature ignored on lawn Bound** | `aptitude-unique-lawn-wire` (Injector cache/resolve per Bound `instanceId`) | **Wiring gap / critical** |
| **8** | Can’t clearly distribute | `aptitude-tile` harden + Mode A wire | Built, defective |
| **9** | Remaining points invisible | `leftover-gauge` in console (**D6**) | Wiring / Built, defective |
| **10** | No Confirm/Cancel on screen | `allocate-decision-strip` (**D7**, draft path **E1**) | Wiring / Built, defective |
| **11** | No auto-assign | `aptitude-auto-assign` (**D8**; Even / posture / active / favour) | Real gap |
| **12** | No build presets / can’t set up & activate | `aptitude-preset-api` + `aptitude-preset-console` + `preset-entry` (**D9**) | **Real gap** |
| **13** | No % distribution chart | `preset-distribution-chart` donut (**D12/E4**) | Real gap |
| **14** | Need fix + percent min/max | Preset constraints (**D13/E2 leftover legal**) | Real gap |
| **15** | Species favour not shown as default | Wire plan catalog (**D14/E3** seed only) | Wiring gap |
| 1 | Feels low / cheap; no frame | `aptitudes-console` + `aptitudes-layout` | Real gap |
| 2 | No icons | Catalog `icon` + `aptitude-tile` glyph | Real gap |
| 3 | No VFX | Posture packs `vfx.select` | Real gap |
| 4 | Lack of information | `aptitude-inspect` (+ read-only commander contribution in Mode A) | Wiring + real |
| 5 | God TSX | RecipeMount thin hosts | Real gap |
| 6 | Raw posture ids | `posture-band` + display titles | Built defective |
| 7 | Dual/triple hosts | One recipe, three bindings | Built defective |

### Shared reuse map

| Piece / pack | Reuse |
|---|---|
| `split-inspect` / `inspect-pane` / `source-list` | Derived inspect grammar |
| `chip` | Scope + posture chips |
| `LeftoverBar` → `leftover-gauge` | Shell Confirm GG-63 (Mode A/C); Mode B priced Confirm |
| `bucket.aptitude` | Derived source buckets only |
| Commander GET/POST | Mode C — commander sheet + Pacts |
| Species GET + respec POST | Mode B — no new allocate write |
| `useAllocationDraft` | Shared draft math; Mode B keeps respec mutation |

---

## Prior art (outside the repo)

| Source | Concrete takeaway | Failure mode |
|---|---|---|
| [Owlcat / Anathema trait controls](https://github.com/anathema/anathema_legacy/wiki/Character-Management) | Overview tracks free dots; purple = remaining; red = overspend | Hidden point totals |
| [Yanfly Stat Allocation](https://yanfly.moe/wiki/Stat_Allocation_(YEP)) | Explicit Revert on allocate scene | No discard path |
| [TestDome Character Points](https://www.testdome.com/questions/javascript/character-points/157923) | Remaining total always visible beside +/- | Points only in a footer |
| [PoB / PoE auto attribute](https://github.com/PathOfBuildingCommunity/PathOfBuilding-PoE2/issues/1489) | Auto-assign is config → apply with confirm; weights not silent save | Silent auto-POST |
| Genre / Crystal preset gallery | Select → load; save/overwrite/delete; own manager | Chip-only “presets” |
| Opsive / s&box loadout presets | Named slots; confirm overwrite/delete | Silent overwrite; absolute points that break on level-up |
| Item `rpg_item_loadout` in this repo | Persist library + entries; player-scoped | Reinventing localStorage presets |

**Implication:** Remaining points + Confirm/Cancel on the allocate board. Auto-assign fills draft
(or uses active preset). **Build presets** are a nested console + BE library with Activate — not a
chip row. Full synergy Vision stays out.

---

## The shape — chosen frame (multi-piece, unique designs)

**Chosen:** `aptitudes-console` (modes A/B/C) + **lawn UniqueCreature wire** + **`aptitude-preset-console`**
(own nested container + BE).  
Rejected: restyle-only; thin preset chip bar as sole preset UI; Vision full synergy inside this
program; silent auto-POST; footer-only leftover.

```text
┌─ aptitudes-console ─────────────────────────────────────────┐
│  [scope-chip] [leftover: Remaining] [Auto-assign]           │
│  [preset-entry: Build presets… · active name]               │
│  posture bands → aptitude-tiles · inspect · decision-strip  │
└─────────────────────────────────────────────────────────────┘
        │ open
        ▼
┌─ aptitude-preset-console (nested layer) ────────────────────┐
│  Gallery │ Editor + [donut chart % · leftover readout]      │
│  Dual fields: abs + % · min/max · Species favour seed       │
│  Select · Apply to draft · Activate (commits) · Save/Delete │
└─────────────────────────────────────────────────────────────┘
```

### Piece catalog — each piece has a unique design job

| Module id | Unique design intent | Payload / theme |
|---|---|---|
| **`aptitude-scope-chip`** | Seal for **this binding** — specimen / species / commander | Side/neutral; Mode A optional commander add-on |
| **`leftover-gauge`** | **Remaining points** in console hero (**D6**) — not footer-only | Overspend pulse; recharts |
| **`allocate-decision-strip`** | Confirm + Cancel on the board (**D7**); shell may mirror | Bus confirm/cancel |
| **`aptitude-auto-assign`** | Draft fill: Even / posture / active / **favour** (**D8/E3**) | Fold rules; no silent POST |
| **`preset-entry`** | Opens preset console; shows active name | Bus `preset.open` |
| **`aptitude-preset-console`** | Gallery + editor + Activate commit (**D9/E1**) | Own recipe + theme |
| **`preset-distribution-chart`** | Live **donut** % of primaries (**D12/E4**) | recharts; leftover twin (E2) |
| **`aptitude-preset-api`** | CRUD + active + D13 materialize + leftover legal + favour read | Store + plan; no level-up autorespec (E6) |
| **`posture-band`** | Three distinct band chrome | `posture.*` packs + non-null `vfx.select` |
| **`aptitude-tile`** | Icon + name + value + **obvious** steppers | Posture `themeRef`; catalog `icon` |
| **`aptitude-inspect`** | Dossier + fed families | Edge join → displayNames |
| **`species-build-chrome`** (Mode B) | Baseline vs override, price chip, free revert | Species-build tunables |
| **`posture-balance`** (optional) | Points per posture on this binding | recharts; deferred OK |
| **`aptitudes-layout`** | Landmarks / no surplus scroll | Structural CSS |
| **`aptitude-unique-allocate`** | GET/POST UniqueCreature by `instanceId` | Map id **`unique-allocate`** (S9) — Server + FE Done gate |
| **`aptitude-unique-lawn-wire`** | Injector Bound UniqueCreature apply | Map id **`unique-lawn-wire`** (S9) — unique GET only (S4) |
| **`aptitude-species-host`** | Thin Mode B mount; retire parallel panel chrome | Map id **`species-host`** |

**Rejected shapes**

| Shape | Why rejected |
|---|---|
| One CSS PR on `AptitudesTab` | Condition incident class |
| Skill-tree node graph | Wrong mechanic |
| Icons-only tiles | Owlcat failure |
| Commander-only UniqueActor allocate | **Critical defect** |
| Four-scope write board on one tab | Aspect blocked; modes exclusive |
| CreatureType on UniqueActor tab | Isolation lock |
| Keep `SpeciesBuildPanel` forever-separate | Owner: shared panel |
| Mode A without lawn wire | Lie #3 (D3) |
| Footer-only leftover / Confirm | Owner can’t find remaining points or Confirm (**D6/D7**) |
| Silent auto-assign POST | Auto-assign is draft-only (**D8**); Activate is explicit |
| Thin preset chip bar as only preset UI | Owner: own container + BE (**D9**) |
| Absolute-only presets / no % chart | **D12/D13** — chart + dual abs & ‰ |
| Re-author species favour in FE | **D14** — read `SpeciesBuildPlanCatalog` |
| Full Vision synergy presets in this program | Out — [build-presets.md](../guide/mechanisms/build-presets.md) |
| Fourth nested layer under preset | GG-10 — editor stays inside preset console |

---

## Tunables

| Kind | Home |
|---|---|
| PointBudget / edges / per-scope milli | `data/tuning/aptitudes.v{n}.json` |
| DisplayName, role, reading, **icon**, posture | `aptitude-catalog.v{n}.json` — add icon |
| Posture titles | Catalog / locale — not raw `force` |
| Paint / VFX | `themes/packs/posture-*.json` |
| UniqueCreature budget from specimen level | `PointBudget.UniqueCreatureSourceFromLevel` |
| Species respec price / decay | `data/tuning/species-build.v{n}.json` (unchanged ownership) |
| Soft max presets per player | `data/tuning/aptitudes.v{n}.json` (or aptitude-presets tuning) |
| Default / soft abs max per aptitude (e.g. 1000) | Same tuning — balance surface |
| System seed presets (Even / posture leans) | Catalog or seed rows — copy-on-edit |
| Species favour vectors | **Built:** `_species-build-plan.json` via `SpeciesBuildPlanCatalog` — do not duplicate |

Structural: leftover empty legal; overspend throws/409 never clamps (PS-8); **D13** materialize uses
`long`, widen before multiply, /1000 last; `lo > hi` refuses; Auto-assign → draft; Activate → commit;
species favour is **read**, never rewritten by player save.

---

## What this deliberately does not decide

- Aspect-scope allocate (stay out until owner reopens).
- Lawn species glance as a second Mode B door (Wave 2; first door locked to Pacts/`AptitudesLayer`).
- Aura-skill enable on this tab.
- Eighth rail entry dedicated only to Aptitudes.
- Exact lucide keys per aptitude.
- Whether commander contribution readout is a chip line or inspect footnote (spec chooses).
- Changing Hub to auto-apply UniqueCreature baseline (requires ADR — D1).
- Full synergy **build presets** (patron + relics + aptitudes + field) — Vision, other program.
- Exact abs max tunable numbers beyond “lives in tuning / preset policy” (E8).
- Exact system seed preset names beyond Even / three posture leans / Species favour.
- “Keep aligned” rematerialize on level-up (deferred — E6).

**Locked by enrich (no longer open):** Activate commits (E1/A14); Mode B price-before-Activate;
leftover legal after D13 (E2/A18); donut chart (E4/A15); favour seed-only (E3/A17); sum-to-1000
(E5); no Wave-1 level-up autorespec (E6/A19).

---

## Open questions (owner)

| Id | Question | Default if silent |
|---|---|---|
| **A1** | Confirm program id **`aptitude-sheet`** and queue pull **P4 → named stream**? | Yes — Aptitudes + shared species mode + lawn wire |
| **A2** | Three **posture theme packs** with unique VFX? | **Yes** |
| **A3** | Inspect shows **fed family displayNames** in Wave 1? | Yes |
| **A4** | Collapse `AptitudesPage` / Layer into the same recipe? | Yes |
| **A5** | ~~UniqueCreature later?~~ **OVERTURNED** — UniqueCreature allocate required for UniqueActor sheet | **Done gate** |
| **A5b** | Show read-only commander contribution on UniqueActor board? | Yes |
| **A6** | `posture-balance` in Wave 1 or defer? | Defer |
| **A7** | Keep commander allocate UI (Dave/Pacts) + commander sheet Mode C? | **Yes — keep Mode C** where role-keyed |
| **A8** | Shared console includes **species / empire mode**? | **Yes — folded**; Done gate |
| **A8b** | First species host door? | **Locked:** collapse `SpeciesBuildPanel` into shared console under existing **Pacts → AptitudesLayer**; lawn glance Wave 2 |
| **A9** | Injector UniqueCreature apply for Bound lawn uniques in-program Done gate? | **Yes** (`aptitude-unique-lawn-wire`) |
| **A10** | Commander ActorSheet uses Mode C (not UniqueCreature)? | **Yes** (D2) |
| **A11** | Leftover + Confirm/Cancel **in console band** (D6/D7), not footer-only? | **Yes** — Done gate |
| **A12** | Auto-assign in Wave 1 (draft-only fill, D8)? | **Yes** — Done gate |
| **A13** | Serious aptitude **build preset console + BE** (D9), not thin chip bar? | **Yes** — Done gate; Vision full loadout stays out |
| **A14** | Activate commits live allocate (not draft-only)? | **Locked Yes** (E1/D8); Apply-to-draft remains; Mode B price-before-Activate |
| **A15** | Distribution chart in preset console (D12)? | **Locked Yes** — **recharts donut** (E4) |
| **A16** | Dual abs + ‰ min/max constraints (D13)? | **Locked Yes** — Done gate |
| **A17** | Species favour from plan as New/default (D14)? | **Locked Yes** — seed/Auto-assign only (E3); not Hub fill |
| **A18** | After D13 clamps, is leftover unspent legal (no silent redistribute)? | **Locked Yes** (E2) — refuse only `lo > hi` |
| **A19** | Auto rematerialize active preset on level-up in Wave 1? | **Locked No** (E6) — player Auto-assign / re-Activate |

---

## The real question

Not “can we show aptitudes?” — and not only “pretty Lego tiles.”

**Will Aptitudes finally feel like free-build — right scope, remaining points, Confirm for draft /
Activate for commit, auto-assign, a real build-preset console with donut chart and abs/‰ caps,
species favour as seed, leftover legal after clamps, and lawn apply — or stay a mute half-screen?**

**Done gates:** UniqueCreature allocate · species Mode B · lawn wire · leftover+decision · auto-assign ·
**preset API + console + Activate (E1)** · **donut chart (D12/E4)** · **dual abs/‰ + leftover legal
(D13/E2)** · **species favour seed (D14/E3)** · multi-piece frame · D5 docs · E5 sum-1000 · E6 no
level-up autorespec.

**Delivery index:** [aptitude-sheet-map.md](aptitude-sheet-map.md) + [aptitude-sheet/](aptitude-sheet/).  
**Next:** `/plan` → `tasks/aptitude-sheet-plan.md` / `tasks/aptitude-sheet-todo.md`.

---

## Hand-off

**Ideal (this file):** D1–D14 + **E1–E8** debate locks; map ids aliased (**S9**).  
**Map:** [aptitude-sheet-map.md](aptitude-sheet-map.md)  
**Module specs:** [aptitude-sheet/](aptitude-sheet/)  
**Map/specs:** strengthened 2026-09-10 — **S1–S10**.  
**Next:** implement from [tasks/aptitude-sheet-plan.md](../tasks/aptitude-sheet-plan.md) /
[tasks/aptitude-sheet-todo.md](../tasks/aptitude-sheet-todo.md).

No implementation from idea/spec alone — use the plan/todo pair.
