# Ideal: `effect-pipeline` — the producer that turns a seed into a concrete effect list

**Status:** idea phase, 2026-09-01. Not a spec, not a plan. The owner's original idea from the start of
the seedsmith design, written down for the first time.

> **The finding that reframes this whole program:** the effect-atom program already named this exact
> piece as the thing it was missing, in its own words. `effect-atom-map.md:213`:
>
> > **A producer of instances/bindings** — anything that actually calls `Instantiator`, `SaveInstance`,
> > or `Bind` with a real owner … Until then the runtime this program built (E6/E7/E15/E19) is
> > **inert**: `ResolveBindings` returns empty for every owner, so `AtomPushService` compiles nothing
> > and `AtomRunner` never receives an entry — **proven correct end to end by tests, unreachable end to
> > end in production.**
>
> **This is that producer.** It is not a new system. It is the one connection that turns four built,
> tested, unreachable modules into a working game.

---

## 0. The principles this is built on, restated inline

Restated rather than linked, because a downstream session reads this doc, not its links.

### The three-layer law — seed → concrete → per-player

Owner, 2026-09-01, binding for **every** generation feature:

> *"Seedsmith generate seed, game generator in game runtime generate concrete object, per player game
> store that object … every generator use this sdk baseline, so no need to duplicated code for all."*

```text
SEED       seedsmith, offline, enums only, no magnitudes       committed, diffable
   |
   v       the GAME RUNTIME resolves it, seeded
CONCRETE   a frozen atom list with rolled values
   |
   v
STORED     that player's own tables. "each player play they own game"
```

### The LLM writes identity. Deterministic code writes magnitude.

Seedsmith P1: *"a model has no calibrated sense of scale, so a number it picks is a plausible-looking
guess that survives review because nothing looks wrong with it."* A wrong enum is visible; a wrong
number is not.

### Items have no behaviour. Actors do.

`effect-atom/definitions.md` §0. An item, trait, skill or species passive is a **source** that put an
atom on an actor's list. **None of them participates at runtime.** This is the sentence that decides
§3.2's shape.

### One power ladder · no hard caps · `long` for magnitudes · tunables not literals

Contests read `Θ`, magnitudes read `P(Θ)`. A cap on a magnitude is a progression ceiling until proven
otherwise. Never `float` for a magnitude. A number a balance pass would change lives in
`data/tuning/`.

---

## 1. What the owner asked for

> *"we have seedsmith generate seed → in game runtime read and **make it version** → use it and random
> generator to generate list of atom effect → atom effect make concrete effect list for each effect
> container. an effect container is anything that contain effect like item, action, passive skill,
> trait, aura, unique demon, specie demon."*

And, on how the layers stack:

> *"roll only make when player create, it will not change. gacha, summon demon is different, it own
> specie base effects but it have it own effect too. unique demon aka actor is more special because it
> own specie + summon/gacha + trait + passive skill + equiped items. **you can associate that specie
> demon like race in some rpg game.**"*

And on who owns the numbers:

> *"specie should have 0-to-N effect but it should not much effect — like lowest rarity maybe have 0-1
> effect, highest rarity is 10 have 7-10 effect. the item container have more than that because it have
> affix, prefix, set bonus, rarity bonus … action container have lesser too, maybe 1 to 5. **so feature
> define tunable variable and balance by them self.**"*

**The race analogy is the load-bearing sentence.** A species passive is not a demon's identity — it is
the floor every member of that species stands on, and everything individual is layered above it.

---

## 2. Findings — built · wiring gap · real gap

### BUILT — every stage of the owner's chain already exists

| Owner's stage | What it is | Evidence |
|---|---|---|
| **"in game runtime read and make it version"** | `AtomImporter` → `RpgStore.ImportContent`: validate-all-then-write in one transaction, **`catalog_revision` bumped once per transaction and only when something changed** | E14a, `effect-atom-map.md:75`, **BUILT 2026-08-22** |
| **"random generator"** | `Instantiator.Draw` — weighted pool selection, **grouped** so one container never rolls `+10 atk / +12 atk / +14 atk` | `Instantiator.cs`, `spec-container-schema.md` |
| **"list of atom effect"** | `effect_container_atom` (fixed core) + `effect_container_pool` (weighted, grouped). *"Fixed core plus optional weighted pool."* | E5 |
| **"concrete effect list"** | `effect_instance` / `effect_instance_atom` — frozen rolls, `roll_seed`, power at roll time — plus `effect_binding` | E6 |
| the reproducibility law | *"Same `(container_id, catalog_revision, roll_seed)` ⇒ identical `effect_instance_atom` rows: same atom set, same `values_json`, same `power_json`."* | `definitions.md:170` |
| rarity → how many, and which tiers | `pool_rolls` + `min_tier`/`max_tier` | `definitions.md:141-147` |
| the shared-SDK precedent | `ActionSeeder` reuses `Instantiator.Draw` **verbatim** — *"unchanged, only its visibility widened"* | `ActionSeeder.cs:19,45` |
| the runtime that consumes it | `AtomRunner` (E15), `atom-compiler` (E7), power vector + reads (E9/E10) | all **BUILT** |
| the consumer side of binding | `AtomPushService.cs:54` calls `ResolveBindings` | wired |

**The owner's design is not a proposal against this architecture. It is this architecture's own missing
half, described independently.**

### WIRING GAP — nothing produces an instance

| Gap | Evidence |
|---|---|
| `Instantiator.TryInstantiate` has **zero production callers** | grep over `src/`: its own definition at `Instantiator.cs:92`, and one comment in `RpgStore.AtomInstances.cs:104`. Everything else is tests |
| `RpgStore.SaveInstance` (`:113`) has **zero production callers** | same grep |
| `ActionSeeder.Generate` has **zero production callers** | referenced only from `ActionSeedingTests.cs` |
| therefore `ResolveBindings` (`:286`) returns empty for every owner | so `AtomPushService` compiles nothing, `AtomRunner` never receives an entry |

**Four built modules, one missing call.** This is the difference between "the effect system does not
work" and "the effect system has never been switched on" — and it is the whole reason this program is
small relative to what it delivers.

### ⚠️ Correction: the *atom* runtime is inert, but effects DO reach actors today

An earlier framing in this document said the effect layer is unreachable in production. **That is true
of the atom layer and false of the game.** There is a shipped, live effect-granting path that does not
go through `Instantiator` at all:

| Path | Evidence |
|---|---|
| `rpg_unique_stat_mods.mods_json` | the table exists (`RpgStore.cs:405`), `UniqueEquipmentCatalog.BuildModsJson` (`:75`) builds it **from equipped slots**, `UniqueLoadoutSpec` parses it |

**E6 already planned to absorb it** — `effect_binding` *"replaces the logical `foundation_effect_grant`
and **absorbs today's `mods_json` grant blobs**."* Nothing has.

So `instance-producer` is **not** lighting up an empty system. It is standing up a second effect path
beside a working one, and the two must not both feed an actor. That makes `mods_json` a **migration
concern, not merely a wiring one** — and it is exactly the "equipped items" line in the owner's own list
of what a unique demon carries. Recorded here because the earlier framing would have let a build session
discover it at the worst moment.

### REAL GAP — what genuinely does not exist

| Gap | Why it is real, not wiring |
|---|---|
| **no per-kind roll policy or count bands** | The owner's numbers (species 0-1 → 7-10, item much higher, action 1-5) exist nowhere. No tuning file declares them, per kind or at all |
| **no species → container mapping** | `species-passive.{speciesId}` is a legal `container_id` and not one exists. Nothing maps a demon species to a container |
| **no player-creation expansion step** | Nothing anywhere rolls a per-player roster at profile creation. There is no such hook |
| **`omni` is refused in an element slot** | `ActorElementTypes.cs:84` throws *"Element slot 'X' cannot use omni."* `omni` exists as a registered channel (`status.power.omni`, `status.resist.omni`) but a single atom that writes all six `combat.*` element channels is **not expressible today**. §4.3 explains why that matters |
| **`stat.derived` is quarantined** | The kind an effect would most naturally use to write a derived channel is not wired end to end |

---

## 3. The shape this suggests

### 3.1 The pipeline, in four stages

```text
[1] SEED                    seedsmith, offline
    atom families (28 authored -> ~980 generated rows)
    container definitions: fixed core + weighted pool + group
    per-kind count bands, in tuning
                |
                v  AtomImporter, one transaction
[2] VERSIONED CATALOG       catalog_revision++ only when something changed
                |
                v  Instantiator.Draw + TryInstantiate(container, lookup, rollSeed, thetaContent)
[3] CONCRETE INSTANCE       effect_instance + effect_instance_atom
    frozen values, roll_seed, power at roll time
                |
                v  effect_binding
[4] ACTOR'S EFFECT LIST     iterated (priority DESC, container_id ASC, seq ASC), ordinal
```

Stages 2, 3 and 4 are built. **Stage 1's authoring and the call from 2 to 3 are this program.**

### 3.2 Six sources, one sink — the owner's seven, reconciled

The owner named seven container kinds; the grammar
(`^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`) has six. Owner's decision: map
onto the existing six, **no grammar change**.

| Owner's word | Kind | Note |
|---|---|---|
| item | `item` | — |
| trait | `trait` | — |
| action | `skill` | active: its atoms declare a trigger |
| passive skill | `skill` | passive: `stat.modify` / `stat.derived`, which **must declare no trigger** — authoring one is `TriggerNotAllowed` |
| aura | `world-buff` or `patron` | by source |
| **specie demon** | `species-passive` | **the race layer** |
| **unique demon** | **not a kind — the ACTOR** | its effect list is the union of every container bound to it |

**A unique demon is never generated as a container.** Containers are generated and *bound*. This
follows directly from *"Items have no behaviour. Actors do."*

### 3.3 Three tiers of demon, and what each carries

The race analogy, made concrete:

| | Species passive | Own roll | Trait | Passive skill | Items | Title / commander |
|---|---|---|---|---|---|---|
| **Lawn spawn** (non-unique) | ✅ shared | — | — | — | — | ✅ commander buff |
| **Summoned / gacha demon** | ✅ shared | ✅ its own | — | — | — | ✅ |
| **Unique demon** (actor) | ✅ shared | ✅ | ✅ | ✅ | ✅ | ✅ |

**The species instance is rolled once per player, at player creation, and never changes.** Every
conezombie that player ever meets reads the same frozen instance — one row, no per-spawn allocation on
the Unity main thread, and *"what does a conezombie do"* stays answerable for that player.

Two players' conezombies differ. One player's conezombies never do.

### 3.4 Each feature owns its own count band

Owner: *"feature define tunable variable and balance by them self."*

So the **mechanism** is shared — one producer, one `Instantiator`, one instance table — and the
**policy** is per kind, in that kind's own tuning file:

| Kind | Owner's stated band | Lives in |
|---|---|---|
| `species-passive` | 0-1 at rung 1 → **7-10 at rung 10** | `data/tuning/demon-species-effects.v1.json` |
| `item` | much higher — affix + prefix + set + rarity bonus | the item program's own tuning |
| `skill` (action) | ~1-5, *"we will fine tune later"* | the action program's own tuning |
| `trait` · `patron` · `world-buff` | not yet stated | their own |

This is the right split, and it is also the one that keeps `pool_rolls` honest — see §4.3, where the
species band and the shipped item ladder turn out to disagree.

---

### 3.5 The release shape — where each stage actually runs

Owner, 2026-09-01, clarifying Q7:

> *"seedsmith only generate seed **when dev phase**. we will ship json seed and commit it to this repo,
> then we will deploy it in release. the **in game runtime** will read the seed, run in game generate,
> and generate map table and concrete species for use **when player is created**."*

| Stage | Runs | Ships |
|---|---|---|
| seedsmith | **dev machine only**, never a player's | nothing — it is a tool |
| the JSON seed | — | **committed to this repo, shipped inside the release** |
| import | player's machine, first run and on version change | the catalog in SQLite, `catalog_revision` stamped |
| the runtime generator | **player's machine, at player creation** | the concrete rows in that player's tables |

**Three consequences worth stating.**

**No model ever runs on a player's machine.** Seedsmith is a development tool (demon-seed Q10), so
everything a model decided is already frozen into committed JSON before the game is packaged.

**The seed is release content, not a database.** It ships as files and is imported, which is exactly
`tools/AtomImporter`'s existing flow — validate-all-then-write in one transaction with a
`catalog_revision` bump. Nothing new is needed to deliver it.

**The map goes through the same pipeline.** The owner named *"map table and concrete species"* in one
breath, which is the seed→concrete principle doing what it was declared to do: one SDK, many content
pipelines. The world map is another feature's content, not another mechanism.

### 3.6 The roster is derived, not merely stored

Owner, on Q5: *"base specie (around 900) is generate by read seedsmith generated seed, it only generate
when game start and frozen, **generate by deterministic function, so very fast**."*

This is stronger than "we save it somewhere". Given `(worldSeed, catalog_revision)`, the whole 904-species
roster is **reproducible** — so the player's table is a **cache of a derivation, not the only copy of a
fact.**

| Property | Because |
|---|---|
| a lost or corrupted roster table can be rebuilt | the inputs are the world seed and the catalog revision, both retained |
| a support question has an answer | *"why does my conezombie have this?"* replays exactly |
| storage is an optimisation | not a correctness requirement — it exists so 904 rolls happen once, not per session |
| the derivation must stay pure | **no clock, no `Random()` without the seed, no dictionary iteration order.** A single impure input destroys every row above |

That last row is the cost of the property, and it is worth paying: it is the same discipline
`definitions.md:170`'s reproducibility law already requires of instances.

---

## 4. Prior art — how many effects a real ARPG item carries

The owner's claim — *"a strongest item maybe contain more than 30-40 effect, that feel crazy but real
in diablo like game"* — is worth checking, because it sets the shape of every count band.

`docs/research/arpg-effects/` already covers stacking, procs, ailments and crit, and **does not cover
affix counts.** So this is new, and narrow: two searches, not a pass.

### 4.1 The measured numbers

| Game | Rolled modifiers on one item |
|---|---|
| **Path of Exile**, rare | **max 6 explicit** — 3 prefixes + 3 suffixes; **+1 implicit** from the base = **7 total**. Jewels are the exception at 4 (2+2) |
| **Diablo II**, rare | **2 to 6 affixes**, max 3 prefixes / 3 suffixes, chosen 50/50 |
| **Diablo II**, unique / set | custom-designed, **any number** — not bound by the affix system |

**So the rolled ceiling in the genre is six.** Not thirty. Even a fully-crafted PoE item with implicit,
enchant and a corrupted mod lands around eight or nine.

### 4.2 ⭐ D2's affix groups are the mechanism we already have

D2's rule: *"one Rare also can't have more than one prefix or suffix from the same group (a set of
prefixes or suffixes that have the same effect, in progressively larger amounts)."*

**That is exactly `effect_container_pool.group`**, and exactly why `spec-container-schema.md` says one
action never rolls `+10 atk / +12 atk / +14 atk`. The default grouping `(family_id, variant)` is D2's
affix group, arrived at independently. **Good sign: the schema's pool mechanism is the genre's, not an
invention.**

### 4.3 ⭐ The reconciliation — an atom is finer than an affix line

Both numbers can be right, because **they count different things.**

An affix *line* is what a player reads. An **atom** is one channel write. Our atoms are per-element,
because `atom_id = {family_id}[.{variant}].t{tier}` and the element is the `variant` — that is exactly
how 28 authored families become ~980 rows.

So one player-facing affix can be several atoms:

| Player reads | Affix lines | Atoms |
|---|---|---|
| `+40-50 hp` | 1 | 1 |
| `+15% fire resistance` | 1 | 1 |
| **`+15% to all resistances`** | **1** | **6** — one per element variant |

**Six equipped items × 6 affix lines each ≈ 36 lines, and well past 40 atoms once multi-channel affixes
are counted.** The owner's 30-40 is right *for an actor's total effect list*; the genre's 6 is right
*for one item's pool rolls*. Neither contradicts the other, and conflating them would set every count
band wrong.

**Three consequences, and the third is a real gap:**

1. **`pool_rolls` counts pool draws, not atoms.** Keep the item ladder's shipped 5-6 at rung 100; it
   matches PoE and D2 exactly. Do not widen it to chase 30-40.
2. **Set bonuses, rarity bonuses and implicits belong in the fixed core**
   (`effect_container_atom`), not in the pool. They are not rolled, so they must not consume a roll.
   The schema already separates the two — *"fixed core plus optional weighted pool"* — and this is what
   that separation is for.
3. **⚠️ "All resistances" is not expressible as one atom today.** `ActorElementTypes.cs:84` throws
   *"Element slot 'X' cannot use omni."* So a single affix covering all six elements must currently be
   six atoms in six different groups — which would consume **six pool rolls**, the entire budget of a
   rung-100 item, for one affix line. Either `omni` becomes legal as a fan-out variant for `combat.*`
   families, or multi-element affixes live in the fixed core. **This is a real gap and it is cheap to
   fix now, expensive after content exists.**

### 4.4 The species band, checked against the item ladder

The owner wants a rung-10 species passive to carry **7-10 effects**. `ssot-rarity.md` §3.3's shipped
band gives a rung-100 *item* **5-6 pool rolls**.

**Taken as pool rolls, the species band exceeds the best item in the game**, which inverts the ladder —
`ssot-rarity.md` §8.6's named failure mode. Taken as *atoms* under §4.3's distinction, 7-10 atoms is
roughly 3-5 rolls of multi-channel families, which sits comfortably below an item. **The bands must
state their unit.** Recorded as open question Q3.

**Sources:** [PoE Wiki — Modifiers](https://pathofexile.fandom.com/wiki/Modifiers) ·
[Diablo Wiki — Rare Items (Diablo II)](https://diablo-archive.fandom.com/wiki/Rare_Items_(Diablo_II)) ·
[Diablo Wiki — Affixes (Diablo II)](https://diablo-archive.fandom.com/wiki/Affixes_(Diablo_II))

---

## 5. The resolution model — four layers (2026-09-01), **five as of 2026-09-03**

> ⭐ **A fifth layer was added by the owner on 2026-09-03 — see §5.6.** It is the fifth layer *added*
> and the **first to run**: it composes the pool the others draw from. §§5.1–5.5 below are unchanged and
> still correct; read §5.6 before treating the four-layer table as complete.

> *"we have something like modify stat, like +10 hp — this is concrete atom effect. but when game
> runtime read the json it shouldn't read +x hp, **that wrong**. it should read +x derived stats in this
> pool … so for example we have a affix `element master of X`, the seed is `+x element power of Y`,
> Y is a pool of [6 type of element]."*
>
> *"layer 1 define the shape of container, how many atom effect, chance it appear · layer 2 define the
> pool, how many derived stats, chance it appear · layer 3 define the range of value · layer 4 make
> resolve number, resolve derived stats, resolve list of atom in 3 layer above"*

**The decomposition is correct, and three of the four layers are already built. The missing one is the
cause of §4.3's defect.**

### 5.1 Where each layer lives today

| Layer | What it decides | Status |
|---|---|---|
| **L0 — pool composition** | **which affixes are even candidates, and at what rate**, given the container's power/rarity class and the channel delivering it | ⭐ **ADDED 2026-09-03 — §5.6.** Real gap |
| **L1 — container shape** | how many atoms, and the chance each appears | **BUILT** — `pool_rolls`, `weight`, `group` |
| **L2 — the channel pool** | *which* derived stats, how many, chance each | **DOES NOT EXIST** |
| **L3 — value range** | the min/max a magnitude may roll into | **BUILT** — the value spec `{min, max, roll, scale}` and `overrides_json` |
| **L4 — resolve** | pick the atoms, pick the stats, freeze the numbers | **BUILT but inert** — `Instantiator.Draw` + `TryInstantiate` |

### 5.2 Why L2 is missing, and why its absence is a real defect

**The channel is baked into the atom's identity.** `atom-family-library.md` §2:

> *"one family per combat family, with the element slot as the **`variant`** column … `atom_id` derives
> as `{family_id}[.{variant}].t{tier}` and the unique key is `(family_id, tier, variant)`, so seven t1
> element rows would collide if the element lived only inside `params_json`."*

That is a sound **storage** decision — the rows must be distinct. But it means the catalog has no way to
say *"element power of **some** element"*. Every element is a different atom, so `element master of X`
is not one affix with a hole in it; it is six unrelated candidates.

**And that is exactly the defect §4.3 found from the other direction.** `+15% to all resistances` had to
become six atoms in six groups, consuming **six pool rolls — the entire budget of a rung-100 item — for
one affix line.** That is not a tuning problem; it is L2 missing, showing up as a budget problem.

**The owner's model dissolves it.** With a channel-selection layer, `+15% all resistances` is **one
affix, one pool roll, six atoms**. `element master of X` is **one affix, one roll, one atom, element
chosen at resolve time.** The `omni`-variant workaround proposed in §4.3 is no longer needed — see Q4,
now superseded.

### 5.3 What L2 needs: a slot, and an affix that is a bundle

Two things, and the second is the one the owner's *second* example proves.

**A variant slot.** A container's atom reference names a *slot* rather than a concrete variant, and the
slot declares its domain and how many it takes:

```text
slot E1 : domain = element, pick = 1
atom ref: atom.elemental-power.$E1
```

At L4, `$E1` resolves to a concrete variant, and the concrete `atom_id` is looked up as it is today.
**The atom catalog does not change at all** — only the container's *reference* becomes parameterised.
Validation gets *stronger*, not weaker: a patterned ref must resolve for **every** member of its
domain, so a missing element row is caught at load instead of at roll time.

**A pool draw must be able to yield more than one atom.** This is what *"master of fire and ice"*
demonstrates:

> *"+x1 to x2 power of fire, +x3 to x4 power of ice, +x5 to x6 defense of fire, +x7 to x8 defense of
> ice"*

Four atoms, **two families, two elements, and the elements are the same across both families.** Today
`effect_container_pool` draws **one atom per row**, and four independent draws cannot be correlated —
nothing links the variant chosen for `elemental-power` to the one chosen for `elemental-defense`. So
this affix is not expressible as a roll at all.

**Therefore the pool's unit must be an affix, not an atom** — a named bundle of atom refs that share
the container's resolved slots and are drawn together as one roll. That is precisely what a Diablo or
PoE affix is, and §4.2 already found that the `group` mechanism is D2's affix-group rule; this is the
other half of the same borrowing.

| Owner's example | Slots | Atoms per draw | Expressible today |
|---|---|---|---|
| `element master of X` | `E1: element, pick 1` | 1 | no — six unrelated candidates |
| `master of fire and ice` | none (fire/ice named) | 4, correlated | no — four independent draws |
| `+15% all resistances` | `E*: element, pick all` | 6 | only at a cost of six pool rolls |

### 5.4 Two refinements the four layers need before they are a design

**The resolution order must be fixed, and it is not the order the layers were listed in.**
L4 says *"resolve number, resolve derived stats, resolve list of atom"* — but the dependencies run the
other way:

```text
1. slots      pick concrete variants                              (L2)
2. affixes    draw which affixes appear                           (L1)
3. atoms      expand each affix's refs against the resolved slots
4. tiers      pick tier within the container's min_tier/max_tier window
5. values     roll each magnitude in its range                    (L3)
```

A slot must resolve before you know which concrete atom to look up, and a tier must resolve before you
know which value range applies. **Stating the order is what makes the roll reproducible**; leaving it
implicit is how two runtimes disagree.

**Each layer needs its own named RNG stream.** E2 already ships *"named RNG streams (`atom.apply`, plus
the per-instance roll seed)"*, so the mechanism exists — it must be *used* here.

If all four layers draw from one stream, then **adding a layer later shifts every historical roll**, and
`CatalogRevision` cannot protect against it: that column detects a *content* change, not a change in how
many numbers the resolver consumed. Every already-owned item would silently re-resolve differently on
replay. Separate streams (`affix.slot`, `affix.draw`, `affix.tier`, `atom.value`) make each layer's
consumption independent, which is the only way the reproducibility law at `definitions.md:170` survives
this program's own future edits.

### 5.5 What this costs

An honest statement, because it is not free:

- **New:** an affix entity (a named bundle of atom refs) and a slot declaration. `effect_container_pool`
  references affixes instead of atoms.
- **Amended:** `effect-atom/definitions.md` and `spec-container-schema.md` — both **win over any spec**,
  so this is a reviewed change to them, not a spec decision.
- **Unchanged:** the atom catalog, `atom_id` derivation, the unique key, `pool_rolls`, `group`,
  rarity to count and tier window, the instance tables, and the compiler. **L2 is inserted between
  existing layers, not laid over them.**

The timing argument is straightforward: containers do not exist yet. Adding a layer now costs a schema
edit; adding it after content exists costs a migration of every container ever authored.

---

### 5.6 ⭐ L0 — pool composition: power and rarity awareness (owner, 2026-09-03)

> *"we need add new layer on effect pipeline to 5 layer instead of 4 — that is power and rarity
> awareness layer. the LLM will resolve by closed enum. our deterministic engine will distribute and
> resolve atom effect rate by the enum."*
>
> *"because if we allow drop table drop any thing, some very strong options can found on a weak item.
> the set bonus, socket bonus, unique affixes bonus will become useless, craft system will become
> useless, and later new mechanism like world map will become useless. no one want to farm, boss fight
> or craft because they can loot anything from kill normal zombie in the run."*
>
> *"so we need make our atom effect pool become a lot of pool not only one."*

#### 5.6.1 The problem, stated against shipped code

Every lever that differentiates an acquisition channel today is a **volume** lever, never a **kind**
lever:

- `loot_source(source_kind, source_id) → table_id` points each source at its own drop table
  (`ssot-generation.md` §5.1);
- `drop_table_entry` carries `rarity_floor` and `rarity_weight_shift_json`, so a boss rolls higher rungs
  more often;
- rarity then buys affix **count** and **tier window** (`ssot-rarity.md` §3.3).

**But once a container is selected, the affix pool it draws from is a property of the container, not of
the source.** A `plate-helm` dropped by a trash zombie and one dropped by a boss draw from the same
affixes. Only probability differs.

**In most games rarity-as-volume-gate would be enough. Here it is not, and the reason is our own SSOT.**
[AGENTS.md](../../AGENTS.md) makes *no hard progression ceilings* a hard boundary — endless grind is the
thing other systems reconcile to. With a level cap, an ilvl gate holds forever. **With endless grind,
every volume gate eventually opens**: enough trash kills reach the same outcome as the boss. So the
absence of a kind-gate is not a balance preference here; it is a structural hole that the no-ceilings
rule guarantees will be found.

The consequence the owner names follows directly: if the strongest affixes are reachable from trash,
then sets, sockets, uniques, crafting and the world map are all **redundant paths to something you were
going to get anyway.**

#### 5.6.2 What L0 is

**One deterministic function, run before L1:**

```text
poolFor(container, channel, rarity)  ->  [ (affixId, weight) ]
```

Two closed enums are its inputs, and **the split between them is the whole design**:

| Axis | Who decides | Values |
|---|---|---|
| **powerClass** — how strong this affix is *as an idea* | ⭐ **the LLM**, by closed enum | authored per affix, carried with a `basis` |
| **channel** — how the effect is being delivered | the call site | `drop` · `boss` · `set` · `socket` · `unique` · `craft` (owner, 2026-09-03) |

The **rate** is never authored and never chosen by a model. A deterministic policy table maps
`(powerClass × channel) → weight`, and it lives in `data/tuning/` because it is the balance surface
([tunables-ssot.md](tunables-ssot.md)):

```text
                 drop    boss     set    socket  unique   craft
 common          high    high     —      med     —        high
 …
 top-shelf       0.01%   low      HIGH   low     fixed    med
```

**Why the LLM decides the class and not the rate.** This is `seedsmith-map.md` P1 applied without
amendment: *the LLM writes identity; deterministic code writes magnitude.* A model has no calibrated
sense of scale, so a weight it picks is a plausible-looking guess — but *"is `Master of Fire and Ice` a
top-shelf effect or an ordinary one?"* is a judgement about what the thing **is**, which is exactly what
a model is for.

⭐ **And the classification does not go stale, because the enum is an input to balance rather than an
output of it.** A balance pass moves the *rates* in the tuning table; it never moves the classifications.
That is the property that makes an authored class safe here, where seedsmith's `power-estimate` had to
mark its tiers **provisional** — that one estimates a *measurable* quantity that a real observation later
contradicts, and this one does not.

#### 5.6.3 ⭐ L0 consumes no RNG, and that is what makes it safe to add late

§5.4 warns, correctly, that adding a layer shifts every historical roll — `CatalogRevision` detects a
*content* change, not a change in how many numbers the resolver consumed, so every owned item would
silently re-resolve differently on replay.

**L0 escapes that warning by construction.** It is a pure function of `(container, channel, rarity)`
and draws no random numbers: it *composes the candidate list* that L1's existing `affix.draw` stream
then draws from. No new stream, no extra consumption, no shifted history.

**This is a design requirement, not an observation.** If L0 ever rolls anything itself, it acquires the
exact fragility §5.4 describes. Whatever varies per drop belongs in L1's draw, never in L0's
composition.

#### 5.6.4 Never zero on a drop — the 0.01% floor (owner, 2026-09-03)

**A `drop`-channel cell may be vanishingly small. It may not be zero.**

This is **D7 stated from the other direction**, and the two together are now one principle the item and
effect programs share:

> **There is always a path, and the path costs the right thing.**
> D7: crafting is gated by *cost*, never by luck — *"don't make it impossible by chance, that is not
> fun."* L0: the strongest affixes are near-zero from trash and reliable through the channel that is
> *for* them.

The floor lives in the tuning table as a named minimum, so a balance pass cannot silently write a zero.

⚠ **The other channels may be exclusive in either direction, and one of them must be.** A hand-authored
unique's fixed affixes are not rollable at all — that is what `ssot-uniques.md` means by *"a unique may
break every rule that lives in the generator."* A structural zero there is legitimate and, per
AGENTS.md, **must carry a comment saying why it is exempt**: it is a content-availability rule, not a
cap on a magnitude.

#### 5.6.5 What this is, and is not, in terms of existing machinery

Stated plainly so nobody rebuilds a draw loop.

| | Verdict |
|---|---|
| The weighted draw itself | **BUILT** — `Instantiator.Draw:130-155`, `AtomRandom.NextBelow:66`. L0 produces its input, it does not replace it |
| Per-container affix weights | **BUILT** — `effect_container_pool.weight` |
| Per-source drop tables, rarity floors and weight shifts | **BUILT** — `ssot-generation.md` §5.1 |
| Tag-based eligibility | **BUILT but unwired** — `EligibilityResolver.DrawablePool` (`EligibilityRule.cs:60-95`) has **no production caller** and its `tagsOf` delegate is supplied by nothing |
| An affix's **power class** | ⛔ **REAL GAP.** `AtomRow.TagsJson` carries *thematic* tags (`offensive`, `elemental`) — nothing says how strong an affix is relative to others |
| A `(powerClass × channel) → weight` policy | ⛔ **REAL GAP** |
| Composing per-container pools at scale | ⛔ **REAL GAP.** With ~1,844 generated sets (`item-ideal.md` D12), hand-authoring a pool per container is not available |

**So L0 is a classification and a distribution policy on top of machinery that already exists** — not a
new draw, and not a replacement for `eligibility-tags`. It **extends** that module rather than
superseding it: eligibility answers *may this affix appear here* (binary); L0 answers *at what rate*
(weighted). Binary allow/deny cannot express 0.01%, which is the whole point.

---

## 6. The per-feature atom pipeline — and the one demon-seed forgot

Owner, 2026-09-01, answering what looked like a units question and correcting the scope instead:

> *"better to make atom specific for each feature and we need make pipelines for it too. **so we really
> miss these pipeline on our specie generator — we just generate demon species without ship atom
> container for it.** other feature need make they own pipeline. we will generate atom seed for specie
> **after** other pipeline complete, because they need data from specie seed like family, rarity, favor,
> lore."*

### 6.1 The scope boundary this draws

| Owns | What |
|---|---|
| **`effect-pipeline`** (this program) | the **SDK and the schema** — affix, slot, the four layers, the resolution order, the RNG streams, the producer that calls `Instantiator` |
| **each feature program** | its own **atom/affix content pipeline** — what its containers actually carry |

**One mechanism, many content pipelines.** That is the same shape as the seed→concrete principle: the
runtime SDK is shared and never duplicated, while what goes *through* it is authored per feature.

**Reconciled with Q6.** The affix *library* stays shared and tag-gated — that is what Q6 chose, and it
is what stops "elemental mastery" being authored once for items and again for species. A feature
pipeline therefore does two things: it may **author affixes specific to its domain**, and it **assigns
which affixes its containers are eligible for** by choosing tags. It does not fork the library.

### 6.2 ⛔ The gap the owner just found in `demon-seed`

**He is right, and it is a real gap in a map that has already been written.**
[demon-seed-map.md](demon-seed-map.md)'s fourteen modules take an almanac entry all the way to a
per-player concrete species — anchors, threat bands, rarity, stats, import, runtime — and **not one of
them produces an effect container.** `species-passive.{speciesId}` is a legal `container_id`; zero
exist; nothing in that map creates one.

So a demon generated by that program has an element, an aptitude, a rarity and a full stat block, and
**does nothing**. The gap was invisible because every module in the map was individually correct.

`demon-seed` gains a module — provisionally `species-effects` — and it sits **after `anchor-emit`**,
for the reason the owner gave: it consumes anchor output.

### 6.3 What the species atom pipeline reads, and what each input constrains

This is why the ordering is not negotiable — the pipeline is a function of the anchor:

| Anchor field | What it constrains in the container |
|---|---|
| `rarity` | `pool_rolls` and the `min_tier`/`max_tier` window — **entirely, and numerically** |
| `elementPrimary` · `elementSecondary` | which element variants the slots may bind to |
| `aptitudePrimary` · `aptitudeSecondary` | which channel families are thematically eligible — Might to power, Fortitude to mitigation, Vigor to shield |
| `posture` (derived) | cross-check: a Bastion species drawing only offensive affixes is a defect |
| `resourceProfile` | which `resource.delta` families are legal at all |
| `family` · `traits` | the shared identity a family's members should visibly share |
| `flavorInfo` / lore | **the actual judgement** — what this creature *does*, in its own words |
| `threatBand` | nothing here. It is a `Θ` offset and belongs to magnitude, not membership |

### 6.4 What the model picks — and what it must never pick

Seedsmith P1 binds harder here than anywhere, because a pool has numbers all over it.

| The model picks | The tables pick |
|---|---|
| **which affix families** a species is eligible for | `pool_rolls` — from rarity |
| the **slot bindings** (which elements a slot may take) | the tier window — from rarity |
| an **ordinal affinity** per affix: `core` · `likely` · `occasional` | the **weight** each affinity maps to |
| the container's tags | every magnitude, from tier bands |

**The affinity ordinal is the load-bearing trick.** A model cannot be allowed to write `weight: 40`, but
it can reliably say *"a fire drake's fire-power affix is **core**, its ice-resist affix is
**occasional**."* A tuning table turns three ordinals into three weights, and a balance pass retunes all
904 species with one file edit instead of a regeneration run.

### 6.5 Two prerequisites, and one of them is not demon-seed's

The pipeline cannot run until **both** exist:

1. the anchors — `anchor-emit`, inside `demon-seed`
2. **an affix library to choose from** — which is `effect-pipeline`'s, not demon-seed's

That second one is the real sequencing consequence of §6.1's split, and it is worth stating plainly:
**`demon-seed` cannot finish on its own.** Its species will have no effects until this program ships an
affix library and a schema for containers to reference it.

### 6.6 Prefix and suffix are two pools, not one

Owner: *"affix/prefix, don't mistake, we have 2 not only affix — affix and prefix is 2 containers."*

**The repo already decided how the two are distinguished, and it decided they are not authored.**
[item/seed-contract.md](item/seed-contract.md) §2.1:

> `affixClass` (prefix/suffix) — **DERIVED from `kindId`** · *permanent-modifier kinds are prefixes;
> triggered kinds are suffixes.* **Present in a seed file → reject.**

So the class is a **consequence of the atom's kind**: a `stat.modify` / `stat.derived` affix is a
prefix, a triggered `resource.delta` / `status.apply` affix is a suffix. Nobody types it, and a seed
file that tries to is rejected.

**What follows for L1: `pool_rolls` is two numbers, not one.** D2 and PoE both cap the two classes
separately — max 3 prefixes *and* max 3 suffixes — which is why a rare tops out at 6 rather than
drawing 6 of whichever it likes. A single count would let a container roll six permanent modifiers and
no triggered effect at all, and the result reads as a stat stick rather than an item.

| | Today | Needed |
|---|---|---|
| `effect_container.pool_rolls` | one INT | **`prefix_rolls` and `suffix_rolls`** |
| the one-per-group rule | unchanged | applies within each class |
| rarity's band | one count band | **a band per class** |

For a species passive specifically, this is a genuinely useful split: the **prefix** side is what the
race *is* (permanent stat shape), and the **suffix** side is what it *does* (on-hit, on-death, on-spawn).
A species with prefixes and no suffixes is a stat block; one with suffixes and no prefixes is a gimmick.

---

## 7. Questions — all closed, 2026-09-01

Nine questions were raised by this document. **Every one is answered.** Recorded with the reasoning, so
a downstream session does not reopen them.

| | Question | Answer |
|---|---|---|
| **Q1** | where does a summoned demon's personal roll live? | a **`trait` container** — **but see Q10**, which corrects what that means. The answer was underspecified: `traits_json` already exists |
| **Q2** | what fills `RollSeed`? | a **per-player world seed**, hashed with the target id |
| **Q3** | do count bands count rolls or atoms? | *superseded* — answered as a scope correction: bands are **per feature**, and each feature owns an atom pipeline (§6) |
| **Q4** | should `omni` become a fan-out variant? | *superseded by §5* — with a channel-selection layer the workaround is unnecessary. `omni` stays a registered channel; `ActorElementTypes.cs:84`'s refusal in an element slot stands |
| **Q5** | what happens on a catalog change? | **existing rolls frozen forever; new species appended** on next load, from the same world seed. A retune reaches new rolls only |
| **Q6** | how does a container declare eligible affixes? | **tags, plus a per-container allow/deny override** — what PoE does |
| **Q7** | how far does the world seed reach? | **the whole save** — every per-player generator derives from it (§3.5 for where each stage runs) |
| **Q8** | do prefix/suffix bands differ per kind? | **yes** — each kind declares its own pair per rung, in its own tuning file |
| **Q9** | where do affixes come from? | **hybrid** — single-family affixes rule-generated from the atom library; multi-atom named affixes LLM-authored, because their identity is a judgement |

### ⭐ The four effect paths that reach an actor today

Enumerated 2026-09-01, because two of them were discovered one at a time and the third and fourth were
found only by looking for more. **The map must state a disposition for each, or `instance-producer`
quietly becomes a fifth.**

| # | Path | How it reaches an actor | Disposition |
|---|---|---|---|
| 1 | `Instantiator` → `effect_binding` | the atom layer | **the new one** — built, inert |
| 2 | `rpg_unique_stat_mods.mods_json` | `UniqueEquipmentCatalog.BuildModsJson` from equipped slots | **ABSORBED** — Q11 |
| 3 | Secondary plugin → grant | `PatronSecondaryPlugin`, `GrantId = "patron:aura"` | **ABSORBED** — Q13 |
| 4 | `AuraContentCatalog` → grant | commander/world auras | **DEFERRED by its owning program**, with evidence |

**Path 4 is not an oversight, and this document does not overrule it.** `AuraContentCatalog.cs:10-16`
states the limit itself: *"Not authored as `world-buff.*` DB containers, and that is a **stated scope
limit, not an oversight**"* — because `spec-aura-content.md` §2 proved a `world-buff.*` container is not
read by the live-lawn scope/grant pipeline. That program investigated and deferred with a reason; the
right move is to record the disposition, not to reopen it from outside.

### Q11 — `mods_json` is absorbed, not paralleled (2026-09-01)

Owner: *"absorb it. equipment spec is generate before atom effect ship, so it is not complete, i still
defer it until now, so this time to fix it."*

`rpg_unique_stat_mods.mods_json` predates the atom layer, and E6 always planned to take it:
*"`effect_binding` … **absorbs today's `mods_json` grant blobs**."* The deferral ends here. Equipped-slot
effects move to bindings, `mods_json` becomes derived and then goes.

**It gets its own module rather than living inside `instance-producer`.** The producer's job is to prove
the atom path works where there is no shipped data to break; absorbing `mods_json` is a migration of
live, save-affecting unique-actor equipment. **Two risks in one change is how a proof becomes a
post-mortem** — so they are sequenced, not merged, and the absorption runs only after the path is proven.

### Q13 — the patron aura is absorbed too (2026-09-01)

Owner: absorb it — `patron.*` becomes a real container kind rather than a plugin.

The move is smaller than it sounds: `patron.*` is **already legal in the `container_id` grammar and
unused**, and E15's `AtomRunner` is described as *"the Secondary effect runner"* — the same layer
`PatronSecondaryPlugin` runs on today. So this is a relocation within one layer, not a jump between two.

**But it carries a risk the `mods_json` absorption does not, and it must be stated.**
`PatronPolicy.AuraMilli(rarity, star, level, pTheta, powerTuning)` is a **shipped formula** whose SIM
half is already shipped and whose LIVE gate is still open. It scales **continuously** with star and
level; a container's atoms carry **discrete tiers**.

Two consequences:

- **The mechanism exists.** E2's `effect_curve` — integer-per-mille interpolated points — is exactly how
  a value spec reads a continuous input, so patron's atom keys its curve on star/level rather than
  taking a flat tier. Nothing new is needed.
- **⛔ Byte-identical behaviour must be *proven*, not intended.** The patron program has SIM results
  standing against the current formula. If absorption moves a single number, those results are
  invalidated and the open LIVE gate gets harder, not easier. **The acceptance criterion is a
  before/after equality test across the full (rarity × star × level × Θ) grid**, not a spot check.

**⭐ And the ground is already staked.** `data/seed/containers/patron.json` **exists and is committed**:

```json
{ "id": "patron.aura", "kind": "patron", "poolRolls": 0, "atoms": [],
  "tags": { "marker": "fx.patron_aura" } }
```

An empty `patron.aura` container carrying a marker tag that names **the exact `EffectId`
`PatronSecondaryPlugin` emits today**. So `patron-absorption` is not greenfield — it fills in a
container that already has the right id, the right kind, and the right correlation marker. **The module
is smaller than Q13 implied**, and someone anticipated this absorption before it was decided.

**This is why it is its own module and not a line inside `mods-absorption`.** One migrates stored save
data; the other relocates a hot-path plugin whose output is under an open gate. Different data, different
risk, different proof.

### Q12 — A variant modifies the roll; it is not a container (2026-09-01)

Owner: *"option 1 — in some game more rarity will give better options and stats right?"*

**Yes, and the distinction matters enough that this repo already locked it.** `ssot-rarity.md` §3.6:

> *"'Rarity should scale magnitudes' — **No.** `CurveInput.Rarity` exists in code (`CurveTable.cs:7`) and
> is **banned on `container_kind = 'item'`**. A multiplier on the rung makes rarity dominant and destroys
> the overlap the owner asked for."*

And §4.5 measured the same thing across every game surveyed: **rarity buys breadth and ceiling, never
power.** Better stats are a *consequence* of more draws and a higher tier ceiling — which is exactly why
a top-roll Grafted can beat a low-roll Fused, and why low rungs stay live content.

So a variant shifts **resolution parameters**, and authors nothing:

| Variant | Shifts |
|---|---|
| `ancient` | tier window up one step |
| `mutated` | +1 pool draw, -1 tier |
| `corrupted` | rerolls one element slot |
| `blessed` | +1 prefix roll |
| `cursed` | +1 suffix roll, -1 prefix roll |
| `shiny` | cosmetic only |

Zero new containers, zero new authoring, and a variant is felt across the whole roster at once rather
than needing an effect written per variant per species. It lives in `resolution-order` (module 2)
because it is a parameter of the resolve, not content.

**⚠️ One bound falls out.** Rarity sets the tier window and a variant shifts it, so a rung-10 `ancient`
can push past **t5 — the highest tier that exists.** That **saturates**, and it is a *structural* limit
(there is no t6 row to select), not a progression ceiling. `AGENTS.md` exempts structural limits from the
no-caps rule **and requires the comment saying so** — which this one must carry, or a later sweep will
correctly flag it as an illegal cap.

### Q10 — `traits_json` answers *which*; the container answers *what it does* (2026-09-01)

**Q1 was underspecified, and the owner caught it: *"trait is built by demon fusion."*** There are not
three populations of "trait". There are **four**, and the fourth is shipped and in save data:

| Population | Where | Status |
|---|---|---|
| `TraitBattleCatalog`'s 14 | code | 1 migrated to a container (`critical-hunter`); **13 blocked** on event dispatch, the kind ceiling, the turn kernel, and the AI/rewards layers |
| `DemonSpeciesDef.TraitPool` | the species | string ids — the source pool a roll draws from |
| **`traits_json`** | `RpgStore.Demons.cs:69`, on the demon row | **shipped, live, in save data** |
| Q1's summon-rolled container | proposed | — |

`FusionRoller.Roll` inherits parent traits and `RollPromotionTraits` grows them on promotion, both
writing `traits_json` as a list of ids validated against `DemonTraitCatalog`. **That is a working
system.** So `traits_json` and a trait binding would be two answers to *"what traits does this demon
have"* — the two-sources-of-truth defect.

**The rule that resolves it:**

> **`traits_json` is the source of truth for *which* traits a demon has.
> A `trait.{traitId}` container is *what that trait does*.
> A demon's trait bindings are DERIVED from `traits_json`, never stored beside it.**

Four consequences, and all of them shrink the work:

- **Fusion needs zero changes.** It keeps inheriting ids; the effects follow because the id resolves.
- **No save migration.** `traits_json` is untouched.
- **The blocked thirteen stop blocking.** A trait id with no container binds nothing — the same
  inert-but-correct state the whole effect layer is in today, lighting up when its container is authored.
- **Summon rolls need no new kind.** A summoned demon's personal roll writes trait ids into
  `traits_json`, which `demon-summoning` (V1) already claims to do. Q1 becomes a far smaller change.

**A convergence worth recording:** `FusionRoller.cs:27` already does
`SeededRng.DeriveStream(seed, "fusion:traits")` — **§5.4's per-layer named RNG streams already exist in
shipped code**, with a naming convention (`system:purpose`) to follow rather than invent.

---

**The two seeding axes compose**, and neither can disturb the other:

```text
hash(worldSeed, streamName, targetId)
       |            |           |
    the save    which layer   what is rolled
                (5.4)         (Q2)
```

---

## 8. Amendments owed before any of this is specced

Every row is a change to a document that **wins over any spec**, so each is a reviewed edit to that
document, not a decision this program may make on its own.

| Document | What changes | Why |
|---|---|---|
| [effect-atom/spec-container-schema.md](effect-atom/spec-container-schema.md) | *"Traits, skills, and **species passives use the core alone**; item templates roll the pool"* — **superseded.** Species passives roll | Q5, and the whole per-player design |
| [effect-atom/spec-container-schema.md](effect-atom/spec-container-schema.md) | `pool_rolls` (one INT) becomes **`prefix_rolls` and `suffix_rolls`**; the one-per-group rule applies within each class | §6.6, Q8 |
| [effect-atom/definitions.md](effect-atom/definitions.md) | a **slot** declaration, and the pool's unit becoming an **affix** (a bundle) rather than a single atom | §5.3 |
| [effect-atom/definitions.md](effect-atom/definitions.md) | the **resolution order** and the **per-layer RNG streams** stated normatively | §5.4 |
| [item/ssot-rarity.md](item/ssot-rarity.md) | rarity governs a band **per affix class**, not one count | §6.6 |
| [demon-seed-map.md](demon-seed-map.md) | a fifteenth module, `species-effects` — already flagged in its §3a | §6.2 |

**Timing is the argument for doing all six now:** no containers exist yet. Today each is a schema edit;
after content each is a migration of everything ever authored.

---

## 9. Adversarial review — seven attacks on this design

Written against the design, not for it. Two are defects that must be closed before any schema work;
two are consequences to accept knowingly; the rest are cheap now and expensive later.

### A1 — ⛔ An affix bundle can carry both classes, and `affixClass` then cannot be derived

**Severity: must fix. This is a hole the bundle idea opened and nothing has closed.**

`seed-contract.md` §2.1 derives the class from the atom's kind — *permanent-modifier kinds are prefixes;
triggered kinds are suffixes* — and that rule was written when **an affix was one atom.** §5.3 made an
affix a *bundle*. So:

> *Searing Aegis* — `+X fire defense` (a `stat.derived`, prefix) **and** `burn attackers on hit`
> (a `status.apply`, suffix)

is a perfectly natural affix whose class is **undefined**. Nothing in the derivation says which budget
it consumes.

Three ways out, and the middle one is best:

| Option | Verdict |
|---|---|
| forbid mixed bundles | enforceable, but forces *Searing Aegis* into two affixes that can roll apart — losing exactly the correlation §5.3 was built to express |
| **a mixed bundle consumes one prefix roll AND one suffix roll** | **Recommended.** Well-defined, needs no new authored field, and prices a mixed affix at what it is worth — two budget slots for two kinds of effect |
| derive from the first or dominant ref | arbitrary, and order-dependent authoring is how the `atom.atom.*` id defect happened |

### A2 — ⭐ The `core` affinity silently dilutes, and the fix is already in the schema

**Severity: must fix, and it is free.**

§6.4 has the model mark each affix `core` / `likely` / `occasional`, mapped to weights. But a weight
cannot express *"always"*. If the model marks twelve affixes `core` and rarity gives `pool_rolls = 3`,
**nine of them never appear**, and `core` has quietly stopped meaning anything — with no error, because
the container is valid.

**Map `core` onto the fixed core, not onto a weight.** `effect_container_atom` already exists for
exactly this — *"Fixed core is determinism: a trait always contains what it says."*

| Affinity | Where it lands |
|---|---|
| `core` | `effect_container_atom` — **always present**, enforceable, and it means what it says |
| `likely` / `occasional` | pool weights, guarded by the existing `pool_rolls ≤ distinct drawable groups` rule |

Two things fall out. **The fixed core needs its own rarity band** (say 0-2), or a rung-1 species could
carry five guaranteed effects while its pool says 0-1. And **a rung-1 species can now still have an
identity** — one defining fixed effect — which the pool-only reading made impossible.

### A3 — "Very fast" is untested, and the cost is writes, not rolls

**Severity: measure before committing.**

Checked what is actually on the instantiation path: `TryInstantiate` applies `ContentScale` per value
(one widened multiply and one divide) and draws from the RNG. It **does not compute power** —
`PowerJson` is nullable, and E9 *"lands later, and backfills."* So the arithmetic really is cheap and
the owner's claim is probably right.

**The unmeasured cost is the write.** ~904 instance rows plus ~5,400 instance-atom rows in one
transaction at profile creation. SQLite handles that inside a transaction, but it has never been run,
and "probably fine" is not a measurement.

⚠️ **And a standing warning for later:** `PowerReads.IntegerFifthRoot` is a **binary search over
`BigInteger`**, needed because *"five categories near 6000 each already overflow Int64."* It is
correctly off the instantiation path today. Moving power onto that path would put 5,400 BigInteger
binary searches inside profile creation. **Keep power backfilled.**

### A4 — Frozen-forever taxes the one loop this repo has optimised hardest

**Severity: cheap now, annoying forever.**

Q5 freezes existing rolls, so **a retuned affix cannot be observed without creating a new profile.**
That is a direct tax on balance iteration — and balance iteration is the loop this repo has spent real
effort protecting everywhere else, moving numbers into `data/tuning/` precisely so a change costs a file
save rather than a rebuild.

The player-facing "offer a reforge" option was considered and rejected, correctly. **A dev-only command
is a different thing**: `POST /api/debug/reforge-world` re-derives the roster from the current catalog
against the same world seed. It ships behind the debug surface, never to players, and it costs one
endpoint because §3.6 already made the roster derivable.

### A5 — The world seed is spoilable. Accept it knowingly.

**Severity: accept, do not discover.**

A deterministic, client-side derivation from a shareable seed means a player can compute their whole
roster before playing, and can **search seeds offline for a good one.** In Diablo this is impossible
because loot rolls at drop time; in Minecraft it is a celebrated feature.

Given the seed is shareable **by design** (Q7), and given a player can reroll at creation anyway,
seed-searching is already implied by the choice. **The point is that it is a consequence of the design,
not a bug to be found later** — and if it ever needs closing, the lever is a server-held salt, which
would cost the shareability that made the world seed worth having.

### A6 — "Each feature owns a pipeline" can re-fragment what the SDK just unified

**Severity: a principle to state now, before the second feature exists.**

§6.1 gives every feature its own atom pipeline, which is right for *content*. The risk is that feature
two forks feature one's prompt structure, affinity vocabulary and validators — **duplication one layer
up from where the shared SDK just removed it**, and against the owner's own *"no need to duplicated code
for all."*

**The content differs; the pipeline shape must not.** One container-authoring pipeline, parameterised
per feature by: its anchor inputs, its eligible families, its rarity bands, and its tag set. A feature
that needs a genuinely different *shape* is evidence the shape is wrong, not that it needs its own.

### A7 — Every conezombie is identical all game. Knowingly traded — and reversible for free.

**Severity: accept, with a noted escape hatch.**

The per-player-species choice means trash variety must come from **wave composition, not the species**.
D3 rolls elite-pack affixes per encounter precisely because identical trash goes stale, and this design
deliberately gives that up for legibility and for zero per-spawn allocation on the Unity main thread.

**Q1's answer already makes the reversal free.** An elite is an actor with **one extra `trait`
binding** — the same mechanism a summoned demon uses for its personal roll. So "elites get their own
roll" can be added later with **no schema change and no migration**, which is the strongest possible
form of a deferred decision.

### Verdict

| | Attack | Action |
|---|---|---|
| **A1** | mixed-class affix bundles | **close before schema** — mixed consumes one of each budget |
| **A2** | `core` dilutes | **close before schema** — `core` maps to the fixed core, with its own band |
| A3 | startup cost | measure; keep power backfilled |
| A4 | balance cannot reach existing saves | add the dev-only reforge command |
| A5 | seed scumming | accepted consequence, recorded |
| A6 | pipeline-layer duplication | one parameterised pipeline shape |
| A7 | identical trash | accepted; reversible for free via Q1 |

**Nothing here invalidates the design.** A1 and A2 are holes in parts added during this session, both
closable with mechanisms already in the schema, and both far cheaper to close now than after content
exists — which is the same timing argument §8 makes about every amendment it lists.

---

## 10. What this document deliberately does not do

- **It does not spec anything.** No schema, no module ids, no build order.
- **It does not design the species effect *content*** — which families a species draws from is the
  seedsmith authoring question, and it needs the roster anchors first.
- **It does not touch the passive skill graph or hybrid element typing** — both named by the owner as
  unbuilt programs this eventually meets.
- **It does not re-open the atom vocabulary.** `definitions.md` wins, and §4.2 found the pool mechanism
  already matches the genre.

---

## 11. Related

- [effect-atom-map.md](effect-atom-map.md) — line 213 names this program's reason to exist
- [effect-atom/definitions.md](effect-atom/definitions.md) — the winner over any spec here
- [effect-atom/spec-container-schema.md](effect-atom/spec-container-schema.md) — fixed core + weighted pool
- [effect-atom/atom-family-library.md](effect-atom/atom-family-library.md) — 28 families → ~980 rows
- [item/ssot-rarity.md](item/ssot-rarity.md) §3.3 — the ten rungs and their pool-roll bands
- [item/seed-contract.md](item/seed-contract.md) — the seed law
- [demon-seed-ideal.md](demon-seed-ideal.md) §7 — the species-passive container, already decomposed
- [../research/arpg-effects/](../research/arpg-effects/) — stacking, procs, ailments, crit
- [../research/ai-native-generation/README.md](../research/ai-native-generation/README.md) — the contract rules
