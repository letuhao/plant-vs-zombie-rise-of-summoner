# Inventory and the workshop — the armoury, the cost vocabulary, and three ways to change an item

**Status:** Detail design, 2026-08-23. **Document 5 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A11** (inventory/storage),
**A12** (materials/crafting), **A13** (reroll), **A14** (charms), **A15** (consumables), **A18**
(drop/loot toast). The largest remaining scope — six gaps, five source SSOTs.

**Sources, all read this session:**
[`ssot-inventory.md`](../architecture/item/ssot-inventory.md) §2.2–2.5, §5.7, §5.9 ·
[`ssot-materials-crafting.md`](../architecture/item/ssot-materials-crafting.md) §3.1–3.3, §5.1–5.3 ·
[`ssot-reroll.md`](../architecture/item/ssot-reroll.md) §3.1–3.2 ·
[`ssot-charms.md`](../architecture/item/ssot-charms.md) §3.1–3.2 ·
[`ssot-consumables.md`](../architecture/item/ssot-consumables.md) §3.1–3.2 ·
[`ssot-generation.md`](../architecture/item/ssot-generation.md) §3.4–3.5.

---

## 1. The armoury, not bags — A11

**One player-scoped armoury.** No per-specimen bag, no bank, no stash tabs. A specimen doesn't *hold*
items; an **assignment** points from `(specimen, role)` at an item in the armoury — *"swap this helm
onto that demon" is one row update, not a move between two containers.*

**Storage grade is derived, never authored** — the same discipline `atom_id` uses:

```text
stock_eligible(container) = pool_rolls == 0 AND every fixed-core atom is Fixed AND no sockets
```

An author cannot tick "stackable" and be wrong. **Promotion is one-way**: the moment a stock item is
socketed, enhanced, or rerolled, it stops being fungible — the counter decrements, a real row is
created carrying the mutation log, and it never re-stacks even if the mutation is reverted, because
reproducibility now depends on an operation list other copies don't have. The card's footer (block 11)
must render this as a real state, not silence: a stock item shown next to a mutated copy of the same
base type are visibly different things.

**No bag limit — the honest cost, and what replaces it.** Unlimited rows, because neither of limited
inventory's two real jobs (field-pressure or monetisation) applies here — no in-run inventory exists at
all, and there's no monetisation to hook. The honest cost is *"an unlimited stash becomes a museum
nobody sorts."* Five counters replace capacity as the pressure:

1. High-volume grades never become rows (stock/materials are counters, not instances).
2. **An inbox** — every rolled item arrives `seen = 0`; the header shows an unreviewed count; an inbox
   can be emptied, a stash cannot.
3. **The gap board** (§1.1) — tells the player *where acting would help*.
4. Auto-salvage rules so junk never becomes a row.
5. A structural ceiling (20,000 rolled rows/player) that rejects, not silently truncates.

### 1.1 The gap board — 720 cells, one query, not a store

For each `(specimen, role)` cell: `locked` / `empty` / `stock` / `rolled`, plus whether an
**unassigned strict improvement exists in the armoury**. Defaults to showing only cells with an
available improvement — a short list — and collapses "issue stock" into one action per specimen.
48 specimens × 15 roles = 720 cells, each comparing ≤8 atoms against a candidate set, **computed
server-side, memoised per `(player_id, armoury revision, catalog_revision)`** — a query, not a
precomputed table.

### 1.2 Salvage — four guards, preview then commit, an undo window

| Guard | Rule |
|---|---|
| **G-A** | An assigned item is never salvageable |
| **G-B** | A locked item is never salvageable, through *any* path including auto-salvage |
| **G-C** | Loadout membership implies lock |
| **G-D** | **Best-in-role items are excluded by default, and *listed* as excluded** — the one guard that prevents the actual disaster: players don't lock what they haven't looked at |

**Preview then commit, atomically.** `POST /salvage/preview` returns the exact id list, the yield, and
a guard report — how many matched, how many excluded by each guard **with the excluded items named**.
Commit takes the preview's id list, so a race adding an item between calls can't widen the selection.

**Undo: 24 hours or 200 salvages, whichever comes first.** Credits the yield immediately (escrowing it
would defeat bulk salvage's point); undo restores rows and debits the yield, refusing
`SalvageUndoInsufficientMaterials` if the player already spent it — and the preview says so up front:
*"undo is available while you still hold the yield."*

---

## 2. The cost vocabulary — five spends, three verbs — A12

**Twenty-one material ids, plus souls — the table every other operation in this document cites.**

| Class | Id shape | Count | Answers | Interchangeable with |
|---|---|---:|---|---|
| **Souls** | ledger balance | — | *permission* — a flat fee on every operation | nothing — the only fungible thing in the game |
| **Substrate** | `substrate.{frame}.{grade}` | 8 | *body* — frame-locked, graded by item level | nothing — frame and grade are hard gates |
| **Shard** | `shard.{band}` | 4 | *ceiling* — the rarity band an operation may reach | nothing — you cannot buy a ceiling with volume |
| **Essence** | `essence.{element}` | 6 | *direction* — carries no magnitude | nothing — an element is not a quantity |
| **Catalyst** | `catalyst.{verb}` | 3 | *which operation* | nothing — see below |

**Three catalysts, three verbs, no fourth:**

| Catalyst | Verb | Spent by |
|---|---|---|
| `catalyst.forge` | **make** — new matter, including boring a socket | craft a base/gem, bore a socket |
| `catalyst.temper` | **improve** along an axis the item already has | enhancement, rarity elevation |
| `catalyst.flux` | **re-randomise**, keep the thing, redraw values | reroll |

**Shards key on a *band*, not a rung** — four ids (`common`/`rare`/`epic`/`legendary`) regardless of
how long the rarity ladder gets, so a twelve-rung ladder doesn't mint twelve shard ids. Adding a rung
inside a band is free; adding a **fifth band** is a reviewed change. That asymmetry is deliberate: it
makes the long ladder cheap and the ceiling expensive.

### 2.1 Salvage yield — pure, integer, and provably lossy

```text
substrate.{frame}.{grade}  ×  substrateBase[band] + affixes
essence.{element}          ×  min(essenceCap[band], elemental)   per distinct element present
shard.{band − 1}           ×  shardBack[band]                    // never the item's own band
catalyst.temper            ×  enh / 3                             // integer division
souls                      ×  0                                   // salvage never mints currency
```

**R1 — the band-1 rule.** Salvage returns a shard **below** the item's own band, never its own. Rarity
flows downhill through recycling only — you cannot bootstrap a ceiling by feeding the grinder its own
output.

**R2 — the strict-loss invariant**, tested as a property over the whole recipe table, not a design
intention: *for every class a recipe spends, salvaging that recipe's output returns strictly less of
that class.* `catalyst.forge` and `catalyst.flux` are **never** returned by salvage — the crafting and
reroll loops can never sustain themselves. `substrate` is returned generously, on purpose: it's the
cheap class, the one a player should never hesitate to spend.

---

## 3. Reroll — three operations, blind by default, one priced take-back — A13

**Two candidates cut on schema grounds, not taste**: a rarity reroll would repoint `container_id` —
a re-instantiation against a different template, not a re-draw, so it belongs to I1/I12, not here. Add
or remove an affix changes `pool_rolls`, a rarity-selected container column — same argument.

**Three operations, one price function:**

| Operation | Re-draws | Targets | Feel |
|---|---|---|---|
| **Temper** | the **value** of one affix, inside its own authored range | one drawn `seq` | cheap, low variance, the on-ramp |
| **Reforge** | **identity, tier, and value** of a chosen subset | `T` of `pool_rolls` drawn seqs | the power tool — everything untargeted is anchored, and anchoring is the price |
| **Imprint** | nothing — **places** a chosen family at the window's floor | one drawn seq | deterministic, guaranteed, deliberately the worst legal version |

*"Reroll one affix" is not a fourth operation* — it's Reforge with `T=1`, and the anchor multiplier
already prices it correctly. One price function, not two that could drift.

**Risk shape: blind by default.** Three shapes were weighed — blind accept (highest variance,
cheapest, picked as default), pick-from-N (removes "worse" outright, rejected — the owner's stated
want was outcomes that *can* be worse), see-then-decline (also removes worse, rejected as default).
**One priced exception: Recall.** An opt-in flag on Temper/Reforge, costing the base price plus one
scarce recall token. After a Recalled operation, and only until the next operation on that item, the
player may append a **revert** — never a deletion; the log stays append-only. Capped at one revert,
gated on a material the economy controls, so it isn't a laundering loophole.

---

## 4. Charms — one binding at `player:{id}`, priced by a size budget — A14

**The crux: whose bonus is a charm?** Four scopes considered — commander-only (invents an actor that
doesn't exist yet), per-deployed-actor (N session-scoped bindings rebuilt every deploy — `entity:` is
explicitly never durable), per-specimen pouches (multiplies the roster-scale problem the ideal already
flags as unsolved), and:

> **Picked: one binding per attuned charm, at `player:{id}`, created at run start, withdrawn at run
> end.** *"The atoms apply to every actor this player has deployed, and to nothing else"* — never
> match-wide, and must never be resolved as such.

**Priced by a size budget, not bag space and not an upkeep resource.** Bag space was rejected on its
own terms — this game's inventory is a web list, not a spatial grid, so there's no tetris to pay with,
only a row count wearing a costume. The real costs:

1. **Attunement points** — opportunity cost, real because charm sizes vary.
2. **Exclusivity** — a charm committed to one live run can't serve another; running expeditions wide
   means splitting charms thin.
3. **Authored drawbacks on the top class** — a large charm carries a negative atom. Not every charm,
   only the ones big enough to distort a build.

---

## 5. Consumables — six classes, one refused — A15

| Class | Fires | Duration | v1 |
|---|---|---|---|
| `restore` | once at use | instant | **author** |
| `draught` | at run start, squad snapshot | run-scoped, withdrawn at run end | **author** |
| `ward` | at battle setup | integer ms — the only real clock v1 has | **author** |
| `board` | once at use, lawn only | instant | declare only — blocked on an overlay use affordance |
| `revive` | once at use, targets one actor | instant | declare only — `Downed → Charging` already exists in the turn FSM; only the *use moment* is missing, which is the action layer's |
| `utility` | once at use, at a menu | permanent, outside combat | declare only |

**Permanent stat-up is refused as a consumable**, on three grounds: it has no container to bind to
after consumption, so the only way to make it stick is a second sourceless write — the exact ad-hoc
path this whole program exists to remove; it's invisible to `actorPower`, so every budget and
comparison understates the actor forever; and it duplicates enhancement, which already has a mutation
model. **What's allowed instead:** a permanent stat-up as a one-shot **quest reward**, authored not
farmed — a progression event with an item wrapper, not a resource to grind.

---

## 6. The drop toast, and visible pity — A18

**Pity counters, visible in the UI** — *"a no-money game has no reason for opacity; visible counters
turn dead pulls into progress,"* reused verbatim from the summoning program. **One deliberate
divergence:** loot pity counts **equipment items minted, not loot events** — a 20-hour expedition is
one event yielding four items, a battle is one event yielding zero; counting events would make
expedition players rich and battle players poor for no design reason.

**Floors sit where they actually bite**, checked against the same trap summoning found (*"the original
10-pull rare floor fired only ~5% of the time — cosmetic"*):

| Counter | Threshold | Natural drought probability | Displayed |
|---|---|---|---|
| epic+ | hard floor at **25** items | 0.90²⁵ ≈ 7.2% | `18/25 to guaranteed Epic` |
| relic+ | soft ramp from item 150, hard ceiling **400** | ramp fires for most players first | `212/400 to guaranteed Relic` |

**No unique pity, deliberately, said out loud rather than left as an omission** — a guaranteed unique
means every player converges on the same handful in the same week, and it stops being a story.

**Two guarantees the toast must distinguish from ordinary drops:** first clear of any content id grants
one **fixed, authored** item (`pool_rolls = 0` — never disappoints); boss ticks/waves guarantee at
least one equipment item, authored as a group with no `nothing` row, so "guaranteed" needs no
special-case rendering.

---

## 7. What this document draws on the plate, and what it defers

Given the scope, two primitives are drawn as real components — the ones every other piece here reuses
— and the rest are named as their own future screens rather than sketched thin:

**Drawn:** the cost-chip vocabulary (§2), the three reroll operations with Recall (§3), the salvage
preview with its four named guards (§1.2).

**Deferred, explicitly:** the 720-cell gap board (a full screen, not a component — belongs with
whichever stage hosts the armoury), the charm pouch UI (thin until document 7's action layer exists to
consume a charm's atoms in a run), and the consumable action-bar entry (depends on the action layer's
own targeting UI, document 7's territory, not this one's).

---

## 8. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **A stock item and a mutated copy of the same base type never render identically** | promotion's one-way state is invisible on the card |
| 2 | **Salvage preview names every excluded item, per guard** | a bulk operation silently protects items with no explanation |
| 3 | **Undo states its own condition** (*"available while you still hold the yield"*) before it's needed | a player discovers the constraint only when undo fails |
| 4 | **A material chip never claims interchangeability it doesn't have** | substrate, shard, essence, and catalyst render as though any one could substitute for another |
| 5 | **Reforge always shows which seqs are anchored**, not just which are targeted | a player can't tell what a Reforge will leave untouched |
| 6 | **A Recalled operation's revert window is visibly time-boxed** ("until your next operation") | a player assumes revert is available indefinitely |
| 7 | **A charm's scope note is exact**: "every actor you deploy, nothing else" | it reads as match-wide or squad-wide loosely |
| 8 | **A pity counter renders as `x/threshold to guaranteed <rung>`**, never a bare percentage | the player can't tell how close they are |

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — inventory, materials, reroll, charms, consumables,
    drop generation.
[x] I read every doc in the §1 row(s) this session: ssot-inventory.md §2.2-2.5/§5.7/§5.9,
    ssot-materials-crafting.md §3.1-3.3/§5.1-5.3, ssot-reroll.md §3.1-3.2, ssot-charms.md §3.1-3.2,
    ssot-consumables.md §3.1-3.2, ssot-generation.md §3.4-3.5.
[x] I checked decisions.md for a lock covering this (Game GUI row).
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE where cited — the SSOTs' own code citations (OwnerScope.cs:38,
    TurnState.cs:22, RpgStore.cs various) were spot-read, not re-derived independently; this document
    largely trusts the source SSOTs' own citations rather than re-verifying each one, unlike documents
    1/8/9 which had running code to check against directly.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite exists — item/README.md
    states no code, no schema. R2 (strict-loss invariant) is described in the source as "a property
    test over the whole recipe table" that does not yet exist to run.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated — no correction to a source SSOT was needed this pass.
```
