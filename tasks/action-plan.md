# Plan: action program

**Rewritten 2026-08-27** against the sealed [action-ideal.md](../docs/architecture/action-ideal.md),
the revised [map](../docs/architecture/action-map.md) (16 modules) and
[audit-2026-08-27.md](../docs/architecture/action/audit-2026-08-27.md) (11 findings, all resolved).

Task list: [action-todo.md](action-todo.md). Paths are prefixed because `tasks/plan.md` and `tasks/todo.md`
hold **Perf v3**.

---

## 0. Two owner instructions that shape this plan

**2026-08-27, and they change how it is written:**

> *"Don't add a gate if it blocks the build."*
> *"Better to build then tune later by tunable variable, instead of trying to build a perfect system
> without data to prove it perfect."*

### 0.1 No human gates. Mechanical assertions only.

The previous plan had two ⛔ checkpoints that **stopped and waited for a sign-off**. Those are gone.

| | Old shape | This plan |
|---|---|---|
| Byte-identity | ⛔ *"stop, do not bless"* | `BattleGoldenTests` **already refuses a silent re-bless**. A moved golden is a **red test**, which stops the build without stopping the builder |
| Balance | ⛔ owner sweep sign-off | a **recorded number** in a baseline file, diffed by a script |

**A checkpoint here means: run these commands, record the numbers, continue.** The only stop is a failing
test — and a failing test is information, not a queue.

### 0.2 Every balance number ships as a tunable with a working value

This is not a shortcut; it is the repo's own standard. PS-7: *"being wrong costs a config version, not a
refactor."* `tier-bands.v1.json` says it of itself: *"working values chosen to make the corpus resolvable,
**not a validated balance decision**."*

So every number below — `p1`, `delta`, `floor`, `cap`, the rung multipliers, the cost tax, the predicate
floor — **lands with a starting value and a declared metric**, and is solved from play data later. **No task
waits on a measurement.**

> The one thing that is *not* deferred is a number's **shape**: `long` not `float`, per-mille not
> fractional, tunable not `const`. Getting the shape wrong costs a refactor; getting the value wrong costs a
> file save.

---

## 1. Shape of the work

**16 modules, 11 phases, ~36 vertical slices.** Every slice is one complete path — row to read, or seam to
consumer — never a horizontal layer.

```text
P0 prerequisites (2 other programs + our guard)
    |
P1 the row + the ladder        A1 A12
    |
P2 targeting + usability       A2 A4
    |
P3 THE PROOF                   A5        <- freezer; nothing else lands in this window
    |
P4 costs + pools               A3
    |
P5 progression                 A11 A16
    |
P6 grants                      A15
    |
P7 defence                     A8
    |
P8 duration                    A14
    |
P9 catalog + generation        A6 A13
    |
P10 selection                  A7
    |
deferred                       A9 A10
```

### 1.1 Three orderings that are not negotiable, and why

1. **`P0.1` (purity guard) before the first line of `Core/Actions/`.** Audit C1 of the *previous* audit:
   that directory has no determinism enforcement. A file landing before the guard is a file nobody checked,
   and wall-clock or ambient-RNG damage is invisible until a replay fails.
2. **Parity capture (`T11`) before any engine change.** You cannot prove byte-identity against a baseline
   you did not record.
3. **`A3` after `A5`, and `A8` after `A5`'s window closes.** `decisions.md` Golden ordering: *"freeze first,
   move last — if a mover overlaps a freezer, neither can attribute a hash change to its own work."*
   **This is a sequencing rule, not a gate** — nothing waits on a person.

Everything else may be reordered if it helps.

### 1.2 Phase 0 is work, not a gate

**Owner, 2026-08-27:** *"we will extend atom effect before we build any action — so build order is extend
dependencies first."* That is the order, and the plan follows it.

But three of the five prerequisites have **seams that let the dependent slice ship without them**, which is
what keeps this an order rather than a blocker:

| Prerequisite | If it is late |
|---|---|
| `P0.2` linkage | only linked actions wait. Nothing else in 36 slices touches it |
| `P0.3` predicate pricing | `T5`'s monotonicity assertion ships with `_meta.measurable` recording its state, and is re-run when pricing lands |
| `P0.5` `turn.speed` | `A14` ships `IDurationResolver` + the clamp; only `BattleDurationResolver` waits |
| `P0.4` `holdsStock` | only consumable actions wait |
| **`P0.1` purity guard** | **no seam. This one really is first** |

---

## 2. Phases

### Phase 0 — prerequisites

`P0.1` is ours and blocks the first line of action code. `P0.2`–`P0.5` belong to two other programs; this
program supplies the requirement and the tests.

### Phase 1 — the row and the ladder (`A1`, `A12`)

Five slices, each row → store → read. `A12` lands **with** `A1` rather than after it, because `A3` and `A11`
both read the rung and two readers of one table is why it exists separately.

**`T3` is easy to skip and expensive to add later** — `rpg_action_grant` is the correction another program
found, and the item lane is blocked on it.

### Phase 2 — targeting and usability (`A2`, `A4`)

`T7`'s **no-board pass-through is the single line `A5`'s freeze rests on.** Asserted here and again in `A5`
— deliberately twice, because one test proves the rule and the other proves the freeze depends on it.

Gate order is not style: it is what lets `A7` hoist per-actor and per-action work out of the target loop,
and it is asserted by **read count**, not by reading the code.

### Phase 3 — the proof (`A5`)

The byte-identity slice. **This is the freezer**, so `A3`, `A8` and `A13` all sit outside its window.

`T14` is where the two shipped `D6` comments close: if an action's atoms resolve in battle, `resource.delta`
and `shield.grant` go **Full** there.

### Phase 4 — costs and pools (`A3`)

The pools are **already registered** (`DerivedStatRegistry.cs:165-171`); this phase is their **reader**.

### Phase 5 — progression (`A11`, `A16`)

`A16` lands with `A11` because a held pool nothing can equip from is not testable, and because **auto-equip
is what lets every non-player actor arrive equipped**.

### Phase 6 — grants (`A15`)

Closes the nine-item handshake. `T23`'s assembly is the entry point the item lane is explicitly forbidden
from implementing.

### Phase 7 — defence (`A8`)

Guard as a stance. **Not blocked on timeline B6** — but it lands after `A5`'s window, per §1.1(3).

### Phase 8 — duration (`A14`)

Ships the seam and the clamp; the battle resolver waits on `P0.5`.

### Phase 9 — catalog and generation (`A6`, `A13`)

`A13` is the **runtime** generator — the loot model. Seedsmith is a dev tool and comes **after** this whole
program.

### Phase 10 — selection (`A7`)

`T35` before `T36`: the `IBattleView` seam erodes on the first convenient shortcut if the AI is written
first.

---

## 3. Checkpoints — all reporting, none blocking

| After | Record | Red means |
|---|---|---|
| **P1** | schema round-trips; every validator rejects a planted row | a validator that cannot fail |
| **P2** | `FactReader.Reads` per gate; zero-alloc evaluation | the hoist is not happening |
| **P3** | 8 goldens byte-identical · `RulesetVersion` 2 · six suites green **with no test edited** | the model is wrong — **not** a re-bless |
| **P4** | lazy regen == scheduled regen; **zero** timers at 200 actors | a scheduled-event regression |
| **P5** | discard does not restore chance; auto-equip deterministic across shuffled input | the ratchet leaks |
| **P6** | all nine handshake items tickable by the item lane | the seam is still one-sided |
| **P7** | `r = poiseRegen / peerPressure < 1` from **emitted metrics** | guard is unbreakable |
| **P8** | a duration-stacking build stays bounded | the clamp is in the wrong place |
| **P9** | every conditional payoff has an enabler in its pool | the discount pays for an unreal combo |
| **P10** | battle **terminates** when nobody can declare | a hang, which is a stopped clock |

**None of these waits on a person.** Each is a command that exits non-zero.

---

## 4. Risks

**The parity harness is the whole program's insurance.** If `T11` is thin, `T14` can only say *"the hashes
match"* — and when they do not, there is no way to tell **which** draw moved. Record values per stream, not
counts.

**`A7` is golden-neutral only while there is no board.** With no coordinates, "nearest" falls back to source
order, which is what `SelectTarget` already does. The moment `A10` lands, this module starts moving hashes.

**This program does not prove `W`.** No wave-1 action consumes a slot in a way that exercises concurrency
width. *"The slot tests pass"* must not be mistaken for coverage that does not exist here.

**Auto-equip is invisible to the dominance guard.** That matrix compares **allocations, not loadouts**, so
`T22` records the auto-equipped set in the report — otherwise a dominant auto-loadout ships green.

---

## 4a. Reopened 2026-08-28 — A17–A20 (Phase 11)

The plan above closed with the action program built but never wired into a real battle — proven by
a completeness audit, not assumed. **This reopening's whole point is Checkpoint A/C's own promise,
delivered for real:** `BattleEngine` calling `StubIntentSource`/`ActionCatalog` at runtime instead
of its own hardcoded `SelectTarget`. Full scope, the two explicit owner decisions (full switch-over;
full multi-action loadouts), and what stays deferred: [action-map.md](../docs/architecture/action-map.md)
§12. Tasks: `action-todo.md` Phase 11 (T35–T39). Module spec:
[spec-action-selection-adoption.md](../docs/architecture/action/spec-action-selection-adoption.md).

**Unlike the plan above, this is explicitly a golden-mover**, not gated by "don't block the build" —
it needs its own re-bless, predicted delta, and win-rate sweep (§12.2's golden-ordering rule),
following this repo's own established discipline for a deliberate change rather than skipping it.

**Closed 2026-08-28.** T35-T39 landed; Checkpoint E closed on the finding that the switch-over was
byte-identical for every battle that exists today (zero goldens moved, measured not assumed) —
`RulesetVersion` held at 4 by owner choice. See `action-todo.md` Phase 11 for full evidence.

## 4b. A18 split into A18a–e (2026-08-28, Phase 12)

A18 ("resolve whichever action A17 chose") turned out to bundle five independently testable
capabilities once specced — a genuine Phase 0 case, not a stylistic split. Same shape as
`effect-atom-map.md`'s own E14a/E14b precedent. Capability map: `action-map.md` §12.1 (module table,
dependency order, Checkpoints F). Module specs, all written and adversarially audited against the
real code this session (two load-bearing bugs found and fixed before any code was written — see each
spec's own corrected design, and `spec-battle-status-apply.md` §1 / `spec-battle-live-stat-modifiers.md`
§1 for the specifics):

| id | Spec | Owns |
|---|---|---|
| A18a | [spec-action-container-binding.md](../docs/architecture/action/spec-action-container-binding.md) | The ephemeral binding seam |
| A18b | [spec-on-activate-trigger.md](../docs/architecture/action/spec-on-activate-trigger.md) | New `OnActivate` trigger (7→8) |
| A18c | [spec-battle-resource-shield-grants.md](../docs/architecture/action/spec-battle-resource-shield-grants.md) | `resource.delta` + `shield.grant` execute for real |
| A18d | [spec-battle-status-apply.md](../docs/architecture/action/spec-battle-status-apply.md) | `status.apply` executes for real |
| A18e | [spec-battle-live-stat-modifiers.md](../docs/architecture/action/spec-battle-live-stat-modifiers.md) | Sourced/revertible modifier ledger for `stat.modify` |

**Build order:** A18a → A18b → {A18c, A18d} → A18e → A19. Tasks: `action-todo.md` Phase 12 (T40–T54).
**Architecture decision that binds every module after A18a:** every cross-module dependency is a
settable property forwarded through `BattleEffectHost`, never a constructor parameter — because
`BattleRunState`'s constructor builds `Host` before most of its own other fields exist
(`BattleRunState.cs:115` vs. `Status` at line 117). This is the exact shape T14 already used for
`ShieldGate`; A18d (`Status`/`StatusRng`) and A18e (`Ledger`) both reuse it rather than reinventing a
constructor-injection approach that cannot compile against the real construction order.

## 4c. A18f, A19, A20 (2026-09-06, Phase 13) — the modules that make a second real action playable

A 2026-09-06 completeness audit (ahead of writing any of these three specs) found the actual gap
was narrower and more precise than "costs/cooldowns are unwired": `DeclareBasicAttack` already
selects and activates any real equipped action correctly; **only the resolve-time damage/cooldown
step ignores that selection**, hardcoding the basic attack's own envelope
(`TimelineDispatch.cs:211-213`). That single, exact defect is what makes A19 depend on a new module
(A18f) never previously named in the map — enforcing costs against an action that can never actually
run live would prove nothing. Full evidence: `action-map.md` §12.1a/§12.3a.

**A second audit pass — adversarial, against the specs themselves before any code was written —
found one load-bearing gap in the A18f design and tightened two things in A19.** See each spec's own
"⛔ Real, load-bearing gap" / corrected sections for the specifics; the short version: `ApplyBasicAttack`
is attack-shaped throughout (a hit/miss roll gates cooldown-arming), so A18f's own acceptance bar is
now scoped to attack-category actions only, with a **named, unbuilt follow-up**
(`action-resolution-by-category`) for a genuinely different-shaped Skill — see §5 below. A19's design
corrected two things found the same way: both `AlwaysAffordable.Instance` construction sites named
explicitly (missing the second would leave two of three shipped battle profiles unenforced), and its
own acceptance criterion for a multi-resource cost failure corrected from "spend then rollback" to
the real, verified "validate all, spend none until all clear" shape `CostLedger.TryPay` actually
implements.

**Build order: A18f → A19 → A20** (A20 needs A19's real cost/cooldown differences for its own
acceptance #4 to test anything beyond target selection, which A17 already proved). Tasks:
`action-todo.md` Phase 13 (T55–T57). Module specs, all written and adversarially audited against real
code before any implementation:

| id | Spec | Owns |
|---|---|---|
| A18f | [spec-action-dispatch-generalization.md](../docs/architecture/action/spec-action-dispatch-generalization.md) | The resolve-time envelope fix — one accessor, one call-site change, scoped to attack-category actions |
| A19 | [spec-action-costs-cooldowns-adoption.md](../docs/architecture/action/spec-action-costs-cooldowns-adoption.md) | Real `CostLedger` affordability + spend at commit; `perTick` costs interrupt on shortfall |
| A20 | [spec-synthetic-loadout-harness.md](../docs/architecture/action/spec-synthetic-loadout-harness.md) | The balance-comparison tool this whole reopening exists to eventually serve |

## 4d. A23, A21, A22 (2026-09-06, Phase 14) — a real player can hold a real, playable action

A 2026-09-06 audit of "is the action system actually playable" (ahead of writing any of these three
specs) found every downstream mechanism (A17–A20: dispatch, costs, cooldowns, loadout resolution,
battle) real and proven, and every upstream mechanism (eligibility, generation, import, grant) real,
tested, and **never called in production** — a repo-wide pattern, not action-specific
(`effect-pipeline-ideal.md`'s own "nothing produces an instance" finding). Full evidence:
`action-map.md` §14.

**Two of the three specs went through a real, load-bearing correction mid-write, each caught by
verifying against code rather than trusting the first plausible design** — see each spec's own
"⛔ Corrected" sections:

- `spec-action-instance-and-grant.md` (A21) first assumed content should roll per-player like
  equipment (`ActionSeeder` + a `WorldSeed`-derived seed). Re-reading `action-map.md` §10.5a's own
  *"a granted action has no instance and no rolls"* showed the roll happens **once, at import**,
  producing shared content every holder receives identically — a materially simpler design.
- The same spec then hooked `ILevelChangeHandler`/`LevelChangePipeline` as the grant trigger — until
  checking `LevelChangeEvent`'s own fields showed it carries no specimen identity at all (it is
  player/species-mastery-scoped). A specific summoned demon's own level lives on a completely
  different, callback-free path, `RpgStore.AwardUniqueActorXpUnlocked`, corrected to hook there.
- `spec-cost-scaling-holder-rung.md` (A23) is a genuinely new module, not anticipated when A19 was
  built: `spec-rung-semantics.md` §3.1 already decided that cost/cooldown scaling must read the
  **holder's** earn-count-derived rung, never the content's authored one — but this program's own
  immediately-prior session wired `CostLedger` to read the authored rung (`BattleRunState.cs:493`).
  Real, confirmed defect in already-shipped code, latent only because nothing has real holder
  progression yet — which is exactly what A21 creates, so A23 builds first.

**Build order: A23 → A21 → A22** (A22 can build in parallel with either once A18f exists — nothing
about it depends on real content existing yet, but nothing makes it *urgent* until A21 ships real
non-Attack rows either). Tasks: `action-todo.md` Phase 14 (T58–T60). Module specs, all written and
adversarially audited against real code before any implementation:

| id | Spec | Owns |
|---|---|---|
| A23 | [spec-cost-scaling-holder-rung.md](../docs/architecture/action/spec-cost-scaling-holder-rung.md) | `CostLedger`'s `rungOf` reads the holder's `EffectiveRung`, not the content's authored `Rung` — a wiring correction to already-shipped A19 code |
| A21 | [spec-action-instance-and-grant.md](../docs/architecture/action/spec-action-instance-and-grant.md) | Import the already-authored corpus once; grant a real, generated action to a real specimen on its own level gain |
| A22 | [spec-action-resolution-by-category.md](../docs/architecture/action/spec-action-resolution-by-category.md) | A non-Attack action skips the attack roll and arms its cooldown unconditionally, instead of every action resolving attack-shaped |

**No pre-work gates.** Per §0.1's own standing rule, nothing here blocks starting on an external
decision: the per-category envelope/cost template (A21 §2) ships with a stated default per
`action-corpus-ideal.md` §36's own "default now, re-tune later" precedent, exactly like every other
number in this plan; the corpus import is idempotent by construction, so there is no "only one chance
to get it right" moment to gate on.

## 4a. Reopening 2026-09-06 (same day) — A24, promoting `container-effect-resolver-not-wired`

A Stop-hook challenge to this program's own "done" claim correctly rejected "named in the deferred
table below" as closure for a gap this program both found and self-classified within the same
session — unlike A9/A10/seedsmith, which predate this program's own involvement. Investigated
further rather than re-asserted: full spec at
[spec-container-effect-resolver-production.md](../docs/architecture/action/spec-container-effect-resolver-production.md).

The deeper investigation found the gap has three layers, not one: (1) no production
`IContainerEffectResolver` exists anywhere — real, bounded, buildable now, closing the gap for any
held action whose container's atoms are ALL `Compilability.AtomPath.Compiled`; (2) `BattleEngine.Resolve`
has **no execution mechanism for the Runner path at all** — a repo-wide grep for
`RunnerEntry`/`AtomRunner` under `src/FusionRpg.Core/Battle/` returns zero files, a separate and larger
gap than this module's own scope; (3) both real seed atom families (`atom.fortitude`, `atom.vitality`)
are Runner-path only (`roll: onApply`, `min != max`), so **A24 alone does not make the 3 real imported
actions playable** — proven directly, not inferred, by a real test against the real committed corpus.

| id | Spec | Owns |
|---|---|---|
| A24 | [spec-container-effect-resolver-production.md](../docs/architecture/action/spec-container-effect-resolver-production.md) | A real, `RpgStore`-backed `IContainerEffectResolver`, wired into all three `WebMatchService.Resolve` call sites |

Tasks: `action-todo.md` §15 (T61.1-T61.4, Checkpoint L) — **CLOSED 2026-09-06.** Full regression run
for real across all three suites: `Core.Tests` 12638/12658 (20 pre-existing, already-documented
failures), `Data.Tests` 1109/1110 (1 pre-existing), `Server.Tests` 278/303 (25 failures, all proven
unrelated — 2 pre-existing, 23 traced by direct `git status` evidence to a concurrent siege-ai
session's own in-progress, untracked work). Zero goldens moved by this module.

## 4b. Reopening 2026-09-06/07 (same continuous session) — A25, `battle-runner-path-not-wired`, built and verified

Found while investigating A24 (§4a): `BattleEngine.Resolve` has zero execution mechanism for
`Compilability.AtomPath.Runner` atoms anywhere — confirmed by an empty repo-wide grep for
`RunnerEntry`/`AtomRunner` under `src/FusionRpg.Core/Battle/`. Both real seed atom families
(`atom.fortitude`, `atom.vitality`) fall in this bucket — proven directly
(`ActionCorpusRealContentQualityTests.TheThreeRealImportedActionsCompileToZeroEffectDefsBecauseTheirAtomsAreRunnerPathOnly`),
which is *why* A24 alone cannot make the 3 real imported actions playable.

**Investigated further rather than left as "an open question this investigation did not answer"
(A24's own earlier, weaker framing) — the real shape is now concretely known:**

- **`AtomRunner` (E15) is fully built and tested** (`spec-atom-runner.md`, "Status: BUILT
  2026-08-22", `AtomRunnerTests.cs` 26 tests + `CapPerMatchTests.cs` 8 tests) — it is NOT a stub. Its
  own spec names its callers as `SimEffectHost` and the injector's `EffectRuntime` — `BattleRunState`/
  `BattleEngine` are not among them, which is the actual gap: not "does the runner exist" but "is it
  wired into the battle-sim caller."
- **The exact two trigger-firing call sites already exist**, matching what a Runner integration needs
  to sit alongside: `BasicAttack.cs:149` (`state.Host.Bag.OnEvent(... Trigger = AtomTriggers.OnActivate
  ...)`) and `BasicAttack.cs:221` (`... Trigger = AtomTriggers.OnDamageDealt ...`) — the ONLY two
  `Bag.OnEvent` call sites in the whole engine.
- **The supporting pieces mostly already exist, verified by reading, not assumed**: `BattleEffectHost`
  already owns a real `EffectFunnel` (`BattleEffects.cs:43,61,66` — `AtomRunner`'s own dispatch target);
  `BattleRunState.FactsOf(string actorKey)` (`BattleRunState.cs:592`) already produces the `EntityFacts`
  a `RunnerEvent` needs; `NowTick` is genuinely millisecond-scaled, confirmed by
  `TimelineDispatch.cs`'s own comment ("150+50=200 ticks against a 1000ms round"), so `AtomRunner`'s
  `Func<long> nowMs` constructor parameter can read `() => state.NowTick` directly, no unit conversion
  needed; `TriggerIndex.Ordinal(string)`/`.Build(IEnumerable<RunnerBinding>)` already convert a named
  trigger to the ordinal `RunnerEvent.TriggerOrdinal` needs; `CompiledCatalog.Runtime` (produced by
  `AtomCompiler.Compile` today, currently discarded by A24's own factory) already carries the
  `RunnerEntry` list a `TriggerIndex` builds from.
- **A fourth, more specific finding, found the same pass**: `TriggerIndex.Build` throws loudly for any
  `RunnerEntry` with no authored trigger ("the compiler and the classifier disagree"). `atom.fortitude`/
  `atom.vitality` author no `when.trigger` at all (confirmed against the real seed JSON) — so even
  after A25 ships, these SPECIFIC atoms still could not activate through it; they would need a real
  trigger authored (e.g. `OnDamageDealt`, matching how existing runner-eligible content like
  `fx.poison_on_hit` is shaped) before Runner-path execution could apply to them. This is a content-
  authoring gap on top of the wiring gap, not caused by either A24 or a hypothetical A25 — named here
  so it is not silently rediscovered later as if it were new.

**Built, tested, and verified the same continuous session** (a Stop hook correctly rejected treating
"precisely scoped" as sufficient closure — the same self-authorized-scope-reduction challenge as A24's
own, applied one layer deeper): `RunnerBinding` construction (`ActionContainerEffectResolverFactory.
Build`, extended to a 4-tuple: `Resolver, Defs, RunnerBindings, ContainersWithRunnerCoverage`),
`BattleEffectHost.UseRunner` (mirroring `SimEffectHost.UseRunner` exactly, with one real, caught
correction — `nowMs` must be the caller's own `state.NowTick`, never `Host.Clock`, which is set once
and never advanced, confirmed by reading, not assumed), `BattleRunState`/`BattleEngine.Resolve` gained
two new optional trailing params (`runnerBindings`, `containersWithRunnerCoverage`), and two new
`Runner?.OnEvent(...)` calls in `BasicAttack.cs:149,221` (before each existing `Bag.OnEvent`, per
`spec-atom-runner.md`'s own documented ordering).

**A real, additional defect found and fixed while building this, not merely predicted**:
`BattleRunState.BindContainers`'s own "resolved to nothing" throw is unconditional on ANY held action
with a non-empty container — it had no way to know a container might be entirely Runner-path (zero
Compiled-path grants by definition) yet still legitimately resolvable via the Runner. Caught by a real
end-to-end test throwing exactly this, not by inspection. Fixed by threading
`containersWithRunnerCoverage` (built from the SAME per-container loop as `runnerBindings`, never
string-parsed back out of a binding id) into `BindContainers`, exempting a container from the throw
when the Runner seam already covers it.

**The real, precise remaining boundary, confirmed empirically via an actual thrown exception, not
inferred**: `EffectBag.Grant` throws `unknown effect_id: <atomId>` when the Runner successfully
dispatches (proving A25's own mechanism works — only a real `Funnel.EnqueueModifier` call reaches
`Grant` with that exact atom id) but no matching `EffectDef` is registered. This is
`spec-atom-runner.md`'s own already-named, separate scope ("nothing emits a def for a runner atom...
until [E19] a host has to have the def in its catalog already") — not a defect in A25, and not
something A25 needed to solve to prove its own contribution. Asserted directly in
`BuildSquadEquippedActionsTests.A_well_formed_triggered_runner_path_action_reaches_the_real_AtomRunner_in_a_real_battle`.

**Full regression, run for real, every failure traced to a specific verified cause**: `Core.Tests`
12637/12658 (20 already-documented + 1 confirmed-flaky via isolation and whole-class re-runs),
`Data.Tests` 1115/1118 (1 pre-existing + 2 from a different concurrent "demon-lawn-deploy" session's
own untracked, in-progress test file — confirmed via `git status`, not assumed), `Server.Tests`
285/311 (25 already-traced + 1 from that same concurrent session's untracked work, now touching a
second test project). Full detail: `action-todo.md` §16, T62.5.

| id | Name | What it owns | Depends on | Status |
|---|---|---|---|---|
| **A25** | `battle-runner-path-integration` | Wires `AtomRunner` (E15, already built) into `BattleEngine`'s real dispatch path via `BasicAttack.cs`'s two existing `Bag.OnEvent` call sites, so a well-formed Runner-path atom (real trigger, per-hit roll) can activate in a real `WebMatchService` battle | A24 (built), E15 (built) | **Built, tested, verified** |

Tasks: `action-todo.md` §16 (T62.1-T62.5, Checkpoint M).

## 5. Deferred, and why

| Module | Waits on |
|---|---|
| **`A10` battle-board** | **BUILT, TESTED, VERIFIED 2026-09-07.** A fourth Stop-hook challenge pressed past "A9/A10/seedsmith are legitimate pre-existing deferrals" (the third hook's own accepted framing) to the CORE RULE's literal text: a deferral, however audit-authorized, is still `[ ]` unchecked, and "no permission exists to treat an audit-text-backed deferral as equivalent to resolved." Re-investigated rather than re-asserted: A10's own locked spec (`spec-battle-board.md`) was written 2026-08-27 and explicitly says "Deferred by the owner... not in wave 1" — a real, dated, owner-made decision, not a self-authorized one. But a direct code read found the underlying mechanism had since been substantially built for a DIFFERENT reason: base-defense's own `siege-positions`/`siege-board` modules built `BoardState`, `GridSpec`, `GridDistance` (Chebyshev, exactly matching this spec's own §3), `BoardPathfinder` (a full A*, exceeding this spec's "paths around or refuses" ask), `Placement.PlaceActors`, and `BattleRunState`'s own `board`/`PositionOf`/`CombatBoardSnapshot` threading — all fully generic, all already wired end-to-end for ONE real production caller (`DistrictAssaultResolver.cs`, siege battles), just never for a normal squad-vs-wave battle. Presented this finding to the owner directly (should wave-1's exclusion be lifted now that its stated conditions — "not in wave 1", "after we complete the action feature" — are arguably satisfied); owner chose to build both A9 and A10 now. **Real remaining scope, once the mechanism was found already built**: a new seeded, bounded board-SIZE generator for a normal (non-district) encounter (`BoardGenerator.cs`, `data/tuning/battle-board.v1.json` — `minSide`/`maxSide`, a genuine balance surface per the spec's own "the bound is a balance decision" text) and a normal-battle placement policy (`NormalBattleBoard.cs`: squad on the left edge, wave on the right, widened to seat the larger roster — A10's own spec is deliberately silent on which edge each side starts, matching `DistrictAssaultResolver`'s own siege-specific approach/core zones being ITS wiring decision, not `Placement.PlaceActors`'s). Wired into all 3 real `WebMatchService.Resolve` call sites (`ResolveAndIngest` plus both replay paths) via `NormalBattleBoard.Build(squadKeys, waveKeys, seed)` — fully deterministic from `(squad, wave, seed)`, so a replay reconstructs an identical board with no board state persisted. **Proven, not merely built**: `BoardGeneratorTests.cs` (10 tests — same-seed-same-board, bounded-interval-over-2000-seeds, minSide-floor-widens); `NormalBattleBoardTests.cs` (5 tests — opposite edges, determinism, widens for an oversized roster, never double-occupies a cell); `NormalBattleBoardWiringTests.cs` (3 tests, through the real `BattleEngine.PositionAndSnapshotForTest` seam `SiegePositionsTests.cs` already established) — 18 new tests, all green, zero duplicated code (`BoardState`/`GridDistance`/`BoardPathfinder`/`Placement` all reused verbatim from base-defense). |
| **`A9` movement-actions** | **BUILT, TESTED, VERIFIED 2026-09-07 — same authorization as A10, which it depends on entirely.** Its own locked spec (`spec-movement-actions.md`) fully specifies the action's SHAPE (ordinary `rpg_action`, `slot_consuming: false`, priced by `time_cost_ticks`, resolved by the existing A22 category-dispatch branch) but is silent on how an autonomous battle chooses `AnchorSource.ChosenCell`'s actual destination — confirmed a real, undocumented gap by an exhaustive grep (zero resolvers anywhere reference `ActionAnchorSource.ChosenCell`). Asked the owner directly rather than inventing gameplay AI unilaterally; owner chose "move toward the nearest living enemy," matching the existing basic-attack AI's own established nearest-enemy convention (`StubIntentSource`/`SourceOrder`) rather than a new philosophy. **Built**: `MoveAction.cs` (`NearestLegalStepToward`/`MoveToward` — a greedy, one-cell-at-a-time walk, never a full pathfind onto the target's own occupied cell, which `BoardPathfinder` would refuse outright by design) plus `BattleRunState.TryMoveTowardNearestEnemy` (finds the nearest ACTIVE opposing actor with a real board position, ignoring same-side and dead/retreated actors). Wired into the ALREADY-EXISTING, ALREADY-TESTED A22 category-dispatch branch in `BasicAttack.cs`'s `ApplyBasicAttack` (`category != ActionCategory.Attack`) — the one new line reads the actor's own `move.range` derived stat and calls the mover only when it resolves > 0, which is byte-identical for every actor shipped today (nothing grants `move.range` yet, confirmed via the reader census below). **Proven**: `MoveActionTests.cs` (8 tests — direct/multi-step/adjacent-stop/commit-vs-resolve-race-falls-back-to-next-best-step/boxed-in/zero-budget/off-board, all against the pure algorithm); `NearestEnemyMovementTests.cs` (5 tests, through a new `BattleEngine.TryMoveTowardNearestEnemyForTest` seam — nearer-of-two-enemies, ignores-same-side, ignores-dead, no-board, no-living-enemy); `MovementActionDispatchTests.cs` (3 tests, through the REAL PUBLIC `BattleEngine.Resolve` with a real `CompiledAction`/`ChannelMods` fixture, the same `BattleGoldenTests.CloseSetup()`/`EquipSquadZero` infrastructure `ActionDispatchGeneralizationTests.cs` already established for this exact dispatch branch — a real equipped movement action moves the actor on a real board it was given; zero `move.range` stays inert; no board never throws). The pre-existing `ActionDispatchGeneralizationTests.T60_1_every_non_attack_category_skips_the_hit_roll[ActionCategory.Movement]` case (built before A9, proving Movement already skipped the hit roll) stayed green throughout, unmodified — direct proof the new code is additive, not a rewrite of shared dispatch logic. **A real, self-caused ripple found and fixed, not swept under the rug**: `move.range` gaining a reader is exactly the event three separate "inertness" canary tests (`ReaderCensusTests.cs`, `AptitudeMatrixTests.cs`, `MovementPayloadTests.cs` — the LAST one is `action-corpus` program's own A-M1 module, explicitly designed by ITS OWN spec to "fail the day someone wires a reader... forcing this spec to be updated rather than a stale claim quietly rotting") were built to catch. All three, plus `data/tuning/aptitudes.v7.json`'s own `_meta.measurable` prose (republished as v8 via `tools/tuning/publish.py`, never hand-edited) and `data/seed/derived-stats/catalog.json`'s `move.range` citation, updated to state the new, true fact — none of the underlying "5 vs 6 reader-less families" MATH was ever in question, only which side of the line `move.range` sits on. `DominanceGuard.cs`'s own SEPARATE "reserved" list (the offline closed-form balance predictor's own exclusions, nothing to do with the real battle engine) was checked and correctly left untouched. Full regression: `Core.Tests` 12780/12787 (7 pre-existing/concurrent-session failures, every one individually traced — golden drift, a live concurrent actor-sheet session's own doc edits confirmed by exact filename match against `git status`, and an external-subprocess `BattleStatComposer.Configure` issue unrelated to this fix's own `UseEquipment`/dispatch code), `Data.Tests` 1119/1120 (1 already-documented pre-existing gap), `Server.Tests` 289/314 (25 failures, all matching the SAME already-traced pre-existing `vocabulary.json`-content-defect/concurrent-class-system-session cluster this program has traced multiple times this session — `BuildSquadEquippedActionsTests` itself 7/7 green throughout). One real mid-session hazard, caught and corrected rather than left standing: a DIFFERENT concurrent session ("session 5", base-defense siege-ai/siege-construction) saved its own edit to this SAME file (`BasicAttack.cs`) mid-window and silently reverted this fix's own code in the process — caught by a direct grep re-check immediately after, not assumed still present, and re-applied. |
| `A8`'s reaction lane | **CLOSED 2026-08-31 by its own evidence, not new work** — B6 shipped 2026-08-28 and its own entry records guard ended up not needing a reaction lane at all (`battle-timeline-todo.md` B6, `action-todo.md`'s own Deferred section). The *stance* half shipped in Phase 7. |
| seedsmith | **after this program**, as a dev tool |
| `action-resolution-by-category` | **Scheduled as A22, Phase 14 (§4d).** No longer an unscheduled deferral. |
| rung-table coverage for rung 0 | **CLOSED 2026-09-07 by tracing the real pipeline — nothing to build.** Re-investigated under the same "is this reachable through real production, not just a test fixture" standard A24/A25 both used: `ActionCompiler.Compile` — the ONLY path `RpgStore.BuildActionCatalog` uses, which is every real `WebMatchService` call site's own action-catalog source — already calls `StructureBudgetGuard.Check`, which itself does `if (!rungTable.TryGet(row.Rung, out var rungRow)) return Fail(row.ActionId, ActionRejectionReason.UnknownRung, ...)` (`StructureBudgetGuard.cs:41-42`, verified by reading the code directly, not the comment that cites it — T30, shipped 2026-08-28, BEFORE this finding was even recorded on 2026-09-06). Any action authored with `Rung: 0` is rejected, loudly, by name, before it can ever reach a compiled `CompiledAction` or `CostLedger` at all. The original finding's own thrown exception is real but reachable only from a hand-built test fixture that skips `ActionCompiler.Compile` entirely (exactly what its own text already said: "every fixture... authors `Rung: 0` for simplicity") — a legitimate test shortcut, not a production gap. The invariant this finding asked for already existed, one layer earlier than the original trace checked. |
| `cooldown-arming-double-call` | **FIXED 2026-09-07, with a complete 4-state RED/GREEN falsifier, not merely reasoned about.** `ApplyBasicAttack` (`BasicAttack.cs`, Attack-category branch) now gates its own `Cooldowns.Start` call behind `envelope.StartsAt == CooldownStart.Resolve` (the enum's own declared default, `ActionEnvelope.cs:110`), matching `ActionRunner`'s own three-way split exactly — provably byte-identical for every envelope that does not override `StartsAt`, which is every one that exists today. Proof, reusing T56.4's own existing `CooldownGatedAttackSkill` fixture (already authored `StartsAt: Commit`) rather than writing a new one: (1) baseline GREEN, 7/7; (2) `ActionRunner.cs:236`'s own `StartsAt==Commit` arm disabled (`if (false && ...)`), fix in place → RED, the actor attacks 13 times unblocked — proves the fix makes `ActionRunner`'s own gate the SOLE, necessary mechanism; (3) SAME line disabled, fix ALSO reverted to unconditional → GREEN again, a false pass — proves the ORIGINAL bug genuinely masked a broken `ActionRunner.cs:236`, exactly as this finding claimed; (4) both restored → GREEN, 16/16 across both cooldown test files. Full `Core.Tests`/`Server.Tests` regression run to confirm zero golden movement (`Data.Tests` untouched — this fix has no surface there). |
| **`container-effect-resolver-not-wired`** | **Promoted to A24, §4a above, 2026-09-06 — BUILT, TESTED, VERIFIED.** T61.1-T61.4 all closed with full regression evidence (`action-todo.md` §15, Checkpoint L). |
| **`battle-runner-path-not-wired`** | **Promoted to A25, §4b above, 2026-09-07 — BUILT, TESTED, VERIFIED.** `AtomRunner` (E15) now wired into `BasicAttack.cs`'s two trigger sites via `BattleEffectHost.UseRunner`; full regression clean. Not action-specific in principle, but only the action program's own callers (`WebMatchService`) are wired — a sibling program wanting Runner-path activation on ITS OWN battle-resolve calls would need to pass `runnerBindings`/`containersWithRunnerCoverage` too. |
| **`equip-atom-source-not-wired`** | **FIXED 2026-09-07; production path corrected 2026-09-08.** Server boot uses `EquippedBoundAtoms.SourceFromStore` → `FromEquippedResolver` (`equip:{role}:{itemRef}`). Legacy `FromResolver` flatten (`equip:unknown:{atomId}`) remains for tests/tools only. Content gap: catalog equip atoms are mostly `stat.modify`, not `stat.derived`. |
| `action-grant-owner-kind-durability` | **FIXED 2026-09-07 — a genuinely live risk, not theoretical.** Verified before fixing, not assumed: `ClearSessionScopedBindings()` DOES have a real production caller (`Program.cs:654`, the server's own boot sweep) — a specimen's hard-earned action grant really could be silently wiped on a server restart. Scope was smaller than the original "every write path" framing feared: mapped precisely to exactly 2 real production sites (`WebMatchService.EquippedActionIdsFor`'s grant read; `RpgStore.UniqueActors.cs`'s `TryRollActionUnlocks` grant write, both T59.7/this program's own code), both moved from `OwnerKind.Entity` to `OwnerKind.UniqueActor` together. **A real, deliberate split found while fixing**: `EquippedActionIdsFor` shares one `OwnerScope` between the grant read AND the loadout read/auto-equip — migrating both together would have also moved loadout preferences, a lower-stakes, gracefully-degrading concern (falls back to auto-equip, never to nothing) unlike a permanently-lost grant. Split into two scopes: `grantScope` (now `UniqueActor`) and `loadoutScope` (stays `Entity`, unchanged, matching `LoadoutStoreTests.cs`'s own existing convention). All affected tests updated (`BuildSquadEquippedActionsTests.cs`, `ActionUnlockGrantWiringTests.cs`) and green, including the loadout-preference test proving the split works correctly together. Full regression, every anomaly traced to a specific verified cause: `Data.Tests` 1117/1118 (1 pre-existing), `Server.Tests` 285/311 (25 already-traced + 1 concurrent-session). `Core.Tests`' first attempt reported "Test host process crashed" after 30+ minutes (normal: ~20s) under 12+ concurrent dotnet/testhost processes — the 3 tests that failed before the crash all passed cleanly in isolation immediately after, proving transient contention, not a bug. A clean re-run (22s) landed 12649/12681: 20 match the established baseline, and the other 12 all reproduce in isolation with causes confirmed via `git status`, not assumed — 11 (`UniqueCorpusTests`/`UniqueContainerBuildTests`/`ItemCardTests`) fail with `Expected: 144, Actual: 154` while 3 real files under `data/seed/items/uniques/` show as actively modified (a different, concurrent item-content session growing the unique corpus live), and 1 (`SiegeAiIntentSourceTests`) is the siege-ai session's own in-progress test file. Zero of the 32 anomalies across any suite trace to this fix or any other change this session. |
