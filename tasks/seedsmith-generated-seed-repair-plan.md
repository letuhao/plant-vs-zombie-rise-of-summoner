# Seedsmith generator repair — items corpus (investigation + plan)

**Status: COMPLETE (2026-09-12).** All of S1–S6 landed; see the todo for per-item evidence. Outcomes
differed from the first draft in four ways, recorded here so the reasoning is not lost:

- **S2 registry shape:** the frozen v4 was `classes.v2.json`, so the additive per-frame slate is
  **`classes.v3.json`** (registryVersion 5), keeping the existing mint-as-a-new-file convention.
- **S3 re-slate volume:** 649 rows, not 428 — the frame slate is the closed vocabulary now, so every
  row outside its own frame's set is repaired, not only the rows on an overlapping family.
- **S3 re-flavour:** NOT done here. `spec-base-types.md` assigns re-flavouring to the authoring fleet
  and says this module emits an `ImplicitFlavourDrift` warning only; the warning now reports 312 rows.
- **S4 recipe keys:** an extra generator defect surfaced — `recipe.nameKey` was a slug of a display
  name that repeats by design, so it is minted from the unique `recipe.NNN` id now. A rename that
  updated `nameKey` also had to re-derive `iconKey` (`icon.<nameKey>`).
**Trigger:** a session set out to hand-edit generated seed data to clear failing tests. The owner
stopped it and asked for the generator-first rule plus a hard gate. This file records why the
hand-edit was wrong and what the generator must become.

**Hard rule this is downstream of:** AGENTS.md / CLAUDE.md — *generated seed data is never
hand-edited; fix the generator and regenerate.* Enforced by
`scripts/guard-generated-seed.ps1` (CI + `deploy-play.ps1`).

---

## 1. What is generated

`data/seed/items/**` is **1011 of 1041** files emitted by seedsmith, each carrying
`_meta.model` (e.g. `claude-haiku-4-5`), `_meta.promptVersion`, `_meta.batch`. The generator
packages live in `tools/seedsmith/seedsmith/adapters/items/` (`basetypegen`, `setgen`, `charmgen`,
`gemgen`, `combo-gen`, `recipegen`, `droptablegen`, `consumablegen`, `materialgen`, `affixfamgen`,
`milestonegen`). Hand-editing an emitted row forks the corpus from its generator: the next run
reverts it, and `_runs/*.ledger.json` stops describing the file.

Author by hand only where a brief says content is authored: `data/tuning/**`,
`data/seed/items/_registry/**`, `_exemplars/**`, and hand-authored kinds (`unique`,
`content/display/*`). Those carry **no** generator `_meta`.

## 2. The two defects a hand-edit was about to paper over

### D11 clause 1 — humanoid/plant implicit families not disjoint (7 of 15 roles)

- **Symptom:** `BaseTypeCorpusTests.Every_live_roles_humanoid_and_plant_implicit_families_are_disjoint`
  fails; spec-base-types.md records clause 1 as "Red today."
  Overlapping roles: `armament-primary`, `armament-secondary`, `core-guard`, `head-guard`,
  `jewel-major`, `manipulator`, `ward-array`.
- **Root cause (generator, not data):** `basetypegen/tuning.py:106`
  `load_legal_implicit_families(role, …)` returns **one role-wide slate**.
  `classes.v2.json.implicitSlates[role].legalFamilies` has no frame axis, and
  `brief.py`'s `implicit_families` is that same role-wide tuple for both frames. So the model is
  asked to "pick the family that best fits" with nothing steering each frame apart, and the two
  frames drift into the same picks.
- **Why the registry is not the fix:** `classes.v2.json` is `frozen: true, registryVersion: 4`;
  the spec says mint `v{n+1}`, never edit a frozen registry in place. And the registry's slate is
  the legal **union** — clause 1 needs the two frames to draw **disjoint subsets**, which is a
  property of what generation produces, not only of what it may pick from.

### Item name/key collisions (848 validator findings)

- **Symptom:** `ItemSeedValidator` reports 411 `NameCollision` + 437 `NameKeyDuplicate`;
  `test_items_adapter::test_authored_item_names_are_unique_across_kinds` fails.
- **Root cause (generator, not data):** `basetypegen/brief.py:83` builds `existing_names` by
  reading **only the one partition file** (`{frame}-{role}-{band}.json`). The brief tells the model
  "avoid a name already used **in this partition**", so a later partition freely re-mints a name an
  earlier partition already shipped (e.g. "Tungsten Spiker" in both
  `humanoid-armament-primary-a.json` and `-b.json`; "Weighted Censer" 11 times in one role).
- **Two distinct sub-cases, different owners:**
  1. **base-type, 65 collisions** — purely the partition-scoped `existing_names` above.
  2. **cross-kind (affix-family×gem, charm×drop-table, set×combination) and `set.item`
     placeholders (6 rows)** — the six hand-authored Chinese-named sets carry a literal
     `"nameKey": "set.item"` placeholder. These are a separate authoring/repair gap.
- **There IS a sanctioned repair tool** for persisted set/charm collisions:
  `seedsmith items repair-names` (`setgen/name_repair.py`) — plan/apply, model-authored, keeps
  gameplay fields byte-identical. It covers `sets`/`charms` only.

## 3. Plan (generator-first)

Ordered; each item ends with "regenerate + commit the diff", never a data edit.

| # | Deliverable | Generator surface | Proves |
|---|---|---|---|
| S1 | **Pass corpus-wide existing names** to the base-type brief | `basetypegen/brief.py` `load_partition_context` reads all `base-types/**`; `run.py` passes them | new rows cannot re-mint a shipped name; the 65 are repaired by re-running generation, not by editing JSON |
| S2 | **Per-frame implicit split** in the slate the brief offers | `basetypegen/tuning.py` `load_legal_implicit_families(role, frame)`; new `classes.v5.json` carrying `legalFamiliesByFrame`; `brief.py`/`schema.py` use it | clause 1 holds by construction (disjoint offered sets), and clause 3's axis is expressed in the same block |
| S3 | **Re-slate generation run** for the 7 overlapping roles | run `basetypegen` against S2; regenerate, commit output | D11 test goes green from generator output |
| S4 | **Extend the collision guard to every colliding kind**, not just set/charm | `name_repair.plan` to scan all kinds the validator checks; or a shared `dedup` used by each `*gen` before write | `repair-names` closes the remainder |
| S5 | **`set.item` placeholder** in 6 hand-authored sets | hand-authored content → replace placeholder nameKeys (authored, not generated) | nameKey uniqueness holds |
| S6 | **CI gate**: generator↔output drift, plus `guard-generated-seed` | `seedsmith check --gate` per tree; guard already added | a hand-edit cannot merge |

### Open questions for the owner

- **S2 registry bump:** mint `classes.v5.json` (add `legalFamiliesByFrame`) vs add a new
  `frame-implicit-slates.v1.json`. The spec's own preference is a `classes` bump; the frozen-v4
  rule forbids in-place edits.
- **S3 re-slate vs re-flavour:** changing a row's implicit can leave `atom.fortitude` prose over an
  `atom.stoicism` implicit. The spec emits `ImplicitFlavourDrift` warnings and defers re-flavour to
  the authoring fleet. Decide: accept drift now, or fund a re-flavour pass with S3.
- **S4 scope:** repair all 848 across 8 kind-sets, or base-type (65) + placeholders first.
- **S2 blast radius:** re-slate is ~122 base-type rows; confirm that volume before the run.

## 4. What NOT to do

- ❌ Edit any `data/seed/items/**` row whose file carries `_meta.model`/`promptVersion`/`batch`.
- ❌ "Fix" a red test by bumping a count or renaming a row by hand.
- ❌ Edit `classes.v2.json` in place (`frozen: true`).
- ✅ Change the generator/tuning/registry → regenerate → commit the re-emitted diff.
- ✅ When the generator cannot express the fix, **fix the generator first** — that is the deliverable.

## 5. References

- Rule: `AGENTS.md` Hard boundaries; `CLAUDE.md` "Generated seed data is never hand-edited".
- Guard: `scripts/guard-generated-seed.ps1`; CI step "Boundary guards"; `scripts/deploy-play.ps1`.
- Defects: `docs/architecture/item/spec-base-types.md` (D11); `tools/ItemSeedValidator`
  (`NamingCheck`, `FrameDirectionCheck`).
- Tooling: `tools/seedsmith/seedsmith/adapters/items/basetypegen/{brief,run,tuning,emit,schema}.py`;
  `.../setgen/name_repair.py`; `seedsmith items repair-names`.
