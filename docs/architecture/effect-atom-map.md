# Effect atoms — capability map

**Status:** **Capability map (2026-08-22)** — module ids, dependency direction, build order, and cross-program hazards. **19 spec files covering 20 module ids are written; E1 is built and accepted.** Everything from E2 onward is specced and unbuilt. Source of truth for intent: [effect-atom-ideal.md](effect-atom-ideal.md), whose §13 closes with *"Every question raised in this document is now decided, defaulted, or explicitly scoped to a later spec. The capability map can start."*

> ⚠ **Completeness audit, 2026-08-23:** [effect-atom/completeness-audit.md](effect-atom/completeness-audit.md).
> All 21 rows are built and every suite is green, and **three links were never built** — a loader, an importer
> run, and a producer of bindings — so most of this layer does not reach the running game. **Wave 6 (E20–E25,
> §3 below, Checkpoint F) is the fix**, planned at [tasks/effect-atom-plan.md](../../tasks/effect-atom-plan.md)
> and [tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md). Read the audit before planning anything
> else on top of this program.

Prefix: `effect-atom`. Module specs at `docs/architecture/effect-atom/spec-<module-id>.md`; plan and tasks at `tasks/effect-atom-plan.md` / `tasks/effect-atom-todo.md` (AGENTS.md parallel-programs convention).

> **New module proposed 2026-08-30: [`derived-write-lawn`](effect-atom/spec-derived-write-lawn.md) — needs a `decisions.md` row, not yet approved.**
> `stat.derived` is `RuntimeSupportMatrix(None, Full, None)` (`AtomKindRegistry.cs:149`) — E12 gave
> **battle** a consumer; the **lawn has none**, so no aura can reach a lawn entity through this layer.
> That is why five features (patron, stars, injuries, contracts, and now commander aptitudes) each grew
> a private derived-write path, exactly as [actor-hub-ssot.md §6.1](actor-hub-ssot.md) predicted. The
> spec **extends the already-built buff/debuff scope primitive** (`ScopeCompatibility` +
> `BattlefieldOwnSideReactor`, shipped 2026-08-29) rather than adding a sixth path — `decisions.md`'s
> own Buff/debuff row already names this wiring as "a separate, later task". The delivery half is
> **already fixed and live-proven** (`EntityFinal.DiffersFrom`, 2026-08-30): a lawn executor's output
> reaches Unity the day it exists, with no edit to `EntityApply`.

**Why this is being specced now:** the [action](action-map.md) program needs a real container contract, and the owner chose to spec atoms first rather than depend on a placeholder (decision D1, 2026-08-22). That makes this program the critical path for the action architecture, and through it for the battle-timeline gate.

---

## 1. What this program is, in one paragraph

An **atom** is one indivisible statement of what happens, with its numbers, its conditions, and its power price attached. Everything a player can own, learn, equip, or inherit is a **container** of atoms. Kind *logic* stays in code; concrete *values* live in SQLite. The layer is a **compiler, not an applier** — it produces the `EffectGrant` / `RpgEffectEvent` shapes the Funnel already accepts and never touches Unity, never writes a stat, never calls a status method. The sealed Foundation contract does not move.

## 2. What already exists, and what this replaces

Audited in the ideal §2 and re-checked against `src/` on 2026-08-22:

| Today | Count | This program's answer |
|---|---|---|
| `EffectSeedCatalog` C# literals | 16 defs | Rows. First migration, and the proof (E11) |
| `StatusCatalog`, ADR-locked code-first | 21 | **Stays code.** Kinds are code everywhere; only magnitudes could ever move, and only if a status spec asks |
| `TraitBattleCatalog`, 15 hand-coded facet fields | **14** | Split: **1** becomes a container (`critical-hunter`); the other **13** are blocked on event dispatch, the kind ceiling, the turn kernel, and the AI/rewards layers (E12) |
| `UniqueEquipmentCatalog` stubs | 3 | Greenfield — containers, instances, rolled values, item power for free. **No item system is built here** |
| Skills | 0 | Containers from day one; activation and cooldown belong to the turn kernel, not here |
| Effect data tables | **0 of 38** are `foundation_effect_*` | Nine new tables (E4–E6), plus `effect_curve` in E2 |
| Power | Does not exist | E9/E10 |

Four parallel content systems sharing no vocabulary, and skills would have become a fifth. **That is the cost this program exists to stop.**

## 3. Modules

### Wave 1 — the spine (code and schema; no content moves, nothing observable changes)

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E1** | `atom-kind-registry` | Kind ids, param schemas, resolve semantics, the **attach-point list** (stat / resource / status / shield / board), and the **runtime support matrix** as a living audited table. Bind-time validation error when a runtime cannot execute a kind. No magnitudes, no content ids. | — |
| **E2** | `value-spec-and-curve` | The value spec `{min, max, roll, scale}`; the three roll policies (`fixed`, `onInstantiate`, `onApply`); the `effect_curve` table with integer-‰ interpolated points; the named RNG streams (`atom.apply`, plus the per-instance roll seed). | — |
| **E3** | `predicate-tree` | Typed AND/OR/NOT tree over a **closed** leaf list, hard depth limit, validation that **rejects** unknown leaves rather than ignoring them. | — |
| **E4** | `atom-schema` | `effect_atom`: `(family_id, tier, variant)`, `when_json` (trigger/stage, chance ‰, ICD, predicate), `params_json`, `tags_json`, `icd_key`, `enabled`, `revision`. | E1, E2, E3 |
| **E5** | `container-schema` | `effect_container`, `effect_container_atom` (ordered, with value-spec overrides), `effect_container_pool` (weighted, grouped). **Fixed core plus optional weighted pool.** This is the contract `action` A1 consumes. | E4 |

### Wave 2 — persistence and compile (the layer starts doing work)

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E6** | `instance-and-binding` | `effect_instance` / `effect_instance_atom` (frozen moment-2 rolls, `roll_seed`, power at roll time) and `effect_binding`, which replaces the logical `foundation_effect_grant` and absorbs today's `mods_json` grant blobs — **named 2026-09-01: `effect-pipeline` module 5 `mods-absorption`** is what carries out this absorption (`effect-pipeline-map.md`), so the promise here is no longer aspirational. Runtime state (ICD clocks, stacks, status instances) stays in session RAM — **no new durable runtime table**. | E5 |
| **E13** | `runtime-form-benchmark` | The build-time benchmark the ideal explicitly refused to decide on paper: typed object graph versus flattened non-recursive encoding, **against real content**. Settled already: no dictionaries and no string comparison on the per-hit path. *(The no-recursion law was withdrawn — the 7 ns winner recurses.)* | E4, E2, E3 |
| **E7** | `atom-compiler` | Binding → Foundation grant / damage rider / status apply. Compiles at load into the form E13 picks. **Server compiles and pushes compiled output**; the injector never holds content rows. | E6, E13, E1 |
| **E8** | `content-hash` | **BUILT.** A hash over atom / container / container_atom / container_pool / curve / rarity rows — never instances. Sort-then-concatenate, columns length-prefixed, covered set held in a **versioned registry** (`contentHashSchemaVersion`, v1 = the six tables that exist). The stamp carries per-table digests so a version bump can still compare the tables both versions share. Its consumer today is the boot sweep's replay refusal; stamping into the report is **E12**'s, because a new stamped field *is* a golden diff. | E4, E5 |

### Wave 3 — the proof

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E11** | `effect-def-migration` | **BUILT 2026-08-22 — Checkpoint D reached.** Falsified the schema and the compiler in seven places, every one invisible until real content ran through: the compiled def id was `atom.atom.*`; a compiled `stat.modify` reached FA1 as none of `flat`/`increased`/`more` and applied a flat **zero**; a three-trigger group emitted three copies of one action; `icd_key`'s grammar forbade the dots a migrated id needs; `status.clear` named a key nothing reads; `resource.delta` did not declare `channel`; and D10's overlay-or-param magnitude check did not exist. The 16 `EffectSeedCatalog` defs become rows, and the **49 existing JSON fixtures** (19 `effect-*`, 5 `combat-*`, 25 `status-*`, plus 15 golden plans) — already the data format — become the test corpus. The cheapest migration, against effects Foundation executes today. **If the schema is wrong, this is where it shows.** | E7, E8, E14a |
| **E14a** | `importer` | **BUILT 2026-08-22.** Seed file format (`AtomSeedFile`, Core), the import transaction (`RpgStore.ImportContent`, data — it needs the store's private connection and gate, and `guard-dal.ps1` scans only `src/`), and `tools/AtomImporter` — arguments, a report, and `SeedScanner`, covered by a new `tests/FusionRpg.AtomImporter.Tests` in CI. Four folders, not two: curves and rarity bands are hashed content tables too. **Validate-all-then-write in one transaction**, cross-file duplicates refused naming both files, and the `catalog_revision` bump once per transaction **and only when something changed**. **Sequenced before E11**, which cannot load its own seed rows without it. | E5, E8 |
| **E14b** | `content-validation` | **BUILT 2026-08-22.** Budget, drift and lint, with the report carrying what it *evaluated* so an empty pass cannot look green. Runs over the real shipped corpus. | E11, E9 |

### Wave 4 — power

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E9** | `power-vector` | **BUILT 2026-08-22.** Three of the four carried defects (D1, D3, D4) turned out to be arithmetic rather than design: integer `chance/1000` priced every proc below 1000‰ at **zero**, and omitted spawn/target counts did the same. All closed. Also found that every damage atom priced **negatively**, so a rarity budget relaxed as an item got deadlier. | E4, E2 |
| **E10** | `power-reads` | **BUILT 2026-08-22.** Scalar is exact integer arithmetic over BigInteger — `Math.Pow` is not bit-reproducible and the number is stamped into hashed reports, and five categories near 6000 each already overflow Int64. The AI-contract test the spec deferred as vacuous **is enforceable now** and ships, with a positive control. | E9, E18 |

### Wave 5 — the expensive migration

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E12** | `trait-migration` | **BUILT 2026-08-23 — Checkpoint E, measured zero delta, NO sign-off needed.** The gate was asserted and never tested: run, **not one blessed hash moved** and `RulesetVersion` stays at 2. The `contentHash` stamp is provenance and sits outside the determinism hash, exactly as the platform stamp already did — the spec's "a stamped field IS a golden diff" holds only if the field is in the hash input. One trait migrates (`critical-hunter`); `stat.derived` re-opens for battle only. | E11, E14b |

### Added by the gap-clearing round (2026-08-22)

*(A provenance heading, **not** a build wave — build positions are in §4.)*

Four owner decisions on [effect-atom/atom-family-library.md](effect-atom/atom-family-library.md) §4–§5 add these. **E15 is not optional** — the map as written had no runtime home for per-binding state, so predicate-tree atoms and `capPerMatch` had nowhere to live.

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E15** | `atom-runner` | **BUILT.** **The Secondary effect runner — the missing runtime half of E7.** E7 compiles what Foundation can already express (FT* trigger + simple filters → an ordinary grant, zero runtime cost). E15 runs what it cannot: per-binding state (cooldowns, counters, charges), predicate-tree evaluation, and **`capPerMatch` counters** — the economy cap that is in the FA9 allowlist today with no implementation anywhere. Hot: Core on the injector game thread, dispatching via `Funnel.Enqueue` only, never awaiting the server. | E7, E13, E3, E2 |
| **E16** | `channel-extension` | **BUILT 2026-08-23, injector half included.** 8 → 11 channels, direction declared once and read by compose, pricing and lint. Found that `AtomRowValidator` never ran E1's channel check, so an invented channel reached the table. `effect_channel_policy` at registry **v4**. The injector half landed once `FUSIONRPG_GAME_DIR` was set: baselines captured, three writer cases, the extras path stopped, cheat keys through `Override` via a real-valued sibling map (`CheatAbsolute` is `int` and would truncate 1.5s to 1s). 7 guard tests. | E11, E9, E14b |
| **E17** | `status-payload-completion` | **BUILT 2026-08-23 — all three "blocked" items closed.** Every gate I named was untested: the injector builds fine once `FUSIONRPG_GAME_DIR` is set (which also unblocked E16's injector half). `ember`/`jala`/`kelp` wired — and they are **flags, not timed CC**: `SetJalaed` takes no parameters, so the obvious copy of `SetFreeze` does not compile. `charm_pulse`'s def error fixed: FA2 was emitted only for `UnityCc` statuses, so it queued an action that matched no case and did nothing. `poison` resolved at its cause — `IsCcLocked` tested `Kind`, which conflates role with delivery, so poison locked actors out of their turn; it now reads the category, un-locking poison and nothing else. | E11 |
| **E18** | `element-roster-data` | **BUILT 2026-08-22.** The roster and both matchup matrices moved from hardcoded enum and `switch` to rows read through `ElementTable`: `effect_element`, `effect_element_matrix_combat`, `effect_element_matrix_shield`. **Two tables — but NOT because they differ.** The claimed light/dark asymmetry does not exist: compared exhaustively they are identical across all 36 pairs, light ⇄ dark mutually strong in both. They stay separate because the shield spec makes them independently editable. Channel generation reads the roster table, so a seventh element generates its 12 channels with no code change. Enum generator still owed. E8 registry at **v2**. Derived channel ids stay *generated* from families × roster, so a seventh element needs no new consumer. Owns ordinal stability, the `families × (roster + omni)` replacement for the hardcoded 84-count test, and extending the content hash to cover the roster. **Registers its three tables with E8 and bumps `ContentHashRegistry.CurrentSchemaVersion` to 2.** | E4, E8 |
| **E19** | `compiled-push` | Server → injector delivery of compiled grants and runner entries: extends `effects.grants.apply` on Hello, negotiates against `catalog_revision`, and holds the guarantee that the injector carries **no content rows**. Without it E7's only consumer is tests. | E7, E8, E15 |

**Also folded into existing modules:**

- **E1** carries the **code-or-data rule** in its spec: *a thing can be data if adding a row changes behaviour without new code; if a new row needs a new consumer, it must be code.* The 12 derived channel **families stay code** because each has a named reader — a thirteenth added as a row would be `status.expose.*` all over again: legal, registered, zero readers, dead.
- **E9** stores coefficients as **data** (`power_coefficient`) with a sweep-written **proposal** side table and a drift-reporting test, so "hand-authored now, fitted later" is mechanically possible instead of aspirational.
- Per-channel policy (compose kind, default, cap) becomes data too, removing today's duplication between the constants file and `DerivedStatRegistry`.

**Also folded into an existing module:** E6 gains two owner-key scopes, `sector:{id}` and `slot:{id}`, so world buildings and sector environments can bind. The trigger list stays at 7 — `OnWorldTick` / `OnSectorEnter` / `OnBuildComplete` belong to the world spec that needs them.

### Wave 6 — the seams (added by the 2026-08-23 completeness audit)

Not a new capability wave — every module here makes an **already-built** capability reach the running
game. Full task detail: [tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md).

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E20** | `content-boot` | The loader: `RpgStore.LoadContentIntoRuntime()` calls `ElementTable.Use`/`PowerTables.Use` at host startup, and `deploy-play.ps1` runs the importer against the live data dir first. Closes the finding that editing a roster row, a matrix cell, or a coefficient moved the content hash and changed nothing. | — |
| **E21** | `status-stat-applier` | No new plugin — mirrors `ExecModifyStat`'s existing session-bag pattern: `EffectRuntime.OnApplied`/`OnEnded` now `Upsert`/`WithdrawSource` a status's mods. Found and fixed a real bug: `ToModifiers` used a bare owner-key pointer `StatApplyScope` never matches. Closes the finding that `rally`/`expose`/`command`/`shatter` still changed no stat after E17. | — |
| **E22** | `channel-policy-reader` | `ChannelPolicyTable`, the same static shape as `ElementTable`/`PowerTables`. Only `direction` has a live consumer (`StatChannels.IsLowerBetter`) — `DerivedStatRegistry` operates on derived channels this table can't even name. Closes `effect_channel_policy` being hashed at registry v4 with zero readers and no author path. | E20 |
| **E23** | `content-codegen` | `tools/ElementEnumGen`: verifies + generates `ElementTypeId`/its three companion switches from the roster, `TraitAtomSource.Shipped()` from migrated trait containers, and `EffectAtomCatalog` (replacing `EffectSeedCatalog`) from `data/seed/atoms/fx-*.json` via `AtomCompiler.Compile` + `AtomPushCodec.ToDef` (E19) — proven by a new execution-parity suite, not just DTO comparison. Closes all four values authored twice. | — |
| **E24** | `validation-in-ci` | `AtomImporter --validate` runs `ContentValidation.Lint`/`Drift` and fails the process on a finding (Budget skipped — no ceiling data exists in the schema); `Server.Tests` and `E2E.Tests` join `ci.yml`, plus a general guard for the next unwired suite. | — |
| **E25** | `compose-channel-cache` | Caches `AllCombatChannelIds` by reference to `ElementTable.Current` — no version counter needed, since `Use`/`UseScoped` always assign a new immutable instance. Closes the uncached 84-string rebuild on every compose and every status-payload channel check. | — |

**Wave 6 spec files — retrospective, written 2026-09-03.** Wave 6 shipped in one day with no specs; its
only record was [tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md). These six backfill the
module list so every id in this map points at a spec. **They describe what shipped, not what to build.**

| id | Spec | Records |
|---|---|---|
| **E20** | [effect-atom/spec-content-boot.md](effect-atom/spec-content-boot.md) | `LoadContentIntoRuntime`, its one server call site, the `deploy-play.ps1` import step, and the empty-store fallbacks |
| **E21** | [effect-atom/spec-status-stat-applier.md](effect-atom/spec-status-stat-applier.md) | The two `EffectRuntime` calls, the `entity:` owner-key fix, and why the battle runtime still has no applier |
| **E22** | [effect-atom/spec-channel-policy-reader.md](effect-atom/spec-channel-policy-reader.md) | `ChannelPolicyTable`, `DirectionOf` reading it first, the seed/import author path, and the three columns still unread |
| **E23** | [effect-atom/spec-content-codegen.md](effect-atom/spec-content-codegen.md) | `tools/ElementEnumGen`'s five modes, the generated `EffectAtomCatalog`, the five repointed call sites, and the frozen oracle |
| **E24** | [effect-atom/spec-validation-in-ci.md](effect-atom/spec-validation-in-ci.md) | `--validate` and `ValidationGate`, the two CI test-project lines, the general CI wiring guard — and that the gate itself still runs in no CI step |
| **E25** | [effect-atom/spec-compose-channel-cache.md](effect-atom/spec-compose-channel-cache.md) | The reference-keyed cache slot, its 2026-08-25 `AsyncLocal` correction, and the 84 → 196 count change |


## 4. Dependency graph and build order

Build position and dependencies, in one table. **This table is the derived view — each row's `Depends on` must equal that module's spec header.** An ASCII graph used to live here; it went stale twice without anyone noticing, so it is gone.

| # | Module | Depends on |
|---|---|---|
| 1 | **E1** | — |
| 2 | **E2** | — |
| 3 | **E3** | — |
| 4 | **E4** | E1, E2, E3 |
| 5 | **E5** | E4 |
| 6 | **E6** | E5 |
| 7 | **E13** | E4, E2, E3 |
| 8 | **E7** | E6, E13 |
| 9 | **E8** | E4, E5 |
| 10 | **E15** | E7, E13, E3, E2 |
| 11 | **E19** | E7, E8, E15 |
| 12 | **E14a** | E5, E8 |
| 13 | **E11** | E7, E8, E14a |
| 14 | **E18** | E4, E8 |
| 15 | **E9** | E4, E2 |
| 16 | **E10** | E9, E18 |
| 17 | **E14b** | E11, E9 |
| 18 | **E12** | E11, E14b |
| 19 | **E16** | E11, E9, E14b |
| 20 | **E17** | E11 |


**Build order (revised 2026-08-22 after the four-way audit):**

```
E1 ‖ E2 ‖ E3 → E4 → E5 → E6 → E13 → E7 → E8 → E15 → E19 → E14a → E11 → E18 → E9 → E10 → E14b → E12
                                                                            E16, E17 after E14b
```

Six structural corrections, each closing an audit finding where the order was **impossible**, not merely awkward:

| Change | Closes |
|---|---|
| **E14 splits.** `E14a` (importer + schema-validation wiring) moves **before E11**; `E14b` (budget, drift, lint, one-row claim) stays after E9 | E11 had no way to load its own seed rows — the importer sat three positions later |
| **`ActorPowerCache` moves from E10 into E9** | E9's spawn recursion needs memoized actor power to terminate; the arrow pointed backwards |
| **`effect_curve` DDL/DAL joins E2** (`RpgStore.Curves.cs`) | the table three modules depend on had **no owner**, and Core cannot hold SQL under `guard-dal.ps1` |
| **New module E19 `compiled-push`** | E7 attributed delivery to "E-push" — a module in no map, no spec, no owner. The compiler's output had no path to the game |
| **E18 sequenced before E10** | E10's matchup read consumes E18's two matrix tables; E18 was unpositioned "off the critical path" |
| **E8 covers a versioned registry, not a fixed list** | E9 and E18 add tables after E8 ships, invalidating every hash E11 stamped |


E7 owns the `RunnerEntry` contract. *(The `Depends on` corrections that used to be listed here are now applied directly in the §4 table — a changelog sentence is not a dependency. E9 is **not** among them: predicates are deliberately not priced.)*

E1, E2, and E3 have no dependencies on each other and can be specced in one pass.

## 4a. Definitions

Every module spec reads [effect-atom/definitions.md](effect-atom/definitions.md) — the shared vocabulary the audit found missing. Where a spec and that document disagree, **the definitions win** until the spec is rewritten.

It also carries the model correction the specs got wrong: **items have no behaviour; actors do.** Items, traits, and skills are *sources* that put atoms on an actor's effect list; that list is the runtime structure, and classification is **per atom**, never per binding.

## 5. Checkpoints

- **✅ Checkpoint A — the spine compiles.** E1–E5: kinds, values, predicates, and the two schemas exist with tests, and **nothing in the game has changed**. Pure addition; if a golden moves here, stop.
- **✅ Checkpoint B — the container contract is signed.** E5 reviewed. **This is the moment [action](action-map.md) A1 unblocks** — it needs the contract, not the implementation.
- **✅ Checkpoint C — atoms execute.** E6+E13+E7+E8+E15+E19+E15+E19: a bound container compiles to a Foundation grant and the Funnel runs it. The content hash **exists** (E8) but is not yet **stamped into reports** — that is E12's, because adding a stamped field to a report *is* a golden diff.
- **✅ Checkpoint D — the schema is proven.** E11: all 16 defs are rows and the **49** fixtures pass unchanged. **The claim "a new effect costs one row" is either true here or the design failed.** `EffectSeedCatalog` **is deleted** (E23, wave 6, 2026-08-23) — production reads a single generated catalog now.
- **✅ Checkpoint E — goldens measured.** E12 only. The gate was asserted and never tested; run, **not one blessed hash moved**, no owner sign-off needed.
- **✅ Checkpoint F — the layer is live, not just tested.** Added 2026-08-23 by the [completeness audit](effect-atom/completeness-audit.md): waves 1–5 proved every module correct in isolation, and almost none of it reached the running game — no host loaded a content table, nothing ran the importer, nothing created a binding, E17's status payload had a parser with no applier. **Wave 6 closed same-day, all six modules.** A real applier bug found and fixed by E21's own seam test — `StatusStatPayload.ToModifiers` set a bare pointer where `StatApplyScope` requires an `entity:` prefix, so the contribution silently composed nothing until the seam test caught it. E23 was first reported as partially closed with `EffectSeedCatalog`'s deletion deferred "pending a converter that doesn't exist" — that claim was wrong: `AtomPushCodec.ToDef` (E19) already was that converter, found only on a forced re-read. `EffectSeedCatalog` is deleted from production, all five call sites repointed at a checked-in generated catalog, proven safe by a new execution-parity suite (19 real scenarios through a real `EffectBag`) before and after the swap. Full acceptance in [tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md).

## 6. Cross-program hazards

**H1 — two programs both want the goldens.** The battle-timeline gate (B13–B15) must prove **byte-identity**; `E12` deliberately **moves** goldens and bumps `RulesetVersion`. If they overlap, neither can tell whether a hash moved for its own reason. **They must be strictly ordered, and the order should be decided before either starts.** E12 is last in this program precisely so it can wait.

**H2 — the action program is blocked on E5, not on all of this.** Waves 3–5 are ~two thirds of the work and the action program needs none of them to *spec* A1. Blocking the whole action program on the whole atom program would be a self-inflicted delay; Checkpoint B is the real gate.

**H3 — battle consumes one opcode of eleven.** The ideal's §5.2 audit is blunt: `BattleEffectSink` consumes **FA10 only**, the battle engine **never calls `OnEvent`**, and FA1 stat grants are ignored by the bag sink. So "one vocabulary, many backends" is aspirational for battle, and the bind-time validation error will fire constantly at wave 1. That is correct behaviour — loud beats silent — but **wave 1 must not promise battle support it cannot deliver**, and the runtime support matrix (E1) is the living record of what is actually true.

**H4 — `action` A4 must not invent a second condition language.** E3 is the one predicate tree. This is written into both maps.

## 7. What stays out

| Out of scope | Owner |
|---|---|
| Item, skill, and trait *features* | Their own specs, inheriting this contract |
| Targeting, retreat, threat, decision-making | The AI layer (does not exist) — contract offered in advance, ideal §5 |
| Merging, ordering, and mitigating damage from many sources into one hit | The damage-applier layer (does not exist) — contract offered in advance, ideal §5.1 |
| Action activation, cooldown, wind-up | The turn kernel — [battle-timeline-map.md](battle-timeline-map.md) |
| Status *kind* logic | Stays code, ADR-locked; the lock does not need revisiting to start |
| The Foundation contract | Sealed. Atoms compile *into* it |
| **A producer of instances/bindings** — anything that actually calls `Instantiator`, `SaveInstance`, or `Bind` with a real owner | Whichever item/skill/trait program binds a container first. **Until then the runtime this program built (E6/E7/E15/E19) is inert**: `ResolveBindings` returns empty for every owner, so `AtomPushService` compiles nothing and `AtomRunner` never receives an entry — proven correct end to end by tests, unreachable end to end in production (completeness-audit.md A4) |

## 8. The things still genuinely open

Everything the ideal raised is decided, defaulted, or scoped out — with one exception it names itself:

> Pricing multiplicative pairs. Crit rate × crit damage, the element ring, and shield layers all multiply, so a per-atom cost function prices each half in isolation and underprices both.

The ideal's resolution was to **add a read rather than a smarter formula**: stored atom power stays
context-free, and AI and the balance sweep read **marginal** power instead (E10).

**That resolution did not work, and the third audit pass proved it arithmetically.** `actorPower` is
defined as `Σ atom.power`. A sum has no cross terms, so
`marginal = Σ_{A∪{x}} − Σ_A = p(x)` for **every** actor — the marginal read returns exactly the
context-free stored power it was supposed to improve on. Multiplicative pricing is **open**, not solved.

**Attempted 2026-08-22, and the fix did not hold.** The proposal was: `actorPower` aggregates **channel totals** and prices the
composed result rather than summing per-atom prices. Crit rate and crit damage land on different
channels, compose the way combat composes them, and the price of the composition is not the sum of the
two prices — so the marginal read finally differs by context. Stored `atom.power` stays the
context-free number for budgets and display. See [definitions.md](effect-atom/definitions.md) §7.

**Second-pass verdict: still open.** `normalize` is linear, so pricing a channel total equals summing
atom prices — the change is inert, and for crit rate × crit damage (different channels) there is no
composition at all. A genuinely nonlinear cross-channel price function is what D2 needs, and it is not
yet designed.

**E9 is build position 15 and blocks nothing before it.** The power model is deferred there rather than
solved now; D1–D4 in §13 carry the verified findings and the shape of each fix. The three others: the display scalar is now
`geomean(vᵢ + 1) − 1` over all five categories, so it can never rank a strictly better vector lower
(**D1**) — though it is underspecified and inverts sort order at real magnitudes; a summon is priced by
its body (**D3**) — though `count` needs a floor; and permanent modifiers declare no trigger, so the 26
passive families price correctly (**D4**) — though the integer `chance/1000` zero is still live.

Both E9's **coefficients** and its **function** remain open.

Still open and owner-gated: **D5** (`System.Random` on the fixture path and `TickCount` in the injector —
fixing either moves goldens and bumps `RngAlgoVersion`). **D6**, **D7**, **D9**, **D10** are mechanical
and tracked in §13.

## 9. Success criteria

1. A new concrete effect using an existing kind costs **one row** — no build, no code, power derived. If it ever costs more, the design failed and should be said to have failed.
2. `EffectSeedCatalog` is deleted, and the **49** JSON fixtures pass unchanged.
3. The Foundation contract is untouched; the three guard scripts stay green throughout.
4. A changed content value produces a **changed content hash** in the report — no silent drift.
5. Nothing here invents targeting, AI, damage merging, or action timing.

## 10. Counts corrected by the repo sweep (2026-08-22)

The closed vocabulary lives in [effect-atom/atom-catalog-ssot.md](effect-atom/atom-catalog-ssot.md), which is the content input to E1 and E4 and the corpus E11 migrates. Its six-way sweep corrected five counts that this map and the ideal had both been quoting, and they are corrected above:

| Was | Actually |
|---|---|
| 19 JSON fixtures | **49** (19 `effect-*`, 5 `combat-*`, 25 `status-*`, + 15 golden plans) |
| 10 FA opcodes | **11** — `GrantShield` is shipped, unnumbered, absent from the FA1–FA10 table, and executes bag-side in Core rather than in the injector sink |
| 13 battle traits | **14**, split 7 `FunnelRouted` / 7 `EngineBehavior` |
| 21 statuses | 21 declared, **~13 functional** |
| 11 effect-shaped sites | **12** — `ContractPolicy` also carries magnitudes |

It also found a schema-level error worth repeating here, because E4 would have inherited it: **the documented `channel` enum in `effect-data.md` is fiction.** Four of its values are cheat-document keys that bypass the modifier bag and cannot be reached by an effect at all; four real armour channels are missing from it. The true primary set is eight: `hp · maxHp · atk · defense · arm1 · arm1Max · arm2 · arm2Max`.

---

# Waves 7 and 8 — added 2026-09-03

Ideal: [effect-atom-ideal.md](effect-atom-ideal.md) §W7 (the pool) and §W8 (capability). **Wave 7 fills
the pool for capabilities that exist. Wave 8 adds capabilities that do not.** They are independent.

## 11. What changed the shape of Wave 7 before it started

Three corrections from the 2026-09-03 adversarial pass, each of which makes a module smaller or removes
one. **Read these before the module table or the table looks arbitrary:**

1. **The pool is buckets, not a cartesian** (§W7.9). An atom names a **pool** of channels; element, tier
   and cell resolve at **layer 4**, per player, at roll time. The owner's four-layer model
   (`effect-pipeline-ideal.md` §5) already said so. **This removed a 41,550-row emitter and replaced it
   with a vocabulary.**
2. **98 atom families are already authored** in `data/seed/items/affix-families/`, all 12 kinds
   (§W7.7.1). E30 reconciles and references; it does not author from scratch.
3. **`effect-pipeline` is approved with ten written specs** and owns the slot declaration, the resolver,
   affix generation, binding production and the authoring run. **Wave 7 states the split rather than
   deleting modules** (§W7.11.1) — the seam table there is normative.

## 12. Modules — Wave 7 (E26–E32, plus the backfill and residual-sweep ids E42–E51)

| # | Module | Owns | Model? | Depends on |
|---|---|---|---|---|
| **E26** | `runner-def-emit` | Emit a def per `RunnerEntry` from its `Params`, so the runner path is deliverable. Closes the gap `AtomRunner.cs:207-209` names in its own comment: *"the def for a runner atom is not emitted by anything yet."* Today any atom with a per-hit roll range, `capPerMatch`, `charges`, `everyHits`, `maxStacks`, or a non-legacy predicate **throws `unknown effect_id` at grant time** | No | — |
| **E27** | `lawn-element-bind` | Pass species `elementPrimary`/`Secondary` through `InjectorCombatBridge` / `InjectorStatusBridge` into `StatContextFactory`, mirroring `BattleEngine.cs:36`. **Nothing in `src/` passes `elementTypes:` today**, so every lawn actor is `ActorElementTypes.Neutral` and **196 element-expanded channels are inert on the lawn** | No | — |
| **E28** | `param-parity` | The declared-but-dropped and honestly-refused params: `resource.delta` over all 6 resources · `board.action` `damage` · `status.clear` to 21-status parity · `grid.clear` cell targeting · `spawn.entity` `count`/`atk` · `grid.spawn` `graveType` · `box.set` `cells[]`. Plus the `fx.set_dirt_box` value fix (authors `boxType: 1` = **Water**, named "dirt") | No | — |
| **E29** | `kind-value-guard` | A registry-backed value check **per kind** — today `AtomKindRegistry.Validate` value-checks only `stat.modify` (G6), so `status: "wither"`, `currency: "souls"` or `gridItemType: 999` validates, compiles, reaches the executor, matches no case and does nothing forever. **Includes the `stat.derived` registered-channel check** `AtomRowValidator.cs:296` explicitly defers to *"G6's job"* — which never runs for it | No | — |
| **E30** | `channel-pool` | **L2 — the missing layer.** The atom-side contract: what a channel **pool** is (a named, authored set of channels with a count and per-member weights), and how `params.channel` may name one instead of a concrete channel. **Also owns pricing a pooled atom**, which `CostFunction`'s `(kindId, channel)` key cannot do today. Reconciles with the 98 authored families | No | E28, E29 |
| ~~E31~~ | ~~`affix-pool-narrowing`~~ | **WITHDRAWN** — it existed only to shrink a 41,550-id prompt. §W7.9 removed the multiplication that created it. Not a scope cut: its reason no longer exists | — | — |
| **E42** | `units-correction` | Correct `definitions.md` §2's units row — `combat.power.*` / `combat.defense.*` / `combat.shield.*` are **flat game units**, not resolver points, proven by the item program 2026-08-22 and never applied. `DESIGN-GATE.md` makes that file win over every spec, so no downstream module can fix it by being right. **Prerequisite of E30 and E38**, both of which author magnitudes from it | No | — |
| **E43** | `family-expand` | The families→atoms rule, **specced nowhere after W7.9 replaced its module**. Reads the 98 authored family definitions, emits **one row per (family, tier) ≈ 490** — element is a **pool reference**, not seven rows; cells are targets, not identities. Owns getting the folder swept and fixing the two CI gates its output would otherwise trip | No | E30, E42 |
| **E44** | `power-sweep` | **Research work with a deliverable**, not a code module: the fitted coefficients E9 was always scheduled for, and D2's close. All 20 coefficients are flat at `CoeffMilli = 1000` today. **Owner, 2026-09-03: the gate stays but may be passed deliberately — *"we cannot avoid tuning in this game, so that is normal."*** Success is measurable: `marginal(x, A)` must differ by `A` for crit rate × crit damage, the element ring and shield layers — the test **both prior attempts failed** | No | E9 (built) |
| **E45** | `derived-write-lawn` | **Gets a module id at last.** A 22 KB spec (2026-08-30) that appears in no map table — only as a pre-§1 callout saying it *"needs a `decisions.md` row, not yet approved."* Spec exists; the ADR does not | — | — |
| **E46** | `player-content-boot` | ⛔ **Found by the Wave 6 backfill.** `AtomImporter` is invoked from exactly one place — `scripts/deploy-play.ps1:218`, a dev script — so **a player install boots on the code fallback with the whole content layer inert.** Everything Wave 7 and the action corpus generate would reach the owner's deploy and no player. Owns the install-time import, and making the fallback **visible** rather than indistinguishable from success | No | — |
| **E47** | `validate-gate-ci` | ⛔ **Also from the backfill.** E24 shipped `--validate` and wired two test projects; **it never wired the validate step**, so the gate it is named for is hand-run only. Owns the CI step **and the finding policy it needs first** — at ~490 generated rows `orphan` fires per unreferenced atom, and a gate that fires 83,100 times on its first real run is one that gets commented out | No | — |
| **E48** | `reader-map-derive` | ⛔ **From the 2026-09-03 residual sweep.** `ContentTableReaderGuardTests` asserts a **hand-typed 18-table list** (`:77-85`) plus six text assertions, not the property the todo claims — *"every table in `ContentHashRegistry.Current` has a reader."* Derive the check from the registry (`ContentHashRegistry.cs:369`, `IReadOnlyList<ContentHashTable>` over `CurrentSchemaVersion = 9` at `:37`) so a new table fails for the right reason. Claims the same trip-wire from `spec-channel-policy-reader.md`'s residuals | No | — |
| **E49** | `battle-status-stat` | ⛔ **From the 2026-09-03 residual sweep.** E21 wired the lawn half only: `StatusRuntime.OnApplied` (`StatusRuntime.cs:118`) has exactly **two** subscribers in `src/`, both injector (`EffectRuntime.cs:69`, `Hud/ActorHudInvalidator.cs:25`), so `rally`/`expose`/`command`/`shatter` **change no stat in battle**. Subscribe battle's status runtime to the same `ToModifiers`/`SourceIdOf` pair through its own modifier bag. The mechanism is already Core-proven by `StatusStatApplierSeamTests` — this is two subscriptions, not a design | No | — |
| **E50** | `effect-check` | ⛔ **From the 2026-09-03 residual sweep.** `ElementEnumGen` has an `--effect-emit` (`Program.cs:25`, undocumented — the header at `:7-11` lists four modes) and **no `--effect-check`**, so a *value-only* edit to an `fx-*.json` atom leaves `EffectAtomCatalog.Generated.cs` stale and fails nothing. Add the check, wire it into CI beside `DemonSpeciesGen --check` (`ci.yml:50`), and fix the usage header. **Sequences after E43 and E26**, which own the two content-shaped gates it would otherwise trip | No | E43, E26 |
| **E51** | `channel-count-drift` | ⛔ **From the 2026-09-03 residual sweep.** Documentation, not code: every `84` generated-channel figure in this map, `tasks/effect-atom-todo.md` and `completeness-audit.md` predates derived-stats H.1 (2026-08-24), which took families to 28 — it is `28 × 7 = 196` today (`DerivedStatChannels.cs:348`). Replace the figure, point at the source, add the doc-drift assertion | No | — |
| **E32** | `affix-import-path` | The chain that makes an authored affix loadable: `SeedContent.Affixes` · an `"affix"` case in `AtomSeedFile.TryKind` (a file with that kind is **refused** today) · `effects` in `SeedScanner.OwnedFolders` · a production caller for `UpsertAffix` (zero today). **Also the container-pool key**: `AtomSeedFile.cs:253` reads JSON key `"atom"` into `ContainerPoolRow.AffixId`, whose own doc says *"references an AffixRow, never a bare atom directly"*. Latent only because no shipped container has a `pool`, and **no test pins the key** | No | E30 |

**Every Wave 7 module is model-free.** No token is spent in this wave.

## 13. Modules — Wave 8 (E33–E41)

Ideal §W8. **E33 is the only one the action corpus is blocked on**; the rest are capability breadth.

| # | Module | Owns | Depends on |
|---|---|---|---|
| **E33** | `activation-edge` | Raise `OnActivate` on the lawn. It is in `AtomTriggers.All` and `TriggerCount = 8`, **absent from `EffectDtos.EffectTriggers`, and raised nowhere in the injector** — so it works in Battle and is inert on the lawn. *"The actor decided to act"* is the trigger an **action** runs on. Carries the `decisions.md` row-97 amendment (already landed): the lawn does not **queue or sequence** actions; a lawn action is **activated**, not scheduled | — |
| **E34** | `trigger-vocabulary` | New host event families → atom triggers. `EffectEventAdapterCore.TryMap` maps exactly **five** today. Adds `onWave`, `onMatchStart`/`onMatchEnd`, `onSunCollect`, `onGridPlace`. `06-unsourced.md` / `07-effect-opportunities.md` already class `onWave` and `onMindControl` **PROBE** and `onHitLand` **NOT SHIPPED** — consume that, do not re-derive it | — |
| **E35** | `match-modify` | A new kind **on a new attach point** (`Match`) — none of the five existing points is a match. `Board.config`: zombie HP/damage/speed/count multipliers, starting armor, plant/zombie modify bands, `waveInterval`, `conveyInterval`. `CheatActions.ApplyBoardConfig` already writes it, reachable only from cheat state. **The entire "curse this level" axis** | E34 |
| **E36** | `wave-control` | Summon a wave, huge wave, set/freeze the wave timer. **Needs both halves** — a kind *and* E34's `onWave` | E34, E35 |
| **E37** | `projectile-control` | `Bullet.Damage` on fired **and** spawned bullets, homing, type swap, `moveWay`. `spawn.entity` can create a bullet and **cannot say how hard it hits**; `DebugActions.SpawnBullet` already reads `damage`/`y`/`moveWay` | E28 |
| **E38** | `entity-fields-12plus` | Primary channels **12+** — `takeDmgMultiplier` (the *"takes +X% damage"* knob), `theArmor`, `theSpeed`/`theOriginSpeed`, `attackSpeedAdder`, attack/produce countdowns, plant `theShieldHealth`, `theLevel`/`shootingLevel`. All injector-writable today. **The same channel-extension shape E16 already ran once** for 8 → 11 | E30 |
| **E39** | `plant-side-status` | Widen `ExecApplyStatus`, which iterates `FindObjectsOfType<Zombie>()` only, so **half the board cannot be statused**. Battle's path is already ptr-generic — this is a lawn-only asymmetry, not a vocabulary change | E28 |
| **E40** | `spawn-non-grid` | Pets, buckets, presents, coins, mowers. `grid.spawn` covers `GridItemType` only (12 values) | E28 |
| **E41** | `ui-attach-point` | A new **read-only** attach point: show a number, flash a banner, toggle a health bar. There is no UI attach point of any kind. A HUD **shows** state, never owns it | — |

## 14. Dependency graph and build order

```
WAVE 7  (all model-free)
  E42 units-correction ─► (E30, E38 may author magnitudes)
  E26 ─┐
  E27 ─┼─ independent, any order
  E28 ─┤
  E29 ─┘
       └─► E30 channel-pool ─┬─► E32 affix-import-path
                              └─► E43 family-expand (also needs E42)

WAVE 8
  E33 activation-edge      (independent — the action corpus's only Wave 8 blocker)
  E41 ui-attach-point      (independent)
  E34 trigger-vocabulary ─► E35 match-modify ─► E36 wave-control
  E28 ─► E37 · E39 · E40
  E30 ─► E38
```

**Build order, and the reason for it:** `E26 · E27 · E28 · E29` first — four independent wiring fixes,
each of which makes a currently-silent failure loud. Then `E30`, which needs E28's params to exist and
E29's guard to refuse a bad pool. Then `E32`. **`E33` may run at any point and should run early**,
because `A9 movement-actions` is blocked on it and nothing else is.

## 15. Checkpoints

- **✅ Checkpoint G — the silent failures are loud.** E26/E28/E29 land. A runner atom no longer throws at
  grant time; a declared param either works or is refused at load; an unknown value in **any** kind is a
  load-time refusal, not a silent no-op. **Proof: a planted violation of each fails a test.**
- **✅ Checkpoint H — the element axis is live on the lawn.** E27 lands. A species with
  `elementPrimary: "fire"` resolves non-`Neutral` in `CombatActorSnapshot` on the lawn. **⚠️ Coordinate:
  this moves the visual baseline the open VFX blind-identity trials score against, and the open shield
  live-proof reads the same `ResolveActor`. Run those before E27 or after — never straddling.**
- **✅ Checkpoint I — one row, many outcomes.** E30 lands. **An atom that names a channel pool resolves
  to a different concrete channel across two roll seeds and to the same one on replay**, and it prices
  without a concrete channel. This is the checkpoint that proves the four-layer model, and it is the one
  worth failing the wave over.
- **✅ Checkpoint J — an authored affix is loadable.** E32 lands. A `"kind": "affix"` file under
  `data/seed/effects/` imports, and a container `pool` referencing it rolls.
- **✅ Checkpoint K — an action fires on the lawn.** E33 lands. `OnActivate` is raised, and a movement
  action's payload applies. **Unblocks `A9`.**

## 16. Cross-program hazards

| Hazard | Detail |
|---|---|
| **effect-pipeline overlap** | §W7.11.1's seam table is normative. E30 owns the pool **contract**; modules 1+2 own the **slot declaration and resolver**. E30 must not implement a resolver |
| **VFX / shield live proofs vs E27** | Both open, both read the element path E27 rewires. Sequence them |
| **battle-timeline B25/B26 vs E27/E28** | B26 freezes shield + DoT behaviour while E27/E28 edit the same `EffectRuntime` drain chain, and **the injector is not built by CI**. This is `effect-atom-map.md` §6's own H1 hazard recurring |
| **CI gates that fail on the first generated row** | `EffectAtomCatalogGeneratedTests` asserts exactly **16 ids**; `EffectCatalogExecutionParityTests` asserts `Assert.Empty(compiled.Runtime)` — which **E26 deliberately violates**; `ElementEnumGen` globs `fx-*.json` **AllDirectories**. Each needs a named change, not a rename to dodge it |
| **`AtomImporter` staleness trap** | Reports *"nothing changed"* when only compiler **code** changed, because the hash covers seed data — and **E26 is exactly a compiler-code change** |
| **Stale instances** | Any `catalog_revision` bump makes every previously rolled `effect_instance` unbindable (`StaleInstance`). Pre-existing for any content change; state it in the rollout note |
| **`definitions.md` §2 units** | Still carries the row the item program corrected on 2026-08-22 (`combat.power.*` etc. are **flat game units**, not resolver points). `DESIGN-GATE.md` makes that file win over any spec, and **E30/E38 author magnitudes from it** |

## 17. What stays out

- **Sim runtime.** `stat.derived` and `shield.grant` stay `RuntimeState.None` — `SimEffectHost` has no
  consumer, and *"flipping it on the strength of the other two would re-create the quarantine's cause."*
- **Generation itself.** effect-pipeline owns atoms→affixes, binding production and the authoring run.
- **Product-OUT surface.** `Time.timeScale`, plant-anywhere, free `SetPlant`, auto-collect, card/tool
  cooldowns. **Several are policy, not backlog** — say which before designing against any of them.
- **Host-side NOT SHIPPED.** Fog, scene weather, ice trail. Not an atom gap.
- **Fusion / mix.** Host is CAPTURE-only — a joint gap, not an atom gap.

## 18. Success criteria

1. **Every kind refuses a bad value at load.** No silent no-op survives in any of the 12.
2. **A pooled atom resolves, replays identically, and prices.** Checkpoint I.
3. **The element axis is live on both runtimes**, not battle-only.
4. **`OnActivate` fires on the lawn**, so an action is a thing that can happen there.
5. **No module authored twice.** The seam table holds; no Wave 7 or 8 module reimplements an
   effect-pipeline one.
6. **Every generation-adjacent module ships `--dry-run` and a small `--count`** (§W7.10) — a full run is
   an owner decision behind a quality gate, never a step a plan schedules.

## 19. Filed by the party-dungeon program (2026-09-05, specs approved or in wave 3)

Two rows the delve modules need from this program; each is one reviewed change here, consumed there.

| Ask | Filed by | Shape | Until it lands |
|---|---|---|---|
| `InstanceOrigin.Delve` | `party-dungeon/spec-event-deck.md` §5 | a new member so an event's frozen instance says where it came from | v1 reads `Drop` with the binding source (`delve:{delveId}`) carrying scope |
| `Freeze` leaves count-unit channels unscaled | `party-dungeon/spec-unique-pipeline.md` §4 | `Instantiator.Freeze` (`Instantiator.cs:313-315`) applies `ContentScale.Apply` to every fixed value; `Apply(1, 4235)` is 4, and a `stat.derived` on `loadout.slots` (a count) must stay 1 — one guard reading the channel's `UnitClass`, tested by *"frozen at Θ 100, still 1"* | the unique build refuses `unique.slot-scaled` rather than ship five extra slots |
| `status.clear` admitted on `OnActivate` | `party-dungeon/spec-supplies-and-objects.md` §3 | one trigger row in `AtomKindRegistry` (today `Events`-only, `AtomKindRegistry.cs:638-646`) so an antidote supply can fire | the supply validator refuses such a row at import (`consumable.trigger-not-allowed`) |

