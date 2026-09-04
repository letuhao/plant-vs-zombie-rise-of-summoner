# Item — capability map

**Status:** **Approved by the owner 2026-09-03** — Phase 0 of `/spec` complete. **Twenty-one modules**
(granularity confirmed: `durable-ownership`/`armoury` and `threshold-grants`/`set-charm-gen` stay split).
✅ **Phase 1 complete 2026-09-04 — all 22 modules written**, `docs/architecture/item/spec-*.md`, linked below.
**X1 resolved — seedsmith classifies `frame` (§3.1).** Build sequencing is **deliberately not here** — it
is the plan's, per §5.

> ⚠ **Revised 2026-09-03 after a seven-auditor review** ([item-ideal.md](item-ideal.md) §2f). **Twenty-one
> modules** — two were added for capabilities nothing owned. Built against **D1–D29**, not D1–D19.
> **Model calls in two modules, not one.** Program prefix `item`; module specs go in `docs/architecture/item/spec-<module-id>.md`,
tasks in `tasks/item-plan.md` / `tasks/item-todo.md`, per the parallel-programs convention in AGENTS.md.

> **The program graduates here.** [item-ideal.md](item-ideal.md) held the intent and, since 2026-09-03,
> **twenty-nine owner rulings (D1–D29)** and five verified defect claims. The seventeen lane SSOTs in
> [item/](item/) hold the mechanics. **What neither had is a build order** — which module is buildable
> today, what it unlocks, and where the payoff sits. That is this file.

**Read before proposing against this:** [item-ideal.md](item-ideal.md) §2b (the rulings — they win over
any lane), §2c (what is open), §2e (the verified defects); then [item/README.md](item/README.md) for the
lane index. Where a lane and a ruling disagree, **the ruling wins** — the lanes were written 2026-08-22
against an older platform.

---

## 1. What this program builds, in one paragraph

An **item** is a container of atoms an actor wears in a slot, rolled per player and frozen at drop. The
machinery to roll and bind one **already exists and runs in production** — this program builds the
*item*: the durable owned thing, the armoury it lives in, the body it attaches to, the base types and
affix legality that make one item differ from another, the generators that produce them at roster
scale, and the sinks that stop the whole thing becoming a museum.

---

## 2. What is already built — do not rebuild it

Verified against code 2026-09-03 ([item-ideal.md](item-ideal.md) §2a). **The substrate is further along
than the lane documents describe**, and three modules below exist only to *consume* it.

| Already shipped | Where | What it means here |
|---|---|---|
| Container / instance / binding schema, `prefix_rolls`+`suffix_rolls`, `effect_affix` with slot refs | `RpgStore.Containers.cs:20-84` | The item *template* and the *affix bundle* are schema, not proposals |
| The resolver and the producer — `Resolver`, `InstanceProducer`, `WorldSeed`, `VariantShift`, `ChannelPool`, `AffixLibraryGenerator`, `EligibilityRule` | `Core/Effects/Atoms/` | **Rolling an item is done.** No module below rolls anything |
| `ProduceAndBind` **called in production** | `RpgStore.UniqueActors.cs:756` | The atom runtime is not inert |
| `stat.derived` executing on battle **and** lawn | `AtomKindRegistry.cs:253` | Any affix the catalogue can express is bindable |
| `OwnerKind.UniqueActor` — the durable per-actor scope | `Effects/Atoms/OwnerScope.cs` | Equipment has an owner to name |
| The `rarity` **table and its per-class columns** — ⚠ **and it has ZERO rows** | `RpgStore.Containers.cs:54-61`; `data/seed/rarity/README.md` (*"Empty on purpose"*) | The ten rungs that *ship* are the `DemonRarity` enum, ordinals **0–9 consecutive** (§2f.1 F3). Module 7 **seeds the table**; it does not build one |
| The demon **theme registry** — demons publish, items consume, one-way | `data/seed/demons/_registry/themes.v1.json`, seedsmith D4 | `set-charm-gen`'s upstream exists |
| Twelve aptitudes | `Stats/Aptitudes/Aptitude.cs:40-51` | D8's target vocabulary |

---

## 3. ⛔ Cross-program dependencies

**Six.** X1 was resolved by owner decision 2026-09-03 and is no longer the open-ended wait it was
when this map was drafted.

| # | Dependency | Owner | Status |
|---|---|---|---|
| **X1** | **`frame` (humanoid / plant / hybrid) exists on no species type**, and `slot-roles` and `base-types` both key on it. By D19's reasoning it is not ours to declare — a frame describes a *body*, exactly as an aptitude vector describes a species | **seedsmith's demon pipeline** | ✅ **Resolved 2026-09-03: seedsmith classifies it**, as a new `frame-classify` stage beside `family-extract` and `motif-derive`, published through the theme registry items already consume. See §3.1 |
| X2 | **`E42 units-correction`** gates band → number resolution | content-stack, gate G3 | ✅ **DONE / gate CLOSED 2026-09-03** (`content-stack-todo.md:15,24`). ⚠ But `ssot-affixes.md` was **explicitly out of E42's scope**, so the item-side residue is unresolved. Was: **Does not block authoring** — `seed-contract.md` §3's band rule closes the units trap by construction |
| X3 | **`ActionSeeder.Generate` has zero callers** | action corpus | Gates `granted-actions` (19) only. ✅ **D36: ordinary external dependency, no action required of us.** `action-corpus` is under active construction by another owner — we consume a production caller when one ships. ⛔ **Do not file requests against their map, propose amendments to their scope, or infer their schedule from their documents** |
| **X5** | ⭐ **The content ladder must keep growing.** Item level *is* content level (`ssot-generation.md` §4.1); content stops at level 10 today. **D29: the ladder is unbounded** — tier saturates at t5 and `contentScale` carries growth past it | **world map · wave catalog · event generator** | Added 2026-09-03. This is the loop D26 says is not ours: gear → harder realm → gear. We supply the middle arrow only |
| **X7** | ⛔ **D27's four `container_kind` values** — `gem` · `set` · `charm` · `combo`, plus the fifth (`consumable`) D27 did not mint. `ContainerRow.cs:7-14` ships six values and none of them | **effect-atom** (`definitions.md` §1 + `ContainerRow.cs`) | Added 2026-09-04. **Gates modules 12, 13, 16, 18 and 21** — `spec-sockets.md:30`: *"this module cannot author a single insert until `gem` and `combo` land"* |
| **X6** | **`E44 power-sweep`** — the 20 power coefficients are flat at `CoeffMilli = 1000` | effect-atom / content-stack | Gates module 9's reads from meaning anything. **The owner already ruled on it 2026-09-03** |
| **X4** | ⭐ **L0 — pool composition by power class × channel.** Without it, a trash drop and a boss drop roll the same affixes, and sets/sockets/uniques/crafting are redundant paths (`effect-pipeline-ideal.md` §5.6) | **effect-pipeline**, modules 11–12 | Added by owner decision 2026-09-03. ⚠ **SPECCED** (`effect-pipeline/spec-affix-channel-weights.md`), unbuilt. It names **six** channel suppliers, so it gates item modules **11, 13, 15, 16 and 17** — not two. ⛔ **And modules 11 and 13 both consume *and* supply it, which is a two-way edge** — both of which *supply channels to it* rather than merely consuming it |

**X1 is stated as a dependency and not a task on purpose.** Declaring `frame` in this program would put
one program's content in another's schema — the boundary error `spec-demon-themes.md` §2.1 refused when
it declined to make items a demons kind.

### 3.1 X1's resolution — `frame-classify`, a new seedsmith stage

> **Owner, 2026-09-03:** seedsmith classifies frame, *"like families and motifs."*

**Why this is better than the three alternatives, and not merely cheaper.** The demon pipeline already
reads each species' name and flavour text and returns a judgement about what it *is* — that is exactly
what `family-extract` and `motif-derive` do. **Frame is the same kind of judgement**, and it needs the
same honesty machinery: a `basis` field recording whether the answer came from the name or the text,
with `blocked` a legal answer.

| Property | Follows the existing stages |
|---|---|
| Input | the species' own name and flavour text — the corpus `family-extract` already reads |
| Output | one of **three enum values**, never a number (`audit_schema` rejects numerics mechanically) |
| Honesty | carries `basis`, exactly as family labels do |
| Publication | the demon pipeline's one-way registry items already consume. ⚠ **Frame must publish independently of theme status** — `spec-demon-themes.md` makes publishing a theme for a `basis=blocked` demon a **Never**, and a species can lack a *flavour* judgement while still having a *body*. Frame is a body fact; it is not gated on theme confidence |
| Scale | ~904 species with no hand-authoring — the property that made the classifier worth building |

⭐ **It also solves the conflation item-ideal §4 warns about, rather than inheriting it.**
`DemonSpeciesDef.Side` carries faction *and* body in one field, and the roster already contains Fusion
hybrids that break it — `peashooterzombie`, `ironpeazombie`, `cherrynutzombie`, `bucketnutzombie` are
zombie-**side** with plant **bodies**. A classifier reading the flavour text can see that; a field
derived from `Side` cannot. **`hybrid` stops being an edge case and becomes a classification outcome.**

**Rejected, with reasons:** a hand-set field on `DemonSpeciesDef` (correct owner, but ~904 rows by hand
and an unscheduled queue); an item-side `item_species_frame` mapping table (unblocks now, but a second
place species truth lives, which can silently disagree); and this program adding the field itself
(fastest, but it overrules the same D19 boundary reasoning that just moved I11's per-species vectors
*out* of this program).

⚠ **This is a request against another program's capability map.** Recorded in
[seedsmith-map.md](seedsmith-map.md) as a proposed module rather than assumed — a cross-program ask that
lives only in the consumer's document is how a dependency surfaces late.

---

## 4. The modules

Twenty-one. Model calls in **two** (13 and 21).

### Foundation — model-free, no content, and it closes both live defects

| # | id | Capability | Depends on |
|---|---|---|---|
| 1 | [`durable-ownership`](item/spec-durable-ownership.md) | `rpg_item` (thin row, PK `instance_id`, carrying `player_id`) as a **second reachability root**, so unequip stops deleting gear. Per-atom bind compatibility replacing `catalog_revision` equality. The missing `effect_binding` FK. **Closes R1 (D5), R2 (D9), S2 and C3 (§2e)** | — |
| 2 | [`armoury`](item/spec-armoury.md) | One **player-scoped** store — no per-specimen bags. Two storage grades (stock counters + rolled rows), category + list surface, unlimited capacity. **D5** | 1 |
| 3 | [`slot-roles`](item/spec-slot-roles.md) | `item_role` (15, + `standard` declared and ungenerated per D14), `item_role_frame`, the **12-role hybrid core**, and the unlock predicate **defaulting to always-open**. **D2, D3, D14** | X1 |
| 4 | [`equip-assign`](item/spec-equip-assign.md) | `rpg_item_assignment` — durable assign; binding rebuilt as a projection at deploy. Retires `rpg_unique_equipment` and the 3-item `UniqueEquipmentCatalog` stub | 1, **2**, 3 |
| 5 | ⭐ [`equip-runtime`](item/spec-equip-runtime.md) | Battle and lawn **read equipment**. Closes wiring gap W2 — `ChannelMods` is the reader, `TraitAtomSource` is the working producer to copy | 4 |

### Content model — model-free

| # | id | Capability | Depends on |
|---|---|---|---|
| 6 | ⛔ [`base-types`](item/spec-base-types.md) | I3's base-type identities, each frame's pair differing by **directional stat profile *and* distinct implicit**, plus the **dominance lint**: for every role a build must exist where each frame's base is correct. **D11 — the correctness condition D3 depends on** | 3 |
| 7 | [`rarity-bands`](item/spec-rarity-bands.md) | Seed the ten rungs, per-class prefix/suffix bands and `rarity_budget`; **re-derive I12's drop weights (authored against 7) and I6's caps (against 5)** | — |
| 8 | [`affix-legality`](item/spec-affix-legality.md) | I8's role × affix-group matrix, tier bands, frame filtering. `item_role_family` **derived**, not authored (§2b.1) — ~1,100 cells saved | 3, 7 |
| 9 | [`item-power-reads`](item/spec-item-power-reads.md) | ⚠ **Rescoped — D13 was VOID (§2f.2). E9 `power-vector` shipped 2026-08-22** with 33 tests. ⚠ **One** production consumer — `RpgStore.Power.cs:212`; `AutoEquip.cs:16-19` explicitly *declines* to wire E9 (*"new, unauthorized scope"*) and `RungMonotonicity` has only test callers. This module **consumes** it and owns the three item reads it was created for: I3's ≤15% implicit budget cap, G4's granted-action budget, G3's power display — plus D8's aptitude pricing. Upstream: **E44 `power-sweep`** (all 20 coefficients flat at 1000), owned by effect-atom | 7, 8, **X6** |
| 10 | [`item-card`](item/spec-item-card.md) | Atoms → readable text, units, the card. G3. Unblocks three lanes that stall on presentation | 8, 9 |

### Generation

| # | id | Capability | Model? | Depends on |
|---|---|---|---|---|
| 11 | [`drop-volume`](item/spec-drop-volume.md) | I12's drop tables. **Volume reads `Θ` linearly; quality keeps reading `P(Θ)` through rarity/tier. No private loot curve. D18.** ⭐ Supplies the `drop` / `boss` **channel** to effect-pipeline's L0 (**X4**) | — | 6, 7, 8, **X4** |
| 12 | [`threshold-grants`](item/spec-threshold-grants.md) | One mechanism: *count equipped things matching a predicate → grant a container at breakpoints, at `UniqueActor` scope*. **Serves sets, charms, and D3's frame-mix bonus** — three consumers, one machine | — | 4, 9 |
| 13 | ⭐ [`set-charm-gen`](item/spec-set-charm-gen.md) | The seedsmith pipeline: 36 build set families + 1 set and 1 charm per species, consuming the demon theme registry. **Owns set/charm atom effect distribution** — §2c #2's ownerless capability, now **answered in part by L0**: the `set` and `socket` channels are what make a set bonus worth collecting. **Capped to the 12 hybrid-core roles before generation, not validated after** (§2c #1) | **yes** | 8, 12, **X4** |

### Sinks — the half that stops the museum

| # | id | Capability | Depends on |
|---|---|---|---|
| 14 | [`salvage-craft`](item/spec-salvage-craft.md) | I9 — materials, salvage, the cost vocabulary. The first sink, and the cheapest | 2, 7 |
| 15 | [`enhance-reroll`](item/spec-enhance-reroll.md) | I6 + I7 under one mutation contract. **D7: cost, never luck** — steep tier-keyed cost, a success chance, and **mandatory bad-luck protection** (`rpg_summon_pity` is the precedent). The cost curve is a **configurable soft cap** in `data/tuning/`, never a hard stop | 9, 14 |
| 16 | [`sockets`](item/spec-sockets.md) | I4 — inserts as instance bindings on the same owner; **the combination evaluator** (25 resonances + Strains/Splices); D22's affinity **bonus**; D21's set-piece exclusivity validator. ⚠ *"No atom-table change"* was wrong — the lane requests `bind_ordinal` on `effect_binding` (§5.4) | 4, **15**, 14 |

### Late and gated

| # | id | Capability | Depends on |
|---|---|---|---|
| 17 | [`uniques`](item/spec-uniques.md) | G1 — hand-authored items that break generator rules and no machine rules | 8, 9 |
| 18 | [`consumables`](item/spec-consumables.md) | G2 — the use path degenerates, never the effect. Names **`OnActivate`** | 4 |
| 19 | [`granted-actions`](item/spec-granted-actions.md) | G4 — the `action_id` seam | 4, **X3** |
| **20** | ⭐ [`item-surfaces`](item/spec-item-surfaces.md) | **Added 2026-09-03 — nothing owned any player-facing surface.** Armoury list + filter, the equip screen, item-card render, comparison, the socket preview and the **combination compendium** (D20 promotes these from nicety to requirement at 127 combos). `docs/web/spec.md` records the same seam as unclaimed from its side — both maps pointed at each other | 2, 10, 16 |
| **22** | [`charm-carry`](item/spec-threshold-grants.md) | ⭐ **Split out of 12 by D40, 2026-09-04.** The charm pouch: five tables, the carry gate, five reason codes and the run-lifecycle hook. Sized larger than the threshold evaluator it would have ridden inside. ⚠ **Specced inside `spec-threshold-grants.md` today** — it needs its own file when it is scheduled | 12 |
| **21** | ⭐ [`strain-splice-gen`](item/spec-strain-splice-gen.md) | **Added 2026-09-03.** The 102 generated combinations — 36 Strains (12 aptitudes × 3 archetypes) + 66 Splices (C(12,2)), seedsmith-configured. **The program's second model call.** Also owns retiring the existing element-keyed `socket-word` corpus | **yes** → 8, 16 |

> ⚠ **Declared dependencies were reconciled against each spec's own body, 2026-09-04.** Five rows
> understated what the module actually reads:
>
> | Module | Also depends on | Because |
> |---|---|---|
> | 8 `affix-legality` | **D28** | every tag-gated rule is inert until E43 stamps family tags into `AtomRow.TagsJson` |
> | 12 `threshold-grants` | **3** | it reads `budgetWeightMilli` and the twelve-role list, and `Core/Items/` does not exist until module 3 creates it |
> | 13 `set-charm-gen` | **3** | the twelve-role generator cap is module 3's to issue |
> | 16 `sockets` | **`bind_ordinal` on `effect_binding`** | requested by the lane (§5.4) and **absent** from the shipped DDL |
> | 21 `strain-splice-gen` | **6** | inert until `socketMax` can reach 4; no shipped base type hosts a 4-ingredient recipe |
>
> **X7** (D27's container kinds) additionally gates **12, 13, 16, 18 and 21**, and **X4** gates **11, 13,
> 15, 16 and 17** — not the two each was first recorded against.

### Dependency graph

```text
X1 (demon program: frame)
 └─► slot-roles ─┬─► base-types ──────────┐
                 ├─► affix-legality ◄── rarity-bands
                 │        │                │
durable-ownership ─► armoury               │
        │                │                 │
        └─► equip-assign ─► equip-runtime  │   ⭐ payoff
                 │                          │
                 ├─► threshold-grants ◄── item-power-reads ◄─┘
                 │        └─► set-charm-gen
                 ├─► sockets ◄── salvage-craft ─► enhance-reroll
                 ├─► consumables
                 └─► granted-actions ◄── X3
                                   drop-volume ◄── base-types + rarity-bands + affix-legality
                                   item-card   ◄── affix-legality + item-power-reads
```

⚠ **One two-way edge, named rather than denied (corrected 2026-09-04):** modules 11 and 13 consume **X4** *and* supply its channels. It is not a build cycle — the channel is **authored inert** and X4 weights it later — but the graph must show it, because an unnamed cycle is how a build order quietly becomes unsatisfiable.

---

## 5. Dependency order — **not** the build sequence

> **Owner, 2026-09-03:** *"build order in plan phase. resolve dependencies first."*
>
> **So this section states dependency order only.** Which modules ship together, in what waves, and
> whether module 1 goes out on its own are **plan-phase decisions** — `tasks/item-plan.md`, not this
> file. A map says what depends on what; a plan says what happens when. Conflating them is how a map
> stops being re-orderable the moment priorities move.
>
> **And dependencies come first:** X1 is resolved (§3.1) but **unbuilt**, and modules 3 and 6 key on it.
> The plan opens with dependency resolution, not with module 1.

A topological order consistent with §4's graph:

```text
durable-ownership → armoury → slot-roles → equip-assign → ⭐ equip-runtime
  → base-types → rarity-bands → affix-legality → item-power-reads → item-card
  → drop-volume → threshold-grants → set-charm-gen
  → salvage-craft → enhance-reroll → sockets → strain-splice-gen
  → uniques → consumables → granted-actions → item-surfaces
```

**Module 1 has standalone value**, which is a property of the module and therefore belongs here. It
closes both live defects — unequip destroying gear, and a content import disabling everything — on code
that is *already running in production*. **Whether it ships alone is the plan's call**; that it *could*
is a fact about the dependency graph.

**⭐ Module 5 is also where item balance becomes testable.** D29 validates item channel bands against the
class-system's existing termination (HARD) and dominance (SOFT) guards, extended to geared corners — and
that can only run once gear reaches battle. **Module 5 is the gate for the first geared corner run.**

**⭐ The payoff is module 5, not module 21.** After `equip-runtime`, one hand-made item on one actor
changes a number in a real battle and on a real lawn. Everything before it is plumbing with no
observable effect; everything after it is content and depth. **Getting an end-to-end proof at module 5
of 19 is the same discipline `effect-pipeline` used** when it put its producer at module 4 of 10.

**Modules 6–10 are the content model and make no model calls.** By the time the first token is spent in
module 13, the base types, the rarity bands, the affix legality and the power model are all inspectable
against real data.

**The sinks (14–16) come after generation on purpose.** A salvage system with nothing to salvage is
untestable, and `enhance-reroll` needs `item-power-reads` to know what a "better" roll is worth.

---

## 6. What this program deliberately does not do

| Excluded | Why |
|---|---|
| Rolling an item | `Resolver` + `InstanceProducer` already do it. This program *calls* them |
| The affix schema, the resolution order, the affix library | `effect-pipeline` modules 1–3, built |
| Declaring `frame` on a species | X1 — demon-program data, by D19's own reasoning |
| Per-species aptitude vectors | D19 — moved to the demon program with the rest of I11's species data |
| Commander-specific gear — `standard` atoms, **artifacts**, commander sets | **D14.** Reserved, blocked on a commander role/class system that does not exist |
| Trading, durability, transmog, item-level squishes | item-ideal §10 |
| Inventory-management minigame | D5 — *"we will add inventory management mini game in future"* |
| A private loot curve | D18 — drop volume reads `Θ` |
| **Metering the player — drop caps, inventory ceilings, cost curves that rise with player power** | ⭐ **D26.** The item system balances items against each other; it does not balance the game. Content pacing, encounter volume and difficulty belong to the world map, battle engine and event generator |
| **How deep the content ladder goes** | **X5.** Item level *is* content level; we consume it, we do not set it |
| **PoE-style socket *links*** — a support gem modifying only the skill it is linked to | ⭐ **D25**, recorded here so the reservation lives somewhere durable (it previously existed only as two one-line mentions inside specs). It is **skill modification**, not combination-unlocks-a-bonus, and belongs with `granted-actions` and the action layer |
| **Per-species aptitude vectors and their growth curves** | **D19** — they describe a species, so they moved to the demon program with the rest of its species data |

---

## 7. Open items, each now owned by a module

§2c's five, assigned — which is the point of a map.

| §2c | Item | Now owned by |
|---|---|---|
| 1 | Cap the set generator to the 12 hybrid-core roles | **13 `set-charm-gen`** — a generator input, stated before generation |
| 2 | Set/charm atom effect distribution has no lane | **13 `set-charm-gen`** — explicitly owns it |
| 3 | C3 and S2, two confirmed defects | **1 `durable-ownership`** |
| 4 | `E42` gates band → number | **X2** — cross-program, does not block authoring |
| — | **X1: `frame` on ~904 species** | ✅ **Resolved** — seedsmith's new `frame-classify` stage (§3.1). **Resolved but unbuilt**, and modules 3 and 6 key on it, so the plan opens here |
| 5 | Mechanical follow-through | **7 `rarity-bands`** (re-derive I12/I6) · **3 `slot-roles`** (I5 §3.7 edit, frame-mix breakpoints) · **X1** (I11's vectors) · **10 `item-card`** (light-theme palette) |

---

## 8. Related

- [item-ideal.md](item-ideal.md) — intent, **twenty-nine rulings (§2b, §2f)**, the platform reconciliation (§2a), the verified defects (§2e)
- [item/README.md](item/README.md) — the seventeen lane SSOTs, four decisions, the defect register
- [effect-pipeline-map.md](effect-pipeline-map.md) — the roll machinery this consumes, modules 1–4 built
- [seedsmith/spec-demon-themes.md](seedsmith/spec-demon-themes.md) — `set-charm-gen`'s upstream
- [power/ssot-power-scale.md](power/ssot-power-scale.md) — `Θ` and `P(Θ)`; D18 puts drop volume on it
