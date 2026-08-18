# Foundation Effect testing

How to prove Foundation Effects and how Secondary / FE / Server must test **without** opening PVZRH.

SSOT design: [effect-system.md](effect-system.md), [effect-data.md](effect-data.md), [effect-runtime.md](effect-runtime.md), [effect-funnel.md](effect-funnel.md) (v2 Funnel/FA10 Core + Writer Add).  
Live match / caps / Effect ClearAll lifecycle (design): [match-runtime.md](match-runtime.md).  
LIVE raw: [`../research/effect-runtime/_checklist-effect-foundation-live.json`](../research/effect-runtime/_checklist-effect-foundation-live.json).

---

## Contract freeze

| Field | Value |
|---|---|
| `FoundationContractVersion` | **2** (FA10 `ApplyResourceDelta` + Funnel; L1–L14 still valid on FA1–FA9) |
| v2 Funnel | [effect-funnel.md](effect-funnel.md) — Core mailbox + Writer Add + `guard-funnel-delta.ps1`. LIVE enqueue-delta prove next |
| Breaking change | bump version + regenerate `tests/fixtures/effects/*.json` goldens |
| Additive FA* | minor note + new LIVE row only (bump `FoundationContractVersion` when Plan shape breaks) |

Public DTOs live in `FusionRpg.Contracts` (`EffectEventDto`, `EffectGrantDto`, `IntentPlanDto`, …).

**Seal gate for Secondary:** offline scenario mirrors + `/api/sim/effect/*` are the regression path; do not open PVZ for Secondary Effect asserts. LIVE matrix remains operator lawn prove only when changing FA* apply paths.

---

## Owner key grammar

| Key | Meaning |
|---|---|
| `match` | All entities in the match |
| `plant:{typeId}` | Plants of that type (spawn / taken / dealt as plant) |
| `zombie:{typeId}` | Zombies of that type |
| `entity:{ptr}` | Runtime Unity pointer (LIVE only) |
| `player:{id}` | Reserved; treated as match-scoped in v1 |

Secondary **prefers** `match` and `side:typeId` so offline tests never need Unity pointers.

### Overlay filters (dealt / death)

| Filter | OnDamageDealt | Other triggers |
|---|---|---|
| `side` / `typeId` | Damaged side / `TargetTypeId` (event.Side is attacker) | Event `Side` / `TypeId` |
| `actorIsKiller` | — | OnDeath: `KillerPtr` required; if owner is `entity:{ptr}`, KillerPtr must equal that ptr |

### Clear between scenarios

`debug.effect.clear` / `EffectRuntime.ClearAll` withdraws grants, clears proc ICD/stacks + dedupe, and strips session `effect` mods. Match reset and `effects.reload` call ClearAll. Every `effect-*` scenario emits clear after session start.

Event dedupe for capture kinds lives on the **injector** `OnCapture` path (not the offline harness).

---

## Offline kit (required for Secondary / FE / Server)

```text
FoundationHarness / SimEffectHost
  .ClearAll / .Grant / .Withdraw / .AdvanceMs
  .OnEvent | HitDealt | Die | Spawn | FireFromCapture
  → IntentPlan (RecordingSink — never lawn)

EffectScenarioRunner
  JSON under tests/fixtures/effects/scenarios/effect-*.json
  ops: clear, matchStart, matchEnd, grant, withdraw, advanceMs, hit, die, spawn, event, fire,
       expectPlan (inline|golden), expectEmpty, expectSkippedContains
```

Secondary lifecycle scenarios use the `effect-secondary-*` naming prefix (`effect-secondary-butter-match`, `effect-secondary-match-cycle`). `matchStart` calls `BeginMatch(scenario.matchKey)`; `matchEnd` calls `EndMatch` (plugin grant/withdraw). `clear` remains bag-only.

- Seeded RNG + fake clock + `RecordingEffectSink`
- Goldens under `tests/fixtures/effects/*.plan.json`
- `ForOwner` + revisioned `Snapshot()` for FE/cache
- `IEffectGrantPlugin` + `EffectPluginHost` + default plugins in `FusionRpg.Core/Effects/Plugins/` — Grant/Withdraw only
- **Never** launch PVZRH or call injector status/spawn for fantasy asserts
- Offline PASS ≠ LIVE lawn PASS

Allowed for upper-layer Effect tests: harness, `SimEffectHost`, scenario fixtures,  
`POST /api/sim/effect/*`, Plan JSON equality.

### Bans

- Opening the game for Secondary/FE Effect regression
- Calling StatusExecutor / EntityApply / `debug.apply-status` from Secondary assemblies
- Asserting lawn side-effects from FE unit tests
- Executing `debug.spawn-plant` offline — synthesize FT* via host helpers / Core adapter instead

---

## LIVE matrix (Foundation only — open game)

| # | Scenario | Covers |
|---|---|---|
| L1 | `effect-butter-hit` | FT2 → FA2 butter |
| L2 | `effect-freeze-hit` | FA2 freeze |
| L3 | `effect-cold-hit` | FA2 cold family |
| L4 | `effect-clear-butter` | FA3 |
| L5 | `effect-passive-atk` | FA1 + OnGranted/OnRemoved |
| L6 | `effect-spawn-ondeath` | FT4 → FA4 |
| L7 | `effect-spawn-plant-bullet` | FA4 kinds |
| L8 | `effect-board-cherry` | FA5 |
| L9 | `effect-grid-cycle` | FA6–FA7 |
| L10 | `effect-set-dirt` | FA8 |
| L11 | `effect-economy-sun` | FA9 |
| L12 | `effect-icd-butter` | proc ICD |
| L13 | `effect-withdraw` | GrantStore |
| L14 | `effect-spawn-filter` | FT1 type grant |

Expect `debug.effect.fired` / `pvz.status.apply` (with `effect_id` / `grant_id` tags). While a Foundation grant is active, debug on-hit arms are skipped (no double-apply).

---

## Debug / reload commands

| Command / route | Role |
|---|---|
| `debug.effect.grant` | Upsert grant (+ Passive OnGranted) |
| `debug.effect.withdraw` | Remove grant (+ OnRemoved) |
| `debug.effect.clear` | Withdraw all grants; clear ICD/stacks/dedupe + session `effect` mods |
| `debug.effect.list` | Emit snapshot summary event (defs/grants counts) — **not** sync HTTP body |
| `debug.effect.fire-synthetic` | Inject FT* without lawn event |
| `debug.effect.enqueue-delta` | Funnel mutation → FA10 Writer Add + overlay floater (`POST /api/debug/effect/enqueue-delta`) |
| `effects.reload` | Re-seed catalog on injector (`POST /api/debug/effects/reload`) |
| `GET /api/debug/effects/contract` | Server-local contract version + frozen FT*/FA* lists (no injector) |

LIVE debug Effect routes return `{ ok, queued }` only; prove via `/api/debug/events`. Offline CI uses sync `/api/sim/effect/*` (Plans + revision) — see A8 / MatchRuntime section below.

---

## IntentPlan ↔ executor parity

Same `EffectBag` + `IEffectActionSink`:

- Harness uses `RecordingEffectSink` → Plan JSON
- Injector uses `InjectorEffectActionSink` → Unity / Intent

Executor throw or `false`: **stop sequence** (remaining actions in that grant seq do not run). Plan `skipped` includes `executor-stop`.

---

## Offline sim routes + MatchRuntime (A8)

`SimEffectHost` + `POST /api/sim/effect/grant|withdraw|clear|fire|scenario` are enough for Secondary/FE CI (no Unity, no full board). Foundation seal does **not** depend on board→Effect wiring.

**A8 end-state (design):** live overlay board + caps + Effect ClearAll lifecycle belong to **[MatchRuntime](match-runtime.md)** — not a second Plants/Zombies SSOT inside SimEngine. **W1 Core shipped:** `MatchValidator.Replay` + optional `SimEngine.EnableMatchOverlay()` folds capture kinds into MatchRuntime (sim HP lists remain for offline combat math only). Overlay auto-ends before mid-session `board.start` so sim reset stays aligned.

- Offline: `MatchValidator.Replay` + Effect bag tests (RAM only; no SQLite Admit).
- LIVE: Emit → `MatchRuntime.Apply` (W2); FA4/Intent Create preceded by `TryAdmitSpawn`.
- SimEngine: use `MatchOverlay` or Replay envelopes — do not treat `Plants`/`Zombies` lists as overlay living SSOT.

Secondary plugin lifecycle (`IEffectGrantPlugin` → `EffectPluginHost`) is wired on offline `SimEffectHost.BeginMatch`/`EndMatch` and LIVE `MatchHost` board edges (W9). Use `matchStart`/`matchEnd` scenario ops or manual Begin/End for plugin grant/withdraw regression.

| Route | Body | Returns |
|---|---|---|
| `POST /api/sim/effect/clear` | — | `{ ok, revision }` |
| `POST /api/sim/effect/grant` | `EffectGrantDto` | `{ ok, grant, revision }` |
| `POST /api/sim/effect/withdraw` | `{ grantId }` | `{ ok, revision }` |
| `POST /api/sim/effect/fire` | `event` **or** `kind`+`payload` **or** `helper=hit\|die\|spawn` | `IntentPlanDto` |
| `POST /api/sim/effect/scenario` | scenario JSON **or** `{ path }` | `EffectScenarioRunResult` |
| `GET /api/sim/effect/snapshot` | — | `EffectCatalogSnapshotDto` |
