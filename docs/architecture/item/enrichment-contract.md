# Item enrichment — the shared contract every lane obeys

**Status:** Authoring contract, written 2026-08-22 before the enrichment round. **Binding on every lane
document in this folder.** Where a lane doc and this file disagree, this file wins until it is amended.

Parent intent: [../item-ideal.md](../item-ideal.md). The lane docs enrich it; they do not replace it and
they must not edit it — reconciliation back into the ideal happens in one pass after all lanes land.

---

> ⚠ **SC9 is STALE (2026-09-03).** It reads *"power is open, and you may not depend on it"*. **E9
> `power-vector` shipped 2026-08-22** — `effect-atom-map.md:82`, `Core/Effects/Atoms/Power/`, with live
> consumers. **D13 is VOID** ([../item-ideal.md](../item-ideal.md) §2f.2). Three lanes inherit SC9 and are
> stale with it: `ssot-item-categories.md:803`, `ssot-granted-actions.md` §10 Q6, `ssot-presentation.md`
> §10 Q7. What *is* open is **E44 `power-sweep`** — all 20 coefficients flat at `CoeffMilli = 1000`.

## 0. Why this file exists

Eleven-plus lanes are being authored in parallel by separate sessions. Without a pre-agreed vocabulary
they will each invent one, and four collisions are already predictable:

1. **"slot"** means an equip position, a socket in an item, a world construction slot, and an expedition
   parallelism gate. Four things, one word.
2. **Rarity** is read by five lanes and owned by one.
3. **Sockets, sets, and charms** are all "combination bonuses" and will overlap unless the boundary is
   cut by *where the combination lives*.
4. **Enhancement and reroll** both mutate an item whose rolls the atom layer freezes by contract.

This file cuts those four, and eight more.

---

## 1. Terminology lock — use these words, only these words

| Term | Means | Never call it |
|---|---|---|
| **equip slot** | a position on a body where one item goes (`head`, `crown`, `main-hand`) | socket, mount, node |
| **role** | the frame-neutral name of an equip slot, shared across frames | slot type |
| **frame** | the body vocabulary: `humanoid` \| `plant` \| `hybrid` | side, race, faction |
| **faction / side** | plant vs zombie allegiance — a *different* axis from frame | frame |
| **socket** | a hole **in an item** that accepts an insert | slot, gem slot |
| **insert** | the thing that goes in a socket (gem, rune, seed, bead) | gem-only naming |
| **base type** | the item template's identity: what it is before affixes | base item, template |
| **implicit** | a fixed modifier every copy of a base type carries | innate affix |
| **affix** | a *rolled* modifier drawn from a pool | mod, stat |
| **atom / family / tier / variant** | as defined in [../effect-atom/definitions.md](../effect-atom/definitions.md) §1 | anything else |
| **container / instance / binding** | as defined in definitions §0 and §5 | item row, equip row |

**Never write a bare `D<n>` for a game.** In this folder `D1`–`D4` are the four **decision documents**
(`decision-d1-durable-ownership.md` … `decision-d4-content-budget.md`). Prior-art games are always
written in full — **Diablo 2**, **Diablo 4**, **Path of Exile** — never `D2` or `D4`. This rule exists
because the first draft of the fleet plan said *"D2 scale"* meaning Diablo 2, in a document that also
cited decision D4, and it was read as a claim about Diablo 4. Same for `E<n>` (effect-atom modules),
`I<n>` (item lanes) and `G<n>` (gap lanes) — those prefixes are reserved for modules, never for anything
else.

**Reserved and untouchable — these already mean something else in this tree:**

- `slot:{id}` **owner scope** = a world-map construction slot. Definitions §6: *"unrelated to an item's
  `slot` column. Two different concepts, one word — do not share a type."*
- **expedition slot** = a parallelism gate (2 → 5 via progression).
- `variant` on an atom = the discriminator within a family (element, channel). Not an item variant.
- `rarity` already exists as a **table with explicit append-only ordinals** (E5). It is not a free string.

---

## 2. The nine shared rules

### SC1 — Everything that grants a bonus is a container of atoms

Item affixes, implicits, socket inserts, socket-combination bonuses, set bonuses, charm bonuses, and
enhancement bonuses all resolve to **atoms bound to an actor**. No lane invents a second effect
mechanism, a second modifier bag, or a bespoke stat-application path.

If your lane's mechanic cannot be expressed as *(container → instance → binding → atoms on the actor's
effect list)*, say so explicitly and explain what is missing. That is a finding, not a failure — but an
undeclared second mechanism is a defect.

### SC2 — The atom vocabulary is closed

**12 kinds, 5 attach points, 7 triggers.** No lane may add one. If your mechanic needs a thirteenth
kind, write it up as a named request with the reason; do not assume it.

Also inherited and non-negotiable:

- `stat.derived` is quarantined `None/None/None` today (defect D6). Anything built on `combat.*`
  channels binds **nowhere** until E12 ships the first consumer. Say so where it bites your lane.
- `stat.modify` on `defense` is **match-scope only** (gap G8). A per-item `+armour` affix bound to one
  actor silently does nothing.
- `stat.modify` and `stat.derived` carry **no trigger** — they are permanent modifiers and the runtime
  owns apply/revert.

### SC3 — Reserved `container_kind` values

`effect_container.container_kind` is a closed enum today: `item` · `trait` · `skill` ·
`species-passive` · `patron` · `world-buff`, and `container_id` must be prefixed to match
(definitions §1). Adding a value is **ask-first** under E5's boundaries.

To stop lanes colliding, these names are **reserved in advance**. Use exactly these strings if your lane
needs one, and flag the addition as a reviewed change against E5:

| Reserved | For | Lane |
|---|---|---|
| `item` | equipment (exists already) | I3 |
| `gem` | a socket insert | I4 |
| `set` | a set-bonus tier | I5 |
| `charm` | an inventory-carried bonus source | I10 |

Anything beyond these four must be justified, not assumed. Prefer expressing a mechanic with an existing
kind over minting a new one.

### SC4 — Units, and no floats in content

From definitions §2, non-negotiable:

| Value | Unit |
|---|---|
| Primary-channel magnitudes (`hp`, `atk`, …) | game units |
| Derived-channel magnitudes (`combat.*`) | **resolver points** — sigmoid scale, `CritRateScale = 100.0` |
| Chances, ratios, multipliers | **integer per-mille** |
| Durations | integer ms |

`+10 hp` and `+10 fire power` differ by roughly an order of magnitude in effect. **Tier bands are
authored per channel family and never copied across.** Every number in your doc states its unit.

### SC5 — Determinism, including after mutation

Anything rolled is reproducible from a recorded seed; no ambient RNG, ever. The atom layer's contract is:

> same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical instance.

**This is the contract that enhancement and reroll strain**, because `effect_instance` freezes its rolls
by design. Any lane that mutates an existing item must say how reproducibility survives it. The
answer is *record the operation*, never *silently re-roll*: an item's current state should be derivable
from its origin seed plus an ordered, recorded list of operations applied to it.

**I6 owns the instance-mutation model.** I7, I4, and I9 adopt it rather than each inventing one.

### SC6 — Reject, never ignore

Every invalid authoring or player action names a **reason code**. Thirty-three exist already
(definitions §10) — reuse one where it fits, and propose new ones in a table if it does not. A silently
ignored bad input is the exact failure the atom program exists to remove.

### SC7 — Code or data

> A thing can be **data** if adding a row changes behaviour **without new code**. If a new row needs a
> new consumer, it must be **code**.

The repo has scar tissue here: `status.expose.*` is a legal, registered, fully-valid derived channel
with **zero readers**. *A row no code consumes is not content; it is a lie in a table.* Every table your
lane proposes names its consumer.

### SC8 — Standalone-first

Every mechanic must be fully usable **with the PvZ game closed**. The injector may enrich, never gate.
Nothing may require a live lawn to function.

### SC9 — Power is open, and you may not depend on it

The power model (E9) is build position 15 and its cost function is unsolved for multiplicative pairs.
`power_json` is nullable. Your lane may **state what it would want** from a power number (a budget, a
band, a comparison), but must ship a design that works before power exists.

---

## 3. Document shape — every lane doc has these sections, in this order

Consistency matters more than personal style: these get reconciled into one program.

1. **Status header** — `Lane <id> SSOT, drafted 2026-08-22`, and the sentence *"Enriches
   [item-ideal.md](../item-ideal.md); bound by [enrichment-contract.md](enrichment-contract.md)."*
2. **Scope** — two lists: **This lane owns** / **This lane does NOT own** (name the lane that does).
3. **The model** — the mechanic, explained plainly, in one page or less before any table.
4. **Options considered, and the recommendation.** At least two real alternatives with their
   tradeoffs, then a clear pick and why. Brainstorm widely; then commit. A doc that lists options
   without picking has moved the decision, not made it.
5. **Data shape** — proposed tables, columns, and how they map onto the existing atom schema. Say
   explicitly which existing columns you reuse and which are new.
6. **Validation and reason codes** — a table: bad input → reason code.
7. **Worked examples with real numbers** — at least three, with units. An item, a bonus, a cost. Numbers
   are illustrative, not balanced; say so.
8. **Failure modes** — what goes wrong with this mechanic in games that shipped it, and what in your
   design prevents each one. Be specific and unsentimental.
9. **What this lane needs from other lanes** — a numbered list, each naming the lane. **This section is
   how insufficiency gets caught; do not leave it empty.**
10. **Open questions for the owner** — decisions you deliberately did not make.

Write in plain English, short sentences, concrete verbs. Match the tone of the existing architecture
docs. No filler, no marketing voice, no "in conclusion". Tables where a table is clearer than prose.

---

## 4. Lane boundaries — the cuts that matter

The general rule: **a bonus is owned by the lane that owns *where the combination lives*.**

| Combination lives in | Lane |
|---|---|
| one item's own affixes | I8 |
| one item's sockets | I4 |
| across several equipped items | I5 |
| the inventory, unequipped | I10 |

Ten specific cuts, each written because two lanes would otherwise both claim it:

| # | Contested thing | Owner | The other lane does this instead |
|---|---|---|---|
| 1 | The word "slot" | I2 owns **equip slots**; I4 owns **sockets** | Never use the other's word |
| 2 | The rarity ladder and its ordinals | **I1** | Others *read* rarity and register what they read |
| 3 | How many sockets a rarity grants | **I4** proposes, **I1** registers it | I1 does not invent socket counts |
| 4 | Fixed modifiers on a base type (implicit) and base stats | **I3** | I8 owns only *rolled* affixes |
| 5 | Affix tier bands and the pool | **I8** | I1 owns how many affixes and which tier window |
| 6 | Post-drop mutation of a frozen instance | **I6** owns the model | I7, I4, I9 adopt it |
| 7 | Cost vocabulary (what you spend) | **I9** | I6, I7, I4 spend in I9's terms |
| 8 | Equip gating (who may wear this) | **I11** | Everyone declares they use the gate |
| 9 | Turning a loot event into an instance | **I12** | I1, I3, I8 supply inputs to it |
| 10 | Bags, stacking, salvage, comparison | **I13** | I3 lists categories; I13 stores them |

---

## 5. Required reading for every lane

In this order. Do not skip — a lane that re-derives a decision already locked here will be sent back.

1. [../item-ideal.md](../item-ideal.md) — the intent this enriches
2. This file
3. [../effect-atom/definitions.md](../effect-atom/definitions.md) — **wins over any spec**; §0 the model,
   §1 identity grammar, §2 values/rolls/curves, §4 rarity, §5 instances and ordering, §6 owner keys,
   §10 reason codes
4. [../effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md) — the container
   contract you are building on
5. [../effect-atom/spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md) — freezing
   and equipping
6. [../effect-atom/atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) — the closed vocabulary and
   the eight gaps that must become rejections
7. [../effect-atom/atom-family-library.md](../effect-atom/atom-family-library.md) — the ~71 affix
   families you draw from

Lane-specific extras are named in each lane's brief.

---

## 6. Owner decisions for this round (2026-08-22)

Binding inputs. Do not re-litigate them; design *within* them.

| # | Decision | Which lanes it binds |
|---|---|---|
| **OD1** | **Three equipment categories** keyed on **frame**: human-like (`humanoid`), plant-like (`plant`), and the commander who is human, plant, or zombie and therefore wears one of the two. Frame is **not** faction | I2, I3, I11 |
| **OD2** | **~15 equip slots** on each pure frame, **including `main-hand` and `off-hand`**. Humanoid and plant get parallel role naming | I2 |
| **OD3** | **Hybrid frames get fewer slots than 15** (12–13) but each role accepts a base type from **either** frame — breadth is paid for with depth | I2, I3 |
| **OD4** | **Rarity is both a long ladder and overlapping.** Many rungs with fine gradation, **and** deliberate power overlap between adjacent rungs, so a high-roll low rarity can beat a low-roll high rarity. Design the overlap mechanism, do not just assert it | I1, I8, I12 |
| **OD5** | **Combination bonuses are first-class**: socket combos (several inserts in one item), set combos (several equipped items), and charm effects from **unequipped** inventory all grant real bonuses | I4, I5, I10 |
| **OD6** | **Every bonus source is a container of atoms** — items, inserts, socket combos, set tiers, charms, enhancement. See SC1 | all |
| **OD7** | **Primary actor attributes do not exist yet.** I11 proposes them by reverse-derivation from what the requirement gate needs. This is a proposal requiring owner sign-off, not a decision | I11 |

## 7. Rules of engagement

- **No web search.** Use your own knowledge of shipped games. Where you cite a game's numbers, mark them
  as recalled and not verified — a number that reaches a spec must be re-checked against a source later.
- **Write exactly one file**, the path named in your brief. Do not create others.
- **Never edit** [../item-ideal.md](../item-ideal.md), this file, or another lane's file.
- **Never run git write commands.** Read-only git is fine.
- Cite `file:line` for any claim about this repo's code.
- If your lane turns out to be mostly empty, or mostly someone else's, **say that** — an honest boundary
  correction is worth more than a padded document.
