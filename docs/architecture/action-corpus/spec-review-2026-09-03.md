# Spec review — action-corpus, 2026-09-03

**Adversarial review of all 13 original specs**, verified against shipped code. **25 findings.** The
laws held — no model picks a magnitude, no second roll, no balance number in code, every tunable routed
to `data/tuning/`. **The damage is structural**, and it clusters.

**Fixed the same day** are marked ✅. Everything else is a **required correction before build**, recorded
here rather than in a spec because it spans several.

**Status 2026-09-03, second pass:** every finding in §2 has been applied to the specs it named.
**One remains open** — F17's citation-drift cluster, which is mechanical and is listed at the bottom.

---

## 1. Fixed 2026-09-03

| # | Finding | Fix |
|---|---|---|
| **F1** | `ActionRow` has **no** `category`, `scope`/`scopeKey`, `pairingRole`, `structureAxes`, `atomPools`, and `Rung` is a single `int`, not a band. The corpus is authored into a row that cannot hold most of it, and the program declared itself content-only so nobody claimed the schema | ✅ **A-E1 widened** to own the whole schema surface (§3.0), including a stated `rungBand` → `Rung` collapse rule |
| **F4** | `reaction` is **unspendable, not undetectable**. `StructureBudgetGuard`'s docstring: *"`ActionKind` has exactly three members… none reaction-shaped… so it is **correctly never flagged, not merely unchecked**"* | ✅ **A-G1 corrected** — and it names the four other specs that repeat the error. A brief naming `reaction` must be **refused**, not flagged |
| **F6** | Six specs cite `test_offline_guarantee.py` as the *"tests never call a model"* precedent. That file **permits `127.*`, `localhost`, `::1`, `0.0.0.0`** — and the model endpoint is `http://localhost:1234` (`llm_caller.py:40`). **The cited guarantee explicitly allows the call it is cited to forbid** | ✅ Repointed in 7 specs at a real raising stub (`test_classify_pipelines.py:36`), with the reason inline |

### Second pass — closed 2026-09-03

| # | Finding | Decision written, and where |
|---|---|---|
| **F3** | A-S1's structure-axis **intersection** rule made its own AC7 unreachable: rungs 1-2 carry `[]`, so general `[1,4]` and family `[1,7]` both intersected to ∅ and `restriction` could never be assigned | ✅ **Decided: union-to-ceiling** — the axes budgeted at the window's **top** rung. `spec-distribution-planner.md` §3 step 5 carries the rule and the measured assignment (general **2** axes, family **5**, signature **6**), §1's Consequence, §4 and AC7 are rewritten, and the collapse rule `Rung = rungBand[1]` is stated in §3 step 4 so the guard resolves the same row. The **four** hazard notes now match: `spec-signature-propose.md` §6 hazard 1, `spec-validate-heal.md` §6 hazard 2, `spec-coverage-report.md` AC7 and its metric register. `reaction` is **refused**, `restriction` is assignable-and-unchecked — the two are no longer one bucket |
| **F7** | The pairing table has **two rows keyed on atom families**, not statuses; four specs assigned pairing roles *"for a status"* | ✅ **Vocabulary corrected to atom families** in A-C1 (`enablesStatus` → `pairedPayoffFamily`, §2 + §3 step 4), A-S1 (§2 example, §3 step 6, §4, §5, AC5), A-S3 (the fingerprint's `pairingRole` note) and A-S5 (`enablerPayoffCoverage` + a new `pairingReach` metric). **Decision written: the role is OPTIONAL** (`enabler \| payoff \| none`, with `none` a value and a missing key a defect), and **growing the table is a named separate deliverable** owned with the atom families — because `EnablerPayoffPairings.Parse` refuses a payoff with zero enablers as *"the exact unreal combination §5 forbids"* (`EnablerPayoffPairings.cs:64-67`), so a planner inventing keys to fill a quota would author unreal combinations by construction |
| **F15** | Family motifs had no producer — A-P2 said *"A-S1 owns it"*, A-S1 never mentioned it, so A-P2's AC5 rejected 100% of A-S1's output | ✅ **The derivation is written into A-S1** (§3 step 2b): `familyMotifs` = **intersection** of member species' motifs, `familyAntiMotifs` = **union** of their anti-motifs, `familyMotifBasis` recording `intersection`/`majority`/`frequency` so the rule is total. Justified against A-P2's own judgement (*"what makes the whole family recognisable, not what makes one member special"*) and **measured**: all 19 families intersect non-empty, every one to exactly **2 motifs** (`cherry` over 7 members → `["僵尸", "樱桃"]` vs a union of 6), every anti-motif union exactly 5 — so the fallbacks never fire today. A-P2's AC5 is now **absent-versus-empty**: a missing key raises, an empty list is legal |
| **F5** | The target shape was decided twice — authored by A-S1, hashed by A-S3, and rolled by A-T1's vectors at `ActionSeeder.cs:55` | ✅ **Decided: AUTHORED.** `ActionRow.Targeting` is already an authored `ActionTargetSpec` on the shipped row (`ActionRow.cs:40`), and identity must be stable to be dedupable. Written into A-S1 (§3 step 4a, allocating `targetModeMilli` by largest remainder at **plan** time, exactly as `categoryMilli`), A-T1 (§2's consumer note, §4, AC6b), A-S3 (why the fingerprint may hash it) and A-C1 (§2). **No second roll is designed**: `ActionSeeder.Generate`'s `WeightedChoice.Pick` is the shipped runtime generator's own roll over a caller-supplied `targetShapePool` (`:37`), untouched and not on the corpus's bind path |
| **F9** | `call_with_self_heal` *"never raises"* and substitutes `default_for(key, original_value)`; four specs claimed the third failure is *"recorded `unresolved`"*, and for a generation stage `items` is the **brief** | ✅ **The adapted exhaustion contract is written** in `spec-validate-heal.md` §2 Stage 3: `default_for` returns **`None`**, always — the shipped default would hand a brief field back as the model's answer (`llm_caller.py:238`) — and `unresolved` is a verdict **A-S4 writes** from the helper's `FAILED:<reason>` soft entries (`llm_caller.py:255-258`), never an exception. The helper's "never raises" is kept and justified. A-P1, A-P2 and A-P3 each point at that one statement from their AC9/AC11 |
| **F12** | A-S0 discarded a derivation it has for 31 of 84 species; AC4 also asked a five-way tie to serialise as a permutation | ✅ **Step 4's signals now apply to family-less species too** (`spec-characteristic-pool.md` §3 step 3): `leanSource: "derived-nofloor"`, `separation: null` — **not `0`**, because separation is distance from a floor. Verified: `TraitPool` is populated on **all 84** catalog rows (zero empty), as are `ElementPrimary` and `BaseRarity`. AC3 and AC4 are rewritten and **AC4b** states the tie-serialisation rule (the declared category order, with `leanSource` telling a reader it is a tie rather than a preference). Consistency carried into A-T1 (§3 steps 1-2, AC5) and A-S6 (`roleLeanMatch`, and its uniform-floor test) |
| **F8** | A-S4's permutation check asserts three **different** permuted orders *"or the run raises"* — collides with probability **1** for `k ≤ 2` and ~44% for `k = 3` | ✅ **Replaced with a check that cannot raise on legal input** (`spec-validate-heal.md` §2 Stage 2): each sample must **reproduce** `order_for(briefId, field, sampleIndex, options)` (`anchor/permute.py:26-33`) byte for byte, plus one structural unit test that `_seed_int(id, field, 0..2)` are three distinct values (`:16-23`). AC5 and the planted-violation list follow, and a new §4.5 *"cannot falsely fail"* guard was added as the mirror of the existing *"cannot fail"* one |
| **F13** | Nothing owned `rungBand` → `ActionRow.Rung`; A-S4's g2 checks a **band's** budget while `Check` resolves one row | ✅ **Rule stated: `Rung = rungBand[1]`, the ceiling** (`spec-distribution-planner.md` §3 step 4 — the only value consistent with union-to-ceiling), and **A-S4's g2 is written against it** (§2 Stage 1's F13 note, AC6c), including that a claimed `reaction` is a hard reject and a claimed `restriction` passes and is reported unchecked. A-E1 AC1b now cites the stated rule. Cross-checked against **`spec-rung-semantics.md` (A-U1)**, which landed mid-day and pins `Rung` (authored, fixes structure) apart from `effectiveRung` (per holder, fixes magnitude and cost) — so the ceiling buys the brief the ceiling's **axis budget** and nothing else; both A-S1 §3 step 4 and A-S4's g2 note say so and cite A-U1 §3.1 |
| **F14** | Duplicate ids guaranteed once A-S6 promotes — A-S3 and A-S6 write the same ids under the same root and `Corpus.load` walks the whole tree | ✅ **The move step is named, in three agreeing places**: A-S3 writes under `data/seed/actions/_rounds/round-<n>/` (§2), A-C1 declares `_rounds/` a manifest-listed prefix **excluded from the committed load** with round reads as an explicit separate `Corpus.load` (§3 step 2b, AC6c), and A-S6's promotion is a **MOVE** — the round file keeps only a `promoted` marker, never a second row (§3.4 step 6b, AC9b). Belt and braces on purpose: the exclusion keeps a mid-run tree loadable, the move keeps the committed tree honest |
| **F10** | Casing — A-C1 emits `"Area"`, `"Row"`, `"Enemy"` while mandating a cross-check that would refuse them; A-T1 is inconsistent within one file and its AC6 cannot pass | ✅ **Fixed against the `Name` functions, not the enum declarations** — which is what let PascalCase in. A-C1's envelope example, its §3 step 5 cross-check, its testing table and AC6; A-T1's `targetModeMilli`/`areaShapeMilli` keys, §3 step 4, §4, §5 and AC6; A-S1's brief example and a new casing test; A-S3's fingerprint components. Code of record: `ActionTargetModes.Name` (`ActionTargetSpec.cs:103-112`), `ActionAreaShapes.Name` (`:134-141`), `RelationKinds.Name` (`RelationKind.cs:23-26`), `ActionCategories.Name` (`ActionEnums.cs:96-104`) |
| **F19** | Three model schemas carry no `description` keys though each AC2 asserts every property has one with a negative clause, asserted mechanically | ✅ **The strings are written**, into the schemas themselves rather than the prose beside them, in A-P1, A-P2 and A-P3 — each property, each with an explicit negative clause, each modelled on the hardened `blocked` description (`adapters/demons/anchor/prompts.py:74-82`, rewritten after a real local model filled that field with `"plant"` on 2026-09-01): normal case first, then the exception, then what must **not** go in the field. Each AC2 now records that it previously asserted over nothing |
| **F2, F11, F16, F18, F20-F25** | The smaller cluster | ✅ All applied — see the table below |

### The smaller cluster, item by item

| Finding | Fix |
|---|---|
| `HasStandalonePayload` reads a `category` field that does not exist | **A-M1** §2 records it against the complete field lists (`ActionRow.cs:15-54`, `CompiledAction.cs:17-35`) and cites **A-E1** §3.0/AC1b, which owns `category` and already names this rejection as a dependant. A-M1 §6's dependency table gains the row; nothing re-derives a category from tags |
| A-M1's `guard-secondary-no-unity.ps1` criterion is vacuous | **A-M1 AC4** rewritten: the guard sets `$PluginDir = src\FusionRpg.Core\Effects\Plugins` (`guard-secondary-no-unity.ps1:9`) and enumerates only that directory (`:37-40`), so `FusionRpg.Core.Actions.Movement` is never scanned. Replaced with a `FusionRpg.Guard.Tests` case over this module's own files, plus a planted-violation test |
| A-S1's family sizes sum to 51, not 53 | **A-S1** §3 step 2 corrected to **eleven** at size 2, with the full histogram `{7:1, 5:2, 4:1, 3:3, 2:11, 1:1}` re-counted from the file |
| A-M2 missed that **A9**, not E33, owns the producer | **A-M2**'s header block now names both blocks and quotes `spec-activation-edge.md:13-14` (*"does **not** own a game-facing producer — that is `A9 movement-actions`"*) and `:168-169`; its §1 wiring-gap table gains three rows (no producer, no `HasOnActivateGrant()` fast gate, debug-only `fire-synthetic`), and §6 hazard 4 carries E33's own D6 recurrence — if A9 does not follow, the map row reads **inert** |
| A-M2 missed `LawnCoords.CellCenter`'s null-`Mouse` fallback | **A-M2** §2 quotes `LawnCoords.cs:59-71` and states the rule: the fallback returns `new Vector2(col, row)` — grid indices as a **world** position, a silent teleport to near-origin for an actor. `EntityPositionWriter` resolves through a fallible path and **drops-and-counts** when `Mouse` is null; `LawnCoords` itself is not changed, because its fallback is right for its 20 read-only callers. Test case and AC7 follow |
| A-S4's g3 penalises A-P3's deliberately honest `none` | **A-S4** §2 Stage 1: `differentiator == "none"` is **recorded and counted**, never scored down, and the `none` rate is a first-class round metric. A-P3 §2 and AC11b state the same promise from the other side — penalising it teaches the pipeline to invent a difference, the exact failure P3 was split out to prevent |
| One `id_pattern` for nine envelope kinds | **A-C1** §2 now declares **one pattern per `KindSpec`** (`adapters/base.py:30`), in a table covering all ten kinds, with `discover_edges` run once per kind. The old `action.`-only pattern silently recorded **no** cross-kind edge, since `discover_edges` only records where `id_pattern.match(value)` holds (`corpus/model.py:154`) |
| Four names for one field | **A-S1** §3 step 8 carries the resolution table: `atomFamilies` is the canonical stored name (the code of record's own word — `AtomRow.FamilyId`, `ActionSeeder.cs:61`; `IsPayoff(string atomFamily)`, `EnablerPayoffPairings.cs:26`), `allowedAtomFamilies`/`forbiddenAtomFamilies` are the brief's **permitted set** and a genuinely different thing, and `sortedAtomFamilies` is a fingerprint **rendering**, not a field. Renamed in A-C1 (§2, §3 step 4) and A-E1 (§3.0, AC1b) |

---

## 2. Required before build — still open

### F17 · the citation-drift cluster

~13 off-by-one line references across the specs — a `:12-21` for a table that starts at `:13`, a
`:16-32` for an enum that ends at `:33`, and similar. **Mechanical, low-risk, and not yet swept.** It
is the only §2 item this pass did not close: every correction above cites re-verified lines, but the
citations the corrections did not touch have not been re-walked one by one.

Not a build blocker on its own — a drifted line number costs a reader thirty seconds, where every
finding above cost a wrong decision — but it should be swept before the specs are read as reference
rather than as a review artefact.

---

## 3. What the review verified as correct

Recorded so the review's coverage is visible, not just its complaints.

**Every roster number matches**: 84 catalog species · 84 motif keys, none outside the catalog · 53 family
assignments over 19 families, no species with two · 84 themes · 28 anchor entries · the 9 non-catalog
anchors named one-for-one · four-way join **8** · `attackTempo == "steady"` on all 28 · the 14-trait
census exact to every count.

**Every vocabulary count is right**: 3 kinds · 5 categories · 8 tags · 6 target modes · 4 area shapes ·
4 relations · 6 elements · 21 statuses · 10 rarity rungs. Only the *casing* (F10, now fixed) and some
line ranges (F17, still open) were off.

**Every law held.** No model picks a number, weight, probability, duration, tier or rung. `confidence` is
correctly kept off the model. `blocked` is required in all three schemas. A-S4's deny-list extension
closes two real holes the shipped `audit_schema` leaves open — verified: it keys on `type`, and
`"string"` is not in `NUMERIC_JSON_TYPES`, so a `"pattern": "^[0-9]+$"` string slips through today.
`Instantiator` is respected as the one roll everywhere — F5 was the one place it was in doubt, and the
decision written into A-S1 §3 step 4a (authored, not rolled) keeps it that way rather than adding a
second roll beside it.

**A-S6 is the strongest spec** — the only one that struck its own prior overclaim (the *"innate climbs
with earn history"* line, which was **mine**), with complete contract, determinism and overflow
discipline. **A-S4 is the most rigorous against shipped code. A-S1 was the weakest** and carried F3,
F7, F11, F13 and F15 — and it is the module every model stage depends on, which is why five of the
second pass's decisions land in it: union-to-ceiling axes (§3 step 5), the `Rung = rungBand[1]`
collapse rule (§3 step 4), the authored target shape (§3 step 4a), the family-motif derivation
(§3 step 2b), and the optional atom-family pairing role (§3 step 6).
