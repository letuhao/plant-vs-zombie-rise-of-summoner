# Base defense — the ideal

**Status:** idea phase, 2026-09-04. **Not a spec. No build authorized.** No module ids, no build
order, no acceptance criteria. This exists to be argued with and cut down before it becomes a
capability map.

**Deliberately the simple version.** The owner will enrich it. Where a section is thin, it is thin on
purpose — the inventory in §2 is the part that is meant to be complete, because it is what stops the
enrichment rounds from designing against a system nobody read.

**Related, and read this session:** [world-graph-ideal.md](world-graph-ideal.md) §10 (the vision this
implements) · [world-map-program.md](world-map-program.md) (`bases-and-defense`, wave 5) ·
[action-map.md](action-map.md) (`A10 battle-board`) ·
[world/spec-sector-development.md](world/spec-sector-development.md) (the `DevelopmentLevel` producer)
· [research/genre-mechanics/05-tower-defense-genre.md](../research/genre-mechanics/05-tower-defense-genre.md).

---

## 0. Owner decisions

**Round 1 — the shape (2026-09-04).**

1. **A base has one central defense area. Lose it and you lose the base.** Capture requires killing
   every troop standing in it. Troops there are legions — one or several.
2. **The central area is a _region inside a larger board_,** not the whole board. Buildings and
   obstacles live in the outer ground the attacker must cross; the central area is the objective.
3. **The board is the sector, zoomed in.** The sector's slots and what is built on them become
   objects on the board. The layout *is* sector state, so capture transfers it for free.
4. **Legion slots are even and paired** — the area supports N per side (2 v 2, 4 v 4). Each legion has
   a maximum member count, which **does not exist today** (§3.6) and is therefore free to choose.
5. **A field cap limits how many units stand on the board at once, identical for both sides.** It is a
   **flat authored integer per base tier**, and it is a **tunable** — never a `const`, never derived
   from the empty-cell count.
6. **Overflow enters in batch waves.** The field resolves, then the next batch of both sides enters
   together.
7. **This feature waits on nothing.** Every input it reads gets a meaningful default, and producers
   are wired when they land — `DevelopmentLevel` included, whose level-0 row is a complete playable
   base. See §5.10, including the one place this does not hold.
8. **The board is turn-based, on its own stage** — a fourth mode-profile row on the existing
   virtual-time kernel, not `#/battle` and not real-time. See §5.11.
9. **The stage is named `siege`** (`#/siege/{id}`). Player vocabulary, and it covers both seats in one
   word — you besiege a base, and a base under attack is besieged.
10. **Both sides move on the board.** Obstacles block movement and shape pathing; walls are real
    barriers. Nothing is built inside the central area — it is a pure arena; buildings, towers, walls
    and obstacles all live in the outer ground. See §5.12.

**Round 2 — buildings and obstacles (2026-09-04).** The defender's fantasy is **trench warfare**;
buildings and obstacles exist to serve it. See §5.13.

11. **Buildings and obstacles are a new kind of actor.** No level, never equipped, and they **receive
    nothing** — no aura, no buff, no debuff. They still carry **traits and actions**.
12. **They have no ownership.** Either side may destroy them or use them. Possession on the board is
    by **occupation**; ownership stays a world-layer fact settled by the outcome record.
13. **Either side may deploy them, both before the battle and during it.**
14. **In-battle deployment costs a unit's action** — so *build* is a third peer of move and attack —
    **and costs world-map building resources, never the six actor pools.**
15. **To use a building, garrison one unit in it.** An unoccupied building does nothing.

**Round 3 — building materials (2026-09-04).** See §5.14.

16. **Two building-material stocks are added** — a bulk material and a worked one — taking the empire
    economy from three stocks to five. This **re-runs P4 with defense construction included**, the
    category the original test excluded; it does not override it.
17. **The worked material is `ironwork`.** The bulk material's name is open — `rubble` is the
    recommendation. `stone` and `metal` are both refused: they collide with shipped content.
18. **Both are construction-only and world-scoped.** They never feed fusion, and they die with the map
    — which is what keeps loam's scope discipline intact.

**Round 4 — sector scale (2026-09-04).**

19. **A sector is planet-scale** — a full economy that can run itself, with **trade between sectors**
    ("like city trading or stellar trading"). This is a return to the design's own north star, not a
    departure: `world-graph-ideal.md`'s owner picks already said *"strategy in the **Endless Space**
    shape: sectors are the graph's nodes, each sector is a **board-level map holding constructible
    objects** — resource generators, buildings, defenses."*
    - **Inter-sector trade is sanctioned as _logistics_, not as a market.** `empire-economy-ssot.md`
      §6's table: a market (*"I need loam, I'll buy some"*) **destroys** the anchoring constraint;
      logistics (*"I have loam there and need it here"*) **preserves** it. Moving your own stock
      between your own sectors is the second. Lanes already carry length/width/hazard and
      `SupplyGraph` already computes connectivity, so the substrate exists.
    - **External trade with neutral clans is permitted for non-loam stocks** under P5 (lossy,
      rate-capped or gated), and `world-graph-ideal.md` §8.2 already has a *"Contract — pay their
      price"* path.
    - ⚠️ **This meets a revisit-trigger the code names by hand.** `RpgStore.World.cs:210-212`: *"the
      store replaces the graph rather than diffing it. **At six sectors** that is far cheaper than the
      bug surface a partial-update path would carry; **revisit if a world ever reaches hundreds**."*
      18 sectors × ~20 structures ≈ **360 slot rows rewritten on every turn commit.** A diffing
      writer stops being optional.
    - ⚠️ **Scope:** planet-scale economy plus inter-sector trade is `sector-development` plus an
      economy program — **not this one.** Base defense should *consume* it per §5.10's rule (read the
      input, default it, wire the producer later). Recorded here as provenance only.

**Round 5 — the open questions, closed (2026-09-04).** See §5.22.

20. **The siege board is the whole city.** Not a district around the Seat. Every economic building the
    sector holds is a board object, and — via decision 15 — a potential garrison point. A siege is an
    **urban assault**, and the trench-warfare frame sits *among* buildings rather than on open ground.
21. **A sector gains capacity by growing slots**, not by stacking depth per slot. `DevelopmentLevel`
    raises the sector's slot count.
22. **A construction stock at capacity halts production; nothing is wasted** — because a **deposit can
    be exhausted**, so discarding extracted material is a double loss. This requires a **player
    notification** telling them to build storage.
23. **Construction accumulates everywhere, boards included.** One mechanism, no board-local system.

**Round 6 — the last two (2026-09-04).** See §5.24.

24. **Batch waves cycle _within one engagement_**, not across map turns. A map turn resolves one
    engagement; inside it the field cap cycles batches until one side is spent or the objective falls.
    A siege spans turns because **engagements repeat**, not because waves do. The owner's reason is the
    rule the repo already holds: *"HOMM3 and other games have different turn for world map and each
    battle — explicit boundary, easier to define and code."*
25. **An unoccupied building is still physical** — it occupies its cell, blocks movement and fire, and
    has HP. It simply does not act. **And not every structure has a control point at all:** garrisoning
    means *taking control* — running production, working a weapon — and **a wall has nothing to
    control.**

**Round 7 — after the four-lens adversarial audit (2026-09-04).** See §5.24.

26. **⛔ Decision 20 is REVISED. The siege board is the district around the Seat, not the whole city.**
    The city lives on the map; a siege contests the works nearest the Seat. **Four independent audits
    converged on this** — economy (the defender's garrison starves on turn 1), engineering (a whole
    city needs a `world-generator`-scale layout generator), playability (~360–600 unit decisions per
    engagement), architecture (a fifth stage against a locked row). Outer city = economy, raidable
    separately; inner district = the board, with its outer ground and central area exactly as designed.
27. **⛔ Decision 14 is REVISED. There are FOUR ways a structure reaches the board, and only one of
    them costs world materials.** See §5.25. This is what lets the attacker overcome the material
    disadvantage the audit computed.
28. **The fifth stock stands on the refine chain** (§5.21 R2): **`ironwork` is _made_ from bulk
    material** at a lossy, gated rate. The exclusivity framing (§5.14) and the ratio framing (§5.15)
    are both retired — exactly one could stand, and this is it.
29. **The force-size numbers stay tunable**, not fixed as bands in §0. The playability audit's objection
    is accepted as a **recorded risk**, not overruled: board dimensions, decisions per engagement and
    viewport fit cannot be settled until the field cap exists, so the first balance pass owns them.
30. **`structure-seed` is its own program**, mirroring `demon-seed` beside `demon-system`. Ideal:
    [structure-seed-ideal.md](structure-seed-ideal.md).

**Round 8 — after the spec completeness audit (2026-09-04).** See
[base-defense/_completeness-audit.md](base-defense/_completeness-audit.md).

31. **⛔ §5.17 addendum 2's ⛔ is OVERRIDDEN. The siege AI reads cover when choosing where to stop.**
    The owner's call, against the recommendation, and recorded with the risk rather than quietly
    softened. Relic removed cover-seeking over five patches for unpredictability; the counter-argument
    accepted here is that an AI with no notion that a cell is dangerous walks into a kill zone every
    turn, which makes cover a mechanic the player must respect and the opponent does not.
    **The residual risk is the one Relic actually hit** — *"unpredictable behaviours"* — and it is
    mitigated by §5.20's own rule 1 (a total order with a documented tie-break) plus R6's decision
    trace, neither of which Relic's real-time pathfinder had. `siege-ai` keeps the risk term as
    specced.
32. **Structure HP is on `P(Θ)` reading the sector's `DevelopmentLevel`, multiplied by an authored
    MATERIAL TIER.** *"we will use llm to generate variant like stone wall, iron wall that iron wall
    have more defense than stone wall."* So a structure's magnitude has two factors: the **tier
    ordinal** the seed authors (identity — stone, iron, …) and `P(Θ_development)` (magnitude —
    deterministic). **This is seedsmith Law 2 stated as content**: the model picks *which material*,
    deterministic code turns that ordinal into a number. A model never picks the HP.
33. **`structure-seed` needs a DETERMINISTIC PLANNER stage before any model call.** *"pipeline
    generator need a deterministic planner (not LLM) to prepare what it should generate first."*
    The planner decides the generation plan — which kinds, which material tiers, how many of each,
    which slots they may sit on — and the model then writes identity into slots the planner already
    fixed. This is the seedsmith rule *"order the build so the model-free modules come first"*
    promoted from advice to a required stage, and it makes the tier ladder (stone < iron < …)
    a **planned** property rather than one that emerges from whatever the model happened to name.
    Belongs to `structure-seed`; recorded here because decision 32 depends on it.
34. **The bulk material is `rubble`.** `ironwork` is refined from it (decision 28).

**Round 9 — the spec round's seven open questions, cleared (2026-09-04).**

35. **⛔ COVER IS THE HoMM3 SHOOTING MODEL, not terrain cover.** This replaces §5.17's
    cover-as-dodge recommendation wholesale. Owner, verbatim:
    > *"obstacle need cover area, target in the area consider coverage · the obstacle can be target and
    > destroy · there will be two types of projectile: 1 will be penalty when fight through obstacle,
    > 2 will get no penalty · range attack have range penalty, if shooter is block by unit or obstacle,
    > the power will reduce · this mechanism is inspire of heroes of might and magic 3 shoot mechanism ·
    > we make it more by add targetable obstacle so shooter or some unit can destroy obstacle/building ·
    > this mechanism need to build both battle engine and action system"*

    Four mechanics, not one: a **cover area** projected by an obstacle; a **range penalty** on distance;
    an **obstruction penalty** when a unit or obstacle is in the line of fire; and a **projectile kind**
    that says whether a given shot pays them. Obstacles being targetable is the addition beyond HoMM3.
36. **Diagonal moves are legal and cost the same as orthogonal.** Chebyshev already means this; a
    surcharge would make a unit move in a Chebyshev circle at a Euclidean price.
37. **The defender's legions are player-placed pre-battle.** Decision 5's deployment step is the
    mechanism. The auto seat places by a deterministic AI policy — same step, different driver — so
    step 7's standalone gate does not wait on a UI.
38. **`rubble` and `ironwork` trade freely between sectors.** ⚠️ **This deliberately gives up material
    denial as a siege lever**, which round 5 had protected. Accepted with that stated: **the blockade's
    teeth are loam and board income**, not materials — `siege-supply`'s besieged sector is still its own
    supply component for loam, and a besieger still takes the board's income nodes. Decision 19's
    logistics framing wins on materials.
39. **An obstacle's cover area is an authored radius per obstacle kind.** The seed writes the kind; a
    tunable writes the cells.
40. **`board-render` serves BOTH `battle` and `siege`.** This retires `railState.ts:31`'s
    declared-but-unbuilt `battle` id rather than adding a second one beside it — which removes the
    third cost the `decisions.md` fifth-stage amendment named.
41. **⛔ THE ENGINE SUPPORTS PAUSE.** *"pause the game, this is single play game, the engine should be
    support pause."* A closed client pauses; it does not auto-resolve and does not forfeit.
    **This is a wiring gap, not a new capability** — `BattleSessionState.Disconnected` already means
    *"the session is preserved and resumable — its trace is intact, it simply has nobody to ask right
    now"*, and `BattleSessionRegistry.Resume` ships at `:119`. What is missing is a **deliberate**
    `Paused` state distinct from a dropped connection, a world turn that does not advance while one is
    held, and a timeout that does not fire on it.
42. **A besieged garrison's top-up is rationed** — a reduced draw rate, so a stockpile lasts longer
    under siege than in peacetime.

**Round 10 — the last three, and two of them change scope (2026-09-04).**

43. **PvZ's static plants STAY demons.** Wall-nut, Tall-nut, Pumpkin, Spikeweed and Lily Pad are not
    reclassified. This confirms what `structure-seed-ideal.md` §3 already argued from the owner's own
    earlier words (*"cannot use soul to summon a wall, that confuse with wallnut demon family"*):
    reclassifying them *"would take content out of the summon roster."*

    > ⚠️ **The question's own framing was wrong and the correction matters.** It offered "stay demons"
    > as implying a **datamine-classify** pipeline. §3 says the opposite, explicitly: *"the PvZ corpus
    > is **not** available for reuse here … **So the source material is the design research, not a
    > datamine**"* — §5.18's four obstacle kinds plus §5.21's ten economic roles, **~25–30 seed concepts
    > authored by hand first and generated second.** Staying demons means an **INVENTION** pipeline,
    > which has a different failure surface (mode collapse and generic flavour, neither caught by
    > majority vote). The decision stands; the reasoning attached to it does not.

44. **`#/battle` is built here, as a thin stage on `board-render`.** Decision 40 said the layer serves
    both; this says the dead id is retired **by using it**, not by deleting it. **Thin is a constraint,
    not an aspiration:** `#/battle` renders a **resolved `BattleReport` in playback** on the generic
    board layer. It invents no battle requirements — the battle already resolves today and produces a
    report; the stage shows one. Anything beyond playback needs `battle`'s own spec.
45. **⛔ DECISION 30 IS REVISED. `structure-seed` folds into `base-defense` as modules**, rather than
    standing as its own program beside it. One map, one plan, one todo pair. Its ideal
    ([structure-seed-ideal.md](structure-seed-ideal.md)) stays as the design record and is **not**
    superseded — only its program boundary is.

**Round 11 — the pause mechanism, corrected by following the prior art (2026-09-05).**

46. **⛔ DECISION 41'S MECHANISM IS REVISED. A paused siege is a PERSISTED DECISION LOG, not a session
    held in memory.** Its intent is unchanged — a closed client pauses, does not auto-resolve and does
    not forfeit.

    **The owner's question was the right one:** *"we won't store battle state? maybe it correct in
    heroes of might and magic and other game? they have reason for it, maybe we should follow."*

    **They do have a reason, and it is not squeamishness about memory.** HoMM3 and Total War refuse a
    mid-battle save because a battle is **re-derivable from its inputs** — so it never needs storing.
    Games that *do* store tactical state (XCOM, Fire Emblem, AoW4) are ones where the tactical layer
    **is** the game. Ours is not: the map is the game and §2 rule 7 already says so.

    So the correct shape is neither *"hold the board in memory"* nor *"lose the siege"*:

    ```text
    (setup, seed, DECISION TRACE)  →  replay  →  the exact board
    ```

    **The machinery is already built and inert:**

    | Piece | State |
    |---|---|
    | `DecisionTrace` — `(Tick, ActorKey, ActionId, TargetKey, Source)`, ordered by `(Tick, Seq)`, with a replay cursor | **Built** |
    | `InteractiveIntentSource`'s **replay constructor** — *"read the trace, never the player … a completed trace reproduces its battle byte-identically"* | **Built** |
    | `decisions_json` column — `RpgStore.cs:603`, read at `RpgStore.WebMatches.cs:180` | **Built, and READ** |
    | A **writer** for it | ⚠️ **Missing** — §3.7 already recorded it: *"`DecisionsJson` is read and never written … a column, a reader and a guard with no producer"* |

    And `DecisionTrace`'s own comment names this exact case as the hole it exists to cover:
    *"Appended per decision, never written at the end. A trace produced only on completion is worthless
    for the failure it exists to cover — **a disconnect mid-battle** … That is the hole T6 must not
    ship without."*

    **What this buys over the in-memory pause, on every axis:**

    - **No battle state is stored** — a decision log is *input*, not state. §2 rule 7 is satisfied
      **unconditionally**; the *"a pause must never survive a turn boundary"* clause pass 4 had to
      invent is no longer needed, because there is no battle in memory to span anything.
    - It **is** §2 rule 8's own save model: *"A save is `(seed, template, command log)` and replay must
      be byte-identical."*
    - **It survives a server restart**, which the in-memory version explicitly could not.
    - It closes a documented wiring gap instead of adding a mechanism.

    **Scope:** the `decisions_json` **writer belongs to `spec-interactive-turns.md` (T10)**, per audit
    F4 — *"consume T6/T10/T11, never re-derive."* This program **consumes** it and names it a
    prerequisite.

**No open questions remain.**

**Consequence worth stating, because two of these answers combine:** with the central area as a
*region* and the cap as a *flat integer*, **towers and troops never compete — not for space, and not
for budget.** Defenses occupy the outer ground; legions occupy the central area; the deployment cap is
authored independently of both. The degenerate "wall off the board to starve the attacker" strategy
in §5.9 cannot arise, and Dungeon Defenders' two-budget separation (§4.1) is satisfied structurally
rather than by tuning.

**Still open:** §8.

---

## 1. What this is

When a hostile force marches into a sector you hold, the fight happens on a small **2D grid**: lanes
running across, your defenses standing on cells, the attackers walking in from the edge they marched
in from. You arrange that layout once, on the strategy map, and it fights whenever someone arrives —
with you at the board, or without you.

The same board, seen from the other side, is how you take someone else's base. **There is one board
model and two seats at it**, not a defense mode and a separate assault mode.

Three tiers, from [world-graph-ideal.md:451-452](world-graph-ideal.md):

| Base | Where it sits | Board |
|---|---|---|
| **Homeworld** | Dave's timeline | a *region* — front lawn, backyard/pool, roof, night garden, each its own board, unlocking across the campaign |
| **Stronghold** | a sector's Seat slot | 5 lanes |
| **Outpost** | any wildland slot | 3 short lanes |

---

## 2. Load-bearing principles — restated, not linked

A downstream session reads this document, not its links. These constrain the choices in §4 and each
one is stated here in full for that reason.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** A base
   defense board is an RPG-layer surface. "Can the lawn represent a base grid" is not a question this
   feature asks — a web-mode board has no Unity at all. The server's battle engine owns its state
   outright ([software-architecture.md](software-architecture.md) §3 scope note). The narrow Unity
   write surface constrains *persistent vanilla stat changes* and nothing here.

2. **Standalone-first / gameless-first.** Every RPG feature must be fully playable and CI-provable
   with the PvZ game closed. The injector may *enrich* a feature, never *gate* one. This board is
   web-mode; PvZ is not on its critical path in any form.

3. **Server-authoritative.** Web-mode outcomes resolve server-side from a recorded seed. The FE
   renders and commands; it never rolls. No client prediction of the living set (the lawn projector's
   RT-15, rejected there and rejected here).

4. **One power ladder — and it has two reads.** `Θ` is the single power index;
   `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`. **Contests read `Θ` linearly; magnitudes read `P(Θ)`.** Tower
   damage, wall HP and wave HP are magnitudes. Anything decided by comparing two numbers — does this
   hit, does this status stick — is a contest and stays linear, because a geometric curve makes a
   fixed level gap unboundedly decisive. **Writing a new `f(developmentLevel)` anywhere in this
   feature is the defect the ladder exists to end.**

5. **The balance surface is data.** Any number a balance pass would touch lives in
   `data/tuning/<domain>.v{n}.json`, never a `const`. Grid dimensions per tier, slots per development
   level, wave scaling, structure HP — all config.

6. **No hard progression ceilings — but a board cap is not a progression ceiling.** Endless grind is
   the SSOT other systems reconcile to. **The relevant exemption is already written down**:
   [ssot-power-scale.md](power/ssot-power-scale.md) §11.3, *"Runtime and board caps — perf
   protection, not progression … These bound how much can exist **at one moment**, not how far you
   can get."* `CapPolicyConfig.MaxLivingPlants = 50` is the precedent, verdict *"Bounds simultaneous
   entities, not lifetime progress."* A grid dimension and a concurrent-placement budget sit in that
   row. A ceiling on tower *power* would not, and must stay uncapped.

7. **The world/combat seam, in four rules** ([world-graph-ideal.md:207-213](world-graph-ideal.md)):
   - **Combat is stateless between turns.** A multi-turn siege is a *fresh engagement each turn,
     built from world-held state* — never a battle paused in memory. Wall damage, wounds and a
     depleted garrison come back in the outcome and are stored by the **world**.
   - **Combat never writes world state.** It reports; the world decides consequences.
   - **The world never reads combat internals.** No round counts in any map-side formula. *"A map
     step is always a **turn**, a battle step is always a **round**. Never convert between them."*
   - **Outcomes are records, not dependencies** — which is exactly what makes a hand-played board
     legal, because world replay reuses the stored record rather than re-simulating.
     > ⛔ **Verified 2026-09-04, and the code does not do this on one path.**
     > `RpgStore.WorldTurns.cs:599-606` — `GetWorldTurnReport`, used when a report body has been
     > trimmed — rebuilds the world from its template and **re-runs `TurnEngine.Step` for every turn
     > from zero**, passing **no resolver**, so it re-simulates every battle with
     > `PlaceholderBattleResolver`. The *world state* itself is persisted and is fine; the **report**
     > is re-derived, and for any resolver but the placeholder it would come back inconsistent with
     > what actually happened. **Consequence for this program: a siege resolver must be supplied at
     > BOTH `RpgStore.WorldTurns.cs:509` and `:603`.** Wiring only `:509` is a one-line bug with a
     > campaign-sized blast radius. The battle side has the same shape at `WebMatchService.cs:104`
     > and `:146`, which re-resolve from `(setup, seed)` rather than reading a stored report — and
     > that is why `DecisionTrace` exists.

8. **World determinism.** Integer/fixed-point only in game-affecting branches, stable ordering by
   entity id (never dictionary enumeration), seeded per-system streams, **no wall-clock read anywhere
   inside `step`**, every resolution stamped `(engineVersion, rulesetVersion, seed)`. A save is
   `(seed, template, command log)` and replay must be byte-identical.

9. **Magnitudes are `long`.** The ladder is quadratic; `float` stops being integer-exact at Θ=232 and
   per-mille `int` at Θ=3,213, both inside normal play. Widen before multiplying, divide by 1000 last
   exactly once, let overflow throw.

10. **Closed vocabularies — do not start a third.** The action layer already owns a grid vocabulary
    (`GridPos`, Chebyshev distance, four area shapes, `ChosenCell` anchoring). Inventing a second
    grid model beside it is the exact defect the atom program exists to stop.

---

## 3. What already exists — the three buckets

Surveyed 2026-09-04 across `src/FusionRpg.Core/World`, `src/FusionRpg.Core/Battle`,
`src/FusionRpg.Core/Actions`, `src/FusionRpg.Data`, `src/FusionRpg.Server`, and
`web/fusion-rpg-web`.

### 3.1 The headline

**The grid is built and the battle resolver is deliberately blind to it. The *tower* is the thing
that genuinely does not exist.**

Three sentences, because the split is not where a first reading puts it:

1. **Positional targeting is fully built** — `TargetResolver` resolves `Row`/`Column`/`Square`/
   `Rectangle` areas over a 10×5 grid on the overlay route (`TargetResolver.cs:163-215`,
   `CombatPolicy.cs:51-52`). The grid vocabulary is not a new capability.
2. **Battle bypasses all of it**, at three named lines (§3.3). *"The battle engine is squad-vs-wave"*
   is true as a description of today and false as a description of the architecture.
3. **A non-acting destructible combatant — which is what a tower and a wall are — has no mechanism
   anywhere**: not in the setup, not in the round loop, not in the report. That is the real gap, and
   it is the one that shapes the design (§3.4).

### 3.2 Built

| Thing | Evidence |
|---|---|
| **Grid math** | `Actions/GridDistance.cs` — `Chebyshev`, `InRange`, `Square(center, size)`. `GridPos(int Row, int Col)` at `:5`, documented as *"Independent of any one board representation"* |
| **Grid targeting vocabulary** | `ActionAreaShape` = `Row`/`Column`/`Square`/`Rectangle` (`ActionTargetSpec.cs:132-151`); `ActionAnchorSource.ChosenCell` (`:58`); absolute cell filters `Row`/`ColMin`/`ColMax` (`ActionTargetFilters.cs:17-20`), kept *"because content may legitimately want 'the front column'"* |
| **The board seam on the AI/read side** | `IBattleView.PositionOf(actorKey)` (`IBattleView.cs:31`), with the absence convention documented: *"`null` when no board exists yet — the SAME sentinel `UsabilityEvaluator`'s own `casterPos`/`targetPos` already use"* |
| **Positional area targeting, end to end** | `Combat/TargetResolver.cs:54-61` (`TargetModes.Area` → `TryAnchor` → `EnumerateCells` → `pool.Where(e => cells.Contains((e.Col, e.Row)))`), `:163-215` (Row/Column/Square/Rectangle with Center/Corner anchors), `:100-124` (row/col filters, exact or min/max). Bounds at `CombatPolicy.cs:51-52`. **This is a working positional targeting engine — on the overlay route** |
| **Range fields on a compiled action** | `CompiledAction` already carries `MinRange`, `MaxRange`, `RangeChannel`, `RequiresLineOfSight`; `UsabilityEvaluator` already takes `GridPos? casterPos, GridPos? targetPos` (`UsabilityEvaluator.cs:25-26`) |
| **Determinism rig** | xoshiro256**/splitmix64, `RngAlgoVersion = 1`, FNV-named per-system streams (`initiative`, `crit`, `essence`, `status`; `proc` and `damage` reserved) — `SeededRng.cs:9-86`, `BattleRunState.cs:123-128`. Seed persisted with the setup so replay re-resolves identically (`WebMatchService.cs:52, 63-66`) |
| **Golden discipline** | 4 locked hashes + a 32-seed sweep (`BattleGoldenTests.cs:57-60, 180-207`), 14 trace fixtures, 4 downstream expedition hashes. Two worked precedents for adding a report field without a re-bless — `ContentHash` and `Warnings`, both `[JsonIgnore(WhenWritingDefault)]` and blanked in `Hash` (`BattleGoldenTests.cs:144-149`) |
| **A side-wide channel modifier** | `ActiveCommanderAura(CommanderSide, TargetChannel, Value, SourceId)` (`BattleModels.cs:192`), delivered to every same-side actor's derived channel at `BattleRunState.cs:250-258`. The right-shaped seam for a totem or a climate aura — see §3.3 for why it is inert |
| **The world↔combat seam** | `BattleSeam.cs` — `BattleRequest` (kinds `sector`/`lane`/`guard`, `DefenderStationary`, `GuardWaveId`, `SlotIndex`), `BattleOutcome`, `IBattleResolver` (`:89`). Clean, data-only, versioned |
| **A `Sieges` turn phase** | `World/Turn/SiegePhase.cs`, running every turn in the locked phase order. Today it resolves a `Clear` order against a slot's guard |
| **Slot structures** | `WorldSlot.StructureId` + `ConstructionTurnsRemaining`, written for real at `BuildResolver.cs:124`, decremented at `LoamPhases.cs:83` |
| **Where the attacker came from** | `WorldEntity.OnLaneId`, `OnLaneTowardSectorId`, `LaneProgressMilli` (`WorldState.cs:232-245`) — the graph already knows which edge a legion is arriving on |
| **Additive persistence pattern** | `RpgStore.World.cs:134-155` — `EnsureColumn` migrations, *"an existing database never re-runs CREATE TABLE"* |
| **A golden-safe way to add hashed state** | `WorldCanonical.cs:90-94`, the `faction-scope` precedent: emit a **separate conditional row** only when non-default. Its own comment records why — appending to the existing row *"moved every prior hash for a value that did not actually change"* |
| **FE camera** | `stages/world/camera.ts` — *"The whole navigation model as pure data … No DOM anywhere in this file."* `zoomAbout`, `panBy`, `fitToExtent`. Zero domain coupling |
| **FE shell for a new stage** | Two mandatory files: the stage component (`useStageMountGuard` + `StageHost`) and a route in `app/routes.tsx`. Layer stack, `PanelShell`/`DialogShell`/`DockShell`, toasts, keymap all inherited free |
| **FE test rig** | Vitest + Testing Library + jsdom, Playwright with a `live-chromium` project, shared server fixtures generated from real server calls (`src/test/mocks.ts:5-9`) |

### 3.3 Wiring gap — inert, **not** an architectural wall

Each row names the specific line that is switched off.

| Gap | The inert line | Why it matters here |
|---|---|---|
| **The battle has no positions** | `Battle/BattleRunState.cs:377` — `public GridPos? PositionOf(string actorKey) => null;` and `:386` — `Row: 0, Col: 0`. The comment at `:363` says it outright: *"PositionOf is always null (no board exists…)"* | This is `A10`. The engine's view seam already *has* the method; it returns null, which by `GridDistance.InRange`'s own contract means "no board — every range check passes" |
| **Area actions are refused for lack of a board** | `RpgStore.ActionCatalog.cs:62` — `boardAvailable: false`, the **one production call site**, with `:26` explaining *"battle is squad-vs-wave, not cell-based; `A10` (a real board) has not landed"*. Default param at `RpgStore.Actions.cs:142` | One hardcoded `false` gates every `Area`-mode action in the corpus |
| **Battle's effect bag has an empty board** | `EffectBag.BoardSnapshot` defaults to `BoardSnapshot.Empty` (`EffectBag.cs:162`), and `BattleEffectHost` (`BattleEffects.cs:38-60`) wires `ShieldGate`, `Status`, `StatusRng`, `Ledger` — **and never assigns `BoardSnapshot`.** The overlay hosts do (`SimEffectHost.cs:138-145`) | **Every `Area`-mode atom in a battle silently resolves to zero targets.** The positional engine of §3.2 is reachable from battle the moment this one property is set |
| **Status contagion cannot spread in battle** | `BattleEngine.cs:275` — `state.Status.Tick(now, state.PulseSink, board: null, spreadRng: state.StatusRng);` | `board: null` disables `StatusSpread.RowNeighbors` (`StatusSpread.cs:77-88`), which filters on `e.Row == self.Row` |
| **⭐ The second seat has a seam and no caller** | `BattleEngine.cs:172-175` — `Resolve` takes an **eighth** optional parameter, `Timeline.IIntentSource? intentSource = null`, threaded through `RunBasicAttackStep` to `BasicAttack.cs:85`. **All three production call sites omit it** — `WebMatchService.cs:104`, `:146`, `:251`. `IntentSource.cs:20-22` documents it as *"the AI-policy seam the auto-resolved modes need, **and the player-input seam an interactive mode needs**"* | **This is the answer to "who plays the other side", and it is three missing arguments.** ⚠️ Added 2026-09-04 — an earlier survey read the signature at `:169-171`, which is only its first three lines, and this row was missing from the first draft of this document. One caveat: it is **one** source for the whole battle (`BasicAttack.cs:85` calls it for every actor regardless of side), so two seats need a dispatching wrapper that routes on `SideOf` — no signature change, no golden movement |
| **A played battle already has its recorder** | `InteractiveIntentSource.cs:30-91` records every declaration including timeouts, and replays from the trace without asking (`:85-91`). `DecisionTrace.cs:38-55` orders by `(Tick, Seq)`. The column exists: `RpgStore.cs:603` `EnsureColumn(…, "decisions_json", "TEXT")` | **`DecisionsJson` is read and never written** — no writer anywhere in `src/`. The played-battle persistence half is a column, a reader and a guard with no producer |
| **No shipped profile is interactive** | `RequiresLiveInput` is `false` on all three catalog rows; `WebMatchService.cs:197` says it plainly — *"Inert today: no shipped profile sets RequiresLiveInput, so no match reaches this branch"* | A siege is §5.11's fourth profile row **and the first `RequiresLiveInput: true` row** |
| **`Resolve`'s mode profile is accepted and ignored** | `BattleEngine.cs:170` — `BattleModeProfile? profile = null`, **never referenced in the method body**. Its own doc at `:143-151`: *"accepted and available for future enrichment but are inert here"*, and *"`NextEventAdvance` is used regardless of which profile is passed"* | Board-relevant only as a caution: the profile that would drive a real-time board is not consulted yet |
| **The whole turn FSM is uninstantiated** | `ActorTurnMachine`, `ActionRunner`, `ActionSlots`, `TurnEconomy`, `TriggerPhase`, `RendezvousLane`, `ReadinessDriver` — 21 test files, **zero constructions in `src/`**. `ReactionLane.cs:60` gates on `wReact > 0` and every shipped profile is `wReact: 0` | If the board wants per-actor turns rather than the round loop, this is built and waiting, not missing |
| **The commander aura has no producer** | `BattleModels.cs:185` defaults to `Array.Empty<ActiveCommanderAura>()`; `:182-184` says *"Empty for every existing caller… populated once a real caller (T13+) resolves an active aura's magnitude"* | The natural carrier for a board totem or a sector climate is built, side-scoped, and unfed |
| **A wave can name a mode profile that nothing resolves** | `WaveCatalog.cs:21` declares `string? Profile = null`; **no production code calls `BattleModeProfileCatalog.Resolve`**, and all four shipped waves leave it null | |
| **The world's battle resolver is never supplied** | `TurnEngine.Step(world, commands, seed, IBattleResolver? resolver = null)` (`TurnEngine.cs:78-79`), `var battles = resolver ?? PlaceholderBattleResolver.Instance;` (`:83`). **Both production call sites pass no resolver** — `RpgStore.WorldTurns.cs:509` and `:603` | A real base-defense resolver drops in **here**, without touching the world module. A missing argument at every call site, exactly |
| **A "this is a fortress" marker exists and is read by nobody** | `SectorTypeCatalog.cs:15` — `Fortress = 1 << 4`. Verified: **one reference in the entire repo, its own declaration.** Set on no catalog row, read nowhere | The most on-point inert line for this feature |
| **Authored defenders per slot are ignored** | `WorldSlot.GuardWaveId` (`WorldState.cs:101`) is authored, persisted, hashed and wire-projected, read into a `BattleRequest` at `SiegePhase.cs:67` — and `PlaceholderBattleResolver.ResolveGuard` (`:44-66`) **never reads it** | The closest thing to "authored defenders on a sector" already rides the wire |
| **`DevelopmentLevel` is a cost with no producer** | `WorldState.cs:135`. Five reads (hash, upkeep, two intel projections, persist), **zero originating writes** — every "write" is a copy. `TurnEngine.Growth` (`:203-207`) is `return world;`, a literal no-op | The obvious board dial is structurally always `0` in production |
| **The FE map camera cannot be driven** | `stages/world/cameraGestures.ts:13` — `wheelZoom` etc., importer: **its own test only** | Built, tested, mounted by nothing |
| **FE board-placement targeting** | The whole `stages/world/targeting/` directory is inert — `targetingState.ts:40` `targetingReducer`, imported only by its test. Verbs already include `"build"`; overlay kinds already include `"placement"` | The closest existing analogue to placing a defense on a cell |
| **A `battle` stage id is declared with nothing behind it** | `shell/railState.ts:31` — `currentStageId: "sanctum" \| "world" \| "lawn" \| "battle"` | The fourth stage is named in the shell and in `information-architecture.md` §2.4, and does not exist |
| **`bind-warden` is unreachable over the general command route** | `WorldCommandRequest` (`WorldDtos.cs:419-438`) has no `WardenId`; the submit mapping (`WorldEndpoints.cs:119-120`) never sets it. It works only via its own dedicated endpoint | Named because a new order kind must pass five plumbing sites, and one shipped kind already fails two of them |

### 3.4 Real gap — no mechanism anywhere

> **The one that shapes the design.** A tower is a thing with HP that stands on a cell, cannot walk,
> and whose destruction is an objective rather than a casualty. **Battle has no such category, and
> three separate rules actively reject one.** Adding a structure as an ordinary `BattleActorSetup`
> would (a) force it to attack — `BasicAttack.cs:76`, an actor with no legal intent returns
> `AttackStepOutcome.Break` and **breaks the entire round**, not just its own turn; (b) be handed a
> basic attack anyway — `BattleRunState.cs:280-283` forces `held = new[] { BasicAttackCompiled };`
> for an actor with no loadout, *"never `ActionIntent.None` by construction"*; and (c) **keep the
> battle alive by existing** — `AnyActive` is purely `a.Active && a.Setup.Side == side`
> (`BattleEngine.cs:405-406`), so an undestroyed wall means the attacker has not won.

| Gap | What would have to be built |
|---|---|
| **A roster that can change mid-battle** | `Actors` is materialised once at `BattleRunState.cs:132-134` and there is no add path — `Actors.Add`/`Actors.Insert` have zero hits in `src/`. Blocks reinforcement waves (§5.9) **and** anything summoned. One build serves both |
| **A non-acting destructible combatant** | See the box above. Needs: a combatant-kind discriminator on `BattleActorSetup`; an opt-out from the initiative list that does not trip `AttackStepOutcome.Break`; a targeting relation that can name structures; and a win/loss predicate distinguishing *"all garrison dead"* from *"core destroyed"* |
| **An outcome that is not a wipe** | `BattleOutcome` has exactly three members — `Victory` (wave wiped), `Defeat` (squad wiped), `Stalemate` (`MaxRounds`) — computed at `BattleEngine.cs:361-363` as nothing but those two `AnyActive` checks. A tower defense needs *breached*, *held*, *objective razed*, and a leak count |
| **A report that can name a structure or a cell** | Neither `BattleEventRec(Round, Kind, ActorKey, TypeId, Side, Amount, Element, ShieldId)` (`BattleModels.cs:205-207`) nor `BattleActorResult` (`:224-227`) carries a row/col or a kind discriminator. Event kinds are `spawn`/`die`/`shield.*` only. **`BattleReportEmitter`** additionally maps everything onto plant/zombie envelopes keyed purely on `actor.Side == "squad"` (`:39-40`) — a third combatant class has no envelope |
| **"Act on a cell"** | `ActionIntent(string ActionId, string? TargetKey, ActionEnvelope Envelope)` (`IntentSource.cs:12`) — `TargetKey` is an actor key. No cell field, no movement event kind, no occupancy rule |
| **A 2D defense board in the world layer** | Nothing, anywhere. `Grep '(?i)\b(board\|grid\|tile\|lawn)\b'` over `src/FusionRpg.Core/World` returns **zero matches**. `WorldSlot.SlotIndex` is an ordinal, not a coordinate; `WorldSector.LayoutX/LayoutY` are strategy-graph node positions, one pair per sector |
| **A base as a first-class thing** | "Stronghold" and "outpost" appear only as doc comments (`SlotTypeCatalog.cs:9`, `:23`) and as two ordinary sector *ids* of `TypeId = "rich"`. `SlotKind.Seat` has a catalog row and a validation rule but **nothing that defends can be built on it** — the only Seat-requiring structure is `waystation`, a loam source |
| **A defensive structure kind** | `StructureKind` has exactly two values, `LoamSource` and `Storage` (`StructureCatalog.cs:7-13`). The four rows are well, waystation, granary, placeholder |
| **A garrison** | Not an entity, not a state, not a flag. "Garrison" is (a) an upkeep headcount — `LoamUpkeep.cs:50-52` sums **any** entity whose `AtSectorId` matches, *regardless of owner or stance*, so an enemy warband parked in your sector raises **your** upkeep; and (b) an AI heuristic. Fortification in the entire codebase is one flat per-mille multiplier: `PlaceholderBattleResolver.cs:79-83`, `entrenched → DefenderBonusMilli` |
| **A generic FE board layer** | The Phaser island is lawn-shaped throughout: `LawnWorldScene` hardcodes its scene key and floor; `LayoutGridSystem` branches on `"plant"`/`"zombie"`/`"mower"`/`"pet"`; `SyncFromModelSystem` hardcodes PvZ colours and a PvZ status-chip palette; `stackLayout.ts:3` types side as `"plant" \| "zombie"`; `createLawnGame.ts:27` has a fixed scene list. Reusable as-is: `FxPool`, the `EventBus` generation pattern, and `gridMath` **if** its four module constants (`CELL_W/H`, `ORIGIN_X/Y`) become arguments |
| **A DOM/SVG cell grid** | The lawn's Phaser canvas is the only cell grid in the FE. If the board is to be SVG (to reuse `camera.ts` and `targeting/`), cell rendering, cell picking and cell→screen math all have to be built |
| **A camera bridge** | Nothing lets a Phaser scene share the FE `Camera` model, and nothing renders `targeting/` overlays over a Phaser canvas |
| **A shared Phaser test harness** | No `createTestScene()`, no shared mock GameObject factory. Every Phaser-touching unit test rebuilds its own mocks; anything needing a real `Game` is provable only in Playwright |

### 3.5 Waves — built, but the composition is code, not data

A wave today is `WaveDef(WaveId, Name, ContentIndex, IReadOnlyList<BattleActorSetup> Enemies, Profile)`
(`WaveCatalog.cs:21`): **a flat ordered list of enemies plus a `Θ`.** No formation, no spawn
positions, no arrival timing, no sub-waves. Four rows exist, hand-written at `WaveCatalog.cs:55-61`
as `(rarity band, count)` tuples.

Where the numbers live splits cleanly, and one half is on the wrong side of the tunables rule:

| Number | Home |
|---|---|
| Which species, how many, which bands; the `Θ` literals `1/3/6/10` | **Code const** — `WaveCatalog.cs:55-61` |
| HP / Atk / Defense at that `Θ` | **Data** — `data/tuning/power-scale.v2.json` |
| Round length, max rounds; trait magnitudes | **Data** — `data/tuning/battle.v{1,2}.json` |
| Accuracy/dodge/crit baselines | **Code const** — `BattleModels.cs:165-168`, e.g. `BaseAccuracy(theta) => 220 + 26 * theta` |

**There is no wave/encounter data file among the 58 in `data/tuning/`.** Wave composition is exactly
the kind of number a balance pass changes constantly, so a base-defense feature that authors waves
inherits a tunables gap it should fix rather than extend.

### 3.6 Legion capacity — asked directly, answered directly

**There is no legion size limit, and no species/type slot limit, anywhere.**

`WorldEntity.Members` is a bare `IReadOnlyList<WorldEntityMember>` (`WorldState.cs:256`) with no
bound. `WorldValidation` runs fourteen rules (`WorldValidation.cs:25-38`) and **none of them counts
members.** Every shipped legion is small only because a template author typed it that way — Dave's
starting legion is three members, Zomboss's two, the wild pack two
(`WorldTemplateCatalog.cs:192-197, 212-216, 222-226`).

**Two squad caps do exist elsewhere, and they are the precedent and the anti-precedent:**

| Cap | Where | Verdict |
|---|---|---|
| **Expedition squad slots — 2 / 3 / 4 / 5 by tier** | `data/tuning/expeditions.v1.json` `tiers.*.squadSlots`, loaded through `ExpeditionTuning.cs:51`, enforced at `ExpeditionResolver.cs:66` (`"Squad exceeds tier slots."`) | ✅ **The pattern to copy.** A slot count that scales with content tier and lives entirely in data |
| **Web battle squad — 6** | `WebMatchService.cs:238` — `const int maxSquad = 6;`, refusing with `"squad.toolarge"` at `:240` | ❌ **A magic number on the balance surface**, in the one production battle path. It is exactly the number a balance pass would change, and it is a `const`. Fixing it belongs to whoever next touches that file |

**One modelling fact that decides how "slots" can work here:** `WorldEntityMember` is **one demon,
not a stack** — it carries `InstanceId` (a roster specimen), `Level`, `Hp`, `Wounds`
(`WorldState.cs:212-220`). HOMM3's seven slots hold *stacks* of one creature type each (up to 9,999).
Ours cannot: a stack has no single `InstanceId`, no single level, and no place to put gear. So in this
codebase **a slot is one specimen**, and "max demon types per legion" and "max legion size" are the
same number unless stacks are introduced — which would be a change to the unique-actor grain, not a
tuning value.

### 3.7 Three corrections for downstream sessions

- **`BattleEnvironment` is not a game environment.** Despite the name, `BattleModels.cs:257-270` is a
  **platform stamp** — architecture / OS / .NET major — used to refuse cross-platform replay. There is
  no sector-climate-applied-to-a-battle mechanism today. Do not cite it as one.
- **The lawn grid is 5×12, not 5×9**, floored at those defaults and capped at 8×16
  (`lawnViewModel.ts:158-160`, `lawnGridExtent.ts:6-7`). The `5, 9` literals exist only in one test
  file. Any "the lawn is 5×9" claim in older prose is stale. (The overlay *combat* bounds are a third
  number again — `CombatPolicy.cs:51-52`, `LastCol = 9` / `LastRow = 4`, i.e. 10×5.)
- **`BattleEngine`'s `SideIndex` is not geometry.** `ActorState.SideIndex` (`BattleEngine.cs:50`) and
  the `Math.Abs(a.SideIndex - around.SideIndex) != 1` check at `:408-421` look like adjacency on a
  line. They are an index into the **authored setup order** (`BattleRunState.cs:132-134`), used only
  by the `guardian`/`loyal` traits. Do not build a formation on them.

---

## 4. Prior art

From [05-tower-defense-genre.md](../research/genre-mechanics/05-tower-defense-genre.md) (in-repo,
2026-09-02, primary-source-graded) plus a targeted search on **base-builders**, which that file does
not cover and which is exactly where the "board size" question lives.

### 4.1 The finding that decides §5: shipped games do not grow the board

| Game | Board | What grows with level | Source grade |
|---|---|---|---|
| **Clash of Clans** | **Fixed 44×44 = 1,936 tiles, unchanged across all 18 Town Hall levels** | The **building count**: TH1 13 → TH2 17 → TH3 27 → TH5 45 → TH7 70 → TH8 88 → TH11 119 | Second-tier (Fandom wiki) — flagged |
| **Arknights** | Authored per stage (`mapData.width`/`height` varies); stage 1-7 is 11×7 = 77 tiles, **39 buildable** | Nothing about the board. The binding constraint is `characterLimit` = **8 concurrent deployments**, *"a single integer in a config file"* | First-tier (shipped level files, computed) |
| **Dungeon Defenders** | Per level | Two orthogonal budgets — fungible mana **and** a non-fungible per-level "Defense Units" allowance | Wikipedia; the DU numbers themselves are a recorded research gap |

**Both shipped patterns make the *placement budget* the scarce thing and leave the board alone.**
That is the single most transferable finding, and §5 is built on it.

### 4.2 Placement scarcity is the difficulty dial

> *"Because it is the only lever that scales difficulty without scaling numbers. Every other dial
> (enemy HP, enemy count, enemy speed) makes the same defence weaker; placement scarcity makes a
> **different defence necessary**."*

Evidence: Kingdom Rush's Heroic and Iron Challenges are *pure constraint* — one life, limited upgrade
paths, *"certain towers are locked … most often it is two towers"* — and **neither raises a single
enemy stat.**

**Its documented failure mode, both directions:** too loose and the answer is "build the best tower
everywhere", so the upgrade system stops mattering; too tight and the answer is a single memorised
solution — *"the same failure with a worse mood."*

### 4.3 Lane geometry — what it buys and how it breaks

**Buys:** position becomes legible (you can see at a glance which lane is losing), and **failure
becomes local** — one lane collapsing is recoverable, not a game over.

**Costs:** the lane is a very tight constraint on ability design; cross-lane effects are *"the main
source of balance surprises."*

**Breaks when:** lanes stop being independent (one cross-lane tower solves all of them, so the
geometry is decoration), **or** they never interact at all (the game is *N* independent one-lane games
and the board adds nothing).

### 4.4 Endless, done right

- **BTD6 freeplay is piecewise *linear* in eight brackets, not exponential** — per-round HP gain
  2% → 5% → 15% → 35% → 100% → 150% → 250% → 500%, with a closed form past round 501 of
  `f(N) = 5N − 2008.5`. *"Growth is `Θ(N)`, not `Θ(c^N)`. A tower whose damage also grows linearly
  stays relevant indefinitely."*
- **Leak damage is explicitly excluded from the ramp** — *"HP ramps; consequence does not. Scaling
  the punishment alongside the threat would make every mistake instantly fatal."*
- **Speed ramps with deliberate discontinuities** at rounds 101/151/201/252 — *"The jumps are the
  design. A smooth speed ramp would let a defence drift into failure; a step forces a re-evaluation on
  a known round."*
- **Control degrades to a floor, never to zero** — status duration loses 10% at round 150 rising to
  50% at 350+, and stops there.
- **Income is throttled harder and earlier than threat grows** — cash-per-pop decays 50% → 2% between
  rounds 51 and 141+.

### 4.5 Attrition and recovery, from lane defense specifically

- **Legion TD 2:** *"After each enemy wave, your fighters are fully healed and restored to their
  original positions."* **The wave, not the unit, is the unit of attrition.** Also: lanes converge
  before the king, so *a clean lane becomes a reserve for a teammate's failure*.
- **Element TD:** leaked creeps respawn at the start of the path — *"the player loses a life but not
  the income"*, which removes the death spiral most TDs have.

### 4.6 The known hazard of a static layout an attacker can study

Forum-tier only, and flagged as such: asynchronous systems where a fixed layout is attacked
repeatedly converge on solved attacks — symmetrical bases are predictable, replays are studied one
piece at a time, and *any* automated defense is eventually cheesed by someone with time. This is a
**single-player campaign**, so the sharp edge (a human opponent farming your layout) does not apply —
but the AI-commander analogue does, and it argues for the layout being re-tested by *varied* forces
rather than the same wave repeatedly.

---

## 5. The shape — simple version

Nine claims. Everything else is enrichment.

### 5.1 The grid does not grow. The placement budget does.

**Rows and columns come from base tier and are fixed** — 5 lanes for a stronghold, 3 for an outpost,
the homeworld's boards authored per region. **`DevelopmentLevel` buys build slots**: how many
defenses may stand on the board at once.

This is Clash of Clans' shape (fixed grid, growing building count) and Arknights' dial
(`characterLimit` as the difficulty lever), and it dissolves the collision that made this question
worth asking:

| | |
|---|---|
| **The collision** | Endless grind says no hard progression ceilings. A 2D grid is finite and must stay renderable |
| **The resolution** | The grid is a **§11.3 board cap** — *"bounds how much can exist at one moment, not how far you can get"* — exempt by the register's own words, and it must **say so in a comment**. `MaxLivingPlants = 50` is the precedent |
| **What stays uncapped** | Once slots fill the board, further development buys **tower tier** — a magnitude, so it reads `P(Θ)` and rises forever. The board stops growing; the investment never does |

Three stages, no ceiling, no unrenderable board.

**There are two budgets, not one** — and that is the shape Dungeon Defenders shipped (§4.1): a
fungible currency plus a non-fungible per-level allowance, *"structurally the same pair as Arknights'
DP and `characterLimit`."*

| Budget | Sized by | What it gates |
|---|---|---|
| **Legion slots** | the central defense area's size (§5.7–5.8) | how many legions may stand in the heart of the base — **even and paired** |
| **Defense slots** | `DevelopmentLevel` | how many towers, walls and traps may stand on the board |

They are orthogonal on purpose: raising development buys more *fortification*, not more *army*. Army
comes from the empire-wide legion budget, which is scarce for entirely different reasons.

### 5.2 The board is the sector, zoomed in — ✅ decided

The sector already holds 3–8 slots carrying real objects — a Well on a Rootbed, a Waystation on the
Seat, a Granary. On the defense board those become **objects standing on cells that an attacker can
reach and destroy.**

What that buys, and why it is the cheaper design:

- **Building and defending become one decision.** Where you put the Well is now also a tactical
  choice. No second authoring surface, no separate layout-editor screen.
- ***"Captured — the layout you designed is now theirs"*** ([world-graph-ideal.md:466](world-graph-ideal.md))
  becomes free. The layout **is** sector state, which already transfers on capture.
- **The attacker gets objectives beyond "reach the back."** Raze the Well and the sector's yield drops
  next turn — a raid that takes no ground still hurts, which is the `Raid` stance
  ([:428](world-graph-ideal.md)) finally meaning something.
- **Losses persist correctly by construction.** Combat is stateless between turns; the outcome record
  says which structures fell and the **world** applies it. That is the seam that already exists.

Its cost is stated honestly in §7.

### 5.3 The attacker enters from the lane they marched down

`WorldEntity.OnLaneId` / `OnLaneTowardSectorId` already say which graph edge a force is arriving on.
That edge is the board edge they enter from. Scouting already gives size, element and arrival turn —
*"five, fire-typed, turn 11"* ([:468](world-graph-ideal.md)) — so the forecast is a projection of state
the world holds, not a new system.

### 5.4 One board, two seats, and it fights without you

Same layout, two drivers: **played** (you at the board) or **auto** (deterministic resolution from
layout, garrison and structures). *"Playing it yourself should be meaningfully better, never
mandatory — a campaign where every skirmish on every front must be hand-played turns a good turn into
a chore."*

> ⛔ **CORRECTED IN PLACE 2026-09-04.** This paragraph originally read *"world replay reuses the
> stored record rather than re-simulating."* **That is false** — §11.4 correction #2 recorded it and
> the source was never fixed here, so a `/spec` session read the wrong version.
> `RpgStore.WorldTurns.cs:599-606` **re-simulates from turn zero**, in a loop, with **no resolver
> supplied**. A hand-played board is therefore legal only if the resolver is supplied at **both**
> `:509` **and** `:603` — see §8 prerequisite 2.

Auto-resolve is still architecturally cheap, but for a different reason than this section first gave:
the seam is `intentSource`, already present as `Resolve`'s eighth parameter, and the played and
auto-resolved paths run **the same kernel** rather than two estimators (§5.16).

Attacking is the same board with the seats swapped — you drive a force against a layout an opponent
(or Zomboss's AI) authored. **Not a second mode.**

### 5.5 Winning is not wiping

Today a battle ends exactly three ways, all wipe-based: `Victory` (wave dead), `Defeat` (squad dead),
`Stalemate` (`MaxRounds`) — `BattleEngine.cs:361-363`. A base defense does not end that way. It ends
when the attacker **breaches** (reaches the back), when the defender **holds** (the wave is spent),
or when a specific objective is **razed** — and a raid that razes the Well and leaves is a real
outcome, not a defeat.

Two prior-art rules should be adopted with the outcome model rather than discovered later:

- **A leak is a life, not a loss.** BTD6 ramps HP for 500+ rounds and explicitly excludes leak damage
  from the ramp: *"HP ramps; consequence does not. Scaling the punishment alongside the threat would
  make every mistake instantly fatal."*
- **The wave is the unit of attrition, not the unit.** Legion TD 2 fully heals and re-positions
  defenders between waves. That is what keeps a multi-wave defense from being decided by round one,
  and it composes cleanly with rule 7 in §2 — combat is stateless between turns, so what persists is
  whatever the outcome record says persists, and that is a design choice rather than a leak.

### 5.6 It is `A10`'s grid, not a new one

[action-map.md:108](action-map.md) already declares **`A10 battle-board` — "Grid, occupancy,
distance"**, unbuilt, with **zero dependencies**. The grid vocabulary in §3.2 is `A10`'s, waiting.
`decisions.md`'s *Lawn position write* row names the same thing: *"in web battle it is `A10`'s
board"*, and lists `A10` as a named-deferred prerequisite.

**A base-defense board that invents its own grid model creates the second vocabulary this repo has
already paid to avoid three times.** Whatever the program boundary ends up being, the cells, the
distance metric and the area shapes are `A10`'s.

### 5.7 The central defense area — the base's heart

**Enrichment round 1, owner, 2026-09-04.**

A base has one **central defense area**. Lose it and you lose the base. To capture a base, the
attacker must **kill every troop standing in that area** — not reach a back row, not raze a
structure. Troops there are **legions**: one or several.

**The area is a region inside a larger board** (owner decision 2, §0), not the board itself. So a
base board has two zones:

| Zone | Holds | Role |
|---|---|---|
| **Outer ground** | buildings, obstacles, towers, walls — the sector's slots and what is built on them | what the attacker crosses; raidable without taking the base |
| **Central area** | legion slots, even and paired (§5.8) | the objective. Kill everything in it and the base falls |

That split is what makes a **raid** a distinct outcome from a **capture**: razing the Well in the
outer ground costs the defender real yield next turn without the centre ever being reached — the
`Raid` stance ([:428](world-graph-ideal.md)) finally meaning something. It also keeps buildings and
troops out of each other's way entirely, which is half of why §5.9's degenerate strategy cannot arise.

This is a better objective than "breach the back row", for three reasons that are already written
down elsewhere in this repo:

- **It reuses the outcome model almost as-is.** §5.5 said a base defense needs outcomes that are not
  wipes. This one *is* a wipe — `BattleEngine.cs:361-363` computes `Victory` as `!AnyActive("wave")`
  and `Defeat` as `!AnyActive("squad")`. Scoping "active" to *the central area* rather than the whole
  board is a far smaller change than inventing breach detection. The tower/wall gap in §3.4 stays
  real, but the **win condition** stops being one.
- **It makes garrisoning a decision with teeth.** Legions are the empire's parallelism budget, and
  the world stage sizes the whole game at **6–10 legions** (`world-stage-ideal.md` §8e.3, tunable).
  Committing 2 or 4 of them to sit in one base is 20–67% of the army not expanding. That is a real
  cost, paid in the same currency as everything else.
- **It gives the board a centre.** §4.3's documented lane-defense failure mode is that lanes never
  interact and the game becomes *N* independent one-lane games. A single shared objective every lane
  leads to is the standard fix — Legion TD 2 converges its lanes on one king for exactly this reason.

### 5.8 Legion slots, and the even-pairing rule

**The area's size sets how many legions may stand in it**, and the owner's rule is that the number is
**even and paired** — the area supports N per side, so 2 v 2 or 4 v 4.

**Recommendation: keep the pairing on legion slots, and put the base's advantage entirely in the
board.** The pairing is what makes the fight legible and what lets one board serve both seats (§5.4).
But if the two sides are equal in *every* respect, the base you built contributes nothing, and §4.3's
other failure mode bites: the geometry becomes decoration.

**HOMM3 — the reference the owner named — resolves this exactly that way, and it is worth being
precise about it**, because it is asymmetric where this proposal is symmetric:

| | HOMM3 | Verified |
|---|---|---|
| Army slots per hero | **7 troop stacks**, one creature type each, stacks up to 9,999 | [Heroes 3 wiki — Troop stack](https://heroes.thelazy.net/index.php/Troop_stack) |
| Siege defender's extra armies | Garrison **and** a visiting hero — two 7-stack armies, not one | [Heroes III wiki — Siege](https://homm.miraheze.org/wiki/Siege) |
| Siege defender's extra advantages | Arrow towers, a moat, siege walls; a hero defending a town **cannot flee or surrender** | same |

So HOMM3's slot count is even (7 and 7) and its **siege** is deliberately not — the defender gets a
second army *and* fortifications. **The proposal here is stricter: even slots, and the fortifications
carry the whole difference.** That is a cleaner rule and a defensible one, but it means the towers,
walls and the central area's own defenses are not decoration — they are the *only* thing that makes
defending better than meeting in the open, and they must be tuned to actually do that.

Two consequences worth settling early rather than discovering:

1. **"Even" should mean the *capacity* is even, not that both sides must fill it.** An attacker with
   three legions should be able to assault a 4-slot area and simply be outnumbered. Requiring a full
   roster to attack would gate a verb behind an inventory count, which is the shape of rule that
   produces "I cannot attack and I do not know why."
2. **`PlaceholderBattleResolver.DefenderBonusMilli` is currently the entire fortification model**
   (`PlaceholderBattleResolver.cs:79-83`) — one flat per-mille multiplier for standing still. If the
   board carries the asymmetry, that multiplier should shrink toward nothing as real fortifications
   land, or the defender gets paid twice for the same thing.

**On slot counts: there is no limit today to reconcile with** (§3.6), so this number is free to
choose. Both the count and its growth per development level are tunables from the first line of code
— and the two existing precedents say which way to do it: like `expeditions.v1.json`'s
`squadSlots`, never like `WebMatchService`'s `const int maxSquad = 6`.

### 5.9 The field cap, and splitting the overflow into waves

**Enrichment round 2, owner, 2026-09-04.**

The area caps how many units stand on the board **at once**. A side whose troops exceed the cap sends
the rest **in later waves**. Both sides get the **same** cap, so one side can never flood the field
while the other cannot answer.

#### The mechanism already exists — and it is the right one

`CapPolicy.TryAdmit(side, LivingCounts, config)` (`Match/CapPolicy.cs`) is exactly this: a per-side
living-count gate, with stable reject reason codes (`cap.plants`, `cap.zombies`, `cap.bullets`),
`-1` as the "unlimited" sentinel, and its numbers already in `data/tuning/match.v1.json`. It is
driven by `MatchRuntime.TryAdmitSpawn` (`MatchRuntime.cs:224-241`), which checks phase first, then
the cap. It is **built, tested, and tunable.**

Two things to carry across carefully:

- It is **asymmetric today** — 50 plants vs 80 zombies — which is the opposite of the rule here. The
  *shape* transfers; the values do not.
- It is **match-scoped and PvZ-sided** (`plant`/`zombie`/`bullet`, living in `MatchRuntime`). A
  battle-side version keys on `squad`/`wave`. Reuse the pattern, not the type, or the world/battle
  boundary picks up a PvZ vocabulary it does not want (§2 rule 1).

#### What genuinely has to be built: reinforcement

**A battle's actor roster is fixed at construction and never grows.** `BattleRunState.cs:132-134`
materialises `Actors` once from `setup.Squad.Concat(setup.Wave)`, and there is **no add path
anywhere** — `Actors.Add` and `Actors.Insert` return zero hits across all of `src/`. No summon, no
spawn, no reinforce.

So "split into waves" is a **real gap** in the battle engine, and it is the same gap as
§3.4's structures: both need a roster that can change during a run. Worth noting they are one build,
not two.

#### ⚠️ Deriving the cap from empty cells has a degenerate strategy

Stated plainly because it is cheap to fix now and expensive later.

If the cap is `f(empty grid cells)` and both sides share it, then **the defender shrinks the attacker's
cap by building.** Wall off thirty of forty cells and the attacker deploys two units at a time into a
board full of towers. That is not a hard defense to beat — it is a defense that cannot be attacked,
which is the same thing and worse.

It also puts towers and troops in competition for one budget. §4.2's documented failure mode applies
in both directions: *"too loose and the answer is build the best tower everywhere … too tight and the
answer is a single memorised solution, which is the same failure with a worse mood."* When two things
share one budget, one of them wins permanently and the other stops being built.

**Arknights, the one shipped game measured for this, deliberately separates them** (§4.1): 39
buildable tiles is the *space*; `characterLimit` = 8 is the *concurrency*, and the research's verdict
is unambiguous — *"The binding constraint is not the board and it is not the money — it is the
concurrent-deployment cap. That is the difficulty dial, and it is a single integer in a config file."*

**Recommendation: make the cap an authored integer per base tier, equal for both sides — not derived
from empty cells.** It keeps the pairing rule exact, it is legible to the player without counting
tiles, it cannot be gamed by walling off the board, and it is one tunable row. The board's size still
matters — it decides where things can stand and how far they must walk — it just is not also the
deployment budget.

> **✅ Decided (owner, 2026-09-04): a flat authored integer per base tier, identical for both sides,
> and a tunable.** It lives in `data/tuning/`, never a `const`. Not derived from the empty-cell count,
> so the degenerate strategy above cannot arise. Combined with decision 2 (the central area is a
> region, not the board), towers and troops compete for neither space nor budget.

If the tower-vs-troop tension is wanted *deliberately*, the safe version is a **separate, additive**
one: towers cost a defense budget, troops cost a troop budget, and a structure may convert one into
the other at an authored rate. That is Dungeon Defenders' two-budget shape (§4.1) and it never lets
one side edit the other's capacity.

#### Trickle or batch — a fork worth naming

"Split as wave" can mean two different games, and `TryAdmit` gives the first one for free:

| | How it plays | Cost |
|---|---|---|
| **Trickle** — a queued unit enters the moment a slot frees | Continuous pressure; PvZ's own feel. Falls straight out of the `TryAdmitSpawn` gate | The board never has a quiet moment; "waves" stop being legible as waves |
| **Batch** — the field resolves, then the next batch enters together | Legible waves, a rebuild window between them, and the even-pairing rule is **visible** every wave | Needs a between-waves phase the engine does not have |

> **✅ Decided (owner, 2026-09-04): batch.**

It makes the symmetric cap something the player can *see* holding rather
than infer, it gives the defender the breathing room that makes rebuilding meaningful, and it matches
the two lane-defense rules already in §4.5 — Legion TD 2 restores fighters between waves (*"the wave,
not the unit, is the unit of attrition"*), and that composes cleanly with §2 rule 7, where what
persists between engagements is whatever the outcome record says persists.

### 5.10 Independent by construction — read the input, default it, wire the producer later

**Owner decision, 2026-09-04: this feature does not wait on any other module.** It is built so every
input it reads has a meaningful default, and the producers are wired when they land.

That is this repo's own established pattern, not a new idea — four shipped precedents:

| Precedent | The shape |
|---|---|
| `WorldSlot.StructureId` | Shipped as a field with the comment *"Null on every slot today — the mechanism ships before any content uses it"* (`WorldState.cs:107-108`). Content arrived later; the field did not change |
| The L25 batch (`decisions.md`) | Five modules' new hashed fields landed **together, with no behaviour wired**, `RulesetVersion` unchanged — deliberately, to close the golden budget once |
| `IBattleResolver` | The seam shipped with a placeholder behind it so *"nothing else can start depending on its numbers"* (`BattleSeam.cs:26-28`) |
| `boardAvailable` | A parameter threaded through compiler, validator and store **before** any board exists, defaulting `false` (`ActionValidator.cs:24-29`) |

#### What this feature reads, and what it defaults to

| Input | Owner | Our default | Wiring later |
|---|---|---|---|
| **`WorldSector.DevelopmentLevel`** — sizes the defense-slot budget | `sector-development` (Draft, unbuilt) | **Level 0 is a complete, playable base**: the smallest authored board with its own defense-slot count. A tunable *table* keyed by level, whose row 0 is real content — never a formula that degenerates at zero | Nothing. The day `TurnEngine.Growth` stops being `return world;`, the table's higher rows start being reached. **Zero changes here** |
| **The battle resolver** for a base assault | `combat-handoff` (unspecced) | We supply our own `IBattleResolver` implementation and pass it at `RpgStore.WorldTurns.cs:509` — the argument that is missing today (§3.3) | Already the wiring. This program *closes* that gap rather than waiting on it |
| **`A10 battle-board`** — grid, occupancy, distance | `action-corpus` (named-deferred, zero deps) | **We cannot default this — the grid is the feature.** Either this program builds `A10` and the action program adopts it, or `A10` lands first and we bind to it. It must not be a second grid (§5.6) | A boundary question for `/spec`, not a dependency to wait on |
| **`SlotKind.Seat` / `SectorTypeFlags.Fortress`** | world model (both inert, §3.3) | Read them; both already exist and both are one line from meaning something | Set the flag on a catalog row; add a Seat-requiring defensive structure |
| **Defensive `StructureKind`s** | this program | We add them. `sector-development` adds *yield* kinds; the two lists do not collide | None |

#### ⚠️ The one place independence does not hold, stated honestly

**The tower gap (§3.4) and the mid-battle roster gap (§5.9) both live in `FusionRpg.Core/Battle`**,
which is shared with expeditions and web matches and is locked by two golden sets — four battle
hashes plus four expedition hashes. A change there is **not** independent by construction: it is
coupled through the hash, and that coupling cannot be designed away, only managed.

What manages it is already proven twice in-repo: `[JsonIgnore(Condition = WhenWritingDefault)]` plus
blanking in `Hash`, the `ContentHash` and `Warnings` precedents (`BattleGoldenTests.cs:144-149`). Note
the sharper lesson recorded at `BattleModels.cs:63-68` — **a property name alone, carrying no value,
moved all four hashes.** So "additive and defaulted" is necessary but not sufficient; the serializer
condition is the actual requirement.

**Consequence for sequencing:** everything world-side, board-side and FE-side can proceed against
defaults. The battle-engine changes are the one place where this program must coordinate with whoever
else is moving `RulesetVersion`, and that is a `/spec` scheduling item, not a design blocker.

### 5.11 The stage — turn-based, on its own

**Owner decision, 2026-09-04: turn-based, on its own stage** — not `#/battle`, and not real-time.

#### Turn-based is a data row, not an engine

[spec-virtual-time-core.md:7](battle/spec-virtual-time-core.md) states the property this rests on:
*"The simulation clock and the Future Event List — **the two pieces that make turn-based and
real-time the same architecture.**"* [battle-turn-ideal.md](battle-turn-ideal.md) §2 elaborates: both
run the **same event queue**, differing only in clock advance — turn-based *jumps* to the next
scheduled event (`NextEventAdvance`), real-time advances in *fixed steps* (`FixedIncrementAdvance`).
Both `ITimeAdvance` implementations are **built** (`SimulationClock.cs:27-35`, `:49-78`).

`data/tuning/battle.v2.json` ships three profile rows — `classic-round` (`w:1`), `galaxy-sync`
(`w:2`), `hybrid-atb` (`w:4`, `maxPoints:2`). **A siege board is a fourth row**, and the ideal states
the rule that makes that binding: *"Adding a mode should mean adding a row, not a branch in the
engine. If a mode needs an `if` inside the scheduler loop, the abstraction is wrong."*

#### What turn-based buys, concretely

| Free, because the kernel already does it | Evidence |
|---|---|
| **Auto-resolve** — §5.4 requires the base to fight without you | The same kernel driven by `StubIntentSource`, which is built. A real-time board needs a second resolution model |
| **Turn-order forecast** — the "deeper control" read | *"Copy the queue, roll it forward `K` events with no side effects, render the list"* (`battle-turn-ideal.md` §7). FFX's CTB window, zero engine work |
| **GG-13 satisfied trivially** | The kernel advances only when acted on, so *"does it keep running under a panel"* is not a question. GG-13 forbids *"a blocking panel over a live board with no pause"* — a real-time board makes that a hard problem on every layer open |
| **Byte-identical replay** | Human input timing never enters the simulation |

#### ⚠️ `W` is not the field cap

Stated because conflating them would quietly couple pacing to army size. **`W` (concurrency width) is
how many actors may be *mid-action* at once** — `hybrid-atb` ships `w: 4`. **The field cap (§5.9) is
how many units *stand on the board*.** Two numbers, two jobs, and neither derives from the other.

#### Its own stage — the cost, in files

A fifth stage is chosen over reusing `#/battle` so a siege board's HUD and transport never constrain
a squad battle's, and vice versa. That is a real benefit and it is not free. From the FE survey:

**Mandatory (2):**
1. `src/stages/<name>/<Name>Stage.tsx` — must call `useStageMountGuard("<name>")` and wrap in
   `<StageHost>` (pattern: `stages/world/WorldStage.tsx:46, 73, 108`).
2. `src/app/routes.tsx` — lazy import plus `<Route>` + `<Suspense fallback={<ChunkFallback …>}>`,
   placed **above** the catch-all `<Route path="*">` at `:120`.

**Conditional, each with a cited trigger:**

| File | Trigger |
|---|---|
| `app/AppShell.tsx:15` `NON_SCROLLING_ROUTES` | A camera-owning stage must add its path or the outlet grows the page |
| `shell/railState.ts:31` `currentStageId` | If the stage renders the `<Rail>`. **Note: `"battle"` is already in this union with no stage behind it** — after this decision there will be *two* declared-but-unbuilt ids unless `#/battle` lands first |
| `shell/bandGuard.ts:97-111` | If it renders `DialogShell` / `band-dialog` |
| `i18n/vocabularyGuard.ts:16-33` | If any engine vocabulary reaches the surface — GG-23 is a Tier-1 gate |
| `scripts/check-bundle.mjs:49` | A Phaser stage must stay lazy or the build fails |
| `theme/hexGuard.ts:25` | No hex colour literals in `stages/` — only `game/` is exempt |

**Automatic:** `vite.config.ts` coverage already globs `src/stages/**` (70/70/60/70 thresholds), and
`contract/contractGuard.ts:57` guards `stages` — **so the stage file may never name a `*Dto`.**

**Two documents this decision moves**, and they should be corrected when the spec lands rather than
discovered: `design/information-architecture.md` §1 says *"Four stages, one at a time"* and its §2
stage catalog lists exactly four; the verb table's `Space` row reads *"Lawn and battle only."*

**Naming — ✅ approved by the owner, 2026-09-04: `siege`.** GG-23 is player vocabulary only, and
`siege` covers both seats in one word — you besiege a base, and a base under attack is besieged. The
route is `#/siege/{id}`; the stage id is `siege`.

### 5.12 Movement — both sides, and what it drags in

**Owner decision, 2026-09-04: both sides move.** Obstacles block movement and shape pathing; walls
are real barriers; positioning is the tactical game.

#### The turn economy is already locked, and it is not HOMM3's

> ⛔ **CORRECTED IN PLACE 2026-09-04.** This paragraph cited the wrong file and drew the opposite
> conclusion from the source. §11.4 correction #5 recorded it; the source was never fixed here, and a
> later `/spec` session then "fixed" a spec **toward Action Points** on the strength of the surviving
> error. Two wrong answers from one uncorrected paragraph — which is the argument for correcting in
> place rather than only in an errata table.

The authority is [action-map.md:430](action-map.md), *"Resolved 2026-08-22"* item 2 — **not**
`action-corpus-ideal.md`:

> *"**Move and attack: two separate actions, and the clock decides whether you get both.** This is
> already what the kernel was built to do, and it needs no new economy. … a cheap step (200) and an
> expensive strike (800) cost differently, so **a fast actor can fit *both* into the window a slow one
> needs for one swing.** No compound move-and-attack action is required, **and no Action Points. The
> time cost is the economy** … (`ActionPoints` still ships in the timeline's economy set for modes
> wanting a fixed per-turn budget — **it is simply not what this mode needs**.)"*

So **a unit may move and strike in the same window if it is fast enough** — readiness is work over
rate, and each action carries its own `TimeCostTicks`. The economy is `OneActionPerTurnEconomy` (one
action per *activation*), **never `ActionPointsEconomy`**.

The consequence for the board is still real, just smaller than first stated: **a slow unit crossing
the outer ground contributes little while it crosses.** The lever is how far one move action carries —
a movement stat in cells, not one cell per turn — plus a board sized against it. Both are tunables and
both should be sized in the same balance pass.

#### This joins `A10` in the "cannot be defaulted" row

Decision 7 (§5.10) says every input gets a meaningful default. **Movement is not an input — it is the
feature**, exactly as the grid is. So `A9 movement-actions`' reposition half sits beside
`A10 battle-board` as something this program either delivers or binds to; it cannot be stubbed and
wired later.

Note the dependency shape works in our favour: `A10` has **zero** dependencies and `A9` depends only
on `A5` and `A10` ([action-map.md:107-108](action-map.md)). Nothing else blocks either.

#### What movement drags in, named now

| Needs | Why | Existing help |
|---|---|---|
| **Cell occupancy** | A unit must not walk through another | Already inside `A10`'s stated scope: *"Grid, occupancy, distance"* |
| **Pathing** | Obstacles reroute rather than merely block | Nothing exists. `GridDistance` is Chebyshev distance only — a metric, not a pathfinder |
| **A refused-move reason on the wire** | GG-55: *never disable a control without saying why* | The world stage's `targeting/` module already has `BlockedTarget.tsx` and `blockedPlacement.ts` built and inert (§3.3) — the pattern is there to copy |
| **A movement distance** | How far one move action carries | ⛔ **Corrected 2026-09-04 — an earlier draft put this on `P(Θ)`, which is a category error.** At the shipped dial `P(1) = 106`, so a move range of **106 cells** saturates the board on turn one. **Board-space quantities are neither contests nor damage magnitudes** — they are flat, board-bounded tunables, the same treatment `CompiledAction.MaxRange` has always had. `ssot-power-scale.md` §10's inventory is closed and contains no range, distance or duration scale |

### 5.13 Buildings and obstacles — trench warfare

**Owner decisions 11–15, 2026-09-04.** The frame is real-life trench warfare: the defender's power
comes from ground they have prepared, not from a bigger army.

#### They are a new actor kind — the keystone, not a detail

**There is no actor-kind discriminator anywhere today.** `BattleActorSetup` carries only `Side`
(`"squad"`/`"wave"`); `EntityFacts` carries Side/TypeId/Hp/Element/Row/Col/IsMindControlled/IsKiller/
StatusMask and no kind. Adding one unblocks all three §3.4 rejection rules **at once** —
forced-to-attack, break-the-round, keeps-battle-alive. One change, not three.

| Property | How it lands |
|---|---|
| **Never equipped** | `SpecimenId` null, `EquippedActionIds` empty. An **absence**, not a mechanism — no work |
| **Has traits and actions** | `TraitIds` and `EquippedActionIds` are plain lists on `BattleActorSetup`; neither requires a specimen or a level. **Free** |
| **No level** | ⚠️ **Cannot mean "no `Θ`".** Level feeds `Θ`, `Θ` feeds `P(Θ)`, and a wall with HP is a magnitude. A structure **inherits `Θ`** — it does not earn it. Anything else is a private `f(x)` curve, which §2 rule 4 forbids and `ssot-power-scale.md` §10's closed inventory would reject |
| **Receives nothing** | See *Scope* below |

#### Garrisoning shrinks the biggest real gap

Decision 15 — a building acts **through its occupant** — is load-bearing architecture, not flavour:

- **Buildings never enter the initiative order.** §3.4's gap shrinks from *"an actor that acts but is
  not a demon"* to *"a destructible board object."* The kind flag is still needed so `AnyActive`
  (`BattleEngine.cs:405-406`) does not count a wall as a living side — but the hard half is skipped.
- **Structures carry traits and actions as _data_; the occupant is the executor.** That reconciles
  "buildings have traits and actions" with "buildings receive no buffs": nothing is ever granted *to*
  the building.
- **Garrisoning costs a field-cap slot**, so buildings compete with bodies for *deployment* — opt-in,
  reversible, re-decided every turn. Categorically different from the space-competition §5.9 rejects,
  which was permanent and unilateral.
- **Capturing a tower is walking into it.** No ownership-transfer mechanic — the same answer decision
  12 gives, arriving from the other direction.

#### No ownership — and the rule that keeps it honest

Possession on the board is by occupation; world ownership is durable and settled by the outcome
record. That is exactly §2 rule 7 — *combat never writes world state; it reports, and the world
decides consequences.*

> **The board never reads `WorldSlot.OwnerFactionId`.** A Well has a world owner and no board owner.
> The moment board logic consults world ownership, the two models diverge and capture gets ambiguous.

#### The build cost is a world resource, and the seam says how

Decision 14 puts the cost on **loam**, not the actor pools — and the repo already draws that line:
`resource-hub-ssot.md` is *"a **different** hub — actor pools, not empire stock"*
(`economy-principles.md`'s own grounding note). So a build cost touches the resource hub **not at all**.

> **✅ A recorded rejection was found over-broad and narrowed — owner call, 2026-09-04.**
> `empire-economy-ssot.md` §8 rejected *"loam as a battle resource (scope collision with the actor
> hub)"*. **§1 of that same document is the evidence against it**: a stock and an actor pool are
> *"different scope entirely"*, so a stock spent during a battle cannot collide with the actor hub
> unless it is modelled *as* a pool — which nothing here proposes. Two things marked it as a defect
> rather than a judgement: it contradicts its own §1, and it is the **only** entry in that rejected
> list with no principle behind it (its siblings cite **P5**, **P7**, **P4**, or a stated mechanism).
> **Narrowed to "loam as a seventh actor pool", which stays rejected.** A side-scoped battle budget is
> permitted. Correction and provenance recorded in `empire-economy-ssot.md` §8; the same pass fixed
> that file's stale *"five actor pools"* (there are six — `poise`, registered three days after it was
> written).

But it meets a rule, and the rule has a shipped answer:

> *"Combat never writes world state. It does not claim sectors, **spend shards**, or move legions."*

So an in-battle build **may not debit `WorldSector.LoamStock` or `WorldEntity.CarriedLoam` directly.**
The resolving pattern is already named in the vision — the **depot**,
[world-graph-ideal.md:458](world-graph-ideal.md): *"depot (starting resource for the fight)."*

```text
world turn  --BattleRequest{ budget }-->  siege board
                                          spends internally
            <--OutcomeRecord{ spent }--   world debits
```

**And this is nearly free.** `BattleRequest` and `BattleOutcome` are **neither hashed nor persisted** —
verified, zero hits across `WorldCanonical.cs` and `RpgStore.World*.cs`. They are transient in-turn
records, so adding a budget field moves **no golden**, unlike anything touching `BattleReport` (§7.7).

#### Where each side's loam comes from — an asymmetry already modelled

| Side | Source | Consequence |
|---|---|---|
| **Defender** | the sector's own `LoamStock` — at home, supplied | Blockading production is how an attacker stops them rebuilding |
| **Attacker** | `WorldEntity.CarriedLoam` — what the legion marched in with | See below |

`CarriedLoam` is *"entity-level, not per-member: members carry as a crew"*, and
`LegionSupply.Capacity = BearerCount × CarryPerBearer` while burn scales on **total headcount** — the
file names why: *"capacity and burn must scale with different things, or bearers buy nothing."*

**Two consequences fall out with no new mechanism:**

1. **`WorldEntityMemberRole.Bearer` becomes a real composition decision.** Today it only extends supply
   range. Make it the siege train and a legion's shape becomes a tactical choice.
2. **Spending your reserve to win a siege can kill you on the way home.** L27 (`decisions.md`) already
   decided: *"a legion beyond its faction's supply burns its own `CarriedLoam` and is **destroyed
   outright** the turn that reserve would go negative."* Build heavily, win, starve marching back — a
   genuine strategic cost, already shipped, requiring nothing.

> ⛔ **Correction, 2026-09-04 — an earlier draft of this row said `LegionSupply` is "unwired", quoting
> *"nothing here is called from `TurnEngine` yet"*. That is a stale class comment
> (`LegionSupply.cs:7-9`), and citing it instead of checking the call site is exactly the failure
> `DESIGN-GATE.md` evidence rule 2 names — *a comment is not evidence.*
>
> **It is wired.** `TurnEngine.cs:232` — `return LegionSupply.Resolve(afterPressure, report,
> Phases.Pressure);` — and the engine's own version log records L27 doing it (`:38`).
>
> **This strengthens the design rather than weakening it.** `LegionSupply.Resolve` runs in `Pressure`
> (phase 5); `Build` resolves in `Snapshot` (phase 8). **So a legion is topped up to
> `BearerCount × CarryPerBearer` before it builds, every turn it stands in supply.** Construction
> funding is not a one-time 500-loam allowance — it is a per-turn budget throttled by bearer count.
> `WorldEntityMemberRole.Bearer` is therefore *already* the siege-train decision this section hoped to
> create, and *"blockade them so they cannot rebuild"* is *already* mechanically real: out of supply
> means no top-up, a burn of `headcount × BurnPerMember`, and `LegionSupply.cs:133-137` destroying the
> legion outright when the reserve would go negative.

#### Blocking a resource to exhaust them — also already shipped

The attacker's obstacles are meant to cut the defender off and wear them down. **Both halves exist**,
and they are different resources doing different jobs:

| What is blocked | Pool | Result |
|---|---|---|
| **Rebuilding** | `loam` (world) | No new walls. Not a debuff — an inability |
| **Sustaining** | `hunger` (actor) — *"metabolic cost, and for plants this is **Sun**. Also **sustain**: it gates regeneration and condition"* | Metabolic failure, derived-stat debuff |
| **Moving and fighting** | `stamina` (actor) — pays for *"move, basic attack, reposition"* | Body failing, derived-stat debuff |

`resource-hub-ssot.md`: **every pool except `hp` already has an exhaustion mechanism that debuffs
derived stats** — *"the actor can still act, but the body is failing."* Not death. That is exactly
"make them exhausted", built.

**The `stamina` case needs no design at all.** Movement costs stamina; a detour is longer; so a
well-placed obstacle exhausts an attacker **by construction**, with no rule saying obstacles are
tiring. Also useful: *"exhaustion is re-evaluated on read, not only on write"* — no polling.

#### Scope: no fifth `WhoKind`, not yet

Decision 11 says structures receive nothing. The instinct is a fifth `WhoKind` — the vocabulary is
`{ Target, Type, UniqueDemon, Relation }`, closed, *"adding a fifth is a reviewed change"*. **Two
reasons not to, today:**

1. **It points the wrong way.** A fifth kind names a population you *can* reach. Exclusion is wanted,
   and adding `Structure` would not stop `Relation = Ally` from reaching walls.
2. **`ScopeCompatibility` is already deny-by-default** — *"Everything else rejects `ScopeUnsupported`
   rather than guessing — an unlisted combination is not assumed safe."*

**The cheaper answer: put the kind on the actor, and let relation-based populations enumerate
combatants only.** Structures fall out of scope by construction, with no enum change.

The bar for extending a closed vocabulary is set by precedent: `OwnerKind.UniqueActor` was added to a
closed seven-value enum on 2026-09-02 — correctly — because a **real** owner did not fit (`Entity` is
session-scoped and would silently wipe bindings), and it went through owner approval. **A real case
that does not fit, never a speculative slot.**

**Where the real case will come from:** trench *cover*. A trench is not buffed — it makes the unit
**in it** harder to kill, so the modifier lands on the occupant and decision 11 holds. That needs a
*positional* population ("whoever is standing here"), which none of the four `WhoKind`s is. The
delivery pattern is already shipped — `ScopeMembershipEvent(Ptr, Transition)` plus a reactor granting
and withdrawing per entity, *"no cached or rescanned population"* — and cell entry/exit is a fourth
transition of exactly that shape. Honest caveat: `ScopeMembershipTransition` is itself declared closed
(*"Nothing else — this is not a general event bus"*), so that **is** the reviewed change, with a real
mechanic behind it.

### 5.14 Building materials — a fourth and fifth stock, world-scoped

**Owner decisions 16–18, 2026-09-04.** Two building materials, construction-only, world-scoped.
`ironwork` is one of them.

#### The map already ships the faucets. The economy deleted the stocks.

Not a proposal — shipped, guarded, and valued:

| Slot | Guard tier | Placements in shipped templates | Yields today |
|---|---|---|---|
| `shard-vein` | **`GuardHeavy`** — the hardest tier | **4** (`WorldTemplateCatalog.cs:159`, `TwoHearths.cs:89, :152, :202`) | **Nothing.** Rift shards were cancelled (`empire-economy-ssot.md` §1) |
| `material-seam` | `GuardMedium` | **3** (`WorldTemplateCatalog.cs:96`, `TwoHearths.cs:125, :175`) | **Nothing.** No material stock exists |

`SlotValueCatalog.cs` values them at **700** and **650**, and comments on its own gap: *"The
producers. Even until `sector-development` gives them output, they are the reason to take ground."*
`LoamProduction` reads `SlotKind.Rootbed` and nothing else.

**So across two shipped maps, seven guarded slots — four of them behind the hardest guard in the
game — produce zero.** The world model and the economy model disagree, and the world model shipped
first.

#### Why this re-runs P4 rather than overriding it

`empire-economy-ssot.md` §2's P4 test concluded *"three stocks, and only three"* from five buildings —
Well, Waystation, Granary, Deep root, Soul conduit — all costing *"loam + turns"*, hence *"not one
bottleneck pair anywhere."*

**It never saw defense construction**, which `world-graph-ideal.md` §7.3 calls *"Separate from economy
and deliberately so: walls, towers, moat, traps, totem, depot, rally point, last stand."* The category
most likely to produce a bottleneck pair was the one category excluded. This is a re-run with a
corrected input set — the same shape as §8's correction, not a second override.

#### Why world-scoped, and not the SSOT's compound-cost alternative

The SSOT's own fix was *"some buildings cost essence alongside loam."* **Loam is World-scoped and
*"never banks"*; essence is Player-scoped.** So that would pay for a building which *dies with the map*
using permanent, cross-world stock — punching through §7's whole bounded-worlds cure (*"granaries and
waystations are world-scoped and die with the map"*).

Two world-scoped materials keep loam's scope discipline intact: both die with the map, and
`min(loam, material)` is a bottleneck between two stocks at the same scope.

**And the thematic argument stands on its own:** souls summon **demons**, and Wall-nut *is* a demon. If
souls also bought walls, "wall" would have two acquisition paths and decision 11's demon/structure
split would blur at the economy layer.

#### Satisfying P4 for the *second* material — the part that needs care

P4's warning applies with full force to stock number five: without its own distinct `min(x, y)`, it is
a currency wearing a costume. **The split that earns it is bulk versus worked**, which is also AoE2's
shipped shape — stone is the fortification-specific scarce resource while wood and food are bulk.

| Stock | Faucet | Buys | Character |
|---|---|---|---|
| **bulk material** | `material-seam` (`GuardMedium`, 3 placements) | earthworks — trenches, ramparts, revetments, moats, obstacles | cheap, fast, plentiful |
| **`ironwork`** | `shard-vein` (`GuardHeavy`, 4 placements) | emplacements — towers, gates, traps, reinforced works | scarce, slow, precise |

**Neither substitutes for the other.** You cannot trench your way to a tower, and you cannot forge your
way to a moat. The bottleneck binds differently per building type, which is exactly what P4 asks for.

**The guard tiers already encode the gradient**, with no tuning: the scarcer material sits behind the
harder guard, in maps that already ship. `world-graph-ideal.md`'s own rule — *"guards scale to
reward"* — is doing the work for free.

**Naming.** `empire-economy-ssot.md` §1 chose "loam" because it *"collides with nothing"*. Same test
run 2026-09-04: **`metal` collides in 49 files** (five demon species — `ferro-flora`,
`magneto-flora`, `magneto-fungi`, `armored-legume`, `explosive-fungi`), **`stone` in 29** (including
Tailwind's `stone-` palette in the shipped FE CSS). Clean: `timber`, `granite`, `masonry`,
`ironwork`, `rubble`. **`ironwork` is chosen** for the worked material; the bulk material's name is
open — `rubble` is the strongest candidate, being both Fracture-native (you build from what the
breaking left) and literally what fills a revetment.

#### ⚠️ Two costs to budget now

1. **`shard-vein` is named for a cancelled stock.** Keeping the id while it yields ironwork is a lie in
   the data. Renaming it moves **every world golden** — `WorldCanonical` writes the slot type id as a
   **string**, not an ordinal (`SlotTypeCatalog.cs:25-28`). Pay it once, batched with this program's
   other hashed changes, per the L25 precedent in `decisions.md`.
2. **Two new faucets need two named sinks (P1), and both must scale with holdings (P2).** More sectors
   → more seams → more material; the sink is more bases to fortify and repair. **The repair half
   matters**: without it, fortification is a one-time cost and the faucet outruns it, which is P2's
   exact failure.

#### Still open from this round

**Building roles.** `StructureKind` has two values (`LoamSource`, `Storage`) and four rows — too thin
for a strategy game, and this is a **vocabulary and content gap, not a currency gap**. The material
this program has to draw on: AoE2 ships **40 buildings, mean 27.4 per civ**, and the research's own
conclusion is that *"differentiation comes from **which** 70 techs, not from how many"* — **chase the
membership list, not the count.**

### 5.15 Warcraft III and StarCraft II — resources and building roles

Researched 2026-09-04 at the owner's direction. The repo's own
[07-rts-and-autobattler.md](../research/genre-mechanics/07-rts-and-autobattler.md) already carries the
supply, upkeep and pacing numbers; its recorded gap #10 — *"StarCraft II tech-building branch counts…
no consolidated tech-tree table"* — is why the building-role half needed a fresh search.

#### ⚠️ Correction: the "gold buys units, lumber buys buildings" split is a myth

A widely repeated secondary claim is *"Soldiers demand Gold, and buildings must be constructed from
Lumber."* **First-party Blizzard contradicts it**
([classic.battle.net/war3/basics/resources.shtml](https://classic.battle.net/war3/basics/resources.shtml)):

> *"Gold is required to create new buildings, **train units, and research upgrades**."*
> *"[Lumber is] used to build many different structures as well as certain weapons and machines of war."*

**Gold is universal; lumber is weighted toward structures.** Most things cost **both, in different
ratios** — and the starting stock says the scale: **500 gold, 150 lumber**, a 3.3 : 1 ratio.

**This refines §5.14's two-material design, and the refinement matters.** I framed bulk and `ironwork`
as near-exclusive — trenches take one, towers take the other. WC3's shipped answer is better:
**overlap with different ratios.** A revetment is mostly bulk with a little ironwork; a gate is mostly
ironwork with some bulk. `min(bulk, ironwork)` still binds, and binds differently per building — but
ratios are far easier to tune than exclusivity, and they never hard-block a build the player is one
unit short on.

#### The building-role taxonomy, from Warcraft III's shipped set

Human buildings ([classic.battle.net/war3/human/buildingstats.shtml](http://classic.battle.net/war3/human/buildingstats.shtml)):
Town Hall → Keep → Castle · Farm · Barracks · Altar of Kings · Lumber Mill · Blacksmith · Workshop ·
Arcane Sanctum · Gryphon Aviary · Scout Tower → Guard/Cannon/Arcane Tower · Arcane Vault.

Eleven roles fall out, and here is what this repo already has for each:

| Role | WC3 / SC2 example | Ours today |
|---|---|---|
| **Seat / tier** — gates everything else | Town Hall → Keep → Castle | `SlotKind.Seat` — **prose only**, nothing buildable on it defends |
| **Resource** | Gold Mine, Lumber Mill, Refinery | ✅ `StructureKind.LoamSource` — well, waystation |
| **Storage** | — | ✅ `StructureKind.Storage` — granary |
| **Supply** — raises what you may field | Farm (+6), Supply Depot (+8) | ❌ **missing.** Our field cap is authored, not built |
| **Production** — makes units, split by class | Barracks / Workshop / Arcane Sanctum / Aviary | ❌ hatchery is designed, unbuilt |
| **Recruitment** — a dedicated building for the special unit | Altar of Kings | player-scoped summoning exists; nothing on the map |
| **Upgrade** — improves units, produces nothing | Blacksmith | ❌ **missing** |
| **Defense** — with upgrade variants | Scout → Guard / Cannon / Arcane Tower | ❌ **missing — this program** |
| **Detection / vision** | Sensor Tower, Missile Turret | watchpost/observatory designed, unbuilt |
| **Shop** | Arcane Vault | `SlotKind.Market` declared, read by nothing |
| **Add-on** — modifies *another building* | Tech Lab / Reactor | ❌ **missing, and the most interesting** |

#### Two ideas worth stealing outright

**1. The add-on, and its forced exclusivity.** SC2's Tech Lab and Reactor attach to a production
building; the Reactor doubles throughput (queue 5 → 8, two units at once) while the Tech Lab unlocks
advanced units — **and a structure may not have both.** That is *throughput versus capability* as a
permanent, reversible-at-a-cost choice, and it is the same shape
[05-tower-defense-genre.md](../research/genre-mechanics/05-tower-defense-genre.md) found to be
universal in TD: *"upgrades are always a forced exclusive choice."* We have no role like it.

**2. The dual-role building.** SC2's Supply Depot raises and lowers — it is **supply and a wall at the
same time**, *"an important part of Terran base defenses."* For a trench-warfare board that is
directly on the nose: a structure that is economy when you need economy and terrain when you need
terrain. It also gives the "no ownership" rule (decision 12) something to bite on — capturing a depot
does two things at once.

#### The most transferable economy idea — and it is already the repo's own rule

The research file names it: **Warcraft III has no hard army cap. It has a penalty curve with two
knees**, taxing *gold income*, not the army:

| Food | Gold income |
|---|---:|
| 0–50 | **100%** |
| 51–80 | **70%** |
| 81+ | **40%** |

*"A player who wants a bigger army may have one, and pays 30% or 60% of their income for it. The
ceiling is economic, soft and configurable."*

That is **exactly** §2 rule 6 — *no hard progression ceilings; caps on magnitudes are soft and
configurable* — shipped, numeric, and proven. **It is not a model for the field cap** (decision 5,
which is a per-battle §11.3 board cap and correctly hard). **It is the model for the question nobody
has answered: how many legions and garrisons the empire can afford.** `world-stage-ideal.md` §8e.3
sizes the game at 6–10 legions with no mechanism behind the number. A two-knee income tax is one.

#### ⛔ Most of that taxonomy does not apply to us — filtered, 2026-09-04

**Owner correction: we do not copy the RTS list, because our actor system already does more than
theirs.** That is right, and it removes more of the table above than it keeps. Two structural reasons,
and they disqualify whole rows rather than trimming them:

1. **RTS units are fungible and disposable. Ours are persistent individuals.** A demon has an
   `instanceId`, a level, XP, gear, traits, contracts, six resource pools and a rolled atom loadout.
   You do not queue five more of it.
2. **There is no *worker* loop on the board** — no fungible harvester unit, so drop-off points,
   refineries and saturation curves have no job here.
   > ⚠️ **Corrected 2026-09-04, owner.** An earlier draft of this row said *"there is no
   > worker-harvest loop; resources come from the world map"* — which was wrong, and it broke
   > decision 13's own premise. If every resource is a fixed depot seeded at battle start, **an
   > attacker blockading a defender's supply has nothing to bite on.** There *is* a board economy;
   > what it lacks is a *worker*. See the box below.

| RTS role | Verdict | Why |
|---|---|---|
| **Production** (Barracks / Factory / Starport) | ❌ **Redundant** | Exists because units are fungible. Demons are summoned at player scope through souls and contracts, and they persist. A barracks producing disposable troops would contradict the roster, contract and unique-actor design at once |
| **Upgrade** (Blacksmith, +1 attack to all footmen) | ❌ **Redundant *and* harmful** | Exists because RTS units have no individual progression. Ours have levels, gear, traits, atoms and a rung ladder. A flat army-wide bonus is a **second progression curve**, which §2 rule 4 forbids outright |
| **Seat / tier** (Town Hall → Keep → Castle) | ❌ **Already ours** | `DevelopmentLevel` is exactly this, and §5.10 already reads it |
| **Supply / upkeep** (Farm, food cap, the two-knee tax) | ❌ **Already shipped** | `LoamUpkeep` already taxes headcount per sector — `Garrison: garrisonMembers × GarrisonUpkeepPerMember` (`LoamUpkeep.cs:71`), summed over every entity standing there (`:50-52`). We do not need to import WC3's idea; we have it, and it is *per-sector* rather than global, which is better for a territorial game |
| **Shop** (Arcane Vault) | ❌ **Redundant** | Items are player-scoped and already have an equip/paperdoll surface |
| **Add-on** (Tech Lab / Reactor) | ⚠️ **Withdrawn — I recommended this and was wrong** | An add-on is **equipment for a building**. Decision 11 says buildings are never equipped. Either it contradicts a decision already made, or it re-invents the atom/affix system under a second name. Both are the failure §2 rule 10 names |
| **Defense**, with upgrade variants | ✅ **Keep** | We have nothing. This is the program |
| **Detection / vision** | ✅ **Keep** | Fog exists at *map* scope (`world-intel`); a board has no equivalent, and a watchpost that reveals part of the board is a real, non-duplicative role |
| **Deny / terrain** | ✅ **Keep, and it is ours more than theirs** | RTS barely has this — walls and little else. A trench-warfare board needs obstacles as a first-class role, and no RTS taxonomy will supply it |
| **Dual-role** (Supply Depot as a raisable wall) | ✅ **Keep — as a design pattern, not a role** | A structure that is economy *and* terrain is directly on the nose for trench warfare, and it gives decision 12's "no ownership" something real to bite on |

**What survives is three roles and one pattern**: defend · see · deny, plus dual-purpose structures.
That is a much smaller set than the eleven above — and it is small precisely because the actor system
already covers what the other eight were compensating for.

#### The board economy — a harvest loop with no workers

**A depot alone cannot support a blockade.** If the whole budget is seeded once at battle start, there
is nothing for an attacker to interdict, and decision 13's *"block their resource and exhaust them"*
has no mechanism. So the board needs a **flow**, not only a **stock**:

| Layer | What it is | Who controls it |
|---|---|---|
| **Depot** | Seeded from the world at request time — what the sector held, or what the legion carried | Fixed at battle start |
| **Board income** | The sector's own production slots, standing on the board as objects (decision 3) — a Well on its Rootbed, a seam, a granary. They **yield per turn to whoever holds them** | Contested, every turn |

**Holding a node is garrisoning it** — decision 15, unchanged. That single rule does the whole job:

- **Three ways to stop an income**, which is the trench-warfare fantasy stated mechanically: kill the
  garrison, take the node yourself, or **cut the route** between the node and the side holding it
  (which is what obstacles and pathing, decision 10, are *for*).
- **A raid gets a concrete objective beyond razing** — take the Well and it pays *you*.
- **The field cap bites much harder.** Every unit garrisoning a node is a unit not fighting, and
  slots are scarce (decision 5). Economy and army compete for the same bodies, every turn,
  reversibly.

**This is the point where our actor system genuinely beats the RTS model**, which is why the filtered
table above is not a loss. An RTS worker is a rounding error — losing one costs 50 minerals. **Ours
are individual, levelled, geared and slot-capped**, so putting one on economy is a real sacrifice and
pulling it off is a real decision. RTS had to invent a separate disposable unit class to make
harvesting exist at all; we get a *better* version by having no worker and spending combat units
instead.

> **⚠️ The guardrail this needs, or it collapses the farming throttle.** `empire-economy-ssot.md` §9
> binds `combat-handoff`: **a world battle must never *pay* loam.** So board income is
> **board-scoped and expires with the battle** — spendable only on the board, never banked, and it
> appears in the outcome record **only as spend, never as gain**. Reducing what a siege costs is not
> farming; producing world stock by fighting is, and that stays forbidden.

### 5.16 The other seat — siege AI and auto-resolve

Enrichment pass, 2026-09-04. Every siege has two seats and the player occupies at most one.

#### Auto-resolve runs the same kernel. Not a second estimator.

**The decisive argument is structural, not economic.** Total War's auto-resolver compares two armies;
a siege is an army versus an army **plus walls, obstacles, garrisoned buildings, contested income
nodes and a chokepoint**. Whatever the cheap model cannot see becomes exactly the size of the
divergence — and the shipped symptom is unmistakable: siege auto-resolve is biased toward the
defender *while* manual play of the same engagement yields decisive victories. **For a base-defense
game the base *is* the missing term**, so a separate estimator starts with the worst possible version
of that bug. HOMM3 runs the same engine (its "Quick Combat" is a *presentation* switch — the manual,
p.21) and has no divergence complaint at all; its only complaint is that the AI plays badly, which is
bounded and tunable.

It is also nearly built: the seam is `intentSource` (§3.3), three missing arguments.

**Three costs, stated rather than discovered:**

1. **The quality ceiling becomes the battle AI's, permanently.** Age of Wonders states the quiet part:
   *"Auto combat employs the same AI for controlling player units as it does enemy units."* Players
   beat that AI, so delegating is a guaranteed downgrade — and with persistent, levelled, geared,
   contract-bound demons, *"losing a unit equals having a worse team"* compounds.
   ⛔ **This is the sharp tension with the requirement that playing be "meaningfully better, never
   mandatory": with one kernel both are set by the same dial.** Too far and auto is unusable
   (mandatory play); not far enough and playing is pointless — **fheroes2's maintainers hit the second
   one and openly debated making their auto-battle AI *dumber*.** That dial is a tunable from line one.
2. **Replay.** See the §2 rule 7 box — the report path re-simulates, and a siege resolver must be
   supplied at both `:509` and `:603`.
3. **Latency.** `MaxRounds = 50`, `MaxLoopIterations = 200_000`. A siege profile with movement and
   pathing is heavier than a squad fight. **Use `NextEvent` advance, and measure before anyone
   proposes a cheap path.** If measurement ever forces one, fit it to the kernel rather than
   hand-tuning a second set of numbers.

> **⛔ And never a hidden difficulty thumb.** Total War's player-penalty / AI-bonus tables produce a
> metagame about the resolver rather than the game. `spec-ai-commander.md`'s assumption 3 already
> binds this — **"Difficulty is which policy, not a stat bonus"** — and it extends to the siege AI
> verbatim.

#### The minimum defender AI that does not look stupid — six rules

| # | Rule | Where it comes from |
|---|---|---|
| **R1** | **Structures never enter initiative.** Decision 15 already gives this; the only AI consequence is that `AnyActive` must not count a wall as a living side | Decision 15 |
| **R2** | **An aggro tier separate from target choice** — `Hold` / `Guard` / `Engage`, on the *actor*, three values and no more | XCOM's `eStat_AlertLevel`, Fire Emblem's `AI1/AI2` byte pair, Dungeon Keeper's `SNIPE_*` vs `SABOTAGE_*`. ⭐ **This alone removes the worst-looking behaviour on a defence board: a garrison abandoning the objective to chase a bait unit** — HOMM3's dragon-fly trick and Clash of Clans' *"Giants that have flattened every defence will happily wander off to punch a builder's hut"* are the same defect in a turn-based and a real-time game |
| **R3** | **An additive target score with a risk term** — hit-chance **+70**, objective-class **+50**, kill available **+15**, low HP **+10**, *cannot counter me* **+10**, *threat exposure of the destination cell* **−N**, *+ current round* | XCOM's shipped weights (hit-chance dominates lethality **70 : 15** — an AI that maximises expected damage with no risk term reads as suicidal, the most-cited "stupid AI" complaint in the whole survey) plus Fire Emblem's two defensive terms and its turn-count term, a soft anti-turtle timer that is monotonic and invisible |
| **R4** | **When the preferred target class is exhausted, fall back to the OBJECTIVE — never to nearest** | The single most-documented failure in both surveyed games; nearest is only ever a tiebreak *within* a class |
| **R5** | **Freeze the acting order at turn start** | FEH's rule; and the repo already believes it one scale up — `ContactResolver.cs:15-18`'s *"one battle per place per turn"* exists for exactly this replay reason |
| **R6** | **Deterministic and readable.** No rolls, no hidden modifiers | The repo has already picked this twice at map scale: `FrontierRulesPolicy` accepts a seed and never consumes it, and `ThreatMap.cs:26-28` states the rule. On a board the player can see whole, a solvable AI makes *reading the defence* the content |

**Where R3 plugs in:** `IBattleView.FactsOf` already returns `EntityFacts(Side, TypeId, HpMilli,
ElementId, Row, Col, IsMindControlled, IsKiller, StatusMask)` — **every input the score needs is
already on the seam.** And the arithmetic is shipped: `World/Ai/Utility/Consideration.cs` has
product-of-considerations with arity compensation *and* a `Weakest()` that hands the turn report a
reason string for free. Its own comment says *"Nothing calls this yet… scoring wants an economy to
score against and there is not one until `sector-development`."* **A siege board has one** — loam,
materials, field-cap slots — so this is its first real caller.

⚠️ **Do not put the score on `ActionTargetOrdering`.** That enum has two values, and the runtime
`TargetSpec` has **no ordering field at all** — it is authored, serialized, and dropped at compile.
Extending it costs a closed-vocabulary change *plus* a wire-contract change *plus* a golden move. The
score belongs in the intent source, which is exactly where `bloodthirsty` already lives
(`BasicAttack.cs:180-188`, a pre-sorted view kept out of `StubIntentSource` so the AI *"must not gain
trait vocabulary it does not own"*).

**What this deliberately omits**, because no shipped defender AI has it and all of them are fine: no
planner, no multi-turn plan, no inter-actor coordination, no adaptation to the player's build. Clash
of Clans' entire strategic depth is a target-class enum, a layout, and a deployment position. **The
board carries the depth; the AI carries the legibility.**

#### One idea worth stealing outright, and it is not binary

**HOMM3's Auto Combat is five independent, revocable checkboxes**, not a toggle (manual p.48):
*Creatures · Spells · Catapult · Ballista · First Aid Tent* — plus *"Click anywhere during auto combat
to take control of your own troops."* And one subsystem's manual control is a **progression unlock**
rather than a setting: without the Ballistics skill the catapult is AI-driven *inside a fully manual
battle*.

That is a far better fit for *"meaningfully better, never mandatory"* than an all-or-nothing toggle,
because it lets a player spend attention exactly where it pays — delegate the garrison, drive the
assault.

#### The AI has never built anything, and nothing chooses where

| Real gap | Evidence |
|---|---|
| **The map AI files four of nine command kinds** | `FrontierRulesPolicy` files `Move`, `Clear`, `Claim`, `Stance`. It never files **`Build`**, `Sustain`, `Cede` or `BindWarden` |
| **Nothing anywhere chooses WHERE to place a structure** | Grepped for `placement`, `ChoosePlacement`, `PlaceAt`, `bestCell` — **zero domain hits.** Placement exists only as a human filling in `WorldCommand.SlotIndex`, and `SlotIndex` is an ordinal, not a coordinate |
| **`Battle/Ai/` is not a battle AI** | It holds `ZombossPattern`, `ZombossCommanderAllocation` — aptitude-point *allocation shares*, i.e. Zomboss's character build. `Resolve(StatContext _) => _cached` is a bare field read. **There is no tactical AI directory** |

**One stale status header found:** `spec-ai-commander.md:3` says *"the template still points both AI
factions at `stand-fast`"*. Not true — Zomboss runs `frontier-rules` in both shipped templates
(`WorldTemplateCatalog.cs:83`, `TwoHearths.cs:27`); only `Wild` is `stand-fast`.

### 5.17 Cover — the shipped numbers, from Relic's own data

Extracted 2026-09-04 from `cohstats/coh3-data` `weapon.json` (Relic's `ReferenceAttributes` export,
**1,078 weapons**) plus CoH2 attribute XML and the official patch-note archive. First-tier unless
marked.

#### The core table, unchanged across three games and seventeen years

| Cover | Accuracy | Damage | Suppression |
|---|---|---|---|
| none (baseline) | 1.0 | 1.0 | 1.0 |
| **heavy (green)** | **0.5** | **0.5** | **0.1** |
| **light (yellow)** | **0.5** | **1.0** | **0.5** |
| **negative (red)** | **1.25** | **1.25** | **1.5** |
| garrison (building) | 0.55 | 0.5 | 0.0 |
| smoke | 0.25 | 1.0 | 0.05 |

**That heavy/light/negative row is byte-identical in CoH1, CoH2 and CoH3.** Relic carried it forward
across three games without changing a digit.

⭐ **Cover is primarily an anti-suppression mechanic, and only secondarily anti-damage.** Heavy cover
cuts damage in half and suppression by **90%**; garrison zeroes suppression outright. We have no
suppression channel — but we have `poise` (guard) and the status layer, and this says which of them
cover should actually touch.

⭐ **A trench is the strongest cover in the game.** CoH2 `tp_trench` is **0.15 / 0.1 / 0** — roughly
three times heavy cover on accuracy and five times on damage. CoH1's is 0.4 / 0.25 / 0. **CoH3 has no
trenches at all** — `tp_trench_cover` is 1.0/1.0/1.0 on all 1,078 weapons, vestigial. For a
trench-warfare design that is the number to anchor on, and the fact that the newest game dropped it
is worth knowing before copying it.

#### One canonical row plus named deviants — the authoring pattern

The tables are **per weapon**, not global. But the distribution shows how that is actually managed:
**883 of 1,078 weapons use damage 0.5 for heavy cover; 959 use suppression 0.1; 208 share the exact
canonical five-row combination.** So the shipped pattern is *one default row, and a short list of
deliberate exceptions* — which is exactly this repo's tunables discipline, not an argument for
per-action authoring.

**And the deviants are the design.** Three archetypes carry all the counter-play:

| Archetype | Behaviour | Numbers |
|---|---|---|
| **Flamethrower** | ***Bonus* damage against cover** — the designed counter | 1.25 vs heavy, **1.5** vs garrison |
| **Sniper** | Ignores cover accuracy entirely, pays in tempo | 1.0/1.0, but `aim_time_multiplier` **2.0** vs heavy and garrison |
| **Artillery / mortar / grenade** | Ignores cover accuracy | 1.0 accuracy; 109 grenades, 105 heavy artillery, 46 mortars |

Which classes *honour* heavy cover: machine guns (194), tank guns (133), single-fire (55). **The split
is direct-fire honours cover, indirect-fire ignores it** — one rule, no table needed.

#### Three facts that simplify our model

1. **Destruction is binary. There is no green→yellow decay.** No source in any of the three games
   describes a degradation ladder; cover is present or gone. Buildable cover carries plain HP — CoH3
   sandbags **300**, tank traps **400**, sangar **400**.
2. **Height beats cover**, and CoH3 surfaces it as a broken-shield icon.
3. **Small arms ignore cover inside 10 m** (raised from 7 in a patch headed *"Cover Combat"*) — about
   29% of a rifle's 35 m range. **The one hard positional number in the whole system**, and the shape
   to steal: cover protects at range, not in a knife fight.

#### The failure modes, from Relic's own patch notes

- **⭐ Cover illegibility is the most repeated bug class by a wide margin.** Two separate patches fix
  cover indicators *"rendered floating in the air"*, and there is a long tail of *"added missing cover
  to many objects"*, *"balustrades now provide cover as expected"*, *"removed cover from Greek
  staircases"*, *"hangar assets no longer provide cover — suspended sections were providing cover to
  units beneath them"*. **The recurring failure is that the cover a player sees and the cover the sim
  computes drift apart.**
  → **This is the one failure mode a turn-based grid largely immunises us against**, and it is a real
  argument for §5.11's stage choice: a cell either has the works or it does not, and the player can
  see which.
- **Blobbing, named by Relic**: HMGs *"currently are not powerful enough to de-incentivize blobbing"*
  (suppression radius 13 → 15, recovery +3 s). Our field cap (decision 5) already bounds this
  structurally.
- **Cover too cheap**: *"Sandbags are receiving a significant increase in their build-times as it was
  too easy to lay down heavy cover, even when the squad was under fire."* Decision 14 already prices
  a build in a unit's action **and** materials — two costs where Relic needed to add one.
- **Per-squad vs per-model ambiguity** produced its own bugfix. We do not have squads on the board —
  one actor, one cell — so this does not arise.

#### Addendum — three findings from Relic's patch history that change a recommendation

**1. ⭐ Accuracy-cover and damage-cover are not interchangeable, and this settles §5.18's fork.**
A CoH2 rebalance mod moved heavy cover from `×0.5 damage` to `×0.9 damage / ×0.35 accuracy`, and
stated why: *"light cover will no longer alter shots required to kill a squad entity, while green and
garrison cover still will."* **Cover as a damage multiplier changes shots-to-kill; cover as an
accuracy multiplier does not.** That is an independent, shipped confirmation of §5.18's
recommendation — a dodge-only cover value keeps breakpoints stable, which is exactly what a
turn-based game with visible numbers wants.

**2. ⛔ Do not build auto-cover-seek pathing.** Relic shipped it and then spent five patches removing
it: *"Infantry will no longer prefer to take paths with denser cover distribution, which has often led
to unpredictable behaviours"* (1.3.0); no sliding into cover mid-aim (1.3.0); *"will not pick a second
cover spot if it moves them further from combat"* (1.7.1); *"will more often focus fire instead of
looking for cover when too close"* (2.3.0); and cover entry was silently costing DPS until *"Units in
cover no longer have an additional firing delay"* (2.4.0). **Cover should be somewhere the player
decides to stand, never somewhere the pathfinder drifts to.** This binds §5.16's AI rules.

**3. The illegibility mechanism, quantified: 18 cover states, 3 shield colours.** A CoH2 weapon's
`cover_table` has eighteen entries; the HUD shows three. And cover is evaluated **per model** while
the shield is shown **per squad** — which is the source of every *"I was in green cover and got
shredded"* report. Relic needed dozens of patch lines for bridges, staircases, hangars, balustrades,
crates, craters, Goliaths and *buried invisible objects*, and the lesson generalises: **if cover comes
from world objects, its class must derive from the same source as the visual, or the two drift.**

> **We are structurally immune to all three, and it is worth knowing why.** Decision 3 makes the board
> the sector zoomed in, so **the structure *is* the cover source** — one object, one class, one
> sprite, no drift. There are no squads on the board (one actor, one cell), so the per-model/per-squad
> mismatch cannot arise. And a turn-based grid means a cell either has the works or it does not. The
> remaining obligation is §5.18's rule 5: **show the number.**

### 5.18 The obstacle vocabulary — four kinds and one building

Enrichment pass, 2026-09-04. Seventeen named historical works collapse to **eight verbs**, and the
eight verbs collapse to **four obstacle kinds plus one building**. Each row below exists only because
cutting it removes a decision no other row can produce.

**The verbs:** BLOCK · SLOW · BLOCK-LOF · COVER · DENY · CHANNEL · CONCEAL · **BITE** (damage on
entry — added because the historical set does not close without it; a minefield does not slow you, it
hurts you).

#### The vocabulary

| # | Kind | Verbs | Material | Mechanically | The decision it creates |
|---|---|---|---|---|---|
| **1** | **Trench** | COVER | bulk | **Occupiable and passable.** Grants the occupant a flat `combat.dodge.*` delta. Two tiers by value (sandbag / revetted) | *Where is it worth standing still?* |
| **2** | **Rampart** | BLOCK + BLOCK-LOF | bulk + a little ironwork | Not occupiable. Blocks movement and fire through the cell. Destructible — razing it is a legitimate attacker action | *Which routes exist at all?* |
| **3** | **Wire** | SLOW | bulk, cheapest | Neither blocks nor covers. **Multiplies the stamina cost of entering the cell** | *Is the short route worth the stamina?* |
| **4** | **Mine** | BITE + DENY | ironwork | Damage on entry, single-use, unrevealed to the other side. **Ignores cover** | *Open ground or covered ground?* — **the only obstacle that punishes the safe-looking cell** |
| **—** | **Emplacement** | COVER + a weapon | ironwork | **A building, not an obstacle.** Garrisoned (decision 15), acts through its occupant, who gets high cover plus a ranged action | *Is a body better spent shooting or standing?* |

⭐ **`CHANNEL` is deliberately not a kind.** Every source describes channelling as an *emergent
consequence* of placing BLOCK and SLOW next to a weapon — dragon's teeth have no "channel" property;
they are staggered concrete, and the anti-tank guns behind them are what the channelling is *for*. If
BLOCK, SLOW and pathing exist, channelling is what the **player** does with them.

#### What is cut, and why — explicitly

| Cut | Merged into | Why |
|---|---|---|
| Parapet · parados · revetment · fire step | **Trench** | Construction details of one object. Parapet/parados are only distinct with **directional cover**, which needs facing — nothing in `BattleActorSetup` or `EntityFacts` carries one |
| **Traverse / fire bay** | *deleted outright* | Its entire job is defeating **enfilade**. On a turn-based square grid with per-cell damage resolution, **enfilade does not exist.** The cleanest cut in the set |
| Communication trench · sap | **Trench** | A trench you can walk along *is* a trench — rule 1 is already "occupiable and passable". Zero added mechanics |
| **Abatis · dragon's teeth · Czech hedgehog · tank trap** | **Wire** | Four names for one verb. They differ historically by *which vehicle class* they stop, and **we have no unit size or type classes.** Four kinds producing one decision is exactly the second vocabulary §2 rule 10 forbids |
| Moat / anti-tank ditch | **Rampart** | A cell you cannot enter and cannot stand on **is** a wall. Identical verbs |
| Sandbag emplacement | **Trench, tier 1** | A tier on an existing kind |
| Pillbox | **Emplacement** | Same thing in concrete |
| **Dugout** | *deferred* | Its distinctiveness is CONCEAL and *damage-source-specific* cover. Fog is **map-scope only** today (`world-intel`), so revisit after fog exists |
| Minefield | **Mine** | Area is a placement *pattern*, not a second kind |
| **Smoke** | *not an obstacle at all* | A temporary conceal effect, and the effect-atom layer already owns temporary area effects. (Worth knowing how strong it is in CoH3 — accuracy **×0.25**, suppression **×0.05**, the largest modifier in the game — and it is still not cover) |

Four is defensible as a **floor**, not a compromise: CoH3 ships five live cover types plus four
declared-and-inert; Wesnoth ships ~12 defense terrains.

#### Three things this board already has

Verified against code, not comments:

| Needed | Status | Evidence |
|---|---|---|
| **Cover as a contest modifier on the occupant** | **Already expressible** | `BattleStatComposer.cs:116-117` writes `CombatAccuracyOmni`/`CombatDodgeOmni`; `OverlayCombatCalculator.cs:162-164` resolves `accuracy − dodge` through a sigmoid |
| **Block line of fire** | ⭐ **Pure wiring gap** | `RequiresLineOfSight` is declared (`ActionRow.cs:49`), compiled (`ActionCompiler.cs:65`), carried (`CompiledAction.cs:37`), persisted twice (`RpgStore.Actions.cs:256`, `:373`) and hardcoded `false` in the battle fallback (`BattleRunState.cs:61`) — **and read by no evaluator anywhere in `src/`.** Verified 2026-09-04 |
| **Movement costs stamina** | **Built** | `Actions/Cost/ActorResourcePools.cs:5-12` |

⭐ **The contest scale is already fixed, so cover values are choosable today.**
`BaseAccuracy(Θ) = 220 + 26·Θ` and `BaseDodge(Θ) = 26·Θ` (`BattleModels.cs:171-172`), with
`accuracyScale: 100.0` — so 100 contest points is one sigmoid unit, and **+50 dodge is half a unit.**
Because both sides' `26·Θ` terms cancel in the `accuracy − dodge` difference, **a flat cover value
stays exactly as decisive at Θ=200 as at Θ=1.** That is precisely what §2 rule 4 demands: cover is a
**contest**, so it is linear and flat — never `P(Θ)`, never a new `f(level)`.

#### Five rules the numbers support

1. **Do not add passive dig-in that grows with turns stationary.** Decision 14 already makes *build* a
   peer of move and attack **and prices it in materials**. Free entrenchment would be a second,
   unpriced path to the same bonus — and it lands on two documented failures at once: Civ's
   *"fortify is always correct, so it is not a decision"*, and **Panzer General's own manual
   prescribing a scripted five-step grind** to strip entrenchment at deliberately unfavourable odds.
   Every mature implementation bounds it anyway (PG base+5; HOI4 cap 5 at +2%/level; Civ IV +25%;
   Civ V +50%). **Making it an action gives us the bounded version for free.** The shipped
   `defenderBonusMilli: 1250` — coincidentally identical to Civ IV's +25% — should then shrink toward
   nothing, per §5.8.
2. **Beat cover with a damage *type*, not a bigger number.** The most consistent finding across three
   independent games: CoH3 flame is **×1.25 damage into green cover and ×1.5 into a garrison**;
   Foxhole trenches carry **97% HE mitigation** but are *"resistant to all damage types **except
   Demolition**"*; Panzer Corps 2 engineers **ignore 50–100%** of entrenchment. **We already have an
   element hub** — *fire ignores trench cover* is a one-row rule that makes composition matter and
   cannot be brute-forced. Without it the trench-warfare fantasy becomes the stalemate it is named
   after.
3. **Terrain should be a matchup, not a modifier.** Wesnoth's forest is **30 for an elf and −70
   (capped) for cavalry on the same hex**; Fire Emblem fliers get Fort/Gate/Throne and nothing else.
   The alternative is Advance Wars' documented outcome — its own wiki says heavy terrain is *"always
   preferred"*, because the bonus is universal and free. **Our lever is better than a unit-class
   enum**: demons have elements, traits and aptitudes, so cover magnitude can key on those.
4. **If cover decays, decay it with the occupant's condition, not with turns.** Advance Wars scales
   terrain defense by **current HP** — a 5 HP unit in 2-star woods gets 10%, not 20% — so a fortress
   stops being one exactly when it is most needed. **We have a better hook and it needs no new
   mechanism**: stamina/hunger exhaustion already debuffs derived stats and is re-evaluated on read.
   It also closes the loop with decision 13's *"block their resource and exhaust them."*
5. **Show the cover contribution on the wire.** XCOM's two most-cited problems are both legibility —
   the 95%-miss perception gap (developer-acknowledged) and per-difficulty aim assist that is never
   surfaced. GG-55 points the same way, and `BlockedTarget.tsx` / `blockedPlacement.ts` are built and
   inert — the pattern to copy for *"this shot is at −40 because the target is in a trench."*

#### One fork, named not decided

**Is cover one number or two?**

- **One number (dodge only)** — what Wesnoth, XCOM, Fire Emblem and Advance Wars all do. A pure
  contest, linear, satisfies §2 rule 4 with no argument, **expressible today with zero new channels.**
- **Two numbers (accuracy × damage)** — CoH3's model, and why green cover is **4× survivability**
  while yellow is 2×. But `combat.reduction` is a **magnitude** read, so a damage-side cover value
  must justify itself against `P(Θ)`, and a flat one would decay to irrelevance as Θ grows.

**Recommendation: one number.** It gets both tiers by *value* (trench +40, emplacement +80) rather
than by mechanism, and it cannot drift onto the magnitude ladder by accident. CoH's two-axis model
exists mainly to make suppression a third axis — which we do not have.

**One CoH3 idea to adopt regardless:** cover is a **matrix of (damage source × cover type)**, not a
scalar on the defender. That is what makes rule 2 expressible at all, and it is a *data shape*, not a
mechanism — a table in `data/tuning/base-defense.v1.json`.

### 5.19 How building works — instant on the board, accumulated on the map

Enrichment pass, 2026-09-04.

#### What a `build` order does today, and the three things that surprise

`BuildResolver.cs` is a complete, tested vertical slice — ten refusal gates, debit at `:115`, site
written at `:124`, resolving in `Snapshot` so it can see a claim that landed the same turn. And
`build` **passes all five plumbing sites**, unlike `bind-warden` — a new order kind inherits a working
reference implementation rather than a hunt.

Three properties matter to decision 14:

1. **Build costs no action and no movement today.** `BuildResolver` never touches
   `MovementRemaining`; movement resolves in `Movement` (phase 3), build in `Snapshot` (phase 8). A
   legion marches its full budget **and** builds, free.
2. **There is no per-entity order cap anywhere.** `Reveal` dedups nothing. The only ceiling is
   `MaxCommandsPerSubmit = 200`. One legion may file 200 builds in a turn.
3. **The world layer has no action economy at all.** The only "spend your turn to fortify" mechanic is
   the `Hold` stance — `BudgetFor(Hold) => 0`, buying `DefenderBonusMilli = 1250`.

#### The recommendation: two different acts, stop pretending they are one

> **Instant on the board. Accumulated at world scope.**

**In-battle build resolves instantly, at full HP.** Every turn-based game that actually ships
in-battle construction does this — Field of Glory II emplaces stakes in one AP allowance, Civ VI's
Builder finishes an improvement in one turn, HOMM3's *"Your town immediately benefits from it."* And
it is the only option that composes with decision 6: **batch waves already give the defender a
between-waves rebuild window**, and a multi-turn build on a board whose waves resolve in a few turns
would complete *after* the wave it was meant to stop. **A trench you finish next turn is a trench you
did not dig.**

**World-scope build stays accumulated**, because it already is, correctly — `ConstructionTurnsRemaining`
is shipped, hashed, persisted, spec'd and tested. Nothing here touches it.

**The tradeoff, named:** instant construction removes "will it finish in time." Buy that tension back
by making the build action **cost the whole turn** — Field of Glory II and Unity of Command both do
exactly this (*"Emplacing stakes uses the unit's entire AP allowance"*; UoC dig-in costs full AP **and**
full MP). That charges **tempo**, which decision 14 already locked as the economy, instead of inventing
a second progress system beside the world's.

⭐ **The survey's most useful negative result:** across Combat Mission, Fire Emblem, Advance Wars,
Wesnoth, XCOM, Jagged Alliance and Panzer Corps, **not one lets a unit build a structure on the field
as an ordinary turn action.** Field of Glory II's stakes and Advance Wars: Days of Ruin's Rig are the
only two exceptions found, and **both charge the unit's whole turn.**

#### Builder killed mid-build → total loss, no refund

Three shipped answers disagree instructively: the DoR Rig **restarts from scratch**; Total War
Warhammer III **destroys in-progress construction when you lose the capture point funding it**; SC2/WC3
refund **75%** — but only on a *voluntary* cancel.

**Total loss needs no new mechanism.** `ActionRunner.Interrupt` already cancels every outstanding hit,
and `InterruptRefundMilli` is per-envelope — set it to zero. It also matches the world layer's own
decided position: *"A half-built waystation is not a refund, it is exactly the loss G1 warns the player
about"* (`spec-loam-structures.md:123`). Board and map say the same thing.

⭐ **The Warhammer III variant is worth stealing deliberately**, because it *is* the trench-warfare
fantasy in mechanical form: §5.15's board income already makes nodes contested every turn, so **cutting
the route to a node should invalidate the work it was funding.** That gives obstacles and pathing a
consequence beyond walking distance.

#### Capture is occupation — confirmed by two shipped precedents

Decisions 12 and 15 already settle it. Two games confirm the shape rather than it being a shortcut:
Advance Wars: Days of Ruin's **temporary airports can be captured by the enemy**; and **Civ VI's Fort
gives `+10 Defense Strength` to whichever unit stands on it, regardless of who built it** — decision 12
verbatim, from a shipped 4X.

**And the rule that keeps it honest is now a build constraint, not a principle:** the board never reads
`WorldSlot.OwnerFactionId` — because `ClaimResolver` leaves it stale on capture (§7.6), that field is
*actively wrong*, and reading it would import a known bug.

#### Repair — proportional, because it is the only model that satisfies P2

§5.14 already flagged this as load-bearing: *"without it, fortification is a one-time cost and the
faucet outruns it."* Two shipped pricings:

| | **Flat** (Warcraft III) | **Proportional** (AoE2, Civ VI) |
|---|---|---|
| Cost | **35% of build cost**, 1 HP → full | **half the build cost × fraction of HP restored** (AoE2); **25% of original production** (Civ VI) |
| Time | **150% of build time** | one Builder turn / rate-based |
| Incentive | repair only when wrecked — a scratch costs the same | repair continuously; every point of damage has a price |
| Satisfies P2? | ❌ a step function — skipped below a threshold, identical above | ✅ **scales with how much fighting the empire absorbs** |

> **Recommendation: proportional.** `repairCost = buildCost × repairRatioMilli/1000 × (hpRestored /
> hpMax)`, `repairRatioMilli` seeded at 500. **Repair is world-scope, not board-scope** — §2 rule 7 is
> absolute, so damage comes back in the outcome record and the *world* prices it. Widening
> `BattleOutcome` with a per-slot damage list **moves no golden** (neither hashed nor persisted).
>
> **Its cost:** proportional repair needs structure **HP**, which does not exist — `SlotState.Ruined`
> and `Depleted` are declared and **never written anywhere in `src/`**. Flat repair could ride a binary
> damaged flag and ship cheaper. Still recommend proportional, because that flag would be replaced the
> moment towers need HP for combat anyway, which §5.13 already commits to.

#### One lever for the map, worth more than any cost balancing

**HOMM3: *"Only one structure per day may be built in each of your towns."*** One line, instantly
legible, costs nothing to compute — and it makes **which** thing you build the entire decision. Today
our map build is unbounded. A per-sector-per-turn build limit is a one-line tunable.

#### Five corrections found in this pass

| Finding | Evidence |
|---|---|
| **`StructureDef.Name` has no reader outside its own validator** | Nothing in the game or web UI can name a structure |
| **`SlotTypeDef.Buildable` is never consulted on the build path** | Admission and `BuildResolver` check `RequiredSlotKind` only. `hazard` is `Buildable = false` and only safe because no structure requires that kind |
| **No slot-level counterpart to `Rule16`** | A `WorldSlot` with `ConstructionTurnsRemaining` and no `StructureId` passes validation — despite `Rule16`'s comment claiming to mirror a rule that does not exist |
| **A whole second construction system is dead** | `WorldSector.ProjectId` / `ProjectTurnsRemaining` have **zero originating writes**; `TurnEngine.Growth` is `return world;`. Declared, hashed, persisted, validated, entirely unreachable |
| **SC2 buildings under construction have _no_ damage mitigation** (first-party Blizzard) | The widely repeated "10% starting HP" traces to *Brood War* and could not be verified for SC2 — do not cite it |

### 5.20 Targeting — the vocabulary, and why legibility beats optimality

Enrichment pass, 2026-09-04. This sharpens §5.16's R3/R4 with shipped vocabularies.

#### The canonical four, and why the default is what it is

BTD6 ships **First · Last · Close · Strong**, and **First is the default** — because it is *the greedy
solution to the loss condition*. Lives are lost when something reaches the exit; target whatever is
nearest the exit.

⭐ **`Strong` is not a standalone rule — it is a filter on top of `First`**: ties break toward the bloon
closest to the exit. **Every priority resolves to a total order, never to "no target."** That is the
single most transferable detail in the whole survey.

**And a unit whose geometry breaks the vocabulary gets a _replacement_ vocabulary, not a degraded one.**
The Mortar has no standard priorities at all — only *Set Target*. The Heli gets *Patrol/Pursuit*. The
Spike Factory gets *Smart*. A unit forced into a vocabulary that does not fit its geometry is the
second-largest source of stupid-looking behaviour.

#### Two mechanisms worth taking outright

**Clash of Clans' `Favourite Target`** is a **named, player-visible validity filter** on every defense —
Air Defense: *Air*; Mortar: *Ground*; Archer Tower and Inferno: *None*. The player can say **why** it
did not shoot **before** they watch it not shoot. Its documented misses are all *features* because the
rule producing them is stated: the Mortar's 4-tile dead zone, its projectile lead failure against fast
troops, the Inferno's ramp reset on retarget.

**Arknights' block-as-override and signed aggression.** *"Blocked enemies are treated as being within
the blocking unit's range, **regardless of whether that is the case or not**"* — block is a targeting
override, not merely a movement stop. And aggression is a **signed scalar (+2 … −2)** inside a
published five-level priority chain, which gives **taunt, stealth and decoy one mechanism instead of
three**. Retarget latency is specified rather than instant: a search cycle every 3 frames, and attack
animations complete even if the target leaves range.

#### The design literature says legibility, not optimality

**Into the Breach**, Justin Ma, first-party: *"We wanted to make something where **every death felt like
your own fault.** This lead us to use of telegraphed enemy attacks as a core mechanic"* — and *"When
every enemy attack is telegraphed and there's no random chance in your attack options, the game starts
to feel like a puzzle."* ⭐ **Subset did not make the AI smarter so it would stop looking stupid. They
made it fully visible, at which point "stupid" stops being a category the player can apply.**

**Damian Isla, *Handling Complexity in the Halo 2 AI*, GDC 2005** — the requirement, stated as a hard
constraint on the architecture: *"given the AI's outward stance, it must be possible for the untrained
observer to make reasonable guesses as to the AI's internal state as well as explain and predict the
AI's actions."* And the failure mode named exactly: *"a murky experience for the player in which the
AIs seem to act **'randomly' rather than 'intentionally'**."*

Isla also gives the architectural rule for any reactive retarget: a stimulus behaviour must be
*"dynamically added by an event-handler to a specific point in the tree"*, because *"only by placing the
stimulus behavior into the tree itself can we be assured that all the higher-level and higher-priority
behaviors have had their say."* **A retarget hook goes inside the priority order, never on top of it.**

**Pac-Man** is the minimum viable case: one shared pathfinder, **four different target-tile functions**,
four legible personalities. And **Clyde is deliberately suboptimal** — he breaks off within eight tiles
— which is precisely what makes him read as a character rather than a bug.

#### The five-rule minimum

Every system surveyed has all five; the most-praised ones (Pac-Man, Into the Breach) have *only* these.

1. **A total order over valid targets, with a documented tie-break.** A rule that can return "no
   preference" is the rule that produces the stupid-looking frame.
2. **A validity filter that is named and visible** — CoC's `Favourite Target`.
3. **A retarget trigger on target loss, with a _stated_ latency.** Instant is not required; *specified*
   is.
4. **An override channel inside the priority order** — Arknights' block and signed aggression, Isla's
   in-tree stimulus. Taunt, stealth, decoy and forced-target all live here as one mechanism.
5. **A replacement vocabulary for units whose geometry breaks the standard one.**

⛔ **And player-configurable targeting is _not_ on that list.** Kingdom Rush ships **no** targeting
control at all and is a genre benchmark; what it has instead is a rule a player can state in one
sentence (*"closest to the exit"*) plus placement as the control surface. **Configurability is a
convenience; _statability_ is the requirement.** Worth weighing before any targeting UI is designed
for the siege board.

### 5.21 The planet-scale node — roles, capacity, and trade

Enrichment pass, 2026-09-04, for decision 19. **Scope note: this belongs to `sector-development` plus
an economy program, not to base defense** (§0 decision 19). Recorded here as provenance.

`docs/research/genre-mechanics/` has **no 4X or city-builder file** — everything here is new to the
repo.

#### Two wiring gaps that decide most of this

| Gap | Evidence | Why it matters |
|---|---|---|
| **`LoamUpkeep` has no structure term** | `Breakdown(garrisonMembers, developmentLevel, dangerBand, intensityMilli, handicapMilli, seasonMilli)` — six terms, none of them structures. Its own comment: *"No structure term yet — structures arrive in `loam-structures`, wave 4"* | **A sector with 20 buildings pays the same upkeep as one with none**, except indirectly via `DevelopmentLevel`. This is the exact line where §5.14's required P2 sink is missing |
| **⭐ `WorldLane.Width` and `WardLevel` are the logistics substrate, shipped and unread** | Both declared (`WorldState.cs`), hashed (`WorldCanonical.cs:55-56`), persisted (`RpgStore.World.cs:288-289`), read back (`:485`), wire-projected (`WorldEndpoints.cs:685`) — and **read by no gameplay code.** `HazardMilli` *is* read (`LaneCost.cs:137`) | Throughput-limited, interdictable logistics is a **wiring gap, not a new capability.** `Width` is capacity; `WardLevel` is protection. Both already replay |

#### Eight economic roles — and the one worth arguing for

§5.15 filtered the *combat* taxonomy to defend/see/deny. A planet-scale *economic* node asks a
different question, and the answer is verbs:

| Role | Ours today |
|---|---|
| **R1 Extract** — geography → stock | ✅ `LoamSource`. But `shard-vein` ×4 and `material-seam` ×3 yield **zero** |
| **⭐ R2 Refine** — one stock in, another out | ❌ **Missing, and it is where `ironwork` belongs** |
| **R3 Multiply** — raise another producer's yield | ✅ `YieldMultiplierMilli` — one row uses it |
| **R4 Store** — capacity per stock | ✅ granary, +300, one stock |
| **R5 Move** — throughput, range, protection | ⚠️ half — waystation gives *range*; nothing gives throughput or protection |
| **R6 Bank** — the Tier-2 faucet | designed (soul conduit), unbuilt |
| **R7 Defend / See / Deny** | ❌ this program |
| **R8 Enable** — gates *what may be built here* | ❌ missing — the district tier |

**Cut, beyond the five already rejected in §5.15:** housing/population (we have **no pop system**;
`LoamUpkeep.Garrison` already taxes headcount, and a housing building would be a second population
system) · amenity/happiness (`StabilityMilli` and the fade loop already do this; a stability building
is a converter needing P5 pricing for a benefit already delivered) · research building (no
world-scoped research stock) · trade-value generator (§6 forbids a loam market; the role we want is
R5) · culture/faith/influence (no such stock, and P7 caps the decision layer at 3–4 headline
quantities — we are already at five).

> ⭐ **R2 (Refine) is the strongest addition, and it pays for itself twice.** If `ironwork` is
> *produced from* bulk material at a lossy, gated rate (P5), then `min(bulk, ironwork)` binds **and**
> the two stocks are coupled by a build decision rather than by two independent deposit rolls. That is
> Anno's steel chain doing exactly what P4 asks. It also gives ironwork a **non-defense sink**, which
> is what stops it becoming "the wall currency" and satisfies P6.
>
> **And it may cancel a cost §5.14 already budgeted:** if ironwork is *made* rather than *mined*,
> `shard-vein` can stay a **bulk** deposit — so the rename that would move **every world golden**
> (`WorldCanonical` writes the slot type id as a string) **may not need paying at all.**

#### Capacity: price it, do not cap it

The prior art splits cleanly. Slot-capped: Civ VI (**1 district per 3 pop**, **3 buildings each** ≈
12–18), Stellaris (**21 buildings**, districts = planet size 12–25). Uncapped, priced instead:
Endless Legend (**150 × n**, linear and unbounded), ES2 (**~1.4% of build cost per turn** in upkeep),
Anno (island land).

**This repo already picked a side and won the argument once:** `ContractPolicy.MaxSlots = 48` was
deleted because *"the escalating price was always the real scarcity control"*, and `AGENTS.md` makes it
binding — *"a cap on a magnitude is removed or made a configurable soft cap."* **A "12 buildings per
sector" const would be the fourth ceiling a sweep has to find.**

**Recommendation: no slot const. Price capacity three ways** — escalating build cost (EL's `150 × n`;
our own `300 × (n+1)`), **per-structure upkeep** (the missing `LoamUpkeep` term), and **geography**
(`RequiredSlotKind` already ships, so a poor sector cannot reach the ceiling regardless of wealth —
this is Stellaris's deposit rule, and it is what keeps sectors different).

**Target equilibrium ~8–14 built things at a rich sector, 2–4 at a marginal one.** Chosen from Civ VI's
12–18 trimmed for our scale, and validated against our own numbers: 18 × 12 ≈ 216 objects against an
outliner already declared to need grouping at **~28 rows**. Stellaris survives 21 × 30 only because it
ships designations and auto-build — **past ~14, delegation stops being optional.** Catalog size ~24–40
types (8 roles × 3–5), against AoE2's 40. Today: **4 rows.**

⚠️ **Upkeep should key off built capacity, not current output** — so a mothballed building still costs
and "build it and forget it" is never free. That is the 500-hour test applied to buildings.

#### Districts vs buildings — two tiers, and the repo already half-wrote it

`spec-sector-development.md:124-128` already says it, quoting the world ideal: *"slot buildings develop
one slot's output; sector projects raise the whole sector."* This is a wiring question, not a new
concept.

⭐ **Why they are genuinely two concepts: they are limited by different things.** District count =
**geography you conquered**; building slots = **development you invested in**. And **the coupling runs
one way — the district is what unlocks the building slot.** You cannot buy quality without first
buying quantity, and quantity is capped by land you had to take. That is the sharpest structural idea
in the genre.

Two cheap mappings onto shipped state: **(a) grow slots** with `DevelopmentLevel` (Stellaris shape —
but every new slot is a geography row to author, and it *dilutes* geography), or **(b) depth per
slot**, 2–3 structures stacked, legality gated by `SlotKind` plus the sector's project (Civ VI shape —
one field, *concentrates* geography, and gives tier chains a natural home). **Recommend (b) plus
modest slot growth** — ~5–6 slots × 2–3 depth ≈ 10–18, inside the band above. Today's mean is
**2.6–3.5 slots × 1**.

**On adjacency: do not copy Civ VI's directly.** Our slots have `SlotIndex`, an ordinal, and no
geometry — real adjacency needs coordinates, a new hash surface and a new render. **A co-location
bonus within a sector** delivers most of the decision for none of that, and note Civ VI's own minor
tier is literally *"any district +0.5"* — much of its adjacency value is **density, not position**.
Also useful precedent: **Civ VI's Encampment has no adjacency at all** — the defense district is
exempt, and ours can be too.

#### Trade — and the genre's own trajectory is the finding

§6 names four things a logistics answer needs: **route, timing, capacity, risk.** Today exactly one
exists — `SupplyReach` is a boolean BFS.

⭐ **Stellaris shipped route-based trade with piracy for six years and deleted it.** Patch 4.0: *"The
Trade Routes system has been removed"*, replaced by *"planets now have logistical upkeep paid by Trade
based on their local resource deficits"* — **distance-and-position-priced upkeep with no routing UI at
all.** That is strong evidence against making a convoy UI the primary mechanism at 18 nodes.

**Recommendation:**

| Stock | Mechanism | Why |
|---|---|---|
| **Loam** | **connection only**, unchanged | It *is* the anchoring field. Shadow Empire's zero-weight class does exactly this |
| **Bulk + ironwork** | **throughput on lanes** (`Width`, weakest-link = `min(Width)` along the path — HOI4's rule) **+ deficit-priced upkeep** (Stellaris 4.0) **+ interdiction** (`WardLevel`) | All three ride shipped, hashed fields. **A blockade finally has something to bite on at world scale** — the same gap §5.15 fixed at board scale |
| **Convoys / bearers** | **opt-in, not primary** | Copy Anno's Charter split verbatim: manual routing is cheaper, automatic costs more |

**External trade with neutral clans** (permitted for non-loam stocks under P5): **ES2's marketplace is
a ready-made lossy mechanism** — a shared order book where *"purchase and sale influences prices in
the entire galaxy"*, so the rate-cap **emerges from the mechanism** rather than being bolted on, which
is what P5 prefers.

#### Storage — two non-obvious consequences

1. **⚠️ A shared storage pool would silently undo P4.** If bulk and ironwork share one capacity number,
   the plentiful stock crowds out the scarce one and the `min(bulk, ironwork)` bottleneck collapses
   into a queueing problem. **Per-stock capacity**, extending what the granary already does.
2. **For construction stocks, _halt_ beats _waste_.** Loam should keep wasting — that is the throttle.
   But Anno's model fits a construction stock better: production stalls, nothing is destroyed, **and
   the stall is a free diagnostic.** It also matches `AGENTS.md`'s own stance that a silent clamp *"is
   a bug with no symptom."* Whichever is chosen, **state it in exactly one place** — Frostpunk's own
   wiki contradicts itself on this rule across two adjacent pages.

**A free win available today:** `LoamForecast.cs:56` already computes `room = EffectiveCapacity −
LoamStock`. The forecast can say *"you will waste N loam this turn"* with **no model change**.

### 5.22 Round 5 — the four closing decisions, and what follows

#### Decision 23 dissolves a problem rather than accepting one — and my recommendation was wrong

I recommended **instant on the board**, arguing *"a trench you finish next turn is a trench you did not
dig."* The owner chose **accumulated everywhere**, and it is the better answer for a reason the
recommendation missed:

⭐ **A battle resolves inside one map turn, but a siege spans many.** `world-graph-ideal.md:207`
already fixes this: *"Combat is stateless between turns. A **multi-turn siege is therefore a fresh
engagement each turn, built from world-held state**, not a battle left paused in memory."*

So board construction accumulates over **map turns**, never over battle rounds — and the world holds
the construction state between engagements. **A trench finished next turn is a trench that helps in
next turn's engagement**, which is exactly what a multi-turn siege is made of. The objection assumed a
siege was one battle; it is not.

**Consequences, all of them favourable:**

- **One mechanism, not two.** `ConstructionTurnsRemaining` already does this — shipped, hashed,
  persisted, spec'd and tested. The board needs **no construction system of its own**.
- **The half-built loss rule transfers for free** — *"A half-built waystation is not a refund, it is
  exactly the loss G1 warns the player about"* — and so does §5.19's Warhammer III variant: cutting the
  route that funds a work should invalidate it.
- **`LoamPhases.cs:195` already destroys in-progress construction** when ground is lost. That is the
  only structure-removal path in the codebase, and a siege now has a legitimate second one.

#### Decision 20 — the whole city, and the frame that actually implies

Every economic building becomes a board object and a garrison point. Two honest consequences:

1. **The genre reference shifts from the Somme to Stalingrad.** Trench warfare among buildings is
   *urban* trench warfare — and that is coherent, not a contradiction: the four obstacle kinds (§5.18)
   are unchanged, they simply sit between structures instead of on open ground. Rampart and wire
   channel movement *through streets*; the emplacement becomes a fortified building, which is what a
   pillbox always was.
2. **Board size scales with development, and so does turn length.** §5.21's equilibrium band (~8–14
   built things at a rich sector) is therefore not just an economy number — **it is the board budget.**
   The two must be tuned together, and going past ~14 makes both the outliner and the siege unwieldy at
   once.

#### Decision 21 — grow slots, and the cost it confirms

`SlotIndex` is already an `int`, so the model bends without a schema fight. Two costs, both now firm:

- **Every new slot is a geography row** to author or roll. Hand-authoring stops at `medium`
  (`WorldSizeCatalog`: *"a `large` map is ~1000 lines"*), so this makes **`world-generator` more
  load-bearing**, not less.
- ⚠️ **The full-graph-rewrite trigger is now unambiguously met.** `RpgStore.World.cs:210-212` —
  *"revisit if a world ever reaches hundreds."* Growing slots multiplies rows rather than adding a
  field to existing ones. **A diffing writer is a prerequisite, not a follow-up.**

**Mitigation for the dilution risk:** keep `RequiredSlotKind` gating hard. More slots must not mean
more *useful* slots — a poor sector should grow ground it cannot exploit, which is Stellaris's deposit
rule and is what keeps sectors different.

#### Decision 22 — halt, and the field that already exists for exhaustion

The owner's reasoning is the load-bearing part: **halt rather than waste _because a deposit can be
exhausted_**, so discarding extracted material is a double loss.

⭐ **`WorldSector.DepletionMilli` is that field, and it is a real gap with the state already carried.**
Declared (`WorldState.cs:134`), hashed (`WorldCanonical.cs:43`), persisted (`RpgStore.World.cs:240`),
read back (`:445`), wire-projected (`WorldDtos.cs:122`) — and **no writer in Core.** The server says so
in its own comment (`WorldEndpoints.cs:444-445`): *"`DepletionMilli` stays zero — nothing in Core
writes it; it remains a real gap, not a wiring one, and is left alone here rather than invented."*

**So exhaustible deposits need a producer, not a schema.** The state hashes and replays already.

**The notification requirement has a home too.** `world-stage`'s `world-notify` module is specced —
two notification classes, a passive right rail, flush on End Turn except blockers, per-category channel
settings changeable *on the notification*. A storage-full warning is one category.

**And the number already exists:** `LoamForecast.cs:56` computes `room = EffectiveCapacity −
LoamStock`. *"Storage full next turn — build a granary"* is available **with no model change**.

> **One rule this makes non-negotiable, from §5.21:** storage must be **per stock**, never pooled. With
> halt semantics and a shared pool, a plentiful stock filling the pool would stall an unrelated scarce
> one — turning P4's `min(bulk, ironwork)` bottleneck into a queueing bug.

#### Taken on recommendation, unopposed — say so if any is wrong

| Recorded | Basis |
|---|---|
| **Cover is one number — a flat dodge delta, never `P(Θ)`** | §5.18's contest argument, independently confirmed by a shipped CoH2 rebalance: damage-cover changes shots-to-kill, accuracy-cover does not |
| **Auto-resolve runs the same kernel** via `intentSource`, not a separate estimator | §5.16 — the base *is* the missing term a cheap model cannot see |
| **Repair is proportional** — `buildCost × ratio × (hpRestored / hpMax)` | §5.19 — the only pricing that satisfies P2 |
| **The bulk material is `rubble`** | Zero collisions; Fracture-native; literally what fills a revetment. `stone` and `metal` are refused — 29 and 49 file collisions |
| **No fifth `WhoKind` yet** | §5.13 — add it when trench cover needs a positional population, not before |
| **Legion slots and field-cap values stay unset** | Tunables by decision, so choosing them is a balance pass, not a design gate |

### 5.23 Round 6 — the turn/round boundary, and the control point

#### Decision 24 restates a rule the repo already holds

The owner's reasoning — *"different turn for world map and each battle, explicit boundary"* — is
`world-graph-ideal.md:190`'s own vocabulary rule, arrived at independently:

> *"A map step is always a **turn**, a battle step is always a **round**. Never 'turn' for a battle
> beat in our docs, never 'round' for a map step."* And: *"**Never convert between them.** One turn is
> not N rounds… The moment a formula multiplies turns by rounds, the seam has leaked."*

**Batches are a battle-internal concept.** They cycle inside one engagement, against the field cap, and
never appear in world state.

**What this buys:** no new hashed state. A siege spanning turns needs nothing stored — both sides
survive (`BattleOutcome.Routed` keeps the field), both remain in the sector, and next turn is another
engagement built from world-held state, exactly as §2 rule 7 specifies.

**Finding recorded rather than rediscovered:** `SectorPhase.Besieged` is declared and referenced
**exactly once — its own declaration** (`WorldState.cs:13`), the same status as `Developed`. It should
**stay** unused: "besieged" is derivable from *a hostile force standing in a sector you own*, and
`spec-sector-development.md:144` already rejects phases that mirror derivable state as *"derived state
that rots"*.

#### Decision 25 — the control point, and the split it creates

The refinement is the second half: **garrisoning means taking control of something that _does_
something.** A wall has nothing to control. So a structure carries a **control point or it does
not** — a new property on `StructureDef`, and it sorts the whole vocabulary cleanly:

| Structure | Control point? | Ungarrisoned behaviour |
|---|---|---|
| **Rampart** (wall) | **No** | Always blocks. There is nothing to man |
| **Wire** | **No** | Always slows |
| **Mine** | **No** | Always bites |
| **Trench** | **No** | Always grants cover to whoever stands in it — *standing in* is not *operating* |
| **Emplacement** (tower) | **Yes** | Occupies, blocks, has HP — **fires nothing** |
| **Economic buildings** (mine, farm, refinery, market, storage…) | **Yes** | Occupies, blocks, has HP — **produces nothing** |

**Three consequences worth stating:**

1. **This is what §5.15's board income was already assuming.** Nodes *"yield per turn to whoever holds
   them"* — holding is having a body in the control point. Now the rule has a name and a field.
2. **An undefended city is a maze, not open ground.** Every structure is terrain before it is a weapon,
   so raiding an ungarrisoned city still costs an attacker time and routing. That is what makes
   decision 20's whole-city board meaningful even when the defender is thin.
3. **The obstacle/building line from §5.18 is now mechanical rather than descriptive.** Obstacles are
   exactly the control-point-less structures; buildings are exactly the ones a body can operate. Two
   categories, one field, no third vocabulary.

### 5.24 Four ways a structure reaches the board

**Owner decision 27, 2026-09-04.** Decision 14 originally said in-battle building *"costs world-map
building resources, never the six actor pools."* That was one path described as if it were the only
one. There are four, they cost different things, and **the difference is what makes a siege winnable.**

| # | Path | Costs | Time | Who it serves |
|---|---|---|---|---|
| **1** | **Built** | world materials — `rubble` / `ironwork` (+ loam) | accumulates over map turns | The **defender**, at home and in supply |
| **2** | **Assembled from a consumable** | the **item** | **immediate** | The **attacker** — a prefabricated work, carried in and deployed |
| **3** | **Summoned by a demon action** | **`qi`** | the action | Any actor with the right action |
| **4** | **Laboured** — digging a moat, throwing up a berm | **`stamina` / `hunger`** | the action | Any actor. **No materials at all** |

#### This is what answers the audit's sharpest economic finding

The economy audit computed that a besieging legion's entire construction budget is
`200·bearers − 10·members·turns` — **roughly one structure per siege**, against a board of 8–14. The
attacker was priced out of the trench-warfare fantasy the design is named after.

⭐ **Paths 2, 3 and 4 bypass the material economy entirely.** The attacker carries prefabs, spends `qi`
on summoned works, and digs with `stamina`. *"Some builds can support this rebuild step, so the attacker
can cover the disadvantage because building takes so long."* The defender's advantage stays real — they
alone can *build* the permanent, material-funded works — while the attacker's disadvantage stops being
absolute.

#### And paths 3 and 4 need no new economy whatsoever

This is the part worth checking against the resource hub rather than assuming, and it holds exactly:

- **`qi` is defined as *"Skill fuel — anything with a trigger, an element, or a container of atoms
  behind it."*** A summoned structure is a skill with a container behind it. Path 3 is an ordinary
  action.
- **`stamina` is defined as paying for *"Physical actions — move, basic attack, reposition."*** **Digging
  is a physical action.** Path 4 is an ordinary action.

So the *"new action sub-category"* is a **category of actions, not a category of economy** — and the
action layer already prices, gates and cools down actions through `ActionCostRow` over the six pools.

> ⚠️ **The one rule this must not break.** `empire-economy-ssot.md` §8 was narrowed this session to
> reject *"loam as a **seventh actor pool**"* while permitting a side-scoped battle budget. Paths 3 and
> 4 run the other direction — an **actor pool paying for a structure** — and that is legal for the same
> reason: no new pool is added, and the actor spends its own resource on its own action. **What stays
> forbidden is a structure's cost being denominated in an actor pool at *world* scope** (path 1), where
> there is no actor to spend it.

#### Consequences for decisions already made

| Decision | Effect |
|---|---|
| **P7 (3–4 headline stocks)** | **Eased.** Structures funded by `qi`/`stamina`/consumables add nothing to the headline stock count — they ride pools and inventory the player already reads |
| **Decision 25 (control points)** | Unchanged. *How* a structure arrived says nothing about whether it can be manned |
| **Decision 23 (construction accumulates)** | **Now path-specific.** Paths 2–4 are immediate by construction; only path 1 accumulates. §5.19's *"a trench you finish next turn is a trench you did not dig"* objection is answered for exactly the paths that needed it |
| **§5.18's four obstacle kinds** | Unchanged as a vocabulary — but each kind now declares **which paths can produce it**. A moat is path 4; a pillbox is path 1 or 2 |
| **F7 (fortification self-limiting under the field cap)** | **Partly answered.** A laboured or summoned work costs an *action*, not a garrison slot, so it does not compete with bodies the way a manned emplacement does |

**New in the structure seed contract:** an `acquisitionPaths` field — `VALIDATED`, a subset of
`{built, assembled, summoned, laboured}`, `none` illegal. It joins the eleven catalogs in
[structure-seed-ideal.md](structure-seed-ideal.md) §5 as a twelfth.

### 5.25 Rejected, and why

| Rejected | Why |
|---|---|
| **Grid dimensions = `f(DevelopmentLevel)`** | Unrenderable at depth, and it needs a new `f(level)` curve, which the power ladder forbids. Both shipped base-builders surveyed do the opposite |
| **Board size from `LoamStock`** | It is a stockpile, not a rate (`WorldState.cs:144` says so explicitly). The board would **shrink when you spend loam** — punishing you for building |
| **Board size from `DangerBand`** | Sizes what attacks you, not what defends. It belongs on the wave, not the board |
| **A separate authored defense layout beside the sector's slots** | A second authoring surface, a second thing to transfer on capture, and it makes the Well's position a decision that costs nothing. **Closed by owner decision 3 (§0).** The cost it avoided — the full-graph rewrite in §7.1 — is now a cost this design pays |
| **Exponential wave scaling** | Every endless game examined ramps piecewise-linear; `P(Θ)` is already the repo's answer and it is triangular, not geometric |
| **Reusing the lawn Phaser scene directly** | It is lawn-shaped in six files (§3.4). Extracting a generic board layer is real work and should be costed as such, not assumed free |
| **Modelling a tower as an ordinary `BattleActorSetup`** | Three rules reject it (§3.4's box): it would be forced to attack, break the round when it had no legal intent, and keep the battle alive by existing. A combatant-kind discriminator is not optional polish |

---

## 6. Tunables

Every number this introduces. None is a `const`. Proposed home:
`data/tuning/base-defense.v{n}.json`, written by `python tools/tuning/publish.py`, never hand-edited.

| Block | Rows |
|---|---|
| `board` | rows and columns per base tier; cell pixel size; the **structural** comment recording §11.3 exemption |
| `slots.defense` | build slots at development 0; slots gained per development level; the grid-capacity point where slots stop and tier begins |
| `field` | **the concurrent-deployment cap per side**, per base tier — one integer, identical for both sides (§5.9). Plus the between-waves pause, if batch |
| `slots.legion` | **legion slots per side** in the central defense area, per base tier — even numbers (2, 4, …); growth per development level if any. Sized against the game's 6-10 total legions (`world-stage-ideal.md` §8e.3) |
| `legion` | **max members per legion** — the limit §3.6 shows does not exist yet. Author it like `expeditions.v1.json`'s `squadSlots`, never like `WebMatchService`'s `const int maxSquad = 6` |
| `structures` | **Two classes, and mixing them was a defect corrected 2026-09-04.** ① **Magnitudes** — HP, damage — `long`, derived from `P(Θ)`. ② **Board-space and pacing quantities** — range (cells), footprint, build turns — **flat authored tunables, never `P(Θ)`**: a build turn-count growing quadratically means a wall takes hundreds of turns at depth. Build **cost** is also flat, per PS-5: *"within one economy loop, faucet and sink scale on the same read, or neither does"*, and the material faucets are `Θ`-invariant |
| `waves` | attacker composition weights, per-band scaling brackets (piecewise linear, §4.4), arrival forecast fidelity. **Note §3.5: wave composition is a code const today and there is no wave data file at all** — this feature should fix that rather than add a second hand-written array |
| `defense` | the entrenchment multiplier that replaces `PlaceholderBattleResolver.DefenderBonusMilli`; structure-loss consequences |
| `development` | **lives in `data/tuning/loam.v{n}.json`, not here** — beside `upkeep.developmentUpkeepPerLevel`, because `empire-economy-ssot.md`'s A8 invariant (*development must raise yield faster than it raises upkeep*) is a comparison between two numbers and splitting them across files makes it unverifiable by reading |

---

## 7. Costs and constraints this design must pay

Named now so no enrichment round rediscovers them.

1. **The world graph is rewritten in full on every turn commit.** `WriteWorldGraphUnlocked`
   (`RpgStore.World.cs:207`) is preceded by `ClearWorldGraphUnlocked` at every commit
   (`RpgStore.WorldTurns.cs:511-512`). Its own comment says this is fine *"at six sectors… revisit if
   a world ever reaches hundreds."* **A per-sector authored board layout is rewritten in full, every
   turn, for every sector.** That is the load-bearing cost of §5.2.

2. **New hashed state moves goldens — unless it uses the conditional-row precedent.** Adding a cell to
   `WorldCanonical`'s unconditional `sector` or `slot` row moves every world hash at every default
   value. The `faction-scope` pattern (`WorldCanonical.cs:90-94`) avoids that entirely. If a hash does
   move: one const (`WorldWaveOneAcceptanceTests.cs:123`), one ledger paragraph, and six FE fixture
   hashes in `first-light-turn.json`.

3. **A new order kind must pass five plumbing sites** — `WorldCommandKinds`, the `WorldCommand` field,
   `RpgStore.CommandPayload`, `WorldCommandRequest`, and the `WorldEndpoints` submit mapping — plus an
   admission arm and a resolver. `bind-warden` currently fails sites 4 and 5. The store's own comment
   records the precedent: *"Adding one to `WorldCommand` and forgetting it here loses it in the round
   trip … which is exactly how `stance` was found missing."*

4. **The FE board layer is a real build, not a reuse.** §3.4 lists what is lawn-coupled. A
   scene-agnostic layer needs: a `GridSpec` passed rather than imported, a generic entity registry, a
   caller-supplied side/kind→visual mapping, and a `createGame({scenes})` facade.

5. **`stages/` files may not name a `*Dto`.** `contract/contractGuard.ts:57` guards `stages`,
   `layers`, `ui`. A board view type goes in `contract/types.ts` (additive) or repeats the
   `features/`-local pattern the lawn uses.

6. **Slot ownership does not follow sector capture.** `ClaimResolver` captures the sector and never
   touches `WorldSlot.OwnerFactionId`, so a captured sector's slots keep the previous owner. If the
   board is the sector zoomed in, this becomes visible and has to be fixed.

7. **Battle goldens are stricter than world goldens, and there are two sets.** `BattleReport` is
   serialized and SHA-256 hashed as the determinism golden, and `BattleActorSetup`/`BattleActorResult`
   ride into the **expedition** hashes too (`ExpeditionResolverTests.cs:205-208`) — so a report change
   moves 4 + 4. `BattleGoldenTests.cs:11-13` states the protocol: *"A diff here is a determinism break
   or a balance change and MUST be a conscious RulesetVersion/EngineVersion bump, never a silent
   re-bless."* The escape is the `ContentHash`/`Warnings` precedent — `[JsonIgnore(WhenWritingDefault)]`
   plus blanking in `Hash`. `BattleModels.cs:63-68` records that even a **property name** with no value
   moved all four hashes. `RulesetVersion` is currently `4` (`BattleModels.cs:108`).

8. **A structure must not be modelled as an ordinary actor.** §3.4's box is a build constraint, not an
   observation: three separate rules in the round loop and the win condition reject a non-acting
   combatant, and each has to be changed deliberately.

---

## 8. Open questions — owner decisions only

**None.** All twenty-three were answered by the owner across five rounds on 2026-09-04 — see §0.

What remains is not a question but a *scope* choice, and it belongs to `/spec`: the module boundary
between this program and `A10 battle-board` (§5.6) — whether this program builds the grid the action
program then adopts, or binds to `A10` if it lands first. Either is workable; neither is a design
decision.

**Deliberately unset rather than open** — choosing these is a balance pass, not a design gate: legion
slots per side, the field cap, cover values, structure costs and build turns, the repair ratio, and
the per-sector equilibrium band. All are tunables by decision.

**Three prerequisites this ideal hands to `/spec` as costs, not questions:**

1. **A diffing world-graph writer.** Decision 21 multiplies slot rows, and `RpgStore.World.cs:210-212`
   names the trigger by hand. No longer a follow-up.
2. **A resolver supplied at BOTH `RpgStore.WorldTurns.cs:509` and `:603`.** Wiring only `:509` makes
   every re-derived turn report disagree with what happened.
3. **`[JsonIgnore(WhenWritingDefault)]` on every new `BattleReport` field**, blanked in `Hash` — the
   `ContentHash`/`Warnings` precedent. A property *name* alone moved all four goldens once.

---

## 9. What this deliberately does not decide

- **Art.** GG-58's art contract and designed placeholder apply; illustration does not.
- **Wave composition authoring** — what a `GuardWaveId` resolves to is the combat stream's, by owner
  decision recorded at `WorldState.cs:97-100`.
- **Multiplayer or asynchronous PvP.** §4.6's hazard is recorded for the AI-commander analogue only.
- **The homeworld loss penalty** — already an open owner decision in
  [world-graph-ideal.md:474](world-graph-ideal.md), belonging to the world program, not this one.
- **Module boundaries, build order, and acceptance criteria** — that is `/spec`'s job, after §8.

---

## 10. Design-gate checklist — completed honestly

[DESIGN-GATE.md](DESIGN-GATE.md) §5. Where a box cannot be ticked, it says so, because *"an honest gap
costs a sentence; a hidden one costs an hour."*

```
[x] I identified the subsystem(s) this touches.
[~] I read every doc in the §1 row(s) for those subsystems, this session.  -- SEE GAPS BELOW
[x] I checked decisions.md for a lock covering this.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) any constraint I am reporting.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
[x] Corrections are propagated (§3.6).
```

**Read in full this session:** `software-architecture.md` · `world-map-program.md` ·
`standalone-rpg-map.md` · `fe-game-foundation.md` · `DESIGN-GATE.md` ·
`research/genre-mechanics/README.md` + `05-tower-defense-genre.md` · `ssot-power-scale.md` §4 and §11
· `world/spec-sector-development.md` (the development sections).

**Read in part, and named as a gap:** `decisions.md` (topic index plus five rows of ~110 —
*Standalone-first*, *Game GUI*, *Web game profile*, *Lawn position write*, *World turn phase order*) ·
`game-gui-principles.md` (GG-1…GG-14 and the audit table; GG-15…GG-61 by heading only) ·
`design/information-architecture.md` (§1–§9) · `battle-timeline-map.md` (the reconciliation box and
the triage table) · `tunables-ssot.md` (§0–§2) · `economy-principles.md` (§0–§A) ·
`world-graph-ideal.md` (§1–§3, §5–§7, §9–§11) · `world-stage-ideal.md` (§8 decisions only).

**Not read, and relevant if this graduates:** `battle-turn-ideal.md` · `element-hub-ssot.md` ·
`status-ssot.md` · `combat-damage-ssot.md` · `effect-funnel.md` · `empire-economy-ssot.md` (quoted
only second-hand via `spec-sector-development.md`) · `design/README.md`. The combat and effect
subsystems were surveyed **in code** rather than in doc, which satisfies evidence rule 2 (*code beats
documentation*) for the inventory but not the design intent behind them.

**Checked and empty:** `docs/design/` holds no `spec-*.md` covering a board, a base, or a defense
surface — verified against the §1 note that requires checking it even when no row names it.

---

## 11. Adversarial audit — 2026-09-04, four lenses

Four independent audits — economy, playability, engineering, architecture-gate — run against this
document before `/spec`, in the shape [loam-map.md](loam-map.md) §1 established. None was told about the
others. **Every finding below was verified against source by this session**, not accepted on report.

### 11.1 The convergence — four lenses, one decision

**All four independently attacked decision 20 (the whole-city board), and it is why decision 26 revises
it.** Recorded because the convergence is the evidence, not any single finding.

| Lens | Finding | Status |
|---|---|---|
| Economy | **The _defender's_ garrison starves on turn 1.** `SupplyGraph.ConnectedSectors`'s `Usable` excludes any sector where `ZoneOfControl.IsHeldAgainst` is true — so a besieged sector drops out of **its own owner's** supply. Then `LegionSupply.Resolve`: not in supply → `remaining = carried − burn` → `destroyed`. A garrison has no bearers, so carried is 0 | **Closed by 26**, but see 11.2 F1 — the mechanism is still live |
| Economy | **The besieger is never topped up.** The top-up loop is gated on `component.Contains(at)` over sectors the faction **owns**. Budget is one tank: `200·bearers − 10·members·turns` ≈ **one structure per siege** | **Closed by decision 27** — paths 2–4 bypass materials |
| Engineering | **A whole-city board needs a procedural tactical-level generator** with a stability contract — deterministic, stable across turns, slot growth and capture. `world-generator` scale, absent from §7 | **Closed by 26.** A district needs a far weaker generator |
| Playability | **~360–600 unit decisions per engagement**, against AoW4's hard cap of **18 total** citing *"Duration & Mental Load"*, and Arknights' **8** concurrent | **Reduced by 26**; the residual is decision 29's accepted risk |
| Playability | **Turn 4 is turn 1 with less HP.** No positional progress survives; re-engagement is automatic (`MovementPhase.cs` calls `ContactResolver` unconditionally); and `BattleSideOutcome.Routed` *"keeps the field it is on"* — the loser cannot withdraw | **Closed by 26** for the multi-turn case; 11.2 F5 keeps the withdraw gap open |
| Architecture | **The `siege` stage contradicts a locked row**, not a stale doc | **Open — see §11.5** |

### 11.2 Findings that survive decision 26 and change the design

| # | Severity | Finding | Verdict |
|---|---|---|---|
| **F1** | **High** | **A besieged sector still drops out of its owner's supply**, whatever the board's extent. Even a single-engagement siege runs `Pressure` (phase 6) after `Sieges` (phase 3) | **The design changes.** A besieged base needs an explicit supply exemption — *a base with stores is not a legion in the field*. Prerequisite, not follow-up |
| **F1b** | **High** | **Besieging a capital grants the defender map-wide supply immunity.** `if (!connectedByFaction.TryGetValue(...) \|\| connected.Count == 0) continue;` — a faction with no connected sectors skips the burn **entirely** | **Pre-existing defect**, surfaced by this design. Belongs to the loam program; named here so it is not rediscovered |
| **F2** | **Critical** | **`MaxRounds` is global, not per-profile.** `BattleModeProfile` carries `W`, `WScope`, `Commitment`, `PassQuantum`, `WReact`, `RendezvousEnabled`, `ForecastExactness`, `OrdersBySpeed`, `RequiresLiveInput` — **and no round horizon**. Hitting 50 yields `Stalemate`. **`[JsonIgnore]` cannot save this** — it is an engine constant every golden was resolved under | **The design changes.** Move `MaxRounds`/`RoundDurationMs` onto the profile, `classic-round` = today's global so goldens hold byte-for-byte. A named §7 cost |
| **F3** | **High** | **The world seam cannot see a board.** `IBattleResolver.Resolve(request, combatants, seed)` — `combatants` is one or two `WorldEntity` records; `BattleRequest` carries no sector, slots, structures or lanes. `BattleApplication` has two methods, neither applying structure damage | **The doc was wrong** to call this *"a missing argument at every call site"*. It is a **seam widening**. The separate claim that widening moves no golden **does** hold — verified zero hits in `WorldCanonical.cs` and `RpgStore.World*.cs` |
| **F4** | **High** | **The played seat is another program's, and it is specced and building.** `spec-interactive-turns.md`, written the same day, covers T6 + T10 with `decisions_json` appended **per decision**; T11 owns live sessions | **Consume T6/T10/T11, never re-derive.** The largest scope overlap in the document |
| **F5** | **High** | **No withdraw, no concession, no walkover short-circuit.** Grep over 2,300 lines returns nothing. **So the raid — decision 20's own headline justification — has no verb** | **The design changes**, and the mechanism exists at the wrong scale: `BattleRunState.CheckRetreats()` already implements *"the actor leaves the battle alive"* for the `coward` trait. A side-level withdraw is that path plus a `Withdrawn` member on `BattleSideOutcome` — unhashed, so no golden moves |
| **F6** | **High** | **The turn order is a random shuffle of both sides, re-rolled every round.** `BattleEngine.cs:314-336` draws a fresh `InitiativeRng.NextInt(1000)` per actor per round; `classic-round` orders on `(0L, jitter)`. **You cannot execute a two-unit plan** — which contradicts §5.20's Into the Breach thesis this document endorses | **The design changes.** The siege profile row sets `OrdersBySpeed` with **jitter disabled**, and the FE presents a contiguous per-side block. Both are profile-row properties — *"a row, not a branch"* |
| **F7** | **Medium** | **Garrisoning costs a shared field-cap slot**, so the defender spends the scarce shared resource to switch on its own investment. Past `N − k` emplacements, `DevelopmentLevel`'s defense slots buy nothing | **Partly answered by decision 27** — laboured and summoned works cost an *action*, not a slot. The residual is a balance question for the first pass |
| **F8** | **Medium** | **The batch trigger is state-based and turtle-exploitable.** *"The field resolves"* is undefined; one surviving unit behind a rampart blocks the next batch and wins on `MaxRounds`. Every batching game surveyed is **timed** | **The design changes.** Make it *clock, or field cleared, whichever first* — one tunable row |
| **F9** | **Medium** | **Hidden mines contradict the perfect-information framing.** §5.18 kind 4 is *"unrevealed to the other side"*; §5.16 R6 forbids hidden modifiers and §5.20 is built on Into the Breach, which has **zero** hidden information | **Pick one.** Recommend revealed mines (the telegraph model), consistent with R6 |
| **F10** | **Medium** | **`DepletionMilli` cannot carry decision 22.** It is one `int` per **sector**, and a sector routinely carries several producing slots. `empire-economy-ssot.md` §7a **already claims the same field** for spawn depletion | **§5.22's *"a producer, not a schema"* is wrong.** Depletion belongs on the slot, which is a hashed-row change needing the conditional-row precedent |
| **F11** | **Medium** | **Capture transfers the stockpile free.** `ClaimResolver` never touches `LoamStock` — up to 600 per sector. Rare today; base defense makes capture *the* loop | **The design changes**, and the change belongs here because this program makes it load-bearing |
| **F12** | **Medium** | **Decision 21 buys zero economy.** 4 rootbeds + wells = 400/turn against a 300 cap; at equilibrium the marginal producer's entire output is destroyed as overflow | **The design changes.** Capacity must grow alongside slots, or decision 21 gains *slots*, not capacity |

### 11.3 Findings that survive as build costs, not design changes

| # | Finding | Cost |
|---|---|---|
| **C1** | **§3's inventory was surveyed against `HEAD`.** The working tree carries **741 insertions across 15 files** in `Core/Battle`, plus an untracked `World/Growth/`. Four §3 rows are now false — the turn FSM **is** instantiated, profiles **do** resolve, `RequiresLiveInput` **is** read, `TurnEngine.Growth` **is** wired | **Re-run the inventory against the working tree at `/spec`.** §3's *conclusions* all survived spot-checking; its coordinates did not |
| **C2** | **No deterministic grid pathfinder exists.** The only pathfinder is `ReachMap`'s Dijkstra, O(V²·log V) with a per-iteration allocation, justified *"at six sectors"*. Its own comment names the hazard: *"a heap would need the same tie-break written explicitly or a replay could disagree with itself"* | A **new determinism-sensitive build** — heap A*, integer costs, explicit ordinal tie-break |
| **C3** | **A new RNG stream or draw site cannot be hidden by `[JsonIgnore]`.** The working tree's own `RidersRng` shows the correct pattern: *"the method returns before touching any RNG for an empty list… no other stream is perturbed"* | **Fourth §7 prerequisite: every new stream and draw site must be _structurally unreachable_ when the siege feature is absent — an early return, not a defaulted value** |
| **C4** | **`EffectBag.cs:180` defaults `UtcNow` to a real wall clock**, and `WorldDeterminismGuardTests` covers **`Core/World` only**. Four hosts override it by hand | **Extend the guard to `Core/Battle` and `Core/Effects` before a siege host exists.** A live gap in the repo's own coverage |
| **C5** | **Turn-commit cost omits its largest term** — `rpg_world_faction_intel` serializes `slots_json` per (faction × sector), and `Insert` creates a fresh `SqliteCommand` per row | **Measure before choosing a diffing writer.** Statement reuse may recover most of it |
| **C6** | **The FE is world-stage scale or larger.** Measured: the lawn Phaser island is **~2,166 LOC**; `stages/world` is **~6,518**. §7.4's four bullets cover the generic board layer only — the smaller half | Budget **5–8k LOC**. The lawn island is a reference implementation to read, not to reuse |
| **C7** | **Batch waves land on the kernel baseline's one named hole** — *"a wave spawn that arms 200 events on one tick would drain all 200 in a single frame"* | Bounded/resumable drain is a **prerequisite of batch waves** |

### 11.4 Corrections to this document's own claims — six, all verified

Recorded in full because the pattern matters more than any single error: **four of six were citing a
comment or a single line instead of reading the code or the surrounding section** — the failure
`DESIGN-GATE.md` evidence rules 2 and 3 exist to prevent, committed by the session that quoted them.

| # | Claim as written | Truth |
|---|---|---|
| 1 | `BattleEngine.Resolve` has no player-input seam | **It has one** — an eighth parameter, `IIntentSource? intentSource`, omitted at all three production call sites. *Favourable* error |
| 2 | *"World replay reuses the stored record rather than re-simulating"* | **`RpgStore.WorldTurns.cs:599-606` re-simulates from turn zero with no resolver.** Recorded in §2 rule 7's box |
| 3 | *"`LegionSupply` is unwired"* | **Wired at `TurnEngine.cs:236`.** The doc quoted a stale class comment |
| 4 | *"`Resolve`'s profile is never referenced in the method body"* | **It is read** — *"B37: the profile is now READ"*. The doc quoted the doc comment, not the body |
| 5 | *"A unit that moves does not also strike that turn"* | **The opposite of the source.** `action-map.md:430`'s heading is *"Move and attack: two separate actions, and the clock decides whether you get both"* — and the doc cited the wrong file (`action-corpus-ideal.md`) |
| 6 | §0 carried a duplicated, text-stripped Round 3 block | A broken shell heredoc ate the backticked names. **Removed** |

**Systematic drift, also recorded:** every `BattleEngine.cs` / `BattleRunState.cs` citation in §3 is
30–160 lines stale, entirely because of C1's uncommitted wave. **The claims survived every spot-check;
the coordinates did not** — and a wrong location is worse than none for a downstream session.

### 11.5 Open, and owed before `/spec`

**The `siege` stage contradicts a locked row, and this document booked it as documentation drift.**

`decisions.md`'s **Game GUI** row (locked 2026-08-22): *"the 20 flat routes become **4 stages**, 8 layers
and 1 gated tree… Rules GG-1…GG-61 are binding… with **20 CI checks**."* And design decision **D2** at
`game-gui-principles.md:965`: *"**Four stages, one at a time.**"*

`AGENTS.md`: *"Architecture changes that lock behavior need `decisions.md` first."*

**A `decisions.md` amendment is owed before `/spec`, and the CI checks behind the four-stage count have
to be costed.** §10's checklist ticked *"I checked decisions.md for a lock covering this"* — and its own
honest gap explains the miss: `game-gui-principles.md` was read *"GG-1…GG-14 and the audit table;
GG-15…GG-61 by heading only"*, and D2 lives at `:965`.

### 11.6 Verified compliant — stated once, not padded

Cover as a **flat dodge delta reading `Θ`** (the strongest ladder reasoning in the document, and
independently confirmed by a shipped CoH2 rebalance: damage-cover changes shots-to-kill, accuracy-cover
does not) · the **grid dimension as a §11.3 board cap** with the `MaxLivingPlants` precedent and an
uncapped tower-tier escape valve · **sector capacity priced, not capped**, citing the `MaxSlots`
deletion by name · **no fifth `WhoKind`**, with the correct bar and the correct reason ·
**`SectorPhase.Besieged` left unused** as derivable state · **standalone-first** — a web-mode board, PvZ
nowhere on the critical path · the **`docs/design/` check**, re-verified independently: nine
`spec-*.md`, none covering a board, base or defense surface · and **§3.2–§3.4's inventory is
substantively accurate everywhere probed**, including its hardest claim — the three round-loop rules
that reject a non-acting combatant.

---

## 12. Next step

`/spec` for a capability map plus module specs — **after the three questions in §8 are answered.**
Module ids, dependency direction and build order are not written here on purpose: getting a map wrong
is expensive and reviewing it is not.
