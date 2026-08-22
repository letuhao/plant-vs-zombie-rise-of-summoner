# The item program — document index

**Status:** Design round complete 2026-08-22. **No build is authorized.** Twenty-four documents, no
code, no schema, no task list yet. Program prefix `item`; when this graduates the capability map is
`docs/architecture/item-map.md` and tasks are `tasks/item-plan.md` / `tasks/item-todo.md`.

Intent lives in [../item-ideal.md](../item-ideal.md). The authoring rules every document here obeys are
in [enrichment-contract.md](enrichment-contract.md). **Where a lane document and the contract disagree,
the contract wins.**

---

## Read in this order

| # | Document | What it is |
|---|---|---|
| 1 | [../item-ideal.md](../item-ideal.md) | The intent. Start here |
| 2 | [enrichment-contract.md](enrichment-contract.md) | Binding authoring contract — terminology lock, nine shared rules, reserved `container_kind` values, ten boundary cuts, owner decisions OD1–OD7 |
| 3 | [reconciliation-plan.md](reconciliation-plan.md) | How the second round was run |
| 4 | [atom-layer-handoff.md](atom-layer-handoff.md) | **What another program has to act on** — corrections, defects, named requests |

## The thirteen lanes

Each is an SSOT for one mechanic, in a fixed ten-section shape ending in *"what this lane needs from
other lanes"*.

| Lane | Document | Owns |
|---|---|---|
| I1 | [ssot-rarity.md](ssot-rarity.md) | The ladder, the overlap mechanism, and the registry of what rarity governs |
| I2 | [ssot-equip-slots.md](ssot-equip-slots.md) | 15 roles per pure frame, two frame vocabularies, hybrid pricing, budget weighting |
| I3 | [ssot-item-categories.md](ssot-item-categories.md) | Categories, base types, implicits, base stats |
| I4 | [ssot-sockets.md](ssot-sockets.md) | Sockets, inserts, and combinations within one item |
| I5 | [ssot-sets.md](ssot-sets.md) | Combinations across equipped items |
| I6 | [ssot-enhancement.md](ssot-enhancement.md) | +X, **and the instance-mutation model other lanes inherit** |
| I7 | [ssot-reroll.md](ssot-reroll.md) | Temper / Reforge / Imprint |
| I8 | [ssot-affixes.md](ssot-affixes.md) | The prefix/suffix system, tier bands, role×family legality |
| I9 | [ssot-materials-crafting.md](ssot-materials-crafting.md) | Materials, crafting, salvage, **the cost vocabulary** |
| I10 | [ssot-charms.md](ssot-charms.md) | Bonuses from unequipped inventory |
| I11 | [ssot-requirements.md](ssot-requirements.md) | The equip gate, and a primary-attribute proposal |
| I12 | [ssot-generation.md](ssot-generation.md) | The drop → instance pipeline and drop tables |
| I13 | [ssot-inventory.md](ssot-inventory.md) | Storage, stacking, salvage, lifecycle, comparison |

## The four gap lanes

Mechanics the first round did not own.

| Lane | Document | Owns |
|---|---|---|
| G1 | [ssot-uniques.md](ssot-uniques.md) | Hand-authored items that break generator rules |
| G2 | [ssot-consumables.md](ssot-consumables.md) | Items spent for an effect |
| G3 | [ssot-presentation.md](ssot-presentation.md) | Atoms → readable text; units; the item card |
| G4 | [ssot-granted-actions.md](ssot-granted-actions.md) | The `action_id` seam |

## The four decisions and the register

| Document | Ruling |
|---|---|
| [decision-d1-durable-ownership.md](decision-d1-durable-ownership.md) | **Assign/bind split.** `actor:{instanceId}` reserved, not added — traced end to end and it does not reach the actor |
| [decision-d2-mutation-contract.md](decision-d2-mutation-contract.md) | **Reconstruction from record + auditable + idempotent.** Byte-replay from catalog is a want, not a need |
| [decision-d3-cost-rarity-rebase.md](decision-d3-cost-rarity-rebase.md) | **Four material bands, four shipped ids.** Container wins `pool_rolls`; the E5 boundary beats `definitions.md` §2 on the magnitude path |
| [decision-d4-content-budget.md](decision-d4-content-budget.md) | **~3 100 hand-authored cells → ~880** in twelve cuts. Three item-level cadence bands |
| [defect-register.md](defect-register.md) | Suites run: **2 664 green**. 9 confirmed, 2 refuted, 1 partial |

---

## What is settled

- **Frame, not faction, is the key.** `humanoid` / `plant` / `hybrid`, because `DemonSpeciesDef.Side`
  conflates body with allegiance and the roster already contains Fusion hybrids.
- **15 equip roles per pure frame**, named twice — one role table, two vocabularies — so the affix
  library is authored once. Hybrid gets 13 and may mix base types.
- **Rarity is ten rungs with measured overlap**, derived from 5 count bands × 5 tier windows walked as a
  monotone chain. Adjacent-rung upset is 7.9–28.3%, proposed as a CI-tested invariant.
- **Prefix versus suffix is derived from `kind_id`, not authored** — permanent modifiers versus
  triggered ones, which makes the cap a frame-time budget.
- **Base stats are atoms**, because the primary channel list is closed at 8 with no `weaponDamage`, so a
  column would be the atom plus a hand-written bypass of the single-writer rule.
- **Two storage grades.** Stock items need no rows at all — a counter plus one shared canonical instance
  — so a roster of 48 × 15 slots is 720 cells but never 720 decisions.
- **Equipping is assign-then-bind**, not one binding. Assignment is durable and ours; binding stays
  session-scoped and is rebuilt as a projection at deploy.
- **Sets grant their one capability at the *lowest* threshold**, inverting genre convention, so a
  two-piece splash is always worth taking and two half-sets compete with one full set.
- **A unique may break every rule that lives in the generator, and no rule that lives in the machine** —
  a line the shipped validator already draws, since the fixed core is never tier-checked.
- **Inserts and charms never count toward set completion.** Three lanes agreed independently.

## Open — needs the owner

Ordered by what blocks the most.

| # | Decision | Why it cannot wait |
|---|---|---|
| 1 | **Authorize the two blocking amendments** — a second reachability root so unbinding stops deleting instances, and revision-compare so a content import stops unequipping everything | Both are cheap *now* and unavoidable later. Both touch E6, which is ask-first |
| 2 | **The content cut: ~3 100 hand-authored cells → ~880** | Nothing should be authored until this is settled. Several cuts cost nothing at all |
| 3 | **The reason-code surface: 33 → 101** | Accept, or adopt the single namespaced `ContentRuleViolated` code |
| 4 | **Max reachable item level is 11 today.** 40% of the tier ladder, the enhancement risk band, and rarity rungs 80–100 cannot drop | A content ladder reaching level 32 has to exist, or the design is authored against nothing |
| 5 | **Primary attributes: five, or none.** The proposal only earns its place if growth curves are per-species divergent; otherwise it is a level gate with extra steps | Gates I11, and touches the stat layer |
| 6 | **The `OnUse` trigger request** — the runtime already fires it, the schema forbids naming it | Gates consumables |
| 7 | **`stat.derived` quarantine / E12** | Five lanes terminate here. Wave 1's prefix pool is 7–9 families until it lifts |

---

## Honest state of this folder

- **No test was run by any lane.** One decision agent ran two suites; the register ran all three. Every
  other document is read-from-source.
- **Three defect claims are unverified** (C1–C3 in the handoff), one of which would change what a status
  magnitude means.
- **Numbers will move.** Tier bands, costs, and drop rates are illustrative and were authored before the
  units correction in the handoff §1. E9's power model does not exist, so nothing here is balanced.
- **No map, no plan, no task list.** Those come when the program graduates, per AGENTS.md.
