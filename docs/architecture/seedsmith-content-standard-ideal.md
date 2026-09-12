# Seedsmith content-completeness + localization standard — the ideal

**Status:** idea phase, 2026-09-08. Not a spec. No build authorized. Supersedes-in-scope
[passive-tree-i18n-ideal.md](passive-tree-i18n-ideal.md), which this doc absorbs and generalizes —
that doc is kept, marked superseded, not deleted (its passive-tree-specific inventory is still
correct and still cited below). Builds on
[docs/architecture/seedsmith/spec-pipeline.md](seedsmith/spec-pipeline.md) and
[seedsmith-map.md](seedsmith-map.md), the existing per-generator specs under
[seedsmith/](seedsmith/), and [item-content-ideal.md](item-content-ideal.md) /
[creature-seed-ideal.md](creature-seed-ideal.md) / [action-corpus-ideal.md](action-corpus-ideal.md)
where each domain's own generator already exists.

## Which loop(s) this extends

Not a new loop — a cross-cutting content-pipeline standard that serves **every spine and place loop
that ships seedsmith-generated player-facing text**: Spine A (level up and power — passive trees),
Spine B (creature summon and fusion — species names/lore), Spine C (item collection — item
names/flavor), and Quests and events (delve events, expedition ticks). `the-loops.md` names none of
these as a loop of their own because this is infrastructure, not something the player does — it
exists so the loops above render correctly, in whatever language, whether their content was
generated last month or five minutes ago.

## What this is

The owner's own framing, stated in full because it is the decision this doc is built around, not a
paraphrase: *"anything seedsmith that generate information and show up to user FE need sub pipeline
to cover it... this must enforce in skill, we will cover everything here, not only passive skills...
also need a deterministic engine to detect missing description and deploy LLM engine to generate
missing when run resume."*

Two intertwined problems, one standard:

1. **Completeness.** A seedsmith generator's own resumable run can leave real gaps — a field never
   generated, a stale record left behind after a prompt version changed, a name collision refused
   and never retried. Right now, detecting "missing" is real for passive-tree (its own ledger) and
   items (`Quality/FlavourMissing`); detecting "stale" is real, live code written independently five
   times but only WIRED to an actual regeneration path in one domain (creatures — see below, audit
   correction 2026-09-08); actions has genuinely nothing at all. Nothing today says "resume this
   run and it will find and fill every real gap, in every domain, the same way."
2. **Localization.** Every domain's generated text is English-only, with no locale dimension
   anywhere. `passive-tree-i18n-ideal.md`'s own finding generalizes without exception: this repo's
   real "two text systems" split (`docs/web/spec.md` §6 — Lingui for hand-authored Chrome text,
   "server data, locale-tagged" for content text) has never actually had a real implementation for
   the "content text WE generate" case, in ANY domain — only for the base game's own pre-existing,
   already-multi-lingual data.

**A load-bearing repo principle restated here, not linked, because it is exactly what makes "one
standard" the right shape rather than "an idea per domain":** `spec-pipeline.md` §1's own hard-won
lesson from ~90 real agent runs — *"not one failure was a generation failure... effort belongs in
the brief, the schema and the gate, not in prompt cleverness."* A missing-content or untranslated
gap is the SAME failure class that lesson names: a gate that was never built to check for it, not a
model that got worse. This standard is that lesson applied to two gates (completeness,
localization) that no domain has today, generalized once instead of five times.

## What already exists

**Built:**
- `spec-pipeline.md`'s own header ("Nothing is built") is **stale** — real, shared infrastructure
  exists and is imported across multiple domains: `tools/seedsmith/seedsmith/pipeline/model.py`
  (the generic `Pipeline` scaffold — items, actions, creatures, dungeon and trees adapters all import
  it), `pipeline/llm_caller.py`, `pipeline/open_loop.py`, `pipeline/provenance.py`,
  `pipeline/run_ledger.py`. The GENERIC MECHANISM exists; **adoption of it does not** — see wiring
  gaps below.
- `Quality/FlavourMissing` (`metrics/quality.py:24`, `Loop.CLOSED`, registered
  `report/cli.py:70-71`) is a REAL, working, deterministic "is this field populated" gate — exactly
  the shape of engine the owner is asking be generalized. It already proves the pattern works.
- Passive-tree's own `run_language_stage` (`adapters/trees/nodegen/run.py:721`, ledger at line 68)
  is a real, live-proven, resumable "detect what's missing, generate only that" engine — this
  session's own H9/J9 work ran it repeatedly and it correctly skipped every already-accepted node
  on every resume.
- `_provenance` (pipeline id, model, prompt version, timestamp) is real, written content, in at
  least two domains: creatures (`data/seed/creatures/species/plant/aerial-flora.json:8-18`) and
  structures (`data/seed/structures/bank/reliquary.json:7-10`).
- The web app's general i18n system (Lingui) is real and working for Chrome text (unchanged from
  `passive-tree-i18n-ideal.md`'s own finding — cited here rather than re-derived).
- `flavorKey` (items, `adapters/items/uniques/briefs.py:101-103`: *"The minted i18n key for this
  anchor's flavor text. NOT authored — fixed by the planner"*) is a real, ALREADY-MINTED
  translation-ready key, on a SECOND domain beyond passive-tree's `nameKey` — proof the "stable key
  now, translated text later" shape has already been independently invented twice, unprompted, by
  two different generators. Nobody has connected either key to an actual second-language catalog.
- **A real, working "detect stale, regenerate exactly that" loop exists — audit correction, found
  2026-09-08.** `adapters/creatures/generate_commander_effects.py:97-99` (`elif args.stale: wanted =
  set(stale_ids(list(existing.values()), subjects))`) genuinely computes a staleness diff and
  regenerates ONLY the stale subjects — a real precedent, stronger than passive-tree's own ledger
  (which only ever detects "never attempted," never "attempted but now stale"). **Two real
  qualifications this program must not gloss over**: this path is an EXPLICIT, OPT-IN CLI flag
  (`--stale`), never the default (the default path, `else:` at line 102, only fills subjects with
  NO existing entry at all) — and the file's own comment at line 85-87 states why: *"Generation is
  stochastic, so regenerating an entry that is already correct DESTROYS good content and costs
  model time for nothing."* This is the precedent the "Automatic-backfill scope" owner decision
  below is built on — resolved by keeping this program's own automatic path missing-only and
  putting existing-content regeneration behind an explicit, human-invoked `--force` flag,
  matching this file's own caution rather than overriding it.
- **A real, working, general language-contamination validator already exists — audit correction,
  found 2026-09-08.** `workflow/validators/language.py:26`'s `language_consistency` (added
  2026-09-01 after a real incident: *"87% [of a real 84-draft run] were code-switched: English
  prose with Chinese motif tokens spliced in"*) rejects a value mixing CJK and Latin prose. Its own
  docstring states it is deliberately general, "not a creatures-specific one" — confirmed: it is
  wired into TWO real domains, `adapters/creatures/commander_effect.py` and
  `adapters/trees/nodegen/run.py` (grep-confirmed, no other adapter imports it).

**Wiring gap** (the mechanism exists somewhere; it is not reaching every domain):
- `Quality/FlavourMissing` structurally cannot fire outside items —
  `metrics/quality.py:21`'s own `FLAVOR_EXPECTED_KINDS` frozenset names only item kinds
  (`base-type`, `unique`, `charm`, `consumable`, `gem`, `set`); its own docstring says "deliberately
  excludes machinery... and material." Creatures, actions and dungeon/events were never added to this
  set — not because they don't need the check, but because nobody extended it.
- `Quality/FlavourMissing` is `gates = False` even for the one domain it covers
  (`metrics/quality.py:28`), and CI's own `--gate` invocation for items
  (`.github/workflows/ci.yml:261`) only fails on `gates=True` findings — so even where the
  detector exists, nothing stops a missing flavor from shipping today.
- The "detect stale, regenerate only that" SHAPE (`stale_ids`: compare a recorded key against a
  freshly-computed one) is written independently **five times** —
  `adapters/creatures/anchor/emit.py:47`, `adapters/creatures/commander_effect.py:121`,
  `adapters/dungeon/provenance.py:50`, `adapters/items/uniques/audit.py:173`,
  `adapters/structures/generate_anchor.py:201-206` — but **audit correction, 2026-09-08: only TWO
  of the five are real, LIVE code with actual callers, and both are in the SAME domain.**
  `creatures/anchor/emit.py:47` is called from `creatures/run/selectors.py:10,73`;
  `creatures/commander_effect.py:121` is called from `generate_commander_effects.py:97-99` (the
  `--stale` path). The other three — dungeon, items, structures — are confirmed DEAD: zero
  production callers anywhere in the repo, only test imports or none at all. This sharpens, not
  weakens, the owner's own "make it a standard" instruction: the mechanism is proven live exactly
  ONCE (in one domain), reinvented as dead code THREE more times, and never generalized. Passive-
  tree's own ledger is a real, live, SEPARATE mechanism (resumability for missing content), not a
  sixth `stale_ids` — it never detects staleness in already-accepted content, only absence.
- `adapters/dungeon/provenance.py`'s own `DungeonProvenance` dataclass (line 12) and `stale_ids`
  (line 50) are real, written CODE — but genuinely **dead**, not merely under-wired: audit
  confirmed (2026-09-08) its only importer anywhere in the repo is its own test file
  (`tests/test_dungeon_idempotency.py`); the real writer (`adapters/dungeon/emit.py`'s own
  `write_entry`/`write_corpus`) never imports or calls it, so the committed content never carries
  the field (`data/seed/dungeon/events/event.bargain-creature.allpeater-001.json` has no `_provenance`
  key at all). This is aspirational code with zero production callers, one step further gone than
  "built but not wired."
- **The live Chinese-fragment defect (below) is a WIRING gap, precisely root-caused 2026-09-08 —
  correcting this doc's own earlier "real gap" framing.** `language_consistency` (above) already
  exists, is proven, and is wired into 2 of 5 domains — dungeon/events is simply not one of them.
  **A second, real defect in the validator itself, found tracing this precisely**: it only fires
  when the SUBJECT'S OWN INPUT MOTIFS are Chinese (`language.py:32`: `if not motifs or not any(
  _CJK.search(m) for m in motifs): return []`) — it has no check for the REVERSE direction (English
  motifs, but the model's output unexpectedly contains CJK anyway), which is exactly the shape of
  the real dungeon defect (an English-motif event whose `flavor` field contains stray Chinese
  fragments). Wiring the EXISTING validator into dungeon unchanged would NOT have caught this
  specific real defect — `core`'s own generalization must fix the directional asymmetry, not just
  copy the validator as-is.

**Real gap** (nothing exists yet, anywhere):
- **Actions have no ledger, no provenance, and no missing-field metric at all.** Resumability is
  handled ad hoc via `_manifest.json`/`_rounds/` file dispositions
  (`adapters/actions/load.py:31,59-72`), not a ledger; `data/seed/actions/committed-round-1.json`
  has zero `_provenance` occurrences. This is the domain furthest behind, not merely inconsistent.
- **No locale dimension anywhere, in any domain** — confirmed for passive-tree already; confirmed
  again here for items (`flavorKey` mints an id, never a translation), creatures, actions, and
  dungeon/events. Zero exceptions found across the whole repeat check.
- **No cross-domain, generalized "gap detector + LLM backfill on resume" engine.** Five
  domain-specific stale-id checks exist; the one REAL, WORKING regeneration loop
  (`generate_commander_effects.py`, above) is deliberately opt-in, for a stated, real reason
  (destroying already-good content). **Resolved** (see "Automatic-backfill scope" under Owner
  decisions): this program's own shared engine does not attempt to make stale-content
  regeneration automatic either — it keeps the commander-effects precedent's own caution by never
  touching existing content automatically, and exposes an explicit `--force` parameter for a
  person to trigger a full regeneration on demand.
- **A bidirectional language-contamination check does not exist anywhere** — `language_consistency`
  (above) covers one direction only; nothing checks the other. A general fix belongs in `core`.

## Prior art

Two research passes, both dated 2026-09-08, cited for numbers/patterns:

- **Localization prior art carries over unchanged from `passive-tree-i18n-ideal.md`**: the
  industry's real "two text systems" terminology (Unity's *static vs. dynamic strings*; Unreal's
  `FText` Namespace/Key/Source-String triples); text-expansion budgets (German +30-40% overall,
  short strings +100-300%,
  [SimpleLocalize/W3C](https://simplelocalize.io/blog/posts/text-expansion-ui-localization/));
  template-generated text is usually localized by re-implementing the GRAMMAR per language
  ([Rogue Legacy 2](https://multilingual.com/issues/july-2023/how-to-localize-a-game-with-procedurally-generated-text/)),
  which does not transfer to this repo's free-LLM-prose content; LLM translation quality/cost
  claims are real but vendor-sourced and unverified independently
  ([Gridly](https://www.gridly.com/blog/ai-translation-game-localization/)), with proper-noun/coined-term
  drift as the one consistently repeated real failure mode.
- **Completeness/gap-detection is a well-established pattern under a different name: incremental,
  content-addressed build caching.** Bazel and Nx do not compare timestamps (a recognized-fragile
  approach — clock skew, mtime doesn't reflect a content revert); they hash the real inputs (Bazel:
  command line + environment + input file contents; Nx: file sets + runtime inputs + env + CLI
  args) and treat a changed hash as "this output is stale, rebuild only this node"
  ([Bazel remote caching](https://bazel.build/remote/caching),
  [Nx inputs](https://nx.dev/docs/reference/inputs)). **This maps directly onto "detect a generated
  record is stale because its prompt/schema/model version changed"** — the SAME staleness-hash
  concept, applied to content records instead of build artifacts. `spec-pipeline.md` §4 already
  describes this exact shape (`_provenance`: pipeline id, model, prompt version) without naming the
  build-system analogy; the analogy is useful because it comes with two decades of known-good
  practice this repo does not need to re-derive.
- **"Fill only the missing values via a model" has a real name in an adjacent field —
  data imputation — now explicitly extended to LLMs** (2025-2026 papers:
  [UnIMP, VLDB 2025](https://www.vldb.org/pvldb/vol18/p3421-wang.pdf);
  [prompt-design-for-imputation, arXiv 2506.04172](https://arxiv.org/pdf/2506.04172)) — but this
  is framed as filling missing *dataset* values, not "regenerate a missing game-content record,"
  and no sourced example of a game studio doing this exact thing at scale was found. Flagged as a
  useful *term* to borrow, not a proven *practice* to copy verbatim.
- **A real, partially-sourced failure mode for automatic backfill**: a 2026 study
  ([arXiv 2606.02334](https://arxiv.org/pdf/2606.02334)) found that regenerating dataset
  descriptions with MORE schema context but without the original data samples produced *worse,
  more generic* output than a minimal baseline — real evidence for "backfilled content risks being
  lower-quality than the original pass because it runs with different/less context." **Two more
  failure modes are reasoned here, not sourced — flagged as such**: (a) a resumed run could
  regenerate a field that was DELIBERATELY left blank (a design choice, not a gap), (b) backfilled
  content generated much later than its siblings could drift in voice/consistency from a tree/
  species/item's own already-shipped content, the same corpus-wide-consistency problem this
  session's own real work already found and fixed twice (cross-tree `nameKey` collisions; the
  favour-fit calibration drift) — for entirely different reasons than staleness, but the SAME
  symptom (two entries that should agree, don't).
- **Provenance-stamping for staleness comparison is standard MLOps practice**, not a house
  invention: DVC/MLflow/Weights & Biases all record model version, prompt template and input hash
  per artifact specifically so a later run can diff "current pipeline version" against "the version
  stamped on this record"
  ([W&B / MLIP versioning](https://mlip-cmu.github.io/book/24-versioning-provenance-and-reproducibility.html)) —
  the exact `_provenance` shape `spec-pipeline.md` §4 already specifies, now confirmed to match
  established practice outside games entirely.

## The shape

Not decided here (see Open questions) — the real candidates, narrowed by the inventory:

1. **One shared completeness-and-staleness engine, in `pipeline/`, that every domain adapter calls
   the same way** — generalizing the FIVE existing bespoke `stale_ids()`/ledger implementations
   into one, rather than writing a sixth (for actions) and leaving the other four as-is. The
   staleness KEY (per the Bazel/Nx analogy above) would be a hash of `{briefHash, promptVersion,
   schemaVersion, modelVersion}` — `adapters/dungeon/provenance.py`'s own already-documented key
   shape (`briefHash + promptVersions + registryVersions + motifSubsetHash`) is the closest
   existing precedent for the KEY's own shape, even though that module is itself dead code (never
   wired to a real writer, above) — a well-designed shape nobody ever connected, not a bad one. The
   key is computed and used for **reporting** (a stale-count metric), not for triggering
   regeneration — per the resolved "Automatic-backfill scope" decision, an automatic run only ever
   fills MISSING records; regenerating existing (stale or not) records happens only through an
   explicit `--force` parameter, which regenerates everything it's pointed at rather than a
   selectively-diffed stale subset.
2. **One shared `Content/FieldMissing`-style metric family**, generalizing `Quality/FlavourMissing`
   the way `FLAVOR_EXPECTED_KINDS` already narrows it per-domain today — turning that same
   narrowing mechanism into a per-domain REGISTRY entry instead of a single hardcoded frozenset, so
   adding a domain is a registration, not an edit to a shared module's own internals.
3. **A locale catalog convention that reuses the two REAL, independently-invented translation keys
   already minted** (`nameKey` in passive-tree, `flavorKey` in items) rather than a third scheme —
   likely one `data/seed/<domain>/i18n/<locale>/` subtree per domain (matching each domain's own
   existing `data/seed/<domain>/` convention), not one shared cross-domain catalog, since each
   domain's own content shape (nodes vs. items vs. species vs. events) differs too much for one
   flat table.
4. **Whether the "gate" for either concern is CI-enforced (`gates=True`) or report-only
   (`gates=False`, matching `FlavourMissing`'s own current, deliberately-soft status)** — a real
   choice, not a default: promoting either gate to hard-fail is exactly the kind of change
   `metrics/model.py`'s own rule (a metric starts `gates=False` until a deliberate, separate
   promotion) already requires being decided on purpose, not inherited from this standard's own
   creation.

## Tunables

None yet. If a staleness/backfill run gets a real numeric dial (a call-count cap per resume, a
"how many locales get generated per pass" throttle, a length-overflow tolerance for translated
text — echoing the real 100-300% short-string expansion band from the localization research above),
it belongs in a real `data/tuning/<domain>-content.v1.json` file per `tunables-ssot.md`'s own
standard, never a `const` — the same rule `spec-pipeline.md` §7 already states for call caps
("Hard call cap per run, from configuration").

## What this deliberately does not decide

- Whether the shared completeness engine is built as a NEW `pipeline/` module first (generalize,
  then migrate five call sites) or built by extracting the best of the five existing
  implementations incrementally, one domain at a time.
- Whether actions (the domain with nothing built) gets its ledger/provenance/metric BEFORE or
  alongside the shared-engine generalization, or as its own separate, prior task.
- Whether backfilled/translated content earns its own review-ladder pass (mirroring J2/J3's
  passive-tree census) before shipping, per domain or shared.
- The real, already-shipped Chinese-fragment defect named above is NOT fixed by this doc — it is
  evidence for why the standard is needed, named here so it is not lost, but fixing that one entry
  is separate, smaller, and does not require this whole standard to exist first (it is, however, a
  real, named acceptance target in the plan's own Task 12).

## Owner decisions (settled since this doc was first written, kept here as the record)

- **Scope**: all five domains, built together, not staged. **Gate severity**: every new metric
  stays `gates=False` (report-only), matching `FlavourMissing`'s own current status. **Translation
  strategy**: this program generates and completes ENGLISH content only; the actual
  translate/regenerate-per-locale question is explicitly deferred to a separate, later, DIFFERENT
  pipeline this program does not build — only the storage shape needs to stay i18n-ready.
- **Automatic-backfill scope (resolved 2026-09-08, after the audit above surfaced the
  missing-vs-stale distinction)**: a resumed run is **fully automatic, no human gate — but only for
  genuinely MISSING content**, on the first run or any later run. **Already-generated content is
  never touched by an automatic run, regardless of whether its staleness key has changed** — a
  resumed run treats "exists" as "done," full stop, matching the commander-effects precedent's own
  caution rather than overriding it. Regenerating existing content (stale or not) is never
  automatic: it happens only through an explicit `--force` parameter a person invokes by hand,
  which regenerates everything it's pointed at, not a selectively-diffed stale subset. This is
  simpler than either the "fully automatic for both" or the "guarded/locked-record" shape
  considered during the audit — there is no silent-regeneration risk to guard against, because
  nothing existing ever regenerates without an explicit, human-invoked flag. The staleness-key
  machinery (`stale_ids`-style hashing) is kept for **reporting** ("N records are stale, run with
  `--force` to refresh them") — a `gates=False` metric, never an auto-trigger.
