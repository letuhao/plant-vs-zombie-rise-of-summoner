# Spec: delve-battle-profile

Status: **APPROVED by the owner 2026-09-05 (wave 2) — not built.** Written against the working tree the
same day; every line number below was opened this session. Three battle files are **uncommitted and
moving** while this is written (`BattleEngine.cs`, `BattleModels.cs`, `BattleModeProfile.cs` — base-defense
`battle-clock-profile`/`combatant-kind` and a staged battle-tempo sweep); §Drift lists what moved.

Module id `delve-battle-profile` in the [party-dungeon map](../party-dungeon-map.md) (row 7, wave 2).
Depends on `encounter-generator` (the `BattleActorSetup`s it plays) and `delve-scope`
(`rpg_delves.decisions_json`, `parties_json`, `AppendDecision`, `spec-delve-scope.md:72-73`, `:272`).
External: `battle-clock-profile` (`base-defense-map.md:92`), `siege-ai` (`:100`), base-defense decision 46
(`base-defense-ideal.md:231-275`), battle-timeline T10/T14 (`battle-timeline-map.md:100`, `:104`).

## Objective

Turn a rolled encounter into a **played or autopiloted fight inside a delve** without changing what a
battle is. One new profile row, one explicit `Resolve` call, two seats on one `IIntentSource`, two decision
logs, three nullable fields, and a second producer for a flag that already exists. Success looks like gate
G2 (`party-dungeon-map.md:158`): one rolled room resolves through `BattleEngine.Resolve` with the `delve`
profile and an automated source, byte-identical on replay; **all four battle hashes, the 32-seed sweep and
the four expedition tier hashes unchanged**; a steered fight frozen mid-round and resumed from its
decision log finishes byte-identical to the uninterrupted fight.

## Locked anchors

- **`decisions.md:42` (Battle time model), incl. the 2026-09-05 clause:** the profile is *"resolved from an
  existing content id … **or from an encounter anchor id passed explicitly to `BattleEngine.Resolve(profile:)`
  by a host that owns no `WaveCatalog` row**; `WebMatchService.ProfileForWave` returns null for a wave id the
  catalog does not know and `BattleEngine.cs:220` turns that null into `classic-round` — that fallback must
  never be the path for generated encounters — and **never serialized onto `BattleSetup`**."* Determinism
  for live sessions is `(setup, seed, decision-trace)` and *"the boot sweep must **refuse** an incomplete
  trace rather than re-resolve it."*
- **`decisions.md:50` item (1):** expeditions and web matches run `hybrid-atb` (W=4, `FixedIncrement`,
  `EarlyBoundWithFallback`, `ActionPoints(2)`); the `WReact` half is corrected — `battle.v4.json:37` ships
  `hybrid-atb.wReact: 1`, read at `BattleEngine.cs:238`, consumed only under `UsesTimelineDispatch`.
  **Item (3):** expeditions are barred from interactive profiles by assertion — the delve is not an
  expedition, so row 50 (3) is untouched (audit §1(b) point 3).
- **R9** (`party-dungeon-ideal.md:1755`): *"A `siege-ai`-class policy per un-steered party; switching away
  freezes the fight as a persisted decision log (base-defense decision 46). 'Same seed, same rewards' holds;
  autopilot is never a competitor to steering."*
- **Review §1(b)** (`audit-2026-09-05.md:60-73`), quoted: *"`BattleEngine.Resolve` takes `profile:` and
  `intentSource:` explicitly (`BattleEngine.cs:172-175`) … **What is needed, exactly:** (1) the delve host
  resolves the profile from the encounter anchor and passes it to `Resolve` itself — never through
  `ProfileForExpedition`, and never through `WebMatchService.ProfileForWave`, which returns `null` →
  `classic-round` for any id not in `WaveCatalog` … (2) an automated `IIntentSource` that is a competent
  policy for a five-kit party — `StubIntentSource` is basic-attack only … that is base-defense's `siege-ai`
  seam (one `IIntentSource` dispatching on `SideOf`), a consumed dependency, not a free one."*
- **S2-15 / S2-16** (`audit:225-226`): a delve-level decision log T10 does not own; a steered party mid-fight
  at logout is refused on boot unless pause = persisted decision log; **extraction is raid-wide** — a party may
  hold at a rest, never bank. **§4.4** (`ideal:389-414`, as corrected): autopilot is one explicit call; carry-in
  is *"that one field … under the `WhenWritingDefault` precedent."* **§4.8** (`:508-511`): `PartyIndex` is a
  label on the setup, not a third side.

## Design

### 1. The `delve` profile row

`BattleModeProfileCatalog` is the one file allowed to hold a profile id literal (`BattleModeProfile.cs:161-168`;
`ModeProfileArchitectureTests.cs:27` lists the four ids). The row is `hybrid-atb`'s shape (`:218-225`) with
two declared differences and one inherited fact:

| Field | `delve` | Source of the value | Note |
|---|---|---|---|
| `AdvancePolicy` | `FixedIncrement` | code (structure) | as `hybrid-atb` |
| `W` / `WScope` | from `timeline.profiles.delve.w`, `Global` | `battle.v{n}.json`; **overridden per encounter** (§Tunables) | `BattleTuning.cs:137` requires the key and `:145-146` throws on `≤ 0` |
| `DefaultCommitment` | `EarlyBoundWithFallback` | code | as `hybrid-atb` |
| economy | `ActionPointsEconomy(maxPoints)` | `timeline.profiles.delve.maxPoints`; `Build` throws without it (`:265-267`) | `PerSide` variant: §1a |
| `OrdersBySpeed` | `true` | declared per row (`:87-102`) | never a branch on `AdvancePolicyKind` |
| `RequiresLiveInput` | **`true`** | declared per row (`:104-116`) | second true row after `siege` (`:258`) |
| `WReact` | inherited `1` if copied from `hybrid-atb` | `battle.v4.json:37`, loaded `BattleTuning.cs:138` | **inert on `delve`**: the lane is consumed only under `UsesTimelineDispatch` (`BattleEngine.cs:395`), which the `delve` row declares `false` in v1 — see Drift on the staged `hybrid-atb` flip |
| `UsesTimelineDispatch` | `false` | declared per row (`:118-135`) | flips with battle-tempo LAND2 sign-off, not here |
| `DownedOnDeplete` | **`true`** | declared per row — a new `bool` field, the `RequiresLiveInput` shape (`:104-116`); `false` on every shipped row | `delve-attrition` §6 owns the behaviour: HP ≤ 0 on an actor whose setup carries `PartyIndex` enters `Downed`, never `Dead`; the wave side keeps today's path. Two gates (field + `PartyIndex`), so no golden actor can reach it |
| `MaxRounds` / `RoundDurationMs` | `timeline.profiles.delve.{maxRounds,roundDurationMs}` | optional per profile (`BattleTuning.cs:127-134`), falling back to `ruleset.*` (`Build` `:276-277`) | already on the record (`:153`, `:158`) — the map's "after `battle-clock-profile`" precondition is met in the working tree |

Landing is the structural four-liner the catalog's own comment prescribes (`:165-167`): a `DelveId` const
and `Delve` property, one switch arm in `Resolve(string?)` (`:316-324`), one `InlineData` in
`ModeProfileTuningBindingTests.cs:27-30` and `:48-51`, one id in `KnownProfileIds`. The row is **never
serialised on `BattleSetup`** (`BattleModels.cs:248-267` gains no field) — a profile field there rides into
`ExpeditionBattlePlan.Setup` (`ExpeditionResolver.cs:21-22`) and moves all four tier hashes.

**1a. `PerSide` economy variant.** The economy *type* is structure, not a magnitude (`battle.v4.json:8`
`noteV2`), so a press-turn feel is a **second row**, `delve-press`: `WScope.PerSide`,
`NewEconomy = () => new PressTurnEconomy(pressIcons)` (`TurnEconomy.cs:113-124`, already `PerSide`-scoped),
key `timeline.profiles.delve-press.pressIcons` under the same loud-loader rule `maxPoints` has. `Build`
(`:260-262`) gains an economy selector in place of `bool points`; `hybrid-atb`'s `maxPoints` check keeps its
exact wording. v1 maps **every** encounter anchor to `delve` — the host resolves the constant, no anchor field exists
(`spec-dungeon-seed-contract.md` §1.6 carries none). A `profileId` on the anchor is a seed-contract schema
change, ask-first, and is needed only when `delve-press` content exists.

### 2. The explicit `Resolve` call

The host resolves `BattleModeProfileCatalog.Resolve(BattleModeProfileCatalog.DelveId)` — the id the anchor
maps to (`delve` for every anchor in v1); a known id resolves, an unknown id throws (`:323`), `null` is
**not passed** — applies the encounter's `W` with the same `with { W = w }`
`WaveCatalog.ProfileFor` uses (`WaveCatalog.cs:84`), and calls `BattleEngine.Resolve(setup, seed, trace,
profile: delve, actionCatalog:, containerResolver:, intentSource: raidSource)` (`BattleEngine.cs:180-183`; the working tree adds a ninth `board` parameter).
Never `WaveCatalog.ProfileForExpedition` — it throws on `RequiresLiveInput` (`:68-72`) — and never
`WebMatchService.ProfileForWave` — `IsKnown(waveId)` is false for a room, so it returns `null` (`:53-56`) and
`:228` silently runs `classic-round`. `setup.WaveId` for a delve fight is the encounter anchor id; nothing
in `WaveCatalog` knows it, which is exactly why the profile travels as a parameter.

### 3. The two seats and the policy

`IIntentSource.TryDeclare(actorKey, nowTick)` (`IntentSource.cs:29-37`) is one seam for both masters
(`:20-23`). A raid fight passes **one** source — `Resolve` takes exactly one (`:175`) — shaped like
`siege-ai`'s wrapper (`spec-siege-ai.md:84-100`, *"dispatching on `IBattleView.SideOf`"*):

- **Enemy side (`"wave"`):** always the automated policy — the `siege-ai`-class `IIntentSource` consumed from
  base-defense. `StubIntentSource` (`StubIntentSource.cs:27`) is the engine's basic-attack fallback and is
  **not** the raid policy; "same seed, same rewards" needs steered-by-autopilot and CI to run the *same*
  policy (audit §1(b) point 2).
- **Squad side, steered party:** `InteractiveIntentSource` live constructor (`InteractiveIntentSource.cs:39-50`)
  over a `BattleSession` (`BattleSessionRegistry.Open` `:93`): the player's `PlayerChoice` inside the T6 dwell;
  an elapsed window takes the fallback and records `DecisionSource.Timeout` (`:76-82`); reconnect is
  `Disconnect`/`Resume` (`:109-127`).
- **Squad side, un-steered parties:** the same automated policy as the enemy side, keyed by `PartyIndex`
  (§8). The wrapper's dispatch is `SideOf(actorKey) == squad && PartyIndexOf(actorKey) == steered ? live :
  policy` — a wrapper, no engine change.

AFK inside a delve does **not** abandon. `BattleSessionRegistry.NoteTurn` abandons at
`MaxConsecutiveTimeouts = 3` (`:67`, `:154-156`) because a web match nobody answers must end; decision 46
says a closed client *pauses, does not auto-resolve and does not forfeit* (`base-defense-ideal.md:232-233`).
The delve session layer therefore calls `Disconnect` (session preserved, trace intact, `:107-113`) at the
same count and records a `steer{party, to: none}` entry (§4) — the fight is frozen, not lost. `Abandon`
(`:131-137`) is reached only by an explicit extraction that leaves the fight (§6).

### 4. Freeze-on-switch and the two decision logs

**Freeze.** Switching steering away from party *p* mid-fight: the per-battle `DecisionTrace` is already
appended per decision (`DecisionTrace.cs:26-33`) and written per decision to the match row (§4b), so the
freeze is `Disconnect(matchKey)` plus one delve-level entry. **No finish-on-autopilot** — the un-steered
policy never takes over a fight the player started; the room stays "in progress" in `rpg_delve_rooms`
(`visited = 1, cleared = 0`). **Resume** re-runs `Resolve(setup, seed, …)` from the row's `setup_json` with
an `InteractiveIntentSource` that **replays the recorded prefix, then goes live**: the class has a live
constructor and a replay constructor (`:39-63`) but no replay-then-live mode, so this module adds a third
constructor `(fallback, ask, envelopeOf, recorded)` that returns `Replay(actorKey)` until
`DecisionTrace.ReplayExhausted` (`:48`) and asks afterwards — one branch on a bool already in the class.
Because a timeout is a decision at a tick (`:14-19`), the resumed fight is byte-identical to the
uninterrupted one up to the freeze and continues from the same queue state.

**4a. The delve-level log** — `rpg_delves.decisions_json` (`spec-delve-scope.md:73`), appended through
`AppendDecision` (`:272`), one JSON array of `{seq, kind, partyIndex, tick?, payload}`; kinds:
`enter` (seq 0, domain-catalog §6), `route` (door taken), `pack.move`/`pack.drop` (loot-pack), `talk` (event-deck and
wild-room answers, `payload.surface` discriminates), `supply.use` and `object.{verb}` (supplies-and-objects), `steer{from,to}`,
`retreat{roomId}`, `extract`. Ordered by `seq` only — the delve has no clock. Room-level rolls read the
sealed seed, so `(delve seed, this log, every room's battle trace)` is the whole run.

**4b. The per-battle trace** — T10's `rpg_web_match_log.decisions_json` (`RpgStore.cs:605-610`, selected at
`RpgStore.WebMatches.cs:177-182`, **no writer anywhere in `src/`**). A delve fight is a web match row like
an expedition battle (`spec-expeditions.md:53`: correlation `exp:{id}:{n}`) — correlation
`delve:{delveId}:{r}:{c}:p{partyIndex}`, match key `delve-{delveId}-{r}-{c}-p{partyIndex}` (colon-free,
same reason). This module is the column's **first writer**: `RpgStore.WriteWebMatchDecisions(id, json)`
called after every `Record`, with `DecisionTrace.ToJson()` (`:79`). Replay = `(setup_json, seed,
decisions_json)` through the replay constructor (`:53-63`), as `WebMatchService.cs:225` already reads it
with `FromJson` (`:86`).

### 5. Carry-in and carry-out — the golden argument

| Field | Type | JSON attribute | Which hash set *could* move | Why it does not |
|---|---|---|---|---|
| `BattleActorSetup.CurrentHp` | `long?` | `[JsonIgnore(Condition = WhenWritingDefault)]` | the four expedition tier hashes — `ExpeditionResolution` serialises every `BattleSetup` (`ExpeditionResolverTests.cs:30-31`, `:214-217`) | null for every expedition and golden squad builder, so the key is absent (`BattleModels.cs:81-89` precedent). Engine reads `Hp = CurrentHp ?? MaxHp`; `null` is today's line exactly |
| `BattleActorSetup.PartyIndex` | `int?` | same | same set | same argument; `WebMatchService.cs:397-415` and `WaveCatalog` never set it |
| `BattleActorSetup.RankSpan` | `int?` | same | same set | same; read by nothing in v1 (`encounter-generator`'s 1-D rank, R1) — carried so the board adoption is a reader, not a field change |
| `BattleActorSetup.ThetaActor` | `int?` | same | same set | §7 |
| `BattleActorSetup.CarryInPools` | `IReadOnlyDictionary<string, long>?` | same | same set | null on every existing caller. The **five non-hp** pools (`delve-attrition` §2); hp rides `CurrentHp`. Engine seeds `ActorResourcePools.FromStored(pools + {hp: CurrentHp ?? MaxHp})` only when non-null |
| `BattleActorSetup.PhaseGrants` | `IReadOnlyList<PhaseGrant>?` (`{hpThresholdMilli, containerInstanceId}`) | same | same set | null on every existing caller. Written by `encounter-generator` §5 for the boss role; the engine's hp-threshold check (`hp * 1000 < threshold * maxHp`, `long`) and `Host.Bag.Grant` + `RecomposeDerived` at the crossing (`BattleRunState.cs:445`, `:157`) run only when the list is non-null |
| `BattleActorSetup.GrantedContainerIds` | `IReadOnlyList<string>?` | same | same set | null on every existing caller. Elite affix instance ids (`encounter-generator` §6), granted at `BindContainers` through the existing grant block; a null list is today's line |
| `BattleActorResult.PartyIndex` | `int?` | same, **init property** after the positional list (`:311-313`) — the `EquippedActionIds` tail precedent (`:327-328`) | the four battle hashes — `BattleGoldenTests.Hash` serialises `BattleReport` (`:144-149`) | absent for every golden actor. **Positional would move all four**: a positional parameter serialises as a key on every actor |
| `BattleActorResult.CarryOut` | `DelveCarryOut?` (`Statuses`, `Shield`, `Retreated`) | same, init | the four battle hashes | populated only when the setup carried `PartyIndex` — a data condition, not a profile branch; null for every golden |

**Carry-in** for the next room: `CurrentHp` from the previous `HpRemaining` — **the one hp seat**; the other
five pools ride `CarryInPools` (`delve-attrition` §2 asserts `pools["hp"] == HpRemaining` at carry-out); `InitialStatuses`
(`BattleModels.cs:67`) and `InnateShield` (`:71`) already carry — the host maps `CarryOut.Statuses` →
`InitialStatuses` and the remaining shield → `InnateShield`. **Carry-out** lands in `parties_json`
(`spec-delve-scope.md:72`) per member: `hp`, `statuses[]`, `shield`, `downedOnce` (`delve-attrition` owns the
meaning). Resource pools are `delve-attrition`'s seat, not this module's.

### 6. `Retreated` producers

`Retreated` exists (`BattleEngine.cs:63`, `Active => Alive && !Retreated` `:68`) and has one producer:
`CheckRetreats` for `coward` (`BattleRunState.cs:663-676` — flag `:671`, `Status.WithdrawEntity` `:672`,
`Shields.RemoveAll` `:673`). Those three lines become `BattleRunState.Withdraw(ActorState)`; `CheckRetreats`
calls it, and two new callers do:

1. **Capture success** — `wild-room`'s corpus action resolves `Withdraw(target)` on the enemy actor: leaves
   alive, no die event, no `KillEarn` (ideal §11.6).
2. **Player-ordered retreat** — a `retreat` decision on the steered party (`Relation = Self`, always usable)
   withdraws every active squad actor of that `PartyIndex` and appends `retreat{roomId}` to the delve log.
   With no active squad actor the engine's outcome is `Defeat` (`:530-531`); the host reads
   `Outcome == Defeat && every squad actor Retreated && none dead` as **retreated** — the report stays as the
   engine wrote it. A `BattleOutcome.Retreated` value is ask-first (enum on every report). Extraction is
   **raid-wide** (S2-16): a retreat leaves the room, not the delve; the party holds at the nearest rest.

### 7. The actor-side Θ field

`BattleStatComposer.cs:108` reads `int theta = setup.Index;` and `Index => Level` (`BattleModels.cs:23-24`),
filled from `s.Actor.Level` at `WebMatchService.cs:407`; `BaseHp/BaseAtk/BaseDefense(level)` (`:407-409`,
`BattleModels.cs:218-221`) read the same `Level`. `difficulty-ladder` §7 leaves the seam to this module. Two
readings:

- **(a) Pass composed `Θ_actor` as `Level`.** No new field; but `BaseHp(Θ_actor)` moves too — a demon's hp
  ladder would jump with Dave level and runs, which is `power-index` hydration's decision
  (`spec-power-index.md` §2.5), and `Level` is the serialised name every tier hash locks (`:15-22`).
- **(b) Add `int? ThetaActor`** (`WhenWritingDefault`, §5 table). **Recommended.** `BattleStatComposer.cs:108`
  becomes `int theta = setup.ThetaActor ?? setup.Index;` — null is today's line; hp/atk/def stay on `Level`.
  The delve host fills it from `IPowerIndexProvider.ActorIndex(StatContext)` (`IPowerIndexProvider.cs:15`,
  `HydratedPowerIndexProvider` `:50`) per member. Contests read Θ, magnitudes read `P(Θ)` — the field keeps
  the two axes separable, which (a) cannot.

### 8. Sides and `PartyIndex`

The engine's side literals are `"squad"` and `"wave"` (`AnyActive` `:275`, `:530-531`, `:582`); allied
parties **share `"squad"`** and differ by `PartyIndex`. The economy key is `"side:" + Side` under `PerSide`
(`EconomyKey`, `:386-387`). **Decision: `PerSide` scope keys on `PartyIndex` when present** —
`"side:squad:p{PartyIndex}"`, `"side:wave"` otherwise — so each party owns its press-turn pool exactly as it
owns its pack, pity and route (R11's spirit), and a party's fight has the same economy alone or at the boss
rendezvous. Gated on `PartyIndex is not null`, so no golden changes shape. Commander auras
(`ActiveCommanderAura.CommanderSide`, `BattleModels.cs:279`) stay side-keyed: one commander, one raid.

### 9. Replay and the boot sweep

Replay of a delve fight is `(setup_json, seed, decisions_json)` plus the profile. **The sweep has a hole
for delve rows:** `WebMatchService.IsInteractive` (`:33-40`) asks `ProfileForWave(setup.WaveId)`, which is
`null` for an anchor id, so a delve fight would read as non-interactive and be **re-resolved under
`classic-round` with no trace** — the exact substitution T10 exists to refuse (`RpgStore.cs:607-609`). Fix,
off `BattleSetup`: `rpg_web_match_log` gains `profile_id TEXT` via `EnsureColumn` (NULL = today's rows,
"content chose via `WaveCatalog`"); `IsInteractive` and the three `Resolve` sites (`:116`, `:168`, `:288`)
read `entry.ProfileId is { } id ? BattleModeProfileCatalog.Resolve(id) : ProfileForWave(...)`. Sweep
metadata beside `environment_stamp`, never hashed. An interactive row whose trace is null or whose replay
does not exhaust (`ReplayExhausted` false at battle end) is marked `sweep_refused` (`:215-227`), terminal.

### 10. `RulesetVersion` and perf

`RulesetVersion` stays **4** (`BattleModels.cs:172`): every engine edit here is gated on a field no golden
sets (`CurrentHp`, `PartyIndex`, `ThetaActor`), `Withdraw` is a lift of three existing lines, the `delve` row
is selected by no golden wave, and `classic-round`/`hybrid-atb` rows are untouched. A bump is earned by a
moved golden (`battle-timeline-map.md:105`); the test suite is the proof, not this sentence.

**Perf (T15):** `Resolve` is a batch resolver — `NextEventAdvance` drives the round boundary regardless of
the profile's `AdvancePolicy` (`BattleEngine.cs:216`, `:248`). A *played* delve steps
`FixedIncrement` live: the SignalR session drives ticks between dwells, so the cost is server-side per
session, not the injector frame (`spec-kernel-performance.md:19-29` budgets are the injector's). T15's own
open task (b) — *measure `FixedIncrement` resolve against the `NextEvent` baseline* (`battle-timeline-map.md:105`)
— is the number this module inherits; `galaxy-sync` shape is the pre-agreed fallback there and here.

## Tunables

`data/tuning/battle.v{n}.json` (`tools/tuning/publish.py battle …`, `:7`) — this module's rows:

| Key | Unit | Starting shape | Note |
|---|---|---|---|
| `timeline.profiles.delve.w` | slots int | 4 | loader-required (`BattleTuning.cs:137`, `:145`); **the host always overrides** with `formation.{pack,party}.w` or `raid.modes.*.bossW` (`spec-dungeon-registries.md:156`, `:119`) via `with { W }`; a test asserts the row's own `W` never reaches `Resolve` |
| `timeline.profiles.delve.{wReact,passQuantum,maxPoints}` | int / ticks / points | 1 / 1 / 2 | copied from `hybrid-atb` (`battle.v4.json:35-40`); `wReact` inert (§1) |
| `timeline.profiles.delve.{maxRounds,roundDurationMs}` | rounds / ms | absent → `ruleset.*` (50 / 1000, `:14-18`) | optional per profile (`BattleTuning.cs:127-134`) |
| `timeline.profiles.delve-press.{w,wReact,passQuantum,pressIcons}` | … / icons int | 4 / 1 / 1 / 4 | §1a; `pressIcons ≤ 0` throws (`TurnEconomy.cs:120`) |
| dwell `inputWindowMs`, `afkTimeoutMs` | ms | 1500 / 5000 | **T6's keys, inherited** (`spec-interactive-turns.md:41-42` names them tunable). Drift: no tuning file carries them and no code reads them today — an ask on T6/T11, not owned here |

Read from `dungeon.v1.json` / `encounter.v1.json` (owned by `dungeon-registries`): `raid.modes.*.{parties,
squadSlots,bossW}`, `formation.{pack,party}.w`. `BattleSessionRegistry.MaxConsecutiveTimeouts`
(`:67`) stays a structural `const` (a turn count bounding an unbounded wait, with its exemption comment).

## Numeric types

| Quantity | Type | Why |
|---|---|---|
| `CurrentHp`, `HpRemaining`, shield amounts | `long` | magnitudes `P(Θ)` can touch (`MaxHp` is `long`, `BattleModels.cs:42`) |
| `PartyIndex`, `RankSpan`, `ThetaActor`, `W`, `maxPoints`, `pressIcons` | `int` | indices, counts, Θ (a linear index; `ContentContext` is `int`) |
| ticks (`TracedDecision.Tick`, `PassQuantum`) | `long` | `1 tick = 1 ms` (`decisions.md:42`); `DecisionTrace.cs:23-24` |
| `seq` in the delve log, `delve_id` | `long` | monotonic counters |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle.BattleGoldenTests|FullyQualifiedName~Expeditions.ExpeditionResolverTests|FullyQualifiedName~Delve.Battle"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ModeProfile"      # binding + architecture tests, new row
dotnet test tests\FusionRpg.Data.Tests  --filter "FullyQualifiedName~WebMatch|FullyQualifiedName~Delve"
.\scripts\guard-dal.ps1; .\scripts\guard-funnel-delta.ps1
python scripts\audit-magic-numbers.py --domain battle
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs   → DelveId/DelvePressId rows, switch arms (the one allowed file)
src/FusionRpg.Core/Battle/Timeline/InteractiveIntentSource.cs → replay-then-live constructor
src/FusionRpg.Core/Battle/BattleModels.cs                 → CurrentHp, PartyIndex, RankSpan, ThetaActor, CarryInPools, PhaseGrants, GrantedContainerIds; result PartyIndex, CarryOut
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs   → DownedOnDeplete (true on delve, false elsewhere)
src/FusionRpg.Core/Battle/BattleRunState.cs               → Withdraw(ActorState); CheckRetreats calls it; PartyPools seeded from CarryInPools; PhaseGrants threshold check; GrantedContainerIds in BindContainers
src/FusionRpg.Core/Battle/BattleEngine.cs                 → Hp = CurrentHp ?? MaxHp; EconomyKey on PartyIndex; result tail
src/FusionRpg.Core/Battle/BattleStatComposer.cs           → theta = ThetaActor ?? Index
src/FusionRpg.Core/Delve/Battle/DelveBattle.cs            → Run(...) — the explicit Resolve call
src/FusionRpg.Core/Delve/Battle/RaidIntentSource.cs       → one IIntentSource dispatching on SideOf + PartyIndex
src/FusionRpg.Core/Delve/Battle/DelveCarry.cs             → CarryIn/CarryOut records and the setup mapping
src/FusionRpg.Core/Delve/Battle/DelveDecision.cs          → delve-level log entry kinds
src/FusionRpg.Data/Sqlite/RpgStore.WebMatches.cs          → WriteWebMatchDecisions; profile_id column + SelectLog
src/FusionRpg.Server/DelveBattleEndpoints.cs, RpgHub      → steer / declare / freeze / resume over SignalR
tests/FusionRpg.Core.Tests/Delve/Battle/, tests/FusionRpg.Data.Tests/Delve/
UNTOUCHED: WaveCatalog.cs, ExpeditionResolver.cs, WebMatchService.BuildSquad, BattleSetup
```

## Code style

Kernel discipline (`spec-kernel-adoption.md`): additive trailing parameters, `[JsonIgnore]` variants
load-bearing and commented, no profile id literal outside the catalog, no branch on `AdvancePolicyKind`.

```csharp
// DelveBattle.Run — the only place a delve fight is resolved.
var profile = BattleModeProfileCatalog.Resolve(BattleModeProfileCatalog.DelveId) with { W = encounter.W };   // never ProfileFor*
var report  = BattleEngine.Resolve(setup, seed, trace: null,
    profile: profile, actionCatalog: catalog, containerResolver: containers,
    intentSource: new RaidIntentSource(steered: steeredParty, live: interactive, policy: raidPolicy));

// BattleActorSetup — additive, absent when null, so no tier hash gains a key.
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long? CurrentHp  { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int?  PartyIndex { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int?  RankSpan   { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int?  ThetaActor { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public IReadOnlyDictionary<string, long>? CarryInPools { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public IReadOnlyList<PhaseGrant>? PhaseGrants { get; init; }
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public IReadOnlyList<string>? GrantedContainerIds { get; init; }
```

## Testing strategy (gate G2, `party-dungeon-map.md:158`)

- **Goldens:** `BattleGoldenTests` four hashes + 32-seed sweep and `ExpeditionResolverTests.Tier_goldens_are_locked`
  byte-identical with every new field absent; a serialisation test asserts a default `BattleActorSetup` and
  `BattleActorResult` JSON contain none of the nine new keys (seven setup, two result).
- **Positional guard:** reflection over `BattleActorResult`'s primary constructor asserts `PartyIndex` and
  `CarryOut` are not parameters (the test that would have caught a hash move at review time).
- **Carry-in:** a delve fight with `CurrentHp` set differs from one without **only** in starting `Hp` — same
  draws, same order (`BattleTrace` diff empty except the hp line).
- **Freeze/resume:** record N decisions, freeze, resume from `(setup, seed, prefix)` with the same remaining
  choices ⇒ `BattleReport` byte-identical to the uninterrupted fight; a `Timeout` in the prefix replays as a
  timeout.
- **Sweep:** an interactive delve row with a null or non-exhausting trace is `sweep_refused`; a row with
  `profile_id = "delve"` never resolves under `classic-round`.
- **Profile:** `ProfileForExpedition` still throws for a `RequiresLiveInput` wave; `ModeProfileTuningBindingTests`
  green for `delve`/`delve-press`; deleting `maxPoints` or `pressIcons` rejects by key name; the host never
  passes the row's own `W`.
- **Sides:** two parties on `"squad"` with `PressTurnEconomy` hold separate pools; the wave keys on side.
- **Retreat:** player retreat withdraws the party alive, host reads *retreated*; capture withdraws one enemy
  with no die event and no `KillEarn`; `coward` behaviour unchanged.
- **Untouched:** `WebMatchService` web-match path resolves exactly as before (`profile_id` null).

## Boundaries

- **Always:** `profile:` and `intentSource:` passed explicitly; the profile from the anchor id through
  `BattleModeProfileCatalog.Resolve`; every new setup/result field nullable + `WhenWritingDefault`; the
  per-battle trace written per decision; a switched-away fight frozen; SQL in `FusionRpg.Data` only.
- **Ask first:** `BattleOutcome.Retreated`; a third profile row; flipping `UsesTimelineDispatch` on `delve`;
  any `Level` semantics change (reading (a) in §7); a per-party commander aura.
- **Never:** `ProfileForExpedition` or `ProfileForWave` for a delve fight; a profile id on `BattleSetup`; a
  positional field on `BattleActorResult`; `StubIntentSource` as the raid policy; finishing a switched-away
  fight on autopilot; a `RulesetVersion` bump without a moved golden; `AnyActive` or the side literals changed.

## Success criteria

1. G2's clauses green. 2. All eight hashes + sweep byte-identical, proven by the run. 3. A frozen steered
fight resumes byte-identical. 4. The sweep refuses an incomplete delve trace and never re-resolves one under
`classic-round`. 5. Guards green; `audit-magic-numbers --domain battle` reports no new M1.

## Interface exposed to dependents

`DelveBattle.Run(EncounterSetup encounter, BattleModeProfile profile, IIntentSource raidSource, CarryIn carry)`
→ `DelveBattleResult { BattleReport Report; IReadOnlyList<PartyCarryOut> CarryOut; DecisionTrace Trace; }`.
**`delve-attrition`** reads `CarryOut` (hp, statuses, shield, `Retreated`) and writes `parties_json`;
**`dungeon-loot`** reads `Report.Outcome`, kills and the retreated reading (§6) for `KillEarn`/no-`KillEarn`;
**`wild-room`** calls `Withdraw` through its capture action and reads the `remembers` outcome from the delve
log; **`delve-stage`** subscribes to the session (dwell, freeze, resume) and renders the initiative rail from
the same `BattleTrace` `TurnOrderRecord` web matches use (`WebMatchService.cs:118`).

## Drift found this session (report, not fixed here)

- `BattleEngine.cs`, `BattleModels.cs`, `BattleModeProfile.cs` are uncommitted (`git status`) and moved
  during the session: `Resolve` is at `:180-183` with a new `Board.BoardState? board` 9th parameter,
  `profile ??` at `:228`, `WReact` at `:238`, `OrdersBySpeed` `:370`, the `UsesTimelineDispatch` branch `:395`.
  This spec cites the **working tree**; the map and review cite HEAD (`:172-175`, `:220`, `:230`, `:377`) —
  quoted passages keep their original numbers.
- `hybrid-atb` carries a **staged, uncommitted** `with { UsesTimelineDispatch = true }`
  (`BattleModeProfile.cs:226-232`, battle-tempo LAND1 "measurement only"). If it lands, `hybrid-atb.wReact: 1`
  stops being inert *there*; the `delve` row declares its own `false`, so §1's inert claim holds for `delve`.
- `battle-clock-profile`'s fields are already on the record and loaded (`:153`, `:158`; `BattleTuning.cs:127-134`);
  the `siege` row exists (`:254-258`) — the map's precondition is satisfied in the tree.
- `CheckRetreats` is at `BattleRunState.cs:663-676`, not `:594-604`; `BattleActorResult` is `:305-323`, not
  `:252-268` (that range is `BattleSetup`); `RulesetVersion` is `:172`, not `:95`.
- `BattleEngine.cs:227`'s comment *"`WReact = 0` (every shipped profile)"* is stale (decision 50 already says so).
- T6's dwell/AFK ms are named tunable and exist in no tuning file and no code path (§Tunables).

## Design-gate checklist

```
[x] Subsystems: battle mode profiles, battle engine, interactive turns / decision trace / live sessions,
    web match log, power index, delve store.
[x] Read this session: party-dungeon-map.md (row 7, external deps, G2); the five wave-1 specs; ideal §2.3,
    §4.4, §4.8, §11.10 R9; audit §1(b) in full, S1-8, S2-11, S2-15, S2-16, §2; BattleModeProfile.cs,
    battle.v4.json, BattleTuning.cs, BattleEngine.cs, WaveCatalog.cs, WebMatchService.cs, IntentSource.cs,
    InteractiveIntentSource.cs, BattleSessionRegistry.cs, DecisionTrace.cs, StubIntentSource.cs,
    RpgStore.WebMatches.cs, RpgStore.cs, BattleModels.cs, BattleRunState.cs, BattleStatComposer.cs,
    TurnEconomy.cs, IPowerIndexProvider.cs, BattleGoldenTests.cs, ExpeditionResolverTests.cs,
    spec-interactive-turns.md, battle-timeline-map.md, base-defense-ideal.md decision 46,
    base-defense-map.md, spec-siege-ai.md, spec-battle-clock-profile.md, decisions.md rows 42/50,
    spec-expeditions.md.
[x] Every code claim cites file:line opened today; drift reported in its own section.
[ ] "No golden moves" is argued from the two hash functions' inputs, not yet run — there is no code to run
    it against; the suite is the first build task.
[x] No §2 invariant contradicted: no profile on BattleSetup, no ProfileFor* path, no Step, no injector work.
[x] Corrections propagated within this spec (Tunables, Testing, Boundaries, Interface, Drift).
```
