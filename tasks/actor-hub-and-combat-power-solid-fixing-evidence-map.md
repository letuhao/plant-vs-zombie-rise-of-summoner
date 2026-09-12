# Evidence map: actor-hub-and-combat-power-solid-fixing

**Plan / todo:** [plan](actor-hub-and-combat-power-solid-fixing-plan.md) · [todo](actor-hub-and-combat-power-solid-fixing-todo.md)
**Runbook:** [runbook](actor-hub-and-combat-power-solid-fixing-runbook.md)
**Purpose:** one row per acceptance criterion → the command that proves it → that command's
**executed** result → the artifact it produced. The ledger, not a summary, is the proof of done.

**Status:** T1–T6 rows `PASS` (T1-T5 executed 2026-09-12, T6 executed 2026-09-13, in worktree `solid-run-20260912-eb53`); T7 onward `PENDING`.

> Rules for this file:
> - A row is `PASS` only if the command was run in the cycle that claims it, with a captured exit
>   code / tail. `N/A` needs a one-clause reason. A documented-but-unfixed defect is `FAIL`, never
>   "known".
> - Only `main` edits this file. Fan-out worktrees write `tasks/evidence-fragments/<task-id>.md`;
>   the rows are folded here at integration (see runbook §5).
> - Rows are `PENDING` until their command exists and was executed.

Legend: `PENDING` · `PASS` · `FAIL` · `N/A`

---

## Wave 1a — ChannelMods + Cold equip

### T1 — Star / Loyalty ChannelMods → Hub · spec `channelmods-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 1.1 | Star + Loyalty channels contribute via Hub/atoms (or DEBT shim, deleted in fuse) | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0, `ACTOR-HUB GUARD OK`; `StarLoyaltySubsystem` registered in `ActorHub.CreateDefault` (`src/FusionRpg.Core/Stats/Derived/ActorHub.cs:158`); adapters carry `// DEBT — channelmods-hub` (`src/FusionRpg.Server/WebMatchService.cs:579-584,596-601`) | `tests/FusionRpg.Server.Tests/StarLoyaltyHubParityTests.cs` |
| 1.2 | Parity fixture: channel totals match pre-migration for same star/loyalty/level | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Star\|Loyalty"` | PASS — 157/157; plus Server filter 43/43 incl. 3 `StarLoyaltyHubParityTests` facts (historical-arithmetic pin + Hub totals + SourceIds + inert case) | `tests/FusionRpg.Server.Tests/StarLoyaltyHubParityTests.cs` |
| 1.3 | ChannelMods allowlist no longer needs these producers (or lists only shim) | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0; `WebMatchService.cs` remains the listed producer = the tagged one-release shim, no other Star/Loyalty producer | `scripts/guard-actor-hub.ps1` (allowlist L111-121) |
| 1.4 | Server channel tests green (if applicable) | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ChannelMods\|Star\|Loyalty"` | PASS — 43/43, 19s | — |
| 1.5 | REGRESSION + discovered defects (program-end debt) | full `FusionRpg.Core.Tests` + full `FusionRpg.Server.Tests` | Core 13322/13361 (39 FAIL, none in Star/Loyalty/Hub area — ClassSystem emit, SpeciesGen, Balance, Adoption goldens, Items corpus, QualityReport); `BaseTypeCorpusTests…disjoint` re-run on stashed (pre-change) tree: still FAIL → pre-existing. Server 403/404; `DelveBattleSessionManagerTests…zero_replayCount` passes solo with AND without the change → order-dependent flake, not T1 | — |
| 1.6 | Ask-first default applied (non-blocking follow-up) | — | SourceId family reuse: `grant:star:{n}` / `grant:loyalty:{n}` via GG-49 `Grant`, no new family (plan default); dedicated family remains tracked follow-up | `StarLoyaltySubsystem.cs:96,103` |

### T2 — aptitude / Zomboss / draught / injury / kit → Hub · spec `channelmods-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 2.1 | UniqueCreature aptitude, Zomboss, draught, expedition injury, boss kit contribute via Hub/atoms | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude\|Draught\|Expedition\|BossBuild\|Zomboss"` | PASS — 373/376; the 3 FAIL are pre-existing `ProveAptitudeJsonEmitTests` (ClassSystem JSON emit, in-filter by name only; identical names in the T1 clean-tree baseline). Aptitude family via `AptitudeSubsystem`/`Resolve`; new `DraughtSubsystem` (`rpg.draught`) + `ExpeditionInjurySubsystem` (`rpg.expedition.injury`) with `CreateDefault` opt-in arms; kit/zomboss via `Resolve` over the same pattern allocation | `tests/FusionRpg.Core.Tests/Stats/ChannelModsHubParityTests.cs` (7 facts) |
| 2.2 | Species aptitude via same Hub aptitude path, or proven unused/deleted | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude"` | PASS (in 2.1 run) — proven unused: zero production callers of `AptitudeChannelMods` (src grep: definition + `UniqueCreatureAptitudeChannelMods` call + comments only; all callers in `AptitudeChannelModsTests`); `// DEBT — channelmods-hub` tag added, delete-or-wire at fuse T6 | `src/FusionRpg.Server/WebMatchService.cs:629-638` |
| 2.3 | Full-set parity fixtures (Zomboss, draught, injury, boss kit, UniqueCreature aptitude) | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude\|Draught\|Expedition\|BossBuild\|Zomboss"` | PASS — 7/7 `ChannelModsHubParityTests`: aptitude Resolve-vs-ResolveForBattle, kit-vs-ResolveKit, zomboss-concat-vs-Resolve, draught twin-vs-Apply, injury twin-vs-historical-expression, both subsystems through composed Hub totals with `grant:` SourceIds | `tests/FusionRpg.Core.Tests/Stats/ChannelModsHubParityTests.cs` |
| 2.4 | No production path requires `BattleChannelMod` for these after fuse (shim OK until T6) | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0; all six producers (`UniqueCreatureAptitudeChannelMods`, `AptitudeChannelMods`, `ApplyZombossPattern`, `DraughtProjection.Apply`, `ApplyInjuries`, `BossBuild.ResolveKit`/`ApplyKit`) carry `// DEBT — channelmods-hub` one-release-shim tags; battle behavior unchanged until fuse | — |
| 2.5 | Server `ChannelMods\|Aptitude\|BuildSquad` green | `dotnet test tests/FusionRpg.Server.Tests` | PASS — full suite 404/404 (3m41s); filtered `ChannelMods\|Aptitude\|BuildSquad` 42/42. The earlier solo Delve flake passed in-suite this run → order-dependent, not T2 | — |
| 2.6 | REGRESSION | full `FusionRpg.Core.Tests` | 39 FAIL — byte-identical set to the T1 clean-tree baseline (ClassSystem emit, SpeciesGen, Balance, Adoption goldens, Items corpus, QualityReport); zero new failures; every T2-area test green | — |
| 2.7 | Ask-first defaults applied (non-blocking follow-ups) | — | (a) SourceId family reuse: `grant:draught:{containerId}`, `grant:injury:{actorKey}` via GG-49 `Grant`, no new family. (b) Species `AptitudeChannelMods`: keep-as-shim vs delete decided at fuse T6 | — |

### T3 — Sole Cold equip path = rolled / atom bindings · spec `cold-equip-one`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 3.1 | Documented sole Cold materialize path is rolled/atom bindings | `rg -n "rolled\|EquippedBoundAtoms" src` + doc read | PASS — 15 code hits, all on one path: `MaterializeRolledEquipRuntime` (rolled deploy) + `EquippedBoundAtoms` (shared battle/sheet reader) + `UpsertUniqueEquipment` single rebuild; `UniqueActorService.PutEquipment` doc rewritten to rolled/atom reality (stub-catalog line removed) | `src/FusionRpg.Server/UniqueActorService.cs:39-48` |
| 3.2 | Equip → Hub Derived proves change **without** BattleStatComposer | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom\|EquippedBound"` | PASS — Core 2/2 + new `EquippedHubParityTests.Rolled_flat_stat_derived_reaches_hub_snapshot_without_composer` (Server): rolled equip → Hub `combat.power.fire` 40.0, no composer reference in the loop | `tests/FusionRpg.Server.Tests/EquippedHubParityTests.cs` |
| 3.3 | No third equip fold introduced | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0; `ModsFor`/`DerivedAtomsFor` share one `EquippedDerived` parse (documented no-third-projection); `EquippedBoundAtoms` remains the reader fuse uses | `src/FusionRpg.Core/Battle/EquipAtomSource.cs:87-96` |
| 3.4 | SourceIds use `equip:{role}:{itemRef}` (GG-49) on sheet | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom\|EquippedBound"` | PASS — Core `EquipAtomSourceIdTests` 2/2 + end-to-end `equip:` prefix and instance-id suffix asserted through store→Hub (`Rolled_flat_...`) | `tests/FusionRpg.Server.Tests/EquippedHubParityTests.cs` |
| 3.5 | Single rebuild: no dual `mods_json` SSOT beside atoms | `.\scripts\guard-single-writer.ps1` | PASS — exit 0; `UpsertUniqueEquipment` runs `RebuildUniqueModsFromEquipment` + `ReconcileUniqueEquipmentAtomBindings` in one lock scope (`RpgStore.UniqueActors.cs:1439-1440`); idempotent reconcile pinned by existing re-equip test; atom-backed grants absent from `mods_json` (existing E2E/Data pins) | — |
| 3.6 | Rolled (`ref_kind = rolled`) equip produces Hub-visible `stat.derived`, ops honored | `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment\|Equipped\|AtomBinding"` | PASS — Data 8/8 + new `Rolled_increased_op_is_honored_on_hub_path_not_coerced_to_flat` (Server): rolled Increased 250 on SumIncreased `status.power.omni` composes to exactly 250.0 on Hub (op parsed via `TryParseOp`, never coerced); battle `ModsFor` keeps its documented additive fold (T7 owns it) | `tests/FusionRpg.Server.Tests/EquippedHubParityTests.cs` |

### T4 — Retire stub catalog as production SSOT · spec `cold-equip-one`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 4.1 | Stub `Items` deleted or test-only with DEBT + no production caller | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | HONEST GAP — not deleted: `// DEBT stub — cold-equip-one` tag added; the dict stays as item-id allowlist because the deletion precondition is absent (no rolled-grant source for stub definitions; catalog's own doc). Remaining src hits: the DEBT-tagged dict + 2 doc comments (RelicCatalog) — zero equip-logic consumers of stub templates for magnitudes | `src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:37-52` |
| 4.2 | Player equip/unequip does not depend on stub templates | `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment"` | PASS — Data 8/8: bound magnitudes come from seeded containers (`The_bound_instance_carries_the_atoms_own_real_stat...` pins atom id + amount 10 from `fx-core.json`, not the stub DTO); atom-backed grants absent from `mods_json`; stale `RpgStore` comment claiming otherwise corrected | `tests/FusionRpg.Data.Tests/UniqueEquipmentAtomBindingTests.cs` |
| 4.3 | Player equip API does not default to stub catalog rows | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PASS — no creation-time equip path exists (only explicit `UpsertUniqueEquipment` via `UniqueActorService.PutEquipment`); new `Fresh_actor_has_no_assignments_bindings_or_hub_derived` pins a fresh actor bare on all three reads | `tests/FusionRpg.Server.Tests/EquippedHubParityTests.cs` |

### Checkpoint: Wave 1a

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP1a.1 | ChannelMods combat writers migrated or shimmed with DEBT | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0; T1 (star/loyalty) + T2 (aptitude/zomboss/draught/injury/kit) Hub twins + parity; all battle shims `// DEBT — channelmods-hub`-tagged for T6 | — |
| CP1a.2 | ChannelMods allowlist only DEBT shims (or empty) | `.\scripts\guard-actor-hub.ps1` | PASS — exit 0; allowlist producers are exactly the tagged shims (`WebMatchService`, `AptitudeResolver`, `ExpeditionResolver`, `DraughtProjection`, atom sources) | `scripts/guard-actor-hub.ps1` L111-121 |
| CP1a.3 | Cold equip path is atom/rolled; stub not SSOT | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PASS — sole path proven end-to-end (T3 tests); stub = DEBT-tagged allowlist, magnitudes from containers (4.1 honest gap recorded) | — |
| CP1a.4 | Guard + focused tests green | `.\scripts\guard-actor-hub.ps1` + filters above | PASS — guard-actor-hub OK, guard-single-writer OK; Core Star\|Loyalty 157/157, T2 filter 373/376 (3 pre-existing), Server ChannelMods\|Star\|Loyalty 43/43, Server full 404/404, Data equip 8/8, Core equip 2/2, Server equip 7/7 | — |

---

## Wave 1b — Fuse + ops

### T5 — BattleEngine compose via ActorHub · spec `battle-hub-fuse`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 5.1 | `BattleEngine` reads Hub Derived only (no Compose call) | `rg -n "BattleStatComposer" src` | PASS — zero `BattleStatComposer.Compose(` calls under `src/` (even the grandfathered engine call is gone); `ActorState` composes via `BattleHubCompose` (`BattleEngine.cs:38-42`) | — |
| 5.2 | Bound vs empire aptitude identity matches `UniqueActorHubCompose` rules | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"` | PASS — squad inputs merge commander + UniqueCreature allocation, never the species fallback (BuildSquad); filter 1299/1305 with only the 6 pre-existing adoption failures | — |
| 5.3 | Delve/siege/web paths inherit Hub | `dotnet test tests/FusionRpg.Server.Tests` | PASS — re-run 2026-09-12 (handoff session): full suite 407/407, the previously-noted order-dependent Delve flake did not reproduce this run. Delve/siege are pass-throughs with no producer concats of their own (verified by code) | — |
| 5.4 | Baseline flats / tempo / resources via Hub; seed parity documented | Compose↔Hub parity fixtures | PASS — new `BattleBaseline`/`BattleAffinity`/`BattleTrait`/`BattleTempo` subsystems (re-home only) + existing `ResourceBaselineSubsystem`; default-neutralized seeds (turn.speed doubling found + fixed); `rpg.battle.base` const mirrors `rpg.resource.base` | `tests/FusionRpg.Core.Tests/Battle/BattleHubComposeParityTests.cs` |
| 5.5 | Pre-delete parity matrix green channel-for-channel | Compose↔Hub parity fixtures | PASS — 4/4: exact on battle channels, narrowing-bounded on funded aptitude channels, 3 adopted op-divergences pinned (`status.resist.{dot,cc,contagion}` → capped 0.95; old raw 73/30/43), Hub-extra == pure defaults | `tests/FusionRpg.Core.Tests/Battle/BattleHubComposeParityTests.cs` |
| 5.6 | `AptitudeResolver.ResolveForBattle` retired or reduced to Hub-only | `rg -n "ResolveForBattle" src` | PENDING → T6 — remaining refs are the DEBT-tagged shims (`BossBuild.ResolveKit`, `AptitudeChannelMods`, `UniqueCreatureAptitudeChannelMods`) + definition + 1 doc comment; all deleted with the composer | — |
| 5.7 | Test-hub hygiene lesson (process) | — | My parity class's 6-hub static-ctor reconfigure flapped 10 unrelated battle/timeline tests (different content vs assembly bootstrap). Fixed per repo convention: `[Collection("AptitudeTuningHub")]` + instance-ctor Configure of that hub only + ambient values elsewhere. No production impact | — |
| 5.8 | Handoff re-verification (2026-09-12, new session) | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"` | Re-ran cold on the uncommitted tree before trusting 5.1-5.7: got 9 FAIL not 6. Triaged each: 3 are `ChannelModsHubParityTests` (Aptitude/Kit/Zomboss) — intentional RED placeholders for T6 (`Assert.Equal(empty, hub)` literal, comment says "captured pre-delete... Hub drift still fails loudly here" — correct to stay red until T6 fills the real literal). 1 is `EquipRuntimeTests.An_equipped_item_changes_a_battle_number` — intentional RED for T7 (equip atoms already wire into `BattleHubCompose` via `BoundAtoms`→`AtomDerivedSubsystem`, but op-handling isn't finished, exactly T7's stated gap). 1 was a **real test bug**, unrelated to any task: `BattleHubComposeTests.ATurnDotChannelModThroughTheComposePathDoesNotThrow` assumed `turn.haste` starts at implicit 0, but `DerivedStatRegistry` registers its default as `NominalHasteMilli` (1000) — same constant `BattleEngine.cs`/`BattleDurationResolver.cs` already fall back to. Fixed the test's expected value + comment; re-ran, now 8 FAIL = 6 pre-existing + the 2 accounted-for RED-for-T6/T7 rows above. Guard `guard-actor-hub.ps1` green; `FusionRpg.Server.Tests` 407/407 (see 5.3 update). Committing T1-T5 on this basis | `tests/FusionRpg.Core.Tests/Battle/BattleHubComposeTests.cs` |

### T6 — Delete BattleStatComposer + RulesetVersion bump · spec `battle-hub-fuse`

> Bump and golden re-bless are **already locked by the approved plan** (T6 runs them; no fresh
> approval step).

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 6.1 | No production `BattleStatComposer.Compose` under `src/` | `rg -n "BattleStatComposer" src` | PASS — zero hits; the whole class (`src/FusionRpg.Core/Battle/BattleStatComposer.cs`) is deleted, a stronger guarantee than a call-site-only check | — |
| 6.2 | Guard updated; ChannelMods allowlist empty (or justified) | `.\scripts\guard-actor-hub.ps1` (manually re-derived — see note) | PASS — this session's sandbox refuses `powershell -File` from a worktree-isolated session ("git operations must target its own worktree" false-positive on a read-only static-analysis script). Verified every guard condition by hand instead: `$allowComposer`'s `BattleStatComposer.cs` entry removed (file gone); the whole "Production BattleStatComposer.Compose call sites" section removed (pattern can never match again); `$allowChannelModProducers` trimmed 7→3 files. `rg -n "new BattleChannelMod\(" src` confirms **exactly** `EquipAtomSource.cs`/`TraitAtomSource.cs`/`TreeAtomSource.cs` — the 3 files still feeding Hub subsystems their `BattleChannelMod`-shaped rows (`BattleTraitSubsystem` etc. call `.ModsFor` and convert). `Program.cs`'s `UseEquipment` guard check now matches nothing (string deleted, harmless). No other `*Composer*.cs` file changed | `scripts/guard-actor-hub.ps1` |
| 6.3 | One RulesetVersion bump + triage notes; unrelated goldens frozen | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden"` | PASS — `BattleRuleset.RulesetVersion` 4→5 (`BattleModels.cs`) with a triage-note doc comment matching every prior bump's own convention. 3 hardcoded `Assert.Equal(4, ...RulesetVersion)` literals fixed to 5 (`BattleAdoptionTests.Retired_symbols_stay_retired`, `BattleShieldTests`, `ModeProfileCapabilityTests.The_ruleset_version_is_unchanged_by_adding_a_fifth_profile`); every other `RulesetVersion` reference in tests reads the symbolic constant (auto-updates) or is `TurnEngine.RulesetVersion` (an unrelated, untouched world-map versioning scheme). Triaged full `Core.Tests` before AND after the bump (see 6.6): the delta is exactly the 2 `BattleGoldenTests` (re-blessed) plus the 1 `BossBuildTests` kit-carrier test (fixed) — the other 38 residual failures are byte-identical to the clean pre-T6 baseline, confirmed by stashing all T6 work and re-running each named cluster against commit `3689f35b` | — |
| 6.4 | Docs mark dual-compose debt retired | `rg -n "dual.compose\|composers stay separate\|locked-separate" docs/architecture/decisions.md docs/architecture/actor-hub-ssot.md` | PASS — `decisions.md`'s "ActorHub sole Hot compose gate" row gets a `✅ Fused 2026-09-13` addendum; its "Combat power number" row's dual-compose line updated to say fused; `actor-hub-ssot.md` §8.3 heading and body rewritten from "debt" framing to "retired — fused" with the concrete mechanism (guard allowlist trim, compile-error guarantee). The other 8 files `rg` also names (specs under this program, `class-system-map.md`, `combat-power-number-ideal.md`, etc.) are explicitly T18's own named sweep ("final polish may wait T18" per this task's own acceptance line) — not touched here, not scope creep | `docs/architecture/decisions.md`, `docs/architecture/actor-hub-ssot.md` |
| 6.5 | T5 parity remains green before delete | Compose↔Hub parity fixtures | PASS — T5's own evidence (5.4/5.5, this evidence map) recorded 4/4 green on `BattleHubComposeParityTests.cs` immediately before this task started, and the commit (`3689f35b`) carrying that green state was the base for every edit in this task. The file itself is deleted here: its sole purpose (proving Hub == old composer before the composer is removed) is satisfied and historical the moment the composer is gone, matching `BattleStatComposerTests.cs`'s own T5 precedent (also deleted, same reasoning) | — |
| 6.6 | Goldens re-blessed once | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition\|Golden\|Battle"` | PASS — `BattleGoldenTests` (4 hashes: Stomp/Close/Wipe/SeedSweep) re-blessed; `Golden_outcomes_hold_their_shapes` stayed green untouched (stomp Victory, wipe Defeat+retreat — shapes held, only the RulesetVersion-stamped hash moved). `ExpeditionResolverTests.Tier_goldens_are_locked` (hunt tier) was investigated and confirmed **pre-existing** — fails identically on the clean, already-committed T1-T5 base, unrelated to this task, not re-blessed (would be re-blessing a defect, not a legitimate move) | `tests/FusionRpg.Core.Tests/Battle/BattleGoldenTests.cs` |
| 6.7 | Full regression, no unaccounted-for failures | `dotnet test` across Core/Server/Data/Guard/SquadHarness.Tests | PASS — Core.Tests 13316/13354 (38 FAIL); Server.Tests 407/407; SquadHarness.Tests 194/194; Data.Tests 1283/1285 (2 FAIL); Guard.Tests 245/248 (3 FAIL). Every one of the 43 total failures individually re-run against the clean, already-committed T1-T5 base (`git stash` the whole T6 diff, re-run the exact named test/filter, `stash apply` back by SHA) and confirmed byte-identical there — `BasicAttackAdoptionTests`×8, `DominanceBaselineTests`×3, `RealDataAggregateTests`×2, `ResidualFitLoopTests`×7, `ResolverMatchesSimulatorTests`×1, `EventSequenceParityTests`×3, `PreAdoptionTraceTests`×3, `CombatSimJsonEmitTests`×3 (environmental — missing local `Release` build of `tools/CombatSim`, not this task's build config), `CreatureQualityReportTests`×2, `CreatureSpeciesGenExplainTests`×2, `ExpeditionResolverTests.Tier_goldens_are_locked`×1, `BaseTypeCorpusTests`×1, `MaterialCorpusTests`×1, `PlantSideStatusGuardTests`×1, `ClassSystemBaselineRegenTests`×2, `CreatureSpeciesImportCliTests`×2 (one of these two flaps between 1-fail-solo/2-fail-in-suite even on the clean base — an order-dependent flake this task's own change cannot cause, since it isn't in any changed file's dependency graph). The remaining 1 (`EquipRuntimeTests`) is the documented, intentional T7 RED placeholder. Zero newly-caused failures | — |
| 6.8 | Fallout beyond the todo's own file list, found and fixed | `rg` sweep across `tests/`, `tools/` for `BattleStatComposer`/`ResolveForBattle`/`ResolveKit`/deleted `WebMatchService` methods | PASS — found and fixed 3 more production-adjacent files this task's own "Files likely touched" list missed: `tools/ProveAptitude/Program.cs` (real subprocess tool `ProveAptitudeJsonEmitTests` invokes — migrated to `BattleHubCompose`/`HubInputs.Aptitude`, added the `AptitudeTuningHub.Configure` call `AptitudeSubsystem` now needs; live-ran both scoped and unfiltered invocations, confirmed the OLD cap-asymmetry gap (`status.resist.*`) is **closed** by the fuse — both engines now share one `AptitudeResolver.Resolve` call — leaving only a pre-existing, unrelated `resource.max.*` baseline-inclusion artifact this tool's own scope doesn't own), `tools/SquadHarness/SquadMatch.cs` + `Erosion.cs` (functional `.Compose`/`ResolveForBattle` calls migrated), and deleted 4 fully-obsolete throwaway probe tools (`TempoProbe`/`MeasProbe`/`TimelineDispatchProbe`/`TraceOptInProbe`) whose own header comments named "delete once Core.Tests builds again" as their retirement condition, now met, with zero test/CI callers. Also rewrote `ChannelModsHubParityTests.cs`'s 3 RED-for-T6 placeholders (captured live Hub output, pinned as literals — this task's own stated purpose per their comments) and `ProveAptitudeJsonEmitTests.cs`'s stale "known gap" test (rewrote to assert the gap is closed, added a new test for the real residual `resource.max.*` gap) | `tools/ProveAptitude/Program.cs`, `tools/SquadHarness/*.cs`, `tests/FusionRpg.Core.Tests/Stats/ChannelModsHubParityTests.cs`, `tests/FusionRpg.Core.Tests/ClassSystem/ProveAptitudeJsonEmitTests.cs` |

### T7 — Battle ops + tree via Hub; retire TreeAtomSource slot · spec `battle-ops-parity`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 7.1 | Battle equip path Hub op-aware only | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|AtomDerived\|Battle"` | PASS — 1413 run, 1407 pass, 6 FAIL = the same pre-existing `PreAdoptionTraceTests`/`EventSequenceParityTests` (verified against clean base earlier, unrelated). `EquipRuntimeTests` 7/7 (the one that was RED under T5/T6 is now GREEN — see 7.2's own finding). `GearedCornerTests`/`TreeAtomSourceParityTests` all green | `tests/FusionRpg.Core.Tests/Battle/EquipRuntimeTests.cs` |
| 7.2 | `EquipAtomSource.ModsFor` ignore-op battle fold removed | `rg -n "ModsFor" src` (scoped to `EquipAtomSource`) | PASS — `EquipAtomSource.ModsFor` deleted (proven zero production callers: nothing in `src/` called it once `BattleStatComposer` was gone). **Real finding while migrating its 3 test callers**: `EquipRuntimeTests.An_equipped_item_changes_a_battle_number` — the test the earlier T5/T6 evidence rows called an "intentional T7 RED placeholder" — was never a wiring gap at all. Its `WithBoundAtoms` helper resolved equip atoms by `setup.Key` ("squad:0", a constant across every fixture) instead of `setup.SpecimenId` ("s42"), so the resolver's `specimenId == "s42"` branch never matched and the atom silently never applied. Fixed the helper to key on `SpecimenId`; the underlying Hub wiring (`BattleHubCompose` reading `HubInputs.BoundAtoms`, `DerivedAtomsFor` honouring ops) was already fully correct | `tests/FusionRpg.Core.Tests/Battle/EquipRuntimeTests.cs` |
| 7.3 | Unknown op skipped visibly — not coerced to flat | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|AtomDerived"` | PASS — `GearedCornerTests.AnUnparseableOp_isSkipped_neverCoercedToFlat` (an `op: "more"` atom on `DerivedAtomsFor` — the now-sole equip projection — is skipped, `Assert.Empty`); this is structurally the only equip path left, so there is no separate battle-side coercion possible any more, not merely untested | — |
| 7.4 | Tree reaches battle actors via Hub when bindings exist | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TreeAtom\|Battle"` | PASS — `TreeAtomSourceParityTests.cs` rewritten (the old file tested only the deleted `Battle.TreeAtomSource.ModsFor` static): `Lawn_and_battle_hub_resolve_to_the_same_total_for_one_actor` and `F_reaches_battle_hub_the_same_way_it_reaches_the_lawn_amount` feed `PassiveTree.Resolve.TreeAtomSource.BoundAtomsFor`'s own real output straight into `BattleHubInputs.BoundAtoms` (no adapter — it already returns `BoundDerivedAtom`, the exact type the field wants) and `BattleHubCompose.Compose`, proving the total and the `F`-scaled amount both reach battle exactly. 5/5 pass | `tests/FusionRpg.Core.Tests/Battle/TreeAtomSourceParityTests.cs` |
| 7.5 | Unused `TreeAtomSource` Compose slot gone | `rg -n "TreeAtomSource" src` | PASS — `src/FusionRpg.Core/Battle/TreeAtomSource.cs` deleted outright (proven zero production callers — its only value-add, `BoundDerivedAtom` → lossy `BattleChannelMod`, is unneeded now that `HubInputs.BoundAtoms` accepts `BoundDerivedAtom` directly); `rg` now only finds `PassiveTree.Resolve.TreeAtomSource` (the real lawn resolver, unrelated, kept) | — |
| 7.6 | AtomKind Battle Full for `stat.derived` matches tests | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~AtomKind\|AtomDerived"` | PASS — `AtomKindRegistryTests.cs:381` already locks `stat.derived` Battle = `Full` (value unchanged, still green). Fixed the registry's own doc comment, which cited the deleted `BattleStatComposer` as evidence: that composer's additive-only ChannelMods loop actually made "Full" thinner than claimed (it summed every op the same way, the identical defect the Sim note two paragraphs below names as "Partial" for itself) — now that `BattleHubCompose` composes through the real `ActorHub`/`DerivedComposer` every other surface uses, Battle genuinely honours `FlatReplace`/`MaxPriorityFlag`/`SumIncreased` like Lawn, so "Full" is what the code does, not a claim ahead of it | `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs` |
| 7.7 | No Partial lie in battle equip comments | `rg -n "Partial" src/FusionRpg.Core/Battle src/FusionRpg.Server` | PASS — zero hits in battle/equip files (the one remaining `Partial` hit repo-wide near battle is `WorldEndpoints.cs`'s unrelated "partial command acceptance" prose) | — |
| 7.8 | Guard allowlist trimmed further; no revival of `BattleChannelMod` equip/tree fold | `.\scripts\guard-actor-hub.ps1` (manually re-derived, same sandbox limitation as T6.2) | PASS — `$allowChannelModProducers` trimmed 3→1 file (`TraitAtomSource.cs` only — the sole remaining `new BattleChannelMod(` producer, confirmed by `rg`); `EquipAtomSource.cs`/`TreeAtomSource.cs` removed from the allowlist since the file that needed the exemption (`TreeAtomSource.cs`) no longer exists and `EquipAtomSource.cs` no longer constructs `BattleChannelMod` at all | `scripts/guard-actor-hub.ps1` |
| 7.9 | Full regression, no unaccounted-for failures | full `dotnet test tests/FusionRpg.Core.Tests` + `FusionRpg.Data.Tests` (`EquipRuntimeStoreTests`) | PASS — Core.Tests 13317/13354 (37 FAIL, exactly one fewer than T6's 38 — the `EquipRuntimeTests` fix). Every failing test name cross-checked by exact-match filter against the full T6-evidence list of known pre-existing categories: zero unmatched / unexpected failures. `EquipRuntimeStoreTests` (Data.Tests) 5/5 | — |

### Checkpoint: Wave 1 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP1.1 | Dual compose retired; guard green | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| CP1.2 | Ops parity Done | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|TreeAtom\|AtomDerived\|Battle"` | PENDING | — |

---

## Wave 2 — Standing honesty

### T8 — CombatPowerMembership predicate · spec `combat-membership`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 8.1 | `CombatPowerMembership` (or named peer) with include/exclude tests | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CombatPowerMembership"` | PASS — 22/22. New `src/FusionRpg.Core/Stats/Derived/CombatPowerMembership.cs`: `Includes(channelId)` (O(1) — reuses `DerivedStatChannels.IsCombatChannel`'s cached HashSet, plain ordinal prefix checks otherwise) + `Filter<T>`. Every `AllCombatChannelIds` member proven included (not sampled); `skill.cooldown.*`/`skill.effectiveness.*`/`status.power.*`/`status.resist.*` included; `progression.*`/`resource.*`/every OTHER `status.*` family (duration/intensity/durationReduction) excluded; a 7th `ElementTable` element's new combat channels included with no code change (same live-generation claim `AllCombatChannelIds` itself makes); empty/null never throws | `src/FusionRpg.Core/Stats/Derived/CombatPowerMembership.cs`, `tests/FusionRpg.Core.Tests/Stats/CombatPowerMembershipTests.cs` |
| 8.2 | No Standing path uses `IsCombatChannel` alone | `rg -n "IsCombatChannel" src` | PASS — exactly 2 hits repo-wide: the definition itself (`DerivedStatChannels.cs`) and one unrelated caller (`StatusStatPayload.cs`, a status-payload validation concern, not Standing). `ProjectStanding` (`UniqueActorHubCompose.cs`) does not reference `IsCombatChannel` at all today — it takes equip+tree atoms unconditionally, with no membership filter of any kind yet (T9's own job to add the filter, per its "today equip+tree only is HF-standing" scope) | — |
| 8.3 | Documented list matches map assumption §7 | doc read (`combat-power-number-ideal.md` "Combat-affecting membership (D2 filter)" vs `spec-combat-membership.md` vs code) | PASS — all three agree exactly: element combat families / `skill.cooldown.*` + `skill.effectiveness.*` / status power+resist potency included; `progression.*` (esp. Θ) / resource pools / future loot-MF-XP class excluded. `CombatPowerMembership`'s own XML doc comment states the same table as its SSOT, per the spec's "Document exclude list in XML next to the type" code-style rule | `src/FusionRpg.Core/Stats/Derived/CombatPowerMembership.cs` |
| 8.4 | Full regression, no unaccounted-for failures | full `dotnet test tests/FusionRpg.Core.Tests` | PASS — 13339/13376 (37 FAIL, unchanged count from T7's baseline — T8 added 22 new passing tests and broke nothing). Every failing test name cross-checked against the established pre-existing list: zero unmatched failures | — |

### T9 — ProjectStanding synthetics + filter · spec `standing-compose`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 9.1 | Standing includes aptitude / Hub combat writers via synthetics + filter | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ProjectStandingTests"` | PASS — new `ProjectStandingTests.Aptitude_grant_on_a_combat_channel_raises_Standing`: allocating 100‰ into a `Might` (combat) aptitude edge raises `Standing.Offense` (bare vs geared, real summon + real `SaveAllocation`), leaves Utility/Economy untouched. `ProjectStanding` rewritten to read the SAME `DerivedContributionBag` `ProjectSheet` already resolved off the SAME Hub build — reuses it, never a second resolve. No naive "price the Hub total" (would double-count equip/tree already priced as real atoms below); instead synthesizes one atom per membership-included contribution whose SourceId is NOT already an `equip:`/`tree.`-prefixed atom — this is "Hub total minus what equip/tree already contributed," computed per-contribution so it can never go negative | `src/FusionRpg.Server/UniqueActorHubCompose.cs` (`ProjectStanding`), `tests/FusionRpg.Server.Tests/ProjectStandingTests.cs` |
| 9.2 | Double-count proven absent for equip/tree | same filter | PASS — `Equipped_gear_is_priced_exactly_once_in_Standing`: a rolled item granting `+40` flat `combat.power.fire` via `BuildSquad` (the real squad-build materialize path) produces `Standing.Offense` exactly equal to a single-atom `ActorPowerCache.Compose` reference of the same `+40` — not `+80`. Proven by the SourceId-prefix skip (`equip:`/`tree.`) in `ProjectStanding`, which excludes equip/tree contributions from the residual-synthetics loop since they are already carried as real `AtomRow`s | — |
| 9.3 | `progression.*` / Θ do not raise Standing | same filter | PASS — `Level_alone_theta_progression_does_not_raise_Standing`: a real summon (`actor.Level >= 1` asserted nonzero) with zero equip/tree/aptitude produces `Standing` == zero on all 5 axes. `CombatPowerMembership.Includes` excludes `progression.*` before any SourceId is even inspected (T8), so Θ structurally cannot reach `ProjectStanding`'s synthetics loop regardless of contribution volume | — |
| 9.4 | Five-axis DTO unchanged | same filter | PASS — `ActorStandingDto`'s shape untouched; only `ProjectStanding`'s internals (how it's populated) changed. All 4 new tests read `Standing.Offense/Survivability/Control/Utility/Economy` on the existing DTO with no compile changes needed elsewhere | — |
| 9.5 | Cooldown (or other non-atom Hub membership channel) raises Standing | same filter | PASS — `An_aptitude_edge_targeting_a_cooldown_channel_raises_Standing`: finds a real shipped `aptitudes.v*.json` edge funding `skill.cooldown.*`/`skill.effectiveness.*` (asserted to exist, not assumed), allocates into it, proves some Standing axis moves bare→geared. This is exactly T9's headline gap: cooldown/effectiveness is a genuine non-atom Hub writer with no equip/tree analog, so it can ONLY reach Standing via the residual-synthetics path this task adds | — |
| 9.6 | Still `ActorPowerCache.Compose(AtomRow[])` only — no Hub-snapshot overload | `rg -n "ActorPowerCache.Compose" src` | PASS — 10 call sites repo-wide, all pre-existing except `UniqueActorHubCompose.cs:287`'s own (unchanged signature, still `Compose(List<AtomRow>)`); no new overload added to `ActorPowerCache` itself | `src/FusionRpg.Server/UniqueActorHubCompose.cs:287` |
| 9.7 | Guard green | `.\scripts\guard-actor-hub.ps1` | PASS (manually re-derived — sandbox blocks direct powershell invocation from this worktree-isolated session). T9's diff touches only `ProjectStanding`'s body: no new `*Composer*` file, no new `BattleChannelMod` construction (uses `BoundDerivedAtom`, not `BattleChannelMod`), and `UniqueActorHubCompose.cs` still contains `ActorHubBootstrap.CreateDefault`/`new ActorHub`, `ResolveDerivedWithContributions`, and `EquippedBoundAtoms` (all 3 guard-required tokens confirmed present via grep) | `scripts/guard-actor-hub.ps1` |
| 9.8 | Full regression — no unaccounted-for new failures | full `dotnet test tests/FusionRpg.Core.Tests` + full `dotnet test tests/FusionRpg.Server.Tests` | PASS — Core.Tests: 13339/13376 passed, 37 FAIL — exact same count as T8's baseline (T9 touched no Core.Tests-covered code). Server.Tests: 409/411 passed, 2 FAIL (`CreatureLawnDeployAtomPushTests` × 2) — confirmed pre-existing via git-stash technique: stashed T9's diff (`UniqueActorHubCompose.cs` + `ProjectStandingTests.cs`), reran the same 2 tests against the clean committed base, identical 2/2 FAIL with 0 passed; restored T9's diff via `git stash apply <sha>`, re-ran `ProjectStandingTests` (4/4 PASS) to confirm the round-trip left no regression; dropped the verification stash entry | — |

### T10 — Chip honesty — Lv / Θ, never "power" · spec `chip-honesty` (FE)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 10.1 | `scopeFiction` / aptitude chrome free of level/Θ labeled "power" | `npm test -- --run foldAptitudesSurfaceVm` (cwd `web/fusion-rpg-web`) | PASS — 5/5. `scopeFiction` (`foldAptitudesSurfaceVm.ts`) rewritten: unique/species → `Lv {n}`/`Lv —`; commander → `Ladder {n}`/`Ladder —` (real `AptitudesState.theta` earns a name, never the literal `Θ` glyph — see 10.2). Same fix applied to the 2 other real occurrences found by grep, both outside the spec's own 2 named code anchors: `AptitudesPage.tsx`'s `StatBar` label (`power ${theta}` → `Ladder ${theta}`) and `ProgressionTab.tsx`'s `ProgressionAptitudes` paragraph (`power {theta}` → `Ladder {theta}`, same commander scope) — both are "sibling aptitude chrome that repeats the lie" per the spec's own scope line | `web/fusion-rpg-web/src/features/gui-lego/foldAptitudesSurfaceVm.ts`, `.../features/aptitudes/AptitudesPage.tsx`, `.../ui/actor/ProgressionTab.tsx` |
| 10.2 | Tests lock new copy (`Lv` / optional `Θ` only) | `npm test -- --run foldAptitudesSurfaceVm` | PASS — new `chip-honesty (T10)` tests: subtitle text asserted exactly (`"Lv 14"`, `"Ladder 100"`, `"Lv 20"`, `"Lv —"` for missing theta); a parametrized mode loop asserts `/power/i` and the raw `Θ` (Θ) codepoint never appear in any mode's subtitle. **Spec deviation, deliberate and documented in-code:** the spec's literal wording asks for the `Θ {n}` glyph itself; `i18n/vocabularyGuard.ts`'s `BANNED_SYMBOLS` (GG-23, `Θ`) is a binding, real-tree-scanned guard that forbids that exact character as player text repo-wide ("a name on screen, never this letter") — code beats the doc's literal wording here, so commander's real, non-fallback theta is named `Ladder {n}` (the wire's own `ladderIndex` concept, spelled out) instead of rendering the glyph. `vocabularyGuard`'s own real-tree scan (`npm test -- --run vocabularyGuard`) still shows only 3 unrelated pre-existing findings (confirmed via git-stash compare against the clean committed base, identical before/after T10) — T10 introduces zero new hits | `web/fusion-rpg-web/src/features/gui-lego/foldAptitudesSurfaceVm.test.ts` |
| 10.3 | Combat power number stays on Condition / copy-surfaces | `rg -n "combat.power|combatPower" web/fusion-rpg-web/src/features/gui-lego/foldAptitudesSurfaceVm.ts web/fusion-rpg-web/src/features/aptitudes/AptitudesPage.tsx web/fusion-rpg-web/src/ui/actor/ProgressionTab.tsx` | PASS — zero hits in all 3 touched files; T10 only ever renders `Lv`/`Ladder` off `theta`/`specimenLevel`-shaped inputs, never touches a combat-power/Standing value. That number remains T11's (`copy-surfaces`) to wire on Condition | — |
| 10.4 | Tick HF-chip on aptitude-sheet / combat-power ideal Done checklists | doc read + checkbox diff | Deferred to the program's own final "Ideal + aptitude-sheet Done checkboxes cross-linked" checklist item (Checkpoint: Program complete) rather than per-task here — consistent with how T1-T9 handled cross-doc Done-checkbox ticks (map/ideal editing deferred to the program-level pass, not scattered per task) | — |
| 10.5 | Playwright E2E: chip shows `Lv`/`Ladder`, never "power" | `npm run test:e2e` | Skipped — honest gap. Port 5088 is already bound by an unrelated, pre-existing `FusionRpg.Server.exe` (PID 59060, not this worktree's build) that is not this session's to stop; standing up a second full dotnet+FE build/deploy in this worktree solely to screenshot a one-line copy change is disproportionate, and the todo's own T10 verification list requires only the unit filters below, no browser step. Compensating evidence: traced the render path end to end — `ui/gui-lego/pieces/aptitude.tsx`'s `aptitudeScopeChipFactory` (lines 16-19) renders `payload.subtitle` via a direct, untransformed `` ` · ${String(payload.subtitle)}` `` with no intervening copy layer, so the unit-asserted VM string (10.2) is exactly what reaches the DOM | `web/fusion-rpg-web/src/ui/gui-lego/pieces/aptitude.tsx:16-19` |
| 10.6 | Screenshots inspected at desktop / tablet / mobile widths | Playwright MCP + CV inspection | Skipped — same reason as 10.5 (no owned/available live server this session could stand up without disproportionate risk); not attempted, not fabricated | — |
| 10.7 | Full regression, no new failures | `npm test -- --run AptitudesPage AptitudesTab ProgressionTab foldAptitudesSurfaceVm` + `npm test -- --run vocabularyGuard` + `npx tsc --noEmit -p .` | PASS — 20/20 component+fold tests green. `vocabularyGuard`: 1 pre-existing failure (3 findings: 2× `key === "revision"` string-literal false-positive in `stampRevision` helpers, 1× `return "Retired"` in `ui/actor/shared.tsx`), confirmed pre-existing via git-stash compare against the clean committed base — identical before/after T10, zero new hits from this task's own edits. `tsc --noEmit`: 1 pre-existing error (`dev/DeveloperTree.tsx` missing `@/features/log/LogPage` module), unrelated to any T10-touched file, zero new errors | — |

### T11 — Copy surfaces — combat power = O+S+C · spec `copy-surfaces` (FE)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 11.1 | Shared O+S+C helper wired | `npm test -- --run foldConditionSurfaceVm` | PASS — 9/9. New exported pure `sumCombatPowerLabel(standing)` in `foldConditionSurfaceVm.ts` = `offense + survivability + control`; `buildStanding` attaches its result as `combatPowerText` (`.toLocaleString()`) on the `stand-row` piece payload — one call site, no FE-private re-fold elsewhere. `condition.tsx`'s `standRowFactory` renders it as a standalone `data-testid="condition-combat-power"` line, separate from the unlabeled 5-axis radar/bars | `web/fusion-rpg-web/src/features/gui-lego/foldConditionSurfaceVm.ts`, `.../ui/gui-lego/pieces/condition.tsx` |
| 11.2 | Utility/Economy excluded from "combat power" string | `npm test -- --run foldConditionSurfaceVm` | PASS — `copy-surfaces (T11)` test: fixture `{offense:77, survivability:58, control:36, utility:26, economy:49}` → `sumCombatPowerLabel` = 171 (77+58+36), NOT 220 (all five); `foldConditionSurfaceVm(...).main[3].combatPowerText` === `"171"`, proving the wiring matches the pure function, not a second computation | `web/fusion-rpg-web/src/features/gui-lego/foldConditionSurfaceVm.test.ts` |
| 11.3 | No sheet copy treats `combat.power.omni` alone as combat power | `rg -in "combat power\|combatPower\|combat\\.power\\.omni" web/fusion-rpg-web/src` | PASS — only 2 repo-wide hits, both pre-existing/unrelated: `DerivedTab.test.tsx:127` (a raw per-channel Derived-tab fixture listing `combat.power.omni` as one row among many element channels, never a "combat power" headline) and T10's own doc comment. `sumCombatPowerLabel` never reads a single channel — it sums three whole Standing axes | — |
| 11.4 | Five-axis vector retained for inspect | `npm test -- --run foldConditionSurfaceVm` | PASS — same test asserts `stand.bars.axes` still lists all 5 ids (`control, economy, offense, survivability, utility`) even though `economy`/`utility` are excluded from `combatPowerText`; `standingRadarFactory`/`standingBarsFactory` untouched, still render all 5 | — |
| 11.5 | No `PowerScalar` on UniqueActor Standing path | `rg -n "PowerScalar" web/fusion-rpg-web/src` | PASS — zero hits repo-wide (FE never had a `PowerScalar` reference to begin with; `PowerScalar` is an item-card-only concept per the spec's own "Code style" note, never introduced here) | — |
| 11.6 | Tick HF-copy on ideal / aptitude-sheet Done checklists | doc read + checkbox diff | Deferred to the program's own final "Ideal + aptitude-sheet Done checkboxes cross-linked" checklist item (Checkpoint: Program complete), same deferral pattern as 10.4 | — |
| 11.7 | Playwright E2E + inspected screenshots (desktop/tablet/mobile) | `npm run test:e2e` + Playwright MCP | Skipped — same reason as 10.5/10.6: port 5088 already held by an unrelated, unowned `FusionRpg.Server.exe` process; a second full build/deploy purely to screenshot a one-line addition is disproportionate, and the todo's own T11 verification list (`npm test -- --run foldConditionSurfaceVm` / `ActorPanel`) requires no browser step. Compensating evidence: traced the render path — `standRowFactory` (`condition.tsx`) renders `payload.combatPowerText` via a direct, untransformed string interpolation with no intervening copy layer, and the piece is wired into the real `condition-console.json` recipe (unchanged structurally, only the new field) | `web/fusion-rpg-web/src/ui/gui-lego/pieces/condition.tsx` |
| 11.8 | Full regression, no new failures | `npm test -- --run foldConditionSurfaceVm ActorPanel condition ConditionTab Standing condition.tsx StandingRadar` + `npm test -- --run vocabularyGuard` + `npx tsc --noEmit -p .` | PASS — all component/fold suites green (9+46+8 = 63 tests across the runs, zero failures). `vocabularyGuard`: same 3 pre-existing findings as T10's baseline, unchanged — T11 introduces zero new hits (new copy is plain "Combat power: {n}", no banned word/symbol). `tsc --noEmit`: same 1 pre-existing, unrelated error | — |

### Checkpoint: Wave 2 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP2.1 | Standing honest; chip/copy Done | `Standing` filter + `foldAptitudesSurfaceVm` + `foldConditionSurfaceVm` | PASS — T8 (membership predicate) + T9 (ProjectStanding synthetics) + T10 (chip Lv/Ladder, never power) + T11 (Condition combat-power = O+S+C, five-axis vector retained) all committed and individually verified above; Standing is now honest end to end from Hub compose through to both player-facing surfaces that touch it | — |

---

## Wave 3 — Lawn / loadout

### T12 — Lawn aptitude parity Done gate · spec `lawn-aptitude-parity` (+ `unique-lawn-wire`)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 12.1-12.5 | Bound Hot includes UniqueCreature shares; unique-GET-only fetch; general unchanged; same-typeId regression guard; parity prove | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~SpeciesAllocation\|UniqueCreature\|Bound"` | **BLOCKED — honest gap, not forced.** `spec-lawn-aptitude-parity.md` (this program's own spec) locks: "This module is a Done/parity gate, not a second lawn-wire design... Implementation work lives in `unique-lawn-wire`" and its own Boundaries: "Always: Defer implementation ownership to `unique-lawn-wire`." `unique-lawn-wire` (program `aptitude-sheet`, `tasks/aptitude-sheet-todo.md:64` **AS-1.1**, unchecked) is NOT built: `RpgClient.cs` has zero `GET /api/aptitudes/unique/{instanceId}` calls (confirmed via `rg -n "aptitudes/unique" src/FusionRpg.Injector` — no hits), `CheatState.cs` has no per-instanceId UniqueCreature cache, and `SpeciesAllocationSource.Resolve` (`src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocationSource.cs`) has no Bound/instanceId branch at all — it resolves purely by `(Side, TypeId)`, so the very regression this task must prove (12.4) is structurally unguarded, not merely untested. This program's own spec forbids implementing that work here; building it would violate the locked boundary, not satisfy it | `docs/architecture/actor-hub-and-combat-power-solid-fixing/spec-lawn-aptitude-parity.md`, `docs/architecture/aptitude-sheet/spec-unique-lawn-wire.md`, `tasks/aptitude-sheet-todo.md:64` |
| 12.6 | HF-lawn ticked on ideal / maps | doc read + checkbox diff | Not ticked — honestly cannot be, per 12.1-12.5's block; `combat-power-number-ideal.md` still lists this as an open wiring gap (`CheatState.SpeciesAllocation` = commander + species only, Bound bindings unused for aptitude) | — |
| 12.7 | aptitude-sheet `unique-lawn-wire` Done before closing | doc read (`aptitude-sheet-todo.md`) | NOT Done — AS-1.1 unchecked, zero implementation (see 12.1-12.5). Per this task's own dependency clause ("Done, **or open criteria listed**"), the open criteria are listed above rather than forced closed | `tasks/aptitude-sheet-todo.md:64` |
| 12.8 | `guard-secondary-no-unity` green | `.\scripts\guard-secondary-no-unity.ps1` | N/A — no code changed for this task (blocked before any implementation), so nothing new for this guard to scan | — |

### T13 — Injector PassiveTree → Hub · spec `lawn-tree-hydrate`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 13.1 | Injector Hub includes tree bound atoms when tree state exists | `dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~TreeBoundAtomsCache"` (`$env:FUSIONRPG_GAME_DIR` set to the local BepInEx pack) | PASS — 4/4. `TreeBoundAtomsCache` (new, `src/FusionRpg.Injector/Stats/TreeBoundAtomsCache.cs`) caches atoms and `CheatState.ActorHub`'s `boundDerivedAtoms` delegate now merges it alongside `GrantedDerivedAtoms.For` (`ctx => GrantedDerivedAtoms.For(ctx).Concat(TreeBoundAtomsCache.For(ctx)).ToList()`) — two independent Hub writers, one delegate, neither shadowing the other. `Apply_thenFor_returnsExactlyWhatWasApplied_regardlessOfCtx` proves the universal (commander-scope, not per-entity) application; `Apply_invalidatesStats` proves the edge-triggered `Stats.Invalidate()` mirroring `CheatState.ApplyCommanderAllocation`'s own regression fix | `src/FusionRpg.Injector/Stats/TreeBoundAtomsCache.cs`, `src/FusionRpg.Injector/CheatState.cs`, `tests/FusionRpg.Injector.Tests/TreeBoundAtomsCacheTests.cs` |
| 13.2 | Parity with Server sheet tree fan-in for same playerId | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~PassiveTreeEndpointsTests.Get_boundAtoms"` | PASS — 3/3, including `Get_boundAtoms_matches_TreeBoundAtomsForPlayer_calledDirectly`: seeds a real Might allocation opening tier 3 + owns a `stat.derived` node, then asserts the NEW `GET /api/passive-tree/bound-atoms/{playerId}` wire response is byte-for-byte the same set (channel/op/amount/sourceId) as calling `TreeBoundAtoms.ForPlayer` directly — proving the wire is a lossless mirror of the SAME method `UniqueActorHubCompose`'s own sheet fan-in already uses, not a second drifting projection. The Injector has no SQL store, so this HTTP round trip (mirroring `RpgClient.RefreshCommanderAllocationAsync`'s own shape exactly) is its only path to these atoms | `src/FusionRpg.Server/PassiveTreeEndpoints.cs` (`/bound-atoms/{playerId}`), `src/FusionRpg.Contracts/PassiveTreeDtos.cs` (`BoundDerivedAtomDto`), `tests/FusionRpg.Server.Tests/PassiveTreeEndpointsTests.cs` |
| 13.3 | Named "injector tree hydrate" gap closed in comments/docs | `rg -in "injector tree hydrate\|does not hydrate" src` | PASS — both prior claims of the gap (`TreeBoundAtoms.cs`'s own doc comment, `UniqueActorHubCompose.cs`'s class doc comment) rewritten to state the closure mechanism (HTTP fan-in, not a local `PassiveTreeTuningHub.Configure`) and cite this task by name. The two `GateCounterHost.cs`/`InjectorLoop.cs` comments the spec's code-anchor line also names were read and left alone on purpose — they describe an UNRELATED, still-true design decision (reusing `PerfReporter.IntervalSeconds` for the gate-counter flush cadence instead of a second tuning load), not this gap | `src/FusionRpg.Server/TreeBoundAtoms.cs`, `src/FusionRpg.Server/UniqueActorHubCompose.cs` |
| 13.4 | Reload refreshes tree bounds | code read: `RpgClient.cs` `PassiveTreeUpdated` SignalR handler + `StartAsync`/`Reconnected` call sites | PASS — `PassiveTreeEndpoints.cs` already broadcasts `"PassiveTreeUpdated"` to both `WebGroup` and `InjectorGroup` on every allocate (pre-existing, confirmed via `rg`); the Injector simply never listened. New `_hub.On<object>("PassiveTreeUpdated", ...)` enqueues `"passive-tree.bound-atoms.reload"`, wired in `CheatCommandRunner.cs` to `RpgHost.Client.RefreshTreeBoundAtomsAsync()`. `RefreshTreeBoundAtomsAsync` is also called at `StartAsync` (session start) and inside the `Reconnected` handler (mirroring the exact 2026-08-30 fix comment already documented for `RefreshCommanderAllocationAsync`: a reconnect must re-sync every cache `StartAsync` populates, not just re-join the group) | `src/FusionRpg.Injector/RpgClient.cs`, `src/FusionRpg.Injector/CheatCommandRunner.cs` |
| 13.5 | Guards green | `.\scripts\guard-secondary-no-unity.ps1` + `.\scripts\guard-actor-hub.ps1` (both manually re-derived — sandbox blocks direct powershell invocation from this worktree-isolated session) | PASS — secondary-no-unity scans only `FusionRpg.Core/Effects/Plugins` + `IEffectGrantPlugin` implementers; T13 touches neither, so it is untouched by this task's diff. actor-hub guard: no new `*Composer*` file, no new `BattleChannelMod` construction, and `UniqueActorHubCompose.cs` still has every required token (`ActorHubBootstrap.CreateDefault`/`new ActorHub`, `ResolveDerivedWithContributions`, `EquippedBoundAtoms`) — the file's diff is comment-only | `scripts/guard-secondary-no-unity.ps1`, `scripts/guard-actor-hub.ps1` |
| 13.6 | Full regression, no unaccounted-for failures | full `dotnet test tests/FusionRpg.Core.Tests` + full `dotnet test tests/FusionRpg.Server.Tests` + full `dotnet test tests/FusionRpg.Injector.Tests` (`$env:FUSIONRPG_GAME_DIR` set) | PASS — Core.Tests 13339/13376 (37 FAIL, identical to the T8-T11 baseline). Server.Tests 412/414 (2 FAIL, the same pre-existing `CreatureLawnDeployAtomPushTests`, 3 new PASS from `PassiveTreeEndpointsTests`). Injector.Tests (not in CI, needs a real BepInEx game dir — confirmed on this machine at `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL`) 24/24, including the 4 new `TreeBoundAtomsCacheTests` and the 20 pre-existing `MatchModifyTests`/`WaveControlTests`, unaffected. Full `FusionRpg.Injector.BepInEx` build: 0 errors, same 16 pre-existing warnings, none new | — |

### T14 — Bound loadout via Hub + Funnel · spec `bound-loadout-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 14.1 | `ApplyAbsolutes` combat path removed (or non-combat leftovers with owner sign-off) | `rg -n "ApplyAbsolutes" src` + owner note | PENDING | — |
| 14.2 | Bound loadout visible on Hub Derived / AppliedCombat | `dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~UniqueBound\|Loadout"` | PENDING | — |
| 14.3 | HP via Funnel Add / preserve-ratio — not `mode=set` current HP | `.\scripts\guard-funnel-delta.ps1` | PENDING | — |
| 14.4 | No type-wide `plant:N` loadout keys | `rg -n "plant:" src/FusionRpg.Injector` | PENDING | — |
| 14.5 | Each former absolute key maps to Hub channel or Funnel grant — no silent drop | same filter + mapping table | PENDING | — |
| 14.6 | Guards green | `guard-single-writer` + `guard-funnel-delta` + `guard-actor-hub` | PENDING | — |
| 14.7 | HF-bound-loadout ticked on ideal | doc read + checkbox diff | PENDING | — |

### Checkpoint: Wave 3 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP3.1 | Lawn UniqueCreature + tree + Bound loadout Done | Wave 3 filters + guards | PENDING | — |

---

## Wave 4 — Place-matrix / D4 / prove

### T15 — Sim combat Full via Hub · spec `sim-hub-parity`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 15.1 | Sim combat equip ops match Hub semantics | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorDerived\|SimEffect\|AtomKind\|Sim"` | PENDING | — |
| 15.2 | Named Partial for combat `stat.derived` retired or narrowly exempted | same filter | PENDING | — |
| 15.3 | Ideal place matrix Sim row updated | doc read + diff | PENDING | — |

### T16 — Standing coeff tuning (D4) · spec `standing-coeff-tuning`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 16.1 | Tunable family/mask coeffs live and loaded | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorPower\|Coefficient\|Standing"` | PENDING | — |
| 16.2 | High dodge still raises combat power; Survivability axis improved | same filter | PENDING | — |
| 16.3 | Magic-number audit clean on new Policy surfaces | `python scripts/audit-magic-numbers.py --summary` | PENDING | — |
| 16.4 | Missing coeff row → load reject or documented structural default | same filter | PENDING | — |
| 16.5 | Standing vector fixtures re-blessed once if they moved | same filter + golden note | PENDING | — |

### T17 — Unique Θ on wire · spec `unique-theta-wire` (FE wire)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 17.1 | Unique wire carries real theta when known | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~Aptitude\|UniqueActor\|Theta"` | PENDING | — |
| 17.2 | Chip shows `Θ` only from wire | `npm test -- --run foldAptitudesSurfaceVm` | PENDING | — |
| 17.3 | No `theta ?? specimenLevel` on aptitude scope path | `rg -n "specimenLevel" src web/fusion-rpg-web/src` | PENDING | — |
| 17.4 | Playwright E2E + inspected screenshots (desktop/tablet/mobile) | `npm run test:e2e` + Playwright MCP | PENDING | — |

### T18 — Stale dual-compose docs · spec `stale-compose-docs`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 18.1 | Blessing phrases gone or clearly historical | `rg -n "composers stay separate\|locked separate from ActorHub\|BattleStatComposer stays\|adapters OK" docs src --glob "!**/bin/**" --glob "!**/obj/**"` | PENDING | — |
| 18.2 | §8.3 / decisions reflect fuse outcome | doc read + diff | PENDING | — |
| 18.3 | Map Done checkbox for stale docs | doc read + diff | PENDING | — |
| 18.4 | Production code comments / `EquipAtomSource` dual-compose prose overturned | same `rg` on `src` | PENDING | — |

### T19 — Prove Hub combat script · spec `prove-hub-combat`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 19.1 | Script exists and documented (`prove-hub-combat.ps1` or successor) | `Test-Path scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.2 | Post-fuse battle Hub totals ≡ sheet Hub for same inputs (equip/aptitude/tree) | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.3 | Standing rises when a membership combat channel rises — not when only Θ rises | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.4 | Bound lawn aptitude input matches Server UniqueCreature compose | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.5 | Ideal handoff prove path checked / runbook linked | doc read | PENDING | — |

### Checkpoint: Wave 4 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP4.1 | Sim / coeffs / Θ / docs / prove green | Wave 4 filters + `prove-hub-combat` + audit | PENDING | — |

---

## Wave 5 — Stub hygiene

### T20 — Delete PlaceholderBattleResolver + feature-off assaults · spec `placeholder-battle-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 20.1 | `PlaceholderBattleResolver` removed from production paths | `rg -n "PlaceholderBattleResolver" src` | PENDING | — |
| 20.2 | `PlaceholderBattleTuning` deleted or unread | `rg -n "PlaceholderBattleTuning" src` | PENDING | — |
| 20.3 | No silent Hp×Level combat outcomes | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~PlaceholderBattle\|DistrictAssault\|TurnEngine\|World"` | PENDING | — |
| 20.4 | World tests re-blessed for feature-off / fail-loud | same filter | PENDING | — |
| 20.5 | Guard green | `.\scripts\guard-actor-hub.ps1` | PENDING | — |

### T21 — Drop intel Strength from placeholder weight · spec `placeholder-battle-hub` (B4)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 21.1 | `IntelRecorder` / `IntelSeed` do not call deleted Strength | `rg -n "PlaceholderBattleResolver\.Strength" src` | PENDING | — |
| 21.2 | Bands not fed by Hp×Level fiction | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Intel"` | PENDING | — |
| 21.3 | Intel tests updated | same filter | PENDING | — |

### T22 — Fold Level-as-Θ aliases (O2) + stub equip leftover sweep

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 22.1 | No production Level-as-Θ alias on battle/delve aptitude paths | `rg -n "ActorThetaSeam\|theta \?\? specimenLevel\|Level as Θ" src` | PENDING | — |
| 22.2 | Stub equip not player-usable SSOT (align T4) | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| 22.3 | Comments point real Θ / Hub only | `rg -n "ActorThetaSeam\|Level as Θ" src` | PENDING | — |

### T23 — Track `world-actor-combat` in docs · spec `placeholder-battle-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 23.1 | Map Out of scope / Tracked names `world-actor-combat` | doc read + diff | PENDING | — |
| 23.2 | Ideal / Wave 5 Done checkboxes honest | doc read + diff | PENDING | — |
| 23.3 | No module under this program claims world combat engine Done | doc read + diff | PENDING | — |

### Checkpoint: Program complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CPP.1 | All Wave 1–5 acceptance criteria met | all rows above `PASS`/`N/A` | PENDING | — |
| CPP.2 | Program map "Done when" checkboxes tickable | doc read + diff | PENDING | — |
| CPP.3 | Ideal + aptitude-sheet Done checkboxes cross-linked | doc read + diff | PENDING | — |
| CPP.4 | Goldens re-blessed once under fuse RulesetVersion bump | `Golden` filter row of T6.6 | PENDING | — |
| CPP.5 | `world-actor-combat` tracked only | doc read + diff | PENDING | — |
| CPP.6 | Owner accepts program close | owner note | PENDING | — |

---

## Program Done when (from map)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| PD.1 | No production `BattleStatComposer.Compose` under `src/` | `rg -n "BattleStatComposer" src` | PENDING | — |
| PD.2 | No new private ChannelMods combat writers; known producers migrated | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| PD.3 | Cold equip rolled/atom — stub not SSOT | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| PD.4 | Standing membership + synthetics; chip never labels level "power" | `Standing` filter + `foldAptitudesSurfaceVm` | PENDING | — |
| PD.5 | Bound lawn UniqueCreature + Bound loadout via Hub | Wave 3 filters | PENDING | — |
| PD.6 | Sim Full; D4 coeffs; unique Θ; stale docs gone | Wave 4 rows | PENDING | — |
| PD.7 | `prove-hub-combat` green | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| PD.8 | Placeholder + intel Strength deleted; `world-actor-combat` tracked | Wave 5 rows | PENDING | — |
| PD.9 | Ideal + aptitude-sheet Done checkboxes cross-linked | doc read + diff | PENDING | — |
| PD.10 | Goldens re-blessed once under fuse RulesetVersion bump | T6.6 | PENDING | — |

---

## Folded fragments

Fragments folded from fan-out worktrees (runbook §5). Each line is a consumed
`tasks/evidence-fragments/<task-id>.md`.

_(none yet)_
