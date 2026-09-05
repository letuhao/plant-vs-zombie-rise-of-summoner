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
      Owner: **effect-pipeline**. Gates **11, 13, 15, ~~16~~, 17**. ✅ **Re-scoped 2026-09-05: it does
      NOT gate module 16.** P4.3 (`sockets`, BUILT AND VERIFIED) has no dependency on L0 pool
      composition or `affix_channel` weighting — `ResonanceGenerator`/`CombinationEvaluator` grant off
      recipes and inserted atoms, a mechanism X4 never touches, and `spec-sockets.md` never names X4 or
      `affix_channel`. Landing X4 remains effect-pipeline's, still gating **11, 13, 15, 17**
- [ ] **X6** — `E44 power-sweep`; ⚠ **Partially landed 2026-09-05** (`scripts/sweep-power-coefficients.py`,
      `docs/research/power/sweep-power-coefficients-2026-09-05.md`): 5 of the 20 `CoefficientTable.Authored()`
      channels are now fitted in `data/seed/power/coefficients.v1.json` (`hp`/`maxHp`=135, `atk`=222,
      `defense`=500, `status.apply`=333), replacing their flat `CoeffMilli = 1000`. The remaining 15 stay
      flat at 1000, honestly marked `pending-content`/`policy` for lack of a real corpus — and
      `CoefficientTable.Authored()`'s own code fallback is unchanged. §4.2's non-additive D2 correction is
      untouched by this sweep.
      Owner: **effect-atom**. Gates module **9**
- [ ] **D28 / E43** — family tags stamped into `AtomRow.TagsJson`. Owner: **effect-atom**.
      Gates module **8** (every tag-gated rule is inert without it)
- [ ] **`bind_ordinal` on `effect_binding`** — requested by `ssot-sockets` §5.4, **absent** from the
      shipped DDL. Owner: **effect-atom**. ~~Gates module **16**~~ ✅ **Re-scoped 2026-09-05: it did NOT
      gate module 16.** P4.3 shipped with the socket half of the contract built and tested
      (`SocketOperations.BindOrdinalFor(i) = i + 1`, content-derived); the column and the comparer arm
      stay effect-atom's, and the comparer **has no implementation anywhere yet**, so nothing is broken
      today. Landing it later is a wiring change, not a design one
- [x] **X3** — ✅ **D36: nothing to do.** `action-corpus` owns the production caller `ActionSeeder.Generate`
      still lacks — the method itself already shipped under the (closed) `action` program (`ActionSeeder.cs`,
      spec-action-seeding.md A13), and `action-corpus`'s own map explicitly disclaims building it.
      `action-corpus` is under active construction by another owner. We consume that caller when one
      ships. ⛔ **Do not
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
~~**Verify:** registry count equals `ls data/seed/demons/species/{plant,zombie} | wc -l` (**386**
today: 292 + 94).~~

⛔ **Corrected 2026-09-04 while building module 13 (P3.3) — this step is sized against the wrong
denominator, and the Verify line above would have certified a still-broken registry as complete.**
The files under `data/seed/demons/species/{plant,zombie}/` are **family** files, each holding many
species; `_index.json` is a flat `{speciesId: "plant/family.json"}` map and it is the species list.
Measured: **840 species across 503 family files** (both move — the concurrent stream is rewriting the
tree; family-file count alone already moved from 495 on 2026-09-04 to 503 on 2026-09-05, species count
held). So the gap `theme-refresh` closes is **84 of 840 — 772 uncovered**, not 84 of 386.

⛔ **And 16 published themes are ORPHANS** — they name a `speciesId` the anchor tree no longer ships
(`cherrygatling`, `cherrypaperzombie`, `cornpot`, `dancepolzombie`, `dolldiamond`, …). A republish
that only *adds* leaves them behind, so the staleness check has to look both ways.

**Verify:** `python -m pytest tools/seedsmith`; registry count equals
`len(json.load(open('data/seed/demons/species/_index.json')))`, and module 13's
`the_theme_registry_covers_every_shipped_species` / `the_species_count_is_the_index_not_the_file_count`
go from asserting the gap exists to asserting it is closed.

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

> ⚠ **Status re-measured 2026-09-05 during the module-22 whole-file consistency pass — the two halves
> are in DIFFERENT states and the unchecked boxes below were hiding that.** Read off the real registry
> files, not from any section's prose:
>
> | Half | State | Evidence |
> |---|---|---|
> | **`core.v1.json` → v2 (D30)** | ✅ **LANDED**, by module 3 at P1.3 | `registryVersion: 2`; exactly **twelve** roles carry `hybridEligible` and they sum to **800‰**; `ward-array` · `head-guard` · `sense` are `false` and `jewel-minor-b` is `true`. Both Python constants moved with it, and P3.2's `The_three_previously_disagreeing_hybrid_role_sources_now_agree` reads all three files so a regression is a named failure |
> | **`classes.v1.json` → v4 (D35)** | ⛔ **NOT landed** — still `registryVersion: 3`, `frozen: true` | So the 32-family exclusion, the five stopgap slates, the directional-profile field and the stale `frozenNote` are all still open, and every downstream module built against v3 |
> | **The 18 legacy sets** | ⛔ **Open by design** — closes with module 13's generation run | P3.3 refused to close it deterministically: a member role is a model-chosen identity field, and code-side role swapping is the inversion P1 forbids |
>
> Nothing is being marked done here. Recorded so the next reader does not infer from one unchecked
> list that the core bump never happened, or from the checkpoint banner that the classes bump did.

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
      no executor until E12"*) expired when `AtomKindRegistry.cs:534` shipped `Full/Full/None`
- [ ] Refill the **five** stopgap slates from each role's real §2.3 cluster: `ward-array` (2),
      `head-guard` (2), `sense` (2), `footing` (2), `mantle` (3). ⚠ **Five, not four** — the registry's
      own `_meta.designNotes` misses `footing`
- [ ] Add the **directional-profile field** the entry shape lacks (`seed-contract.md:324-343`,
      `adapters/items/kinds.py:49-51`)
- [ ] Fix the stale `frozenNote` (reads *"FROZEN v2"* at `registryVersion 3`)
- [ ] **Re-author the 18 legacy sets** under the twelve-role cap — the same generation run module 13
      performs for the ~904, so no extra pass

      ⛔ **Re-measured 2026-09-04 at P3.3, and still open.** Counted directly off
      `data/seed/items/sets/**` rather than from any document: **18 of 30 sets** name a dropped role —
      **10 use `head-guard`, 11 use `sense`, 3 use both** — and
      `seedsmith check --adapter items --metric Linkage/SetCompletability` reports **30 GAP findings**
      over exactly those 18. ⚠ **It cannot be closed deterministically, and module 13 refused to try.**
      A member role is a **model-chosen** field under P1 ("the model writes identity, deterministic
      code writes magnitude"), so a code-side role swap would be deterministic code writing identity —
      the exact inversion P1 forbids. It closes with the generation run, exactly as D30 priced it.
      **Cross-referenced from P3.3.**

**Acceptance:** ⚠ **corrected 2026-09-04 against a measured result, not a prediction.**
`Linkage/SetCompletability` (which **gates**) is *not* clean against the corrected core — correcting
the core is exactly what makes it report the 18 findings it was blind to before (measured:
`seedsmith check --adapter items --gate` goes from exit 0 to exit 1). **That is D30's accepted cost,
not a failure of this step** — the metric goes clean again only when module 13 regenerates the 18
legacy sets (Phase 3, "no additional pass" per D30). No role carries a stopgap slate; the directional
field exists.
**Verify:** `python -m pytest tools/seedsmith` green (**1497 passed** — code correctness); the gating
metrics carry one **named, ruling-anticipated** red (`SetCompletability`'s 18) until module 13 runs.

> ### ⚠ CHECKPOINT 0 — partially met, named honestly
> Every external dependency accepted, declined or built. **`core.v1.json` bumped to v2 (D30) — met, by
> module 3 at P1.3. `classes.v1.json` NOT bumped to v4 (D35) — still `registryVersion: 3`, `frozen:
> true`, re-measured 2026-09-05 in the P0.5 status table above.** "Both registries bumped in one pass"
> does not hold; that half of the regeneration pass stays open, tracked in the table above, not here.
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
- [x] `effect_binding` FK with `ON DELETE CASCADE`, matching `definitions.md:323`'s promise. ⚠
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

⚠ Two flaky, pre-existing, unrelated tests observed intermittently under parallel execution
(`Battle.Timeline.TimelinePurityGuardTests`, plus this run added `ClassSystem.CombatSimJsonEmitTests`,
also 100% green in isolation) — noted, out of item-program scope. **Superseded same day:**
`CombatSimJsonEmitTests`'s flake was root-caused and fixed by the battle-timeline stream
(`battle-timeline-todo.md` Phase 7/T14 baseline note — a `dotnet run` subprocess without `-c Release
--no-build` raced the parent `dotnet test`'s held Core compiler lock, CS2012; fixed with that one
argument, confirmed live in `CombatSimJsonEmitTests.cs`, and absent from every full-suite run recorded
here since, e.g. P2.4's 2026-09-05 addendum). `TimelinePurityGuardTests` remains open and
unfixed — still recurring as late as that same P2.4 run.

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
`Linkage/SetCompletability` metric (`gates = True`, wired into CI at `ci.yml:231`) report its
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

⚠ **Cross-referenced from P3.2 (module 12).** `ApplyEquipProjection` tags every equip binding
`equip-assign`; `ssot-sets.md` §4.5's recount SQL names `equip`. Examined here and left as shipped —
renaming the tag would be a schema-wide rename this module never made, and module 12's
`CountSetPieces` already defaults to the shipped `equip-assign` spelling and keeps `equip` as a named
parameter, so no wearer's set silently fails to complete. **Cross-referenced back into P3.2.**

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
- [x] ⭐ **The live lawn push, wire-capacity half — BUILT AND VERIFIED 2026-09-05.**
      `AtomPushService.Build` gained a multi-owner overload (`IReadOnlyList<OwnerScope>`) that
      resolves every owner's bindings, compiles the UNION of their atoms once (never once per owner —
      two owners sharing an atom must compile to one catalog entry, proven), and wires each
      `RunnerBinding` to its own binding's `OwnerKey` — the single-owner overload now forwards to it
      and is pinned byte-identical by regression test. `RpgHub.BuildApplyCommand` now enumerates
      every `UniqueActorPhases.ActiveBound` specimen via `ListUniqueActors` and pushes each one
      alongside the player's own scope. 7 new tests, `tests/FusionRpg.Server.Tests/MultiOwnerPushTests.cs`.
      **The earlier claim that "no Injector edit is needed" is CORRECT for this half** — runner-path
      atoms (triggered effects) already carry per-owner identity end-to-end, proven by a real test.
- [ ] ⛔ **A deeper, more precise gap found while building the above — the compiled-grant half is
      NOT scoped, and this predates this session's work.** `AtomCompiler.EmitDefAndGrant`
      (`AtomCompiler.cs`) never stamps an `EffectGrantDto.OwnerKey` at all — every COMPILED (passive,
      non-runner) grant defaults to `EffectOwnerKeys.Match` regardless of which owner's binding
      produced it. Harmless for `Player` scope (a player has no single live entity to scope a passive
      buff to, so match-wide is correct there — measured: this is true of the pre-existing,
      already-shipped Player-only push too, not a regression this pass introduced). **Genuinely wrong
      for a `UniqueActor` specimen** — a specific live entity on the lawn — whose passive
      `stat.derived`/`stat.modify` gear (no trigger, so it compiles rather than routing to the
      runner) would silently apply match-wide instead of to that one specimen.
      `FINDING_a_specimens_compiled_grant_is_not_scoped_to_it_it_reaches_match_scope` pins the
      current (wrong-for-this-case) behavior exactly, with the full mechanism explained inline.
      **The fix exists in shipped code but has never been wired to equipment**: `UniqueOwnerBinder`
      (`src/FusionRpg.Core/Match/UniqueOwnerBinder.cs`) rewrites a durable `instance:{guid}` owner key
      to a live `entity:{ptr}` one — a whole-repo grep confirms `UniqueOwnerBinder.BindGrant` is only
      ever called from `UniqueLoadoutSpec.cs:91` (a specimen's own innate-kit grants, bound at spawn) —
      never from anything equip-runtime related. (The only other `"instance:"` construction in `src/` is
      the legacy non-atom grant blob's placeholder `OwnerKey = "instance:pending"` in
      `UniqueEquipmentCatalog.Grant` / `RelicCatalog.TryGetGrant` — a deploy-time template marker per
      `decision-d1-durable-ownership.md` §5.1, itself never passed through `BindGrant` either, and on the
      legacy grant-blob path non-atom-backed items take, not the `AtomCompiler.EmitDefAndGrant` compiled-
      grant path this finding is about, which constructs no owner key of any kind.) Closing this for real
      needs: (1) this push to stamp
      `"instance:" + specimenId` on a `UniqueActor`-sourced compiled grant (a genuinely new design
      question, since `AtomCompiler.Compile` groups atoms by ICD key across ALL owners for the
      catalog union, so two owners sharing one compiled atom cannot both be given distinct keys on
      the SAME grant — compiling per-owner-group rather than globally-merged is the real shape), and
      (2) an **Injector-side** call to `UniqueOwnerBinder.BindGrant` at the moment a specimen's live
      `ptr` becomes known, mirroring `UniqueLoadoutSpec`'s own pattern. That second half is an
      Injector change `GrantedDerivedAtomReader`'s own doc comment says cannot be verified by any
      test CI runs (net6.0 + BepInEx/Il2Cpp interop, needs a real PVZ Fusion install) — **so the
      earlier claim "no Injector edit is needed" does NOT hold for this half**, corrected here rather
      than left standing. Named as the real next concrete step, not "not attempted."
- [ ] ⏸ **Deferred, explicit — the first geared corner run. Re-investigated 2026-09-05, in depth, and
      the original deferral CONFIRMED CORRECT rather than found to be a shortcut — the exact
      integration point is now identified, not vague.** `tools/DominanceBaseline/Program.cs` (backing
      `DominanceBaselineTests`) builds its 12 corners as bare `AptitudeAllocation`s and resolves them
      via `TerminationGuard.ToActor` → `ActorHubBootstrap.CreateDefault(...).ResolveDerived(ctx)` —
      the `ActorHub`/`IActorStatSubsystem` **primary/derived pipeline**, never `BattleStatComposer`.
      Module 5's own payoff (`EquipAtomSource`/`BattleStatComposer.Equipment`) folds equipment on the
      **other** side — the battle/`ChannelMods` pipeline. These two composers are not merely
      currently-separate by omission: **`class-system-map.md` §2a.0 records an explicit 2026-08-26
      owner decision, "the composers stay separate,"** made for the identical cross-boundary problem
      one layer over (aptitudes reaching battle) — *"the battle-side seam is `ChannelMods`, the way
      `StarPolicy` already feeds progression stats in."* The same section's evidence table separately
      quotes `StarPolicy.cs:6`: *"ChannelMods — never engine changes (battle goldens stay
      byte-identical)."* By the same logic in reverse, making a corner run "geared" correctly means
      teaching the `ActorHub` **primary/derived side** about equipment — a new `IActorStatSubsystem`
      implementation reading a specimen's real `ResolveBindings(OwnerScope.UniqueActor)` atoms, wired
      into `ActorHubBootstrap` the same way `AptitudeSubsystem` already is — **not** grafting
      `BattleStatComposer`'s battle-side fold logic into `TerminationGuard.ToActor`, which would
      quietly cross the exact seam that decision drew. That subsystem does not exist yet, would live
      in and be tested by **class-system's own framework** (`DominanceGuardTests.cs`/
      `DominanceBaselineTests.cs` — self-declared class-system-todo.md checkpoints P5.2/Checkpoint 8,
      extensively tested, actively developed by a concurrent stream this whole session — confirmed live
      right now: `_baseline-dominance.json` sits uncommitted with today's mtime) **plus** `ActorHub`/
      `ActorHubTests.cs`, which the new subsystem would also have to touch but which are NOT
      class-system's own property — they are the shared derived-stat SSOT `actor-hub-ssot.md` governs
      under its own `decisions.md` ADR row, predating class-system by 8 days (shipped 2026-08-19 vs.
      class-system's 2026-08-27) and never one of `class-system-map.md`'s 14 modules; class-system's own
      `aptitude-resolve` module is only ever described as "wired into" it, the identical relationship a
      new equip subsystem would have, and per this repo's own
      rule ("architecture changes that lock behavior need `decisions.md` first") is an ask-first
      cross-program change, not a same-pass wiring fix. **Confirmed, not assumed, by reading the
      actual composer code and the actual decision text** — the deferral stands, now for a reason
      that is checkable rather than a placeholder.
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
Fixed with `[JsonIgnore]`, matching the golden-hash-safety pattern `BattleActorSetup.Index` already
establishes (any newly-serialized member perturbs `ExpeditionResolverTests.Tier_goldens_are_locked`'s
hash unless suppressed) — though the underlying reason differs by field: `Index`'s own comment cites a
redundant computed alias (`Index => Level`, serialized by default like any get-only property), while
`SpecimenId`'s own comment gives the field-specific reason (a specimen id is always null in an
expedition context, since expeditions build actors from wave/species data, never a real owned demon —
not semantically part of what that hash locks).

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

⛔ **2026-09-05 — a real methodology gap, found and closed, not merely noted.** Every module in this
program was verified against `Core.Tests`/`Data.Tests`/`Guard.Tests` only. **`FusionRpg.Server.Tests`
(and five other test projects) were never run once, by any module, across this entire effort** —
discovered only while building the live lawn push above, since that is the first item-program change
to touch `FusionRpg.Server` at all. Running it cold surfaced **21 real, pre-existing failures**, all
traced to this same module's own C3 fix (`AtomRowValidator`'s empty-name check, `AtomRejection.cs`,
P1.1) landing on day one of this session — five Server.Tests fixture files construct an `AtomRow`
with no `Name` and have been silently broken since, unnoticed because nothing ever ran that suite:

| File | Fixed |
|---|---|
| `CompiledPushTests.cs` | 15 tests — the pre-existing `AtomPushService` suite this pass extends |
| `AtomEndToEndTests.cs` | 1 test |
| `WalkingSkeletonTests.cs` | 1 test |
| `BuildSquadEquippedActionsTests.cs` | 3 tests |
| `LoadoutEndpointsTests.cs` | 1 test |

All five: added `Name = ...` to the offending `AtomRow` construction. Re-verified: **`Server.Tests`
158/158 minus 23 remaining failures, all in `World*`/`District*`/`AptitudeChannelModsTests` — zero
item-related, but NOT a "some other stream's mid-edit file" story: `git status --porcelain` shows
every implicated file clean, already committed. 22 throw the identical `SiegeTuningPolicy.Configure(...)
has not run` (`SiegeTuning.cs:424`, reached via `StructurePolicy.CapacityGrowthFor`/`LoamPhases.
EffectiveCapacity`/`DistrictLayout.SideFor`) because this assembly's own `WorldPolicyTestBootstrap.cs`
was never given that call — base-defense's own `tasks/base-defense-todo.md` records wiring
`SiegeTuningPolicy` into "all three test bootstraps (Core/Data/E2E.Tests)" and never Server.Tests, and
its "`LoamPhases.EffectiveCapacity` addition is inert (adds 0)" claim is true of the value, not of
whether the unconfigured call throws first. `AptitudeChannelModsTests`'s one failure is the separate,
already-tracked `data/tuning/battle.v2.json`-missing-`speciesTempo` gap `tasks/species-build-todo.md`
(Checkpoint 4) independently confirms via the identical `git status --porcelain` check, reaching the
opposite conclusion — the file is untouched, not mid-edit. Both are standing, already-committed gaps
in other streams' shipped work, not in-flight edits that resolve on their own once committed.** Zero
item-related failures remain.

⛔ **Two more, smaller, found the same way (running every previously-unrun test project once).**
- `FusionRpg.AtomImporter.Tests`: `SeedScannerTests` asserted `data/seed/rarity/` sweeps to **zero**
  JSON files — true when written, false since P2.1 (module 7) seeded `ladder.v1.json` there on
  purpose, the module's own entire deliverable. Split into two tests: `curves/` (never touched, still
  asserted empty) and a new test pinning that `rarity/ladder.v1.json` **is** swept — a regression that
  silently re-empties the folder now fails loudly either way it goes wrong.
- `tools/ItemSeedValidator`'s own test suite: `RoleFamilyCheck.cs` (P2.3, module 8) read
  `family-overrides.v1.json`/`role-relocation.v1.json` via raw `File.Exists`/`Path.Combine` against
  `ctx.Registries.RegistryDir` — the ONLY check in the whole tool that bypasses the `RegistrySet`
  abstraction every other check and the ENTIRE test suite uses. `RegistrySet.FromNodes` (the in-memory
  test seam) sets `RegistryDir = "(in-memory)"`, so this check silently failed **every scoped test
  that loads even one affix-family entry**, for a reason unrelated to what any of them tested — caught
  by running the suite in full for the first time, not by a targeted look at this file. Fixed
  properly, not papered over: added `FamilyOverrides`/`RoleRelocation` as proper optional
  `JsonObject?` properties on `RegistrySet` (mirroring `Words`/`BuildThemes`/`RetiredIds`'s own
  established pattern exactly), rewired both `Load` and `FromNodes`, and downgraded
  `RoleRelocationArtefactMissing` from a blocking `CorpusError` to a `CorpusWarn` — matching
  `WordPoolAbsent`/`SocketCeilingTableAbsent`'s own precedent for every other optional registry in
  this tool (absence is reported, never blocking). **Verified as a pure plumbing fix, not a behavior
  change**: the real sweep (`dotnet run --project tools/ItemSeedValidator`) still reports exactly
  **170 errors across 120 partitions** before and after, zero `RoleRelocation`/`RoleFamilyOverride`
  findings either way — the real `role-relocation.v1.json` was always internally consistent (module
  8's own "0 orphans" claim), only the test seam was broken.

**Final regression, all previously-unrun projects, this session's first full pass over each:**

| Suite | Result |
|---|---|
| `FusionRpg.Server.Tests` | 135/158 → **158/158 minus the 23 confirmed concurrent-stream failures** (0 item-related) |
| `FusionRpg.AtomImporter.Tests` | **28/28** |
| `FusionRpg.CheatCore.Tests` | **40/40** |
| `FusionRpg.ElementEnumGen.Tests` | **14/14** |
| `FusionRpg.Launcher.Tests` | **162/162** |
| `FusionRpg.ItemSeedValidator.Tests` | **71/71** |
| `dotnet test tests\FusionRpg.Core.Tests` (full, re-verified after all fixes) | **7150 passed / 5 failed** — all `ClassSystem.*` (concurrent stream) |
| `dotnet test tests\FusionRpg.Data.Tests` (full, re-verified) | **842 passed / 0 failed** |

`FusionRpg.Injector.Tests` and `FusionRpg.E2E.Tests` not run — the former needs a real PVZ Fusion
install to build at all (net6.0 + BepInEx/Il2Cpp interop), the latter's scope was not investigated
this pass; named rather than silently skipped.

**Files (this addendum):** `src/FusionRpg.Server/{AtomPushService.cs, RpgHub.cs}` (EDIT — multi-owner
push); `tests/FusionRpg.Server.Tests/MultiOwnerPushTests.cs` (new);
`tests/FusionRpg.Server.Tests/{CompiledPushTests.cs, AtomEndToEndTests.cs, WalkingSkeletonTests.cs,
BuildSquadEquippedActionsTests.cs, LoadoutEndpointsTests.cs}` (EDIT — C3 `Name` fixes);
`tests/FusionRpg.AtomImporter.Tests/SeedScannerTests.cs` (EDIT);
`tools/ItemSeedValidator/Registries/RegistrySet.cs`,
`tools/ItemSeedValidator/Checks/RoleFamilyCheck.cs` (EDIT).

⚠ **Mid-build, another stream's concurrent uncommitted edit (`DerivedTurnChannels.cs`,
`DerivedStatTuning.cs`) briefly broke the shared `FusionRpg.Core.Tests` assembly build** (8 compile
errors, none in item files). Per standing instruction this is expected, sanctioned, concurrent work —
not touched; the build was rechecked after their edit settled and came back clean.

**Files:** `src/FusionRpg.Core/Battle/{EquipAtomSource.cs (new), BattleModels.cs, BattleStatComposer.cs}`;
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (EDIT — `ApplyEquipProjection`, `specimen_id` → `TEXT`);
`tests/FusionRpg.Core.Tests/Battle/EquipRuntimeTests.cs`,
`tests/FusionRpg.Data.Tests/Items/EquipRuntimeStoreTests.cs` (new).

> ### ⭐ CHECKPOINT 1 — THE PAYOFF: **partially met, named precisely (revisited 2026-09-05)**
> ✅ **A real item changes a real number in a real fight** — proven, deterministic, in Core.Tests.
> ✅ The DB half of "on the lawn" (bindings actually created and resolvable at `unique-actor:` scope)
> is proven. ✅ **The wire-capacity half of the live push is now built and tested** — a player's own
> grants and every deployed specimen's RUNNER-path (triggered) atoms travel correctly, each with its
> own owner key, proven against a real `AtomPushService.Build` call, not assumed. ⛔ **A deeper gap
> found while proving that**: the COMPILED (passive) grant path has never carried per-owner identity
> at all, for either scope kind — real, pre-existing, not a regression — and closing it for a specific
> live entity needs an Injector-side change unverifiable without a real game install (see the finding
> above). ⏸ **The geared corner run's deferral is now re-verified rather than merely restated** — its
> exact integration point (a new `IActorStatSubsystem` on class-system's `ActorHub`, governed by
> `class-system-map.md` §2a.0's explicit "composers stay separate" decision) is identified and cited,
> confirming this is a genuine cross-program, ask-first change rather than an item-program shortcut.
> **Phase 2 through 5
> already proceeded and are complete** (per the rest of this file) — nothing in them depended on
> either open item here, matching this checkpoint's own original reasoning; both remain open, tracked,
> and now more precisely scoped than when this checkpoint was first written.

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

⭐ **Addendum 2026-09-04 — `salvage_yield` is no longer awaiting, and this row is now five keys plus
one.** Module 14 (`salvage-craft`, P4.1 below) decided its shape: **one integer per rung, the substrate
quantity a salvage of that rung returns before the affix bonus**, read from
`data/tuning/materials.v1.json`'s `salvageCoefficient.{rung}.substrateBase` and seeded by
`RpgStore.SeedSalvageYield`. It meets `ssot-rarity.md` §9.8's one constraint on this key — *"must not
reuse `shard.{DemonRarity}` ids"* — by naming **no shard id at all**: the shard leg of a salvage is R1's
derived rung−1 rule, not a per-rung budget row. `RarityBudgetKeys` flips it to `HasDecidedShape: true`
and this section's own `RarityBudgetKeysTests` row moved with it (renamed
`The_ready_keys_are_registered`) rather than being loosened — `socket_min`, `socket_max` and
`reroll_cost_mult` stay pinned as unregistered exactly as hard as before. *(⭐ Superseded 2026-09-05:
all three are now decided — see the two addenda below.)* Seeding is deliberately in
`SeedSalvageYield`, **not** folded into `SeedRarityLadder`, so this module's seeding never grows a
dependency on a later module's tuning file.

⭐ **Addendum 2026-09-05 — `reroll_cost_mult` is no longer awaiting, and one authored-but-unread
row was removed from this module's own tuning file.** Module 15 (`enhance-reroll`, P4.2 below) decided
the key's shape: **the per-rung integer is the reroll price's RUNG LEG**,
`1000 + rerollCostRungSlopeMilli × rungIndex` (`chaff` 1000 … `almanac` 2980), read from
`data/tuning/enhancement.v1.json` and seeded by `RpgStore.SeedRerollCostMult`. `ssot-rarity.md` §9.7's
constraint — *"must also scale with **affix count**, not rung alone"* — is met by a second leg that is
deliberately **not** a per-rung row, and `EnhancementTuning.Parse` refuses at load any tuning whose
affix leg does not out-spread the rung leg. `RarityBudgetKeys` flips it to `HasDecidedShape: true` and
this section's `RarityBudgetKeysTests` row moved with it; `socket_min` and `socket_max` stay pinned as
unregistered exactly as hard as before. *(⭐ Superseded 2026-09-05 by module 16 — see the next
addendum.)*

⛔ **And a real defect in this module's output, found and fixed there:** `data/tuning/item-rarity.v1.json`
carried **`enhanceCapAsymptoteK: 8`, which nothing read.** `ItemRarityTuning.Parse` never parses it and
no test touched it, while `spec-enhance-reroll.md` §4a is explicit that *"module 7 owns the column; this
module owns `K`"* — so the live copy is `enhancement.v1.json`'s `asymptoteK` and this one was a second
source of truth a balance pass could edit with no effect. Removed 2026-09-05 and replaced with a note
naming where `K` actually lives. Seeding this module's own `enhance_cap` column is unaffected.

⭐ **Addendum 2026-09-05 — `socket_min` and `socket_max` are no longer awaiting, and the closed
key list is now fully decided.** Module 16 (`sockets`, P4.3 below) decided the shape `ssot-rarity.md`
§5 recorded as *"awaiting I4"*: **two integers per rung, the inclusive window a drop's socket count is
rolled from**, before the base type's own `socketMax` clamps it —
`rarityGrant.{rung}.socketMin`/`.socketMax` in `data/tuning/sockets.v1.json`, seeded by
`RpgStore.SeedSocketGrants` and read by `SocketGeometry.SocketsAtDrop`. `ssot-sockets.md` §9.5's one
constraint — *"rarity grants a **range**, not a number"*, so OD4's overlap principle reaches this axis
— is met and **enforced at LOAD**: `SocketTuning.Parse` refuses a table whose adjacent windows do not
overlap or whose grant is non-monotonic, because a gap turns socket count into a strict ladder and
re-opens `ssot-sockets.md` §8.1 at full strength. Seeding is again its own method, **not** folded into
`SeedRarityLadder`, so this module's seeding never grows a dependency on a later module's tuning file.
`RarityBudgetKeys` flips both to `HasDecidedShape: true`, and ⭐ **with every listed key now decided,
this section's `RarityBudgetKeysTests` row was MOVED rather than dropped**: the "not decided is not
safe-to-seed" gate is now asserted against a *synthetic* key with no consumer at all, because the
mechanism has to survive the closed list happening to be fully decided today — the next key added will
not be. Three sibling rows in modules 14/15's own suites moved the same way; all four are named in
P4.3's verification section.

⭐ **Addendum 2026-09-05 — the overlap simulator's anticipated consumer arrived, and the parity
invariant now has a real threshold.** This section built `RarityOverlapSimulator` naming
`spec-uniques.md` as the consumer that had *"declined to build a second simulator"*. Module 17
(`uniques`, P5.1 below) is that consumer, and it did not build one: `UniqueParityMetric` calls this
harness — same `Seed`, same `RollsPerRung`, same `UpsetRate` paired comparison — with the unique's own
magnitude as the fixed side, which is `ssot-uniques.md` §9.2's ask word for word (*"the same
measurement with a fixed-value item on one side, and it should be run on the same code with the same
seed"*). A test
walks every file under `Items/Uniques/` and asserts none names `SeededRng` or `new Random`.

**So `W ∈ [25%, 75%]` — *"stated, never measured"* since the lane was drafted, and an open question to
the owner at `ssot-uniques.md` §10.3 — is measured.** ⭐ **Its threshold is live** rather than the
unbounded placeholder `spec-uniques.md` prescribed *"until the harness exists"*, and the bounds are
tunable in `data/tuning/uniques.v1.json`. ⛔ **And the first measurement is not green:** 287 readings
over the real 144-row corpus report **90 in band, 47 strictly-better, 150 trophy** — reported, not
refused, because device 3 was never one of the three HARD devices. Details and the reason the rolled
side draws ONE affix (parity is per channel family; overlap is per rung) are in P5.1.

⭐ **One EDIT to this module's own file, recorded here rather than only there:**
`RarityOverlapSimulator` gained `TierCount` / `TierBand(tier)` / `TierMidpoint(tier)`. The band table
was private; module 17 must price a unique's fixed side in **the same units the harness rolls in**, and
a second copy of `(10,12),(20,25),(40,50),(85,100),(170,205)` is how a comparison starts measuring
nothing. Exposing it applies *"never write a second parity simulator"* to the data as well as the code.
No behaviour changed — `RarityOverlapSimulatorTests` is green, unmodified.

⭐ **Addendum 2026-09-05 — `unique_eligible` is the tenth key, and the closed list grew by a reviewed
addition rather than ad hoc.** Module 17 decided the shape `ssot-uniques.md` §5.3 proposed and named
this registry as the home for: **one 0/1 integer per rung, "may a unique carry this rung"**, 0 at
ordinals 10–20 and 1 at 30–100. It is **derived** from the ordinal against `uniques.v1.json`'s
`rungFloorOrdinal` (`UniqueTuning.IsRungEligible`), not authored as a second per-rung table beside the
seeded ladder — a table would be a second source of truth for a fact one comparison already decides.
Seeded by `RpgStore.SeedUniqueEligible`, again its own method so this module's seeding never grows a
dependency on a later module's tuning file (modules 14/15/16's precedent). §10.7 leaves the owner one
number to move if a `sprout`-rung joke unique is ever wanted.

**Not this module's job, named so nobody re-derives it here:** the `ceilingFor` reader / `pinAE`
live-pricing (module 9); the D11 dominance lint leaving channel-split mode (module 6, consumes the
seeded `power_ceiling` row); ~~`socket_min`/`socket_max` and `reroll_cost_mult` budget keys~~
(**all three resolved — `reroll_cost_mult` 2026-09-05 by module 15, `socket_min`/`socket_max`
2026-09-05 by module 16; see the two addenda above**; ~~`salvage_yield`~~ **resolved 2026-09-04**);
~~a light-theme palette for the ten rung colours (module 20 `item-surfaces`)~~ — **✅ RESOLVED
2026-09-04 by module 10, not by module 20.** See the addendum below.

⭐ **Addendum 2026-09-05 — the light-theme palette and the deuteranope transform this section
deferred to module 20 were ALREADY BUILT, one module later, and module 20 confirmed it rather than
building a second one.** This section's own note said *"`colourToken`s already exist in `core.v1.json`
and are asserted distinct here, but the deuteranope-transform test needs a palette that does not
exist yet."* Module 10 (`item-card`, P2.5 below) shipped both on 2026-09-04:
`src/FusionRpg.Core/Items/Display/RarityPalette.cs` implements sRGB → CIE L\*, WCAG 2 contrast and the
**Machado/Oliveira/Fonseca (2009) deuteranope *and* protanope** simulation matrices, cross-checked
against this section's own documented figures (`ssot-rarity.md` §3.3's L\* 42.1 → 91.9 reproduced to
one decimal), **and** the light-theme palette itself — L\* DECREASING 46.9 → 4.5, monotone under both
colour-blindness transforms, WCAG AA 4.5:1 against white on every rung, with a negative-control test
proving `Validate()` rejects a flat palette rather than always passing. ⛔ **Module 20 verified this
before writing a line and deliberately built no second palette or transform** (G3 §8.6's
one-renderer rule reaches colour science too); its own colour obligation reduced to GG-27's
*word-and-shape* redundancy channel, which is `DominancePresentation.Badge` and is asserted to carry
no colour property at all. **Nothing is owed here any longer.** The ten hexes remain a design pass
the owner may revise, exactly as module 10 recorded.

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
      `AtomKindRegistry.cs:534`'s live `RuntimeSupportMatrix(Full, Full, None)` on the `stat.derived`
      kind itself — not `:287`, which today is E30's unrelated channel-pool-object skip inside
      `Validate` — and `atom-family-library.md` §3.2's own *"the D6 quarantine is OVER"* banner, which
      itself cites the same matrix at its own stale `AtomKindRegistry.cs:160`) — from the global list
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
         by reshaping per (role, frame) instead of per role.
         ⭐ **Addendum 2026-09-05, from module 21 (`strain-splice-gen`): this reshape closed module
         21's only hard dependency, and it did so a day before the spec that declared the dependency
         open was even read.** `spec-strain-splice-gen.md` (measured 2026-09-03) states *"the maximum
         `socketMax` anywhere is 2 … no Strain and no Splice is buildable on any shipped chassis"* and
         *"this module is inert until"* module 6 issues 4 on `armament-primary` and `core-guard`. The
         even spread across `[0, ceiling]` did exactly that: the live distribution over 740 entries is
         `0×253 · 1×255 · 2×148 · 3×68 · 4×16`, the sixteen 4s are 8 `armament-primary` + 8
         `core-guard`, and **no entry omits the field any more**. So `RolesThatCanHostAStrain` returns
         a non-empty list, the geometric per-actor Strain ceiling of **2** is live rather than
         hypothetical, and module 21 is not inert. Recorded here because module 6 is where the fact
         lives; the stale citations it leaves behind are filed in **P4.3** and **P4.4**
- [x] **`socketCeiling(role)` forward-seeded** — `data/tuning/sockets.v1.json`, the exact 15-row table
      `spec-sockets.md` §3 already publishes (module 16 hasn't built yet; same precedent as module 7's
      provisional `power_ceiling`, module 16 stays the numbers' owner).
      ✅ **Confirmed 2026-09-05, not corrected.** Module 16 (P4.3 below) built and took ownership of the
      file (`version` 1 → 2) and carried all fifteen rows **unchanged, value for value** — re-deriving
      them would have minted a second source of truth. Both of this module's claims held on inspection:
      the ceiling is module 16's, and the per-entry value is module 6's. ⭐ **And the note this module
      wrote into the file — that module 16 must restate its own *"never varies by base type"* invariant
      as *"never exceeds its role's ceiling"* — was right, and module 16 restated it exactly that way
      (its correction **S2**). The corpus fact this module measured (`armament-primary` = `{0:18, 1:26,
      2:4}`) is what settled it.** Module 16's `SocketGeometry.ValidateEntry` now runs the same bound
      this module's `SocketMaxCheck` enforces, and the two agree on the real 720-entry corpus with zero
      findings
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
- [ ] ⏸ **`ContentValidation.cs:73`'s null-ceiling skip — not this module's to fix.** Named in the old
      todo wording as this module's; re-reading `spec-base-types.md` in fact names **module 9 alone**
      as owner of the `power_ceiling`-gated `corner-matrix` mode (`spec-base-types.md:228`: *"module 9.
      Owed there..."*; `:426`: *"module 9's, not this module's"*) — `channel-split`, this module's own
      obligation, needs nothing beyond `frame-lean.v1.json` per `spec-base-types.md:227` and never
      touches `power_ceiling` at all. The "module 6 also consumes the seeded row" framing comes from
      module 7's own todo entry (P2.1) and from `spec-rarity-bands.md`'s downstream chain (*"module 9 R1
      returns Unpriced → module 6's D11 dominance lint has no `score`"*), not from `spec-base-types.md`
      itself. Left exactly where module 7's own todo entry already named it as deferred to module 9

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet run --project tools\ItemSeedValidator` | **165 errors, unchanged from the pre-module-6 baseline** — all in `base-types/{humanoid,plant}-standard` (module 3's pre-existing, uncommitted `retiredReason` schema gap, D14 out-of-scope content) and three completely unrelated files (`affix-families/g-board.json` TierGap, `consumables/k3.json`, `enhancement-milestones/milestones.json`, `recipes/recipes.json` TagAxisNotApplicable) never touched by this module. **Zero** findings from `FrameDirectionCheck`/`SocketMaxCheck`/`ImplicitFamilyNotLegalForRole` against the live 720-entry corpus |
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.FrameLeanTests\|Items.ItemCategoryTableTests\|Items.BaseTypeCorpusTests` | **25 passed** (new), including the channel-split dominance lint green for all 12 hybrid-core roles |
| Standalone Python cross-check: every live entry's `implicit.family` against `classes.v2.json`'s `legalFamilies` | **0 illegal assignments** across 740 entries |
| Standalone Python cross-check: humanoid ∩ plant implicit families, per role | **0 violations** across all 15 live roles |
| `python -m pytest` (seedsmith, full suite) | **1498 passed, 1 skipped** — unaffected; seedsmith's `registries.py` reads `classLadders` from `classes.v1.json` only, which v2 never touches (purely additive to `excludedFamilies`/`implicitSlates`) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **5657 passed / 2 failed** — both `ClassSystem.UnitClassContractParityTests` — **world-stage**'s `world-numbers` module landing `loamUnits` mid-flight (W37/W38, same day), not class-system (its own `UnitClass` P1.4 closed 2026-08-26); **zero** in `Items.*` |
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

⛔ **Addendum 2026-09-05, filed from module 17 (`uniques`, P5.1 below) — `naming.v1.json` is stale in
four places, all in one block, and nothing reads the stale numbers.** `idNamespaces.uniques` declares
`partitionCount: 20`, `totalCombinations: "20 (matches authoring-fleet-plan.md's 20 agents exactly)"`
and `agentsEach: "~15 uniques"`, while its **own** `bandAssignment` table lists **18** rows (5 + 5 + 3 +
5) and the shipped corpus is **18 partitions × 8 = 144** — the count `ssot-uniques.md`'s own 2026-08-23
banner already carries. The same block's `themeSource` says *"themes.v1.json (15 themes)"* while
`themes.v1.json` holds **13**, which its neighbouring `themeCountNote` already states correctly.
A **documentation** defect, not a behaviour one: module 17 counted the corpus rather than quoting the
registry (the standing rule from the plan's own ⛔ box — *"never derive a design proportion from a
snapshot of a generated corpus; count it, or don't quote it"*), so nothing shipped against the stale
figures. Naming is this module's lane, which is why it is filed here rather than edited from there.

⛔ **Also filed from module 17: the `unique.` seed-id → `item.` container-id derivation this file left
open is CLOSED.** `idNamespaces.uniques.idVsContainerIdNote` recorded it as *"an open question for
wave-1b"* — the corpus's `unique.{theme}-{band}-{seq}` tracking id has no arm in `definitions.md` §1's
`container_id` alternation. `UniqueContainerIds` derives `item.{slug}` from the seed id's body verbatim
and inverts it, so a shipped row can always name the partition that authored it; all 144 pass the
shipped container-id grammar and are distinct. The registry note is now describable as answered rather
than open — left for whoever next edits that file, since editing it is this lane's call and not
module 17's.

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

### ✅ P2.4 — Module 9 `item-power-reads` — BUILT AND VERIFIED 2026-09-04 (R2/R3-card wiring left for module 19/10's own production callers; the chaff-chassis watch explicitly carried forward, unanswerable before module 21 exists)

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
- [x] ⛔ **ADDENDUM 2026-09-05, found and FIXED while building module 19 (`granted-actions`): R2 could
      report a number but could never say it was too big.** `ItemPowerReads.GrantedActionPrice`
      computed a share and then returned `Over: false` unconditionally, while
      `ItemPowerTuning.GrantedActionShareCapMilli` was parsed at boot (`item-power.v1.json`, `null`)
      and read by **nothing** — a tunable no code consumed, which is SC7 from the inside. This module's
      own note (*"reportable today and gating only when module 19 `granted-actions` lands"*) described
      the fix exactly: `GrantedActionPrice` now takes an **optional** `ItemPowerTuning`, so every
      pre-existing two-argument caller keeps `Over: false` unchanged, and `ItemGrantValidator` — the
      first caller ever — passes it and gets the gate. The cap stays `null` (no number invented); the
      fallback is the whole ceiling, 1000‰, a bounded ratio. **Cross-referenced from P5.3.**

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
and **`UnitClass` (N3) was already shipped too** — a real, fully-built enum at
`Stats/Derived/StatClass.cs` — 11 members when this module shipped, now 13 (`ReciprocalPoints`
2026-08-26, `LoamUnits` 2026-09-04) — with per-channel data in `DerivedStatRegistry`, exceeding this
spec's own 9-member proposal by an even wider margin today. Neither needed rebuilding; both needed a real consumer, which is what this module
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
      DECREASING 46.9 → 4.5 (adjacent Δ ≥ 2.5, distance-2 Δ ≥ 7), monotone under both colour-blindness
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
      modules 6 and 8's own entries above since it predates and is outside all three modules' scope.
      ⭐ **Confirmed from a second direction 2026-09-05 by module 17 (P5.1):** five of the eight —
      `atom.bonding`, `atom.buttering`, `atom.chilling`, `atom.marking`, `atom.rotting` — are also named
      by the shipped 144-row **unique** corpus, where they resolve to no affix-family row either, so
      their `kindId` is unknown. Module 17 **excludes them from `narrow`'s raw-stat subtotal rather than
      guessing** (a guess would make an unresolved reference look like a balance failure) and pins the
      five as a set. Same defect, wider blast radius than the display corpus alone; still not fixed
      from either module, and still the authoring fleet's re-run.
      ⛔ **A NINTH, found from a third direction 2026-09-05 by module 18 (P5.2):**
      `atom.elemental-power` — named by **11 of the 60 shipped consumables** (every elemental draught),
      resolving to no affix-family row. It differs from the other eight in one way worth recording: it
      is not merely unauthored, it is **exemplar-only** — `_exemplars/affix-family.exemplar.json`
      carries it as template content that P2.3 above explicitly and correctly left outside the real 98,
      and `ssot-consumables.md` §7.2's own worked example (`atom.elemental-power|fire`) is written
      against it. So a lane doc, an exemplar and 11 authored rows all name a family the corpus does not
      have. Module 18 excludes them from its runtime-legality check rather than guessing, and pins the
      set at exactly one family and 11 rows. ⚠ Note `atom.elemental-defense` **is** real and is what
      the other two element-bearing consumables use — the near-miss is part of why this went unnoticed
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
      scope. **Cross-referenced into P5.4 (module 20).**

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
> module 9/6's own consumers**, per module 6's todo entry (P2.2), tracing back to module 7's (P2.1); not
> flipped by this pass. An item card
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
      and `item_generation` stays the permanent record.
      ✅ **The filing was honoured 2026-09-05 by module 20 (P5.4), as a filter and never as a cap:**
      `LootFilterView` is a client-side view rule over already-owned rows plus the inbox count, and a
      guard test strips the comments and asserts its source names no `LootPipeline`, `DropTable`,
      `LootPity`, `DropEnvelope` or `RpgStore` at all. I12's wall-clock axis is restated **per content
      event** in `data/tuning/item-surfaces.v1.json` — the file has no clock parameter it could read one
      from. `CountEquipmentMinted` stays exactly what this module made it: a measurement with no
      consumer that could turn it into a gate
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
   (module 18; `ssot-generation.md` §5.4's "wait for the action layer" reasoning is now stale —
   item-ideal §7 was refined by `ssot-consumables.md` to skip the action layer via a menu-spend design,
   and module 18 shipped exactly that 2026-09-05; the entry-kind stays refused for a narrower reason
   instead: X7 has not minted the `consumable` `container_kind`, and the 60 are seeds with no
   `effect_container` row yet — see the ⭐ note below).
   Each is refused **by name** with `ContentRuleViolated{drop.entry-kind-unavailable}` naming the
   module that lands it — a build order, not a defect, and never a silent drop. **Not this module's to
   fix**, and cross-referenced into the owning modules' sections below.
   ⭐ **Resolved in part 2026-09-05 by modules 17 and 18 — the two largest blocks are now referentially
   live, and both reasons MOVED rather than being left pointing at a module that exists.** The 144
   `unique` refs resolve against module 17's corpus and the 60 `consumable` refs against module 18's
   (`ConsumableCorpusTests.All_sixty_consumable_drop_entries_resolve_against_this_corpus` asserts every
   one). **Both entry kinds stay refused**, one step further on in each case: no CONCRETE container
   exists for either (the corpora hold seeds, and rolling one is the runtime generator's under the
   seed-to-concrete rule), and `consumable` additionally waits on **X7**'s fifth `container_kind`.
   `DropTableDraw.UnavailableKinds` carries both updated reasons and both are pinned by a test, so
   neither can go stale a second time. **204 of the 315 unresolvable rows now have a named, one-step
   blocker instead of a module pointer;** the remaining 111 (70 `charm`, 41 `insert`) are still X7's.
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
- [x] ⭐ **`world-sector` `loot_source` — BUILT 2026-09-05. Before: the formula was decided and nothing
      implemented it, so `drop.world.sector-clear` shipped with no source. After:
      `PowerIndexComposer.MapLevel` is the decided formula in code, and `WorldSectorLootSource` wires
      the table to it.** The blocker had already moved once — `sectorLevel(danger_band)` was
      **resolved, not owed**: `ssot-power-scale.md` §5.3/§10.3 (owner decision 2026-08-23) closes it as
      `mapLevel(M) = Wm · DangerBand(M)`, `Wm = 5` derived from the shipped `SectorTypeCatalog` bands,
      and states the world program **"no longer owes an unknown"**; `spec-content-authoring.md` §2.1
      (owner approved 2026-08-24) confirms the identical formula for this exact `contentLevel` row.
      What was left was **unbuilt, not undecided** — a grep found no `MapLevel`/`SectorLevel` anywhere
      in `src/`, `web/`, `tools/` or `tests/`. It exists now, in `Core/Power` where the one ladder
      lives, reading `WmMilli` from `data/tuning/power-scale.v{n}.json` and never a literal `5`
      (`Map_level_reads_the_weight_and_never_a_literal_five` moves the weight and the level moves).
      ⛔ **No new §10 row was needed and none was added** — `mapLevel(M)` is **row 23**, already closed
      and already mirrored; a level derived from a *world state* is still that row, not a new
      power-shaped scale. What the row needed was its `location` repointed from prose to the code, in
      §10.2, §10.3 and `inventory.json` alike. X5 (content ladder past level 10) still bounds what a
      boss-lair's `contentLevel = 30` has *authored enemy content* to draw from; it never gated the
      formula, and it does not gate the loot lane — the shared 24-row slate is ilvl-band-free, so a
      band-6 clear resolves end to end today
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
   hit it at counts 2–3.

   ⏸ **Answered at P3.3 2026-09-04: examined, and deliberately not renamed — it is FOUR moving parts,
   one of them a frozen registry.** `NamespaceAllocation.cs:219-231` scrapes the breakpoints out of
   `naming.v1.json`'s `resonanceNote` **prose** and splices that raw regex-captured digit-string straight
   into `charm.res-{axis}-{breakpoint}` — no `int.Parse`, no reformatting, so whatever width the prose
   spells passes through verbatim — so padding the corpus alone makes the allocation mismatch;
   `naming.v1.json` is
   `registryVersion 4, "frozen": true`, which makes the note edit an **Ask first**; and
   `All_ten_shipped_resonance_ids_are_unpadded_…` pins the current spelling on purpose. Module 13
   generates the 60 authored charms, not the 10 resonance containers.
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
   pins the numbers. **Cross-referenced into P5.4 (module 20).** ✅ **PICKED UP 2026-09-05:** module 20
   built `SetDisclosure` — `SharedMembers` re-measures all three numbers (154 / 25 / max 3) against the
   real corpus, and `ForWearer` reports **per piece** which sets it advances and which it is
   *redundant* in, which is ssot-sets.md §4.5's *"say why the fourth did not count"* half. It counts
   nothing of its own: the `(set, role)` dedupe is `SetEvaluator.Hits`' own discipline re-expressed,
   and a test asserts the two agree on the same wearer, so the tooltip can never disagree with the
   "3 / 4" beside it.

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
- [x] ✅ **Module 22 `charm-carry` (D40) — the pouch was NOT here, by ruling, and it is now BUILT at
      P5.5 (2026-09-05). This deferral is CLOSED.** The five tables (`charm_def`, `charm_pouch`,
      `charm_run_hold`, `charm_attunement`, `charm_resonance`), the AP gate (budget · axis cap 3 · copy
      cap 2 · `unique_carry` 1 · `level_req`), its five reason codes, the run-start snapshot and the
      `CharmInUse` refusal all landed there, and `data/tuning/charm-attunement.v1.json` is its file,
      created there and not here. What this module kept is what D40 says it keeps: the evaluator, plus
      the `charm_def` **class rules** the evaluator's own corpus reader needs. ⭐ **Module 22 forked
      nothing** — its resonance tiers come from `CharmResonance.Consumer` driven through
      `ThresholdEvaluator`, and a test drives the evaluator by hand over the same snapshot and demands
      the identical list. ⛔ **One thing module 22 found that this section could not have known:** the
      five reason codes were **not** minted — §5.2's four player-action names became a module-local
      `CharmCarryRefusalReason` (module 4's `EquipRefusalReason` precedent) and the fifth became a
      `ContentRuleViolated{charm.*}` rule id, so definitions.md §10's closed 33 is still untouched
- [ ] ⏸ **The `(capability, threshold-family multiset)` median ≤ 2 gate is module 13's, and it is
      already passing on today's corpus.** It is `Distribution/CellOccupancy` in
      `spec-set-charm-gen.md` §, a **generation distinctness** gate over the generated population —
      this module generates nothing. Measured on the 30 shipped sets as a data point, not as a gate:
      **28 cells, median 1, max 2, 26 of 28 singletons.** The gate belongs where the generator is.
      ✅ **Landed at P3.3 2026-09-04** as `Distribution/CellOccupancy`
      (`seedsmith/metrics/cell_occupancy.py`), registered in `build_registry()` and reproducing those
      exact four numbers from real data. It ships `gates = False` with a written promotion trigger —
      the threshold is defined over the generated ~904, not over these 30
- [ ] ⏸ **Module 16 (`sockets`) should reuse `ThresholdEvaluator` rather than write a second one.**
      Same shape — count inserts in one item, grant at breakpoints — at the **host item's** scope
      rather than the actor's. Deliberately not folded in: merging them would make the scope a
      parameter of a thing whose whole identity is its scope. `ThresholdConsumer<T>` is generic in the
      held-thing type precisely so module 16 can instantiate it over an insert.
      ⚠ **Re-checked 2026-09-05 during the module-22 consistency pass: P4.3 never engages this ask.**
      `ResonanceGenerator`/`CombinationEvaluator` count inserts and grant at breakpoints — the same
      shape this bullet describes — but P4.3's build list, files and deferred items never mention
      `ThresholdEvaluator` or `ThresholdConsumer<T>`, so whether this was a deliberate decline or an
      unnoticed miss is still open. **Cross-referenced into P4.3; not resolved there.**
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

### ✅ P3.3 — Module 13 `set-charm-gen` — MACHINERY BUILT AND VERIFIED 2026-09-04 (⏸ the generative authoring pass itself is model-call work and is explicitly out of scope for a coding session — named below with who runs it)

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped 40-table
seedsmith drop-table corpus (`data/seed/items/drop-tables/`) already references **70 `charm` entries**
that no build can resolve to a payload — `ContainerRow.cs`'s `ContainerKind` ships six values and none
of D27's four (`gem`/`set`/`charm`/`combo`), so **X7 has not landed**. Module 11's importer refuses each
by name — `ContentRuleViolated{drop.entry-kind-unavailable}` — rather than dropping it silently, and
names this module plus X7 as what unblocks it. **Not a defect in module 11 or in the corpus**: the
entries were authored deliberately in wave R2 to close the "144 uniques, 70 charms and 60 consumables
that no table could yield" gap `entry-shapes.md` §9 records. Filed here so this module knows those 70
references are already waiting on it. **Still true after this pass** — this module builds the
generator, not the container kind.

⭐ **What "model calls" means for this module, decided by reading the plan's own text rather than
guessing.** The spec's Project structure is a **Python seedsmith package**, not C#: `setgen/`,
`charmgen/`, a `cli.py` edit, a new registry, a new tuning file and a new metric. All of that is
deterministic and all of it is built here. The one genuinely generative step — drawing 36 build sets
and ~904 species sets and ~904 charms out of a model — cannot be run from a coding session, so it is
deferred **by name**, with the command that runs it and the person who runs it stated. Everything the
run consumes, everything it emits ids for, and everything that judges it afterwards is built and
tested against real shipped data.

- [x] ⭐ **The twelve-role cap is a GENERATOR INPUT and it is applied inside the SCHEMA, not after.**
      `setgen/roles.py` enumerates `HYBRID_CORE_ROLES`, and `schema.set_schema` puts that exact tuple
      in `members[].items.properties.role.enum` — so the model is **never offered** `head-guard`, and
      `SetRoleNotUniversal` is unproducible from a well-formed answer. That is the whole point:
      §3.7 fires at LOAD, so ~1,000 sets checked afterwards is ~1,000 rejections and a re-run.
      `every_generated_member_role_is_in_the_twelve` asserts the schema enum *is* the tuple, and
      `a_generated_set_never_claims_head_guard_sense_or_ward_array` names all three drops
- [x] ⚠ **The spec's own reason for enumerating rather than deriving is now STALE, and the code says
      so instead of repeating it.** The spec's code-style block says to enumerate because
      `core.v1.json`'s `hybridEligible` flags still name thirteen roles at 895‰. They do not — P1.3
      shipped `registryVersion 2` and **`assert_core_agrees()` re-measures 800‰ over exactly those
      twelve on every call** (measured, not asserted from the doc). The list stays enumerated for the
      *other* reason `linkage.py` already states — it must work against a fixture with no registry —
      and the drift check is what keeps the two honest.
      `a_role_table_that_moved_raises_instead_of_being_absorbed` moves one `budgetWeightMilli` by 5 in
      a temp copy and asserts `RoleCapViolation`
- [x] ⭐ **`audit_schema`-clean by construction, proven three ways.** Both schemas return `[]` from
      `audit_schema`; adding one bare `{"type": "integer"}` field makes `Pipeline(...)` **raise at
      construction** (`a_bare_integer_magnitude_field_fails_pipeline_construction`); and
      `pieces` is the single legal numeric shape — a closed enum **read from the tuning file**, so
      the schema and the distributor cannot disagree about which piece counts are legal. Neither
      schema carries a name on the deny-list: no `tier` (tiers come from `numerics`), no `cost`
      (`apCost` is derived from `charmClass`), no `powerBand` (the distributor assigns the band
      positionally). **No allow-list escape hatch was used anywhere** — the schemas avoid the names
      rather than exempting them
- [x] **`data/tuning/set-charm-gen.v1.json` + a pure parser, and the parser refuses rather than
      defaults.** Every number a balance pass would touch is there — set shape, the piece roll plan,
      the AE budget, the charm class table, all three distinctness thresholds with their derivations
      written beside them. `SetCharmGenTuning` has **no default for any key**: a missing one raises
      `SetCharmTuningError` at load, because a generator silently running on a default is how an
      unreviewed number reaches ~1,800 entries. Nine structural invariants are checked at load, each
      with its own message so a balance pass reads which one it broke
- [x] ⭐ **Both of ssot-sets §3.9's named failure modes are refused BY THE PARSER, so no run can be
      configured into them.** `fixedIdentityAtoms = 0` is *"fixed like a unique"* and
      `prefixRolls + suffixRolls >= rareComparisonRolls` is *"rolled like a rare — set jail arriving
      through the item layer, where none of §3.5's five rules can reach it."*
      `no_set_piece_is_fixed_like_a_unique_or_rolled_like_a_rare` feeds the parser each cheat as a
      real temp tuning file and asserts the refusal by message
- [x] ⭐ **The vocabularies are COUNTED from the live corpus, never transcribed — and they reproduce
      the spec's arithmetic exactly.** `vocab.build()` over the real 98 affix families:
      **42 capability families → 60 picks** (39 element-free + 3 × 7 variant) and **56 stat families
      → 242 picks** (2 element-free + 31 × 7 variant + 23 `stat.modify`). The standing rule from this
      program's own plan phase — *never derive a design proportion from a snapshot of a generated
      corpus* — applies to a vocabulary size just as hard, so the counts live in a test and in the
      todo, never in the tuning file. A family whose `kindId` is in neither bucket **raises** rather
      than being quietly dropped
- [x] **The distributor prices what the model chose and refuses what it broke, naming every rule.**
      `distribute_set` returns ALL violations, not the first — one capability at the lowest threshold
      (`SetCapabilityMissing` / `SetTierForbiddenAtom`), stats only above it, no `More`-op family on
      any tier, a threshold at 2 with no exceptions, top threshold ≤ member count, ≤ 6 roles, at most
      one armament (`SetRoleForbidden`), and the AE budget. **Nothing is repaired into legality** —
      silently fixing a draft teaches the next call nothing, which is `call_with_self_heal`'s own
      reasoning
- [x] **The AE budget is integer per-mille and the apportionment is exact.** `aePerMemberMilli` 1500,
      so a 4-piece set is 6000 milli-AE; the split is by atom count with the remainder landing on the
      top threshold, so the sum **equals** the budget and can never round above it.
      `a_sets_total_tier_value_never_exceeds_one_and_a_half_AE_per_member` asserts both the bound and
      the equality; the multiply happens before the divide, once
- [x] ⭐ **The id defect that would have shipped broken is refused at the minting function.**
      `emit.set_id("demon.allpeater", 1)` raises `IdRefused` with *"two dots"* in the message;
      `emit.set_id("allpeater", 1)` gives `set.allpeater-001` and `tier_container_id(..., 4)` gives
      `set.allpeater-001-04`. The pad is asserted load-bearing by sorting `-02 / -04 / -10`
      (module 12 proved that at the DAL). Minting into the **900-999 correction range** is refused,
      and so is a `speciesId` that collides with one of `naming.v1.json`'s five pinned partitions.
      `every_shipped_species_id_is_kebab_legal_and_mintable` re-verifies all 84 rather than trusting
      the spec's "verified safe"
- [x] ⭐ **`data/seed/items/_registry/build-themes.v1.json` — the third `themeKey` population, and it
      is DERIVED, not authored.** 36 rows = 12 aptitudes × 3 archetypes, generated from
      `data/seed/aptitudes/roster.json` (the checked-in mirror of `AptitudeCatalog.All`, whose own
      count is `PostureCount × PerPosture`), so a thirteenth aptitude changes the grid by
      construction. `aptitudeMeaning` / `aptitudeReading` are that roster's own strings carried
      verbatim — **no flavour is invented in this file.** Deliberately not frozen, and append-only.
      Wired into `registries.load_theme_keys()` (Python) and `RegistrySet`/`ReferenceCheck` (C#), so
      a build set's `themeKey` resolves on both sides
- [x] **The theme bridge is one-way and asserted structurally.**
      `nothing_in_the_generator_writes_the_demons_corpus` scans every module in `setgen/` and
      `charmgen/` for a write verb on the same line as `demons`, and
      `nothing_generated_keys_on_theme_rarity` scans for a read of `theme.rarity` (§2.4a — rarity is a
      roster snapshot, not an attribute)
- [x] ⭐ **`Distribution/CellOccupancy` built and registered — the reskin bar, on the axis that
      carries distinctness.** Cell key = `(capability, sorted multiset of the stat families at every
      threshold above the lowest)`. Measured on the real 30-set corpus: **28 cells, median 1, max 2,
      26 singletons (928‰)** — the same numbers P3.2 measured by hand, now produced by a registered
      metric. Capability usage (**19 distinct over 30 sets**) is emitted as a separate NOTE and
      **never gates**, because passing it proves nothing about distinctness
- [x] **The run verdict is `pass` only when every gating metric both ran and cleared.**
      `verdict.py` names the five gates and the tuning key each threshold is read from;
      `missing_thresholds()` returns `[]` and the meta-test asserts it, so *"a command with no
      threshold is something you run and then argue about"* is closed as a fact, not an intention. A
      held partition alone denies the pass; a FAIL beats a NOT_MEASURED; the two report-only metrics
      still appear in the report, because a metric that runs and is never read is the same as one
      that never ran
- [x] ⛔ **`seedsmith items` — the subcommand group the spec's own Commands block called and that did
      not exist.** `build_parser` registered `check`/`report`/`metrics`/`demons`/`effects` and nothing
      else, so every command the spec listed was a documented interface that only worked if you knew
      the private module path. `items generate --kind set|charm --population build|species` now runs,
      prints the plan as JSON, and `--sample-brief` prints a real assembled brief. **`--write` is
      refused with a reason** rather than silently writing nothing
- [x] **Resume is built and atomic.** `run.plan_run` reads a ledger and returns only the subjects not
      already done; `write_ledger` writes through a temp file and `os.replace`s, so a killed process
      leaves the old ledger or the new one, never half of one. `the_run_resumes_after_an_interrupt_without_duplicating_entries`
      marks 10 of 36 done and asserts 26 remain with zero overlap; `re_running_over_unchanged_themes_is_byte_identical`
      compares both the subject dicts and the assembled brief text
- [x] **`set_eligible` / `charm_potency` are never asked back.** Module 7 dropped both under SC7 (D15
      makes the first vacuous — a set has no rarity and completes from pieces of any rung — and a
      registered key with no shipped consumer rejects at seed load). `SC7Tests` greps every module in
      both packages **and** the tuning file for either name
- [x] **D17's dead tail is protected in code.** `the_tuning_file_carries_no_content_ceiling` refuses
      `maxGeneratedSets` / `maxSpeciesSets` / `rosterCap` anywhere in the tuning file — a cap on the
      generated population would be a hard progression ceiling on content breadth, and D12's
      roster-scale generation is the point

**⛔ Five defects / stale claims found while building, all measured rather than asserted:**

1. ⛔ **D30's 18 legacy sets are STILL OPEN, and the corpus says so directly.** Measured against
   `data/seed/items/sets/**` rather than trusting any document: **18 of 30 sets** name a dropped
   role — **10 use `head-guard`, 11 use `sense`, 3 use both** — and
   `seedsmith check --adapter items --metric Linkage/SetCompletability` reports **30 GAP findings**
   over exactly those 18. This is D30's own accepted cost and it closes only when the generation run
   below actually executes. **Cross-referenced into P0.5.** ⚠ Not fixable deterministically: a member
   role is a **model-chosen** field under P1, so a code-side role swap would be deterministic code
   writing identity — the exact inversion P1 forbids.
2. ⛔ **The species denominator every D34 number is quoted against counts the wrong thing.** The plan
   and the spec both say *"386 species (292 plant + 94 zombie)"*, derived from
   `ls data/seed/demons/species/{plant,zombie} | wc -l`. Those are **family files**, each holding many
   species. `_index.json` is a flat `{speciesId: "plant/family.json"}` map and it holds **840
   species** across **495 family files** (measured 2026-09-04; the tree is being rewritten by the
   concurrent stream, so both move). So the theme-registry staleness is **84 of 840 — 772 uncovered**,
   not 84 of 386. `species_family_file_count()` exists as its own function precisely so a test can pin
   that it is NOT the species count. **Cross-referenced into P0.2** — `theme-refresh` is sized against
   the wrong number today.
3. ⛔ **16 published themes name a species the anchor tree no longer ships** (`cherrygatling`,
   `cherrypaperzombie`, `cornpot`, `dancepolzombie`, `dolldiamond`, …). `coverage_report` reports
   `orphaned` beside `uncovered` for exactly this reason: a republish that only *adds* leaves them
   behind. **Cross-referenced into P0.2.**
4. ⛔ **`SemanticDedup/NearDuplicate`'s MinHash estimate over-reports by up to 7× on names this
   short — and this module's spec makes that metric a GATE.** Measured on live corpus names:

   | pair | true Jaccard | 32-hash MinHash estimate |
   |---|---|---|
   | `'Tier Duration'` / `'Husk of the Murmuration'` | **0.120** | 0.844 |
   | `'Spiralled Bead'` / `'Spiralled Intercom'` | **0.333** | 0.906 |
   | `'Root of the Foundation'` / `'Signet of the Foundation'` | 0.652 | 0.719 |

   Over the 100 shipped set + charm rows the shared metric flags **4** pairs; the exact filter finds
   **1**. Gating a run on a signal with that false-positive rate would fail every run for the wrong
   reason. Fixed **for this module only** — `setgen/dedup.py` applies the standard MinHash+LSH
   pattern (LSH proposes, exact Jaccard filters) and imports `shingles` from the shared metric so the
   tokenisation cannot drift. ⚠ **The shared metric is deliberately NOT changed from here**: it is
   registered for every adapter and its 62-finding count is another stream's baseline. The test
   pinning the divergence is written so the eventual fix has something waiting for it.
5. ⚠ **Mitigation #2 does not hold uniformly, and the spec states it as though it does.** *"Capability
   families carry `roles`, so a set's member roles already narrow the legal capability pool"* is true
   for most roles — `retinue` reaches **7 of 60**, `footing` **13 of 60** — but **`jewel-minor-a`
   reaches all 60**. It is the universal capability host in the shipped corpus, so a set claiming a
   minor jewel gets the whole pool back and the constraint does no work for it. Found by a test whose
   first draft assumed narrowing everywhere and failed; corrected against the data rather than the
   data being read as wrong.

**Three judgement calls the spec does not state, all named:**

- ⚠ **`CellOccupancy` ships `gates = False`, and the promotion trigger is written down rather than
  remembered.** The threshold is defined over the **generated species-set population** (~904), which
  does not exist; today's corpus is 30 legacy sets, a different denominator, and promoting now would
  gate CI on the wrong population. The finding is still **GAP** severity when the median is exceeded,
  so a plain `seedsmith check` catches it, and `verdict.py` treats it as a real gate for the run
  itself. `PROMOTION_TRIGGER` is a module constant a test asserts.
- ⚠ **The 5‰ near-duplicate ceiling is not measurable at n = 100 — one pair is already 10‰.** The
  shipped set + charm population has **zero** exact duplicates and **one** genuine near-duplicate
  (`'Root of the Foundation'` / `'Signet of the Foundation'`, true Jaccard 0.652). The test asserts
  the exact count — so a second pair is a failure — and records that the rate exceeds the ceiling
  *because of granularity*, not because of a distinctness problem. The threshold is meaningful at
  ~1,844 entries, where 5‰ is ~9 pairs.
- ⚠ **A family's legality on a dropped role is filtered out of the brief.** The shipped families list
  `head-guard` / `sense` / `ward-array` in their own `roles`, and printing that verbatim would put a
  dropped role in front of the model in the same document that tells it those roles do not exist.
  Found by a test; `_core_roles` narrows the display to the twelve.

**Verification, run and green:**

| Command | Result |
|---|---|
| `python -m pytest tests/test_set_charm_gen.py -q` | **78 passed, 201 subtests** (new) |
| `python -m pytest` (seedsmith, full) | **1583 passed, 1 skipped, 288 subtests** — exactly P3.2's 1505 plus this module's 78 |
| `python -m seedsmith items generate --kind set --population build --dry-run` | **36 subjects, held 0, complete true**; 60 capability picks / 242 stat picks; `gatesMissingAThreshold: []` |
| `python -m seedsmith items generate --kind set --population species --dry-run` | **53 subjects, 31 held (`basis=name`), complete false** — the honest answer while P0.3 is unbuilt |
| `python -m seedsmith items generate --kind charm --population build` | **refused, exit 2** — there is no build charm population |
| `python -m seedsmith check … --metric Distribution/CellOccupancy` | **30 sets over 28 cells: median 1 (threshold ≤ 2), max 2, singletons 26/28 (928‰)** + the capability-usage NOTE (19 distinct) |
| `python -m seedsmith check … --adapter items --gate` | exit 1, **61 gap / 80 note / 14 not_measured** — the 61 gaps are **byte-identical to the pre-build set** (diffed); the only delta is **+2 NOTE** from the new metric |
| `python -m seedsmith check … --metric Linkage/SetCompletability` | **30 gap** — D30's 18 sets, unchanged and expected |
| `dotnet build tools/ItemSeedValidator` | **0 warnings, 0 errors** |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors across 120 partitions — identical to the module-6/8/11/12 baseline.** Zero new findings from the `build-themes` union |
| `dotnet run --project tools\AtomImporter -- --check --validate` | **clean** — 17 files, 66 atoms, 7 containers, 10 rarity bands |
| `python scripts\audit-overflow.py` | **0 critical**, 55 findings — unchanged from P3.2 |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0**, 17 total; the 5 `items` rows are modules 8/10's pre-existing ones. Nothing this module added is C# |
| `.\scripts\guard-dal.ps1` / `guard-single-writer.ps1` / `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` | **all four OK** |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6177 passed / 0 failed** — ⭐ the six-failure baseline measured at the start of this session is **gone**, fixed upstream by the concurrent stream mid-session |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **713 passed / 2 failed** — both `DemonSpeciesImportCliTests`, the concurrent demon stream's (48 files under `data/seed/demons/` are mid-edit in `git status`). Down from the 3-failure baseline: `AtomStoreTests.An_unknown_trigger_is_rejected` was also fixed upstream |
| `dotnet test tests\FusionRpg.Guard.Tests` | **197 / 197**, up from 184 at P3.2 |

⚠ **One Core run aborted with *"Test host process crashed"* mid-suite** (1 failure recorded before the
abort, `Demons.VariantCountBandTests`). The immediately following clean re-run is 6177/0. Same
intermittent P3.2 recorded for `Data.Tests`, now seen on `Core.Tests` too, and it happens while the
concurrent stream is rewriting the species tree under both.

⚠ **One test in another suite had to be updated, and it is this module's change that moved it.**
`test_demon_themes.py::test_load_theme_keys_returns_the_thirteen_registered_legacy_themes_prefixed`
pinned the themeKey vocabulary at exactly two populations. Rewritten to assert **13 legacy + 36 build
= the whole union**, so the original subject (exactly thirteen legacy themes) is still pinned exactly
rather than loosened to `>= 13`.

- [ ] ⏸ ⭐ **THE GENERATIVE AUTHORING PASS ITSELF — 36 build sets + ~904 species sets + ~904 charms —
      is out of scope for this pass, and this is the honest boundary, not a gap in the build.** It is
      ~1,844 live model calls; a coding session cannot make them. **Who runs it:** the owner, from
      their own terminal, once the two blockers below clear. Everything the run needs is built: the
      briefs assemble, the ids mint, the schema is audit-clean, the distributor prices, the ledger
      resumes and the verdict judges. **Until it runs, `Linkage/SetCompletability` stays red on 18
      sets and the species population's verdict is `not_measured` — both by design.**
- [ ] ⏸ **The generation graph is not wired, and `--write` says so instead of writing nothing.** A
      `workflow/graphs/item_set.py` (mirroring `workflow/graphs/effect_affix.py`) is what connects
      `plan_run`'s subjects to `llm_caller`. Deliberately not stubbed: a graph that silently produces
      nothing is worse than a command that refuses. `cmd_items` returns `EXIT_REFUSED` with the
      reason.
- [ ] ⏸ **P0.2 `theme-refresh` and P0.3 `theme-enrich` are unbuilt, and they gate the species half of
      the run — they are seedsmith's modules, not this one's.** Today **31 of 84** themes sit at
      `basis = "name"` and are HELD (never generated from, never silently skipped), and the registry
      covers 84 of **840** species. This module's contribution is to make both states *loud*:
      `holdback_report` and `coverage_report` are what the run verdict reads, and defect 2 above
      corrects the number P0.2 is sized against. **The build half (36 sets) needs neither** and is
      `complete: true` today.
- [ ] ⏸ **`naming.v1.json` registryVersion 3 — widening the set `partitionCount` from 5 to ~904 — is
      an ASK-FIRST on a frozen registry and is not done here.** The spec's own Boundaries list it
      under *"Ask first"*, and the file's `frozenNote` prices a required change at *"v3 plus an
      explicit re-run decision."* `emit.set_id` already mints the correct shape and refuses a
      collision with the five pinned partitions, so nothing is blocked by the bump not having
      happened — it is a registry ceremony the owner owns.
- [ ] ⏸ **`demon.*` themeKeys do not resolve in `ItemSeedValidator`, so a generated species set would
      report `RegistryValueUnknown` today.** `ReferenceCheck` resolves `themeKey` against
      `RegistrySet.ThemeIds`, which is now `theme.*` ∪ `build.*` — the demon population lives in
      `data/seed/demons/_registry/themes.v1.json`, and having the **items** validator read the
      **demons** registry is a boundary decision, not an edit. Named rather than crossed: the Python
      adapter already has the seam (`load_vocabularies(demon_theme_keys=…)`); the C# tool does not.
      **This blocks persisting species sets, not generating them.**
- [ ] ⏸ **`Distribution/CellOccupancy` promotion to `gates = True`** — trigger recorded in
      `PROMOTION_TRIGGER` and asserted by a test. It flips with the generation run, not before.
- [ ] ⏸ **X4 / L0 channel registration — sets SUPPLY the `set` channel to effect-pipeline's pool
      composition (charms ride the same channel; the enum is closed at six —
      `drop`/`boss`/`set`/`socket`/`unique`/`craft` — there is no separate `charm` channel, per
      `spec-affix-channel-weights.md`'s own table), and L0 is **SPECCED**
      (`spec-affix-power-class.md`, `spec-affix-channel-weights.md`) **and unbuilt** — matching X4's own
      re-scoped entry above, not contradicting it.** Generation can proceed
      (channels are a weighting layer over an already-legal pool) but **the run's value is not
      provable until L0 lands**, which the spec itself says should be stated before tokens are spent.
      Restated here so it is said twice.
- [ ] ⏸ **X7 — `ContainerKind` gaining D27's four values — same blocker P3.1 and P3.2 both carry.**
      Nothing this module generates has a legal container home until it lands. A wiring gap with a
      named owner, not a wall.
- [ ] ⏸ **P3.2's defect 2 — the ten unpadded resonance ids — was examined here and is deliberately
      NOT renamed, because the rename needs a frozen-registry bump and would break a shipped test.**
      Module 12 forwarded it to this module. Traced end to end: the ids live in
      `data/seed/items/charms/resonance.json` (`charm.res-offense-2`), the allocation is derived by
      `tools/ItemSeedValidator/Registries/NamespaceAllocation.cs:219-231`, which regex-scrapes the
      breakpoints out of **`naming.v1.json`'s `resonanceNote` prose**
      (`Regex.Matches(note, @"charm\.res-[a-z]+-(\d+)")`) and rebuilds `charm.res-{axis}-{breakpoint}`
      **as a raw string splice — no `int.Parse` anywhere in the file — unpadded only because the
      prose's own worked examples (`charm.res-offense-2`, `-3`) are already unpadded** — so padding
      the corpus alone (without also repadding the prose's worked examples) makes the allocation
      mismatch. `naming.v1.json` is `registryVersion 4, "frozen": true`, which
      puts the note edit under the same **Ask first** as the set `partitionCount` bump. And
      `ThresholdGrantCorpusTests.All_ten_shipped_resonance_ids_are_unpadded_and_the_divergence_is_measured_not_normalised_away`
      asserts the current spelling on purpose. **Four things move together or none do**; this module
      generates the 60 authored charms, not the 10 resonance containers, which the spec itself says
      *"are not charms a player carries."* **Cross-referenced back into P3.2.**
- [ ] ⏸ **`SemanticDedup/NearDuplicate`'s MinHash false-positive rate (defect 4) is fixed for this
      module only.** The shared metric keeps its estimate; the one-line change (verify each LSH
      candidate with exact Jaccard) belongs to whoever owns that metric's baseline, and the test
      pinning the divergence is already written.

**Files:** `data/tuning/set-charm-gen.v1.json` (new — set shape, piece roll plan, AE budget, charm
class table, the three distinctness thresholds with their derivations);
`data/seed/items/_registry/build-themes.v1.json` (new — 36 `build.*` keys, derived from the aptitude
roster); `tools/seedsmith/seedsmith/adapters/items/setgen/{__init__.py, roles.py, tuning.py, vocab.py,
schema.py, brief.py, themes.py, distribute.py, cells.py, dedup.py, emit.py, verdict.py, run.py}` (new);
`tools/seedsmith/seedsmith/adapters/items/charmgen/{__init__.py, rules.py}` (new);
`tools/seedsmith/seedsmith/metrics/cell_occupancy.py` (new — `Distribution/CellOccupancy`);
`tools/seedsmith/seedsmith/report/cli.py` (EDIT — the `items` subcommand group, `CellOccupancy`
registered); `tools/seedsmith/seedsmith/adapters/items/registries.py` (EDIT — the `build.*` population
unioned into `load_theme_keys`); `tools/ItemSeedValidator/Registries/RegistrySet.cs` (EDIT — optional
`build-themes.v1.json`, unioned into `ThemeIds`); `tools/ItemSeedValidator/Checks/ReferenceCheck.cs`
(EDIT — strip `build.` as well as `theme.`); `tools/seedsmith/tests/test_set_charm_gen.py` (new, 78
tests); `tools/seedsmith/tests/test_demon_themes.py` (EDIT — the themeKey union is three populations).

**Verify:** `cd tools\seedsmith; python -m pytest tests/test_set_charm_gen.py -q`;
`python -m seedsmith items generate --kind set --population build --dry-run`;
`python -m seedsmith check ..\..\data\seed\items --adapter items --metric Distribution/CellOccupancy`;
`dotnet run --project tools\ItemSeedValidator`

> ### ⏸ CHECKPOINT 3 — HALF HELD, AND NAMED
> A drop table produces an item at a level and its rarity distribution matches the published bands
> (module 11, P3.1 ✅). A set bonus fires at its breakpoint at `unique-actor:` scope with no atom at
> `player:` scope (module 12, P3.2 ✅). **The remaining half — a *generated* set doing that — waits on
> the model-call run named above, and on X7 for a container to bind into.** Stated as held rather than
> ticked: the machinery is built and tested, the content is not authored.

---

## Phase 4 — economy and depth

### ✅ P4.1 — Module 14 `salvage-craft` — BUILT AND VERIFIED 2026-09-04 (the `rpg_demon_materials` rename, the ten missing shard display rows, and the seven `reroll` corpus recipes explicitly deferred with owners named)

- [x] ⛔ **The 10× re-key, done — and the field is named so the mistake cannot be made again.**
      `RecipeContext.TargetRungIndex` / `SalvageInput.RungIndex` are the rung **index** 0–9 on
      `RarityLadder.RungIds`, never `rarity.ordinal` (10…100). Both throw on an out-of-range value
      rather than clamping, and `An_out_of_range_rung_throws_rather_than_clamping` feeds one a
      literal `60` — a real mid-rung `ordinal` — and asserts the refusal, so the 10× defect is a red
      test rather than a silently wrong price. ⚠ **The spec's own Code-style block still spells the
      field `TargetRarityOrdinal, // 0..9, the rarity table's own ordinal`**, which is exactly the
      confusion its own Platform-correction section warns against; the correction wins, and the
      divergence is recorded in the type's XML doc so a reader of the spec finds it.
- [x] ⭐ **The 27-id closed vocabulary, five classes, with the shipped sixteen REUSED not re-minted.**
      `MaterialCatalog` builds `shard.*` ×10 off `DemonRarityLadder.All` and `essence.*` ×6 off
      `ElementRoster.Concrete` — the same two rosters `DemonMaterialCatalog` reads — and appends the
      eleven this module owns (`substrate.{frame}.{grade}` ×8, `catalyst.{verb}` ×3). **27 and not
      28** because souls carry no id: they are a ledger balance, and the test asserts that too. The
      four legacy shard ids are `IsKnown` **true** / `IsIssuable` **false**, so a saved reference
      resolves and nothing new is ever created in the retired vocabulary
- [x] **A source-tagged id has no spelling at all.** `essence.fire.pvz` / `shard.heirloom.web` /
      `catalyst.forge.lawn` are refused by `ClassOf` on the dot count, not by a deny-list — the
      Boundaries' "Never" made structural. The injector enriches; it never gates (SC8)
- [x] ⭐ **`socket.imbue` has a cost row, it is `bore`'s verbatim, and the equality is checked AT
      LOAD.** I9 §7.4 has nine operations and no row for imbuing at all; the reference table now has
      **ten**. `imbue`'s souls (`50 × b`) and substrate (`3 × b`) legs are byte-equal to `bore`'s, per
      D24, plus one essence leg (`2 × b`) because essence is the class whose whole job is direction
      without magnitude. `MaterialTuning.Parse` **refuses a tuning where they diverge**
      (`A_tuning_that_breaks_D24_is_refused_at_load_not_at_the_first_crafted_socket` moves `imbue`'s
      coefficient by 1 in a temp copy and asserts the message names D24), so a balance pass that
      moves `bore` and forgets `imbue` fails at boot rather than at the first crafted socket.
      ⚠ `socket-imbue` as an `op_kind` is still **module 15's** to add and this module mints none —
      `CraftOperations.TryParse("socket-imbue")` returns false, asserted
- [x] ⭐ **D23 is real on the wire: any rarity can bore, and the bottom of the ladder pays a real
      price.** `bore` is rung-linear (`50 × b`, `b` = rung index + 1), so
      `Cost_rises_with_the_target_and_theta_is_not_an_input_at_all` walks all ten rungs asserting each
      costs strictly more than the one below **and** that `chaff` costs more than zero — the exact
      failure the old per-rarity table had, where the bottom rung was granted zero and could not
      reach its own `socket_max`
- [x] ⭐ **D26 is proven MECHANICALLY, not reviewed.** `RecipeContext` and `SalvageInput` are asserted
      by reflection to expose exactly their five/six target fields and nothing else;
      `MaterialRecipeCatalog.Resolve` is asserted to take `(string, RecipeContext)` and no third
      argument that could smuggle a player stat past the type; and the closed `CostVariable` enum is
      asserted to have **no spelling** for `theta` / `playerLevel` / `powerIndex` / a daily or session
      counter. There is nowhere to put a player property, which is the point
- [x] **Every quantity is in `data/tuning/materials.v1.json`, and the parser REFUSES rather than
      defaults.** No key has a default: stripping any of the five top-level sections throws at load
      (asserted section by section against the real file). Nine structural invariants are checked at
      parse time, each with its own message — grade count against the substrate vocabulary, the
      upcycle cap below the top grade, the upcycle ratio against its own drain valve, D24's equality,
      the cost-class matrix against every priced leg, salvage monotonicity, R1's bottom edge, and a
      positive `substrateBase` on every rung
- [x] **`audit-magic-numbers.py --summary` reports `M1 = 0`** — the `materials` domain appeared with
      one M1 mid-build (a `new List<string>(27)` capacity hint, not a balance number) and it is gone;
      the domain no longer appears in the table at all. `audit-overflow.py`: **0 critical, zero
      findings anywhere under `Items/Materials/`**
- [x] **`long` on every magnitude, widened before multiplying, divided by 1000 last and exactly
      once.** `CostLeg.BaseQty` is `checked` and widens (`Coefficient * (rungIndex + 1L)`), and
      `MaterialTuning.ApplyBand` is the single divide:
      `checked(Math.Max(1, (baseQty * multiplierPerMille + 999) / 1000))`. A 3-billion base quantity —
      past `int`'s 2,147,483,647 ceiling — resolves exactly to 24,000,000,000; `long.MaxValue`
      **throws**, it does not wrap. ⚠ `ContentScale.Apply`'s `int` return (the A3 target the spec
      warns about) is **not** copied onto the cost path: nothing in `Items/Materials/` calls it
- [x] ⭐ **The band→quantity resolution is the seed contract working, and it is asserted against the
      FROZEN registry.** `seed-contract.md` §3 forbids an author typing a magnitude, so the 30
      shipped recipes author a `costBand` and this module resolves it:
      `resolvedQty = max(1, ceil(baseQty × multiplierPerMille / 1000))`, bands.v1.json's own formula.
      The multiplier table is mirrored into `materials.v1.json` (Core never reads a file) and
      `The_cost_band_table_mirrors_the_frozen_registry_value_for_value` reads the **real**
      `bands.v1.json`, asserts it is still `frozen`, and compares every value and the whole enum — so
      a drift is a red test rather than a silent 2× price change
- [x] ⭐ **I9's two worked examples both reproduce off the shipped files.** §7.5 example 1 (forge a
      plant base at grade 2 — souls 80, substrate 8, catalyst 1) reproduces off the reference table
      exactly; ⚠ **no shipped recipe reproduces it verbatim**, and that is the band mechanism working
      rather than a mismatch — `recipe.004` authors `standard` (×2.000) where the example is the
      `modest` (×1.000) baseline, so the same row resolves to exactly 2×, asserted. §7.5 example 2
      (salvage a level-60 epic humanoid chest: 11 fine substrate, 2 fire, 1 dark, 2 shards, 2 temper)
      reproduces **line for line** on the ten-rung ladder, because `epic` anchors on `heirloom`
- [x] **`socket` costs ten souls and nothing else, at every rung — and the rule survives the author's
      band.** I9 §7.4 states it as a rule, not an illustration; the shipped `recipe.022` authors
      `soulsCostBand: "cheap"`, which would resolve it to **5**. The souls leg is `bandImmune` with
      the reason in the tuning file itself, and the test walks all ten rungs asserting a single line
      of exactly 10
- [x] **The upcycle cap is a BOUNDED RATIO and the file a balance pass edits says so.**
      `upcycle.capNote` carries "BOUNDED RATIO … not a ceiling on how much a player may earn", and
      the test asserts that string is present, not just that the number is 2. Raising
      `maxInputGrade` to the top grade is **refused at load** — upcycling into `prime` is the leak
      the cap closes (I9 §5.3), and it throws rather than clamping
- [x] ⭐ **The salvage coefficients are RE-DERIVED to ten rungs by a stated rule, and the derivation
      is re-computed in the test rather than transcribed.** I9's four-row table is keyed on the
      retired bands. The four anchors are **not chosen** — they are `LegacyDemonRarityIds.ForwardMap`,
      the shipped one-way band→rung map, so `common`→`chaff`, `rare`→`cultivated`, `epic`→`heirloom`,
      `legendary`→`sunwoven` land value for value (asserted against the live map, not a copy).
      Between anchors: integer linear interpolation with **floor**, never round-half-up, because
      rounding a salvage yield **up** is the only direction that can break R2. Above the top anchor:
      `substrateBase`/`shardBack` continue the last segment's slope, floored; `essenceCap` stays flat,
      because I9's own table already stopped it growing at epic. All thirty numbers are re-computed
      from the four anchors plus that rule and compared to the file
- [x] **R1 on the ten-rung ladder, and its bottom edge as data.** Salvage returns
      `shard.{rung − 1}`, never the item's own — asserted for all nine non-bottom rungs by id, not
      just by count. `chaff` returns none, and `MaterialTuning` **refuses a tuning** that gives the
      bottom rung a non-zero `shardBack`, so R1's edge cannot be edited away
- [x] **The two bottleneck classes have no faucet, proven over the whole input space.**
      `catalyst.forge` and `catalyst.flux` never appear in a yield at any rung × any enhancement ×
      any affix count; every catalyst line a salvage ever produces is `catalyst.temper`. Souls are
      never returned at all — not even as a zero line, because a zero line is a row that invites
      someone to make it non-zero
- [x] **The grade lock, and the D26 distinction it is easy to get wrong.** A level-10 zone returns
      `crude` across 2,000 salvages; a level-75 item returns `prime` immediately, with no counter and
      no cooldown between them. ⚠ That is **not** metering the player — it is the salvage output of a
      *low-level item* being low-level, a property of the target
- [x] ⭐ **The spend transaction, every property copied from a shipped path and each one tested.**
      `RpgStore.TrySpendRecipe` — replay returns the **original** outcome ref and spends nothing; a
      reused correlation with **different** arguments returns `correlation.mismatch` (compared against
      a SHA-256 digest of the resolved lines, so a different recipe *or* a different quantity is
      caught, not just a different total); a refusal writes nothing, so a retried refusal
      re-evaluates and succeeds once funded; the material legs use `RpgStore.Fusion.cs:395`'s
      conditional decrement verbatim; an unknown material id **throws** at the write boundary; and a
      forced throw from step 5 leaves **zero rows across all three stores** — materials unchanged,
      souls ledger unchanged, spend log empty
- [x] **Fixed class order is enforced at the write boundary, not only in the resolver.** A caller
      handing `TrySpendRecipe` lines out of the souls → shard → substrate → essence → catalyst order
      is refused with an `ArgumentException` naming the rule, so "two logs of one refusal are
      byte-comparable" is a fact about the store rather than about one call site
- [x] ⭐ **`salvage_yield` is UNBLOCKED, registered and seeded — the sixth `rarity_budget` key.**
      `ssot-rarity.md` §5 recorded it as "awaiting I9"; the decided shape is **one integer per rung,
      the substrate quantity a salvage of that rung returns before the affix bonus**. It satisfies
      §9.8's one constraint — *"must not reuse `shard.{DemonRarity}` ids"* — by naming **no shard id
      at all**: the shard leg is R1's derived rung−1 rule, not a per-rung budget row.
      `RpgStore.SeedSalvageYield` seeds all ten from `materials.v1.json` and is wired into
      `Program.cs` at boot, deliberately **separate** from `SeedRarityLadder` so module 7's own
      seeding never grows a dependency on a later module's tuning file. **Cross-referenced into P2.1
      below**
- [x] **No new member of the closed 33-code list.** `AtomRejectionReason` still has exactly 35 names,
      asserted. Every refusal this module raises is a namespaced `ContentRuleViolated{material.*}`
      under a `material` namespace registered through `ContentRuleNamespaces.Register`
- [x] **Two builds of the recipe catalog are byte-identical** (the fusion-catalog golden precedent) —
      an ordinal-sorted SHA-256 over every loaded recipe and cost line

**⛔ Five defects / spec-vs-code divergences found while building, all named rather than silently absorbed:**

1. ⛔ **R2 AS WRITTEN IS A MINT-SHAPED INVARIANT, and it is false for the six mutation verbs.**
   Measured, not argued: `recipe.012` (temper +0 → +1) spends **1** `substrate.humanoid.crude` and
   salvaging its output returns **2** — and no pricing fixes that, because 2 is `substrateBase[chaff]`,
   what the *item* was already worth, paid for by the drop and not by tempering. R2's own text ("for
   every class a recipe spends, salvaging that recipe's output returns strictly less of that class")
   silently assumes the recipe *minted* its output. **The property test therefore asserts the two forms
   that are actually true**, over the whole loadable table × ten rungs × six enhancement levels × three
   content levels: **mints** get R2 literally (per id, against a fresh-base salvage), and **mutations**
   get the **marginal** form — running the operation may never raise its output's salvage yield by more
   than it cost — backed by the **cumulative** strict form I9 §5.3 actually states
   (`Temper_returns_strictly_less_catalyst_than_enhancement_paid_in`, n = 1…30, all strict). 1,000+
   (recipe, material) pairs are checked in each half and the counts are asserted, so a test that
   quietly stopped checking anything fails.
2. ⛔ **R2 must be per material ID, not per class — and the class-level reading is measurably wrong.**
   `catalyst.forge` and `catalyst.temper` are non-fungible sinks, and I9 §5.3's own table is per id
   ("`catalyst.forge` → **never**"). Measured and pinned as its own test: boring a hole into a +12 item
   spends 1 `catalyst.forge` and its output salvages for 4 `catalyst.temper`, so the **class** sum
   rises 1 → 4 while **every per-id claim still holds** (nothing spent comes back). Recorded so a later
   session that "tightens" R2 to the class level knows which case it will hit and why the looser
   reading is the wrong one.
3. ⛔ ⭐ **A forge can be priced below its own salvage floor with one authored word, and nothing
   refused it — now closed at import.** The SC7 line ("adding a forge recipe is one row plus two or
   three cost rows and **no code**") means an author can build a substrate perpetual-motion machine
   without a review: `cheap` halves a grade-1 forge's 4 substrate to **2**, and salvaging the output
   returns the chaff floor of **2** — not strictly less. The shipped corpus happens not to contain one,
   so a property test over the shipped table alone would **never have seen it**. `MaterialRecipeCatalog`
   now runs the check at **load**, on every mint, against the same coefficients `SalvagePolicy` reads,
   and refuses by name (`ContentRuleViolated{material.strict-loss-violated}`) with the fix in the
   message. Two tests: the leaky recipe is refused, and the same recipe one band up is accepted — the
   guard refuses the leak, not the shape.
4. ⛔ **The shipped 30-recipe corpus is 40 % unresolvable, and each entry is refused BY NAME with the
   module that unblocks it** (module 11's pattern, kept). **18 load, 12 are refused:**
   **seven `reroll` recipes** (recipe.015/016/017/018/026/027/028) name a verb that predates the
   `reroll-one` / `reroll-all` split — **module 15** owns that split and the `op_kind` namespace it
   lives in, and inventing it here would mint a second vocabulary the Boundaries forbid outright; and
   **five `elevate` recipes** (recipe.009/010/011/025/029) name one of the four **retired band shard
   ids**, which resolve but are never minted, so they are recipes nothing can ever pay. Counts are
   asserted against the real file so a corpus change cannot quietly move them. ⚠ Two of the seven
   `reroll` recipes *also* carry a legacy shard, but a refusal names **one** reason — the verb, checked
   first.
5. ⛔ **`spec-salvage-craft.md`'s own re-issued cost table has a column shift on the `reroll-all` row.**
   It prints `b` `flux` in the **Essence** column and leaves **Catalyst** blank. I9 §7.4, the source,
   has essence `—` and catalyst `b flux`. The source wins; the reason is written into that row's own
   `note` in `materials.v1.json`, where a balance pass reads it.
   ⚠ **And the spec's `rpg_demon_materials` site count does not match its own table.** It says *"nine
   SQL sites across five files"*; the table below it lists **eight** lines in **four** files, and a
   fresh repo-wide grep confirms **eight SQL sites in four files** plus three doc-comment mentions
   (eleven occurrences total). The reset site is `RpgStore.cs:714`, not `:697`. Corrected list below.

**Two decisions this module had to make that the spec does not state, both named:**

- ⭐ **A missing `soulsCostBand` means the recipe authors NO souls leg — the corpus wins over the
  reference table.** Four of the thirty recipes (all `upcycle`) omit it, and `KindCatalog` already
  marks the field optional. I9 §7.4 prices upcycle at `20 × g` souls; the reference row stays in the
  tuning for modules 15/16 to price against, but a recipe that authors no band gets no leg, because
  defaulting one to `modest` would invent a price no author wrote. Asserted by the resolve tests over
  the whole table.
- ⚠ **The authored band scales the upcycle ratio too, so two shipped recipes convert at 10:1 rather
  than the reference 5:1.** `recipe.006` and `recipe.008` author `standard` (×2.000) on their
  substrate line, so `inputPerOutput: 5` resolves to 10 for those two. That is the band mechanism
  working as designed — an author choosing `standard` means "twice the reference" — but a balance pass
  reading `5` in the tuning file and expecting every recipe to convert at 5:1 would be surprised, so
  it is written down here. Not a defect: the reference row and the per-recipe band are two different
  decisions on purpose, and the drain-valve guarantee (more in than out) holds at every band.
- ⭐ **A recipe prices on the grade its OWN substrate line names**, falling back to the target's item
  level only when it has no substrate line. That keeps the grade a property of the thing being made —
  a `crude` forge is a grade-1 forge whatever the target's level — rather than letting a high-level
  context silently reprice a low-grade recipe. It is also what makes the upcycle cap checkable at
  resolve time.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.MaterialVocabularyTests\|FullyQualifiedName~Items.MaterialCorpusTests\|FullyQualifiedName~Items.SalvagePolicyTests"` | **48 passed** (new — `MaterialVocabularyTests` 12, `MaterialCorpusTests` 20, `SalvagePolicyTests` 16). The filtered run measured **47** before the last fact (`Upcycles_own_strict_loss_is_its_conversion_ratio`) was added at 23:46; all 48 are inside the fully-green 6308-test full-suite run below, which started at 23:59 — so every one of them is verified, the aggregate just came from the full run rather than a re-filtered one |
| `dotnet test tests\FusionRpg.Data.Tests --filter MaterialSpendTests` | **12 passed** (new) |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors across 120 partitions — identical to the module-6/8/11/12/13 baseline.** Zero new findings |
| `dotnet run --project tools\AtomImporter -- --check --validate` | **clean** — 17 files, 66 atoms, 7 containers, 10 rarity bands, catalog revision 2, byte-identical to P3.3's snapshot |
| `python scripts\audit-overflow.py` | **0 critical**, 57 findings total, **zero** under `Items/Materials/` |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0**; `materials` no longer appears in the table |
| `.\scripts\guard-dal.ps1` / `guard-single-writer.ps1` / `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` | **all four OK** |
| `dotnet build src\FusionRpg.Server` | **0 errors** — boot parses `materials.v1.json`, seeds `salvage_yield`, imports the recipe corpus and prints every refusal |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | **198 / 198**, unchanged from the session-start baseline |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | ⭐ **723 passed / 0 failed** — fully green. ⚠ A run mid-build showed **4** failures, all `UniqueActorStoreTests.Equipment_*`; they were ruled out as this module's by **ownership rather than by name** (`git diff src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` is a 47-line `CutoverUniqueEquipmentModsAbsorption` addition plus a `double`→`long` `Xp` read, cites `spec-mods-absorption.md`, and mentions `material`/`salvage`/`recipe` **zero** times) and the concurrent stream cleared them before this final run |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | ⭐ **6308 passed / 0 failed** — fully green, including this module's 48 and module 7's moved `salvage_yield` row. The 13-failure baseline measured at the start of this module is **gone**, cleared upstream by the concurrent stream mid-build |

⚠ **The baseline was re-measured fresh at the start of this module rather than inherited, and it
moved in both directions during the build.** At session start: `Core` **13 failed / 6215 passed**
(4 `Atoms.UiPresentTests`, 7 `World.Loam.*`, `AtomCatalogSsotDriftTests`, `AtomCompilerTests` — all the
concurrent stream's), `Data` **0 failed / 711 passed** (host crash after), `Guard` **198 / 198**,
`ItemSeedValidator` **165**. Mid-build the concurrent stream cleared all 13 Core failures and then
introduced a repo-wide `double`→`long` migration plus a `mods-absorption` cutover that took `Data` to
4. **Compare against the numbers in each row below, not against an earlier module's snapshot.**

⚠ **One shipped guard caught a real defect in this module's first draft, which is the guard working.**
`SalvagePolicy` computed R1's rung−1 as `DemonRarityLadder.RungsBelow((DemonRarity)item.RungIndex, 1)`,
and `Guard.Tests DemonRarityLadderGuardTests.No_bare_cast_between_int_and_DemonRarity_outside_the_ladder_helper`
went red on it — the bare cast is exactly the form that silently changed meaning the day the enum
widened from four values to ten. Rewritten as `OneRungBelow(DemonRarityLadder.All[item.RungIndex])`,
which indexes the ladder's own ordered list and has no cast at all. Guard back to **198/198**.

⚠ **Three transient build breaks from the concurrent stream, all resolved by retry and none in a file
this module touched** — `StructureCatalog.cs`/`LoamPolicy` (mid-edit, `data/tuning/loam.v3.json`
untracked), `ContractTuningTestBootstrap.cs` vs a widened `LoamStructuresTuning` record, and
`RpgProgression.cs`'s `CS0266`. Also hit `MSB3027` on `FusionRpg.Core.dll` locked by another
`testhost` several times. Same pattern P3.1 recorded.

⚠ **One test in module 7's own suite had to move, and it is this module's change that moved it.**
`RarityBudgetKeysTests.A_key_awaiting_a_decided_shape_is_not_registered_yet` pinned `salvage_yield` as
**unregistered**. It moved to the ready set (renamed `The_ready_keys_are_registered`) rather than being
loosened — the three keys that *are* still awaiting (`socket_min`, `socket_max`, `reroll_cost_mult`)
stay pinned exactly as hard, and `MaterialSpendTests` re-asserts at the DAL that writing one still
throws.

- [ ] ⏸ **The `rpg_demon_materials` → `rpg_materials` rename is RULED but deliberately NOT in this
      module's task list**, exactly as the spec's Success criteria require. This module ships against
      the shipped name. ⛔ **The site list drifted again during this same build — re-measured
      2026-09-05: ELEVEN SQL sites in FIVE files, not the eight-in-four this note claimed on
      2026-09-04 (itself a correction of the spec's stale "nine across five"):**
      `src/FusionRpg.Data/Sqlite/RpgStore.cs` **575** (DDL, was `:573`), **754** (reset, was `:714`,
      itself corrected from the spec's `:697`); `RpgStore.Expeditions.cs` **233**, **253** (was `232`,
      `252`); `RpgStore.Fusion.cs` **395** (unchanged); `Migrations/ShardRungs.cs` **48**, **71**, **89**
      (unchanged; doc-comment mentions at `11`, `16`, `18`); and **this module's own new file**,
      `RpgStore.Materials.cs` **153**, **175**, **293** (doc-comment mentions at `18`, `19`) — omitted
      from the prior count even though this same P4.1 entry's "files touched" list (below) names
      `RpgStore.Materials.cs` as built here. `src/FusionRpg.Data/` remains the complete boundary —
      nothing outside it references the table. Recorded for the day the owner says go.
- [ ] ⏸ ⛔ **The shipped materials DISPLAY corpus is 21 rows for a 27-id vocabulary, and its four
      shard rows point at ids that are never minted.** `data/seed/items/materials/materials.json`
      authors `shard.common` / `rare` / `epic` / `legendary` — the retired band ids — and **zero** of
      the ten `shard.{rung}` ids that actually ship, so six-plus shards would render with no name and
      no icon. Everything that is not a shard row is already correct and issuable, which is what makes
      this a re-author of ten rows rather than a corpus rebuild. Measured and pinned by
      `The_shipped_materials_display_corpus_is_measured_not_assumed` so it cannot quietly change size.
      **Not fixed here** because it is a stage-1a *generated* seed file whose ids are allocated by
      `NamespaceAllocation`, and because the four legacy rows' retirement is bound up with the
      "resolvable for one release" window `spec-rarity-migration.md` §4 point 4 owns — the same
      four-things-move-together shape P3.3 recorded for the resonance ids. **Owner: this module, as a
      corpus re-author; the presentation consumer is module 20 `item-surfaces`.**
- [x] ⭐ **RESOLVED IN PART 2026-09-05 by module 15 — the corpus is 23 of 30 resolvable, up from 18.**
      The seven `reroll` rows were re-authored against the split verb the moment module 15 minted the
      `op_kind` namespace it lives in: `recipe.015/016/026/027/028` → `reroll-one` and
      `recipe.017/018` → `reroll-all`, read off each row's own `nameKey`. **No recipe is refused on
      the verb any longer** and `MaterialCorpusTests` asserts `Assert.Empty(verbRefusals)`.
      ⛔ **The split also made a second, pre-existing defect visible on two of those rows:** this
      section already recorded that *"two `reroll` recipes also carry a legacy shard, but a refusal
      names ONE reason — the verb, checked first."* With the verb fixed, `recipe.017`'s `shard.rare`
      and `recipe.018`'s `shard.epic` surface their own refusal, so the legacy-shard count moves
      **5 → 7** and the resolvable corpus moves **18 → 23, not 25**. That is the same corpus re-author
      the ten missing shard display rows need — **still this module's**, still unscheduled, and now
      with two more rows on its list.
- [ ] ⏸ **`a_t5_affix_costs_more_than_a_t1_at_every_theta` is asserted on the RUNG axis, not the tier
      axis, and the reason is a real gap rather than a shortcut.** All ten rows of I9 §7.4's reference
      table are keyed on rung, grade or enhancement — **not one leg reads tier**. Tier enters pricing
      only through `qty_curve_id` → `effect_curve`, whose `CurveInput` is exactly `{ Level, Rarity,
      Tier }` (verified in `CurveTable.cs:4-9`, as the spec claims) and which **no shipped recipe
      authors**. So D26's positive half is asserted where the shipped table actually prices — cost
      rises strictly with the target across all ten rungs, and Θ is not an input anywhere — and the
      tier half waits on **module 15**, which owns the per-affix operations that would price on it.
      The `material_recipe.qty_curve_id` column ships so the seam exists.
- [ ] ⏸ **Step 5 (`perform`) is an injected delegate, not yet wired to a production mutation.**
      `TrySpendRecipe` runs the owning module's mutation inside the same transaction and the tests
      exercise the seam (including the forced-throw rollback), but the mints and mutations themselves
      belong to modules 14's own forge executor and 15/16 — a **wiring gap** with named owners, not a
      wall. Nothing in this module's scope produces an `effect_container` instance yet, because a
      per-base-type item container is a thing no module has authored.
- [ ] ⏸ **No `forge-gem` or `imbue` recipe exists to author against.** Both have priced reference rows
      and both are in the operation vocabulary; neither has a content row, because gems are
      **module 16**'s and D24's `socket-imbue` `op_kind` is **module 15**'s. The rows exist so those
      modules price against a fixed vocabulary rather than a moving one, which is this module's whole
      stated purpose.
- [ ] ⏸ **A sixth spend class, a fourth catalyst, and a new operation verb all stay ask-first** — the
      Boundaries list, unchanged. `MaterialClass` has five members and `CatalystVerbs` three, both
      asserted closed.

**Files:** `data/tuning/materials.v1.json` (new — the ten-operation reference cost table, the ten-rung
salvage coefficients with their derivation, the grade function, the upcycle bounded ratio, the mirrored
cost-band multipliers); `src/FusionRpg.Core/Items/Materials/{MaterialCatalog.cs, CostClassMatrix.cs,
MaterialTuning.cs, MaterialRecipeCatalog.cs, SalvagePolicy.cs}` (new);
`src/FusionRpg.Core/Items/RarityBudgetKeys.cs` (EDIT — `salvage_yield` → `HasDecidedShape: true`);
`src/FusionRpg.Data/Sqlite/RpgStore.Materials.cs` (new — the three tables, `ImportRecipeCatalog`,
`TrySpendRecipe`, `GrantMaterials`, `SeedSalvageYield`); `src/FusionRpg.Data/Sqlite/RpgStore.cs`
(EDIT — `EnsureMaterialSchemaUnlocked` in `Init`); `src/FusionRpg.Server/Program.cs` (EDIT — parses the
tuning at boot, seeds `salvage_yield`, imports the recipe corpus and prints every refusal);
`tests/FusionRpg.Core.Tests/Items/{MaterialVocabularyTests.cs, MaterialCorpusTests.cs,
SalvagePolicyTests.cs}`, `tests/FusionRpg.Data.Tests/Items/MaterialSpendTests.cs` (new);
`tests/FusionRpg.Core.Tests/Items/RarityBudgetKeysTests.cs` (EDIT — `salvage_yield` moves to the ready set).

⚠ **One deviation from the spec's Project structure, stated rather than silent:** the five Core files
live under `src/FusionRpg.Core/Items/Materials/` rather than flat in `Items/`, matching what modules
10/11/12 already did (`Display/`, `Drops/`, `Thresholds/`). Same files, same names.

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.MaterialVocabularyTests|FullyQualifiedName~Items.MaterialCorpusTests|FullyQualifiedName~Items.SalvagePolicyTests"`; `dotnet test tests\FusionRpg.Data.Tests --filter MaterialSpendTests`; `dotnet run --project tools\ItemSeedValidator`

### ✅ P4.2 — Module 15 `enhance-reroll` — BUILT AND VERIFIED 2026-09-05 (the `Mixed`-affix reroll **now BUILT** — see the 2026-09-05 addendum — the workbench executor and module 1's two §9 defects explicitly handled)

⛔ **The four things module 14 filed here are all answered.** Each is resolved or carried with its
reason, in the order P4.1 filed them:

1. ⭐ **The `reroll-one` / `reroll-all` split landed, and the seven shipped recipes were re-authored
   against it.** Module 14 could not invent the verb (its Boundaries forbid defining an `op_kind`
   outside `ssot-enhancement.md` §5.3); this module owns that namespace, so it made the call from each
   row's own `nameKey`: `recipe.015/016/026/027/028` → **`reroll-one`** (`reroll-single-common`,
   `reroll-single-elemental`, `reroll-essence-fire|dark|air`) and `recipe.017/018` → **`reroll-all`**
   (`reroll-all-rare`, `reroll-all-epic`). **Not one recipe is refused on the verb any more** and
   `MaterialCorpusTests` asserts `Assert.Empty(verbRefusals)` rather than the old count of 7.
2. ⭐ **`socket-imbue` exists in the `op_kind` namespace before module 16 needs it.** D24's operation
   was priced by module 14 (`CraftOperation.Imbue`, `bore`'s curve verbatim) with **no** `op_kind`;
   minting one in module 16 would fork the namespace, so it is minted here as
   `MutationOpKind.SocketImbue` → `"socket-imbue"`, alongside `socket-add`/`socket-insert`/
   `socket-remove`. The namespace is a closed ten and a test pins the list.
3. ⭐ **`reroll_cost_mult` has a decided shape and is registered.** Priced against module 14's
   published vocabulary, not re-derived. **The shape:** the `rarity_budget` integer is the **rung
   leg** (`1000 + rerollCostRungSlopeMilli × rungIndex` — `chaff` 1000 … `almanac` 2980), and
   `ssot-rarity.md` §9.7's *"must scale with **affix count**, not rung alone"* is met by a **second
   leg that is deliberately not a per-rung row**: `affixBase + affixStep × affixCount`. The total is
   `rungLeg × affixLeg / 1000` — widened first, divided by 1000 once, at the end.
   ⭐ **And the §9.7 constraint is enforced at LOAD, not left as a comment:**
   `EnhancementTuning.Parse` refuses a document whose affix leg does not out-spread the rung leg
   (×4.00 against ×2.98 as shipped), because a rung-dominant price inverts `ssot-rarity.md` §8.1's
   *"low rungs are the best crafting bases"* mechanism — *"cheap to own and expensive to use, and the
   mechanism inverts"* is its own wording.
4. ⏸ **`a_t5_affix_costs_more_than_a_t1` is still unassertable, and this module did not make it
   assertable.** Carried forward with the same evidence: none of I9 §7.4's ten reference rows reads
   tier, and this module prices reroll on **rung × affix count**, which is what §9.7 asked for — not
   on tier. The tier axis still enters only through `qty_curve_id` → `CurveInput.Tier`, which no
   shipped recipe authors. **Owner: whoever authors the first tier-keyed `qty_curve_id` row**; the
   column ships, so the seam exists.

**What was built:**

- [x] **I6 + I7 under one mutation contract — D2 §9 adopted verbatim, not re-derived.** `MutationOp`
      (the closed ten-member `op_kind` namespace, the `MutationResult` delta record, `MutationLimits`),
      `MutationReplay` (the transcript law), `MutationCanonical` (the `result_json` canonical form and
      the `state_hash`), plus the DAL half in `RpgStore.InstanceOps.cs`: `effect_instance_op` with
      `UNIQUE(instance_id, correlation_id)`, the five head columns
      (`enhance_level`, `enhance_pity_counter`, `mutation_seq`, `state_hash`, `origin_values_json`)
      and `effect_instance_atom.suppressed`. ⚠ **`origin_catalog_revision` was NOT added** — it already
      exists as `effect_instance.catalog_revision` and D2 §7.1 granted it as a semantic lock;
      I6 §5.1's request for a new column stays refused
- [x] ⭐ **Clause 4 is enforced by the TYPE, not by a comment.** Every method on `MutationReplay` takes
      an origin head and a list of ops **and nothing else** — there is no parameter through which a
      tuning, a catalog, a container or an RNG could reach it, so a re-simulating replay is not
      expressible. `Replay_never_reads_the_rules_table` asserts it by reflection over the real
      signatures, and `A_rebalance_of_the_odds_table_changes_no_owned_item` shows the head is
      byte-identical across a wrecked tuning
- [x] **D7 — cost, never luck, on all three of its named mechanisms.** Material cost was module 14's
      (built); the success chance is §4's three bands, read from tuning; the **mandatory** bad-luck
      protection is `CraftPityCounter`. ⭐ **The odds never reach zero at any level** —
      `The_success_curve_never_reaches_zero_at_any_level` walks +1…+5000, and the loader refuses a
      `successEndMilli` of 0 by name, quoting D7
- [x] ⭐ **The craft-pity resolution, implemented exactly as §5 decided it — the guarantee is not a
      draw.** Below the threshold the container's weighted tier draw runs and its answer is used
      **unmodified**; at the threshold the draw delegate **is never called at all** and the tier is
      *placed* at `max_tier`. `Craft_pity_shifts_no_draw_weight` proves it by counting delegate
      invocations, so `ssot-rarity.md` §3.5's measured overlap invariant (2×10⁵ rolls/rung, seed
      `20260822`) is untouched. D31 (§3.8 scoped to *drop* pity) had already landed as module 7's E1 —
      re-verified in the shipped doc, not assumed
- [x] **The `enhance_cap` shrinking soft cap, consuming module 7's seeded column.**
      `EnhancePolicy.GainMicro(n, cap) = cap × 1000 × n / (n + K)`, `K = 8` from
      `data/tuning/enhancement.v1.json`. `No_enhancement_gain_is_a_hard_stop` runs **every rung × every
      n to 4096** and `Enhancement_gain_stays_below_its_rungs_asymptote_at_every_n` pairs with module
      7's `Enhance_cap_asymptotes_below_one_rung_step_at_every_rung` — neither spec can move without
      the other going red, which is the property the previous arrangement lacked
- [x] ⭐ **The curve is compared EXACTLY, not through a rounded render.**
      `GainIsStrictlyIncreasing` cross-multiplies in `long` (`cap·a·(b+K) < cap·b·(a+K)`), so the
      answer is the mathematical one at every `n`. This was a real correctness call, not a flourish:
      a per-mille render of the same curve **ties** above `n ≈ 1265` under integer division, and a tie
      reads exactly like the hard stop the test exists to forbid. Micro is the canonical unit for the
      same reason
- [x] ⚠ **`pool_rolls` does not exist, and the algebra is restated per budget.** `BudgetTargets`
      carries `PrefixRolls`/`SuffixRolls` and their two target counts;
      `ANCHOR_MULT = 2^(K_prefix + K_suffix)`, and it **throws** rather than saturating past 2^63.
      `RetainedGroups` seeds each budget's exclusion set from *that budget's* retained affixes, and
      `ValidatePostOp` restates the post-op invariant per budget. Proven both ways the success
      criterion asks for: by test, and by a test that greps the module's own non-comment source for
      `PoolRolls`
- [x] ⚠ **The `Mixed` hazard was DECIDED, not discovered — and it is now BUILT.** A `Mixed` affix
      consumes a prefix roll **and** a suffix roll simultaneously, and `Instantiator.Draw`'s own
      comment called its two-independent-draws model *"an interim, honestly-documented
      simplification"*. This module refused to build a second one on top of it: a reroll targeting a
      `Mixed` affix was refused `ContentRuleViolated{reroll.mixed-affix-undefined}` naming module 2
      (`resolution-order`). ✅ **Superseded 2026-09-05** — module 2 had already landed, so this module
      threaded its A1 semantics into `Instantiator.DrawBudget` itself and deleted the refusal. See the
      addendum below
- [x] **Transfer ships** (§6a) — both `op_kind`s, the 700‰ ratio and the ±8 window in tuning, role
      equality on module 3's stable id, the donor emptied to `+0`, the grant clamped to the
      *recipient's own* item-level cap, and a hybrid frame refused by name until module 3 settles
      hybrid role ids (I6 §9 #7). ⭐ **A lossless ratio is refused at load**, quoting I6's own reason
- [x] ⛔ **`CraftingHorizonReport` ships, and it reproduces §4b's whole table from the shipped
      `power-scale.v2.json` rather than from a number in a doc.** Θ′ is solved by exact integer
      interpolation between the two bracketing Θ on the ladder's own per-mille values — no floating
      point, and no bracket-and-report-the-integer, which would round N = 0.19 to 0. All seven rows
      match to the last digit, `FirstThetaReachingRealms(2 realms)` **computes** Θc = 123, and
      `The_horizon_moves_when_the_power_dial_moves` shows the figure tracks `bMilli`
- [x] ⛔ **N ≈ 0.19 is recorded in code, with its consequence.** The tuning file's own
      `craftPityNote` says the threshold is `rpg_summon_pity`'s shipped 25 **reused verbatim and
      deliberately not sized as a progression choice**, and cites §4b for why there is nothing to size
      it against at v1 depth. §4a's soft cap makes it smaller on purpose: ×1.12 → N = 0.09 at v1's
      reachable +12 on an `almanac`, and N ≤ 0.16 at *any* n — asserted, both of them
- [x] **The one module-9 read is used, and it is the only one.** `MutationPreview` calls
      `ItemPowerReads.CardPower` (R3) for the before/after figure and nothing else; a test walks the
      module's own source and fails if it declares a `PowerVector`, a `PowerScalar` or a second
      pricer. `showPowerOnCard: false` suppresses **both** halves of the preview, so G3 §10 Q7's
      reversal is a whole reversal
- [x] **`mutation_seq ≤ 4096` is the one legal ceiling and it says so.** Structural — it bounds a
      retry loop and a log's length, not how strong an item may become — and it **throws** on the
      append path rather than clamping. `Mutation_seq_is_capped_at_4096_and_the_comment_says_it_is_structural`
      asserts the comment text as well as the number
- [x] **D2 clauses 5 and 11 have their columns, not just their comments.** `effect_instance_op`
      carries `catalog_revision` and `rules_version` — <b>the op's own</b>, stamped per row, never
      `effect_instance.catalog_revision` which stays origin-only — and `cost_json`, clause 11's record
      of the spend in module 14's vocabulary (*"a spent cost with no op is theft; an op with no cost is
      duplication"*). ⚠ **Caught by reading D2 §9's fifteen clauses one at a time against the
      implementation rather than trusting a summary** — the first draft had the ledger, the replay law
      and the idempotency and would have shipped clauses 5 and 11 as prose. Also fixed there:
      definitions §8's `N:` NULL marker, which the canonical form now honours even though no head field
      is nullable today, so a nullable column added later cannot be silently encoded as an empty string
- [x] **D26 holds on every input.** `EnhanceContext` has nowhere to *put* a player property, and the
      test asserts that by walking its property names — the same guard shape module 14 used on
      `RecipeContext`
- [x] **Module 1's two §9 defects: verified CLOSED before the first operation shipped.** Both were
      fixed in P1.1 and re-checked here rather than assumed: the orphan sweep now needs
      `NOT HasBinding AND NOT HasOwner` (two reachability roots, so unequipping no longer deletes the
      item), and D9's strict `catalog_revision` equality is gone, replaced by the per-atom
      `AtomIdentityDigest` test. Nothing in this module had to ship over a live defect

**Two decisions this module had to make that the spec does not state, both named:**

- ⭐ **Milestones are a STRIDE, not a five-entry list.** I6 authors them at +4/+8/+12/+16/+20. A
      five-entry list is a hard stop at +20 wearing content's clothes — the exact shape AGENTS.md
      forbids and the one §4a spent a whole section removing from `enhance_cap`. Shipped as
      `milestoneStride: 4`, so +24 and +400 are milestones too, and the test says so.
- ⭐ **A natural `max_tier` roll resets the pity counter, not only a guaranteed one.** The counter
      exists to guarantee `max_tier`; continuing to count toward a guarantee of something the player
      just rolled would be a counter that means nothing. Stated here because the spec's own code-style
      block resets only on the guarantee.

⛔ **One real defect found in an earlier module's output, and fixed:**

- **`data/tuning/item-rarity.v1.json` carried `enhanceCapAsymptoteK: 8`, which nothing read.** Module
  7 (P2.1) authored it alongside `enhanceCapStepMarginAlphaMilli`, but `ItemRarityTuning.Parse` never
  reads it and neither did any test — while the spec is explicit that *"module 7 owns the column; this
  module owns `K`"*. Two files holding the same dial, one of them inert, is a balance pass editing a
  number with no effect. **Removed from `item-rarity.v1.json` and replaced with a note pointing at
  `enhancement.v1.json`'s `asymptoteK`, which is the live one.** Cross-referenced into P2.1 above.

⛔ **The power guard caught this module's own curve, and the fix was to REGISTER it, not to rename
around it.** `guard-power.ps1` failed G2/G3 on `EnhancePolicy.GainMicro` and `LinearGainMilli` —
*"private `f(level)`-shaped method outside Core/Power"* and *"not listed in `inventory.json`"*. That is
the guard working: AGENTS.md's one-power-ladder rule says a scale not in `ssot-power-scale.md` §10's
inventory *"does not have permission to exist yet"*. Renaming the parameter would have dodged the check
and left the scale undeclared, so instead it is now **§10.2 row 24**, with `inventory.json` rows 24/25
and `EnhancePolicy.cs` on the G2 allowlist beside `PatronPolicy.cs`. **The standing is row 16's,
verbatim:** the input is the *item's own* `+n`, a per-item counter, never a character or content level;
the curve is bounded by an asymptote it never reaches; and everything Θ-shaped in this module reads the
shared `PowerLadder` (`CraftingHorizonReport`) rather than a private `f(Θ)`. Guard green afterwards.

⚠ **Two magic-number findings in this module's own first draft, both fixed rather than filed:**
`RerollPolicy.cs`'s bare `63` in the anchor-overflow guard is now `const int MaxAnchorExponent = 62`
with a comment saying it is `long`'s width and not a balance dial; and `CraftingHorizonReport`'s
`V1ThetaContent`/`V1ItemLevel` consts are **gone entirely** — Θc is read from the power curve's own
`pinIndex` (so v1's reach cannot drift from the curve it is measured against) and the item level is the
caller's, because it is D4's content decision. `--targets M1` reports nothing in this module now.

⛔ **One defect the reroll split made VISIBLE (it is not new, and it is not this module's to fix):**

- **`recipe.017` and `recipe.018` name a retired band shard** (`shard.rare`, `shard.epic`). P4.1
  already recorded that *"two `reroll` recipes also carry a legacy shard, but a refusal names ONE
  reason — the verb, checked first."* With the verb fixed, the second reason surfaces, so the
  legacy-shard refusal count moves **5 → 7** and the resolvable corpus moves **18 → 23**, not to 25.
  `MaterialCorpusTests` was updated to the new counts with the reason written next to them. **Owner:
  module 14's own deferred corpus re-author** (the same one the ten missing shard display rows need);
  cross-referenced into P4.1 above.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "…EnhancePolicyTests\|…RerollPolicyTests\|…MutationReplayTests\|…RarityBudgetKeysTests\|…MaterialCorpusTests\|…MaterialVocabularyTests"` | **107 passed / 0 failed** — **62 new here** (`EnhancePolicyTests` 25, `RerollPolicyTests` 23, `MutationReplayTests` 14) plus module 7's `RarityBudgetKeysTests` and module 14's `MaterialCorpusTests`/`MaterialVocabularyTests`, re-run in the same filter because this module moved three of their assertions |
| `dotnet test tests\FusionRpg.Data.Tests --filter "…InstanceOpTests\|…MaterialSpendTests"` | **24 passed / 0 failed** — `InstanceOpTests` 12 (new) + module 14's `MaterialSpendTests` 12, re-run because this module moved its awaiting-key list |
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items."` | **409 passed / 0 failed** — the WHOLE item program's Core suite, modules 1-15, green together |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6425 passed / 14 failed** — **zero in `Items.*`** (grepped, count 0). All 14 are `Battle.*`, `ClassSystem.*` and `Expeditions.*`: the concurrent battle-tempo/class-system stream's in-flight work, confirmed against `git status` showing `src/FusionRpg.Core/Battle/*`, `RpgStore.Expeditions.cs` and the class-system tuning mid-edit — none touched by this module |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **734 passed / 2 failed** — **zero in `Items.*`** (grepped, count 0). Both are `WorldWaveOneAcceptanceTests` (the twenty-turn scenario golden and its verb-coverage check), the concurrent world/battle-tempo stream's, confirmed against `git status` showing `RpgStore.World.cs`, `ClaimResolver.cs` and `LaneCost.cs` mid-edit. ⚠ **The session's own opening baseline for this suite was unusable** — it read `723 passed / 0 failed` but the run ABORTED on a test-host crash, so it never finished; this run completed |
| `dotnet test tests\FusionRpg.Guard.Tests` | **202 passed / 0 failed — fully green.** Better than this session's own start-of-run baseline (201/1, `ClassSystemBaselineRegenTests`, since fixed by the concurrent stream). The one guard failure this module DID cause, `PowerGuardTests`, is the ⛔ finding below and was fixed, not filed |
| `dotnet run --project tools\ItemSeedValidator` | **165 errors across 120 partitions — identical to the module-6/8/11/12/13/14 baseline.** Zero new findings, and the seven re-authored recipe rows moved none of them |
| `dotnet run --project tools\AtomImporter -- --check --validate` | **`--check: clean, and nothing would change`** — this module authors no atom or container content, so the catalog is untouched |
| `.\scripts\guard-dal.ps1` · `guard-single-writer` · `guard-secondary-no-unity` · `guard-funnel-delta` | all four **OK** |
| `.\scripts\guard-power.ps1` | **OK** after this module registered its curve — see the ⛔ power-guard finding above |
| `python scripts/audit-overflow.py` | **0 critical**, 57 findings, none in `Items/Mutation/` |
| `python scripts/audit-magic-numbers.py --targets M1` | **0 in this module** after two fixes — see the ⚠ magic-number note above |

**Deferred, with owners named:**

- [ ] ⏸ **No workbench executor — the operations are decided here and performed by a caller that does
      not exist yet.** `EnhancePolicy.Resolve`, `RerollPolicy.*`, `TransferPolicy.Resolve` and
      `CraftPityCounter` are pure decisions; `RpgStore.AppendMutationOp` commits one. What is missing
      is the thing that calls the decision, spends module 14's materials through `TrySpendRecipe`'s
      `perform` delegate and writes the head — the same **wiring gap** P4.1 named at its own step 5,
      with the same owner. Nothing here is inert by design: every piece has a test driving it, and the
      seam module 14 built is the one it plugs into.
- [x] ✅ **A reroll calls `Instantiator.DrawBudget` with a count and an exclusion set, and the `Mixed`
      refusal is gone with its reason. RESOLVED 2026-09-05 — see the addendum below.**
      **Before:** the spec's one behavioural ask of the instantiator (`count` and `excludeGroups` on
      `DrawBudget`) was *not* made here, and `ContentRuleViolated{reroll.mixed-affix-undefined}`
      refused every `Mixed` reroll, naming module 2 (`resolution-order`) — tracked as a
      **cross-program blocker**.
      **After:** both parameters exist, `Resolver`'s A1 `Mixed` semantics are threaded into
      `DrawBudget` itself, and the refusal is deleted — a **same-module wiring gap, closed in the
      module that owned it.** The residual is named and narrowed rather than left implicit.
- [ ] ⏸ **`Restore` is in the namespace and has no implementation.** It is an administrative rollback
      to a recorded `op_seq`; the ledger it needs is built and dense, so it is a small addition, but no
      surface asks for it and shipping an untriggered rollback path is how a destructive operation
      reaches production untested. **Owner: this module, when an admin surface exists (module 20).**
      ⚠ **Checked 2026-09-05 and module 20 is NOT that surface.** Its server file is read-only by
      construction — it carries no `MapPost` at all, deliberately, because a write path through the
      presentation layer is the "second surface" that module exists to prevent — and an admin console
      is not one of its six player surfaces. So this stays open with the same owner and a corrected
      trigger: **an admin surface, which nothing in the item program schedules.** See P5.4
- [ ] ⏸ **The milestone ATOMS are not authored.** The stride is decided and tested; the reserved family
      space no affix pool may draw from is content, and no `affix-families/*.json` entry declares one.
      **Owner: the authoring fleet, same lane as the phantom families P2.2/P2.3 named.**
- [ ] ⏸ **No endpoint, no wire DTO, no UI.** Consistent with modules 2/4/5/10–14: the item program's
      server surface is **module 20 `item-surfaces`**, and adding an ad-hoc endpoint here would be the
      second surface that module exists to prevent.

---

#### ⭐ Addendum 2026-09-05 — the `Mixed`-affix reroll is BUILT, and `reroll.mixed-affix-undefined` is deleted with its reason

**Why this was reopened.** A rigor pass re-checked this module's one tracked *"blocked on another
program"* claim against real code and found it stale: the refusal named module 2
(`resolution-order`) as the blocker, and that module **had already landed 2026-09-02**
(`Resolver.cs` + `ResolverTests.cs`), with module 4's `InstanceProducer.Compose` consuming its
`Mixed` semantics the same day. What had *not* happened is the last hop —
`Instantiator.Draw`/`DrawBudget`, the atom-id entry point a reroll actually redraws through, was
deliberately left on the old two-independent-draws model. **A same-module wiring gap wearing a
cross-program label**, which is exactly the mis-frame `CLAUDE.md`'s RPG-layer rule exists to catch.

**Before → after, stated plainly:**

| | Before | After |
|---|---|---|
| `Mixed` budget accounting | two **independent** draws; a `Mixed` affix could be picked in one pass, both, or neither | one pass carries the paired budget: a `Mixed` pick spends **one prefix roll AND one suffix roll simultaneously**, is never drawn twice, and is ineligible once the paired budget is spent |
| `DrawBudget` surface | `private static void`, whole-budget only, no exclusions | `public static BudgetDraw`, with the spec's **`count`** and **`excludeGroups`** (§2's one behavioural ask), plus the `crossBudget` / `excludeAffixIds` state A1 needs |
| Multi-ref bundles | `ExpandSingleRefAffix` threw for **any** bundle with >1 ref | `ExpandConcreteRefs` expands every concrete ref in `seq` order. ⚠ **Not a widening for its own sake:** `AffixValidator` derives `Mixed` only from refs of two different kinds, so a `Mixed` affix is multi-ref *by construction* — without this the new semantics were unreachable through `Draw` |
| Reroll refusal | `ContentRuleViolated{reroll.mixed-affix-undefined}` on every `Mixed` target, gated by a `resolutionOrderLanded` bool parameter | **deleted.** `ValidateRerollable(targets, lookupAffix)` now refuses `reroll.slot-affix-undefined` instead |
| Target counting | caller had to remember that a `Mixed` target frees a slot in *both* budgets — nothing enforced it | `RerollPolicy.TargetsFor(container, drawn, targetSeqs)` derives `BudgetTargets`, counting a `Mixed` target in **both** |

⛔ **The residual is narrowed, not hand-waved — and it is deliberately class-agnostic.**
`Instantiator.DrawBudget` returns bare atom ids and rolls no domain member, tier or value, so it
cannot redraw into a **slot-bearing** pool; `Resolver.Resolve` can, but has no
`count`/`excludeGroups` seam for a partial redraw. A slot-bearing **`Prefix`** affix is exactly as
un-redrawable as a slot-bearing `Mixed` one, so refusing only `Mixed` would name the wrong thing and
let a real failure through. **Owner: this module, if and when a slot-bearing affix reaches a
container a workbench can reroll** — no shipped affix seed authors one today
(`data/seed/effects/affixes/all.json` carries two rows, both `suffix`, both all-concrete).

⛔ **One real latent defect found while doing this, named rather than silently absorbed: the shipped
affix corpus could not be drawn at all.** Both rows in `data/seed/effects/affixes/all.json`
(`affix.authored.affix-draw-000/001`, the `affix-authoring` pipeline's output) carry **two concrete
refs**, and the old `ExpandSingleRefAffix` threw `NotSupportedException` for *any* bundle with
`Refs.Count != 1` — so a container pooling either one crashed `Instantiator.Draw` rather than rolling
it. It went unnoticed because `Draw`'s live callers (`ActionSeeder`, `TryInstantiate`) are fed
single-ref affixes generated 1:1 from the atom catalog, and nothing wires the authored corpus into a
container pool yet. `ExpandConcreteRefs` closes it as a side effect of the work this addendum
describes; `A_multi_concrete_ref_bundle_expands_to_every_ref_in_seq_order` pins it.

⭐ **The safety claim is proven, not asserted.** `Draw` is the shared instantiation path every module
draws from (`ActionSeeder`, `TryInstantiate`, `AffixImportPathTests`), so
`Every_mixed_free_pool_draws_exactly_what_the_two_independent_draws_model_drew` runs the **verbatim
pre-change implementation** as an oracle over 16 container shapes × 25 seeds and asserts the new code
agrees on every draw. A golden recorded from the *new* code could not tell a preserved sequence from a
shifted one — that is why the oracle is the old algorithm and not a captured string. The two RNG
stream names (`atom.pool.prefix.{id}`, `atom.pool.suffix.{id}`) are byte-unchanged, and
`StreamNameOf`'s two literals carry a comment saying they are structural, never tunable.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "…InstantiatorDrawBudgetTests\|…Items.RerollPolicyTests"` | **39 passed / 0 failed** — 12 new in `InstantiatorDrawBudgetTests`, `RerollPolicyTests` 23 → 27 |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **7267 passed / 4 failed** against a **freshly measured** same-session baseline of **7238 / 4** — the *same four*, none in `Items.*` or `Atoms.*`. All four are the concurrent class-system / expeditions stream's: three `ProveAptitudeJsonEmitTests` throwing `BattleStatComposer.Configure(...) has not run`, and `ExpeditionResolverTests.Tier_goldens_are_locked`; `git status` shows `src/FusionRpg.Core/Battle/BattleStatComposer.cs` and the class-system tuning/baselines mid-edit |
| `dotnet test tests\FusionRpg.Guard.Tests` | **204 passed / 0 failed — fully green** |
| `.\scripts\guard-single-writer.ps1` · `guard-funnel-delta` · `guard-dal` · `guard-secondary-no-unity` | all four **OK** |
| `python scripts\audit-overflow.py` | **0 critical**, 60 findings, **none** in `Effects/Atoms/Instantiator.cs` or `Items/Mutation/` |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0.** The one M2 is `Delve/DoorTypeCatalog.cs`, the party-dungeon stream's untracked new tree |

⚠ **One flake seen once and dismissed with evidence, not by assumption.**
`ActionCatalogTests.NoJsonIsParsedAfterLoadEvaluatingTheCompiledConditionAllocatesZeroBytes` failed on
one full-suite run (2280 bytes against an expected 0) and passed on the next full run plus three
isolated runs. It measures `GC.GetAllocatedBytesForCurrentThread()` across a 100k-iteration loop, so a
tier-1 re-JIT on the same thread lands inside the window; nothing on that path
(`ActionCompiler`, `PredicateCompiler`, `FactReader`) is touched here.

⚠ **A second concurrent-stream artifact, named rather than absorbed:** a mid-run
`dotnet test tests\FusionRpg.Data.Tests` from the other stream held
`tests/FusionRpg.Data.Tests/bin/**/FusionRpg.{Core,Data}.dll` open for ~20 minutes, failing this
session's Data build with `MSB3027`. Re-running with `-p:BaseOutputPath=<scratch>` dodges the lock but
**invalidates the result** — 77 broad seed/read failures, because those tests resolve `data/` relative
to the assembly directory. Recorded so the number is not mistaken for a regression.

**Files (addendum):** `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs` (EDIT — `BudgetDraw`,
`DrawBudget` public with `count`/`excludeGroups`/`crossBudget`/`excludeAffixIds`, A1 state threaded
through `Draw`, `ExpandSingleRefAffix` → `ExpandConcreteRefs`, `EligibleFor`/`StreamNameOf`/
`BudgetCandidate` helpers, the stale *"module 2, not yet built"* comments corrected);
`src/FusionRpg.Core/Items/Mutation/RerollPolicy.cs` (EDIT — `reroll.mixed-affix-undefined` deleted,
`reroll.slot-affix-undefined` added, `ValidateRerollable` takes a `lookupAffix` instead of a
`resolutionOrderLanded` bool, `TargetsFor` added, `BudgetTargets`' doc corrected);
`tests/FusionRpg.Core.Tests/Atoms/InstantiatorDrawBudgetTests.cs` (new — 12 facts, including the
legacy-algorithm equivalence oracle); `tests/FusionRpg.Core.Tests/Items/RerollPolicyTests.cs`
(EDIT — the Mixed refusal test replaced by five: no-longer-refused, the slot residual, `TargetsFor`'s
both-budget counting, a retained `Mixed` blocking both exclusion sets, and an end-to-end partial
reroll of a `Mixed` affix through the real `DrawBudget` that `ValidatePostOp` accepts on all 40 seeds).

⚠ **`docs/architecture/item/spec-enhance-reroll.md` §2 is now describing a satisfied condition**
(*"If module 2 `resolution-order` has not landed the real semantics, a reroll targeting a `Mixed`
affix is refused with `NotRerollable` until it has"*). Left as authored — it is a conditional whose
antecedent is false, not a wrong statement — but flagged here so a later reader does not take it as
current state.

---

**Files:** `data/tuning/enhancement.v1.json` (new — the gain asymptote's `K`, the three risk bands,
the `ilvl_cap` floor, the milestone stride, the craft-pity threshold, the transfer ratio and window,
and the reroll price's two legs; **THE soft cap lives here**);
`src/FusionRpg.Core/Items/Mutation/{EnhancementTuning.cs, MutationOp.cs, EnhancePolicy.cs,
RerollPolicy.cs, CraftPityCounter.cs, TransferPolicy.cs, MutationReplay.cs, CraftingHorizonReport.cs,
MutationPreview.cs}` (new); `src/FusionRpg.Core/Items/RarityBudgetKeys.cs` (EDIT — `reroll_cost_mult`
→ `HasDecidedShape: true`); `data/tuning/item-rarity.v1.json` (EDIT — the unread `enhanceCapAsymptoteK`
duplicate removed, note added); `data/seed/items/recipes/recipes.json` (EDIT — seven `reroll` rows
re-authored to `reroll-one`/`reroll-all`); `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs` (new —
`effect_instance_op`, the five head columns, `suppressed`, `AppendMutationOp`, `ReadMutationOps`,
`SetInstancePityCounter`, `SeedRerollCostMult`); `src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT —
`EnsureInstanceOpSchemaUnlocked` in `Init`); `src/FusionRpg.Server/Program.cs` (EDIT — parses
`enhancement.v1.json` at boot, seeds `reroll_cost_mult`);
`tests/FusionRpg.Core.Tests/Items/{EnhancePolicyTests.cs, RerollPolicyTests.cs,
MutationReplayTests.cs}`, `tests/FusionRpg.Data.Tests/Items/InstanceOpTests.cs` (new);
`tests/FusionRpg.Core.Tests/Items/RarityBudgetKeysTests.cs` (EDIT — `reroll_cost_mult` moves to the
ready set); `tests/FusionRpg.Core.Tests/Items/MaterialCorpusTests.cs` (EDIT — the verb refusals are
gone, the legacy-shard count moves 5 → 7, the resolvable corpus 18 → 23);
`tests/FusionRpg.Core.Tests/Items/MaterialVocabularyTests.cs` (EDIT — the `reroll`/`socket-imbue`
comments now say which vocabulary each name belongs to, the assertions unchanged);
`tests/FusionRpg.Data.Tests/Items/MaterialSpendTests.cs` (EDIT — `reroll_cost_mult` leaves the
still-awaiting list); `docs/architecture/power/ssot-power-scale.md` (EDIT — §10.2 row 24),
`docs/architecture/power/inventory.json` (EDIT — rows 24/25) and `scripts/guard-power.ps1`
(EDIT — `EnhancePolicy.cs` on the G2 allowlist with its reason), all three from the power-guard
finding above.

⚠ **One deviation from the spec's Project structure, stated rather than silent:** the nine Core files
live under `src/FusionRpg.Core/Items/Mutation/` rather than flat in `Items/`, matching what modules
10/11/12/14 already did (`Display/`, `Drops/`, `Thresholds/`, `Materials/`). Same files, same names,
plus `MutationPreview.cs` for §10's single read, which the spec describes but does not list.

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.EnhancePolicyTests|FullyQualifiedName~Items.RerollPolicyTests|FullyQualifiedName~Items.MutationReplayTests"`;
`dotnet test tests\FusionRpg.Data.Tests --filter InstanceOpTests`; `dotnet run --project tools\ItemSeedValidator`

### ✅ P4.3 — Module 16 `sockets` — BUILT AND VERIFIED 2026-09-05 (the `gem`/`combo` container kinds, `bind_ordinal` and the 102 explicitly deferred to their real owners — all three upstream, none skipped)

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** Two things filed from
there: (1) the shipped seedsmith drop-table corpus already references **41 `insert` entries** that
cannot resolve until X7 lands the `gem` container kind and this module lands the count rule — module
11's importer refuses each by name with `ContentRuleViolated{drop.entry-kind-unavailable}`, naming this
module. **⏸ Still refused after this module** — the count rule landed, X7 has not; see the deferred
list below. (2) ⚠ **The lane disagrees with itself on the socket stream's name.** `ssot-generation.md`
§4.3's stream table says `item.socket.{i}` derived from the **loot seed**; `spec-sockets.md:143` (this
module) and `spec-drop-volume.md`'s own step-10 row both say `DeriveStream(roll_seed, "item.socket")`.
Module 11 shipped **this module's** spelling — ✅ **confirmed correct here**, and step 10 now consumes
exactly that stream, so the divergence recorded in `LootStreams.Sockets`' doc comment is resolved in
this module's favour rather than left open.

⛔ **Four spec corrections, each checked in the file the spec cites, recorded rather than absorbed:**

| # | `spec-sockets.md` says | Verified | What shipped |
|---|---|---|---|
| **S1** | §12: mint `NotSocketable` / `NoFreeSocket` / `SocketOccupied`, *"moves that assertion 34 → 37, and it is a reviewed change"* | **Refused by the code's own rule.** `AtomRejectionReason.ContentRuleViolated`'s declaration reads *"the 34th and last member by design — a caller that wants a new rule registers a namespace, it never mints a 35th code"*; item-ideal.md §2b.1 says the same. ⚠ The spec's arithmetic is wrong too: the shipped list is 33 + `None` + `ContentRuleViolated` = **35**, which `AtomKindRegistryTests.cs:45` already asserts | The three land as `ContentRuleViolated{socket.not-socketable / .no-free-socket / .occupied}` (plus `.not-imbuable`, `.entry-exceeds-role-ceiling`). §12's actual requirement — **each operator fix stays distinct** — is met, and the enum stays **35**, asserted |
| **S2** | §3: *"`socket_max` is a ROLE property, **fixed per role, not varied per base type**"*, with a named test `socket_max_is_fixed_per_role_and_never_varies_by_base_type` | **Contradicted by the shipped corpus.** Module 6 measured `armament-primary` = `{0:18, 1:26, 2:4}` across 740 entries; that test is unwritable without refusing the corpus | Re-stated as the enforceable half — **"never EXCEEDS its role's ceiling"** (`SocketGeometry.ValidateEntry`), which is the clause that actually defends §8.1. Proven against the **real live corpus** (720 live entries walked, zero violations), not a fixture. Module 6's `sockets.v1.json` note had already anticipated this exact restatement; ✅ **confirmed, not re-derived** |
| **S3** | ssot §5.2: `socket_combo_ingredient` is keyed `(combo_id, **position**)`, consecutive from socket 0 | **Superseded by D41** (unordered multiset) | The table is `(combo_id, family_id, min_tier)` + `qty`. ⛔ **No `position` column exists**, deliberately: a schema with one is how a matcher becomes order-sensitive by accident. Asserted by reflection over `ComboIngredient` as well as by the DDL |
| **S4** | ssot §5.2: `item_socket` is *"a materialized view of I6's operation log, not the SSOT"* | **D2 §6 refused it by name**, and clause 13 exempts sockets from the reconstruction clauses entirely | `item_socket` **is** the SSOT. `GetSockets` takes one instance id and reaches no op log — asserted by reflection on the real signature **and** by writing sockets with zero ops and reading them back |

**What was built:**

- [x] ⭐ **Module 16 took real ownership of `data/tuning/sockets.v1.json` (`version` 1 → 2).** Module 6
      forward-seeded it with the `socketCeiling` table alone and said explicitly *"module 16 owns the
      ceiling"*. The fifteen rows are carried **unchanged, value for value** — they are
      `spec-sockets.md` §3's own re-issued table and re-deriving them would have minted a second source
      of truth — and seven new sections are added here: `structuralCeiling`, `maxCombosPerActor`,
      `rarityGrant`, `insertTiers`, `removal`, `resonance`, `strainSplice`. ✅ **Module 6's ownership
      claim is confirmed rather than corrected** — see the ⭐ addendum added to P2.2 above
- [x] **I4 — sockets, inserts and the four operations.** `SocketOperations` is four pure state
      transitions over one item's `item_socket` rows: `socket-add` (opens an empty **crafted** socket),
      `socket-insert` (explicit index, or a deterministic lowest-empty auto-pick), `socket-remove`,
      `socket-imbue`. ⛔ **This module defines no `op_kind`** — the namespace is module 15's and already
      carries all four; a reflection test asserts this module exposes no enum of its own
- [x] ⭐ **The combination evaluator — 127 rows, one pure function.** `Evaluate(host, fill, catalog,
      tuning)`: no RNG, no clock, no ambient state, no writes. Resolution order is carried by
      `ComboShape`'s own **declaration order** (Strain, Splice, Pure, Ring, Eclipse, Diversity) rather
      than by a method's statement sequence, so a later shape cannot be slipped ahead of Strain by
      writing it earlier in a loop — asserted
      (`Strains_resolve_before_pure_before_ring_eclipse_and_diversity`)
- [x] **The 25 resonances are GENERATED, and the generator re-derives its own count.**
      `ResonanceGenerator` builds `|Concrete| × |pureThresholds|` Pure + `|ringOrder|` Ring + 1 Eclipse
      + `|diversityThresholds|` Diversity = 6×3 + 4 + 1 + 2 = **25** off `ElementRoster.Concrete` and
      the tuning. The test asserts **both** the literal 25 **and** the re-derivation, so adding a
      seventh element grows the catalog instead of going red. ssot §6.4's authoring rule (*"a resonance
      may not repeat a family its triggering inserts carry"*) is structurally impossible rather than
      reviewed: a generated recipe names no ingredient families at all
- [x] ⭐ **D27 renamed every combination container id** — `combo.pure-fire-3`, `combo.ring-fire-ice`,
      `combo.eclipse`, `combo.diversity-3`, `combo.strain-*`, `combo.splice-*`. The lane's
      `gem.combo-*` / `gem.word-*` spelling is retired (definitions.md §1 forces the prefix to match
      the kind); inserts keep `gem.`. Asserted per row, not by spot check
- [x] **D22 as amended — affinity is a BONUS on both layers, and the gate is gone.** A mismatched fill
      still fires (`Affinity_is_a_bonus_and_a_mismatched_fill_still_fires`). All-attuned raises a
      **resonance's effective count** by 1 and a **Strain/Splice's granted tier** by 1 — the shared
      `+1`, both arms tested. ssot §7.1 and §7.2's worked examples both reproduce: two attuned earth
      inserts reach `combo.pure-earth-3`; one unattuned contributor removes the whole bonus and the
      item lands on `combo.pure-fire-2`
- [x] ⛔ **A real design defect the spec's own §8/§5.2 reading would have shipped, found by a red test
      and fixed.** Giving a generated Pure row `min_sockets = k` (the obvious reading of ssot §5.2's
      column) makes attunement's `+1` **unreachable by construction** — a 2-socket item could never
      fire the k=3 step, which is exactly ssot §7.4's worked payoff (*"three attuned inserts on a
      three-socket item fire `pure-earth-4`"*) and §4.2's *"single most load-bearing anti-tax
      mechanism"*. Generated rows now carry `MinSockets = 0` and are **self-gating** (you cannot put
      three fire inserts in two sockets); `min_sockets` belongs to **authored** recipes, which gate on
      host size before any insert is placed. Pinned as
      `Attunement_reaches_a_step_the_socket_count_alone_could_not`
- [x] **Affinity never scales an insert's magnitude — asserted by reflection, not by intent.**
      `CombinationResult` carries exactly `{ComboId, Shape, EffectiveCount, GrantedTier, AllAttuned}`,
      so there is nowhere to put a scaled magnitude and §4.3's inventory defence cannot collapse
- [x] **`omni` counts toward Diversity only, and an ELEMENT-FREE insert counts toward nothing.** Both
      tested. The second is stated because its absence would otherwise read as an oversight: `""` is an
      absent element, not a seventh one, so a vitality gem joins no shape at all. `omni` is refused as
      an affinity at **load** (`SocketTuning.Parse`) and at **imbue time** (`BadParamValue`)
- [x] ⛔ **D21's exclusivity validator — and it mints no reason code.** A set piece never fires a
      Strain or Splice; the inserts stay and every resonance still fires; socketing *toward* one is
      **allowed** (refusing the insert would punish a fill that is legal for resonance).
      `SetExclusivityValidator.SuppressionReason` is display copy naming D21, not a code — and
      `Evaluate` is asserted to return a list with no rejection channel at all, so a code cannot be
      minted for a bonus that did not fire
- [x] **✅ D41 — recipes are UNORDERED, proven four ways.** `MultisetSatisfied` counts and claims; the
      same inserts in any arrangement resolve identically
      (`The_same_inserts_in_any_arrangement_resolve_to_the_same_combination`); the DDL carries no
      `position` column; and `bind_ordinal` is computed for **display order only**
      (`SocketOperations.BindOrdinalFor(i) = i + 1`, content-derived) with a comment saying a matcher
      that reads it is a bug. ⛔ **A real matcher defect was caught while writing it**: a first-come
      ingredient loop lets a `minTier 1` requirement eat the only t5 insert and starve a `minTier 5`
      one on the same family. Fixed by matching most-specific-first and spending the lowest qualifying
      tier; pinned as `A_min_tier_ingredient_is_not_starved_by_a_lower_one_claiming_the_high_insert`
- [x] ⭐ **`socket_min` / `socket_max` have a decided shape and are registered** — the two keys
      `ssot-rarity.md` §4.4 recorded as *"awaiting I4"*, and the **last two** undecided rows in
      `RarityBudgetKeys`' closed list. **The shape:** two integers per rung, the **inclusive window a
      drop's socket count is rolled from**, before the base type's own `socketMax` clamps it.
      Transcribed from ssot-sockets.md §4.1's five ordinal **bands** onto the shipped ten rungs (two
      rungs per band) — not re-derived. ⭐ **§9.5's one constraint (*"rarity grants a RANGE, not a
      number"*) is enforced at LOAD**: `SocketTuning.Parse` refuses a table whose adjacent windows do
      not overlap or whose grant is non-monotonic, because a gap makes socket count a strict ladder and
      re-opens §8.1 at full strength. Seeded by `RpgStore.SeedSocketGrants`, deliberately its own method
      so module 7's seeding never grows a dependency on a later module's tuning file (module 14's
      precedent, module 15's follow). Cross-referenced into **P2.1** and **P2.3** above
- [x] ⭐ **Step 10 of the loot pipeline is LIVE, and the switch moved no other draw.** Module 11 shipped
      it as a documented no-op that *reserved and advanced* `DeriveStream(roll_seed, "item.socket")`.
      Both blockers it named are now closed, so `LootPipeline` calls `SocketGeometry.SocketsAtDrop` —
      and because the stream was always reserved, **every affix roll at every band is byte-identical
      across the change** (`LootPipelineTests` green, unmodified). The host supplies `SocketMaxFor` and
      `SocketTuning`; with either absent the step stays the no-op it was and still advances, because
      **half a socket rule grants the wrong count**, which is worse than granting none
- [x] **`item_socket` + `socket_combo_recipe` + `socket_combo_ingredient` DDL and their operations**
      (`RpgStore.Sockets.cs`, inside `FusionRpg.Data` — `guard-dal` green). `SetSockets` writes the
      whole next state in one transaction rather than a diff, because the Core operations already
      return the whole next state and a diff would put a second, weaker copy of the transition rules in
      the DAL. A sparse socket list **throws**; it is never stored
- [x] ⛔ **`item_socket.instance_id` carries a live FK to `effect_instance` — and it caught its own
      test fixture.** The first version of `ItemSocketStoreTests` wrote against made-up host ids and
      four tests failed with `SQLite Error 19: FOREIGN KEY constraint failed`. That is the constraint
      working, not a bug: a socket cannot exist without a host, and `ON DELETE CASCADE` means deleting
      the item takes its sockets with it. The tests now mint **real** `effect_instance` rows via
      `SaveInstance`
- [x] **Nothing socketing does can reach the host's frozen instance — asserted, not promised.** No
      method on `SocketOperations` returns or accepts `AtomAppend` / `MutationResult` / `InstanceHead`
      (reflection over the real signatures), and `Socketing_writes_no_row_the_host_instance_owns` writes
      two sockets against a **real store** and shows the mutation head's `state_hash`, `mutation_seq`
      and `enhance_level` all unchanged and the op log still empty. SC5 is not strained by this module
- [x] **The structural ceiling qualifies, and it was checked rather than waved through.**
      `SocketLimits.SocketMaxCeiling = 4` is exempt under AGENTS.md as a **legibility** limit on one
      item's recipe shape, and the comment says so **and names what stays open**: `insertTiers.count`
      is a **soft content axis** (raise it in the file and the ladder extends — tested at 12), a
      combination's granted tier is unbounded above, and magnitude growth rides `contentScale`, which
      this layer never reads. A ceiling above it **THROWS at load**, never clamps. The file and the
      `const` cannot drift: `Parse` refuses a `structuralCeiling` that disagrees with the code, in both
      directions, and a test asserts the file's own note carries the words `STRUCTURAL`, `LEGIBILITY`
      and `contentScale` so a tidy-up cannot delete the justification
- [x] **Every number a balance pass would touch is in the tuning file, and the parser REFUSES rather
      than defaults.** Stripping any of the six sections throws at load, asserted section by section
      against the real file. Nine structural invariants are checked at parse time, each with its own
      message: the fifteen ceiling rows against the role registry; `standard`'s deliberate absence
      (D14 — a zero row would read as *"in scope, allowed no sockets"*); every ceiling against the
      structural 4; the grant windows' well-formedness, monotonicity and OD4 overlap; the ring against
      the concrete element roster; `omni` refused as any resonance member; the removal thresholds
      against the tier ladder (a table with no commitment tier is refused); the upcycle ratio's drain
      direction; and D20's ingredient count against the ceiling
- [x] **`audit-magic-numbers.py --summary` reports `M1 = 0`** and **zero** findings anywhere under
      `Items/Sockets/`; `audit-overflow.py` reports **0 critical** and **zero** findings under
      `Items/Sockets/`. The module holds no magnitude of its own — counts, tiers and thresholds are
      shape indices, and the numbers a combination *grants* live on its `combo` container's atoms,
      which are X7's

**⛔ Two real defects found, named, not silently fixed:**

- [x] ⛔ **`gem.g1-007` ("Primal Shard") declares `affinityElement: "omni"`, and `omni` is not an
      affinity.** `element-hub-ssot.md` §4 is explicit that `omni` is not an actor type slot, and
      `spec-sockets.md` §6 restates it — so this gem names a socket that can never exist and its
      attuned bonus can never fire. Found by reading the real corpus; confirmed against `git show HEAD`
      to **predate this session** (seedsmith batch `gems-g1`, authored 2026-08-22). **Not hand-fixed**
      — `ItemSeedValidator`'s own footer says *"Re-run the partitions named above; do not hand-fix"* —
      but it is now **reported by name** instead of invisible: new check `GemAffinityCheck.cs`
      (`GemAffinityNotConcrete` / `GemElementUnknown`), wired into `Validator.cs`. **This moves the
      validator baseline 165 → 166, and the single new error is this row.** Owner: the authoring
      fleet's `gems/1` partition re-run. Also pinned in
      `SocketOperationsTests.No_shipped_gem_declares_an_omni_affinity` so the set cannot grow silently
- [x] ⛔ **`spec-sockets.md` §12's enum arithmetic is wrong** (34 → 37; the shipped list is 35). Filed
      as **S1** above rather than absorbed, because a spec that miscounts the closed list is how a 36th
      code eventually gets minted "to match the doc". No code change needed — the rule already refuses
      it, and the test now pins 35 with the reason written next to it

**⏸ Deferred, each with its owner named — none silently skipped:**

- [ ] ⏸ **`ContainerKind.Gem` and `ContainerKind.Combo` — effect-atom's (X7), not this module's.**
      `ContainerRow.cs` is still six values (`Item · Trait · Skill · SpeciesPassive · Patron ·
      WorldBuff`) with six `PrefixOf` arms, verified. `spec-sockets.md`'s own Project Structure marks
      that row **"NOT this module's"** by name. **Consequence, stated plainly:** this module cannot
      author a single `gem.*` or `combo.*` **container row**, so the 25 generated resonances land in
      `socket_combo_recipe` (their *recipe*) and the atoms they grant do not exist yet. What shipped is
      the count rule, the evaluator, the operations and the state — which is the whole of what is
      reachable at build position 16. ⛔ This is also why module 11's **41 `insert` drop entries stay
      refused**: they need the container kind, not the count rule
- [ ] ⏸ **`bind_ordinal INTEGER NOT NULL DEFAULT 0` on `effect_binding` — effect-atom E6's.** Today's
      DDL is `binding_id · instance_id · owner_kind · owner_key · slot · priority · source ·
      bound_utc · revision`, confirmed in `RpgStore.AtomInstances.cs`. The socket half of the contract
      **is** built and tested (`BindOrdinalFor`), so landing the column is a wiring change, not a
      design one. ⚠ The comparer it would tiebreak **has no implementation anywhere yet**, so nothing
      is broken today — which is precisely why the spec argues to add it now rather than after E12.
      Requested here, not built: a column on another program's table is not ours to add
- [x] ⏸→✅ **The 102 Strains and Splices — module 21's (`strain-splice-gen`, P4.4 below), TAKEN UP
      2026-09-05.** The evaluator, the recipe tables, D20's four-ingredient rule, the one-per-item cap,
      the lowest-`container_id` tie-break and the per-actor backstop were all built and tested here
      **against synthetic Strain rows**, because the real ones are model-call output. ⭐ **Module 21
      built the generator for them and the seam held exactly as stated** — `StrainSpliceGrid` derives
      all 102 ids from `AptitudeCatalog.All` × the archetype registry, `SocketTuning`'s
      `strainSplice.ingredientCount` and `resonance.attunedTierBonus` are read from **this module's**
      file rather than forked into a second one, `RolesThatCanHostAStrain` is mirrored in Python and
      the two agree, and `Program.cs` now validates every recipe on the `SeedComboRecipes` path
      against the derived grid. ⏸ The 102 CONTENT rows are still model-call output and are still
      unauthored — see P4.4 for who runs it. ⛔ **And module 21 found one stale citation in this
      module's shipped code** — see the addendum below
⛔ **Addendum 2026-09-05, found while building module 21 (`strain-splice-gen`).**
`SocketGeometry.ValidateEntry`'s doc comment cites *"module 6 measured `armament-primary` at
`{0:18, 1:26, 2:4}`"*, and this entry's own S2 row plus its *"720 live entries walked"* quote the
same figures. **Module 6 re-issued the `socketMax` table on 2026-09-04** (`dcabac3 update seeds`, the
owner's own commit, the day this module was built): the live corpus is **740 entries**,
`armament-primary` is `{0:10, 1:10, 2:10, 3:10, 4:8}`, the maximum anywhere is **4** rather than 2,
and **no entry omits the field**. ⚠ **The RULE this module chose is unaffected and re-verified across
all 740** — no role's declared `socketMax` exceeds its ceiling, so S2's restatement to *"never
EXCEEDS its role's ceiling"* was right and remains right. This is a **stale citation, not a broken
check**, and it is filed rather than hand-edited because the same numbers appear in
`spec-sockets.md`, in `spec-strain-splice-gen.md` and in this entry. ⭐ The practical consequence is
the good one: `RolesThatCanHostAStrain` now returns a **non-empty** list on the real corpus
(`armament-primary`, `core-guard`), so the geometric Strain ceiling of 2 this module computed is
live rather than hypothetical.

- [ ] ⏸ **The 25 legacy `sockword.*` entries are NOT migrated, and that is P4.4's call, not an
      oversight.** They are position-ordered (D41 makes recipes unordered), carry the retired
      `gem.word-*` runtime ids (D27 renames them `combo.*`), and **not one reaches D20's four
      ingredients**, so not one is a legal Strain or Splice today. The carry table below already rules
      *"regenerate, not retain"*. Recorded as a standing test
      (`The_legacy_socket_word_corpus_is_ordered_and_awaits_module_21s_retirement`) so *"we forgot"* and
      *"we decided"* stay distinguishable. ⭐ **Confirmed and quantified by module 21 2026-09-05, and
      this module's claim was exactly right:** `combogen/migrate.py`'s `legality_report()` measures all
      25 against the new rules and finds **0 legal, 25 illegal**, naming every reason per entry (25 at
      2 or 3 ingredients, 25 carrying `position`, 25 on `gem.word-*`, 25 with a non-derived
      `minSockets`, 4 hosted on a role whose ceiling can't reach four (`footing`×1,
      `armament-secondary`×2, `ward-array`×1)). ⏸ Still not migrated: the retirement is one of five
      sites in a rename bundle whose other four include a **frozen registry** and the 102 model calls
      that replace them. The standing test above is left untouched and still green. See P4.4
- [ ] ⏸ **Wave-1 insert authoring — held, deliberately (ssot §9.13).** A `+armour` insert is
      `ScopeUnsupported` at any per-actor scope (G8) — unchanged. Most element gems are `stat.derived`,
      and D6's quarantine on that kind is already lifted on both real runtimes: E12 reopened Battle
      2026-08-23, and the Derived-write lawn executor reopened Lawn 2026-08-30 —
      `AtomKindRegistry.cs:534` ships `Full/Full/None` (Lawn/Battle/Sim), wired at the live-lawn
      `ActorHub` (`CheatState.cs:59`). A `stat.derived` element gem would bind and execute today.
      Authoring one now still produces *"a row no code consumes, which is a lie in a
      table"*, for a narrower reason than before: it **cannot be enforced here** because no `gem`
      container can exist yet (X7, above) — the atom kind is no longer why. It becomes enforceable the
      moment X7 lands. Owner: whoever authors the first `gem` container after X7
- [ ] ⏸ **Socket-combination budget versus set budget on one item — module 9's (`item-power-reads`).**
      §2g's surviving half of D21. It is a budget question and cannot be answered before the power
      reads run. Named in `SetExclusivityValidator`'s own doc comment so a reader finds it in the code
      as well as here
- [x] ⏸→✅ **The compendium, the socket-UI preview and the ~~"one swap away"~~ hint — module 20's
      (`item-surfaces`), TAKEN UP 2026-09-05.** This module's stated obligation was to expose
      `evaluate()` in a **write-free preview form** so module 20 has something truthful to render, and
      that is done and tested: `Preview` is literally the same code path (a second implementation is
      how a preview starts lying about what socketing will do), plus `PreviewWithOneMore` for the hint.
      ⭐ **Module 20 built `CombinationDistance` on top of it — one call to this module's `Evaluate`,
      asserted as exactly one** (`DistanceDiagnostics.ActiveSetEvaluations == 1`), with the four closed
      display states and the `∞`-is-`undiscovered` rule. ⛔ **And it found that the SWAP half of the
      hint no longer exists:** `spec-item-surfaces.md` (2026-09-03) specifies an INSERT/SWAP split whose
      swap leg counts `n − cycles(σ)` over an ORDERED recipe, and **D41 (2026-09-04) made recipes
      unordered the next day**, with a consequence row naming module 20 by name — *"distance counts
      missing kinds, never positions."* This module shipped it unordered (no `position` column, no
      `bind_ordinal` read), so a swap distance would always be zero and the hint would be a lie.
      Module 20 implemented the multiset distance and pinned it over all 24 permutations of a
      four-insert fill. **Nothing here changes; the record is that D41 reached module 20 correctly and
      its spec did not.** See P5.4
- [ ] ⏸ **The workbench executor that debits and appends a socket op.** Pricing is module 14's and
      already shipped — `bore`, `imbue`, `socket` and `upcycle` all carry cost rows, D24's
      `imbue == bore` equality is checked at load, and `socket` costs a flat ten souls at every rung.
      `AppendMutationOp` and `TrySpendRecipe` are both shipped too, so the call site that joins them to
      `SetSockets` is a composition rather than a design — the same carry module 15 recorded for its
      own executor, deliberately not duplicated here
- [ ] ⏸ **P3.2's ask that this module reuse `ThresholdEvaluator`/`ThresholdConsumer<T>` for resonance
      counting — found unaddressed during the module-22 consistency pass, not during this build.**
      `ResonanceGenerator`/`CombinationEvaluator` count inserts and grant at breakpoints at the host
      item's scope, independently of that mechanism. Whether that should be reconciled with module 12's
      evaluator, or is correctly separate because folding scope into the evaluator is the exact merge
      module 12's own bullet argues against, is an open question for the owner — named here rather than
      left silent. **Cross-referenced from P3.2.**

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter SocketGeometryTests\|CombinationEvaluatorTests\|SocketOperationsTests` | **72 passed / 0 failed** (new — 3 classes) |
| `dotnet test tests\FusionRpg.Data.Tests --filter ItemSocketStoreTests` | **8 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items."` | **481 passed / 0 failed** — the whole item program, modules 1–15's own suites included, still green under this module's two registry changes |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Items."` | **96 passed / 0 failed** — the item program's whole DAL half, including modules 7/11/12/14/15's own store suites, green under the new socket schema and the four moved SC7 rows |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6564 passed / 8 failed** — all 8 in `Atoms.EntityFieldsTwelvePlusTests` (1), `Battle.*` (3), `ClassSystem.ProveAptitudeJsonEmitTests` (3) and `Expeditions.ExpeditionResolverTests` (1), the concurrent stream's own in-flight work; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **763 passed / 1 failed** — `WorldGraphDiffTests`, whose test file **and** its `RpgStore.WorldGraphDiff.cs` are both untracked (`git status ??`), i.e. the concurrent stream's brand-new work; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | **202 passed / 0 failed** — clean, zero-tolerance held |
| `dotnet run --project tools\ItemSeedValidator` | **166 errors** (165 before this module). The **one** new finding is `gem.g1-007 GemAffinityNotConcrete`, the real pre-existing corpus defect this module's new check makes visible. **Zero** new findings from `SocketMaxCheck`, and no other check moved |
| `python scripts\audit-magic-numbers.py --summary` | **`M1 = 0`** overall; the `items` domain shows 1 M3 (`ArmouryQuery.cs:79`, module 2's) and **nothing** under `Items/Sockets/` |
| `python scripts\audit-overflow.py` | **0 critical**, 57 findings, **zero** under `Items/Sockets/` |
| `python -m pytest tools/seedsmith` | **not run — no Python content touched.** This module edited no `tools/seedsmith/**` and no `data/seed/**` file; the only data file it wrote is `data/tuning/sockets.v1.json`, which seedsmith does not read |

⚠ **Baseline re-measured fresh at the start of this session, not carried forward.** `Data` measured
**736 passed / 0 failed** before any of this module's code — the 3 failures P2.1–P4.2 recorded are
**gone**, closed by the owner's own commits. `Guard` measured **201/201** (now 202/202; the concurrent
stream added one). `Core` could not be measured before the build because that stream's uncommitted
`Progression/SpeciesProgression.cs` did not yet compile against its own new
`tests/FusionRpg.Core.Tests/Progression/` — it resolved on retry, exactly as expected. Every Core and
Data failure in the runs above was checked by name against `git status`: their source files
(`ActionEnvelope.cs`, `ActionRunner.cs`, `ActorPowerCache.cs`, `CoefficientTable.cs`,
`AptitudeSubsystem.cs`, `PointBudget.cs`, `ContractPolicy.cs`, `RpgStore.Expeditions.cs`,
`RpgStore.WorldGraphDiff.cs`, `affixes/all.json`, `coefficients.v1.json`) are all mid-edit or brand-new
in that stream and **none is touched by this module.**

⚠ **Three tests outside this module went red and all three WERE this module's** — named rather than
quietly edited, and all three **moved rather than loosened**:
`Items.RarityBudgetKeysTests.A_key_awaiting_a_decided_shape_is_not_registered_yet`,
`Items.RerollPolicyTests.Reroll_cost_mult_is_registered_with_a_decided_shape` (module 15's) and
`Items.MaterialSpendTests.Salvage_yield_is_seeded_for_all_ten_rungs_and_matches_the_tuning` +
`Items.InstanceOpTests.Reroll_cost_mult_seeds_every_rung_through_the_SC7_gate` (modules 14/15's) all
pinned `socket_min`/`socket_max` as **unregistered**. Deciding their shape is precisely what this
module owed. Each now asserts the keys **are** registered and that each names `sockets (16)` as its
consumer — a strictly stronger claim. ⭐ **And the SC7 gate itself is preserved, not dropped:** with
every key in the closed list now decided, *"not decided is not safe-to-seed"* is asserted against a
**synthetic key with no consumer at all**, because the mechanism has to survive the list happening to
be fully decided today — the next key added will not be.

**Files:** `data/tuning/sockets.v1.json` (EDIT — v1 → v2, module 16 takes ownership; the fifteen
ceiling rows unchanged, seven sections added);
`src/FusionRpg.Core/Items/Sockets/{SocketTuning.cs, SocketModel.cs, SocketGeometry.cs,
ResonanceGenerator.cs, CombinationEvaluator.cs, SetExclusivityValidator.cs, SocketOperations.cs}`
(new); `src/FusionRpg.Core/Items/RarityBudgetKeys.cs` (EDIT — `socket_min`/`socket_max` →
`HasDecidedShape: true`); `src/FusionRpg.Core/Items/Drops/LootPipeline.cs` (EDIT — step 10 live;
`LootContentView` gains optional `SocketMaxFor` + `SocketTuning`);
`src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs` (new — the three tables, `GetSockets`/`SetSockets`,
`SeedComboRecipes`/`GetComboRecipes`, `SeedSocketGrants`);
`src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT — `EnsureSocketSchemaUnlocked` in `Init`, after the
instance schema it references); `src/FusionRpg.Server/Program.cs` (EDIT — parses `sockets.v1.json` at
boot, then `SeedSocketGrants` + `SeedComboRecipes`);
`tools/ItemSeedValidator/Checks/GemAffinityCheck.cs` (new), wired into `Validator.cs`;
`tests/FusionRpg.Core.Tests/Items/{SocketGeometryTests.cs, CombinationEvaluatorTests.cs,
SocketOperationsTests.cs}` (new); `tests/FusionRpg.Data.Tests/Items/ItemSocketStoreTests.cs` (new);
`tests/FusionRpg.Core.Tests/Items/{RarityBudgetKeysTests.cs, RerollPolicyTests.cs}` and
`tests/FusionRpg.Data.Tests/Items/{InstanceOpTests.cs, MaterialSpendTests.cs}` (EDIT — the four moved
SC7 rows above).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket|FullyQualifiedName~Combination"`; `dotnet test tests\FusionRpg.Data.Tests --filter ItemSocketStoreTests`; `dotnet run --project tools\ItemSeedValidator`


### ✅ P4.4 — Module 21 `strain-splice-gen` — MACHINERY BUILT AND VERIFIED 2026-09-05 (⏸ the generative authoring pass itself is model-call work and is explicitly out of scope for a coding session — named below with who runs it; the `socket-word` kind rename is a five-site BUNDLE that lands *with* that run, and one of its five sites is a frozen registry)

⭐ **What "model calls" means here, decided by reading module 13's precedent rather than re-deriving
it.** P3.3 established the shape for a `(model calls)` module: the deterministic generator MACHINERY
is this session's job, the LLM-authored content draw is the owner's. That holds unchanged. The one
genuinely generative step — drawing 102 combination identities out of a model — cannot be run from a
coding session, so it is deferred **by name**, with the command that runs it. Everything the run
consumes, everything it mints ids for, and everything that judges it afterwards is built and tested
against real shipped data.

⛔ **Four spec claims checked in the file the spec cites, and the central one is STALE:**

| # | `spec-strain-splice-gen.md` says | Verified 2026-09-05 | What shipped |
|---|---|---|---|
| **T1** | ⛔ *"Measured over all 740 shipped base types (read 2026-09-03): the maximum `socketMax` anywhere is **2**"*, therefore *"no Strain and no Splice is buildable on any shipped chassis"* and *"**this module is inert until** [module 6 issues `socketMax = 4`]"* | **Contradicted by the shipped corpus.** Module 6 re-issued the table on **2026-09-04** (`dcabac3 update seeds`, one day after the spec measured). The live distribution over 740 entries is `0×253 · 1×255 · 2×148 · 3×68 · **4×16**`, and the sixteen 4s are **8 `armament-primary` + 8 `core-guard`** — exactly the two roles ssot-sockets §4.1 assigns 4 | ⭐ **The hard dependency is CLOSED and the module is not inert.** The spec's prescribed *failing* fixture `no_shipped_base_type_can_host_a_four_ingredient_combination_today` is written **in its flipped form**, both sides, with the old state named in the test's own docstring so the transition is a recorded fact rather than a test nobody remembers deleting. ✅ This module's own todo stub already said *"only `armament-primary` and `core-guard` do"* — **confirmed, not corrected** |
| **T2** | §"Ingredient count is 4" per-role table: `armament-primary` `0×18 · 1×26 · 2×4`; *"`jewel-minor-a` additionally has 24 entries with `socketMax` **absent**"* | **Stale in every row.** `armament-primary` is now `0×10 · 1×10 · 2×10 · 3×10 · 4×8`, and **no entry anywhere omits `socketMax`** | Re-measured in the test rather than transcribed. ⛔ The same stale figures are quoted in **module 16's shipped code** — filed as a defect below and cross-referenced into **P4.3** |
| **T3** | Project structure: `data/tuning/strain-splice.v1.json` — *"per-actor cap, affinity tier bonus"* | **Stale by one module.** Module 16 shipped `maxCombosPerActor: 3` **and** `resonance.attunedTierBonus: 1` in `sockets.v1.json` when it took ownership at v2. Writing them again here would be two sources of truth for the numbers the runtime evaluator reads | The file is created, and it carries **neither**. It holds only what module 16 does not own (the min-tier plan, the per-shape base tier, the learnability bar, two distinctness thresholds), and **both parsers REFUSE a file that declares one of module 16's six keys**, by name, at load |
| **T4** | *"No `items` subcommand exists… Module 13 adds the group; this module extends it"*; *"the live gem corpus carries **40 entries across 34 families**"*; the 25-entry legacy table (`2 ×15, 3 ×10`; `ward-array` 1) | ✅ **All confirmed exactly** — measured, not assumed. Module 13 did add the group; the gem corpus is 40/34; the legacy corpus is 25 entries at 2 and 3 ingredients with one `ward-array` host | The group is extended with `--kind combination --shape strain\|splice`; the 34 supplied families become the schema's **closed enum**; the 25 legacy rows are measured against the new rules rather than described |

**What was built:**

- [x] ⭐ **The grid is DERIVED from two shipped files, and it re-measures them against each other on
      every call.** The twelve come from `data/seed/aptitudes/roster.json` (the checked-in mirror of
      `AptitudeCatalog.All`, whose count is `PostureCount × PerPosture`); the three archetypes come
      from **module 13's `build-themes.v1.json`**, so `combo.strain-might-offense` and
      `build.might-offense` are the *same grid cell* rather than two lists that drift.
      `assert_grid_agrees()` raises on four distinct drifts (registry-only aptitude, roster-only
      aptitude, a repeated cell, an incomplete product) — the `assert_core_agrees` discipline module
      13 established, applied to a different pair of axes
- [x] ⭐ **A Splice pair is sorted by ordinal at MINT time, in both ports.** `(Might, Agility)` and
      `(Agility, Might)` produce one id; a uniqueness check would only have discovered the collision
      after 66 rows existed, one of them a wasted call. Asserted by minting all 66 and counting
      distinct, not by trusting the loop shape
- [x] ⭐ **`StrainSpliceGrid` (C#) and `combogen/grid.py` (Python) mint the same 102 ids from the same
      two files** — one reads `AptitudeCatalog.All` in code, the other the roster mirror; both read
      the archetype registry. Two derivations that agree are worth more than one derivation and one
      literal, and the C# half is what an authored corpus is checked **against**
- [x] ⛔ **`ContentRuleViolated{strainsplice.*}` — five rules, and NO new `AtomRejectionReason` is
      minted.** The enum is closed at 35 by its own declaration (module 16 recorded the same refusal
      as its S1), so `not-on-the-grid`, `ingredient-count`, `min-sockets-derived`, `host-cannot-hold`
      and `base-tier-not-tunable` are all one code with a namespaced payload. `ValidateRecipe` returns
      **all** violations, not the first — asserted with a row that breaks five at once
- [x] **The validator is LIVE on the seed path, not dead code.** `Program.cs` runs it over every
      recipe `SeedComboRecipes` is about to write. Today's 25 generated resonances are neither Strain
      nor Splice, so it refuses nothing — asserted (`The_validator_never_fires_on_a_generated_resonance`)
      — and it starts refusing the moment the authored 102 land beside them. Putting the guard in
      before the content is the point
- [x] ⛔ **The gem-supply precheck runs BEFORE the plan exists, and the supplied set becomes the
      schema's closed enum.** `Registration/IngredientUnsatisfiable` is `gates = True`; a 102-entry
      run that minted unsupplied families would be 102 wasted calls plus a red gate. Here the finding
      is **unproducible from a well-formed answer** rather than merely rare. Measured on the live
      corpus: **40 gems, 34 families**, and the metric reports **no findings**
- [x] ⭐ **`Registration/IngredientUnsatisfiable` now follows the kind PERMANENTLY, not at cutover.**
      The 2026-09-04 ruling's own warning is *"the metric must follow the kind, or a `gates = True`
      check quietly stops gating"* — and a metric keyed on one spelling does exactly that: it goes on
      passing, over zero rows, and nothing says so. `COMBINATION_KINDS = ("socket-word",
      "combination")` removes the failure mode for good; the class is renamed
      `CombinationIngredients`, the **metric id is unchanged**, and the message is byte-identical for
      a legacy row (`position` is printed only when present, because D41 superseded it and only the
      legacy rows carry it). A test drives a synthetic row of **each** kind id through the gate
- [x] **`data/tuning/strain-splice.v1.json` + two pure parsers, and both refuse rather than default.**
      A missing section raises at load in Python (`ComboTuningError`) and in C#
      (`StrainSpliceTuning.Parse`), asserted section by section against the real file. The C# parser
      **cross-validates against `SocketTuning`**: a min-tier plan whose length disagrees with D20's
      ingredient count, or a tier outside the shipped insert ladder, fails at boot rather than
      producing 102 recipes the evaluator can never match. A `baseTier` row for a *resonance* shape is
      refused by name — a generated resonance's tier is `ResonanceGenerator`'s and is not tunable here
- [x] ⭐ **`audit_schema`-clean by construction, proven three ways.** `audit_schema(schema) == []`;
      adding one bare `{"type": "integer"}` field makes `Pipeline(...)` **raise at construction**; and
      the schema carries **no** `tier`, `cost`, `chance`, `duration`, `minTier`, `baseTier` or
      `position` field — the names are avoided, never allow-listed past. `ingredients` is a flat array
      of exactly four family strings **with repeats legal**, because D41 makes a recipe a multiset and
      four named slots would have re-introduced position by the back door
- [x] **The brief refuses itself.** `build_brief` scans its own output for D20's banned word before
      returning, and for the ingredient count spelled as a digit or an English word — the schema's
      fixed array length is the enforcement, and prose restating it is a second source of truth. ⚠ The
      guard's first draft refused **every** brief because it matched the `4.` of the brief's own
      numbered list; found by running it, fixed by stripping enumeration markers first, and the
      false-positive shape is recorded in the code
- [x] **D41 holds at the EMIT layer, not only at the matcher.** `ingredient_rows` sorts the four picks
      by family id before zipping the ascending min-tier plan onto them and folding duplicates into a
      quantity — so the same four families in any arrangement produce byte-identical rows (asserted
      over three permutations), and the emitted row has **no `position` key** (asserted by field set)
- [x] **D22 as amended, both arms, and failure is impossible.** `granted_tier(..., all_attuned)`
      differs by exactly `attunedTierBonus`, and the unattuned arm still returns a real positive tier.
      Proven from the abuse side too, at module 16's own evaluator rather than restated: a fill whose
      insert element does not match its socket affinity **still fires**, at the base tier
- [x] **D21's exclusivity, at this module's angle.** The same fill on a plain host fires the Strain
      and on a set piece fires nothing of that shape — module 16's `SetExclusivityValidator`, reused
      and re-proven from the generator's side rather than re-implemented
- [x] ⭐ **The 127-against-45 learnability debt is MEASURED and reported, never enforced.**
      `catalogue.report()` derives the resonance half from module 16's own tuning (`|concrete| ×
      |pureThresholds| + |ringOrder| + 1 + |diversityThresholds| = 25`, so a seventh element grows it
      by construction) and prints **127 total against a bar of 45 — `ratioPermille` 2822, i.e. 2.8×**.
      ⛔ Nothing refuses the 102nd combination: a cap on how many combinations may exist would be a
      hard content ceiling. What the report carries instead is module 20's two mitigations as
      **REQUIREMENTS with an owner** — the compendium reveal and the socket-UI preview
- [x] **The tuning file carries no content ceiling** — `maxCombinations` / `maxStrains` / `maxSplices`
      / `gridCap` are refused anywhere in it by a test in **both** languages, and the learnability
      note's own words `REPORTED, NEVER ENFORCED` are asserted so a tidy-up cannot delete the reason
- [x] ⚠ **No 12 → 6 aptitude-to-element mapping is introduced, and the gap is asserted STRUCTURALLY.**
      `StrainSpliceGrid.cs` names no element id and no element type, in its source text **and** across
      every public signature by reflection; no brief names an element; nothing in `combogen/` reads
      `resonance.attunedEffectiveCountBonus`, which is Pure's bonus and not this layer's. The two
      layers treat affinity differently **on purpose** and this module re-specifies neither
- [x] ⛔ **`seedsmith items generate --kind combination --shape strain|splice`** runs, prints the plan
      plus the catalogue report plus the legacy-retirement measurement as JSON, and `--sample-brief`
      prints a real assembled brief. **`--write` is refused with a reason** (exit 3) rather than
      silently writing nothing, and **`--population` is refused for a combination** (exit 2) rather
      than ignored — the grid is closed, so there is no species/build split to make and silently
      accepting the flag would let a caller believe they had selected something
- [x] **No resume ledger, deliberately.** The spec's own Commands block says 102 is small enough not
      to need the `demons run` harness; module 13 built one because it faced ~1,800. A ledger here
      would be machinery with no failure to survive, and `plan_run` is byte-identical across runs
      (asserted over the subject dicts, the assembled briefs **and** the summary), which is the
      property that makes re-running safe instead

**⛔ Two defects / stale claims found while building, both measured rather than asserted:**

1. ⛔ **Module 16's `SocketGeometry.ValidateEntry` doc comment quotes a socketMax distribution that
   is one day out of date.** It reads *"module 6 measured `armament-primary` at `{0:18, 1:26, 2:4}`"*
   over a *"shipped 740-entry corpus"*; the live figures are `{0:10, 1:10, 2:10, 3:10, 4:8}`. ⚠ **The
   RULE the comment defends is unaffected and still true** — re-verified here across all 740 entries,
   no role's declared `socketMax` exceeds its ceiling — so this is a stale citation, not a broken
   check. Filed rather than hand-edited because the same figures appear in **P4.3's own entry** (and
   its *"720 live entries walked"* is now 740). **Cross-referenced into P4.3.**
2. ⛔ **`spec-strain-splice-gen.md`'s central inertness claim is stale (T1 above), and its
   `strain-splice.v1.json` row is stale by one module (T3).** Recorded here rather than absorbed,
   because a spec that says a built module is *"inert until"* something that already happened is how
   the next reader skips it. The corrections are pinned by tests against the live corpus, not by this
   paragraph.

**Three judgement calls the spec does not state, all named:**

- ⚠ **No new tuning file for module 16's numbers, and the parser enforces that rather than trusting
  it.** `SOCKETS_OWNED_KEYS` lists all six; `_refuse_forked_keys` walks the document's KEYS at every
  depth (not its text — the ownership note names all six in prose deliberately) and raises. The
  alternative, copying `ingredientCount` into this module's file, is precisely how a generator and a
  matcher come to disagree about how many ingredients a Strain takes.
- ⚠ **A combination may GRANT only from the same closed family vocabulary its ingredients are drawn
  from.** An atom family no gem supplies is one no insert can carry, so granting it would put the
  payoff outside the layer's own vocabulary — and it is what makes
  `Registration/IngredientUnsatisfiable` a sufficient check rather than half of one. Stated in
  `run.granted_family_vocabulary`'s docstring, because it is a design choice, not an implementation
  detail.
- ⚠ **A Splice cell carries NO `themeKey`.** It is a *pair* of build themes, not a 37th one, and
  minting `build.might-agility` here would add a row to a registry module 13 owns. A Strain cell
  reuses module 13's existing key verbatim; asserted both ways.

**⏸ Deferred, each with its owner named — none silently skipped:**

- [ ] ⏸ ⭐ **THE GENERATIVE AUTHORING PASS ITSELF — 36 Strains + 66 Splices — is out of scope for this
      pass, and this is the honest boundary, not a gap in the build.** It is 102 live model calls; a
      coding session cannot make them. **Who runs it:** the owner, from their own terminal, once X7
      lands a container home. Everything the run needs is built: the grid derives, the ids mint, the
      schema is audit-clean, the supply prechecks, the briefs assemble, the validator judges and the
      catalogue report prints. **Until it runs, `data/seed/items/combinations/` does not exist and the
      102 are 102 ids with no rows — by design, not by omission.**
- [ ] ⏸ ⛔ **The `socket-word` → `combination` kind rename is a FIVE-SITE BUNDLE that lands with the
      run, and one of its five sites is a frozen registry.** Encoded as executable analysis in
      `combogen/migrate.py` (`MIGRATION_SITES`, asserted to still exist) rather than as prose:
      **(1)** the gating metric — ✅ **done here**, and done permanently rather than at cutover;
      **(2)** `adapters/items/kinds.py`'s `KindSpec`; **(3)** `tools/ItemSeedValidator/Registries/KindCatalog.cs`,
      the C# port the Python list mirrors; **(4)** `naming.v1.json`'s `idNamespaces.socketWords` +
      its `sockword.{seq:03}` template — **`registryVersion 4, "frozen": true`**, which the spec's own
      Boundaries put under **Ask first** and which `NamespaceAllocation.ByNamespace` reads, so
      renaming (3) without bumping (4) breaks the validator's partition allocation; **(5)** the 25
      entries, which are **model-call output** under the "regenerate, do not retain" ruling.
      ⚠ **Not one of (2)–(5) is separable without leaving the corpus worse than either endpoint** —
      renaming the kind over the legacy rows gives a `combination` kind whose every row fails its own
      required fields, and deleting the 25 with nothing to replace them empties the only input a
      `gates = True` metric has. So the bundle waits for the content. `kinds.py` still holds **15**
      kinds and still names `socket-word`; asserted, so "renamed, not removed" survives the wait.
- [ ] ⏸ ⛔ **The evidence for "regenerate, do not retain" is measured and it is unanimous: NOT ONE of
      the 25 legacy socket-words is a legal combination today.** `legality_report()` names every
      reason per entry — 25 take 2 or 3 ingredients instead of D20's 4, 25 carry `position` (D41), 25
      use the retired `gem.word-*` runtime prefix, 25 declare a `minSockets` that is not the derived
      value, and **4 are hosted on a role whose ceiling cannot reach four** (`footing`×1,
      `armament-secondary`×2, `ward-array`×1) — only one of which is `ward-array` outside the
      twelve-role hybrid core; the other three roles are inside the hybrid core but still below the
      4-ceiling `{armament-primary, core-guard}` set `legality_report()` checks against. Module 16's
      standing test
      (`The_legacy_socket_word_corpus_is_ordered_and_awaits_module_21s_retirement`) is left untouched
      and still green — this is its measured companion, not its replacement.
- [ ] ⏸ **The generation graph is not wired, and `--write` says so instead of writing nothing.** A
      `workflow/graphs/item_combination.py` (mirroring `workflow/graphs/effect_affix.py`) is what
      connects `plan_run`'s subjects to `llm_caller`. Deliberately not stubbed, for module 13's
      reason: a graph that silently produces nothing is worse than a command that refuses.
- [ ] ⏸ **X7 — `ContainerKind` gaining D27's `combo` value — effect-atom's, and the same blocker P3.1,
      P3.2 and P4.3 all carry.** `ContainerRow.cs` is still six values. **Consequence, stated plainly:**
      the 102 have recipe rows waiting for them in `socket_combo_recipe` (module 16's
      `SeedComboRecipes` never deletes a row it did not write) but **no container to bind their atoms
      into**, so the run's output cannot be persisted as effects until it lands. A wiring gap with a
      named owner, not a wall.
- [ ] ⏸ **The per-actor cap is TUNED but not ENFORCED — module 12's evaluator, at assignment time.**
      `maxCombosPerActor: 3` ships in `sockets.v1.json` (module 16) and this module asserts it stays a
      **non-binding backstop above the geometric ceiling of 2**. Nothing counts combinations across
      *equipped items* yet; the spec places that in the threshold evaluator — citing item-ideal.md §2g #8
      only for the raw per-actor-cap number, and that citation itself quotes §2g #8's pre-2026-09-04
      wording, not its corrected "ceiling is 2, backstop 3" text — and the spec says **"named here; not
      built here."** Restated so it stays named.
- [ ] ⏸ **Module 20's compendium reveal and socket-UI preview are REQUIREMENTS, not niceties — and
      only half of the pair exists.** P5.4 built `CombinationDistance` on module 16's write-free
      `Preview`, with the four display states and the multiset swap distance D41 forced. The
      **compendium reveal** (*"a combination is revealed once the player has held every ingredient at
      least once"*) has no owner-side state and is not built. Both are carried in this module's
      catalogue report with `owner: module 20 (item-surfaces)` so a run cannot print 127 without
      printing who owes the mitigation. **Cross-referenced into P5.4.**
- [ ] ⏸ **`naming.v1.json` registryVersion 5 — the `socketWords` → `combinations` idNamespace and its
      `sockword.{seq:03}` template — is an ASK-FIRST on a frozen registry and is not done here.** The
      grid mints no `{seq:03}` at all (a Strain's identity is its cell, not its position in a wave),
      so nothing this module built is blocked by the bump not having happened; it is a registry
      ceremony bundled with site (4) above, and the owner owns it.
- [ ] ⏸ **`SemanticDedup/NearDuplicate` and `SemanticDedup/ExactDuplicateName` over the 102 — the
      thresholds ship, the population does not.** `exactDuplicateNamesMax: 0` and
      `nearDuplicateRateMaxPermille: 5` are in the tuning file with their derivations beside them
      (the second carried from module 13 rather than re-derived, so both generated item populations
      are judged on one bar), and at n = 102 a 5‰ rate is **0 pairs** — recorded in the file, because
      a granularity effect nobody writes down gets rediscovered as a bug. ⚠ Module 13's defect 4 (the
      shared metric's MinHash over-reports by up to 7× on short names) applies to this population too
      and is **still not fixed in the shared metric** — its owner is whoever owns that metric's
      62-finding baseline, unchanged.
- [ ] ⏸ **`bind_ordinal` on `effect_binding` — effect-atom E6's, restated because this module is the
      case that makes it bite.** A four-ingredient combination is exactly where two identical inserts
      tie in a sort `definitions.md` §5 requires to be total. Module 16 built the socket half
      (`BindOrdinalFor`, display-order only, with a comment saying a matcher that reads it is a bug);
      the column is another program's table and is not ours to add.

**Verification, run and green:**

| Command | Result |
|---|---|
| `python -m pytest tests/test_strain_splice_gen.py -q` | **52 passed** (new) |
| `python -m pytest` (seedsmith, full) | **1678 passed, 1 skipped, 288 subtests** — exactly the **1626** measured at the start of this session plus this module's 52 |
| `python -m seedsmith items generate --kind combination --shape strain --dry-run` | **36 subjects, complete true**; 34 supplied families from 40 gems; hostRoles `[armament-primary, core-guard]`; `geometricCombosPerActor: 2`; catalogue `127 / bar 45 / 2822‰` |
| `python -m seedsmith items generate --kind combination --shape splice --dry-run` | **66 subjects, complete true** — 36 + 66 = 102, all ids distinct |
| `python -m seedsmith items generate --kind combination --shape strain --write` | **refused, exit 3** — the graph is not wired and the rename touches a frozen registry |
| `python -m seedsmith items generate --kind combination --population build` | **refused, exit 2** — a combination's grid is closed |
| `python -m seedsmith check … --adapter items --metric Registration/IngredientUnsatisfiable` | **no findings** — the `gates = True` port still gates, now over both kind ids |
| `python -m seedsmith check … --adapter items --gate` | **61 gap / 80 note / 14 not_measured** — **byte-identical to P3.3's recorded set.** The metric rename and the dual-kind lookup move nothing |
| `dotnet test tests\FusionRpg.Core.Tests --filter StrainSplice` | **22 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items."` | **720 passed / 0 failed** — the whole item program, modules 1–20's own suites included, green under this module's Core additions (698 before) |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Items."` | **131 passed / 0 failed** — the item program's whole DAL half |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **7004 passed / 6 failed** — ⛔ **Re-checked 2026-09-05 after commit `20743ba` landed the concurrent stream's edits: only half the attribution holds.** `Atoms.PredicateCompilerTests` (21/21) and `ActorHub.SpecChannelClaimTests` (2/2) now pass — confirms they were `PredicateNode.cs`/`RespecPolicy.cs` mid-edit, now resolved. But `Expeditions.ExpeditionResolverTests.Tier_goldens_are_locked` and all 3 `ClassSystem.ProveAptitudeJsonEmitTests` **still fail** after `RpgStore.Aptitudes.cs`/`AptitudeEndpoints.cs`/`ExpeditionEndpoints.cs` are already committed — not those files. Root cause is the battle-tempo/battle-resources stream, still mid-edit: `Squad()`'s golden hash reads `BattleRuleset.BaseHp/Atk/Defense`, and `ProveAptitude`'s tool process hits `BattleStatComposer.Configure(...) has not run` — both trace to `BattleModels.cs`/`BattleStatComposer.cs`/`ContractTuningTestBootstrap.cs`, not to this claim's five files. **Zero in `Items.*`**, and this module added only two new Core files |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **823 passed / 0 failed** — identical to the baseline measured at the start of this session, with the same intermittent *"Test host process crashed"* after the last test |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | **203 passed / 1 failed** — `ClassSystemBaselineRegenTests.DominanceBaseline_coverageNamesEveryAxisHonestly`, which measured **204 / 0** at the start of this session and went red **while it ran**, on the concurrent stream's class-system tuning drift (`docs/research/class-system/_baseline-dominance.json` is mid-edit in `git status`). Not this module's: nothing here touches class-system, and the failure is the already-recorded dominance-baseline drift |
| `dotnet build src\FusionRpg.Server` | **0 warnings, 0 errors**; `strain-splice.v1.json` copies to the output tree |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors across 120 partitions — identical to the baseline measured at the start of this session.** Zero new findings; this module authors no corpus row |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0**, 13 total. **Zero** findings under `Items/Sockets/` — every number a balance pass would touch is in `strain-splice.v1.json` or `sockets.v1.json` |
| `python scripts\audit-overflow.py` | **0 critical**, 59 findings, **zero** naming `StrainSplice`. The module holds no magnitude of its own — counts, tiers and ordinals are shape indices |
| `.\scripts\guard-dal.ps1` / `guard-single-writer.ps1` / `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` | **all four OK** |

⚠ **Baseline re-measured fresh at the start of this session, not carried forward.** `Items.` measured
**698 / 0** in Core and the validator **170** before any of this module's code (P4.3 recorded 481 and
166 — modules 17–20 and the owner's own 2026-09-04 corpus commits moved both). `Data` measured
**823 / 0**, `Guard` **204 / 204**, seedsmith **1626 passed / 1 skipped**.

⚠ **The Core and Data builds broke twice mid-session on `SiegeTuning.cs` / `ContractTuningTestBootstrap.cs`**
— the concurrent stream adding a `SiegeShootingTuning` parameter, on files this module never touches.
Both resolved on retry, exactly as expected.

⚠ **No test outside this module was edited.** The one shared file this module changed behaviourally
is `metrics/linkage.py`, and its finding message is byte-identical for a `socket-word` row — the
`position` clause is emitted only when the field is present, which it always is on the legacy corpus.
`test_linkage.py` and `test_parity_seed_graph.py` are both green, unmodified.

**Files:** `data/tuning/strain-splice.v1.json` (new — the min-tier plan, the per-shape base tier, the
learnability bar, two distinctness thresholds, and an ownership note naming the six keys it refuses
to duplicate); `tools/seedsmith/seedsmith/adapters/items/combogen/{__init__.py, grid.py, tuning.py,
supply.py, schema.py, brief.py, emit.py, catalogue.py, migrate.py, run.py}` (new);
`tools/seedsmith/seedsmith/metrics/linkage.py` (EDIT — `COMBINATION_KINDS`, `SocketWordIngredients`
→ `CombinationIngredients`, the gate reads both kind ids);
`tools/seedsmith/seedsmith/planner/schedule.py` (EDIT — `combination` joins `invents_identity`
beside `socket-word`); `tools/seedsmith/seedsmith/report/cli.py` (EDIT — `items generate --kind
combination --shape strain|splice`); `src/FusionRpg.Core/Items/Sockets/StrainSpliceGrid.cs` (new —
the derived grid, the five content rules, `StrainSpliceRules`);
`src/FusionRpg.Core/Items/Sockets/StrainSpliceTuning.cs` (new — the pure parser, cross-validated
against `SocketTuning`); `src/FusionRpg.Server/Program.cs` (EDIT — parses `strain-splice.v1.json` at
boot and validates every combo recipe on the seed path against the derived grid);
`tests/FusionRpg.Core.Tests/Items/StrainSpliceGridTests.cs` (new, 22 tests);
`tools/seedsmith/tests/test_strain_splice_gen.py` (new, 52 tests).

**Verify:** `cd tools\seedsmith; python -m pytest tests/test_strain_splice_gen.py -q`;
`python -m seedsmith items generate --kind combination --shape splice --dry-run`;
`python -m seedsmith check ..\..\data\seed\items --adapter items --metric Registration/IngredientUnsatisfiable`;
`dotnet test tests\FusionRpg.Core.Tests --filter StrainSplice`

> ### ✅ CHECKPOINT 4
> salvage → craft → enhance → socket is a closed loop on one item. `CraftingHorizonReport` prints.

---

## Phase 5 — content breadth and the player surface

### ✅ P5.1 — Module 17 `uniques` — BUILT AND VERIFIED 2026-09-05 (the seed→concrete generator, D39's `Override` and the two private-atom lints explicitly deferred to their real owners — all three either upstream or downstream, none skipped)

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped seedsmith
drop-table corpus already carries **144 `unique` entries** — by far the largest block of the 315
currently-unresolvable rows — because wave R2 added the `unique` entry kind precisely so the 144
authored uniques would stop being *"referentially perfect and unobtainable"* (`entry-shapes.md` §9).
Module 11's importer refuses each by name with `ContentRuleViolated{drop.entry-kind-unavailable}`,
naming this module. ⚠ Also note `entry-shapes.md` §9's band→channel table (`acquisition = 'drop'` at
ordinal ≥ 90 is `UniqueUnreachable`, so band 90 never appears in d1) — module 11 does not enforce that
rule, and this module owns it.
→ ✅ **Both halves answered here.** The band→channel rule is **built and green against the real drop
corpus** (`UniqueCorpusValidator.ValidateDropReferences`; the shipped corpus partitions the three
channels exactly — d1 holds all 40 `drop` uniques, d2 all 64 `source-locked`, d4 all 40
`deterministic`, and the 40 at ordinal ≥ 90 are all in d4). The **entry kind stays refused**, and its
reason MOVED rather than being left pointing at a module that now exists — see the deferred list.

⭐ **The 144-row corpus was AUTHORED AND NEVER WIRED — the same pattern as `item_role_family`,
`nameWords`, `displayTemplate` and `UnitClass` before it.** `data/seed/items/uniques/*.json` shipped
2026-08-22 — 8 entries × 18 partitions, every `baseType` resolving, zero axis collisions — and **not one
line of Core read a single row until this module.** So had `core.v1.json`'s `counterPressure` registry
(wave 0c), whose own `_note` says it was added *because* "ssot-uniques.md requires every unique to
carry a drawback, a condition, or deliberate narrowness, and the validator rejects one that does not":
the registry existed, the validator did not. This module is therefore a **wiring pass plus the
validators**, not a from-scratch build — checked before assuming, exactly as modules 6/7/8/10 were.

⛔ **Five doc corrections, each checked in the file the doc cites, recorded rather than absorbed:**

| # | The doc says | Verified | What shipped |
|---|---|---|---|
| **U1** | `spec-uniques.md`: `AtomKindRegistry.KindCount = 12`, the twelve enumerated at `:197-476` | **16.** The vocabulary grew under another lane since the lane doc was written | Nothing asserts 12. The test asserts `KindCount == All.Count` and that **`damage.convert` is absent** — which is the fact this module actually depends on |
| **U2** | `spec-uniques.md`: *"`AtomRejectionReason` has 34 members and `ContentRuleViolated` is not one of them"*, and adding it is an **Ask first** | **35, and `ContentRuleViolated` IS one of them** — added by an earlier item module under item-ideal §2b.1. `ContentRuleNamespaces` is the registration mechanism | The ask is already granted. All **nine** `unique.*` rule ids raise that one code; the enum stays **35**, asserted. No member minted |
| **U3** | `ssot-uniques.md` §3.6 / §5.1: a unique's shape is `pool_rolls ≤ 1` | **`pool_rolls` no longer exists** — `PrefixRolls`/`SuffixRolls` replaced it (T3.2), confirmed by reflection over the shipped `ContainerRow` | The rule is `PrefixRolls + SuffixRolls ≤ 1` (`UniqueLimits.MaxTotalRolls`), and a test asserts **no `PoolRolls` property exists** so no code path can read one |
| **U4** | `naming.v1.json idNamespaces.uniques`: `partitionCount: 20`, `totalCombinations: "20 (matches authoring-fleet-plan.md's 20 agents exactly)"`, `agentsEach: "~15 uniques"`, `themeSource: "themes.v1.json (15 themes)"` | **All four stale.** The file's own `bandAssignment` table lists **18** rows (5 + 5 + 3 + 5), the corpus ships **18** partitions × 8 = **144**, and `themes.v1.json` holds **13** — which the same block's own `themeCountNote` already says | Not edited (another lane's registry). Named as a defect below and cross-referenced into **P2.3** |
| **U5** | `spec-uniques.md` §3.2's own ⚠: the lane quotes a comment at `ContainerValidator.cs:87` that is not in the file | **Stale — the line moved in the same commit.** The rarity-bands wiring (the static-constructor registration plus the `rarityExists` `<param>` doc) landed just above the pool loop and pushed everything down ~12 lines: `:87` is now the T3.2 mixed-class-group comment, and the negative-weight rejection sits at `:95-97`. The *behaviour* is still real and still proven here from the loop structure | The premise is asserted against the shipped validator, not against a comment (`a_fixed_core_atom_out_of_band_loads_clean` plus its negative twin) |

**What was built:**

- [x] ⭐ **G1's premise proven against the SHIPPED validator, both ways.** An out-of-band fixed-core
      magnitude (t1 atom overridden to 120–138 against a window of t3) loads **clean**; the identical
      tier offered from the **pool** is refused `TierOutOfWindow`. `ValidateOverrides` is proven to check
      well-formedness only — a 9000-magnitude override passes, an inverted `Min > Max` does not. This is
      the fact the whole class rests on and it is now a test, not a paragraph
- [x] **`UniqueRow.cs` — ssot §5.2's nine columns**, plus the three closed vocabularies
      (`UniqueCounterPressure`, `UniqueAcquisition`, `UniqueEnhanceScope`) and `UniqueContainerIds`
      — ⭐ **the seed-id → container-id derivation `naming.v1.json` explicitly left open "for
      wave-1b"** (its own `idVsContainerIdNote`: the corpus's `unique.` tracking id *"does not have a
      `unique.` alternative in definitions.md §1's container_id alternation"*). `unique.{slug}` →
      `item.{slug}`, body verbatim, invertible; all 144 derived ids pass the shipped container-id
      grammar and are distinct
- [x] ⛔ **Three structural limits, each carrying the AGENTS.md exemption comment that rule requires**,
      and a test that greps for the words so a tidy-up cannot delete the justification:
      `MaxTotalRolls = 1` (**the class's own definition** — a tunable here would let a balance pass
      author a rare with a name), `UniquesArePromotable = false` (promotion only ADDS pool draws;
      **D7 lifted the rung ceiling and did not lift this one**, asserted alongside `PromoteFrom == 1`
      for all ten rungs), and `FixedCoreChannelWeightMilli = 0` (**there is no draw for a weight to
      modify** — the one line that stops a reviewer reading L0's coverage report as a gap)
- [x] **`UniqueValidator.cs` — the per-row import checks, all nine rule ids under one code.** Returns
      **every** failure rather than first-fail, because 144 rows reported one problem at a time is 144
      round trips. ⭐ **AE is priced from the atom's TIER, never its raw parameter**: a core may hold hp,
      per-mille and millisecond params at once and SC4 forbids summing across those units, so tier — the
      unit the AE unit is *defined* against — is the only unit-safe basis. The raw value is read for
      exactly two unit-free things: the **sign** of a drawback and the **±15% spread** (a ratio)
- [x] ⭐ **Device 1 — counter-pressure CHECKED against content, never trusted, all three arms.**
      `drawback` reads a negative value spec **and asks the kind first** (sign carries meaning per kind:
      a negative `box.set` param is a malformed row, not a cost — tested); `conditional` requires a
      non-empty `when.predicate` object; `narrow` compares summed raw-stat AE against ‰ of the rung
      baseline. ⭐ **§3.2's corollary is a test, not a sentence**: an item that is only three fat
      positive raw-stat lines is refused **whichever of the three arms it declares**, and the budget
      catches it a second time — so the class cannot be forged by picking the right declaration
- [x] **Device 2 — the budget, with the ±25% drift PINNED at load.** `UniqueTuning.Parse` refuses any
      tuning whose `budgetDriftTolerancePercent` differs from `ContentValidation.DriftTolerancePercent`
      in either direction — definitions §7 owns that number and this file reuses it, the same device
      module 9 used for `powerDisplayBandPercent`. Declared-vs-summed is checked in **both** directions
- [x] **Device 4 — the four cross-row checks, import-phase, over the whole corpus** (§6.4: *"cross-row
      checks MUST be import-phase; they are properties of the catalog, not of a row"*). Axis collision
      keyed on `(rung band, role, power axis)` — ⭐ **the band comes from the PARTITION, not the entry's
      rung**, because each band spans two rungs and splitting by rung would double the grid from 40
      slots to 80 and quietly retire "exactly saturated at 144". Measured: **144 distinct keys, zero
      collisions**, both saturated bands using all 40 of their slots
- [x] ⭐ **§9.1's missing publication now exists.** The lane asked module 7 for *"the rolled baseline in
      AE per rung, which §3.7's budget check divides by and which does not exist in any document yet."*
      `UniqueBudget.RungBaselineAeHundredths` derives it from the **seeded ladder** rather than
      authoring a second table — monotone up the rungs, `chaff` (the one rung with no pool) exactly 0,
      `almanac` 500. ⚠ It reads the count-band **FLOOR**, because the shipped schema has no
      `pool_rolls_max` (module 7's own recorded ask-first), so it **understates** the allowance and every
      caller that reports rather than refuses says so in its own `Basis` string
- [x] ⭐ **`unique_eligible` — the tenth `rarity_budget` key, and the ONE key ssot §5.3 asked for.**
      Shape: one 0/1 integer per rung, **derived** from the ordinal against `uniques.v1.json`'s
      `rungFloorOrdinal` rather than authored as a second per-rung table beside the seeded ladder;
      seeded by `RpgStore.SeedUniqueEligible`, which reads the `rarity` table's **own ordinals** rather
      than list position (the ladder is pre-spaced by 10 so a rung can be inserted later). Cross-
      referenced into **P2.1**
- [x] **`RpgStore.ItemUniques.cs`** — the nine columns, upsert/get/list, a live FK to
      `effect_container` with `ON DELETE CASCADE` (a unique is a **flag on a container** and cannot
      exist without one — asserted), and `IsUniqueSetMember`, which turns §3.8's *"hard no"* into a
      query rather than a promise. `guard-dal` green
- [x] **Every number a balance pass would touch is in `data/tuning/uniques.v1.json`, and the parser
      REFUSES rather than defaults.** Stripping any of the ten keys throws at load, asserted key by key
      against the real file. Two structural invariants are checked at parse time: the drift pin above,
      and a parity band that must be a real band inside 0…1000‰ (an inverted one would make every
      reading simultaneously "too strong" and "a trophy", which reads as a metric working). A
      `forbiddenRoles` entry naming a role that is not in the core registry is refused — a ban on a role
      that does not exist bans nothing and reads as protection
- [x] **`audit-magic-numbers.py --summary` reports `M1 = 0` AND `M2 = 0`, exit 0**, with **zero**
      findings in the `uniques` domain; `audit-overflow.py` reports **0 critical** and **zero** findings
      under `Items/Uniques/`. ⚠ Two structural consts (`AeScale`, `FixedCoreChannelWeightMilli`) matched
      `BALANCE_WORD` on the substrings "scale" and "weight" and were added to the audit's
      **`EXEMPT_NAMES`** with the documented-reason discipline that list already uses — the established
      mechanism (`MaxTier`, `ReferenceLevel`, `ReferenceStar` sit there for the same reason), **not** a
      rename to dodge the check

**⭐ Device 3 — the parity invariant, MEASURED for the first time, and module 7's anticipation paid off:**

- [x] ⭐ **No second simulator, and it is asserted structurally.** `spec-uniques.md` forbids one by
      name; module 7 built `RarityOverlapSimulator` saying explicitly it claimed the invariant *"because
      the only would-be consumer (`spec-uniques.md`) declined to build a second simulator."*
      `UniqueParityMetric` calls that harness — same `Seed`, same `RollsPerRung`, same `UpsetRate`
      paired comparison — and §9.2's exact ask (*"the same measurement with a fixed-value item on one
      side, run on the same code with the same seed"*) is what the fixed side **literally is**: an array
      of the unique's own magnitude. A test walks every file under `Items/Uniques/` and asserts none
      names `SeededRng` or `new Random`
- [x] **The one parameter that differs, and why.** The rolled side draws **one** affix, not the rung's
      whole count band: parity is measured *within one channel family* (SC4 forbids cross-family
      totals) and the one-atom-per-group rule means a rolled rare's total inside a single family is
      exactly one affix however many it draws overall. Module 7's own §3.5 measurement is about a rung
      beating the rung below it; this is about one line beating one line
- [x] ⭐ **The threshold is LIVE.** `spec-uniques.md` said to ship parity *"as a reported metric with no
      threshold **until the harness exists**, and say in the report that it is unbounded."* The harness
      exists (module 7, 2026-09-04), so `UniqueParityReport.HasThreshold` is **true** and the band comes
      from `uniques.v1.json`. ⛔ It bounds a **report**, not an import refusal — the three HARD devices
      are counter-pressure, budget and anti-convergence, device 3 was never one of them, and making it
      hard on the day it first became measurable would refuse authored content against a number nobody
      has yet had a chance to author against
- [x] ⛔ **The measurement does not come out green, and that is the point of having one.** 287 readings
      (one per unique × identity atom) over the real corpus: **90 in band, 47 strictly-better
      (`W < 25%`), 150 trophy (`W > 75%`)**. The shape is systematic rather than random — identity
      `powerBand`s were chosen largely independently of the item's rung, so a `low`-band line on a
      `sunwoven` item loses to a rolled `sunwoven` affix **every time** (`W = 1000‰`), which is §8.4's
      trophy failure exactly. Pinned as a corpus regression so a re-authoring pass can watch it move,
      and reproducibility is its own test

**⛔ Real defects found, named, not silently fixed:**

- [x] ⛔ **Three shipped uniques carry a family their own frame cannot execute — a NEW check found
      them, and the lane's own named example is clean.** ssot §3.5 draws a line inside the frame filter
      that no registry encoded: a unique may bypass it where the filter is **taste** and may not where
      it is **physics** (a channel that only exists on the other side). Its example is
      `plating`/`carapace` on a plant — **no shipped unique carries either**. Three carry different
      members of the same class: `unique.sunwoven-almanac-90-006` ("Hypocotyl of the Precept", **plant**)
      carries `atom.swiftness` → `zombieSpeed`, family `frames: ["humanoid"], side: "zombie"`;
      `unique.umbral-swarm-50-004` ("Encroaching Leash", **humanoid**) carries `atom.quickening` →
      `attackInterval` **and** `atom.flourishing` in its variance slot, both plant-only; and
      `unique.umbral-swarm-50-005` (**humanoid**) draws `atom.quickening` in its variance slot. Four
      findings across three rows — the rule covers the variance slot too, because a pool that can only
      ever draw a dead line is the same defect one step later. **Not hand-fixed** (`ItemSeedValidator`'s
      own footer: *"Re-run the partitions named above; do not hand-fix"*) but **reported by name**: new
      check `UniqueFrameCheck.cs` (`UniqueFrameImpossible`), wired into `Validator.cs`. **This moves the
      validator baseline 166 → 170**, and all four new errors are these rows. Owner: the authoring
      fleet's `uniques/sunwoven-almanac/90` and `uniques/umbral-swarm/50` partitions. Also pinned in
      Core so the set cannot grow silently
- [x] ⛔ **36 of 144 uniques price above `baseline + 1.5 AE`, and 12 of the 98 declaring `narrow`
      exceed its 60% ceiling — REPORTED, deliberately not refused.** The seed corpus authors no
      `budget_ae` at all (seed-contract §3 forbids a number in a seed), so the summed side is priced by
      **this module's own** band → tier → AE reckoning rather than by anything an author wrote. Refusing
      144 authored rows against a price they were never given a way to see is a validator invented after
      the fact, not a validator working. The hard check runs where a declared `budget_ae` exists (the
      concrete container). ⚠ And the count is an **upper bound**: the baseline reads the count-band
      floor. §7.2's own worked example fails its `narrow` check by four points and the lane kept it —
      *"the check has teeth"*
- [x] ⛔ **`naming.v1.json idNamespaces.uniques` is stale in four places** (U4 above): `partitionCount`
      and `totalCombinations` say 20 against its own 18-row `bandAssignment`, `agentsEach` says "~15
      uniques" against a shipped 8, and `themeSource` says 15 themes against `themes.v1.json`'s 13 —
      which the same block's `themeCountNote` already corrects. Nothing reads the stale numbers, so this
      is a documentation defect, not a behaviour one; naming is **module 8's** lane, so it is
      cross-referenced into **P2.3** rather than edited from here
- [x] ⛔ **Five phantom affix families are named by the shipped unique corpus** — `atom.bonding`,
      `atom.buttering`, `atom.chilling`, `atom.marking`, `atom.rotting` — none of which resolves to an
      affix-family row, so their kind is unknown. They are **excluded from `narrow`'s raw-stat subtotal
      rather than guessed into it**, because guessing would make an unresolved reference look like a
      balance failure. This is **module 10's already-filed phantom-family defect** (P2.5's list of eight)
      reaching this corpus; pinned here as a set so it cannot grow

**⏸ Deferred, each with its owner named — none silently skipped:**

- [ ] ⏸ **The seed → concrete generator — the runtime generator's, per the binding seed-to-concrete
      rule, and it is the single reason three other items below are still open.** The corpus holds 144
      **seeds** (families and bands, never numbers); no `effect_container` row exists for any of them.
      Everything this module owns operates on either the seed (the corpus validators, the reports) or on
      a concrete container supplied by a caller (the per-row validator, `item_unique`). Rolling a seed
      into a container with its private atom rows is a shared-SDK job, not this module's
- [ ] ⏸ **The `unique` drop ENTRY KIND stays refused, and the reason MOVED.** Module 11's
      `DropTableDraw.UnavailableKinds[Unique]` read `"module 17 (uniques)"`; module 17 exists, so that
      pointer is now stale in exactly the way this program keeps naming. Updated in place to name the
      real remaining blocker (no concrete unique container exists, so a draw resolves to nothing) and
      pinned by a test that greps the reason for `seed-to-concrete`, so it cannot go stale a second
      time. ⚠ The **band→channel rule itself is built** and green over the real corpus — the two are
      different obligations and only one of them was blocked
- [ ] ⏸ **The general-channel marker is the drop-table lane's, not ours.**
      `ValidateDropReferences` takes `IsGeneralChannel` **as a parameter** because the shipped
      drop-table schema carries no channel field — `entry-shapes.md` §9 states the rule and the row has
      nothing that says which channel a table is. Inventing a field would be this module authoring
      another lane's schema; the test reads it from the `droptable.d1-` id prefix and says so
- [ ] ⏸ **D39's `Override` op and its damage applier — effect-atom's, and NOT started deliberately.**
      Verified today: `AtomRowValidator.StatOps` is still `flat|increased|more`, and
      `AtomKindRegistry.cs:336` refuses `Override` for `stat.modify` by name. The ruling is explicit
      that *"the consumer is part of the ask, not a follow-up"* — an `Override` that binds to nothing
      would be the third instance of the `status.expose.*` / `stat.derived` defect — so this module
      adds neither half and pins both as absent (`D39s_override_op_and_the_thirteenth_kind_are_both_still_absent`)
- [ ] ⏸ **`damage.convert`, the 13th kind — recorded as an ask, depended on by nothing.** Asserted
      absent, and asserted that no kind id contains "convert", so nothing here can quietly start
      needing it
- [ ] ⏸ **§4.6's private-atom rule and §8.6's "referenced by exactly one container" lint — both need a
      concrete container to lint.** They are the right rules (a shared `vitality.t5` row with an
      out-of-band override bricks every dropped copy at the next bind, with a code that blames the
      instance), and neither is checkable while zero unique containers exist. Owner: whoever lands the
      seed→concrete generator; the rule is recorded here so it lands with it rather than after
- [ ] ⏸ **`item_base_type` has no table, so `derived_from` carries no FK.** ssot §5.2 wants
      `FK → item_base_type`; module 6 shipped the 740-row corpus and the Core readers, **not a table**,
      so the FK has nothing to point at. The reference is checked instead by `UniqueCorpusValidator`
      against the loaded base-type registry, which is where the role and frame rules already resolve.
      A **wiring gap**, named with that word; the column is ready the day the table exists
- [ ] ⏸ **`unique_value_reroll` — module 15's surface (§10.5), not requested from here.** ssot §8.4
      names the conditional it creates: if module 15 refuses the operation, `identitySpreadPerMille`
      should narrow from 150 to 100 so a bad copy hurts less. That is a one-number edit in
      `uniques.v1.json`, and the tuning file's own note records it so the conditional is not lost
- [ ] ⏸ **Whether a unique is salvageable (§10.6) — module 14's, recommendation "no", not decided
      here.** Likewise the `no_reassign` flag being driven by `acquisition = 'deterministic'` (§9.11) —
      inventory's — and the flavour-text render (§9.12) — module 20's
- [ ] ⏸ **Relics stay on `rpg_unique_equipment`, confirmed again today, unchanged.** Four shipped
      relics served at `/api/relics` (`RelicEndpoints.cs:15`), rendered by `RelicsLayer.tsx`, with
      `RpgStore.UniqueActors.cs` still reading and writing that table. The row migration is **module
      4's** and **the stub must not be retired before relics have their home** — nothing here touches it
- [ ] ⏸ **Sim stays `None` for `stat.derived`, and that is the one real remaining runtime limit.**
      ⭐ The D6 quarantine the lane calls *"the largest single constraint on what this lane can author,
      larger than SC2"* is **lifted** — asserted, not assumed: `stat.derived` is `Full` on the lawn and
      `Full` in battle. §4.3's "practical palette" paragraph is void, and the lane doc still says
      otherwise, which is why the lift is a test rather than a note

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.Unique"` | **61 passed / 0 failed** (new — `UniqueTests` 44, `UniqueCorpusTests` 17) |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemUnique"` | **7 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items."` | **542 passed / 0 failed** — the whole item program, modules 1–16's own suites included, green under this module's two registry edits |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Items."` | **103 passed / 0 failed** — the item program's whole DAL half, green under the new `item_unique` schema |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6712 passed / 8 failed** — all 8 in `Actions.ActionsPurityGuardTests`, `Battle.*` (3), `ClassSystem.ProveAptitudeJsonEmitTests` (3) and `Expeditions.ExpeditionResolverTests`, the concurrent stream's own in-flight world/district work; **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **777 passed / 0 failed**, then the host process **crashed** on `DemonSpeciesImportCliTests.A_stale_committed_file_refuses_the_whole_import_and_writes_nothing` — the demon stream's own CLI-spawning test, reproducible under `--blame-hang`; **zero** failures anywhere, **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | **204 passed / 0 failed** — clean, zero-tolerance held |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors** (166 before this module). All **4** new findings are `UniqueFrameImpossible` on the three real corpus rows above; no other check moved |
| `python scripts\audit-magic-numbers.py --summary` | **`M1 = 0`, `M2 = 0`, exit 0**; the `uniques` domain reports **zero** findings |
| `python scripts\audit-overflow.py` | **0 critical**, 57 findings, **zero** under `Items/Uniques/` |
| `.\scripts\guard-dal.ps1` / `guard-single-writer` / `guard-funnel-delta` / `guard-secondary-no-unity` | all four **OK** |
| `dotnet build src\FusionRpg.Server\FusionRpg.Server.csproj` | succeeds — the new tuning load and `SeedUniqueEligible` do not break boot |
| `python -m pytest tools/seedsmith` | run — this module wrote no `tools/seedsmith/**` and no `data/seed/**` file; the only Python it touched is `scripts/audit-magic-numbers.py`'s `EXEMPT_NAMES`, which seedsmith does not import |

⚠ **Baseline re-measured fresh, not carried forward.** `Core` measured **7 failed / 6584 passed** at the
start of this session (all in `Battle.*`, `Expeditions.*`, `ClassSystem.*`) and drifted to 14 and back
to 9 while the concurrent stream landed `src/FusionRpg.Core/World/District/` — its `BattleSeam.cs` and
`BattleApplication.cs` did not compile at all for two windows mid-session, which resolved on retry
exactly as expected. `Guard` measured **201 passed / 1 failed** at session start (the known
`ClassSystemBaselineRegenTests` dominance-baseline drift) and is **204/204** now, closed by that
stream. Every failing name in the runs above was checked against `git status`: their sources
(`World/Turn/*.cs`, `World/Movement/*.cs`, `World/District/*`, `Battle/Timeline/*`, `Actions/*`,
`RpgStore.Aptitudes.cs`) are all mid-edit or brand-new in that stream and **none is touched by this
module.**

**Files:** `data/tuning/uniques.v1.json` (new — the ten tunables);
`src/FusionRpg.Core/Items/Uniques/{UniqueRow.cs, UniqueTuning.cs, UniqueBudget.cs, UniqueCorpus.cs,
UniqueValidator.cs, UniqueCorpusValidator.cs, UniqueCorpusReport.cs, UniqueParityMetric.cs}` (new);
`src/FusionRpg.Core/Items/RarityOverlapSimulator.cs` (EDIT — `TierCount`/`TierBand`/`TierMidpoint`
exposed so the parity metric prices the fixed side in the harness's own units instead of copying the
table); `src/FusionRpg.Core/Items/RarityBudgetKeys.cs` (EDIT — `unique_eligible` registered);
`src/FusionRpg.Core/Items/Drops/DropTableModel.cs` (EDIT — the `Unique` unavailable-reason moved off a
module that now exists); `src/FusionRpg.Data/Sqlite/RpgStore.ItemUniques.cs` (new — `item_unique` DDL,
upsert/get/list, `IsUniqueSetMember`, `SeedUniqueEligible`); `src/FusionRpg.Data/Sqlite/RpgStore.cs`
(EDIT — `EnsureItemUniqueSchemaUnlocked` in `Init`, after the container schema it keys on);
`src/FusionRpg.Server/Program.cs` (EDIT — parses `uniques.v1.json` at boot, then `SeedUniqueEligible`);
`tools/ItemSeedValidator/Checks/UniqueFrameCheck.cs` (new), wired into `Validator.cs`;
`scripts/audit-magic-numbers.py` (EDIT — two structural consts added to `EXEMPT_NAMES` with reasons);
`tests/FusionRpg.Core.Tests/Items/{UniqueTests.cs, UniqueCorpusTests.cs}` (new);
`tests/FusionRpg.Data.Tests/Items/ItemUniqueStoreTests.cs` (new).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.Unique"`;
`dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemUnique"`;
`dotnet run --project tools\ItemSeedValidator`

### ✅ P5.2 — Module 18 `consumables` — BUILT AND VERIFIED 2026-09-05 (X7's fifth container kind, the seed→concrete generator, the missing menu executor and module 6's `consumableSlots` explicitly deferred to their real owners — all four either upstream or downstream, none skipped)

⛔ **Addendum 2026-09-04, found while building module 11 (`drop-volume`).** The shipped seedsmith
drop-table corpus carries **60 `consumable` entries**, added in wave R2 (`entry-shapes.md` §9), while
`ssot-generation.md` §5.4 still says consumables are *deliberately absent* from `entry_kind` — *"adding
it now would ship a degenerate action mechanism that the action program then has to absorb"*. Both are
true at once: the seed vocabulary grew, the runtime arm did not. Module 11 refuses each entry by name
with `ContentRuleViolated{drop.entry-kind-unavailable}`, naming this module, rather than picking a
side. Landing the use path here is what makes those 60 entries drawable.

→ ✅ **Answered here, and the reason MOVED rather than being left pointing at a module that now
exists.** All 60 refs resolve against the shipped consumable corpus (asserted). The **entry kind stays
refused**, one step further on: X7 has still not minted the `consumable` `container_kind`, and even
once it has, the 60 are seeds with no `effect_container` row — see the deferred list. Cross-referenced
back into **P3.1**.

⭐ **The 60-row corpus was AUTHORED AND NEVER WIRED — the same pattern as `item_role_family`,
`nameWords`, `displayTemplate`, `UnitClass`, the 144 uniques and the 30-set corpus before it.**
`data/seed/items/consumables/{k1,k2,k3}.json` shipped **2026-08-22** — 3 partitions × 20 entries, every
one carrying a class, a use context, a family, a power band and a manifest cost — and **not one line of
Core read a single row until this module.** So had the whole atom-layer half of the lane's ask: the
eighth trigger (`OnActivate`, A18b), `LeafId.HoldsStock` (landed 2026-08-28) and `rpg_item_stock`
(module 2's table, shipped) all exist. This is therefore a **wiring pass plus the validators**, not a
from-scratch build — checked before assuming, exactly as modules 6/7/8/10/17 were.

⛔ **Five doc corrections, each checked in the file the doc cites, recorded rather than absorbed:**

| # | The doc says | Verified | What shipped |
|---|---|---|---|
| **C1** | `spec-consumables.md`: *"There are **8** triggers, not 7"*, citing `TriggerCount = 8` | **13.** E34 (`spec-trigger-vocabulary.md`) added `OnWave`/`OnMatchStart`/`OnMatchEnd`/`OnSunCollect`/`OnGridPlace` afterwards | Nothing asserts 8. The test asserts `TriggerCount == AtomTriggers.All.Length` and that `OnActivate` is in it — the fact this module actually depends on |
| **C2** | `spec-consumables.md`: *"Four kinds carry it"* — `stat.modify`, `resource.delta`, `status.apply`, `shield.grant` | **Five.** E41's `ui.present` takes `AllTriggers` too | The set is derived by asking the registry and asserted as five; the spec's four are all still there, so the drift is an addition, not a substitution. Harmless here — `ui.present` is Battle/Sim `None` and `PowerCategory.None`, so the runtime check refuses it anyway |
| **C3** | `ssot-consumables.md` §7.3: *"`shield.grant`'s battle runtime support is **None**"*, which is why the `ward` class takes the setup road as a declared SC1 deviation | **Battle = `Full`.** T14 wired Battle's own `Bag.ShieldGate`; A18c grew the grant path | The deviation is **narrower than the lane states** — asserted both ways, including that **Sim is still `None`**, which is the half of §9 item 12(b) that has *not* closed |
| **C4** | `spec-consumables.md`: *"`rpg_item_stock` appears in exactly two `src/` files and both are comments saying it does not exist"* | **The table ships** — `RpgStore.Items.cs:96`, with `AdjustStock`/`ListStock` | The spec's own conclusion (*"this module's real upstream is module 2 `armoury`… `holdsStock` becomes answerable the moment it exists"*) is now satisfied. ⚠ `PredicateNode.cs:10-12` and `CrossProgramLandedFlags.cs:37` still carry the stale *"unbuilt — confirmed absent by search"* comment; that is the **action program's** file and is named below rather than edited from here |
| **C5** | `ssot-consumables.md` §7.1: a `menu` consumable's road is *"on the lawn, grant → the bag fires the action through the `Passive` lifecycle path"* | **Contradicts its own §6.2**, whose `UseContextUnsupported` row names the two contexts a host may fail to serve as *"`battle` before the action layer, `lawn` with no injector"* — so `menu` cannot be the lawn | §6.2 wins (normative table over worked example): `menu` names **no combat runtime**, and the real consequence — no menu executor exists — is recorded as a named wiring gap rather than papered over |

**What was built:**

- [x] ⭐ **G2 held exactly: the USE PATH degenerates, the effect does not.** ⛔ **No scalar effect column
      exists anywhere in the module**, and that is asserted twice rather than reviewed —
      `No_scalar_effect_column_exists_anywhere_in_the_module` greps every Core and DAL file for
      `heal_amount`/`duration_ms`/`shield_hp` (comments stripped), and
      `consumable_def_carries_no_scalar_effect_column` asserts the shipped `PRAGMA table_info` is
      exactly the ten §5.2 columns. That absence **is** the no-migration proof (§2.5); a `heal_amount INT`
      would have to be migrated, re-priced, re-displayed and re-hashed the moment an action could fire it
- [x] **`OnActivate`, not `OnUse` — one name per concept, enforced.** `AtomTriggers.IsKnown("OnUse")` is
      **false** and the string appears nowhere in the module; an atom authoring it is refused by name and
      the message says what shipped instead. Which kinds carry the trigger is **read from the registry**,
      never a copied list, exactly as the spec's own Code-style block instructs
- [x] **The §14.2 invariant is intact, by the shipped mechanism rather than the lane's remedy.** The lane
      wanted `stat.modify` **excluded** from the new trigger so *"no trigger"* would keep its one meaning.
      Shipped code kept it a better way: `AtomKind.TriggerOptional` is a third case in a binary that had
      two, and `stat.modify` is the **only** kind carrying it — asserted, so a second kind acquiring it
      would be a red test rather than a silent widening of what "permanent" means
- [x] ⭐ **D37 built as an item property, not a constant — and there is deliberately NO carry limit in
      the tuning file.** `BeltCapacity` carries the equipped `girdle`'s own `consumableSlots`;
      `ConsumableLimits.UnbeltedSlots = 0` is structural and says why (*"an unequipped slot grants
      nothing, exactly as every other role behaves"*). ⛔ **A reintroduced `N` is refused BY NAME at
      load** — `ConsumableTuning.Parse` throws on `carryLimit`/`maxManifestEntries`/`n`/`N` with a
      message naming D37, because a withdrawn key that silently does nothing is the worst failure a
      balance file can have. **No upper bound is applied to a belt** either (`int.MaxValue` slots is
      legal and tested): a carry limit the player *grows* is a content axis, and clamping it would be
      the hard progression ceiling AGENTS.md forbids
- [x] **The manifest gate, at dispatch and not after, returning EVERY refusal.** `DraughtLimitExceeded`,
      `DraughtFamilyConflict`, `UseContextUnsupported`, an unknown container and a non-positive qty all
      reach the player as text, and a manifest with four bad lines reports four. The exclusion key is
      `(family, variant)` — the **shipped** `ContainerPoolRow.Group` default, reused rather than
      reinvented — so two fire draughts collide and fire + ice do not, both asserted
- [x] **The summed manifest cost is a `long`, widened before multiplying, and it THROWS.**
      `checked(total + (long)ManifestCost * Qty)`. A test drives `int.MaxValue` cost × `int.MaxValue`
      qty: two lines resolve exactly to 9,223,372,030,926,249,058 and refuse honestly; **three** lines
      overflow `long` and throw rather than wrapping into a total that would pass any belt
- [x] ⭐ **The grade is DERIVED, never authored beside the band.** `grade = gradeTierMap[powerBand]`,
      mirrored value-for-value from `bands.v1.json`'s **frozen** `powerBand.tierMap` (the same device
      module 14 used for the cost bands). `The_grade_tier_map_mirrors_the_frozen_registry_value_for_value`
      reads the real registry, asserts it is still `frozen`, and compares every pair — so a drift is a red
      test rather than a silently re-graded corpus. Asserted from the other side too: **no shipped entry
      and no property on `ConsumableSeed` carries a `grade` key at all.** Histogram over the real 60:
      **3 / 17 / 31 / 9 / 0** across grades 1–5
- [x] **`grade` equals the tier of every core atom** (I3's band-consistency rule, borrowed), and the
      parser refuses a `gradeTierMap` that is not a bijection onto 1..5 — a hole or a duplicate would
      grade two bands the same and make the check pass on a row it should refuse
- [x] ⭐ **The invisible-nerf guard is real, at catalog load, and it CAUGHT SOMETHING** — see the defect
      below. `UseContexts.RuntimesFor` is a documented four-row table (a decision the spec does not state,
      named below), and every core atom must be legal in every runtime its context names. A planted
      violation (`board.action`, Battle = `None`) proves the teeth independently of the corpus
- [x] **`chance` / `icd_ms` refused at import, with the runtime reason in the message.**
      `EffectBag.FireGrant` short-circuits both `PassesOverlayFilters` and `_proc.TryPass` on the
      lifecycle path, so either key would be a silent no-op. The refusal cites the mechanism, not a rule id
- [x] **`consumable_def` + `rpg_run_draught`, and the dispatch spend as ONE transaction.**
      `TrySpendDraughts` decrements `rpg_item_stock` with the **verbatim** conditional-decrement shape
      from module 14's `TrySpendRecipe`, writes the draught rows, and runs the caller's `seal` **inside**
      the same transaction. `An_insufficient_stack_rolls_the_WHOLE_manifest_back` proves the first line's
      decrement is gone too — no peek-and-keep; `A_throwing_seal_rolls_back_the_stock_too` proves a
      dispatch that fails to seal costs nothing; and `Run_draughts_are_written_before_the_seed_resolves`
      reads the rows **from inside the seal**, which is what §5.3's determinism-input rule actually asks for
- [x] ⛔ **Recall refunds no draught, and it is proven STRUCTURALLY rather than by not calling one.**
      `Recall_refunds_no_draught_because_no_refund_path_exists_at_all` reflects over `RpgStore` and
      asserts the only `*Draught*` methods are `ListRunDraughts` and `TrySpendDraughts`. Failure mode 7 —
      dispatch, peek at the outcome, recall, get the draughts back — has nowhere to live
- [x] **A retry on a sealed run is a `"replay"` and spends nothing** (the shipped `TrySpendSouls` /
      `TrySpendRecipe` spelling, reused), keyed on `(run_kind, run_id)`. A run is sealed once
- [x] **`effect_binding` carries no duration, asserted as a schema fact** — no `expires_utc`, no
      `duration_ms`, no `until_tick`. That is *why* a timed buff must be a status and a run-scoped buff is
      a lifecycle, and it is why this module builds **no second scheduler**: a grep over every Core file
      asserts no `Timer`, `Stopwatch`, `DateTime.UtcNow`, `Task.Delay` or `Queue<`
- [x] ⭐ **The run-start snapshot is this module's, and the charm side adopts it.** §9 item 10: *"whoever
      builds the run-start snapshot first owns it and the other adopts it."* Module 22 `charm-carry` is
      unbuilt, so `DraughtProjection` fixes the shape — `owner_kind = 'player'`, `slot = NULL`,
      `source = 'draught'`, priority from the tuning — mirroring `ssot-charms.md`'s charm binding exactly,
      with `source` the only difference. **Cross-referenced into P5.5 below**

      ⛔ **CORRECTED 2026-09-05 when module 22 built: `source` is NOT the only difference — the OWNER
      SCOPE differs too, and that is a ruling rather than a drift.** This bullet, and
      `consumables.v1.json`'s `_draughtBindingPriorityNote` beside it, both mirror `ssot-charms.md`
      §3.8's `player:{id}` — which **D33(a) withdrew on 2026-09-04**: *"Charms bind at **actor** scope,
      not `player:`"* (`item-ideal.md:1388`; `ssot-charms.md` §3.1's own banner says the same). So
      charms bind at `unique-actor:{specimenId}`, one row per deployed actor, and module 22's tuning
      refuses `player` **by name** at load. **Nothing here is wrong** — a draught really does bind at
      `player:` by ssot-consumables' own ruling, and the LIFECYCLE (snapshot at run start, `slot = NULL`,
      priority −100, withdraw by `source` at run end) is shared and was adopted unchanged, with a test
      that reads **both** real tuning files and asserts the two priorities still agree. Only the
      sentence "`source` is the only difference" was too strong
- [x] **`DraughtProjection` is `ApplyInjuries` with the opposite sign, and pure.** It appends a
      `BattleChannelMod` to every squad member (v1 is per-squad, §10.4's own answer), never mutates its
      input, and coexists with an injury on the same channel. ⛔ **A non-positive amount THROWS rather
      than being clamped** — a draught that lowers a channel is an injury wearing a potion's name, and a
      clamp would turn "your draught did nothing" into a bug with no symptom. `long` throughout: a
      3-billion contribution survives the round trip un-narrowed
- [x] **No new member of the closed 33-code list.** §6.2 proposed **four** (`ConsumableRolls`,
      `DraughtLimitExceeded`, `DraughtFamilyConflict`, `UseContextUnsupported`); this module mints
      **none**. `AtomRejectionReason` still has exactly **35** names, asserted, and all **15** rules are
      namespaced `ContentRuleViolated{consumable.*}` under a registered namespace — asserted by
      reflection over the rule-id constants, so a new rule added without registration fails
- [x] **Every number a balance pass would touch is in `data/tuning/consumables.v1.json`, and the parser
      REFUSES rather than defaults** — stripping any of the five keys throws at load, asserted key by key
      against the real file. Four structural invariants are checked at parse time: the grade-map
      bijection, non-empty subsets of both closed vocabularies, the bounded-ratio band on the authoring
      ceiling, and the withdrawn-`N` refusal
- [x] ⭐ **The seed id is ALREADY a legal container id, which a unique's was not.**
      `naming.v1.json idNamespaces.consumables`' template is `consumable.k{slot}-{seq:03}` and §4.6 fixes
      the kind's prefix as `consumable.`, so the two coincide: this module needs a **grammar check**
      (`ConsumableContainerIds`, reusing `UniqueContainerIds`' own slug expression) rather than the
      derivation module 17 had to invent. All 60 pass
- [x] **`audit-magic-numbers.py --summary` reports `M1 = 0` AND `M2 = 0`, exit 0**, with the
      `consumables` domain absent from the table entirely; `audit-overflow.py`: **0 critical**, **zero**
      findings anywhere under `Items/Consumables/`. ⚠ Two structural consts (`MinManifestCost`,
      `UnbeltedSlots`) matched `BALANCE_WORD` on the substrings "cost" and "slot" and were added to the
      audit's **`EXEMPT_NAMES`** with the documented-reason discipline that list already uses — the
      established mechanism (module 17 added two the same way), **not** a rename to dodge the check

**⛔ Real defects found, named, not silently fixed:**

- [x] ⛔ ⭐ **One shipped consumable is FAILURE MODE 5 ITSELF — the invisible nerf, live in the corpus,
      found by the check the lane wrote for exactly this.** `consumable.k2-015` (*"Purifying Tonic"*,
      class `draught`, `useContext: dispatch`) authors family `atom.cleansing`, which resolves to kind
      **`status.clear`**. Two independent things are wrong with that, and the row fails both:
      **(a)** `status.clear` is `Battle = None` (`AtomKindRegistry.cs:644`), and a `dispatch` consumable
      runs in battle — so it would bind and do nothing; **(b)** `status.clear` carries only
      `AtomTriggers.Events` (H3, deliberate), so it has **no fire point it may legally name** either —
      §4.2's "hardest finding" returning on a real row. Its own authored note (*"Removes debuffs at run
      start"*) describes a capability the runtime cannot deliver. **59 of the 60 are clean.** Refused by
      name with `ContentRuleViolated{consumable.runtime-unsupported}` and pinned as a **set of exactly
      one** from both directions, so it can neither grow silently nor be waved away by loosening the
      check. **Not hand-fixed** (`ItemSeedValidator`'s own footer: *"Re-run the partitions named above;
      do not hand-fix"*). **Owner: the authoring fleet's `consumables/2` partition** — the fix is to
      re-author the family, since `cleansing` has no consumable-shaped kind at all today
- [x] ⛔ **A ninth phantom affix family — `atom.elemental-power`, named by 11 of the 60.** It resolves to
      no affix-family row, so its kind is unknown, and the 11 are excluded from the runtime check rather
      than guessed into it (module 17's rule, kept). It differs from the other eight in one way worth
      recording: it is **exemplar-only**, carried by `_exemplars/affix-family.exemplar.json` as template
      content P2.3 explicitly and correctly left outside the real 98 — and `ssot-consumables.md` §7.2's
      own worked example is written against it. A lane doc, an exemplar and 11 authored rows all name a
      family the corpus does not have. ⚠ `atom.elemental-defense` **is** real and is what the other two
      element-bearing consumables use; the near-miss is part of why this went unnoticed.
      **Cross-referenced into P2.5 above**
- [x] ⛔ **One shipped comment still asserts something false about `rpg_item_stock`; the other was
      already corrected 2026-09-05.** `PredicateNode.cs:10-12` now reads *"the table... EXISTS —
      `RpgStore.Items.cs:96` creates it and `:302` upserts it (this comment said "unbuilt" until
      2026-09-05...)"* — fixed in place, no longer the stale claim quoted in earlier passes of this
      doc. `CrossProgramLandedFlags.cs:37` (*"INVENTORY SYSTEM (`rpg_item_stock`) remains unbuilt, by
      design"*) is the one still false: module 2 `armoury` shipped the table (`RpgStore.Items.cs:96`)
      with `AdjustStock`/`ListStock`, and this module adds `StockQty`. So `LeafId.HoldsStock` is
      answerable from a store today and still reads a caller-supplied quantity — a **wiring gap**, not a
      wall. **Not edited from here:** the file is the **action program's**, and the leaf's own contract
      (which quantity it reads, and when) is `A4`'s to change.

**Three decisions this module had to make that the spec does not state, all named:**

- ⭐ **`use_context` → runtime is a four-row table, and `menu` names NO combat runtime.** §6.3 requires
  every core atom to be legal in *every runtime the `use_context` names*, and neither document says
  which `RuntimeId` each of the four contexts is. `battle` → Battle and `lawn` → Lawn are direct;
  `dispatch` → **Battle**, because an expedition's encounters resolve through `BattleEngine` and §5.4's
  projection lands on `BattleActorSetup`. `menu` → **nothing**, derived from §6.2's own code-4 row,
  which names only *"`battle` before the action layer, `lawn` with no injector"* as the contexts a host
  can fail to serve — so a menu consumable must not require the game to be running (SC8). ⛔ **The
  honest consequence, named rather than hidden: the check is vacuously true for the 26 menu-only rows,
  because no menu executor exists.** Recorded as `ConsumableRules.MenuExecutorAbsent` — a rule id with
  no raiser, so the gap has a name a report can carry.
- ⭐ **The exclusion group is DERIVED as `{family}|{element}`, never authored.** §5.2 says it *"defaults
  to the container's dominant `(family_id, variant)`"*; the corpus authors no `exclusionGroup` key, and
  adding one would be a second source of truth for a derived fact. Spelled exactly as §7.1/§7.2 do
  (`atom.vitality|`, `atom.elemental-power|fire`) so a reader of the worked examples recognises the
  string. Measured over the real 60: **17 groups hold more than one row** — several grades of one
  family, of which a run may take exactly one. That is the rule forcing breadth, not a collision.
- ⭐ **The `consumable_def` DDL SHIPS, and the container-kind binding is refused by name instead.**
  spec-consumables.md says *"this module does not proceed past its DDL until [the container kind] is
  answered."* The two tables are **kind-agnostic** — `container_id` is a text key and `rpg_run_draught`
  never mentions a kind — so shipping them costs nothing and blocks nothing, while a live
  `FK → effect_container` would make the table unusable the moment anything wrote to it. What is
  actually gated is the **binding**, and `ConsumableValidator.ValidateDef` refuses *every* container
  kind by name with `ContentRuleViolated{consumable.container-kind-unavailable}`, message citing D27
  and X7. Neither the enum value **nor the documented `item` fallback** is chosen here — that is the
  owner's, and §Open is explicit the fallback must be a decision and never a drift.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Consumable\|FullyQualifiedName~DraughtManifest"` | **78 passed / 0 failed** (new — `ConsumableTests` 39 + `ConsumableCorpusTests` 18 + `DraughtManifestTests` 17, measured per class) |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~RunDraught"` | **14 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6792 passed / 9 failed** — **zero** in `Items.*`. All 9 are `Actions.*` (2), `Battle.*` / `Battle.Timeline.*` (3), `ClassSystem.ProveAptitudeJsonEmitTests` (3) and `Expeditions.ExpeditionResolverTests` — the concurrent stream's own in-flight work; `git status` shows `Actions/TimelineDispatch.cs`, `Battle/BattleEngine.cs`, `Battle/BattleModels.cs`, `Battle/BattleRunState.cs` mid-edit and `Battle/Timeline/ReactionLaneTuning.cs` brand new, **none touched by this module** |
| `dotnet test tests\FusionRpg.Data.Tests` (full, minus the demon CLI test that spawns a process) | ⭐ **793 passed / 0 failed** — fully green |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | ⭐ **204 passed / 0 failed** — the session-start `ClassSystemBaselineRegenTests` dominance drift cleared by the concurrent stream mid-build |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors across 120 partitions — identical to module 17's baseline.** Zero new findings; the three `consumables/*` partitions carry only the two pre-existing `MetaRegistryVersion*` notices every partition carries |
| `python -m pytest tools/seedsmith` | **1608 passed, 1 skipped, 288 subtests** — this module wrote no `tools/seedsmith/**` and no `data/seed/**` file; the only Python it touched is `scripts/audit-magic-numbers.py`'s `EXEMPT_NAMES`, which seedsmith does not import |
| `python scripts\audit-magic-numbers.py --summary` | **`M1 = 0`, `M2 = 0`, `M4 = 0`, exit 0**; the `consumables` domain does not appear in the table |
| `python scripts\audit-overflow.py` | **0 critical**, 57 findings, **zero** under `Items/Consumables/` |
| `.\scripts\guard-dal.ps1` / `guard-single-writer` / `guard-funnel-delta` / `guard-secondary-no-unity` | all four **OK** |
| `dotnet build src\FusionRpg.Server\FusionRpg.Server.csproj` | **0 errors** — boot parses `consumables.v1.json`. ⚠ Built to a scratch `OutDir`: the owner's own `FusionRpg Server (61116)` was running and holding `bin\`, and killing it is not this module's call |

⚠ **Baseline re-measured fresh at the start of this module, not inherited, and it moved during the
build.** At session start: `Core` **10 failed / 6714 passed** (`Actions.ActionsPurityGuardTests`,
`Atoms.AtomBenchGuardTests`, `Atoms.PredicateCompilerTests`, `Battle.*` ×3, `ClassSystem.*` ×3,
`Expeditions.*`), `Guard` **201 passed / 1 failed** (the known dominance-baseline drift), `Data`
**host-crashed** on `DemonSpeciesImportCliTests` before printing a summary. By the end, `Guard` and
`Data` were fully green and `Core` was 9: the two allocation-budget tests
(`AtomBenchGuardTests`, `PredicateCompilerTests.Evaluating_allocates_nothing`) had gone green and a
third of the same kind (`ActionSelectionTests.TryDeclareAllocatesZeroBytesAcrossTwoHundredActors`,
5,904 bytes against a budget of 0) had gone red — the same allocation-benchmark family, in the same
`Actions/` tree the concurrent stream is editing. **Compare against the numbers in the rows above, not
against an earlier module's snapshot.**

⚠ **Two transient build breaks from the concurrent stream, both resolved by waiting, neither in a file
this module touched** — `BattleEngine.cs:375` (`CS1501`, `RunTimelineActionPhase` mid-signature-change)
and `RpgStore.cs:679` (`CS0103`, a call to `EnsureSpeciesRespecSchemaUnlocked` landing before the file
that defines it). The same pattern P3.1 and P4.1 recorded. Also killed one orphaned `testhost` holding
`FusionRpg.Data.dll` from the crashed baseline run.

⚠ **Three tests were corrected mid-build rather than left passing on a false premise**, and each
correction is a fact this section now records: `OnActivate` carries on **five** kinds not four (C2),
the phantom count is **11 rows on one family** not 13 (two element-bearing rows use the real
`atom.elemental-defense`), and *"every kind the corpus reaches carries `OnActivate`"* is the **wrong
rule** — a draught is a triggerless permanent modifier by design (§7.2's `stat.derived`), so the real
requirement is *a fire point **or** no trigger at all*, which is what let `status.clear` stand out as
the one row that is neither.

**⏸ Deferred, each with its owner named — none silently skipped:**

- [ ] ⏸ ⛔ **`ContainerKind.Consumable` — the owner's, batched with D27, and NOT drifted into.** The lane
      argues the fifth kind out properly (§4.6) and D27 mints four that do not include it. Re-verified
      2026-09-05, not assumed: `ContainerRow.cs:7` ships six values and `PrefixOf` has six arms. Both
      the enum value and the documented `item`-with-`slot IS NULL` fallback are refused here, because
      §Open is explicit that the fallback is a decision to be taken. **Owner: the owner, through
      effect-atom, on the same amendment that lands D27's four — one review of five costs what one
      review of four costs.** `ConsumableLimits.ConsumableContainerKindAvailable = false` is the single
      line that flips
- [ ] ⏸ **The seed → concrete generator — the runtime generator's, per the binding seed-to-concrete
      rule, and the reason three other items below are still open.** The corpus holds 60 **seeds**
      (a family and a band, never a magnitude); no `effect_container` row exists for any of them, and no
      `effect_container_atom` row either. Everything this module owns operates on either the seed (the
      corpus validators, the grade derivation) or on a concrete container supplied by a caller (the
      per-row validator, `consumable_def`, the projection's `DraughtMod`). Rolling a seed into a
      container with its atom rows is a **shared-SDK** job — the identical deferral module 17 recorded
- [ ] ⏸ **`consumableSlots` on `girdle` base types — module 6's, and measured as absent rather than
      assumed.** D37's consequence 1 says module 6 authors it on the directional-profile pass; a fresh
      scan of every `girdle` row in `data/seed/items/base-types/` finds the key on **none** of them, and
      a test pins that. Until it lands, the belt count reaches `GateManifest` as a parameter and an
      unequipped player is refused at 0 — a **wiring gap** with a named owner, not a wall
- [ ] ⏸ **No menu executor exists, so a `menu` consumable's effect reaches nothing today.** 26 of the 60
      rows are `menu`-only. There is no out-of-combat surface that applies a container's atoms to a
      persistent actor, which is why `UseContexts.RuntimesFor(Menu)` is empty and why the honest name
      `consumable.menu-executor-absent` exists with no raiser. **Owner: whoever lands the out-of-combat
      apply path; the player surface is module 20's.** `dispatch` — the 34 rows that matter for v1 — has
      the real, shipped projection road
- [ ] ⏸ **`LeafId.HoldsStock` still reads a caller-supplied quantity, and the leaf is the action
      program's.** `RpgStore.StockQty(playerId, containerId)` ships here so the answer exists; wiring it
      into `PredicateNode`'s evaluation is `A4`'s, together with the two stale comments named above.
      ⚠ `ActionCompiler.cs:97-98` already refuses a `HoldsStock` action in **lawn** mode
      (`ConsumableUnsupportedInMode`), which is the lawn half correctly closed from the action side
- [ ] ⏸ **No recipe outputs a consumable, pinned as an absence.** §7.5 prices a batch of Lesser
      Restorative (`operation = forge`, `output_kind = container`, `output_qty = 5`) and I9's schema
      already allows all three, but module 14's 30-recipe corpus authors **none** — asserted, so
      *"recipes output consumables"* is not read as shipped. **Owner: module 14 as a corpus addition**
      (it is one `material_recipe` row plus two or three cost rows and no code — SC7 working)
- [ ] ⏸ **`grants_action_id` and `cooldown_key` ship, are authored nowhere, and are inert.** Asserted
      null on all 60 rows and writable through the DAL, so the absorption really is *"one UPDATE on two
      nullable columns and one INSERT"*. `cooldown_key` is carried now precisely because a cooldown group
      retrofitted after content ships re-prices every row that already shipped (§3.3). **Owner: `A1`**
- [ ] ⏸ **A status whose payload is a container of atoms — asked JOINTLY with the Resource model, not
      from here.** §4.5's conclusion still holds: `effect_binding` has no duration, so a timed buff must
      be a status, and `StatusDef` carries no container reference. ⚠ One lane claim **has** drifted:
      `StatusPayloadKind.ModifyStat` now has two production declarers (`ExhaustionPolicy.cs:77`,
      `StanceRuntime.cs:46`), so *"declared and dead, four references, all in the file that declares
      them"* is no longer true — but the **mechanism** the lane asked for is still absent, which is the
      part that matters. v1's run-scoped lifetime needs none of it
- [ ] ⏸ **The `board`, `revive` and `utility` classes stay declared and ungenerated**, refused by name
      with the reason each has no executor: `board` waits on an overlay use affordance plus
      `capPerMatch` (**G4**, still unimplemented), `revive` on the battle-mode use moment (the action
      layer), `utility` on the menu executor above. Widening `classesAuthored` in
      `data/tuning/consumables.v1.json` is the whole change the day one lands
- [ ] ⏸ **`use_context = battle` / `lawn` likewise stay refused**, and widening `contextsAuthored` is one
      line. ⛔ The **reason** for keeping battle out is now the use SITE, not the runtime — `resource.delta`
      is Battle `Full` (C3 above) — and the blocker the lane names in §9.5(b) is **stale, not open**:
      `A3` (`spec-action-costs.md` §8) recommends, and `A4` (`spec-usability-conditions.md` §3a, locked)
      already states, *"consuming the item is a precondition… rather than a cost"* — both REVISED
      2026-08-27, a week before this module — shipped as `LeafId.HoldsStock` (`action-todo.md` T10, done
      2026-08-28), the very leaf this entry already cites above as closing the lawn half.
      `ssot-consumables.md` §9.5(b) was never annotated with the answer, which is what let this entry
      restate it as open. What is genuinely still unresolved, narrower than the lane's original ask:
      `HoldsStock` only reads a quantity (previous bullet), so nothing yet decrements the stock when a
      `battle` action gated on it actually fires.
- [ ] ⏸ **`rpg_run_draught` has no production writer yet.** `TrySpendDraughts` takes the run's own
      creation as a `seal` delegate so the store stays free of expedition knowledge, and the tests drive
      the seam including the forced-throw rollback. The dispatch endpoint that calls it is the
      **standalone/expedition stream's** — a wiring gap with a named owner. The `battle` `run_kind` is
      likewise legal in the schema and written by nothing
- [ ] ⏸ **A `stale` marker on `rpg_item_stock` — inventory's (§9 item 6a), failure mode 8, still open.**
      `rpg_item.stale` exists for rolled instances; `rpg_item_stock` has four columns and none of them is
      it, so a stack of potions whose atom an import disabled cannot say so. Re-verified today against
      the shipped DDL. Not added from here: the column belongs to the table's owner, module 2
- [ ] ⏸ **`item_category`'s `consumable` row still says *"the action layer, unbuilt — do not author"***
      (§9 item 8). The menu/dispatch consumer now exists and is `ConsumableCatalog`; the battle/lawn one
      does not. Flipping the `consumer` column and changing `stack_intent` from `charges` to `qty` is
      **I3's / module 6's** doc-side edit, not this module's
- [ ] ⏸ **§4.4's ≤10% authoring ceiling is carried as a tunable and measured by nothing yet.**
      `authoringCeilingPerMille = 100` is parsed, bounded and asserted, but pricing a consumable's
      contribution against a geared actor's needs the concrete containers that do not exist. It bounds a
      **report** and never an import, the same disposition module 17 gave its parity band. **Owner: this
      module, once the seed→concrete generator lands**
- [ ] ⏸ **Per-specimen draughts stay the owner's (§10.4).** v1 is per-squad and every member receives
      every mod. Per-specimen is expressible on the same road today — `ChannelMods` is already per-actor
      — and would make the manifest a targeting decision rather than a shopping list. Recorded, not decided
- [ ] ⏸ **PvZ-mode consumables (§10.2), "rest" (§10.6) and the permanent-stat-up confirmation (§10.3)
      stay open-by-design.** Nothing here contradicts the lane's standing answers (yes-later via the
      intent road; no refill at rest; refused as consumables and allowed as quest rewards)

**Files:** `data/tuning/consumables.v1.json` (new — the two authored vocabularies, the mirrored grade
map, §4.4's ceiling, the run-start binding priority, and the recorded absence of a carry limit);
`src/FusionRpg.Core/Items/Consumables/{ConsumableDef.cs, ConsumableTuning.cs, ConsumableCorpus.cs,
ConsumableValidator.cs, ConsumableCatalog.cs, ConsumableCorpusValidator.cs, DraughtProjection.cs}` (new);
`src/FusionRpg.Core/Items/Drops/DropTableModel.cs` (EDIT — the `Consumable` unavailable-reason moved off
a module that now exists); `src/FusionRpg.Data/Sqlite/RpgStore.Consumables.cs` (new — `consumable_def`,
`rpg_run_draught`, `TrySpendDraughts`, `ListRunDraughts`, `StockQty`);
`src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT — `EnsureConsumableSchemaUnlocked` in `Init`, after the
`rpg_item_stock` schema its spend decrements); `src/FusionRpg.Server/Program.cs` (EDIT — parses
`consumables.v1.json` at boot); `scripts/audit-magic-numbers.py` (EDIT — two structural consts added to
`EXEMPT_NAMES` with reasons);
`tests/FusionRpg.Core.Tests/Items/{ConsumableTests.cs, ConsumableCorpusTests.cs, DraughtManifestTests.cs}`,
`tests/FusionRpg.Data.Tests/Items/RunDraughtStoreTests.cs` (new).

⚠ **One deviation from the spec's Project structure, stated rather than silent:** it lists four Core
files; seven shipped. `ConsumableCorpus.cs` / `ConsumableCorpusValidator.cs` exist because the spec was
written before anyone checked whether a corpus existed (it does, 60 rows), and `ConsumableValidator.cs`
is split out of `ConsumableCatalog.cs` so the per-row rules can run against a seed and a concrete
container alike. Same directory, same names for the four it does list.

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Consumable|FullyQualifiedName~DraughtManifest"`; `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~RunDraught"`; `dotnet run --project tools\ItemSeedValidator`

### ✅ P5.3 — Module 19 `granted-actions` — GATE GA2 BUILT AND VERIFIED 2026-09-05 (GA3/GA4, the `decisions.md` requests and the content-hash registration explicitly deferred to their real owners — all four either upstream or another program's, none skipped)

⭐ **The pattern inverts for the first time in this program: nothing was authored, and half the
CONSUMER was already built.** Every prior module found a shipped corpus nobody read
(`item_role_family`, `nameWords`, `displayTemplate`, `UnitClass`, 144 uniques, 30 sets, 60
consumables). Here `data/seed/items/base-types/**` authors **zero** grant rows — asserted, not
assumed, by a grep over all 740 — and that is **correct**: gate GA2's whole definition is "DDL,
validator, reason codes, zero content rows." What *was* already built is the other side of the seam:
`rpg_action` with both flags, `rpg_action_grant` as §5.5 item 5's option (a) **verbatim** (its DDL
comment cites this lane by name), `ActionSetAssembler`, `FrozenActionSet` and `CapPolicy`. So this
module is **one new table, one validator, one projection and the FSM contract** — and the checking
was still worth it, because it found handshake item 8 already closed (G1 below).

⛔ **Six doc corrections, each checked in the file the doc cites, recorded rather than absorbed:**

| # | The doc says | Verified | What shipped |
|---|---|---|---|
| **G1** | `spec-granted-actions.md`'s handshake table: item 8 (the cap) is ⛔ **open** — *"`ActionSetAssembler.cs:30` — 'no cap enforcement (item 8 / T24's own job)'. Nothing enforces one"* | ⭐ **CLOSED, and the answer is "uncapped by design."** `Actions/Grants/CapPolicy.cs` (T24) answers item 8 by NAMING which existing cap governs instead of minting one: `HeldCap` is the levelling faucet, `EquippedSkillCap` (= `LoadoutSet.MaxSize`) is the real bottleneck, and *"granted by paid sources: uncapped, on purpose — an uncapped pool grows the choice, never the power."* The class deliberately **has no `grantedCap` member**, which is the answer, not an omission | `ItemGrantLimits.GrantedCountCapExists = false`, asserted, plus a reflection test that `CapPolicy` carries no `GrantedCap`. §3.7(d)'s proposed **8** and its `TooManyGrantedActions` code have **no raiser on either side of the seam**. The *reject-never-truncate* requirement is carried by the cap that DOES exist — `LoadoutSet.Validate` returns `LoadoutFull` rather than dropping the overflow, asserted |
| **G2** | `spec-granted-actions.md` invariant 3 and ssot §3.5: *"the enum is `CrowdControl` and `Damage`"* | **THREE members.** `ResourceExhausted` landed with the per-tick cost model, and its own comment already states this lane's rule — *"a mechanical fact about the actor's own resources, never an inventory/content concept reaching this enum"* | Nothing asserts a count. The test asserts the **invariant** — no `InterruptCause` member names an inventory concept (`item`/`equip`/`unequip`/`grant`/`inventory`/`gear`) — so a fourth mechanical cause is not a red test and a third *inventory* cause is |
| **G3** | `ssot-granted-actions.md` §3.6's runtime matrix: *"Eleven of twelve kinds have no battle consumer at all; one is `Partial`"*, and the headline *"neither runtime executes both halves"* | ⛔ **Stale end to end, and the conclusion INVERTS.** **Five kinds are `Battle = Full`** — `stat.modify` (`AtomKindRegistry.cs:217`), `stat.derived` (`:255`), `resource.delta` (`:290`), `status.apply` (`:344`), `shield.grant` (`:396`) — and **no kind is `Partial`**. Seven stay `None`: the five `AttachPoint.Board` kinds plus `resource.economy` and `status.clear` | **Corrected in the lane doc**, per the spec's own success criterion — a ⛔ block under §3.6 with every line cited and the board-kind count stated as **five**, not six |
| **G4** | `ssot-granted-actions.md` §5.6: *"four independent reasons, any one alone is sufficient"* | **Two are false.** Reason 1 (*"`rpg_action` does not exist. No table, no `src/FusionRpg.Core/Actions/` directory"*) — both exist. Reason 3 — see G3. Reasons 2 and 4 hold, and 2 is narrower than it reads (module 6 shipped the corpus and the readers, **not a table**) | **Corrected in the lane doc** with a four-row verdict table, and the real remaining blocker named: **X3**, not the four |
| **G5** | `ssot-granted-actions.md` §4.3 cost 2 / §8.6: per-base-type authoring would be *"344 hand-authored actions"* | ⭐ **48, MEASURED against the shipped corpus.** `default-attack` is legal only on `armament-primary` (§4.3 option C), of which the 740-row corpus has **48**. 344 was I3's whole-catalogue figure and never applied to this rule | The **mitigation's own number is exact**: the corpus has precisely **3 weapon classes × 2 frames = 6** distinct `(frame, class)` pairs on `armament-primary` (`blade`/`blunt`/`launcher` × humanoid, `lash`/`nozzle`/`seedpod` × plant), which is §8.6's "roughly 3 × 2 = 6" to the number. Only the comparator was stale — 48 → 6 is an 8× saving, not 57× |
| **G6** | `spec-granted-actions.md` §(b): `UpsertGrant` at `RpgStore.Actions.cs:512`, `ListGrants` at `:538`, *"delete by source (`:571`)"* | `:515`, `:541`, `:567` — and the delete is named **`WithdrawGrantsBySource`**, not a "delete" | Cosmetic line drift only; every method is real and at the shape described. Recorded so the next reader does not conclude the methods moved |

**What was built:**

- [x] ⭐ **`item_granted_action` — ssot §5.2's SIX columns, and §5.3's Never list is enforced twice
      rather than promised.** `The_row_carries_exactly_six_properties…` asserts the record's property
      set by reflection; `The_item_side_carries_no_cooldown_cost_target_or_condition_column` asserts
      the shipped `PRAGMA table_info` is exactly `{container_id, seq, action_id, grant_role, enabled,
      revision}`; and `No_source_file_in_the_module_declares_a_forbidden_column` greps **27** forbidden
      names (the lane's own list, verbatim) across every Core and DAL file in the module. ⭐ **The grep
      strips comment lines and, where needed, string-literal contents** — the whole point of the Never
      list is that it is *discussed* by name in the doc comments, and a grep that could not tell a
      paragraph from a declaration would forbid explaining the rule it enforces
- [x] **A child table, not a nullable column on the base type** (§5.2's own reason): a unique granting
      two abilities needs no schema change, and *"at most one `default-attack`"* is a constraint the
      validator states rather than a comment. PK `(container_id, seq)`, plus the one index §5.2 asks
      for — `ix_item_granted_action_action`, so the action layer can answer *"what grants this"*
      (`ListContainersGranting`, tested)
- [x] ⭐ **Wiring gap (b) closed at the store: `RpgStore.UpsertGrant` has a caller in `src/` for the
      first time.** `ApplyEquippedGrants` withdraws by `source` then upserts, at
      **`OwnerKind.Entity` + the specimen instance id** — *the exact scope*
      `WebMatchService.EquippedActionIdsFor` (`WebMatchService.cs:517`) already reads, asserted against
      the shipped reader's own construction rather than against a constant. `source` is the item's
      container id, so unassign is one delete against `ix_rpg_action_grant_source`, which already
      exists
- [x] ⭐ **The `grant_id` is DERIVED, not a fresh `Guid.NewGuid()`, and a test greps for the absence.**
      A projection is a full rebuild (the shape `EquipProjector` already chose); a random primary key
      would insert a duplicate row on every re-apply instead of upserting the one that exists.
      `Re_applying_the_same_projection_upserts_rather_than_duplicating` applies three times and asserts
      one row; `A_grant_row_removed_from_the_base_type_disappears_on_the_next_apply` proves a content
      edit **converges** rather than leaving an orphan, which is what withdraw-by-source-first buys
- [x] **Refusals are RETURNED, never swallowed.** `ApplyEquippedGrants` hands back one
      `ActionRejection` per grant that failed to write, because `UpsertGrant` runs the shipped
      `ActionValidator.ValidateGrant`. Proven for an unknown action (`UnknownContainer`) and a
      non-grantable one (`ActionNotGrantable`), and proven to write **nothing** in both cases
- [x] **`ItemGrantValidator` — §6.1's nine content rules at IMPORT, returning every failure.** Unknown
      action and disabled action under **one** rule (the lane's own instruction); non-grantable;
      basic-collision; `default-attack` on a role other than `armament-primary`; `default-attack` on an
      ineligible action; a non-`Item` container kind; a malformed container id; a negative `seq`. Plus
      §6.4's three cross-row checks over a whole base type — duplicate `seq`, duplicate `action_id`,
      and **at most one `default-attack`** — run once over the catalogue because *"they are properties
      of the catalog, not of a row"*
- [x] ⭐ **`default-attack` is `armament-primary` only, and the off-hand keeps `granted` — both arms
      tested.** The same row that is refused as a default attack on `armament-secondary`,
      `jewel-major` and `girdle` **passes** as a `granted` row from those roles, so §4.3 option (C)'s
      actual claim (the conflict is *unrepresentable*, not *banned*) is what is asserted
- [x] **The item side's wire spelling IS the assembler's constant.** `ItemGrantRoles.Wire(DefaultAttack)`
      returns `ActionGrantRoles.DefaultAttack` (`Grants/ActionSetAssembler.cs:10`) rather than a second
      copy of the string, and `TryParse` round-trips it. ⛔ G2's proposed third role (`on-use`) parses
      as **false** — a consumable is module 18's `grants_action_id` column, not a third grant role
- [x] ⭐ **No merge of our own, and it is asserted structurally.** Every stacking assertion —
      two items → one entry with two provenance rows, removing one source leaves the action, an
      already-known action **reported not swallowed**, `default-attack` replacing the species
      intrinsic, an unarmed actor keeping it — runs through the **shipped** `ActionSetAssembler`. A
      source test asserts no file under `Items/Grants/` declares an assembler or writes
      `DefaultAttackActionId`
- [x] ⭐ **R2 PICKED UP — module 9 built the read, named this module as its consumer, and nothing had
      ever called it.** `ItemPowerReads.GrantedActionPrice` gains a third, optional `ItemPowerTuning`
      parameter, which is the literal mechanism of its own documented lifecycle (*"reportable today and
      gating only when module 19 `granted-actions` lands"*): with no tuning `Over` stays `false` and
      every pre-existing caller is unchanged; with one, the share is measured. `ItemGrantValidator`
      passes it, and it is the first caller ever to do so
- [x] ⛔ **`unpriced` is REFUSED, never read as `0` — and the two unpriced arms are split, which the
      spec does not do.** *No resolvable rung* is a **content defect** and refuses
      (`grant.unpriced`), because G4's stated fear is that *"pricing it at zero would make every
      action-granting item strictly dominant"*. *No seeded rarity ceiling* is a **caller gap** and is
      reported, not refused — and that is not hypothetical: `chaff`'s shipped
      `powerCeilingShareMilli` is **0**, so a real rung in the real ladder reaches that branch.
      Refusing an authored row because the harness has no ceiling would blame the content
- [x] **The over-budget refusal is measured against the SHIPPED ladder, not a fixture.** A rung-10
      action (`action-rungs.v2.json`) priced against `sprout`'s ceiling (`item-rarity.v1.json`, 22‰)
      refuses with `grant.over-budget`, naming the share and the band
- [x] ⭐ **No new tuning file, and no invented number.** The one knob a balance pass would touch —
      `grantedActionShareCapMilli` — **already exists** in `data/tuning/item-power.v1.json` (module 9
      shipped it, `null`, and boot already parses it at `Program.cs:164`). It stays `null`: the module
      asserts it is null, asserts the effective cap therefore falls back to the whole ceiling, and
      asserts that **setting it to 300 makes the same price refuse** — so tightening is a file save,
      not a code change. `ItemGrantLimits.WholeCeilingShareMilli = 1000` is a **bounded ratio** (the
      per-mille identity: "this one action costs the item's entire budget") and carries the AGENTS.md
      exemption comment that rule requires, as do the other three structural constants
- [x] **The share is a `long`, widened before multiplying, divided by 1000 last.** A max-int rung price
      against a ceiling of 1 resolves to a share **above `int.MaxValue`**, asserted — which is the
      reason it is a `long` rather than an assertion that it is one
- [x] ⭐ **Handshake item 7 CLAIMED and written — the per-`TurnState` removal table.** §5.5 marked it
      *partial* and assigned it to **nobody**; the audit's own words were *"not written down anywhere
      the kernel can be held to."* `GrantRemovalPolicy` is that table, verified against the shipped
      FSM: `Charging`/`Ready` → immediate, `Committed`/`Resolving` → **the run completes** (no refund
      path exists, by rule), `Recovering` → at the transition to `Charging`, and
      `Downed`/`Dead`/`Withdrawn` → **recorded and survives a revive**. The two edges the rules turn on
      (`Recovering → Charging`, `Downed → Charging`) are asserted against `TurnTransitions.IsLegal`,
      not against the doc. ⛔ **There is no enforcement code and that is deliberate** — there is nothing
      to enforce until mid-match equip exists, and a policy reaching into the kernel now would be
      inventing the coupling invariant 3 forbids. The four FSM tests **skip against
      `ItemGrantLandedFlags.MidRunEquipLanded`**, never silently absent
- [x] **The two free wins are asserted rather than assumed.** A granted action creates **no binding**
      (a grep proves no file in the module names `effect_binding` or `BindingRow`), so the
      apply/revert lifecycle does not apply; and `CooldownSlot` carries exactly `{ActorKey, Slot}`, so
      unequip-then-re-equip does not reset a cooldown — **the classic swap exploit is closed for free
      by a key shape that shipped for an unrelated reason, and nobody should "fix" it**
- [x] **No new member of the closed code list.** §6.3 proposes **four** (`UnknownAction`,
      `ActionNotGrantable`, `DefaultAttackNotAllowed`, `TooManyGrantedActions`, *"33 to 37"*); this
      module mints **none** — the same call modules 11, 17 and 18 made. `AtomRejectionReason` stays at
      **35**, asserted, and all **twelve** rules are `ContentRuleViolated{grant.*}` under a registered
      namespace, checked by reflection over the rule-id constants so a rule added without registration
      is a red test. ⚠ The action program's own `ActionRejectionReason` already ships
      `ActionNotGrantable` / `ActionNotDefaultAttackEligible`; those are the **write path's** refusals
      and are reused verbatim, never duplicated
- [x] **Two rule ids exist with NO raiser, on purpose, so each gap has a name a report can carry** —
      `grant.action-corpus-absent` (X3) and `grant.too-many-granted` (the cap G1 closed). A test
      asserts neither appears in the validator, so neither can be deleted as dead code nor quietly
      wired to a refusal
- [x] **`audit-magic-numbers.py --summary` reports `M1 = 0`, `M2 = 0`, `M4 = 0`, exit 0**, with no
      `grants` domain in the table and **zero** `Items/Grants/` entries in M3;
      `audit-overflow.py` reports **0 critical**, 57 findings, **zero** under `Items/Grants/`.
      ⚠ One structural const (`MaxDefaultAttacksPerContainer`) matched `MAGNITUDE` on the substring
      "attack" and `percontainer` was added to the audit's **`NOT_MAGNITUDE`** suffix list with the
      documented-reason discipline that list already uses — the established mechanism, and the exact
      precedent the file's own comment records for `peractor` (`MaxShieldsPerActor`, a slot count).
      **Not** a rename to dodge the check

**⛔ Real defects found, named, not silently fixed:**

- [x] ⛔ ⭐ **The grant seam's required scope is the SESSION-SCOPED one, and the owner already ruled
      against it for durable per-specimen state.** `OwnerScope.IsSessionScoped` is true for exactly
      `OwnerKind.Entity`, and its own doc says why: *"`entity:` bindings are session-scoped and never
      durable — the pointer is reused."* `rpg_action_grant` is a **durable** table. And the owner
      approved `OwnerKind.UniqueActor` on **2026-09-02** for durable per-specimen state
      (`RpgStore.UniqueActors.cs:697`, `ReconcileUniqueEquipmentAtomBindings`) *"specifically because
      `OwnerKind.Entity` is session-scoped and would silently drop equipped-item bonuses on the next
      session boundary."* ⚠ **It works today only by coincidence**: `CreateUniqueActor` mints
      `Guid.NewGuid().ToString("N")` — 32 lowercase hex — which is exactly what `Entity`'s grammar
      requires, so `OwnerScope.Validate` passes and the mismatch is invisible. (A readable placeholder
      like `"spec-1"` is `BadOwnerKey`; the tests use a real 32-hex id and assert both facts, because
      testing with a placeholder would have proven the opposite of what they claim.) **This module
      writes where the shipped reader reads** — the spec's Boundaries are explicit, and writing
      elsewhere would produce rows nothing sees. **Owner: the server/loadout lane** —
      `rpg_actor_loadout` is read at the identical scope by the same method
      (`GetLoadoutOrAutoEquip`), so the question is one decision covering both, not two. Pinned by
      `The_grant_scope_is_the_session_scoped_one_and_that_conflicts_with_a_durable_table`
- [x] ⛔ **Module 9's R2 read shipped with `Over` hard-coded to `false` and a parsed-but-unread
      tunable.** `GrantedActionPrice` computed a share and then always returned `Over: false`, while
      `ItemPowerTuning.GrantedActionShareCapMilli` was parsed at boot and read by nothing — so the read
      could report a number but could never say it was too big, and the tunable was a row no code
      consumed (SC7, from the inside). Fixed here by the optional-tuning parameter above, which is the
      shape module 9's own doc comment already described. **Cross-referenced into P2.4.**
- [x] ⛔ **`ssot-granted-actions.md` §4.3's "344 hand-authored actions" is wrong by 7×** (G5) — the
      figure is **48**, because the rule it prices applies only to `armament-primary`. The mitigation
      still pays and its own number (6) is exact; the comparator was borrowed from I3's whole-catalogue
      count. Corrected in the lane doc's own §3.6/§5.6 blocks is G3/G4; this one is recorded here and
      left in place, because §4.3's cost paragraph is argument text rather than a normative table

**⏸ Deferred, each with its owner named — none silently skipped:**

- [ ] ⏸ ⛔ **X3 — an ordinary external dependency, and NOTHING is filed against `action-corpus`
      (D36).** Re-verified 2026-09-05 by a test that walks every `.cs` file under `src/` and `tools/`:
      **no production call to `ActionSeeder.Generate` exists** (the grep is for the call shape,
      `ActionSeeder.Generate(`, so a refusal MESSAGE naming the method is not mistaken for a use of
      it). `ItemGrantLandedFlags.ActionCorpusProducerLanded = false` carries it, and a second test
      asserts this module builds no producer of its own — no `ActionSeeder`, no `new ActionRow`.
      **We consume a production caller the day one ships. We do not build one, amend their map, file a
      row in their program, or infer their schedule from their documents**
- [ ] ⏸ **Gates GA3 and GA4 — both blocked on X3, and neither is faked.** GA3 needs one weapon base
      type with a real action driven through a battle; GA4 needs the `granted` role's first real
      exercise. With no `rpg_action` row, both would be a fixture pretending to be a proof. **GA2 is
      what ships**, and the module says so rather than authoring rows that point at an empty table
- [ ] ⏸ **`ApplyEquippedGrants` has no equip ENDPOINT calling it — and neither does module 4's own
      write.** Verified, not assumed: `RpgStore.SaveAssignment` / `RemoveAssignment` (module 4's whole
      equip road) also have **zero** callers outside `tests/`. The projection is wired to exactly the
      depth module 4's own equip write is wired — store level — and the missing caller is the same
      missing endpoint (module 2's entry already records *"Core + DAL; endpoints deferred"*).
      ⛔ **Deliberately NOT called from inside `SaveAssignment`:** module 1's R1 keeps unequip as *"one
      row deleted, no second writer"*, and `EquipProjector` is likewise a separate projection the
      caller runs rather than a side effect of the assignment write. `ApplyEquippedGrants` mirrors it
      exactly. **Owner: module 20 / the server's equip endpoint.**
      ⚠ **Re-checked 2026-09-05 and still open — module 20 landed the item program's first server
      surface and it is deliberately READ-ONLY.** `ItemSurfaceEndpoints.cs` carries three `MapGet`
      routes and **no `MapPost` at all**, because equipping, socketing and salvaging already have
      owners (modules 4, 16, 14) and a write path through the presentation layer is the "second
      surface" that module exists to prevent. So the equip WRITE endpoint is still unbuilt, and its
      real owner is **module 4's own server surface**, not module 20's. Corrected here rather than left
      pointing at a module that has now shipped without it. See P5.4
- [ ] ⏸ **`item_granted_action.container_id` carries no FK — the identical wiring gap module 17
      recorded for `item_unique.derived_from`.** §5.2 wants `FK → item_base_type(container_id)`;
      module 6 shipped the 740-row corpus and the Core readers, **not a table**, so the FK has nothing
      to point at. The reference is checked by `ItemGrantValidator` against caller-supplied base-type
      facts (the shape `EquipItemFacts` already uses for the same reason). **Owner: module 6**; the
      column is ready the day the table exists
- [ ] ⏸ **§9 item 9's content-hash registration — effect-atom's (E8), and NO item table is registered
      today.** `ContentHashRegistry` is at **V9** (V8/V9 landed via effect-pipeline's affix-schema
      (T3.1) and prefix/suffix split (T3.2) — unrelated to items) and carries `rpg_action`,
      `rpg_action_cost` and `rpg_action_effect_scope` but **no** `item_unique`, `consumable_def`,
      `item_set` or `item_display_template` — so this is the program's standing position, not a one-off
      omission. Registering `item_granted_action` means a **V10** and a moved stamp. **Owner:
      effect-atom, as one amendment covering every item table**
- [ ] ⏸ **Handshake item 9 and the §5.6 `decisions.md` row — both doc changes with no code, and both
      the owner's.** *"Record that the item side of an action grant is a reference and a role, never a
      definition"* (so `A1` starts against a settled seam instead of negotiating one mid-build), and
      the timeline program's written refusal that an inventory event may become an `InterruptCause`
      (§9.10). This module **requests** both and enforces the second from its own side by a guard on
      the shipped enum; neither is a row this program may write into `decisions.md`
- [ ] ⏸ **Amending I2's *"legal on both armament roles"* (§9.3) is R4's.** §4.3 option (C) narrows
      `default-attack` to `armament-primary`, which is a tightening of `ssot-equip-slots.md:205`'s
      assertion, not a contradiction of its principle. This module implements the tightening and does
      not edit I2
- [ ] ⏸ **§6.3's anti-silence import warning — needs a resolvable action to inspect.** The check
      (*"a granted action whose container holds no atom the battle runtime can execute must warn at
      import"*, reusing `RuntimeUnsupported`) resolves `action_id` → `rpg_action.container_id` → its
      atoms. With X3 unresolved there is no action and therefore no container, so the check would be
      vacuous on every row. ⚠ Its urgency also **dropped by G3**: five kinds now execute in battle, not
      one. **Owner: this module, once a production action producer exists**
- [ ] ⏸ **The `battle-only` presentation tag is module 20's to render.** §3.6's option (b) pick
      includes the display requirement, and failure mode 7 (*the tooltip lie*) is a UI failure, not a
      schema one. Nothing here can render it; the six-column row deliberately carries no display field
      (§5.3), so the tag is derived from *"this base type has a grant row"* rather than stored
- [ ] ⏸ **Mid-run equip stays unlanded and the FSM contract stays inert.** Re-verified: equipment
      cannot change mid-run — `UniqueActorService.PutEquipment` refuses unless the actor's phase is
      `Roster` (`phase.not_roster`) and `ClearEquipment` routes through the same method. The shipping
      rule is unchanged: **the granted-action set is assembled at run start and is immutable for the
      run** (`FrozenActionSet.FreezeAtRunStart`). `MidRunEquipLanded = false` is the single line that
      flips
- [ ] ⏸ **§9.5's third `grant_role` (`on-use`) is NOT added, and module 18 already answered it
      differently.** The lane floats *"the cheap answer is a third `grant_role` value rather than a
      second table"* for consumables; what shipped 2026-09-05 is `consumable_def.grants_action_id` +
      `cooldown_key`, two nullable columns on module 18's own table. Two mechanisms for one concept is
      what §5.3 exists to prevent, so this module keeps the closed two and `TryParse("on-use")`
      returns **false**, asserted. If the seam ever unifies, it unifies onto one of the two — a
      decision, never a drift
- [ ] ⏸ **§9.11's *"is a granted action rolled"* (I12) needs no work and is recorded as satisfied.**
      The grant lives on the base type (§4.4), so nothing about this seam is rolled and there is no
      generator surface to forbid — SC5 is satisfied by having none, which is the third of §4.4's own
      three reasons

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.ItemGrantedActionTests"` | **50 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemGrantStore"` | **14 passed / 0 failed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items."` | **666 passed / 0 failed** — the whole item program, modules 1–18's own suites included, green under this module's `ItemPowerReads` edit |
| `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Items."` | **131 passed / 0 failed** — the item program's whole DAL half, green under the new `item_granted_action` schema |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6872 passed / 8 failed** — **zero** in `Items.*`. All 8 are `Battle.BattleStatComposerTests`, `Expeditions.ExpeditionResolverTests`, `ClassSystem.ProveAptitudeJsonEmitTests` ×3, `Demons.DemonSpeciesGenExplainTests`, and the two allocation benchmarks `Atoms.ValueSpecTests.Resolving_allocates_nothing` / `Atoms.PredicateCompilerTests.Evaluating_allocates_nothing` — ⭐ **both of which PASS when run in isolation** (2/2), so they are the order-sensitive allocation family module 18 already recorded flapping, not a regression |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **816 passed / 2 failed** — both `DemonSpeciesImportCliTests` (the demon stream's own process-spawning tests, which host-crashed at module 17's run and were excluded at module 18's); **zero** in `Items.*` |
| `dotnet test tests\FusionRpg.Guard.Tests` (full) | **203 passed / 1 failed** — `ClassSystemBaselineRegenTests.EveryBaselineParsesAndCarriesMeta`, reading the four `docs/research/class-system/_baseline-*.json` files `git status` shows mid-edit by the concurrent stream |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors across 120 partitions — identical to modules 17 and 18's baseline.** Zero new findings: this module authors no seed content, which is gate GA2's definition |
| `python scripts\audit-magic-numbers.py --summary` | **`M1 = 0`, `M2 = 0`, `M4 = 0`, exit 0**; no `grants` domain in the table, and no `Items/Grants/` entry in M3's 13 |
| `python scripts\audit-overflow.py` | **0 critical**, 57 findings (the module-17/18 baseline number), **zero** under `Items/Grants/` |
| `python -m pytest tools/seedsmith` | **1608 passed, 1 skipped, 288 subtests** — identical to module 18. This module wrote no `tools/seedsmith/**` and no `data/seed/**` file; the only Python it touched is `scripts/audit-overflow.py`'s suffix list, which seedsmith does not import |
| `.\scripts\guard-dal.ps1` / `guard-single-writer` / `guard-funnel-delta` / `guard-secondary-no-unity` | all four **OK** |
| `dotnet build src\FusionRpg.Server\FusionRpg.Server.csproj` | **succeeds** — the new schema step does not break boot, and no new tuning parse was added (module 9's `item-power.v1.json` is already read at `Program.cs:164`). ⚠ Built to a scratch `OutDir`, as module 18 did |

⚠ **Baseline re-measured fresh at the start of this module, not inherited, and it moved during the
build.** At session start `Core` was **9 failed / 6801 passed** (`Battle.*` ×2,
`Expeditions.ExpeditionResolverTests`, `Actions.ActionsPurityGuardTests`,
`Battle.Timeline.TimelinePurityGuardTests`, `Demons.DemonQualityReportTests`,
`ClassSystem.ProveAptitudeJsonEmitTests` ×3). By the end **four of those nine had gone green** and
three different ones had gone red — the two allocation benchmarks above and
`Demons.DemonSpeciesGenExplainTests`. Every failing name in the final runs was checked against
`git status`: their sources (`World/`, `Battle/`, `Battle/Ai/`, `ClassSystem` baselines, the demon
species tree) are all mid-edit or brand-new in the concurrent stream and **none is touched by this
module.**

⚠ **Three transient build breaks from the concurrent stream, all resolved by waiting, none in a file
this module touched** — `World/StructureCatalog.cs` (`CS0103`, calling a `StructurePolicy` whose file
had not landed yet; it appeared as an untracked file minutes later), both test projects'
`ContractTuningTestBootstrap.cs` (`CS7036`, `SiegeTuning` grew a required eighth `Structure`
parameter and the two bootstrap copies had not caught up), and `Battle/Ai/ZombossAdaptiveTuning.cs`
(`CS0111`, a duplicate `PositiveLong`). The same pattern P3.1, P4.1 and P5.2 recorded — the second
one blocked all test execution for several minutes.

**Files:** `src/FusionRpg.Core/Items/Grants/{ItemGrantedActionRow.cs, ItemGrantValidator.cs,
EquippedGrantProjection.cs, GrantRemovalPolicy.cs}` (new — the row + the three closed vocabularies +
the structural limits + the landed flags + the rule namespace, the import/cross-row/R2 validator, the
equip→grant projection, and handshake item 7's table);
`src/FusionRpg.Core/Items/Power/ItemPowerReads.cs` (EDIT — `GrantedActionPrice` takes an optional
`ItemPowerTuning` and sets `Over`, turning module 9's read from reportable into gating);
`src/FusionRpg.Data/Sqlite/RpgStore.ItemGrants.cs` (new — the `item_granted_action` DDL,
upsert/list/reverse-index/remove, and `ApplyEquippedGrants` / `WithdrawEquippedGrants`,
`UpsertGrant`'s first `src/` caller); `src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT —
`EnsureItemGrantSchemaUnlocked` in `Init`, after the action schema whose `rpg_action_grant` the
projection writes into); `docs/architecture/item/ssot-granted-actions.md` (EDIT — the §3.6 runtime
matrix and §5.6 reasons corrected in the lane, per the spec's own success criterion);
`docs/architecture/item-map.md` (EDIT — module 19 row 155 gains `6`, and the reconciliation note gains
its sixth row); `scripts/audit-overflow.py` (EDIT — `percontainer` added to `NOT_MAGNITUDE` with a
documented reason, the `peractor` precedent);
`tests/FusionRpg.Core.Tests/Items/ItemGrantedActionTests.cs` (new),
`tests/FusionRpg.Data.Tests/Items/ItemGrantStoreTests.cs` (new).

⚠ **One deviation from the spec's Project structure, stated rather than silent:** it lists a separate
`ItemGrantLandedFlags.cs`; the flags ship inside `ItemGrantedActionRow.cs` alongside `ItemGrantLimits`
and `ItemGrantRules`, because all three are the module's constant surface and splitting a two-const
class into its own file would make the "what does this module refuse and why" answer live in three
places. Same directory, same type names, same content. No `data/tuning/granted-actions.v1.json` was
created either — deliberately: the one balance number this module needs already exists as module 9's
`grantedActionShareCapMilli`, and a second file holding a copy of it is the drift this program keeps
naming.

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.ItemGrantedActionTests"`;
`dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemGrantStore"`;
`dotnet run --project tools\ItemSeedValidator`

### ✅ P5.4 — Module 20 `item-surfaces` ⭐ — THE DECIDING HALF BUILT AND VERIFIED 2026-09-05 (the eight `.tsx` render files, the `docs/web/spec.md` amendment and the gap board explicitly deferred — one of the three is genuinely this module's own and is named as such, not laundered onto someone else)

⭐ **Three of this module's pieces already existed and were ADOPTED rather than rebuilt — the same
"authored but never wired" pattern half this program's modules have hit.** Verified by reading the
live files before writing a line:

| Claimed by the spec | Real state, checked |
|---|---|
| *"the home already exists and is already built"* | ✅ True. `web/fusion-rpg-web/src/layers/relics/RelicsLayer.tsx` is a `PanelShell` with three tabs fed by `useRelics()` → `/api/relics` (`RelicEndpoints.cs:15`), and the `storage` tab's `EmptyState` is a designed state, not a fake. **No route added, none needed** |
| *"the contract type already exists, with nine of eleven blocks stubbed"* | ✅ True. `contract/types.ts:135-149`'s `ContainerView` carries the eleven blocks and `Pending<T>`; `adaptRelic` (`adapt.ts:124-144`) returns `absent()` for seven and `pendingWithReason` for the implicit |
| module 16's write-free preview | ✅ Already shipped. `CombinationEvaluator.Preview` / `PreviewWithOneMore` — **this module calls them and wrote no second pass** |
| ⭐ module 7's deferred **light-theme palette + deuteranope transform** | ✅ **ALREADY BUILT — by module 10 on 2026-09-04, not owed here at all.** `RarityPalette.cs` ships sRGB → CIE L\*, WCAG 2 contrast, the Machado/Oliveira/Fonseca (2009) deuteranope **and** protanope matrices, and the constructed light palette. **P2.1's note is struck and its addendum written.** This module built no second palette |

⛔ **THE DEFECT THIS MODULE FOUND, and it is in its own spec: the near-miss algorithm
`spec-item-surfaces.md` specifies is STALE BY ONE DAY, and the spec that supersedes it names this
module by name.**

The spec (2026-09-03) devotes a whole section — *"⭐ How the swap hint stays tractable — decide by
multiset, then count cycles"* — to an INSERT/SWAP split whose swap leg computes
`distance = n − cycles(σ)` over an **ordered** recipe, with a worked `Code style` block. **D41
(2026-09-04, the owner, `spec-sockets.md:320`) made recipes unordered:** *"unordered — we only need
collect enough type of socket and put it to the item, if the item match condition, it will got bonus,
no need order."* D41's own consequence table has a row that reads, verbatim:

> | Module 20's swap-distance | sized against unordered — `distance` counts *missing kinds*, never positions |

And module 16 shipped it that way: `ComboIngredient` carries **no position field**, the DDL has **no
`position` column**, `MultisetSatisfied` counts and claims, and `bind_ordinal` carries a comment
saying a matcher that reads it is a bug (P4.3's own four-way proof). **So the swap leg is not an
optimisation this module declined — over an unordered recipe a swap distance is always zero and the
hint would be a lie.** Implemented unordered, and pinned so it cannot come back:
`D41_made_recipes_unordered_so_every_arrangement_of_one_fill_reports_the_same_distance` walks **all 24
permutations** of a four-insert fill and asserts one signature, plus asserts by reflection that
`CombinationDisplayState` has no `Swap` member and `MissingIngredient` no position/ordinal field.
**Cross-referenced into P4.3 (module 16).** The spec's *tractability* claim survives intact and is
asserted as data rather than prose (`DistanceDiagnostics.PermutationsEnumerated == 0`,
`ActiveSetEvaluations == 1`, `MultisetComparisons ≤ catalog.Count`).

⛔ **A SECOND spec-vs-code divergence, smaller and decided the same way — by the shipped evaluator.**
The spec's affinity note says *"affinity never changes a `distance` — it changes the result."* That is
true of a Strain (attunement moves the granted **tier**) and **false of Pure**: the shipped
`CombinationEvaluator` Pure arm adds `attunedEffectiveCountBonus` to the **contributor count**, which
is the exact quantity the threshold is compared against (`sockets.v1.json`'s own note documents the
two-arm split). A distance that ignored it would report *"one more"* about a resonance already firing
— the same-evaluator rule's failure mode, inverted. **Distance follows the evaluator**, and both arms
are pinned: `A_matched_affinity_changes_a_strains_result_not_its_distance` and
`Pure_distance_follows_the_shipped_evaluator_including_attunements_effective_count`.

**Built:**

- [x] ⭐ **`CombinationDistance` — the near-miss evaluator, one call to module 16's own `Evaluate`.**
      Four closed states (`Active` / `OneAway` / `KnownInactive` / `Undiscovered`), G3 §4.3's `∞` rule
      as `Distance == null` (a **nullable, not a sentinel**, so an arithmetic use site cannot treat ∞
      as a large number), and per-shape arms each reading off the arm its evaluator uses. Reachability
      is three permanent facts about the ITEM — too few sockets, wrong host role/frame, and D21's
      set-piece exclusivity — never about the fill, so *"a set piece is one insert from a Strain"* is
      unreachable by construction
- [x] **`CompendiumReveal` — the held-ledger reveal rule and the display cap.** A Strain/Splice
      reveals when every ingredient FAMILY has been held; a generated resonance names no families, so
      its condition is **derived from its shape** (Pure wants its element, Ring/Eclipse both of theirs,
      Diversity `threshold` distinct elements) rather than authored as a second reveal table — a
      seventh element needs no content edit. Render is active → one-away → known-inactive-by-name, in
      that order, stable within a band, and ⛔ **the row cap touches only the name-only tail**, so it
      can never hide a combination the player is about to earn
- [x] **`LootFilterView` + `LootFilterRule` — a client-side VIEW rule, and D26 is enforced by
      construction.** Every method takes an already-materialised row list and returns a subset. A guard
      test strips the comments and asserts the source names no `LootPipeline`, `DropTable`, `LootPity`,
      `DropEnvelope` or `RpgStore` at all. ⛔ **A `Locked` row is never hidden** — the exemption is
      first in the predicate, and it is written as one predicate rather than a union so the caller's
      sort order survives. The inbox counts over the WHOLE armoury, never the filtered view
- [x] **I12's `40/day` restated on the axis the game has.** `reviewPressurePerContentEvent` (60,
      ssot-inventory.md) and `inflowWatchPerContentEvent` (40, ssot-generation.md's tripwire) are
      **per content event**, per §2f.2 — and this file **never reads a clock**: it has no parameter
      that could carry one. Both are watch numbers; `Review_pressure_is_a_warning_and_never_a_refusal`
      asserts every row survives the flag firing
- [x] **`CollectionStrategy` — GG-50 at 10 / 100 / 1,000, as a function rather than a paragraph.**
      `RenderAll ≤ 100 < Virtualize ≤ 2,000 < SearchFirst`, from ssot-inventory.md:534-541's measured
      numbers. ⛔ **No band refuses a row** — asserted at 0 and at 1,000,000 — which is what keeps it a
      layout call and not the bag cap §2.5 forbids. `RpgStore.InventoryCeiling` stays module 2's own
      structural abuse guard and is named as a different thing
- [x] **`SurfaceCatalog` — the six surfaces × four designed states (GG-17), with GG-44 mechanical.**
      Precedence is **locked → loading → error → empty**, each with its reason: a locked surface must
      not spin (the player cannot make the spinner finish), and an errored one must not read as empty
      (*"you own nothing"* and *"we could not read what you own"* are different sentences). A locked
      surface can always say what unlocks it because `UnlockKeyFor` is **total over the six**, and
      `ItemSurfaceTuning.Parse` refuses at LOAD a `surfaceUnlocks` table missing any of them — so a
      seventh surface cannot be added without declaring its unlock
- [x] **`DominancePresentation` — GG-27 and SC4.** All four verdicts are a **word and a shape**
      (`▲ ▼ ◆ ◇`), all distinct, and `VerdictBadge` is asserted by reflection to carry **no colour
      property at all**, so a renderer cannot fall back to hue. `GroupByUnitClass` puts the unit in the
      GROUP HEADER and never in the column, over module 10's `ChannelUnits` facade — and an
      unresolvable channel gets **its own `null` group**, never folded into `GameUnits`, because
      guessing a unit is the lie the rule exists to prevent. ⛔ **The no-single-score footnote has no
      dismiss API**, asserted by reflection over every public member of the namespace
- [x] ⭐ **`SetDisclosure` — module 12's cross-referenced tooltip requirement, picked up.** P3.2 filed
      it here by name: the 30 shipped sets declare **154** distinct `(role, base type)` member pairs
      and **25** belong to more than one set, one to three, so a card that renders one *"3 / 4"* has
      rendered a third of the truth. `SharedMembers` re-measures all three numbers against the real
      corpus and `ForWearer` reports per piece which sets it advances and which it is **redundant** in
      — the *"say why the fourth did not count"* half of ssot-sets.md §4.5, and a **disclosure, never a
      refusal**: equipping the duplicate stays legal. It counts nothing of its own; the `(set, role)`
      dedupe is `SetEvaluator.Hits`' discipline re-expressed, and a test asserts the two agree
- [x] **`data/tuning/item-surfaces.v1.json` + `ItemSurfaceTuning` — the balance surface is config.**
      Five sections, every one carrying a note saying it is a PRESENTATION threshold and not a meter.
      No key has a default; the parser refuses an unordered render band, a zero one-away distance
      (which would name the active set), a negative cap, a zero watch number and a missing surface
      unlock — each with its own message
- [x] ⭐ **`ItemSurfaceEndpoints.cs` — the item program's first server surface, READ-ONLY.**
      `GET /api/items/surfaces/{playerId}` (the six states, derived from real ownership + real socket
      rows), `GET /api/items/armoury/{playerId}` (keyset page via module 2's `ArmouryQuery`, plus the
      inbox count and the render strategy), `GET /api/items/{instanceId}/combinations` (the four-state
      list for one item). ⛔ **There is no `MapPost` in the file, deliberately** — equipping, socketing
      and salvaging already have owners (modules 4, 16, 14) and a second write path through the
      presentation layer is the *"second surface"* this module exists to prevent. Wired in `Program.cs`
      beside the other eight item tuning loads

**⏸ Deferred, with the owner named — and the FIRST one is this module's own, said plainly:**

- [ ] ⏸ ⛔ **The eight `.tsx` files ARE THIS MODULE'S OWN WORK AND THEY ARE NOT BUILT.** `ArmouryList`,
      `ArmouryFilter`, `Paperdoll`, `ItemCard`, `CompareView`, `SocketBench`, `Compendium` and the
      `RelicsLayer` body swap, plus `contract/types.ts`'s `SocketsView`/`SetView` and `adaptRelic`.
      **Not laundered onto another module:** the reason is that the web tree is being actively
      refactored by the concurrent world-stage stream right now — `git status` shows
      `stages/world/WorldStage.tsx`, `targeting/QueuedOrders.tsx`, and the whole
      `stages/world/playback/` + `stages/world/turn/` subtrees modified — already tracked, not
      untracked; the untracked half of the churn is `stages/world/`'s own root (`commanderIntent.ts`,
      `labels.ts`, `playbackKeyframes.ts`, `playbackTable.ts`, `turnPlayback.ts`, `worldSelection.ts`,
      `worldViewModel.ts`, `fixtures/`), moving in as `features/world/`'s matching files show
      `deleted:` mid-edit — and the owner's own memory
      note records *"map FE frozen pre-refactor — do not add UI to it."* Composing eight new files
      against a kit whose shell files are moving is how a merge conflict eats a day's work.
      ⭐ **What is now READY for that pass, so it is a composition and not a design:** every number it
      renders comes from module 10's `DisplayModel`; every combination state and distance from
      `GET /api/items/{instanceId}/combinations`; the four surface states from
      `GET /api/items/surfaces/{playerId}`; the render strategy and inbox from
      `GET /api/items/armoury/{playerId}`; the verdict word/shape, the sidegrade trade, the unit-class
      grouping and the footnote key from `DominancePresentation`; the per-piece set disclosure from
      `SetDisclosure`. **No layout decision in the spec was re-litigated** — comparison stacks at
      640px, the bench and compendium are band-3, identity blocks 1–6 above the fold
- [ ] ⏸ **`patronView.ts`'s own call site — module 10's cross-referenced hand-off, NOT picked up here.**
      P2.5 filed it by name: `FormatPerMille` is the shared conversion this module (or the web layer)
      is meant to call instead of `patronView.ts` owning a second `pct` closure. Not one of the eight
      `.tsx` files above — `patronView.ts` lives in `features/demons/`, not `layers/relics/` — and this
      pass touched no TypeScript file, so the call site is unchanged. **Owner: this module, on the same
      web pass as the eight `.tsx` files above.**
- [ ] ⏸ **`docs/web/spec.md` §399's success criterion 7 is NOT amended — the spec puts it under
      "Ask first" and it is another program's document.** The collision is real and re-verified
      today: `ssot-presentation.md` §1 cedes component code to the web spec, `docs/web/spec.md:137-144`
      says *"that seam is unclaimed from this side"*, and `:399` nonetheless **claims** *"the item
      card's eleven blocks"* as web-program work. **Owner: the owner**, one sentence: the eleven blocks
      are item module 20's, delivered against the web kit
- [ ] ⏸ **The compendium is 25 rows today, not 127 — module 21's 102 Strains and Splices do not
      exist yet.** `P4.4` above is the model-call work that authors them. Everything here is sized for
      127 and measured against 25: the distance pass is O(k) per recipe with no allocation and no
      permutation, so the catalog tripling changes the row count and nothing else.
      **Owner: module 21**. ⭐ **Addendum 2026-09-05 — module 21 built the generator and confirmed
      the sizing, and it hands one requirement back.** Its `catalogue.report()` prints
      **127 against ssot-sockets §4.4's ~45 bar (2822‰, 2.8×)** and carries this module's two
      mitigations as REQUIREMENTS with `owner: module 20 (item-surfaces)` — so a run cannot print 127
      without printing who owes them. ⚠ **Only half of the pair exists:** the socket-UI preview and
      the swap-distance hint landed here (P5.4), the **compendium REVEAL rule** — *"a combination is
      revealed once the player has held every ingredient at least once"* — has no owner-side state and
      is **not built**. The 102 rows are still module 21's; the reveal rule is this module's. See P4.4
- [ ] ⏸ **An armoury row's `role` and `frame` come back empty, and role/frame filtering is not
      offered.** They live on the item's BASE TYPE, and module 6 shipped the 740-row corpus and the
      Core readers but **not a table** — the identical wiring gap P5.1 recorded for
      `item_unique.derived_from` and P5.3 for `item_granted_action.container_id`. ⛔ Deliberately **not**
      answered from the container's `slot`, which is a different axis and would be a plausible wrong
      answer. **Owner: module 6**; the field is ready the day the table exists
- [ ] ⏸ **The gap board (48 × 15 = 720 cells, server-computed and memoised) is not built** — it needs
      exactly the role join above, because a "gap" is a role with no strict improvement available.
      Blocked on the same table. **Owner: this module, once module 6's table lands**
- [ ] ⏸ **The held ledger is approximated by current stock, and the endpoint says so.** The reveal
      rule wants *"has ever held"*; the shipped schema has `rpg_item_stock` (what you hold **now**) and
      no ever-held table. A ledger that decayed when the player spent a gem would **un-teach** a recipe,
      which is worse than never teaching it — so the Core rule takes a `HeldLedger` and the server
      fills it from stock as the honest approximation available today. **Owner: inventory (I13's own
      table set)**; `CompendiumReveal` needs no change when it lands
- [ ] ⏸ ⚠ **D3 `frame-mix` still has no player surface, and this is the record the stub asked for.**
      It appears in modules 3, 6 and 12 (`FrameMixPredicate`, `item-frame-mix.v1.json`, the hybrid core
      at 800‰) and in **none of the six surfaces**. Recorded as an omission rather than added: the
      six surfaces are the spec's own closed list, `ItemSurface` is a closed enum, and a seventh
      surface is a spec change, not an implementation detail. ⛔ The cheap half — *showing a frame
      badge on the item card* — is already in `ContainerHeader.frameBadge` (`types.ts:139`) and is the
      `.tsx` pass's, not a new surface
- [ ] ⏸ **The `battle-only` presentation tag (module 19's P5.3 hand-off) and a unique's flavour text
      (module 17's P5.1) are `.tsx` work**, not Core work — both are *"render this string"* with no
      deterministic rule to test. They ride the deferred render pass above rather than being claimed as
      done here
- [ ] ⏸ **Module 15's `Restore` admin surface (P4.2's hand-off, *"when an admin surface exists (module
      20)"*) is NOT built.** It is an administrative rollback to a recorded `op_seq` — **a write**, and
      this module's server file is read-only by design. An admin console is not one of the six player
      surfaces. **Owner: module 15, on an admin surface that is not this one**

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemSurfaceTests` | **32 passed** (new) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **6,951 passed / 7 failed** — `ActorHub.SpecChannelClaimTests`, `Atoms.PredicateCompilerTests.Evaluating_allocates_nothing`, `Battle.BattleStatComposerTests`, 3 × `ClassSystem.ProveAptitudeJsonEmitTests`, `Expeditions.ExpeditionResolverTests.Tier_goldens_are_locked`. **Zero in `Items.*`**, and every one of the seven traces to a file the concurrent stream has mid-edit (`git status`: `Effects/Atoms/PredicateNode.cs`, `Stats/Aptitudes/RespecPolicy.cs`, `Server/ExpeditionEndpoints.cs`, `Data/Sqlite/RpgStore.Aptitudes.cs`) |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | ✅ **823 passed / 0 failed** — fully green, first time in this build. The three reds every module from P2.1 onward carried (2 × `DemonSpeciesImportCliTests`, 1 × `AtomStoreTests`) have been fixed by the streams that owned them. **This module touched no `src/FusionRpg.Data` file at all** |
| `dotnet test tests\FusionRpg.Guard.Tests` | **203 passed / 1 failed** — `ClassSystemBaselineRegenTests.RegeneratingTwiceReproducesIdenticalPayloads`, the same concurrent class-system red P2.5 recorded |
| `.\scripts\guard-dal.ps1` · `guard-single-writer.ps1` · `guard-secondary-no-unity.ps1` · `guard-funnel-delta.ps1` | ✅ **all four OK** — the new server file reads through `RpgStore` and writes no SQL |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors across 120 partitions — identical to modules 17, 18 and 19's baseline.** Zero new findings: this module authors no seed content |
| `dotnet msbuild src\FusionRpg.Server\FusionRpg.Server.csproj -t:Compile` | ✅ **0 errors** — the boot parse of `item-surfaces.v1.json` and the three routes compile. ⚠ Compile target rather than Build because the owner's server is running and holds a lock on `bin\Debug\net8.0` (`MSB3027 … locked by "FusionRpg Server"`) — a machine state, not a code failure |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0, M2 = 0** (13 M3 total, none under `Items/Surfaces`) |
| `python scripts\audit-overflow.py` | **0 critical**; zero findings under `Items/Surfaces` |

⚠ **Baseline note, re-measured at the START of this pass rather than carried:** `Core.Tests` was
**6 failed / 6,909 passed** before a line of this module was written, and is **7 failed / 6,951
passed** after — a different SET, not a growing one (`PatronAuraOverlayTests` went green; `ActorHub`
and `PredicateCompilerTests` went red) and every move belongs to the concurrent stream, which was
visibly mid-edit throughout: `FusionRpg.Core` itself failed to compile **twice** during this pass on
`Battle/Board/SiegeTuning.cs` and `Battle/BattleEngine.cs`, and `Core.Tests` failed to compile on the
stream's own untracked `Battle/Board/SiegePositionsTests.cs` — each resolved on its own within
minutes, exactly the retry case. ⭐ **`Data.Tests` moved the other way and is now zero**, so the
"14 and 2" bar this program set on 2026-09-04 is now **7 and 0**, both of them other streams'.

⛔ **One process note, recorded because it changes how a red is read here.** While `Core.Tests` was
uncompilable on another stream's file, this module's 32 tests were run against a **throwaway project
in the scratchpad** that globbed only `tests/FusionRpg.Core.Tests/Items/**` — and then **re-run in the
real project the moment it compiled again, twice, both times 32/32.** The scratch run is not the
evidence; the real one is. Named so nobody reads the scratch harness as a way around a red suite.

**Files:** `data/tuning/item-surfaces.v1.json` (new — render bands, the compendium's four-state
boundary and tail cap, the loot filter's default and its two per-content-event watch numbers, the six
GG-44 unlock keys); `src/FusionRpg.Core/Items/Surfaces/{ItemSurfaceTuning.cs, SurfaceCatalog.cs,
CollectionStrategy.cs, CombinationDistance.cs, CompendiumReveal.cs, LootFilterRule.cs,
DominancePresentation.cs, SetDisclosure.cs}` (new);
`src/FusionRpg.Server/ItemSurfaceEndpoints.cs` (new — three read-only routes);
`src/FusionRpg.Server/Program.cs` (EDIT — parses `item-surfaces.v1.json` at boot, maps the three
routes); `tests/FusionRpg.Core.Tests/Items/ItemSurfaceTests.cs` (new — 32 tests).

**Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter Items.ItemSurfaceTests`

### ✅ P5.5 — Module 22 `charm-carry` — BUILT AND VERIFIED 2026-09-05 (D40's split closed; ⛔ one spec-vs-ruling divergence found and resolved against the ruling)

**⛔ THE FINDING THAT SHAPED THIS MODULE, stated first because the stub above is wrong about it.**
The stub says the snapshot binds at `player:{id}` and *"`source` is the only difference between the
two"*. **That is stale by D33(a)**, and it was checked rather than assumed:

| Source | Says | Dated |
|---|---|---|
| `ssot-charms.md` §3.8 | run start binds "one `effect_binding` per charm at `player:{id}`" | lane text, 2026-08-22 |
| `ssot-consumables.md` §9 item 10 | mirrors §3.8, citing `ssot-charms.md:319-328` | lane text |
| The stub above (written by module 18) | mirrors the mirror | 2026-09-05 |
| **`item-ideal.md:1388` — D33** | ⭐ ***"(a) Charms bind at **actor** scope, not `player:`"*** | **owner ruling, 2026-09-04** |
| `ssot-charms.md` §3.1 banner | *"SETTLED 2026-09-04 by owner ruling D33(a) — the answer is B, not C… Option C's `player:{id}` is **withdrawn**"* | same |

**The ruling wins, and the reason is a correctness bug rather than taste** — the one module 12 already
refuses in code: `StatApplyScope.Matches` returns `true` unconditionally for a `player:` owner
(*"stub → match-wide apply"*) and `match` matches **both sides** before it looks at `side`, so a
`player:`-scoped `+atk` charm **buffs the zombies**. So this module binds at
**`unique-actor:{specimenId}`, one binding per deployed actor**, and `bindingOwnerKind` is refused **by
name** at tuning load for `player` / `match` / `entity`
(`charm.binding-owner-kind-not-actor`), so a balance edit cannot reintroduce the withdrawn option C.
**Everything else about the shared lifecycle is adopted unchanged**, which is what §9 item 10 actually
asks for. ⚠ **Cross-referenced into P5.2 (module 18)** — its own `draughtBindingPriority` note says
*"`source` is the only difference between the two"*, and that sentence is now one difference short.

- [x] **The five tables, ssot-charms.md §4.2 verbatim, and zero columns added to any atom table.**
      `charm_def` · `charm_pouch` · `charm_run_hold` · `charm_resonance` · `charm_attunement`, wired
      into `Init()` after `EnsureConsumableSchemaUnlocked`. `The_five_tables_exist_and_none_of_them_added_a_column_to_an_atom_table`
      asserts all five against `sqlite_master` **and** that `effect_container` gained none of
      `axis`/`ap_cost`/`unique_carry`/`frame_hint` — §4.2's own reason for side tables ("repeating
      `slot`/`rarity`'s precedent for a fifth kind is how a shared table becomes a union of every kind's
      private fields")
- [x] ⭐ **The partial unique index IS the exclusivity rule, and it is proven from OUTSIDE the store.**
      `CREATE UNIQUE INDEX … ON charm_run_hold(instance_id) WHERE active = 1`, mirroring
      `ix_rpg_expedition_members_active` exactly. `OpenCharmRunHold` **does not check then insert** — it
      inserts and translates SQLite error 19 into `CharmInUse`, because a read-then-write check has a
      window and an index does not. `A_raw_insert_that_bypasses_every_C_sharp_check_still_cannot_double_hold_a_charm`
      opens its own connection, goes around the store's method entirely, and asserts the constraint
      violation; `The_partial_unique_index_is_what_enforces_exclusivity_and_a_second_run_rolls_back_whole`
      adds the all-or-nothing half — the clashing run leaves **zero** rows, never a half-sealed run
- [x] ⛔ **No new reason code, and the five §5.2 asked for all still exist by name.** definitions.md
      §10's list is closed at 33 + `ContentRuleViolated`, and §5.2 itself called five *"a large ask"*.
      This module takes **none**. The split follows the program's own two answers: an **authoring**
      failure is `ContentRuleViolated{charm.*}` under a registered namespace (modules 1/7/11/12/17/18's
      device), and a **player-action** refusal is a module-local enum — `CharmCarryRefusalReason`,
      exactly module 4's `EquipRefusalReason` precedent, because *"may this player attune this charm?"*
      is not an atom rejection at all. `CharmBudgetExceeded`, `CharmAxisOverflow`, `CharmInUse` and
      `CharmNotCarryable` survive verbatim as enum members; `CharmAtomNotPermitted` survives as the rule
      id `charm.atom-not-permitted`. `This_module_mints_no_new_reason_code_and_registers_a_namespace_instead`
      asserts both halves — the four names are **absent** from `AtomRejectionReason` and **present**
      in the local enum
- [x] ⛔ **The carry LIMIT is a soft, configurable ladder — there is no hard ceiling anywhere, and the
      parser refuses one BY NAME.** §3.3 says *"6 AP at start, 20 AP at cap"*; AGENTS.md forbids a hard
      progression ceiling. So `capacityLadder` in `data/tuning/charm-attunement.v1.json` is
      `[6,8,10,12,14,16,18,20]` and **20 is the last AUTHORED rung, not a maximum**: `CapacityAtRung`
      past the end returns the last rung (content exhaustion, and the comment says so),
      `CharmPouchGate.Explain` takes whatever capacity it is handed, `SetCharmCapacity` writes 10,000
      without complaint, and the DDL carries no `CHECK` and no ceiling column.
      `A_capacity_ceiling_key_is_refused_at_load_by_name_rather_than_ignored` appends `maxCapacityAp` to
      the **real file** and asserts `charm.capacity-ceiling-not-permitted` — the device module 18 used
      for its withdrawn `carryLimit` key, because a ceiling key that parses and does nothing is worse
      than one that works. `The_gate_carries_no_max_charms_parameter` sweeps the whole public surface by
      reflection for `maxCharms` / `charmSlots` / `maxCapacity` / `capacityCap`
- [x] ⚠ **The axis cap (3) and copy cap (2) are NOT progression ceilings, and the distinction is
      written down rather than assumed.** They bound **loadout composition**, never a magnitude — nothing
      here caps how strong a charm may be. They are therefore ordinary tunables in
      `charm-attunement.v1.json` (a balance pass moves them with a file save), and §3.3's own reason for
      making the axis cap a **rejection** rather than a soft cap is quoted beside it: *"a fourth
      same-axis charm contributing nothing is a silent no-op, which is exactly what this program exists
      to remove"*
- [x] **Nine structural invariants are checked at tuning load, each with its own rule id**, so a balance
      pass reads which one it broke: `charm.ap-domain-empty` / `-not-positive` / `-unordered`,
      `charm.capacity-ladder-empty` / `-unordered`, ⭐ **`charm.starting-capacity-below-largest-charm`**
      (a start below 5 AP makes every signet dead content on day one — §6.1's *"a signet is 5 of 6"*),
      `charm.unique-carry-cap-not-tighter` (inverted, "unique" would silently **loosen** the class it
      restrains), `charm.binding-priority-not-below-equipment`, and
      `charm.binding-source-collides-with-draught`. **No key has a default**: a gate silently running on
      a defaulted capacity is an unreviewed number reaching every pouch in the game
- [x] ⭐ **"One snapshot mechanism, two sources" is now CHECKABLE, not a sentence.**
      `The_run_start_binding_priority_mirrors_module_18s_draught_priority_value_for_value` reads **both
      real tuning files** and asserts `charm-attunement.v1.json`'s `bindingPriority` equals
      `consumables.v1.json`'s `draughtBindingPriority` (−100). A balance pass that reorders one
      run-start layer and forgets the other is a red test instead of a silent split.
      `Withdrawal_is_by_source_…` asserts the two keys differ, and the parser refuses
      `bindingSource: "draught"` by name — sharing the tag would make one run-end withdrawal take both
      layers down
- [x] ⛔ **No second snapshot mechanism was built, asserted by reflection.**
      `There_is_no_second_snapshot_mechanism_and_the_binder_declares_no_clock` scans `CharmRunBinder`'s
      whole public surface for `Expire` / `Duration` / `Tick` / `Until` / `Ttl`. `effect_binding` carries
      no expiry, duration or until-tick, so a timed buff is a status and a run-scoped one is a
      lifecycle — module 18's finding, re-asserted here rather than re-derived
- [x] ⭐ **Resonance counts nothing of its own — module 12's evaluator, driven, not forked.**
      `CharmRunBinder.ResonanceTiers` builds `CharmResonance.Consumer(axis, table)` per axis and calls
      `ThresholdEvaluator.Grant`. Cumulativeness comes free and is asserted from that direction: three
      survivability charms hold **both** the 2-tier and the 3-tier
      (`Resonance_tiers_come_from_module_12s_evaluator_and_are_cumulative`), and
      `The_binder_counts_nothing_of_its_own_and_agrees_with_the_evaluator_directly_driven` runs the
      evaluator by hand over the same snapshot and demands the identical list
- [x] **The seal: bindings read the snapshot, never the live pouch.**
      `Bindings_apply_from_the_run_start_snapshot_not_the_live_pouch` edits the pouch after
      `Snapshot(...)` and shows the new charm reaching **no** binding.
      `The_snapshot_seq_is_stable_across_input_orderings` pins `seq` as a determinism input — ordinal by
      instance id, so two replays cannot disagree about row order (module 18's own reason for `seq` on
      `rpg_run_draught`, adopted)
- [x] **Refuse, never silently hold — at both ends of the lifecycle.** `Unattune` on a held charm
      refuses `CharmInUse` **and names the run** (`expedition#1`), the pouch row survives, and closing
      the run frees it (`Un_attuning_a_held_charm_refuses_CharmInUse_and_the_row_survives`). Attuning a
      held instance into a *second* player's pouch refuses the same way. ⭐ **And the pouch stays
      editable**: `The_pouch_stays_editable_while_a_run_holds_only_some_of_it` holds one of three charms
      and un-attunes another successfully — §3.8's *"freezing the whole pouch while any run is live
      would be miserable once expeditions run 20 hours in parallel"*
- [x] **Run end leaves the audit trail.** `CloseCharmRunHold` sets `active = 0`; the rows **stay**
      (`An_inactive_hold_frees_the_charm_for_the_next_run_and_stays_for_audit` asserts all three still
      readable, all inactive, and the next run sealing cleanly). Deleting them would take the replay
      input with them
- [x] **The gate returns EVERY refusal, never first-fail** (module 17's rule, kept): a pouch reported one
      problem at a time is one round trip per mistake and the player is holding all of them at once.
      `The_gate_returns_every_refusal_rather_than_first_fail` drives five distinct failures through one
      call. `Ap_budget_axis_cap_and_copy_cap_each_refuse_with_their_own_reason` reproduces
      **§6.3's own loadouts C and D** as fixtures — 9 AP against 8 is `CharmBudgetExceeded`; a fourth
      offense charm is `CharmAxisOverflow` **and** `DuplicateKey`, which is exactly §5.2's argument for
      keeping the axis code separate ("drop *this* charm, not any charm")
- [x] **§6.3's wide and tall loadouts both still fit the same 8 AP**
      (`The_wide_and_tall_loadouts_of_section_6_3_both_fit_the_same_eight_AP`). If either stops fitting,
      the packing decision the whole mechanic exists for is gone and nothing else would have said so
- [x] **The `unique_carry` tighter cap is real** — two copies of an ordinary charm pass, two signets
      refuse with a detail naming `unique_carry`. And it is **class-shaped in the corpus**: exactly the
      7 signets carry it and nothing else does, measured
      (`Exactly_the_seven_signets_are_unique_carry_so_the_tighter_copy_cap_is_class_shaped`)
- [x] ⭐ **Resonance containers can never enter the pouch — in BOTH shipped spellings.** §4.2's device is
      *"a `charm.` container with no `charm_def` row is not attunable"*, and the corpus ships all ten
      resonance ids **unpadded** (module 12 measured that divergence rather than renaming it — four
      moving parts, one a frozen registry). So the gate's predicate accepts 1–2 digits, or all ten walk
      straight in. `All_ten_shipped_resonance_containers_are_refused_by_the_pouch_gate` refuses each by
      its **authored** id; `No_shipped_charm_id_is_mistaken_for_a_resonance_container` checks the false
      positive, which is the invisible half of the same bug. At the DAL,
      `The_resonance_table_never_becomes_attunable` shows the ten going into `charm_resonance` and into
      nothing else, then refuses `Attune` on every one
- [x] ⛔ **`long` for every magnitude, `checked` throughout, and the overflow is asserted.** `ap_cost`
      and every AP total are `long`; `CharmPouchGate.TotalAp` sums inside `checked` and
      `An_AP_total_overflows_by_throwing_never_by_wrapping` asserts the `OverflowException` — a wrapped
      AP sum is a pouch that fits everything, the budget silently gone, with a green suite. A negative
      capacity **throws** at both the gate and the store rather than clamping to zero (a clamp would
      silently empty the pouch)
- [x] **Server boot wired** — `Program.cs` parses `charm-attunement.v1.json` at startup (so a ceiling
      key, an inverted cap or a `player` owner kind fails there, not at the first dispatch) and imports
      the charm corpus after `store.Init()`, non-fatally, matching modules 11/12's own rule.
      `resonance.json` is routed to `charm_resonance` **only**, which is what keeps §4.2's device true
      at boot as well as in a test

**⛔ Defects and divergences found while building, all named rather than absorbed:**

1. ⛔ **The P5.5 stub's own `player:{id}` claim is stale against D33(a)** — see the table at the top of
   this section. Not a defect in module 18's code (its draughts really do bind at `player:`, which is
   ssot-consumables' own ruling); a defect in the **inherited sentence** that `source` is the only
   difference. **Cross-referenced into P5.2 (module 18).** ⚠ It is also live in two lane docs:
   `ssot-charms.md` §3.8's run-start row and `ssot-consumables.md` §9 item 10 both still say
   `player:{id}` while `ssot-charms.md` §3.1's own banner says the opposite. **Not edited from here** —
   a lane doc's prose is its owner's, and the banner already carries the ruling; recorded so the next
   reader of §3.8 does not build against the withdrawn option C.
2. ⛔ **`CharmAttunementTuningRejection` first registered the wrong namespace, and the closed-vocabulary
   guard caught it.** It copied module 12's `ThresholdEvaluator.EnsureRegistered()` while raising
   `charm.*` rule ids, and `AtomRejection.ContentRule` **throws** on an unregistered prefix rather than
   accepting an unknown vocabulary — so the first run of
   `A_capacity_ceiling_key_is_refused_at_load_by_name_rather_than_ignored` failed with
   `InvalidOperationException` instead of the rejection. Fixed to `CharmCarryRules.EnsureRegistered()`.
   Named because it is the guard working exactly as designed: a copied registration is the most likely
   way a new lane's namespace goes wrong, and it cost one test run instead of a mystery at boot.
3. ⚠ **`ListCharmDefs` round-trips `CharmDef` only PARTIALLY, deliberately, and the code says why.**
   `charm_def` holds no `prefix_rolls` / `suffix_rolls` / negative-atom flag, because those are the
   **corpus's** facts and module 12's `CharmCorpus.ValidateClassRules` already enforces them at parse
   time. Storing them here would be a second, weaker source for a rule that already has one. The
   reconstructed record therefore reports `0/0` rolls and derives the drawback flag from the class; a
   caller that needs the roll shape reads the corpus, not the table.
4. ⚠ **`players` still has no level column** — `(id, name, created_utc, world_seed)`, checked against
   the live DDL, not the doc (which still says three columns). `ssot-charms.md` §8 item 6's question is
   therefore **still open**, and the gate does not paper over it: a charm with a `level_req` and no
   supplied player level refuses `PlayerLevelUnavailable` rather than passing a check it cannot make
   (SC6). ⏸ Inert today —
   `No_shipped_charm_declares_a_level_req_so_the_player_level_gap_is_inert_today` reads all four corpus
   files and shows the key absent from every one. Not this module's to answer.

**Three corpus facts measured here for the first time, each pinned as a test:**

- ⚠ **The axis distribution is 20 / 10 / 10 / 10 / 10 — `economy` ships twice as many charms as every
  other axis.** Not a defect: §3.5's axes are **open categories**, not quotas, and every axis still
  clears the cap of 3 comfortably, so the cap binds on the player's packing rather than on what the
  corpus can supply. Pinned in `No_axis_can_be_starved_by_the_axis_cap_and_economy_is_the_deepest_pool`
  so a balance pass can see it move.
- ✅ **Every axis can actually reach its top resonance tier** — a 3-tier on an axis with two charms
  would be unreachable and invisibly so
  (`Every_axis_has_enough_shipped_charms_to_reach_its_top_resonance_tier`, driven off both real files).
  And the two sets of axes are **equal**: no charm axis lacks a ladder, no ladder lacks charms.
- ⚠ **All 60 charms declare `frameHint: any`, so §3.7's frame check is structurally present and inert.**
  Written anyway, and measured
  (`Every_shipped_charm_declares_frame_hint_any_so_section_3_7s_check_is_inert_and_that_is_measured`),
  because §3.7's whole point is that the **first** frame-restricted charm must not ship as a silent
  dud. Named so a later session does not read the observation as "the check is dead code".

**⏸ Deferred, each with a named owner and a reason:**

- [ ] ⏸ **X7 again — nothing this module binds has a legal `ContainerKind` yet, and that is the same
      wiring gap modules 11, 12, 13, 16, 18 and 21 all carry.** `ContainerRow.cs` ships six values
      (`Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff`) and D27's `charm` is not one of
      them, so `charm_def.container_id` carries **no FK** and `CharmRunBinder.Bindings` produces binding
      **rows** rather than writing them. The grammar row in `definitions.md` §1 is the SSOT the id regex
      mirrors and it wins over any spec — **an ask, owned by effect-atom, not an edit from here.**
- [ ] ⏸ **No production caller seals a run yet, and the missing caller is expedition dispatch — the
      SAME gap module 18 left.** Verified rather than assumed: `TrySpendDraughts` has no production
      caller either (grep over `src/`), so neither run-start layer is wired to dispatch. Both halves of
      this module's seam ship and are tested — `ListPouch` / `OpenCharmRunHold` / `HeldByLiveRun` on one
      side, `Snapshot` / `Bindings` / `RefuseUnsupportedScope` on the other. **A wiring gap with a named
      trigger (the dispatch transaction, plus X7 before a binding can be written), not a design gap.**
      Wiring both layers in one change is the right shape, because they must seal in one transaction.
- [ ] ⏸ **I13's sinks do not yet consult `charm_pouch` / `charm_run_hold`.** ssot-charms §8 item 2(b)
      asks that an attuned or held charm cannot be salvaged, sold or destroyed. That is a check inside
      **module 2/14's** salvage and transfer paths (`RpgStore.Items.cs`, `SalvageGuards`), and it is
      shaped exactly like the `rpg_delve_pack_lock` row the party-dungeon program filed into
      `item-map.md` §9 — so the two should land as one arm, not two. `HeldByLiveRun` is the read those
      paths need and it ships here.
- [ ] ⏸ **Capacity GROWTH is progression's, not this module's** (§8 item 11). `SetCharmCapacity` is the
      write and nothing calls it in production; whether 6 → 20 competes with expedition slots (2 → 5) is
      the owner's open question 11 and is deliberately not answered by a ladder that only lists rungs.
- [ ] ⏸ **`CharmCarryRules.AtomNotPermitted` is declared and not yet raised.** §5.2 code 5 is an
      **import-time** check on a charm container's ATOMS (`op = Increased`/`More`, or a
      `board.*`/`grid.*`/`box.*`/`spawn.*` kind) — and the corpus holds **seeds**, which carry a
      `family` and a `powerBand` and no atom rows at all (seed-contract.md §3). There is nothing to
      check until the runtime generator rolls a seed into a concrete container, which is the binding
      seed-to-concrete rule. The rule id and its doc comment ship so the check has a home; raising it is
      the generator's, not this module's. Same disposition module 18 gave its own atom-level rules.
- [ ] ⏸ **`ssot-charms.md` §9's eight owner questions stay open and none of them blocks this build.**
      Cross-run exclusivity's harshness (q1), the 6→20 / {1,2,3,5} shape (q2), commander interaction
      (q3), tradeability (q4), lawn-only charms (q5), the five-code ask (q6 — **answered in practice
      here: none minted**), resonance scaling with deployed count (q7 — built **flat**, which is the
      lane's own recommendation), and whether the axis cap should exist at all (q8). Every one is a
      tunable or a content question, and each is a file save away.
- [ ] ⏸ **The module still has no spec file of its own** — `item-map.md` row 22 points at
      `spec-threshold-grants.md`, whose *"Charm carry runtime"* section is the spec this module was
      built from and is complete enough that nothing was guessed. ⚠ Its **Project structure** block
      names `src/FusionRpg.Core/Items/CharmPouchGate.cs`; the files landed under
      `Items/Thresholds/` beside module 12's, because that is where the machinery this module extends
      lives and a sibling directory would have split one mechanism across two. Recorded as a knowing
      deviation, not a drift.

**Verification, run and green:**

| Command | Result |
|---|---|
| `dotnet test tests\FusionRpg.Core.Tests --filter CharmCarry` | **48 passed** (new — `CharmCarryTests` 33, `CharmCarryCorpusTests` 15) |
| `dotnet test tests\FusionRpg.Data.Tests --filter CharmCarry` | **19 passed** (new — `CharmCarryStoreTests`) |
| `dotnet test tests\FusionRpg.Core.Tests` (full) | **7096 passed / 6 failed / 7102 total.** ⚠ Five are the session baseline's own (`ActorHub.SpecChannelClaimTests`, `Expeditions.ExpeditionResolverTests.Tier_goldens_are_locked`, 3 × `ClassSystem.ProveAptitudeJsonEmitTests`) — all the concurrent class-system / world streams'. The sixth, `Demons.DemonQualityReportTests.A_perfectly_even_split_reports_entropy_1_00`, is **build contention, not a failure**: it shells out to `dotnet run` and got *"Error writing to source link file … used by another process"* while the other stream was rebuilding `FusionRpg.Core`. **Re-run in isolation: 1 passed.** **Zero** failures in `Items.*` |
| `dotnet test tests\FusionRpg.Data.Tests` (full) | **842 passed / 0 failed / 842 total.** ⭐ Better than the recorded baseline of 3 red — the `AtomStoreTests` and `DemonSpeciesImportCliTests` failures earlier modules carried are gone, and this run did **not** hit the intermittent host crash P3.2 recorded |
| `dotnet test tests\FusionRpg.Guard.Tests` | **204 / 204**, up from 184 at P3.2 |
| `dotnet run --project tools\ItemSeedValidator` | **170 errors across 120 partitions — identical to modules 17, 18, 19 and 20's baseline.** Zero new findings; the four `charms/*` partitions carry only the two pre-existing `MetaRegistryVersion{Mismatch,Behind}` notices every partition carries. This module authors **no** seed content |
| `python scripts\audit-overflow.py` | **0 critical**, 59 findings — **zero** under `Items/Thresholds/` and zero naming a charm path |
| `python scripts\audit-magic-numbers.py --summary` | **M1 = 0, M2 = 0**, 13 M3 total across 8 domains; **zero** under `Items/Thresholds/` and zero in the `items` domain from this module |
| `.\scripts\guard-dal.ps1` / `guard-single-writer.ps1` / `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` | **all four OK** — the five charm tables' SQL stays inside `FusionRpg.Data` |
| `dotnet build src\FusionRpg.Server` | **Build succeeded** — boot parses the tuning and imports the corpus |
| `python -m pytest` (seedsmith, full) | **1678 passed, 1 skipped**, 288 subtests — unaffected; **no Python content was touched** (this module reads the charm corpus and writes none of it) |

⚠ **Two transient build failures from the concurrent stream, both waited out rather than worked around**
(`SiegeTuning.SiegeTuning` gained a required `Economy` parameter mid-session and both test projects'
`ContractTuningTestBootstrap` lagged it by a few minutes; and a `data/tuning/loopwarntest*.json` fixture
appeared and vanished under the server's copy step). Neither touches a file this module owns, and both
cleared on retry. Recorded so a later reader does not mistake the retries for flakiness here.

**Files:** `data/tuning/charm-attunement.v1.json` (new — the AP domain, the capacity **ladder**, the
axis/copy/unique caps, and the run-start binding shape including D33(a)'s owner kind);
`src/FusionRpg.Core/Items/Thresholds/{CharmAttunementTuning.cs, CharmPouchGate.cs, CharmRunBinder.cs}`
(new); `src/FusionRpg.Data/Sqlite/RpgStore.Charms.cs` (new — ssot-charms §4.2's five tables,
`ImportCharmCorpus`, `ListCharmDefs`, `ListCharmResonance`, `Get`/`SetCharmCapacity`, `Attune`,
`Unattune`, `ListPouch`, `HeldByLiveRun`, `OpenCharmRunHold`, `CloseCharmRunHold`, `ListCharmRunHold`);
`src/FusionRpg.Data/Sqlite/RpgStore.cs` (EDIT — `EnsureCharmSchemaUnlocked` in `Init`);
`src/FusionRpg.Server/Program.cs` (EDIT — parses `charm-attunement.v1.json` at boot, imports the charm
corpus after `store.Init()`); `tests/FusionRpg.Core.Tests/Items/{CharmCarryTests.cs,
CharmCarryCorpusTests.cs}`, `tests/FusionRpg.Data.Tests/Items/CharmCarryStoreTests.cs` (new — 67 tests).

**Depends on:** module 12. **Verify:** `dotnet test tests\FusionRpg.Core.Tests --filter CharmCarry`; `dotnet test tests\FusionRpg.Data.Tests --filter CharmCarry`; `.\scripts\guard-dal.ps1`

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
