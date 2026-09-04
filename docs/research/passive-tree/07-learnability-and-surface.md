# Passive trees — learnability and the player surface

**Status:** research, 2026-09-05. Not a spec, no build authorized. Enriches
[architecture/passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) (D1–D23) with the
requirement the owner added on 2026-09-05:

> *"the passive skills tree need concrete value before the game run / it is different with item loot
> mechanism / it need solid stats, so user can learn it, if it random every new player create, it
> will cause confuse, use cannot built because they need to relearn"*

That makes **learnability a design requirement**, and learnability is a property of a *surface*, not
of a data file. So this document is half information architecture.

**Evidence marking follows the research packs' convention:** FACT = read from code or a first-tier
source this session. INFERENCE = drawn from a fact. RECALL = general knowledge, unverified in-repo.

---

## Answer up front

### The scale, in one line

**~50 trees × ~29 nodes = ~1,450 nodes, each with two spend tracks = ~2,900 places to spend — one
shared catalog, applied separately to the commander and every demon a player owns.** At Θ=100 a
player holds 100 skill points, so they can unlock **6.9% of it**. The catalog is the size of Path of
Exile's whole tree (RECALL: ~1,300 nodes) but split into fifty disjoint pieces instead of one map you
learn once. Full arithmetic and sources in §1.

### The learnability contract — seven guarantees

Each is a promise to the player, and each names the mechanism that keeps it.

| # | Guarantee, in the player's words | Mechanism |
|---|---|---|
| **L1** | *"The tree is the same for me as it is for you, and the same tomorrow as today."* | D13's deterministic plan decides shape, ladder, requirements and links **before** any generator writes text. The plan is a versioned artifact in `data/`, not a per-player roll. This is the one place the tree is **unlike loot** — and it is the whole reason a build guide can exist |
| **L2** | *"A node does not move. If it is nerfed, it is still the same node."* | Balance numbers live in `data/tuning/<domain>.v{n}.json`, never in the tree's shape (`tunables-ssot.md`; `AptitudeTuning.cs:156` is the shipped pattern). A rebalance changes a magnitude. It must never renumber, reorder or re-parent a node — **that is a new rule this document asks for**, and it is the load-bearing half of L1 |
| **L3** | *"I can read what a node does before I own it."* | Every number reaches the eye as a `Magnitude` carrying a `UnitClass` — thirteen classes, and `formatMagnitude` **refuses a bare number by construction** (`web/fusion-rpg-web/src/i18n/magnitude.ts:5-15`). GG-46 |
| **L4** | *"I can lay out a whole build without spending anything."* | The draft → dirty → commit flow already ships for primary stats (`AptitudesPage.tsx:18-38`). The tree extends it. Two things are missing today and must land with it: a **Revert** control, and a **preview of what the draft changes** |
| **L5** | *"If one of my nodes has stopped working, the game tells me — it does not quietly fix it."* | D14's printed runtime no-op, plus D11's red-invalid state when gear points are withdrawn. Both are printed on the node **before** you spend, and surfaced as a count on the tab when they fire |
| **L6** | *"I can see the rule I am being scored on."* | The focus multiplier is rendered as a live number that moves while you edit the draft — §5 |
| **L7** | *"What I learned on my commander is still true on my demons."* | The catalog is shared; only allocation is per-actor (D21). One thing to learn, N places to apply it. §6 |

Two supporting promises that fall out of the design and should be stated to the player rather than
discovered: **respec is total and priced, never partial** (D18 — so there is no orphaned-unlock trap
to learn), and **spreading is a real choice, not a mistake** (D7 — hybrids sit behind, not dead).

### The IA, in brief

**The passive tree is the actor sheet's Passives tab. It is not a new route, not a new rail entry,
and not a new stage.**

That slot already exists and is already a locked placeholder:
`web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx:19-26` declares six tabs including `passives`, and
`PassivesTab.tsx:12-20` renders four `LockedGridSlot`s with the reason *"Passive skills are a
reserved sub-feature, no target date yet."* (FACT.) The actor sheet is a band-2 panel that opens over
any stage and is already reached from Creatures, Commanders and the sanctum roster strip — so a
per-actor tree costs **zero new navigation**, which is the whole point under D21.

Three levels of disclosure inside that tab, plus a node detail:

| Level | What the player sees | Volume |
|---|---|---|
| **0 — Yours** | Only the trees this actor has invested in, plus the focus readout and a count of any nodes that have stopped working | 1–4 cards for most builds |
| **1 — All paths** | The ~50-tree browse, searchable and filtered by what the actor's build touches | 50 cards — the windowed tier of the shipped volume rule |
| **2 — One tree** | 2 branches × 7 tiers, ~29 nodes, rendered as a fixed lattice | 29 cells — render-all |
| **3 — One node** | Value in its unit class, what it costs next, the exclusion print, where it sits | 1 |

Levels 0 and 1 are **tabs inside the Passives tab**, not pushes. So depth from a stage is: actor
sheet (1) → tree (2) → node (3). Exactly GG-10's budget, not over it.

**The tree — not the node — is the unit of browsing.** This is not taste; it falls out of the
shipped volume rule. `CreaturesLayer.tsx:18-24` declares render-all ≤ 24, virtualized window 25–240,
search-first above 240 (FACT). Fifty trees is the window tier. Fourteen hundred and fifty nodes
flattened would be the search-first tier — a search box over a bag of nodes, which is exactly the
"database viewer" GG-25 rejects. One tree's 29 nodes is render-all. **So nodes are only ever seen
inside a tree.**

### D21 in one sentence

**A Plan is an object; an actor is where you apply it.** Write a plan once, apply it to any actor,
and let a newly bound demon arrive with a species-derived plan already sitting in its draft —
suggested, never committed. That turns fifty authoring chores into fifty reviews. §6.

### Did any GUI principle have to bend?

**No.** The design fits inside GG-1 … GG-61 as written. The closest call is GG-9 (one canonical home
per concept): the Almanac should carry a **read-only** view of the same tree so a player can read a
build guide about a tree they have not unlocked. That is inside GG-9, which says other surfaces
*link into* the canonical one and do not re-implement it — the Almanac renders the same component in
a read-only mode, and the actor sheet stays the only place you spend. Stated here explicitly so
nobody builds two.

The real costs are not bends, they are work: `diffStateMatrix.test.ts:19-45` will fail if a picker
surface is added without declaring it (FACT), and comparing two *builds* across 1,450 nodes has no
shipped shape.

---

## 1. Scale — the number that drives every other answer

### 1.1 How many trees

D9: *"12 primary + all elemental + all status + each demon family."*

| Category | Count | Source |
|---|---:|---|
| Primary (aptitudes) | 12 | `decisions.md:103` — *"Twelve aptitudes are the RPG primary stats"* |
| Elemental | 6 | `ElementRoster.Concrete` — fire, ice, air, earth, light, dark (`src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs:21-29`) |
| Status | 21 | `status-ssot.md:227` — *"Locked status catalog (21 named ids)"*. Counted in code: **20** registered in `StatusCatalogBootstrap.cs:16-58`, plus 2 more at `ExhaustionPolicy.cs:70` and `StanceRuntime.cs:39` = 22 live ids. Minor drift, not material here |
| **Subtotal before demon families** | **39** | |
| Demon families | **unknown — see below** | |

**⚠️ D9's "each demon family" has no roster to read.** Measured across all 841 entries in 503 species
files this session: the `family` field holds **699 distinct freeform strings** — `undead` (64),
`artillery-flora` (17), `fungal-artillery` (16), `explosive-flora` (14), then a long tail of
one-offs (FACT, script over `data/seed/demons/species/*/*.json`). It is LLM prose, not a curated
vocabulary.

So D9's `n ≈ 40–60` is only reachable if someone curates that 699 down to **1–21 families**. That is
an owner decision and a generation task, and it is a prerequisite for the tree roster existing at
all. **Working number for the rest of this document: 50 trees.**

### 1.2 How many nodes

D10: every tree is 2 branches × tiers. D20 gives seven tier thresholds — 10 · 15 · 25 · 40 · 60 ·
85 · 115 — so **7 tiers**, and both branches share one tier requirement.

Nodes per tree is still open (ideal §7, owner decision 1). The reference point the ideal itself names
is Last Epoch's ~29 nodes per tree (FACT, prior-art §2.1).

```text
50 trees × 29 nodes                    = 1,450 nodes in the catalog
× 2 tracks each (D3: unlock + deepen)  = 2,900 places a player can spend
× ~3 lines of text per node            ≈ 4,350 lines to read the whole catalog
```

**Comparison, and it is the finding that matters.** Path of Exile's shared tree is roughly 1,300
nodes (RECALL, prior-art §2.5). We are proposing **more nodes than PoE**, and PoE's is *one* tree —
a single spatial map, learned once, where "I am here and I am going there" is a memory of a picture.
Ours is fifty disjoint pictures with no shared geography. (INFERENCE.) **Fifty small maps are harder
to learn than one big one**, which is why §4's information architecture spends its whole budget on
making the tree — not the node — the thing you navigate.

### 1.3 How much a player can actually spend

`data/tuning/aptitudes.v5.json:15-16` (FACT):

```json
"grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 }
```

`PointBudget.PointsFor` is `sourceValue × rate` with **no cap anywhere** (`PointBudget.cs:31-41`,
and its own comment says so).

| At Θ = | Skill points | Nodes reachable | Aptitude points | Trees you can hold at tier 7 (`req(7)=115`) |
|---:|---:|---:|---:|---:|
| 100 | 100 | **6.9%** of 1,450 | 300 | 2.6 |
| 500 | 500 | 34% | 1,500 | 13 |
| 1,450 | 1,450 | **100%** | 4,350 | 37 |

Two things follow.

**First, the early game is a scarcity game and that is good for learnability.** Ninety-three percent
of the catalog is out of reach, so the surface's job is not to show it all — it is to help the player
find the seven percent that is theirs.

**Second — and this is a correction to the prior-art doc — the breadth axis is not bounded.**
`passive-tree-prior-art-2026-09-04.md` §2.1 records breadth as *"Bounded — skill points are finite
(`skillPointsPerTheta × Θ`)"*. Skill points are finite **relative to Θ**, but Θ is uncapped by PS-8,
and the rate is flat. So at Θ ≈ 1,450 every node in every tree is unlocked and the tree stops being
a choice. Last Epoch's entire design rests on the opposite (FACT, EHG: *"these trees are specifically
designed to not be completable"*). Concentration then survives only through the soul track and `F`.
**This is an open question for the owner, not a claim that it is wrong** — §8, item 3.

### 1.4 D21 multiplies the state, not the learning

D21 gives the commander and **every demon** its own tree state. The catalog is shared (ideal §7,
owner decision 1: *"the catalog is shared; only allocation is per-actor"*).

```text
31 actors  (commander + 30 demons)  × 1,450 skills = 44,950 per-skill soul levels
101 actors (commander + 100 demons) × 1,450 skills = 146,450
```

There is no roster cap to read (PS-8 forbids one), and the species catalog is 841 entries, so the
upper end is a player's appetite, not a rule.

**This is why sparse storage is a hard requirement and not a nicety** — which the ideal already says
(D21, §7.9). It is also why §6's answer to D21 is *plans*, not per-actor authoring: the learning cost
is `1,450`, the management cost is `101 × 1,450`, and only the second one scales with the roster.

---

## 2. What already exists — surveyed, not assumed

DESIGN-GATE §4 records a session that concluded *"no UI exists"* because two `docs/design/` specs
were never opened. This section is the counter-check. Everything below was read this session.

### 2.1 The layer stack is built

The shell the GUI principles specify is **shipped**, not planned:

| Piece | Path | Note |
|---|---|---|
| Layer stack | `web/fusion-rpg-web/src/shell/layerStack.ts:8-52` | zustand store, six bands |
| Band tokens + guard | `src/theme/tokens.css:100-129`, `src/shell/bandGuard.ts:46-95` | bans `z-index` outside tokens |
| Panel shell | `src/shell/PanelShell.tsx:26,82-87` | `max-h-[min(720px,82vh)]`, internal scroll — GG-61 satisfied |
| Layer host + URL state | `src/stages/sanctum/SanctumStage.tsx:70-118` | `?panel=<id>&sel=<id>`; layers stay mounted after first open |
| Rail | `src/shell/railState.ts:8-104`, `src/shell/Rail.tsx:27` | renders from state, locked entries carry a reason |
| Global keymap | `src/shell/useGlobalKeys.ts:13`, `src/shell/keymap.ts` | one listener, one verb table |

Adding a *layer* is about four small edits. **We are not proposing one** — see §4 — but the fact that
it is cheap is why the choice to use the actor sheet instead has to be argued rather than assumed.

### 2.2 An allocation surface with draft-and-commit already ships

`web/fusion-rpg-web/src/features/aptitudes/AptitudesPage.tsx` (86 lines) is the template (FACT):

```ts
const [draft, setDraft] = useState<Record<string, number> | null>(null);   // :18
useEffect(() => { if (aptitudes.data && draft === null) setDraft(...); }); // :23-26  never clobbers an edit
const withinBudget = spent <= budget;                                       // :37
const dirty = JSON.stringify(draft) !== JSON.stringify(aptitudes.data.shares); // :38
<Button disabled={!dirty || !withinBudget || save.isPending} title={...}/>  // :76-83
```

Server side, `src/FusionRpg.Server/AptitudeEndpoints.cs:24-56`: `GET /api/aptitudes/{playerId}` and
`POST /api/aptitudes/allocate` with a full `shares` map. **The write is one whole-allocation
transaction, budget-checked, and it refuses rather than clamps** (`409 aptitudes.overbudget`, `:49-50`).

That transaction shape is already D18's shape. A full respec that clears and redistributes in one
write needs no new seam.

**Three gaps in it, all relevant:**

1. **No preview.** The player moves a number and learns nothing about what it did. GG-46 and GG-47
   both point at this.
2. **No revert.** Reset is implicit (reload the page, or save a different body).
3. **The label is the raw id** — `<Field key={id} label={id}>` (`ProgressionTab.tsx:107`,
   `AptitudesPage.tsx:65`). That is a live GG-23/GG-24 defect on a player surface, and the tree must
   not inherit it.

Also: `src/layers/aptitudes/AptitudesLayer.tsx` exists but is **imported nowhere** — dead code.
`CommandersLayer.tsx:52-53` explains why: *"Aptitudes is sheet-only via Progression tab; this layer
replaces the old rail slot."* (FACT.) **The project already made this exact decision once, in the
direction this document recommends:** an allocation surface belongs on the actor sheet, not on the
rail.

And `ProgressionTab.tsx:7-14` admits the allocation logic is copied verbatim from `AptitudesPage`,
naming the fix as *"extracting a shared `useAptitudeAllocation()` hook."* A tree spend flow would be
the **third** copy. Extract first.

### 2.3 The number-rendering contract is built; the stat sheet's producer is not

- `src/i18n/magnitude.ts:15` — `formatMagnitude(m: Magnitude)`, thirteen unit classes, **no overload
  takes a bare number** (FACT). `perMilleRatio` carries `op: flat | increased | more | absolute`.
- `src/contract/types.ts:173-181` — `ActorChannelDetail { channelId, value, unitClass, state, cap?,
  composeSentence?, contributions }`, and `:246` `DerivedChannelState` carries the six render states
  from `spec-derived-stat-sheet.md` §3.
- **But `channelSummary` is unconditionally pending** — `DerivedStatsTab.tsx:8-11` says there is no
  server endpoint, and the "Open full derived-stat sheet" button is `disabled` at `:65-72` (FACT).

**INFERENCE:** this is a wiring gap, not an architectural wall. The types, the six states, the
thirteen unit classes and the renderer all exist and are guarded. What is missing is a producer.
A tree node's "what this grants" line binds to exactly this contract, so the tree's L3 guarantee
lands the day that producer does — and the tree is a good reason to build it.

The nearest working thing is `ChannelContributions.tsx:10`, a per-source attribution list fed by
`useActorDerived` — **the closest existing shape to a node's "what this grants" readout.**

### 2.4 Volume, search and the node-graph question

- `CreaturesLayer.tsx:18-24` declares the three-tier volume rule: **≤24 render all · 25–240
  virtualized window · >240 search-first** (FACT). This is what §4's browse tier is chosen against.
- **`@xyflow/react` v12 ships and is used** in `features/world/WorldPage.tsx:223-243` — pan/zoom,
  click-to-select, custom nodes and edges, `nodesDraggable={false}`.
- **But the newer world stage deliberately abandoned it** for hand-rolled rendering on an authored
  grid (`stages/world/render/WorldScene.tsx:19-25`), and there is a guard test enforcing that:
  `stages/world/xyflowGuard.test.ts` (FACT).
- `PassivesTab.tsx` + `LockedGridSlot.tsx:14` already render a node-ish grid cell with a locked
  state and a stated reason.
- **There is no `Tooltip` primitive, on purpose.** `ActionCluster.tsx:22-26` — *"The reason is
  visible, not a tooltip"*; `disabledReasonGuard.ts:55-57` treats a bare `title` as the floor.
  `ui/world/ModifierLedger.tsx:36` is the sanctioned hover-card pattern (WCAG 1.4.13: dismissible,
  hoverable with a 60 ms grace, persistent).

### 2.5 What does not exist

- **No build / loadout / preset / template concept anywhere.** `diffStateMatrix.test.ts:26-30`
  records the loadout picker as explicitly excluded — *"no real backend supports a per-run squad
  selection."* The closest persisted presets are default commander and patron selection.
- No comparison surface for anything but relics.
- `PassivesTab.tsx:12`'s own comment carries a decision this document contradicts head-on: *"a flat
  locked list, not a node-graph tree (this game doesn't have PoE's content scale to justify one)."*
  **That was true when written and is now false** — §1.2 puts the catalog above PoE's node count.
  The comment should be corrected when the tab is built out, and it is a good example of why a
  comment is not evidence.

---

## 3. The learnability contract, argued

L1–L7 are stated in the answer-up-front. The two that need argument:

### L1 — why "same for everyone" is the point, and where it comes from

The owner's constraint is a comparison: *"it is different with item loot mechanism."* That is exactly
right, and the design already honours it. D13 makes generation **deterministic-first** — math decides
shape, ladder, requirements and links, and only then does a generator fill vocabulary and bonuses
inside that plan. So the tree is generated *once, into the game*, not *per player, at runtime*.

That is the opposite end of the repo's own seed → concrete → per-player principle from where items
sit. An item's affixes roll per player at runtime, on purpose. **A tree must not**, and the reason is
now stated by the owner rather than inferred: a build you cannot describe to another player is a
build you cannot learn.

Everything else follows. Build guides work. A wiki works. A screenshot of a tree means the same thing
to the person who sees it. A share code (§5.3) is possible at all.

### L2 — the rule this document is asking to add

L1 is only worth anything if the shape survives a patch. So:

> **A rebalance may change a node's magnitude. It may never change a node's identity, position,
> parent, tier or tree.**

The mechanism is already the repo's standard: node magnitudes are balance numbers, so they live in
`data/tuning/`, and the tree's *shape* is a plan artifact that changes only through a reviewed
regeneration. `tunables-ssot.md`'s test applies cleanly — *would a balance pass ever want to change
this number?* A node's damage: yes, tunable. A node's tier: no, structural.

This is not free. It means the generator's plan output is a **compatibility surface**, and a
regeneration that moves nodes is a migration, not a rerun. Worth saying out loud before the pipeline
is specced, because it is far cheaper to design in than to retrofit.

---

## 4. Progressive disclosure — the information architecture

### 4.1 Why the actor sheet and not a new layer

| Option | Verdict |
|---|---|
| A new top-level route | **Rejected.** GG-1's core failure mode, and the codebase already refuses it — `AptitudesLayer.tsx:5`: *"a layer, never a route (web/spec.md's own hard rule)"* |
| A new rail entry + layer | **Rejected.** Trees are per-actor under D21, so a rail-level layer would have to open, then ask *which actor* — a picker in front of the content. The project already walked this back once for aptitudes (`CommandersLayer.tsx:52-53`) |
| **The actor sheet's Passives tab** | **Chosen.** The slot exists (`ActorPanel.tsx:19-26`). The sheet is band-2, opens over any stage, is already per-actor, and is already reached from Creatures, Commanders and the sanctum roster strip. GG-9: one canonical home. GG-4's test: you go there to look and configure, so it is a layer |
| Almanac, read-only | **Also yes, and it is not a second home.** A player must be able to read about a tree with no actor in mind. The Almanac renders the same component in read-only mode and links into the sheet to spend. GG-9 permits exactly this |

### 4.2 The four levels

**Level 0 — Yours.** Opens first, every time. Contains only:

- the trees this actor has put anything into (typically 1–4),
- the **focus readout** (§5),
- **a count of any nodes that have stopped working**, which opens filtered to them (§5.4),
- what is unspent: *"14 skill points and 3,200 souls waiting."*

The empty state is content, not an edge case (GG-17): a new actor sees *"You have 6 points. Pick a
path."* and one affordance, not fifty cards.

**Level 1 — All paths.** The ~50-tree browse. Fifty is the virtualized-window tier of the shipped
volume rule, so it does not need search to function — but it needs **ordering**, because 46 of the 50
are irrelevant to any given build. Default order:

1. trees you have invested in,
2. trees gated by aptitudes you have already bought (the tier gate makes these cheap for you),
3. trees matching this actor's element and status,
4. everything else.

Then a search box and filters on category (primary / element / status / family). Query state persists
per layer (GG-51), which the codebase already does per-layer with local state.

A tree card carries: name, category token, how deep you are (tier 3 of 7), and **the two or three
things it is for** — not a node list. GG-25, GG-26.

**Level 2 — one tree.** 2 branches × 7 tiers, ~29 nodes. Render all. **A fixed lattice, not a
graph.**

> **Do not reach for `@xyflow`.** A 2×7 lattice is a CSS grid. The world stage already made this
> call and enforces it with `xyflowGuard.test.ts`, and GG-38 names the graph library in the entry
> chunk as a live weight defect. `LockedGridSlot.tsx` is already the cell.

Both branches sit side by side sharing a tier ladder down the middle — because D20's rule is that
**one investment opens offence and defence together**, and a layout that shows the shared ladder
teaches that rule without a sentence of tutorial (GG-45).

**Level 3 — one node.** Value in its unit class with the raw magnitude beside it, what the next soul
level costs, the compose sentence if sources do not simply add, the exclusion print, and — when the
`channelSummary` producer lands — where the number goes.

### 4.3 Volume, declared (GG-50)

| Surface | 10 | 100 | 1,000 |
|---|---|---|---|
| All paths | render all | window (the shipped 25–240 tier) | search-first — reachable if demon families are curated wide |
| One tree's nodes | render all | render all | not reachable — a tree is 2 × 7 tiers by D10 |
| Nodes flattened across trees | **never rendered** — nodes only exist inside a tree | | |

### 4.4 Unlocking (GG-44)

The **tab** unlocks when the player first earns a skill point. Inside it, **all fifty trees are
visible from the start**, because a build guide the player is reading references trees they have not
touched, and an invisible tree cannot be planned toward. Locked *tiers* say what opens them, which is
what GG-17 asks for. This is GG-44 applied at the menu entry, which is where the rule is aimed.

---

## 5. Preview, planning, and making `F` felt

### 5.1 A Plan is a first-class object

Both tracks make planning-before-spending valuable, for different reasons. D3's soul track has an
arithmetic cost that only goes up, so a wrong order is expensive. D18 makes respec a full reset
priced in souls, so a wrong build is expensive to undo. **INFERENCE: the cheapest fix for both is to
make the wrong build free to discover.**

The shipped draft/dirty/commit flow is the mechanism. Three additions:

1. **Revert** — restore the draft to what is committed. Named, not implicit.
2. **A preview panel** that reads the draft, not the server state: what the build grants, what the
   focus readout would become, and which nodes would stop working.
3. **A plan can outlive the panel.** Save a draft under a name without committing it.

Player words: **Plan**. Not "template", not "preset", not "loadout" (which the codebase already
reserves for a squad picker).

### 5.2 Authority stays server-side, and a draft does not violate GG-15

GG-15 forbids painting authority early, not showing a preview. A draft is explicitly *not committed*
and is labelled as such; the committed number is what the server returned. The distinction is already
built into the shipped page (`draft` vs `aptitudes.data.shares`, `dirty` between them).

The commit is one `POST` of the whole allocation, which the endpoint already refuses rather than
clamps. GG-16 needs a toast on both outcomes; the toast stack ships (`shell/Toasts.tsx:11`).

### 5.3 Shareable builds — the payoff of the static catalog

Because the catalog is identical for every player (L1), a build is completely described by:

```text
<catalog version> + [ (treeId, nodeId, points, soulLevel) … ]
```

Nothing else. No item, no roll, no seed. That serializes to a short code, and — the part that
matters — **a code from another player resolves against your catalog and means exactly the same
thing.** An item build cannot do this, because the item does not exist for you. This is the concrete
payoff of the static-catalog decision, and it is worth naming in the ideal doc as a *reason* for D13,
not just a consequence.

The URL grammar already carries it. GG-8 makes the address bar *stage + open layers*, and
`SanctumStage.tsx:70-72` already reads `?panel=` and `?sel=`. A plan is one more parameter:

```text
#/sanctum?panel=creatures&sel=<actor>&plan=<code>
```

Cold-loading that restores the stage, opens the sheet, and loads the plan **as a draft** — never
committed. GG-8's *"a URL never means throw away what you were doing"*, honoured exactly.

### 5.4 Making `F` felt

Prior art flagged `F` as *"mathematical rather than felt"* and suggested an effective-tree-count
readout. Under L6 that is no longer optional: **a player scored on a rule they cannot see will not
believe the game is fair.**

The good news is that the readout is not an analogy. `H = Σ(shareᵢ)²` is a Herfindahl index, and
`1/H` is its standard reading — the **effective number** of trees. It is exact, not a metaphor:

| Commitment | `H` | `1/H` — effective trees | `F` at `Fmax = 1.2` |
|---|---:|---:|---:|
| all in one | 1.000 | 1.0 | ×1.200 |
| 70 / 30 | 0.580 | 1.7 | ×1.116 |
| two, even | 0.500 | 2.0 | ×1.100 |
| 50 / 25 / 25 | 0.375 | 2.7 | ×1.075 |
| three, even | 0.333 | 3.0 | ×1.067 |
| twelve, even | 0.083 | 12.0 | ×1.017 |

So the readout and the multiplier are the same fact stated twice, and
`F = 1 + (Fmax − 1)/N_eff`.

**The surface.** One line, on Level 0 and in the plan preview:

> **Focus** — your commitment sits across about **2 paths**. Tree bonuses ×1.10.

**And it moves while you edit.** That is what turns a formula into a felt rule: the player drags
points between two trees and watches both halves of that line move together. GG-33 (numbers that
change, show that they changed) already asks for this, and the motion vocabulary declares M8 for
exactly this case (`information-architecture.md` §10).

**Where it must also appear:** on any tree-derived number the player reads, as the reason that number
is what it is. Otherwise `F` is a hidden coefficient and the stat sheet's contribution list will not
add up — the same defect `spec-derived-stat-sheet.md` §4 flags for `FlatReplace`.

**One unit-class question, flagged rather than invented.** `F` is a multiplier and renders as
`PerMilleRatio` with `op: "absolute"` — the class added on 2026-09-04 for exactly this shape
(a field whose neutral baseline is 1000, rendered `×1.10`). That already exists and needs nothing.
But the **effective-tree count is fractional**, and no unit class fits: `Count` is integer-shaped,
`aptitudePoints` is not this. DESIGN-GATE §1 warns that inventing a third classification is a known
past failure, so this document does not invent one. **Recommendation: render it as prose —
*"about 2 paths"* — which is not a `Magnitude` and needs no class.** Owner question §8, item 4.

---

## 6. Printed exclusions (D14) — reading them, and finding them

D14 is a **printed runtime no-op**, not an allocation block: the node stays allocatable and simply
stops working, both sides print the rule, and both name the same winner. Target rarity ~2% of nodes.

### On the node, before you spend

The exclusion is printed on **both** nodes, always, whether or not it is currently firing. The player
reads it while deciding, which is the entire point:

> **Ashen Root** · +40 fire power
> *Does nothing while your damage is converted away from fire. If both are taken, Ashen Root is the
> one that stops.*

Note the shape: it names a **property** (*converted away from fire*), not a node. That is D14's
O(1) rule made visible — the sentence stays true for conversion nodes that do not exist yet.

### On the node, once it has stopped

A distinct state, not a dimmed one. The stat sheet's six-state vocabulary already establishes the
discipline that *zero is four different things*; this adds a fifth kind of nothing and must look
unlike the others:

- a red border **and** the word **Not working** **and** a distinct fill — never colour alone (GG-27),
- the winner named inline: *"switched off by Emberflow"*,
- the node keeps its allocated marks. Nothing is refunded and nothing is silently repaired. Prior art
  is explicit that Last Epoch highlights lost nodes in red rather than fixing them, and this repo's
  own posture is to fail loudly.

The same state serves D11's other case: gear that granted points is removed, and the nodes those
points held become invalid. Same red, different sentence — *"needs 3 more points; the gear that gave
them is off."*

### Finding it without opening fifty trees

This is the part a naive design gets wrong. With 50 trees the player will not go looking.

1. **A count on Level 0**, always visible when non-zero: *"2 of your traits are not working."*
   Clicking it filters to exactly those. The rail already has a `badgeCount` mechanism
   (`railState.ts:27`) if it should also surface at the sheet's door.
2. **A toast at the moment it happens** (GG-16): allocating the node that switches another off
   reports it immediately, naming both. An outcome the player caused is never silent.
3. **Never a modal.** GG-53's interruption budget is spent on run-ending results only
   (`game-gui-principles.md` §20.1 D6). A dead node reports at band 4.

---

## 7. D21 — fifty actors without fifty chores

### The mechanism

**1. One catalog, N allocations.** Already the design's position. The learning cost is 1,450 nodes
once; only the *management* cost scales with the roster.

**2. Plans are objects; actors are where you apply them.** A plan is written once and applied to any
actor. "Apply" fills that actor's **draft** — it never commits. The player reviews and commits, or
reverts. This reuses the shipped flow exactly and adds no new authority path.

**3. A newly bound demon arrives with a suggested plan already drafted.** D17 locks a species'
build-favour triple — primary tree + element + status. That triple is enough to derive a starter plan
deterministically, with no generation and no LLM. So the per-demon experience is *"here is a sensible
build, change what you like, commit"* — a review, not an authoring task.

This is inheritance-from-species done honestly: it is a **suggestion in a draft**, so the player
still spends, still sees the cost, and can still say no.

**4. Bulk apply is one dialog, not fifty visits.** From Level 0's Plans section: apply a plan to a
selection of actors, one confirm dialog naming the total points and souls it will cost and exactly
what changes. GG-22 (destructive actions confirm and name what is lost) applies — this spends souls.

**5. Switching actors is already solved.** `CreaturesLayer.tsx` selection is a URL param lifted to
`SanctumStage.selectCreature` (`:111-118`); the sheet opens on the selected actor. Nothing new.

### What this does and does not fix

**Fixes:** the authoring cliff. Fifty demons become fifty reviews of a plan the player already
understands.

**Does not fix:** the *spending* cost. Each actor still pays its own points and souls (D21 is
maximum build expression, and that is the point). The surface's job is to make that cost **visible
before it is paid**, which is what the confirm dialog is for.

**Does not fix, and is a real open cost:** comparing two plans side by side. GG-47 makes comparison
first-class wherever a player chooses, and `diffStateMatrix.test.ts:19-45` will fail if a picker is
added without declaring it. A plan comparison across 1,450 nodes has no shipped shape and is not
designed here.

---

## 8. Standalone-first — confirmed

**Everything in this document works with the game closed.** (FACT, verified against code.)

| Piece | Where it runs |
|---|---|
| Allocation read/write | `AptitudeEndpoints.cs:24-56` — server REST, SQLite via `RpgStore.SaveAllocation`/`LoadAllocation` |
| Θ, the budget source | `IPowerIndexProvider` / `ServerPowerIndexProvider`, server-side (`AptitudeEndpoints.cs:46`) |
| Budget arithmetic | `PointBudget.PointsFor` — pure function in `FusionRpg.Core`, Unity-free |
| The surface | React, band-2 panel over the sanctum stage |
| Where tree power is *felt* with the game closed | Expeditions and web battles — `WebMatchService.BuildSquad` → `ResolveAndIngest`, server-resolved with seeded RNG |

`spec-aptitude-allocation-surface.md` §1 names this as the reason commander scope shipped first:
every expedition battle a real player already runs carries the allocation, through an already-shipped
UI, with no new battle trigger. A tree rides the same path.

**What the injector adds — enrichment, never a gate:**

- Tree-derived channels reach a live lawn through the existing derived-channel path
  (`AptitudeChannelMods` → `ChannelMods` → `EntityStatWriter` / Funnel). No new write surface.
- The allocation-changed broadcast **already reaches the injector**:
  `AptitudeEndpoints.cs:66-71` sends `AptitudesUpdated` to both `WebGroup` and `InjectorGroup`, and
  its own comment records that a `WebGroup`-only send was found dead by a live probe. A tree
  allocation uses the same wire.
- A lawn run is a faucet for the souls the deepen track spends.

None of it gates anything. GG-39 holds.

---

## 9. Open questions for the owner

1. **The demon-family roster.** D9 says "each demon family", and the corpus has **699 distinct
   freeform family strings** across 841 entries. `n ≈ 40–60` needs that curated to ≤21. Who curates
   it, and is a tree per family or per family *group*? **This blocks the roster existing at all.**

2. **Nodes per tree** (already ideal §7 item 1, now with a learnability frame). ~29 gives 1,450
   nodes — more than Path of Exile's whole tree, split into fifty pieces. Is that the intended
   reading load, or is a smaller per-tree count the way to keep fifty trees learnable?

3. **Should breadth actually be bounded?** At `skillPointsPerTheta: 1` and uncapped Θ, every node in
   every tree unlocks at Θ ≈ 1,450, and the tree stops being a choice. The prior-art doc records
   breadth as "bounded"; measured against the tuning file it is only *slow*. Options: leave it
   (concentration then rests entirely on the soul track and `F`), or make the rate itself a
   diminishing tunable — which needs a `ssot-power-scale.md` §10 row, not a local decision.

4. **How to render the effective-tree count.** `F` itself is `perMilleRatio`/`absolute` and needs
   nothing. The fractional tree count fits no existing unit class. **Recommendation: prose, no new
   class** — DESIGN-GATE §1 names inventing a third classification as a known past failure. Confirm,
   or authorise a fourteenth class.

5. **Are shareable build codes wanted?** They are the payoff of the static catalog (§5.3), and they
   imply a stable catalog-version stamp and a decoder. Worth deciding before the plan artifact's
   format is fixed, not after.

6. **The read-only Almanac view** — same wave as the sheet, or later? Without it, a player cannot
   read about a tree they have not unlocked, which weakens L1 in practice.

7. **Species-derived starter plans** (§7, item 3) — auto-draft on binding a demon, or only on
   request? Auto-draft is friendlier and costs nothing, but it does put a build in front of a player
   who did not ask for one.

8. **Naming.** This document used *paths* for trees, *traits* for nodes, *Focus* for `F` and *Plan*
   for a saved draft, to keep player vocabulary out of engine words (GG-23). Naming is content and
   the owner's call — but a name is needed before any player text is written, and "tree"/"node" are
   fine too.

---

## 10. Two things this document asks to be fixed in existing files

Not proposals, just corrections found while reading:

| File | What is wrong |
|---|---|
| `web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx:12` | *"a flat locked list, not a node-graph tree (this game doesn't have PoE's content scale to justify one)"* — the catalog is now ~1,450 nodes, above PoE's. True when written, false now |
| `docs/research/passive-tree-prior-art-2026-09-04.md` §2.1 | Records breadth as *"Bounded — skill points are finite"*. Finite per Θ, but Θ is uncapped by PS-8 and the rate is flat, so breadth is unbounded — §1.3 |

Also worth noting for whoever builds this: `ProgressionTab.tsx:7-14` admits the allocation logic is a
verbatim copy of `AptitudesPage.tsx` and names the fix (*"extracting a shared
`useAptitudeAllocation()` hook"*). A tree spend flow would be the third copy. And both surfaces label
allocation rows with the raw id (`AptitudesPage.tsx:65`, `ProgressionTab.tsx:107`) — a live
GG-23/GG-24 defect the tree must not inherit.

---

## 11. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, class system / allocation,
    player UI, derived stats, standalone web.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md, game-gui-principles.md (all 61 rules, §16-§21),
    design/information-architecture.md, design/README.md, fe-game-foundation.md §1-§3,
    design/spec-derived-stat-sheet.md, design/spec-magnitude-and-units.md §1-§3.2,
    architecture/standalone-rpg-map.md, architecture/passive-tree-ideal.md,
    research/passive-tree-prior-art-2026-09-04.md, plus decisions.md rows
    Standalone-first / Game GUI / Class system.
[x] I checked decisions.md for a lock covering this. There is no passive-tree row —
    the ideal is idea phase and no build is authorized.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — and found one comment that is now
    wrong (PassivesTab.tsx:12) and one research claim that is wrong (prior-art §2.1).
    The 699-family count, the 20 registered statuses, the 6 elements, the two grant
    rates and the shipped draft/commit flow were each read or counted, not quoted.
[x] I read the surrounding section of every rule I quoted — GG-9's "link into, do not
    re-implement" and GG-44's menu-entry scope both matter to the argument.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite was
    run. Every claim is a read of source, a grep, or a script over data/seed. The
    "diffStateMatrix.test.ts will fail" claim is read from the test body, not executed.
[x] Nothing contradicts a §2 invariant. Standalone-first is checked explicitly in §8.
    No new power-shaped scale is proposed. No cap is proposed. No third stat
    classification is invented — the one gap (fractional tree count) is flagged as an
    owner question instead.
[~] Corrections propagated. PARTIAL: §10 names two files that need fixing; this is a
    research document and does not edit them.
```
