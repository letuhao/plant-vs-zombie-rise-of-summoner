# Implementation plan: `lawn-combat-wire`

**Program:** `lawn-combat-wire` · **Map:** [../docs/architecture/lawn-combat-wire-map.md](../docs/architecture/lawn-combat-wire-map.md)
**Ideal:** [../docs/architecture/lawn-combat-wire-ideal.md](../docs/architecture/lawn-combat-wire-ideal.md) (D1–D9)
**Specs:** `docs/architecture/lawn-combat-wire/spec-*.md` (11 modules)
**Task list:** [lawn-combat-wire-todo.md](lawn-combat-wire-todo.md)

---

## Overview

Make a vanilla PvZ lawn hit carry RPG elemental damage. The RPG stat layer already reaches the lawn
(proven live: a buffed Peashooter's peas really do 1350); the RPG *combat* layer does not — 197
`combat.*` channels resolve on live entities and touch nothing.

Almost all of this is wiring. The overlay damage stack, the element ring, the VFX, the Funnel and FA10
are built and default-on; what is missing is the entry point, an attacker identity, a grant, and a few
numbers. Three of the eleven modules are **pre-existing defects worth landing regardless** of whether
this program ever ships.

## Architecture decisions carried from the ideal

- **D1 — two lanes, both live.** `progression.bonus.*` → `EntityStatWriter` → vanilla fields (incl.
  `attackDamage`) is the stat-bleed lane; `combat.*` → overlay damage is the other. **`combat.*` must
  never join `MergeAppliedCombat`** — that is the one prohibition the design rests on, and Task 5
  guards it. Balance is a separate program.
- **D3 — the lawn is a reflection with ≥1 step delay.** We do not own PvZ's sim loop. Byte-identical
  determinism belongs to siege/delve/world assault, never here.
- **D6 — exhaustion suppresses the rider, never the shot.** We may not modify vanilla projectile
  behaviour, so "no resource, no trigger" gates the RPG's own contribution only.
- **D8 — one attack is one action trigger.** A piercing pea hitting five zombies is one swing, five
  damage applications.
- **D9 — an effect-bearing lawn hit is carried and coalesced, never dropped.** `combat.hit` is
  droppable today *because it has no consumer*; this program changes that class.

## Dependency graph

```
T3 element-cache-invalidate ─┐
T4 combat-numerics ──────────┤
T5 resource-subtick ─────────┤   (independent; disjoint files; shippable alone)
T6 lawn-hit-attribution ─────┤
T7 basic-attack-seed ────────┘
                │
                ├─► T8  lawn-action-bridge      (needs T7)
                └─► T9  lawn-hit-entry          (needs T6, T4)
                            │
                            └─► T10 basic-attack-grant   ⚠ STRICTLY AFTER T9
                                        │
                        T11 calibration ─┼─► T12 basic-attack-cost   (needs T5, T8, T9, T10)
                                         │
                                         └─► T13 lawn-combat-live-proof
```

## The one hard ordering constraint

**`basic-attack-grant` (T10) must not ship before `lawn-hit-entry` (T9).** Binding the grant flips
`HasOnDamageDealtGrant()` true for every actor, opening `EventDrainHost.cs:48/72` for every hit — with
none of T9's liveness guard, never-drop rule, swing dedupe or instakill guard in place. That means
per-victim triggering (violating D8), the `next <= 0` → `ForceKill*` double-`Die()` path live, and a
rider on the lawnmower's 1,000,000-damage event.

**A half-deployed program is worse here than an undeployed one.** This is a real ordering constraint,
not a preference.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The feature ships **silently inert** — `resource.max.stamina` is 0 for every lawn actor today, so under D6 "no delta" is indistinguishable from the bug we are fixing | High — looks like failure, is actually an unwired pool | T12 carries an explicit *"pool max is non-zero"* acceptance criterion, checked before anything else in that task |
| Per-hit cost regression — T10 pins the damage trigger-mask bit **on** permanently for every actor | High | T13 re-measures with the mask on. Ceiling **≤ 6% frame share at 300z** (existing figure: 4.44% with the mask off). On breach the feature ships behind the kill switch **defaulted off**, not green |
| A live probe run inside a debug session proves nothing — `EventDrainHost.Active` is false when `DebugRuntime.SessionActive`, and `EmitOverlayBreakdown` only emits *inside* one | High — this already happened once this session | T13 requires running outside a debug session and says how to confirm (a stamped `scenarioId` is the tell) |
| `OVERLAY-COMBAT` off with the grant bound ⇒ actors pay stamina for a delta that is dropped — **worse than undeployed** | Medium | T10 ships a feature-specific kill switch that disables grant-binding and cost-charging **together** |
| Two same-shape errors already occurred this session (inferring a call path from a component's existence) | Medium | T1/T2 are *reads*, scheduled before any code, precisely to stop a third |
| Four attack methods are unhooked (`QingZombie.AttackPlant` override, two `AttackPlants()`, two plant-side `AttackEffect(List)`) — those creatures deal **zero** elemental damage | Medium | T6 hooks them; accepting the gap is a scope change to be asked for, not a way to pass the task |

## Verification posture

Three of eleven modules are pre-existing defects (T3, T4, T5) and each must land **without moving a
golden** — they are correctness and representation fixes, not balance changes. If a golden moves in
T4, the conversion is wrong, not the golden.

The program's own proof is T13, and every proof there is paired with a falsifier. A passing run with
no negative case proves nothing; that is the lesson this program was created from.

## Open questions

None blocking. T1 and T2 are **tasks, not gates**: T1 is a file read, and T2 has a stated reversible
default (ship the first increment uncosted, restore cost in a later slice) so it cannot stall the
plan. Neither protects an irreversible action.

The genuinely deferred work — elemental reactions, status resolver, ICD, proc coefficient, plant-side
status, resource-exhaustion debuffs, per-actor vanilla defense — is tracked in the ideal's
"Deferred, tracked" table and owned elsewhere.
