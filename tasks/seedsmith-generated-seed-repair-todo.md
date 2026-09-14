# Seedsmith generator repair — task list

Plan: [seedsmith-generated-seed-repair-plan.md](seedsmith-generated-seed-repair-plan.md).
Hard rule: generated seed is regenerated, never hand-edited (AGENTS.md / CLAUDE.md);
guard `scripts/guard-generated-seed.ps1` enforces it.

## Done (this session)

- [x] **Hard rule in AGENTS.md + CLAUDE.md** — generated seed is never hand-edited; fix the
      generator and regenerate. Includes the 2026-09-12 incident.
- [x] **`scripts/guard-generated-seed.ps1`** — fails when a provenance-carrying file under a
      generated tree changes without its generator/tuning/registry. Proven to block a real
      hand-edit and to pass an authored `_registry/` edit.
- [x] **Wired the guard** into `.github/workflows/ci.yml` (with `fetch-depth: 2`) and
      `scripts/deploy-play.ps1`.
- [x] **Investigation** — both defects root-caused to the generator, not the data.

## S1 — corpus-wide existing names in the base-type brief

- [x] `basetypegen/run.py`: `load_corpus_names` reads **all** `base-types/**`; `run_draws` takes the
      map, re-asks once on a hit, then refuses the draw by name (prompt bloat avoided — 860 names
      would add ~16k chars per brief). Tests added; 60 pass.
- [x] `setgen/seedfile.py`: an unsluggable name raises `NameKeyUnsluggable` instead of minting the
      shared `set.item` placeholder key.

## S2 — per-frame implicit slate

- [x] Minted `data/seed/items/_registry/classes.v3.json` (registryVersion 5) with
      `legalFamiliesByFrame`; v2 stays frozen on disk.
- [x] `basetypegen/tuning.py`: `load_legal_implicit_families(role, frame)`; v2-shaped fixtures fall
      back to the union.
- [x] `FrameDirectionCheck` validates against the frame slate; `RegistrySet` reads v3.
- [x] Test: the two frames are offered disjoint slates for every role.

## S3 — re-slate generation run

- [x] New `basetypegen/reslate.py` (a generator verb, not a data edit): 649 rows re-slated onto
      their frame slate, identity + powerBand + class preserved (seed-contract §7.2).
- [x] Core `BaseTypeCorpusTests` 8/8; validator 0 frame errors.
- [x] `ImplicitFlavourDrift` warning wired (312 rows) — re-flavour stays the authoring fleet's, per
      spec-base-types.md.

## S4 — name collisions beyond set/charm

- [x] `ItemSeedValidator --collision-groups` prints the authoritative groups (its own
      NameNormalizer); exemplars excluded.
- [x] `name_repair` consumes the groups and covers every kind; per-row bounded retry names the
      rejected candidates; a failed row no longer discards the batch.
- [x] `NameCollision` + `NameKeyDuplicate`: **848 → 0**.
- [x] `recipegen`: nameKey minted from the unique `recipe.NNN` id (a recipe name repeats by design).
- [x] `iconKey` re-derived with `nameKey` (gemgen derives it); 343 stale rows fixed.

## S5 — placeholders

- [x] The 51 rows carrying `set.item`/`charm.item` renamed (their names are CJK, so the key cannot
      be re-derived; the name is what must change).

## S6 — gates

- [x] `guard-generated-seed.ps1` (CI + `deploy-play.ps1`) blocks a hand-edit of generated seed.
- [x] seedsmith suite: 3791 passed, 0 failed. Core 13361/13361. Guard 248/248 (flaky regen test
      fixed: temp OutDir + serial collection).

## Follow-ups (not this module)

- [ ] `NameGrammarViolation` (808), `PossessiveForbidden` (159), `InventedConnective` (158),
      `PluralForbidden` (31), `FusionNotDecomposable` (27), `GeneratedOnlyNamePattern` (26): a
      separate naming-grammar pass over the same corpus.
- [ ] 312 `ImplicitFlavourDrift` rows — authoring fleet re-flavour.
