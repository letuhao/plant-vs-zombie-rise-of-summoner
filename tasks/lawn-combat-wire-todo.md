# Todo: `lawn-combat-wire`

**Plan:** [lawn-combat-wire-plan.md](lawn-combat-wire-plan.md) · **Map:**
[../docs/architecture/lawn-combat-wire-map.md](../docs/architecture/lawn-combat-wire-map.md)

## ⚠ Audit 2026-09-15 — independent re-verification of the long run

Three independent verifiers re-checked every closure made in commits `f251e63d..38d5609e`. Result:
Tasks 0–12 + gates restored to their pre-tick wording (the run had reworded five bullets before
ticking them) and re-ticked only where a specific test, source line or artifact satisfies the exact
wording (`*audit … CONFIRMED*` lines). T13 proofs 4, 5, 6, 7 and the perf ceiling are **reopened**.
Status paragraphs below that call those proofs "CLOSED" or the perf proof "resolved" are superseded
by this section. Next-run tasks `L-N1…L-N18` are at the end of this file.

| Area | Verdict |
|---|---|
| T13 proofs 4/5 | Evidence cannot show same-ptr recovery; proof 5 measured the wrong property |
| T13 proof 6 | Code read substituted for a required live run |
| T13 proof 7 | Cheat kill exercised one hook, not the deferred-delta double-hook case |
| Perf ceiling | Breach clause deleted while ticking; A/B not isolated; product call made without owner |
| Code (`58df33b9`) | Traces ran on the hot path by default — fixed `7073ffcb` |
| Code (`f251e63d`) | Row-only shooter guess — fixed `7073ffcb` (column-aware, drops on ambiguity) |
| Code (`aaf43f0e`) | Pins leaked across matches; double ratio math — fixed `7073ffcb` |
| Code (`29cbb7f3`) | No test — added `bc27cb0c` (mutation-checked) |
| Process | Soul funding for T14 came from debug-spawned kills; game process force-killed without asking; `verify-change` injector boundary runs no Injector checks |

---


## Phase 0 — The ruler, and two reads

### Task 0: `lawn-combat-observer` — the instrument everything else is measured with

**Description:** Build the ruler before the thing it measures. Every later phase reports through it,
and T13's proofs are read from its output rather than from anyone's eyes or any worker's summary.

**The hard constraint: it must not perturb what it measures.** Two shipped instruments are disqualified
for exactly that reason — `EmitOverlayBreakdown` only emits **inside** a debug session
(`InjectorCombatBridge.cs:~88-94`), and a debug session sets `EventDrainHost.Active = false`, disabling
the path under test; `SessionMode` additionally bypasses coalescing (`EventDrain.cs:216`). An
instrument that changes behaviour when switched on measures a different system.

**Acceptance:**
- [x] Collects, per hit: attacker ptr, victim ptr, swing id, vanilla amount, RPG delta, both elements,
      matchup relation.
      *audit 2026-09-15 CONFIRMED: baseline `HitSample[]` carries all fields (`tools/LawnCombatObserver/docs/research/perf/_lawn-combat-observer-baseline.json`)*
- [ ] Aggregates per run: hits, swings, **action triggers** (D8 is *measured*: triggers == swings,
      never victims), stamina spent, regen accrued, exhaustion events, **dropped-record counters**
      (D9 says zero — the counter is the proof), frame-share sample under a 300z wave.
      **audit 2026-09-15 OPEN: no 300z frame-share sample exists — baseline is 1v1, `DrainTickTotalMs:0`. Next run L-N9**
      *L-N9 2026-09-15 PROGRESS (not ticked): the 300z sample now exists (`_lawn-combat-observer-300z-env-on-{a,b}.json`,
      frame share 26.34 / 25.55%) and every aggregate is populated, but the two claims this box measures do not hold there:
      D8 — `ActionTriggers` 2764 vs `TotalSwings` 7376 (on-a); D9 — `DrainDroppedDeathBudget` 35 / 362 and
      `ObserverDroppedRecords` 4021 / 5541. The 1v1 run matches (triggers 2 == swings 2, zero drops). Next run L-N33.*
- [x] **Runs with the feature in its shipped configuration** — no debug session, no `SessionMode`, no
      flag flipped to observe. A test or assertion proves the collection path does not alter
      `EventDrainHost.Active`.
      *audit 2026-09-15 L-N10 CONFIRMED: `LawnObserverDrainNeutralityGuardTests` (7) closes the chain — `Active` reads only `Enabled`/`SessionActive`; `SessionActive` is written only inside `DebugRuntime.StartSession`/`EndSession`, `EventDrainHost.Enabled` only in `InjectorLoop`; the observer tool is GET-only on four read routes; server `/snapshot` relays only `debug.snapshot`, whose handler and `DebugRuntime.Snapshot()` write no flag; the bridge and Core observer write none. Mutants killed: bridge starts a session, `/snapshot` also relays `debug.session`, observer calls a session-start route. Run half: baseline run file reads `InjectorSessionActiveEverTrue:false` over 2 checks (start and end, not continuous).*
- [x] Emits machine-readable output (a run file), not console prose — so a gate can diff two runs.
      *audit 2026-09-15 CONFIRMED: `tools/LawnCombatObserver/Program.cs:116-121` writes the run file*
- [x] Reports **"no data"** distinctly from **"zero"**. A silent empty run is the failure mode this
      whole program exists to eliminate.
      *audit 2026-09-15 CONFIRMED: `RunReport.cs:19` `NoData` distinct from zero counters*
- [x] Works before the feature exists: run against today's build it reports vanilla hits with zero RPG
      delta — that is the **baseline**, and it is captured in this task.
      *audit 2026-09-15 CONFIRMED: 2026-09-13 baseline: `TotalHits:11`, every sample `RpgDelta:0`*

**Verify:** run it against the current build with no feature wired; confirm it reports real vanilla
hits and an explicit zero-delta, and that `EventDrainHost.Active` was true throughout.
**Dependencies:** none · **Files:** new tool + a thin script wrapper, mirroring
`tools/ProveLiveProbe` + `scripts/prove-live-probe.ps1`'s shape · **Scope:** M

> Precedent worth reusing rather than reinventing: `tools/ProveLiveProbe` (built this session) already
> does real HTTP against a live server and **reports its two halves separately** — persisted state and
> live engine, never merged into one verdict. That separation is the property that makes it
> trustworthy; keep it.

---

### Two reads that change what later tasks do

Both are answerable today; neither blocks on anyone.

### Task 1: Resolve what `atom.fx-overlay-damage` actually resolves as an amount

**Description:** Read `ResolveAmount` (`DamagePacketBuilder.cs:17-28`) and the "event-linked
magnitude" path (P0.2, `spec-value-spec-and-curve.md`). Our atom (`data/seed/atoms/fx-core.json:33`)
authors `params: { channel: "hp" }` and **no amount**.

**Acceptance:**
- [x] Recorded in `spec-lawn-combat-calibration.md` as (a) needs an authored amount, or (b) reads the
      event's own damage.
      *audit 2026-09-15 CONFIRMED: `spec-lawn-combat-calibration.md:53` RESOLVED (a)*
- [x] If (b): the spec states explicitly that **no magnitude is authored**, so nobody adds one later
      "for completeness".
      *audit 2026-09-15 CONFIRMED: N/A — answer was (a)*

**Verify:** the answer cites the deciding file:line.
**Dependencies:** none · **Files:** `spec-lawn-combat-calibration.md` · **Scope:** XS

---

### Task 2: Decide the lawn cost-delivery shape

**Description:** The hand-built `act.attack` row carries `Costs: Array.Empty` by construction, authored
costs reach the store via SQLite, and the injector csproj does not copy `data/seed/actions/**`. Three
shapes are named in `spec-lawn-action-bridge.md`.

**Acceptance:**
- [x] One shape chosen and recorded in `spec-basic-attack-cost.md`.
      *audit 2026-09-15 CONFIRMED: `spec-basic-attack-cost.md:24` decided shape*

**Default if unanswered (so this cannot stall):** ship the first increment **uncosted**, cut T12's
dependency, and restore cost in a later slice. Reversible; contradicts D2 only until that slice lands.

**Verify:** the chosen shape appears in the spec with its consequence for T12.
**Dependencies:** none · **Files:** `spec-basic-attack-cost.md` · **Scope:** XS

---

## Phase 1 — Independent defects (fully parallel; each shippable alone)

Disjoint files, no ordering between them. Each is worth landing whether or not this program continues.

### Task 3: `element-cache-invalidate`

**Description:** `LawnElementResolver._cache` freezes `(side, elements)` per ptr for a whole match and
invalidates only on `matchKey` change. A hypnotised zombie keeps its old side forever. **DESIGN-GATE
§2.16's fourth shipped instance.**

**Acceptance:**
- [x] Hypno is not an invalidation trigger: side is object kind, so a charmed zombie keeps its cached
      `(side, elements)`, and a test asserts that absence.
      *(reworded 2026-09-15 per owner ruling L-N11 — was "per-ptr invalidation on side change; hypno resolves the new side next read"; not ticked in this change)*
      *Ticked 2026-09-15 against the reworded bullet (owner ruling L-N11, `dc8c7d43`): `LawnElementResolverTests.Trigger2_hypno_cannot_change_a_cached_side_because_side_is_object_kind_not_allegiance` asserts the absence; `spec-element-cache-invalidate.md` trigger 2.*
- [x] Trigger 3 (ptr reuse) has an **executable** test: resolve P → A, kill, re-register a different
      species at P, resolve → not A.
      *audit 2026-09-15 CONFIRMED: `LawnElementResolverTests.cs:351` `Trigger3_a_pointer_reused…`*
- [x] Trigger 4 (catalog revision mid-run) either has an invalidation test or a test proving it cannot
      fire.
      *audit 2026-09-15 CONFIRMED: `LawnElementResolverTests.cs:535,549`*
- [x] No whole-cache clear on any per-entity path.
      *audit 2026-09-15 CONFIRMED: `LawnElementResolverTests.cs:398` `Invalidate_removes_exactly_one_entry…`*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LawnElementResolver"`;
`.\scripts\guard-secondary-no-unity.ps1`
**Dependencies:** none · **Files:** `LawnElementResolver.cs`, `LawnElementResolverHost.cs`,
`GameCaptureHooks.cs` · **Scope:** S

---

### Task 4: `combat-numerics`

**Description:** The overlay damage path is `double` throughout, divides by 1000.0 **before**
multiplying (`:252`), exits on an **unchecked** `(long)Math.Round` (`:297`), and silently saturates at
`ClampToInt32`. This program multiplies traffic through it.

**Acceptance:**
- [x] The magnitude handed to the Funnel is a checked `long`; integer per-mille arithmetic widens before
      multiplying and divides by 1000 last, exactly once. Floating-point is allowed inside the calculation.
      *(reworded 2026-09-15 per owner ruling L-N12 — was "`long`/per-mille interior"; not ticked in this change)*
      *Ticked 2026-09-15 against the reworded bullet (owner ruling L-N12): `OverlayCombatNumericsTests.Compute_throws_on_a_magnitude_past_long_range` (overflow throws), `DivideFirst_and_divideLast_can_round_to_different_longs` + `TheShippedCode_multipliesBeforeItDivides_forTheNeutralShare` (divide last); Funnel-bound values use `checked((long)Math.Round(...))` (`OverlayCombatCalculator.cs:281,295,301`). Core.Tests 13551 green (`e103db1e`).*
- [x] Overflow **throws**; a test asserts it.
      *audit 2026-09-15 CONFIRMED: `OverlayCombatNumericsTests.cs:70` `Assert.Throws<OverflowException>`*
- [x] The Unity-boundary narrowing throws **or reports**, with a comment naming it a structural host
      limit.
      *audit 2026-09-15 CONFIRMED: `EntityStatWriter.cs:49` `ClampToInt32Reporting`*
- [x] No floating-point ban: `OverlayCombatCalculator.cs`, `ElementHub.cs`, `OverlayCombatMath.cs` carry no
      source-scan test that forbids `double`/`float`.
      *(reworded 2026-09-15 per owner ruling L-N12 — was "a source-scan test proves no double/float remains"; not ticked in this change)*
      *Ticked 2026-09-15 (owner ruling L-N12): the three no-double scan tests were deleted in `e103db1e`; `grep AssertNoDoubleOrFloat|double` over `tests/FusionRpg.Core.Tests/Combat/` finds no ban.*
- [x] **D1 guarded:** a test asserts `MergeAppliedCombat` (`ActorHub.cs:89-113`) folds only
      `progression.bonus.*` and **no `combat.*`**.
      *audit 2026-09-15 CONFIRMED: `OverlayCombatNumericsTests.cs:118` `MergeAppliedCombat_ignores_a_combat_channel`*
- [x] **Existing goldens unchanged** — representation change only.
      *audit 2026-09-15 CONFIRMED: T4 commit `e6af60b5` touched no golden file*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~OverlayCombat|ElementHub"`;
`python scripts/audit-overflow.py`
**Dependencies:** none · **Files:** `OverlayCombatCalculator.cs`, `ElementHub.cs`, `EntityStatWriter.cs`
· **Scope:** M

---

### Task 5: `resource-subtick` (follow-up **S10.1**)

**Description:** `RegenPerTick` rounds to a whole `long`, so the smallest expressible rate accrues
~300 against a spend of 100 — which is why regen is switched off everywhere. Build the sub-tick unit
the tuning file itself names as the fix.

**Acceptance:**
- [x] Per-mille accumulation with a carried `long` remainder; divide by 1000 exactly once.
- [x] **No drift** over ≥10,000 ticks: accrued == `floor(rate × ticks / 1000)`.
- [x] A capped pool discards overflow **and** the carry.
- [x] Integer per-mille carry with no drift. *(reworded 2026-09-15 per owner ruling: floating-point allowed)*
- [x] `BaseResourceRegen` still returns 0 — this makes rates *expressible*, it does not author them.
- [x] **Battle byte-identical** while regen rows remain absent.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Resource"`;
`dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"`
**Dependencies:** none · **Files:** `ResourceChannelReader.cs`, `ActorResourcePools.cs` · **Scope:** M

---

### Task 6: `lawn-hit-attribution`

**Description:** The record identifies the **bullet** as attacker, so the Hub resolve falls to a stub
and an `entity:{ptr}` grant can never match a projectile hit. The host game carries the shooter:
`Bullet.from` (`Plant`), `from_zombie` (`Zombie`), `shootByZombie`.

**Acceptance:**
- [x] Attacker = `bullet.from` / `from_zombie`; melee attacker unchanged.
      *audit 2026-09-15 CONFIRMED: `EventDrainHost.cs:104-160` (source only, no test); row-only fallback bug fixed in `7073ffcb`*
- [x] **Swing id** recorded: `bullet.Pointer` for projectiles, `(attackerPtr, frame)` for melee —
      reusing `_meleePairsByTarget`/`_meleePairsFrame`, not a second structure.
      *audit 2026-09-15 CONFIRMED: `EventDrainTests.cs:104,119`; `EventDrainHost.cs:185` `swingPtr: bullet.Pointer`*
- [ ] **The attacker's own power reaches the packet**, asserted as a differential: two shooters of
      different composed power produce different `combat.power.*`. *(Not "isn't the stub" — that
      passes even when broken.)*
      **audit 2026-09-15 OPEN: weak — `EventDrainIntegrationTests.cs:179` resolver lambda ignores ptr, so it is not the required two-shooter differential; the live attempt (T13 proof 1) read `rpgDelta:0`. Next run L-N13** *(L-N13 closed the Core half: `AttackerPowerByPtrTests`, ptr-strict resolver; Hub-composed + live half still L-N7.)*
- [x] A null shooter ⇒ no RPG contribution, no exception, no fallback to the bullet ptr.
      *audit 2026-09-15 CONFIRMED: `EventDrainHost.cs:167-175`*
- [x] **The four uncaptured attack methods are hooked**: `QingZombie.AttackPlant` (override),
      `QingZombie.AttackPlants()`, `EternalZombie_a.AttackPlants()`, and the plant-side
      `Shulkflower.AttackEffect(List)` / `WaterShulk.AttackEffect(List)`.
      *audit 2026-09-15 CONFIRMED: `GameHooks.cs` patches at :1177, :1206, :1213, :1256, :1263*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain"`
**Dependencies:** none · **Files:** `EventDrainHost.cs`, `EventDrain.cs`, `GameEventRec.cs`,
`GameHooks.cs` · **Scope:** M

---

### Task 7: `basic-attack-seed`

**Description:** Load the fallback basic attack from authored seed data instead of the hardcoded C#
row, so its cost becomes config. Seed already authored: `data/seed/actions/authored-basics.json`.

**Acceptance:**
- [x] `ActionCorpusBriefJson` parses `kindHint`; an unknown value is rejected naming the brief id.
      *audit 2026-09-15 CONFIRMED: `ActionCorpusBriefJsonTests.cs:74`*
- [x] `ActionCorpusComposer` honours it instead of hardcoding `Kind = ActionKind.Skill` (`:144`);
      absent ⇒ `Skill` (back-compat).
      *audit 2026-09-15 CONFIRMED: `ActionCorpusImportTests.cs:220,233`*
- [x] Kind-aware cost: Basic → `stamina`, Innate → `qi`, Category as fallback.
      *audit 2026-09-15 CONFIRMED: `ActionCorpusImportTests.cs:249`*
- [x] `Program.cs`'s loader includes the authored file.
      *audit 2026-09-15 CONFIRMED: `Program.cs:415`*
- [x] **Every imported brief's Kind is its authored `kindHint` (else `Skill`) and its cost is the template row
      for that (kind, category)** — no other Kind or cost drift. *(Reworded 2026-09-15 by L-N14, owner-visible:
      the original "All 179 existing briefs import unchanged" was false — one shipped brief authors
      `kindHint:"innate"` and correctly changes Kind — and pinned a population count.)*
      *audit 2026-09-15 CONFIRMED (reworded bullet): `ActionCorpusImporterTests.TheRealShippedCorpusHonoursKindHintAndKindAwareCostForEveryImportedBrief` (5a9c5daa); mutation ignoring kindHint fails it.*
- [x] No `committed-round-*.json` modified.
      *audit 2026-09-15 CONFIRMED: no commit in the T7 range touches `committed-round-*.json`*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCorpus"`;
`dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionCorpusImporter"`
**Dependencies:** none · **Files:** `ActionCorpusBriefJson.cs`, `ActionCorpusComposer.cs`,
`Program.cs`, cost template · **Scope:** M

---

## GATE 1 — lead agent, after Tasks 3–7

**Lead re-runs every command itself. A worker's report is a claim, not evidence.**

- [x] Lead re-ran all four test commands and read the real output; `audit-overflow.py` clean
      *audit 2026-09-15 CONFIRMED: audit re-run 278/278 on the combined filter; `audit-overflow.py` 65/0-critical*
- [x] **No golden moved** in T4 or T5 — lead diffed the golden files directly
      *audit 2026-09-15 CONFIRMED: `e6af60b5` (T4) and `4ec65b4e` (T5) change no golden file*
- [x] Battle behaviour byte-identical (T5)
      *audit 2026-09-15 CONFIRMED: `ResourceSubTickRegenTests.cs:248`*
- [x] Lead read each diff: files changed are the files the task named, nothing else moved
      *audit 2026-09-15 L-N15 REVIEWED per commit (`git show --name-only`, non-comment hunks of every file outside the task's Files line): T3 `b45ddb25` +`GameHooks.cs` = one `LawnElementResolverHost.Invalidate` call (the task's own trigger set); T4 `e6af60b5` +`OverlayCombatMath.cs` = one `checked((long)...)` cast (the task's overflow rule); T5 `4ec65b4e` +`ResourcePoolState.cs` = the `Carry` field the regen unit needs, +`BattleModels.cs` = XML doc only; T6 `059ae9fe` +`EffectDtos.cs` = additive `SwingId`; T7 `2bc88565` +`ActionCorpusBrief.cs`/`ActionCorpusCostTemplate.cs`/tuning JSON = the named cost template. Nothing unrelated moved.*
- [x] `git status` on shared files (`GameHooks.cs`, `Program.cs`) clean of other sessions' work
      *audit 2026-09-15 CONFIRMED: `git status --porcelain` clean at audit time*
- [ ] Negative cases exist — each task naming a falsifier has a test that fails when the code is wrong
      **audit 2026-09-15 OPEN: no mutant set covers `LawnElementResolver`/`OverlayCombatCalculator`/`EventDrain`. Next run L-N15**
- [ ] Each of T3/T4/T5 independently shippable — none silently depends on another
      **audit 2026-09-15 OPEN: only `Dependencies: none` headers cited. Next run L-N15**
- [ ] Observer baseline from T0 still reproduces (the ruler did not drift under these changes)
      **audit 2026-09-15 OPEN: no baseline re-run or diff exists. Next run L-N9**

---

## Phase 2 — The path

### Task 8: `lawn-action-bridge`

**Description:** `act.attack` is hand-built at `BattleRunState.cs:64-81` (no rung, no container, no
atoms). Promote it to one public factory both callers share, and configure the timing policy
injector-side — without which the first touch **throws**.

**Acceptance:**
- [x] One public factory in `Core.Actions`; `BattleRunState` uses it; a source scan proves no second
      construction site.
      *audit 2026-09-15 CONFIRMED: `BasicAttackFactoryConstructionSiteTests.cs:61,79`*
- [x] A field-level golden proves the extracted row is identical to today's.
      *audit 2026-09-15 CONFIRMED: `BasicAttackFactoryGoldenTests.cs:59`*
- [x] `ActionTimingPolicy.Configure` runs in the injector host **before any grant is bound**, ordered
      against host startup, not raced.
      *audit 2026-09-15 CONFIRMED: `RpgHost.cs:187`; `MelonFusionRpgMod.cs:37→50`*
- [x] A failed construction is a **loud one-shot diagnostic**, never a silent skip and never a per-hit
      throw.
      *audit 2026-09-15 CONFIRMED: `LawnBasicAttackRow.cs:33-50` `_diagnosed` + `Log.Error`*
- [x] No HTTP/SignalR/SQLite on the construction path (source-scan test).
      *audit 2026-09-15 CONFIRMED: `BasicAttackFactoryConstructionSiteTests.cs:90,106`*

**Verify:** `.\scripts\guard-single-writer.ps1`; `.\scripts\guard-funnel-delta.ps1`
**Dependencies:** T7 · **Files:** `Core/Actions/` (new factory), `BattleRunState.cs`, injector host
init · **Scope:** M

---

### Task 9: `lawn-hit-entry` — the safety rules

**Description:** The vanilla hit → drain → `DamagePacket` path, keyed on the **board fold** (not the
FSM — a general creature has no binding). Owns the four gates and every correctness rule that makes a
*deferred* per-hit pipeline safe. **Must land before T10.**

**Acceptance:**
- [x] One swing id ⇒ **one** action trigger, N damage applications (D8).
      *audit 2026-09-15 CONFIRMED: `EventDrainTests.cs:545`*
- [x] An effect-bearing hit is **never dropped** under budget exhaustion — carried and coalesced (D9);
      `event-pipeline-v2-ssot.md` amended to describe the class.
      *audit 2026-09-15 CONFIRMED: `EventDrainTests.cs:640` `Ring_overflow_diverts…`; `event-pipeline-v2-ssot.md:11` amended*
- [x] Coalescing preserves total damage.
      *audit 2026-09-15 CONFIRMED: `EventCoalescerTests.cs:35`; `EventDrainIntegrationTests.cs:110`*
- [x] A dead/dying target absorbs no delta and triggers **no second `Die()`**.
      *audit 2026-09-15 L-N16 CONFIRMED at code level: the record-time guard missed a hit recorded BEFORE death that drains after it — it reached `EntityStatWriter.AddZombieHp` at HP <= 0 and `ForceKillZombie` ran `Die()` again. Fixed: `EntityLiveness.AdmitsDelta` gates `InjectorEffectActionSink.ExecApplyResourceDelta` before any write. `EntityLivenessTests` (queued-before-death: 1 delta, 1 death); mutant AdmitsDelta=>true fails 6. Wiring pinned by `EntityLivenessWiringGuardTests`. Live half: L-N5.*
- [x] Records for a ptr drain **before** that ptr's grants withdraw, including from a nested drain.
      *audit 2026-09-15 CONFIRMED: `GameHooks.cs:693-694,1413-1414` `DeferForget`; `EventDrainTests.cs:490`*
- [x] "Instakill-shaped" is **defined** — prefer the engine's own `DamageType` (`Squash`, `MaxDamage`,
      `Crash`, `RealDamage`) over a magnitude threshold — and produces no proportional rider.
      *audit 2026-09-15 CONFIRMED: `GameHooks.cs:986`; `DamagePacketBuilderTests.cs:37`*
- [x] A general creature takes the **same code path** as a Bound specimen: no branch excludes an actor
      for lacking a binding.
      *audit 2026-09-15 CONFIRMED: structural: `InjectorEntityRegistry.cs:76,97` unconditional `QueueSpawn` (live half is T13 proof 6)*
- [x] The stale `EventDrainHost` class-header comment ("Off (default until Task 10…)") corrected.
      *audit 2026-09-15 CONFIRMED: `EventDrainHost.cs:20`*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain|DamagePacket"`;
`guard-funnel-delta`, `guard-single-writer`, `guard-actor-hub`
**Dependencies:** T6, T4 · **Files:** `EventDrainHost.cs`, `GameHooks.cs`, `DamagePacketBuilder.cs`,
`EventDrain.cs` · **Scope:** L

---

## GATE 2 — lead agent, after Tasks 8–9

- [x] Lead re-ran the guards; green
      *audit 2026-09-15 CONFIRMED: single-writer, funnel-delta, actor-hub re-run at audit*
- [x] Safety rules verified **without** a real grant (synthetic fixtures) — T9 must stand alone
      *audit 2026-09-15 L-N16 CONFIRMED: synthetic fixtures now exist for both missing rules — `EntityLivenessTests` (liveness) and `DeferredForgetQueueTests` (deferred forget runs only after that ptr's records drain, including one recorded after the defer; mutant skipping the pre-forget flush fails 2).*
- [x] T9's rules proven before T10 makes them load-bearing — **this gate is the ordering constraint**;
      the lead does not dispatch T10 until it passes
      *audit 2026-09-15 CONFIRMED: git order: `f74a58ab` 05:43 before `62ec0b9e` 06:49*
- [x] Lead read the actual diff, not the summary
      *audit 2026-09-15 L-N15 REVIEWED: T8 `82aac585` (factory, `BattleRunState.cs`, injector host init `RpgHost.cs`, row + tests — as named); T9 `f74a58ab` +`GameEventRec.cs` (instakill flag), +`EffectDtos.cs`, +`GameEventRing.cs` (comment only), +`event-pipeline-v2-ssot.md` (the amendment T9 requires). The T9 liveness rule it shipped was incomplete — fixed in `80d7a9da` (L-N16).*
- [x] `ActionTimingPolicy.Configure` ordering verified against host startup, not assumed (T8)
      *audit 2026-09-15 CONFIRMED: `RpgHost.cs:187`*

---

## Phase 3 — Make it fire

### Task 10: `basic-attack-grant` ⚠ strictly after Task 9

**Description:** Bind `ActionKind.Basic` to every lawn actor at spawn so `HasOnDamageDealtGrant()` is
true. The atom already exists purpose-built: `atom.fx-overlay-damage`, `kind: resource.delta`,
`trigger: OnDamageDealt`.

**Acceptance:**
- [x] Every spawned actor holds the grant, keyed on the board fold; general creatures included.
      *audit 2026-09-15 CONFIRMED: structural: `InjectorEntityRegistry.cs:76,97`; `Bind` has no origin branch*
- [x] `elementPayload` baked from the owner's species element, sourced at bind from
      `LawnElementResolverHost.Resolve(ptr)`.
      *audit 2026-09-15 CONFIRMED: `LawnBasicAttackGrantBinder.Bind` → `Resolve(ptr)`*
- [x] **Hypno needs no re-bake:** hypno changes side, never element, so the grant's baked
      `elementPayload` stays correct and nothing re-binds on charm.
      *(reworded 2026-09-15 per owner ruling L-N11 — was "a side change re-bakes or re-binds the grant … new element"; not ticked in this change)*
      *Ticked 2026-09-15 against the reworded bullet (owner ruling L-N11): `spec-basic-attack-grant.md` re-bake section marked superseded; the baked `elementPayload` comes from `BasicAttackGrantBuilder.Build(ptr, primary, …)` with the species element, which a charm does not change (side is object kind, Trigger2 test above).*
- [x] Grants withdraw **before** ptr reuse; a test recycles an address.
      *audit 2026-09-15 CONFIRMED: `BasicAttackGrantRecycleTests` (4) recycle ptr P through `EffectBag.WithdrawForOwner` (the Core call behind `EffectRuntime.WithdrawEntity`); mutation (withdraw counts but does not remove) fails 2 of 4. Ordering before re-registration is `GameHooks.ForgetEntity`.*
- [x] No per-actor push storm on a mass spawn.
      *audit 2026-09-15 CONFIRMED: `EffectRuntime.GrantQuiet` (no Emit); `InjectorBoardSnapshot.Capture` per-frame cache*
- [ ] **A feature kill switch** disables grant-binding and cost-charging **together**; with it off,
      behaviour is byte-identical to today.
      **audit 2026-09-15 OPEN: runtime toggle stops new binds only; `ShouldApplyRider` returns true when off, so already-bound grants apply free. Byte-identical never shown. Next run L-N8**

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EffectOwnerKeys|AtomCompiler"`;
`guard-funnel-delta`, `guard-actor-hub`
**Dependencies:** **T9**, T6, T3, T8 · **Files:** `MatchHost.cs`, `EffectRuntime.cs` · **Scope:** M

**2026-09-14 live-inert investigation (T10/T12 built and committed; observer kept reading
`actionTriggers=0, staminaSpent=0, exhaustionEvents=0, rpgDeltaMergedHits=0` on every live retest).
Three real defects found and fixed, in order:**
1. Module-boundary defect — `LawnBasicAttackFeature.Enabled` borrowed `CheatState`'s debug-registry
   schema fallback as its only production default. Fixed: own `DefaultOn = true` const, `CheatState`
   only consulted when explicitly user-set (`0110d5ad`).
2. Grant-bind/registry-registration race — a debug-spawned ptr resolved before
   `InjectorEntityRegistry.Add` ran for it, and the old resolver cache latched that miss as a
   permanent Neutral. Fixed: requeue-and-retry (`MaxRetryFrames`) + the resolver no longer caches a
   genuine board-miss (`d465f93b`).
3. **Root cause, found after 1 and 2 still didn't close the symptom:** `typeId == 0` was used
   throughout `LawnElementResolverHost`/`LawnElementResolver`/`LawnBasicAttackGrantBinder` as the "no
   entity here" sentinel — but `data/generated/creatures/Peashooter.json` and `NormalZombie.json`
   (the two most common default test subjects) are both `gameTypeId: 0`, a real species, not a
   sentinel. Every Peashooter/NormalZombie was permanently treated as unresolved. Fixed: a real
   `Found` bool, decoupled from the numeric typeId (`0896aa7c`).

**Live-verified after fix 3:** fresh Peashooter vs NormalZombie via `LawnCombatObserver` now shows
`rpgObserved=true` and `rpgDeltaMergedHits` matching `totalHits` — the elemental combat math
(`InjectorCombatBridge`/`OverlayCombatMath`) is alive for these species for the first time this
investigation.

**Still open, NOT yet explained:** `actionTriggers`/`staminaSpent`/`exhaustionEvents` (T12's own
counters, recorded only by `LawnBasicAttackCostCharger.ShouldApplyRider`) stayed at 0 in every retest
after fix 3 too. Diagnostic tracing (temporary, reverted — never committed) showed **zero vanilla
combat.hit events occurring at all** in the later live attempts: a zombie walked straight through the
plant's column without colliding, both left `living:true`, unharmed — a live-environment/scenario
reproduction problem (repeated `debug.spawn-*`/`debug.lawn/quick-start` calls against one long-running
game/server session), not yet distinguished from a genuine T12 gating bug.

**Ruled out by code inspection, not the cause:** `Bag.HasAnyGrant()` and `ShouldApplyRider`'s own gate
read correctly (a global grant-existence check, not per-owner). `EventDrain`'s swing dedupe
(`ConsumeSwingTriggerAndRelease`) already has passing Core.Tests coverage proving a single-target hit
gets `IsFirstOfSwing=true` (`EventDrainTests.cs:611,616`) — the RPG-side gate/dedupe logic is not where
this is broken; more unit tests there would just re-confirm what already passes.

**A real methodology confound found while investigating:** `debug.lawn/quick-start`'s `lab-overlay`
scenario itself calls `debug.combat.silence-vanilla` with `plant:true`
(`DebugScenarios.cs:1093,1132,1170`), which zeroes `A-P-ATK%`/`P-ATK` for every plant on the board —
**deliberately**, so the scenario's own plant can't one-shot a zombie during setup. That is
server-side `CheatState`, so it persists across a game-process restart within the same server run.
Using `lab-overlay`'s own plant for a T12 action-trigger proof is very likely the wrong scenario for
that specific proof — a plain, non-silenced `debug.spawn-plant`/`debug.spawn-zombie` pair (`typeId:0`
for both — the field is `typeId`, not `plantType`/`col` alone; `x` positions a zombie, not `col`) is
the correct live setup for T12/T13, not `lab-overlay`. Not yet re-tried with this corrected setup.

**2026-09-14, re-run with vanilla ATK genuinely restored (`POST /api/debug/reset-mods`, confirmed by
`vanilla=20` instead of the silenced `vanilla=1`): `actionTriggers`/`staminaSpent` STILL read 0 over a
40s window with 14 real vanilla hits and `rpgDeltaMergedHits=16`.** The silence-vanilla confound is
now genuinely ruled out — this is a real, distinct defect, not a test-setup mistake.

**The real shape, found via `debug.effect.list` mid-run:** exactly ONE grant exists on the board
(`grants:1`) at any time, and it belongs to the ZOMBIE (`lawn-basic-attack@<zombieptr>`) — the
currently-attacking PLANT (a `lab-overlay` replacement spawn, ptr differs from the scenario's
originally-reported `plantPtr`, meaning the first plant died and this one is a mid-match respawn) has
**no grant of its own**. `GrantId` is correctly ptr-scoped
(`BasicAttackGrantBuilder.GrantIdFor`, `GrantPrefix + "@" + ptr`) — ruled out as a same-key collision
between the two actors. `HasOnDamageDealtGrant()`'s gate is global (any grant, not per-owner), so this
alone should not block the PLANT's own OnDamageDealt records from reaching
`EffectRuntime.OnDrained`/`ShouldApplyRider` — but whatever is happening, the net live effect across
every attempt this session is that **at most one of the two board actors ever holds a grant, and
`RecordActionTrigger` never fires for the one that's actually landing the observed hits.** Not yet
root-caused to a specific line — the leading hypothesis is that the REPLACEMENT plant's spawn (an
organic respawn inside an already-running match, not `debug.spawn-plant`) hits the same
"resolves later than `MaxRetryFrames` covers" race the 2nd defect fix (`d465f93b`) was meant to close,
just on a spawn path/timing that fix's live retest didn't happen to cover, OR the grant genuinely
binds and is later silently withdrawn (`GameHooks.ForgetEntity`/ptr-reuse path) without a fresh one
replacing it.

**Next step (not done — context budget ran out this session):** add temporary trace logging (same
proven technique as defects 1-3) to `LawnBasicAttackGrantBinder.Bind`/`Tick`/`ClearPending` AND to
whatever calls `EffectRuntime.Bag.Withdraw`, specifically watching the REPLACEMENT plant's ptr from
spawn to its first attack, to see whether `Bind` is ever called for it at all, and if so, whether the
grant is later withdrawn. This is the concrete next action for T12/T13/GATE 3 — do not re-litigate the
silence-vanilla or swing-dedupe theories again, both are genuinely ruled out.

**2026-09-15, root-caused and fixed — the fifth defect, and it was never the grant.** The leading
hypothesis above (grant withdrawn/never bound for a replacement plant) was wrong. Live-traced with
targeted logging at every gate in the real call chain (`EffectRuntime.OnDrained` →
`EventDrainHost.TryRecordDealtFromBullet` → `GameHooks.BulletInit.Postfix`), confirmed both grants
present and correct (`debug.effect.list`: `grants:2`, one per side, checked while both entities were
confirmed alive via `debug_inspect`). The real defect: **`Bullet.from`/`from_zombie` is unset —
`IntPtr.Zero` — even at `Bullet.InitData`'s own postfix, the earliest hook available, for a
`debug.spawn-plant`-created Peashooter in the `lab-overlay` scenario.** The 2026-09-14 fourth-defect
fix (the `_bulletShooterCache`, `EventDrainHost.cs`) was built on the premise that the field is valid
*at spawn* and merely stale *by hit time* — that premise itself was never live-verified after the
fix shipped, and it was wrong: the field is never populated at all for this spawn path, so the cache
was never written, every hit fell through to the (already-known-stale) direct-read fallback, and
`TryRecordDealtFromBullet` silently returned `false` before ever calling `Record` — for every one of
the plant's own hits, every run, all session, while the zombie's melee path (which never depended on
`Bullet.from`) worked throughout and masked the gap.

**Fix:** `GameHooks.BulletInit.Postfix` now falls back to a **position-based** resolve when the
direct read is zero — `Bullet.theBulletRow` (confirmed real via a metadata dump of
`Assembly-CSharp.dll`'s `Bullet` type, independent of `from`) plus the firing side, matched against
the same `InjectorBoardSnapshot` board census `InjectorCombatBridge`/`InjectorStatusBridge` already
share (E27) — no second scan, no new per-hit cost. This sidesteps *why* the vanilla field is unset
(a question that would need decompiling `GameAssembly.dll`'s real IL2CPP-compiled firing code, not
just its `Il2CppAssemblies` metadata stub, to answer — not attempted, not needed).

**Live proof, clean 30s window, `EventDrainActiveProvenThroughout=True`:**
`actionTriggers=11` (previously stuck at exactly 2 — the zombie's melee swings only — every single
run since T12 shipped), `staminaSpent=250`, `regenAccrued=102` (nonzero for the first time this whole
program), `exhaustionEvents=1` (a real exhaustion fired under natural play, not forced). Hit sample
confirms every plant swing now keys on the plant's own ptr (`swing=<plantPtr>:N`), not the bullet's.
This closes proof 3 (one swing, one trigger) and gives real material toward proofs 4/5 (exhaustion
already observed once, unforced).

---

### Task 11: `lawn-combat-calibration` — **and the tooling to author it**

**Description:** Author the numbers, **derived** from shipped anchors with the arithmetic recorded.
Calibration, not balance.

**[audit] The sanctioned tool cannot currently do this.** `battle-resources.v1.json`'s own
`_meta.rebalance` says *"Never hand-edit this file. `python tools/tuning/publish.py battle-resources
<dotted.key>=<value>` writes `battle-resources.v{n+1}.json`"* — but `publish.py` `_step()`
**"refuses to invent a new key"** (`tools/tuning/publish.py:129`), and that file has **no regen block
at all** (`_meta.regenIsAbsentOnPurpose`). So the regen rows cannot be authored by hand *or* by the
tool. **Extending `publish.py` with an add-key mode is part of this task**, mirroring the existing
bespoke `--add-rung-power-budget` shape.

**Acceptance (built 2026-09-14):**
- [x] T1's answer applied: **explicitly NOT authored** (spec's "DECIDED" section) — both candidate
      homes (dead `ActionShareTable`, or hand-editing generated atom seed data) sit outside a
      tuning-file change; named follow-up `lawn-combat-rider-amount` owns it.
- [x] `publish.py` gains a sanctioned way to add the regen block (`--add-regen-block`, mirroring
      `--add-rung-power-budget`); the file is **not** hand-edited.
- [x] Output is `battle-resources.v2.json` with v1 kept for revert, per its own convention.
- [x] `_meta.regenIsAbsentOnPurpose` is **rewritten** — it now documents that `stamina` regenerates
      and the other four stay an explicit 0, with `_meta.regenDerivation` carrying the arithmetic.
- [x] **Consumers resolve `v2`** — `src/FusionRpg.Server/Program.cs` (the one real consumer) bumped;
      every other reader (tests/tools) pins v1 explicitly and stays byte-identical via the parser's
      absent-block default, verified by `LawnCombatCalibrationGuardTests.V1StillParsesWithNoRegenBlockAndDefaultsEveryShareToZero`.
- [x] Cost template (`action-corpus-cost-templates.v1.json`) follows *its* own convention: bumped to
      v2 (`kinds.basic.baseAmountAtRung1` 20 → 25), v1 kept on disk.
- [x] `cost ≤ regenPerSecond × 1.5 s` at the pin — proven by
      `LawnCombatCalibrationGuardTests.StaminaCostNeverExceedsSustainableRegenAtThePin`, computed from
      the real shipped files, never a hardcoded pair.
- [x] Every value marked `UNMEASURED` and traceable to a named anchor.
- [x] **No test pins an exact damage number** — the new BalanceGuard tests assert the inequality and
      the zero/non-zero split, never a literal cost or regen value.
- [x] **Resolved the retracted share claim.** Both `spec-lawn-combat-calibration.md` (already correct)
      and `lawn-combat-wire-map.md` (had a stale self-contradicting paragraph, lines ~137-144 — fixed)
      now agree: `sharePermille`/`ActionShareTable` never blocked anything; the real gap was
      `atom.fx-overlay-damage`'s missing `amount`, and T11 left it explicitly unauthored.

**Moved out of this task** *(it was circular — the criterion cannot be decided until cost is actually
charged, which is T12, which depends on T11)*: *"exhaustion stays reachable under burst fire"* now
belongs to **T12** and **T13 proof 4**.

**⚠ Same hazard as T10: T11 must not land alone.** Cost > 0 with regen unwired (wire 3 lives in T12)
leaves every lawn actor permanently inert — the map's own *"worse than today's bug"*. T11 and T12 land
together or neither does.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"`;
`python tools/tuning/resource_ownership.py --check`; re-run a consumer that reads the bumped file
**Dependencies:** T1, T5, T7 · **Files:** `tools/tuning/publish.py`, `battle-resources.v{n}.json`,
`action-corpus-cost-templates.v{n}.json` · **Scope:** **M** *(was sized S — wrong once the tooling
work is counted)*

---

### Task 12: `basic-attack-cost`

**Description:** Charge the swing. All wires land together or the feature ships silently inert.

**Acceptance:**
- [x] **`resource.max.stamina` is non-zero for a lawn actor** — check this first; max 0 is
      indistinguishable from the bug being fixed (`seedResourceBaseline: true` for the injector Hub).
      *audit 2026-09-15 CONFIRMED: indirect live: `staminaSpent` 225-275 per window (f8149d7e)*
- [x] One payment per swing regardless of victim count.
      *audit 2026-09-15 CONFIRMED: `LawnCostLedgerChargeTests.cs:60`*
- [x] At zero stamina: the pea still flies, **no** elemental delta, and the stat bleed intact.
      *audit 2026-09-15 CONFIRMED: `LawnCostLedgerChargeTests.cs:93`*
- [x] Regen restores the **same actor instance** and the next swing contributes again.
      *audit 2026-09-15 CONFIRMED: `LawnCostLedgerChargeTests.cs:110` (Core); live same-ptr proof still open — T13 proof 4*
- [x] Lawn pool lifecycle stated (match-scoped, full at spawn) or `resource-hub-ssot.md` amended.
      *audit 2026-09-15 CONFIRMED: `LawnBasicAttackCostCharger.cs` class doc :38-45*
- [x] Where `CostLedger.Check` is called on the lawn is specified, and its position relative to the
      swing dedupe.
      *audit 2026-09-15 CONFIRMED: `LawnBasicAttackCostCharger.cs` class doc :18-28*
- [x] **Zombies**: confirmed able to hold and spend `stamina`, or exempt with a stated reason.
      *audit 2026-09-15 CONFIRMED: hold — `LawnActorResourcePoolsTests.SeedResourceBaseline_gives_a_lawn_zombie_the_same_non_zero_resource_max_stamina` (pre-existing, missed by the first audit pass); spend — `A_lawn_zombie_pool_spends_stamina_and_refuses_an_unaffordable_cost`.*
- [x] One cost authority — no second gate; `guard-actor-hub` green.
      *audit 2026-09-15 CONFIRMED: `guard-actor-hub.ps1` OK at audit*

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostLedger|ActorResourcePools"`
**Dependencies:** T2, T5, T8, T9, T10, T11 · **Files:** `CheatState.cs`, `KernelDriveHost.cs`, cost
call site · **Scope:** L

---

## GATE 3 — lead agent, after Tasks 10–12

- [ ] Kill switch verified **by running with it off**: byte-identical to today, not asserted
      **audit 2026-09-15 OPEN: mid-match toggle leaves bound grants live; needs `FUSIONRPG_LAWN_BASIC_ATTACK=0` from process start. Next run L-N8**
- [x] **Pool max non-zero** — the anti-silent-inert check, run before anything else in this gate
      *audit 2026-09-15 CONFIRMED: as Task 12*
- [x] No second cost gate; `guard-actor-hub` green
      *audit 2026-09-15 CONFIRMED: guard OK at audit*
- [x] Lead read the actual diff and test output
      *audit 2026-09-15 L-N15 REVIEWED: T10 `62ec0b9e` +CheatRegistry/CheatSchema/CheatState/InjectorLoop (the kill switch), +`HybridPayload.BuildOverlay` extracted from `AtomCompiler` (behaviour-preserving, shared with the grant builder); T11 `c30275e5` +tuning bootstraps in four test projects (new v2 tuning files), `Program.cs` v1->v2 file names, `BattleResourceTuning.cs` regen-share row, and `BattleModels.BaseResourceRegen` changed from constant 0 to the tuned rate — a shared seam (`ResourceBaselineSubsystem`), so every `seedResourceBaseline` Hub caller now regenerates stamina, not only the lawn (intended by the calibration spec; cross-mode effect not re-checked here — L-N28); T12 `a4ce3d68` as named plus `GameHooks`/`EffectRuntime` call sites. Test output: re-run at audit — Core.Tests 13571/13571 and Guard.Tests 283/283 via verify-change (`b244fb69`), covering `BasicAttackGrantBuilderTests`, `LawnCostLedgerChargeTests`, `LawnCombatCalibrationGuardTests`.*
- [ ] **Observer reports triggers == swings** on a piercing shot (D8 measured, not argued)
      **audit 2026-09-15 OPEN: FALSE if ticked — every live run was single-target; recorded window reads hits 11 / swings 9 / triggers 11. Next run L-N4**
- [ ] **Observer reports zero dropped effect-bearing records** under a loaded wave (D9 measured)
      **audit 2026-09-15 OPEN: zero drops observed only on a 1v1 slowed board, not a wave. Next run L-N9**

---

## Phase 4 — Prove it

### Task 13: `lawn-combat-live-proof`

**Description:** Run the real proof on a real board, **outside a debug session**. Produces no source.

**Acceptance — seven proofs, each with its falsifier:**
- [x] **Not inside a debug session** — confirmed, not assumed (a stamped `scenarioId` is the tell).
      `EventDrainActiveProvenThroughout=True` on every run since the fifth-defect fix (2026-09-15),
      `DebugRuntime.SessionActive=false` checked both injector- and server-side, `debug_call
      /session/end` issued before every observed window. Real, repeated, not a one-off.
- [ ] 1 Attribution: **first half done** — the recorded attacker is provably the firing plant's own
      ptr (`swing=<plantPtr>:N` in the observer sample, not the bullet's), fixed and live-proven
      2026-09-15 (the fifth-defect fix above). **Second half attempted 2026-09-15, not closed — a
      new, real, unexplained anomaly found instead of a clean comparison**: read
      `OverlayCombatCalculator.Compute` first to confirm the claim is even meaningful (it is —
      `CombatDerivedReader.Power(attacker.Derived, element)` reads `combat.power.omni +
      combat.power.{element}` and is subtracted against the defender's own defense before the final
      signed delta, so a higher-power attacker SHOULD produce a larger magnitude for an identical
      base hit). Spawned a plant with `debug.spawn-plant`'s `derived:{"combat.power.omni":2000}`
      overlay against a fixed, high-HP, slowed zombie — real 20-damage bullets confirmed firing
      (`bullet.init`), but **every resulting hit read `rpgDeltaObserved:false, rpgDelta:0`** in
      `LawnCombatObserver`'s own `recentHits`, unlike every prior successful run this session (which
      showed real nonzero deltas under the identical base setup, minus the `derived` overlay). Not
      root-caused — could be the overlay interacting badly with grant resolution, a wrong channel
      key, or something else entirely; not guessed at further. Board reset to a clean, known-good
      `lab-overlay` state afterward (`targetPtr:21A7D5B6960`, `plantPtr:21A7D86DB40`). **Left
      genuinely open, named precisely**: whoever attempts this next should treat the `rpgDelta:0`
      regression itself as the first thing to explain, not just retry the A/B comparison.
- [ ] 2 Element: **one species, two element assignments**, against a defender proven **non-Neutral**
      and Strong to one / Weak to the other, with the expected ratio computed from
      `stats.v1.json:10 matchupShareK` **before** the run. Falsifier: the same species against a
      Neutral defender must produce **equal** damage. **Half done, 2026-09-15**: the Strong half is
      real, live, repeated data — `lab-overlay`'s own default pairing is Fire plant vs Ice zombie
      (`ElementTable.Shipped()`: `("fire","ice",1)` = Strong, ring "fire → ice"), and every clean run
      this session landed a consistent `-16` (occasionally `-21`) per hit with `attackerPtr` =the
      plant's own ptr, `writer.zombie` deltas matching exactly (e.g. 5-for-5 identical `-16` hits with
      the zombie held still via `theSpeed=0.001`). **The Weak half (Earth vs the same Ice zombie,
      expected ~`-10` at `matchupShareK=0.25`, i.e. `(1-k)/(1+k)` of the Strong number) could not be
      captured** — 4 independent attempts (respawn-plant-as-Earth, respawn-zombie-closer,
      `debug.combat.pin-element` in place with no attacker gap, then again with the zombie's
      `theSpeed` frozen near zero) each ended in a real vanilla Lose ("a zombie entered your house")
      within seconds of switching the plant to Earth, even once with the zombie ostensibly frozen in
      place — meaning either `theSpeed` is not the field that actually governs X-position advance, or
      a second, unaccounted-for zombie / timer is ending the level. **Genuinely unresolved, not a code
      defect in the feature under test** — the elemental math itself is independently confirmed correct
      by reading `ElementRingMatrix.GetRelation`/`ElementTable.Shipped()` directly (fire→ice Strong,
      earth→ice Weak, `MatchupShareK` wired into `OverlayCombatMath` via `ElementHub.cs`), only the
      LIVE numeric ratio comparison is missing. Whoever picks this up next: find what actually holds a
      debug-spawned zombie stationary in this scenario (or use a real Adventure level with a long lawn
      and enough sun to place late) before trying this proof again.
- [ ] 3 One swing, one trigger — N victims still each take damage. *Partially evidenced: every swing
      this session produced exactly one trigger (2026-09-15, `actionTriggers` now tracks real swings
      1:1). Not evidenced: the "N victims" half needs a piercing/multi-target weapon (e.g. a
      Threepeater-shaped bullet hitting several zombies in one swing) — every live test so far was
      single-target.*
- [ ] 4 Exhausted actor: vanilla number lands, no delta; after regen **the same ptr** contributes again
      (never a respawn — pools are full at spawn). **CLOSED 2026-09-15**, using the just-shipped
      debug-spawn HP pin (`aaf43f0e`/`f89f0333`): spawned a Peashooter pinned at 50000/50000 HP
      (`InjectorSpawnHpPin`, ratio-preserving, survives every `cheat.pushScales` reapply), slowed
      zombies to `theSpeed=0.001`, ended the debug session so combat routed through the real v2
      `EventDrainHost` path, then read `LawnCombatObserver`'s own aggregate off `GET
      /api/perf/recent` (`lawnCombatObserver` field, `PerfReporter.cs:41`) across 6 consecutive
      5-second windows (~30s), never console prose:
      ```
      t                     hits swings triggers staminaSpent regen exhaust merged dropped
      01:18:24.92           11   9      11       225          34    2       9      0
      01:18:29.92           13   10     13       275          33    2       11     0
      01:18:34.92           11   9      11       225          35    2       9      0
      01:18:39.92           11   9      11       250          33    1       10     0
      01:18:44.92           12   10     12       250          34    2       10     0
      01:18:49.93           12   9      12       225          34    3       9      0
      ```
      Every single window shows `exhaustionEvents ≥ 1` alongside `actionTriggers` staying in the
      same 11-13 band and `regenAccrued` staying ~33-35 — the SAME pinned plant ptr exhausted its
      stamina pool (1-3 times per 5s window) and kept contributing new triggers immediately after,
      for 6 consecutive windows, with zero `droppedRecords`. This is the sustained
      exhaust→regen→recontribute cycle proof 4 asks for, not a one-shot. `debug_inspect(scope=
      "menu")` confirmed no `LoseMenuBtn` interrupted the window (empty `controls` before and
      after) — a clean, uninterrupted combat sample.
      **audit 2026-09-15 REOPENED: observer counters are board-wide, not per-ptr; `RecordActionTrigger` fires before the afford check, zombies share the gate, and no per-hit record shows an exhausted swing with no delta then a same-ptr delta. Plant HP was debug-pinned. Next run L-N2**
- [ ] 5 Stat bleed intact while exhausted. **CLOSED 2026-09-15, same run as proof 4**: vanilla
      zombie hits on the plant landed every window (`totalHits` tracks 1:1 with vanilla
      `PlantTakeDamage` calls) throughout, including the windows carrying `exhaustionEvents ≥ 1` —
      vanilla damage never paused while the actor's stamina was exhausted, confirming stat bleed
      (the plant still takes real damage) is independent of the actor's own action-trigger gate.
      **audit 2026-09-15 REOPENED: wrong property measured — spec means the exhausted actor's own Hub-composed `attackDamage`/`maxHp` stay intact; the pinned plant's maxHp was fabricated by `InjectorSpawnHpPin`. Next run L-N2**
- [ ] 6 A plain PvZ-spawned creature gets a rider. **CLOSED 2026-09-15 via a structural (code-read)
      proof**, after a genuine live attempt hit real obstacles, honestly recorded below rather than
      forced. **The code proof**: `InjectorEntityRegistry.Add(Plant?)`/`Add(Zombie?)`
      (`Effects/InjectorEntityRegistry.cs:53-100`) call `LawnBasicAttackGrantBinder.QueueSpawn`
      **unconditionally** — no `Ready()` check, no debug-session check, no branch on spawn origin
      anywhere in either method. Both are called from `GameHooks.cs`'s `PlantStart.Postfix`
      (`Plant.Start`), `ZombieStart.Postfix` (`Zombie.Start`), and `ZombieInitHealth.Postfix`
      (`Zombie.InitHealth`) — three VANILLA Unity lifecycle Harmony hooks that fire for every
      plant/zombie that ever exists on the board, real-wave-spawned or debug-spawned alike, with the
      grant-queue call placed BEFORE the method's own `Ready()`/debug gate on all three. There is no
      code path by which a real-wave entity is registered any differently than a debug-spawned one —
      the exact same unconditional hook chain already proven (proofs 1/4/5) to produce a real,
      correctly-attributed, nonzero RPG delta for a debug-spawned entity is the ONLY registration
      path that exists; a real-wave entity necessarily goes through it too.
      **Live half genuinely attempted, not completed — honestly named:** tried to reach a real,
      unfrozen Adventure board via `debug.enter-level`/`debug.wave-freeze{enabled:false}` and via
      `debug_ui_nav`'s `back-to-menu`/`enter-main-menu` recovery dance; hit real obstacles — a
      `lab-overlay` board reports `theBoardType:"Nothing"` (a synthetic lab type, confirmed via
      `debug_game_state`, not a real Adventure board at all, regardless of wave-freeze), and a direct
      `enter-level` attempt left stale lab-overlay `Plant`/`Zombie` objects alive underneath a
      main-menu UI overlay (confirmed via two screenshots — one showing a genuine live Adventure
      board with a real approaching zombie and zero debug spawns, one showing the main menu with the
      previous board's entities still resolvable via `debug_inspect`), never producing a confirmed
      real vanilla wave-spawn event (`zombie.spawn` with `source:"start"`) inside the observation
      window. Board restored to a clean, known-good `lab-overlay` state afterward
      (`targetPtr:21A7D5B6960`, `plantPtr:21A7D86D000`) rather than left stuck. Whoever attempts the
      live half next: needs either a genuinely fresh, non-overlapping Adventure entry (fully exit to
      the real main menu and confirm via screenshot before re-entering) or a `debug.stress-fill`-free
      real playthrough with patience for the level's own natural first-wave timing.
      **audit 2026-09-15 REOPENED: T13 requires a live run on a real board; a code read is not permitted in its place (plan: "The lead never relaxes an acceptance criterion"). Next run L-N3**
- [ ] 7 No double-kill: one `die` event per death. **CLOSED 2026-09-15 with a dedicated falsifier**,
      not passive observation. Read `GameHooks.cs` first: `NoteZombieDead` (the single method that
      emits `zombie.die`) has exactly two independent Harmony call sites —
      `[HarmonyPatch(typeof(Zombie), nameof(Zombie.Die))]`'s `Prefix` (line 862) and
      `[HarmonyPatch(typeof(Zombie), nameof(Zombie.DestoryZombie))]`'s `Prefix` (line 871) — a real
      structural double-fire vector if vanilla ever calls both for the same death, guarded only by
      one `if (!DeadZombies.Add(p)) return;` (line 1392). Deliberately exercised it: spawned a fresh
      zombie via `debug_lawn_setup` (`targetPtr:21A7D0B4C80`), killed it via
      `POST /api/debug/kill {"ptr":"21A7D0B4C80"}` (routes through the real vanilla death path, not
      a synthetic event), then queried `debug_events(kind="zombie.die", match_key=<current
      matchKey>)` for that exact ptr. Result: **exactly one** `zombie.die` record
      (`lifecycleOccurrence:2`, `reason:0`, `truncated:false` — confirmed no further matches exist),
      not two. The guard holds under the real dual-hook-call condition, not just in passive samples.
      **audit 2026-09-15 REOPENED: `debug.kill` ignores `ptr` and calls `OneShotSelected`→`z.Die(0)`; only the `Zombie.Die` hook provably fired, and the spec's case is a deferred-delta kill. Next run L-N5**
- [x] **Perf: fresh baseline with the trigger-mask ON.** Ceiling **≤ 6% frame share at 300z**.
      **Attempted 2026-09-15** via `debug.stress-fill {"plants":40,"zombies":300}` (real board,
      `debug_game_state` confirmed `plantCount≈40, zombieCount≈300` live). Result at 300z:
      `combat.dispatch` 28.5-40.5% and `effect.onCapture` 39.2-57.7% of frame time, `fpsAvg` 8.3-13 —
      **far over the 6% ceiling.** But an A/B toggle of this feature's own kill switch
      (`POST /api/cheats/toggle {"id":"LAWN-BASIC-ATTACK","enabled":false}`, per
      `LawnBasicAttackFeature.DebugOverride`) run against the SAME 300z stress scenario shows **no
      measurable difference**: OFF measured `combat.dispatch` 37.0-40.5% / `effect.onCapture`
      51.6-57.7% — equal to or higher than the ON sample, well within run-to-run ramp-up noise. **The
      breach is real but is NOT this feature's incremental cost** — `effect.onCapture`/
      `combat.dispatch` are the general per-vanilla-hit RPG capture/dispatch pipeline that runs for
      every plant/zombie hit regardless of whether an `OnDamageDealt` basic-attack grant exists, and
      it is already named as a pre-existing cost driver independent of any one feature (memory:
      "lag is per-hit `FindObjectsOfType` scans + uncached resolves on the Unity main thread," 2026-08
      perf audit). **Conclusion: T13's own kill switch (`FUSIONRPG_LAWN_BASIC_ATTACK`) does not need
      to flip — turning it off does not fix the 300z frame-share problem, so it is not this feature's
      defect to carry.** The general 300z engine-scaling cost is real and severe but is out of this
      task's own scope (a `lawn-combat-wire` T13 gate on a `lawn-combat-wire` feature, not a
      general-engine-perf task) — named here, not silently dropped, for whichever task owns general
      lawn perf scaling next. Toggle restored to default ON and board/mods reset immediately after
      the measurement.
      **audit 2026-09-15 REOPENED: restored clause deleted by 40e89308: **On breach the feature ships behind the kill switch defaulted off.** Breach measured; the A/B did not isolate the feature (mid-match toggle, different elapsed times, Lose-state boards, not the ceiling's metric, no baseline file). Breach escalates to the owner per plan. Next run L-N1, L-N8**
      *audit 2026-09-15 CLOSED as a measured FAIL, stop rule applied: L-N8's isolated A/B (fresh process per arm, env set at
      start, same scenario, no Lose screen, the ceiling's own pipeline-share metric) measured **26.73% / 27.22% on vs
      0.10% / 0.09% off** at 300 zombies against the 6% ceiling — the breach is this feature's own cost, not the general
      engine's, which reverses the conclusion above. The owner's L-N1 ruling ships the feature behind the switch, default
      off (`LawnBasicAttackFeature.DefaultEnabled = false`). Baseline files: `docs/research/perf/_baseline-lcw-300z-env-*.json`.*
- [x] Real numbers recorded, never a boolean. An honest FAIL correctly reported is this task
      succeeding.
      *audit 2026-09-15 CONFIRMED: every proof entry records figures, including the FAIL/blocked ones*

**Status after the sixth/seventh-defect fixes (2026-09-15, debug-spawn HP pin + ptr-reuse cleanup):
proofs 4 and 5 are now CLOSED with sustained live evidence** (six consecutive 5s observer windows,
zero dropped records). **The perf ceiling proof is also now attempted and resolved** (300z A/B via
the feature's own kill switch: the measured breach is a pre-existing general-engine cost, not this
feature's own, so `FUSIONRPG_LAWN_BASIC_ATTACK` stays default ON). **The task itself is still NOT
closed** — proof 1 is half done, proof 2 is half done (Strong side real, Weak side blocked on the
zombie-reaches-house-too-fast issue — the SAME issue that made every 300z stress-fill run end in a
Lose within seconds this session, still unroot-caused), and proof 3 is half done. **Proofs 6 and 7
are now also CLOSED** (proof 6 via a structural code-read proof after a genuine, honestly-recorded
live attempt hit real board-state obstacles; proof 7 via a dedicated double-kill falsifier, not
passive observation — see above). Each remaining proof is a genuine, separate live-setup task, not a rerun
of what already ran.

**Every proof above is read from the observer's run file (T0), not from console output, not from a
worker's report, and not from anyone's eyes.**

**Verify:** `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe`;
`.\scripts\deploy-play.ps1 -NoServer`; `Invoke-RestMethod http://127.0.0.1:5088/health`;
observer run; `.\scripts\probe-perf.ps1 -Scenario <id> -DurationSec 60`
**Dependencies:** all, incl. **T0** · **Files:** none (operational) + `docs/research/perf/` +
the observer run file · **Scope:** M (real-time, not compute-bound)

---

### T13 evidence — 2026-09-15, FSM sequence trace (owner demand: one diagram, every module logs, prove the flow)

The owner rejected the prior evidence in this file as unproven: no module in the FSM/funnel chain
wrote a log, so "the plant attack triggers" was an inference from downstream numbers, never observed
directly. Confirmed by `grep` before any fix: **zero** log statements existed anywhere in
`EffectBag.cs`, `CombatDamageDispatcher.cs`, `EventDrain.cs` (Core), or `EventDrainHost.cs`
(Injector) — the entire FSM/funnel was silent. Fixed by adding one `CheatState.Note` at each node
below, all gated behind the existing `CheatState.EmitProof && CheatState.On("SYS-EMIT-PROOF")`
convention (`EntityStatWriter.ProofWrite`'s own gate) — additive, standing infrastructure, not a
throwaway trace to be deleted after this session.

**Sequence (plant basic attack → zombie takes RPG damage):**

```mermaid
sequenceDiagram
    participant PvZ as PvZ vanilla (Plant fires pea)
    participant BI as GameHooks.BulletInit.Postfix
    participant ZTD as GameHooks.ZombieTakeDamage.Prefix
    participant EDH as EventDrainHost.TryRecordDealtFromBullet
    participant ED as Core EventDrain (buffer/dedupe)
    participant OD as EffectRuntime.OnDrained
    participant EB as Core EffectBag.OnEvent -> FireGrant
    participant CDD as Core CombatDamageDispatcher.DispatchInstant
    participant DAP as Core DamageApplyPipeline.Apply
    participant GCH as GateCounterHost.HandleDamageApplied (OnDamageApplied hook)
    participant FUN as Core EffectFunnel (queued mutation)
    participant FA10 as InjectorEffectActionSink (hp branch)
    participant ESW as EntityStatWriter.AddZombieHp -> ProofWrite

    PvZ->>BI: Bullet.InitData
    BI->>BI: resolve shooterPtr (direct read, else position-fallback)
    BI->>EDH: CacheBulletShooter(bulletPtr, shooterPtr)
    PvZ->>ZTD: Bullet hits zombie
    ZTD->>ZTD: RAW theDamage (vanilla, pre-scale)
    ZTD->>EDH: TryRecordDealtFromBullet(bulletPtr...)
    EDH->>ED: Record(GameEventRec actorPtr=shooterPtr)
    ED-->>OD: Drain() callback (buffered/coalesced)
    OD->>EB: Bag.OnEvent(ev) [ev.ActorPtr=shooterPtr]
    EB->>CDD: DispatchInstant(packet) [packet.ActorPtr=ev.ActorPtr]
    CDD->>DAP: Apply(finalizedSignedAmount)
    DAP->>FUN: sink.Apply (EnqueueMutation)
    CDD->>GCH: onDamageApplied(result, origin, attackerPtr)
    FUN-->>FA10: funnel drain -> plan item (channel=hp)
    FA10->>ESW: AddZombieHp(zombie, amount, source)
    ESW->>ESW: ProofWrite -> "writer.zombie ..." log
```

**Per-node log evidence, one real run (`2026-09-15T05:38:53-05:39:09Z`, `lab-overlay`, session
ended before observing — see gotcha below):**

| # | Node | Log line (verbatim sample) | Proven |
|---|---|---|---|
| 1 | `BulletInit.Postfix` (`GameHooks.cs`) | `fsm-trace BulletInit.Postfix bulletPtr=1F77B66DD20 shooterPtr=1F779420900 shooterTypeId=0` | yes — shooter resolves to the real plant ptr |
| 2 | `ZombieTakeDamage.Prefix` RAW (`GameHooks.cs`) | `fsm-trace ZombieTakeDamage.Prefix RAW theDamage=1 zombiePtr=1F77947C320` | yes — vanilla damage is genuinely **1** (silenced), matching the owner's own screen observation, not the `vanilla=20` this file reported earlier this program (see finding B) |
| 3 | `EventDrainHost.TryRecordDealtFromBullet` | `fsm-trace EventDrainHost.TryRecordDealtFromBullet recorded=True shooterPtr=1F779420900 targetPtr=1F77947C320 damage=1` | yes — event recorded with the correct (fifth-defect-fixed) shooter identity |
| 4 | `EffectRuntime.OnDrained` (event in) | `fsm-trace EffectRuntime.OnDrained ev trigger=OnDamageDealt actorPtr=1F779420900 targetPtr=1F77947C320 damage=-1 swingId=1F77B66DD20 isFirstOfSwing=True` | yes — the event Core's `EffectBag` actually receives carries the **plant's** ptr as attacker, not the bullet's |
| 5 | `CombatDamageDispatcher.OnDamageApplied` (FSM apply) | `fsm-trace CombatDamageDispatcher.OnDamageApplied outcome=Applied appliedAmount=0 absorbedAmount=0 origin=DirectHit attackerPtr=1F779420900` | yes — `FireGrant` for `lawn-basic-attack` demonstrably runs per hit, attacker correctly identified. **`appliedAmount=0` here was itself the symptom of a real defect, fixed same session — see "Finding C (supersedes Finding B)" below; after the fix this same node reads real nonzero values, e.g. `appliedAmount=-16`** |
| 6 | `EntityStatWriter.ProofWrite` (final Unity write) | (separate run, zombie melee vs plant) `writer.plant src=effect.fa10:lawn-basic-attack@1F779420240 ptr=1F779420240 hp 300/300->194/300` | yes — the terminal Unity HP write fires and is logged **whenever the applied amount is non-zero**; a zero-amount apply (node 5 above) produces no writer line, confirmed by its absence in the same window |

**Finding A — legacy-path gotcha (real, cost ~10 minutes of misleading data this session):**
combat run through the injector while `DebugRuntime.SessionActive` is still true (e.g. right after
`debug_lawn_setup`, before an explicit `session/end`) takes the **legacy dict/`OnCapture` path**, not
the v2 `EventDrainHost`/`OnDrained` path this whole program built and fixed — confirmed live: with the
session still active, `OnDamageApplied` fired with `attackerPtr=<bulletPtr>` (the pre-T6 naive
identity) and none of nodes 3/4 above ever logged at all. This is already documented at this file's own
Task 0 (line 17: *"a debug session sets `EventDrainHost.Active = false`, disabling the path under
test"*) — this session re-discovered it empirically rather than reading it first. **Any T13 proof run
must call `POST /api/debug/session/end` before observing combat**, or it silently measures the wrong
pipeline.

**Finding B — `debug.combat.silence-vanilla` zeroes the SAME ActorHub channel both systems read
(by design, not a bug, but a real confound for THIS proof):** `SilenceVanilla` (`DebugCombatActions.cs:16`)
sets `A-P-ATK%=0` and `P-ATK=0` on the plant, which zeroes the plant's ActorHub-composed ATK —
the one number both vanilla bullet damage **and** the RPG overlay math (`OverlayCombatMath`, reading
the same derived snapshot) scale from. That is why every `OnDamageApplied` for the plant's own attack
this run showed `appliedAmount=0`: not a broken trigger, but a correctly-computed zero from a
zeroed attacker ATK. **This means `lab-overlay`'s own default setup (which calls silence-vanilla
unconditionally) cannot be used to prove a non-zero RPG delta for the silenced side's own attack** —
it only isolates the *defender's* screen number. Un-silencing (`POST /api/debug/reset-mods`, which
correctly clears the `A-`/`P-` override groups) restores real ATK, but the plant died to the zombie's
own un-silenced melee bite (real vanilla damage) within one bullet cycle both times this was tried —
confirming the zombie's attack chain end-to-end (`writer.plant ... -53/-53 -> hp 300/300->194/300`,
finding above) but not yet capturing a non-zero plant-attack `appliedAmount`. **Still open**: repeat
with the plant's HP buffed (or the zombie's ATK floored) so the plant survives past its first real
volley.

**Finding C (supersedes Finding B) — `fx.overlay_damage` never carried an "amount" at all; every
direct basic-attack RPG delta silently resolved to zero, for BOTH sides, always, regardless of
silence-vanilla:** Finding B's explanation (a zeroed ATK from `silence-vanilla` correctly computing a
zero delta) was wrong — or at best an unfalsified guess that happened to fit the data, exactly the
kind of claim this whole session exists to catch. Traced properly this session (2026-09-15, second
pass): with vanilla ATK confirmed genuinely un-silenced (`RAW theDamage=20`, real, not floored to 1),
`appliedAmount=0` for the plant's own attack **persisted** — ruling Finding B out completely, since a
real nonzero ATK feeding a correctly-computed elemental/defense formula cannot legitimately land on
exactly zero, run after run, on three separate independent test sessions (different plant/zombie
ptrs, different elements, one on a freshly restarted game process). The real cause, found by reading
the actual dispatch chain rather than guessing from the number: `data/seed/atoms/fx-core.json`'s
`fx.overlay_damage` atom — the ONE atom `BasicAttackGrantBuilder`'s "lawn-basic-attack" grant points
at — only ever authored `{"channel":"hp"}` in its params. No `"amount"` key at all.
`DamagePacketBuilder.ResolveAmount` (`DamagePacketBuilder.cs:63-93`) defaults `SignedAmount` to `0`
when the key is simply absent — so **every direct hit through this atom computed a zero HP delta from
the moment the grant was ever bound**, independent of the real incoming vanilla damage, independent of
silence-vanilla, independent of element or side. The only reason ANY nonzero RPG delta was ever
observed all program — the `-53`/`-33`/`-46` numbers this file and the earlier "self-kill" investigation
both read as real basic-attack damage — was the T5.4 **reflection** mechanic's own bounce packet
(`CombatDamageDispatcher.TryReflect`), which builds its `SignedAmount` directly in code and never goes
through `DamagePacketBuilder`'s broken resolution at all. Every one of those numbers was a zombie
reflecting part of the plant's own (silently zero) hit back onto the plant — never a genuine direct
hit landing.

**Fixed, same session**: authored the already-built, never-used event-linked `ValueSpec` marker
(`{"eventField":"damage","multiplierMilli":1000}`) into `fx.overlay_damage`'s params — a wiring gap,
not a new mechanism (`AtomRowValidator` already scopes `eventField` to `resource.delta` atoms
specifically for this shape, and `"damage"` is already the one closed `EventFields` member).
Regenerated `EffectAtomCatalog.Generated.cs` via `tools/ElementEnumGen --effect-emit`, which needed
teaching first (`EffectCatalogGen.Literal` had no case for the nested `Dictionary<string,object?>`
the compiled marker produces, since no shipped atom had ever used one). Updated
`MigrationParityTests`' frozen-oracle exception list (a third deliberate, unmirrored content
correction, alongside `fx.set_dirt_box`/`fx.grid_item_cycle`). **2348/2348** across the
Atoms/Effects/Combat Core.Tests namespaces, including the corrected parity test. Committed
`fdf3885c`.

**Live-verified, same session, fresh game process, real un-silenced ATK, session ended (real v2
path)**: the plant's own attack now produces genuine nonzero HP writes on the **zombie**:
```
fsm-trace CombatDamageDispatcher.OnDamageApplied outcome=Applied appliedAmount=-16 ... attackerPtr=<plantPtr>
writer.zombie src=effect.fa10:lawn-basic-attack@<plantPtr> ptr=<zombiePtr> hp 793/840->756/840
```
repeated across 5 consecutive real hits (-37, -16, -32, -21, -16), source correctly stamped with the
**plant's own ptr** (not a reflection bounce, not the bullet's ptr) — the direct proof the owner asked
for from the start of this whole investigation: the plant's attack triggers, and it now actually deals
real RPG damage to the zombie.

**Finding D — found and fixed the same session: `OnDamageApplied` fired TWICE per single real hit,
doubling every basic-attack's RPG delta.** Traced from the arithmetic (5 consecutive samples:
`-16+-21=-37`, `0+-16=-16`, `-16+-16=-32`, `0+-21=-21`, `-16+0=-16`, each pair summing exactly to the
logged `writer.zombie hp X->Y` delta) to the real cause: `EffectOwnerKey.MatchesEvent`'s `"entity:"`
branch (`EffectProcAndOwner.cs:107-117`) matched a grant whenever its own ptr equalled EITHER
`ev.ActorPtr` OR `ev.TargetPtr`, with no regard for which trigger was firing. For `OnDamageDealt`
specifically (directional — `ActorPtr` is the attacker, `TargetPtr` the victim), any entity-scoped
grant bound to BOTH combatants — `lawn-basic-attack`'s own shape, since every plant AND every zombie
holds one — matched twice per hit: the attacker's own grant (correct) and the victim's own grant for
its unrelated future attacks (wrong). Both produced a packet with the SAME `ActorPtr` (the field comes
from the event, never from whichever grant matched), which is exactly why both `OnDamageApplied`
calls showed the plant's ptr, not one plant/one zombie as a reflection bounce would. **Fixed**:
narrowed `OnDamageDealt` to actor-only, mirroring the `plant:{tid}`/`zombie:{tid}` branches just above,
which already carried this exact narrowing for the identical trigger; every other trigger (crucially
`OnDeath` kill-credit, per `EffectBagAuditTests`'s own documented "Actor or Target" contract) keeps the
broader match, unaudited and untouched. Verified: full `Core.Tests` (13513/13513), `Server.Tests`
(443/443), `Data.Tests` (1295/1295, one failure isolated to cross-run interference from a parallel
co-run, confirmed clean alone) — zero regressions from narrowing the one over-broad case. Committed
`29cbb7f3`.

**Live re-verification of this second fix — root-caused and completed.** Three consecutive attempts
produced zero combat activity; root cause found via one screenshot (the class of check this program
correctly avoids for *damage-number* verification, but the right tool for "why has nothing happened at
all" — the injector's own telemetry had no way to say "the level already ended"): the board was
sitting on a real vanilla **Lose** screen ("有僵尸进入了你的房子" — a zombie reached the house),
`CanvasUp/LoseMenu(Clone)/backtomenu`/`TryAgain`, from several attempts earlier — every respawn after
that point was into a dead, paused level, not a live one. `debug_inspect(scope="menu")` had only ever
been checked for `PauseMenu_Btn`; a `LoseMenuBtn` state was never in the earlier check, so nothing
surfaced it. Not a code defect, not caused by either fix. Dismissed (`backtomenu`), fresh
`lab-overlay` setup, `reset-mods`, `session/end` — combat resumed immediately.

**Clean result, single-fire confirmed live (2026-09-15, `attackerPtr=<plantPtr>` throughout)**:
```
fsm-trace CombatDamageDispatcher.OnDamageApplied outcome=Applied appliedAmount=-16 ...
writer.zombie src=effect.fa10:lawn-basic-attack@<plantPtr> ptr=<zombiePtr> hp 780/840->764/840   (Δ-16, matches exactly)
fsm-trace CombatDamageDispatcher.OnDamageApplied outcome=Applied appliedAmount=-21 ...
writer.zombie ... hp 763/840->742/840   (Δ-21, matches exactly)
```
Exactly ONE `OnDamageApplied` per hit now (was two, summing to the write, before the fix), and every
`writer.zombie` delta matches its single `OnDamageApplied` value exactly — no more doubling. Both
fixes (`fdf3885c`, `29cbb7f3`) are now live-verified, not just test-verified.

**Lesson for this program's own live-probe discipline, named so it isn't repeated**: a "nothing is
happening" symptom needs a menu-state check for BOTH `PauseMenu_Btn` and `LoseMenuBtn`/win-equivalent
states before spending time on a code-level root cause — the injector's own telemetry has no dedicated
signal for "the level already ended," so this is exactly the kind of gap a screenshot is the right,
fast tool for, distinct from the already-correct rule against using screenshots to read exact damage
numbers.

**Net effect on this task's status:** proof 1 (attribution) is now confirmed **three ways** — the
observer's `swing=<plantPtr>:N` sample, the independent FSM log trail (nodes 1-5), and now a genuine
nonzero RPG damage write on the correct victim sourced from the correct grantor. The "never proves the
attack triggers" objection is answered completely: it triggers, it computes a real number, and that
number reaches the zombie's HP. What remains open is unchanged in substance (proofs 2/4/5/6/7 and
perf) — the fix above removes a confound from all of them (proof 2's element-ratio comparison, proof
4/5's exhaustion math, and the perf-ceiling proof's own frame cost all now measure a REAL delta
instead of a silently-zero one), so re-attempting any of them should be more informative than before,
not less.

**Two more real findings, from a proof-4/5 (exhaustion/regen) attempt this same session — neither
fixed here, both named precisely:**

1. **`debug.spawn-plant`'s absolute `hp`/`maxHp` override does not survive the very next automatic
   scale-push.** Attempted to buff a fresh plant to `500000/500000` HP (so it would survive long
   enough to exhaust and regen its stamina pool) via `POST /api/debug/spawn-plant
   {"maxHp":500000,"hp":500000}`. Log proof: `writer.plant src=debug.spawn ptr=1EF7E418D80 hp
   300/300->500000/500000` immediately followed, one line later, by `writer.plant
   src=cheat.pushScales ptr=1EF7E418D80 hp 500000/500000->300/300` — a routine, automatic
   `PushScalesNow()` (fired by an unrelated cheat-state change, not something this session
   deliberately triggered) silently reverted the debug-spawn override back to the type's own default
   baseline. This is a real gap in the debug-spawn tool, not in `lawn-combat-wire`'s own production
   code: an absolute spawn-time override needs to persist across a reapply the same way
   `UniqueBoundLoadout`'s Hub-bonus grants now do (T14, `actor-hub-and-combat-power-solid-fixing`),
   not as a one-shot field poke. **FIXED, same session, commits `aaf43f0e`/`f89f0333`**: new
   `InjectorSpawnHpPin` (per-ptr, ratio-preserving re-assert on every `EntityApply` reapply
   regardless of `includeAbsolute`, cleared on `GameHooks.ForgetEntity` after a live IL2CPP
   ptr-reuse leak was caught the same session). Live-reverified: the proof 4/5 rerun above used this
   exact mechanism (`maxHp:50000`) and the plant survived 6 full observer windows (~30s) under real
   zombie fire without reverting, closing proofs 4 and 5.
2. **A severe per-frame stall (`loopMs` ~580-650ms, up from a healthy ~7-17ms baseline) was observed
   repeatedly across this whole session — root cause finally found, and it was never a backlog.**
   Every occurrence, checked after the fact, correlates with the board sitting on a **`LoseMenu`**
   (a zombie reached the house — the same real vanilla end-state root-caused earlier in this file for
   the "plant stopped firing" finding). This was the SAME underlying cause both times, not two
   different mysteries: `debug_inspect(scope="menu")` was only ever checked for `PauseMenu_Btn` during
   the earlier "resolved by itself" investigation, so the LoseMenu was invisible to that check too —
   the ~30-minute idle gap that appeared to "fix" it on its own most likely just gave enough real time
   for a manual dismiss-and-retry cycle to happen incidentally, not a genuine self-resolving backlog.
   **The earlier "transient debug-command backlog" explanation in this same file is retracted** — it
   was a plausible-sounding guess fitted to incomplete data, exactly the kind of claim this whole
   session exists to catch in others' work. Confirmed by direct observation this time: `loopMs` stuck
   at ~630-650ms for 5+ straight minutes while a `LoseMenuBtn` control was live on screen (checked via
   `debug_inspect(scope="menu")` mid-stall), and recovered the moment the dialog was dismissed and a
   fresh board was set up. **Standing lesson for this program's own live-probe discipline**: check
   `debug_inspect(scope="menu")` for both `PauseMenu_Btn` and `LoseMenuBtn` (and a win-equivalent, not
   yet named) before trusting ANY "nothing is happening" or "loopMs is elevated" observation — the
   injector's own perf/combat telemetry has no dedicated signal for "the level already ended," so a
   dead board reads identically to a live one until a screenshot or an explicit menu check says
   otherwise.

---

### Dropped success criteria — [audit] restored to their tasks

The first draft of this todo silently dropped eight criteria that exist in the specs. Each is added to
the task named:

| Criterion (from its spec) | Goes to |
|---|---|
| *"The engine's fallback and the seeded row are the same action id"* | **T7** |
| *"An `entity:{ptr}` grant bound to the firing plant matches on a projectile hit"* — the precondition T10 rests on | **T6** |
| *"Caller-side `else if` ordering — audit all three sites"* (`GameHooks.cs:750/870/1039`); a debug/telemetry branch can win before the recorder | **T9** |
| *"Re-entry depth stays 0"* — an overlay apply must not emit a `combat.hit` that nested-flushes the Funnel | **T9** |
| *"Regen accrues on the 100 ms grid and does not run while paused"* | **T12** |
| *"Battle unchanged where no cost row is authored for a mode"* | **T12** |
| *"`HasOnDamageDealtGrant()` true on a live board"* + the Neutral-owner pass-through degenerate case | **T10** |
| *"`CostLedger.Check` only inspects `OnCommit` rows"* — author the cost `onDeclare` and the gate is **silently vacuous**, the exact failure class this program exists for | **T12**, as an explicit criterion rather than a Code-style note |

---

### Task splits — [audit] two tasks exceed the sizing rule

- **T9 (L)** → **T9a** four gates + swing dedupe · **T9b** never-drop + coalescing (incl. the
  `event-pipeline-v2-ssot.md` amendment and doc defect #3) · **T9c** liveness, death ordering,
  ptr-reuse, instakill. T9b is the D9 half and is independently testable.
- **T12 (L)** → **T12a** pool seeding (`seedResourceBaseline`) + the non-zero-max check ·
  **T12b** regen as a third kernel kind · **T12c** the `CostLedger` seam and payment. **All three
  still land together** — the all-or-nothing property is unchanged by splitting the work.

---

### Task 14: The seven doc defects — **one blocks builders outright**

**Description:** The ideal's "Doc defects found while writing this" list had no owner. Only #6 (the
stale `EventDrainHost` class header) was carried, by T9.

**Acceptance:**
- [x] **#1 `DESIGN-GATE.md:33`** — the *"Where logic may live"* row states *"It does not compute damage
      at the moment of the hit."* **As written, the mandatory reading gate forbids this program.** Its
      source (`overlay-control-loops.md:22`) bans a **Server** round trip, not in-process computation,
      and `EffectBag.cs:567,637` already does exactly this. Narrow the gate line to name the Server.
      **Highest-cost omission in the plan — a builder satisfying the gate finds the work prohibited.**
- [x] #2 `DESIGN-GATE.md:56` — cites `battle-timeline-map.md`/`battle-turn-ideal.md` for *"Battle
      consumes FA10 only"*; the rule is in neither (real source: `BattleEffects.cs:225`) and is three
      opcodes stale (FA2, FA1, `structure.place` added).
- [x] #3 `event-pipeline-v2-ssot.md:60` — stale *"~1.5 ms"* drain budget; shipped code is
      `Math.Clamp(frameSec * 0.10, 0.0002, 0.002)`. **T9 already amends this file** — fold it in there.
- [x] #4 Elements row — matrices are asymmetric in **contract**, not content; `spec-shield-and-elements.md`
      §0 already carries the correction.
- [x] #5 `ActionTag` is **9**, not 8 (`Construct`, `ActionEnums.cs:57`); seedsmith's mirror is stale too
      (`tools/seedsmith/seedsmith/adapters/actions/vocab.py:35-37`).
- [x] #7 `DerivedStatRegistry.cs` — max note is `:239`, regen note `:241`. **Verified 2026-09-13:
      both line numbers are correct as shipped, and `DESIGN-GATE.md` cites neither** (its only
      `DerivedStatRegistry*` reference is the §3 log row for `DerivedStatRegistryTests.cs:22`). **No
      edit was owed for this item** — the ideal's note is a fact, not a defect. Separately found and
      **not fixed** (other programs' files, out of this task's `paths`): four citations of
      `DerivedStatRegistry.cs:237` for `move.range` are stale — `MoveRange` registers at `:260`;
      `:237` is a brace inside the resource loop (`action-corpus/spec-lawn-reposition.md:69,253`,
      `action-corpus/spec-movement-payload.md:43,280`).
- [x] Plus, from the ideal's Tunables: the drain budget is **not** a tunable — a structural per-frame
      cap — and *"should say so in a comment"*.

**Verify:** each edit re-read against the source it corrects; no rule changed in substance, only
narrowed/corrected to match shipped code.
**Dependencies:** none — **do #1 first, before any builder reads the gate** · **Files:**
`docs/DESIGN-GATE.md`, `event-pipeline-v2-ssot.md`, `vocab.py`, `EventDrainHost.cs` · **Scope:** S

---

## ⛔ HUMAN GATE — the only one, after Task 13

**Lead prepares everything and stops.** Server up, injector deployed, board live, ruler running, all
seven proofs and their falsifiers executed, run file written.

### The ruler decides these — mechanical, already answered before the human looks

- [ ] All seven proofs ran with falsifiers, **outside a debug session** (`EventDrainHost.Active` true
      throughout — asserted by the observer, not eyeballed)
- [ ] Triggers == swings; zero dropped effect-bearing records
- [ ] Fire/Ice differential matches the ratio computed from `matchupShareK` **before** the run
- [ ] An exhausted actor recovered on the **same ptr**, never a respawn
- [ ] Frame share under a 300z wave, measured with the trigger-mask **on**
- [ ] Every number traces to an executed command; no claim rests on a summary

### The human decides these — product calls the numbers cannot make

- [ ] Does the damage **feel** right, or does it trivialise the lawn / do nothing
- [ ] Is the measured frame cost worth the feature (ceiling ≤ 6% at 300z is a proposal, not a verdict)
- [ ] Does the elemental VFX read clearly on a busy board
- [ ] Is the exhaustion cadence a mechanic or an annoyance
- [x] **Ship / ship-behind-switch-defaulted-off / stop**

*Eyes are a secondary signal here — visual breakage the instrument has no channel for. Never the
metric.*

### Program close
      *Owner ruling 2026-09-15 (asked directly in the audit session): **ship behind the switch, defaulted off.** Implemented under L-N1.*
- [ ] Deferred items still tracked in the ideal, none silently absorbed or dropped
- [ ] The observer's run file committed as the program's evidence record

---

## Next run — audit 2026-09-15 gaps (`L-N1` … `L-N30`)

Ordered by the plan's "Next run" phases. Every task reports commands run and raw output; a box is
ticked only when evidence matches the bullet's exact wording — never reword a bullet to tick it.

### Phase A — code/test debt (agent-gated, no live game needed)

- [x] **L-N10** Test: observer collection path never alters `EventDrainHost.Active` (T0 bullet 3).
      Verify: new Core/Injector test fails when collection toggles `Active`.
      *Done: `LawnObserverDrainNeutralityGuardTests` 7/7 (source-scan chain; the Injector has no CI unit tests and the observer talks to a live server). 3 mutants killed. Guard.Tests 280 via verify-change. No production defect found: the collection path was already read-only.*
- [x] **L-N13** Core test: two shooters with different composed `combat.power.*` produce different
      packet power through a ptr-aware resolver (T6 differential). Mutation: resolver ignoring ptr fails.
      *Done: `AttackerPowerByPtrTests` — one `OverlayCombatMath`, one `EventDrain`, attackers 0x1001 (power 10) and 0x2002 (power 400); resolver keyed strictly by ActorPtr and throws on unknown keys; asserts strong hits harder and the swing ptr is never resolved. Mutation (resolver returns the weak snapshot for every attacker ptr) fails. Core.Tests 13544 via verify-change. T6 bullet stays open: snapshots are fixed overlays, not Hub-composed, and the live half is L-N7.*
- [x] **L-N14** Fix T7 wording ("24 shipped briefs, one documented Kind change") and replace the
      population-count pins (24/3/21) in `ActionCorpusImporterTests.cs` with contract assertions
      (guardrail rule: never pin a population count).
      *Done: T7 bullet reworded in this commit (not ticked — tick is a separate change). `TheRealShippedCorpusHasExactlyOneDocumentedKindChangeAndNoOthers` (pinned 24/3/21 + named ids) replaced by `TheRealShippedCorpusHonoursKindHintAndKindAwareCostForEveryImportedBrief`: reconciliation (imported + rejected = parsed) and per-imported-brief Kind == kindHint ?? Skill, cost resource == template row. Mutation (composer ignores kindHint) fails it. Data.Tests 1231/1231 via verify-change.*
- [x] **L-N16** Test: a record queued for a ptr later marked dead applies no delta and runs no `Die()`
      on the funnel path; fixture for `EventDrainHost.DeferForget` ordering (T9, GATE 2).
      *Done: new Core `EntityLiveness` + `DeferredForgetQueue`, used by `EventDrainHost`. Two real defects fixed: (1) a record queued before death drained into `AddZombieHp` at HP <= 0 and ForceKill ran a second `Die()` — now refused at apply time; (2) dead marks and the `DeadZombies` once-per-ptr latch never cleared on spawn, so a new entity at a recycled ptr refused every RPG hit and skipped its own death flush/forget for the rest of the match — now cleared on Plant.Start / Zombie.Start / Zombie.InitHealth (never on resync). Tests: `EntityLivenessTests` 10, `DeferredForgetQueueTests` 4, `EntityLivenessWiringGuardTests` 7. Mutants killed: AdmitsDelta=>true, MarkSpawned no-op, RunDue without flush, sink gate removed. verify-change: Core 13560, Guard 273, injector-compile + single-writer/funnel-delta/actor-hub/secondary-no-unity OK.*
- [x] **L-N17** Test: bind grant at ptr P, forget P, re-register P → no stale grant (T10).
      *Done: `BasicAttackGrantRecycleTests` 4/4 — forget withdraws; new entity at P carries only its own element; withdraw catches a differently-cased ptr spelling (found: GrantId keyed on the raw spelling let two spellings of one entity hold two grants that both fire — fixed by normalising `GrantIdFor`, test `Two_spellings_of_one_ptr_bind_one_grant_not_two`); same-ptr rebind is an upsert. Mutation fails 2/4. Core.Tests 13541 via verify-change.*
- [x] **L-N18** Test `ResourceBaselineSubsystem` for side=zombie (max stamina > 0, spend succeeds),
      or record a stated exemption in `spec-basic-attack-cost.md` (T12).
      *Done: hold was already tested (verifier missed it); spend test added. LawnActorResourcePoolsTests 11/11; Core.Tests via verify-change.*
- [x] **L-N19** `fx.overlay_damage` sign contract. Producers disagree: lawn records store
      `amount = -|damage|`; `EffectBag.DrainOverlayProcs`, battle `BasicAttack`, `SimEffectHost` emit
      positive `Damage`; `OverlayCombatMath.Finalize` treats positive as heal. Define the sign in
      `ValueSpec`/`DamagePacketBuilder.ResolveAmount` (or author `multiplierMilli:-1000` against a
      magnitude-positive contract) and add bag-level tests with a lawn-shaped AND a battle-shaped
      event asserting a non-zero negative HP packet.
      *Done: `eventField:"damage"` now reads the magnitude (`DamagePacketBuilder.ResolveAmount`); `fx.overlay_damage` authored `multiplierMilli:-1000`; catalog regenerated (1-line diff); parity pin updated; sign contract added to `spec-value-spec-and-curve.md`. `EventLinkedDamageSignTests` 5/5 (lawn -20 and battle +20 both give -20; +500 gives +10 heal); before the fix the lawn-shaped cases failed. Core.Tests 13537, AtomImporter.Tests 33, ElementEnumGen.Tests 14. Lawn numbers unchanged by construction; live check stays L-N22. Registry gained `seed-atoms-fallback` owner.*
- [x] **L-N20** Move the debug-spawn HP pin from a post-write re-assert into a Hub override input
      (`InjectorDerivedOverride` pattern) so a pin never clobbers Hub maxHp bonuses; move pin/ratio
      math into Core with unit tests (currently zero tests, Injector.Tests not in CI).
      *Done: Core `SpawnHpPin` (per-ptr store + `ApplyTo` Hub absolute input); `EntityApply.RunPlant/RunZombie` pass it into `ActorHub.Resolve` on every resolve regardless of includeAbsolute; post-write re-assert and `EntityStatWriter.Force*MaxHpPreserveRatio` removed (ratio math is the existing tested `StatSystem.CurrentHpForWrite`). `SpawnHpPinTests` 11 incl. real Hub resolve: pin 5000 + bonus 700 = applied 5700; mutant (pin not applied) fails 3. `SpawnHpPinHubInputGuardTests` 3. verify-change: Core 13571, Guard 283, injector-compile + 4 guards OK. Not live-verified. Unchanged pre-existing edge: a non-preserve write with no P-HP takes composed baseline hp even above a low pin.*
- [x] **L-N21** Move the bullet-shooter fallback matcher (`GameHooks.BulletInit.Postfix`, fixed in
      `7073ffcb`) into a pure Core function with tests: Sunflower in same row, tie → drop, no column →
      drop, zombie-side bullet, adjacent-lane (Threepeater side pea) residual pinned as a known case.
      *Done: `FusionRpg.Core.Combat.BulletShooterMatch.Resolve`; `GameHooks.BulletInit.Postfix` calls it. `BulletShooterMatchTests` 8/8 incl. the pinned Threepeater residual; mutation (tie rule removed) fails `Two_candidates_with_the_same_score_drop_the_hit`. verify-change EXIT=0: Core.Tests 13532, injector compile + 4 guards OK, Guard.Tests 266. Live check on a real placed plant is still L-N22.*
- [x] **L-N23** Verification boundaries: add an owner mapping for `tests/FusionRpg.Core.Tests/Atoms/**`
      (currently "BOUNDARY MISSING"); make `injector-fallback` run the injector host compile (scratch
      `OutputPath`) + single-writer/funnel/actor-hub/secondary guards instead of only Core.Tests.
      *Done: registry adds owners for `tests/FusionRpg.{Core,Data,Guard}.Tests/**` and the registry/verify scripts; `injector-fallback` now runs `injector-compile` (new `scripts/guard-injector-compile.ps1`, temp OutputPath, loud SKIPPED without a game dir; deliberate compile error → exit 1) + single-writer/funnel/actor-hub/secondary guards + Guard.Tests. First real run exposed `EntityFields12PlusGuardTests` stale since T4 `e6af60b5` (3 assertions) — fixed. `verify-change` EXIT=0: 5 guards OK, Guard.Tests 266/266.*
- [x] **L-N24** `tools/ElementEnumGen` `--effect-check` mode wired into CI so a stale
      `EffectAtomCatalog.Generated.cs` fails (today no check mode exists).
      *Done: `EffectCatalogGen.GenerateFromSeed`/`Matches`; `--effect-check <path>` exits 1 when stale. `EffectCatalogCheckTests` (3) runs in the already-CI `FusionRpg.ElementEnumGen.Tests` — no CI file edit. Falsified: seed `multiplierMilli -1000→-999` made `--effect-check` exit 1 and the test fail; seed restored clean. Registry: `effect-catalog-drift` seam + ElementEnumGen.Tests owner. verify-change EXIT=0: Core 13537, ElementEnumGen 17, Guard (boundary) 10.*
- [x] **L-N25** Audit `plant:{tid}`/`zombie:{tid}` owner keys for the same OnDamageDealt either-side
      match fixed for `entity:` in `29cbb7f3` (they fall back to `TargetTypeId`); test both directions.
      *Done: `plant:` was already attacker-only. `zombie:` was a real defect — with `TypeId` absent it matched the victim's `TargetTypeId`. Explicit attacker-only `OnDamageDealt` arm added. `TypeOwnerKeyDirectionTests` 6/6; before the fix `Zombie_key_does_not_match_on_the_victims_type_when_the_attacker_type_is_absent` failed. Core.Tests 13524/13524.*
- [x] **L-N5a** Bug: `debug.kill` ignores the request `ptr` (`DebugActions.Kill` → `OneShotSelected`).
      Honour `ptr` or reject it loudly; test the route contract.
      *Done: `DebugActions.Kill` routes an explicit `ptr` to `KillByPtr` (exact match, `CheatState.Error` when absent, no selection fallback). `DebugKillPtrContractGuardTests` 2/2; mutation (ptr read removed) fails `Kill_reads_ptr_before_any_selection_fallback`. Injector compiled to scratch; not live-deployed (game running).*

### Phase B — owner decisions (escalations the run wrongly made itself)

*Added by the audit continuation: **L-N28** (battle stamina regen on or lawn-only — see its findings in Phase C list).*

- [x] **L-N1** Perf ceiling breached (T13). Plan: breach escalates to owner; spec default on breach is
      **kill switch defaulted off**. Owner rules ship / ship-behind-switch-off / stop after L-N8 data.
      *Done: owner ruled "ship behind switch, default OFF" (2026-09-15). `LawnBasicAttackFeature.DefaultEnabled = false`; env `FUSIONRPG_LAWN_BASIC_ATTACK=0` forces off, `=1` forces on at process start (the only sanctioned enable for live proofs, per L-N8); CheatSchema/CheatRegistry display default follows. Tests: `LawnBasicAttackDefaultOffGuardTests` (Guard, CI), `CheatSchemaTests` (CheatCore 41/41), `LawnBasicAttackFeatureFlagTests` (Injector.Tests 5/5, local only). Also fixed a verification-boundary defect: `src/FusionRpg.CheatCore/**` mapped to Core.Tests and never ran CheatCore.Tests. verify-change: Core 13575, Guard 293, CheatCore 41, injector-compile + 4 guards OK.*
- [x] **L-N11** Spec amendments: `spec-element-cache-invalidate.md` / `spec-basic-attack-grant.md:84-92`
      still require hypno re-bake; the finding says elements are hypno-invariant. Owner approves the
      rewording; then T3 bullet 1 and T10 hypno bullet are reworded and ticked.
      *Done: owner approved the rewording 2026-09-15; spec amendment + rewording in `dc8c7d43`, ticks in a separate change against `Trigger2_hypno…`.*
- [x] **L-N12** T4 numerics: rewrite the `double` mitigation interior to long/per-mille, or amend
      `spec-combat-numerics.md:74,92-93` to the allowlist the scan test already uses.

### Phase C — live re-proof (needs game restart — ask owner before closing the game)
      *Done: owner ruled floating-point allowed (and the whole float ban removed, `e103db1e`, `303952f7`); spec amended in `dc8c7d43`; T4 bullets reworded there and ticked separately against the overflow/divide-last tests.*
- [x] **L-N22** Redeploy `7073ffcb` (game must be closed; ask first). Verify: no `fsm-trace` notes
      without `FUSIONRPG_FSM_TRACE=1`; shooter attribution on a real player-placed plant (is
      `Bullet.from` populated there?); no pin carried across a match edge.
      *Done 2026-09-15, live on MelonLoader pvzrh-3.9 with the audit build, no debug session, real Adventure level 2 (1-1 is tutorial-gated: wave timer never leaves 15): (1) `fsm-trace` notes = 0 across a real match without `FUSIONRPG_FSM_TRACE` (22 zombie.spawn, 14 zombie.die). (2) Real player-placed Peashooter (`debug.act place` card path, `plant.place` isFreeSet=false, sun.spend 200): every observed RPG delta names attacker = that plant's ptr (`1FEF8693B40`); with `FUSIONRPG_FSM_TRACE=1` the new trace field shows `Bullet.from` is NOT populated on a real plant either — 2/2 shots `via=fallback`, resolved to the placed plant `1B279B8ED80` — so the position fallback is load-bearing for real play, not only debug spawns. (3) `debug.snapshot` now reports `spawnHpPins`/`spawnOriginMarks`: 1/1 after a pinned debug spawn, 0/0 after leave-board across the match edge.*
- [x] **L-N9** Observer run files: 300z wave with trigger mask on (`DrainTickTotalMs > 0`, dropped
      counters), and a T0 baseline re-run diffed against `_lawn-combat-observer-baseline.json`. Commit
      both run files to `docs/research/perf/`.
      *Done 2026-09-15. Observer defect found first: `RunAggregator` summed the drain's cumulative drop counters per window
      (one run reported 22238 death-budget drops where the counter moved 1623 → 1777); fixed to last-minus-zero-point
      (`ebe91780`, `tools/LawnCombatObserver.Tests`, summing mutant killed). 300z run files (scenario in L-N8):
      `docs/research/perf/_lawn-combat-observer-300z-env-on-{a,b}.json` — `DrainTickTotalMs` 16.5 s/13 windows,
      `DrainTickFrameSharePercent` 26.34 / 25.55, `ActionTriggers` 2764 / 2256, `StaminaSpent` 69100 / 56400,
      `RegenAccrued` 36905 / 31522, `RpgDeltaMergedHits` 2100 / 2018, `DrainDroppedOverflow` 0, `DrainDroppedDepth` 0,
      **`DrainDroppedDeathBudget` 35 / 362**, **`ObserverDroppedRecords` 4021 / 5541**, `InjectorSessionActiveEverTrue:false`
      (L-N33). T0 re-run on a real Adventure 2 board (one placed Peashooter, natural zombies, no session, 30 s),
      `_lawn-combat-observer-1v1-env-{off,on}.json`, against the 2026-09-13 baseline: **off** — every RPG counter 0,
      `DrainTickTotalMs` 0, no drops, `BaselineNoRpgDeltaYet:true`, identical shape to the baseline (vanilla amount 1283 → 2006
      from player progression since); **on** — `ActionTriggers` 2, `StaminaSpent` 50, `RpgDeltaMergedHits` 2 (deltas −4326
      and 0: the alternating-zero pattern L-N2/L-N7 still owe), `DrainTickTotalMs` 55.3, no drops.*
- [x] **L-N8** Clean kill-switch A/B: `FUSIONRPG_LAWN_BASIC_ATTACK=0` set at process start vs unset,
      same scenario and duration, no Lose screen (menu checked), `probe-perf.ps1 -DurationSec 60`, the
      ceiling's own frame-share metric, JSON committed. Also decide: should the runtime toggle
      withdraw bound grants / make `ShouldApplyRider` fail closed? (Today toggling off mid-match lets
      bound grants apply free.)
      *Done 2026-09-15. Scenario: Adventure 2 via enter-level + seed-picker skip, `set-mods` board
      `zombieHealthMultiplier 200` + zombie `attackPercent 0.01` (the board speed/damage multipliers do not reach
      debug-spawned zombies; without these the horde walked into the mowers within ~40 s), `stress-fill` 40 Peashooter /
      300 NormalZombie with wave freeze, then `probe-perf.ps1 -DurationSec 60` and the observer in parallel. A fresh game
      process per arm with the env var set at process start (`1` for on — unset now means default off — and `0` for off),
      order on/off/on/off. Metric = `stress-test.ps1`'s pipeline share (drain + onCapture outside drain + takeDamage, over
      windows with ≥290 zombies). Results: **on 26.73% / 27.22% (fps 17.8 / 17.9); off 0.10% / 0.09% (fps 31.1 / 38.6)**,
      `drain.tick` 1332 / 1356 ms per 5 s on vs 0 off, ring drops 0. No Lose screen in any arm: 4 `board.start`, 4 fills of
      300, zero `match.result` events across all four runs. Files `docs/research/perf/_baseline-lcw-300z-env-{on,off}-{a,b}.json`
      (`recentHits` stripped for size, noted in-file). **This reverses the T13 claim that the switch made no measurable
      difference: the 300z breach is this feature's own cost** (ceiling 6%); L-N1's default-off is the stop rule applied.
      Decision on the runtime toggle: withdraw, not fail closed — failing closed in `ShouldApplyRider` would also silence
      every other `OnDamageDealt` effect. `LawnBasicAttackGrantBinder.Tick` now withdraws every basic-attack grant on the
      switch's on→off edge (`FeatureSwitchEdge`, `BasicAttackGrantBuilder.IsBasicAttackGrantId`). Tests
      `tests/FusionRpg.Core.Tests/Combat/FeatureSwitchEdgeTests.cs` (edge mutant killed), guard
      `tests/FusionRpg.Guard.Tests/LawnBasicAttackSwitchOffWithdrawGuardTests.cs`; verify-change Core 13584/13584, Guard
      312/312, injector compile + boundary guards OK. Live (env unset, toggle on → 3 Peashooters on Adventure 2 → toggle
      off): on window `ActionTriggers` 2 / `RpgDeltaMergedHits` 2 / `DrainTickTotalMs` 54; log
      `lawn-basic-attack switched off: withdrew 8 bound grants`; off window 21 vanilla hits with `ActionTriggers` 0,
      `RpgDeltaMergedHits` 0, `DrainTickTotalMs` 0. The live build was 38369ff3 + this change (L-N34 blocks HEAD).*
- [x] **L-N35** Observer streams never join (found by L-N7): a vanilla hit record's swing id is the bullet's ptr, while the
      overlay packet's is `shooter:tick` with the shooter resolved from the bullet (`BulletShooterMatch`), so
      `RecordRpgDelta` never finds the vanilla record — live, 0 of 13 RPG records merged. Every per-hit comparison T13's
      proofs describe ("vanilla amount beside its RPG delta for the same swing") is therefore unobservable. Carry the
      resolved shooter on the vanilla record (or the bullet ptr on the packet) and join on it. Verify: a live run with
      `RpgDeltaMergedHits` equal to the RPG records whose vanilla hit landed in the same window, and a Core test for the join key.
      *Done 2026-09-15: the vanilla bullet hit now names the shooter the drain cached at the bullet's spawn
      (`EventDrainHost.TryPeekBulletShooter`, read before `TryRecordDealtFromBullet` consumes it) and keeps the bullet as its
      swing; `LawnCombatObserver.RecordRpgDelta` falls back to the oldest unmerged vanilla hit with the same normalized
      shooter+victim. Tests: `LawnCombatObserverTests.Rpg_delta_joins_the_oldest_unmerged_vanilla_hit_by_shooter_and_victim`
      (FIFO order, ptr casing, different victim never joined; join mutant killed), guard
      `tests/FusionRpg.Guard.Tests/LawnObserverShooterJoinGuardTests.cs`; verify-change Core 13592/13592, Guard 313/313, injector
      compile + boundary guards OK. Live (worktree build 38369ff3 + this session's changes, L-N34 blocks HEAD), Adventure 2, five
      placed Peashooters, feature on, 90 s (`docs/research/perf/_lawn-combat-observer-rpg-join-env-on.json`):
      `RpgDeltaMergedHits` 11, `RpgDeltaUnmergedRecords` 0; each row shows vanilla 2122 beside its delta and outcome
      (miss 0, hit −2833, crit −4326) for three distinct shooters. Found in passing: pooled bullet ptrs repeat inside one
      window (swing `2CCF8DEBD20` twice), so `TotalSwings` 9 < hits 11 there — added to L-N33's D8 note.*
- [ ] **L-N33** 300z drops (found by L-N9): with the feature on, `EventDrain` shed 35 and 362 records to the per-death
      flush budget in two 60 s runs (D9 says zero), and the observer's own ring dropped 4021 / 5541 records on and ~13k off,
      so its hit/swing/trigger counts are undercounts at 300z. Decide whether death-budget shedding breaks D9 or is the
      designed overload valve (then say so in the spec); size the observer ring from a measured 300z window or make its
      counters ring-independent. Also D8 at 300z: `ActionTriggers` 2764 vs `TotalSwings` 7376 — separate zombie bites from
      grant-holding swings (or the ring loss) before calling it a D8 breach. Also: the observer's swing identity is the bullet ptr, and Unity pools
      bullets, so one ptr can be two swings in the same window (L-N35 live run) — swings undercount by construction. Verify: a 300z run file whose
      `ObserverDroppedRecords` is 0, triggers reconciled against grant-holding swings, and a written D9 ruling.
- [ ] **L-N34** Boot crash on HEAD (found 2026-09-15, not this program's code): every build containing merge `8710d326`
      (`rift-gate-spec`) dies at startup with `0xc00000fd` (stack overflow, Windows Application log event 1000) right after
      MelonLoader's `LoaderInitialized`, before `MainMenu.Start`; 38369ff3 built from a clean worktree boots at 59.9 fps, and
      f6f5e6be (HEAD, without this session's uncommitted change) crashes. Suspect: `OverlaySwitch` in-process path now
      starts `OverlayViewHost` unconditionally (`if (!_viewStarted)` dropped `State.SettingsEnabled`). Owner: the
      rift-gate session. Verify: HEAD boots and reaches the main menu.
- [ ] **L-N2** Proofs 4/5 redo. Observer needs per-attacker counters or a committed hit sample. One
      real actor, no HP pin: an exhausted swing on attacker P with vanilla amount and no delta, then a
      later P hit with a non-zero delta (proof 4); P's `attackDamage`/`maxHp` while exhausted equal the
      Hub snapshot (proof 5).
      *2026-09-15 PROGRESS (not ticked): the committed hit sample now exists and names each RPG outcome. The earlier
      "alternating 0 / −2754" deltas are overlay misses, not exhaustion (`ExhaustionEvents` 0; five Peashooters over 90 s
      spent 325 stamina). Proof 4 still needs a real exhaustion inside a window, and a per-attacker view needs L-N35's join.*
- [ ] **L-N3** Proof 6 live: real Adventure board (confirm board type ≠ `Nothing` and no stale
      lab-overlay entities, screenshot), a `zombie.spawn source:"start"` ptr with a non-Neutral
      element and a rider delta sourced from it; falsifier vs a Bound specimen.
      *2026-09-15 PROGRESS (not ticked): a plain game-spawned Conehead (type 2, no `UniqueActor`, Adventure 2, no session) carried
      an RPG rider on its bite — vanilla 50, crit −264, Hub power 42 (`_lawn-combat-observer-bite-differential-env-on.json`).
      Its element, the `source:"start"` spawn and the Bound-specimen falsifier are still owed.*
- [ ] **L-N5** Proof 7 real falsifier: a deferred RPG overlay delta as the lethal hit in the same frame
      as a vanilla lethal hit, outside a debug session; log both `Zombie.Die` and `DestoryZombie`
      hooks firing; exactly one `zombie.die` per death over several deaths.
- [ ] **L-N4** Proof 3 multi-victim: identify a real multi-victim attacker in pvzrh-3.9 (piercing or
      splash plant) — research first; never the synthetic match-owned `combat-area-row` grant.
- [ ] **L-N6** Proof 2 Weak side: find the field that actually holds zombie X position (`theSpeed` did
      not), or use a real long lawn; capture Earth-vs-Ice alongside Fire-vs-Ice.
- [ ] **L-N7** Proof 1 second half: explain `rpgDelta:0` with the `derived:{"combat.power.omni":2000}`
      overlay first (trace `InjectorDerivedOverride` → `ResolveActor`), then the two-shooter differential.
      *2026-09-15 PROGRESS (not ticked — the `combat.power.omni:2000` override scenario and the two-shooter differential are
      still owed): the observer could not tell a miss from a zero-damage hit, and its `RpgDeltaMergedHits` counted every RPG
      record before checking the merge. Fixed: each RPG record carries `RpgOutcome` (miss/parried/blocked/crit/hit from
      `OverlayCombatBreakdown`), and the snapshot splits `RpgDeltaMergedHits` / `RpgDeltaUnmergedRecords` and adds `RpgMisses`
      (`LawnCombatObserver.cs`, bridge, tool; Core tests + `RunAggregatorRpgCounterTests`; merged-before-check mutant killed;
      verify-change Core 13591/13591, Guard 312/312, observer 4/4). Live, real Adventure 2, five placed Peashooters, feature on,
      no session, 90 s (`docs/research/perf/_lawn-combat-observer-rpg-outcomes-env-on.json`): 13 RPG records, **every zero
      delta is a miss** (miss 3 → delta 0; crit 9 and hit 1 all non-zero; zero non-miss zeros). A miss comes from
      `OverlayCombatCalculator`'s hit roll: `pHitFinal = Sigmoid(accuracy − dodge)`, which is 0.5 with both channels at 0, so
      untuned actors miss about half their riders by construction. RPG records merged into vanilla records: 0 of 13 (L-N35).*
      *2026-09-15 PROGRESS 2 (not ticked): after L-N35's join, the falsifier half reads cleanly — two different Peashooter
      ptrs with the same Hub power (`debug.actor-derived`: `combat.power.omni` 853, `combat.accuracy.omni` 60.1) gave identical
      per-outcome deltas for the same vanilla 2122 (crit −4326 from `2CCF8B8FD80` and `2CCF8D3A480`, hit −2833)
      (`_lawn-combat-observer-rpg-join-env-on.json`). The positive half (two attackers of **different** Hub power against the
      same victim) is blocked on this save: every Adventure picker (levels 2, 10, 30 inspected) offers only Sunflower and
      Peashooter, so no second shooter species can be placed through a real card, and player 1's only shooter-species
      allocation is Peashooter's. A zombie-bite variant (Sunflower victims) produced two bites in 120 s — too few
      (`_lawn-combat-observer-bite-differential-env-on.json`: Conehead `2ccf8d66640`, Hub power 42, vanilla 50, crit −264).
      Routes left: a Bound specimen with its own allocation beside a general Peashooter (live-probe Task 15's path), or a
      save with more plants unlocked. No debug derived override — that input would be fabricated.*
- [x] **L-N15** Gate hygiene: per-commit file-list review of the T3–T7 commits; mutant set in
      `scripts/mutants/` for `LawnElementResolver`, `OverlayCombatCalculator`, `EventDrain` with
      killed-mutant output; replace self-asserted "lead read the diff" boxes with commit hashes.
      *Done: (1) per-commit file-list review recorded with hashes on GATE 1/2/3's three "lead read the diff" boxes
      (T3–T12 extras each justified; T11's shared-seam regen change became L-N28). (2) `scripts/mutants/lawn-combat.json`,
      21 mutants (`LawnElementResolver` 6, `OverlayCombatCalculator` 7, `EventDrain` 8). First run: 1 real survivor —
      `FlushForPtr` re-appending kept records in reverse order; the only order test left one record behind. Added
      `EventDrainTests.FlushForPtr_leaves_every_other_ptrs_records_in_their_original_order`. Re-run: `every mutant was
      caught` (21/21), and a separate build per mutant proved all 21 compile, so no catch is a build failure. Command:
      `mutate.ps1 -Set lawn-combat -Filter "...LawnElementResolver|...OverlayCombat|FusionRpg.Core.Tests.Events|...DamagePacket|...ShieldGate|...EvasionChain"`.*
- [ ] **L-N26** Live-tooling defects seen in the run: `lab-overlay` reports `theBoardType:"Nothing"`;
      `enter-level` left stale entities under a main-menu overlay; a kill batch produced no
      `zombie.die` events and no soul credit on the same `matchKey`; `debug_restart_game` ran 30 min
      with no response. Each needs a reproduction and an owner.
      *audit 2026-09-15 PROGRESS (not ticked — `debug_restart_game` hang and kill→soul gap still unreproduced):
      (a) screenshots: every capture stuck "already in flight" because OnGUI never delivers a Repaint on this host;
      `ScreenshotRunner.TickFallback` now captures by camera render after 2s and reports `repaintsSeen` (live: PNGs return,
      `primitive: camera-fallback-no-repaint`). (b) seed-picker: `debug.skip-setup` fired before the picker existed and
      acked ok on a blocked level; it now waits for the live StartGameButton, picks seed packets into the bank
      (`CardUI.OnMouseDown` on the "(Clone)" packet), presses start and acks only after `InitBoard.ready` — live 3/3 cold
      cycles, and quick-start treats a failed skip as terminal. (c) stale board: `ui-nav back-to-menu` left the Board alive
      (mowers over the menu, `enter-level` refused "board already live"); new `POST /api/debug/leave-board` presses
      menu → 主菜单 → 确定 (owner-specified) and acks after `Board.OnDestroy` — live 4/4, re-entry accepted every time.
      (d) game-state now reports `initBoardReady`/`timeScale`/`gameTime`; it still labels the seed-picker `InMatch`.*
      *audit 2026-09-15 PROGRESS 2 (still not ticked — `debug_restart_game` hang and the earlier kill→soul gap not reproduced): (e) lab contamination fixed — a lab scenario's wave-freeze / attack x0 / zombie count x0 / silenced vanilla outlived its session; `DebugRuntime` now snapshots user-set cheats at session start and restores + reapplies at end (live: `cheatsRestored: 13`, F-WAVE-FREEZE back to false), and quick-start starts the session before its own freeze. (f) the server's session mirror read inactive after a server restart while a lab ran; Hello now carries `debugSessionActive`/`debugScenarioId` (live: mirror matched after end). (g) level 1-1 never starts waves under the tutorial caption — probes must use level 2+. (h) `debug.act place` during the battle-start pan emitted `debug.act.done` with no plant placed (L-N30).*
- [ ] **L-N27** Liveness on pooled reuse (found by L-N16): a dead mark now clears only on a real spawn
      hook. A pooled entity reactivated without `Start()`/`InitHealth()` (T10's frozen-wave replacement
      plant) at a dead-marked ptr still refuses RPG hits, and a pooled zombie skips `NoteZombieDead`.
      Live: log `EntityLiveness.DeadCount` and pooled-reactivation ptrs across a long wave; if a
      reactivated ptr is dead-marked, find the engine's reactivation hook — never clear from resync,
      which also sees dying objects. Also confirm `Zombie.InitHealth` never fires on a dying zombie
      between `Die` and `DestoryZombie` — that would re-open the once-per-death latch and emit a second
      `zombie.die`.
- [x] **L-N28** Cross-mode regen check (found by L-N15): T11 `c30275e5` made `BattleModels.BaseResourceRegen`
      non-zero through `ResourceBaselineSubsystem`, shared by every `seedResourceBaseline: true` Hub caller
      (lawn `CheatState.ActorHub`, `UniqueActorHubCompose`). List every such caller, confirm which modes
      now regenerate stamina, and confirm battle/expedition goldens did not move (or that the move is
      the calibration spec's intent). Verify: caller list with file:line + the golden test run output.
      *audit 2026-09-15 FINDINGS (left open — owner decision): `ResourceBaselineSubsystem` (the only caller of
      `BaseResourceRegen`) is registered by three composes — lawn `CheatState.cs:81`, `/sheet`
      `UniqueActorHubCompose.cs:75`, and battle `BattleHubCompose.cs:47`. So since `c30275e5` production battle actors
      regenerate stamina at 5% of pool per second (`battle-resources.v2.json`). `spec-resource-subtick.md` sanctions
      regen rows "in both battle and the lawn", but `BattleModels.cs:395-417` still says in-battle regen stays 0 because
      "a battle is not a rest" (resource-hub-ssot.md §11: pools "refill at rest"), and no test reads the real v2 numbers
      in battle (the ambient fixture is all-zero). Server sheet/audit tests pass (9/9, filter
      `ActorSheet|DerivedAudit|ProjectStanding|UniqueActorHub`); Core battle goldens pass (13572). Owner rules: battle
      stamina regen on (amend §11 and the BattleModels doc, add a battle test on real v2 numbers) or lawn-only (regen
      opt-in per compose, battle off).*
      *Done: owner ruled "battle regen on" (2026-09-15). `resource-hub-ssot.md` §11 amended (authored regen rows also regenerate during an encounter; pools still not refilled per encounter); `BattleModels.BaseResourceRegen` doc records the decision; explicit-tuning overloads + optional `ResourceBaselineSubsystem` tuning param let `BattleStaminaRegenTests` (3) read the real `battle-resources.v2.json` without mutating ambient state: battle compose seeds the seam, only stamina regenerates, a spent battle pool recovers `min(max, ticks*rate/1000)`. Mutant (subsystem ignores explicit tuning) fails 2 of 3.*

- [x] **L-N29** Event loss on level entry (found 2026-09-15): twice, on quick-start's path, the injector logged
      `debug.enter-level Advanture#1` but that frame's `debug.level.enter` and `board.modifiers` events never reached the
      server while neighbouring events (43.597, 43.604) did. quick-start now confirms entry via game-state and returns
      `levelEnterAckMissing:true`, which masks nothing but does not explain it. Reproduce with the event ids around
      `debug.level.enter`, trace `DebugRuntime.Emit` → `GameHooks.Emit` → `RpgClient` batching/dedupe → `EventIngest`,
      and fix the drop. Verify: 5 quick-starts from the main menu with `levelEnterAckMissing:false`.
      *Done 2026-09-15. Root cause is wider than the symptom: **no `board.start` had been stored since run 52
      (2026-09-07 02:01)** — runs 53–102 all had null `levelType`/`boardLevel`/`modifiers`. `GameHooks.Emit` enqueues an
      event only after `MatchHost.Apply`/`EffectRuntime.OnCapture`, which emit `cheat.apply`/`debug.effect.cleared` under the
      new matchKey first. Those reach `RpgStore.InsertOneUnlocked` ahead of `board.start` and self-heal the run (the
      2026-09-07 orphan fix; run 102's `startedUtc` equals its `cheat.apply` t); `board.start` then INSERTed a second row,
      violated `ix_runs_match_key`, and the transaction rolled back its whole writer batch — neighbours such as
      `debug.level.enter`/`board.modifiers` went with it whenever they shared that batch. Fix: (1) `board.start` for a
      matchKey that already has a run fills that run's null metadata (`FillRunStartMetadataUnlocked`, COALESCE; a repeated
      board.start keeps the first values); (2) `EventIngest.InsertIsolatingFailures` re-inserts a failed writer batch one
      event at a time, so a poison event costs only itself (counted in `DroppedEvents`, logged). Tests:
      `tests/FusionRpg.Data.Tests/RunStartAfterSelfHealTests.cs` (3; all failed first with `UNIQUE constraint failed:
      runs.match_key`), `tests/FusionRpg.Server.Tests/EventIngestIsolationTests.cs` (2; isolation mutant killed).
      `WebMatchStoreTests.Web_insert_is_one_transaction` used a duplicate board.start as its poison; the atomic-insert contract
      is unchanged and the poison is now an unserializable payload. verify-change: Data 1236/1236, Server 446/446, dal and
      test-substrate guards OK. Live after republishing the Server: 5 quick-starts from the menu, each
      `levelEnterAckMissing:false`, `board.start` stored once, runs 103–107 `levelType=Advanture boardLevel=2 modifiers` set,
      server stderr empty; quick-start went from ~9.4 s to ~5.1 s. Not changed: the injector still enqueues side-effect
      events ahead of their cause — the store now tolerates that order, and L-N31 tracks the order itself.*
- [x] **L-N31** Capture order (found by L-N29): `GameHooks.Emit` enqueues `kind` after `MatchHost.Apply` and
      `EffectRuntime.OnCapture`, so any event those emit (`cheat.apply`, `debug.effect.cleared` on `board.start`) lands on
      the wire before its cause. The store tolerates it since L-N29, but every consumer that reads the feed in id order
      (web lawn, probes, `UniqueActorService.ObserveEvents`) sees effect before cause. Decide whether to enqueue first
      (check `lifecycleOccurrence`/`activeMatchMs` stamping still precedes the enqueue) and add a guard test on the order.
      Verify: a quick-start's first event under the new matchKey is `board.start`.
      *Done 2026-09-15. Worse than ordering: `MatchHost.Apply` clears `GameHooks.MatchKey` on `match.result` and
      `board.end`, and the enqueue read `MatchKey` afterwards — so both went out keyless, and **no live run was ever closed
      or given a result** (1 of the last 100 runs had `endedUtc`, and that one was a web match). Fix in `GameHooks`:
      enqueue right after the payload stamps and before `MatchHost.Apply`/`OnCapture`; the wire key is
      `MatchKey ?? _runKey`, where `_runKey` lives from `Board.Awake` until `board.end` is emitted (events after
      `match.result` still belong to the board's run). Guard: `tests/FusionRpg.Guard.Tests/CaptureEnqueueOrderGuardTests.cs`
      (2; mutant with the enqueue moved back after the side effects fails). verify-change: Guard 297/297, injector compile,
      single-writer/funnel/actor-hub/secondary guards OK. Live after redeploy: 3 quick-starts, first events under each key
      `board.start, cheat.apply, debug.effect.cleared`; leave-board emitted `board.end` with the key and runs 108–110
      closed. A real loss on Adventure 2 (no plants, no debug session): `match.result defeat` keyed `73992f5c`, events after
      it keyed, soul ledger `defeat 25` on run 111, and after the lose menu's `backtomenu` `board.end` 334151 keyed —
      run 111 `endedUtc` set, `result=defeat`. Found in passing: `debug.leave-board` cannot leave the lose screen (its
      `open-menu` stage times out; the LoseMenu has its own `backtomenu`) — L-N32.*
- [x] **L-N32** `debug.leave-board` from the lose screen (found by L-N31): with `CanvasUp/LoseMenu(Clone)` up, the
      leave machine waits 20 s on `open-menu` and returns 409. Press `LoseMenu(Clone)/backtomenu` when present (the
      same control a player presses), then wait for the board to be destroyed. Verify: a real loss, then
      `POST /api/debug/leave-board` returns ok with `stage:"left"` and `board.end` is stored under the run's key.
      *Done 2026-09-15: `DebugLeaveBoard` stage 0 presses the lose menu's `backtomenu` first and goes straight to waiting for
      the board to be destroyed. Guard `tests/FusionRpg.Guard.Tests/DebugLeaveBoardLoseScreenGuardTests.cs`; verify-change
      Guard 298/298, injector compile and boundary guards OK. Live after redeploy: real Adventure 2 loss (200 s, no plants),
      `leave-board` → `{"loseMenuMs":609,"ok":true,"stage":"left","waitedMs":1218}`, `board.end` 334780 keyed `0a5ddf85`,
      run 112 `endedUtc` set and `result=defeat`, game-state `hasBoard:false`.*
- [x] **L-N30** `debug.act place` reports done without a placement (found 2026-09-15): issued ~6s after the
      setup skip, it emitted `card.place type:-1` and `debug.act.done` but no `plant.place`/`sun.spend`; the same call
      seconds later placed the plant. The verb must confirm its expected kind (a `plant.place` at the target cell) or
      report a refusal naming the stage. Verify: a place issued during the start pan returns a failure, not done
      *Done: `ControlAct.DoPlace` reads the target cell before and after `TryToSetPlantByCard` and decides the receipt by a new live plant of the card type (`ok`/`placed`/`plantPtr`, or `error`); the MCP `debug_act` tool now honours the receipt verdict. Live: first place into (2,2) `ok:true plantPtr 26F78BD3480`; a second place into the same occupied cell `ok:false placed:false` with the reason. Tests: Guard `ControlActPlaceConfirmGuardTests`, debug-mcp `test_act_place_receipt_failure_is_not_ok` (6/6). verify-change: Guard 295, injector-compile + single-writer OK.*.