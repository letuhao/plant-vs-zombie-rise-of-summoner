# Effect atoms — capability map

**Status:** **Capability map (2026-08-22)** — module ids, dependency direction, build order, and cross-program hazards. **19 spec files covering 20 module ids are written; E1 is built and accepted.** Everything from E2 onward is specced and unbuilt. Source of truth for intent: [effect-atom-ideal.md](effect-atom-ideal.md), whose §13 closes with *"Every question raised in this document is now decided, defaulted, or explicitly scoped to a later spec. The capability map can start."*

Prefix: `effect-atom`. Module specs at `docs/architecture/effect-atom/spec-<module-id>.md`; plan and tasks at `tasks/effect-atom-plan.md` / `tasks/effect-atom-todo.md` (AGENTS.md parallel-programs convention).

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
| **E6** | `instance-and-binding` | `effect_instance` / `effect_instance_atom` (frozen moment-2 rolls, `roll_seed`, power at roll time) and `effect_binding`, which replaces the logical `foundation_effect_grant` and absorbs today's `mods_json` grant blobs. Runtime state (ICD clocks, stacks, status instances) stays in session RAM — **no new durable runtime table**. | E5 |
| **E13** | `runtime-form-benchmark` | The build-time benchmark the ideal explicitly refused to decide on paper: typed object graph versus flattened non-recursive encoding, **against real content**. Settled already: no dictionaries and no string comparison on the per-hit path. *(The no-recursion law was withdrawn — the 7 ns winner recurses.)* | E4, E2, E3 |
| **E7** | `atom-compiler` | Binding → Foundation grant / damage rider / status apply. Compiles at load into the form E13 picks. **Server compiles and pushes compiled output**; the injector never holds content rows. | E6, E13, E1 |
| **E8** | `content-hash` | **BUILT.** A hash over atom / container / container_atom / container_pool / curve / rarity rows — never instances. Sort-then-concatenate, columns length-prefixed, covered set held in a **versioned registry** (`contentHashSchemaVersion`, v1 = the six tables that exist). The stamp carries per-table digests so a version bump can still compare the tables both versions share. Its consumer today is the boot sweep's replay refusal; stamping into the report is **E12**'s, because a new stamped field *is* a golden diff. | E4, E5 |

### Wave 3 — the proof

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E11** | `effect-def-migration` | **BUILT 2026-08-22 — Checkpoint D reached.** Falsified the schema and the compiler in seven places, every one invisible until real content ran through: the compiled def id was `atom.atom.*`; a compiled `stat.modify` reached FA1 as none of `flat`/`increased`/`more` and applied a flat **zero**; a three-trigger group emitted three copies of one action; `icd_key`'s grammar forbade the dots a migrated id needs; `status.clear` named a key nothing reads; `resource.delta` did not declare `channel`; and D10's overlay-or-param magnitude check did not exist. The 16 `EffectSeedCatalog` defs become rows, and the **49 existing JSON fixtures** (19 `effect-*`, 5 `combat-*`, 25 `status-*`, plus 15 golden plans) — already the data format — become the test corpus. The cheapest migration, against effects Foundation executes today. **If the schema is wrong, this is where it shows.** | E7, E8, E14a |
| **E14a** | `importer` | **BUILT 2026-08-22.** Seed file format (`AtomSeedFile`, Core), the import transaction (`RpgStore.ImportContent`, data — it needs the store's private connection and gate, and `guard-dal.ps1` scans only `src/`), and `tools/AtomImporter` — arguments, a report, and `SeedScanner`, covered by a new `tests/FusionRpg.AtomImporter.Tests` in CI. Four folders, not two: curves and rarity bands are hashed content tables too. **Validate-all-then-write in one transaction**, cross-file duplicates refused naming both files, and the `catalog_revision` bump once per transaction **and only when something changed**. **Sequenced before E11**, which cannot load its own seed rows without it. | E5, E8 |
| **E14b** | `content-validation` | Seed and migration files; the budget check as **content validation** that fails over its ceiling (never as a generation mechanism); the power-drift test; bind-time runtime-support validation. | E11, E9 |

### Wave 4 — power

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E9** | `power-vector` | The SSOT: category vector (`offense · survivability · control · utility · economy`), the cost function `coeff × normalize(magnitude, referenceScale) × conditionality`, the hand-authored coefficient table, computed base plus stored override with a required note, and spawn-power recursion at **depth 1, memoized**. **Registers `power_coefficient` and `power_trigger_frequency` with E8 and bumps `ContentHashRegistry.CurrentSchemaVersion` to 3** — the proposal side table stays uncovered, being a sweep artefact. | E4, E2 |
| **E10** | `power-reads` | The three projections over one price: **display scalar** (geometric mean, humans only), **matchup-conditioned** (vector × element ring — impossible to retrofit onto a stored scalar), and **marginal** (`vector(with) − vector(without)`, which captures multiplicative pairs by construction). The AI contract: AI reads vector and matchup, **never** the display scalar. | E9, E18 |

### Wave 5 — the expensive migration

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E12** | `trait-migration` | **One** trait becomes a container of atoms: `critical-hunter`, via `stat.derived` ChannelMods at squad build — a path battle already runs. The other **13** do not move: `regenerator` and `soul-eater` need event dispatch battle does not have, `berserker`/`guardian` need kinds that would break the 12-kind ceiling, `swift` is the turn kernel's, and the 7 engine behaviours are the AI and rewards layers wearing an effect costume. **Touches `RulesetVersion` and needs a golden re-bless.** | E11, E14b |

### Added by the gap-clearing round (2026-08-22)

*(A provenance heading, **not** a build wave — build positions are in §4.)*

Four owner decisions on [effect-atom/atom-family-library.md](effect-atom/atom-family-library.md) §4–§5 add these. **E15 is not optional** — the map as written had no runtime home for per-binding state, so predicate-tree atoms and `capPerMatch` had nowhere to live.

| id | Name | Owns | Depends on |
|---|---|---|---|
| **E15** | `atom-runner` | **BUILT.** **The Secondary effect runner — the missing runtime half of E7.** E7 compiles what Foundation can already express (FT* trigger + simple filters → an ordinary grant, zero runtime cost). E15 runs what it cannot: per-binding state (cooldowns, counters, charges), predicate-tree evaluation, and **`capPerMatch` counters** — the economy cap that is in the FA9 allowlist today with no implementation anywhere. Hot: Core on the injector game thread, dispatching via `Funnel.Enqueue` only, never awaiting the server. | E7, E13, E3, E2 |
| **E16** | `channel-extension` | `attackInterval`, `produceInterval`, `zombieSpeed` promoted from cheat-document keys to real composed channels (8 → 11), so fire rate, sun rate, and creep speed become authorable. A composer case and a Writer case each, plus a guard test that the extras path stops writing them behind compose's back. **Sequenced after the atom layer** — it touches the sealed stat path. | E11, E9, E14b |
| **E17** | `status-payload-completion` | Wire the **three** missing Unity CC branches (`ember`, `jala`, `kelp` — the vanilla methods exist, we never called them); `charm_pulse` is a **def error**, not missing wiring, because no vanilla method exists at all, implement a `StatusPayloadKind.ModifyStat` consumer (`rally`, `expose`, `command`, `shatter`), and finish `leech`'s heal half. Turns 13 functional statuses into 21. **Cross-stream:** this is status-system and injector work, and `StatusCatalog` is ADR-locked code-first — it needs the status stream's agreement, not just ours. | E11 |
| **E18** | `element-roster-data` | **BUILT 2026-08-22.** The roster and both matchup matrices moved from hardcoded enum and `switch` to rows read through `ElementTable`: `effect_element`, `effect_element_matrix_combat`, `effect_element_matrix_shield`. **Two tables — but NOT because they differ.** The claimed light/dark asymmetry does not exist: compared exhaustively they are identical across all 36 pairs, light ⇄ dark mutually strong in both. They stay separate because the shield spec makes them independently editable. Channel generation reads the roster table, so a seventh element generates its 12 channels with no code change. Enum generator still owed. E8 registry at **v2**. Derived channel ids stay *generated* from families × roster, so a seventh element needs no new consumer. Owns ordinal stability, the `families × (roster + omni)` replacement for the hardcoded 84-count test, and extending the content hash to cover the roster. **Registers its three tables with E8 and bumps `ContentHashRegistry.CurrentSchemaVersion` to 2.** | E4, E8 |
| **E19** | `compiled-push` | Server → injector delivery of compiled grants and runner entries: extends `effects.grants.apply` on Hello, negotiates against `catalog_revision`, and holds the guarantee that the injector carries **no content rows**. Without it E7's only consumer is tests. | E7, E8, E15 |

**Also folded into existing modules:**

- **E1** carries the **code-or-data rule** in its spec: *a thing can be data if adding a row changes behaviour without new code; if a new row needs a new consumer, it must be code.* The 12 derived channel **families stay code** because each has a named reader — a thirteenth added as a row would be `status.expose.*` all over again: legal, registered, zero readers, dead.
- **E9** stores coefficients as **data** (`power_coefficient`) with a sweep-written **proposal** side table and a drift-reporting test, so "hand-authored now, fitted later" is mechanically possible instead of aspirational.
- Per-channel policy (compose kind, default, cap) becomes data too, removing today's duplication between the constants file and `DerivedStatRegistry`.

**Also folded into an existing module:** E6 gains two owner-key scopes, `sector:{id}` and `slot:{id}`, so world buildings and sector environments can bind. The trigger list stays at 7 — `OnWorldTick` / `OnSectorEnter` / `OnBuildComplete` belong to the world spec that needs them.

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
- **✅ Checkpoint D — the schema is proven.** E11: all 16 defs are rows, the **49** fixtures pass unchanged, `EffectSeedCatalog` is deleted. **The claim "a new effect costs one row" is either true here or the design failed.**
- **⛔ Checkpoint E — goldens move.** E12 only. A re-bless with a written predicted delta and a win-rate sweep, needing owner sign-off — and it must not collide with the timeline gate (§6).

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
