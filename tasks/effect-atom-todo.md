# Tasks: effect-atom

Plan: [effect-atom-plan.md](effect-atom-plan.md) · Map: [../docs/architecture/effect-atom-map.md](../docs/architecture/effect-atom-map.md) · Specs: [../docs/architecture/effect-atom/](../docs/architecture/effect-atom/)

> **Spec closed 2026-08-22** after four adversarial passes. [definitions.md](../docs/architecture/effect-atom/definitions.md) **wins over any spec**; its §13 is the defect log and says which build position owns each open item. Nothing in that log gates a module before position 15.

> ⛔ **Owner action, before anything else:** `docs/architecture/effect-atom/` and this task pair are **untracked in git**. They were destroyed once today and recovered from session logs. Please commit them.

## Wave 1 — the spine (Checkpoint A: nothing in the game changes)

- [x] **E1: atom-kind-registry** — accepted 2026-08-22. 12 kinds, 5 attach points, 7 triggers, 33 reason codes, four-state runtime matrix.
  - Post-audit corrections applied: `RuntimeSupportMatrix` (Full/Partial/PlanOnly/None) replacing a 3-flag bitfield · E1 owns the trigger vocabulary with count guards · `CostHook` removed (E9's types) · `stat.derived` quarantined `None/None/None`, `resource.delta` battle and `shield.grant` battle+sim → `None` (D6) · `stat.modify`/`stat.derived` carry **no trigger** (§14.2).
  - [ ] **E1-follow-up: re-derive five param schemas** (§13 **D7**) — `box.set.boxType` is declared String and read as int; `status.apply` declares `statusId`/`durationMs` where FA2 reads `status`/`duration` as float seconds; `status.apply.target` is required but FA2 has no such param; the DoT/contagion payload lives on FA10 not FA2; `shield.grant` omits `sourceClass`, which its executor honours. Also declare `spawn.entity.count` (`min: 1`, default 1).
    - Acceptance: every declared key is read by its executor, with the right type; a kind spanning two opcodes says so; tests cover each corrected key.
    - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom"`.
    - **Must land before E7**, where a wrong schema first produces a wrong grant. Scope: M.

- [x] **E2: value-spec-and-curve** — BUILT 2026-08-22. 28 tests; `effect_curve` DDL+DAL; `SeededRng`-backed named streams; zero-alloc resolve.
  - Files: `src/FusionRpg.Core/Effects/Atoms/{ValueSpec,CurveTable,AtomRandom}.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Curves.cs`.
  - Note: `SeededEffectRandom` was also moved off `System.Random` (§13 **D5**) — zero goldens moved, verified.

- [ ] **E3: predicate-tree** ← **next**
  - Description: typed AND/OR/NOT over the 8 closed leaves, depth ≤ 4 and ≤ 16 nodes, `subject` required on **every** leaf, ship the `CompiledPredicate` interface plus equivalence fuzz so **E13** can choose the encoding later without reopening E3, narrow readonly `FactReader`.
  - Acceptance: unknown leaf rejects (`UnknownLeaf`); depth 5 rejects (`DepthExceeded`); 17 nodes rejects (`NodeCountExceeded`); missing `subject` rejects (`AmbiguousSubject`); empty AND/OR node rejects (`EmptyNode`); empty tree is `true`; **equivalence fuzz** 10⁴ trees ≡ reference interpreter; short-circuit proven by a counting reader; zero allocation. **No no-self-call test** — it would disqualify the encoding E13 measures as fastest.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Predicate"`.
  - Files: `src/FusionRpg.Core/Effects/Atoms/{PredicateNode,PredicateCompiler,FactReader}.cs`, tests. Scope: M.
  - Dependencies: none. Spec: [spec-predicate-tree.md](../docs/architecture/effect-atom/spec-predicate-tree.md).

- [ ] **E4: atom-schema**
  - Description: `effect_atom` DDL + DAL (**`(family_id, tier, variant)`** unique, `kind_id` and trigger indexed) plus **`content_meta`** (the single `catalog_revision` row), `AtomRow`, `AtomRowValidator` wiring E1/E2/E3 checks. Whole-row rejection.
  - **New this round:** the **`icd_key`** column (TEXT, nullable, defaults to `atom_id` — E7 groups on it, §14.1); the trigger column is **nullable** and the `when_json.trigger` key is simply **omitted** for permanent modifiers (no `None` trigger name, so the closed count stays 7); `input: level` on an `OnApply` value spec rejects `BadValueSpec` (§13 **D9**).
  - Acceptance: round-trip byte-identical JSON; each validator case rejects; one bad row in 50 loads 49; revision bumps on edit; a triggerless `stat.modify` row round-trips; a trigger on `stat.modify` rejects `TriggerNotAllowed`; `guard-dal.ps1` passes.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AtomStore"` + `.\scripts\guard-dal.ps1`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs`, `src/FusionRpg.Core/Effects/Atoms/{AtomRow,AtomRowValidator}.cs`, tests. Scope: M.
  - Dependencies: E1, E2, E3. Spec: [spec-atom-schema.md](../docs/architecture/effect-atom/spec-atom-schema.md).

- [ ] **E5: container-schema** — ⚠️ **Checkpoint B: review before combat-action builds on it**
  - Description: `effect_container`, `effect_container_atom` (ordered, overrides), `effect_container_pool` (weighted, grouped), plus the `rarity` table. Fixed core + optional weighted pool.
  - Acceptance: `seq` order stable; `pool_rolls` > distinct **drawable** groups rejects (a group whose every row is `weight = 0` does not count — counting it passes validation and then under-fills the instance); one atom per group per draw; negative weight rejects (not clamped); `weight = 0` kept but never drawn; override naming an undeclared param rejects; override changing `kind_id` rejects; `rarity` ordinals explicit and **append-only**.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContainerStore"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs`, `src/FusionRpg.Core/Effects/Atoms/{ContainerRow,ContainerValidator}.cs`, tests. Scope: M.
  - Dependencies: E4. Spec: [spec-container-schema.md](../docs/architecture/effect-atom/spec-container-schema.md).

## Wave 2 — the layer does work (Checkpoint C)

- [ ] **E6: instance-and-binding** — instances with frozen rolls + `roll_seed`; `effect_binding` with a `priority` column; 7 owner scopes incl. new `sector:`/`slot:`; bind gate (G8, runtime support, `level_req`); `mods_json` grants migrate, **absolutes stay**. `power_json` is **nullable here** — E9 backfills it nine positions later. List order is `(priority DESC, container_id ASC, seq ASC)`, compared **ordinal** — never `binding_id`, which is generated. Dependencies: E5.
- [ ] **E13: runtime-form-benchmark** — candidate comparison over ~200 real atoms, cold-cache; pick the encoding; CI budget guard ≤ 50 ns/atom, zero alloc. Dependencies: E4, E2, E3.
- [ ] **E7: atom-compiler** — pure compilability classifier (incl. the `subject: target` legacy-filter rule), emit `EffectGrantDto` + `RunnerEntry`, bake into E13's form, server-side. **Three rules added this round:** group atoms by `COALESCE(icd_key, atom_id)` into **one** grant with the union of their triggers (§14.1); emit `EffectType = Passive` for triggerless `stat.modify`/`stat.derived`, or the grant never fires (§14.2); bake **pre-multiplied `(Min, Max)`** for curve-scaled values so no curve row travels (**D9**). ⚠️ Write "the Writer", never the type name — `guard-funnel-delta.ps1` matches it in comments. Dependencies: E6, E13, **E1-follow-up**.
- [ ] **E8: content-hash** — **sort-then-concatenate** (XOR-fold banned: it cancels duplicates); columns **length-prefixed**, not `\x1f`-separated, or two rows can forge the same digest; covered tables are a **versioned registry**, and a differing `contentHashSchemaVersion` compares shared per-table digests rather than refusing outright. Dependencies: E4, E5.
- [ ] **E15: atom-runner** — Hot runner: trigger index, per-binding ICD/cooldown/charges, **`capPerMatch`**, predicate eval, Funnel dispatch. Gate order cheapest-first, cap last. Exact-count acceptance rows run on a Core-hosted double until E19 supplies a per-match seed. Dependencies: E7, E13, E3, E2.
- [ ] **E19: compiled-push** — server → injector delivery of compiled grants + runner entries; extends `effects.grants.apply` on Hello; negotiates `catalog_revision`; injector holds no content rows; **carries the per-match seed** so injector rolls become replayable (**D5**). **Creates `tests/FusionRpg.Server.Tests`** — the project does not exist. Dependencies: E7, E8, E15.

## Wave 3 — the proof (Checkpoint D)

- [ ] **E14a: importer** — seed/migration file format, `tools/AtomImporter`, schema-validation wiring, all-or-nothing import, `catalog_revision` bump once per transaction. ⚠️ `guard-dal.ps1` scans only `src/`, so `tools/` is a blind spot: the importer **must** call `RpgStore` upserts and open no connection of its own. Dependencies: E5, E8.
- [ ] **E11: effect-def-migration** — 16 defs → rows; **49** fixtures byte-identical; `subject: target` on migrated `OnDamageDealt`; `fx.patron_aura` as a zero-atom marker; **owns `OneRowClaimTests`** (Checkpoint D's claim, tested where it is claimed). Multi-trigger defs resolved: `fx.shield_grant` → 3 atoms sharing `icd_key`, one clock; `fx.passive_atk_flat` → **1** atom, no trigger. `shield.grant.amount` is **optional** with a bind-time presence check against the overlay (**D10**) — authoring a magnitude would break byte-identity. **Step 0:** thread a catalog source through the **five** call sites hardcoding `EffectSeedCatalog.CreateAll()` — `BattleEffects.cs`, `SimEffectHost.cs`, `EffectRuntime.cs`, `CheatCommandRunner.cs`, `FoundationHarness.cs`. The `VfxCatalog` mirror is **verified phantom**; just fix its stale comment. Dependencies: E7, E8, E14a.

## Wave 4 — power

- [ ] **E18: element-roster-data** — roster + **two** matrix tables to data; append-only ordinals; the 84 literal becomes `families × (roster + omni)`. Needs a **build-time generator** for the enum mirror (a C# enum cannot be generated from rows at load; precedent `tools/DemonCatalogGen`). Does **not** move goldens. Dependencies: E4, E8.
- [ ] **E9: power-vector** — owns `ActorPowerCache` (memo key includes `truncateSpawns`) and the `power_trigger_frequency` table; 5-category vector, data-backed coefficients + proposal table, override with required note, spawn recursion depth 1, budget as validation. **Carries §13 D1–D4 as an accepted limitation**: pin the integer fixed-point scale and rounding point for `conditionality` (today `chance/1000` is 0 for every proc below 1000‰), floor `count` and `expectedTargets` at 1, and decide whether a nonlinear cross-channel price is worth fitting — budgets, sorting, and AI reads do not depend on it. Dependencies: E4, E2.
- [ ] **E10: power-reads** — `geomean(vᵢ+1)−1` scalar over **all five** categories (pin its type and rounding — `pow` is not bit-reproducible and E10 stamps into hashed reports); matchup-conditioned read (two matrices); marginal read; AI contract enforced by architecture test. Dependencies: E9, E18.

## Wave 5 — expensive

- [ ] **E14b: content validation** — budget test, power-drift test (±25%), content lint (tier gaps keyed on **family+variant**). Re-runs E11's one-row claim as a regression; does not own it. Dependencies: E11, E9.
- [ ] **E12: trait-migration** — ⛔ **owner sign-off; goldens move.** Migrates **1 trait** (`critical-hunter`) and **re-opens `stat.derived` for battle** by shipping its first consumer (`BattleStatComposer` at squad build) — without that its own bind is rejected `RuntimeUnsupported`. Also **stamps `contentHash` into fixture reports** (deferred here from E8, because a stamped field *is* a golden diff). The other 13 traits are blocked. Predicted delta **zero**. Must not collide with the battle-timeline gate. Dependencies: E11, E14b.

## Later (after E14b)

- [ ] **E16: channel-extension** — `attackInterval`, `produceInterval`, `zombieSpeed` become real channels (8 → 11); direction-aware; cheat keys route through `Override`; extras path stops writing them. Does **not** fix G8. Dependencies: E11, E9, E14b.
- [ ] **E17: status-payload-completion** — wire the **three** real Unity CC branches (`ember`, `jala`, `kelp`); `ModifyStat` payload consumer; `leech` heal half; `charm_pulse` is a **def error** to fix, not wiring — no vanilla method exists; resolve `poison`'s three-way inconsistency. Cross-stream with status. Dependencies: E11.

## Unowned, tracked

- [ ] **`effect_channel_policy`** — compose kind, default, and **cap** (the 0.95 resist cap) were decided to be data, but the table has no owning module and is in no covered-hash list. Changing a cap would move every battle golden with an unchanged `contentHash`. Either give it a module and register it with E8, or strike it.
