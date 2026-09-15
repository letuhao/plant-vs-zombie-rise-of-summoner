# Lawn combat wire — the ideal

**Status:** idea phase, 2026-09-13 (revised after a three-pass adversarial audit). Not a spec. No
build authorized.

**Requested as two sub-programs of the SOLID run — "Lawn Run" and "Battle Engine Wire". The first
name stands; the second is retired:** `BattleEngine` is the wrong component and cannot sit on a lawn
frame (§4.1). The accurate name for that half is **lawn hit wire**.

> **Revision note.** The first draft of this doc claimed the feature was "one guard plus a grant".
> That was wrong, and an audit round found it. It is recorded here rather than quietly fixed, because
> the wrong version was briefly the basis for a module split. Corrections are marked **[audit]**.

---

## Which loop this extends

[The loops](../guide/the-loops.md) **1. Lawn — first core**, plus the cross-cutting **Combat depth**
section, which already lists *"element ring, shields, statuses, crit"* as **Already in**. No new
loop, no parallel pitch, no fourth stock, no player class, no stamina gate on the *player*.

---

## Why this exists

On a live board, 2026-09-13, a real specimen minted/levelled/allocated/deployed through real
endpoints reached `phase: ActiveBound` with a real Unity ptr and resolved `bonusAtk 222` /
`bonusMaxHp 1110`, written through to `attack 223 hp 1410 maxHp 1410` on the actual board. **The RPG
stat layer reaches the lawn.**

The same entity carried `combat.power.omni 32`, `combat.defense.omni 4`, `combat.crit.rate.omni 75`
— funded, resolvable, and touching nothing. The owner's summary was exact: a peashooter with 5000
omni attack power has a number that is "just for fun".

**[audit] The live measurement that seemed to prove this was invalid.** `EventDrainHost.Active =>
Enabled && !DebugRuntime.SessionActive` (`EventDrainHost.cs:32`), and every event captured in that
probe carried a `scenarioId`, meaning a debug session *was* active and the record path was off by
design throughout. The conclusion still holds — §3's blockers each suffice independently — but it
holds on code reading, not on that measurement. **Any live probe of this feature must run outside a
debug session**, or it exercises the legacy path and proves nothing. This is the same trap as the
2026-09-13 T14 incident.

---

## Load-bearing principles, restated inline

A downstream session reads this doc, not its links.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The
   narrow Unity write surface constrains only what a *persistent vanilla stat change* may touch.
2. **Two async systems; deltas, never absolutes; record-then-drain.** Hooks record a struct and
   return; effects resolve later in a budgeted drain. Delay is the designed degradation mode.
3. **The Server never sits on the hit path.** `overlay-control-loops.md:87`: *"Server FSM … must not
   sit between `combat.hit` … and FA* apply."* Hot rule 3 (`:150`): *"**Never await** SignalR, HTTP,
   or SQLite for the roll or apply."* **The RPG layer hosted in-process on the injector may, and
   already does** — `EffectBag.cs:567,637` calls `CombatDamageDispatcher.DispatchInstant` at hit time.
4. **One ActorHub compose / one read.** No private fold, no second composer.
5. **One power ladder.** Contests read `Θ`; magnitudes read `P(Θ)`. `action-ideal.md` §1.3 already
   rejected a second curve for actions.
6. **The balance surface is data**, in `data/tuning/<domain>.v{n}.json`.
7. **No hard progression ceilings.** Caps on magnitudes are soft; absolute bounds **throw, never
   clamp silently**. Structural per-frame caps are exempt and must say so.
8. **`long` for integer magnitudes; floating-point allowed** (owner ruling 2026-09-15). Widen before
   multiplying; divide by 1000 last, exactly once, in integer per-mille math; integer overflow throws.
9. **Unbuilt means build it.** Owner, 2026-09-13: *"when i ask for a feature mean you must pursuit
   how to fix it, not defer it because unbuilt."* A dependency being unbuilt is scope, not an excuse
   — this doc therefore designs the resource-regen fix rather than handing it to another program.

---

## The three blockers, and why none is an architectural wall

**[audit] The first draft said the feature was one guard. It is not.** Three independent things stop
a vanilla hit from producing RPG damage. All three are wiring.

### Blocker 1 — the hit is gated four times, not once

```csharp
EventDrainHost.cs:32   static bool Active => Enabled && !DebugRuntime.SessionActive;
EventDrainHost.cs:48   if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;   // bullet
EventDrainHost.cs:72   if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;   // melee
GameHooks.cs:750       ...else if (EventDrainHost.Enabled)    // reached only AFTER a debug/telemetry Emit branch
```
Same caller-side ordering at `GameHooks.cs:870` and `:1039`.

- `Enabled` is **default ON** — `InjectorLoop.cs:197-198` sets `Enabled = !off` where `off` requires
  `FUSIONRPG_EVENT_V2=0`. The class header still says *"Off (default until Task 10 wires the tick)"*;
  that comment is **stale** and should be corrected (code beats comments).
- `!DebugRuntime.SessionActive` is the one that matters for verification, not for normal play.
- `HasOnDamageDealtGrant()` is the designed guard — and §4.4 is how it becomes true.

### Blocker 2 — the attacker was the bullet · **RESOLVED: one field** ✅

The record stores `actorPtr: bullet.Pointer` (`EventDrainHost.cs:61`), passed through as `ActorPtr`
(`EventDrain.cs:453`), and `OverlayCombatMath.Finalize` resolves the attacker from exactly that ptr
(`OverlayCombatMath.cs:50-51`) → `InjectorCombatBridge.ResolveActor(bulletPtr)` → no ActorHub
baseline → the literal stub `{Hp=100,MaxHp=100,Atk=10}` (`InjectorCombatBridge.cs:57-59`) and a
Neutral element. So the shooting plant's `combat.power.omni` is never read, and — because
`EffectProcAndOwner.MatchesEvent` (`:106-116`) matches `entity:` keys against `ev.ActorPtr` /
`ev.TargetPtr`, which are the bullet and the victim — **an `entity:{ptr}` grant could never fire on a
projectile hit at all.**

**This looked like a real gap. It is a wiring gap.** The game's own `Bullet` type carries the firing
instance, verified by reading `Assembly-CSharp.dll`'s metadata directly:

```
Il2Cpp.Plant      from            <- the firing plant INSTANCE
Il2Cpp.Zombie     from_zombie     <- the firing zombie instance
Il2Cpp.PlantType  fromType        <- the type enum; the ONLY one the injector reads today
System.Boolean    shootByZombie   <- which side fired
```

The injector reads only `fromType` (`EventDrainHost.cs:57`, `GameHooks.cs:972`,
`GrantedBulletModifyAtoms.cs:37` which keys `EffectOwnerKeys.PlantType`). Recording
`bullet.from?.Pointer` (or `from_zombie` when `shootByZombie`) instead of `bullet.Pointer` **dissolves
both halves of this blocker at once**: the attacker resolves to a real ActorHub-composed plant, and
`entity:{ptr}` grant matching starts working, which is what makes §4.4 implementable.

Melee never had this problem — `TryRecordMeleeDealt` already records the real `attackerPtr`.

### Blocker 3 — `elementPayload` is a hard precondition, not decoration

`OverlayCombatMath.Finalize:42-43` returns the amount **unchanged** when the payload is null/empty —
power, defense, crit, accuracy, penetration all skipped. `decisions.md`'s combat-damage row states
the same lock ("pass-through when no payload"). The bake fires only when the owner has a resolved
`ownerElementPrimary` **and** the atom group contains `ApplyResourceDelta` (`AtomCompiler.cs:237-243`).

**[audit] Consequence the first draft got wrong:** `OverlayCombatCalculator.cs:110-126`'s omni
fallback is **unreachable from the lawn**, and the first draft cited it as shipped lawn math. A
Neutral-element species contributes exactly zero. So the basic-attack grant must carry a real
`elementPayload`, which it can — E27 `lawn-element-bind` landed 2026-09-03 and
`LawnElementResolverHost.Resolve(ptr)` is live.

---

## What already exists — three buckets

### Built (and reachable from the lawn)

| Thing | Evidence |
|---|---|
| `ActorHub` composes for **every** lawn actor, general creatures included | Proven live: a plain `debug.spawn-zombie` NormalZombie picked up `bonusMaxHp 480` / `bonusAtk 120` from species progression with no `UniqueActor` row |
| `progression.bonus.*` → `EntityStatWriter` → vanilla fields → PvZ's own damage math | Live: pea 1350, bite 170. Writer sets **13 fields** incl. `attackDamage` (`EntityStatWriter.cs:47-81`) |
| The overlay damage stack, gate **default-on** | `EffectRuntime.WireCombatMath` (`:505`); `OVERLAY-COMBAT` true at `CheatSchema.cs:105`, `CheatRegistry.cs:80`, `CheatState.cs:379`. **[audit] Note the env half (`FUSIONRPG_OVERLAY_COMBAT`) defaults false — cite the schema default, not the env var; two review passes read this file oppositely** |
| Overlay combat math, locked | `attackerPower(E) = combat.power.omni + combat.power.E` vs `defenderDefense(E)`; crit/accuracy/penetration/absorption/parry/block/reflect shipped — `combat-damage-ssot.md` §5-§6 |
| Element vocabulary, closed: 6 (Fire, Ice, Air, Earth, Light, Dark) + `omni` baseline | `ActorElementTypes.cs:3-11`; `ElementRoster.Concrete` `:21-29` |
| Element ring math | `ElementHub.ResolveComponentBonus` (`ElementHub.cs:9-24`); multiplier at `ElementRingMatrix.cs:34-40,46-47`. STR **1.25** / WEK **0.75**, *derived* from `k = 0.25` |
| Element resolution **by ptr** on a live entity | `LawnElementResolverHost.Resolve(ptr)` (`:27-32`) → `InjectorCombatBridge.ResolveActor` |
| `elementPayload` baked from owner species onto grants | `AtomCompiler.cs:237-243` ← `AtomPushService.cs:293`; parsed at `DamagePacketBuilder.cs:48` |
| Elemental VFX, per-element palette, default-on | `ElementFxPalette.cs:11-20`; rides `DamageFxDto.Elements` → `VfxCueMapper.cs:17` → `VfxDirector.cs:201` |
| **The firing instance is available on the bullet** | `Il2Cpp.Bullet.from` / `.from_zombie` / `.shootByZombie` — verified from `Assembly-CSharp.dll` metadata |
| Cost model, `CostLedger`, all six pools | `ActionRow.cs:141`; `CostLedger.cs:85-100,106-138,145`; `ActorResourcePools.cs:13,64,93,113` |
| **"No resource, no trigger" is real, and gates at _declare_** | `UsabilityEvaluator.cs:66` → `CostLedger.Check` → `UsabilityReason.CannotAfford` (`CostLedger.cs:96`); real ledger passed at `BasicAttack.cs:163-166` |
| Lawn resource pools exist, keyed by combat ptr, lifecycle-cleaned | `LawnActorResourcePools.cs:26`; `InjectorEntityRegistry.cs:51,127,138` |
| Record-then-drain, budgeted, same-target coalescing | `EventDrainHost.cs:145-157`; coalesce `event-pipeline-v2-ssot.md:52` |
| Pointer-reuse ordering: die emits before grants withdraw | `GameHooks.cs:664/676`, `:1154/1156` |
| `resource.max.{id}` is **derivable** — `BaseHp(theta) × poolShareMilli[id] / 1000` | `ResourceBaselineSubsystem`; shares in `data/tuning/battle-resources.v1.json` |

### Built elsewhere — **not** on the lawn

**[audit] The first draft filed these as "Built" without the qualifier.**

| Thing | Where it is built | Why it does not count here |
|---|---|---|
| `ActionKind.Basic` / `act.attack`, sealed | `action-ideal.md:40` D1, `:60` D25; `BasicAttack.cs:28,30`; fallback at `TimelineDispatch.cs:209,270` | Battle/delve/siege/sim only |
| Basic attack computing elemental damage via `OverlayCombatCalculator` | `BasicAttack.cs:364-380`; element from `BattleEngine.cs:48-53` | Path ends at `DamageApplyPipeline.Apply` → `IHpDeltaSink` — **no Unity end** (§4.3) |
| `CostLedger` enforcement | battle | `grep CostLedger src/FusionRpg.Injector` → **0 hits** |

### Wiring gap

| Gap | Inert line |
|---|---|
| The vanilla hit is turned away — no on-damage-dealt grant is ever bound | `EventDrainHost.cs:48`, `:72` |
| **Attacker recorded as the bullet, not the shooter** — `bullet.from` never read | `EventDrainHost.cs:57,61` |
| `resource.max.*` is **0 for every lawn actor** — `ResourceBaselineSubsystem` is opt-in and the injector's Hub never opts in | `ActorHub.cs:147` `seedResourceBaseline = false`; sole `true` caller `UniqueActorHubCompose.cs:75`; injector Hub `CheatState.cs:49` |
| No `CostLedger` caller anywhere in the injector | — |
| Basic attack's costs are a **hardcoded empty array**, not data | `BattleRunState.cs:81` `Costs: Array.Empty<CompiledActionCost>()` |
| `CostLedger.Check` passes vacuously when an action has no rows | `CostLedger.cs:99` (`RowsFor` returns empty at `:68-70`) |
| **`CostLedger.Check` only inspects `OnCommit` rows** — an `OnDeclare`-timed cost is invisible to the declare gate | `CostLedger.cs` `Check` |
| The action stack is unreachable from the lawn | `grep FusionRpg.Core.Actions src/FusionRpg.Injector` → **0 files** |
| `LawnElementResolver._cache` freezes `(side, elements)` per ptr for a whole match; `zombie.hypno` never invalidates it | `LawnElementResolver.cs:23,43-67`; `GameCaptureHooks.cs:283-294` |
| 155 of 179 action briefs never load | `Program.cs:402` |
| `UpsertSpeciesBasics` / `GetSpeciesBasics` — zero production callers | `RpgStore.Actions.cs:650,673` |
| `type-weights.json` (1,131 entries), `species-innate.json` (904) — zero `src/**` readers | — |
| `ActionSeeder.Generate` — zero production callers; the code asserts it (`ActionCorpusProducerLanded = false`) | `ActionSeeder.cs:30,32`; flag `ItemGrantedActionRow.cs:111-119` |
| Corpus composer cannot emit Basic/Innate — forces `Skill`. **[audit] the first draft cited `ActionCorpusComposer.cs:149` for dropping a `kindHint`; that citation was fabricated — `kindHint` appears nowhere in `src/**` except a doc comment at `ActionCorpusBriefJson.cs:16`. The effect needs re-deriving before a spec relies on it** | re-verify |
| ~~`combat.*` excluded from `MergeAppliedCombat`~~ — **correct by design, not a gap** (D1) | `ActorHub.cs:89-113` — keep |

### Real gap — and each one is designed below, not deferred

| Gap | Design |
|---|---|
| **Regen cannot be expressed** — `RegenPerTick` rounds to a whole `long` (`ResourceChannelReader.cs:19`), so on a several-hundred-tick round the smallest non-zero rate accrues ~300 against a spend of 100 | §4.6 — build the sub-tick unit the tuning file itself names as follow-up **S10.1** |
| `BaseResourceRegen => 0` | `BattleModels.cs:412`. **Not a design position against regen** — the `_meta.regenIsAbsentOnPurpose` note names the rounding above as the reason. Fixing §4.6 makes a baseline expressible |
| Numerics violate the repo's own rule | §4.7 |
| Death during a deferred hit; drop policy; multi-hit | §4.8 |

---

## Prior art

Already in-repo — do not re-research: `docs/research/game-design/01-typing-matrices.md` (Pokémon
18×18: 8 cells 0×, 61 at 0.5×, 51 at 2×, 37% non-neutral; SC1; WC3 7×6; AoE2's 38 armour classes;
and the conclusion that **four AAA franchises deleted N×N matrices and none returned** — a warning
against *growing* our 6×6, not against using it). `effect-runtime/05-icd-audit.md` holds our own ICD
behaviour.

**Proc coefficient is the shipped answer to "many hits per second".** D3 normalises to *"the number
of hits each skill does at exactly 1.00 attacks per second"*; RoR2 defaults to **1.0** with fast
weapons below — MUL-T Nailgun **0.6**, turning a 10% rider into 6%/hit
([RoR2](https://riskofrain2.wiki.gg/wiki/Proc_Coefficient),
[diablowiki](https://www.diablowiki.net/Proc_rate)). The failure both prevent is ours exactly: **a
rider priced per-hit is trivially maximised by raising fire rate**, a different axis from the one it
was balanced on — the same defect `action-taxonomy/03-composable-skill-systems.md` §11 catalogues as
*"the priced thing and the powerful thing were not the same thing"*.

**Ring band.** Shipped rings sit in **0.5×–2.0×**, 0× rare (2.5% of Pokémon cells). Our 1.25/0.75 is
inside it and conservative. Counter-example: Diablo II's flat percentage resistance, player cap
**95%**, Hell **−100%**, monsters at **≥100%** outright immune — a binary wall that made off-element
builds unplayable and needed break-immunity items ([Maxroll](https://maxroll.gg/d2/resources/damage-reductions)).
**Keep multiplicative shares; never introduce an immunity threshold.**

**Already-dead targets: a death phase, not per-hit checks.** MTG resolves lethal damage as a
state-based action with simultaneous deaths as one event ([rule 704](https://mtg.fandom.com/wiki/State-based_action));
Hearthstone collects deaths into an explicit Death Phase ([Advanced rulebook](https://hearthstone.wiki.gg/wiki/Advanced_rulebook)).
The observed bug class is **on-death effects firing repeatedly** on an already-lethal target. **[audit]
The first draft claimed "our equivalent already exists in shape" — it does not; see §4.8.**

**Basic attack as a real table row is the majority pattern** — WoW's auto-attack is genuinely spell
6603; League defines it as 100% of total AD. The documented failure of the hardcoded variant: it
*"cannot be buffed, elementalised, or replaced by equipment without a special case per feature"* —
which validates `ActionKind.Basic` over a bespoke lawn fallback.

Not found this session: any published per-hit CPU budget for tower defense; any post-mortem on
deliberately deferred damage in single-player; any measurement of player-perceptible thresholds for
HP-bar jump or damage-number ordering; PoE's 75%/90% caps (unverified); any prior art for an overlay
mod adding an elemental layer to a host game.

---

## The shape

### 4.1 The requested loop, corrected

Requested: `PvZ → Injector → FSM → Battle Engine → Actor Hub → Battle Engine → FSM → Injector → VFX
→ damage`. Two defects.

**`Battle Engine` is the wrong component.** `BattleEngine` (`BattleEngine.cs:25`) is a pure
deterministic **batch** resolver — *"No I/O, no clock, no ambient state"* (`:14-24`), *"`Resolve` is a
batch resolver, **not a live per-frame loop**… which nothing here does"* (`:167-170`) — with **zero
injector callers** (sole grep hit is a comment, `InjectorCombatBridge.cs:51`).
`battle-turn-ideal.md:24` settles it: for PvZ realtime the clock owner is *"**The Unity game. Not
us.**"*, and mode 1 is *"an **adapter, not a scheduler**"*. The component that does this job exists
and is wired: `CombatDamageDispatcher → OverlayCombatCalculator → ShieldGate`, from `EffectBag.cs:567,637`.

**`FSM` does not cover general creatures.** A PvZ-spawned plant has no `instanceId`, no correlation,
therefore no `UniqueBinding` row — it exists only as `MatchState.Plants[ptr]` (`match-runtime.md:367-370`).
`DESIGN-GATE.md:47`: *"General creature = PvZ-engine-spawned, species-stats-only, **no persistent
instance**."* **The board fold is the universal per-actor structure; key on it, and treat a binding as
enrichment when present.**

**Corrected loop:**

```
PvZ lawn hit
  → Harmony hook RECORDS a struct and returns            (never computes in the hook)
  → budgeted drain, ≤10% of frame                         (EventDrainHost)
  → attacker = bullet.from / from_zombie / melee attackerPtr    [blocker 2 fix]
  → MatchRuntime board fold (+ UniqueBinding IF a Bound specimen)
  → EffectBag.OnEvent, grant matched on entity:{attackerPtr}
  → CostLedger: pay stamina, or contribute nothing        [D6]
  → ActorHub-composed derived for attacker and defender
  → CombatDamageDispatcher → OverlayCombatCalculator → ElementHub → ShieldGate   [in-process]
  → EffectFunnel (signed delta, never absolute, never mode=set)
  → flush barrier, re-entry depth 0 → FA10 EntityStatWriter Add (Die if HP≤0)
  → elemental VFX from DamageFxDto.Elements
  → Server observes asynchronously — NEVER a decision gate
```

### 4.2 Where the gate's summary is broader than its source

`DESIGN-GATE.md:33` says the RPG *"does not compute damage at the moment of the hit."* Read
literally that forbids this feature. **It is broader than its source.** `overlay-control-loops.md:22`
gives the real rule: Unity owns physics/lifetime, and the RPG overlay's *"hot evaluation must not
require a Server round-trip."* **[audit] The first draft cited `overlay-control-loops.md:126-139` as
showing the Hot loop computing damage; that block contains no damage math.** The rebuttal rests
solely on `EffectBag.cs:567,637`, which is confirmed. **Restated: the Server does not compute damage
at hit time; the in-process RPG layer does.** That gate line should be narrowed.

### 4.3 Two damage paths, converging late — know which you are extending

```
ACTION path (battle):  ApplyBasicAttack → OverlayCombatCalculator → SignedDelta
   → DispatchHit (BattleRunState.cs:1136) → ApplyHp (:869) → DamageApplyPipeline.Apply (:874)
   → ShieldGate.AbsorbFinalized → IHpDeltaSink        ← NO Unity end

EFFECT path (lawn):    EffectBag.OnEvent → DamagePacketBuilder.FromOverlay (:11)
   → CombatDamageDispatcher.DispatchInstant (:12) → math (:41)
   → DamageApplyPipeline.ApplyPacketToFunnel (:47) → Funnel → FA10 Writer Add
```

They share `OverlayCombatCalculator` and `DamageApplyPipeline` by two entry points. **The lawn wire
uses the EFFECT path.** The two already touch: `ApplyBasicAttack` raises `OnDamageDealt` into the bag
(`BasicAttack.cs:397-406`) — the seam this feature generalises.

### 4.4 The basic attack is a grant — and blocker 2's fix is what makes it possible

`HasOnDamageDealtGrant()` is the guard. **The fallback basic attack is the grant that makes it true
for every lawn actor.** Bind at spawn through the existing Funnel path, owner key `entity:{ptr}` —
which only works once the record carries the *shooter's* ptr (§blocker 2). Element rides the existing
`elementPayload` bake.

Vocabulary needs no widening: `Kind=Basic, Category=Attack, Tag=Offensive, Mode=Single` all exist,
and `Mode=Area` is rejected at bind time while no board exists, so `Single` is also the only legal
choice.

### 4.5 Cost: make it data, not a hardcoded empty array

`BasicAttackCompiled` is a shared `readonly` field with `Costs: Array.Empty<...>()`
(`BattleRunState.cs:64-81`), and `costsByActionId` is built only from **held** actions
(`:558-565`) — which a basic attack never is (D2: basics cost no loadout capacity). So `act.attack`
cannot carry a cost by any data path today.

**Fix: make the basic attack's cost authored rather than hardcoded**, supplied per context. One
authority (`CostLedger`), one evaluator, different data — not a second gate, so no SOLID fork.
Because §4.6 makes regen expressible, **battle gets a real cost too**; this is not a lawn-only
carve-out.

### 4.6 Build the sub-tick regen unit — the repo's own named follow-up S10.1

`data/tuning/battle-resources.v1.json` `_meta.regenIsAbsentOnPurpose` states the problem and names
the fix:

> *"`ResourceChannelReader.RegenPerTick` rounds the channel to a whole long, and a battle round runs
> several hundred ticks … the SMALLEST expressible non-zero rate accrues ~300 poise per round against
> a spend of 100 … **A sub-tick unit is a named follow-up (spec S10.1)** and regen earns its rows
> then."*

**Design — integer per-mille carry, deterministic:**

- Regen is read and accumulated in **per-mille units per tick** in a `long`, never rounded per tick.
- Each pool carries a `long` remainder. On each tick: `acc += regenPerMilleTick`; whole units are
  `acc / 1000`; `acc %= 1000`. **Divide by 1000 last, exactly once** — principle 8.
- The remainder is **carry-preserving**, the same discipline `KernelDriveHost` already applies to
  *time* by re-arming off `e.DueTick` rather than "now" (`:186-192`). This applies it to *quantity*.
- Deterministic and platform-independent: integers only, so battle stays byte-identical.

This unblocks both modes at once: battle regen earns its tuning rows, and the lawn gets a rate that
is not forced to 0 or 10/s.

**Lawn cadence:** regen becomes a third kind on the existing 100 ms kernel (`KernelDriveHost`,
`UpkeepPeriodTicks = 100`, *"It stays 100 ms deliberately"* `:61,65`, kinds at `:69-70`), alongside
`KindDotPulse` and `KindShieldUpkeep`. **Not a frame counter** — 60 frames is not a second unless the
machine holds 60 fps, which would make regen hardware-dependent.

**Pool max:** `resource.max.{id} = BaseHp(theta) × poolShareMilli[id] / 1000` is already derivable;
the lawn just never seeds it. Pass `seedResourceBaseline: true` from the injector's Hub
(`CheatState.cs:49`; the flag is `ActorHub.cs:147`, sole `true` caller today
`UniqueActorHubCompose.cs:75`). **Without this the feature ships silently inert** — max 0 means
never any stamina, which under D6 is indistinguishable from today's bug.

**Lawn pool lifecycle must be stated, not assumed.** `KernelDriveHost` runs only between
`BeginBoard`/`EndBoard` (`MatchHost.cs:127,153,189`) and ticks with `* Time.timeScale`, so it does
not run paused or between matches; pools are created full per ptr and dropped on death
(`LawnActorResourcePools.cs:38,50`). `resource-hub-ssot.md:284` says pools *"persist across a run and
refill **at rest**"* — the lawn does not do that. **The spec must declare lawn pools match-scoped and
full at spawn**, or explicitly amend that lock.

### 4.7 Numerics — the current path violates the repo's own rule

`OverlayCombatCalculator.cs:9` takes `double BaseOverlayDamage`; the interior is `double`;
`ElementHub.cs:17` uses `combinedMult = 1.0`; `:252` divides by `1000.0` **before** the multiply; the
exit `(long)Math.Round(...)` at `:297` is **unchecked**; and at the Unity boundary
`EntityStatWriter.cs:191` → `ZombieCombatFields.ClampToInt32` **silently saturates** — a clamp on a
magnitude where the caps rule demands a throw. `audit-overflow.py --targets A3` matches `int`
declarations and structurally cannot see any of this.

Wiring this feature multiplies traffic through that path, so the spec must either convert it to
`long`/per-mille discipline or record an explicit, reasoned exemption. It cannot be left unstated.

### 4.8 Lifecycle and edge cases the first draft missed

| Case | Today | Needed |
|---|---|---|
| **Target dies before a deferred delta lands** | No liveness check; `InjectorEffectActionSink.cs:176-209` can find a dying-but-not-destroyed object; `EntityStatWriter.cs:176-227` guards only `== null`, and `next <= 0` calls `ForceKill*` (`:263-287`) → a **second `Die()`** | A death-resolution/"already marked dead" guard, per the MTG/Hearthstone prior art |
| **Re-entrant drain vs. ptr reuse** | `FlushForPtr` is a no-op inside a nested drain (`EventDrain.cs:277`), so records can drain **after** grants withdraw — and FA10 kills inside a drain are exactly what this feature makes common | Explicit ordering rule |
| **Records can be dropped, not just delayed** | `combat.hit`/`*.damage` are **not** on the never-drop list (`event-pipeline-v2-ssot.md:248`) and are droppable under budget exhaustion (`:62`); death flush has its own cap + `droppedDeathBudget` (`EventDrainHost.cs:178,200-216`) | Decide whether an elemental delta may be dropped; G5's "delayed, never dropped" does **not** cover it |
| **Hypno / charm** | `LawnElementResolver._cache` freezes `(side, elements)` per ptr for the match, invalidated only on `matchKey`; `zombie.hypno` (`GameCaptureHooks.cs:283-294`) never invalidates | Invalidate on side change. **This is DESIGN-GATE §2.16's fourth shipped instance** |
| **Multi-hit / piercing / AoE** | One bullet hitting 5 zombies yields 5 records sharing one `actorPtr` (`GameHooks.cs:756/879`); the coalescer keys on `TargetPtr` (`EventCoalescer.cs:44`) | A per-swing cost/rider rule, or the fire-rate exploit ships |
| **Lawnmower / instant-kill** | A 1,000,000-damage event was observed live | Must not produce a proportional rider |

### 4.9 Performance — the cited baseline does not apply

**[audit]** The "300z at 4.44% frame share" figure was measured with the damage trigger-mask **off**
— the hook fast path is `if ((mask & kindBit) == 0) return;`, rebuilt only on grant change
(`event-pipeline-v2-ssot.md` §3.1, `:80`). Binding an `OnDamageDealt` grant to *every* lawn actor
pins that bit on permanently, adding a packet plus two ActorHub resolves per bullet hit. **A fresh
probe baseline is a spec precondition, not a citation.** DESIGN-GATE's Performance row
(`perf-probe-plan.md`, `research/perf/00-baseline.md`) must be read before the spec.

### 4.10 Alternatives rejected

| Alternative | Why |
|---|---|
| Route the hit through `BattleEngine` | Batch resolver, no clock, no injector caller (§4.1) |
| Server computes the delta and returns it | `overlay-control-loops.md:87` hard ban; ptr may be dead or reused; named anti-pattern at §9 |
| Modify the vanilla pea's damage number | `combat-damage-ssot.md:611` — the injector *"must not modify vanilla projectile or bite formulas"*; `decisions.md:34` locks it |
| A fourth `ActionKind` for a lawn attack | D25 closes kinds at three |
| A hardcoded lawn attack outside the action table | Documented failure: cannot be buffed/elementalised/replaced without a per-feature special case |
| A lawn-only cost gate beside `CostLedger` | Second authority for one rule — §2.15 SOLID fail. §4.5 uses data instead |
| Defer regen to `residual-fit` | Principle 9 — unbuilt means build it. §4.6 |

---

## Decisions (owner, 2026-09-13)

**D1 — Both systems run together; the RPG keeps its own damage calculation.** The two halves are
already separate features: `aura-skill-ideal.md:626-631` tabulates **commander stat bleed**
(`progression.bonus.*` → `EntityStatWriter` → vanilla fields, reach *"maxHp, atk, arm (arm is
zombie-only; `defense` is W6)"*) versus **aura/overlay** (`combat.*` → RPG damage/shield resolution).
Writing `attackDamage` is designed behaviour. **`combat.*` must never join `MergeAppliedCombat`** —
that would be the real double-dip; `ActorHub.cs:89-113` correctly excludes it. **Balance is a separate
program.**

**D2 — Follow the existing resource design.** Basic attack consumes a resource; no resource, no
trigger. The rule is genuinely implemented at declare time; what is missing is the cost row, the
pool max, and the regen — all designed in §4.5/§4.6.

**D3 — Byte-identical determinism does not apply to the lawn.** It applies to modes we own the clock
for (siege, world assault, delve). The lawn is a **reflection built from event capture, delayed one
or more steps**, because we do not own PvZ's sim loop — exactly record-then-drain and G5. The A5
goldens question was malformed and is withdrawn.

**D4 — No ICD, no elemental reactions, no status resolver here.** Elements stay bonus/reduce only.
Tracked below.

**D5 — `stamina` for the basic attack; `qi` marks a signature action.** The apparent D22-vs-template
contradiction was not one: they are indexed by different things — D22 keys on `ActionKind.Basic`,
while `action-corpus-cost-templates.v1.json` keys on `ActionCategory` and applies **only** to
corpus-composed rows (`ActionCorpusComposer.cs:164-167`). `act.attack` is constructed directly and
was never composed. Owner's rule: *the basic attack uses `stamina`; anything costing `qi` is a
species/family **signature** action* — which is `ActionKind.Innate`, already in the sealed three.
"Free" in D1 means free of *loadout capacity*, not of resource cost.

**D6 — Exhaustion suppresses the rider only.** We do not own the sim loop and may not touch vanilla
projectile behaviour, so "no resource, no trigger" **cannot stop the shot**. Out of stamina ⇒ the pea
flies for its vanilla/`attackDamage` number and **no elemental delta is added**; the actor waits for
regen and triggers again. Same underlying rule as battle — *the RPG gates only the RPG's own
contribution* — at a different level of control over the clock. **Verified no coupling drops the stat
bleed:** `progression.bonus.*` reaches Unity via a compose-time `EntityStatWriter` write, independent
of packet creation.

**Explicitly rejected: decaying the stat bleed on exhaustion.** Owner: that belongs to a
**resource-exhaustion feature with real statuses/debuffs**; doing it ad-hoc would overlap and confuse
the status system.

**D7 — Regen rides the existing 100 ms kernel, and the sub-tick unit gets built.** See §4.6.

**D8 — One attack is one action trigger.** Owner, 2026-09-13: *"a attack is 1 action trigger, if we
trigger it multiple times that is defect."* A piercing pea hitting five zombies is **one swing** — one
action trigger, one cost payment — while the elemental rider still resolves **per victim**, because
each victim has its own defence and element matchup. This requires the hit record to carry **two**
identities: the shooter's ptr (attacker) and the bullet's ptr (swing id). Melee needs no swing id.
Specced in `lawn-hit-attribution` (record shape) and `lawn-hit-entry` (dedupe).

**D9 — An effect-bearing lawn hit is carried and coalesced, never dropped.** Corrected after owner
review: `combat.hit` is droppable today *because it has no consumer* — §4c's droppable kinds are
explicitly ones with *"no consumer anywhere"*. **This feature changes that class.** Once a record
carries an elemental delta, dropping it is dropping gameplay, not telemetry, and that collides with
G5's own promise (*"degrades to delayed effects, never to frame drops"* `:35`) versus §3.3's *"drop
droppable kinds with a counter"* (`:61-62`). Resolved in favour of G5. The architecture does not
currently describe this case — `event-pipeline-v2-ssot.md` is amended by `lawn-hit-entry`.

---

## Tunables

| Number | Owner file |
|---|---|
| Basic-attack `stamina` cost | **No per-action cost file exists today.** The only current home is the C# literal at `BattleRunState.cs:81` — a balance number in code. The spec must create one |
| Resource regen rates (per-mille/tick) | `data/tuning/battle-resources.v1.json` — earns its rows once §4.6 lands |
| Pool shares (`poolShareMilli`) | `data/tuning/battle-resources.v1.json` — exists, uniform 500 placeholders |
| Element ring K | **`data/tuning/stats.v1.json:10` `matchupShareK: 0.25`** (mirror `shield.v1.json:9 matchupShareKPm: 250`), read via `CombatPolicies.cs:5` → `StatsTuningHub`. **[audit] The first draft said `combat.v1.json`, which does not own K** |
| Basic-attack `sharePermille` | existing action rung tuning; `anchor(Θ) = sharePermille × P(Θ)/1000` — never a new `f(level)` |

Magnitudes are `long`. The drain budget is **not** a tunable — a structural per-frame cap
(`Math.Clamp(frameSec * 0.10, 0.0002, 0.002)`, `EventDrainHost.cs:145-157`), exempt, and should say so
in a comment.

---

## Deferred, tracked

| Deferred | Why | Depends on |
|---|---|---|
| Elemental reactions | Element system is bonus/reduce only; reactions are a large individual feature | status resolver |
| Elemental status application + status resolver | Needs the action system fully wired | action system |
| ICD / element-application rate limiting | Only meaningful once statuses apply | the two above |
| Proc coefficient (fire-rate value scaling) | Real anti-exploit, but prices *value* — the balance program's axis | balance program |
| Plant-side lawn status | Lawn status executor iterates zombies only (`action-corpus-ideal.md:1379`) | status resolver |
| Resource exhaustion as a real status/debuff | D6 | status resolver |
| Per-actor defense on incoming vanilla hits | `GameHooks.cs:700,703` synthetic `"dmg"` key, one cached number per side | that program |

---

## What this deliberately does not decide

- Action corpus **content** (179 briefs, 24 loading) — the loader is in scope, the content is not.
- Any FE/HUD surface for elemental damage numbers.
- Any rebalancing (D1).
- Everything in "Deferred, tracked".

Settled and no longer open: whether `combat.*` joins `MergeAppliedCombat` (**no**, D1); whether
battle goldens moving is acceptable (**not applicable**, D3); `stamina` vs `qi` (**both**, D5).

---

## Verification — how this gets proven without repeating T14

Per `live-probe-standard.md`, and shaped by the traps this feature specifically carries:

1. **Not inside a debug session.** `EventDrainHost.Active` is false when `DebugRuntime.SessionActive`,
   and `EmitOverlayBreakdown` only emits *inside* one (`InjectorCombatBridge.cs:~88-94`) — so the
   obvious instrument disables the thing it measures, and `SessionMode` also bypasses coalescing
   (`EventDrain.cs:216`). A probe must read state through a path that does not flip the mode.
2. **Real record, real flow.** Mint/level/allocate through real endpoints; deploy through the real
   deploy path; never a fabricated loadout or a synthetic ptr.
3. **Both halves reported separately** — persisted state, then the live Unity read — never merged.
4. **The falsifier:** an actor with zero stamina must show the pea landing for its vanilla number and
   **no** elemental delta; the same actor after regen must show the delta. That difference, on the
   live board, is the proof. A single passing run proves nothing without its negative case.

---

## Open questions

None outstanding. D1–D9 cover the design decisions; everything else in this doc is a wire or a build
whose shape is specified above.

---

## Doc defects found while writing this (file separately)

1. `DESIGN-GATE.md:33` states the no-damage-at-hit-time rule more broadly than its source
   (`overlay-control-loops.md:22`); as written it forbids the shipped Hot loop. Narrow it to name the
   **Server**.
2. `DESIGN-GATE.md:56` cites `battle-timeline-map.md` / `battle-turn-ideal.md` for *"Battle consumes
   FA10 only"*; the rule is in neither. Real source is a code comment (`BattleEffects.cs:225`), and it
   is **three opcodes stale** — FA2, FA1 and `structure.place` were added (`:206,213,221`).
3. `event-pipeline-v2-ssot.md:60` says the drain budget is *"~1.5 ms"*; shipped code is
   `Math.Clamp(frameSec * 0.10, 0.0002, 0.002)` (`EventDrainHost.cs:145-157`).
4. DESIGN-GATE's Elements row calls the two matrices asymmetric **in content**; code shows identical
   content, asymmetric **contract**. `spec-shield-and-elements.md` §0 already carries the correction.
5. DESIGN-GATE's action row says `ActionTag` is 8; it is **9** (`Construct`, `ActionEnums.cs:57`).
   Seedsmith's mirror is stale too (`tools/seedsmith/seedsmith/adapters/actions/vocab.py:35-37`).
6. `EventDrainHost`'s class header says *"Off (default until Task 10 wires the tick)"*; Task 10 wired
   it and it is **default-on** (`InjectorLoop.cs:197-198`).
7. `DerivedStatRegistry.cs` — the max note is `:239`, the regen note `:241` (identical text).

### A trap that caught two independent reviewers

`OverlayCombatFeature.cs:10-13` is `Enabled => EnvEnabled || CheatState.On(CheatToggleId)`. The env
half defaults **false**, which reads at a glance like a default-off feature flag. It is not — the
cheat-toggle half defaults **true**. Cite the schema default (`CheatSchema.cs:105`), never the env var.

---

## Next step

`/spec` — capability map plus module specs. The split this doc supports:

| Module | Wires |
|---|---|
| **lawn-hit-attribution** | Record `bullet.from` / `from_zombie` as the attacker instead of the bullet — unblocks Hub resolve **and** `entity:{ptr}` grant matching in one change |
| **lawn-hit-entry** | The four gates; drain → packet keyed on the **board fold**; drop/ordering/liveness rules (§4.8) |
| **basic-attack-grant** | Bind `ActionKind.Basic` per lawn actor at spawn; element via the existing `elementPayload` bake |
| **resource-subtick** | S10.1 — per-mille accumulation with carry (§4.6); unblocks regen in **both** modes |
| **basic-attack-cost** | Cost becomes data not a hardcoded empty array; `seedResourceBaseline: true` for the lawn Hub; regen as a third kernel kind; lawn `CostLedger` call |
| **element-cache-invalidate** | Hypno/side-change invalidation — DESIGN-GATE §2.16's fourth instance |
| **corpus-unblock** | `Program.cs:402`; `UpsertSpeciesBasics` callers; re-derive the Basic/Innate composer blocker (the first draft's citation was fabricated) |

Rate limiting is not a module here — proc coefficient went to the balance program, ICD to the
deferred status feature.

Not written here; the ideal doc is where this phase stops.
