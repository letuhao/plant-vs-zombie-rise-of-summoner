# Tasks: effect-atom

Plan: [effect-atom-plan.md](effect-atom-plan.md) · Map: [../docs/architecture/effect-atom-map.md](../docs/architecture/effect-atom-map.md) · Specs: [../docs/architecture/effect-atom/](../docs/architecture/effect-atom/)

> **Spec closed 2026-08-22** after four adversarial passes. [definitions.md](../docs/architecture/effect-atom/definitions.md) **wins over any spec**; its §13 is the defect log and says which build position owns each open item. Nothing in that log gates a module before position 15.

> ✅ Committed by the owner at `842907f` — the untracked-spec risk is closed.

> ⚠ **All 21 rows below are `[x]` and every suite is green — and a completeness audit (2026-08-23)
> found that most of this layer does not reach the running game:** no host loads a content table, nothing
> runs the importer, nothing creates a binding, and E17's stat payload has a parser with no applier.
> Findings and a proposed wave 6: [completeness-audit.md](../docs/architecture/effect-atom/completeness-audit.md).

## Wave 1 — the spine (Checkpoint A: nothing in the game changes)

- [x] **E1: atom-kind-registry** — accepted 2026-08-22. 12 kinds, 5 attach points, 7 triggers, 33 reason codes, four-state runtime matrix.
  - Post-audit corrections applied: `RuntimeSupportMatrix` (Full/Partial/PlanOnly/None) replacing a 3-flag bitfield · E1 owns the trigger vocabulary with count guards · `CostHook` removed (E9's types) · `stat.derived` quarantined `None/None/None`, `resource.delta` battle and `shield.grant` battle+sim → `None` (D6) · `stat.modify`/`stat.derived` carry **no trigger** (§14.2).
  - [x] **E1-follow-up: re-derive five param schemas** — DONE 2026-08-22 (10 tests pin each schema to its executor; G5 reclassified as a runtime hole) (§13 **D7**) — `box.set.boxType` is declared String and read as int; `status.apply` declares `statusId`/`durationMs` where FA2 reads `status`/`duration` as float seconds; `status.apply.target` is required but FA2 has no such param; the DoT/contagion payload lives on FA10 not FA2; `shield.grant` omits `sourceClass`, which its executor honours. Also declare `spawn.entity.count` (`min: 1`, default 1).
    - Acceptance: every declared key is read by its executor, with the right type; a kind spanning two opcodes says so; tests cover each corrected key.
    - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom"`.
    - **Must land before E7**, where a wrong schema first produces a wrong grant. Scope: M.

- [x] **E2: value-spec-and-curve** — BUILT 2026-08-22. 28 tests; `effect_curve` DDL+DAL; `SeededRng`-backed named streams; zero-alloc resolve.
  - Files: `src/FusionRpg.Core/Effects/Atoms/{ValueSpec,CurveTable,AtomRandom}.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Curves.cs`.
  - Note: `SeededEffectRandom` was also moved off `System.Random` (§13 **D5**) — zero goldens moved, verified.

- [x] **E3: predicate-tree** — BUILT 2026-08-22. 23 tests incl. a 10⁴-tree equivalence fuzz against a naive reference interpreter, short-circuit proven by a counting `FactReader`, zero-alloc evaluate. Typed object graph (the 7 ns benchmark shape); `ICompiledPredicate` is the contract so **E13** can swap the encoding without reopening this module.
  - Description: typed AND/OR/NOT over the 8 closed leaves, depth ≤ 4 and ≤ 16 nodes, `subject` required on **every** leaf, ship the `CompiledPredicate` interface plus equivalence fuzz so **E13** can choose the encoding later without reopening E3, narrow readonly `FactReader`.
  - Acceptance: unknown leaf rejects (`UnknownLeaf`); depth 5 rejects (`DepthExceeded`); 17 nodes rejects (`NodeCountExceeded`); missing `subject` rejects (`AmbiguousSubject`); empty AND/OR node rejects (`EmptyNode`); empty tree is `true`; **equivalence fuzz** 10⁴ trees ≡ reference interpreter; short-circuit proven by a counting reader; zero allocation. **No no-self-call test** — it would disqualify the encoding E13 measures as fastest.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Predicate"`.
  - Files: `src/FusionRpg.Core/Effects/Atoms/{PredicateNode,PredicateCompiler,FactReader}.cs`, tests. Scope: M.
  - Dependencies: none. Spec: [spec-predicate-tree.md](../docs/architecture/effect-atom/spec-predicate-tree.md).

- [x] **E4: atom-schema** — BUILT 2026-08-22. `effect_atom` + `content_meta`, `AtomRow`, `AtomRowValidator`, 23 tests. Includes `icd_key`, the nullable extracted `trigger_id` index, `variant` as `''` never NULL, and the per-row rejection log (one bad row in fifty loads forty-nine). **Completed 2026-08-22 (second pass):** the first cut wired only E1 — predicate trees and value specs in a row were entirely unvalidated, and D9 was unimplemented. `AtomJson` now reads both canonical shapes, the validator runs them through E3's compiler and E2's `ValueSpec.Validate`, and the store supplies a curve-input resolver so D9 and unknown-curve are real refusals. 15 more tests. **Also added `CurveStoreTests`** — E2's DDL had never been exercised, because `RpgStore.Init()` builds the schema and no test had called it.
  - Description: `effect_atom` DDL + DAL (**`(family_id, tier, variant)`** unique, `kind_id` and trigger indexed) plus **`content_meta`** (the single `catalog_revision` row), `AtomRow`, `AtomRowValidator` wiring E1/E2/E3 checks. Whole-row rejection.
  - **New this round:** the **`icd_key`** column (TEXT, nullable, defaults to `atom_id` — E7 groups on it, §14.1); the trigger column is **nullable** and the `when_json.trigger` key is simply **omitted** for permanent modifiers (no `None` trigger name, so the closed count stays 7); `input: level` on an `OnApply` value spec rejects `BadValueSpec` (§13 **D9**).
  - Acceptance: round-trip byte-identical JSON; each validator case rejects; one bad row in 50 loads 49; revision bumps on edit; a triggerless `stat.modify` row round-trips; a trigger on `stat.modify` rejects `TriggerNotAllowed`; `guard-dal.ps1` passes.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AtomStore"` + `.\scripts\guard-dal.ps1`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Atoms.cs`, `src/FusionRpg.Core/Effects/Atoms/{AtomRow,AtomRowValidator}.cs`, tests. Scope: M.
  - Dependencies: E1, E2, E3. Spec: [spec-atom-schema.md](../docs/architecture/effect-atom/spec-atom-schema.md).

- [x] **E5: container-schema** — BUILT 2026-08-22. `effect_container` + atom/pool children + `rarity`; `ContainerRow`/`ContainerValidator`; 38 tests. Drawable-group rule enforced, group key defaults to `family|variant` (separated, not concatenated), rarity ordinals append-only, whole-container replacement on upsert. **Checkpoint B reached.** The contract is published in the spec's §"The contract action A1 consumes". It gates **A1 in the action program** ([action-map.md](../docs/architecture/action-map.md) B1: *"do not start A1 before it"*) — not E6. The action program has no specs, no build authorized, and no consumers of this contract, so review is due when A1 starts, not now. — ⚠️ **Checkpoint B: review before combat-action builds on it**
  - Description: `effect_container`, `effect_container_atom` (ordered, overrides), `effect_container_pool` (weighted, grouped), plus the `rarity` table. Fixed core + optional weighted pool.
  - Acceptance: `seq` order stable; `pool_rolls` > distinct **drawable** groups rejects (a group whose every row is `weight = 0` does not count — counting it passes validation and then under-fills the instance); one atom per group per draw; negative weight rejects (not clamped); `weight = 0` kept but never drawn; override naming an undeclared param rejects; override changing `kind_id` rejects; `rarity` ordinals explicit and **append-only**.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContainerStore"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs`, `src/FusionRpg.Core/Effects/Atoms/{ContainerRow,ContainerValidator}.cs`, tests. Scope: M.
  - Dependencies: E4. Spec: [spec-container-schema.md](../docs/architecture/effect-atom/spec-container-schema.md).

## Wave 2 — the layer does work (Checkpoint C)

- [x] **E6: instance-and-binding** — BUILT 2026-08-22. `effect_instance` / `effect_instance_atom` / `effect_binding`; `OwnerScope` (7 scopes), `BindGate` (G8 corrected — defense rejects at **every** non-match scope), `Instantiator` (weighted draw, one-per-group, freeze). **61 tests.** Reproducibility asserted over a content fingerprint that excludes the generated `instance_id` and `created_utc`; `OnApply` values proven left unresolved; `power_json` null pending E9; ties break on `container_id`, never on the generated `binding_id`. Dependencies: E5.
  - **Completed 2026-08-22 (second pass):** `BindGate` shipped called from nowhere but its own 34 tests — the E4 defect again. Added **`ResolveBindings`**, where a host asks what may execute and the gate actually runs; `Bind` stays persistence, because a durable binding outlives any one runtime. Added **`catalog_revision`** to `effect_instance`, without which the `(container, catalog_revision, roll_seed)` reproducibility claim had nothing to compare and `StaleInstance` was undetectable. 9 more tests.
  - **`mods_json` migration moved to E11 — it cannot be done here.** E6's task line assigned it, but
    `effect_binding.instance_id` points at an instance of a **container**, and a legacy grant points at an
    `effectId` (`fx.butter_on_hit`). Those defs do not become containers until **E11**
    ([spec-effect-def-migration.md](../docs/architecture/effect-atom/spec-effect-def-migration.md): *"two atoms,
    one container"*), so at E6 there is nothing to instantiate and nothing to bind. A planning error in the
    original task line, not optional work.
- [x] **E13: runtime-form-benchmark** — BUILT 2026-08-22. **Winner: the flattened non-recursive form**, wired into `PredicateCompiler.TryCompile`; the typed graph is kept as the fuzz reference and the second candidate. Cold-cache win of 15–20% over 200 varied predicates — this **reverses** the scratch benchmark, which used six identical trees and a naive `ref int pc` walker. New `tests/FusionRpg.Bench` project; CI guard at ≤ 50 ns/atom (observed **26.94**, fails above 75), zero alloc, plus a test that `TryCompile` still returns the chosen form. Results: [atom-runtime-form.md](../docs/research/perf/atom-runtime-form.md). Two methodology errors found and recorded: the alloc probe measured its own `Stopwatch`, and sequential measurement let drift pick the winner. — candidate comparison over ~200 real atoms, cold-cache; pick the encoding; CI budget guard ≤ 50 ns/atom, zero alloc. Dependencies: E4, E2, E3.
- [x] **E7: atom-compiler** — BUILT 2026-08-22. `Compilability` (pure classifier), `AtomCompiler`, `RunnerEntry` + `CompiledCatalog`. **22 tests.** All three late rules implemented: ICD-group → one def with the **union** of triggers; triggerless modifiers emit `EffectType = Passive`; curve-scaled bounds are **pre-multiplied** so no curve row travels (D9). The subject trap is a test, not a comment — `subject: self` on `OnDamageDealt` goes to the runner even though it looks identical.
  - **Spec correction found by building it:** the spec said *"emit `EffectGrantDto`"*, but triggers, `EffectType` and actions live on **`EffectDefDto`** — a grant only carries an overlay and an `effectId`. A compiled ICD group therefore emits **one def + one grant**, and `CompiledCatalog` carries both. Without this the trigger union had nowhere to go and `Passive` was unreachable. — pure compilability classifier (incl. the `subject: target` legacy-filter rule), emit `EffectGrantDto` + `RunnerEntry`, bake into E13's form, server-side. **Three rules added this round:** group atoms by `COALESCE(icd_key, atom_id)` into **one** grant with the union of their triggers (§14.1); emit `EffectType = Passive` for triggerless `stat.modify`/`stat.derived`, or the grant never fires (§14.2); bake **pre-multiplied `(Min, Max)`** for curve-scaled values so no curve row travels (**D9**). ⚠️ Write "the Writer", never the type name — `guard-funnel-delta.ps1` matches it in comments. Dependencies: E6, E13, **E1-follow-up**.
- [x] **E8: content-hash** — BUILT 2026-08-22. `ContentHash` (canonical form + sorted concat), `ContentHashRegistry` (versioned covered set, order = table name ordinal), `ContentHashStamp` + `ContentHashComparison` (the replay verdict), `RpgStore.ComputeContentHash`. **65 tests** (44 Core, 17 Data, 4 E2E). All four task clauses delivered: sort-then-concatenate with the duplicate-row case asserted; columns length-prefixed; a versioned registry; and a differing `contentHashSchemaVersion` reports added/removed tables and still compares the shared per-table digests instead of refusing.
  - **Spec defect found by building it (definitions §8 corrected):** NULL was specified as a literal `\x00` byte, with the claim that the length prefix kept it distinct from a string containing one. It does not — `"\0"` is also one byte of `0x00` under prefix `1:`, so any value could forge a NULL column. NULL is now the sentinel `N:` with no payload. Caught RED by `ContentHashTests.Null_and_a_single_nul_character_string_do_not_collide`.
  - **Wired to a real consumer, not left for E19:** `WebMatchService.SweepUnresolved` now refuses to re-resolve a logged match across edited content, beside the version and platform stamps it already checks (new nullable `rpg_web_match_log.content_hash`). The stamp is hashed **once per sweep**, not per row. Report stamping stays E12's — a new stamped report field *is* a golden diff.
  - **Not cached on `catalog_revision`:** the revision is bumped explicitly (E14a, once per import), so a direct upsert changes content without touching it — a cache keyed on it would serve a stale hash for exactly the hand edit this module exists to make visible.
  - Registry v1 covers the six tables that exist. `effect_element` + both matrices join at **E18** (v2); `power_coefficient` + `power_trigger_frequency` at **E9** (v3). Both must bump `CurrentSchemaVersion`.
- [x] **E15: atom-runner** — BUILT 2026-08-22. `TriggerIndex` (trigger interned to an ordinal, slots not string keys), `RunnerState` (flat arrays: ICD, charges, meters, caps), `AtomRunner` (the gate ladder + Funnel dispatch), `RunnerEventMapper`. **34 tests** (26 runner, 8 cap). `capPerMatch` now has an implementation for the first time since the FA9 allowlist declared it. Gate order is cheapest-first with the cap last, and a capped binding is proven to sit at the same RNG stream position as an uncapped one — the property that keeps a replay valid. Chance is pinned to an **exact count** over 10⁴ events against an independently-read draw sequence, not a tolerance. Zero allocation over 10⁵ gated events.
  - **E7 contract gap found by building it:** `Compilability` routes an atom to the runner *because* it declares `capPerMatch` / `charges` / `everyHits` / `maxStacks`, and `AtomCompiler` then dropped every one of them plus all non-value params. The classifier and the payload disagreed, so this module's headline deliverable was unimplementable. `RunnerEntry` now carries `Limits` and `Params`.
  - **`RunnerLimits.None` was silently the most restrictive limits there are.** `new()` on a record struct ignores positional defaults and zeroes the fields — cap 0, charges 0. Positional defaults removed; `default != None` is pinned by a test.
  - **No cooldown implemented, deliberately.** The spec lists a cooldown as distinct from an ICD, but no kind schema declares one and nothing routes on it. Inventing the key would widen a closed vocabulary by convenience.
  - **The caller is `SimEffectHost`** — it drives the runner from the same event the bag sees and resets caps on `BeginMatch`. A host with no runner behaves exactly as before.
  - **Ordering corrected 2026-08-22 (E19 cycle):** the runner must run **before** `Bag.OnEvent`, not after. `EffectBag.OnEvent` flushes the Funnel inside itself, so a dispatch enqueued afterwards waited for the next event — a silent one-event lag on every proc. Caught RED by asserting the funnel was *drained* rather than merely pending.
  - ⚠️ **Handoff to E19: nothing emits a def for a runner atom.** A dispatch names the atom id as its `effectId` and `EffectBag.Grant` throws on an unknown one; E7 emits defs only for the compiled path. E19 must ship one def per runner entry, built from `RunnerEntry.Params`.
- [x] **E19: compiled-push** — BUILT 2026-08-22 (contract, codec, server, injector receiver, hub). **68 tests** (25 contract, 15 server, 13 installer, plus runner coverage). — `AtomPushDtos` (Contracts), `AtomPushCodec` (Core, one codec both ends), `AtomPushService` (Server: resolve → compile → negotiate revision → seed), and **`tests/FusionRpg.Server.Tests` created** with a solution entry. **40 tests** (25 contract, 15 server). Full set never a delta; an up-to-date receiver gets an empty apply that still carries the content hash and the seed; the payload is asserted to contain no atom, container, curve or instance column.
  - **A compiled predicate travels as itself.** `FlatPredicate`'s ops are already all ints, so the injector rebuilds the evaluator with no status catalog, no element roster, no content row — proven by a 4096-case fact matrix per predicate shape, guarded against vacuity (at least one true and one false required).
  - **Runner binding id is `(binding, atom)`.** A container with two runner atoms needs two ICD clocks and two caps, and a shared id would also tie the `(priority, bindingId)` sort.
  - **E1's `capPerMatch` "not available yet" guard lifted** — it refused the param at load, so the counter E15 shipped was unauthorable by the content it exists for. `AtomKindRegistryTests` flipped to assert the opposite.
  - **`BindResolution` now carries the atom rows it already loaded** — the push would otherwise re-query per binding and reopen the N+1 that method was rewritten to close.
  - **Seed derived with FNV-1a, not `String.GetHashCode`** (randomised per process — "same match key, same rolls" would be false after every restart).
  - **Injector receiver BUILT:** `AtomPushReceiver` (install defs + bindings, per-event dispatch, match start, `board.end`), `EffectRuntime` drives it **before** `Bag.OnEvent` on both event paths, and `effects.grants.apply` grew an atom half that only runs when `runnerBindings`/`defs`/`upToDate` is present — the legacy grants-only payload takes exactly the path it always did. The receiver installs defs and bindings but **not** grants: the command runner's existing loop owns owner-key normalisation (`entity:selected`, `instance:` refusal) and skipping it would be silent.
  - **Hub wiring BUILT — the "open question" was a precedent I had not looked up.** `RpgHub` now resolves the atom half **per player** via `GetCurrentPlayerId()`, exactly as `PatronEndpoints.TryBuildPatronCommand` already does for the same Hello-time push shape, and as the middle-layer constitution requires (*"Server stamps `player_id`; injector never sends it"*). The session grant snapshot beside it stays session-scoped, because it always was. Both halves ride one command: a reconnect must not leave the injector holding half its effects, and a failed atom build never costs it the Foundation grants.
  - **The seed does not travel at Hello, and should not.** The lawn match key is born in the injector's `board.start` capture, so the server has none at connect time — the first cut invented a `GetCurrentMatchKey` that does not exist. `MatchSeed.For(matchKey)` now lives in **Core**, both ends compute it, and the receiver reseeds on match start. That is what D5 actually needs: the rolls are reproducible and the match key is already in every event and in `runs`. The wire field stays so a stored seed can override it later.
  - **Receiver tests BUILT via extraction.** The injector cannot host a test project (its host needs the game's interop assemblies), so the state machine moved to Core as `AtomPushInstaller` — humble object — and `AtomPushReceiver` is now a shim holding only what exists inside the game process. **13 tests** on the half that was untestable: up-to-date keeps what is held, a new match rebuilds and reseeds, `board.end` forgets the revision, a mid-match push re-arms without waiting for `board.start`.
  - The dispatch sink is a **required** constructor argument, not a settable property with a refuse-everything default — a host that forgot to wire it would get a runner that silently swallows every proc, indistinguishable from content that never fires.

## Wave 3 — the proof (Checkpoint D)

- [x] **E14a: importer** — BUILT 2026-08-22. `AtomSeedFile` (Core: the format), `RpgStore.ImportContent` (data: validate-all → one transaction → one conditional bump), `tools/AtomImporter` + `SeedScanner`, `data/seed/README.md`, and **`tests/FusionRpg.AtomImporter.Tests` created and wired into CI**. **52 tests** (25 format, 17 import, 10 scanner). Every clause of the task line delivered.
  - **Idempotency prerequisite BUILT.** The spec's "import twice → content hash unchanged" row was **unmeetable**: `revision` is a hashed column (E8) and every upsert bumped it whether anything changed or not, so a repeat import looked exactly like a content edit. Atom and curve upserts now skip the update when no column differs (`IS NOT`, SQLite's null-safe inequality); `UpsertContainer` compares the **whole** container including its children — a parent-column check cannot see a changed atom list, and the first cut made exactly that mistake and was caught by `ContainerStoreTests.Revision_bumps_on_edit`. A revision now counts how many times a row *changed*, not how many times it was written. 5 new tests.
  - **The blind spot was answered by moving, not by discipline** — for two reasons that are not the same one. The write path needs `RpgStore`'s private connection, gate and unlocked writers; and `guard-dal.ps1` scans only `src/`, so SQL under `tools/` sits outside the rule that keeps SQL in one project. The format is in Core, the transaction is in the data project, and the tool is arguments and a report.
  - **My own justification was wrong the first time, and the review caught it.** I wrote — in code comments, the spec and this file — that the move was needed because `tools/` has no test project. `tests/FusionRpg.ItemSeedValidator.Tests` exists and CI runs it. So the tool's one real decision (which files a sweep takes) had no excuse for being untestable: it is now `SeedScanner`, a class rather than top-level statements, with **10 tests** in a new `tests/FusionRpg.AtomImporter.Tests` wired into `ci.yml` beside the item validator's. The headline case is the collision already in the tree — sweeping `data/seed/` recursively refuses all ~125 files of `data/seed/items/`, which is another tool's format, and looks like broken content.
  - **Validate-first and one transaction are two guarantees, and both are needed.** Validating everything before the first write stops a known-bad file landing half a catalog; the single transaction stops a crash or an unforeseen constraint doing the same. The batch is validated against **stored ∪ incoming**, so a container may reference an atom — and an atom a curve — authored in the same import. That is how a new item and its affixes normally arrive; validating against the stored table alone would reject every genuinely new item.
  - **Four folders, not the two the spec named.** `effect_curve` and `rarity` are hashed content tables in E8's registry v1 with upserts of their own; leaving either unauthorable would make a covered table reachable only by hand-editing the database — the same shape as E1 refusing `capPerMatch` for a counter E15 had already shipped. And an atom scaling through a curve is a validation failure until that curve exists, so they cannot land in separate imports.
  - **The importer sweeps exactly its four folders, never the seed root.** `data/seed/items/` already holds 125 files in a different format read by `tools/ItemSeedValidator`; a recursive sweep would refuse every one and report the import broken.
  - **JSON columns are stored canonically**, not as authored. Raw text would make re-indenting a file differ from the stored column, bumping `revision` — a hashed column — and moving the content hash for an edit that changed nothing. Pinned by a test *and* its opposite (a real value edit must still move it), so the claim is not vacuous.
  - **The revision bump is conditional, not merely once per transaction.** Once per transaction rather than per row (a fifty-row file would otherwise move it fifty times) **and not at all when nothing changed** — `catalog_revision` is what E6 reproduces against and what E19 negotiates on, so a bump for unchanged content makes every connected receiver re-download the full push. `UpsertRarity` had no skip-when-identical clause and would have bumped on every import; it now has one.
  - **`--check` runs the whole import and rolls back** — not the same as validating files: it resolves every cross-table reference against the real catalog and lets the database itself refuse, which is what an author wants before an import lands.
  - Cross-file duplicate ids are refused naming **both** files, across all four kinds — one id namespace, because four that only overlap by accident is the more expensive rule to hold.
  - Verified end to end against a real database: clean import → 4 rows, revision 1; re-import → 0 rows, revision and hash held; duplicate, bad row, and dangling container reference each refused with the hash unmoved.
- [x] **E11: effect-def-migration** — BUILT 2026-08-22. **Checkpoint D reached.** 16 defs are rows in `data/seed/atoms/fx-*.json`; every fixture produces an identical plan down both paths; the one-row claim is tested. **92 tests** (12 schema, 73 parity, 6 one-row, 1 compiler). Original scope: 16 defs → rows; **49** fixtures byte-identical; `subject: target` on migrated `OnDamageDealt`; `fx.patron_aura` as a zero-atom marker; **owns `OneRowClaimTests`** (Checkpoint D's claim, tested where it is claimed). Multi-trigger defs resolved: `fx.shield_grant` → 3 atoms sharing `icd_key`, one clock; `fx.passive_atk_flat` → **1** atom, no trigger. `shield.grant.amount` is **optional** with a bind-time presence check against the overlay (**D10**) — authoring a magnitude would break byte-identity. **Also inherits E6's `mods_json` grant migration** (see E6) — one `effect_binding` row per legacy grant, **`absolutes` stay put**: they are Tab B/C `Override` writes on a hand-built channel map, and effects cannot emit `Override` at all, so moving them would smuggle a fourth write path into this program. One-way and idempotent — re-running on a migrated instance is a no-op. **Step 0:** thread a catalog source through the **five** call sites hardcoding `EffectSeedCatalog.CreateAll()` — `BattleEffects.cs`, `SimEffectHost.cs`, `EffectRuntime.cs`, `CheatCommandRunner.cs`, `FoundationHarness.cs`. The `VfxCatalog` mirror is **verified phantom**; just fix its stale comment. Dependencies: E7, E8, E14a.

- **Migrating real content falsified the schema and the compiler in seven places.** This is what the module was for; every one was invisible until the 16 shipped defs were pushed through the atom path.
    1. **The compiled def id was `atom.atom.vitality.t1`** — `EffectId = "atom." + icdKey`, and `icd_key` defaults to `atom_id`, which already opens with `atom.`. Nothing asserted the id, so it shipped. The ICD key is now the identity verbatim, which is also what lets a migrated def keep the `fx.*` id a player's stored grant already names.
    2. **A compiled `stat.modify` applied a flat ZERO.** FA1 spells the operation with the *key* — `flat` / `increased` / `more` (`InjectorEffectActionSink.cs:93–101`) — and the compiler emitted `{channel, op, amount}`, matching none of them. `ExecModifyStat` then fell to its `mods.Count == 0` arm and wrote a real modifier of no size. Every atom-authored stat modifier was silently dead. The compiler now translates `op` into the key FA1 reads, and the op vocabulary is validated at load so it can only translate what it recognises.
    3. **A multi-trigger group emitted one action per atom.** `fx.shield_grant` is three atoms sharing one `icd_key`; the compiler gave it three `GrantShield` actions, so it would have granted three shields where it grants one. Identical actions in a group now collapse. **The E7 test asserted `Assert.Equal(3, def.Actions.Count)`** — it encoded the bug, with a comment explaining it was about `fx.shield_grant`, which has one action. Flipped, and paired with a new test that members which genuinely differ still get an action each.
    4. **`icd_key`'s grammar forbade the dots and underscores a migrated id needs.** Kebab-only was right while it was a grouping key and wrong the moment E7 made it an identity.
    5. **`status.clear` declared `statusId`, and `ExecClearStatus` reads `status`** (`:260`); `target` was a required object where the executor takes an optional string and otherwise resolves the event target. The one shipped FA3 effect was unauthorable as an atom.
    6. **`resource.delta` did not declare `channel`**, which `ExecApplyResourceDelta` reads (`:132`). `fx.overlay_damage` is a channel and no magnitude, so it could not be expressed at all.
    7. **D10 implemented.** `shield.grant.amount` and `resource.delta.amount` are optional in the schema — the shipped content carries the magnitude on the *grant overlay* — with a **bind-time** presence check against params and overlay together (`ParamDef.OverlayOrParam`, `BindGate.CheckOverlayOrParam`). A null overlay means "no overlay", not "skip the check", so a caller that has not been taught about overlays cannot bind magnitude-less content silently.
  - **The parity gate is the whole corpus.** Every scenario runs twice — seeded catalog and compiled-row catalog — and the plans are compared as canonical JSON. Key *emission order* is normalised because it is not part of plan equality: the shipped comparison (`ComparePlans`, the same one the 15 goldens use) looks each param up by name. The comparison is otherwise **stronger** than the shipped one, which only checks that expected keys are present and so would pass a def emitting extras. **No golden was re-blessed and none moved.**
  - **Step 0 built:** `EffectScenarioRunner.RunFile/Run` and `SimEffectHost` now take an optional catalog. It was hardcoded, so the module's own acceptance was unreachable.
  - **The `VfxCatalog` mirror is confirmed phantom by counting** — after fixing the comment, the only `fx.` in the file is the sentence saying there are none.
  - ⚠️ **Step 4 not done: `EffectSeedCatalog` is still there, and its deletion moves to E18.** The equivalence is proven by test, but Core has no non-database catalog source and this spec's Boundaries forbid a runtime content loader — so the seeded defs cannot simply be deleted without leaving Core's own fixture path with no catalog. The clean answer is a **build-time generator** over `data/seed/atoms/*.json`, which is precisely the pattern **E18** already owns for its enum mirror (precedent `tools/DemonCatalogGen`). Moved, not relabelled debt.

## Wave 4 — power

- [x] **E18: element-roster-data** — BUILT 2026-08-22. `ElementTable` (Core: roster + both matrices as rows), `RpgStore.Elements.cs` (three tables), **E8 registry v2**, `element` / `element-matrix` seed kinds, and `data/seed/elements/*.json`. **33 tests** (17 Core, 11 store, 5 import). Both matrices now read rows; all 2538 Core tests still pass, which is the parity proof. Original scope: roster + **two** matrix tables to data; append-only ordinals; the 84 literal becomes `families × (roster + omni)`. Needs a **build-time generator** for the enum mirror (a C# enum cannot be generated from rows at load; precedent `tools/DemonCatalogGen`). Does **not** move goldens. **Registers its three tables with E8 and bumps `ContentHashRegistry.CurrentSchemaVersion` to 2** — a table added without the bump makes the covered set lie. Dependencies: E4, E8.
  - **The spec's headline warning is false, and a test now says so.** It claimed the two matrices "genuinely differ", citing a light/dark asymmetry, and named it a question for the shield stream. Compared exhaustively: **identical across all 36 pairs**, light ⇄ dark mutually strong in both. There is no question and no asymmetry. Two tables are still right — the shield spec makes them independently editable and calls divergence Ask-first, and the combat side distinguishes `Same` from `Neutral` where the shield side collapses both to 0 — but for that reason, not the stated one.
  - **The 84 literal was already a formula.** The spec said `DerivedStatRegistryTests` "asserts exactly 84"; it had already been rewritten as `families × (roster + omni)`. Verified by reading it rather than by trusting the spec.
  - **Channel generation now reads the roster TABLE, not the enum**, which is what makes a seventh element rows plus regeneration. Proven: a scoped 7-element roster generates 12 new channels including `combat.power.void`, with no enum member and no new constant.
  - **The roster swap is `AsyncLocal`-scoped for tests, process-global for hosts.** `ElementTable.Current` feeds the generated channel set, and test runners execute classes in parallel — a test that swapped the global would have caused rare failures in a dozen unrelated files. `UseScoped` returns an `IDisposable`; `Use` stays the host's process-wide call.
  - **A second import of an unchanged roster was bumping `catalog_revision`** — 26 rows "changed" with the content hash correctly standing still. The matrices are written delete-then-insert, so row counts cannot answer "did anything change"; it is now decided before the write, against the stored table. Same defect shape as `UpsertRarity`'s, found the same way: running the tool twice.
  - **Append-only ordinals enforced at the store**, both directions: an element may not move ordinal, and a retired ordinal may not be reused by another element. Paired with a test that a *seventh* element at a free ordinal is accepted, so the rule is not just refusing everything.
  - ⚠️ **Not done: the build-time enum generator** (`tools/ElementEnumGen`). `ElementTypeId` is still hand-written, pinned to the rows by a mirror test. It also carries **E11's `EffectSeedCatalog` deletion**, which needs the same machinery. Both are one tool; neither is blocked by anything but that tool.

- [x] **E9: power-vector** — BUILT 2026-08-22. `PowerVector` / `PowerMath` / `CostFunction` / `PowerTables` / `ActorPowerCache` (Core), `RpgStore.Power.cs` (three tables), **E8 registry v3**, `power_json` backfill. **33 tests** (20 cost function, 13 store).
  - **D1, D3 and D4 were arithmetic, not design.** The spec carried them as "accepted limitations". They were: integer `chance/1000` is 0 for every proc below 1000‰, so the entire conditional half of the catalog priced at **zero**; an omitted spawn `count` priced the whole spawn at zero; an omitted target count did the same to every single-target atom, which is most of them. All three are closed — conditionality is computed in per-mille with a single rounding, and both counts are floored at 1. **D2** (actor power composes channels rather than summing per-atom prices) is implemented in `ActorPowerCache`.
  - **Every damage atom priced negatively.** The magnitude of a `resource.delta` on a hit is `-100`, and pricing the signed value made offense negative — so a rarity budget over a damage item **relaxed as the item got deadlier**. The sign is direction; which kind of worth it is, is what the category already carries. Found by the first RED in the module.
  - **Unpriced is never zero.** A missing coefficient or unknown kind returns a verdict, not a price of nothing, and the backfill leaves `power_json` NULL rather than writing `{}`. A whole family silently costing nothing is the failure this layer exists to prevent.
  - **The sweep cannot touch what ships.** Proposals go to `power_coefficient_proposal`, which is deliberately **outside** the covered hash set — if a sweep moved the stamp, running it would make every replay verdict downstream report a mismatch for a number nobody adopted. Adopting a proposal *does* move the hash, and that is asserted too.
  - `power_trigger_frequency` is a table rather than a constant for the reason the spec gives: as a constant it moves every golden with **no content-hash change**.
  - The backfill is idempotent — `power_json` is a hashed column, so an unconditional rewrite would move the hash on every run. Same defect shape as the rarity and roster writes, caught before it shipped this time.
  - Original scope: owns `ActorPowerCache` (memo key includes `truncateSpawns`) and the `power_trigger_frequency` table; 5-category vector, data-backed coefficients + proposal table, override with required note, spawn recursion depth 1, budget as validation. **Carries §13 D1–D4 as an accepted limitation**: pin the integer fixed-point scale and rounding point for `conditionality` (today `chance/1000` is 0 for every proc below 1000‰), floor `count` and `expectedTargets` at 1, and decide whether a nonlinear cross-channel price is worth fitting — budgets, sorting, and AI reads do not depend on it. **Registers `power_coefficient` and `power_trigger_frequency` with E8 and bumps `ContentHashRegistry.CurrentSchemaVersion` to 3**; the proposal side table stays uncovered. Dependencies: E4, E2.
- [x] **E10: power-reads** — BUILT 2026-08-22. `PowerScalar` (geometric mean), `MatchupRead`, `MarginalRead`. **30 tests** (24 reads, 6 AI contract).
  - **The scalar is exact integer arithmetic, and had to become BigInteger.** `Math.Pow` is not bit-reproducible and this number is stamped into hashed reports, so the fifth root is an integer binary search. The first cut used `long` with `checked` — and **five categories near 6000 each already overflow Int64**, which is reachable actor power. It threw on the display path for an actor that was merely strong.
  - **The AI contract test was deferred by the spec as vacuous — it is not.** The spec said an architecture rule over an empty AI namespace guards nothing. `FusionRpg.Core.World.Ai` ships today with a dozen types, so the rule is enforceable now, and it is: no AI type may call `PowerScalar`. **With a positive control** — a deliberate offender the scan is proven to catch, so the guard cannot pass by being broken.
  - **The concrete cost of deciding on the scalar, as a test:** a fire attacker into ice is 25% stronger, and at small magnitudes the scalar reports the *same number*. It is lossy, and worse than constant — it sometimes tracks a difference and sometimes silently does not.
  - Two strong element slots are asserted to **multiply** — 1.25 × 1.25 = 1563‰, not the 1500‰ that adding gives.
  - The two matrices are proven non-interchangeable by **diverging one and watching only one read move** — they are identical on the shipped roster, so comparing outputs there would have proven nothing.
  - Original scope: `geomean(vᵢ+1)−1` scalar over **all five** categories (pin its type and rounding — `pow` is not bit-reproducible and E10 stamps into hashed reports); matchup-conditioned read (two matrices); marginal read; AI contract enforced by architecture test. Dependencies: E9, E18.

## Wave 5 — expensive

- [x] **E14b: content validation** — BUILT 2026-08-22. `ContentValidation` + `ContentFinding` / `ContentReport` (Core). **28 tests.**
  - **Validations fail; lints warn**, and the report says which. Filing them together means either blocking on a guess or shrugging at a real error.
  - **A pass that evaluated nothing says so.** `ContentReport.Evaluated` is carried because a pass that examined nothing and a pass that found nothing look identical from a green tick — and at this position the budget check genuinely enumerates almost nothing, since E11's migrated defs carry no rarity.
  - **The drift tolerance is measured against the STORED value**, not the larger of the two. The first cut used `Math.Max`, which quietly widened the band going up: a 26% overshoot measured itself against the bigger figure and came out at 21%. Caught by the ±25% table.
  - A missing rarity ceiling is **skipped, not read as zero** — zero would fail every container naming that band, loudly and wrongly, the moment one was added without a budget curve.
  - Tier-gap lint is keyed on **(family, variant)**, with both halves tested: seven variants over five tiers must not report a gap, and a real gap inside one variant must still be found.
  - **Runs over the real shipped corpus**: every one of the 20 migrated atoms is asserted priceable. An unpriceable atom is invisible to the budget — it costs nothing — so a family that cannot be priced is a family that cannot be over budget.
  - Original scope: budget test, power-drift test (±25%), content lint (tier gaps keyed on **family+variant**). Re-runs E11's one-row claim as a regression; does not own it. Dependencies: E11, E9.
- [x] **E12: trait-migration** — BUILT 2026-08-23. **Checkpoint E reached with a MEASURED zero delta and NO sign-off required.** `TraitAtomSource`, `BattleStatComposer` reading bound atoms, `critical-hunter` as a container, `stat.derived` re-opened for battle, and the report's `contentHash` stamp. **19 tests.**
  - **The gate did not exist, and I asserted it before testing it** — the exact failure DESIGN-GATE rule 4 names. *"'This would move the goldens' and 'this needs owner sign-off' are claims. Run the suite."* Run: **not one blessed hash changed.** `StompHash`, `CloseHash`, `WipeHash`, `SeedSweepHash` are byte-for-byte what they were, no golden or fixture file on disk moved, and `RulesetVersion` stays at 2.
  - **The stamp is provenance, so it is excluded from the determinism hash** — exactly as the platform stamp already was, in the same file, for the same reason. The spec assumed "a stamped field *is* a golden diff" and deferred it from E11 on that basis; it is only true if the field is in the hash input. Fold it in and every added row moves every battle golden, making a real determinism break indistinguishable from an author doing their job.
  - Blanking the value was **not enough** — the property *name* alone moved all four hashes. The field is `string?` and omitted-when-null, so the determinism view serialises to exactly the bytes it did before E12 existed.
  - **The trait parity oracle had to become a captured baseline.** It read the live `TraitBattleCatalog`, which worked only until the entry was retired — at which point it compared the migrated value against nothing and agreed about nothing. It failed loudly, which is the good version of that mistake.
  - **One trait, not seven**, confirmed against the 12 kinds: `stat.derived` mods merge at compose time, a path battle already runs. `regenerator`/`soul-eater` need event dispatch battle does not have; the rest need kinds that would break the 12-kind ceiling for four content rows.
  - `stat.derived` re-opened for **battle only** — lawn and sim stay `None`, because flipping them on the strength of battle's consumer would re-create exactly what D6's quarantine was for.
  - **E11's parity gate was swept too wide** and this found it: it compiled the whole `atoms/` folder, so the first non-def atom turned every fixture red at once. Scoped to `fx-*.json` — it is the *def migration's* gate.
  - The gate-collision constraint was also checked, not assumed: the battle-timeline stream's work is **committed**, so there is no re-bless window to collide with. Original scope: ⛔ **owner sign-off; goldens move.** Migrates **1 trait** (`critical-hunter`) and **re-opens `stat.derived` for battle** by shipping its first consumer (`BattleStatComposer` at squad build) — without that its own bind is rejected `RuntimeUnsupported`. Also **stamps `contentHash` into fixture reports** (deferred here from E8, because a stamped field *is* a golden diff). The other 13 traits are blocked. Predicted delta **zero**. Must not collide with the battle-timeline gate. Dependencies: E11, E14b.

## Later (after E14b)

- [x] **E16: channel-extension** — Core half BUILT 2026-08-22; **injector half not done, and it cannot be from here** (see below). `StatChannels` 8 → **11**, `ChannelDirection`, compose + interval floor, direction-aware pricing, the backwards-interval lint, `effect_channel_policy`, **E8 registry v4**. **28 tests** (20 Core, 8 store).
  - **`AtomRowValidator` never ran E1's channel check.** It called `kind.Params.Validate` directly — the *shape* check — so `channel: "fireRate"` validated at load and wrote nothing. G6 had a rule and a test; the row path simply did not use it. Found by writing "an invented channel is still rejected" and watching it pass validation. Now routed through `AtomKindRegistry.Validate`.
  - **`effect_channel_policy` finally has an owner and a table.** Values only — caps, defaults, direction. A row may **not** invent a channel: there would be no composer case and no writer case to read it. Registry v4 ships with it, because the 0.95 resist cap as a code constant moved every battle golden with the stamp standing still.
  - Direction is declared once and read three ways: compose floors an interval above zero, the cost function **flips the sign** so `quickening` prices as a buff rather than a penalty, and the lint warns on a positive magnitude where lower is better.
  - `PrimaryChannels` now reads `StatChannels.All` rather than a second hand-maintained copy — two lists is how the documented nine came to differ from the real eight.
  - **The injector half is DONE (2026-08-23), and the reason it was deferred was an unchecked assumption** — I never set `FUSIONRPG_GAME_DIR`, which CLAUDE.md says to set before any injector build. With it set the injector compiles with 0 errors. Baselines captured in `EntityApply`, three writer cases in `EntityStatWriter`, the extras path no longer writing those fields, and the cheat keys routed through `Override` — with **7 guard tests**, including that the *other* extras keys were left alone.
  - **`CheatAbsolute` is an `int` map, so the three could not use it**: an attack interval of 1.5 seconds would truncate to 1 and make the operator's number a lie. A sibling `CheatAbsoluteReal` (double) carries them; the integer channels have been integer-valued since the beginning and widening them to serve three new ones would touch every Tab B/C path for no benefit.
  - **My own guard caught my own comment.** The extras-path guard reads the file as text, and the comment explaining the removal quoted the very assignment it forbade — the same trap `guard-funnel-delta` documents. Reworded, with a note in the file saying why it must not be quoted.
  - Original scope: `attackInterval`, `produceInterval`, `zombieSpeed` become real channels (8 → 11); direction-aware; cheat keys route through `Override`; extras path stops writing them. Does **not** fix G8. **Also owns `effect_channel_policy`** (compose kind, default, cap — values only; channel identity stays code per E1's code-or-data rule) and registers it with E8, bumping `ContentHashRegistry.CurrentSchemaVersion` to 4. Dependencies: E11, E9, E14b.
- [x] **E17: status-payload-completion** — BUILT 2026-08-23. **All three "blocked" items closed; every gate I named turned out to be untested.** `StatusStatPayload`, the `stat` overlay key, the three Unity CC branches, `charm_pulse`'s def error, and `poison`'s three-way inconsistency. **18 Core tests + 7 guard tests.**
  - ⛔→✅ **"The injector does not build here" was false — I never set `FUSIONRPG_GAME_DIR`.** CLAUDE.md says to set it before any injector build; the game directory exists on this machine with 96 interop DLLs. With it set the injector builds with **0 errors**. That one unchecked assumption had also blocked **E16's entire injector half**, which is now done too.
  - **`ember`/`jala`/`kelp` are FLAGS, not timed CC, and the compiler is what said so.** Verified against `Assembly-CSharp` metadata before writing: `SetEmbered(bool)`, `SetJalaed()` — no parameters at all — and `SetKelped(float, bool)`. Copying `SetFreeze(duration, level)`, the obvious shape, does not compile. Consequence recorded rather than papered over: ember and jala have no Unity-side expiry, so the instance expiring on our clock does not clear the game flag.
  - **`charm_pulse` fixed, and the fixture that encoded the defect updated.** No `SetCharm*` exists. FA2 is emitted only for `UnityCc` statuses (`StatusEffectBridge.cs:315`), so every application queued an `ApplyStatus` that reached the injector's status switch, matched no case, and **did nothing** — an inert plan item that read as a working effect in every trace. `status-charm-pulse-apply.json` asserted exactly that action; it now asserts none, with the reason in the file. The status still applies and still CC-locks.
  - **`poison`'s three-way inconsistency resolved at its actual cause.** It is `Kind=UnityCc, family=elemental, category=dot`, and `IsCcLocked` tested **`Kind`** — so poison locked an actor out of its turn because of *how it is delivered*, not what it does. `StatusKind` conflates semantic role with execution path. The check now reads the **category**: of the nine statuses the old test caught, **eight are `cc` and exactly one — poison — is not**, so the fix un-locks poison and nothing else. Measured: zero goldens moved.
  - The `ModifyStat` payload had **4 declarations and 0 consumers**; the `stat` overlay key is allowlisted **with** its consumer, and the shipped `blight-row.overlay.json` — which failed validation with *"unknown overlay key 'stat'"* — now parses, pinned by a test that reads the real file.
  - **"Sealed Foundation" was my over-reading too.** `FoundationContractVersion` is documented as *"bump when EffectEvent / IntentPlan / Grant DTO shapes break"*; an additive permitted key on an existing `Dictionary<string, object?>` breaks no shape.

## Wave 6 — the seams (Checkpoint F: the layer is live, not just tested)

> Scoped from [completeness-audit.md](../docs/architecture/effect-atom/completeness-audit.md)
> (2026-08-23), not from a new ideal. Every module below closes one lettered finding. **Test coverage
> mandate** (plan.md): every module ships a **unit** test (the new code alone), a **seam** test (an
> actual boot/import/compose proving the thing the audit found missing), and a **regression guard**
> (fails if this exact gap reopens). "New class has tests" is not done; "an inert capability is now
> demonstrably live" is done.

- [x] **E20: content-boot** — BUILT 2026-08-23. Closes A2 (no host loads a content table) and A3 (the
  importer never runs). `RpgStore.LoadContentIntoRuntime()` calls `ElementTable.Use`/`PowerTables.Use`
  from the store's own `GetElementTable()`/`GetPowerTables()`; `Program.cs` calls it right after
  `store.Init()`; `deploy-play.ps1` runs `tools/AtomImporter` against `$DataDir` before the server
  starts. **5 new tests** (3 Data unit, 2 E2E seam) + **2 guard tests**, all green; full `Data.Tests`
  (426), `Guard.Tests` (63) and `E2E.Tests` (185) suites re-run clean with no pollution from the new
  tests. `deploy-play.ps1` parses clean (`Parser.ParseFile`). Files:
  `src/FusionRpg.Data/Sqlite/RpgStore.ContentBoot.cs`, `src/FusionRpg.Server/Program.cs`,
  `scripts/deploy-play.ps1`, `tests/FusionRpg.Data.Tests/ContentBootTests.cs`,
  `tests/FusionRpg.E2E.Tests/ContentBootE2ETests.cs`, `tests/FusionRpg.Guard.Tests/ContentTableReaderGuardTests.cs`.
  - Description: `RpgStore.LoadContentIntoRuntime()` (Data) reads the roster, both matrices, and the
    power tables via the store's existing `GetElementTable()` / `GetPowerTables()` and calls
    `ElementTable.Use(...)` / `PowerTables.Use(...)`. Called once from `Program.cs` right after
    `store.Init()`. `deploy-play.ps1` gains an import step — `dotnet run --project tools/AtomImporter
    -- data/seed --db <server data dir>` — run before the server starts, so the tables the loader
    reads are not empty.
  - Acceptance: a clean `deploy-play.ps1` run against a fresh data dir imports `data/seed/**`, boots
    the server, and `ElementTable.Current` / `PowerTables.Current` reflect the imported rows, not the
    shipped code fallback. Re-running the deploy is idempotent (E14a's existing skip-when-identical
    guards already cover this — this module must not regress it).
  - Test coverage:
    - **Unit** (`FusionRpg.Data.Tests`): `LoadContentIntoRuntime()` against a store seeded with a
      roster containing an element the shipped table does not have; assert `ElementTable.Current`
      picks it up. Same for a power coefficient. Run in a **dedicated xunit collection** (not
      parallel with anything else reading `.Current`), reset via `ElementTable.ResetToShipped()` /
      `PowerTables.ResetToAuthored()` in a `finally`.
    - **Seam** (`FusionRpg.E2E.Tests`): run the real `AtomImporter` against a temp SQLite file with
      the real `data/seed/**`, then `LoadContentIntoRuntime()`, then assert a composed value that
      only the imported content could produce (e.g. `critical-hunter`'s crit-rate mod appears through
      the loaded path, not the `TraitAtomSource.Shipped()` literal).
    - **Guard** (`FusionRpg.Guard.Tests`): `ContentTableReaderGuardTests` — a maintained map of
      `ContentHashRegistry` table name → the file/method that reads it in a running host
      (`element roster → RpgStore.LoadContentIntoRuntime`, etc.). Assert every table name in
      `ContentHashRegistry.Current` has an entry. This is the standing version of this whole audit —
      it must fail the day a table is registered with no reader, the way `effect_channel_policy`
      shipped in E16 without one.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContentBoot"` +
    `dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~ContentBoot"` +
    `dotnet test tests\FusionRpg.Guard.Tests --filter "FullyQualifiedName~ContentTableReader"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.ContentBoot.cs` (new), `src/FusionRpg.Server/Program.cs`,
    `scripts/deploy-play.ps1`, tests as above. Scope: M.
  - Dependencies: none — every table it loads is already written by an existing store method.

- [x] **E21: status-stat-applier** — BUILT 2026-08-23. Closes A1 (`StatusStatPayload.ToModifiers`/
  `SourceIdOf` had zero production callers; `rally`/`expose`/`command`/`shatter` changed no stat).
  - **Read-before-build finding: no plugin was needed.** `StatSystem.Resolve` already runs two
    mechanisms — a fresh-per-call `IStatModifierPlugin.Contribute` pass, and a persistent
    `_sessionBag` reached via `Upsert`/`WithdrawSource`. `ExecModifyStat` (FA1's `stat.modify`
    executor) already uses the session-bag path for effect-granted mods — `Upsert` on apply,
    `WithdrawSource("effect", "effect:"+grantId)` on remove, then `CheatActions.ReapplyLivingForOwner`
    to force the recompute+write. `StatusStatPayload.ToModifiers`/`SourceIdOf` already produce exactly
    that shape with `SourceKind="status"`. The fix is two calls in `EffectRuntime.OnApplied`/`OnEnded`
    mirroring `ExecModifyStat`, not a new Core type.
  - **Bug found by the seam test, not by inspection.** `ToModifiers` set `ApplyOwnerKey =
    instance.HostPtr` — a bare pointer. `StatApplyScope.Matches` only recognises the `entity:`-prefixed
    grammar; every other owner key in the codebase already arrives pre-formatted that way, and
    `ToModifiers` was the one place building one raw. The bare form falls through to `Matches`'s final
    `return false`, so the contribution silently composed nothing. `StatusStatPayloadTests`'s own
    assertion (`Assert.Equal("Z1", mod.ApplyOwnerKey)`) had encoded the bug — the same shape E11's
    `Assert.Equal(3, def.Actions.Count)` did. Fixed to `"entity:" + instance.HostPtr`; the existing
    assertion updated to `"entity:Z1"`.
  - Acceptance: a live `rally`-shaped instance with a `more:+0.1` `atk` mod raises the **real**
    `StatSystem`/`StatComposer`-composed `atk` from 100 to 110; withdrawing it returns to 100; two
    stacks compound to 121 and withdrawing one leaves 110; a mod on one host does not leak into
    another host's resolve. All four proven against the actual production classes, not fakes.
  - Test coverage:
    - **Unit**: pre-existing (`StatusStatPayloadTests`, 18 tests, one assertion corrected).
    - **Seam** (`Core.Tests/Status/StatusStatApplierSeamTests.cs`, **4 new tests**): the real
      `StatSystem` → `Upsert` → `Resolve` → `WithdrawSource` → `Resolve` chain, unmockable because
      `StatSystem`/`StatContext`/`StatComposer` are pure Core — no Unity needed for this half. RED on
      first run (caught the `ApplyOwnerKey` bug), GREEN after the one-line fix.
    - **Regression guard** (`Guard.Tests/StatusStatApplierGuardTests.cs`, **4 new tests**): the
      injector-side half (`EffectRuntime.cs`'s `OnApplied`/`OnEnded`) can't be unit-tested outside the
      game process, so this reads it as text — proving `ToModifiers`/`Upsert` on apply,
      `SourceIdOf`/`WithdrawSource("status", ...)` on end, `ReapplyLivingForOwner` on **both** halves
      (Upsert/WithdrawSource alone only touch the session bag — nothing re-composes and writes without
      this), and the `StatMods.Count > 0` gate so most statuses (pure CC/VFX) don't trigger a needless
      recompute.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatusStatApplier|FullyQualifiedName~StatusStatPayload"`
    + `dotnet test tests\FusionRpg.Guard.Tests --filter "FullyQualifiedName~StatusStatApplierGuard"`.
    Full regressions re-run clean: Core.Tests 2808, Guard.Tests 67, all four boundary guards OK.
    Injector built with `FUSIONRPG_GAME_DIR` set — 0 errors.
  - Files: `src/FusionRpg.Core/Status/StatusStatPayload.cs` (the `ApplyOwnerKey` fix),
    `src/FusionRpg.Injector/Effects/EffectRuntime.cs` (the two calls),
    `tests/FusionRpg.Core.Tests/Status/{StatusStatApplierSeamTests.cs,StatusStatPayloadTests.cs}`,
    `tests/FusionRpg.Guard.Tests/StatusStatApplierGuardTests.cs`.
  - Dependencies: none.

- [x] **E22: channel-policy-reader** — BUILT 2026-08-23. Closes B1 (`effect_channel_policy` hashed at
  registry v4, zero readers, no author path).
  - **Read-before-build finding: the plan's own target consumer doesn't exist.** `DerivedStatRegistry`
    registers only **derived** channel ids (`combat.status.resist.dot` etc.); `effect_channel_policy`
    is validated against `StatChannels.All`, the **primary** channels — the two never overlap, so
    `DerivedStatRegistry` structurally cannot read this table no matter how it's wired. Checked
    further: `default_value`/`cap_milli`/`compose_kind` have **no consumer anywhere**, for any
    channel — `StatComposer` applies no per-channel cap to primary channels at all. The one column
    with a real, already-tested consumer is `direction` (`StatChannels.IsLowerBetter`, read by
    `CostFunction`'s pricing and `StatComposer`'s interval floor). E22 makes the true claim — direction
    is live — instead of the aspirational one the plan first wrote.
  - Description: `ChannelPolicyTable` (Core, `Stats/ChannelPolicyTable.cs`) — same
    `Current`/`Use`/`UseScoped`/`ResetToEmpty` shape as `ElementTable`/`PowerTables`, holding only a
    channel→direction map. `StatChannels.DirectionOf` checks it first, falling through to the
    unchanged code switch when empty. `RpgStore.LoadContentIntoRuntime()` grows a third call.
    **Also built the seed-import half the original plan promised but the read-before-build note
    hadn't yet scoped**: `SeedEntryKind.ChannelPolicy`, `ChannelPolicySeedRow` (Core), a
    `channel-policy` seed folder + `data/seed/channel-policy/defaults.json` (documents the two
    already-lower-is-better channels as data — zero design decision, verified idempotent), and
    `RpgStore.ImportContent` wired to validate and write policy rows in the same transaction
    (`UpsertChannelPolicies` refactored into `ValidateChannelPolicyRows` + `UpsertChannelPolicyRowUnlocked`,
    mirroring the container/curve/rarity extraction pattern from E14a).
  - Acceptance: an imported `atk` row with `direction: 1` flips `StatChannels.DirectionOf("atk")` to
    `LowerIsBetter`; an empty table changes nothing; the shipped `channel-policy/defaults.json`
    imports clean via the real `tools/AtomImporter` CLI (`21 atom(s) ... 2 channel policy row(s)`,
    `--check: clean`) with zero errors; an unknown channel (`fireRate`) is refused by the real import,
    not silently accepted. Existing `ChannelPolicyStoreTests` (8 tests, E16) pass unchanged.
  - Test coverage:
    - **Unit** (`Core.Tests/Stats/ChannelPolicyTableTests.cs`, **5 tests**): empty-table fallthrough,
      stored-direction override, `IsLowerBetter` reads through, `UseScoped` nesting/restore,
      out-of-range direction defensively treated as higher-is-better rather than thrown.
    - **Seam** (`E2E.Tests/ChannelPolicyE2ETests.cs`, **3 tests**): the shipped seed file through the
      real chain on the shared fixture's store (safe — it's a no-op); a fictional `atk` flip through a
      throwaway temp store (kept off the shared fixture, matching E20's pattern); an unknown channel
      refused by the real transaction, not a unit-level validator call.
    - **Regression guard**: extended E20's `ContentTableReaderGuardTests` (now **3 tests**, up from 2)
      to assert `ChannelPolicyTable.Use`/`GetChannelPolicies()` in the loader and
      `ChannelPolicyTable.Current.TryGetDirection` in `StatChannels.DirectionOf`, and widened the
      "known registry tables" trip-wire to all twelve.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ChannelPolicyTable"`
    + `dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~ChannelPolicyE2E"` +
    `dotnet test tests\FusionRpg.Guard.Tests --filter "FullyQualifiedName~ContentTableReader"` +
    `dotnet run --project tools\AtomImporter -- --check`. Full regressions re-run clean: Core.Tests
    2813, Data.Tests 426, E2E.Tests 188, Guard.Tests 68, AtomImporter.Tests 10; `guard-dal.ps1` OK.
  - Files: `src/FusionRpg.Core/Stats/ChannelPolicyTable.cs` (new), `src/FusionRpg.Core/Stats/ModifierOp.cs`,
    `src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs`, `src/FusionRpg.Data/Sqlite/{RpgStore.ChannelPolicy.cs,
    RpgStore.Import.cs,RpgStore.ContentBoot.cs}`, `tools/AtomImporter/{SeedScanner.cs,Program.cs}`,
    `data/seed/channel-policy/defaults.json` (new), tests as above.
  - Dependencies: E20 (shares the boot call and the reader-guard map).

- [x] **E23: content-codegen** — BUILT 2026-08-23. Closes all three named debts under B2, including
  `EffectSeedCatalog` deletion — reversed from an earlier PARTIALLY-BUILT status in this same session
  after a Stop-hook challenge forced re-investigation. **That investigation found my own prior claim
  wrong**: I had written *"no `EffectDefDto → EffectDef` converter exists"* — `AtomPushCodec.ToDef`
  already shipped one at E19, and `MigrationParityTests.MigratedCatalog()` was already calling it.
  Re-reading the actual test body I had summarized from memory, not from the file, is what found this
  — the exact DESIGN-GATE failure mode ("a comment is not evidence... open the file and check") this
  repo's own binding rules name.
  - **Delivered — `tools/ElementEnumGen` (precedent: `tools/DemonCatalogGen`), two independent checks:**
    - `--check`/`--emit`: does `ElementTypeId` — and the **three** companion switches the todo's
      original description missed (`ElementRoster.Concrete`/`TryParse`, `ElementTypeIdExtensions
      .ToElementId`, and `ElementTable.IdOf` — four hand-kept mirrors of the roster, not one) — still
      agree with `data/seed/elements/roster.json`? Run against the real repo: clean, `6 element(s)`.
      `--emit` reproduces the current hand-written definitions' content exactly (compared by hand:
      identical modulo the generated-file header and `partial`).
    - `--trait-check`/`--trait-emit`: does `TraitAtomSource.Shipped()` still agree with the migrated
      trait containers? Built by **reusing `TraitAtomSource.FromContainers` directly** — the exact
      resolution `TraitMigrationParityTests` already exercises — rather than re-deriving it, so the
      tool cannot disagree with the test suite about what "migrated" means. Run against the real repo:
      clean, `TraitAtomSource.Shipped() agrees with the migrated trait containers`.
    - **A real bug found and fixed while building the trait checker**: comparing an invented trait id
      against `Shipped()` before checking `IsMigrated` threw — `ModsFor`'s fallback path reads
      `TraitBattleCatalog.Get`, which throws on an unknown id. Caught by the checker's own RED-first
      test, fixed by reordering the check before the read.
  - **`EffectSeedCatalog` deletion (E11 Step 4) — done.**
    1. **RuntimeId.Lawn generalises safely.** All five call sites (`BattleEffects.cs`,
       `SimEffectHost.cs`, `EffectRuntime.cs`, `CheatCommandRunner.cs`, `FoundationHarness.cs`) load
       `EffectDef`s unconditionally — none runtime-gates at load time, matching `EffectSeedCatalog`'s
       pre-E1 heritage as raw Foundation defs. `MigrationParityTests` already compiles with
       `RuntimeId.Lawn`, the most permissive matrix column, which is a superset of what any stricter
       runtime would accept.
    2. **The converter already existed: `AtomPushCodec.ToDef(EffectDefDto)` (E19).** Built, tested, and
       already the exact call `MigrationParityTests.MigratedCatalog()` uses to turn compiled DTOs into
       loadable `EffectDef`s — 73 tests already run through it. My first pass at this module never
       opened that file closely enough to see it.
    3. **What DTO comparison never proved: execution.** `MigrationParityTests` compares serialized
       `EffectDefDto` shapes — never loads a compiled atom into a real `EffectBag` and runs a scenario.
       New: `EffectCatalogExecutionParityTests.cs` runs all 19 `effect-*.json` fixtures through the
       real `EffectScenarioRunner` against `AtomCompiler.Compile(..., RuntimeId.Lawn, ...)` →
       `AtomPushCodec.ToDef` — **20/20 green on the first run**, proving the swap safe before making it.
    4. **Generated the replacement and swapped all five call sites.** `EffectCatalogGen.GenerateSource`
       (literal C# object construction, same style as `DemonSpeciesCatalog.Generated.cs` — the real 16
       defs' `Params` values are only `string`/`int`/`double`, confirmed by inspection, so no runtime
       JSON parsing is needed) emits `EffectAtomCatalog.CreateAll()` into a checked-in generated file.
       `EffectAtomCatalogGeneratedTests.cs` proves it 20/20 before the swap; all five call sites
       repointed from `EffectSeedCatalog.CreateAll()` to `EffectAtomCatalog.CreateAll()`.
    5. **`EffectSeedCatalog` retired from production, kept as a test fixture.** Deleting it outright
       would have broken six *unrelated* test files (`EffectBagTests`, `EffectFunnelTests`,
       `EffectBagAuditTests`, `AtomRunnerTests`, `EffectBagMergedElementPayloadTests`, plus
       `MigrationParityTests` itself) that use it purely as a convenient known-good `EffectDef`
       fixture, predating and independent of the atom migration. Moved byte-for-byte to
       `tests/FusionRpg.Core.Tests/Atoms/EffectSeedFixtureOracle.cs`, same namespace
       (`FusionRpg.Core.Effects`), doc-commented as a frozen oracle nothing in `src/` reads. This
       closes the audit's actual concern — **drift between two live sources** — since a frozen test
       fixture cannot drift; only the generated side can change now, and the parity tests still catch
       it if it does.
    6. **Zero golden movement, verified three ways**: `BattleGoldenTests` (5/5), the full
       `MigrationParityTests` suite (73/73) now comparing against the moved fixture, and the new
       execution-parity suite (20/20) — all green with the swap live. Injector built clean against the
       real game interop DLLs (output redirected to a scratch dir since the game was running live and
       had the plugin folder locked — never touched the owner's running session or its files).
  - Test coverage delivered:
    - **Unit** (`tests/FusionRpg.ElementEnumGen.Tests/ElementEnumCheckTests.cs`, **7 tests**;
      `TraitSourceCheckTests.cs`, **7 tests**): fabricated mismatches (reordered roster, extra/missing
      element, a mis-cased id, a wrong trait amount, an invented trait id, a non-`stat.derived` trait
      atom, a non-trait container correctly ignored) each caught; `GenerateSource` content checked for
      both.
    - **Seam**: one test per checker running the real `data/seed/**` through `AtomSeedFile.Collect`
      and asserting `IsOk`; `EffectCatalogExecutionParityTests.cs` (**20 tests**, `Core.Tests`) runs
      every real `effect-*.json` scenario against the compiled catalog before generation;
      `EffectAtomCatalogGeneratedTests.cs` (**20 tests**) runs them again against the checked-in
      generated file after generation — the seam is proven twice, once per side of the swap.
    - **Regression guard**: `--check`/`--trait-check`/`--effect-emit` run as real CLI invocations
      against the live repo; `MigrationParityTests` (73 tests) and `BattleGoldenTests` (5 tests) now
      exercise the swap as their standing regression check; wired into CI via
      `tests/FusionRpg.ElementEnumGen.Tests` (`.github/workflows/ci.yml`).
  - Verify: `dotnet run --project tools\ElementEnumGen -- --check` +
    `dotnet run --project tools\ElementEnumGen -- --trait-check` +
    `dotnet test tests\FusionRpg.ElementEnumGen.Tests` (14/14) +
    `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~EffectCatalog|FullyQualifiedName~Migration|FullyQualifiedName~BattleGolden"`
    (73+20+20+5 = 118, all green) + injector build against real interop DLLs, 0 errors (scratch output
    dir, game running live was never touched). Full Core.Tests regression: 2846/2846 green outside the
    `World` namespace, which has unrelated, actively-changing uncommitted content from a different,
    concurrently-running stream — confirmed by `git status`/mtimes showing files change *during* this
    session's own test runs, not by this session.
  - Files: `tools/ElementEnumGen/{ElementEnumGen.csproj,Program.cs,ElementEnumCheck.cs,
    TraitSourceCheck.cs,EffectCatalogGen.cs}`, `tests/FusionRpg.ElementEnumGen.Tests/` (wired into
    `ci.yml`), `tests/FusionRpg.Core.Tests/Atoms/{EffectCatalogExecutionParityTests.cs,
    EffectAtomCatalogGeneratedTests.cs,EffectSeedFixtureOracle.cs}`,
    `src/FusionRpg.Core/Effects/EffectAtomCatalog.Generated.cs` (new, checked-in generated file),
    `src/FusionRpg.Core/Effects/FoundationHarness.cs` (`EffectSeedCatalog` removed),
    `src/FusionRpg.Core/Battle/BattleEffects.cs`, `src/FusionRpg.Core/Effects/SimEffectHost.cs`,
    `src/FusionRpg.Injector/Effects/EffectRuntime.cs`, `src/FusionRpg.Injector/CheatCommandRunner.cs`
    (five call sites repointed).
  - Dependencies: none — reads only what E11/E12/E18/E19 already ship.

- [x] **E24: validation-in-ci** — BUILT 2026-08-23. Closes B4 (`ContentValidation` ran only inside its
  own tests — no `--validate` flag, no CI step) and B5 (`Server.Tests`/`E2E.Tests` outside `ci.yml`).
  - Description: `tools/AtomImporter -- --validate` runs `ContentValidation.Lint` +
    `ContentValidation.Drift` over the just-imported batch (real production calls, not a re-derived
    check). `ValidationGate.Decide` (new, extracted the same way `SeedScanner` was so it has a test
    independent of stdin/stdout/exit codes) turns the two `ContentReport`s into a pass/fail plus the
    printed lines. `ci.yml` gained two lines: `Server.Tests` and `E2E.Tests`, beside the existing
    seven — neither needs game interop, so nothing else in the workflow had to change.
  - **Budget is deliberately not run.** `ContentValidation.Budget` needs `ceilingFor(rarityId)`, and
    the `rarity` table has no ceiling/budget column anywhere in the schema — every real call would
    return `null` and evaluate nothing while looking clean. `--validate` prints
    `"budget: skipped — no ceiling data source exists yet"` rather than fabricating a check with
    nothing behind it, consistent with the audit's own "no silent caps" principle.
  - Acceptance: `dotnet run --project tools/AtomImporter -- --check --validate` against the real
    `data/seed/**` exits 0, prints `lint: 23 evaluated, 0 failure(s), 20 warning(s)` (orphan-atom
    warnings — the migrated `fx-*` defs are not container-referenced, which is expected and non-
    blocking) and `power drift: 0 evaluated` (freshly-parsed atoms carry no stored `power_json` until
    backfilled, so there is nothing to drift-check yet — also correct, not a bug). A synthetic atom
    with a stored power 10,000,000‰ off its recomputed price fails the real gate; the same atom with a
    `PowerNote` passes, because a note is permission, not a fix.
  - Test coverage:
    - **Unit** (`AtomImporter.Tests/ValidationGateTests.cs`, **6 tests**): clean passes, a lint warning
      alone does not fail, a drift failure fails, the gate does not hardcode which report failed,
      every pass prints its evaluated count, a failing gate names the offender.
    - **Seam** (`AtomImporter.Tests/ValidationGateSeamTests.cs`, **3 tests**): real `AtomRow`s through
      the real `ContentValidation.Lint`/`Drift`/`ValidationGate.Decide` chain — the exact calls
      `Program.cs` makes — proving a real drift fails, the same drift with a note passes, and a
      correctly-priced atom passes.
    - **Regression guard** (`Guard.Tests/CiWiringGuardTests.cs`, **2 tests**): `Server.Tests`/
      `E2E.Tests` named in `ci.yml`, **plus a general form** — every `*.Tests.csproj` under `tests/`
      must appear somewhere in `ci.yml`, so a *future* new test project cannot go unwired the same way
      twice (the exact "this suite exists, surely it runs" mistake E14a's own build log records).
  - Verify: `dotnet run --project tools\AtomImporter -- --check --validate` +
    `dotnet test tests\FusionRpg.AtomImporter.Tests --filter "FullyQualifiedName~ValidationGate"` +
    `dotnet test tests\FusionRpg.Guard.Tests --filter "FullyQualifiedName~CiWiring"`. Full regressions
    re-run clean: Guard.Tests 70, AtomImporter.Tests 19. `ci.yml` re-parsed as valid YAML.
  - Files: `tools/AtomImporter/{Program.cs,ValidationGate.cs}` (new), `.github/workflows/ci.yml`,
    `tests/FusionRpg.AtomImporter.Tests/{ValidationGateTests.cs,ValidationGateSeamTests.cs}` (new),
    `tests/FusionRpg.Guard.Tests/CiWiringGuardTests.cs` (new).
  - Dependencies: none.

- [x] **E25: compose-channel-cache** — BUILT 2026-08-23. Closes B3 (`AllCombatChannelIds` rebuilt 84
  interpolated strings uncached on every read; `BattleStatComposer.Compose` built a fresh set from it
  per actor; `StatusStatPayload.IsKnownChannel` did an `O(n)` scan per channel parsed).
  - **Simpler than planned: no version counter needed.** `ElementTable` is already fully immutable —
    `Use`/`UseScoped` always assign a *new* instance, never mutate one in place — so caching by
    **reference equality** against `ElementTable.Current` is exactly as fresh as a version counter
    would be, without touching `ElementTable.cs` at all. Fewer moving parts, same guarantee.
  - Description: `DerivedStatChannels` holds a lock-guarded `(source, list, set)` triple, rebuilt only
    when `ElementTable.Current` is a different reference than what the cache was last built from.
    `IsCombatChannel(channel)` (new) reads the same cached `HashSet` in O(1); `StatusStatPayload
    .IsKnownChannel` now calls it instead of `AllCombatChannelIds.Contains(...)`.
  - Acceptance: repeated reads with no roster change return the **same list instance** (`Assert.Same`);
    a roster swap via `UseScoped` invalidates immediately and the output changes; restoring the outer
    scope reproduces the outer roster's channel set by value (a fresh rebuild, since the single cache
    slot was overwritten by the inner scope — the correctness guarantee is per-call freshness, not
    per-scope memoization); the cached output is byte-identical to an uncached `BuildAllCombatChannelIds`
    call; `BattleStatComposer.Compose` still produces correct results across a real roster swap.
  - Test coverage:
    - **Unit** (`Core.Tests/Stats/ChannelCacheTests.cs`, **5 tests**): same-instance on repeat, swap
      invalidates and un-invalidates by value, cached output matches an uncached rebuild,
      `IsCombatChannel` agrees with `AllCombatChannelIds` for every id it generates (and correctly
      rejects a non-channel), `IsCombatChannel` also invalidates on a roster swap.
    - **Seam** (`Core.Tests/Stats/ChannelCacheSeamTests.cs`, **2 tests**): the real
      `BattleStatComposer.Compose` — the actual consumer, not a fabricated one — across a real
      seventh-element roster swap (reusing E18's fixture shape) and across repeated composes with no
      swap.
    - **Regression guard** (`Core.Tests/Stats/ChannelCacheBudgetGuardTests.cs`, **2 tests**, beside
      `AtomBenchGuardTests`): 10⁴ warm reads of `AllCombatChannelIds`/`IsCombatChannel` allocate under
      64/16 bytes-per-call respectively — generous headroom over "should be ~0", but would fail loudly
      if the cache were ever accidentally removed (back to ~84 allocations per call).
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ChannelCache"`. Full
    Core.Tests regression re-run clean (2822, up from 2813). Injector rebuilt with `FUSIONRPG_GAME_DIR`
    set — 0 errors, confirming the shared `Stats`/`Status` edits don't break the injector-side consumer.
  - Files: `src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs` (cache + `IsCombatChannel`),
    `src/FusionRpg.Core/Status/StatusStatPayload.cs` (`IsKnownChannel` reads the O(1) path), tests.
  - Dependencies: none.

## Deliberately out of scope, tracked

- **A4 — a producer of instances/bindings.** Completeness-audit.md's one finding wave 6 does not
  close: nothing in this program calls `Instantiator`, `RpgStore.SaveInstance`, or `Bind` with a real
  owner, so `ResolveBindings` returns empty for every owner and `AtomPushService`/`AtomRunner`
  (E6/E7/E15/E19 — all built, all tested end to end) are **unreachable end to end in production**.
  Not a gap in this program: per the map §1/§7, items/skills/traits have no *features* here by design
  — a container's content is this program's contract, binding one to an actor is the feature program's
  job. Recorded explicitly (map §7, this line) so the next reader is not misled by every other row
  reading `[x]`. Closes when the first item/skill/trait program calls `Bind`.

## Minor findings (completeness-audit.md §4, C1–C5) — all closed 2026-08-23

Wave 6's own §7 table scoped itself to A1–A4/B1–B5 only — these five were never assigned a module
there. Closed anyway, in the same session, after review found "outside wave 6's stated scope" was not
the same claim as "resolved," and the audit document itself still owns them.

- **C1 — `ClearSessionScopedBindings`/`CountOrphanInstances` had no caller.** ✅ Closed. Wired into the
  server boot sweep in `Program.cs`, right after `LoadContentIntoRuntime()`: a fresh boot is exactly
  the moment every `entity:` binding from the previous process is guaranteed stale (IL2CPP pointer
  reuse), whether the last shutdown was clean or a crash — arguably a *more* correct trigger than
  `board.end` alone, which cannot see a crash-restart. A no-op today (A4 — nothing binds one yet), the
  cheap place for this to already be correct once something does. **4 new tests**
  (`AtomInstanceStoreTests`: clears entity bindings + collects the now-orphaned instance, leaves
  durable owner scopes alone, `CountOrphanInstances` is read-only and correctly counts a never-bound
  instance as an orphan by the query's own definition) + **3 guard tests**
  (`ServerBootSweepGuardTests`, text-scanning `Program.cs` since it's top-level statements) proving
  both calls exist and run in the stated order. `Server.Tests` (15/15) re-run clean — the real boot
  path exercised, not simulated.
- **C2 — nothing checked a status carrying a `stat` overlay also declared `ModifyStat`.** ✅ Closed.
  `StatusEffectBridge.TryApplyFromGrant` now refuses (`skipped.Add(...":status-stat-overlay-without-
  ModifyStat")`) right beside the existing `unknown-statusId`/`status-no-target` refusals, gated on
  the overlay actually carrying `stat` (a status with no such overlay is unaffected). The shipped
  `blight-row.overlay.json` **was** the exact violation — `blight` is real (Contagion/PulseHp/Spread),
  never declared `ModifyStat` — so its `stat` block moved to a new `expose-row.overlay.json` (`expose`
  *does* declare `ModifyStat`), and `StatusStatPayloadTests.The_shipped_example_overlay_validates` now
  reads that file. **3 new tests** in `StatusEffectBridgeTests`: the violation is refused, the correct
  status applies normally, a status with no `stat` overlay at all is unaffected. No shipped seed
  content or scenario fixture uses the `stat` key today (checked), so this is zero-blast-radius
  against real content. Full non-`World` Core.Tests regression: 2284/2284.
- **C3 — `SeedScanner.OwnedFolders` declared `curves`/`rarity`; neither folder existed.** ✅ Closed.
  `data/seed/curves/README.md` and `data/seed/rarity/README.md` added — real, checked-in files (not
  synthetic fixtures) making the empty-folder state explicitly intentional rather than
  indistinguishable from forgotten, with a pointer to the format each already documents in
  `data/seed/README.md`. `.md` files are invisible to the importer by construction (`Directory.GetFiles(r,
  "*.json", ...)`), verified again by **2 new tests** in `SeedScannerTests` against the real repo
  (folders + READMEs exist; the real sweep still finds zero JSON in either). Real CLI import against
  the full `data/seed/**` re-run clean with the new files present.
- **C4 — `UpsertPowerTables`/`UpsertChannelPolicies` didn't bump `catalog_revision`.** ✅ Closed.
  `UpsertPowerTables` bumps unconditionally (no changed-count tracking exists for it, unlike the import
  path); `UpsertChannelPolicies` bumps only when a row actually changed, matching the import path's
  rule. **2 new tests** (`PowerStoreTests.UpsertPowerTables_bumps_the_catalog_revision`,
  `ChannelPolicyStoreTests.A_real_edit_bumps_the_catalog_revision` +
  `Writing_the_same_policy_twice_does_not_bump_the_revision_the_second_time`); both files' full suites
  re-run clean (`PowerStoreTests` 13, `ChannelPolicyStoreTests` 11); `guard-dal.ps1` OK.
- **C5 — `PowerScalar`'s BigInteger-exactness rationale ("stamped into hashed reports") was unearned.**
  ✅ Closed. Comment corrected: nothing stamps `PowerScalar` anywhere today (`PowerScalar.Of` has zero
  production callers, confirmed by grep); the implementation stays right, the doc comment now says so
  instead of claiming a caller that doesn't exist, and names the correction so a future edit can't
  quietly re-add the false claim.

All five verified together: full injector build against the real game interop DLLs (0 errors, real
deploy — the plugin folder was no longer locked once the owner's game session ended) + all four
boundary guards green + the full non-`World`/non-`Loam` regression sweep across every touched suite.

## Unowned, tracked

- [x] **`effect_channel_policy` — OWNER ASSIGNED 2026-08-22: E16.** Resolved by **E1's own code-or-data rule** (*a thing can be data if adding a row changes behaviour without new code; if a new row needs a new consumer, it must be code*): changing a **cap or default on an existing channel** is a value change with a live consumer (`DerivedStatRegistry.cs:46-48`, `ActorDerivedProfiles`) — **that is data**. Adding a *channel* needs a new reader — **that stays code**. So the table ships with **E16** (the channel module, which already adds a composer and a Writer case per channel) holding values only, never channel identity, and registers with **E8** bumping `ContentHashRegistry.CurrentSchemaVersion` to **4** (E18=2, E9=3).
  - ⚠️ Until E16, the 0.95 resist cap stays a code constant: changing it moves every battle golden with an unchanged `contentHash`. Acceptable only because a constant edit is visible in a diff — which stops being true the moment it becomes a row, which is exactly why it must register with E8 in the same change.
