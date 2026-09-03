# Item — capability map

**Status:** Proposed 2026-09-03, Phase 0 of `/spec`. **Nineteen modules. No build authorized until this
map is approved.** Program prefix `item`; module specs go in `docs/architecture/item/spec-<module-id>.md`,
tasks in `tasks/item-plan.md` / `tasks/item-todo.md`, per the parallel-programs convention in AGENTS.md.

> **The program graduates here.** [item-ideal.md](item-ideal.md) held the intent and, since 2026-09-03,
> **nineteen owner rulings (D1–D19)** and five verified defect claims. The seventeen lane SSOTs in
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
| The `rarity` table, ten rungs, per-class bands | `RpgStore.Containers.cs:54-61` | `rarity-bands` seeds data, it does not build a table |
| The demon **theme registry** — demons publish, items consume, one-way | `data/seed/demons/_registry/themes.v1.json`, seedsmith D4 | `set-charm-gen`'s upstream exists |
| Twelve aptitudes | `Stats/Aptitudes/Aptitude.cs:40-51` | D8's target vocabulary |

---

## 3. ⛔ Cross-program dependencies

Three, and the first is the one that will surface late if it is not named now.

| # | Dependency | Owner | Status |
|---|---|---|---|
| **X1** | **`frame` does not exist on any species type** — and by D19's own reasoning it is **not ours to add.** A frame describes a *body*, exactly as an aptitude vector describes a species; D19 sent per-species data to the demon program for precisely this reason. `slot-roles` and `base-types` both key on it | **the demon program** | ⛔ **Unbuilt, unscheduled.** Same shape as seedsmith's `aspect-scope` dependency, and recorded here as first-class for the same reason |
| X2 | **`E42 units-correction`** gates band → number resolution | content-stack, gate G3 | Scheduled. **Does not block authoring** — `seed-contract.md` §3's band rule closes the units trap by construction |
| X3 | **`ActionSeeder.Generate` has zero callers** | action corpus | Gates `granted-actions` (module 19) only |

**X1 is stated as a dependency and not a task on purpose.** Declaring `frame` in this program would put
one program's content in another's schema — the boundary error `spec-demon-themes.md` §2.1 refused when
it declined to make items a demons kind.

---

## 4. The modules

Nineteen. Model calls in exactly one.

### Foundation — model-free, no content, and it closes both live defects

| # | id | Capability | Depends on |
|---|---|---|---|
| 1 | `durable-ownership` | `rpg_item` (thin row, PK `instance_id`, carrying `player_id`) as a **second reachability root**, so unequip stops deleting gear. Per-atom bind compatibility replacing `catalog_revision` equality. The missing `effect_binding` FK. **Closes R1 (D5), R2 (D9), S2 and C3 (§2e)** | — |
| 2 | `armoury` | One **player-scoped** store — no per-specimen bags. Two storage grades (stock counters + rolled rows), category + list surface, unlimited capacity. **D5** | 1 |
| 3 | `slot-roles` | `item_role` (15, + `standard` declared and ungenerated per D14), `item_role_frame`, the **12-role hybrid core**, and the unlock predicate **defaulting to always-open**. **D2, D3, D14** | X1 |
| 4 | `equip-assign` | `rpg_item_assignment` — durable assign; binding rebuilt as a projection at deploy. Retires `rpg_unique_equipment` and the 3-item `UniqueEquipmentCatalog` stub | 1, 3 |
| 5 | ⭐ `equip-runtime` | Battle and lawn **read equipment**. Closes wiring gap W2 — `ChannelMods` is the reader, `TraitAtomSource` is the working producer to copy | 4 |

### Content model — model-free

| # | id | Capability | Depends on |
|---|---|---|---|
| 6 | ⛔ `base-types` | I3's base-type identities, each frame's pair differing by **directional stat profile *and* distinct implicit**, plus the **dominance lint**: for every role a build must exist where each frame's base is correct. **D11 — the correctness condition D3 depends on** | 3 |
| 7 | `rarity-bands` | Seed the ten rungs, per-class prefix/suffix bands and `rarity_budget`; **re-derive I12's drop weights (authored against 7) and I6's caps (against 5)** | — |
| 8 | `affix-legality` | I8's role × affix-group matrix, tier bands, frame filtering. `item_role_family` **derived**, not authored (§2b.1) — ~1,100 cells saved | 3, 7 |
| 9 | `power-model` | **E9.** *"How strong is this thing?"* — evaluation, distinct from `P(Θ)`'s derivation. **D13: authored as a general model with no item-specific concepts in its interface.** Items are its first consumer, not its subject | 7, 8 |
| 10 | `item-card` | Atoms → readable text, units, the card. G3. Unblocks three lanes that stall on presentation | 8, 9 |

### Generation

| # | id | Capability | Model? | Depends on |
|---|---|---|---|---|
| 11 | `drop-volume` | I12's drop tables. **Volume reads `Θ` linearly; quality keeps reading `P(Θ)` through rarity/tier. No private loot curve. D18** | — | 6, 7, 8 |
| 12 | `threshold-grants` | One mechanism: *count equipped things matching a predicate → grant a container at breakpoints, at `UniqueActor` scope*. **Serves sets, charms, and D3's frame-mix bonus** — three consumers, one machine | — | 4, 9 |
| 13 | ⭐ `set-charm-gen` | The seedsmith pipeline: 36 build set families + 1 set and 1 charm per species, consuming the demon theme registry. **Owns set/charm atom effect distribution** — §2c #2's ownerless capability. **Capped to the 12 hybrid-core roles before generation, not validated after** (§2c #1) | **yes** | 8, 12 |

### Sinks — the half that stops the museum

| # | id | Capability | Depends on |
|---|---|---|---|
| 14 | `salvage-craft` | I9 — materials, salvage, the cost vocabulary. The first sink, and the cheapest | 2, 7 |
| 15 | `enhance-reroll` | I6 + I7 under one mutation contract. **D7: cost, never luck** — steep tier-keyed cost, a success chance, and **mandatory bad-luck protection** (`rpg_summon_pity` is the precedent). The cost curve is a **configurable soft cap** in `data/tuning/`, never a hard stop | 9, 14 |
| 16 | `sockets` | I4 — inserts as their own instance binding on the same owner; no atom-table change | 4, 14 |

### Late and gated

| # | id | Capability | Depends on |
|---|---|---|---|
| 17 | `uniques` | G1 — hand-authored items that break generator rules and no machine rules | 8, 9 |
| 18 | `consumables` | G2 — the use path degenerates, never the effect. Names **`OnActivate`** | 4 |
| 19 | `granted-actions` | G4 — the `action_id` seam | 4, **X3** |

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
                 ├─► threshold-grants ◄── power-model ◄─┘
                 │        └─► set-charm-gen
                 ├─► sockets ◄── salvage-craft ─► enhance-reroll
                 ├─► consumables
                 └─► granted-actions ◄── X3
                                   drop-volume ◄── base-types + rarity-bands + affix-legality
                                   item-card   ◄── affix-legality + power-model
```

No cycles.

---

## 5. Build order, and where the payoff sits

```text
durable-ownership → armoury → slot-roles → equip-assign → ⭐ equip-runtime
  → base-types → rarity-bands → affix-legality → power-model → item-card
  → drop-volume → threshold-grants → set-charm-gen
  → salvage-craft → enhance-reroll → sockets
  → uniques → consumables → granted-actions
```

**Module 1 has standalone value before anything else lands.** It closes both live defects — unequip
destroying gear, and a content import disabling everything — on code that is *already running in
production*. That is worth shipping whether or not the rest of the program proceeds.

**⭐ The payoff is module 5, not module 19.** After `equip-runtime`, one hand-made item on one actor
changes a number in a real battle and on a real lawn. Everything before it is plumbing with no
observable effect; everything after it is content and depth. **Getting an end-to-end proof at module 5
of 19 is the same discipline `effect-pipeline` used** when it put its producer at module 4 of 10.

**Modules 6–10 are the content model and make no model calls.** By the time the first token is spent in
module 13, the base types, the rarity bands, the affix legality and the power model are all inspectable
against real data.

**The sinks (14–16) come after generation on purpose.** A salvage system with nothing to salvage is
untestable, and `enhance-reroll` needs `power-model` to know what a "better" roll is worth.

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

---

## 7. Open items, each now owned by a module

§2c's five, assigned — which is the point of a map.

| §2c | Item | Now owned by |
|---|---|---|
| 1 | Cap the set generator to the 12 hybrid-core roles | **13 `set-charm-gen`** — a generator input, stated before generation |
| 2 | Set/charm atom effect distribution has no lane | **13 `set-charm-gen`** — explicitly owns it |
| 3 | C3 and S2, two confirmed defects | **1 `durable-ownership`** |
| 4 | `E42` gates band → number | **X2** — cross-program, does not block authoring |
| 5 | Mechanical follow-through | **7 `rarity-bands`** (re-derive I12/I6) · **3 `slot-roles`** (I5 §3.7 edit, frame-mix breakpoints) · **X1** (I11's vectors) · **10 `item-card`** (light-theme palette) |

---

## 8. Related

- [item-ideal.md](item-ideal.md) — intent, **nineteen rulings (§2b)**, the platform reconciliation (§2a), the verified defects (§2e)
- [item/README.md](item/README.md) — the seventeen lane SSOTs, four decisions, the defect register
- [effect-pipeline-map.md](effect-pipeline-map.md) — the roll machinery this consumes, modules 1–4 built
- [seedsmith/spec-demon-themes.md](seedsmith/spec-demon-themes.md) — `set-charm-gen`'s upstream
- [power/ssot-power-scale.md](power/ssot-power-scale.md) — `Θ` and `P(Θ)`; D18 puts drop volume on it
