# Lane I4 SSOT — sockets, inserts, and socket-combination bonuses

**Status:** Lane I4 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Terminology per the contract §1: a **socket** is a hole *in an item*; an **insert** is what goes in it.
An **equip slot** is lane I2's word and never appears here for either concept.

---

> ⚠ **AMENDED 2026-09-03 by owner rulings D20–D25** — [../item-ideal.md](../item-ideal.md) §2b.
> **Where this lane and those rulings disagree, the rulings win.** Six statements below are superseded:
>
> | § | Superseded | Now |
> |---|---|---|
> | §4.4 | "words", ≤ 20, hand-authored | **Strains (36) and Splices (66)** — **102, generated** on the twelve-aptitude grid, configured in seedsmith (**D20**) |
> | §4.4 | catalog of ~45 is learnable | 127 is past that bar. **The compendium reveal and the socket-UI preview become requirements, not niceties** (§8.2) |
> | §4.2 | affinity is *always* a soft +1 | **Soft for resonance, HARD for Strains/Splices** — every ingredient must sit in a matching socket (**D22**) |
> | §4.1, §8.7 | *"low rarities grant zero sockets"* | Still true **at drop**. **Crafting extends sockets at any rarity, and rarity sets the price** (**D23**) — this resolves a blocking contradiction: under D21 a Strain was otherwise unbuildable |
> | §4.2 | affinity is base-type-declared and unchanging | A **crafted** socket's affinity is **chosen by the crafter**, via a new priced `socket.imbue` operation (**D24**). `attune` was already taken by §4.2's own term |
> | §10.3 | *"does the commander get more sockets?"* | **Closed by D14** — the commander is another unique demon |
>
> **New, with no prior text here:** a Strain or Splice requires a **low-rarity, non-set** base
> (**D21**), which is also D2's verified rule and which closes §8.6's double-dipping structurally.
> **PoE-style links are out of scope** and belong to the action layer (**D25**).
>
> ✅ **The lane's four *"recalled, not verified"* D2/PoE claims are now verified** — all confirmed; see
> §2b's D20–D25 closing table. §10.5's roster-scale question is closed by **D1**.

## 2. Scope

### This lane owns

- **Sockets** — where a socket count comes from, what caps it, and how a socket is added.
- **Inserts** — what an insert is as data, its tiers, its rarity read, and how it is stored before and
  after socketing.
- **Socket-combination bonuses (OD5)** — bonuses granted by *combinations of inserts within one item*.
  This is the centrepiece of the lane, not a rider on it.
- **Socketing, unsocketing, and their consequences** — including what each does to the insert and to
  the item, expressed as operations against I6's mutation model.
- The socket-layer **validation table and its reason codes**.

### This lane does NOT own

| Not mine | Whose |
|---|---|
| Bonuses across several **equipped items** | **I5** (sets) |
| Bonuses from **unequipped inventory** | **I10** (charms) |
| The **instance-mutation model** — I adopt it and state my requirements (§9.1) | **I6** |
| **Material costs** and the cost vocabulary I spend in | **I9** |
| The **rarity ladder and its ordinals** — I propose socket counts by rarity and register them | **I1** |
| **Base types** and their columns — I propose `socket_max` per role; I3 owns the column | **I3** |
| **Rolled affixes** on the host item | **I8** |
| **Bags and stacking** — including the insert stack table I require | **I13** |
| **Equip gating** | **I11** |
| Turning a loot event into an instance | **I12** |

---

## 3. The model

An item may have **sockets**. Each socket is either empty or holds one **insert**. An insert is a
container of atoms with `container_kind = 'gem'` (contract SC3), a fixed definition with no rolled
values — which is why it stacks in the bag like a material instead of occupying a row per copy.

Socketing an insert does **not** touch the host item's frozen instance. The item's `effect_instance`
and its `effect_instance_atom` rows are written once, at drop, and never again. Instead a socketed
insert is instantiated as *its own* instance and bound to the same owner as the host, with a lifetime
tied to the host's binding: equip the item and the item's atoms, every socketed insert's atoms, and
every satisfied combination's atoms all bind together; unequip and all of them withdraw together. The
socket layer composes at the **binding** level, which is where the atom program already models "how an
atom arrived and how to withdraw it" ([definitions.md](../effect-atom/definitions.md) §0).

On top of the inserts sits the combination layer, in two tiers:

- **Resonance** is *rule-generated*. It reads the multiset of inserts in one item and fires when a shape
  matches — three of one element, two adjacent elements on the ring, light beside dark, three distinct
  elements. The player never needs a wiki for it, because the shapes are generated from the element
  roster and the socket UI can state them exhaustively.
- **Words** are *hand-authored*, exact, and ordered — a named recipe naming specific insert families in
  specific consecutive sockets, in the Diablo 2 runeword tradition. There are few of them, they are the
  chase, and each is revealed in-game once the player has held every ingredient at least once, so the
  recipe list is content the game hands you rather than a page you open in a browser.

A combination bonus is itself a container of atoms (SC1). It is not stored on the item and never merges
into the item's instance: it is instantiated on demand from a deterministic evaluation of the socket
contents and bound alongside the host. Nothing about the combination is persisted except the socket
contents that produce it, so the whole layer is a pure function of state that already exists.

That is the entire model. Socketing never rolls, never mutates a frozen row, and never consumes RNG —
which makes it the cheapest possible client of I6's mutation model, and makes SC5's reproducibility
contract survive it without an argument.

---

## 4. Options considered, and the recommendation

### 4.1 Where the socket count comes from

| Option | Tradeoff |
|---|---|
| **A — rolled at drop, independent axis** | Diablo 2's model. Creates the failure this lane most has to avoid: socket count becomes the only stat that matters and the loot chase collapses into "find one with four" (recalled from D2 runeword bases — a 4-socket Monarch, a 5-socket Colossus Voulge; **not verified**) |
| **B — fixed by base type** | Predictable, zero chase, and every copy of a base type is identical on this axis. Cheap, dull |
| **C — added by crafting only** | Full player agency and a good material sink, but every drop arrives sterile and the socket layer never surprises anyone |
| **D — granted by rarity** | Simple, ties into I1, but a strict ladder: higher rarity always means more sockets, so it *is* an independent axis wearing a costume |

**Recommendation: all four, layered, with each doing one job.**

```text
socketsAtDrop = min( base_type.socket_max ,
                     roll(rarity.socket_min .. rarity.socket_max, socketSeed) )

socketsNow    = socketsAtDrop + (recorded socket.add operations)
                capped at base_type.socket_max
```

- **Base type declares `socket_max` (0–4)**, and it is a *role* property, so a ring never holds four and
  a breastplate does. This is what stops socket count being one number compared across the whole loot
  pool — a 1-socket ring and a 4-socket cuirass are not in the same conversation.
- **Rarity grants a range, not a number** (D), rolled from `roll_seed` at drop. The range **overlaps
  between adjacent rungs**, which is OD4's overlap principle applied to this axis: a high roll on a
  mid-band rarity beats a low roll on the band above it.
- **Crafting tops the count up** (C) to `base_type.socket_max`. A bad socket roll is therefore a cost,
  not a discard — which removes most of the pressure that makes option A toxic.

**Maximum is 4.** Four and not six, for four reasons:

1. **Combination legibility.** With six concrete elements the resonance ladder reads cleanly at
   k ∈ {2, 3, 4}. A fifth and sixth step add rows and add nothing a player can hold in their head.
2. **Roster scale.** [item-ideal.md](../item-ideal.md) §8 already flags that twenty demons times twelve
   equip slots is 240 items before anything sits in a bag. At a maximum of 4 that is up to 540 sockets
   across a roster; at 6 it is 810.
3. **A word is at most four ingredients**, which is memorable. Six-ingredient recipes are exactly the
   thing nobody remembers.
4. Prior art in the other direction is a warning, not a model: D2 went to 6 and the base hunt became a
   socket-count hunt; PoE went to 6 links and made socket *colour* a currency treadmill (both recalled,
   **neither verified**).

**Registered with I1** (contract cut #3 — I propose, I1 registers). I1 owns the rungs and their names,
so this is stated by ordinal band rather than by rung name:

| Rarity band (position in I1's ladder) | `socket_min` | `socket_max` |
|---|---|---|
| bottom ~20% | 0 | 0 |
| ~20–40% | 0 | 1 |
| ~40–60% | 1 | 2 |
| ~60–80% | 1 | 3 |
| top ~20% | 2 | 4 |

Adjacent bands overlap by design: a `[1..3]` item rolling 3 out-sockets a `[2..4]` item rolling 2.

**Proposed `socket_max` per role** (I3 owns the column; roles are I2's twelve from item-ideal §5.1):

| Role | `socket_max` | Why |
|---|---|---|
| core-protective (`torso` / `stem`) | 4 | already the largest budget on the body |
| armament-primary (`main-hand` / `muzzle`) | 4 | identity-defining, so it should be where words live |
| head-protective (`head` / `crown`) | 3 | |
| mantle-utility (`back` / `canopy`) | 3 | |
| armament-secondary (`off-hand` / `thorn`) | 3 | |
| manipulator-offense (`hands` / `leaves`) | 2 | |
| girdle-resource (`waist` / `soil`) | 2 | |
| footing (`feet` / `roots`) | 2 | |
| sense-utility (`face` / `bract`) | 1 | |
| jewel-major (`neck` / `pollen`) | 1 | the amulet should earn its place with affixes, not with sockets |
| jewel-minor A / B (`ring-1/2` / `graft-1/2`) | 1 | item-ideal §5.5: keep the pair's budget small |
| commander `standard` (item-ideal §5.6, if it ships) | 2 | |

### 4.2 Typed sockets or universal sockets

| Option | Tradeoff |
|---|---|
| **A — hard typing** (a fire socket takes only fire inserts) | Creates matching puzzles and makes the *item* matter, but bricks items: a triple-fire cuirass is worthless to an ice build, and every drop needs a second lottery to be usable |
| **B — universal** | No bricking, but "always insert the best" — the anti-tax failure this lane is explicitly asked about |

**Recommendation: universal in acceptance, typed in yield.**

Every socket accepts every insert. No socket ever rejects an insert for element. But each socket carries
an **affinity** — one concrete element, or none — declared by the base type and unchanging. Affinity
does not scale the insert (scaling a frozen instance's magnitude is the mutation trap, §5.3); it feeds
the **combination** layer:

> When *every* insert contributing to a resonance sits in a socket whose affinity matches that insert's
> element, the resonance's effective count is **+1**.

So an item with three earth-affinity sockets is genuinely *the earth item*: two earth inserts in it
reach the resonance step that would otherwise need three. The player is blocked from nothing, but "which
insert is best" becomes a question about *this item* rather than a global answer. That is the single most
load-bearing anti-tax mechanism in the design, and it costs one nullable column.

`omni` is not an affinity — [element-hub-ssot.md](../element-hub-ssot.md) §4 is explicit that `omni` is
not an actor type slot, and treating it as a socket affinity would import that confusion here.

### 4.3 What an insert is

| Option | Tradeoff |
|---|---|
| **A — inserts are rolled instances**, like items | Richer per-gem variation, and *fatal to the inventory*: a rolled thing cannot stack (item-ideal §7 draws exactly this line), so a player ends the campaign holding nine hundred near-identical gem rows |
| **B — inserts are fixed containers**, tiered | Every `gem.ember-shard.t3` is identical everywhere, so they stack. Loses per-gem variety |

**Recommendation: B — fixed containers with `pool_rolls = 0`, five tiers.** The variety A would buy is
bought instead by the combination layer, which is a better place for it: a combination is variety that
comes from *player choice* rather than from a second lottery. And B is what makes the endless-inventory
failure (§8.4) solvable at all.

Consequences, all good:

- An insert in the bag is a **quantity**, not a row per copy — the same shape as `rpg_demon_materials`
  (`src/FusionRpg.Data/Sqlite/RpgStore.cs:520`). I do not propose that table; I13 owns it (§9.9).
- An insert has **no `roll_seed` that matters**. Instantiating `gem.ember-shard.t3` is deterministic by
  construction, so SC5's reproduction contract holds trivially: there is nothing to reproduce.
- **Upcycling is stack arithmetic**: 3 × tier *k* → 1 × tier *k+1*. No instances, no mutation model, and
  it is the primary drain on the gem inventory (§8.4). The 3:1 ratio is illustrative; I9 owns whether it
  also costs a material.

Inserts **do** read I1's rarity ladder — a `gem` container has a `rarity` column already
(`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:23`) and it is what a drop table weights against. But
rarity on an insert selects no `pool_rolls` (always 0) and no tier window (the tier is in the id).
**Rarity on an insert is a drop weight and a display colour, nothing more** — saying that plainly is
better than letting a column imply a mechanism it does not have.

### 4.4 The combination grammar

| Option | Tradeoff |
|---|---|
| **A — named exact recipes only** (runeword style) | Iconic, memorable, a huge chase — and the canonical wiki-dependency failure. D2's runeword list lived outside the game (recalled, **not verified**) |
| **B — colour/element counts only** | Zero wiki, fully inferable, and completely flat: no chase and no identity |
| **C — thresholds on tags** ("2+ defensive inserts") | Legible, but tags are invisible unless the UI teaches them, and it produces bonuses with no fiction |
| **D — ordered patterns**, position-sensitive | Maximum expressiveness, maximum obscurity. Nobody infers an ordered pattern |

**Recommendation: B as the floor and A as the ceiling, with nothing in between.**

- **Resonance (B)** is the baseline every socketed item participates in. It is *generated* from the
  element roster the way the atom library generates its element families
  ([atom-family-library.md](../effect-atom/atom-family-library.md) §2), so the whole set is enumerable
  in the UI and inferable from two examples.
- **Words (A)** are the ceiling: ≤ 20 hand-authored, ordered, exact. The wiki problem is solved by
  **revealing a word in the compendium once the player has held every ingredient at least once**, and by
  the socket UI previewing which combinations the current fill produces and which are one insert away.
  The recipe stops being secret knowledge and becomes a goal the game states.
- **C and D are rejected.** C because a tag threshold is a bonus with no story; D because ordered
  patterns are unguessable — a word is already ordered, and a word at least has a name to remember.

The four resonance shapes, all generated:

| Shape | Fires when | Rows generated |
|---|---|---|
| **Pure** | k inserts share one concrete element, k ∈ {2, 3, 4} | 6 elements × 3 = **18** |
| **Ring** | ≥ 1 insert of each of two elements adjacent on the ring (`fire → ice → earth → air → fire`, element-hub §8.5) | **4** |
| **Eclipse** | ≥ 1 `light` and ≥ 1 `dark` — the mutual counter (element-hub §4) | **1** |
| **Diversity** | 3 or 4 *distinct* elements present | **2** |

Twenty-five generated containers plus ≤ 20 words is a combination catalog of ~45. That is a size a
player can learn. Four hundred would not be.

**`omni` inserts count toward Diversity only** — never toward Pure, Ring, or Eclipse. They are the
deliberate *no-combo* option: raw additive power (element-hub §7: omni is the additive baseline) for a
player who does not want to chase a shape. Making the tax-payer's choice explicit and competitive is
better than pretending everyone wants a puzzle.

### 4.5 Where the combination bonus lives, and when it binds

A combination bonus is a container of atoms (SC1, OD6). Whose container, and bound to whom:

- **Whose:** its own, `container_kind = 'gem'`, id-namespaced `gem.combo-*` (resonance) and `gem.word-*`
  (words). Not the item's container, not any insert's. Adding a word is then a container row + its atom
  rows + a recipe row + its ingredient rows — **no new code**, which is what SC7 demands of anything
  called content.
- **Bound to whom:** the same owner as the host item's binding, with the same `priority`.
- **When it binds:** when the host item's binding is created, after the socket contents are read and the
  combination evaluated. **When it unbinds:** with the host, or immediately on any `socket.insert` /
  `socket.remove` that changes the evaluation while the host is equipped. Re-evaluation is a pure
  function, so this is a withdraw-and-rebind, never a patch.

**Reusing `gem` for combination containers is a deliberate reading of the reservation, and it is worth
saying out loud.** Contract SC3 reserves `gem` "for a socket insert". I am using it for the socket
*layer* — inserts and their combinations both. The alternative is a fifth reserved kind, and
`ContainerKind` is a **C# enum** (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:7`), not a table, so
each value costs a code change plus a `PrefixOf` arm plus a regex arm
(`src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs:17`). One code change for a layer beats two for
a distinction the `container_id` prefix already draws. The id grammar is one segment after the prefix —
`gem.combo-pure-fire-3` is legal, `gem.combo.pure-fire-3` is not.

### 4.6 Can one item satisfy two combinations at once? — Yes, with rules

**Yes across layers. No within the word layer.**

| Rule | |
|---|---|
| **At most one word per item** | A word is the item's identity; two identities is one too many. If several match, the lowest `container_id` ordinal wins — content-derived, so the choice is deterministic and independent of evaluation order |
| **Resonances do not consume inserts** | Every shape is evaluated over the full multiset, independently. A word and a resonance may be produced by the same inserts |
| **Within Pure, only the highest k per element fires** | Three fire inserts fire `pure-fire-3`, *not* `pure-fire-2` as well. Otherwise the ladder stacks with itself, which is §8.3 in miniature |
| **Ring, Eclipse, and Diversity stack with each other and with Pure** | Different shapes, different bonuses. A fire/ice/light/dark fill fires Eclipse + Diversity(4) and no Pure |
| **A word consumes its ingredients for word purposes only** | So it never blocks a resonance, and a second word cannot reuse the first word's inserts — moot under "at most one", stated so the rule survives if that cap is ever raised |

Evaluation is a pure, ordered function of `(socket contents, socket affinities, catalog_revision)`:
words first, then Pure (highest k per element), then Ring, Eclipse, Diversity. No RNG, no ambient state.

### 4.7 Removal

| Option | What it does to player behaviour |
|---|---|
| **Free** | Gems become a loadout you shuffle before every fight. Zero stakes, so the socket is never a decision and "the best gem" is always correct. The anti-tax argument fails outright |
| **Destroys the item** | Nobody socket-experiments. Sockets become endgame-only and are dead content for most of the campaign |
| **Destroys the insert** | The classic. Every insert is a commitment, and cheap inserts make experimentation cheap |
| **Costed, both survive** | Softest. Removal becomes a currency check — fine, but toothless at low stakes |

**Recommendation: tiered by insert tier. The item always survives.**

| Insert tier | Removal | Why |
|---|---|---|
| **t1–t2** | **Free** | The learning tier. Early play should never punish not-knowing, and the bag should never fill with "just in case" gems |
| **t3** | **Costed** (I9's terms — a material, not a currency); the insert survives | The transition: the decision gains weight without becoming irreversible |
| **t4–t5** | **Destroys the insert**; the item survives | The commitment tier. You may always clear a socket; you cannot always get the gem back |

You can always empty a socket. What varies is what you keep. That asymmetry matters: an item can never
be permanently ruined by a bad insert, which is what makes it safe to socket mid-campaign gear.

One hook, not a decision: an expensive consumable that downgrades t4–t5 removal from destructive to
costed is an obvious I9 sink and I10-adjacent. Named so it is not re-invented.

### 4.8 The mutation problem — confirm or refute the ideal's claim

[item-ideal.md](../item-ideal.md) §11 says sockets are *"the one deferred mechanic that probably needs
new schema"*, because *"an insert is an atom arriving after instantiation, which the current model does
not have a shape for."*

**Verdict: half right. The premise is wrong; the conclusion is accidentally right for a different
reason.**

**Refuted — no atom table changes shape, and no frozen row is ever mutated.** An insert is not "an atom
arriving after instantiation". It is *its own instance*, arriving as *its own binding*, on the same
owner. The atom program already has exactly that shape: definitions §0 says a binding is "how an atom
arrived and how to withdraw it", and an actor's effect list is fed by many bindings from many sources.
Adding a source to an actor is the mechanism's normal operating mode, not an extension of it. Concretely:

| Table | Written after drop by socketing? |
|---|---|
| `effect_instance` (host item) | **no** |
| `effect_instance_atom` (host item) | **no** |
| `effect_container` / `_atom` / `_pool` | **no** — content only |
| `effect_instance` (insert / combination) | yes, new rows for new things — not a mutation |
| `effect_binding` | yes, new rows — its normal use |

`InstanceRow.ContentFingerprint()` (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:47`) is computed
over `ContainerId` plus the instance's atom rows. Socketing touches neither, so
`same (container_id, catalog_revision, roll_seed) ⇒ byte-identical instance` survives untouched. **SC5
is not strained by this lane at all.**

**Confirmed, narrowly — new schema *is* needed, but it is a sidecar, not a change.** Three new tables
(§5.2), one new column on `effect_binding` (§5.4), two new columns on `rarity` (§9.4), one enum value.
None of them alters an existing atom table's shape or semantics.

**Why the distinction matters:** the ideal's framing invites a design where socketing appends rows to
`effect_instance_atom` and re-stamps the instance. That design breaks reproducibility permanently — the
fingerprint would no longer be derivable from `(container_id, catalog_revision, roll_seed)`, and SC5's
prescribed remedy (an origin plus an ordered op list) would have to reconstruct *frozen values* rather
than just *state*. Composing at the binding layer avoids the whole problem instead of paying it down.

**What I need from I6, precisely** — three RNG-free operation kinds in the mutation log, and one
declaration about which store is authoritative. Full statement in §9.1–9.2.

---

## 5. Data shape

### 5.1 Existing columns reused, unchanged

| Table | Used for | Change |
|---|---|---|
| `effect_container` | inserts and combination bonuses, `container_kind = 'gem'` | **none** — `rarity`, `tags_json`, `enabled`, `revision` used as-is; `pool_rolls` forced to 0 for `gem` |
| `effect_container_atom` | the atoms an insert or combination grants | **none** |
| `effect_container_pool` | — | **unused.** A `gem` container with any pool row is rejected |
| `effect_instance` / `effect_instance_atom` | a socketed insert's instance, and a live combination's instance | **none, and that is the point** — the host's rows are never rewritten |
| `effect_binding` | how insert and combination atoms reach the actor | one new nullable column (§5.4) |
| `rarity` | the socket grant range | two new columns, I1's to add (§9.4) |

### 5.2 New tables — the socket layer's own state

**`item_socket`** — the sockets on one item instance and what fills them. Consumer: the equip path
(reads it to build bindings) and the socket UI.

| Column | Type | Notes |
|---|---|---|
| `instance_id` | TEXT | FK → `effect_instance`, `ON DELETE CASCADE` |
| `socket_index` | INT | 0-based, `< socketsNow` |
| `affinity` | TEXT | one concrete element id, or `''` for none. Copied from the base type at drop |
| `insert_container_id` | TEXT | nullable — the `gem.*` container filling it; NULL = empty |
| `insert_instance_id` | TEXT | nullable — FK → `effect_instance`, the insert's own instance |
| | | PK `(instance_id, socket_index)` |

This table is a **materialized view of I6's operation log, not the SSOT** (§9.2). It exists because
replaying the whole log on every equip is silly, not because it is authoritative.

**`socket_combo_recipe`** — one row per combination. Consumer: the combination evaluator.

| Column | Notes |
|---|---|
| `combo_id` | PK, and the `container_id` of the bonus (`gem.combo-*` / `gem.word-*`) |
| `shape` | `pure` \| `ring` \| `eclipse` \| `diversity` \| `word` |
| `element` | for `pure`, the element; `''` otherwise |
| `threshold` | for `pure` the k; for `diversity` the distinct count; `0` for `word` |
| `host_role` | nullable — a word may require a role (`armament-primary`) |
| `host_frame` | nullable — `humanoid` \| `plant` \| `''` (I2's vocabulary, read not owned) |
| `min_sockets` | the host must have at least this many |
| `enabled`, `revision` | joins the content hash (definitions §8) |

**`socket_combo_ingredient`** — words only. Consumer: same.

| Column | Notes |
|---|---|
| `combo_id` | FK |
| `position` | 0-based, consecutive from socket 0 |
| `family_id` | the insert family required at that position — a family, not an exact `container_id`, so tiers are interchangeable |
| `min_tier` | the lowest insert tier that satisfies it |
| | PK `(combo_id, position)` |

Both recipe tables **register into the content-hash table set** (definitions §8: "a registry, not a
list"), so adding a word bumps `contentHashSchemaVersion`'s covered set the first time and `contentHash`
every time after — exactly as intended.

### 5.3 Socket count is derived, not stored

`socketsAtDrop` is a **pure function** of values already recorded on the instance:

```text
socketSeed    = SeededRng.Derive(instance.roll_seed, "socket")   // domain-separated: the socket draw
                                                                 // never consumes the affix pool's stream
grant         = socket_min + socketSeed % (socket_max - socket_min + 1)
                // rarity row read at instance.catalog_revision
socketsAtDrop = min(grant, base_type.socket_max)
```

Nothing is stored, so nothing can drift, and reproducing the drop reproduces the socket count.

Two hard requirements fall out:

- The RNG must be `SeededRng` (`src/FusionRpg.Core/Battle/SeededRng.cs`) and never `System.Random` —
  definitions §13 D5 is explicit that a `System.Random` seeded sequence is not stable across .NET
  versions and would move goldens with no content change and no `contentHash` change.
- The `rarity` socket columns must be **append-only in the same sense `ordinal` is** (§9.4). A silent
  edit to `socket_max` on an existing rung re-sockets every item ever dropped at that rung.

The same reasoning is why affinity does not *scale* an insert's magnitude (§4.2). A scaled magnitude
would have to be frozen somewhere, and the only honest place is the insert's instance — at which point
`gem.ember-shard.t3` is no longer the same thing everywhere, inserts stop stacking, and §4.3 collapses.

### 5.4 One column requested on `effect_binding`

Two identical inserts in two sockets of one item produce two bindings whose atoms sort **identically**
under the one execution order the program guarantees — `(priority DESC, container_id ASC, seq ASC)`,
compared ordinal (definitions §5). Same container, same seq, same priority.

That is a tie in a sort definitions §5 requires to be **total**, and it is load-bearing: §5 states that
list order is what makes multi-atom `OnApply` draws reproducible, because two atoms rolling on one hit
consume the RNG stream in list order.

The fix must be **content-derived** — §5 rejects `binding_id` as a tiebreak precisely because it is
generated. `socket_index` is content: recorded, stable, and chosen by the player rather than the runtime.

> **Request to I6 / E6:** add `bind_ordinal INTEGER NOT NULL DEFAULT 0` to `effect_binding` and append it
> to the effect-list comparer as the final tiebreak. Socket-layer bindings set it to `socket_index + 1`;
> everything else leaves it 0 and sorts exactly as it does today.

This is a spec-level defect in the atom layer that this lane happens to be the first to hit. The comparer
is **not implemented anywhere yet** — no consumer exists — so fixing it now costs a column and a
sentence, and fixing it after E12 costs a behaviour change.

### 5.5 What is code and what is data (SC7)

Honest accounting, because SC7 asks for it:

| Change | Code or data | Where |
|---|---|---|
| `ContainerKind.Gem` + its `PrefixOf` arm + the id regex arm | **code** | `ContainerRow.cs:7`, `ContainerValidator.cs:17` |
| Three reason codes | **code**, and it moves a guard test | `AtomRejection.cs`; `tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:33` asserts the enum length is exactly 34 |
| `bind_ordinal` column + comparer | **code** | I6's |
| `socket_min` / `socket_max` on `rarity` | **code** (DDL) once, **data** thereafter | I1's |
| The three socket-layer tables | **code** (DDL) once | this lane |
| The 25 resonance containers | **generated data** | one generator, run at import |
| Each new word | **data** — 1 container row + N atom rows + 1 recipe row + M ingredient rows | authoring |
| Each new insert | **data** — 1 container row + its atom rows | authoring |

The last three are the test SC7 sets, and the layer passes it: after the one-time enum, column, and DDL
work, every content addition is rows.

---

## 6. Validation and reason codes

### 6.1 Reused codes — six, no change required

| Bad input | Code | Note |
|---|---|---|
| A `gem` container declares `pool_rolls > 0`, or carries a pool row | `BadParamValue` | inserts are fixed by definition (§4.3) |
| `socket_index` outside `[0, socketsNow)` | `BadParamValue` | out-of-range param — the exact case the code documents |
| The same **unique-tagged** insert is already in this item | `DuplicateKey` | a uniqueness violation; the existing code's meaning covers it |
| A socketed insert references a disabled atom | `StaleInstance` | inherited free from the E6 bind gate |
| An insert whose `level_req` exceeds the wearer's level | `LevelTooLow` | inherited free — an insert binds through the same gate as its host |
| A recipe names an ingredient family that does not resolve | `UnknownContainer` | import-time, all-or-nothing (definitions §10) |

Two inherited rejections that **will bite in practice** and should not be discovered at runtime:

- A `warding` or `resilience` insert — a "+armour gem", the most obvious gem in the genre — bound at any
  scope other than `match` is `ScopeUnsupported`. G8 is not negotiable (definitions §6;
  `spec-instance-and-binding.md`). **A +defense gem cannot ship.**
- Every `stat.derived` insert — which is *most* element gems — is quarantined `None/None/None` (D6) and
  rejects `RuntimeUnsupported` at bind until E12 lands `BattleStatComposer`. See §9.13.

### 6.2 New codes — three, and why each earns its place

Adding a reason code is expensive: definitions §10 fixes the list at 33 and
`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:33` asserts the enum length is exactly 34.
Three is the minimum that does not collapse two different operator fixes into one message.

| Code | Raised when | Why not an existing code |
|---|---|---|
| `NotSocketable` | the thing being inserted is not an insert (`container_kind ≠ 'gem'`, or a `gem.combo-*` / `gem.word-*` row), **or** the host's `base_type.socket_max` is 0 | Both are socketability refusals with the same fix — pick a different thing, or a different item. No existing code says "wrong kind of container for this operation" |
| `NoFreeSocket` | every socket is full when auto-picking, or a `socket.add` would exceed `base_type.socket_max` | The fix is *make room* — remove an insert, or accept the cap. Distinct from an out-of-range index |
| `SocketOccupied` | an explicit `socket_index` is already filled | The fix is *remove first*. Folding this into `NoFreeSocket` would tell a player to add a socket when they need to empty one |

### 6.3 The full table

| Bad input | Code |
|---|---|
| Insert an `item`, `trait`, or any non-`gem` container ("socketing a socketed item", reading 1) | `NotSocketable` |
| Insert a combination container (`gem.combo-*`, `gem.word-*`) | `NotSocketable` |
| Insert into an item whose base type has `socket_max = 0` | `NotSocketable` |
| Insert into an item with sockets, all full, no index given (over-filling) | `NoFreeSocket` |
| `socket.add` on an item already at `base_type.socket_max` ("socketing a socketed item", reading 2) | `NoFreeSocket` |
| Insert at an index already filled | `SocketOccupied` |
| Insert at an index `< 0` or `≥ socketsNow` (over-filling by index) | `BadParamValue` |
| Insert a second copy of a unique-tagged insert into the same item | `DuplicateKey` |
| Remove from an empty socket | `NoFreeSocket` — nothing there to take |
| A `gem` container authored with `pool_rolls > 0` or a pool row | `BadParamValue` |
| A word recipe whose `min_sockets` exceeds 4 | `BadParamValue` |
| A word recipe with non-consecutive `position` values | `BadParamValue` |
| A recipe naming an unresolvable ingredient family | `UnknownContainer` |
| A socketed insert whose atom was disabled since socketing | `StaleInstance` at bind |
| An insert atom whose kind has no consumer in the target runtime | `RuntimeUnsupported` |
| A `warding` / `resilience` insert at a non-`match` scope | `ScopeUnsupported` (G8) |

**"Wrong type" is deliberately absent.** Under §4.2 a socket never rejects an insert for element, so
there is no wrong-type rejection to name. That is the design choice, stated where its absence would
otherwise look like an oversight.

### 6.4 Two authoring rules that are not rejections

- **A resonance container may not repeat a family its triggering inserts carry.** Enforced *in the
  generator*, so it cannot be violated — no code, no rejection needed.
- **A word container should not repeat a family its ingredients carry.** A **lint**, reported at import
  and not rejected, because a word deliberately amplifying one of its ingredients is a legitimate design
  and a hard rule would forbid it. Reporting the honest thing beats rejecting the useful thing.

---

## 7. Worked examples

**Every number below is illustrative, not balanced.** Units are stated on each line per SC4.

### 7.1 Resonance on a three-socket plant chest

**Host:** `item.bark-plating`, frame `plant`, role core-protective (`stem`), `socket_max = 4`. Dropped at
a mid-band rarity (`socket_min = 1`, `socket_max = 2`) and rolled **2**; the player later spends to add a
third (§7.3). Socket affinities from the base type: `[earth, earth, fire]`.

**Fill:**

| Socket | Affinity | Insert | Atom | Value |
|---|---|---|---|---|
| 0 | earth | `gem.stone-heart.t3` | `atom.vitality.t3` — `stat.modify`, `maxHp`, `Flat` | **+45 hp** (game units) |
| 1 | earth | `gem.stone-heart.t3` | same | **+45 hp** |
| 2 | fire | `gem.ember-shard.t3` | `atom.elemental-power.fire.t3` — `stat.derived`, `combat.power.fire` | **+30 resolver points** ⚠️ |

⚠️ That third row **binds nowhere today** — `stat.derived` is quarantined `None/None/None` (D6). It is in
the example precisely because a socket design that pretends otherwise is lying.

**Combination evaluation:**

- **Pure earth:** 2 earth inserts, both in earth-affinity sockets, so *every contributor is attuned* →
  effective count **3**. `gem.combo-pure-earth-3` fires.
- **Pure fire:** 1 insert, below the k = 2 floor. Nothing.
- **Ring:** needs two adjacent elements; earth and fire are not adjacent
  (`fire → ice → earth → air → fire`). Nothing.
- **Diversity:** 2 distinct elements, below the floor of 3. Nothing.

**`gem.combo-pure-earth-3`** contains two atoms — deliberately *not* more flat `maxHp`:

| Atom | Kind | Value |
|---|---|---|
| `atom.fortitude.t2` | `stat.modify`, `maxHp`, `Increased` | **+80‰** (integer per-mille = +8%) |
| `atom.regeneration.t2` | `resource.delta`, `OnTimer` | **+6 hp per 5 000 ms** |

**Socket-layer total:** +90 hp flat, +8% maxHp, +6 hp/5 s, and +30 fire power that goes nowhere until
E12. The combination is a *different shape* from what the inserts grant — a multiplier and a trigger,
against two flat adds. That is §8.3's rule made concrete.

### 7.2 A word, and three combinations at once

**Host:** `item.pea-nozzle`, frame `plant`, role armament-primary (`muzzle`), `socket_max = 4`, dropped at
a top-band rarity and rolled **3**. Affinities `[fire, ice, '']` — the third socket has none.

**Word `gem.word-frostfire`** — recipe:

| Field | Value |
|---|---|
| `shape` | `word` |
| `host_role` | `armament-primary` |
| `min_sockets` | 3 |
| ingredients | pos 0: `atom.searing-strike` family insert, `min_tier 3` · pos 1: `atom.rime-tear` family insert, `min_tier 3` · pos 2: `atom.searing-strike` family insert, `min_tier 3` |

**Fill:** socket 0 `gem.ember-shard.t3` (fire), socket 1 `gem.rime-tear.t3` (ice), socket 2
`gem.ember-shard.t4` (fire).

**Word atoms:**

| Atom | Kind | Value |
|---|---|---|
| `atom.searing-strike.fire.t4` | `resource.delta`, `OnDamageDealt` | **200–300 hp** fire damage, `chance: 350‰`, `icd_ms: 2000` |
| `atom.cruelty.ice.t3` | `stat.derived`, `combat.crit.damage.ice` | **+45 resolver points** ⚠️ quarantined (D6) |

**Also fires:**

- **Pure fire:** 2 fire inserts. Socket 0 is fire-affinity (attuned) but socket 2 has no affinity, so
  *not every contributor is attuned* — no +1. Effective count **2** → `gem.combo-pure-fire-2` fires.
- **Ring:** fire and ice are adjacent on the ring and both are present → `gem.combo-ring-fire-ice` fires.

So this item carries **three combinations simultaneously**: one word, one Pure, one Ring. That is the
intended ceiling for a top-rarity three-socket weapon, and it is exactly why `min_sockets` and the
once-per-shape caps exist — without them a four-socket item would stack six.

**Why the word is not merely additive:** neither `gem.ember-shard` nor `gem.rime-tear` grants a proc or a
crit-damage rider. The word's contribution is a *mechanism* the ingredients do not have, not a bigger
number of one they do.

### 7.3 The mutation trail — what actually gets written

Item instance `a3f1c7…`, container `item.bark-plating`, `roll_seed = 8812349`, `catalog_revision = 412`,
`origin = drop`. Two sockets at drop.

| `op_seq` | `op_kind` | args | cost (I9's vocabulary — illustrative) |
|---|---|---|---|
| 1 | `socket.insert` | `{index: 0, gem: 'gem.stone-heart.t3'}` | — |
| 2 | `socket.insert` | `{index: 1, gem: 'gem.stone-heart.t3'}` | — |
| 3 | `socket.add` | `{}` | 1 × `shard.epic` |
| 4 | `socket.insert` | `{index: 2, gem: 'gem.ember-shard.t3'}` | — |
| 5 | `socket.remove` | `{index: 2}` | 2 × `essence.fire` — t3, so **costed**; the insert returns to the stack |
| 6 | `socket.insert` | `{index: 2, gem: 'gem.stone-heart.t5'}` | — |

`essence.{element}` and `shard.{rarity}` are the material ids expeditions already seed (item-ideal §3) —
this lane invents no cost vocabulary; I9 owns it.

**What was written to the item's atom tables: nothing.** Its `effect_instance_atom` rows are exactly what
they were at drop.

```text
reproduce(item.bark-plating, catalog_revision 412, roll_seed 8812349)
  ⇒ byte-identical effect_instance_atom rows, before and after all six operations
```

That holds because `InstanceRow.ContentFingerprint()`
(`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:47`) is computed over `ContainerId` plus the atom
rows, and none of the six operations touched either. The item's current *state* is
`origin instance + ops 1–6 replayed in order`; its *rolls* never moved.

**What was written elsewhere:** six rows in I6's operation log; three rows in `item_socket`; three
`effect_instance` rows for the socketed inserts (one per insert instance, each deterministic from its
`gem` container with no rolls to freeze); and stack decrements and increments in I13's insert inventory.

### 7.4 What equipping produces

Equipping the §7.3 item — three earth inserts in three sockets whose affinities are `[earth, earth,
earth]` after the §7.3 sequence — creates, in one transaction:

| Binding | `container_id` | `bind_ordinal` |
|---|---|---|
| the item | `item.bark-plating` | 0 |
| insert in socket 0 | `gem.stone-heart` | 1 |
| insert in socket 1 | `gem.stone-heart` | 2 |
| insert in socket 2 | `gem.stone-heart` | 3 |
| the resonance | `gem.combo-pure-earth-4` | 0 |

Three earth inserts, all attuned → effective count **4** on a three-socket item. That is §4.2's affinity
payoff: the right item reaches a step the socket count alone could not.

Note the three insert bindings share a `container_id` and a `seq`. Without `bind_ordinal` (§5.4) they
sort identically and definitions §5's total order is not total. With it, the order is content-derived and
stable across runs.

Unequipping withdraws all five bindings. Removing the socket-2 insert while equipped withdraws all five
and rebinds four, because the resonance dropped from `pure-earth-4` to `pure-earth-3` — a
withdraw-and-rebind, never a patch.

---

## 8. Failure modes

### 8.1 Socket count becomes the only stat that matters

**How it shipped elsewhere:** D2's endgame item hunt was substantially a socket-count hunt — the base
mattered less than whether it rolled the socket count a runeword needed (recalled, **not verified**).

**What prevents it here:**

- `socket_max` is a **role** property, so socket count is not comparable across the loot pool.
- Socket count is **derived, not an independent rolled column** (§5.3) — no separate lottery to re-roll,
  no separate stat to filter on.
- Crafting **tops the count up to the base cap**, so a low roll is a cost rather than a discard.
- The cap is **4**, low enough that the top of the range is reachable rather than mythical.

**Residual risk, stated honestly:** if I3 ever varies `socket_max` *within* a role, a `socket_max = 4`
base type immediately outranks a `socket_max = 2` one in the same role, and this defence is gone. My
proposal fixes `socket_max` per role precisely so it cannot become a base-type lottery — see §9.6.

### 8.2 Recipes so obscure players need a wiki

**How it shipped elsewhere:** D2's runeword list was, in practice, an out-of-game resource.

**What prevents it here:**

- **25 of the ~45 combinations are generated by rule** from the element roster. Two examples teach the
  whole set.
- **Words are ≤ 20**, and each is **revealed in the compendium once the player has held every ingredient
  at least once**. The list is content the game gives you, not knowledge you import.
- **The socket UI previews** which combinations the current fill produces and which are one insert away.
  That is a design requirement of this lane, not a nicety: without it the resonance layer is invisible
  and reverts to being a stat tax.

**Residual risk:** the *ordering* requirement on words is the obscure part. A player holding all three
ingredients but arranging them wrongly gets nothing and may not know why. The preview must state the
required order explicitly, and the "one insert away" hint must include "one *swap* away".

### 8.3 Combinations that are strictly additive, and therefore not combinations

**The failure:** if `pure-fire-3` grants "+30 more fire power", it is not a combination; it is a volume
discount with a name. The player is not deciding, they are counting.

**What prevents it here:**

- **A resonance may not contain a family its triggering inserts carry** — enforced *in the generator*
  (§6.4), so it is structurally impossible rather than reviewed.
- Every resonance grants a **different shape** from a flat add: a percentage (`Increased`), a triggered
  effect (`OnTimer`, `OnDamageDealt`), a shield, a status. §7.1 is the worked instance.
- **Words grant mechanisms, not magnitudes** — a proc, a rider, a spawn — which is why they are
  hand-authored rather than generated.

**Residual risk, and I am committing it in my own example:** an `Increased` bonus on the same channel as
the inserts' `Flat` bonus is *technically* a different shape but *feels* additive. §7.1's `+45 hp × 2`
plus `+8% maxHp` is close to that line. The generator rule catches `Flat + Flat`; it does not catch
`Flat + Increased` on one channel. That is a judgement left to authoring review, named rather than
papered over.

### 8.4 The endless inventory of gems

**The failure:** two hundred hours in, the gem tab is nine hundred rows and sorting it is the game.

**What prevents it here:**

- **Inserts are unrolled, so they stack** (§4.3). One row per `(player, gem_container_id)`. The whole
  catalog is at most `6 elements × ~8 families × 5 tiers` distinct rows, and realistically far fewer.
- **3:1 upcycling** drains the low tiers continuously and gives junk a use.
- **Free removal at t1–t2** means nobody hoards "in case I need it back".
- **Gems are player-scoped, not actor-scoped** — one stack serves the whole roster, so roster size
  multiplies the *sockets to fill*, never the *inventory rows to manage*.

### 8.5 Socketing becomes an inventory chore at roster scale

**The failure this lane cannot fully solve.** Twenty demons × twelve equip slots × up to four sockets is
up to **540 sockets**. Even at a realistic fill rate that is hundreds of socketing decisions, most of
which are not interesting.

**What is done about it:** low rarities grant **zero** sockets, so most gear never poses the question;
`socket_max` is 1 or 2 on seven of the twelve roles; and an **auto-fill** action exists that fills an item
toward a named target combination in one click. Designing the affordance for the degenerate case is more
honest than pretending the case does not arise.

**What is not done about it:** the real answer is item-ideal §8's unanswered question — whether gear is
scarce, disposable, or restricted to a small deployable squad. **My sizing changes completely depending
on that answer**, and I have designed against the pessimistic reading. See §9.10 and §10.5.

### 8.6 Double-dipping between the socket layer and the set layer

**The failure:** if inserts counted toward set piece counts, a four-socket item would be most of a set by
itself, and socket count would become the set layer's only stat too — §8.1 imported wholesale into
someone else's lane.

**What prevents it:** the §9.7 position, which I5 must agree to.

### 8.7 The anti-tax rule — what makes a socket a real choice, and where it is honestly a tax

Five things make a socket a decision:

1. **The combination layer creates opportunity cost.** With combinations, the marginal value of an insert
   is not its own atoms but its contribution to the multiset. Putting the best fire gem into an item with
   two earth inserts costs you `pure-earth-3`. That is a decision by construction, not by exhortation.
2. **Affinity makes "best" item-relative** (§4.2). There is no global answer to "which gem is best",
   because the answer depends on the affinities of the item in front of you.
3. **Words are plans, not pickups.** A word requires exact families in exact positions, so socketing
   toward one is a multi-session goal rather than a click.
4. **Removal cost at t4–t5** gives the decision weight. A commitment you can undo for free is not one.
5. **The `omni` insert is the explicit opt-out.** A player who wants raw stats and no puzzle has a
   competitive option, so the combination layer is a choice rather than a mandatory minigame.

**And here is the honest part.** At tiers 1–3, with no combination in reach, **sockets are a stat tax.**
The player puts the biggest number in and moves on, and no amount of design language changes that. Three
things blunt it and none of them eliminates it:

- Low rarities grant **zero** sockets, so the tax simply does not exist for most early gear.
- Inserts **stack**, so the inventory cost of paying the tax is one row per gem type, not one per gem.
- **Auto-fill exists specifically for this case.** When socketing is a tax, the player should be able to
  pay it in one click.

Designing the one-click affordance for the degenerate case is a more honest response than claiming every
socket is a fascinating decision. Some of them are not, and the design should let those be fast.

---

## 9. What this lane needs from other lanes

1. **I6 — the mutation model must accept RNG-free recorded operations.** I need three op kinds —
   `socket.add`, `socket.insert`, `socket.remove` — in an append-only log keyed `(instance_id, op_seq)`
   with `op_seq` monotonic per instance, such that replaying ops in order over the origin instance
   reconstructs current state deterministically. **My operations consume no RNG at all**, which makes
   this lane the easiest possible client: if I6's model can only express *re-roll* (RNG-bearing)
   mutations, it is under-specified for the layer that needs it least.

2. **I6 — declare the operation log the SSOT and `item_socket` a materialized view.** If both are
   authoritative they will drift. §5.2 assumes the log wins; I need that confirmed rather than assumed.

3. **I6 / E6 — `bind_ordinal INTEGER NOT NULL DEFAULT 0` on `effect_binding`, appended to the
   effect-list comparer as the final tiebreak.** Reason in §5.4: two identical inserts in one item
   produce two bindings with identical `(priority, container_id, seq)`, and definitions §5 requires a
   total order because RNG-stream consumption order depends on it. `binding_id` is explicitly rejected
   as a tiebreak because it is generated; `socket_index` is content-derived. The comparer has no
   implementation yet, so this is cheap now and a behaviour change after E12.

4. **I1 — two columns on the `rarity` table: `socket_min`, `socket_max`.** The table is at
   `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:52` and already carries `pool_rolls`, `min_tier`,
   `max_tier`; these sit beside them. **They must be append-only in the same sense `ordinal` is** — my
   socket count derives from the rarity row at the instance's recorded `catalog_revision` (§5.3), so a
   silent edit re-sockets every item ever dropped at that rung.

5. **I1 — confirm rarity may grant a *range* on this axis**, and confirm the band mapping in §4.1. Ranges
   are how OD4's overlap principle reaches the socket axis; a single number per rung would make socket
   count a strict ladder and re-open §8.1.

6. **I3 — `socket_max` and per-socket `affinity` on the base type.** I propose the per-role caps in §4.1
   and ask that `socket_max` be **fixed per role, not varied per base type** — §8.1's residual risk is
   entirely about that choice. Affinity is a list of `socket_max` element ids (or `''`), declared once on
   the base type and copied into `item_socket` at drop.

7. **I5 — agree the socket/set boundary.** My position, which I5 must agree to or overrule:
   - An **insert is never a set piece.** I5 counts *equipped items*; an insert is not equipped, it is
     inside one.
   - **Atoms granted by inserts or by socket combinations never count toward a set's piece count or its
     thresholds.** If they did, one four-socket item could satisfy a set alone, and §8.1 lands in I5's
     lane at full strength.
   - **One seam offered, read-only in my direction:** a set tier may *reference* a socket condition as a
     **requirement** — "4-piece: while every piece holds at least one insert…". That gives I5 a lever
     over my layer without owning any of it. I5 decides whether to use it.

8. **I9 — cost vocabulary for four operations:** `socket.add`, costed removal at t3, destructive removal
   at t4–t5 (which may still cost something), and 3:1 upcycling. §7.3 spends `shard.{rarity}` and
   `essence.{element}` because those already exist (item-ideal §3); **I have invented no cost terms** and
   every amount is illustrative.

9. **I13 — the stackable insert inventory.** `(player_id, gem_container_id, qty)`, the same shape as
   `rpg_demon_materials` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:520`). I do not propose the table —
   contract cut #10 gives bags and stacking to I13 — but the whole of §4.3 and §8.4 depends on inserts
   being a quantity rather than a row per copy. **If I13 stores inserts as instances, this lane's
   inventory defence collapses.**

10. **I12 — two requirements on the drop pipeline.** (a) A dropped insert is a **stack increment**, not an
    instance; an insert becomes an instance only when it is socketed. (b) Socket count at drop is derived
    with a **domain-separated sub-seed** (§5.3) so it never consumes the affix pool's RNG stream, and
    adding sockets later cannot move an existing item's affixes.

11. **I11 — confirm inserts carry no independent wear requirement.** My assumption is that an insert
    inherits its host's equip gate entirely, so `level_req` on an insert is checked at bind like anything
    else and nothing extra is needed. If I11 wants inserts to carry their own requirements, I need the
    check at *socket* time as well as bind time, and a reason code for it.

12. **I8 — confirm a socket-granted atom does not share `group` space with the host's rolled affixes.**
    My position: it does not. `group` is scoped to a container
    (`effect_container_pool.group_key`, `RpgStore.Containers.cs:45`), and an insert is a different
    container, so a `vitality` gem in an item that already rolled `vitality` is legal and both apply. If
    I8 wants one-mod-per-family to span the item *and* its inserts, that is a much larger change and it
    needs saying now, not after authoring starts.

13. **The effect-atom program (E12) — most of my natural content binds nowhere today.** Element gems are
    `stat.derived`, quarantined `None/None/None` (D6), and a "+armour" gem is impossible at any per-actor
    scope (G8). Authoring them now produces exactly the `status.expose.*` failure SC7 names — *a row no
    code consumes is not content; it is a lie in a table*. **I need a decision on whether wave 1 inserts
    are restricted to `stat.modify`, `resource.delta`, `status.apply`, `shield.grant`, and the
    board/economy families** (which all work), with the element gem catalog held until E12 opens the
    battle cell. My lean: hold them, and ship wave 1 from the working kinds.

14. **SC9 — what I would want from the power model, and do not depend on.** Two things when E9 lands: a
    **socket-layer budget** bounding the total power a fully-socketed item may reach relative to its
    unsocketed self; and a rule that a **combination is priced against the composed actor**, not as a
    standalone container — a combination exists only in context, so its context-free price is meaningless
    by construction (definitions §7's `actorPower` change is the right shape for this). Neither is needed
    for anything above to ship.

---

## 10. Open questions for the owner

1. **Is the tiered removal rule (§4.7) too harsh at t4–t5?** Destroying a top-tier insert on removal is
   the genre standard, but this game has a *roster*: a player re-speccing one demon loses gems across
   twelve items, not one character's ten. A softer alternative is destructive removal only at t5.
2. **How many words ship in wave 1?** I propose 12. And: are words shared across frames, or does each of
   `humanoid` / `plant` get its own set? Shared is cheaper; per-frame is more flavourful and doubles the
   authoring.
3. **Does the commander get more sockets than a demon?** The `standard` role (item-ideal §5.6) binds at
   `match` scope, so a socketed combination on it would buff the whole squad. That is either the best
   thing in the design or a balance hole, and it depends on decisions §5.6 has not made.
4. **May a player socket during a run, or only out of combat on unequipped items?** My lean is
   **unequipped, out of combat only** — live socketing means withdraw-and-rebind mid-match, which touches
   the injector and interacts with SC8's standalone rule. But "swap a gem between waves" is a genuinely
   good PvZ-shaped mechanic and I did not want to close it unilaterally.
5. **The roster-scale gear economy** (item-ideal §8). Not my decision, and the single input that most
   changes my numbers: §8.5's 540 sockets is either a rich system or a chore depending entirely on it. I
   have designed for the pessimistic reading.
6. **Is attunement a +1 to the effective count (my pick, §4.2), or should attuned fills grant a separate,
   stronger combination row?** The +1 costs zero rows and caps the reward at one step. A separate row is
   more expressive and doubles the resonance catalog to 50.
7. **Reusing `container_kind = 'gem'` for combination containers as well as inserts** (§4.5) is a reading
   of SC3's reservation, not a literal application of it. If the owner wants a distinct kind, it is one
   more enum value and one more regex arm — say so and I will take it.
