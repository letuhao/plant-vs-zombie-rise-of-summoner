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

- [ ] `basetypegen/brief.py`: `load_partition_context` gathers `existing_names` from **all**
      `base-types/**`, not only the current partition file.
- [ ] `brief.py` render states the true scope ("already shipped in the base-type corpus").
- [ ] Unit test: two partitions sharing a name is refused / avoided.

## S2 — per-frame implicit slate

- [ ] Mint `data/seed/items/_registry/classes.v5.json` with `legalFamiliesByFrame`
      (humanoid/plant) per role; `v4` stays frozen on disk.
- [ ] `basetypegen/tuning.py`: `load_legal_implicit_families(role, frame)`.
- [ ] `brief.py` + `schema.py`: offer the frame's own slate.
- [ ] Test: offered sets are disjoint per role by construction.

## S3 — re-slate generation run (7 roles)

- [ ] Run `basetypegen` against S2 for `armament-primary`, `armament-secondary`, `core-guard`,
      `head-guard`, `jewel-major`, `manipulator`, `ward-array`.
- [ ] Commit the regenerated `base-types/**` diff.
- [ ] `BaseTypeCorpusTests.Every_live_roles_humanoid_and_plant_implicit_families_are_disjoint` green.
- [ ] Decide re-flavour: accept `ImplicitFlavourDrift` now, or fund the authoring pass.

## S4 — name collisions beyond set/charm

- [ ] Extend `name_repair.plan` (or a shared `dedup`) to every kind the validator checks.
- [ ] Run `seedsmith items repair-names` (plan → apply) for set/charm.
- [ ] Re-run for base-type/drop-table/recipe/combination/gem/affix-family rows as S1 lands.
- [ ] `ItemSeedValidator` NameCollision/NameKeyDuplicate → 0.

## S5 — `set.item` placeholders

- [ ] Replace the placeholder `nameKey` on the 6 hand-authored sets (authored content, not
      generated).

## S6 — gates

- [ ] `seedsmith check --gate` for each generated tree in CI.
- [ ] Keep `guard-generated-seed.ps1` green; confirm it blocks a hand-edit in CI.

## Guardrail

- `python -m pytest tools/seedsmith/tests -q`
- `dotnet test tests/FusionRpg.ItemSeedValidator.Tests`
- `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~BaseTypeCorpusTests"`
- `python tools/ItemSeedValidator` over `data/seed/items` → 0 NameCollision/NameKeyDuplicate,
  0 FrameImplicitNotDisjoint
- `.\scripts\guard-generated-seed.ps1` → clean
