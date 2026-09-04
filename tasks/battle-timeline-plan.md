# Plan: battle-timeline — the virtual-time battle kernel

Map: [docs/architecture/battle-timeline-map.md](../docs/architecture/battle-timeline-map.md) · Ideal: [battle-turn-ideal.md](../docs/architecture/battle-turn-ideal.md) · Specs: [docs/architecture/battle/](../docs/architecture/battle/spec-virtual-time-core.md) · Audit: [audit-2026-08-21.md](../docs/architecture/battle/audit-2026-08-21.md) · Perf: [spec-kernel-performance.md](../docs/architecture/battle/spec-kernel-performance.md)

Task list: [battle-timeline-todo.md](battle-timeline-todo.md). *(`tasks/plan.md` and `tasks/todo.md` hold the Perf v3 program; this stream uses the `battle-timeline-*` pair.)*

**Revised 2026-08-22** after the performance decision. What changed and why is in "The gap this revision closes" below.

## What we're building

A virtual-time kernel — simulation clock, event queue, per-actor state machine — on which every battle mode is configuration rather than a code path, and on which combat action management (skills, attack, defence, movement) is later scheduled.

## Owner decisions this plan is built on

1. **Full kernel** — reaction lane, side-scheduled press-turn, multi-actor link-strikes (2026-08-21).
2. **T9 after the gate, versioned** — T5 preserves today's sub-round under-delivery; T9 fixes it deliberately.
3. **Full live sessions** — real interactive input, which makes the determinism trace mandatory.
4. **Profile chosen by content**, resolved from the existing `WaveId` / tier id so nothing new is serialized.
5. **The kernel runs per-frame inside the injector** (2026-08-22) — and performance is a first-class feature, not a later pass.

## The gap this revision closes

Decision 5 named where the kernel runs, and **the previous plan had no module that runs it there.** T7 observes the game; nothing *drove* the kernel inside the frame. That module now exists as **T13 `injector-kernel-drive`**, and it is not a thin adapter — it is the task that replaces the injector's ad-hoc timing grids (the 100 ms shield tick, the 100 ms DoT grid) with the kernel's event queue. Those grids *are* a primitive scheduler, so this is the same single-source-of-truth argument that motivated the whole program, applied to the injector.

Three consequences:

- **Performance became a constraint on type design**, not a tuning pass. Frame-critical Unity code cannot allocate per tick, so the expensive mistakes are structural (a class where a struct belongs, a string key on a tick path) and cost a rewrite rather than a tune. Phase P now sits *before* the kernel work rather than after it.
- **The riskiest module is now T13, not T5.** T5 is protected by goldens; T13 touches the hot path this repo has already had to rescue once, where the failure mode is stutter that no unit test sees.
- **So T13 gets a measurement gate in front of it.** P2 measures the kernel at stress scale in a synthetic harness *before* any injector code is written. If the kernel cannot hold its slice offline, it will not hold it in the frame, and we learn that for the cost of a harness.

## Scope honesty

~~Thirteen modules across seven phases.~~ **Seventeen across nine, as of 2026-09-04**: T14
`timeline-tunables` and T15 `profile-migration` were added by the reconciliation pass, and **B37
`fsm-routing` and B38 `turn-cycle` were added mid-build** — both were B9's own deliberately deferred
half (the code says so: *"B9's own remaining half … is NOT attempted here"*), never scheduled, and
between them they gated six other items. The ordering still protects the existing game early (the gate
at Phase 2) and puts every piece of new machinery behind its own checkpoint. **If the program stops at
any checkpoint, what shipped is coherent and green.**

~~Five modules (T6, T7, T8, T10, T11) are still unspecced; their tasks begin by writing the spec.~~
**All five are specced as of 2026-09-04** — `spec-turn-order-forecast.md` (T8),
`spec-interactive-turns.md` (T6+T10, one spec because the map binds them), `spec-pvz-observer.md` (T7),
plus `spec-timeline-tunables.md`, `spec-profile-migration.md` and `spec-fsm-routing.md` for the four
new modules. T11 is specced within the interactive spec.

## Slicing principle

Vertical: each task carries one complete path — types, behaviour, and its tests — not "all the records, then all the logic". The kernel stays provable with zero real actions until Phase 1's last task, which deliberately drives a **real** attack through it, because the audit's sharpest finding was that the seam would otherwise reach the gate with no consumer.

## Phases

### Phase 0 — prerequisites *(complete)*
The `decisions.md` row, and the RNG observation seam plus pre-adoption fixtures — which could only be captured before the kernel existed.

### Phase P — the performance contract *(P1a complete)*
Budgets inherited from `perf-probe-plan.md`, never restated. Zero steady-state allocation, asserted in CI as **bytes not milliseconds** — a wall-clock assertion in CI measures the build agent's mood and gets muted the first time it flakes. P1d (tick-path source guard) and P2 (stress harness) can run now; P1b/P1c need something ticking in the injector and land with T13.

### Phase 1 — the kernel (T1–T4) *(B2–B12 complete — Checkpoint A CLOSED 2026-08-28)*
Pure, no game attached, ending with a real attack under `galaxy-sync`.
**Checkpoint A — capability.**

**Session scope through Checkpoint A, set explicitly 2026-08-28 — fully delivered.** (the 2026-08-22 hold on B6+ —
"define the action architecture before building on an unvalidated seam" — is lifted: `action-map.md`
is sealed and its own Checkpoints A/B/C are ✅). B6 (reaction lane) is done. Remaining: B7–B12, in
the map's own dependency order (T2 finishes with B7–B8 before T3's B9–B10, before T4's B11–B12).
B12 is documented `Scope: L` and is its own session, not part of this push. Full task breakdown,
acceptance criteria, and verification commands live per-item in `battle-timeline-todo.md`; the two
design calls made **up front so they are not discovered mid-build**:

1. **B7 (rendezvous) stays FSM-neutral, deliberately avoiding the spec's "ask first" boundary on
   adding a `TurnState`.** A reserving actor stays in `Ready`; an external coordinator tracks
   reservation membership and the bounded dwell timeout. "Two actors commit together and produce
   one `Resolving`" is satisfied at the **scheduling** level — one shared resolve `EventHandle` for
   every linked participant, not a merged state machine — so no new `TurnState` and no new legal
   transition row are needed.
2. **B10's `PressTurn` does not need the spec's sketched `ISchedulable` union type.** `EventQueue`
   already schedules against an opaque string `OwnerKey`; a side-scheduled event is a distinct key
   namespace (`"side:left"`), the same trick `CooldownSlot` already uses, not a new type.

### Phase 2 — the gate (T5) *(B13–B15 complete — Checkpoint B CLOSED 2026-08-28)*
`BattleEngine` gives up its loop. Byte-identical: eight goldens still, six suites green with no test edits.
**Checkpoint B — safety.** Nothing proceeds past a drift. **No drift occurred** — every verification
pass (goldens, expedition hashes, pre-adoption trace fixtures, the new event-sequence fixtures, all
6 suites, all 4 guards) matched on first attempt. `BattleEngine.Resolve` now runs its round boundary
on `Timeline.EventQueue`/`Timeline.SimulationClock` instead of a raw integer counter; the ten-step
round body stays synchronous and unchanged, per Design decision 2 above. Full evidence:
`battle-timeline-todo.md` B13–B15.

**Scope set 2026-08-28, owner-directed.** With multiple battle types/maps planned, the owner chose
to build the complete correct engine now rather than the minimal-risk slice — Phase 2 **and** Phase
3 in the same push, sequenced (byte-identical first, so an adoption bug can't hide inside an
intentional timing change) rather than combined into one re-bless. Design calls made up front:

1. **The profile parameter is a 5th optional trailing parameter on `Resolve`, not a new overload.**
   The spec's "53 call sites / 2-arg overload" framing is stale — the real count is **~80 call
   sites across 18 files**, and `Resolve` already has two optional trailing parameters
   (`trace`, `onEffectHostReady`). Adding `BattleModeProfile? profile = null` after them preserves
   every existing call site verbatim; `null` resolves to `ClassicRound`, mirroring B12's
   `WaveDef.Profile` resolution.
2. **B14 schedules the existing 10 round-skeleton steps as kernel events — it does not route
   combat through the per-actor turn FSM.** `ActorTurnMachine`/`ActionRunner`'s envelope is future
   enrichment work (`battle-timeline-map.md`: "E2 skills... respec after T5"), not this gate's job.
   Every step's body stays exactly as it is today; only what sequences the steps changes from
   inline statement order to `EventQueue`-scheduled offsets — which is how the seven named
   byte-identity hazards stay preserved by construction.
3. **The parity ladder validates against the existing pre-adoption fixtures before any golden hash
   is checked.** `PreAdoptionTraceTests.cs` + `tests/fixtures/battle-traces/{stomp,close,wipe}.trace.txt`
   already exist (B1) and are real, live-asserting fixtures, not just commit-note claims — confirmed
   by direct research before this plan was written.

### Phase 3 — the deliberate change (T9) *(B16–B18 complete — Checkpoint B2 CLOSED 2026-08-28)*
Status pulses and shield upkeep move onto kernel ticks and start firing at true times. **Landed
with zero version bump** — verified, not assumed, that no authored content exercises a sub-1000ms
status period or a non-round-aligned shield duration, so the fix has no measurable delta today;
`tools/CombatSim` can't see this path either (separate `StatusModel.cs`, never the real engine
loop). The bump-vs-hold call was put to the owner rather than decided unilaterally — owner chose to
hold, with the trigger condition for the next bump recorded in `decisions.md`. **A real infinite-loop
bug was found and fixed during B16** (a pure-CC status's `NextPulse` never advances under
`StatusRuntime.Tick`'s own `Kind` gate, so naively scheduling against it spun forever) — caught by a
30 GB runaway test host before being isolated and fixed at the root cause, not papered over with a
round-count cap alone. Full evidence: `battle-timeline-todo.md` B16–B18.
**Checkpoint B2 — versioned.**

### Phase 4 — interactive battles (T8, T6, T10, T11) *(B19–B22 complete — Checkpoint C CLOSED 2026-09-04)*
Forecast, then the dwell, then the trace, then live sessions. **T6 and T10 shipped together**, as the plan required — an interactive battle without a persisted decision trace is precisely the hole where a boot sweep silently overwrites a player's win.
**Checkpoint C — CLOSED** on its own criterion: every interactive battle replays from its trace, and the
sweep refuses an untraced interactive match terminally rather than healing it. A timeout is recorded as
a decision **at a tick**, never re-measured — the determinism trap this phase existed to avoid.

### Phase 5 — the observer (T7) *(B23 complete 2026-09-04 — Checkpoint D 2 of 3)*
Stateless projection of live PvZ events into the same vocabulary. No queue, no scheduling, no per-actor machine injector-side.
**Checkpoint D — CLOSED**: the projection and the no-injector-state rule are proven (zero allocation
over 100,000 projections, in Core so CI actually builds it). ⛔ **`Committed` is deliberately never
projected** — PvZ has no observable moment between deciding and resolving, so the vocabulary is shared
but the coverage is not. ✅ The third clause, **"frame budget held at 200+ entities"**, ~~needs a deploy
and a stress scenario and is owner-run~~ — **measured live 2026-09-04 on a 252-entity board**:
`loop.tick` **0.040 ms/frame** against a 2 ms budget, kernel share **0.0018 ms** against 0.15 ms, **zero
gen2 collections**, at a steady 60 fps. **"Owner-run" was the wrong label** — the run is assistant-safe
via runbook §3's bare build, which deploys straight into the game's `Mods/` folder.

### Phase 6 — the injector drive (T13) — *the highest-risk phase*
The kernel ticks inside the frame and takes over the injector's timing grids, under a bounded resumable drain with live probe sections. Gated by P2's measurement and verified against the B1–B9 matrix at 200+ entities.
**Checkpoint E — program complete.**

### Phase 7 — the balance surface (T14 `timeline-tunables`)
The kernel's numbers were never triaged against `tunables-ssot.md`, which landed three days after this
program was specced. Every number under `Battle/Timeline/` becomes either a published key in
`battle.v2.json` or a `const` that says why it is not tunable. **No dependencies — it can run in
parallel with Phases 4–6.** Byte-identical throughout: it relocates values, it does not change one.
**Checkpoint F.**

### Phase 8 — the profile migration (T15) — *the only phase that moves the economy*
Expeditions and web matches leave `classic-round` for `hybrid-atb`, so `turn.speed`/`turn.haste`
finally matter in production. **Four axes change at once** (advance policy, `W` 1→4, commitment,
economy), so the sweep is staged across five configurations and its deliverable is an *attribution
table*, not a verdict. Two gates run first: close the `KernelPurityScan` hole, and measure
`FixedIncrement` resolve cost. **Its re-bless is shared with Phase 6's B26** — one bump to
`RulesetVersion` 5 covering both movers.
**Checkpoint G — program complete.**

## Risks and how the plan handles them

| Risk | Handling |
|---|---|
| Kernel cost shows up as stutter no unit test sees | P2 measures at stress scale **before** T13 exists; CI asserts zero-allocation continuously; probe sections measure live |
| A frame held hostage by a large drain | Bounded and resumable, following event-pipeline-v2's frame-budgeted precedent; simulated time is decoupled from wall-clock, so a deferred drain is a pacing effect, not a correctness one |
| "Runs in the injector" quietly becomes "drives the game" | Written boundary: the kernel schedules *our* timeline; Unity still owns its own actors. PvZ stays observed |
| T13 regresses working timing grids | Sequenced last, behind the gate and the observer, with the existing grids' behaviour as the acceptance baseline |
| The gate fails with an opaque hash diff | The parity ladder localises drift to a stream, phase, round, or event before the hash is consulted |
| Live sessions silently corrupt match history | T10 ships with T6; timeouts recorded **as decisions at a tick**; the sweep refuses incomplete traces rather than healing them |
| **Four profile axes move the goldens together and nobody can say which did it** | Phase 8's sweep runs **five** configurations, one axis at a time, and its acceptance is that the four deltas **sum** to the observed total. If they do not, the interaction is named before the re-bless, not after |
| **`FixedIncrement` makes expedition resolve expensive** | Measured before the migration, not assumed — the ~50,000-steps-vs-few-hundred-pops figure is an estimate and is labelled as one. `galaxy-sync` for expeditions is the pre-agreed fallback, **but it needs a per-surface axis that does not exist**, so it is a scope change and is budgeted as one |
| **Phase 8's flip is coupled to B26, which is blocked on an owner spec review (B24)** | Everything in Phase 8 *except* the flip is independent and proceeds. If B26 slips far enough that holding the flip costs more than a second bump, that is an owner call **at that moment** with a named trigger — not a decision to pre-empt now |
| **T14 turns into a balance pass by accident** | Every published value is extracted from the shipped code, and any value differing from what the code holds today is a defect in the module. Changing one is explicitly ask-first |

## Verification standard

Every task: its own tests green, the full Core suite green, no edits to existing tests. From Phase 2 on, also the four boundary guards. From Phase 6, also the kernel's probe sections within budget. **Golden movement — corrected twice, and the second correction is the interesting one.** The original
wording predicted "exactly twice". Phase 3 (T9) closed with **no** version change, and then **B35
measured that Phase 8's profile flip moves no golden either** — the golden fixtures use `"golden-*"`
wave ids that are not in `WaveCatalog`, and the expedition tier hash covers the expedition *plan*, not
resolved battle reports. **Verified by probe**, not predicted: the four rows were temporarily flipped to
`hybrid-atb` and the full golden set re-run at 40/40.

So the count now stands at: **zero movements through Phase 8**, with `RulesetVersion` still **4** —
and by the end of 2026-09-04, **zero across the whole program**. ⭐ **Three separate predicted
golden-movers were each measured to move nothing**, which is the most useful single result of this run:
**B26** (the scaled clock is injector-side; Core has no `Time.timeScale` and `SimulationClock` cannot
read a wall clock), **B36** (the fixtures use `"golden-*"` wave ids absent from `WaveCatalog`; the
expedition tier hash covers the *plan*), and **B39** (readiness only reorders a round when speeds
differ, and no shipped content authors a `turn.speed`).

⛔ **The lesson is not "predictions are pessimistic" — it is that a predicted re-bless was twice used as
a reason to defer real work, and was wrong both times.** Test the constraint before declaring it.
Most of this run shipped its mechanism **inert** — riders on no trait, `secondaryWeightMilli` at 0,
per-wave `W` unset, skill channels at neutral — which is *why* nothing moved. **B36 is the exception
and is deliberately live:** the four waves now select `hybrid-atb`, which really does change how a
battle resolves (two action points per round). It still moved no golden, because the golden fixtures
use `"golden-*"` wave ids that are not in `WaveCatalog` — measured, not assumed.

**Test baseline — re-measure it, never assume it.** The 14-red-Core/2-red-Data figure this plan used to
quote was stale before it was read; so was every figure that replaced it. Across one session it read
**14/2**, then **2/3**, then **10 Core**, then **6 Core / 3 Data** — ⛔ **the number is a reading, not
a constant, and it moved four times while other streams worked.** Never carry it forward.

**The final reading for this run, every figure from an executed command:** Core **6 failed / 5 915
passed** (demons 4, class-system 2) · Data **3 failed / 684 passed** (atom 1, demon-import 2) · Guard
**170/171** (the known class-system dominance drift) · the battle-scope filter **403/403** ·
four boundary guards green · overflow **A1=0, A2=0, 0 critical** · magic numbers **M1 = 1**, in the
atom stream's untracked `BulletModifyMath.cs`. **Zero failures anywhere in battle or timeline**, and
every red belongs to another stream and was red before this work.

⚠️ **Two run hazards that cost real time here, both self-inflicted and both avoidable.** (1) **Any Core
change invalidates every `tools/` binary that references it** — six tests that shell out with
`--no-build` reported false regressions until the eleven tools were rebuilt. (2) **Never run two
`dotnet test` invocations concurrently**: an orphaned `testhost` holds `FusionRpg.Core.dll` and the
next build fails with `MSB3027` after ten retries, which reads exactly like a real breakage. Serialise
the suites, and `taskkill /IM testhost.exe` before a fresh run if one has been interrupted.

## Not in this program

Action *content* — specific skills, attacks, defences, damage numbers, targeting shapes, AOE, projectiles. That is the next program, and this one exists to give it a timeline. E1 (riders) rebases onto the timeline after T9 — done, see `combat/spec-battle-enrichment.md`'s 2026-09-04 header. **E2 (skills) was not rebased but replaced**: `combat/spec-species-skills.md` builds no catalog and no vocabulary, because `ActionRow`/`ActionEnvelope`/`ActionCatalog` already carry every field its `SkillDef` proposed. E3 (hybrid payloads) is resolver-side and independent.
