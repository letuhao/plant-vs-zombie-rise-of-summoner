# Spec: wave-control (E36)

**Program:** effect-atom · **Map:** [../effect-atom-map.md](../effect-atom-map.md) §13 ·
**Ideal:** [../effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 2 ·
**Definitions (win over this spec):** [definitions.md](definitions.md) ·
**Kind registry:** [spec-atom-kind-registry.md](spec-atom-kind-registry.md)

**Status: specced, unbuilt (2026-09-03).** Wave 8, depends on E34 and E35.

E36 owns `wave.control`, a kind on E35's `Match` attach point that lets content shape **the pressure the
player is actually fighting**: summon a wave, summon a huge wave, set the wave timer, hold it. The ideal
calls this the one gap that needs both halves — a kind to act, and E34's `OnWave` to react — which is
why it sits behind both.

---

## 1. What exists today

**Built** — every host-side write already exists as a cheat action.

| Fact | Evidence |
|---|---|
| `CheatActions.SummonWave(int)` calls `BoardSpawner.SummonZombies(wave)` | `src/FusionRpg.Injector/CheatActions.cs:696-706` |
| `CheatActions.HugeWave()` calls `board.HugeWaveEvent(board.theWave)` | `:708-719` |
| `CheatActions.SetWaveTimer(float)` writes `board.timeUntilNextWave` | `:721-728` |
| `DebugActions.WaveFreeze(bool)` toggles `F-WAVE-FREEZE` and emits `debug.wave.freeze` | `DebugActions.cs:939-943` |
| All four are reachable as cheat commands | `CheatCommandRunner.cs:271-272`, `:659` |
| Wave events are already emitted: `wave.change`, `wave.spawn`, `wave.huge` | `GameHooks.cs:333-341`; `GameCaptureHooks.cs:214`, `:221` |
| Coverage rows and status | `docs/research/cheat-menu-coverage.md:243-246` — `F-SUMMON` / `F-HUGE` / `F-WAVE-T` / `F-WAVE-FREEZE`, all "done", all "needs-probe" |
| Summon wave classed **PROBE** host-side | `docs/research/effect-runtime/07-effect-opportunities.md:71` |

**Wiring gap.**

| Gap | Evidence |
|---|---|
| All four are reachable only from cheat/debug state — no content path | the call sites above |
| `wave.change`/`wave.spawn`/`wave.huge` reach `TryMap` and fall through to `return null` | `EffectEventAdapterCore.cs:44`. **E34 closes this half** |

**Real gap — and one of them is a naming defect, not a missing feature.**

| Gap | Evidence |
|---|---|
| No kind, no opcode, no sink arm | `AtomKindRegistry.cs:105-346`; `AtomCompiler.cs:180-195`; `InjectorEffectActionSink.cs:20-46` |
| **`F-WAVE-FREEZE` does not freeze.** It floors the timer at `30f` every tick | `CheatActions.cs:33-36` — `board.timeUntilNextWave = Mathf.Max(board.timeUntilNextWave, 30f)`. Calling the atom op `freeze` would be the `fx.set_dirt_box` defect again (E28 authors `boxType: 1`, which is Water, and names it "dirt") |
| `30f` is a bare literal on what is unmistakably a balance surface | `CheatActions.cs:35`. It belongs in `data/tuning/match.v1.json` |

## 2. The contract

### 2.1 The kind

```csharp
new("wave.control", AttachPoint.Match, new ParamSchema(
        new ParamDef("op",      ParamKind.String, Required: true),
        new ParamDef("wave",    ParamKind.Int,  HonouredOnlyWhen: "op=summon"),
        new ParamDef("timerMs", ParamKind.Int,  HonouredOnlyWhen: "op=setTimer"),
        new ParamDef("enabled", ParamKind.Bool, HonouredOnlyWhen: "op=hold")),
    new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.None),
    Concat(AtomTriggers.Events, AtomTriggers.MatchEvents),
    PowerCategory.Offense | PowerCategory.Control,
    "Ops are summon|huge|setTimer|hold. 'hold' floors the wave timer, it does not stop it — " +
    "CheatActions.cs:33-36. Refused at ChainDepth > 0.")
```

**E36 adds one kind and no attach point.** `KindCount` (`AtomKindRegistry.cs:18`) gains **+1**;
`AttachPointCount` (`:16`) is not changed **by this module** — E36 reuses the `Match` point E35 adds.
`EffectActions.WaveControl = "WaveControl"`, and `AtomCompiler.OpcodeOf` gains
`"wave.control" => EffectActions.WaveControl` — the bijection stays exact.

> **⛔ CORRECTED 2026-09-03 — this read `13 → 14`, an absolute that is only true if E35 lands first.**
> The wave's counts are stated once, in `spec-match-modify.md` §2.1: **`AttachPointCount = 7`,
> `KindCount = 16`** as the Wave 8 end state, from `5` and `12` today. Every module states its own
> **delta** and asserts the registry, never a literal — `AtomKindRegistryTests.cs:22` compares
> `KindCount` to `All.Count`, so a copied number cannot satisfy it anyway.

**Adding an opcode means growing `/effects/contract`'s `actions` array in the same change.** That array
publishes **ten** of `EffectActions`' twelve constants under `frozen = true`
(`DebugEndpoints.cs:388-394`; the constants at `EffectDtos.cs:22-44`) — `GrantShield` and
`ModifyDerivedStat` are already missing. **E33 repairs those two** (`spec-activation-edge.md` §2.1a);
E36 owns adding `WaveControl` beside them rather than making it three. Criterion 1 asserts it by count.

`HonouredOnlyWhen` is the discriminator mechanism E1 already uses for `spawn.entity`'s per-kind params
(`AtomKindRegistry.cs:291-298`), so an unhonoured key is a validation error rather than a silent drop.
Battle and Sim are `None`: neither has a `BoardSpawner`, and flipping a cell without an executor is D6.

### 2.2 The four ops

| `op` | Executes | Param | Units |
|---|---|---|---|
| `summon` | `CheatActions.SummonWave(wave)` → `BoardSpawner.SummonZombies` (`:696-706`) | `wave` | wave ordinal, `int`, `>= 1`. Not a magnitude — an index |
| `huge` | `CheatActions.HugeWave()` → `board.HugeWaveEvent(board.theWave)` (`:708-719`) | none | — |
| `setTimer` | `CheatActions.SetWaveTimer(timerMs / 1000f)` (`:721-728`) | `timerMs` | integer **ms** (`definitions.md` §2), `>= 0`, divided by 1000 once, at the boundary |
| `hold` | `DebugActions.WaveFreeze(enabled)` (`:939-943`) | `enabled` | bool |

**`hold`, not `freeze`.** The toggle floors `timeUntilNextWave` at a configured minimum each tick
(`CheatActions.cs:33-36`); it does not stop the clock. The vocabulary says what the code does. E36 also
moves that `30f` into `data/tuning/match.v1.json` as `wave.holdFloorSeconds` — a balance pass would
change it, so by [tunables-ssot.md](../tunables-ssot.md)'s own test it is a tunable, and a bare literal
in a path content can now reach is exactly the case that rule covers.

### 2.3 Recursion — the guard that makes this kind safe

`summon` and `huge` cause zombie spawns, which emit `zombie.place` → `OnSpawn`
(`EffectEventAdapterCore.cs:26-29`) and, through E34, `wave.spawn` → `OnWave`. A `wave.control` atom
triggered on either would summon forever.

> **A `wave.control` atom is refused when `EffectEventDto.ChainDepth > 0`** (`EffectDtos.cs:79`), and
> the executor returns `false` — a real failure, so sequence stops (`InjectorEffectActionSink.cs:63-72`).

This is a structural limit, not a progression ceiling, and it is exempt from the no-caps rule for that
reason. It is stated as a rule rather than left to the runtime because the failure mode is an unbounded
spawn loop on the Unity main thread, which is unrecoverable rather than merely wrong.

### 2.4 Seed JSON

```json
{
  "family": "atom.curse-early-pressure",
  "tier": 2,
  "kind": "wave.control",
  "name": "Early pressure",
  "icdKey": "curse.early_pressure",
  "params": { "op": "setTimer", "timerMs": 3000 },
  "when": { "trigger": "OnWave" }
}
```

### 2.5 Scope and lifetime

- **Owner key must be `match` or `player:`**, as for `match.modify` — an entity does not own the wave
  clock. Anything else is `ScopeUnsupported` at bind (`definitions.md` §6).
- **`hold` is the only op with state**, and it is a `CheatState` toggle. `EffectRuntime.NotifyMatchEnd`
  (`EffectRuntime.cs:130`) must clear `F-WAVE-FREEZE` for the same reason E35 restores the board config:
  a toggle that survives a match leaks silently and permanently.
- **`summon` and `huge` are edges, not state** — nothing to revert on withdraw.

## 3. What it must NOT do

- **No new host write.** All four ops call existing `CheatActions` / `DebugActions` entry points. If a
  design wants a wave behaviour those four cannot express, that is a new capability and a new spec.
- **No `Time.timeScale`, no wave skipping, no wave count change.** `timeScale` is product-OUT by policy
  (`modifiable-gameplay.md`, map §17); the rest have no host write.
- **No self-triggering.** §2.3's `ChainDepth` refusal is not optional and not a runtime nicety.
- **No magnitude chosen by a model.** `timerMs` and any tier band come from authored content or a value
  spec, and the numbers a balance pass would turn — the hold floor included — live in
  `data/tuning/match.v1.json`, not in code. `long` for any magnitude, never `float`; widen before
  multiplying; divide by 1000 last, exactly once; overflow throws rather than wrapping or clamping.
- **No wave-number predicate.** *"Only after wave 10"* is an E3 predicate leaf, and E34 says the same.
- **No opcode without the published list growing with it.** §2.1. `WaveControl` enters
  `/effects/contract`'s `actions` array in the same change as the constant. A published list that lies
  is the defect E33 and E34 exist to close, and it stays open for `actions` unless every
  opcode-adding module says so.

## 4. Testing strategy

Core tests cover the schema, the compiler, the ops and the recursion guard. **The injector is not built
by CI** — `.github/workflows/ci.yml` tests ten managed projects and never compiles
`FusionRpg.Injector` — so `ExecWaveControl` is proven by a LIVE run.

| Case | Expect |
|---|---|
| `KindCount == AtomKindRegistry.All.Count`, both one higher than before this module, and `OpcodeOf` returns non-null for every kind | pass; the bijection is asserted over the registry, not against a copied number (`AtomKindRegistryTests.cs:22`) |
| `/effects/contract`'s `actions` array | contains `WaveControl`; count asserted, so a further opcode cannot arrive silently (§2.1) |
| `wave.control` with `op: "summon"`, `wave: 3` | `Ok` |
| `wave.control` with `op: "summon"`, `timerMs: 5000` | `ParamNotHonoured` — `timerMs` is `op=setTimer` only |
| `wave.control` with `op: "freeze"` | `BadParamValue`, naming the four legal ops. The op is `hold`, and the message says the floor is a floor |
| `wave.control` with no `op` | `MissingParam` |
| `wave.control` with `op: "setTimer"`, `timerMs: -1` | `BadParamValue` |
| Bind to an `entity:{ptr}` owner | `ScopeUnsupported` |
| Execute with `ChainDepth == 1` | executor returns `false`, sequence stops, error emitted |
| `wave.holdFloorSeconds` is read from `data/tuning/match.v1.json` | pass — no literal `30` in `CheatActions.cs` |
| **PLANTED VIOLATION** — remove the `ChainDepth` refusal | a test that fires an `OnSpawn`-triggered `summon` atom against a spawn event with `ChainDepth = 1` **fails**. Without it the atom summons on its own spawns, and the failure mode is an unbounded loop on the Unity main thread — the one defect here that cannot be diagnosed after the fact |
| **PLANTED VIOLATION** — rename the op back to `freeze` and keep the floor implementation | the vocabulary test fails, naming `CheatActions.cs:33-36`. This is the `fx.set_dirt_box` class of defect: a name that says one thing while the executor does another |

**LIVE proof (owner-run):** bind `{op: "setTimer", timerMs: 3000}` on `OnWave`, enter a level, and watch
two wave transitions. The gap between them must shorten to about three seconds and must return to the
level's own interval once the container is withdrawn.

## 5. Acceptance criteria

1. `wave.control` is registered on `AttachPoint.Match`; `KindCount` is **one higher than before this
   module** and equals `AtomKindRegistry.All.Count`; the bijection holds for every registered kind; and
   `/effects/contract`'s `actions` array contains `WaveControl`, asserted by count.
2. `op` accepts exactly `summon` · `huge` · `setTimer` · `hold`, and refuses anything else with
   `BadParamValue`.
3. `wave`, `timerMs` and `enabled` are each honoured only under their own `op`, refused otherwise.
4. `timerMs` is integer ms and is divided by 1000 exactly once, at the Unity boundary.
5. A `wave.control` action at `ChainDepth > 0` returns `false` and stops the sequence.
6. Only `match` and `player:` owner keys bind.
7. `NotifyMatchEnd` clears `F-WAVE-FREEZE`.
8. The hold floor is a tunable in `data/tuning/match.v1.json`; no bare `30` survives in
   `CheatActions.cs`, and `python scripts/audit-magic-numbers.py` reports no new M1/M2 finding.
9. Both planted violations in §4 fail their tests.
10. The LIVE proof shows the wave interval shortening and then returning on withdraw.

## 6. Dependencies and cross-program hazards

**Depends on:** E34 (the `OnWave` trigger — without it this kind can act but nothing can make it act)
and E35 (the `Match` attach point).

| Hazard | Detail |
|---|---|
| **Both halves or neither** | The ideal is explicit that this row *"needs both halves"*. Landing `wave.control` before `OnWave` gives a kind whose only triggers are the four board events — authorable, and not the design. Do not split |
| **Unbounded spawn loop** | §2.3. The most dangerous failure in Wave 8, because it runs on the Unity main thread and takes the game with it. Its planted test is the acceptance surface, not a nicety |
| **Injector not built by CI** | `.github/workflows/ci.yml` — `effect-atom-map.md` §6 H1 recurring |
| **`AtomImporter` staleness trap** | Map §16: a new `OpcodeOf` arm is a compiler-code change, and the importer's hash covers seed data only, so it will report *"nothing changed"* |
| **Stale instances** | Any `catalog_revision` bump makes previously rolled `effect_instance` rows unbindable (`StaleInstance`). Pre-existing; state it in the rollout note |
| **Perf** | `summon` and `huge` are per-event Unity calls. They are edges rather than per-hit work, so the 2026-08 audit's `FindObjectsOfType` pattern does not apply — but an `OnDamageDealt`-triggered `summon` would make it apply immediately. The `ChainDepth` guard does not cover that case; ICD does, and content authoring it without one is a review finding |
| **Cheat-state coupling** | `hold` shares `F-WAVE-FREEZE` with the cheat UI and with the stress runner, which saves and restores it (`DebugActions.cs:986`, `:1049`). A stress run therefore overwrites a bound hold. Acceptable and intentional — one writer — but it belongs in the rollout note |
