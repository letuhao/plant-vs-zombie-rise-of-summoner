# Ideal: buff/debuff scope

**Status: discussion draft, 2026-08-29 — not a spec, no build authorized.** Captures the shape of the
problem and a proposed direction so the next step (a Phase 0 capability map, if the owner confirms the
shape) starts from grounded material instead of a blank page. Matches this repo's own ideal → map →
spec sequence ([action-ideal.md](action-ideal.md), [class-system-ideal.md](class-system-ideal.md),
[demon-system-map.md](demon-system-map.md) all preceded their specs the same way).

**Deliberately narrow.** The owner's own sequencing: build the scope primitive first; the aura skill
content and the commander concept itself (Zomboss, Crazy Dave, "join battle directly") are a **separate,
later discussion**. Nothing here decides commander identity, roster, or aura magnitude math — this is
only "which population does a buff/debuff effect reach," as a reusable primitive other systems can call,
not a commander-specific mechanism.

---

## 1. Objective

A general answer to: **given a buff or debuff effect, which entities does it actually reach?**

Not scoped to commanders. The owner's own framing: *"this apply for multiple gameplay"* — patron-demon's
aura, a future world-buff, an item aura, a status that spreads to allies, are all instances of the same
question, and today each would have to work it out from scratch.

Two things this is explicitly **not**:
- Not a new effect *kind* (that vocabulary — `stat.modify`, `status.apply`, `resource.delta`,
  `shield.grant` — is closed and already covers "what happens." This is "who it happens to.")
- Not a targeting system for a single cast (`ActionTargetSpec` already owns that, for one action
  resolving against one caster, at cast time). This is for a **standing** effect — a grant that sits on
  the board for the life of a match/turn/etc. — deciding, possibly repeatedly, who is currently in scope.

## 2. What already exists — read this session, cited

Four real mechanisms already answer pieces of this question, in different corners of the codebase. None
of them were built as a general "scope" system, but all four are candidates to compose rather than
duplicate.

### 2.1 `owner_kind` — the effect-atom layer's own scope table

[effect-atom/definitions.md §6](effect-atom/definitions.md) already has a closed, validated vocabulary
for where a grant is anchored:

| Scope | `owner_kind` | `owner_key` |
|---|---|---|
| match | `match` | `''` |
| plant type | `plant` | typeId |
| zombie type | `zombie` | typeId |
| entity | `entity` | live ptr, hex, session-scoped |
| player | `player` | decimal id |
| sector | `sector` | world-map sector id |
| slot | `slot` | world-map construction slot |

This is an **anchor/ownership** scope — where a grant's row lives and what it's keyed on — not a
population query. But `match` is already exactly "battlefield-wide," and `patron.aura`
([demons/spec-patron-demon.md](demons/spec-patron-demon.md)) is already described as *"a match-owner
effect grant"* — so **the battlefield case (PvZ lawn, and by the same shape expeditions/web-RPG
battles) is not new work.** It already has a working, shipped precedent.

`sector`/`slot` exist but are construction-slot-scoped today, not "a standing effect over a world-map
faction or legion." **World map is the one row in this table that doesn't yet mean what a commander-scope
would need it to mean.**

**G8 is a warning worth repeating here, not just citing:** *"`stat.modify` on `defense` is legal only at
`match` scope. `plant:N`, `zombie:N`, and `entity:` all reject with `ScopeUnsupported`."* Scope and kind
are **not** orthogonal today. Whatever scope system ships needs its own compatibility answer per (kind ×
scope), not an assumption that any effect can target any scope.

### 2.2 `ActionRelation` / `ActionTargetFilters` — the action layer's per-cast targeting

[ActionTargetSpec.cs](../../src/FusionRpg.Core/Actions/ActionTargetSpec.cs) (built this session, Phase
2/A2 of the action program) already has exactly the "own side vs. enemy side" relation the owner
described — `ActionRelation.Self/Ally/Enemy/Any`, resolved **relative to the caster**, compiled to one
`TargetSpec` per possible caster side so one authored row serves both factions. And
[ActionTargetFilters.cs](../../src/FusionRpg.Core/Actions/ActionTargetFilters.cs) already has `TypeIds`
(the "a type" scope) and, notably, `ExcludeMindControlled` — **a boolean that already exists because PvZ
already needs to reason about hypno-zombies crossing the plant/zombie type boundary.**

This is single-cast, resolved once at the moment an action fires. A standing buff/debuff scope needs the
same relation vocabulary but evaluated **repeatedly** (every time a population might have changed —
someone died, someone spawned, someone got mind-controlled), not once.

### 2.3 `MatchUniqueBindingsFacet` — durable specimen ↔ live entity, already built

[Match/UniqueBindings.cs](../../src/FusionRpg.Core/Match/UniqueBindings.cs) already resolves both
directions: `TryGet(instanceId) → Ptr` and `TryGetByPtr(ptr) → instanceId`, tracked through
`PendingSpawn → Bound → Cleared`. This is precisely the "unique demon" scope's resolution step — a
commander's aura naming a durable demon specimen resolves to a live `entity:{ptr}` through this facet,
**with no new binding table needed.**

**One real wrinkle, found reading this file, not assumed:** `UniqueBinding.Side` is hard-normalized to
`"plant"` or `"zombie"` only (`NormalizeSide`, line 217-221) — there is no third value. A hypno-zombie
demon binds with `Side = "zombie"` because that's what it deploys as in Unity. **So "my own side" cannot
be read off this field alone.**

**Resolved 2026-08-29 (owner) — and the real rule is sharper than a plant/zombie split.** There are two
distinct kinds of hypno-zombie on the board, not one:

1. **A player-deployed demon in hypno-zombie form** (a specimen the player captured/summoned — see
   `demon-system-map.md`'s *"designated boss-class species deploy as hypno-zombie allies"*).
2. **An ordinary vanilla zombie temporarily mind-controlled by a plant hit** (PvZ's own classic
   Hypnotize mechanic) — mechanically fighting for the player, but **still Zomboss's for buff-scope
   purposes.** The owner's own words: *"zomboss's hypno zombie still consider as his side and earn
   zomboss's buff, not crazy dave's buff."*

So the deciding signal is **ownership identity, not current combat allegiance.** A status effect (charm/
mind-control — a well-established pattern, not unique to this game: see Sources) can flip who a unit
*fights for* without changing who it *belongs to* for scope purposes. Case 1 has a durable specimen
record with a real `player_id` (confirmed: `RpgStore.Demons.cs` / `RpgStore.UniqueActors.cs`) — case 2
has no specimen at all, just a plain zombie-type entity with a temporary status flag. **"Own side" for a
buff/debuff scope resolves through specimen ownership when a specimen exists, and falls through to the
mechanical PvZ type only when it doesn't** — never the other way around. See §4 for the FSM work this
implies.

### 2.4 `ContainerKind.WorldBuff` — reserved, wired, unused

[ContainerRow.cs:14](../../src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs) already has a `WorldBuff`
member; [ContainerValidator.cs:17](../../src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs) already
accepts the `world-buff.*` id prefix; `RpgStore.Containers.cs:348` already round-trips it. **Nothing has
ever authored a `world-buff.*` row.** This is real, tested plumbing sitting idle — the closed-vocabulary
discipline this repo enforces (DESIGN-GATE: *"Adding one is a reviewed change, not a convenience"*)
already anticipated something in this shape. A commander aura is a strong candidate to be the first real
`world-buff.*` content, once the aura-skill discussion (deferred, per the owner) gets there.

### 2.5 `ZoneOfControl.IsHostile` — the world map's own, separate relation

[world/spec-ai-commander.md](world/spec-ai-commander.md) §ThreatMap: *"a pure faction-id comparison, so
it is belief-safe."* This is the world map's own answer to "own side vs. enemy," structurally unrelated
to `ActionRelation` — different data (`WorldState`/`IWorldView`, factions and legions, not
`BattleEffectHost`/`ActorState`). The demon map's own words about a parallel case are worth repeating
here: *"Two different catalogs, deliberately... Collapsing them would make [X] and [Y] the same axis."*
**A unified scope system should let battlefield and world-map both express "own side," without merging
their two relation mechanisms into one.** They answer the same question over structurally different
state.

## 3. Proposed shape — two orthogonal axes, not one enum

Following this codebase's own established pattern (`ActionTargetSpec` already separates Mode × Relation
× Filters × Ordering rather than one flat enum), a buff/debuff scope reads as a **(WHERE × WHO)** pair.

### WHERE — execution context, reusing `owner_kind` rather than inventing a parallel table

| WHERE value | What it resolves against | New work? |
|---|---|---|
| `battlefield` | **Corrected during audit, 2026-08-29 — not one host.** `owner_kind = match` and the `EffectBag`/Funnel grant mechanism are shared, and *that* part needs no new work, proven by patron-demon. But "PvZ lawn" and "expeditions/web-RPG" are not the same **reader**: `BattleEngine`/`BattleEffectHost` (this session's A17/A18 work) is the SIM/expedition kernel and never runs for live PvZ; live PvZ's own damage path is Unity-side and the RPG's write side deliberately never touches it (`EntityStatWriter.cs`: *"Never TakeDamage"*). So the SIM reader is real new work (`battlefield-scope`'s own spec); the live-PvZ reader is not — the injector's existing overlay/Funnel path already does it, proven by patron.aura. See `buff-debuff-scope/spec-battlefield-scope.md` for the full split |
| `world map` | A `WorldState` faction/legion/sector row, over many turns | **Real, new — and explicitly in scope for v1 (owner, 2026-08-29: "build both now, full parity").** No `BattleEffectHost` exists here at all — this is closer to a `WorldCanonical` state field than an `EffectGrant`, so it needs its own delivery mechanism, not a variant of the battlefield one. **Confirmed during audit:** `TurnEngine.Step` is a pure pipeline of `with`-expression rewrites (`WorldCanonical.cs`/`TurnEngine.cs` read directly) — a world-map buff is one more such rewrite, not a new mutation mechanism. **Building this crosses the World Map row's own standing caution — [DESIGN-GATE.md](../DESIGN-GATE.md) §1: "Specs pending owner review — no build authorized." That caution is explicitly lifted here, by the owner, for this scope only** — the same shape this repo already used for `P0.2`–`P0.5` in the action program (`tasks/action-todo.md`: "unblocked by building it across the program boundary under explicit owner authorization"). Worth a `decisions.md` line when this reaches a real spec, so the authorization is traceable later, not just in this conversation |

Naming the asymmetry explicitly because it's easy to miss: "battlefield" and "world map" *sound* like two
values of one enum. They are not two branches of the same executor — one already has a Funnel, an
`EffectGrant`, an owner_kind. The other has none of that machinery and needs its own, built from
scratch, under the authorization above.

### WHO — population selector, reusing `ActionRelation` + adding classification/identity

| WHO value | Reuses | New work? |
|---|---|---|
| a specific target | `entity:` owner_kind directly | None |
| a type | `ActionTargetFilters.TypeIds` | None |
| a unique demon | `MatchUniqueBindingsFacet` (§2.3) | None for plant-side; resolved for hypno-zombie-demons via specimen ownership (§2.3) |
| own side / enemy side | `ActionRelation.Ally/Enemy/Any`, **resolved through specimen ownership when a specimen exists, mechanical PvZ type otherwise** (§2.3) | Real: not a population re-query — see §4's resolved delivery model |

## 4. Resolved decisions (owner, 2026-08-29)

### 4.1 Own-side resolution — ownership identity, via FSM events, not a per-read population scan

Settled in §2.3: a hypno-zombie demon resolves "mine" through specimen ownership; an ordinary
hypnotized zombie stays Zomboss's regardless of who it currently fights for.

**Delivery mechanism (this question and the old §4.4 below converge on one answer):** researched
against Unreal GAS, which this codebase already treats as studied prior art elsewhere
([definitions.md §14.1](effect-atom/definitions.md)). GAS's own aura pattern is **not** "recompute a
population on every read." An aura is an infinite-duration Gameplay Effect; an Ability Task watches for
a membership-change event (there, spatial overlap enter/exit) and, on that event, **grants or removes a
small, real per-entity effect on the affected actor** — see *Gameplay Effects for the Gameplay Ability
System* and the Ability-Task overlap pattern (Sources, below).

That maps directly onto machinery this codebase already has, not a new concept:

- **The membership-change events, here, are FSM transitions** — the specimen's own phase FSM
  (`PendingSpawn → Bound → Cleared`, `Match/UniqueBindings.cs`) already emits spawn/clear. The
  hypnotize-on/hypnotize-off half is real but bigger than first assumed: **corrected during audit,
  2026-08-29** — `"zombie.hypno"` is a genuinely shipped injector event, but `MatchRuntime.cs`'s own
  dispatch has only ever carried a placeholder comment for it, never a working case (`Match/
  MatchRuntime.cs:110`: `// W1 later: ... zombie.hypno`). So this is not "wire an event that already
  flows somewhere" — it is building the first real consumer of an event that already arrives but has
  never been acted on. **This is still the "extend our FSM" the owner asked about**, just a real new
  dispatch case plus new tracked state, not a one-line addition to existing handling.
- **The reaction to that event is an ordinary grant/withdraw**, exactly like every other effect in this
  codebase (`EffectBag.Grant`/`ClearGrant`, patron.aura's own "applied once at `board.start`, withdrawn
  at `board.end`" lifecycle) — a commander aura's source subscribes to the relevant FSM transitions and
  grants or withdraws a per-entity `EffectGrant` accordingly. Reading "is this entity currently buffed"
  then costs exactly what reading any other grant costs today — nothing new.

This is a stronger answer than any of the three originally-offered options: it avoids both the
redundant-rescan cost of polling every round and the "new event plumbing for every transition" risk
named against the event-hook option — because the FSM transitions it needs (spawn, clear, and the new
hypnotize-toggle) are a small, enumerable, already-FSM-shaped set, not an open-ended list of things to
remember to hook.

### 4.2 World-map scope — in v1, full parity, under explicit owner authorization

*"Build both now, full parity."* This deliberately crosses [DESIGN-GATE.md](../DESIGN-GATE.md) §1's
World map row ("Specs pending owner review — no build authorized") — recorded here so the crossing is
traceable, the same way `tasks/action-todo.md` recorded each `P0.x` cross-program build this session as
*"unblocked... under explicit owner authorization."* **A real `decisions.md` line is worth adding once
this reaches a spec**, not just this document, so a future session doesn't re-trip the same caution.

Consequence for §3: `world map` is a first-class WHERE value from the start, not a reserved-but-unbuilt
slot. It still needs its own delivery mechanism — no `BattleEffectHost` exists at that layer — but that
mechanism is now in scope to design, not deferred.

### 4.3 Compatibility enforcement — table + runtime rejection

Mirrors this repo's own existing two-phase pattern (`definitions.md` §10): a reviewed (kind × scope ×
WHERE) table as the primary, checked contract; `ScopeUnsupported` as defence-in-depth for anything the
table misses. No further discussion needed — this matches established precedent directly.

### 4.4 Re-evaluation model — resolved by §4.1

Folded into §4.1 above: not a separate re-query or polling mechanism at all. An aura's currently-affected
set is never stored or rescanned — it is exactly "whoever currently holds a live per-entity grant from
this source," maintained by FSM-event-driven grant/withdraw, read the same way every other grant is read
today.

## 5. Explicitly deferred (the owner's own sequencing)

Aura skill content and magnitude math; the commander concept itself (who Zomboss/Crazy Dave are as
playable/AI identities, "player-first commander," a future commander roster); which container/grant
actually carries a commander's aura in a real match; anything about `world-buff.*` authoring. All of
that is the **next** conversation, once this scope shape is confirmed or corrected.

## 6. Suggested next step

Wants a short Phase 0 capability map before a real spec — "scope definition" (the WHERE/WHO model
itself), "battlefield scope execution" (reusing owner_kind + Funnel), "world-map scope execution" (new
delivery mechanism, owner-authorized per §4.2), and the FSM extension from §4.1 (spawn/clear already
exist; the hypnotize-toggle transition does not) read as separable, differently-risky pieces, matching
how `effect-atom-map.md`'s own E14a/E14b split worked. Not proposed as final here — that split is itself
a decision worth a deliberate look, not something to infer from this document alone.

## 7. Sources

Researched 2026-08-29 for §4.1's delivery-mechanism decision:

- [Gameplay Effects for the Gameplay Ability System in Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/gameplay-effects-for-the-gameplay-ability-system-in-unreal-engine) — Infinite-duration effects, the shape an aura uses
- [Gameplay Ability System for Unreal Engine](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-for-unreal-engine) — Ability Tasks, including overlap/radius watches that add or remove an effect on membership change
- [Understanding the Unreal Engine Gameplay Ability System](https://dev.epicgames.com/documentation/en-us/unreal-engine/understanding-the-unreal-engine-gameplay-ability-system)
