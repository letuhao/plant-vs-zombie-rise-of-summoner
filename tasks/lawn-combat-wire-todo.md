# Todo: `lawn-combat-wire`

**Plan:** [lawn-combat-wire-plan.md](lawn-combat-wire-plan.md) · **Map:**
[../docs/architecture/lawn-combat-wire-map.md](../docs/architecture/lawn-combat-wire-map.md)

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
- [ ] Collects, per hit: attacker ptr, victim ptr, swing id, vanilla amount, RPG delta, both elements,
      matchup relation.
- [ ] Aggregates per run: hits, swings, **action triggers** (D8 is *measured*: triggers == swings,
      never victims), stamina spent, regen accrued, exhaustion events, **dropped-record counters**
      (D9 says zero — the counter is the proof), frame-share sample under a 300z wave.
- [ ] **Runs with the feature in its shipped configuration** — no debug session, no `SessionMode`, no
      flag flipped to observe. A test or assertion proves the collection path does not alter
      `EventDrainHost.Active`.
- [ ] Emits machine-readable output (a run file), not console prose — so a gate can diff two runs.
- [ ] Reports **"no data"** distinctly from **"zero"**. A silent empty run is the failure mode this
      whole program exists to eliminate.
- [ ] Works before the feature exists: run against today's build it reports vanilla hits with zero RPG
      delta — that is the **baseline**, and it is captured in this task.

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
- [ ] Recorded in `spec-lawn-combat-calibration.md` as (a) needs an authored amount, or (b) reads the
      event's own damage.
- [ ] If (b): the spec states explicitly that **no magnitude is authored**, so nobody adds one later
      "for completeness".

**Verify:** the answer cites the deciding file:line.
**Dependencies:** none · **Files:** `spec-lawn-combat-calibration.md` · **Scope:** XS

---

### Task 2: Decide the lawn cost-delivery shape

**Description:** The hand-built `act.attack` row carries `Costs: Array.Empty` by construction, authored
costs reach the store via SQLite, and the injector csproj does not copy `data/seed/actions/**`. Three
shapes are named in `spec-lawn-action-bridge.md`.

**Acceptance:**
- [ ] One shape chosen and recorded in `spec-basic-attack-cost.md`.

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
- [ ] Per-ptr invalidation on side change; hypno resolves the new side next read.
- [ ] Trigger 3 (ptr reuse) has an **executable** test: resolve P → A, kill, re-register a different
      species at P, resolve → not A.
- [ ] Trigger 4 (catalog revision mid-run) either has an invalidation test or a test proving it cannot
      fire.
- [ ] No whole-cache clear on any per-entity path.

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
- [ ] `long`/per-mille interior; widen before multiplying; divide by 1000 last, exactly once.
- [ ] Overflow **throws**; a test asserts it.
- [ ] The Unity-boundary narrowing throws **or reports**, with a comment naming it a structural host
      limit.
- [ ] A **source-scan test** proves no `double`/`float` remains in `OverlayCombatCalculator.cs`,
      `ElementHub.cs`, `OverlayCombatMath.cs`.
- [ ] **D1 guarded:** a test asserts `MergeAppliedCombat` (`ActorHub.cs:89-113`) folds only
      `progression.bonus.*` and **no `combat.*`**.
- [ ] **Existing goldens unchanged** — representation change only.

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
- [x] Integer-only; no `double`/`float`.
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
- [ ] Attacker = `bullet.from` / `from_zombie`; melee attacker unchanged.
- [ ] **Swing id** recorded: `bullet.Pointer` for projectiles, `(attackerPtr, frame)` for melee —
      reusing `_meleePairsByTarget`/`_meleePairsFrame`, not a second structure.
- [ ] **The attacker's own power reaches the packet**, asserted as a differential: two shooters of
      different composed power produce different `combat.power.*`. *(Not "isn't the stub" — that
      passes even when broken.)*
- [ ] A null shooter ⇒ no RPG contribution, no exception, no fallback to the bullet ptr.
- [ ] **The four uncaptured attack methods are hooked**: `QingZombie.AttackPlant` (override),
      `QingZombie.AttackPlants()`, `EternalZombie_a.AttackPlants()`, and the plant-side
      `Shulkflower.AttackEffect(List)` / `WaterShulk.AttackEffect(List)`.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain"`
**Dependencies:** none · **Files:** `EventDrainHost.cs`, `EventDrain.cs`, `GameEventRec.cs`,
`GameHooks.cs` · **Scope:** M

---

### Task 7: `basic-attack-seed`

**Description:** Load the fallback basic attack from authored seed data instead of the hardcoded C#
row, so its cost becomes config. Seed already authored: `data/seed/actions/authored-basics.json`.

**Acceptance:**
- [ ] `ActionCorpusBriefJson` parses `kindHint`; an unknown value is rejected naming the brief id.
- [ ] `ActionCorpusComposer` honours it instead of hardcoding `Kind = ActionKind.Skill` (`:144`);
      absent ⇒ `Skill` (back-compat).
- [ ] Kind-aware cost: Basic → `stamina`, Innate → `qi`, Category as fallback.
- [ ] `Program.cs`'s loader includes the authored file.
- [ ] **All 179 existing briefs import unchanged** — no Kind or cost drift.
- [ ] No `committed-round-*.json` modified.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCorpus"`;
`dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionCorpusImporter"`
**Dependencies:** none · **Files:** `ActionCorpusBriefJson.cs`, `ActionCorpusComposer.cs`,
`Program.cs`, cost template · **Scope:** M

---

## GATE 1 — lead agent, after Tasks 3–7

**Lead re-runs every command itself. A worker's report is a claim, not evidence.**

- [ ] Lead re-ran all four test commands and read the real output; `audit-overflow.py` clean
- [ ] **No golden moved** in T4 or T5 — lead diffed the golden files directly
- [ ] Battle behaviour byte-identical (T5)
- [ ] Lead read each diff: files changed are the files the task named, nothing else moved
- [ ] `git status` on shared files (`GameHooks.cs`, `Program.cs`) clean of other sessions' work
- [ ] Negative cases exist — each task naming a falsifier has a test that fails when the code is wrong
- [ ] Each of T3/T4/T5 independently shippable — none silently depends on another
- [ ] Observer baseline from T0 still reproduces (the ruler did not drift under these changes)

---

## Phase 2 — The path

### Task 8: `lawn-action-bridge`

**Description:** `act.attack` is hand-built at `BattleRunState.cs:64-81` (no rung, no container, no
atoms). Promote it to one public factory both callers share, and configure the timing policy
injector-side — without which the first touch **throws**.

**Acceptance:**
- [ ] One public factory in `Core.Actions`; `BattleRunState` uses it; a source scan proves no second
      construction site.
- [ ] A field-level golden proves the extracted row is identical to today's.
- [ ] `ActionTimingPolicy.Configure` runs in the injector host **before any grant is bound**, ordered
      against host startup, not raced.
- [ ] A failed construction is a **loud one-shot diagnostic**, never a silent skip and never a per-hit
      throw.
- [ ] No HTTP/SignalR/SQLite on the construction path (source-scan test).

**Verify:** `.\scripts\guard-single-writer.ps1`; `.\scripts\guard-funnel-delta.ps1`
**Dependencies:** T7 · **Files:** `Core/Actions/` (new factory), `BattleRunState.cs`, injector host
init · **Scope:** M

---

### Task 9: `lawn-hit-entry` — the safety rules

**Description:** The vanilla hit → drain → `DamagePacket` path, keyed on the **board fold** (not the
FSM — a general creature has no binding). Owns the four gates and every correctness rule that makes a
*deferred* per-hit pipeline safe. **Must land before T10.**

**Acceptance:**
- [ ] One swing id ⇒ **one** action trigger, N damage applications (D8).
- [ ] An effect-bearing hit is **never dropped** under budget exhaustion — carried and coalesced (D9);
      `event-pipeline-v2-ssot.md` amended to describe the class.
- [ ] Coalescing preserves total damage.
- [ ] A dead/dying target absorbs no delta and triggers **no second `Die()`**.
- [ ] Records for a ptr drain **before** that ptr's grants withdraw, including from a nested drain.
- [ ] "Instakill-shaped" is **defined** — prefer the engine's own `DamageType` (`Squash`, `MaxDamage`,
      `Crash`, `RealDamage`) over a magnitude threshold — and produces no proportional rider.
- [ ] A general creature takes the **same code path** as a Bound specimen: no branch excludes an actor
      for lacking a binding.
- [ ] The stale `EventDrainHost` class-header comment ("Off (default until Task 10…)") corrected.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain|DamagePacket"`;
`guard-funnel-delta`, `guard-single-writer`, `guard-actor-hub`
**Dependencies:** T6, T4 · **Files:** `EventDrainHost.cs`, `GameHooks.cs`, `DamagePacketBuilder.cs`,
`EventDrain.cs` · **Scope:** L

---

## GATE 2 — lead agent, after Tasks 8–9

- [ ] Lead re-ran the guards; green
- [ ] Safety rules verified **without** a real grant (synthetic fixtures) — T9 must stand alone
- [ ] T9's rules proven before T10 makes them load-bearing — **this gate is the ordering constraint**;
      the lead does not dispatch T10 until it passes
- [ ] Lead read the actual diff, not the summary
- [ ] `ActionTimingPolicy.Configure` ordering verified against host startup, not assumed (T8)

---

## Phase 3 — Make it fire

### Task 10: `basic-attack-grant` ⚠ strictly after Task 9

**Description:** Bind `ActionKind.Basic` to every lawn actor at spawn so `HasOnDamageDealtGrant()` is
true. The atom already exists purpose-built: `atom.fx-overlay-damage`, `kind: resource.delta`,
`trigger: OnDamageDealt`.

**Acceptance:**
- [ ] Every spawned actor holds the grant, keyed on the board fold; general creatures included.
- [ ] `elementPayload` baked from the owner's species element, sourced at bind from
      `LawnElementResolverHost.Resolve(ptr)`.
- [ ] **Hypno re-bake:** a side change re-bakes or re-binds the grant — invalidating the resolver cache
      alone leaves a stale baked payload. A hypnotised zombie deals damage with its **new** element.
- [ ] Grants withdraw **before** ptr reuse; a test recycles an address.
- [ ] No per-actor push storm on a mass spawn.
- [ ] **A feature kill switch** disables grant-binding and cost-charging **together**; with it off,
      behaviour is byte-identical to today.

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
- [ ] **`resource.max.stamina` is non-zero for a lawn actor** — check this first; max 0 is
      indistinguishable from the bug being fixed (`seedResourceBaseline: true` for the injector Hub).
- [ ] One payment per swing regardless of victim count.
- [ ] At zero stamina: the pea still flies, **no** elemental delta, and the stat bleed intact.
- [ ] Regen restores the **same actor instance** and the next swing contributes again.
- [ ] Lawn pool lifecycle stated (match-scoped, full at spawn) or `resource-hub-ssot.md` amended.
- [ ] Where `CostLedger.Check` is called on the lawn is specified, and its position relative to the
      swing dedupe.
- [ ] **Zombies**: confirmed able to hold and spend `stamina`, or exempt with a stated reason.
- [ ] One cost authority — no second gate; `guard-actor-hub` green.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostLedger|ActorResourcePools"`
**Dependencies:** T2, T5, T8, T9, T10, T11 · **Files:** `CheatState.cs`, `KernelDriveHost.cs`, cost
call site · **Scope:** L

---

## GATE 3 — lead agent, after Tasks 10–12

- [ ] Kill switch verified **by running with it off**: byte-identical to today, not asserted
- [ ] **Pool max non-zero** — the anti-silent-inert check, run before anything else in this gate
- [ ] No second cost gate; `guard-actor-hub` green
- [ ] Lead read the actual diff and test output
- [ ] **Observer reports triggers == swings** on a piercing shot (D8 measured, not argued)
- [ ] **Observer reports zero dropped effect-bearing records** under a loaded wave (D9 measured)

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
      2026-09-15 (the fifth-defect fix above). **Second half not done**: "composed power differs from
      a weaker shooter's" needs two live runs with different `derivedProfile` values on the plant
      (`debug.spawn-plant`'s own param, already used by this scenario) against the same defender,
      comparing `rpgDelta` magnitude. Not attempted this session.
- [ ] 2 Element: **one species, two element assignments**, against a defender proven **non-Neutral**
      and Strong to one / Weak to the other, with the expected ratio computed from
      `stats.v1.json:10 matchupShareK` **before** the run. Falsifier: the same species against a
      Neutral defender must produce **equal** damage. *Not attempted — every run so far used the
      scenario's default Earth/Earth (Same matchup) pairing, real numbers (`rpgDelta=-53`/`-33` etc.,
      2026-09-15) but not the two-assignment comparison this proof specifies.*
- [ ] 3 One swing, one trigger — N victims still each take damage. *Partially evidenced: every swing
      this session produced exactly one trigger (2026-09-15, `actionTriggers` now tracks real swings
      1:1). Not evidenced: the "N victims" half needs a piercing/multi-target weapon (e.g. a
      Threepeater-shaped bullet hitting several zombies in one swing) — every live test so far was
      single-target.*
- [ ] 4 Exhausted actor: vanilla number lands, no delta; after regen **the same ptr** contributes again
      (never a respawn — pools are full at spawn). *A real exhaustion event fired unforced
      (2026-09-15, `exhaustionEvents=1`), but the plant died to the zombie's own vanilla attack (187
      dmg vs 300 HP, ~2 hits) before a regen-then-recontribute window could be observed on the same
      ptr — needs either a longer-lived actor (buffed HP) or a weaker opposing zombie.*
- [ ] 5 Stat bleed intact while exhausted. *Not attempted — depends on proof 4's own setup.*
- [ ] 6 A plain PvZ-spawned creature gets a rider. *Not attempted — `lab-overlay` spawns both sides
      via `debug.spawn-plant`/`debug.spawn-zombie`, never a real wave. Needs a non-`lab-overlay`
      scenario with waves NOT frozen, or a real Adventure playthrough.*
- [ ] 7 No double-kill: one `die` event per death. *Supporting evidence only: every `plant.die`/
      `zombie.die` this session carries a unique `(ptr, lifecycleOccurrence)` pair, no duplicates
      found (2026-09-15, 17 death events surveyed) — but this was passive observation, not a
      dedicated falsifier test deliberately trying to trigger a double-kill (e.g. overlapping AOE +
      basic attack lethal in the same frame).*
- [ ] **Perf: fresh baseline with the trigger-mask ON.** Ceiling **≤ 6% frame share at 300z**. On
      breach the feature ships behind the kill switch **defaulted off**. *Not attempted — every
      measured `drainTickFrameSharePercent` so far is from a 1v1 lab-overlay board (0.04%-0.33%
      observed 2026-09-15), nowhere near the 300z ceiling this proof requires.*
- [ ] Real numbers recorded, never a boolean. An honest FAIL correctly reported is this task
      succeeding.

**Status after the fifth-defect fix (2026-09-15): the blocking defect that made every one of these
proofs read zero/inert is resolved and live-proven. The task itself is NOT closed** — proof 1 is
half done, proof 3 is half done, proof 7 has supporting (not dedicated) evidence, and proofs 2/4/5/6
plus the perf ceiling are not attempted. Each remaining proof is a genuine, separate live-setup task,
not a rerun of what already ran.

**Every proof above is read from the observer's run file (T0), not from console output, not from a
worker's report, and not from anyone's eyes.**

**Verify:** `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe`;
`.\scripts\deploy-play.ps1 -NoServer`; `Invoke-RestMethod http://127.0.0.1:5088/health`;
observer run; `.\scripts\probe-perf.ps1 -Scenario <id> -DurationSec 60`
**Dependencies:** all, incl. **T0** · **Files:** none (operational) + `docs/research/perf/` +
the observer run file · **Scope:** M (real-time, not compute-bound)

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
- [ ] **Ship / ship-behind-switch-defaulted-off / stop**

*Eyes are a secondary signal here — visual breakage the instrument has no channel for. Never the
metric.*

### Program close

- [ ] Deferred items still tracked in the ideal, none silently absorbed or dropped
- [ ] The observer's run file committed as the program's evidence record
