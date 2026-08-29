# Plan: buff-debuff-scope program

Source: [buff-debuff-scope-map.md](../docs/architecture/buff-debuff-scope-map.md) (4 modules, approved)
and its four specs under [buff-debuff-scope/](../docs/architecture/buff-debuff-scope/), all written and
adversarially audited 2026-08-29 (see each spec's own "Corrected/Resolved during audit" sections — three
real findings were caught and fixed before this plan was drafted, not left for implementation to
discover: `battlefield-scope`'s Live/Sim host conflation, `membership-events`' overclaimed existing
handling, and `scope-model`'s relation-type extraction target).

Task list: [buff-debuff-scope-todo.md](buff-debuff-scope-todo.md). Paths are prefixed per this repo's
parallel-programs convention — `tasks/plan.md`/`tasks/todo.md` belong to the perf stream.

---

## 1. Shape of the work

**4 modules, 4 phases, 13 tasks + 1 owner-gated LIVE checkpoint.** Every task is one complete
resolve-and-verify path, not a horizontal layer.

```text
Phase 1  scope-model          T1-T4    (foundational — no seam, first)
    |
Phase 2  membership-events    T5-T6    (independent of scope-model; sequenced here so
    |                                   battlefield-scope's own-side task has it ready)
Phase 3  battlefield-scope    T7-T11   (needs scope-model; own-side task needs membership-events)
    |
Phase 4  world-map-scope      T12-T13  (needs scope-model only; independent of Phases 2-3)
```

### 1.1 Orderings that matter, and why

1. **`scope-model` before everything.** Every other module either executes a scope
   (`battlefield-scope`, `world-map-scope`) or feeds one (`membership-events`, indirectly — it emits
   events `battlefield-scope` reacts to). None can be typed or tested without the WHERE/WHO vocabulary
   existing first. No partial version a dependent module could build against — matches this repo's own
   `P0.1`-shaped precedent (`action-plan.md` §1.1: *"no seam, this one really is first"*).
2. **`membership-events` before `battlefield-scope`'s own-side task, not before the whole module.**
   `battlefield-scope`'s target/type/unique-demon WHO-values (T7) need nothing from `membership-events`
   and could ship first if reordered — this plan sequences `membership-events` as Phase 2 anyway so
   Phase 3 never has an internal wait, but the dependency itself is narrow (T8 only), matching this
   session's own `T29 *(after P0.5)*`-shaped annotation rather than forcing a phase split.
3. **`world-map-scope` last, but not because it is blocked on anything.** It depends only on
   `scope-model`. It is sequenced after `battlefield-scope` because it is explicitly the
   least-precedented module (crosses `DESIGN-GATE.md`'s World map caution under owner authorization) —
   building the well-precedented modules first means a real surprise in the newer one doesn't stall
   everything behind it. **This is a sequencing preference, not a dependency** — reorder freely if it
   helps.

### 1.2 What's explicitly not in this plan

Aura skill content and magnitude math; the commander concept itself (Zomboss/Crazy Dave identity,
roster, "player-first commander"); `world-buff.*` content authoring; "commander joins battle directly"
as a combat participant in expeditions/world-map/web-RPG. All of this is the ideal document's own §5 —
restated here so the boundary travels with the plan, matching this program's own established discipline.

---

## 2. Phases

### Phase 1 — `scope-model` (T1-T4)

The vocabulary: `WhereScope` (Battlefield/WorldMap), `WhoSelector` (target/type/unique-demon/relation),
the `(kind, where, who, host)` compatibility table, and the `RelationKind` extraction into
`FusionRpg.Contracts` this module's own audit surfaced as real, necessary, shipped-code-touching work
(not an implementation detail to discover later).

### Phase 2 — `membership-events` (T5-T6)

Two real transitions: `Bound`/`Cleared` (already-correct FSM points in `UniqueBindings.cs`, this module
only adds an event) and `MindControlToggled` (the audit found this is a genuinely new consumer of an
event — `zombie.hypno` — that already arrives at `MatchRuntime.cs` but has only ever been a placeholder
comment there, never real handling).

### Phase 3 — `battlefield-scope` (T7-T11)

Two hosts sharing one grant-issuing front end, per the audit's central finding: SIM (new
`BattleEffectHost` reader wiring, this session's own A18a-e pattern reused a fourth time) and live PvZ
(no new reader — the injector's own overlay/Funnel path already works, proven by patron.aura; this
module's live-PvZ job is grant-shape correctness only). Closes with a LIVE gate matching patron-demon's
own precedent — SIM-passing is tracked as done; the LIVE gate is owner-only and does not block calling
the SIM half complete. **Resolved, owner, 2026-08-29:** asked directly whether to run the LIVE gate now
or track it separately — the owner chose to track it separately, explicitly matching `patron-demon`'s
own standing "SIM shipped, LIVE owner gate open" status rather than treating it as a program blocker.

### Phase 4 — `world-map-scope` (T12-T13)

A named, hashed, per-mille field on `WorldFaction`, following `UpkeepHandicapMilli`'s exact precedent —
confirmed via the audit to be genuinely all this module needs to do, since `UpkeepHandicapMilli` itself
has no single "compute path" (three independent consumers read it, each on its own), meaning this module
declares and hashes the modifier without needing to wire itself into any future consumer.

---

## 3. Checkpoints — all reporting, none blocking

Matching this repo's own established shape (`action-plan.md` §0.1): a checkpoint means *run these
commands, record the result, continue*. The only stop is a failing test.

| After | Record | Red means |
|---|---|---|
| **Phase 1** | compatibility table resolves the G8 case differently for `Live` vs `Sim`; purity + architecture guards green; `RelationKind` extraction leaves `~ActionTargeting` unmoved | the host dimension isn't real, or the extraction broke shipped targeting |
| **Phase 2** | `MatchRuntime`'s 4 existing dispatch cases unmoved; new hypno case proven both directions | a regression in shipped dispatch, or one-directional hypno handling |
| **Phase 3** | all 4 WHO values resolve correctly on SIM; G8 case confirmed live-only; full suite + 8 goldens unmoved; **LIVE gate tracked separately, owner-only** | a scope reaching the wrong population, or a golden moving with nothing new wired into any path a golden exercises |
| **Phase 4** | hash round-trips both directions; replay byte-identical across two runs; `WorldDeterminismGuardTests` green | the modifier is invisible to replay, or mutates in place instead of via `with` |

---

## 4. Risks

**`battlefield-scope`'s live-PvZ half is unverified against a real match until the LIVE gate runs.**
SIM passing is real proof of the grant-issuing logic, but the injector's own overlay path reading these
specific grants correctly is only proven by precedent (patron.aura), not by this program's own tests.
Matches patron-demon's own accepted risk shape exactly — flagged, not hidden. **Resolved, owner,
2026-08-29:** this cannot be executed or observed by an assistant session (it needs a human watching a
real, rendered game window) — asked directly, and the owner chose to track it separately rather than
treat it as a blocker, the same standing shape `patron-demon` has carried for over a week in this repo.

**`world-map-scope` crosses a real, standing caution** (`DESIGN-GATE.md` §1, World map row) under
explicit owner authorization scoped to this program only. A `decisions.md` line recording that
authorization is still outstanding — worth adding before or during Phase 4, not left implicit.

**The `RelationKind` extraction (T1) is the one task in this plan editing code outside this program's own
new files.** Its own regression check (`~ActionTargeting` suite green, T6-T8's fixtures unmoved) is
already named in `scope-model`'s spec — treat a moved fixture there as a stop-and-report, the same
discipline this session used throughout the action program.

---

## 5. Deferred, and why

Same four items named throughout this program's own documents, restated once more so the plan carries
its own boundary:

| Item | Why |
|---|---|
| Aura skill content + magnitude math | explicit owner sequencing — "later discuss" |
| Commander concept (Zomboss/Crazy Dave identity, roster) | explicit owner sequencing — "later discuss" |
| `world-buff.*` content authoring | no aura content exists yet to author |
| "Join battle directly" (expeditions/world-map/web-RPG combat participant) | a different, unscoped future capability — this program only builds the scope primitive |
