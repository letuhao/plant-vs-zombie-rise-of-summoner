# creature-corpus-self-heal: todo

Owner approved all four phases 2026-09-04 (AskUserQuestion: "Approve all four phases"). Full plan:
`tasks/creature-corpus-self-heal-plan.md`. Baseline snapshot (pre-heal, 2026-09-04): 833 anchors, 217
duplicate species, 11 on-disk-not-indexed, 47 indexed-not-on-disk, attackTempo entropy 0.00 (1/5
used), rarity unresolved 68/833 (81‰), aptitude unresolved 29/833 (34‰), element unresolved 4/833,
threatBand unresolved 11/833, 98/833 generation failures.

## Phase A — deterministic fix

- [x] A1: fix the stale-duplicate write bug in `runner.py` — **done 2026-09-04**. Two-part fix,
  found by writing the regression test first: (1) `_write_species_entry` now scans every OTHER
  file already loaded into `existing_by_file` and removes this species from it before writing the
  new location; (2) a DEEPER, pre-existing bug in `_run_loop`'s own startup rebuild of
  `existing_by_file` — it called `_family_for(species_id, families)` WITHOUT
  `classified_family=entry.get("family")`, so every existing entry got re-bucketed via the
  external family-assignments.json fallback (mostly "unclassified") instead of its REAL on-disk
  path — meaning fix (1) alone silently cleaned up a phantom miscategorized dict, never the real
  stale file. Both fixed together; 2 new regression tests (single-species-moves,
  sibling-species-stays-behind), full seedsmith suite 1458/1458 green.
- [x] A2: one-time cleanup of the 217 existing duplicates — **done 2026-09-04**. Used the real
  `emit.py` functions, index-authoritative (kept whichever copy `_index.json` already pointed to,
  newest-`emittedUtc` fallback when unindexed). Also found and fixed a SEPARATE bug shape: 4
  species listed TWICE within the SAME file (`plant/unclassified.json`, 297→293 entries).
  Verified NOT data loss (a real concern raised and resolved during execution, not assumed away):
  raw index count dropped 880→840, which looked alarming until cross-checked against
  CreatureQualityReport's own real-anchor count (833 before, using index-resolution fallback) — the
  true comparison is 833→840, a NET GAIN, because 47 of the original 880 index keys pointed at
  files that never existed at all (a separate, pre-existing index/tree drift issue, unaffected by
  this cleanup). Confirmed with a 10-species spot-check (all survived, correctly resolved) and
  generation-quality parity (735/833 clean before -> 741/840 after, same failure-reason ratios).

### ✅ Checkpoint A
- [x] seedsmith suite green (1458/1458)
- [x] CreatureQualityReport shows 0 duplicates — confirmed: "840 anchor entries on disk, 840
  distinct species ids, 840 indexed — clean, every anchor entry is unique and the index matches
  the tree exactly."

## Phase B — single-pipeline rerun capability

- [x] B1: `run-control` gains a pipeline-scoped rerun mode (merge, not full reclassify) — **done
  2026-09-04**. `record.selector["pipeline"]` (read regardless of `kind`) scopes `_classify_one` to
  `run_one_species(pipelines=[scope], initial_context=<existing entry's fields>)` — the
  `initial_context` seeding matters for real: `kit-shape` has its own `posture_resource` validator
  reading `context["aptitudePrimary"]` from an EARLIER pipeline, which a naive standalone rerun
  would leave missing. `_write_species_entry` gained `merge_from` — folds the reran pipeline's own
  fields onto the existing entry, merges `_provenance.attempts`/`confidence`/`minorityValues`
  per-key rather than replacing wholesale. A real bug found live during the smoke test (not just
  theorized): `rerun --pipeline kit-shape --species X,Y,Z` silently ran a FULL 8-pipeline
  reclassification (49 calls for 3 species, not 3) because `cli.py`'s `_selector_from_args` picked
  `--species` OR `--pipeline` via if-elif, discarding whichever lost — fixed so `--pipeline`
  attaches as an execution-scope key alongside any species-selecting flag. 8 new tests (4 in
  `test_run_runner.py` for the merge mechanism, 3 in `test_cli.py` for the selector-combining fix,
  1 already covered) — full suite 1465/1465.

### ✅ Checkpoint B
- [x] seedsmith suite green (1465/1465, `test_candidate_assembly.py` collection error confirmed
  pre-existing/unrelated — a different work stream's module, not touched by this plan)
- [x] live smoke-tested for real: `rerun --pipeline kit-shape --species Peashooter,SunFlower,WallNut`
  (4 calls for 3 species — 1 repair round). Verified by hand: `aptitudePrimary`/`elementPrimary`/
  `rarity` byte-identical for all 3; `reach` (kit-shape's own field) genuinely re-judged for
  Peashooter (long -> short), proving the pipeline actually ran fresh, not a no-op. The FIRST
  (mis-scoped, pre-fix) attempt accidentally did a real full reclassification of the same 3 species
  — checked for harm: every substantive field came back identical except WallNut's family bucket
  (a legitimate, expected run-to-run variance for an open-vocabulary field), and A1's cleanup fix
  correctly removed the stale old file live, an unplanned but welcome real-world proof of A1 too.

## Phase C — the self-heal (real model calls)

- [x] C1: strengthen the `kit-shape` prompt, wire it into permutation/voting — **done 2026-09-04**.
  Brief now lists `attackTempo`'s 5-value vocabulary explicitly (permuted order, matching every
  other pipeline's own convention) plus differentiating guidance per rung (ponderous/slow/steady/
  quick/flurry) and names the audited 100%-steady failure directly so the model isn't just guessing
  at why the extra guidance exists. `attackTempo` joined both `_PERMUTABLE_FIELD` (orchestrator.py)
  and `VOTED_FIELDS` (vote.py) — the one explicitly-named "ask first" boundary in this whole plan,
  covered by the owner's own full-plan approval. `CALLS_PER_OBSERVED_SPECIES` 16->18,
  inferred-basis count 18->20 (kit-shape moved from 1 unvoted call to 3 voted) — updated everywhere
  it was hardcoded (10 call-count assertions across 3 files, all real fixes not workarounds). Full
  suite in scope: 1465/1465 (10 unrelated failures in `seedsmith/adapters/actions/` confirmed
  pre-existing/concurrent — different subsystem, different file tree
  `data/seed/actions/_briefs/round-1.json`, already `git status`-modified before this session
  touched it).
> ### ✅ RESOLVED 2026-09-04 21:20 (owner-approved) — **C2 left two starter plants on the top rarity rung; both corrected, and a guard added**
>
> **Fixed, not just reported.** Both rows are corrected in place with full `_provenance.manualCorrection`
> (from/to/why), and the creature suite is now **183/183 green** (was 4 red).
>
> | Species | was | now | how it was caught |
> |---|---|---|---|
> | `Peashooter` | `almanac`, **6** variants | `cultivated`, `[normal, mutated]` | only 6-variant row in 841; no `ancient`; `confidence.rarity` was **split** with minority `sprout`; own reason text says *"a nuisance or minor obstacle"* |
> | `SunFlower` | `almanac`, **7** variants | `cultivated`, `[normal, mutated]` | only 7-variant row; own reason says *"a passive resource producer with no offensive or destructive capabilities"* |
>
> ⚠️ **The one that should worry you: `SunFlower`'s `confidence.rarity` was `high`, not `split`** — the
> classifier was *confidently* wrong, and only the variant-count outlier gave it away. Worth a prompt
> look; the rest of the `almanac` rung is coherent (six `Ultimate*` units, `JacksonDriverBoss`,
> `ZombieBoss`), so this is not a systemic rung problem.
>
> ⭐ **A guard now enforces what the data already implied.** Across 841 rows the variant count tracks
> the rarity rung tightly and monotonically (chaff 0, sprout 1, cultivated 1–2, fused/chimeric/heirloom
> 2–3, firstseed 3, sunwoven 3–4, almanac 4) — but **nothing checked it**, and the only existing
> assertion covered two hand-named species, which is why two bad rows sat there at once.
> `tests/FusionRpg.Core.Tests/Creatures/VariantCountBandTests.cs` now sweeps every anchor through
> `_index.json`, names offenders by species id, keeps the `unresolved` exemption **bounded** (fails if
> unclassified rows exceed 5% of the corpus), and asserts the ladder stays monotonic. Falsified:
> restoring Peashooter's old row reddens it with `Peashooter: rarity 'almanac' allows 4..4 variants, has 6`.
>
> Also updated `SpeciesExpanderTests`' trait expectation — C2 moved traits to lower-kebab
> (`Defensive` → `defensive-line`), which is corpus-wide: **2802 lower-case tokens against 79
> upper-first**. Same maintenance path that test's own comment already documents for C3.
>
> <details><summary>Original report (kept — its first hypothesis was wrong and the correction is instructive)</summary>
>
> ### ⛔ Handoff finding from the battle-timeline session, 2026-09-04 20:40 — **C2's second pass left a live inconsistency**
>
> Reported rather than fixed: this is the creature stream's surface, and the fix is a pipeline/index
> decision, not a test edit. Found while verifying whether `combat-unification`'s **S5** could be
> unblocked (it cannot, and this is why).
>
> **Four Core tests are red, and they share one root cause** (`dotnet test --filter ~Creatures|~Species`
> → **4 failed / 179 passed**, after corpus writes had stopped at 20:27):
>
> | Test | Expected | Actual |
> |---|---|---|
> | `SpeciesExpanderTests.Peashooter_expands_without_throwing_and_carries_its_own_theta` | `Cultivated` | **`Almanac`** |
> | `…Peashooter_carries_every_catalog_runtime_field_straight_from_its_real_anchor` | `["normal","mutated"]` | `["normal","mutated","corrupted","blessed","cursed",…]` |
> | `…Real_anchors_variant_counts_already_fall_inside_their_rarity_bands` | in band `2–3` | **`6`** |
> | `SpeciesCatalogDiffTests.Fields_that_genuinely_match_are_never_reported_as_differences` | `baseRarity` matches | reported as differing |
>
> ⛔ **Correcting my own first hypothesis, which was wrong — please ignore any earlier "index drift"
> reading of this.** I initially matched on the file `legume-projectile.json` (which still carries
> `variants: ["normal","mutated"]`) and concluded the index had re-pointed Peashooter. **It has not.
> The index and the anchors are self-consistent.** Traced properly:
>
> ```
> _index.json:  "Peashooter"      -> "plant/sentinel-flora.json"
>               "JalaPeashooter"  -> "plant/legume-projectile.json"
>
> sentinel-flora.json    row  speciesId=Peashooter      rarity=almanac  variants=6
>                             [normal, mutated, corrupted, blessed, cursed, shiny]
> legume-projectile.json row  speciesId=JalaPeashooter  rarity=fused    variants=2
> ```
>
> The 2-variant row I first matched is **JalaPeashooter's, not Peashooter's.** So this is **not** a
> lookup bug: **C2's reclassification genuinely moved Peashooter into `sentinel-flora` and promoted it
> to `almanac`** — the top rarity rung — with 6 variants.
>
> ⭐ **The real finding, and it is internal to the corpus rather than a stale test:** whatever family is
> right, **the row violates its own rarity/variant band.**
> `Real_anchors_variant_counts_already_fall_inside_their_rarity_bands` computes an allowed band of
> **2–3** for that row and finds **6**. The corpus is inconsistent with its own banding rule, so at
> least one of `rarity` / `variants` is wrong regardless of the family question.
>
> **The judgement call is yours:** is the starter plant at top rarity intended? If not, this is a
> classification regression from the kit-shape rerun and the band violation is the symptom that caught
> it.
>
> ⚠️ **Please do not close this by updating the four tests.** `Almanac` is the top rarity rung, and the
> starter plant landing there with 6 variants reads as a classification regression, not a new truth. The
> tests are currently doing their job.

> </details>

- [~] C2: redeploy kit-shape corpus-wide (`rerun --pipeline kit-shape --all`) — **first pass done
  2026-09-04, 641/904 succeeded, 263 failed; root-caused live, second pass in flight.**
  - First pass (`workers=4`, 2860 calls, ~80 min): 641 completed, 263 failed. Investigated rather
    than accepted — split the failures: 64 genuinely never-classified (correctly refused, matches
    B1's own "cannot merge into content that was never classified" design), but **199 had real,
    intact anchor content that the run simply could not SEE.**
  - **Real root cause found and fixed, not theorized**: `_load_existing_anchors` (the function
    EVERY run-control entrypoint uses to know what's already classified) read `_index.json`'s own
    VALUE SET to decide which files to open — it never scanned the tree directly. Any species whose
    real file the index had drifted away from (exactly the staleness class A1/A2 exist to fix)
    became invisible, and `_rewrite_index` then rebuilt the index FROM that already-incomplete
    read — making the loss permanent and self-reinforcing across runs, not one-off. Confirmed live:
    `CherryBomb`'s fully real, intact `plant/cherry.json` entry (family `"Explosive Flora"`,
    correctly classified) was silently unreachable this way. Fixed: now scans
    `anchors_dir.rglob("*.json")` directly, `_index.json` stays the fast lookup, never the source
    of truth. Added `sys.stderr` logging for `_finalize`'s error path too (previously silently
    discarded WHY a species failed — this whole diagnosis needed a live repro to recover the real
    exception text, a real observability gap now closed for future runs). Also updated one
    downstream test (`test_characteristic_pool.py::AttackTempoExclusionTests::
    test_live_anchor_tree_attack_tempo_is_constant`) that asserted the NOW-INVALID premise
    "attackTempo is always steady" as its own regression guard — converted to a clearly-flagged
    skip naming the real, undecided follow-up question (should `compute_scores` read attackTempo
    now that it discriminates?) rather than silently deleted or left failing.
  - Index rebuilt from a real disk scan (840, recovering the ~12 that the first pass's
    `_rewrite_index` had already started dropping mid-run). 4 new tests (the loader fix, verified
    both against a corrupted-index fixture and via re-tracing the real CherryBomb case) — full
    suite in scope: 1466/1467 (+1 confirmed-transient unrelated flake in the SAME "actions"
    subsystem already flagged pre-existing, re-ran clean in isolation).
  - **Second pass done**: 196/199 succeeded (the loader fix confirmed working at scale, not just
    in the unit test). 3 (`RedEmeraldUmbrella`, `SilverCorn`, `SunSquash`) failed twice in a row
    with "model call failed after 2 attempts: timed out" — checked for a content-length cause
    (normal 99-199 char lore, nothing unusual) and left as a small, honestly-reported residual gap
    (3/904, 0.3%) rather than retried indefinitely for diminishing returns; retryable any time with
    `rerun --pipeline kit-shape --species RedEmeraldUmbrella,SilverCorn,SunSquash`.
  - The 64 genuinely-unclassified species are correctly left for `start`/`rerun` without
    `--pipeline` (a first classification, out of THIS plan's scope) if the owner wants them filled
    in later.
  - **C2 final: 837/904 kit-shape-current (925 species total minus the 64 never-classified minus
    the 3 residual timeouts), corpus-wide redeploy complete.**
> ### ⛔ C3 is marked done, but **9 rows still carry `attackTempo: "unresolved"`** — and they hard-fail the species import
>
> Found 2026-09-04 while clearing cross-stream reds. C3's line reads *"self-heal every currently-unresolved
> field"*; `attackTempo` was not covered.
>
> **It is not cosmetic — it fails the import outright**, and fail-closed means all-or-nothing:
> ```
> FusionRpg.Data.Tests.CreatureSpeciesImportCliTests
>   A_real_import_against_the_real_committed_tree_succeeds_and_writes_a_real_store   FAILED
>   A_stale_committed_file_refuses_the_whole_import_and_writes_nothing               FAILED
>   -> 'SeaMagnet': attackTempo 'unresolved' has no entry in creature-shape.v1.json
> ```
> The second failure is collateral: it asserts the refusal message contains "stale", but the import dies
> on SeaMagnet before it ever reaches the staleness check — so **one unhealed row masks an unrelated
> test's real subject.**
>
> **The 9:** `SeaMagnet`, `CactusPumpkin`, `SunBomb`, `Chrysantheautumn`, `DoomSeaShroom`, `CherrySplit`,
> `BloverPot`, `JackboxJumpZombie`, `ImpZombie` — 9 of 831 rows.
>
> ⛔ **Deliberately not healed by me.** `attackTempo`'s vocabulary is a closed five
> (`ponderous` 3000 / `slow` 2400 / `steady` 1500 / `quick` 900 / `flurry` 500 in `creature-shape.v1.json`)
> with **no sanctioned default** — the `creature-fix-unresolved` skill covers `threatBand` *only*, and says
> so precisely because the other fields have no defensible fallback. Picking a tempo is a classification
> call, which is this pipeline's job and not a hand edit. A targeted
> `run --species SeaMagnet,CactusPumpkin,…` re-classify is the fix.

- [x] C3: self-heal every currently-unresolved field (`rerun --unresolved`) — **done 2026-09-04,
  110/110 succeeded, 0 failed, 2159 calls.** Full 8-pipeline reclassification for every species
  with at least one unresolved voted field (rarity/aptitude/element/threatBand/attackTempo),
  benefiting from the SAME loader fix C2 needed — 0 failures throughout confirms it holds at scale
  under the harder (full-reclassify, not scoped) path too.

### ✅ Checkpoint C
- [ ] both reruns state=completed (checked via `creatures run status`, not assumed)

## Phase D — verification

- [x] D1: before/after report — **done 2026-09-04.** Real before/after, both from
  `CreatureQualityReport`, not a claim:

  | | Before | After |
  |---|---|---|
  | Duplicate anchor entries | 217 (26%) | 0 |
  | attackTempo entropy | 0.00 (1/5 used) | 0.94 (5/5 used) |
  | aptitudePrimary unused values | Ferocity | none (12/12 used) |
  | rarity unresolved | 68/833 (81‰) | 50/840 (59‰) |
  | aptitudePrimary unresolved | 29/833 (34‰) | 17/840 (20‰) |
  | elementPrimary unresolved | 4/833 (4‰) | 2/840 (2‰) |
  | threatBand unresolved | 11/833 (13‰) | 10/840 (11‰) |
  | Generation success | 735/833 (88%) | 767/840 (91%) |

  A found-and-fixed root cause during execution, not just the four planned items: `_load_existing_
  anchors` trusted `_index.json`'s own value set instead of scanning disk, so any species the index
  had drifted away from became permanently, silently unreachable to every future run. Fixed to scan
  the tree directly; recovered 196 of 199 real species this uncovered live (3 residual transient
  timeouts, retryable any time, not a data problem). Also found and fixed 2 tests that had baked in
  facts about the live, evolving corpus as permanent fixtures (a hardcoded `pea.json` path, a
  hardcoded trait value) — both now resolve/assert against CURRENT reality instead. One deliberate,
  clearly-flagged skip left for a downstream subsystem's own call (`characteristic_pool`'s
  attackTempo-exclusion premise is now false; whether to re-include it in scoring isn't this plan's
  decision).

  Remaining, honestly reported, not closed by this plan: rarity's own unresolved rate (59‰) is
  still above the classification quality gate's 50‰ threshold and above every other field — the
  natural next self-heal target, structurally identical to what `attackTempo` needed (a
  `_PERMUTABLE_FIELD`/vote reweight or a prompt strengthening), not attempted here since it wasn't
  a named finding going in. 64 species remain genuinely unclassified (never had a `start`, out of a
  `--pipeline`-scoped plan's own scope by design).

## Phase E — rarity, the same fix class applied again (owner-directed follow-up, 2026-09-04)

- [x] **E1: strengthen the `identity` pipeline's rarity guidance** — reused
  `docs/architecture/item/ssot-rarity.md` §3.3's own canonical one-line description per rung
  (already-authored, reviewed content — nothing invented) instead of the single generic sentence
  the prompt had for all ten rungs. `rarity` was already wired into voting/permutation (unlike
  `attackTempo`), so only the prompt text changed, no orchestrator/vote.py wiring needed.
  - Smoke-tested on 8 real currently-unresolved species first: **8/8 resolved, 0 unresolved**
    (`DiamondImitater` landed on `almanac` — the rarest tier — with high confidence, a concrete
    sign the differentiation is working, not just noise).
  - Redeployed corpus-wide (`rerun --pipeline identity --all`, workers=4, 2584 calls, ~106 min):
    **840/840 real species succeeded**, the 64 failures exactly match the known never-classified
    count (correctly refused). Zero unexpected failures — the loader fix from Phase C held.
- [x] **E2: measured, honest result** — real before/after:

  | | Before | After |
  |---|---|---|
  | rarity unresolved | 50/840 (59‰, worst field) | **15/840 (17‰, now 2nd-best)** |
  | rarity entropy | 0.68 | 0.64 |
  | rarity used/possible | 9/10 (sunwoven unused) | 9/10 (sunwoven still unused) |
  | sprout share | ~0% | 5% |
  | grafted share | 1% | 3% |
  | almanac share | 1% | 2% |
  | fused share | 40% | **55%** |

  **Honest verdict, not spun**: the unresolved-rate fix worked as well as `attackTempo`'s did, and
  gave real, meaningful population to rungs that were previously near-zero (`sprout`, `grafted`,
  `almanac`). But `fused`'s dominance got MORE extreme, not less (40%→55%) — entropy actually
  ticked down slightly. This is the answer to the earlier open question about redistribution: it
  confirms giving the model better judgment criteria was the principled move (not guessing at a
  target curve), and the result it produced — MORE `fused`, not less — may be an ACCURATE signal
  about this corpus's own composition (many species are literally named as hybrids —
  `GarlicPumpkin`, `IceShroom` — and `fused`'s own canonical description is "two natures in one
  object, the game's own word"), not a remaining defect to force-correct. Still no target
  population curve exists anywhere in the repo; nothing here invents one. `sunwoven` staying at
  0/840 even with much better guidance is a real, standing question — either genuinely no species
  in this corpus reads as "made of sun, not of matter," or there's a further gap not yet
  diagnosed.

## Phase F — deterministic auto-assign fallback + skill (owner-directed, 2026-09-04)

- [x] **F1: investigated which fields have a real, defensible stats-based fallback** — only
  `threatBand` does (`creature-threat.v1.json`'s own `inferredDefaultRung`, already committed and
  sanctioned, previously wired only for "no computable score," never for "vote never converged").
  `aptitudePrimary`/`rarity`/`elementPrimary` have NO equivalent real signal anywhere in the repo —
  `rarity`'s own field description explicitly forbids deriving it from threatBand/danger; forcing a
  rule for any of the three would be inventing one, not deriving it. These three correctly stay
  `"unresolved"`/reported, not auto-assigned.
- [x] **F2: built `fix_unresolved`** (`runner.py`) + `resolve_unresolved_threat_band` (`derive.py`)
  — a deliberate, on-demand FIX STEP, never automatic during classification, zero model calls,
  idempotent, honestly provenance-stamped (`confidence.threatBand = "deterministic-fallback"`,
  never faked as a real LLM judgment). CLI: `creatures run fix-unresolved [--dry-run]`. 7 new tests
  (2 `derive.py`, 5 `runner.py` — resolves correctly, scope boundary holds, no-op when nothing to
  fix, dry-run writes nothing, idempotent).
- [x] **F3: skill written** — `.claude/skills/creature-fix-unresolved/SKILL.md`, documenting the
  scope boundary (why threatBand only) and the real 2026-09-04 result.
- [x] **F4: run for real** — 10 species (`Jalakelp`, `DoomChomper`, `SwordStar`, `DoomBlover`,
  `IceBean`, `MagnetDoom`, `UltimateTorch`, `HypnoQueen`, `HypnoTorch`, `CherryTorch`) had
  `threatBand: "unresolved"`; all 10 resolved to `raider` (the sanctioned default) — which also
  happened to be the one threatBand value the corpus had never used at all, closing two audit
  findings in one step. Verified via `CreatureQualityReport`: threatBand unresolved 10‰→**0‰**,
  catalog coverage 9/10→**10/10 used**, entropy 0.39→0.46. Full suite 1474/1474 (+1 skip).

## Phase G — rarity/identity follow-up, mixed result (owner-directed, 2026-09-04)

- [x] **G1: `sunwoven` investigated** — not a hard model/code limit (confirmed via grep, no cap
  mechanism exists); it WAS genuinely considered by the model (recorded as the minority vote for
  2 species, including a near-perfect thematic fit `AcientSunNut`) but consistently lost 2-1 to
  `almanac`. Diagnosed as a calibration gap: "made of sun, not of matter" doesn't concretely
  connect to "solar-themed plant" the way `almanac`'s "everyone knows its name" concretely connects
  to "famous."
- [x] **G2, attempt 1: sunwoven guidance sharpened, smoke-tested, NOT proven** — added a concrete
  fused-vs-sunwoven distinction to the prompt. An 8-species smoke test on real sun-themed species
  (`SunFlower`, `AcientSunNut`, `TorchSunflower`, ...) still landed 0/8 on `sunwoven` — the
  refinement did shift the minority-vote signal around but didn't flip the outcome on this sample.
  **Not redeployed corpus-wide** — an unproven fix isn't worth ~2500 real model calls. Left as a
  real, honestly-reported open question, not claimed fixed.
- [x] **G3: root-caused via direct model interrogation** — built a diagnostic script
  (`call_model` used directly, bypassing the pipeline) that ran the real `identity` classification
  call on `AcientSunNut` and `SolarSunflower`, then asked the model, same chat session, plain text,
  why it didn't pick `sunwoven`. Both answers were coherent and consistent: the wording "OWN NATURE
  is solar/light energy itself, not matter" reads as a literal composition claim — no biological PvZ
  creature, not even `SolarSunflower` (the strongest sun-plant in the corpus), can be "made of light,
  not matter." **Not a model limit and not a hidden cap** — the bar itself was unreachable by design.
  Confirmed the shared SSOT (`docs/architecture/item/ssot-rarity.md` §3.3) is fine as written —
  `sunwoven` already works for crafted items (`sunwoven-almanac-90.json` and others) — so only the
  creature-`identity` pipeline's own prompt needed to change, not the shared doc.
- [x] **G4: rewritten as an achievable superlative, proven, redeployed** — reframed `sunwoven` from
  an absolute metaphysical bar to a reachable one: "the apex of solar power within its own family,"
  keyed off naming/ability language (`ultimate`/`primordial`/`emperor`-tier descriptors) rather than
  narrative flourish — the dump's `flavorIntroduce` is `None` for every sun-plant checked, so there's
  no lore text to hang a literary bar on. Full suite re-verified green (1474 passed + 1 skip) before
  any redeploy. Smoke test (`AcientSunNut`, `SolarSunflower`) landed **2/2 on `sunwoven`, high
  confidence** — proven before redeploying further, matching this session's own established
  discipline. Redeployed to the remaining 35 real sun-themed species found by name-matching
  `sun`/`solar` in the plant dump (34 completed, 1 — `SunCabbage` — skipped: never classified in the
  first place, out of scope for a pipeline-scoped rerun).
  **Final result:** 4 real `sunwoven` assignments (`AcientSunNut`, `SolarSunflower`,
  `UltimateSunNut`, `UltimateSunflower`) — exactly the species whose own names mark them as the
  peak/ultimate tier of their line. Mechanism-flavored "Ultimate" variants (`UltimateSunGatlingPuff`,
  `UltimateSunMagnet`, apex-tier weapons rather than apex-tier solar beings) correctly landed
  elsewhere (`chimeric`), showing the new bar discriminates on the right axis, not just the word
  "Ultimate." `CreatureQualityReport` confirms: rarity catalog coverage **10/10 used, 0 unused** (was
  9/10 with `sunwoven` unused); `Sunwoven` balance in real simulated combat: n=4, mean win share 75%,
  median 75% — sits between `Fused`/`Chimeric` and `Heirloom`/`Firstseed`, a plausible near-top rung,
  not a broken outlier.

### ✅ Checkpoint D/E/F/G — plan closes, sunwoven resolved (owner directive: "resolve it or replace
  it", not tolerated as a permanently-unusable enum value)

## Phase H — rarity gets a deterministic power fallback too (owner-directed, 2026-09-07)

Reopens F1's own scope boundary on the owner's explicit direction: *"rarity is our game mechanism
not pvz engine mechanism... if llm cannot solve it, just define a deterministic engine to solve it
(fall back)... stronger species will be rarier."* F1 was correct that no fallback existed in the
repo **at the time** — this phase authors one, on purpose, rather than treating "no signal yet" as
permanent.

- [x] **H1: `creature-rarity-power-fallback.v1.json`** (new tunable) — a rank-preserving
  `threatBand -> rarity` correspondence over both closed 10-value ladders, reusing
  `creature-threat.v1.json`'s own already-validated power banding (real p10..p90 deciles of a measured
  toughness/damage score) rather than inventing a second, independent curve. "Stronger species are
  rarer" falls out of the two ladders' own ascending order (nuisance→calamity, chaff→almanac), not
  a new formula.
- [x] **H2: `resolve_unresolved_rarity` + `load_rarity_power_fallback`** (`derive.py`) — same
  `(value, was_deterministic)` contract as `resolve_unresolved_threat_band`. Only fires when
  `rarity == "unresolved"` AND the species' `threatBand` is itself a real rung (resolved, or
  resolved earlier in the SAME fix pass) — no signal in, no guess out. 5 new tests
  (`test_anchor_derive.py`): pass-through when already resolved, resolves from a resolved
  threatBand, stays unresolved when threatBand has no signal either, and the mapping is a genuine
  rank-preserving bijection over both ladders.
- [x] **H3: `fix_unresolved` chains the two fallbacks in one pass** (`runner.py`) — a species with
  BOTH fields unresolved gets `threatBand` defaulted first, then `rarity` derived from that
  freshly-fixed value, not the pre-fix "unresolved" string. Each fixed FIELD (not species) is its
  own report entry (`{"speciesId","field","before","after"}`) since one species can now produce two
  fixes. 5 new/updated tests (`test_run_runner.py`): resolves rarity from an already-resolved
  threatBand, chains off a same-pass threatBand fix, leaves an already-resolved rarity untouched,
  leaves rarity unresolved when threatBand truly has no value, aptitude/element still untouched.
  CLI message (`cli.py`) updated to name both fields. Skill doc
  (`.claude/skills/creature-fix-unresolved/SKILL.md`) rewritten with the new table and reasoning.
- [x] **H4: full seedsmith suite reverified** — 3103 passed, 1 skipped (own new/changed tests
  included); the 14 failures seen in a full run are pre-existing, unrelated atom-family-count drift
  (100 vs 109 across `test_usage_stats.py`/`test_nodegen_vocab.py`/`test_distribution_planner.py`/
  etc., a different concurrent session's action-corpus content work — confirmed no overlap with any
  file this phase touched).
- [x] **H5: run for real against the live corpus, honestly reported** — `creatures run fix-unresolved
  --dry-run` against the real committed `data/seed/creatures/species` tree reported **0 field-fixes**.
  `CreatureQualityReport` confirms why: `rarity` and `threatBand` both already show 0/840 unresolved
  today (rarity's own 15/840 from Phase E's 2026-09-04 result had already been closed by later,
  undocumented work by the time this phase ran) — `aptitudePrimary` is the field actually open now
  (11/840, 13‰), and it has no equivalent sanctioned fallback, so it correctly stays unresolved.
  **Nothing was force-fixed to manufacture a result.** The mechanism ships as standing
  infrastructure: the next classification rerun or corpus growth that leaves any species' `rarity`
  unresolved is closed automatically, without rebuilding this from scratch.

### ✅ Checkpoint H — rarity has a real deterministic fallback, proven safe against the live corpus
- [x] `resolve_unresolved_rarity`/`load_rarity_power_fallback` unit-tested in isolation (5 tests)
- [x] `fix_unresolved` integration-tested chaining both fields in one pass (5 tests)
- [x] Full seedsmith suite green modulo pre-existing, unrelated atom-family drift
- [x] Real dry-run against the live corpus confirms the mechanism is inert today (nothing left to
  fix) without ever needing to fabricate a non-zero result to prove it works

## Phase I — aptitudePrimary gets an INVENTED flat default, not a derivation (owner-directed, 2026-09-07)

Owner asked for the same treatment as Phase H's rarity fallback. Measured first, same discipline as
every other fallback decision in this plan — the measurement came back negative, and the owner's
own direction on hearing that was explicit: *"dont say data don't it, we make up it"* — invent a
flat default rather than derive one that doesn't exist, since aptitude is this game's own mechanism
too.

- [x] **I1: measured whether a stats-based derivation holds, before building anything** — joined
  every species with a computable power score (151/840, `observed`/`stated` basis) against its
  resolved `aptitudePrimary`, and ran the same one-way separability check that validated rarity's
  threatBand mapping. Result: **F≈1.34** (noise — rarity/threatBand's real ordinal signal was
  nowhere near this weak), several categories with n=1-2 samples, stdev routinely larger than the
  mean. **Worse: 10 of the 11 real unresolved species have no computable score at all** — a
  stats-based classifier would have nothing to read for 91% of the real cases regardless of
  accuracy. Conclusion reported honestly: no real signal exists, unlike rarity. `elementPrimary`
  checked too for completeness (F≈1.68, equally weak; also currently 0/840 unresolved, moot today).
- [x] **I2: owner overrode the negative finding on purpose** — direction was to invent a flat
  default rather than leave it unresolved, explicitly NOT to represent it as derived from anything.
  `data/tuning/creature-aptitude-fallback.v1.json` (new) carries `inferredDefaultAptitude: "Onslaught"`
  — picked because it is already the single most common aptitude in the real 840-species corpus
  (332/840, 395‰, measured directly), so the 11 residual species nudge an already-dominant category
  by ~1% rather than creating a new population anomaly. The tuning file's own `_note` says plainly
  this is invented, never "calculated."
- [x] **I3: `resolve_unresolved_aptitude` + `load_aptitude_fallback`** (`derive.py`) — same
  `(value, was_deterministic)` contract as the other two, but NOT a `resolve_unresolved_*` sibling
  that derives from another field — nothing chains into it, by design, since there is nothing to
  derive from. 3 new tests: pass-through when resolved, resolves to the flat default, the default
  itself names a real aptitude.
- [x] **I4: wired into `fix_unresolved`** (`runner.py`) as a third, independent field alongside
  threatBand/rarity — `posture`/`pure` (both functions of `aptitudePrimary`) are recomputed in the
  same write, mirroring exactly what the real classification path already does at
  `_finalize` when `aptitudePrimary` is in the merged output, so the fix never leaves those two
  derived fields stale relative to the new value. 5 new/updated tests (`test_run_runner.py`):
  resolves to the flat default, posture/pure recompute, idempotent, dry-run never writes, an
  already-resolved aptitude stays untouched (including its own posture/pure). CLI message and skill
  doc (`.claude/skills/creature-fix-unresolved/SKILL.md`) rewritten to distinguish DERIVED (threatBand,
  rarity) from INVENTED (aptitude) fallbacks explicitly, so a future reader never mistakes one for
  the other.
- [x] **I5: full seedsmith suite reverified** — 3114 passed, 1 skipped (11 new tests included); the
  11 failures in a full run are the same pre-existing, unrelated atom-family-count drift from
  Phase H's own verification (confirmed no overlap with any file this phase touched).
- [x] **I6: run for real against the live corpus** — `--dry-run` correctly previewed all 11 real
  unresolved species resolving to `Onslaught`; applied for real
  (`Tower_iceGloom`, `ZombieEndoFlame`, `AllPeater`, `NutTorch`, `BloverPot`, `EnumValue265`,
  `EnumValue267`, `Extract_single`, `Refrash`, `EnumValue268`, `SnorkleZombie`). `CreatureQualityReport`
  confirms: **"Unresolved rate per voted field" is now completely empty** — every voted field
  across all 840 species is resolved, for the first time, by either a real classification or an
  honestly-labeled deterministic fallback. Re-running the fix confirms idempotency (0 field-fixes).

### ✅ Checkpoint I — aptitude gets an honest, invented fallback; the corpus has zero unresolved fields left
- [x] The negative empirical finding was measured and reported BEFORE building anything, not
  skipped to get to the owner's preferred answer faster
- [x] The owner's override is applied exactly as directed, and documented as an override, not
  quietly reframed as a derivation it isn't
- [x] `resolve_unresolved_aptitude`/`load_aptitude_fallback` unit-tested in isolation (3 tests)
- [x] `fix_unresolved` integration-tested including the posture/pure recompute (5 tests)
- [x] Full seedsmith suite green modulo the same pre-existing, unrelated atom-family drift
- [x] Real run against the live corpus: all 11 genuinely-unresolved species fixed, verified via
  `CreatureQualityReport`, idempotency reconfirmed — a real, non-trivial result this time, not a
  already-closed no-op

## Phase J — the propagation chain, and closing the one CI gap it exposed (2026-09-07)

Fixing the anchor alone was not the end of it — Phase I's aptitude fix had to be pushed through the
whole `creature-seed` chain to become real, and doing that live surfaced a real, standing CI gap.

- [x] **J1: pushed the fix through the full chain, verified at each step, not assumed** —
  `CreatureSpeciesGen` (829 -> 840 generatable species), `CreatureSpeciesImport` (live `creature_species`
  table 829 -> 840 rows, server stayed healthy), `CreatureBuildPlanGen` (67 -> 68 of 84 shipped species
  planned — this is what actually closed `species-build`'s own G3 for `AllPeater`, see that plan's
  todo file). Each step's own real console output checked, not inferred.
- [x] **J2: found the chain's own already-frozen fusion-recipe seed had gone stale** —
  `data/generated/creatures/_fusion-recipes.json` (Phase 8/T8.3) stopped matching a fresh deterministic
  build (709->713 eligible outputs, 695->699 deterministic recipes) purely because more same-rung
  species now exist to pair; the Almanac/Sunwoven shortfall itself never moved (still exactly 14
  deficits). Fixed by re-running `python -m seedsmith.adapters.creatures.fusion.reconcile` — the
  freeze-on-commit `corpusContentHash` (scoped to each deficit's OWN candidate pool, never a
  whole-corpus hash) correctly reused all 14 existing gap-fill picks with **zero new model calls**.
  Two stale hardcoded-literal test assertions fixed to match (`test_fusion_recipe.py`'s
  `test_real_corpus_end_to_end`; `FusionRecipeReconcileTests.EligibleOutputs_...`); the third,
  `CreatureRecipeCatalogTests`'s `695 + 14`, fixed to `699 + 14`.
- [x] **J3: the real gap this exposed — fusion-recipe-reconcile had a working `--check` but no CI
  wiring, unlike its two siblings.** `.github/workflows/ci.yml` already gates
  `CreatureSpeciesGen --check` (species-generator) and `CreatureBuildPlanGen --check` (species-build
  plan) — the exact two other links in this chain that commit a generated tree. The fusion-recipe
  seed had the identical shape of risk and the identical `--check` mechanism already built (T8.3),
  just never wired to CI the same way — this is *why* the staleness this phase found was only
  caught by an incidental full local test run, not by the build. Closed by adding a matching
  "Fusion-recipe seed staleness guard" step (`ci.yml`, beside the other `creatures`-prefixed seedsmith
  steps), same throw-on-nonzero pattern, verified locally with the exact command and working
  directory CI will use (`python -m seedsmith.adapters.creatures.fusion.reconcile --check` from
  `tools/seedsmith`, exit 0).
- [x] **J4: full affected-area suite reverified after every step above** — the targeted
  creature-recipe/species-build C# slice (46 tests) green twice in isolation; seedsmith
  `test_fusion_recipe.py` green (31/31). A full `dotnet test tests/FusionRpg.Core.Tests` run shows
  11-14 *other* failures, but the exact SET changes between consecutive runs on identical code
  (Siege/District-assault/Domain-encounter tests, nothing touching creatures) with 11 concurrent
  `dotnet.exe` processes confirmed running at the time — build contention from other active
  sessions, not a regression; a targeted, isolated filter is the trustworthy signal here, not the
  noisy full-suite number.

### ✅ Checkpoint J — the chain is consistent end to end, and the gap that let it drift silently is closed
- [x] Every real downstream artifact (concrete species, live DB, species-build plan, fusion-recipe
  seed) matches the current anchor corpus — verified via each tool's own real output, not assumed
- [x] The CI gap that let the fusion-recipe seed go stale silently is closed with a guard matching
  its two siblings' own established pattern, verified locally with CI's exact command
- [x] Zero new model calls were spent closing any of this — every "reconcile" was a pure
  recomputation over already-resolved data
