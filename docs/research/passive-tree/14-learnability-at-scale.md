# Learnability at 25,900 nodes — re-checking doc 07 against D24 / D25 / D28 / D29 / D30

**Status:** research, 2026-09-05. Not a spec, no build authorized. This is a **re-check** of
[07-learnability-and-surface.md](07-learnability-and-surface.md) against five owner decisions taken
after it was written. Doc 07 is not replaced — most of it survives, and where it survives this
document says so and stops.

**What changed under it:**

| | Doc 07 assumed | Now decided |
|---|---|---|
| Tree shape | 7 tiers, ~29 nodes | **D29** — 10 tiers × 2 branches, ~40 nodes |
| Roster | ~50 trees, demon *families* | **D27/D29** — 39 closed (12 aptitudes + 6 elements + 21 statuses); families are a build-order task |
| Species trees | deferred | **D30** — every one of 841 species gets its own 29-node tree |
| Breadth | unbounded, flagged as an open question | **D25** — unlock cost rises with the nodes you already own |
| Tier gate | local to one tree | **D28** — satisfied by your largest same-stance tree |
| Catalog | static (owner, same day) | **D24** — unchanged, and now the acceptance criterion |

**Evidence marking:** FACT = read from code or data this session. INFERENCE = drawn from a fact.
RECALL = general knowledge, unverified in-repo. Computed figures name what produced them.

---

## Answer up front

### What broke

Doc 07 made twelve load-bearing calls. **Nine hold, two need amending, one is wrong.**

| # | Doc 07's call | Verdict at the new scale |
|---|---|---|
| 1 | L1 — same catalog for everyone | **Holds**, and matters more: 25,900 nodes is exactly the size at which a rolled catalog would be unlearnable |
| 2 | L2 — a rebalance may move a magnitude, never an identity | **Holds, cost rises.** The id-stability surface is now 841 separately generated artifacts, and `AptitudeAllocation.Single` throws on an unknown id (`src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs:36-39`, FACT) |
| 3 | L3 — readable before you own it | **Holds.** Thirteen unit classes ship and `formatMagnitude` still refuses a bare number (`web/fusion-rpg-web/src/i18n/magnitude.ts:15`, FACT) |
| 4 | L4 — plan a build without spending | **Holds and gets more important** — D25 makes a wrong order expensive |
| 5 | L5 — a dead node says so | **Holds** |
| 6 | L6 — you can see the rule you are scored on | **Holds, and now covers two rules**, not one. Focus was the only hidden coefficient in doc 07. D28 adds a second, and it is harder to see |
| 7 | L7 — what you learned on the commander is true on the demons | **Amended.** True for the 39 shared paths. **False for a species tree**, which is unique by construction (D23/D30) |
| 8 | The four-level IA (Yours → All paths → one tree → one node) | **Holds for the shared corpus. Breaks if species trees are put in level 1** — see §3 |
| 9 | "The tree, not the node, is the unit of browsing" | **Holds, and is now the whole answer.** 39 tree cards is still the window tier |
| 10 | "One tree's ~29 nodes is render-all under the shipped volume rule" | **Wrong.** `RENDER_ALL_MAX = 24` (`web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx:21`, FACT) — 29 was already over it and 40 is further over. The error is applying the wrong rule: a fixed lattice is **one entity's own content**, which is GG-61's subject, not GG-50's. §2.3 |
| 11 | The Plan object as the answer to D21 | **Holds and extends.** D25 gives a plan a price, which is the thing a plan was missing. §5 |
| 12 | "No GUI principle had to bend" | **Still true**, with one rule read differently (GG-61 in place of GG-50 for a lattice) and one live defect found in an unrelated surface (§3.4) |

### The revised information architecture

Two changes to doc 07's four levels, and nothing else.

```text
Actor sheet (band 2, already exists) → Passives tab
├── Level 0  Yours            paths this actor has invested in · Focus · dead-trait count · unspent
├── Level 0b Bloodline        THIS demon's own path — pinned, never in a browse   ← new
├── Level 1  All paths        the 39 shared paths, ordered, searchable            ← 39, not 50
├── Level 2  One path         2 branches × 10 tiers, ~40 traits, one fixed lattice
└── Level 3  One trait        value · what it costs next · depth · exclusion print · where it goes
```

1. **A species tree is level 0b, not level 1.** It is pinned to the demon that has it and never
   enters the browse. 841 unique trees create **zero** browse pressure, because a player never picks
   one — you get the one your demon is. §3.
2. **Level 2 is a GG-61 surface, not a GG-50 one.** A 40-cell lattice is bounded by construction and
   scrolls inside `PanelShell`'s own bound (`max-h-[min(720px,82vh)]`,
   `web/fusion-rpg-web/src/shell/PanelShell.tsx:86`, FACT). §2.3.

Depth from a stage is unchanged: sheet (1) → path (2) → trait (3). GG-10's budget, not over it.

### The verdict

**Yes, ~25,900 nodes is learnable — and only because the number a player has to learn is 1,560.**

| Quantity | Nodes | Share of 25,949 |
|---|---:|---:|
| Whole corpus (39 × 40 + 841 × 29) | 25,949 | 100% |
| **The shared corpus — the only part a build guide can be written about** | **1,560** | **6.0%** |
| A 30-demon player's whole reachable reading surface (1,560 + 30 × 29) | 2,430 | 9.4% |
| Traits *open* to a one-aptitude build at Θ=100, across the twelve primary paths (computed, §7) | 176 | 0.68% |
| Traits that build actually **owns** at Θ=100 under D25 (computed, §5.1) | 13–36 | **0.05–0.14%** |

The last row is the honest one. A player at Θ=100 owns somewhere between thirteen and thirty-six
traits out of twenty-six thousand. **This is not a defect; it is what D25 was decided for**, and it
is the same shape every deep-tree game in the genre ships (RECALL). But it means the surface's job
is never "show the catalog" — it is *"help this player find the thirteen that are theirs, out of a
hundred and seventy-six that are open, inside four paths out of thirty-nine."*

Two consequences follow, and both are design work rather than opinions:

- **The 24,389 species nodes are not a learning surface at all.** They are per-demon content, read
  once when a demon is bound, never compared across. Treating them as catalog is the mistake that
  would make this unlearnable.
- **The remaining 1,560 must be exhaustively legible**, because it is the whole shared vocabulary of
  the game and it is what every build guide, screenshot and shared plan refers to.

### Did a GUI principle have to bend?

**No.** One was mis-applied by doc 07 and is corrected here (GG-50 → GG-61 for a single lattice),
and one live defect was found in a surface this design will sit next to (§3.4). Neither is a bend.

---

## 1. The arithmetic, recomputed

### 1.1 The corpus

| Category | Trees | Nodes each | Nodes |
|---|---:|---:|---:|
| Aptitudes | 12 | 40 | 480 |
| Elements | 6 | 40 | 240 |
| Statuses | 21 | 40 | 840 |
| **Shared subtotal** | **39** | | **1,560** |
| Demon species | 841 | 29 | 24,389 |
| **Total** | **880** | | **25,949** |

FACT: 841 species across 503 files, counted this session over `data/seed/demons/species/*/*.json`.
FACT: 12 aptitudes at `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:38-51`; 6 concrete elements;
21 statuses per `status-ssot.md`. Matches D27's roster and D29's own `39 × 40 = 1,560`.

With D3's two tracks, that is **51,898 places a player could spend** — 3,120 in the shared corpus
and 48,778 across the species trees.

> **⚠ D29 and D30 disagree on tree size, and D10 says they must not.**
> D29 sets every tree at **10 tiers, ~40 nodes**. D30 gives each species a **29-node** tree. D10 is
> *"Same shape everywhere: every tree is 2 branches × tiers."* Three readings are possible — species
> trees are genuinely a different shape (D10 amended), or D30 predates D29's number and means "a
> full tree" (total becomes 841 × 40 = 33,640 and the corpus 35,200), or 29 is deliberate because a
> species tree is gated on specimen level rather than Θ. **This document uses 29 because that is
> what D30 says**, and flags the discrepancy rather than picking. Owner item §10.1.

### 1.2 Reading load

At the ~3 lines per node doc 07 used: the shared corpus is **~4,700 lines**, and the whole corpus is
**~78,000 lines** — a 300-page book. The first number is a wiki. The second is not a thing anyone
reads, which is why §3 keeps it out of the browse.

### 1.3 Comparison, restated

Doc 07's headline was *"more nodes than Path of Exile's whole tree"* (RECALL: ~1,300). At the new
numbers the shared corpus is 1,560 — still just above PoE — and the total is **twenty times** it.
The comparison that now matters is different: **PoE's 1,300 is one map every player learns; ours is
39 maps every player learns plus 841 maps nobody learns.** The design problem is not size, it is
**keeping the two piles apart.**

---

## 2. Doc 07's calls, re-checked

### 2.1 The learnability contract L1–L7

L1, L3, L4 and L5 are unaffected by scale — they are properties of the catalog and of the
draft/commit flow, both of which are unchanged. Two need work.

**L6 now guards two hidden rules, not one.** Doc 07's L6 was written about the focus multiplier `F`:
*"a player scored on a rule they cannot see will not believe the game is fair."* D28 adds a second
rule with the same property and a worse failure mode — `F` only changes how big a number is, while
D28 changes **whether a node exists for you at all**. A wrong number is an argument; a node that is
open for no visible reason is a bug report. §6 is the design.

**L7 splits.** *"What I learned on my commander is still true on my demons"* is true for the 39
shared paths — the catalog is shared, only allocation is per-actor (D21). It is **false by
construction** for a species tree, because D23's whole reward is *"nodes no other tree has."* The
promise the player should be given instead is two sentences, not one:

> *"Every path you learn works the same on every creature you own."*
> *"On top of that, each creature has one path only its kind has."*

That is not a weakening. It is the collection hook D23 was decided for, stated honestly.

### 2.2 The four-level IA

Levels 0, 2 and 3 survive verbatim. Level 1 survives at 39 trees. The break is only that **species
trees must not enter level 1** — §3.

Level 0's contents change in one place: it gains the **path-affecting-path** readout D28 makes
necessary (§6), alongside the Focus line doc 07 already put there.

### 2.3 The volume rule, rechecked — and doc 07 got one line wrong

The shipped rule, FACT, `web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx:21-22`:

```ts
const RENDER_ALL_MAX = 24;
const SEARCH_FIRST_ABOVE = 240;
```

| Surface | Count | Tier |
|---|---:|---|
| All paths (shared only) | 39 | **windowed** (25–240) — unchanged conclusion |
| All paths + species in one browse | 880 | **search-first** — a search box over a bag of trees, which is the "database viewer" GG-25 rejects. §3 rejects this arrangement |
| Traits flattened across paths | 25,949 | never rendered — traits only exist inside a path |
| **One path's lattice** | **40** | doc 07 called this "render-all"; **40 > 24, and 29 was already over** |

**The fix is a rule swap, not a redesign.** GG-61 says in its own text: *"This is not GG-50. GG-50 is
about many entities… This rule is about one entity's own content — one actor's 99-channel
derived-stat sheet."* A path's 40 traits are one entity's own content, bounded at 40 by D29, laid
out as a fixed 2 × 10 lattice. It is governed by GG-61: the shell declares a bounded height and the
body scrolls. `PanelShell` already does exactly that (`:86` `max-h-[min(720px,82vh)]`, `:98`
`overflow-y-auto`, FACT).

**What this costs:** a GG-61 volume fixture for the lattice at the 1280×720 floor. Ten tier rows
will not fit in the panel body there, so the tier ladder scrolls — and **the tier ladder is what the
player navigates by**, which means the current tier and the next locked one must both stay reachable
without hunting. Concretely: Level 2 opens scrolled to the player's own depth, not to tier 1.

### 2.4 The Plan object

Holds, and D25 completes it. See §5.

---

## 3. The species-tree problem

### 3.1 The question, stated precisely

A player who owns 30 demons holds **30 unique trees plus the 39 shared ones**. Doc 07 never faced
this because D23 deferred species trees and D30 had not happened.

### 3.2 What already exists — checked before proposing

| Surface | Path | What it is |
|---|---|---|
| Actor sheet, six tabs incl. `passives` | `web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx:19-26` | Band-2 panel, per-actor, opens over any stage. FACT |
| Passives tab, four locked slots | `web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx:5-20` | Placeholder, reason stated. FACT |
| **Demon Codex** — every species, `seen`/`discovered`, silhouette when neither | `web/fusion-rpg-web/src/features/demons/DemonsPage.tsx:365-390`, `lib/bus/demons.ts:50-51,117-121` | **A shipped species reference with a discovery state.** FACT |
| Pacts layer — bound demons, loyalty, tribute | `web/fusion-rpg-web/src/layers/pacts/PactsLayer.tsx:47-60` | Contracts, not content. FACT |
| Almanac layer — Creatures + Recipes tabs | `web/fusion-rpg-web/src/layers/almanac/AlmanacLayer.tsx:6-9` | Its own comment says the fuller per-species book *"has no real backing yet."* FACT |

So the repo already has a species-shaped reference surface with an unlock state, and it is **not**
the Almanac — it is the Codex.

### 3.3 The answer

**A species tree is the demon's own content, reached two ways, and it is never in a browse.**

1. **To spend:** the demon's actor sheet → Passives tab → its bloodline pinned above the shared
   paths (level 0b). One extra card on a surface the player already opens for that demon. Zero new
   navigation, same as doc 07's argument for the shared trees.
2. **To read:** the Codex entry for that species, read-only — the same relationship doc 07 gave the
   Almanac for shared paths, and GG-9 permits exactly it (*other surfaces link into the canonical
   one*). A `discovered` species shows its bloodline; an undiscovered one keeps the silhouette it
   already has.

**Why this is not a dodge — three reasons, in order of strength.**

**A species tree is not a choice, so it needs no chooser.** Every other browse in the game exists
because the player picks from it. A player cannot pick a bloodline: it is a property of the demon
they bound. There is no build-planning reason to put 841 of them side by side, because no decision
is taken by comparing them. (INFERENCE, but a strong one: D23's own framing is *"you give up build
freedom and receive something unobtainable elsewhere."*)

**Keeping them out is what keeps the browse in the window tier.** 39 cards is the shipped
25–240 tier. 880 would cross `SEARCH_FIRST_ABOVE`, and a search-first browse over trees is the exact
shape GG-25 rejects.

**The one real cross-species question is a collection question, not a build question.** *"Which
demon should I bind next?"* is answered by the Codex, at the resolution the Codex already works at —
a rarity badge, an element, a favour triple (D17), and one line naming what the bloodline is *for*.
Not by 29 node descriptions × 841. That line is content the D30 pipeline must emit **per species, as
a summary** — one sentence per tree, 841 sentences, reviewable in an afternoon. That is a real, cheap
addition to the generator's contract and it is worth booking now (§10.3).

### 3.4 One live defect found next door

`DemonsPage.tsx:367-388` maps the **entire** species catalog into a grid with no volume strategy —
`(catalog.data?.species ?? []).map(...)`. FACT. At 841 species that is 841 DOM subtrees on one tab,
against a rule whose search-first threshold is 240. It is a GG-50 violation today, independent of
passive trees, and it is the surface a bloodline reference would be added to. Worth fixing before
anything is hung off it.

---

## 4. The two-track trait (D3) — one cell, one verb

40 traits × 2 tracks is 80 interactions per path. Rendering both on the cell turns a lattice into a
form, which is the failure to avoid.

**The design: the lattice carries exactly one verb per cell. The second track lives in the detail.**

The two tracks have different cadences, and that is what decides where each belongs:

| | Unlock | Deepen |
|---|---|---|
| Currency | Skill points | Souls |
| Bounded | Yes, by D25's rising price | **No** — souls are uncapped by PS-8 |
| How often | 13–36 times at Θ=100 (§5.1) | Unbounded, repeated |
| Reversible | Only by a full respec (D18) | Only by a full respec (D18) |

A rare, structural, one-shot act belongs on the map. A repeated, unbounded act with a rising price
does not — it needs a repeat control and a running total, and neither fits in a lattice cell.

**Level 2 — the cell.** Three states, one verb:

```text
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│  Deep Roots      │   │  Deep Roots      │   │  Deep Roots      │
│  +40 earth power │   │  +40 earth power │   │  +260 earth power│
│  Unlock · 14 pts │   │  Tier 8 · locked │   │  Depth 6         │
└──────────────────┘   └──────────────────┘   └──────────────────┘
     available              not open              owned
```

- The owned cell shows the **current** value, not the base one — a player reading their own map
  should read what they have, not what a guide has.
- `Depth 6` is a count and renders through the existing `count` unit class. No new class.
- The locked cell's reason is visible sibling text on the tier row, not repeated on 40 cells (§6).

**Level 3 — the detail.** This is where the deepen track lives:

> **Deep Roots** — earth power **+260**
> Depth **6**. Next depth **+43 earth power**, costs **1,900 souls**.
> `[ − ]` `[ + ]` `[ +10 ]`   → planned depth **9** · **6,900 souls**

Three rules on that control, each with a reason:

1. **A stepper, never a slider.** A slider needs a maximum; PS-8 forbids one. Souls are unbounded by
   design (ideal §4).
2. **Never a number input.** `AptitudesPage.tsx:64` and `ProgressionTab.tsx:107` both render a raw
   `NumberInput` under a raw-id label — a live GG-23/GG-24 defect doc 07 already flagged. A 40-trait
   grid must not inherit it forty times.
3. **The step edits the draft, not the server.** Commit stays one whole-allocation POST, the shape
   `AptitudeEndpoints.cs` already ships and the shape D18's full respec needs anyway.

**So the interaction count per path is 40 map decisions plus one detail per trait you actually
own — not 80.** That is the whole point of splitting by cadence.

---

## 5. D25 — the price of a plan, not of a trait

### 5.1 What D25 does to the arithmetic

D25: *"Unlock cost rises with the number of nodes an actor already owns — arithmetic, per actor."*
No slope is decided yet, so the table below sweeps it. Skill points are `Θ × 1`
(`data/tuning/aptitudes.v5.json:17`, FACT), and `PointBudget.PointsFor` has **no cap**
(`src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs:49-58`, FACT).

Cost of the *k*-th trait `c(k) = 1 + d·(k−1)`; traits affordable with Θ points (computed this
session):

| slope `d` | Θ=100 | Θ=500 | Θ=1,000 | Θ=5,000 |
|---|---:|---:|---:|---:|
| 1.0 | 13 | 31 | 44 | 99 |
| 0.5 | 18 | 43 | 61 | 139 |
| 0.25 | 25 | 59 | 86 | 196 |
| 0.1 | 36 | 90 | 132 | 306 |
| *(no D25, flat 1)* | *100* | *500* | *1,000* | *5,000* |

**The shape, which is the finding:** owned traits grow like **√Θ**, not like Θ. D25 does not slow
breadth down — it changes its exponent. Doc 07's *"6.9% of the catalog at Θ=100"* is now
**0.05–0.14% of 25,949**, and the fraction *shrinks* relative to the corpus as Θ grows, for any
`d > 0`. That is exactly what D25 was decided for, and it should be stated plainly rather than
discovered by a player who expected a flat rate.

### 5.2 The mechanic already ships, twice

**This is not a new player concept.** `ContractPolicy.NextSlotPrice(purchasedSlots, …)` is an
arithmetic price in the count you already own — `SoulSinkPolicy.Price(SlotPriceStep × (purchased+1), …)`
(`src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs:176-177`, FACT) — and the FE already renders
it in player words: `· next 900 Souls` (`web/fusion-rpg-web/src/features/demons/contractView.ts:50-54`,
FACT). Souls are rendered today as composed sentences, not through `formatMagnitude`, so **no new
unit class is needed.** (Checked: the `UnitClass` union at
`web/fusion-rpg-web/src/contract/types.ts:33-56` holds thirteen, and none of them has to grow.)

### 5.3 The one good property, and it must be promised

Because the price depends on **how many** you own and not **which**, the cost of a set of *m* traits
is `Σ from k = owned+1 to owned+m of c(k)` — **independent of the order you take them in.**
(INFERENCE from D25's stated arithmetic shape.)

That is worth a printed promise, because the alternative is the trap D18 already dissolved once:

> *"A plan costs the same whichever order you follow it in."*

If the slope is ever made non-uniform per trait, that sentence stops being true and the plan object
stops being trustworthy. Booking it here so it stays a decision rather than a drift.

### 5.4 The Plan gets a price — three numbers, not one

Doc 07's Plan object is the right container; D25 gives it the field it was missing.

> **Ashroot Line** · 9 traits
> **112 points** and **48,000 souls** from where you are now
> Next: **Deep Roots** — 14 points
> *Every trait you own makes the next one cost more.*

- **Total** is the whole plan from *this actor's* current state, so the same plan applied to a fresh
  demon and to a deep commander prices differently — which is correct and must be visible, since
  D21 makes plan-to-many-actors the normal case.
- **Next** is what lets the player start without arithmetic.
- **The rule sentence appears once**, at the top of the tab, first time only (GG-45).

### 5.5 The rule this forces on shared builds — new, and load-bearing

L1 makes a build shareable because the catalog is identical for everyone. D25 makes **the price
personal**. So:

> **A shared plan may name traits and how many. It may never name a price.**

A build guide that prints *"this costs 112 points"* is wrong for every reader whose actor owns a
different number of traits. The share code doc 07 specified (`<catalog version> + [(treeId, nodeId,
points, soulLevel)…]`) is unaffected — it carries *what*, never *how much* — but any rendering of an
imported plan must price it against **the importing actor**, on arrival, and say so.

This becomes **L8** in §8.

---

## 6. D28 — making a non-local gate legible

### 6.1 The mechanic, precisely

D28: cross-unlock credits **one** tree — your largest same-posture mate — never a sum. In the
measured model the gate quantity for tree *i* is `p_i + max(p_j : j is a posture-mate of i)`, and
**only the gate reads the credit; power stays linear per tier**
(`tools/HybridViability/Program.cs:360-374`, FACT — the `"largest"` arm at `:367`, the comment *"the
GATE reads the credit"* at `:373`).

Postures are already player-facing groupings of four: Force, Finesse, Bastion
(`src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:11,38-51`, FACT — and the file's own comment says a
posture is *"READ… never stored"*, so this is display vocabulary by design).

### 6.2 Why this is the hardest comprehension problem in the design

Computed for Θ=100 (this session, same construction as `tools/HybridViability`'s `Build()` —
`Floor = 4167`, `Total = 100_000`, 3 points per Θ, D26 ladder `req(t) = 5·t(t+1)/2`):

| Build | Tier reached, per path | Open traits |
|---|---|---:|
| One aptitude, all-in | **7 · 7 · 7 · 7** in its own stance, 2 in the other eight | 176 |
| Two, same stance | 7 · 7 · 5 · 5, 2 elsewhere | 160 |
| Two, different stances | 5 in all eight of those two stances, 2 in the third | 192 |
| Even twelve | 4 in all twelve | 192 |

**Read row 1.** A player who put every point into Might opens **tier 7 in Fortitude, Vigor and
Onslaught — three paths they have never touched.** Three quarters of what an all-in build can reach
is in trees it did not invest in. That is the design working exactly as D28 intends
([09-crossunlock-sweep.md](09-crossunlock-sweep.md): *"its whole posture comes along for free"*), and
it is completely invisible unless the surface says so.

The reverse is worse. Moving points **out** of Might closes tiers in three other paths at once.
Under D18 respec is a full reset so this cannot happen silently mid-session — but it happens
constantly **inside a draft**, which is where a player does their thinking.

### 6.3 The design — five parts

**1. Name the rule in the fiction, once, where it first matters.** On the first path the player opens
that they have not invested in:

> *Paths of the same stance help each other. Your deepest **Force** path lends its progress to the
> other three.*

GG-45: at the moment of first encounter, in place, once. No engine words — "stance", "lends",
"progress". Never "cross-unlock", never "posture", never "gate".

**2. The tier row shows where its progress came from.** Doc 07 already puts a shared tier ladder down
the middle of the two branches, because D20's *"one investment opens offence and defence together"*
is taught by the layout. Give each tier row an attributed requirement:

```text
── Tier 8 ─────────────────  180 needed · you have 175
     55 from Fortitude · 120 lent by Might
```

That is GG-49 (*a change is attributable*) applied to a **gate** instead of to a stat, and the
grammar already ships: `web/fusion-rpg-web/src/ui/actor/ChannelContributions.tsx:10-35` renders
exactly this shape — a source name and its contribution, per row (FACT). Reuse it rather than
inventing a second attribution component.

**3. Exactly one lender is named, always singular.** The credit is `max`, not a sum, so a second mate
never helps. If the surface renders a total the player will assume compounding and spread inside
their stance — the mistake the red team itself made before the sweep. *"lent by Might"*, one name, is
the O(1) rule made visible.

**4. The locked reason is visible text, and it names both routes.** The repo has already settled this
argument: `web/fusion-rpg-web/src/stages/world/inspector/ActionCluster.tsx:18-27` explicitly rejects
the hover-only floor that `ui/disabledReasonGuard.ts:51-53` accepts, because *"a hover-only reason is
unreachable on touch and invisible to a keyboard user"* (FACT). So:

> *Opens at 180. You have 175 — five more in Fortitude, **or** five more in Might.*

Both routes, because under the largest-mate rule either works and a player shown only one will take
the wrong one. Reasons route through one table, the way
`web/fusion-rpg-web/src/stages/world/inspector/reasonFor.ts:11` already routes world refusals, so no
engine token reaches player text.

**5. The draft preview reports what a change would close.** This is the highest-value line the
preview panel renders, and it is one sentence computed from the draft:

> ⚠ *Moving 30 points out of Might closes tier 8 in Fortitude, Vigor and Onslaught — 4 of your traits
> would stop working.*

It reuses D14's *Not working* red state rather than adding a sixth kind of nothing (the stat sheet
already establishes that zero is four different things — `spec-derived-stat-sheet.md` §3), and it
fires inline in the preview or at band 4, **never** band 3: GG-53's interruption budget is spent on
run-ending results only (`game-gui-principles.md` §20.1 D6).

### 6.4 What this does not solve

A player still cannot see, from inside one path, that *deepening a different stance* would do nothing
for this one. The negative case has no natural home. The mitigation is ordering, not explanation:
Level 1's default order (doc 07 §4.2) should put **your own stance's other three paths second**,
directly after the paths you have invested in — because those are the three that are already open and
the player does not know it. Ordering teaches the rule by putting its consequence in front of them.

---

## 7. The tenth tier

### 7.1 The distance is not a constant, and that is the whole answer

Θ at which each tier opens for a one-aptitude build (share `0.5416` — computed from
`tools/HybridViability/Program.cs:79-93`'s `Floor = 4167` / `Total = 100_000`, at 3 points per Θ,
D26 ladder):

| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Θ | 3 | 9 | 19 | 31 | 46 | 65 | **86** | 111 | 139 | **169** |

That confirms D29's own figures. But it is true only for an all-in build:

| Build shape | Θ for tier 10 |
|---|---:|
| One aptitude | **169** |
| Two aptitudes | **314** |
| Even across twelve | **1,100** |

**So "tier 10 is at Θ 170" is true for one build shape and wrong by 6.5× for another.** No catalog
constant can state it. The tier ladder must render **your** distance, computed from your own
allocation — which the surface can do exactly, because the requirement is a closed formula over
points the client already holds.

### 7.2 Most of what looks locked is not depth

At Θ=100 an all-in build's twelve primary paths hold 480 traits, of which 176 are open. Of the 304
closed ones:

- **48** are deep tiers (8, 9, 10) of the four paths it is actually in.
- **256** are shallow tiers of the eight paths in other stances.

**84% of a focused player's locked surface is other stances, not their own depth.** So the
motivating/discouraging question is aimed slightly wrong: three locked tiers at the bottom of a path
you are already deep in is a small part of what the player sees. The large part is eight paths sitting
at tier 2 — and that is *a choice they made*, which reads very differently from a wall.

### 7.3 How the repo already presents out-of-reach content, and which one to copy

Three shipped presentations, and they say different things (all FACT):

| Presentation | Where | What it communicates |
|---|---|---|
| **A condition** — *"Unlocks when you hold your first item"* | `web/fusion-rpg-web/src/shell/railState.ts:52-60` | This exists and you cannot have it yet. **No distance.** Reads as a wall |
| **A silhouette** — `???`, grayscale icon | `web/fusion-rpg-web/src/features/demons/DemonsPage.tsx:371-379` | This exists, is countable, and its identity is the reward. A collection hook |
| **A distance** — a filled bar against a target | `StatBar`, used at `web/fusion-rpg-web/src/ui/actor/ProgressionTab.tsx:34` | You are *here*, the thing is *there*, and the gap is a number |

**Recommendation: deep tiers get a distance, and never a silhouette.**

- **Show the traits.** A locked tier renders its trait names and effects in full. GG-44 governs
  *menu entries* — *"a menu rail with a fixed entry list cannot support progression"* — not content
  inside a menu the player already opened. Doc 07 made this argument for whole trees (*"an invisible
  tree cannot be planned toward"*); it applies again per tier, and harder, because tier 10 is what a
  build guide is written about.
- **Show the gap as a bar plus a Θ.** *"Tier 9 · 225 needed · you have 175 · about Θ 139 at your
  current shape."* The last clause is what turns three locked tiers from a wall into a destination —
  and it is honest, because it changes when the player's shape changes.
- **Silhouette exactly one thing: a bloodline the player has not discovered.** That is where the
  Codex pattern belongs and it already works there.

**Verdict on the framing question:** three locked tiers are motivating *if they carry a distance* and
discouraging *if they carry a condition*. The repo ships both patterns, so this is a choice, not a
constraint.

---

## 8. The learnability contract at the new scale

Doc 07's L1–L7 stand, with L7 split as §2.1 describes. Four rules are added, each because a specific
decision made a specific promise unkeepable.

| # | Guarantee, in the player's words | Mechanism | Because |
|---|---|---|---|
| **L1–L6** | unchanged from doc 07 | unchanged | — |
| **L7a** | *"Every path I learn works the same on every creature I own."* | Shared catalog, per-actor allocation (D21) | unchanged |
| **L7b** | *"Each creature also has one path only its kind has."* | D23/D30 | Split out because L7 as written is false for bloodlines |
| **L8** | *"A build guide tells me **which** traits and **how many**. What they cost is mine."* | D25 prices per actor; a shared plan carries `(treeId, nodeId, points, soulLevel)` and is priced on import | D25 makes price personal; a guide printing a price is wrong for every reader |
| **L9** | *"Wherever a path tells me it is locked, it tells me what would open it — including the other path that is already helping."* | §6.3's attributed tier row and two-route reason sentence | D28 makes availability non-local, and a gate that does not name its lender is unreadable |
| **L10** | *"A creature's own path lives with that creature. I never have to go looking through eight hundred of them."* | §3 — bloodline at level 0b, Codex for reference, never in a browse | D30 puts 94% of the corpus behind this rule |
| **L11** | *"When something is out of reach, the game tells me how far — not just that it is."* | §7.3 — distance, not condition, on every locked tier | D29's three extra tiers are the first content most players will see locked for a long time |

**And L2 gets a cost note rather than an amendment.** *"A rebalance may change a magnitude, never an
identity"* is unchanged as a rule. Its enforcement surface is now 841 separately generated artifacts
plus one shared plan, and `AptitudeAllocation.Single` throws on an unknown id
(`src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs:36-39`, FACT) — which the ideal already
flags as making an actor unloadable rather than red (§11.2). At 25,949 ids that is not a tail risk;
it is the normal consequence of regenerating one species.

---

## 9. The honest verdict, with the arithmetic shown

**Is ~25,900 nodes learnable under any surface design? Yes — because a player meaningfully sees a
small fraction, and the fractions are not close to each other.**

```text
Whole corpus                                        25,949   100%
  shared, guide-able, the thing to learn             1,560     6.0%
  species bloodlines, one per demon                 24,389    94.0%

A player who owns 30 demons
  shared corpus (learn once, forever)                1,560     6.0%
  their 30 bloodlines (read once each, on binding)     870     3.4%
  ─ reachable reading surface                        2,430     9.4%

That player's commander at Θ=100, all-in on one aptitude
  traits OPEN across the twelve primary paths          176     0.68%
  traits actually OWNED (D25, slope 1.0 … 0.1)      13 … 36    0.05 … 0.14%
  paths they read closely                              4 of 39
```

**The three sentences that matter:**

1. **The learning target is 1,560, not 25,949.** Everything a player can plan with, share, or read a
   guide about is in the shared corpus. The other 94% is content they receive one demon at a time.
2. **Even 1,560 is met 176 traits at a time.** A player at Θ=100 has four paths open past tier 2. The
   surface's entire job at level 0 and level 1 is getting them to those four.
3. **Ownership is a rounding error, and D25 makes it one deliberately.** Thirteen to thirty-six
   traits out of twenty-six thousand. That is fine — it is *scarcity*, which doc 07 already
   identified as good for learnability — but it must never be presented as *"here is the catalog."*

**Where it would fail.** One arrangement makes this unlearnable, and it is the obvious one: putting
all 880 trees in one browse. That crosses the search-first threshold, turns the map into a query, and
GG-25 rejects it by name. Everything else in this document is downstream of not doing that.

---

## 10. Open items

**Answerable by the owner:**

1. **§1.1's shape conflict.** D29 says every tree is ~40 nodes; D30 says a species tree is 29; D10
   says the shape is the same everywhere. Which of the three moves? The corpus is 25,949 or 35,200
   depending on the answer, and every arithmetic in this document is footnoted to it.
2. **D25's slope.** §5.1 sweeps `d` from 1.0 to 0.1 and it changes owned traits at Θ=100 by 2.8×. It
   is a `data/tuning/` number, not a decision that blocks a spec — but the surface's *"you can afford
   about N more"* readout is only honest once it exists.
3. **A one-line summary per species tree** (§3.3). 841 sentences the generator must emit so the Codex
   can answer *"which demon should I bind?"* without 29 node descriptions each. Cheap if booked into
   the D30 pipeline now, expensive as a second pass over 841 artifacts.
4. **Naming.** This document kept doc 07's *paths* / *traits* / *Focus* / *Plan* and adds
   **bloodline** for a species tree and **stance** for a posture. Both are owner calls; both need
   deciding before any player text is written.

**Already decided elsewhere, listed so nobody re-opens them:** the read-only reference home is the
Codex for bloodlines and the Almanac for shared paths (GG-9's *link into, do not re-implement*);
respec stays a full reset (D18); the tab is the actor sheet's, not a new route (doc 07 §4.1).

**Defects found while reading, not proposals:**

| File | What is wrong |
|---|---|
| `web/fusion-rpg-web/src/features/demons/DemonsPage.tsx:367-388` | The Codex grid maps the entire species catalog with no volume strategy. 841 entries against a 240 search-first threshold — a live GG-50 violation, unrelated to passive trees |
| `docs/research/passive-tree/07-learnability-and-surface.md` §4.2/§4.3 | *"One tree's 29 nodes is render-all"* — `RENDER_ALL_MAX` is 24, and a lattice is a GG-61 surface, not a GG-50 one. §2.3 |
| `web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx:12` | Still says *"this game doesn't have PoE's content scale to justify one"*. Doc 07 already flagged it; the gap is now 20× rather than 1.1× |

---

## 11. Design-gate checklist

```
[x] I identified the subsystem(s) this touches - passive trees, player UI,
    derived stats / units, demon species content, standalone web.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md (whole), architecture/game-gui-principles.md (all 61 rules,
    §16-§21), design/information-architecture.md (whole), architecture/
    fe-game-foundation.md §3/§5/§6, design/spec-magnitude-and-units.md §1-§3.2,
    design/spec-derived-stat-sheet.md §1-§4, architecture/passive-tree-ideal.md
    (whole, D1-D32), research/passive-tree/07 and 09.
[x] I checked the decision record carried in the ideal. There is still no
    passive-tree row in decisions.md; the ideal is idea phase, no build authorized.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments - and corrected one of doc 07's
    own claims (RENDER_ALL_MAX = 24 makes a 29- or 40-cell lattice not
    "render-all"), found one live volume defect (DemonsPage.tsx:367), and read
    the largest-mate rule out of tools/HybridViability rather than the prose.
[x] I read the surrounding section of every rule I quoted - GG-61's own "This is
    not GG-50" paragraph and GG-44's menu-entry scope are both load-bearing here.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite
    was run. Every number is a read of source/data or a script over it. The tier
    and D25 tables were computed this session from the shipped Floor/Total/ladder
    constants, not from the sweep's own output - the sweep was read, not re-run.
[x] Nothing contradicts a §2 invariant. No cap is proposed (§4 rule 1 rejects a
    slider for exactly this reason). No new power-shaped scale. No fourteenth
    unit class - §5.2 checks the union and finds nothing must grow.
[~] Corrections propagated. PARTIAL: §10 names three files to fix; this is a
    research document and does not edit them.
```
