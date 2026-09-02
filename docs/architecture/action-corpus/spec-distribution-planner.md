# Spec: distribution-planner (A-S1)

**Module id:** `distribution-planner` · **Program:** [action-corpus](../action-corpus-map.md) §4 · **Build order:** 4 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none.** This is **Engine 1** — the state machine that decides what work exists. *"No model decides what work to do."*

It owns every count the model is not allowed to choose: the category of each brief, its pairing role,
the per-tier quotas, the rung windows, and the per-tier atom-family access sets. It emits N fully
specified briefs and nothing else. Its correction to the naive design is the load-bearing one — **the
planner assigns the category, because the planner is the thing that owns the distribution target**, so
distribution is correct by construction rather than by retry, and `category` leaves the vote set
entirely.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. A brief's `allowedAtomFamilies` names **pools**. **A cell is a target, never
   an identity** — no brief may name a cell, a tier or a resolved channel.
2. **Small-batch proof before any full run.** Every model-adjacent stage ships `--dry-run` and a small
   `--count`; the call budget is a **ceiling, not a plan**, and a full run is an owner decision behind
   a quality gate. This module is where a run's size is chosen, so it is where that rule bites hardest:
   it must refuse to plan a full run without an explicit flag.
3. **The roster is 84 species, not 904.** Motif anchors 84, family assignments **53**, 19 families.
   Per-species count is a **tunable**, so growing the roster is config, not a schema change.
4. **C1's family-access widening is gated** on three things that do not exist — a per-rung `powerBudget`
   row, a family-aware non-additive price (needs D2), and a budget check with a production caller.
   **Until all three hold, this module emits structure-gated tiers only**, and every tier's
   `allowedAtomFamilies` is the same set.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| The rung table — 10 rows with `minTier`/`maxTier`/`poolRolls`/`qPowerMilli`/`costMulti`/`cdMulti`/`structureBudget`, `cap: 10` | `data/tuning/action-rungs.v1.json` |
| `structureBudget` per rung, and the guard that rejects an over-budget action naming rung and axis | `StructureBudgetGuard.cs:38` |
| `rung(n) = min(earnCount, cap)` — the only input is `earnCount` | `UnlockLadder.cs:56-61` |
| Enabler/payoff pairing data and its closed-loop coverage assertion | `EnablerPayoffPairings.cs:20-31`, `EnablerPayoffCoverage.cs:21-34` |
| The 21 statuses a payoff can key on | `StatusCatalogBootstrap.cs:15-56` |
| Closed slot vocabularies: 5 categories, 3 kinds, **6** target modes, 4 area shapes, 4 relations | `ActionEnums.cs:10-49`; `ActionTargetSpec.cs:16-47` |
| A committed dry-run entrypoint pattern to match (`--dry-run` prints briefs, makes no calls; `--count` bounds a real run) | `adapters/effects/affix/generate_affixes.py:74-99` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `StructureBudgetGuard` cannot detect two of the seven axes — `reaction` and `restriction` — and says so in its own docstring | `StructureBudgetGuard.cs:27-34` |
| Those two are the signature tier's only structural advantage over the family tier, first appearing at rung 9 | `data/tuning/action-rungs.v1.json` rows 9-10 |

**⛔ CORRECTED 2026-09-03 (review F3/F4).** The row above conflated two different states, and the
older wording made this module's own AC7 describe a case with zero instances.

- **`reaction` is UNSPENDABLE, not undetectable.** `StructureBudgetGuard.cs:27-30` says so in its own
  words — *"`ActionKind` has exactly three members (`Basic`/`Innate`/`Skill`), none reaction-shaped …
  so it is correctly never flagged, not merely unchecked."* A brief naming `reaction` authors something
  the shipped action model cannot express, so this module **refuses** it rather than emitting it with a
  flag (`spec-tier-access-gate.md` §3.3 and its AC5).
- **`restriction` is genuinely undetectable.** `StructureBudgetGuard.cs:30-34` names the reason — it
  needs the effect-atom program's per-atom payload/target data, outside the three tables the guard
  reads. This module **may** assign it, and every brief that does carries `structureEnforced: false`.

**Consequence, stated rather than assumed:** the family/signature structural split is real but only
**partly** enforced. `restriction` is the signature tier's one exclusive axis (§3 step 5), it is
assignable, and no guard checks it — so the report must give the count rather than claim the tiers
differ. It is no longer a case with zero instances, which is what the intersection rule made it.

### Real gap

There is no planner, no brief schema, and no quota table.

## 2. Inputs and outputs

**Reads:** `role-lean.json` and `characteristic-pool.json` (A-S0) · `type-weights.json` (A-T1) ·
`data/tuning/action-rungs.v1.json` · `data/tuning/action-corpus-run.v1.json` (**new** — every count) ·
the previous round's coverage report from A-S5 · the accepted corpus, for `avoidNeighbours`.

**Writes** `data/seed/actions/_briefs/round-<n>.json`, `kind: "action-brief"`, in the A-C1 envelope.
One entry per brief:

```jsonc
{
  "briefId": "brief.species.cherrybomb.002",   // deterministic: scope + key + ordinal
  "scope": "species", "scopeKey": "cherrybomb",
  "anchor": {                                   // group B — READ from the seed, never invented
    "family": "cherry", "element": "fire", "rarity": "cultivated",
    "themeKey": "demon.cherrybomb",
    "motifs": ["樱桃", "爆炸"], "antiMotifs": [],
    // family scope only — DERIVED here, §3 step 2b. Present as a key on every family brief,
    // possibly empty; absent is a defect.
    "familyMotifs": ["僵尸", "樱桃"], "familyAntiMotifs": ["屋顶", "植物", "种植", "花盆", "保护"],
    "familyMotifBasis": "intersection"
  },
  "slot": {                                     // group C — the PLANNER decides these
    "category": "attack", "targetMode": "area", "areaShape": "row",
    "relation": "enemy", "kind": "skill",
    "rungBand": [5, 10], "structureAxes": ["riderStatus", "condition"]
  },
  "pool": {                                     // group D
    "allowedAtomFamilies": ["atom.burn", "atom.spread"],
    "forbiddenAtomFamilies": ["atom.crit-rate", "atom.crit-damage"]
  },
  // group E — atom FAMILIES, never statuses. `role: "none"` is the common case; see §3 step 6.
  "pairing": { "role": "enabler", "pairedPayoffFamily": "atom.rot-punisher" },
  "avoidNeighbours": [                          // group F — proactive dedup
    { "actionId": "action.species.cherrybomb.001",
      "fingerprint": "burn+spread|attack|area|row|enemy|condition+riderStatus|enabler" }
  ],
  "_provenance": { "corpusHash": "...", "promptVersion": 1, "round": 1, "tuningVersion": 1 }
}
```

**Every field is an enum, an id, or a list of them.** No magnitudes, no weights, no probabilities, no
durations — the schema audit rejects all four smuggling shapes mechanically, before a call is made.

**⛔ CORRECTED 2026-09-03 (review F5, F7, F10, F15).** Four things in the earlier example were wrong
against the code of record, and each is load-bearing downstream:

- **Casing (F10).** `ActionTargetModes.Name` returns `"self" "single" "multi" "rolledTarget" "all"
  "area"` (`ActionTargetSpec.cs:103-112`), `ActionAreaShapes.Name` returns `"row" "column" "square"
  "rectangle"` (`ActionTargetSpec.cs:134-141`), `RelationKinds.Name` returns `"self" "ally" "enemy"
  "any"` (`RelationKind.cs:23-26`), and `ActionCategories.Name` returns the `DerivedStatChannels`
  constants `"attack"…` (`ActionEnums.cs:96-104`). The example emitted `"Area"`, `"Row"`, `"Enemy"`
  while A-C1 §3 step 5 mandates a cross-check that refuses an unknown member — it would have refused
  this module's own output.
- **`pairing` keys on an atom FAMILY, not a status (F7).** `data/seed/actions/pairings.json` maps
  `atom.chill-punisher → [atom.chill-applier]` and `atom.rot-punisher → [atom.rot-applier,
  atom.blight-applier]`, and `EnablerPayoffPairings.IsPayoff(string atomFamily)` /
  `EnablersOf(string payoffFamily)` (`EnablerPayoffPairings.cs:26,30-31`) take atom families
  throughout. `rot` reads as both a status id and as `atom.rot-punisher`'s stem, which is how the two
  got conflated. `enablesStatus` is gone; see §3 step 6.
- **`targetMode`/`areaShape` are AUTHORED here (F5).** See §3 step 4a.
- **`familyMotifs` is derived here (F15).** See §3 step 2b.

## 3. The algorithm

1. **Read the run target.** `generalCount`, `perFamilyCount`, `perSpeciesCount` and `mode`
   (`smoke` | `full`) come from `data/tuning/action-corpus-run.v1.json`. **`full` requires an explicit
   `--full` flag *and* a passing quality gate from the previous smoke batch** — constraint 2 turned
   into a refusal rather than a note.
2. **Enumerate the plan's subjects, from real data.** Species: the 84 catalog rows. Families: the 19
   distinct families in `family-assignments.json`, whose sizes are measured, not assumed — `cherry` 7,
   `fire` 5, `pea` 5, `ice` 4, then three at 3, **eleven** at 2 and `nut` at 1. ⛔ **CORRECTED
   2026-09-03 (review):** the earlier "ten at 2" summed to 51, not 53; re-counted from the file, the
   size histogram is `{7:1, 5:2, 4:1, 3:3, 2:11, 1:1}` and sums to exactly 53. Mean family size is **2.8**
   (53/19), not the 48 an earlier sizing assumed; that number decides how much a family-scoped
   judgement is worth and it belongs in the report.
2b. **⛔ Derive each family's motif set — added 2026-09-03 (review F15).** `A-P2` correctly recorded
   that `motif-assignments.json` is **species**-keyed (84 keys) and that a family's motif set *"has to
   be derived … and that derivation is not written anywhere. A-S1 owns it"*
   (`spec-family-propose.md:68-70`). It was not written here, so `A-P2`'s AC5 rejected 100% of this
   module's output. **The derivation, decided and written:**

   - **`familyMotifs` = the INTERSECTION** of its member species' `motifs`, sorted byte-wise.
   - **`familyAntiMotifs` = the UNION** of its member species' `antiMotifs`, sorted byte-wise.
   - **`familyMotifBasis`** records which rule produced the set: `intersection` normally; `majority`
     when the intersection is empty (motifs held by at least `ceil(n/2)` members); `frequency` when
     that is empty too (the highest-count motifs, ties broken byte-wise, capped by a
     `familyMotifMax` tuning row). The fallbacks exist so the derivation is **total**, not because
     they are expected — see the measurement below.
   - A family of one (`nut`) intersects to its single member's own set, and `familyMotifBasis` says
     `intersection`; the coverage report is where that reads as the thin-family problem A-P2 hazard 1
     names, not here.

   **Why intersection and not union**, stated so it can be overturned on evidence: A-P2's own
   judgement is *"what makes the whole family recognisable, not what makes one member special"*
   (`spec-family-propose.md:107`). A union hands the model motifs owned by exactly one member — that
   is a signature motif wearing a family label, and it is precisely the output A-P3 exists to produce.
   The union is also permissive where it matters most: the set becomes A-P2's `motifsExpressed` enum,
   so a union of six lets a family action express a motif no sibling shares.

   **Why the union for anti-motifs**, and it is not symmetry: an anti-motif is a *refusal*, and every
   member holds the family's action. A refusal held by any one member must bind the whole family's
   action, or that member is handed an action expressing what it must not be.

   **Measured against the shipped files, 2026-09-03 — the fallbacks never fire today.** All 19
   families have a **non-empty** intersection, and every one of them is **exactly 2 motifs**
   (`cherry` over its 7 members intersects to `["僵尸", "樱桃"]` against a union of 6). Every
   family's anti-motif union is exactly 5. So `familyMotifBasis` is `intersection` for all 19 rows
   today, and a row that is not is a real content change worth failing on.

   **Absent is a defect; empty is a value.** Every family-scoped brief carries `anchor.familyMotifs`,
   `anchor.familyAntiMotifs` and `anchor.familyMotifBasis` as **keys**, even when a list is empty —
   the same absent-versus-empty discipline A-P3 applies to `familyActions`
   (`spec-signature-propose.md:156-158`).

3. **Allocate categories by quota, not by sampling.** For each subject, `count` briefs are split across
   the five categories by largest remainder over A-T1's `categoryMilli`, computed in `long`, widening
   before the multiply and dividing by 1000 last, exactly once. Remainder units go to the largest
   fractional parts, ties breaking on `ActionEnums.cs:119-123`. **The distribution is therefore exact,
   not approximate**, which is what makes A-S5's question *"is the plan satisfiable?"* rather than
   *"did the model drift?"*.
4. **Assign the rung window per scope** from tuning: general **1-4**, family **1-7**, signature
   **5-10** — geometrically even, three rungs apart, each ceiling 2.315× the last. Emitted as
   `rungBand`, never as a magnitude.

   **⛔ The `rungBand` → `ActionRow.Rung` collapse rule, stated here — added 2026-09-03 (review
   F3/F13).** `ActionRow.Rung` is one `int` (`ActionRow.cs:23`) and `StructureBudgetGuard.Check`
   resolves exactly one row from it (`StructureBudgetGuard.cs:41`), while a band spans budgets of 2
   and 7 axes. A-E1 §3.0 requires the rule be stated; it is: **`Rung = rungBand[1]`, the band's
   ceiling.** It is the only choice consistent with step 5 — the axes are drawn from the ceiling
   rung's budget, so the guard must resolve that same row or it checks a budget the brief was never
   planned against.

   **What this rule does and does not decide**, against
   [`spec-rung-semantics.md`](spec-rung-semantics.md) (A-U1) §3.1, which pins the two readings apart:
   `Rung` is the **authored** value and it fixes the **structure budget**, because structure is a
   property of the action rather than of who holds it; `effectiveRung = min(earnCount, rungCap)`
   (`UnlockLadder.cs:56-61`) is the **holder's** value and it fixes magnitude and cost. So setting the
   authored `Rung` to the band's ceiling buys the brief the ceiling's **axis budget** and **nothing
   else** — it does not price the action at rung 10's `costMulti`, because cost never reads the
   authored column. The band stays on the row as the planning window (A-E1 AC1b) and is never a
   runtime range.

4a. **⛔ Assign the target shape — AUTHORED here, not rolled. Decided 2026-09-03 (review F5).** The
   earlier drafts decided this twice: this module authored `targetMode`/`areaShape`, A-S3 hashed them
   as mechanical identity, and A-T1 emitted `targetModeMilli`/`areaShapeMilli` described as feeding
   `WeightedChoice.Pick` at roll time (`ActionSeeder.cs:55`). **Both cannot hold**, and the decision
   is **authored**:

   - **The shipped row already stores an authored one.** `ActionRow.Targeting` is an
     `ActionTargetSpec` on the row (`ActionRow.cs:40`), compiled once and cached. A corpus action
     binds through that field, so authoring it stores something that already exists rather than
     inventing a field.
   - **Identity must be stable to be dedupable.** A-S3's tier-1 fingerprint is *"mechanical
     identity"*; if the shape is rolled per player, the fingerprint hashes a field that is not part of
     the seed, and two seeds are indistinguishable until instantiation.
   - **This designs no second roll.** `ActionSeeder.Generate`'s `WeightedChoice.Pick`
     (`ActionSeeder.cs:55`) is the shipped **runtime** generator's own roll over a caller-supplied
     `targetShapePool`, alongside `Instantiator.Draw` (`ActionSeeder.cs:47`). It is untouched, and it
     is not on the corpus's bind path. Nothing new rolls.
   - **A-T1's vectors keep a real consumer, deterministic and plan-side**: this step allocates
     `targetMode` across a subject's briefs by **largest remainder over `targetModeMilli`**, exactly
     as step 3 allocates `category` over `categoryMilli` — `long`, widened before the multiply,
     divided by 1000 last, exactly once, ties on the declared member order
     (`ActionTargetSpec.cs:14-33`). `areaShapeMilli` is consulted **only** for briefs allocated
     `area`, and it allocates by largest remainder too.
   - **The board gate is not duplicated.** `ActionSeeder.cs:51-53` keeps owning the roll-time board
     gate; a corpus brief allocated `area` that reaches a boardless context is refused at bind time by
     `ActionValidator`'s existing `AreaRequiresBoard` rule, which is unchanged.
5. **Assign structure axes** as the **union-to-ceiling** of the rung window: the axes budgeted at the
   window's **top** rung in `data/tuning/action-rungs.v1.json`, minus the axes that are unspendable.

   **⛔ CORRECTED 2026-09-03 (review F3). The rule was "intersection", and the intersection is
   empty.** Rungs 1 and 2 carry `structureBudget: []`, so general `[1,4]` intersected to ∅ and family
   `[1,7]` intersected to ∅ — every brief in two of the three tiers would have carried zero axes, and
   `restriction` (rungs 9-10 only) could never be assigned, which made this module's own AC7 and the
   matching hazard notes in A-P3, A-S4 and A-S5 describe a case with **zero instances**.
   **Union-to-ceiling** is the reading that matches what a band means everywhere else in this spec —
   step 4's collapse rule already puts `Rung` at the ceiling, so the axes must come from the same row
   the guard will check.

   Measured against the shipped table, this is the whole assignment:

   | Tier | Window | Ceiling row | Axes assignable |
   |---|---|---|---|
   | general | `[1,4]` | rung 4 | `scopeSplit`, `riderStatus` — **2** |
   | family | `[1,7]` | rung 7 | + `condition`, `sequence`, `consumption` — **5** |
   | signature | `[5,10]` | rung 10 | + `restriction` — **6** (`reaction` subtracted, below) |

   **`reaction` is subtracted, always, and a brief naming it is REFUSED — not flagged.** It is
   unspendable, not undetectable: `StructureBudgetGuard.cs:27-30` verified `ActionKind` has exactly
   three members and none is reaction-shaped, so the guard is *correct* never to flag it, and
   authoring it authors something the shipped model cannot express
   (`spec-tier-access-gate.md` §3.3 and its AC5).

   **`restriction` is assignable and unchecked.** A brief spending it carries
   `"structureEnforced": false`, and the run report names the count, because
   `StructureBudgetGuard.cs:30-34` needs the effect-atom program's per-atom payload/target data to
   detect it. It is also the signature tier's **one** exclusive axis, so it is the only mechanical
   thing separating signature from family today — which is exactly why the count is a report line
   rather than a silent pass.
6. **Assign the pairing role — over ATOM FAMILIES, and the role is OPTIONAL.**

   **⛔ CORRECTED 2026-09-03 (review F7).** The earlier wording paired briefs *"for a status"* and
   the brief carried `enablesStatus: "rot"` from the 21-member status catalog. **The shipped pairing
   surface has no status in it anywhere.** `data/seed/actions/pairings.json` is, in full,
   `{"atom.chill-punisher": ["atom.chill-applier"], "atom.rot-punisher": ["atom.rot-applier",
   "atom.blight-applier"]}`, and `EnablerPayoffPairings.IsPayoff(string atomFamily)` /
   `EnablersOf(string payoffFamily)` (`EnablerPayoffPairings.cs:26,30-31`) take atom families
   throughout, as does `EnablerPayoffCoverage.Check(IReadOnlyList<string> poolAtomFamilies, …)`
   (`EnablerPayoffCoverage.cs:21-23`). `rot` reads as both a status id and as `atom.rot-punisher`'s
   stem, which is how the two got conflated.

   The rule, restated against the real table:

   - `pairing.role` is one of **`enabler` | `payoff` | `none`**, and `none` is the common case.
     **`none` is a value; a missing key is a defect** — the repo's own rule, applied to a field that
     cannot be populated for most briefs.
   - A brief is assigned `payoff` **only** when its `allowedAtomFamilies` intersects the payoff keys
     `EnablerPayoffPairings` actually holds. It then carries
     `pairing.pairedPayoffFamily = <that payoff family>`.
   - For every `payoff` brief in a `(scope, scopeKey)` group, the planner assigns at least one
     sibling brief `role: "enabler"` with the same `pairedPayoffFamily`, and forces one of
     `EnablersOf(pairedPayoffFamily)` into that brief's `allowedAtomFamilies`. This is the plan-side
     twin of `EnablerPayoffCoverage.Check` (`EnablerPayoffCoverage.cs:21-34`), assigned rather than
     hoped for, because independent weighting would almost never put the pair in one pool.
   - Roles are drawn from the pairing table (`EnablerPayoffPairings.cs:20-31`), never inferred by
     parsing a predicate tree.

   **⛔ The decision the review asked for, made: the role is optional, and the table grows
   separately.** The table has **two** payoff keys and **three** distinct enabler families, so it
   cannot supply a pairing role across 84 species × 5 categories — the arithmetic is not close. The
   two available answers were *grow the table* or *make the role optional*; **both are needed, and
   only the second belongs to this module.**

   - Growing `pairings.json` authors *which payoff families exist*, which is atom-program content, not
     a planner decision — and `EnablerPayoffPairings.Parse` refuses a payoff with zero enablers in
     exactly these words: *"a payoff with no possible enabler is the exact unreal combination §5
     forbids pricing a discount for"* (`EnablerPayoffPairings.cs:64-67`). A planner that invented
     payoff keys to fill a quota would author unreal combinations by construction.
   - So **this module never invents a pairing key**, `role: "none"` is legal and expected for the
     overwhelming majority of briefs, and the run report states the payoff-key count and the share of
     briefs whose pool could touch one. Growing the table is a **named, separate deliverable** owned
     with the atom families themselves; until it lands, the pairing tier covers only the briefs whose
     pool reaches `atom.chill-punisher` or `atom.rot-punisher`.
7. **Set `allowedAtomFamilies`.** Constraint 4: **the same eligible set for every tier**, with only
   `forbiddenAtomFamilies` narrowing it — and the one narrowing that is always applied is *never both
   halves of a known multiplicative pair* (crit rate with crit damage), because pricing is knowingly
   additive there and the generated corpus is disproportionately that shape.
8. **Fill `avoidNeighbours`** from the accepted corpus: the k nearest already-accepted fingerprints in
   the same `(scope, scopeKey)`, k a tuning row, ordered by fingerprint field distance then by action
   id ordinal. The fingerprint rendered here is **A-S3's, verbatim** (`spec-dedup-select.md` §2) —
   one definition, quoted, never a second one shaped like it. Proactive dedup is far cheaper than
   generate-and-reject.

**⛔ One field, one name — corrected 2026-09-03 (review).** Four names were in circulation for what
read as one field: `atomPools` (A-C1's stored seed), `allowedAtomFamilies` (this brief), `atomFamilies`
(the model schemas) and `sortedAtomFamilies` (A-S3's fingerprint). Resolved as:

| Name | What it actually is |
|---|---|
| `pool.allowedAtomFamilies` / `pool.forbiddenAtomFamilies` | the **permitted set** this module fixes — a brief field, and a genuinely different thing from the chosen set |
| `atomFamilies` | the **chosen subset**, the model's answer, and the field stored on the seed and the row. **This is the canonical stored name**; A-C1's `atomPools` and A-E1 §3.0's `atomPools` are renamed to it |
| `sortedAtomFamilies` | not a field at all — A-S3's byte-wise **rendering** of `atomFamilies` inside the fingerprint string |

`atomFamilies` wins because it is the code of record's own word: `AtomRow.FamilyId`
(`ActionSeeder.cs:61`), `IsPayoff(string atomFamily)` and `EnablersOf(string payoffFamily)`
(`EnablerPayoffPairings.cs:26,30-31`). Constraint 1's *"an atom names a pool"* is preserved by the
constraint, which is binding, and not by a field name, which drifts.
9. **Emit deterministically.** `briefId = brief.<scope>.<scopeKey|general>.<ordinal:03>`, ordinals
   assigned in the subject's canonical order. Canonical write, sorted keys, `\n`.

## 4. What it must NOT do

- **Never call a model.** Not for a category, not for a quota, not to "sanity check" a plan.
- **Never put a number in a brief.** No magnitude, weight, probability or duration. `rungBand` is a
  pair of table indices, and the schema audit must prove that is all it is.
- **Never widen family access per tier** until all three of constraint 4's gates hold. Structure-gating
  is the safe default, kept as the default rather than as a rejected option.
- **Never plan a full run implicitly.** A full run is the owner's decision on the smoke batch's
  evidence, and this module does not schedule past that checkpoint.
- Never invent an anchor. Group B is read from A-S0's output; a species with `family: null` gets a
  brief with `family: null`, and is simply not a subject of the family tier.
- **Never emit a brief naming `reaction`.** ⛔ **CORRECTED 2026-09-03 (review F3/F4):** the earlier
  line said only *"report it"*. `reaction` is **unspendable** — `StructureBudgetGuard.cs:27-30`
  verified `ActionKind` has three members and none is reaction-shaped — so a brief naming it is
  **refused**, never flagged (`spec-tier-access-gate.md` AC5).
- Never claim the family/signature structural split is fully enforced while `restriction` is
  undetectable. `restriction` is the signature tier's one exclusive axis, it **is** assignable, and
  the report gives the count.
- **Never invent a pairing key.** The payoff/enabler vocabulary is `pairings.json`'s, and a payoff
  with no authored enabler is refused at parse time (`EnablerPayoffPairings.cs:64-67`). If the pool
  reaches no payoff family, the role is `none` — that is the answer, not a gap to fill.
- **Never let the target shape be decided twice.** `targetMode`/`areaShape` are authored here (§3
  step 4a); nothing downstream re-rolls them, and `ActionSeeder.cs:55` is the runtime generator's own
  roll on a path a corpus action does not take.
- Never let a brief's ordinal depend on dictionary or filesystem iteration order.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | two runs with the same tuning, lean, weights and coverage report produce a byte-identical `round-<n>.json`, asserted by hash |
| **Quota exactness** | for every subject, the per-category brief counts sum to the subject's count, and each matches the largest-remainder allocation of its `categoryMilli` exactly |
| **Planted violation — a magnitude in a brief** | a brief carrying `"chance": 250`, `"durationMs": 3000`, `"powerMilli"`, a string field matching `^[0-9]+$`, or an enum of numeric strings is **refused by the schema audit**, all four shapes tested |
| **Planted violation — unpaired payoff** | a plan where a `payoff` brief has no `enabler` brief carrying the same `pairedPayoffFamily` in its `(scope, scopeKey)` group **fails the planner's own check**, mirroring `EnablerPayoffCoverage.cs:21-34` on the plan side. The planted pair uses `atom.rot-punisher` / `atom.rot-applier`, the real keys |
| **Pairing vocabulary** | every `pairedPayoffFamily` is a key of `pairings.json` and every forced enabler is a member of `EnablersOf` it; a brief carrying a **status id** in that field is refused, naming the field |
| **Structure axes — union-to-ceiling** | the assignable sets are asserted as literals: general **2**, family **5**, signature **6**; a brief naming `reaction` is **refused**, and one naming `restriction` carries `structureEnforced: false` |
| **Family motifs derived** | every family-scoped brief carries `familyMotifs`, `familyAntiMotifs` and `familyMotifBasis` as keys; all 19 families resolve `intersection` against today's data, and the intersection for `cherry` is asserted as exactly two motifs |
| **Target shape allocation** | `targetMode` counts per subject equal the largest-remainder allocation of A-T1's `targetModeMilli` exactly, and `areaShapeMilli` is consulted only for briefs allocated `area` |
| **Casing** | every emitted `category`, `targetMode`, `areaShape` and `relation` round-trips through `ActionCategories.TryParse` / `ActionTargetModes.TryParse` / `ActionAreaShapes.TryParse` / `RelationKinds.TryParse`; `"Area"` is refused |
| **Planted violation — family widening** | a tuning file that narrows `allowedAtomFamilies` per tier while any of constraint 4's three gates is absent is **refused**, naming the missing gate |
| **Planted violation — multiplicative pair** | a plan allowing crit-rate and crit-damage in one brief is refused |
| **Full-run refusal** | `mode: "full"` without `--full` and without a passing smoke gate exits non-zero with a message naming the missing evidence |
| **`--dry-run`** | renders every brief and makes zero calls; the transport stub raises if anything tries |
| **Roster** | subject counts are asserted as literals: 84 species, 19 families, 53 family-assigned species. Drift toward 904 fails |
| **Overflow** | quota arithmetic is `long`, widened before multiplying, divided by 1000 once; forced overflow **throws** |

## 6. Acceptance criteria

1. A round file exists at `data/seed/actions/_briefs/round-<n>.json`, loads through A-C1's envelope,
   and every entry validates against the brief schema.
2. The schema audit finds no numeric field, testing all four smuggling shapes.
3. Per-subject category counts equal the largest-remainder allocation of A-T1's weights, exactly.
4. Every `rungBand` is inside its scope's window: general ⊆ [1,4], family ⊆ [1,7], signature ⊆ [5,10],
   and the `Rung = rungBand[1]` collapse rule (§3 step 4) is asserted, so `StructureBudgetGuard.Check`
   resolves the same row the axes were drawn from.
4b. Every brief's `targetMode` and `areaShape` are **authored** (§3 step 4a) and every value parses
   through the code of record's own `TryParse` — `"self" "single" "multi" "rolledTarget" "all" "area"`
   (`ActionTargetSpec.cs:103-112`) and `"row" "column" "square" "rectangle"` (`:134-141`).
5. Every `payoff` brief has an `enabler` brief with the same `pairedPayoffFamily` in the same
   `(scope, scopeKey)` group; every `pairedPayoffFamily` is a key of `pairings.json`; `role: "none"`
   is present as a key on every brief that has neither role. ⛔ **CORRECTED 2026-09-03 (review F7)** —
   this criterion named a *status*, and the shipped pairing surface has none.
6. `allowedAtomFamilies` is identical across tiers, and the run report states that family-access
   widening is gated and names the three missing preconditions.
7. Briefs whose structure axes include `restriction` carry `structureEnforced: false`, and the report
   gives the count. A brief naming `reaction` is **refused**, and a test asserts the refusal.
   ⛔ **CORRECTED 2026-09-03 (review F3/F4):** under the old intersection rule this criterion had
   **zero** reachable instances; under union-to-ceiling `restriction` is assignable at the signature
   tier, and `reaction` is refused rather than flagged.
7b. Every family-scoped brief carries `anchor.familyMotifs`, `anchor.familyAntiMotifs` and
   `anchor.familyMotifBasis` as keys (§3 step 2b) — the derivation A-P2's AC5 requires and which
   nothing owned before.
8. `--dry-run` produces briefs and zero model calls; `mode: full` refuses without the flag and the gate.
9. A rerun over unchanged inputs is byte-identical by hash, with provenance recording corpus hash,
   tuning version, prompt version and round.

## 7. Dependencies

**Depends on:** **A-S0** (map §4 and §5), **A-T1** (weights), **A-S5** (round n+1 targets — a cycle
that is broken by round 1 reading no report), and A-C1's envelope for its output file.
**Depended on by:** **A-P1**, **A-P2** and **A-P3**, each of which reads exactly one brief. A-P1 and
A-P2 may run in parallel; A-P3 waits on A-P2 because a signature must differ from its own family's
output.
**Cross-program (map §7):** channel pools come from **effect-atom E30** — briefs reference pools, so a
pool id that does not resolve yet is an edge, not an error. The **power** program owes the rung window
a row in the caps register; §5 constraint 2 promised one and `ssot-power-scale.md` §11 does not have it.
