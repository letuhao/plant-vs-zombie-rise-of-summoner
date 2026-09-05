# Spec: `tree-surface` — the player surface

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-surface` · **Wave 3** · **Depends on:** `tree-state`, `tree-catalog` ·
**Reads:** `tree-resolve`'s `TreeResolveReport` · **Blocks:** nothing

**Built on, not re-derived:**
[14-learnability-at-scale.md](../../research/passive-tree/14-learnability-at-scale.md), which
re-checked [07-learnability-and-surface.md](../../research/passive-tree/07-learnability-and-surface.md)
against D24/D25/D28/D29/D30 and found **nine of doc 07's twelve calls hold, two need amending, one is
wrong.** This spec adopts that verdict. Where doc 14 says doc 07 survives, this spec stops.

---

## 1. Objective

Make **35,160** authored nodes learnable, plannable and spendable — on a surface the player already
opens, in words a player already uses.

**It is learnable because the number a player has to learn is 1,560**, not 35,160:

| Quantity | Nodes | Share |
|---|---:|---:|
| Whole corpus (39 × 40 shared + 840 species × 40) | **35,160** | 100% |
| **The shared corpus — the only part a build guide can be written about** | **1,560** | **4.4%** |
| A 30-demon player's whole reachable reading surface | **2,760** | 7.9% |
| Traits *open* to a one-aptitude build at Θ=100 across the twelve primary paths | 176 | 0.50% |
| Traits that build actually **owns** at Θ=100 under D25 | 13–36 | **0.04–0.10%** |

*(the Θ=100 rows are computed in
[14](../../research/passive-tree/14-learnability-at-scale.md) §9 from the shipped
`Floor`/`Total`/ladder constants; the corpus rows are the 2026-09-05 verified count — **840 species ·
40 nodes per tree everywhere including species · 879 trees**. Doc 14's own totals were written against
841 species and 29 nodes per species tree and are superseded. The share column is recomputed, not
carried over: every one of these percentages fell, because the denominator grew by 36% while the
learnable numerator did not move at all. **That is the finding, not a correction** — the shared corpus
is now a smaller fraction of the whole than doc 14 measured, which strengthens §1's argument rather
than weakening it.)*

The last row is the honest one and it sets the module's job. **The surface's job is never "show the
catalog."** It is *"help this player find the thirteen that are theirs, out of a hundred and
seventy-six that are open, inside four paths out of thirty-nine."*

**Success is measurable:** a player can lay out a whole build without spending anything, can see why
every locked thing is locked and how far away it is, and can read every number in a unit class that
already exists.

---

## 2. The information architecture

### 2.1 It is the Passives tab of the actor sheet. It is not a new route.

**GG-1 is the rule and the codebase already refuses the alternative.** A new top-level route is
GG-1's core failure mode (`game-gui-principles.md:36-51`); `AptitudesLayer.tsx:5` records the same
refusal in code (*"a layer, never a route"*). A rail entry plus a layer is also wrong under D21 —
trees are per-actor, so a rail-level layer would open and then ask *which actor*, a picker in front of
the content, which the project already walked back once for aptitudes.

**The slot exists and is a locked placeholder — verified this session:**

- `web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx:16` declares the tab union including `"passives"`;
  `:19-26` lists six tabs with `{ id: "passives", label: "Passives", testId: "actor-sheet-tab-passives" }`.
- `:28-35`'s doc comment: *"band 2, opens over any stage (GG-9: the one canonical actor surface)"*,
  and *"Progression/Derived Stats/Actions/Passives/Gear are each a later module's own tab body."*
  This module is that later module.
- `web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx:13-21` renders four `LockedGridSlot`s with the
  reason *"Passive skills are a reserved sub-feature, no target date yet."*

**So a per-actor tree costs zero new navigation**, which is the whole point under D21. Depth from a
stage is sheet (1) → path (2) → trait (3) — GG-10's budget of three pushes, not over it.

> **`PassivesTab.tsx:12`'s own comment is now false and this module owns correcting it.** It reads
> *"a flat locked list, not a node-graph tree (this game doesn't have PoE's content scale to justify
> one)."* True when written; the shared corpus alone is now above PoE's ~1,300, and the whole corpus
> is twenty times it. **A comment is not evidence** (DESIGN-GATE §3.2), and this is the example.

### 2.2 The four levels, plus one

```text
Actor sheet (band 2, already exists) → Passives tab
├── Level 0   Yours       paths this actor has invested in · Focus · not-working count · unspent
├── Level 0b  Bloodline   THIS creature's own path — pinned, never in a browse
├── Level 1   All paths   the 39 shared paths, ordered, searchable
├── Level 2   One path    2 branches × 10 tiers, 40 traits, one fixed lattice — species too
└── Level 3   One trait   value · what it costs next · depth · exclusion print · where it goes
```

Levels 0 and 1 are **tabs inside the Passives tab**, not pushes. Level 2 and 3 are the two pushes.

**Level 0 — Yours.** Opens first, every time. The paths this actor has put anything into (typically
1–4), the **Focus** line (§6), a count of any traits that have stopped working (§7), and what is
unspent. The empty state is content, not an edge case (GG-17): a new actor sees *"You have 6 points.
Pick a path."* and one affordance — never thirty-nine cards.

**Level 1 — All paths.** The 39 shared paths. **Ordering matters more than search**, because 35 of
the 39 are irrelevant to any given build. Default order:

1. paths you have invested in,
2. **your own stance's other three paths** — because under D28 those are already open and the player
   does not know it (§5.4),
3. paths matching this creature's element and status,
4. everything else,
5. **paths whose gate quantity does not exist yet**, collapsed behind one row (§9.1). Today that is 27
   of the 39, which is why the bucket is a rule rather than an edge case.

Then search and category filters. **Categories are five** — `primary | elemental | status | family |
species` (R7) — and species never enters this browse (§3), so the filter offers four here. Query state belongs to the layer and survives closing it (GG-51).

**Level 2 — one path.** A fixed 2 × 10 lattice, both branches sharing one tier ladder down the middle
— because D26's rule is that **one investment opens offence and defence together**, and a layout that
shows the shared ladder teaches that without a sentence of tutorial (GG-45).

> **Do not reach for a graph library.** A 2 × 10 lattice is a CSS grid. The world stage already made
> this call and enforces it with `stages/world/xyflowGuard.test.ts`, and GG-38 names a graph library
> in the entry chunk as a live weight defect. `LockedGridSlot.tsx` is already the cell.

**Level 3 — one trait.** The value in its unit class, what the next depth costs, the compose sentence
if sources do not simply add, the exclusion print, and where the number goes.

### 2.3 The tree, not the trait, is the unit of browsing — and doc 14 corrected the rule

The shipped volume rule, verified at
`web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx:21-22`:

```ts
const RENDER_ALL_MAX = 24;
const SEARCH_FIRST_ABOVE = 240;
```

| Surface | Count | Tier |
|---|---:|---|
| All paths (shared only) | 39 | **windowed** (25–240) |
| All paths **+ species in one browse** | **879** | search-first — a search box over a bag of trees, the "database viewer" GG-25 rejects. **§3 refuses this arrangement** |
| Traits flattened across paths | **35,160** | never rendered — traits exist only inside a path |
| **One path's lattice** | **40** | **GG-61, not GG-50** |

**Doc 07 called a single path "render-all" and that was wrong** — 40 > 24, and 29 was already over.
**The error was applying the wrong rule, and the fix is a rule swap, not a redesign.** GG-61 says so
in its own text (`game-gui-principles.md:788-792`): *"This is not GG-50. GG-50 is about many
entities… This rule is about one entity's own content — one actor's 99-channel derived-stat sheet."*
A path's 40 traits are **one entity's own content**, bounded at 40 by D29, laid out as a fixed
lattice.

`PanelShell` already does exactly what GG-61 asks: a bounded height at
`web/fusion-rpg-web/src/shell/PanelShell.tsx:86` (`max-h-[min(720px,82vh)]`) with an internally
scrolling body at `:96-99` (`min-h-0 flex-1 overflow-y-auto`).

**What this costs, concretely.** Ten tier rows will not fit in the panel body at the 1280×720 floor,
so the tier ladder scrolls — and **the tier ladder is what the player navigates by**. Therefore:
**Level 2 opens scrolled to the player's own depth, never to tier 1.** That is an acceptance
criterion, not a nicety, and it needs a GG-61 volume fixture at the 720px floor.

---

## 3. Species trees are level 0b

**A species tree is the creature's own content, reached two ways, and it is never in a browse.**

1. **To spend:** that creature's actor sheet → Passives tab → its bloodline pinned above the shared
   paths. One extra card on a surface the player already opens for that creature. Zero new
   navigation.
2. **To read:** the Demon Codex entry for that species, read-only. GG-9 permits exactly this — other
   surfaces *link into* the canonical one rather than re-implementing it.

**The Codex already ships the states this needs**, verified at
`web/fusion-rpg-web/src/features/demons/DemonsPage.tsx:365-390`: a `discovered`/`seen` map, a name or
`???`, and a grayscale silhouette (`opacity-30 grayscale`) for anything neither. A discovered species
shows its bloodline; an undiscovered one keeps the silhouette it already has.

**Why this is not a dodge — the strongest reason first.**

> **A species tree is not a choice, so it needs no chooser.**

Every other browse in the game exists because the player picks from it. A player cannot pick a
bloodline — it is a property of the creature they bound. There is no build-planning reason to put 840
of them side by side, because **no decision is taken by comparing them.** D23's own framing is *"you
give up build freedom and receive something unobtainable elsewhere."*

Two supporting reasons: keeping them out is what keeps the browse in the windowed tier (39 vs 879,
against a 240 threshold); and the one real cross-species question — *"which creature should I bind
next?"* — is a **collection** question the Codex already answers at its own resolution, given one
sentence per bloodline saying what it is *for*. 840 sentences, reviewable in an afternoon, and they
are the D30 pipeline's to emit.

**L7 splits into two promises, and both get printed:**

> *"Every path you learn works the same on every creature you own."*
> *"On top of that, each creature has one path only its kind has."*

That is not a weakening. It is the collection hook D23 was decided for, stated honestly.

> ⛔ **A live GG-50 defect sits on the surface this would hang off.**
> `DemonsPage.tsx:367-388` maps the **entire** species catalog into a grid with no volume strategy —
> `(catalog.data?.species ?? []).map(...)`. At 840 species that is 840 DOM subtrees against a
> search-first threshold of 240. It is a violation today, independent of passive trees. **It is fixed
> before a bloodline reference is added to it** — §12 lists it as ask-first, because it is another
> program's file.

---

## 4. The two-track trait (D3) — one cell, one verb

40 traits × 2 tracks is 80 interactions per path. **Rendering both on the cell turns a lattice into a
form**, which is the failure to avoid.

The two tracks have different cadences, and that is what decides where each belongs:

| | Unlock | Deepen |
|---|---|---|
| Currency | Skill points | Souls |
| Bounded | Yes, by D25's rising price | **No** — souls are uncapped by PS-8 |
| How often | 13–36 times at Θ=100 | Unbounded, repeated |
| Reversible | Only by a full respec (D18) | Only by a full respec (D18) |

**A rare, structural, one-shot act belongs on the map. A repeated, unbounded act with a rising price
does not** — it needs a repeat control and a running total, and neither fits in a lattice cell.

**Level 2 — the cell carries exactly one verb.** Three states:

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
- `Depth 6` is a count and renders through the existing `count` class. **No new unit class.**
- The locked cell's reason is visible sibling text on the **tier row**, not repeated on forty cells
  (§5.3).

**Level 3 — the detail is where the deepen track lives:**

> **Deep Roots** — earth power **+260**
> Depth **6**. Next depth **+43 earth power**, costs **1,900 souls**.
> `[ − ]` `[ + ]` `[ +10 ]` → planned depth **9** · **6,900 souls**

Three rules on that control, each with a reason:

1. **A stepper, never a slider.** A slider needs a maximum; PS-8 forbids one. Souls are unbounded by
   design.
2. **Never a raw number input under a raw-id label.** `AptitudesPage.tsx:64-65` renders
   `<Field label={id}><NumberInput …/></Field>` and `ProgressionTab.tsx:107` does the same — a live
   GG-23/GG-24 defect. **A 40-trait grid must not inherit it forty times.**
3. **The step edits the draft, not the server.** Commit stays one whole-allocation POST, the shape
   `AptitudeEndpoints.cs:32-57` already ships and the shape D18's full respec needs anyway.

**So the interaction count per path is 40 map decisions plus one detail per trait you actually own —
not 80.** That is the whole point of splitting by cadence.

### 4.1 Three currencies. The surface names each one, every time.

**Ruling R1: `req(t)` is a threshold on APTITUDE POINTS allocated to this path's gate quantity.**
Traits are *bought* with skill points; tiers are *opened* by aptitude points. They are different
wallets, and the surface is where that either gets learned or gets misunderstood.

| | Opens a tier | Buys a trait | Deepens a trait |
|---|---|---|---|
| Currency | **Aptitude points** in this path's gate quantity | **Skill points** | **Souls** |
| Where it is spent | The Aptitudes tab — **not on this surface** | Here, on the cell | Here, in the trait detail |
| Where this surface shows it | The tier row (§7.2) | The cell's one verb (§4) | The stepper (§4) |
| Can gear feed it? | **No** | Yes (D11) | No |

**Why this is load-bearing and not pedantry.** A player who reads *"Tier 8 · 180 needed · you have
175"* two centimetres above *"Unlock · 14 pts"* will assume one wallet. They will then spend skill
points expecting a tier to open, and **nothing will happen and nothing will say why** — the gate is
reading a different number entirely. The two must never share the bare word *points*:

> **Tier 8** — opens at **180 aptitude points**. You have 175.
> **Deep Roots** — Unlock · **14 skill points**.

**The gear rule falls straight out of it, and it is the reason §8 has two red sentences instead of
one.** An aptitude is a *source*, not a registered channel, so an item cannot write one — D12 holds by
construction rather than by enforcement. D11's gear-granted points are **skill** points. So removing
gear can invalidate a *purchase* (*"needs 3 more points; the gear that gave them is off"*) and can
never close a *tier*. Two different failures, two different sentences, and neither is comprehensible
until the currencies are named.

*(Both names are working vocabulary — see §17 open question 1.)*

---

## 5. Plan before spend

### 5.1 Why planning is worth building, in two decisions

**D18** makes respec a full reset priced in souls, so a wrong build is expensive to undo. **D25**
makes unlock cost rise with the number of traits you already own, so a wrong *order* is expensive
too. The cheapest fix for both is to **make the wrong build free to discover.**

The draft → dirty → commit flow already ships for primary stats — `AptitudesPage.tsx:18-19` holds the
draft, `:36` computes `dirty` against the server state. Three additions:

1. **Revert** — restore the draft to what is committed. Named, not implicit.
2. **A preview panel** reading the draft, not the server: what the build grants, what Focus would
   become, and which traits would stop working.
3. **A Plan can outlive the panel** — save a draft under a name without committing it.

Player word: **Plan**. Not "template", not "preset", not "loadout" — the codebase already reserves
that last one for a squad picker (`diffStateMatrix.test.ts:22-26`).

**A draft does not violate GG-15.** GG-15 forbids painting *authority* early, not showing a preview.
The draft is labelled as uncommitted; the committed number is what the server returned. The shipped
page already draws that line.

### 5.2 The price of a PLAN, not of a trait in isolation

D25's price depends on **how many** traits you own, not **which**, so the cost of a set of *m* traits
is `Σ c(k)` from `owned+1` to `owned+m` — **independent of the order you take them in.** That is
worth a printed promise:

> *"A plan costs the same whichever order you follow it in."*

If the slope is ever made non-uniform per trait, that sentence stops being true and the Plan object
stops being trustworthy.

**The Plan renders three numbers, not one:**

> **Ashroot Line** · 9 traits
> **112 skill points** and **48,000 souls** from where you are now
> Next: **Deep Roots** — 14 skill points
> *Every trait you own makes the next one cost more.*

- **Total** is from *this actor's* current state, so the same plan prices differently on a fresh
  creature and a deep commander — which is correct and must be visible, since D21 makes
  plan-to-many-actors the normal case.
- **Next** is what lets the player start without arithmetic.
- **The rule sentence appears once**, at the top of the tab, first time only (GG-45).

**D25 is not a new player concept.** `ContractPolicy.NextSlotPrice` is already an arithmetic price in
the count you own, and the FE already renders it in player words — `· next 900 Souls`,
`contractView.ts:50-54`. **Souls render as composed sentences, not through `formatMagnitude`, so no
unit class has to grow.**

### 5.3 The rule this forces on shared builds

The catalog is identical for every player (D24), so a build is completely described by
`<catalog version> + [(pathId, traitId, points, depth)…]`. But **D25 makes the price personal.**
Therefore:

> **A shared plan may name traits and how many. It may never name a price.**

A guide that prints *"this costs 112 points"* is wrong for every reader whose actor owns a different
number of traits. The share code carries *what*, never *how much*; any imported plan is **priced
against the importing actor, on arrival, and says so.**

The URL grammar already carries a plan: GG-8 makes the address *stage + open layers*, so
`#/sanctum?panel=creatures&sel=<actor>&plan=<code>` cold-loads the stage, opens the sheet, and loads
the plan **as a draft** — never committed. *"A URL never means throw away what you were doing."*

---

## 6. Focus — making the rule you are scored on visible

**L6: a player scored on a rule they cannot see will not believe the game is fair.** `F` is that rule
— computed by `tree-resolve` from `concentration.fmaxMilli` and `concentration.wMilli` in
`data/tuning/passive-tree.v1.json`, both **per-mille** (R2; `1200` and `500`, and `1000` is a legal
value for the first because D5 is provisional). The surface renders what that module returns and never
re-derives it, so a dial change moves the line with no FE edit. The readout is not an analogy — `H = Σ(shareᵢ)²` is a Herfindahl index and `1/H` is its standard
reading, the **effective number** of paths. Exact, not a metaphor.

| Commitment | `H` | `1/H` | `F` at `concentration.fmaxMilli = 1200` |
|---|---:|---:|---:|
| all in one | 1.000 | 1.0 | ×1.200 |
| 70 / 30 | 0.580 | 1.7 | ×1.116 |
| two, even | 0.500 | 2.0 | ×1.100 |
| twelve, even | 0.083 | 12.0 | ×1.017 |

**One line, on Level 0 and in the plan preview:**

> **Focus** — your commitment sits across about **2 paths**. Path bonuses ×1.10.

**And it moves while you edit.** That is what turns a formula into a felt rule — the player moves
points between two paths and watches both halves of the line move together. The motion vocabulary
already declares **M8** for exactly this (`information-architecture.md` §10: *"a number the player
caused changes"*), and GG-33 asks for it.

**It also appears on any path-derived number the player reads**, as the reason that number is what it
is. Otherwise `F` is a hidden coefficient and the contribution list will not add up — the same defect
`spec-derived-stat-sheet.md` §4 flags for `FlatReplace`.

**No new unit class.** Verified against the shipped union at
`web/fusion-rpg-web/src/contract/types.ts:33-55`, which holds **thirteen** and none of them has to
grow:

- `F` is a multiplier and renders as `perMilleRatio` with `op: "absolute"` — the shape added
  2026-09-04 for a field whose neutral baseline is 1000, rendered `×1.10`. It already exists.
- The **effective path count is fractional** and fits no class. **It renders as prose — *"about 2
  paths"* — which is not a `Magnitude` and needs no class.** DESIGN-GATE §1's *Stats* row names
  inventing a third classification as a known past failure; this spec does not invent one.

Everything else routes through `formatMagnitude`, which **refuses a bare number by construction**
(`web/fusion-rpg-web/src/i18n/magnitude.ts:15` — *"No overload accepts a bare `number`"*). GG-46.

---

## 7. D28 — a gate that depends on a different path

### 7.1 Why this is the hardest comprehension problem in the design

Computed at Θ=100 in [14](../../research/passive-tree/14-learnability-at-scale.md) §6.2: a player who
put every point into Might opens **tier 7 in Fortitude, Vigor and Onslaught — three paths they have
never touched.** Three quarters of what an all-in build can reach is in paths it did not invest in.

That is D28 working exactly as intended (*"its whole posture comes along for free"*), and it is
**completely invisible unless the surface says so.** The reverse is worse: moving points *out* of
Might closes tiers in three other paths at once. Under D18 that cannot happen silently mid-session,
but it happens constantly **inside a draft**, which is where a player does their thinking.

**L6 now guards two rules, not one, and D28's failure mode is worse than `F`'s.** `F` changes how big
a number is — a wrong number is an argument. D28 changes **whether a trait exists for you at all** —
a trait open for no visible reason is a bug report.

### 7.2 Five parts

**1. Name the rule in the fiction, once, where it first matters** (GG-45) — on the first path the
player opens that they have not invested in:

> *Paths of the same stance help each other. Your deepest **Force** path lends its progress to the
> other three.*

No engine words. Never "cross-unlock", never "posture", never "gate".

**2. The tier row shows where its progress came from.**

```text
── Tier 8 ────  opens at 180 aptitude points · you have 175
     55 from Fortitude · 120 lent by Might
```

**The unit is in the row, not in a legend.** Per §4.1 the gate reads aptitude points and the cells
below it read skill points, and a row that says only *"180 needed"* is the exact sentence that makes
a player spend the wrong wallet.

That is GG-49 (*a change is attributable*) applied to a **gate** instead of a stat, and the grammar
already ships: `web/fusion-rpg-web/src/ui/actor/ChannelContributions.tsx:10-35` renders exactly this
shape — a source name and its contribution, per row. **Reuse it rather than inventing a second
attribution component.**

**3. Exactly one lender is named, always singular.** The credit is `max`, not a sum, so a second mate
never helps. **If the surface renders a total the player will assume compounding and spread inside
their stance** — the mistake the red team itself made before the sweep. *"lent by Might"*, one name,
is the `O(1)` rule made visible.

**4. The locked reason is visible text, and it names both routes.**

> *Opens at 180 aptitude points. You have 175 — five more in Fortitude, **or** five more in Might.*

Both routes, because under the largest-mate rule either works and a player shown only one will take
the wrong one. The repo has already settled the hover argument:
`web/fusion-rpg-web/src/stages/world/inspector/ActionCluster.tsx:18-29` explicitly rejects the
hover-only floor that `ui/disabledReasonGuard.ts` accepts, because *"a hover-only reason is
unreachable on touch and invisible to a keyboard user."* Reasons route through **one table**, the way
`stages/world/inspector/reasonFor.ts:11` already routes world refusals, so no engine token reaches
player text.

**5. The draft preview reports what a change would close** — the highest-value line the preview
renders, one sentence computed from the draft:

> ⚠ *Moving 30 points out of Might closes tier 8 in Fortitude, Vigor and Onslaught — 4 of your traits
> would stop working.*

It reuses §8's *Not working* state rather than adding a sixth kind of nothing, and it fires **inline
in the preview or at band 4, never band 3** — GG-53's interruption budget is spent on run-ending
results only (`game-gui-principles.md` §20.1 D6).

### 7.3 What this does not solve

A player still cannot see, from inside one path, that deepening a **different stance** would do
nothing for this one. The negative case has no natural home. **The mitigation is ordering, not
explanation** — §2.2's Level 1 order puts your own stance's other three paths second, because those
are the three that are already open and the player does not know it. Ordering teaches the rule by
putting its consequence in front of them.

---

## 8. Printed exclusions (D14)

**D14 is a printed runtime no-op, not an allocation block.** The trait stays allocatable and simply
stops working; both sides print the rule and both name the same winner. Target rarity ~2% of nodes.

**Before you spend — printed on both traits, always, whether or not it is currently firing:**

> **Ashen Root** · +40 fire power
> *Does nothing while your damage is converted away from fire. If both are taken, Ashen Root is the
> one that stops.*

Note the shape: it names a **property** (*converted away from fire*), not a trait. That is D14's
`O(1)` rule made visible — the sentence stays true for conversion traits that do not exist yet.

**Once it has stopped — a distinct state, not a dimmed one.** The derived-stat sheet already
establishes that *zero is four different things* (`spec-derived-stat-sheet.md` §3, six states), so
this fifth kind of nothing must look unlike the others:

- a red border **and** the word **Not working** **and** a distinct fill — **never colour alone**
  (GG-27),
- the winner named inline: *"switched off by Emberflow"*,
- the trait keeps its marks. **Nothing is refunded and nothing is silently repaired.**

The same state serves D11's other case — gear that granted points is removed, and the traits those
points held become invalid. Same red, different sentence: *"needs 3 more points; the gear that gave
them is off."*

**Finding it without opening thirty-nine paths.** This is the part a naive design gets wrong:

1. **A count on Level 0**, always visible when non-zero — *"2 of your traits are not working."*
   Clicking it filters to exactly those.
2. **A toast at the moment it happens** (GG-16): allocating the trait that switches another off
   reports immediately, naming both. An outcome the player caused is never silent.
3. **Never a modal.** Band 4, per GG-53.

> **The property vocabulary D14 keys on is thin today** — atom tags are free-form JSON
> (`AtomRow.cs:40`) and the corpus carries three semantic values, so a predicate can key on stance and
> little else. That is a **wiring/content gap, not a wall**: the surface's job is to render whatever
> predicate the catalog carries, and it renders correctly at three properties or three hundred.

---

## 9. The tenth tier — distance, never a condition

**Most of what looks locked is not depth.** At Θ=100 an all-in build's twelve primary paths hold 480
traits, 176 open. Of the 304 closed, **48** are deep tiers of the four paths it is actually in and
**256** are shallow tiers of the eight paths in other stances. **84% of a focused player's locked
surface is other stances, not their own depth** — and that is *a choice they made*, which reads very
differently from a wall.

The repo ships three presentations of out-of-reach content, and they say different things:

| Presentation | Where | Communicates |
|---|---|---|
| A **condition** — *"Unlocks when you hold your first item"* | `shell/railState.ts:52-60` | It exists, you cannot have it. **No distance.** Reads as a wall |
| A **silhouette** — `???`, grayscale | `features/demons/DemonsPage.tsx:371-379` | It exists, is countable, and its identity is the reward |
| A **distance** — a filled bar against a target | `StatBar`, used at `ui/actor/ProgressionTab.tsx:34` | You are *here*, it is *there*, and the gap is a number |

**Deep tiers get a distance, and never a silhouette.**

- **Show the traits.** A locked tier renders its trait names and effects in full. GG-44 governs
  *menu entries* — *"a menu rail with a fixed entry list cannot support progression"* — not content
  inside a menu the player already opened. An invisible tier cannot be planned toward, and tier 10 is
  what a build guide is written about.
- **Show the gap as a bar plus a `Θ`.** *"Tier 9 · 225 aptitude points · you have 175 · about Θ 139
  at your current shape."* The last clause turns three locked tiers from a wall into a destination, and it is
  honest because it changes when the player's shape changes.
- **The distance is not a catalog constant.** Tier 10 opens at Θ≈169 for a one-aptitude build,
  Θ≈314 for two and Θ≈1,100 spread evenly — **wrong by 6.5× if stated once.** It must render *your*
  distance, from `TreeResolveReport`, which the client can do exactly because the requirement is a
  closed formula over points it already holds.
- **Silhouette exactly one thing:** a bloodline the player has not discovered. The Codex pattern
  already works there.

### 9.1 A path whose gate quantity does not exist yet — **answered, not deferred**

**The state, verified in code this session.** A tier gate reads a *gate quantity*. Of the 39 shared
paths, **27 gate on a quantity with no producer in `src/`**:

| Category | Paths | Gate quantity | State |
|---|---:|---|---|
| Primary | 12 | aptitude points, `Commander` scope | ✅ shipped and wired |
| Elemental | 6 | `element_mastery` | ⛔ comments only — `PointBudget.cs:15` says outright it *"is owned by the demon program's `aspect-scope` module and does not exist yet"* |
| Status | 21 | `status_applied.<id>` | ⛔ **zero hits in `src/`** — D35 removed the `AllocationScope` dependency and, with it, the only place the counter was going to live |

`tree-resolve` §3.3 resolves those paths to zero aptitude points, therefore tier 0, therefore no
contribution — *"inert, not broken"*, and arithmetically it is fine. **On a player surface it is the
single worst thing this module can render: 1,080 of the 1,560 shared traits, 69% of the corpus, at
tier 0 for every player, forever.**

**Applied naively, §9's own rule causes the damage.** *"Give an out-of-reach thing a distance"*
produces *"Tier 1 · 5 aptitude points · you have 0"* on 1,080 traits. That is a gap the player cannot
close, printed in the exact grammar the surface uses for gaps they can. It reads as content they
failed to unlock — the one reading §9 exists to prevent.

> **The answer: this is the one case that takes the CONDITION presentation, and it is the reason the
> repo ships three and not two.**

§9's table names three shipped presentations of out-of-reach content. A deep tier gets a **distance**;
an undiscovered bloodline gets a **silhouette**; and the third — the **condition**
(`shell/railState.ts:52-60`, *"Unlocks when you hold your first item"*) — was rejected two paragraphs
above because it carries *"no distance, reads as a wall."* **That objection is exactly right for a
deep tier and exactly wrong here.** A condition is honest precisely when the thing that would close
the gap does not exist, because then there is no gap to state. The fabricated distance is the lie; the
condition is the truth, and it is already the grammar this codebase uses for *"the world has not grown
this yet."*

Five rules, each with its reason:

**1. The condition names the world, not the player.** No requirement number, no have-number, no verb.

> **Ashen Path** — *nothing in the world teaches this yet.*

Not *"locked"* (the player did not fail), not *"you need…"* (there is nothing to need), and **not
*"coming soon"*** — that is a delivery promise this module cannot keep, and the sentence has to stay
true whether the gate lands next month or never.

**2. The lattice still opens, and still shows its traits.** §9's *show the traits* rule holds here for
exactly the reason it was written: content nobody can see cannot be planned toward, and this content
is authored, committed and byte-identical for every player (D24). What changes is what each row and
cell renders:

- the tier row shows the condition **instead of** a requirement and a have-number,
- **no cell shows a price or an Unlock verb.** A price is an offer, and offering a purchase that
  cannot complete is worse than the wall — it is a wall the player is invited to walk into,
- name, effect and unit class render exactly as anywhere else.

**3. It sorts last and it collapses.** §2.2's Level 1 ordering gains a fifth bucket, after
*everything else*, holding every such path behind one row:

> **27 paths aren't open to anyone yet** — *expand*

At this scale ordering is the mitigation that actually works (the same argument §7.3 makes): a
player's first screen is the 12 paths that function, and 27 inert cards never sit between them.

**4. It is counted in nothing.** Never in the *not working* count (§8) — that state means a trait that
**was** working and stopped, and folding a content gap into it would make a real regression
unfindable. Never in a locked-trait total, never in the Focus denominator, never on Level 0. An actor
with nothing invested in these paths has nothing here to be told about.

**5. The surface reads a field, never a list of 27 ids.** `tree-resolve` §3.3 already owes this and
names §9.1 as the renderer; this is the requirement stated from the consuming side.
`TreeResolveReport` carries, per path, a **`gateState`** of `wired | unproduced`, **read from the
catalog and never inferred from a zero**. Inferring it would put a gate that genuinely broke into the
same visual state as a known content gap, and this surface would then be the thing hiding the bug.
A hardcoded id list would do the same and would also go stale the day one gate lands.

**What this deliberately does not do: hide them.** A build guide can name a path that exists in the
catalog; a player who reads one, searches, and finds nothing files a bug — and that is the failure
mode hiding creates, traded for a smaller one. The Codex already ships the *"exists, and it is not
yours"* idiom for species; this is the same idiom for a gate the world has not grown yet.

**When a gate lands, nothing about this surface changes except the field.** `gateState` flips to
`wired`, the condition row becomes a tier row with a real requirement, and the path leaves the
collapsed bucket. **That is the acceptance test** (§14 tests 29–32), and it is why the answer is a
render rule rather than a content decision this module is not entitled to make.

---

## 10. Standalone-first

**Everything in this module works with the game closed.**

| Piece | Where it runs |
|---|---|
| Allocation read/write | Server REST + SQLite — `AptitudeEndpoints.cs:26-57` is the shipped shape a tree allocation copies |
| `Θ`, the budget source | `IPowerIndexProvider.ActorIndex`, server-side (`AptitudeEndpoints.cs:49`) |
| Gate / Focus / distance arithmetic | `tree-resolve`, a pure `FusionRpg.Core` path with no Unity reference |
| The surface | React, band-2 panel over whichever stage is current |
| Where path power is felt with the game closed | Expeditions and web battles, server-resolved |

**What the injector adds — enrichment, never a gate.** The allocation-changed broadcast already
reaches it: `AptitudeEndpoints.cs:115-117` sends to **both** `WebGroup` and `InjectorGroup`, and the
comment at `:63-64` records that a `WebGroup`-only send was found dead by a live probe. A tree
allocation uses the same wire. Path-derived channels reach a live lawn through the existing derived
path; **no new write surface**, and nothing gates on the game being open. GG-39 holds.

---

## 11. Commands

```powershell
cd web\fusion-rpg-web
npm test -- volumeMatrix          # GG-50: this module adds its rows
npm test -- diffStateMatrix       # GG-47: a plan picker must declare its comparison state
npm test -- fourStatesMatrix      # GG-17: loading / empty / error / locked per surface
npm test -- vocabularyGuard       # GG-23: no engine token in player text
npm test -- magnitudeGuard        # GG-46: no unlabelled number
npm test -- bandGuard             # GG-53: band 4, never band 3
npm test -- xyflowGuard           # the lattice is a CSS grid, not a graph library
npm run test:coverage
npm run build                     # includes tsc --noEmit
npm run check:bundle              # GG-38 entry-chunk budget
npm run test:e2e                  # volume fixtures at 10 / 100 / 1000, and the 1280x720 floor
```

---

## 12. Project structure

```text
web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx              replaces the placeholder; :12's comment corrected
web/fusion-rpg-web/src/ui/actor/passives/YoursPanel.tsx      level 0 — invested paths, Focus, not-working count
web/fusion-rpg-web/src/ui/actor/passives/BloodlinePin.tsx    level 0b — this creature's own path
web/fusion-rpg-web/src/ui/actor/passives/PathBrowse.tsx      level 1 — 39 cards, ordered, windowed
web/fusion-rpg-web/src/ui/actor/passives/PathLattice.tsx     level 2 — 2 x 10 CSS grid, GG-61 bounded body
web/fusion-rpg-web/src/ui/actor/passives/TierRow.tsx         the attributed requirement + two-route reason
web/fusion-rpg-web/src/ui/actor/passives/TraitCell.tsx       three states, one verb
web/fusion-rpg-web/src/ui/actor/passives/TraitDetail.tsx     level 3 — the deepen stepper lives here
web/fusion-rpg-web/src/ui/actor/passives/PlanPanel.tsx       draft preview, Revert, the three plan numbers
web/fusion-rpg-web/src/ui/actor/passives/focusLine.ts        1/H prose. No Magnitude, no new unit class
web/fusion-rpg-web/src/ui/actor/passives/passiveReason.ts    one reason table, reasonFor.ts's shape
                                                            -- including the gateState condition (9.1)
web/fusion-rpg-web/src/hooks/useAllocationDraft.ts           the EXTRACTION named below
web/fusion-rpg-web/src/contract/types.ts                     tree DTOs. UnitClass union unchanged
src/FusionRpg.Server/PassiveTreeEndpoints.cs                 GET state, POST whole allocation
src/FusionRpg.Contracts/PassiveTreeDtos.cs                   the wire shape
```

> **Extract the shared allocation hook first.** `ProgressionTab.tsx:7-14` admits its allocation logic
> is a verbatim copy of `AptitudesPage.tsx` and names the fix — *"extracting a shared
> `useAptitudeAllocation()` hook."* **A tree spend flow would be the third copy.** Extract, then
> build on it.

**No SQL outside `FusionRpg.Data`** — `guard-dal.ps1` enforces it. **No new top-level route**, no new
rail entry, no new stage.

---

## 13. Code style

```tsx
/**
 * The tier row (D28, L9, R1). Three rules live here and all three are load-bearing:
 *
 * 0. THE GATE READS APTITUDE POINTS, and the row says the word. The cells under this row are priced
 *    in SKILL points and the detail panel spends SOULS. Three wallets on one screen; a row that says
 *    only "180 needed" is how a player comes to spend the wrong one, with no error to tell them.
 *
 * 2. EXACTLY ONE LENDER, always singular. The credit is `max`, never a sum, so a second same-stance
 *    path never helps. Rendering a total teaches compounding that does not exist -- the mistake the
 *    red team itself made before the sweep, and the player would answer it by spreading inside their
 *    stance, which is the behaviour D28 exists to discourage.
 * 3. THE REASON IS VISIBLE SIBLING TEXT, never a tooltip, and it names BOTH routes. `ActionCluster`
 *    already settled this argument for world verbs: `disabledReasonGuard` accepts a bare `title` as
 *    the floor, but a hover-only reason is unreachable on touch and invisible to a keyboard user who
 *    has not focused the control yet. Both routes, because under the largest-mate rule either one
 *    works and a player shown only one will take the wrong one.
 *
 * Attribution reuses `ChannelContributions`' grammar (a source name and its contribution, per row)
 * rather than a second attribution component -- GG-49 applied to a GATE instead of to a stat.
 */
export function TierRow({ tier, need, have, own, lender }: TierRowProps) {
  const short = need - have;
  return (
    <li className="border-t border-border py-1" data-testid={`tier-row-${tier}`}>
      <div className="flex justify-between text-xs">
        <span>{t`Tier ${tier}`}</span>
        <span>{t`opens at ${need} aptitude points · you have ${have}`}</span>
      </div>

      {/* One lender. Never a sum, never a second name. */}
      <p className="text-2xs text-muted" data-testid={`tier-sources-${tier}`}>
        {lender
          ? t`${own} from ${pathName} · ${lender.amount} lent by ${lender.pathName}`
          : t`${own} from ${pathName}`}
      </p>

      {short > 0 ? (
        <p className="text-2xs text-bad" data-testid={`tier-locked-${tier}`} id={`tier-why-${tier}`}>
          {lender
            ? t`Opens at ${need} aptitude points. ${short} more in ${pathName}, or ${short} more in ${lender.pathName}.`
            : t`Opens at ${need} aptitude points. ${short} more in ${pathName}.`}
        </p>
      ) : null}
    </li>
  );
}
```

**Player vocabulary only.** No `typeId`, no `Intent`, no `UniqueActor`, no `channelId`, no
`pathId` in visible text — `vocabularyGuard` enforces it and this surface is the one most likely to
leak, because every trait is a channel underneath.

---

## 14. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Passives_is_a_tab_not_a_route` | No entry added to the router or the rail; the sheet mounts over any stage. GG-1 |
| 2 | `Depth_from_a_stage_is_three_pushes` | sheet → path → trait. GG-10 |
| 3 | `Path_browse_declares_windowed_at_39` | A row in `volumeMatrix.test.ts`. GG-50 |
| 4 | `A_lattice_scrolls_inside_the_panel_and_never_grows_it` | 40 cells at the 1280×720 floor; `scrollHeight > clientHeight` on the **body**, shell height unchanged. GG-61 — the measurement GG-61 was written after, not an eyeball |
| 5 | `Level_two_opens_scrolled_to_your_own_depth` | Not to tier 1. §2.3's acceptance criterion |
| 6 | `Species_trees_never_enter_the_browse` | 879 is never a collection anywhere. §3 |
| 7 | `A_bloodline_is_pinned_to_its_creature` | Level 0b renders on that sheet and nowhere else |
| 8 | `Exactly_one_lender_is_named` | Three same-stance mates → one name, and the number equals the largest, not the sum |
| 9 | `A_locked_tier_names_both_routes_in_visible_text` | Queried by text, not by `title`. §7.2 part 4 |
| 10 | `A_locked_tier_carries_a_distance_not_a_condition` | A bar and a Θ, computed from this actor. §9 |
| 11 | `A_not_working_trait_is_not_merely_dimmed` | Border **and** word **and** fill; never colour alone. GG-27 |
| 12 | `A_not_working_trait_keeps_its_marks` | Nothing refunded, nothing repaired. D14 |
| 13 | `The_not_working_count_filters_to_exactly_those` | §8 finding rule 1 |
| 14 | `Focus_moves_while_the_draft_is_edited` | Both halves of the line, together. M8 / GG-33 |
| 15 | `Focus_renders_as_prose_and_creates_no_unit_class` | The `UnitClass` union is byte-identical before and after this module |
| 16 | `No_bare_number_reaches_the_eye` | `magnitudeGuard`. GG-46 |
| 17 | `No_engine_token_reaches_player_text` | `vocabularyGuard`, over every string this module adds |
| 18 | `An_imported_plan_is_priced_against_the_importing_actor` | The same code on two actors renders two totals. L8 |
| 19 | `A_plan_names_which_and_how_many_never_a_price` | The share code carries no cost field |
| 20 | `The_deepen_control_is_a_stepper_with_no_maximum` | No slider, no `max`. PS-8 |
| 21 | `No_raw_id_labels_a_control` | The defect at `AptitudesPage.tsx:64-65` is not inherited. GG-23/24 |
| 22 | `A_draft_never_paints_authority` | Committed values come from the server response only. GG-15 |
| 23 | `Every_mutation_produces_a_visible_result` | Success and failure both surface at band 4. GG-16 |
| 24 | `Nothing_this_module_adds_opens_at_band_three` | `bandGuard`. GG-53 |
| 25 | `Four_states_per_surface` | loading / empty / error / locked, in `fourStatesMatrix.test.ts`. GG-17 |
| 26 | `The_plan_picker_declares_its_comparison_state` | A row in `diffStateMatrix.test.ts`, or a real stated reason it has none. GG-47 |
| 27 | `The_lattice_uses_no_graph_library` | `xyflowGuard`. GG-38 |
| 28 | `Every_surface_renders_with_the_injector_absent` | GG-39, standalone-first |
| 29 | `A_gateless_path_renders_a_condition_never_a_distance` | On a path whose `gateState` is `unproduced`: no requirement number, no have-number, no bar, no Unlock verb, no price, on any tier. §9.1 rules 1–2 |
| 30 | `Gateless_paths_collapse_into_one_row_and_sort_last` | 27 of 39 behind one expandable row; all 12 wired paths render above it. §9.1 rule 3 |
| 31 | `A_gateless_path_is_counted_in_nothing` | Absent from the not-working count, from any locked total, from the Focus denominator and from Level 0. §9.1 rule 4 |
| 32 | `gateState_comes_from_the_report_never_from_a_zero` | A **wired** path with zero allocation renders a distance; an **unproduced** path renders a condition. Flipping only the field flips only the render, and no path id appears anywhere in this module. §9.1 rule 5 |
| 33 | `The_gate_row_names_aptitude_points_and_the_cell_names_skill_points` | `vocabularyGuard`-adjacent string assertion over `TierRow` and `TraitCell`: neither ever renders the bare word *points*. §4.1, R1 |

**E2E volume fixtures** at 10 / 100 / 1000 for the path browse, plus the 40-cell lattice at the
1280×720 floor — assert rendered node count and that the body, not the page, scrolls.

---

## 15. Boundaries

**Always**

- Render inside the Passives tab of the actor sheet.
- Treat a path's lattice as GG-61 (one entity's own content), the path browse as GG-50.
- Name exactly one lender, always singular.
- Print an exclusion on both traits, before the player spends.
- Give an out-of-reach thing a **distance** — *except* a path whose gate quantity does not exist,
  which gets a **condition** (§9.1). Those are the only two, and which one applies is read from
  `gateState`.
- Name the wallet: **aptitude points** open a tier, **skill points** buy a trait, **souls** deepen one
  (§4.1).
- Route every refusal through one reason table.
- Edit a draft; commit one whole allocation.
- Use player vocabulary, and route every number through `formatMagnitude`.

**Ask first**

- **Naming.** This spec uses *paths* / *traits* / *Focus* / *Plan* / *bloodline* / *stance*. A name is
  content and the owner's call, and one is needed before any player text is written.
- **Fixing `DemonsPage.tsx:367-388`'s volume defect** — another program's file, and it must be fixed
  before a bloodline reference hangs off it (§3).
- **Auto-drafting a species-derived starter plan** when a creature is bound. Friendly and free, but it
  puts a build in front of a player who did not ask for one.
- **Shareable build codes** as a shipped feature — they are the payoff of the static catalog, and they
  imply a stable catalog-version stamp and a decoder.

**Never**

- Add a top-level route, a rail entry, or a stage (GG-1).
- Put all 879 paths in one browse. That crosses the search-first threshold and turns the map into a
  query — GG-25 rejects it by name, and it is the **one arrangement that makes this unlearnable**.
- Invent a unit class. Thirteen ship; the fractional path count renders as prose.
- Use engine vocabulary on a player surface (GG-23).
- Show a locked reason only on hover.
- Render a requirement, a have-number, a bar, a price or an Unlock verb on a path whose `gateState` is
  `unproduced` — or promise that it is *"coming soon"* (§9.1).
- Infer a missing gate quantity from a zero, or carry a list of the 27 paths (§9.1 rule 5).
- Use the bare word *points* anywhere a number is shown (§4.1).
- Render a *sum* of same-stance credit (§7.2 part 3).
- Silently repair or refund a trait that stopped working (D14, D11).
- Use a slider for the deepen track, or a raw `NumberInput` under a raw-id label.
- Open at band 3 for anything that is not a run-ending result (GG-53).
- Gate any of this on the injector being attached (GG-39).
- Re-implement the tree in a second surface. The Almanac and the Codex **link into** this one (GG-9).

---

## 16. Success criteria

1. A player reaches, plans and spends a whole tree from the actor sheet with **zero new navigation**
   and at most three pushes from a stage.
2. The path browse sits in the shipped windowed tier; a path's lattice scrolls inside `PanelShell`
   and never grows it; **both are declared rows in the volume matrix**, not assumptions.
3. Every locked tier says how far away it is, **in aptitude points**, in this actor's own numbers, and
   names both routes that would open it. No surface in this module renders the bare word *points*.
4. Exactly one lender is ever named.
5. A trait that stopped working is visibly a fifth kind of nothing, keeps its marks, and is findable
   from Level 0 without opening a single path.
6. Focus is visible, moves while the draft is edited, and adds **no fourteenth unit class**.
7. A plan prices itself against the actor it is applied to, and a shared plan carries no price.
8. Every surface renders correctly with the game closed.
9. A path whose gate quantity has no producer renders a **condition**, is counted in nothing, sorts
   into one collapsed bucket, and is identified from `TreeResolveReport.gateState` rather than from
   any list this module holds — so the day a gate lands, no FE change is needed.

---

## 17. Open questions

1. **Naming** (see §15). *paths* / *traits* / *Focus* / *Plan* / *bloodline* / *stance* — plus
   *aptitude points* and *skill points* from §4.1, which are the two the player must be able to tell
   apart — are this spec's working vocabulary and need an owner call before player text is authored.
2. **One summary sentence per species tree.** The Codex answers *"which creature should I bind
   next?"* at its own resolution given one line per bloodline saying what it is for. 840 sentences —
   **cheap if booked into the D30 pipeline now, expensive as a second pass over 840 artifacts.** It
   is `species-tree`'s generator contract, not this module's, but this module is what needs it.
3. **Comparing two plans side by side.** GG-47 makes comparison first-class wherever a player
   chooses, and a plan is chosen. A comparison across 1,560 traits has no shipped shape and is not
   designed here — §14 test 26 accepts a stated reason in place of a diff state, which is the honest
   interim.

---

## 18. Decisions implemented

| Decision | What this module does about it |
|---|---|
| **D3** two tracks per trait | §4 — one verb on the cell, the deepen stepper in the detail. Split by cadence |
| **D4/D5** the focus multiplier | §6 — Focus line, `1/H` prose, `perMilleRatio`/`absolute` for `F` |
| **D7** hybrids are Neutral | *"Spreading is a real choice, not a mistake"* is stated to the player, not discovered |
| **D8** `H` reads self-spent | Focus renders what `tree-resolve` computes; the surface never re-derives the rule |
| **D9/D27** the roster ships whole | Level 1 holds 39 shared paths, and grows without re-scaling — the browse tier is the constraint, not the roster |
| **D11** items grant points | §8's second sentence — *"needs 3 more points; the gear that gave them is off"* |
| **D14** printed exclusion, runtime no-op | §8 in full |
| **D17** species favour triple | The Codex line and the starter-plan question (§17.2, §15) |
| **D18** respec is a full reset | §5.1 — the reason planning is worth building, and why there is no orphaned-unlock trap to teach |
| **D21** every actor has its own tree state | The sheet is already per-actor; a Plan is the object, an actor is where it is applied |
| **D23/D30** unique species trees | §3 — level 0b, Codex reference, never in a browse. L7 splits into two printed promises |
| **D24** static shared catalog | §5.3 — the reason a build is describable at all, and the reason a share code means the same thing to two players |
| **D25** rising unlock cost | §5.2 — the price of a plan, three numbers, the order-independence promise, and L8 |
| **D26** one tier ladder | §2.2 — both branches share one ladder down the middle, which teaches D26's rule by layout |
| **D28** cross-unlock, one lender | §7 in full — five parts, and §7.3's ordering mitigation for the case with no home |
| **D29** 10 tiers × 2 branches | §2.3 — 40 cells is GG-61, not GG-50; §9 — the tenth tier gets a distance |
| **ideal §13.4** 27 of 39 gates have no producer | §9.1 — the condition presentation, the collapsed bucket, and `gateState` read from the report. Not a decision this module takes; a render rule for the state the decision leaves behind |
| **D32** near-uniform target distribution | Not rendered. It is a generation-side property with no player surface |
| **PS-8** endless grind | §4 rule 1 — a stepper, never a slider, because a slider needs a maximum |

**Decisions with no surface here:** D1, D2, D6, D10, D12, D13, D15, D16, D19, D20 (superseded), D22,
D31 (superseded), D33, D34, D35, D36. **D16 in particular** — conversion traits have no runtime
(no atom kind writes an element payload, `OverlayCombatCalculator.cs:128-172`), so §8's example
sentence is written against a property the catalog may not carry yet. The sentence is correct
whenever the property exists; the surface does not depend on it existing.

---

## 19. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, player UI,
    derived stats and units, demon species content, standalone web.
[x] I read every doc in DESIGN-GATE §1's "Anything a player sees" row this
    session: architecture/game-gui-principles.md (GG-1, 8, 9, 10, 15, 16, 17,
    22-27, 33, 38, 39, 44-51, 53, 61 and §20.1's decisions),
    design/information-architecture.md (whole), architecture/fe-game-foundation.md
    §3/§5, design/spec-derived-stat-sheet.md §1/§3/§4,
    design/spec-magnitude-and-units.md §1-§3. Plus passive-tree-map.md,
    passive-tree-ideal.md (whole), research/passive-tree 07, 14, 16, 09.
[x] I checked for a lock covering this. No passive-tree row in decisions.md; the
    ideal's idea phase is closed and the map is approved. No build authorized.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — ActorPanel's six tabs and its
    "a later module's own tab body" comment, PassivesTab's four locked slots AND
    its now-false :12 comment, RENDER_ALL_MAX = 24 / SEARCH_FIRST_ABOVE = 240,
    PanelShell's real bounded height and scrolling body, the thirteen-member
    UnitClass union (checked by counting), formatMagnitude's refusal, the raw-id
    NumberInput defect at AptitudesPage:64-65, contractView's "next 900 Souls",
    ActionCluster's rejection of hover-only reasons, reasonFor's one table, the
    Codex's discovered/seen/silhouette states, and the dual-group broadcast at
    AptitudeEndpoints:115-117.
[x] I read the surrounding section of every rule I quoted — GG-61's own "This is
    not GG-50" paragraph and GG-44's menu-entry scope are both load-bearing, and
    misreading either is what doc 07 got wrong. Also 9's own three-presentation
    table, whose rejection of the CONDITION form is scoped to a deep tier and does
    not reach 9.1's case.
[x] 2026-09-05 audit fold, re-verified in code THIS session: PointBudget.cs:15's
    element_mastery comment; status_applied has ZERO src/ hits; railState.ts:52-60's
    UNLOCK_LADDER condition strings. Corpus numbers recomputed from the verified
    840 species / 40 nodes / 879 trees rather than carried from doc 14.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no suite was
    run. Every claim is a read of source. The Θ=100 tier and ownership figures are
    quoted from research doc 14's computation, not re-derived here — but their
    SHARE column is recomputed against the corrected 35,160 denominator, because
    doc 14's own percentages were against a superseded corpus size.
[x] Nothing contradicts a §2 invariant. Standalone-first is checked explicitly in
    §10. No cap is proposed (§4 rule 1 rejects a slider for exactly that reason).
    No new power-shaped scale. NO FOURTEENTH UNIT CLASS — §6 checks the union and
    finds nothing must grow.
[~] Corrections propagated. PARTIAL: §2.1 and §3 name two files that need fixing
    (PassivesTab.tsx:12's comment, DemonsPage.tsx:367-388's volume defect); this
    is a spec and does not edit them. Both are booked — one as this module's own
    work, one as ask-first because it is another program's file.
```

**Did a GUI principle have to bend? No.** One was mis-applied by doc 07 and is corrected here
(GG-50 → GG-61 for a single lattice), and one live defect was found in a surface this design sits
next to. Neither is a bend.

---

## 20. Related

- [passive-tree-map.md](../passive-tree-map.md) · [passive-tree-ideal.md](../passive-tree-ideal.md)
- [spec-tree-resolve.md](spec-tree-resolve.md) — supplies `TreeResolveReport`: gate, tier, lender,
  `H`, `F`, and the excluded traits
- [game-gui-principles.md](../game-gui-principles.md) ·
  [information-architecture.md](../../design/information-architecture.md) ·
  [fe-game-foundation.md](../fe-game-foundation.md)
- [spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
  [spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)
- [14-learnability-at-scale.md](../../research/passive-tree/14-learnability-at-scale.md) — the
  predecessor this spec builds on ·
  [07-learnability-and-surface.md](../../research/passive-tree/07-learnability-and-surface.md) ·
  [16-depth-exhaustion.md](../../research/passive-tree/16-depth-exhaustion.md)
