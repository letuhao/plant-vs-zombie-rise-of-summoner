# Spec: `aura-delivery-path`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Audit:** [../derived-pipeline-audit-2026-08-30.md](../derived-pipeline-audit-2026-08-30.md)
**Status:** 2026-08-30. ⛔ **SPIKE FIRST — this document does not yet specify a build.**

> **Why this module is different from the other eight.** Every other spec in this program describes
> work whose shape is known. This one describes a question nobody has answered: **there is no path in
> the tree from "an aura is on" to "a derived channel moved."** Writing an implementation spec before
> answering it would be writing fiction — and the first version of this program did exactly that,
> across seven documents, which is why this one exists.

---

## 1. The question

An aura must, on some host, cause a named derived channel on a friendly entity to change while it is
enabled, and change back when it is not.

**No mechanism in the tree does this today.** Four independent refusals, each verified:

| # | Refusal | Evidence |
|---|---|---|
| 1 | `stat.derived` is `RuntimeState.None` on **Lawn and Sim** | `AtomKindRegistry.cs:149`; `BindGate` rejects `None` with `RuntimeUnsupported` — a lawn bind **fails** |
| 2 | `stat.derived` carries **`AtomTriggers.None`** | `AtomKindRegistry.cs:150` — there is no trigger vocabulary, so **nothing can toggle it** |
| 3 | `ScopeCompatibility` has **no `stat.derived` row** | `ScopeCompatibility.cs:48-70` is four rows; anything unlisted throws `ScopeUnsupportedException` |
| 4 | The battle sink has no arm for it | `BattleEffects.cs:135` — *"battle mode consumes ApplyResourceDelta (FA10) / ApplyStatus (FA2) / ModifyStat (FA1) only"* |

And the two paths that *do* carry derived values both have a disqualifying property:

- **`TraitAtomSource.FromContainers`** (`Battle/TraitAtomSource.cs:55-90`) — the one shipped
  `stat.derived` consumer — accepts only `ContainerKind.Trait` with a `trait.` prefix, and reads
  `amount` via **`TryGetInt32` on authored params JSON** (`data/seed/atoms/trait-critical-hunter.json`
  shows the shape: `"amount": 150`). **A runtime-computed magnitude structurally cannot travel this
  path.** That kills the two-axis formula on this route.
- **`BattleSetup.ChannelMods` → `BattleStatComposer.Compose(setup)`** — the only runtime-computed
  derived path in battle — is called **once, at `BattleEngine` construction** (`BattleEngine.cs:30`).
  **Derived channels are frozen for the match.**

⛔ **That last line is the crux.** If delivery rides `ChannelMods`, then **mid-run toggling is
impossible in battle**, and two things the program currently assumes are false:
`aura-action-shape` §5.5's explicitly-allowed mid-run toggle, and the map's acceptance rule
(*"disabling returns the channel to its prior value"*).

**Also missing, independently:** there is no production own-side oracle. `BattlefieldOwnSideReactor` —
the only `RelationKind` delivery mechanism in the repo — has **one wiring in the entire tree**:
`Injector/Effects/DebugScopeRuntime.cs:39`, reached from a debug cheat, using `AlwaysRelationOracle`,
whose own doc says it is *"Deliberately NOT the real specimen-ownership bridge"* (`:11-14`).

---

## 2. ⛔ The spike — run this before writing anything else

**Goal:** move one number, end to end, on one host — then try to turn it off.

**Scope, deliberately minimal:**

- **One aura**, hardcoded. No content pipeline, no container, no authoring.
- **One channel**: `combat.power.omni`.
- **One host**: the **web battle engine** (`decisions.md:92` makes battle mandatory anyway, and it is
  the only host where `stat.derived` is `Full`).
- **One friendly actor**, resolved however is cheapest — do **not** build an own-side oracle for this.
- **Timeboxed.** If it is not moving a number within the box, that outcome is itself the answer.

**The two questions it must answer, in order:**

1. **Does a runtime-computed value reach a squad actor's `combat.power.omni` at all?** The likely route
   is `BattleSetup.ChannelMods` — `BattleStatComposer.cs:128-133` applies them and is **side-blind**,
   so this half is expected to work. Confirm it, and confirm the value survives to a real combat
   resolution rather than merely into the snapshot.
2. **Can it then be turned off mid-match?** This is the real question. `Compose` runs once at
   construction, so the honest sub-questions are: is there a recompose seam? Can one be added without
   disturbing determinism or goldens? Or is battle structurally match-frozen?

**Deliverable:** a short findings note (not a spec) recording which of the outcomes below happened, with
`file:line` for whatever blocked or allowed it.

### The decision matrix — what each outcome costs downstream

| Outcome | What it means | Downstream rewrites |
|---|---|---|
| **A — `ChannelMods` works, and a recompose seam is cheap** | Best case. Auras are `ChannelMod` producers; the bucket, the toggle model and the acceptance rule all stand | none — write the implementation spec |
| **B — `ChannelMods` works, but the match is frozen** | Auras are chosen **at match start** and cannot be toggled mid-run | ⛔ `aura-action-shape` loses enable/disable/eviction entirely — it becomes a *pre-match loadout choice*. The acceptance rule's "disabling returns it" half is deleted. `aura-surface` loses the toggle UI. **This is the most likely outcome and it is a large rewrite** |
| **C — `ChannelMods` is the wrong seam; `stat.derived` must be un-quarantined** | The runtime matrix, the trigger vocabulary, `ScopeCompatibility` and a sink arm all become in-scope | `aura-delivery-path` becomes an **atom-layer program**, reviewed against the closed vocabulary. Multi-module. Everything downstream waits |
| **D — nothing works without an own-side oracle** | The `RelationKind` half is the blocker, not the channel half | A production `IOwnSideOracle` becomes a prerequisite module. Note `buff-debuff-scope-todo.md:249-250` already records this as *"real, unscoped, additional work"* |

**Outcome B is the one to expect**, and it is the one that most changes the program. Planning around A
without evidence is how this program got its first seven specs wrong.

---

## 3. What this module will own, once the spike answers it

Whichever outcome lands, this module owns **the seam** — the single place where "an aura is active"
becomes "a channel has a value." Its shape is unknown; its responsibility is not:

- The **runtime-support decision** for whatever atom kind or seam is chosen (a change to a closed
  vocabulary is a reviewed change, per `definitions.md`).
- The **toggle semantics** the chosen seam can actually support — including, if outcome B, honestly
  reporting that there are none.
- The **own-side resolution** for `RelationKind.Ally`, or an explicit decision to defer it and use a
  narrower selector for v1.
- **The lawn as a separate, later question.** `stat.derived` is `None` there and a lawn consumer does
  not exist; the standalone charter makes battle the required host regardless.

---

## 4. Boundaries

**Always**
- Run the spike before specifying. This module's entire reason for existing is that the answer is
  unknown.
- Record the outcome with `file:line`, including negative results.
- Treat "battle is match-frozen" as a **legitimate answer**, not a failure to engineer around.

**Ask first**
- Any change to `AtomKindRegistry`'s runtime matrix, trigger vocabulary, or `ScopeCompatibility` — all
  three are closed vocabularies where *"adding one is a reviewed change, not a convenience."*
- Adding a recompose seam to `BattleEngine`. It is deterministic, golden-tested kernel code.

**Never**
- Write the implementation spec before the spike reports.
- Route a runtime-computed magnitude through `TraitAtomSource`'s authored-`int32` `amount` field.
- Ship `AlwaysRelationOracle` as a production own-side oracle. Its own comment forbids it.

---

## 5. Success criteria — for the spike, not for a build

- [ ] A runtime-computed value provably reaches `combat.power.omni` on a friendly squad actor in a web
      battle, and affects a real combat resolution.
- [ ] The mid-match toggle question is **answered with evidence** — seam found, seam addable, or
      structurally frozen.
- [ ] The findings note names which of outcomes A–D occurred, with citations.
- [ ] The downstream rewrites that outcome implies are listed, so the map can be re-cut once rather
      than drifted into.

## 6. Open questions

1. **Does outcome B end the toggle design entirely, or does the lawn offer a toggleable path later?**
   The lawn is `RuntimeState.None` today, so this is not a fallback — it is a second, larger question.
2. **If a recompose seam is addable, what is its cost to determinism and goldens?** `BattleEngine` is
   *"a batch resolver, not a live per-frame loop"* — a recompose is not obviously a small change.
