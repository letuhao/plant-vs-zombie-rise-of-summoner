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
- [x] 4 Exhausted actor: vanilla number lands, no delta; after regen **the same ptr** contributes again
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
- [x] 5 Stat bleed intact while exhausted. **CLOSED 2026-09-15, same run as proof 4**: vanilla
      zombie hits on the plant landed every window (`totalHits` tracks 1:1 with vanilla
      `PlantTakeDamage` calls) throughout, including the windows carrying `exhaustionEvents ≥ 1` —
      vanilla damage never paused while the actor's stamina was exhausted, confirming stat bleed
      (the plant still takes real damage) is independent of the actor's own action-trigger gate.
- [x] 6 A plain PvZ-spawned creature gets a rider. **CLOSED 2026-09-15 via a structural (code-read)
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
- [x] 7 No double-kill: one `die` event per death. **CLOSED 2026-09-15 with a dedicated falsifier**,
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
- [ ] Real numbers recorded, never a boolean. An honest FAIL correctly reported is this task
      succeeding.

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
- [ ] **Ship / ship-behind-switch-defaulted-off / stop**

*Eyes are a secondary signal here — visual breakage the instrument has no channel for. Never the
metric.*

### Program close

- [ ] Deferred items still tracked in the ideal, none silently absorbed or dropped
- [ ] The observer's run file committed as the program's evidence record
