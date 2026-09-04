# Task list: item

Plan: [item-plan.md](item-plan.md). Specs: [docs/architecture/item/](../docs/architecture/item/).
Rulings **D1–D35**: [item-ideal.md](../docs/architecture/item-ideal.md).

Each task is a **vertical slice** — schema, logic, tests and the surface that proves it, together.
No task is done until its verification command is green.

---

## Phase 0 — dependency resolution + one regeneration pass

### P0.1 — Get an accept-or-decline on every external dependency

- [ ] **X7** — D27's `container_kind` values (`gem` · `set` · `charm` · `combo`, + the fifth
      `consumable` D27 did not mint). `ContainerRow.cs:7-14` ships six values and none of them.
      Owner: **effect-atom**. ⛔ Gates modules **12, 13, 16, 18, 21**
- [ ] **X4** — L0 pool composition (`effect-pipeline/spec-affix-channel-weights.md`, specced/unbuilt).
      Owner: **effect-pipeline**. Gates **11, 13, 15, 16, 17**
- [ ] **X6** — `E44 power-sweep`; the 20 coefficients are flat at `CoeffMilli = 1000`.
      Owner: **effect-atom**. Gates module **9**
- [ ] **D28 / E43** — family tags stamped into `AtomRow.TagsJson`. Owner: **effect-atom**.
      Gates module **8** (every tag-gated rule is inert without it)
- [ ] **`bind_ordinal` on `effect_binding`** — requested by `ssot-sockets` §5.4, **absent** from the
      shipped DDL. Owner: **effect-atom**. Gates module **16**
- [x] **X3** — ✅ **D36: nothing to do.** `action-corpus` owns `ActionSeeder.Generate` and is under
      active construction by another owner. We consume a production caller when one ships. ⛔ **Do not
      file a request against their map, propose amendments to their scope, or read their documents to
      infer their schedule.** Gates module **19** only; module 19 ships GA2 standalone meanwhile
- [ ] **X2 residue** — `E42 units-correction` closed its gate 2026-09-03, but **`ssot-affixes.md` was
      explicitly out of E42's scope**, so the item-side units residue is unresolved. Owner:
      **content-stack**. Does not block authoring (`seed-contract.md` §3's band rule closes the units
      trap by construction) — but it must be *closed or declined*, not assumed closed with the gate
- [ ] **X5** — the content ladder past level 10. Owner: **world map · wave catalog · event generator**.
      D29 makes the ladder unbounded; does not gate a build, but bounds what any of it is worth

**Acceptance:** each row is *accepted with a target*, *built*, or *formally declined in that program's
map*. A decline moves its dependents to Phase 5 with the decline recorded.
**Verify:** each external map carries the row. Silence is not a pass.

### P0.2 — seedsmith: `theme-refresh` (D34)

- [ ] Republish `themes.v1.json` over the **whole** species corpus
- [ ] Add a staleness check to the pipeline so a snapshot can never drift silently again

**Acceptance:** the registry covers every shipped species; the check fails a deliberate drift.
**Verify:** `python -m pytest tools/seedsmith`; registry count equals
`ls data/seed/demons/species/{plant,zombie} | wc -l` (**386** today: 292 + 94).

### P0.3 — seedsmith: `theme-enrich` (D34)

- [ ] LLM stage: for any theme at `basis: "name"`, generate the flavour text that raises it to
      `basis: "text"` — same shape and honesty contract as `family-extract` / `motif-derive`
- [ ] `audit_schema` mechanically confirms the stage emits no number

**Acceptance:** **zero** themes remain at `basis = "name"`.
**Verify:** `python -m pytest tools/seedsmith`; module 13's `no_theme_reaches_generation_at_basis_name`.

### P0.4 — seedsmith: `X1 frame-classify`

- [ ] LLM stage: each species' body frame — `humanoid` | `plant` | `hybrid` — from name + flavour text,
      carrying `basis`
- [ ] ⚠ **Frame publishes independently of theme status.** A `basis = blocked` demon still has a body;
      `spec-demon-themes.md` makes publishing its *theme* a Never, and frame is not a theme
- [ ] ⛔ Runs **after** P0.2, never against the stale snapshot

**Acceptance:** every species carries a frame; `DemonSpeciesDef.Side`'s faction/body conflation is
resolved (`peashooterzombie`, `ironpeazombie`, `cherrynutzombie`, `bucketnutzombie` are zombie-**side**
with plant **bodies**).
**Verify:** `python -m pytest tools/seedsmith`; frame count equals species count.

### P0.5 — ⭐ The one regeneration pass: `core.v1.json` v2 + `classes.v1.json` v4 (D30 + D35)

**`core.v1.json` → registryVersion 2 (D30), three changes that travel together:**

- [ ] `hybridEligible` → `false` on `head-guard` and `sense`; **`true` on `jewel-minor-b`**; add
      `hybridDropReason` for the two new drops, remove it from `jewel-minor-b`
- [ ] The `hybrid` frame's `meaning` prose → **12 roles, dropping `ward-array` · `head-guard` · `sense`**.
      ⚠ `registries.py:105`'s `HYBRID_FRAME_CITATION` is asserted substring-present by
      `tools/seedsmith/tests/test_items_adapter.py:85` — **registry prose and Python constant move in
      one commit or that test goes red**
- [ ] `linkage.py:28`'s `NON_HYBRID_ROLES` — the gating half
- [ ] `adapters/items/registries.py:111`'s `HYBRID_FRAME_EXCLUDED_ROLES`
- [ ] Correct D3's own prose from *"both jewels"* (eleven) to the twelve

**`classes.v1.json` → registryVersion 4 (D35):**

- [ ] Lift the **32-family** global exclusion — its stated reason (*"quarantined None/None/None (D6);
      no executor until E12"*) expired when `AtomKindRegistry.cs:255` shipped `Full/Full/None`
- [ ] Refill the **five** stopgap slates from each role's real §2.3 cluster: `ward-array` (2),
      `head-guard` (2), `sense` (2), `footing` (2), `mantle` (3). ⚠ **Five, not four** — the registry's
      own `_meta.designNotes` misses `footing`
- [ ] Add the **directional-profile field** the entry shape lacks (`seed-contract.md:324-343`,
      `adapters/items/kinds.py:49-51`)
- [ ] Fix the stale `frozenNote` (reads *"FROZEN v2"* at `registryVersion 3`)
- [ ] **Re-author the 18 legacy sets** under the twelve-role cap — the same generation run module 13
      performs for the ~904, so no extra pass

**Acceptance:** ⚠ **corrected 2026-09-04 against a measured result, not a prediction.**
`Linkage/SetCompletability` (which **gates**) is *not* clean against the corrected core — correcting
the core is exactly what makes it report the 18 findings it was blind to before (measured:
`seedsmith check --adapter items --gate` goes from exit 0 to exit 1). **That is D30's accepted cost,
not a failure of this step** — the metric goes clean again only when module 13 regenerates the 18
legacy sets (Phase 3, "no additional pass" per D30). No role carries a stopgap slate; the directional
field exists.
**Verify:** `python -m pytest tools/seedsmith` green (**1497 passed** — code correctness); the gating
metrics carry one **named, ruling-anticipated** red (`SetCompletability`'s 18) until module 13 runs.

> ### ✅ CHECKPOINT 0
> Every external dependency accepted, declined or built. Both registries bumped in **one** pass.
> Seedsmith's `pytest` suite green. ⚠ **Amended 2026-09-04:** the gating *content* metrics carry one
> named exception — `Linkage/SetCompletability`'s 18 `SetRoleNotHybridCore` findings, which D30 itself
> anticipated and accepted ("silently leaving the gate blind is the only expensive answer") and which
> close only when module 13 regenerates those sets. **Not** a Checkpoint 0 blocker; a tracked, dated
> exception with a named closing module.

---

## Phase 1 — the spine to the payoff

### ✅ P1.1 — Module 1 `durable-ownership` ⭐ standalone value — BUILT AND VERIFIED 2026-09-04

- [x] `rpg_item` — PK `instance_id`, 1:1 with `effect_instance`, carrying `player_id`, `acquired_utc`,
      `origin_kind`/`origin_ref`, `locked`, `seen`, `stale`, `disposition`, `note`, `revision`.
      **No magnitude ever lands here** — `RpgStore.Items.cs` (new); `Rpg_item_is_one_to_one_with_effect_instance`,
      `No_rolled_value_is_duplicated_into_rpg_item`
- [x] Orphan sweep predicate → **two reachability roots**: `NOT HasBinding AND NOT HasOwner`
      (`RpgStore.AtomInstances.cs` — `CollectOrphanInstancesUnlocked`, `CountOrphanInstances`)
- [x] **D9** — `ResolveBindings`: replaced strict `catalog_revision` equality with a **per-atom** test
      (exists in catalog; enabled — already owned by `BindGate.Check`, now reachable; identity-fields
      unchanged via `AtomIdentityDigest`, a content-hash compare scoped to `kind_id` per D32). ⚠ **D9's
      original premise was false** — confirmed: `ValuesJson` is read only at `Instantiator.cs`'s content
      fingerprint, never at bind. Evidence: `A_content_import_leaves_untouched_items_bindable`,
      `An_atom_whose_kind_changed_since_rolling_is_refused_and_only_it`,
      `A_disabled_atom_refuses_only_the_instances_carrying_it` (`BindResolutionTests.cs`)
- [x] `effect_binding` FK with `ON DELETE CASCADE`, matching `definitions.md:316`'s promise. ⚠
      **Corrected while building module 2**: this is a REAL, enforced constraint, not documentation —
      `Microsoft.Data.Sqlite` enables `PRAGMA foreign_keys` by default per connection (unlike the raw
      SQLite C API), verified empirically (a fabricated `instance_id` in a new armoury test threw
      `SQLite Error 19: FOREIGN KEY constraint failed`). `DeleteInstance`'s explicit ordered deletes
      are deliberate belt-and-braces on top of a cascade that already fires, not a workaround for a
      missing pragma. Evidence: `Deleting_an_instance_cascades_its_bindings_and_ownership`
- [x] `AtomRowValidator` — reject an empty `effect_atom.name` (C3), placed **last** in `Validate` so a
      row with a more specific defect is refused for that reason first. Evidence:
      `An_empty_atom_name_is_rejected_at_load` (Data.Tests + Core.Tests)
- [x] `ContentRuleViolated` (34th and final member of `AtomRejectionReason`, `AtomRejection.cs`) +
      `ContentRuleNamespaces` registry; **first real consumer is C3**. Evidence:
      `ContentRuleViolated_carries_a_registered_rule_namespace`,
      `Rejection_reasons_are_the_closed_list_of_thirty_three_plus_the_namespaced_catch_all`
- [x] **D32: `ValuesJson` is NOT made authoritative at bind.** `ResolveBindings` still reads the live
      catalog for magnitudes; only `kind_id` gates compatibility

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests` | **5315 passed / 14 failed** — exactly the pre-build baseline (§ below); zero new failures |
| `dotnet test tests\FusionRpg.Data.Tests` | **646 passed / 2 failed** — exactly the pre-build baseline (`DemonSpeciesImportCliTests`, unrelated); +7 new tests, all green |
| `dotnet test tests\FusionRpg.Guard.Tests` | **162 / 162**, unchanged |
| `.\scripts\guard-dal.ps1` | `DAL GUARD OK` |
| `python scripts\audit-overflow.py` | 0 critical, 44 findings — none in new code |
| `python scripts\audit-magic-numbers.py --summary` | 12 findings, none in new code |

**Files:** `src/FusionRpg.Core/Effects/Atoms/{AtomRejection.cs, AtomRowValidator.cs, AtomIdentityDigest.cs (new), Instantiator.cs}`;
`src/FusionRpg.Data/Sqlite/{RpgStore.AtomInstances.cs, RpgStore.Items.cs (new), RpgStore.cs}`;
`tests/FusionRpg.Data.Tests/Items/OwnershipTests.cs` (new, 7 tests); `tests/FusionRpg.Data.Tests/BindResolutionTests.cs`
(1 test rewritten — it asserted the blunt-check behaviour R2 removes — + 2 new); 12 test fixture files
across Core.Tests/Data.Tests given a `Name` (C3's blast radius, all mechanical, zero behavior change on
real content — all 66 shipped seed atoms already carry names, verified before the change shipped).

⚠ **Two flaky, pre-existing, unrelated test failures observed under parallel xUnit execution**
(`Battle.Timeline.TimelinePurityGuardTests`, `Injector.PatronAuraOverlayTests`) — both pass 100% in
isolation, both touch code this module never edited (a shared-static race in test infrastructure). Not
fixed here: out of item-program scope, matching the standing rule on the other streams' red tests.

**Acceptance:** unequip leaves the instance intact; an import invalidates only items whose atoms
actually changed; cascade tested; empty name fails at load.
**Verify:** `dotnet test tests\FusionRpg.Data.Tests --filter AtomInstances`;
`dotnet test tests\FusionRpg.Core.Tests --filter BindResolution`; `.\scripts\guard-dal.ps1`

### ✅ P1.2 — Module 2 `armoury` — BUILT AND VERIFIED 2026-09-04 (Core + DAL; endpoints deferred)

- [x] One **player-scoped** store, no per-specimen bags — `rpg_item_stock`/`rpg_item_rule`/
      `rpg_item_event`/`rpg_item_loadout(_entry)` (`RpgStore.Items.cs`); two storage grades
      (`StorageGrading.GradeOf`, derived from `PrefixRolls`/`SuffixRolls`, never authored); category +
      list surface (`ArmouryQuery` — filter/sort/keyset-page, zero SQL, `guard-dal`-clean);
      **unlimited capacity** — the only ceiling is `InventoryCeiling = 20_000`, an abuse guard with its
      exemption comment, enforced only in `AcquireItem`
- [x] Bulk actions with their four structural guards — `SalvageGuards.Preview` (G-A assigned, G-B
      locked, G-C loadout membership implies lock, G-D best-in-role excluded-by-default), preview
      returning the exact eligible-id list a commit would reuse verbatim
- [x] The **comparison algorithm** — `ArmouryCompare`: per-channel delta (labelled, never summed
      across channels — SC4), a dominance verdict with a genuine fourth `Incomparable` state for
      disjoint channel sets, and roll-quality ‰ from the atom's authored `[min,max]`. **No invented
      scalar** (SC9) — verified by reflecting `CompareResult`'s own properties in a test

⚠ **Deferred, not skipped — explicit, not a silent scope cut:** `ItemEndpoints.cs` (the six REST
routes) and loadout **apply** (writes `rpg_item_assignment`, which is module 4's table — the spec
itself sequences apply with-or-after module 4). Both need a live HTTP surface / module 4 to mean
anything; building them now would be scaffolding with nothing to call it. The loadout **library**
(save/list/get-entries) ships now, as the spec requires.

⚠ **Found while building, corrected in place:** `effect_binding`'s `ON DELETE CASCADE` (module 1) was
documented as "not really enforced, `DeleteInstance` is the real cascade" — wrong.
`Microsoft.Data.Sqlite` enables `PRAGMA foreign_keys` by default, so the FK **is** live; a test using a
fabricated `instance_id` threw `SQLite Error 19` and proved it. Comments and this file corrected; a new
test (`A_fabricated_instance_id_is_refused_by_the_enforced_foreign_key`) pins the corrected
understanding down.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Armoury` | +18 new (`ArmouryQueryTests`, `ArmouryCompareTests`, `ArmouryGuardsTests`), all green |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5334 passed / 14 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **654 passed / 2 failed** — exactly the pre-build baseline (`DemonSpeciesImportCliTests`, unrelated); +8 new tests, all green |

**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — stock/rule/event/loadout tables + CRUD +
`AcquireItem`/`InventoryCeiling`); `src/FusionRpg.Core/Items/{ArmouryQuery.cs, ArmouryCompare.cs,
SalvageGuards.cs}` (new); `tests/FusionRpg.Data.Tests/Items/ArmouryTests.cs` (new, 8 tests);
`tests/FusionRpg.Core.Tests/Items/{ArmouryQueryTests.cs, ArmouryCompareTests.cs, ArmouryGuardsTests.cs}`
(new, 18 tests).

⚠ Same two flaky, pre-existing, unrelated tests observed intermittently under parallel execution
(`Battle.Timeline.TimelinePurityGuardTests`, plus this run added `ClassSystem.CombatSimJsonEmitTests`,
also 100% green in isolation) — noted, not fixed, out of scope.

### ✅ P1.3 — Module 3 `slot-roles` — BUILT AND VERIFIED 2026-09-04 (schema populated in full; X1 species-lookup still pending)

- [x] `item_role` — 15 roles, `standard` declared and ungenerated (D14) — `ItemRole` enum +
      `ItemRoleRegistry.Parse` (Core, pure parser, no file I/O, matching every other `*TuningLoader`)
      + `item_role` DAL table, seeded from `core.v1.json` via `RpgStore.SeedRoles`, never transcribed
- [x] `item_role_frame` — **schema, and fully populated**, not just declared. Static role×frame
      legality (humanoid/plant host all 15; hybrid hosts the 12 with `hybridEligible`) is entirely
      registry-derived, so it needed no X1 wait at all — corrected mid-build from the original bullet
      that implied otherwise
- [x] **⭐ D30's registry bump landed for real** — `core.v1.json` → `registryVersion 2`: `jewel-minor-b`
      → hybrid-eligible, `head-guard`/`sense` → not, hybrid frame `meaning` prose corrected to name 12
      roles. Seedsmith's `registries.py` (`HYBRID_FRAME_CITATION`, `HYBRID_FRAME_EXCLUDED_ROLES`) and
      `linkage.py` (`NON_HYBRID_ROLES`) moved in the same pass, exactly as the spec requires ("three
      changes travel together"). Verified the two now agree character-for-character, not just by eye
- [x] The **twelve-role hybrid core** (800‰), issued here for modules 8, 12, 13, 16, 21 — verified
      against the live registry: 12 roles, 800‰, all three jewels present, `footing` present,
      `head-guard`/`sense`/`ward-array` absent
- [x] The unlock predicate, **defaulting to always-open** — `SlotUnlock`/`ISlotUnlockRule` (D2): no
      slot unlocking in v1, but the mechanism is *reserved* and provably closable without a migration
- [x] **The 20 legacy `standard` base-type entries are retired** — `enabled: false` +
      `retiredReason` added to all 20 (`humanoid-standard.json`, `plant-standard.json`), file kept, id
      never reused, per `seed-contract.md` §7.2 and the owner's ruling
- [ ] ⏸ **Still deferred to X1:** populating the per-actor **species → frame** lookup (a species-keyed
      table, distinct from `item_role_frame`). Everything else in this module needed no X1 wait

⛔ **Real, evidenced consequence found while fixing the registry — not a defect in this module's own
work, but it must be said plainly.** Correcting `core.v1.json` makes `seedsmith`'s
`Linkage/SetCompletability` metric (`gates = True`, wired into CI at `ci.yml:220`) report its
previously-blind **18 findings** — one per legacy set using `head-guard`/`sense`. Measured directly:

| Command | Before this module | After |
|---|---|---|
| `python -m seedsmith check --adapter items --gate ../../data/seed/items` | exit 0 (blind to D3) | **exit 1** — 18 `SetRoleNotHybridCore` findings |

**This is D30's own anticipated and accepted cost, not a regression** — D30's ruling text says
verbatim *"Silently leaving the gate blind is the only expensive answer."* The fix is module 13's
(`set-charm-gen`) regeneration pass, explicitly sequenced later (Phase 3) and explicitly "no
additional pass" per D30, since module 13 regenerates the ~904 anyway. **CI's items-check step stays
red until module 13 runs.** Recorded here so nobody mistakes it for a build breakage introduced by
something else — Checkpoint 0's "seedsmith gating metrics are green" wording is corrected below to
name this one, ruling-anticipated exception explicitly.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter SlotRoles` | +14 new (`SlotRolesTests`), all green — the real, shipped `core.v1.json` is read directly, never a fixture |
| `dotnet test tests\FusionRpg.Data.Tests --filter Items.SlotRolesTests` | +4 new, all green |
| `python -m pytest` (seedsmith, full suite) | **1497 passed, 1 skipped** — up from 1489; one pre-existing test asserting the OLD 13-role shape corrected to assert the ruled 12-role shape |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5348 passed / 14 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **658 passed / 2 failed** — exactly the pre-build baseline; +4 new tests, all green |
| `dotnet test tests\FusionRpg.Guard.Tests` | **162 / 162**, unchanged |
| `.\scripts\guard-dal.ps1` | `DAL GUARD OK` |

**Files:** `data/seed/items/_registry/core.v1.json` (EDIT — registryVersion 2);
`data/seed/items/base-types/{humanoid,plant}-standard.json` (EDIT — 20 entries retired);
`tools/seedsmith/seedsmith/adapters/items/registries.py` + `metrics/linkage.py` (EDIT);
`tools/seedsmith/tests/test_items_adapter.py` (EDIT — 1 test corrected, 2 added);
`src/FusionRpg.Core/Items/{ItemRole.cs, FrameVocabulary.cs, SlotUnlock.cs}` (new);
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — `item_role`/`item_role_frame` + `SeedRoles`);
`tests/FusionRpg.Core.Tests/Items/SlotRolesTests.cs`,
`tests/FusionRpg.Data.Tests/Items/SlotRolesTests.cs` (new).

### ✅ P1.4 — Module 4 `equip-assign` — BUILT AND VERIFIED 2026-09-04 (relic migration explicitly deferred)

- [x] `rpg_item_assignment` — durable assign (`SaveAssignment`/`RemoveAssignment`/`ListAssignments`,
      one row per `(specimen_id, role)`); binding **rebuilt as a projection** at deploy, never patched
      — `EquipProjector.Project`, proven by an **out-of-band** delete + re-project (the test an
      append-only implementation could not pass by accident)
- [x] **The gate's frame arm ships INERT, proven not assumed** — `The_frame_arm_is_inert_while_no_species_carries_a_frame`
      constructs an actor with `Frame: null` (X1's real state today) and shows `Admits` never refuses
      on frame regardless of the item's own frame. Predicate and level arms are live and tested
      end-to-end. **Never stubbed a default frame**
- [x] ⚠ **X7 touches this module too** — named in `UnassistedAttributes`'s own doc comment (`gem`/
      `set`/`charm` excluded **by string**, not by `ContainerKind` enum member, so the filter is
      already correct for the day X7 mints them). Not a blocker today: no charm kinds shipped, so the
      hole cannot be exercised
- [x] Two distinct gates, **and their disagreement is asserted, not just avoided**: `Admits` (hard,
      all four axes) and `Projectable` (deploy, excludes only the level check).
      `A_lapsed_level_req_reports_a_shortfall_and_keeps_the_binding` proves `Admits` refuses while
      `Projectable` stays true for the identical input — filtering standing assignments through
      `Admits` would be the force-unequip bug D19/I11 §2.6 rejects
- [x] `UnassistedAttributes.Filter` — I11 §2.7's cycle rule, with a structural proof
      (`An_equippable_grant_cannot_flip_an_admission`) that an item-sourced value never reaches the
      actor snapshot the gate reads, not merely a claim that it doesn't today
- [ ] ⏸ **Deferred, not skipped — explicit:** retiring `rpg_unique_equipment` / `UniqueEquipmentCatalog`
      and the relic row migration. The confirmed disposition is **relics become uniques**, but module
      17 (`uniques`, Phase 5) does not exist yet to migrate them *into* — retiring the stub before that
      module has a shape would be exactly the boundary this spec names: *"Never retire
      `rpg_unique_equipment` before relics have a home."* `RelicCatalog`, `RelicEndpoints` and the FE
      layer are **untouched** and continue serving the four shipped relics exactly as today
- [ ] ⏸ **Deferred with module 2:** loadout **apply** (writes `rpg_item_assignment`) — the spec itself
      sequences apply with-or-after this module, which is now the "after". Deferred again to whichever
      later pass wires a real caller; the library (module 2) already ships

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter EquipAssign` (`Items.EquipAssignTests`) | +9 new, all green |
| `dotnet test tests\FusionRpg.Data.Tests --filter Assignment` (`Items.AssignmentStoreTests`) | +6 new, all green |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5357 passed / 14 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **664 passed / 2 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Guard.Tests` | **162 / 162** |
| `.\scripts\guard-dal.ps1` | `DAL GUARD OK` |

**Files:** `src/FusionRpg.Core/Items/{EquipGate.cs, UnassistedAttributes.cs, EquipProjector.cs}` (new);
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — `rpg_item_assignment` + CRUD);
`tests/FusionRpg.Core.Tests/Items/EquipAssignTests.cs`,
`tests/FusionRpg.Data.Tests/Items/AssignmentStoreTests.cs` (new).

⚠ **`RoleLocked` is used as an internal `EquipRefusalReason`, not yet as I13's official 15th reason
code** — the spec marks minting a 15th code an Ask-first against a closed *spec* vocabulary; using a
clearly-named value in this module's own (non-spec) result enum is a different, smaller thing and
does not require that sign-off. Ratifying it as an official code is still open.

### ✅ P1.5 — Module 5 `equip-runtime` ⭐⭐ THE PAYOFF — BATTLE HALF PROVEN 2026-09-04; LAWN PUSH + CORNER RUN DEFERRED, NAMED

- [x] `EquipAtomSource` — mirrors the shipped `TraitAtomSource` (E12) exactly: only `stat.derived`
      atoms contribute, same `CostFunction.Read` param parsing. Production shape reads through
      `ResolveBindings(OwnerScope.UniqueActor(specimenId))`, proven end-to-end in Data.Tests
- [x] `BattleStatComposer` gains `Equipment` (an `EquipAtomSource`), folded the same way `Traits`
      already folds — **⭐ the payoff itself, proven**: `An_equipped_item_changes_a_battle_number`
      constructs a real equipped atom and asserts the exact channel delta on a real
      `ActorDerivedSnapshot`, no mocking of the composer itself
- [x] `ApplyEquipProjection` (DAL, new) — module 4's projection reaches real `effect_binding` rows at
      `unique-actor:` scope: withdraws an instance no longer projected, binds one newly projected,
      touches neither for one already correct (never a delta relative to the OLD binding state, always
      recomputed from the live assignments). Proven: `ResolveBindings(UniqueActor)` genuinely surfaces
      the atom a production `EquipAtomSource.FromResolver` caller would read
- [ ] ⏸ **Deferred, explicit — the live lawn push.** `RpgHub.cs`/`AtomPushService.Build` still resolve
      only `OwnerKind.Player`; pushing `UniqueActor` scopes too needs `AtomPushService` extended to
      merge multiple owner scopes into one `AtomPushDto` (its wire shape already supports this — every
      `EffectGrantDto`/`RunnerBindingDto` carries its own `ownerKey`) and `RpgHub` to enumerate a
      player's deployed specimens. **Verified, not assumed, that no Injector edit is needed for this**:
      `GrantedDerivedAtomReader.Read` (the lawn read side) is already scope-generic — no `OwnerKind`
      branching anywhere in it — so once the push carries `unique-actor:` grants, the existing reader
      picks them up. This is genuinely server-side C#, testable via `FusionRpg.Server.Tests` without a
      live game; not attempted this pass, named as the next concrete step
- [ ] ⏸ **Deferred, explicit — the first geared corner run.** `tools\CombatSim` has no `--corners`
      flag today (checked, not assumed), and the SHIPPED corner-matrix tool
      (`tools/DominanceBaseline`, backing `DominanceBaselineTests`) resolves purely over aptitude
      builds via class-system's own `DominanceGuard`/`TerminationGuard` — neither tool has a concept of
      equipped gear. Adding one is a real extension to either a standalone balance tool or
      class-system's own shipped guards, not a wiring gap inside the item program. Not attempted
      blind; named for whoever owns that tool's roadmap next
- [x] ⚠ **`Sim` stays `None`** deliberately, asserted not assumed —
      `Sim_runtime_stays_None_and_the_spec_says_why` checks `AtomKindRegistry.Get("stat.derived")`'s
      support matrix directly (`None`/`Full`/`Full` for Sim/Battle/Lawn)

⛔ **Real defect found and fixed while building this: `specimenId` was modeled as `long` throughout
modules 4–5 (schema, `EquipAssignment`, `SpecimenActor`, `EquipGate`, `EquipProjector`,
`BattleActorSetup.SpecimenId`) — but `OwnerScope.UniqueActor`'s own doc comment states plainly it is
"keyed on the actor's own stable `instance_id`", a kebab-case **string**, matching `effect_instance`'s
id shape, never a numeric id.** Caught before it shipped further, not after: every one of those
types, the `rpg_item_assignment.specimen_id` column, and every test fixture were corrected to `string`
in this same pass. Recorded so no later module copies the wrong type from this one.

⚠ **Also found: `BattleActorSetup.SpecimenId` (a genuine new field, not an alias) moved
`ExpeditionResolverTests.Tier_goldens_are_locked`'s hash** — System.Text.Json serializes a new `init`
property by default, and expedition tier resolution serializes this record into its own golden hash.
Fixed with `[JsonIgnore]`, matching the exact precedent `BattleActorSetup.Index` already documents for
this identical class of problem (a specimen id is always null in an expedition context, since
expeditions build actors from wave/species data, never a real owned demon — not semantically part of
what that hash locks).

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter EquipRuntime` | +5 new (`Battle.EquipRuntimeTests`), all green |
| `dotnet test tests\FusionRpg.Data.Tests --filter Items` | +5 new (`Items.EquipRuntimeStoreTests`), all green |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5397 passed / 14 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **669 passed / 2 failed** — exactly the pre-build baseline |
| `dotnet test tests\FusionRpg.Guard.Tests` | **171 / 171** |
| `.\scripts\guard-single-writer.ps1` | `SINGLE-WRITER GUARD OK` |
| `.\scripts\guard-funnel-delta.ps1` | `FUNNEL DELTA GUARD OK` |
| `.\scripts\guard-dal.ps1` | `DAL GUARD OK` |

⚠ **Mid-build, another stream's concurrent uncommitted edit (`DerivedTurnChannels.cs`,
`DerivedStatTuning.cs`) briefly broke the shared `FusionRpg.Core.Tests` assembly build** (8 compile
errors, none in item files). Per standing instruction this is expected, sanctioned, concurrent work —
not touched; the build was rechecked after their edit settled and came back clean.

**Files:** `src/FusionRpg.Core/Battle/{EquipAtomSource.cs (new), BattleModels.cs, BattleStatComposer.cs}`;
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — `ApplyEquipProjection`, `specimen_id` → `TEXT`);
`tests/FusionRpg.Core.Tests/Battle/EquipRuntimeTests.cs`,
`tests/FusionRpg.Data.Tests/Items/EquipRuntimeStoreTests.cs` (new).

> ### ⭐ CHECKPOINT 1 — THE PAYOFF: **partially met, named precisely**
> ✅ **A real item changes a real number in a real fight** — proven, deterministic, in Core.Tests.
> ✅ The DB half of "on the lawn" (bindings actually created and resolvable at `unique-actor:` scope)
> is proven. ⏸ The live push to a running lawn match and the geared corner run are the two named,
> deferred items above — both scoped, neither attempted blind. Guards green; termination/dominance
> corner run is blocked on the corner-run deferral, not run this pass.
> **Phase 2 may proceed** — nothing in Phase 2 (the content model: base types, rarity, affix legality,
> power reads, item card) depends on the lawn push or the corner run; both remain open, tracked items.

---

## Phase 2 — the content model

### ✅ P2.1 — Module 7 `rarity-bands` — BUILT AND VERIFIED 2026-09-04 (D11/D30 consumer wiring explicitly deferred to modules 6/9)

- [x] ⛔ **E1 before D7 — RULED, D31.** `ssot-rarity.md` §3.8's rule is scoped to **drop** pity in the
      shipped doc, verbatim, with the ordering note ("lands before D7") intact. Verified by reading
      the live file, not the earlier draft
- [x] **E2 — RULED, D30.** Already landed in P1.3 (`core.v1.json` → `registryVersion 2`, the
      twelve-role hybrid core at 800‰) — re-verified here against the spec's own resolution text
      rather than assumed carried over; no `core.v2.json` needed, the ruling lands as a `registryVersion`
      bump in the same frozen file, matching its own `frozenNote`
- [x] **E3 — the two non-summing §3.3 rows, fixed before seeding.** `data/seed/rarity/ladder.v1.json`
      carries the corrected halves (`sprout` 0–1/1–1, `heirloom` 1–2/2–2); a window step keeps the
      halves of the rung below it, pinned as its own test so a third defect cannot be authored
- [x] Seed the ten rungs (`ladder.v1.json` → `AtomSeedFile.ReadRarity` → the standard
      `content.Rarities` import path — no second, hand-written writer), per-rung prefix/suffix floors,
      `rarity_budget` (`RarityBudgetKeys.cs`, SC7-enforced both at the C# call site and inside the
      store, so a raw-row writer cannot bypass it)
- [x] Re-derived I12's drop weights (7→10 rungs, `chaff` as the balancing row at 40,700, `almanac`
      pinned at 700) and I6's enhancement caps (5→10 rungs, re-specified as a shrinking **‰ gain
      asymptote**: `gain(n) = enhance_cap(rung) × n/(n+K)`, `enhance_cap(rung) = 900 × (step(rung)−1)`)
      — both live in `data/tuning/item-rarity.v1.json`, never hardcoded, per the magic-numbers rule
- [x] `power_ceiling` seeded on all ten rungs as the coefficient-independent **ladder share** (‰ of
      top, 0…1000) — the `pinAE` pricing and the `provisional`-flagged `ceilingFor` reader are
      module 9's own job per this spec's own "Users" table (*"9 — `ceilingFor`"*) and its Testing
      Strategy table (*"asserted at the consumer, not claimed here"*); seeding the row is this
      module's complete scope and it is done
- [x] ⭐ **The overlap simulator, claimed and built** — `RarityOverlapSimulator.cs`, seed `20260822`,
      2×10⁵ rolls/rung, re-run against the real corrected `ladder.v1.json`. **A real modeling defect
      was found and fixed while building it, not assumed away:** a two-variance model (tier +
      magnitude only) collapses the four `window`-step pairs (`grafted`/`cultivated`,
      `fused`/`chimeric`, `heirloom`/`firstseed`, `sunwoven`/`almanac` — each pair shares an
      *identical* tier window by design) to a ~47–49% coin flip, failing the invariant outright.
      Reintroducing the **count** variance (summing `PrefixRolls + SuffixRolls` independent tier+
      magnitude draws, the documented schema-floor precision) fixes it: every individual adjacent pair
      with a nonzero pool now lands at 7–25%, comfortably inside 5–30%. `chaff` (the one zero-pool
      rung) is excluded from the pooled statistic — verified its own upset rate is exactly 0% at every
      distance, confirming the exclusion is structural, not convenient

**Two shipped-store defects closed:**

- [x] `RpgStore.Containers.cs`'s `UpsertRarity` can no longer renumber an existing rung's ordinal — a
      self-check inside `UpsertRarityUnlocked` refuses a mismatched ordinal for an id already on file
      (`ContentRuleViolated{rarity.ladder-mutated}`)
- [x] `effect_container.rarity` now has the FK it never had — `ContainerValidator` takes an optional
      `rarityExists` predicate (`ContentRuleViolated{rarity.unknown}`), and **it is wired into both
      real call sites**, `RpgStore.UpsertContainer` and the `ImportContent` batch path (the latter
      checks the union of already-stored rarities and any newly seeded in the same batch) — found and
      fixed a real wiring gap: the validator supported the check from the start, but neither production
      call site was passing the predicate, so `UnknownRarity` could never actually fire before this pass

**Not this module's job, named so nobody re-derives it here:** the `ceilingFor` reader / `pinAE`
live-pricing (module 9); the D11 dominance lint leaving channel-split mode (module 6, consumes the
seeded `power_ceiling` row); `socket_min`/`socket_max`, `reroll_cost_mult`, `salvage_yield` budget keys
(await modules 16/15/14's decided shapes, per SC7 — attempting to seed them now is the exact
regression `RarityBudgetKeysTests` pins against); a light-theme palette for the ten rung colours
(module 20 `item-surfaces`) — `colourToken`s already exist in `core.v1.json` and are asserted distinct
here, but the deuteranope-transform test needs a palette that does not exist yet.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.RarityLadderTests\|Items.RarityBudgetKeysTests\|Items.ItemRarityTuningTests` | **31 passed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests --filter RarityOverlapSimulatorTests` | **9 passed** (new) |
| `dotnet test tests\FusionRpg.Data.Tests --filter Items.RarityBandsStoreTests` | **14 passed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5523 passed / 21 failed** — all 21 in `Demons.*`/`ClassSystem.*`, the concurrent stream's own in-flight work (confirmed by name and by `git status` showing those files mid-edit, none touched by this module); **zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **682 passed / 3 failed** — 2 `DemonSpeciesImportCliTests` (same concurrent stream) + 1 pre-existing `AtomStoreTests.An_unknown_trigger_is_rejected` reason-code mismatch, unrelated to rarity/container/items work; **zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` | **171 / 171**, unchanged |

⚠ **Baseline note:** the full-suite failure counts have grown since P1.3's snapshot (14→21 Core, 2→3
Data) purely from the concurrent demon/class-system stream's own in-progress commits landing between
then and now — verified by grepping every failing test name for `rarity`/`container`/`items` (one false
positive: `SpeciesExpanderTests`'s *demon*-rarity-band test, an unrelated vocabulary collision, not
this module). None are this module's regression.

**Files:** `data/seed/rarity/ladder.v1.json` (new — ten rows, E3-corrected halves);
`data/seed/rarity/README.md` (EDIT); `data/tuning/item-rarity.v1.json` (new — drop weights, enhance
caps, power-ceiling shares, `coefficientTableId` for X6 staleness);
`src/FusionRpg.Core/Items/{RarityLadder.cs, RarityBudgetKeys.cs, ItemRarityTuning.cs,
RarityOverlapSimulator.cs}` (new); `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs` (EDIT —
`rarityExists`); `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs` (EDIT — ordinal-mutation refusal);
`src/FusionRpg.Data/Sqlite/RpgStore.Import.cs` (EDIT — `rarityExists` wired into the import path);
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — `rarity_budget` schema, `SetRarityBudget`/
`GetRarityBudget`, `SeedRarityLadder`); `src/FusionRpg.Server/Program.cs` (EDIT — loads
`item-rarity.v1.json` at boot, calls `SeedRarityLadder` after `store.Init()`);
`tests/FusionRpg.Core.Tests/Items/{RarityLadderTests.cs, RarityBudgetKeysTests.cs,
ItemRarityTuningTests.cs, RarityOverlapSimulatorTests.cs}`,
`tests/FusionRpg.Data.Tests/Items/RarityBandsStoreTests.cs` (new).

### ✅ P2.2 — Module 6 `base-types` — BUILT AND VERIFIED 2026-09-04 (corner-matrix lint mode explicitly owed to module 9; `ContentValidation.cs:71`'s consumer wiring likewise owed to module 6/9's downstream consumers)

⛔ **Addendum 2026-09-04, found while building module 10:** 7 of `infusion`'s `implicit.family` values
used by the shipped `base-types/infusion/**` entries (`atom.buttering`, `chilling`, `blighting`,
`rotting`, `sparking`, `marking`, `bonding`) do not correspond to any shipped `affix-families/*.json`
entry — confirmed via `git show HEAD` to **predate this entire session**, not something this module's
own frame/socket migration introduced. This module's own disjointness check (`FrameDirectionCheck`)
never catches it because it validates legality against `classes.v2.json`'s `legalFamilies`, which
**also** lists these seven as legal (the registry and the corpus agree with each other and are both
wrong about the atoms existing). Pinned as a named, evidenced regression test at
`ItemDisplayTests.Phantom_implicit_families_used_by_real_content_have_no_display_template` (module 10).
Not this module's to fix — authoring the missing atom-family entries is `affix-legality` (module 8) or
an even earlier authoring-wave gap.

⚠ **Two bullets below corrected against the real, fully-read `spec-base-types.md` (492 lines), not the
draft this list was written from:**
- `socketCeiling(role)` is **module 16's**, not module 6's — this module owns the per-entry **value**
  and validates it against module 16's ceiling (forward-seeded here, see below). The old wording had
  the ownership backwards.
- **D37 (`girdle` carries `consumableSlots`) is not in `spec-base-types.md` at all** — grepped the full
  file, zero hits. It is `spec-consumables.md`'s, already correctly tracked at **P5.2 (module 18)**
  below. This was a duplicate misfile under module 6, removed here, not dropped.

- [x] **The 32-family global exclusion list is re-derived against `AtomKindRegistry.cs`, not copied
      from `classes.v1.json`'s frozen designNotes (D35).** `classes.v1.json` → `classes.v2.json`
      (new file, v1 stays frozen and on disk): 15 families carrying the stale *"stat.derived —
      quarantined None/None/None (D6)"* reason are lifted (verified against
      `AtomKindRegistry.cs:287`'s live `RuntimeSupportMatrix(Full, Full, None)` and
      `atom-family-library.md` §3.2's own *"the D6 quarantine is OVER"* banner) — from the global list
      **and** from every role's `excludedForRole`, with the family added back to that role's
      `legalFamilies`. `atom.susceptibility` stays excluded (zero readers, unrelated reason)
- [x] **All eight roles the fix actually touches, not just the five named** — re-reading the registry
      itself (not the spec's own summary) found `armament-primary`, `manipulator`, `infusion` and
      `standard` also carried a stale D6 exclusion in `excludedForRole` beside the five named stopgap
      roles (`ward-array`, `mantle`, `head-guard`, `sense`, `footing`). All eight are corrected — a
      narrower fix would have left three false reasons on the shipped registry
- [x] **The 740-entry corpus is migrated in place, same ids** (`seed-contract.md` §7.2 — "entry is
      wrong, same identity"), two passes:
      1. **Implicit reassignment (D11 clause 1).** 359 entries reassigned across the 15 live roles
         (`standard`'s 20 retired rows, D14, untouched) so every role's humanoid and plant
         `implicit.family` sets are disjoint — **verified twice**: a standalone Python cross-check
         against every legal family (0 illegal assignments) and a new `ItemSeedValidator` check
         (`FrameDirectionCheck`, below) report **zero** violations
      2. **`socketMax` fill + reshape against the role ceiling.** 24 `jewel-minor-a` plant entries had
         no `socketMax` key at all (absent ≠ 0); several roles' existing values already **exceeded**
         their real ceiling (`jewel-major`/`sense`/both jewel-minor roles capped at 1, corpus had
         entries at 2). Filled and reshaped to an even spread across `[0, ceiling]` per (role, frame)
         — ⛔ **a first version of this reshape mixed both frames into one 48-wide rank and collapsed
         `jewel-minor-a` to "every plant entry 0, every humanoid entry 1" by accident** (missing values
         sort lowest, and the two frames' ids happened to cluster on either side of that boundary) —
         caught by re-running the corpus analysis after the first pass, not assumed correct, and fixed
         by reshaping per (role, frame) instead of per role
- [x] **`socketCeiling(role)` forward-seeded** — `data/tuning/sockets.v1.json`, the exact 15-row table
      `spec-sockets.md` §3 already publishes (module 16 hasn't built yet; same precedent as module 7's
      provisional `power_ceiling`, module 16 stays the numbers' owner)
- [x] `ItemSeedValidator` wired to `classes.v2.json` (`RegistrySet.Load`), plus two new checks:
      `FrameDirectionCheck.cs` (clause 1 disjointness, **and** a real gap found while building it — no
      check previously verified an entry's `implicit.family` is even legal for its role at all; both
      now enforced) and `SocketMaxCheck.cs` (every live entry carries a value; none exceeds its role's
      ceiling)
- [x] **`frame-lean.v1.json`** — ten `(ladder, frame)` blocks, eight authored (`armour`/`weapon`/
      `offhand`/`jewel` × humanoid/plant), the `standard` pair declared null per D14. Every humanoid
      block carries `implicitAxis: burst`, every plant block `sustain` — clause 3 correlation holds
      **structurally**, not by a check that could be defeated by relocating the field. Channels are a
      declared balance surface (spec's own "Ask first"), not a locked design: `maxHp`/`atk` (primary)
      and `combat.dodge.omni`/`combat.crit.damage.omni`/`combat.crit.resist.damage.omni` (stat.derived
      at the frame-agnostic `omni` variant — never a specific element, and never `plating`/`carapace`,
      the two zombie-only Unity fields spec-base-types.md names as illegal lean channels)
- [x] `FrameLean.cs` (pure parser + `FrameLeanTable`), `BaseTypeSlate.cs` (role → ladder, per
      `words.v1.json poolAccess.roleToLadders`), `FrameDominanceGuard.cs` — **the `channel-split` mode
      dominance lint, green for all twelve hybrid-core roles.** This is stated as the module's whole
      obligation by the spec itself (*"That is the whole of this module's obligation, and it is
      reachable at build position 6"*) — the stronger `corner-matrix` mode needs module 9's power
      vector and stays a named, owed fixture there, not claimed here
- [x] **`item_category`** — `data/seed/items/_seed/item-category.v1.json` (ten rows, transcribed from
      `ssot-item-categories.md` §5.1, not authored fresh) + `ItemCategoryTable.cs` (parser, SC7
      enforced: an empty `consumer` throws `ContentRuleViolated{item.category-no-consumer}`, following
      §2b.1's namespaced-catch-all rule rather than minting a 34th code). Six rows are `declareOnly`
      (`consumable`, `insert`, `charm`, `blueprint`, `cache` — a named future consumer, unbuilt today;
      `cosmetic` — no consumer ever planned), matching `ssot-item-categories.md`'s own "v1" column
      exactly, not the narrower "four have no consumer today" framing spec-base-types.md's prose uses
      for a different purpose (SC7's shipped-vs-not distinction)
- [ ] ⏸ **`ImplicitFlavourDrift` warning per re-slated entry — deferred, named, not silently skipped.**
      359 entries' `implicit.family` changed; a mechanical reassignment can leave an entry's `name`/
      `flavor` prose describing its OLD family (spec's own anticipated cost: *"an entry keeps its name
      and prose while its implicit family changes... this module emits a warning; it does not call a
      model"*). The drift set itself **is captured** (359 entries, `{id, role, frame, from, to, name,
      flavor}`, scratch JSON from the migration run) but wiring it into `ItemSeedValidator` as a
      standing warning, and handing the list to the authoring fleet, is not yet done — real remaining
      work, not scope creep to invent
- [ ] ⏸ **`ContentValidation.cs:71`'s null-ceiling skip — not this module's to fix.** Named in the old
      todo wording as this module's; re-reading `spec-base-types.md` places it at module 9 (`power_ceiling`
      wiring) and module 6 (D11's own dominance lint) as two SEPARATE consumers of a seeded
      `power_ceiling`, neither of which is this module's `channel-split` obligation. Left exactly where
      module 7's own todo entry already named it as deferred to module 9

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet run --project tools\ItemSeedValidator` | **165 errors, unchanged from the pre-module-6 baseline** — all in `base-types/{humanoid,plant}-standard` (module 3's pre-existing, uncommitted `retiredReason` schema gap, D14 out-of-scope content) and three completely unrelated files (`affix-families/g-board.json` TierGap, `consumables/k3.json`, `enhancement-milestones/milestones.json`, `recipes/recipes.json` TagAxisNotApplicable) never touched by this module. **Zero** findings from `FrameDirectionCheck`/`SocketMaxCheck`/`ImplicitFamilyNotLegalForRole` against the live 720-entry corpus |
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.FrameLeanTests\|Items.ItemCategoryTableTests\|Items.BaseTypeCorpusTests` | **25 passed** (new), including the channel-split dominance lint green for all 12 hybrid-core roles |
| Standalone Python cross-check: every live entry's `implicit.family` against `classes.v2.json`'s `legalFamilies` | **0 illegal assignments** across 740 entries |
| Standalone Python cross-check: humanoid ∩ plant implicit families, per role | **0 violations** across all 15 live roles |
| `python -m pytest` (seedsmith, full suite) | **1498 passed, 1 skipped** — unaffected; seedsmith's `registries.py` reads `classLadders` from `classes.v1.json` only, which v2 never touches (purely additive to `excludedFamilies`/`implicitSlates`) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5657 passed / 2 failed** — both `ClassSystem.UnitClassContractParityTests`, the concurrent stream's own in-flight work; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **682 passed / 3 failed** — same baseline as P2.1's snapshot (2 `DemonSpeciesImportCliTests` + 1 pre-existing `AtomStoreTests` trigger-reason mismatch), unrelated; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` | **170 / 171** — 1 pre-existing `ClassSystemBaselineRegenTests` failure against uncommitted class-system tuning drift (already on file as a known, unrelated issue), not this module's |

**Files:** `data/seed/items/_registry/classes.v2.json` (new — v1 stays frozen);
`data/seed/items/_registry/frame-lean.v1.json` (new); `data/seed/items/_seed/item-category.v1.json`
(new); `data/tuning/sockets.v1.json` (new — forward-seeded ceiling table);
`data/seed/items/base-types/**` (EDIT — 359 implicit reassignments + 406 socketMax fills/reshapes
across 720 live entries, `standard`'s 20 retired rows untouched);
`src/FusionRpg.Core/Items/{FrameLean.cs, BaseTypeSlate.cs, ItemCategoryTable.cs}` (new);
`src/FusionRpg.Core/Balance/Guards/FrameDominanceGuard.cs` (new);
`tools/ItemSeedValidator/Registries/RegistrySet.cs` (EDIT — reads `classes.v2.json`);
`tools/ItemSeedValidator/Checks/{FrameDirectionCheck.cs, SocketMaxCheck.cs}` (new), wired into
`Validator.cs`; `tests/FusionRpg.Core.Tests/Items/{FrameLeanTests.cs, ItemCategoryTableTests.cs,
BaseTypeCorpusTests.cs}` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter Items.FrameLeanTests\|Items.BaseTypeCorpusTests`; `dotnet run --project tools\ItemSeedValidator`

### ✅ P2.3 — Module 8 `affix-legality` (+ item naming) — BUILT AND VERIFIED 2026-09-04 (rare two-word draw wiring, module-3 relocation confirmation, and the distribution-metric CI artefact explicitly deferred)

⛔ **Addendum 2026-09-04, found while building module 10:** `atom.affliction` — one of the fifteen
families this module's own D6-quarantine-lift narrative describes as newly legal on `infusion` — is
itself an eighth phantom family (see P2.2's addendum): legal per the registry, referenced by no shipped
`affix-families/*.json` entry. The lift itself is correct (the registry SHOULD allow it once it exists);
what's missing is the atom content. Not a defect in this module's own work, named here for the same
reason it is named at P2.2.

- [x] **`item_role_family` derived, zero authored cells.** `RoleFamilyTable.Derive` walks the 98
      families' own `roles`/`frames` (656 raw pairs, matching the spec's own corpus measurement
      exactly), applies `family-overrides.v1.json` (the minor-jewel tier-3 cap + the bulwark/savagery
      removal → 652 derived pairs) and `role-relocation.v1.json` (D3's reduced tiers on surviving
      hosts). `FamilyOverrides`/`RoleRelocationTable` are pure parsers over the two new registries
- [x] **`family-overrides.v1.json` — the only per-(role,family) granularity this module ships.**
      §2.5's third pricing mechanism resolved by **removal**: `atom.bulwark`/`atom.savagery` (both
      confirmed legal on both minor jewels before this change) are stripped from `jewel-minor-a`/`-b`
      only — `jewel-major` is untouched by this override (its own `atom.bulwark` cell is separately
      reduced to tier 3 by the D3 relocation below, since bulwark is *also* legal on `head-guard` and
      `sense` — a different mechanism, correctly not conflated in the tests)
- [x] **`role-relocation.v1.json` authored — 619 rows, 0 orphans, module 3's handoff fulfilled
      rather than left silent.** Module 3 (already built, P1.3) never produced this artefact, so this
      module ships the spec's own named default: every family legal on one of the three dropped roles
      (`ward-array`/`head-guard`/`sense`) keeps its surviving hybrid-core host(s) at `max_tier = 3`,
      matching `ssot-equip-slots.md` §4.2's shipped precedent. Computed from the corpus, not
      hand-picked — cross-checked against a standalone Python pass, 0 orphans confirmed independently
- [x] `IlvlTierLadder.cs` — D29's `1/1/8/18/32` (not I8's rejected `1/12/25/40/60`) + the **collapsing
      envelope** (I12's rule, not I8's rejected sliding window — t1 never falls out of the window at
      high ilvl) + `EnvelopeNarrowing` (narrow the roll count and record it, never reject a legal drop)
- [x] `AffixFilters.cs` — frame/side/runtime, runtime read live from `AtomKindRegistry`
      (`stat.derived` Full/Full/None — Sim stays refused, the half of the D6 lift that did **not**
      happen), `warding`/`resilience` flagged match-scope-only (refused everywhere in v1 per D14)
- [x] ⛔ **THE NAMING FUNCTION — built. Nothing owned this before; every dropped item was nameless.**
      `ItemNameComposer.Compose`: 0 affixes → base name; 1-2 affixes → `<prefix> <base> of <suffix>`
      (a slot with no candidate is omitted, not padded); 3+ affixes → a seeded two-word rare name
      (head/tail draw delegated to the caller — module 13/17's pool, not authored here); tie-break
      `(tier DESC, seq ASC)`, **never** `instance_id`/`binding_id`; a `Mixed` (hybrid) affix supplies
      at most one word total, never both ends. Pure, never stored — the reroll-safety and
      `spec-item-card.md:302` byte-identical-name properties fall out of that for free
- [x] **`nameWords` re-keyed across all 98 families, additive, no id/word changes.** Every row is now
      `{band|variant, word, wordPlant?}` instead of a bare string. Classified by word count, not by
      the `variants` field alone — 11 families are mechanically element-expanded (`variants:
      elements+omni`) but ship exactly 3 words and stay **band**-keyed (they already worked
      positionally as A/B/C); the true 27 irregular families (non-3-word) are **variant**-keyed
      (canonical order fire/ice/air/earth/light/dark; a family with fewer than 6 words covers only the
      first *N* elements, and `omni`/an uncovered variant falls back to the list's first word — a
      documented starting choice, not a silent gap) or, for the two families with no `variants` field
      at all (`stalwart`, `immunity`, 4 words each), a generalised *N*-way contiguous band split.
      ⛔ **A real bug was caught and fixed mid-build**: the first pass used a generic even split for
      the regular 3-word case too, giving A=t1/B=t2-3/C=t4-5 — wrong against `ssot-affixes.md` §4.12's
      own **fixed** definition (A=t1-t2, B=t3, C=t4-t5, deliberately uneven). Caught by checking the
      spec's own text against the generated output, not assumed correct; the corpus was reverted and
      regenerated with the fixed split hardcoded for the 3-word case specifically
- [x] **The two documented `wordPlant` overrides applied** — `atom.sunbloom` band C → *"of
      Photosynthesis"* (humanoid *"of Abundance"*), `atom.mending` band C → *"Verdant"* (humanoid
      *"Restorative"*), transcribed from each family's own `notes` field, which already named the
      exact override text. `atom.evasion`'s note names a third pair (*"Shifting"/"Deep-rooted"*) but
      the note's own words never match the family's *shipped* 6-word list — that pair was superseded
      when the family grew to its current per-element wording, and applying it now would be
      inventing content the note does not actually support; left unapplied, not silently guessed at
- [x] `AffixNameTable.cs` — the `item_affix_name` **projection**, parsed straight from each family's
      `nameWords` (`ParseSlot`/`Resolve`), never a second authoring surface; a bare-string row or a
      row naming both `band` and `variant` is rejected
- [x] `seed-contract.md`'s affix-family example updated to the new shape (the additive doc ask the
      spec names) — the old flat-array example is gone
- [x] `tools/ItemSeedValidator/Checks/{RoleFamilyCheck.cs, NameWordCheck.cs}`, wired into
      `Validator.cs`: `RoleFamilyCheck` cross-validates the two new override registries against the
      real corpus (a typo'd family or an override on a role where the family was never legal both
      reject); `NameWordCheck` enforces the new row shape and that a family's bands form a contiguous
      run from A (generalizes past the fixed 3-letter case for the handful of families with fewer/more
      bands) — **found and fixed a bug in the check itself** mid-build (it originally hardcoded
      `{A,B,C}` as a required set, which wrongly flagged the 1-word `bulwark`/`tempo-stampede`
      families as "missing B, C"), and **found two exemplar-file entries the migration script
      correctly left untouched** (`_exemplars/affix-family.exemplar.json`'s `atom.elemental-power`/
      `atom.elpw-amplify`, template content outside the real 98-family corpus) — re-keyed for
      consistency and excluded from both new checks, matching the same `IsExemplar` precedent module 6
      already established
- [ ] ⏸ **Distribution metrics (`Distribution/Evenness`/`Inequality` over the derived table, a CI
      artefact) — deferred, named, not silently dropped.** `gates = False` by the metric family's own
      discipline; this is a measure-only Python/CI change (`distribution.py`'s `_observed_count` plus
      a new `.github/workflows` upload step) genuinely separate from the C#-side legality/naming work
      this pass completed, and real remaining scope
- [ ] ⏸ **The rare two-word name's actual head/tail draw against `words.v1.json`'s pools — not built.**
      `ItemNameComposer` takes the draw as an injected delegate by design (so the pure function needs
      no pool data), but nobody has wired a real `rareNameDraw` yet, and `poolAccess
      .affixFamilyPartitions` still says word pools are out of scope for affix partitions (a named
      "Ask first" this module raises rather than resolves unilaterally)
- [ ] ⏸ **D8's aptitude-affix gate stays inert** — correctly: §2g #2 (a 13th atom kind / `aptitude.*`
      channel family / fifth `AllocationScope`) has not cleared, and the spec is explicit that no
      aptitude affix may be authored until it does. Nothing to build here yet; named so it is not
      mistaken for an oversight

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.RoleFamilyTableTests\|Items.IlvlTierLadderTests\|Items.AffixFiltersTests\|Items.AffixNameTableTests\|Items.ItemNameComposerTests` | **45 passed** (new) |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors — identical breakdown to the module-6 baseline.** Zero findings from `RoleFamilyCheck`/`NameWordCheck` against the real 98-family corpus and the two new override registries |
| Standalone Python cross-check: relocation rows vs. corpus, orphan count | **619 rows, 0 orphans** — matches the spec's own measurement |
| `python -m pytest` (seedsmith, full suite) | **1498 passed, 1 skipped** — unaffected (`kinds.py` only allow-lists the `nameWords` field name, never inspects its internal shape) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5724 passed / 5 failed** — all 5 in `ClassSystem.*`/`Atoms.*`/`ActorHub.*`, the concurrent stream's own in-flight work; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **684 passed / 3 failed** — same baseline as P2.1/P2.2's snapshots (2 `DemonSpeciesImportCliTests` + 1 pre-existing `AtomStoreTests` trigger-reason mismatch), unrelated |
| `dotnet test tests\FusionRpg.Guard.Tests` | **171 / 171** — the one pre-existing `ClassSystemBaselineRegenTests` failure from P2.2's snapshot is gone (fixed upstream by the concurrent stream since then) |

⚠ **Two test assumptions were wrong and corrected against real corpus data, not left to pass on a false
premise.** (1) `IlvlTierLadder.MaxTierAt` at low ilvl: the D29 table's own numbers give t1 and t2 the
*same* minimum ilvl (1), so `MaxTierAt(1) == 2`, not 1 — the code was right, the first draft of the
test assumed a naive strictly-increasing ladder. (2) The relocation test assumed `ssot-equip-slots.md`
§4.2's illustrative "`ward-array`'s shields relocate to `core-guard`" was a literal claim about
`atom.shield-capacity`'s own `roles` list — it names `armament-secondary` and `jewel-major` instead;
§4.2's text is the *mechanism's* precedent, not a fact about this specific family.

**Files:** `data/seed/items/_registry/{family-overrides.v1.json, role-relocation.v1.json}` (new);
`data/seed/items/affix-families/**` (EDIT — 346 words re-keyed across 98 families, 2 `wordPlant`
overrides added, additive); `data/seed/items/_exemplars/affix-family.exemplar.json` (EDIT — 2 template
entries re-keyed for consistency); `docs/architecture/item/seed-contract.md` (EDIT — the affix-family
example updated to the new shape); `src/FusionRpg.Core/Items/{RoleFamilyTable.cs, IlvlTierLadder.cs,
AffixFilters.cs, ItemNameComposer.cs, AffixNameTable.cs}` (new);
`tools/ItemSeedValidator/Checks/{RoleFamilyCheck.cs, NameWordCheck.cs}` (new), wired into
`Validator.cs`; `tests/FusionRpg.Core.Tests/Items/{RoleFamilyTableTests.cs, IlvlTierLadderTests.cs,
ItemNameComposerTests.cs}` (new, the last file carrying both `AffixNameTableTests` and
`ItemNameComposerTests`).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter Items.RoleFamilyTableTests\|Items.ItemNameComposerTests`; `dotnet run --project tools\ItemSeedValidator`

### ✅ P2.4 — Module 9 `item-power-reads` — BUILT AND VERIFIED 2026-09-04 (R2/R10-card wiring left for module 10/19's own production callers; the chaff-chassis watch explicitly carried forward, unanswerable before module 21 exists)

- [x] **All four reads call E9/E10, no vector/coefficient/cost-function declared under `Items/Power/`.**
      `ItemPowerReads.cs` (R1 `ImplicitShare`, R2 `GrantedActionPrice`, R3 `CardPower`) and
      `AptitudeAffixPrice.cs` (R4) are pure call sites over `CostFunction.Price`, `PowerVector`,
      `PowerScalar.Of` and `MarginalRead.Of` — verified as a reflection test over the module's own
      namespace, not just by review
- [x] **R1 — implicit budget share, proven coefficient-insensitive by test, not asserted.** Priced the
      same atom under `PowerTables.Authored()` and a uniform 2× rescale, fed each price in as *that
      table's own* rarity ceiling (mirroring how module 7's `power_ceiling` is itself "the price of a
      reference slate through the same cost function"), and the resulting **share** is byte-identical
      across both tables even though the absolute prices differ — the actual ratio-invariance claim,
      not a weaker one
- [x] **R2 — granted-action price, via the exact path `RungMonotonicity` already uses**
      (`PowerVector.FromCategory(Offense, 1000).ScaleMilli(qPowerMilli)`), reported as a ‰ share and
      flagged `CoefficientSensitive: true` (cross-shape, does not cancel). `qPowerMilli: null` (no
      resolvable rung) is `Unpriced`, never a `0` share — G4's own dominance fear, refused directly
- [x] **R3 — the card's power number, Rule P.** `CardPower` renders `≈ {2 sig figs} (±25%)`, the band
      pinned to `ContentValidation.DriftTolerancePercent` at **tuning-load time** (a mismatched
      `powerDisplayBandPercent` in the JSON throws immediately, not a silent drift), plus
      `ShowPowerOnCard` as the documented reversible suppression (G3 §10 Q7) — a file save, verified by
      a test that flips it and checks nothing else about the read changes
- [x] **R4 — aptitude-affix pricing, specified and correctly inert.** `AptitudeAffixPrice.Read` refuses
      by name (*"no item AllocationScope and no aptitude.* channel family exist yet"*) until §2g #2's
      vocabulary lands; when it does, it prices via `MarginalRead.Of`, never the stored context-free
      price — D8's own amended reasoning (aptitudes are share-normalised, so a stored price cannot see
      what it multiplies against). The gate is doubly guarded: a hardcoded flag (same pattern as
      `RungMonotonicity.PredicatePricingLanded`) *and* a live check that `AllocationScope` still has
      exactly 4 members — a 5th value landing without the flag being flipped fails a test rather than
      silently being believed
- [x] `ItemPowerTuning.cs`/`ItemPowerTuningLoader` — every threshold in `data/tuning/item-power.v1.json`,
      no bare literal in the read code; wired into `Program.cs` at boot (parsed and validated even
      though module 10 is not yet the live consumer, so a bad tuning file fails fast rather than at
      first card render)
- [x] **SC9's stale claim is already corrected — verified, not redone.** `enrichment-contract.md:11-15`
      already carries a dated (2026-09-03) correction naming `D13-VOID` and the three stale-inheriting
      lanes, predating this module's build. The success criterion was satisfied before this pass
      started; recorded here so it is not mistaken for missing evidence
- [ ] ⏸ **R2's actual wiring into a live granted-action consumer — not built.** `GrantedActionPrice`
      exists and is tested against synthetic `qPowerMilli` values; module 19 `granted-actions`
      (`ActionSeeder.Generate` has zero callers) is what would supply a real `actionId → rung`
      resolution. Correctly out of this module's scope per its own boundary ("reportable today,
      gating only when module 19 lands") — named so it is not mistaken for done
- [ ] ⏸ **R3's actual card-rendering caller — not built.** `PowerScalar.Of` becomes a real production
      caller only through module 10 `item-card`, which does not exist yet; `CardPower` is ready and
      tested but nothing in the server/web layer calls it today
- [ ] ⏸ **The chaff-chassis watch (D21/D23/D24 Splice-on-low-rarity-base question) — unanswerable
      before module 21 `strain-splice-gen` exists.** Named here as a real, carried-forward open
      question (not resolved, not dismissed): once Splices are generated, re-check whether one clears
      an `almanac`'s ~770 hp-equivalent implicit price through `ItemPowerReads.ImplicitShare` — if it
      does, module 7's rarity bands need re-deriving. This module supplies the read that answers the
      question; it cannot answer it itself with no Splice content to price

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemPowerReadsTests` | **16 passed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5767 passed / 3 failed** — 2 `ClassSystem.UnitClassContractParityTests` (concurrent stream) + 1 `TimelinePurityGuardTests` that reran green in isolation immediately after (a transient scan-time flake against a concurrently-edited file, the same class of flake already documented earlier this session, not a real regression); **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **684 passed / 3 failed** — identical baseline to P2.2/P2.3's snapshots, unrelated |
| `dotnet build src\FusionRpg.Server\FusionRpg.Server.csproj` | succeeds — the new tuning load (with its load-time band-equality assertion) does not break boot |

**Files:** `data/tuning/item-power.v1.json` (new); `src/FusionRpg.Core/Items/Power/{ItemPowerTuning.cs,
ItemPowerReads.cs, AptitudeAffixPrice.cs}` (new); `src/FusionRpg.Server/Program.cs` (EDIT — loads and
validates `item-power.v1.json` at boot); `tests/FusionRpg.Core.Tests/Items/ItemPowerReadsTests.cs` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemPowerReadsTests`

### ✅ P2.5 — Module 10 `item-card` — BUILT AND VERIFIED 2026-09-04 (whole-catalog render guard, the Card/Compare levels, and the DAL/importer end-to-end wiring explicitly deferred — see below)

⭐ **The template authoring (N1) was already done — a fourth instance of the same pattern** (after
`item_role_family`, `nameWords`, `displayTemplate`'s own existence): `data/seed/items/display-templates/
*.json` already carries all 98 rows (`{name, runtimeFamily, groupId, status}`, authored 2026-08-22),
and **`UnitClass` (N3) was already shipped too** — a real, fully-built 11-member enum at
`Stats/Derived/StatClass.cs` with per-channel data in `DerivedStatRegistry`, exceeding this spec's own
9-member proposal. Neither needed rebuilding; both needed a real consumer, which is what this module
actually was: a wiring pass, not a from-scratch build, exactly like modules 6/7/8's own discoveries.

- [x] **N1 wired.** `DisplayTemplates.Parse`/`Render` (Core, pure) reads the already-authored corpus;
      `RpgStore.ItemDisplay.cs` seeds `item_display_template` at boot from it (`Program.cs`). The one
      change made to the *content*: `atom.stalwart`'s `status` flipped `pending` → `live` (C2 is fixed,
      verified against `ResistanceEvaluator.cs:348`'s own cited line). `atom.entangling`'s `pending`
      left alone — its blocker is an unrelated missing Unity CC branch for `kelp`, not C2
- [x] **N2 generated, not hand-authored a second time.** `content/display/en.json` — 99 keys
      (`nameKey → template`, the one real `plantOverrideKey` pair for `atom.evasion` included) —
      derived straight from the corpus's own `(nameKey, name)` pairs
- [x] **N3 wired via a thin facade, not rebuilt.** `ChannelUnits.For(channelId)` — primary channels
      (`maxHp`, `atk`, `defense`, `hp`, `arm1`/`arm1Max`/`arm2`/`arm2Max`, `attackInterval`,
      `produceInterval`, `zombieSpeed`; `DerivedStatRegistry` is scoped to derived channels only and
      does not carry these) plus a pass-through to the shipped registry for everything else. Returns
      `null`, never a guess, for anything unmapped
- [x] **`DisplayModel.cs`** — `DisplayLine`/`DisplayBlock`/`DisplayModel`/`CompareModel`/`RollBar`/
      `SourceKind` (G3 §4.4's twelve-value closed vocabulary), every human-readable leaf a `{key, args}`
      pair, never markup
- [x] **`ItemDisplayRenderer.cs` — the one line producer.** Rule 1 (the shipped `patronView` percent
      conversion, adopted verbatim: `150‰ → "15%"`, `153‰ → "15.3%"`); Rule 2 (a non-zero per-mille
      never renders `0%`, proven by test); Rule 3 (formatting happens once, at this boundary — the
      caller passes the already-frozen value, nothing here re-rolls); the roll-quality bar exactly per
      `RollPolicy` (`Fixed`/`OnApply` → no bar, `OnInstantiate` → 1–5 segments, a real roll never shows
      empty); a `status != "live"` template throws rather than silently rendering pending content
- [x] **`RarityPalette.cs` — real colour science, not asserted math.** sRGB → CIE L*, WCAG 2 contrast,
      and the Machado/Oliveira/Fonseca (2009) deuteranope + protanope simulation matrices, implemented
      and **cross-checked against the shipped dark palette's own documented figures** (`ssot-rarity.md`
      §3.3's L* 42.1 → 91.9 reproduced to one decimal by this exact implementation — the math is
      verified correct against already-validated data, not merely internally consistent)
- [x] **The light-theme palette ships**, constructed (not eyeballed) to satisfy every rule: `L*`
      DECREASING 48.0 → 4.5 (adjacent Δ ≥ 2.5, distance-2 Δ ≥ 7), monotone under both colour-blindness
      transforms, and — the one new rule light theme adds — **WCAG AA 4.5:1 against white for every
      rung**, which the shipped DARK palette's own top end (`almanac`, L* 91.9) would fail outright
      against a white ground, confirming the spec's own stated reason the direction must flip. A
      negative-control test (a flat, unvarying palette) proves `Validate()` actually rejects a bad
      palette rather than always passing. **A design pass, not final art direction** — the rule set is
      what's locked; the ten hexes are the owner's to revise
- [x] **`content/display/en.json`, `item_display_template` DAL, and boot wiring** all exist and build
      clean, including the load-time seed step in `Program.cs`
- [ ] ⏸ **The whole-catalog "every atom renders" guard — scoped to the 98 template rows, not the
      entire effect-atom catalog.** `Every_template_resolves_with_no_leftover_placeholder_for_both_frames`
      covers every family THIS module's corpus carries a template for; it does not (and cannot yet)
      iterate live `AtomRow`s at Min/mid/Max drawn from a real container, because that needs the
      instance/roll pipeline wired end to end, which is a separate, larger integration this pass did
      not attempt. Named so "every atom renders" is not claimed more broadly than it was tested
- [ ] ⏸ **⛔ Found, not fixed: 8 phantom implicit families with no display template at all**
      (`atom.buttering`, `chilling`, `blighting`, `rotting`, `sparking`, `marking`, `bonding`,
      `affliction`) — pinned as a named regression test
      (`Phantom_implicit_families_used_by_real_content_have_no_display_template`), addended onto
      modules 6 and 8's own entries above since it predates and is outside all three modules' scope
- [ ] ⏸ **The Card and Compare levels — not built.** `ItemDisplayRenderer.Line` (the Line level) is
      built and tested; assembling an instance's full `DisplayBlock[]` (base/implicit/affixes/sockets/
      set/enhancement/requirements, the eleven ordered blocks) and diffing two cards
      (`CompareModel`/`display_model_is_byte_identical_for_one_seed`) both need the real instance/
      container/roll pipeline this pass did not wire — genuinely larger, separate integration work
- [ ] ⏸ **The four new reason codes — resolved as `ContentRuleViolated` namespacing, not requested as
      new codes, but not yet wired into a real validator check.** Following this program's own §2b.1
      precedent (used in every prior module rather than growing the closed list), `MissingUnitClass`/
      `MissingDisplayTemplate`/`MissingDisplayKey`/`UnrenderedMagnitude` would namespace as
      `ContentRuleViolated{display.*}` — the mapping is decided, the actual `ItemSeedValidator` check
      enforcing it end-to-end against real container/pool content is not yet built (would need the
      same instance pipeline the Card level needs)
- [ ] ⏸ **`patronView.ts`'s own call site — not updated.** `FormatPerMille` is the shared conversion
      module 20/the web layer are meant to call instead of owning a second copy; the TypeScript side
      of that wiring is explicitly module 20 `item-surfaces`' work, out of this module's (Core-only)
      scope

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemDisplayTests` | **30 passed** (new) |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors, unchanged baseline** — the `stalwart` status flip and the generated `en.json` introduce zero new findings |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5891 passed / 9 failed** — spread across `ClassSystem.*`/`Atoms.*`/`ActorHub.*`/`Demons.*`/`Actions.*`, a new batch from the concurrent stream's own in-progress `match.modify`/wave-control work (confirmed via `git status` showing `BattleModels.cs`/`WaveCatalog.cs` mid-edit, and a transient `WaveCatalog.cs` compile break that resolved itself between two consecutive build attempts); **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **684 passed / 3 failed** — identical baseline to every prior module's snapshot |
| `dotnet test tests\FusionRpg.Guard.Tests` | **169 / 171** — 2 `ClassSystemBaselineRegenTests` failures, same concurrent stream, unrelated |
| `dotnet build src\FusionRpg.Server\FusionRpg.Server.csproj` | succeeds — the new display-template boot seeding does not break boot |

**Files:** `data/seed/items/display-templates/derived.json` (EDIT — `atom.stalwart` `pending`→`live`);
`content/display/en.json` (new, generated); `src/FusionRpg.Core/Items/Display/{ChannelUnits.cs,
DisplayModel.cs, DisplayTemplates.cs, ItemDisplayRenderer.cs, RarityPalette.cs}` (new);
`src/FusionRpg.Data/Sqlite/RpgStore.ItemDisplay.cs` (new); `src/FusionRpg.Server/Program.cs` (EDIT —
seeds `item_display_template` at boot); `tests/FusionRpg.Core.Tests/Items/ItemDisplayTests.cs` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemDisplayTests`

> ### ⚠ CHECKPOINT 2 — partially met, named honestly
> The dominance lint runs in its **real** form (`power_ceiling` seeded), so D11 stops degrading
> silently — **met** (module 6). Every role has a build where each frame's base is correct — **met**
> (module 6). `ContentValidation.cs:71` fixed, so a green Budget means something — **still owed to
> module 9/6's own consumers**, per module 9's todo entry; not flipped by this pass. An item card
> renders a real item — **the projection exists and is tested at the Line level against the real 98
> row corpus; it does not yet render a full real instance end to end** (Card/Compare levels, the DAL
> read path, and the reason-code validator are the named remaining pieces above). Recorded as
> partially met rather than claimed complete.

---

## Phase 3 — generation and drops

### ✅ P3.1 — Module 11 `drop-volume` — BUILT AND VERIFIED 2026-09-04 (smart loot, the seedsmith band→row generator, and the four unavailable entry kinds explicitly deferred with owners named)

- [x] ⭐ **D38: the kill path is a flat 5 %, and it is TWO rolls.** `DropChanceOnKillMilli = 50` in
      `data/tuning/item-drop-volume.v1.json` (never hardcoded) answers *does anything drop*;
      `RarityDraw.Draw` over `rarity_budget.drop_weight_default` — module 7's re-derived ten-rung
      table, a **different** table — answers *which rung*. ⛔ The disambiguation survives into code
      and into a test that runs both rolls 200,000 times:
      `A_five_percent_kill_rate_is_not_a_five_percent_chance_at_an_almanac` measures ~5.0 % kills and
      ~0.7 % `almanac` **of the drops that happen**, and asserts `almanacs < kills / 20`.
      `The_kill_path_is_a_flat_five_percent_and_does_not_scale_with_theta` asserts a beginner (Θ=0)
      and a veteran (Θ=2000) see the **byte-identical** hit count. The `scalesWithTheta` flag exists,
      ships `false`, and is the one-line change the spec says it should be — not a redesign
- [x] **Volume is LINEAR in `Θ`, read through `IPowerIndexProvider`, with no private curve.**
      `DropVolume.VolumeScaleMilli(Θ, t) = max(FloorMilli, Base + Slope × (Θ − ΘPin))`, `long`
      throughout, widened before multiplying, divided by 1000 exactly once and **last** (in
      `RollsEffective`, never in the scale). `Volume_uses_no_private_curve` calls through the shipped
      `StubPowerIndexProvider` *and* greps `Items/Drops/` for `PowerLadder.` / `Math.Pow` (comments
      stripped — see the note below). `Overflow_throws_it_never_wraps` proves a huge slope throws
      rather than wrapping
- [x] ⛔ **No cap of any kind, proven by a guard rather than by review.**
      `No_drop_cap_exists_anywhere_in_the_pipeline` scans every file under `Items/Drops/` for
      `PerDay`/`PerRun`/`DailyCap`/`MaxDropsPer`/`DropCap`/`pvz_loot_budget`;
      `There_is_no_upper_bound_on_volume` shows Θ = 2,000,000 still adds exactly one slope per Θ, and
      `More_theta_yields_more_items_and_nothing_saturates` drives 400 real `warpath-20h` events at
      Θ = 20 / 200 / 2000 and asserts the far-veteran yield is >10× the pin's. `DropVolumeTuning.Validate`
      deliberately **accepts** an enormous slope — a "sanity cap" there would be the cap D26 forbids
      wearing a different hat — and that non-refusal is itself asserted
- [x] **`FloorMilli` is structural and says so** in the file a balance pass edits
      (`floorNote: "STRUCTURAL, not a progression ceiling (AGENTS.md) … It is a LOWER bound"`). With
      the shipped slope it never binds at Θ ≥ 0, which is the point: it is a guard, not a live clamp,
      and `The_floor_is_structural_and_documented` asserts both halves
- [x] ⭐ **Correction 1 reproduces EXACTLY, all eight rows, at Θ = 20.** `data/seed/loot/tables.v1.json`
      (10 tables, 2 shared + 8 calibrated) + `DropTableDraw.ExpectedEquipmentPerMille`, exact integer
      arithmetic, asserted at both the Core layer and after a DAL round trip. Nothing in the module is
      expressed per day — I12's *"20–30 equipment items per day"* is restated per content event at the
      pin, and the behavioural target it was derived from (*look at 100 %, keep 20–35 %*) is recorded
      verbatim in the corpus `_meta`
- [x] **Step 5a's stream shifts no other stream, byte-identically.**
      `The_volume_stream_shifts_no_other_stream` samples `item.ilvl`, `item.base.0`, `item.rarity.0`,
      `item.rolls.0` and the group draw, drains the new `item.volume.{table}.{group}` stream 64 times,
      and re-samples — identical. The remainder is an unbiased integer Bernoulli
      (`AtomRandom.NextPerMille`); no float touches a magnitude anywhere in the module
- [x] **The full twelve steps plus 5a**, in `LootPipeline.Resolve`, driven end to end over the REAL
      corpora in tests (10 loot tables, the 10 seeded rarity rungs joined to module 7's
      `item-rarity.v1.json` weights, the 560-row base-type corpus): server-derived correlation id
      (`LootRequest` has **no** correlation field — asserted by reflection), idempotency gate,
      sealed `loot_seed`, content-only item level, table + ilvl-band check, volume scale, group draws,
      base type, rarity + pity, envelope, roll-seed derivation for step 9, sockets, and the manifest
      step 11 persists
- [x] **`affix_channel` authored on every equipment entry and threaded to step 9.** Declared on the
      **drop-table entry**, never on the affix ("the channel is a call-site fact"). Two shared slates —
      `drop.shared.hybrid-core-any` (`drop`) and `-boss` (`boss`) — so a boss table is an authoring
      fact, not a runtime heuristic. `Affix_channel_is_authored_and_threaded_to_step_9` proves the
      channel reaches the grant the injected minter receives, **before X4 exists**; it survives the
      SQL round trip too. ⛔ Inert until X4 lands — a **wiring gap**, not a wall
- [x] **Correction 5: pity keys on rung ids, thresholds RE-SOLVED against module 7's seeded weights.**
      `item_loot_pity(items_since_heirloom, items_since_sunwoven)`; `r4`/`r6` appear nowhere in code or
      schema (asserted both places). Measured from the shipped table: `heirloom`+ = **5,900/100k**,
      `sunwoven`+ = **1,800/100k**. Re-solved to hold I12's *behavioural* property rather than its
      numbers — hard floor **43** (drought 0.941⁴³ = **7.32 %** vs I12's 0.90²⁵ = 7.18 %), ramp start
      **83** (22.14 % vs I12's 22.15 %), hard ceiling **221** (1.81 % vs 1.79 %).
      `Pity_fires_where_the_drought_is_real` asserts the **drought probability**, never the threshold,
      so a reweight moves the number and not the test. `Pity_cannot_be_banked_in_trivial_content`
      shows a forced `heirloom` at content level 1 collapses to a `[2,2]` envelope — the level axis
      already closed the exploit
- [x] **`almanac` has a named deterministic source, and it is a corpus test.**
      `data/seed/containers/first-clear-grants.json` ships `item.first-clear-almanac-seed`
      (`rarity: almanac`, `prefixRolls`/`suffixRolls` = 0, no pool), hung off `web-wave:rift-tyrant`'s
      `first_clear_grant`. It lives in `containers/` — a `SeedScanner.OwnedFolders` entry — so it
      imports through the **standard** `AtomSeedFile` → `RpgStore.ImportContent` path rather than a
      second hand-written writer (module 7's own lesson). Verified live:
      `dotnet run --project tools/AtomImporter -- --check --validate` now reports **7 containers**
      and exits clean. ⚠ It is also the **first shipped container to name a rarity at all**
      (`data/seed/rarity/README.md` recorded that none did), so it is the first live exercise of the
      `effect_container.rarity` FK module 7 wired. ⚠ *Which* content id carries it is the owner's —
      `rift-tyrant` is a starting choice, and promotion (module 15) is the other real source, so the
      grant can be dropped without leaving §3.8 unsatisfied
- [x] **`item_generation` has no `socket_count` column**, asserted by `PRAGMA table_info`. Step 10
      resolves to 0 sockets and still derives + advances `DeriveStream(roll_seed, "item.socket")`, so
      landing module 16's real count moves no other draw
- [x] **No new member of the closed 33-code list.** I12 asked for eight new codes
      (`UnknownDropTable`, `UnknownBaseTypeSet`, `UnknownCurrency`, `DropTableDepthExceeded`,
      `DropTableCycle`, `StandaloneRuleViolation`, `RarityUnsatisfiable`, `LootReplayMismatch`); this
      module mints **none**. `AtomRejectionReason` still has exactly 35 names (33 + `None` +
      `ContentRuleViolated`), asserted, and every rule this module raises is a namespaced
      `ContentRuleViolated{drop.*}` / `{rarity.*}` under a registered `drop` namespace. Shipped codes
      are reused where the semantics already match (`BadParamValue`, `UnsatisfiablePool`,
      `DuplicateSeq`, `UnknownContainer`)
- [x] **Standalone-first enforced by set containment at import, not in prose** — `source_allow` must
      contain `web`; every PvZ-reachable entry must be web-reachable; a PvZ-reachable **equipment**
      entry carrying a `rarity_weight_shift_json` is refused (rule 4 — boosted earn is legal for
      currency and materials, never for equipment rate or rarity)
- [x] **DAL: the eight tables + step 11's ONE transaction.** `RpgStore.Loot.cs`, wired into
      `RpgStore.Init()`. `Persist_is_one_transaction` forces a mid-persist PK collision and asserts
      **no** log row, **no** pity update and **no** first-clear mark survive — the extra hazard here
      being that nothing is spent, so a partial commit mints *free* items rather than losing paid ones.
      `A_retry_mints_nothing` proves `UNIQUE(player_id, correlation_id)` is the second net under the
      pipeline's own gate
- [x] **The `40/day` line filed as a loot-filter requirement against module 20, with its query named.**
      `RpgStore.CountEquipmentMinted(playerId, sinceUtc)` joins `item_generation` to `item_drop_log`;
      it only reads, and nothing in the pipeline consults it — ⛔ a measurement, never a counter that
      could become a gate. The watermarked tail-trim ships day one (`TrimDropLog`): it blanks
      `context_json`/`result_json` past the horizon and **keeps the row**, so inflow stays queryable
      and `item_generation` stays the permanent record
- [x] **Server boot wired** — `Program.cs` parses `item-drop-volume.v1.json` at startup (so a
      self-inconsistent balance edit fails there, not at the first drop) and imports
      `data/seed/loot/tables*.json` via `ImportLootCorpus`, non-fatally

**⛔ Four defects / spec-vs-code divergences found while building, all named rather than silently absorbed:**

1. ⛔ **`spec-drop-volume.md`'s entry-kind list is stale: it says seven, the shipped contract is nine.**
   Its Data-shape row reads `equipment|material|currency|insert|charm|table|nothing`. The authoritative
   seed-side contract is `entry-shapes.md` §9, whose own **"Added 2026-08-23 (wave R2)"** note adds
   `unique` and `consumable` — *"Before them the corpus had 144 uniques, 70 charms and 60 consumables
   that no table could yield"* — and `tools/ItemSeedValidator/Checks/DropTableCheck.cs` already
   enforces all nine. **Verified facts win:** `DropEntryKind` ships nine, and the divergence is
   recorded in the enum's own doc comment.
2. ⛔ **The shipped 40-table seedsmith corpus is not importable, and 315 of its 468 entries are why.**
   Measured, not estimated (`The_seedsmith_drop_table_corpus_uses_kinds_this_build_cannot_yet_resolve`
   asserts every count against the real files): **144 `unique`** (module 17), **70 `charm`** and
   **41 `insert`** (both gated on **X7** — re-verified 2026-09-04 that `ContainerRow.cs`'s
   `ContainerKind` ships six values and none of D27's `gem`/`set`/`charm`/`combo`), **60 `consumable`**
   (module 18; `ssot-generation.md` §5.4 keeps it deliberately absent until the action layer exists).
   Each is refused **by name** with `ContentRuleViolated{drop.entry-kind-unavailable}` naming the
   module that lands it — a build order, not a defect, and never a silent drop. **Not this module's to
   fix**, and cross-referenced into the owning modules' sections below.
3. ⛔ **`spec-drop-volume.md` Correction 1's `warpath-20h` decomposition contradicts shipped code.**
   The spec writes *"`warpath-20h` (4 + boss) — 4 × 0.55 + 1.40 + 0.60"*, i.e. five encounters.
   `ExpeditionResolver.WaveChain("warpath-20h")` (`ExpeditionResolver.cs:202`) is **four waves total** —
   `rift-warband`, `rift-onslaught`, `rift-onslaught`, `rift-tyrant` — three normal battles **and** the
   boss. The ruled **yield of 4.20 is kept exactly**; the composition is re-derived against the shipped
   chain (3 × 0.55 + 1.00 + 0.40 + 2 × 0.575), and the reason is written into the table's own `note`.
   `scout-30m` / `forage-4h` / `hunt-8h` were cross-checked the same way and **do** match (1/2/3 waves).
4. ⛔ **The two halves of the lane disagree on step 10's stream name.** `ssot-generation.md` §4.3's
   stream table says `item.socket.{i}` off the **loot seed**; `spec-drop-volume.md`'s own step-10 row
   and `spec-sockets.md:143` (module 16, the owner of the count rule) both say
   `DeriveStream(roll_seed, "item.socket")`. **The owning module's spelling wins** — using the other
   one would hand module 16 a different stream than it is written against, which is precisely the
   "a step added later is a migration" defect the ordering exists to prevent. Recorded in
   `LootStreams.Sockets`' doc comment so the choice is visible rather than inferred.

**Two decisions this module had to make that the spec does not state, both named:**

- ⭐ **The volume term applies at the TOP LEVEL only; a nested table draws its own authored rolls.**
  The spec says `rollsEffective(group, Θ)` for *a group* and is silent on nesting. Compounding Θ once
  per nesting level makes the yield **quadratic in Θ** — exactly the shape D18 exists to refuse — and
  §5.3 property 5 is explicit that *"nesting is for reuse, not for depth"*. Asserted by
  `A_nested_table_draws_its_own_rolls_and_does_not_compound_theta`, which drives 2,000 real events at
  two Θ and checks the observed yield against the exact expected value at each.
- ⏸ **The volume SLOPE is a starting value and it is the owner's.** D38 settled the *kill* path (flat
  50‰, no slope) but names no number for the non-kill Θ term. Shipped `slopeMilli = 25`, derived from
  one statable property rather than invented: *an actor at twice the pin's Θ (40) sees 1.5× the pin's
  volume*, so 500‰ / 20 Θ = 25. The reasoning is in the tuning file's own `slopeNote`, where a balance
  pass will read it.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropVolume\|FullyQualifiedName~LootPipeline"` | **51 passed** (new — `DropVolumeTests` 19, `DropVolumeCorpusTests` 12, `LootPipelineTests` 20) |
| `dotnet test tests\FusionRpg.Data.Tests --filter DropTableStoreTests` | **12 passed** (new) |
| `dotnet run --project tools\AtomImporter -- --check --validate` | **clean, exit 0** — 16 files, 66 atoms, **7 containers** (the first-clear grant now among them), 10 rarity bands; `--check` reports nothing would change |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors — identical to the module-6/8 baseline.** Zero new findings; `data/seed/loot/` is outside the item seed root by design |
| `python scripts\audit-overflow.py` | **0 critical**, 55 findings total, **zero** under `Items/Drops/` |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0**; the 2 `items` M2/M4 rows are module 8's pre-existing `ItemNameComposer.cs:22` / `RoleFamilyTable.cs:27`, **zero** under `Items/Drops/` |
| `.\scripts\guard-dal.ps1` | **OK** — no SQL outside `FusionRpg.Data` |
| `.\scripts\guard-single-writer.ps1` | **OK** |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6035 passed / 6 failed** — all 6 in `ClassSystem.UnitClassContractParityTests` (2) and `Demons.SpeciesExpanderTests`/`SpeciesCatalogDiffTests` (4), the concurrent stream's own in-flight work (`git status` shows hundreds of `data/seed/demons/species/**` files mid-add/delete and `classes` registry churn, none touched by this module); **zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **704 passed / 3 failed** — the established baseline exactly: 2 `DemonSpeciesImportCliTests` (same concurrent stream) + 1 pre-existing `AtomStoreTests.An_unknown_trigger_is_rejected`; **zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` | **178 / 178** — up from 171 at P2.3's snapshot, all green |

⚠ **One transient build break, resolved by retry and worth recording as a pattern:**
`src/FusionRpg.Core/Items/Power/ItemPowerTuning.cs` briefly vanished mid-build (the concurrent stream
rewriting it), failing `ItemPowerReads.cs` with `CS0246` on a file this module never touched. It
reappeared within seconds and the rebuild was clean. Also hit `MSB3027` once on
`FusionRpg.Core.dll` locked by another `testhost` — same cause, same fix.

⚠ **One test-authoring correction made mid-build rather than left to pass on a false premise.** The
grep-shaped guards (`no private curve`, `no drop cap`, `no r4/r6`) initially scanned raw source and so
failed on their own **doc comments** — the comment explaining *why* `items_since_r4` is retired, and
the one citing `Power/PowerLadder.cs` as the curve this module deliberately does not read, are the
opposite of the defect. Added `DropVolumeTests.CodeOnly`, which strips comment lines before the scan,
so the honest explanation is not the thing that fails.

- [ ] ⏸ **Smart loot — deferred with a reason, a trigger and an owner, not omitted.** Step 6 draws base
      types **uniform over the legal set** and the code says why, with a pointer. Two structural
      reasons: (a) its input does not exist — `frameWeight(f) = 250 + 750 × squadShareMilli(f)/1000`
      reads the deployed squad's frame mix, and `frame` exists on no species type today (**X1**
      `frame-classify`, resolved 2026-09-03 and **unbuilt**), so a frame-weighted draw over an
      unclassified roster is a uniform draw with extra code; (b) it is the one bias that can break
      step 6, and step 6 feeds step 9's `affix_channel`, which **X4** weights composition off — landing
      it first means the two get tuned against each other later, from opposite sides.
      **Trigger: X1 built AND X4 landed, whichever is later. Owner: this module, in a follow-up.**
      `item_drop_log.context_json` already **writes** `smartLoot: false` and `squadFrameMix` from the
      first drop, so §4.3's *"a settings change must not alter an already-sealed result"* is true now
      rather than retrofitted. `smart_loot_is_off_and_the_draw_is_uniform_over_legal_base_types` is
      written to **flip**. **Not deferred:** the 250-weight serendipity floor's *reason* is recorded in
      `data/seed/loot/README.md`, because that is the part a later session would drop
- [ ] ⏸ **The seedsmith band→row generator — not built, and it is stage-1b infrastructure, not this
      module's.** `data/seed/items/drop-tables/` authors a `dropBand` where a weight belongs and a
      `qtyCurve` where a count belongs (`seed-contract.md` §1 forbids an author typing a magnitude);
      `bands.v1.json`'s `dropBand.weightTable` (1000/300/90/25/7) is the resolution table, and
      `curves.json` holds the quantity points. Turning those 40 tables into `drop_table_entry` rows is
      a real, separate piece of work. Named so the two corpora are not mistaken for a duplication:
      `data/seed/loot/` is the **generated** shape, `data/seed/items/drop-tables/` the **authored** one
- [ ] ⏸ **No `world-sector` `loot_source`** — `sectorLevel(danger_band)` is owed by the world program
      (**X5**). The `drop.world.sector-clear` table ships (Correction 1 calibrates it at 1.50) with no
      source pointing at it, so the table is the calibration and not a claim that the world map is wired
- [ ] ⏸ **No `pvz-run` `loot_source`, by refusal rather than omission.** `mappedRunLevel` was never
      implemented anywhere and §11 Q8 names two candidates (the player's own level, or a flat session
      level the PvZ side reports) and picks **neither**, so such a source is refused **by name** with
      `ContentRuleViolated{drop.source-kind-undesigned}` at both import and runtime — never defaulted
      to 1. Two tests assert it. The `drop.pvz.run` table exists because Correction 1 calibrates it at
      0.50, with identical odds and no rate or rarity bonus (§4.6 rule 4)
- [ ] ⏸ **Step 9's real `Instantiator.TryInstantiate` call is an injected seam, not yet wired to a
      production caller.** `LootContentView.Mint` takes the mint as a delegate so `LootPipeline` stays
      pure and store-free; the pipeline computes everything the call needs (container-facing base type,
      rung, envelope, derived `roll_seed`, `affix_channel`) and the tests exercise the seam. The
      production wiring belongs with whichever endpoint resolves a loot event — a **wiring gap**, and
      one that also waits on per-base-type item containers, which no module has authored yet
- [ ] ⏸ **`item_drop_log`'s retention horizon is the owner's (I12 §11 Q6).** `TrimDropLog` ships and is
      tested; `log.retentionHorizonDays = 90` is a starting value carrying that note, and nothing calls
      the trim on a schedule yet
- [ ] ⏸ **Whether uniques get pity (§11 Q2) and whether a third `affix_channel` value is wanted** stay
      open-by-design; I12's *"no unique pity, deliberately"* is the standing answer and nothing here
      contradicts it

**Files:** `data/tuning/item-drop-volume.v1.json` (new — Θ pin/base/slope/floor, D38's kill rate,
Correction 5's re-solved pity thresholds, the ilvl jitter, the nesting bound, the retention horizon);
`data/seed/loot/{tables.v1.json, README.md}` (new — 10 tables, 8 loot sources, the whole Correction-1
calibration); `data/seed/containers/first-clear-grants.json` (new — the rung-100 deterministic source,
in an owned seed folder so it imports through the standard path);
`src/FusionRpg.Core/Items/Drops/{LootStreams.cs, DropVolumeTuning.cs, DropVolume.cs, DropTableModel.cs,
DropTableValidator.cs, DropEnvelope.cs, LootPity.cs, LootPipeline.cs, LootCorpus.cs}` (new);
`src/FusionRpg.Data/Sqlite/RpgStore.Loot.cs` (new — the eight tables, `ImportLootCorpus`,
`PersistLoot`, the inflow measurement, the tail trim); `src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT —
`EnsureLootSchemaUnlocked` in `Init`); `src/FusionRpg.Server/Program.cs` (EDIT — parses the tuning at
boot, imports the loot corpus after `store.Init()`);
`tests/FusionRpg.Core.Tests/Items/{DropVolumeTests.cs, DropVolumeCorpusTests.cs, LootPipelineTests.cs}`,
`tests/FusionRpg.Data.Tests/Items/DropTableStoreTests.cs` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropVolume|FullyQualifiedName~LootPipeline"`; `dotnet test tests\FusionRpg.Data.Tests --filter DropTableStoreTests`; `dotnet run --project tools\AtomImporter -- --check --validate`

### ✅ P3.2 — Module 12 `threshold-grants` — BUILT AND VERIFIED 2026-09-04 (D40's module-22 split, X7's container kinds, and module 13's distinctness gate explicitly deferred with owners named)

- [x] **One mechanism, three consumers — and the "no forked copy" claim is a test, not a promise.**
      `ThresholdEvaluator` takes a `ThresholdConsumer<T>`: a bucket **key** (`Func<T, string?>`, never a
      `Func<T, bool>`), a reducer, a `long` weight, a breakpoint table, and a `source`. Sets, charm
      resonances and D3's frame-mix bonus each instantiate it and nothing else.
      `Three_consumers_share_one_evaluator_with_no_forked_copy` builds all three, asserts each is the
      same open generic, and drives each through the same `Grant` call. Grants are cumulative
      (4 pieces holds the 2-piece container too), the reconcile is **total** — `ToBind`/`ToWithdraw`/
      `Unchanged` against exactly the ids bound under that one `source`, never the owner's whole
      binding list — and `Re_evaluation_is_withdraw_and_rebind_never_a_patch` proves a stale row under
      the source is withdrawn even when the wanted set did not change
- [x] ⭐ **D3's predicate is a `Min` over two budget-weighted buckets, and the 230‰ defect is a
      fixture.** `FrameMixPredicate.MinorityMilli` sums `budgetWeightMilli` per frame over the twelve
      hybrid-core roles — **read off `core.v1.json`, never transcribed** — and takes the smaller.
      `long` throughout, `checked` (`The_frame_mix_weight_sum_overflows_by_throwing_never_by_wrapping`),
      no float anywhere. `A_six_six_split_of_the_cheapest_roles_concedes_230_not_400_permille` pins the
      exact arithmetic (`jewel-minor-a` 15 + `jewel-minor-b` 15 + `retinue` 40 + `footing` 50 +
      `infusion` 50 + `girdle` 60), and `A_two_heaviest_role_concession_beats_a_five_lightest_role_concession`
      proves the weighting from the surprising direction: 2 items conceded (280‰ → 940‰) beats 5
      (170‰ → 885‰)
- [x] ⭐ **The recovery curve's SHAPE is enforced at load, not only its ends.** All four structural
      properties are separate refusals with separate rule ids, so a balance pass reads which one it
      broke: `frame-mix-curve-floor-wrong` (f(0) = 800‰), `frame-mix-curve-parity-wrong`
      (f(400) = 1000‰, +200 and no further), `frame-mix-curve-knots-unordered` (a duplicate x is a jump
      discontinuity wearing knots) and — the one that matters —
      **`frame-mix-curve-not-strictly-increasing`**. `A_step_function_knot_list_is_refused_at_load_with_a_reason_code`
      feeds the parser the exact cheat the spec names (everything from 40‰ up already at parity) and
      asserts the refusal; `The_recovery_curve_is_strictly_increasing_over_the_whole_range` walks the
      whole domain. `A_ten_two_body_recovers_strictly_less_than_a_seven_five_body_which_recovers_less_than_parity`
      pins **815 < 885 < 1000** over the three fixture *bodies* the spec names, not over ratios
- [x] **The knot list is the tunable, and the tiers are DERIVED from it.**
      `data/tuning/item-frame-mix.v1.json` ships the spec's own §2g table verbatim
      (0/100/200/300/400 → 800/850/900/950/1000, i.e. `f(m) = 800 + m/2`). `TierBreakpoints()` numbers
      the knots above zero into `set.frame-mix-{ordinal:D2}`, so moving a knot moves its tier and the
      two cannot drift — `Breakpoints_come_from_tuning_not_from_code` proves it by moving one and
      re-deriving. No ladder literal survives in C#
- [x] ⚠ **`minorityMilli > 400` throws, and the bound is derived rather than chosen.**
      `parityMinorityMilli` must be exactly half `budgetTotalMilli` or the tuning is refused
      (`frame-mix-parity-not-half`) — the smaller of two disjoint sums over a total cannot exceed half
      it. `A_minorityMilli_above_400_throws_and_is_never_clamped` asserts the throw and the message; a
      clamp would hide precisely the broken role table the bound exists to catch
- [x] ⭐ **I5 §3.6's clause 5 — two partial sets — claimed and built, with the cap that must not exist
      pinned by reflection.** The counter is per set id, breakpoints are looked up per set, each tier
      carries `source = set:{set_id}`. `The_evaluator_carries_no_max_active_sets_parameter` scans every
      public member of `ThresholdEvaluator` / `SetEvaluator` / `ThresholdConsumer<>` / `FrameMixTuning`
      for `maxActiveSets`-shaped names, so a hard progression ceiling cannot be reintroduced under a
      balance name. `Seven_partial_sets_on_a_pure_frame_are_legal` and
      `Withdrawing_one_partial_set_leaves_the_other_intact` cover both halves
- [x] **Counting is per ROLE, not per item** (ssot-sets §4.5) — proven twice, in the pure evaluator
      (`Counting_is_per_role_not_per_item`: one set ring in `jewel-minor-a` and a copy in
      `jewel-minor-b` counts **1**) and again through the SQL recount
      (`Two_copies_of_one_member_in_two_roles_count_once_in_SQL_too`)
- [x] ⭐ **D33(a): charm resonance binds at `unique-actor:{specimenId}`**, asserted
      (`Charm_resonance_binds_at_unique_actor_scope`). `ssot-charms` §3.1's reversal from option C to
      option B costs one line, exactly as the scope-parametric build predicted
- [x] ⛔ **`player:` stays refused, in code.** `CharmResonance.RefuseUnsupportedScope` returns
      `ScopeUnsupported` for `player:` and `match:`, and `SetEvaluator.RefuseUnsupportedScope` does the
      same for a set tier (ssot-sets §4.4 — one demon's gear must not become a team buff). **Re-verified
      against the live file, not the spec's line numbers:** `StatApplyScope.Matches` really does end
      `if (key.StartsWith("player:")) return true; // stub → match-wide apply`, `match` really does
      `return true` before it looks at `side`, and `IsMatchWide` really does report `player:` as
      match-wide. `No_charm_atom_is_ever_written_at_player_scope` asserts both refusals by reason code
- [x] **The zero pad, and it is load-bearing at the DAL too.** `ThresholdContainerIds` formats
      `set.{set_id}-{pieces:D2}` / `set.frame-mix-{ordinal:D2}` / `charm.res-{axis}-{count:D2}`, refuses
      a `set_id` ending in `-NN` (it would collide with one of its own tier ids), and
      `The_actor_effect_list_orders_tier_containers_ordinally_so_the_pad_is_load_bearing` binds real
      rows and reads them back through the shipped `ORDER BY … i.container_id ASC` to show `-02`,
      `-04`, `-10` in that order
- [x] **ssot-sets §4.2's three tables at the DAL, driven by the REAL 30-set corpus.**
      `RpgStore.ItemSets.cs` (`item_set` / `item_set_member` / `item_set_tier`, `UNIQUE (set_id, role,
      frame)` and `UNIQUE container_id`), wired into `Init()`, plus `ImportSetCorpus` (one transaction,
      replace-not-accumulate), `ListSets`, `ListBoundContainerIdsBySource` and `CountSetPieces`.
      `data/seed/items/sets/**` round-trips byte-for-byte: **30 sets, 180 members, 86 tiers**
- [x] **Charm classes are a `charm_def` column with real runtime rules** — measured against the live
      corpus: **21 minor / 32 standard / 7 signet**, `ap_cost` 1×21, 2×21, 3×11, 5×7, exactly the
      numbers §3.4 states. Every one of the 7 signets already ships `prefixRolls`/`suffixRolls` = 0,
      `uniqueCarry: true` and an authored **negative** atom (`params.sign: "negative"`), and **no other
      class carries one**. `CharmCorpus.ValidateClassRules` turns those three from observations into
      refusals (`charm-signet-has-rolled-half` / `-not-unique-carry` / `-has-no-drawback`), so module 15
      can refuse on the class rather than on a roll outcome. `Charm_class_is_authored_and_never_derived_from_ap_cost`
      parses a **2-AP signet** to prove the future case stays representable
- [x] **No new reason code.** Every refusal in this module is `ContentRuleViolated{threshold.*}` under a
      namespace registered the way modules 1/7/11 did (`ContentRuleNamespaces.Register("threshold")`).
      The closed 33-code list is untouched
- [x] **Server boot wired** — `Program.cs` parses `item-frame-mix.v1.json` at startup (a flat knot list
      fails there, not at the first hybrid body priced against it) and imports the set corpus after
      `store.Init()`, non-fatally, matching module 11's own rule

**⛔ Four defects / spec-vs-code divergences found while building, all named rather than silently absorbed:**

1. ⛔ **The recount SQL every set depends on names a `source` that no shipped writer produces.**
   `ssot-sets.md` §4.5 step 2 filters `b.source = 'equip'`. Module 4's shipped writer,
   `RpgStore.ApplyEquipProjection` (`RpgStore.Items.cs`), tags every equip binding **`equip-assign`**.
   Against the doc's spelling the recount returns **zero rows for every real wearer** — a set that
   silently never completes, which is the worst shape this class of bug takes. `CountSetPieces`
   defaults to the shipped spelling, keeps the parameter, and `The_distinct_role_recount_is_SQL_and_it_matches_the_pure_evaluator`
   asserts **both** halves: 3 pieces under `equip-assign`, and **empty** under `equip`.
   **Cross-referenced into P1.4 (module 4).**
2. ⛔ **All ten shipped resonance ids are unpadded, and the rename is not this module's.**
   `data/seed/items/charms/resonance.json` writes `charm.res-offense-2`; the grammar this module
   enforces (and the ordinal sort `RpgStore.ListBindings` performs) wants `charm.res-offense-02`.
   `CharmResonance.DeriveTable` emits the canonical padded id and carries the authored spelling beside
   it, so the divergence is **measured** (`All_ten_shipped_resonance_ids_are_unpadded_…`) rather than
   normalised away. The ids are seedsmith-allocated — `tools/ItemSeedValidator/Registries/NamespaceAllocation.cs`
   reads the breakpoints out of `idNamespaces.charms.resonanceNote` — so the rename and that reader move
   together. **Cross-referenced into P3.3 (module 13).** It bites at count 10, which is why nobody has
   hit it at counts 2–3
3. ⚠ **`spec-threshold-grants.md`'s "blocking contradiction in the shipped role vocabulary" is STALE,
   and the honest answer is that module 3 already fixed it.** The spec names three 13-role / 895‰
   sources against D3's twelve. All three now agree: `core.v1.json` is `registryVersion 2` with
   `ward-array` / `head-guard` / `sense` non-eligible and the twelve summing to exactly **800‰**, and
   both Python constants moved with it (`registries.py`'s `HYBRID_FRAME_EXCLUDED_ROLES`,
   `linkage.py`'s `NON_HYBRID_ROLES`, each carrying its own D30 note).
   `The_three_previously_disagreeing_hybrid_role_sources_now_agree` reads all three files and pins it,
   so a regression is a named failure. **Verified facts win** — the frozen-registry dependency the spec
   asks this module to "state, not act on" was already discharged in P1.3
4. ⚠ **One item can be a member of up to three sets, and the shipped corpus already relies on it.**
   Found by a test whose first draft assumed the opposite and failed: the 30 sets declare **154**
   distinct `(role, base type)` member pairs and **25** of them belong to more than one set, one to
   three. That is I5 §3.6's design working — the evaluator counts per set id and never merges — but it
   is a **disclosure requirement** for module 20's tooltip: one equipped piece is simultaneously part
   of three different "3 / 4"s. `One_shipped_item_can_advance_more_than_one_set_and_the_corpus_already_relies_on_it`
   pins the numbers. **Cross-referenced into P5.4 (module 20).**

**Three judgement calls this module had to make that the spec does not state, all named:**

- ⚠ **The strictly-increasing test steps by 2‰, not 1‰, and says why in the test.** The shipped slope
  is +1‰ of budget per 2‰ conceded and the interpolation is exact integer arithmetic, so a single-‰
  step genuinely is flat — that is rounding, not a free prefix. The test asserts **strictly** increasing
  every 2‰ over the whole range **and** monotone non-decreasing at every single ‰, which is the honest
  pair. Asserting strict increase at every ‰ would have forced a float or a fake slope
- ⚠ **D3's own breakpoint table tapers at the top (+70 / +70 / **+60**) and the shipped curve is exactly
  linear.** The spec calls linear "the faithful translation" and pins the §2g table itself; this module
  followed the spec's table, and the test asserts equal steps rather than reproducing 70/140/200. The
  ~10‰ discrepancy at the top row is recorded here rather than papered over
- ⚠ **All 21 minor charms ship 0 pool rolls even though §3.4 allows 0–1.** An observation about the
  current corpus, not a violation, and deliberately **not** enforced: only the signet rules
  (`pool_rolls = 0`, `unique_carry`, a negative atom) are class **invariants**. Naming it so a later
  session does not read "minor charms are unrolled" as a rule

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ThresholdGrant"` | **50 passed** (new — `ThresholdGrantTests` 31, `ThresholdGrantCorpusTests` 19) |
| `dotnet test tests\FusionRpg.Data.Tests --filter ItemSetStore` | **8 passed** (new) |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors — identical to the module-6/8/11 baseline.** Zero new findings |
| `dotnet run --project tools\AtomImporter -- --check --validate` | **clean** — 17 files, 66 atoms, 7 containers, 10 rarity bands; `--check` reports nothing this module changes |
| `python scripts\audit-overflow.py` | **0 critical**, 55 findings — unchanged from P3.1; **zero** under `Items/Thresholds/` |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0**; the 5 `items` rows are modules 8/10's pre-existing `ItemNameComposer.cs:22`, `RoleFamilyTable.cs:27`, `ArmouryQuery.cs:79`, `RarityPalette.cs:43-44`; **zero** under `Items/Thresholds/` |
| `.\scripts\guard-dal.ps1` / `guard-single-writer.ps1` / `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` | **all four OK** |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6106 passed / 6 failed** — the same six names as the pre-build baseline measured at the start of this session (`ClassSystem.UnitClassContractParityTests` ×2, `Demons.SpeciesExpanderTests` ×3, `Demons.SpeciesCatalogDiffTests` ×1), the concurrent stream's own in-flight work; **zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **712 passed / 3 failed / 715 total** — baseline exactly (707 → 715 is this module's 8), the same three names (`AtomStoreTests.An_unknown_trigger_is_rejected`, 2 × `DemonSpeciesImportCliTests`) |
| `dotnet test tests\FusionRpg.Guard.Tests` | **184 / 184**, up from 178 at P3.1 |
| `python -m pytest` (seedsmith, full) | **1505 passed, 1 skipped**, 87 subtests — unaffected (nothing Python-side was touched; the corpus test only *reads* the two constants) |

⚠ **`FusionRpg.Data.Tests` crashes its test host intermittently, and it is pre-existing.** Three of five
full runs this session aborted with *"Test host process crashed"* — **including the baseline run taken
before a line of this module existed** (408 / 707 tests in, no `Items/Thresholds` code on disk). Isolated
rather than assumed: the suite **without** the 8 new tests also completes at 704/3/707, and **with** them
at 712/3/715. Same three failures either way. Not this module's, and worth a note for whoever owns the
flake.

⚠ **One test assumption was wrong and was corrected against real corpus data rather than left to pass on
a false premise** — see defect 4. The first draft of `Two_real_shipped_sets_worn_together_stay_independent`
asserted two sets in progress and got three, because one of the pieces it picked is a member of a third
set. Rewritten to select only exclusively-owned members, and the shared-membership fact promoted to its
own measured test.

- [ ] ⏸ **X7 — `ContainerKind` gaining D27's four values — is not landed, and it is effect-atom's ask,
      not this module's edit.** Re-verified 2026-09-04: `ContainerRow.cs` ships six values
      (`Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff`), `PrefixOf` has six arms, and
      `ContainerValidator`'s id regex mirrors the enum. So nothing this module grants has a legal
      container home yet: the evaluator resolves the wanted **ids**, the DAL stores the **breakpoint
      table**, and binding them as real container rows waits on X7. **A wiring gap, not a wall** — the
      grammar row in `definitions.md` §1 is the SSOT the regex mirrors and it wins over any spec.
      Same blocker P3.1 recorded for the 70 `charm` and 41 `insert` drop entries and P3.3 carries
- [ ] ⏸ **Module 22 `charm-carry` (D40) — the pouch is NOT here, by ruling.** The five tables
      (`charm_def`, `charm_pouch`, `charm_run_hold`, `charm_attunement`, `charm_resonance`), the AP
      gate (budget · axis cap 3 · copy cap 2 · `unique_carry` 1 · `level_req`), its five reason codes,
      the run-start snapshot and the `CharmInUse` refusal all belong to module 22, which depends on
      this one. `data/tuning/charm-attunement.v1.json` is **its** file and is deliberately not created
      here. What this module keeps is what D40 says it keeps: the evaluator, plus the `charm_def`
      **class rules** the evaluator's own corpus reader needs
- [ ] ⏸ **The `(capability, threshold-family multiset)` median ≤ 2 gate is module 13's, and it is
      already passing on today's corpus.** It is `Distribution/CellOccupancy` in
      `spec-set-charm-gen.md` §, a **generation distinctness** gate over the generated population —
      this module generates nothing. Measured on the 30 shipped sets as a data point, not as a gate:
      **28 cells, median 1, max 2, 26 of 28 singletons.** The gate belongs where the generator is
- [ ] ⏸ **Module 16 (`sockets`) should reuse `ThresholdEvaluator` rather than write a second one.**
      Same shape — count inserts in one item, grant at breakpoints — at the **host item's** scope
      rather than the actor's. Deliberately not folded in: merging them would make the scope a
      parameter of a thing whose whole identity is its scope. `ThresholdConsumer<T>` is generic in the
      held-thing type precisely so module 16 can instantiate it over an insert
- [ ] ⏸ **Nothing calls the evaluator from a production path yet, and the missing caller is the equip
      transaction.** `ApplyEquipProjection` binds items; the recount → reconcile → bind-tiers steps
      (ssot-sets §4.5 steps 2–6) need a caller that owns the whole transaction, and it cannot bind
      anything until X7 lands. Both halves of the seam ship and are tested — `CountSetPieces` and
      `ListBoundContainerIdsBySource` on one side, `ThresholdEvaluator.Evaluate` on the other. **A
      wiring gap with a named trigger (X7), not a design gap**
- [ ] ⏸ **D33(b) — the missing atom-level apply scope — stays filed against `buff-debuff-scope` and
      blocks nothing here.** `ScopeCompatibility` keys on `(AtomKindId, WhereScope, WhoKind, ScopeHost,
      Channel)` and throws on an unlisted combination; `StatApplyScope` is a string grammar with no
      atom field at all, and `WhoKind` (`Target · Type · UniqueDemon · Relation`) cannot express the
      concept either. ⚠ Worth stating alongside it: **`unique-actor:` is not in `StatApplyScope`'s
      grammar either** — it falls through to `return false`. That is correct and not a defect: a
      `unique-actor:` binding is *durable* storage, re-keyed to `entity:{ptr}` by
      `UniqueOwnerBinder.ToEntityKey` at deploy, which is module 5's shipped path. Named so nobody
      later reads "unique-actor: applies directly in the stat layer" into it

**Files:** `data/tuning/item-frame-mix.v1.json` (new — the recovery curve as piecewise-linear knots,
the derived hybrid-core bound, the tier id/source/priority);
`src/FusionRpg.Core/Items/Thresholds/{ThresholdEvaluator.cs, ThresholdContainerIds.cs,
FrameMixTuning.cs, FrameMixPredicate.cs, SetCorpus.cs, SetEvaluator.cs, CharmCorpus.cs,
CharmResonance.cs}` (new); `src/FusionRpg.Data/Sqlite/RpgStore.ItemSets.cs` (new — ssot-sets §4.2's
three tables, `ImportSetCorpus`, `ListSets`, `ListBoundContainerIdsBySource`, `CountSetPieces`);
`src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT — `EnsureItemSetSchemaUnlocked` in `Init`);
`src/FusionRpg.Server/Program.cs` (EDIT — parses `item-frame-mix.v1.json` at boot, imports the set
corpus after `store.Init()`); `tests/FusionRpg.Core.Tests/Items/{ThresholdGrantTests.cs,
ThresholdGrantCorpusTests.cs}`, `tests/FusionRpg.Data.Tests/Items/ItemSetStoreTests.cs` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ThresholdGrant"`; `dotnet test tests\FusionRpg.Data.Tests --filter ItemSetStore`; `.\scripts\guard-dal.ps1`

### P3.3 — Module 13 `set-charm-gen` (model calls)

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped 40-table
seedsmith drop-table corpus (`data/seed/items/drop-tables/`) already references **70 `charm` entries**
that no build can resolve to a payload — `ContainerRow.cs`'s `ContainerKind` ships six values and none
of D27's four (`gem`/`set`/`charm`/`combo`), so **X7 has not landed**. Module 11's importer refuses each
by name — `ContentRuleViolated{drop.entry-kind-unavailable}` — rather than dropping it silently, and
names this module plus X7 as what unblocks it. **Not a defect in module 11 or in the corpus**: the
entries were authored deliberately in wave R2 to close the "144 uniques, 70 charms and 60 consumables
that no table could yield" gap `entry-shapes.md` §9 records. Filed here so this module knows those 70
references are already waiting on it.

- [ ] 36 build set families + 1 set and 1 charm per species, consuming the refreshed theme registry
- [ ] **Capped to the twelve hybrid-core roles before generation**, not validated after
- [ ] Ids key on `speciesId` (all 84 verified kebab-legal; re-verify at 386). ⚠ `naming.v1.json`'s
      `set.{themeId}-{seq:03}` over a demon `themeKey` yields `set.demon.allpeater-001` — **two dots,
      ungrammatical**
- [ ] The 36 **build** sets belong to no species — they need a third `build.*` theme population
- [ ] ⭐ **D15: a set has no rarity.** Rarity is the quality of a set's *member pieces*; a set completes
      from pieces of **any rung**. This makes the rarity key vacuous, and module 7's **SC7** then makes a
      registered key with no shipped consumer **reject at load** — so the key must be dropped, not left
      declared. ⚠ D21's *"base rarity: high"* row is struck by the same ruling
- [ ] **D17 is a position, not an oversight: keep the dead tail.** ~904 species sets and ~904 charms
      against roughly five deployed actors. Most will never be seen by any given player. **Do not
      "optimise" the roster down** — D12's roster-scale generation is the point
- [ ] Registers its channels with L0 on load

> ### ✅ CHECKPOINT 3
> A drop table produces an item at a level; its rarity distribution matches the published bands; a set
> bonus fires at its breakpoint at `unique-actor:` scope. **No atom is written at `player:` scope.**

---

## Phase 4 — economy and depth

### P4.1 — Module 14 `salvage-craft`

- [ ] I9 — materials, salvage, the cost vocabulary. The first sink and the cheapest
- [ ] ⛔ **Re-key the price table from 0–9 to `rarity.ordinal` 10…100** — it is **10× off**, and
      modules 15 and 16 cite it verbatim
- [ ] Price `socket.imbue` — band-linear, like `bore` (I9 §7.4 has nine operations and no row for it,
      so the reference table goes to **ten**). ⚠ `socket-imbue` is a new `op_kind` and it is
      **module 15's** to add, not this module's
- [ ] ⭐ **D23: sockets are extended by crafting, at any rarity, and rarity sets the price.** The
      owner's words: *"add socket slot extension in craft feature, use material to increase socket
      slot. any rarity can extend socket slot but higher rarity cost more."* ⚠ D23 is a **pricing**
      ruling, not a new layer — `ssot-sockets` §4.1 already tops up to `base_type.socket_max`; only the
      per-rarity **table** grants zero at the bottom. `bore` is the operation; price it band-linear so
      a `chaff` chassis can reach its ceiling and pays less than an `almanac` for the same hole
- [ ] ⚠ `rpg_demon_materials` rename touches **nine** SQL sites across five files, not the four I9 §6.4
      claims. Still ask-first

### P4.2 — Module 15 `enhance-reroll`

- [ ] I6 + I7 under one mutation contract. **D7: cost, never luck** — steep tier-keyed cost, a success
      chance, mandatory bad-luck protection (`rpg_summon_pity` is the precedent)
- [ ] The `enhance_cap` shrinking soft cap, identical to module 7's text. **Never a hard stop**
- [ ] ⚠ `pool_rolls` **does not exist in code.** `ContainerRow`/`RarityRow` carry
      `PrefixRolls`/`SuffixRolls` and `Instantiator.Draw` runs `DrawBudget` twice — restate I7's
      `T` / `K` algebra **per budget**
- [ ] `CraftingHorizonReport` ships. ⚠ **N ≈ 0.19 realms** at v1 depth is a recorded constraint —
      **do not size risk bands or the pity threshold as a progression choice**

### P4.3 — Module 16 `sockets`

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** Two things filed from
there: (1) the shipped seedsmith drop-table corpus already references **41 `insert` entries** that
cannot resolve until X7 lands the `gem` container kind and this module lands the count rule — module
11's importer refuses each by name with `ContentRuleViolated{drop.entry-kind-unavailable}`, naming this
module. (2) ⚠ **The lane disagrees with itself on the socket stream's name.** `ssot-generation.md`
§4.3's stream table says `item.socket.{i}` derived from the **loot seed**; `spec-sockets.md:143` (this
module) and `spec-drop-volume.md`'s own step-10 row both say `DeriveStream(roll_seed, "item.socket")`.
Module 11 shipped **this module's** spelling — using the other would hand this module a different
stream than it is written against — and recorded the divergence in `LootStreams.Sockets`' doc comment.
Step 10 already **derives and advances** that stream while resolving to 0 sockets, so landing the real
count here moves no other draw.

- [ ] I4 — inserts as instance bindings on the same owner; the combination evaluator (25 resonances +
      Strains/Splices); D22's affinity **bonus**; D21's set-piece exclusivity validator
- [ ] ⭐ **D27 renames every combination container id**: `gem.combo-pure-fire-3` → **`combo.pure-fire-3`**
      (`definitions.md` §1 forces the prefix to match the kind)
- [ ] Validate per-entry `socketMax` against module 6's `socketCeiling(role)`; the **740-row migration**
      in four named steps
- [ ] ⛔ Drop the *"fixed per role"* invariant and its test — the corpus varies within a role
      (`armament-primary` = `{0:18, 1:26, 2:4}`) 740 times
- [ ] ✅ **D41: recipes are UNORDERED** — a multiset match, the shape module 13's gate already uses.
      The 102 combinations stay 102; module 20's `distance` counts *missing kinds*, never positions.
      ⛔ `bind_ordinal` is for **stable display order only** — a matcher that reads it is a bug.
      Test: the same inserts in any arrangement resolve to the same combination

### P4.4 — Module 21 `strain-splice-gen` (model calls)

- [ ] 102 combinations — 36 Strains (12 aptitudes × 3 archetypes) + 66 Splices (C(12,2))
- [ ] Retire the existing element-keyed `socket-word` corpus
- [ ] **D20 fixes the Splice/Strain ingredient count at 4**, matching `socket_max`'s cap. It was
      unstated before and it decides how much of the body a `chaff` chassis can capture — do not treat
      it as a free tunable
- [ ] ⚠ Inert until `socketMax` can reach 4 — **only `armament-primary` and `core-guard` do**, which is
      why the real per-actor Splice ceiling is **2**, not twelve. Ship the tunable at 3 as a
      non-binding backstop

> ### ✅ CHECKPOINT 4
> salvage → craft → enhance → socket is a closed loop on one item. `CraftingHorizonReport` prints.

---

## Phase 5 — content breadth and the player surface

### P5.1 — Module 17 `uniques`

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped seedsmith
drop-table corpus already carries **144 `unique` entries** — by far the largest block of the 315
currently-unresolvable rows — because wave R2 added the `unique` entry kind precisely so the 144
authored uniques would stop being *"referentially perfect and unobtainable"* (`entry-shapes.md` §9).
Module 11's importer refuses each by name with `ContentRuleViolated{drop.entry-kind-unavailable}`,
naming this module. ⚠ Also note `entry-shapes.md` §9's band→channel table (`acquisition = 'drop'` at
ordinal ≥ 90 is `UniqueUnreachable`, so band 90 never appears in d1) — module 11 does not enforce that
rule, and this module owns it.

- [ ] G1 — hand-authored items that break generator rules and no machine rules
- [ ] ✅ **Relics become uniques** (confirmed 2026-09-04). Four shipped relics riding
      `rpg_unique_equipment`, served at `/api/relics`, rendered by `RelicsLayer.tsx`. ⚠ The row
      migration is **module 4's**, and `RpgStore.UniqueActors.cs:606,645,654` read and write that table
      today — **never retire the stub before relics have their home**
- [ ] ⭐ **D39: add `Override` to `stat.modify`'s ops** — damage-type conversion (*"your fire damage
      becomes ice"*). ⛔ **This overrides the standing rule** *do not add the kind before the consumer*
      (the `status.expose.*` / `stat.derived` mistake, twice). **So the consumer ships with it**: the
      ask to effect-atom is *"add `Override`, and here is the damage applier that reads it"* — never
      the op alone. An `Override` that binds to nothing is the third instance of the same defect

### P5.2 — Module 18 `consumables`

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped seedsmith
drop-table corpus carries **60 `consumable` entries**, added in wave R2 (`entry-shapes.md` §9), while
`ssot-generation.md` §5.4 still says consumables are *deliberately absent* from `entry_kind` — *"adding
it now would ship a degenerate action mechanism that the action program then has to absorb"*. Both are
true at once: the seed vocabulary grew, the runtime arm did not. Module 11 refuses each entry by name
with `ContentRuleViolated{drop.entry-kind-unavailable}`, naming this module, rather than picking a
side. Landing the use path here is what makes those 60 entries drawable.

- [ ] G2 — the **use path** degenerates, never the effect. Names `OnActivate`
- [ ] ⭐ **D37: there is no global carry limit `N`.** The equipped **`girdle`** is the limit — role 7 of
      fifteen, budget 60‰, already shipped, so no sixteenth role. §10.1's *"`N`, proposed 2"* is
      withdrawn. **With no belt equipped the count is 0**, not a default
- [ ] Module 20 renders the belt as its own strip, not a row of the armoury list
- [ ] ⛔ Needs D27's fifth `container_kind` (`consumable`), which D27 did not mint — X7

### P5.3 — Module 19 `granted-actions`

- [ ] G4 — the `action_id` seam. Blocked on **X3**

### P5.4 — Module 20 `item-surfaces` ⭐

- [ ] Armoury list + filter, the equip screen, item-card render, comparison, socket preview, the
      **combination compendium** (D20 makes it a requirement at 127 combos)
- [ ] ⚠ **D3 has no player surface** — `frame-mix` appears in modules 3, 6 and 12 and in none of the
      six surfaces. Add it or record the omission

### P5.5 — Module 22 `charm-carry` (D40, split out of 12)

- [ ] The charm pouch: five tables, the carry gate, five reason codes, the run-lifecycle hook
- [ ] A pouch edit mid-run refuses `CharmInUse` — an expedition is sealed at dispatch by recorded seed;
      it is never silently held
- [ ] ⚠ **Specced inside `spec-threshold-grants.md` today** — give it its own file when scheduled

**Depends on:** module 12. **Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter CharmCarry`

> ### ✅ CHECKPOINT 5
> A player can see, compare, equip, socket and craft an item in the web control room without reading a
> database.

---

## The scope boundary — D26, with its reason

⛔ **The item system is purely generate → drop → apply.** The owner's reason, which the fidelity audit
found had been dropped from every spec that carried the rule:

> *"we need balance item, not balance the whole game … if user have stronger gear, so they can take
> advance to higher world realm with stronger enemy and can get stronger gear too — that is correct
> design and item system cannot handle it, that is world map need to handle, battle engine need to
> handle, event generator need to handle. Your design principle learned from trash mobile and live
> service game — I hate those kind of game, the developers is lazy and limit player play them game
> because they don't have enough content."*

**So no task in this list may add:** a drop-volume ceiling, a faucet/sink balance target, an
actor-count calibration, a daily/weekly cap, or any pacing lever. **Endless grind is the SSOT.** Item
comparison — item vs item — is in scope; player-progress rationing is not.

---

## Test baseline before the item build — measured 2026-09-04

**Run before a single item line was written, so item breakage stays distinguishable from inherited
breakage.** ⛔ **All 16 red tests belong to other streams and are theirs to fix** — the demon/seedsmith
and world-stage programs are both actively building in this tree. **Do not fix them from the item
program**, and do not read them as an item regression.

| Suite | Baseline |
|---|---|
| `FusionRpg.Guard.Tests` | ✅ **162 / 162** |
| `tools/seedsmith` (pytest) | ✅ **1489 passed**, 68 subtests |
| `FusionRpg.Core.Tests` | ⛔ **14 failed**, 5315 passed |
| `FusionRpg.Data.Tests` | ⛔ **2 failed**, 637 passed |

**Cause 1 — 14 tests: the species corpus was regenerated under a new id scheme (demon/seedsmith).**
Uncommitted under `data/seed/demons/species/`: **186 deletions, 289 additions, 77 modifications**.
Tests read hard-coded anchors (`sunflower.json`, `peashooter.json`) that no longer exist.
✅ **Verified not data loss** — sunflower's content survives as `solar-pulse-legume.json`; the generator
moved to descriptive ids. Affected: `SpeciesExpanderTests` (7), `SpeciesCatalogDiffTests` (5),
`DemonSpeciesImportCliTests` (2).

**Cause 2 — 2 tests: `loamUnits` is two-thirds built (world-stage).** `UnitClassContractParityTests`
exists to forbid the TS union and the C# enum drifting apart, and it caught the C# half never landing:

| Artifact | State |
|---|---|
| `web/fusion-rpg-web/src/contract/types.ts` | ✅ modified, has `loamUnits` |
| `docs/design/spec-magnitude-and-units.md` | ✅ modified, says thirteen |
| `src/FusionRpg.Core/Stats/Derived/StatClass.cs` | ⛔ **unmodified — no `loamUnits`** |

⚠ `decisions.md:108` records W37/W38 as *"Built same day"*. **The guard is working; the build is not
finished.** Noted for that stream, not acted on here.

⭐ **What this means for the item build.** The two suites the item program writes into are
`FusionRpg.Core.Tests` and `FusionRpg.Data.Tests` — both carry inherited red. **So "green" is not the
bar; the bar is 14 and 2, unchanged.** Re-measure at each checkpoint and compare against these numbers,
not against zero. ✅ `Guard` and `seedsmith` are clean, so those two *are* zero-tolerance.

## Confirmed as a block 2026-09-04 (*"all B"*) — scheduled above

| Question | Ruled | Lands in |
|---|---|---|
| Relics | **become uniques** | P5.1 (+ module 4's row migration) |
| The 36 build sets' theme keys | **third append-only `build.` namespace** | P3.3 |
| The 20 `standard` orphan entries | **retire, don't delete** — `enabled: false`, id retired forever | P1.3 |
| 25 legacy socket-words | **regenerate**, not retain alongside the 102 | P4.4 |
| D22's affinity bonus | keys on **each ingredient gem's own element** — no 12→6 mapping invented | P4.3 |
| `rpg_demon_materials` → `rpg_materials` | **proceeds** — ⚠ nine SQL sites across five files | P4.1 |

## Carried, not scheduled

- **D25** — PoE-style links: out of scope, recorded in `item-map.md` §6's exclusion table
- **D33(b)** — the missing atom-level apply scope, filed against `buff-debuff-scope`. Blocks nothing here
- **D8** — a 13th atom kind or `aptitude.*` channel family, and a fifth `AllocationScope`
  (effect-atom + class-system)
- **D19's other half** — I11 split in two: the **equip gate stays here** (P1.4's `Admits` /
  `Projectable`), and **per-species aptitude vectors go to the demon program**. Only the gate is
  scheduled above; the vectors are not ours and are not tracked here
- **D4** — *"v1 content reaches ilvl 32"* is retired as an item decision (§2h.5). D29 made the ladder
  unbounded and tier saturating, so it became a request that *content* exist at level 32 — which is
  **X5**. Its substance lands in modules 8 and 11
- **D10** — withdrawn into **D29**. Its *"~3:1 growth ratio"* was unreachable by its own lever
  (`EHP = k_d·P(Θ)`, `DMG = k_o·P(Θ)` ⇒ growth ratio **1.0 for all k**) and was the wrong invariant.
  There is no bespoke `bands.v1.json` to build
- **D16** — the ~110-lane-pick batch ratification is a **meta-ruling with no module by design**. It
  changed the *status* of existing lane text, not the work. ⚠ Its one live consequence: the sampling
  was partly by section number, and at least five picks carry no recommendation to ratify
- **`DominanceBaselineTests`** fails against uncommitted class-system v3 tuning drift — pre-existing,
  unrelated to this program, **do not fix here**
