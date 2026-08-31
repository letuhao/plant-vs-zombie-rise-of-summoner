# Tasks: backlog-clear

Plan: [backlog-clear-plan.md](backlog-clear-plan.md). Every task belongs to an existing program and
stays recorded there too — this file is the running order.

**10 phases · 5 checkpoints.** Scope: S ≈ under an hour · M ≈ a focused session · L ≈ multi-session.

> ## ⛔ Rules binding on every phase
>
> **1. Git stays hands-off.** No commits, no staging. Each phase ends with a commit-message draft and
> the paths it touched.
> **2. A phase marked "spec-first" does not get code until its spec is reviewed.** Phase 1 is the only
> new module here and it carries the full design gate.
> **3. Phases 6–10 open with a read task.** Their specs were **not** read when this plan was written,
> so their acceptance criteria are written at phase start against the spec, never invented here.
> **4. Owner-run items are marked ⛔ and are never self-certified.**

---

## Phase 1 — the binding producer *(spec-first)*

**Why first:** it is aura-skill's last real gap **and** `commander-surface`'s last unmet cross-program
prerequisite. Two programs finish behind it.

**⛔ Read the correction in the plan before starting.** The claim that this is `effect-atom` E20–E25 is
false — those six modules are built and are a different six things. `RpgStore.Bind` has 19 test callers
and **zero** production callers; this module is new and unspecced.

- [ ] **BP1: spec `aura-binding-producer`** · **M** · *(spec-first — ⛔ owner review before BP2)*
  — **WRITTEN 2026-08-31, ⛔ awaiting owner review.** Spec:
  [spec-aura-binding-producer.md](../docs/architecture/aura-skill/spec-aura-binding-producer.md);
  map row added. Stays unchecked: the review is the acceptance.
  - **The consumer chain is entirely built** — traced end to end at source, not assumed:
    `ResolveBindings` (`RpgStore.AtomInstances.cs:286`) → `AtomPushService.Build`
    (`AtomPushService.cs:54`) → `RpgHub.cs:105`, which already asks for **`OwnerScope(Player,
    playerId)` on `RuntimeId.Lawn`** — the exact scope A5's hand-made row used → SignalR →
    `AtomPushReceiver.Install` (`:64`) → `Funnel.EnqueueModifier` (`:34`) → bag. Only the first row
    is missing.
  - **Loop ownership settled by the doc, then checked against code:** `overlay-control-loops.md:79`
    and `:118` put "push grants/loadout at deploy/bind" in **Cold**, and `:196` makes a plugin
    calling `Bag.Grant` a hard-law anti-pattern. Server-side, Cold. Never touches Unity.
  - **⛔ Two wiring gaps found while tracing — neither was in the task description, and both expand
    BP2/BP3:**
    - **G1 — the atom push fires only on `Hello`.** `PushGrantSnapshotAsync` has exactly one caller,
      `RpgHub.cs:43`. So *even a perfect producer would do nothing until the game restarted.* Fix is
      small: the producer triggers the push, which is already a full idempotent rehydrate by its own
      design (`RpgHub.cs:89-92`).
    - **G2 — active auras are RAM-only while bindings are durable.**
      `AuraRuntimeEndpoints.cs:31` is a `ConcurrentDictionary`; grepping `FusionRpg.Data` for
      `ActiveAura`/`active_aura` returns nothing. Equipped persists, active does not — so a server
      restart would leave bindings for auras the runtime no longer thinks are on.
  - **One ⛔ owner question in the spec (§3.3, §10.1):** do bindings become the SSOT for activation
    (recommended — collapses two states into one and deletes the desync class, the same move the
    `EventQueue` indexed-heap rewrite made), or does activation get its own table? **It blocks only
    the seeding line, not BP2.**
  - Satisfy the design gate: the injector↔game row, the effects rows, and `overlay-control-loops.md`
    — which already bounds the answer. Grants reach the bag **Cold → Funnel** at deploy/bind
    (`overlay-control-loops.md:118-120`), never plugin→Bag, and *"Secondary plugin calls … `Bag.Grant`"*
    is listed as a hard-law anti-pattern (`:196`). So the producer is Server-side, not injector-side —
    **confirm that against code rather than inheriting it from this sentence.**
  - Must answer, each against a `file:line`: **when** a binding is created (loadout save? aura enable?
    `board.start`?), **who owns the instance rows** the binding points at, **how withdraw works** when
    an aura is disabled or a commander changes, and **what happens to a stale binding** whose instance
    no longer exists (`Bind` already returns `AtomRejection.StaleInstance` — the producer must not
    depend on that as flow control).
  - Acceptance: spec written at `docs/architecture/aura-skill/spec-aura-binding-producer.md`, gate
    checklist completed with any un-tickable box named, map row added.
  - Files: the spec, `docs/architecture/aura-skill-map.md`.

- [ ] **BP2: the producer — Core + Data** · **M** · **Deps:** BP1 approved
  - Acceptance: creating/updating a commander's aura loadout produces the `effect_instance` +
    `effect_binding` rows the lawn already knows how to read; disabling withdraws them; the operation
    is idempotent (re-saving an unchanged loadout writes no new row and bumps no revision).
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`, `dotnet test tests\FusionRpg.Core.Tests`
  - **Falsifier required:** break the producer and prove the aura stops reaching a resolved actor. A
    test that passes because the fixture already had rows proves nothing — that is exactly how the
    A5 proof had to be redone in the aura program.

- [ ] **BP3: server wiring** · **S** · **Deps:** BP2
  - Acceptance: the real save path calls it — no debug endpoint, no test-only seam. Proven by
    `guard-dal.ps1` staying green (SQL stays in `FusionRpg.Data`) and by a `Server.Tests` test that
    drives the public endpoint, not the store.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, `.\scripts\guard-dal.ps1`

- [ ] **BP4: ⛔ owner live proof** · **S** · **Deps:** BP3
  - Acceptance: an aura reaches a live lawn plant **with no hand-made instance or binding row** — the
    thing the A5 proof could only do by hand. Same measurement shape as A5: a `combat.power.*` channel
    on a real plant ptr, moving by `AuraMagnitude.Compute(...)` and back.
  - ⛔ Owner-run: needs a deploy and a live board.

### ✅ Checkpoint 1 — two programs close
- [ ] BP1–BP3 green; full Core + Data + Server suites; four boundary guards; overflow 0 critical; M1 = 0.
- [ ] `aura-skill-todo.md`'s binding-producer entry corrected (it currently misattributes this to
  `effect-atom` E20–E25) and closed.
- [ ] `commander-surface-map.md`'s cross-program prerequisite list updated — all three now met.
- [ ] ⛔ BP4 owner live proof.

---

## Phase 2 — finish aura

- [ ] **AU1: ~~delete `commanderOnly`~~ → REFRAMED: propagate the decision that already exists** · **S**
  - **⛔ D1 was taken on a misleading brief and is withdrawn pending the owner. Not actioned.**
    Three errors in how the option was put:
    1. **"Zero consumers in `src/`" was literally true and materially misleading** — the grep was
       scoped to `src/`. Real consumers exist outside it:
       `tools/ItemSeedValidator/Registries/RegistrySet.cs:244` folds it into `RoleIds`,
       `tools/seedsmith/seedsmith/adapters/items/registries.py:39` reads it,
       `tests/FusionRpg.ItemSeedValidator.Tests/SeedFixture.cs:21` carries it, and
       `docs/architecture/item/build-log.md:271` records commander base types expanded from it.
       Deleting it breaks the seed validator and seedsmith.
    2. **It is not an abstract role, it is one concrete slot.** `roles.commanderOnly` holds exactly
       one entry — `standard` (banner / root-totem): *"one slot, not several. Binds at match
       owner-scope so its atoms reach the whole squad. Priced from a separate 100‰ commander budget,
       never drawn from the body's 1000‰."*
    3. **The owner had already decided it**, and the decision is the opposite of delete.
       `aura-skill-map.md:119` decision #5: *"**Keep both, relationship defined.** Banner = gear
       (found/crafted, item progression, 100‰ item budget); aura = skill (chosen/invested, aptitude
       progression, aura budget). **They stack; budgets stay separate.**"* That is option (c) of the
       question I asked, already answered.
  - **The real defect is a stale doc, not dead data.** `spec-aura-content.md:303` still calls this an
    open question — *"Whether banner atoms and aura atoms stack, and against which budget, is
    undecided anywhere. Owner call; not blocking"* — which the map's decision table had already
    settled. That staleness is what made three separate sessions re-raise a closed question.
  - **Reframed acceptance (pending owner confirmation):** delete nothing; instead propagate the map's
    decision #5 into `spec-aura-content.md` §10.2 and `aura-skill-ideal.md:863`, so the next reader
    finds the answer where they look for the question.
  - Verify: `dotnet test tests\FusionRpg.ItemSeedValidator.Tests` (must stay green — it would not
    have, under the original D1).

- [ ] **AU2: author the aura containers** · **M** · *(decision D2)* · **Deps:** AU1, Phase 1
  - Acceptance: each shipped aura in `AuraContentCatalog` has a real `world-buff` container whose
    magnitude derives from `P(Θ)` with an even split across the channels that aura declares — **no
    hand-picked per-aura constants in code.** Coefficients land in `data/tuning/`, so a balance pass
    is a file save (`tunables-ssot.md`).
  - Verify: content validation, full Core suite, and the delivery path proven end-to-end via Phase 1.
  - Closes T16's second ground; its first ground ("nothing can read a `world-buff.*` container") was
    already void once the executor shipped.

---

## Phase 3 — the kernel drive *(delivers P1b + P1c)*

B24's spec is **approved** (2026-08-31), so the injector edits are unblocked. Both of its §11 open
questions are settled: clock stays **unscaled**, one kernel **per board**.

- [x] **B25 (injector half)** · **M** — **DONE 2026-08-31.** `KernelDriveHost` adapter + the
  `InjectorLoop` call site + board lifecycle. Core half was already built and green
  (`DeltaTickAdvance`, `TimelineDrive`, bounded `PopDue`).
  - `Effects/KernelDriveHost.cs` (new) — adapter only, per the spec's Core-vs-Injector rule. Owns the
    **float→integer conversion at the boundary** (`Math.Round` to whole microseconds, not truncation:
    truncating biases every frame downward, which is the exact drift the carry exists to remove), the
    budget, and the board lifecycle.
  - **Budget lands on the specced slice by construction:** 1 % of measured frame time clamped to
    [0.05 ms, **0.15 ms**]. At a 16.6 ms frame that is 0.166 ms → clamped to exactly the 0.15 ms
    kernel share from `spec-kernel-performance.md`. Structural per-frame cap, commented as such.
  - **A huge frame delta is deliberately NOT clamped.** After a level load the delta can be seconds;
    clamping would silently lose simulated time. Offering it whole makes a large backlog due at once,
    which the bounded drain spreads across frames in unchanged order — the designed path, not an edge
    case to defend against.
  - **One kernel per board** (owner decision): `BeginBoard` at `board.start`, `EndBoard` at
    `board.end`/`match.result` — **and also on the no-prior-end `BoardAwake` path**
    (`MatchHost.cs:118-127`), which a naive two-site wiring would have missed and left a stale queue
    alive into the next match.
  - `PerfProbe`: `kernel.tick` / `kernel.drain` / `kernel.schedule` added, `SectionCount` 21 → 24 in
    the same edit (the count and the name array are two halves of one declaration).
  - **Behaviour-neutral by construction today:** the queue is empty until B26, so a tick is a clock
    advance and a heap peek. That is why it lands separately — the drive gets to be provably harmless
    before it carries anything.
  - **⛔ An unrelated build break in the owner's uncommitted work, found and fixed.** The injector
    would not compile: `Hud/ActorHudPool.cs` (untracked, actor-hud work) referenced bare
    `FxResources.*` from namespace `FusionRpg.Injector.Hud`, where it binds to .NET's own internal
    `FxResources.*` resource namespaces instead of `FusionRpg.Injector.Fx.FxResources` — the `using`
    at line 5 does not help, because namespace lookup beats a using-imported type as a qualifier.
    Qualified all three sites to `Fx.FxResources.*`, the same idiom as this session's `GameDumps.cs`
    fix. **Worth the owner's glance before committing.**
  - Verified: Core **4878/4878**, injector builds (`FusionRpg.Injector.MelonLoader.39`, Release,
    pvzrh-3.9), four boundary guards green, magic numbers **M1 = 0**.
- [x] **B26: shield + DoT grids onto the kernel** · **L** — **BUILT 2026-08-31.** Substitution, not
  redesign: the **100 ms period is unchanged**, only the scheduling moved.
  - **Why the period stays 100 ms:** shield regen accumulates in integer milli-HP, so a 1 ms drive
    truncates small regen rates to zero — the hazard B25's own spec flagged. Moving the scheduling
    without the granularity is what keeps this behaviour-identical.
  - `EffectRuntime`: accumulator split from work — `PulseDotsNow()` / `PulseShieldsNow()` (+ a shared
    `FlushShieldEvents`). The kernel schedules them; the legacy grids still call them on the fallback
    path, so there is exactly **one** copy of the work.
  - **Ordering is preserved by construction and asserted.** `BeginBoard` schedules the DoT pulse
    *first*, so `(DueTick, Seq)` fires DoT before shield upkeep at every shared tick — the frame order
    the grids had, which `shield-system-spec.md` §2.6 requires so an expiring shield still absorbs its
    final frame's damage.
  - **Re-arm is off `e.DueTick + period`, never off "now".** Re-arming off `now` would let every
    deferred drain push the next pulse further out, permanently slowing cadence on a stuttering
    machine instead of merely delaying it.
  - **The DoT overshoot bug is fixed in the legacy path too** (`_dotAccum = 0` → `-= 0.1f`), so the
    fallback is not left carrying a defect the kernel path doesn't have.
  - Kill switch `FUSIONRPG_KERNEL_GRIDS=0`, mirroring `FUSIONRPG_EVENT_V2=0`.
  - **5 substitution tests in Core** (`UpkeepSubstitutionTests`) — CI never builds the injector, so
    what is tested is the *scheduling*, which is what B26 changes: exact pulse count over 60 fps
    frames, DoT-before-shield at every shared tick, a 2 s hitch delivering all 20 pulses **without
    running them in one frame**, deferred pulses landing on exact period multiples, and irregular
    frames losing/gaining nothing.
  - **Falsifiers run.** Re-arming off `Clock.Now` reddens both count tests; swapping the schedule
    order reddens the ordering test. **One correction made as a result:** the multiples test's
    docstring claimed it would catch the re-arm mistake — it does **not** (at a 2 s hitch the clock is
    already on a period multiple, so the drifted schedule still lands on multiples). The count tests
    are what catch it. Docstring corrected to say what the test actually reaches.
  - **The predicted "zero goldens move" held** — full Core **4883/4883** with no test edits, four
    guards green, injector builds.
  - ⛔ **Success criterion #1 ("no accumulator-plus-period grid survives in `EffectRuntime.cs`") is
    NOT met yet, deliberately.** The legacy accumulators remain as the kill switch's fallback. That is
    the same shape `FUSIONRPG_EVENT_V2=0` kept during its own migration, and removing the revert path
    before B27's live proof would leave no way back if the live run disagrees. **Deleting them is a
    named follow-up after B27, not a forgotten one.**
- [ ] **B27: ⛔ owner-run probe sections + live verification** · **M** — `kernel.*` against B1–B9.

### ✅ Checkpoint 2 — one scheduler in the injector
- [ ] No accumulator-plus-period grid survives in `EffectRuntime.cs` — **deliberately deferred until
  after B27's live proof**: the accumulators are the `FUSIONRPG_KERNEL_GRIDS=0` fallback, and removing
  the revert path before the live run would leave no way back. Named follow-up, not a forgotten one.
- [x] Full suites green with **no test edits**; whether any golden moved is recorded either way —
  **Core 4883/4883, zero goldens moved**, which was the prediction and is now measured.
- [x] `P1c` delivered (B25 built, bounded resumable drain live in the injector drive).
- [ ] `P1b` — the sections exist (`kernel.tick`/`kernel.drain`/`kernel.schedule`) but are only
  *closed* once B27's live run reports them.
- [ ] ⛔ B27 owner run: kernel ≤ 0.15 ms/frame at 200+ entities, no gen2 GC during a level.

---

## Phase 4 — Zomboss momentum *(owner decision D5)*

- [x] **WM1: momentum term** · **M** — **BUILT 2026-08-31, as hysteresis.**
  - ⛔ **The plan's own acceptance for this task was written without reading the spec, and the spec
    changed it.** Two findings, both recorded in `spec-ai-commander.md` §Momentum:
    - **The stated blocker was wrong.** It said momentum *"needs memory across turns… which becomes
      hashed, replayed state."* It needs neither. A policy's orders already land in
      `rpg_world_commands` — which **is** the save, never trimmed, indexed
      `(world_id, turn, commander_id, seq)` — so last turn's choice is already durable. And this
      spec's own `IFactionPolicy` contract states *"replay reads that log and never re-runs the
      policy"*, so a policy input cannot reach a replayed hash by any path. **That is the same
      property that makes rewriting Zomboss's brain safe; momentum is an ordinary case of it.**
    - **A bonus term could not have worked.** `FrontierRulesPolicy` is a rule ladder, not a utility
      scorer, so there is no score to add to. The oscillation is a **feedback loop between two
      rules** (Defend pulls home → garrison rises → threat clears → Expand sends out → garrison
      drops). What breaks that is **hysteresis**: an alternative must beat the standing choice by a
      margin, not merely tie it.
  - Applied in `Expand`, **not** `Defend` — Defend is the emergency at the top of the ladder and must
    stay free to interrupt a march. Making an emergency sticky trades dithering for a legion that
    ignores a real threat, which is the worse failure.
  - `momentumMarginMilli: 250` is a **tunable**, in a new `data/tuning/ai.v2.json`. It could not be a
    `publish.py` edit — that tool refuses to invent keys, correctly, since a new tunable is a schema
    change and not a rebalance. Loader extended; a missing key is a load rejection naming it.
  - Plumbing: `IWorldView.LastOrderedDestination(entityId)` (self-knowledge, beside `OwnForces`'s
    "you know what you brought"), supplied by `CommitWorldTurn` from the previous turn's log.
    Destination is **walked** from the lane path, because a `Move` stores lane ids and a null
    `SectorId` — the destination is not recorded anywhere and had to be derived.
  - **Three defects of my own, each caught by a check rather than by reading:**
    1. **The behaviour test was vacuous** — an early `return` when the fixture never reached `Expand`
       meant it asserted nothing. Found by falsifying the margin to 0 and watching it stay green.
    2. **The fixture then reached `Hold`, not `Expand`** — a Legion of Fighters fails
       `SurvivesTheRoute` at the march-loam gate. Fixed by copying `FrontierRulesTests`' own proven
       Warband-with-a-Bearer rather than inventing one.
    3. **The SQL named a column that does not exist** (`entity_id` — the id lives inside
       `payload_json`). Ten Data tests caught it on the first run.
  - **Falsifier:** disabling the hold-course branch reddens the behaviour test. **Honest limit,
    written into the test:** setting the margin to 0 does *not* redden it, because the two candidates
    tie by construction and a tie holds at any margin. So it covers *that* hysteresis happens, not
    *where the threshold sits* — a fixture with a controlled percentage gap is named as the follow-up
    for whenever the margin is next tuned.
  - **D4 is a decision NOT to build:** routed forces are still finished off where they stand.
  - Verified: **Core 4887 · Data 548 · Guard 142 · E2E 195 · CheatCore 40 · Server 87**, all green;
    four boundary guards green; **M1 = 0**. The replay-determinism tests
    (`The_pure_engine_reproduces_the_stored_hashes_from_the_command_log_alone`,
    `Replaying_a_stored_log_ignores_the_policy_that_produced_it`) pass, so the safety argument above
    is measured, not merely argued.
  - ⛔ **Owner playtest is the real sign-off** — ten turns, and whether the turn report now reads as
    a commander with a plan rather than one that dithers.
  - Acceptance: on the reshaped map he no longer alternates `defend black-gate` (threat 899) /
    `expand to verdant-shelf` (value 436) from T8 onward. A commitment bonus for continuing last
    turn's plan, **as a tunable** — the size of the bonus is a balance number.
  - Verify: the world AI suites; `.\scripts\mutate.ps1 -Set world-ai` if the change touches scoring.
  - ⛔ Owner playtest is the real sign-off: *can you tell from the turn report why he did what he did?*
  - **D4 is a decision NOT to build:** routed forces are still finished off where they stand.

---

## Phase 5 — game-gui dead-code deletion

Sign-off given 2026-08-31.

- [ ] **GG1: delete the superseded page components and their redirects** · **S**
  — **⛔ NOT EXECUTABLE AS WRITTEN. Six of the seven named components are live dependencies of their
  own replacements, and deleting them would break the app.** One was genuinely dead and is gone.
  - **First: the gate.** Checkpoint I is *"gated on Checkpoint H"* and *"per the owner's own
    instruction (2026-08-24), this does not start until Checkpoint H passes."* My plan recorded the
    gate as "sign-off already given", which was the wrong gate. H's last open box was its owner
    review, ticked 2026-08-31 — so H is now fully ✅ and the gate did clear. Right answer, wrong
    reason; recorded so the next reader does not inherit the wrong one.
  - **The premise is false.** Checkpoint I assumes the pre-refactor pages are superseded. They are
    **composed**, which is the "thin wrap" risk its own text raised — and the answer turns out to be
    that the layers are not thin wraps but genuine compositions *of these pages*:
    - `AlmanacLayer.tsx:7-8` — `Component: CatalogPage`, `Component: RecipesPage`
    - `ChronicleLayer.tsx:8-10` — `Component: MetricsPage`, `RpgProgressionPage`, `PvzStatsPage`
    - `ExpeditionsLayer.tsx` — imports `ExpeditionsPage`; `DeveloperTree.tsx` — imports `MetricsPage`
    - `AlmanacLayer.tsx:17` says it outright: *"`CatalogPage`/`RecipesPage` are the honest, real,
      current richness"*.
  - **Done: `RosterPage.tsx` + `RosterPage.test.tsx` deleted.** Verified dead first — its only
    importer anywhere was its own test; the sole other mention (`i18n/vocabularyGuard.ts:12`) is a
    comment. This is exactly the *"`RosterPage.tsx`'s standalone form"* the checkpoint names.
  - **The `roster` route stays**, deliberately. The rule is *"every route that redirected into a
    **since-deleted component**"* — `roster` redirects to `/sanctum?panel=creatures`, a live
    destination, and `e2e/creatures.spec.ts:87` asserts that redirect works. Deleting it would break
    a passing test to satisfy a rule that does not apply to it.
  - **Newly orphaned, deliberately kept, owner's call:** `rosterPhase.ts` + its test. Only RosterPage
    used it, so it is now dead — but it is a tested pure module encoding **server parity**
    (*"matches Server TryBegin/TryRetire"*: `canDeploy`/`canEquip`/`canAwardXp`/`canRetire`), not a
    page component, and a future Creatures layer wants exactly those rules. Deleting it would be
    scope creep that loses real domain knowledge.
  - Verified: `tsc --noEmit` clean, `npm run build` clean, unit suite **805/806**. The one failure
    (`disabledReasonGuard` GG-55) is **pre-existing and unrelated** — proven, not assumed: restoring
    `RosterPage` from `HEAD` and re-running reproduces it identically. It names
    `layers/commanders/CommandersLayer.tsx:145,179` and `ui/actor/CommanderSheetFooter.tsx:37` —
    uncommitted commander-surface work, three disabled `<Button>`s with no accessible reason.
    **⛔ Owner's, and worth fixing before that work is committed.**
  - **What remains of GG1 is a question, not a task:** the six composed pages cannot be deleted while
    the layers render them. Either Checkpoint I is reframed (the pages are the implementation, the
    layers are the shell — nothing to retire), or the layers first grow their own content. That is a
    design call, not a deletion.

### ✅ Checkpoint 3 — decided work is done
- [ ] Phases 1–5 complete. Every 2026-08-31 decision has either shipped or is explicitly a
  decision-not-to-build (D4, D9), recorded as such.

---

## Phase 6 — loam L44–L50 *(read first)*

The todo names a **real, unresolved gap**: the post-gate mechanics are largely invisible on the wire.

- [ ] **LO0: read `spec-loam-fe-2.md` §1 and L44–L50's own entries** — acceptance criteria for the
  tasks below are written from that read, not from these titles.
- [ ] L44 — wire the five missing fields; catch up the TS mirror · **M**
- [ ] L45 — turn-playback narration for the loam/legion/Unmade vocabulary · **M**
- [ ] L46 — Sustain and Build command UI · **M**
- [ ] L47 — Ward: Core (command, admission, resolver) · **M**
- [ ] L48 — Ward: server endpoint · **S**
- [ ] L49 — Ward: web UI · **M**
- [ ] L50 — Prospecting: wire and UI · **M**
- [ ] Program acceptance: a player can Sustain, Build and Bind a Warden entirely from `#/world` with
  no raw API call; the playback rail never prints a raw engine detail string; `AbandonRuleTests`'
  100-turn and `TwoHearthsCampaignTests`' 60-turn properties both still pass.

---

## Phase 7 — combat-unification waves *(read first)*

U14 signed off 2026-08-31, so the waves are all that remain.

- [x] **CU0: read the three wave specs** — done 2026-08-31. Findings below; acceptance lines now come
  from the spec rather than from the plan's guesses.
- [ ] E1 — on-hit status riders (v3) · **L** — zero-rider battles byte-identical v2→v3.
  **⛔ Blocked on a respec.** `battle-timeline-map.md:112` marks `spec-battle-enrichment.md`
  **"partly superseded and should be rebased after T5"**, and T5 shipped 2026-08-28. For E1
  specifically: *"a DoT pulse is a scheduled event. Rebasing fixes the sub-round `PeriodMs`
  under-delivery the current round loop cannot express."* Building against the pre-T5 text would
  encode a design the kernel already replaced.
- [ ] E2 — species skills (v4) · **L** — **⛔ same respec, and named as the worst case:**
  *"was the wave that most needed this. A cooldown **is** a readiness function; skills become actions
  on the timeline rather than a bolt-on. Respec after T5."*
- [ ] E3 — hybrid payloads (v5) · **M, not L** — **the one wave that is NOT respec-blocked**
  (`battle-timeline-map.md:112`: *"genuinely independent (resolver-side, not timeline-side). **Can
  ship any time**, before or after this program."*). Scoped against code 2026-08-31:
  - **Most of it already exists.** `BattleActorSetup.ElementSecondary` is a real field
    (`BattleModels.cs:27`) and already feeds **defence** (`ActorElementTypes.Create`,
    `BattleEngine.cs:36-38` — dual-type matchup already handled) and **affinity stats**
    (`BattleStatComposer.cs:111`). The gap is one place: `AttackComponents`
    (`BattleEngine.cs:39-41`) is `ElementPrimary` at weight `1.0` and ignores the secondary.
  - **⛔ Owner decision required — the weight split.** The spec says *"weighted components (policy
    constant, e.g. 0.7/0.3 — **locked at plan time, ask-first to change**)"*. It was never locked at
    plan time, so there is nothing to inherit and the "e.g." must not be mistaken for a decision.
    It is also a balance number, so per `tunables-ssot.md` it belongs in `data/tuning/battle.v{n}.json`,
    not as a `const`.
  - **⛔ It moves goldens, and the size is measured rather than guessed: 4 of 24 shipped species
    carry a secondary element** (Light, Ice, Fire, Dark — counted in
    `DemonSpeciesCatalog.Generated.cs`). Every battle involving one currently attacks as pure
    primary and would afterwards attack split. That is a `RulesetVersion` bump plus a
    predicted-delta writeup, on the same discipline the Battle time model row already records.
  - **The zero-content invariant to assert** (Wave R's own shape, applied here): an actor with **no**
    secondary element must be byte-identical before and after — which is what makes the other 20
    species' goldens provably safe rather than hopefully safe.

### ✅ Checkpoint 4 — enrichment on stamped history
- [ ] All waves on stamped `RulesetVersion` history; ban test green; expeditions resolve on v5.

---

## Phase 8 — battle-timeline Phases 4–5 *(each spec-first)*

- [ ] B19 — T8 turn-order forecast · **M**
- [ ] B20 + B21 — T6 interactive turns + T10 decision trace · **L** (ship together)
- [ ] B22 — T11 live sessions · **L**
- [ ] B23 — T7 PvZ observer · **L**
- [ ] Checkpoint C: every interactive battle replays from its trace; the sweep cannot overwrite a
  played result.
- [ ] Checkpoint D: live game events project into the shared state vocabulary; frame budget held.

---

## Phase 9 — actor-hud backlog + housekeeping

- [ ] Boss tier signal from expeditions → builder emits `boss` · **S**
- [ ] HP sliver when owner enables the tunable · **S**
- [ ] Status icon art pass (replace initials) · **S**
- [ ] Perf probe B2 before/after published in research · **S**
- [ ] **Housekeeping: clear the stale line at [action-todo.md:1705](action-todo.md#L1705)** — *"A8's
  reaction lane waits on timeline B6"*. B6 shipped 2026-08-28 and records that A8 **did not need the
  lane at all** (it ships as a stance with riposte-on-release). Closed by its own evidence.

---

## Phase 10 — seedsmith (49 items) *(read first)*

Deliberately last: [action-todo.md:1706](action-todo.md#L1706) records it as *a development tool, built
after this program*.

- [x] **SS0: read `seedsmith-plan.md` and the W2/W3 entries** — done 2026-08-31. **P1's acceptance
  criteria were already fully specified in the plan** (three concrete conditions), so they are used
  verbatim below rather than invented — which is exactly what the "read first" rule is for.
- [x] **P1 — feasibility: pigeonhole → Hopcroft–Karp → König** · **BUILT 2026-08-31.**
  `seedsmith/planner/{__init__,feasibility}.py` + `tests/test_feasibility.py` (15 tests).
  - **Layer 1 — pigeonhole**, O(n), short-circuits before anything expensive runs.
  - **Layer 2 — Hopcroft–Karp**, O(E√V). Chosen over the simpler augmenting-path loop because that
    one is O(V·E), and at this planner's own sizes (75 demands × 40 slots, dense allowance) that is
    the difference between an instant answer and one slow enough that someone reaches for a smaller
    corpus — which is how a feasibility check stops being run at all.
  - **Layer 3 — König**, only on failure. The minimum vertex cover **is** the binding constraint, so
    the refusal names *which slots the unplaceable demands are all contesting* instead of saying
    "infeasible". That is the whole point: the original 75-into-40 incident cost a manual bisect
    because the refusal said nothing actionable.
  - **Slot capacity > 1 is expanded into seats** — the caller's model (a slot holds k things) stays
    separate from the algorithm's (a seat holds one), and seats are generated in sorted order so an
    assignment is reproducible.
  - **Acceptance, all three met, quoted from the plan rather than paraphrased:**
    - [x] *"A synthetic 5-themes × 15-uniques-into-8-roles × 5-axes fixture … is refused with the
      specific bottleneck named, not 'infeasible'"* — 75 into 40, refused at layer 1, naming all
      **35** demands that have nowhere to go.
    - [x] *"A balanced 5-theme fixture's Latin-square construction produces 0 axis collisions across
      all 25 (role, theme) pairs"* — plus a stronger check the criterion does not require: every
      role **and** every theme sees each axis exactly once. Collisions-only would also pass for an
      assignment using a single axis, which is not a Latin square at all.
    - [x] *"A feasible-but-locally-starved fixture (totals fit, one subset doesn't) is caught by
      layer 2 where layer 1 would incorrectly pass it"* — 4 demands into 4 seats, three of which can
      only take one slot. The test asserts layer 1 passes first, or it would prove nothing.
  - **Falsifiers run, each reddening the intended test:** the Latin-square formula (`(r+t)%n → r%n`)
    reddens both square tests; a non-minimum König cover reddens the cover test; removing layer 2
    lets the locally-starved case escape.
  - **F1 is worth recording rather than just passing:** making the Hopcroft–Karp DFS greedy
    (dropping the `dist[w] == dist[u]+1 and dfs(w)` augmentation) does not merely lose maximality —
    it makes `while bfs():` **non-terminating**, because BFS keeps finding augmenting paths the DFS
    never takes. The falsifier hung for ten minutes rather than failing. The augmentation is
    load-bearing for termination, not only for optimality.
  - Verified: `tests/test_feasibility.py` **15/15**; full seedsmith suite **180/180**.
- [x] **P2 — ordering: derive kind-level stages, never hand-label them** · **BUILT 2026-08-31.**
  `seedsmith/planner/ordering.py` + `tests/test_ordering.py` (12 tests).
  - **The prerequisite the plan names, done first:** `KindSpec` gains
    `reference_fields: frozenset[str]`, and the items adapter declares them — `baseType` on
    `unique`, `outputRef` on `recipe`, `sourceAllow`+`groups` on `drop-table`, plus `members` on
    `set` (which the plan's prose omits but the corpus plainly needs: a set references its uniques).
  - **`corpus.discover_edges` is reused, not reinvented**, per the plan. Entry-level reference
    discovery already handles nested paths and skip-fields; ordering only collapses those edges up
    to kind level.
  - **Kahn into layers, not a flat sequence** — layers say what may generate in parallel, which a
    plain topological order throws away for nothing. **Tarjan names a cycle's exact members**:
    "cycle detected" sends a human to read the whole graph, "`recipe` and `unique` reference each
    other" sends them to two files.
  - **Tarjan is iterative on purpose.** The recursive form dies with `RecursionError` on a deep
    graph — a failure that reads as a bug in this module rather than the depth it actually is. A
    2 000-deep chain is a test.
  - **Acceptance, all three met, quoted from the plan:**
    - [x] *"Reproduces the real historical order … `drop-table` lands after
      `unique`/`base-type`/`set`/`gem`/`charm`/`consumable`"* — asserted per-kind, not as one
      sequence, so a single misplacement cannot hide inside a passing whole.
    - [x] *"A synthetic two-kind cycle fixture is caught and both kinds are named by Tarjan's SCC"*
      — plus a three-kind cycle, and two independent cycles reported as **two** SCCs rather than one
      blob (a caller told "a, b, c, d are in a cycle" goes hunting for an edge that does not exist).
    - [x] *"The derived order needs no hand-maintained stage label anywhere in the adapter"* —
      asserted against the **shipped** `KINDS`, not a fixture: the named reference fields are
      declared, and no kind carries a `stage`/`order`/`generation_stage`/`wave` attribute for anyone
      to let go stale.
  - **Falsifiers run, each reddening the intended test:** not collapsing `members[0].ref` to
    `members` (nested references silently ignored — the order looks clean while being wrong, the
    exact shape of the original incident); ignoring `reference_fields` (a `flavorKey` that looks
    like an id invents a dependency); reversing the edge direction (an order that is wrong
    everywhere).
  - **A self-reference is deliberately not a cycle** — `unique.baseType` pointing at another unique
    orders nothing between stages, and reporting it would make the common case look broken.
  - **`stage_of` raises on an unknown kind** rather than returning `-1`, which would sort first,
    silently — the failure mode this whole module exists to end.
  - Verified: `tests/test_ordering.py` **12/12**; full seedsmith suite **192/192**;
    `FusionRpg.ItemSeedValidator.Tests` **71/71** (the C# side that shares `KindCatalog.cs` is
    untouched by the `KindSpec` extension).

### ✅ CP-F1 — the planner refuses the impossible and orders the possible
- [x] Proven against synthetic fixtures reproducing **both** real incidents — 75-into-40 (P1) and the
  274 same-stage errors (P2) — and against the real adapter's own kind declarations.
- [x] **P3 — input validation: the exemplar gate before dispatch** · **BUILT 2026-08-31.**
  `seedsmith/planner/validate.py` + `tests/test_exemplar_gate.py` (9 tests).
  - **Placed before dispatch, not after generation.** A bad exemplar caught afterwards has already
    been copied into everything the order produced; caught here it costs one refusal. The metric's
    own history is the argument: a set exemplar teaching members-by-role-alone produced **30
    uncompletable sets in one wave**.
  - **`ExemplarConformance` is reused, not reimplemented**, exactly as P3 instructs — and that is
    asserted structurally, not just claimed: a test compares the gate's findings against the
    metric's own output id-for-id, so a future change to what a valid exemplar is cannot leave a
    stale second copy of that judgement in the planner.
  - `EXIT_EXEMPLAR_REFUSED = 3` is a named constant per spec-foundation §7.3's CLI contract — a bare
    `3` at a `sys.exit` is a number nobody can grep for.
  - **Scoping (`referenced_kinds`) is about adoption as much as correctness.** A gate that refuses an
    order over a kind the order never touches is one people learn to skip, and a skipped gate
    protects nothing. Both halves are tested: an unreferenced broken exemplar does not refuse, and a
    referenced one still does — without the second, the first would also pass for a gate that had
    simply stopped refusing.
  - **Acceptance, both met, quoted from the plan:**
    - [x] *"A work order referencing a synthetic exemplar with a missing required field is refused,
      not partially emitted"* — refusal is all-or-nothing, and a test proves one broken exemplar
      refuses an order whose other kinds are clean.
    - [x] *"A clean exemplar set passes through untouched"* — plus an empty corpus, which must pass
      rather than block the first run of a new adapter ("no exemplars" is not "bad exemplars").
  - **Falsifiers run, each reddening the intended test:** never refusing (6 red), exit code 3 → 0
    (the CLI contract), and ignoring `referenced_kinds` (scoping).
  - Verified: `tests/test_exemplar_gate.py` **9/9**; full seedsmith suite **201/201**.
- [x] **P4 — scheduling and work-order output** · **BUILT 2026-08-31.**
  `seedsmith/planner/schedule.py` + `tests/test_schedule.py` (14 tests).
  - **⛔ A spec conflict found and resolved, with evidence, rather than picked.**
    `spec-planner.md` §7 says the planner *"must place the four base-type partitions in the
    base-type layer"*. The plan and todo say the **opposite** — exclude them. The newer,
    evidence-backed reading wins: **S2 verified their `_meta.partition` string is wrong while the
    entries' own `role`/`frame` fields are intact**, so those cells hold real content and need a
    **relabel**, not generation. Generating into them would author duplicates. The resolution is
    pinned in both the module docstring and the test file so nobody re-derives it from the stale
    sentence. **Spec §7 should be corrected — owner's call, flagged not silently patched.**
  - **An excluded partition is reported, never silently dropped** (`excluded[]` +
    `EXCLUDED_REASON_MISLABELED`). A partition that vanishes with no explanation reads as a planner
    bug, and someone re-adds it.
  - **`layer` stays "dependency stage"; the concurrency cap is a derived view** (`waves()`). Folding
    the cap into the layer number would conflate *"cannot run yet"* with *"no worker free"* —
    different problems with different fixes.
  - **Model tier is a replaceable table, not an optimiser** — a test swaps it and watches the tiers
    invert, because "auditable" is only true if a reader can actually change it.
  - **Acceptance, both met, quoted from the plan:**
    - [x] *"the emitted plan places `gems/2` after its registry dependency and the three
      display-template partitions after the affix families they render"* — plus the whole plan
      asserted as one shape against spec §7's own standard, *"if the plan matches what a human would
      write, the module works"*.
    - [x] *"The four base-type partitions from S2 are correctly NOT included as generation jobs"*.
  - **Falsifiers run, each reddening the intended tests:** shortest-job-first (3 red), scheduling
    excluded partitions anyway (3 red), dropping the `closes` link, and scheduling past a cycle.
  - Verified: `tests/test_schedule.py` **14/14**; full seedsmith suite **215/215**.
- [ ] ⛔ **Follow-up for the owner:** `spec-planner.md` §7's base-type sentence contradicts the
  shipped decision and should be corrected in the spec, not just in the code that disagrees with it.
- [x] **P5 — generation pipelines: the declare/fulfil split** · **BUILT 2026-08-31.**
  `seedsmith/planner/demand.py` + `tests/test_demand.py` (13 tests).
  - **The defect it removes, in one sentence:** a set's stage 5 can discover it needs a base type
    that does not exist, which looks like a backward edge and breaks any hand-written order. That is
    why "generate sets" and "generate base types" could never be sequenced by a human — the
    dependency is only visible *after* the set is planned.
  - **Phase A declares, Phase B fulfils.** Phase A is pure: no model, no file, safe to re-run, which
    is exactly what lets the whole graph be assembled before anything is decided.
  - **Reuse is the default, and no cap is needed** (spec §8.3, owner decision superseding the
    audit's structural-cap recommendation). With full sight of every need at once, a candidate that
    already served another need is chosen last among equally-good ones. **A cap could only refuse at
    an arbitrary number; the policy weighs spread and still degrades gracefully** — when the only
    candidate has already been used twice it is used a third time rather than the plan failing,
    which is asserted.
  - **Acceptance, both met, quoted from the plan:**
    - [x] *"reuses existing base types where they satisfy the demand and requests new ones only for
      the genuine shortfall, without concentrating all three sets' demand onto the same handful"* —
      three themes × three roles across nine candidates gives **max concentration 1**. The contrast
      test matters more than the assertion: with `spread=False` the same fixture concentrates to
      **3**, so `spread=True` cannot be a no-op passing on a fixture that never concentrates.
    - [x] *"A recipe fixture proves materials are demanded, and therefore generated, before the
      recipe that consumes them — structurally, not by a human remembering the order"* — the
      ordering edge *is* the declared demand, so removing the declaration removes the edge. Asserted
      both ways.
  - **Falsifiers run, each reddening the intended tests:** spread made a no-op; shortfall silently
    dropped; `kind_dependencies` losing the demander→needs edge (3 red — the recipe ordering
    collapses); and an over-loose `satisfied_by` (2 red), which is the subtler failure — matching
    too broadly silently generates duplicates of content that already fits.
  - Verified: `tests/test_demand.py` **13/13**; full seedsmith suite **228/228**.
- [x] **P6 — `briefkit`: work order → briefs** · **BUILT 2026-08-31.**
  `seedsmith/briefkit/{__init__,render}.py` + `tests/test_briefkit.py` (14 tests).
  - **"Inlined literally, never cited" has a check behind it, not a convention.**
    `CITATION_PATTERNS` is grepped over the rendered text and a match **refuses** the brief. The
    patterns match the *shape* of a citation rather than a list of filenames, so a new registry file
    cannot silently become a legal thing to cite. The incident: *"tags come from `tags.v1.json`"*
    cost **51 invented tags** — an agent cannot follow a filename, so it fills the gap.
  - **An empty vocabulary says so** rather than being omitted. An absent section reads as "no
    constraint"; an empty one reads as "nothing is legal here". Opposite instructions, and the
    silent version is the dangerous one.
  - **Acceptance, all three met, quoted from the plan:**
    - [x] *"inlines the literal legal `family` vocabulary … grep the brief text for a citation
      string … and fail if found"* — the literal grep is a test, and a planted citation (via a
      constraint value, the realistic way one sneaks in) is refused.
    - [x] *"Two brief generations from byte-identical inputs produce the identical content hash"* —
      plus the control that a *changed* input moves it, since a hash that never moves identifies
      nothing.
    - [x] *"A brief whose exemplar failed P3's gate is never emitted"* — and the gate is checked once
      for the whole batch, because a half-batch built on a known-broken pattern set is worse than
      none: nothing records which half.
  - **⛔ A falsifier found an untested line and a false claim in my own comment.** Removing
    `sort_keys=True` from `_hash_inputs` reddened **nothing** — every payload the module builds is
    already assembled in fixed order with pre-sorted vocabularies, so the line was real
    belt-and-braces but entirely uncovered, while its docstring called it *"load-bearing, not
    tidiness"*. **Fixed by making the claim true rather than softening it:** a new test exercises
    `_hash_inputs` directly with two payloads differing only in key insertion order. Re-falsified —
    `sort_keys=False` now reddens.
  - Other falsifiers, each reddening the intended tests: dropping the citation check (2 red),
    emitting despite a refused gate (2 red), and not sorting vocabularies (render order leaking into
    both text and hash).
  - Verified: `tests/test_briefkit.py` **14/14**; full seedsmith suite **242/242**.

### ⭐ CP-F2 / CP-F3 — W2 done
- [x] Feasibility, ordering, validation, scheduling, the demand split and `briefkit` all built and
  proven against synthetic incident-replay fixtures (75-into-40, 274 same-stage, 51 invented tags,
  30 uncompletable sets) **and** the real corpus's own remaining gaps.
- [x] **G1 — pipeline scaffold: schema-per-metric + guardrails** · **BUILT 2026-08-31.**
  `seedsmith/pipeline/{model,run}.py` + `tests/test_pipeline_scaffold.py` (16 tests).
  - **The load-bearing guardrail is "never a number", and it is enforced mechanically.**
    `audit_schema` walks `properties`, `items` and the composition keywords, so a numeric field
    three levels inside an array of objects is caught — which is the entire argument for auditing by
    machine rather than by eye. **`integer` counts as numeric**: a per-mille int is exactly the
    shape a model most plausibly invents. An **`enum` of numbers is allowed** — choosing from a
    closed set is not deriving, and over-refusing would push authors to encode numbers as strings.
  - **The audit is wired into `Pipeline.__post_init__`**, so an unusable schema cannot be
    *registered*. A lint nobody runs is not a guardrail.
  - **`call_with_self_heal` (S0) is reused, not rebuilt**, generalized from a flat string-keyed
    payload to a schema-validated object. `MockModelServer` is imported from the existing S0 tests
    rather than re-rolled — a second fake server would drift from the one the transport is actually
    tested against.
  - **`blocked` is an answer, not a failure.** It writes nothing, is reported with its reason, and
    is **never retried** — a pipeline that retries "I cannot" burns its whole budget learning
    nothing. `escalated` is kept separate, because collapsing the two hides the difference exactly
    when someone is deciding whether to intervene.
  - **Acceptance, all three met, quoted from the plan:**
    - [x] *"A schema-audit test rejects any registered pipeline whose schema has a bare numeric
      field"* — plus the positive control that the shipped flavour schema passes its own audit,
      without which every rejection test is satisfied by an audit that refuses everything.
    - [x] *"proves retry-with-named-defect then escalate-on-persistent-failure, with zero real model
      calls"* — the heal prompt is asserted to contain the offending field **and its bad value**, and
      the retry budget is asserted bounded (1 initial + 2 heals). Every request went to a loopback
      port the test itself opened.
    - [x] *"A `blocked` response writes nothing and is reported, not treated as a failure"*.
  - **Falsifiers run:** allowing `integer`; not recursing into arrays; unwiring the audit from
    construction; and treating a block as a hard defect (2 red). Each reddened its intended test.
  - **⛔ One falsifier did NOT redden, recorded rather than papered over.** Removing the
    *persist-time* re-gate changed nothing, because `verify` already gates every value before it can
    reach persist — the branch is unreachable through the current heal loop by construction. Unlike
    briefkit's `sort_keys` case, the code comment there already claims only that it guards *"even if
    the heal loop changes"*, which is accurate, so the honest action is to record the gap rather
    than invent a test that fakes reachability or soften a claim that was never overstated.
  - Verified: `tests/test_pipeline_scaffold.py` **16/16**; full seedsmith suite **258/258**.
- [x] **G2 — idempotence and provenance** · **BUILT 2026-08-31.**
  `seedsmith/pipeline/provenance.py` + `tests/test_provenance.py` (13 tests).
  - **⛔ I had called G2 blocked in the previous turn — that was overstated, and it is corrected
    here.** I said it "needs decisions about how generated content is written into the corpus". It
    does not: its two acceptance criteria are concrete, and `Pipeline.on_persist` is already an
    injected seam, so idempotence and provenance are testable without deciding corpus layout at all.
  - **The finding is checked before the ledger, and the order is load-bearing.** A finding closed by
    a *human*, or by another pipeline, must stop this one — checking only "did I already run" would
    regenerate content whose reason for existing had gone away.
  - **Re-recording a row raises rather than last-write-wins.** Two runs both believing they created
    a row *is* the duplicate write G2 exists to prevent; overwriting would hide it.
  - **The timestamp is injected**, like the kernel drive's stopwatch. A clock read internally makes
    provenance non-reproducible, and a test that cannot pin it either asserts nothing about it or
    goes flaky.
  - **Acceptance, both met, quoted from the plan:**
    - [x] *"Running a pipeline twice over unchanged input produces zero new writes on the second
      run"* — and the second run is asserted not to call the model **at all**, not merely to discard
      its output.
    - [x] *"Provenance is queryable by finding id"* — both halves: *why does this row exist* (the
      finding) and *which prompt version produced it* (scoping a bad batch by version is exact;
      scoping by timestamp is a guess about when a change landed).
  - **Falsifiers run, each reddening its intended test:** reversing the check order, last-write-wins
    on a duplicate row, `stamp` mutating in place, and skipping the already-generated check.
  - **⛔ A test whose name outran its fixture, caught by falsifying.**
    `test_the_finding_is_checked_before_the_ledger` used an *empty* ledger — with which either check
    order reaches the same answer, so it asserted nothing about ordering. The falsifier reddened a
    different test than the one named for the property. Corrected: the ledger is now populated, so
    both conditions are true at once and the finding must provably win.
  - Verified: `tests/test_provenance.py` **13/13**; full seedsmith suite **271/271**.
- [x] **G3 — open-loop review queue wiring** · **BUILT 2026-08-31.**
  `seedsmith/pipeline/open_loop.py` + `tests/test_open_loop.py` (24 tests).
  - **The rule, in one line: a pipeline may not mark its own homework.**
    `audit_open_loop_schema` rejects any verdict field — `pass`/`ok`/`valid`/`quality`/`score`/
    `verdict`/`grade` and friends — matched **normalized**, because `qualityOk` and `is_valid` are
    the same mistake wearing different spellings. It recurses, for the same reason the numeric audit
    does: a `score` three levels down grades just as effectively and is harder to spot.
  - **`blocked` is explicitly not a verdict.** Declining to do the work is not a judgement about the
    work's quality; conflating them would remove the model's only honest way out, which is the
    opposite of what G1 built.
  - **Over-refusing is guarded too** — `passage` must not trip the `pass` rule. A guard authors work
    around by renaming fields is worse than the defect it prevents.
  - **The sampler is `seedsmith.sampling`, reused** — asserted structurally
    (`stratified_sample.__module__`), because a second sampler would drift from the every-stratum
    guarantee and the drift would be invisible until a band went unreviewed.
  - **Acceptance, both met, quoted from the plan:**
    - [x] *"An open-loop pipeline's schema never includes a pass/fail field"*.
    - [x] *"Re-running `metrics` … still reports the same finding as open-loop (never silently flips
      to a pass)"* — every finding is `NOTE` + `needsReview`, and generation is asserted to **add**
      review work rather than clear it. A pipeline able to close its own open-loop finding would
      make the queue look emptiest exactly when it had just filled it.
  - **Two falsifiers did not redden, and both were my fault rather than the code's:**
    1. **F1 never actually applied** — my planted-edit search string used single quotes where the
       source has double, so nothing changed and the "green" result was meaningless. Re-run
       correctly, it reddens the `isValid` case: the normalization is genuinely load-bearing. **A
       falsifier that silently fails to plant is worse than none, because it manufactures
       confidence.**
    2. **F5 (unsorted strata) reddened nothing because the test could not reach it** — that fixture
       passes a fixed list, so the strata dict is insertion-ordered and stable. The sort earns its
       place against a *caller* whose candidate order varies between runs, which is the shape
       `FlavourGeneric` was actually bitten by. Added a test that passes the same candidates in two
       different orders; F5 now reddens.
  - Other falsifiers, each reddening the intended tests: no array recursion, `GAP` instead of `NOTE`
    (2 red), and treating `blocked` as a verdict (2 red).
  - Verified: `tests/test_open_loop.py` **24/24**; full seedsmith suite **295/295**.
- [x] **⭐ CP-G — the loop closes end to end, no real token spent** · **BUILT 2026-08-31.**
  `tests/test_cp_g_end_to_end.py` (4 tests).
  - **The full chain, as one narrative:** `metrics` finds partition `b` allocated-but-empty →
    `planner` schedules it **from the finding**, with `closes` naming it → `briefkit` briefs the job,
    inlining the legal colour vocabulary and citing nothing → `pipeline` generates against
    `MockModelServer` → the entry is stamped with provenance and written → **`metrics` re-run
    reports the finding cleared.**
  - **Each stage consumes the previous stage's own output.** No stage is handed a fixture standing
    in for the one before it — that is the only way this proves the *loop* rather than proving five
    modules separately.
  - **The stub adapter is used rather than the item corpus, deliberately** (`spec-foundation` §2
    keeps it for exactly this). It proves the machinery closes the loop, not that one particular
    corpus happens to.
  - **Falsified three ways, each reddening the closure assertion:** never writing the generated
    entry; writing it into the *wrong* partition (the subtlest — the file exists, the corpus grows,
    and the finding stays open); and a schema-violating value from the model, which the gate stops
    before anything is persisted.
  - **Two honest failure paths are asserted too:** a dependency cycle refuses the whole schedule
    rather than half-running it, and a **blocked** model leaves the partition empty and the finding
    **open** — the alternative is a loop that reports success while the corpus gained nothing.
  - Verified: `tests/test_cp_g_end_to_end.py` **4/4**; full seedsmith suite **299/299**;
    `FusionRpg.ItemSeedValidator.Tests` **71/71** (the C# side sharing `KindCatalog.cs` is still
    untouched by the `KindSpec` extension P2 added).

**Seedsmith W2 + W3 are now complete through CP-G.** Every incident the program was built to end has
a structural fix with a replay fixture: 75-into-40, the 274 same-stage errors, 51 invented tags, 30
uncompletable sets, and recipes-before-materials.
- [ ] …remaining items enumerated at phase start from the program's own todo.

### ✅ Checkpoint 5 — backlog cleared
- [ ] Every phase above closed, or explicitly reclassified with a reason.
- [ ] The only open items left across all `tasks/*-todo.md` are: owner live runs, class-system
  P9.3/P9.4 (blocked on real play), action A9/A10 (owner deferral), and the three review ticks not in
  the 2026-08-31 batch.
- [ ] Commit drafts handed over per phase (**git hands-off — never commit**).
