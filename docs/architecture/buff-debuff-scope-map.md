# Capability map: buff/debuff scope

Source: [buff-debuff-scope-ideal.md](buff-debuff-scope-ideal.md) — objective, grounding against shipped
code, and four resolved decisions (owner, 2026-08-29). **Status: proposed, pending owner approval.**
Module specs live in [buff-debuff-scope/](buff-debuff-scope/), one per module id, written in dependency
order once this map is approved.

## What this program is

A reusable **(WHERE × WHO)** scope primitive answering *"which population does a buff/debuff effect
reach"* — general-purpose, not commander-specific. Two WHERE values (`battlefield`, `world map`), four
WHO values (a target, a type, a unique demon, own/enemy side), and an event-driven delivery model that
reuses this codebase's existing Grant/Withdraw/Funnel machinery rather than polling or re-scanning a
population on every read.

**Explicitly not this program:** aura skill content, magnitude math, the commander concept itself
(Zomboss/Crazy Dave identity, roster), or which container carries a commander's aura. See "Deliberately
deferred" below — all of that is the ideal document's own §5, unchanged.

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `scope-model` | The WHERE/WHO types themselves; the (kind × scope) compatibility table plus `ScopeUnsupported` rejection (ideal §4.3). Pure vocabulary — no execution, no host | — |
| `battlefield-scope` | **Two hosts sharing one grant-issuing front end, corrected during audit 2026-08-29 — not one host, as first drafted.** SIM host (expeditions/web-RPG, `BattleEffectHost`/Funnel — this session's own A18 machinery) needs real new reader wiring. Live-PvZ host needs none — the injector's own overlay/Funnel path already reads grants from the same shared `EffectBag`, proven by `patron.aura`; this module only issues correctly-shaped grants into it. Both reuse `owner_kind = match`, `ActionTargetFilters`, `MatchUniqueBindingsFacet`, `EffectBag.WithdrawForOwner`/`EffectFunnel.WithdrawByPluginId`. Reacts to `membership-events` for the per-entity-grant shape; the G8-shaped side-wide-constant case (live-only) is untouched, existing behavior | `scope-model`; own-side completeness soft-depends on `membership-events` (see build order); the live-PvZ host's own proof needs a LIVE gate (owner checklist), not just SIM, matching `patron-demon`'s precedent |
| `world-map-scope` | Executes a resolved scope against `WorldState`/`IWorldView` — a genuinely new delivery mechanism, since no `BattleEffectHost` exists at this layer. Built under **explicit owner authorization** crossing [DESIGN-GATE.md](DESIGN-GATE.md) §1's World map row ("Specs pending owner review — no build authorized") — see ideal §4.2 | `scope-model` |
| `membership-events` | Extends the specimen phase FSM (`PendingSpawn → Bound → Cleared` already emits spawn/clear — nothing new there) to emit scope-membership-changed events. **Corrected during audit, 2026-08-29:** the hypnotize-toggle half is bigger than first drafted — `zombie.hypno` is a real, shipped injector event, but `MatchRuntime.cs`'s own dispatch has only a placeholder comment for it (`// W1 later: ... zombie.hypno`), never a real case. This module becomes the **first real consumer** of that event in Core, adding both the dispatch case and the tracked mind-control state it updates — not just re-announcing an existing read | — |

### Why `scope-model` is first, and why it has no seam

Every other module either executes a scope (`battlefield-scope`, `world-map-scope`) or feeds one
(`membership-events`) — none of them can be typed or tested without the WHERE/WHO shape existing first.
Matches this repo's own `P0.1`-shaped precedent (`action-plan.md` §1.1: *"no seam, this one really is
first"*) — there is no partial version of `scope-model` a dependent module could build against.

### Build order

`scope-model` → {`battlefield-scope`, `world-map-scope`, `membership-events`} — three parallel-safe
siblings, the same shape A18c/A18d used earlier in the action program.

**One soft dependency worth naming, not hiding:** `battlefield-scope`'s target/type/unique-demon
WHO-values need nothing from `membership-events` and can ship first. Only the **own-side, hypno-zombie
cross-type case** (ideal §2.3/§4.1) needs the hypnotize-toggle event to exist. This is a seam, not a
blocker — matching `action-plan.md`'s own P0.x pattern of prerequisites "three of five" of which had
"seams that let the dependent slice ship without them."

## Deliberately deferred (not in any module here)

Aura skill content and magnitude math; the commander concept itself (Zomboss/Crazy Dave as playable/AI
identities, a future commander roster, "player-first commander"); which container actually carries a
commander's aura (`world-buff.*` authoring, per the reserved-but-unused `ContainerKind.WorldBuff`); the
"commander joins battle directly" combat-participant case for expeditions/world-map/web-RPG. All of this
is the ideal document's own §5, restated here only so the boundary travels with the map.
