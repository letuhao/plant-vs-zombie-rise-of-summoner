# demon-corpus-self-heal: todo

Owner approved all four phases 2026-09-04 (AskUserQuestion: "Approve all four phases"). Full plan:
`tasks/demon-corpus-self-heal-plan.md`. Baseline snapshot (pre-heal, 2026-09-04): 833 anchors, 217
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
  DemonQualityReport's own real-anchor count (833 before, using index-resolution fallback) — the
  true comparison is 833→840, a NET GAIN, because 47 of the original 880 index keys pointed at
  files that never existed at all (a separate, pre-existing index/tree drift issue, unaffected by
  this cleanup). Confirmed with a 10-species spot-check (all survived, correctly resolved) and
  generation-quality parity (735/833 clean before -> 741/840 after, same failure-reason ratios).

### ✅ Checkpoint A
- [x] seedsmith suite green (1458/1458)
- [x] DemonQualityReport shows 0 duplicates — confirmed: "840 anchor entries on disk, 840
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
> ### ⛔ Handoff finding from the battle-timeline session, 2026-09-04 20:40 — **C2's second pass left a live inconsistency**
>
> Reported rather than fixed: this is the demon stream's surface, and the fix is a pipeline/index
> decision, not a test edit. Found while verifying whether `combat-unification`'s **S5** could be
> unblocked (it cannot, and this is why).
>
> **Four Core tests are red, and they share one root cause** (`dotnet test --filter ~Demons|~Species`
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
- [x] C3: self-heal every currently-unresolved field (`rerun --unresolved`) — **done 2026-09-04,
  110/110 succeeded, 0 failed, 2159 calls.** Full 8-pipeline reclassification for every species
  with at least one unresolved voted field (rarity/aptitude/element/threatBand/attackTempo),
  benefiting from the SAME loader fix C2 needed — 0 failures throughout confirms it holds at scale
  under the harder (full-reclassify, not scoped) path too.

### ✅ Checkpoint C
- [ ] both reruns state=completed (checked via `demons run status`, not assumed)

## Phase D — verification

- [x] D1: before/after report — **done 2026-09-04.** Real before/after, both from
  `DemonQualityReport`, not a claim:

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
  `threatBand` does (`demon-threat.v1.json`'s own `inferredDefaultRung`, already committed and
  sanctioned, previously wired only for "no computable score," never for "vote never converged").
  `aptitudePrimary`/`rarity`/`elementPrimary` have NO equivalent real signal anywhere in the repo —
  `rarity`'s own field description explicitly forbids deriving it from threatBand/danger; forcing a
  rule for any of the three would be inventing one, not deriving it. These three correctly stay
  `"unresolved"`/reported, not auto-assigned.
- [x] **F2: built `fix_unresolved`** (`runner.py`) + `resolve_unresolved_threat_band` (`derive.py`)
  — a deliberate, on-demand FIX STEP, never automatic during classification, zero model calls,
  idempotent, honestly provenance-stamped (`confidence.threatBand = "deterministic-fallback"`,
  never faked as a real LLM judgment). CLI: `demons run fix-unresolved [--dry-run]`. 7 new tests
  (2 `derive.py`, 5 `runner.py` — resolves correctly, scope boundary holds, no-op when nothing to
  fix, dry-run writes nothing, idempotent).
- [x] **F3: skill written** — `.claude/skills/demon-fix-unresolved/SKILL.md`, documenting the
  scope boundary (why threatBand only) and the real 2026-09-04 result.
- [x] **F4: run for real** — 10 species (`Jalakelp`, `DoomChomper`, `SwordStar`, `DoomBlover`,
  `IceBean`, `MagnetDoom`, `UltimateTorch`, `HypnoQueen`, `HypnoTorch`, `CherryTorch`) had
  `threatBand: "unresolved"`; all 10 resolved to `raider` (the sanctioned default) — which also
  happened to be the one threatBand value the corpus had never used at all, closing two audit
  findings in one step. Verified via `DemonQualityReport`: threatBand unresolved 10‰→**0‰**,
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
  demon-`identity` pipeline's own prompt needed to change, not the shared doc.
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
  "Ultimate." `DemonQualityReport` confirms: rarity catalog coverage **10/10 used, 0 unused** (was
  9/10 with `sunwoven` unused); `Sunwoven` balance in real simulated combat: n=4, mean win share 75%,
  median 75% — sits between `Fused`/`Chimeric` and `Heirloom`/`Firstseed`, a plausible near-top rung,
  not a broken outlier.

### ✅ Checkpoint D/E/F/G — plan closes, sunwoven resolved (owner directive: "resolve it or replace
  it", not tolerated as a permanently-unusable enum value)
