# Resource Hub SSOT — actor pools, scope, accrual, exhaustion

**Status:** Design locked (docs). **Not built** — no `resource.*` channel family exists yet.
**Parent:** [decisions.md](decisions.md) (ADR row **Resource model**, 2026-08-22).
**Channels:** [actor-hub-ssot.md](actor-hub-ssot.md) §3.G. **Exhaustion vehicle:**
[status-ssot.md](status-ssot.md). **Consumers:** [action-map.md](action-map.md),
[battle-timeline-map.md](battle-timeline-map.md), [effect-atom-map.md](effect-atom-map.md).

**Supersedes** [resource-hub-ideal.md](resource-hub-ideal.md), which is retained only as the
reasoning trail. That document's §2, its header "refused names" bullet, and its §10.2 table were
already superseded by its own §10.2a on 2026-08-22 and are **not** authoritative; reading them as
current is the mistake this file exists to prevent.

---

## 1. The model

**Six actor resources. One shared set. Both factions carry all six.**

| id | Class | Exhaustion | Notes |
|---|---|---|---|
| `hp` | body | **none** | Depletion is death, already owned by the turn FSM's `Downed` state |
| `stamina` | body | ✅ debuff | |
| `hunger` | energy | ✅ debuff | |
| `spirit` | essence | ✅ debuff | Extinguished spirit is what the summoner harvests *as* soul. **Never an action cost** |
| `qi` | essence | ✅ debuff | **Skill fuel** — see §2 |
| `poise` | body | ✅ debuff | **Guard** — see §2. Registered 2026-08-26 (class-system, `poise-resource`); exhaustion is breaking guard, not death, so it stays a debuff like the other four |

There is **no faction branch anywhere in the model.** Plants and zombies hold the same six pools
with the same ids, the same polarity, the same channels and the same mechanics. Everything that
differs between them is a string chosen at the display layer.

---

## 2. What each resource pays for

**Decided 2026-08-22, `poise` added 2026-08-26.** Three pools are action costs and they split by
*kind of effort*:

| Pool | Pays for | Exhausted means |
|---|---|---|
| `stamina` | **Physical actions** — move, basic attack, reposition | The actor can still act, but the body is failing: derived-stat debuff |
| `qi` | **Skills and abilities** — anything with a trigger, an element, or a container of atoms behind it | No skills. The actor falls back to physical actions only |
| `poise` | **Guarding** — a flat commit cost to raise a guard, drained further in proportion to what it absorbs (spec-guard-economy.md §3) | Guard breaks. The actor can still act, but cannot absorb: derived-stat debuff, never death |
| `hunger` | **Metabolic cost** — and for plants this is **Sun**, so a sun-priced action is a `hunger` cost. Also still **sustain**: it gates regeneration and condition | Metabolic failure: derived-stat debuff |
| `spirit` | **Essence cost** — spending what the actor *is*. Also what the summoner harvests as soul when it is extinguished | Identity failure: derived-stat debuff |
| `hp` | **Sacrifice cost** — paying with the body itself | Death — owned by the turn FSM's `Downed` state, not by exhaustion |

> ### ⚠️ Corrected 2026-08-30 — all six resources are legal action costs
>
> This table previously read *"`hp` — Nothing"*, *"`hunger` — Nothing directly"*, and **"`spirit` is
> never an action cost"**. **That was a design defect, not a rule.** Owner, 2026-08-30: *"any resource
> can be cost for actions, like hp sacrifice action — how can we make something like that if we can't
> pay for hp?"*
>
> The rule made three legitimate designs unbuildable — an HP-sacrifice action, a sun-priced plant
> action (`hunger` **is** Sun on the plant side, and spending sun is the core PvZ verb), and any sink
> at all for `spirit`, which had **none**. A resource with no sink is not a resource.
>
> **Every resource must document what spending it *means*** — the "pays for" column above is now
> normative, not descriptive. A new cost on a resource whose meaning is undecided is an authoring
> error.
>
> **`hp` costs floor at 1 by default**, refusing with the existing `CannotAfford(hp)` typed reason —
> *but an action may explicitly opt into being lethal*, because true sacrifice is a design the owner
> wants available. A lethal cost is a **per-action opt-in**, never the default.

**`stamina` no longer claims guard** (moved 2026-08-26): a guard is its own kind of effort, not a
physical action, and it needed its own pool once `guard-economy` required one the resolver could
target without also draining move/attack. This is the distinction `qi` and `spirit` needed, since
both sit in the `essence` class: **`qi` is what an actor channels; `spirit` is what an actor is.**
One is spendable and refills; the other is depleted only by harm and is the thing the summoner
mechanism ultimately collects.

**Consumer note.** [action-map.md](action-map.md) currently models a single cost pool. Two cost
pools means an action declares *which* it draws on. That is a field on the action, not a branch —
and the "validate all, consume all at commit, roll back on any failure" rule already stated there
applies unchanged across both.

---

## 3. Display labels — the only faction difference

| id | Plant label | Zombie label |
|---|---|---|
| `hp` | HP | HP |
| `stamina` | Stamina | Stamina |
| `hunger` | **Sun** | Hunger |
| `spirit` | Spirit | Spirit |
| `qi` | **Yang** | **Yin** |
| `poise` | Poise | Poise |

**Labels are content.** They are never a channel id, never a branch, never a key, and never
serialized into a battle report. A label change is a content edit and moves nothing.

`qi`, `yin` and `yang` were verified against `src/` and the web app and collide with nothing.

---

## 4. The two things called "Sun" — read this before writing any UI

This is the single most confusable point in the hub, and it is not a conflict once stated.

| | Lawn sun | The plant's `hunger` pool |
|---|---|---|
| Layer | **`pvz.*`** — the game foundation | **`rpg.*`** — this hub |
| Scope | **Match** — one shared bank | **Actor** — one pool per creature |
| What it is | The sunflower → bank → plant economy | Metabolic energy an actor spends on actions and skills |
| Owned by | `SimEngine` / `SimModels`, untouched by this hub | The Actor Hub |
| Displayed as | "Sun" on the lawn HUD | "Sun" on a plant's resource meters |

They are two different things that share a word, the way two programs can both have a variable
called `count`. There is nothing to bridge, and **an RPG resource never reads a PvZ value** — if the
RPG wants to know something about the lawn it arrives as a captured event fact, like any other
telemetry.

**Consequence for the GUI:** a surface showing both must distinguish them by *scope*, not by name —
the match bank belongs to the stage HUD, the actor pool belongs to the actor's meters.

---

## 5. Registry shape

Every resource declares:

| Field | Values | Why it exists |
|---|---|---|
| `id` | `hp` · `stamina` · `hunger` · `spirit` · `qi` · `poise` | Closed set; adding one is an ADR (`poise` added 2026-08-26 — decisions.md *Resource model*) |
| `scope` | `actor` · `side` · `match` · `player` | Resolves the sun and soul ambiguities without renaming anything |
| `class` | `body` · `energy` · `essence` | |
| `polarity` | `asset` · `burden` | Decides what every generic operation means — §6 |
| `accrual` | `none` · `regen` · `onEvent` · `generated` | §9 |
| `bounds` | max channel, floor, whether it may exceed max | |
| `onEmpty` / `onFull` | what happens at the rail | Death at zero `hp`; exhaustion at zero for the rest |
| `visibility` | which UI surfaces show it | Not every pool is a bar |
| `labels` | per-faction display strings | §3 — content, never a key |

**The registry is data.** Adding a sixth resource costs a row, not a system. That property is the
reason this file exists before any of it is built.

---

## 6. Polarity — all five are assets today

`polarity` decides what every generic operation means. Without it, the moment a shared path says
`Regenerate(resource, amount)`, half the resources would heal and half would get worse.

**Under the locked set, all five resources are `asset`:** they fill up, you spend them, empty is bad.
`hunger` is a fed/starving gauge in the ordinary survival-game sense — **full is good** — not a
rising affliction.

The field is retained rather than dropped because it is free now and a rewrite later, and because
`burden` remains available if a future resource genuinely needs it. **No resource in the locked set
uses it.** A proposal to add a burden is an ADR, not a content edit, because it changes what every
generic operation means.

---

## 7. Layer — `rpg.*`, never the PvZ write channel

Resources exist for **our** mechanics: actions, skills, costs, the turn kernel. They are not PvZ
attributes.

| Layer | Owns | Reaches Unity |
|---|---|---|
| `pvz.*` — the game foundation | `StatChannels` (`hp` `maxHp` `atk` `defense` `arm1` `arm1Max` `arm2` `arm2Max`), facts, intents | Yes — `EntityApply` → `EntityStatWriter` is the only write path |
| `rpg.*` — content and progression | Derived channels in the Actor Hub, overlay combat, status, shields, **and these resources** | **No, by design** |

`hp` is the single exception, and only in PvZ mode, because Unity is SSOT for current HP there.
In **standalone / web mode there is no Unity at all** and the server's battle engine owns the state
outright — so the RPG battle is the unconstrained runtime and PvZ mode is the special case, not the
reference.

Asking whether `stamina` reaches a Unity field is a category error; the layering exists so that it
does not.

---

## 8. Channels and current values

**Magnitudes are registered Actor-Hub derived channels (F8, reconcile pass, 2026-08-25 — shipped by
[spec-actor-channels.md](derived-stats/spec-actor-channels.md), no longer hypothetical):**

```text
resource.max.{id}      resource.regen.{id}
```

`resource.efficiency.*` is a third, registered alongside them (`SumIncreased`, capped at 1.0 —
`DerivedStatPolicy.ResourceEfficiencyCap`).

> ### ⛔ Six-coverage rule (owner, 2026-09-02) — normative
>
> **Every derived-stat family that affects a resource MUST cover all six resources.**
> `ResourceIds` is `{ hp, stamina, hunger, spirit, qi, poise }` and it is the only list. A family that
> covers a subset is a **defect, never a feature** — actions can cost any of the six
> (`ActorResourcePools.cs`: *"All six resource pools for one actor"*), so a family covering three means
> **only three resources have a stat that governs them**, which is the design error this rule exists to
> forbid.
>
> **This applies to the aptitude edges and to every hand-maintained list, not just to registration.**
> `DerivedStatRegistry` already loops `ResourceIds` and is correct by construction. The drift is
> everywhere a list was typed by hand.
>
> **Open defects as of 2026-09-02** — see
> [`../research/resource-symmetry-audit-2026-09-02.md`](../research/resource-symmetry-audit-2026-09-02.md):
>
> | Layer | max | regen | efficiency |
> |---|---|---|---|
> | Registered channels (loops `ResourceIds`) | 6/6 ✅ | 6/6 ✅ | 6/6 ✅ |
> | **Aptitude edges** (`aptitudes.v2.json`) | 5/6 — no `poise` | 5/6 — no `poise` | **3/6 — no `hp`, `spirit`, `poise`** |
> | **`DominanceGuard.ReservedFamilies`** (hand-listed) | 4/6 | 4/6 | 3/6 |
>
> **`poise` has zero aptitude edges of any kind**, which is why `guard-economy` is blocked
> (`class-system/spec-poise-resource.md` §1; tracked as P7.2). **`resource.efficiency` has only four
> edges in the whole game** — Agility→stamina, Focus→{hunger, qi, stamina}.
>
> **Fix direction: derive, never hand-list.** Any code or data that enumerates resources should loop
> `ResourceIds` so a seventh resource is covered by construction, the way registration already is. All three form **their own family list and do not join
`CombatChannelFamilies`/`AllCombatChannelIds`**, which is exactly **28 families / 196 channels** today
(reconcile pass, F6/F9, 2026-08-25 — was 12/84 when this doc was first drafted; see
`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs`'s `CombatChannelFamilies` for the canonical
count, not a hand-copied number). Registration rules are the Actor Hub's
([actor-hub-ssot.md](actor-hub-ssot.md) §3.G): unknown channel → reject. Proven live end-to-end by
`tests/FusionRpg.Core.Tests/Stats/ActorChannelsTests.cs` (`ResourceChannelsNotInCombatRoster`,
`LazyValueMatchesTicked`, `EfficiencyCannotExceedOne`, `MaxAndRegenUncapped`).

**Current values are not channels.** They are per-actor runtime state resolved **lazily**:

```text
value + rate × (now − lastTick)
```

following the standing compute-on-read law. 200 actors × 4 regenerating pools would otherwise be
800 recurring events against a 0.15 ms kernel slice.

**Therefore exhaustion is re-evaluated on read, not only on write.** A pool that decayed past its
rail while nothing touched it is exhausted the moment anything looks at it.

---

## 9. Accrual — three shapes

| Shape | Example | Structure | Owner |
|---|---|---|---|
| **Regen** | stamina per tick | A rule *on the pool*, driven by the timeline kernel | This hub |
| **On event** | spirit on kill | A rule *on a trigger* | **The effect-atom program** — resources declare *that* they can be granted; atoms declare *when* |
| **Generated** | a sunflower producing into a side bank | An actor with an output, writing into a pool it does not own | This hub |

Keeping the second line is what stops this becoming a fifth content system.

---

## 10. Exhaustion

**Every resource except `hp` has an exhaustion mechanism that debuffs derived stats.**

- Expressed as a **status**, reusing `StatusRuntime`'s instances, stacking, resistance, VFX cues and
  `icd_ms` — the last of which is what stops apply/clear flicker at the rail.
- The debuff is a **container of atoms**, never a hardcoded channel list.
- **An exhaustion debuff must never touch a channel feeding its own resource's regen.** That is the
  only true spiral, and it is rejectable by validation.
- `hp` is exempt: depletion is death, owned by the turn FSM's `Downed` state.

---

## 11. Persistence

Pools **persist across a run and refill at rest.** They are not per-encounter.

---

## 12. What this hub does not own

| | Why |
|---|---|
| **Shields** | Excluded by decision. Nothing pays a shield to act — they are a damage-layer absorption pool, not an action cost. `ShieldRuntime` keeps them, with its own 4 derived families × omni + 6 elements |
| **`soul`** | **Player-scoped currency**, not an actor pool. `rpg_soul_balances` / `rpg_soul_ledger`, `SoulEarnPolicy`, demon binding, daily tribute, expeditions — all shipped. An actor's extinguished `spirit` is what the summoner harvests *as* soul: a conversion between two named resources at two scopes, not one resource wearing two hats |
| **`xp`** | Actor-scoped and persistent, but progression, not a spendable pool — `rpg_xp_ledger`, `rpg_actor_progression` |
| **Demon materials** | Player-scoped fusion inputs — `rpg_demon_materials` |
| **Lawn sun** | `pvz.*`, match-scoped. §4 |

---

## 13. Cost, stated honestly

Five resources is not five numbers. Each is a max channel, a regen channel, an accrual rule, a
serialization field, a UI element, a balance axis, and — once it appears in a battle report — a
**golden-visible number that moves `RulesetVersion`**.

Two mitigations, both already the plan: the registry is data (§5), and resource channels are their
own family list that never joins the 84 (§8).

---

## 14. Binding for the UI

The GUI binds to the **registry shape**, never to the id list:

```text
(id, label, value, max, polarity)
```

`label` is resolved from the actor's faction at the display layer (§3). A resource meter therefore
has no knowledge of which resources exist, and adding a sixth changes no component.

Design reference: [design/00-foundation.html](../design/00-foundation.html) §C.5 (resource meter),
[design/07-flows.html](../design/07-flows.html) and the actor panel in §D.2.

---

## 15. Open — genuinely undecided

1. **Whether `burden` is ever used.** The field exists and nothing uses it (§6).
2. **`resource.*` channel registration.** Designed, not registered — [actor-hub-ssot.md](actor-hub-ssot.md) §3.G.
