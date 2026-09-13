# Todo: `lawn-combat-wire`

**Plan:** [lawn-combat-wire-plan.md](lawn-combat-wire-plan.md) · **Map:**
[../docs/architecture/lawn-combat-wire-map.md](../docs/architecture/lawn-combat-wire-map.md)

---

## Phase 0 — Read before building (tasks, not gates)

Two questions that change what later tasks do. Both are answerable today; neither blocks on anyone.

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
- [ ] Per-mille accumulation with a carried `long` remainder; divide by 1000 exactly once.
- [ ] **No drift** over ≥10,000 ticks: accrued == `floor(rate × ticks / 1000)`.
- [ ] A capped pool discards overflow **and** the carry.
- [ ] Integer-only; no `double`/`float`.
- [ ] `BaseResourceRegen` still returns 0 — this makes rates *expressible*, it does not author them.
- [ ] **Battle byte-identical** while regen rows remain absent.

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

## Checkpoint 1 — after Tasks 3–7

- [ ] All four test commands green; `audit-overflow.py` clean
- [ ] **No golden moved** in T4 or T5
- [ ] Battle behaviour byte-identical (T5)
- [ ] `git status` on shared files (`GameHooks.cs`, `Program.cs`) checked clean of other sessions' work
- [ ] Each of T3/T4/T5 is independently shippable — confirm none silently depends on another

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

## Checkpoint 2 — after Tasks 8–9

- [ ] Guards green; the safety rules are testable **without** a real grant (synthetic grant fixtures)
- [ ] T9's rules verified before T10 makes them load-bearing — this is the ordering constraint
- [ ] Reviewer confirms the actual diff, not the summary

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

---

### Task 11: `lawn-combat-calibration`

**Description:** Author the numbers, **derived** from shipped anchors with the arithmetic recorded.
Calibration, not balance.

**Acceptance:**
- [ ] T1's answer applied: magnitude authored, or explicitly not authored.
- [ ] `stamina` cost and lawn regen authored, satisfying `cost ≤ regenPerSecond × 1.5 s` at the pin
      (a Peashooter's `thePlantAttackInterval` is 1.5).
- [ ] **Exhaustion stays reachable** under burst fire — otherwise T13 proof 4 cannot run.
- [ ] Every value marked `UNMEASURED` and traceable to a named anchor.
- [ ] Each target tuning file's own `_meta.rebalance` convention followed (several forbid hand-edits
      and require `tools/tuning/publish.py`).
- [ ] **No test pins an exact damage number** — that is a reading, not a contract.

**Verify:** `dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"`;
`python tools/tuning/resource_ownership.py --check`
**Dependencies:** T1, T5, T7 · **Files:** `battle-resources.v1.json`, cost template · **Scope:** S

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

## Checkpoint 3 — after Tasks 10–12

- [ ] Kill switch verified: off ⇒ byte-identical to today
- [ ] Pool max non-zero (the anti-silent-inert check)
- [ ] No second cost gate; `guard-actor-hub` green
- [ ] Reviewer confirms the actual diff and test output

---

## Phase 4 — Prove it

### Task 13: `lawn-combat-live-proof`

**Description:** Run the real proof on a real board, **outside a debug session**. Produces no source.

**Acceptance — seven proofs, each with its falsifier:**
- [ ] **Not inside a debug session** — confirmed, not assumed (a stamped `scenarioId` is the tell).
- [ ] 1 Attribution: recorded attacker is the firing plant, and its composed power differs from a
      weaker shooter's.
- [ ] 2 Element: **one species, two element assignments**, against a defender proven **non-Neutral**
      and Strong to one / Weak to the other, with the expected ratio computed from
      `stats.v1.json:10 matchupShareK` **before** the run. Falsifier: the same species against a
      Neutral defender must produce **equal** damage.
- [ ] 3 One swing, one trigger — N victims still each take damage.
- [ ] 4 Exhausted actor: vanilla number lands, no delta; after regen **the same ptr** contributes again
      (never a respawn — pools are full at spawn).
- [ ] 5 Stat bleed intact while exhausted.
- [ ] 6 A plain PvZ-spawned creature gets a rider.
- [ ] 7 No double-kill: one `die` event per death.
- [ ] **Perf: fresh baseline with the trigger-mask ON.** Ceiling **≤ 6% frame share at 300z**. On
      breach the feature ships behind the kill switch **defaulted off**.
- [ ] Real numbers recorded, never a boolean. An honest FAIL correctly reported is this task
      succeeding.

**Verify:** `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe`;
`.\scripts\deploy-play.ps1 -NoServer`; `Invoke-RestMethod http://127.0.0.1:5088/health`;
`.\scripts\probe-perf.ps1 -Scenario <id> -DurationSec 60`
**Dependencies:** all · **Files:** none (operational) + `docs/research/perf/` · **Scope:** M
(real-time, not compute-bound)

---

## Checkpoint 4 — program complete

- [ ] All seven proofs run with falsifiers, outside a debug session
- [ ] Perf within ceiling, or shipped behind the kill switch defaulted off
- [ ] No fabricated evidence at any point — every claim traces to an executed command or a real number
- [ ] Deferred items still tracked in the ideal, none silently absorbed or dropped
