# Audit — effect-shaped logic across the codebase (2026-08-22)

**Purpose:** an **adoption tracker**. When the atom effect layer lands as the SSOT for how effects are defined and resolved ([effect-atom-ideal.md](effect-atom-ideal.md)), every system below has to decide whether it adopts the contract, and what changes when it does. Each stream checks itself against its own row here and updates the status when it moves.

**This is not a spec and not a work order.** No stream is obliged to adopt on our schedule. The point is that nobody discovers the coupling late.

**Method:** every claim was read out of `src/` on 2026-08-22 and carries its evidence. Where a doc and the code disagree, the code wins. Prior audit of the wider machine: [rpg-mechanism-audit-2026-08-21.md](rpg-mechanism-audit-2026-08-21.md).

**Moving-target caveat:** `Battle/Timeline/` and `World/` were both being edited during this audit. Re-read those rows before building on them.

---

## 1. Verdict

**Effect-shaped logic is authored in at least eleven places, in four different shapes, and none of them can see each other.**

- Four **content catalogs** hold effect definitions (effects, statuses, traits, items) — plus a fifth, VFX, that mirrors one of them by hand.
- Four **magnitude sites** author effect-shaped numbers directly in resolvers and policies, with no def at all.
- One **parallel modifier path** (`IStatModifierPlugin`) intentionally sits outside the effect bag and should stay there.
- Two **consumers do not exist**: no gameplay damage applier for the lawn, no AI layer.

The cost is not that any one of these is wrong. It is that a change to how effects work has eleven landing sites, and today there is no list of them. This is the list.

---

## 2. The inventory

### A — content catalogs (effect definitions in code)

| # | Site | What it holds | Evidence | On adoption |
|---|---|---|---|---|
| A1 | `EffectSeedCatalog` | 16 effect defs (trigger + FA action + params) | `Core/Effects/FoundationHarness.cs` | Becomes atom rows. First and cheapest migration; 19 JSON fixtures already have the shape |
| A2 | `StatusCatalog` | 21 status defs; ADR-locked code-first | `Core/Status/StatusCatalogBootstrap.cs` | **Kind logic stays code** — consistent with the design, since kinds are code everywhere. Only magnitudes would move, only if a status spec asks |
| A3 | `TraitBattleCatalog` | 13 traits as 15 hand-coded facet fields; 7 funnel-routed, 6 engine behaviours | `Core/Battle/TraitBattleCatalog.cs` | The 7 become containers of atoms. The 6 wait for the AI and rewards layers. Touches `RulesetVersion` and needs a golden re-bless |
| A4 | `UniqueEquipmentCatalog` | 3 stub items → grant templates; one points at a placeholder effect id | `Core/Match/UniqueEquipmentCatalog.cs` | Greenfield. Gets containers, instances, rolled values, and item power for free |
| A5 | `VfxCatalog` | Cue defs, self-described as *"C#-seeded catalog, mirroring `EffectSeedCatalog`"* | `Core/Vfx/VfxCatalog.cs:67` | **Coupled by hand today.** If effect ids move to data, this mirror breaks silently unless cues key off atom ids or the mirror is validated |
| A6 | `ShieldInnateCatalog` | Innate shield defs | `Core/Combat/Shield/ShieldInnateCatalog.cs` | Shield capacity/toughness are already derived channels; innate shields are a container-shaped thing worth revisiting when items land |

### B — effect-shaped magnitudes authored in code, with no def

These are real effects with no definition anywhere — the number *is* the design.

| # | Site | The magnitude | Evidence | On adoption |
|---|---|---|---|---|
| B1 | Patron aura | `clamp(rarityBase + 10×star + level, 0, 150)` ‰; primary element full power, half defence | `Core/Demons/Patron/PatronPolicy.cs:33` | The aura grant already exists as a marker with no overlay. Magnitudes become atom values on a container |
| B2 | Star merge | `PerStarPowerMilli = 30`, `PerStarDefenseMilli = 30` | `Core/Demons/Fusion/StarPolicy.cs:10` | A per-star container of two stat atoms, or stays a progression curve — a fusion-spec call |
| B3 | Expedition injury | writes `combat.power.omni −max(1, Atk/4)` straight onto the actor setup | `Core/Expeditions/ExpeditionResolver.cs:56,246` | Clearest case of an effect with no def. Becomes a temporary bound container — and gains a power price for free |
| B4 | Lane cost | per-lane-type `CostMultiplierMilli`, `LeyDiscountMilli = 800` | `Core/World/LaneTypeCatalog.cs`, `World/Movement/LaneCost.cs:62` | **Adjacent, probably not an atom** — terrain economics, not an actor effect. Listed so the world stream can decide, not because we claim it |

### C — parallel modifier path (stays parallel, by decision)

| # | Site | What it is | On adoption |
|---|---|---|---|
| C1 | `IStatModifierPlugin` — `CheatScaleStatPlugin`, `CheatAbsoluteStatPlugin`, `PvzStatsPlugin`, `StubStatPlugins` | Feature-owned stat contributions composed by `StatSystem`, deliberately outside the effect bag (effect-system.md decision **D9**) | **No adoption.** Cheats stay the operator path. Listed so nobody "unifies" it by accident. The shared floor is `ModifierBag` and the single Writer, which both paths already honour |

### D — consumers that do not exist

| # | Missing layer | Reality | Contract offered |
|---|---|---|---|
| D1 | **Damage consumer / applier** | No gameplay applier on the lawn. Vanilla hits run Unity `TakeDamage` and are observed only; `DamageApplyPipeline` / `OverlayCombatCalculator` are reached from battle, sim, debug, and tests — never lawn gameplay | Atoms emit **resolved contributions** (amount already rolled, element, source, target, hit). That layer owns merge, order, mitigation, apply. [effect-atom-ideal.md §5.1](effect-atom-ideal.md) |
| D2 | **AI layer** | Does not exist. Three traits (`coward`, `bloodthirsty`, `loyal`) are AI wearing an effect costume; two more (`greedy`, `genius`) are reward math | AI reads atom-declared **tags** and the **power vector / matchup read**; never atom internals, never the display scalar. Behaviours referenced from containers by id |

---

## 3. Runtime consumers — where the vocabulary can actually run

A kind may only claim a runtime where a consumer exists. Audited 2026-08-22:

| Attach point | Lawn (injector) | Battle (web engine) | Sim / offline |
|---|---|---|---|
| **Stat** | FA1 → ModifierBag → `EntityApply` → Writer — shipped, LIVE L1–L14 | `BattleStatComposer` at setup only; the bag sink **ignores** FA1 | plan only |
| **Damage** | **none** (D1) | inlined in the `BattleEngine` attack loop, hardcoded per trait | n/a |
| **Trigger** (FT*) | `EffectBag.OnEvent` from capture — shipped | **none — the engine never calls `OnEvent`** | `OnEvent` via harness / scenarios |
| **Board** | FA4–FA9 → PvzIntent — LIVE-proven | inert | recorded plan |
| **Status** | `StatusExecutor` + `StatusRuntime` — shipped | `StatusRuntime` mounted, entered only via scripted `InitialStatuses` | plan only |
| **Resource delta** | FA10 → Writer Add — shipped | `BattleEffectSink` — **the only opcode battle consumes** | plan only |

`InjectorEffectActionSink` implements all ten opcodes. `BattleEffectSink` states it in its own comment: *"battle mode consumes FA10 only; other actions are inert here."*

**Therefore: wave 1 of the atom layer is lawn-only.** Battle adoption is a later wave, gated on battle growing an `OnEvent` call and more than one consumer. Content authored for a runtime with no consumer is rejected at bind time — loudly, on purpose.

---

## 4. What "follows the effect SSOT" means

A system has adopted when all of these are true:

1. **Its effect content is atom rows**, not fields on a bespoke catalog record.
2. **Its magnitudes live in data**, not in a policy constant or a resolver expression.
3. **It does not apply anything itself** — it produces contributions or bindings and lets the owning layer apply.
4. **Its effects carry a power price**, computed by the shared cost function, override only with a note.
5. **It declares runtime support** and accepts bind-time rejection where a consumer is missing.
6. **It adds no new attach point** without an ADR, and no new Foundation primitive without one either.

A system may **decline** adoption — C1 does, by decision. Declining is a recorded choice, not a silent omission.

---

## 5. Track

Status values: `not started` · `contract accepted` · `in migration` · `adopted` · `declined`. Each stream owns its own row.

| # | System | Owner stream | Status (2026-08-22) | Blocked on |
|---|---|---|---|---|
| A1 | Effect defs | effect | not started | atom schema |
| A2 | Statuses | status | not started | nothing — kinds already code-first |
| A3 | Battle traits | combat / battle | not started | atom schema; `RulesetVersion` + golden re-bless |
| A4 | Items | items (none yet) | not started | an item spec existing at all |
| A5 | VFX cue mirror | vfx | not started | atom ids being stable |
| A6 | Innate shields | shield | not started | item containers |
| B1 | Patron aura | demons | not started | atom schema |
| B2 | Star merge | fusion | not started | product call: container or curve |
| B3 | Expedition injury | standalone | not started | atom schema |
| B4 | Lane cost | world | **out of scope unless the world stream says otherwise** | — |
| C1 | Cheat / PvzStats plugins | stats | **declined** (decision D9) | — |
| D1 | Damage applier | undesigned | contract offered | that layer being designed |
| D2 | AI | undesigned | contract offered | that layer being designed |

---

## 6. What this audit does not cover

- **Skills** — zero exist; nothing to audit. They arrive as containers from day one.
- **Web / FE** — no effect authoring surface exists yet; the authoring path is an open question in the ideal (§C of the research plan).
- **Perf** — the hot-path cost of a data-driven layer is unmeasured. Presumed to require a baked runtime form; not verified here.
- **The power math** — deliberately undecided ([effect-atom-ideal.md §8.6](effect-atom-ideal.md)).
