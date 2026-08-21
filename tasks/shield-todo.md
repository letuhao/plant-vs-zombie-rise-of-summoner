# Tasks: shield-system

Plan: [shield-plan.md](shield-plan.md) · Spec: [../docs/architecture/shield-system-spec.md](../docs/architecture/shield-system-spec.md) (Draft v3)

## Phase 1 — Foundation (no behavior change)

- [x] **Task T1: Docs unlock — decisions.md amendment + SSOT pointer**
  - Description: add the decisions.md row "Shield layer: CombatMath shield sits above Funnel (shield-system-spec.md)" unlocking the element-hub v1 ban; update the Element Hub row's channel count 56 → 84; point element-hub-ssot.md §13 ban-list line at the spec.
  - Acceptance: decisions.md and element-hub-ssot.md reference the spec; no contradicting locked row remains.
  - Verify: doc read-through; grep "No element-specific shield engine" resolves to the amended wording.
  - Files: `docs/architecture/decisions.md`, `docs/architecture/element-hub-ssot.md`. Scope: XS.
  - Dependencies: none — spec approved 2026-08-21 (owner decisions 8–10 folded in).

- [x] **Task T2: Channel expansion 56 → 84 + reader maps**
  - Description: add the 4 `combat.shield.*` families to `CombatChannelFamilies`; fix the `8 × 7 = 56` doc comment; add 4 element→channel switch maps to `CombatDerivedReader` (`ShieldCapacity/Toughness/Pen/Regen`); update the registry test literal 56 → 84 (+ name/comment); add the exhaustiveness walk (all 84 ids resolve through reader + registry without throwing).
  - Acceptance: all existing tests green with only the permitted churn (the one literal + comment/name); walk covers 4 new families × roster.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `DerivedStatChannels.cs`, `CombatDerivedReader.cs`, `DerivedStatRegistryTests.cs`, new walk test. Scope: S.
  - Dependencies: T1.

- [x] **Task T3: ShieldPolicy + ShieldElementMatrix**
  - Description: `ShieldPolicy` permille consts (`ShieldMatchupShareKPm=250`, `ShieldChipFloorKPm=100`, `ShieldPenCapKPm=3000`, cap=3); `ShieldElementMatrix` returning unit relations (−1|0|+1) seeded from ring + light/dark mutual counter, roster-generated.
  - Acceptance: golden matrix generated from `ElementRoster` (all pairs, fail-open defense); seed-equality test vs `ElementRingMatrix` comparing **relations, not K-scaled shares**.
  - Verify: Core tests, new `Combat/Shield` test folder.
  - Files: `Core/Combat/Shield/ShieldPolicy.cs`, `ShieldElementMatrix.cs`, tests. Scope: S.
  - Dependencies: none (parallel with T2).

- [x] **Task T4: ShieldMath — single-layer absorb + goldens**
  - Description: pure static permille-`long` absorb per spec §2.4: elemMod (unit relation × K), `hitCount × breakerDelta`, clamp `[ceilPm(0.10×input), 3×input]`, half-away-from-zero remainder. No runtime state.
  - Acceptance: goldens cover roster × relation × pen/toughness × {hold, exact-break, overflow}; chip-floor + pen-cap engagement rows; `hitCount>1` with coalesced ≡ n× uncoalesced assert; untyped (`none`) omni-only rows; `input=1,d=2` tie golden; `input=0` → 0; all §2.4 invariants asserted.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Shield`.
  - Files: `Core/Combat/Shield/ShieldMath.cs`, tests. Scope: M.
  - Dependencies: T3.

### Checkpoint 1 — Foundation
- [x] Core suite green (971 → then 1104 incl. concurrent Battle stream); only the permitted registry churn. (Guards re-run at Checkpoint 3.)

## Phase 2 — Runtime core (still unwired)

- [x] **Task T5: ShieldInstance + runtime state (apply/merge/admission)**
  - Description: per-owner store (dict by ownerKey), `Apply` with capacity read via reader, merge on `(sourceId, element)` (`refillOnMerge` split, expiry refresh, `maxHp≤0` → remove as expired), admission at cap (`newMaxHp > weakest.currentHp` else drop), cap 3, `(priority, createdSeq)` ordering with source-class defaults aura 30 / skill 20 / innate 10 (owner decision 9).
  - Acceptance: stacking tests — cap, both merge modes, capacity-downgrade clamp, admission accept/drop, eviction marked expired-not-broken, multi-element same-source slots, `maxHp≤0` rejection, order determinism.
  - Verify: `--filter FullyQualifiedName~Shield`.
  - Files: `ShieldInstance.cs`, `ShieldRuntime.cs`, tests. Scope: M.
  - Dependencies: T2.

- [x] **Task T6: Absorb cascade**
  - Description: `ShieldRuntime.Absorb(ptr, amount, hitCount, components, attackerSnapshot, ownerSnapshot)` — cascade over the stack calling `ShieldMath` per layer with per-layer element reads; prune broken inline (single broken mark per instance); track per-shield spent for events/debug; no-shield fast path = single `TryGetValue` miss, zero alloc.
  - Acceptance: the worked 240-fire/ice-earth-untyped cascade golden byte-exact; per-layer flat-pen compounding under cap; remainder monotonicity across layers; fast-path allocation test.
  - Verify: `--filter FullyQualifiedName~Shield`.
  - Files: `ShieldRuntime.cs`, tests. Scope: M.
  - Dependencies: T4, T5.

- [x] **Task T7: Tick upkeep — regen, expiry, prune**
  - Description: front-shield regen (first in drain order with `hp<maxHp`; `regen.omni + regen.{el}` as HP/sec, permille carry on the 100 ms grid), tick expiry → `expired`, prune; locked order regen → expiry/prune; broken-in-dispatch never revives same tick.
  - Acceptance: no omni multi-dip across 3 shields; carry determinism (10 ticks ≡ 1 s exactly); expiry semantics; 1-HP survivor regens.
  - Verify: `--filter FullyQualifiedName~Shield`.
  - Files: `ShieldRuntime.cs`, tests. Scope: S.
  - Dependencies: T5.

### Checkpoint 2 — Runtime core
- [x] Shield unit suite 124 green; zero call sites outside tests; Core full suite 1104/1104.

## Phase 3 — Gate + host (the vertical spine)

- [x] **Task T8: Dispatcher gate + HitCount plumbing** *(deviation: no DamagePacket field needed — the gate reads the existing `EffectEventDto.HitCount` from the coalesced record at dispatch time)*
  - Description: `DispatchInstant` routes negative finalized amounts through `Absorb`, enqueues only the remainder; additive `HitCount` field on `DamagePacket` (default 1) fed from the coalesced record; heals bypass.
  - Acceptance: integration tests — instant damage and **status DoT tick** (via `StatusFunnelPulseSink`) spend shield, Funnel receives exactly the remainder; heals untouched; **no-shield path byte-identical** (existing combat goldens re-run unmodified); PerfProbe `ShieldAbsorb` section emits.
  - Verify: Core suite + all four guard scripts.
  - Files: `CombatDamageDispatcher.cs`, `Contracts/CombatDtos.cs` (or `DamagePacket` home), `Core/Diagnostics/PerfProbe.cs`, tests. Scope: M.
  - Dependencies: T6.

- [x] **Task T9: Injector tick host**
  - Description: `ShieldRuntime.Tick` in `InjectorLoop` on the 100 ms grid behind its **own** `HasAnyInstances()` guard (not TickDots' status guard); frame order locked: drain dispatch first, then shield upkeep.
  - Acceptance: injector builds; tick fires with shields present and early-outs without; order verified by test or probe trace.
  - Verify: build + Core/Launcher suites; `guard-secondary-no-unity.ps1`.
  - Files: `Injector/Host/InjectorLoop.cs`, `Injector/Effects/` glue. Scope: S.
  - Dependencies: T7, T8.

### Checkpoint 3 — Spine
- [x] Offline E2E green (instant partial/full absorb, typed-shield matchup, DoT 3-tick break, heal bypass, no-shield byte-identical); injector builds; all four guards OK.

## Phase 4 — Grant surfaces (one slice per source)

- [x] **Task T10: `shield.grant` effect action** *(action name landed as `GrantShield`, matching the EffectActions PascalCase vocabulary; handled bag-side like ApplyResourceDelta since shields are Core runtime state — the sink never sees it)*
  - Description: new action kind handled by the effect action sink → `ShieldRuntime.Apply` (never a Funnel mutation); direct targets (`Actor`/`EventTarget`/`Single`); grant fields (base, element, priority, durationTicks, `refillOnMerge` default true).
  - Acceptance: E2E grant → absorb test; unknown element rejects; plugin-side sink respects `guard-secondary-no-unity` tokens.
  - Verify: Core suite + guards.
  - Files: effect action vocabulary + sink, `ShieldRuntime.cs`, tests. Scope: M.
  - Dependencies: T8.

- [x] **Task T11: Aura path — OnTimer + Area multi-grant** *(deviation: no separate ShieldAuraGrants.cs — ExecGrantShield in EffectBag subsumes it via `sourceClass=aura` + the Area target grammar; a second resolver would have duplicated it. Naming discipline honored: `resolvedOwners`.)*
  - Description: `ShieldAuraGrants` resolving via existing `TargetResolver` (`Area/Row`, `Square`/`Rectangle`, pool filters incl. explicit `side`, `excludeMindControlled`); one `Apply` per resolved owner, `refillOnMerge=false`; rides the `OnTimer` trigger. **Naming discipline: no `targetPtrs`/guarded literals anywhere in the file, comments included.**
  - Acceptance: OnTimer grant → area resolve → multi-apply; re-assert idempotent (no refill on undamaged aura); no per-hit aura work; guard grep clean.
  - Verify: Core suite + `guard-funnel-delta.ps1`.
  - Files: `Core/Combat/Shield/ShieldAuraGrants.cs`, tests. Scope: M.
  - Dependencies: T10.

- [x] **Task T12: Innate shields — queue + barrier** *(deviation: the additive `BattleActorSetup` innate field is deferred — the Battle-C2 stream is actively editing `Core/Battle/` in this working tree right now and owns that seam; flagged for the owner in the final report)*
  - Description: innate defs keyed by `TypeId` (content row: base, element, priority; no expiry); queue at `InjectorEntityRegistry.Add`, apply on first shield tick after owner derived snapshot completeness (capacity read there); stable `innate:{typeId}` sourceId so resync re-fires are idempotent merges; additive innate field on `BattleActorSetup` (seam only — standalone absorb stays blocked on C2).
  - Acceptance: barrier test (capacity contributor landing between registration and first tick is included); resync idempotency; `maxHp≤0` innate rejected at apply.
  - Verify: Core suite + build.
  - Files: `InjectorEntityRegistry.cs`, `ShieldRuntime.cs` (queue), `Core/Battle/BattleModels.cs`, content ingest validation, tests. Scope: M.
  - Dependencies: T9, T10.

### Checkpoint 4 — Grant surfaces
- [x] All three sources grant end-to-end offline (effect action, OnTimer aura lane-wide + idempotent, innate queue/barrier/resync semantics); Core 1170/1170; injector builds; guards green.

## Phase 5 — Observability + surfacing

- [x] **Task T13: Events + aggregation + noisy-kind + protocol doc**
  - Description: `shield.granted/absorbed/broken/expired` on the string stream; runtime aggregates `absorbed` per `(ownerKey, shieldId)` per flush window (sum + hitCount payload); `broken` flushes that shield's pending aggregate first; `RpgConstants.IsNoisyKind += shield.absorbed`; document kinds + payload keys in `docs/protocol/events.md`.
  - Acceptance: aggregation and flush-ordering tests; noisy suppression test; doc updated.
  - Verify: Core suite.
  - Files: `ShieldRuntime.cs` (emit), `Contracts/Dtos.cs`, `docs/protocol/events.md`, tests. Scope: M.
  - Dependencies: T8.

- [x] **Task T14: VFX cue registration for shield.broken**
  - Description: new `VfxCueIds` const + core recipe entry + emit point on break (art/tuning stays with the VFX stream).
  - Acceptance: cue id registered and referenced from the break emit; VFX tests green.
  - Verify: Core suite.
  - Files: `Core/Vfx/VfxCatalog.cs` (+ recipes), tests. Scope: XS.
  - Dependencies: T13.

- [x] **Task T15: Debug surfaces — grant endpoint + probe re-route + boards + breakdown** *(injector paths are compile-verified + covered by Core gate tests; the command runner itself is Unity-bound, so its E2E is the owner-run live proof. Bonus: targeted `enqueue-delta` dispatch now passes the gate too.)*
  - Description: debug shield-grant action (grant to selected ptr: base, element, duration, priority default 20 → `ShieldRuntime.Apply`, same command pattern as `enqueue-delta` — owner decision 10); re-route `debug.combat.probe`'s overlay branch through `DispatchInstant` (it currently enqueues the computed delta directly, bypassing the gate); per-shield lines on the debug board (`element`, `hp/maxHp`, spent last window); `absorbed`/`hpRemainder` lines in the damage breakdown output.
  - Acceptance: debug grant → probe against the shielded target shows absorption (offline test asserts the grant + re-routed path end-to-end); pass-through branch behavior unchanged; boards render shield lines.
  - Verify: Core + CheatCore suites; guards.
  - Files: `Injector/DebugCombatActions.cs`, breakdown DTO surface, tests. Scope: M.
  - Dependencies: T8.

- [x] **Task T16: Dump keys + web shield bar**
  - Description: `rpgShieldHp`/`rpgShieldMax` in `SimEngine.PlantDump`/`ZombieDump` + injector `GameDumps`; web lawn fold + view model render a **separate** shield bar from the new keys — vanilla `theShieldHealth` → armor mapping untouched.
  - Acceptance: fold test asserts armor mapping unchanged and shield bar sourced only from `rpgShield*`; dumps additive.
  - Verify: Core suite + web build/tests.
  - Files: `Core/SimEngine.cs`, `Injector/GameDumps.cs`, `web/.../lawnProjectorFold.ts`, `lawnViewModel.ts` (+ component), tests. Scope: M.
  - Dependencies: T9.

### Checkpoint 5 — Complete (final gate)
- [x] Full suites green (Core **1196/1196** after the /test gap pass; Guard 40, Data 114, CheatCore 40, Launcher 128, web fold 51 + tsc clean) + all four guard scripts OK.
- [x] /test gap pass (12:32): +5 coverage locks — determinism replay (identical scripts → identical stacks + event streams), no-shield fast path **0 bytes / 1000 calls**, gate never resolves snapshots on miss/heal (throwing-resolver proof), merge-without-duration clears expiry (locked as recast-upgrade), PerfProbe `shield.absorb` section emits. Shield suite: **161**.
- [x] Spec §11 criteria walked: 1–2, 5–8 met offline (registry churn = the one 56 literal + the two VFX 24→25 literals, both additive); 3–4 offline halves met, live halves owner-run.
- [x] Five-axis review pass (14:19): 1 Critical fixed (death/lifecycle flush was never wired — `RemoveAll` had no production caller; now: registry `Remove` → per-actor flush, board-start `Clear` + `EffectBag.ClearAll` → full `ShieldRuntime.Clear`), 3 Important fixed (regen-carry leak on absorb-emptied stacks; grant rejections silently dropped — now `_lastSkipped` lines; stale web shield bar — dumps emit explicit 0/0, fold clears on `rpgShieldMax <= 0`), dead `ShieldGate.OnAbsorbed` hook removed. +4 Prove-It tests (RED-verified). Shield suite **164**, Core 1199/1199.
- [ ] **Owner-run:** deploy, stress scenario (no-shield pipeline share unchanged vs baseline), live `debug.shield.grant` → `debug.combat.probe` absorb proof.
- [x] Commit drafts handed to owner in the session report (no git writes per AGENTS).
