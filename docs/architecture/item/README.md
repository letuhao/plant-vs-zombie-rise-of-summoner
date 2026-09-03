# The item program — document index

**Status:** Design round complete 2026-08-22. **Reconciled against the shipped platform 2026-09-03 —
see [../item-ideal.md](../item-ideal.md) §2a before trusting any lane's constraints.** **No build is
authorized.** Twenty-four documents, no code, no schema, no task list yet.

> ✅ **Owner decisions taken 2026-09-03 — eight rulings, in [../item-ideal.md](../item-ideal.md) §2b.**
> Gear is uncapped and roster scale is not this program's problem (D1); fifteen roles stands; no slot
> unlocking in v1 but the predicate ships (D2); the commander may be hybrid, and **hybrid floors at 80% — 12 roles — earning parity back only by keeping both frames equipped** (D3); v1 authors the whole
> ilvl ladder to 32 (D4); the inventory feature is how durable ownership lands (D5); offence/defence
> drifts ~3:1 (D6); crafting reaches t5 gated by **cost, never luck** (D7); items may grant aptitude
> points, rarity-gated (D8). **D9–D12 followed the same day:** R2 closes by per-atom compatibility; one band table with a per-runtime
> scalar; base types differ by **directional profile *and* implicit** (the lint that makes D3 work); and
> **sets/charms are GENERATED at roster scale** — ~2,168 of them from ~3 authored rows, through
> seedsmith's `demon-themes` registry. **D13–D15:** the item program **builds E9** (the power model — three lanes were blocked on it); the
> **commander is just another unique demon** for now, so `standard`, artifacts and commander sets leave
> scope — which closes `sets` §10.1, `charms` §10.3 and `sockets` §10.3 at a stroke; and **rarity is the
> quality of a set's member pieces, not a property of the set** — 36 build set families, not 360.
>
> ⭐ **The 144 lane open questions, in perspective.** Roughly 25 are closed — **D1 alone closed seven
> lanes** (roster scale) and the namespaced reason code closed eight. **Most of the rest are
> lane-internal *"I picked X, confirm or overrule"* and are decided unless disputed.** §2c lists the
> **three** that genuinely remain. **Nothing blocks authoring.**
>
> **D16–D19 completed the round:** the ~110 lane picks are **ratified as a batch** (reversible, reopened
> only when one bites); the **dead tail is accepted** — a species set exists because the species does;
> **drop volume reads `Θ`**, so the item program adds no private loot curve; and **I11 splits** — the
> equip gate stays, per-species aptitude vectors go to the demon program. That last one exposed a real
> ownership gap: **which atoms a generated set or charm grants has no lane.**
>
> ⚠ **Two of the seven open decisions below are closed and one is superseded** — most importantly the
> `stat.derived` quarantine (#7), which lifted on 2026-08-30 and which five lanes terminated at. Lane
> documents written before that date state it as a live constraint; they are stale on that point and
> nowhere else. The table below is annotated. Program prefix `item`. **It graduated 2026-09-03 — the capability map is
[../item-map.md](../item-map.md), nineteen modules, awaiting approval.** Tasks will be
`tasks/item-plan.md` / `tasks/item-todo.md`.

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
| I5 | [ssot-sets.md](ssot-sets.md) | Combinations across equipped items. ⚠ **D12: sets are generated (~2,168), not hand-authored; §3.7 needs the D3 edit; the generator must be capped to the 12 hybrid-core roles** |
| I6 | [ssot-enhancement.md](ssot-enhancement.md) | +X, **and the instance-mutation model other lanes inherit** |
| I7 | [ssot-reroll.md](ssot-reroll.md) | Temper / Reforge / Imprint |
| I8 | [ssot-affixes.md](ssot-affixes.md) | The prefix/suffix system, tier bands, role×family legality |
| I9 | [ssot-materials-crafting.md](ssot-materials-crafting.md) | Materials, crafting, salvage, **the cost vocabulary** |
| I10 | [ssot-charms.md](ssot-charms.md) | Bonuses from unequipped inventory. ⚠ **D12: one charm per demon species, generated** |
| I11 | [ssot-requirements.md](ssot-requirements.md) | The equip gate — **frame + level only** after D19. ⚠ **Its per-species attribute vectors move to the demon program**; the stale `5 attributes × 24 species` sizing leaves with them |
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
| 1 | ✅/⛔ **Split by the 2026-09-03 ruling.** **R1 is CLOSED** — it ships as the inventory feature (`rpg_item.player_id` is the ownership root), see ideal §2b D5. **R2 remains OPEN** — strict `catalog_revision` equality is untouched. Original ask: **authorize the two blocking amendments** — a second reachability root so unbinding stops deleting instances, and revision-compare so a content import stops unequipping everything | **OPEN, and no longer cheap (2026-09-03).** `ProduceAndBind` is now called in production (`RpgStore.UniqueActors.cs:756`), so the code these protect is live. `RpgStore.AtomInstances.cs:607-620` still deletes an instance when its last binding goes; `:436` still refuses on strict `catalog_revision` equality. **This is now the thing that must land before a single item row exists** |
| 2 | ✅ **The content cut: ~3 100 → ~880 — ON.** Follows from the 2026-09-03 ruling that v1 authors the **whole ilvl ladder to 32** (ideal §2b D4); the cut was sized for exactly that choice | Was: *nothing should be authored until this is settled*. It is settled. **But d4 §7.7 first** — size G1–G4 before committing, or uniques add back by hand what the cut removed |
| 3 | ✅ **Reason codes: adopt the single namespaced `ContentRuleViolated`** (resolved by recommendation 2026-09-03, ideal §2b.1 — reversible) | 101 codes is a vocabulary to maintain and keep in sync with the FE forever; a namespaced code carries the same information in its payload |
| 4 | ✅ **Build the ladder to ilvl 32** (2026-09-03, ideal §2b D4). Max reachable was 11; rungs 80–100, the enhancement risk band and the top 40% of the tier ladder all become live | d4 §6.5's *"what must move to hit it"* list is now live work rather than a contingency |
| 5 | ~~**Primary attributes: five, or none.**~~ ⚠ **Answered by another program: twelve aptitudes**, shipped 2026-08-26 (`Stats/Aptitudes/Aptitude.cs:40-51`) | Not a decision any more. **An aptitude is a *source*, not a registered channel** — so I11's proposal needs rewriting against a real system rather than deciding |
| 6 | ⚠ **The `OnUse` trigger request** — largely answered: **`OnActivate` exists**, `TriggerCount = 8` (`AtomKindRegistry.cs:22,31`) | What remains is whether consumables name it, not whether a trigger exists |
| 7 | ✅ **`stat.derived` quarantine / E12 — CLOSED.** `Full/Full/None`: battle 2026-08-23 (`TraitAtomSource`), **lawn 2026-08-30** (`AtomDerivedSubsystem`, ActorHub order-350). `AtomKindRegistry.cs:253` | **Five lanes unblocked.** Wave 1's prefix pool is no longer 7–9 families — it is the whole catalogue. Every lane document that cites this constraint is stale on it |

---

## Honest state of this folder

- **Round 2 (2026-09-03) verified the *platform*, not the lanes.** §2a of the ideal re-checked every
  cross-program constraint against code. The lanes' own internal numbers — the ~880-cell content cut,
  the reason-code count, tier bands, costs, drop rates — were **not** re-verified and remain exactly as
  round 1 left them.
- **No test was run by any lane.** One decision agent ran two suites; the register ran all three. Every
  other document is read-from-source.
- ✅ **The three unverified defect claims are verified** (2026-09-03, ideal §2e). **C2 was real and has
  already been fixed** by the power program's audit F4 (`ResistanceEvaluator.cs:347`) — the item lanes
  were never told, and have carried it as a blocker on tier bands ever since. **C3 confirmed** (`AtomRow.Name`
  is never validated). **C1 reassigned** to `E42 units-correction`; it does not block authoring, because
  `seed-contract.md` §3's band rule closes the units trap by construction. Two structural claims also
  checked: *"`effect_binding` has zero production consumers"* is **refuted**; the missing
  `ON DELETE CASCADE` FK is **confirmed**.
- **Numbers will move.** Tier bands, costs, and drop rates are illustrative and were authored before the
  units correction in the handoff §1. E9's power model does not exist, so nothing here is balanced.
- **No map, no plan, no task list.** Those come when the program graduates, per AGENTS.md.
