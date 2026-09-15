# Spec: `basic-attack-grant`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `lawn-hit-attribution`, `element-cache-invalidate`, `basic-attack-seed`

---

## Objective

`HasOnDamageDealtGrant()` is the predicate that decides whether a vanilla lawn hit reaches the RPG
layer at all. Nothing binds such a grant today, so every hit is turned away.

**The fallback basic attack is the grant that makes it true** — for every lawn actor, plant and
zombie, specimen and general creature alike.

Success: every actor on a live board carries a bound basic-attack grant whose `elementPayload` is its
own species element, so a hit produces a real elemental packet rather than a pass-through.

## Tech stack

`FusionRpg.Injector` (bind at spawn, via the existing Funnel push path) + `FusionRpg.Core` (owner
keys, atom compile). Unity-free Core half.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EffectOwnerKeys|AtomCompiler"
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-actor-hub.ps1
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Match/MatchHost.cs` | Bind at spawn, per actor |
| `src/FusionRpg.Injector/Effects/EffectRuntime.cs` | The existing Funnel push path — reuse, never a second grant route |
| `src/FusionRpg.Core/Effects/AtomCompiler.cs` | `elementPayload` bake (already built, `:237-243`) |

## The atom already exists, purpose-built

`data/seed/atoms/fx-core.json:33`:

```json
{ "family": "atom.fx-overlay-damage", "kind": "resource.delta",
  "params": { "channel": "hp" },
  "when": { "trigger": "OnDamageDealt" },
  "icdKey": "fx.overlay_damage" }
```

Two preconditions, one atom:
- **`kind: resource.delta` → `ApplyResourceDelta`**, which is what `AtomCompiler` requires before it
  bakes an `elementPayload` at all. Without a payload, `OverlayCombatMath.Finalize:42-43` returns the
  amount **unchanged** — power, defence, crit, accuracy and penetration all skipped. A Neutral-element
  actor contributes exactly zero.
- **`when.trigger: OnDamageDealt`** is exactly the trigger `HasOnDamageDealtGrant()` tests.

It also already carries `icdKey: fx.overlay_damage`, which the deferred ICD work can use later without
re-authoring the atom.

## Owner key: `entity:{ptr}`, and why it only now works

`EffectProcAndOwner.MatchesEvent` (`:106-116`) matches `entity:` keys against `ev.ActorPtr` /
`ev.TargetPtr`. Until `lawn-hit-attribution` lands, `ActorPtr` is the **bullet**, so an `entity:{ptr}`
grant bound to a plant could never match a projectile hit. That dependency is hard — this module
cannot be built or tested before it.

**Do not fall back to `plant:{typeId}`.** Type-scoped binding is what `GrantedBulletModifyAtoms.cs:37`
already does, and it is a different capability: it cannot express a *specific* buffed specimen's power,
which is the whole point.

## Bind at spawn, and unbind correctly

- Bind on the spawn edge, for **every** lawn actor — keyed on the `MatchState.Plants[ptr]` /
  `Zombies[ptr]` board fold, not on a `UniqueBinding`. A general creature has no binding and must still
  get its grant.
- **Withdraw before IL2CPP reuses the pointer.** The ordering already exists and must be honoured:
  emit die first so entity-scoped `OnDeath` can fire, then `ForgetEntity` withdraws
  (`GameHooks.cs:664/676`, `:1154/1156`; `unique-entity-effects.md:87`). A grant that outlives its ptr
  reattaches to whatever creature lands at that address next.
- Binding N actors in one tick must not issue N separate pushes — coalesce, or bind from the existing
  spawn fold pass.

## The hypno re-bake seam — [audit], and neither module owned it

> **Superseded 2026-09-15 — owner ruling (lawn-combat-wire `L-N11`): hypno changes side, never element;
> no re-bake.** A charmed zombie keeps its species, so the baked `elementPayload` stays correct. The cached
> side is object kind, not allegiance (`spec-element-cache-invalidate.md` trigger 2; control state rides
> separately as `MindControlled`), and `LawnElementResolverTests.Trigger2_hypno_cannot_change_a_cached_side_because_side_is_object_kind_not_allegiance`
> asserts the absence of a hypno trigger. The "Required" paragraph below is kept only as the history of
> the original audit and is not a requirement.

`elementPayload` is baked **at bind/compile time** from the owner's species element
(`AtomCompiler.cs:237-243`). `element-cache-invalidate` fixes the **resolver cache**, so after a hypno
a fresh *resolve* returns the new side — but this grant's **already-baked payload is still the old
element**. Neither spec covered it, and hypno is the exact trigger that module exists for.

**Required: a side change re-bakes (or re-binds) the actor's basic-attack grant**, not just
invalidates the resolver. Test it explicitly — a hypnotised zombie must deal damage with its *new*
element.

**Where the element comes from at bind time** must also be stated, because a general creature has no
`UniqueActor` to read from: `LawnElementResolverHost.Resolve(ptr)`
(`Injector/Effects/LawnElementResolverHost.cs:27-32`) → `(Side, GameTypeId)` → species → element. That
is the same path `InjectorCombatBridge` uses, and it must not become a second lookup.

## Code style

Reuse the Funnel grant path every other grant already uses. A Secondary plugin never calls
`Bag.Grant`, Unity, or `StatusExecutor` directly — Funnel enqueue only.

The element comes from the actor, never the action row:

```csharp
// AtomCompiler bakes ownerElementPrimary/Secondary into overlay["elementPayload"]
// -> DamagePacketBuilder.cs:48 reads it back onto the packet
```

That is why one shared `act.attack` row still yields per-species elemental damage
(`action-ideal.md:158-160`), and why per-species basic attacks are not required for this feature.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | A grant compiled for an owner with a species element bakes a non-empty `elementPayload`; a Neutral owner bakes none and the packet passes through unchanged (the documented degenerate case) |
| Core unit | `entity:{ptr}` matches an event whose `ActorPtr` is that ptr; does not match when it is the bullet's |
| Injector | Every spawned actor holds the grant; `HasOnDamageDealtGrant()` becomes true on the first spawn |
| Injector | On death, the grant withdraws **before** the ptr can be reused; a new actor at the same address does not inherit it |
| Injector | Binding many actors in one tick issues no per-actor push storm |
| Live | `lawn-combat-live-proof` — the falsifier is a Fire actor and an Ice actor dealing *different* damage to the same target |

## Boundaries

- **Always:** bind per actor on the board fold; withdraw before ptr reuse; element from the actor.
- **Always:** reuse the existing Funnel push path.
- **Ask first:** binding anything beyond the basic attack at spawn — every additional always-on grant
  pins another trigger-mask bit and costs per-hit work.
- **Never:** `plant:{typeId}` as a substitute for `entity:{ptr}`; a second grant route; a grant that
  survives its pointer; authoring the element on the action row.

## Success criteria

- [ ] `HasOnDamageDealtGrant()` is true on a live board with any actor present.
- [ ] A hit produces a packet carrying the attacker's own species element.
- [ ] A Fire actor and an Ice actor deal measurably different damage to the same target — the proof
      that element rides the actor, not the row.
- [ ] General creatures (no `UniqueActor`) are granted identically to Bound specimens.
- [ ] Grants withdraw before ptr reuse, proven by a test that recycles an address.
- [ ] No per-actor push storm on a mass spawn.
