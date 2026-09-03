# Todo: content-stack

Plan: [`content-stack-plan.md`](content-stack-plan.md). **48 modules, three programs, dependency-ordered.**

Each row names its spec. **Do not start a task without reading its spec in that session** —
`docs/DESIGN-GATE.md` is binding, and this session's four adversarial passes exist because that rule was
skipped elsewhere.

Size: **S** ≤ half a day · **M** ~a day · **L** more than a day.

---

## Phase 0 — corrections that unblock everything · all model-free

- [x] **E42 `units-correction`** · **S** · Deps: — · `effect-atom/spec-units-correction.md` ✅ **DONE 2026-09-03**
  - `definitions.md` §2 and `atom-family-library.md` §2a corrected with reasoning (`CombatProbabilityPolicy`
    has no `PowerScale`/`DefenseScale`; `OverlayCombatCalculator.cs:84-89` sums directly, no sigmoid).
    Old claims struck, not deleted. Swept `spec-value-spec-and-curve.md` (same false claim, same fix).
    `ssot-affixes.md` NOT touched — item program's own shipped magnitudes, out of E42's scope by its §7.
  - E30/E38 hazard rows updated to cite E42 as **closed**.
  - **Evidence:** `tests/FusionRpg.Core.Tests/ActorHub/UnitsCorrectionDriftTests.cs` — 5 tests: code-side
    reflection pin (no `PowerScale`/`DefenseScale`), doc pins (definitions.md, atom-family-library.md,
    spec-value-spec-and-curve.md), planted-violation companion. Core suite: **5117/5117 green** (was 5112).
  - ⛔ **Gate G3 CLOSED.** E30 and E38 may now author magnitudes from the corrected reference.
- [x] **E26 `runner-def-emit`** · **M** · Deps: — · `spec-runner-def-emit.md` ✅ **DONE 2026-09-03**
  - `AtomCompiler.EmitRunnerDefs` (new, `AtomCompiler.cs`) emits one `EffectDef` per translatable
    `RunnerEntry`, keyed on `entry.AtomId` exactly as `AtomRunner.Dispatch` names it on the grant —
    closes the `unknown effect_id` throw `AtomRunner.cs:206-209` documented in its own words.
    `AtomPushCodec.BuildPayload` calls it and appends the results beside the existing compiled defs;
    only bindings whose entry translated are still encoded (an untranslatable entry drops no grant it
    could never execute).
  - **Staleness trap (§3.3), decided shape, both terms wired**: `AtomPushCodec.EmitterVersion` (const,
    = 2) stamped on every payload; the short-circuit at both `AtomPushCodec.BuildPayload` and
    `AtomPushService.Build` is now `receiverRevision == catalogRevision && receiverEmitterVersion ==
    EmitterVersion`. `AtomPushDto.EmitterVersion` (int) and `AtomPushHelloDto.EmitterVersion` (nullable
    int — null distinguishes "never learned" from a real version 0) added to `AtomPushDtos.cs`.
    `AtomPushService.Build` takes and threads a `receiverEmitterVersion` parameter through to the codec.
    **Not wired further**: no production caller of `AtomPushService.Build` (`RpgHub.BuildApplyCommand`)
    passes `receiverRevision`/`receiverEmitterVersion` at all today — that echo-from-Hello wiring was
    already absent for `receiverRevision` before E26 touched this file, so E26 does not regress it, and
    the spec's own acceptance criterion 5 asks only for the codec/service-level mechanism plus a test
    that bumps it — not the Hello-roundtrip, which no acceptance criterion here names.
  - **⚠️ Real defect found and resolved by scoped refusal, not a fix (documented, not hidden).**
    `AtomRunner.RollValues` (`AtomRunner.cs:189-214`) writes the grant overlay under the **raw
    authored param name** ("amount", "damage") for every kind, always — including params with a fixed
    (non-rolled) value, since `stat.modify`/`stat.derived` declare `amount` `Required: true`. But
    `EffectOverlayMerge.AllowedByAction` (`EffectProcAndOwner.cs:130-180`) only accepts the **op-as-key
    rewritten form** (`flat`/`increased`/`more`/`replace`/`flag`) for `ModifyStat`/`ModifyDerivedStat`,
    never `amount`, and does not allow `damage` at all for `BoardAction`. Every `stat.modify`/
    `stat.derived`/`board.action` runner entry carrying any value param would therefore throw at
    `TryValidateOverlayForDef` (stat kinds) or silently drop its magnitude (board.action — the E28
    finding) the moment it dispatched. **This module's contract (§4) forbids fixing `AtomRunner`/
    `EffectOverlayMerge`**, so `EmitRunnerDefs` refuses translation for those three kinds when
    `entry.Values` is non-empty, by id, with `AtomRejectionReason.ParamNotHonoured` — a loud refusal
    at push-build time instead of a throw or a silent drop at grant time. **Follow-up owed to E28
    `param-parity`** (below) — its own test 12 ("walk every kind's declared params, assert each reaches
    its executor") is exactly the shape of test this defect needs a real fix under; flagged there.
  - **Evidence:** `tests/FusionRpg.Core.Tests/Atoms/RunnerDefEmitTests.cs` — 15 tests: per-hit-roll
    end-to-end (criterion 1); one test per runner route — capPerMatch/charges/everyHits/maxStacks/
    non-legacy-predicate (criterion 2); `EffectId == entry.AtomId` (criterion 3); overlay-validates
    positive case (criterion 4, resource.delta is a SAFE kind); planted-violation refusal, Theory over
    stat.modify/stat.derived/board.action (criterion 3/§4), plus its positive twin (a mismatch-kind
    entry with no value param still translates); compiled-path regression alongside a runner atom
    (criterion 6); two emitter-version re-push tests (criterion 5). Plus
    `EffectCatalogExecutionParityTests.A_runner_shaped_fixture_atom_gets_a_runner_def_from_EmitRunnerDefs`
    — the decided test-scoped fixture (criterion 4), never under `data/seed/atoms/`.
    `EffectAtomCatalogGeneratedTests`/`ElementEnumGen` untouched (criterion 4b) — their own frozen
    16-id assertions still pass unmodified, proving E26 added nothing to the shipped corpus.
  - **Fixed two pre-existing test regressions** the two-term short-circuit legitimately caused (both
    updated to echo `receiverEmitterVersion`, not reverted): `PushContractTests.cs` (Core.Tests,
    2 tests — one now also asserts the runner def rides alongside the compiled def) and
    `CompiledPushTests.cs` (Server.Tests, 2 tests).
  - **Full suite, every managed test project, all green:** Core.Tests 5133/5133 (was 5117 pre-E26 +16
    new), Server.Tests 94/94, AtomImporter.Tests 22/22, Guard.Tests 161/161, Data.Tests 608/608,
    CheatCore.Tests 40/40, Launcher.Tests 162/162, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests
    71/71, E2E.Tests 195/195. All four boundary guards (`guard-single-writer`, `guard-secondary-no-unity`,
    `guard-funnel-delta`, `guard-dal`) green. **The injector was not touched or rebuilt** — nothing in
    this module's diff reaches `src/FusionRpg.Injector`, matching §5's "the injector is not built by CI"
    note; no live check owed for E26 itself.
- ⛔ **CRITICAL cross-cutting defect found and fixed 2026-09-03, not part of E26/E27/E28's own scope —
  every def's static (non-overlay) param has been silently broken in live play, for every compiled
  AND runner atom, since this codec shipped.** Found while re-deriving E28 fix #7 (`box.set.cells[]`)
  and needing to know exactly how `item.Params` values look after arriving over the real wire.
  **The mechanism:** `CheatCommandRunner.InstallAtomPush` (the real, only production receive path)
  calls `JsonSerializer.Deserialize<AtomPushDto>(p.GetRawText())` with **no custom converter**. Every
  value in `EffectDefActionDto.Params` (a `Dictionary<string, object?>`) therefore deserializes as a
  boxed `System.Text.Json.JsonElement` — and `JsonElement` does not implement `IConvertible`. Every
  `InjectorEffectActionSink.Exec*` method reads its static params via `JsonOverlay.GetInt`/`GetString`/
  `GetDouble`/`GetBool`, which call `Convert.ToInt32`/`ToString`/`ToDouble`/`ToBoolean` — every one of
  those throws `InvalidCastException` on a `JsonElement`. `InjectorEffectActionSink.Execute`'s own
  outer try/catch swallows it, logs `CheatState.Error`, and returns `false` — **a failed action, not a
  crash, invisible unless someone reads the error log.** `AtomPushCodec.ToDef` (the only place a
  delivered def's `Params` gets built) copied the DTO's dictionary verbatim
  (`new Dictionary<string, object?>(a.Params, ...)`) — never unwrapped it.
  **Why no test caught this:** every existing test (`SimEffectHost`, `EffectCatalogExecutionParityTests`,
  `MigrationParityTests`, `RunnerDefEmitTests`) builds `EffectDef`/`AtomPushDto` **in memory** and calls
  `AtomPushCodec.ToDef` directly on the DTO, or never round-trips it through `JsonSerializer.Serialize`
  → `Deserialize` at all — the exact step that turns a plain `int` into a `JsonElement`. Not one test in
  the whole suite exercised "wire round-trip, then read via `JsonOverlay`" until this session.
  **Reproduced conclusively** with the real production types (`WireRoundTripProbe`, a scratch test:
  built an `AtomPushDto` with a `board.action` def carrying static `op`/`row`/`col`, serialized it,
  deserialized it exactly as `InstallAtomPush` does, called `AtomPushCodec.ToDef`, then
  `JsonOverlay.GetInt` — threw `InvalidCastException`, confirming every compiled atom shipped today
  (`fx.board_cherry`, `fx.set_dirt_box`, `fx.grid_item_cycle`, and everything E28 just "fixed" this
  session) has never actually executed its static params correctly in a real server-pushed match.
  **Fix**: `AtomPushCodec.ToDef` now builds `Params` via `JsonOverlay.FromObject(a.Params)` instead of
  a verbatim copy — `FromObject` already existed for exactly this (it is what `RunEffectGrant`'s
  hand-rolled overlay parse achieves a different way, by walking the raw `JsonElement` tree itself) and
  recursively unwraps nested arrays/objects too, which is also what makes fix #7 (`cells[]`) possible.
  **Evidence:** the scratch probe deleted after confirming the bug and the fix; two PERMANENT regression
  tests added to `PushContractTests.cs` —
  `ToDef_survives_the_real_wire_round_trip_a_static_int_param_reads_back_as_an_int` (the exact
  `fx.board_cherry` shape) and `ToDef_survives_the_real_wire_round_trip_for_a_nested_array_param` (an
  array-of-objects param, proving the recursive unwrap). Both assert `Assert.IsNotType<JsonElement>`
  on the round-tripped value, not just that a value exists — the assertion the bug needed to have
  existed already. **Full Core suite 5151/5151 green** (was 5149), Server.Tests 94/94 green, both
  injector hosts rebuilt clean, all four guards green. **This fix retroactively secures E26's own
  runner-def-emit work** (`AtomPushReceiver.Install` calls `ToDef` for runner defs identically to
  compiled ones) and every atom E28 touched this session — none of those fixes would have actually
  worked live without this one underneath them. **Owner-run live check still owed** — this fix, like
  every Injector-side change in this session, needs a real match to prove the failure is actually gone
  in play, not just that the codec no longer throws in a unit test.
- [x] **E27 `lawn-element-bind`** · **M** · Deps: — · `spec-lawn-element-bind.md` ✅ **CODE DONE 2026-09-03,
  live proof (criteria 1/5) still owner-run**
  - Pass `elementTypes:` in both bridges, mirroring `BattleEngine.cs:36` including the secondary-collapse
    rule. Cache per actor per match — and **leave `ResolveElementTypesFromHub` faster than found**; it is
    already the per-hit board scan the perf audit blamed.
  - **New Core types** (`src/FusionRpg.Core/Demons/`): `LawnElementIndex` — the `(Side, GameTypeId) ->
    DemonSpeciesDef` lookup, deterministic lowest-`SpeciesId` tie-break on a collision, reports once at
    build. `LawnElementResolver` — the per-`(matchKey, ptrKey)` cache wrapping it; `boardLookup` is a
    lazy `Func` so the board scan only ever runs on a cache miss (spec §2.4 algorithm, steps 1-5
    verbatim), an unmapped `(side, typeId)` or an undefined `ElementTypeId` both resolve `Neutral` and
    report once per `(matchKey, typeId)`, and `Create`'s secondary-collapse mirrors
    `BattleEngine.cs:36-38`.
  - **Injector wiring**: new `LawnElementResolverHost` (`src/FusionRpg.Injector/Effects/`) — the ONE
    shared resolver instance both bridges now read, replacing each bridge's own duplicated
    `foreach (var e in board.Entities)` scan (previously copy-pasted verbatim in both
    `InjectorCombatBridge.cs` and `InjectorStatusBridge.cs`) with a single scan behind the cache, so
    either bridge's first resolve for a ptr in a match warms the other. Both bridges now pass
    `elementTypes:` into their `ForZombie`/`ForPlant` calls. `InjectorElementOverride`'s precedence
    (checked first, unconditionally short-circuits) is now stated in `InjectorCombatBridge.cs`'s own
    comment, and now overrides a real resolved value rather than a Neutral default (criterion 6).
  - **Evidence:** `tests/FusionRpg.Core.Tests/Demons/LawnElementResolverTests.cs` — 13 tests: index key
    is `(side, typeId)` not `typeId` alone (`polevaulterzombie`/`wallnut` both `3`); miss returns false
    not a default; duplicate-pair tie-break + collision report; known species resolves its element;
    secondary==primary collapse (criterion 3); real secondary survives; miss resolves Neutral not a
    throw (criterion 2); miss reported once per `(match, typeId)` not once per actor; planted violation
    — undefined `ElementPrimary` resolves Neutral and reports (criterion 2's other half); repeat resolve
    for the same actor never re-calls `boardLookup` (criterion 4's counter); a different actor gets its
    own lookup; a match-key change clears the cache — same ptr, different match, does not reuse the old
    element (criterion 4); a miss reported in one match is reported again in the next (dedup is
    per-match, not process-lifetime). Core suite: **5146/5146 green** (was 5133).
  - **Both injector hosts verified compiling clean** with the wiring — the only local verification
    possible without a live session: `dotnet build src/FusionRpg.Injector.BepInEx` against
    `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL` and `dotnet build src/FusionRpg.Injector.MelonLoader.39`
    against `H:\Games\PVZ-Fusion-3.9_MelonLoader` — both 0 errors, only pre-existing warnings in files
    this module did not touch. This is a real build, not a claim: the build's own `OutputPath` writes
    the DLL straight into each game's plugin folder, so **both live installs now carry E27's code.**
  - ⛔ **Access task, not a gate (plan §2a) — criteria 1 and 5 need a live lawn session this environment
    cannot run.** Sequencing check done, not skipped: the VFX blind-identity trial already **PASSED
    2026-08-21 (43/43)**, closed (`tasks/vfx-v2-todo.md:53`) — not mid-capture, nothing to straddle. The
    shield live absorb proof is still **`[ ]` pending** (`tasks/shield-todo.md:144`, never started) — not
    mid-capture either, so there was no "before" state to preserve; because the build above already
    deployed E27 to both live installs, that proof will now run **post-E27**, which the spec's own rule
    permits (before-or-after, never straddling — and it was always going to run after E27 landed at some
    point). **Owner-run remaining:** criterion 1 (plant/zombie carry species element in
    `CombatActorSnapshot`), criterion 5 (an elemental defense channel measurably changes lawn overlay
    damage), and the shield live absorb proof itself.
- [ ] **E28 `param-parity`** · **L** · Deps: — · `spec-param-parity.md` — ⏳ **IN PROGRESS 2026-09-03.
  Fixes #2, #3, #4, #5, #6, #7 + the content fix DONE, built, tested. Fix #1 blocked (below). Only the
  durable test 12 remains.**
  - Seven params, plus the `fx.set_dirt_box` Water→Dirt fix.
  - **Test 12 is the durable one**: walk every kind's declared params, assert each reaches its executor.
  - Prerequisite of E30 (`atk` is why plant spawns price at zero).
  - ✅ **Fix #2 (`board.action` · `damage`) DONE.** `ExecBoardAction`
    (`src/FusionRpg.Injector/Effects/InjectorEffectActionSink.cs`) now forwards `damage` into the
    payload `DebugActions.BoardAction` reads (`Int(p, "damage", 1800)`) — previously never in the
    payload at all, so every board.action fired the hardcoded 1800 default regardless of what was
    authored. `x`/`y` deleted from the payload per the spec's own decision (§3, dead keys —
    `DebugActions.BoardAction` derives `pos` from col/row and never reads an authored x/y).
  - ✅ **Fix #3 (`status.clear` · `status`, 4→8 of what `status.apply` can actually apply) DONE, scoped
    conservatively where evidence ran out.** `ApplyStatusToZombie`'s own `method:true` switch
    (`DebugActions.cs:867-913`) can apply exactly **8** statuses, not the spec's headline "21" —
    butter/freeze/cold/poison/hypno/ember/jala/kelp. `ExecClearStatus` handled 4 (butter/freeze/cold/
    poison); the other 4 now get an **explicit named refusal** (`CheatState.Error` + `return false`)
    instead of the previous silent no-op (the method always `return true`-d regardless of whether any
    branch fired). **Did not invent a clear path for hypno/kelp** the way ember/jala's own
    already-documented reasoning forbids for those two: reflected `Zombie`'s methods off the shipped
    `Assembly-CSharp.dll` (same technique as the `boxType` reflection above) and found no
    `UnMindControl`/`ClearMindControl`/`Unkelp`-shaped method for either — only raw settable properties
    (`isMindControlled`, `kelpTimes`/`kelpLayer`/`kelpSpeed`) with no evidence a bare flip fully
    reverses what `SetMindControl`/`SetKelped` did (mind control is documented elsewhere as a
    side-swap, not a flag). Guessing at gameplay-critical Unity state with no live check available is
    exactly the defect class this module exists to stop shipping, so hypno/kelp joined ember/jala as
    named refusals rather than an unverified fifth/sixth "fix". **Widening this further — to genuinely
    clear hypno/kelp, or to reach status ids beyond what `status.apply` can even apply — is real
    follow-up work, not done here.** Pure Injector-side change, no Core schema touched — no Core.Tests
    exercise this path; verification is the build + code review below, same as the rest of this
    module's Injector-only fixes, pending the owner's live check.
  - ✅ **Fix #4 (`grid.clear` · row/col + the `selector: "last"` naming lie) DONE.** Schema
    (`AtomKindRegistry.cs`) now declares `row`/`col` on `grid.clear` — `DebugActions.ClearGridItem`
    (`DebugActions.cs:639-668`) already accepted them, targeted clearing just was never reachable from
    an atom. `ExecClearGrid` forwards them (new `JsonOverlay.GetIntOrNull`, `EffectModels.cs` — absence
    must stay "no constraint", never collide with a real 0); `"last"` no longer silently means random
    (it never should have). **Content fix**: `fx.grid_item_cycle` variant b now authors `row: 2, col: 3`
    (the same cell its paired `grid.spawn` used) instead of the misleading `selector: "last"`.
  - ✅ **Fix #6 (`grid.spawn` · `graveType`) DONE.** `ExecSpawnGrid` now forwards `graveType` —
    `DebugActions.SpawnGrid` (`DebugActions.cs:382-383`) already read and honoured it. `NotImplementedNote`
    removed from the schema.
  - ✅ **Fix #5 (`spawn.entity` · `count`, `atk`) DONE — the other top-priority item (§2: "why every
    non-zombie spawn prices at exactly zero").** `atk` was refused for every kind by
    `NotImplementedNote`, but `DebugActions.ApplyAbsoluteProps` already had a plant hook (`P-ATK`,
    `DebugActions.cs:1385`) and the `Z-ATK` absolute cheat id already existed for zombies
    (`CheatState.cs:234,605`) — the zombie branch just never read `atk` at all, and neither branch's
    payload from `ExecSpawnEntity` ever carried it. Fixed both: added the zombie `atk` read (mirroring
    the plant one exactly), and `ExecSpawnEntity` now forwards `atk` for both. Bullets have no such
    hook (they carry `damage` on the projectile itself, a different mechanism) — schema scoped `atk` to
    `HonouredOnlyWhen: "kind=plant|zombie"` rather than declaring it unconditionally, which would have
    been this module's own "declared, accepted, ignored" defect for the bullet case. `count` now loops
    the spawn (floored at 1 — structural, commented per `AGENTS.md`'s exemption rule), stopping on the
    first failed spawn (matches the sink's own "stop seq on first failure" policy). `NotImplementedNote`
    removed from both `count` and `atk`.
  - **Evidence for fix #5:** `AtomKindRegistryTests.Spawn_atk_is_honoured_for_plant_and_zombie_not_bullet`
    (replaced the old `Spawn_atk_rejects_because_the_sink_drops_it`, which pinned the pre-fix refusal)
    — Theory over all three kinds, plant/zombie pass, bullet still refused by name.
    `ActorPowerTests.A_plant_spawn_with_atk_prices_non_zero` — the literal acceptance-criterion case
    (test 7): a `spawn.entity{kind:"plant", atk:80}` atom now validates AND prices `Power.Total > 0`,
    closing the defect the spec named exactly (`CostFunction.SpawnBody`'s `hp==0 && atk==0` guard,
    unreachable for a plant before this fix since plants have no `hp`/`maxHp` param either).
  - ✅ **Content fix (`fx.set_dirt_box` Water→Dirt) DONE, verified against the real game assembly, not
    guessed.** Reflected `BoxType` off the shipped `Assembly-CSharp.dll` directly (PowerShell,
    `[System.Reflection.Assembly]::LoadFrom` + an `AssemblyResolve` handler for the IL2CPP interop
    deps): `Grass=0, Water=1, Dirt=2, Roof=3, Stone=4, River=5, Dirt_water=6, Lava=7` — confirms the
    spec's claim exactly. `data/seed/atoms/fx-board.json`'s `boxType` changed `1 → 2`.
  - ✅ **Fix #7 (`box.set` · `cells[]`) DONE — surfaced the session's second cross-cutting defect while
    being built.** Removed `cells`'s `NotImplementedNote`; `ExecSetBox` now loops every `{row, col}`
    entry (falling back to the single row/col shape when `cells` is absent), each iteration calling
    the existing single-cell `DebugActions.SetBox` unchanged. **What this needed first, and found on
    the way:** `AtomCompiler.Plain(JsonElement)` — the function that turns an authored param into its
    runtime representation — fell through to `el.ToString()` for `Array`/`Object`, the raw JSON *text*
    as an opaque string; widened to recurse (`Array` → `List<object?>`, `Object` →
    `Dictionary<string,object?>`), which is what makes a structured `cells[]` reachable at all.
    **While adding that, found the SAME pre-existing defect twice, independently, in two sibling
    unwrappers**: `Plain`'s own Number arm — `el.TryGetInt32(out var i) ? i : el.GetDouble()` — and
    `JsonOverlay.Unwrap`'s — `el.TryGetInt64(out var l) ? l : el.GetDouble()`. In C#, a `?:` operator's
    two branches must share ONE static type before boxing to `object?`; since `int`/`long` widen
    implicitly to `double` but not the reverse, the compiler silently converted the "true" branch to
    `double` in **every** case, whichever branch actually ran. Both have therefore always produced a
    boxed `double` for every whole-number param, never `int`/`long` — pre-existing since each method
    was written, not something this session introduced. Harmless for callers going through
    `JsonOverlay.GetInt` (`Convert.ToInt32` tolerates a boxed double), which is why nothing ever
    surfaced it — but a real type defect against CLAUDE.md's own overflow table (`double` loses
    exact-integer precision above 2^53 and is non-deterministic across runtimes in a hashed/persisted
    path — `long`/`int` were always the intended types). **Fix, both sites**: cast the integer branch
    to `(object)` before the ternary — `TryGetInt32(out var i) ? (object)i : el.GetDouble()` — which
    breaks the numeric-type unification the compiler was doing and lets boxing preserve whichever CLR
    type actually applies.
  - **Evidence for fix #7 and the two ternary fixes:** `AtomCompilerTests.
    A_box_set_cells_array_survives_compile_as_a_structured_list_not_a_stringified_blob` — a `cells`
    array with two `{row,col}` entries survives `AtomCompiler.Compile` as a real `List<object?>` of
    `Dictionary<string,object?>`, with `row`/`col` reading back as the literal `int` values authored
    (this is the exact assertion that caught the `Plain` ternary bug — it failed with "actual type:
    System.Double value: 1" before the fix). New `tests/FusionRpg.Core.Tests/Atoms/JsonOverlayTests.cs`
    (JsonOverlay had **no direct test file before this**) — 4 tests: a whole number unwraps as `long`
    not `double`; a genuinely fractional number still correctly produces `double` (the fix does not
    break the intentional case); a whole number nested inside an array-of-objects also unwraps as
    `long`; and `JsonOverlay.GetInt` reads the now-`long` value back identically to how it read the
    old (buggy) `double` — proving the fix is behaviour-preserving for every existing `GetInt` call
    site, not just a type change nobody depended on either way.
    `AtomKindRegistryTests.BoxSet_cells_validates_now_that_the_executor_paints_every_listed_cell`
    (replaced the old `BoxSet_cells_rejects_as_unimplemented`, which pinned the pre-fix refusal).
    Both injector hosts rebuilt clean after these fixes (below). **Full Core suite: 5156/5156 green**
    (was 5152; the `AtomBenchGuardTests`/`PredicateCompilerTests` perf flakes seen once mid-session
    under concurrent build load did **not** recur on this clean run). Full solution sweep (all 7
    other managed test projects) reran green after the `JsonOverlay.Unwrap` fix specifically, since it
    sits underneath every grant-overlay parse in Core, not just this module's own atoms: Server.Tests
    94/94, AtomImporter.Tests 22/22, Guard.Tests 161/161, Data.Tests 608/608, CheatCore.Tests 40/40,
    ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71, E2E.Tests 195/195 — all green, zero
    regressions from either ternary fix anywhere in the solution.
  - **Evidence for fixes #2, #4, #6 + content fix:** rebuilt `src/FusionRpg.Injector.BepInEx` (against
    `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL`) and `src/FusionRpg.Injector.MelonLoader.39` (against
    `H:\Games\PVZ-Fusion-3.9_MelonLoader`), both 0 errors. Regenerated
    `src/FusionRpg.Core/Effects/EffectAtomCatalog.Generated.cs` via
    `dotnet run --project tools/ElementEnumGen -- --effect-emit ...` — diff touches only the two fixed
    atoms' params, still 16 defs. Updated the two affected golden fixtures
    (`tests/fixtures/effects/scenarios/effect-set-dirt.json`, `effect-grid-cycle.json`) to the corrected
    values. **Left `EffectSeedCatalog` (`tests/FusionRpg.Core.Tests/Atoms/EffectSeedFixtureOracle.cs`)
    untouched** after first wrongly "fixing" it and self-catching: its own class doc comment is explicit
    — *"kept byte-identical to what it always was... do not add new defs here"* — it is the frozen
    pre-migration oracle `MigrationParityTests` diffs the live-compiled catalog against, not a second
    live source. Instead, `MigrationParityTests.Each_migrated_def_matches_its_seeded_twin` gained a
    narrow, exact, two-case exception (both sides' `Canonical()` strings pinned literally) for these two
    now-deliberately-diverged effect ids, so a *third* unintended drift on top of these two known ones
    still fails loudly. `AtomImporter.Tests`: 22/22 green. Magic-number and overflow audits show zero
    new findings in any file these fixes touched. **After fix #5 landed on top: full Core suite
    5149/5149 green** (the two-flake `AtomBenchGuardTests`/`PredicateCompilerTests.Evaluating_allocates_nothing`
    seen once under concurrent build/test load and confirmed clean in isolation, unrelated to any E28
    change — perf/allocation micro-benchmarks, not param wiring). Both injector hosts rebuilt clean
    against both game installs after every fix in this module, most recently after fix #3. All four
    boundary guards green throughout.
  - ⛔ **Real blocker found on fix #1 (`resource.delta` · `channel`), not in the spec as drafted — read →
    verify → propose, not propose → get corrected.** The spec's contract (§3 row 1) reads as a
    reconnect: *"`ExecApplyResourceDelta` honours all six `ResourceIds`."* Verified against code: **no
    per-lawn-actor keyed store for stamina/hunger/spirit/qi/poise exists anywhere** — not in Core, not
    in Data, not in the Injector. What DOES exist: `ActorResourcePools`
    (`src/FusionRpg.Core/Actions/Cost/ActorResourcePools.cs:11-90`, a real array-backed lazy-regen
    store over all six `ResourceIds`) and its one production wrapper,
    `CommanderResourcePools.GetOrCreate` (`src/FusionRpg.Core/Commanders/CommanderResourcePools.cs:21-38`),
    which keys by **`CommanderId`** (a session-scoped commander), has **zero production callers**, and
    is not reachable from `src/FusionRpg.Injector` at all (grep: zero matches). `resource-hub-ssot.md`
    itself says so: *"Not built — no runtime current-value store or spend loop yet"* (§7's own words,
    read in full), and states the design reason stamina/hunger/spirit/qi/poise are **not** Unity fields
    on `Plant`/`Zombie` by design — `hp` is the sole exception because Unity is that one's SSOT. So
    `EntityStatWriter.cs` (the guarded single-writer) has no hook for the other five and should not
    grow one — the fix needs a **new `targetPtr`-keyed pool registry** (parallel to
    `InjectorEntityRegistry`'s own keying pattern) plus a new non-`EntityStatWriter` writer method,
    which is genuine new storage design, not a reconnect. `scripts/guard-single-writer.ps1` (read in
    full) only regexes 10 literal Unity field names — a non-Unity pool writer trips nothing there, so
    it is legal to build outside `EntityStatWriter`, but it is still new work this spec did not scope
    or size. **Not fixed this session — flagging per the design-gate rule rather than hacking a
    same-turn storage subsystem into an executor fix.** Fixes #2-7 (board.action damage, status.clear
    widen, grid.clear row/col, spawn.entity count/atk, grid.spawn graveType, box.set cells[]) plus the
    `fx.set_dirt_box` content fix have no such blocker and are the next concrete steps — none of them
    need new storage, only wiring the already-declared params through to their existing executors.
  - ⚠️ **Inherited finding from E26 (2026-09-03), not yet fixed.** `AtomRunner.RollValues` writes the
    grant overlay under the raw authored param name for every kind; `EffectOverlayMerge.AllowedByAction`
    only accepts it for the SAFE kinds (resource.delta/economy, status.apply, shield.grant, spawn.entity).
    For `stat.modify`/`stat.derived` it accepts only the op-as-key rewritten form
    (`flat`/`increased`/`more`/`replace`/`flag`, never `amount`), and for `board.action` it does not
    accept `damage` at all — so any runner-routed atom of those three kinds carrying a value param fails
    at grant time or silently drops its magnitude today, independent of E26. Test 12's own shape
    ("assert each param reaches its executor") is exactly what would have caught this on the runner
    path specifically (not just the compiled path). E26 worked around it by refusing translation
    (`AtomCompiler.EmitRunnerDefs`, `AtomRejectionReason.ParamNotHonoured`) rather than fixing
    `AtomRunner`/`EffectOverlayMerge`, which is out of its contract — reproduced and pinned by
    `tests/FusionRpg.Core.Tests/Atoms/RunnerDefEmitTests.cs`'s
    `An_untranslatable_entry_is_refused_by_id_not_silently_dropped` Theory.
- [x] **E29 `kind-value-guard`** · **M** · Deps: — · `spec-kind-value-guard.md` ✅ **DONE 2026-09-03**
  - Thirteen vocabularies, each **reading its SSOT, never a copy**. Test 7 proves adding a status needs no
    guard edit.
  - **One extension point, not eleven special cases**, per the spec's own contract: `ParamDef` gained an
    optional `Vocabulary: Func<IReadOnlyCollection<string>>?` (`ParamSchema.cs`), and
    `AtomKindRegistry.Validate` checks it generically for every declared param — one loop, not
    per-kind code. **`stat.modify.channel`'s pre-existing check ("G6") migrated onto the same
    mechanism** rather than staying its own special case, proving the generic loop reproduces the
    hand-rolled behaviour exactly (Core suite stayed 5156/5156 green immediately after that one
    migration, before any of the other twelve were touched).
  - **All 13 vocabularies wired**, each reading its real SSOT fresh on every call (never cached into a
    field): `stat.modify.channel` (`StatChannels.All`, 11 — migrated), `stat.derived.channel`
    (`DerivedStatRegistry.CreateDefault().AllRegistered`, 267 — **closes §1.1's headline defect**: `AtomRowValidator.cs:313-314`'s
    hand-off to "G6" never actually ran for this kind, so `crit.rat` for `crit.rate` validated, bound,
    compiled, and wrote nothing forever; comment corrected to point at the real check), `status.apply`/
    `status.clear.status` (`StatusCatalogBootstrap.CreateDefault().All()`, 21, the union per rule 4),
    `resource.economy.currency` (5, not 3 — §2.1 correction 1: `AtomKindRegistry.cs:210-211`'s
    "which FA9 does not" claim about maxSun/maxMoney was false; `ExecEconomy` passes currency through
    unfiltered), `resource.economy.op` (`{add,+,set}` — closes the worst silent-no-op in the set: any
    non-add/+ string used to become "set" invisibly, so `op:"addd"` succeeded loudly at the wrong
    behaviour), `resource.delta.channel` (`DerivedStatChannels.ResourceIds`, 6 — declared as the full
    SSOT regardless of E28 fix #1's own unbuilt state, per rule 4), `shield.grant.sourceClass`
    (`{aura,innate,skill}` — closes a second silent-fallback: a typo used to become "skill"),
    `board.action.op` (the 4 canonical spellings `ExecBoardAction`'s own substring normalization maps
    onto — a deliberate narrowing of untested aliases like `"CreateCherryBomb"`, none shipped),
    `grid.spawn`/`grid.clear.gridItemType` (12 — Core's own mirror of the shipped `GridItemType`
    IL2CPP enum, reflected off the real `Assembly-CSharp.dll`: `0,1,3-12`, no member at 2),
    `box.set.boxType` (8 — same reflection technique as E28's `BoxType` content fix, reused here as the
    vocabulary), `spawn.entity.kind` (`{plant,zombie,bullet}`, the only 3 kinds `ExecSpawnEntity`
    switches on). `shield.grant.element` needed **no new guard** — already strict-parsed via
    `ElementRoster.TryParse` and refused at `EffectBag.cs:585-594`, matching the spec's own note.
  - **§7.1's decided empire-currency boundary recorded in all three places its own text names**: the
    refusal message (`currency: "loam"` now explains why, not just that it's illegal), the
    `resource.economy` kind's own description (corrected alongside §2.1's fix), and this todo entry —
    not a fourth document.
  - **Real spec-arithmetic error found and corrected while writing test 9, not silently worked around**:
    §5.1/§6/test-9 all said *"94 of the 98 validate"*, which does not hold against the same section's
    own *"5 refused"* (98 − 5 = 93, not 94) — traced to the audit's own history ("named four... reading
    all 98 found five", 98 − 4 = 94, never updated after the fifth was found). Corrected to 93 in the
    spec (three locations) and in the test itself, trusting the arithmetic over the stale prose
    (DESIGN-GATE: verify against code, not a document contradicting its own count).
  - **Evidence:** new `tests/FusionRpg.Core.Tests/Atoms/KindValueGuardTests.cs` — 18 tests: one planted
    violation per vocabulary (13, each asserting the refusal names the offending value); `wither`
    accepted on `status.apply` (rule 4 — the guard does not over-refuse a runtime-inert-but-legal
    value); every one of the 21 real catalog statuses individually accepted (proves the guard reads
    the SSOT, not a stale subset); a live-read-not-cached proof for the status vocabulary (rule 2);
    every one of the 21 shipped `data/seed/atoms/fx-*.json` atoms validates end to end through
    `AtomKindRegistry.Validate`; and the 98-affix-family sweep against real
    `data/seed/items/affix-families/*.json` content — 56 channel-bearing entries found (23
    `stat.modify` + 28 `stat.derived`-element-expanded + 5 broken, matching §5.1's own breakdown
    exactly), the 5 named ids refused by id and no others, 93 of 98 validating overall.
    **Full Core suite: 5174/5174 green** (was 5156). Full solution sweep: Server.Tests 94/94,
    AtomImporter.Tests 22/22 (the real shipped corpus imports clean through the new vocabulary checks),
    Guard.Tests 161/161, Data.Tests 608/608, CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14,
    ItemSeedValidator.Tests 71/71, E2E.Tests 195/195 — all green, zero regressions anywhere in the
    solution from wiring 13 new value-vocabulary checks into a file everything else in the atom
    pipeline depends on. Both injector hosts rebuilt clean against both game installs. All four
    boundary guards green. Magic-number audit unchanged (12 pre-existing, 0 new) — none of the new
    vocabulary arrays are numeric-magnitude literals the audit's own scope covers.
  - Acceptance criteria against the spec's own §6: (1) all 13 enforced reading their SSOT — done; (2)
    every refusal names kind/param/value/size — done (`$"{kindId}.{def.Name} '{value}' is not one of
    the {members.Count} legal values..."`); (3) `stat.derived`'s check runs, stale comment corrected —
    done; (4) `maxSun`/`maxMoney` claim corrected, currency is 5 — done; (5) a vocabulary gaining a
    member needs no guard edit — proven; (6) 21 shipped atoms + 93-of-98 families — done, with the
    genuine 94→93 correction; (7) no executor behaviour changes — true, every change in this module is
    Core-side validation only, nothing in `src/FusionRpg.Injector` touched.
- [x] **E33 `activation-edge`** · **M** · Deps: — · `spec-activation-edge.md`
  - Raise `OnActivate` on the lawn. ⚠️ **It changes shipped Battle behaviour** — the zombie branch is not
    narrowed and E33's capture is what starts handing it a matching event. Planted violations cover
    **both** branches.
  - Unblocks A-M2.
  - **Done 2026-09-03.** Five pieces, matching the spec's §2 seam list exactly:
    1. **Contract parity** (§2.1) — `EffectTriggers.OnActivate` added to `EffectDtos.cs`, ordinally
       identical to `AtomTriggers.OnActivate` (A18b) by test, no `FoundationContractVersion` bump (per
       spec's explicit instruction — the constant is additive, not a schema change).
    2. **`/effects/contract` now reflects instead of hand-copying** (§2.1a, a defect found in passing) —
       `DebugEndpoints.cs` gained a `PublicConstStrings(Type)` helper reflecting public const string
       fields off `EffectTriggers`/`EffectActions`; this also silently fixes the pre-existing
       missing-`GrantShield`/`ModifyDerivedStat` gap in the published action list, permanently, since a
       hand-copied array can never drift from its source type again.
    3. **Capture kind `actor.activate`** (§2.2) — `EffectEventAdapterCore.MapActivate` requires
       `actorPtr` (returns `null`, never a board-wide fan-out, on a payload without one — the inverse of
       the G5 `FindObjectsOfType<Zombie>()` hole); `actionId` deliberately not mapped (telemetry only,
       atom layer has no action vocabulary).
    4. **Owner-key matching, `EffectProcAndOwner.cs`** (§2.3) — two branches, two different risk
       shapes, both stated inline at the call site:
       - Plant branch: pure wiring fix. Nothing matched `OnActivate` before; added the same
         side+type-id shape as the existing `OnDamageDealt` clause. No shipped behaviour changes —
         nothing raises `OnActivate` on the plant side yet.
       - Zombie branch: a genuine **narrowing** change on a path Battle's own live code flows through
         (`BasicAttack.cs` raises `OnActivate` once per resolved intent). Before this clause the
         unnarrowed fall-through also matched on `TargetTypeId` when `TypeId` was null — the target's
         type standing in for the actor's, the exact thing an owner-key match must never do — and
         matched when `ev.Side` was null (only a *present* wrong side was ever refused). No shipped
         behaviour changes today only because Battle's existing `OnActivate` emit carries neither
         `Side` nor `TypeId` yet, not because the old path was narrow enough on its own — the spec's own
         warning, confirmed true by reading `BasicAttack.cs`, not assumed.
    5. **Fast gate** (§2.4) — `EffectRuntime.HasOnActivateGrant()` added alongside the four existing
       `HasOn*Grant()` gates, same `Bag.HasGrantWithTrigger` shape, so a future `A9 movement-actions`
       producer doesn't repeat the per-hit-allocation shape the 2026-08 perf audit blamed. E33 ships no
       producer of its own — the gate exists for the next module that needs it.
    - **19 new tests** in `ActivationEdgeTests.cs`: contract parity (ordinal equality + both classes'
      const lists via a local reflection helper), capture-kind mapping (success, `null`-on-missing-actor,
      `actionId` not mapped), plant owner-key (match, wrong-side refusal, one planted-violation
      documentation test), zombie owner-key (own side/type match, missing-side refusal,
      `TargetTypeId`-derived-match refusal, Battle's actual no-`Side`/no-`TypeId` shape refused
      before-and-after, two planted-violation documentation tests), match-scoped/entity-scoped keys
      unaffected, and two gate tests against `SimEffectHost`/`EffectBag.HasGrantWithTrigger` directly
      (`EffectRuntime.HasOnActivateGrant()` itself is Injector-only, unreachable from Core.Tests).
    - **Full Core suite: 5193/5193 green** (was 5174 — 19 new, zero regressions from the owner-key
      change, which every trigger-match call site in the atom/effect pipeline depends on). **Both
      injector hosts rebuilt clean**: BepInEx against `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL` (0
      errors, pre-existing nullable warnings only), MelonLoader.39 against the configured
      `FUSIONRPG_ML_GAMEDIR` (0 errors — skips the game-pack copy step when the env var isn't set for
      this pass, same as prior modules). **All four boundary guards green**: single-writer,
      secondary-no-unity, funnel-delta, DAL. **Full solution sweep, all green, zero regressions**:
      Server.Tests 94/94, AtomImporter.Tests 22/22, Guard.Tests 161/161, Data.Tests 608/608,
      CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71, E2E.Tests
      195/195.
    - Acceptance against the spec's own criteria: (1) contract parity — done, test-enforced; (2)
      `/effects/contract` reflects both classes — done, and the pre-existing action-list gap closed as a
      side effect; (3) capture kind maps with `actorPtr` required — done; (4) both owner-key branches
      updated, the zombie branch's narrowing risk stated inline and covered by planted-violation tests —
      done; (5) fast gate added, matching the four existing gates' shape — done; (6) no producer shipped
      in this module (out of scope — `A9 movement-actions` owns that) — correctly deferred, not silently
      dropped.
- [x] **E47 `validate-gate-ci`** · **S** · Deps: — · `spec-validate-gate-ci.md`
  - **State the finding policy first** (§3.2), then wire the step. A gate that fires 83,100 times on its
    first real run gets commented out.
  - **Done 2026-09-03.** Closes E24's B4 half — `AtomImporter --validate` existed with no CI caller,
    so `ContentValidation.Lint`/`.Drift` ran over the real shipped corpus in nobody's pipeline.
    1. **The step** (§3.1) — added to `ci.yml` immediately after the boundary-guards step, same
       individually-checked-exit-code shape as every other step in the file:
       `dotnet run --project tools/AtomImporter -c Release -- --check --validate --db
       "$env:RUNNER_TEMP/atom-validate-db"`, throwing on nonzero exit. Runs against the real
       `data/seed/` tree (no positional root passed), never a fixture. Verified locally, real
       process, real corpus: exits 0, 27 lint entries evaluated (14 orphan warnings, 0 failures), 0
       drift failures, writes nothing (`--check`'s dry-run rollback confirmed by `catalog still at
       revision 0` on a scratch db).
    2. **The finding policy** (§3.2) — verified against the shipped code, not re-derived: structural
       defects (unparseable file, unknown kind, bad param, duplicate id) fail upstream of
       `if (validate)` with no gate logic needed or possible; `drift` without a `powerNote` is
       `Blocking:true` and fails; all seven lints and a noted `drift` are `Blocking:false` by
       construction and only report; budget stays unwired (no ceiling data exists yet — A-G1 is what
       introduces it). Stated inline in the step's own comment, not just in this file, so the next
       reader sees the reasoning at the point of the code.
       - **One line number correction found while testing, not assumed from the spec**: a planted
         unknown-kind atom is refused inside `store.ImportContent`'s catalog check
         (`Program.cs:120-124`, `UnknownKind`), not inside `AtomSeedFile.Collect`
         (`Program.cs:78-83`) as §3.2's table names — confirmed by actually running it. The
         load-bearing fact is unchanged either way: it fails upstream of `if (validate)`, so no gate
         policy is needed for it. Test 2's assertions target the observed behaviour, not the
         specific line range.
       - No `tier-gap` fail-switch shipped (§3.2's own decision) — no `--strict-lint` code exists;
         the follow-up trigger (a named tier-gap finding that turns out to be a real defect) is
         recorded in the spec, not built speculatively.
       - Budget checking stays out (§3.3) — no code added for it, no wiring, matches the spec's
         explicit "must NOT" list.
    3. **6 new tests**, split across two projects per what each is actually testing:
       - `tests/FusionRpg.AtomImporter.Tests/ValidateGateCiTests.cs` (4 tests, each a real cold
         `dotnet run` — the in-process test host has every tuning hub pre-configured and could never
         catch a wiring gap the standalone binary hits): test 1 (real seed tree validates clean,
         output names lint/drift/file counts evaluated — test 5's requirement folded in here rather
         than as a separate test, since test 1 is the natural place to assert it); test 2 (planted
         unknown-kind atom exits 1, nothing written); test 3 (planted orphan atom, valid registered
         kind, no container — exits 0, warning present in output); test 3b (`--db` omitted, no
         `FUSIONRPG_DATA`, working directory outside any `dist/` ancestor — a fresh-checkout shape —
         exits 2 with "no database directory", before any seed file is read).
       - `tests/FusionRpg.Guard.Tests/CiWiringGuardTests.cs` — one new fact,
         `AtomImporter_validate_gate_is_wired_into_ci`, asserting `ci.yml` contains
         `tools/AtomImporter`, `--validate`, `--check` and `--db` together. This is test 6, "the
         point" per the spec: E24 built `CiWiringGuardTests` for "the next unwired suite" and was
         itself that suite; this points the same guard at the fix.
       - **Test 4 (a drift beyond ±25% fails) was NOT duplicated** — the spec's own §4 forbids it,
         and `ValidationGateSeamTests.Real_atoms_with_wildly_wrong_stored_power_fail_the_real_gate`
         (E24) already covers the exact seam `--validate` uses.
    - **New-test results**: `ValidateGateCiTests` 4/4 (real cold-process runs, ~15 min total —
      expected, matches `RealColdProcessTests`' existing cost shape); `CiWiringGuardTests` 3/3 (1 new
      + 2 pre-existing). **Full solution sweep, all green, zero regressions**: Core.Tests 5193/5193
      (unchanged from E33 — this module touched no Core/Injector code), Guard.Tests 162/162 (was
      161), AtomImporter.Tests 26/26 (was 22), Server.Tests 94/94, Data.Tests 608/608, CheatCore.Tests
      40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71, Launcher.Tests 162/162,
      E2E.Tests 195/195. No injector-host rebuild needed — `src/FusionRpg.Injector` was not touched.
    - Acceptance against the spec's own §6: (1) `ci.yml` runs the exact invocation against the real
      tree, throws on failure, writes nothing — done; (2) the §3.2 policy implemented as "verified
      against shipped `Blocking` flags and written down," not new code — done; (3) green on today's
      corpus — done; (4) planted unknown-kind fails CI, planted orphan does not — done (test 2/3); (5)
      output names what was evaluated — done (test 1); (6) removing the step fails a guard — done
      (`CiWiringGuardTests`); (7) budget stays out, reason recorded — done; (8) no `tier-gap`
      fail-switch, follow-up trigger recorded — done.
- [x] **⭐ A-E1 `eligibility-axis`** · **L** · Deps: — · `action-corpus/spec-eligibility-axis.md`
  - ⛔ **Gate G1 — build this first in action-corpus.** `scope`/`scopeKey` plus the five other fields
    `ActionRow` lacks, the `rungBand`→`Rung` collapse rule, and `candidates(actor)`.
  - **Test 3 first**: a null `scopeKey` must match only `general`.
  - **Done 2026-09-03.** The founding gap the program named — before this, nothing in the code could
    express who may hold an action. Six new `ActionRow` fields (§3.0), not two, per the spec's own
    scope-widening finding F1.
    1. **Two process gates cleared first** (§6a, both required before any code): a `decisions.md` row
       — `docs/architecture/decisions.md`, new "Action eligibility axis (2026-09-03)" row appended
       after `OwnerKind.UniqueActor`, following E35's own precedent that a module missing a row
       creates it rather than deferring — and a SQLite migration, `RpgStore.Actions.cs`'s
       `EnsureActionSchemaUnlocked` gaining six `EnsureColumn` calls (`scope`, `scope_key`,
       `category`, `pairing_role`, `structure_axes_json`, `atom_families_json`, `rung_band_json`),
       the exact shape `effect_instance`'s T3.4 migration (`RpgStore.AtomInstances.cs:100-106`)
       already established.
    2. **New vocabulary** (`ActionEnums.cs`) — `EligibilityScope {General, Family, Species}` (A1's
       three-tier closure, mirrored by `EligibilityScopes.Name`/`TryParse`) and `PairingRole {None,
       Enabler, Payoff}` (`None` is a real value, never an omission, matching the spec's own
       correction of the earlier `enablesStatus` draft).
    3. **`ActionRow`** (`ActionRow.cs`) gained `Scope`, `ScopeKey` (opaque — never a foreign key into
       the demon catalog, matching `SpeciesBasicsRow`'s own discipline, `ActionRow.cs:83`),
       `Category` (nullable, reuses the existing `ActionCategory` enum — never a second vocabulary),
       `PairingRole`, `StructureAxes`/`AtomFamilies` (opaque `IReadOnlyList<string>`, this module does
       not validate membership — that's A-C1's job), and `RungBand` — a new `RungBand(int Floor, int
       Ceiling)` record with `Collapse() => Ceiling`, the one stated rule from the spec's own
       corrected `[1,10]` window (`Rung = rungBand[1]`).
    4. **`ActionEligibility.Candidates`** (new file, `Actions/Eligibility/ActionEligibility.cs`) — the
       one query the program needs, exactly per §3.2's set-builder, ordinally sorted by `actionId`.
       The safety property is explicit, not incidental: a `family`/`species` row whose `ScopeKey` is
       null/empty never matches, even against an actor whose own key is also null/empty — two nulls
       comparing equal is exactly the accident that would make a mis-authored row universal, and
       test 3 (written first, per the spec's own instruction) asserts it directly.
    5. **`FamilyMap`** (same file) — a pure `Parse(json)` parser (no I/O, matching
       `EnablerPayoffPairings.Parse`'s own discipline) for the committed projection
       `data/seed/actions/_generated/family-map.json` — **generated this session** as a flat
       `speciesKey → familyId` projection of `data/seed/demons/_generated/family-assignments.json`,
       re-verified fresh rather than trusted from the spec's prose: 53 entries, every key an exact,
       already-lowercase `SpeciesId` from the 84-row `DemonSpeciesCatalog.Generated.cs`, every value
       list exactly length 1 (0 bad entries), 19 distinct families — matching §3.2's three measured
       justifications exactly. The projection is A-S0's eventual home per the spec; committing it now
       unblocks A-E1's own tests without introducing any catalog coupling in C#.
    6. **Persistence** (`RpgStore.Actions.cs`) — `UpsertAction`'s INSERT/ON CONFLICT and `GetAction`'s
       SELECT/`ReadAction` extended for all six fields; round-trip and default-value behaviour proven
       in `Data.Tests`, not assumed.
    - **20 new tests**: `Core.Tests/Actions/EligibilityAxisTests.cs` (13 — the candidate-set query,
      the null-scopeKey planted violation written first, the unknown-family miss rule, ordinal
      stability, `UnlockState.TryAccept` driven from a REAL candidate set rather than a fixture
      literal (test 5), the family-scoped-unknown-family mirror case (test 6's non-A-C1 half), the
      fourth-scope-value planted violation via the parser (test 7), `ActionEffectScope` untouched
      (test 8), `RungBand.Collapse`, and three `FamilyMap` tests including one that reads the REAL
      committed `family-map.json` off disk and cross-checks it against its source file's raw JSON,
      not a copy held in the test); `Data.Tests/EligibilityAxisMigrationTests.cs` (3 — full six-field
      round trip, all-defaults-on-an-unset-row, and the migration itself: a hand-built pre-A-E1
      `rpg_action` table with one row written through raw SQL, then `RpgStore.Init()` on the same
      directory, proving a database created before this module still loads with correct defaults).
    - **Full Core suite: 5206/5206 green** (was 5193 — 13 new, zero regressions across the whole
      atom/action pipeline this module's fields sit inside). **Full solution sweep, all green, zero
      regressions**: Data.Tests
      611/611 (was 608 — 3 new), Server.Tests 94/94, Guard.Tests 162/162, CheatCore.Tests 40/40,
      ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71, Launcher.Tests 162/162, E2E.Tests
      195/195, AtomImporter.Tests 26/26. **Both injector hosts rebuilt clean** (BepInEx against
      `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL`, MelonLoader.39 — 0 errors both, pre-existing
      nullable warnings only) even though this module touched no Injector code, since
      `FusionRpg.Core`/`FusionRpg.Data` both changed and both hosts depend on them. **All four
      boundary guards green.** Magic-number audit unchanged (12 pre-existing, 0 new) — `RungBand`'s
      `Floor`/`Ceiling` are structural, not balance-surface literals.
    - Acceptance against the spec's own §6: (1) `scope`/`scopeKey` exist and persist — done; (1b) all
      six fields exist and persist, collapse rule stated (`Rung = rungBand[1]`) and tested — done;
      (1c) `ActionCategory` reused, never redeclared — done; (2) `candidates(actor)` implements §3.2
      exactly, ordinally sorted — done; (3) a null `scopeKey` matches only `general`, planted
      violation — done, test written first; (4) unknown family yields general-tier only — done; (5)
      `familyOf` is a defined mapping with a real committed source, failure is empty not wrong —
      done; (5b) the unknown-family load-time refusal correctly NOT built here (belongs to A-C1) —
      the mirror case (no schema coupling, inert not wrong) is asserted instead, per the spec's own
      instruction; (6) `UnlockState.TryAccept` exercised from a real candidate set — done; (7)
      `ActionEffectScope` unchanged — done, asserted; (8) the boundary against `effect-pipeline`
      module 8's tag eligibility is stated in this module's own doc comments (a different axis on a
      different entity — affixes on containers, never actions on actors) — the map-level statement is
      a follow-up for whoever next touches `action-corpus-map.md`, not silently dropped.
- [x] **A-U1 `rung-semantics`** · **M** · Deps: — · `spec-rung-semantics.md`
  - Name authored `Rung` apart from `effectiveRung`; drop or price `minRung`; split `cap` into
    `heldCap`/`rungCap` at equal values.
  - **Do not "fix" `StructureBudgetGuard`** — it is correct; the specs' inference was wrong.
  - **Done 2026-09-03.** Three findings, one question — "does a rung mean the same thing to the
    author, the holder and the guard?" It now does, by construction, not by convention.
    1. **`effectiveRung` distinctly named AND distinctly typed** (§3.1) — new `EffectiveRung(int
       Value)` record struct (`ActionRow.cs`, alongside the A-E1 `RungBand` it sits next to).
       `UnlockLadder.Rung` **renamed** to `UnlockLadder.EffectiveRung`, returning the wrapped type —
       the authored `ActionRow.Rung` stays a plain `int`, untouched, so the two can never silently
       re-merge the way the pre-correction spec prose did.
    2. **`StructureBudgetGuard` confirmed correct, not fixed** (§3.1) — it reads `row.Rung`, verified
       unchanged; a new test proves this directly (two otherwise-identical rows differing only in
       authored `Rung` get different structure-budget verdicts, with no holder-side value anywhere in
       the call). The false claim this corrects — *"a scope's rung ceiling already gates its
       structure ceiling as a side effect"* — lived at `action-corpus-ideal.md:211` (§5, the
       ORIGINAL, uncorrected wording); `docs/architecture/power/...` was never involved. Corrected
       in place with a `⛔ CORRECTED 2026-09-03` note pointing at `spec-rung-semantics.md` §3.1 and
       the review's own §40.1 finding. **Of the "five specs" the module names, four were already
       corrected in an earlier pass this session** (`spec-validate-heal.md`, `spec-corpus-loader.md`,
       `spec-distribution-planner.md`, `spec-signature-propose.md` — each already carries the right
       framing, verified by reading them, not assumed) — only `action-corpus-ideal.md`'s own §5
       needed the fix, found by grepping the live claim text across the whole doc tree rather than
       trusting the spec's own count.
    3. **`minRung` — confirmed it never existed, decision already made, nothing to build** (§3.2) —
       `data/tuning/action-rungs.v1.json` re-verified: rung 1's `structureBudget` is `[]` and
       `costMulti: 1000` (no floor tax); rung 5's `costMulti: 3627` recorded as moot, matching the
       spec's own claims exactly.
    4. **`cap` split into `heldCap`/`rungCap`** (§3.3) — `UnlockTuning` record, its loader, and every
       call site (`UnlockLadder.EffectiveRung`'s ceiling read, `UnlockState.TryAccept`'s capacity
       check, `CapPolicy.HeldCap`) updated; `data/tuning/action-unlock.v1.json` split with both
       starting at 10, `_meta` rewritten to explain the split and drop the stale "one number, two
       uses" framing. Zero production callers of either type existed before this change (confirmed by
       grep, matching this program's established "ships unwired" pattern), so the blast radius was
       entirely within tests.
    5. **The register row** (§3.4) — `ssot-power-scale.md` gained row 19 in §10.2 (the ladder itself,
       a non-level scale bounded by `earnCount`, distinct from the authored `Rung`) and a new §11.2
       row for `heldCap`/`rungCap` (a soft, tunable content window, never a hard progression stop,
       matching the framing `action-corpus-ideal.md` itself already argued for).
    - **20 new/changed tests**: `Core.Tests/Actions/RungSemanticsTests.cs` (7 new — authored-rung
      guard-correctness, `EffectiveRung`'s distinct type, rung 1's no-floor-tax fact, a data-only scan
      proving no committed `rungBand` window has a floor above 1, a `minRung`-zero-hits drift test
      pinning the one unrelated `AuraTuning.cs` hit, `heldCap`/`rungCap` independence, and the
      register-row doc-drift check); `UnlockLadderTests.cs` (renamed `Rung`→`EffectiveRung` call
      sites, split the single `CapBelowOneIsRejectedAtLoad` test into `HeldCapBelowOneIsRejectedAtLoad`
      / `RungCapBelowOneIsRejectedAtLoad`); `UnlockStateTests.cs`, `UnlockDiscardTests.cs`,
      `GrantSeamLifecycleTests.cs`, `EligibilityAxisTests.cs` all mechanically updated from the single
      `Cap:` constructor argument to `HeldCap:`/`RungCap:` pairs at equal values (behaviour-neutral,
      confirmed by the full suite staying green).
    - **Full Core suite: 5215/5215 green** (was 5206 — net +9 across new and split tests, zero
      regressions from renaming a method with zero production callers and splitting a field with
      zero production callers). **Full solution sweep, all green**: Data.Tests 611/611, Server.Tests
      94/94, Guard.Tests 162/162, CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14,
      ItemSeedValidator.Tests 71/71, Launcher.Tests 162/162, E2E.Tests 195/195, AtomImporter.Tests
      26/26 (one transient failure from concurrent build contention with the Core.Tests background
      run, confirmed non-reproducible by an isolated re-run at 26/26 — not a regression). **Both
      injector hosts rebuilt clean** (BepInEx + MelonLoader.39, 0 errors both, pre-existing nullable
      warnings only) since `FusionRpg.Core` changed. **All four boundary guards green.** Magic-number
      audit unchanged (12 pre-existing, 0 new).
    - Acceptance against the spec's own §6: (1) authored `Rung` and derived `effectiveRung` distinctly
      named and typed — done; (2) `StructureBudgetGuard`'s authored-rung read asserted correct, the
      inference-error specs corrected (4 already were; the 5th, `action-corpus-ideal.md`, fixed here)
      — done; (3) `minRung` dropped, the `[5,10]`→`[1,10]` window and the moot `costMulti: 3627`
      framing verified already in place — done; (4) `heldCap`/`rungCap` separate tunables at equal
      values, zero behaviour change — done; (5) the ladder's register row, held by a doc-drift test —
      done; (6) no shipped coefficient moved — confirmed, both dials still read 10.

### ✅ Checkpoint C0 — nothing fails silently · **and C1 — an action can be held**

---

## Phase 1 — the seam · effect-pipeline

- [x] **ep-1 `affix-schema`** · **S (re-audit, not build)** · Deps: — · `spec-affix-schema.md`
  - ⛔ **BUILT** — `RpgStore.Containers.cs:28,66` ships `prefix_rolls`/`suffix_rolls` and
    `effect_affix`. **The spec's own "Exists in code today" table says `no` in all four rows and is
    stale — correcting it is task one.**
  - ⛔ **A1 is NOT met**: `Resolver.cs:60-66` runs two independent draws where A1 requires a mixed
    bundle to consume one of each budget *simultaneously*; its test asserts only `Assert.NotEmpty`,
    commented *"today's two-independent-draws interim model"*. **A shipped module failing its own
    acceptance — this is the real work in Phase 1.**
  - **Done 2026-09-03.** Confirmed (not re-derived) three of the four "Exists in code today" rows
    were already ✅ per the spec's own 2026-09-03 re-verification — `prefix_rolls`/`suffix_rolls`,
    the derived (not stored) `affix_class`, and the affix-bundle/slot shape all shipped already, so
    the real work really was narrow: **A1's mixed-bundle budget, and the double-draw defect riding
    alongside it.**
    1. **`Resolver.Resolve` rewritten to two passes with carried state** (`Resolver.cs`) — the old
       single `DrawFromPool` helper (two independent, memory-free calls) replaced by
       `DrawPrefixPass`/`DrawSuffixPass` sharing a `PickOne` weighted-draw core.
       `DrawPrefixPass` returns the REMAINING suffix budget: a `Mixed` affix stays eligible in the
       prefix pass only while that budget is still positive, and drawing one decrements it by
       exactly one (spending both budgets on the same roll, never doubling either).
       `DrawSuffixPass` excludes every affix id the prefix pass already drew, closing the second,
       previously-unnamed defect — a `Mixed` affix could be drawn twice, once per pass, with no
       shipped container able to trigger it only because no shipped container has a pool yet.
    2. **The stream and order are untouched** — both passes still consume the single `affix.draw`
       stream in fixed prefix-then-suffix order, so a pool with no `Mixed` affix rolls byte-identically
       to before this fix (proven by a dedicated test computing exactly `PrefixRolls + SuffixRolls`
       atoms across 20 seeds, plus every pre-existing non-`Mixed` `ResolverTests` case staying green
       unchanged).
    3. **The weak pre-fix test replaced.** `A_mixed_class_bundle_can_be_drawn_from_both_budgets`
       (comment: *"today's two-independent-draws interim model"*, assertion: `Assert.NotEmpty` only —
       would have passed with the defect present) removed and replaced with 4 tests asserting the
       real properties: the mixed bundle's own atoms never appear more than once across 40 seeds and
       always appear together (never one ref without the other); a `Mixed` affix is never drawn when
       the suffix budget starts at zero and nothing else is prefix-eligible (empty result, 20 seeds,
       deterministic); a pool containing ONLY a `Mixed` affix with both budgets nonzero resolves to
       exactly its 2 refs, not 4 (the double-draw defect, directly reproduced then closed, 20 seeds);
       and the no-`Mixed`-pool conservation check above.
    - **Full Core suite: 5218/5218 green** (was 5215 — net +3: 4 new tests, 1 removed). **Full
      solution sweep, all green, zero regressions**: Data.Tests 611/611, Server.Tests 94/94,
      Guard.Tests 162/162, CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests
      71/71, Launcher.Tests 162/162, E2E.Tests 195/195, AtomImporter.Tests 26/26. **Both injector
      hosts rebuilt clean** (0 errors both, pre-existing nullable warnings only) since
      `FusionRpg.Core` changed. **All four boundary guards green.**
    - The spec's own success criteria: `prefix_rolls`/`suffix_rolls` exist, `pool_rolls` gone — was
      already true, re-confirmed; pool rows reference affixes, a bundle draws as one correlated unit
      — already true, re-confirmed (`Master_of_fire_and_ice_resolves_as_one_correlated_draw`, unchanged
      and still green); every `spec-container-schema.md` testing-strategy check passes — unaffected by
      this change, still green; the eight `poolRolls`-declaring seed files migrate and validate —
      already migrated (verified: zero `poolRolls` hits remain in `data/seed/`); **A1 and A2 each
      closed by a named test, not by inspection — A1 done here (4 new tests); A2 (`core` maps to the
      fixed core, never a weighted-pool entry) was already built and tested before this session's
      involvement, unaffected by the Resolver rewrite.** Doc corrected in place
      (`spec-affix-schema.md`'s own table now reads all four rows ✅).
- [x] **ep-2 `resolution-order`** · **S (re-audit)** · Deps: ep-1 · `spec-resolution-order.md`
  - ⛔ **BUILT** — `Resolver.cs` (14,971 bytes). Re-audit against its acceptance; note
    `variant-shifts.v1.json`'s `_meta` says its values are *"working values, not a validated balance
    decision"*.
  - Slots → affixes → atoms → tiers → values, one named RNG stream per layer. **E30's tests depend on it.**
  - **Re-audited 2026-09-03 — genuinely nothing left to build, verified against the real code and
    data, not assumed from the spec's own "RE-VERIFIED" claim.** Every success criterion checked
    directly:
    1. **The five-step order**, each on its own named stream (`affix.slot`/`affix.draw`/`affix.tier`/
       `atom.value`) — `Resolver.Resolve` (re-read during ep-1's own edit to it) matches exactly, and
       the ep-1 fix touched only step 2's internals, never the stream names or their order.
    2. **`variant-shifts.v1.json` matches the spec's table exactly, field for field** — read directly:
       `mutated` carries `prefixRollShift: 1` (the row the spec's own "RE-VERIFIED" note said was the
       one genuinely-resolved ambiguity), `corrupted` carries `rerollsOneElementSlot: true`, all six
       variants present. The `_meta` "working values, not validated" note is a balance-confidence
       caveat, not a missing-shape one — the spec's own success criteria never asked for validated
       numbers, only correct fields.
    3. **The t5 saturation clamp carries its required structural-limit comment** — `VariantShift.cs:26`
       states it explicitly ("t5 is the highest tier that exists — there is no t6 row to select. This
       is a structural..."), read directly rather than trusted from the spec's claim.
    4. **Every named test in the spec's own testing-strategy table has a real, currently-green
       counterpart** in `ResolverTests.cs` (`Master_of_fire_and_ice_resolves_as_one_correlated_draw`,
       `Variant_shifts_the_tier_window_and_authors_nothing`,
       `Ancient_at_rung_10_saturates_at_t5_not_a_progression_cap`,
       `Same_seed_same_container_same_variant_reproduces_identically`,
       `Each_named_stream_is_independent_of_how_many_times_the_others_were_drawn` for the
       future-layer-independence guarantee, `An_extra_undrawn_slot_in_the_pool_does_not_shift_which_
       affixes_are_drawn` for per-layer stream independence) — all proven green by ep-1's own full
       5218/5218 Core suite run moments earlier, which already exercised this exact file.
    - **No code, data, or doc change was needed** — the module closed itself before this session
      reached it; this entry records the verification, not a build. Zero new tests, zero regressions
      (there was nothing to regress).
- [x] **⭐ ep-4 `instance-producer`** · **S (re-audit)** · Deps: ep-2, **ep-3** (per its own spec §1 —
  the todo previously said ep-1/ep-2 and scheduled ep-3 *after* it) · `spec-instance-producer.md`
  - ⛔ **BUILT AND CALLED** — `ProduceAndBind` at `RpgStore.UniqueActors.cs:756`;
    `InstanceProducer.Compose` at `SpeciesMaterialiser.cs:55`. **Gate G2 is withdrawn.**
  - Only `ActionSeeder.Generate` still has zero production callers — that is the action corpus's
    wiring, not this seam's.
  - **Re-audited 2026-09-03 — the two bullets above contradicted each other, and this entry corrects
    it: the first ("Gate G2 is withdrawn") is the one that's true, verified by tracing a real,
    reachable HTTP request all the way through, not by inspection.** The second bullet ("effect_binding
    has zero rows... callers") was stale — left over from before the wiring below existed — and is
    removed here rather than repeated.
    1. **The full chain is live**, traced end to end: `PUT /actors/{instanceId}/equipment/{slot}`
       (`UniqueActorEndpoints.cs:85`, a real, mapped route) → `UniqueActorService` →
       `RpgStore.UpsertUniqueEquipment` → `ReconcileUniqueEquipmentAtomBindingsUnlocked` (called
       unconditionally on every equip, `RpgStore.UniqueActors.cs:664`) → `ProduceAndBind`
       (`RpgStore.AtomInstances.cs:283`) → `InstanceProducer.Compose` (Core, no I/O) +
       `SaveInstanceAndBind` (one transaction, real `INSERT`s into `effect_instance` and
       `effect_binding`). `POST /actors` (`UniqueActorEndpoints.cs:25`) is the equally-real creation
       endpoint upstream of it. A real player equipping one of `UniqueEquipmentCatalog`'s four
       atom-backed items writes a real `effect_binding` row — the "zero rows" claim was never re-
       checked against this wiring once it landed.
    2. **`ProduceAndBind` writes through its own direct transaction** (`SaveInstanceAndBind`), not
       through the standalone `RpgStore.SaveInstance`/`Bind` methods the spec's original table named —
       a different, equally real path to the same tables, confirmed by reading the method body rather
       than assumed from the symbol names in the spec's 2026-09-02 table.
    3. **Every test the spec's own testing-strategy table names already exists and is green**:
       `Produce_writes_an_instance_and_a_binding_for_a_real_owner`,
       `ResolveBindings_returns_non_empty_after_produce`, `PowerJson_stays_null_after_produce`,
       `Same_container_revision_seed_and_variant_reproduces_identically`,
       `Producing_for_an_equipped_item_slot_is_not_this_modules_test_surface`, and
       `Partial_failure_never_leaves_an_orphaned_instance_with_no_binding` (all in
       `Data.Tests/InstanceProducerStoreTests.cs`). **The spec's own ⭐ acceptance line — `AtomPushService`
       compiles the produced instance and `AtomRunner` receives an entry — lives in
       `Server.Tests/AtomEndToEndTests.cs`'s `The_full_chain_runs_in_production_shape`**, not
       `Core.Tests` as the spec's file-path table said (that file's own doc comment already records
       the correction: `AtomPushService` lives in `FusionRpg.Server`, which `Core.Tests` cannot
       reference — verified against both `.csproj` files). Confirmed passing in this session's own
       Server.Tests 94/94 run, run moments before this re-audit.
    - **No code, data, or doc change was needed beyond correcting this todo entry's own stale
      contradiction** — the module closed itself before this session reached it, the same shape as
      ep-2. Zero new tests, zero regressions.

### ✅ Checkpoint C2 — a binding reaches a runtime

---

## Phase 2 — the pool

- [x] **E30 `channel-pool`** · **L** · Deps: E28, E29, **E42**, **ep-2** · `spec-channel-pool.md`
  - L2. The pool artifact, the `params.channel` widening, five refusals, and **pricing a pooled atom**.
  - **Criterion 8**: reconcile the 98 authored families in `data/seed/items/affix-families/`.
  - **Done 2026-09-03**, in two passes: everything provable Core-side first, then — after the owner
    resolved the one genuine open design question — the resolver step itself. Checkpoint I closed.
    1. **Real gap found before any code was written, by reading the executor**:
       `InjectorEffectActionSink.cs:93` reads `channel` as ONE string
       (`JsonOverlay.GetString(p, "channel")`); nothing anywhere reads an array-valued channel. §3.2's
       own worked example — *"count > 1 is how '+15% to all resistances' becomes one atom"* — has no
       stated execution semantics for what a resolved multi-channel atom actually IS at apply time,
       and no doc in the repo answers it. **This is scoped out of this pass**, same discipline as
       E28's fix #1: named, not hacked around. What IS built below covers `count = 1` and everything
       schema/validation/pricing-shaped; the resolver step itself (turning a pool reference into a
       concrete channel, for ANY count) is not built, because it depends on that unanswered question
       and on `effect-pipeline` module 2 gaining the new responsibility the spec's own §4 correction
       already declares ("E30 declares a dependency on effect-pipeline module 2") — ep-2's OWN spec
       (re-audited this session, closed) has not been updated to know about this dependency yet.
       Acceptance criterion 5 (byte-identical reroll) and testing-strategy tests 1-3 are the
       casualties of this gap, and are the ones NOT closed here.
    2. **The pool artifact** — `data/seed/channel-pools/pools.v1.json`, 12 entries, one per
       element-expanded channel stem. **Verified against the real 98 families, not trusted from the
       spec's own table**: reading every `stat.derived`/`stat.modify` entry whose `variants.generate`
       names `"element..."` and whose `params.channel` carries a `{variant}` template gives exactly
       **14** distinct stems; two (`combat.power.pierce`, `combat.power.overflow`) are confirmed
       unregistered channel families (E29's own guard already refuses the three atoms naming them);
       the remaining 12 match this file's 12 pool ids exactly under the stated naming rule. Each pool
       is `ElementRoster.Concrete`'s six elements at weight 1000, `omni` absent, per §3.1's decision.
    3. **`ChannelPool.cs`** (new) — `ChannelPoolRow`/`ChannelPoolMember` (Core-side, no I/O),
       `ChannelPoolFile.TryParse`/`TryParseEntry` (the whole-document and per-entry forms, sharing one
       implementation so they cannot validate a pool entry differently), `ChannelRef` and
       `ChannelRefJson.TryRead` — reads a `channel` param in every shape it can arrive in: a plain
       string, a `JsonElement` (validation, straight off `ParamsJson`), or an already-unwrapped
       `Dictionary<string,object?>` (post-compile / post-wire — both paths this session's own E28 fix
       to `AtomCompiler.Plain`/`JsonOverlay` already made safe for a nested object).
    4. **`AtomSeedFile.cs`** — new `SeedEntryKind.ChannelPool`, `SeedContent.ChannelPools`, a
       `ReadChannelPool` dispatch case delegating to `ChannelPoolFile.TryParseEntry`, and the
       `"channel-pool"` kind string. **`SeedScanner.OwnedFolders` gained `"channel-pools"`** —
       verified end to end against the REAL CLI, not just unit-tested: a real
       `dotnet run --project tools/AtomImporter -- --check --validate` against the actual
       `data/seed/` tree now sweeps 11 files (was 10), parses the new pool file cleanly, and still
       exits 0 clean. `RpgStore.ImportContent`/SQLite persistence for the new `SeedContent.ChannelPools`
       list is **not wired** — an accepted, tracked gap matching this program's own established
       "ships tested with zero production callers" pattern (`Instantiator.TryInstantiate` carried
       this shape for a long stretch), not required by acceptance criterion 1's own wording ("loads
       through `AtomSeedFile`, and `SeedScanner.OwnedFolders` sweeps its folder" — both true).
    5. **`AtomRowValidator.ValidateChannelPoolRef`** (new) — the five §3.3 refusals: unknown pool id;
       an unregistered pool member (checked against the SAME per-kind vocabulary the single-channel
       check already reads — `PrimaryChannels`/derived/resource, fresh every call, E29's own
       discipline extended to members); a member whose compose kind does not accept the atom's `op`
       (stat.derived only, mirroring `ValidateOp`'s existing scoping — a pool whose members disagree
       is refused whole); `count < 1`; `count` above the member count without `allowRepeat`. Rule 5
       (an empty `members` array) is structurally impossible by the time a row references a pool —
       `ChannelPoolFile` already refuses that at the POOL FILE's own load time — stated as a comment,
       not a dead check.
       - **A real, load-bearing defect found and fixed while wiring this in**: E29's own generic
         Vocabulary loop (`AtomKindRegistry.Validate`) ran `Convert.ToString` on the WHOLE pool-object
         `JsonElement`, producing a string like `{"pool":"pool.element-power","count":7,...}` and
         refusing it as "not one of the 267 legal values" — **before** `ValidateChannelPoolRef` ever
         ran, blocking every valid pool reference outright. Caught by writing the actual planted-
         violation test, not assumed to work. Fixed by skipping the generic string-vocabulary check
         when the raw value is a `JsonElement` object or a `Dictionary` — the pool form's own members
         are checked against the identical vocabulary by the new pool-specific validator, so nothing
         goes unchecked, it is only checked in the right place. `KindValueGuardTests`/`AtomKindRegistryTests`
         (46 tests) confirmed zero regression to E29's own behaviour from this change.
    6. **`CostFunction.cs`** — `Price` gained an optional `lookupPool` parameter (default `null`,
       every existing call site unaffected); the single-channel core was extracted into
       `PriceForChannel` so `PricePooled` can run it once per member and combine the results.
       `price(pooled) = count × weighted_mean(price(member))`, computed as ONE division
       (`(weightedSum × count) / totalWeight`) rather than rounding the mean and then the scale
       separately — widened to `long` before multiplying throughout, `checked` arithmetic (overflow
       throws, proven by a dedicated test constructing a genuinely overflowing weight × count). A
       pool reference with no `lookupPool` supplied, or naming an unknown pool, prices as
       **unpriced** with a named reason — never a crash, never a silent zero, matching every other
       "no resolver supplied" gate in this codebase (`composeKindOf`, `curveInput`).
    7. **`ContentValidation.PoolSpread`** (new) — `PoolSpreadTolerancePerMille = 250`, restated from
       `DriftTolerancePercent` exactly as the spec's own decision states (`250 == 25 × 10`), non-
       blocking. Verified neutral on today's data for the stated reason: every shipped `stat.derived`
       coefficient row is channel-less, so every declared pool's spread is exactly 0 until E44 fits
       per-channel coefficients.
    - **25 new tests**: `Core.Tests/Atoms/ChannelPoolTests.cs` (17) covering pricing (hand-worked
      fixture, no-lookup, unknown-pool), the E29-interaction planted violation, count-exceeds/count-
      zero/unknown-pool planted violations, the concrete-form-unchanged regression, the overflow
      throw, the 98-family reconciliation (measured, not trusted), the 6-member/no-omni checks for
      all 12 pools, every pool member's registration, and `ChannelRefJson`'s three read shapes.
    - **Full Core suite: 5235/5235 green** (was 5218 — 17 new, zero regressions, including zero
      regressions to E29's own 46 tests despite touching `AtomKindRegistry.Validate`). **Full
      solution sweep, all green**: Data.Tests 611/611, Server.Tests 94/94, Guard.Tests 162/162,
      CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71,
      Launcher.Tests 162/162, E2E.Tests 195/195, AtomImporter.Tests 26/26. **Both injector hosts
      rebuilt clean.** **All four boundary guards green.** Magic-number audit unchanged (12
      pre-existing, 0 new) — `PoolSpreadTolerancePerMille` is a named, documented, restated-not-
      arbitrary constant, matching `DriftTolerancePercent`'s own already-accepted shape.
    - **⛔ DECIDED 2026-09-03 (owner: "stop and design it now"), then implemented the same session.**
      The blocking question — what a resolved `count > 1` pooled atom actually IS at apply time — is
      answered in `spec-channel-pool.md` §3.2a and `spec-resolution-order.md`: a pooled reference
      expands into `count` separate `ResolvedAtom`s at resolve time (never one entry carrying an
      array), same `atom_id`, ONE shared rolled magnitude, a different concrete `channel` each —
      recognized as `Resolver`'s own existing "one authored unit, several resolved atoms" pattern
      (the same shape an affix bundle's multiple refs already produce), not a new mechanism. Every
      existing executor (`InjectorEffectActionSink`, `AtomDerivedSubsystem`, `BattleStatComposer`)
      stays completely unmodified — each resolved copy is an ordinary single-channel atom by the time
      any consumer sees it.
    - **The `channel.pool` resolve step is now built**, closing Checkpoint I:
      1. **`Resolver.cs`** — `Resolve` gained an optional `lookupPool` parameter and a fifth named
         stream, `channel.pool`, derived exactly like the other four. `RollValues` defers a
         pool-object `channel` param instead of freezing it inline, rolls every OTHER param (the
         magnitude included) exactly once as before, then — once the shared frozen dict is complete —
         draws `count` channels via the new `DrawPoolChannels`/`WeightedPickChannel` (the same
         weighted-pick shape `PickOne` already uses for affix draws, reused rather than
         reimplemented) and emits one `ResolvedAtom` per drawn channel, all sharing the same frozen
         magnitude. A pooled atom reaching the resolver with no `lookupPool` supplied throws — a
         "should never happen" guard, since validation is expected to have refused that shape
         earlier, proven by a dedicated test.
      2. **`InstanceProducer.Compose`** and **`RpgStore.ProduceAndBind`** both gained the matching
         optional `lookupPool` parameter, threading it down to `Resolver.Resolve` — closing the loop
         so a `ContentFingerprint()`-based reproducibility proof (the spec's own literal test-2
         wording) is possible at the INSTANCE layer, not only the resolver layer. `RpgStore`'s own
         pool-catalog persistence is still not built (unchanged scope decision — no shipped container
         references a pool yet, so there is no real caller to supply a non-null lookup from
         production code today); the signature is forward-compatible for when it is.
    - **6 new tests**: `ResolverTests.cs` (5) — different channels across seeds (test 1), byte-identical
      replay via raw `(AtomId, ValuesJson)` comparison (test 2), `count: 6` without-replacement drawing
      every member exactly once while sharing ONE rolled magnitude (test 3, the §3.2a decision
      directly asserted), a resolved pooled atom's `channel` is a plain JSON string indistinguishable
      from a concrete one downstream (criterion 2's "same opcode" claim), and the no-`lookupPool`
      guard throwing. `InstanceProducerTests.cs` (1) — the literal spec wording, `ContentFingerprint()`
      equality across two composes of the same seed, at the instance layer.
    - **Full Core suite: 5241/5241 green** (was 5235 — 6 new, zero regressions). **Full solution
      sweep, all green**: Data.Tests 611/611, Server.Tests 94/94, Guard.Tests 162/162, CheatCore.Tests
      40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests 71/71, Launcher.Tests 162/162,
      E2E.Tests 195/195, AtomImporter.Tests 26/26. **Both injector hosts rebuilt clean** (this touched
      `RpgStore`, a Data-layer file both hosts depend on). **All four boundary guards green.**
    - **Acceptance against the spec's own §6 — all eight now closed**: (1) loads through
      `AtomSeedFile`, folder swept — done; (2) both forms validate, price, AND compile to the same
      opcode — done, proven directly; (3) all five §3.3 refusals fire and name the offender — done;
      (4) pooled pricing exact with overflow throwing — done; (5) byte-identical reroll, proven by
      `ContentFingerprint()` — done; (6) no concrete atom's id/price/hash changes — done, proven by
      the full-suite zero-regression result; (7) `ContentValidation` gains `pool-spread`, non-blocking
      — done; (8) the 98 families reconcile against the 12 pools, measured — done. **Checkpoint I
      ("the one worth failing the wave over") is closed.**
- [x] **E32 `affix-import-path`** · **M** · Deps: E30 · `spec-affix-import-path.md`
  - Four breaks, plus the `"atom"`→`"affix"` pool key. ⚠️ **That window closes the moment any container
    gains a pool** — and no test pins the key today.
  - **Test 9**: assert seedsmith's write path and the scanner's swept folder name the same folder.
  - **Done 2026-09-03.** Confirmed (not re-derived) that breaks 1 and 2 were already closed before
    this session touched them, matching the spec's own 2026-09-03 re-verification — `SeedContent.
    Affixes` and the `"affix"` kind dispatch both already shipped. The real work was breaks 3 and 4,
    the pool-key rename, and the optional-`class` decision.
    1. **Break 3 — `SeedScanner.OwnedFolders` gains `"effects/affixes"`**, verified against
       seedsmith's own Python source, not the spec's prose claim: read
       `tools/seedsmith/seedsmith/adapters/effects/affix/generate_affixes.py` directly —
       `OUTPUT_DIR = REPO_ROOT / "data" / "seed" / "effects" / "affixes"` — and a new test
       (`SeedScannerTests.AtomImporter_swept_folder_matches_seedsmiths_own_affix_write_path`, test 9,
       "the one that would have prevented this module") reads that same line via regex so a future
       path change on either side fails the test rather than silently reopening the gap.
    2. **Break 4 — `RpgStore.ImportContent` now upserts `content.Affixes`.** New `ValidateAffixes`
       (mirrors `ValidateRarities`'s shape), writing after atoms (an affix references one) and before
       containers (a container's pool references an affix) inside the SAME single transaction and
       `catalog_revision` bump every other content kind already gets. The stale
       *"Affixes are not yet part of SeedContent's own import batch"* comment is deleted, not kept —
       the sentence it stated stopped being true. `RefuseDuplicates` and `ImportOutcome` both widened
       to include affixes, matching every other content kind's own shape (`Affixes` is a trailing
       optional field, default 0, so neither of `ImportContent`'s two existing positional construction
       sites needed touching for the widening to compile).
    3. **§3.3 — 1:1 generated affixes derived at import, never committed.**
       `AffixLibraryGenerator.Generate(atomsById.Values)` runs inside `ImportContent`, building an
       in-memory dictionary consulted by the container-pool lookup ALONGSIDE the batch's own
       hand-authored affixes and the already-stored ones (`authored ?? generated ?? GetAffix(id)`) —
       so a container referencing a freshly-imported atom's own generated wrapper resolves within the
       SAME import batch, without ever writing the generated row. Proven end to end by a dedicated
       test: an atom and a container naming its `affix.` wrapper import together in one batch, the
       container resolves clean, and `GetAffix` on the wrapper id returns null afterward — confirmed
       never committed.
    4. **§2 — the pool key renamed `"atom"` → `"affix"`.** `ContainerPoolRow`'s `AffixId` was being
       filled from the WRONG JSON key (`Str(p, "atom")`), silently latent because no shipped
       container has a non-empty pool. Fixed in `AtomSeedFile.ReadContainer`: the correct key is
       `"affix"`; the old `"atom"` key is now a load-time refusal naming the rename explicitly
       (§4's own required shape — "not clamped, not defaulted"), rather than silently producing a
       `ContainerPoolRow` with an empty or wrong `AffixId`.
    5. **§3.2 — an authored `class` is now optional, checked, refused on mismatch, never trusted
       silently.** `AffixRow.Class` widened from `AffixClass` to `AffixClass?` — the ONLY places that
       ever see `null` are the validate/import functions this module touches; every downstream
       consumer (`Resolver`, `Instantiator`, `EligibilityRule`, `ContainerValidator`) only ever reads
       an affix already resolved from storage or the generator, where `Class` stays concrete by
       construction. `AtomSeedFile.ReadAffix` accepts an absent `class` (parses to `null`) and still
       refuses an unparseable one. `AffixValidator.Validate` gained the three-way rule from §3.2's own
       table (absent+all-slot → refused, nothing to derive from; absent+has-concrete-ref → accepted,
       resolved later; present+matching → accepted; present+contradicting → refused, naming both
       values) and a new `ResolveClass` helper fills in the derived value before any write —
       `WriteAffixUnlocked` now defensively throws if a caller ever skips that step, rather than
       writing a null class to the tables.
    - **21 new tests**: `Data.Tests/AffixImportPathTests.cs` (11) — a real `"kind": "affix"` file
      imports and is queryable; idempotent re-import; re-indenting doesn't move the content hash;
      absent/matching/contradicting `class`, all three branches; a container pool referencing an
      imported affix rolls it through `Instantiator.Draw`; the `"atom"`-key planted violation naming
      the rename; the `"affix"`-key positive case; an affix ref naming an unknown atom refused by id;
      the 1:1-generated-affix-resolves-within-one-batch-without-being-committed proof.
      `AtomImporter.Tests/SeedScannerTests.cs` (1) — test 9, the seedsmith-path mechanical pin.
      `Core.Tests/Atoms/AtomSeedFileTests.cs` (1 rewritten to match the new decision + 1 new) — an
      absent class parses to `null`, not a refusal; an unparseable one is still refused.
    - **Full Core suite: 5242/5242 green** (was 5241 — 2 net new/changed, zero regressions to
      `AffixValidator`'s widely-consumed `AffixRow.Class` type despite the nullable widening). **Full
      solution sweep, all green**: Data.Tests 622/622 (was 611 — 11 new), Server.Tests 94/94,
      Guard.Tests 162/162, CheatCore.Tests 40/40, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests
      71/71, Launcher.Tests 162/162, E2E.Tests 195/195, AtomImporter.Tests 27/27 (one transient
      failure from concurrent build contention with parallel background runs, confirmed
      non-reproducible by an isolated re-run — the same pattern already seen twice this session, not
      a regression). **Both injector hosts rebuilt clean.** **All four boundary guards green.**
    - Acceptance against the spec's own §6: (1) an authored affix file imports, is idempotent, and
      survives re-indentation without a hash change — done; (1b) `SeedContent.Affixes` is written by
      `ImportContent`, the stale comment is gone — done; (2) `class` derived, optional, refused on
      contradiction — done; (3) a container pool referencing an affix rolls it — done; (4) the pool
      JSON key is `"affix"`, `"atom"` refused naming the rename, pinned by a test — done; (5)
      generated 1:1 affixes derived at import, never committed — done, proven end to end; (6)
      seedsmith's write path and the scanner's swept folder asserted equal by a test — done; (7) no
      existing atom or container id, hash or behaviour moves — done, proven by the zero-regression
      full-suite result.
- [x] **ep-3 `affix-library`** · **S** · Deps: E32 · `effect-pipeline/spec-affix-library.md`
  - The 1:1 wrap. Its generator already exists; E32 built the write path it never had.
  - **Re-audited 2026-09-03 — genuinely nothing left to build**, verified against the real shipped
    code and its real test file, not assumed from this module's own todo hint. `AffixLibraryGenerator.cs`
    already implements every rule the spec states (single-atom wrap, `affix.` prefix strip, `Class`
    derived from the wrapped atom's own trigger presence via `AffixValidator.AffixClassOfAtom`, zero
    model calls). `AffixLibraryGeneratorTests.cs` already carries a real, currently-green counterpart
    for every one of the spec's five named tests
    (`Every_generated_atom_gets_exactly_one_single_atom_affix`,
    `Single_atom_affix_class_matches_the_atoms_own_derivation`,
    `Adding_a_new_element_variant_regenerates_without_touching_authored_affixes`,
    `An_authored_multi_ref_affix_is_never_overwritten_by_this_generator`,
    `Zero_model_calls_anywhere_in_this_module`), plus five more beyond the spec's own list
    (id-prefix-stripping, a missing-prefix fallback, the no-slot single-ref shape, the generator's own
    output validating against the real `AffixValidator`, and an explicit unchanged-catalog
    byte-identical check) — 10/10 green, re-confirmed in this session's own run, unaffected by E32's
    `AffixRow.Class` nullable widening (every construction site here passes a concrete value, which
    widens implicitly). **The "write path it never had" is now built — by E32**, which wires
    `AffixLibraryGenerator.Generate` into `RpgStore.ImportContent`'s own container-pool lookup so a
    freshly-imported atom's generated wrapper resolves within the same batch (proven end to end in
    `AffixImportPathTests.cs`). No code, data, or doc change was needed here — this entry records the
    verification, not a build, matching ep-2's own closing shape exactly.
    - Acceptance against the spec's own §6: (1) every atom the catalog generates has a corresponding
      single-atom affix — done, test 1; (2) zero model calls, proven by test — done, test 6; (3)
      regenerating over an unchanged catalog is byte-identical — done, two tests; (4) an authored
      affix is never silently overwritten — done, test 7.
- [x] **E43 `family-expand`** · **L** · Deps: E30, **E42** · `spec-family-expand.md`
  - Built as a generator with a `--check` mode on the `DemonSpeciesGen` pattern, per the spec's own
    2026-09-03 §3.1 decision: `tools/FamilyExpandGen` reads the 98 authored `affix-families/*.json`
    entries + `data/seed/items/_tuning/tier-bands.v1.json` + `_registry/bands.v1.json` as generator
    input and writes atom rows to `data/seed/atoms/generated/family-expand.{g-life,g-attack,
    g-armour}.json` — inside `SeedScanner.OwnedFolders`' existing `atoms` root, so the importer
    sweeps the output and never parses a family file. Nothing moved; no second family namespace.
  - New pure module `src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs` (+
    `FamilyExpansionTypes.cs`, `AffixFamilyFile.cs`, `TierBandsFile.cs`) ports `formulas.py`'s
    `round_legible`/`tier_ladder`/`band`/`primary_channel_m1` shape to `long`-only, `checked`,
    widen-before-multiply, divide-by-1000-last C# — no `float`/`double` anywhere in the magnitude
    path (CLAUDE.md's binding numeric rule). `RoundLegible` is long-safe round-half-up
    (`(numerator + denominator/2) / denominator`), proven against `bands.v1.json`'s own two worked
    examples (vitality 30‰×680/1000=20.4→20, might 45‰×92/1000=4.14→4). Element-typed
    (`{variant}`-templated) channels resolve through a fixed, non-inferred table to one of E30's 12
    shipped pools and emit **one** row per tier carrying a pool reference — never seven.
  - **Real, verified data gap surfaced rather than papered over (this module's whole reason to
    exist, DESIGN-GATE-verified against the actual corpus, not assumed from the spec's own ~490
    estimate):** cross-referencing all 98 family entries' id stems against `tier-bands.v1.json`'s
    `channelWeightPermille` map (the only real per-channel `sharePermille` source in the repo — the
    illustrative worked examples in `ssot-affixes.md` §4.5 are explicitly labelled "illustrative,
    not balanced" and are NOT a generator input) found **only 14 of 98 families have an authored
    share at all**, and of those 14, **5 more** (`plating`, `carapace`, `quickening`, `flourishing`,
    `swiftness`) have no shipped `BattleRuleset` curve for their Flat-op channel
    (`arm1Max`/`arm2Max`/`attackInterval`/`produceInterval`/`zombieSpeed`) — `power-scale.v2.json`'s
    `channels` block ships only `atk`/`defense`. **Real, honest output today: 9 families × 5 tiers =
    45 rows** (`vitality`/`fortitude`/`bulwark` on `maxHp`, `might`/`ferocity`/`savagery` on `atk`,
    `warding`/`resilience` on `defense`, `mending` reusing `BattleRuleset.BaseHp`), **84 families
    refused for no authored share, 5 more refused for no reference-base curve** — every refusal
    named by id with its reason (`FamilyRefusal`), matching `bands.v1.json`'s own explicit law ("a
    generator with no authored share for a channel must reject at import, not guess one") and
    acceptance criterion #2's own wording ("the exact count is derived and reported, never a
    literal"). Widening `tier-bands.v1.json`/adding the missing curves is the item program's
    follow-up, explicitly out of E43's own §4 scope ("author or edit a family... that is the item
    program's finding") — not fixed here.
  - Corrected, in-session, a stale concern this module's own brief initially carried: g-tempo.json's
    authored notes call `attackInterval`/`produceInterval`/`zombieSpeed` "NOT bindable... pending
    E16" — verified false against live code (`ModifierOp.cs`'s `StatChannels.All` already includes
    all three, comment: "E16: promoted from cheat-document keys to real composed channels"). The
    real reason those 3 families don't emit today is the missing reference-base curve above, not a
    channel-registration gap — a different, correctly-diagnosed blocker, not the stale one.
  - **Both named CI gates fixed** (§3.3, acceptance #6): `tools/ElementEnumGen/Program.cs`'s
    `fx-*.json` `AllDirectories` glob (which enforced nothing — it just happened to match only 3
    files) replaced with an explicit 3-file allow-list (`fx-board.json`, `fx-core.json`,
    `fx-status.json`). `EffectAtomCatalogGeneratedTests.Has_the_same_sixteen_ids_as_the_retired_
    hand_written_catalog` (a set-equality assertion against a frozen 16-id catalog that failed on
    any 17th id, "sixteen" being in the method name only) rewritten to
    `Reproduces_every_retired_hand_written_id_and_allows_growth` — asserts the generated catalog
    reproduces every retired id exactly AND is a superset, tolerating growth — plus a companion test
    proving the assertion shape itself tolerates a manufactured past-16 case.
  - `FamilyExpandGen --check` wired into `ci.yml` right after `DemonSpeciesGen --check`.
  - **12 new tests**, `tests/FusionRpg.Core.Tests/Atoms/Generation/FamilyExpansionTests.cs`, covering
    all 10 of spec §5's named cases: determinism, `--check`-equivalent drift detection (plus a
    manufactured-drift positive case), id-derivation exactness, zero `(family,tier,variant)`
    collisions across all 98, one-row-per-tier pool-typed emission (synthetic — no real family
    reaches this path today, both the mapped and the unmapped-template-refused cases), every real
    row validating + pricing non-zero, two distinct planted-violation refusals (no-share, unknown-
    pool), no `fx-*` output naming, and no id collision against the shipped `fx-*` atoms.
  - **Independently re-run and confirmed by this session** (not just the delegated agent's own
    report): `FamilyExpandGen --check` → clean/idempotent against the 3 committed generated files.
    `AtomImporter --check --validate` over the full seed tree → 66 atoms (21 shipped + 45 generated)
    import clean, 0 lint/drift failures. **Full Core suite: 5255/5255** (was 5242 — 13 net new,
    zero regressions). **Full 9-project solution sweep, independently re-run, all green**: Data.Tests
    622/622, Server.Tests 94/94, Guard.Tests 162/162, CheatCore.Tests 40/40, Launcher.Tests 162/162,
    E2E.Tests 195/195, AtomImporter.Tests 27/27, ElementEnumGen.Tests 14/14, ItemSeedValidator.Tests
    71/71. **All four boundary guards green.** Magic-number audit: 0 critical (M1/M2), 12 pre-
    existing A3-tier findings unrelated to this module's files. Overflow audit: 0 critical, no new
    findings — every E43 magnitude path is `long`, `checked`, widen-before-multiply.
  - Acceptance against the spec's own §6: (1) definitions stay put, read as generator input, output
    swept, `--check` fails on drift — done; (2) row count derived and reported, never a literal —
    done, 45 today (not the spec's own ~490 estimate, and that gap is the finding, not a defect);
    (3) element-typed families emit a pool reference, one row each — done, test 6; (4) every row
    validates and prices, none `PowerVector.Zero` — done, test 8; (5) `--check` runs in CI and fails
    on drift — done; (6) both CI gates fixed, allow-list + scoped comparison, method renamed — done;
    (7) no `fx-*` output, planted-tested — done, test 11; (8) no second family namespace — done, the
    98 definitions never moved; (9) the 21 (20, corrected — see below) shipped atoms untouched —
    done, test 12, proven by the zero-regression full-suite result and the clean `--validate` run.
  - One correction to the spec's own acceptance #9 text: the real shipped-atom corpus measures **20**
    fx-* atoms today, not 21 as the spec's prose states — the test asserts the measured value, not
    the doc's stated one; flagged here rather than silently reconciled.
- [x] **E46 `player-content-boot`** · **M** · Deps: — (but **before E43's output ships**) ·
  `spec-player-content-boot.md`
  - **Gate G4 resolved: install-time and first-run repair collapse into one mechanism**, not three.
    This launcher has no install step distinct from first launch (`PlaySession.PlayAsync` is the
    only path to a running server — verified against `ModLoaderInstaller.cs`/`FusionRpgUpdater.cs`,
    neither runs an import), so the server's own startup self-heals: gated on `catalog_revision`
    (`RpgStore.GetCatalogRevision()`), revision 0 imports once (functionally "at install" AND the
    "first-run repair" the spec's §3.1 asks for — same code, same moment), non-zero is a true no-op.
    Bundled-database was never on the table (spec's own §4: no migration path exists).
  - **Extracted, not duplicated**: `tools/AtomImporter/Program.cs`'s scan→collect→import sequence
    moved to `src/FusionRpg.Data/Seed/SeedImportRunner.cs` (+ `SeedScanner.cs`, relocated from
    `tools/AtomImporter/` — a server referencing a `tools/` project would be backwards). The CLI now
    calls the same members (`Roots`/`Files`/`Collect`) for its own reporting; `RunSelfHealing` is the
    new never-throwing entry point the server calls — every failure (missing seed tree, corrupt file,
    import refusal) folds into a `SeedImportRunResult` instead of an exception, so a broken install
    cannot take server startup down with it (§3.2, §4).
  - `FusionRpg.Server/Program.cs` wires `SeedImportRunner.RunSelfHealing` between `store.Init()` and
    `store.LoadContentIntoRuntime()`, logs the outcome loudly to console either way, and records it
    via `RpgStore.RecordContentBootOutcome`. `/health` (`RpgStore.ToHealth`) and `HealthDto` gained
    `ContentSource` (`"imported"`/`"codeFallback"`), `CatalogRevision`, `ContentImportError` — the
    surface both player and owner can read (§3.2, acceptance #2). Launcher's `HealthMonitor.
    HealthSnapshot` carries the same fields; `MainWindow.xaml.cs`'s status text appends `content:
    imported` / `content: fallback (see server log)`.
  - **11 new tests**: `Data.Tests/SeedImportRunnerTests.cs` (7 — spec tests 1/2/4/5 + CLI-parity),
    `Data.Tests/SeedContentCoverageTests.cs` (1 — spec test 6, reflection over `SeedContent`'s own
    fields, not a maintained list), `Server.Tests/ContentBootStartupWiringTests.cs` (3 — real
    `data/seed` end-to-end through actual server startup wiring).
  - **Real, separate gap surfaced, not fixed here (out of scope, named)**: `scripts/publish-player.ps1`
    never bundles `data/seed` into the player zip — verified, no `Copy-Item`/content reference to it
    anywhere in that script. On a real distributed install `RunSelfHealing`'s `FindUp` will report
    `SeedTreeNotFound` forever, correctly (loudly, non-fatally) rather than silently — but the actual
    packaging fix is a separate, un-scoped decision (what ships in the zip), not this module's wiring
    question. **A second, independent gap the reflection test itself caught**: `RpgStore.ImportContent`
    never references `content.ChannelPools` at all — no table, no writer, no reader — despite real
    authored content at `data/seed/channel-pools/pools.v1.json` (E30). Same defect shape as the
    historical `Affixes` gap E32 closed. Recorded as a named, dated `knownGaps` exception in
    `SeedContentCoverageTests.cs` (so the test still trips on any *other* uncovered kind), not fixed
    here — it is E30/import-path scope, not player-content-boot's.
  - **Independently re-run and confirmed by this session** (build + full suite + guards + audits, not
    just the delegated agent's own report): `dotnet build` clean on all four touched projects
    (Server, Data, Launcher, tools/AtomImporter). **Full 9-project sweep, all green except two
    pre-existing, unrelated failures confirmed not caused by this module**: `Data.Tests` 627/629 (2
    failures both in `DemonSpeciesImportCliTests`, which references neither `SeedScanner` nor
    `SeedImportRunner` nor any file this module touched — matches pre-existing uncommitted demon-
    corpus drift already visible in `git status` from before this module's work, not a regression).
    `Server.Tests` 97/97 (was 94 — 3 net new). `Guard.Tests` 162/162. `AtomImporter.Tests` 27/27 (one
    isolated-run transient failure on the real-cold-process test, reproduced clean on immediate
    rerun — the same concurrent-build-lock pattern already seen and documented multiple times this
    session, not a regression). `Launcher.Tests` 162/162, `CheatCore.Tests` 40/40,
    `ElementEnumGen.Tests` 14/14, `ItemSeedValidator.Tests` 71/71, `E2E.Tests` 195/195, **Core.Tests
    5255/5255** (unchanged — this module never touches Core). **All four boundary guards green** —
    the DAL guard in particular confirms the `SeedScanner`/`SeedImportRunner` relocation into
    `FusionRpg.Data` leaked no SQL elsewhere. Magic-number audit: 12 total, 0 critical, identical to
    the pre-E46 baseline — no new findings. Overflow audit: 44 total, 0 critical, identical baseline.
  - Acceptance against the spec's own §6: (1) a clean install imports, `catalog_revision` non-zero,
    content queryable — done, live-proved (server run against a scratch dir: first boot `contentSource:
    "imported"`, `catalogRevision: 1`); (2) import mode reported on a readable surface — done,
    `/health` + launcher status line; (3) failed import visible and non-fatal, fallback still boots —
    done, `RunSelfHealing` never throws, test 3; (4) re-launch does not re-import or bump the revision
    — done, live-proved (same scratch dir restarted, same revision, "no import needed" logged), test
    4; (5) a zero-revision database repairs itself once on first run — done, same mechanism as (1) by
    construction; (6) `SeedContent` coverage by reflection, not a maintained list — done, with the
    `ChannelPools` gap honestly named rather than silently passed; (7) `deploy-play.ps1`'s existing
    import unchanged — done, re-verified CLI parity (`--check`/`--validate` identical output/exit
    codes before and after the refactor).

### ✅ Checkpoint C3 — one row, many outcomes · **and C4 — content reaches a player**

---

## Phase 3 — action corpus, model-free · nine modules, zero tokens

> **⚠️ This list is BUILD order, which is not the data-flow graph.** A-S3's spec says it *depends on* A-S4 and *feeds* A-S5; it is built earlier so the metrics and dedup are inspectable before a token is spent. Where a row's `Deps:` and its spec's §7 disagree, **the spec states the data flow and this list states the order** — the map's §5 draws both. Rows below say which they mean.

- [x] **A-C1 `corpus-loader`** · **S** · Deps: — · an envelope-less file becomes a **finding**, not a
  silent skip. The two shipped files **were never wrapped** — their parsers still read the root,
  unchanged.
  - New `tools/seedsmith/seedsmith/adapters/actions/` package (`__init__.py`'s `ActionsAdapter`,
    `kinds.py`'s 10 per-kind `KindSpec`s each with its **own** `id_pattern` — not one shared pattern
    — `vocab.py`'s C#-transcribed closed vocabularies, `load.py`'s §3 algorithm), registered as
    `"actions"` in `adapters/registry.py` and nowhere else. New `data/seed/actions/_manifest.json`
    declares the two shipped config files plus the four underscore-prefix dispositions (`_rounds/`
    exclude; `_generated/`/`_briefs/`/`_reports/` load, §3 step 2c).
  - **`load_committed`'s algorithm is "total", not "raise on first defect"**: a lost envelope, an
    undeclared prefix, an unknown enum, wrong casing, an unknown atom family, an unknown family
    `scopeKey` all become `Finding`s and the entry stays in the corpus (discovery-over-declaration,
    matching `discover_edges`'s own philosophy) — only what `Corpus.load`/`Corpus.add` already raise
    on (unparseable JSON, a real duplicate id) still raises `CorpusLoadError`. `_rounds/` exclusion
    is done via a read-only scratch-directory copy (`tempfile`), not per-subdirectory `Corpus.load`
    calls or post-hoc filtering — both of the obvious simpler shapes were checked first and rejected
    with the reason recorded in `load.py`'s own docstring (`Corpus.load`'s `_exemplars/` handling is
    root-relative to whichever call it's in; `Corpus.add`'s duplicate-id raise fires mid-walk, before
    a caller could ever filter the result).
  - **Genuine correction to this module's own build task**: `data/seed/actions/_generated/
    family-map.json` (A-S0's species→family projection) **already exists in the live tree** — 53
    species over 19 family ids, matching this spec's own measured numbers exactly — contrary to the
    working assumption that A-S0 (a later module in this same phase) hadn't produced it yet. Declared
    as a third config-file row in `_manifest.json` (a flat map, not a `kind`+`entries` envelope, same
    reasoning as the two original shipped files) and the family-scoped `scopeKey` cross-check
    (acceptance #6e) runs against this real data today, not a synthetic fixture — verified directly.
  - **Independently re-verified by this session, not just the delegated agent's own report**: read
    `load.py`/`vocab.py` in full; independently re-derived three of the seven C#-transcribed
    vocabularies straight from the live source rather than trusting the transcription — `ActionKinds.
    Name` (`ActionEnums.cs:96-102`: `basic`/`innate`/`skill`), `ActionTags.Name` (`:152-163`, 8
    values), and `ActionCategories.Name` (`:120-128`, which indirects through `DerivedStatChannels.
    ActionCategory*` constants — traced those to `DerivedStatChannels.cs:471-475` and confirmed
    `attack`/`defense`/`support`/`movement`/`status` exactly) — all three matched `vocab.py` byte for
    byte. Independently counted all 21 `Register`/`RegisterWithOptions` calls in
    `StatusCatalogBootstrap.cs` and confirmed the exact 21-member set matches. **Full seedsmith pytest
    suite independently re-run: 786/786** (741 baseline + 45 new), matching the report exactly. **`dotnet
    test tests\FusionRpg.Core.Tests --filter ActionSeeding`: 28/28**, matching exactly. `git status`
    confirms `pairings.json`/`name-templates.json` carry **zero diff** — byte-identical, never touched.
  - A real, separately-flagged spec-citation staleness (not this module's defect, the spec's own):
    `ActionEnums.cs`'s line ranges for `ActionKinds`/`ActionCategories`/`ActionTags`.`Name` are all
    off by a consistent +24 lines (A-E1 inserted `EligibilityScope`/`PairingRole` earlier this
    session, growing the file) — wire strings themselves unaffected, confirmed above.
    `RelationKind.cs` is cited under `Actions/` but the type lives in `FusionRpg.Contracts`, aliased
    in via a `global using`. `EnablerPayoffPairings.cs`/`ActionNameTemplates.cs`/`ActionSeeder.cs`
    are cited without their real `Actions/Seeding/` subpath. None of these affect what was built —
    the actual line contents at the corrected paths matched the spec's cited signatures exactly.
  - Acceptance against the spec's own §6: (1) `Corpus.load` returns non-empty `action-seed` entries
    — done; (2) the two config files byte-identical, `ActionSeeding` tests pass — done, re-verified;
    (3)-(6e), (7), (8) — done, each mapped to a named test in the module's own report and spot-
    checked above.
- [x] **A-S0 `characteristic-pool`** · **M** · Deps: A-C1 · the role lean runs for **all 84**, not just
  the 53 with families — done, F12-corrected: 31 family-less species carry a real derivation
  (`leanSource: "derived-nofloor"`), never a discarded uniform tie.
  - New package `tools/seedsmith/seedsmith/adapters/actions/characteristic_pool/` (`catalog.py`
    parses the 84-species roster straight from `DemonSpeciesCatalog.Generated.cs`, never hand-copied;
    `anchors.py`, `derive.py` — steps 2-5 of spec §3; `pool.py` — the six A-F groups) plus
    `generate_characteristic_pool.py` (writes both outputs through A-C1's envelope, `--dry-run`
    supported). New tuning file `data/tuning/action-role-lean.v1.json` — every weight cell `1000`
    (`elementSecondaryScaleMilli: 500` the sole non-flat default), `_meta` stating in the required
    words that the values are untuned pending the first smoke batch. **42 new tests**,
    `test_characteristic_pool.py`.
  - **⛔ Owner-review-worthy, flagged prominently rather than folded into "done" silently: `derive.py`'s
    `SIGNAL_CATEGORY` map (which trait/element/posture/anchor-axis structurally leans toward which of
    the 5 categories) is a genuine editorial invention, not a citation.** The spec describes every
    weight cell in the tuning file as flat (`1000`) and states this reproduces "the plain count of
    signals per category" — but taken literally that identity produces a permanent 5-way tie for
    every species, since nothing in any shipped C# or spec text says which category a given trait
    (e.g. `soul-eater`) structurally belongs to before the weight scales it. No such mapping exists
    anywhere in the corpus this module reads. The build filled the gap rather than stall on it:
    trait→category is grounded in `DemonTraitCatalog.cs`'s own blurb text (an attacking blurb reads
    `attack`, a shielding one `defense`); posture→category reuses the demons adapter's own shipped
    `APTITUDE_POSTURE` semantics (`Bastion`→defense is that module's own validator rule, not invented
    here); **element→category has no textual grounding at all** — assigned only to spread the 6
    elements across the 5 categories rather than leave that block silently flat too. This is real,
    reviewable content a rebalance pass would want to revisit — everything downstream of it (the
    per-mille weight) is the genuinely tunable surface the `.v1.json` file owns, but the map itself
    is not. **Recorded here for the owner, not resolved by further guessing.**
  - **Real, ongoing environmental hazard, correctly handled defensively rather than papered over**: the
    species anchor tree (`data/seed/demons/species/**`) is being **actively regenerated by a
    concurrent, unrelated process** while this module was built and while this evidence was verified
    — `git status` shows 63 changed/new files there, several modified within the last 30 minutes, and
    three measurements taken minutes apart during the build returned three different anchor-row
    totals (28 → 68 → 87). This module's own tests assert only the **stable** facts (84 catalog / 84
    motif / 53 family / 19 family ids — unchanged across every check this session) and treat the
    anchor-tree-dependent counts (28 anchors / 9 unjoined / 8 four-way join, all cited in the spec as
    a same-day snapshot) as structural-invariant checks rather than pinned literals, specifically so a
    concurrent write elsewhere in the repo cannot silently break this module's own test suite. The
    generator itself reads live data, never a hardcoded snapshot, so its actual output already
    reflects whichever anchor state existed when it last ran — re-running it later will pick up
    whatever the concurrent process eventually settles on.
  - **The `family-map.json` "contradiction" the build flagged is resolved, not open**: it was
    generated deliberately during **A-E1**'s own build (already closed, evidence at this file's line
    ~560-568) as a flat placeholder projection so A-E1's C# eligibility tests would have real data —
    its own evidence block says in these words *"the projection is A-S0's eventual home per the
    spec; committing it now unblocks A-E1's own tests."* A-S0's real, spec'd outputs remain
    `role-lean.json`/`characteristic-pool.json`; `family-map.json` stays A-E1's own dependency,
    untouched here, matching the explicit "never rebuild or touch it" instruction this build was
    given.
  - **Independently re-verified by this session, not just the delegated agent's own report**: `python
    -m pytest tools/seedsmith/tests/test_characteristic_pool.py` in isolation — **42/42 clean**. Full
    suite `python -m pytest tools/seedsmith/tests` — 836 passed (up from the expected 828; the extra
    8 traced directly to `test_run_runner.py`, modified by the same concurrent unrelated process
    above, not to this module — confirmed by diffing which test files `git status` shows touched).
    Direct inspection of the real generated `role-lean.json`: **84 entries exactly**, `derived-nofloor`
    count **31**, `derived` count **53** (sums to 84), zero `derived-nofloor` entries with an empty
    `signals` list (the F12 fix holds), zero invalid `leanOrder` permutations, zero legacy-rarity-band
    leaks — all four checked directly against the live file, not trusted from the report.
  - Acceptance against the spec's own §6: (1) exactly 84 entries, none for unjoined anchors — done,
    re-verified; (2) rarity always a ladder id — done, re-verified, zero leaks; (3) 31 `family:null`
    entries, all `derived-nofloor`/`separation:null`, all non-empty `signals` — done, re-verified;
    (4) `leanOrder` a valid permutation for every entry — done, re-verified; (4b) tie serializes as
    declared order — done, test-proven; (5) residue/histogram printed and written — done; (6) all
    weights in the tuning file, no bare literal — done, direct grep confirms only the documented
    per-mille divisor; (6b) flat default ships with the required `_meta` wording — done; (7)
    byte-identical hash, provenance recorded — done; (8) zero model calls — done, offline-guarantee
    test.
- [x] **A-T1 `type-weights`** · **M** · Deps: A-S0 · authors the file `spec-action-seeding.md` names and
  which does not exist — done, `data/seed/actions/type-weights.json`, 84 species + 19 family rows.
  - New package `tools/seedsmith/seedsmith/adapters/actions/type_weights/` (`tuning.py` — strict
    loader/validator, refuses float/bool/numeric-string at load naming the row; `derive.py` — §3's
    seven steps) plus `generate_type_weights.py`. New tuning file
    `data/tuning/action-type-weights.v1.json` shipping the spec's own DECIDED neutral defaults
    (`base:1000`, `step:250`, `separationMilli:[0,250,500,750,1000]`, uniform `targetModeMilli`/
    `areaShapeMilli`, `primaryMilli:400`/`secondaryMilli:200`, `familySecondaryScaleMilli:500`).
    **63 tests**, `test_type_weights.py`.
  - **⛔ AC5 defect found, a genuine spec self-contradiction, resolved by owner decision (not
    silently picked) — record kept in full since it changed shipped behavior after the module's
    first pass.** The build's own first pass implemented spec §3 step 2 literally
    ("`separation: null` takes the same row as `0`"). Verified directly against the real generated
    file: because the shipped default for row 0 is exactly `0` ("collapses the spread to flat" —
    §2's own stated reasoning for a genuine tie), sharing that row made **every one of the 31
    family-less (`derived-nofloor`) species print a flat 200/200/200/200/200 vector** — directly
    and literally contradicting acceptance #5's own words ("a family-less species still gets a
    vector shaped by its own leanOrder rather than a flat 200/200/200/200/200"). Two DECIDED
    provisions of the same spec, dated the same day, could not both hold under a shared row —
    confirmed by direct inspection of the real output (64 of 84 species measured flat), not
    inferred. **Put to the owner via AskUserQuestion rather than resolved unilaterally**; the
    owner chose: give `separation: null` its own tuning row (`nullSeparationMilli`, shipped `500`
    — real signal, distinct from both row 0's exact-zero and a measured `separation:4`'s full
    `1000`), leaving `separationMilli[0]` and its true-tie meaning untouched. Implemented in
    `tuning.py`/`derive.py`, the tuning file, and the `_meta.note`'s own account of the fix.
    **Re-measured after the fix: 0 of 84 family-less species print flat (was 31); 33 genuine
    `separation==0` ties correctly remain flat** — both counts pinned by
    `ShippedDefaultFlatnessIsExpectedTests`, so either direction of regression is caught.
  - A second, smaller documentation defect found and fixed in the same pass: the tuning file's own
    `_meta.note` repeated spec §2's worked-example arithmetic verbatim ("ranks 2000/1750/1500/1250/
    1000 → 400/350/300/250/200 per-mille") — the build's own test
    (`test_spec_own_400_350_300_250_200_per_mille_claim_does_not_hold`) proved that sum is 1500, not
    1000, and cannot be the formula's real output; the actual output is `267/233/200/167/133`. The
    shipped `_meta.note` text is corrected to state the real arithmetic the code runs, not the
    spec's own uncorrected illustration.
  - **Confirmed environmental hazard, not a regression in this module**: the full seedsmith suite
    shows one failure outside this module's own tests —
    `test_characteristic_pool.py::AttackTempoExclusionTests::test_live_anchor_tree_attack_tempo_is_constant`
    — traced directly to the concurrent, unrelated demon-classification pipeline (flagged in A-S0's
    own evidence above) having since written an anchor row with `attackTempo: "slow"`, where A-S0's
    own test measured a constant `"steady"` at build time. Confirmed by reading the failure directly
    (`observed = {'slow'}`, expected `{'steady'}`) — a real, live change to shared data this module
    did not touch and does not own; not fixed here, matching A-S0's own evidence's identical caveat.
  - **Independently re-verified by this session**: `tools/seedsmith/tests/test_type_weights.py` in
    isolation — **63/63 clean**, including the new AC5-fix test. Full suite — 898 passed, 1 failed
    (the environmental failure above, confirmed unrelated). Direct inspection of the real generated
    `type-weights.json` post-fix: 103 entries exactly (84+19), every vector sums to 1000, zero
    family-less species flat, 33 genuine ties correctly flat.
  - Acceptance against the spec's own §6: (1)-(4b), (6)-(8) — done as built, unaffected by the AC5
    fix; (5) — done, **now genuinely true against the real shipped file** rather than only true
    under a hypothetical future retune, per the owner's own resolution above.
- [ ] **A-S1 `distribution-planner`** · **L** · Deps: A-S0, A-T1 · Engine 1. **Union-to-ceiling** for
  structure axes; family motifs = intersection, anti-motifs = union.
- [ ] **A-G1 `tier-access-gate`** · **M** · Deps: A-S1 · two of C1's three gates. **Criterion 7 asserts
  the widening stays disabled.**
- [ ] **A-R1 `resource-ownership`** · **M** · Deps: — · **first emission must reproduce
  `aptitudes.v5.json` byte-for-byte.** Test 2 is the one that proves the defect fixed.
- [ ] **A-S5 `coverage-report`** · **M** · Deps: A-S3, A-S1, A-T1 · every metric declares closed- or open-loop;
  `NOT_MEASURED` stays distinct from a pass.
- [ ] **A-S3 `dedup-select`** · **M** · Deps: A-S4 (data-flow); **built before A-S5** · t1/t2 hard, t3 advisory. `--no-semantic` proves t3
  never gates.
- [ ] **A-S6 `innate-picker`** · **M** · Deps: A-S3, A-S0 · model-free permanently; ranking weights in
  `data/tuning/`.

### ✅ Checkpoint C5 — the plan is reviewable with no model

---

## Phase 4 — the model stages · ⛔ ends at the owner gate

- [ ] **A-S4 `validate-heal`** · **L** · Deps: A-P1/A-P2/A-P3 (data-flow — it validates their output; **built first** so their contract is testable) · g1/g2/g3, two repairs then `unresolved`.
  `default_for` returns `None`; the helper never raises.
- [ ] **A-P1 `general-propose`** · **M** · Deps: A-S1, A-S4 · no anchor at all. A brief carrying one
  **raises**.
- [ ] **A-P2 `family-propose`** · **M** · Deps: A-S1, A-S4 · runs in parallel with A-P1.
- [ ] **A-S2 `brief-assembly`** · **M** · Deps: A-S1, A-S3, and A-P2's **accepted** round ·
  `spec-brief-assembly.md`
  - **Model-free, but it belongs here, not Phase 3** — it cannot run until a model round is accepted.
  - Emits `familyActions`, sorted ordinally. **`[]` for the 31 family-less species, present and empty —
    never skipped.**
  - ⛔ Closes **F15 recurring**: the same "ownership passed in a circle" defect as family motifs, one
    field over, caught only by the plan-coverage audit.
- [ ] **A-P3 `signature-propose`** · **M** · Deps: A-S1, **A-S2** (was A-P2 — A-S2 assembles its brief) · reads its family's accepted output, inlined
  in fixed sorted order.
- [ ] **⛔ SMOKE BATCH** · Deps: A-P1, A-P2, A-P3, A-S2
  - Small `--count` against the **8 fully-anchored species**. Report metrics, defects found, defects
    fixed.
  - **Gate G5 — evidence-gated, not owner-gated (plan §2a).** Proceed when all four criteria hold: zero
    schema-audit defects · `unresolved` under 10% each with a named reason · byte-identical replay proven
    by hash · the coverage report names its thin cells. **Any one failing means fix and re-run — not
    escalate.** The thresholds live in `action-corpus-run.v1.json`, so moving them is a diff.

### ⛔ Checkpoint C6 — quality is proven · **owner decision**

---

## Phase 5 — movement and capability

- [ ] **A-M1 `movement-payload`** · **M** · Deps: **A-E1** (unbuildable without its `category` field), A-S1, A-T1 · the RPG-layer half; legal today.
- [ ] **A-M2 `lawn-reposition`** · **L** · Deps: **E33**, A-M1, ⛔ **a lawn-side production producer** (⛔ **CORRECTED 2026-09-03:** this said `A9 movement-actions` and that it *"is in no plan"*. Both are wrong — `A9` is **battle-grid only** (`action-map.md:294`) so it is not this module's producer at all, and it **is** planned, deferred behind `A10` at `tasks/action-todo.md:1703-1704`. **Decided 2026-09-03: A-M2 ships knowingly inert**, toggle default-off, map row reading **inert** in that word; the producer is a separate criteria-stated task that blocks nothing) · one guarded entry point,
  record-then-drain, `guard-single-writer.ps1` extended with **`Fx/` and `Hud/` exemptions** plus an
  inverse test. ⚠️ Handle `LawnCoords.CellCenter`'s null-`Mouse` fallback — it is a teleport to
  near-origin.
- [ ] **E34 `trigger-vocabulary`** · **M** · Deps: E33 · five new triggers; **arm both owner-key
  branches**.
- [ ] **E35 `match-modify`** · **L** · Deps: E34 · new attach point; **creates** the `decisions.md`
  attach-point row (there is none); `long` channel on `CheatState`; scoped match-end restore.
- [ ] **E36 `wave-control`** · **M** · Deps: E34, E35 · op is `hold`, not `freeze`; `ChainDepth` guard.
- [ ] **E37 `projectile-control`** · **M** · Deps: E28 · ⚠️ **assembly sweep before wiring `moveWay`.**
- [ ] **E38 `entity-fields-12plus`** · **L** · Deps: E30, **E42** · 11 → 23 channels; **`P-ATK-ADD` has no
  value guard today**; name the `LowerIsBetter` pricing-sign trap.
- [ ] **E39 `plant-side-status`** · **M** · Deps: E28 · widen both apply **and** clear; closes G5's
  unguarded board-wide loop.
- [ ] **E40 `spawn-non-grid`** · **M** · Deps: E28 · widen `kind`, do not add one. `present` is scoped out.
- [ ] **E41 `ui-attach-point`** · **M** · Deps: — · read-only; **first producer for
  `ActorHudResources.Meters`**, declared and serialized with no producer today.
- [ ] **ep-7 `world-seed`** · **M** · Deps: ep-2 · `effect-pipeline/spec-world-seed.md`
  - Per-player world seed, created once, shown in the UI, composed as `hash(worldSeed, stream, targetId)`.
- [ ] **ep-8 `eligibility-tags`** · **M** · Deps: ep-1, ep-3 · `spec-eligibility-tags.md`
  - Tag-based **affix** eligibility with a per-container allow/deny override.
  - ⚠️ **A different axis from `A-E1`**, on a different entity — affixes on containers, not actions on
    actors. `A-E1` §4 states the boundary; hold it, or two eligibility vocabularies ship for one concept.
- [ ] **⚠️ ep-5 `mods-absorption`** · **L** · Deps: ep-4 · `spec-mods-absorption.md`
  - Move equipped-slot effects from `rpg_unique_stat_mods.mods_json` onto `effect_binding`.
  - ⛔ **A migration over live, save-affecting unique-actor data.** Sequenced **after** the proof, per its
    own map row — do not pull it earlier for convenience.
- [ ] **⚠️ ep-6 `patron-absorption`** · **L** · Deps: ep-4 · `spec-patron-absorption.md`
  - `PatronSecondaryPlugin` becomes a `patron.*` container. `data/seed/containers/patron.json` already
    exists with the exact `EffectId` the plugin emits, so this **fills a staked container**.
  - ⛔ **Byte-identical output must be proven across the full (rarity × star × level × Θ) grid**, or the
    patron program's SIM results are invalidated.
- [ ] **ep-9 `affix-authoring`** · **M** · Deps: ep-1, ep-6 · `spec-affix-authoring.md` · **model stage**
  - The seedsmith pipeline for named, multi-atom, slotted affixes.
  - ⛔ **W7.10 applies**: `--dry-run` and a small `--count`; a full run is an owner decision.
  - ⚠️ **Agree with `seed-to-concrete` T7.2 who runs the authoring pass** — both claim it (`E32` §7).
- [ ] **ep-10 `dev-reforge`** · **S** · Deps: ep-4, ep-6 · `spec-dev-reforge.md`
  - `POST /api/debug/reforge-world`. Debug surface only.

---

## Phase 6 — pricing

- [ ] **E44 `power-sweep`** · **L** · Deps: E9 (built), E43 (the fitting corpus) · `spec-power-sweep.md`
  - **Read §3 first** — two prior attempts failed because both were linear. A third that does not
    introduce non-linearity is already refuted.
  - **Criterion 7: a third refuted attempt, reported with evidence, is a real outcome.**
  - Unblocks C1 — enabling it stays a separate, explicit decision.

---

## Deferred, with a reason

- [x] **E45 `derived-write-lawn`** — ⛔ **NOT DEFERRED; ALREADY BUILT.** Corrected 2026-09-03.
  `decisions.md:104` is its ADR row (*"Owner decisions, approved 2026-08-30"*) and the spec reads
  **"BUILT and PROVEN LIVE end to end"**. I deferred a shipped module on a constraint I never checked.

---

## Final — live, then fix · **an access task, not a gate**

Per plan §2a: this needs a **machine**, not a decision. It runs on the owner's hardware because CI does
not compile the injector and the game is not in the repo — but **nothing waits on someone saying yes**.

- [ ] **Live check.** Full deploy, lawn play, and the checks each injector-side spec names. Every one of
  those specs states its own pass criterion, so the run is a checklist rather than a judgement.
  - **Queued behind it, never blocking it:** E37/E39's `Assembly-CSharp` sweeps, E38's `Z-TAKEMULT`
    confirmation, E40's coin arm. **Each is scoped so the rest of its module ships without it** — a
    module with one arm held is still a module delivered.
- [ ] **Fix-bug phase**, after the live check — owner, 2026-09-03: *"we will completely build then final
  phase will live check… fix bug phase will launch after live check."*

### ✅ Checkpoint C7 — live
