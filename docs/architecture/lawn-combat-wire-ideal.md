# Lawn combat wire — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.

**Named on request as two sub-programs of the SOLID run — "Lawn Run" and "Battle Engine Wire". This
doc keeps the first name and retires the second:** `BattleEngine` is the wrong component and cannot
sit on a lawn frame (§3.1). The accurate name for the second half is **lawn hit wire**.

---

## Which loop this extends

[The loops](../guide/the-loops.md) **1. Lawn — first core** ("Shipped (mirror, HUD, deploy,
progression from play)"), plus the cross-cutting **Combat depth** section, which already lists
*"element ring, shields, statuses, crit"* as **Already in** and *"meters, skills, interactive
battles"* as WIP. No new loop. No parallel pitch.

This doc does not make the lawn the whole game, add a fourth stock, add a class, add a stamina gate
to the *player*, or replace expeditions with delves.

---

## Why this exists — the observation that started it

On a live board, 2026-09-13, with a real specimen and a real server:

- A buffed Peashooter had `appliedAtk 1350` and its peas **did** deal 1350. A buffed zombie had
  `theAttackDamage 170` and its bites **did** deal 170. **The stat bridge works.**
- The same live entity carried `combat.power.omni 32`, `combat.defense.omni 4`,
  `combat.crit.rate.omni 75`, `combat.crit.damage.omni 75` — funded, resolvable, and **touching
  nothing**. Across 36 real hits, every damage event read `before == after`.

So the RPG's *stat* layer reaches the lawn and its *combat* layer does not. The owner's summary was
exact: a peashooter with 5000 omni attack power has a number that is "just for fun".

**This is a wiring gap, not an architectural wall, and not a defect of `ActorHub`.** `ActorHub`
composes correctly for every lawn actor including plain PvZ-spawned ones (proven live: a vanilla
`debug.spawn-zombie` NormalZombie picked up `bonusMaxHp 480` / `bonusAtk 120` from species
progression with no `UniqueActor` row at all).

---

## Load-bearing principles, restated inline

A downstream session reads this doc, not its links. These constrain every choice below.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The
   narrow Unity write surface constrains exactly one thing — what a *persistent vanilla stat change*
   may touch. It says nothing about what an RPG feature may do, because RPG mechanics resolve in the
   RPG's own stack during a live lawn match. "Does the lawn support X" is almost always the wrong
   question.
2. **Two async systems; deltas, never absolutes; record-then-drain.** Hooks record a struct and
   return. Effects are decided later in a budgeted drain, and records carry to the next frame. Delay
   is the designed degradation mode: *worst case degrades to delayed effects, never to frame drops*.
3. **The Server never sits on the hit path.** `overlay-control-loops.md:87`, hard ban: *"Server FSM
   (UniqueActor or any 'in-run game logic' on Server) must not sit between `combat.hit` (or
   equivalent capture) and FA* apply."* Hot rule 3 (`:150`): *"**Never await** SignalR, HTTP, or
   SQLite for the roll or apply."* **But the RPG layer hosted in-process on the injector may and
   already does compute damage at hit time** — see §3.2, where the gate's own one-line summary is
   broader than its source.
4. **One ActorHub compose / one read.** Actor combat derived and AppliedCombat compose once in
   `ActorHub`; contribute via a registered subsystem/atom reader or consume Hub output. No private
   fold, no second composer.
5. **One power ladder.** Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`. A new
   `f(level)` is the defect the power SSOT exists to end. `action-ideal.md` §1.3 has already rejected
   a second curve for actions specifically.
6. **The balance surface is data.** Any number a balance pass would touch lives in
   `data/tuning/<domain>.v{n}.json`, not a `const`.
7. **No hard progression ceilings.** Caps on magnitudes are soft/configurable; absolute bounds throw.
   Structural per-frame caps are exempt and must say so.
8. **`long` for any magnitude.** The ladder is quadratic; `float` stops being integer-exact at
   `Θ`=232 and `int` per-mille at 3,213 — both inside real play.
9. **Gameless-first is capability, not the pitch.** This feature is lawn-only by nature; it must not
   become a thing the rest of the game depends on to function.

---

## What already exists — three buckets

### Built

| Thing | Evidence |
|---|---|
| `ActorHub` composes for **every** lawn actor, general creatures included | Proven live 2026-09-13: plain spawned NormalZombie got `bonusMaxHp 480` from species `Vigor 2`, `bonusAtk 120` from `Ferocity 1`, no `UniqueActor` row |
| `progression.bonus.atk/maxHp/arm` → `EntityStatWriter` → vanilla Unity field → PvZ's own damage math | Live: pea 1350, bite 170 |
| The **whole overlay damage stack**, gate default-**on** | `EffectRuntime.WireCombatMath` (`Effects/EffectRuntime.cs:505-531`); `OVERLAY-COMBAT` default true at `CheatSchema.cs:105`, and set true in both registries (`CheatRegistry.cs:80`, `CheatState.cs:379`) |
| Overlay combat math, locked formulas | `attackerPower(E) = combat.power.omni + combat.power.E` vs `defenderDefense(E)`; crit, accuracy, penetration, absorption, parry, block, reflect all shipped — `combat-damage-ssot.md` §5-§6 |
| Element vocabulary, closed: 6 members (Fire, Ice, Air, Earth, Light, Dark) + `omni` baseline | `ActorElementTypes.cs:3-11`; `ElementRoster.Concrete` the only legal iteration (`:21-29`) |
| Element **ring** math | `ElementHub.ResolveComponentBonus` (`Combat/Element/ElementHub.cs:9-42`): `combinedMult = Π(1 + RelationShare(rel))`, STR **1.25** / WEK **0.75**; consumed `OverlayCombatCalculator.cs:130,145,152` |
| Element resolution **by ptr** on a live lawn entity | `LawnElementResolverHost.Resolve(ptr)` (`Injector/Effects/LawnElementResolverHost.cs:27-32`) → `InjectorCombatBridge.ResolveActor` (`:38,61-84`) |
| `elementPayload` baked from owner species onto grants, end to end | `AtomCompiler.cs:237-243` ← `AtomPushService.cs:293`; parsed back at `DamagePacketBuilder.cs:48` |
| Elemental VFX, per-element palette, default-on | `Core/Vfx/ElementFxPalette.cs:11-20`; rides `DamageFxDto.Elements` (`Contracts/DamageFxDtos.cs:28`) → `VfxCueMapper.cs:17` → `VfxDirector.cs:201`; `SYS-ELEMENT-FX` true in all three registries |
| **`ActionKind.Basic` — the basic attack already exists, sealed** | `action-ideal.md:40` D1 (three kinds), `:60` D25 (kinds close at three); `BattleEngine.BasicAttackEnvelope`, `ActionId = "act.attack"` (`Actions/BasicAttack.cs:28,30`) |
| Basic attack already computes **elemental** damage through the same calculator the lawn uses | `ApplyBasicAttack` → `calculator.Compute(new OverlayCombatRequest{...})` (`BasicAttack.cs:364-380`); element from `setup.ElementPrimary → ActorElementTypes.Create → AttackComponents` (`BattleEngine.cs:48-53`) |
| Species element already binds on the lawn | E27 `lawn-element-bind`, code done 2026-09-03 (`tasks/content-stack-todo.md:123-160`) — live proof owed |
| Record-then-drain, budgeted, with same-target damage coalescing | `EventDrainHost.cs:145-157`; coalesce at `event-pipeline-v2-ssot.md:52` |
| In-process per-hit RPG combat is already performant | 300z stress at **4.44% frame share**, playable at 1,006 (`event-pipeline-v2-ssot.md:5`) |
| Pointer-reuse safety: die emits before grants are withdrawn | `GameHooks.cs:1283-1288` → `EffectRuntime.WithdrawEntity`; ordering enforced at `GameHooks.cs:664/676` and `:1154/1156` |

**The single most important built fact:** the vanilla hit **already reaches the RPG layer**.
`EventDrainHost.TryRecordDealtFromBullet` (`EventDrainHost.cs:44-47`) and `TryRecordMeleeDealt`
(`:68-70`) receive it.

### Wiring gap

Every one of these is an inert line, not a wall.

| Gap | The inert line |
|---|---|
| **The vanilla hit is turned away at the door.** Both drain entry points short-circuit on `!EffectRuntime.HasOnDamageDealtGrant()`. No grant bound ⇒ no packet, no element, no VFX | `EventDrainHost.cs:44-47`, `:68-70` |
| No seeded action can ever **be** a Basic or Innate action — the composer hardcodes `Skill` and drops `kindHint` | `ActionCorpusComposer.cs:143`, `:149` |
| 155 of 179 shipped action briefs never load — hardcoded two-file array | `Program.cs:402` |
| `UpsertSpeciesBasics` / `GetSpeciesBasics` have zero production callers | `RpgStore.Actions.cs:650,673` |
| The action stack is unreachable from the lawn — `grep FusionRpg.Core.Actions src/FusionRpg.Injector` → **0 files** | — |
| ~~`combat.power.omni` excluded from the field bridge~~ — **reclassified: correct by design, NOT a gap.** `MergeAppliedCombat` folding only `progression.bonus.*` is what keeps the two halves separate (D1). Folding `combat.*` here would double-dip RPG power into one hit via two routes | `ActorHub.cs:89-113` — keep as is |
| Per-actor defense never reaches its own incoming hit — the damage scalar resolves ActorHub with a literal `"dmg"` key and a `{Hp=1,MaxHp=1,Atk=1}` baseline, caching one number per side | `GameHooks.cs:691-710`, inert line `:702` |
| `type-weights.json` exists and is populated (1,131 entries) but has **zero `src/**` readers** — Python-only | `data/seed/actions/type-weights.json` |
| A13's runtime roll `ActionSeeder.Generate` is implemented but has **zero production callers** — the code asserts this about itself: `ActionCorpusProducerLanded = false` ("No production path exists"), enforced by a test | `Actions/Seeding/ActionSeeder.cs:30,32`; flag at `Items/Grants/ItemGrantedActionRow.cs:111-119` |
| `ActionUnlockGrantService` — zero production callers | `Actions/Unlock/ActionUnlockGrantService.cs:22,41` |
| `BattleEngine.Resolve(unlockStateFor:)` never passed in production, so `EffectiveRungOf` always falls through to the authored `Rung` | `BattleRunState.cs:598-612`, fallthrough `:611` |
| `species-innate.json` — 904 entries, zero C# readers | — |
| Lawn status executor iterates **zombies only**; plant-side status is a named capability gap with no owner | `action-corpus-ideal.md:1379` |

### Real gap

| Gap | What would have to be built |
|---|---|
| **Nothing converts the vanilla damage *number* into an elemental one.** An overlay packet is an *additive* delta beside vanilla damage. Absorbing or replacing the vanilla share needs a new mechanism — the only existing analogue is debug-only `silence-vanilla` (`CheatCommandRunner.cs:446`) | A production vanilla-share policy. **This is Open question 1 — and `decisions.md:34` currently locks the opposite** |
| **No per-actor FSM covers a general creature.** A plain PvZ-spawned plant/zombie has no `instanceId`, no correlation, therefore no `UniqueBinding` row. It exists only as a `MatchState.Plants[ptr]` / `Zombies[ptr]` dictionary row (`match-runtime.md:367-370`) | Nothing — **the board fold is already the universal per-actor structure.** The design must key on the fold, not on the FSM. See §3.3 |
| A basic-attack **rate limiter** (proc coefficient / ICD) for a tower-defense fire rate | §3.5 |

---

## Prior art — with numbers and sources

Already in-repo; do not re-research: `docs/research/game-design/01-typing-matrices.md` carries the
Pokémon 18×18 (8 cells 0×, 61 at 0.5×, 51 at 2×, 37% non-neutral), SC1, WC3 7×6, AoE2 38 armour
classes, and the conclusion that **four AAA franchises deleted N×N matrices and none returned**. Our
ring is 6×6 and already shipped — that conclusion is a warning against *growing* it, not against
using it. `docs/research/effect-runtime/05-icd-audit.md` holds our own per-`statusId` ICD behaviour.

New, external, and directly design-shaping:

**Proc coefficient is the shipped answer to "many hits per second".** Both Diablo III and Risk of
Rain 2 multiply a rider's chance/magnitude by a per-skill scalar so fast, multi-hit and AoE attacks
do not get free value. D3 normalises to *"the number of hits each skill does at exactly 1.00 attacks
per second"*; RoR2 defaults to **1.0** with fast weapons below it — MUL-T Nailgun **0.6**, turning a
Tri-Tip Dagger's 10% into 6% per hit
([RoR2 wiki](https://riskofrain2.wiki.gg/wiki/Proc_Coefficient),
[diablowiki](https://www.diablowiki.net/Proc_rate)). The failure mode both exist to prevent is
exactly ours: **a rider priced per-hit is trivially maximised by raising fire rate**, a different
axis from the one it was balanced on. This is the same defect the repo already catalogued in
`action-taxonomy/03-composable-skill-systems.md` §11 as *"the priced thing and the powerful thing
were not the same thing"* across five shipped blow-ups.

**Ring multiplier band.** Shipped rings sit in **0.5×–2.0×**, with 0× rare and reserved (8/324 =
2.5% of Pokémon cells). Our STR **1.25** / WEK **0.75** is inside that band and conservative. The
documented counter-example is Diablo II: resistance as a flat percentage, player max resist **95%**,
Hell applying a flat **−100%**, and a monster at **≥100%** being outright *immune* — a binary wall
that made off-element builds unplayable and required dedicated break-immunity items
([Maxroll](https://maxroll.gg/d2/resources/damage-reductions)). **Keep multiplicative shares; never
introduce an immunity threshold.**

**Already-dead targets are solved with a death phase, not per-hit checks.** MTG resolves lethal
damage as a state-based action checked when a player would next get priority, with all simultaneous
deaths as one event ([rule 704](https://mtg.fandom.com/wiki/State-based_action)); Hearthstone
collects deaths into an explicit **Death Phase** with triggers queued in order of play
([Advanced rulebook](https://hearthstone.wiki.gg/wiki/Advanced_rulebook)). The observed bug class in
the wild is **on-death effects firing multiple times** when repeated hits land on an
already-lethally-damaged target. Our equivalent already exists in shape: FA10 `Die if HP≤0` at the
flush barrier, plus `ForgetEntity` deferred into the same drain slot as the death record.

**Genshin's ICD** — 2.5 s **or** 3 hits per aura application, gauge units 1/1.5/2/4/8, 0.8× aura tax
— is the best-documented rate limiter for "every hit would otherwise apply an element"
(`game-design/01-typing-matrices.md` §4, already in-repo).

**Basic attack as a real table row is the majority shipped pattern** — WoW's auto-attack is genuinely
spell ID 6603; League defines it as 100% of total AD, weapon/stat-derived. The documented failure
mode of the *hardcoded* variant: it *"cannot be buffed, elementalised, or replaced by equipment
without a special case per feature."* **This validates the existing `ActionKind.Basic` design over a
bespoke lawn fallback** — which is what the repo already has.

Could not verify this session: any published per-hit CPU budget for tower defense; any post-mortem on
deliberately deferred damage in a single-player context; any measurement of player-perceptible
thresholds for HP-bar jump or damage-number ordering; PoE's 75%/90% resist caps (widely repeated,
unverified); any prior art for an *overlay mod* adding an elemental layer to a host game.

---

## The shape

### 3.1 The proposed loop, corrected

Requested:

```
PvZ Lawn Run → Game Injector → FSM → Battle Engine → Actor Hub → Battle Engine
  → FSM → Game Injector → VFX → damage applied
```

Two named defects.

**`Battle Engine` is the wrong component.** `BattleEngine` (`src/FusionRpg.Core/Battle/BattleEngine.cs:25`)
is a *pure deterministic batch resolver* — its own contract says *"No I/O, no clock, no ambient
state: same setup + seed + platform ⇒ byte-identical report"* (`:14-24`) and *"`Resolve` is a batch
resolver, **not a live per-frame loop**… which nothing here does"* (`:168-172`). It has **zero
injector callers** (the single grep hit is a comment at `InjectorCombatBridge.cs:51`).
`battle-turn-ideal.md:24` settles it: for PvZ realtime the clock owner is *"**The Unity game. Not
us.**"* and mode 1 is *"an **adapter, not a scheduler**… we must not pretend we can schedule PvZ."*

The component that actually does this job already exists and is already wired:
**`CombatDamageDispatcher` → `OverlayCombatCalculator` → `ShieldGate`**, invoked in-process from
`EffectBag.cs:567,637`.

**`FSM` does not cover general creatures** (§ Real gap). The universal per-actor structure is the
**MatchRuntime board fold**.

**Corrected loop:**

```
PvZ lawn hit
  → injector Harmony hook RECORDS a struct and returns      (never computes in the hook)
  → budgeted drain, ≤10% of frame                            (EventDrainHost)
  → MatchRuntime board fold  (+ UniqueBinding phase IF the actor is a Bound specimen)
  → EffectBag.OnEvent
  → ActorHub-composed derived for attacker and defender
  → CombatDamageDispatcher → OverlayCombatCalculator → ElementHub → ShieldGate   [in-process]
  → EffectFunnel mailbox (signed delta, never absolute, never mode=set)
  → flush barrier, re-entry depth 0 → FA10 EntityStatWriter Add (Die if HP≤0)
  → elemental VFX from DamageFxDto.Elements
  → Server observes asynchronously — NEVER a decision gate
```

Everything in that chain exists. The feature is the **entry point**.

### 3.2 Where the gate's summary is broader than its source — read this before citing it

`DESIGN-GATE.md:33` says the RPG *"does not compute damage at the moment of the hit."* Taken
literally that forbids this feature. **It is broader than its source.** `overlay-control-loops.md:126-139`
documents the shipped Hot loop doing exactly that in-process, and `EffectBag.cs:567,637` really does
call `CombatDamageDispatcher.DispatchInstant` at hit time. The true rule, from
`overlay-control-loops.md:22`: Unity owns physics/lifetime, and the RPG overlay's *"hot evaluation
must not require a Server round-trip."*

**Restated correctly: the Server does not compute damage at the moment of the hit. The RPG layer,
hosted in-process on the injector, does.** That gate line should be narrowed (§ Open questions).

### 3.3 Key on the board fold, not the FSM

A general creature has no FSM row by design — *"General creature = PvZ-engine-spawned,
species-stats-only, no persistent instance"*. It is `MatchState.Plants[ptr]`. A Bound specimen
additionally has a `UniqueBinding`. The basic attack must therefore resolve its attacker from the
**fold**, and treat the binding as an *enrichment* when present, never a precondition. This is what
makes "every plant and zombie" achievable — and it is already proven, because species aptitude
bonuses already reach general creatures live.

### 3.4 Two damage paths exist, and they converge late — know which one you are extending

This surprised a reviewing pass, so it is stated explicitly. The **action** path and the **effect**
path are parallel and meet only at the last stage:

```
ACTION path (battle today):
  ApplyBasicAttack → OverlayCombatCalculator.Compute → SignedDelta
    → DispatchHit (BattleRunState.cs:1136) → ApplyHp (:869)
    → DamageApplyPipeline.Apply (:874)  → ShieldGate.AbsorbFinalized → IHpDeltaSink
  No DamagePacket. No CombatDamageDispatcher. No Funnel. No FA10.

EFFECT path (lawn today, for grant-originated damage):
  EffectBag.OnEvent → DamagePacketBuilder.FromOverlay (DamagePacketBuilder.cs:11)
    → CombatDamageDispatcher.DispatchInstant (:12) → math (:41)
    → DamageApplyPipeline.ApplyPacketToFunnel (:47) → Funnel → FA10 Writer Add
```

They share `OverlayCombatCalculator` and `DamageApplyPipeline`, by two different entry points.
**The lawn wire must use the EFFECT path** — it is the one that reaches `EntityStatWriter` and the
one the elemental VFX rides. The action path's `IHpDeltaSink` is a battle-state sink with no Unity
end. The two touch indirectly already: `ApplyBasicAttack` raises `OnDamageDealt` into the bag
(`BasicAttack.cs:397-406`) carrying `Damage = -signedDelta`, and riders on that trigger build their
own packets — which is precisely the seam this feature generalises.

### 3.5 The basic attack is a grant, and that is the whole trick

`HasOnDamageDealtGrant()` is the guard that turns the vanilla hit away. **The fallback basic attack
is precisely the grant that makes that predicate true for every lawn actor.** Bind it at spawn/bind
through the same Funnel path every other grant already uses; the element rides it via the existing
`elementPayload` bake, which already reads the owner's species element.

Nothing new is needed in the vocabulary: `Kind=Basic, Category=Attack, Tag=Offensive, Mode=Single`
all exist. `Mode=Area` is rejected at bind time while no board exists, so `Single` is also the only
legal choice.

### 3.6 Rate limiting is mandatory, not optional

A peashooter fires continuously. Per prior art, adopt a **proc coefficient** on the basic attack —
default `1.0`, authored per plant/zombie archetype, so a fast shooter does not out-earn a slow heavy
hitter on an axis nobody balanced. Our drain already coalesces same-target damage records within a
window, which is a partial mitigation but **not** a substitute: coalescing bounds *cost*, a proc
coefficient bounds *value*.

### 3.7 Alternatives rejected

| Alternative | Why rejected |
|---|---|
| Route the hit through `BattleEngine` | Batch resolver, no clock, no injector caller, cannot tick per frame (§3.1) |
| Server computes the elemental delta and returns it | `overlay-control-loops.md:87` hard ban; ptr may be dead or reused by the time it lands; §9 lists it as a named anti-pattern |
| Modify the vanilla pea's damage number in the `TakeDamage` prefix | `combat-damage-ssot.md:608-612` — the injector *"must not modify vanilla projectile or bite formulas"*; `decisions.md:34` locks vanilla peas/bites. See Open question 1 |
| A new "lawn basic attack" action kind | `action-ideal.md` D25 closes kinds at three; a fourth vocabulary is the exact defect the atom program exists to stop |
| A bespoke hardcoded lawn attack outside the action table | Documented failure mode: cannot be buffed, elementalised or replaced by equipment without a per-feature special case |
| Give the basic attack its own damage curve | `action-ideal.md` §1.3 already rejected a second curve. Magnitude is `anchor(Θ) × q(rung)`, `anchor(Θ) = sharePermille × P(Θ)/1000` |

---

## Tunables

Every number this introduces is data, not code.

| Number | Owner file | Notes |
|---|---|---|
| Proc coefficient per archetype | `data/tuning/action-*.v{n}.json` (new key) | Default `1.0`; fast shooters below |
| Basic-attack `sharePermille` | existing action rung tuning | Feeds `anchor(Θ)`; **never** a new `f(level)` |
| Element ring K (STR/WEK share) | `data/tuning/combat.v1.json` via `CombatPolicies.cs:5` | Today STR 1.25 / WEK 0.75 |
| Element application ICD, if adopted | `data/tuning/combat.v1.json` | Genshin precedent: 2.5 s or 3 hits |
| Vanilla-share policy, if adopted | `data/tuning/combat.v1.json` | Open question 1 |

Magnitudes are `long`. The drain budget is **not** a tunable — it is a structural per-frame cap
(`Math.Clamp(frameSec * 0.10, 0.0002, 0.002)`, `EventDrainHost.cs:145-157`), legitimately exempt, and
it should say so in a comment.

---

## What this deliberately does not decide

- The action corpus content itself (179 briefs, 24 loading) — the *loader* is in scope, the *content*
  is not.
- Any FE/HUD surface for elemental damage numbers.
- Any rebalancing (D1 — separate program).
- Everything in "Deferred, tracked" above.

Settled since the first draft, and no longer open: whether `combat.*` should join `MergeAppliedCombat`
(**no** — D1), and whether moving battle goldens is acceptable (**not applicable** — D3).

---

## Decisions (owner, 2026-09-13)

**D1 — Both systems run together. The RPG keeps its own damage calculation.**
Not "additive vs absorb" as originally framed: the two halves are *already* separate features and
both are intended. `aura-skill-ideal.md:625-632` states it as a table — **commander stat bleed**
(`progression.bonus.*` → `ActorHub` → `EntityStatWriter` → vanilla plant/zombie stats, reach
"maxHp, atk, arm") versus **aura/overlay** (`combat.*` → RPG damage/shield resolution). So writing
`attackDamage` is *designed behaviour*, not a leak — corrected here because a reasonable reading of
"we don't touch PvZ stats" suggests otherwise, and the live writer really does set `attackDamage`,
`thePlantAttackInterval`, `theShieldHealth`, `theLevel` and eight more fields
(`EntityStatWriter.cs:47-81`, E16/E38).

**The one thing that would create a real double-dip is folding `combat.*` into `AppliedCombat`.**
`ActorHub.cs:89-113` folds only `progression.bonus.*` and must keep doing so. That exclusion is
**correct by design**, not a gap — an earlier draft of this doc mis-filed it as a wiring gap.

**Balance is a separate program.** This program wires gaps only. No rebalancing of vanilla numbers,
no retuning of the element ring, here.

**D2 — Follow the existing resource design. No new resource invention.**
The basic attack consumes a resource; no resource, no trigger. Verified: the rule is genuinely
implemented at *declare* time, which is the right place (§ Resource findings). What is missing is the
cost row and the regen reader — both wiring.

**D3 — Byte-identical determinism does not apply to the lawn.**
It applies to the modes we own the clock for: siege, world-map assault, delve. **The lawn is a
reflection built from event capture, with a delay of one or more steps — not an instant apply**,
because we do not own PvZ's sim loop. This is exactly record-then-drain and G5's *"worst case
degrades to delayed effects, never to frame drops"*. So the A5 "byte-identical" constraint is a
battle-mode property and never transferred to the lawn; the goldens question in an earlier draft of
this doc was malformed. The correct audit question is *does the spec follow the principles* — and the
finding of this doc is that the principles are sound and the wiring simply never happened.

**D4 — No ICD, no elemental reactions, no status resolver in this program.**
ICD belongs to status application, and the elemental system is deliberately half-built: it has
bonus/reduce damage only (a simple Pokémon-shaped ring), with no reactions and no status resolver.
Building those is a serious individual feature. **Deferred and tracked — see "Deferred, tracked"
below.** Consequence for this program: the basic attack deals elemental *damage*, and applies no
elemental *status*.

---

## Resource cost and regeneration — verified 2026-09-13

The owner's recollection was checked against code. Design and machinery confirmed; two wires missing
and one contradiction.

### Built

| Thing | Evidence |
|---|---|
| Cost model | `ActionCostRow(ActionId, ResourceId, AmountSpec, When, AllowLethal)` — `Actions/ActionRow.cs:141` |
| `CostLedger` — pre-gate `Check` (deterministic, no roll) and validate-all-then-consume-all `TryPay`; hp floors at 1 unless `AllowLethal` | `Actions/Cost/CostLedger.cs:85-100`, `:106-138`, `:145` |
| All six pools, array-backed over `DerivedStatChannels.ResourceIds` | `Actions/Cost/ActorResourcePools.cs:13,64,93,113` |
| **"No resource, no trigger" is real, and gates at _declare_ not commit** — an unaffordable action is never declared, so no attack happens | `UsabilityEvaluator.cs:66` → `CostLedger.Check` → `UsabilityReason.CannotAfford` (`CostLedger.cs:96`); real ledger passed at `BasicAttack.cs:163-166` |
| Lawn resource pools exist, keyed by combat ptr, with lifecycle cleanup | `Combat/LawnActorResourcePools.cs:26`; registered `Injector/Effects/InjectorEntityRegistry.cs:51`, cleaned `:127,:138` |
| Real cost rows do exist in production — siege constructions spend `qi`/`stamina`/`hunger` | `Battle/Siege/ConstructionActions.cs:120-122` |

### Wiring gap

| Gap | Inert line |
|---|---|
| **The basic attack costs nothing.** One empty array, so `CostLedger.Check` early-returns `Usable` and the gate passes vacuously for every shipped action | `Battle/BattleRunState.cs:80` — `Costs: Array.Empty<CompiledActionCost>()`; early-out at `CostLedger.cs:69-70` |
| **No regen reader.** `resource.regen.*` channels are registered and aptitudes can fund them, but nothing consumes them for `stamina`/`qi`/`spirit`/`hunger` | `Stats/Derived/DerivedStatRegistry.cs:239` records this in its own note |
| **The lawn has no cost gate at all.** `grep CostLedger src/FusionRpg.Injector` → zero hits. Lawn pools are written only by the `resource.delta` atom sink | only caller `InjectorEffectActionSink.cs:254` |
| Item-stock precondition (`ActionStockCommit` / `StockDemand`) is a separate, also-inert path — not resource cost, do not conflate | `Actions/Cost/StockLedger.cs:119` early-out; zero authored `holdsStock` |

### Real gap

| Gap | Detail |
|---|---|
| **Combat regeneration is deliberately zero.** `BattleRuleset.BaseResourceRegen(theta, resourceId) => 0`, rationale at `:395-411`: rounding to whole ticks makes the smallest non-zero rate ~300 poise/round against a 100 spend. `resource-hub-ssot.md:284` — pools *"persist across a run and refill **at rest**"*, and a battle is not a rest | `Battle/BattleModels.cs:412` |
| **Cadence mismatch.** `action-ideal.md:223`: *"An action cost is authored against the pool's **REGEN**, never against its MAX."* Battle costs are per-*round*; a lawn Peashooter fires roughly every 1.4 s of wall-clock. A round is not a second, so no battle-authored cost transfers to the lawn unmapped | — |
| Stamina regen coefficient is sized against the wrong opponent — named defect, owned by `residual-fit`, not this program | `tools/CombatSim/tuning/aptitudes.v1.json` `recovery.scaleMilli: 374`; diagnosis `action-ideal.md:184-201` |

### The apparent contradiction — resolved, there was never one (2026-09-13)

It looked like D22 (`stamina`) and the corpus template (`qi 20`) disagreed. They do not: **they are
indexed by different things.**

| | Indexed by | Applies to | Resource |
|---|---|---|---|
| `action-ideal.md:63` D22 | `ActionKind.Basic` | `act.attack`, the intrinsic fallback | **`stamina`** |
| `action-corpus-cost-templates.v1.json` | `ActionCategory` (all five) | **corpus-composed rows only** — `ActionCorpusComposer.cs:165-168` | **`qi`** |

`act.attack` is never corpus-composed — it is constructed directly at `BattleRunState.cs:80`, so the
template has never applied to it. The collision was purely lexical: "attack" is both a Category and
the Basic action's id. The template's own `_meta.derivation` even reasons about *"the default/
basic-attack-shaped path"* while actually keying on Category, which is where the confusion came from.

**Owner's reconciliation rule (2026-09-13), now binding for this program:** *the basic attack uses
`stamina`; if something costs `qi`, it is by definition **not** a basic attack — it is a species/
family **signature** action.*

That rule needs no new vocabulary. A species/family signature action is **`ActionKind.Innate`**
(`action-ideal.md:40` D1 — "innate (1, free, per creature type)"), where "free" means free of
*loadout capacity* (D2: *"Basic actions cost no loadout capacity — they are intrinsic"*), **not**
free of resource cost. So an Innate costing `qi` is consistent with both documents.

Remaining work is therefore a wire, not a decision: author a `stamina` cost row for `act.attack`.

---

## Deferred, tracked (not this program)

Named here so they cannot become invisible, per D4 and the owner's instruction to track rather than
silently drop.

| Deferred | Why | Depends on |
|---|---|---|
| **Elemental reactions** (wet+lightning-shaped combos) | The element system is bonus/reduce only today. Reactions are a large individual feature | A status resolver |
| **Elemental status application + status resolver** | Needs the action system completely wired first | action system |
| **ICD / element-application rate limiting** | Only meaningful once statuses apply; pointless while elements are damage-only | the two above |
| **Proc coefficient** (fire-rate value scaling, D3/RoR2 prior art) | Real anti-exploit, but it prices value — that is the balance program's axis, not a wiring gap | balance program |
| Plant-side lawn status | Lawn status executor iterates zombies only (`action-corpus-ideal.md:1379`) — a named, ownerless capability gap | status resolver |
| Per-actor defense on incoming vanilla hits | `GameHooks.cs:702` synthetic `"dmg"` key, one cached number per side | whichever program owns that fix |
| **Resource exhaustion as a real status/debuff** | Owner, 2026-09-13 (D6): an exhausted actor showing decayed stats belongs to a proper exhaustion feature using the existing buff/debuff/status architecture — not an ad-hoc stat decay here, which would overlap and confuse the status system | status resolver |

---

## Decisions, round 2 (owner, 2026-09-13)

**D5 — `stamina` for the basic attack; `qi` marks a signature action.**
See "The apparent contradiction — resolved" above. *The basic attack uses `stamina`; anything costing
`qi` is not a basic attack, it is a species/family signature action* — which is `ActionKind.Innate`,
already in the sealed three-kind vocabulary. No widening, no new concept.

**D6 — Exhaustion suppresses the rider only.**
On the lawn we do not own the sim loop: PvZ fires the pea on its own cadence, and we are forbidden
from modifying vanilla projectile behaviour (`combat-damage-ssot.md:608-612`). So "no resource, no
trigger" **cannot stop the shot**. Out of `stamina` ⇒ the pea still flies and deals its
vanilla/`attackDamage` number, and **no elemental delta is added**. The actor simply waits for regen
and triggers again.

*Semantics diverge per mode by necessity, and the spec must say so plainly:* in battle, no resource
means the action is never declared; on the lawn, it means the RPG contributes nothing to a shot that
happens anyway. Both are the same rule — "the RPG gates only the RPG's own contribution" — applied to
two different levels of control over the clock.

**Explicitly rejected: decaying the stat bleed on exhaustion.** Owner, 2026-09-13: that belongs to a
**resource-exhaustion feature with real statuses/debuffs**, since the architecture already supports
adding buffs/debuffs/statuses. Doing it ad-hoc here would overlap and confuse the status system.
Tracked below.

**D7 — Regen rides the existing 100 ms kernel. Do not count frames, do not add a clock.**
The lawn's own clock is per-frame — `InjectorLoop.Tick(unscaledDeltaTime)`, called from
`MonoBehaviour.Update` / `MelonMod.OnUpdate` (`InjectorLoop.cs:9,37,224`). But a periodic scheduler
already exists and is the right host: `KernelDriveHost`, `UpkeepPeriodTicks = 100` (ms), documented
*"It stays 100 ms deliberately"* (`KernelDriveHost.cs:61,65`), today driving `KindDotPulse` and
`KindShieldUpkeep` (`:69-70,201-202`). **Resource regen becomes a third kind on that queue.**

Why not "every 60 frames": 60 frames is not one second unless the machine holds 60 fps — on a 30 fps
machine regen would run at half rate, making game behaviour depend on hardware speed. The kernel is
carry-corrected against real time and deliberately re-arms off the event's own `DueTick`, never off
"now", *"so a stuttering frame would permanently slow DoT cadence instead of merely delaying it"*
(`KernelDriveHost.cs:186-192`). Riding it inherits that correctness for free and honours the
"don't invent a clock" principle.

**Consequence for the cost cadence question:** with regen on a 100 ms real-time grid, a lawn cost is
authored against **regen per second**, per `action-ideal.md:223` (*"authored against the pool's REGEN,
never against its MAX"*). No round↔second fiction is needed. The three wires — cost row, regen
reader, lawn `CostLedger` call — must still land in the same slice, or plants dry up permanently.

---

## Doc defects found while writing this (file separately)

1. `DESIGN-GATE.md:33` states the no-damage-at-hit-time rule more broadly than its source
   (`overlay-control-loops.md:22`). As written it forbids the shipped Hot loop. Narrow it to name the
   **Server**.
2. `DESIGN-GATE.md:55` cites `battle-timeline-map.md` / `battle-turn-ideal.md` for *"Battle consumes
   FA10 only"* — grep finds the rule in neither. The real source is a code comment
   (`BattleEffects.cs:225`), and it is **three opcodes stale**: FA2, FA1 and `structure.place` were
   added (`:206,213,221`).
3. `event-pipeline-v2-ssot.md:60` says the drain budget is *"~1.5 ms"*; the shipped code is
   `Math.Clamp(frameSec * 0.10, 0.0002, 0.002)` (`EventDrainHost.cs:145-157`).
4. DESIGN-GATE's Elements row calls the two matrices asymmetric **in content**; the code shows
   identical content and asymmetric **contract** (`Same` vs `Neutral`; K applied at different
   points). `spec-shield-and-elements.md` §0 already carries the correction.
5. DESIGN-GATE's action row says `ActionTag` is 8; it is **9** (`Construct`, 2026-09-06,
   `ActionEnums.cs:57`). Seedsmith's mirror is also stale
   (`tools/seedsmith/seedsmith/adapters/actions/vocab.py:35-37`).

### A trap that caught two independent reviewers — `OVERLAY-COMBAT` is default-ON

`OverlayCombatFeature.cs:10-13` reads `Enabled => EnvEnabled || CheatState.On(CheatToggleId)`. The
**env half** (`FUSIONRPG_OVERLAY_COMBAT`) defaults false, which reads at a glance like a default-off
feature flag. It is not: the **cheat-toggle half** defaults **true** (`CheatSchema.cs:105`
`T("OVERLAY-COMBAT", true)`, plus `CheatRegistry.cs:80` and `CheatState.cs:379` both setting it).
Two separate review passes reached opposite conclusions from the same file this session. Anyone
citing this gate should cite the schema default, not the env var.

---

## Next step

**All open questions are answered (D1–D7). This doc is ready for `/spec`.**

The natural module split this doc now suggests, all of it wiring:

| Module | Wires |
|---|---|
| **lawn-hit-entry** | The `HasOnDamageDealtGrant()` guard → drain → packet, keyed on the **board fold** (not the FSM) |
| **basic-attack-grant** | Bind `ActionKind.Basic` per lawn actor at spawn so the predicate above is true; element rides the existing `elementPayload` bake |
| **basic-attack-cost** | A `stamina` cost row for `act.attack` (D5) + a regen reader + the lawn's absent `CostLedger` call + regen as a third kind on the 100 ms kernel (D7) — **these must land together**, or plants dry up permanently |
| **corpus-unblock** | `ActionCorpusComposer.cs:143` (forces `Skill`), `Program.cs:402` (24 of 179 load), `UpsertSpeciesBasics` callers |

Rate limiting is **not** a module here — proc coefficient went to the balance program (D4), and ICD
went to the deferred status feature.

Not written here; the ideal doc is where this phase stops.
